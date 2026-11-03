using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using static CSM.Sim.Places;

namespace CSM.Sim
{
    /// <summary>
    /// O IGL de cada time: escolhe a estratégia do round e dá as ordens; no meio do round lê a informação do time
    /// (contatos, calor, quem morreu, bomb) e faz as calls: TR decide o bomb pelo lado mais fraco, executa com
    /// granadas, planta e arma o pós-plant; CT monta o setup, gira quando aparece gente num bomb, retoma junto,
    /// salva quando não dá e joga o clutch. Toda call vai pro log com o motivo.
    /// </summary>
    public class TeamBrain
    {
        public static string EXP = "";   // experimento ligado pela linha de comando (MatchCli -exp)
        readonly Match m; readonly Team team; readonly Rng rng;
        public string plan = "", stage = "";
        public readonly List<(float t, string s)> calls = new List<(float, string)>();
        float nextLook; bool executed, postPlantSet, retakeSet, clutchCalled;
        char rotatedTo = ' '; bool giroCerto; float giroT = -99;
        public bool c4Resolvido; public float c4PassadoEm = -99;
        public string estiloPP = "", portaRetakeNome = ""; V2[] entradasPP; float proxSmokePP; readonly List<V2> smokadas = new List<V2>();

        /// <summary>O time VIU que é naquele bomb (o C4 indo pra lá ou 3+ entrando) — não só estimou.</summary>
        bool CertoEm(char site)
        {
            V2 c = site == 'A' ? SITES_A : SITES_B;
            bool c4 = m.t - Intel.bombaT < 6 && (Intel.bombaPos - c).Len < 700;
            int la = Intel.contacts.Values.Count(x => m.t - x.t < 5 && m.agents[x.id].alive && (x.pos - c).Len < 520);
            return c4 || la >= Math.Min(3, m.Alive(Other));
        }

        public TeamBrain(Match m, Team team, int seed) { this.m = m; this.team = team; rng = new Rng(seed); }

        List<Agent> Mine => m.agents.Where(a => a.team == team && a.alive).ToList();
        Agent ByRole(Role r) => m.agents.FirstOrDefault(a => a.team == team && a.alive && a.role == r);
        /// <summary>Quem tem a função, vivo ou não (ordem pra morto não faz nada; evita nulo no meio do round).</summary>
        Agent Funcao(Role r) => m.agents.First(a => a.team == team && a.role == r);
        void Call(string s) { calls.Add((m.t, s)); m.Log($"[{(team == Team.T ? "TR" : "CT")} IGL] {s}"); }
        Intel Intel => m.intel[(int)team];
        Pesos P => m.pesos[(int)team];

        static Order Go(string name, Hold h, string why = null, bool run = true, bool quiet = false) => new Order { name = name, hold = h, why = why, run = run, quiet = quiet };

        // ================================================================ início do round
        public void Start()
        {
            if (team == Team.T) StartT(); else StartCT();
        }

        void StartT()
        {
            string[] plans = { "default + split", "execute A", "execute B", "rush B", "fake A, vai B", "default lento" };
            string compra = m.compra[(int)team];
            // a compra muda o que dá pra fazer: sem granada não tem fake nem execução cheia; eco costuma ir junto e rápido
            // rush é exceção (pro quase nunca rusha de compra cheia; mais em force/eco, que precisam de surpresa)
            float[] w = compra == "full" ? new[] { 0.3f, 0.17f, 0.17f, 0.04f, 0.14f, 0.18f }
                      : compra == "force" ? new[] { 0.26f, 0.2f, 0.2f, 0.14f, 0.08f, 0.12f }
                      : new[] { 0.26f, 0.16f, 0.14f, 0.28f, 0.02f, 0.14f };
            // leitura da partida: aposta no que vem dando certo, não repete o mesmo plano em sequência, e contra-ataca o CT deles
            var L = m.leitura[(int)team]; var motivos = new List<string>();
            if (L != null)
            {
                for (int i = 0; i < plans.Length; i++)
                {
                    w[i] *= 0.6f + 0.8f * L.Sucesso(plans[i]);
                    int rep = L.Repetido(plans[i]); if (rep >= 1) w[i] *= rep >= 2 ? 0.25f : 0.6f;
                }
                if (L.Adapta)
                {   // a intenção do time pesa nos planos; o motivo sai na call
                    L.AtualizaTR();
                    var it = L.intencao; float eA = MathF.Exp(1.2f * it.lado);
                    w[1] *= eA; w[2] /= eA; w[3] /= eA; w[4] /= eA; w[5] *= 1.5f - it.ritmo; w[3] *= 0.5f + it.ritmo;
                    if (it.motivo != "") motivos.Add(it.motivo);
                }
            }
            plan = plans[rng.Weighted(w)];
            if (L != null && L.modo == "previsivel") plan = compra == "full" ? "execute B" : "rush B";
            else if (EscolherDoBanco(true))
            {
                Call($"plano: {estr.Nome}{(compra != "full" ? $" ({compra})" : "")} — bomb {estr.site}, entrar ~{estrHora:0}s{(motivos.Count > 0 ? " — " + string.Join("; ", motivos) : "")}");
                stage = $"plano do banco ({estr.Tipo})"; m.planSite = estr.site;
                SeguirPlano(0, true);
                foreach (var a in m.agents) a.hasBomb = false;
                // o C4 vai (já na base, como o pro faz) pra quem a rota leva mais cedo pro bomb do plano — nem o entry, nem o lurker
                Agent leva = null; float cedo = float.MaxValue;
                if (!EXP.Contains("c4Sorteado"))
                    foreach (var kv in slotDe)
                    {
                        var a = m.agents[kv.Key]; if (a.role == Role.Entry || kv.Value == lurkSlot) continue;
                        float pe = estr.PrimeiroEm(kv.Value, estr.site.ToString());   // 1º instante no lado do bomb (zona)
                        if (pe >= 0 && pe < cedo) { cedo = pe; leva = a; }
                    }
                if (leva == null) { var trsB = m.agents.Where(a => a.IsT).ToList(); leva = trsB[rng.Int(trsB.Count)]; }
                leva.hasBomb = true;
                return;
            }
            Call($"plano: {plan}{(compra != "full" ? $" ({compra})" : "")}{(motivos.Count > 0 ? " — " + string.Join("; ", motivos) : "")}");
            // execute/fake não é ficar 40 s parado na entrada: primeiro controle de mapa (info, espaço), e junta pro
            // execute uns 12 s antes da hora (como no pro); o rush vai direto
            bool depois = (plan.StartsWith("execute") || plan.StartsWith("fake")) && !EXP.Contains("semControle");
            // controle do LADO do execute (execute A: meio, rampa, palácio; B e fake-vai-B: meio, curta, casarão) — junta
            // perto, não atravessa o mapa na hora
            Posicionar(depois ? (plan == "execute A" ? "controle A" : "controle B") : plan);
            if (depois) { encenarEm = (plan.StartsWith("fake") ? P.tFake : P.tExec) - 15; stage = $"controle de mapa (depois {plan})"; }
            // quem leva o bomb: suporte (não é o primeiro a morrer)
            var sup0 = Funcao(Role.Suporte); var lurk0 = Funcao(Role.Lurker);
            foreach (var a in m.agents) a.hasBomb = false;
            // (o lurker fica do outro lado no controle: bomb com ele ficaria longe do execute)
            (plan.Contains("B") && !plan.StartsWith("fake A") && !depois ? lurk0 : sup0).hasBomb = true;
            if (plan.StartsWith("fake")) sup0.hasBomb = true;
            // o C4 não tem dono: nasce com qualquer um, e quem precisar pega depois
            if (!EXP.Contains("c4Velho")) { foreach (var a in m.agents) a.hasBomb = false; var trs = m.agents.Where(a => a.IsT).ToList(); trs[rng.Int(trs.Count)].hasBomb = true; }
        }
        float encenarEm = -1, proxVigiaC4; bool tomouMeio; Agent c4Junto; float c4Reavalia;

        /// <summary>Ordens de posição de um plano do TR ("controle" = controle de mapa sem gastar a granada do execute).</summary>
        void Posicionar(string p)
        {
            var entry = Funcao(Role.Entry); var sup = Funcao(Role.Suporte); var lurk = Funcao(Role.Lurker); var awp = Funcao(Role.Awper); var igl = Funcao(Role.Igl);
            switch (p)
            {
                case "controle A":
                    entry.brain.SetOrder(new Order { name = "controle da rampa", hold = T_RampaBaixo, why = "espaço na rampa antes do execute" });
                    sup.brain.SetOrder(new Order { name = "controle do meio", hold = T_MeioSul, why = "info do meio / conector", notBefore = 3 });
                    awp.brain.SetOrder(new Order { name = "segurar a rampa", hold = T_RampaDobra, why = "pick em quem vier pela rampa" });
                    igl.brain.SetOrder(new Order { name = "controle do palácio", hold = T_PalacioSala, why = "info do A pela sacada" });
                    lurk.brain.SetOrder(new Order { name = "lurk no casarão", hold = T_CasaraoDobra, why = "info do B e pegar quem girar", quiet = true });
                    break;
                case "controle B":
                    entry.brain.SetOrder(new Order { name = "controle da curta", hold = T_CurtaCima, why = "espaço na curta antes do execute", notBefore = 3 });
                    sup.brain.SetOrder(new Order { name = "controle do topo", hold = T_TopoMeio, why = "info do meio" });
                    awp.brain.SetOrder(new Order { name = "segurar o meio", hold = T_MeioSul, why = "pick no meio" });
                    igl.brain.SetOrder(new Order { name = "controle do casarão", hold = T_CasaraoDobra, why = "info do tapete" });
                    lurk.brain.SetOrder(new Order { name = "lurk no palácio", hold = T_PalacioSala, why = "info do A e pegar quem girar", quiet = true });
                    break;
                case "default + split":
                case "default lento":
                    bool lento = p == "default lento";
                    sup.brain.SetOrder(new Order { name = "smoke do janelão e segurar o topo", hold = T_TopoMeio, why = "tirar a visão da AWP do meio", nades = { SmokeJanelao }, paciente = lento });
                    entry.brain.SetOrder(new Order { name = "tomar o meio", hold = T_MeioSul, why = "controle de meio depois da smoke", waitSmoke = true, smokeAt = SmokeJanelao.to, notBefore = 3, paciente = lento });
                    awp.brain.SetOrder(new Order { name = "segurar a rampa", hold = T_RampaDobra, why = "pick em quem vier pela rampa", paciente = lento });
                    igl.brain.SetOrder(new Order { name = "controle do palácio", hold = T_PalacioSala, why = lento ? "segurar e ver se alguém se expõe" : "pressão no A pela sacada", paciente = lento });
                    lurk.brain.SetOrder(new Order { name = "lurk no casarão", hold = T_CasaraoDobra, why = "info do B e pegar rotação", quiet = true, paciente = lento });
                    stage = lento ? "lento: esperando eles se mexerem" : "controle de mapa"; break;
                case "execute A":
                    entry.brain.SetOrder(Go("ir pra dobra da rampa", T_RampaDobra, "entrada do A"));
                    sup.brain.SetOrder(new Order { name = "smoke da plataforma e ir com o entry", hold = T_RampaBaixo, why = "cortar a plataforma e trocar o entry", nades = { SmokePlataforma } });
                    awp.brain.SetOrder(new Order { name = "smoke da descida do CT", hold = T_RampaDobra, why = "cortar a rotação pela rua do CT", nades = { SmokeRuaCT } });
                    igl.brain.SetOrder(Go("palácio pra cair na sacada", T_PalacioSala, "segunda entrada no A"));
                    lurk.brain.SetOrder(Go("lurk no conector", T_Conector, "flanco pelo meio", quiet: true));
                    stage = "indo pro A"; break;
                case "execute B":
                case "rush B":
                    bool rush = p == "rush B";
                    entry.brain.SetOrder(Go(rush ? "rush pela curta" : "ir pra curta", T_CurtaCima, rush ? "chegar antes do CT girar" : "entrada do B"));
                    sup.brain.SetOrder(rush ? Go("rush atrás do entry", T_CurtaCima) : new Order { name = "flash e smoke pela curta", hold = T_CurtaCima, why = "cegar a banca e cortar o mercado", nades = { SmokeMercado, FlashB } });
                    awp.brain.SetOrder(Go("segurar o meio", T_MeioSul, "ninguém gira pelo meio", quiet: !rush));
                    igl.brain.SetOrder(rush ? Go("rush pelo casarão", T_CasaraoDobra) : new Order { name = "molotov na banca", hold = T_CasaraoDobra, why = "tirar o âncora da banca", nades = { MolotovBanca } });
                    lurk.brain.SetOrder(rush ? Go("rush pelo casarão", T_CasaraoDobra) : new Order { name = "smoke da viela", hold = T_CasaraoDobra, why = "cortar a rotação do CT", nades = { SmokeViela } });
                    stage = rush ? "rush" : "indo pro B"; break;
                default: // fake A, vai B
                    awp.brain.SetOrder(new Order { name = "fake no A: smoke e flash", hold = T_RampaDobra, why = "puxar a rotação pro A", nades = { SmokeRuaCT, FlashA } });
                    igl.brain.SetOrder(Go("fazer barulho na rampa", T_RampaBaixo, "fingir execução no A"));
                    entry.brain.SetOrder(Go("esperar na curta", T_CurtaCima, "execução real no B", quiet: true));
                    sup.brain.SetOrder(Go("esperar na curta", T_CurtaCima, "trocar o entry no B", quiet: true));
                    lurk.brain.SetOrder(Go("esperar no casarão", T_CasaraoDobra, "entrar pelo tapete", quiet: true));
                    stage = "fake"; break;
            }
            Espalhar();
        }

        /// <summary>
        /// Dois do time com a mesma posição (ou colada): o segundo vai pra um ponto próprio perto dela que vê o mesmo
        /// ângulo de outro lugar — ninguém em cima do outro mirando o mesmo pixel.
        /// </summary>
        void Espalhar()
        {
            if (EXP.Contains("semEspalhar")) return;
            var usados = new List<V2>();
            foreach (var a in m.agents.Where(x => x.alive && x.team == team && x.brain.order.hold != null))
            {
                var h = a.brain.order.hold;
                if (!usados.Any(u => (u - h.pos).Len < 45)) { usados.Add(h.pos); continue; }
                V2 alvo = h.look.Length > 0 ? h.look[0] : h.pos;
                for (int k = 0; k < 24; k++)
                {
                    V2 q = NavGrid.Snap(h.pos + V2.Ang(k * 0.2618f + rng.Uniform(0, 0.2f)) * rng.Uniform(45, 95));
                    if (!NavGrid.Movable(q) || usados.Any(u => (u - q).Len < 45)) continue;
                    if (!MapData.LosWalk(q, NavGrid.ZAt(q), alvo, NavGrid.ZAt(alvo), 18f) && rng.Rand() < 0.8f) continue;
                    var nh = new Hold(h.name + " (do lado)", q, h.look.Reverse().ToArray());   // outro ponto, ângulo na outra ordem
                    a.brain.order.hold = nh; usados.Add(q);
                    break;
                }
            }
        }

        // ================================================================ banco de estratégias (rounds pro traduzidos)
        Estrategias.Plano estr; readonly Dictionary<int, int> slotDe = new Dictionary<int, int>(); readonly Dictionary<int, string> estrUlt = new Dictionary<int, string>();
        int lurkSlot = -1; bool lurkSegue; float estrHora = -1;
        bool UsaBanco => estr != null;
        /// <summary>Plano do banco forçado nesta thread (índice + 1; 0 = escolhe) — só pra medir a força de cada um.</summary>
        [ThreadStatic] public static int ForcaTR, ForcaCT;
        public Estrategias.Plano Estrategia => estr;

        /// <summary>
        /// Escolhe um plano do banco pela compra: pesa a força medida no headless, se a jogada deu certo no pro, não
        /// repetir o que já usou na partida, o que vem dando certo por tipo (rush/execução/default/setup) e a leitura
        /// (TR: o lado que o time quer atacar; CT: onde o TR deles costuma ir).
        /// </summary>
        bool EscolherDoBanco(bool tr)
        {
            if (EXP.Contains("semBanco") || EXP.Contains(tr ? "semBancoTR" : "semBancoCT")) return false;
            string compra = m.compra[(int)team];
            int forcado = tr ? ForcaTR : ForcaCT;   // medição de força (Headless -banco): plano fixo nesta thread
            if (forcado > 0) { estr = Estrategias.Todos[forcado - 1]; plan = "banco " + estr.Tipo; DistribuirSlots(tr); return true; }
            var cands = Estrategias.Todos.Where(p => p.tr == tr && p.compra == compra).ToList();
            if (cands.Count < 10) cands = Estrategias.Todos.Where(p => p.tr == tr && p.compra == "full").ToList();
            if (cands.Count == 0) return false;
            var L = m.leitura[(int)team];
            float lado = L != null && L.Adapta ? L.intencao.lado : 0;
            var w = new float[cands.Count];
            for (int i = 0; i < cands.Count; i++)
            {
                var p = cands[i]; float f = Estrategias.Forca(p.idx) * (p.ganhou ? 1.15f : 0.9f);
                if (L != null)
                {
                    if (L.bancoUsados.Contains(p.idx)) f *= 0.25f;
                    f *= 0.6f + 0.8f * L.Sucesso("banco " + p.Tipo);
                }
                if (tr) f *= MathF.Exp((p.site == 'A' ? 1.2f : -1.2f) * lado);
                else
                {   // CT: quantos o setup põe em cada bomb aos 20 s
                    int nA = 0, nB = 0;
                    for (int k = 0; k < 5; k++) { var z = p.ZonaEntre(k, 15, 25); if (z == "A") nA++; else if (z == "B") nB++; }
                    f *= MathF.Exp(0.3f * lado * (nA - nB));
                }
                w[i] = f;
            }
            estr = cands[rng.Weighted(w)];
            L?.bancoUsados.Add(estr.idx);
            plan = "banco " + estr.Tipo;
            DistribuirSlots(tr);
            return true;
        }

        /// <summary>Quem do nosso time faz a rota de qual jogador do plano: pela função (lurker, entry, AWP, âncoras).</summary>
        void DistribuirSlots(bool tr)
        {
            slotDe.Clear(); estrUlt.Clear(); lurkSlot = -1; lurkSegue = false;
            var livres = new List<int> { 0, 1, 2, 3, 4 };
            int Pega(Func<int, float> sc) { int b = livres.OrderByDescending(sc).First(); livres.Remove(b); return b; }
            void Dar(Agent a, int k) { if (a != null && !slotDe.ContainsKey(a.id)) slotDe[a.id] = k; }
            if (tr)
            {
                estrHora = estr.HoraDeEntrar(MathF.Min(P.tExecDefault + 10, 85));
                string outro = estr.site == 'A' ? "B" : "A";
                int lk = Pega(k => estr.TempoEm(k, outro, 10, MathF.Max(20, estrHora)));
                lurkSlot = estr.TempoEm(lk, outro, 10, MathF.Max(20, estrHora)) >= 6 ? lk : -1;   // a rota dele é mesmo do outro lado
                Dar(Funcao(Role.Lurker), lk);
                Dar(Funcao(Role.Entry), Pega(k => { float p = estr.PrimeiroEm(k, estr.site.ToString()); return p < 0 ? -999 : -p; }));
                Dar(Funcao(Role.Awper), Pega(k => estr.TempoEm(k, "Mid", 5, 35)));
                Dar(Funcao(Role.Igl), Pega(k => rng.Rand()));
                Dar(Funcao(Role.Suporte), livres[0]);
            }
            else
            {
                var ancA = m.agents.First(a => a.team == Team.CT && a.role == Role.Ancora && a.brain.homeSite == 'A');
                var ancB = m.agents.First(a => a.team == Team.CT && a.role == Role.Ancora && a.brain.homeSite == 'B');
                Dar(ancA, Pega(k => estr.TempoEm(k, "A", 10, 35)));
                Dar(ancB, Pega(k => estr.TempoEm(k, "B", 10, 35)));
                Dar(ByRole(Role.Awper), Pega(k => estr.TempoEm(k, "Mid", 5, 35)));
                Dar(ByRole(Role.Rotacao), Pega(k => rng.Rand()));
                Dar(ByRole(Role.Suporte), livres[0]);
            }
        }

        /// <summary>
        /// Cada um vai pra região onde o jogador dele estava no pro nesse instante (um pouco adiantado, pra dar tempo de
        /// chegar). Só mexe em quem ainda está na ordem do plano: troca, giro, call do IGL e granada têm prioridade.
        /// </summary>
        void SeguirPlano(float t, bool forcar = false, Agent so = null)
        {
            foreach (var kv in slotDe)
            {
                var a = m.agents[kv.Key];
                if (!a.alive || so != null && a != so) continue;
                var o = a.brain.order;
                if (!forcar && !(o.name.StartsWith("plano:") || o.name == "esperar")) continue;
                // antes do 1º passo da rota (passagem curta pela base não conta) já vai pra ele: ninguém espera parado na base
                string area = estr.Onde(kv.Value, t + 2f);
                if (area == null || area == "TR" || area == "CT")
                {   // o próximo lugar da rota; se no pro ele ficou um tempão na base (default lento), espera até faltar ~10 s
                    string prox = null; float quando = 0;
                    foreach (var ps in estr.js[kv.Value].passos) if (ps.de >= t && ps.area != "TR" && ps.area != "CT") { prox = ps.area; quando = ps.de; break; }
                    if (prox == null || quando - t > 10) continue;
                    area = prox;
                }
                if (!forcar && estrUlt.TryGetValue(a.id, out var u) && u == area) continue;
                estrUlt[a.id] = area;
                var h = Estrategias.Hold(area, team == Team.T, estr.site);
                // dois na mesma região: cada um num ponto dela (ninguém em cima do outro mirando o mesmo pixel)
                var ocup = new List<V2>();
                foreach (var x in m.agents) if (x != a && x.alive && x.team == team && x.brain.order.hold != null) ocup.Add(x.brain.order.hold.pos);
                h = Estrategias.Espalhado(area, h, ocup, rng);
                bool quieto = team == Team.T && kv.Value == lurkSlot;
                a.brain.SetOrder(new Order { name = $"plano: {h.name}", hold = h, why = $"{estr.Nome}: no pro ele estava aqui agora", paciente = team == Team.T && estr.Tipo == "default", quiet = quieto });
            }
        }

        void StartCT()
        {
            // "B forte": o B é o lado rápido do TR neste mapa (TR chega em ~9 s, a rotação do A leva 14 s) — 3 no B, 1 no A,
            // AWP no meio; o A segura com 1 + a rotação (o TR também leva 14 s pra chegar lá)
            string[] plans = { "padrão 2-1-2", "stack A", "stack B", "B forte 1-1-3" };
            bool eco = m.compra[(int)team] != "full";
            bool ecoPuro = m.compra[(int)team] == "eco" && !EXP.Contains("ecoVelho");
            float[] w = ecoPuro ? new[] { 0.08f, 0.42f, 0.42f, 0.08f } : eco ? new[] { 0.25f, 0.3f, 0.3f, 0.15f } : new[] { 0.42f, 0.13f, 0.15f, 0.3f };   // sem arma: aposta num lado
            if (EXP.Contains("semBforte")) w[3] = 0;
            // leitura da partida: empilha onde o TR deles costuma ir, gira devagar contra time que faz fake, segura a
            // agressão contra time que joga lento; e aposta no setup que vem dando certo (sem ficar previsível)
            var L = m.leitura[(int)team]; var motivos = new List<string>(); float agrFator = 1, meioFator = 1;
            if (L != null)
            {
                for (int i = 0; i < plans.Length; i++)
                {
                    w[i] *= 0.6f + 0.8f * L.Sucesso(plans[i]);
                    int rep = L.Repetido(plans[i]); if (rep >= 2 && i > 0) w[i] *= 0.5f;
                }
                if (L.Adapta)
                {
                    L.AtualizaCT();
                    var it = L.intencao;   // stack puro ganha pouco (37-39% em compra cheia): a leitura puxa mais pro setup reforçado
                    w[1] *= MathF.Exp(0.6f * it.lado); w[2] *= MathF.Exp(-0.5f * it.lado); w[3] *= MathF.Exp(-1.5f * it.lado); if (it.lado < -0.3f) w[1] *= 0.12f; if (it.lado > 0.3f) { w[2] *= 0.12f; w[3] *= 0.4f; } giroCauteloso = it.giroCauteloso; agrFator = it.agressao; meioFator = it.meio;
                    if (it.motivo != "") motivos.Add(it.motivo);
                }
            }
            plan = plans[rng.Weighted(w)];
            if (L != null && L.modo == "previsivel") plan = plans[0];
            else if (EscolherDoBanco(false))
            {
                Call($"setup: {estr.Nome}{(eco ? $" ({m.compra[(int)team]})" : "")}{(motivos.Count > 0 ? " — " + string.Join("; ", motivos) : "")}");
                stage = "setup do banco";
                SeguirPlano(0, true);
                return;
            }
            Call($"setup: {plan}{(eco ? $" ({m.compra[(int)team]})" : "")}{(motivos.Count > 0 ? " — " + string.Join("; ", motivos) : "")}");
            var ancA = m.agents.First(a => a.team == Team.CT && a.role == Role.Ancora && a.brain.homeSite == 'A');
            var ancB = m.agents.First(a => a.team == Team.CT && a.role == Role.Ancora && a.brain.homeSite == 'B');
            var rot = ByRole(Role.Rotacao); var awp = ByRole(Role.Awper); var sup = ByRole(Role.Suporte);
            // âncora varia o lugar (o TR que pré-mira sempre o mesmo canto/AWP na linha longa pune quem é previsível)
            // de pistola, ângulo longo é suicídio: eco pega o canto de perto (quem entra tem que vir na cara)
            float perto = ecoPuro ? 0.85f : 0.5f;
            var hA = AncoraA[EXP.Contains("ancoraFixa") ? 0 : rng.Weighted(new[] { 1 - perto, perto })];
            var hB = AncoraB[EXP.Contains("ancoraFixa") ? 0 : rng.Weighted(new[] { 1 - perto, perto })];
            ancA.brain.SetOrder(Go($"âncora: {hA.name}", hA, "segurar rampa e sacada"));
            ancB.brain.SetOrder(Go($"âncora: {hB.name}", hB, "segurar tapete e curta"));
            // eco empilhado: o AWPer (de pistola) vai junto pro bomb da aposta em vez de segurar o meio de longe
            if (ecoPuro && (plan == "stack A" || plan == "stack B"))
            {
                bool sa = plan == "stack A";
                var h = sa ? (hA == CT_A_Caixa ? CT_A_Plataforma : CT_A_Caixa) : (hB == CT_B_Caixa ? CT_B_Banca : CT_B_Caixa);
                awp.brain.SetOrder(Go($"eco: {h.name}", h, $"de pistola junto no {(sa ? 'A' : 'B')}"));
            }
            // sem AWP (não deu dinheiro): o janelão ainda é o melhor lugar pra ver o meio (de rifle também se joga, como a
            // janela da Mirage) — às vezes segura o meio de perto, da praça. De pistola/SMG, menos (linha longa)
            else if (L != null && L.Adapta && L.Queimado(CT_Janelao.pos, team, 90) >= 1.5f && rng.Rand() < 0.5f)
            {   // abrimos o janelão muitas vezes: agora eles esperam lá
                var hj = rng.Rand() < 0.5f ? CT_MeioCaixa : CT_Praca;
                awp.brain.SetOrder(Go($"{awp.gunId.ToUpper()} em {hj.name}", hj, "o janelão queimou: eles já esperam a gente lá"));
                Call($"{awp.name} sai do janelão neste round: queimou");
            }
            else if (awp.gunId == "awp" || rng.Rand() < (EXP.Contains("janelaoVelho") ? 0.35f : WeaponStats.Pistola(awp.gunId) || awp.gunId == "mp9" || awp.gunId == "mac10" ? 0.4f : 0.65f))
                awp.brain.SetOrder(Go(awp.gunId == "awp" ? "AWP no janelão" : "janelão de rifle", CT_Janelao, awp.gunId == "awp" ? "pick no meio" : "info do meio e pick de quem cruza"));
            else awp.brain.SetOrder(Go("praça (sem AWP)", CT_Praca, "de rifle seguro o meio de perto"));
            // o rotação no padrão joga no meio (praça: 7 s do A, 8 s do B) — coringa que chega nos dois bombs, como o
            // jogador do conector da Mirage; no canto do A ele levava 13 s pro B. Se a praça já é do AWPer sem AWP, fica no A
            bool pracaLivre = !(awp.brain.order.hold == CT_Praca) && !EXP.Contains("rotNoA");
            // o meio é do CT: sem ele o TR escolhe o bomb e corta a rotação. Quem joga ali varia o lugar pra não ser pré-mirado
            var meio = new List<Hold> { CT_MeioCaixa, CT_Conector, CT_Rato }; if (pracaLivre) meio.Add(CT_Praca);
            float pMeio = meioFator * (EXP.Contains("meioVelho") ? 0 : plan == "padrão 2-1-2" ? 0.9f : plan.StartsWith("B forte") ? 0.5f : 0.3f);
            if (rng.Rand() < pMeio) { var hm = meio[rng.Int(meio.Count)]; rot.brain.SetOrder(Go($"meio: {hm.name}", hm, "controle do meio: chego nos dois bombs")); }
            else
            rot.brain.SetOrder(plan == "stack B" ? Go("stack: viela", CT_B_Viela, "o IGL leu B") : plan.StartsWith("B forte") ? Go("viela (B forte)", CT_B_Viela, "o B é o lado rápido deles")
                             : plan == "padrão 2-1-2" && pracaLivre && rng.Rand() < 0.65f ? Go("praça (coringa)", CT_Praca, "chego nos dois bombs") : Go("canto do A", CT_A_Canto, "segurar o conector"));
            sup.brain.SetOrder(plan == "stack A" ? Go("stack: rua do CT", CT_A_RuaCT, "o IGL leu A") : Go("pilar do B", CT_B_Pilar, "segurar a curta"));
            stage = "setup";
            // smoke no topo do meio cedo: corta a passagem do TR pelo meio (às vezes; quem tem smoke e está a caminho)
            if (!eco && rng.Rand() < 0.35f && !EXP.Contains("semSmokeMeio"))
            {
                var sm = new[] { awp, rot, sup }.Where(a => a.nades.Contains("smoke")).OrderBy(a => (a.pos - CT_SmokeMeio.from).Len).FirstOrDefault();
                if (sm != null) { var o = sm.brain.order; o.nades.Add(CT_SmokeMeio); Call($"{sm.name}: smoke no topo do meio cedo"); }
            }
            // jogada agressiva cedo: um vai pegar info/abate antes do TR se posicionar e volta pro lugar dele
            if (rng.Rand() < (EXP.Contains("agrVelha") ? 1f : 0.4f) * P.ctAgressao * (1 + 0.5f * P.estilo) * (eco ? 1.6f : 1f) * agrFator)   // de eco, caçar arma/abate cedo vale mais
            {
                agressaoCedo = true; m.Ev1("CT jogada agressiva cedo % rounds");
                var opcoes = new List<(Agent who, Hold h)>();
                if (plan != "stack B") opcoes.Add((rot, CT_Agr_Rampa));
                if (plan != "stack A") { opcoes.Add((sup, CT_Agr_Curta)); opcoes.Add((ancB, CT_Agr_Tapete)); }
                opcoes.Add((rng.Rand() < 0.5f ? rot : sup, CT_Agr_Meio));
                var (who, h) = opcoes[rng.Int(opcoes.Count)];
                var depois = who.brain.order;
                who.brain.SetOrder(new Order { name = h.name, hold = h, why = "pegar info cedo e voltar", until = P.ctRecua * rng.Uniform(0.8f, 1.2f), then = depois, zona = true });
                Call($"{who.name}: {h.name} até {who.brain.order.until:0}s e volta");
            }
        }

        // ================================================================ calls no meio do round
        public void Tick()
        {
            if (m.t < nextLook) return;
            nextLook = m.t + 0.5f;
            var mine = Mine; if (mine.Count == 0) return;
            if (mine.Count == 1 && !clutchCalled) { clutchCalled = true; Call($"clutch: {mine[0].name} sozinho contra {m.Alive(Other)}"); }
            if (team == Team.T) TickT(mine); else TickCT(mine);
        }

        Team Other => team == Team.T ? Team.CT : Team.T;

        void TickT(List<Agent> mine)
        {
            var carrier = mine.FirstOrDefault(a => a.hasBomb);
            // C4 guardado de propósito num lugar seguro: fica lá até a execução (alguém pega no caminho pro bomb)
            float buscar = 0;
            if (m.bombDropped && carrier == null && !m.bombPlanted && mine.Count > 0)
            {
                float ate = float.MaxValue; foreach (var a in mine) ate = MathF.Min(ate, NavGrid.PathLen(a.pos, m.droppedBomb));
                buscar = (ate + NavGrid.PathLen(m.droppedBomb, Plant(m.planSite))) / MapCheck.RUN * 1.5f + 15;   // ida + levar + entrar no bomb lutando
            }
            bool guardadoSeguro = m.bombGuardado && !executed && m.RoundLeft > buscar && Intel.Esperados(m.droppedBomb, 300) < 0.4f && !EXP.Contains("semGuardaC4");   // quando o tempo só dá pra buscar e plantar, alguém vai
            if (carrier == null && !m.bombPlanted && m.bombDropped && !guardadoSeguro)
            {
                var near = m.bombGuardado && executed ? QuemPegaC4(mine, m.planSite) : mine.OrderBy(a => (a.pos - m.droppedBomb).Len).First();
                // tem CT conhecido olhando o bomb? passar na frente dele é morrer: smoke na linha e pega atrás dela, ou
                // espera a info ficar velha (ele pode ter saído). Tempo apertando: vai assim mesmo
                var vig = Intel.contacts.Values.Where(c => m.t - c.t < 8 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 650
                                                      && MapData.LosWalk(c.pos, c.z, m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f)
                                                      && !m.SmokeBlocks(c.pos, c.z + 18, m.droppedBomb, NavGrid.ZAt(m.droppedBomb) + 10))   // smoke na linha: aproveita e pega
                                               .OrderBy(c => (c.pos - m.droppedBomb).Len).FirstOrDefault();
                if (vig != null && m.RoundLeft > 35 && !EXP.Contains("semC4Vigiado") && Rola1("c4 vigiado " + vig.id, 0.85f))
                {
                    var quem = m.agents[vig.id];
                    V2 lado = (vig.pos - m.droppedBomb) / MathF.Max(1, (vig.pos - m.droppedBomb).Len);
                    var o = new Order { name = "pegar o bomb (vigiado)", dest = m.droppedBomb, why = $"o {quem.name} tá olhando o bomb" };
                    var smk = mine.Where(a => a.nades.Contains("smoke") && a.brain.action == null && (a.pos - m.droppedBomb).Len < 600).OrderBy(a => (a.pos - m.droppedBomb).Len).FirstOrDefault();
                    var fl = mine.Where(a => a.nades.Contains("flash") && a.brain.action == null && (a.pos - vig.pos).Len < 650).OrderBy(a => (a.pos - m.droppedBomb).Len).FirstOrDefault();
                    // cada C4 caído é um round diferente: fumar a linha, bangar e ir junto, ou segurar e deixar o olho dele sair de lá
                    int jeito = EXP.Contains("c4CaidoVelho") ? (smk != null ? 0 : 2) : rng.Weighted(new[] { smk != null ? 1f : 0, fl != null && mine.Count >= 2 ? 0.9f : 0, 0.6f });
                    if (jeito == 1)
                    {
                        var l = new Lineup("flash pra pegar o bomb", "flash", fl.pos, vig.pos);
                        if (fl == near) o.nades.Add(l); else fl.brain.order.nades.Add(l);
                        o.notBefore = m.t + (fl == near ? 0 : 1.2f); o.push = true;
                        var apoio = mine.Where(a => a != near).OrderBy(a => (a.pos - m.droppedBomb).Len).FirstOrDefault();
                        if (apoio != null) { var ant = apoio.brain.order; apoio.brain.SetOrder(new Order { name = "cobrir quem pega o C4", dest = NavGrid.Snap(m.droppedBomb + lado * 90), push = true, why = "trocar se ele cair", until = m.t + 7, then = ant }); }
                        Call($"bomb caiu na mira do {quem.name}: {fl.name} banga, {near.name} pega{(apoio != null ? $", {apoio.name} cobre" : "")}");
                    }
                    else if (jeito == 0)
                    {
                        var l = new Lineup("smoke pra pegar o bomb", "smoke", smk.pos, NavGrid.Snap(m.droppedBomb + lado * 45));
                        if (smk == near) o.nades.Add(l);
                        else { smk.brain.order.nades.Add(l); o.waitSmoke = true; o.smokeAt = l.to; o.notBefore = m.t; }
                        Call($"bomb caiu na mira do {quem.name}: {smk.name} smoka, {near.name} pega atrás da smoke");
                    }
                    else { o.notBefore = vig.t + 8; Call($"bomb caiu na mira do {quem.name}: {near.name} espera ele sair de lá"); }
                    near.brain.SetOrder(o);
                }
                else if (!near.brain.order.name.StartsWith("pegar o bomb")) { near.brain.SetOrder(new Order { name = "pegar o bomb", dest = m.droppedBomb, why = "bomb caiu" }); Call($"{near.name} pega o bomb"); }
                return;
            }
            // pegou o C4 e ninguém mandou nada: leva pra perto de quem está mais à frente do bomb, não fica parado com ele na base
            bool pegou = carrier != null && carrier.brain.order.name.StartsWith("pegar o bomb") && carrier.brain.order.then == null;
            // na base sem ter pra onde ir: se a ordem dele já leva pra fora da base (entrada, jogada), não atropela
            V2 vaiPara = carrier?.brain.order.hold?.pos ?? carrier?.brain.order.dest ?? V2.Zero;
            bool ordemFora = carrier != null && (carrier.brain.order.plant || vaiPara.Len > 1 && (vaiPara - MapData.SPAWN_T[0]).Len > 420) && !EXP.Contains("c4BaseVelho");
            bool naBase = carrier != null && m.t > 22 && (carrier.pos - MapData.SPAWN_T[0]).Len < 420 && carrier.brain.target == null && !carrier.brain.order.name.StartsWith("levando") && !ordemFora;
            // levando pro time, mas quem ele ia encontrar morreu: o destino virou um lugar qualquer — escolhe de novo
            bool levandoMorto = carrier != null && carrier.brain.order.name == "levando o C4 pro time" && c4Junto != null && !c4Junto.alive && !EXP.Contains("c4JuntoVelho");
            // levando pro time e já chegou (o colega estava perto, ou parou ali): o time anda e o bomb lido muda, então de
            // tempos em tempos olha de novo quem está mais à frente — senão ele fica o round inteiro parado no ponto antigo
            bool levandoChegou = carrier != null && carrier.brain.order.name == "levando o C4 pro time" && (carrier.pos - carrier.brain.order.dest).Len < 45
                                 && m.t > c4Reavalia && carrier.brain.target == null && !EXP.Contains("c4ChegouVelho");
            if (carrier != null && (pegou || naBase || levandoMorto || levandoChegou) && !executed && !m.bombPlanted && mine.Count > 1 && !EXP.Contains("c4Base"))
            {
                V2 pl = Plant(siteLido != ' ' ? siteLido : m.planSite);
                var junto = mine.Where(a => a != carrier).OrderBy(a => NavGrid.PathLen(a.pos, pl)).First();
                V2 ate = NavGrid.Snap(junto.pos); if (float.IsInfinity(NavGrid.PathLen(carrier.pos, ate))) ate = pl;
                // leva pra trás de quem está mais à frente (o C4 não vai pra ponta, pra onde o colega está tomando contato)
                if (!EXP.Contains("c4ProPonta"))
                {
                    var cam = NavGrid.Path(carrier.pos, ate);
                    if (cam != null && cam.Count > 1)
                    {
                        float atras = 180 + 120 * rng.Rand(), resto = 0;
                        V2 orig = ate;
                        for (int k = cam.Count - 1; k > 0; k--) { resto += (cam[k].p - cam[k - 1].p).Len; if (resto >= atras) { ate = cam[k - 1].p; break; } if (k == 1) ate = carrier.pos; }
                        if (Places.Region(ate) == "base TR") ate = orig;   // recuar não é ficar na base
                    }
                }
                c4Junto = junto; c4Reavalia = m.t + 4;
                if (levandoChegou && (ate - carrier.brain.order.dest).Len < 30) { }   // nada mudou: segue onde está (sem reiniciar a ordem)
                else carrier.brain.SetOrder(new Order { name = "levando o C4 pro time", dest = ate, why = $"o time está com o {junto.name}" });
            }
            // adaptação: vem perdendo o pós-plant, a smoke guardada corta a entrada por onde eles vêm
            if (m.bombPlanted && entradasPP != null && m.t >= proxSmokePP && (m.leitura[(int)team]?.intencao.guardaSmoke ?? false) && !EXP.Contains("semAdaptaPos"))
            {
                proxSmokePP = m.t + 1;
                foreach (var e in entradasPP)
                {
                    if (smokadas.Contains(e) || Intel.Esperados(e, 340) < 0.5f) continue;
                    var q = mine.Where(a => a.nades.Contains("smoke") && a.brain.action == null && a.brain.target == null).OrderBy(a => (a.pos - e).Len).FirstOrDefault();
                    if (q != null && q.brain.JogaRetake("smoke", e)) { smokadas.Add(e); m.Ev("smoke guardada no pós-plant/round"); Call($"{q.name}: smoke na entrada deles ({Region(e)})"); break; }
                }
            }
            if (carrier != null && executed && !m.bombPlanted && !carrier.brain.order.plant)
                carrier.brain.SetOrder(new Order { name = $"levar o bomb pro {m.planSite} e plantar", dest = Plant(m.planSite), plant = true, push = true, why = "estou com o bomb" });
            Flanquear(mine);
            if (m.bombPlanted)
            {
                if (!postPlantSet)
                {
                    postPlantSet = true;
                    // cada time joga o pós-plant de um jeito, e cada round é um round
                    string[] estilos = { "de longe", "colados no bomb", "misto" };
                    var lp = m.leitura[(int)team];
                    float PP(int k) => lp == null || EXP.Contains("semAdaptaPos") ? 1 : lp.Peso("pp:" + estilos[k]);
                    string estilo = estilos[rng.Weighted(new[] { 0.5f * PP(0), (0.2f + 0.1f * P.estilo) * PP(1), 0.3f * PP(2) })];
                    estiloPP = estilo;
                    stage = $"pós-plant ({estilo})";
                    var spots = PostPlant(m.planSite);
                    V2[] entradas = m.planSite == 'A' ? new[] { A_Descida, A_RuaCT, CT_A_Plataforma.pos } : new[] { B_Viela, B_Mercado };
                    entradasPP = entradas;
                    // cada entrada da retomada tem um dono (não dois na mesma e uma aberta); quem sobra fica no bomb
                    var donos = CoberturaPosPlant(mine, estilo);
                    int k = 0;
                    foreach (var a in mine)
                    {
                        bool colado = estilo == "colados no bomb" || estilo == "misto" && k % 2 == 1;
                        Hold h = !EXP.Contains("semCobertura") && donos.TryGetValue(a, out var hd) ? hd
                               : colado ? new Hold("colado no bomb", NavGrid.Snap(m.bombPos + V2.Ang(rng.Uniform(0, 6.28f)) * rng.Uniform(22, 45)), entradas)
                               : spots[k % spots.Length];
                        // o mais agressivo às vezes vai buscar espaço na entrada do CT (às vezes pega, às vezes morre)
                        if (rng.Rand() < 0.25f * P.Agr(a.skill.aggression) && mine.Count >= 3 && !h.name.StartsWith("cobrindo"))   // dono de entrada não larga ela
                            h = new Hold("pegando espaço", NavGrid.Snap(entradas[rng.Int(entradas.Length)]), m.planSite == 'A' ? CT_A_RuaCT.pos : CT_B_Viela.pos);
                        a.brain.SetOrder(Go(h.name, h, h.name == "pegando espaço" ? "pegar info da retomada" : h.name.StartsWith("cobrindo") ? "essa entrada é minha" : colado ? "segurar em cima do bomb" : "proteger o bomb de longe"));
                        k++;
                    }
                    Call($"bomb no {m.planSite}: pós-plant {estilo}");
                }
                // ouviu o defuse: cada um decide. Se dá tempo pra ele, a maioria vai. Se não dá, o certo é deixar ele perder
                // tempo — mas com a folga apertada (explode em 4,5 s, defuse acaba em 5) muita gente fica com medo e vai mesmo assim
                if (m.defuserActive && !defuseResp)
                {
                    defuseResp = true;
                    float folga = m.DefuseFolga;
                    foreach (var a in mine)
                    {
                        float medo = MathX.Clamp(1 - folga / 2.5f, 0, 1) * (1.25f - a.skill.composure);
                        float p = folga < 0 ? 0.5f + 0.4f * a.skill.aggression : medo;
                        if (rng.Rand() < p)
                            a.brain.SetOrder(new Order { name = "pegar o defuse", dest = m.bombPos, push = true,
                                why = folga < 0 ? "o defuse dele dá tempo" : $"não devia dar tempo ({folga:0.0}s), mas não vou arriscar" });
                        else a.brain.Think(folga < 0 ? "ouvi o defuse — segurando o ângulo do bomb" : $"defuse não dá tempo pra ele ({folga:0.0}s) — deixo ele perder tempo");
                    }
                }
                if (!m.defuserActive) defuseResp = false;
                CacarSave(mine);
                return;
            }
            // decide o bomb (default) — lado mais fraco pela informação
            float t = m.t;
            // plano do banco: cada um segue a rota do jogador dele no pro até a hora de entrar (o lurker segue depois também)
            if (UsaBanco && !executed) SeguirPlano(t);
            else if (UsaBanco && lurkSegue) foreach (var kv in slotDe) if (kv.Value == lurkSlot) SeguirPlano(t, false, m.agents[kv.Key]);
            // meio dominado é forte (corta a rotação do CT, abre os dois bombs): quem segura o meio sul avança pro meio
            // norte/conector se a info diz que está vazio ou fraco — não com a AWP deles olhando o meio sem smoke
            if (!executed && !tomouMeio && t > 15 && t < 40 && !EXP.Contains("semMeio"))
            {
                var noMeio = mine.FirstOrDefault(a => (a.pos - T_MeioSul.pos).Len < 120 && a.brain.target == null && !a.hasBomb);
                float eMeio = Intel.Esperados(Meio_Norte, 260);
                bool awpOlha = Intel.contacts.Values.Any(c => m.t - c.t < 10 && m.agents[c.id].alive && m.agents[c.id].gunId == "awp" && (c.pos - CT_Janelao.pos).Len < 120)
                               && !m.SmokeNear(SmokeJanelao.to, 60);
                if (noMeio != null && eMeio < 0.7f && !awpOlha && Rola1("meio", 0.7f))
                {
                    tomouMeio = true;
                    var antes = noMeio.brain.order;
                    noMeio.brain.SetOrder(new Order { name = "tomando o meio", hold = T_Conector, why = $"meio fraco ({eMeio:0.0}): dominar corta a rotação deles", until = t + 20, then = antes });
                    Call($"{noMeio.name}: toma o meio (devem ter {eMeio:0.0} lá)");
                }
            }
            // execute/fake: acabou o controle de mapa, junta nas posições do execute (12 s antes de entrar)
            if (encenarEm > 0 && !executed && t >= encenarEm)
            {   // o lurker continua do outro lado (info, pegar rotação): não atravessa o mapa pra posição do execute
                encenarEm = -1; var lk = Funcao(Role.Lurker); var lkOrdem = lk.brain.order;
                Posicionar(plan);
                if (lk.alive && !lk.hasBomb) lk.brain.SetOrder(lkOrdem);
                Call($"hora de juntar: {plan}");
                PegarC4Cedo(mine, plan.Contains("A") && !plan.StartsWith("fake") ? 'A' : 'B');
            }
            // info do time manda: viu 2+ CT empilhados num bomb no default → executa o outro agora, sem esperar a hora
            if (!executed && (plan.StartsWith("default") || UsaBanco && estr.Tipo == "default") && t > 12)
            {
                float a10 = Intel.Esperados(SITES_A, 330), b10 = Intel.Esperados(SITES_B, 330);
                char outro = a10 >= 2.5f && b10 < 1 ? 'B' : b10 >= 2.5f && a10 < 1 ? 'A' : ' ';
                string porque = outro != ' ' ? $"devem ter {(outro == 'B' ? a10 : b10):0.0} no {(outro == 'B' ? 'A' : 'B')} e {(outro == 'B' ? b10 : a10):0.0} no {outro}" : null;
                // alguém limpou um bomb (lurk, info): deve estar vazio → vai nele. Às vezes é armadilha: nem sempre confia
                if (outro == ' ' && t > 16 && !EXP.Contains("semVazio"))
                {
                    float eA5 = Intel.Esperados(SITES_A, 520), eB5 = Intel.Esperados(SITES_B, 520);
                    char vz = eB5 < 0.3f && eA5 >= 1 ? 'B' : eA5 < 0.3f && eB5 >= 1 ? 'A' : ' ';
                    if (vz != ' ')
                    {
                        // só troca se faz sentido: se a gente já domina o outro (2+ nossos lá e eles fracos), fica nele
                        char ot = vz == 'A' ? 'B' : 'A'; V2 cOt = ot == 'A' ? SITES_A : SITES_B;
                        int nossos = mine.Count(a => (a.pos - cOt).Len < 520);
                        if (nossos >= 2 && (ot == 'A' ? eA5 : eB5) < nossos) { if (Rola1("dominado" + ot, 0.7f)) { outro = ot; porque = $"o {vz} tá limpo, mas a gente já domina o {ot} ({nossos} lá)"; } }
                        else if (Rola1("vazio" + vz, 0.65f)) { outro = vz; porque = $"o {vz} tá limpo (ninguém no caminho) e o outro tem {(vz == 'A' ? eB5 : eA5):0.0}"; }
                    }
                }
                // a info escolhe o bomb, mas ninguém sai correndo: continua tomando o mapa e entra na hora (um pouco antes)
                // IGL decide uma vez e no máximo muda de ideia uma: trocar a cada mudança de estimativa é o time andando à toa
                if (outro != ' ' && siteLido != outro && (EXP.Contains("leituraVelha") || siteLido == ' ' || trocasLeitura < 1 && t - siteLidoT > 12))
                {
                    if (siteLido != ' ') { trocasLeitura++; m.Ev("IGL TR mudou de bomb lido/round"); m.sus.LeituraMudou(outro); }
                    siteLido = outro; siteLidoT = t;
                    Call($"{porque}: vamos {outro} — segura o mapa e entra na hora");
                    PegarC4Cedo(mine, outro);
                }
            }
            // vantagem: matamos gente e um bomb já é nosso (a gente lá dentro/perto e eles sem ninguém lá) — não fica
            // esperando a hora do plano: entra e planta agora (o tempo é deles quando a gente espera)
            if (!executed && t > 10 && !EXP.Contains("semVantagem"))
            {
                int nos = mine.Count, eles = m.Alive(Other);
                (char s, float sc, int perto, float e) best = (' ', float.NegativeInfinity, 0, 0);
                foreach (char s in new[] { 'A', 'B' })
                {
                    V2 c = s == 'A' ? SITES_A : SITES_B;
                    float e = Intel.Esperados(c, 450);
                    int perto = mine.Count(a => (a.pos - c).Len < 420);
                    // levar o C4 atravessando o mapa custa tempo e o C4 pode cair no caminho: onde o C4 já está pesa muito
                    float sc = perto - 2 * e - NavGrid.PathLen(carrier?.pos ?? (m.bombDropped ? m.droppedBomb : mine[0].pos), Plant(s)) / (EXP.Contains("c4LongeVelho") ? 600 : 260);
                    if (sc > best.sc) best = (s, sc, perto, e);
                }
                bool vantagem = nos - eles >= 2 || eles <= 1 && nos >= 2;
                // dominado = gente nossa DENTRO do bomb e eles sem ninguém lá (não é só estar perto controlando o mapa)
                bool dominado = best.perto >= 2 && best.e < 0.35f && t > 25 && mine.Any(a => CSM.Map.MapData.SITES[best.s.ToString()].Contains(a.pos.x, a.pos.y));
                if (best.s != ' ' && best.e < 0.8f && best.perto >= 1 && (vantagem || dominado) && Rola1("vantagem" + nos + "x" + eles, vantagem ? 0.85f : 0.5f))
                {
                    executed = true; m.planSite = best.s; m.Ev(vantagem ? "TR entrou na hora: vantagem de jogadores/round" : "TR entrou na hora: bomb dominado/round");
                    Call(vantagem ? $"somos {nos} contra {eles} e o {best.s} é nosso ({best.perto} lá, devem ter {best.e:0.0}): entra e planta agora"
                                  : $"o {best.s} tá dominado ({best.perto} nossos lá, devem ter {best.e:0.0}): entra e planta agora");
                    Execute(best.s, mine);
                }
                // o C4 já está DENTRO de um bomb que parece vazio (chegou trocando, girou, foi levar pro time e passou por
                // lá): sair andando com ele é jogar o round fora. Leitura, não regra: quanto mais vazio e mais tempo
                // passa ali, mais planta; ouvindo/vendo gente lá, ou com o time longe e eles muitos, segura
                else if (carrier != null && carrier.brain.target == null && t > 15 && !EXP.Contains("semC4NoBomb"))
                {
                    char s = MapData.SITES["A"].Contains(carrier.pos.x, carrier.pos.y) ? 'A' : MapData.SITES["B"].Contains(carrier.pos.x, carrier.pos.y) ? 'B' : ' ';
                    if (s != ' ')
                    {
                        V2 c = s == 'A' ? SITES_A : SITES_B;
                        float e = Intel.Esperados(c, 450);
                        bool visto = Intel.contacts.Values.Any(k => m.t - k.t < 3 && m.agents[k.id].alive && (k.pos - c).Len < 450);
                        float p = MathX.Clamp(0.8f - 0.7f * e, 0, 0.85f) * (nos >= 2 ? 1f : 0.75f) * (eles - nos >= 3 ? 0.6f : 1f);
                        if (!visto && e < 1f && Rola1("c4NoBomb" + s + (int)(t / 5), p))
                        {
                            executed = true; m.planSite = s; m.Ev("TR plantou onde o C4 já estava/round");
                            Call($"{carrier.name} tá com o C4 dentro do {s} e lá parece vazio (devem ter {e:0.0}): planta aí, o resto vem pro {s}");
                            Execute(s, mine);
                        }
                    }
                }
            }
            // indo pro outro bomb, mas o C4 já está no bomb que ficou limpo (matamos quem estava lá): planta onde o C4 está
            // em vez de atravessar o mapa com ele. Leitura, não regra: às vezes o time segue o plano
            if (executed && !m.bombPlanted && carrier != null && t > proxOportunidade && !EXP.Contains("semOportunidade"))
            {
                proxOportunidade = t + 1;
                char aqui = m.planSite, outro = aqui == 'A' ? 'B' : 'A';
                float ate = NavGrid.PathLen(carrier.pos, Plant(outro)), plano = NavGrid.PathLen(carrier.pos, Plant(aqui));
                V2 cO = outro == 'A' ? SITES_A : SITES_B;
                bool visto = Intel.contacts.Values.Any(c => m.t - c.t < 4 && m.agents[c.id].alive && (c.pos - cO).Len < 450);
                float eO = Intel.Esperados(cO, 450), eA = Intel.Esperados(aqui == 'A' ? SITES_A : SITES_B, 450);
                if (ate > 0 && ate < 380 && plano > 650 && !visto && eO < 0.6f + 0.3f * eA && Rola1("oportunidade" + outro + (int)(t / 8), 0.75f))
                {
                    m.Ev("TR mudou pro bomb onde o C4 estava/round");
                    Call($"o C4 já tá no {outro} e lá limpou (devem ter {eO:0.0}) — planta no {outro}, não atravessa o mapa");
                    m.planSite = outro; Execute(outro, mine);
                }
            }
            // o relógio manda: se o que sobra mal dá pra levar o C4 até o bomb (na velocidade de verdade, com parada e
            // quina — ~75% da máxima), plantar e brigar, entra agora no bomb mais perto/mais vazio
            if (!executed && !EXP.Contains("semRelogio"))
            {
                V2 de = carrier?.pos ?? (m.bombDropped ? m.droppedBomb : mine[0].pos);
                float la = NavGrid.PathLen(de, Plant('A')) - 300 * Intel.Esperados(SITES_A, 450), lb = NavGrid.PathLen(de, Plant('B')) - 300 * Intel.Esperados(SITES_B, 450);
                char sj = siteLido != ' ' ? siteLido : la <= lb ? 'A' : 'B';
                float precisa = NavGrid.PathLen(de, Plant(sj)) / (MapCheck.RUN * 0.75f) + Match.PLANT + 20;
                if (m.RoundLeft < precisa)
                {
                    executed = true; m.planSite = sj; m.Ev("TR entrou pelo relógio/round");
                    Call($"sobra {m.RoundLeft:0} s e o C4 leva ~{precisa - 20:0} s pra chegar e plantar: vamos {sj} agora");
                    Execute(sj, mine);
                }
            }
            float hora = siteLido != ' ' ? 0.85f : 1f;   // com info do bomb, entra um pouco antes
            if (!executed && (plan == "default + split" && t > P.tExecDefault * hora || plan == "default lento" && t > MathF.Min(P.tExecDefault + 18, 82) * hora || plan == "execute A" && t > P.tExec || plan.EndsWith("B") && !plan.StartsWith("fake") && t > (plan == "rush B" ? 6 : P.tExec + 1) || plan.StartsWith("fake") && t > P.tFake || UsaBanco && t > estrHora * (estr.Tipo == "default" ? hora : 1)))
            {
                executed = true;
                char site;
                if (UsaBanco)
                {   // o bomb do plano; no default, a leitura do meio do round manda (o pro também vai onde tem menos CT)
                    site = estr.Tipo == "default" && siteLido != ' ' ? siteLido : estr.site;
                    Call($"hora do plano: vamos {site}{(site != estr.site ? $" (o plano era {estr.site}, a leitura mudou)" : "")}");
                }
                else if (plan.StartsWith("default") && siteLido != ' ') { site = siteLido; Call($"hora: vamos {site}"); }
                else if (plan.StartsWith("default"))
                {
                    float a = Intel.Esperados(SITES_A, 330), b = Intel.Esperados(SITES_B, 330);   // CT esperados em cada bomb
                    float pref = m.leitura[(int)team]?.intencao.lado ?? 0;   // empate de informação: vai pro lado que o time quer
                    site = a - pref <= b ? 'A' : 'B';
                    Call($"vamos {site}: devem ter {(site == 'A' ? a : b):0.0} CT no {site} e {(site == 'A' ? b : a):0.0} no {(site == 'A' ? 'B' : 'A')}");
                }
                else site = plan.Contains("A") && !plan.StartsWith("fake") ? 'A' : 'B';
                m.planSite = site;
                Execute(site, mine);
                // lurker do plano: no pro ele ficou do outro lado depois da entrada (info, pegar a rotação) → segue a rota dele
                if (UsaBanco && lurkSlot >= 0 && site == estr.site)
                {
                    string outro = estr.site == 'A' ? "B" : "A";
                    var lk = m.agents.FirstOrDefault(a => a.alive && slotDe.TryGetValue(a.id, out int k) && k == lurkSlot);
                    if (lk != null && !lk.hasBomb && estr.TempoEm(lurkSlot, outro, t, t + 15) > 8)
                    { lurkSegue = true; estrUlt.Remove(lk.id); SeguirPlano(t, true, lk); Call($"{lk.name} segue no lurk ({outro})"); }
                }
            }
            // execução travando: perdemos gente na entrada e eles seguem fortes lá, e o outro bomb deve estar vazio → recua
            // e vai no outro (se dá tempo de atravessar)
            if (executed && !m.bombPlanted && !pivotou && mine.Count >= 2 && m.RoundLeft > 40 && t - execT > 4 && !EXP.Contains("semPivo"))
            {
                char aqui = m.planSite, outro = aqui == 'A' ? 'B' : 'A';
                float eAqui = Intel.Esperados(aqui == 'A' ? SITES_A : SITES_B, 420), eLa = Intel.Esperados(outro == 'A' ? SITES_A : SITES_B, 420);
                int perdi = execVivos - mine.Count;
                V2 cAqui = aqui == 'A' ? SITES_A : SITES_B;
                int vistos = Intel.contacts.Values.Count(x => m.t - x.t < 5 && m.agents[x.id].alive && (x.pos - cAqui).Len < 520);
                bool travou = EXP.Contains("pivoVelho") ? perdi >= 1 && eAqui >= mine.Count * 0.8f || eAqui >= mine.Count + 1
                                                        : perdi >= 1 && eAqui >= mine.Count * 0.8f || vistos >= mine.Count;   // sem perder ninguém, só larga o bomb pelo que viu
                // já tem gente nossa dentro do bomb (ou o bomb já chegou lá): não larga o que conquistou
                var rect = CSM.Map.MapData.SITES[aqui.ToString()];
                if (mine.Count(a => rect.Contains(a.pos.x, a.pos.y)) >= 2 || mine.Any(a => a.hasBomb && rect.Contains(a.pos.x, a.pos.y))) travou = false;
                if (travou && eLa < 0.8f && Rola1("pivo", 0.5f + 0.15f * perdi))
                {
                    pivotou = true; m.planSite = outro;
                    Call($"tá difícil no {aqui} (caímos {perdi}, devem ter {eAqui:0.0} lá) e o {outro} deve estar vazio ({eLa:0.0}): recua e vai {outro}");
                    Execute(outro, mine);
                }
            }
            // execução rolando e o tempo apertando (ou sobrou pouca gente): quem ficou segurando posição vai pro bomb —
            // ninguém fica no meio esperando o tempo acabar
            if (executed && !m.bombPlanted && !reagrupou && (m.RoundLeft < 35 || mine.Count <= 2 && m.RoundLeft < 70))
            {
                reagrupou = true;
                var foram = new List<string>();
                foreach (var a in mine)
                {
                    if (a.guardando || a.hasBomb || a.brain.order.plant || a.brain.order.push) continue;
                    a.brain.SetOrder(new Order { name = $"ir pro {m.planSite}", dest = Plant(m.planSite) + new V2(rng.Uniform(-50, 50), rng.Uniform(-50, 50)), push = true,
                        why = mine.Count <= 2 ? "sobrou pouca gente: vamos pro bomb" : $"{m.RoundLeft:0}s no relógio" });
                    foram.Add(a.name);
                }
                if (foram.Count > 0) Call($"{string.Join(", ", foram)}: larga a posição e vai pro {m.planSite}");
            }
            Guardar(mine);
        }
        bool reagrupou, giroCauteloso, pivotou, vazioCT; int execVivos, trocasLeitura; float execT, siteLidoT, proxOportunidade; char siteLido = ' ';
        readonly HashSet<string> rolados = new HashSet<string>();
        /// <summary>Decide uma vez por round (com chance).</summary>
        bool Rola1(string chave, float p) => rolados.Add(chave) && rng.Rand() < p;
        public bool agressaoCedo;            // o CT fez jogada agressiva cedo neste round (o adversário anota)
        public bool Executou => executed;   // o TR decidiu o bomb neste round
        readonly HashSet<char> recuou = new HashSet<char>();

        static readonly V2 SITES_A = Cell(34.5f, 8), SITES_B = Cell(6, 11);

        /// <summary>Quem pega o C4 guardado: quem desvia menos do próprio caminho pro bomb (o entry e a AWP só se não tiver outro).</summary>
        Agent QuemPegaC4(List<Agent> mine, char site)
        {
            V2 alvo = Plant(site), c4 = m.droppedBomb;
            float Desvio(Agent a) => NavGrid.PathLen(a.pos, c4) + NavGrid.PathLen(c4, alvo) - NavGrid.PathLen(a.pos, alvo)
                                     + (a.role == Role.Entry || a.gunId == "awp" ? 250 : 0) + (a.brain.target != null ? 400 : 0);
            return mine.OrderBy(Desvio).First();
        }

        /// <summary>Bomb decidido antes da hora de entrar: quem passa mais perto do C4 guardado vai buscar e volta pro lugar.</summary>
        void PegarC4Cedo(List<Agent> mine, char site)
        {
            if (!m.bombGuardado || !m.bombDropped || m.bombPlanted || mine.Any(a => a.hasBomb) || EXP.Contains("semLargaC4")) return;
            var q = QuemPegaC4(mine, site); var antes = q.brain.order;
            float ida = NavGrid.PathLen(q.pos, m.droppedBomb) / MathF.Max(q.MaxSpeed, 1);
            q.brain.SetOrder(new Order { name = "pegar o bomb", dest = m.droppedBomb, why = $"vamos {site}: passo perto do C4", until = m.t + ida * 1.2f + 1.5f, then = antes });
            m.c4PegoNaHora++;
            Call($"{q.name} pega o C4 no caminho pro {site}");
        }

        void Execute(char site, List<Agent> mine)
        {
            stage = $"execução {site}"; execVivos = mine.Count; execT = m.t;
            bool c4Atras = !EXP.Contains("c4Frente2");
            // quem abre nunca é o portador: o entry com o C4 cede a frente pro colega mais perto do bomb
            var entry = mine.FirstOrDefault(a => a.role == Role.Entry && !(c4Atras && a.hasBomb));
            if (entry == null && c4Atras) entry = mine.Where(a => !a.hasBomb && a.role != Role.Awper).OrderBy(a => NavGrid.PathLen(a.pos, Plant(site))).FirstOrDefault();
            foreach (var a in mine)
            {
                var o = new Order { name = $"entrar no {site}", dest = Plant(site) + new V2(rng.Uniform(-40, 40), rng.Uniform(-40, 40)), push = true, why = "execução" };
                if (a == entry) { o.name = $"entry no {site}"; o.why = "primeiro a entrar, limpa os cantos"; }
                else if (a.role == Role.Suporte && entry != null) { o.follow = entry; o.name = $"suporte do entry no {site}"; o.why = "trocar o entry"; }
                else if (a.role == Role.Lurker && site == 'A') { o = Go("segurar o flanco", T_Conector, "pegar quem girar pelo meio", quiet: true); }
                else if (a.role == Role.Awper && site == 'B') { o = Go("segurar rotação no meio", T_MeioSul, "AWP corta quem gira", quiet: true); }
                else o.notBefore = m.t + 1.5f;   // espera o entry ir primeiro
                // o C4 não abre o bomb: entra atrás (o entry e o suporte limpam), senão morre na entrada e o bomb cai
                if (a.hasBomb)
                {   // decisão do portador, pela situação: bomb deve estar vazio → entra junto (até primeiro); deve ter CT →
                    // segura um pouco atrás (o entry limpa), mais quem é cauteloso
                    o.plant = true; o.follow = null; o.name += " e plantar";
                    float eSite = Intel.Esperados(site == 'A' ? SITES_A : SITES_B, 380);
                    float pAtras = MathX.Clamp(eSite / 1.5f, 0, 1) * (1.2f - 0.6f * P.Agr(a.skill.aggression));
                    if (c4Atras && mine.Count > 1 && rng.Rand() < (plan.StartsWith("rush") ? 0.6f : 0.88f)) { o.notBefore = MathF.Max(o.notBefore, m.t + rng.Uniform(1.2f, 3f)); o.why = "entro atrás de quem abre, o C4 não pode cair na entrada"; }
                    else if (!EXP.Contains("c4Frente") && rng.Rand() < pAtras)
                    { o.notBefore = MathF.Max(o.notBefore, m.t + rng.Uniform(1.2f, 3f)); o.why = $"devem ter {eSite:0.0} no bomb — o entry limpa, eu entro atrás com o C4"; }
                    else o.why = eSite < 0.5f ? "bomb deve estar vazio — vou direto plantar" : "entro junto com o C4";
                }
                a.brain.SetOrder(o);
            }
            Call($"execução no {site}: entry na frente, suporte troca, bomb atrás");
        }

        void TickCT(List<Agent> mine)
        {
            float t = m.t;
            // setup do banco: cada um segue a rota do jogador dele no pro até ~40 s (depois do contato o giro é do IGL)
            if (UsaBanco && t < 40 && !m.bombPlanted) SeguirPlano(t);
            Flanquear(mine);
            // o plant se ouve: quem chega antes de terminar vai cortar, é a última chance de não ter retake
            if (m.plantando != null && m.plantando.alive && !m.bombPlanted && !EXP.Contains("semCortar"))
            {
                float resta = m.plantandoDesde + Match.PLANT - t;
                foreach (var a in mine)
                {
                    if (a.guardando || a.brain.target != null || a.brain.order.name == "cortar o plant") continue;
                    float eta = NavGrid.PathLen(a.pos, m.plantando.pos) / MathF.Max(a.MaxSpeed, 1);
                    // situação, não regra: sozinho o retake quase não sai (7% real com 1 CT) → cortar vale mais; vários
                    // deles em volta do plantador → ir é morrer; inteiro vai mais que machucado
                    float cobrem = MathF.Max(0, Intel.Esperados(m.plantando.pos, 300) - 1);
                    float pc = 0.45f + 0.35f * P.Agr(a.skill.aggression) + (mine.Count == 1 ? 0.25f : 0) - 0.25f * cobrem + (a.hp > 70 ? 0.1f : -0.15f);
                    if (eta > resta + 1f || !Rola1("cortar" + a.id, EXP.Contains("cortarVelho") ? 0.6f + 0.4f * P.Agr(a.skill.aggression) : MathX.Clamp(pc, 0.1f, 0.95f))) continue;
                    var antes = a.brain.order;
                    a.brain.SetOrder(new Order { name = "cortar o plant", dest = NavGrid.Snap(m.plantando.pos), push = true, why = $"ouvi o plant: chego em {eta:0.0} s", until = m.plantandoDesde + Match.PLANT + 1, then = antes });
                    m.Ev("CT foi cortar o plant/round");
                }
            }
            // C4 caído que a gente sabe onde está (viu cair, matou o portador ali): o mais perto segura o ângulo em cima
            // dele — quem vier pegar morre (punir o bomb caído)
            if (m.bombDropped && !m.bombPlanted && !EXP.Contains("semVigiaC4") && t > proxVigiaC4)
            {
                bool sabe = m.c4CaiuComCT || m.agents.Any(c => c.alive && !c.IsT && (c.pos - m.droppedBomb).Len < 520 && MapData.LosWalk(c.pos, c.z, m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f));
                if (sabe && !mine.Any(c => c.brain.order.name == "segurando o C4 caído"))
                {
                    proxVigiaC4 = t + 4;
                    var quem = mine.Where(c => c.brain.target == null && !c.guardando && !c.brain.order.defuse).OrderBy(c => NavGrid.PathLen(c.pos, m.droppedBomb)).FirstOrDefault();
                    if (quem != null)
                    {
                        // um lugar perto (90-200) que vê o C4, de preferência de onde ele já está
                        V2 q = quem.pos;
                        if (!MapData.LosWalk(quem.pos, quem.z, m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f) || (quem.pos - m.droppedBomb).Len > 300)
                            for (int k = 0; k < 20; k++)
                            {
                                V2 c2 = NavGrid.Snap(m.droppedBomb + V2.Ang(k * 0.314f) * rng.Uniform(90, 200));
                                if (NavGrid.Movable(c2) && MapData.LosWalk(c2, NavGrid.ZAt(c2), m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f)) { q = c2; break; }
                            }
                        var antes = quem.brain.order;
                        quem.brain.SetOrder(new Order { name = "segurando o C4 caído", hold = new Hold("C4 caído", NavGrid.Snap(q), m.droppedBomb), why = "quem vier pegar o bomb morre", until = t + 20, then = antes });
                        Call($"C4 caiu em {Region(m.droppedBomb)}: {quem.name} segura ele");
                        // um segundo de outro lado: ele atira e o outro troca (quando dá; ninguém larga o bomb pra isso)
                        var dois = mine.Where(c => c != quem && c.brain.target == null && !c.guardando && !c.brain.order.defuse && c.brain.order.hold?.name != "C4 caído" && NavGrid.PathLen(c.pos, m.droppedBomb) < 520).OrderBy(c => NavGrid.PathLen(c.pos, m.droppedBomb)).FirstOrDefault();
                        if (dois != null && !EXP.Contains("c4UmSo") && rng.Rand() < 0.6f)
                        {
                            V2 p2 = dois.pos;
                            if (!MapData.LosWalk(dois.pos, dois.z, m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f))
                                for (int k = 0; k < 20; k++)
                                {
                                    V2 c2 = NavGrid.Snap(m.droppedBomb + V2.Ang(k * 0.314f) * rng.Uniform(120, 240));
                                    if (NavGrid.Movable(c2) && (c2 - q).Len > 120 && MapData.LosWalk(c2, NavGrid.ZAt(c2), m.droppedBomb, NavGrid.ZAt(m.droppedBomb), 18f)) { p2 = c2; break; }
                                }
                            var ant2 = dois.brain.order;
                            dois.brain.SetOrder(new Order { name = "cobrindo o C4 caído", hold = new Hold("C4 caído", NavGrid.Snap(p2), m.droppedBomb), why = $"cruzo com o {quem.name}", until = t + 20, then = ant2 });
                        }
                    }
                }
            }
            if (m.bombPlanted)
            {
                char s = m.planSite;
                Guardar(mine);
                mine = mine.Where(a => !a.guardando).ToList();
                if (mine.Count == 0) return;
                // quanto tempo o melhor defuser precisa (andar + 5 s com kit / 10 s sem)
                float Precisa(Agent a) => Match.DefuseTime(a) + (a.pos - m.bombPos).Len / 55f;
                float need = mine.Min(Precisa);
                if (!retakeSet)
                {
                    retakeSet = true; stage = $"retomada {s}";
                    var st = RetakeStage(s); entradaRetake.Clear();
                    // cada um na entrada mais perto DELE (quem vem de lados diferentes entra de lados diferentes; quem vem
                    // junto entra junto pra trocar). Com folga de 2+ o time força a divisão: pressão por vários lados e o
                    // pós-plant não segura tudo. Em igualdade, dividir é 1x1 sem troca em cada porta
                    bool divide = mine.Count >= 3 && st.Length > 1 && mine.Count >= m.Alive(Other) + 2 && !EXP.Contains("retakeJunto");
                    var usados = new int[st.Length];
                    foreach (var a in mine.OrderBy(a => st.Min(p => NavGrid.PathLen(a.pos, p))))
                    {
                        int best = 0; float bc = float.MaxValue;
                        for (int k = 0; k < st.Length; k++)
                        {
                            float c = NavGrid.PathLen(a.pos, st[k]) + (divide ? 220f * usados[k] : 0) + rng.Uniform(0, 40);
                            var lr = m.leitura[(int)team];   // porta que já deu errado contra esse time custa mais
                            if (lr != null && !EXP.Contains("semAdaptaPos")) c += 260f * MathF.Max(0, 1 - lr.Peso($"porta:{s}{k}"));
                            if (c < bc) { bc = c; best = k; }
                        }
                        usados[best]++; entradaRetake[a.id] = best;
                        a.brain.SetOrder(new Order { name = $"juntar pra retomar o {s} ({Region(st[best])})", dest = st[best], why = divide ? "cada um por uma entrada, entra na mesma hora" : "entrar junto" });
                    }
                    int portas = usados.Count(x => x > 0); portasRetake = portas;
                    int maior = 0; for (int k = 1; k < usados.Length; k++) if (usados[k] > usados[maior]) maior = k;
                    portaRetakeNome = $"porta:{s}{maior}";
                    Call(portas > 1 ? $"bomb no {s}: retake por {portas} lados — {string.Join(", ", Enumerable.Range(0, st.Length).Where(k => usados[k] > 0).Select(k => $"{usados[k]} por {Region(st[k])}"))}, entra junto no meu call"
                                    : $"bomb no {s}: junta em {Region(st[0])} e entra junto");
                    if (!EXP.Contains("semRetakeUtil")) DarGranadas(mine, RetakeSmokes(s), "smoke");
                    retakeAt = t + 6;
                }
                // entra quando juntou ou o tempo aperta
                bool juntou = false;
                if (retakeSet && !retakeGo)
                {   // entra quando todo mundo chegou; só larga quem está longe se o relógio não deixa esperar
                    float falta = 0; var st = RetakeStage(s);
                    foreach (var a in mine) { float eta = st.Min(p => NavGrid.PathLen(a.pos, p)) / MathF.Max(1, a.MaxSpeed); if (eta < 9) falta = MathF.Max(falta, eta); }   // só espera quem chega logo
                    juntou = falta < 1.2f;
                    if (EXP.Contains("retakeVelho")) juntou = t > retakeAt || mine.Count(a => st.Any(p => (a.pos - p).Len < 120)) >= Math.Min(2, mine.Count);
                }
                if (retakeSet && !retakeGo && (juntou || m.BombLeft < need + (EXP.Contains("retakeVelho") ? 4 : 13)))
                {
                    retakeGo = true; vaiRetake.Clear(); if (juntou) m.Ev1("retake entrou junto % rounds");
                    var closest = mine.OrderBy(Precisa).First();   // o que termina o defuse antes (kit pesa: 5 s x 10 s)
                    // chegar junto de lados diferentes: quem está mais perto do bomb segura um pouco (sem estourar o tempo)
                    float Eta(Agent a) => NavGrid.PathLen(a.pos, m.bombPos) / MathF.Max(1, a.MaxSpeed);
                    float folga = MathF.Max(0, m.BombLeft - need - 2);
                    float maxEta = mine.Max(Eta);
                    foreach (var a in mine)
                    {
                        float espera = EXP.Contains("retakeJunto") ? 0 : MathX.Clamp(maxEta - Eta(a) - 0.5f, 0, MathF.Min(4f, folga));
                        vaiRetake[a.id] = t + espera * rng.Uniform(0.7f, 1f) + (a == closest ? MathF.Min(0.7f, folga) : 0);   // quem desarma não entra primeiro
                        if (espera > 0.3f) a.brain.Think($"segurando {espera:0.0}s pra entrar junto com quem vem de longe");
                    }
                    retakeDefuser = closest;
                    Call("entra! cada um limpa o seu lado, quem estiver perto desarma");
                    if (!EXP.Contains("semRetakeUtil"))
                    {
                        if (EXP.Contains("retakeVelho")) DarGranadas(mine.Where(a => a != closest).ToList(), RetakeFlashes(s), "flash");
                        else if (UtilRetake(mine, closest, s) && folga > 1.5f) foreach (var a in mine) vaiRetake[a.id] += 1f;   // entra com a granada estourando
                    }
                }
                if (retakeGo)
                    foreach (var a in mine)
                    {
                        if (!vaiRetake.TryGetValue(a.id, out float em) || t < em) continue;
                        vaiRetake.Remove(a.id);
                        if (a == retakeDefuser && retakeDefuser.alive)
                        { a.brain.SetOrder(new Order { name = "retomar e desarmar", dest = m.bombPos, defuse = true, push = true, why = a.hasKit ? "tenho kit" : "mais perto do bomb" }); continue; }
                        // limpa o lado do bomb que fica pra entrada dele (não um ponto qualquer em volta do C4)
                        V2 de = entradaRetake.TryGetValue(a.id, out int ek) ? RetakeStage(s)[ek] : a.pos;
                        V2 dir = de - m.bombPos; float dl = MathF.Max(1, dir.Len);
                        V2 alvo = NavGrid.Snap(m.bombPos + dir / dl * rng.Uniform(35, 80) + new V2(rng.Uniform(-25, 25), rng.Uniform(-25, 25)));
                        a.brain.SetOrder(new Order { name = $"retomar limpando ({Region(de)})", dest = alvo, push = true, calmo = !EXP.Contains("semCalmo"), why = "cobrir o defuse do meu lado" });
                    }
                // quem ia desarmar morreu: o mais perto assume (senão o bomb explode com CT vivo do lado)
                if (retakeGo && !mine.Any(a => a.brain.order.defuse) && mine.Any(a => Precisa(a) < m.BombLeft)
                    && !(retakeDefuser != null && retakeDefuser.alive && vaiRetake.ContainsKey(retakeDefuser.id)))   // o do defuse ainda tá segurando pra entrar junto
                {
                    var c = mine.OrderBy(Precisa).First();
                    c.brain.SetOrder(new Order { name = "desarmar", dest = m.bombPos, defuse = true, push = m.Alive(Team.T) > 0, why = m.Alive(Team.T) == 0 ? "não sobrou TR" : "o do defuse caiu" });
                    Call($"{c.name} desarma");
                }
                return;
            }
            // execução chegando com muita gente num bomb defendido por pouco CT: o âncora sozinho contra 3 morre e entrega
            // o bomb com vantagem pro TR. Pro atrasa e recua pra jogar a retomada com a rotação (alguns seguram até o fim)
            foreach (char site in new[] { 'A', 'B' })
            {
                if (recuou.Contains(site)) continue;
                V2 centro = site == 'A' ? SITES_A : SITES_B;
                float vem = Intel.Esperados(centro, 420);
                var meus = mine.Where(a => (a.pos - centro).Len < 520 && a.brain.target == null && !a.guardando && a.role != Role.Awper && !a.brain.Malotando).ToList();
                if (vem < 2.5f || meus.Count == 0 || vem < meus.Count * 2f) continue;
                recuou.Add(site);
                var st = RetakeStage(site); var foram = new List<string>();
                foreach (var a in meus)
                {
                    if (rng.Rand() > 0.75f - 0.45f * P.Agr(a.skill.aggression)) { a.brain.Think($"vêm {vem:0.#} no {site} — vou segurar até o fim"); continue; }
                    a.brain.SetOrder(Go("recuar e jogar pelo retake", new Hold("retomada do " + site, NavGrid.Snap(st[rng.Int(st.Length)]), centro), $"devem vir {vem:0.#} no {site} e eu tô sozinho: atraso e espero a rotação"));
                    foram.Add(a.name);
                }
                if (foram.Count > 0) Call($"{string.Join(", ", foram)}: recua no {site}, joga pelo retake");
            }
            // info de vazio: alguém olhou e o bomb (com o caminho até ele) tá limpo → o TR deve estar do outro lado: um que
            // não é âncora joga pro outro bomb antes do ataque chegar (o âncora fica: pode ser lurk)
            if (!vazioCT && t > 20 && m.Alive(Other) >= 3 && !EXP.Contains("semVazio"))
                foreach (char s in new[] { 'A', 'B' })
                {
                    char o2 = s == 'A' ? 'B' : 'A';
                    float eS = Intel.Esperados(s == 'A' ? SITES_A : SITES_B, 520), eO = Intel.Esperados(o2 == 'A' ? SITES_A : SITES_B, 520);
                    if (eS >= 0.3f || eO < 1.2f) continue;
                    vazioCT = true;
                    var quem = mine.Where(a => a.brain.homeSite == s && a.role != Role.Ancora && a.role != Role.Awper && !a.guardando && a.brain.target == null && !a.brain.Malotando)
                                   .OrderBy(a => NavGrid.PathLen(a.pos, o2 == 'A' ? SITES_A : SITES_B)).FirstOrDefault();
                    if (quem == null || !Rola1("vazioCT", 0.7f)) break;
                    var livres = Livres(o2, mine);
                    if (livres.Count == 0) break;
                    var h = livres.OrderBy(x => NavGrid.PathLen(quem.pos, x.pos)).First();
                    quem.brain.SetOrder(Go($"jogando pro {o2}: {h.name}", h, $"o {s} tá limpo — devem estar {eO:0.0} pro {o2}"));
                    Call($"{s} limpo, ninguém no caminho: {quem.name} joga pro {o2}");
                    break;
                }
            // giro: 2+ TR vistos perto de um bomb nos últimos 5 s
            // giro pela estimativa: quantos TR devem estar em cada bomb (visto, ouvido e por onde ninguém pode ter
            // passado) — não precisa ver 3 na cara pra saber que é A
            float eA = Intel.Esperados(SITES_A, 400), eB = Intel.Esperados(SITES_B, 400), giro = P.ctGiro * 0.75f * (giroCauteloso ? 1.35f : 1f);
            // granada do TR caindo no bomb (smoke na entrada, flash por cima, molotov no canto) = execução começando:
            // gira já, não espera ver 3 na cara (a rotação leva 12-16 s; o TR planta em 4-5 s depois de entrar)
            if (!EXP.Contains("semGiroUtil"))
            {
                int uA = m.pousos.Count(p => p.tm == Other && m.t - p.t < 5 && (p.p - SITES_A).Len < 480);
                int uB = m.pousos.Count(p => p.tm == Other && m.t - p.t < 5 && (p.p - SITES_B).Len < 480);
                eA += 0.9f * Math.Min(uA, 3); eB += 0.9f * Math.Min(uB, 3);
            }
            char hot = eA >= giro && eA >= eB + 1 ? 'A' : eB >= giro && eB >= eA + 1 ? 'B' : ' ';
            // lendo o fake: o bomb foi visto do outro lado há pouco → é barulho, não gira
            if (hot != ' ' && hot != rotatedTo && m.t - Intel.bombaT < 8 && !EXP.Contains("semLerFake"))
            {
                V2 cHot = hot == 'A' ? SITES_A : SITES_B, cOutro = hot == 'A' ? SITES_B : SITES_A;
                if ((Intel.bombaPos - cOutro).Len < (Intel.bombaPos - cHot).Len)
                {
                    if (fakeLido != hot) { fakeLido = hot; Call($"barulho no {hot}, mas o bomb tá do outro lado ({Region(Intel.bombaPos)}): fake, ninguém gira"); }
                    hot = ' ';
                }
            }
            // info fraca (pouca gente, sem ver o bomb) contra time que já fez fake na partida: gira um só pra ver
            if (hot != ' ' && hot != rotatedTo && giroCauteloso && hot != giroUm && !EXP.Contains("semLerFake"))
            {
                V2 cHot = hot == 'A' ? SITES_A : SITES_B;
                bool bombaLa = m.t - Intel.bombaT < 8 && (Intel.bombaPos - cHot).Len < 700;
                if (!bombaLa && (hot == 'A' ? eA : eB) < 3.2f)
                {
                    giroUm = hot;
                    var um = mine.Where(a => a.brain.homeSite != hot && a.role != Role.Ancora && !a.guardando)
                                 .OrderBy(a => NavGrid.PathLen(a.pos, cHot)).FirstOrDefault();
                    if (um != null)
                    {
                        var st = RetakeStage(hot);
                        um.brain.SetOrder(new Order { name = $"girando sozinho pro {hot}", dest = st[rng.Int(st.Length)], why = "pode ser fake: vou ver" });
                        Call($"pode ser fake no {hot}: só o {um.name} gira, o resto segura");
                    }
                    hot = ' ';
                }
            }
            // acabou de girar pela estimativa: ela virar pro outro lado não é informação nova, só o que viu desfaz o giro
            // pro gira por informação: alguém viu, ouviu ou caiu granada lá agora. Estimativa sozinha é correr pra cima do TR sem saber
            if (hot != ' ' && hot != rotatedTo && !EXP.Contains("giroPorConta"))
            {
                V2 cI = hot == 'A' ? SITES_A : SITES_B;
                bool info = Intel.contacts.Values.Any(x => m.t - x.t < 8 && m.agents[x.id].alive && (x.pos - cI).Len < 700) || m.pousos.Any(p => p.tm == Other && m.t - p.t < 6 && (p.p - cI).Len < 520);
                if (!info) { hot = ' '; m.Ev("giro sem info evitado (s)/round"); }
            }
            if (hot != ' ' && rotatedTo != ' ' && hot != rotatedTo && m.t - giroT < (EXP.Contains("giro12") ? 12 : 25) && !CertoEm(hot) && !EXP.Contains("giroVai")) hot = ' ';
            if (hot != ' ' && (hot != rotatedTo || !giroCerto && CertoEm(hot)))   // girou pela estimativa e agora VIU: vai o resto
            {
                if (hot != rotatedTo && rotatedTo != ' ') m.Ev("CT desfez o giro/round");
                m.sus.Giro(team, hot, hot != rotatedTo && rotatedTo != ' ' && !CertoEm(hot));
                rotatedTo = hot; giroCerto = CertoEm(hot); giroT = m.t; m.Ev("giros CT/round");
                // bomb ainda nosso (nenhum TR visto dentro dele agora): quem gira vai SEGURAR junto (crossfire com o
                // âncora); já tomado: junta pra retomada. (a molotov/smoke de atrasar é decisão de quem está no bomb)
                var rect = CSM.Map.MapData.SITES[hot.ToString()];
                bool tomado = m.intel[(int)team].contacts.Values.Any(c => m.t - c.t < 3 && m.agents[c.id].alive && rect.Contains(c.pos.x, c.pos.y));
                // quantos giram: VIU (o C4 indo pra lá ou 3+ entrando) = todo mundo; só ESTIMOU = o que falta pra ter um a
                // mais que eles lá, os mais perto primeiro — e alguém sempre fica do outro lado (estimativa engana)
                V2 cH = hot == 'A' ? SITES_A : SITES_B;
                bool certo = giroCerto || EXP.Contains("giroTodos");
                int jaLa = mine.Count(a => a.brain.homeSite == hot);
                var podem = mine.Where(a => a.brain.homeSite != hot && a.role != Role.Ancora && !a.brain.Malotando).OrderBy(a => NavGrid.PathLen(a.pos, cH)).ToList();
                int outros = mine.Count(a => a.brain.homeSite != hot);
                int vao = certo ? podem.Count : Math.Min(podem.Count, Math.Max(1, (int)MathF.Ceiling(hot == 'A' ? eA : eB) + 1 - jaLa));
                if (!certo) vao = Math.Min(vao, outros - 1);   // um fica do outro lado
                var giram = new HashSet<Agent>(podem.Take(Math.Max(0, vao)));
                foreach (var a in podem) if (!giram.Contains(a)) a.brain.Think($"giro pro {hot} pela estimativa — fico deste lado, pode ser fake");
                // call só do que acontece: ninguém pra girar (sozinho, ou quem está lá já basta) não vira "giram 0"
                float eHot = hot == 'A' ? eA : eB;
                if (giram.Count > 0)
                    Call($"devem ter {eHot:0.0} no {hot} (no outro {(hot == 'A' ? eB : eA):0.0}): {(certo && giram.Count == podem.Count ? "todo mundo gira" : $"{string.Join(", ", giram.Select(g => g.name))} gira{(giram.Count > 1 ? "m" : "")}")}{(tomado ? " pro retake" : " e segura junto")}");
                else if (mine.Count > 1 && jaLa > 0)
                    Call($"devem ter {eHot:0.0} no {hot}: {jaLa} nosso{(jaLa > 1 ? "s" : "")} lá segura{(jaLa > 1 ? "m" : "")}, o resto fica (pode ser fake)");
                var livres = Livres(hot, mine);
                foreach (var a in mine)
                {
                    if (a.brain.homeSite == hot) continue;
                    if (!giram.Contains(a)) continue;               // âncora do outro bomb fica (pode ser fake), e quem não precisa ir
                    // saindo do bomb: deixa uma granada pro âncora que fica (não leva tudo embora)
                    var fica = mine.FirstOrDefault(x => x.role == Role.Ancora && x.brain.homeSite == a.brain.homeSite && (x.pos - a.pos).Len < 300);
                    if (fica != null && !EXP.Contains("semDrop"))
                        foreach (var nd in new[] { "molotov", "smoke", "flash" })
                            if (a.nades.Contains(nd) && Match.CabeGranada(fica, nd) && Rola1("drop giro " + a.id, 0.4f)) { m.DropaGranada(a, fica, nd); Call($"{a.name} girando: dropou {nd} pro {fica.name}"); break; }
                    if (!tomado && livres.Count > 0 && !EXP.Contains("giroRetake"))
                    {
                        var h = livres.OrderBy(x => NavGrid.PathLen(a.pos, x.pos)).First(); livres.Remove(h);
                        a.brain.SetOrder(Go($"girando pro {hot}: {h.name}", h, "contato no " + hot + " — segurar junto"));
                        continue;
                    }
                    var st = RetakeStage(hot);
                    a.brain.SetOrder(new Order { name = $"girando pro {hot}", dest = st[rng.Int(st.Length)], why = "contato no " + hot });
                }
            }
            // âncora do outro bomb segura o dele (pode ser fake)... mas viu o bomb do outro lado, ou quase o time todo
            // lá: sem o C4 ninguém planta aqui. Gira — a não ser que prefira ficar (guardando, ou ainda tem gente aqui)
            foreach (var a in mine)
            {
                if (a.role != Role.Ancora || ancoraDecidiu.Contains(a.id) || a.guardando || a.brain.target != null) continue;
                char outro = a.brain.homeSite == 'A' ? 'B' : 'A';
                V2 cOutro = outro == 'A' ? SITES_A : SITES_B, cMeu = outro == 'A' ? SITES_B : SITES_A;
                // tem que VER (não estimar): o C4 chegando no outro bomb, ou 3+ deles vistos agora entrando lá — TR fazendo
                // default perto do outro lado não é ataque (e o âncora que sai entrega o bomb dele de graça)
                bool bomba = m.t - Intel.bombaT < 4 && (Intel.bombaPos - cOutro).Len < 520;
                int vistos = m.intel[(int)team].contacts.Values.Count(c => m.t - c.t < 5 && m.agents[c.id].alive && (c.pos - cOutro).Len < 450);
                float eM = Intel.Esperados(cMeu, 420), eO = vistos;
                bool stack = vistos >= Math.Min(3, m.Alive(Other));
                if (!bomba && !stack || EXP.Contains("ancoraFica")) continue;
                ancoraDecidiu.Add(a.id);
                string porque = bomba ? $"vi o bomb no {outro}" : $"vi {vistos} entrando no {outro}";
                float fica = (bomba ? 0.1f : 0.3f) + (eM >= 0.8f ? 0.35f : 0) - 0.1f * P.Agr(a.skill.aggression);
                if (rng.Rand() < fica) { a.brain.Think($"{porque}, mas fico{(eM >= 0.8f ? " — ainda tem gente aqui" : " de olho no flanco")}"); continue; }
                var rect = CSM.Map.MapData.SITES[outro.ToString()];
                bool tomado = m.intel[(int)team].contacts.Values.Any(c => m.t - c.t < 3 && m.agents[c.id].alive && rect.Contains(c.pos.x, c.pos.y));
                var livres = Livres(outro, mine);
                if (!tomado && livres.Count > 0)
                {
                    var h = livres.OrderBy(x => NavGrid.PathLen(a.pos, x.pos)).First();
                    a.brain.SetOrder(Go($"âncora girando pro {outro}: {h.name}", h, porque));
                }
                else
                {
                    var st = RetakeStage(outro);
                    a.brain.SetOrder(new Order { name = $"âncora girando pro retake do {outro}", dest = st[rng.Int(st.Length)], why = porque });
                }
                Call($"{a.name}: {porque}, girando");
            }
        }
        /// <summary>
        /// Vários contra um que a gente sabe onde está: ninguém fica parado esperando ele — um vai por trás (um ponto do
        /// outro lado dele, fora da vista dele) e o resto segura ele de onde está. TR com o bomb plantado não precisa (o
        /// tempo é dele); CT sem tempo de retomada vai direto.
        /// </summary>
        void Flanquear(List<Agent> mine)
        {
            if (mine.Count < 2 || m.t < proxFlanco || EXP.Contains("semFlanco")) return;
            var eles = m.agents.Where(a => a.alive && a.team == Other).ToList();
            if (eles.Count == 0 || mine.Count < eles.Count + 1) return;   // só com vantagem (em igualdade, quem sai sozinho morre)
            if (team == Team.T && m.bombPlanted) return;
            if (team == Team.CT && m.bombPlanted && m.BombLeft < 18) return;
            V2 cen = mine.Aggregate(new V2(0, 0), (s, a) => s + a.pos) / mine.Count;
            // o inimigo que a gente VIU agora (posição exata), perto do time
            var c = Intel.contacts.Values.Where(x => m.t - x.t < 3 && m.agents[x.id].alive && (x.pos - cen).Len < 650).OrderBy(x => m.t - x.t).FirstOrDefault();
            if (c == null || jaFlanqueado.Contains(c.id)) return;
            var e = m.agents[c.id];
            var podem = mine.Where(a => !a.hasBomb && !a.brain.order.defuse && !a.brain.order.plant && !a.brain.order.push && a.brain.target == null && !a.guardando && !a.brain.Malotando).ToList();
            if (podem.Count == 0) return;
            V2 dir = (c.pos - cen) / MathF.Max(1, (c.pos - cen).Len);
            (Agent a, V2 q, float sc) best = (null, default, float.MaxValue);
            foreach (float r in new[] { 90f, 130f, 180f })
                foreach (float ang in new[] { -1.2f, -0.6f, 0f, 0.6f, 1.2f })
                {
                    V2 d = new V2(dir.x * MathF.Cos(ang) - dir.y * MathF.Sin(ang), dir.x * MathF.Sin(ang) + dir.y * MathF.Cos(ang));
                    V2 q = NavGrid.Snap(c.pos + d * r);
                    if (!NavGrid.Movable(q) || MapData.LosWalk(c.pos, c.z, q, NavGrid.ZAt(q), 18f)) continue;   // tem que ser fora da vista dele
                    foreach (var a in podem)
                    {
                        float pl = NavGrid.PathLen(a.pos, q);
                        if (pl <= 0 || pl > (EXP.Contains("flancoVelho") ? 1400 : 800)) continue;
                        if (pl < best.sc) best = (a, q, pl);
                    }
                }
            if (best.a == null) return;
            jaFlanqueado.Add(c.id); proxFlanco = m.t + 8;
            var f = best.a;
            float chega = m.t + best.sc / f.MaxSpeed + 1.5f;
            var antes = f.brain.order;
            f.brain.SetOrder(new Order { name = $"flanco: pegar o {e.name} por trás", dest = best.q, why = $"sabemos onde o {e.name} está — vou por outro caminho",
                                         until = chega, then = new Order { name = $"em cima do {e.name}", dest = c.pos, push = true, quiet = true, why = "cheguei por trás", until = chega + 6, then = antes } });
            // o resto: um segura ELE, os outros a rota de fuga mais provável dele (ninguém no mesmo pixel)
            V2 obj = e.IsT ? (m.bombPlanted ? m.bombPos : Plant((c.pos - Plant('A')).Len < (c.pos - Plant('B')).Len ? 'A' : 'B')) : MapData.SPAWN_CT[0];
            var rp = NavGrid.Path(c.pos, obj); V2 fuga = c.pos; float acc = 0;
            for (int i = 1; rp != null && i < rp.Count && acc < 130; i++) { acc += (rp[i].p - rp[i - 1].p).Len; fuga = rp[i].p; }
            int k = 0;
            // só quem está no duelo: quem está do outro lado do mapa continua na posição dele, não para pra mirar o inimigo por dentro da parede
            foreach (var x in podem.Where(x => x != f && (EXP.Contains("flancoVelho") || NavGrid.PathLen(x.pos, c.pos) < 550)).OrderBy(x => (x.pos - c.pos).Len))
            {
                bool nele = k++ == 0; var antesX = x.brain.order;
                x.brain.SetOrder(Go(nele ? $"segurando o {e.name}" : $"cobrindo a fuga do {e.name}", new Hold("segurando " + e.name, NavGrid.Snap(x.pos), nele ? c.pos : fuga, nele ? fuga : c.pos),
                                    $"o {f.name} vai por trás dele"));
                x.brain.order.until = m.t + 12; x.brain.order.then = antesX;   // depois volta pro que fazia
            }
            Call($"{f.name} vai por trás do {e.name}; o resto segura ele e a fuga dele");
        }
        float proxFlanco; readonly HashSet<int> jaFlanqueado = new HashSet<int>();

        /// <summary>
        /// Pós-plant: as entradas da retomada (por onde o CT vem) e quem cobre cada uma — o lugar que enxerga a entrada E
        /// o bomb (pega quem vier e quem for desarmar), de longe ou colado conforme o estilo. Cada entrada um dono; se
        /// sobra gente, um fica de olho só no bomb e o resto reforça a entrada mais provável.
        /// </summary>
        Dictionary<Agent, Hold> CoberturaPosPlant(List<Agent> mine, string estilo)
        {
            var res = new Dictionary<Agent, Hold>();
            V2 b = m.bombPos;
            var ents = m.planSite == 'A'
                ? new (string nome, V2 p)[] { ("descida do CT", A_Descida), ("plataforma", CT_A_Plataforma.pos), ("conector", A_Conector) }
                : new (string nome, V2 p)[] { ("viela", B_Viela), ("mercado", Cell(9.5f, 5.5f)) };
            // a mais provável primeiro (onde o CT deve estar vindo)
            ents = ents.OrderByDescending(e => Intel.Esperados(e.p, 300)).ToArray();
            var livres = mine.Where(a => !a.guardando).ToList();
            bool colado = estilo == "colados no bomb";
            foreach (var (nome, ep) in ents)
            {
                if (livres.Count == 0) break;
                // lugares candidatos perto do bomb que veem a entrada e o bomb, longe o bastante da entrada pra não ser
                // atropelado; o dono é quem chega mais rápido
                (Agent a, V2 q, float sc) best = (null, default, float.MaxValue);
                for (int r = 0; r < 24; r++)
                {
                    float dist = colado ? rng.Uniform(30, 90) : rng.Uniform(90, 260);
                    V2 q = NavGrid.Snap(b + V2.Ang(rng.Uniform(0, 6.283f)) * dist);
                    if (!NavGrid.Movable(q) || (q - ep).Len < 110) continue;
                    float zq = NavGrid.ZAt(q);
                    if (!MapData.LosWalk(q, zq, ep, NavGrid.ZAt(ep), 18f) || !MapData.LosWalk(q, zq, b, NavGrid.ZAt(b), 14f)) continue;
                    foreach (var a in livres)
                    {
                        float sc = NavGrid.PathLen(a.pos, q) + 0.4f * Intel.HeatIn(q, 120) * 100;
                        if (sc < best.sc) best = (a, q, sc);
                    }
                }
                if (best.a == null) continue;
                res[best.a] = new Hold($"cobrindo {nome}", best.q, ep, ep, b);   // 2/3 do tempo na entrada, 1/3 no bomb
                livres.Remove(best.a);
            }
            // sobrou gente: um de olho no bomb (defuse), o resto reforça a entrada mais provável
            foreach (var a in livres)
                res[a] = res.Values.Any(h => h.name == "de olho no bomb") || ents.Length == 0
                    ? new Hold($"reforço na {ents[0].nome}", NavGrid.Snap(b + (ents[0].p - b) / MathF.Max(1, (ents[0].p - b).Len) * 70 + V2.Ang(rng.Uniform(0, 6.28f)) * 25), ents[0].p, b)
                    : new Hold("de olho no bomb", NavGrid.Snap(b + V2.Ang(rng.Uniform(0, 6.28f)) * 60), b, ents[0].p);
            return res;
        }

        /// <summary>
        /// Posições de segurar livres num bomb: ninguém do time está nela nem INDO pra ela (dois girando pro mesmo canto
        /// = dois colados no mesmo ângulo e um ângulo aberto). Nem colada numa já tomada.
        /// </summary>
        List<Hold> Livres(char site, List<Agent> mine)
        {
            var todas = site == 'A' ? new[] { CT_A_Canto, CT_A_RuaCT, CT_A_Plataforma, CT_A_Caixa } : new[] { CT_B_Pilar, CT_B_Viela, CT_B_Banca, CT_B_Caixa };
            bool Tomada(Hold h) => mine.Any(o => o.alive && ((o.pos - h.pos).Len < 70 || o.brain.order.hold == h || (o.brain.order.hold != null && (o.brain.order.hold.pos - h.pos).Len < 70)));
            return todas.Where(h => !Tomada(h)).ToList();
        }

        /// <summary>
        /// A entrada do retake: o que cada um guardou sai junto, em cima de onde o TR foi visto (ou de onde ele costuma ficar
        /// no pós-plant). Até 2 flashes de pessoas diferentes, uma molotov e uma HE.
        /// </summary>
        bool UtilRetake(List<Agent> mine, Agent defuser, char s)
        {
            var alvos = Intel.contacts.Values.Where(c => m.t - c.t < 15 && m.agents[c.id].alive && (c.pos - m.bombPos).Len < 450).Select(c => c.pos).ToList();
            alvos.AddRange(PostPlant(s).Select(h => h.pos)); alvos.Add(m.bombPos);
            int fl = 0, mo = 0, he = 0; var feitas = new List<string>();
            foreach (var a in mine.OrderBy(a => a == defuser ? 1 : 0))
            {
                string kind = fl < 2 && a.nades.Contains("flash") ? "flash" : mo < 1 && a.nades.Contains("molotov") ? "molotov" : he < 1 && a.nades.Contains("he") ? "he" : null;
                if (kind == null || a.brain.action != null) continue;
                foreach (var alvo in alvos)
                    if (a.brain.JogaRetake(kind, alvo)) { if (kind == "flash") fl++; else if (kind == "molotov") mo++; else he++; feitas.Add($"{a.name} {kind}"); break; }
            }
            if (feitas.Count > 0) { Call($"entrando com {string.Join(", ", feitas)}"); m.Ev($"granadas juntas no retake/round", feitas.Count); }
            return feitas.Count > 0;
        }

        /// <summary>Lineups da jogada: cada uma vai pra quem tem a granada e está mais perto de onde se joga.</summary>
        void DarGranadas(List<Agent> quem, Lineup[] ls, string kind)
        {
            var livres = quem.Where(a => a.alive && a.nades.Contains(kind)).ToList();
            foreach (var l in ls)
            {
                if (livres.Count == 0 || m.utilUsada.Contains(l.name)) continue;
                var a = livres.OrderBy(x => (x.pos - l.from).Len).First(); livres.Remove(a);
                if ((a.pos - l.from).Len > (kind == "flash" ? 160 : 500)) continue;   // flash da entrada: só quem já está ali
                a.brain.order.nades.Add(l); m.utilUsada.Add(l.name);
                Call($"{a.name}: {l.name}");
            }
        }

        readonly HashSet<int> ancoraDecidiu = new HashSet<int>();
        char fakeLido = ' ', giroUm = ' ';
        float retakeAt; bool retakeGo, defuseResp;
        readonly Dictionary<int, int> entradaRetake = new Dictionary<int, int>();      // por qual entrada cada um retoma
        readonly Dictionary<int, float> vaiRetake = new Dictionary<int, float>();      // quando cada um entra (chegar junto)
        Agent retakeDefuser; public int portasRetake;
        readonly HashSet<string> saveRolado = new HashSet<string>();
        V2? saveSpot; V2 SaveDe;
        /// <summary>Onde o time guarda: o primeiro que guarda escolhe (longe do inimigo esperado, pro lado do spawn), os
        /// outros vão pro mesmo canto (CT costuma guardar junto).</summary>
        V2 SaveSpot(Agent quem = null)
        {
            if (quem != null && !EXP.Contains("saveVelho")) return SaveSpotNovo(quem);
            if (saveSpot != null) return saveSpot.Value + new V2(rng.Uniform(-20, 20), rng.Uniform(-20, 20));
            V2 de = quem?.pos ?? SaveDe;
            // candidatos: o spawn e as posições do meu lado; longe do bomb (se plantado) e de onde o inimigo deve estar —
            // nunca "guardar" andando pro bomb
            var cand = new List<V2>(team == Team.T ? MapData.SPAWN_T : MapData.SPAWN_CT);
            foreach (var h in team == Team.T ? new[] { T_TopoMeio, T_RampaBaixo, T_CasaraoDobra, T_RuaB } : new[] { CT_A_RuaCT, CT_B_Viela, CT_Praca, CT_Janelao }) cand.Add(h.pos);
            V2 best = cand[0]; float bv = float.MinValue;
            foreach (var s in cand)
            {
                float longeBomb = m.bombPlanted ? (s - m.bombPos).Len : 0;
                if (m.bombPlanted && longeBomb < 600) continue;
                float sc = longeBomb - 500 * Intel.Esperados(s, 350);
                // não vai pra onde teve a última informação dos inimigos (nem passa por eles no caminho)
                foreach (var c in Intel.contacts.Values)
                {
                    if (m.t - c.t > 15 || !m.agents[c.id].alive) continue;
                    V2 seg = s - de; float L = seg.Len; if (L < 1) continue;
                    float u = MathX.Clamp((c.pos - de).Dot(seg) / (L * L), 0, 1);
                    if ((de + seg * u - c.pos).Len < 250) sc -= 900;
                    if ((s - c.pos).Len < 400) sc -= 600;
                }
                if (sc > bv) { bv = sc; best = NavGrid.Snap(s); }
            }
            saveSpot = best; return best;
        }

        /// <summary>Esconderijo que deixou de ser: tem inimigo visto perto/com visão dele, ou devem estar chegando.</summary>
        public bool SaveRuim(V2 p, float z)
        {
            if (Intel.Esperados(p, 350) >= 0.6f) return true;
            foreach (var c in Intel.contacts.Values)
            {
                if (m.t - c.t > 8 || !m.agents[c.id].alive) continue;
                if ((p - c.pos).Len < 300 || CSM.Map.MapData.LosWalk(c.pos, c.z, p, z, 18f)) return true;
            }
            return false;
        }

        static List<Spots.Spot> spotsSave;
        /// <summary>Esconderijo: longe do que sabemos deles, fora da rota de quem caça (canto onde o inimigo quase não passa), fora do raio e sem cruzar com eles.</summary>
        public V2 SaveSpotNovo(Agent a)
        {
            spotsSave ??= Spots.All();
            V2 best = a.pos; float bv = float.NegativeInfinity;
            foreach (var s in spotsSave)
            {
                if (s.kind == Spots.Kind.EmCima) continue;
                float ida = NavGrid.PathLen(a.pos, NavGrid.Snap(s.p)); if (ida <= 0 && (s.p - a.pos).Len > 30 || ida > 900 || float.IsInfinity(ida)) continue;
                if (m.bombPlanted && (s.p - m.bombPos).Len < Match.RAIO_BOMB + 80) continue;
                float chega = ida / MathF.Max(a.MaxSpeed, 1); if (m.bombPlanted && chega > m.BombLeft) continue;
                float sc = -ida * 0.25f - 900 * Rotas.Perto(Other, s.p, m.t, 120) - 400 * Intel.Esperados(s.p, 300);
                bool ruim = false;
                foreach (var c in Intel.contacts.Values)
                {
                    if (m.t - c.t > 12 || !m.agents[c.id].alive) continue;
                    if ((s.p - c.pos).Len < 300 || CSM.Map.MapData.LosWalk(c.pos, c.z, s.p, s.z, 18f)) { ruim = true; break; }
                    V2 seg = s.p - a.pos; float L = seg.Len; if (L < 1) continue;
                    float u = MathX.Clamp((c.pos - a.pos).Dot(seg) / (L * L), 0, 1);
                    if ((a.pos + seg * u - c.pos).Len < 220) sc -= 500;
                }
                if (ruim) continue;
                float aberto = 0;   // esconderijo é ponta sem saída: corredor tem parede dos lados, mas é por onde todo mundo passa
                for (int j = 0; j < 8; j++) aberto += CSM.Map.MapData.Raio(s.p, s.z, V2.Ang(j * MathF.PI / 4), 450, 18f);
                sc -= aberto * 0.22f + 500 * Rotas.Perto(team, s.p, m.t, 120);
                if (sc > bv) { bv = sc; best = NavGrid.Snap(s.p); }
            }
            return best;
        }

        /// <summary>
        /// Onde esperar o TR fugindo do bomb: no caminho dele de volta pra base, fora do raio da explosão, num canto que
        /// vê esse caminho.
        /// </summary>
        Hold PontoDeCaca()
        {
            var p = NavGrid.Path(m.bombPos, MapData.SPAWN_T[0]);
            V2 passa = m.bombPos; float acc = 0; V2 prev = m.bombPos;
            foreach (var w in p) { acc += (w.p - prev).Len; prev = w.p; passa = w.p; if (acc > Match.BOMB_SIGMA * 3.2f) break; }
            for (int k = 0; k < 24; k++)
            {
                V2 q = NavGrid.Snap(passa + V2.Ang(rng.Uniform(0, 6.28f)) * rng.Uniform(60, 150));
                if ((q - m.bombPos).Len < Match.BOMB_SIGMA * 2.8f || !NavGrid.Movable(q)) continue;
                if (MapData.LosWalk(q, NavGrid.ZAt(q), passa, NavGrid.ZAt(passa), 18f)) return new Hold("esperando o TR sair do bomb", q, passa);
            }
            return new Hold("esperando o TR sair do bomb", NavGrid.Snap(passa), m.bombPos);
        }
        bool cacaChamada;
        /// <summary>
        /// TR com bomb plantado e o CT desistindo (pouco tempo, ninguém vindo pro bomb): se a economia do TR está boa,
        /// os mais agressivos vão caçar o save do CT (tirar arma = CT quebrado no próximo).
        /// </summary>
        void CacarSave(List<Agent> mine)
        {
            if (cacaChamada || m.BombLeft > 22 || m.defuserActive || mine.Count < 2) return;
            float vindo = Intel.Esperados(m.bombPos, 450);
            if (vindo > 0.6f || m.aperto[(int)team] > 0.4f) return;
            cacaChamada = true;
            var cacadores = mine.Where(a => rng.Rand() < 0.2f + 0.5f * P.Agr(a.skill.aggression)).Take(2).ToList();
            if (cacadores.Count == 0) { Call("CT desistiu do retake: segura o bomb"); return; }
            V2 alvo = MapData.SPAWN_CT[0]; float bv = -1;
            foreach (var s in MapData.SPAWN_CT) { float e = Intel.Esperados(s, 400); if (e > bv) { bv = e; alvo = s; } }
            foreach (var a in cacadores) a.brain.SetOrder(new Order { name = "caçando o save do CT", dest = alvo, push = true, why = "economia boa: tirar arma deles quebra o próximo round" });
            Call($"CT tá guardando: {string.Join(" e ", cacadores.Select(a => a.name))} vão caçar");
        }

        /// <summary>
        /// Guardar a arma: cada jogador, quando a situação fica ruim (em desvantagem sem tempo, sem tempo de desarmar,
        /// clutch impossível...), pesa o quanto a arma dele vale, o quanto o time precisa dela no próximo round (aperto
        /// da economia) e o próprio jeito de jogar. Às vezes guarda, às vezes tenta. Uma decisão por situação.
        /// </summary>
        void Guardar(List<Agent> mine)
        {
            int nos = mine.Count, eles = m.Alive(Other);
            foreach (var a in mine)
            {
                bool semSaida = team == Team.T && !m.bombPlanted && nos <= 2 && eles >= 3 && m.RoundLeft < 27 && !EXP.Contains("saveTRVelho");   // real: 96% dos tempos têm 1-2 TR vivos, 82% com 3+ CT
                if (a.guardando || a.brain.order.defuse || (a.hasBomb && !m.bombPlanted && !semSaida)) continue;
                string ruim = null;
                if (team == Team.T && !m.bombPlanted)
                {
                    if (semSaida) ruim = $"{nos} contra {eles} com {m.RoundLeft:0}s";
                    else if (nos == 1 && eles >= 3 && m.RoundLeft < 45) ruim = $"1 contra {eles}";
                    else if (eles - nos >= 2 && m.RoundLeft < 35) ruim = $"{nos} contra {eles} com pouco tempo";
                    else if (m.RoundLeft < 12 && (a.pos - Plant(m.planSite)).Len > 450) ruim = "não dá tempo de chegar no bomb";
                }
                else if (team == Team.CT && m.bombPlanted)
                {
                    bool alguemDa = mine.Any(x => Match.DefuseTime(x) + (x.pos - m.bombPos).Len / 55f < m.BombLeft - 0.5f);
                    if (!alguemDa) ruim = $"não dá tempo de desarmar ({m.BombLeft:0}s)";
                    else if (nos == 1 && eles >= 3) ruim = $"1 contra {eles} no retake";
                    else if (eles - nos >= 2 && m.BombLeft < 18) ruim = $"{nos} contra {eles} com {m.BombLeft:0}s";
                }
                if (ruim == null || !saveRolado.Add(a.id + ruim)) continue;
                if (m.semAmanha[(int)team]) { a.brain.Think($"{ruim}, mas guardar pra quê? não tem próximo round com essa arma — vou"); continue; }
                float valor = WeaponStats.Forca((a.Primaria ?? a.gunId)) / 10f + ((a.Primaria ?? a.gunId) == "awp" ? 0.4f : 0);
                float p = (0.15f + 0.45f * m.aperto[(int)team] + 0.35f * valor - (WeaponStats.Pistola((a.Primaria ?? a.gunId)) ? 0.45f : 0)) * (1.15f - 0.5f * a.skill.aggression);
                float aperto = m.aperto[(int)team];
                if (ruim.StartsWith("não dá tempo"))
                {
                    // CT sem tempo de desarmar: correr pra guardar, guardar onde está, ou caçar o TR que sai do bomb pra tirar arma
                    float agr = P.Agr(a.skill.aggression);
                    float[] w = { 0.5f, 0.25f + 0.4f * aperto, (0.15f + 0.45f * agr) * (nos >= eles ? 1.4f : 0.6f) * (1 - 0.7f * aperto) * (WeaponStats.Pistola((a.Primaria ?? a.gunId)) ? 1.3f : 1f) };
                    int op = rng.Weighted(w);
                    if (op == 2)
                    {
                        a.brain.SetOrder(new Order { name = "caçando quem sai do bomb", hold = PontoDeCaca(),
                            why = $"{ruim}; o TR vai ter que sair correndo — dá pra tirar arma dele" });
                        Call($"{a.name}: não dá tempo, vai caçar quem sai do bomb");
                        continue;
                    }
                    a.guardando = true; m.Ev(team == Team.CT ? "CT guardou (jogadores)/round" : "TR guardou (jogadores)/round");
                    V2 onde = op == 1 && (a.pos - m.bombPos).Len > Match.RAIO_BOMB + 60 ? a.pos : SaveSpot(a);
                    a.brain.SetOrder(new Order { name = "guardando a arma", dest = onde, quiet = true, why = $"{ruim}; {(op == 1 ? "fico escondido aqui" : "corre pra guardar")}" });
                    Call($"{a.name} guarda a {WeaponStats.ALL[(a.Primaria ?? a.gunId)].name}: {ruim}");
                    continue;
                }
                if (rng.Rand() < p)
                {
                    a.guardando = true; m.Ev(team == Team.CT ? "CT guardou (jogadores)/round" : "TR guardou (jogadores)/round");
                    a.brain.SetOrder(new Order { name = "guardando a arma", dest = SaveSpot(a), quiet = true,
                        why = $"{ruim}; {(aperto > 0.5f ? "o time precisa dessa arma no próximo" : "a arma vale mais que esse round")}" });
                    Call($"{a.name} guarda a {WeaponStats.ALL[(a.Primaria ?? a.gunId)].name}: {ruim}");
                }
                else a.brain.Think($"{ruim}, mas vou tentar");
            }
        }
    }
}
