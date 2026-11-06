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
        public bool doPlano;                                 // ordem da rota do banco (ou que saiu dela): o IGL pode seguir a rota depois
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
        float agachaSprayAte = -9, calmoLog = -9; V2 tradeMira; float c4DentroDesde = -1; bool c4FoiDentro; float plantaJaLog = -9;
        int burst; float errV0, errVSalto, errVSaltoT, agachaPeekAte = -9; bool alvoAgachado;
        public bool AgachouNoPeek => m.t < agachaPeekAte + 1.5f && agachaPeekAte > 0;
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
        public string action; public float actionEnd; public Lineup actionNade;
        public float ActionEnd => actionEnd;   // (vídeo: progresso da animação de plantar/desarmar/granada)
        /// <summary>
        /// (visual) Granada na mão: o jeito que ela VAI sair (mesmo cálculo do arremesso, sem o erro de mira) e pra onde
        /// ele está mirando. A ação "granada" inteira é a fase de alinhar a mira no ponto; ela sai da mão em ActionEnd.
        /// </summary>
        public (Arremesso.Tipo tipo, float forca, float up, float yaw, V2 alvo, bool sabe)? PlanoGranada
        {
            get
            {
                if (action != "granada" || actionNade == null) return null;
                if (planoDe == actionNade && planoCache.HasValue) return planoCache;
                bool sabe = Places.Treinada(actionNade) && ConheceLineup(actionNade);
                var pl = Arremesso.Resolve(me.pos, me.z, actionNade.to, actionNade.kind, sabe ? Arremesso.Tipo.CorrePulo : Arremesso.Tipo.Correndo);
                planoDe = actionNade; planoCache = (pl.tipo, pl.forca, pl.up, pl.yaw, actionNade.to, sabe);
                return planoCache;
            }
        }
        bool esperouBang;
        float recargaAte = -9, recargaProx; V2 recargaPonto, recargaDe; bool recargaPula;
        /// <summary>
        /// Recarregar como pro: com pouca bala e sem ninguém na mira, não recarrega parado no aberto — se o lugar está
        /// exposto pra onde o inimigo deve aparecer, primeiro sai da linha (passo pra trás da quina, às vezes com pulo) e
        /// recarrega coberto. Quanto mais vazio o pente e mais perigo, mais ele quer recarregar já; com o pente meio cheio
        /// e perigo perto, segura (recarregar é ficar 2-3 s sem atirar). Pelo que ele acha, não pelo que é.
        /// </summary>
        void RecargaPro(float t)
        {
            if (me.gun.Reloading || t < recargaProx || action != null) return;
            float frac = me.gun.inMag / (float)me.gun.s.mag; if (frac >= 0.5f) return;
            recargaProx = t + 0.4f;
            var it = m.intel[(int)me.team];
            float perigo = it.HeatIn(me.pos, 260) + it.Esperados(me.pos, 450);
            // quer recarregar: pente vazio pesa muito; perigo perto pesa contra (recarregar agora é ficar sem arma)
            float quer = (1 - frac) * 1.6f - perigo * (frac > 0.2f ? 0.5f : 0.15f);
            if (quer < 0.6f) return;
            V2 de = DeOndeAparece();
            bool exposto = MapData.LosWalk(de, NavGrid.ZAt(NavGrid.Snap(de)), me.pos, me.z, 18f) && (de - me.pos).Len < 900 && perigo > 0.25f;
            if (exposto && recargaAte < t)
            {   // ponto coberto perto, pra trás da linha de onde ele vem
                V2 fora = me.pos - de; float lf = fora.Len; if (lf > 1) fora = fora / lf;
                foreach (float r in new[] { 25f, 40f, 60f })
                    foreach (float ang in new[] { 0.9f, -0.9f, 0.4f, -0.4f, 1.4f, -1.4f })
                    {
                        V2 q = NavGrid.Snap(me.pos + V2.Ang(fora.Atan2 + ang) * r);
                        if (!NavGrid.Movable(q) || !NavGrid.Direct(me.pos, q)) continue;
                        if (MapData.LosWalk(de, NavGrid.ZAt(NavGrid.Snap(de)), q, NavGrid.ZAt(q), 18f)) continue;
                        recargaPonto = q; recargaDe = de; recargaAte = t + 1.6f;
                        recargaPula = rng.Rand() < 0.08f + 0.2f * P.Agr(me.skill.aggression);
                        m.Ev("recarga: saiu da linha antes/round"); Think("pouca bala — saio da linha pra recarregar"); return;
                    }
            }
            if (exposto && t < recargaAte) return;   // ainda indo pra cobertura
            if (me.gun.StartReload()) { m.Ev(exposto ? "recarga: recarregou exposto/round" : "recarga: recarregou coberto/round"); Think(exposto ? "recarregando aqui mesmo (não achei cobertura)" : "recarregando coberto"); }
        }
        /// <summary>
        /// O corpo faz o jeito do arremesso: parado e pulo param (mira alinhada), andando/correndo dão o passo na direção
        /// do arremesso só no fim (o embalo de ~0,3 s antes de soltar), e o pulo sai no tique em que a granada sai da mão.
        /// </summary>
        void MovimentoDoArremesso(float t)
        {
            var pg = PlanoGranada; if (pg == null) return;
            var (tipo, _, _, yaw, _, _) = pg.Value;
            float falta = actionEnd - t;
            if ((tipo == Arremesso.Tipo.Andando || tipo == Arremesso.Tipo.Correndo || tipo == Arremesso.Tipo.CorrePulo) && falta < (EXP.Contains("embaloLongo") ? 0.32f : 0.15f))
                wantVel = V2.Ang(yaw) * me.MaxSpeed * (tipo == Arremesso.Tipo.Andando ? 0.52f : 1f);
            if ((tipo == Arremesso.Tipo.Pulo || tipo == Arremesso.Tipo.CorrePulo) && falta < Match.DT * 1.5f && !me.noAr)
            {   // pulo do jump throw: o Match só deixa pular "de deslocamento" já correndo; parado é o pulo seco
                if (tipo == Arremesso.Tipo.CorrePulo) me.querPular = true;
                else { me.noAr = true; me.vz = Match.PULO; me.quedaDe = me.z; }
                m.Ev($"granada: pulou no arremesso ({Arremesso.Nome(tipo)})/round");
            }
        }
        Lineup planoDe; (Arremesso.Tipo tipo, float forca, float up, float yaw, V2 alvo, bool sabe)? planoCache;
        public string AcaoGranada => action == "granada" ? actionNade?.kind : null;   // (visual: qual granada está na mão)
        int holdLook; float holdLookUntil;
        public float tradeUntil = -9; V2 tradePos; Agent tradeMatador; bool tradeViu, tradeFlash; float trocaAudAte; int trocaTicks, trocaJuizTicks;
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
        float levouAte = -9; V2 levouDe; V2? levouAbrir;
        public void Atingido(Agent by)
        {
            if (by == null || by.team == me.team && !EXP.Contains("tiroAmigoConta")) return;   // bala do colega sem querer: não vira pra ele
            // já duelando com ele: o tiro não é surpresa, mas se eu perder ele de vista, o lugar do tiro é o que vale olhar
            if (target == by) { if (!EXP.Contains("levouVelho") && me.alive) { levouDe = by.pos; levouAte = m.t + rng.Uniform(1.2f, 2.2f); } return; }
            if (m.t - lastSeenT > 1.5f) m.aparecidos.Add((me.team, me.pos, m.t, m.bombPlanted, (by.pos - me.pos).Atan2));   // pegou desprevenido: também conta como de onde ele aparece
            if (!me.alive) return;
            levouDe = by.pos + V2.Ang(rng.Uniform(0, 6.2832f)) * rng.Uniform(0, 20);
            // de onde veio o tiro vira o lugar mais importante por um tempo: inteiro, segura a linha esperando ele
            // reaparecer; machucado, tende a sair da linha (decide na hora, no "levei tiro")
            // sem ter visto quem atirou, ninguém volta a andar 1 s depois: segura o lado do tiro uns 2-3 s (ou sai da linha)
            levouAte = m.t + (EXP.Contains("levouVelho") ? rng.Uniform(0.6f, 1f) : EXP.Contains("reacaoCurta") ? rng.Uniform(1.2f, 2.6f) : rng.Uniform(2f, 3f));
            levouSair = !EXP.Contains("levouVelho") && rng.Rand() < (me.hp < 35 ? 0.75f : me.hp < 65 ? 0.4f : 0.12f) * (1.3f - 0.6f * P.Agr(me.skill.aggression));
            /* bx: visão do corpo — ele me acertou, eu JÁ estou olhando pra onde veio e não vejo nada dele: ele vê um pedaço
               de mim que eu não uso. Ficar parado ali é perder (era o que mais morria com a visão do corpo). Pela situação:
               machucado/calmo sai da linha; inteiro/agressivo abre pro lado que mostra ele pra duelar de igual pra igual. */
            levouAbrir = null;
            if (VisaoCorpo && !EXP.Contains("bxLevouVistoVelho") && MathF.Abs(MathX.AngDiff((levouDe - me.pos).Atan2, me.aim)) < 0.5f)
            {
                float agr = P.Agr(me.skill.aggression);
                float pSai = MathX.Clamp(0.25f + 0.6f * (1 - me.hp / 100f) - 0.35f * agr + (order.hold != null ? -0.1f : 0.1f), 0.05f, 0.95f);
                if (rng.Rand() < pSai) levouSair = true;
                else
                {
                    V2 d = levouDe - me.pos; float dl = MathF.Max(1, d.Len); V2 lat = new V2(-d.y / dl, d.x / dl); float zl = NavGrid.ZAt(NavGrid.Snap(levouDe));
                    float s1 = rng.Rand() < 0.5f ? 1f : -1f;
                    foreach (float sg in new[] { s1, -s1 })
                    {
                        V2 q = NavGrid.Snap(me.pos + lat * sg * 22f);
                        if (NavGrid.Movable(q) && MapData.LosWalk(q, NavGrid.ZAt(q) + me.Eye - 18f, levouDe, zl + me.Eye - 18f, 18f)) { levouAbrir = q; break; }
                    }
                    if (levouAbrir == null) levouSair = true;
                }
                if (Match.Auditar) m.Aud("levei tiro sem ver, já olhando pra lá", levouSair ? "saí da linha" : "abri pra ver ele");
            }
            if (action == "granada") { Think("levei tiro — larguei a granada"); action = null; if (!EXP.Contains("sacaGranadaVelho")) me.sacaAte = MathF.Max(me.sacaAte, m.t + me.TempoSacar); }
            GuardaGranada(m.t, "levei tiro com a granada na mão");   // estava com a granada na mão: saca a arma
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
        public void Ouviu(V2 p, float t, string kind)
        {
            // um som novo não apaga o que acabei de ouvir mais perto (tiro longe por cima do passo do lado); -exp somVelho:
            // o último som sempre
            if (!EXP.Contains("somVelho") && t - somT < 1.2f && (p - me.pos).Len > (somP - me.pos).Len + 40) return;
            somP = p; somT = t; somK = kind;
        }
        /// <summary>Quanto tempo o som ainda vira a mira: perto lembra mais (~4 s colado, ~2 s a 400); -exp somVelho: 2 s.</summary>
        float SomVale => EXP.Contains("somVelho") ? 2f : 2f + 2f * MathF.Max(0, 1 - (somP - me.pos).Len / 400f);
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
            // o ponto bom é bom pra todo mundo: sem isso quem vai pra mesma região acaba colado no mesmo pixel (o melhor
            // canto é de quem chegou primeiro; o resto fica a uns 50 dele)
            var colegas = new List<V2>();
            if (!EXP.Contains("pontoColado"))
                foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && x.brain.order.hold != null) colegas.Add(x.brain.order.hold.pos);
            bool colado(V2 q) { foreach (var c in colegas) if ((c - q).Len < 50) return true; return false; }
            float atual = colado(cen) ? 99 : Exposicao(cen, t), melhor = atual - 0.5f; V2? res = null;
            for (int k = 0; k < 28; k++)
            {
                V2 q = NavGrid.Snap(h.pos + V2.Ang(k * 2.4f) * (14 + 62 * (k + 1) / 28f));   // espiral: cobre o entorno sem sorteio
                if (!NavGrid.Movable(q) || MathF.Abs(NavGrid.ZAt(q) - z0) > 6 || colado(q) || !MapData.LosWalk(q, NavGrid.ZAt(q), alvo, zAlvo, 18f)) continue;
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
            if (o.why != "acabou a jogada, voltando") m.sus.Ordem(me, o.name);   // desempilhar ordens vencidas não é ordem nova
            m.Aud("ordem trocada", m.t - ordemDesde < 3 ? "menos de 3 s depois da anterior" : "normal"); ordemDesde = m.t;
            if (!me.IsT && o.hold != null && o.hold != CT_Janelao && EXP.Contains("holdVaria")) o.hold = HoldVariado(o.hold);
            if (o.hold != null && o.hold != CT_Janelao && !o.hold.name.StartsWith("segurando") && !o.hold.name.StartsWith("C4") && !EXP.Contains("semUmAngulo")) o.hold = HoldPoucoExposto(o.hold);
            if (o.hold != null && o.hold != CT_Janelao && !o.hold.name.StartsWith("segurando") && !o.hold.name.StartsWith("C4")) o.hold = HoldNaoQueimado(o.hold);
            /* ordem só com hold: o dest fica no V2 zero e qualquer ramo que usa o.dest manda pro canto do mapa — o dest é o hold */
            if (o.dest.Len < 1 && !EXP.Contains("apOrdemDestVelho"))
            {   if (Match.Auditar) m.Aud("ordem sem dest (chegou assim)", o.hold != null ? "só hold → dest = hold" : o.follow != null ? "segue alguém" : "sem alvo nenhum · " + o.name);
                if (o.hold != null) o.dest = o.hold.pos; }
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
            // na luneta da AWP a tela é só o zoom (FOV 40° do CS2): fora dele não vê nada — é o preço de segurar cravado
            if (me.scoped && me.gunId == "awp" && !EXP.Contains("awpLunetaVelha") && da > 20f * MathF.PI / 180 && dist > 30) return false;
            bool centro = MapData.LosWalk(me.pos, me.z + me.Eye - 18f, e.pos, e.z + e.Eye - 18f, 18f) && !m.SmokeBlocks(me.pos, me.z + me.Eye, e.pos, e.z + e.Eye);
            if (!VisaoCorpo) return centro;
            parcial[e.id] = !centro;
            return centro || VeParte(e);
        }

        /* bx: o que se vê é o CORPO (as mesmas caixas do tiro e do boneco da tela: cabeça até 23,4, tronco ±4,6, perna ±3,6),
           não só o ponto do olho no centro — ombro/cabeça saindo da quina aparece. Ver só um pedaço é mais lento de
           reconhecer (ReactionTime) e a mira vai no pedaço que aparece (Fight). -exp bxVisaoCorpoLigado (opt-in até o A/B) */
        public static bool VisaoCorpo => EXP.Contains("bxVisaoCorpoLigado");
        static readonly (float h, float lat)[] PONTAS_CORPO = { (22.5f, 0f), (15f, 4f), (15f, -4f), (4f, 3f), (4f, -3f) };
        public readonly bool[] parcial = new bool[10]; readonly V2[] parteVista = new V2[10]; readonly float[] parteH = new float[10];
        bool VeParte(Agent e)
        {
            V2 d = e.pos - me.pos; float dl = MathF.Max(1, d.Len); V2 lat = new V2(-d.y / dl, d.x / dl); float k = 1f - 0.28f * e.duck;
            foreach (var (h, l) in PONTAS_CORPO)
            {
                V2 p = e.pos + lat * l; float z = e.z + h * k;
                if (MapData.LosWalk(me.pos, me.z + me.Eye, p, z, 0f) && !m.SmokeBlocks(me.pos, me.z + me.Eye, p, z)) { parteVista[e.id] = p; parteH[e.id] = h * k; return true; }
            }
            return false;
        }

        float ReactionTime(Agent e)
        {
            float da = MathF.Abs(MathX.AngDiff((e.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI;
            if (MiraVertical) { float dv = FlickV(e); da = MathF.Sqrt(da * da + dv * dv); }   /* o flick é na diagonal (lado + altura) */
            // achar e corrigir a mira custa caro: pré-mirado (<6°) quase nada; 30° ~0,13 s a mais (antes 0,03 s: segurar
            // ângulo pré-mirado não dava vantagem nenhuma e o CT perdia duelo segurando posição)
            // real (143 partidas pro): com a mira a <2° do inimigo 63% atira na hora; virando >8° a mediana é 0,28 s. A
            // reação "pura" é rápida — o que custa é achar e virar. Só a base é acelerada (o flick e o frear continuam)
            float fBase = TeamBrain.ExpNum("reac", 1f);   // testado 0,55-0,75: não encurtou o duelo (o tempo está em virar/assentar) e o TR subiu
            float r = me.skill.reaction * rng.Uniform(0.85f, 1.25f) * fBase + 0.08f * MathF.Pow(da / 30f, 0.7f) + da / (me.skill.flick * 0.66f);
            if (da < 6) r -= 0.04f;                         // mira já no lugar (crosshair placement)
            // parado e com a mira no lugar o pro atira quase na hora (real: 63% atira até 0,1 s depois de ver quando a mira está a menos de 2°); quem vem andando ainda tem que parar
            if (da < 6 && me.Speed < me.MaxSpeed * 0.2f && !EXP.Contains("semPreTiro")) r *= EXP.Contains("pre75") ? 0.75f : 0.55f;
            if (e.Speed > e.MaxSpeed * 0.6f) r += 0.03f;     // alvo passando correndo é mais difícil
            // vindo correndo de rifle tem que frear antes do tiro: quem já está parado e mirando sai na frente
            if (!me.ArmaLeve && !EXP.Contains("semAssentar")) r += (EXP.Contains("assenta17") ? 0.17f : 0.25f) * MathF.Min(1, me.Speed / me.MaxSpeed);
            if (EXP.Contains("semPanico")) { if (m.ClutchFor(me.team)) r *= 1.15f - 0.15f * me.skill.composure; }
            else r *= 1 + 0.25f * me.panico;   // pânico: demora a achar e decidir
            if (me.MeioCego(m.t)) r += 0.3f * me.flashI * me.flashI;   // ainda vendo branco (fraca: quase nada)
            if (VisaoCorpo && parcial[e.id]) r += 0.07f;   /* bx: só um pedaço do corpo na quina: demora mais pra reconhecer */
            return MathF.Max(EXP.Contains("semPreTiro") ? 0.1f : 0.05f, r);
        }

        // ================================================================ passo
        public void Tick(float dt)
        {
            wantVel = V2.Zero; hasWantAim = false; me.crouch = false;
            if (me.nadeNaMao != null && action != "granada" && (order.nades.Count == 0 || order.nades[0].kind != me.nadeNaMao || !me.nades.Contains(me.nadeNaMao)))
                GuardaGranada(m.t, "a lineup mudou — guardei a granada");
            if (m.t < evasivoPulaAte && me.Speed > me.MaxSpeed * 0.7f && !me.noAr) { me.querPular = true; evasivoPulaAte = -9; }   // pulo da quebra de linha
            float t = m.t; faseAnt = fase; fase = "ordem";
            long ppn = P0(); Panico(t, dt); P1("pânico", ppn);
            // ---- vê
            long pc = P0();
            Agent best = null; float bestScore = float.MaxValue;
            foreach (var e in m.agents)
            {
                if (e.team == me.team || !CanSee(e)) continue;
                m.Spot(me, e);
                // larguei esse duelo de longe (saí da linha): não volto pra ele enquanto saio, a não ser que ele me acerte
                if (e == largouAlvo && t < largouAte && t - me.apanhouEm > 1f) continue;
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
                            { float dvA = FlickV(best); m.Aud("mira no contato: erro vertical (°)", dvA < 2 ? "a <2" : dvA < 6 ? "b 2-6" : dvA < 12 ? "c 6-12" : "d 12+"); if (MathF.Abs(best.z - me.z) > 10) m.Aud("contato com diferença de altura (>10)", (best.z > me.z ? "ele acima · " : "ele abaixo · ") + (dvA < 6 ? "mira na altura" : "mira fora da altura")); }
                            m.Aud("onde o inimigo apareceu", flick0 < 6 ? "na mira" : aq < 8 ? "em OUTRA quina que eu não olhava" : (best.pos - me.pos).Len < 120 ? "colado, sem quina" : "em lugar aberto, sem quina");
                            if (flick0 >= 6 && aq < 8) m.Aud("olhando outra quina porque", $"{fonteMira} / {(target == null ? faseAnt : "")}");
                            if (flick0 >= 6)
                            {   // a quina por onde ele veio: eu achava que tinha perigo ali? (estimativa errada x escolha errada)
                                float thAp = 0;
                                foreach (var q in Pixels.Em(me.pos)) if (MathF.Abs(MathX.AngDiff((q.ponto - me.pos).Atan2, dirE)) * 180 / MathF.PI < 8) foreach (var a2 in q.atras) thAp = MathF.Max(thAp, Ameaca(a2));
                                float calorAp = m.intel[(int)me.team].HeatAvg(best.pos, 90);
                                m.Aud("mira fora: o lugar de onde ele veio eu achava", calorAp < 0.03f && thAp < 0.01f ? "a vazio (estimativa errada)" : thAp < 0.5f * MathF.Max(bordaRisco, 0.01f) ? "b menos perigoso que o que eu olhava" : "c tão perigoso quanto (escolha/rodízio)");
                            }
                            if (flick0 >= 8 && Match.LogContatos)
                            {   // amostra um a um: onde eu estava, pra onde olhava e por quê, de onde ele saiu, o que eu achava de cada lugar
                                var itc = m.intel[(int)me.team]; V2 olho = me.pos + V2.Ang(me.aim) * 120;
                                m.Log($"CONTATO mira {flick0:0}° · {me.name}({(me.IsT ? "TR" : "CT")}) em {Region(me.pos)} [{me.pos.x:0},{me.pos.y:0}] {(Segurando ? "segurando" : "andando")} fase={faseAnt} micro={micro} fonte={fonteMira} ordem={order.name}"
                                    + $" | olhava {Region(olho)} (esperados {itc.Esperados(olho, 150):0.00})"
                                    + $" | {best.name} saiu em {Region(best.pos)} a {(best.pos - me.pos).Len:0} (esperados {itc.Esperados(best.pos, 150):0.00}, calor {itc.HeatAvg(best.pos, 60):0.00}{(best.brain.target == me ? ", ele já me via" : "")})"
                                    + $" | movendo pra {(wantVel.Len > 1 ? Region(me.pos + wantVel / wantVel.Len * 150) : "parado")}"
                                    + $" | posição deles mais perto dele: {DiagPos(best.pos)}");
                            }
                            float atrasoM = MathF.Abs(MathX.AngDiff(wantAim, me.aim)) * 180 / MathF.PI, querM = MathF.Abs(MathX.AngDiff(dirE, wantAim)) * 180 / MathF.PI;
                            if (flick0 >= 6) m.Aud("mira fora: por quê", querM < 6 ? "queria o ponto certo, a mão atrasou" : atrasoM > 15 ? "virando pra outro lugar" : "parado no lugar errado");
                            if (!me.IsT && Segurando)
                            {   // CT segurando ângulo no contato: de onde vinha a mira (fonte) e se acertou o lado
                                m.Aud("CT segurando: mira no contato", (flick0 < 6 ? "a pré-mirado <6°" : flick0 < 25 ? "b perto 6-25°" : "c errado 25°+") + " · " + fonteMira);
                                if (flick0 >= 6) m.Aud("CT segurando: mira fora por quê", querM < 6 ? "a mão atrasou" : atrasoM > 15 ? "virando pra outro lugar" : "parado no lugar errado · " + fonteMira);
                            }
                            { float ex = Exposicao(me.pos, t); m.Aud(Segurando ? "ângulos expostos segurando, no contato" : "ângulos expostos andando, no contato", ex < 2 ? "até 2" : ex < 3.5f ? "2-3,5" : ex < 6 ? "3,5-6" : "6+"); }
                            m.Aud("mira no contato por time", (me.IsT ? "TR " : "CT ") + (flick0 < 2 ? "<2" : flick0 <= 8 ? "2-8" : ">8")); m.Aud("contato por distância", (best.pos - me.pos).Len < 150 ? "perto (<150)" : (best.pos - me.pos).Len < 350 ? "médio" : "longe");
                        }
                    }
                    lastMicroOuFase = micro != Micro.Nada ? micro.ToString() : faseAnt;
                    target = best; reactAt = t + ReactionTime(best); alvoDesde = t; reacao0 = reactAt - t; primeiroTiro = -1; tirosNoAlvo = 0;
                    // jeito de duelar desta vez: ADAD (a maioria dos pros; AWP de luneta não) e agachar no spray (às vezes)
                    // de rifle, perto/médio o pro para uma vez (counter-strafe) e manda o spray; o ADAD entre os tiros é de
                    // quem está de longe dando toque/rajada (ou de sub/pistola). Dançando no spray o duelo durava demais
                    float dAlvo = (best.pos - me.pos).Len;
                    bool rifle = me.gunId != "awp" && !WeaponStats.Pistola(me.gunId) && me.gunId != "mp9" && me.gunId != "mac10";
                    float pAdad = EXP.Contains("adadVelho") || !rifle ? 0.45f + 0.45f * P.Agr(me.skill.aggression) + 0.2f * P.strafeRajada
                                : dAlvo > 220 ? 0.2f + 0.2f * P.Agr(me.skill.aggression)    // de longe: às vezes um passo entre os toques
                                : 0.05f + 0.08f * P.Agr(me.skill.aggression);
                    adadDuelo = me.gunId != "awp" && rng.Rand() < pAdad;
                    // spray agachado (CS2: agachado a arma abre menos): de rifle, médio, mais em quem é calmo; real ~20% dos tiros
                    float agrD = P.Agr(me.skill.aggression); bool awpD = me.gunId == "awp";
                    agachaDuelo = EXP.Contains("agachaVelho") ? rng.Rand() < 0.3f
                        : !awpD && rng.Rand() < (rifle ? 0.17f + 0.15f * (1 - agrD) + (dAlvo > 150 && dAlvo < 420 ? 0.1f : 0) : 0.12f);
                    // agachar no peek: quem aparece pra quem segura o ângulo (mira parada na altura da cabeça em pé) às vezes
                    // agacha na hora — a primeira bala dele passa por cima. Pelo jeito do jogador e pela situação
                    bool peekando = me.Speed > 15 || micro == Micro.Jiggle || micro == Micro.Wide || micro == Micro.Avancar;
                    float pPeek = peekando && !awpD && dAlvo > 60 && dAlvo < 450 ? (0.06f + 0.22f * agrD) * (best.Speed < 8 ? 1.5f : 0.6f) : 0;
                    agachaPeekAte = !EXP.Contains("semAgachaPeek") && rng.Rand() < pPeek ? t + rng.Uniform(0.5f, 1.1f) : -9;
                    if (agachaPeekAte > t) m.Ev("agachou no peek/round");
                    alvoAgachado = false; errVSalto = 0;   // quem aparece, aparece em pé até eu ver o contrário
                    c4ModoAte = -9;   // situação nova: o C4 reavalia já
                    duelModo = 0; duelHpRef = best.hp; duelReval = t + 2.5f; duelHpEu = me.hp; evasivoProx = t + 0.5f; evasivoDecidiu = false;
                    // a mira raramente está na altura exata da cabeça quando ele aparece (mais baixa, em geral); quem já
                    // estava mirando ali (pré-mirado) erra pouco, quem precisa dar flick erra mais; AWP de luneta erra metade
                    float flick = MathX.Clamp((MiraVertical ? MathF.Sqrt(MathF.Pow(MathX.AngDiff((best.pos - me.pos).Atan2, me.aim) * 180 / MathF.PI, 2) + FlickV(best) * FlickV(best)) : MathF.Abs(MathX.AngDiff((best.pos - me.pos).Atan2, me.aim)) * 180 / MathF.PI) / 25f, EXP.Contains("hsVelho") ? 0.2f : 0.5f, 1f);
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
                if (threat && (action == "fake defuse" || action == "fake plant")) { FinishAction(); }
                else if (threat && (action == "plantar" || (action == "desarmar" && m.BombLeft > Match.DefuseTime(me) + 1.5f && !m.SmokeNear(me.pos, 30))))   // dentro da smoke segue desarmando
                { Think($"parei de {action}: inimigo à vista"); if (action == "desarmar") m.ParouDefuse(me); action = null; }
                else if (action == "desarmar" && m.NoFogo(me)) { Think("molotov no bomb — larguei o defuse"); action = null; m.ParouDefuse(me); }
                else if (t >= actionEnd) FinishAction();
                else
                {
                    fase = "ação"; Face(action == "granada" ? actionNade.to : me.pos + V2.Ang(me.aim) * 10, 0);
                    if (action == "granada" && !EXP.Contains("arremessoParado")) MovimentoDoArremesso(t);
                    return;
                }
            }

            // ---- pisou no fogo: sai (pro lado seguro, de preferência pra trás) e segura dali — molotov tira do lugar,
            // não mata quem reage. Plantando/desarmando decide na ação (acima)
            if (action == null && m.NoFogo(me) && !EXP.Contains("semSairFogo"))
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
            // ...mas quem já está atirando em mim decidiu por mim: correr de costas é morrer sem tirar nada dele
            if (me.guardando && t - me.apanhouEm < 0.3f && t >= proxTrocaSave && !EXP.Contains("saveVelho"))
            { proxTrocaSave = t + 3; order.dest = m.igl[(int)me.team].SaveSpotNovo(me); }
            // guardando: o canto vale enquanto ninguém chega nele — de tempos em tempos confere e, se esquentou, vai com
            // calma (andando, sem barulho) pra outro canto, sem cruzar com eles
            if (me.guardando && target == null && t >= proxReavaliaSave && !EXP.Contains("saveFixo"))
            {
                proxReavaliaSave = t + rng.Uniform(2.5f, 4.5f);
                var ig = m.igl[(int)me.team];
                if (ig.SaveRuim(order.dest, me.z))
                {
                    V2 novo = ig.SaveSpotNovo(me);
                    if ((novo - order.dest).Len > 60) { order.dest = novo; order.quiet = true; m.Ev("guardando: trocou de esconderijo/round"); Think($"aqui tá esquentando — indo com calma pra {Region(novo)}"); }
                }
            }
            if (target != null && me.guardando && (target.pos - me.pos).Len > 260 && t - entregouEm > 1.5f && (t - me.apanhouEm > 2.5f || EXP.Contains("saveVelho")))
            { fase = "guardando"; Think($"guardando a arma — não vou no duelo com o {target.name}"); MoveTo(order.dest, true); return; }
            if (target != null && order.malote && (order.dest - me.pos).Len <= 14 && !EXP.Contains("semSegurarTiro") && SegurarTiroMalote(t)) return;
            if (target != null) { fase = "combate"; Fight(target, t); return; }
            if (t < levouAte && action == null)
            {
                fase = "levei tiro";
                // C4 indo plantar: vira pro tiro e segue pro bomb se ainda está inteiro e perto; machucado, segura
                if (me.hasBomb && order.plant && !m.bombPlanted && !EXP.Contains("c4Parado") && (m.PlantSpot - me.pos).Len > 10
                    && me.hp >= 40 && Rola("c4 levou " + (int)(t / 3), (m.PlantSpot - me.pos).Len < 500 ? 0.75f : 0.4f)) { MoveTo(m.PlantSpot, false); fase = "levei tiro"; }
                else if (levouAbrir is V2 ab && !levouSair && !order.plant && !order.defuse)
                {   /* bx: ele me vê e eu não: abro pro lado que mostra ele (visão do corpo) */
                    MoveTo(ab, true); fase = "levei tiro"; Face(levouDe, 3); Think("levei tiro de quem não vejo — abrindo pra ver ele");
                    return;
                }
                else if (levouSair && !order.plant && !order.defuse)
                {   // machucado e sem ver de onde veio: sai da linha de tiro (pra trás, longe do tiro), olhando pra lá
                    V2 away = me.pos - levouDe; float la = away.Len;
                    if (la > 1) { V2 dst = NavGrid.Snap(me.pos + away / la * 50); if (NavGrid.Movable(dst)) MoveTo(dst, true); }
                    fase = "levei tiro"; Face(levouDe, 3); Think("levei tiro e tô machucado — saindo da linha");
                    return;
                }
                Face(levouDe, 3);   // vira pro tiro; parado (counter-strafe) pra poder responder quando achar
                // só termina de reagir depois de olhar pra lá: o tempo sorteado curto acabava no meio da virada e ele voltava a
                // andar com a mira no caminho, sem nunca ter olhado de onde veio
                if (!EXP.Contains("reacaoCurta") && MathF.Abs(MathX.AngDiff((levouDe - me.pos).Atan2, me.aim)) > 0.3f && t - me.apanhouEm < 3.5f)
                    levouAte = MathF.Max(levouAte, t + 0.4f);
                Think("levei tiro — virando pra onde veio");
                return;
            }
            if (lastSeen != null && t - lastSeenT < 1.6f && lastSeen.alive && !(me.hasBomb && order.plant && !EXP.Contains("c4Briga")))
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
            VoltarArma(t);
            FacaOuArma(t);
            LargarC4(t);
            C4NaPonta(t);
            // ---- recarga com calma
            if (!EXP.Contains("recargaVelha")) RecargaPro(t);
            else if (me.gun.inMag < me.gun.s.mag * 0.35f && !me.gun.Reloading && m.intel[(int)me.team].HeatIn(me.pos, 220) < 1.5f && me.gun.StartReload())
                Think("recarregando (ninguém perto)");
            if (t < recargaAte && !me.gun.Reloading && (recargaPonto - me.pos).Len > 6)
            {   // saindo da linha pra recarregar: passo firme até a cobertura (às vezes um pulo — tira a mira de quem pré-mira)
                fase = "saindo da linha pra recarregar"; MoveTo(recargaPonto, true); Face(recargaDe, 3);
                if (recargaPula && me.Speed > me.MaxSpeed * 0.75f) { me.querPular = true; recargaPula = false; m.Ev("recarga: pulou saindo da linha/round"); }
                return;
            }
            if (!EXP.Contains("juizVelho"))
            {
                // sobrevivência e compra vêm antes de tudo (reflexo); o resto disputa no juiz
                if (SairDoBomb(t)) return;
                if (RoundGanho && !order.defuse) { if (LootGranada(t)) return; DeBoa(); return; }
                if (EntregarCompra(t)) return;
                if (PegarEntrega(t)) return;
                Juiz(t); long pl = P0(); LunetaSegurando(t); ReacaoColega(t); P1("luneta + reação colega", pl); return;
            }
            // ---- troca: aliado morreu perto, vai em cima de quem matou
            if (t < tradeUntil) { fase = "indo trocar"; MoveTo(tradePos, true); if (SmokeEspera && SmokeNoCaminho(t)) { Face(Pixel(tradePos), 3); return; } /* bx: a troca/ajuda também decide a smoke inimiga no caminho (antes atravessava sem pensar)*/ ClearAngles(true); Face(Pixel(tradePos), 3); Think("indo trocar"); return; }   // pré-mirado na quina dele
            if (RoundGanho && !order.defuse) { if (LootGranada(t)) return; DeBoa(); return; }
            if (SairDoBomb(t)) return;
            if (EntregarCompra(t)) return;
            if (PegarEntrega(t)) return;
            if (PegarArma(t)) return;
            if (PegarGranada(t)) return;
            if (Utilitaria(t)) return;
            if (UtilProativa(t)) return;
            if (GranadaNoBarulho(t)) return;
            if (AbrirSmoke(t)) return;
            if (SprayNaSmoke(t)) return;
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
            if (!EXP.Contains("cegoParadoVelho")) { CegoAnda(t); return; }
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
                { m.Shoot(me, cegoAlvo, me.z + (Combat.HitboxTela ? Combat.CabecaZ(0) : 21.5f)); cegoTiros++; cegoTiro = t + rng.Uniform(0.12f, 0.3f); }
            }
            else if (cegoPlano == 2)
            {
                V2 tras = -d / dl;
                if (NavGrid.Movable(me.pos + tras * 14)) { wantVel = tras * me.MaxSpeed * 0.52f; me.move = Move.Andando; }
                else me.crouch = true;
            }
            else me.crouch = true;
        }

        float lunetaRevisa = -9;
        /// <summary>
        /// AWP segurando parado: luneta aberta (cravado, sem o tempo de entrar na luneta quando ele aparece, mas vê só o
        /// zoom) ou fechada (vê tudo e faz o quick-scope). O pro crava no ângulo longo onde acredita que vem gente e fica
        /// sem luneta no ângulo curto/aberto ou quando tem que olhar vários lados. Pesado pela situação, revisto a cada
        /// 1,5-3 s. -exp awpLunetaVelha: sempre sem luneta até ver.
        /// </summary>
        void LunetaSegurando(float t)
        {
            if (me.gunId != "awp" || EXP.Contains("awpLunetaVelha")) return;
            if (target != null || me.Blind(t) || me.gun.Reloading || !hasWantAim || wantVel.Len > 1 || me.Speed > me.MaxSpeed * 0.25f) return;
            if (t < lunetaRevisa) return;
            lunetaRevisa = t + rng.Uniform(1.5f, 3f);
            V2 dir = V2.Ang(wantAim);
            float raio = MapData.Raio(me.pos, me.z, dir, 900, me.Eye);   // até onde a linha vai
            V2 q = me.pos + dir * MathF.Min(raio, 600);
            float esp = MathF.Min(1, m.intel[(int)me.team].Esperados(q, 220));
            float agr = P.Agr(me.skill.aggression);
            float longo = Math.Clamp((raio - 140) / 300f, 0, 1);
            float p = 0.1f + 0.55f * longo + 0.25f * esp + 0.15f * (1 - agr) - (raio < 110 ? 0.3f : 0);
            if (m.t - somT < SomVale && (somP - me.pos).Len < 300) p -= 0.25f;   // barulho perto: precisa ver os lados
            bool quer = rng.Rand() < Math.Clamp(p, 0.03f, 0.92f);
            if (quer != me.scoped)
            {
                me.scoped = quer; m.Ev(quer ? "AWP: segurando de luneta aberta/revisão" : "AWP: segurando sem luneta/revisão");
                Think(quer ? $"cravado na luneta ({raio:0} de linha)" : "sem luneta — vejo os lados e faço o quick-scope");
            }
            else m.Ev(quer ? "AWP: segurando de luneta aberta/revisão" : "AWP: segurando sem luneta/revisão");
        }

        int rajadaN = 30; float rajadaDe = -99;
        Agent colegaAtirador; V2 colegaAtiradorP; float colegaApanhouT = -9, colegaReagiuT = -9, colegaDecidiuT = -9; Agent colegaDecidiuDe; bool colegaVira;
        /// <summary>Colega perto tomou tiro: o tiro veio de onde o atirador está (ouvido, não visto: um tanto de erro).</summary>
        public void ColegaApanhou(Agent colega, Agent atirador)
        {
            colegaAtirador = atirador; colegaApanhouT = m.t;
            colegaAtiradorP = ContatoDe(atirador) ?? atirador.pos;   // contato do time ou de onde o tiro saiu (ouvido do lado)
        }
        /// <summary>
        /// Reflexo de quem vê o colega apanhar do lado: a mira vai pra de onde vem o tiro (se eu tenho linha pra lá), mesmo
        /// andando — o inimigo que aparecer ali já está na mira (reação rápida, troca fácil). Sem linha: segue o que fazia
        /// (o caminho/ângulo dele cuida). Dura ~2,5 s ou até o atirador morrer. -exp colegaApanhaVelho
        /// </summary>
        void ReacaoColega(float t)
        {
            if (EXP.Contains("colegaApanhaVelho") || t - colegaApanhouT > 2.5f || colegaAtirador == null || !colegaAtirador.alive) return;
            if (target != null || action != null || me.Blind(t)) return;
            V2 p = ContatoDe(colegaAtirador) ?? colegaAtiradorP;
            if ((p - me.pos).Len > 900 || !MapData.LosWalk(me.pos, me.z + me.Eye, p, NavGrid.ZAt(p) + 18, 18f)) return;
            if (colegaDecidiuDe != colegaAtirador || t - colegaDecidiuT > 2.5f)
            {   // saber eu sei (ele está ali, atirando); virar é decisão: quase sempre sim, mas se o meu ângulo tem uma ameaça
                // que eu acredito mais forte e mais perto (o cara que vai abrir do outro spot), posso ficar nela
                colegaDecidiuDe = colegaAtirador; colegaDecidiuT = t;
                V2 aqui = me.pos + V2.Ang(me.aim) * 250;
                float espAqui = m.intel[(int)me.team].Esperados(aqui, 200);
                bool somAqui = t - somT < SomVale && MathF.Abs(MathX.AngDiff((somP - me.pos).Atan2, me.aim)) < 0.5f && (somP - me.pos).Len < (p - me.pos).Len;
                float pVira = 0.92f - (espAqui >= 0.8f ? 0.35f : 0) - (somAqui ? 0.35f : 0);
                colegaVira = rng.Rand() < pVira;
                m.Ev(colegaVira ? "reflexo: virou pro tiro no colega/round" : "reflexo: ficou no ângulo (ameaça mais forte)/round");
                Think(colegaVira ? $"tão atirando no colega — mira em {Region(p)}" : $"sei que tem um em {Region(p)}, mas o meu ângulo é mais quente — fico");
            }
            if (!colegaVira) return;
            fonteMira = "colega apanhando";
            Face(PixelDele(p), 3);
        }
        bool cegoAtira; int cegoMov; V2 cegoLado; float cegoRevisa;

        /// <summary>
        /// Tela branca, como o pro faz no CS2: ninguém fica parado esperando clarear — sai da linha em que estava (quem
        /// flashou vem peekar AQUELE ângulo): recua pra trás da cobertura de onde veio ou sai de lado, e às vezes segura
        /// agachado no canto (o ângulo apertado, perto do colega que troca). Atirar é outra decisão, junto: no vento pro
        /// ângulo onde ele acredita que tem gente (quem viu agora há pouco, o call, de onde veio a flash), andando ou parado
        /// — quase sempre erra, mas às vezes pega quem entra. Pesa o jeito dele, a arma e se tem colega na linha.
        /// -exp cegoParadoVelho: o antigo (atira parado, agacha ou recua, um só por flash).
        /// </summary>
        void CegoAnda(float t)
        {
            if (me.blindUntil != cegoDe)
            {
                cegoDe = me.blindUntil; cegoTiros = 0;
                V2? ameaca = null;
                if (lastSeen != null && lastSeen.alive && t - lastSeenT < 3f) ameaca = lastSeenPos;
                else ameaca = Chamada(600);
                cegoAlvo = ameaca ?? me.flashDe;
                float agr = P.Agr(me.skill.aggression), comp = me.skill.composure;
                bool awp = me.gunId == "awp";
                // atirar no vento: crença de que tem gente ali e o jeito dele; AWP quase não (um tiro e fica sem nada)
                float pTiro = (ameaca != null ? 0.25f + 0.35f * agr + 0.2f * (1 - comp) : 0.08f + 0.12f * agr) * (awp ? 0.35f : 1f);
                cegoAtira = rng.Rand() < pTiro;
                // andar: recuar (mais quando tem cobertura atrás e ninguém pra trocar), de lado (sai da linha do peek) ou
                // segurar agachado (ângulo apertado, colega perto pra trocar, plantando/desarmando)
                int perto = m.agents.Count(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 300);
                V2 d0 = cegoAlvo - me.pos; float dl0 = MathF.Max(1, d0.Len);
                V2 tras = -d0 / dl0, lado = new V2(-d0.y, d0.x) / dl0;
                bool podeTras = NavGrid.Movable(me.pos + tras * 24) && NavGrid.Movable(me.pos + tras * 48);
                float ladoA = NavGrid.Movable(me.pos + lado * 24) && NavGrid.Movable(me.pos + lado * 40) ? 1 : 0;
                float ladoB = NavGrid.Movable(me.pos - lado * 24) && NavGrid.Movable(me.pos - lado * 40) ? 1 : 0;
                cegoLado = ladoA >= ladoB ? lado : -lado; if (ladoA == 0 && ladoB == 0) cegoLado = V2.Zero;
                float wT = podeTras ? 0.45f + 0.3f * (1 - agr) + (perto == 0 ? 0.2f : 0) : 0;
                float wL = cegoLado.Len > 0 ? 0.35f + 0.25f * agr : 0;
                float wS = 0.12f + (perto > 0 ? 0.12f : 0) + (dl0 < 250 ? 0.1f : 0) + (order.plant || order.defuse ? 0.6f : 0) + (awp && order.hold != null ? 0.15f : 0);
                float u = rng.Rand() * (wT + wL + wS);
                cegoMov = u < wT ? 1 : u < wT + wL ? 2 : 0;
                cegoRevisa = t + rng.Uniform(0.5f, 0.9f);
                if (cegoAtira) me.tiroCego++; if (cegoMov == 1) me.recuoCego++; else if (cegoMov == 0) me.agachaCego++;
                m.Ev("cego: " + (cegoMov == 1 ? "recua" : cegoMov == 2 ? "sai de lado" : "segura agachado") + (cegoAtira ? " atirando" : "") + "/flash");
                Think($"cego — {(cegoMov == 1 ? "recuando pra cobertura" : cegoMov == 2 ? "saindo da linha de lado" : "agachado no canto")}{(cegoAtira ? $", atirando no vento pra {Region(cegoAlvo)}" : "")}");
            }
            Face(cegoAlvo, 3);
            V2 d = cegoAlvo - me.pos; float dl = MathF.Max(1, d.Len);
            // andando às cegas: sem correr (não vê o chão nem quem está na frente); se travar, troca de jeito
            if (cegoMov == 1)
            {
                V2 tras = -d / dl;
                if (NavGrid.Movable(me.pos + tras * 14)) { wantVel = tras * me.MaxSpeed * 0.55f; me.move = Move.Andando; }
                else if (cegoLado.Len > 0) cegoMov = 2; else cegoMov = 0;
            }
            else if (cegoMov == 2)
            {
                // ADAD curtinho pro mesmo lado: sai da linha e não volta pra ela
                if (NavGrid.Movable(me.pos + cegoLado * 14)) { wantVel = cegoLado * me.MaxSpeed * 0.6f; me.move = Move.Andando; }
                else if (t > cegoRevisa) { cegoLado = -cegoLado; cegoRevisa = t + 0.6f; }
            }
            else me.crouch = true;
            if (cegoAtira)
            {
                bool aliado = m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < dl &&
                                                MathF.Abs(MathX.AngDiff((x.pos - me.pos).Atan2, d.Atan2)) < 0.25f);
                if (!aliado && t > cegoTiro && cegoTiros < (me.gunId == "awp" ? 1 : 8) && me.gun.inMag > 0)
                {
                    // o tiro sai pro ângulo com o tremido do braço (Shoot já soma o espalhamento de quem anda)
                    V2 alvo = cegoAlvo + rng.Normal2(18);
                    m.Shoot(me, alvo, me.z + (Combat.HitboxTela ? Combat.CabecaZ(0) : 21.5f)); cegoTiros++; cegoTiro = t + rng.Uniform(0.12f, 0.3f);
                }
            }
        }

        void Trava(string r) { if (primeiroTiro < 0 && Match.Auditar) m.Aud("ver → 1º tiro: tempo gasto em (ticks)", r); }
        void Fight(Agent e, float t)
        {
            float dist = (e.pos - me.pos).Len;
            float aimH = Agent.AgacharVelho || EXP.Contains("miraDuckBinaria") ? Combat.AimHeight(me, dist, e.duck > 0.5f) : Combat.AimHeight(me, dist, e.duck);
            V2 aimPt = e.pos + e.vel * rng.Normal(0.02f, 0.07f);   // rastrear alvo andando: às vezes adianta, às vezes atrasa
            if (VisaoCorpo && parcial[e.id]) { aimPt += parteVista[e.id] - e.pos; aimH = parteH[e.id]; }   /* bx: mira no pedaço que aparece */
            Face(aimPt, aimH - me.Eye + e.z - me.z);
            if (me.faca) { m.Ev("faca: pego de faca (viu inimigo com a faca na mão)/round"); Sacar(t, $"pego de faca pelo {e.name}!"); reactAt = MathF.Max(reactAt, me.sacaAte); }
            if (me.nadeNaMao != null && action != "granada") { m.Ev("granada: pego com a granada sacada/round"); GuardaGranada(t, $"pego com a {me.nadeNaMao} na mão pelo {e.name}!"); reactAt = MathF.Max(reactAt, me.sacaAte); }
            if (t < agachaPeekAte) me.crouch = true;   // agachou no peek (some quando acaba ou na troca de jeito)
            // agachou no spray: fica embaixo entre os tiros e as rajadas deste duelo (CS2 pune levantar e agachar de novo)
            if (t < agachaSprayAte && !Agent.AgacharVelho && !EXP.Contains("agacharSemJeito")) me.crouch = true;
            // o alvo mudou de altura (apareceu agachado, agachou ou levantou no meio do duelo): a mira estava na cabeça de
            // antes — fica alta/baixa ~6 unidades até a mão corrigir (~0,25 s, depois de reagir)
            if (!EXP.Contains("miraVerticalVelha") && e.duck > 0.5f != alvoAgachado)
            {
                float graus = MathF.Atan2(Combat.HitboxTela ? 2.7f : 21.5f * 0.28f, MathF.Max(dist, 30f)) * 180 / MathF.PI;
                errVSalto = e.duck > 0.5f ? graus : -graus; errVSaltoT = t; alvoAgachado = e.duck > 0.5f;
                if (Match.Auditar && e.duck > 0.5f) m.Aud("alvo agachou: mira ficou alta", t < reactAt ? "ao aparecer" : "no meio do duelo");
            }
            me.aimErrV = errV0 * MathF.Exp(-MathF.Max(0, t - reactAt) / 0.3f) + errVSalto * MathF.Exp(-MathF.Max(0, t - MathF.Max(errVSaltoT, reactAt)) / 0.25f);
            if (t < reactAt) { Trava("a reação"); wantVel = me.vel * 0.6f; return; }   // ainda reagindo
            if (me.gunId == "awp" && !me.scoped) { Trava("b luneta"); me.scoped = true; reactAt = MathF.Max(reactAt, t + 0.14f); return; }   // entrar na luneta leva um tempo
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
            // C4 indo plantar: troca tiro, mas entre as rajadas o passo é pro bomb (não fica parado duelando no caminho);
            // de perto não tem pra onde ir, é duelo
            bool adadDuelo = this.adadDuelo;
            // duelo empacado: a cada ~2,5 s sem tirar vida dele, reavalia (não é regra de tempo: pesa a situação)
            bool entrando = !EXP.Contains("entradaParadaVelho") && Entrando(out _);
            if (t - alvoDesde > (entrando ? 1.5f : 2.5f) && t >= duelReval && !EXP.Contains("dueloEterno"))
            {
                duelReval = t + (entrando ? rng.Uniform(1.2f, 2f) : rng.Uniform(2f, 3f));
                bool progresso = e.hp < duelHpRef; duelHpRef = e.hp;
                if (!progresso && (dist > 220 || entrando)) duelModo = ModoDuelo(e, dist, t);
                else if (progresso) duelModo = 0;
            }
            // "vou morrer": perdendo o duelo (levei muito e ele quase nada, ou vários me vendo) — decide UMA vez, pelo jeito
            // dele e pela situação: quebrar a linha (às vezes pulando pra tirar a mira dele), agachar e spray (aposta
            // tudo na mira) ou seguir no ADAD. Não é regra: às vezes fica e ganha, às vezes foge e morre de costas
            if (!EXP.Contains("evasivoVelho") && !evasivoDecidiu && t >= evasivoProx && duelModo != 2)
            {
                evasivoProx = t + 0.4f;
                int vendo = 0; foreach (var x in m.agents) if (x.alive && x.team != me.team && x.brain.target == me) vendo++;
                bool perdendo = me.hp <= duelHpEu - 35 && e.hp >= duelHpRef - 20 || vendo >= 2 && me.hp < 70;
                if (perdendo)
                {
                    evasivoDecidiu = true;
                    float agr = P.Agr(me.skill.aggression);
                    bool pressa = me.IsT ? m.RoundLeft < 25 || order.plant : order.defuse;
                    float wFoge = 0.3f + (me.hp < 40 ? 0.5f : 0) + (vendo >= 2 ? 0.5f : 0) - (pressa ? 0.4f : 0) - (dist < 150 ? 0.35f : 0) + 0.3f * (1 - agr);
                    float wAgacha = 0.25f + (dist < 320 ? 0.3f : 0) + 0.4f * me.skill.spray + 0.2f * agr;
                    float wSegue = 0.35f + 0.2f * agr;
                    int k = rng.Weighted(new[] { wFoge, wAgacha, wSegue });
                    if (k == 0)
                    {
                        duelModo = 2; evasivoPula = rng.Rand() < 0.25f + 0.3f * me.skill.composure;
                        m.Ev(evasivoPula ? "evasivo: quebrou a linha pulando/round" : "evasivo: quebrou a linha/round");
                    }
                    else if (k == 1) { agachaDuelo = true; m.Ev("evasivo: agachou e foi no spray/round"); Think($"tô perdendo pro {e.name} — agacho e vou no spray"); }
                    else m.Ev("evasivo: seguiu no ADAD/round");
                }
            }
            if (duelModo == 2)
            {   // sair da linha
                // vai pra onde ele não me vê e larga esse duelo um tempo (pulando, se decidiu: o pulo sai já embalado)
                if (evasivoPula) { evasivoPulaAte = t + 0.9f; evasivoPula = false; }
                largouAlvo = e; largouAte = t + rng.Uniform(3f, 6f); duelModo = 0; target = null;
                V2 sai = SemVisaoDe(e);
                MoveTo(sai, true); Face(PixelDele(e.pos), 3); fase = "saindo do duelo";
                Think($"duelo de longe com o {e.name} sem acertar — saio da linha");
                m.Ev("duelo empacado: saiu da linha/round");
                return;
            }
            if (duelModo == 1 && dist > 140)
            {   // fechar a distância: entre os tiros o passo vai na direção dele (de shift; para pra atirar)
                V2 sv = wantVel; wantVel = V2.Zero; string fs = fase;
                MoveTo(e.pos, false); adadVel = wantVel; wantVel = sv; fase = fs;
                if (adadVel.Len > 0.1f) adadDuelo = true;
            }
            if (me.hasBomb && order.plant && !m.bombPlanted && !EXP.Contains("c4Parado") && (m.PlantSpot - me.pos).Len > 10)
            {
                if (t >= c4ModoAte) { c4ModoAte = t + rng.Uniform(0.8f, 1.4f); c4Modo = ModoC4(e, dist, t); }
                if (c4Modo != 0)
                {   // entre as rajadas o passo vai pro bomb (1) ou pra trás, pra perto do time (2); o tiro continua parado
                    V2 sv = wantVel; wantVel = V2.Zero; string fs = fase;
                    if (c4Modo == 1) MoveTo(m.PlantSpot, false); else Retreat(e);
                    adadVel = wantVel; wantVel = sv; fase = fs;
                    if (adadVel.Len > 0.1f) adadDuelo = true;
                }
            }
            wantVel = V2.Zero;
            if (me.TemOutra && !EXP.Contains("semPistola") && TrocaNoDuelo(e, dist, t)) return;
            if (me.gun.Reloading) { Think("recarregando no meio da troca — me escondendo"); Retreat(e); return; }
            if (me.gun.inMag == 0) { m.Clique(me); me.gun.StartReload(); return; }   // apertou com o pente vazio: clique seco (som), e recarrega se tem bala
            int maxBurst = me.gunId == "awp" ? 1 : deagle ? (dist > 120 ? 1 : 3) : dist > 420 ? 2 : dist > 220 ? 5 : 30;   // longe toca, médio rajada, perto spray; Deagle: um tiro e espera a mira voltar
            bool rifle = me.gunId == "ak" || me.gunId == "m4" || me.gunId == "m4s" || me.gunId == "galil" || me.gunId == "famas";
            float espera = deagle ? 0.5f : 0.32f;
            if (rifle && !EXP.Contains("rajadaVelha"))
            {
                // rifle como o pro (guias de CS2): longe (25 m+, ~250 aqui) para e dá o tap — a 1ª bala não tem recuo e
                // mata na cabeça; médio (10-25 m) rajada de 3-7; perto spray. Sorteado a cada rajada pelo jeito e pela
                // hora (alvo parado puxa o one tap; pânico/afobado estica a rajada), não regra
                if (burst == 0 && rajadaDe != me.lastShotT)
                {
                    float afob = 0.5f * me.panico + 0.3f * (1 - me.skill.composure);
                    if (dist > 250)
                    {
                        float pUm = 0.45f + (e.Speed < e.MaxSpeed * 0.2f ? 0.2f : 0) - afob * 0.4f;
                        float u = rng.Rand(); rajadaN = u < pUm ? 1 : u < pUm + 0.35f ? 2 : 3;
                    }
                    else if (dist > 100) rajadaN = Math.Clamp((int)MathF.Round(rng.Normal(7f - 4f * (dist - 100) / 150f + 2 * afob, 1.2f)), 3, 9);
                    else rajadaN = rng.Rand() < 0.15f ? 6 + rng.Int(5) : 30;
                    rajadaDe = me.lastShotT;
                    m.Ev(rajadaN == 1 ? "rifle: one tap/rajada" : rajadaN <= 3 ? "rifle: tap 2-3/rajada" : rajadaN <= 9 ? "rifle: rajada 4-9/rajada" : "rifle: spray/rajada");
                }
                maxBurst = rajadaN;
                espera = rajadaN <= 2 ? 0.42f : 0.36f;   // a mira volta (AK ~0,4 s pro 1º tiro exato de novo)
            }
            if (burst >= maxBurst)
            {
                if (t - me.lastShotT > espera) { burst = 0; me.spray = 0; }
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
            if (err > tol * 1.6f) { Trava("c mira ainda chegando"); if (adadDuelo) wantVel = adadVel; return; }   // ainda acertando a mira: se mexe enquanto isso
            if (!me.gun.CanFire(t) || !me.ArmaPronta(t)) { Trava("d arma (cadência/saque)"); if (adadDuelo && burst == 0 || runGun) wantVel = adadVel; return; }
            if (!runGun && me.Speed > me.MaxSpeed * limiar) { Trava("e freando (counter-strafe)"); return; }               // counter-strafe: para antes de atirar
            // amigo na linha do tiro: ninguém atira atravessando o colega (o tiro machuca ele e para nele) — segura e
            // dá um passo de lado pra abrir a linha
            if (!EXP.Contains("atiraNoAmigo") && AmigoNaLinha(aimPt, e.z + aimH, dist, out var amigo))
            {
                Trava("f amigo na linha");
                V2 dl = (aimPt - me.pos) / MathF.Max(1, dist), pp = new V2(-dl.y, dl.x);
                float ladoAm = MathF.Sign(pp.Dot(amigo.pos - me.pos)); if (ladoAm == 0) ladoAm = 1;
                V2 sai = pp * -ladoAm;
                wantVel = NavGrid.Movable(me.pos + sai * 8) ? sai * me.MaxSpeed * 0.7f : NavGrid.Movable(me.pos - sai * 8) ? sai * (-me.MaxSpeed * 0.7f) : V2.Zero;
                if (t - amigoNaLinhaLog > 2) { amigoNaLinhaLog = t; Think($"o {amigo.name} tá na minha linha — não atiro, abro de lado"); m.Ev("segurou o tiro: amigo na linha/round"); }
                return;
            }
            if (runGun && adadDuelo) wantVel = adadVel * 0.8f;
            if (agachaDuelo && me.spray >= 2 && dist > 100 && dist < 420) { me.crouch = true; agachaSprayAte = t + 0.9f; }   // spray agachado (mais preciso, cabeça mais baixa)
            if (primeiroTiro < 0)
            {
                primeiroTiro = t; float rr = t - alvoDesde;   // real (143 partidas pro): p25 0,08 · p50 0,16 · p75 0,25 · p90 0,45
                m.Aud("viu → 1º tiro (todos os duelos)", rr < 0.08f ? "a <0,08" : rr < 0.16f ? "b 0,08-0,16" : rr < 0.25f ? "c 0,16-0,25" : rr < 0.45f ? "d 0,25-0,45" : "e 0,45+");
            }
            tirosNoAlvo++;
            m.Aud("tiro parado/andando/agachado (real 58% parado, 20% agachado)", me.Agachado ? "agachado" : me.Speed < me.MaxSpeed * 0.16f ? "parado" : "andando");
            m.Shoot(me, aimPt, e.z + aimH);
            if (t < tiroAte) { m.contatosTiro.Add(tiroFlick); tiroAte = -9; }
            burst++;
            Think($"atirando no {e.name} ({(aimH > 18 ? "cabeça" : "peito")}, {(burst > 1 ? "spray" : "1º tiro")})");
        }

        /// <summary>
        /// Pistola no meio do duelo, como o pro: acabou o pente com ele na cara → puxar a pistola é mais rápido que
        /// recarregar; com tempo ou atrás de parede, recarrega. AWP: deu o tiro e tem vários vindo (ou um colado) → às
        /// vezes puxa a pistola pra segurar o resto, às vezes fica na AWP — vai do jogador (tem quem tente matar os 5 de AWP).
        /// Na pistola sem bala, volta pra primária se ela tem pente.
        /// </summary>
        bool TrocaNoDuelo(Agent e, float dist, float t)
        {
            var outra = me.outraGun;
            if (WeaponStats.Pistola(me.gunId))
            {
                if (me.Primaria == null || me.gun.inMag > 0 || me.gun.Reloading || outra.inMag == 0) return false;
                me.TrocarArma(t); burst = 0; m.Ev("pistola vazia: voltou pra primária/round");
                Think($"pistola vazia — volto pra {WeaponStats.ALL[me.gunId].name}");
                return true;
            }
            bool vazio = me.gun.inMag == 0 && !me.gun.Reloading;
            bool awpAtirou = me.gunId == "awp" && me.lastShotT > awpTiroVisto && t - me.lastShotT < 0.25f;
            if (!vazio && !awpAtirou) return false;
            if (awpAtirou) awpTiroVisto = me.lastShotT;
            if (outra.inMag == 0) return false;
            var it = m.intel[(int)me.team];
            int vistos = 0;
            foreach (var c in it.contacts.Values) if (m.agents[c.id].alive && t - c.t < 2.5f && (c.pos - me.pos).Len < 650) vistos++;
            int hpDele = it.contacts.TryGetValue(e.id, out var ce) && t - ce.hpT < 8 ? ce.hp : 100;
            bool exposto = t - lastSeenT < 0.4f;
            float mira = MathX.Clamp(Skill.MIRA_PADRAO / MathF.Max(0.2f, me.skill.aimSigma), 0.5f, 1.6f);
            float jeito = (((uint)Estavel(me.name + "pistola") * 2654435761u >> 24) / 255f - 0.5f) * 0.6f;   // tendência do jogador (fixa): uns puxam mais a pistola
            float pist, fica;
            if (vazio)
            {
                pist = 0.4f + (exposto ? 0.7f : 0) + (dist < 350 ? 0.5f : 0) + (hpDele <= 45 ? 0.4f : 0) + (me.gun.s.Reload > 2.8f ? 0.2f : 0) + (vistos >= 2 ? 0.3f : 0);
                fica = 0.5f + (!exposto ? 0.9f : 0) + (dist > 550 ? 0.5f : 0) + 0.3f * me.skill.composure + (me.gun.mags == 0 ? -9 : 0);
                if (!EXP.Contains("trocaSemTempo"))
                {   // a conta que o pro faz sem pensar: sacar a pistola (CS2 ~1 s) x recarregar (2,4-3,7 s). Pouca diferença e
                    // coberto, recarrega; exposto, a pistola ainda chega antes
                    float tPist = 1.0f, ganho = me.gun.s.Reload - tPist;
                    pist += 0.5f * MathX.Clamp((ganho - 1.6f) / 1.2f, -1, 1);
                }
            }
            else
            {
                if (vistos < 2 && dist > 220) return false;   // um só e de longe: a AWP é a arma
                pist = 0.2f + 0.45f * (vistos - 1) + (dist < 220 ? 0.6f : 0) + (dist < 120 ? 0.5f : 0);
                fica = 0.7f + (dist > 400 ? 0.7f : 0) + 0.4f * (mira - 1);
            }
            pist += jeito + rng.Normal(0, 0.3f); fica += -jeito + rng.Normal(0, 0.3f);
            if (pist <= fica) { if (awpAtirou) m.Ev("AWP: ficou na AWP contra vários/round"); return false; }
            me.TrocarArma(t); burst = 0;
            m.Ev(vazio ? "sem bala no duelo: puxou a pistola/round" : "AWP: puxou a pistola contra vários/round");
            Think(vazio ? $"acabou o pente com o {e.name} na cara — pistola!" : $"dei o tiro de AWP e tem {vistos} vindo — puxo a {WeaponStats.ALL[me.gunId].name}");
            return true;
        }
        float awpTiroVisto = -9, amigoNaLinhaLog = -9, proxPulo = 2;

        /// <summary>
        /// Pulo de deslocamento, como o pro faz girando: correndo numa reta plana, longe de inimigo (aterrissar faz barulho
        /// e no ar não se acerta nada), às vezes dá um strafe jump — e às vezes emenda um 2º (bunny hop; a estamina do
        /// servidor oficial não deixa mais que isso). No ar faz o strafe: aperta de lado pro lado que quer virar, o que
        /// ganha um pouco de velocidade. Vai do jogador: tem quem pule muito e quem quase nunca pule.
        /// </summary>
        public void Pulo(float dt)
        {
            if (EXP.Contains("semPulo")) return;
            if (me.noAr) { if (me.puloLivre) StrafeNoAr(); return; }
            float t = m.t;
            float jeito = JeitoPulo;
            bool emendou = t - me.pousouEm < 0.04f && me.pulosSeguidos == 1;   // só o tick em que pousou (uma chance por pouso)
            if (t < proxPulo && !emendou) return;
            if (target != null || action != null) return;
            if (wantVel.Len < me.MaxSpeed * 0.9f || me.Speed < me.MaxSpeed * 0.85f || me.hasBomb && order.plant || order.quiet || Silencio(order)) return;
            var it = m.intel[(int)me.team];
            // no ar não se acerta nada: perto de onde pode ter inimigo, ou logo depois de ver/ouvir alguém, fica no chão
            float risco = MathX.Clamp(it.Esperados(me.pos, 500) * 2, 0, 1);
            if (risco > 0.6f || t - lastSeenT < 5 || t - somT < 3) return;
            V2 dir = wantVel / wantVel.Len;
            // o pulo leva ~0,55 s (uns 40 de chão): precisa de reta plana até depois de aterrissar
            if (!NavGrid.Direct(me.pos, me.pos + dir * 55) || MathF.Abs(NavGrid.ZAt(me.pos + dir * 55) - me.z) > 2) return;
            proxPulo = t + rng.Uniform(0.6f, 1.4f);
            float p;
            if (emendou) p = 0.05f + 0.3f * jeito;   // bunny hop: emenda no tick em que pousa (e a estamina come o resto)
            else
            {   // o strafe jump é o pro ganhando tempo: saindo da base, girando, indo pro bomb na execução ou no retake —
                // longe do destino e com pressa pula mais; quem segura posição/anda devagar não pula
                float longe = MathX.Clamp(((order.hold != null ? order.hold.pos : order.dest) - me.pos).Len / 400f, 0, 1);
                float pressa = t < 12 ? 0.8f : order.push || order.defuse || m.bombPlanted ? 1f : order.paciente ? 0.2f : 0.5f;
                p = (0.05f + 0.25f * jeito * jeito) * (0.3f + longe) * pressa * (1 - risco);
            }
            if (rng.Rand() >= p) return;
            me.querPular = true;
        }
        /// <summary>Tendência e técnica de pulo do jogador (0..1, fixa): quem pula muito também faz melhor o strafe.</summary>
        float JeitoPulo => ((uint)Estavel(me.name + "pulo") * 2654435761u >> 24) / 255f;
        int ladoStrafe;

        /// <summary>
        /// No ar: o A/D sincronizado com a mira (strafe). No ar do Source só ganha velocidade quem aperta quase de lado
        /// em relação à velocidade (a ~90° dela): aí cada tick soma até o teto do ar e a velocidade cresce virando pro
        /// lado apertado. Alterna o lado quando passa do rumo, então a curva oscila em volta do caminho e o ganho fica.
        /// Humano: o ângulo sai torto (menos quem tem jeito) e às vezes dessincroniza (aperta pra frente: não ganha nada).
        /// </summary>
        void StrafeNoAr()
        {
            V2 v = me.vel; float vl = v.Len; if (vl < 5) return;
            V2 g = wantVel.Len > 1 ? wantVel / wantVel.Len : v / vl;
            float desvio = MathX.AngDiff(g.Atan2, v.Atan2);   // >0: o rumo está pro lado anti-horário da velocidade
            if (ladoStrafe == 0 || desvio * ladoStrafe < -0.3f) ladoStrafe = desvio >= 0 ? 1 : -1;
            if (EXP.Contains("strafeVelho")) { wantVel = V2.Ang(v.Atan2 + ladoStrafe * (MathF.PI / 2 - 0.25f)) * me.MaxSpeed; return; }
            float q = 0.4f + 0.6f * JeitoPulo;
            if (rng.Rand() < 0.08f + 0.3f * (1 - q)) { wantVel = g * me.MaxSpeed; return; }   // dessincronizou
            float ang = MathF.PI / 2 + rng.Normal(0, (3 + 15 * (1 - q)) * MathF.PI / 180f);
            wantVel = V2.Ang(v.Atan2 + ladoStrafe * ang) * me.MaxSpeed;
        }

        /// <summary>Tem colega no caminho da bala (corpo dele cruza a linha até o alvo, na altura em que ela passa)?</summary>
        bool AmigoNaLinha(V2 alvo, float zAlvo, float dist, out Agent amigo)
        {
            amigo = null;
            V2 dir = (alvo - me.pos) / MathF.Max(1, dist); float z0 = me.z + me.Eye;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                V2 rel = x.pos - me.pos; float ao = rel.Dot(dir);
                if (ao < -5 || ao > dist - 3) continue;   // colado (até do lado/um pouco atrás) também: a bala já pega ele nos primeiros passos
                ao = MathF.Max(ao, 0);
                float lat = MathF.Abs(rel.x * dir.y - rel.y * dir.x);
                // a bala pode sair até aqui do lado: a abertura da arma (spray) + o erro da mão (2 sigmas da mira do jogador)
                // (mesma conta do Combat.Fire: imprecisão parado + penalidade do spray + andando, recoil não compensado)
                var sg = me.gun.s; float vr = MathX.Clamp((me.Speed - 0.34f * me.MaxSpeed) / (0.61f * me.MaxSpeed), 0, 1);
                float inac = (me.Agachado ? sg.inCrouch : sg.inStand) + me.gun.penalidade + (vr > 0 ? sg.inMove * MathF.Pow(vr, 0.25f) : 0);
                float recoil = me.spray > 0 && me.gunId != "awp" ? Math.Min(me.spray + 1, 12) * 0.55f * sg.recoil / 30f * (1 - me.skill.spray) : 0;
                float abre = inac * Combat.ESCALA_ARMA + (2 * me.skill.aimSigma + recoil + 1.4f) * MathF.PI / 180f;
                if (lat > 4.8f + MathF.Min(MathF.Tan(abre) * ao, 10f)) continue;   // corpo ~4,6 de meia largura
                float zb = z0 + (zAlvo - z0) * (ao / dist);
                if (zb < x.z - 1 || zb > x.z + (25 - 7 * x.duck)) continue;   // passa por cima/baixo dele
                amigo = x; return true;
            }
            return false;
        }

        /// <summary>Fora do duelo: de pistola com a primária guardada, volta pra primária quando a área acalma; primária
        /// sem nenhuma bala (pente e reserva), fica na pistola.</summary>
        void VoltarArma(float t)
        {
            if (!me.TemOutra || EXP.Contains("semPistola") || target != null || me.faca) return;
            if (WeaponStats.Pistola(me.gunId))
            {
                if (me.Primaria == null || t - lastSeenT < 1.2f || me.outraGun.inMag == 0 && me.outraGun.mags == 0) return;
                me.TrocarArma(t); Think($"calmo de novo — volto pra {WeaponStats.ALL[me.gunId].name}");
            }
            else if (me.gun.inMag == 0 && me.gun.mags == 0 && !me.gun.Reloading)
            { me.TrocarArma(t); Think($"sem bala nenhuma na primária — fico de {WeaponStats.ALL[me.gunId].name}"); }
        }

        public float primeiroTiro = -1, reacao0; public int tirosNoAlvo;
        float tiroFlick, tiroAte = -9, proxReavaliaSave, c4ModoAte = -9;
        int c4Modo; bool levouSair; float preTrocaAte = -9;
        int duelModo, duelHpRef, duelHpEu = 100; float duelReval, largouAte = -9, evasivoProx; bool evasivoDecidiu, evasivoPula; float evasivoPulaAte = -9; Agent largouAlvo;
        /// <summary>
        /// Duelo de longe sem ninguém acertar: 0 continua, 1 fecha a distância, 2 sai da linha. Pesa a minha vida e a
        /// dele, a pressa (TR sem tempo, C4, defuse), se eu sou AWP (de longe é a arma dele), e o jeito do jogador.
        /// </summary>
        int ModoDuelo(Agent e, float dist, float t)
        {
            float agr = P.Agr(me.skill.aggression);
            bool pressa = me.IsT ? (m.RoundLeft < 35 && !m.bombPlanted) || order.plant : order.defuse || m.bombPlanted && m.BombLeft < 20;
            int hpDele = m.intel[(int)me.team].contacts.TryGetValue(e.id, out var c) && t - c.hpT < 8 ? c.hp : 100;
            float fica = 0.4f + (me.gunId == "awp" ? 0.9f : 0) + (order.hold != null && !pressa ? 0.3f : 0);
            float vai = 0.2f + 0.6f * agr + (pressa ? 1.0f : 0) + (hpDele < 50 ? 0.5f : 0) + (dist < 380 ? 0.2f : 0) - (me.hp < 40 ? 0.5f : 0);
            float sai = 0.4f + (me.hp < 40 ? 0.9f : 0) + (dist > 420 ? 0.5f : 0) + (me.gunId == "awp" ? -0.5f : 0) - (pressa ? 0.4f : 0);
            if (!EXP.Contains("entradaParadaVelho") && Entrando(out int parados))
            {   // entrando no bomb (execução/retake) com o time na porta: ficar parado trocando tiro é todo mundo no mesmo lugar
                // tomando bala de quem segura — quanto mais gente parada colada em mim, mais vale eu ir caindo (os de trás
                // cobrem e trocam); a vida baixa e a AWP ainda pesam contra
                vai += 0.45f + 0.35f * parados; fica -= 0.25f * parados;
                if (Match.Auditar) m.Aud("entrada: duelo empacado com o time parado", parados == 0 ? "a sozinho" : parados == 1 ? "b 1 colega parado do lado" : "c 2+ parados do lado");
            }
            fica += rng.Normal(0, 0.3f); vai += rng.Normal(0, 0.3f); sai += rng.Normal(0, 0.3f);
            int modo = fica >= vai && fica >= sai ? 0 : vai >= sai ? 1 : 2;
            if (modo == 1) { m.Ev("duelo empacado: fechou a distância/round"); Think($"de longe ninguém acerta — vou fechar no {e.name}"); }
            return modo;
        }
        /// <summary>
        /// Estou entrando no lugar que o time quer (TR indo pro bomb do plano antes do plant; CT indo pro bomb plantado no
        /// retake), perto dele, com gente deles esperada lá e colega junto: é entrada, não posição. `parados` = colegas
        /// colados em mim (até 120) quase parados — o "todo mundo na porta".
        /// </summary>
        bool Entrando(out int parados)
        {
            parados = 0;
            V2 obj; if (me.IsT && !m.bombPlanted) obj = Plant(m.planSite); else if (!me.IsT && m.bombPlanted) obj = m.bombPos; else return false;
            if ((obj - me.pos).Len > 700 || order.hold != null && !order.push) return false;
            if (m.intel[(int)me.team].Esperados(obj, 450) < 0.5f) return false;
            int junto = 0;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                float d = (x.pos - me.pos).Len;
                if (d < 260) junto++;
                if (d < 120 && x.Speed < x.MaxSpeed * 0.3f) parados++;
            }
            return junto >= 1;
        }

        /// <summary>Ponto perto onde ele não me vê (quebrar a linha), preferindo o lado de onde eu vim.</summary>
        V2 SemVisaoDe(Agent e)
        {
            V2 best = me.pos; float bd = float.MaxValue;
            for (int k = 0; k < 16; k++)
                foreach (float r in new[] { 40f, 70f, 110f })
                {
                    V2 q = NavGrid.Snap(me.pos + V2.Ang(k * 0.3927f) * r);
                    if (!NavGrid.Movable(q) || MapData.LosWalk(e.pos, e.z, q, NavGrid.ZAt(q), 18f)) continue;
                    float pl = NavGrid.PathLenAte(me.pos, q, 180); if (pl <= 0 || pl > 180) continue;
                    float sc = pl - 0.3f * (q - e.pos).Len;
                    if (sc < bd) { bd = sc; best = q; }
                }
            if (bd == float.MaxValue) { V2 away = me.pos - e.pos; best = NavGrid.Snap(me.pos + away / MathF.Max(1, away.Len) * 60); }
            return best;
        }
        /// <summary>
        /// C4 indo plantar e trocando tiro: não tem regra, ele lê a situação — 0 duela parado, 1 segue pro bomb atirando,
        /// 2 recua pro time. Pesa vida, distância do bomb, quantos são, se tem colega pra trocar, se o cara está no caminho.
        /// </summary>
        int ModoC4(Agent e, float dist, float t)
        {
            var it = m.intel[(int)me.team];
            float bomb = NavGrid.PathLen(me.pos, m.PlantSpot); if (bomb <= 0) bomb = (m.PlantSpot - me.pos).Len;
            int colegas = m.agents.Count(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 300);
            int vistos = it.contacts.Values.Count(c => t - c.t < 3 && m.agents[c.id].alive && (c.pos - e.pos).Len < 350);
            V2 rumo = path != null && wp < path.Count && pathGoal.Equals(m.PlantSpot) ? path[wp].p - me.pos : m.PlantSpot - me.pos;
            bool noCaminho = rumo.Len > 0.1f && MathF.Abs(MathX.AngDiff((e.pos - me.pos).Atan2, rumo.Atan2)) < 0.8f && dist < bomb;
            int hpDele = it.contacts.TryGetValue(e.id, out var ce) && t - ce.hpT < 6 ? ce.hp : 100;
            float agr = P.Agr(me.skill.aggression);
            // duelar: de perto, ele no caminho, ele fraco, eu inteiro e com gente pra trocar
            float duela = (dist < 200 ? 1.2f : dist < 400 ? 0.4f : 0) + (noCaminho ? 0.9f : 0) + (hpDele < 50 ? 0.6f : 0)
                        + (me.hp > 70 ? 0.3f : 0) + 0.25f * MathF.Min(colegas, 2) + 0.4f * agr;
            // seguir pro bomb: perto do plant, ele longe/de lado, colegas já indo
            float vai = (bomb < 250 ? 1.3f : bomb < 600 ? 0.7f : 0.1f) + (dist > 350 ? 0.6f : 0) + (!noCaminho ? 0.4f : -0.8f)
                      + 0.2f * MathF.Min(colegas, 2) + 0.3f * agr;
            // recuar: machucado, vários deles, sozinho — o C4 vivo vale mais que o duelo
            float recua = (me.hp < 40 ? 1.0f : 0) + (vistos >= 2 ? 0.9f : 0) + (colegas == 0 ? 0.5f : 0) + (bomb > 600 ? 0.3f : 0) + 0.4f * (1 - agr);
            duela += rng.Normal(0, 0.35f); vai += rng.Normal(0, 0.35f); recua += rng.Normal(0, 0.35f);
            int modo = duela >= vai && duela >= recua ? 0 : vai >= recua ? 1 : 2;
            if (modo != c4Modo || t - alvoDesde < 0.1f)
            { m.Ev(modo == 0 ? "C4 no tiro: duelou/round" : modo == 1 ? "C4 no tiro: seguiu pro bomb/round" : "C4 no tiro: recuou/round"); Think(modo == 0 ? "com o C4: dá pra ganhar esse duelo" : modo == 1 ? "com o C4: troco tiro andando pro bomb" : "com o C4: recuo pro time, não perco o bomb aqui"); }
            return modo;
        }
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
            if (angAtivo && Match.Auditar && (dead.pos - angPos).Len < 300) angMortes++;
            if (!me.alive) return;
            float d = (dead.pos - me.pos).Len;
            V2 matador = killer != null ? ContatoDe(killer) ?? dead.pos : dead.pos;   // ninguém viu quem matou: só sei onde o colega caiu
            if (killer != null && !EXP.Contains("apTrocaSomVelho") && ContatoDe(killer) == null)
            {   /* TROCA PELO SOM (padrão desde 2026-11-05; apTrocaSomVelho volta): ninguém viu quem matou, mas todo mundo ouviu o tiro — o tiro mais recente perto de onde
                   o colega caiu é onde o matador está (crença do time, não a posição real). Antes ia pra onde o colega caiu: 47% das trocas */
                var hs = m.intel[(int)me.team].heard; float melhor = 1e9f;
                for (int i = hs.Count - 1; i >= 0 && m.t - hs[i].t < 1.2f; i--)
                    if (hs[i].kind == "tiro") { float dd = (hs[i].p - dead.pos).Len; if (dd < 700 && dd < melhor) { melhor = dd; matador = hs[i].p; } }
            }
            if (killer != null && d < 650) { focoMorteDe = killer; focoMortePos = matador; focoMorteT = m.t; focoMorteAte = m.t + 6; }
            // longe pra trocar, mas sei onde ele está: não entro no mesmo ângulo do mesmo jeito (um de cada vez é morrer igual)
            if (killer != null && d <= 1100 && (d > 420 || me.guardando || action != null) && !EXP.Contains("semCuidado") && !me.guardando)
            { cuidadoDe = killer; cuidadoPos = matador; cuidadoAte = m.t + 10; cuidadoModo = -1; }
            if (Match.Auditar && d <= 420 && killer != null) m.Aud("troca: colega a até 420 — por que não foi", me.guardando ? "guardando a arma" : action != null ? "em ação (" + action + ")" : target != null ? "já em combate" : "avaliou");
            if (Match.Auditar && d <= 420 && killer != null && target != null)   /* bx: em combate com quem? (visão do corpo: mais duelos ao mesmo tempo) */
                m.Aud("troca: já em combate com", (target == killer ? "o matador" : "outro") + (parcial[target.id] ? " · só um pedaço" : " · corpo todo") + ((target.pos - me.pos).Len < 300 ? " · <300" : " · 300+"));
            if (d > 420 || killer == null || me.guardando || action != null) { if (d < 260) Think($"{dead.name} caiu perto — segurando"); return; }
            // troca (refrag): quem está perto vai em cima de quem matou — o suporte/entry quase sempre, quem segura
            // posição às vezes (depende do jeito dele e de quanto a posição importa); o AWP raramente sai do ângulo
            bool trader = me.role == Role.Suporte || me.role == Role.Entry || me.role == Role.Igl || me.role == Role.Rotacao;
            bool tv = EXP.Contains("trocaVelha");
            // pro troca quase sempre quem está do lado: o matador acabou de atirar (mira no colega morto, às vezes
            // recarregando) — é a hora de abrir. Quem segura posição chave (AWP, âncora) troca menos
            float p = (trader ? 1f : tv ? 0.65f : 0.8f) * (order.hold == null ? 1f : tv ? 0.8f : 0.9f) * (me.gunId == "awp" ? 0.35f : 1f) * (0.7f + 0.5f * P.Agr(me.skill.aggression)) * (d < 180 ? (tv ? 1.15f : 1.3f) : 0.85f);
            // trocar correndo pra dentro de 2+ inimigos é morrer junto: aí segura o ângulo e espera ele aparecer
            float espM = m.intel[(int)me.team].Esperados(matador, 300);
            if (tv ? espM >= 2f : espM >= 2.5f) p *= tv ? 0.3f : 0.45f;
            if (Explicar) porquePendente = $"chance de trocar {p:0%}: colega a {d:0} de mim, função {me.role}, devem ter {m.intel[(int)me.team].Esperados(matador, 300):0.0} em volta do matador";
            if (Match.Auditar) m.Aud("troca: decisão (chance)", (p >= 0.8f ? "a alta" : p >= 0.5f ? "b média" : p >= 0.25f ? "c baixa" : "d quase nada"));
            if (rng.Rand() < p)
            {
                tradeUntil = m.t + 3.4f; tradePos = EXP.Contains("apTrocaAlvoVelho") ? matador : AlvoTroca(matador, dead.pos);
                if (!EXP.Contains("apTrocaJanelaVelho"))
                {   /* janela pela distância (padrão desde 2026-11-05; apTrocaJanelaVelho volta): 3,4 s não dá pra chegar de 250+ (51% das trocas); tempo andando + 1,2 s pra abrir, até 6 s */
                    float pl = NavGrid.PathLenAte(me.pos, matador, 700); if (!float.IsInfinity(pl)) tradeUntil = m.t + Math.Clamp(pl / me.MaxSpeed + 1.2f, 3.4f, 6f);
                } tradeMatador = killer; tradeViu = false; tradeMira = matador;
                if (Match.Auditar)
                {   /* auditoria da troca: sabia onde o matador estava? a que distância (andando) eu estava? */
                    bool sabia = ContatoDe(killer) != null; trocaAudAte = tradeUntil; trocaTicks = 0; trocaJuizTicks = 0;
                    m.Aud("troca: começou — sabia do matador", sabia ? "sim (contato)" : "não (vou onde o colega caiu)");
                    float dl = (matador - me.pos).Len; m.Aud("troca: começou — distância até o alvo", dl < 150 ? "a <150" : dl < 250 ? "b 150-250" : dl < 350 ? "c 250-350" : "d 350+");
                }
                if (EXP.Contains("trocaPico") && !EXP.Contains("trocaPicoVelho"))   /* opt-in: swing na troca deu TR +1,9 e retake −3 */
                {   // troca de pro é o swing: o primeiro ponto do caminho de onde eu vejo onde ele estava, já mirando lá — não
                    // andar até o lugar dele (dava a volta na quina e quase nunca via o matador: 2 vezes em 450 rounds)
                    var cam = NavGrid.Path(me.pos, matador);
                    float zm = NavGrid.ZAt(NavGrid.Snap(matador));
                    if (cam != null)
                        foreach (var w in cam)
                            if (MapData.LosWalk(w.p, w.z, matador, zm, 18f)) { tradePos = w.p; break; }
                }
                if (EXP.Contains("trocaCruzada"))   // A/B: ângulo cruzado na troca ficou neutro/levemente pior (trocas 14,1→13,4) — desligado
                {   // não entro pelo mesmo caminho em que ele acabou de matar (ele está mirando ali): outro ângulo se tiver,
                    // e com flash, banga ele antes de abrir (quem sabe usar utilitária faz mais)
                    V2 cruz = AnguloCruzado(dead, matador); if ((cruz - matador).Len > 1) tradePos = cruz;
                    tradeFlash = EXP.Contains("trocaComFlash") && me.nades.Contains("flash") && rng.Rand() < 0.35f + 0.45f * me.skill.utilitaria;   // A/B: bangar antes atrasa a troca (o matador sai do ângulo) — desligado
                }
                Think($"{dead.name} morreu do {killer.name} — vou trocar");
            }
            else Think($"{dead.name} caiu perto — segurando o ângulo dele");
        }

        /// <summary>Alvo da troca alcançável (padrão desde 2026-11-05; apTrocaAlvoVelho volta): o tiro ouvido pode vir de
        /// cima de borda/telhado ou de fora da malha — aí não tem caminho e o bot fica "indo trocar" parado. Vai pro ponto
        /// do chão mais perto de onde ele está que tem linha pra ele e que eu alcanço; sem isso, onde o colega caiu.</summary>
        V2 AlvoTroca(V2 matador, V2 caiu)
        {
            if (!float.IsInfinity(NavGrid.PathLenAte(me.pos, matador, 900))) return matador;
            float zm = NavGrid.ZAt(NavGrid.Snap(matador)); int testes = 0;
            for (float r = 8; r <= 160 && testes < 12; r += 8)
                for (int k = 0; k < 16 && testes < 12; k++)
                {
                    V2 c = matador + V2.Ang(k * MathF.PI / 8) * r;
                    if (c.x < 0 || c.y < 0 || c.x >= MapData.NX || c.y >= MapData.NY || !NavGrid.Movable(c)) continue;
                    if (!MapData.LosWalk(c, NavGrid.ZAt(c), matador, zm, 18f)) continue;
                    testes++;
                    if (!float.IsInfinity(NavGrid.PathLenAte(me.pos, c, 900))) { if (Match.Auditar) m.Aud("troca: alvo inalcançável", "chão com linha pra ele"); return c; }
                }
            if (Match.Auditar) m.Aud("troca: alvo inalcançável", float.IsInfinity(NavGrid.PathLenAte(me.pos, caiu, 900)) ? "nada (ficou o antigo)" : "onde o colega caiu");
            return float.IsInfinity(NavGrid.PathLenAte(me.pos, caiu, 900)) ? matador : caiu;
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
                if (ocupada && !EXP.Contains("zonaVelha") && HoldLivrePerto(nx.hold) is Hold livre) { nx = new Order { name = livre.name, hold = livre, why = $"{nx.hold.name} já tem gente — fico com {livre.name}", doPlano = nx.doPlano && !EXP.Contains("livreSemPlano") }; m.Ev("trocou pra posição livre ao voltar/round"); }
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
            // bomb dominado e eu com o C4: a prioridade é plantar (as smokes das saídas viram pós-plant) — o pro não fica
            // jogando util com o bomb limpo e o relógio da rotação correndo
            bool plantaJa = false;
            if (o.plant && me.hasBomb && !m.bombPlanted && o.nades.Count > 0 && !EXP.Contains("plantaPrimeiroVelho"))
            {
                V2 ps0 = m.PlantSpot; var it0 = m.intel[(int)me.team];
                bool perto = (ps0 - me.pos).Len < 420;
                bool limpo = it0.Esperados(ps0, 380) < 0.5f && !it0.contacts.Values.Any(c => t - c.t < 3 && m.agents[c.id].alive && (c.pos - ps0).Len < 450);
                if (perto && limpo) { plantaJa = true; if (t - plantaJaLog > 3) { plantaJaLog = t; m.Ev("plant: bomb dominado, plantou antes da util/round"); Think("bomb limpo: planto primeiro, a util fica pro pós-plant"); } }
            }
            // granadas na fila
            if (o.nades.Count > 0 && me.alive && !plantaJa)
            {
                var n = o.nades[0];
                if (!me.nades.Contains(n.kind))
                {   // não comprei/gastei: se pedi e alguém dropou (ou vai), pego no caminho; senão a lineup fica pra outra
                    bool esperando = !EXP.Contains("semPedirItem") && meuPedidoItem != null && meuPedidoItem.nade == n.kind && t - meuPedidoItem.t < 4;
                    if (!esperando) { o.nades.RemoveAt(0); Think($"sem {n.kind} — {n.name} fica pra outra"); return; }
                    Esperando(null); fase = "esperando o colega dropar"; return;
                }
                if ((n.from - me.pos).Len > 14)
                {
                    // chegando: saca a granada antes (o saque de 1 s acontece andando), se está calmo — ninguém visto agora
                    if (!EXP.Contains("semPreSaque") && !EXP.Contains("sacaGranadaVelho") && me.nadeNaMao == null && (n.from - me.pos).Len < 110 && target == null && t - lastSeenT > 2.5f && t >= me.sacaAte)
                    { me.nadeNaMao = n.kind; me.nadeNaMaoDesde = t; me.sacaAte = float.MaxValue; m.Ev("granada: pré-sacou chegando na lineup/round"); }
                    MoveTo(n.from, true); ClearAngles(false); Think($"indo jogar {n.name}"); return;
                }
                if (!CaiNoLugar(n.kind, n.to, n.name)) { o.nades.RemoveAt(0); return; }
                if (!EXP.Contains("utilAToaVelho"))
                {   // lineup do plano também não sai à toa: ninguém deles vivo, ou (bomb já plantado) flash/molotov/HE longe do
                    // bomb onde ninguém é esperado — o plano era de antes do plant
                    var eles = me.IsT ? Team.CT : Team.T;
                    bool semNinguem = m.Alive(eles) == 0;
                    bool longeDoBomb = n.kind != "smoke" && m.bombPlanted && (n.to - m.bombPos).Len > 700 && m.intel[(int)me.team].Esperados(n.to, 350) < 0.08f;
                    if (!semNinguem && !longeDoBomb && n.kind == "smoke" && SmokeJaCobre(n.to))
                    { m.Ev("utilitário: guardou a smoke — o lugar já está smokado/round"); Think($"{n.name}? já está smokado — guardo a minha"); o.nades.RemoveAt(0); return; }
                    if (semNinguem || longeDoBomb)
                    {
                        m.Ev(semNinguem ? "utilitário: lineup cancelada — nenhum inimigo vivo/round" : "utilitário: lineup cancelada — bomb plantado do outro lado/round");
                        Think($"{n.name}? {(semNinguem ? "não tem mais ninguém deles" : "o bomb está do outro lado e ninguém aqui")} — guardo");
                        o.nades.RemoveAt(0); return;
                    }
                }
                action = "granada"; actionNade = n; actionEnd = SoltaGranada(t, 0.9f);
                Think($"jogando {n.name}");
                return;
            }
            // plantar
            if (o.plant && me.hasBomb && !m.bombPlanted)
            {
                if (!EXP.Contains("plantaOndeEstaVelho"))
                {   // já estou DENTRO do outro bomb, limpo (o IGL trocou o plano pro outro lado): planto aqui — atravessar o mapa
                    // com o C4 pela base CT pro bomb do plano era o bug "passou do A e foi pra base CT"
                    char outroS = m.planSite == 'A' ? 'B' : 'A';
                    var itS = m.intel[(int)me.team];
                    if (MapData.SITES[outroS.ToString()].Contains(me.pos.x, me.pos.y) && itS.Esperados(me.pos, 300) < 1.2f
                        && !itS.contacts.Values.Any(c => t - c.t < 2f && m.agents[c.id].alive && (c.pos - me.pos).Len < 350))   /* atravessar pela base CT com o C4 é pior que plantar com risco */
                    { m.planSite = outroS; m.Ev("C4: plantou no bomb onde já estava (plano era o outro)/round"); Think($"tô no {outroS} limpo com o C4 — planto aqui"); }
                }
                V2 ps = m.PlantSpot;
                if ((ps - me.pos).Len > 10 && C4NaFrente(ps)) { Esperando(null); fase = "C4 atrás de quem abre"; return; }
                if (Match.Auditar && ((int)(t / Match.DT)) % 8 == 0)
                {   // validador: portador que ENTROU no bomb do plano e SAIU sem plantar (o bug que o usuário viu no A)
                    bool dentro = MapData.SITES[m.planSite.ToString()].Contains(me.pos.x, me.pos.y);
                    if (dentro) { c4DentroDesde = c4DentroDesde < 0 ? t : c4DentroDesde; c4FoiDentro = true; }
                    else if (c4FoiDentro && c4DentroDesde >= 0)
                    {
                        m.Ev("C4: entrou no bomb e saiu sem plantar/round");
                        Match.GuardaExemplo("C4 entrou no bomb e saiu sem plantar", m.seedRound, m.t, $"{me.name} saiu do {m.planSite} ({Region(me.pos)}) com o C4 depois de {t - c4DentroDesde:0.0}s dentro; ordem {order.name}; fase {fase}; ação {action}; alvo {(target != null ? target.name : "-")}; plant em {Region(m.PlantSpot)} a {(m.PlantSpot - me.pos).Len:0}");
                        c4DentroDesde = -1;
                    }
                }
                if (Match.Auditar && ((int)(t / Match.DT)) % 16 == 0)
                {   // validador do bug "passou do bomb com o C4 e foi pra base CT": portador com ordem de plantar longe do bomb
                    bool noSite = MapData.SITES[m.planSite.ToString()].Contains(me.pos.x, me.pos.y);
                    float dBase = (me.pos - MapData.SPAWN_CT[2]).Len, dPs = (ps - me.pos).Len;
                    if (dBase < 380 && dPs > 150)
                    {
                        m.Ev("C4: portador com ordem de plantar perto da base CT/round");
                        if (((int)(t / Match.DT) * 7919 + me.id) % 5 == 0)
                            Match.GuardaExemplo("C4 indo pra base CT", m.seedRound, m.t, $"{me.name} em {Region(me.pos)} com o C4 ({order.name}, {fase}); plant {m.planSite} em {Region(ps)} a {dPs:0}; ps andável {NavGrid.Movable(ps)}");
                    }
                }
                if ((ps - me.pos).Len > 10) { MoveTo(ps, true); ClearAngles(true); Think($"levando o bomb pro {m.planSite}"); return; }
                if (t < fakeSegura) { Esperando(null); Face(DeOndeAparece(), 3); fase = "segurando depois do fake"; return; }
                if (fakeSegura > 0) { fakeSegura = -9; decidiuFakePlant = false; }   // segurei e ninguém veio: decide de novo (fingir de novo ou fazer)
                if (!decidiuFakePlant)
                {
                    decidiuFakePlant = true;
                    if (Fingir(true, m.RoundLeft - Match.PLANT - 4))
                    { action = "fake plant"; actionEnd = t + rng.Uniform(0.5f, 1.2f); m.PlantStart(me); Think("fake plant — começo o plant pra puxar quem vier cortar"); return; }
                }
                action = "plantar"; actionEnd = t + Match.PLANT; m.PlantStart(me);
                Think("plantando"); return;
            }
            // desarmar
            if (o.defuse && m.bombPlanted)
            {
                // chegando no bomb: desarmar direto ou limpar antes (caçar quem ainda está ali, ouvindo os passos)? Leitura:
                // devem ter TR em volta e sobra tempo → limpa; vazio ou relógio apertado → desarma
                float dB = (m.bombPos - me.pos).Len, sobra = m.BombLeft - Match.DefuseTime(me);
                if (dB < 170 && !decidiuLimpar && !EXP.Contains("semLimpar"))
                {
                    decidiuLimpar = true;
                    float eles = m.intel[(int)me.team].Esperados(m.bombPos, 450);
                    float limpar = 0.6f * MathF.Min(eles, 2) + (sobra > 12 ? 0.5f : 0) + (m.Alive(me.team) >= 2 ? 0.2f : 0) + 0.3f * me.skill.composure;
                    float direto = 0.6f + (sobra < 7 ? 1.2f : 0) + (eles < 0.3f ? 0.9f : 0);
                    if (limpar + rng.Normal(0, 0.3f) > direto + rng.Normal(0, 0.3f))
                    { limparAte = m.t + MathF.Min(sobra - 5, rng.Uniform(3f, 7f)); m.Ev("retake: limpou antes de desarmar/round"); }
                }
                if (m.t < limparAte && sobra > 5 && target == null)
                {
                    fase = "limpando antes do defuse";
                    bool ouviu = m.t - somT < 2.5f && somK == "passos" && (somP - me.pos).Len < 400;
                    if (ouviu) { Esperando(new[] { Pixel(somP) }); Think($"ouvi passo em {Region(somP)} — seguro antes de desarmar"); }
                    else { if (dB > 40) MoveTo(m.bombPos, false); ClearAngles(true); Think("limpando o bomb antes de desarmar"); }
                    return;
                }
                if (dB > 8) { MoveTo(m.bombPos, !o.quiet); ClearAngles(true); Think("indo desarmar"); return; }
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
                        action = "granada"; actionNade = new Lineup("smoke no bomb (smoke defuse)", "smoke", me.pos, m.bombPos + V2.Ang(rng.Uniform(0, 6.28f)) * 6); actionEnd = SoltaGranada(t, 0.6f);
                        esperaSmoke = t + 0.6f + 1.8f;
                        Think($"smoke defuse: {tr} TR vivo{(tr > 1 ? "s" : "")}{(perto ? ", tem gente perto" : "")}, {folga:0.0}s de folga"); return;
                    }
                }
                if (t < fakeSegura) { Esperando(null); Face(DeOndeAparece(), 3); fase = "segurando depois do fake"; return; }
                if (fakeSegura > 0) { fakeSegura = -9; decidiuFake = false; }
                if (!decidiuFake)
                {
                    decidiuFake = true;
                    if (Fingir(false, m.BombLeft - dt - 1))
                    { action = "fake defuse"; actionEnd = t + rng.Uniform(0.35f, 0.9f); m.DefuseStart(me); Think("fake defuse — toco no bomb pra puxar o TR"); return; }
                }
                action = "desarmar"; actionEnd = t + dt; m.DefuseStart(me);
                Think($"desarmando ({dt:0}s{(me.hasKit ? " com kit" : " sem kit")}, faltam {m.BombLeft:0.0}s)"); return;
            }
            // seguir o entry (suporte troca)
            if (o.follow != null && o.follow.alive)
            {
                V2 back = o.follow.pos - V2.Ang(o.follow.aim) * o.followDist;
                if (o.follow.Speed < 3 && (o.follow.pos - me.pos).Len < 60) { Esperando(null); fase = "colado no entry"; Think($"o {o.follow.name} parou — espero colado"); }   // ele esperando (molotov, smoke): eu também
                else if ((back - me.pos).Len > 25)
                {
                    MoveTo(NavGrid.Movable(back) ? back : o.follow.pos, !o.quiet); ClearAngles(true);
                    // quem vai trocar pré-mira onde o entry vai tomar tiro (o duelo dele, ou pra onde ele olha), passando pelo ombro
                    if (!EXP.Contains("semPreTroca"))
                    {
                        var dele = o.follow.brain.target;
                        if (dele != null && ContatoDe(dele) is V2 cd) Face(PorOmbro(Pixel(cd)), 3);
                        else if (rng.Rand() < 0.04f || preTrocaAte > t) { if (preTrocaAte <= t) preTrocaAte = t + rng.Uniform(0.6f, 1.6f); Face(PorOmbro(o.follow.pos + V2.Ang(o.follow.aim) * 220), 3); }
                    }
                    Think($"atrás do {o.follow.name} pra trocar");
                }
                else Esperando(null);
                return;
            }
            V2 dest = o.hold != null ? o.hold.pos : o.dest;
            // chegou = perto E na altura (do lado da caixa, embaixo, não é "em cima da caixa": tem que pular)
            // a C4 caída só é pega passando quase por cima (Match.RAIO_PEGA, ímã): quem vai buscar ela só "chegou" ali dentro
            float chega = me.IsT && m.bombDropped && (dest - m.droppedBomb).Len < 2 && Match.RAIO_PEGA < 12 ? Match.RAIO_PEGA - 2 : 12;
            if (baseP == null && ((dest - me.pos).Len > chega || MathF.Abs(NavGrid.ZAt(NavGrid.Snap(dest)) - me.z) > 6 || me.noAr))   // no meio do pulo não chegou
            {
                // o colega avisou "vou bangar" pra onde eu estou indo: espero estourar e entro junto
                bool minhaBang = me.bangAmigaDe == me && !EXP.Contains("abreJuntoVelho");
                if ((o.push || minhaBang) && t < me.bangAmigaEm - 0.3f && (me.bangAmigaPos - me.pos).Len < 450 && (me.bangAmigaPos - dest).Len < (me.pos - dest).Len + 60)
                {
                    // fico atrás da quina (sem dar o peek antes) e saio no estouro: quem estiver ali olhando está cego
                    Esperando(null); Face(me.bangAmigaPos, 3); fase = "esperando a flash estourar";
                    if (!esperouBang) { esperouBang = true; m.Ev(minhaBang ? "flash: abriu junto com a própria/round" : "flash: abriu junto com a do colega/round"); }
                    Think(minhaBang ? "bangei — espero estourar e abro junto" : $"o {me.bangAmigaDe.name} vai bangar — espero estourar pra entrar"); return;
                }
                esperouBang = false;
                if (SegurarContraVarios(t)) return;
                if (CuidadoComMatador(t, dest)) return;
                if (o.push && !o.plant && EsperaOGrupo(t, dest)) { Esperando(null); fase = "esperando o colega pra entrar junto"; return; }
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
                float pl = NavGrid.PathLenAte(me.pos, s.p, 480); if (pl <= 0 || pl > 480) continue;
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
            fase = "malotando"; me.crouch = true; Face(e.pos, Combat.AimHeight(me, (e.pos - me.pos).Len, e.duck > 0.5f) - me.Eye + e.z - me.z);
            reactAt = MathF.Max(reactAt, t + 0.05f);   // ainda não atiro (mas já estou mirando nele)
            Think($"deixando o {e.name} passar ({passaram.Count}/{o.deixaPassar})");
            return true;
        }
        readonly HashSet<int> passaram = new HashSet<int>(); float passouUlt;

        /// <summary>Quando a granada sai da mão: sacar leva DEPLOY_NADE (CS2) — se acabou de jogar outra, só começa a sacar depois
        /// que o braço termina. A mira (ajustar o ponto) acontece junto com o saque.</summary>
        float SoltaGranada(float t, float mira)
        {
            if (EXP.Contains("sacaGranadaVelho")) return t + mira;
            float ini = me.nadeNaMao != null ? me.nadeNaMaoDesde : MathF.Max(t, me.ultArremesso + Match.FIM_ARREMESSO);
            return MathF.Max(t + mira, ini + Match.DEPLOY_NADE);
        }

        /// <summary>
        /// Como gente agacha (CS2 pune o spam: cada apertada nova desce mais devagar): agachou no duelo, fica embaixo entre
        /// os tiros e as rajadas — levanta quando o duelo acaba ou quando vai andar. E cansado (agachar lento) não aperta de
        /// novo por qualquer coisa: só pra se esconder (cego, malote) ou se já estava descendo.
        /// </summary>
        /// <summary>Estava com a granada sacada (pré-saque) e não vai jogar agora: volta pra arma (leva o tempo de sacar).</summary>
        void GuardaGranada(float t, string porque)
        {
            if (me.nadeNaMao == null) return;
            me.nadeNaMao = null; me.sacaAte = t + me.TempoSacar; Think(porque);
        }

        public void SeguraAgachado(float t)
        {
            if (Agent.AgacharVelho || EXP.Contains("agacharSemJeito")) return;
            if (me.crouch && !me.CrouchAntes && me.duckVel < 4.5f && me.duckCorpo < 0.05f && fase != "malotando" && !me.MeioCego(t))
            { me.crouch = false; m.Ev("agachar: cansado, não apertou de novo/round"); }
            if (me.crouch) agachouQuer = t;
        }
        float agachouQuer = -9;

        void FinishAction()
        {
            if (action == "granada")
            {
                m.Throw(me, actionNade); order.nades.Remove(actionNade); me.nades.Remove(actionNade.kind); Think($"{actionNade.name} jogada");   // a ordem pode ter mudado no meio do arremesso
                me.ultArremesso = m.t;
                float ts = EXP.Contains("sacarGranadaCurto") ? me.TempoSacarCurto : me.TempoSacar;
                if (me.nadeNaMao != null) { me.nadeNaMao = null; me.sacaAte = m.t + Match.FIM_ARREMESSO + ts; }
                else if (!EXP.Contains("sacaGranadaVelho")) me.sacaAte = MathF.Max(me.sacaAte, m.t + Match.FIM_ARREMESSO + ts);   // CS: depois de jogar, volta pra arma sacando
            }   // a ordem pode ter mudado no meio do arremesso
            else if (action == "plantar") { m.Plant(me); Think("bomb plantado"); }
            else if (action == "desarmar") { m.Defuse(me); Think("desarmei"); }
            else if (action == "fake defuse")
            {   // tirei a mão: o barulho já foi — quem vier pegar o defuse aparece no meu ângulo
                m.ParouDefuse(me);
                fakeSegura = m.t + rng.Uniform(1.5f, 3.2f); Think("fake defuse — tirei a mão, segurando quem vier");
            }
            else if (action == "fake plant")
            {   // finta do plant: o CT que ouviu vem cortar e cai no meu ângulo
                if (m.plantando == me) m.plantando = null;
                fakeSegura = m.t + rng.Uniform(1.5f, 3.5f); Think("fake plant — parei, segurando quem vier cortar");
            }
            action = null;
        }

        float fakeSegura = -9, limparAte = -9; bool decidiuFake, decidiuFakePlant, decidiuLimpar; int fakes;
        /// <summary>De onde o inimigo deve aparecer pra cortar/pegar o defuse: o último contato dele, senão o lado mais quente.</summary>
        V2 DeOndeAparece()
        {
            var inim = me.IsT ? Team.CT : Team.T; V2? best = null; float bt = -99;
            foreach (var c in m.intel[(int)me.team].contacts.Values)
                if (m.agents[c.id].alive && m.agents[c.id].team == inim && c.t > bt && (c.pos - me.pos).Len < 900) { bt = c.t; best = c.pos; }
            if (best != null) return Pixel(best.Value);
            return DeOndeVem() is V2 v ? Pixel(v) : me.pos + V2.Ang(me.aim) * 100;
        }

        /// <summary>
        /// Fingir (defuse ou plant) ou fazer de verdade: não tem regra de número — vale em 1x1 ou em 5x5. Pesa se deve ter
        /// alguém por perto pra cair na finta, quanto tempo sobra pra fingir e ainda fazer, quantos são de cada lado, se tem
        /// colega cobrindo (com cobertura a finta vira troca fácil) e quantas fintas já fiz (ele já sabe).
        /// </summary>
        bool Fingir(bool plant, float folga)
        {
            if (EXP.Contains("semFake")) return false;
            var inim = me.IsT ? Team.CT : Team.T; int eles = m.Alive(inim), nos = m.Alive(me.team);
            if (!plant) m.Aud("fake defuse: chegou no bomb", eles == 0 ? "TR todos mortos" : folga < 2.5f ? "sem folga" : fakes >= 2 ? "já fingiu 2" : "decidindo");
            if (eles == 0 || folga < 2.5f || fakes >= (plant ? 1 : 2)) return false;
            float perto = m.intel[(int)me.team].Esperados(me.pos, 650);
            // fake plant só faz sentido com CT perto pra ouvir e vir cortar; fake defuse vale sempre que tem TR vivo
            // (ele vai ouvir de onde estiver segurando)
            if (plant && perto < 0.4f) return false;
            bool cobertura = m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 300);
            if (plant && !EXP.Contains("fakeVelho"))
            {   // fake plant como pro: é pra quem ACHA que tem CT bem perto, escondido, pronto pra sair no barulho do plant —
                // mais em fim de round com pouca gente (1x1, 2x2); no execute 5x5 o tempo vale mais (a rotação está vindo)
                float colado = m.intel[(int)me.team].Esperados(me.pos, 380);
                if (colado < 0.6f) return false;
                float poucos = eles <= 1 ? 0.45f : eles == 2 ? 0.25f : eles == 3 ? -0.2f : -0.6f;
                float fingirP = 0.5f * MathF.Min(colado, 2) + poucos + (cobertura ? 0.2f : 0) + 0.15f * me.skill.composure + (folga > 8 ? 0.1f : 0) - 0.6f * fakes;
                float fazerP = 1.45f + (folga < 6 ? 0.6f : 0) + (nos > eles + 1 ? 0.4f : 0);
                fingirP += rng.Normal(0, 0.3f); fazerP += rng.Normal(0, 0.3f);
                m.Aud("fake plant: decisão", fingirP > fazerP ? $"fingiu ({nos}x{eles})" : $"plantou ({nos}x{eles})");
                if (fingirP <= fazerP) return false;
                fakes++; m.Ev("fake plant/round"); return true;
            }
            float fingir = 0.7f * MathF.Min(perto, 2) + (folga > 6 ? 0.4f : 0.1f) + (nos <= eles ? 0.25f : 0) + (cobertura ? 0.25f : 0) + 0.2f * me.skill.composure - 0.35f * fakes;
            float fazer = (plant ? 0.9f : 0.5f) + (perto < 0.25f ? (plant ? 0.9f : 0.4f) : 0) + (folga < 4 ? 0.9f : 0) + (nos > eles + 1 ? 0.3f : 0);
            fingir += rng.Normal(0, 0.3f); fazer += rng.Normal(0, 0.3f);
            if (!plant) m.Aud("fake defuse: decisão", fingir > fazer ? "fingiu" : $"desarmou (perto {MathF.Min(perto, 2):0.0}, folga {(folga > 6 ? "6+" : "<6")})");
            if (fingir <= fazer) return false;
            fakes++;
            m.Ev(plant ? "fake plant/round" : "fake defuse/round");
            return true;
        }

        // ================================================================ segurar ativo
        /// <summary>
        /// Pra onde olhar segurando, sem mirar nas costas do colega: se ele está parado na frente, na linha do ângulo, quem
        /// segura aquele ângulo é ele (e o meu tiro sairia nele) — pego outro ângulo da posição, a borda de onde vem
        /// gente, ou abro uns 35° pro lado dele que tem mais perigo.
        /// </summary>
        V2 LookAlvo(V2[] look)
        {
            V2 r = LookAlvo0(look);
            if (EXP.Contains("miraNoColega") || target != null || ColegaNaFrente(r) == null) return r;
            foreach (var L in look) if (ColegaNaFrente(L) == null && Ameaca(L) > 0.002f) return Fonte("ângulo livre do colega", L);
            if ((Borda() ?? DeOndeVem()) is V2 vem && ColegaNaFrente(vem) == null) return Fonte("borda livre do colega", vem);
            float a0 = (r - me.pos).Atan2, dl = MathF.Min((r - me.pos).Len, 300);
            V2 e = me.pos + V2.Ang(a0 + 0.6f) * dl, d = me.pos + V2.Ang(a0 - 0.6f) * dl;
            return Fonte("do lado do colega", Ameaca(e) >= Ameaca(d) ? e : d);
        }
        /// <summary>
        /// O pixel que eu miro esperando (a quina por onde ele apareceria) também não pode ser as costas do colega: o
        /// LookAlvo checa o ponto, mas o pixel do caminho até ele pode cair noutra direção — então checa o pixel também.
        /// </summary>
        V2 PixelSemColega(V2[] look)
        {
            V2 px = Pixel(LookAlvo(look));
            if (EXP.Contains("esperaMiraVelha") || target != null || ColegaNaFrente(px) == null) return px;
            foreach (var L in look) { var q = Pixel(L); if (ColegaNaFrente(q) == null && Ameaca(L) > 0.002f) return q; }
            float a0 = (px - me.pos).Atan2, dl = MathF.Min((px - me.pos).Len, 300);
            V2 e = me.pos + V2.Ang(a0 + 0.6f) * dl, d = me.pos + V2.Ang(a0 - 0.6f) * dl;
            return Ameaca(e) >= Ameaca(d) ? e : d;
        }
        /// <summary>Colega parado entre mim e L, quase na linha (ele tapa o ângulo).</summary>
        Agent ColegaNaFrente(V2 L)
        {
            V2 dir = L - me.pos; float dl = dir.Len; if (dl < 30) return null; dir = dir / dl;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                V2 rel = x.pos - me.pos; float ao = rel.Dot(dir);
                // a mira não para no ponto: colega logo depois dele também tapa (e fica com a mira nas costas)
                if (ao < 20 || ao > (EXP.Contains("colegaAteAlvo") ? dl : MathF.Max(dl, 200))) continue;
                if (MathF.Abs(rel.x * dir.y - rel.y * dir.x) < 8 + 0.05f * ao) return x;
            }
            return null;
        }
        V2 LookAlvo0(V2[] look)
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
            // passo/granada colado e agora: vira na hora, antes do call (é o que eu mesmo ouvi, aqui do lado)
            if (!EXP.Contains("somVelho") && m.t - somT < 1.5f && (somP - me.pos).Len < 300) { m.Aud("passo que virou a mira", "ouviu colado"); return Fonte("som colado", somP); }
            var call = Chamada(650) ?? FocoMorte();
            if (call != null && (Ameacado(call.Value) || !Lotado(call.Value))) return Fonte("call/morte", call.Value);
            if (m.t < lookFocoAte && !m.SmokeBlocks(me.pos, me.z + me.Eye, lookFoco, NavGrid.ZAt(lookFoco) + 18)) return Fonte("pixel fora da smoke", lookFoco);   // pixel novo por fora da smoke
            // ângulo smokado: não fica olhando a fumaça — pega outro (se todos estão smokados, segura a saída da smoke)
            for (int k = 0; k < look.Length && m.smokes.Count > 0; k++)
            {
                V2 L0 = look[holdLook % look.Length];
                if (!m.SmokeBlocks(me.pos, me.z + me.Eye, L0, NavGrid.ZAt(L0) + 18)) break;
                if (!EXP.Contains("bxSaidaSmokeVelho") && SaidaSmoke(L0) is V2 sai) return Fonte("saída da smoke", sai);
                if (k == 0) Think($"smoke em {Region(L0)} — seguro outro ângulo");
                holdLook = (holdLook + 1) % look.Length;
            }
            if (m.t - somT < SomVale && (somP - me.pos).Len < 400)
            {
                // o passo do lado de um ângulo que eu seguro: ele vai sair pela MINHA quina — a mira fica nela (virar pro som
                // tirava a mira de onde ele aparece: 15% das miras fora do CT segurando)
                if (EXP.Contains("passoNaQuina"))   /* opt-in: no A/B piorou o retake (mirar no som já acertava 18% pré-mirado) */
                {
                    float aS = (somP - me.pos).Atan2;
                    foreach (var L in look)
                        if (MathF.Abs(MathX.AngDiff((L - me.pos).Atan2, aS)) < 35f * MathF.PI / 180f) { m.Aud("passo que virou a mira", "fiquei na quina dele"); return Fonte("passo pela quina", L); }
                }
                m.Aud("passo que virou a mira", "ele ouviu"); return Fonte("passo", somP);
            }
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

        /// <summary>Segurando: se outro ângulo da posição é quase tão perigoso e fica perto (5-40°), a mira fica entre os
        /// dois, puxando pro mais perigoso (o pro "divide" dois ângulos próximos em vez de olhar só um).</summary>
        V2 EntreAngulos(V2[] look, int i)
        {
            V2 L = look[i];
            if (EXP.Contains("semEntre") || look.Length < 2) return L;
            float vi = Ameaca(L), aL = (L - me.pos).Atan2, v2 = 0; V2? p2 = null;
            for (int k = 0; k < look.Length; k++)
            {
                if (k == i || CobertoPor(look[k]) != null) continue;
                float dA = MathF.Abs(MathX.AngDiff((look[k] - me.pos).Atan2, aL)) * 180 / MathF.PI, v = Ameaca(look[k]);
                if (dA < 5 || dA > 40 || v < 0.5f * vi || v <= 0.005f) continue;
                if (v > v2) { v2 = v; p2 = look[k]; }
            }
            return p2 is V2 q ? Entre(L, q, v2 / (vi + v2)) : L;
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
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && x.Primaria == "awp") return 4;   // já tem AWP no time
            return 7.5f;   // melhor que SMG/pistola, pior que rifle na mão de quem não é AWPer
        }

        Match.Drop MelhorDrop(float raio, float ganhoMin)
        {
            Match.Drop best = null; float bv = ValorArma(me.gunId) + ganhoMin;
            foreach (var d in m.drops)
            {
                if (d.para != null && d.para != me && m.t - d.t < 6) continue;   // dropada pra um colega: é dele
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
                bool timeTemAwp = m.agents.Any(x => x.alive && x.team == me.team && x.Primaria == "awp");
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
            bool comPressa = order.push || order.defuse || m.bombPlanted && !me.IsT;   // entrando no bomb não volta atrás por arma: só se está no pé
            var d = MelhorDrop(comPressa ? 45 : 130, 1.5f);   // tem que ser bem melhor pra parar no meio do round (pistola→pistola melhor, SMG→rifle...)
            if (d == null) return false;
            if (m.intel[(int)me.team].Esperados(me.pos, 350) > 0.3f) return false;   // perigoso parar pra isso agora
            fase = "pegando arma";
            if ((d.pos - me.pos).Len > 8) { MoveTo(d.pos, true); Face(d.pos, 3); Think($"seguro aqui: pegando a {WeaponStats.ALL[d.gun].name} do chão"); }
            else m.Pegar(me, d);
            return true;
        }

        /// <summary>
        /// Começo do round: o que comprei pro colega (rifle pro pobre, AWP pro AWPer, smoke/flash pra quem faz o lineup)
        /// eu dropo na direção dele — chego perto se ele já se afastou; passados 10 s, já era (o round começou).
        /// </summary>
        bool EntregarCompra(float t)
        {
            if (me.entregar.Count == 0 || action != null) return false;
            if (t > 10 || target != null) { me.entregar.Clear(); return false; }
            if (entregaEm < 0) entregaEm = rng.Uniform(0.2f, 1.4f);   // cada um no seu tempo (ninguém dropa no mesmo tique)
            if (t < entregaEm) return false;
            var (para, item) = me.entregar[0];
            if (!para.alive) { me.entregar.RemoveAt(0); return false; }
            fase = "dropando pro colega";
            string nome = WeaponStats.ALL.ContainsKey(item) ? WeaponStats.ALL[item].name : item;
            // perto (até ~220): joga na direção dele e ele vem buscar (como no CS2). Longe: vai até onde ele estava, num ponto
            // que só muda a cada 1 s — seguir o colega andando era um caminho novo por tique (destino nunca repete no cache)
            if ((para.pos - me.pos).Len > (EXP.Contains("dropPerto") ? 90 : 220))
            {
                if (t > entregaAlvoT) { entregaAlvoT = t + 1; entregaAlvo = NavGrid.Snap(para.pos); }
                MoveTo(entregaAlvo, false); Face(para.pos, 3); Think($"levando {nome} pro {para.name}"); return true;
            }
            Face(para.pos, 3);
            m.Entregar(me, para, item); me.entregar.RemoveAt(0); entregaEm = t + rng.Uniform(0.3f, 0.6f);
            return true;
        }
        float entregaEm = -1, entregaAlvoT = -1; V2 entregaAlvo;
        public bool Reagindo => m.t < levouAte;
        public float ViuEm(Agent x) => m.intel[(int)me.team].contacts.TryGetValue(x.id, out var c) ? c.t : -99;

        /// <summary>Colega dropou arma pra mim (compra): vou lá e pego — é minha, ninguém mais pega nos primeiros segundos.</summary>
        bool PegarEntrega(float t)
        {
            bool velho = EXP.Contains("dropVelho");
            if (!velho && action == null && target == null && t < 10)
            {   // o colega comprou arma pra mim e ainda não dropou: espero na base olhando pra ele (pro fica e pega)
                Agent vem = null;
                foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team) foreach (var (pa, it) in x.entregar) if (pa == me && WeaponStats.ALL.ContainsKey(it)) { vem = x; break; }
                if (vem != null) { Esperando(null); Face(vem.pos, 3); fase = "esperando o drop"; Think($"o {vem.name} vai dropar pra mim — espero"); return true; }
            }
            if (action != null || target != null || m.drops.Count == 0 || t > 25) return false;   // roda todo tique: sem LINQ (aloca no Mono)
            Match.Drop d = null;
            float raio = velho ? 250 : t < 20 ? 650 : 250;   // já saí andando: no começo do round volto pra buscar (a arma é minha)
            foreach (var x in m.drops) if (x.para == me && x.pegouPor == null && t - x.t < (velho ? 12 : 18) && (x.pos - me.pos).Len < raio) { d = x; break; }
            if (d == null) return false;
            if (!velho && (d.pos - me.pos).Len > 250 && m.intel[(int)me.team].Esperados(d.pos, 400) > 0.4f) return false;   // voltar só se é seguro
            fase = "pegando o drop";
            if ((d.pos - me.pos).Len > 8) { MoveTo(d.pos, false); Face(d.pos, 3); Think($"pegando a {WeaponStats.ALL[d.gun].name} que o {d.de?.name} dropou"); }
            else m.Pegar(me, d);
            return true;
        }

        /// <summary>Colega dropou granada pra mim, perto e sem perigo: vou lá pegar.</summary>
        bool PegarGranada(float t)
        {
            if (action != null || m.itensChao.Count == 0 || me.hasBomb && order.plant) return false;
            var it = m.itensChao.FirstOrDefault(x => x.para == me && (x.pos - me.pos).Len < 220 && Match.CabeGranada(me, x.nade));
            if (it == null) return LootGranada(t);
            if (m.intel[(int)me.team].Esperados(me.pos, 300) > 0.5f) return false;
            fase = "pegando granada"; MoveTo(it.pos, true); Think($"pegando a {it.nade} que o {it.de?.name} dropou");
            return true;
        }

        /// <summary>
        /// Lootear: granada caída de quem morreu (colega ou inimigo) perto de mim. Pesa: o quanto ela me falta (smoke e
        /// flash valem mais, inventário vazio vale mais), o desvio, o perigo ali (corpo de quem morreu costuma estar na
        /// mira de alguém — o que eu acho, não o que é) e a pressa. Decide uma vez por item; às vezes vai e se dá mal.
        /// </summary>
        bool LootGranada(float t) => LootGranada(t, false);
        /// <param name="propor">juiz: só avalia (o melhor item e a chance de valer a pena em lootNota), sem decidir</param>
        bool LootGranada(float t, bool propor)
        {
            lootCand = null; lootNota = 0;
            if (EXP.Contains("semLoot") || target != null || order.defuse && !(!me.IsT && m.bombPlanted && action == null) || order.plant) return false;
            if (lootAlvo != null && (!m.itensChao.Contains(lootAlvo) || !Match.CabeGranada(me, lootAlvo.nade) || t > lootAte)) lootAlvo = null;
            if (lootAlvo != null && !m.over && !EXP.Contains("lootTeimoso"))
            {   // indo pegar e a situação mudou (inimigo visto agora perto, ou um colega caiu aqui): larga o item e volta pro jogo
                var itL = m.intel[(int)me.team];
                bool viuAgora = itL.contacts.Values.Any(c => m.agents[c.id].alive && t - c.t < 1.5f && (c.pos - me.pos).Len < 500);
                bool caiuColega = m.agents.Any(c => !c.alive && c.team == me.team && t - c.diedAt < 2f && (c.pos - me.pos).Len < 500 && c.killedBy != null && c.killedBy.alive);
                if (viuAgora || caiuColega) { lootAlvo = null; lootProx = t + 2f; m.Ev("loot: largou o item (situação mudou)/round"); Think("largo a granada do chão — tem gente aqui"); return false; }
            }
            if (lootAlvo == null)
            {
                if (t < lootProx) { if (propor && lootUltCand != null && m.itensChao.Contains(lootUltCand)) { lootCand = lootUltCand; lootNota = lootUltNota; lootCandPl = lootUltPl; return true; } return false; }
                lootProx = t + 0.7f; lootUltCand = null;
                var intel = m.intel[(int)me.team]; float agr = P.Agr(me.skill.aggression);
                float melhorP = 0; Match.ItemChao melhor = null; float melhorPl = 0;
                // pressa pelo que EU entendo do tempo: CT com bomb plantado tem pressa só se o desvio não cabe na folga
                // (chegar no bomb + desarmar); TR, fim de tempo ou entrando
                bool ctRetake = !me.IsT && m.bombPlanted;
                float folga = ctRetake ? m.BombLeft - NavGrid.PathLenAte(me.pos, m.bombPos, 1500) / (MapCheck.RUN * 0.85f) - Match.DefuseTime(me) : 99;
                bool pressa = !m.over && (me.IsT ? m.RoundLeft < 25 || order.push : ctRetake ? folga < 6 : false);
                bool fimRound = m.over;   // acabou o round: quem sobrou cata o que vê (granada que sobra vai pro próximo round)
                foreach (var x in m.itensChao)
                {
                    if (x.morto == null || !Match.CabeGranada(me, x.nade) || lootVisto.Contains(x)) continue;
                    // o que vale ir buscar depende do que vem: CT indo pro retake quer smoke (cortar a visão de quem segura o
                    // bomb) e, sem kit, o kit vale muito (desarme pela metade); retake provável (bomb plantado ou time perdendo
                    // o bomb) também conta antes do plant
                    bool retakeVem = !me.IsT && (m.bombPlanted || order.name.Contains("retomar") || order.name.Contains("retake"));
                    float alcance = x.nade == "kit" ? (retakeVem ? 320 : 200) : retakeVem && x.nade == "smoke" ? 240 : 170;
                    float d = (x.pos - me.pos).Len; if (d > alcance) continue;
                    // só o que eu vejo no chão (ou vi cair do lado de quem morreu perto de mim)
                    if (!MapData.LosWalk(me.pos, me.z, x.pos, x.z + 2, 18f)) continue;
                    if (!propor) lootVisto.Add(x);
                    float pl = NavGrid.PathLenAte(me.pos, x.pos, alcance * 1.3f); if (float.IsInfinity(pl)) continue;
                    if (ctRetake && pl / (MapCheck.RUN * 0.85f) * 2 > folga - 3) continue;   // ir e voltar não cabe no tempo
                    float valor = x.nade == "kit" ? (retakeVem ? 3.0f : 1.2f)
                        : (x.nade == "smoke" ? (retakeVem ? 1.8f : 1.0f) : x.nade == "flash" ? (retakeVem ? 1.3f : 0.8f) : x.nade == "molotov" ? 0.75f : 0.5f) * (me.nades.Count == 0 ? 1.4f : me.nades.Count >= 3 ? 0.6f : 1f);
                    float perigo = intel.Esperados(x.pos, 300) + intel.HeatAvg(x.pos, 120) * 2;
                    // colega acabou de cair perto: quem matou se expôs atirando e está ali (ainda vivo) — a hora é de cravar
                    // onde ele aparece/trocar, não de olhar pro chão. Some em ~5 s
                    if (!fimRound && !EXP.Contains("lootSemAmeaca"))
                        foreach (var c in m.agents)
                            if (!c.alive && c.team == me.team && c.killedBy != null && c.killedBy.alive && t - c.diedAt < 5f
                                && ((c.pos - me.pos).Len < 600 || (c.pos - x.pos).Len < 400))
                                perigo += 2.5f * (1 - (t - c.diedAt) / 5f);
                    float wVai = valor * (1.2f - pl / (alcance * 1.3f)) * (pressa ? 0.3f : 1f) * (fimRound ? 2.5f : 1f);
                    if (fimRound) perigo *= 0.3f;
                    float wNao = 0.35f + perigo * (1.5f - 0.7f * agr);
                    if (!fimRound && !EXP.Contains("lootTeimoso"))
                    {   // pro não cata granada no meio da troca: inimigo visto há pouco por perto pesa muito, e o perigo onde ela
                        // está conta o dobro (10 s/round catando, 70% com inimigo que ele mesmo achava perto)
                        bool viuPerto = intel.contacts.Values.Any(c => m.agents[c.id].alive && t - c.t < 3f && (c.pos - x.pos).Len < 650);
                        wNao = 0.35f + perigo * (2.6f - 1.0f * agr) + (viuPerto ? 1.6f : 0);
                    }
                    if (propor)
                    {   // o gosto dele por aquele item (repertório, não sorteio a cada passo)
                        float gosto = 0.8f + 0.4f * (((uint)(me.id * 7919 + (int)(x.origem.x * 73 + x.origem.y * 151 + x.t * 977)) % 1000) / 1000f);
                        float pv = wVai / (wVai + wNao) * gosto; if (pv > melhorP) { melhorP = pv; melhor = x; melhorPl = pl; }
                        continue;
                    }
                    if (rng.Weighted(new[] { wNao, wVai }) == 1) { lootAlvo = x; lootAte = t + 4 + pl / 60f; if (x.nade == "kit") m.Ev("kit do chão: foi buscar/round"); else if (retakeVem) m.Ev("granada do chão: buscou antes do retake/round"); m.Ev(fimRound ? "granada do chão: looteou no fim do round/round" : "granada do chão: foi lootear/round"); Think(x.nade == "kit" ? $"vou pegar o kit do {x.morto.name} antes do retake" : $"vou pegar a {x.nade} do {x.morto.name}"); break; }
                }
                if (propor) { lootCand = melhor; lootNota = melhorP; lootCandPl = melhorPl; lootUltCand = melhor; lootUltNota = melhorP; lootUltPl = melhorPl; return melhor != null; }
                if (lootAlvo == null) return false;
            }
            else if (propor) { lootNota = 0.75f; return true; }   // já estou indo: continuar vale
            fase = "looteando granada"; MoveTo(lootAlvo.pos, true); ClearAngles(true);
            return true;
        }
        Match.ItemChao lootAlvo, lootCand, lootUltCand; float lootAte, lootProx, lootNota, lootCandPl, lootUltNota, lootUltPl; readonly HashSet<Match.ItemChao> lootVisto = new HashSet<Match.ItemChao>();

        // ================================================================ troca de itens no meio do round
        /// <summary>
        /// "Dropa uma flash pra mim": quem vai abrir o lugar (entry entrando, ou quem pega o ponto de contato) sem flash
        /// pede pra um colega perto que tem sobrando — o AWP e o suporte costumam ter e ficar atrás. Pede uma vez por
        /// situação, antes do contato; depois ele mesmo banga pra si. Não é regra: só pede quem acha que vai precisar.
        /// </summary>
        void PedirItem(float t)
        {
            if (EXP.Contains("semPedirItem") || t < proxPedidoItem || action != null || target != null) return;
            proxPedidoItem = t + 2f;
            if (meuPedidoItem != null && m.pedidosItem.Contains(meuPedidoItem)) return;
            // o que EU vou precisar agora, pelo que acho que vem (não é lista fixa):
            string quero = null; float vontade = 0;
            // a lineup que o plano me deu e eu não tenho (não comprei/gastei): peço pra quem tem, antes de largar ela
            foreach (var l in order.nades) if (!me.nades.Contains(l.kind) && Match.CabeGranada(me, l.kind)) { quero = l.kind; vontade = 0.9f; break; }
            // vou abrir o lugar (entry entrando, bang pra mim): flash
            if (quero == null && !me.nades.Contains("flash") && Match.CabeGranada(me, "flash") && !m.bombPlanted
                && (order.push && (me.role == Role.Entry || order.follow == null) || micro == Micro.Bang)
                && m.intel[(int)me.team].HeatAvg(order.hold?.pos ?? order.dest, 200) >= 0.15f) { quero = "flash"; vontade = 0.6f; }
            // retake vindo: smoke pra cortar quem segura o bomb
            if (quero == null && !me.IsT && m.bombPlanted && !me.nades.Contains("smoke") && Match.CabeGranada(me, "smoke") && m.BombLeft > 20) { quero = "smoke"; vontade = 0.55f; }
            // âncora segurando o bomb sem nada pra atrasar a entrada: molotov
            if (quero == null && !me.IsT && !m.bombPlanted && me.role == Role.Ancora && me.nades.Count == 0 && Match.CabeGranada(me, "molotov")) { quero = "molotov"; vontade = 0.35f; }
            if (quero == null) return;
            bool temQuem = false;
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 350 && x.nades.Contains(quero) && x.brain.target == null) { temQuem = true; break; }
            if (!temQuem || !Rola($"pedir {quero} " + (int)(t / 10), vontade * (0.6f + 0.5f * P.Agr(me.skill.aggression)))) return;
            meuPedidoItem = new Match.PedidoItem { quem = me, nade = quero, t = t };
            m.pedidosItem.Add(meuPedidoItem); m.Ev($"item: pediu drop de {quero}/round");
            Think($"dropa uma {quero} pra mim!");
        }
        float proxPedidoItem; Match.PedidoItem meuPedidoItem;

        /// <summary>
        /// Colega pediu item: dropo se tenho sobrando e não vou usar agora. Pesa: tenho 2 flashes, sou AWP/suporte (fico
        /// atrás e quem abre é ele), a minha lineup precisa dela, estou no meio de algo. Dropar é o G: cai na frente,
        /// na direção dele; ele passa e pega.
        /// </summary>
        void AtenderItem(float t)
        {
            if (EXP.Contains("semPedirItem") || action != null || target != null || m.pedidosItem.Count == 0) return;
            for (int i = m.pedidosItem.Count - 1; i >= 0; i--)
            {
                var p = m.pedidosItem[i];
                if (!p.quem.alive || t - p.t > 4 || p.atendido != null) { if (t - p.t > 4 || !p.quem.alive) m.pedidosItem.RemoveAt(i); continue; }
                if (p.quem == me || p.quem.team != me.team || !me.nades.Contains(p.nade) || (p.quem.pos - me.pos).Len > 350) continue;
                if (!MapData.LosWalk(me.pos, me.z, p.quem.pos, p.quem.z, 18f)) continue;   // dropar é jogar na frente: tem que ver ele
                if (!rolouItem.Add(p)) continue;   // decido uma vez por pedido
                int minhas = me.nades.Count(n => n == p.nade);
                bool usoNaLineup = order.nades.Any(l => l.kind == p.nade);
                float wDrop = 0.5f + (minhas >= 2 ? 0.6f : 0) + (me.gunId == "awp" ? 0.6f : 0) + (me.role == Role.Suporte ? 0.3f : 0);
                float wNao = 0.35f + (usoNaLineup && minhas < 2 ? 1.2f : 0) + (order.push && me.role == Role.Entry ? 0.5f : 0);
                if (rng.Weighted(new[] { wNao, wDrop }) == 0) continue;
                if (m.DropaGranada(me, p.quem, p.nade)) { p.atendido = me; m.pedidosItem.RemoveAt(i); m.Ev($"item: dropou {p.nade} pro colega que pediu/round"); }
                return;
            }
        }
        readonly HashSet<Match.PedidoItem> rolouItem = new HashSet<Match.PedidoItem>();

        /// <summary>
        /// O pixel provável: onde o inimigo que está (ou pode estar) em `alvo` vai aparecer pra mim. Se eu já vejo o
        /// lugar, é ele mesmo; senão, anda pelo caminho de mim até ele e pega a quina — o último ponto que eu enxergo
        /// antes da parede, um pouquinho pra fora dela (ninguém aparece dentro da parede). A mira fica ali, na cabeça.
        /// </summary>
        V2 Pixel(V2 alvo)
        {
            // memória de vários pontos (quem testa ângulos alternativos pede vários seguidos; com um só, cada pedido
            // apagava o anterior e todos viravam caminho novo)
            for (int i = 0; i < PIX; i++)
                if (m.t - pixAt[i] < 0.6f && (alvo - pixDe[i]).Len < 20 && (me.pos - pixMeu[i]).Len < 15) return pixRes[i];
            int slot = 0; for (int i = 1; i < PIX; i++) if (pixAt[i] < pixAt[slot]) slot = i;
            long pc = P0(); pixelAdiado = false; pixelRes = PixelCalc(alvo); P1("pixel (quina do caminho)", pc);
            if (pixelAdiado)
            {   // o passo estourou o orçamento de A*: mira no último pixel desse alvo (mesmo velho, de perto daqui); sem ele, no
                // alvo — e não guarda, o próximo passo calcula
                for (int i = 0; i < PIX; i++) if ((alvo - pixDe[i]).Len < 20 && (me.pos - pixMeu[i]).Len < 60) return pixRes[i];
                return pixelRes;
            }
            pixDe[slot] = alvo; pixMeu[slot] = me.pos; pixAt[slot] = m.t; pixRes[slot] = pixelRes;
            return pixelRes;
        }
        const int PIX = 6; readonly V2[] pixDe = new V2[PIX], pixMeu = new V2[PIX], pixRes = new V2[PIX]; readonly float[] pixAt = { -9, -9, -9, -9, -9, -9 };
        V2 PixelCalc(V2 alvo)
        {
            pixelRes = alvo;
            if (MapData.LosWalk(me.pos, me.z, alvo, NavGrid.ZAt(alvo), 18f)) return alvo;
            var p = NavGrid.Path(me.pos, alvo, Perigo.Camada(me.team), !EXP.Contains("orcAVelho"));
            if (p == null) { pixelAdiado = true; return alvo; }   // orçamento de A* do passo estourado: adia
            if (p.Count < 2) return alvo;
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
        V2 pixelRes; bool pixelAdiado;

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
            bool pico = PicoCostume(null, ref best); if (PosicaoDeles(null, ref best)) pico = true;
            if (pico) { quinaRes = quinaBase = picoPonto; return quinaRes; }
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

        /// <summary>
        /// Posição de segurar do inimigo à vista (janelão, plataforma, banca...): não é quina — ele fica ali parado, no fim
        /// de uma linha aberta (o "sniper spot" do bot do CS). Concorre com as quinas pelo quanto deve ter alguém lá agora,
        /// na altura da cabeça dele. Sem isso o TR andava no meio olhando um canto vazio e tomava da AWP do janelão.
        /// </summary>
        bool PosicaoDeles(V2? fw, ref float best)
        {
            if (EXP.Contains("semPosicaoDeles")) return false;
            bool achou = false;
            foreach (var h in me.IsT ? HOLDS_CT : HOLDS_T)
            {
                V2 d = h.pos - me.pos; float dl = d.Len; if (dl < 90 || dl > 1100) continue;
                float zh = NavGrid.ZAt(h.pos);
                // já à vista, ou vai aparecer nos próximos passos do meu caminho (o "encounter spot": a mira já fica lá
                // antes de abrir — no passo em que abre ele já está me vendo)
                float antecipa = 1f;
                if (!MapData.LosWalk(me.pos, me.z, h.pos, zh, 18f))
                {
                    antecipa = 0;
                    if (fw != null && path != null && wp < path.Count)
                    {
                        float acc = 0; V2 prev = me.pos;
                        for (int k = wp; k < path.Count && acc < 110 && antecipa == 0; k++)
                        {
                            V2 pk = path[k].p; float seg = (pk - prev).Len;
                            for (float s2 = 20; s2 <= seg && acc + s2 <= 110; s2 += 20)
                            {
                                V2 q2 = prev + (pk - prev) * (s2 / seg);
                                if (MapData.LosWalk(q2, NavGrid.ZAt(q2), h.pos, zh, 18f)) { antecipa = 1f - 0.5f * (acc + s2) / 110f; break; }
                            }
                            acc += seg; prev = pk;
                        }
                    }
                    if (antecipa == 0) continue;
                }
                if (m.SmokeBlocks(me.pos, me.z + me.Eye, h.pos, zh + 18)) continue;
                float th = Ameaca(h.pos) * antecipa; if (th < 0.008f || Lotado(h.pos)) continue;
                V2 u = d / dl; float pf = fw == null ? 1f : u.Dot(fw.Value) > 0.35f ? 1f : u.Dot(fw.Value) > -0.3f ? 0.6f : 0.2f;
                float sc = th * pf * (1.3f - MathF.Min(dl, 900) / 900f) * Costume(h.pos);
                if (Match.LogContatos && sc <= best && dl > 300) diagPos = $"{h.name} perdeu: {sc:0.00} x {best:0.00}";
                if (sc > best) { best = sc; picoPonto = h.pos; picoTh = th; achou = true; }
            }
            return achou;
        }

        string diagPos = ""; bool posGanhou;
        string DiagPos(V2 onde)
        {
            Hold h = null; float bd = 1e9f; foreach (var x in me.IsT ? HOLDS_CT : HOLDS_T) if ((x.pos - onde).Len < bd) { bd = (x.pos - onde).Len; h = x; }
            if (h == null) return "-";
            return $"{h.name} a {bd:0} dele, dist {(h.pos - me.pos).Len:0}, los {MapData.LosWalk(me.pos, me.z, h.pos, NavGrid.ZAt(h.pos), 18f)}, z {NavGrid.ZAt(h.pos):0}/{me.z:0}, ameaça {Ameaca(h.pos):0.000}, esperados {m.intel[(int)me.team].Esperados(h.pos, 110):0.00}, lotado {Lotado(h.pos)}, bordaRisco {bordaRisco:0.000}, smoke {m.SmokeBlocks(me.pos, me.z + me.Eye, h.pos, NavGrid.ZAt(h.pos) + 18)}, {diagPos}, costume {Costume(h.pos):0.00}, posGanhou {posGanhou}, quer {MathF.Abs(MathX.AngDiff(wantAim, (h.pos - me.pos).Atan2)) * 57.3f:0}° dela, mira {MathF.Abs(MathX.AngDiff(me.aim, (h.pos - me.pos).Atan2)) * 57.3f:0}° dela, bordaAt há {m.t - bordaAt:0.00}s";
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
                fase = "ajudando no duelo"; fonteMira = "ajudando"; MoveTo(ajudaPos, true); if (SmokeEspera && SmokeNoCaminho(t)) { Face(PixelDele(ContatoDe(ajudaAlvo) ?? ajudaPos), 3); return true; } /* bx: a troca/ajuda também decide a smoke inimiga no caminho (antes atravessava sem pensar)*/ Face(PixelDele(ContatoDe(ajudaAlvo) ?? ajudaPos), 3); return true;
            }
            ajudaAlvo = null;
            if (me.hasBomb && order.plant || action != null || t < ajudaCooldown) return false;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || x.brain.target == null) continue;
                float d = (x.pos - me.pos).Len, raio = (me.role == Role.Ancora ? 150 : me.gunId == "awp" ? 180 : 260) * (order.push && !EXP.Contains("semSwing") ? 1.4f : 1f);   // entrando, o contato do colega é o sinal: todo mundo abre junto
                if (d > raio || !x.brain.target.alive) continue;
                if (ContatoDe(x.brain.target) is not V2 vi) continue;   // ele ainda não cantou
                // com o C4: se tem outro colega tão perto quanto eu pra ajudar, deixo pra ele (o bomb não vai pra troca à toa)
                if (me.hasBomb && !m.bombPlanted && !EXP.Contains("c4Ajuda")
                    && m.agents.Any(y => y != me && y != x && y.alive && y.team == me.team && (y.pos - x.pos).Len < d + 60))
                { ajudaCooldown = t + 1.5f; Think($"com o C4 — o duelo do {x.name} fica com quem está do lado dele"); return false; }
                // vêm vários: correr pra cena é morrer junto — seguro o ângulo da troca daqui (raro alguém ir assim mesmo)
                int vistos = m.intel[(int)me.team].contacts.Values.Count(c => t - c.t < 3 && m.agents[c.id].alive && (c.pos - vi).Len < 300);
                if (vistos >= 2 && !EXP.Contains("semSegurarVarios") && !Rola("ajuda varios " + x.brain.target.id + (int)(t / 4), 0.08f * P.Agr(me.skill.aggression)))
                { ajudaCooldown = t + 2; Think($"{vistos} deles ali com o {x.name} — seguro a troca daqui"); return false; }
                if (rng.Rand() > 0.35f + (order.push && !EXP.Contains("semSwing") ? 0.3f : 0) + 0.5f * P.Agr(me.skill.aggression)) { ajudaCooldown = t + 1.5f; return false; }   // às vezes segura a posição
                ajudaAlvo = x.brain.target; ajudaAte = t + 2.5f; ajudaCooldown = t + 5; ajudaPos = AnguloCruzado(x, vi);
                Think($"ajudando o {x.name}: trocando tiro com {ajudaAlvo.name} — {((ajudaPos - vi).Len > 1 ? "por outro ângulo" : "indo pra cena")}", $"ele está a {d:0} de mim, {vistos} visto(s) ali: dá pra virar 2x1"); fase = "ajudando no duelo";
                MoveTo(ajudaPos, true); Face(PixelDele(vi), 3);
                return true;
            }
            return false;
        }
        Agent ajudaAlvo; float ajudaAte = -9, ajudaCooldown = -9; V2 ajudaPos;

        // ================================================================ juiz
        /// <summary>
        /// O que fazer agora, quando não estou em combate nem reagindo: cada possibilidade (seguir o plano, trocar o
        /// colega, ajudar no duelo, pegar arma, pegar/lootear granada) dá uma nota pela MESMA leitura da situação — o
        /// que eu acho que está acontecendo, o tempo, o meu jeito — e ganha a maior. A escolha atual tem um bônus de
        /// compromisso (ninguém troca de ideia a cada passo); só troca se a outra for bem melhor. Feature nova = mais
        /// uma possibilidade disputando, não mais um degrau numa fila.
        /// </summary>
        void Juiz(float t)
        {
            long pj = P0(); PedirItem(t); AtenderItem(t);   // pedir/dropar item é de passagem (o G não para ninguém)
            float agr = P.Agr(me.skill.aggression);
            var intel = m.intel[(int)me.team];
            juizN = 0;
            // 1) o plano (a ordem do IGL): quanto ele aperta agora
            float nOrdem = 0.35f;
            if (order.plant && me.hasBomb && !m.bombPlanted) nOrdem += m.RoundLeft < 35 ? 0.65f : 0.4f;
            if (order.defuse) nOrdem += 0.6f;
            if (order.push) nOrdem += me.IsT && m.RoundLeft < 30 ? 0.35f : 0.15f;
            if (order.hold != null && (me.role == Role.Ancora || me.gunId == "awp")) nOrdem += 0.12f;   // meu ângulo é o que segura o bomb
            if (me.guardando) nOrdem += 0.5f;
            Prop(J_ORDEM, nOrdem);
            // 2) trocar o colega que caiu do meu lado
            if (Match.Auditar && trocaAudAte > 0)
            {   /* auditoria: durante a janela, quanto o juiz ficou na troca; no fim, viu o matador? */
                if (t < trocaAudAte) { trocaTicks++; if (juizAtual == J_TROCAR) trocaJuizTicks++; if (!tradeViu && tradeMatador != null && CanSee(tradeMatador)) tradeViu = true; }
                else
                {
                    m.Aud("troca: fim da janela", (tradeViu ? "viu o matador" : "não viu") + (tradeMatador != null && !tradeMatador.alive ? " · matador morreu" : "") + $" · faltava {((tradePos - me.pos).Len < 60 ? "<60" : (tradePos - me.pos).Len < 150 ? "60-150" : "150+")}");
                    m.Aud("troca: tempo do juiz na troca", trocaTicks == 0 ? "?" : trocaJuizTicks * 1f / trocaTicks > 0.8f ? "a >80%" : trocaJuizTicks * 1f / trocaTicks > 0.4f ? "b 40-80%" : "c <40%");
                    trocaAudAte = 0;
                }
            }
            if (t < tradeUntil)
            {
                float d = (tradePos - me.pos).Len;
                float n = 0.6f + 0.25f * agr + (d < 180 ? 0.1f : 0);   // o perigo já pesou na hora de decidir trocar
                Prop(J_TROCAR, n);
            }
            // 3) ajudar no duelo do colega
            if (t < ajudaAte && ajudaAlvo != null && ajudaAlvo.alive) Prop(J_AJUDAR, 0.7f);
            else if (AjudaPropor(t, out float nAj)) Prop(J_AJUDAR, nAj);
            // 4) pegar arma melhor do chão
            if (ArmaPropor(t, out float nArma)) Prop(J_ARMA, nArma);
            // 5) granada dropada pra mim / lootear a de quem morreu
            if (GranadaPraMim(t)) Prop(J_GRANADA, 0.5f);
            else if (LootGranada(t, true)) Prop(J_LOOT, 0.15f + 0.75f * lootNota);
            // 6) item útil a poucos passos (de passagem): desvia e passa por cima
            if (PassagemPropor(t, out float nPa)) Prop(J_PASSA, nPa);
            // 7) pezinho: subir na pilha alta com o colega (CT no começo do round) ou dar o pezinho pra quem pediu
            if (BoostPropor(t, out float nBo)) Prop(J_BOOST, nBo);
            if (PezinhoPropor(t, out float nPz)) Prop(J_PEZINHO, nPz);

            // escolhe: a atual ganha um bônus enquanto ainda é possível
            int esc = 0; float bn = float.MinValue;
            for (int i = 0; i < juizN; i++)
            {
                float n = juizNota[i] + (juizOp[i] == juizAtual && juizOp[i] != J_ORDEM ? 0.15f : 0);   // compromisso com o que comecei (o plano não precisa: é o padrão)
                if (n > bn) { bn = n; esc = juizOp[i]; }
            }
            if (esc != juizAtual)
            {
                if (Match.Auditar && juizAtual >= 0) m.Aud("juiz: troca de ideia", $"{J_NOME[juizAtual]} → {J_NOME[esc]}");
                juizAtual = esc; juizDesde = t;
                if (Explicar) porquePendente += $" → juiz: {J_NOME[esc]} ({bn:0.00})";
            }
            if (Match.Auditar && juizN > 1) m.Aud("juiz: venceu com concorrência", J_NOME[esc]);
            Executa(esc, t);
        }
        const int J_ORDEM = 0, J_TROCAR = 1, J_AJUDAR = 2, J_ARMA = 3, J_GRANADA = 4, J_LOOT = 5, J_PASSA = 6, J_BOOST = 7, J_PEZINHO = 8;
        static readonly string[] J_NOME = { "seguir o plano", "trocar o colega", "ajudar no duelo", "pegar arma", "pegar granada dropada", "lootear", "pegar de passagem", "subir no pezinho", "dar pezinho" };

        /* ---- PEZINHO (boost; apBoostVelho desliga) -----------------------------------------------------------------------
           Pro: o CT monta o boost no começo do round pra segurar o bomb de um lugar que ninguém pré-mira (em cima da pilha alta,
           vendo por cima). Precisa de um colega livre perto, sem contato à vista e do ângulo que eu seguro dar pra ver lá de
           cima. O colega agacha encostado na pilha, eu subo pelo ombro dele e ele volta pro lugar dele. Decidido uma vez por
           round (só sorteia quando existe pilha boa e colega: senão nem gasta o rng). */
        float audSemCam = -9;
        bool boostDecidido; public int boostEstado; public V2 boostTopo; V2 boostBase, boostEspera; Agent boostColega; float boostAte, boostChega;
        public Agent pezinhoPara; public V2 pezinhoBase; public float pezinhoAte;
        bool BoostPropor(float t, out float nota)
        {
            nota = 0;
            if (EXP.Contains("apBoostSemPropor")) return false;   /* bissecção: nem avalia subir */
            if (boostEstado == 1 || boostEstado == 2)
            {   // em andamento: compromisso enquanto o colega está vindo/agachado e ninguém aparece
                if (t > boostAte || boostColega == null || !boostColega.alive || target != null || ContatoIminente())
                {   string pq = "desisti do pezinho";
                    if (Match.Auditar)
                    {   /* auditoria: qual das condições e onde eu estava no corpo do colega/na pilha */
                        string qual = t > boostAte ? "tempo" : boostColega == null || !boostColega.alive ? "colega morto" : target != null ? "inimigo na mira" : "contato iminente";
                        string corpo = boostColega == null ? "-" : me.emCimaDe == boostColega ? "na cabeça" : me.noAr ? "no ar" : $"chão a {(boostColega.pos - me.pos).Len:0} do colega";
                        m.Aud("pezinho: desisti no estado 2 (por quê · corpo · z até o topo · ponto)", boostEstado == 2 ? $"{qual} · {corpo} · {NavGrid.ZAt(boostTopo) - me.z:0} · {(boostTopo - me.pos).Len:0} da borda · {Match.PontoBoost(boostTopo)}" : $"estado 1 · {qual}");
                    }
                    CancelaBoost(pq); return false; }
                if (boostEstado == 1 && t > boostChega && !boostColega.dandoPezinho) { CancelaBoost("o colega não chegou pro pezinho — volto pro meu ponto"); return false; }
                if (boostEstado == 2 && !boostColega.dandoPezinho && me.emCimaDe != boostColega && !me.noAr && me.z < NavGrid.ZAt(boostTopo) - 3) { CancelaBoost("o colega largou o pezinho"); return false; }
                nota = 0.66f; return true;
            }
            if (AtalhoPezinho(t)) { nota = 0.64f; return true; }
            if (boostDecidido || !NavGrid.BOOST || EXP.Contains("apBoostVelho") || me.IsT || me.gunId == "awp" && EXP.Contains("apAwpPezinhoVelho") || order.hold == null || m.t > 30 || target != null || action != null || order.name.StartsWith("esticando")) return false;   /* AWPer não sobe: lá em cima o FK dele disparava */
            if (NavGrid.Ilhas.Count == 0 || ContatoIminente()) return false;
            bool formacaoCT = EXP.Contains("apPezinhoCTLigado");   /* CT: o pezinho só sai da formação do IGL (ctPezinhoPar), não de qualquer um */
            if (formacaoCT && !me.IsT && ctPezinhoPar == null) return false;   /* só o CT espera a formação; o TR segue livre */
            V2 hp = order.hold.pos;
            if ((hp - me.pos).Len > 250) return false;   /* decide perto do meu ponto, não do spawn */ V2 look = order.hold.look != null && order.hold.look.Length > 0 ? order.hold.look[0] : hp + V2.Ang(me.aim) * 200;
            float zLook = NavGrid.ZAt(look);
            // melhor topo: perto do meu ponto, que veja o ângulo lá de cima (e mais longe do que do chão)
            float melhor = 0; V2 topo = default, chaoEsc = default; bool deBorda = false;
            void Avalia(V2 q, float zq, V2 chao, bool borda)
            {
                if (!MapData.LosWalk(q, zq + NavGrid.EYE, look, zLook + 18, 0f)) return;
                float ganho = MapData.Raio(q, zq, V2.Ang((look - q).Atan2), 700, NavGrid.EYE) - MapData.Raio(hp, NavGrid.ZAt(hp), V2.Ang((look - hp).Atan2), 700, NavGrid.EYE);
                float sc = 1 + MathF.Max(0, ganho) / 200 - (q - hp).Len / 300;
                if (sc > melhor) { melhor = sc; topo = q; chaoEsc = chao; deBorda = borda; }
            }
            foreach (var il in NavGrid.Ilhas)
            {
                if ((il.centro - hp).Len > 160) continue;
                foreach (var q in il.pontos) { var (cx0, cy0) = NavGrid.NearestChao(q); Avalia(q, il.z, NavGrid.Center(cx0, cy0), false); }
            }
            foreach (var bd in NavGrid.BordasPezinho) if ((bd.alto - hp).Len <= 160) Avalia(bd.alto, NavGrid.ZAt(bd.alto), bd.chao, true);
            if (melhor <= 0) { boostDecidido = true; return false; }
            if (MapData.Callout(topo).StartsWith("bomb") && !EXP.Contains("apBoostBombLivre") && !formacaoCT)   /* na formação o IGL já pesou quem fica no bomb */
            {   /* CT no bomb (decisão pela situação): subir tira o parceiro do chão e lá em cima morre-se fácil (66-92% no A/B).
                   Só sobe com gente pra trocar (2+ colegas a até 400 do topo — âncora sozinho nunca gasta o parceiro) e se
                   não acredita que vem gente agora (contato ou som perto nos últimos 3 s: subir tarde é morrer em cima) */
                int perto = 0; foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - topo).Len <= 400) perto++;
                var it = m.intel[(int)me.team]; bool vem = it.RecentNear(topo, 600, m.t, 3f) > 0;
                for (int i = it.heard.Count - 1; i >= 0 && !vem && m.t - it.heard[i].t < 3f; i--) if ((it.heard[i].p - topo).Len < 600) vem = true;
                if (perto < 2 || vem) { if (Match.Auditar) m.Aud("pezinho no bomb: não subiu", perto < 2 ? "sem gente pra trocar" : "vem gente"); boostDecidido = perto < 2; return false; }
            }
            foreach (var x in m.agents) if (x.team == me.team && x != me && (x.brain.boostEstado == 1 || x.brain.boostEstado == 2 || x.brain.boostEstado == 4 || x.brain.pezinhoPara != null)) return false;   /* um pezinho por time */
            Agent col = null; float dc = 260; bool parVelho = EXP.Contains("apBoostParVelho"); string comoPar = "";
            foreach (var x in m.agents)
                if (!formacaoCT && x != me && x.alive && x.team == me.team && x.brain.target == null && x.brain.action == null && x.brain.pezinhoPara == null && x.brain.boostEstado == 0 && !x.hasBomb && (x.gunId != "awp" || !EXP.Contains("apAwpPezinhoVelho") && !parVelho) && !x.brain.order.name.StartsWith("esticando") && (x.pos - me.pos).Len < dc)
                {
                    if (!parVelho)
                    {   /* PAR DO PEZINHO (padrão desde 2026-11-05; apBoostParVelho volta): como no CS, ninguém chama um cara do outro
                           lado — ou combinam saindo da base (o ponto dele é ali do lado da pilha, vão juntos), ou é quem está do meu
                           lado agora. E ele concorda pela situação dele (AceitaPezinho), não é convocado */
                        float dx = (x.pos - me.pos).Len;
                        V2 destX = x.brain.order.hold != null ? x.brain.order.hold.pos : x.brain.order.dest;
                        bool doLado = dx < 110;
                        bool combinado = m.t < 12 && (destX - chaoEsc).Len < 220;
                        if (!doLado && !combinado) continue;
                        if (!x.brain.AceitaPezinho(me, chaoEsc)) continue;
                        comoPar = combinado && !doLado ? "combinado na saída" : "do lado";
                    }
                    dc = (x.pos - me.pos).Len; col = x;
                }
            if (formacaoCT)
            {   /* o par já foi combinado no setup: é ele (se ainda está livre e vivo), sem sorteio de novo */
                var cp = ctPezinhoPar; ctPezinhoPar = null; boostDecidido = true;
                if (cp == null || !cp.alive || cp.brain.target != null || cp.brain.boostEstado != 0 || cp.brain.pezinhoPara != null || it0Vem(topo)) { if (Match.Auditar) m.Aud("pezinho CT: formação (resultado)", "caiu (par ocupado ou vem gente)"); return false; }
                PedirPezinho(topo, chaoEsc, cp, t); if (Match.Auditar) m.Aud("pezinho CT: formação (resultado)", "foram · " + Match.PontoBoost(topo));
                nota = 0.66f; return true;
            }
            if (col == null) return false;
            boostDecidido = true;
            float p = TeamBrain.ExpNum("apBoostChance", 0.025f) * (0.5f + P.Agr(me.skill.aggression)) * MathF.Min(1.5f, melhor);   /* pro: boost é raro (~1 em 10 rounds de CT) */
            if (me.gunId == "awp") p *= 0.5f * (1f + MathF.Min(1f, (look - topo).Len / 500f));   /* AWP lá em cima: vale pela linha longa (peso, não regra) */
            if (p <= 0 && !EXP.Contains("apSorteioExtra") || rng.Rand() >= p) return false;   /* apSorteioExtra: teste nulo do RNG (sorteio descartado) */
            PedirPezinho(topo, chaoEsc, col, t);
            if (Match.Auditar && comoPar != "") m.Aud("pezinho: como achou o par", comoPar);
            m.Ev(deBorda ? "pezinho: pediu pra borda/round" : "pezinho: pediu pra pilha/round");
            nota = 0.66f; return true;
        }
        /// <summary>Eu (o colega) topo dar o pezinho? Pesa o que eu largaria: com contato chegando, não; se o ponto da minha
        /// ordem é longe da base (o pezinho me tira do meu trabalho), menos (suporte topa mais, lurker menos); segurando ângulo
        /// sozinho perto do bomb pesa contra. Nada de regra fixa: vira uma chance.</summary>
        public bool AceitaPezinho(Agent quem, V2 base_)
        {
            if (ContatoIminente() || target != null) return false;
            V2 meuPonto = order.hold != null ? order.hold.pos : order.dest;
            float desvio = (meuPonto - base_).Len;
            float p = desvio < 150 ? 0.9f : desvio < 300 ? 0.55f : 0.2f;   /* no meu caminho / perto do meu ponto: quase sempre */
            if (order.hold != null && desvio > 150) p *= 0.6f;              /* larga o ângulo que eu seguro */
            p *= me.role == Role.Suporte ? 1.15f : me.role == Role.Lurker ? 0.7f : 1f;   /* suporte topa mais, lurker joga sozinho */
            if (me.gunId == "awp") p *= 0.25f;   /* abaixar a AWP pra ser escada custa caro: topa pouco, mas não zero */
            bool ok = rng.Rand() < p;
            if (Match.Auditar) m.Aud("pezinho: o colega topou?", (ok ? "sim" : "não") + (desvio < 150 ? " · no caminho dele" : desvio < 300 ? " · desvio médio" : " · longe do ponto dele"));
            return ok;
        }
        /// <summary>
        /// Monta o pezinho (como no CS): o colega agacha no chão (perto do alto), eu pulo na cabeça dele, ele levanta e eu pulo
        /// pro alto. Serve pro CT segurar de cima e pra quem quiser usar numa rota (ex.: rush pelo palácio) — a sessão dos bots
        /// decide quando. Precisa de NavGrid.BOOST.
        /// </summary>
        public void PedirPezinho(V2 alto, V2 chao, Agent col, float t)
        {
            boostTopo = alto; boostBase = chao; boostColega = col; boostEstado = 1; boostDecidido = true;
            boostChega = t + 3 + (boostBase - col.pos).Len / MathF.Max(1, col.MaxSpeed * 0.8f);   /* o colega tem a viagem + 3 s pra chegar e agachar */
            boostAte = t + 6 + MathF.Max((boostBase - me.pos).Len, (boostBase - col.pos).Len) / MathF.Max(1, col.MaxSpeed * 0.8f);
            { V2 fora = boostBase - alto; float lf = fora.Len; V2 e = lf > 0.5f ? boostBase + fora / lf * 10 : boostBase; boostEspera = NavGrid.Movable(e) ? e : boostBase; }   /* espero atrás do colega, não no lugar dele */
            col.brain.pezinhoPara = me; col.brain.pezinhoBase = boostBase; col.brain.pezinhoAte = boostAte;
            if (Match.Auditar) col.brain.AngLargadoInicio(boostBase);
            m.Ev("pezinho: pediu/round"); m.Aud("pezinho por ponto: pediu", Match.PontoBoost(alto) + " · " + (me.IsT ? "TR" : "CT")); m.Log($"PEZINHO {me.name} pede ao {col.name}: alto ({alto.x:0},{alto.y:0}) z {NavGrid.ZAt(alto):0}, base ({boostBase.x:0},{boostBase.y:0}), espera ({boostEspera.x:0},{boostEspera.y:0})"); Think($"vou subir com o pezinho do {col.name} — seguro de cima, ninguém mira lá");
        }
        /* PEZINHO COMO ATALHO (só com o pezinho ligado): andando pra um lugar longe, se subir na borda com o colega (o chão
           perto, ~4 s de pezinho, o resto do caminho de lá de cima) sai bem mais curto que dar a volta, propõe. Pro: boost
           que economiza tempo (apartamento rápido, rush pelo casarão/palácio) — nunca de pistola, nunca com o C4, nunca com
           contato perto. Reavalia no máximo a cada 2 s (PathLen por borda) */
        bool boostAtalho; float atalhoProx;
        public Agent callPar; public V2 callChao, callAlto; public float callAte;
        public Agent ctPezinhoPar;   /* formação do CT com pezinho (apPezinhoCTLigado): o par que o IGL combinou */
        bool it0Vem(V2 topo)
        {   var it = m.intel[(int)me.team]; if (it.RecentNear(topo, 600, m.t, 3f) > 0) return true;
            for (int i = it.heard.Count - 1; i >= 0 && m.t - it.heard[i].t < 3f; i--) if ((it.heard[i].p - topo).Len < 600) return true;
            return false; }   /* call do IGL pro pezinho */
        bool AtalhoPezinho(float t)
        {
            if (!NavGrid.BOOST || EXP.Contains("apBoostVelho") || EXP.Contains("apAtalhoVelho") || boostEstado != 0 || t < atalhoProx) return false;
            if (callPar != null)
            {   /* call do IGL (apPezinhoCallLigado): a dupla já sai combinada da base; cai se o par morreu/está ocupado ou passou a hora */
                if (t > callAte || !callPar.alive || callPar.brain.boostEstado != 0 || callPar.brain.pezinhoPara != null || me.hasBomb)
                { if (Match.Auditar) m.Aud("pezinho: call do IGL (resultado)", "caiu antes de começar"); callPar = null; }
                else if (target == null && action == null && !ContatoIminente())
                {
                    var cp = callPar; callPar = null; boostAtalho = true; PedirPezinho(callAlto, callChao, cp, t);
                    if (Match.Auditar) m.Aud("pezinho: call do IGL (resultado)", "foram"); m.Ev("pezinho: call do IGL/round"); Think($"call do IGL: vou com o {cp.name} pro pezinho");
                    return true;
                }
            }
            atalhoProx = t + 2f;
            if (target != null || action != null || me.hasBomb || WeaponStats.Pistola(me.gunId) || NavGrid.BordasPezinho.Count == 0 || ContatoIminente()) return false;
            V2 goal = order.hold != null ? order.hold.pos : order.dest;
            float direto = NavGrid.PathLen(me.pos, goal); if (float.IsInfinity(direto) || direto < 250) return false;
            float melhor = 150; (V2 chao, V2 alto) esc = default; bool achou = false;
            float custoPezinho = 4f * me.MaxSpeed;   /* ~4 s parado montando e subindo, em distância */
            foreach (var bd in NavGrid.BordasPezinho)
            {
                if ((bd.chao - me.pos).Len > 200) continue;
                if (m.intel[(int)me.team].Esperados(bd.chao, 400) > 0.3f || m.intel[(int)me.team].Esperados(bd.alto, 400) > 0.3f) continue;   /* pezinho no meio da briga não: só onde acho que não tem ninguém */
                float ida = NavGrid.PathLenAte(me.pos, bd.chao, 260); if (float.IsInfinity(ida)) continue;
                float resto = NavGrid.PathLen(bd.alto, goal); if (float.IsInfinity(resto)) continue;
                float ganho = direto - (ida + custoPezinho + resto);
                if (ganho > melhor) { melhor = ganho; esc = (bd.chao, bd.alto); achou = true; }
            }
            if (!achou) return false;
            Agent col = null; float dc = 140;
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && x.brain.target == null && x.brain.action == null && x.brain.pezinhoPara == null && x.brain.boostEstado == 0 && !x.hasBomb && (x.pos - me.pos).Len < dc)
                { dc = (x.pos - me.pos).Len; col = x; }
            if (col == null) return false;
            float p = 0.5f * (0.6f + P.Agr(me.skill.aggression)) * MathF.Min(1, melhor / 400f);   /* ganho maior, mais vontade */
            if (rng.Rand() >= p) return false;
            boostAtalho = true; PedirPezinho(esc.alto, esc.chao, col, t);
            m.Ev("pezinho: atalho/round"); Think($"atalho: subo com o pezinho do {col.name} — economiza {melhor / me.MaxSpeed:0} s");
            return true;
        }
        void CancelaBoost(string porque)
        {
            if (boostColega != null && boostColega.brain.pezinhoPara == me) { boostColega.brain.pezinhoPara = null; boostColega.dandoPezinho = false; }
            me.subindoPezinho = false; m.Log($"PEZINHO {me.name} desiste no estado {boostEstado}: {porque} (eu ({me.pos.x:0},{me.pos.y:0}) z {me.z:0}; colega {(boostColega != null ? $"({boostColega.pos.x:0},{boostColega.pos.y:0}) agachado {boostColega.dandoPezinho}" : "-")})"); m.Aud("pezinho: desistiu (motivo · estado · ponto)", porque + " · " + boostEstado + " · " + Match.PontoBoost(boostTopo)); boostEstado = 3; m.Ev("pezinho: desistiu/round"); Think(porque);
        }
        /* AUDITORIA "ângulo largado pelo pezinho" (só mede, não muda nada): quem dá o pezinho larga o ponto da ordem dele.
           Enquanto o pezinho dura: alguém do time cobre aquele ponto (perto e com linha pro ângulo)? morreu alguém por ali?
           E pela crença no começo: esperavam inimigo na rota largada? quantos vivos? */
        bool angAtivo; V2 angPos, angLook; float angDesde, angProx; int angAmostras, angCoberto, angMortes; string angCtx;
        public void AngLargadoInicio(V2 base_)
        {
            angAtivo = false; var h = order.hold; if (h == null) return;
            V2 lk = h.look != null && h.look.Length > 0 ? h.look[0] : h.pos;
            float longe = (h.pos - base_).Len;
            angPos = h.pos; angLook = lk; angDesde = m.t; angProx = m.t; angAmostras = angCoberto = angMortes = 0; angAtivo = true;
            int vivos = 0; foreach (var x in m.agents) if (x.alive && x.team == me.team) vivos++;
            float esp = m.intel[(int)me.team].Esperados(lk, 400);
            angCtx = (longe < 120 ? "perto da base" : longe < 300 ? "médio" : "longe") + " · " + (esp >= 0.5f ? "esperavam gente" : "rota calma") + $" · {vivos} vivos";
        }
        void AngLargadoTick(float t)
        {
            if (pezinhoPara == null || !me.alive)
            {   angAtivo = false;
                if (angAmostras == 0) return;
                float fr = angCoberto / (float)angAmostras;
                m.Aud("pezinho: ângulo largado — alguém cobriu?", (fr >= 0.7f ? "a coberto" : fr >= 0.3f ? "b às vezes" : "c aberto") + " · " + angCtx);
                m.Aud("pezinho: ângulo largado — morreu alguém por ali?", (angMortes > 0 ? "sim" : "não") + " · " + (fr >= 0.3f ? "coberto" : "aberto") + $" · {t - angDesde:0} s");
                return;
            }
            if (t < angProx) return; angProx = t + 1f; angAmostras++;
            float zl = NavGrid.ZAt(angLook);
            foreach (var x in m.agents)
                if (x != me && x != pezinhoPara && x.alive && x.team == me.team && (x.pos - angPos).Len < 220 && MapData.LosWalk(x.pos, x.z, angLook, zl, 18f)) { angCoberto++; break; }
        }
        bool PezinhoPropor(float t, out float nota)
        {
            nota = 0;
            if (angAtivo) AngLargadoTick(t);
            if (pezinhoPara == null) return false;
            if (!pezinhoPara.alive || t > pezinhoAte || target != null || pezinhoPara.brain.boostEstado >= 2 && pezinhoPara.z > me.z + 20 && pezinhoPara.emCimaDe != me) { pezinhoPara = null; me.dandoPezinho = false; return false; }   /* com ele na minha cabeça, não solto */
            nota = 0.66f; return true;
        }
        void ExecutaBoost(float t)
        {
            if (boostEstado == 1)
            {   // indo: espero atrás do colega até ele agachar colado na base
                fase = "indo pro pezinho";
                bool pronto = boostColega != null && boostColega.dandoPezinho && (boostColega.pos - boostBase).Len < 6;
                if ((boostEspera - me.pos).Len > 5 || !pronto)
                {   /* esperando o colega: olho pro ângulo que vou segurar (não pra pilha) */
                    if ((boostEspera - me.pos).Len > 3) MoveTo(boostEspera, false);
                    var hd = order.hold; Face(hd != null && hd.look != null && hd.look.Length > 0 ? hd.look[0] : boostTopo, 3); return;
                }
                boostEstado = 2; me.subindoPezinho = true; m.Log($"PEZINHO {me.name} vai subir");
            }
            if (boostEstado == 2)
            {   // 1) encosto no colega (a física pula pra cabeça dele); 2) em cima, espero ele levantar; 3) ando pra borda e pulo
                fase = "subindo no pezinho";
                if (me.z >= NavGrid.ZAt(boostTopo) - 3 && !me.noAr && me.emCimaDe == null && (boostTopo - me.pos).Len < 14)
                {   // na borda: segura dali (posição especial, como em cima da caixa)
                    boostEstado = 4; me.subindoPezinho = false;
                    if (boostAtalho) { boostAtalho = false; boostEstado = 0; boostDecidido = true; if (boostColega != null) { boostColega.brain.pezinhoPara = null; boostColega.dandoPezinho = false; } m.Ev("pezinho: atalho feito/round"); Think("subi — sigo pelo alto"); return; }   /* atalho: segue a ordem lá de cima */
                    baseP = ancora = boostTopo; baseDesde = t; posEspecial = 2; m.AudBoostSubiu(me, boostTopo);   /* âncora também: o replanejar do cover partia de (0,0) */
                    if (boostColega != null) { boostColega.brain.pezinhoPara = null; boostColega.dandoPezinho = false; }
                    m.Ev("pezinho: segurando de cima/round"); Think("em cima — seguro daqui"); return;
                }
                bool acima = me.z > boostColega.z + 15f;   /* já na altura da cabeça dele (em cima ou no pulo pro alto): o alvo é o alto */
                V2 alvo = !acima ? boostColega.pos : me.emCimaDe == boostColega && boostColega.duck > 0.1f ? boostColega.pos : boostTopo;   /* em cima: espero ele levantar */
                V2 d0 = alvo - me.pos; float l0 = d0.Len;
                if (l0 > 0.6f) { wantVel = d0 / l0 * me.MaxSpeed * (me.emCimaDe != null ? 0.45f : 0.6f); me.move = Move.Andando; }   /* reto: não há caminho de navegação até a borda (só o pezinho) */
                Face(boostTopo, 3);
            }
        }
        void ExecutaPezinho(float t)
        {
            fase = "dando pezinho";
            bool emCima = pezinhoPara != null && pezinhoPara.emCimaDe == me;
            if (!emCima && (pezinhoBase - me.pos).Len > (me.dandoPezinho ? 8f : 3.5f)) { me.dandoPezinho = false; MoveTo(pezinhoBase, true); return; }   /* já agachado: empurrão pequeno não desfaz */
            me.dandoPezinho = true; wantVel = V2.Zero;
            me.crouch = !emCima;   /* agachado pra ele subir; com ele na cabeça, levanto (ele ganha a altura do em pé) */
            var hp = pezinhoPara.brain.order.hold; if (hp != null && hp.look != null && hp.look.Length > 0) Face(hp.look[0], 3);
        }

        /// <summary>
        /// Item útil a poucos passos e reto (granada que cabe, kit se sou CT sem kit, primária se estou sem): desviar
        /// custa quase nada — passo por cima. Só onde acho seguro e sem pressa de verdade.
        /// </summary>
        bool PassagemPropor(float t, out float nota)
        {
            nota = 0;
            if (EXP.Contains("semPassagem") || action != null || order.defuse && m.BombLeft < Match.DefuseTime(me) + 4) return false;
            if (m.intel[(int)me.team].Esperados(me.pos, 300) > 0.5f) return false;
            float melhor = 999; V2 alvo = default;
            foreach (var it in m.itensChao)
            {
                bool livre = it.morto != null || it.time == me.team && (it.para == me || t - it.t > 3 && it.de != me);
                if (!livre || !Match.CabeGranada(me, it.nade)) continue;
                float d = (it.pos - me.pos).Len; if (d < melhor && d < 45 && NavGrid.Direct(me.pos, it.pos)) { melhor = d; alvo = it.pos; }
            }
            if (me.Primaria == null)
                foreach (var dr in m.drops)
                {
                    if (dr.para != null && dr.para != me && t - dr.t < 3 || WeaponStats.Pistola(dr.gun)) continue;
                    float d = (dr.pos - me.pos).Len; if (d < melhor && d < 45 && NavGrid.Direct(me.pos, dr.pos)) { melhor = d; alvo = dr.pos; }
                }
            if (melhor >= 999) return false;
            passaAlvo = alvo; nota = 0.62f - melhor / 150f; return true;
        }
        V2 passaAlvo;
        readonly int[] juizOp = new int[12]; readonly float[] juizNota = new float[12]; int juizN, juizAtual = -1; float juizDesde;
        void Prop(int op, float nota) { juizOp[juizN] = op; juizNota[juizN] = nota; juizN++; }

        void Executa(int op, float t)
        {
            if (op != J_PEZINHO && me.dandoPezinho) me.dandoPezinho = false;
            switch (op)
            {
                case J_BOOST: ExecutaBoost(t); return;
                case J_PEZINHO: ExecutaPezinho(t); return;
                case J_TROCAR:
                    if (tradeFlash && action == null && tradeMatador != null && tradeMatador.alive)
                    {
                        tradeFlash = false; V2 mp = ContatoDe(tradeMatador) ?? tradePos;
                        if (Alcanca(NaCara(mp), "flash") && !AmigoNoAlvo(mp))
                        {
                            float t1 = Jogar("flash", $"flash no {tradeMatador.name} pra trocar", NaCara(mp));
                            if (action != null) { tradeUntil = MathF.Max(tradeUntil, t1 + 1.2f); m.Ev("troca: bangou antes de trocar/round"); return; }
                        }
                    }
                    if (Match.Auditar && tradeMatador != null && !tradeViu && CanSee(tradeMatador)) { tradeViu = true; m.Aud("troca: viu o matador indo trocar", "sim"); }
                    if (me.bangAmigaDe == me && t < me.bangAmigaEm - 0.3f && !EXP.Contains("trocaRetaVelha"))
                    { Esperando(null); Face(me.bangAmigaPos, 3); fase = "esperando a flash pra trocar"; return; }   // abre no estouro
                    fase = "indo trocar"; MoveTo(tradePos, true); if (SmokeEspera && SmokeNoCaminho(t)) { Face(Pixel(tradePos), 3); return; } /* bx: a troca/ajuda também decide a smoke inimiga no caminho (antes atravessava sem pensar)*/ ClearAngles(true); Face(!EXP.Contains("trocaPico") || EXP.Contains("trocaPicoVelho") ? Pixel(tradePos) : tradeMira, 3); Think("indo trocar"); return;
                case J_AJUDAR:
                    if (!(t < ajudaAte && ajudaAlvo != null && ajudaAlvo.alive)) AjudaCompromete(t);
                    if (AjudarNoDuelo(t)) return; break;
                case J_ARMA:
                    if (PegarArma(t)) return; break;
                case J_GRANADA:
                    if (PegarGranada(t)) return; break;
                case J_PASSA:
                    fase = "pegando de passagem"; MoveTo(passaAlvo, true); return;
                case J_LOOT:
                    if (lootAlvo == null && lootCand != null)
                    {
                        var x = lootCand; lootAlvo = x; lootVisto.Add(x); lootAte = t + 4 + lootCandPl / 60f; lootUltCand = null;
                        bool retakeVem = !me.IsT && (m.bombPlanted || order.name.Contains("retomar") || order.name.Contains("retake"));
                        if (x.nade == "kit") m.Ev("kit do chão: foi buscar/round"); else if (retakeVem) m.Ev("granada do chão: buscou antes do retake/round");
                        m.Ev(m.over ? "granada do chão: looteou no fim do round/round" : "granada do chão: foi lootear/round");
                        Think(x.nade == "kit" ? $"vou pegar o kit do {x.morto.name} antes do retake" : $"vou pegar a {x.nade} do {x.morto.name}");
                    }
                    if (LootGranada(t)) return; break;
            }
            // seguir o plano (e as granadas que ele pede no caminho)
            if (Utilitaria(t)) return;
            if (UtilProativa(t)) return;
            if (GranadaNoBarulho(t)) return;
            if (AbrirSmoke(t)) return;
            if (SprayNaSmoke(t)) return;
            if (AtenderBang(t)) return;
            DoOrder(t);
        }

        /// <summary>Granada que um colega dropou pra mim, perto e sem perigo (o juiz decide se vale ir agora).</summary>
        bool GranadaPraMim(float t)
        {
            if (action != null || m.itensChao.Count == 0 || me.hasBomb && order.plant) return false;
            foreach (var x in m.itensChao) if (x.para == me && (x.pos - me.pos).Len < 220 && Match.CabeGranada(me, x.nade)) return m.intel[(int)me.team].Esperados(me.pos, 300) <= 0.5f;
            return false;
        }

        /// <summary>Arma melhor no chão perto e lugar seguro: a nota é pelo ganho (o juiz pesa com o resto).</summary>
        bool ArmaPropor(float t, out float nota)
        {
            nota = 0;
            if (action != null || me.hasBomb && order.plant || m.drops.Count == 0) return false;
            bool comPressa = order.push || order.defuse || m.bombPlanted && !me.IsT;
            var d = MelhorDrop(comPressa ? 45 : 130, 1.5f);
            if (d == null || m.intel[(int)me.team].Esperados(me.pos, 350) > 0.3f) return false;
            nota = 0.62f; return true;
        }

        // candidato de ajuda avaliado (sem decidir): quem, contra quem, onde
        Agent ajudaCandColega, ajudaCandAlvo; V2 ajudaCandVi; int ajudaCandVistos; float ajudaCandD, ajudaCandEm = -9, ajudaCandNota;
        /// <summary>Colega trocando tiro perto: a nota de ir ajudar (pelo meu jeito, pela entrada, por quantos tem lá).</summary>
        bool AjudaPropor(float t, out float nota)
        {
            nota = 0;
            if (me.hasBomb && order.plant || action != null || t < ajudaCooldown) return false;
            // mesma situação avaliada há pouco: mesma nota (o gosto do momento não muda a cada passo)
            if (t - ajudaCandEm < 1.2f && ajudaCandColega != null && ajudaCandColega.alive && ajudaCandAlvo != null && ajudaCandAlvo.alive && ajudaCandColega.brain.target == ajudaCandAlvo)
            { nota = ajudaCandNota; return true; }
            ajudaCandColega = null;
            float agr = P.Agr(me.skill.aggression);
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || x.brain.target == null) continue;
                float d = (x.pos - me.pos).Len, raio = (me.role == Role.Ancora ? 150 : me.gunId == "awp" ? 180 : 260) * (order.push && !EXP.Contains("semSwing") ? 1.4f : 1f);
                if (d > raio || !x.brain.target.alive) continue;
                if (ContatoDe(x.brain.target) is not V2 vi) continue;
                if (me.hasBomb && !m.bombPlanted && !EXP.Contains("c4Ajuda")
                    && m.agents.Any(y => y != me && y != x && y.alive && y.team == me.team && (y.pos - x.pos).Len < d + 60))
                { ajudaCooldown = t + 1.5f; return false; }
                int vistos = m.intel[(int)me.team].contacts.Values.Count(c => t - c.t < 3 && m.agents[c.id].alive && (c.pos - vi).Len < 300);
                if (vistos >= 2 && !EXP.Contains("semSegurarVarios") && !Rola("ajuda varios " + x.brain.target.id + (int)(t / 4), 0.08f * agr))
                { ajudaCooldown = t + 2; Think($"{vistos} deles ali com o {x.name} — seguro a troca daqui"); return false; }
                float pr = 0.35f + (order.push && !EXP.Contains("semSwing") ? 0.3f : 0) + 0.5f * agr;
                ajudaCandColega = x; ajudaCandAlvo = x.brain.target; ajudaCandVi = vi; ajudaCandVistos = vistos; ajudaCandD = d; ajudaCandEm = t;
                ajudaCandNota = 0.2f + 0.5f * MathF.Min(1.2f, pr) + (float)rng.Normal(0, 0.08f);
                nota = ajudaCandNota; return true;
            }
            return false;
        }
        void AjudaCompromete(float t)
        {
            var x = ajudaCandColega; if (x == null) return;
            ajudaAlvo = ajudaCandAlvo; ajudaAte = t + 2.5f; ajudaCooldown = t + 5; ajudaPos = AnguloCruzado(x, ajudaCandVi);
            Think($"ajudando o {x.name}: trocando tiro com {ajudaAlvo.name} — {((ajudaPos - ajudaCandVi).Len > 1 ? "por outro ângulo" : "indo pra cena")}", $"ele está a {ajudaCandD:0} de mim, {ajudaCandVistos} visto(s) ali: dá pra virar 2x1");
        }

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
                    float pl = NavGrid.PathLenAte(me.pos, q, 320);
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
        /// <summary>
        /// Dá pra parar e jogar granada agora? Soltar deixa ~1 s sem arma (CS2: volta sacando). Com inimigo visto há pouco
        /// perto (ou que eu acredito colado), granada fora de lineup espera — quem joga com o cara em cima morre jogando.
        /// </summary>
        bool CalmoPraGranada(float t, bool atraso = false)
        {
            if (EXP.Contains("granadaSemCuidado") || EXP.Contains("sacarVelho")) return true;
            var it = m.intel[(int)me.team];
            // inimigo visto há pouco perto: quem joga com ele ali é pego virando a quina no segundo sem arma. A granada de
            // atrasar a entrada (CT) é feita pra isso: só segura se ele já me vê
            foreach (var c in it.contacts.Values)
                if (m.agents[c.id].alive && t - c.t < 1.5f && (c.pos - me.pos).Len < 380 && (!atraso || MapData.LosWalk(c.pos, c.z, me.pos, me.z, 18f)))
                { if (t - calmoLog > 2) { calmoLog = t; m.Ev("granada: segurou (inimigo perto)/round"); } return false; }
            return atraso || it.Esperados(me.pos, 220) < 0.9f;
        }

        bool Utilitaria(float t)
        {
            bool molo = me.nades.Contains("molotov"), he = me.nades.Contains("he");
            if (action != null || !(molo || he || me.nades.Contains("smoke")) || !CalmoPraGranada(t, true)) return false;
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
                    if (kind == "he" && !EXP.Contains("heAlcanceVelho") && !Alcanca(alvo.p, "he")) kind = molo ? "molotov" : smk ? "smoke" : null;   // a HE daqui estoura no ar
                    if (kind == null) return false;
                    l = new Lineup($"{kind} na {alvo.nome}", kind, me.pos, alvo.p); atrasos++;
                    m.utilUsada.Add("atraso " + alvo.nome);
                    porque = $"{(nMax >= 4 ? "vi gente" : "ouvi passo")} chegando — atrasar até a rotação vir";
                }
            }
            else if (he && HeNoParado(t, it, out l, out porque)) { }
            else if (!molo && !(he && me.IsT && m.bombPlanted && m.defuserActive && !EXP.Contains("heAlcanceVelho"))) return false;
            else if (!molo) { if ((m.bombPos - me.pos).Len < 500 && Alcanca(m.bombPos, "he") && Rola("defuse", 0.6f)) { l = new Lineup("HE no bomb", "he", me.pos, m.bombPos); porque = "ouvi o defuse — HE em quem desarma"; m.Ev("HE: no defuse/round"); } }
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
            if (!l.name.Contains(" na ") && !l.name.EndsWith("no bomb"))   /* não é atraso de entrada nem no defuse: a regra cheia */
                if (!CalmoPraGranada(t, !EXP.Contains("heNoVistoVelho") && l.kind == "he" && l.name.StartsWith("HE no "))) return false;   /* HE no inimigo que acabei de ver atrás da cobertura: só segura se ele me vê (barrar atrasava e ele saía do lugar: HE perto 47→35%) */
            if (!EXP.Contains("utilAToaVelho") && !UtilVale(l.kind, l.to, l.name)) return false;
            m.utilUsada.Add(l.name); action = "granada"; actionNade = l; actionEnd = SoltaGranada(t, 0.9f); fase = "ação";
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
        /// <summary>
        /// Spam na smoke, como pro: sem ver ninguém, mas ACHANDO que tem gente atrás da fumaça (passo ouvido ali há pouco, o
        /// inimigo que eu vi sumir dentro dela, ou call do time), dá uma rajada curta onde acho que ele está — às vezes erra
        /// (é crença, não visão). O tiro também abre o túnel na fumaça (Shot) e às vezes revela. Pausa entre rajadas.
        /// </summary>
        bool SprayNaSmoke(float t)
        {
            if (t < spamAte && target == null && action == null && !EXP.Contains("spamSmokeVelho"))
            {   // a rajada em andamento (uma bala por cadência da arma)
                Face(spamAlvo, 4); fase = "spam na smoke";
                if (me.gun.CanFire(t) && me.ArmaPronta(t)) m.Shoot(me, spamAlvo + new V2(rng.Uniform(-6, 6), rng.Uniform(-6, 6)), spamZ + rng.Uniform(-3, 3));
                return true;
            }
            if (EXP.Contains("spamSmokeVelho") || target != null || action != null || m.smokes.Count == 0 || t < spamProx || me.faca || me.gun.inMag < 4 || me.gun.Reloading || !me.ArmaPronta(t) || me.gunId == "awp") return false;
            spamProx = t + 0.5f;
            var it = m.intel[(int)me.team];
            V2? alvo = null; string porque = null;
            for (int i = it.heard.Count - 1; i >= 0 && t - it.heard[i].t < 2f; i--)
                if (it.heard[i].kind == "passos" && (it.heard[i].p - me.pos).Len < 600) { alvo = it.heard[i].p; porque = "ouvi passo atrás da smoke"; break; }
            if (alvo == null && lastSeen != null && lastSeen.alive && t - lastSeenT < 2.5f) { alvo = lastSeenPos; porque = $"o {lastSeen.name} sumiu na smoke"; }
            if (alvo == null && Chamada(550) is V2 ch) { alvo = ch; porque = "call: tem gente ali"; }
            if (alvo == null) return false;
            V2 a = alvo.Value; float d = (a - me.pos).Len;
            if (d < 100 || d > 600) return false;
            float za = NavGrid.ZAt(NavGrid.Snap(a));
            if (!MapData.LosWalk(me.pos, me.z + me.Eye, a, za + 18, 18f) || !m.SmokeBlocks(me.pos, me.z + me.Eye, a, za + 18)) return false;
            if (m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < d && MathF.Abs(MathX.AngDiff((x.pos - me.pos).Atan2, (a - me.pos).Atan2)) < 0.22f)) return false;
            if (!Rola($"spam smoke {(int)(t / 3)}", 0.25f + 0.45f * P.Agr(me.skill.aggression))) { spamProx = t + 2f; return false; }
            spamAlvo = a + new V2(rng.Uniform(-18, 18), rng.Uniform(-18, 18)); spamZ = za + 18;
            spamAte = t + me.gun.s.Interval * (3 + rng.Int(4)) + 0.05f;   // 3-6 balas
            Face(spamAlvo, 4);
            spamProx = spamAte + rng.Uniform(2f, 4f);
            m.Ev("spam na smoke (rajada)/round"); Think($"spam na smoke ({Region(a)}): {porque}");
            fase = "spam na smoke";
            return true;
        }
        float spamProx, spamAte = -9, spamZ; V2 spamAlvo;
        float heOlhar = -9, furoAte = -9, furoDesde; V2 furoPos;

        /// <summary>Ouviu passo perto, fora da vista, e ele deve estar parado ali ou passando: molotov/HE no barulho (às vezes).</summary>
        bool GranadaNoBarulho(float t)
        {
            if (action != null || EXP.Contains("semPunir") || t < proxBarulho || !CalmoPraGranada(t)) return false;
            var som = (p: somP, kind: somK);
            if (som.kind != "passos" || t - somT > 1.2f) return false;
            float d = (som.p - me.pos).Len; if (d < 120 || d > 330) return false;
            string kind = me.nades.Contains("molotov") ? "molotov" : me.nades.Contains("he") ? "he" : null;
            if (kind == null || !Alcanca(som.p, kind) || m.agents.Any(x => x.alive && x.team == me.team && (x.pos - som.p).Len < 110)) return false;
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
            if (action != null || t < proxUtil || !CalmoPraGranada(t) || m.bombPlanted && !me.IsT || order.defuse || me.hasBomb && order.plant || EXP.Contains("semUtilPro") || EscondeStack(t)) return false;
            proxUtil = t + 1.2f;
            var inim = me.IsT ? Team.CT : Team.T; var it = m.intel[(int)me.team];
            string[] tipos = me.IsT ? new[] { "flash", "smoke", "molotov", "he" } : new[] { "molotov", "smoke", "he", "flash" };
            // CT costuma guardar uma granada pro que vier depois (retake); cada um decide uma vez por round
            if (!me.IsT && guardaUtil == null) guardaUtil = rng.Rand() < 0.6f && EXP.Contains("guardaUtil") ? new[] { "flash", "molotov", "he" }[rng.Int(3)] : "";
            bool guardaSmoke = me.IsT && !m.bombPlanted && (m.leitura[(int)me.team]?.intencao.guardaSmoke ?? false) && !EXP.Contains("semAdaptaPos");
            string kind = tipos.FirstOrDefault(k => me.nades.Contains(k) && k != guardaUtil && !(guardaSmoke && k == "smoke")); if (kind == null) return false;
            (string nome, V2 p) alvo = default; float bv = 0, bsFl = 0;
            void Ve(string nome, V2 p)
            {
                float d = (p - me.pos).Len;
                if (d < 140 || d > 560 || m.utilUsada.Contains($"pro {nome} {(int)(t / 12)}")) return;
                float v = Rotas.Perto(inim, p, t, 120) + Rotas.Perto(inim, p, t + 5, 120);
                if (me.IsT) v *= MathF.Min(1, it.HeatAvg(p, 70) * 4);   // TR: só onde ainda pode ter alguém
                // HE machuca quem ESTÁ lá: só vale onde eu acredito que tem gente agora (visto/ouvido/cantado), não onde costuma ter
                if (kind == "he" && !EXP.Contains("heVelha")) v *= MathX.Clamp(it.Esperados(p, 110), 0, 1.2f);
                /* bx: flash cega só quem ESTÁ lá: a crença de agora escolhe ONDE jogar (entre os lugares), não corta o QUANTO
                   (a vontade de jogar segue a das rotas — pro joga muita flash; só joga no lugar onde mais acredito) */
                if (kind == "flash" && !EXP.Contains("bxFlashCrencaVelho"))
                {
                    float vs = v * MathX.Clamp(0.35f + 1.6f * it.Esperados(p, 160), 0.35f, 1.6f);
                    if (vs > bsFl) { bsFl = vs; bv = v; alvo = (nome, p); }
                    return;
                }
                if (v > bv) { bv = v; alvo = (nome, p); }
            }
            if (me.IsT) foreach (var h in HOLDS_CT) Ve(h.name, h.pos);
            else foreach (var e in ENTRADAS_A.Concat(ENTRADAS_B)) Ve(e.nome, e.p);
            float sobra = EXP.Contains("utilVolumeVelho") || !me.IsT ? 1 : 1 + 0.3f * MathF.Max(0, me.nades.Count - 1);   // CT guarda pro retake: quem gasta a sobra é quem ataca   // quanto mais granada na mão, mais fácil gastar uma
            if (bv < 0.12f || AmigoNoAlvo(alvo.p) || !Alcanca(alvo.p, kind == "he" && !EXP.Contains("heAlcanceVelho") ? "he" : "molotov") || rng.Rand() > (0.1f + 0.25f * MathF.Min(1, bv)) * sobra) return false;
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
                float d = (c.pos - me.pos).Len; if (d < 130 || d > 400 || !Alcanca(c.pos, EXP.Contains("heAlcanceVelho") ? "molotov" : "he")) continue;
                if (m.agents.Any(x => x.alive && x.team == me.team && (x.pos - c.pos).Len < 120)) continue;
                bool pos = m.bombPlanted && (c.pos - m.bombPos).Len < 350;   // segurando o bomb / indo desarmar
                // acabou de matar um nosso (se expôs, está ali, provavelmente machucado da troca): HE em cima dele, como pro
                bool matou = !EXP.Contains("heAlcanceVelho") && m.agents.Any(x => !x.alive && x.team == me.team && x.killedBy == m.agents[c.id] && t - x.diedAt < 3.5f);
                float pHe = (pos ? 0.4f : 0.22f) + (c.hp < 100 ? 0.15f : 0) + (matou ? 0.2f : 0) + (!EXP.Contains("heAlcanceVelho") && c.hp < 60 ? 0.2f : 0);
                if (!Rola("he dano " + c.id, pHe)) continue;
                if (matou) m.Ev("HE: no matador do colega/round");
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
            V2 prev = me.pos;
            var seguros = new List<V2> { me.pos };
            foreach (var w in p)
            {
                float seg = (w.p - prev).Len; bool parou = false;
                for (float s = 10; s <= seg; s += 10)
                {
                    V2 q = prev + (w.p - prev) * (s / seg);
                    // pode ter gente perto, ou fica na mira de onde eles costumam segurar (a AWP do janelão vê de longe)
                    if ((q - dest).Len < 100 || it.HeatIn(q, 200) > 0.05f || Perigo.Visto(me.team, q) > 0) { parou = true; break; }
                    seguros.Add(q);
                }
                if (parou) break;
                prev = w.p;
            }
            if (EXP.Contains("esperaColada")) return seguros[seguros.Count - 1];
            // quem vem pelo mesmo caminho pararia todo mundo na mesma borda do perigo: recua pelo caminho até ter espaço
            // (colado é uma granada pega dois, e um só vê o que o outro já vê)
            bool Livre(V2 q)
            {
                foreach (var x in m.agents)
                {
                    if (x == me || !x.alive || x.team != me.team) continue;
                    if ((x.pos - q).Len < 45 || x.brain.order.hold != null && (x.brain.order.hold.pos - q).Len < 45) return false;
                    // nem na frente da mira de quem está segurando: ele fica com a mira nas minhas costas e não vê nada
                    V2 dq = q - x.pos; float dl = dq.Len;
                    if (!EXP.Contains("esperaNaMira") && x.Speed < 10 && dl < 300 && MathF.Abs(MathX.AngDiff(MathF.Atan2(dq.y, dq.x), x.aim)) < 0.3f) return false;
                }
                return true;
            }
            /* fora da porta/beco (folga ≤ 15): o pro espera atrás da quina, não no batente — ali é alvo e trava quem vem atrás.
               Sem lugar largo no caminho seguro, vale o de antes. apPortaVelho desliga */
            if (EXP.Contains("apPortaLigado") && !EXP.Contains("apPortaVelho"))   /* opt-in até o A/B */
                for (int i = seguros.Count - 1; i >= 0; i--) { var (fx, fy) = NavGrid.CellOf(seguros[i]); if (NavGrid.folga[fx, fy] > 15f && Livre(seguros[i])) return seguros[i]; }
            for (int i = seguros.Count - 1; i >= 0; i--) if (Livre(seguros[i])) return seguros[i];
            return seguros[seguros.Count - 1];
        }

        /// <summary>Esperando a vez (entry ir primeiro, smoke subir, colado no entry): ajeita de um lado pro outro devagar,
        /// sem perder a mira (abaixo de 34% da velocidade continua preciso).</summary>
        void Esperando(V2[] look)
        {
            fase = "esperando a vez";
            DecidirC4(m.t);
            if (look != null && look.Length > 0) Face(PixelSemColega(look), 3); else ClearAngles(false);
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
            if (look != null && look.Length > 0)
            {   // do cover: mira na quina por onde o ângulo aparece — e a quina não pode ser as costas do colega (o pixel do
                // caminho até o ângulo às vezes cai em cima de quem está na frente)
                bool mexendo = micro == Micro.Wide || micro == Micro.Avancar;
                L = mexendo ? microRumo : LookAlvo(look); alvoMira = Pixel(L.Value);
                if (!mexendo && !EXP.Contains("esperaMiraVelha") && ColegaNaFrente(alvoMira.Value) != null)
                {
                    bool achou = false;
                    foreach (var o in look) { var q = Pixel(o); if (ColegaNaFrente(q) == null && Ameaca(o) > 0.002f) { L = o; alvoMira = q; achou = true; break; } }
                    if (!achou)
                    {
                        float a0 = (alvoMira.Value - me.pos).Atan2, dl = MathF.Min((alvoMira.Value - me.pos).Len, 300);
                        V2 e = me.pos + V2.Ang(a0 + 0.6f) * dl, d = me.pos + V2.Ang(a0 - 0.6f) * dl;
                        alvoMira = Ameaca(e) >= Ameaca(d) ? e : d;
                    }
                }
                Reservar(L.Value, alvoMira); Face(alvoMira.Value, 3);
            }
            else
            {
                ClearAngles(false); L = focus;
                if (L == null && !EXP.Contains("olharParadoVelho"))
                {   // parado sem ângulo da ordem (guardando a arma, esperando) e nenhum canto quente: olha de onde PODE vir gente
                    // (a quina mais provável, senão de onde vem o calor) — não fica com a mira onde estava andando (a parede)
                    V2? q = Borda() ?? DeOndeVem();
                    if (q != null) { q = ForaDaParede(q.Value); Face(q.Value, 3); L = q; m.Ev("parado sem ângulo: olhou de onde pode vir/round"); }
                    else if (MapData.Raio(me.pos, me.z, V2.Ang(me.aim), 450, 18f) < 40)
                    {   // nada provável à vista e a mira chapada na parede: vira pro lado aberto
                        V2 aberto = ForaDaParede(me.pos + V2.Ang(me.aim) * 120); Face(aberto, 3);
                    }
                }
            }
            if (baseP == null) { baseP = ancora = me.pos; baseDesde = t; baseLook = new V2(-999, -999); trilha.Clear(); }
            // a mira mudou: só replaneja o lugar se daqui não dá pra ver o novo ângulo (segurar é ficar, não dançar a cada call)
            bool emPilha = NavGrid.ESTADO && NavGrid.SobrePilha(me.pos) && boostEstado == 4;   /* segurando de cima da pilha: fica no topo (sem cover/peek, que são do chão) */
            if (!emPilha && micro == Micro.Nada && L != null && (L.Value - baseLook).Len > 30 && t - planAt > 1.5f
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
            if (t >= microProx && !emPilha) Escolher(L);
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
                float pl = NavGrid.PathLenAte(me.pos, s.p, 850); if (pl < 90 || pl > 850) continue;
                float v = sep * (1.3f - pl / 900f) * rng.Uniform(0.7f, 1.3f) * (EXP.Contains("semUmAngulo") ? 1 : 1 / MathF.Max(1, Exposicao(s.p, t)));
                if (v > bv) { bv = v; best = s; }
            }
            if (best == null) return false;
            var q = best.Value; m.Ev("buscou ângulo novo/round");
            SetOrder(new Order { name = "buscando outro ângulo", hold = new Hold("outro ângulo", q.p, L), quiet = true, why = $"{colegas[0].name} já olha {Region(L)} daqui: vou por outro lado pra ele ter que olhar dois lugares", paciente = order.paciente, doPlano = order.doPlano });
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
            bool velho = EXP.Contains("facaVelha");
            // ninguém do outro time vivo (round decidido, falta desarmar/andar): faca, que corre mais (CS2: faca 250 x AK 215)
            bool ninguem = !velho && m.Alive(me.team == Team.T ? Team.CT : Team.T) == 0 && action == null && !order.plant;
            // bomb plantado: o CT girando pro retake longe do bomb e sem ameaça perto também vai de faca; o TR segura com a arma
            bool plantOk = !m.bombPlanted || !velho && !me.IsT && (m.bombPos - me.pos).Len > 500;
            bool seguro;
            if (velho) seguro = !contato && action == null && it.HeatAvg(me.pos, 380) < 0.004f && it.HeatAvg(frente, 300) < 0.004f && !m.bombPlanted && t < 60;
            else if (ninguem) seguro = true;
            else if (contato || action != null || !plantOk || target != null) seguro = false;
            else
            {   // é o que ELE acha (calor estimado do time), pesado pelo jeito dele e pela pressa, decidido a cada ~1 s: às
                // vezes acha que está seguro e não está (o lurker que ninguém esperava pega ele de faca)
                if (t >= facaDecideEm)
                {
                    facaDecideEm = t + rng.Uniform(0.8f, 1.3f);
                    float r = MathF.Max(it.HeatAvg(me.pos, 380), it.HeatAvg(frente, 300)) / 0.004f;   // 1 = o antigo limite
                    float agr = P.Agr(me.skill.aggression);
                    bool pressa = m.bombPlanted && !me.IsT || m.RoundLeft < 25 || order.name.StartsWith("girando") || order.name.StartsWith("âncora girando");
                    float wFaca = (0.5f + 0.6f * agr + (pressa ? 0.6f : 0)) * MathF.Exp(-r * (1.6f - 0.8f * agr));
                    if (EXP.Contains("facaDefuse") && !EXP.Contains("facaDefuseVelho") && m.bombPlanted && !me.IsT && order.defuse && !m.defuserActive)
                    {   // indo desarmar: de rifle não chega, de faca chega → a faca é a única chance (pro corre de faca e desarma)
                        float dB0 = (m.bombPos - me.pos).Len * 1.15f, sobra0 = m.BombLeft - Match.DefuseTime(me);
                        if (sobra0 - dB0 / 54f < 0.8f && sobra0 - dB0 / MapCheck.RUN > -0.3f) { wFaca *= 4f; m.Ev("faca: pra chegar no defuse/round"); }
                    }
                    if (!EXP.Contains("facaParadoVelho"))
                    {   // a faca só vale pra CORRER uma distância: parado segurando/esperando ou quase chegando, trocar pra faca e
                        // depois sacar de novo é só atraso na hora que ele aparece (ninguém puxa faca cravado no pixel)
                        float falta = path != null && wp < path.Count ? (path[path.Count - 1].p - me.pos).Len : 0;
                        bool parado = me.Speed < me.MaxSpeed * 0.3f && wantVel.Len < 1 || falta < 250;
                        if (parado) { wFaca *= 0.03f; m.Ev("faca: parado/perto do destino, ficou com a arma/decisão"); }
                    }
                    float wArma = 0.25f + 0.35f * (1 - agr);
                    facaQuer = rng.Weighted(new[] { wArma, wFaca }) == 1;
                }
                seguro = facaQuer;
            }
            if (seguro && !me.faca && target == null && me.gun.inMag > 0 && !me.gun.Reloading)
            {
                me.faca = true; me.scoped = false;
                m.Ev(ninguem ? "faca: sem inimigo vivo/round" : "faca: com inimigo vivo/round");
                if (!logouFaca) { logouFaca = true; Think(ninguem ? "faca na mão — não tem mais ninguém deles" : "faca na mão — ninguém chega aqui ainda", $"calor perto {it.HeatAvg(me.pos, 380):0.000}, à frente {it.HeatAvg(frente, 300):0.000}: inimigo não chega aqui agora"); }
            }
            else if (!seguro && me.faca) Sacar(t, contato ? "saquei: tem contato perto" : "saquei a arma — chegando na zona de contato");
        }
        bool logouFaca, facaQuer; float facaDecideEm = -9;

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
            if (!EXP.Contains("c4Base"))
            {   // o C4 muda de mão uma vez, na base: quem vai abrir espaço entrega pra quem joga atrás e segue. Depois fica com quem está
                var eq = m.igl[(int)me.team];
                if (eq.c4Resolvido) return;
                bool livreNaoLeva = me.role == Role.Entry || me.role == Role.Lurker || me.gunId == "awp";
                // como no pro: na base o C4 vai pra quem o plano leva pro bomb. Quem vai segurar ângulo longe da base
                // (topo do meio, palácio) também não fica com ele: passa pro colega do lado ou dropa ali na base e alguém
                // pega quando o time decidir o bomb. Mais ainda no default lento (vai ficar parado lá um tempão)
                bool vaiSegurar = order.hold != null && !order.push && !order.plant && (order.hold.pos - MapData.SPAWN_T[0]).Len > 200 && !EXP.Contains("c4SeguraVelho");
                if (t < 5)
                {
                    // default (tomada de mapa lenta): como pro, às vezes o C4 fica num canto seguro da base e os cinco vão pegar
                    // espaço; quem o IGL mandar busca quando decidir o bomb (é o que dá pra fazer com cinco no mapa)
                    bool lento = (order.paciente || eq.plan.StartsWith("default")) && !eq.plan.StartsWith("rush");
                    if (lento && !EXP.Contains("c4EspacoVelho") && m.Alive(me.team) >= 4 && Rola("c4 base pro espaço", 0.3f + (order.paciente ? 0.15f : 0) - 0.1f * me.skill.aggression))
                    {
                        V2 c = CantoC4(it); m.GuardaBomb(me, c); m.c4ProEspaco = true; m.Ev("C4 deixado na base pro espaço/round"); eq.c4Resolvido = true;
                        Think($"deixo o C4 na base ({Region(c)}) e vou pegar espaço", "default: os cinco tomam o mapa, o C4 vem quando decidir o bomb");
                        return;
                    }
                    float pPassa = livreNaoLeva ? 0.8f - 0.2f * me.skill.aggression
                                 : vaiSegurar ? 0.45f + (order.paciente ? 0.25f : 0) - 0.2f * me.skill.aggression : 0;
                    if (pPassa > 0 && Rola("c4 passa na base", pPassa))
                    {
                        if (PassarNaBase()) eq.c4Resolvido = true;
                        else if (vaiSegurar && m.Alive(me.team) >= 3)
                        {
                            V2 c = CantoC4(it); m.GuardaBomb(me, c); m.Ev("C4 deixado na base/round"); eq.c4Resolvido = true;
                            Think($"dropo o C4 aqui na base ({Region(c)}) e vou segurar {order.name}", "vou ficar segurando ângulo: quem for pro bomb pega");
                        }
                    }
                    return;
                }
                if (it.HeatIn(me.pos, 380) < 0.05f) return;
                eq.c4Resolvido = true;
                if (!livreNaoLeva || eq.plan.StartsWith("rush") || it.Esperados(me.pos, 300) > 0.3f || m.Alive(me.team) < 3 || PassarC4(it)) return;
                // sem ninguém pra passar: às vezes larga num canto pra jogar solto (acontece no jogo de verdade); o normal é ficar com ele
                if (rng.Rand() < 0.1f + 0.2f * me.skill.aggression)
                {
                    V2 canto = CantoC4(it); m.GuardaBomb(me, canto); m.Ev("C4 largado pra jogar solto/round");
                    Think($"deixo o C4 no canto ({Region(canto)}) e vou jogar solto", "ninguém perto pra passar e quero abrir espaço");
                }
                return;
            }
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
        bool decidiuC4; float proxTrocaSave, esperaGrupoDesde = -1; bool? stackQuieto;

        /// <summary>Stack vale pela surpresa: enquanto ninguém viu ninguém, não se mostra nem gasta granada à toa. Cada um decide uma vez por round.</summary>
        bool EscondeStack(float t)
        {
            if (me.IsT || m.bombPlanted || !m.igl[(int)me.team].plan.StartsWith("stack") || EXP.Contains("semStackQuieto")) return false;
            stackQuieto ??= rng.Rand() < 0.85f - 0.35f * me.skill.aggression;
            if (stackQuieto != true) return false;
            foreach (var c in m.intel[(int)me.team].contacts.Values) if (t - c.t < 8 && (c.pos - me.pos).Len < 500) return false;
            return true;
        }

        Agent cuidadoDe; V2 cuidadoPos; float cuidadoAte = -9, cuidadoEspera; int cuidadoModo = -1;
        /// <summary>
        /// Um colega acabou de morrer pra alguém que eu sei onde está, e meu caminho passa por ele. Não tem regra: pesa
        /// o jeito, a vida, se tem flash, se tem colega pra ir junto, se o tempo aperta — e escolhe esperar o colega e ir
        /// junto, flashar antes de passar, ou ir assim mesmo (às vezes ele já saiu de lá, às vezes é o certo).
        /// </summary>
        bool CuidadoComMatador(float t, V2 dest)
        {
            if (t > cuidadoAte || cuidadoDe == null || !cuidadoDe.alive || target != null || order.defuse || me.guardando) return false;
            if (ContatoDe(cuidadoDe) is V2 novo) cuidadoPos = novo;
            // meu caminho chega perto dele? (o próximo trecho do caminho, até ~350 à frente)
            bool passa = false; float acc = 0;
            if (path != null) for (int k = wp; k < path.Count && acc < 350; k++) { acc += (path[k].p - (k > wp ? path[k - 1].p : me.pos)).Len; if ((path[k].p - cuidadoPos).Len < 260) { passa = true; break; } }
            if ((dest - cuidadoPos).Len < 220) passa = passa || (me.pos - cuidadoPos).Len < 600;
            if (!passa) return false;
            var colega = m.agents.Where(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 600).OrderBy(x => (x.pos - me.pos).Len).FirstOrDefault();
            if (cuidadoModo < 0)
            {
                float agr = P.Agr(me.skill.aggression), pressa = me.IsT ? (m.RoundLeft < 30 ? 1 : 0) : (m.bombPlanted && m.BombLeft < 15 ? 1 : 0);
                bool flash = me.nades.Contains("flash") && Alcanca(NaCara(cuidadoPos), "flash");
                float esperar = colega != null && (colega.pos - me.pos).Len > 120 ? 1.0f : 0.1f;
                float flashar = flash ? 1.1f : 0;
                float ir = 0.25f + 0.6f * agr + 1.5f * pressa + (me.hp > 80 ? 0.15f : 0);
                cuidadoModo = rng.Weighted(new[] { esperar, flashar, ir });
                m.Ev(cuidadoModo == 0 ? "matador conhecido: esperou o colega/round" : cuidadoModo == 1 ? "matador conhecido: flash antes/round" : "matador conhecido: foi assim mesmo/round");
                cuidadoEspera = t + rng.Uniform(3f, 6f);
            }
            if (cuidadoModo == 1 && action == null)
            {
                cuidadoModo = 2;   // depois da flash, vai
                if (!AmigoNoAlvo(cuidadoPos)) { Jogar("flash", $"flash no {cuidadoDe.name} antes de passar ({Region(cuidadoPos)})", NaCara(cuidadoPos)); return true; }
            }
            if (cuidadoModo == 0)
            {
                if (t > cuidadoEspera || colega == null || (colega.pos - me.pos).Len < 140) { cuidadoModo = 2; return false; }
                Esperando(new[] { Pixel(cuidadoPos) }); fase = "esperando o colega (matador ali)";
                Think($"o {cuidadoDe.name} matou ali — espero o {colega.name} encostar pra ir junto");
                return true;
            }
            return false;
        }

        /// <summary>Entrar pra ser trocado: se fui na frente e o colega que deveria me trocar ficou longe, espero ele encostar um pouco. Tendência: quem é agressivo espera menos.</summary>
        bool EsperaOGrupo(float t, V2 dest)
        {
            if (!me.IsT || m.bombPlanted || m.RoundLeft < 22 || target != null || EXP.Contains("semGrupo")) return false;
            // quanto esperar é leitura: bomb cheio (devem ter vários) espera mais; pouco tempo ou quem é agressivo espera menos
            bool novo = !EXP.Contains("grupoVelho");
            float eSite = m.intel[(int)me.team].Esperados(dest, 380);
            float limite = novo ? (1.5f + 2.8f * MathX.Clamp(eSite / 2f, 0, 1)) * (1.25f - 0.6f * P.Agr(me.skill.aggression)) * (m.RoundLeft < 32 ? 0.5f : 1f)
                                : 2.2f + 1.2f * (1 - me.skill.aggression);
            if (esperaGrupoDesde >= 0 && t - esperaGrupoDesde > limite) { if (t - esperaGrupoDesde > 8) esperaGrupoDesde = -1; return false; }
            if (m.intel[(int)me.team].Esperados(me.pos, 450) < 0.3f) return false;   // ainda longe de quem atira
            float meu = NavGrid.PathLen(me.pos, dest); bool temQuemVem = false, colado = false;
            foreach (var x in m.agents)
            {
                // quem vem junto: indo pro mesmo bomb (entrar, plantar), não só quem tem ordem de empurrar; o C4 entra
                // atrás, mas conta como gente vindo (o entry não abre achando que está sozinho)
                bool vem = x.brain.order.push || novo && x.brain.order.plant;
                if (x == me || !x.alive || x.team != me.team || !vem || x.hasBomb && !novo) continue;
                float ele = NavGrid.PathLen(x.pos, dest);
                if (ele < meu + 40 || (x.pos - me.pos).Len < (novo ? (EXP.Contains("grupoLonge") ? 170 : 115) : 230)) { colado = true; break; }   // junto = a distância de troca (de 170 pra lá ninguém chega a tempo de trocar)
                if (ele < meu + (novo ? 900 : 500)) temQuemVem = true;
            }
            if (colado || !temQuemVem) { esperaGrupoDesde = -1; return false; }
            if (esperaGrupoDesde < 0) { esperaGrupoDesde = t; m.Ev("esperou o colega pra entrar junto/round"); }
            return true;
        }

        /// <summary>O C4 entra atrás de quem abre: se quem está indo pro bomb ficou pra trás de mim, espero ele passar (sem tempo, vou).</summary>
        bool C4NaFrente(V2 ps)
        {
            if (m.RoundLeft < 18 || EXP.Contains("c4Frente2")) return false;
            if (m.t - ordemDesde > 6) return false;   // deram tempo de abrirem; se ninguém passou, vou
            float meu = NavGrid.PathLen(me.pos, ps), deles = float.MaxValue;
            if (meu < 110) return false;
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && x.brain.order.push && !x.brain.order.plant) deles = MathF.Min(deles, NavGrid.PathLen(x.pos, ps));
            return deles < float.MaxValue && deles > meu - 40;
        }

        /// <summary>
        /// C4 em quem não está levando ele a lugar nenhum. Não é regra de tempo: nas horas em que estou livre pra decidir pesa
        /// se o C4 aqui atrapalha o time. Ouvindo alguém, com contato ou com inimigo em cima, não faz nada (fica quieto e joga).
        /// Pesa mais quanto mais longe estou do bomb em relação a quem pode levar e quanto mais o round já andou comigo aqui.
        /// </summary>
        void DecidirC4(float t)
        {
            if (!me.hasBomb || !me.IsT || order.plant || order.push || m.bombPlanted || t < proxC4 || EXP.Contains("c4Velho")) return;
            proxC4 = t + 1.5f;
            var it = m.intel[(int)me.team]; var eqC4 = m.igl[(int)me.team]; bool semCanto = !EXP.Contains("c4Base");
            if (semCanto && (t - eqC4.c4PassadoEm < 20 || m.Alive(me.team) < 3)) return;
            if (target != null || t - somT < 2.5f || t - lastSeenT < 4 || it.Esperados(me.pos, 250) >= 0.8f) return;
            V2 bomb = Plant(m.planSite); float meu = NavGrid.PathLen(me.pos, bomb), melhor = meu;
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && x.role != Role.Entry) melhor = MathF.Min(melhor, NavGrid.PathLen(x.pos, bomb));
            float aqui = me.c4Desde >= 0 ? t - me.c4Desde : 0;
            float p = aqui / (aqui + 15f) * (melhor < meu - 60 ? 1f : 0.35f);
            if (rng.Rand() >= p) return;
            if (PassarC4(it) || semCanto) return;
            V2 canto = CantoC4(it);
            m.GuardaBomb(me, canto); m.Ev("C4 largado por estar parado/round");
            Think($"deixo o C4 no canto ({Region(canto)}) e sigo jogando", $"chance {p:0%}: {aqui:0} s aqui com ele, eu a {meu:0} do bomb e tem colega a {melhor:0}; sem ninguém perto de mim agora");
        }
        float proxC4;

        /// <summary>Vou entrar onde não conheço: dropa pro amigo que está mais pra trás (nunca pro entry nem pra quem está em duelo).</summary>
        /// <summary>
        /// O C4 virou a ponta: antes da entrada, o portador é o mais perto do bomb do plano e está chegando onde pode ter
        /// gente (a rota do pro às vezes põe ele na frente). Passa pro colega de trás — o C4 entra atrás de quem abre.
        /// Um teste por segundo; sem colega perto pra passar, segue (não força).
        /// </summary>
        void C4NaPonta(float t)
        {
            if (!me.hasBomb || !me.IsT || order.plant || m.bombPlanted || target != null || action != null || t < proxPonta || EXP.Contains("c4PontaVelho")) return;
            proxPonta = t + 1;
            var ig = m.igl[(int)me.team]; var it = m.intel[(int)me.team];
            // uma vez por round, só com perigo de verdade perto e colega logo atrás: passar à toa deixava o C4 no chão em
            // lugar disputado (e o round acabava no tempo)
            if (ig.Executou || ig.c4PontaPassou || t - ig.c4PassadoEm < 8 || it.Esperados(me.pos, 400) < 0.5f) return;
            V2 pl = Plant(m.planSite); float meu = NavGrid.PathLen(me.pos, pl);
            int atras = 0;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                float d = NavGrid.PathLen(x.pos, pl);
                if (d < meu) return;   // tem alguém na frente: o C4 já está atrás
                if ((x.pos - me.pos).Len < 250 && x.brain.target == null) atras++;
            }
            if (atras == 0) return;
            if (PassarC4(it)) { ig.c4PontaPassou = true; m.Ev("C4 passado: era a ponta/round"); }
        }
        float proxPonta;

        bool PassarC4(Intel it)
        {
            if (EXP.Contains("c4Velho")) return false;
            Agent q = null; float bh = it.HeatIn(me.pos, 200) + 0.001f;
            foreach (var x in m.agents)
            {
                // passa pra quem está do lado (o C4 cai a ~28 e ele vem pegar); longe, deixa no canto e alguém busca
                if (x == me || !x.alive || x.team != me.team || x.role == Role.Entry || x.brain.target != null || x.brain.action != null || (x.pos - me.pos).Len > (EXP.Contains("c4TelepatiaVelha") ? 420 : 160)) continue;
                float h = it.HeatIn(x.pos, 200); if (h < bh) { bh = h; q = x; }
            }
            if (q == null) return false;
            EntregaC4(q);
            Think($"passo o C4 pro {q.name} e vou entrar", $"entrada arriscada, ele está mais pra trás (risco ali {bh:0.00})");
            return true;
        }
        void EntregaC4(Agent q)
        {
            V2 ponto = NavGrid.Snap(q.pos);   // no pé dele: o meio do caminho pode ser parede
            // CS2: o G joga o C4 curto (~28 aqui) na direção dele; ele vem pegar no chão. No pé dele (o antigo, -exp
            // c4TelepatiaVelha) era o C4 sumir de uma mão e aparecer na outra a até 420
            if (!EXP.Contains("c4TelepatiaVelha"))
            {
                V2 d = q.pos - me.pos; float l = d.Len;
                ponto = NavGrid.Snap(l < 1 ? me.pos : me.pos + d / l * MathF.Min(28, l));
                if (!MapData.LosWalk(me.pos, me.z, ponto, NavGrid.ZAt(ponto), 8f) || float.IsInfinity(NavGrid.PathLen(ponto, Plant('A')))) ponto = NavGrid.Snap(me.pos);
            }
            m.GuardaBomb(me, ponto); m.Ev("C4 passado pro amigo/round");
            var eq = m.igl[(int)me.team]; eq.c4PassadoEm = m.t; eq.c4Resolvido = true;
            var antes = q.brain.order;
            q.brain.SetOrder(new Order { name = "pegar o bomb", dest = ponto, why = $"{me.name} vai jogar na frente e me passou o C4", until = m.t + (ponto - q.pos).Len / MathF.Max(q.MaxSpeed, 1) * 1.3f + 1, then = antes });
        }
        /// <summary>Na base: quem abre espaço entrega o C4 pro colega mais perto que joga atrás (suporte primeiro).</summary>
        bool PassarNaBase()
        {
            bool velho = EXP.Contains("passeVelho");
            Agent q = null; float melhor = velho ? 420 : 650;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team || x.role == Role.Entry || x.role == Role.Lurker || x.gunId == "awp") continue;
                float d = (x.pos - me.pos).Len + (x.role == Role.Suporte ? 0 : velho ? 100 : 60);
                if (!velho)
                {   // o C4 vai pra quem o plano leva pro lado do bomb, não pra quem vai ficar segurando o meio
                    V2 vai = x.brain.order.hold?.pos ?? x.brain.order.dest;
                    if (vai.Len > 1) d += 0.25f * MathF.Min(NavGrid.PathLen(vai, Plant('A')), NavGrid.PathLen(vai, Plant('B')));
                }
                if (d < melhor) { melhor = d; q = x; }
            }
            if (q == null) return false;
            EntregaC4(q);
            Think($"passo o C4 pro {q.name} aqui na base", "vou abrir espaço, ele joga atrás");
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
            bool naPilha = NavGrid.ESTADO && NavGrid.SobrePilha(me.pos) && NavGrid.SobrePilha(p);   /* em cima da pilha (fora da malha): anda direto no topo */
            if (naPilha || NavGrid.Direct(me.pos, p)) { if (l < 8) spd *= MathF.Max(0.35f, l / 8); wantVel = d / l * spd; me.move = spd > me.MaxSpeed * 0.6f ? Move.Correndo : Move.Andando; }
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
            posEspecial = 0;
            if (!EXP.Contains("semPosEspecial") && PosicaoEspecial(a, L, zL, cand)) return;
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

        /// <summary>
        /// Acabei de matar e o time ficou na frente (5x4, 4x3...): como pro, o padrão é SEGURAR a vantagem — sair do
        /// ângulo onde matei (o próximo deles vem trocar mirando ali) e segurar de um lugar coberto perto, olhando pra onde
        /// ele veio; depois volto pro que fazia. Seguir em frente pesa pra quem é agressivo, está embalado (2+ abates no
        /// round), está na entrada do bomb ou é TR sem tempo. Objetivo (plantar, desarmar, retake) sempre segue.
        /// </summary>
        public void Abateu(Agent v)
        {
            if (!me.alive || EXP.Contains("semSegurarVantagem")) return;
            Team outro = me.team == Team.T ? Team.CT : Team.T;
            int nos = m.Alive(me.team), eles = m.Alive(outro);
            if (eles == 0 || nos <= eles) return;   // sem vantagem: segue o jogo
            if (order.plant || order.defuse || me.hasBomb || m.bombPlanted || me.guardando || action != null) return;
            if (m.t - ordemDesde < 2f && !EXP.Contains("vantagemSempre")) return;   // acabou de receber ordem do time: ela já cuida da situação
            float agr = P.Agr(me.skill.aggression), vant = nos - eles;
            float wSegura = 1f + 0.35f * vant + 0.6f * (1 - agr);
            float wSegue = 0.25f + 0.7f * agr + (me.kills >= 2 ? 0.4f * (me.kills - 1) : 0) + (me.IsT && m.RoundLeft < 30 ? 1.2f : 0) + (order.push ? 0.8f : 0);
            if (rng.Weighted(new[] { wSegura, wSegue }) == 1) { m.Ev("abateu com vantagem: seguiu em frente/round"); return; }
            // um lugar perto, pra trás da linha de onde ele veio e fora da vista de lá (se tiver)
            V2 de = v.pos, fora = me.pos - de; float lf = fora.Len; if (lf < 1) return; fora = fora / lf;
            V2 ponto = me.pos; bool coberto = false;
            foreach (float r in new[] { 30f, 50f, 70f })
            {
                foreach (float ang in new[] { 0f, 0.5f, -0.5f, 1f, -1f })
                {
                    V2 q = NavGrid.Snap(me.pos + V2.Ang(fora.Atan2 + ang) * r);
                    if (!NavGrid.Movable(q) || !NavGrid.Direct(me.pos, q)) continue;
                    if (!MapData.LosWalk(de, NavGrid.ZAt(de), q, NavGrid.ZAt(q), 18f)) { ponto = q; coberto = true; break; }
                }
                if (coberto) break;
            }
            m.Ev("abateu com vantagem: segurou/round");
            var antes = order;
            SetOrder(new Order { name = "segurando a vantagem", hold = new Places.Hold("vantagem", ponto, de), until = m.t + rng.Uniform(5f, 10f), then = antes,
                why = $"{nos}x{eles}: não dou o mesmo peek — o próximo deles vem trocar mirando onde eu matei" });
            Think(coberto ? $"matei o {v.name}: saio do ângulo e seguro a vantagem" : $"matei o {v.name}: seguro a vantagem daqui");
        }

        /// <summary>1 = só a cabeça aparece (caixa tapa o corpo); 2 = em cima de caixa (ângulo que ninguém pré-mira).</summary>
        int posEspecial;
        /// <summary>
        /// Segurar como pro, quando o lugar deixa: (1) do lado de uma caixa baixa onde em pé o olho vê o ângulo e a caixa
        /// tapa o corpo — de lá só a cabeça aparece pra quem vem; (2) em cima de uma caixa perto, pegando quem chega
        /// mirando na altura de sempre. Pesado pela situação: a cabeça só é quase sempre boa (mais pra quem é calmo); a
        /// caixa é surpresa — mais pra quem é agressivo, quando deve vir gente, e não toda vez (vira previsível).
        /// </summary>
        bool PosicaoEspecial(V2 a, V2 L, float zL, List<(V2 p, bool ve)> cand)
        {
            if ((L - a).Len < 1) return false;
            float agr = P.Agr(me.skill.aggression);
            // (1) só a cabeça: vê de pé, e do ângulo não se vê o meu tronco
            V2? cab = null; float dc = float.MaxValue;
            foreach (var c in cand)
            {
                if (!c.ve) continue;
                float zc = NavGrid.ZAt(c.p);
                if (MapData.LosWalk(c.p, zc + 13f, L, zL + 18f, 0f)) continue;   // tronco à vista: não é cabeça só
                float d = (c.p - a).Len; if (d < dc) { dc = d; cab = c.p; }
            }
            // (2) em cima de caixa: célula de topo perto, alcançável, que vê o ângulo lá de cima
            V2? cx = null; float dx = float.MaxValue;
            var (ax, ay) = NavGrid.CellOf(a);
            for (int gx = ax - 22; gx <= ax + 22; gx += 2)
                for (int gy = ay - 22; gy <= ay + 22; gy += 2)
                {
                    if (gx < 0 || gy < 0 || gx >= NavGrid.NGX || gy >= NavGrid.NGY || !NavGrid.onTop[gx, gy] || !NavGrid.move[gx, gy] || NavGrid.soBoost[gx, gy]) continue;   /* topo de pilha é só de pezinho (decisão à parte) */
                    V2 q = NavGrid.Center(gx, gy); float d = (q - a).Len; if (d > 46 || d >= dx) continue;
                    if (!MapData.LosWalk(q, NavGrid.ZAt(q), L, zL, 18f)) continue;
                    bool colega = false; foreach (var o in m.agents) if (o != me && o.alive && o.team == me.team && (o.pos - q).Len < 20) colega = true;
                    if (colega) continue;
                    float pl = NavGrid.PathLenAte(me.pos, q, d * 2.5f + 40); if (float.IsInfinity(pl) || pl > d * 2.5f + 40) continue;
                    dx = d; cx = q;
                }
            if (cab == null && cx == null) return false;
            var it = m.intel[(int)me.team];
            float vem = MathF.Min(1, it.HeatAvg(L, 120) * 2f);
            float wNormal = 1f;
            float wCab = cab != null ? 1.1f + 0.5f * (1 - agr) : 0;
            float wCaixa = cx != null && me.gunId != "awp" && caixaNoRound < 2 ? (0.12f + 0.45f * agr) * (0.4f + vem) : 0;
            int op = rng.Weighted(new[] { wNormal, wCab, wCaixa });
            if (op == 0) return false;
            deCover = false; temPeek = false;
            if (op == 1)
            {
                baseP = cab.Value; posEspecial = 1; m.Ev("segurou só com a cabeça (caixa na frente)/round");
                Think($"seguro {Onde(L)} do lado da caixa: só a cabeça aparece", "a caixa tapa o corpo: pra me acertar ele tem que acertar a cabeça");
            }
            else
            {
                baseP = cx.Value; posEspecial = 2; caixaNoRound++; m.Ev("segurou em cima da caixa/round");
                Think($"subo na caixa pra segurar {Onde(L)} de um ângulo que ele não espera", $"quem vem mira na altura de sempre{(vem > 0.3f ? "; deve vir gente agora" : "")}");
            }
            return true;
        }
        int caixaNoRound;

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
                float pl = NavGrid.PathLenAte(me.pos, c.q, 340);
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
                ? P.bang * (calor + (contatoL ? 0.6f : 0)) * (0.5f + agr) * (EXP.Contains("utilVolumeVelho") ? 1 : 1 + 0.4f * MathF.Max(0, me.nades.Count(n => n == "flash") - 1) + (me.IsT ? 0.15f * MathF.Max(0, me.nades.Count - 2) : 0)) : 0;   // granada sobrando na mão: morrer com ela é desperdício
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
            /* LINHA LONGA CEDO (apLinhaCedoLigado): nos primeiros ~22 s, sem info (nada visto nem ouvido perto), segurar parado numa
               linha longa (AWP meio × janelão) é o duelo que o pro evita sem motivo — ele espia e sai (shoulder/jiggle) e espera
               a info. A posição continua a mesma; muda o COMO. Com info, volta ao normal */
            if (EXP.Contains("apLinhaCedoLigado") && L != null && m.t < 22 && !contato && !ouviuPerto && temPeek)
            {
                float alc = MapData.Raio(me.pos, me.z, V2.Ang((L.Value - me.pos).Atan2), 700, 18f), kL = MathX.Clamp((alc - 300) / 300, 0, 1);
                if (kL > 0)
                {
                    u[(int)Micro.Quieto] *= 1 - 0.55f * kL; u[(int)Micro.Jiggle] *= 1 + 1.0f * kL;
                    if (me.IsT && me.gunId == "awp") u[(int)Micro.Avancar] *= 1 - 0.6f * kL;   /* AWP do TR no meio: peek longo só com info */
                }
            }
            // time na frente (5x4...): quem segura não sai caçando — wide e avançar ficam raros, ficar quieto vale mais
            if (!EXP.Contains("semSegurarVantagem"))
            {
                int vantM = m.Alive(me.team) - m.Alive(me.team == Team.T ? Team.CT : Team.T);
                if (vantM > 0 && !(me.IsT && m.RoundLeft < 30))
                {
                    float f = 1f / (1f + 0.6f * vantM * (1.3f - agr));
                    u[(int)Micro.Wide] *= f; u[(int)Micro.Avancar] *= f; u[(int)Micro.Quieto] *= 1f + 0.3f * vantM;
                }
            }
            if (posEspecial != 0 && !EXP.Contains("semPosEspecial")) { u[(int)Micro.Jiggle] = 0; u[(int)Micro.Wide] = 0; u[(int)Micro.Ajeitar] = 0; u[(int)Micro.Avancar] = 0; u[(int)Micro.Quieto] *= 1.6f; }   // não desce da caixa nem sai da cabeça só à toa
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
                if (!EXP.Contains("bxSaidaSmokeVelho") && SaidaSmoke(L.Value) != null) { m.Ev("smoke no ângulo: fiquei segurando a saída/round"); Think($"smoke em {Onde(L)} — devem cruzar: fico parado mirando a saída"); }
                else
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
            if (esc != Micro.Quieto && esc != Micro.Trocar && EscondeStack(t)) { esc = Micro.Quieto; m.Ev("CT quieto no stack/round"); }
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
            { if (!EXP.Contains("c4Base")) { if (m.t - m.igl[(int)me.team].c4PassadoEm > 20) PassarC4(it); } else if (!PassarC4(it)) { m.GuardaBomb(me); Think($"deixo o C4 aqui atrás ({Region(me.pos)}) e vou buscar espaço"); } }
            // call de dano: inimigo fraco perto (alguém do time tirou vida dele agora) → vai em cima
            foreach (var c in it.contacts.Values)
                if (t - c.hpT < 4 && c.hp < 45 && m.agents[c.id].alive && (c.pos - me.pos).Len < 450 && me.gunId != "awp" && Rola("fraco" + c.id + "_" + (int)c.hpT, 0.45f + 0.4f * agr))
                { esc = Micro.Wide; L = c.pos; Think($"{m.agents[c.id].name} tá com {c.hp} — vou em cima"); break; }
            // vê a jogada do aliado: ele está saindo largo do lado → sai junto (o inimigo não consegue mirar nos dois)
            Agent dupla = null;
            foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 130 && x.brain.ComecandoWide) dupla = x;
            if (dupla != null && me.gunId != "awp" && rng.Rand() < 0.4f + 0.5f * agr) { esc = Micro.Wide; L = dupla.brain.Rumo; }

            // o colega avisou "vou bangar" pro ângulo que eu seguro: saio junto quando estourar (em vez de outra jogada)
            if (L != null && me.bangAmigaDe != me && t < me.bangAmigaEm - 0.3f && (me.bangAmigaPos - L.Value).Len < 220 && me.gunId != "awp" && rng.Rand() < 0.5f + 0.4f * agr)
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
                    /* jump-peek: pula na saída do jiggle pra pegar info (alvo subindo de lado é difícil pra AWP; no ar vê por cima
                       do que é baixo). Raro e situacional: AWP deles no ângulo, SMG/pistola (leve, perde pouco), agressivo.
                       De rifle sem AWP quase nunca; AWPer não pula. apJumpPeekVelho desliga */
                    jigglePulo = false;
                    if (!EXP.Contains("apJumpPeekVelho") && me.gunId != "awp" && (awp || me.ArmaLeve))   /* rifle sem AWP no ângulo: nem sorteia */
                    {
                        float pj = (awp ? 0.35f : 0.04f) * (me.ArmaLeve ? 1.6f : 0.5f) * (0.5f + P.Agr(me.skill.aggression));
                        jigglePulo = rng.Rand() < pj;
                    }
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
                    gingando = !EXP.Contains("apGingaVelho") && DecideGinga(L, ouviuPerto);
                    Think($"segurando apertado {Onde(L)}{(ouviuPerto ? " (ouvi passo)" : "")}{(gingando ? " — gingando de lado (linha de AWP / sem contato perto)" : "")}"); break;
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
                    if (gingando) Ginga(L);
                    return;
                case Micro.Jiggle:
                    if (microFase == 0 && jigglePulo && !me.noAr && me.Speed > me.MaxSpeed * 0.75f) { me.querPular = true; jigglePulo = false; m.Ev("jump-peek pra info/round"); Think("pulo na quina pra ver (jump-peek)"); }
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

        void FimMicro() { micro = Micro.Nada; microProx = m.t + P.pausa * rng.Uniform(0.4f, 1.6f); gingando = false; }

        /* ---- ginga segurando (apGingaVelho desliga) ------------------------------------------------------------------
           Pro segurando ângulo de rifle fica parado com a mira no lugar (atira primeiro). Mas na linha de uma AWP, ou
           esperando sem contato perto, ele se mexe de lado atrás do ponto (shuffle): parado é o alvo que a AWP pega, e
           alvo cruzando de lado é difícil de acertar (rastreio, Combat.Fire). Tem custo: andando a arma dele erra
           (inMove). Quando acredita que vem contato (passo perto, colega viu alguém perto, muita gente esperada em cima)
           ele PARA e segura parado pra atirar primeiro. Tudo pela crença do time (intel), não pela posição real. */
        bool gingando; int gingaLado = 1; V2 gingaCentro; float gingaAmp; bool jigglePulo;
        bool DecideGinga(V2? L, bool ouviuPerto)
        {
            if (me.gunId == "awp" || ouviuPerto || L == null || target != null) return false;
            var it = m.intel[(int)me.team];
            if (ContatoIminente()) return false;
            // linha de AWP: o mapa de perigo (onde a AWP deles costuma segurar) ou a AWP deles vista há pouco com visão em mim
            float awp = Perigo.Visto(me.team, me.pos) >= 2 ? 1f : 0f;
            foreach (var c in it.contacts.Values)
            {
                var e = m.agents[c.id];
                if (!e.alive || !c.seen || m.t - c.t > (EXP.Contains("apGingaAwpCurta") ? 20 : 45) || e.gunId != "awp") continue;   /* AWP vista: o time sabe que ela está por ali (o AWPer não some) */
                float d = (c.pos - me.pos).Len;
                if (d > 350 && d < 1400 && MapData.LosWalk(c.pos, c.z + 18, me.pos, me.z + 18, 18f)) awp = 1f;
            }
            float calmo = MathX.Clamp(1 - it.Esperados(me.pos, 350), 0, 1);
            float p = (TeamBrain.ExpNum("apGingaBase", 0.12f) + 0.55f * awp) * (0.6f + 0.6f * P.Agr(me.skill.aggression)) * (0.3f + 0.7f * calmo) * (me.ArmaLeve ? 1.3f : 1f);
            if (rng.Rand() >= p) return false;
            // espaço de lado (perpendicular à mira) dos dois lados, andável e em linha reta
            V2 d0 = L.Value - me.pos; float l0 = d0.Len; if (l0 < 1) return false;
            V2 perp = new V2(-d0.y / l0, d0.x / l0); float amp = rng.Uniform(6f, 9f);
            if (!NavGrid.Movable(me.pos + perp * amp) || !NavGrid.Movable(me.pos - perp * amp) || !NavGrid.Direct(me.pos + perp * amp, me.pos - perp * amp)) return false;
            gingaCentro = me.pos; gingaAmp = amp; gingaLado = rng.Rand() < 0.5f ? 1 : -1;
            m.Ev(awp > 0 ? "ginga: segurando na linha de AWP/round" : "ginga: segurando sem contato perto/round");
            return true;
        }
        /// <summary>O time acredita que vem gente agora: passo perto, colega viu inimigo perto, muita gente esperada em cima.</summary>
        bool ContatoIminente()
        {
            if (m.t - somT < 3f && somK == "passos" && (somP - me.pos).Len < 450) return true;
            var it = m.intel[(int)me.team];
            foreach (var c in it.contacts.Values) if (m.agents[c.id].alive && m.t - c.t < 1.5f && (c.pos - me.pos).Len < 600) return true;
            return it.Esperados(me.pos, 300) > 0.8f;
        }
        void Ginga(V2? L)
        {
            if (ContatoIminente()) { gingando = false; m.Ev("ginga: parou pro contato/round"); Think("vem gente — paro e seguro parado pra atirar primeiro"); return; }
            if (L == null) return;
            V2 d0 = L.Value - gingaCentro; float l0 = d0.Len; if (l0 < 1) return;
            V2 perp = new V2(-d0.y / l0, d0.x / l0);
            V2 alvo = gingaCentro + perp * (gingaLado * gingaAmp);
            if ((alvo - me.pos).Len < 1.5f) { gingaLado = -gingaLado; return; }
            Ir(alvo, me.MaxSpeed * 0.55f);
        }

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
            if (kind != "smoke" && kind != "molotov" && (kind != "he" || EXP.Contains("heAlcanceVelho"))) return true;
            float erro;
            if (Arremesso.Velha) erro = Match.MiraCerta(me.pos, me.z, alvo, kind).erro;
            else
            {   // dá pra cair lá daqui? quem sabe a lineup pode pular/correr; quem não sabe, só parado/andando
                Places.Lineup lin = null; foreach (var x in Places.TREINADAS) if (x.name == nome) { lin = x; break; }
                if (lin == null) lock (Places.BANCO) foreach (var x in Places.BANCO) if (x.name == nome) { lin = x; break; }
                bool sabe = lin != null && ConheceLineup(lin);
                erro = Arremesso.Resolve(me.pos, me.z, alvo, kind, sabe ? Arremesso.Tipo.CorrePulo : Arremesso.Tipo.Correndo).erro;
            }
            if (erro <= 40) return true;
            m.Ev("granada não jogada: daqui não cai no pixel/round");
            Think($"{nome}: daqui não chega", $"testei o arremesso e ele para a {erro:0} do alvo");
            return false;
        }

        /// <summary>Daqui o arco chega lá (sem prédio no meio)?</summary>
        /// <summary>Granada guardada pra entrada do retake (flash vai na cara de quem está lá).</summary>
        public bool JogaRetake(string kind, V2 alvo)
        {
            if (action != null || !Alcanca(kind == "flash" ? NaCara(alvo) : alvo, kind) || AmigoNoAlvo(alvo)) return false;
            V2 cai = kind == "flash" ? NaCara(alvo) : alvo;
            Jogar(kind, $"{kind} na entrada do retake", cai);
            return action != null;
        }

        /// <summary>Dá pra cair lá daqui, sem lineup (até correndo)? Pela granada certa: molotov/HE/smoke no chão, flash no ar.</summary>
        bool Alcanca(V2 alvo, string kind = "molotov") => Arremesso.Velha ? (Match.Arco(me.pos, me.z, alvo) - alvo).Len < 20 : Arremesso.Resolve(me.pos, me.z, alvo, kind, Arremesso.Tipo.Correndo).erro < (kind == "flash" ? 45 : 30);

        /// <summary>
        /// Se esse jogador sabe essa lineup (lugar + mira + jeito). Ninguém sabe todas: quem tem mais utilitária sabe mais
        /// (55% a 100%); é fixo pra ele (é o repertório do jogador). Quem não sabe joga "no olho" (parado ou andando, erra mais).
        /// </summary>
        static int Estavel(string x) { int h = 17; foreach (char c in x) h = h * 31 + c; return h; }
        public bool ConheceLineup(Places.Lineup l)
        {
            if (EXP.Contains("todosSabem")) return true;
            if (Places.DoBanco(l)) return Places.Aprendeu(me.name, l);   // aprendida: só quem aprendeu
            uint h = (uint)(Estavel(me.name) * 31 + Estavel(l.name));   // do jogador (é repertório dele, não sorteio do round)
            h ^= h >> 13; h *= 0x5bd1e995; h ^= h >> 15;
            return (h % 1000) / 1000f < 0.55f + 0.45f * me.skill.utilitaria;
        }

        /// <summary>Joga uma granada daqui (a ação leva ~0,5 s); devolve quando ela estoura/cai.</summary>
        float Jogar(string kind, string nome, V2 alvo)
        {
            // smoke/molotov que eu sei de outro jeito (aprendi acertando daqui de perto): vou no pixel, paro, miro e jogo
            if ((kind == "smoke" || kind == "molotov") && !EXP.Contains("semLineupAprendida") && target == null && action == null)
            {
                var lb = Places.DoBanco(me.name, kind, alvo, me.pos, 28, 140);
                if (lb != null && (lb.from - me.pos).Len > 14 && !order.nades.Contains(lb) && NavGrid.PathLenAte(me.pos, lb.from, 180) < 180)
                {
                    order.nades.Insert(0, lb); m.Ev("lineup: foi pro pixel de uma aprendida/round");
                    Think($"{nome}: sei essa — vou no pixel");
                    return m.t + (lb.from - me.pos).Len / (MapCheck.RUN * 0.8f) + 0.9f + (kind == "smoke" ? 1.5f : 1f);
                }
            }
            if (!CaiNoLugar(kind, alvo, nome)) return m.t;
            if (!EXP.Contains("utilAToaVelho") && !UtilVale(kind, alvo, nome)) return m.t;
            // flash pra cegar quem eu JÁ estou vendo não serve (ele também me vê: é atirar, não bangar)
            if (kind == "flash" && !EXP.Contains("flashVendo"))
                foreach (var e in m.agents)
                    if (e.alive && e.team != me.team && (e.pos - alvo).Len < 260 && CanSee(e))
                    { m.Ev("flash: não bangou — já via o inimigo/round"); Think($"não bango: já estou vendo o {e.name}"); return m.t; }
            var l = new Lineup(nome, kind, me.pos, alvo);
            action = "granada"; actionNade = l; actionEnd = SoltaGranada(m.t, 0.5f); fase = "ação";
            Think(nome);
            // quando estoura: flash e HE pelo pavio (sai da mão em actionEnd); o resto, pelo voo
            if (!EXP.Contains("estouroVelho") && kind == "flash") return actionEnd + Match.FUSE_FLASH;
            if (!EXP.Contains("estouroVelho") && kind == "he") return actionEnd + Match.FUSE_HE;
            return actionEnd + 0.4f + (alvo - me.pos).Len / 380f;
        }

        /// <summary>
        /// Granada decidida na hora só sai se vale: (1) tem inimigo vivo (bomb plantado e TR todo morto: nada de bang nem
        /// molotov — só desarmar); (2) flash/HE/molotov só onde eu ACREDITO que tem gente (inimigos esperados ali pela
        /// intel: visto, ouvido, call, por onde podem ter passado) — bomb no B e flash pro alto no A não cega ninguém;
        /// (3) o colega já jogou ou está jogando a mesma coisa no mesmo lugar: não duplica (os dois tinham a mesma ideia
        /// ao mesmo tempo). Smoke fica de fora do (2): fecha uma linha, não precisa de gente no ponto. -exp utilAToaVelho
        /// </summary>
        bool UtilVale(string kind, V2 alvo, string nome)
        {
            var eles = me.IsT ? Team.CT : Team.T;
            if (m.Alive(eles) == 0) { m.Ev("utilitário: não jogou — nenhum inimigo vivo/round"); Think($"{nome}? não tem mais ninguém deles vivo"); return false; }
            if (kind == "smoke" && SmokeJaCobre(alvo)) { m.Ev("utilitário: guardou a smoke — o lugar já está smokado/round"); Think($"{nome}? já tem smoke lá — guardo a minha pra quando precisar"); return false; }
            float esp = m.intel[(int)me.team].Esperados(alvo, 350);
            if (Match.Auditar) m.Aud($"utilitário na hora ({kind}): inimigos esperados no alvo", esp < 0.08f ? "a <0,08" : esp < 0.3f ? "b 0,08-0,3" : esp < 1 ? "c 0,3-1" : "d 1+");
            if (kind != "smoke" && esp < 0.08f && !preNadeAgora) { m.Ev($"utilitário: não jogou {kind} — ninguém esperado ali/round"); Think($"{nome}? não acho que tem alguém lá"); return false; }
            // o colega já jogou/está jogando o mesmo no mesmo lugar: no MESMO instante é duplicata (os dois tiveram a mesma
            // ideia); ESCALONADA (a minha estoura depois que a dele passou: cobre quem entra em seguida, como os bangs do
            // fundo) vale pela crença — mais inimigos esperados ali, mais vale a segunda
            float minha = m.t + 0.5f + (kind == "flash" ? Match.FUSE_FLASH : kind == "he" ? Match.FUSE_HE : 1f), dele = -1;
            foreach (var n in m.flying)
                if (n.by != me && n.by.team == me.team && n.l.kind == kind && (n.l.to - alvo).Len < 150 && m.t - n.t0 < 2.5f) dele = MathF.Max(dele, n.t1);
            foreach (var x in m.agents)
                if (x != me && x.alive && x.team == me.team && x.brain.action == "granada" && x.brain.actionNade != null && x.brain.actionNade.kind == kind && (x.brain.actionNade.to - alvo).Len < 150)
                    dele = MathF.Max(dele, x.brain.actionEnd + (kind == "flash" ? Match.FUSE_FLASH : kind == "he" ? Match.FUSE_HE : 1f));
            if (dele < 0) return true;
            bool escalonada = kind == "flash" && minha - dele >= 0.8f;
            float pSegunda = escalonada ? Math.Clamp(0.2f + 0.35f * esp, 0.05f, 0.9f) : 0;
            if (rng.Rand() < pSegunda) { m.Ev("utilitário: flash escalonada em cima da do colega/round"); return true; }
            m.Ev($"utilitário: não duplicou {kind} do colega/round"); return false;
        }

        /// <summary>
        /// Já tem smoke ali: jogar outra é pela crença, não por corte fixo. O pro conta o tempo da que está lá e pensa até
        /// quando o time precisa daquela linha fechada (chegar lá e passar). Se ela acaba antes disso, renova (ou guarda pra
        /// jogar na hora); se dura mais que o necessário, guarda a dele pra quando precisar. Smoke do colega já voando pro
        /// mesmo ponto é duplicata (a informação é certa: não tem o que pesar). Decidido uma vez por lugar a cada ~2 s.
        /// </summary>
        bool SmokeJaCobre(V2 alvo)
        {
            foreach (var n in m.flying) if (n.by.team == me.team && n.l.kind == "smoke" && (n.l.to - alvo).Len < 60) return true;
            float z = NavGrid.ZAt(alvo) + 18, resta = -1;
            foreach (var sm in m.smokes)
                if (sm.SampleVis(new CSM.View.V3(alvo.x, alvo.y, z), m.t) > 0.3f) resta = MathF.Max(resta, CSM.View.SmokeVolume.LIFE - sm.age);
            if (resta < 0) return false;
            if ((alvo - smokeDecAlvo).Len < 60 && m.t - smokeDecT < 2f) return smokeDecCobre;
            // até quando eu (e quem vem comigo) precisa da linha fechada: chegar perto dela e cruzar
            float preciso = (alvo - me.pos).Len / (MapCheck.RUN * 0.8f) + 3.5f;
            float pJoga = Math.Clamp(0.5f + (preciso - resta) / 4f, 0.03f, 0.97f);
            smokeDecAlvo = alvo; smokeDecT = m.t; smokeDecCobre = rng.Rand() >= pJoga;
            if (Match.Auditar) m.Aud("smoke em cima de smoke: decisão", smokeDecCobre ? "guardou" : "renovou (a de lá acaba antes)");
            return smokeDecCobre;
        }
        V2 smokeDecAlvo; float smokeDecT = -9; bool smokeDecCobre;

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
            if (!EXP.Contains("naCaraVelha"))
            {   // flash boa estoura onde QUEM ESTÁ LÁ vê (no ar, ~40 acima), de preferência atrás da quina pra mim (pop):
                // ponto que só eu vejo cega ninguém. Procura no caminho de lá até mim e um pouco pros lados
                float zL = NavGrid.ZAt(NavGrid.Snap(L)); V2 dn = d / l, ld = new V2(-dn.y, dn.x);
                V2? melhor = null; float bs = float.MaxValue;
                if (!EXP.Contains("bxFlashCrencaVelho") && FlashOndeAcredito(L, dn, ld, l) is V2 fc) return fc;
                foreach (float k in new[] { 40f, 65f, 90f, 120f, 150f })
                    foreach (float s in new[] { 0f, 35f, -35f })
                    {
                        if (k > l * 0.85f) continue;
                        V2 q = L + dn * k + ld * s; float zq = NavGrid.ZAt(NavGrid.Snap(q)) + 40;
                        if (!MapData.LosWalk(L, zL, q, zq, 18f)) continue;   // de lá não vê: não cega
                        bool euVejo = MapData.LosWalk(me.pos, me.z, q, zq, 18f);
                        float sc = k * 0.5f + MathF.Abs(s) * 0.3f + (euVejo ? 25 : 0);
                        if (sc < bs) { bs = sc; melhor = q; }
                    }
                if (melhor != null) return melhor.Value;
            }
            foreach (float k in new[] { 45f, 75f, 110f })
            {
                V2 q = L + d / l * MathF.Min(k, l * 0.8f);
                if (MapData.LosWalk(me.pos, me.z + 10, q, NavGrid.ZAt(q) + 14, 18f)) return q;
            }
            return PontoVisivelNoCaminho(L, 70, 190);
        }

        /// <summary>
        /// bx: a flash estoura onde quem eu ACREDITO que está perto de L vai ver (células prováveis da crença do time, não só o
        /// ângulo genérico): cada ponto candidato vale a soma das probabilidades de quem tem visão dele. Pop (atrás da quina
        /// pra mim) é melhor: eu não me cego e ele não vê a granada chegando. Sem crença perto de L, fica o ângulo (antigo).
        /// </summary>
        V2? FlashOndeAcredito(V2 L, V2 dn, V2 ld, float l)
        {
            var prov = m.intel[(int)me.team].Provaveis(L, 220, 5);
            float tot = 0; foreach (var pv in prov) tot += pv.w;
            if (tot < 0.08f) return null;
            V2? melhor = null; float bs = 0.05f * tot;
            foreach (float k in new[] { 30f, 55f, 80f, 110f, 145f, 185f })
                foreach (float sd in new[] { 0f, 40f, -40f })
                {
                    if (k > l * 0.85f) continue;
                    V2 q = L + dn * k + ld * sd; if (!NavGrid.Movable(NavGrid.Snap(q))) continue;
                    float zq = NavGrid.ZAt(NavGrid.Snap(q)) + 40, vale = 0;
                    foreach (var pv in prov) if (MapData.LosWalk(pv.p, NavGrid.ZAt(pv.p), q, zq, 18f)) vale += pv.w;
                    if (vale <= 0) continue;
                    bool euVejo = MapData.LosWalk(me.pos, me.z, q, zq, 18f);
                    float sc = vale * (euVejo ? 0.75f : 1f) - 0.0006f * k * tot;
                    if (sc > bs) { bs = sc; melhor = q; }
                }
            if (Match.Auditar) m.Aud("flash: alvo pela crença", melhor == null ? "ninguém provável vê nenhum ponto — ângulo" : bs >= 0.6f * tot ? "a maioria provável vê" : "parte provável vê");
            return melhor;
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

        /* bx: PRE-NADE (granada "de praxe"): o pro joga molotov/HE no canto onde PODE ter alguém sem ter visto ninguém —
           abrindo o bomb, no mocadinho, no ângulo que eles mais seguram. Vale = P(tem alguém ali: a crença de agora +
           onde eles costumam ficar) × quanto aquele canto custa pra quem entra (pega de perto, de lado/de costas) × a
           urgência (vou passar na frente dele já) − o valor de guardar a granada (pós-plant/retake). Pouca info: testa
           com a granada mais barata. Sem limiar: a chance de jogar cresce com o ganho. -exp bxPreNadeLigado (opt-in até o A/B) */
        public static bool PreNade => EXP.Contains("bxPreNadeLigado");
        float proxPreNade; bool preNadeAgora;
        sealed class PreNadeReg { public V2 p; public float t; public string kind, porque; public List<(Agent e, int hp, V2 pos, float cego)> real; }
        readonly List<PreNadeReg> preNades = new List<PreNadeReg>();
        /// <summary>
        /// bx: pre-nade — a MESMA crença pra qualquer granada, e o TIPO sai da situação de cada canto do meu caminho:
        /// flash quando EU vou dar o peek nele logo (estoura e eu chego cegando); HE pra testar barato ou machucar quem
        /// segura ali; molotov pra limpar canto fechado perto de onde eu passo (não no meu caminho: ela me trava);
        /// smoke quando o canto tem linha longa pra onde eu vou. Cada uma tem a sua janela (precisa estourar antes de eu
        /// chegar) e o seu custo de guardar (a flash do execute, a molotov do pós-plant, a smoke da entrada).
        /// </summary>
        bool PreNadeEntrando(float t)
        {
            if (t < proxPreNade || path == null || wp >= path.Count) return false;
            proxPreNade = t + 0.5f;
            int nF = me.nades.Count(n => n == "flash"), nM = me.nades.Count(n => n == "molotov"), nH = me.nades.Count(n => n == "he"), nS = me.nades.Count(n => n == "smoke");
            if (nF + nM + nH + nS == 0) return false;
            var it = m.intel[(int)me.team]; var inim = me.IsT ? Team.CT : Team.T; int eles = m.Alive(inim);
            if (eles == 0) return false;
            float spd = MathF.Max(60, me.MaxSpeed * 0.85f), util = 0.6f + 0.6f * me.skill.utilitaria;
            bool antesDoPlant = me.IsT && !m.bombPlanted;
            var cands = new List<(string nome, V2 p)>();
            foreach (var hd in HOLDS_CT) cands.Add((hd.name, hd.pos));   /* os cantos onde se segura: o TR entrando acha o CT neles; o CT retomando acha o TR do pós-plant neles */
            if (focus is V2 fo) cands.Add((focusName ?? "canto", fo));
            float melhorG = 0; string aNome = null, aK = null, aPq = null, aChave = null; V2 aP = default, aJoga = default;
            foreach (var (nome, sp0) in cands)
            {
                V2 sp = sp0; string chave = "prenade " + (int)(sp0.x / 40) + "_" + (int)(sp0.y / 40);
                if (m.utilUsada.Contains(chave)) continue;
                if (!me.IsT && (sp - m.bombPos).Len > 520) continue;
                {   /* bx: o canto é uma região — quem segura fica um pouco pro lado do ponto: miro onde a crença diz (agora + costume) */
                    V2 c0 = sp; float bq = it.Esperados(sp, 45) + 0.5f * Rotas.Perto(inim, sp, m.t, 45);
                    for (int k = 0; k < 8; k++)
                    {
                        V2 q = NavGrid.Snap(c0 + V2.Ang(k * 0.7854f) * 55f); if (!NavGrid.Movable(q)) continue;
                        float vq = it.Esperados(q, 45) + 0.5f * Rotas.Perto(inim, q, m.t, 45); if (vq > bq + 0.02f) { bq = vq; sp = q; }
                    }
                }
                float dh = (sp - me.pos).Len; if (dh < 120 || dh > 560 || AmigoNoAlvo(sp)) continue;
                /* onde no meu caminho o canto me pega primeiro, e quão perto o caminho passa dele */
                float zs = NavGrid.ZAt(NavGrid.Snap(sp)) + 18, anda = 0, andaV = -1, minPath = 1e9f; V2 ant = me.pos, dir = default, pv = default;
                for (int i = wp; i < path.Count && anda < 700; i++)
                {
                    var w = path[i]; anda += (w.p - ant).Len; minPath = MathF.Min(minPath, (w.p - sp).Len);
                    if (andaV < 0 && MapData.LosWalk(w.p, w.z + 18, sp, zs, 18f)) { andaV = anda; dir = w.p - ant; pv = w.p; }
                    ant = w.p;
                }
                if (andaV < 60) continue;   /* não pega meu caminho, ou já estou na frente dele (aí é duelo) */
                float tu = andaV / spd;
                /* crença: tem alguém ali? (agora + costume deles, só onde ainda pode ter alguém) */
                float e = it.Esperados(sp, 110);
                float hab = MathF.Min(1, Rotas.Perto(inim, sp, m.t, 110)) * MathX.Clamp(it.HeatAvg(sp, 70) * 3f, 0, 1);
                float pAli = 1 - MathF.Exp(-(1.6f * e + 0.8f * hab)), duvida = 4f * pAli * (1 - pAli);
                string crenca = e < 0.15f ? "sem info" : e < 0.6f ? "talvez" : "acredito";
                /* quanto o canto custa pra quem entra: me pega de perto (mocadinho) e de lado/de costas */
                float dc = (sp - pv).Len, ang = dir.Len > 1 ? MathF.Abs(MathX.AngDiff(dir.Atan2, (sp - pv).Atan2)) : 0;
                float perigo = MathX.Clamp(1.3f - dc / 350f, 0.25f, 1.1f) + (ang > 75f * MathF.PI / 180 ? 0.3f : 0);
                void Pesa(string k, float voo, float janela, float vale, float guardar, V2 joga, string pq)
                {
                    if (tu < voo || tu > voo + janela) return;   /* fora da hora dela: cedo decide depois, tarde não estoura a tempo */
                    string ck = chave + " " + k; if (rolou.Contains(ck)) return; rolou.Add(ck);   /* uma decisão por canto e tipo */
                    float g = (vale - guardar) * util;
                    if (Match.Auditar) m.Aud("pre-nade: avaliou (tipo · crença · ganho)", k + " · " + crenca + " · " + (g <= 0 ? "a não vale" : g < 0.1f ? "b pouco" : g < 0.25f ? "c médio" : "d muito"));
                    if (g > melhorG) { melhorG = g; aNome = nome; aK = k; aP = sp; aJoga = joga; aPq = pq; aChave = chave; }
                }
                /* flash pré-peek: eu vou passar na cara dele logo depois do estouro; colega perto do estouro fica de fora */
                if (nF > 0)
                {
                    V2 fl = NaCara(sp);
                    if (!m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - fl).Len < 300))
                        Pesa("flash", 0.5f + Match.FUSE_FLASH, 1.3f, 0.9f * pAli * perigo, 0.05f + (nF == 1 && antesDoPlant ? 0.05f : 0), fl, $"flash antes do peek em {nome}: pode ter alguém ali");
                }
                /* HE: testar barato (vale pela dúvida) ou machucar quem segura ali */
                if (nH > 0) Pesa("he", 0.5f + Match.FUSE_HE, 2.5f, 0.6f * pAli * perigo * (e >= 0.6f ? 1.2f : 1f) + 0.25f * perigo * duvida, 0.05f, sp, e < 0.15f ? $"testar {nome} com HE" : $"HE em quem segura {nome}");
                /* molotov: canto fechado perto de onde eu passo (no meu caminho ela me trava) */
                if (nM > 0)
                {
                    float onde = minPath < 70 ? 0.3f : minPath < 160 ? 1.2f : 0.9f;
                    Pesa("molotov", 0.9f + dh / 380f + 0.6f, 3f, 1f * pAli * perigo * onde + 0.25f * perigo * duvida,
                         antesDoPlant ? 0.10f + 0.10f * eles / 5f * (nM > 1 ? 0.4f : 1f) : 0.04f, sp, $"molotov pra limpar {nome}");
                }
                /* smoke: o canto tem linha longa pra onde eu vou — fecho a linha em vez de duelar de longe */
                if (nS > 0 && dc > 280)
                {
                    V2 sm = sp + (pv - sp) / MathF.Max(1, (pv - sp).Len) * 45;
                    Pesa("smoke", 0.9f + dh / 380f + 1f, 3f, 0.8f * pAli * MathX.Clamp((dc - 250) / 300f, 0, 1), antesDoPlant ? 0.12f : 0.06f, sm, $"smoke na linha longa de {nome}");
                }
            }
            if (aK == null || rng.Rand() > MathX.Clamp(melhorG / 0.3f, 0, 0.9f) || !Alcanca(aJoga, aK == "he" ? "he" : aK == "flash" ? "flash" : "molotov")) return false;
            preNadeAgora = true; Jogar(aK, $"{aK} de praxe em {aNome}", aJoga); preNadeAgora = false;
            if (action != "granada") return false;
            m.utilUsada.Add(aChave); m.Ev($"pre-nade {aK}/round"); porquePendente = aPq;
            if (Match.Auditar)
            {   /* auditoria: quem estava lá de verdade (só lê) — o resultado sai 4 s depois do estouro */
                float rr = aK == "molotov" ? 100f : aK == "he" ? 110f : aK == "flash" ? 200f : 160f;
                var real = new List<(Agent, int, V2, float)>(); foreach (var x in m.agents) if (x.alive && x.team != me.team && (x.pos - aP).Len < rr) real.Add((x, x.hp, x.pos, x.blindUntil));
                string info = it.Esperados(aP, 110) < 0.15f ? "sem info" : "com crença";
                preNades.Add(new PreNadeReg { p = aP, t = actionEnd + 1.6f, kind = aK, porque = info, real = real });
                m.Aud("pre-nade: jogado · tinha alguém lá de verdade", aK + " · " + info + " · " + (real.Count == 0 ? "ninguém" : real.Count == 1 ? "1" : "2+"));
            }
            return true;
        }
        /// <summary>bx: resultado do pre-nade por tipo (auditoria, só lê): cegou, matou, tirou da posição, machucou ou ficou.</summary>
        public void PreNadeAud()
        {
            for (int i = preNades.Count - 1; i >= 0; i--)
            {
                var r = preNades[i]; if (m.t < r.t + 4f) continue;
                preNades.RemoveAt(i);
                if (r.real.Count == 0) { m.Aud("pre-nade: resultado", r.kind + " · ninguém lá"); continue; }
                foreach (var (e, hp, pos, cego0) in r.real)
                {
                    string ef = !e.alive ? "morreu depois" : (e.pos - pos).Len > 80 ? "saiu da posição" : e.hp < hp ? "machucou, ficou" : "ficou inteiro";
                    if (r.kind == "flash") ef = (e.blindUntil > cego0 + 0.3f && e.flashPor == me ? "cegou" : "não cegou") + " · " + ef;
                    m.Aud("pre-nade: resultado", r.kind + " · " + ef);
                }
            }
        }

        bool UtilEntrando(float t)
        {
            if (action != null) return false;
            if (PreNade && PreNadeEntrando(t)) return true;
            // smoke na cara de quem costuma segurar a minha entrada (posição típica do inimigo, quente, que vê o meu caminho)
            if (!PreNade && me.nades.Contains("smoke") && path != null && wp < path.Count && t >= proxSmokeHold)   /* bx: com o pre-nade, a smoke na linha é pesada lá */
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
            if (!PreNade && me.nades.Contains("molotov") && focusName == "cantinho" && rng.Rand() < 0.25f && MapData.LosWalk(me.pos, me.z + 6, f, NavGrid.ZAt(f), 18f))   /* bx: com o pre-nade, o canto é pesado lá */
            { Jogar("molotov", $"molotov no cantinho ({Region(f)}) pra limpar", f); return true; }
            if (rng.Rand() > 0.35f + 0.3f * agr) return false;
            var alvo = NaCara(f);
            if (!PreNade && me.nades.Contains("flash") && (me.role != Role.Entry || rng.Rand() < 0.5f)) { Jogar("flash", $"flash no {focusName} ({Region(f)}) antes de entrar", alvo); return true; }
            if (AliadoComFlash(320) != null && (EXP.Contains("utilAToaVelho") || m.Alive(me.IsT ? Team.CT : Team.T) > 0 && m.intel[(int)me.team].Esperados(f, 350) >= 0.08f)) { m.pedidos.Add(new Match.PedidoBang { quem = me, alvo = alvo, t = t }); me.pediuBang++; Think($"me banga o {focusName} ({Region(f)})!"); }
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
        // shift ou correndo é decisão de som (quem pode ouvir daqui a pouco): vale por 0,25 s — recalcular a cada tique
        // custava duas estimativas de inimigos por jogador andando
        bool Silencio(Order o)
        {
            if (o == silOrdem && m.t - silAt < 0.25f && !EXP.Contains("silencioTique")) return silRes;
            long pc = P0(); silRes = SilencioX(o); silAt = m.t; silOrdem = o; P1("silêncio (andar de shift?)", pc); return silRes;
        }
        bool silRes; float silAt = -9; Order silOrdem;
        bool SilencioX(Order o)
        {
            if (EXP.Contains("semShift") || o.push && !o.calmo || o.defuse && (!o.calmo || EXP.Contains("defuseCorre")) || me.faca) return false;
            // sem tempo: corre. No retake quem manda é o tempo do defuse, não um número fixo
            if (m.RoundLeft < 25 && !m.bombPlanted || m.bombPlanted && !me.IsT && m.BombLeft < NavGrid.PathLen(me.pos, m.bombPos) / MapCheck.RUN + 10 * (EXP.Contains("retakeVelho") ? 2 : 1)) return false;
            if (me.panico > 0.6f) return false;   // em pânico esquece de andar no shift
            var it = m.intel[(int)me.team];
            bool shift;
            if (EXP.Contains("shiftVelho"))
            {
                float perto = it.HeatAvg(me.pos, 300);
                if (path != null && wp < path.Count) perto = MathF.Max(perto, it.HeatAvg(path[Math.Min(path.Count - 1, wp + 2)].p, 300));
                shift = perto > 0.03f * (1.6f - me.skill.composure);
            }
            else
            {   // consciência do som: passo correndo se ouve a ~260 (escala do mapa). Shift só faz sentido se alguém deles
                // pode estar a esse alcance agora ou daqui a pouco (eles também andam): quantos devem estar perto de mim e
                // do ponto do caminho uns 2 s à frente. Longe de qualquer um, corre (o pro corre até a zona de contato)
                const float OUVE = 260 + 40;
                float e = it.Esperados(me.pos, OUVE);
                if (path != null && wp < path.Count) e = MathF.Max(e, it.Esperados(path[Math.Min(path.Count - 1, wp + 3)].p, OUVE));
                foreach (var c in it.contacts.Values) if (m.t - c.t < 6 && m.agents[c.id].alive && (c.pos - me.pos).Len < OUVE + 150) e = MathF.Max(e, 1);
                float lim = ShiftLim();
                shift = e > lim * (1.5f - me.skill.composure);
            }
            if (Match.Auditar && m.t - audShiftT > 0.5f)
            {
                audShiftT = m.t; float dr = 9999;
                foreach (var x in m.agents) if (x.alive && x.team != me.team) dr = MathF.Min(dr, (x.pos - me.pos).Len);
                m.Aud("andando: shift ou correndo x inimigo real mais perto", (shift ? "shift" : "corre") + (dr < 350 ? " · <350 (ouviria)" : dr < 700 ? " · 350-700" : " · 700+ (ninguém ouve)"));
            }
            return shift;
        }
        float audShiftT = -9;
        // parâmetro de experimento lido uma vez por texto de EXP (era uma expressão regular por tique por jogador andando)
        static float ShiftLim() => TeamBrain.ExpNum("shiftLim", 0.5f);

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

        float esperaCruzar = -9, proxSmokeHold, semAndarDesde, desenroscaAte = -9; V2 destravaPos;
        bool Parar(string porque) { wantVel = V2.Zero; Think(porque); return true; }

        /// <summary>
        /// Caminho entra numa smoke do inimigo: atravessar (pegar de surpresa — arriscado: pode ter gente segurando a
        /// saída) ou esperar ela abrir. Decide uma vez por smoke; esperando, reavalia quando o tempo aperta.
        /// </summary>
        bool SmokeNoCaminho(float t)
        {
            if (path == null || wp >= path.Count) return false;
            var s = m.SmokeInimiga(path[wp].p, me.team) ?? m.SmokeInimiga(me.pos + (path[wp].p - me.pos) * 0.5f, me.team);
            if (SmokeEspera) s ??= SmokeAFrente();
            if (s == null) return false;
            string chave = "atravessar" + (int)s.origin.x + "_" + (int)s.origin.y;
            if (!decisaoSmoke.TryGetValue(chave, out bool atravessa))
            {
                bool urgente = order.push && m.RoundLeft < 30 || order.defuse || order.plant && me.hasBomb && m.RoundLeft < 25;
                if (!EXP.Contains("bxSmokeCrencaVelho")) atravessa = AtravessaPelaCrenca(s, urgente);
                else atravessa = rng.Rand() < 0.2f + 0.5f * P.Agr(me.skill.aggression) + (urgente ? 0.35f : 0);
                decisaoSmoke[chave] = atravessa;
                Think(atravessa ? "atravessando a smoke pra pegar de surpresa" : "não vou atravessar a smoke — espero ela abrir");
                if (Match.Auditar) m.Aud("smoke no caminho: decisão", (me.IsT ? "TR · " : "CT · ") + (urgente ? "urgente · " : "") + (atravessa ? "atravessa" : "espera"));
                esperaDesde = t;
            }
            if (atravessa) return false;
            if (t - esperaDesde > 7) { decisaoSmoke.Remove(chave); return false; }   // cansou de esperar: decide de novo
            Esperando(null); fase = "esperando a smoke abrir";
            if (SmokeEspera)
            {   /* bx: esperar é ficar FORA: o MoveTo deste tique já tinha posto o passo pra dentro da smoke (antes o bot
                   "esperava" andando pra dentro). Se a borda já me pega, um passo pra trás (pra longe do centro dela). */
                wantVel = V2.Zero;
                V2 o = new V2(s.origin.x, s.origin.y), fora = me.pos - o; float fl = fora.Len;
                if (fl > 1 && (m.SmokeInimiga(me.pos, me.team) != null || m.SmokeInimiga(me.pos - fora / fl * 14f, me.team) != null))
                {
                    V2 v = fora / fl * me.MaxSpeed * 0.52f;
                    if (NavGrid.Movable(me.pos + v * 0.3f)) wantVel = v;
                }
            }
            return true;
        }
        /* bx: padrão desde o A/B fatorial bxf (2026-11-06); -exp bxSmokeEsperaVelho volta: esperar a smoke parado do lado de fora, e enxergar a smoke no
           caminho até ~60 à frente (o ponto do caminho pode estar depois dela) */
        public static bool SmokeEspera => !EXP.Contains("bxSmokeEsperaVelho");
        CSM.View.SmokeVolume SmokeAFrente()
        {
            V2 p = me.pos; float andou = 0;
            for (int i = wp; i < path.Count && andou < 60; i++)
            {
                V2 q = path[i].p, d = q - p; float L = d.Len;
                for (float k = 10; k <= L && andou + k <= 60; k += 10) { var s = m.SmokeInimiga(p + d * (k / L), me.team); if (s != null) return s; }
                andou += L; p = q;
            }
            return null;
        }
        readonly Dictionary<string, bool> decisaoSmoke = new Dictionary<string, bool>(); float esperaDesde;
        public bool? DecidiuSmoke(CSM.View.SmokeVolume s) => decisaoSmoke.TryGetValue("atravessar" + (int)s.origin.x + "_" + (int)s.origin.y, out bool v) ? v : (bool?)null;   /* bx: auditoria */

        /// <summary>
        /// bx: atravessar a smoke do inimigo é decisão pelo risco que EU acredito. Quem segura a saída tem a vantagem (parado,
        /// pré-mirado na borda, vê quem sai primeiro); eu saio andando sem saber onde ele está. Pesa: inimigos esperados
        /// do outro lado (crença + passo ouvido ali), colega junto pra trocar, pressa (tempo, bomb, defuse) e quanto falta
        /// pra smoke acabar (perto do fim, esperar é barato). Sem ninguém esperado do outro lado, atravessar é só andar.
        /// </summary>
        bool AtravessaPelaCrenca(CSM.View.SmokeVolume s, bool urgente)
        {
            V2 saida = path[path.Count - 1].p; bool dentro = false;
            for (int k = wp; k < path.Count && k < wp + 40; k++)
            {
                bool sm = m.SmokeInimiga(path[k].p, me.team) == s;
                if (sm) dentro = true; else if (dentro) { saida = path[k].p; break; }
            }
            var it = m.intel[(int)me.team];
            float esp = it.Esperados(saida, 300); int passos = 0;
            foreach (var h in it.heard) if (m.t - h.t < 5 && h.kind == "passos" && (h.p - saida).Len < 350) passos++;
            float risco = MathF.Min(2.5f, esp + 0.4f * passos);
            int juntos = 0; foreach (var x in m.agents) if (x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 160) juntos++;
            float resta = CSM.View.SmokeVolume.LIFE - s.age;
            float pressa = urgente ? 1f : 0f;
            if (me.IsT && !m.bombPlanted && m.RoundLeft < 40) pressa = MathF.Max(pressa, 0.5f);
            if (!me.IsT && m.bombPlanted) pressa = MathF.Max(pressa, m.BombLeft < resta + Match.DefuseTime(me) + 3 ? 1f : 0.5f);   /* retake: esperar a smoke custa o bomb */
            float v = (0.35f + 0.4f * P.Agr(me.skill.aggression)) * MathF.Exp(-1.4f * risco) * (1 + 0.25f * MathF.Min(juntos, 2))
                    + 0.6f * pressa * MathF.Exp(-0.4f * risco) - (resta < 5 && pressa < 1 ? 0.15f : 0);
            v = Math.Clamp(v, 0.03f, 0.95f);
            bool vai = rng.Rand() < v;
            if (Match.Auditar)
            {   /* bx: calibração — a crença contra o que estava lá de verdade (só lê) */
                int real = 0; foreach (var x in m.agents) if (x.alive && x.team != me.team && (x.pos - saida).Len < 300) real++;
                m.Aud("smoke no caminho: crença x real (inimigos a 300 da saída)", (risco < 0.2f ? "a <0,2" : risco < 0.6f ? "b 0,2-0,6" : risco < 1.2f ? "c 0,6-1,2" : "d 1,2+") + " · real " + Math.Min(real, 2) + (real >= 2 ? "+" : ""));
            }
            if (Match.Auditar) m.Aud("smoke no caminho: risco que acredito", (risco < 0.2f ? "a ninguém (<0,2)" : risco < 0.6f ? "b talvez (0,2-0,6)" : risco < 1.2f ? "c provável (0,6-1,2)" : "d certo (1,2+)") + " · " + (vai ? "atravessa" : "espera"));
            Think(vai ? $"atravesso a smoke: {(risco < 0.2f ? "acho que não tem ninguém do outro lado" : pressa >= 1 ? "não dá tempo de esperar" : "vale o risco")} (risco {risco:0.0}, {juntos} comigo)"
                      : $"não atravesso: deve ter {risco:0.0} segurando a saída — quem sai da smoke perde o duelo");
            return vai;
        }

        /// <summary>
        /// bx: meu ângulo está smokado e eu ACREDITO que vem gente por ela (esperados atrás + passo ouvido): seguro a SAÍDA
        /// — a borda da fumaça do meu lado na linha do ângulo. Quem sai da smoke aparece ali, andando e sem me ver ainda; eu
        /// já estou parado e mirado. Sem crença, a smoke só tapa o ângulo e eu olho outro (o de antes).
        /// </summary>
        V2? SaidaSmoke(V2 L)
        {
            if ((L - saidaDe).Len < 20 && m.t - saidaT < 0.5f) return saidaRes;
            saidaDe = L; saidaT = m.t; saidaRes = null;
            var it = m.intel[(int)me.team];
            float crenca = it.Esperados(L, 250); int passos = 0;
            foreach (var h in it.heard) if (m.t - h.t < 5 && h.kind == "passos" && (h.p - L).Len < 300) passos++;
            crenca += 0.4f * passos;
            if (crenca < 0.3f) return null;
            V2 d = L - me.pos; float dl = d.Len; if (dl < 60) return null; d /= dl;
            V2 antes = me.pos;
            for (float r = 24; r < dl; r += 8)
            {
                V2 q = me.pos + d * r;
                if (!NavGrid.Movable(q)) { antes = q; continue; }
                if (m.SmokeDensaEm(q, NavGrid.ZAt(q) + 18)) { saidaRes = (antes - me.pos).Len > 30 ? antes : (V2?)null; break; }
                antes = q;
            }
            if (saidaRes != null && Match.Auditar) m.Aud("smoke no ângulo: segurei a saída", (me.IsT ? "TR" : "CT") + (crenca >= 1 ? " · crença forte" : " · crença média"));
            return saidaRes;
        }
        V2 saidaDe; float saidaT = -9; V2? saidaRes;

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
            if (goal.Len < 30 && Match.Auditar) { var st = new System.Diagnostics.StackTrace(1, true); int ln1 = st.GetFrame(0)?.GetFileLineNumber() ?? 0; var f1 = st.GetFrame(0)?.GetMethod(); var f2 = st.GetFrame(1)?.GetMethod(); var f3 = st.GetFrame(2)?.GetMethod(); m.Aud("destino (0,0): quem chamou", $"{f1?.Name}:{ln1} ← {f2?.Name} ← {f3?.Name} · {(me.IsT ? "TR" : "CT")} · hold {(order?.hold != null ? $"({order.hold.pos.x:0},{order.hold.pos.y:0})" : "-")} · fase {fase} · micro {micro} · ordem {order?.name}"); m.Log($"DEST00 {me.name} goal ({goal.x:0.0},{goal.y:0.0}) {f1?.Name}<{f2?.Name}<{f3?.Name} fase {fase} micro {micro} hold {order?.hold?.name} dest ({order?.dest.x:0},{order?.dest.y:0})"); }
            goal = NavGrid.AlvoAlcancavel(me.pos, goal);   /* ponto em cima de pilha (só de pezinho) e eu no chão: o chão do lado */
            // perto do inimigo anda de shift em qualquer deslocamento — corre só no que é urgente (duelo, troca, ajudar,
            // fogo, bomb pra explodir) ou quando a ordem tem pressa (Silencio diz)
            if (run && target == null && fase != "indo trocar" && fase != "ajudando no duelo" && fase != "saindo do fogo" && fase != "saindo do bomb"
                && fase != "round ganho" && Silencio(order)) run = false;
            // recalcula se o destino mudou, se saiu do caminho (empurrado, desviou) ou de vez em quando
            bool fora = path != null && wp < path.Count && (path[Math.Max(0, wp - 1)].p - me.pos).Len > 60 && (path[wp].p - me.pos).Len > 60;
            // o recálculo de rotina (5 s) espera um passo com orçamento de A* (o caminho de agora segue valendo): todo mundo
            // recalculando no mesmo tique era pico
            bool rotina = m.t - pathAt > 5f && !(NavGrid.Estourou && path != null && path.Count >= 2 && wp < path.Count && !EXP.Contains("orcAVelho"));
            if (path == null || (goal - pathGoal).Len > 8 || fora || rotina)
            {
                long pc = P0();
                path = NavGrid.Path(me.pos, goal, Perigo.Camada(me.team)); wp = 1; pathGoal = goal; pathAt = m.t;   // evita cruzar na mira
                // destino fora da navmesh (em cima de borda/caixa): vai pro ponto andável mais perto dele
                if ((path == null || path.Count < 2) && (goal - me.pos).Len > 12) { path = NavGrid.Path(me.pos, NavGrid.Snap(goal), Perigo.Camada(me.team)); if (path.Count < 2) path = NavGrid.Path(me.pos, NavGrid.Snap(goal)); }
                P1("caminho (Path)", pc);
            }
            if ((path == null || path.Count < 2) && (goal - me.pos).Len > 12 && Match.Auditar && m.t - audSemCam > 1f)
            {   audSemCam = m.t;   /* auditoria: onde está e pra onde quer ir quando não acha caminho */
                var (cx, cy) = NavGrid.CellOf(me.pos); bool soB = cx >= 0 && cy >= 0 && cx < NavGrid.NGX && cy < NavGrid.NGY && NavGrid.soBoost[cx, cy];
                m.Aud("sem caminho: de onde · pezinho", Match.PontoBoost(me.pos) + $" z+{me.z - MapData.GZ:0}" + (soB ? " (topo de pezinho)" : "") + $" · estado {boostEstado}" + (me.emCimaDe != null ? " · em cima de alguém" : ""));
                m.Aud("sem caminho: pra onde", Match.PontoBoost(goal) + " · " + fase + (goal.Len < 30 ? $" · de {Match.PontoBoost(me.pos)} z+{me.z - MapData.GZ:0} base {(baseP.HasValue ? Match.PontoBoost(baseP.Value) : "-")} trilha {trilha.Count} estado {boostEstado}" : ""));
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
            // (pelo quanto saiu do lugar, não pela velocidade: raspando na quina a velocidade não zera mas ele não sai dali)
            // (preso = pediu pra andar o dobro disso e não saiu: andando devagarinho fatiando a quina não é estar preso —
            // antes isso disparava o "destrava" com passo de lado pra dentro da parede)
            if ((me.pos - destravaPos).Len > 8 || (goal - me.pos).Len < 14) { destravaPos = me.pos; semAndarDesde = m.t; me.pedidoAcum = 0; }
            else if (m.t - me.travadoUlt < 0.3f && m.t - me.travadoDesde < 2.5f && me.travadoPor != null && me.travadoPor.alive && !EXP.Contains("travaCorpoVelha"))
            {   // o que trava é um corpo (colega/inimigo), não o mapa: o caminho está certo — escorrego/ele dá passagem
                semAndarDesde = m.t; me.pedidoAcum = 0;
            }
            else if (m.t - semAndarDesde > 0.8f && m.t - pathAt > 0.8f && (me.pedidoAcum > 16 || EXP.Contains("travaVelha")))
            {
                pathAt = -9; semAndarDesde = m.t; me.pedidoAcum = 0; desenroscaAte = m.t + 0.35f; m.Ev("destravou (preso andando)/round");
                if (Match.Auditar)
                {   /* onde trava: em cima de caixa, colado numa caixa alta, ou outro */
                    float acima = me.z - MapData.floor[Math.Clamp((int)me.pos.x, 0, MapData.NX - 1), Math.Clamp((int)me.pos.y, 0, MapData.NY - 1)];
                    bool colado = false; for (int dx = -6; dx <= 6 && !colado; dx += 2) for (int dy = -6; dy <= 6 && !colado; dy += 2) { int ux = (int)me.pos.x + dx, uy = (int)me.pos.y + dy; if (ux >= 0 && uy >= 0 && ux < MapData.NX && uy < MapData.NY && MapData.coverH[ux, uy] >= 28) colado = true; }
                    m.Aud("destravou: lugar", Match.PontoBoost(me.pos));
                    if (path != null && wp < path.Count) { float dzw = path[wp].z - me.z; m.Aud("destravou: próximo ponto", path[wp].link + (dzw > 4 ? " sobe " + (dzw < 14 ? "<14" : dzw < 19 ? "14-19" : "19+") : dzw < -4 ? " desce" : " plano") + $" · a {(path[wp].p - me.pos).Len:0}" + (me.noAr ? " · no ar" : "")); }
                    m.Aud("destravou: onde", (acima > 10 ? "em cima de caixa (z " + (acima < 20 ? "~16" : "28+") + ")" : colado ? "no chão, colado em caixa alta" : "outro") + (me.noAr ? " · no ar" : ""));
                }
                if (Match.Auditar) m.Aud("destravou: fase / ordem", fase + " / " + (order.name.StartsWith("plano:") ? "plano" : order.name.Length > 24 ? order.name.Substring(0, 24) : order.name) + (me.Speed < 3 ? " (parado)" : " (andando)"));
                if (Match.Auditar && order.name.StartsWith("plano:")) m.Aud("destravou no plano: onde → pra onde", $"{Region(me.pos)} → {Region(goal)}");
            }
            if (!EXP.Contains("apQuedaVelho") && target == null && path[wp].z < me.z - 10 && (path[wp].p - me.pos).Len < 30)
            {   /* vai descer (sacada/caixa): colega bem embaixo de onde vou cair? espero ele passar (cair na cabeça do amigo é raro no pro) */
                foreach (var x in m.agents)
                    if (x != me && x.alive && x.team == me.team && x.z < me.z - 10 && (x.pos - path[wp].p).Len < 10) { wantVel = V2.Zero; fase = "esperando o colega sair de baixo"; return; }
            }
            if (path[wp].link == NavGrid.Link.Salto && wp > 0 && (path[wp - 1].p - me.pos).Len < 4 && !me.noAr)
            {   /* salto por cima do vão: da beirada, pula pro outro lado (a física dá o embalo pra chegar) */
                V2 ds = path[wp].p - me.pos; float ls = ds.Len;
                if (ls > 0.5f) { wantVel = ds / ls * me.MaxSpeed; me.move = Move.Correndo; }
                me.querPular = true; me.saltandoAte = m.t + 1.2f; me.saltoAlvo = path[wp].p; me.saltoZ = path[wp].z; m.Ev("salto por cima do vão/round");
                return;
            }
            V2 to = path[wp].p - me.pos; float l = to.Len;

            // ponto de queda/pulo logo abaixo/acima (no chão já cheguei, a altura não): segue na direção do trecho
            // seguinte — pisa pra fora da beira (cai) ou empurra a caixa (pula). Antes ficava parado na beira
            float pertoDegrau = NavGrid.BORDA && (path[wp].link == NavGrid.Link.Jump || path[wp].link == NavGrid.Link.Drop) ? 6.5f : 4f;   /* a folga da parede segura o corpo a 4 do degrau: com < 4 nunca pulava */
            if (l < pertoDegrau && MathF.Abs(path[wp].z - me.z) >= 5 && !me.noAr)
            {
                if (NavGrid.BORDA) me.subindoDegrauAte = m.t + 0.5f;   /* passando o degrau: a física deixa pisar na folga entre o chão e a caixa */
                if (l > 0.3f) { to = to / l * 6; l = 6; }   // vai direto no ponto (passo firme, não para na beira)
                else
                {
                    V2 ref_ = wp + 1 < path.Count ? path[wp + 1].p : path[wp].p;
                    V2 seg = ref_ - (wp > 0 ? path[wp - 1].p : me.pos);
                    if (seg.Len > 0.5f) { to = seg / seg.Len * 6; l = 6; }
                }
                // degrau acima e perto: pula já (escadinha de caixas: andar até encostar fazia cair pela borda)
                float dz = path[wp].z - me.z;
                if (dz > 4 && dz <= NavGrid.JUMP_MAX + 0.5f && path[wp].link != NavGrid.Link.Salto) { me.pularPara = path[wp].z; me.subindoDegrauAte = m.t + 0.4f; }
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
            if (m.FireAt(path[wp].p) && !m.NoFogo(me)) { fase = "esperando a molotov"; Think("esperando a molotov apagar"); return; }
            float spd = me.MaxSpeed * (run ? 1f : 0.52f);
            if (l < 10 && wp == path.Count - 1) spd *= l / 10;            wantVel = to / l * spd;
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
                        (EXP.Contains("atiraNoAmigo") || !AmigoNaLinha(s.p, s.z + (Combat.HitboxTela ? Combat.CabecaZ(0) : 21.5f), dl, out _)) &&   // pre-fire com colega na frente, não
                        (porMemoria || (!EXP.Contains("prefireCrenca") ? intel.HeatAt(s.p) > 0.55f && (me.role == Role.Entry || me.skill.aggression > 0.65f) : PrefireCrenca(s.p))))   /* crença: opt-in — 2 pre-fires/round em canto vazio, retake −8 */
                    {
                        prefired.Add(key);
                        if (porMemoria) me.prefireMemoria++;
                        Think(porMemoria ? $"pre-fire no pixel de onde mataram a gente ({Region(s.p)})" : $"pre-fire no {(s.kind == Spots.Kind.Canto ? "cantinho" : "atrás da caixa")} ({Region(s.p)})");
                        for (int k = 0; k < 2; k++) m.Shoot(me, s.p, s.z + (Combat.HitboxTela ? Combat.CabecaZ(0) : 21.5f));
                        m.Ev("pre-fire/round");
                        if (Match.Auditar)
                        {   // validador: tinha alguém mesmo no canto? (verdade do jogo, só pra medir — não muda nada)
                            bool tinha = false; foreach (var x in m.agents) if (x.alive && x.team != me.team && (x.pos - s.p).Len < 35) tinha = true;
                            m.Aud("pre-fire: tinha alguém no canto?", (tinha ? "sim" : "não") + (porMemoria ? " · memória" : " · calor") + $" · {me.role}");
                        }
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
            if (call != null) { Borda(); V2 pc2 = ForaDaParede(Pixel(call.Value)); Reservar(call.Value, pc2); Face(pc2, 3); AbrirPixel(pc2); }
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
                    if (!calor && q is V2 fq && !EXP.Contains("paredeVelha") && (fq - me.pos).Len < 70) q = Aberto(fq);
                }
                if (q != null) { V2 q0 = q.Value; q = PorOmbro(q.Value); if ((q.Value - q0).Len > 1) miraFonte += "+ombro"; q = ForaDaParede(q.Value); Face(q.Value, 3); Reservar(q.Value); }
                AbrirPixel(q);
            }
            fonteMira = miraFonte;
            if (Match.Auditar && m.t - audParedeT > 0.25f && wantVel.Len > 1)
            {   // andando olhando pra parede: a mira bate numa parede a menos de 40 (não é quina: é parede chapada na cara)
                audParedeT = m.t;
                float raio = MapData.Raio(me.pos, me.z, V2.Ang(me.aim), 450, 18f);
                bool parede = raio < 40;
                float costas = MathF.Abs(MathX.AngDiff(wantAim, wantVel.Atan2)) * 180 / MathF.PI;
                if (wantVel.Len > me.MaxSpeed * 0.3f)
                {
                    // o da frente: ninguém do meu time mais perto do que eu de onde devem estar eles (na direção em que ando)
                    V2 alvoC = me.pos + wantVel / wantVel.Len * 300;
                    bool frente = !m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - alvoC).Len < (me.pos - alvoC).Len - 30);
                    m.Aud("andando: mira pra trás (>100° de onde vou)", costas > 100 ? $"sim · {miraFonte} · {pfTrasDiag} · picoCostume {(bordaQuina == null)}" : "não");
                    {   // o caso que o usuário viu: AVANÇANDO pro lado deles (me afastando da minha base) e mirando pro MEU lado
                        V2 casa = (me.IsT ? MapData.SPAWN_T[2] : MapData.SPAWN_CT[2]) - me.pos; float dc = casa.Len;
                        if (dc > 300 && wantVel.Dot(casa) < -0.5f * wantVel.Len * dc)
                        {
                            float praCasa = MathF.Abs(MathX.AngDiff(wantAim, casa.Atan2)) * 180 / MathF.PI;
                            m.Aud("avançando pro lado deles: mira pro meu lado", praCasa < 60 ? $"sim · {miraFonte}" : "não");
                            if (praCasa < 60 && ((int)(m.t / Match.DT) * 7919 + me.id * 104729) % 60 == 0)
                            {
                                var itE = m.intel[(int)me.team]; V2 olho = me.pos + V2.Ang(wantAim) * 200;
                                string ult = string.Join("; ", itE.contacts.Values.Where(c => m.agents[c.id].alive).Select(c => $"{m.agents[c.id].name} em {Region(c.pos)} há {m.t - c.t:0}s"));
                                Match.GuardaExemplo("avançando e olhando pro próprio lado", m.seedRound, m.t, $"{me.name}({(me.IsT ? "TR" : "CT")}) em {Region(me.pos)} indo pra {Region(me.pos + wantVel / wantVel.Len * 200)} ({order.name}) mira em {Region(olho)} [{miraFonte}] esperados lá {itE.Esperados(olho, 150):0.00} · na frente {itE.Esperados(me.pos + wantVel / wantVel.Len * 250, 200):0.00} | info: {ult}");
                            }
                        }
                    }
                    if (costas > 100 && miraFonte.StartsWith("borda") && bordaQuina != null)
                    {   // RAIZ: por que a quina de trás ganhou? quanto de crença (esperados) e de costume (rotas pro) ela tinha x a frente
                        var itA = m.intel[(int)me.team];
                        float eT = 0, rT = 0; foreach (var a2 in bordaQuina.atras) { eT = MathF.Max(eT, itA.Esperados(a2, 110)); rT = MathF.Max(rT, Rotas.Perto(me.IsT ? Team.CT : Team.T, a2, m.t, 110)); }
                        float eF = 0; V2 fwA = wantVel / wantVel.Len;
                        foreach (var q2 in Pixels.Em(me.pos)) { V2 dq = q2.ponto - me.pos; if (dq.Dot(fwA) <= 0.35f * dq.Len) continue; foreach (var a2 in q2.atras) eF = MathF.Max(eF, itA.Esperados(a2, 110)); }
                        string fx(float v) => v < 0.05f ? "~0" : v < 0.2f ? "baixa" : v < 0.6f ? "média" : "alta";
                        m.Aud("olhou pra trás: crença atrás x frente", $"atrás {fx(eT)} · frente {fx(eF)} · costume atrás {(rT < 0.005f ? "~0" : rT < 0.03f ? "baixo" : "alto")}");
                        if (eT >= 0.2f && ((int)(m.t / Match.DT) * 7919 + me.id * 104729) % 40 == 0)
                        {   // exemplo concreto: de onde vem a crença de trás (último visto/ouvido de cada inimigo vivo)
                            string ult = string.Join("; ", itA.contacts.Values.Where(c => m.agents[c.id].alive).Select(c => $"{m.agents[c.id].name} {(c.seen ? "visto" : "ouvido")} em {Region(c.pos)} há {m.t - c.t:0}s"));
                            V2 atr = bordaQuina.atras.Length > 0 ? bordaQuina.atras[0] : bordaQuina.ponto;
                            Match.GuardaExemplo("olhou pra trás com crença", m.seedRound, m.t, $"{me.name}({(me.IsT ? "TR" : "CT")}) em {Region(me.pos)} indo pra {Region(me.pos + fwA * 200)} ({order.name}) olhou {Region(atr)} (esperados {eT:0.00}) | vivos info: {ult}");
                        }
                    }
                }
                m.Aud("andando: mira bate na parede a <40", parede ? "sim · " + miraFonte : "não");
                if (parede)
                {
                    float rq = MapData.Raio(me.pos, me.z, V2.Ang(wantAim), 450, 18f);
                    m.Aud("parede: a mira que ele QUER também bate?", (rq < 40 ? "quer olhar a parede · " : "só a mão atrasada · ") + miraFonte + (miraFonte.StartsWith("borda") && bordaQuina != null ? (bordaQuina.dp < 40 ? " (quina colada <40)" : " (quina longe)") : ""));
                }
                if (parede && ((int)(m.t / Match.DT) * 7919 + me.id * 104729) % 50 == 0) Match.GuardaExemplo("andando olhando pra parede", m.seedRound, m.t, $"{me.name} em {Region(me.pos)} ({miraFonte}, {fase}, {order.name}) mira bate a {raio:0}");
            }
        }
        /// <summary>
        /// Pre-fire como pro: atira no canto quando ACHA que tem gente lá (crença: esperados, ou passo ouvido perto há pouco),
        /// não só porque poderia ter (o calor "possível" dava 88% de canto vazio — e tiro à toa entrega a posição). Quem é
        /// mais agressivo pre-atira com menos certeza; o entry um pouco mais.
        /// </summary>
        bool PrefireCrenca(V2 p)
        {
            var it = m.intel[(int)me.team];
            float e = it.Esperados(p, 90);
            bool passo = false;
            for (int i = it.heard.Count - 1; i >= 0 && m.t - it.heard[i].t < 1.5f; i--) if ((it.heard[i].p - p).Len < 80) { passo = true; break; }
            float limiar = 0.8f - 0.2f * P.Agr(me.skill.aggression) - (me.role == Role.Entry ? 0.1f : 0);   /* crença forte: 0,45 dava 3,8 pre-fires/round quase todos em canto vazio */
            return passo || e > limiar;
        }
        float audParedeT = -9; string pfTrasDiag = "";
        /// <summary>A mira que eu quero bate numa parede colada (andando rente à parede, quina na mesma parede mais à frente,
        /// call atrás do muro): gira pro lado aberto mais próximo (até 40°) — ninguém anda com a mira chapada na parede.</summary>
        V2 ForaDaParede(V2 q)
        {
            if (EXP.Contains("paredeVelha")) return q;
            float a0 = (q - me.pos).Atan2, L = MathF.Max(60, (q - me.pos).Len);
            if (MapData.Raio(me.pos, me.z, V2.Ang(a0), 450, 18f) >= 40) return q;
            for (int k = 1; k <= 8; k++)
                foreach (int s in new[] { 1, -1 })
                {
                    float a = a0 + s * k * 0.0873f;   // passos de 5°
                    if (MapData.Raio(me.pos, me.z, V2.Ang(a), 450, 18f) >= 60) return me.pos + V2.Ang(a) * L;
                }
            return Aberto(q);   // corredor fechado: olha pra onde ele abre, perto da direção em que ando
        }
        /// <summary>
        /// Sem ameaça e o ponto do caminho que eu vejo está colado (curva fechada de corredor): ninguém anda olhando a
        /// parede da frente — olha pra onde está aberto, perto da direção em que anda (o corredor), a uns 150 adiante.
        /// </summary>
        V2 Aberto(V2 perto)
        {
            float aBase = wantVel.Len > 1 ? wantVel.Atan2 : (perto - me.pos).Atan2;
            float melhor = -1, aM = aBase;
            for (int k = -6; k <= 6; k++)
            {
                float a = aBase + k * 0.17f;   // ±60° em passos de ~10°
                float r = MapData.Raio(me.pos, me.z, V2.Ang(a), 450, 18f);
                float sc = MathF.Min(r, 220) - 1.2f * MathF.Abs(k) * 10;   // aberto vale, mas longe da direção em que ando custa
                if (sc > melhor) { melhor = sc; aM = a; }
            }
            return me.pos + V2.Ang(aM) * 150;
        }

        /// <summary>Colega na linha da mira (andando atrás dele): quem segue não mira nas costas dele — mira passando
        /// pelo ombro, pro lado que ele não cobre.</summary>
        V2 PorOmbro(V2 q)
        {
            if (EXP.Contains("miraNasCostas")) return q;
            V2 d = q - me.pos; float L = d.Len; if (L < 1) return q;
            V2 u = d / L;
            foreach (var x in m.agents)
            {
                if (x == me || !x.alive || x.team != me.team) continue;
                V2 r = x.pos - me.pos; float ao = r.Dot(u);
                if (ao < 10 || ao > MathF.Min(L, 350)) continue;
                float lado = u.x * r.y - u.y * r.x;   // de que lado da linha ele está
                if (MathF.Abs(lado) > 14) continue;
                float giro = (lado >= 0 ? -1 : 1) * MathF.Max(0.3f, MathF.Atan2(22, ao));
                V2 nova = me.pos + V2.Ang(d.Atan2 + giro) * L;
                if (!EXP.Contains("paredeVelha") && MapData.Raio(me.pos, me.z, V2.Ang(d.Atan2 + giro), 450, 18f) < 40) return q;   // pelo ombro, não pra dentro da parede
                return nova;
            }
            return q;
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
            var cands = new List<(V2 p, float sc)>();
            // quem vai na frente andando pra onde eles devem estar não cobre as costas (isso é do último da fila): as
            // quinas de trás quase não contam, a não ser que o perigo esteja mais atrás do que à frente
            float pfTras = 0.2f;
            if (!EXP.Contains("costasVelho") && wantVel.Len > me.MaxSpeed * 0.2f)
            {
                var itB = m.intel[(int)me.team];
                float eFrente = itB.Esperados(me.pos + fw * 300, 350), eTras = itB.Esperados(me.pos - fw * 300, 350);
                V2 alvoC = me.pos + fw * 300;
                bool euFrente = !m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - alvoC).Len < (me.pos - alvoC).Len - 30);
                if (euFrente && eFrente >= 0.8f * eTras) pfTras = 0.03f;
                // o último de um grupo que anda junto é quem cobre as costas (a fila divide: frente pro primeiro, trás pro último)
                bool emGrupo = m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 350);
                bool euUltimo = emGrupo && !m.agents.Any(x => x != me && x.alive && x.team == me.team && (x.pos - me.pos).Len < 350 && (x.pos - alvoC).Len > (me.pos - alvoC).Len + 30);
                if (!euFrente && euUltimo && eTras > 0.15f && !EXP.Contains("semUltimo")) pfTras = 0.5f;
                pfTrasDiag = euFrente ? (eFrente >= 0.8f * eTras ? "frente: perigo à frente" : "frente: perigo atrás") : "atrás de colega";
                if (EXP.Contains("costasPush") && !EXP.Contains("costasPushVelho") && !euUltimo && (order.push || me.Speed > me.MaxSpeed * 0.75f))   /* desligado: regra por posição — o usuário quer a raiz (crença) */
                {   // entrando/correndo pra cima deles: o da frente e o do meio olham pra FRENTE (a conta de ameaça de trás vem do
                    // calor velho — o da frente "achava perigo atrás" e virava). Só vira se tem coisa CONCRETA atrás: alguém visto
                    // ou ouvido há pouco atrás de mim
                    bool atrasConcreto = false;
                    foreach (var c in itB.contacts.Values) if (m.agents[c.id].alive && m.t - c.t < 3f && (c.pos - me.pos).Dot(fw) < -40 && (c.pos - me.pos).Len < 700) { atrasConcreto = true; break; }
                    if (!atrasConcreto && m.t - somT < 2.5f && (somP - me.pos).Dot(fw) < -40 && (somP - me.pos).Len < 500) atrasConcreto = true;
                    if (!atrasConcreto) { pfTras = 0.02f; pfTrasDiag = "entrando: olha pra frente"; }
                }
            }
            foreach (var q in Pixels.Em(me.pos))
            {
                // atrás da quina: pode ter gente agora e eles costumam passar por ali? (geometria pura sem isso piora)
                float th = 0;
                foreach (var a in q.atras) th = MathF.Max(th, Ameaca(a));
                if (th < 0.008f) continue;
                V2 dir = (q.ponto - me.pos) / MathF.Max(1, (q.ponto - me.pos).Len);
                float frente = dir.Dot(fw), pf = frente > 0.35f ? 1f : frente > -0.3f ? 0.6f : pfTras;
                // RAIZ do olhar pra trás: avançando onde ninguém viu, a crença pra FRENTE é ~0 (ninguém visto) e qualquer quina de
                // trás com um fiapo de crença ganhava. Onde eu vou e ainda pode ter alguém (o calor diz que dá pra ter chegado), é
                // o que se checa primeiro: vale um mínimo. Crença forte atrás (visto/ouvido) ainda ganha — é leitura, não regra
                if (frente > 0.35f && wantVel.Len > me.MaxSpeed * 0.2f && EXP.Contains("frentePossivel") && !EXP.Contains("frentePossivelVelho"))   /* opt-in: TR +4,8, retake −12 */
                {
                    float possivel = 0; var itP = m.intel[(int)me.team];
                    foreach (var a in q.atras) possivel = MathF.Max(possivel, itP.HeatAvg(a, 90));
                    if (possivel > 0.004f) th = MathF.Max(th, 0.12f);
                }
                // o costume dos pros (de onde costumam aparecer pra quem está aqui) REFORÇA a crença, não cria ameaça onde eu
                // acho que não tem ninguém: era ele que virava a mira pra trás do avanço (crença 0,01 atrás x 0,86 na frente)
                float cmB = Costume(q.ponto);
                if (EXP.Contains("costumeSemCrenca"))   /* opt-in: não mudou o olhar pra trás e o retake caiu 5 */
                {
                    float crB = 0; var itC = m.intel[(int)me.team];
                    foreach (var a in q.atras) crB = MathF.Max(crB, itC.Esperados(a, 110));
                    if (crB < 0.05f) cmB = MathF.Min(cmB, 1f);
                }
                float sc = th * pf * (1.3f - q.dp / Pixels.MAX) * cmB;
                if (bordaQuina != null && m.t < bordaTrava && (q.ponto - bordaQuina.ponto).Len < 40) sc *= 6f;   // termina de checar antes de trocar de quina
                if (frente < 0.35f && (Lotado(q.ponto) || CobertoPor(q.ponto) != null)) sc *= 0.4f;   // o time já tem essa: prefere a que ninguém olha
                if (bordaRes != null && (q.ponto - bordaRes.Value).Len < 40) sc *= 1.4f;   // não fica pulando entre duas quinas
                cands.Add((q.ponto, sc));
                if (sc > best) { best = sc; res = q.ponto; esc = q; escTh = th; }
            }
            if (PicoCostume(fw, ref best)) { res = picoPonto; esc = null; escTh = picoTh; }
            posGanhou = false;
            if (PosicaoDeles(fw, ref best)) { res = picoPonto; esc = null; escTh = picoTh; posGanhou = true; }
            // duas quinas perigosas perto uma da outra (no ângulo): a mira fica entre elas, puxando pra mais perigosa —
            // o flick pra qualquer uma fica curto (quem olha só uma toma da outra com 20-40° pra virar)
            bordaSeg = null;
            if (res != null && EXP.Contains("comEntre"))   // testado: não mudou a mira no contato e o TR subiu — desligado
            {
                float aR = (res.Value - me.pos).Atan2, s2 = 0; V2? p2 = null;
                foreach (var c in cands)
                {
                    float dA = MathF.Abs(MathX.AngDiff((c.p - me.pos).Atan2, aR)) * 180 / MathF.PI;
                    if (dA < 5 || dA > 40 || c.sc < 0.5f * best) continue;
                    if (c.sc > s2) { s2 = c.sc; p2 = c.p; }
                }
                if (p2 != null) { bordaSeg = p2; bordaSegW = s2 / (best + s2); }
            }
            if (esc != bordaQuina && esc != null) bordaTrava = m.t + 0.45f;
            bordaRes = res; bordaQuina = esc; bordaRisco = escTh; exatoAt = -9;
            P1("borda (crosshair placement)", pc);
            return BordaExata();
        }

        /// <summary>A borda escolhida, medida daqui (recalcula a cada passinho: andando o ângulo da quina muda).</summary>
        V2? BordaExata()
        {
            V2? r;
            if (bordaQuina == null || EXP.Contains("semExato")) r = bordaRes;
            else
            {
                if (!(m.t - exatoAt < 0.1f && (me.pos - exatoMeu).Len < 3)) { exatoAt = m.t; exatoMeu = me.pos; exatoRes = Pixels.Exato(bordaQuina, me.pos, me.z); }
                r = exatoRes;
            }
            return r is V2 rv && bordaSeg is V2 sg ? Entre(rv, sg, bordaSegW) : r;
        }
        V2? bordaSeg; float bordaSegW;
        /// <summary>Ponto entre dois ângulos (peso w pro segundo), na distância do primeiro.</summary>
        V2 Entre(V2 a, V2 b, float w)
        {
            float aA = (a - me.pos).Atan2, aB = (b - me.pos).Atan2, L = (a - me.pos).Len;
            return me.pos + V2.Ang(aA + w * MathX.AngDiff(aB, aA)) * L;
        }
        float exatoAt = -9; V2 exatoMeu, exatoRes;
        float bordaTrava = -9;
        float bordaAt = -9, bordaRisco; V2 bordaMeu; V2? bordaRes; Pixels.Quina bordaQuina;

        /// <summary>
        /// Abrir pixel: ninguém anda reto pra dentro de um ângulo que não checou. Tem quina à frente com perigo atrás: enquanto
        /// a mira não chegou nela, o passo cai; com a mira lá, anda de shift abrindo largo (longe da parede, fatiando o
        /// ângulo). Vale pra qualquer lugar do mapa e qualquer ordem; só a pressa de verdade (relógio, troca, defuse) corre.
        /// </summary>
        void AbrirPixel(V2? mira) { long pc = P0(); AbrirPixelX(mira); P1("abrir pixel (fatiar ângulo)", pc); }
        void AbrirPixelX(V2? mira)
        {
            var q = bordaQuina; if (mira == null || EXP.Contains("semAbrir")) return;
            float d = (mira.Value - me.pos).Len;
            if (wantVel.Len < 1 || d < 1) return;
            // o risco é o que a gente espera ter por perto, não só o valor da quina escolhida
            // (o que se espera do lado de lá da quina: o inimigo que está atrás de mim, no meio, não conta pro canto da base)
            V2 ladoLa = EXP.Contains("abrirVelho") ? me.pos : me.pos + (mira.Value - me.pos) / d * MathF.Min(d, 120);
            if (bordaRisco < 0.03f && m.intel[(int)me.team].Esperados(ladoLa, EXP.Contains("abrirVelho") ? 350 : 250) < 0.25f) return;
            // só se abre o ângulo pra DENTRO do qual se está andando: olhando de lado ou pra trás (indo embora, girando pela
            // base) anda normal — o pro passa pelo meio olhando a janela sem parar a perna
            if (!EXP.Contains("abrirVelho") && MathF.Abs(MathX.AngDiff((mira.Value - me.pos).Atan2, wantVel.Atan2)) > 1.2f) return;
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

        void Face(V2 p, float pitch)
        {
            V2 d = p - me.pos; if (d.Len < 0.5f) return; wantAim = d.Atan2; hasWantAim = true; wantPitch = pitch;
            /* mira na altura: a cabeça de quem estaria em p (piso de p + olho de quem está em pé), vista do meu olho */
            wantV = MathF.Atan2(NavGrid.ZAt(p) + 18f - (me.z + me.Eye), MathF.Max(8f, d.Len));
        }
        float wantV;
        /* MIRA VERTICAL (apMiraVerticalVelho desliga): a mira tem altura (Agent.aimV). Quem segura a janela alta e é pego por alguém
           embaixo (ou o contrário) paga o flick vertical — no tempo de reação e no erro do 1º tiro. Antes só o ângulo de
           lado contava: inimigo em cima do janelão na direção da mira era "pré-mirado" mesmo com a mira no chão. */
        static bool MiraVertical => !EXP.Contains("apMiraVerticalVelho");   /* padrão: A/B 2x900 nota de erros −0,10/−0,12, TR −3,4/−1,0 */
        float FlickV(Agent e) => MathF.Abs(MathF.Atan2(e.z + e.Eye - (me.z + me.Eye), MathF.Max(8f, (e.pos - me.pos).Len)) - me.aimV) * 180 / MathF.PI;

        /// <summary>
        /// Gira a mira. No duelo: flick na velocidade do jogador. Reagindo (levou tiro, perdeu o inimigo de vista agora):
        /// rápido, mas não tanto. Checando ângulo com calma: ~300°/s e desacelerando ao chegar (mão de gente, não
        /// teleporte de mira).
        /// </summary>
        public void TurnAim(float dt)
        {
            if (!hasWantAim) return;
            { float dvv = wantV - me.aimV, passoV = 5.2f * dt; me.aimV += MathF.Abs(dvv) < passoV ? dvv : MathF.Sign(dvv) * passoV; }   /* altura da mira acompanha (~300°/s) */
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
