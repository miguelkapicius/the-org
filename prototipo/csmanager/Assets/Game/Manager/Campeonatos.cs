using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // ============================================================================================================
    // Campeonatos (decisão 0012): times, séries (MD1/MD3/MD5) e os formatos liga, grupos, grupos de dupla eliminação (GSL),
    // suíço e mata-mata. Sem dependência do Unity. Todos os números (premiação, pontos, força dos times) são PROVISÓRIOS.
    // ============================================================================================================

    public enum Circuito { Amador, Pro, Elite }
    public enum Formato { Liga, GruposMataMata, GruposGslMataMata, SuicoMataMata, MataMata }

    /// <summary>Um time do mundo do jogo (o clube do manager é o time de id 0).</summary>
    public class Equipe
    {
        public int id; public string nome; public float forca; public Estilo estilo;
        public int[] afinidade = new int[Mapas.TOTAL];
        public List<(string nome, Role funcao)> jogadores = new List<(string nome, Role funcao)>();
        public float pontos, pontosAnt;   // ranking: pontos da temporada e da anterior (esta conta pela metade)
        public Circuito circuito;
        public bool clube;
        public int Forca => (int)Math.Round(forca);
        public float PontosRanking => pontos + 0.5f * pontosAnt;
    }

    /// <summary>Uma série entre dois times (MD1, MD3 ou MD5).</summary>
    public class Confronto
    {
        public int a, b, md = 1, vitA, vitB, semana, dia; public bool feito; public string rotulo;
        public int Vencedor => vitA > vitB ? a : b;
        public int Perdedor => vitA > vitB ? b : a;
        public bool Envolve(int id) => a == id || b == id;
        public int Alvo => md / 2 + 1;   // vitórias para ganhar: MD1=1, MD3=2, MD5=3
    }

    // ------------------------------------------------------------------------------------------------ fases
    abstract class Fase
    {
        /// <summary>A próxima rodada (os resultados da anterior já estão registrados); null = a fase acabou.</summary>
        public abstract List<Confronto> Proxima();
        /// <summary>Times em ordem de classificação (válido quando a fase acabou).</summary>
        public abstract List<int> Classificados();
    }

    /// <summary>Todos contra todos (liga e grupos): método do círculo.</summary>
    class FaseRR : Fase
    {
        readonly List<int> times; readonly int md; readonly string nome; readonly List<List<(int, int)>> rodadas = new List<List<(int, int)>>();
        readonly List<Confronto> todos = new List<Confronto>(); int r; readonly Random rng;
        public FaseRR(List<int> times, int md, string nome, int seed)
        {
            this.times = times; this.md = md; this.nome = nome; rng = new Random(seed);
            var l = times.ToList(); if (l.Count % 2 == 1) l.Add(-1);
            int n = l.Count;
            for (int k = 0; k < n - 1; k++)
            {
                var rd = new List<(int, int)>();
                for (int i = 0; i < n / 2; i++) { int a = l[i], b = l[n - 1 - i]; if (a >= 0 && b >= 0) rd.Add((a, b)); }
                rodadas.Add(rd);
                var ult = l[n - 1]; l.RemoveAt(n - 1); l.Insert(1, ult);
            }
        }
        public override List<Confronto> Proxima()
        {
            if (r >= rodadas.Count) return null;
            var lista = rodadas[r].Select(p => new Confronto { a = p.Item1, b = p.Item2, md = md, rotulo = nome + " · rodada " + (r + 1) }).ToList();
            todos.AddRange(lista); r++;
            return lista;
        }
        public List<(int id, int pts, int vit, int der, int dif)> Tabela()
        {
            var t = times.ToDictionary(x => x, x => (pts: 0, vit: 0, der: 0, dif: 0));
            foreach (var c in todos.Where(c => c.feito))
            {
                var w = t[c.Vencedor]; t[c.Vencedor] = (w.pts + 3, w.vit + 1, w.der, w.dif + Math.Abs(c.vitA - c.vitB));
                var p = t[c.Perdedor]; t[c.Perdedor] = (p.pts, p.vit, p.der + 1, p.dif - Math.Abs(c.vitA - c.vitB));
            }
            return times.Select(x => (x, t[x].pts, t[x].vit, t[x].der, t[x].dif)).OrderByDescending(x => x.Item2).ThenByDescending(x => x.Item5).ThenBy(x => rng.Next()).ToList();
        }
        public override List<int> Classificados() => Tabela().Select(x => x.id).ToList();
    }

    /// <summary>Grupo de dupla eliminação (estilo GSL), 4 times: 2 jogos de abertura, partida dos vencedores, partida dos perdedores e a decisão.</summary>
    class FaseGSL : Fase
    {
        readonly List<int> t; readonly string nome; int passo; Confronto c1, c2, w, l, d;
        public FaseGSL(List<int> times, string nome) { t = times; this.nome = nome; }
        public override List<Confronto> Proxima()
        {
            switch (passo++)
            {
                case 0: c1 = new Confronto { a = t[0], b = t[3], md = 3, rotulo = nome + " · abertura" }; c2 = new Confronto { a = t[1], b = t[2], md = 3, rotulo = nome + " · abertura" }; return new List<Confronto> { c1, c2 };
                case 1: w = new Confronto { a = c1.Vencedor, b = c2.Vencedor, md = 3, rotulo = nome + " · vencedores" }; l = new Confronto { a = c1.Perdedor, b = c2.Perdedor, md = 3, rotulo = nome + " · perdedores" }; return new List<Confronto> { w, l };
                case 2: d = new Confronto { a = w.Perdedor, b = l.Vencedor, md = 3, rotulo = nome + " · decisão" }; return new List<Confronto> { d };
                default: return null;
            }
        }
        public override List<int> Classificados() => new List<int> { w.Vencedor, d.Vencedor, d.Perdedor, l.Perdedor };
    }

    /// <summary>Vários grupos em paralelo (cada rodada junta a rodada de todos os grupos); classificam os melhores de cada um.</summary>
    class FaseGrupos : Fase
    {
        readonly List<Fase> grupos; readonly int avancam; readonly List<List<int>> times;
        public FaseGrupos(List<Fase> grupos, List<List<int>> times, int avancam) { this.grupos = grupos; this.times = times; this.avancam = avancam; }
        public override List<Confronto> Proxima()
        {
            var tudo = new List<Confronto>(); bool algum = false;
            foreach (var g in grupos) { var p = g.Proxima(); if (p != null) { algum = true; tudo.AddRange(p); } }
            return algum ? tudo : null;
        }
        public override List<int> Classificados()
        {
            // 1º de cada grupo, depois os 2º... (para o chaveamento cruzado); quem não avança vem depois, por posição
            var porGrupo = grupos.Select(g => g.Classificados()).ToList();
            var res = new List<int>();
            for (int pos = 0; pos < porGrupo.Max(g => g.Count); pos++) foreach (var g in porGrupo) if (pos < g.Count) res.Add(g[pos]);
            return res;
        }
        public int Avancam => avancam;
        public int NumGrupos => grupos.Count;
    }

    /// <summary>Sistema suíço (estilo Major): 3 vitórias classificam, 3 derrotas eliminam; rodadas decisivas em MD3.</summary>
    class FaseSuico : Fase
    {
        readonly List<int> times; readonly Dictionary<int, (int v, int d)> rec = new Dictionary<int, (int, int)>();
        readonly HashSet<(int, int)> jogados = new HashSet<(int, int)>(); readonly Random rng; List<Confronto> ultima; readonly string nome;
        public FaseSuico(List<int> times, string nome, int seed) { this.times = times; this.nome = nome; rng = new Random(seed); foreach (var x in times) rec[x] = (0, 0); }
        void Processar()
        {
            if (ultima == null) return;
            foreach (var c in ultima) { var w = rec[c.Vencedor]; rec[c.Vencedor] = (w.v + 1, w.d); var p = rec[c.Perdedor]; rec[c.Perdedor] = (p.v, p.d + 1); jogados.Add((Math.Min(c.a, c.b), Math.Max(c.a, c.b))); }
            ultima = null;
        }
        public override List<Confronto> Proxima()
        {
            Processar();
            var ativos = times.Where(x => rec[x].v < 3 && rec[x].d < 3).ToList();
            if (ativos.Count == 0) return null;
            var res = new List<Confronto>(); int rodada = rec.Values.Max(x => x.v + x.d) + 1;
            foreach (var g in ativos.GroupBy(x => rec[x]).OrderByDescending(g => g.Key.v).ThenBy(g => g.Key.d))
            {
                var seeds = g.OrderBy(x => times.IndexOf(x)).ToList();
                List<(int, int)> pares = null;
                for (int tentativa = 0; tentativa < 40 && pares == null; tentativa++)
                {
                    var ordem = tentativa == 0 ? seeds : seeds.OrderBy(x => rng.Next()).ToList();   // 1ª tentativa: melhor contra pior
                    var p = new List<(int, int)>(); bool repete = false;
                    for (int i = 0; i < ordem.Count / 2; i++)
                    {
                        int a = ordem[i], b = ordem[ordem.Count - 1 - i];
                        if (jogados.Contains((Math.Min(a, b), Math.Max(a, b)))) { repete = true; break; }
                        p.Add((a, b));
                    }
                    if (!repete) pares = p;
                }
                if (pares == null) { pares = new List<(int, int)>(); for (int i = 0; i < seeds.Count / 2; i++) pares.Add((seeds[i], seeds[seeds.Count - 1 - i])); }
                int md = g.Key.v == 2 || g.Key.d == 2 ? 3 : 1;
                foreach (var (a, b) in pares) res.Add(new Confronto { a = a, b = b, md = md, rotulo = nome + " · rodada " + rodada + " (" + g.Key.v + "-" + g.Key.d + ")" });
            }
            ultima = res;
            return res;
        }
        public override List<int> Classificados()
        {
            Processar();
            return times.OrderByDescending(x => rec[x].v).ThenBy(x => rec[x].d).ThenBy(x => times.IndexOf(x)).ToList();
        }
        public (int v, int d) Registro(int id) { Processar(); return rec[id]; }
    }

    /// <summary>Mata-mata simples: vencedores avançam; MD1 nas rodadas iniciais, MD3 nas decisivas, MD5 na final.</summary>
    class FaseMM : Fase
    {
        List<int> atual; readonly Dictionary<int, int> saiu = new Dictionary<int, int>(); List<Confronto> ultima; int rodada; readonly string nome; readonly bool finalMd5;
        public FaseMM(List<int> sementes, string nome, bool jaChaveado = false, bool finalMd5 = true) { atual = jaChaveado ? sementes.ToList() : ChaveCruzada(sementes); this.nome = nome; this.finalMd5 = finalMd5; }

        /// <summary>Ordem de chaveamento padrão (1º contra último, etc.) para uma lista de sementes que já vem ordenada.</summary>
        static List<int> ChaveCruzada(List<int> s)
        {
            // para 8: 1,8,4,5,2,7,3,6; generaliza por recursão
            List<int> Ordem(int n) { if (n == 2) return new List<int> { 0, 1 }; var m = Ordem(n / 2); var r = new List<int>(); foreach (var x in m) { r.Add(x); r.Add(n - 1 - x); } return r; }
            int p = 1; while (p < s.Count) p *= 2;
            if (p != s.Count) return s;   // só potências de 2
            return Ordem(p).Select(i => s[i]).ToList();
        }
        void Processar()
        {
            if (ultima == null) return;
            var prox = new List<int>();
            foreach (var c in ultima) { prox.Add(c.Vencedor); saiu[c.Perdedor] = rodada; }
            atual = prox; ultima = null;
        }
        public override List<Confronto> Proxima()
        {
            Processar();
            if (atual.Count <= 1) return null;
            rodada++;
            int n = atual.Count;
            string rot = n == 2 ? "Final" : n == 4 ? "Semifinal" : n == 8 ? "Quartas de final" : "Oitavas de final (" + n + " times)";
            int md = n == 2 ? (finalMd5 ? 5 : 3) : n <= 8 ? 3 : 1;
            var res = new List<Confronto>();
            for (int i = 0; i + 1 < n; i += 2) res.Add(new Confronto { a = atual[i], b = atual[i + 1], md = md, rotulo = nome + " · " + rot });
            ultima = res;
            return res;
        }
        public override List<int> Classificados()
        {
            Processar();
            var res = new List<int>(); if (atual.Count == 1) res.Add(atual[0]);
            res.AddRange(saiu.OrderByDescending(kv => kv.Value).Select(kv => kv.Key));
            return res;
        }
    }

    // ------------------------------------------------------------------------------------------------ campeonato
    public class Campeonato
    {
        public int id; public string nome; public Formato formato; public Circuito circuito; public bool major;
        public int semanaInicio; public List<int> times = new List<int>(); public int premio; public float pesoRanking;
        public bool iniciado, apurado, terminado; public List<int> colocacao = new List<int>();
        public List<Confronto> rodadaAtual = new List<Confronto>();
        public readonly List<Confronto> historico = new List<Confronto>();   // todos os confrontos já resolvidos, em ordem

        readonly List<Fase> fases = new List<Fase>(); int faseIdx; int seed; int clube = -1; int rodadasGeradas;
        Random rng;
        /// <summary>Séries por semana: ligas têm 1 por semana; o resto agrupa várias rodadas em dias da mesma semana.</summary>
        int Ritmo => formato == Formato.Liga ? 1 : 3;

        public Campeonato() { }
        public Campeonato(int id, string nome, Formato f, Circuito c, int semanaInicio, List<int> times, int premio, float peso, int seed, bool major = false)
        {
            this.id = id; this.nome = nome; formato = f; circuito = c; this.semanaInicio = semanaInicio; this.times = times.ToList(); this.premio = premio; pesoRanking = peso; this.seed = seed; this.major = major;
            rng = new Random(seed);
            Montar();
        }

        void Montar()
        {
            var t = times;
            switch (formato)
            {
                case Formato.Liga: fases.Add(new FaseRR(t, 1, nome, seed)); break;
                case Formato.MataMata: fases.Add(new FaseMM(t, nome)); break;
                case Formato.SuicoMataMata: fases.Add(new FaseSuico(t, nome, seed)); break;
                default:
                {
                    int ng = Math.Max(1, t.Count / 4); var gr = new List<Fase>(); var tg = new List<List<int>>();
                    for (int g = 0; g < ng; g++)
                    {
                        var membros = new List<int>(); for (int k = g; k < t.Count; k += ng) membros.Add(t[k]);   // distribui as sementes (serpentina simples)
                        tg.Add(membros);
                        string nomeG = nome + " · grupo " + (char)('A' + g);
                        gr.Add(formato == Formato.GruposGslMataMata ? (Fase)new FaseGSL(membros, nomeG) : new FaseRR(membros, 1, nomeG, seed + g));
                    }
                    fases.Add(new FaseGrupos(gr, tg, 2));
                    break;
                }
            }
        }

        /// <summary>Resolve por probabilidade tudo que falta (o clube deixa de ser esperado).</summary>
        public void FinalizarForcado() { clube = -1; Progredir(); }

        public void Iniciar(int clubeId)
        {
            clube = clubeId;
            Progredir();
        }

        /// <summary>O confronto do clube que está esperando resultado (null se não há).</summary>
        public Confronto PendenteDoClube => clube < 0 ? null : rodadaAtual.FirstOrDefault(c => c.Envolve(clube) && !c.feito);
        public bool ClubeParticipa => times.Contains(clube);

        /// <summary>Registra o resultado do confronto do clube e avança o campeonato até o próximo confronto do clube (ou o fim).</summary>
        public void RegistrarClube(Confronto c, int vitA, int vitB)
        {
            c.vitA = vitA; c.vitB = vitB; c.feito = true;
            resultadosClube.Add((vitA, vitB));
            Progredir();
        }

        /// <summary>Os resultados das séries do clube, na ordem: com eles e a semente dá para reconstruir o campeonato inteiro (salvamento).</summary>
        public readonly List<(int vitA, int vitB)> resultadosClube = new List<(int, int)>();

        /// <summary>Refaz o campeonato a partir da semente e dos resultados do clube (o resto é determinístico).</summary>
        public void Reproduzir(int clubeId, IEnumerable<(int vitA, int vitB)> resultados)
        {
            resultadosClube.Clear();
            Iniciar(clubeId);
            foreach (var r in resultados) { var p = PendenteDoClube; if (p == null) break; RegistrarClube(p, r.vitA, r.vitB); }
        }

        /// <summary>Resolve por probabilidade tudo que não é do clube e avança as rodadas.</summary>
        void Progredir()
        {
            int guarda = 0;
            while (!terminado && guarda++ < 200)
            {
                if (rodadaAtual.Count == 0 || rodadaAtual.All(c => c.feito)) { if (!ProximaRodada()) break; }
                foreach (var c in rodadaAtual.Where(c => !c.feito && !c.Envolve(clube))) Resolver(c);
                if (PendenteDoClube != null) return;   // espera o manager
                foreach (var c in rodadaAtual.Where(c => !c.feito)) Resolver(c);   // clube eliminado ou fora: resolve tudo
            }
        }

        bool ProximaRodada()
        {
            foreach (var c in rodadaAtual) historico.Add(c);
            rodadaAtual = new List<Confronto>();
            while (faseIdx < fases.Count)
            {
                var prox = fases[faseIdx].Proxima();
                if (prox != null && prox.Count > 0)
                {
                    for (int i = 0; i < prox.Count; i++) { prox[i].semana = semanaInicio + rodadasGeradas / Ritmo; prox[i].dia = rodadasGeradas % Ritmo; }
                    rodadasGeradas++; rodadaAtual = prox; return true;
                }
                // a fase acabou: monta a próxima com os classificados
                if (!AvancarFase()) { Encerrar(); return false; }
            }
            Encerrar(); return false;
        }

        bool AvancarFase()
        {
            var fase = fases[faseIdx];
            if (faseIdx + 1 < fases.Count) { faseIdx++; return true; }
            // só cria a fase de mata-mata dos formatos compostos uma vez
            var cl = fase.Classificados();
            if (fase is FaseGrupos g)
            {
                var porPos = cl; int ng = g.NumGrupos, top = g.Avancam * ng;
                // a ordem de porPos é A1,B1,C1,D1,A2,B2,C2,D2,...; o chaveamento cruzado é A1xB2, C1xD2, B1xA2, D1xC2
                var seeds = ng == 4 ? new List<int> { porPos[0], porPos[5], porPos[2], porPos[7], porPos[1], porPos[4], porPos[3], porPos[6] }
                          : ng == 2 ? new List<int> { porPos[0], porPos[3], porPos[1], porPos[2] } : porPos.Take(top).ToList();
                fases.Add(new FaseMM(ngSeedsPotencia2(seeds), nome + " · playoffs", ng == 4 || ng == 2)); faseIdx++; naoClassificadosFaseAnterior = porPos.Skip(top).ToList(); return true;
            }
            if (fase is FaseSuico)
            {
                var top8 = cl.Take(8).ToList();
                fases.Add(new FaseMM(top8, nome + " · playoffs")); faseIdx++; naoClassificadosFaseAnterior = cl.Skip(8).ToList(); return true;
            }
            return false;
        }
        List<int> naoClassificadosFaseAnterior = new List<int>();
        static List<int> ngSeedsPotencia2(List<int> s) { int p = 1; while (p * 2 <= s.Count) p *= 2; return s.Take(p).ToList(); }

        void Encerrar()
        {
            terminado = true;
            var ult = fases[faseIdx].Classificados();
            colocacao = new List<int>(ult);
            colocacao.AddRange(naoClassificadosFaseAnterior.Where(x => !colocacao.Contains(x)));
            foreach (var x in times) if (!colocacao.Contains(x)) colocacao.Add(x);
        }

        // ------------------------------------------------------------------ resolução por probabilidade
        public Func<int, int, double> ChanceMapa;   // chance do time a vencer um mapa contra b (o mundo injeta a conta com força, afinidade e estilo)

        void Resolver(Confronto c)
        {
            double p = ChanceMapa != null ? ChanceMapa(c.a, c.b) : 0.5;
            int a = 0, b = 0, alvo = c.Alvo;
            while (a < alvo && b < alvo) { if (rng.NextDouble() < p) a++; else b++; }
            c.vitA = a; c.vitB = b; c.feito = true;
        }

        // ------------------------------------------------------------------ consultas
        /// <summary>Colocação (1 = campeão) de um time no campeonato terminado.</summary>
        public int Colocacao(int idTime) => colocacao.IndexOf(idTime) + 1;
        public int PremioPara(int posicao) => (int)(premio * FracaoPremio(posicao, times.Count));
        public float PontosPara(int posicao) => pesoRanking * PontosFracao(posicao);

        public static float FracaoPremio(int k, int n)
        {
            if (n <= 8) return k == 1 ? 0.40f : k == 2 ? 0.25f : k == 3 ? 0.15f : k == 4 ? 0.10f : 0.025f;
            return k == 1 ? 0.38f : k == 2 ? 0.20f : k <= 4 ? 0.11f : k <= 8 ? 0.04f : 0.005f;
        }
        public static float PontosFracao(int k) => k == 1 ? 1f : k == 2 ? 0.7f : k <= 4 ? 0.5f : k <= 8 ? 0.3f : k <= 16 ? 0.12f : 0.05f;

        /// <summary>Semana em que o campeonato acaba (estimada pelo formato).</summary>
        public int SemanaFim
        {
            get
            {
                int rodadas = formato == Formato.Liga ? times.Count - 1 : formato == Formato.MataMata ? (int)Math.Log(times.Count, 2) : formato == Formato.SuicoMataMata ? 5 + 3 : 3 + 3;
                return semanaInicio + (int)Math.Ceiling(rodadas / (double)Ritmo) - 1;
            }
        }

        /// <summary>Tabela de pontos (só ligas): id, pontos, vitórias, derrotas.</summary>
        public List<(int id, int pts, int vit, int der)> TabelaLiga()
        {
            var t = times.ToDictionary(x => x, x => (pts: 0, vit: 0, der: 0));
            foreach (var c in historico.Concat(rodadaAtual).Where(c => c.feito))
            {
                var w = t[c.Vencedor]; t[c.Vencedor] = (w.pts + 3, w.vit + 1, w.der); var p = t[c.Perdedor]; t[c.Perdedor] = (p.pts, p.vit, p.der + 1);
            }
            return times.Select(x => (x, t[x].pts, t[x].vit, t[x].der)).OrderByDescending(x => x.Item2).ThenByDescending(x => x.Item3).ToList();
        }

        public IEnumerable<Confronto> TodosConfrontos => historico.Concat(rodadaAtual);
        public Confronto ProximoDoClube => PendenteDoClube;
        public int Seed => seed;
    }
}
