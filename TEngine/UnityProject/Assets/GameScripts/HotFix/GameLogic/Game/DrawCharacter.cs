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
        private readonly List<MeshRenderer> _strokeRenderers = new List<MeshRenderer>();
        private Color _defaultStrokeColor = Color.white;
        // 只保存本视图的峰值笔画数量；切字时复用，视图销毁时统一释放。
        private sealed class StrokeSlot
        {
            public GameObject Object;
            public MeshFilter Filter;
            public MeshRenderer Renderer;
            public PolygonCollider2D Collider;
            public Mesh Mesh;
            public Material Material;
            public TextMesh IndexLabel;
            public Vector2[] SourcePoints;
            public readonly List<Vector3> Vertices = new List<Vector3>();
        }
        private readonly List<StrokeSlot> _strokePool = new List<StrokeSlot>();
        private readonly HashSet<GameObject> _pooledObjects = new HashSet<GameObject>();
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
            foreach (var slot in _strokePool)
            {
                ReleaseObject(slot.Mesh);
                ReleaseObject(slot.Material);
            }
            _strokePool.Clear();
            _pooledObjects.Clear();
            _strokeObjects.Clear();
            _strokeRenderers.Clear();
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

        public float CustomScale { get; set; } = 1f;

        /// <summary>
        /// 清除显示与当前字形缓存，笔画资源留在局部池中。
        /// </summary>
        public void Clear()
        {
            CancelPendingDraw();
            _strokeObjects.Clear();
            _strokeRenderers.Clear();
            _cachedPoints.Clear();
            _hasCachedGeometry = false;
            int childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                var child = transform.GetChild(i).gameObject;
                child.SetActive(false);
                // 编辑器重载后可能留下旧预览子物体，仍按原逻辑清理非池对象。
                if (!_pooledObjects.Contains(child)) ReleaseObject(child);
            }
        }

        private StrokeSlot GetStrokeSlot(int index)
        {
            while (_strokePool.Count <= index) _strokePool.Add(new StrokeSlot());
            return _strokePool[index];
        }

        private void PrepareStroke(StrokeSlot slot, int index, Material template)
        {
            if (slot.Object == null || slot.Mesh == null || slot.Material == null)
            {
                _pooledObjects.Remove(slot.Object);
                ReleaseObject(slot.Object);
                ReleaseObject(slot.Mesh);
                ReleaseObject(slot.Material);
                slot.Object = new GameObject($"Stroke_{index}");
                slot.Object.SetActive(false);
                slot.Object.transform.SetParent(transform, false);
                slot.Filter = slot.Object.AddComponent<MeshFilter>();
                slot.Renderer = slot.Object.AddComponent<MeshRenderer>();
                slot.Collider = slot.Object.AddComponent<PolygonCollider2D>();
                slot.Mesh = new Mesh();
                slot.Mesh.MarkDynamic();
                slot.Material = new Material(template);
                slot.IndexLabel = null;
                _pooledObjects.Add(slot.Object);
            }
            else
            {
                if (slot.Material.shader != template.shader) slot.Material.shader = template.shader;
                slot.Material.CopyPropertiesFromMaterial(template);
            }

            // 清掉上一个字的选择偏移、引导层级和颜色，保持首次创建时的状态。
            slot.Object.layer = 0;
            slot.Object.transform.localPosition = Vector3.zero;
            slot.Object.transform.localRotation = Quaternion.identity;
            slot.Object.transform.localScale = Vector3.one;
            slot.Renderer.sortingLayerID = 0;
            slot.Renderer.sortingOrder = 0;
            slot.Renderer.enabled = true;
            slot.Collider.enabled = true;
            slot.Material.color = _defaultStrokeColor;
            slot.Renderer.sharedMaterial = slot.Material;
            slot.Filter.sharedMesh = slot.Mesh;
        }

        /// <summary>
        /// 画一个汉字（传入你解析好的 TextGraphicData）。
        /// 异步：取材质模板需 await（模板在 loading 阶段预加载，此处通常直接命中缓存）。
        /// </summary>
        public async UniTask DrawAsync(TextGraphicData data, bool showStrokeIndices = false)
        {
            transform.localScale = new Vector3(CustomScale, CustomScale, transform.localScale.z);
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

                StrokeSlot slot = GetStrokeSlot(strokeIndex);
                if (slot.SourcePoints == null || slot.SourcePoints.Length != points.Count)
                    slot.SourcePoints = new Vector2[points.Count];
                points.CopyTo(slot.SourcePoints);
                Vector2[] sourcePoints = slot.SourcePoints;
                _cachedPoints.Add(sourcePoints);
                if (points.Count < 3)
                {
                    // 保留原始笔画索引，避免后续笔画选中错位。
                    _strokeObjects.Add(null);
                    _strokeRenderers.Add(null);
                    continue;
                }

                if (!new Triangulator(sourcePoints).TryTriangulate(out int[] triangles))
                {
                    Log.Error($"[DrawCharacter] 字形『{data.character}』笔画 {strokeIndex} 无法完成三角剖分，请检查轮廓数据");
                    _strokeObjects.Add(null);
                    _strokeRenderers.Add(null);
                    continue;
                }

                // 1～3. 复用笔画物体、组件和逐笔画独立材质。
                PrepareStroke(slot, strokeIndex, template);
                _strokeObjects.Add(slot.Object);
                _strokeRenderers.Add(slot.Renderer);

                // 4. 复用 Mesh 与顶点工作区，三角剖分及绕序保持原样。
                Mesh mesh = slot.Mesh;
                var verts = slot.Vertices;
                verts.Clear();
                for (int p = 0; p < points.Count; p++)
                {
                    float x = points[p].x + PositionOffset.x;
                    float y = points[p].y + PositionOffset.y;
                    verts.Add(new Vector3(x, y, 0f));
                }
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    int second = triangles[j + 1];
                    triangles[j + 1] = triangles[j + 2];
                    triangles[j + 2] = second;
                }

                mesh.Clear();
                mesh.SetVertices(verts);
                mesh.triangles = triangles;
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                // 5. 复用2D碰撞器，碰撞轮廓简化算法保持原样。
                Vector2[] simplified = SimplifyColliderPoints(points);
                for (int k = 0; k < simplified.Length; k++)
                    simplified[k] += PositionOffset;
                slot.Collider.points = simplified;

                // 6. 标注笔画索引
                if (showStrokeIndices)
                {
                    AddStrokeIndexLabel(slot, strokeIndex, points);
                }
                if (slot.IndexLabel != null) slot.IndexLabel.gameObject.SetActive(showStrokeIndices);
                slot.Object.SetActive(true);
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
        private void AddStrokeIndexLabel(StrokeSlot slot, int index, List<Vector2> points)
        {
            // 计算笔画中心点作为标签位置（含偏移）
            Vector2 center = Vector2.zero;
            foreach (var p in points) center += p;
            center /= points.Count;
            center += PositionOffset;

            if (slot.IndexLabel == null)
            {
                GameObject labelObj = new GameObject($"Idx_{index}");
                labelObj.transform.SetParent(slot.Object.transform, false);
                TextMesh tm = slot.IndexLabel = labelObj.AddComponent<TextMesh>();
                tm.text = index.ToString();
                tm.fontSize = 40;
                tm.characterSize = 0.04f;
                tm.anchor = TextAnchor.MiddleCenter;
                tm.color = Color.red;
                tm.alignment = TextAlignment.Center;
                tm.fontStyle = FontStyle.Bold;
                MeshRenderer labelRenderer = labelObj.GetComponent<MeshRenderer>();
                if (labelRenderer != null) labelRenderer.sortingOrder = 10;
            }
            slot.IndexLabel.transform.localPosition = center;
        }

        #region Collider Optimization

        private const float MinVertexDistance = 0.02f;
        private const int MaxColliderVertices = 200;

        private static Vector2[] SimplifyColliderPoints(List<Vector2> points)
        {
            if (points.Count < 3)
                return System.Array.Empty<Vector2>();

            int count = points.Count;
            Vector2 first = points[0];
            Vector2 last = points[count - 1];
            if (Mathf.Approximately(first.x, last.x) && Mathf.Approximately(first.y, last.y))
            {
                count--;
            }

            List<Vector2> filtered = new List<Vector2>(count);
            filtered.Add(points[0]);
            float sqrMinDist = MinVertexDistance * MinVertexDistance;

            for (int i = 1; i < count; i++)
            {
                if ((points[i] - filtered[filtered.Count - 1]).sqrMagnitude >= sqrMinDist)
                {
                    filtered.Add(points[i]);
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
                Vector2[] sampled = new Vector2[MaxColliderVertices];
                float step = (float)(filtered.Count - 1) / (MaxColliderVertices - 1);
                for (int i = 0; i < MaxColliderVertices; i++)
                {
                    int idx = Mathf.RoundToInt(i * step);
                    if (idx >= filtered.Count)
                        idx = filtered.Count - 1;
                    sampled[i] = filtered[idx];
                }

                return sampled;
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
            MeshRenderer mr = _strokeRenderers[index];
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
            for (int i = 0; i < _strokeObjects.Count; i++)
            {
                var strokeObj = _strokeObjects[i];
                if (strokeObj == null) continue;
                MeshRenderer mr = _strokeRenderers[i];
                if (mr != null && mr.sharedMaterial != null)
                {
                    mr.sharedMaterial.color = _defaultStrokeColor;
                }
            }
        }
    }
}
