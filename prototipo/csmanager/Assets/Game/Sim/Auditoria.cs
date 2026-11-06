using System;
using System.Collections.Generic;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Auditor: confere se o que o bot diz bate com o que ele faz e conta o que um humano chamaria de hive mind ou conflito.
    /// Amostra a cada 0,25 s (Match.Auditar liga). Cada grupo vira % no relatório (Metricas.Auditoria).
    /// </summary>
    public class Auditoria
    {
        const float MESMO = 8f * MathF.PI / 180;   // duas miras no mesmo ponto
        readonly Match m; readonly float[] aimAnt = new float[10], pulo = new float[10]; readonly string[] fonteAnt = new string[10]; readonly float[] fogoDesde = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }, fogoHp = new float[10], fogoSaiu = { -9, -9, -9, -9, -9, -9, -9, -9, -9, -9 };

        public Auditoria(Match m) { this.m = m; }

        /* bx: travessia de smoke (medição; só lê o estado — Sample das células é puro, sem rng/PathLen/Pixel) */
        readonly float[] fumDesde = { -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }, fumSaiuT = { -9, -9, -9, -9, -9, -9, -9, -9, -9, -9 };
        readonly V2[] fumEntrou = new V2[10]; readonly bool[] fumInimiga = new bool[10], fumCruzou = new bool[10];
        readonly List<(Agent a, float t, bool inimiga)> fumPend = new List<(Agent, float, bool)>();
        int fumAbIdx;
        /// <summary>Em que smoke o agente está (olho na fumaça das células &gt; 0,25), ou null.</summary>
        CSM.View.SmokeVolume FumacaEm(Agent a)
        {
            var q = new CSM.View.V3(a.pos.x, a.pos.y, a.z + a.Eye);
            foreach (var s in m.smokes) { if (s.age < 1.2f) continue; if (s.Sample(q, m.t) > 0.25f) return s; }
            return null;
        }
        string FumFaixa(Agent v) => fumDesde[v.id] >= 0 ? "dentro" : m.t - fumSaiuT[v.id] <= 3f ? "saindo (até 3 s)" : null;
        void Fumaca()
        {
            foreach (var a in m.agents)
            {
                if (!a.alive) continue;
                var s = FumacaEm(a);
                if (s != null && fumDesde[a.id] < 0)
                {
                    fumDesde[a.id] = m.t; fumEntrou[a.id] = a.pos;
                    fumInimiga[a.id] = m.smokeDe.TryGetValue(s, out var dono) && dono != a.team;
                    if (fumInimiga[a.id] && a.brain != null)
                    {   /* bx: por onde se entra na smoke do inimigo (a decisão "smoke no caminho" só vale no andar de ordem) */
                        string on = a.brain.order?.name ?? "-"; int ix = on.IndexOfAny(new[] { ':', '(', '—' }); if (ix > 0) on = on.Substring(0, ix).Trim();
                        m.Aud("smoke: entrou na inimiga (fase)", (a.IsT ? "TR · " : "CT · ") + (a.brain.fase == "" ? "(sem fase)" : a.brain.fase));
                        m.Aud("smoke: entrou na inimiga (ordem)", (a.IsT ? "TR · " : "CT · ") + on + (a.brain.DecidiuSmoke(s) is bool dz ? (dz ? " · decidiu atravessar" : " · decidiu esperar") : " · sem decisão"));
                    }
                }
                else if (s == null && fumDesde[a.id] >= 0)
                {
                    bool cruzou = (a.pos - fumEntrou[a.id]).Len > 30;
                    string lado = a.IsT ? "TR" : "CT", de = fumInimiga[a.id] ? "inimiga" : "do meu time";
                    m.Aud("smoke: saiu da fumaça", lado + " · " + de + " · " + (cruzou ? "atravessou" : "entrou e voltou"));
                    if (cruzou) { m.Ev("smoke: atravessou " + (fumInimiga[a.id] ? "inimiga" : "do time") + "/round"); fumPend.Add((a, m.t, fumInimiga[a.id])); }
                    fumDesde[a.id] = -1; fumSaiuT[a.id] = m.t; fumCruzou[a.id] = cruzou;
                }
            }
            /* abates novos: quem morre dentro/saindo da smoke e quem matou */
            for (; fumAbIdx < m.abates.Count; fumAbIdx++)
            {
                var ab = m.abates[fumAbIdx];
                if (ab.by == null || ab.aliado || ab.suicidio) continue;
                string fv = FumFaixa(ab.v), fk = FumFaixa(ab.by);
                if (fv != null)
                {
                    var k = ab.by.brain; bool parado = ab.by.Speed < ab.by.MaxSpeed * 0.2f;
                    string quem = fk != null ? "matador também na smoke" : k.Segurando && parado ? "matador segurando parado" : parado ? "matador parado (sem posição)" : "matador andando";
                    m.Aud("smoke: morreu na smoke", (ab.v.IsT ? "TR " : "CT ") + fv + " · " + quem);
                    m.Ev("smoke: morreu dentro/saindo/round");
                }
                if (fk != null && fv == null) { m.Aud("smoke: quem saiu da smoke matou", (ab.by.IsT ? "TR " : "CT ") + fk); m.Ev("smoke: matou saindo/round"); }
                if (ab.atravesSmoke) m.Ev("smoke: abate através da smoke/round");
            }
            /* resultado de cada travessia: os 4 s depois de sair */
            for (int i = fumPend.Count - 1; i >= 0; i--)
            {
                var (a, t0, inim) = fumPend[i];
                if (m.t - t0 < 4f && a.alive && !m.over) continue;
                bool matou = false, morreu = !a.alive && a.diedAt <= t0 + 4f;
                foreach (var ab in m.abates) if (ab.by == a && !ab.aliado && ab.t >= t0 - 0.5f && ab.t <= t0 + 4f) matou = true;
                m.Aud("smoke: 4 s depois de atravessar", (a.IsT ? "TR" : "CT") + " · " + (inim ? "inimiga" : "do time") + " · " + (matou && morreu ? "trocou (matou e morreu)" : matou ? "matou" : morreu ? "morreu" : "nada"));
                fumPend.RemoveAt(i);
            }
            foreach (var a in m.agents)
                if (!a.alive && fumDesde[a.id] >= 0) { m.Aud("smoke: 4 s depois de atravessar", (a.IsT ? "TR" : "CT") + " · " + (fumInimiga[a.id] ? "inimiga" : "do time") + " · morreu DENTRO"); fumDesde[a.id] = -1; }
        }

        /// <summary>bx: inimigo no campo de visão com parte do corpo à vista (cabeça em cima, ombro, perna) e o raio único
        /// da Sim (olho → olho do alvo) tapado. Pares com smoke na caixa ficam de fora (a auditoria não toca na smoke).</summary>
        void VisaoCorpoAud()
        {
            foreach (var a in m.agents)
            {
                if (!a.alive || a.Blind(m.t)) continue;
                foreach (var e in m.agents)
                {
                    if (!e.alive || e.team == a.team) continue;
                    V2 d = e.pos - a.pos; float dl = d.Len; if (dl > 1400 || dl < 30) continue;
                    float da = MathF.Abs(MathX.AngDiff(d.Atan2, a.aim));
                    bool luneta = a.scoped && a.gunId == "awp";
                    if (da > 55f * MathF.PI / 180 || luneta && da > 20f * MathF.PI / 180) continue;
                    bool fum = false;
                    foreach (var s in m.smokes) { var (mn, mx) = s.Bounds; if (MathF.Max(a.pos.x, e.pos.x) >= mn.x && MathF.Min(a.pos.x, e.pos.x) <= mx.x && MathF.Max(a.pos.y, e.pos.y) >= mn.y && MathF.Min(a.pos.y, e.pos.y) <= mx.y) fum = true; }
                    if (fum) continue;
                    if (MapData.LosWalk(a.pos, a.z + a.Eye - 18f, e.pos, e.z + e.Eye - 18f, 18f)) { m.Aud("visão: inimigo no campo de visão (0,25 s)", "vê pelo raio da Sim"); continue; }
                    V2 lat = new V2(-d.y / dl, d.x / dl); float k = 1f - 0.28f * e.duck; string parte = null;
                    foreach (var (h, l, nome) in new[] { (22.5f, 0f, "topo da cabeça"), (15f, 4f, "ombro/braço"), (15f, -4f, "ombro/braço"), (4f, 3f, "perna/pé"), (4f, -3f, "perna/pé") })
                        if (MapData.LosWalk(a.pos, a.z + a.Eye, e.pos + lat * l, e.z + h * k, 0f)) { parte = nome; break; }
                    if (parte == null && MapData.LosWalkTela(a.pos, a.z + a.Eye - 18f, e.pos, e.z + e.Eye - 18f, 18f)) parte = "só pela quina arredondada da tela";
                    if (parte == null) continue;
                    string faixa = dl < 300 ? "<300" : dl < 700 ? "300-700" : "700+";
                    m.Aud("visão: inimigo no campo de visão (0,25 s)", "PARTE à vista, Sim não vê");
                    m.Aud("visão: parte à vista que a Sim não vê", parte + " · " + faixa + (luneta ? " · AWP na luneta" : "") + (da < 3f * MathF.PI / 180 ? " · na mira" : ""));
                    m.Aud("visão: onde (quem olha → alvo)", MapData.Callout(a.pos) + " → " + MapData.Callout(e.pos));
                }
            }
        }

        /// <summary>bx: corpo encostando no fogo com o centro fora (a Sim só queima o centro; a tela mostra o pé nas chamas).</summary>
        void BordaFogo(Agent a)
        {
            if (m.fires.Count == 0) return;
            bool centro = m.FireAt(a.pos); int toca = 0;
            for (int k = 0; k < 8; k++) if (m.FireAt(a.pos + V2.Ang(k * 0.785f) * Match.FOLGA)) toca++;
            if (centro) m.Aud("fogo: corpo x chamas (0,25 s)", "centro no fogo (queima)");
            else if (toca > 0) m.Aud("fogo: corpo x chamas (0,25 s)", "só a borda do corpo no fogo (não queima)");
        }

        /// <summary>Onde a mira bate: raio até a parede (até 800).</summary>
        static V2 Bate(Agent a) { V2 d = V2.Ang(a.aim); return a.pos + d * MapData.Raio(a.pos, a.z, d, 800, 18f); }

        static bool Mira(Agent a, V2 p) => (p - a.pos).Len > 30 && MathF.Abs(MathX.AngDiff((p - a.pos).Atan2, a.aim)) < MESMO;

        static readonly HashSet<string> MEIO = new HashSet<string> { "Topo_meio", "Topo_entrada", "Meio", "Praca", "Janelao", "Esc_janelao", "Conector", "Conector_dobra", "Conector_fim", "Curta", "Curta_sobe", "Curta_fim", "Rato", "Esc_rato", "Rato_fundo" };
        readonly HashSet<int> zonaFeita = new HashSet<int>();

        /// <summary>Ocupação do meio (mesma conta dos pros, por tempo e sem bomb plantado): 20/40/60 s.</summary>
        void Zonas()
        {
            int ts = (int)m.t; if (ts != 10 && ts != 20 && ts != 30 && ts != 40 && ts != 60 || m.bombPlanted || !zonaFeita.Add(ts)) return;
            foreach (var tm in new[] { Team.CT, Team.T })
            {
                int n = 0, vivos = 0, jan = 0;
                foreach (var a in m.agents)
                {
                    if (!a.alive || a.team != tm) continue;
                    vivos++; string z = MapData.AreaAt(a.pos.x, a.pos.y);
                    if (z != null && MEIO.Contains(z)) { n++; if (z == "Janelao") jan++; }
                }
                if (vivos == 0) continue;
                string q = $"meio {(tm == Team.CT ? "CT" : "TR")} aos {ts}s";
                m.Aud(q, n == 0 ? "0" : n == 1 ? "1" : "2+");
                for (int i = 0; i < vivos; i++) m.Aud(q + " (por jogador)", i < n ? "no meio" : "fora");
                if (tm == Team.CT) m.Aud($"janelão CT aos {ts}s", jan > 0 ? "tem" : "não");
            }
        }

        public void Passo()
        {
            Zonas();
            foreach (var tm in new[] { Team.CT, Team.T }) Time(tm);
            foreach (var a in m.agents) { if (a.alive) Jogador(a); }
            foreach (var a in m.agents) if (a.alive) Passagem(a);
            Fumaca(); foreach (var a in m.agents) if (a.alive) BordaFogo(a);
            VisaoCorpoAud();
            foreach (var a in m.agents) a.brain.PreNadeAud();
            if (m.t < 20)
                foreach (var a in m.agents)
                {   /* exposição na linha longa cedo: algum inimigo com visão de mim a mais de 300 (medição: posição real) */
                    if (!a.alive) continue; bool exposto = false;
                    foreach (var e in m.agents) if (e.alive && e.team != a.team && (e.pos - a.pos).Len > 300 && MapData.LosWalk(a.pos, a.z + a.Eye, e.pos, e.z + e.Eye, 0f) && !m.SmokeBlocks(a.pos, a.z + a.Eye, e.pos, e.z + e.Eye)) { exposto = true; break; }
                    if (!exposto) continue;
                    var b = a.brain; string est = b.Segurando ? (a.Speed < a.MaxSpeed * 0.2f ? "segurando parado" : "segurando se mexendo") : a.Speed < a.MaxSpeed * 0.2f ? "parado sem posição" : "andando (rota)";
                    var (cx, cy) = NavGrid.CellOf(a.pos); bool cover = NavGrid.folga[cx, cy] < 10f;
                    m.Aud("exposto na linha longa cedo (0,25 s)", $"{(a.IsT ? "TR" : "CT")} {Places.Region(a.pos)} · {est}{(cover ? " · colado em cover" : " · aberto")}");
                    if (!b.Segurando && a.Speed < a.MaxSpeed * 0.2f) { string on = b.order.name; int sp = on.IndexOf(':'); if (sp > 0) on = on.Substring(0, sp); if (on.Length > 30) on = on.Substring(0, 30); m.Aud("exposto parado sem posição cedo: fase · ordem", $"{(a.IsT ? "TR" : "CT")} {b.fase} · {on}"); }
                }
            foreach (var a in m.agents)
                if (a.alive && !a.noAr) { var (cx, cy) = NavGrid.CellOf(a.pos); if (NavGrid.onTop[cx, cy]) m.Aud("em cima de caixa (0,25 s)", (a.z - MapData.GZ >= 26 ? "alta (28) · " : "baixa (16) · ") + Places.Region(a.pos)); }
            foreach (var a in m.agents) if (!a.alive && fogoDesde[a.id] >= 0) { if (a.killedByFire) m.Aud("morreu no fogo: vida ao entrar", fogoHp[a.id] <= 30 ? "até 30" : fogoHp[a.id] <= 60 ? "31-60" : "61+"); fogoDesde[a.id] = -1; fogoSaiu[a.id] = m.t; }
        }

        /// <summary>Parado em passagem estreita (porta/beco: folga ≤ 15) sem estar em combate: alvo fácil e trava o colega.</summary>
        void Passagem(Agent a)
        {
            var b = a.brain;
            if (a.Speed > 3 || b.target != null || b.action != null) return;
            var (cx, cy) = NavGrid.CellOf(a.pos);
            if (NavGrid.folga[cx, cy] > 15f) return;
            string tipo = b.Segurando ? "segurando" : "sem posição";
            m.Aud("parado em passagem estreita (0,25 s)", tipo + " · " + (b.fase ?? "?"));
            foreach (var o in m.agents)
                if (o != a && o.alive && o.team == a.team && (o.pos - a.pos).Len < 26 && o.brain.wantVel.Len > 20 && o.Speed < 6)
                { m.Aud("travou o colega numa passagem estreita (0,25 s)", tipo + " · " + (b.fase ?? "?")); break; }
        }

        void Time(Team tm)
        {
            string lado = tm == Team.CT ? "CT" : "TR";
            var livres = new List<Agent>();
            foreach (var a in m.agents) if (a.alive && a.team == tm && a.brain.target == null && a.brain.action == null) livres.Add(a);
            if (livres.Count < 3) return;
            var hit = new V2[livres.Count];
            for (int i = 0; i < hit.Length; i++) hit[i] = Bate(livres[i]);
            int maior = 1, dono = -1;
            for (int i = 0; i < hit.Length; i++)
            {
                int n = 1;
                for (int j = 0; j < hit.Length; j++) if (i != j && (hit[j] - hit[i]).Len < 120 && Mira(livres[j], hit[i])) n++;
                if (n > maior) { maior = n; dono = i; }
            }
            string q = m.firstSightT >= 0 && m.t > m.firstSightT ? "depois do 1º contato" : "antes do 1º contato";
            m.Aud($"mira igual {lado}, {q}", maior >= 3 ? "3+ no mesmo ponto" : maior == 2 ? "2 no mesmo ponto" : "todos diferentes");
            if (maior >= 3) m.Aud($"por que 3+ {lado} miram igual", livres[dono].brain.fonteMira);
            int viraram = 0;
            foreach (var a in livres) if (MathF.Abs(MathX.AngDiff(a.aim, aimAnt[a.id])) > 0.52f) viraram++;
            m.Aud($"virada junta {lado}", viraram >= 3 ? "3+ viraram no mesmo 0,25 s" : viraram == 2 ? "2 viraram" : "0-1 virou");
        }

        void Jogador(Agent a)
        {
            var b = a.brain; float d = MathF.Abs(MathX.AngDiff(a.aim, aimAnt[a.id]));
            string lado = a.IsT ? "TR" : "CT";
            if (b.target == null && b.action == null)
            {
                // mira pulando pra lá e pra cá = duas regras brigando pela mira
                if (d > 1f && fonteAnt[a.id] != b.fonteMira)
                {
                    bool volta = m.t - pulo[a.id] < 1f; pulo[a.id] = m.t;
                    m.Aud("conflito de mira", volta ? "pulou 60°+ duas vezes em 1 s" : "pulou 60°+ uma vez");
                    if (volta) m.Aud("quem briga pela mira", $"{fonteAnt[a.id]} x {b.fonteMira}");
                }
                else m.Aud("conflito de mira", "estável");
                // andando onde pode ter inimigo: está checando o pixel?
                if (a.Speed > a.MaxSpeed * 0.4f && m.intel[(int)a.team].Esperados(a.pos, 350) > 0.25f)
                {
                    bool pressa = m.t < b.tradeUntil || b.fase == "ajudando no duelo" || b.order.defuse || b.order.plant || m.RoundLeft < 20;
                    bool abrindo = m.t - b.abrirT < 0.2f || b.Segurando && b.alvoMira != null && MathF.Abs(MathX.AngDiff((b.alvoMira.Value - a.pos).Atan2, a.aim)) < 0.35f;
                    m.Aud("andando com inimigo possível", pressa ? "com pressa (pode)" : abrindo ? "abrindo pixel" : "reto sem abrir");
                    if (!pressa && !abrindo) m.Aud("quem anda reto", $"{b.fase} / mira: {b.fonteMira}");
                }
                // segurando: enxerga o que diz que segura?
                if (b.Segurando && b.alvoMira != null)
                {
                    V2 p = b.alvoMira.Value;
                    bool ve = MapData.LosWalk(a.pos, a.z, p, NavGrid.ZAt(p), 18f);
                    m.Aud($"segurando {lado}", (ve ? "vê o ângulo" : "fora de vista (cover)") + (a.Speed < 3 ? ", parado" : ", andando"));
                }
            }
            if (m.FireAt(a.pos)) m.Aud("dentro do fogo", b.action != null ? "em ação (plantar/desarmar)" : b.target != null ? "duelando" : a.Speed < 3 ? "PARADO no fogo" : "saindo"); if (m.FireAt(a.pos) && b.action == null && b.target == null && a.Speed < 3) m.Aud("parado no fogo: fase", b.fase + (a.noAr ? " (no ar)" : ""));
            if (m.FireAt(a.pos)) { if (fogoDesde[a.id] < 0) { m.Aud("entrada no fogo", m.t - fogoSaiu[a.id] < 1.5f ? "VOLTOU pro fogo em até 1,5 s" : "entrada normal"); fogoDesde[a.id] = m.t; fogoHp[a.id] = a.hp; } }
            else if (fogoDesde[a.id] >= 0) { float s = m.t - fogoDesde[a.id]; m.Aud("tempo dentro do fogo", s <= 0.5f ? "até 0,5 s" : s <= 1 ? "até 1 s" : s <= 2 ? "até 2 s" : "mais de 2 s"); fogoDesde[a.id] = -1; fogoSaiu[a.id] = m.t; }
            aimAnt[a.id] = a.aim; fonteAnt[a.id] = b.fonteMira;
        }

        /// <summary>No fim do round: o porquê de cada decisão é dela mesma ou só repete a ordem?</summary>
        public void Fim()
        {
            m.sus.Regua();
            if (m.reason == "tempo acabou")
                m.Aud("round acabou no tempo", m.bombPlanted ? "bomb plantado" : m.bombDropped ? $"C4 no chão em {Places.Region(m.droppedBomb)}{(float.IsInfinity(NavGrid.PathLen(m.droppedBomb, Places.Plant('A'))) ? " (SEM CAMINHO)" : "")}" : m.Alive(Team.T) == 0 ? "sem TR" : "C4 com alguém: " + (System.Array.Find(m.agents.ToArray(), a => a.hasBomb)?.brain.order.name ?? "?"));
            if (m.reason == "tempo acabou")
            {
                int gu = 0; foreach (var a in m.agents) if (a.alive && a.IsT && a.guardando) gu++;
                m.Aud("tempo: TR vivos", m.Alive(Team.T).ToString());
                m.Aud("tempo: CT vivos", m.Alive(Team.CT).ToString());
                m.Aud("tempo: quem guardava", gu == 0 ? "ninguém guardando" : gu == m.Alive(Team.T) ? "todo TR vivo guardando" : "parte guardando");
                m.Aud("tempo: o time executou?", m.igl[(int)Team.T].Executou ? "executou e não plantou" : "nem chegou a executar");
            }
            foreach (var a in m.agents)
                foreach (var d in a.brain.decisoes)
                {
                    if (d.s.StartsWith("vi ") || d.s.StartsWith("atirando") || d.s.StartsWith("morri") || d.s.StartsWith("levei")) continue;   // fato, não decisão
                    bool generico = d.porque == null || d.porque.StartsWith("ordem:");
                    m.Aud("porquê das decisões", generico ? "genérico (repete a ordem)" : "motivo próprio");
                    if (generico)
                    {
                        string s = d.s; int i = 0, esp = 0;
                        for (; i < s.Length && esp < 2; i++) if (s[i] == ' ') esp++;
                        m.Aud("decisões sem motivo próprio", s.Substring(0, i).Trim());
                    }
                }
        }
    }
}
