using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// bx (só mede, -auditar): o que o Gabriel viu no exe 10.
    /// (3) "demoram pra entender que é no outro bomb": do 1º sinal forte que o CT TEM (intel do time: contato visto, sons
    ///     juntos, granada ouvida) no bomb que vai ser plantado até cada CT de fora começar a ir pra lá (ordem ou corpo);
    ///     quantos só vão depois do plant.
    /// (4) "bomba plantada, eles no eco, e você vai lá e rusha": mortes de TR no pós-plant — no lugar que segurava, a caminho
    ///     dele, ou depois de largar a posição pra frente; por vantagem e pela economia do CT.
    /// Não decide nada.
    /// </summary>
    public class MedeLeitura
    {
        readonly Match m;
        public MedeLeitura(Match m) { this.m = m; }

        // ---- (3) linha do tempo do que o CT sabe, por zona
        readonly Dictionary<string, List<(float t, int id)>> vistos = new Dictionary<string, List<(float, int)>>();
        readonly Dictionary<string, List<(float t, string kind)>> ouvidos = new Dictionary<string, List<(float, string)>>();
        readonly Dictionary<int, float> contatoT = new Dictionary<int, float>();
        int heardLidos;
        // por CT: quando a ordem / o corpo mudaram de zona
        readonly Dictionary<int, List<(float t, string zOrdem, string zPos)>> ctLinha = new Dictionary<int, List<(float, string, string)>>();
        bool fechou3, abriu; readonly Dictionary<int, (string z, string ordem, float t)> morteCT = new Dictionary<int, (string, string, float)>(); readonly Dictionary<string, float> compromisso = new Dictionary<string, float>();   // 1ª vez com 3+ TR vivos na zona (verdade, régua pro)
        // ---- (4) por TR no pós-plant: onde se firmou
        readonly Dictionary<int, (V2 p, float desde)> parado = new Dictionary<int, (V2, float)>();
        readonly Dictionary<int, V2> ancora = new Dictionary<int, V2>();
        readonly Dictionary<int, string> naBaseCT = new Dictionary<int, string>();
        /* pós-plant: TR que foi pra base CT depois do plant (o Gabriel vê "toda partida": TR indo brigar no CT) */
        readonly Dictionary<int, string> naBaseCTpp = new Dictionary<int, string>();
        readonly HashSet<int> vivoNoPlant = new HashSet<int>();
        readonly HashSet<int> baseLonge = new HashSet<int>();   /* na base CT a 250+ do C4 (o que o Gabriel vê: TR indo brigar no CT) */
        /// <summary>Base CT da Vila = regiões "CT" e "Mercado" (no pro: CTSpawn e Shop).</summary>
        static bool BaseCT(string rg) => rg == "CT" || rg == "Mercado";

        static string Z(V2 p) => Places.Zona(p);
        static void Add<T>(Dictionary<string, List<T>> d, string k, T v) { if (!d.TryGetValue(k, out var l)) d[k] = l = new List<T>(); l.Add(v); }

        public void Passo()
        {
            float t = m.t;
            var it = m.intel[(int)Team.CT];
            foreach (var c in it.contacts.Values)
                if (c.seen && (!contatoT.TryGetValue(c.id, out float ct) || c.t > ct + 0.01f)) { contatoT[c.id] = c.t; Add(vistos, Z(c.pos), (c.t, c.id)); }
            for (; heardLidos < it.heard.Count; heardLidos++) { var h = it.heard[heardLidos]; Add(ouvidos, Z(h.p), (h.t, h.kind)); }
            if (!m.bombPlanted)
                foreach (var zz in new[] { "A", "B" })
                    if (!compromisso.ContainsKey(zz) && m.agents.Count(a => a.IsT && a.alive && Z(a.pos) == zz) >= 3) compromisso[zz] = t;
            if (!m.bombPlanted)
                foreach (var a in m.agents)
                {
                    if (a.IsT || !a.alive) continue;
                    var o = a.brain.order; V2 alvo = o.hold != null ? o.hold.pos : o.dest;
                    string zo = alvo.x == 0 && alvo.y == 0 ? "?" : Z(alvo), zp = Z(a.pos);
                    if (!ctLinha.TryGetValue(a.id, out var l)) ctLinha[a.id] = l = new List<(float, string, string)>();
                    if (l.Count == 0 || l[^1].zOrdem != zo || l[^1].zPos != zp) l.Add((t, zo, zp));
                }
            if (!m.bombPlanted)
                foreach (var a in m.agents)
                {   /* rota TR pela base CT antes do plant (pros Mirage: 5,7% dos jogador-round pisam lá antes do plant) */
                    if (!a.IsT || !a.alive || naBaseCT.ContainsKey(a.id) || !BaseCT(RotaHumana.Regiao(a.pos))) continue;
                    naBaseCT[a.id] = Corta(a.brain.order.name) + (a.hasBomb ? " · com C4" : "");
                }
            if (m.bombPlanted && !fechou3) { fechou3 = true; Fecha3(); foreach (var a in m.agents) if (a.IsT && a.alive) vivoNoPlant.Add(a.id); }
            if (m.bombPlanted)
                foreach (var a in m.agents)
                {
                    if (!a.IsT || !a.alive) continue;
                    string rgp = RotaHumana.Regiao(a.pos);
                    if (!BaseCT(rgp)) continue;
                    if ((a.pos - m.bombPos).Len > 250) baseLonge.Add(a.id);
                    if (naBaseCTpp.ContainsKey(a.id)) continue;
                    naBaseCTpp[a.id] = Corta(a.brain.order.name) + " · " + rgp + " · bomb " + m.planSite;
                }
            if (!abriu && t >= 15)
            {   /* abertura real do TR aos 15 s (pros Mirage full/force: 0 no topo/meio 30% · 1 32% · 2 26% · 3+ 12%) */
                abriu = true; int top = 0; var zs = new Dictionary<string, int> { ["A"] = 0, ["Mid"] = 0, ["B"] = 0 };
                foreach (var a in m.agents) { if (!a.IsT || !a.alive) continue; string ar = MapData.AreaAt(a.pos.x, a.pos.y); if (ar == "Topo_meio" || ar == "Topo_entrada" || ar == "Meio") top++; string z = Z(a.pos); if (zs.ContainsKey(z)) zs[z]++; }
                m.Aud("abertura TR aos 15 s: no topo/meio (pros: 0 30 · 1 32 · 2 26 · 3+ 12)", top >= 3 ? "3+" : top.ToString());
                m.Aud("abertura TR aos 15 s: zonas", $"A{zs["A"]} Mid{zs["Mid"]} B{zs["B"]}");
            }
            if (m.bombPlanted)
                foreach (var a in m.agents)
                {
                    if (!a.IsT || !a.alive) continue;
                    if (a.Speed < a.MaxSpeed * 0.3f)
                    {
                        if (!parado.TryGetValue(a.id, out var pd) || (pd.p - a.pos).Len > 40) parado[a.id] = (a.pos, t);
                        else if (t - pd.desde >= 1.0f) ancora[a.id] = pd.p;
                    }
                    else parado.Remove(a.id);
                }
        }

        /// <summary>No plant: S = bomb plantado. Sinal forte = 2+ TR vistos lá em 4 s, ou 3+ sons lá em 3 s, ou granada ouvida
        /// lá com outro sinal em 4 s.</summary>
        void Fecha3()
        {
            string S = m.planSite.ToString(); float tp = m.plantedAt;
            float tContato = -1, tForte = -1; string porque = "nenhum";
            var vs = vistos.TryGetValue(S, out var v1) ? v1.OrderBy(x => x.t).ToList() : new List<(float t, int id)>();
            var os = ouvidos.TryGetValue(S, out var o1) ? o1.OrderBy(x => x.t).ToList() : new List<(float t, string kind)>();
            if (vs.Count > 0) tContato = vs[0].t;
            void Cand(float tt, string pq) { if (tt >= 0 && tt <= tp && (tForte < 0 || tt < tForte)) { tForte = tt; porque = pq; } }
            for (int i = 0; i < vs.Count; i++) { var ids = new HashSet<int>(); for (int j = i; j >= 0 && vs[i].t - vs[j].t <= 4; j--) ids.Add(vs[j].id); if (ids.Count >= 2) { Cand(vs[i].t, "2+ vistos"); break; } }
            for (int i = 2; i < os.Count; i++) if (os[i].t - os[i - 2].t <= 3) { Cand(os[i].t, "3+ sons (" + os[i].kind + ")"); break; }
            foreach (var g in os.Where(x => x.kind == "granada"))
                if (vs.Any(x => Math.Abs(x.t - g.t) <= 4) || os.Count(x => x.kind != "granada" && Math.Abs(x.t - g.t) <= 4) >= 1) { Cand(g.t, "granada + outro"); break; }
            if (tContato > tp) tContato = -1;
            var ig = m.igl[(int)Team.CT];
            float tc0 = compromisso.TryGetValue(S, out float cc) ? cc : tp;
            m.Aud("leitura CT: giro do time no plant (vs 3+ TR na zona)", ig == null ? "?" : ig.GiroPara == m.planSite ? (ig.GiroEm < tc0 ? "girou pro bomb certo antes" : "girou pro bomb certo " + B(ig.GiroEm - tc0, "depois")) : ig.GiroPara == ' ' ? "não girou" : "girou pro OUTRO bomb");
            m.Aud("leitura CT: 1º sinal forte no bomb do plant (s antes do plant)", tForte < 0 ? "z nenhum antes do plant" : B(tp - tForte, "antes") + " · " + porque.Split(' ')[0]);
            m.Aud("leitura CT: 1º contato visto no bomb do plant (s antes do plant)", tContato < 0 ? "z nenhum antes do plant" : B(tp - tContato, "antes"));
            Regua2(S, compromisso.TryGetValue(S, out float cr2) ? cr2 : tp, tp, "com plant");
            /* régua pro (playbook, 1458 plants): CT fora da zona do bomb quando 3+ TR entram nela — foi (pelo corpo) quando */
            float tc = compromisso.TryGetValue(S, out float c0) ? c0 : tp;
            foreach (var kv in ctLinha)
            {
                var a = m.agents.FirstOrDefault(x => x.id == kv.Key); if (a == null) continue;
                string zp0 = null; foreach (var e in kv.Value) { if (e.t > tc + 1e-3f) break; zp0 = e.zPos; }
                if (zp0 == null || morteCT.TryGetValue(a.id, out var md0) && md0.t <= tc) continue;   // já morto quando o TR entrou: o pro não conta
                const string K = "leitura CT (régua pro): fora da zona quando 3+ TR entram — foi quando (pros: já 36 · foi 32 · não foi 25 · morreu 6)";
                if (zp0 == S) { m.Aud(K, "já estava"); continue; }
                float tv = -1; foreach (var e in kv.Value) if (e.t > tc && e.zPos == S) { tv = e.t; break; }
                m.Aud(K, tv >= 0 ? (tv - tc < 3 ? "foi <3" : tv - tc < 8 ? "foi 3-8" : tv - tc < 15 ? "foi 8-15" : "foi 15+") : !a.alive ? "morreu sem ir" : "não foi (" + Z(a.pos) + ")");
                if (tv < 0)
                {   /* onde trava: quem não vai (papel, casa) e onde morre quem morre antes */
                    string papel = a.role == Role.Ancora ? "âncora" : a.role == Role.Awper ? "AWP" : a.role == Role.Rotacao ? "rotação" : "outro";
                    if (!a.alive && morteCT.TryGetValue(a.id, out var mc))
                        m.Aud("leitura CT: morreu sem ir — onde · ordem (depois de 3+ TR na zona?)", $"{mc.z} · {mc.ordem} · {(mc.t >= tc ? "depois" : "antes")}");
                    else if (a.alive) m.Aud("leitura CT: não foi — papel · casa · zona", $"{papel} · casa {a.brain.homeSite} · {Z(a.pos)}");
                }
            }
            /* cada CT vivo no plant que estava fora do bomb do plant no sinal */
            float t0 = tForte >= 0 ? tForte : tp;
            foreach (var kv in ctLinha)
            {
                var a = m.agents.FirstOrDefault(x => x.id == kv.Key); if (a == null) continue;
                var l = kv.Value; string zoSig = null, zpSig = null;
                foreach (var e in l) { if (e.t > t0 + 1e-3f) break; zoSig = e.zOrdem; zpSig = e.zPos; }
                if (zpSig == null || morteCT.TryGetValue(a.id, out var md1) && md1.t <= t0) continue;
                if (zpSig == S) { m.Aud("leitura CT: quem estava fora do bomb no sinal — foi quando", "(já estava no bomb)"); continue; }
                if (zoSig == S) { m.Aud("leitura CT: quem estava fora do bomb no sinal — foi quando", "a já ia (ordem pro bomb antes do sinal)"); continue; }
                float tv = -1; foreach (var e in l) if (e.t > t0 && (e.zOrdem == S || e.zPos == S)) { tv = e.t; break; }
                string r = tv >= 0 ? (tForte < 0 ? "f foi sem sinal forte" : B(tv - t0, "depois")) : !a.alive ? "y morreu antes do plant sem ir" : "z não foi até o plant";
                m.Aud("leitura CT: quem estava fora do bomb no sinal — foi quando", r);
                if (tv < 0 && a.alive) m.Aud("leitura CT: no plant, fora do bomb sem ir (onde · ordem)", $"{Z(a.pos)} · {Corta(a.brain.order.name)}");
            }
        }
        static string B(float d, string s) => d < 3 ? $"b <3 s {s}" : d < 8 ? $"c 3-8 s {s}" : d < 15 ? $"d 8-15 s {s}" : $"e 15+ s {s}";
        static string Corta(string n) { if (n == null) return "?"; int sp = n.IndexOf(':'); if (sp > 0) n = n.Substring(0, sp); int pa = n.IndexOf('('); if (pa > 0) n = n.Substring(0, pa); return n.Trim().Length > 32 ? n.Trim().Substring(0, 32) : n.Trim(); }

        /// <summary>(4): TR morto pro CT depois do plant.</summary>
        public void Morte(Agent v, Agent por)
        {
            if (!m.bombPlanted && !v.IsT) morteCT[v.id] = (Z(v.pos), Corta(v.brain.order.name), m.t);
            if (!m.bombPlanted && v.IsT && Z(v.pos) == "Mid") m.Aud("abertura TR: morto no meio antes do plant (ordem)", Corta(v.brain.order.name));
            if (!m.bombPlanted && v.IsT)
            {   /* régua pro (Mirage, 1740 mortes TR antes do plant): BombsiteA 18 · Apartments 15 · PalaceInterior 11 · Middle 9 · BombsiteB 9 · TopofMid 8 · CTSpawn 1,5 */
                string rg = RotaHumana.Regiao(v.pos) ?? "?";
                m.Aud("TR morto antes do plant — região (pros: A 18 · Apart 15 · Palácio 11 · Middle 9 · B 9 · TopMid 8 · CTSpawn 1,5)", rg);
                m.Aud("TR morto antes do plant: na base CT/mercado? (pros 1,4%)", BaseCT(rg) ? "sim" : "não");
                if (BaseCT(rg)) m.Aud("TR morto na base CT antes do plant — ordem · C4", Corta(v.brain.order.name) + (v.hasBomb ? " · com C4" : ""));
                if (v.hasBomb)
                {
                    var eq = m.igl[(int)Team.T];
                    m.Aud("C4: portador morto antes do plant — região", rg);
                    m.Aud("C4: portador morto antes do plant — call · estilo · ordem", (eq.callTipo == "" ? "-" : eq.callTipo) + " · " + (eq.estilo == "" ? "-" : eq.estilo) + " · " + Corta(v.brain.order.name));
                }
            }
            if (!m.bombPlanted || por == null || por.team == v.team || !v.IsT) return;
            int nos = m.Alive(Team.T) + (v.alive ? 0 : 1), eles = m.Alive(Team.CT);
            int dv = nos - eles; string van = dv >= 2 ? "a +2" : dv == 1 ? "b +1" : dv == 0 ? "c igual" : "d atrás";
            var cts = m.agents.Where(a => a.team == Team.CT && a.alive).ToList();
            int rif = cts.Count(a => a.Primaria != null);
            string eco = cts.Count == 0 ? "?" : rif == 0 ? "CT eco/pistola" : rif < cts.Count ? "CT misto" : "CT armado";
            float dBomb = NavGrid.PathLen(v.pos, m.bombPos);
            string onde;
            if (!ancora.TryGetValue(v.id, out V2 an)) onde = "indo pro pós-plant (não firmou)";
            else
            {
                float dAn = NavGrid.PathLen(an, m.bombPos);
                onde = (v.pos - an).Len < 120 ? "no lugar que segurava" : dBomb > dAn + 100 ? "SAIU pra frente (longe do C4)" : "mudou de lugar (perto do C4)";
            }
            m.Aud("pós-plant: TR morto pro CT — onde (vantagem antes)", $"{onde} · {van}");
            if (BaseCT(RotaHumana.Regiao(v.pos)))
            {
                float dk = (por.pos - v.pos).Len;
                int juntos = m.agents.Count(x => x.team == por.team && x.alive && x != por && (x.pos - por.pos).Len < 400);
                m.Aud("pós-plant: TR morto na base CT — distância · CT em dupla?", (dk < 600 ? "perto (<600)" : dk < 1200 ? "médio" : "longe (1200+)") + " · " + (juntos > 0 ? "dupla" : "sozinho") + (por.brain.order.name != null && por.brain.order.name.StartsWith("punir") ? " · PUNIÇÃO" : ""));
            }
            m.Aud("pós-plant: TR morto pro CT — onde (economia do CT)", $"{onde} · {eco}");
            string S = m.planSite.ToString(), ar = MapData.AreaAt(v.pos.x, v.pos.y);
            string ondeZ = ar == S ? "no bomb" : Z(v.pos) == S ? "zona do bomb" : "FORA da zona do bomb";
            m.Aud("pós-plant (régua pro): TR morto pro CT — onde · vantagem (pros: FORA 11% = +2 5,6 · +1 2,0 · igual 2,0 · atrás 1,7)", $"{ondeZ} · {van}");
            m.Aud("pós-plant (régua pro): TR morto pro CT — onde · economia do CT", $"{ondeZ} · {eco}");
            if (onde.StartsWith("SAIU")) m.Aud("pós-plant: TR que saiu pra frente — ordem · fase", $"{Corta(v.brain.order.name)} · {Corta(v.brain.fase)}");
        }

        /* todos os rounds em que 3+ TR entraram numa zona de bomb (com e sem plant — rodar pode estar evitando o plant) e quem
           foi pro ponto do retake fora da zona ("indo": ordem pro bomb) */
        void Regua2(string S, float tc, float tFim, string tag)
        {
            foreach (var kv in ctLinha)
            {
                var a = m.agents.FirstOrDefault(x => x.id == kv.Key); if (a == null) continue;
                string zp0 = null; foreach (var e in kv.Value) { if (e.t > tc + 1e-3f) break; zp0 = e.zPos; }
                if (zp0 == null || morteCT.TryGetValue(a.id, out var md) && md.t <= tc) continue;
                string r;
                if (zp0 == S) r = "a já estava";
                else
                {
                    float tv = -1; string zoFim = null; bool morto = morteCT.TryGetValue(a.id, out var mf) && mf.t <= tFim;
                    foreach (var e in kv.Value) { if (e.t > tFim) break; if (e.t > tc && e.zPos == S && tv < 0) tv = e.t; zoFim = e.zOrdem; }
                    r = tv >= 0 ? "b foi (entrou na zona)" : morto ? "e morreu sem ir" : zoFim == S ? "c indo (ordem pro bomb, fora da zona)" : "d não foi";
                }
                m.Aud("leitura CT (todos os rounds): fora da zona quando 3+ TR entram · " + tag, r);
            }
        }
        public void Fim(bool trVenceu)
        {
            if (!trVenceu && !m.bombPlanted && m.Alive(Team.T) > 0)
            {   /* tempo acabou sem plant com TR vivo: o que cada TR vivo fazia (quem segura o meio? o C4 onde?) */
                var eq = m.igl[(int)Team.T]; var port = m.agents.FirstOrDefault(x => x.IsT && x.alive && x.hasBomb);
                string c4 = port != null ? "C4 com " + (port.id == eq.MeioDono ? "o dono do meio" : "TR vivo") : m.bombDropped ? (m.bombGuardado ? "C4 guardado no chão" : "C4 caído no chão") : "C4 ?";
                m.Aud("tempo acabou (TR vivo): onde o C4", c4 + (m.bombDropped ? " · " + (RotaHumana.Regiao(m.droppedBomb) ?? "?") : ""));
                foreach (var a in m.agents)
                {
                    if (!a.IsT || !a.alive) continue;
                    string q = (a.id == eq.MeioDono ? "dono do meio · " : "") + (a.hasBomb ? "com C4 · " : "") + (a.guardando ? "guardando · " : "") + Corta(a.brain.order.name) + " · " + (RotaHumana.Regiao(a.pos) ?? "?");
                    m.Aud("tempo acabou (TR vivo): cada um — papel · ordem · onde", q);
                    if (m.bombDropped && !a.hasBomb) m.Aud("tempo acabou (TR vivo): distância até o C4 no chão", NavGrid.PathLen(a.pos, m.droppedBomb) < 400 ? "perto (<400)" : NavGrid.PathLen(a.pos, m.droppedBomb) < 1200 ? "médio" : "longe");
                }
            }
            foreach (var a in m.agents)
                if (a.IsT) m.Aud("rota TR: pisou na base CT antes do plant (pros CTSpawn+Shop: 6,2% dos jogador-round)", naBaseCT.ContainsKey(a.id) ? "sim" : "não");
            foreach (var kv in naBaseCT) m.Aud("rota TR: na base CT — ordem ao entrar", kv.Value);
            if (m.bombPlanted)
            {
                foreach (var id in vivoNoPlant) m.Aud("pós-plant: TR pisou na base CT depois do plant (dos vivos no plant)", naBaseCTpp.ContainsKey(id) ? "sim" : "não");
                foreach (var id in vivoNoPlant) m.Aud("pós-plant: TR na base CT a 250+ do C4 (dos vivos no plant)", baseLonge.Contains(id) ? "sim" : "não");
                foreach (var kv in naBaseCTpp) m.Aud("pós-plant: TR na base CT — ordem · região · bomb", kv.Value);
            }
            if (!m.bombPlanted)
            {
                string Sx = null; float tcx = 999; foreach (var kv in compromisso) if (kv.Value < tcx) { tcx = kv.Value; Sx = kv.Key; }
                m.Aud("leitura CT: rounds — 3+ TR entraram numa zona? plantou?", Sx == null ? "sem compromisso" : "comprometeu e NÃO plantou · " + (trVenceu ? "TR venceu" : "CT venceu"));
                if (Sx != null) Regua2(Sx, tcx, m.t, "sem plant");
            }
            else m.Aud("leitura CT: rounds — 3+ TR entraram numa zona? plantou?", "plantou · " + (trVenceu ? "TR venceu" : "CT venceu"));
            var ig = m.igl[(int)Team.CT];
            if (ig != null) foreach (var r in ig.rodadas)
                m.Aud("roda pela crença: foi pro bomb do plant? · por quê", (!m.bombPlanted ? "sem plant" : r.X == m.planSite ? "certo" : "ERRADO") + " · " + (r.pq == "" ? "estimativa" : r.pq.Split(' ')[0]));
            if (!m.bombPlanted) return;
            m.Aud("pós-plant: round com plant — TR venceu?", trVenceu ? "venceu" : "perdeu");
        }
    }
}
