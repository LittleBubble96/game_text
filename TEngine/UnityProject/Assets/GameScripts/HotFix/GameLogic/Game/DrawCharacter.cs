using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using GameLogic.Data;
using TEngine;
using UnityEngine;

namespace GameLogic.View
{

    public class DrawCharacter : MonoBehaviour
    {
        private List<GameObject> _strokeObjects = new List<GameObject>();
        private Color _defaultStrokeColor = Color.white;
        private readonly List<Mesh> _ownedMeshes = new List<Mesh>();
        private readonly List<Material> _ownedMaterials = new List<Material>();
        private readonly List<Vector2[]> _cachedPoints = new List<Vector2[]>();
        private string _cachedCharacter;
        private Vector2 _cachedOffset;
        private bool _cachedIndices;
        private bool _hasCachedGeometry;
        private int _drawVersion;
        private const double DrawFrameBudgetSeconds = 0.003;

        public void CancelPendingDraw() => _drawVersion++;

        private static void ReleaseObject(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        private void ReleaseGeometry()
        {
            foreach (var mesh in _ownedMeshes) ReleaseObject(mesh);
            foreach (var material in _ownedMaterials) ReleaseObject(material);
            _ownedMeshes.Clear();
            _ownedMaterials.Clear();
            _cachedPoints.Clear();
            _hasCachedGeometry = false;
        }

        private void OnDestroy()
        {
            CancelPendingDraw();
            ReleaseGeometry();
        }

        private bool CanReuse(TextGraphicData data, bool showIndices)
        {
            if (!_hasCachedGeometry || _cachedCharacter != data.character ||
                !_cachedOffset.Equals(PositionOffset) || _cachedIndices != showIndices ||
                _cachedPoints.Count != data.strokes.Count || _strokeObjects.Count != data.strokes.Count)
                return false;
            for (int i = 0; i < data.strokes.Count; i++)
            {
                var points = data.strokes[i].points;
                var cached = _cachedPoints[i];
                if (points.Count != cached.Length || (points.Count >= 3 && _strokeObjects[i] == null))
                    return false;
                for (int j = 0; j < points.Count; j++)
                    if (!points[j].Equals(cached[j])) return false;
            }
            return true;
        }

        /// <summary>笔画材质模板资源名（AddressByFileName 规则，放 AssetRaw/Materials 下）。
        /// 用材质资源而非 Shader.Find，确保打包后 shader 引用被静态收集、运行时不会丢失。</summary>
        private const string StrokeMaterialPath = "StrokeMaterial";

        /// <summary>笔画材质模板（全局共享，loading 阶段异步预加载，Draw 时直接取用）</summary>
        private static Material _strokeMaterialTemplate;

        public List<GameObject> StrokeObjects => _strokeObjects;

        public Color DefaultStrokeColor
        {
            get => _defaultStrokeColor;
            set => _defaultStrokeColor = value;
        }

        /// <summary>
        /// 笔画渲染时的位置偏移
        /// </summary>
        public Vector2 PositionOffset { get; set; }

        /// <summary>
        /// 清除所有已绘制的笔画
        /// </summary>
        public void Clear()
        {
            CancelPendingDraw();
            ReleaseGeometry();
            _strokeObjects.Clear();
            int childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                ReleaseObject(child);
            }
        }

        /// <summary>
        /// 画一个汉字（传入你解析好的 TextGraphicData）。
        /// 异步：取材质模板需 await（模板在 loading 阶段预加载，此处通常直接命中缓存）。
        /// </summary>
        public async UniTask DrawAsync(TextGraphicData data, bool showStrokeIndices = false)
        {
            CancelPendingDraw();
            if (CanReuse(data, showStrokeIndices))
            {
                ResetAllStrokeColors();
                foreach (var stroke in _strokeObjects)
                {
                    if (stroke == null) continue;
                    stroke.transform.localPosition = Vector3.zero;
                    stroke.SetActive(true);
                }
                return;
            }
            Clear();
            int version = _drawVersion;

            Material template = await GetStrokeMaterialTemplateAsync();
            if (this == null || version != _drawVersion) return;
            if (template == null) return;

            double batchStart = Time.realtimeSinceStartupAsDouble;
            for (int i = 0; i < data.strokes.Count; i++)
            {
                // 异步加载之后的网格/碰撞体创建仍在主线程；按时间预算让出整帧。
                // 每次恢复都检查版本，防止快速切关/退出后继续创建旧笔画。
                if (Application.isPlaying && i > 0 &&
                    Time.realtimeSinceStartupAsDouble - batchStart >= DrawFrameBudgetSeconds)
                {
                    await UniTask.NextFrame();
                    if (this == null || version != _drawVersion) return;
                    batchStart = Time.realtimeSinceStartupAsDouble;
                }
                int strokeIndex = i;
                List<Vector2> points = data.strokes[i].points;

                Vector2[] sourcePoints = points.ToArray();
                _cachedPoints.Add(sourcePoints);
                if (points.Count < 3)
                {
                    // 保留原始笔画索引，避免后续笔画选中错位。
                    _strokeObjects.Add(null);
                    continue;
                }

                // 1. 创建笔画物体
                GameObject strokeObj = new GameObject($"Stroke_{strokeIndex}");
                strokeObj.transform.SetParent(transform, false);
                strokeObj.transform.localPosition = Vector3.zero;
                _strokeObjects.Add(strokeObj);

                // 2. 组件
                MeshFilter mf = strokeObj.AddComponent<MeshFilter>();
                MeshRenderer mr = strokeObj.AddComponent<MeshRenderer>();

                // 3. 默认材质：克隆材质模板（保留逐笔画独立着色），shader 引用随模板资源打包进包
                Material mat = new Material(template);
                _ownedMaterials.Add(mat);
                mat.color = _defaultStrokeColor;
                mr.sharedMaterial = mat;

                // 4. 生成实心Mesh（应用位置偏移）
                Mesh mesh = new Mesh();
                _ownedMeshes.Add(mesh);
                Vector3[] verts = new Vector3[points.Count];
                for (int p = 0; p < points.Count; p++)
                    verts[p] = new Vector3(points[p].x + PositionOffset.x, points[p].y + PositionOffset.y, 0);
                Triangulator tr = new Triangulator(sourcePoints);
                int[] triangles = tr.Triangulate();
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    int second = triangles[j + 1];
                    triangles[j + 1] = triangles[j + 2];
                    triangles[j + 2] = second;
                }

                mesh.vertices = verts;
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                mf.sharedMesh = mesh;

                // 5. 添加2D碰撞器（应用位置偏移）
                PolygonCollider2D collider = strokeObj.AddComponent<PolygonCollider2D>();
                Vector2[] simplified = SimplifyColliderPoints(points);
                for (int k = 0; k < simplified.Length; k++)
                    simplified[k] += PositionOffset;
                collider.points = simplified;

                // 6. 标注笔画索引
                if (showStrokeIndices)
                {
                    AddStrokeIndexLabel(strokeObj, strokeIndex, points);
                }
            }
            _cachedCharacter = data.character;
            _cachedOffset = PositionOffset;
            _cachedIndices = showStrokeIndices;
            _hasCachedGeometry = true;
        }

        /// <summary>
        /// 在 loading 阶段异步预加载笔画材质模板（全局静态缓存，所有 DrawCharacter 共享）。
        /// 这样 Draw 时无需同步加载、不卡帧，且 shader 引用随材质资源打包进包。
        /// </summary>
        public static async UniTask PreloadStrokeMaterialAsync()
        {
            if (_strokeMaterialTemplate != null) return;

#if UNITY_EDITOR
            _strokeMaterialTemplate = new Material(Shader.Find("Unlit/Color"));
            await UniTask.Yield();
#else 
            _strokeMaterialTemplate = await GameModule.Resource.LoadAssetAsync<Material>(StrokeMaterialPath);
            if (_strokeMaterialTemplate == null)
            {
                Log.Error($"[DrawCharacter] 笔画材质模板预加载失败: {StrokeMaterialPath}，回退到内置 Unlit/Color");
                _strokeMaterialTemplate = new Material(Shader.Find("Unlit/Color"));
            }
#endif
        }

        /// <summary>
        /// 取笔画材质模板（异步）。模板已在 loading 阶段预加载，通常直接返回缓存；
        /// 用 await UniTask.Yield() 保持异步契约，调用方据此编排顺序，不引入任何同步加载。
        /// </summary>
        private static async UniTask<Material> GetStrokeMaterialTemplateAsync()
        {
            await UniTask.Yield();
            if (_strokeMaterialTemplate == null)
            {
                Log.Error($"[DrawCharacter] 笔画材质模板未预加载: {StrokeMaterialPath}，请确认 loading 阶段调用了 PreloadStrokeMaterialAsync");
            }
            return _strokeMaterialTemplate;
        }

        /// <summary>
        /// 给笔画的起始点添加索引标签
        /// </summary>
        private void AddStrokeIndexLabel(GameObject strokeObj, int index, List<Vector2> points)
        {
            // 计算笔画中心点作为标签位置（含偏移）
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Count;
            center += PositionOffset;

            GameObject labelObj = new GameObject($"Idx_{index}");
            labelObj.transform.SetParent(strokeObj.transform);
            labelObj.transform.localPosition = center;

            TextMesh tm = labelObj.AddComponent<TextMesh>();
            tm.text = index.ToString();
            tm.fontSize = 40;
            tm.characterSize = 0.04f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.color = Color.red;
            tm.alignment = TextAlignment.Center;
            tm.fontStyle = FontStyle.Bold;

            // 给标签一个白色背景一样的 quad，使其在任何颜色笔画上都可见
            MeshRenderer labelRenderer = labelObj.GetComponent<MeshRenderer>();
            if (labelRenderer != null)
            {
                labelRenderer.sortingOrder = 10;
            }
        }

        #region Collider Optimization

        private const float MinVertexDistance = 0.02f;
        private const int MaxColliderVertices = 200;

        private Vector2[] SimplifyColliderPoints(List<Vector2> points)
        {
            if (points.Count < 3)
                return new Vector2[0];

            List<Vector2> result = new List<Vector2>(points);
            if (result.Count >= 3)
            {
                Vector2 first = result[0];
                Vector2 last = result[result.Count - 1];
                if (Mathf.Approximately(first.x, last.x) && Mathf.Approximately(first.y, last.y))
                {
                    result.RemoveAt(result.Count - 1);
                }
            }

            List<Vector2> filtered = new List<Vector2>();
            filtered.Add(result[0]);
            float sqrMinDist = MinVertexDistance * MinVertexDistance;

            for (int i = 1; i < result.Count; i++)
            {
                if ((result[i] - filtered[filtered.Count - 1]).sqrMagnitude >= sqrMinDist)
                {
                    filtered.Add(result[i]);
                }
            }

            if (filtered.Count >= 3)
            {
                if ((filtered[0] - filtered[filtered.Count - 1]).sqrMagnitude < sqrMinDist)
                {
                    filtered.RemoveAt(filtered.Count - 1);
                }
            }

            if (filtered.Count > MaxColliderVertices)
            {
                List<Vector2> sampled = new List<Vector2>(MaxColliderVertices);
                float step = (float)(filtered.Count - 1) / (MaxColliderVertices - 1);
                for (int i = 0; i < MaxColliderVertices; i++)
                {
                    int idx = Mathf.RoundToInt(i * step);
                    if (idx >= filtered.Count)
                        idx = filtered.Count - 1;
                    sampled.Add(filtered[idx]);
                }

                filtered = sampled;
            }

            return filtered.ToArray();
        }

        #endregion

        /// <summary>
        /// 设置指定索引笔画的颜色
        /// </summary>
        public void SetStrokeColor(int index, Color color)
        {
            if (index < 0 || index >= _strokeObjects.Count) return;
            GameObject strokeObj = _strokeObjects[index];
            if (strokeObj == null) return;
            MeshRenderer mr = strokeObj.GetComponent<MeshRenderer>();
            if (mr != null && mr.sharedMaterial != null)
            {
                mr.sharedMaterial.color = color;
            }
        }

        /// <summary>
        /// 重置所有笔画颜色为默认颜色
        /// </summary>
        public void ResetAllStrokeColors()
        {
            foreach (var strokeObj in _strokeObjects)
            {
                if (strokeObj == null) continue;
                MeshRenderer mr = strokeObj.GetComponent<MeshRenderer>();
                if (mr != null && mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.color = _defaultStrokeColor;
                }
            }
        }
    }
}
