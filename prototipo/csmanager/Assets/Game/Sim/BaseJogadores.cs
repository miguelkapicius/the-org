using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSM.Sim
{
    /// <summary>
    /// Base de jogadores FAKE (nunca nome real: sem licença). Gerada por semente, então o jogo pode recriar a mesma base sem
    /// arquivo. Distribuição por nível (tier 1 = elite mundial .. 4 = academia/semi-pro), função natural, idade, país e
    /// características típicas da função. O tier 1 médio fica perto de 15, que é o jogador padrão da sim de hoje.
    /// </summary>
    public static class BaseJogadores
    {
        // fatia de cada tier e a média de talento (pontos de atributo)
        static readonly (int tier, float fatia, float media)[] Tiers = { (1, 0.04f, 15.0f), (2, 0.12f, 13.3f), (3, 0.34f, 11.6f), (4, 0.50f, 9.6f) };
        // função natural de TR (um IGL por time, AWPer por time, o resto dividido)
        static readonly (Role r, float p)[] FuncoesT = { (Role.Entry, 0.22f), (Role.Suporte, 0.24f), (Role.Lurker, 0.16f), (Role.Awper, 0.20f), (Role.Igl, 0.18f) };
        // agressividade típica de quem nasce em cada função (jeito, não qualidade)
        static float AgressividadeDe(Role r) => r switch { Role.Entry => 17f, Role.Rotacao => 15f, Role.Igl => 14f, Role.Suporte => 13.5f, Role.Awper => 12.5f, Role.Ancora => 12f, Role.Lurker => 11.5f, _ => 14f };

        // países (peso aproximado do cenário) com nomes e sobrenomes genéricos — inventados, sem pessoa real por trás
        static readonly (string pais, float p, string[] nomes, string[] sobrenomes)[] Paises =
        {
            ("Brasil", 0.16f, new[] { "Lucas", "Gabriel", "Rafael", "Matheus", "Pedro", "Thiago", "Bruno", "Vinícius", "Felipe", "Gustavo", "Caio", "Diego", "Leonardo", "Rodrigo", "Henrique" }, new[] { "Almeida", "Barbosa", "Cardoso", "Duarte", "Fernandes", "Gomes", "Lima", "Moreira", "Nogueira", "Pereira", "Ramos", "Siqueira", "Teixeira", "Vieira", "Xavier" }),
            ("Rússia", 0.09f, new[] { "Dmitri", "Sergei", "Alexei", "Ivan", "Nikita", "Pavel", "Mikhail", "Artem", "Kirill", "Yegor" }, new[] { "Volkov", "Sokolov", "Lebedev", "Kozlov", "Novikov", "Morozov", "Pavlov", "Orlov", "Belov", "Zaitsev" }),
            ("Ucrânia", 0.05f, new[] { "Oleksandr", "Andriy", "Bohdan", "Taras", "Yurii", "Maksym", "Denys", "Vitalii" }, new[] { "Kovalenko", "Bondarenko", "Tkachenko", "Kravchenko", "Melnyk", "Shevchuk", "Lysenko", "Marchenko" }),
            ("Dinamarca", 0.06f, new[] { "Mads", "Frederik", "Rasmus", "Mikkel", "Emil", "Jonas", "Magnus", "Oliver" }, new[] { "Kjær", "Holm", "Lund", "Bruun", "Dahl", "Vestergaard", "Krog", "Skov" }),
            ("Suécia", 0.05f, new[] { "Erik", "Linus", "Oskar", "Viktor", "Albin", "Hugo", "Isak", "Filip" }, new[] { "Lindqvist", "Berglund", "Sandberg", "Ekström", "Holmgren", "Nyberg", "Åberg", "Wikström" }),
            ("França", 0.06f, new[] { "Théo", "Hugo", "Mathis", "Lucas", "Nathan", "Enzo", "Maxime", "Romain" }, new[] { "Lefèvre", "Moreau", "Girard", "Bonnet", "Fontaine", "Mercier", "Rousseau", "Faure" }),
            ("Polônia", 0.05f, new[] { "Jakub", "Kacper", "Mateusz", "Bartosz", "Piotr", "Szymon", "Michał", "Wiktor" }, new[] { "Nowak", "Wójcik", "Kamiński", "Lewandowicz", "Zieliński", "Szymański", "Dąbrowski", "Kozłowski" }),
            ("Finlândia", 0.03f, new[] { "Aleksi", "Eetu", "Joonas", "Lauri", "Onni", "Veeti" }, new[] { "Virtanen", "Mäkinen", "Nieminen", "Heikkinen", "Koskinen", "Laine" }),
            ("Noruega", 0.02f, new[] { "Sander", "Henrik", "Kristian", "Sindre", "Vegard" }, new[] { "Haugen", "Berg", "Strand", "Moen", "Solberg" }),
            ("Alemanha", 0.04f, new[] { "Jonas", "Leon", "Felix", "Paul", "Niklas", "Tim" }, new[] { "Bauer", "Keller", "Hoffmann", "Schreiber", "Krüger", "Vogel" }),
            ("Cazaquistão", 0.03f, new[] { "Arman", "Dauren", "Nurlan", "Yerlan", "Timur" }, new[] { "Abenov", "Saparov", "Zhakupov", "Nurlanov", "Omarov" }),
            ("Mongólia", 0.02f, new[] { "Bat-Erdene", "Temuulen", "Ganbold", "Enkhjin", "Munkh" }, new[] { "Batbayar", "Ganzorig", "Erdenebat", "Tsogt", "Bold" }),
            ("EUA", 0.07f, new[] { "Tyler", "Jake", "Ryan", "Kyle", "Austin", "Brandon", "Ethan", "Logan" }, new[] { "Carter", "Mitchell", "Brooks", "Hayes", "Reed", "Coleman", "Sullivan", "Parker" }),
            ("Canadá", 0.02f, new[] { "Liam", "Noah", "Owen", "Carter" }, new[] { "Tremblay", "Gagnon", "Roy", "Fraser" }),
            ("Argentina", 0.03f, new[] { "Santiago", "Tomás", "Joaquín", "Facundo", "Nicolás" }, new[] { "González", "Romero", "Sosa", "Benítez", "Acosta" }),
            ("Chile", 0.01f, new[] { "Benjamín", "Vicente", "Martín" }, new[] { "Muñoz", "Rojas", "Contreras" }),
            ("Austrália", 0.03f, new[] { "Jack", "Cooper", "Riley", "Mitchell" }, new[] { "Walsh", "Kelly", "Ryan", "Murphy" }),
            ("China", 0.03f, new[] { "Wei", "Hao", "Jun", "Lei", "Chen" }, new[] { "Zhang", "Liu", "Huang", "Zhao", "Zhou" }),
            ("Turquia", 0.03f, new[] { "Emre", "Burak", "Kerem", "Mert", "Arda" }, new[] { "Yılmaz", "Kaya", "Demir", "Şahin", "Çelik" }),
            ("Portugal", 0.02f, new[] { "João", "Tiago", "Rui", "Diogo" }, new[] { "Sousa", "Martins", "Pinto", "Correia" }),
            ("Israel", 0.01f, new[] { "Noam", "Itai", "Omer" }, new[] { "Levi", "Mizrahi", "Peretz" }),
            ("Estônia", 0.01f, new[] { "Karl", "Rasmus", "Markus" }, new[] { "Tamm", "Saar", "Kask" }),
            ("Letônia", 0.01f, new[] { "Jānis", "Roberts", "Kārlis" }, new[] { "Bērziņš", "Ozols", "Liepa" }),
            ("Sérvia", 0.02f, new[] { "Nikola", "Stefan", "Luka", "Marko" }, new[] { "Jovanović", "Petrović", "Nikolić", "Marković" }),
            ("Bósnia", 0.01f, new[] { "Adnan", "Emir", "Haris" }, new[] { "Hodžić", "Kovačević", "Begić" }),
            ("Eslováquia", 0.01f, new[] { "Martin", "Tomáš", "Peter" }, new[] { "Horváth", "Kováč", "Varga" }),
            ("República Tcheca", 0.02f, new[] { "Jakub", "Ondřej", "Vojtěch" }, new[] { "Novák", "Svoboda", "Dvořák" }),
            ("Bélgica", 0.01f, new[] { "Arthur", "Louis", "Jules" }, new[] { "Peeters", "Janssens", "Maes" }),
        };

        // sílabas pra nick (inventados). Lista de nicks famosos reais pra nunca sair igual por acaso.
        static readonly string[] Ini = { "kr", "z", "v", "br", "sh", "t", "m", "n", "fl", "dr", "k", "x", "j", "r", "s", "g", "pl", "st", "qu", "h", "l", "b", "c", "f", "w", "y", "tr", "gr", "sn", "bl" };
        static readonly string[] Nuc = { "a", "e", "i", "o", "u", "ae", "io", "y", "ou", "ei", "ar", "ex", "on", "ul", "ir" };
        static readonly string[] Fim = { "", "", "x", "n", "k", "z", "r", "s", "ck", "th", "lo", "ra", "do", "ti", "zy", "ze", "v", "nn", "sh", "m" };
        static readonly HashSet<string> NicksReais = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "s1mple", "zywoo", "niko", "device", "dev1ce", "fallen", "coldzera", "fer", "fnx", "taco", "kscerato", "yuurih", "art", "arT", "chelo", "vini",
            "zeus", "electronic", "b1t", "perfecto", "boombl4", "flamie", "edward", "guardian", "kennys", "shox", "apex", "rain", "karrigan", "ropz",
            "broky", "twistzz", "elige", "naf", "stewie2k", "tarik", "jw", "flusha", "olofmeister", "krimz", "get_right", "forest", "gla1ve", "xyp9x",
            "dupreeh", "magisk", "blamef", "stavn", "jabbi", "m0nesy", "donk", "sh1ro", "ax1le", "hobbit", "interz", "jl", "im", "jimpphat", "frozen",
            "huntr", "huNter", "nexa", "malbsmd", "spinx", "torzsi", "xantares", "woxic", "kyojin", "mezii", "siuhy", "jks", "aleksib", "hooxi",
            "snappi", "cadian", "sunpayus", "blitz", "techno", "mzinho", "senzu", "910", "brollan", "headtr1ck", "zont1x", "zorte", "magixx", "chopper",
            "fame", "flameZ", "molodoy", "jame", "qikert", "ultimate", "drop", "saffee", "lucaozy", "hen1", "felps", "nqz", "exit", "biguzera",
            "hardzao", "dumau", "latto", "skullz", "try", "insani", "noway", "kng", "boltz", "lnz", "story", "dgt", "sener1", "xertion", "nertz",
        };

        static string Nick(Rng rng, HashSet<string> usados)
        {
            for (int tent = 0; ; tent++)
            {
                var sb = new StringBuilder();
                int sil = 1 + rng.Int(2);
                for (int k = 0; k < sil; k++) { sb.Append(Ini[rng.Int(Ini.Length)]); sb.Append(Nuc[rng.Int(Nuc.Length)]); }
                sb.Append(Fim[rng.Int(Fim.Length)]);
                string n = sb.ToString();
                // um pouco de "leet" e número, como no cenário
                if (rng.Rand() < 0.18f) n = n.Replace('i', '1');
                else if (rng.Rand() < 0.10f) n = n.Replace('o', '0');
                if (rng.Rand() < 0.08f) n += rng.Int(10);
                if (rng.Rand() < 0.12f) n = char.ToUpper(n[0]) + n.Substring(1);
                if (n.Length < 3 || NicksReais.Contains(n) || usados.Contains(n.ToLowerInvariant())) { if (tent > 50) n += rng.Int(100); else continue; }
                usados.Add(n.ToLowerInvariant()); return n;
            }
        }

        /// <summary>Um nome genérico do país (pro staff e afins).</summary>
        public static string NomeDe(string pais, Rng rng)
        {
            var p = Paises.FirstOrDefault(x => x.pais == pais); if (p.nomes == null) p = Paises[0];
            return p.nomes[rng.Int(p.nomes.Length)] + " " + p.sobrenomes[rng.Int(p.sobrenomes.Length)];
        }
        public static string PaisAleatorio(Rng rng) => Sorteia(rng, Paises.Select(p => (p.pais, p.p)).ToList());

        static T Sorteia<T>(Rng rng, IList<(T x, float p)> lista)
        {
            float tot = lista.Sum(e => e.p), u = rng.Rand() * tot;
            foreach (var e in lista) { u -= e.p; if (u <= 0) return e.x; }
            return lista[lista.Count - 1].x;
        }
        static float Cl(float x) => MathF.Max(1f, MathF.Min(20f, x));

        /// <summary>Gera n jogadores com a semente (mesma semente = mesma base).</summary>
        public static List<Ficha> Gerar(int seed, int n)
        {
            var rng = new Rng(seed * 31 + 7); var usados = new HashSet<string>(); var lista = new List<Ficha>(n);
            var paises = Paises.Select(p => (p, p.p)).ToList();
            for (int i = 0; i < n; i++)
            {
                // tier pela fatia
                float u = (i + 0.5f) / n; float acc = 0; var tr = Tiers[Tiers.Length - 1];
                foreach (var t in Tiers) { acc += t.fatia; if (u < acc) { tr = t; break; } }
                var f = new Ficha { id = i + 1, tier = tr.tier };
                f.funcao = Sorteia(rng, FuncoesT);
                f.funcaoCT = f.funcao switch
                {
                    Role.Awper => Role.Awper, Role.Lurker => Role.Ancora, Role.Entry => rng.Rand() < 0.7f ? Role.Rotacao : Role.Ancora,
                    Role.Igl => rng.Rand() < 0.6f ? Role.Suporte : Role.Rotacao, _ => rng.Rand() < 0.55f ? Role.Ancora : Role.Suporte,
                };
                var pais = Sorteia(rng, paises);
                f.pais = pais.pais; f.nome = pais.nomes[rng.Int(pais.nomes.Length)] + " " + pais.sobrenomes[rng.Int(pais.sobrenomes.Length)];
                f.nick = Nick(rng, usados);
                f.idade = (int)MathF.Round(tr.tier == 4 ? MathF.Max(15, MathF.Min(26, rng.Normal(18.6f, 1.8f))) : MathF.Max(17, MathF.Min(35, rng.Normal(24f, 3.4f))));

                // talento geral do jogador + perfil da função (as duas funções naturais puxam os atributos delas)
                float g = rng.Normal(tr.media, 1.0f);
                var bonus = new float[Atributos.N];
                foreach (var (a, w) in Atributos.PesoNaFuncao[f.funcao]) bonus[(int)a] += w switch { >= 4 => 2.0f, >= 3 => 1.5f, >= 2 => 0.8f, _ => 0.3f };
                foreach (var (a, w) in Atributos.PesoNaFuncao[f.funcaoCT]) bonus[(int)a] += 0.5f * (w switch { >= 4 => 2.0f, >= 3 => 1.5f, >= 2 => 0.8f, _ => 0.3f });
                for (int k = 0; k < Atributos.N; k++)
                {
                    var a = (Atr)k;
                    if (Atributos.Personalidade(a)) continue;
                    float b = bonus[k] > 0 ? bonus[k] : -0.4f;
                    if (a == Atr.Awp && f.funcao != Role.Awper) b = -3.5f;      // AWP de quem não é AWPer é bem pior
                    if (a == Atr.Lideranca && f.funcao != Role.Igl) b = -1.0f;
                    f.atr[k] = Cl(rng.Normal(g + b, 1.4f));
                }
                f[Atr.Agressividade] = Cl(rng.Normal(AgressividadeDe(f.funcao), 2f));
                foreach (var a in Atributos.Ocultos) f[a] = Cl(rng.Normal(a == Atr.Profissionalismo ? 11.5f + (4 - tr.tier) : 11.5f, 3.5f));
                f[Atr.Resistencia] = Cl(rng.Normal(g - 0.5f, 1.8f));
                // idade: a mecânica já caiu um pouco em quem passou dos 28
                if (f.idade > 28) foreach (var a in Atributos.Tecnicos) if (Atributos.Mecanico(a)) f[a] = f[a] - 0.35f * (f.idade - 28);
                // teto (oculto): jovem tem espaço pra crescer; o "wonderkid" da academia pode chegar na elite
                float espaco = MathF.Max(0f, 25f - f.idade) * rng.Uniform(0.15f, 0.75f) + rng.Uniform(0f, 1.2f);
                for (int k = 0; k < Atributos.N; k++) f.teto[k] = Atributos.Personalidade((Atr)k) ? f.atr[k] : Cl(f.atr[k] + espaco * rng.Uniform(0.6f, 1.2f));

                // familiaridade: natural alta, a outra do lado CT média-alta, as parecidas médias, o resto baixo
                for (int k = 0; k < 7; k++) f.fam[k] = Cl(rng.Uniform(2f, 8f));
                f.fam[(int)f.funcao] = Cl(rng.Uniform(17f, 20.5f));
                f.fam[(int)f.funcaoCT] = MathF.Max(f.fam[(int)f.funcaoCT], Cl(rng.Uniform(15f, 20.5f)));
                Role parecida = f.funcao switch { Role.Entry => Role.Rotacao, Role.Lurker => Role.Rotacao, Role.Suporte => Role.Igl, Role.Igl => Role.Suporte, _ => Role.Ancora };
                f.fam[(int)parecida] = MathF.Max(f.fam[(int)parecida], Cl(rng.Uniform(9f, 15f)));
                if (f[Atr.Lideranca] >= 14 && f.funcao != Role.Igl) f.fam[(int)Role.Igl] = MathF.Max(f.fam[(int)Role.Igl], Cl(rng.Uniform(6f, 13f)));

                // características: 1–3 típicas da função + às vezes uma qualquer
                var tipicas = Atributos.TipicasDaFuncao[f.funcao].Concat(Atributos.TipicasDaFuncao[f.funcaoCT]).Distinct().ToList();
                int nc = 1 + (rng.Rand() < 0.55f ? 1 : 0) + (rng.Rand() < 0.2f ? 1 : 0);
                for (int k = 0; k < nc && tipicas.Count > 0; k++) { int q = rng.Int(tipicas.Count); f.Aprende(tipicas[q]); tipicas.RemoveAt(q); }
                if (rng.Rand() < 0.25f) f.Aprende((Caracteristica)rng.Int(Enum.GetValues(typeof(Caracteristica)).Length));

                f.moral = rng.Uniform(-0.2f, 0.3f); f.forma = rng.Uniform(-0.2f, 0.2f);
                lista.Add(f);
            }
            return lista;
        }

        /// <summary>CSV pra conferir a base (uma linha por jogador).</summary>
        public static string Csv(IEnumerable<Ficha> fs)
        {
            var sb = new StringBuilder();
            sb.Append("id;nick;nome;pais;idade;tier;funcaoT;funcaoCT;hab;melhorFuncao");
            for (int k = 0; k < Atributos.N; k++) sb.Append(';').Append(((Atr)k).ToString());
            for (int k = 0; k < 7; k++) sb.Append(";fam").Append(((Role)k).ToString());
            sb.Append(";caracteristicas\n");
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var f in fs)
            {
                sb.Append(string.Join(";", f.id, f.nick, f.nome, f.pais, f.idade, f.tier, f.funcao, f.funcaoCT, f.Habilidade.ToString("0.0", inv), f.MelhorFuncao()));
                for (int k = 0; k < Atributos.N; k++) sb.Append(';').Append(MathF.Round(f.atr[k]).ToString(inv));
                for (int k = 0; k < 7; k++) sb.Append(';').Append(MathF.Round(f.fam[k]).ToString(inv));
                sb.Append(';').Append(string.Join("|", f.carac.Select(Atributos.Nome))).Append('\n');
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Evolução na carreira: quem joga numa função fica familiar com ela e sobe os atributos que ela usa (até o teto oculto,
    /// mais rápido quando é novo e profissional). Mecânica cai depois dos ~28. Moral e forma andam com os resultados.
    /// </summary>
    public static class Evolucao
    {
        static float FatorIdade(int idade) => idade <= 21 ? 1.4f : idade <= 24 ? 1.0f : idade <= 27 ? 0.5f : 0.15f;
        static float FatorProf(Ficha f) => 0.5f + f[Atr.Profissionalismo] / 20f + f[Atr.Determinacao] / 40f;

        /// <summary>Rating simples de partida (~1,0 = média): abates, sobrevivência e dano por round.</summary>
        public static float Rating(int k, int d, int dano, int rounds)
        {
            if (rounds <= 0) return 1f;
            float kpr = k / (float)rounds, dpr = d / (float)rounds, adr = dano / (float)rounds;
            return 0.5f * kpr / 0.68f + 0.25f * (1f - dpr) / 0.32f + 0.25f * adr / 76f;
        }

        /// <summary>Depois de uma partida: rounds jogados em cada função (índice = Role), rating e se ganhou.</summary>
        public static void AposPartida(Ficha f, int[] roundsPorFuncao, float rating, bool venceu, Equipe staff = null)
        {
            f.partidas++;
            int total = roundsPorFuncao.Sum();
            float fi = FatorIdade(f.idade), fp = FatorProf(f);
            for (int r = 0; r < 7; r++)
            {
                int n = roundsPorFuncao[r]; f.roundsNaFuncao[r] += n;
                if (n > 0) f.fam[r] = MathF.Min(20f, f.fam[r] + (20f - f.fam[r]) * n / (n + 400f) * (staff?.Familiaridade ?? 1f) * (f.Tem(Caracteristica.EstudaOJogo) ? 1.2f : 1f));
                else if (f.fam[r] > 5f) f.fam[r] -= 0.002f * (f.fam[r] - 5f);   // função que não joga esquece devagar
                if (n == 0 || total == 0) continue;
                float fatia = n / (float)total;
                foreach (var (a, w) in Atributos.PesoNaFuncao[(Role)r])
                {
                    int k = (int)a; float falta = f.teto[k] - f.atr[k];
                    if (falta <= 0) continue;
                    f.atr[k] = MathF.Min(f.teto[k], f.atr[k] + 0.012f * w * fatia * fi * fp * falta / (falta + 2f) * (staff?.TreinoDe(a) ?? 1f) * Estudo(f, a));
                }
            }
            if (f.idade >= 28)
                foreach (var a in Atributos.Tecnicos)
                    if (Atributos.Mecanico(a)) f[a] = f[a] - 0.006f * (f.idade - 27) * (1.5f - f[Atr.Profissionalismo] / 20f);
            f.moral = MathF.Max(-1f, MathF.Min(1f, 0.9f * f.moral + 0.08f * (venceu ? 1f : -1f) + 0.05f * MathF.Max(-1f, MathF.Min(1f, rating - 1f))));
            f.forma = 0.7f * f.forma + 0.3f * MathF.Max(-1f, MathF.Min(1f, (rating - 1f) * 2f));
        }

        /// <summary>Quem estuda o jogo evolui 30% mais rápido o lado "cabeça" (granada, leitura, decisões, posicionamento).</summary>
        public static float Estudo(Ficha f, Atr a) => f.Tem(Caracteristica.EstudaOJogo) && Atributos.DeCabeca(a) ? 1.3f : 1f;

        /// <summary>Um ano a mais (fim de temporada).</summary>
        public static void Aniversario(Ficha f) => f.idade++;
    }

    /// <summary>
    /// Mentoria (como no FM, e como o veterano que pega o novato pela mão): um mentor que manja do jogo (liderança, leitura,
    /// comunicação; estudar o jogo ajuda) faz o aprendiz crescer mais rápido no lado "cabeça" e na familiaridade com a função
    /// do mentor, passa um pouco do jeito de ser (profissionalismo, determinação, temperamento) e pode passar características.
    /// Só sobe até o teto do aprendiz e nunca passa o número do mentor.
    /// </summary>
    public static class Mentoria
    {
        /// <summary>Quão bom mentor ele é, −0,5..1 (0 = mentor médio que quase não ajuda).</summary>
        public static float Qualidade(Ficha m)
        {
            float q = (m[Atr.Lideranca] + m[Atr.LeituraDeJogo] + m[Atr.Comunicacao]) / 3f + (m.Tem(Caracteristica.EstudaOJogo) ? 1f : 0f);
            if (m.idade < 22) q -= 2f;   // mentor muito novo ainda não tem bagagem
            return Math.Clamp((q - 11f) / 7f, -0.5f, 1f);
        }

        /// <summary>Uma semana de mentoria. Devolve a característica que o aprendiz pegou nesta semana (ou null).</summary>
        public static Caracteristica? Semana(Ficha mentor, Ficha aprendiz, Equipe staff = null, float foco = 1f)
        {
            float q = Qualidade(mentor) * foco * (mentor.pais == aprendiz.pais ? 1.15f : 1f);   /* foco: mentor dividido entre aprendizes; mesmo país conversa mais */
            if (q <= 0f || mentor == aprendiz) return null;
            float idade = aprendiz.idade <= 21 ? 1.3f : aprendiz.idade <= 25 ? 1f : 0.5f;
            for (int k = 0; k < Atributos.N; k++)
            {
                var a = (Atr)k;
                if (!Atributos.DeCabeca(a) && a != Atr.Calma && a != Atr.Comunicacao && a != Atr.TrabalhoEmEquipe) continue;
                float alvo = MathF.Min(mentor.atr[k], aprendiz.teto[k]), falta = alvo - aprendiz.atr[k];
                if (falta > 0) aprendiz.atr[k] += 0.025f * q * idade * falta / (falta + 2f) * Evolucao.Estudo(aprendiz, a);
            }
            foreach (var a in new[] { Atr.Profissionalismo, Atr.Determinacao, Atr.Temperamento })
                aprendiz[a] = aprendiz[a] + 0.01f * q * (mentor[a] - aprendiz[a]);
            int fm = (int)mentor.funcao;
            aprendiz.fam[fm] = MathF.Min(20f, aprendiz.fam[fm] + 0.03f * q * (20f - aprendiz.fam[fm]));
            foreach (var c in mentor.carac)
            {
                if (aprendiz.Tem(c)) continue;
                aprendiz.treinando.TryGetValue(c, out float p);
                p += 0.02f * q * (aprendiz[Atr.Adaptabilidade] + aprendiz[Atr.Profissionalismo]) / 20f * (staff?.Caracteristica(c) ?? 1f);
                if (p >= 1f) { aprendiz.Aprende(c); return c; }
                aprendiz.treinando[c] = p;
            }
            return null;
        }
    }

    /// <summary>
    /// Mentoria do staff (técnico, assistente, psicólogo): o coach também pega o novato. Ele não joga, então ensina o lado
    /// "cabeça" e o jeito (calma, profissionalismo, determinação, temperamento) e passa só as características de jogo coletivo
    /// e de estudo — as de mecânica (pre-fire, AWP agressiva, bom de pistola...) vêm de jogador. Cobre mais gente ao mesmo
    /// tempo com menos foco em cada um.
    /// </summary>
    public static class MentoriaStaff
    {
        static readonly Caracteristica[] Passa = { Caracteristica.EstudaOJogo, Caracteristica.SegueACall, Caracteristica.JogaColadoNoTime, Caracteristica.SalvaAArma, Caracteristica.PreNade, Caracteristica.DaDrop };
        /// <summary>Quão bom mentor ele é, −0,5..1 (treino mental, treino tático e gestão de pessoas).</summary>
        public static float Qualidade(Staff s) => Math.Clamp(((s[AtrStaff.TreinoMental] + s[AtrStaff.TreinoTatico] + s[AtrStaff.GestaoDePessoas]) / 3f - 11f) / 7f, -0.5f, 1f);

        public static Caracteristica? Semana(Staff mentor, Ficha aprendiz, float foco = 1f)
        {
            float q = Qualidade(mentor) * foco * (mentor.pais == aprendiz.pais ? 1.15f : 1f);
            if (q <= 0f) return null;
            float idade = aprendiz.idade <= 21 ? 1.3f : aprendiz.idade <= 25 ? 1f : 0.5f;
            for (int k = 0; k < Atributos.N; k++)
            {
                var a = (Atr)k;
                if (!Atributos.DeCabeca(a) && a != Atr.Calma && a != Atr.Concentracao && a != Atr.TrabalhoEmEquipe && a != Atr.Comunicacao) continue;
                float falta = aprendiz.teto[k] - aprendiz.atr[k];   /* o coach não tem número de jogador: puxa até o teto do aprendiz, mais devagar */
                if (falta > 0) aprendiz.atr[k] += 0.015f * q * idade * falta / (falta + 3f) * Evolucao.Estudo(aprendiz, a);
            }
            aprendiz[Atr.Profissionalismo] = aprendiz[Atr.Profissionalismo] + 0.03f * q;
            aprendiz[Atr.Determinacao] = aprendiz[Atr.Determinacao] + 0.02f * q;
            aprendiz[Atr.Temperamento] = aprendiz[Atr.Temperamento] + 0.02f * q;
            aprendiz.moral = Math.Clamp(aprendiz.moral + 0.01f * q, -1f, 1f);
            foreach (var c in Passa)
            {
                if (aprendiz.Tem(c) || c == Caracteristica.EstudaOJogo && mentor[AtrStaff.AnaliseDeDados] < 12 && mentor[AtrStaff.TreinoMental] < 14) continue;
                aprendiz.treinando.TryGetValue(c, out float p);
                p += 0.012f * q * (aprendiz[Atr.Adaptabilidade] + aprendiz[Atr.Profissionalismo]) / 20f;
                if (p >= 1f) { aprendiz.Aprende(c); return c; }
                aprendiz.treinando[c] = p;
            }
            return null;
        }
    }

    /// <summary>
    /// Grupo de mentoria (como o do FM): 1-2 mentores (veterano, IGL, técnico/assistente) e os novatos. Jogador mentor foca
    /// em até 2 aprendizes (mais que isso dilui); staff cobre o grupo com foco 1/√n. Uma chamada = uma semana.
    /// </summary>
    public class GrupoMentoria
    {
        public readonly List<Ficha> mentores = new List<Ficha>();
        public readonly List<Staff> mentoresStaff = new List<Staff>();
        public readonly List<Ficha> aprendizes = new List<Ficha>();
        public List<(Ficha quem, Caracteristica c)> Semana(Equipe staff = null)
        {
            var pegou = new List<(Ficha, Caracteristica)>();
            int n = Math.Max(1, aprendizes.Count);
            float focoJog = n <= 2 ? 1f : 2f / n, focoStaff = 1f / MathF.Sqrt(n);
            foreach (var a in aprendizes)
            {
                foreach (var m in mentores) { var c = Mentoria.Semana(m, a, staff, focoJog); if (c != null) pegou.Add((a, c.Value)); }
                foreach (var s in mentoresStaff) { var c = MentoriaStaff.Semana(s, a, focoStaff); if (c != null) pegou.Add((a, c.Value)); }
            }
            return pegou;
        }
    }

    /// <summary>Treino da semana: um grupo de atributos ou uma característica (como o treino individual do FM 2026).</summary>
    public static class Treino
    {
        /// <summary>Treina um atributo: sobe devagar até o teto (novo e profissional sobe mais).</summary>
        public static void Atributo(Ficha f, Atr a, float intensidade = 1f, Equipe staff = null)
        {
            int k = (int)a; float falta = f.teto[k] - f.atr[k];
            if (falta <= 0 || Atributos.Personalidade(a)) return;
            float fi = f.idade <= 21 ? 1.4f : f.idade <= 24 ? 1f : f.idade <= 27 ? 0.5f : 0.2f;
            f.atr[k] = MathF.Min(f.teto[k], f.atr[k] + 0.05f * intensidade * fi * (0.5f + f[Atr.Profissionalismo] / 20f) * falta / (falta + 2f) * (staff?.TreinoDe(a) ?? 1f) * Evolucao.Estudo(f, a));
        }
        /// <summary>Treina uma característica: progresso por semana; aprende ao chegar em 1 (~8–15 semanas).
        /// Devolve true na semana em que aprendeu.</summary>
        public static bool Caracteristica(Ficha f, Caracteristica c, float intensidade = 1f, Equipe staff = null)
        {
            if (f.Tem(c)) return false;
            f.treinando.TryGetValue(c, out float p);
            p += 0.045f * intensidade * (f[Atr.Adaptabilidade] + f[Atr.Profissionalismo]) / 20f * (staff?.Caracteristica(c) ?? 1f);
            if (p >= 1f) { f.Aprende(c); return true; }
            f.treinando[c] = p; return false;
        }
        /// <summary>Desaprender (o treinador pede pra largar o hábito): some na hora, mas o jogador perde um pouco de moral.</summary>
        /// <summary>Uma semana de convivência: o clima do elenco anda pro que o staff sustenta (gestão, motivação, psicologia)
        /// e pro resultado recente; a moral de cada um puxa junto um pouco.</summary>
        public static void Semana(Clube c, float resultadoRecente)
        {
            float alvo = Math.Clamp(c.equipe.ClimaAlvo + 0.4f * resultadoRecente, -1f, 1f);
            c.clima += 0.15f * (alvo - c.clima);
            foreach (var f in c.elenco) f.moral = Math.Clamp(f.moral + 0.05f * (c.clima - f.moral), -1f, 1f);
        }
        public static void Larga(Ficha f, Caracteristica c) { if (f.carac.Remove(c)) f.moral = MathF.Max(-1f, f.moral - 0.05f); }
    }
}
