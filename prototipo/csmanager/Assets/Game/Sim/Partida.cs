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
        public const bool PISTOLAS_EXTRAS = false;   // P250 no eco: liga quando tiver arte (a Deagle já tem)
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
            A = new Time { nome = "VULCÃO", pesos = pA, js = Match.T_TEAM.Select((x, i) => new Jogador { nome = x.name, roleT = x.role, roleCT = ctDeA[i], siteCT = siteDeA[i] }).ToArray() };
            B = new Time { nome = "ROCHA", pesos = pB, js = Match.CT_TEAM.Select((x, i) => new Jogador { nome = x.name, roleCT = x.role, siteCT = x.site, roleT = tDeB[i] }).ToArray() };
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
        public Match Preparar()
        {
            bool aT = ATerrorista(r);
            tAtual = aT ? A : B; ctAtual = aT ? B : A;
            bool trocaLado = r == 12 || r >= 24 && (r - 24) % 6 == 3;
            if (r == 0 || r == 12) foreach (var t in new[] { A, B }) { t.nivelPerda = PERDA_INICIO; foreach (var j in t.js) { j.dinheiro = START; Limpa(j); } }
            else if (r >= 24 && (r - 24) % 6 == 0) foreach (var t in new[] { A, B }) { t.nivelPerda = PERDA_INICIO; foreach (var j in t.js) j.dinheiro = OT_START; }
            if (trocaLado) foreach (var j in A.js.Concat(B.js)) Limpa(j);
            int antesT = tAtual.js.Sum(j => j.dinheiro), antesCT = ctAtual.js.Sum(j => j.dinheiro);
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
            var aperto = new float[2]; aperto[(int)Team.T] = Aperto(tAtual, ctAtual); aperto[(int)Team.CT] = Aperto(ctAtual, tAtual);
            var m = new Match(seed * 1000 + r, tAtual.pesos?.Clone(), ctAtual.pesos?.Clone(), setup, new[] { compraTAtual, compraCTAtual }, aperto, SemLeitura ? null : tAtual.leitura, SemLeitura ? null : ctAtual.leitura);
            for (int i = 0; i < 10; i++) m.agents[i].skill = (i < 5 ? tAtual : ctAtual).js[i % 5].skill;
            m.semAmanha[(int)Team.T] = SemAmanha(ctAtual); m.semAmanha[(int)Team.CT] = SemAmanha(tAtual);
            return m;
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
        string Comprar(Time tm, bool t)
        {
            var js = tm.js; var outro = tm == A ? B : A;
            // a AWP vai pro AWPer (quem pegou do chão entrega)
            var awper = js.First(j => j.Role(t) == Role.Awper);
            var comAwp = js.FirstOrDefault(j => j != awper && j.arma == "awp");
            if (comAwp != null && awper.arma != "awp") { (comAwp.arma, awper.arma) = (awper.arma, "awp"); Log($"  {comAwp.nome} dropou a AWP pro {awper.nome}"); }

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

            foreach (var j in js)
            {
                bool awp = j.Role(t) == Role.Awper;
                if (tipo == "pistol") { Compra(j, "colete"); continue; }
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
                    else if (tipo == "force")
                    {
                        string smg = t ? "mac10" : "mp9";
                        if (j.dinheiro >= Preco(smg) + 650) { j.dinheiro -= Preco(smg); DarPrimaria(j, smg); }
                        else if (j.arma != "deagle" && j.dinheiro >= Preco("deagle") + 650) { j.dinheiro -= Preco("deagle"); j.arma = "deagle"; }   // meia compra: colete + Deagle
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
            }
            // AWP no AWPer na compra cheia: se ele tem dinheiro, troca o rifle que sobreviveu pela AWP (o rifle vai pro colega
            // sem primária); se não tem, quem está rico dropa a AWP pra ele — time pro joga com a AWP
            if (tipo == "full" && !TeamBrain.EXP.Contains("awpCompraVelha") && awper.arma != "awp")
            {
                var rico = js.Where(j => j != awper).OrderByDescending(j => j.dinheiro).First();
                Jogador paga = awper.dinheiro >= Preco("awp") ? awper : rico.dinheiro >= Preco("awp") + 300 ? rico : null;
                if (paga != null)
                {
                    paga.dinheiro -= Preco("awp");
                    string velho = awper.arma; DarPrimaria(awper, "awp"); if (velho != null && WeaponStats.Pistola(velho)) velho = null;
                    var semArma = js.FirstOrDefault(j => j != awper && !TemPrimaria(j));
                    if (velho != null && !WeaponStats.Pistola(velho) && semArma != null) DarPrimaria(semArma, velho);
                    Log($"  {(paga == awper ? $"{awper.nome} comprou a AWP" : $"{paga.nome} dropou a AWP pro {awper.nome}")}");
                }
            }
            // drop: quem sobrou com dinheiro compra rifle pro colega que não conseguiu
            if (tipo == "full" || tipo == "force")
                foreach (var pobre in js.Where(j => !TemPrimaria(j)))
                {
                    string rf = Rifle(pobre, t);
                    var rico = js.Where(j => j != pobre).OrderByDescending(j => j.dinheiro).First();
                    if (rico.dinheiro >= Preco(rf)) { rico.dinheiro -= Preco(rf); DarPrimaria(pobre, rf); Log($"  {rico.nome} dropou {WeaponStats.ALL[rf].name} pro {pobre.nome}"); }
                }
            // granada pro colega: quem sobrou rico compra a smoke/flash que faltou pra quem não teve dinheiro e dropa
            if (tipo == "full" || tipo == "force")
                foreach (var nd in new[] { "smoke", "flash" })
                    foreach (var sem in js.Where(j => !j.nades.Contains(nd)))
                    {
                        var rico = js.Where(j => j != sem).OrderByDescending(j => j.dinheiro).First();
                        if (rico.dinheiro < Preco(nd) + 1200) continue;
                        rico.dinheiro -= Preco(nd); sem.nades.Add(nd); Log($"  {rico.nome} comprou {nd} pro {sem.nome}");
                    }
            return tipo;
        }

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
