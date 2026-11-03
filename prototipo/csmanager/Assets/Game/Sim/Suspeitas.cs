using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Detector do que um humano vendo o replay chamaria de estranho. Cada tipo nasceu de uma classe de reclamação real e
    /// mede o defeito, não o sintoma: parado junto do time esperando a hora não marca, parado longe de todo mundo marca.
    /// Grave = quase certo que é erro; Atenção = olhar o replay, pode ter motivo. Cada tipo marca uma vez por jogador
    /// (ou por time) no round, com o instante, pra ir direto ver (replay) e contar (relatório, com exemplos por semente).
    /// </summary>
    public class Suspeitas
    {
        public const string GRAVE = "grave", ATENCAO = "atenção";
        const int N = 10;
        readonly Match m;
        readonly HashSet<string> uma = new HashSet<string>();
        readonly List<float> maosC4 = new List<float>();
        readonly List<float>[] ordens = new List<float>[N];
        readonly float[] apanhou = new float[N]; readonly Agent[] quemAtirou = new Agent[N];
        readonly int[] miraParede = new int[N], miraLonge = new int[N], miraColega = new int[N], noFogo = new int[N], amontoado = new int[N];
        readonly List<(float t, int matador, int vitima)> mortes = new List<(float, int, int)>();
        float c4Dist0 = -1;
        float vantagemDesde = -1, c4CaiuEm = -1, plantDesde = -1, ultimoTiro; Agent plantador; int giros;
        public int pontos;   // o tamanho do problema do round: grave 3, atenção 1

        public Suspeitas(Match m)
        {
            this.m = m;
            for (int i = 0; i < N; i++) { ordens[i] = new List<float>(); apanhou[i] = -99; }
        }

        void Marca(Agent a, string tipo, string grau, string texto)
        {
            if (!uma.Add(tipo + a.id)) return;
            Registra(a.id, tipo, grau, texto);
        }
        /// <summary>Defeito do time (não repete uma linha por jogador).</summary>
        void MarcaTime(Team time, string tipo, string grau, string texto)
        {
            if (!uma.Add(tipo + time)) return;
            int id = 0; foreach (var a in m.agents) if (a.team == time) { id = a.id; if (a.alive) break; }
            Registra(id, tipo, grau, texto);
        }
        void Registra(int id, string tipo, string grau, string texto)
        {
            m.suspeitas.Add((m.t, id, tipo, $"[{grau}] {texto}")); m.Ev($"SUSPEITA {tipo}/round");
            pontos += grau == GRAVE ? 3 : 1;
            Match.GuardaExemplo(tipo, m.seedRound, m.t, texto);
        }

        // ---- eventos ----
        public void MudouMaoC4(Agent a)
        {
            float t = m.t; maosC4.Add(t);
            if (maosC4.Count(x => t - x < 10) >= 3) MarcaTime(Team.T, "C4 de mão em mão", GRAVE, $"o C4 trocou de mão 3 vezes em 10 s (agora com {a.name}): time gastando tempo sem levar ele a lugar nenhum");
        }
        public void CaiuC4(Agent v, int colegas, bool alguemNaFrente)
        {
            c4CaiuEm = m.t; c4Dist0 = -1;
            // não é "abrir o bomb": plantando, no meio do bando (rush, entrada junto); depois que o time já trocou tiro ali é só atenção
            bool plantando = (v.pos - m.PlantSpot).Len < 60;
            bool noBando = m.agents.Any(x => x != v && x.alive && x.team == v.team && (x.pos - v.pos).Len < 130);
            bool jaTeveContato = m.agents.Any(x => x != v && x.team == v.team && !x.alive && m.t - x.diedAt < 8);
            if (colegas >= 2 && !alguemNaFrente && !m.bombPlanted && !plantando && !noBando)
                Marca(v, "C4 abriu o bomb", jaTeveContato ? ATENCAO : GRAVE, $"{v.name} morreu com o C4 na frente de {colegas} colegas em {Places.Region(v.pos)}: o C4 entra atrás de quem abre");
        }
        public void Ordem(Agent a, string nome)
        {
            var l = ordens[a.id]; float t = m.t; l.Add(t); l.RemoveAll(x => t - x > 3);
            if (l.Count >= 4) Marca(a, "ordens trocando", ATENCAO, $"{a.name} recebeu {l.Count} ordens em 3 s (última: {nome}): não dá pra executar nada assim");
        }
        public void Dano(Agent v, Agent por) { if (por == null) return; apanhou[v.id] = m.t; quemAtirou[v.id] = por; ultimoTiro = m.t; }
        public void Morte(Agent v, Agent por)
        {
            ultimoTiro = m.t;
            if (por == null) return;
            if (v.guardando && (m.t - apanhou[v.id]) > 1.2f) Marca(v, "morreu guardando", ATENCAO, $"{v.name} morreu guardando a arma em {Places.Region(v.pos)} pro {por.name}");
            mortes.Add((m.t, por.id, v.id));
            int seguidas = mortes.Count(x => x.matador == por.id && m.t - x.t < 25);
            bool semTroca = por.alive;
            if (seguidas >= 3 && semTroca && por.team != v.team) Marca(por, "um segurou o time sozinho", ATENCAO, $"{por.name} matou {seguidas} em 25 s em {Places.Region(por.pos)} sem levar troca: entraram um de cada vez");
        }
        public void InicioPlant(Agent a) { plantDesde = m.t; plantador = a; }
        public void Giro(Team time, char site, bool desfez)
        {
            giros++;
            if (desfez) MarcaTime(time, "giro desfeito", ATENCAO, $"CT girou e voltou atrás pro {site} sem ter visto nada novo");
            if (giros >= 4) MarcaTime(time, "gira demais", ATENCAO, $"CT já girou {giros} vezes neste round");
        }
        public void LeituraMudou(char site) => MarcaTime(Team.T, "IGL trocou de bomb", ATENCAO, $"IGL do TR mudou de ideia pro {site}: a estimativa virou, não a informação");

        // ---- a cada segundo ----
        public void Passo()
        {
            if (m.over) return;
            float t = m.t;
            Vantagem(t); TimeC4(t); Parado(t); Plant(t); Retake(t);
            foreach (var a in m.agents)
            {
                if (!a.alive) continue;
                Mira(a, t);
                Apanhando(a, t);
                var b = a.brain;
                noFogo[a.id] = m.FireAt(a.pos) && b.action == null && b.target == null && a.Speed < 15 ? noFogo[a.id] + 1 : 0;
                if (noFogo[a.id] >= 2) Marca(a, "parado no fogo", GRAVE, $"{a.name} parado na molotov há {noFogo[a.id]} s em {Places.Region(a.pos)}");
                if (a.paradoHa > 10 && b.order.hold == null && !b.order.malote && t >= b.order.notBefore && !a.guardando && !b.order.name.StartsWith("girando") && !b.order.name.StartsWith("juntar") && !b.order.name.StartsWith("levando"))
                    Marca(a, "parado sem posição", ATENCAO, $"{a.name} parado há {a.paradoHa:0} s sem segurar nada: {b.order.name} ({b.fase})");
                if (a.IsT && a.team == Team.T && t > 15 && !m.bombPlanted && !b.order.push)
                {
                    int perto = 0; foreach (var x in m.agents) if (x.alive && x.team == a.team && (x.pos - a.pos).Len < 70) perto++;
                    amontoado[a.id] = perto >= 3 && a.Speed < 30 ? amontoado[a.id] + 1 : 0;
                    if (amontoado[a.id] >= 5) MarcaTime(Team.T, "time amontoado", ATENCAO, $"{perto} TR colados há 5 s em {Places.Region(a.pos)}, sem ninguém pegando espaço");
                }
                if (a.guardando && m.bombPlanted && m.BombLeft < 5 && (a.pos - m.bombPos).Len < Match.RAIO_BOMB)
                    Marca(a, "guardou no raio", GRAVE, $"{a.name} foi guardar a arma dentro do raio do bomb");
            }
        }

        void Vantagem(float t)
        {
            bool vant = !m.bombPlanted && m.Alive(Team.T) - m.Alive(Team.CT) >= 2;
            if (!vant) { vantagemDesde = -1; return; }
            if (vantagemDesde < 0) vantagemDesde = t;
            Agent port = null; foreach (var a in m.agents) if (a.alive && a.hasBomb) port = a;
            // o portador indo pro bomb é o jogo acontecendo; só vale se ninguém está levando ou já passou demais da conta
            bool levando = port != null && port.brain.order.plant && port.Speed > 30;
            float limite = levando ? 40 : 20;
            if (t - vantagemDesde > limite)
                MarcaTime(Team.T, "vantagem sem plantar", ATENCAO, $"{m.Alive(Team.T)}x{m.Alive(Team.CT)} há {t - vantagemDesde:0} s sem plantar; C4 {(port != null ? $"com {port.name} ({port.brain.order.name})" : "no chão")}");
        }

        void TimeC4(float t)
        {
            if (m.bombPlanted) return;
            int vivos = m.Alive(Team.T);
            foreach (var a in m.agents)
            {
                if (!a.alive || !a.hasBomb) continue;
                var b = a.brain; float aqui = a.c4Desde >= 0 ? t - a.c4Desde : 0;
                bool comTime = false;
                foreach (var x in m.agents) if (x != a && x.alive && x.team == a.team && (x.pos - a.pos).Len < 450) comTime = true;
                if (aqui > 10 && !b.order.plant && !comTime) Marca(a, "C4 parado longe do time", GRAVE, $"{a.name} há {aqui:0} s no mesmo lugar com o C4, longe de todo mundo ({b.fase}) em {Places.Region(a.pos)}");
                if (t > 35 && vivos >= 2 && !b.order.plant && Places.Region(a.pos) == "base TR") Marca(a, "C4 na base", GRAVE, $"C4 ainda na base TR aos {t:0} s com {a.name} ({b.order.name}, {b.fase})");
            }
            if (m.bombDropped)
            {
                if (c4CaiuEm < 0) c4CaiuEm = t;
                if (c4Dist0 < 0) { c4Dist0 = 0; foreach (var a in m.agents) if (a.alive && a.IsT) { float pl = NavGrid.PathLen(a.pos, m.droppedBomb); if (pl > 0 && !float.IsInfinity(pl)) c4Dist0 = c4Dist0 == 0 ? pl : MathF.Min(c4Dist0, pl); } }
                bool base_ = Places.Region(m.droppedBomb) == "base TR";
                bool vigiado = false; foreach (var c in m.intel[(int)Team.T].contacts.Values) if (t - c.t < 10 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 650) vigiado = true;
                // o prazo é o de quem estava mais perto ir andando (shift) até ele, com folga — longe demora, e tudo bem
                float prazo = MathF.Max(15, c4Dist0 / (MapCheck.RUN * 0.45f) + 8);
                if (t - c4CaiuEm > prazo && vivos >= 2 && m.RoundLeft > 10 && !vigiado && !(m.bombGuardado && t < 50)) MarcaTime(Team.T, "C4 esquecido no chão", GRAVE, $"C4 há {t - c4CaiuEm:0} s no chão em {Places.Region(m.droppedBomb)} com {vivos} TR vivos");
                if (t > 35 && base_ && vivos >= 2) MarcaTime(Team.T, "C4 na base", GRAVE, $"C4 no chão da base TR aos {t:0} s");
                if (vivos == 1 && m.RoundLeft < 20)
                    foreach (var a in m.agents)
                        if (a.alive && a.IsT && a.brain.order.name.StartsWith("pegar o bomb") && m.intel[(int)Team.T].contacts.Values.Any(c => t - c.t < 8 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 500))
                            Marca(a, "sozinho no C4 vigiado", ATENCAO, $"{a.name} sozinho indo no C4 vigiado com {m.RoundLeft:0} s: morre sem chance de plantar");
            }
            else { c4CaiuEm = -1; c4Dist0 = -1; }
        }

        void Parado(float t)
        {
            if (t < 50 || m.bombPlanted || m.Alive(Team.T) < 3 || t - ultimoTiro < 25 || m.RoundLeft < 8) return;
            float ultimo = 0;
            foreach (var it in m.intel) foreach (var c in it.contacts.Values) ultimo = MathF.Max(ultimo, c.t);
            if (t - ultimo > 20) MarcaTime(Team.T, "round parado", ATENCAO, $"{t - MathF.Max(ultimo, ultimoTiro):0} s sem tiro nem contato com {m.Alive(Team.T)} TR vivos");
        }

        void Plant(float t)
        {
            if (plantDesde < 0 || m.bombPlanted || plantador == null || !plantador.alive || plantador.brain.action != "plantar") { plantDesde = -1; return; }
            float resta = plantDesde + Match.PLANT - t;
            foreach (var a in m.agents)
            {
                if (!a.alive || a.IsT || a.guardando || a.brain.target != null) continue;
                float chega = NavGrid.PathLen(a.pos, plantador.pos) / MathF.Max(a.MaxSpeed, 1);
                if (chega < resta + 0.6f && a.Speed < 30) Marca(a, "viu o plant passar", GRAVE, $"{a.name} a {chega:0.0} s do {plantador.name} plantando ({resta:0.0} s de plant) e ficou parado");
            }
        }

        void Retake(float t)
        {
            if (!m.bombPlanted || m.over) return;
            Agent melhor = null; float eta = float.MaxValue; bool algumIndo = false; int vivos = 0;
            foreach (var a in m.agents)
            {
                if (!a.alive || a.IsT || a.guardando) continue;
                vivos++;
                float e = NavGrid.PathLen(a.pos, m.bombPos) / MathF.Max(a.MaxSpeed, 1) + Match.DefuseTime(a);
                if (e < eta) { eta = e; melhor = a; }
                if (a.brain.order.defuse || a.Speed > 60) algumIndo = true;
            }
            if (melhor != null && vivos >= 2 && !algumIndo && m.BombLeft < eta + 4 && m.BombLeft > eta)
                MarcaTime(Team.CT, "retake não saiu", GRAVE, $"{vivos} CT vivos, {m.BombLeft:0} s de bomb e {eta:0} s pra desarmar: era a última hora e ninguém foi");
        }

        void Mira(Agent a, float t)
        {
            var b = a.brain;
            bool livre = b.target == null && b.action == null;
            V2 olho = V2.Ang(a.aim); var it = m.intel[(int)a.team];
            bool perto = false, longe = false;
            if (livre)
                foreach (var c in it.contacts.Values)
                {
                    if (t - c.t > 4 || !m.agents[c.id].alive) continue;
                    V2 d = c.pos - a.pos; float dl = d.Len;
                    if (t - c.t < 2 && dl < 350) perto = true;
                    // olhando uma quina na mesma direção é só coincidência; o defeito é a mira colada numa parede perto
                    if (dl > 450 && a.Speed < 30 && MathF.Abs(MathX.AngDiff(d.Atan2, a.aim)) < 0.12f && MapData.Raio(a.pos, a.z, olho, 100, a.Eye) < 95) longe = true;
                }
            bool parede = livre && perto && !MapData.LosWalk(a.pos, a.z, a.pos + olho * 60, a.z, a.Eye);
            miraParede[a.id] = parede ? miraParede[a.id] + 1 : 0;
            if (miraParede[a.id] >= 3) Marca(a, "mira na parede com inimigo conhecido", ATENCAO, $"{a.name} em {Places.Region(a.pos)} mirando a parede há 3 s com inimigo visto perto ({b.fase})");
            miraLonge[a.id] = longe ? miraLonge[a.id] + 1 : 0;
            if (miraLonge[a.id] >= 3) Marca(a, "mirando através do mapa", ATENCAO, $"{a.name} parado em {Places.Region(a.pos)} há 3 s mirando por dentro da parede em quem está longe ({b.order.name})");
            bool colega = false;
            if (livre && a.Speed < 30)
                foreach (var x in m.agents)
                {
                    if (x == a || !x.alive || x.team != a.team) continue;
                    V2 d = x.pos - a.pos; float dl = d.Len;
                    if (dl > 40 && dl < 200 && MathF.Abs(MathX.AngDiff(d.Atan2, a.aim)) < 0.12f && MapData.LosWalk(a.pos, a.z, x.pos, x.z, a.Eye)) colega = true;
                }
            miraColega[a.id] = colega ? miraColega[a.id] + 1 : 0;
            if (miraColega[a.id] >= 5) Marca(a, "olhando pro colega", ATENCAO, $"{a.name} há 5 s com a mira nas costas de um colega em {Places.Region(a.pos)}");
        }

        void Apanhando(Agent a, float t)
        {
            float ha = t - apanhou[a.id];
            if (ha < 1 || ha > 2.2f) return;
            var b = a.brain;
            if (b.target != null || b.action != null || a.Speed > 30 || a.Blind(t)) return;
            var por = quemAtirou[a.id];   // já mirando pra quem atirou é reação, mesmo sem ele à vista
            if (por == null || MathF.Abs(MathX.AngDiff((por.pos - a.pos).Atan2, a.aim)) < 0.8f) return;
            if (a.guardando) Marca(a, "guardando sob fogo", GRAVE, $"{a.name} levou tiro guardando a arma e ficou parado em {Places.Region(a.pos)}");
            else Marca(a, "levou tiro e não reagiu", GRAVE, $"{a.name} levou tiro em {Places.Region(a.pos)} e {ha:0.0} s depois nem virou nem saiu ({b.fase})");
        }

        // ---- fim do round ----
        public void Fim(string motivo)
        {
            if (motivo != null && motivo.Contains("tempo") && m.Alive(Team.T) >= 2)
            {
                int gu = 0; foreach (var a in m.agents) if (a.alive && a.IsT && a.guardando) gu++;
                if (gu < m.Alive(Team.T)) MarcaTime(Team.T, "tempo com TR vivo", GRAVE, $"tempo acabou com {m.Alive(Team.T)} TR vivos, {gu} guardando");
            }
            m.Ev("rounds com suspeita grave", m.suspeitas.Any(s => s.texto.StartsWith("[grave]")) ? 1 : 0);
            m.Ev("pontos de suspeita/round", pontos);
        }
    }
}
