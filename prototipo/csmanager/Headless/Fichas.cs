using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSM.Sim;

namespace CSM.Headless
{
    /// <summary>
    /// Validação dos status dos jogadores (Jogadores.cs) e geração da base fake.
    ///   -base N [-seed S]           gera N jogadores; escreve Logs/jogadores/base_S.csv e um resumo
    ///   -fichas CASO -np N -seed S  joga N partidas pareadas; o time A recebe a mudança
    /// Casos: eq (ficha padrão = sem ficha, tem que dar IGUAL), nivel11/13/17/19 (A com tudo nesse número),
    /// fora (A fora da função, familiaridade 5), momento0/momento (clima e moral do A em 0 e no máximo, momento ligado),
    /// t1t3 (5 do tier 1 contra 5 do tier 3 da base), t1t1 (dois times do tier 1 sorteados: nota x resultado).
    /// </summary>
    public static class Fichas
    {
        static readonly Role[] TdeA = { Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };
        static readonly Role[] TdeB = { Role.Igl, Role.Entry, Role.Awper, Role.Lurker, Role.Suporte };

        public static int Base(int n, int seed)
        {
            var fs = BaseJogadores.Gerar(seed, n);
            Directory.CreateDirectory("Logs/jogadores");
            File.WriteAllText($"Logs/jogadores/base_{seed}.csv", BaseJogadores.Csv(fs), new UTF8Encoding(true));
            var sb = new StringBuilder(); var inv = System.Globalization.CultureInfo.InvariantCulture;
            sb.AppendLine($"BASE {n} jogadores (semente {seed})");
            foreach (var g in fs.GroupBy(f => f.tier).OrderBy(g => g.Key))
                sb.AppendLine($"  tier {g.Key}: {g.Count(),5} | hab média {g.Average(f => f.Habilidade).ToString("0.0", inv)} (p10 {Pct(g.Select(f => f.Habilidade), 0.1):0.0} p90 {Pct(g.Select(f => f.Habilidade), 0.9):0.0}) | idade {g.Average(f => f.idade):0.0} | mira {g.Average(f => f[Atr.Mira]):0.0} reflexo {g.Average(f => f[Atr.Reflexo]):0.0} calma {g.Average(f => f[Atr.Calma]):0.0}");
            sb.AppendLine("  funções (TR): " + string.Join(", ", fs.GroupBy(f => f.funcao).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
            sb.AppendLine("  funções (CT): " + string.Join(", ", fs.GroupBy(f => f.funcaoCT).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}")));
            sb.AppendLine("  países: " + string.Join(", ", fs.GroupBy(f => f.pais).OrderByDescending(g => g.Count()).Take(12).Select(g => $"{g.Key} {g.Count()}")));
            sb.AppendLine($"  melhor função = a natural: {100.0 * fs.Count(f => f.MelhorFuncao() == f.funcao || f.MelhorFuncao() == f.funcaoCT) / n:0.0}%");
            sb.AppendLine($"  nicks únicos: {fs.Select(f => f.nick.ToLowerInvariant()).Distinct().Count()} de {n}");
            sb.AppendLine("  características mais comuns: " + string.Join(", ", fs.SelectMany(f => f.carac).GroupBy(c => c).OrderByDescending(g => g.Count()).Take(8).Select(g => $"{Atributos.Nome(g.Key)} {g.Count()}")));
            sb.AppendLine("  top 10:");
            foreach (var f in fs.OrderByDescending(f => f.Habilidade).Take(10)) sb.AppendLine("    " + f + " | " + string.Join(", ", f.carac.Select(Atributos.Nome)));
            // evolução: um jovem do tier 4 jogando 150 partidas de entry, 12 rounds por lado
            var jovem = fs.Where(f => f.tier == 4 && f.idade <= 18 && f.funcao == Role.Entry).OrderByDescending(f => f.teto[(int)Atr.Reflexo] - f[Atr.Reflexo]).FirstOrDefault();
            if (jovem != null)
            {
                string Linha(Ficha f) => $"hab {f.Habilidade:0.0} | reflexo {f[Atr.Reflexo]:0.0} movim {f[Atr.Movimento]:0.0} | fam entry {f.Fam(Role.Entry):0.0} lurker {f.Fam(Role.Lurker):0.0} | nota lurker {f.NotaNaFuncao(Role.Lurker):0.0}";
                sb.AppendLine($"  evolução de {jovem.nick} ({jovem.idade}, entry): antes  {Linha(jovem)}");
                var rpf = new int[7]; rpf[(int)Role.Entry] = 12; rpf[(int)jovem.funcaoCT] = 12;
                for (int p = 0; p < 150; p++) { Evolucao.AposPartida(jovem, rpf, 1.05f, p % 2 == 0); if (p == 99) Evolucao.Aniversario(jovem); }
                sb.AppendLine($"  depois de 150 partidas (+1 ano): {Linha(jovem)}");
                int sem = 0; while (!Treino.Caracteristica(jovem, Caracteristica.PreFire) && sem < 60) sem++;
                sb.AppendLine($"  treino da característica 'Pre-fire nos cantos': aprendeu em {sem + 1} semanas");
            }
            {   // mentoria: o melhor mentor do tier 1 com um novato da academia, 1 ano (52 semanas)
                var mentor = fs.Where(f => f.tier == 1 && f.idade >= 25).OrderByDescending(Mentoria.Qualidade).First();
                var nov = fs.Where(f => f.tier == 4 && f.idade <= 18).OrderByDescending(f => f.teto[(int)Atr.LeituraDeJogo] - f[Atr.LeituraDeJogo]).Skip(3).First();
                var gemeo = BaseJogadores.Gerar(seed, 5000).First(f => f.id == nov.id);   // o mesmo novato sem mentor
                string L(Ficha f) => $"leitura {f[Atr.LeituraDeJogo]:0.0} decisões {f[Atr.Decisoes]:0.0} granada {f[Atr.Utilitaria]:0.0} calma {f[Atr.Calma]:0.0} prof {f[Atr.Profissionalismo]:0.0} fam {mentor.funcao} {f.Fam(mentor.funcao):0.0} | {string.Join(", ", f.carac.Select(Atributos.Nome))}";
                sb.AppendLine($"  mentoria: {mentor.nick} ({mentor.idade}, {mentor.funcao}, qualidade {Mentoria.Qualidade(mentor):0.00}) com {nov.nick} ({nov.idade})");
                sb.AppendLine($"    antes:            {L(nov)}");
                var ganhou = new List<string>();
                for (int w = 0; w < 52; w++) { var c = Mentoria.Semana(mentor, nov); if (c != null) ganhou.Add(Atributos.Nome(c.Value)); }
                sb.AppendLine($"    1 ano de mentoria: {L(nov)}{(ganhou.Count > 0 ? " | pegou: " + string.Join(", ", ganhou) : "")}");
                sb.AppendLine($"    sem mentor:        {L(gemeo)}");
                // o técnico do melhor time do tier 1 mentorando um gêmeo do mesmo novato (grupo com 3 novatos)
                var (clubes, _, _) = BaseClubes.Gerar(BaseJogadores.Gerar(seed, 5000), seed);
                var coach = clubes.Where(c => c.tier == 1).SelectMany(c => c.equipe.staff).Where(x => x.funcao == FuncaoStaff.Tecnico).OrderByDescending(MentoriaStaff.Qualidade).First();
                var g = new GrupoMentoria(); var nov2 = BaseJogadores.Gerar(seed, 5000).First(f => f.id == nov.id);
                g.mentoresStaff.Add(coach); g.aprendizes.Add(nov2); g.aprendizes.AddRange(BaseJogadores.Gerar(seed, 5000).Where(f => f.tier == 4 && f.idade <= 18 && f.id != nov.id).Take(2));
                var pegouC = new List<string>(); for (int w = 0; w < 52; w++) foreach (var (q, c) in g.Semana()) if (q.id == nov.id) pegouC.Add(Atributos.Nome(c));
                sb.AppendLine($"    técnico {coach.nome} (qualidade {MentoriaStaff.Qualidade(coach):0.00}), grupo de 3, 1 ano: {L(nov2)}{(pegouC.Count > 0 ? " | pegou: " + string.Join(", ", pegouC) : "")}");
            }
            File.WriteAllText($"Logs/jogadores/base_{seed}_resumo.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
        public static int Clubes(int seed)
        {
            var fs = BaseJogadores.Gerar(seed, 5000);
            var (clubes, livres, staffLivre) = BaseClubes.Gerar(fs, seed);
            var sb = new StringBuilder(BaseClubes.Resumo(clubes, livres, staffLivre));
            // fora de posição: quanto cai a nota de um titular do tier 1 se ele fizer a pior função do time
            var tit = clubes.Where(c => c.tier == 1).SelectMany(c => c.Titulares).ToList();
            sb.AppendLine($"  nota média na função natural (tier 1): {tit.Average(f => f.NotaNaFuncao(f.funcao)):0.0} | na pior função: {tit.Average(f => Enumerable.Range(0, 7).Min(r => f.NotaNaFuncao((Role)r))):0.0}");
            var e = clubes[0].equipe; var vazio = new Equipe();
            sb.AppendLine($"  staff do melhor time: treino de mira x{e.TreinoDe(Atr.Mira):0.00} tático x{e.Familiaridade:0.00} tilt x{e.AmorteceTilt:0.00} clima alvo {e.ClimaAlvo:+0.00;-0.00} | sem staff: x{vazio.TreinoDe(Atr.Mira):0.00} tilt x{vazio.AmorteceTilt:0.00}");
            var f0 = tit[0]; var olheiroBom = clubes[0].equipe.staff.FirstOrDefault(s => s.funcao == FuncaoStaff.Olheiro);
            sb.AppendLine($"  olheiro: {f0.nick} mira real {f0[Atr.Mira]:0.0} | olheiro do time vê {Olheiro.Ver(f0, Atr.Mira, olheiroBom)} | sem olheiro vê {Olheiro.Ver(f0, Atr.Mira, null)}");
            Directory.CreateDirectory("Logs/jogadores");
            File.WriteAllText($"Logs/jogadores/clubes_{seed}.txt", sb.ToString());
            Console.Write(sb.ToString());
            return 0;
        }
        static float Pct(IEnumerable<float> xs, double q) { var l = xs.OrderBy(x => x).ToList(); return l[(int)Math.Min(l.Count - 1, q * l.Count)]; }

        static Ficha[] Padroes(int id0) => Enumerable.Range(0, 5).Select(i => Ficha.Padrao(id0 + i)).ToArray();
        static Ficha[] Nivel(float x, int id0)
        {
            var fs = Padroes(id0);
            foreach (var f in fs) for (int k = 0; k < Atributos.N; k++) if (!Atributos.Personalidade((Atr)k)) f.atr[k] = x;
            return fs;
        }
        static Ficha[] Time(List<Ficha> pool, Role[] funcoes, Rng rng)
        {
            var t = new Ficha[5];
            for (int i = 0; i < 5; i++) { var c = pool.Where(f => f.funcao == funcoes[i] && !t.Contains(f)).ToList(); t[i] = c[rng.Int(c.Count)]; }
            return t;
        }

        public static int Rodar(string caso, int n, int seed0)
        {
            bool momento = caso.StartsWith("momento") || caso.StartsWith("t1");
            if (!momento && !TeamBrain.EXP.Contains("momentoDesligado")) TeamBrain.EXP += " momentoDesligado";
            List<Ficha> baseFs = caso.StartsWith("t1") || caso == "escala" || caso == "bagunca" ? BaseJogadores.Gerar(77, 5000) : null;
            var trava = new object();
            int rA = 0, vA = 0, partA = 0, erros = 0, diferentes = 0; double kA = 0; var notaXres = new List<(float dif, bool venceu)>();
            new Match(seed0); CSM.Map.Spots.All();
            Parallel.For(0, n, new ParallelOptions { MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount / 2) }, i =>
            {
                int seed = seed0 + i;
                var p = new Partida(seed);
                Ficha[] fa = null, fb = null;
                switch (caso)
                {
                    case "eq": fa = Padroes(1); fb = Padroes(6); break;
                    case "fora": fa = Padroes(1); foreach (var f in fa) for (int k = 0; k < 7; k++) f.fam[k] = 5f; fb = Padroes(6); break;
                    case "momento0": fa = Padroes(1); fb = Padroes(6); break;
                    case "momento": fa = Padroes(1); fb = Padroes(6); p.A.clima = 1f; foreach (var f in fa) { f.moral = 1f; f.forma = 1f; } break;
                    case "t1t3": { var rng = new Rng(seed * 13 + 5); fa = Time(baseFs.Where(f => f.tier == 1).ToList(), TdeA, rng); fb = Time(baseFs.Where(f => f.tier == 3).ToList(), TdeB, rng); break; }
                    case "t1t1": { var rng = new Rng(seed * 13 + 5); var t1 = baseFs.Where(f => f.tier == 1).ToList(); fa = Time(t1, TdeA, rng); fb = Time(t1, TdeB, rng); break; }
                    case "escala":
                    case "bagunca":
                        {   // um time do tier 1 de verdade: escalado pelos atributos x cada um fora do lugar dele
                            var (clubes, _, _) = BaseClubes.Gerar(baseFs, 77);
                            var cl = clubes.Where(c => c.tier == 1).ToList()[i % 20];
                            var tit = cl.Titulares;
                            if (caso == "escala") Escalacao.Aplicar(p.A, tit, true);
                            else
                            {   // mesma escalação girada: cada um faz a função e a posição de CT de outro
                                Escalacao.Aplicar(p.A, tit, true);
                                var fs = p.A.js.Select(j => j.ficha).ToArray();
                                for (int k = 0; k < 5; k++) p.A.js[k].ficha = fs[(k + 2) % 5];
                            }
                            fa = p.A.js.Select(j => j.ficha).ToArray(); fb = Padroes(6);
                            p.UsarFichas(null, fb, nomes: false);
                            break;
                        }
                    default:
                        if (caso.StartsWith("nivel")) { fa = Nivel(float.Parse(caso.Substring(5)), 1); fb = Padroes(6); }
                        break;
                }
                if (caso != "escala" && caso != "bagunca") p.UsarFichas(fa, fb, nomes: false);
                var res = new StringBuilder(); int lr = 0, lv = 0; double lk = 0; int r = 0;
                p.Jogar(m =>
                {
                    bool aT = Partida.ATerrorista(r); r++;
                    bool venceuA = (m.winner == "TR") == aT; lr++; if (venceuA) lv++;
                    foreach (var ab in m.abates) if (!ab.aliado && ab.by != null && ab.by.IsT == aT) lk++;
                    res.Append(m.winner[0]).Append(m.abates.Count).Append(',');
                });
                int le = p.log.Count(l => l.Contains("ERRO"));
                bool dif = false;
                if (caso == "eq")
                {   // a mesma semente sem ficha nenhuma tem que dar o mesmo jogo, round a round
                    var p0 = new Partida(seed); var res0 = new StringBuilder();
                    p0.Jogar(m => res0.Append(m.winner[0]).Append(m.abates.Count).Append(','));
                    dif = res0.ToString() != res.ToString();
                }
                lock (trava)
                {
                    rA += lr; vA += lv; kA += lk; erros += le; if (p.vencedor == p.A) partA++; if (dif) diferentes++;
                    if (fa != null && fb != null) notaXres.Add((fa.Average(f => f.Habilidade) - fb.Average(f => f.Habilidade), p.vencedor == p.A));
                }
            });
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string extra = caso == "eq" ? $" | IGUAL ao sem ficha: {n - diferentes}/{n}" : "";
            if (caso == "t1t1" && notaXres.Count > 3)
            {   // quem tem a nota média maior ganha mais?
                int certo = notaXres.Count(x => x.dif > 0 == x.venceu);
                extra = $" | o time de nota maior venceu {100.0 * certo / notaXres.Count:0.0}% (dif média |{notaXres.Average(x => Math.Abs(x.dif)):0.00}|)";
            }
            if (caso == "t1t3") extra = $" | nota A−B média {notaXres.Average(x => x.dif):0.00}";
            string linha = string.Format(inv, "{0} n={1} seed={2} | A rounds {3:0.0}% partidas {4:0.0}% | abates A/r {5:0.000} | ERROS {6}{7}", caso, n, seed0, 100.0 * vA / rA, 100.0 * partA / n, kA / rA, erros, extra);
            Directory.CreateDirectory("Logs/jogadores");
            File.WriteAllText($"Logs/jogadores/fichas_{caso}_{seed0}.txt", linha + "\n");
            Console.WriteLine(linha);
            return 0;
        }
    }
}
