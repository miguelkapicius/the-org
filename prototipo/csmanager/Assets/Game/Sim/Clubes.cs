using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSM.Sim
{
    /* =====================================================================================================================
       TIMES E STAFF (2026-11-07)
       - Staff com atributos de 1 a 20 (como o FM): técnico, assistente, analista, treinador de mira, psicólogo,
         fisioterapeuta, preparador físico e olheiro. Eles não jogam: ajudam os jogadores a evoluir (treino e familiaridade),
         seguram o moral e o clima, e o olheiro diz quão certo é o número que a tela mostra de um jogador de fora.
       - Todo efeito de staff é multiplicador perto de 1 (staff médio 10 = 1,0), limitado: staff bom acelera e protege, não
         transforma um jogador ruim em craque.
       - Time (clube): elenco (5 titulares + reservas), staff, país, tier, reputação e clima do elenco.
       - Escalação: os atributos decidem a função de cada um no TR e a posição no CT (âncora do A, do B, rotação, AWP, suporte).
       ===================================================================================================================== */

    public enum FuncaoStaff { Tecnico, Assistente, Analista, TreinadorDeMira, Psicologo, Fisioterapeuta, PreparadorFisico, Olheiro }

    public enum AtrStaff
    {
        // técnico / tático
        Tatica, LeituraAoVivo, TreinoMecanico, TreinoUtilitaria, TreinoTatico, TreinoMental,
        // gestão
        GestaoDePessoas, Motivacao, Disciplina, Adaptabilidade,
        // análise
        AnaliseDeAdversario, AnaliseDeDados,
        // saúde
        Psicologia, Fisioterapia, PreparoFisico,
        // olheiro
        AvaliarHabilidade, AvaliarPotencial,
    }

    public class Staff
    {
        public const int N = 17;
        public int id; public string nome, pais; public int idade; public FuncaoStaff funcao;
        public readonly float[] atr = new float[N];
        public float this[AtrStaff a] { get => atr[(int)a]; set => atr[(int)a] = MathF.Max(1f, MathF.Min(20f, value)); }

        public static string Nome(FuncaoStaff f) => f switch
        {
            FuncaoStaff.Tecnico => "Técnico", FuncaoStaff.Assistente => "Assistente técnico", FuncaoStaff.Analista => "Analista",
            FuncaoStaff.TreinadorDeMira => "Treinador de mira", FuncaoStaff.Psicologo => "Psicólogo", FuncaoStaff.Fisioterapeuta => "Fisioterapeuta",
            FuncaoStaff.PreparadorFisico => "Preparador físico", FuncaoStaff.Olheiro => "Olheiro", _ => f.ToString(),
        };
        public static string Nome(AtrStaff a) => a switch
        {
            AtrStaff.Tatica => "Tática", AtrStaff.LeituraAoVivo => "Leitura ao vivo", AtrStaff.TreinoMecanico => "Treino de mecânica",
            AtrStaff.TreinoUtilitaria => "Treino de utilitária", AtrStaff.TreinoTatico => "Treino tático", AtrStaff.TreinoMental => "Treino mental",
            AtrStaff.GestaoDePessoas => "Gestão de pessoas", AtrStaff.Motivacao => "Motivação", AtrStaff.Disciplina => "Disciplina",
            AtrStaff.Adaptabilidade => "Adaptabilidade", AtrStaff.AnaliseDeAdversario => "Análise de adversário", AtrStaff.AnaliseDeDados => "Análise de dados",
            AtrStaff.Psicologia => "Psicologia", AtrStaff.Fisioterapia => "Fisioterapia", AtrStaff.PreparoFisico => "Preparo físico",
            AtrStaff.AvaliarHabilidade => "Avaliar habilidade", AtrStaff.AvaliarPotencial => "Avaliar potencial", _ => a.ToString(),
        };

        /// <summary>O que cada função de staff mais tem (o gerador puxa esses pra cima).</summary>
        public static readonly Dictionary<FuncaoStaff, AtrStaff[]> Chave = new Dictionary<FuncaoStaff, AtrStaff[]>
        {
            [FuncaoStaff.Tecnico] = new[] { AtrStaff.Tatica, AtrStaff.LeituraAoVivo, AtrStaff.GestaoDePessoas, AtrStaff.Motivacao, AtrStaff.TreinoTatico },
            [FuncaoStaff.Assistente] = new[] { AtrStaff.TreinoTatico, AtrStaff.TreinoUtilitaria, AtrStaff.Tatica, AtrStaff.Disciplina },
            [FuncaoStaff.Analista] = new[] { AtrStaff.AnaliseDeAdversario, AtrStaff.AnaliseDeDados, AtrStaff.Tatica },
            [FuncaoStaff.TreinadorDeMira] = new[] { AtrStaff.TreinoMecanico, AtrStaff.Disciplina, AtrStaff.Motivacao },
            [FuncaoStaff.Psicologo] = new[] { AtrStaff.Psicologia, AtrStaff.TreinoMental, AtrStaff.GestaoDePessoas, AtrStaff.Motivacao },
            [FuncaoStaff.Fisioterapeuta] = new[] { AtrStaff.Fisioterapia, AtrStaff.PreparoFisico },
            [FuncaoStaff.PreparadorFisico] = new[] { AtrStaff.PreparoFisico, AtrStaff.Fisioterapia, AtrStaff.Motivacao },
            [FuncaoStaff.Olheiro] = new[] { AtrStaff.AvaliarHabilidade, AtrStaff.AvaliarPotencial, AtrStaff.AnaliseDeDados },
        };
        public override string ToString() => $"{nome} ({Nome(funcao)}, {pais}, {idade})";
    }

    /// <summary>
    /// O que o staff de um time faz pelos jogadores. Tudo é multiplicador perto de 1: staff 10 = 1,0; 20 = até 1,4; 1 = 0,64.
    /// Sem staff (null) = 1 em tudo (é o que o jogo faz hoje).
    /// </summary>
    public class Equipe
    {
        public readonly List<Staff> staff = new List<Staff>();
        /// <summary>O melhor do staff nesse atributo (quem tem a função certa costuma ser o melhor).</summary>
        public float Melhor(AtrStaff a) => staff.Count == 0 ? 10f : staff.Max(s => s[a]);
        static float Mult(float x) => 0.6f + 0.04f * x;   // 10 → 1,0; 20 → 1,4; 1 → 0,64

        /// <summary>Velocidade de treino/evolução de um atributo do jogador.</summary>
        public float TreinoDe(Atr a)
        {
            AtrStaff s = a switch
            {
                Atr.Mira or Atr.Reflexo or Atr.Flick or Atr.Spray or Atr.MiraPosicionada or Atr.Movimento or Atr.Awp => AtrStaff.TreinoMecanico,
                Atr.Utilitaria => AtrStaff.TreinoUtilitaria,
                Atr.Decisoes or Atr.LeituraDeJogo or Atr.Posicionamento or Atr.TrabalhoEmEquipe or Atr.Comunicacao or Atr.Lideranca => AtrStaff.TreinoTatico,
                Atr.Resistencia => AtrStaff.PreparoFisico,
                _ => AtrStaff.TreinoMental,
            };
            return Mult(Melhor(s));
        }
        /// <summary>Velocidade de ganhar familiaridade numa função (treino tático).</summary>
        public float Familiaridade => Mult(Melhor(AtrStaff.TreinoTatico));
        /// <summary>Velocidade de aprender uma característica: tática pras de jogo coletivo, mental pras de jeito.</summary>
        public float Caracteristica(Caracteristica c) => Mult(Melhor(c == CSM.Sim.Caracteristica.SegueACall || c == CSM.Sim.Caracteristica.JogaColadoNoTime || c == CSM.Sim.Caracteristica.PreNade || c == CSM.Sim.Caracteristica.RotacionaCedo || c == CSM.Sim.Caracteristica.AncoraTeimosa ? AtrStaff.TreinoTatico : AtrStaff.TreinoMental));
        /// <summary>Quanto o momento ruim afunda (1 = como sem staff; psicólogo e gestão bons seguram até ~25%).</summary>
        public float AmorteceTilt => 1f - 0.25f * Math.Clamp((MathF.Max(Melhor(AtrStaff.Psicologia), Melhor(AtrStaff.GestaoDePessoas)) - 10f) / 10f, -1f, 1f);
        /// <summary>Pra onde o clima do elenco anda sozinho por semana (gestão + motivação + psicologia).</summary>
        public float ClimaAlvo => Math.Clamp((Melhor(AtrStaff.GestaoDePessoas) + Melhor(AtrStaff.Motivacao) + Melhor(AtrStaff.Psicologia) - 30f) / 30f, -0.5f, 0.6f);
        /// <summary>Recuperação de cansaço/lesão (quando existir): fisioterapia + preparo físico.</summary>
        public float Recuperacao => Mult((Melhor(AtrStaff.Fisioterapia) + Melhor(AtrStaff.PreparoFisico)) / 2f);
        /// <summary>Qualidade do relatório do adversário (0..1). Gancho futuro: a crença inicial da Leitura, com A/B.</summary>
        public float Preparo => Math.Clamp((Melhor(AtrStaff.AnaliseDeAdversario) - 1f) / 19f, 0f, 1f);
    }

    /// <summary>O olheiro: quão certo é o número que a tela mostra de um jogador que não é do seu time.</summary>
    public static class Olheiro
    {
        /// <summary>O atributo como o olheiro vê (erro maior com olheiro fraco; sem olheiro = erro de um olheiro 6).
        /// Mesmo jogador + mesmo olheiro = mesma leitura (não muda a cada clique).</summary>
        public static int Ver(Ficha f, Atr a, Staff olheiro)
        {
            float av = olheiro == null ? 6f : olheiro[AtrStaff.AvaliarHabilidade];
            var rng = new Rng(f.id * 7349 + (olheiro?.id ?? 0) * 211 + (int)a * 31);
            float sd = (20f - av) / 8f;   // 20 → exato; 12 → ±1; 4 → ±2
            return (int)MathF.Round(MathF.Max(1f, MathF.Min(20f, f.atr[(int)a] + rng.Normal(0f, sd))));
        }
        /// <summary>Potencial estimado (o teto médio dos atributos da função), em estrelas de 0,5 a 5.</summary>
        public static float Potencial(Ficha f, Staff olheiro)
        {
            float av = olheiro == null ? 6f : olheiro[AtrStaff.AvaliarPotencial];
            var rng = new Rng(f.id * 9187 + (olheiro?.id ?? 0) * 17);
            float teto = Atributos.PesoNaFuncao[f.funcao].Sum(x => f.teto[(int)x.a] * x.w) / Atributos.PesoNaFuncao[f.funcao].Sum(x => x.w);
            float est = teto + rng.Normal(0f, (20f - av) / 6f);
            return MathF.Round(Math.Clamp(est / 4f, 0.5f, 5f) * 2f) / 2f;
        }
    }

    public class Clube
    {
        public int id; public string nome, sigla, pais; public int tier; public float reputacao, clima;
        public readonly List<Ficha> elenco = new List<Ficha>();      // os 5 primeiros são os titulares
        public readonly Equipe equipe = new Equipe();
        public Ficha[] Titulares => elenco.Take(5).ToArray();
        public float Media => elenco.Take(5).Average(f => f.Habilidade);
        public override string ToString() => $"{nome} [{sigla}] ({pais}, tier {tier}) média {Media:0.0}";
    }

    /// <summary>
    /// Escalação: escolhe a função de cada titular no TR (Entry, Suporte, Lurker, AWP, IGL) e a posição no CT
    /// (âncora do A, rotação do A, AWP no meio, âncora do B, suporte do B) somando a nota de cada um em cada lugar.
    /// O AWPer é o mesmo nos dois lados. As posições do CT pesam um pouco diferente do papel puro: o A da Vila é mais
    /// duelo de mira (Mira, Reflexo); o B é mais segurar e granada (Utilitária, Posicionamento, Calma).
    /// </summary>
    public static class Escalacao
    {
        public static readonly Role[] FuncoesT = { Role.Entry, Role.Suporte, Role.Lurker, Role.Awper, Role.Igl };
        public static readonly (Role r, char site)[] PosicoesCT = { (Role.Ancora, 'A'), (Role.Rotacao, 'A'), (Role.Awper, 'M'), (Role.Ancora, 'B'), (Role.Suporte, 'B') };

        public static float NotaNaPosicaoCT(Ficha f, Role r, char site)
        {
            float nota = f.NotaNaFuncao(r);
            if (r == Role.Awper) return nota;
            float lado = site == 'A' ? (f[Atr.Mira] + f[Atr.Reflexo]) / 2f : site == 'B' ? (f[Atr.Utilitaria] + f[Atr.Posicionamento] + f[Atr.Calma]) / 3f : nota;
            float bonus = 0f;
            if (r == Role.Ancora && f.Tem(Caracteristica.AncoraTeimosa)) bonus += 0.4f;
            if (r == Role.Rotacao && f.Tem(Caracteristica.RotacionaCedo)) bonus += 0.4f;
            return 0.75f * nota + 0.25f * lado + bonus;
        }

        /// <summary>A melhor escalação dos 5: índice do jogador em cada função de TR e em cada posição de CT.</summary>
        public static (int[] t, int[] ct, float nota) Melhor(Ficha[] js)
        {
            int[] bestT = null, bestCT = null; float best = float.MinValue;
            foreach (var pt in Permutacoes(5))
            {
                int awp = pt[3];   // quem é AWPer no TR é o AWPer do CT
                float st = 0; for (int i = 0; i < 5; i++) st += js[pt[i]].NotaNaFuncao(FuncoesT[i]);
                var resto = Enumerable.Range(0, 5).Where(x => x != awp).ToArray();
                foreach (var pc in Permutacoes(4))
                {
                    var ct = new int[5]; int q = 0;
                    for (int k = 0; k < 5; k++) ct[k] = k == 2 ? awp : resto[pc[q++]];
                    float sc = 0; for (int k = 0; k < 5; k++) sc += NotaNaPosicaoCT(js[ct[k]], PosicoesCT[k].r, PosicoesCT[k].site);
                    if (st + sc > best) { best = st + sc; bestT = (int[])pt.Clone(); bestCT = ct; }
                }
            }
            return (bestT, bestCT, best / 10f);
        }

        /// <summary>Põe a escalação num time da Partida: jogador i do elenco da partida recebe a ficha, a função de TR e a de CT.
        /// A ordem dos jogadores da Partida segue as funções de TR do time A (Entry, Suporte, Lurker, AWP, IGL) ou do B
        /// (IGL, Entry, AWP, Lurker, Suporte), então a ficha certa vai pra cada lugar.</summary>
        public static void Aplicar(Partida.Time tm, Ficha[] js, bool timeA)
        {
            var (t, ct, _) = Melhor(js);
            Role[] ordemT = timeA ? FuncoesT : new[] { Role.Igl, Role.Entry, Role.Awper, Role.Lurker, Role.Suporte };
            for (int i = 0; i < 5; i++)
            {
                int quem = t[Array.IndexOf(FuncoesT, ordemT[i])];
                int pos = Array.IndexOf(ct, quem);
                var j = tm.js[i];
                j.ficha = js[quem]; j.roleT = ordemT[i]; j.roleCT = PosicoesCT[pos].r; j.siteCT = PosicoesCT[pos].site;
            }
        }

        static IEnumerable<int[]> Permutacoes(int n)
        {
            var a = Enumerable.Range(0, n).ToArray();
            IEnumerable<int[]> Rec(int k)
            {
                if (k == n) { yield return (int[])a.Clone(); yield break; }
                for (int i = k; i < n; i++)
                {
                    (a[k], a[i]) = (a[i], a[k]);
                    foreach (var p in Rec(k + 1)) yield return p;
                    (a[k], a[i]) = (a[i], a[k]);
                }
            }
            return Rec(0);
        }
    }

    /// <summary>Gera os times e o staff a partir da base de jogadores (mesma semente = mesmo mundo).</summary>
    public static class BaseClubes
    {
        static readonly string[] Palavras =
        {
            "Granito", "Corvo", "Maré", "Lince", "Tundra", "Ferrugem", "Eclipse", "Brasa", "Trovão", "Basalto", "Cobalto", "Ciclone", "Jaguatirica", "Coruja",
            "Âmbar", "Obsidiana", "Nevasca", "Sertão", "Pampa", "Fiorde", "Estepe", "Monção", "Quasar", "Pulsar", "Nêmesis", "Tempestade", "Graúna", "Carcará",
            "Hydra", "Orca", "Bisão", "Raposa", "Vespa", "Escorpião", "Cometa", "Aurora", "Zênite", "Vórtice", "Magma", "Granada", "Fênix", "Vulcão", "Rocha",
            "Kestrel", "Halcyon", "Ironclad", "Northwind", "Bluefin", "Redline", "Nightjar", "Stormbreak", "Wolfram", "Ashfall", "Cinder", "Rift", "Vanta",
            "Zephyr", "Onyx", "Talon", "Glacier", "Saber", "Torrent", "Ember", "Quill", "Basilisk", "Mantis", "Krait", "Osprey", "Polaris", "Sable", "Umbra",
        };
        static readonly string[] Sufixos = { "Esports", "Gaming", "Club", "Team", "e-Sports", "GG", "Clan" };
        // nomes de organizações reais (nunca sair igual por acaso)
        static readonly HashSet<string> Reais = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "furia", "navi", "natus vincere", "vitality", "faze", "g2", "mibr", "liquid", "astralis", "heroic", "spirit", "mouz", "imperial", "pain", "cloud9",
            "complexity", "fnatic", "nip", "ninjas in pyjamas", "ence", "big", "eternal fire", "virtus.pro", "falcons", "3dmax", "monte", "legacy", "oddik",
            "sharks", "red canids", "flamengo", "the mongolz", "saw", "apeks", "gamerlegion", "betboom", "aurora", "mongolz", "9z", "w7m", "fluxo", "case",
        };

        static string NomeClube(Rng rng, HashSet<string> usados, int tier)
        {
            for (int t = 0; ; t++)
            {
                string w = Palavras[rng.Int(Palavras.Length)];
                string n = tier == 4 && rng.Rand() < 0.5f ? w + " Academy" : rng.Rand() < 0.35f ? w : w + " " + Sufixos[rng.Int(Sufixos.Length)];
                if (t > 30) n += " " + (2 + rng.Int(8));
                if (Reais.Contains(n) || Reais.Contains(w) && n == w || usados.Contains(n) || tier == 1 && t <= 30 && usados.Contains("#" + w)) continue;   /* elite: uma palavra por time (sem "Ferrugem" e "Ferrugem Club") */
                usados.Add(n); usados.Add("#" + w); return n;
            }
        }
        static string Sigla(string nome)
        {
            var ps = nome.Split(' ').Where(p => p.Length > 0 && char.IsLetter(p[0])).ToArray();
            string s = ps.Length >= 2 ? (ps[0].Substring(0, Math.Min(3, ps[0].Length)) + ps[1][0]) : ps[0].Substring(0, Math.Min(4, ps[0].Length));
            var semAcento = new string(s.Normalize(NormalizationForm.FormD).Where(ch => System.Globalization.CharUnicodeInfo.GetUnicodeCategory(ch) != System.Globalization.UnicodeCategory.NonSpacingMark).ToArray());
            return semAcento.ToUpperInvariant();
        }

        static readonly (FuncaoStaff f, float[] chance)[] StaffPorTier =   // chance de ter por tier 1..4
        {
            (FuncaoStaff.Tecnico, new[] { 1f, 1f, 1f, 0.9f }), (FuncaoStaff.Assistente, new[] { 0.9f, 0.5f, 0.15f, 0f }),
            (FuncaoStaff.Analista, new[] { 1f, 0.8f, 0.45f, 0.1f }), (FuncaoStaff.Analista, new[] { 0.6f, 0.1f, 0f, 0f }),
            (FuncaoStaff.TreinadorDeMira, new[] { 0.7f, 0.3f, 0.05f, 0f }), (FuncaoStaff.Psicologo, new[] { 0.85f, 0.4f, 0.1f, 0f }),
            (FuncaoStaff.Fisioterapeuta, new[] { 0.8f, 0.35f, 0.05f, 0f }), (FuncaoStaff.PreparadorFisico, new[] { 0.6f, 0.2f, 0f, 0f }),
            (FuncaoStaff.Olheiro, new[] { 0.9f, 0.6f, 0.25f, 0.05f }),
        };
        static readonly float[] MediaStaff = { 0, 14.5f, 12.5f, 10.5f, 8.5f };

        public static Staff GerarStaff(Rng rng, int id, FuncaoStaff funcao, int tier, string pais, Func<string> nome)
        {
            var s = new Staff { id = id, funcao = funcao, pais = pais, nome = nome(), idade = (int)Math.Clamp(rng.Normal(36f, 7f), 23f, 65f) };
            float g = rng.Normal(MediaStaff[tier], 1.2f);
            var chave = Staff.Chave[funcao];
            for (int k = 0; k < Staff.N; k++)
            {
                var a = (AtrStaff)k; int ix = Array.IndexOf(chave, a);
                float b = ix < 0 ? -3f : ix == 0 ? 2f : 1f;
                s.atr[k] = MathF.Max(1f, MathF.Min(20f, rng.Normal(g + b, 1.6f)));
            }
            return s;
        }

        /// <summary>Monta os times: cada tier vira clubes de 6 (5 titulares + 1 reserva), com IGL, AWP, entry, suporte e
        /// lurker; o país do clube puxa jogadores de lá (60%). Sobra = sem time (agente livre).</summary>
        public static (List<Clube> clubes, List<Ficha> livres, List<Staff> staffLivre) Gerar(List<Ficha> base_, int seed)
        {
            var rng = new Rng(seed * 101 + 9); var usados = new HashSet<string>(); var clubes = new List<Clube>(); var livres = new List<Ficha>();
            int sid = 1;
            foreach (var g in base_.GroupBy(f => f.tier).OrderBy(g => g.Key))
            {
                var pool = g.OrderBy(_ => rng.Rand()).ToList();
                int nClubes = pool.Count / 6;
                for (int c = 0; c < nClubes; c++)
                {
                    var cl = new Clube { id = clubes.Count + 1, tier = g.Key };
                    var ancora = pool.FirstOrDefault(f => f.funcao == Role.Igl) ?? pool[0];
                    cl.pais = ancora.pais;
                    foreach (var fn in Escalacao.FuncoesT)
                    {
                        var cand = pool.Where(f => f.funcao == fn).ToList();
                        if (cand.Count == 0) cand = pool.ToList();
                        var mesmoPais = cand.Where(f => f.pais == cl.pais).ToList();
                        var pick = mesmoPais.Count > 0 && rng.Rand() < 0.6f ? mesmoPais[rng.Int(mesmoPais.Count)] : cand[rng.Int(cand.Count)];
                        cl.elenco.Add(pick); pool.Remove(pick);
                    }
                    if (pool.Count > 0) { var res = pool[rng.Int(pool.Count)]; cl.elenco.Add(res); pool.Remove(res); }
                    cl.nome = NomeClube(rng, usados, g.Key); cl.sigla = Sigla(cl.nome);
                    cl.reputacao = MathF.Max(1f, MathF.Min(20f, cl.Media + rng.Normal(0f, 1f)));
                    cl.clima = rng.Uniform(-0.3f, 0.5f);
                    string paisStaff = cl.pais;
                    foreach (var (fs, ch) in StaffPorTier)
                        if (rng.Rand() < ch[g.Key - 1])
                            cl.equipe.staff.Add(GerarStaff(rng, sid++, fs, g.Key, paisStaff, () => BaseJogadores.NomeDe(paisStaff, rng)));
                    clubes.Add(cl);
                }
                livres.AddRange(pool);
            }
            var staffLivre = new List<Staff>();
            for (int i = 0; i < 300; i++)
            {
                int tier = 1 + rng.Int(4); var fs = StaffPorTier[rng.Int(StaffPorTier.Length)].f; string pais = BaseJogadores.PaisAleatorio(rng);
                staffLivre.Add(GerarStaff(rng, sid++, fs, tier, pais, () => BaseJogadores.NomeDe(pais, rng)));
            }
            return (clubes, livres, staffLivre);
        }

        public static string Resumo(List<Clube> clubes, List<Ficha> livres, List<Staff> staffLivre)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"TIMES {clubes.Count} | jogadores sem time {livres.Count} | staff sem time {staffLivre.Count}");
            foreach (var g in clubes.GroupBy(c => c.tier).OrderBy(g => g.Key))
                sb.AppendLine($"  tier {g.Key}: {g.Count(),4} times | média do titular {g.Average(c => c.Media):0.0} | staff por time {g.Average(c => c.equipe.staff.Count):0.0} | técnico médio {g.Average(c => c.equipe.Melhor(AtrStaff.Tatica)):0.0}");
            sb.AppendLine("  5 melhores times:");
            foreach (var c in clubes.OrderByDescending(c => c.Media).Take(5))
            {
                sb.AppendLine($"    {c} | clima {c.clima:+0.00;-0.00}");
                var (t, ct, nota) = Escalacao.Melhor(c.Titulares);
                var tit = c.Titulares;
                sb.AppendLine("      TR: " + string.Join(", ", Enumerable.Range(0, 5).Select(i => $"{tit[t[i]].nick} {Escalacao.FuncoesT[i]}")));
                sb.AppendLine("      CT: " + string.Join(", ", Enumerable.Range(0, 5).Select(k => $"{tit[ct[k]].nick} {Escalacao.PosicoesCT[k].r}-{Escalacao.PosicoesCT[k].site}")));
                sb.AppendLine("      staff: " + string.Join(", ", c.equipe.staff.Select(s => $"{Staff.Nome(s.funcao)} {s.atr.Max():0}")));
            }
            return sb.ToString();
        }
    }
}
