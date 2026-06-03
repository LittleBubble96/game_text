using UnityEngine;
using System.Collections.Generic;

public class Triangulator
{
    private List<Vector2> m_points;

    public Triangulator(Vector2[] points)
    {
        m_points = new List<Vector2>(points);
    }

    public int[] Triangulate()
    {
        // 如果首尾顶点相同（闭合多边形），移除最后一个重复顶点
        int count = m_points.Count;
        if (count >= 3)
        {
            Vector2 first = m_points[0];
            Vector2 last = m_points[count - 1];
            if (Mathf.Approximately(first.x, last.x) && Mathf.Approximately(first.y, last.y))
            {
                m_points.RemoveAt(count - 1);
                count--;
            }
        }

        if (count < 3)
            return new int[0];

        List<int> indices = new List<int>();

        // 顶点索引数组
        int[] V = new int[count];
        for (int i = 0; i < count; i++)
            V[i] = i;

        int n = count;
        int nv = n;

        // 移除退化顶点数量
        int countDeletions = 0;

        // Ear Clipping 算法
        for (int v = nv - 1; n > 3; )
        {
            // 如果遍历一圈后仍有3个顶点，直接添加最后的三角形
            if (countDeletions >= n)
            {
                indices.Add(V[2]);
                indices.Add(V[1]);
                indices.Add(V[0]);
                break;
            }

            int u = v;
            if (n <= u) u = 0;
            v = u + 1;
            if (n <= v) v = 0;
            int w = v + 1;
            if (n <= w) w = 0;

            if (Snip(u, v, w, n, V))
            {
                indices.Add(V[u]);
                indices.Add(V[v]);
                indices.Add(V[w]);

                // 从V中移除V[v]
                for (int s = v, t = v + 1; t < n; s++, t++)
                    V[s] = V[t];
                n--;
                countDeletions = 0;
            }
            else
            {
                countDeletions++;
            }
        }

        // 添加最后一个三角形
        if (n == 3)
        {
            indices.Add(V[0]);
            indices.Add(V[1]);
            indices.Add(V[2]);
        }

        return indices.ToArray();
    }

    private bool Snip(int u, int v, int w, int n, int[] V)
    {
        Vector2 A = m_points[V[u]];
        Vector2 B = m_points[V[v]];
        Vector2 C = m_points[V[w]];
        if (Mathf.Epsilon > (B.x - A.x) * (C.y - A.y) - (B.y - A.y) * (C.x - A.x))
            return false;

        for (int p = 0; p < n; p++)
        {
            if (p == u || p == v || p == w) continue;
            Vector2 P = m_points[V[p]];
            if (PointInTriangle(A, B, C, P)) return false;
        }
        return true;
    }

    private bool PointInTriangle(Vector2 A, Vector2 B, Vector2 C, Vector2 P)
    {
        float ax = C.x - B.x, ay = C.y - B.y;
        float bx = A.x - C.x, by = A.y - C.y;
        float cx = B.x - A.x, cy = B.y - A.y;
        float apx = P.x - A.x, apy = P.y - A.y;
        float bpx = P.x - B.x, bpy = P.y - B.y;
        float cpx = P.x - C.x, cpy = P.y - C.y;

        float aCross = ax * bpy - ay * bpx;
        float bCross = bx * cpy - by * cpx;
        float cCross = cx * apy - cy * apx;

        return aCross >= 0 && bCross >= 0 && cCross >= 0;
    }
}