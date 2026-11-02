using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using static CSM.Sim.Places;

namespace CSM.Sim
{
    /// <summary>Ordem do IGL pra um jogador (o bot executa do jeito dele).</summary>
    public class Order
    {
        public string name, why;
        public V2 dest; public Hold hold;
        public bool run = true, quiet;                       // correndo / andando de shift (sem barulho)
        public float notBefore;                              // só sai depois desse tempo do round
        public List<Lineup> nades = new List<Lineup>();
        public bool calmo;                                   // entrada do retake: anda de shift enquanto o relógio deixa
        public bool plant, defuse, push;                     // push = entra no bomb limpando (entry/execução)
        public Agent follow; public float followDist = 110;  // suporte: vai atrás do entry pra trocar
        public bool waitSmoke; public V2 smokeAt;            // espera a smoke subir antes de sair
        public float until = 999; public Order then;         // jogada com hora pra acabar (agressão cedo) e o que fazer depois
        public bool paciente;                                // jogo lento: segura e espera o outro se expor (menos wide/avanço)
        public bool zona;                                    // jogada pra tomar espaço (agressão cedo): no fim decide se segura, malota ou volta
        public bool zonaSegura;                              // decidiu ficar na zona (reavalia de tempos em tempos)
        public bool malote; public V2 maloteOlha; public char maloteSite; public float maloteDesde; public int deixaPassar;   // escondido fora da rota deles: deixa passar e pega de costas
    }

    /// <summary>
    /// Cabeça de um jogador. A cada passo: vê (FOV + linha de visão + smoke + cego), reage (tempo de reação pelo
    /// quanto precisa virar a mira e se já estava mirando ali), atira (para antes = counter-strafe; cabeça perto, peito
    /// longe; spray compensado), e sem inimigo cumpre a ordem do IGL: anda pela navmesh limpando os ângulos de onde pode
    /// ter inimigo (mapa de calor), dá pre-fire em canto comum, troca o aliado que morreu, joga granada, planta e desarma.
    /// Cada decisão nova vira um "pensamento" com o motivo (o painel mostra; o log guarda tudo).
    /// </summary>
    public class Brain
    {
        public readonly Agent me; readonly Match m; readonly Rng rng;
        public Order order = new Order { name = "esperar" };
        public string thought = ""; public readonly List<(float t, string s)> log = new List<(float, string)>();
        public char homeSite = 'M';
        // caminho
        public List<NavGrid.Waypoint> path; int wp; V2 pathGoal; float pathAt = -9;
        public V2 wantVel; float wantAim; bool hasWantAim; float wantPitch;
        // combate
        public Agent target; public float reactAt, alvoDesde; public V2 lastSeenPos; public float lastSeenT = -9; Agent lastSeen;
        int burst; float errV0;
        bool adadDuelo, agachaDuelo; int adadLado = 1; float adadTroca;
        string faseAnt = "", miraFonte = "";
        public float flickAlvo;   // quantos graus a mira estava fora quando viu o alvo atual (anatomia do HS)
        public string fonteMira = ""; public V2? alvoMira; public float abrirT = -9;   // de onde veio a mira agora (auditor)
        public bool Segurando => baseP != null && order.hold != null;
        V2 Fonte(string f, V2 p) { fonteMira = f; return p; }
        /// <summary>Como ele entrou no duelo: segurando quieto, peek, andando...</summary>
        public string Situacao => (lastMicroOuFase ?? "") switch { "Quieto" or "quieto (escolha)" => "segurando quieto", "pausa no cover" => "no cover", "pausa segurando aberto" => "segurando aberto", "Jiggle" or "Wide" or "Avancar" or "Bang" => "peek", "Trocar" or "Ajeitar" => "se mexendo no lugar", "andando" => "andando", _ => "outro" };
        /// <summary>A posição que ele segura, se está nela.</summary>
        public string PosicaoAgora => order.hold != null && (order.hold.pos - me.pos).Len < 150 ? (me.IsT ? "TR " : "CT ") + order.hold.name : null;
        public string lastMicroOuFase = "";   // o que estava fazendo antes de entrar no duelo (anatomia dos duelos)
        // limpeza de ângulos
        public V2? focus; string focusName = ""; float focusT; readonly HashSet<int> prefired = new HashSet<int>(); readonly HashSet<int> wasVisible = new HashSet<int>(), ignorado = new HashSet<int>();
        List<Spots.Spot> nearSpots = new List<Spots.Spot>(); float spotsAt = -9;
        // ações
        public string action; float actionEnd; Lineup actionNade;
        public float ActionEnd => actionEnd;   // (vídeo: progresso da animação de plantar/desarmar/granada)
        int holdLook; float holdLookUntil;
        public float tradeUntil = -9; V2 tradePos;
        public float stuckT; V2 stuckRef; float stuckCheck;
        // segurar ativo (micro): segurando posição o bot não fica plantado — dá jiggle, wide, ajeita, troca de ângulo,
        // avança; ficar quieto é uma escolha (ouviu passo, é AWP), não falta do que fazer
        public enum Micro { Nada, Jiggle, Wide, Ajeitar, Trocar, Avancar, Quieto, Bang }
        static readonly string[] FASE_MICRO = { "", "jiggle", "wide", "ajeitar", "trocar de ângulo", "avançar", "quieto (escolha)", "bang e peek" };
        public string fase = "";   // o que está fazendo agora (validador: em que fase fica parado)
        public Micro micro; int microFase, microRep; V2 microAlvo, microRumo; float microAte, microProx, microDwell;
        V2? baseP; V2 ancora; float baseDesde, planAt = -9; V2 baseLook = new V2(-999, -999); V2 peekPt; bool temPeek, deCover;
        readonly List<V2> trilha = new List<V2>(); float trilhaAt;
        float entregouEm = -9; int strafeLado = 1; bool strafeEssa;
        Pesos P => me.pesos;

        public Brain(Agent me, Match m, int seed) { this.me = me; this.m = m; rng = new Rng(seed); margemFuga = rng.Uniform(0.3f, 3f); }

        // ---- perfil (MatchCli.Stats -perfil)
        public static readonly System.Collections.Concurrent.ConcurrentDictionary<string, long> Perfil = new System.Collections.Concurrent.ConcurrentDictionary<string, long>();
        static long P0() => Match.Perfil ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
        static void P1(string nome, long c0) { if (Match.Perfil) Perfil.AddOrUpdate(nome, System.Diagnostics.Stopwatch.GetTimestamp() - c0, (_, v) => v + System.Diagnostics.Stopwatch.GetTimestamp() - c0); }

        /// <summary>Atirou, levou tiro ou matou: o inimigo sabe onde ele está (pede trocar de ângulo).</summary>
        public void Entregou() => entregouEm = m.t;

        // levou tiro de quem não está vendo: larga a granada e vira pra onde o tiro veio (de onde veio, não o pixel exato)
        float levouAte = -9; V2 levouDe;
        public void Atingido(Agent by)
        {
            if (by == null || target == by) return;
            if (m.t - lastSeenT > 1.5f) m.aparecidos.Add((me.team, me.pos, m.t, m.bombPlanted, (by.pos - me.pos).Atan2));   // pegou desprevenido: também conta como de onde ele aparece
            if (!me.alive) return;
            levouDe = by.pos + V2.Ang(rng.Uniform(0, 6.2832f)) * rng.Uniform(0, 20);
            levouAte = m.t + rng.Uniform(0.6f, 1f);
            if (action == "granada") { Think("levei tiro — larguei a granada"); action = null; }
        }

        readonly Dictionary<string, float> lastLogged = new Dictionary<string, float>();
        float holdThoughtUntil;
        /// <summary>Pensamento novo: mostra no painel e grava no log (o mesmo pensamento só volta pro log depois de 4 s,
        /// pra não encher de "segurando/checando" repetido). Pensamento de evento (vi, atirei, morri) segura o painel 1 s.</summary>
        public void Think(string s, string porque = null)
        {
            string pq = porque ?? (porquePendenteT == m.t ? porquePendente : null);
            if (s == thought) return;
            if (!lastLogged.TryGetValue(s, out float lt) || m.t - lt > 4f)
            {
                lastLogged[s] = m.t; log.Add((m.t, s));
                if (Explicar)
                {
                    string q = pq ?? Motivo(s); porqueDe[s] = q;
                    decisoes.Add((m.t, s, q, Contexto()));
                    if (pq != null) m.Aud("motivo calculado", "chegou na decisão");
                }
            }
            bool evento = s.StartsWith("vi ") || s.StartsWith("atirando") || s.StartsWith("levei") || s.StartsWith("morri") || s.StartsWith("pre-fire") || s.Contains(" — ");
            if (!evento && m.t < holdThoughtUntil) return;
            if (evento) holdThoughtUntil = m.t + 1f;
            thought = s;
            if (Explicar) { porqueAgora = pq ?? (porqueDe.TryGetValue(s, out var pd) ? pd : Motivo(s)); contextoAgora = Contexto(); }
        }

        /// <summary>O motivo de um pensamento sem conta própria: de onde veio a mira e o que tem lá; a ordem só se não há mais nada.</summary>
        string Motivo(string s)
        {
            string ordem = order.why != null ? $"ordem: {order.name} ({order.why})" : $"ordem: {order.name}";
            int corte = s.IndexOf(" — "); if (corte < 0) corte = s.IndexOf(": ");
            if (corte > 0 && !s.StartsWith("segurando")) return s.Substring(corte + (s[corte] == ':' ? 2 : 3));   // o pensamento já diz o motivo
            if (alvoMira == null || !s.StartsWith("segurando")) return ordem;
            return $"mira em {Region(alvoMira.Value)} por {fonteMira}, ameaça ali {Ameaca(alvoMira.Value):0.00} · {ordem}";
        }
        readonly Dictionary<string, string> porqueDe = new Dictionary<string, string>();

        /// <summary>Liga o registro do porquê (replay e Play; nas estatísticas fica desligado pra não gastar à toa).</summary>
        public static bool Explicar;
        /// <summary>Cada decisão com o motivo (o que pesou) e a situação daquele instante.</summary>
        public readonly List<(float t, string s, string porque, string ctx)> decisoes = new List<(float, string, string, string)>();
        public string porqueAgora = "", contextoAgora = "";
        string pendente; float porquePendenteT = -9;   // motivo da decisão deste passo: vale pra todo pensamento do mesmo passo
        string porquePendente { get => pendente; set { pendente = value; porquePendenteT = m.t; } }

        string Contexto()
        {
            var it = m.intel[(int)me.team]; int vistos = 0;
            foreach (var c in it.contacts.Values) if (m.t - c.t < 3 && m.agents[c.id].alive) vistos++;
            return $"{m.Alive(me.team)}x{m.Alive(me.IsT ? Team.CT : Team.T)} · {me.hp} hp · {me.gunId} · {Region(me.pos)} · devem ter {it.Esperados(me.pos, 420):0.0} perto · {vistos} visto(s) há <3 s · fase: {faseAnt}";
        }

        float ordemDesde = -9;
        /// <summary>Som que EU ouvi (o time só sabe depois do call).</summary>
        public void Ouviu(V2 p, float t, string kind) { somP = p; somT = t; somK = kind; }
        V2 somP; float somT = -9; string somK = "";

        /// <summary>Quantos do time já cuidam desse ponto: dois por lados diferentes é crossfire; dois na mesma linha é um ângulo aberto em outro lugar.</summary>
        bool Lotado(V2 L)
        {
            if (EXP.Contains("semReserva")) return false;
            int n = 0; V2 dm = L - me.pos;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || !x.brain.Reservou(L)) continue;
                n++;
                if (MathF.Abs(MathX.AngDiff((L - x.pos).Atan2, dm.Atan2)) < 0.6f) return true;
            }
            return n >= 2;
        }
        /// <summary>Esse ponto chega em mim logo: perto o bastante pra eu não poder deixar pro colega.</summary>
        bool Ameacado(V2 L) => (L - me.pos).Len < 420;
        public bool Reservou(V2 L) => reservaT > m.t - 0.6f && ((reserva - L).Len < 90 || reservaPx != null && (reservaPx.Value - L).Len < 90);
        void Reservar(V2 L, V2? pixel = null) { reserva = L; reservaPx = pixel; reservaT = m.t; }
        V2 reserva; V2? reservaPx; float reservaT = -9;

        readonly float[] expH = new float[Aparece.BINS];
        /// <summary>De quantas direções o inimigo costuma aparecer pra quem fica em q (1 = um ângulo só). Sem dados, conta as quinas.</summary>
        float Exposicao(V2 q, float t)
        {
            float tot = Aparece.Monta(me.team, q, t, m.bombPlanted, expH);
            if (tot < 10) return 1 + 0.35f * Pixels.Em(q).Length;
            float soma = 0; for (int b = 0; b < Aparece.BINS; b++) soma += expH[b] * expH[b];
            return soma > 0 ? 0.28f / soma : 9;   // 0,28 = a soma de um pico só, depois da suavização (3 bins vizinhos)
        }
        /// <summary>
        /// Quem segura escolhe o ponto, perto da posição, que ainda vê o ângulo principal mas se expõe a menos direções: o pro
        /// luta um duelo de cada vez, com a parede tampando o resto. Só troca se ganhar de verdade.
        /// </summary>
        Hold HoldPoucoExposto(Hold h)
        {
            if (h.look.Length == 0) return h;
            V2 alvo = h.look[0], cen = NavGrid.Snap(h.pos); float zAlvo = NavGrid.ZAt(alvo), z0 = NavGrid.ZAt(cen);
            float t = m.t + NavGrid.PathLen(me.pos, h.pos) / MathF.Max(1, me.MaxSpeed);
            float atual = Exposicao(cen, t), melhor = atual - 0.5f; V2? res = null;
            for (int k = 0; k < 28; k++)
            {
                V2 q = NavGrid.Snap(h.pos + V2.Ang(k * 2.4f) * (14 + 62 * (k + 1) / 28f));   // espiral: cobre o entorno sem sorteio
                if (!NavGrid.Movable(q) || MathF.Abs(NavGrid.ZAt(q) - z0) > 6 || !MapData.LosWalk(q, NavGrid.ZAt(q), alvo, zAlvo, 18f)) continue;
                float e = Exposicao(q, t) + (q - h.pos).Len / 200f;
                if (e < melhor) { melhor = e; res = q; }
            }
            if (res == null) return h;
            m.Aud("escolha do ponto", "achou ponto menos exposto");
            return new Hold(h.name, res.Value, h.look);
        }

        /// <summary>
        /// Matei daqui em rounds passados: agora podem estar esperando. Cada um decide pelo jeito dele: o agressivo às vezes repete
        /// (o outro pode achar que ele saiu), o resto muda pra perto, vendo o mesmo ângulo. Quanto mais usado, mais fácil sair.
        /// </summary>
        Hold HoldNaoQueimado(Hold h)
        {
            var L = m.leitura[(int)me.team]; if (L == null || !L.Adapta || h.look.Length == 0 || EXP.Contains("semQueimado")) return h;
            float q = L.Queimado(h.pos, me.team); if (q < 0.7f) return h;
            if (rng.Rand() >= MathX.Clamp(0.3f + 0.35f * q - 0.3f * P.Agr(me.skill.aggression), 0, 0.9f)) { m.Ev("repetiu ponto queimado/round"); return h; }
            V2 alvo = h.look[0]; float zA = NavGrid.ZAt(alvo), z0 = NavGrid.ZAt(NavGrid.Snap(h.pos));
            for (int k = 0; k < 18; k++)
            {
                V2 c = NavGrid.Snap(h.pos + V2.Ang(rng.Uniform(0, 6.2832f)) * rng.Uniform(65, 125));
                if (!NavGrid.Movable(c) || MathF.Abs(NavGrid.ZAt(c) - z0) > 6 || L.Queimado(c, me.team) > 0.3f || !MapData.LosWalk(c, NavGrid.ZAt(c), alvo, zA, 18f)) continue;
                m.Ev("trocou de ponto queimado/round"); Think($"já matei de {h.name}: devem estar esperando, fico em outro ponto");
                return new Hold(h.name, c, h.look);
            }
            return h;
        }

        /// <summary>Mesmo ângulo de um ponto um pouco diferente. Desligado: medido, o CT perde 2 pontos de TR vence (exp holdVaria).</summary>
        Hold HoldVariado(Hold h)
        {
            V2 alvo = h.look.Length > 0 ? h.look[0] : h.pos;
            for (int k = 0; k < 16; k++)
            {
                V2 q = NavGrid.Snap(h.pos + V2.Ang(rng.Uniform(0, 6.2832f)) * rng.Uniform(25, 65));
                if (!NavGrid.Movable(q) || MathF.Abs(NavGrid.ZAt(q) - NavGrid.ZAt(NavGrid.Snap(h.pos))) > 6) continue;
                if (h.look.Length > 0 && !MapData.LosWalk(q, NavGrid.ZAt(q), alvo, NavGrid.ZAt(alvo), 18f)) continue;
                return new Hold(h.name, q, h.look);
            }
            return h;
        }

        public void SetOrder(Order o)
        {
            if (me.guardando && o.name != "guardando a arma") return;   // decidiu guardar: não entra mais em jogada
            m.Aud("ordem trocada", m.t - ordemDesde < 3 ? "menos de 3 s depois da anterior" : "normal"); ordemDesde = m.t;
            if (!me.IsT && o.hold != null && o.hold != CT_Janelao && EXP.Contains("holdVaria")) o.hold = HoldVariado(o.hold);
            if (o.hold != null && o.hold != CT_Janelao && !o.hold.name.StartsWith("segurando") && !o.hold.name.StartsWith("C4") && !EXP.Contains("semUmAngulo")) o.hold = HoldPoucoExposto(o.hold);
            if (o.hold != null && o.hold != CT_Janelao && !o.hold.name.StartsWith("segurando") && !o.hold.name.StartsWith("C4")) o.hold = HoldNaoQueimado(o.hold);
            order = o; pathAt = -9; holdLook = 0;
            if (o.malote) passaram.Clear();
            baseP = null; micro = Micro.Nada; trilha.Clear(); microProx = m.t + rng.Uniform(0.3f, 1.2f);
            Think($"{o.name}{(o.why != null ? " — " + o.why : "")}");
        }

        // ================================================================ percepção
        public bool CanSee(Agent e)
        {
            if (!e.alive || me.Blind(m.t)) return false;
            V2 d = e.pos - me.pos; float dist = d.Len;
            if (dist > 1400) return false;
            // tela ainda branca: flash forte só deixa ver de perto; flash fraca quase não atrapalha (vê ~890 com 0,1)
            if (me.MeioCego(m.t) && dist > 80 + 900 * (1 - me.flashI)) return false;
            float da = MathF.Abs(MathX.AngDiff(d.Atan2, me.aim));
            if (da > 55f * MathF.PI / 180 && dist > 30) return false;
            if (!MapData.LosWalk(me.pos, me.z + me.Eye - 18f, e.pos, e.z + e.Eye - 18f, 18f)) return false;
            return !m.SmokeBlocks(me.pos, me.z + me.Eye, e.pos, e.z + e.Eye);
        }

        float ReactionTime(Agent e)
        {
            float da = MathF.Abs(MathX.AngDiff((e.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI;
            // achar e corrigir a mira custa caro: pré-mirado (<6°) quase nada; 30° ~0,13 s a mais (antes 0,03 s: segurar
            // ângulo pré-mirado não dava vantagem nenhuma e o CT perdia duelo segurando posição)
            float r = me.skill.reaction * rng.Uniform(0.85f, 1.25f) + 0.08f * MathF.Pow(da / 30f, 0.7f) + da / (me.skill.flick * 0.66f);
            if (da < 6) r -= 0.04f;                         // mira já no lugar (crosshair placement)
            // parado e com a mira no lugar o pro atira quase na hora (real: 63% atira até 0,1 s depois de ver quando a mira está a menos de 2°); quem vem andando ainda tem que parar
            if (da < 6 && me.Speed < me.MaxSpeed * 0.2f && !EXP.Contains("semPreTiro")) r *= EXP.Contains("pre75") ? 0.75f : 0.55f;
            if (e.Speed > e.MaxSpeed * 0.6f) r += 0.03f;     // alvo passando correndo é mais difícil
            // vindo correndo de rifle tem que frear antes do tiro: quem já está parado e mirando sai na frente
            if (!me.ArmaLeve && !EXP.Contains("semAssentar")) r += (EXP.Contains("assenta17") ? 0.17f : 0.25f) * MathF.Min(1, me.Speed / me.MaxSpeed);
            if (EXP.Contains("semPanico")) { if (m.ClutchFor(me.team)) r *= 1.15f - 0.15f * me.skill.composure; }
            else r *= 1 + 0.25f * me.panico;   // pânico: demora a achar e decidir
            if (me.MeioCego(m.t)) r += 0.3f * me.flashI * me.flashI;   // ainda vendo branco (fraca: quase nada)
            return MathF.Max(EXP.Contains("semPreTiro") ? 0.1f : 0.05f, r);
        }

        // ================================================================ passo
        public void Tick(float dt)
        {
            wantVel = V2.Zero; hasWantAim = false; me.crouch = false;
            float t = m.t; faseAnt = fase; fase = "ordem";
            Panico(t, dt);
            // ---- vê
            long pc = P0();
            Agent best = null; float bestScore = float.MaxValue;
            foreach (var e in m.agents)
            {
                if (e.team == me.team || !CanSee(e)) continue;
                m.Spot(me, e);
                float s = (e.pos - me.pos).Len + MathF.Abs(MathX.AngDiff((e.pos - me.pos).Atan2, me.aim)) * 200;
                if (s < bestScore) { bestScore = s; best = e; }
            }
            P1("ver inimigos", pc);
            if (best != null)
            {
                if (target != best)
                {
                    if (target == null && t - lastSeenT > 1.5f)
                    {
                        string k = micro != Micro.Nada ? micro.ToString() : faseAnt;
                        if (k == "andando")
                        {   // andando: de onde vinha a mira e se ele apareceu na frente do caminho, do lado ou atrás
                            V2 fw = path != null && wp < path.Count ? path[wp].p - me.pos : V2.Ang(me.aim);
                            float a = fw.Len > 0.1f ? MathF.Abs(MathX.AngDiff((best.pos - me.pos).Atan2, fw.Atan2)) * 180 / MathF.PI : 0;
                            k += "/" + miraFonte + (a < 45 ? "/frente" : a < 120 ? "/lado" : "/atrás");
                        }
                        float flick0 = MathF.Abs(MathX.AngDiff((best.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI;
                        m.contatos.Add((flick0, k)); tiroFlick = flick0; tiroAte = t + 2;
                        m.aparecidos.Add((me.team, me.pos, t, m.bombPlanted, (best.pos - me.pos).Atan2));
                        if (Match.Auditar)
                        {
                            float aq = 999, dirE = (best.pos - me.pos).Atan2; bool pontoCego = false;
                            foreach (var q in Pixels.Em(me.pos)) aq = MathF.Min(aq, MathF.Abs(MathX.AngDiff((q.ponto - me.pos).Atan2, dirE)) * 180 / MathF.PI);
                            m.Aud("onde o inimigo apareceu", flick0 < 6 ? "na mira" : aq < 8 ? "em OUTRA quina que eu não olhava" : (best.pos - me.pos).Len < 120 ? "colado, sem quina" : "em lugar aberto, sem quina");
                            if (flick0 >= 6 && aq < 8) m.Aud("olhando outra quina porque", $"{fonteMira} / {(target == null ? faseAnt : "")}");
                            float atrasoM = MathF.Abs(MathX.AngDiff(wantAim, me.aim)) * 180 / MathF.PI, querM = MathF.Abs(MathX.AngDiff(dirE, wantAim)) * 180 / MathF.PI;
                            if (flick0 >= 6) m.Aud("mira fora: por quê", querM < 6 ? "queria o ponto certo, a mão atrasou" : atrasoM > 15 ? "virando pra outro lugar" : "parado no lugar errado");
                            { float ex = Exposicao(me.pos, t); m.Aud(Segurando ? "ângulos expostos segurando, no contato" : "ângulos expostos andando, no contato", ex < 2 ? "até 2" : ex < 3.5f ? "2-3,5" : ex < 6 ? "3,5-6" : "6+"); }
                            m.Aud("mira no contato por time", (me.IsT ? "TR " : "CT ") + (flick0 < 2 ? "<2" : flick0 <= 8 ? "2-8" : ">8")); m.Aud("contato por distância", (best.pos - me.pos).Len < 150 ? "perto (<150)" : (best.pos - me.pos).Len < 350 ? "médio" : "longe");
                        }
                    }
                    lastMicroOuFase = micro != Micro.Nada ? micro.ToString() : faseAnt;
                    target = best; reactAt = t + ReactionTime(best); alvoDesde = t;
                    // jeito de duelar desta vez: ADAD (a maioria dos pros; AWP de luneta não) e agachar no spray (às vezes)
                    adadDuelo = me.gunId != "awp" && rng.Rand() < 0.45f + 0.45f * P.Agr(me.skill.aggression) + 0.2f * P.strafeRajada;
                    agachaDuelo = rng.Rand() < 0.3f;
                    // a mira raramente está na altura exata da cabeça quando ele aparece (mais baixa, em geral); quem já
                    // estava mirando ali (pré-mirado) erra pouco, quem precisa dar flick erra mais; AWP de luneta erra metade
                    float flick = MathX.Clamp(MathF.Abs(MathX.AngDiff((best.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI / 25f, EXP.Contains("hsVelho") ? 0.2f : 0.5f, 1f);
                    flickAlvo = MathF.Abs(MathX.AngDiff((best.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI;
                    errV0 = rng.Normal(-0.6f * flick, me.skill.pitchErr * (1.4f - me.skill.crosshair) * flick * (me.gunId == "awp" ? 0.5f : 1f));
                    Think($"vi {best.name} {(Region(best.pos) != "?" ? "na " + Region(best.pos) : "")}{(best.Speed < 5 ? " (parado)" : "")}");
                }
                lastSeenPos = best.pos; lastSeenT = t; lastSeen = best;
            }
            else { target = null; me.aimErrV = 0; }

            // ---- ação em andamento (plantar, desarmar, granada)
            if (action != null)
            {
                bool threat = best != null;
                if (threat && (action == "plantar" || (action == "desarmar" && m.BombLeft > Match.DefuseTime(me) + 1.5f && !m.SmokeNear(me.pos, 30))))   // dentro da smoke segue desarmando
                { Think($"parei de {action}: inimigo à vista"); action = null; }
                else if (action == "desarmar" && m.FireAt(me.pos)) { Think("molotov no bomb — larguei o defuse"); action = null; m.defuserActive = false; }
                else if (t >= actionEnd) FinishAction();
                else { fase = "ação"; Face(action == "granada" ? actionNade.to : me.pos + V2.Ang(me.aim) * 10, 0); return; }
            }

            // ---- pisou no fogo: sai (pro lado seguro, de preferência pra trás) e segura dali — molotov tira do lugar,
            // não mata quem reage. Plantando/desarmando decide na ação (acima)
            if (action == null && m.FireAt(me.pos) && !EXP.Contains("semSairFogo"))
            {
                // a saída escolhida vale até pegar fogo nela (recalcular toda hora fazia ele ir e voltar dentro do fogo)
                if (t > fugaFogoCalc + 3f || m.FireAt(fugaFogo) || (fugaFogo - me.pos).Len < 3) { fugaFogoCalc = t; fugaFogo = SaidaDoFogo(); }
                fase = "saindo do fogo"; Think("molotov — saindo do fogo");
                MoveTo(fugaFogo, true);
                if (target != null) Face(target.pos, 3);
                // segura de onde saiu (a posição antiga está queimando)
                ancora = fugaFogo; baseP = fugaFogo; baseDesde = t; planAt = -9; micro = Micro.Nada;
                return;
            }
            // ---- cego pela flash: não dá pra seguir o caminho como se visse
            if (me.Blind(t) && !EXP.Contains("semCego")) { Cego(t); return; }
            // ---- combate
            // guardando a arma: duelo de longe não vale a arma (de perto não tem escolha)
            if (target != null && me.guardando && (target.pos - me.pos).Len > 260 && t - entregouEm > 1.5f)
            { fase = "guardando"; Think($"guardando a arma — não vou no duelo com o {target.name}"); MoveTo(order.dest, true); return; }
            if (target != null && order.malote && (order.dest - me.pos).Len <= 14 && !EXP.Contains("semSegurarTiro") && SegurarTiroMalote(t)) return;
            if (target != null) { fase = "combate"; Fight(target, t); return; }
            if (t < levouAte && action == null)
            {
                fase = "levei tiro";
                Face(levouDe, 3);   // vira pro tiro; parado (counter-strafe) pra poder responder quando achar
                Think("levei tiro — virando pra onde veio");
                return;
            }
            if (lastSeen != null && t - lastSeenT < 1.6f && lastSeen.alive)
            {
                fase = "segurando onde o inimigo sumiu";
                // perdeu de vista: segura o ângulo onde ele sumiu um pouco antes de seguir
                Face(Pixel(lastSeenPos), 3);   // segura a quina por onde ele sumiu (não a parede na frente dele)
                // re-peek do entry: não no mesmo lugar onde devem ter vários (ou eu fraco) — isso é insistir no que já deu errado
                bool repeekRuim = !EXP.Contains("zonaVelha") && (me.hp < 40 || m.intel[(int)me.team].Esperados(lastSeenPos, 250) >= 1.6f);
                if (me.role == Role.Entry && t - lastSeenT > 0.7f && !repeekRuim) { Think($"re-peek no {lastSeen.name}"); MoveTo(lastSeenPos, false); }
                else Think($"segurando o ângulo onde {lastSeen.name} sumiu");
                return;
            }
            // HE na smoke (minha ou do colega): mira no furo enquanto está aberto — quem estava atrás fica exposto
            if (t < furoAte && action == null && !order.defuse && !(me.hasBomb && order.plant))
            {
                fase = "olhando o furo da smoke"; Face(furoPos, 3);
                if (t > furoDesde) Think("smoke aberta pela HE — procurando quem tá atrás");
                return;
            }
            burst = 0; me.spray = 0;
            FacaOuArma(t);
            LargarC4(t);
            // ---- recarga com calma
            if (me.gun.inMag < me.gun.s.mag * 0.35f && !me.gun.Reloading && m.intel[(int)me.team].HeatIn(me.pos, 220) < 1.5f && me.gun.StartReload())
                Think("recarregando (ninguém perto)");
            // ---- troca: aliado morreu perto, vai em cima de quem matou
            if (t < tradeUntil) { fase = "indo trocar"; MoveTo(tradePos, true); ClearAngles(true); Face(Pixel(tradePos), 3); Think("indo trocar"); return; }   // pré-mirado na quina dele
            if (RoundGanho && !order.defuse) { DeBoa(); return; }
            if (SairDoBomb(t)) return;
            if (PegarArma(t)) return;
            if (PegarGranada(t)) return;
            if (Utilitaria(t)) return;
            if (UtilProativa(t)) return;
            if (GranadaNoBarulho(t)) return;
            if (AbrirSmoke(t)) return;
            if (AtenderBang(t)) return;
            if (AjudarNoDuelo(t)) return;
            DoOrder(t);
        }

        /// <summary>
        /// Pânico: o quanto a situação aperta (em menos, aliado caindo do lado, pouca vida, sozinho, C4 acabando) vezes
        /// o quanto ELE sente (sangue-frio). Sobe rápido, desce devagar. Mão treme, demora a reagir, esquece o shift e
        /// dá peek afobado; quem é frio no clutch faz o contrário — isola duelo e usa o tempo.
        /// </summary>
        void Panico(float t, float dt)
        {
            if (EXP.Contains("semPanico")) { me.panico = 0; return; }
            int nos = m.Alive(me.team), eles = m.Alive(me.team == Team.T ? Team.CT : Team.T);
            float sit = 0;
            // estar em menos/sozinho pesa pouco (clutch é rotina pra quem joga); o que aperta são os sustos
            if (eles > nos) sit += 0.07f * (eles - nos);
            if (nos == 1 && eles >= 2) sit += 0.05f * eles;
            foreach (var x in m.agents) if (x != me && !x.alive && x.team == me.team && t - x.diedAt < 4 && (x.pos - me.pos).Len < 450) sit += 0.3f;
            if (me.hp < 40) sit += 0.2f;
            if (m.bombPlanted && !me.IsT && m.BombLeft < 10) sit += 0.2f;
            if (t < levouAte) sit += 0.15f;
            float alvo = MathX.Clamp(sit * (1.25f - me.skill.composure), 0, 1);
            me.panico += (alvo - me.panico) * MathF.Min(1, dt * (alvo > me.panico ? 2.5f : 0.35f));
        }

        float cegoDe = -9, cegoTiro; int cegoPlano, cegoTiros; V2 cegoAlvo;

        /// <summary>
        /// Tela branca. Decide uma vez por flash, pelo jeito dele e pelo que sabia antes de cegar: atirar no vento pra
        /// onde o perigo estava (quem ele viu agora há pouco, o call do time ou de onde veio a flash — quem flasha vem
        /// atrás), agachar no canto segurando aquela direção, ou recuar de costas até clarear.
        /// </summary>
        void Cego(float t)
        {
            fase = "cego";
            if (me.blindUntil != cegoDe)
            {
                cegoDe = me.blindUntil; cegoTiros = 0;
                V2? ameaca = null;
                if (lastSeen != null && lastSeen.alive && t - lastSeenT < 3f) ameaca = lastSeenPos;
                else ameaca = Chamada(600);
                cegoAlvo = ameaca ?? me.flashDe;
                float agr = P.Agr(me.skill.aggression);
                float pTiro = ameaca != null ? 0.2f + 0.4f * agr + 0.25f * (1 - me.skill.composure) : 0.08f + 0.12f * agr;
                float pRecua = order.plant || order.defuse || me.gunId == "awp" && order.hold != null ? 0.1f : 0.25f + 0.3f * (1 - agr);
                float u = rng.Rand();
                cegoPlano = me.gunId == "awp" ? (u < 0.5f ? 1 : 2) : u < pTiro ? 0 : u < pTiro + pRecua ? 2 : 1;
                if (cegoPlano == 0) me.tiroCego++; else if (cegoPlano == 2) me.recuoCego++; else me.agachaCego++;
                Think(cegoPlano == 0 ? $"cego — atirando no vento pra {Region(cegoAlvo)}" : cegoPlano == 2 ? "cego — recuando até clarear" : "cego — agachado no canto esperando clarear");
            }
            Face(cegoAlvo, 3);
            V2 d = cegoAlvo - me.pos; float dl = MathF.Max(1, d.Len);
            if (cegoPlano == 0)
            {
                bool aliado = m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < dl &&
                                                MathF.Abs(MathX.AngDiff((x.pos - me.pos).Atan2, d.Atan2)) < 0.25f);
                if (!aliado && t > cegoTiro && cegoTiros < 8 && me.gun.inMag > 0)
                { m.Shoot(me, cegoAlvo, me.z + 21.5f); cegoTiros++; cegoTiro = t + rng.Uniform(0.12f, 0.3f); }
            }
            else if (cegoPlano == 2)
            {
                V2 tras = -d / dl;
                if (NavGrid.Movable(me.pos + tras * 14)) { wantVel = tras * me.MaxSpeed * 0.52f; me.move = Move.Andando; }
                else me.crouch = true;
            }
            else me.crouch = true;
        }

        void Fight(Agent e, float t)
        {
            float dist = (e.pos - me.pos).Len;
            float aimH = Combat.AimHeight(me, dist, e.crouch);
            V2 aimPt = e.pos + e.vel * rng.Normal(0.02f, 0.07f);   // rastrear alvo andando: às vezes adianta, às vezes atrasa
            Face(aimPt, aimH - me.Eye + e.z - me.z);
            if (me.faca) { Sacar(t, $"pego de faca pelo {e.name}!"); reactAt = MathF.Max(reactAt, me.sacaAte); }
            me.aimErrV = errV0 * MathF.Exp(-MathF.Max(0, t - reactAt) / 0.3f);
            if (t < reactAt) { wantVel = me.vel * 0.6f; return; }   // ainda reagindo
            if (me.gunId == "awp" && !me.scoped) { me.scoped = true; reactAt = MathF.Max(reactAt, t + 0.14f); return; }   // entrar na luneta leva um tempo
            // ---- movimento no duelo, como no CS: ADAD entre os tiros (não ficar parado igual bot, que é alvo fácil) e
            // counter-strafe na hora do tiro (andando a bala espalha: AK precisa estar quase parado, SMG/pistola aguenta mais)
            bool deagle = me.gunId == "deagle";
            bool leve = me.gunId == "mp9" || me.gunId == "mac10" || (WeaponStats.Pistola(me.gunId) && !deagle);   // Deagle andando não acerta nada
            bool runGun = leve && dist < 110;                                   // de sub/pistola perto atira andando
            float limiar = me.gunId == "awp" ? 0.12f : leve ? 0.55f : 0.15f;    // fração da velocidade que ainda dá tiro preciso
            V2 dirE = (e.pos - me.pos) / MathF.Max(1, dist), lado = new V2(-dirE.y, dirE.x);
            if (t > adadTroca) { adadLado = -adadLado; adadTroca = t + rng.Uniform(0.2f, 0.45f); }
            if (m.fires.Count > 0 && m.FireAt(me.pos + lado * adadLado * 24)) adadLado = -adadLado;   // não dança pra dentro do fogo
            V2 adadVel = NavGrid.Movable(me.pos + lado * adadLado * 8) && !(m.fires.Count > 0 && m.FireAt(me.pos + lado * adadLado * 24)) ? lado * adadLado * me.MaxSpeed : V2.Zero;
            wantVel = V2.Zero;
            if (me.gun.Reloading) { Think("recarregando no meio da troca — me escondendo"); Retreat(e); return; }
            if (me.gun.inMag == 0) { me.gun.StartReload(); return; }
            int maxBurst = me.gunId == "awp" ? 1 : deagle ? (dist > 120 ? 1 : 3) : dist > 420 ? 2 : dist > 220 ? 5 : 30;   // longe toca, médio rajada, perto spray; Deagle: um tiro e espera a mira voltar
            if (burst >= maxBurst)
            {
                if (t - me.lastShotT > (deagle ? 0.5f : 0.32f)) { burst = 0; me.spray = 0; }
                else { if (adadDuelo && t - me.lastShotT < 0.22f) wantVel = adadVel; return; }   // entre as rajadas: ADAD, e para pra próxima
            }
            // pistola: tap no ritmo — espera a mira voltar até o cone caber no alvo (cabeça de longe, corpo de perto);
            // colado (<90) é spam mesmo. Quem tem mais calma (composure) espera mais certinho
            if (WeaponStats.Pistola(me.gunId) && dist > 90 && burst > 0 && !EXP.Contains("semTap"))
            {
                float cone = me.gun.penalidade * Combat.ESCALA_ARMA;
                float cabe = MathF.Atan2(dist < 200 ? 4.6f : 2.2f, dist) * (1.6f - 0.6f * me.skill.composure);
                if (cone > cabe) { if (adadDuelo) wantVel = adadVel; return; }
            }
            float err = MathF.Abs(MathX.AngDiff((aimPt - me.pos).Atan2, me.aim));
            float tol = MathF.Atan2(4f, MathF.Max(dist, 1));
            if (err > tol * 1.6f) { if (adadDuelo) wantVel = adadVel; return; }   // ainda acertando a mira: se mexe enquanto isso
            if (!me.gun.CanFire(t)) { if (adadDuelo && burst == 0 || runGun) wantVel = adadVel; return; }
            if (!runGun && me.Speed > me.MaxSpeed * limiar) return;               // counter-strafe: para antes de atirar
            if (runGun && adadDuelo) wantVel = adadVel * 0.8f;
            if (agachaDuelo && me.spray >= 2 && dist > 100 && dist < 420) me.crouch = true;   // spray agachado (mais preciso, cabeça mais baixa)
            m.Shoot(me, aimPt, e.z + aimH);
            if (t < tiroAte) { m.contatosTiro.Add(tiroFlick); tiroAte = -9; }
            burst++;
            Think($"atirando no {e.name} ({(aimH > 18 ? "cabeça" : "peito")}, {(burst > 1 ? "spray" : "1º tiro")})");
        }

        float tiroFlick, tiroAte = -9;
        void Retreat(Agent from)
        {
            V2 away = (me.pos - from.pos); float l = away.Len; if (l < 1) return;
            V2 dst = me.pos + away / l * 60;
            if (NavGrid.Movable(dst)) MoveTo(dst, true);
        }

        /// <summary>Onde o time sabe que esse inimigo está (visto ou cantado há pouco); null = ninguém sabe.</summary>
        V2? ContatoDe(Agent e) => m.intel[(int)me.team].contacts.TryGetValue(e.id, out var c) && m.t - c.t < 4 ? c.pos : (V2?)null;

        public void OnTeammateDied(Agent dead, Agent killer)
        {
            if (!me.alive) return;
            float d = (dead.pos - me.pos).Len;
            V2 matador = killer != null ? ContatoDe(killer) ?? dead.pos : dead.pos;   // ninguém viu quem matou: só sei onde o colega caiu
            if (killer != null && d < 650) { focoMorteDe = killer; focoMortePos = matador; focoMorteT = m.t; focoMorteAte = m.t + 6; }
            if (d > 420 || killer == null || me.guardando || action != null) { if (d < 260) Think($"{dead.name} caiu perto — segurando"); return; }
            // troca (refrag): quem está perto vai em cima de quem matou — o suporte/entry quase sempre, quem segura
            // posição às vezes (depende do jeito dele e de quanto a posição importa); o AWP raramente sai do ângulo
            bool trader = me.role == Role.Suporte || me.role == Role.Entry || me.role == Role.Igl || me.role == Role.Rotacao;
            float p = (trader ? 1f : 0.65f) * (order.hold == null ? 1f : 0.8f) * (me.gunId == "awp" ? 0.35f : 1f) * (0.7f + 0.5f * P.Agr(me.skill.aggression)) * (d < 180 ? 1.15f : 0.85f);
            // trocar correndo pra dentro de 2+ inimigos é morrer junto: aí segura o ângulo e espera ele aparecer
            if (m.intel[(int)me.team].Esperados(matador, 300) >= 2f) p *= 0.3f;
            if (Explicar) porquePendente = $"chance de trocar {p:0%}: colega a {d:0} de mim, função {me.role}, devem ter {m.intel[(int)me.team].Esperados(matador, 300):0.0} em volta do matador";
            if (rng.Rand() < p)
            {
                tradeUntil = m.t + 3.4f; tradePos = matador;
                Think($"{dead.name} morreu do {killer.name} — vou trocar");
            }
            else Think($"{dead.name} caiu perto — segurando o ângulo dele");
        }

        // ================================================================ ordem do IGL
        void DoOrder(float t)
        {
            var o = order;
            // indo tomar espaço e a info mudou: devem estar vindo vários ali, ou um aliado acabou de cair ali — não insiste
            if (o.zona && !o.zonaSegura && t < o.until && t > proxZona && !EXP.Contains("zonaVelha"))
            {
                proxZona = t + 0.5f;
                V2 alvoZ = o.hold?.pos ?? o.dest;
                float vemZ = m.intel[(int)me.team].Esperados(alvoZ, 320);
                bool caiu = m.agents.Any(x => x.team == me.team && !x.alive && t - x.diedAt < 8 && (x.pos - alvoZ).Len < 260);
                if (vemZ >= 1.6f || caiu) { o.until = t; m.Ev("desistiu da jogada no caminho/round"); Think(caiu ? "caiu gente ali — não vou insistir nessa jogada" : $"devem ter {vemZ:0.#} ali — não vou insistir nessa jogada"); }
            }
            if (o.then != null && t >= o.until)
            {
                if (o.zona && !EXP.Contains("zonaVelha") && FimDaZona(o, t)) return;
                var nx = o.then; nx.why = "acabou a jogada, voltando";
                bool ocupada = nx.hold != null && HoldOcupado(nx.hold);
                if (o.zona) { m.Ev("jogada de zona acabou/round"); if (ocupada) m.Ev("voltou pra posição que um aliado já segura/round"); }
                // voltar pro mesmo pixel que o colega já segura não existe: pega uma posição livre parecida
                if (ocupada && !EXP.Contains("zonaVelha") && HoldLivrePerto(nx.hold) is Hold livre) { nx = new Order { name = livre.name, hold = livre, why = $"{nx.hold.name} já tem gente — fico com {livre.name}" }; m.Ev("trocou pra posição livre ao voltar/round"); }
                SetOrder(nx); return;
            }
            if (o.malote && Malote(o, t)) return;
            // esperar a vez/a smoke: chega perto antes (até onde ainda não pode ter inimigo) — ninguém espera na base
            bool espera = t < o.notBefore || o.waitSmoke && !m.SmokeNear(o.smokeAt, 40) && t < o.notBefore + 14;   // sem smoke em 14 s: vai assim mesmo
            if (espera)
            {
                V2 ondeVai = o.hold?.pos ?? o.dest;
                if (t > esperaCalc) { esperaCalc = t + 1; esperaPonto = o.hold == null && ondeVai.Len < 1 ? me.pos : PontoDeEspera(ondeVai); }
                if ((esperaPonto - me.pos).Len > 16) { fase = "indo pro ponto de espera"; MoveTo(esperaPonto, true); ClearAngles(true); Think($"chegando perto de {o.hold?.name ?? Region(ondeVai)} pra esperar"); return; }
                Esperando(o.hold?.look);
                thought = o.waitSmoke && t >= o.notBefore ? "esperando a smoke subir" : "esperando a vez";
                return;
            }
            // granada de tomada: vou tomar um lugar e sei a lineup que limpa o ângulo típico do CT de lá — uso se deve ter
            // alguém (o time não gasta duas iguais)
            if (me.IsT && o.hold != null && !m.bombPlanted && t >= proxTomada && !EXP.Contains("semTomada"))
            {
                proxTomada = t + 0.5f;
                foreach (var l in Tomada(o.hold))
                {
                    if (o.nades.Contains(l) || m.utilUsada.Contains(l.name) || !me.nades.Contains(l.kind) || (l.from - me.pos).Len > 260) continue;
                    float calor = m.intel[(int)me.team].HeatAvg(l.to, 70);
                    if (calor < 0.2f || !Rola("tomada " + l.name, 0.4f + 0.6f * calor)) continue;
                    m.utilUsada.Add(l.name); o.nades.Add(l);
                    Think($"vou tomar {o.hold.name}: {l.name} (deve ter alguém lá)");
                    break;
                }
            }
            // granadas na fila
            if (o.nades.Count > 0 && me.alive)
            {
                var n = o.nades[0];
                if (!me.nades.Contains(n.kind)) { o.nades.RemoveAt(0); Think($"sem {n.kind} — {n.name} fica pra outra"); return; }   // não comprou ou já gastou
                if ((n.from - me.pos).Len > 14) { MoveTo(n.from, true); ClearAngles(false); Think($"indo jogar {n.name}"); return; }
                if (!CaiNoLugar(n.kind, n.to, n.name)) { o.nades.RemoveAt(0); return; }
                action = "granada"; actionNade = n; actionEnd = t + 0.9f;
                Think($"jogando {n.name}");
                return;
            }
            // plantar
            if (o.plant && me.hasBomb && !m.bombPlanted)
            {
                V2 ps = m.PlantSpot;
                if ((ps - me.pos).Len > 10) { MoveTo(ps, true); ClearAngles(true); Think($"levando o bomb pro {m.planSite}"); return; }
                action = "plantar"; actionEnd = t + Match.PLANT; m.PlantStart(me);
                Think("plantando"); return;
            }
            // desarmar
            if (o.defuse && m.bombPlanted)
            {
                if ((m.bombPos - me.pos).Len > 8) { MoveTo(m.bombPos, !o.quiet); ClearAngles(true); Think("indo desarmar"); return; }
                if (t < esperaSmoke) { Esperando(null); fase = "esperando a smoke do defuse"; return; }
                if (m.FireAt(m.bombPos)) { Esperando(null); Think("molotov no bomb — esperando apagar"); return; }
                float dt = Match.DefuseTime(me);
                // smoke defuse: escolha do momento, não regra — tem smoke? tem TR vivo perto? dá tempo de esperar subir?
                if (!decidiuSmoke)
                {
                    decidiuSmoke = true;
                    int tr = m.Alive(Team.T); bool perto = false;
                    foreach (var c in m.intel[(int)me.team].contacts.Values) if (t - c.t < 6 && (c.pos - me.pos).Len < 600) perto = true;
                    float folga = m.BombLeft - dt;
                    float p = tr == 0 || folga < 2.5f ? 0 : 0.15f + 0.12f * tr + (perto ? 0.3f : 0) - (folga < 5 ? 0.2f : 0);
                    if (me.nades.Contains("smoke") && rng.Rand() < p)
                    {
                        action = "granada"; actionNade = new Lineup("smoke no bomb (smoke defuse)", "smoke", me.pos, m.bombPos + V2.Ang(rng.Uniform(0, 6.28f)) * 6); actionEnd = t + 0.6f;
                        esperaSmoke = t + 0.6f + 1.8f;
                        Think($"smoke defuse: {tr} TR vivo{(tr > 1 ? "s" : "")}{(perto ? ", tem gente perto" : "")}, {folga:0.0}s de folga"); return;
                    }
                }
                action = "desarmar"; actionEnd = t + dt; m.DefuseStart(me);
                Think($"desarmando ({dt:0}s{(me.hasKit ? " com kit" : " sem kit")}, faltam {m.BombLeft:0.0}s)"); return;
            }
            // seguir o entry (suporte troca)
            if (o.follow != null && o.follow.alive)
            {
                V2 back = o.follow.pos - V2.Ang(o.follow.aim) * o.followDist;
                if (o.follow.Speed < 3 && (o.follow.pos - me.pos).Len < 60) { Esperando(null); fase = "colado no entry"; Think($"o {o.follow.name} parou — espero colado"); }   // ele esperando (molotov, smoke): eu também
                else if ((back - me.pos).Len > 25) { MoveTo(NavGrid.Movable(back) ? back : o.follow.pos, !o.quiet); ClearAngles(true); Think($"atrás do {o.follow.name} pra trocar"); }
                else Esperando(null);
                return;
            }
            V2 dest = o.hold != null ? o.hold.pos : o.dest;
            // chegou = perto E na altura (do lado da caixa, embaixo, não é "em cima da caixa": tem que pular)
            if (baseP == null && ((dest - me.pos).Len > 12 || MathF.Abs(NavGrid.ZAt(NavGrid.Snap(dest)) - me.z) > 6 || me.noAr))   // no meio do pulo não chegou
            {
                // o colega avisou "vou bangar" pra onde eu estou indo: espero estourar e entro junto
                if (o.push && t < me.bangAmigaEm - 0.3f && (me.bangAmigaPos - me.pos).Len < 450 && (me.bangAmigaPos - dest).Len < (me.pos - dest).Len + 60)
                { Esperando(null); Face(me.bangAmigaPos, 3); Think($"o {me.bangAmigaDe.name} vai bangar — espero estourar pra entrar"); return; }
                if (SegurarContraVarios(t)) return;
                MoveTo(dest, o.run && !o.quiet && !Silencio(o));
                ClearAngles(o.push);
                if (SmokeNoCaminho(t)) return;          // smoke inimiga no caminho: atravessa ou espera abrir
                if (o.push && UtilEntrando(t)) return;  // entrando: bang/molotov no canto, ou pede bang
                SmokeCruzar(t);                          // cruzando na mira de alguém: smoke na linha
                return;
            }
            HoldHere(o.hold?.look);
        }

        float proxZona;
        /// <summary>Escondido no malote: o IGL não tira ele de lá pra girar (o objetivo é justamente deixar eles passarem).</summary>
        public bool Malotando => order.malote && !EXP.Contains("maloteIgl");

        bool HoldOcupado(Hold h) => m.agents.Any(x => x != me && x.alive && x.team == me.team && ((x.pos - h.pos).Len < 70 || x.brain.order.hold == h || x.brain.order.hold != null && (x.brain.order.hold.pos - h.pos).Len < 70));

        /// <summary>Uma posição do mesmo lado (perto da que eu ia) que ninguém do time segura nem está indo segurar.</summary>
        Hold HoldLivrePerto(Hold h)
        {
            Hold best = null; float bd = 520;
            foreach (var x in me.IsT ? HOLDS_T : HOLDS_CT)
            {
                if (x == h || HoldOcupado(x)) continue;
                float d = (x.pos - h.pos).Len; if (d < bd) { bd = d; best = x; }
            }
            return best;
        }

        /// <summary>
        /// Acabou o tempo da jogada de espaço. Pro não larga a zona por nada: o espaço dá info e leitura (por onde vêm,
        /// quantos). Pesa: quantos devem estar vindo aqui, se o bomb que eu defendia fica sem ninguém, se a minha
        /// posição de volta já tem um colega (voltar pro mesmo pixel dele não ajuda ninguém), vida, AWP e o jeito dele.
        ///  • segurar a zona (reavalia a cada 2-3 s; com vários vindo, sai);
        ///  • malotar: esconde fora da rota deles, deixa passar e pega de costas;
        ///  • voltar (pra uma posição livre).
        /// Devolve true se ficou (segurando ou malotando); false = volta pelo caminho normal.
        /// </summary>
        bool FimDaZona(Order o, float t)
        {
            var it = m.intel[(int)me.team];
            float agr = P.Agr(me.skill.aggression);
            float vem = it.Esperados(me.pos, 420);
            var volta = o.then;
            bool redundante = volta?.hold != null && HoldOcupado(volta.hold);
            V2 base0 = volta?.hold?.pos ?? volta?.dest ?? me.pos;
            char site = (base0 - Plant('A')).Len < (base0 - Plant('B')).Len ? 'A' : 'B';
            // sem mim o bomb fica com alguém? (aliado segurando ou indo segurar perto dele)
            bool siteVazio = !m.agents.Any(x => x != me && x.alive && x.team == me.team && (((x.brain.order.hold?.pos ?? x.pos) - Plant(site)).Len < 520));
            bool ferido = me.hp < 50, awp = me.gunId == "awp";
            if (m.bombPlanted || RoundGanho) return false;
            if (o.zonaSegura)
            {   // já decidiu ficar: só sai se a coisa mudou (vários vindo, bomb ficou vazio)
                if (vem < 1.6f && !siteVazio && !ferido && t < 75) { o.until = t + rng.Uniform(2f, 3.2f); return true; }
                Think(vem >= 1.6f ? $"devem vir {vem:0.#} aqui — saio da zona" : siteVazio ? $"o {site} ficou sem ninguém — volto" : "saindo da zona");
            }
            float wSeg = o.zonaSegura ? 0 : (0.6f + 0.6f * agr) * (redundante ? 1.6f : 1) * (siteVazio ? 0.3f : 1) * MathX.Clamp(1.6f - vem, 0.1f, 1.6f) * (ferido ? 0.4f : 1) * (awp ? 0.7f : 1);
            float wVol = (0.5f + 0.5f * (1 - agr)) * (siteVazio ? 3 : 1) * (redundante ? 0.25f : 1) * (ferido ? 2 : 1) * (vem >= 2.5f ? 1.5f : 1);
            float wMal = 0; V2 olha = default; V2? esc = null;
            if (!awp && !EXP.Contains("semMalote") && t < 70)
            {
                wMal = (0.3f + 0.8f * agr) * (vem >= 1f ? 1.5f : 0.7f) * (redundante ? 1.4f : 1) * (siteVazio ? 0.35f : 1) * (ferido ? 0.5f : 1);
                if (wMal > 0) { esc = AcharMalote(site, out olha); if (esc == null) wMal = 0; }
            }
            int k = rng.Weighted(new[] { wSeg, wMal, wVol });
            if (Explicar)
            {
                float tot = MathF.Max(0.001f, wSeg + wMal + wVol);
                porquePendente = $"pesei: segurar {wSeg / tot:0%}, malotar {wMal / tot:0%}, voltar {wVol / tot:0%} | devem vir {vem:0.0}, minha posição já tem gente: {(redundante ? "sim" : "não")}, {site} vazio sem mim: {(siteVazio ? "sim" : "não")}, ferido: {(ferido ? "sim" : "não")}{(esc == null && !awp ? ", sem esconderijo bom" : "")}";
            }
            if (k == 0)
            {
                o.zonaSegura = true; o.until = t + rng.Uniform(2f, 3.2f); m.Ev("segurou a zona/round");
                Think($"fico segurando {o.hold?.name ?? Region(me.pos)} — o espaço é nosso{(redundante ? $" ({volta.hold.name} já tem gente)" : "")}");
                return true;
            }
            if (k == 1)
            {
                m.Ev("malotou/round");
                var mo = new Order { name = $"malotando ({Region(esc.Value)})", dest = esc.Value, quiet = true, malote = true, maloteOlha = olha, maloteSite = site,
                                     maloteDesde = t, deixaPassar = 1 + (rng.Rand() < 0.35f + 0.3f * agr ? 1 : 0) + (rng.Rand() < 0.2f * agr ? 1 : 0),
                                     then = redundante && HoldLivrePerto(volta.hold) is Hold lv ? new Order { name = lv.name, hold = lv, why = "volta do malote" } : volta,
                                     why = vem >= 1f ? $"devem vir {vem:0.#} — me escondo e pego de costas" : "fico escondido fora da rota deles pra pegar de costas" };
                SetOrder(mo);
                return true;
            }
            return false;
        }

        static List<Spots.Spot> spotsMalote;

        /// <summary>
        /// Lugar pra malotar perto de mim: fora da rota que eles fazem até o bomb (mas perto dela), escondido de quem vem
        /// (quase nada da rota antes dele enxerga o lugar) e vendo o pedaço da rota depois dele (as costas de quem passou).
        /// <paramref name="olha"/> = onde eles aparecem primeiro (mira pré-posicionada).
        /// </summary>
        V2? AcharMalote(char site, out V2 olha)
        {
            olha = default;
            var it = m.intel[(int)me.team];
            V2 de = (me.IsT ? MapData.SPAWN_CT : MapData.SPAWN_T)[0];
            var rp = NavGrid.Path(de, Plant(site));
            if (rp == null || rp.Count < 2) return null;
            var rota = new List<V2>(); V2 prev = de;
            foreach (var w in rp) { float seg = (w.p - prev).Len; for (float s = 0; s < seg; s += 20) rota.Add(prev + (w.p - prev) * (s / seg)); prev = w.p; }
            rota.Add(prev);
            spotsMalote ??= Spots.All();
            float best = float.NegativeInfinity; V2? res = null;
            foreach (var s in spotsMalote)
            {
                if ((s.p - me.pos).Len > 340 || s.kind == Spots.Kind.EmCima) continue;
                int j0 = -1; float dr = float.MaxValue;
                for (int j = 0; j < rota.Count; j++) { float d = (rota[j] - s.p).Len; if (d < dr) { dr = d; j0 = j; } }
                if (dr < 45 || dr > 260) continue;
                if (it.HeatAvg(s.p, 60) > 0.1f) continue;
                float eye = s.z + 18;
                int up = 0, upN = 0, down = 0, primeiro = -1;
                for (int j = 0; j < rota.Count; j++)
                {
                    float d = (rota[j] - s.p).Len;
                    if (j < j0 && d > 500 || j >= j0 && d > 350) continue;
                    bool ve = MapData.LosWalk(s.p, eye, rota[j], NavGrid.ZAt(rota[j]) + 18, 18f);
                    if (j < j0) { upN++; if (ve) up++; }
                    else if (ve) down++;
                    if (ve && primeiro < 0 && j >= j0 - 2) primeiro = j;
                }
                if (upN > 0 && up > 0.2f * upN || down < 2) continue;
                float pl = NavGrid.PathLen(me.pos, s.p); if (pl <= 0 || pl > 480) continue;
                float sc = 0.3f * MathF.Min(down, 8) - 6f * (upN > 0 ? up / (float)upN : 0) - pl / 250 + (s.kind == Spots.Kind.AtrasCaixa ? 0.5f : 0) - Perigo.Visto(me.team, s.p) + rng.Uniform(0, 0.5f) - 2.5f * (m.leitura[(int)me.team]?.Adapta == true ? m.leitura[(int)me.team].Queimado(s.p, me.team, 90) : 0);
                if (sc > best) { best = sc; res = s.p; olha = rota[primeiro >= 0 ? primeiro : Math.Min(rota.Count - 1, j0)]; }
            }
            return res;
        }

        /// <summary>
        /// Malote: vai quieto até o esconderijo e espera agachado com a mira onde eles aparecem. Quando o time vê/ouve
        /// eles já passados (mais perto do bomb que eu), sai pegando de costas. Demorou demais ou o round apertou: volta.
        /// </summary>
        bool Malote(Order o, float t)
        {
            var it = m.intel[(int)me.team];
            if (m.bombPlanted || t - o.maloteDesde > 38 || RoundGanho) { var nx = o.then ?? new Order { name = "esperar" }; nx.why = "acabou o malote"; SetOrder(nx); return true; }
            float meu = NavGrid.PathLen(me.pos, Plant(o.maloteSite));
            // eles passaram: alguém visto/ouvido agora há pouco já mais perto do bomb do que eu, perto de mim
            foreach (var c in it.contacts.Values)
            {
                if (t - c.t > 3 || !m.agents[c.id].alive || (c.pos - me.pos).Len > 650) continue;
                float dele = NavGrid.PathLen(c.pos, Plant(o.maloteSite));
                if (dele > 0 && meu > 0 && dele < meu - 40)
                {
                    m.Ev("malote saiu pegando de costas/round");
                    SetOrder(new Order { name = $"pegando de costas ({Region(c.pos)})", dest = c.pos, push = true, quiet = true, until = t + 8, then = o.then, why = "eles passaram por mim" });
                    return true;
                }
            }
            if ((o.dest - me.pos).Len > 10) { MoveTo(o.dest, false); ClearAngles(false); fase = "indo malotar"; Think($"indo malotar quieto ({Region(o.dest)})"); return true; }
            fase = "malotando"; me.crouch = true;
            Face(Pixel(o.maloteOlha), 3);
            Think($"malotando — deixa passar e pego de costas");
            return true;
        }

        /// <summary>
        /// Escondido no malote vendo inimigo: se ele não me viu e ainda está passando, segura o tiro (deixa passar 1-2 e
        /// pega o próximo de costas). Atira se ele me viu, está colado ou já virou as costas depois de passarem os que eu
        /// queria deixar.
        /// </summary>
        bool SegurarTiroMalote(float t)
        {
            var e = target; var o = order;
            if ((e.pos - me.pos).Len < 70 || MathF.Abs(MathX.AngDiff((me.pos - e.pos).Atan2, e.aim)) < 0.5f) return false;   // a mira dele vindo pra cá
            V2 deMim = me.pos - e.pos;
            bool costas = MathF.Abs(MathX.AngDiff(e.aim, deMim.Atan2)) > 1.75f;   // > 100°: está de costas pra mim
            if (passaram.Add(e.id)) passouUlt = t;
            if (costas && (passaram.Count >= o.deixaPassar || t - passouUlt > 1.4f)) { m.Ev1("malote: abriu fogo de costas % rounds");m.Ev("malote: tiros de costas/round"); return false; }
            fase = "malotando"; me.crouch = true; Face(e.pos, Combat.AimHeight(me, (e.pos - me.pos).Len, e.crouch) - me.Eye + e.z - me.z);
            reactAt = MathF.Max(reactAt, t + 0.05f);   // ainda não atiro (mas já estou mirando nele)
            Think($"deixando o {e.name} passar ({passaram.Count}/{o.deixaPassar})");
            return true;
        }
        readonly HashSet<int> passaram = new HashSet<int>(); float passouUlt;

        void FinishAction()
        {
            if (action == "granada") { m.Throw(me, actionNade); order.nades.Remove(actionNade); me.nades.Remove(actionNade.kind); Think($"{actionNade.name} jogada"); }   // a ordem pode ter mudado no meio do arremesso
            else if (action == "plantar") { m.Plant(me); Think("bomb plantado"); }
            else if (action == "desarmar") { m.Defuse(me); Think("desarmei"); }
            action = null;
        }

        // ================================================================ segurar ativo
        /// <summary>Pra onde olhar segurando: passo ouvido perto agora, senão os ângulos da posição em rodízio.</summary>
        V2 LookAlvo(V2[] look)
        {
            if (m.t > holdLookUntil)
            {
                holdLookUntil = m.t + rng.Uniform(2.5f, 5f);
                // próximo ângulo que nenhum aliado perto já está olhando (dois na mesma mira = um ângulo aberto)
                for (int k = 0; k < look.Length; k++)
                {
                    holdLook = (holdLook + 1) % look.Length;
                    var dono = CobertoPor(look[holdLook]);
                    if (dono == null) break;
                    if (k == 0) Think($"{dono.name} já olha {Region(look[holdLook])} — fico com outro ângulo");
                }
            }
            var call = Chamada(650) ?? FocoMorte();
            if (call != null && (Ameacado(call.Value) || !Lotado(call.Value))) return Fonte("call/morte", call.Value);
            if (m.t < lookFocoAte && !m.SmokeBlocks(me.pos, me.z + me.Eye, lookFoco, NavGrid.ZAt(lookFoco) + 18)) return Fonte("pixel fora da smoke", lookFoco);   // pixel novo por fora da smoke
            // ângulo smokado: não fica olhando a fumaça — pega outro (se todos estão smokados, segura a saída da smoke)
            for (int k = 0; k < look.Length && m.smokes.Count > 0; k++)
            {
                V2 L0 = look[holdLook % look.Length];
                if (!m.SmokeBlocks(me.pos, me.z + me.Eye, L0, NavGrid.ZAt(L0) + 18)) break;
                if (k == 0) Think($"smoke em {Region(L0)} — seguro outro ângulo");
                holdLook = (holdLook + 1) % look.Length;
            }
            if (m.t - somT < 2f && (somP - me.pos).Len < 400) { m.Aud("passo que virou a mira", "ele ouviu"); return Fonte("passo", somP); }
            var hd = m.intel[(int)me.team].heard;
            if (hd.Count > 0 && m.t - hd[hd.Count - 1].t < 2f && (hd[hd.Count - 1].p - me.pos).Len < 400 && !Lotado(hd[hd.Count - 1].p)) { m.Aud("passo que virou a mira", "colega cantou"); return Fonte("passo cantado", hd[hd.Count - 1].p); }   // ouviu passo (a mira vai na quina: Pixel)
            if (!EXP.Contains("semQuinaProvavel") && QuinaProvavel() is V2 qp) return Fonte("quina provável", qp);
            // não fica mirando onde é impossível ter alguém (base do próprio time, lugar limpo atrás): pula o ângulo sem
            // chance; se nenhum tem, olha de onde o inimigo mais provavelmente vem
            if (!EXP.Contains("semMiraUtil"))
            {
                // cada ângulo vale (pode ter alguém agora) x (costuma ter alguém vindo por ali nesse momento do round):
                // fica no que vale mais; o de valor quase zero não leva olhada (a não ser que todos valham nada)
                float Vale(V2 L) => Ameaca(L);
                V2 L1 = look[holdLook % look.Length]; float v1 = Vale(L1), vmax = v1; int imax = holdLook % look.Length;
                for (int k = 0; k < look.Length; k++) { float v = Vale(look[k]); if (v > vmax && CobertoPor(look[k]) == null) { vmax = v; imax = k; } }
                if (v1 < 0.35f * vmax) { holdLook = imax; return Fonte("ângulo da posição", look[imax]); }
                if (vmax < 0.01f) { var vem = Borda() ?? DeOndeVem(); if (vem != null) return Fonte("borda/calor", vem.Value); }
            }
            for (int k = 0; k < look.Length && (Lotado(look[holdLook % look.Length]) || Lotado(Pixel(look[holdLook % look.Length]))); k++) holdLook = (holdLook + 1) % look.Length;
            return Fonte("ângulo da posição", look[holdLook % look.Length]);
        }

        /// <summary>
        /// Quanto vale olhar pra L agora: chance de ter inimigo ali (calor do que a gente sabe) x quanto o time deles
        /// costuma passar por ali nesse momento do round (Rotas, aprendido) — possível E provável.
        /// </summary>
        float Ameaca(V2 L)
        {
            float calor = EXP.Contains("calorVelho") ? m.intel[(int)me.team].HeatAvg(L, 90) : MathF.Min(1, m.intel[(int)me.team].Esperados(L, 110));   // onde provavelmente tem alguém, não só onde poderia ter
            float costuma = Rotas.Perto(me.IsT ? Team.CT : Team.T, L, m.t, 110) + Rotas.Perto(me.IsT ? Team.CT : Team.T, L, m.t + 5, 110);
            return calor * (0.15f + 3f * costuma);
        }

        float deOndeAt = -9; V2? deOnde;
        /// <summary>O ponto à vista (até 500) com mais chance de ter inimigo — pra onde olhar quando os ângulos da posição
        /// estão todos "limpos" (ninguém pode estar lá).</summary>
        V2? DeOndeVem()
        {
            if (m.t - deOndeAt < 1) return deOnde;
            deOndeAt = m.t; deOnde = null; float bv = 0.02f; var it = m.intel[(int)me.team];
            for (int k = 0; k < 24; k++)
                foreach (float r in new[] { 120f, 250f, 420f })
                {
                    V2 q = me.pos + V2.Ang(k * 0.2618f) * r;
                    if (!NavGrid.Movable(q)) continue;
                    float h = Ameaca(q);
                    if (h > bv && MapData.LosWalk(me.pos, me.z, q, NavGrid.ZAt(q), 18f)) { bv = h; deOnde = q; }
                }
            return deOnde;
        }

        /// <summary>
        /// Bomb pra explodir e eu no raio: sai correndo pra longe (TR e CT). Fica quem está num defuse que dá tempo.
        /// Cada um decide a hora pela distância (quem está colado sai antes).
        /// </summary>
        bool SairDoBomb(float t)
        {
            if (!m.bombPlanted || action == "desarmar" || order.defuse && Match.DefuseTime(me) + (m.bombPos - me.pos).Len / 55f < m.BombLeft) return false;
            if (order.defuse) SetOrder(new Order { name = "sem tempo de desarmar", dest = me.pos, why = $"faltam {m.BombLeft:0.0}s e o defuse não dá mais: vou salvar a vida" });   // senão sai do raio e volta pro bomb
            float d = (m.bombPos - me.pos).Len;
            if (Match.DanoBomb(d, me.armor > 0) < me.hp * 0.8f) return false;
            // tempo pra sair do raio: o caminho não é reto (x1,6) + margem de cada um (uns saem cedo, outros arriscam — às vezes demais)
            float corre = (Match.BOMB_SIGMA * 2.6f - d) * 1.6f / me.MaxSpeed + margemFuga;
            if (m.BombLeft > corre) return false;
            fase = "saindo do bomb";
            if (fugaAte < t)
            {
                // pra longe do bomb, num ponto andável (tenta algumas direções, prefere longe do inimigo esperado)
                // só pontos alcançáveis (campo de distância a partir do bomb): longe do bomb pelo caminho, perto de mim, sem inimigo
                float best = float.NegativeInfinity; var it = m.intel[(int)me.team];
                fuga = me.pos;
                for (int k = 0; k < 16; k++)
                    foreach (float rr in new[] { 2.6f, 3.2f })
                    {
                        V2 q = NavGrid.Snap(m.bombPos + V2.Ang(k * MathF.PI / 8) * (Match.BOMB_SIGMA * rr));
                        float pl = NavGrid.PathLen(q, m.bombPos);
                        if (float.IsInfinity(pl) || (q - m.bombPos).Len < Match.BOMB_SIGMA * 2.3f) continue;
                        float s = -0.6f * pl - 0.4f * (q - me.pos).Len - 200 * it.HeatAt(q);
                        if (s > best) { best = s; fuga = q; }
                    }
                fugaAte = t + 3;
            }
            MoveTo(fuga, true); ClearAngles(false);
            if (!fugindo) { fugindo = true; Think($"saindo do raio do bomb ({m.BombLeft:0.0}s pra explodir)"); }
            return true;
        }
        V2 fuga; float fugaAte = -9; bool fugindo; float margemFuga;

        /// <summary>Round decidido: nenhum TR vivo e o bomb plantado — um desarma, o resto não precisa fazer nada.</summary>
        /// <summary>Só está ganho se alguém do time ainda consegue desarmar a tempo; senão o bomb explode e quem está no raio morre.</summary>
        bool RoundGanho => me.team == Team.CT && m.bombPlanted && m.Alive(Team.T) == 0
                           && m.agents.Any(x => x.alive && !x.IsT && (x.brain.action == "desarmar" || Match.DefuseTime(x) + (x.pos - m.bombPos).Len / 55f < m.BombLeft));

        /// <summary>Quanto uma arma vale PRA MIM: força no meta; a AWP vale pela função (AWPer quer; o time não quer duas).</summary>
        float ValorArma(string g)
        {
            if (g != "awp") return WeaponStats.Forca(g);
            if (me.role == Role.Awper) return 12;
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && x.gunId == "awp") return 4;   // já tem AWP no time
            return 7.5f;   // melhor que SMG/pistola, pior que rifle na mão de quem não é AWPer
        }

        Match.Drop MelhorDrop(float raio, float ganhoMin)
        {
            Match.Drop best = null; float bv = ValorArma(me.gunId) + ganhoMin;
            foreach (var d in m.drops)
            {
                float v = ValorArma(d.gun);
                if (v > bv && (d.pos - me.pos).Len < raio) { bv = v; best = d; }
            }
            return best;
        }

        /// <summary>
        /// Round ganho: alguém leva a AWP do chão pro AWPer usar no próximo (se o time não tem outra), cada um pega arma
        /// melhor que a dele se tiver, e o resto vai de boa pro bomb.
        /// </summary>
        void DeBoa()
        {
            fase = "round ganho";
            var d = MelhorDrop(700, 0.4f);
            if (d == null)
            {
                bool timeTemAwp = m.agents.Any(x => x.alive && x.team == me.team && x.gunId == "awp");
                var awpChao = m.drops.FirstOrDefault(x => x.gun == "awp");
                if (!timeTemAwp && awpChao != null && m.agents.Where(x => x.alive && x.team == me.team && !x.brain.order.defuse).OrderBy(x => (x.pos - awpChao.pos).Len).First() == me)
                {
                    d = awpChao;
                    var awper = m.agents.FirstOrDefault(x => x.team == me.team && x.role == Role.Awper);
                    if ((d.pos - me.pos).Len > 8) { MoveTo(d.pos, true); Face(d.pos, 3); Think($"pegando a AWP pra dropar pro {awper?.name ?? "AWPer"} no próximo round"); return; }
                }
            }
            if (d != null)
            {
                if ((d.pos - me.pos).Len > 8) { MoveTo(d.pos, true); Face(d.pos, 3); Think($"round ganho: pegando a {WeaponStats.ALL[d.gun].name} do chão pro próximo round"); }
                else m.Pegar(me, d);
                return;
            }
            if ((m.bombPos - me.pos).Len > 120) { MoveTo(m.bombPos, false); Think("round ganho: indo de boa pro bomb"); }
            else { Esperando(null); Think("round ganho — de boa esperando o defuse"); }
        }

        /// <summary>
        /// No meio do round: arma bem melhor caída do lado e ninguém deve estar perto (estimativa) → pega. Rifler não
        /// troca rifle por AWP (quem pega AWP é o AWPer, ou quem está de pistola/SMG).
        /// </summary>
        bool PegarArma(float t)
        {
            if (action != null || me.hasBomb && order.plant || m.drops.Count == 0) return false;
            var d = MelhorDrop(130, 1.5f);   // tem que ser bem melhor pra parar no meio do round (pistola→pistola melhor, SMG→rifle...)
            if (d == null) return false;
            if (m.intel[(int)me.team].Esperados(me.pos, 350) > 0.3f) return false;   // perigoso parar pra isso agora
            fase = "pegando arma";
            if ((d.pos - me.pos).Len > 8) { MoveTo(d.pos, true); Face(d.pos, 3); Think($"seguro aqui: pegando a {WeaponStats.ALL[d.gun].name} do chão"); }
            else m.Pegar(me, d);
            return true;
        }

        /// <summary>Colega dropou granada pra mim, perto e sem perigo: vou lá pegar.</summary>
        bool PegarGranada(float t)
        {
            if (action != null || m.itensChao.Count == 0 || me.hasBomb && order.plant) return false;
            var it = m.itensChao.FirstOrDefault(x => x.para == me && (x.pos - me.pos).Len < 220 && Match.CabeGranada(me, x.nade));
            if (it == null || m.intel[(int)me.team].Esperados(me.pos, 300) > 0.5f) return false;
            fase = "pegando granada"; MoveTo(it.pos, true); Think($"pegando a {it.nade} que o {it.de?.name} dropou");
            return true;
        }

        /// <summary>
        /// O pixel provável: onde o inimigo que está (ou pode estar) em `alvo` vai aparecer pra mim. Se eu já vejo o
        /// lugar, é ele mesmo; senão, anda pelo caminho de mim até ele e pega a quina — o último ponto que eu enxergo
        /// antes da parede, um pouquinho pra fora dela (ninguém aparece dentro da parede). A mira fica ali, na cabeça.
        /// </summary>
        V2 Pixel(V2 alvo)
        {
            if ((alvo - pixelDe).Len < 20 && (me.pos - pixelMeu).Len < 15 && m.t - pixelAt < 0.6f) return pixelRes;
            pixelDe = alvo; pixelMeu = me.pos; pixelAt = m.t; pixelRes = alvo;
            if (MapData.LosWalk(me.pos, me.z, alvo, NavGrid.ZAt(alvo), 18f)) return alvo;
            var p = NavGrid.Path(me.pos, alvo, Perigo.Camada(me.team));
            if (p == null || p.Count < 2) return alvo;
            V2 prev = me.pos, ultimoVisto = me.pos; bool viu = false;
            for (int i = 1; i < p.Count; i++)
            {
                float seg = (p[i].p - prev).Len;
                for (float s = 6; s <= seg; s += 6)
                {
                    V2 q = prev + (p[i].p - prev) * (s / seg);
                    if (MapData.LosWalk(me.pos, me.z, q, NavGrid.ZAt(q), 18f)) { ultimoVisto = q; viu = true; }
                    else if (viu && (q - me.pos).Len > 30)
                    {   // virou a quina: mira na borda, um passo pra dentro da parte que eu vejo
                        pixelRes = ultimoVisto; return pixelRes;
                    }
                }
                prev = p[i].p;
            }
            pixelRes = viu ? ultimoVisto : alvo; return pixelRes;
        }
        V2 pixelDe = new V2(-9999, -9999), pixelMeu, pixelRes; float pixelAt = -9;

        /// <summary>Inimigo em lugar conhecido atrás da parede: a quina atrás da qual ele está, na direção dele.</summary>
        V2 PixelDele(V2 alvo)
        {
            if (MapData.LosWalk(me.pos, me.z, alvo, NavGrid.ZAt(alvo), 18f)) return alvo;
            Pixels.Quina dona = null; float bq = 0.6f, dir = (alvo - me.pos).Atan2;
            foreach (var q in Pixels.Em(me.pos)) { float da = MathF.Abs(MathX.AngDiff((q.ponto - me.pos).Atan2, dir)); if (da < bq) { bq = da; dona = q; } }
            return dona != null ? dona.ponto : Pixel(alvo);
        }

        /// <summary>Segurando sem info: a quina à vista por onde é mais provável aparecer alguém agora, que o time ainda não cobre.</summary>
        V2? QuinaProvavel()
        {
            if (m.t - quinaAt < 0.4f && (me.pos - quinaMeu).Len < 14) return quinaRes;
            quinaAt = m.t; quinaMeu = me.pos; float best = 0; V2? ant = quinaBase; quinaRes = null; quinaBase = null; Pixels.Quina esc = null;
            foreach (var q in Pixels.Em(me.pos))
            {
                float th = 0; bool ouvi = m.t - somT < 2.5f;
                foreach (var a in q.atras) { th = MathF.Max(th, Ameaca(a)); if (ouvi && (a - somP).Len < 130) th = MathF.Max(th, 0.6f); }
                if (th < 0.008f || Lotado(q.ponto)) continue;
                float sc = th * (1.3f - q.dp / Pixels.MAX) * (ant != null && (q.ponto - ant.Value).Len < 40 ? 1.4f : 1f) * Costume(q.ponto);
                if (sc > best) { best = sc; quinaBase = q.ponto; esc = q; }
            }
            if (PicoCostume(null, ref best)) { quinaRes = quinaBase = picoPonto; return quinaRes; }
            if (esc != null) quinaRes = EXP.Contains("semExato") ? esc.ponto : Pixels.Exato(esc, me.pos, me.z);
            return quinaRes;
        }
        float quinaAt = -9; V2 quinaMeu; V2? quinaRes, quinaBase;

        readonly float[] apH = new float[Aparece.BINS]; float apTotal, apAt = -9; V2 apMeu; V2 picoPonto; float picoTh;
        /// <summary>O costume daqui, nesta hora: de onde o inimigo costuma aparecer pra quem está neste lugar.</summary>
        void AtualizaCostume()
        {
            if (m.t - apAt < 1f && (me.pos - apMeu).Len < 18) return;
            apAt = m.t; apMeu = me.pos; apTotal = EXP.Contains("semAparece") || EXP.Contains(me.IsT ? "semApTR" : "semApCT") ? 0 : Aparece.Monta(me.team, me.pos, m.t, m.bombPlanted, apH);
        }
        /// <summary>Multiplica o valor de um ponto pelo quanto costumam aparecer dali (1 = sem dados; um pico vale até ~7x).</summary>
        float Costume(V2 p)
        {
            AtualizaCostume();
            float mem = Lembra((p - me.pos).Atan2);
            if (apTotal < 10) return mem;
            return (0.2f + 60f * Aparece.Parte(apH, (p - me.pos).Atan2)) * mem;
        }
        /// <summary>Esta partida: o quanto essa direção lembra lugar deles em rounds passados. Cada um guarda de um jeito; 1 = nada.</summary>
        float Lembra(float ang)
        {
            var L = m.leitura[(int)me.team]; if (L == null || !L.Adapta || EXP.Contains("semLembra")) return 1;
            return 1 + (0.6f + 0.8f * me.skill.crosshair) * 1.2f * MathF.Min(2, L.Lembranca(me.pos, ang, me.IsT ? Team.CT : Team.T));
        }
        /// <summary>O pixel de onde morremos ou onde vimos alguém parado concorre com as quinas, se puder ter alguém lá agora.</summary>
        bool PicoMemoria(V2? fw, ref float best)
        {
            var L = m.leitura[(int)me.team]; if (L == null || !L.Adapta || EXP.Contains("semLembra")) return false;
            bool achou = false; Team eles = me.IsT ? Team.CT : Team.T;
            foreach (var x in L.deles)
            {
                if (x.lado != eles || x.rodada >= L.rodada || L.rodada - x.rodada > 4) continue;
                V2 d = x.p - me.pos; float dl = d.Len; if (dl < 60 || dl > 650) continue;
                V2 u = d / dl; float pf = fw == null ? 1f : u.Dot(fw.Value) > 0.35f ? 1f : u.Dot(fw.Value) > -0.3f ? 0.6f : 0.2f;
                float th = Ameaca(x.p); if (th < 0.008f) continue;
                float sc = th * pf * 2.5f * x.peso * MathF.Pow(0.75f, L.rodada - x.rodada - 1) * (0.6f + 0.8f * me.skill.crosshair);
                if (sc <= best || Lotado(x.p) || !MapData.LosWalk(me.pos, me.z, x.p, NavGrid.ZAt(x.p), 18f)) continue;
                best = sc; picoPonto = x.p; picoTh = th; achou = true;
            }
            if (achou) m.Ev("mira no pixel lembrado/round");
            return achou;
        }

        /// <summary>O inimigo também aparece fora de quina (lugar aberto, ângulo longo): os picos do costume concorrem com as quinas, se puder ter alguém lá.</summary>
        bool PicoCostume(V2? fw, ref float best)
        {
            AtualizaCostume();
            bool achou = PicoMemoria(fw, ref best);
            if (apTotal < 10) return achou;
            for (int b = 0; b < Aparece.BINS; b++)
            {
                float parte = apH[b]; if (parte < 0.05f) continue;
                V2 d = V2.Ang(Aparece.AngDoBin(b));
                float r = MapData.Raio(me.pos, me.z, d, 450, 18f); if (r < 40) continue;
                V2 q = me.pos + d * MathF.Min(r - 8, 320);
                float th = Ameaca(q); if (th < 0.008f || Lotado(q)) continue;
                float pf = fw == null ? 1f : d.Dot(fw.Value) > 0.35f ? 1f : d.Dot(fw.Value) > -0.3f ? 0.6f : 0.2f;
                float sc = th * pf * (0.2f + 60f * parte);
                if (sc > best) { best = sc; picoPonto = q; picoTh = th; achou = true; }
            }
            return achou;
        }

        /// <summary>Aliado perto que já está com a mira nesse ponto.</summary>
        Agent CobertoPor(V2 L)
        {
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                if (x.brain.Reservou(L) && MathF.Abs(MathX.AngDiff((L - x.pos).Atan2, (L - me.pos).Atan2)) < 0.6f) return x;
                if ((x.pos - me.pos).Len > 400) continue;
                V2 d = L - x.pos; if (d.Len > 750) continue;
                if (MathF.Abs(MathX.AngDiff(d.Atan2, x.aim)) < 0.26f) return x;
            }
            return null;
        }

        /// <summary>Aliado colado começando um wide agora (pra sair junto: peek duplo).</summary>
        public bool ComecandoWide => micro == Micro.Wide && microFase == 0;
        public V2 Rumo => microRumo;

        /// <summary>
        /// Call do time: inimigo que algum aliado viu nos últimos 4 s, perto o bastante pra vir até mim — pré-mira lá
        /// (quando ele aparecer, a mira já está no lugar: reage mais rápido e erra menos).
        /// </summary>
        V2? Chamada(float maxD)
        {
            Intel.Contact best = null; float bd = maxD;
            foreach (var c in m.intel[(int)me.team].contacts.Values)
            {
                if (m.t - c.t > 4 || c.by == me.name || !m.agents[c.id].alive) continue;
                // cada um processa o call no seu tempo (estava olhando outra coisa, terminando o que fazia)
                if (!callVisto.TryGetValue(c.id, out float cv) || m.t - cv > 4) { callRecebe[c.id] = m.t + rng.Uniform(0.1f, 0.45f); callErro[c.id] = V2.Ang(rng.Uniform(0, 6.283f)) * rng.Uniform(15, 55); }
                callVisto[c.id] = m.t;
                if (m.t < callRecebe[c.id] || !Ameacado(c.pos) && Lotado(c.pos)) continue;
                float d = (c.pos - me.pos).Len; if (d < bd) { bd = d; best = c; }
            }
            if (best == null) return null;
            if (lastCall != best.id || m.t - lastCallT > 4) { lastCall = best.id; lastCallT = m.t; Think($"call do {best.by}: {m.agents[best.id].name} na {Region(best.pos)} — pré-mirando", $"{best.by} viu há {m.t - best.t:0.0} s, ele está a {bd:0} de mim e ninguém mais cobre essa linha"); }
            return best.pos + callErro[best.id];
        }
        readonly Dictionary<int, V2> callErro = new Dictionary<int, V2>();
        readonly Dictionary<int, float> callVisto = new Dictionary<int, float>(), callRecebe = new Dictionary<int, float>();
        int lastCall = -1; float lastCallT = -9;

        /// <summary>Colega caiu perto e eu não fui trocar: a mira fica em quem matou (onde ele está ou por onde vai sair), não
        /// no ângulo de antes. Vale enquanto o matador está vivo e a info é nova.</summary>
        V2? FocoMorte()
        {
            if (EXP.Contains("semFocoMorte") || m.t > focoMorteAte || focoMorteDe == null || !focoMorteDe.alive) return null;
            var it = m.intel[(int)me.team];
            V2 p = it.contacts.TryGetValue(focoMorteDe.id, out var c) && c.t > focoMorteT ? c.pos : focoMortePos;
            return p;
        }
        Agent focoMorteDe; V2 focoMortePos; float focoMorteAte = -9, focoMorteT;

        /// <summary>Aliado perto trocando tiro: vai pra cena pra trocar (o duelo vira 2x1 em vez de cada um morrer sozinho).</summary>
        bool AjudarNoDuelo(float t)
        {
            if (t < ajudaAte && ajudaAlvo != null && ajudaAlvo.alive)
            {   // vai pro lugar do duelo (fixo: perseguir a posição exata dele a cada passo recalculava o caminho toda hora)
                fase = "ajudando no duelo"; fonteMira = "ajudando"; MoveTo(ajudaPos, true); Face(PixelDele(ContatoDe(ajudaAlvo) ?? ajudaPos), 3); return true;
            }
            ajudaAlvo = null;
            if (me.hasBomb && order.plant || action != null || t < ajudaCooldown) return false;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || x.brain.target == null) continue;
                float d = (x.pos - me.pos).Len, raio = me.role == Role.Ancora ? 150 : me.gunId == "awp" ? 180 : 260;
                if (d > raio || !x.brain.target.alive) continue;
                if (ContatoDe(x.brain.target) is not V2 vi) continue;   // ele ainda não cantou
                // vêm vários: correr pra cena é morrer junto — seguro o ângulo da troca daqui (raro alguém ir assim mesmo)
                int vistos = m.intel[(int)me.team].contacts.Values.Count(c => t - c.t < 3 && m.agents[c.id].alive && (c.pos - vi).Len < 300);
                if (vistos >= 2 && !EXP.Contains("semSegurarVarios") && !Rola("ajuda varios " + x.brain.target.id + (int)(t / 4), 0.08f * P.Agr(me.skill.aggression)))
                { ajudaCooldown = t + 2; Think($"{vistos} deles ali com o {x.name} — seguro a troca daqui"); return false; }
                if (rng.Rand() > 0.35f + 0.5f * P.Agr(me.skill.aggression)) { ajudaCooldown = t + 1.5f; return false; }   // às vezes segura a posição
                ajudaAlvo = x.brain.target; ajudaAte = t + 2.5f; ajudaCooldown = t + 5; ajudaPos = AnguloCruzado(x, vi);
                Think($"ajudando o {x.name}: trocando tiro com {ajudaAlvo.name} — {((ajudaPos - vi).Len > 1 ? "por outro ângulo" : "indo pra cena")}", $"ele está a {d:0} de mim, {vistos} visto(s) ali: dá pra virar 2x1"); fase = "ajudando no duelo";
                MoveTo(ajudaPos, true); Face(PixelDele(vi), 3);
                return true;
            }
            return false;
        }
        Agent ajudaAlvo; float ajudaAte = -9, ajudaCooldown = -9; V2 ajudaPos;

        /// <summary>
        /// Posição exata do inimigo (o colega está trocando com ele): vou num ângulo DIFERENTE do colega (crossfire — ele
        /// não consegue mirar nos dois), um lugar que vê o inimigo, a 100-220 dele, perto de mim. Sem achar, vou na cena.
        /// </summary>
        V2 AnguloCruzado(Agent colega, V2 inimigo)
        {
            if (EXP.Contains("semCruzado")) return inimigo;
            float aCol = (colega.pos - inimigo).Atan2, zI = NavGrid.ZAt(inimigo);
            V2 best = inimigo; float bv = float.MaxValue;
            for (int k = 0; k < 16; k++)
            {
                float ang = k * 0.3927f;
                if (MathF.Abs(MathX.AngDiff(ang, aCol)) < 0.61f) continue;   // menos de 35° da linha do colega
                foreach (float r in new[] { 110f, 160f, 220f })
                {
                    V2 q = NavGrid.Snap(inimigo + V2.Ang(ang) * r);
                    if (!NavGrid.Movable(q) || !MapData.LosWalk(q, NavGrid.ZAt(q), inimigo, zI, 18f)) continue;
                    float pl = NavGrid.PathLen(me.pos, q);
                    if (pl <= 0 || pl > 320) continue;
                    if (pl < bv) { bv = pl; best = q; }
                }
            }
            return best;
        }

        /// <summary>
        /// Utilitária por conta própria (CT): ouviu/viu gente chegando na entrada do bomb dele → molotov na entrada pra
        /// atrasar enquanto a rotação vem. Uma por round.
        /// </summary>
        bool Utilitaria(float t)
        {
            bool molo = me.nades.Contains("molotov"), he = me.nades.Contains("he");
            if (action != null || !(molo || he || me.nades.Contains("smoke"))) return false;
            var it = m.intel[(int)me.team];
            Lineup l = null; string porque = null;
            if (!me.IsT && !m.bombPlanted && atrasos < 2)
            {
                // CT: gente chegando numa entrada do meu bomb → atrasar até a rotação vir: molotov (tira de lá) ou smoke
                // (corta a visão e a entrada fica no escuro); o time não gasta duas na mesma entrada
                var ents = homeSite == 'A' ? ENTRADAS_A : homeSite == 'B' ? ENTRADAS_B : null;
                (string nome, V2 p) alvo = default; int nMax = 0;
                if (ents != null)
                    foreach (var e in ents)
                    {
                        if ((e.p - me.pos).Len > 520 || m.utilUsada.Contains("atraso " + e.nome) || !Alcanca(e.p)) continue;
                        int n = 0;
                        foreach (var c in it.contacts.Values) if (t - c.t < 3 && (c.pos - e.p).Len < 230 && m.agents[c.id].alive) n += 2;
                        for (int i = it.heard.Count - 1; i >= 0 && t - it.heard[i].t < 3; i--) if (it.heard[i].kind == "passos" && (it.heard[i].p - e.p).Len < 260) n++;
                        if (n > nMax) { nMax = n; alvo = e; }
                    }
                if (nMax >= 2 && Rola($"atraso {alvo.nome} {(int)(t / 5)}", 0.5f + 0.1f * nMax))
                {
                    bool smk = me.nades.Contains("smoke");
                    // vários vindo juntos: HE pega todo mundo de uma vez (e tira vida pra rotação)
                    string kind = he && (nMax >= 4 && rng.Rand() < 0.45f || !molo && !smk) ? "he"
                                : molo && (!smk || rng.Rand() < 0.6f) ? "molotov" : smk ? "smoke" : "he";
                    l = new Lineup($"{kind} na {alvo.nome}", kind, me.pos, alvo.p); atrasos++;
                    m.utilUsada.Add("atraso " + alvo.nome);
                    porque = $"{(nMax >= 4 ? "vi gente" : "ouvi passo")} chegando — atrasar até a rotação vir";
                }
            }
            else if (he && HeNoParado(t, it, out l, out porque)) { }
            else if (!molo) return false;
            else if (!me.IsT && m.bombPlanted && !order.defuse)
            {
                // CT na retomada: TR segurando o pós-plant → molotov pra tirar ele de lá (às vezes ele morre saindo, às vezes ganha a troca)
                foreach (var c in it.contacts.Values)
                    if (t - c.t < 4 && m.agents[c.id].alive && (c.pos - me.pos).Len < 450 && (c.pos - m.bombPos).Len < 400 && Alcanca(c.pos) && Rola("retake" + c.id, 0.45f))
                    { l = new Lineup($"molotov no {m.agents[c.id].name}", "molotov", me.pos, c.pos); porque = "tirar ele do pós-plant"; break; }
            }
            else if (me.IsT && m.bombPlanted && m.defuserActive && (m.bombPos - me.pos).Len < 500 && Alcanca(m.bombPos) && Rola("defuse", 0.75f))
            { l = new Lineup("molotov no bomb", "molotov", me.pos, m.bombPos); porque = "ouvi o defuse"; }
            if (l == null) return false;
            m.utilUsada.Add(l.name); action = "granada"; actionNade = l; actionEnd = t + 0.9f; fase = "ação";
            Think($"{l.name}: {porque}");
            return true;
        }

        /// <summary>
        /// Smoke do inimigo na frente com gente provável atrás (estimativa ou passo ouvido): HE na smoke abre um furo por
        /// ~3 s e dá pra pegar quem está esperando atrás. Jogo eu mesmo, ou peço pra um aliado perto que tenha ("abre a
        /// smoke"). Uma decisão por smoke, com chance — às vezes nem vale a granada.
        /// </summary>
        bool AbrirSmoke(float t)
        {
            if (action != null || t < heOlhar || m.smokes.Count == 0 || order.defuse || me.hasBomb && order.plant) return false;
            heOlhar = t + 0.8f;
            var it = m.intel[(int)me.team];
            foreach (var s in m.smokes)
            {
                if (!m.smokeDe.TryGetValue(s, out var dono) || dono == me.team) continue;
                if (s.age < 2.5f || s.age > CSM.View.SmokeVolume.LIFE - 4) continue;   // ainda subindo / já vai sumir sozinha
                V2 c = new V2(s.origin.x, s.origin.y); float d = (c - me.pos).Len;
                if (d < 90 || d > 420) continue;
                if (!MapData.LosWalk(me.pos, me.z + 18, c, s.origin.z + 14, 18f)) continue;   // tenho que ver a smoke pra usar o furo
                if (m.agents.Any(x => x.alive && x.team == me.team && (x.pos - c).Len < 110)) continue;   // aliado dentro/colado nela
                float e = it.Esperados(c, 200); int passos = 0;
                for (int i = it.heard.Count - 1; i >= 0 && t - it.heard[i].t < 3; i--) if ((it.heard[i].p - c).Len < 240) passos++;
                if (e < 0.6f && passos == 0) continue;
                float p = 0.2f + 0.3f * P.Agr(me.skill.aggression) + 0.12f * MathF.Min(e, 2) + (passos > 0 ? 0.15f : 0);
                if (!Rola($"he smoke {s.GetHashCode()}", p)) continue;
                string porque = passos > 0 ? "ouvi passo atrás da smoke" : $"deve ter {e:0.#} atrás da smoke";
                if (me.nades.Contains("he"))
                {
                    float t1 = Jogar("he", $"HE na smoke ({Region(c)}): {porque} — abrir e pegar quem tá atrás", c);
                    furoPos = c; furoDesde = t1; furoAte = t1 + 2.6f; me.heFuro++;
                    return true;
                }
                foreach (var x in m.agents)
                {
                    if (x == me || !x.alive || x.team != me.team || (x.pos - me.pos).Len > 350) continue;
                    float t1 = x.brain.JogarHE(c, me);
                    if (t1 < 0) continue;
                    Think($"{porque} — pedi HE pro {x.name} abrir"); furoPos = c; furoDesde = t1; furoAte = t1 + 2.6f;
                    return true;
                }
            }
            return false;
        }
        float heOlhar = -9, furoAte = -9, furoDesde; V2 furoPos;

        /// <summary>Ouviu passo perto, fora da vista, e ele deve estar parado ali ou passando: molotov/HE no barulho (às vezes).</summary>
        bool GranadaNoBarulho(float t)
        {
            if (action != null || EXP.Contains("semPunir") || t < proxBarulho) return false;
            var som = (p: somP, kind: somK);
            if (som.kind != "passos" || t - somT > 1.2f) return false;
            float d = (som.p - me.pos).Len; if (d < 120 || d > 330) return false;
            string kind = me.nades.Contains("molotov") ? "molotov" : me.nades.Contains("he") ? "he" : null;
            if (kind == null || !Alcanca(som.p) || m.agents.Any(x => x.alive && x.team == me.team && (x.pos - som.p).Len < 110)) return false;
            proxBarulho = t + 6;
            if (!Rola("granada barulho " + (int)(t / 6), 0.18f + 0.25f * P.Agr(me.skill.aggression))) return false;
            me.puniu++;
            Jogar(kind, $"{kind} no barulho ({Region(som.p)}): ouvi passo ali", som.p);
            return true;
        }
        float proxBarulho;

        /// <summary>
        /// Usar a granada antes de precisar, como pro: vale quando as rotas dizem que eles costumam estar ali agora (ou daqui a
        /// pouco) e dá pra alcançar. CT atrasa a chegada na entrada; TR limpa a posição onde costumam segurar. Não tem hora nem
        /// script: cada lugar pede granada conforme a chance, e o time não repete o mesmo lugar em seguida.
        /// </summary>
        string guardaUtil;
        bool UtilProativa(float t)
        {
            if (action != null || t < proxUtil || m.bombPlanted && !me.IsT || order.defuse || me.hasBomb && order.plant || EXP.Contains("semUtilPro")) return false;
            proxUtil = t + 1.2f;
            var inim = me.IsT ? Team.CT : Team.T; var it = m.intel[(int)me.team];
            string[] tipos = me.IsT ? new[] { "flash", "smoke", "molotov", "he" } : new[] { "molotov", "smoke", "he", "flash" };
            // CT costuma guardar uma granada pro que vier depois (retake); cada um decide uma vez por round
            if (!me.IsT && guardaUtil == null) guardaUtil = rng.Rand() < 0.6f && EXP.Contains("guardaUtil") ? new[] { "flash", "molotov", "he" }[rng.Int(3)] : "";
            string kind = tipos.FirstOrDefault(k => me.nades.Contains(k) && k != guardaUtil); if (kind == null) return false;
            (string nome, V2 p) alvo = default; float bv = 0;
            void Ve(string nome, V2 p)
            {
                float d = (p - me.pos).Len;
                if (d < 140 || d > 560 || m.utilUsada.Contains($"pro {nome} {(int)(t / 12)}")) return;
                float v = Rotas.Perto(inim, p, t, 120) + Rotas.Perto(inim, p, t + 5, 120);
                if (me.IsT) v *= MathF.Min(1, it.HeatAvg(p, 70) * 4);   // TR: só onde ainda pode ter alguém
                if (v > bv) { bv = v; alvo = (nome, p); }
            }
            if (me.IsT) foreach (var h in HOLDS_CT) Ve(h.name, h.pos);
            else foreach (var e in ENTRADAS_A.Concat(ENTRADAS_B)) Ve(e.nome, e.p);
            if (bv < 0.12f || AmigoNoAlvo(alvo.p) || !Alcanca(alvo.p) || rng.Rand() > 0.1f + 0.25f * MathF.Min(1, bv)) return false;
            m.utilUsada.Add($"pro {alvo.nome} {(int)(t / 12)}"); m.Ev($"granada proativa {kind}/round");
            V2 cai = kind == "flash" ? NaCara(alvo.p) : alvo.p;
            if (kind == "flash" && m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - cai).Len < 300)) return false;   // colega perto da flash
            Jogar(kind, $"{kind} em {alvo.nome}", cai);
            porquePendente = $"eles costumam estar ali agora ({bv:0.00}) e ainda tenho {kind}";
            return true;
        }
        float proxUtil;

        /// <summary>
        /// HE de dano: inimigo visto há pouco segurando um canto perto (não está andando) e sem aliado do lado dele —
        /// tirar vida antes do duelo (entrada do TR, pós-plant, retake). Com chance, uma vez por inimigo.
        /// </summary>
        bool HeNoParado(float t, Intel it, out Lineup l, out string porque)
        {
            l = null; porque = null;
            foreach (var c in it.contacts.Values)
            {
                if (t - c.t > 4 || t - c.t < 0.6f || !m.agents[c.id].alive) continue;
                float d = (c.pos - me.pos).Len; if (d < 130 || d > 400 || !Alcanca(c.pos)) continue;
                if (m.agents.Any(x => x.alive && x.team == me.team && (x.pos - c.pos).Len < 120)) continue;
                bool pos = m.bombPlanted && (c.pos - m.bombPos).Len < 350;   // segurando o bomb / indo desarmar
                if (!Rola("he dano " + c.id, (pos ? 0.4f : 0.22f) + (c.hp < 100 ? 0.15f : 0))) continue;
                var nome = m.agents[c.id].name;
                l = new Lineup($"HE no {nome}", "he", me.pos, c.pos);
                porque = c.hp < 100 ? $"ele tá com {c.hp} — HE pode matar" : pos ? "tirar vida de quem segura o bomb" : "tirar vida antes do duelo";
                return true;
            }
            return false;
        }

        /// <summary>Colega pediu "abre a smoke": jogo a HE se estou livre e vejo a smoke. Devolve quando estoura (-1 = não).</summary>
        public float JogarHE(V2 c, Agent pediu)
        {
            if (!me.alive || action != null || target != null || !me.nades.Contains("he") || order.defuse || me.hasBomb && order.plant) return -1;
            if ((c - me.pos).Len > 480 || !MapData.LosWalk(me.pos, me.z + 18, c, NavGrid.ZAt(c) + 14, 18f)) return -1;
            float t1 = Jogar("he", $"HE na smoke pro {pediu.name} ({Region(c)})", c);
            furoPos = c; furoDesde = t1; furoAte = t1 + 2.6f; me.heFuro++;
            return t1;
        }

        /// <summary>Decide uma vez por situação (com chance), pra não ficar rolando o dado todo passo.</summary>
        bool Rola(string chave, float p) => rolou.Add(chave) && rng.Rand() < p;
        readonly HashSet<string> rolou = new HashSet<string>();
        bool decidiuSmoke; int atrasos; float esperaSmoke = -9, esperaCalc = -9, proxTomada; V2 esperaPonto;
        static string EXP => TeamBrain.EXP;

        /// <summary>
        /// Onde esperar a vez indo pra dest: o ponto mais adiantado do caminho onde ainda não pode ter inimigo (calor ~0
        /// num raio), sem passar de ~100 antes do destino. Recalculado a cada 1 s (o calor anda).
        /// </summary>
        V2 PontoDeEspera(V2 dest)
        {
            var it = m.intel[(int)me.team];
            if (it.HeatIn(me.pos, 200) > 0.05f) return me.pos;   // já estou onde pode ter gente: espera aqui
            var p = NavGrid.Path(me.pos, dest, Perigo.Camada(me.team));
            if (p == null || p.Count == 0) return me.pos;
            V2 seguro = me.pos, prev = me.pos;
            foreach (var w in p)
            {
                float seg = (w.p - prev).Len;
                for (float s = 10; s <= seg; s += 10)
                {
                    V2 q = prev + (w.p - prev) * (s / seg);
                    // pode ter gente perto, ou fica na mira de onde eles costumam segurar (a AWP do janelão vê de longe)
                    if ((q - dest).Len < 100 || it.HeatIn(q, 200) > 0.05f || Perigo.Visto(me.team, q) > 0) return seguro;
                    seguro = q;
                }
                prev = w.p;
            }
            return seguro;
        }

        /// <summary>Esperando a vez (entry ir primeiro, smoke subir, colado no entry): ajeita de um lado pro outro devagar,
        /// sem perder a mira (abaixo de 34% da velocidade continua preciso).</summary>
        void Esperando(V2[] look)
        {
            fase = "esperando a vez";
            DecidirC4(m.t);
            if (look != null && look.Length > 0) Face(Pixel(LookAlvo(look)), 3); else ClearAngles(false);
            if (m.intel[(int)me.team].HeatIn(me.pos, 350) < 0.05f) return;   // ninguém pode estar perto: fica parado, sem dançar
            if (!EXP.Contains("dancaEspera")) return;   // esperando a vez gente fica parada mirando (dançar A-D sem duelo não é de pro)
            V2 f = V2.Ang(me.aim), side = new V2(-f.y, f.x);
            float s = MathF.Sin(m.t * 2.2f + me.id * 1.7f);
            if (MathF.Abs(s) > 0.35f) { V2 v = side * MathF.Sign(s) * me.MaxSpeed * 0.3f; if (NavGrid.Movable(me.pos + v * 0.4f)) wantVel = v; }
        }

        /// <summary>
        /// Segurando uma posição. Chega, acha um cover perto (lugar que não vê o ângulo, com um ponto de peek a um passo)
        /// e segura dali dando peek — ou segura aberto (AWP, quem não acha cover). A cada pausa escolhe a próxima jogada
        /// pela utilidade: jiggle (info com pouco risco), wide (info/duelo, agressivo e com aliado perto), ajeitar,
        /// trocar de ângulo (cansou do lugar ou entregou a posição), avançar (tomar espaço) ou segurar quieto (ouviu passo,
        /// AWP). Os pesos de cada uma vêm dos Pesos (o treino ajusta).
        /// </summary>
        void HoldHere(V2[] look)
        {
            float t = m.t;
            if (SairDaLinhaLonga(t)) return;
            if (me.gunId == "awp" && !EXP.Contains("semLuneta")) me.scoped = micro == Micro.Nada || micro == Micro.Quieto;   // AWP segurando já está na luneta; só sai pra se mexer
            V2? L = null;
            if (look != null && look.Length > 0) { L = micro == Micro.Wide || micro == Micro.Avancar ? microRumo : LookAlvo(look); alvoMira = Pixel(L.Value); Reservar(L.Value, alvoMira); Face(alvoMira.Value, 3); }   // do cover: mira na quina por onde o ângulo aparece
            else { ClearAngles(false); L = focus; }
            if (baseP == null) { baseP = ancora = me.pos; baseDesde = t; baseLook = new V2(-999, -999); trilha.Clear(); }
            // a mira mudou: só replaneja o lugar se daqui não dá pra ver o novo ângulo (segurar é ficar, não dançar a cada call)
            if (micro == Micro.Nada && L != null && (L.Value - baseLook).Len > 30 && t - planAt > 1.5f
                && (baseLook.x < -900 || alvoMira == null || !MapData.LosWalk(me.pos, me.z, alvoMira.Value, NavGrid.ZAt(alvoMira.Value), 18f))) PlanCover(L.Value);
            if (micro != Micro.Nada) { fase = FASE_MICRO[(int)micro]; RunMicro(L); return; }
            fase = deCover ? "pausa no cover" : "pausa segurando aberto";
            // volta pro lugar (depois do peek pelo mesmo caminho; depois de ajeitar/trocar já é o lugar novo)
            if (trilha.Count > 0)
            {
                V2 b = trilha[trilha.Count - 1];
                if ((b - me.pos).Len < 4) trilha.RemoveAt(trilha.Count - 1); else { Ir(b, me.MaxSpeed); return; }
            }
            V2 home = baseP.Value;
            if ((home - me.pos).Len > 3) { Ir(home, (home - me.pos).Len < 45 ? me.MaxSpeed : me.MaxSpeed * 0.52f); return; }
            if (t >= microProx) Escolher(L);
            else if (micro == Micro.Nada) Think($"segurando {order.hold?.name ?? "posição"}{(L != null ? ", olhando " + Region(L.Value) : "")}{(deCover ? " do cover" : "")}");
        }

        float proxSaida;
        /// <summary>
        /// Janelão é posição de abrir o round: dá a olhada, às vezes um tiro, e sai (ficar lá dá o tempo pro TR smokar ou
        /// esperar o AWP). A cada segundo pesa: entreguei a posição, atirei, ninguém mais vem por essa linha. Sai pra outra
        /// posição do meio que ninguém do time segura, então o CT continua com o meio.
        /// </summary>
        bool SairDaLinhaLonga(float t)
        {
            if (order.hold != CT_Janelao || t < proxSaida || EXP.Contains("semSaida")) return false;
            proxSaida = t + 1;
            float vem = Ameaca(Meio_Sul) + Ameaca(TopoEntrada);
            float p = 0.02f + (t - entregouEm < 4 ? 0.1f : 0) + (t - me.lastShotT < 3 ? 0.12f : 0) + (vem < 0.05f ? 0.1f : 0);
            if (rng.Rand() >= p) return false;
            Hold melhor = null; float bv = float.MaxValue;
            foreach (var h in new[] { CT_MeioCaixa, CT_Praca, CT_Conector, CT_Rato })
            {
                if (HoldOcupado(h)) continue;
                float c = NavGrid.PathLen(me.pos, h.pos) + rng.Uniform(0, 250);
                if (c < bv) { bv = c; melhor = h; }
            }
            if (melhor == null) return false;
            m.Ev("saiu do janelão/round");
            SetOrder(new Order { name = melhor.name, hold = melhor, why = t - entregouEm < 4 ? "me viram no janelão: troco de lugar e sigo no meio" : "já dei a olhada do janelão: sigo no meio de outro ângulo" });
            return true;
        }

        float proxAngulo;
        /// <summary>
        /// Colega segurando a mesma coisa que eu: dois olhando o mesmo lugar do mesmo lado não prendem ninguém. Um vai pra outro
        /// ponto que vê o mesmo lugar por outro lado, longe da linha do colega: ganha espaço e o inimigo tem que olhar dois lugares.
        /// </summary>
        bool BuscarAngulo(V2 L, float t)
        {
            if (t < proxAngulo || order.hold == null || order.plant || me.hasBomb || order.malote || EXP.Contains("semAnguloNovo")) return false;
            proxAngulo = t + rng.Uniform(3f, 6f);
            var it = m.intel[(int)me.team];
            var colegas = new List<Agent>();
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 380 && (x.brain.Reservou(L) || MathF.Abs(MathX.AngDiff((L - x.pos).Atan2, x.aim)) < 0.35f)
                    && MathF.Abs(MathX.AngDiff((L - x.pos).Atan2, (L - me.pos).Atan2)) < 0.6f) colegas.Add(x);
            if (colegas.Count == 0) return false;
            foreach (var c in it.contacts.Values) if (t - c.t < 2 && (c.pos - me.pos).Len < 300) return false;   // duelo perto: agora não é hora de sair
            if (rng.Rand() > (me.gunId == "awp" ? 0.08f : 0.15f + 0.2f * P.Agr(me.skill.aggression))) return false;
            Spots.Spot? best = null; float bv = 0;
            foreach (var s in Spots.All())
            {
                float dL = (s.p - L).Len; if (dL < 90 || dL > 520) continue;
                float sep = 9; bool ruim = false; V2 dirS = L - s.p;
                foreach (var x in colegas) { float a = MathF.Abs(MathX.AngDiff(dirS.Atan2, (L - x.pos).Atan2)); sep = MathF.Min(sep, a); if ((x.pos - s.p).Len < 80) ruim = true; }
                sep = MathF.Min(sep, MathF.Abs(MathX.AngDiff(dirS.Atan2, (L - me.pos).Atan2)));
                if (ruim || sep < 0.7f || it.Esperados(s.p, 140) >= 0.35f) continue;
                if (!MapData.LosWalk(s.p, s.z, L, NavGrid.ZAt(L), 18f)) continue;
                float pl = NavGrid.PathLen(me.pos, s.p); if (pl < 90 || pl > 850) continue;
                float v = sep * (1.3f - pl / 900f) * rng.Uniform(0.7f, 1.3f) * (EXP.Contains("semUmAngulo") ? 1 : 1 / MathF.Max(1, Exposicao(s.p, t)));
                if (v > bv) { bv = v; best = s; }
            }
            if (best == null) return false;
            var q = best.Value; m.Ev("buscou ângulo novo/round");
            SetOrder(new Order { name = "buscando outro ângulo", hold = new Hold("outro ângulo", q.p, L), quiet = true, why = $"{colegas[0].name} já olha {Region(L)} daqui: vou por outro lado pra ele ter que olhar dois lugares", paciente = order.paciente });
            return true;
        }

        /// <summary>
        /// Faca ou arma: o calor no começo do round É o timing — ele escorre do spawn inimigo na velocidade de quem corre,
        /// então onde não tem calor perto de mim (nem no caminho à frente) nenhum inimigo consegue estar ainda: faca na
        /// mão e corre. Chegando na zona de contato (ou vendo/ouvindo alguém), saca a arma.
        /// </summary>
        void FacaOuArma(float t)
        {
            var it = m.intel[(int)me.team];
            bool contato = false;
            foreach (var c in it.contacts.Values) if (t - c.t < 8 && (c.pos - me.pos).Len < 900) contato = true;
            V2 frente = path != null && wp < path.Count ? path[Math.Min(path.Count - 1, wp + 1)].p : me.pos;
            bool seguro = !contato && action == null && it.HeatAvg(me.pos, 380) < 0.004f && it.HeatAvg(frente, 300) < 0.004f
                          && !m.bombPlanted && t < 60;
            if (seguro && !me.faca && target == null && me.gun.inMag > 0 && !me.gun.Reloading)
            {
                me.faca = true; me.scoped = false;
                if (!logouFaca) { logouFaca = true; Think("faca na mão — ninguém chega aqui ainda", $"calor perto {it.HeatAvg(me.pos, 380):0.000}, à frente {it.HeatAvg(frente, 300):0.000}: inimigo não chega aqui agora"); }
            }
            else if (!seguro && me.faca) Sacar(t, contato ? "saquei: tem contato perto" : "saquei a arma — chegando na zona de contato");
        }
        bool logouFaca;

        /// <summary>
        /// Portador do C4 no controle de mapa: chegando na zona de contato (sem inimigo conhecido perto), deixa o bomb num
        /// canto de parede seguro, um pouco pra trás e fora da passagem, e vai jogar a posição livre (o C4 nas costas faz
        /// jogar com medo). Quando o IGL decidir o bomb, quem passa mais perto pega. Decide uma vez por round, pelo jeito dele.
        /// </summary>
        void LargarC4(float t)
        {
            if (!me.hasBomb || !me.IsT || order.plant || m.bombPlanted || EXP.Contains("semLargaC4")) return;
            var it = m.intel[(int)me.team];
            if (order.push) return;
            // jogo lento, sem saber nada do mapa ainda: o C4 fica na base até decidir pra onde vai
            if (order.paciente && !decidiuBase && t > 1 && !EXP.Contains("c4Velho"))
            {
                decidiuBase = true;
                if (Rola("c4 na base", 0.35f)) { V2 c = CantoC4(it); m.GuardaBomb(me, c); m.Ev("C4 deixado na base/round"); Think($"deixo o C4 aqui na base ({Region(c)})", "jogo lento e sem info: decido pra onde vai depois"); return; }
            }
            if (decidiuC4) return;
            if (it.HeatIn(me.pos, 380) < 0.05f) return;   // ainda longe do contato: leva junto
            decidiuC4 = true;
            var igl = m.igl[(int)me.team];
            if (igl.plan.StartsWith("rush") || it.Esperados(me.pos, 300) > 0.3f) return;
            if (PassarC4(it)) return;
            if (rng.Rand() > 0.45f + 0.25f * me.skill.aggression) { Think("fico com o C4 — vou jogar mais pra trás"); return; }
            if (PassarC4(it)) return;
            V2 onde = CantoC4(it);
            m.GuardaBomb(me, onde);
            Think($"deixo o C4 no canto ({Region(onde)}) e vou ajudar a tomar espaço — alguém pega quando a gente decidir o bomb");
        }
        bool decidiuC4;

        /// <summary>
        /// C4 em quem não está levando ele a lugar nenhum. Não é regra de tempo: nas horas em que estou livre pra decidir pesa
        /// se o C4 aqui atrapalha o time. Ouvindo alguém, com contato ou com inimigo em cima, não faz nada (fica quieto e joga).
        /// Pesa mais quanto mais longe estou do bomb em relação a quem pode levar e quanto mais o round já andou comigo aqui.
        /// </summary>
        void DecidirC4(float t)
        {
            if (!me.hasBomb || !me.IsT || order.plant || order.push || m.bombPlanted || t < proxC4 || EXP.Contains("c4Velho")) return;
            proxC4 = t + 1.5f;
            var it = m.intel[(int)me.team];
            if (target != null || t - somT < 2.5f || t - lastSeenT < 4 || it.Esperados(me.pos, 250) >= 0.8f) return;
            V2 bomb = Plant(m.planSite); float meu = NavGrid.PathLen(me.pos, bomb), melhor = meu;
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && x.role != Role.Entry) melhor = MathF.Min(melhor, NavGrid.PathLen(x.pos, bomb));
            float aqui = me.c4Desde >= 0 ? t - me.c4Desde : 0;
            float p = aqui / (aqui + 15f) * (melhor < meu - 60 ? 1f : 0.35f);
            if (rng.Rand() >= p) return;
            if (PassarC4(it)) return;
            V2 canto = CantoC4(it);
            m.GuardaBomb(me, canto); m.Ev("C4 largado por estar parado/round");
            Think($"deixo o C4 no canto ({Region(canto)}) e sigo jogando", $"chance {p:0%}: {aqui:0} s aqui com ele, eu a {meu:0} do bomb e tem colega a {melhor:0}; sem ninguém perto de mim agora");
        }
        float proxC4;

        /// <summary>Vou entrar onde não conheço: dropa pro amigo que está mais pra trás (nunca pro entry nem pra quem está em duelo).</summary>
        bool PassarC4(Intel it)
        {
            if (EXP.Contains("c4Velho")) return false;
            Agent q = null; float bh = it.HeatIn(me.pos, 200) + 0.001f;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || x.role == Role.Entry || x.brain.target != null || x.brain.action != null || (x.pos - me.pos).Len > 420) continue;
                float h = it.HeatIn(x.pos, 200); if (h < bh) { bh = h; q = x; }
            }
            if (q == null) return false;
            V2 ponto = NavGrid.Snap(q.pos);   // no pé dele: o meio do caminho pode ser parede
            m.GuardaBomb(me, ponto); m.Ev("C4 passado pro amigo/round");
            var antes = q.brain.order;
            q.brain.SetOrder(new Order { name = "pegar o bomb", dest = ponto, why = $"{me.name} vai entrar e me passou o C4", until = m.t + (ponto - q.pos).Len / MathF.Max(q.MaxSpeed, 1) * 1.3f + 1, then = antes });
            Think($"passo o C4 pro {q.name} e vou entrar", $"entrada arriscada, ele está mais pra trás (risco ali {bh:0.00})");
            return true;
        }
        bool decidiuBase;

        /// <summary>Canto seguro aqui perto, um pouco pra trás e fora da passagem.</summary>
        V2 CantoC4(Intel it)
        {
            V2 fw = path != null && wp < path.Count ? path[wp].p - me.pos : V2.Ang(me.aim); float fl = fw.Len; fw = fl > 0.1f ? fw / fl : V2.Ang(me.aim);
            V2 onde = me.pos; float best = float.NegativeInfinity;
            for (int r = 12; r <= 48; r += 12)
                for (int k = 0; k < 16; k++)
                {
                    V2 q = me.pos + V2.Ang(k * MathF.PI / 8) * r;
                    if (!NavGrid.Movable(q) || !NavGrid.Direct(me.pos, q) || Perigo.Visto(me.team, q) > 0 || float.IsInfinity(NavGrid.PathLen(NavGrid.Snap(q), Plant('A')))) continue;
                    int paredes = 0;   // canto: parede colada em 2+ lados
                    for (int j = 0; j < 8; j++) if (MapData.Raio(q, NavGrid.ZAt(q), V2.Ang(j * MathF.PI / 4), 10, 18f) < 10) paredes++;
                    float sc = paredes - 40 * it.HeatAvg(q, 60) - 1.5f * MathF.Max(0, ((q - me.pos) / r).Dot(fw)) - r / 60f;
                    if (sc > best) { best = sc; onde = q; }
                }
            return onde;
        }

        void Sacar(float t, string porque)
        {
            if (!me.faca) return;
            me.faca = false; me.sacaAte = t + me.TempoSacar;
            Think(porque, $"calor perto {m.intel[(int)me.team].HeatAvg(me.pos, 380):0.000}: pode ter gente aqui, arma na mão");
        }

        /// <summary>Anda até p: reto se dá (passo curto), senão pela navmesh.</summary>
        void Ir(V2 p, float spd)
        {
            V2 d = p - me.pos; float l = d.Len; if (l < 0.8f) return;
            if (m.fires.Count > 0 && m.FireAt(me.pos + d / l * 20)) return;   // passinho de jiggle/ajeitar não entra no fogo
            if (NavGrid.Direct(me.pos, p)) { if (l < 8) spd *= MathF.Max(0.35f, l / 8); wantVel = d / l * spd; me.move = spd > me.MaxSpeed * 0.6f ? Move.Correndo : Move.Andando; }
            else MoveTo(p, spd > me.MaxSpeed * 0.6f);
        }

        /// <summary>Acha cover e ponto de peek em volta da âncora pra olhar L (anéis de 8 a 36 unidades).</summary>
        void PlanCover(V2 L) { long pc = P0(); if (planAt > 0) m.Aud("cover replanejado", me.IsT ? "TR" : "CT"); PlanCoverX(L); P1("planejar cover", pc); }
        void PlanCoverX(V2 L)
        {
            baseLook = L; planAt = m.t; temPeek = false;
            V2 a = ancora; float zL = NavGrid.ZAt(L);
            bool Ve(V2 q) => MapData.LosWalk(q, NavGrid.ZAt(q), L, zL, 18f);
            // peek é de LADO (A-D, perpendicular a onde mira), como gente faz — ir e voltar na linha do ângulo (W-S) não
            // é peek, é andar pra frente e pra trás olhando pro mesmo lugar
            bool DeLado(V2 de, V2 para)
            {
                if (EXP.Contains("peekQualquer")) return true;
                V2 mv = para - de, ol = L - de; float lm = mv.Len, lo = ol.Len;
                return lm < 0.5f || lo < 1 || MathF.Abs(mv.Dot(ol)) / (lm * lo) < 0.5f;   // > 60° da linha de visão
            }
            var cand = new List<(V2 p, bool ve)> { (a, Ve(a)) };
            for (int r = 8; r <= 36; r += 7)
                for (int k = 0; k < 12; k++)
                {
                    V2 q = a + V2.Ang(k * MathF.PI / 6 + r) * r;
                    if (NavGrid.Movable(q) && NavGrid.Direct(a, q)) cand.Add((q, Ve(q)));
                }
            float best = float.MaxValue; V2 cov = a, pk = a; bool achou = false;
            foreach (var c in cand)
            {
                if (c.ve) continue;
                foreach (var d in cand)
                {
                    if (!d.ve) continue;
                    float dd = (c.p - d.p).Len; if (dd < 6 || dd > P.peekDist * 1.3f || !DeLado(c.p, d.p)) continue;
                    float sc = dd + (c.p - a).Len * 0.5f;
                    if (sc < best && NavGrid.Direct(c.p, d.p)) { best = sc; cov = c.p; pk = d.p; achou = true; }
                }
            }
            if (achou && rng.Rand() < P.cover * (me.gunId == "awp" ? 0.4f : 1f)) { baseP = cov; peekPt = pk; temPeek = true; deCover = true; return; }
            // aberto: fica num ponto que vê; o jiggle é um passo de lado (A-D) até outro ponto que vê
            deCover = false;
            V2 home = a; float hd = cand[0].ve ? 0 : float.MaxValue;
            foreach (var c in cand) if (c.ve && (c.p - a).Len < hd) { hd = (c.p - a).Len; home = c.p; }
            baseP = home;
            foreach (var c in cand)
            {
                float dd = (c.p - home).Len;
                if (c.ve && dd > 8 && dd < P.peekDist * 1.3f && NavGrid.Direct(home, c.p) && DeLado(home, c.p) && (!temPeek || rng.Rand() < 0.4f)) { peekPt = c.p; temPeek = true; }
            }
        }

        static string Onde(V2? p) => p != null ? Region(p.Value) : "o ângulo";

        float pixelProx = -9, lookFocoAte = -9; V2 lookFoco;
        /// <summary>
        /// Smoke no meu ângulo L: a passagem que ela esconde (pontos em volta de L fora da fumaça, pesados por onde deve
        /// ter gente) — qual lugar perto de mim vê mais dela sem a smoke na frente? AWP prefere ficar longe; quem é
        /// agressivo topa chegar perto da smoke. Um pouco de sorte no meio: o pixel improvável é o que pega.
        /// </summary>
        bool PixelFora(V2 L, out V2 alvo, out V2 olhar, out bool ataca)
        {
            alvo = me.pos; olhar = L; ataca = false;
            var it = m.intel[(int)me.team];
            var pass = new List<(V2 p, float w)>();
            foreach (float r in new[] { 45f, 90f, 140f })
                for (int k = 0; k < 12; k++)
                {
                    V2 p = L + V2.Ang(k * 0.5236f) * r;
                    if (!NavGrid.Movable(p) || m.SmokeNear(p, 15)) continue;
                    pass.Add((p, 0.2f + it.HeatAvg(p, 50)));
                }
            if (pass.Count == 0) return false;
            float agr = P.Agr(me.skill.aggression), dL = (me.pos - L).Len; bool awp = me.gunId == "awp";
            var cands = new List<(V2 q, float sc, V2 olha)>();
            for (float r = 30; r <= 240; r += 30)
                for (int k = 0; k < 14; k++)
                {
                    V2 q = me.pos + V2.Ang(k * 0.4488f + r * 0.01f) * r;
                    if (!NavGrid.Movable(q) || m.SmokeNear(q, 10)) continue;
                    float zq = NavGrid.ZAt(q), vis = 0, melhor = 0; V2 olha = L;
                    foreach (var (p, w) in pass)
                    {
                        float zp = NavGrid.ZAt(p);
                        if (!MapData.LosWalk(q, zq, p, zp, 18f) || m.SmokeBlocks(q, zq + me.Eye, p, zp + 18)) continue;
                        vis += w; if (w > melhor) { melhor = w; olha = p; }
                    }
                    if (vis <= 0) continue;
                    float frente = dL - (q - L).Len;   // >0 = cheguei mais perto da smoke
                    float sc = vis * 10 - r * 0.02f - it.HeatIn(q, 110) * 15 + (awp ? -0.03f : 0.02f * (agr - 0.4f)) * frente + rng.Uniform(0, 3);
                    cands.Add((q, sc, olha));
                }
            foreach (var c in cands.OrderByDescending(c => c.sc).Take(4))
            {
                float pl = NavGrid.PathLen(me.pos, c.q);
                if (pl > 340) continue;
                alvo = c.q; olhar = c.olha; ataca = (c.q - L).Len < dL - 80;
                return true;
            }
            return false;
        }

        void Escolher(V2? L) { long pc = P0(); EscolherX(L); P1("escolher jogada", pc); }
        void EscolherX(V2? L)
        {
            float t = m.t; var it = m.intel[(int)me.team];
            float calor = L != null ? MathF.Min(1, it.HeatAvg(L.Value, 110) * 1.5f) : 0;
            bool contato = false, awp = false;
            foreach (var c in it.contacts.Values)
            {
                if (t - c.t < 5 && (c.pos - me.pos).Len < 550) contato = true;
                if (t - c.t < 10 && m.agents[c.id].gunId == "awp" && m.agents[c.id].alive) awp = true;
            }
            bool ouviuPerto = t - somT < 2.5f && (somP - me.pos).Len < 300;
            bool aliado = false;
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 220) aliado = true;
            float agr = P.Agr(me.skill.aggression), aqui = t - baseDesde;
            bool entregue = t - entregouEm < 4;
            // ninguém pode estar aqui ainda (nem calor, nem contato, nem som): não tem por que dançar nem dar peek, é ficar pronto
            bool semPorque = !EXP.Contains("dancaVazia") && calor < 0.03f && !contato && !ouviuPerto;
            bool podeAvancar = L != null && order.hold != null && (me.IsT || order.then != null) && (ancora - order.hold.pos).Len < 150;

            DecidirC4(t);
            if (L != null && BuscarAngulo(L.Value, t)) return;
            var u = new float[8];
            // abrir o ângulo com bang (pra mim ou pedindo pro colega): quando deve ter gente ali e eu quero ir
            // bang só onde pode ter inimigo (calor no ângulo ou contato PERTO dele) e nunca onde um colega está ou está entrando
            bool contatoL = L != null && it.contacts.Values.Any(c => t - c.t < 5 && m.agents[c.id].alive && (c.pos - L.Value).Len < 300);
            bool amigoLa = L != null && !EXP.Contains("bangVelho") && AmigoNoAlvo(L.Value);
            if (EXP.Contains("bangVelho")) contatoL = contato;
            u[(int)Micro.Bang] = L != null && !amigoLa && me.gunId != "awp" && (calor > 0.3f || contatoL) && (me.nades.Contains("flash") || AliadoComFlash(320) != null)
                ? P.bang * (calor + (contatoL ? 0.6f : 0)) * (0.5f + agr) : 0;
            u[(int)Micro.Jiggle] = temPeek && L != null ? P.jiggle * (0.35f + P.sedeInfo * calor + (awp ? 0.5f : 0)) : 0;
            u[(int)Micro.Wide] = L != null && me.gunId != "awp" ? P.wide * agr * (contato ? 0.5f : 1) * (awp ? 0.25f : 1) * (aliado ? 1.3f : 0.6f) : 0;
            u[(int)Micro.Ajeitar] = P.ajeitar * 0.6f;
            u[(int)Micro.Trocar] = P.trocar * (aqui / P.cansaLugar + (entregue ? 2.5f : 0))
                                   * (!me.IsT && !entregue && !contato && !ouviuPerto ? 0.3f : 1f);   // CT sem info não fica trocando de lugar à toa
            u[(int)Micro.Avancar] = podeAvancar ? P.avancar * agr * (1 - calor) * (contato ? 0.2f : 1) : 0;
            u[(int)Micro.Quieto] = P.quieto * (ouviuPerto || contato ? 2.2f : 0.5f) * (me.gunId == "awp" ? 1.8f : 1) * MathF.Max(0.2f, 1 - 0.4f * P.estilo) + 0.01f;
            // posição forte: ficar e esperar paga mais que se mexer; posição fraca: parado ali só perde duelo
            float forca = order.hold != null ? Posicoes.Forca(me.IsT, order.hold.name) : 0.5f, fk = MathX.Clamp((forca - 0.35f) / 0.15f, 0, 2);
            u[(int)Micro.Quieto] *= 0.7f + 1.5f * fk; u[(int)Micro.Bang] *= 0.7f + 1.5f * fk; u[(int)Micro.Avancar] *= 1.3f - 0.5f * fk; u[(int)Micro.Ajeitar] *= 1.2f - 0.5f * fk; u[(int)Micro.Wide] *= 1.2f - 0.4f * fk;
            if (!entregue) u[(int)Micro.Trocar] *= 1.3f - 0.5f * fk;
            if (semPorque) { u[(int)Micro.Jiggle] = 0; u[(int)Micro.Wide] = 0; u[(int)Micro.Bang] = 0; u[(int)Micro.Ajeitar] = 0; u[(int)Micro.Trocar] = 0; u[(int)Micro.Quieto] += 0.5f; }
            if (order.paciente)
            {   // jogo lento: segura o ângulo e espera alguém se expor (pouco wide/avanço; jiggle pra info ainda vale)
                u[(int)Micro.Wide] *= 0.3f; u[(int)Micro.Avancar] *= 0.25f; u[(int)Micro.Quieto] *= 1.8f;
            }
            // cabeça: em pânico fica inquieto e dá peek afobado; frio no clutch isola duelo (segura, pega info, sem wide)
            float pn = me.panico; bool clutchFrio = m.ClutchFor(me.team) && me.skill.composure > 0.6f && pn < 0.35f && !EXP.Contains("semPanico");
            if (clutchFrio) { u[(int)Micro.Wide] *= 0.25f; u[(int)Micro.Avancar] *= 0.4f; u[(int)Micro.Quieto] *= 1.6f; u[(int)Micro.Jiggle] *= 1.2f; }
            else if (pn > 0.3f) { u[(int)Micro.Wide] *= 1 + 1.2f * pn; u[(int)Micro.Trocar] *= 1 + pn; u[(int)Micro.Quieto] *= 1 - 0.5f * pn; }
            // meu ângulo foi smokado: não fico olhando fumaça — procuro um pixel que veja a passagem por fora dela (por
            // baixo, pelo lado, um lugar improvável) ou, se sou agressivo, vou pra cima da smoke pegar quem cruza
            if (L != null && m.smokes.Count > 0 && t > pixelProx && !EXP.Contains("semPixel")
                && m.SmokeBlocks(me.pos, me.z + me.Eye, L.Value, NavGrid.ZAt(L.Value) + 18))
            {
                pixelProx = t + 4;
                if (PixelFora(L.Value, out var q, out var olhar, out bool ataca))
                {
                    micro = Micro.Trocar; microFase = 0; microAlvo = q; microAte = t + 5; trilha.Clear(); trilhaAt = -9; me.trocas++; me.pixelSmoke++;
                    lookFoco = olhar; lookFocoAte = t + 9;
                    if (order.hold == CT_Janelao) m.Ev("saiu do janelão por causa de smoke/round");
                    Think(ataca ? $"smoke em {Onde(L)} — vou pra cima dela pegar quem cruza ({Region(q)})" : $"smoke em {Onde(L)} — outro pixel pra ver a passagem ({Region(q)})");
                    return;
                }
            }
            var esc = (Micro)rng.Weighted(u);
            if (esc == Micro.Nada) esc = Micro.Quieto;
            if (Explicar)
            {
                float tot = MathF.Max(0.001f, u.Sum());
                string pesos = string.Join(", ", Enumerable.Range(0, u.Length).Where(i => u[i] > 0).OrderByDescending(i => u[i]).Take(4).Select(i => $"{FASE_MICRO[i]} {u[i] / tot:0%}"));
                porquePendente = $"pesei: {pesos} | calor no ângulo {calor:0.0}, contato {(contato ? "sim" : "não")}, ouvi passo {(ouviuPerto ? "sim" : "não")}, aliado perto {(aliado ? "sim" : "não")}, AWP deles {(awp ? "sim" : "não")}, {aqui:0} s aqui{(amigoLa ? ", bang descartado: colega no ângulo" : "")}, posição vale {forca:0.00}";
            }
            // punir o barulho: ouviu passo perto (ele correndo = mira ruim e posição entregue) → às vezes sai largo pra pegar
            // ele andando, em vez de só esperar (quem é agressivo vai mais)
            if (ouviuPerto && me.gunId != "awp" && !EXP.Contains("semPunir") && Rola("punir passo " + (int)(t / 4), 0.2f + 0.35f * agr))
            {
                if (somK == "passos") { esc = Micro.Wide; L = somP; me.puniu++; Think($"ouvi passo em {Region(somP)} — vou pegar ele andando"); }
            }
            // vou buscar espaço com o C4 nas costas onde deve ter CT: deixo ele aqui atrás (seguro) e o time pega na execução
            if (me.hasBomb && !order.plant && (esc == Micro.Wide || esc == Micro.Avancar || esc == Micro.Bang) && calor > 0.3f
                && it.Esperados(me.pos, 250) < 0.2f && !EXP.Contains("semGuardaC4") && Rola("guarda c4 " + (int)(t / 10), 0.6f))
            { if (!PassarC4(it)) { m.GuardaBomb(me); Think($"deixo o C4 aqui atrás ({Region(me.pos)}) e vou buscar espaço"); } }
            // call de dano: inimigo fraco perto (alguém do time tirou vida dele agora) → vai em cima
            foreach (var c in it.contacts.Values)
                if (t - c.hpT < 4 && c.hp < 45 && m.agents[c.id].alive && (c.pos - me.pos).Len < 450 && me.gunId != "awp" && Rola("fraco" + c.id + "_" + (int)c.hpT, 0.45f + 0.4f * agr))
                { esc = Micro.Wide; L = c.pos; Think($"{m.agents[c.id].name} tá com {c.hp} — vou em cima"); break; }
            // vê a jogada do aliado: ele está saindo largo do lado → sai junto (o inimigo não consegue mirar nos dois)
            Agent dupla = null;
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 130 && x.brain.ComecandoWide) dupla = x;
            if (dupla != null && me.gunId != "awp" && rng.Rand() < 0.4f + 0.5f * agr) { esc = Micro.Wide; L = dupla.brain.Rumo; }

            // o colega avisou "vou bangar" pro ângulo que eu seguro: saio junto quando estourar (em vez de outra jogada)
            if (L != null && t < me.bangAmigaEm - 0.3f && (me.bangAmigaPos - L.Value).Len < 220 && me.gunId != "awp" && rng.Rand() < 0.5f + 0.4f * agr)
            {
                micro = Micro.Bang; bangPorMim = true; microFase = 1; microRumo = L.Value; bangSai = me.bangAmigaEm - 0.3f; microAte = t + 4;
                Think($"o {me.bangAmigaDe.name} vai bangar {Onde(L)} — saio junto"); return;
            }
            if (Explicar) porquePendente += $" → fui de {FASE_MICRO[(int)esc]}";
            micro = esc; microFase = 0; microAte = t + 4; trilha.Clear(); trilhaAt = -9;
            if (esc == Micro.Wide && pn > 0.5f) { me.afobado++; Think("na pressão — vou no peek largo"); }
            if (clutchFrio && esc == Micro.Quieto) { me.frio++; if (rng.Rand() < 0.3f) Think($"clutch: um de cada vez, sem pressa"); }
            switch (esc)
            {
                case Micro.Jiggle:
                    microAlvo = peekPt; microDwell = rng.Uniform(0.12f, 0.45f) * (awp ? 0.5f : 1); me.peeks++;
                    microRep = calor > 0.3f && rng.Rand() < 0.6f ? 1 + rng.Int(2) : 0;   // deve ter gente: A-D repetido pra pegar info
                    Think($"{(microRep > 0 ? "strafe (A-D) pra" : "jiggle pra")} ver {Onde(L)}{(awp ? " (AWP deles)" : calor > 0.5f ? " (deve ter gente)" : "")}"); break;
                case Micro.Wide:
                    if (!IniciaWide(L.Value, false)) { micro = Micro.Quieto; microAte = t + rng.Uniform(1.2f, 2.8f); Think($"sem como sair largo de lado — segurando apertado {Onde(L)}"); break; }
                    Think(dupla != null ? $"peek duplo com o {dupla.name} em {Onde(L)}" : $"wide peek em {Onde(L)} pra pegar info"); break;
                case Micro.Bang:
                    {
                        microRumo = L.Value; microAte = t + 4;
                        bangAlvo = NaCara(L.Value);
                        var colega = AliadoComFlash(320);
                        bangPorMim = me.nades.Contains("flash") && (colega == null || rng.Rand() < 0.55f);
                        Think(bangPorMim ? $"bang pra mim e peek em {Onde(L)}" : $"me banga {Onde(L)}, {colega.name}!");
                        if (!bangPorMim) { meuPedido = new Match.PedidoBang { quem = me, alvo = bangAlvo, t = t }; m.pedidos.Add(meuPedido); me.pediuBang++; }
                    }
                    break;
                case Micro.Ajeitar:
                    microAlvo = me.pos;
                    for (int k = 0; k < 8; k++) { V2 q = baseP.Value + V2.Ang(rng.Uniform(0, 6.283f)) * rng.Uniform(4, 10); if (NavGrid.Movable(q) && NavGrid.Direct(me.pos, q)) { microAlvo = q; break; } }
                    Think("ajeitando a posição"); break;
                case Micro.Trocar:
                    if (!NovoAngulo(L, out microAlvo)) { micro = Micro.Nada; microProx = t + 0.5f; return; }
                    me.trocas++; microAte = t + 6;
                    Think(entregue ? "entreguei a posição — trocando de ângulo" : $"trocando de ângulo ({aqui:0}s no mesmo lugar)"); break;
                case Micro.Avancar:
                    microRumo = L.Value; microAlvo = PontoNoCaminho(me.pos, L.Value, rng.Uniform(35, 70)); me.avancos++; microAte = t + 5;
                    Think($"avançando pra tomar espaço ({Region(microAlvo)})"); break;
                case Micro.Quieto:
                    microAte = t + rng.Uniform(1.2f, 2.8f);
                    Think($"segurando apertado {Onde(L)}{(ouviuPerto ? " (ouvi passo)" : "")}"); break;
            }
        }

        void RunMicro(V2? L)
        {
            float t = m.t;
            if (micro == Micro.Bang) { RunBang(t); return; }
            // ouviu passo perto no meio de uma jogada andando: para e segura apertado (não é hora de se mexer)
            if ((micro == Micro.Wide || micro == Micro.Trocar || micro == Micro.Ajeitar || micro == Micro.Avancar)
                && t - somT < 0.6f && somK == "passos" && (somP - me.pos).Len < 380)
            {
                micro = Micro.Quieto; microFase = 0; microAte = t + rng.Uniform(1.5f, 3f); trilha.Clear();
                Think("ouvi passo perto — paro e seguro apertado"); return;
            }
            if (t > microAte)
            {
                if (micro == Micro.Jiggle && microFase == 1 && microRep > 0) { microRep--; microFase = 2; microAte = t + 1.5f; me.peeks++; }   // volta e sai de novo
                else
                {
                    // saiu largo e não tinha ninguém: às vezes fica no ângulo novo (quem segura apertado na quina antiga erra)
                    // em vez de voltar de ré pelo mesmo caminho
                    if (micro == Micro.Wide && microFase == 1 && !EXP.Contains("wideVolta") && rng.Rand() < 0.35f + 0.3f * P.Agr(me.skill.aggression)
                        && m.intel[(int)me.team].HeatIn(me.pos, 120) < 1.2f)
                    { trilha.Clear(); ancora = me.pos; baseP = me.pos; baseDesde = t; baseLook = new V2(-999, -999); planAt = -9; Think("ninguém ali — fico nesse ângulo novo", "quem segura a quina antiga erra se eu ficar aqui"); }
                    FimMicro(); return;
                }
            }
            switch (micro)
            {
                case Micro.Quieto:
                    // segurar apertado é com o ângulo à vista e a mira pré-posicionada: do cover vai de shift até o peek
                    if (microFase == 0 && deCover && temPeek) { if ((peekPt - me.pos).Len > 2.5f) { Ir(peekPt, me.MaxSpeed * 0.45f); return; } microFase = 1; }
                    return;
                case Micro.Jiggle:
                    if (microFase == 0) { if ((microAlvo - me.pos).Len > 2.5f) { Ir(microAlvo, me.MaxSpeed); return; } microFase = 1; microAte = t + microDwell; }
                    else if (microFase == 2) { if ((baseP.Value - me.pos).Len > 2.5f) { Ir(baseP.Value, me.MaxSpeed); return; } microFase = 0; microAte = t + 1.5f; microDwell = rng.Uniform(0.1f, 0.35f); }
                    return;   // fase 1: parado olhando (counter-strafe) e volta pro cover; fase 2: voltando pra sair de novo
                case Micro.Wide:
                    if (microFase == 0)
                    {
                        if (t - trilhaAt > 0.15f) { trilha.Add(me.pos); trilhaAt = t; }   // migalhas pra voltar pelo mesmo caminho
                        if ((microAlvo - me.pos).Len > 5) { SegueRumo(microAlvo, me.MaxSpeed); return; }
                        microFase = 1; microAte = t + microDwell;
                    }
                    return;
                case Micro.Ajeitar:
                    if ((microAlvo - me.pos).Len > 1.5f && t < microAte) { Ir(microAlvo, me.MaxSpeed * 0.3f); return; }
                    baseP = me.pos; FimMicro(); return;
                case Micro.Trocar:
                case Micro.Avancar:
                    if ((microAlvo - me.pos).Len > 4) { if (micro == Micro.Avancar) SegueRumo(microAlvo, me.MaxSpeed * 0.52f); else Ir(microAlvo, me.MaxSpeed * 0.52f); return; }
                    ancora = me.pos; baseP = me.pos; baseDesde = t; baseLook = new V2(-999, -999); planAt = -9; FimMicro(); return;
            }
        }

        void FimMicro() { micro = Micro.Nada; microProx = m.t + P.pausa * rng.Uniform(0.4f, 1.6f); }

        /// <summary>Sai largo. Com a bang (<paramref name="podeReto"/>) entra reto se não tiver lado; wide por escolha, não.</summary>
        bool IniciaWide(V2 L, bool podeReto = true)
        {
            var lado = WideLado(L, P.peekDist * rng.Uniform(1.8f, 3f));
            if (lado == null && !podeReto && !EXP.Contains("wideReto")) return false;
            micro = Micro.Wide; microFase = 0; microAte = m.t + 4; trilha.Clear(); trilhaAt = -9;
            microRumo = L; microAlvo = lado ?? PontoNoCaminho(me.pos, L, P.peekDist * rng.Uniform(1.8f, 3f)); microDwell = rng.Uniform(0.5f, 1.3f); me.peeks++;
            return true;
        }

        /// <summary>
        /// Wide de verdade: sair LARGO de lado da quina (o inimigo que mira apertado na quina não acha), num ponto que vê
        /// o ângulo — não ir reto na direção dele e voltar de ré pelo mesmo caminho.
        /// </summary>
        V2? WideLado(V2 L, float dist)
        {
            if (EXP.Contains("wideReto")) return null;
            V2 ol = L - me.pos; float lo = ol.Len; if (lo < 30) return null;
            V2 dir = ol / lo, lado = new V2(-dir.y, dir.x); float zL = NavGrid.ZAt(L);
            V2? melhor = null; float bs = float.MaxValue;
            foreach (float k in new[] { 1f, 0.6f, 1.4f })
            foreach (float s in new[] { 1f, -1f })
                for (float ang = 0.8f; ang <= 1.6f; ang += 0.16f)   // 45°..90° da linha de visão (um pouco pra frente)
                {
                    V2 q = me.pos + (lado * s * MathF.Sin(ang) + dir * MathF.Cos(ang)) * dist * k;
                    if (!NavGrid.Movable(q) || !NavGrid.Direct(me.pos, q)) continue;
                    if (!MapData.LosWalk(q, NavGrid.ZAt(q), L, zL, 18f)) continue;
                    float sc = m.intel[(int)me.team].HeatIn(q, 90) + rng.Uniform(0, 0.3f);   // menos exposto a outros ângulos
                    if (sc < bs) { bs = sc; melhor = q; }
                }
            return melhor;
        }

        // bang e peek: joga (ou espera o colega jogar) e sai junto com o estouro
        V2 bangAlvo; bool bangPorMim; Match.PedidoBang meuPedido; float bangSai;
        void RunBang(float t)
        {
            if (microFase == 0)
            {
                if (bangPorMim)
                {
                    float t1 = Jogar("flash", $"flash pra mim (pop-flash) em {Region(bangAlvo)}", bangAlvo);
                    bangSai = t1 - 0.35f; microFase = 1; return;   // sai um pouco antes de estourar (a flash pega quem está olhando)
                }
                microFase = 1; bangSai = -1; microAte = t + 2.5f;
            }
            if (bangPorMim) { if (t >= bangSai) { IniciaWide(microRumo); Think("saindo com a bang"); } return; }
            // esperando o colega bangar
            if (meuPedido.atendido != null && meuPedido.estouraEm > 0) { if (t >= meuPedido.estouraEm - 0.35f) { IniciaWide(microRumo); Think($"o {meuPedido.atendido.name} bangou — entrando"); } return; }
            if (t > microAte)
            {
                m.pedidos.Remove(meuPedido);
                if (rng.Rand() < 0.4f + 0.4f * P.Agr(me.skill.aggression)) { IniciaWide(microRumo); Think("ninguém bangou — vou assim mesmo"); }
                else { Think("ninguém bangou — fico"); FimMicro(); }
            }
        }

        float fugaFogoCalc = -9; V2 fugaFogo;
        /// <summary>Ponto fora do fogo mais perto, preferindo o lado do meu spawn (pra trás, longe do inimigo).</summary>
        /// <summary>Fogo aqui ou colado: a saída tem que ficar longe da borda, senão um passo de lado volta pra dentro.</summary>
        bool FogoPerto(V2 q) => m.FireAt(q) || m.FireAt(q + new V2(20, 0)) || m.FireAt(q - new V2(20, 0)) || m.FireAt(q + new V2(0, 20)) || m.FireAt(q - new V2(0, 20));
        V2 SaidaDoFogo()
        {
            V2 casa = (me.IsT ? MapData.SPAWN_T : MapData.SPAWN_CT)[0];
            V2 best = me.pos; float bv = float.MaxValue;
            for (float r = 16; r <= 96; r += 16)
                for (int k = 0; k < 16; k++)
                {
                    V2 q = me.pos + V2.Ang(k * 0.3927f) * r;
                    if (!NavGrid.Movable(q) || FogoPerto(q) || !NavGrid.Direct(me.pos, q)) continue;
                    float sc = r + 0.25f * ((q - casa).Len - (me.pos - casa).Len) + 40 * m.intel[(int)me.team].HeatIn(q, 80);
                    if (target != null && MapData.LosWalk(q, NavGrid.ZAt(q), target.pos, target.z, 18f)) sc += 70;   // quem está me vendo: prefere sair pra onde ele não vê
                    if (sc < bv) { bv = sc; best = q; }
                }
            if (bv < float.MaxValue) return best;
            // sem saída reta perto (fogo grande, corredor): a saída mais perto andando, mesmo passando pelo fogo
            for (float r = 112; r <= 240; r += 16)
                for (int k = 0; k < 16; k++)
                {
                    V2 q = NavGrid.Snap(me.pos + V2.Ang(k * 0.3927f) * r);
                    if (!NavGrid.Movable(q) || m.FireAt(q)) continue;
                    float sc = NavGrid.PathLen(me.pos, q);
                    if (sc < bv) { bv = sc; best = q; }
                }
            return best;
        }

        /// <summary>Daqui essa granada para perto do alvo? Mede na física antes de gastar; se não para, não joga.</summary>
        bool CaiNoLugar(string kind, V2 alvo, string nome)
        {
            if (kind != "smoke" && kind != "molotov") return true;
            float erro = Match.MiraCerta(me.pos, me.z, alvo, kind).erro;
            if (erro <= 40) return true;
            m.Ev("granada não jogada: daqui não cai no pixel/round");
            Think($"{nome}: daqui não chega", $"testei o arremesso e ele para a {erro:0} do alvo");
            return false;
        }

        /// <summary>Daqui o arco chega lá (sem prédio no meio)?</summary>
        /// <summary>Granada guardada pra entrada do retake (flash vai na cara de quem está lá).</summary>
        public bool JogaRetake(string kind, V2 alvo)
        {
            if (action != null || !Alcanca(alvo) || AmigoNoAlvo(alvo)) return false;
            V2 cai = kind == "flash" ? NaCara(alvo) : alvo;
            Jogar(kind, $"{kind} na entrada do retake", cai);
            return action != null;
        }

        bool Alcanca(V2 alvo) => (Match.Arco(me.pos, me.z, alvo) - alvo).Len < 20;

        /// <summary>Joga uma granada daqui (a ação leva ~0,5 s); devolve quando ela estoura/cai.</summary>
        float Jogar(string kind, string nome, V2 alvo)
        {
            if (!CaiNoLugar(kind, alvo, nome)) return m.t;
            var l = new Lineup(nome, kind, me.pos, alvo);
            action = "granada"; actionNade = l; actionEnd = m.t + 0.5f; fase = "ação";
            Think(nome);
            return m.t + 0.5f + 0.4f + (alvo - me.pos).Len / 380f;
        }

        /// <summary>Um colega está lá (ou indo pra lá, à minha frente): flash ali cega ele e não tem inimigo pra cegar.</summary>
        bool AmigoNoAlvo(V2 L)
        {
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                if ((x.pos - L).Len < 170) return true;
                V2? vai = x.brain.order.hold?.pos ?? (x.brain.order.dest.Len > 1 ? x.brain.order.dest : (V2?)null);
                if (vai != null && (vai.Value - L).Len < 200 && (x.pos - L).Len < (me.pos - L).Len + 50) return true;
            }
            return false;
        }

        Agent AliadoComFlash(float raio)
        {
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < raio && x.nades.Contains("flash") && x.brain.action == null) return x;
            return null;
        }

        /// <summary>
        /// Onde estourar a flash: na cara de quem segura L — um ponto colado no ângulo, do meu lado, que eu alcanço
        /// jogando (em arco). Se não alcanço, o ponto mais perto do ângulo no caminho.
        /// </summary>
        V2 NaCara(V2 L)
        {
            V2 d = me.pos - L; float l = d.Len; if (l < 1) return L;
            foreach (float k in new[] { 45f, 75f, 110f })
            {
                V2 q = L + d / l * MathF.Min(k, l * 0.8f);
                if (MapData.LosWalk(me.pos, me.z + 10, q, NavGrid.ZAt(q) + 14, 18f)) return q;
            }
            return PontoVisivelNoCaminho(L, 70, 190);
        }

        /// <summary>Ponto do caminho até L, entre min e max unidades, que eu consigo ver (dá pra jogar a granada lá).</summary>
        V2 PontoVisivelNoCaminho(V2 L, float min, float max)
        {
            var p = NavGrid.Path(me.pos, L, Perigo.Camada(me.team));
            V2 best = me.pos + (L - me.pos) / MathF.Max(1, (L - me.pos).Len) * min; float acc = 0; V2 prev = me.pos;
            if (p != null)
                for (int i = 1; i < p.Count && acc < max; i++)
                {
                    float seg = (p[i].p - prev).Len;
                    for (float s = 0; s < seg && acc + s < max; s += 10)
                    {
                        V2 q = prev + (p[i].p - prev) * (s / MathF.Max(1, seg));
                        if (acc + s >= min && MapData.LosWalk(me.pos, me.z + 6, q, NavGrid.ZAt(q), 18f)) best = q;
                    }
                    acc += seg; prev = p[i].p;
                }
            return best;
        }

        /// <summary>Aliado pediu bang perto de mim e eu tenho flash: bango pra ele (nem sempre: posso estar ocupado).</summary>
        bool AtenderBang(float t)
        {
            if (action != null || !me.nades.Contains("flash")) return false;
            foreach (var p in m.pedidos)
            {
                if (p.atendido != null || p.quem == me || p.quem.team != me.team || !p.quem.alive || t - p.t > 2 || (p.quem.pos - me.pos).Len > 350) continue;
                if (!EXP.Contains("bangVelho") && m.agents.Any(x => x != p.quem && x != me && x.alive && x.team == me.team && (x.pos - p.alvo).Len < 150)) continue;   // tem colega lá
                if (!Rola("pedido" + p.GetHashCode(), 0.8f)) continue;
                p.atendido = me; me.bangou++;
                p.estouraEm = Jogar("flash", $"bang pro {p.quem.name}", p.alvo);
                return true;
            }
            return false;
        }

        /// <summary>
        /// Entrando num lugar (push): canto suspeito à frente → às vezes abre com bang (pra mim, ou o entry pede), às vezes
        /// limpa o cantinho com molotov. Decisão por canto, com chance.
        /// </summary>
        static readonly Hold[] HOLDS_CT = { CT_A_Plataforma, CT_A_Caixa, CT_A_Canto, CT_Janelao, CT_Praca, CT_B_Banca, CT_B_Caixa, CT_B_Pilar, CT_B_Viela, CT_A_RuaCT };
        static readonly Hold[] HOLDS_T = { T_TopoMeio, T_MeioSul, T_RampaBaixo, T_RampaDobra, T_PalacioSala, T_CasaraoDobra, T_CurtaCima, T_Conector };

        bool UtilEntrando(float t)
        {
            if (action != null) return false;
            // smoke na cara de quem costuma segurar a minha entrada (posição típica do inimigo, quente, que vê o meu caminho)
            if (me.nades.Contains("smoke") && path != null && wp < path.Count && t >= proxSmokeHold)
            {
                proxSmokeHold = t + 0.5f;
                V2 nx = path[Math.Min(path.Count - 1, wp + 1)].p;
                foreach (var hd in me.IsT ? HOLDS_CT : HOLDS_T)
                {
                    float dh = (hd.pos - me.pos).Len;
                    if (dh > 650 || dh < 150 || m.intel[(int)me.team].HeatAvg(hd.pos, 60) < 0.3f || m.utilUsada.Contains("smokehold" + hd.name)) continue;
                    if (!MapData.LosWalk(nx, NavGrid.ZAt(nx), hd.pos, NavGrid.ZAt(hd.pos), 18f)) continue;
                    if (!Rola("smokehold" + hd.name, 0.5f)) continue;
                    V2 s = hd.pos + (nx - hd.pos) / MathF.Max(1, (nx - hd.pos).Len) * 45;
                    if (!MapData.LosWalk(me.pos, me.z + 10, s, NavGrid.ZAt(s) + 14, 18f)) continue;
                    m.utilUsada.Add("smokehold" + hd.name);
                    Jogar("smoke", $"smoke na cara de quem segura {hd.name} pra entrar", s);
                    return true;
                }
            }
            if (focus == null) return false;
            V2 f = focus.Value; float d = (f - me.pos).Len, h = m.intel[(int)me.team].HeatAt(f);
            if (h < 0.35f || d < 90 || d > 300) return false;
            if (!EXP.Contains("bangVelho") && AmigoNoAlvo(f)) return false;   // o colega já está lá / entrando lá
            string chave = "entrada" + (int)(f.x / 24) + "_" + (int)(f.y / 24);
            if (rolou.Contains(chave)) return false;
            rolou.Add(chave);
            float agr = P.Agr(me.skill.aggression);
            if (me.nades.Contains("molotov") && focusName == "cantinho" && rng.Rand() < 0.25f && MapData.LosWalk(me.pos, me.z + 6, f, NavGrid.ZAt(f), 18f))
            { Jogar("molotov", $"molotov no cantinho ({Region(f)}) pra limpar", f); return true; }
            if (rng.Rand() > 0.35f + 0.3f * agr) return false;
            var alvo = NaCara(f);
            if (me.nades.Contains("flash") && (me.role != Role.Entry || rng.Rand() < 0.5f)) { Jogar("flash", $"flash no {focusName} ({Region(f)}) antes de entrar", alvo); return true; }
            if (AliadoComFlash(320) != null) { m.pedidos.Add(new Match.PedidoBang { quem = me, alvo = alvo, t = t }); me.pediuBang++; Think($"me banga o {focusName} ({Region(f)})!"); }
            return false;
        }

        /// <summary>
        /// Andando: o próximo trecho está na mira de um inimigo que a gente sabe onde está (longe) → smoke na linha e cruza
        /// por trás dela (se tiver smoke e achar que vale). Uma decisão por inimigo.
        /// </summary>
        bool SmokeCruzar(float t)
        {
            if (action != null || !me.nades.Contains("smoke") || path == null || wp >= path.Count || t < esperaCruzar) return t < esperaCruzar && Parar("esperando a smoke subir pra cruzar");
            V2 nx = path[wp].p;
            foreach (var c in m.intel[(int)me.team].contacts.Values)
            {
                if (t - c.t > 15 || !m.agents[c.id].alive || (c.pos - nx).Len < 250 || (c.pos - nx).Len > 1100) continue;
                if (!MapData.LosWalk(nx, NavGrid.ZAt(nx), c.pos, c.z, 18f)) continue;
                if (!Rola("cruzar" + c.id + "_" + (int)(nx.x / 60) + "_" + (int)(nx.y / 60), m.agents[c.id].gunId == "awp" ? 0.75f : 0.45f)) continue;
                V2 s = nx + (c.pos - nx) / (c.pos - nx).Len * 55;
                Jogar("smoke", $"smoke pra cruzar na mira do {m.agents[c.id].name}", s);
                esperaCruzar = t + 0.5f + 0.4f + (s - me.pos).Len / 380f + 1.3f;
                return true;
            }
            return false;
        }
        /// <summary>
        /// Andar de shift (sem barulho): perto de onde pode ter inimigo, ninguém entrega a posição correndo — a não ser
        /// com pressa (entrando no execute/retomada, pouco tempo, longe de tudo). Quem é mais calmo segura mais o shift.
        /// </summary>
        bool Silencio(Order o)
        {
            if (EXP.Contains("semShift") || o.push && !o.calmo || o.defuse || me.faca) return false;
            // sem tempo: corre. No retake quem manda é o tempo do defuse, não um número fixo
            if (m.RoundLeft < 25 && !m.bombPlanted || m.bombPlanted && !me.IsT && m.BombLeft < NavGrid.PathLen(me.pos, m.bombPos) / MapCheck.RUN + 10 * (EXP.Contains("retakeVelho") ? 2 : 1)) return false;
            if (me.panico > 0.6f) return false;   // em pânico esquece de andar no shift
            var it = m.intel[(int)me.team];
            float perto = it.HeatAvg(me.pos, 300);
            if (path != null && wp < path.Count) perto = MathF.Max(perto, it.HeatAvg(path[Math.Min(path.Count - 1, wp + 2)].p, 300));
            return perto > 0.03f * (1.6f - me.skill.composure);
        }

        /// <summary>
        /// CT andando (girar, voltar pro lugar) e 2+ inimigos vistos agora na frente, perto: não vai pra cima deles —
        /// pega o cover aqui e segura (eles que venham no meu ângulo; strafe e tiro no counter-strafe). Rushar assim é
        /// raro (só quem é muito agressivo, às vezes). Uma decisão por situação.
        /// </summary>
        bool SegurarContraVarios(float t)
        {
            if (me.IsT || m.bombPlanted || order.push || order.defuse || EXP.Contains("semSegurarVarios") || path == null || wp >= path.Count) return false;
            V2 rumo = path[wp].p - me.pos; float rl = rumo.Len; if (rl < 1) return false;
            rumo = rumo / rl;
            var vs = m.intel[(int)me.team].contacts.Values.Where(c => t - c.t < 4 && m.agents[c.id].alive && (c.pos - me.pos).Len < 450 && (c.pos - me.pos).Dot(rumo) > 0.3f * (c.pos - me.pos).Len).ToList();
            if (vs.Count < 2) return false;
            if (Rola("rush contra varios " + (int)(t / 6), 0.07f * P.Agr(me.skill.aggression))) { Think($"vêm {vs.Count} — vou pra cima mesmo"); return false; }
            if (!rolou.Add("segura varios " + (int)(t / 6))) return false;
            V2 c0 = vs.Aggregate(new V2(0, 0), (s, c) => s + c.pos) / vs.Count;
            var antes = order;
            SetOrder(new Order { name = $"segurando contra {vs.Count}", hold = new Hold("cover contra " + vs.Count, NavGrid.Snap(me.pos), vs.Select(c => c.pos).Concat(new[] { c0 }).ToArray()),
                                 why = $"vi {vs.Count} vindo — pego cover e seguro o ângulo", until = t + 10, then = antes });
            Think($"vi {vs.Count} vindo na minha frente — pego cover aqui e seguro");
            return true;
        }

        float esperaCruzar = -9, proxSmokeHold, semAndarDesde, desenroscaAte = -9;
        bool Parar(string porque) { wantVel = V2.Zero; Think(porque); return true; }

        /// <summary>
        /// Caminho entra numa smoke do inimigo: atravessar (pegar de surpresa — arriscado: pode ter gente segurando a
        /// saída) ou esperar ela abrir. Decide uma vez por smoke; esperando, reavalia quando o tempo aperta.
        /// </summary>
        bool SmokeNoCaminho(float t)
        {
            if (path == null || wp >= path.Count) return false;
            var s = m.SmokeInimiga(path[wp].p, me.team) ?? m.SmokeInimiga(me.pos + (path[wp].p - me.pos) * 0.5f, me.team);
            if (s == null) return false;
            string chave = "atravessar" + (int)s.origin.x + "_" + (int)s.origin.y;
            if (!decisaoSmoke.TryGetValue(chave, out bool atravessa))
            {
                bool urgente = order.push && m.RoundLeft < 30 || order.defuse || order.plant && me.hasBomb && m.RoundLeft < 25;
                atravessa = rng.Rand() < 0.2f + 0.5f * P.Agr(me.skill.aggression) + (urgente ? 0.35f : 0);
                decisaoSmoke[chave] = atravessa;
                Think(atravessa ? "atravessando a smoke pra pegar de surpresa" : "não vou atravessar a smoke — espero ela abrir");
                esperaDesde = t;
            }
            if (atravessa) return false;
            if (t - esperaDesde > 7) { decisaoSmoke.Remove(chave); return false; }   // cansou de esperar: decide de novo
            Esperando(null); fase = "esperando a smoke abrir"; return true;
        }
        readonly Dictionary<string, bool> decisaoSmoke = new Dictionary<string, bool>(); float esperaDesde;

        /// <summary>Vai pra um ponto que está no caminho até o ângulo (usa o campo de distância do ângulo, que já está em
        /// cache, em vez de calcular um caminho novo pra um ponto qualquer).</summary>
        void SegueRumo(V2 alvo, float spd)
        {
            if (NavGrid.Direct(me.pos, alvo)) { Ir(alvo, spd); return; }
            MoveTo(microRumo, spd > me.MaxSpeed * 0.6f);
            if (wantVel.Len > 0.1f) wantVel = wantVel / wantVel.Len * spd;
        }

        /// <summary>Ponto a d unidades de a no caminho até L.</summary>
        V2 PontoNoCaminho(V2 a, V2 L, float d)
        {
            var p = NavGrid.Path(a, L, Perigo.Camada(me.team));
            if (p == null || p.Count < 2) return a;
            V2 prev = a; float acc = 0;
            for (int i = 1; i < p.Count; i++)
            {
                if (p[i].link != NavGrid.Link.Walk) return prev;   // não pula/cai/sobe escada num peek
                float seg = (p[i].p - prev).Len;
                if (acc + seg >= d) return prev + (p[i].p - prev) * ((d - acc) / seg);
                acc += seg; prev = p[i].p;
            }
            return prev;
        }

        /// <summary>Outro lugar pra segurar o mesmo ângulo perto da posição (andando reto, sem caminho novo).</summary>
        bool NovoAngulo(V2? L, out V2 q)
        {
            V2 o = order.hold?.pos ?? ancora;
            float zL = L != null ? NavGrid.ZAt(L.Value) : 0;
            for (int k = 0; k < 24; k++)
            {
                q = o + V2.Ang(rng.Uniform(0, 6.283f)) * rng.Uniform(15, 80);
                if (!NavGrid.Movable(q) || (q - ancora).Len < 22 || !NavGrid.Direct(me.pos, q)) continue;
                if (L != null && !MapData.LosWalk(q, NavGrid.ZAt(q), L.Value, zL, 18f)) continue;
                bool colado = false;
                foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - q).Len < 25) colado = true;
                if (!colado) return true;
            }
            q = me.pos; return false;
        }

        // ================================================================ andar e limpar ângulo
        public void MoveTo(V2 goal, bool run)
        {
            me.scoped = false;
            // perto do inimigo anda de shift em qualquer deslocamento — corre só no que é urgente (duelo, troca, ajudar,
            // fogo, bomb pra explodir) ou quando a ordem tem pressa (Silencio diz)
            if (run && target == null && fase != "indo trocar" && fase != "ajudando no duelo" && fase != "saindo do fogo" && fase != "saindo do bomb"
                && fase != "round ganho" && Silencio(order)) run = false;
            // recalcula se o destino mudou, se saiu do caminho (empurrado, desviou) ou de vez em quando
            bool fora = path != null && wp < path.Count && (path[Math.Max(0, wp - 1)].p - me.pos).Len > 60 && (path[wp].p - me.pos).Len > 60;
            if (path == null || (goal - pathGoal).Len > 8 || fora || m.t - pathAt > 5f)
            {
                long pc = P0();
                path = NavGrid.Path(me.pos, goal, Perigo.Camada(me.team)); wp = 1; pathGoal = goal; pathAt = m.t;   // evita cruzar na mira
                // destino fora da navmesh (em cima de borda/caixa): vai pro ponto andável mais perto dele
                if ((path == null || path.Count < 2) && (goal - me.pos).Len > 12) { path = NavGrid.Path(me.pos, NavGrid.Snap(goal), Perigo.Camada(me.team)); if (path.Count < 2) path = NavGrid.Path(me.pos, NavGrid.Snap(goal)); }
                P1("caminho (Path)", pc);
            }
            if ((path == null || path.Count < 2) && (goal - me.pos).Len > 12) { me.semCaminho += Match.DT; Think($"não acho caminho até {Region(goal)}"); return; }
            // já está na célula do destino (caminho de 1 ponto): vai reto até o ponto (antes parava a 8-12 do bomb e nunca desarmava)
            if (path == null || path.Count < 2) { V2 dd = goal - me.pos; float ll = dd.Len; if (ll > 0.8f) { wantVel = dd / ll * me.MaxSpeed * (run ? 1f : 0.52f) * MathF.Min(1, ll / 6 + 0.3f); } return; }
            if (fase == "ordem") fase = "andando";
            // só pula o ponto se chegou nele, ou se dá pra ir reto DAQUI até o próximo (senão corta a quina e trava na parede)
            // ponto de pulo/queda só conta quando os pés estão na altura dele (senão pula o degrau da carroça e fica
            // empurrando a parede de 30 pra chegar no último ponto)
            while (wp < path.Count - 1 && MathF.Abs(path[wp].z - me.z) < 5 && !me.noAr
                   && ((path[wp].p - me.pos).Len < 2.5f || (path[wp].p - me.pos).Len < 8 && NavGrid.Direct(me.pos, path[wp + 1].p))) wp++;
            // destravar: quer andar e não sai do lugar há 0,8 s → recalcula o caminho a partir de onde está
            if (me.Speed < 4) { if (m.t - semAndarDesde > 0.8f && m.t - pathAt > 0.8f) { pathAt = -9; semAndarDesde = m.t; desenroscaAte = m.t + 0.35f; } }
            else semAndarDesde = m.t;
            V2 to = path[wp].p - me.pos; float l = to.Len;
            // ponto de queda/pulo logo abaixo/acima (no chão já cheguei, a altura não): segue na direção do trecho
            // seguinte — pisa pra fora da beira (cai) ou empurra a caixa (pula). Antes ficava parado na beira
            if (l < 4 && MathF.Abs(path[wp].z - me.z) >= 5 && !me.noAr)
            {
                if (l > 0.3f) { to = to / l * 6; l = 6; }   // vai direto no ponto (passo firme, não para na beira)
                else
                {
                    V2 ref_ = wp + 1 < path.Count ? path[wp + 1].p : path[wp].p;
                    V2 seg = ref_ - (wp > 0 ? path[wp - 1].p : me.pos);
                    if (seg.Len > 0.5f) { to = seg / seg.Len * 6; l = 6; }
                }
                // degrau acima e perto: pula já (escadinha de caixas: andar até encostar fazia cair pela borda)
                float dz = path[wp].z - me.z;
                if (dz > 4 && dz <= NavGrid.JUMP + 0.5f) me.pularPara = path[wp].z;
            }
            // travado mesmo assim: um passo pro lado livre que mais aproxima do ponto do caminho
            if (m.t < desenroscaAte)
            {
                V2 melhor = to; float bd = float.MaxValue;
                for (int k = 0; k < 8; k++)
                {
                    V2 q = me.pos + V2.Ang(k * MathF.PI / 4) * 5;
                    if (!NavGrid.Movable(q)) continue;
                    float dd = (path[wp].p - q).Len; if (dd < bd) { bd = dd; melhor = q - me.pos; }
                }
                to = melhor; l = to.Len;
            }
            if (l < 1) return;
            // molotov no caminho: espera
            if (m.FireAt(path[wp].p) && !m.FireAt(me.pos)) { fase = "esperando a molotov"; Think("esperando a molotov apagar"); return; }
            float spd = me.MaxSpeed * (run ? 1f : 0.52f);
            if (l < 10 && wp == path.Count - 1) spd *= l / 10;
            wantVel = to / l * spd;
            me.move = run ? Move.Correndo : Move.Andando;
        }

        /// <summary>
        /// Mira enquanto anda: escolhe o "pixel" mais perigoso à vista (cantinho/caixa/posição com calor alto, perto,
        /// na frente do caminho) e deixa a mira nele na altura da cabeça. Quando o ângulo aparece pela primeira vez e é
        /// lugar comum de inimigo, o entry dá pre-fire.
        /// </summary>
        void ClearAngles(bool aggressive)
        {
            var intel = m.intel[(int)me.team];
            if (m.t - spotsAt > 0.25f)
            {
                long pc = P0();
                spotsAt = m.t; nearSpots.Clear();
                foreach (var s in Spots.All())
                {
                    float d = (s.p - me.pos).Len;
                    if (d > 420 || d < 25) continue;
                    if (intel.HeatAt(s.p) < 0.12f) continue;
                    if (!MapData.LosWalk(me.pos, me.z, s.p, s.z, 18f)) { wasVisible.Remove(Key(s)); continue; }
                    nearSpots.Add(s);
                }
                float bestS = 0; Spots.Spot? pick = null;
                V2 fwd = path != null && wp < path.Count ? (path[wp].p - me.pos) : V2.Ang(me.aim);
                float fl = fwd.Len; if (fl > 0.1f) fwd = fwd / fl;
                // canto cego (off-angle): lugar que quase ninguém joga. Correndo, gente só confere de vez em quando — e
                // quando não confere passa reto e pode morrer de costas. Andando no shift/limpando com calma confere tudo
                var inim = me.team == Team.T ? Team.CT : Team.T; var L = m.leitura[(int)me.team];
                float maxComum = 0; foreach (var s in nearSpots) maxComum = MathF.Max(maxComum, Rotas.Perto(inim, s.p, m.t, 60));
                bool correndo = me.move == Move.Correndo && !EXP.Contains("semCantoCego");
                foreach (var s in nearSpots)
                {
                    V2 d = s.p - me.pos; float dl = d.Len;
                    int key = Key(s);
                    bool newly = wasVisible.Add(key);
                    bool lembra = L != null && !EXP.Contains("semMemoria") && L.Lembra(s.p, me.team, 45);   // morremos daqui há pouco
                    if (newly)
                    {
                        ignorado.Remove(key);
                        bool raro = maxComum > 0 && Rotas.Perto(inim, s.p, m.t, 60) < 0.15f * maxComum;
                        if (correndo && raro && !lembra && rng.Rand() > 0.2f + 0.2f * me.skill.crosshair) { ignorado.Add(key); me.cantoIgnorado++; }
                    }
                    if (ignorado.Contains(key)) continue;
                    float front = (d / dl).Dot(fwd);
                    float sc = intel.HeatAt(s.p) * (1.4f - dl / 420f) * (0.6f + 0.6f * MathF.Max(0, front)) * (lembra ? 1.8f : 1f);
                    if (sc > bestS) { bestS = sc; pick = s; }
                    // pre-fire: ângulo apareceu agora, calor alto, perto, canto comum — ou o pixel de onde mataram a gente
                    // no round passado (dedução, não wallhack: só se ainda pode ter alguém ali)
                    bool porMemoria = lembra && intel.HeatAt(s.p) > 0.15f && rng.Rand() < 0.45f + 0.35f * P.Agr(me.skill.aggression);
                    if (newly && aggressive && !prefired.Contains(key) && dl < (porMemoria ? 320 : 260) && s.kind != Spots.Kind.EmCima && me.gunId != "awp" && me.gun.CanFire(m.t) &&
                        (porMemoria || intel.HeatAt(s.p) > 0.55f && (me.role == Role.Entry || me.skill.aggression > 0.65f)))
                    {
                        prefired.Add(key);
                        if (porMemoria) me.prefireMemoria++;
                        Think(porMemoria ? $"pre-fire no pixel de onde mataram a gente ({Region(s.p)})" : $"pre-fire no {(s.kind == Spots.Kind.Canto ? "cantinho" : "atrás da caixa")} ({Region(s.p)})");
                        for (int k = 0; k < 2; k++) m.Shoot(me, s.p, s.z + 21.5f);
                    }
                }
                if (pick != null)
                {
                    var s = pick.Value;
                    if (focus == null || (focus.Value - s.p).Len > 2) { focusT = m.t; focusName = s.kind == Spots.Kind.Canto ? "cantinho" : s.kind == Spots.Kind.AtrasCaixa ? "atrás da caixa" : "em cima da caixa"; }
                    focus = s.p;
                    Think($"checando {focusName} ({Region(s.p)})", $"lugar mais perigoso à vista: calor {intel.HeatAt(s.p):0.00} a {(s.p - me.pos).Len:0} de mim");
                }
                else focus = null;
                P1("limpar ângulos (spots)", pc);
            }
            var call = Chamada(500) ?? FocoMorte();   // call do time (ou de onde mataram o colega) vale mais que ângulo genérico
            miraFonte = call != null ? "call" : focus != null ? "cantinho" : "frente";
            bool cantinhoAntes = EXP.Contains("cantinhoAntes");
            if (call != null && (call.Value - me.pos).Len > 280 && Borda() is V2 bq && bordaQuina != null && bordaQuina.dp < 220 && bordaRisco > 0.05f
                && (bq - me.pos).Dot(path != null && wp < path.Count ? path[wp].p - me.pos : V2.Ang(me.aim)) > 0) call = null;
            if (call != null) { Borda(); V2 pc2 = Pixel(call.Value); Reservar(call.Value, pc2); Face(pc2, 3); AbrirPixel(pc2); }
            else if (focus != null && cantinhoAntes) Face(focus.Value, 3);
            else if (EXP.Contains("miraCaminho")) { if (path != null && wp < path.Count) Face(path[Math.Min(wp + 1, path.Count - 1)].p, 3); }
            else
            {   // mira na quina por onde o inimigo apareceria (borda de parede com gente possível atrás); senão o cantinho
                // quente à vista; senão de onde ele vem (calor); senão a borda do meu próprio caminho. Nunca o waypoint atrás da parede
                V2? q = null;
                if (!EXP.Contains("semBorda")) { q = Borda(); if (q != null) miraFonte = "borda"; }
                if (q == null && focus != null) { q = focus; miraFonte = "cantinho"; }
                if (q == null)
                {
                    var vem = DeOndeVem();
                    bool calor = vem != null && m.intel[(int)me.team].HeatAvg(vem.Value, 90) > 0.05f;
                    if (calor) miraFonte = "calor";
                    q = calor ? Pixel(vem.Value) : FrenteNoCaminho();
                }
                if (q != null) { Face(q.Value, 3); Reservar(q.Value); }
                AbrirPixel(q);
            }
            fonteMira = miraFonte;
        }

        /// <summary>
        /// Crosshair placement: a borda de parede que pode revelar alguém. Joga raios em volta; onde a visão pula de perto
        /// pra longe tem uma quina, e quem estiver escondido atrás dela aparece exatamente ali (no ângulo da borda), seja
        /// onde for que ele esteja lá atrás. Cada quina vale o quanto pode ter inimigo atrás (calor + rotas) x estar na
        /// frente de onde eu ando x estar perto (abre antes). Mira colada na borda, do lado aberto.
        /// </summary>
        V2? Borda()
        {
            if (m.t - bordaAt < 0.35f && (me.pos - bordaMeu).Len < 14) return BordaExata();
            long pc = P0();
            bordaAt = m.t; bordaMeu = me.pos;
            V2 fw = path != null && wp < path.Count ? path[wp].p - me.pos : V2.Ang(me.aim); float fwl = fw.Len; fw = fwl > 0.1f ? fw / fwl : V2.Ang(me.aim);
            float best = 0; V2? res = null; Pixels.Quina esc = null; float escTh = 0;
            foreach (var q in Pixels.Em(me.pos))
            {
                // atrás da quina: pode ter gente agora e eles costumam passar por ali? (geometria pura sem isso piora)
                float th = 0;
                foreach (var a in q.atras) th = MathF.Max(th, Ameaca(a));
                if (th < 0.008f) continue;
                V2 dir = (q.ponto - me.pos) / MathF.Max(1, (q.ponto - me.pos).Len);
                float frente = dir.Dot(fw), pf = frente > 0.35f ? 1f : frente > -0.3f ? 0.6f : 0.2f;
                float sc = th * pf * (1.3f - q.dp / Pixels.MAX) * Costume(q.ponto);
                if (bordaQuina != null && m.t < bordaTrava && (q.ponto - bordaQuina.ponto).Len < 40) sc *= 6f;   // termina de checar antes de trocar de quina
                if (frente < 0.35f && (Lotado(q.ponto) || CobertoPor(q.ponto) != null)) sc *= 0.4f;   // o time já tem essa: prefere a que ninguém olha
                if (bordaRes != null && (q.ponto - bordaRes.Value).Len < 40) sc *= 1.4f;   // não fica pulando entre duas quinas
                if (sc > best) { best = sc; res = q.ponto; esc = q; escTh = th; }
            }
            if (PicoCostume(fw, ref best)) { res = picoPonto; esc = null; escTh = picoTh; }
            if (esc != bordaQuina && esc != null) bordaTrava = m.t + 0.45f;
            bordaRes = res; bordaQuina = esc; bordaRisco = escTh; exatoAt = -9;
            P1("borda (crosshair placement)", pc);
            return BordaExata();
        }

        /// <summary>A borda escolhida, medida daqui (recalcula a cada passinho: andando o ângulo da quina muda).</summary>
        V2? BordaExata()
        {
            if (bordaQuina == null || EXP.Contains("semExato")) return bordaRes;
            if (m.t - exatoAt < 0.1f && (me.pos - exatoMeu).Len < 3) return exatoRes;
            exatoAt = m.t; exatoMeu = me.pos; exatoRes = Pixels.Exato(bordaQuina, me.pos, me.z);
            return exatoRes;
        }
        float exatoAt = -9; V2 exatoMeu, exatoRes;
        float bordaTrava = -9;
        float bordaAt = -9, bordaRisco; V2 bordaMeu; V2? bordaRes; Pixels.Quina bordaQuina;

        /// <summary>
        /// Abrir pixel: ninguém anda reto pra dentro de um ângulo que não checou. Tem quina à frente com perigo atrás: enquanto
        /// a mira não chegou nela, o passo cai; com a mira lá, anda de shift abrindo largo (longe da parede, fatiando o
        /// ângulo). Vale pra qualquer lugar do mapa e qualquer ordem; só a pressa de verdade (relógio, troca, defuse) corre.
        /// </summary>
        void AbrirPixel(V2? mira)
        {
            var q = bordaQuina; if (mira == null || EXP.Contains("semAbrir")) return;
            float d = (mira.Value - me.pos).Len;
            if (wantVel.Len < 1 || d < 1) return;
            // o risco é o que a gente espera ter por perto, não só o valor da quina escolhida
            if (bordaRisco < 0.03f && m.intel[(int)me.team].Esperados(me.pos, 350) < 0.25f) return;
            float err = MathF.Abs(MathX.AngDiff((mira.Value - me.pos).Atan2, me.aim));
            bool pressa = m.t < tradeUntil || m.RoundLeft < 20 || order.defuse || order.plant || m.bombPlanted && m.BombLeft < 14;
            m.Ev(err > 0.45f ? "pixel à frente sem checar (amostras)/round" : "pixel à frente checado (amostras)/round");
            if (pressa) return;
            float k = err > 0.35f ? 0.3f : 0.6f;
            V2 v = wantVel * k;
            if (q != null && q.dp < 170 && err <= 0.35f)
            {   // abre largo: passo de lado, pra longe da parede que esconde o ângulo
                V2 lado = V2.Ang((mira.Value - me.pos).Atan2 + q.lado * MathF.PI / 2);
                if (NavGrid.Movable(me.pos + lado * 12)) v = v + lado * me.MaxSpeed * 0.22f;
            }
            wantVel = v; me.move = Move.Andando; abrirT = m.t;
        }

        /// <summary>A quina do meu caminho: o último ponto dele que eu ainda vejo antes de sumir atrás da parede (até 700).
        /// Chegando na quina ela vai abrindo sozinha (a borda anda pro lado de lá) — é o fatiar do ângulo.</summary>
        V2? FrenteNoCaminho()
        {
            if (path == null || wp >= path.Count) return null;
            if (m.t - frenteAt < 0.2f && (me.pos - frenteMeu).Len < 12) return frenteRes;
            frenteAt = m.t; frenteMeu = me.pos; frenteRes = null;
            V2 prev = me.pos; float tot = 0;
            for (int i = wp; i < path.Count && tot < 700; i++)
            {
                float seg = (path[i].p - prev).Len;
                for (float s = 8; s <= seg && tot + s < 700; s += 8)
                {
                    V2 q = prev + (path[i].p - prev) * (s / seg);
                    if (MapData.LosWalk(me.pos, me.z, q, NavGrid.ZAt(q), 18f)) frenteRes = q;
                    else if ((q - me.pos).Len > 40 && frenteRes != null) return frenteRes;
                }
                tot += seg; prev = path[i].p;
            }
            return frenteRes ?? path[Math.Min(wp + 1, path.Count - 1)].p;
        }
        float frenteAt = -9; V2 frenteMeu; V2? frenteRes;

        static int Key(Spots.Spot s) => (int)s.p.x * 10000 + (int)s.p.y;

        void Face(V2 p, float pitch) { V2 d = p - me.pos; if (d.Len < 0.5f) return; wantAim = d.Atan2; hasWantAim = true; wantPitch = pitch; }

        /// <summary>
        /// Gira a mira. No duelo: flick na velocidade do jogador. Reagindo (levou tiro, perdeu o inimigo de vista agora):
        /// rápido, mas não tanto. Checando ângulo com calma: ~300°/s e desacelerando ao chegar (mão de gente, não
        /// teleporte de mira).
        /// </summary>
        public void TurnAim(float dt)
        {
            if (!hasWantAim) return;
            float diff = MathX.AngDiff(wantAim, me.aim);
            bool duelo = target != null, reagindo = m.t < levouAte || m.t - lastSeenT < 1.6f || EXP.Contains("miraRapida");
            float vmax = duelo ? me.skill.flick : reagindo ? me.skill.flick * 0.6f : 320f;   // graus/s
            float v = duelo ? vmax : MathF.Min(vmax, 40f + 6f * MathF.Abs(diff) * 180 / MathF.PI);   // freia chegando
            float maxd = v * MathF.PI / 180 * dt;
            me.aim += MathF.Abs(diff) <= maxd ? diff : MathF.Sign(diff) * maxd;
            me.aimPitch = wantPitch;
        }
    }
}
