using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace GameLogic.Data
{
    [Serializable]
    public struct QuantizedPoint
    {
        public short x;
        public short y;
    }

    [Serializable]
    public class TextDrawPoints : ISerializationCallbackReceiver
    {
        public const int CoordinateScale = 4096;
        // 兼容旧 .asset 的 points；同时存储不适合量化的笔画。
        [SerializeField, FormerlySerializedAs("points")]
        private List<Vector2> floatPoints;
        [SerializeField] private List<QuantizedPoint> quantizedPoints;
        [NonSerialized] private List<Vector2> _decoded;

        // 绘制、碰撞和编辑器仍读 Vector2；仅第一次访问时解码。
        public List<Vector2> points
        {
            get
            {
                if (quantizedPoints == null || quantizedPoints.Count == 0) return floatPoints;
                if (_decoded != null) return _decoded;
                _decoded = new List<Vector2>(quantizedPoints.Count);
                foreach (var point in quantizedPoints)
                    _decoded.Add(new Vector2(point.x / (float)CoordinateScale, point.y / (float)CoordinateScale));
                return _decoded;
            }
        }

        public TextDrawPoints(List<Vector2> source)
        {
            floatPoints = source;
            Quantize();
        }

        public void OnBeforeSerialize() { }
        public void OnAfterDeserialize() => _decoded = null;

        /// <summary>显式迁移旧数据；不适合量化的笔画保留 float。返回是否完成了迁移。</summary>
        public bool Quantize()
        {
            if (quantizedPoints != null && quantizedPoints.Count > 0) return false;
            if (floatPoints == null || floatPoints.Count == 0) return false;
            var packed = new List<QuantizedPoint>(floatPoints.Count);
            var decoded = new List<Vector2>(floatPoints.Count);
            foreach (var point in floatPoints)
            {
                double x = Math.Round((double)point.x * CoordinateScale);
                double y = Math.Round((double)point.y * CoordinateScale);
                if (double.IsNaN(x) || double.IsNaN(y) || x < short.MinValue || x > short.MaxValue ||
                    y < short.MinValue || y > short.MaxValue) return false;
                packed.Add(new QuantizedPoint { x = (short)x, y = (short)y });
                decoded.Add(new Vector2((float)x / CoordinateScale, (float)y / CoordinateScale));
            }
            // 防止细小坐标变化导致耳切算法丢失三角形。
            int[] before = new Triangulator(floatPoints.ToArray()).Triangulate();
            int[] after = new Triangulator(decoded.ToArray()).Triangulate();
            double area = Area(floatPoints, before);
            if (before.Length != after.Length || Math.Abs(Area(decoded, after) - area) > Math.Max(0.0001, area * 0.001))
                return false;
            quantizedPoints = packed;
            floatPoints = null;
            _decoded = null;
            return true;
        }

        private static double Area(List<Vector2> points, int[] triangles)
        {
            double area = 0;
            for (int i = 0; i < triangles.Length; i += 3)
            {
                var a = points[triangles[i]];
                var b = points[triangles[i + 1]];
                var c = points[triangles[i + 2]];
                area += Math.Abs(((double)b.x - a.x) * (c.y - a.y) - ((double)c.x - a.x) * (b.y - a.y)) * 0.5;
            }
            return area;
        }
    }
}
