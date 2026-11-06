using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
                // o time se comprometeu antes de alguém pisar no bomb (rush que segurou no casarão/na curta): a hora é a do
                // compromisso — senão um "rush" virava entrada aos 77 s
                // (só no rush: nos outros o "comprometeu" do pro é tomar o mapa, e entrar nele adiantava o round ~10 s)
                if (comprometeu >= 0 && comprometeu < 25 && !TeamBrain.EXP.Contains("horaVelha")) h = MathF.Min(h, comprometeu + 8);
                // até ~80 s: o mapa daqui é mais curto que a Mirage e entrar depois disso é o tempo acabar (real 7,6%)
                if (h < float.MaxValue) return MathX.Clamp(h - 4, 3, 80);
                return plant > 0 ? MathX.Clamp(plant - 12, 3, 80) : MathF.Min(padrao, 72);
            }
            /// <summary>Estilo de jogo do plano (decisão 0014), calculado dos dados em <see cref="Classificar"/>.</summary>
            public Categoria categoria;
            /// <summary>Jogada a que o plano pertence (decisão 0022), com nome legível: "Rush A pela Rampa", "Setup 2-1-2 com avanço".</summary>
            public string jogada;
            public string Nome => $"{(tr ? "TR" : "CT")} {compra} #{idx} ({Tipo}, {categoria})";
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
            Classificar(l);
            NomearJogadas(l);
            todos = l;
        }

        // ---------------------------------------------------------------- jogadas e playbook (decisão 0022)
        /// <summary>Quanto uma jogada favorita do playbook pesa a mais na escolha do IGL. Provisório.</summary>
        public const float PREFERENCIA_PLAYBOOK = 4f;
        /// <summary>Jogadas com menos planos que isto não aparecem na tela do playbook (continuam no banco).</summary>
        public const int PLANOS_MIN_JOGADA = 5;

        // por onde o TR entra em cada bomb: a área de onde ele pisa no bomb vira o nome da rota (valem para a Vila)
        static readonly Dictionary<string, string> ROTA_B = new Dictionary<string, string>
        {
            ["Casarao"] = "Casarão", ["Casarao_corredor"] = "Casarão", ["Rua_B"] = "Casarão", ["Topo_entrada"] = "Casarão",
            ["Curta_sobe"] = "Curta", ["Meio"] = "Curta", ["Topo_meio"] = "Curta", ["Rato"] = "Curta", ["Esc_rato"] = "Curta",
            ["Mercado"] = "Mercado", ["Viela"] = "Mercado",
        };
        static readonly Dictionary<string, string> ROTA_A = new Dictionary<string, string>
        {
            ["Rampa_baixo"] = "Rampa", ["Hall_palacio"] = "Rampa", ["Palacio_sala"] = "Palácio", ["Sacada_A"] = "Palácio",
            ["Conector"] = "Conector", ["Conector_fim"] = "Conector", ["Meio"] = "Conector", ["Topo_meio"] = "Conector", ["Janelao"] = "Conector", ["Esc_plataforma"] = "Conector",
        };
        static string Pela(string rota) => (rota == "Rampa" || rota == "Curta" ? "pela " : "pelo ") + rota;

        static string NomeJogada(Plano p)
        {
            if (p.tr)
            {
                var rotas = p.site == 'A' ? ROTA_A : ROTA_B; string bomb = p.site.ToString();
                var vias = new Dictionary<string, int>();
                foreach (var j in p.js)
                {
                    string ant = null;
                    foreach (var ps in j.passos)
                    {
                        if (ps.area == bomb) { if (ant != null && rotas.TryGetValue(ant, out var rota)) vias[rota] = (vias.TryGetValue(rota, out var n) ? n : 0) + 1; break; }
                        ant = ps.area;
                    }
                }
                string tipo = p.comprometeu >= 0 && p.comprometeu < 25 ? "Rush" : p.comprometeu >= 0 && p.comprometeu < 50 ? "Execução" : "Default";
                if (vias.Count == 0) return tipo + " " + bomb + " (sem entrar)";
                var fortes = vias.Where(kv => kv.Value >= 2).Select(kv => kv.Key).OrderBy(x => x).ToList();
                if (fortes.Count >= 2) return "Split " + bomb + " (" + fortes[0] + " + " + fortes[1] + ")";
                return tipo + " " + bomb + " " + Pela(vias.OrderByDescending(kv => kv.Value).ThenBy(kv => kv.Key).First().Key);
            }
            else
            {
                int a = 0, meio = 0, b = 0; bool avanco = false;
                foreach (var j in p.js)
                {
                    float ta = 0, tm = 0, tb = 0;
                    foreach (var ps in j.passos)
                    {
                        float d = MathF.Min(ps.ate, 25) - MathF.Max(ps.de, 15);
                        if (d > 0) { if (ZONA_A.Contains(ps.area)) ta += d; else if (ZONA_B.Contains(ps.area)) tb += d; else tm += d; }
                        if (ps.de < 20 && LADO_TR.Contains(ps.area)) avanco = true;
                    }
                    if (ta > tm && ta > tb) a++; else if (tb > tm && tb >= ta) b++; else meio++;
                }
                return "Setup " + a + "-" + meio + "-" + b + (avanco ? " com avanço" : "");
            }
        }

        static void NomearJogadas(List<Plano> l) { foreach (var p in l) p.jogada = NomeJogada(p); }

        /// <summary>Uma jogada para a tela do playbook: quantos planos, quanto o time pro venceu, o estilo mais comum e a força medida.</summary>
        public class InfoJogada { public string nome; public bool tr; public int planos; public float vitoriaPro, forca; public Categoria estilo; }

        static List<InfoJogada> jogadasTR, jogadasCT;
        /// <summary>As jogadas de um lado com pelo menos PLANOS_MIN_JOGADA planos, da mais comum para a menos.</summary>
        public static List<InfoJogada> Jogadas(bool tr)
        {
            ref var cache = ref (tr ? ref jogadasTR : ref jogadasCT);
            if (cache != null) return cache;
            cache = Todos.Where(p => p.tr == tr).GroupBy(p => p.jogada).Where(g => g.Count() >= PLANOS_MIN_JOGADA)
                .Select(g => new InfoJogada
                {
                    nome = g.Key, tr = tr, planos = g.Count(), vitoriaPro = g.Count(p => p.ganhou) / (float)g.Count(),
                    forca = g.Average(p => Forca(p.idx)), estilo = g.GroupBy(p => p.categoria).OrderByDescending(x => x.Count()).First().Key,
                }).OrderByDescending(x => x.planos).ToList();
            return cache;
        }

        // ---------------------------------------------------------------- estilos de jogo
        /// <summary>
        /// Os 5 estilos que o manager escolhe (decisão 0014; mesma ordem do Estilo do manager). Cada plano do banco cai em um
        /// deles; o IGL prefere os do estilo do time (<see cref="PREFERENCIA_ESTILO"/>). Equilibrada = sem preferência.
        /// </summary>
        public enum Categoria { Equilibrada = 0, Agressiva = 1, Defensiva = 2, ExecucoesRapidas = 3, ControleDeMapa = 4 }

        /// <summary>Quanto um plano do estilo escolhido pesa a mais na escolha do IGL. Provisório: medir com a simulação.</summary>
        public const float PREFERENCIA_ESTILO = 6f;
        /// <summary>Nota mínima (em desvios-padrão) para o plano ganhar um estilo; abaixo disso fica Equilibrada.</summary>
        const float NOTA_MINIMA = 0.6f;

        // Áreas medidas nos próprios dados (presença de cada lado nos primeiros 25 s): do lado do TR (o CT só pisa nelas se
        // avançar), disputadas (os dois lados chegam cedo), do meio e onde o CT segura os bombs. Valem para a Vila.
        static readonly HashSet<string> LADO_TR = new HashSet<string> { "Casarao_corredor", "Rato", "Palacio_sala", "Rampa_baixo", "Topo_meio", "Hall_palacio", "Casarao", "Rua_B", "Topo_entrada" };
        static readonly HashSet<string> DISPUTADAS = new HashSet<string> { "Meio", "Rato", "Casarao_corredor", "Palacio_sala", "Curta_sobe" };
        static readonly HashSet<string> MEIO = new HashSet<string> { "Meio", "Janelao", "Rato", "Topo_meio", "Esc_rato" };
        static readonly HashSet<string> SEGURAR_CT = new HashSet<string> { "A", "B", "CT", "Conector_fim", "Viela", "Mercado", "Esc_plataforma", "Sacada_A", "Conector" };
        static readonly HashSet<string> ZONA_A = new HashSet<string> { "A", "Esc_plataforma", "Rampa_baixo", "Hall_palacio", "Palacio_sala", "Sacada_A", "Conector", "Conector_fim" };
        static readonly HashSet<string> ZONA_B = new HashSet<string> { "B", "Curta_sobe", "Mercado", "Viela", "Rua_B", "Casarao", "Casarao_corredor" };

        /// <summary>
        /// Põe cada plano num estilo, pelo que o time fez no round pro:
        ///   TR — Agressiva: muitas mortes nos primeiros 25 s e presença cedo nas áreas disputadas;
        ///        Defensiva: entra tarde, quase sem contato cedo e sem brigar pelo meio (espera o erro);
        ///        Execuções rápidas: entra no bomb cedo, sem trocar tiro antes;
        ///        Controle de mapa: entra tarde depois de passar muito tempo no meio e nas disputadas.
        ///   CT — Agressiva: avança para o lado do TR e troca tiro cedo; Defensiva: fica nos bombs, sem avançar;
        ///        Execuções rápidas (no CT, rotação rápida): jogadores que passam de um bomb para o outro antes dos 50 s;
        ///        Controle de mapa: muito tempo no meio nos primeiros 30 s.
        /// Cada traço vira nota padronizada (desvios da média do lado); ganha o estilo de maior nota, se passar de NOTA_MINIMA.
        /// </summary>
        static void Classificar(List<Plano> l)
        {
            foreach (bool tr in new[] { true, false })
            {
                var q = l.FindAll(p => p.tr == tr); if (q.Count == 0) continue;
                float[][] notas;
                if (tr)
                {
                    var h = Z(q, p => p.HoraDeEntrar(72)); var m = Z(q, p => Mortes(p, 25)); var c = Z(q, p => Tempo(p, DISPUTADAS, 0, 20));
                    var ctl = Z(q, p => { float hh = p.HoraDeEntrar(72); return Tempo(p, DISPUTADAS, 10, MathF.Max(10, hh)) + Tempo(p, MEIO, 10, MathF.Max(10, hh)); });
                    notas = new float[q.Count][];
                    for (int i = 0; i < q.Count; i++) notas[i] = new[] { 0, m[i] + c[i], h[i] - m[i] - 0.5f * ctl[i], -h[i] - 0.5f * m[i], ctl[i] + 0.5f * h[i] };
                }
                else
                {
                    var f = Z(q, p => Tempo(p, LADO_TR, 0, 30)); var m = Z(q, p => Mortes(p, 25)); var s = Z(q, p => Tempo(p, SEGURAR_CT, 0, 40));
                    var r = Z(q, Rotacoes); var mi = Z(q, p => Tempo(p, MEIO, 0, 30));
                    notas = new float[q.Count][];
                    for (int i = 0; i < q.Count; i++) notas[i] = new[] { 0, f[i] + m[i], s[i] - f[i] - m[i], r[i], mi[i] - 0.5f * f[i] };
                }
                for (int i = 0; i < q.Count; i++)
                {
                    int b = 1; for (int k = 2; k < 5; k++) if (notas[i][k] > notas[i][b]) b = k;
                    q[i].categoria = notas[i][b] >= NOTA_MINIMA ? (Categoria)b : Categoria.Equilibrada;
                }
            }
        }

        static float[] Z(List<Plano> q, Func<Plano, float> v)
        {
            var x = new float[q.Count]; double soma = 0, soma2 = 0;
            for (int i = 0; i < q.Count; i++) { x[i] = v(q[i]); soma += x[i]; soma2 += (double)x[i] * x[i]; }
            double media = soma / q.Count, dp = Math.Sqrt(Math.Max(0, soma2 / q.Count - media * media)); if (dp < 1e-6) dp = 1;
            for (int i = 0; i < q.Count; i++) x[i] = (float)((x[i] - media) / dp);
            return x;
        }
        static float Tempo(Plano p, HashSet<string> areas, float t0, float t1)
        {
            float s = 0;
            foreach (var j in p.js) foreach (var ps in j.passos) if (areas.Contains(ps.area)) s += MathF.Max(0, MathF.Min(ps.ate, t1) - MathF.Max(ps.de, t0));
            return s;
        }
        static float Mortes(Plano p, float t) { int n = 0; foreach (var j in p.js) if (j.morreu > 0 && j.morreu <= t) n++; return n; }
        static float Rotacoes(Plano p)
        {
            int n = 0;
            foreach (var j in p.js)
            {
                bool a = false, b = false;
                foreach (var ps in j.passos) if (ps.de < 50) { a |= ZONA_A.Contains(ps.area); b |= ZONA_B.Contains(ps.area); }
                if (a && b) n++;
            }
            return n;
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
                            if (!s.has(x, y) || !Chao(q) || AreaAt(q.x, q.y) != area) continue;
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
        static readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> zonas = new System.Collections.Concurrent.ConcurrentDictionary<string, string>();
        public static string ZonaDaArea(string area) => zonas.GetOrAdd(area, a => Places.Zona(Centro(a)));
        /// <summary>Chão de verdade pra ficar: andável, fora de cima de caixa e fora da escada de mão (lá o boneco engancha).</summary>
        static bool Chao(V2 q) { if (!NavGrid.Movable(q)) return false; var (cx, cy) = NavGrid.CellOf(q); return !NavGrid.onTop[cx, cy] && !NavGrid.ladder[cx, cy]; }

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
                            if (s.has(x, y) && Chao(q) && AreaAt(q.x, q.y) == area) l.Add(q);
                        }
                }
                celulas[area] = l; return l;
            }
        }

        /// <summary>Se já tem colega segurando perto (45) do ponto, escolhe outro ponto da mesma área que vê o mesmo lado.</summary>
        public static Places.Hold Espalhado(string area, Places.Hold h, List<V2> ocupados, Rng rng)
        {
            bool cheio(V2 q) { foreach (var o in ocupados) if ((o - q).Len < 70) return true; return false; }
            float folga(V2 q) { float d = 999; foreach (var o in ocupados) d = MathF.Min(d, (o - q).Len); return d; }
            // ponto atrás de colega, mirando o mesmo lado através dele, não vê nada (e o outro fica com a mira nas costas)
            bool atras(V2 q, V2 alvo)
            {
                if (TeamBrain.EXP.Contains("espalhaAtras")) return false;
                V2 d = alvo - q; float L = d.Len; if (L < 1) return false; d = d * (1 / L);
                foreach (var o in ocupados) { V2 w = o - q; float ao = w.x * d.x + w.y * d.y; if (ao > 20 && ao < L && MathF.Abs(w.x * d.y - w.y * d.x) < 18) return true; }
                return false;
            }
            if (!cheio(h.pos)) return h;
            V2 olha = h.look.Length > 0 ? h.look[0] : h.pos;
            V2 best = h.pos; float bs = float.MinValue;
            // região pequena (o pro tinha lugares diferentes que aqui viram um só): vale também o entorno da mesma zona
            var cand = new List<V2>(Celulas(area)); string zona = Places.Zona(h.pos);
            for (int k = 0; k < 24; k++)
                foreach (float r in new[] { 70f, 110f, 150f })
                {
                    V2 q = NavGrid.Snap(h.pos + V2.Ang(k * 0.2618f) * r);
                    if (Chao(q) && Places.Zona(q) == zona && NavGrid.Direct(h.pos, q)) cand.Add(q);   // reto daqui (barato; caminho custa caro)
                }
            foreach (var q in cand)
            {
                if (cheio(q)) continue;
                float sc = -0.02f * (q - h.pos).Len + (MapData.LosWalk(q, NavGrid.ZAt(q), olha, NavGrid.ZAt(olha), 18f) ? 3 : 0) + rng.Uniform(0, 0.5f) - (atras(q, olha) ? 3 : 0);
                if (sc > bs) { bs = sc; best = q; }
            }
            if (bs == float.MinValue && !TeamBrain.EXP.Contains("espalhaVelho"))
            {   // lugar apertado (nenhum ponto a 70 de todo mundo): o mais afastado que der, de preferência vendo o mesmo lado —
                // desistir deixava 3 no mesmo pixel
                float f0 = folga(h.pos);
                foreach (var q in cand)
                {
                    float f = folga(q); if (f < f0 + 15) continue;
                    float sc = 0.08f * MathF.Min(f, 70) - 0.01f * (q - h.pos).Len + (MapData.LosWalk(q, NavGrid.ZAt(q), olha, NavGrid.ZAt(olha), 18f) ? 1.5f : 0) + rng.Uniform(0, 0.3f) - (atras(q, olha) ? 1.5f : 0);
                    if (sc > bs) { bs = sc; best = q; }
                }
            }
            return bs == float.MinValue ? h : new Places.Hold(h.name + " (do lado)", best, h.look);
        }
        public static string Callout(string area) => CALLOUT.TryGetValue(area, out var c) ? c : area;

        /// <summary>
        /// Posição de segurar numa área: o ponto dela e pra onde olhar — o caminho que leva até quem vem
        /// (TR: pro bomb daquela zona ou pra base CT; CT: pra base TR), uns 140 à frente.
        /// </summary>
        static readonly Dictionary<string, Places.Hold> holdsG = new Dictionary<string, Places.Hold>();
        // feito na 1ª vez com NavGrid.Path: quem cria paga o A* no orçamento do passo e o caminho depende do cache — dividido
        // entre rounds paralelos, o resultado dependia de quem chegou antes. No modo NavGrid.PorRound é da thread e zera por round.
        [System.ThreadStatic] static Dictionary<string, Places.Hold> holdsT;
        static Dictionary<string, Places.Hold> holds => NavGrid.PorRound ? (holdsT ??= new Dictionary<string, Places.Hold>()) : holdsG;
        public static void NovoRound() { if (NavGrid.PorRound) holdsT?.Clear(); }
        public static void ZeraDinamico() { lock (trava) holdsG.Clear(); holdsT?.Clear(); }
        public static Places.Hold Hold(string area, bool tr, char site)
        {
            string k = area + (tr ? "|T|" : "|C|") + site;
            lock (trava) if (holds.TryGetValue(k, out var h0)) return h0;
            var h = CriaHold(area, tr, site);
            lock (trava) holds[k] = h;
            return h;
        }
        static Places.Hold CriaHold(string area, bool tr, char site)
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
