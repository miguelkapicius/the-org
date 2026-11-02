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
            foreach (var a in m.agents) if (!a.alive && fogoDesde[a.id] >= 0) { if (a.killedByFire) m.Aud("morreu no fogo: vida ao entrar", fogoHp[a.id] <= 30 ? "até 30" : fogoHp[a.id] <= 60 ? "31-60" : "61+"); fogoDesde[a.id] = -1; fogoSaiu[a.id] = m.t; }
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
