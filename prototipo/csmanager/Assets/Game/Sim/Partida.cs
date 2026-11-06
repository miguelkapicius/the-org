using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Sim
{
    /// <summary>
    /// Uma partida inteira com as regras do CS2 competitivo: MR12 (quem fizer 13 ganha; 12x12 vai pra prorrogação),
    /// troca de lado no half, prorrogação MR3 (6 rounds com $12.500, quem fizer 4; 3x3 = outra), economia (dinheiro
    /// inicial, vitória, bônus de derrota, abates, plant/defuse, teto de $16.000), compra do time por situação
    /// (pistol, eco, force, full, com drop pro colega sem grana) e a arma/colete/kit/granadas de quem sobrevive seguindo
    /// pro próximo round. A AWP vai pro AWPer.
    /// </summary>
    public class Partida
    {
        // ---- regras (CS2). Os valores marcados "conferir" mudam entre fontes: conferir no jogo.
        public const int START = 800, OT_START = 12500, MAX = 16000;
        public const int WIN = 3250, WIN_BOMB = 3500, PLANT_TIME = 800;
        public static readonly int[] PERDA = { 1400, 1900, 2400, 2900, 3400 };
        public static bool PISTOLAS_EXTRAS => !TeamBrain.EXP.Contains("armasNovasVelho");   // P250/Tec-9/Five-SeveN/Galil/FAMAS (o visual mostra Glock/USP/AK/M4 até ter arte)
        public const int PERDA_INICIO = 1;   // conferir: CS2 começa o half com 1 derrota "na conta" (1ª derrota paga $1.900)
        public static int Preco(string item) => item switch
        {
            "colete" => 650, "capacete" => 350, "kit" => 400, "smoke" => 300, "flash" => 200, "molotovT" => 400, "molotovCT" => 500, "he" => 300,
            _ => WeaponStats.ALL.TryGetValue(item, out var s) ? s.price : 0,
        };

        public class Jogador
        {
            public string nome; public Role roleT, roleCT; public char siteCT = 'M'; public Skill skill = new Skill();
            public int dinheiro; public string arma, pistola; public bool colete, capacete, kit; public List<string> nades = new List<string>();
            public int k, d, hs, dano, fk, rounds, guardou;
            public Role Role(bool t) => t ? roleT : roleCT;
        }
        public class Time { public string nome; public Jogador[] js; public int placar, nivelPerda; public Pesos pesos; public readonly Leitura leitura = new Leitura(); }

        public class Info
        {
            public int n, placarA, placarB; public string ladoA, compraT, compraCT, vencedor, motivo, planoT, planoCT; public bool timeAvenceu;
            public int guardaram, guardadasVivas, gastoT, gastoCT; public Match m;
        }

        public readonly Time A, B; public readonly Rng rng; readonly int seed;
        /// <summary>Diagnóstico da compra cheia por lado [0=TR,1=CT]: rounds, AWP com o AWPer, com primária, capacete, granadas, kits.</summary>
        public static readonly int[,] Diag = new int[2, 6];
        public static string ModoA = "", ModoB = "";   // teste de adaptação: "semAdaptar" ou "previsivel"
        public static bool SemLeitura;   // experimento: partida sem a leitura do adversário
        public int r; public bool acabou; public Time vencedor;
        public readonly List<Info> historico = new List<Info>();
        public readonly List<string> log = new List<string>();
        Time tAtual, ctAtual, ultimo; string compraTAtual, compraCTAtual; int gastoT, gastoCT;

        public Partida(int seed, Pesos pA = null, Pesos pB = null)
        {
            this.seed = seed; rng = new Rng(seed * 17 + 3);
            // time A começa de TR (as funções de CT de cada um), time B começa de CT (as funções de TR de cada um)
            Role[] ctDeA = { Role.Rotacao, Role.Suporte, Role.Ancora, Role.Awper, Role.Ancora }; char[] siteDeA = { 'A', 'B', 'B', 'M', 'A' };
            Role[] tDeB = { Role.Igl, Role.Entry, Role.Awper, Role.Lurker, Role.Suporte };
            A = new Time { nome = "VULCÃO", pesos = pA, js = Match.T_TEAM.Select((x, i) => new Jogador { nome = x.name, roleT = x.role, roleCT = ctDeA[i], siteCT = siteDeA[i], skill = Skill.Estilo(x.role) }).ToArray() };
            B = new Time { nome = "ROCHA", pesos = pB, js = Match.CT_TEAM.Select((x, i) => new Jogador { nome = x.name, roleCT = x.role, siteCT = x.site, roleT = tDeB[i], skill = Skill.Estilo(tDeB[i]) }).ToArray() };
            A.leitura.modo = ModoA; B.leitura.modo = ModoB;
        }

        // ================================================================ lados e placar
        public static bool ATerrorista(int r)
        {
            if (r < 12) return true;
            if (r < 24) return false;
            int k = (r - 24) % 6, ot = (r - 24) / 6;
            bool aCTprimeiro = ot % 2 == 0;   // a prorrogação começa com os lados do fim do tempo anterior
            return k < 3 ? !aCTprimeiro : aCTprimeiro;
        }
        public static int Alvo(int r) => r < 24 ? 13 : 16 + 3 * ((r - 24) / 6);
        static bool UltimoDoHalf(int r) => r == 11 || r == 23 || r >= 24 && ((r - 24) % 6 == 2 || (r - 24) % 6 == 5);

        // ================================================================ round
        /// <summary>Monta o próximo round: lados, reset de dinheiro, compra dos dois times. Devolve o round pra rodar.</summary>
        // drops da compra: a decisão (quem paga pra quem) é daqui; o item nasce com quem pagou e é dropado em campo
        readonly List<(Jogador de, Jogador para, string item, string antes, bool naMao)> entregas = new List<(Jogador, Jogador, string, string, bool)>();
        readonly List<(Jogador j, string arma)> comecaCom = new List<(Jogador, string)>();
        /// <summary>Arma que mudou de dono sem compra (o rifle velho do AWPer pro colega): não aparece como compra na cabeça.</summary>
        readonly List<(Jogador j, string arma)> repasses = new List<(Jogador, string)>();
        public Match Preparar()
        {
            entregas.Clear(); comecaCom.Clear(); repasses.Clear();
            bool aT = ATerrorista(r);
            tAtual = aT ? A : B; ctAtual = aT ? B : A;
            bool trocaLado = r == 12 || r >= 24 && (r - 24) % 6 == 3;
            if (r == 0 || r == 12) foreach (var t in new[] { A, B }) { t.nivelPerda = PERDA_INICIO; foreach (var j in t.js) { j.dinheiro = START; Limpa(j); } }
            else if (r >= 24 && (r - 24) % 6 == 0) foreach (var t in new[] { A, B }) { t.nivelPerda = PERDA_INICIO; foreach (var j in t.js) j.dinheiro = OT_START; }
            if (trocaLado) foreach (var j in A.js.Concat(B.js)) Limpa(j);
            int antesT = tAtual.js.Sum(j => j.dinheiro), antesCT = ctAtual.js.Sum(j => j.dinheiro);
            var antesInv = tAtual.js.Concat(ctAtual.js).ToDictionary(j => j, j => (j.arma, j.colete, j.capacete, j.kit, nades: j.nades.ToList()));
            compraTAtual = Comprar(tAtual, true); compraCTAtual = Comprar(ctAtual, false);
            gastoT = antesT - tAtual.js.Sum(j => j.dinheiro); gastoCT = antesCT - ctAtual.js.Sum(j => j.dinheiro);
            // diagnóstico: o que cada lado leva nos rounds de compra cheia
            foreach (var (tm, t, tipo) in new[] { (tAtual, 0, compraTAtual), (ctAtual, 1, compraCTAtual) })
                if (tipo == "full")
                {
                    Diag[t, 0]++; Diag[t, 1] += tm.js.Count(j => j.Role(t == 0) == Role.Awper && j.arma == "awp");
                    Diag[t, 2] += tm.js.Count(j => TemPrimaria(j)); Diag[t, 3] += tm.js.Count(j => j.capacete); Diag[t, 4] += tm.js.Sum(j => j.nades.Count);
                    Diag[t, 5] += tm.js.Count(j => j.kit);
                }
            var setup = tAtual.js.Select(j => Loadout(j, true)).Concat(ctAtual.js.Select(j => Loadout(j, false))).ToArray();
            for (int i = 0; i < 10; i++)
            {   // validador: arma do outro lado no começo do round (CT com Galil/Tec-9/AK/MAC-10, TR com FAMAS/Five-SeveN/M4/MP9)
                string g = setup[i].gun; bool tr = i < 5;
                bool doOutro = tr ? g == "famas" || g == "fiveseven" || g == "m4" || g == "m4s" || g == "mp9" || g == "usp" : g == "galil" || g == "tec9" || g == "ak" || g == "mac10" || g == "glock";
                if (doOutro) { ev.Add($"ARMA DO OUTRO LADO no começo: {(tr ? "TR" : "CT")} com {g} (round {r + 1})"); Log($"  ARMA DO OUTRO LADO: {setup[i].name} ({(tr ? "TR" : "CT")}) começa com {g}"); }
            }
            if (!TeamBrain.EXP.Contains("dropTeleporte")) EntregasEmCampo(setup);
            var aperto = new float[2]; aperto[(int)Team.T] = Aperto(tAtual, ctAtual); aperto[(int)Team.CT] = Aperto(ctAtual, tAtual);
            var m = new Match(seed * 1000 + r, tAtual.pesos?.Clone(), ctAtual.pesos?.Clone(), setup, new[] { compraTAtual, compraCTAtual }, aperto, SemLeitura ? null : tAtual.leitura, SemLeitura ? null : ctAtual.leitura);
            for (int i = 0; i < 10; i++) m.agents[i].skill = (i < 5 ? tAtual : ctAtual).js[i % 5].skill;
            m.semAmanha[(int)Team.T] = SemAmanha(ctAtual); m.semAmanha[(int)Team.CT] = SemAmanha(tAtual);
            AgendaCompras(m, antesInv);
            // arrependeu da compra (CS2: vende de volta na base, no tempo de compra, pelo preço cheio): quem comprou no
            // impulso antes da call do IGL ("vamos de eco") vende — o dinheiro volta igual, só aparece no visual
            if (!TeamBrain.EXP.Contains("semVenda"))
                for (int i = 0; i < 10; i++)
                {
                    var tm = i < 5 ? tAtual : ctAtual; var j = tm.js[i % 5]; string tipo = i < 5 ? compraTAtual : compraCTAtual;
                    if (tipo == "full" || j.dinheiro < 1250 || setup[i].gun == "mp9" || setup[i].gun == "mac10") continue;
                    if (rng.Rand() > 0.06f + 0.12f * j.skill.aggression) continue;
                    string item = i < 5 ? "mac10" : "mp9";
                    m.vendas.Add((m.agents[i], item, rng.Uniform(1.2f, 6f), false)); m.Ev("compra: vendeu de volta (arrependeu)/round");
                }
            return m;
        }

        /// <summary>
        /// Em vez de o item aparecer no colega, ele nasce com quem pagou e é dropado no começo do round (como no CS2): quem
        /// recebe começa com o que tinha antes; quem paga fica com o item "pra entregar" (ou na mão, na troca da AWP).
        /// </summary>
        void EntregasEmCampo(Match.Loadout[] setup)
        {
            int Idx(Jogador j) { int i = Array.IndexOf(tAtual.js, j); return i >= 0 ? i : 5 + Array.IndexOf(ctAtual.js, j); }
            string Pistola(int i) => i < 5 ? "glock" : "usp";
            foreach (var (j, arma) in comecaCom) { int i = Idx(j); setup[i].gun = arma ?? Pistola(i); }
            foreach (var e in entregas)
            {
                int de = Idx(e.de), pa = Idx(e.para);
                if (WeaponStats.ALL.ContainsKey(e.item))
                {
                    setup[pa].gun = e.antes ?? Pistola(pa);
                    if (e.naMao) setup[de].gun = e.item; else setup[de].entregar.Add((pa, e.item));
                }
                else { setup[pa].nades.Remove(e.item); setup[de].entregar.Add((pa, e.item)); }
            }
        }

        static void Limpa(Jogador j) { j.arma = null; j.pistola = null; j.colete = j.capacete = j.kit = false; j.nades.Clear(); }

        static Match.Loadout Loadout(Jogador j, bool t) => new Match.Loadout
        {
            name = j.nome, role = j.Role(t), site = t ? 'M' : j.siteCT, gun = j.arma ?? (t ? "glock" : "usp"), pistola = j.pistola,
            armor = j.colete, helmet = j.capacete, kit = !t && j.kit, nades = new List<string>(j.nades),
        };

        /// <summary>
        /// Quanto perder as armas agora quebra o próximo round (0..1): fração do time que, perdendo e morrendo, não
        /// compra cheio no próximo. Fim de half ou match point contra = 0 (não tem "próximo" com esse dinheiro).
        /// </summary>
        /// <summary>Guardar não serve pra nada: o dinheiro reseta depois (fim de half, começo da prorrogação) ou perder
        /// este round acaba a partida.</summary>
        bool SemAmanha(Time outro) => UltimoDoHalf(r) || outro.placar == Alvo(r) - 1;

        float Aperto(Time tm, Time outro)
        {
            if (UltimoDoHalf(r) || outro.placar == Alvo(r) - 1) return 0;
            int bonus = PERDA[Math.Min(tm.nivelPerda + 1, 4)];
            return tm.js.Count(j => j.dinheiro + bonus < 4600) / 5f;
        }

        string Rifle(Jogador j, bool t) => t ? "ak" : j.roleCT == Role.Ancora ? "m4" : "m4s";
        bool TemPrimaria(Jogador j) => j.arma != null && !WeaponStats.Pistola(j.arma);
        /// <summary>Primária nova: a pistola comprada que ele tinha na mão (Deagle) vai pro coldre, não some.</summary>
        static void DarPrimaria(Jogador j, string g) { if (j.arma != null && WeaponStats.Pistola(j.arma)) j.pistola = j.arma; j.arma = g; }

        int CustoCheio(Jogador j, bool t)
        {
            int c = TemPrimaria(j) ? 0 : Preco(j.Role(t) == Role.Awper ? "awp" : Rifle(j, t));
            bool velho = TeamBrain.EXP.Contains("compraCTVelha");
            if (!j.colete) c += 650; if (!j.capacete && (t || velho)) c += 350;   // CT compra cheio sem capacete (AK mata de cabeça igual)
            if (!t && !j.kit && velho) c += 400;                                   // kit é de quem sobra dinheiro, não condição pra comprar
            return c + 500;   // o mínimo de granada
        }

        /// <summary>
        /// Compra do time: pistol (1º round do half), full (dá pra quase todo mundo comprar cheio, com drop), force
        /// (fim de half, match point contra, ou não adianta guardar), eco (guardando pra comprar cheio no próximo).
        /// </summary>
        /// <summary>
        /// Agenda as compras de cada um no freeze (Match.compras, t de -FREEZE a 0) pelo que mudou no inventário: arma,
        /// colete, capacete, kit, granadas. Quem pagou pro colega é quem compra. Sem rng (hash), pra não mexer na partida.
        /// </summary>
        void AgendaCompras(Match m, Dictionary<Jogador, (string arma, bool colete, bool capacete, bool kit, List<string> nades)> antes)
        {
            var itens = new Dictionary<Jogador, List<string>>();
            foreach (var kv in antes)
            {
                var j = kv.Key; var a = kv.Value; var l = new List<string>();
                if (j.arma != null && j.arma != a.arma) l.Add(j.arma);
                if (j.colete && !a.colete) l.Add("colete");
                if (j.capacete && !a.capacete) l.Add("capacete");
                if (j.kit && !a.kit) l.Add("kit");
                var sobra = a.nades.ToList();
                foreach (var n in j.nades) if (!sobra.Remove(n)) l.Add(n);
                itens[j] = l;
            }
            // repasse não é compra: o rifle velho (até uma Galil que o CT pegou do chão) ia pra cabeça como "comprou galil"
            bool repV = TeamBrain.EXP.Contains("repasseVelho");
            if (!repV) foreach (var (jr, ar) in repasses) if (itens.TryGetValue(jr, out var lr)) lr.Remove(ar);
            foreach (var e in entregas)
            {
                if (!itens.TryGetValue(e.para, out var lp) || !itens.TryGetValue(e.de, out var ld)) continue;
                if (!repV && repasses.Contains((e.para, e.item))) continue;
                if (e.naMao) { lp.Remove(e.item); if (e.antes != null) ld.Remove(e.antes); continue; }   // troca da AWP: ninguém comprou
                if (lp.Remove(e.item)) ld.Add(e.item);
            }
            for (int i = 0; i < 10; i++)
            {
                var j = (i < 5 ? tAtual : ctAtual).js[i % 5];
                if (!itens.TryGetValue(j, out var l) || l.Count == 0) continue;
                uint h = (uint)(seed * 7919 + r * 104729 + i * 15485863);
                float ini = 0.4f + (h % 1000) / 1000f * 1.6f, passo = 0.28f + (h / 1000 % 1000) / 1000f * 0.2f;
                ini = Math.Min(ini, Match.FREEZE * 0.3f);   // freeze curto: começa cedo
                if (l.Count > 1) passo = Math.Min(passo, (Match.FREEZE - 0.3f - ini) / (l.Count - 1));   // cabe no freeze, sem amontoar no fim
                for (int k = 0; k < l.Count; k++)
                    m.compras.Add((m.agents[i], l[k], -Match.FREEZE + ini + k * passo));
            }
            m.compras.Sort((x, y) => x.t.CompareTo(y.t));
        }

        string Comprar(Time tm, bool t)
        {
            var js = tm.js; var outro = tm == A ? B : A;
            // a AWP vai pro AWPer (quem pegou do chão entrega)
            var awper = js.First(j => j.Role(t) == Role.Awper);
            var comAwp = js.FirstOrDefault(j => j != awper && j.arma == "awp");
            if (comAwp != null && awper.arma != "awp") { entregas.Add((comAwp, awper, "awp", awper.arma, true)); (comAwp.arma, awper.arma) = (awper.arma, "awp"); Log($"  {comAwp.nome} dropou a AWP pro {awper.nome}"); }

            string tipo;
            bool pistol = r == 0 || r == 12;
            int total = js.Sum(j => j.dinheiro), precisa = js.Sum(j => CustoCheio(j, t));
            if (pistol) tipo = "pistol";
            else if (js.Count(j => j.dinheiro >= CustoCheio(j, t)) >= 4 || total >= precisa) tipo = "full";
            else if (UltimoDoHalf(r) || outro.placar == Alvo(r) - 1) tipo = "force";
            // anti-eco: o outro time está sem dinheiro (pro faz essa conta) e a gente tem pra SMG + colete → compra
            // (SMG ainda paga $600 por abate)
            else if (ultimo == tm && outro.js.Count(j => j.dinheiro + (TemPrimaria(j) ? 3000 : 0) >= 3700) <= 1 && total / 5 >= 1700) tipo = "force";
            // ganhou o pistol: compra SMG/colete no 2º round (o outro time está quebrado ou forçando com pouco)
            else if (ultimo == tm && (r == 1 || r == 13) && total / 5 >= 1700) tipo = "force";
            // perdeu o pistol: força às vezes (colete + SMG/Deagle) em vez de eco seco — pro faz muito isso; ganha ~1 em 4,
            // mas quando perde o 3º round sai quebrado. Time agressivo (IGL) e quem matou bastante no pistol forçam mais
            else if (ultimo != null && ultimo != tm && (r == 1 || r == 13) && total / 5 >= 1800 && !TeamBrain.EXP.Contains("ecoVelho")
                     && rng.Rand() < 0.3f + 0.25f * js.Average(j => j.skill.aggression) + (total / 5 >= 2400 ? 0.1f : 0)) tipo = "force";
            else
            {
                int bonus = PERDA[Math.Min(tm.nivelPerda + 1, 4)];
                int proximo = js.Count(j => j.dinheiro + bonus >= CustoCheio(j, t));
                tipo = proximo >= 4 ? "eco" : total / 5 >= 2300 ? "force" : "eco";
            }

            // call do IGL na força: "compra até X" — o time força sem matar o próximo round (quem tem mais guarda o resto e
            // a compra fica parecida pra todo mundo). Último round do half / o outro no match point: vai tudo
            int teto = int.MaxValue;
            if (tipo == "force" && !UltimoDoHalf(r) && outro.placar != Alvo(r) - 1 && !TeamBrain.EXP.Contains("tetoVelho"))
            {
                teto = tm.nivelPerda >= 3 ? 3000 : tm.nivelPerda == 2 ? 2500 : 2000;   // perdendo seguido (bônus alto) dá pra Galil/FAMAS + colete
                Log($"  IGL ({tm.nome}): força até {teto}");
                ev.Add("ECO força com teto");
            }
            var armaAntes = new Dictionary<Jogador, string>(); foreach (var j0 in js) armaAntes[j0] = j0.arma;   // o que cada um tinha antes da compra (revenda)
            foreach (var j in js)
            {
                bool awp = j.Role(t) == Role.Awper;
                if (tipo == "pistol")
                {   // pistol round ($800): quase todo mundo de colete; alguns de pistola melhor (Tec-9/Five-SeveN) + granada,
                    // ou P250 + utilitária — o time mistura (quem entra quer colete, quem fica atrás quer a pistola)
                    float pr = PISTOLAS_EXTRAS ? rng.Rand() : 1f;
                    string pf = t ? "tec9" : "fiveseven";
                    // CT no bomb: kit + flash no lugar do colete (o retake do pistol sem kit quase não sai — pro sempre tem um)
                    if (!t && !TeamBrain.EXP.Contains("pistolKitVelho") && j.roleCT == Role.Ancora && rng.Rand() < 0.65f)
                    { Compra(j, "kit"); Compra(j, "flash"); continue; }
                    if (pr < 0.22f && j.dinheiro >= Preco(pf)) { j.dinheiro -= Preco(pf); j.arma = pf; Compra(j, "flash"); }
                    else if (pr < 0.35f && j.dinheiro >= Preco("p250")) { j.dinheiro -= Preco("p250"); j.arma = "p250"; Compra(j, "flash"); Compra(j, "smoke"); }
                    else Compra(j, "colete");
                    continue;
                }
                int guardado = teto < int.MaxValue ? Math.Max(0, j.dinheiro - teto) : 0;
                j.dinheiro -= guardado;   // fora do teto: não entra nesta compra (volta no fim)
                try {
                if (tipo == "eco")
                {
                    // guarda o dinheiro (fica com o que sobreviveu), mas quem pode sem estragar a compra do próximo pega
                    // Deagle (ou P250): é assim que eco às vezes ganha — um na cabeça e pega o rifle do chão
                    // a Deagle já tem arte (P250 ainda não: PISTOLAS_EXTRAS liga ela quando tiver)
                    if (!TemPrimaria(j) && j.arma != "deagle")
                    {
                        int sobra = j.dinheiro + PERDA[Math.Min(tm.nivelPerda + 1, 4)] - CustoCheio(j, t);
                        string p = sobra >= 700 && rng.Rand() < 0.7f ? "deagle" : PISTOLAS_EXTRAS && (sobra >= 300 || rng.Rand() < 0.25f) && j.arma != "p250" ? "p250" : null;
                        if (p != null && j.dinheiro >= Preco(p)) { j.dinheiro -= Preco(p); j.arma = p; }
                    }
                    continue;
                }
                if (!TemPrimaria(j))
                {
                    string quer = awp && tipo == "full" && j.dinheiro >= Preco("awp") + (TeamBrain.EXP.Contains("awpCompraVelha") ? 1000 : 650) ? "awp" : Rifle(j, t);
                    if (j.dinheiro >= Preco(quer) + 650) { j.dinheiro -= Preco(quer); DarPrimaria(j, quer); }
                    else if (tipo == "full" && PISTOLAS_EXTRAS && !TeamBrain.EXP.Contains("famasGalilVelho") && j.dinheiro   /* A/B 40 partidas (2026-11-06): galil/famas 0,15→0,33/round, TR 49,3→50,3 (ruído ±2,4), cheia x cheia igual */ >= Preco(t ? "galil" : "famas") + 650 && rng.Rand() < 0.7f)
                    {   // compra cheia mas curto pro rifle: o pro vai de Galil/FAMAS + colete (às vezes espera o drop do colega rico)
                        string barato0 = t ? "galil" : "famas"; j.dinheiro -= Preco(barato0); DarPrimaria(j, barato0);
                    }
                    else if (tipo == "force")
                    {
                        string smg = t ? "mac10" : "mp9", barato = t ? "galil" : "famas", pf = t ? "tec9" : "fiveseven";
                        // força: o degrau é pelo dinheiro — Galil/FAMAS (rifle barato) > SMG > Deagle/Tec-9/Five-SeveN; sempre com colete
                        if (PISTOLAS_EXTRAS && j.dinheiro >= Preco(barato) + 650) { j.dinheiro -= Preco(barato); DarPrimaria(j, barato); }
                        else if (j.dinheiro >= Preco(smg) + 650) { j.dinheiro -= Preco(smg); DarPrimaria(j, smg); }
                        else if (j.arma != "deagle" && j.dinheiro >= Preco("deagle") + 650 && (!PISTOLAS_EXTRAS || rng.Rand() < 0.5f)) { j.dinheiro -= Preco("deagle"); j.arma = "deagle"; }   // meia compra: colete + Deagle
                        else if (PISTOLAS_EXTRAS && WeaponStats.Pistola(j.arma ?? "glock") && j.arma != pf && j.dinheiro >= Preco(pf) + 650) { j.dinheiro -= Preco(pf); j.arma = pf; }   // quebrado forçando: Tec-9/Five-SeveN + colete
                    }
                }
                Compra(j, "colete");
                bool velhoCT = TeamBrain.EXP.Contains("compraCTVelha");
                // real (143 partidas pro): capacete TR 83%, CT 67%; kit 42% dos CTs. O CT põe a granada antes do capacete e do kit
                if (t || velhoCT) Compra(j, "capacete");
                if (!t && velhoCT) Compra(j, "kit");
                Compra(j, "smoke"); Compra(j, "flash"); Compra(j, t ? "molotovT" : "molotovCT");
                if (!t && !velhoCT)
                {
                    if (j.dinheiro >= 350 + 600) Compra(j, "capacete");
                    int kits = js.Count(x => x.kit);
                    bool perto = j.roleCT == Role.Ancora || j.Role(false) == Role.Suporte;   // quem joga no bomb desarma
                    if (kits < 3 && j.dinheiro >= 400 + (perto ? 0 : 500)) Compra(j, "kit");
                }
                if (tipo == "full" && j.dinheiro >= 300 + 1000 && !TeamBrain.EXP.Contains("semHE")) Compra(j, "he");   // HE por último, só sobrando dinheiro
                // pistola melhor no coldre: o AWPer quase sempre (a AWP sozinha não segura entrada de vários), os outros só
                // sobrando bem — a Deagle é o que tem arte agora (P250/Tec-9/Five-SeveN quando tiver)
                if (tipo == "full" && j.arma != null && !WeaponStats.Pistola(j.arma) && j.pistola == null && !TeamBrain.EXP.Contains("semPistola"))
                {
                    bool quer = j.arma == "awp" ? j.dinheiro >= 700 + 600 && rng.Rand() < 0.75f : j.dinheiro >= 700 + 2500 && rng.Rand() < 0.3f;
                    if (quer) { j.dinheiro -= Preco("deagle"); j.pistola = "deagle"; }
                }
                } finally { j.dinheiro += guardado; }
            }
            if (tipo == "eco" && !TeamBrain.EXP.Contains("ecoAjudaVelha")) EcoComRico(tm, t);
            // AWP no AWPer na compra cheia: se ele tem dinheiro, troca o rifle que sobreviveu pela AWP (o rifle vai pro colega
            // sem primária); se não tem, quem está rico dropa a AWP pra ele — time pro joga com a AWP
            if (tipo == "full" && !TeamBrain.EXP.Contains("awpCompraVelha") && awper.arma != "awp")
            {
                var rico = js.Where(j => j != awper).OrderByDescending(j => j.dinheiro).First();
                Jogador paga = awper.dinheiro >= Preco("awp") ? awper : rico.dinheiro >= Preco("awp") + 300 ? rico : null;
                if (paga != null)
                {
                    paga.dinheiro -= Preco("awp");
                    string antesAwper = awper.arma;
                    string velho = awper.arma; DarPrimaria(awper, "awp"); if (velho != null && WeaponStats.Pistola(velho)) velho = null;
                    var semArma = js.FirstOrDefault(j => j != awper && !TemPrimaria(j));
                    // ninguém sem primária: o rifle do AWPer ia ficar largado na base. No CS2 quem comprou uma arma pior NESTE
                    // round revende (reembolso cheio no tempo de compra) e fica com o rifle. -exp revendaVelha: fica no chão
                    if (semArma == null && velho != null && !TeamBrain.EXP.Contains("revendaVelha"))
                    {
                        var vende = js.Where(j => j != awper && j != paga && TemPrimaria(j) && j.arma != "awp" && armaAntes.TryGetValue(j, out var ant) && ant != j.arma && Preco(j.arma) < Preco(velho))
                                      .OrderBy(j => Preco(j.arma)).FirstOrDefault();
                        if (vende != null)
                        {
                            Log($"  {vende.nome} revendeu {WeaponStats.ALL[vende.arma].name} pra ficar com {WeaponStats.ALL[velho].name} do {awper.nome}");
                            vende.dinheiro += Preco(vende.arma); vende.arma = armaAntes[vende] != null && WeaponStats.Pistola(armaAntes[vende]) ? armaAntes[vende] : null;
                            if (vende.pistola != null && vende.arma == null) { vende.arma = vende.pistola; vende.pistola = null; }
                            semArma = vende;
                        }
                    }
                    // em campo: quem pagou dropa a AWP pro AWPer; o rifle velho dele cai quando ele pega, e quem está sem
                    // arma pega do chão (se o AWPer comprou, ele mesmo dropa o rifle pro colega)
                    if (paga != awper) entregas.Add((paga, awper, "awp", antesAwper, false));
                    if (velho != null && !WeaponStats.Pistola(velho) && semArma != null)
                    {
                        if (paga == awper) entregas.Add((awper, semArma, velho, semArma.arma, false)); else comecaCom.Add((semArma, semArma.arma));
                        DarPrimaria(semArma, velho); repasses.Add((semArma, velho));
                    }
                    Log($"  {(paga == awper ? $"{awper.nome} comprou a AWP" : $"{paga.nome} dropou a AWP pro {awper.nome}")}");
                }
            }
            // drop: quem sobrou com dinheiro compra rifle pro colega que não conseguiu
            if (tipo == "full" || tipo == "force")
                foreach (var pobre in js.Where(j => !TemPrimaria(j)))
                {
                    string rf = Rifle(pobre, t);
                    var rico = js.Where(j => j != pobre).OrderByDescending(j => j.dinheiro).First();
                    if (rico.dinheiro >= Preco(rf)) { rico.dinheiro -= Preco(rf); entregas.Add((rico, pobre, rf, pobre.arma, false)); DarPrimaria(pobre, rf); Log($"  {rico.nome} dropou {WeaponStats.ALL[rf].name} pro {pobre.nome}"); }
                }
            // granada pro colega: quem sobrou rico compra a smoke/flash que faltou pra quem não teve dinheiro e dropa
            if (tipo == "full" || tipo == "force")
                foreach (var nd in new[] { "smoke", "flash" })
                    foreach (var sem in js.Where(j => !j.nades.Contains(nd)))
                    {
                        var rico = js.Where(j => j != sem).OrderByDescending(j => j.dinheiro).First();
                        if (rico.dinheiro < Preco(nd) + 1200) continue;
                        rico.dinheiro -= Preco(nd); sem.nades.Add(nd); entregas.Add((rico, sem, nd, null, false)); Log($"  {rico.nome} comprou {nd} pro {sem.nome}");
                    }
            return tipo;
        }

        /// <summary>
        /// Eco com alguém rico: o que passa do que ele precisa no próximo round (a compra cheia dele + o drop de rifle pros
        /// colegas que não vão chegar) não fica parado — compra granada pra ele e pistola pros mais pobres (dropa na base),
        /// como pro: o eco fica mais forte sem estragar a compra do time no próximo.
        /// </summary>
        void EcoComRico(Time tm, bool t)
        {
            var js = tm.js; int bonus = PERDA[Math.Min(tm.nivelPerda + 1, 4)];
            // quanto falta, no próximo round, pros que não chegam na compra cheia (o rico vai dropar)
            int falta = js.Sum(o => Math.Max(0, CustoCheio(o, t) - (o.dinheiro + bonus)));
            foreach (var rico in js.OrderByDescending(j => j.dinheiro))
            {
                int livre = rico.dinheiro + bonus - CustoCheio(rico, t) - Math.Min(falta, 3200);
                if (livre < 700) continue;
                falta = Math.Max(0, falta - (rico.dinheiro + bonus - CustoCheio(rico, t)));
                int gasto0 = rico.dinheiro;
                // pistola pros colegas mais pobres (Deagle: um na cabeça e pega o rifle do chão)
                foreach (var pobre in js.Where(o => o != rico && !TemPrimaria(o) && o.arma != "deagle").OrderBy(o => o.dinheiro))
                {
                    if (livre < Preco("deagle") + 300) break;
                    rico.dinheiro -= Preco("deagle"); livre -= Preco("deagle");
                    entregas.Add((rico, pobre, "deagle", pobre.arma, false)); pobre.arma = "deagle";
                    Log($"  eco: {rico.nome} dropou Deagle pro {pobre.nome}"); ev.Add("ECO rico dropou pistola no eco");
                }
                // granada pra ele (flash primeiro: um pick no eco começa com flash)
                foreach (var nd in new[] { "flash", "smoke", "he" })
                    if (livre >= Preco(nd) + 200 && !rico.nades.Contains(nd)) { rico.dinheiro -= Preco(nd); rico.nades.Add(nd); livre -= Preco(nd); }
                if (rico.dinheiro < gasto0) Log($"  eco: {rico.nome} tinha {gasto0} — gastou {gasto0 - rico.dinheiro} no eco sem estragar o próximo");
            }
        }
        /// <summary>Contagem de decisões de economia (validador de partidas: Program.Partidas soma por round).</summary>
        public readonly List<string> ev = new List<string>();

        static void Compra(Jogador j, string item)
        {
            bool tem = item == "colete" ? j.colete : item == "capacete" ? j.capacete : item == "kit" ? j.kit : j.nades.Contains(item.StartsWith("molotov") ? "molotov" : item);
            if (tem || item == "capacete" && !j.colete) return;
            int p = Preco(item); if (j.dinheiro < p) return;
            j.dinheiro -= p;
            if (item == "colete") j.colete = true; else if (item == "capacete") j.capacete = true; else if (item == "kit") j.kit = true;
            else j.nades.Add(item.StartsWith("molotov") ? "molotov" : item);
        }

        /// <summary>Fim do round: placar, dinheiro (vitória, bônus de derrota, plant, abates), o que cada um leva pro próximo.</summary>
        public void Fechar(Match m)
        {
            bool tWin = m.winner == "TR";
            var ganhou = tWin ? tAtual : ctAtual; var perdeu = tWin ? ctAtual : tAtual;
            ganhou.placar++; ultimo = ganhou;
            // leitura: cada time anota o próprio plano (e se deu certo) e o que viu do adversário
            char siteTR = m.bombPlanted || m.igl[0].Executou ? m.planSite : ' ';
            tAtual.leitura.MeuPlano(m.igl[0].plan, tWin, siteTR); ctAtual.leitura.MeuPlano(m.igl[1].plan, !tWin);
            if (m.igl[0].estiloPP != "") tAtual.leitura.Anota("pp:" + m.igl[0].estiloPP, tWin);
            if (m.igl[1].portaRetakeNome != "") ctAtual.leitura.Anota(m.igl[1].portaRetakeNome, !tWin);
            ctAtual.leitura.DeduzTR(m, Team.CT); tAtual.leitura.DeduzCT(m);   // o que o outro planejou ninguém sabe: só o que viram
            int premio = m.reason == "bomb explodiu" || m.reason == "bomb desarmado" ? WIN_BOMB : WIN;
            perdeu.nivelPerda = Math.Min(perdeu.nivelPerda + 1, 4);
            int bonus = PERDA[perdeu.nivelPerda];
            ganhou.nivelPerda = Math.Max(ganhou.nivelPerda - 1, 0);   // CS2: ganhar tira uma derrota da conta (não zera)
            int guardaram = 0, guardadasVivas = 0;
            for (int i = 0; i < 10; i++)
            {
                var a = m.agents[i]; var tm = i < 5 ? tAtual : ctAtual; var j = tm.js[i % 5];
                if (tm == ganhou) j.dinheiro += premio;
                else if (!(a.team == Team.T && m.reason == "tempo acabou" && (a.vivoNoFim || TeamBrain.EXP.Contains("semPosRound") && a.alive)))   // TR vivo quando o tempo acabou não ganha nada (morrer depois, no pós-round, não devolve)
                    j.dinheiro += bonus + (a.team == Team.T && m.bombPlanted ? PLANT_TIME : 0);
                j.dinheiro = Math.Min(MAX, j.dinheiro + a.ganho);
                if (a.guardando && !a.salvaPos) { guardaram++; j.guardou++; if (a.alive) guardadasVivas++; }
                if (a.alive) { string pis = a.Pistola == "glock" || a.Pistola == "usp" ? null : a.Pistola; j.arma = a.Primaria ?? pis; j.pistola = a.Primaria != null ? pis : null; j.colete = a.armor > 0; j.capacete = a.helmet && a.armor > 0; j.kit = a.hasKit; j.nades = new List<string>(a.nades); }
                else Limpa(j);
                j.k += a.kills; j.d += a.deaths; j.hs += a.hs; j.dano += a.damage; j.fk += a.firstKills; j.rounds++;
            }
            var info = new Info
            {
                n = r + 1, placarA = A.placar, placarB = B.placar, ladoA = tAtual == A ? "TR" : "CT", compraT = compraTAtual, compraCT = compraCTAtual,
                vencedor = ganhou.nome, motivo = m.reason, timeAvenceu = ganhou == A, planoT = m.igl[0].plan, planoCT = m.igl[1].plan, guardaram = guardaram, guardadasVivas = guardadasVivas, gastoT = gastoT, gastoCT = gastoCT, m = m,
            };
            historico.Add(info);
            Log($"R{info.n,2}  {A.nome} {A.placar,2} x {B.placar,-2} {B.nome}   TR {tAtual.nome} ({compraTAtual}, gastou ${gastoT})  x  CT {ctAtual.nome} ({compraCTAtual}, gastou ${gastoCT})   → {ganhou.nome} ({(tWin ? "TR" : "CT")}): {m.reason}{(guardaram > 0 ? $"   guardaram {guardaram} ({guardadasVivas} salvas)" : "")}");
            Log($"     dinheiro {A.nome}: {string.Join(" ", A.js.Select(j => j.dinheiro))}   {B.nome}: {string.Join(" ", B.js.Select(j => j.dinheiro))}");
            int alvo = Alvo(r);
            r++;
            if (A.placar >= alvo || B.placar >= alvo) { acabou = true; vencedor = A.placar > B.placar ? A : B; Log($"FIM: {vencedor.nome} {Math.Max(A.placar, B.placar)} x {Math.Min(A.placar, B.placar)}{(r > 24 ? " (prorrogação)" : "")}"); }
            else if (r == 24) Log("12 x 12: prorrogação (MR3, $12.500)");
            else if (r == 12) Log("troca de lado");
        }

        void Log(string s) => log.Add(s);

        /// <summary>Joga a partida inteira (sem vídeo). Guarda os rounds no histórico (sem o Match, pra não pesar).</summary>
        public void Jogar(Action<Match> cadaRound = null)
        {
            while (!acabou && r < 60)
            {
                var m = Preparar();
                while (!m.over && m.t < 200)
                {
                    try { m.Step(); }
                    catch (Exception e) { Log($"ERRO NA SIMULAÇÃO R{r + 1}: {e.Message} @ {e.StackTrace?.Split('\n').FirstOrDefault()?.Trim()}"); m.over = true; m.winner = "CT"; m.reason = "erro na simulação"; }
                }
                if (!m.over) { foreach (var x in m.agents) x.vivoNoFim = x.alive; m.over = true; m.winner = "CT"; m.reason = "tempo acabou"; }
                // pós-round: quem perdeu tenta salvar a arma, quem ganhou pode caçar (morrer aqui = perder a arma)
                if (!TeamBrain.EXP.Contains("semPosRound"))
                    for (float pr = 0; pr < Match.POS_ROUND; pr += Match.DT)
                        try { m.StepPosRound(); } catch (Exception e) { Log($"ERRO NO PÓS-ROUND R{r + 1}: {e.Message}"); break; }
                Fechar(m);
                cadaRound?.Invoke(m);
                historico[historico.Count - 1].m = null;
            }
        }
    }
}
