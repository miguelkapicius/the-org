using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CSM.Map;
using static CSM.Map.MapData;

namespace CSM.Sim
{
    /// <summary>
    /// O time cria tática nova do jeito que se cria na vida real: ninguém inventa do zero, o IGL (com o analista) pega uma
    /// jogada que o time conhece e muda um pedaço com motivo — o que leu do adversário, o que vem dando errado, ou só pra
    /// não ser previsível. A ideia vai pro treino (pracc, Headless -pracc) e só a que se segura entra no repertório do
    /// clube; no jogo, tática criada concorre com o banco pelo que vem rendendo.
    ///
    /// Operadores (cada um é uma conversa de IGL):
    ///   Enxerto  — "o nosso default até X s, depois a execução daquela outra" (o jeito mais comum de montar tática nova)
    ///   Fake     — dois mostram no outro bomb (o CT gira) e voltam pra execução
    ///   Lurk     — um vai pro lado oposto pegar a rotação
    ///   Evitar   — a rota passava onde a gente vem morrendo (AWP deles, posição queimada): troca o caminho de quem passava ali
    ///   Ritmo    — a mesma jogada mais rápida (CT deles passivo) ou mais lenta (CT deles sai cedo)
    ///   Stack/Agressivo (CT) — um jogador vai pro bomb que eles mais atacam / sai pra pegar info cedo
    /// A tática criada tem o mesmo formato das do banco (rota por jogador e horário), então o cérebro executa sem nada novo.
    /// </summary>
    public static class Taticas
    {
        public const int BASE_IDX = 1_000_000;
        static int prox = BASE_IDX;

        // ---------------------------------------------------------------- utilidades de rota
        static float Viagem(string a, string b)
        {
            if (a == null || b == null || a == b) return 0;
            return (Estrategias.Centro(a) - Estrategias.Centro(b)).Len * 1.3f / (MapCheck.RUN * 0.9f);
        }

        static Estrategias.Plano Clonar(Estrategias.Plano p)
        {
            var c = new Estrategias.Plano
            {
                idx = Interlocked.Increment(ref prox), tr = p.tr, compra = p.compra, id = p.id, ganhou = p.ganhou, site = p.site,
                plant = p.plant, comprometeu = p.comprometeu, duracao = p.duracao, js = new Estrategias.Jogador[5], pai = p.criada ? p.pai : p.idx, criada = true,
            };
            for (int k = 0; k < 5; k++) c.js[k] = new Estrategias.Jogador { passos = (Estrategias.Passo[])p.js[k].passos.Clone(), morreu = -1 };
            return c;
        }

        /// <summary>Rota do jogador antes de t (cortada em t).</summary>
        static List<Estrategias.Passo> Antes(Estrategias.Passo[] ps, float t)
        {
            var l = new List<Estrategias.Passo>();
            foreach (var p in ps) { if (p.de >= t) break; var q = p; q.ate = MathF.Min(q.ate, t); l.Add(q); }
            return l;
        }
        /// <summary>Rota do jogador depois de t (começando em t).</summary>
        static List<Estrategias.Passo> Depois(Estrategias.Passo[] ps, float t)
        {
            var l = new List<Estrategias.Passo>();
            foreach (var p in ps) { if (p.ate <= t) continue; var q = p; q.de = MathF.Max(q.de, t); l.Add(q); }
            return l;
        }

        /// <summary>Junta dois pedaços: o segundo começa quando dá pra chegar andando do fim do primeiro (empurra o resto).</summary>
        static Estrategias.Passo[] Emenda(List<Estrategias.Passo> a, List<Estrategias.Passo> b, float tCorte)
        {
            var r = new List<Estrategias.Passo>(a);
            if (b.Count == 0) return r.ToArray();
            string de = a.Count > 0 ? a[a.Count - 1].area : null;
            float chega = tCorte + Viagem(de, b[0].area) * 0.6f, atraso = MathF.Max(0, chega - b[0].de);
            foreach (var p in b) { var q = p; q.de += atraso; q.ate += atraso; r.Add(q); }
            return Reparar(r.ToArray(), a.Count, a.Count + 1);   // só a costura: dentro do pedaço do pro o tempo é o dele (o centro da área superestima)
        }

        /// <summary>Ninguém se teletransporta: entre entrar numa área e entrar na próxima tem que dar tempo de andar.</summary>
        static Estrategias.Passo[] Reparar(Estrategias.Passo[] ps, int desde, int ate = int.MaxValue)
        {
            for (int j = Math.Max(1, desde); j < ps.Length && j < ate; j++)
            {
                float precisa = Viagem(ps[j - 1].area, ps[j].area) * 0.8f, falta = ps[j - 1].de + precisa - ps[j].de;
                if (falta > 0) for (int i = j; i < ps.Length; i++) { ps[i].de += falta; ps[i].ate += falta; }
            }
            return ps;
        }

        static string OndeOuBase(Estrategias.Plano p, int k, float t) => p.Onde(k, t) ?? (p.tr ? "TR" : "CT");

        /// <summary>Quem do doador assume a rota de quem do pai no corte: o par que anda menos (5! = 120 combinações).</summary>
        static int[] Parear(Estrategias.Plano pai, Estrategias.Plano doador, float t)
        {
            var custo = new float[5, 5];
            for (int i = 0; i < 5; i++) for (int j = 0; j < 5; j++) custo[i, j] = Viagem(OndeOuBase(pai, i, t), OndeOuBase(doador, j, t));
            int[] best = null; float bc = float.MaxValue; var perm = new[] { 0, 1, 2, 3, 4 };
            void Rec(int d)
            {
                if (d == 5) { float c = 0; for (int i = 0; i < 5; i++) c += custo[i, perm[i]]; if (c < bc) { bc = c; best = (int[])perm.Clone(); } return; }
                for (int i = d; i < 5; i++) { (perm[d], perm[i]) = (perm[i], perm[d]); Rec(d + 1); (perm[d], perm[i]) = (perm[i], perm[d]); }
            }
            Rec(0);
            return best;
        }

        // ---------------------------------------------------------------- operadores
        /// <summary>O default do pai até tCorte, depois a execução do doador.</summary>
        public static Estrategias.Plano Enxerto(Estrategias.Plano pai, Estrategias.Plano doador, float tCorte, string motivo)
        {
            var c = Clonar(pai); var par = Parear(pai, doador, tCorte);
            for (int k = 0; k < 5; k++) c.js[k].passos = Emenda(Antes(pai.js[k].passos, tCorte), Depois(doador.js[par[k]].passos, tCorte), tCorte);
            float atraso = Atraso(c, doador);
            c.site = doador.site; c.doador = doador.idx;
            c.plant = doador.plant > 0 ? doador.plant + atraso : -1;
            c.comprometeu = doador.comprometeu >= 0 ? MathF.Max(tCorte, doador.comprometeu + atraso) : -1;
            c.duracao = MathF.Max(pai.duracao, doador.duracao + atraso);
            c.motivo = motivo;
            return c;
        }

        /// <summary>Quanto o enxerto empurrou a execução (média do atraso de quem pisa no bomb).</summary>
        static float Atraso(Estrategias.Plano c, Estrategias.Plano doador)
        {
            float s = 0; int n = 0; string a = doador.site.ToString();
            for (int k = 0; k < 5; k++)
            {
                float pc = c.PrimeiroEm(k, a); if (pc < 0) continue;
                float pd = float.MaxValue; for (int j = 0; j < 5; j++) { float x = doador.PrimeiroEm(j, a); if (x >= 0) pd = MathF.Min(pd, x); }
                if (pd < float.MaxValue) { s += MathF.Max(0, pc - pd); n++; }
            }
            return n > 0 ? s / n : 0;
        }

        /// <summary>Mesma jogada em outro ritmo (fator &lt; 1 = mais rápida). O começo (sair da base) não muda.</summary>
        public static Estrategias.Plano Ritmo(Estrategias.Plano pai, float fator, string motivo)
        {
            const float T0 = 8f;
            float F(float t) => t <= T0 ? t : T0 + (t - T0) * fator;
            var c = Clonar(pai);
            for (int k = 0; k < 5; k++)
            {
                var ps = c.js[k].passos;
                for (int j = 0; j < ps.Length; j++) { ps[j].de = F(ps[j].de); ps[j].ate = F(ps[j].ate); }
                c.js[k].passos = ps;   // fator ≥ 0,78: o pro anda bem abaixo da velocidade máxima; reparar pelo centro da área empurrava tudo
            }
            if (c.plant > 0) c.plant = F(c.plant); if (c.comprometeu >= 0) c.comprometeu = F(c.comprometeu); c.duracao = F(c.duracao);
            c.motivo = motivo;
            return c;
        }

        /// <summary>Um jogador (o que menos participa da execução) vira lurk do lado oposto, com a rota de um lurker de verdade.</summary>
        public static Estrategias.Plano Lurk(Estrategias.Plano pai, IList<Estrategias.Plano> banco, Rng rng, string motivo)
        {
            string outro = pai.site == 'A' ? "B" : "A"; float entra = pai.HoraDeEntrar(70);
            int k = Enumerable.Range(0, 5).OrderBy(i => pai.TempoEm(i, pai.site.ToString(), 10, entra) - pai.TempoEm(i, outro, 10, entra)).First();
            var donos = new List<(Estrategias.Plano p, int j, float s)>();
            foreach (var d in banco)
            {
                if (d.tr != pai.tr || d.idx == pai.idx) continue;
                for (int j = 0; j < 5; j++) { float s = d.TempoEm(j, outro, 15, 45); if (s >= 10) donos.Add((d, j, s)); }
            }
            if (donos.Count == 0) return null;
            var (dp, dj, _) = donos[rng.Int(donos.Count)];
            var c = Clonar(pai);
            c.js[k].passos = Emenda(Antes(pai.js[k].passos, 8), Depois(dp.js[dj].passos, 8), 8);
            c.doador = dp.idx; c.motivo = motivo;
            return c;
        }

        /// <summary>Dois mostram no outro bomb até pouco antes da hora de entrar e voltam pra execução do pai.</summary>
        public static Estrategias.Plano Fake(Estrategias.Plano pai, IList<Estrategias.Plano> banco, Rng rng, string motivo)
        {
            char outroC = pai.site == 'A' ? 'B' : 'A'; string outro = outroC.ToString();
            float entra = pai.HoraDeEntrar(70), tVolta = MathF.Max(18, entra - 10);
            var donos = banco.Where(d => d.tr == pai.tr && d.site == outroC && d.idx != pai.idx).ToList();
            if (donos.Count == 0) return null;
            var dp = donos[rng.Int(donos.Count)];
            // quem finge: os dois do doador que mais chegam perto do bomb dele; no pai, os dois que menos fazem na execução cedo
            var fingem = Enumerable.Range(0, 5).OrderByDescending(j => dp.TempoEm(j, outro, 10, tVolta)).Take(2).ToArray();
            var meus = Enumerable.Range(0, 5).OrderBy(i => pai.TempoEm(i, pai.site.ToString(), 10, tVolta)).Take(2).ToArray();
            if (dp.TempoEm(fingem[1], outro, 10, tVolta) < 2) return null;
            var c = Clonar(pai);
            for (int x = 0; x < 2; x++)
            {
                int k = meus[x], j = fingem[x];
                var ida = Emenda(Antes(pai.js[k].passos, 8), Depois(dp.js[j].passos, 8), 8).ToList();
                var ate = Antes(ida.ToArray(), tVolta);
                c.js[k].passos = Emenda(ate, Depois(pai.js[k].passos, tVolta), tVolta);
            }
            c.doador = dp.idx; c.motivo = motivo;
            return c;
        }

        /// <summary>Quem passava pela área queimada entre 8 s e a entrada pega a rota de alguém de outra jogada do mesmo bomb que não passa lá.</summary>
        public static Estrategias.Plano Evitar(Estrategias.Plano pai, string area, IList<Estrategias.Plano> banco, Rng rng, string motivo)
        {
            float entra = pai.HoraDeEntrar(70);
            var passam = new List<(int k, float t)>();
            for (int k = 0; k < 5; k++) foreach (var p in pai.js[k].passos) if (p.area == area && p.ate > 8 && p.de < entra) { passam.Add((k, MathF.Max(8, p.de - 4))); break; }
            if (passam.Count == 0) return null;
            bool passa(Estrategias.Plano d, int j) { foreach (var p in d.js[j].passos) if (p.area == area && p.ate > 8 && p.de < entra + 5) return true; return false; }
            var c = Clonar(pai); int trocados = 0;
            foreach (var (k, t) in passam)
            {
                string aqui = OndeOuBase(pai, k, t); float bc = float.MaxValue; Estrategias.Plano bd = null; int bj = -1;
                foreach (var d in banco)
                {
                    if (d.tr != pai.tr || d.site != pai.site || d.idx == pai.idx) continue;
                    for (int j = 0; j < 5; j++)
                    {
                        if (passa(d, j) || d.PrimeiroEm(j, pai.site.ToString()) < 0) continue;
                        float cst = Viagem(aqui, OndeOuBase(d, j, t)) + rng.Uniform(0, 1.5f);
                        if (cst < bc) { bc = cst; bd = d; bj = j; }
                    }
                }
                if (bd == null) continue;
                c.js[k].passos = Emenda(Antes(pai.js[k].passos, t), Depois(bd.js[bj].passos, t), t); c.doador = bd.idx; trocados++;
            }
            if (trocados == 0) return null;
            c.motivo = motivo;
            return c;
        }

        /// <summary>CT: um jogador do bomb menos atacado passa a jogar no outro (rota inteira de um CT de verdade que joga lá).</summary>
        public static Estrategias.Plano Stack(Estrategias.Plano pai, char para, IList<Estrategias.Plano> banco, Rng rng, string motivo)
        {
            string z = para.ToString(), oz = para == 'A' ? "B" : "A";
            var cands = Enumerable.Range(0, 5).Where(i => pai.ZonaEntre(i, 15, 30) == oz).ToList();
            int nOz = cands.Count; if (nOz < 2) return null;   // não deixa o outro bomb vazio
            int k = cands[rng.Int(cands.Count)];
            var donos = new List<(Estrategias.Plano, int)>();
            foreach (var d in banco) if (!d.tr && d.idx != pai.idx) for (int j = 0; j < 5; j++) if (d.ZonaEntre(j, 15, 30) == z) donos.Add((d, j));
            if (donos.Count == 0) return null;
            var (dp, dj) = donos[rng.Int(donos.Count)];
            var c = Clonar(pai); c.js[k].passos = (Estrategias.Passo[])dp.js[dj].passos.Clone(); c.doador = dp.idx; c.motivo = motivo;
            return c;
        }

        /// <summary>CT: um jogador sai cedo pra pegar info (rota de um CT de verdade que passou 4+ s no meio ou no lado TR antes dos 25 s).</summary>
        public static Estrategias.Plano Agressivo(Estrategias.Plano pai, IList<Estrategias.Plano> banco, Rng rng, string motivo)
        {
            var donos = new List<(Estrategias.Plano, int)>();
            foreach (var d in banco) if (!d.tr && d.idx != pai.idx) for (int j = 0; j < 5; j++) if (d.TempoEm(j, "Mid", 5, 25) >= 4) donos.Add((d, j));
            if (donos.Count == 0) return null;
            var (dp, dj) = donos[rng.Int(donos.Count)];
            int k = Enumerable.Range(0, 5).OrderByDescending(i => pai.TempoEm(i, "Mid", 5, 25)).First();
            var c = Clonar(pai); c.js[k].passos = (Estrategias.Passo[])dp.js[dj].passos.Clone(); c.doador = dp.idx; c.motivo = motivo;
            return c;
        }

        // ---------------------------------------------------------------- a conversa do IGL: de onde vem a ideia
        /// <summary>
        /// Gera n ideias pro lado/compra. qualidade (0..1) = leitura do IGL + analista: com qualidade alta a ideia segue o
        /// que o time leu do adversário; com baixa, é mais tentativa. Sem leitura (pré-jogo), é variação pra não ser previsível.
        /// </summary>
        public static List<Estrategias.Plano> Ideias(bool tr, string compra, Leitura L, float qualidade, int n, Rng rng, IList<Estrategias.Plano> repertorio = null)
        {
            var banco = Estrategias.Todos.Where(p => p.tr == tr && p.compra == compra).ToList();
            if (banco.Count < 10) banco = Estrategias.Todos.Where(p => p.tr == tr && p.compra == "full").ToList();
            var fonte = new List<Estrategias.Plano>(banco); if (repertorio != null) fonte.AddRange(repertorio.Where(p => p.tr == tr));
            var r = new List<Estrategias.Plano>();
            for (int tent = 0; r.Count < n && tent < n * 6; tent++)
            {
                var w = fonte.Select(p => p.criada ? 1.2f : Estrategias.Forca(p.idx) * (p.ganhou ? 1.15f : 0.9f)).ToArray();
                var pai = fonte[rng.Weighted(w)];
                var ideia = tr ? IdeiaTR(pai, banco, L, qualidade, rng) : IdeiaCT(pai, banco, L, qualidade, rng);
                if (ideia != null && Sensata(ideia)) r.Add(ideia);
            }
            return r;
        }

        static Estrategias.Plano IdeiaTR(Estrategias.Plano pai, List<Estrategias.Plano> banco, Leitura L, float q, Rng rng)
        {
            bool segue = L != null && rng.Rand() < 0.3f + 0.6f * q;
            if (segue)
            {
                // o que o time leu: CT empilha num bomb → fake nele e executa o outro; perdemos 2 num bomb → enxerta execução do
                // outro; morremos muito num lugar → evitar; CT sai cedo → mais lento; CT passivo → mais rápido
                float lado = L.LadoCrenca();
                if (MathF.Abs(lado) > 0.25f)
                {
                    char quer = lado > 0 ? 'A' : 'B';
                    if (pai.site == quer && rng.Rand() < 0.5f) { var f = Fake(pai, banco, rng, $"a leitura puxa o {quer}: fake no {(quer == 'A' ? 'B' : 'A')} pra tirar gente de lá"); if (f != null) return f; }
                    var don = banco.Where(d => d.site == quer).ToList();
                    if (pai.site != quer && don.Count > 0) return Enxerto(pai, don[rng.Int(don.Count)], rng.Uniform(18, 32), $"a leitura puxa o {quer}: nosso default e a execução no {quer}");
                }
                string queimada = AreaQueimada(L, Team.T);
                if (queimada != null) { var e = Evitar(pai, queimada, banco, rng, $"a gente vem morrendo em {Estrategias.Callout(queimada)}: outro caminho"); if (e != null) return e; }
                if (L.intencao.ritmo < 0.4f) return Ritmo(pai, rng.Uniform(1.15f, 1.3f), "o CT deles sai cedo: mesma jogada mais lenta pra punir");
                if (L.ctRounds >= 3 && L.ctAgressivo == 0) return Ritmo(pai, rng.Uniform(0.78f, 0.9f), "o CT deles é passivo: mesma jogada mais rápida");
            }
            // sem motivo da leitura: variação de repertório (o que um analista traria do estudo)
            float x = rng.Rand();
            if (x < 0.45f) { var d = banco[rng.Int(banco.Count)]; if (d.idx != pai.idx) return Enxerto(pai, d, rng.Uniform(15, 35), $"default do #{IdxCurto(pai)} com a execução do #{d.idx} no {d.site}"); }
            if (x < 0.65f) return Fake(pai, banco, rng, "fake com dois no outro bomb antes de entrar");
            if (x < 0.85f) return Lurk(pai, banco, rng, "um lurk do outro lado pra pegar a rotação");
            return Ritmo(pai, rng.Rand() < 0.5f ? rng.Uniform(0.8f, 0.9f) : rng.Uniform(1.12f, 1.25f), "mesma jogada em outro ritmo");
        }

        static Estrategias.Plano IdeiaCT(Estrategias.Plano pai, List<Estrategias.Plano> banco, Leitura L, float q, Rng rng)
        {
            bool segue = L != null && L.trRounds >= 3 && rng.Rand() < 0.3f + 0.6f * q;
            if (segue)
            {
                float a = Leitura.Taxa(L.trA, L.trA + L.trB);
                if (a > 0.6f) { var s = Stack(pai, 'A', banco, rng, $"eles foram A em {L.trA} de {L.trA + L.trB}: mais um no A"); if (s != null) return s; }
                if (a < 0.4f) { var s = Stack(pai, 'B', banco, rng, $"eles foram B em {L.trB} de {L.trA + L.trB}: mais um no B"); if (s != null) return s; }
                if (Leitura.Taxa(L.trLento, L.trRounds, 0.15f) > 0.3f) { var g = Agressivo(pai, banco, rng, "o TR deles joga lento: um sai cedo pra pegar info"); if (g != null) return g; }
            }
            float x = rng.Rand();
            if (x < 0.5f) return Stack(pai, rng.Rand() < 0.5f ? 'A' : 'B', banco, rng, "setup diferente: um a mais num bomb");
            return Agressivo(pai, banco, rng, "um sai cedo pra pegar info");
        }

        static string IdxCurto(Estrategias.Plano p) => p.criada ? "criada " + (p.idx - BASE_IDX) : p.idx.ToString();

        /// <summary>Área (callout) onde o time mais morreu jogando desse lado nos últimos rounds — se for bem mais que o resto.</summary>
        static string AreaQueimada(Leitura L, Team lado)
        {
            var cont = new Dictionary<string, int>();
            foreach (var (p, l, rodada) in L.mortes) { if (l != lado || L.rodada - rodada > 6) continue; string a = AreaAt(p.x, p.y); if (a == null) continue; cont.TryGetValue(a, out int c); cont[a] = c + 1; }
            if (cont.Count == 0) return null;
            var top = cont.OrderByDescending(kv => kv.Value).First();
            return top.Value >= 3 ? top.Key : null;
        }

        /// <summary>Filtro de mesa (antes do treino): todo mundo tem rota, ninguém chega no bomb depois dos 95 s, o TR pisa no bomb.</summary>
        public static bool Sensata(Estrategias.Plano p)
        {
            for (int k = 0; k < 5; k++) if (p.js[k].passos.Length == 0) return false;
            if (p.tr)
            {
                float h = p.HoraDeEntrar(70); if (h > 85) return false;
                int noBomb = 0; for (int k = 0; k < 5; k++) if (p.PrimeiroEm(k, p.site.ToString()) >= 0) noBomb++;
                if (noBomb < 2) return false;
            }
            else
            {
                int nA = 0, nB = 0; for (int k = 0; k < 5; k++) { var z = p.ZonaEntre(k, 15, 30); if (z == "A") nA++; else if (z == "B") nB++; }
                if (nA == 0 || nB == 0) return false;   // CT sem ninguém num bomb não é setup
            }
            return true;
        }

        /// <summary>Resumo legível da rota (zona por zona) pra relatório e pro coach ver.</summary>
        public static string Resumo(Estrategias.Plano p)
        {
            var sb = new System.Text.StringBuilder();
            for (int k = 0; k < 5; k++)
            {
                var vis = new List<string>(); string ult = null;
                foreach (var ps in p.js[k].passos) { string c = Estrategias.Callout(ps.area); if (c != ult) { vis.Add($"{c}@{ps.de:0}"); ult = c; } }
                sb.Append($"      j{k + 1}: {string.Join(" → ", vis.Take(9))}{(vis.Count > 9 ? " …" : "")}\n");
            }
            return sb.ToString();
        }
    }

    /// <summary>
    /// Repertório do clube: táticas criadas que passaram no treino. Confiança sobe com treino e com resultado em jogo,
    /// cai quando para de funcionar; cheio, sai a pior. Persistente na carreira (o clube guarda).
    /// </summary>
    public class Repertorio
    {
        public class Item { public Estrategias.Plano p; public float conf; public int n, w, treinos; }
        public readonly List<Item> itens = new List<Item>();
        public int MAX = 12;   // por lado

        public IEnumerable<Estrategias.Plano> Planos(bool tr) => itens.Where(i => i.p.tr == tr).Select(i => i.p);
        public Item De(Estrategias.Plano p) => itens.FirstOrDefault(i => i.p == p);

        public void Adiciona(Estrategias.Plano p, float conf)
        {
            itens.Add(new Item { p = p, conf = conf, treinos = 1 });
            var lado = itens.Where(i => i.p.tr == p.tr).ToList();
            if (lado.Count > MAX) itens.Remove(lado.OrderBy(Valor).First());
        }
        /// <summary>Resultado no jogo: taxa com prior pela confiança do treino (uns rounds ruins não matam uma boa).</summary>
        public void Resultado(Estrategias.Plano p, bool ganhou)
        {
            var i = De(p); if (i == null) return;
            i.n++; if (ganhou) i.w++;
            i.conf = MathX.Clamp(i.conf + (ganhou ? 0.04f : -0.06f), 0.1f, 1f);
        }
        public static float Valor(Item i) => Leitura.Taxa(i.w, i.n, 0.35f + 0.3f * i.conf, 4) * (0.7f + 0.3f * i.conf);
        /// <summary>Peso de escolha no jogo (perto de 1 = igual a uma tática média do banco).</summary>
        public float Peso(Estrategias.Plano p) { var i = De(p); return i == null ? 0 : MathX.Clamp(2f * Valor(i), 0.2f, 2f); }
    }
}
