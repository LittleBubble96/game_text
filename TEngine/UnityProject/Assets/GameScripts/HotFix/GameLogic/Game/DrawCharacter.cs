using System.Collections.Generic;
using GameLogic.Data;
using UnityEngine;

namespace GameLogic.View
{

    public class DrawCharacter : MonoBehaviour
    {
        private List<GameObject> _strokeObjects = new List<GameObject>();
        private Color _defaultStrokeColor = Color.white;

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
            _strokeObjects.Clear();
            int childCount = transform.childCount;
            for (int i = childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(transform.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// 画一个汉字（传入你解析好的 TextGraphicData）
        /// </summary>
        public void Draw(TextGraphicData data, bool showStrokeIndices = false)
        {
            Clear();
            _strokeObjects = new List<GameObject>();

            for (int i = 0; i < data.strokes.Count; i++)
            {
                int strokeIndex = i;
                List<Vector2> points = data.strokes[i].points;

                if (points.Count < 3) continue;

                // 1. 创建笔画物体
                GameObject strokeObj = new GameObject($"Stroke_{strokeIndex}");
                strokeObj.transform.SetParent(transform);
                _strokeObjects.Add(strokeObj);

                // 2. 组件
                MeshFilter mf = strokeObj.AddComponent<MeshFilter>();
                MeshRenderer mr = strokeObj.AddComponent<MeshRenderer>();

                // 3. 默认材质（使用 sharedMaterial 避免编辑模式下泄漏）
                Material mat = new Material(Shader.Find("Unlit/Color"));
                mat.color = _defaultStrokeColor;
                mr.sharedMaterial = mat;

                // 4. 生成实心Mesh（应用位置偏移）
                Mesh mesh = new Mesh();
                List<Vector3> verts = new List<Vector3>();
                foreach (var p in points) verts.Add(new Vector3(p.x + PositionOffset.x, p.y + PositionOffset.y, 0));
                Triangulator tr = new Triangulator(points.ToArray());
                int[] triangles = tr.Triangulate();
                List<int> reversedTriangles = new List<int>();
                for (int j = 0; j < triangles.Length; j += 3)
                {
                    reversedTriangles.Add(triangles[j]);
                    reversedTriangles.Add(triangles[j + 2]);
                    reversedTriangles.Add(triangles[j + 1]);
                }

                mesh.vertices = verts.ToArray();
                mesh.triangles = reversedTriangles.ToArray();
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();

                mf.mesh = mesh;

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