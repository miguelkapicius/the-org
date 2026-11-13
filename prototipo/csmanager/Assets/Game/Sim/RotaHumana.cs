using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Modelo de rota humanizada (sim-apoio, 2026-11-08): pra onde o pro vai a partir de uma região e quanto tempo fica nela,
    /// tirado das demos (Mirage → Vila, Inferno → Brasa). Dados gerados por Tools/rotas_humanas/gera.py em
    /// RotaHumanaDadosVila.cs / RotaHumanaDadosBrasa.cs, compilados (sem arquivo pra ler no exe).
    ///
    /// Proximas: transição pela chave lado × faixa do round (0-15, 15-30, 30-50, 50+ s) × plantado, suavizada em degraus
    /// (chave ← lado+plantado ← função do lugar nos 7 mapas). Com <paramref name="anterior"/> (de onde veio) mistura a 2ª ordem
    /// observada (n2*p2 + K3*p1)/(n2 + K3): mantém o compromisso com a rota (sem ela o modelo vai e volta entre duas regiões).
    /// Validação (1.000 rotas TR, 4 regiões ou até o bomb): erro médio nas 10 mais comuns do pro 1,6 pontos na Vila, 0,6 na Brasa.
    ///
    /// Regiões = as áreas do pro traduzidas (23 por mapa). Área que não está na lista vai pra região de centro mais perto
    /// (mesma regra do Humano.RegiaoPro). Tudo aqui é só leitura depois de montar: pode chamar de várias threads.
    /// </summary>
    public static partial class RotaHumana
    {
        /// <summary>Quantis guardados da permanência (o sorteio interpola entre eles).</summary>
        /// <summary>Peso da 1ª ordem na mistura com a 2ª (igual ao K3 de Tools/rotas_humanas/gera.py).</summary>
        const float K3 = 8f;
        public static readonly float[] QS = { 0.05f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 0.95f, 0.99f };

        sealed class Tab
        {
            public readonly Dictionary<(byte, byte, bool, string), List<(string, float)>> t1 = new Dictionary<(byte, byte, bool, string), List<(string, float)>>();
            public readonly Dictionary<(byte, bool, string, string), (int n, Dictionary<string, float> p)> t2 = new Dictionary<(byte, bool, string, string), (int, Dictionary<string, float>)>();
            public readonly Dictionary<(byte, string), (float[] q, int n, string fonte)> perm = new Dictionary<(byte, string), (float[], int, string)>();
            public readonly Dictionary<string, string> funcao = new Dictionary<string, string>();
            public string[] regs;
        }
        static Tab tab; static readonly object trava = new object();
        static readonly ConcurrentDictionary<string, string> regDaArea = new ConcurrentDictionary<string, string>();

        static Tab T
        {
            get
            {
                if (tab != null) return tab;
                lock (trava)
                {
                    if (tab != null) return tab;
                    bool brasa = MapData.MAPA == "brasa"; var t = new Tab();
                    var reg = brasa ? REG_BRASA : REG_VILA;
                    t.regs = Array.ConvertAll(reg, r => r.regiao);
                    foreach (var r in reg) t.funcao[r.regiao] = r.funcao;
                    foreach (var x in brasa ? TRANS_BRASA : TRANS_VILA)
                    {
                        var k = (x.lado, x.faixa, x.plantado, x.de);
                        if (!t.t1.TryGetValue(k, out var l)) t.t1[k] = l = new List<(string, float)>();
                        l.Add((x.para, x.p));
                    }
                    foreach (var x in brasa ? TRANS2_BRASA : TRANS2_VILA)
                    {
                        var k = (x.lado, x.plantado, x.anterior, x.de);
                        if (!t.t2.TryGetValue(k, out var d)) t.t2[k] = d = (x.n, new Dictionary<string, float>());
                        d.p[x.para] = x.p;
                    }
                    foreach (var x in brasa ? PERM_BRASA : PERM_VILA) t.perm[(x.lado, x.regiao)] = (x.q, x.n, x.fonte);
                    return tab = t;
                }
            }
        }

        static byte L(Team lado) => (byte)(lado == Team.T ? 0 : 1);
        public static int Faixa(float t) => t < 15 ? 0 : t < 30 ? 1 : t < 50 ? 2 : 3;

        static Dictionary<string, HashSet<string>> viz;
        /// <summary>Regiões vizinhas (alguma transição de/para vista no pro, qualquer lado). Só leitura.</summary>
        public static IReadOnlyCollection<string> Vizinhos(string regiao)
        {
            if (viz == null)
                lock (trava)
                    if (viz == null)
                    {   var v = new Dictionary<string, HashSet<string>>();
                        foreach (var kv in T.t1) foreach (var (para, _) in kv.Value)
                        {   string de = kv.Key.Item4;
                            if (!v.TryGetValue(de, out var a)) v[de] = a = new HashSet<string>(); a.Add(para);
                            if (!v.TryGetValue(para, out var b)) v[para] = b = new HashSet<string>(); b.Add(de);
                        }
                        viz = v;
                    }
            return regiao != null && viz.TryGetValue(regiao, out var r) ? r : (IReadOnlyCollection<string>)Array.Empty<string>();
        }
        /// <summary>Distância em regiões (0 = mesma; 99 = mais de maxd).</summary>
        public static int Distancia(string a, string b, int maxd = 3)
        {
            if (a == null || b == null) return 99; if (a == b) return 0;
            var vist = new HashSet<string> { a }; var fr = new List<string> { a };
            for (int k = 1; k <= maxd; k++)
            {   var nx = new List<string>();
                foreach (var x in fr) foreach (var y in Vizinhos(x)) { if (y == b) return k; if (vist.Add(y)) nx.Add(y); }
                fr = nx;
            }
            return 99;
        }

        /// <summary>As regiões do pro neste mapa (nomes de área da Vila/Brasa).</summary>
        public static IReadOnlyList<string> Regioes => T.regs;
        /// <summary>Função do lugar (spawn_T, spawn_CT, bomb, A_tr, A_disputa, A_ct, B_…, Mid_…), ou null se não é região.</summary>
        public static string Funcao(string regiao) => regiao != null && T.funcao.TryGetValue(regiao, out var f) ? f : null;

        /// <summary>Região do pro onde fica a área (a própria, se é uma das regiões; senão a de centro mais perto).</summary>
        public static string RegiaoDaArea(string area)
        {
            if (area == null) return null;
            return regDaArea.GetOrAdd(area, a =>
            {
                if (T.funcao.ContainsKey(a)) return a;
                V2 c = Estrategias.Centro(a); string best = null; float bd = float.MaxValue;
                foreach (var r in T.regs) { float d = (Estrategias.Centro(r) - c).Len; if (d < bd) { bd = d; best = r; } }
                return best;
            });
        }
        /// <summary>Região do pro no ponto (null fora de qualquer área).</summary>
        public static string Regiao(V2 p) => RegiaoDaArea(MapData.AreaAt(p.x, p.y));

        /// <summary>
        /// Próximas regiões com peso (somam 1), da mais provável pra menos. <paramref name="regiaoAtual"/> pode ser área ou
        /// região; <paramref name="t"/> = segundos de round (contados como no pro: do fim do freeze); <paramref name="anterior"/>
        /// = região de onde veio (opcional, melhora muito a rota). Lista nova a cada chamada (pode mexer). Vazia se não há dado.
        /// </summary>
        public static List<(string regiao, float peso)> Proximas(Team lado, string regiaoAtual, float t, bool plantado, string anterior = null)
        {
            var r = new List<(string regiao, float peso)>();
            string de = RegiaoDaArea(regiaoAtual); if (de == null) return r;
            byte l = L(lado); int fx = Faixa(t);
            if (!T.t1.TryGetValue((l, (byte)fx, plantado, de), out var p1) && !T.t1.TryGetValue((l, (byte)fx, !plantado, de), out p1)) return r;
            string ant = RegiaoDaArea(anterior);
            if (ant != null && ant != de && (T.t2.TryGetValue((l, plantado, ant, de), out var c2) || T.t2.TryGetValue((l, !plantado, ant, de), out c2)))
            {
                float s = 0;
                foreach (var (v, p) in p1) { float x = (c2.n * (c2.p.TryGetValue(v, out var q2) ? q2 : 0f) + K3 * p) / (c2.n + K3); r.Add((v, x)); s += x; }
                for (int i = 0; i < r.Count; i++) r[i] = (r[i].regiao, r[i].peso / s);
                r.Sort((a, b) => b.peso.CompareTo(a.peso));
            }
            else foreach (var x in p1) r.Add(x);
            return r;
        }

        /// <summary>Sorteia a próxima região (u em [0,1)); null se não há dado.</summary>
        public static string SorteiaProxima(Team lado, string regiaoAtual, float t, bool plantado, string anterior, float u)
        {
            var ops = Proximas(lado, regiaoAtual, t, plantado, anterior);
            foreach (var (v, p) in ops) { u -= p; if (u <= 0) return v; }
            return ops.Count > 0 ? ops[ops.Count - 1].regiao : null;
        }

        /// <summary>Permanência do pro na região (s): p25, p50, p75. Zeros se não há dado.</summary>
        public static (float p25, float p50, float p75) Permanencia(Team lado, string regiao)
        {
            var q = Quantis(lado, regiao); return q == null ? (0f, 0f, 0f) : (q[2], q[3], q[4]);
        }

        /// <summary>Quantis da permanência (QS: p5, p10, p25, p50, p75, p90, p95, p99), ou null.</summary>
        public static float[] Quantis(Team lado, string regiao)
        {
            string rg = RegiaoDaArea(regiao); return rg != null && T.perm.TryGetValue((L(lado), rg), out var x) ? x.q : null;
        }

        /// <summary>
        /// Sorteia uma permanência (s) com a distribuição do pro, interpolando entre os quantis (u em [0,1)). A cauda longa
        /// (quem segura/lurka 50 s+) vem das passagens cortadas por morte ou fim de round, tratadas como censuradas.
        /// </summary>
        public static float SorteiaPermanencia(Team lado, string regiao, float u)
        {
            var q = Quantis(lado, regiao); if (q == null) return 0f;
            float xa = 0f, ya = q[0] * 0.5f;
            for (int i = 0; i <= QS.Length; i++)
            {
                float xb = i < QS.Length ? QS[i] : 1f, yb = i < QS.Length ? q[i] : q[q.Length - 1] * 1.15f;
                if (u <= xb) return ya + (yb - ya) * (u - xa) / MathF.Max(xb - xa, 1e-6f);
                xa = xb; ya = yb;
            }
            return ya;
        }
    }
}
