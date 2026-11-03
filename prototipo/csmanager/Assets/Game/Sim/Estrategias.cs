using System;
using System.Collections.Generic;
using System.Globalization;
using CSM.Map;
using static CSM.Map.MapData;

namespace CSM.Sim
{
    /// <summary>
    /// Banco de estratégias: rounds pro de Mirage traduzidos pra Vila pela função de cada lugar (Tools/estrategias/converter.py
    /// gera EstrategiasDados.cs). Um plano diz, pra cada um dos 5, por onde ele passa e quando (região da Vila), o bomb e a
    /// hora em que o time se comprometeu. O IGL escolhe um pela compra, pela força medida e pela leitura do adversário, e o
    /// time segue as rotas até a hora de entrar — o resto (troca, leitura, pivô, retake) é o cérebro de sempre.
    /// </summary>
    public static partial class Estrategias
    {
        public struct Passo { public string area; public float de, ate; }
        public class Jogador { public Passo[] passos; public float morreu = -1; }
        public class Plano
        {
            public int idx; public bool tr; public string compra, id; public bool ganhou; public char site;
            public float plant, comprometeu, duracao; public Jogador[] js;
            /// <summary>Onde o jogador k está (área) no instante t pela rota real; null antes do 1º passo ou depois que morreu.</summary>
            public string Onde(int k, float t)
            {
                var j = js[k]; string ult = null;
                foreach (var p in j.passos) { if (t < p.de) break; ult = p.area; }
                return ult;
            }
            /// <summary>Zona (A/Mid/B) onde o jogador passa a maior parte do tempo entre t0 e t1.</summary>
            public string ZonaEntre(int k, float t0, float t1)
            {
                var soma = new Dictionary<string, float>();
                foreach (var p in js[k].passos)
                {
                    float d = MathF.Min(p.ate, t1) - MathF.Max(p.de, t0); if (d <= 0) continue;
                    string z = ZonaDaArea(p.area); soma.TryGetValue(z, out float s); soma[z] = s + d;
                }
                string best = "Spawn"; float bv = 0;
                foreach (var kv in soma) if (kv.Value > bv) { bv = kv.Value; best = kv.Key; }
                return best;
            }
            /// <summary>Tempo que o jogador k passa na zona z entre t0 e t1.</summary>
            public float TempoEm(int k, string z, float t0, float t1)
            {
                float s = 0;
                foreach (var p in js[k].passos) { float d = MathF.Min(p.ate, t1) - MathF.Max(p.de, t0); if (d > 0 && ZonaDaArea(p.area) == z) s += d; }
                return s;
            }
            /// <summary>Primeiro instante em que o jogador k pisa na área (ou zona, se z for A/B/Mid); -1 se nunca.</summary>
            public float PrimeiroEm(int k, string areaOuZona)
            {
                foreach (var p in js[k].passos) if (p.area == areaOuZona || ZonaDaArea(p.area) == areaOuZona) return p.de;
                return -1;
            }
            /// <summary>Hora de entrar no bomb: o 1º da equipe que pisa nele, um pouco antes; sem ninguém, antes do plant.</summary>
            public float HoraDeEntrar(float padrao)
            {
                float h = float.MaxValue; string a = site.ToString();
                for (int k = 0; k < 5; k++) foreach (var p in js[k].passos) if (p.area == a) { h = MathF.Min(h, p.de); break; }
                if (h < float.MaxValue) return MathF.Max(3, h - 4);
                return plant > 0 ? MathF.Max(3, plant - 12) : padrao;
            }
            public string Nome => $"{(tr ? "TR" : "CT")} {compra} #{idx} ({Tipo})";
            public string Tipo => !tr ? "setup" : comprometeu >= 0 && comprometeu < 25 ? "rush" : comprometeu >= 0 && comprometeu < 50 ? "execução" : "default";
        }

        static List<Plano> todos;
        public static List<Plano> Todos { get { if (todos == null) Carregar(); return todos; } }

        static void Carregar()
        {
            var l = new List<Plano>(DADOS.Length);
            var ci = CultureInfo.InvariantCulture;
            foreach (var linha in DADOS)
            {
                var c = linha.Split(';');
                var p = new Plano
                {
                    idx = l.Count, tr = c[0] == "T", compra = c[1], id = c[2], ganhou = c[3] == "1", site = c[4][0],
                    plant = float.Parse(c[5], ci), comprometeu = float.Parse(c[6], ci), duracao = float.Parse(c[7], ci), js = new Jogador[5],
                };
                for (int k = 0; k < 5 && 8 + k < c.Length; k++)
                {
                    var partes = c[8 + k].Split('|');
                    var j = new Jogador { morreu = partes.Length > 1 ? float.Parse(partes[1], ci) : -1 };
                    var ps = new List<Passo>();
                    if (partes[0].Length > 0)
                        foreach (var s in partes[0].Split(','))
                        {
                            int dp = s.IndexOf(':'), tr = s.IndexOf('-', dp);
                            ps.Add(new Passo { area = s.Substring(0, dp), de = float.Parse(s.Substring(dp + 1, tr - dp - 1), ci), ate = float.Parse(s.Substring(tr + 1), ci) });
                        }
                    j.passos = ps.ToArray(); p.js[k] = j;
                }
                for (int k = 0; k < 5; k++) if (p.js[k] == null) p.js[k] = new Jogador { passos = new Passo[0] };
                l.Add(p);
            }
            todos = l;
        }

        // ---------------------------------------------------------------- áreas → posições
        static readonly Dictionary<string, V2> centro = new Dictionary<string, V2>();
        static readonly object trava = new object();
        /// <summary>Ponto andável da área mais perto do meio dela (onde o jogador fica quando a rota diz "nessa região").</summary>
        public static V2 Centro(string area)
        {
            lock (trava)
            {
                if (centro.TryGetValue(area, out var v)) return v;
                double sx = 0, sy = 0; int n = 0; var cel = new List<V2>();
                foreach (var s in areas)
                {
                    if (s.name != area) continue;
                    for (int x = s.box.x0; x < s.box.x1; x += NavGrid.CELL)
                        for (int y = s.box.y0; y < s.box.y1; y += NavGrid.CELL)
                        {
                            var q = new V2(x + 1, y + 1);
                            if (!s.has(x, y) || !NavGrid.Movable(q) || AreaAt(q.x, q.y) != area) continue;
                            sx += q.x; sy += q.y; n++; cel.Add(q);
                        }
                }
                V2 r = V2.Zero;
                if (n > 0)
                {
                    var m = new V2((float)(sx / n), (float)(sy / n)); float bd = float.MaxValue;
                    foreach (var q in cel) { float d = (q - m).Len; if (d < bd) { bd = d; r = q; } }
                }
                centro[area] = r;
                return r;
            }
        }
        public static string ZonaDaArea(string area) => Places.Zona(Centro(area));

        static readonly Dictionary<string, List<V2>> celulas = new Dictionary<string, List<V2>>();
        /// <summary>Pontos andáveis da área (um a cada 2 células).</summary>
        static List<V2> Celulas(string area)
        {
            lock (trava)
            {
                if (celulas.TryGetValue(area, out var l)) return l;
                l = new List<V2>();
                foreach (var s in areas)
                {
                    if (s.name != area) continue;
                    for (int x = s.box.x0; x < s.box.x1; x += 2 * NavGrid.CELL)
                        for (int y = s.box.y0; y < s.box.y1; y += 2 * NavGrid.CELL)
                        {
                            var q = new V2(x + 1, y + 1);
                            if (s.has(x, y) && NavGrid.Movable(q) && AreaAt(q.x, q.y) == area) l.Add(q);
                        }
                }
                celulas[area] = l; return l;
            }
        }

        /// <summary>Se já tem colega segurando perto (45) do ponto, escolhe outro ponto da mesma área que vê o mesmo lado.</summary>
        public static Places.Hold Espalhado(string area, Places.Hold h, List<V2> ocupados, Rng rng)
        {
            bool cheio(V2 q) { foreach (var o in ocupados) if ((o - q).Len < 45) return true; return false; }
            if (!cheio(h.pos)) return h;
            V2 olha = h.look.Length > 0 ? h.look[0] : h.pos;
            V2 best = h.pos; float bs = float.MinValue;
            foreach (var q in Celulas(area))
            {
                if (cheio(q)) continue;
                float sc = -0.02f * (q - h.pos).Len + (MapData.LosWalk(q, NavGrid.ZAt(q), olha, NavGrid.ZAt(olha), 18f) ? 3 : 0) + rng.Uniform(0, 0.5f);
                if (sc > bs) { bs = sc; best = q; }
            }
            return bs == float.MinValue ? h : new Places.Hold(h.name + " (do lado)", best, h.look);
        }
        public static string Callout(string area) => CALLOUT.TryGetValue(area, out var c) ? c : area;

        /// <summary>
        /// Posição de segurar numa área: o ponto dela e pra onde olhar — o caminho que leva até quem vem
        /// (TR: pro bomb daquela zona ou pra base CT; CT: pra base TR), uns 140 à frente.
        /// </summary>
        public static Places.Hold Hold(string area, bool tr, char site)
        {
            V2 p = Centro(area);
            V2 alvo = tr ? (ZonaDaArea(area) == "B" ? Places.PlantB : ZonaDaArea(area) == "A" ? Places.PlantA : Places.Plant(site)) : SPAWN_T[0];
            V2 olha = alvo;
            var cam = NavGrid.Path(p, alvo);
            if (cam != null && cam.Count > 1)
            {
                float d = 0; olha = cam[cam.Count - 1].p;
                for (int k = 1; k < cam.Count; k++) { d += (cam[k].p - cam[k - 1].p).Len; if (d >= 140) { olha = cam[k].p; break; } }
            }
            return new Places.Hold(Callout(area), p, olha);
        }
    }
}
