using System;
using UnityEngine;

/// <summary>简单多边形耳切剖分。输出逆时针三角形，索引始终指向原始点位。</summary>
public class Triangulator
{
    private readonly Vector2[] _points;

    public Triangulator(Vector2[] points)
    {
        // 保留输入快照，重复调用不会修改点位或逐次删除闭合点。
        _points = points == null ? Array.Empty<Vector2>() : (Vector2[])points.Clone();
    }

    public int[] Triangulate()
    {
        TryTriangulate(out var triangles);
        return triangles;
    }

    /// <summary>无法完成剖分时返回 false 和空数组，不返回部分网格。</summary>
    public bool TryTriangulate(out int[] triangles)
    {
        triangles = Array.Empty<int>();
        if (_points.Length < 3) return false;
        int[] vertices = new int[_points.Length];
        int count = 0;
        for (int i = 0; i < _points.Length; i++)
        {
            var point = _points[i];
            if (float.IsNaN(point.x) || float.IsNaN(point.y) ||
                float.IsInfinity(point.x) || float.IsInfinity(point.y)) return false;
            if (count == 0 || !SamePoint(_points[vertices[count - 1]], point)) vertices[count++] = i;
        }
        if (count > 1 && SamePoint(_points[vertices[0]], _points[vertices[count - 1]])) count--;
        if (count < 3) return false;
        double area = 0;
        for (int i = 0, previous = count - 1; i < count; previous = i++)
        {
            var a = _points[vertices[previous]];
            var b = _points[vertices[i]];
            area += (double)a.x * b.y - (double)b.x * a.y;
        }
        if (area == 0) return false;
        if (area < 0) Array.Reverse(vertices, 0, count);

        // 双向索引环：剪去耳朵只更新两个链接，不搬移剩余索引。
        int[] previousIndices = new int[count];
        int[] nextIndices = new int[count];
        for (int i = 0; i < count; i++)
        {
            previousIndices[i] = (i + count - 1) % count;
            nextIndices[i] = (i + 1) % count;
        }
        int[] result = new int[(count - 2) * 3];
        int written = 0, remaining = count, cursor = 0, rejected = 0;
        while (remaining > 3)
        {
            int before = previousIndices[cursor], after = nextIndices[cursor];
            if (IsEar(before, cursor, after, vertices, nextIndices))
            {
                result[written++] = vertices[before];
                result[written++] = vertices[cursor];
                result[written++] = vertices[after];
                nextIndices[before] = after;
                previousIndices[after] = before;
                remaining--;
                rejected = 0;
            }
            else if (++rejected >= remaining)
            {
                return false;
            }
            cursor = after;
        }
        int second = nextIndices[cursor], third = nextIndices[second];
        if (Cross(_points[vertices[cursor]], _points[vertices[second]], _points[vertices[third]]) <= 0)
            return false;
        result[written++] = vertices[cursor];
        result[written++] = vertices[second];
        result[written] = vertices[third];
        triangles = result;
        return true;
    }

    private bool IsEar(int before, int current, int after, int[] vertices, int[] next)
    {
        Vector2 a = _points[vertices[before]], b = _points[vertices[current]], c = _points[vertices[after]];
        if (Cross(a, b, c) <= 0) return false;
        float minX = Math.Min(a.x, Math.Min(b.x, c.x)), maxX = Math.Max(a.x, Math.Max(b.x, c.x));
        float minY = Math.Min(a.y, Math.Min(b.y, c.y)), maxY = Math.Max(a.y, Math.Max(b.y, c.y));
        for (int i = next[after]; i != before; i = next[i])
        {
            Vector2 p = _points[vertices[i]];
            if (p.x < minX || p.x > maxX || p.y < minY || p.y > maxY) continue;
            if (Cross(a, b, p) >= 0 && Cross(b, c, p) >= 0 && Cross(c, a, p) >= 0) return false;
        }
        return true;
    }

    private static double Cross(Vector2 a, Vector2 b, Vector2 c) =>
        ((double)b.x - a.x) * ((double)c.y - a.y) - ((double)b.y - a.y) * ((double)c.x - a.x);

    private static bool SamePoint(Vector2 a, Vector2 b) =>
        Mathf.Approximately(a.x, b.x) && Mathf.Approximately(a.y, b.y);
}
