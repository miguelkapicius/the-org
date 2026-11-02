using System;
using System.Collections.Generic;

namespace CSM.Sim
{
    /// <summary>Vetor 2D do mundo (x = leste, y = sul), em unidades do mapa (1 unidade ~ 1 voxel).</summary>
    public struct V2
    {
        public float x, y;
        public V2(float x, float y) { this.x = x; this.y = y; }
        public static V2 operator +(V2 a, V2 b) => new V2(a.x + b.x, a.y + b.y);
        public static V2 operator -(V2 a, V2 b) => new V2(a.x - b.x, a.y - b.y);
        public static V2 operator -(V2 a) => new V2(-a.x, -a.y);
        public static V2 operator *(V2 a, float k) => new V2(a.x * k, a.y * k);
        public static V2 operator *(float k, V2 a) => new V2(a.x * k, a.y * k);
        public static V2 operator /(V2 a, float k) => new V2(a.x / k, a.y / k);
        public float Len => MathF.Sqrt(x * x + y * y);
        public float Dot(V2 b) => x * b.x + y * b.y;
        public static float Dist(V2 a, V2 b) => (a - b).Len;
        public static V2 Ang(float a) => new V2(MathF.Cos(a), MathF.Sin(a));
        public float Atan2 => MathF.Atan2(y, x);
        public static readonly V2 Zero = new V2(0, 0);
        public override string ToString() => $"({x:F1}, {y:F1})";
    }

    /// <summary>Gerador aleatório com as distribuições que a simulação usa (uniforme, normal, escolha).</summary>
    public class Rng
    {
        readonly Random r;
        public Rng(int seed) { r = new Random(seed); }
        public float Rand() => (float)r.NextDouble();
        public float Uniform(float a, float b) => a + (b - a) * (float)r.NextDouble();
        public V2 Uniform2(float a, float b) => new V2(Uniform(a, b), Uniform(a, b));
        public int Int(int n) => r.Next(n);
        public float Normal(float mu, float sd)
        {
            double u1 = 1.0 - r.NextDouble(), u2 = r.NextDouble();
            return mu + sd * (float)(Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Cos(2.0 * Math.PI * u2));
        }
        public V2 Normal2(float sd) => new V2(Normal(0, sd), Normal(0, sd));
        public T Choice<T>(IList<T> xs) => xs[r.Next(xs.Count)];
        public int Weighted(float[] w)
        {
            float s = 0; foreach (float x in w) s += x;
            float u = Rand() * s;
            for (int i = 0; i < w.Length; i++) { u -= w[i]; if (u <= 0) return i; }
            return w.Length - 1;
        }
        public void Shuffle<T>(IList<T> xs)
        {
            for (int i = xs.Count - 1; i > 0; i--) { int j = r.Next(i + 1); (xs[i], xs[j]) = (xs[j], xs[i]); }
        }
    }

    /// <summary>Fila de prioridade mínima (heap binário) pro Dijkstra.</summary>
    public class MinHeap
    {
        readonly List<(int v, float k)> h = new List<(int, float)>();
        public int Count => h.Count;
        public void Push(int v, float k)
        {
            h.Add((v, k));
            int i = h.Count - 1;
            while (i > 0) { int p = (i - 1) / 2; if (h[p].k <= h[i].k) break; (h[p], h[i]) = (h[i], h[p]); i = p; }
        }
        public (int v, float k) Pop()
        {
            var top = h[0];
            h[0] = h[h.Count - 1]; h.RemoveAt(h.Count - 1);
            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = l + 1, m = i;
                if (l < h.Count && h[l].k < h[m].k) m = l;
                if (r < h.Count && h[r].k < h[m].k) m = r;
                if (m == i) break;
                (h[m], h[i]) = (h[i], h[m]); i = m;
            }
            return top;
        }
    }

    public static class MathX
    {
        public const float PI = MathF.PI;
        public static float AngDiff(float a, float b)
        {
            float d = (a - b + PI) % (2 * PI);
            if (d < 0) d += 2 * PI;
            return d - PI;
        }
        public static int Clamp(int v, int a, int b) => v < a ? a : (v > b ? b : v);
        public static float Clamp(float v, float a, float b) => v < a ? a : (v > b ? b : v);
        public static float Smooth(float x) { x = Clamp(x, 0, 1); return x * x * (3 - 2 * x); }
    }
}
