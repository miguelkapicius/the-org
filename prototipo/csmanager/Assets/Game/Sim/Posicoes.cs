using System;
using System.Collections.Generic;
using System.IO;

namespace CSM.Sim
{
    /// <summary>
    /// Quanto vale segurar cada posição: duelos ganhos ali comparados com a média do próprio lado (medido em rounds e salvo
    /// em posicoes.txt). 0,5 = posição comum; acima disso vale ficar e esperar; abaixo, ficar parado ali só perde duelo.
    /// </summary>
    public static class Posicoes
    {
        public const string ARQ = "Assets/Game/Sim/posicoes.txt";
        static Dictionary<string, float> forca; static readonly object trava = new object();

        public static float Forca(bool tr, string nome)
        {
            if (forca == null) lock (trava) { if (forca == null) forca = Carrega(); }
            return forca.TryGetValue((tr ? "TR " : "CT ") + nome, out float f) ? f : 0.5f;
        }

        static Dictionary<string, float> Carrega()
        {
            var r = new Dictionary<string, float>();
            if (!File.Exists(ARQ)) return r;
            var linhas = new List<(string n, float a, float b)>();
            var tot = new float[2]; var soma = new float[2];
            foreach (var l in File.ReadAllLines(ARQ))
            {
                var p = l.Split('|'); if (p.Length != 3) continue;
                float a = float.Parse(p[1]), b = float.Parse(p[2]);
                linhas.Add((p[0], a, b));
                int i = p[0].StartsWith("TR") ? 0 : 1; soma[i] += a; tot[i] += a + b;
            }
            foreach (var (n, a, b) in linhas)
            {
                int i = n.StartsWith("TR") ? 0 : 1;
                float media = tot[i] > 0 ? soma[i] / tot[i] : 0.5f;
                r[n] = (a + 20 * media) / (a + b + 20) - media + 0.5f;   // poucos duelos puxam pra média do lado
            }
            return r;
        }
    }
}
