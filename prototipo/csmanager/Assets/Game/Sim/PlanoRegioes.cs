using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Plano de time por regiões (sim-apoio, 2026-11-08): no começo do round (ou numa recall) cada um dos 5 ganha a sua
    /// sequência de regiões, sorteada do RotaHumana (rota do pro), e o conjunto é escolhido pra o time se espalhar como o pro:
    /// regiões distintas por vivo e "3+ na mesma região" por fase (cedo &lt;25 s, meio 25-50, tarde 50+), medidos no playbook
    /// (Tools/pro/regua.json: Mirage pra Vila, Inferno pra Brasa). Não é regra "cada um num lugar": é a sobreposição do pro.
    ///
    /// Como: pra cada jogador sorteia CAND rotas candidatas (RotaHumana com de-onde-veio, permanência sorteada, até HORIZ s);
    /// depois algumas voltas de escolha por jogador com peso exp(-BETA * erro do time contra a régua) — softmax, não o melhor,
    /// pra cada round sair diferente. A função pesa: lurker quer a rota que menos divide região; entry e suporte (TR) aceitam
    /// dividir (entram juntos); âncora (CT) fica mais (rota curta); com bombAlvo (TR) quem não é lurker prefere a rota que
    /// chega naquele bomb.
    /// </summary>
    public static class PlanoRegioes
    {
        public const float HORIZ = 70f, DT = 0.5f, BETA = 15f, BETA_T = 5f, AGRUPA = 0.6f;   /* TR: as candidatas (com "seguir o colega") já agrupam perto do pro; empurrar forte achatava o "3+" do meio */
        public const int CAND = 12, VOLTAS = 4, MAXREG = 9;
        /// <summary>Só pra teste (Headless -planoRegioes): cada um sorteia a sua rota sem olhar o time.</summary>
        public static bool SoIndependente;

        /// <summary>Régua do pro por fase: (regiões distintas por vivo, fração do tempo com 3+ na mesma região).</summary>
        public static (float dist, float tres) Regua(Team lado, int fase)
        {
            bool brasa = MapData.MAPA == "brasa", t = lado == Team.T;
            // Tools/pro/regua.json (full buy, antes do plant, amostra a cada 0,5 s)
            var r = brasa
                ? (t ? new[] { (0.673f, 0.348f), (0.665f, 0.358f), (0.622f, 0.434f) } : new[] { (0.766f, 0.156f), (0.811f, 0.078f), (0.825f, 0.061f) })
                : (t ? new[] { (0.741f, 0.246f), (0.792f, 0.157f), (0.764f, 0.197f) } : new[] { (0.819f, 0.098f), (0.869f, 0.048f), (0.851f, 0.055f) });
            return r[Math.Clamp(fase, 0, 2)];
        }
        public static int Fase(float t) => t < 25 ? 0 : t < 50 ? 1 : 2;

        /// <summary>Sequência de regiões de cada jogador (a primeira é a atual). funcoes = (int)Role de cada um; regiaoAtual
        /// aceita área ou região. bombAlvo ("A"/"B", só TR) puxa a rota pro bomb do plano.</summary>
        public static string[][] Distribuir(Team lado, float t, string[] regiaoAtual, int[] funcoes, Rng rng, string bombAlvo = null)
        {
            var p = DistribuirComTempos(lado, t, regiaoAtual, funcoes, rng, bombAlvo);
            var r = new string[p.Length][];
            for (int i = 0; i < p.Length; i++) { r[i] = new string[p[i].Length]; for (int k = 0; k < p[i].Length; k++) r[i][k] = p[i][k].regiao; }
            return r;
        }

        /// <summary>Igual ao Distribuir, com a hora (s de round) prevista de chegada em cada região.</summary>
        public static (string regiao, float chega)[][] DistribuirComTempos(Team lado, float t, string[] regiaoAtual, int[] funcoes, Rng rng, string bombAlvo = null)
        {
            int n = regiaoAtual.Length;
            var cand = new List<(string regiao, float chega)>[n][]; var peso = new float[n][];
            for (int i = 0; i < n; i++)
            {
                cand[i] = new List<(string, float)>[CAND]; peso[i] = new float[CAND];
                Role f = funcoes != null && i < funcoes.Length ? (Role)funcoes[i] : Role.Suporte;
                for (int c = 0; c < CAND; c++)
                {
                    cand[i][c] = Rota(lado, t, regiaoAtual[i], f, rng, bombAlvo);
                    float w = 1f;
                    if (bombAlvo != null && lado == Team.T && f != Role.Lurker)
                    {   bool chega = false, outro = false;
                        foreach (var (rg, _) in cand[i][c]) { if (rg == bombAlvo) chega = true; else if (rg == "A" || rg == "B") outro = true; }
                        w = chega ? 1f : outro ? 0.1f : 0.35f;
                    }
                    peso[i][c] = w;
                }
            }
            // seguir o colega: o pro anda junto (TR "3+ na mesma" 25-43%), e rotas sorteadas cada uma por si quase nunca coincidem.
            // Parte das candidatas vira a rota de um colega que sai do mesmo lugar, uns 0,3-1,5 s atrás (TR: 6; CT: 2).
            int seg = lado == Team.T ? 6 : 2;
            for (int i = 0; i < n; i++)
            {
                Role f = funcoes != null && i < funcoes.Length ? (Role)funcoes[i] : Role.Suporte;
                if (f == Role.Lurker) continue;
                for (int c = CAND - seg; c < CAND; c++)
                {
                    int j = rng.Int(n); if (j == i || cand[j][0].Count == 0 || cand[i][c].Count == 0 || cand[j][0][0].regiao != cand[i][c][0].regiao) continue;
                    var o = cand[j][rng.Int(CAND - seg)]; float atraso = rng.Uniform(0.3f, 1.5f);
                    var cp = new List<(string, float)> { cand[i][c][0] };
                    for (int k = 1; k < o.Count; k++) cp.Add((o[k].regiao, o[k].chega + atraso));
                    cand[i][c] = cp; peso[i][c] = peso[j][0];
                }
            }
            // meta deste round pra "3+ na mesma região", por fase: a régua é média de rounds que juntam e rounds que não juntam;
            // mirar a média em todo round empurra todos pro meio-termo (dava 0,03 no lugar de 0,16). Sorteia: junta (fração
            // AGRUPA do tempo da fase) com chance régua/AGRUPA, senão 0 — a média fica a do pro e cada round é um ou outro.
            var meta3 = new float[3];
            for (int f = 0; f < 3; f++) { float r3 = Regua(lado, f).tres; meta3[f] = rng.Rand() < MathF.Min(1f, r3 / AGRUPA) ? AGRUPA : 0f; }
            var esc = new int[n];
            for (int i = 0; i < n; i++) esc[i] = rng.Weighted(peso[i]);
            var tmp = new float[CAND];
            for (int v = 0; v < (SoIndependente ? 0 : VOLTAS); v++)
                for (int i = 0; i < n; i++)
                {
                    Role f = funcoes != null && i < funcoes.Length ? (Role)funcoes[i] : Role.Suporte;
                    // as vagas de "seguir" passam a copiar a rota que o colega ESCOLHEU (copiar uma candidata qualquer dele não junta ninguém)
                    if (f != Role.Lurker)
                        for (int c = CAND - seg, k = 0; c < CAND; c++, k++)
                        {
                            int j = (i + 1 + k) % n; if (j == i) continue;
                            var o = cand[j][esc[j]]; if (o.Count == 0 || cand[i][c].Count == 0 || o[0].regiao != cand[i][c][0].regiao) continue;
                            float atraso = rng.Uniform(0.3f, 1.5f); var cp = new List<(string, float)> { cand[i][c][0] };
                            for (int q = 1; q < o.Count; q++) cp.Add((o[q].regiao, o[q].chega + atraso));
                            cand[i][c] = cp; peso[i][c] = peso[j][esc[j]];
                        }
                    for (int c = 0; c < CAND; c++)
                    {
                        int antes = esc[i]; esc[i] = c;
                        float erro = Erro(lado, t, cand, esc, meta3);
                        esc[i] = antes;
                        // lurker: quanto menos divide região com o time, melhor; entry/suporte TR: dividir não pesa contra
                        float extra = f == Role.Lurker ? 1.5f * Dividi(cand, esc, i, c, t) : 0f;
                        tmp[c] = peso[i][c] * MathF.Exp(-(lado == Team.T ? BETA_T : BETA) * (erro + extra));
                    }
                    esc[i] = rng.Weighted(tmp);
                }
            var r = new (string, float)[n][];
            for (int i = 0; i < n; i++) r[i] = cand[i][esc[i]].ToArray();
            return r;
        }

        /// <summary>Uma rota do pro a partir da região atual: sorteia permanência e próxima (com de-onde-veio) até HORIZ s.</summary>
        static List<(string regiao, float chega)> Rota(Team lado, float t0, string atual, Role f, Rng rng, string alvo)
        {
            var r = new List<(string, float)>(); string rg = RotaHumana.RegiaoDaArea(atual), ant = null;
            if (rg == null) return r;
            float t = t0; r.Add((rg, t0));
            // âncora (CT) e AWP seguram mais: permanência puxada pro quantil de cima
            float puxa = f == Role.Ancora || f == Role.Awper ? 0.35f : 0f;
            bool primeiro = true;
            while (t < t0 + HORIZ && r.Count < MAXREG)
            {
                float u = rng.Rand(); u = u + puxa * (1 - u);
                float d = RotaHumana.SorteiaPermanencia(lado, rg, u);
                if (primeiro && t0 > 0) d *= rng.Rand();   // já está aqui há um tempo: só o que falta
                primeiro = false;
                t += MathF.Max(d, 0.3f);
                if (t >= t0 + HORIZ) break;
                var ops = RotaHumana.Proximas(lado, rg, t, false, ant);
                if (ops.Count == 0) break;
                if (alvo != null && lado == Team.T && f != Role.Lurker)   // o plano puxa pro bomb dele: mais peso pro lado do alvo
                    for (int k = 0; k < ops.Count; k++)
                    {   string q = ops[k].regiao, fq = RotaHumana.Funcao(q) ?? "";
                        float m = q == alvo ? 3f : (q == "A" || q == "B") ? 0.15f : fq.StartsWith(alvo + "_") ? 1.8f : 1f;
                        ops[k] = (q, ops[k].peso * m);
                    }
                float tot = 0; foreach (var o in ops) tot += o.peso;
                float x = rng.Rand() * tot; string v = ops[ops.Count - 1].regiao;
                foreach (var o in ops) { x -= o.peso; if (x <= 0) { v = o.regiao; break; } }
                ant = rg; rg = v; r.Add((rg, t));
                if (lado == Team.T && (rg == "A" || rg == "B")) break;   // no bomb o plano acaba (dali é o plant/pós-plant)
            }
            return r;
        }

        static string Onde(List<(string regiao, float chega)> rota, float t)
        {
            string s = null; foreach (var (rg, c) in rota) { if (c > t) break; s = rg; }
            return s;
        }

        /// <summary>Erro do time contra a régua: |distintas - régua| + |3+ - régua| por fase, média nas fases com amostra.</summary>
        static float Erro(Team lado, float t0, List<(string, float)>[][] cand, int[] esc, float[] meta3)
        {
            int n = esc.Length; var sd = new float[3]; var s3 = new float[3]; var na = new int[3];
            var cont = new Dictionary<string, int>();
            for (float t = MathF.Max(t0, 5f); t < t0 + HORIZ; t += DT)
            {
                cont.Clear(); int vivos = 0;
                for (int i = 0; i < n; i++) { var s = Onde(cand[i][esc[i]], t); if (s == null) continue; vivos++; cont.TryGetValue(s, out int c); cont[s] = c + 1; }
                if (vivos < 3) continue;
                if (lado == Team.T && (cont.TryGetValue("A", out int na_) ? na_ : 0) + (cont.TryGetValue("B", out int nb_) ? nb_ : 0) >= 2) break;   // 2+ no bomb = plantou (a régua é antes do plant)
                int mx = 0; foreach (var c in cont.Values) mx = Math.Max(mx, c);
                int f = Fase(t); sd[f] += (float)cont.Count / vivos; s3[f] += mx >= 3 ? 1 : 0; na[f]++;
            }
            float e = 0; int fs = 0;
            for (int f = 0; f < 3; f++)
            {
                if (na[f] == 0) continue;
                var (rd, r3) = Regua(lado, f);
                e += 0.5f * MathF.Abs(sd[f] / na[f] - rd) + MathF.Abs(s3[f] / na[f] - meta3[f]); fs++;   /* distintas pesa metade: no round que junta ela cai junto */
            }
            return fs > 0 ? e / fs : 0f;
        }

        /// <summary>Fração do tempo em que a rota c do jogador i divide região com alguém do time.</summary>
        static float Dividi(List<(string, float)>[][] cand, int[] esc, int i, int c, float t0)
        {
            int tot = 0, div = 0;
            for (float t = MathF.Max(t0, 5f); t < t0 + HORIZ; t += DT * 2)
            {
                var s = Onde(cand[i][c], t); if (s == null) continue; tot++;
                for (int j = 0; j < esc.Length; j++) if (j != i && Onde(cand[j][esc[j]], t) == s) { div++; break; }
            }
            return tot > 0 ? (float)div / tot : 0f;
        }

        // ================================================================ plano até um destino (rotação, retake, ir pro bomb, setup sem banco)
        /// <summary>
        /// Cada um tem destino (rotação/retake/ir pro bomb/posição do setup): escolhe a sequência de regiões de cada um até lá,
        /// pela rota do pro (RotaHumana com de-onde-veio, até o destino, sem volta absurda: &lt; 1,9x a reta), e o conjunto
        /// pra não ir 3+ pela mesma região na mesma hora (o hive mind); 2 juntos pode (troca). Entre as candidatas de cada um
        /// está "o caminho direto" (A*) e "seguir o colega" (a escolha de quem sai da mesma região pro mesmo destino).
        /// Devolve null pra quem não tem rota pelas regiões (já está na região do destino, ou perto).
        /// </summary>
        public static string[][] DistribuirAte(Team lado, float t, V2[] pos, V2[] destino, bool plantado, Rng rng)
        {
            int n = pos.Length; var cand = new List<(string[] regs, List<(string, float)> tl, float w)>[n];
            var de = new string[n]; var ate = new string[n];
            for (int i = 0; i < n; i++)
            {
                de[i] = RotaHumana.Regiao(pos[i]); ate[i] = RotaHumana.Regiao(destino[i]);
                cand[i] = Candidatas(lado, t, pos[i], destino[i], de[i], ate[i], plantado);
            }
            var esc = new (string[] regs, List<(string, float)> tl, float w)[n];
            for (int i = 0; i < n; i++) esc[i] = cand[i].Count > 0 ? cand[i][rng.Weighted(cand[i].Select(c => c.w).ToArray())] : (null, null, 0);
            for (int v = 0; v < VOLTAS; v++)
                for (int i = 0; i < n; i++)
                {
                    if (cand[i].Count == 0) continue;
                    var ops = new List<(string[] regs, List<(string, float)> tl, float w)>(cand[i]);
                    float wmax = cand[i].Max(c => c.w);
                    for (int j = 0; j < n; j++)   // seguir o colega: mesma saída, mesmo destino, um pouco atrás
                        if (j != i && esc[j].regs != null && de[j] == de[i] && ate[j] == ate[i])
                        {   float atraso = rng.Uniform(0.3f, 1.5f); var tl = new List<(string, float)>();
                            foreach (var (rg, ch) in esc[j].tl) tl.Add((rg, ch + atraso));
                            ops.Add((esc[j].regs, tl, 0.5f * wmax));
                        }
                    var w = new float[ops.Count];
                    for (int c = 0; c < ops.Count; c++)
                    {   var antes = esc[i]; esc[i] = ops[c];
                        w[c] = ops[c].w * MathF.Exp(-BETA_ATE * Amontoa(esc, t));
                        esc[i] = antes;
                    }
                    esc[i] = ops[rng.Weighted(w)];
                }
            var r = new string[n][];
            for (int i = 0; i < n; i++) r[i] = esc[i].regs;
            return r;
        }
        public const float BETA_ATE = 8f;

        /// <summary>Fração do tempo (até todos chegarem, no máx. 30 s) com 3+ na mesma região, contando quantos a mais.</summary>
        static float Amontoa((string[] regs, List<(string, float)> tl, float w)[] esc, float t0)
        {
            float fim = t0; foreach (var e in esc) if (e.tl != null && e.tl.Count > 0) fim = MathF.Max(fim, e.tl[e.tl.Count - 1].Item2);
            fim = MathF.Min(fim, t0 + 30f); int ns = 0; float custo = 0; var cont = new Dictionary<string, int>();
            for (float t = t0; t <= fim; t += DT)
            {
                cont.Clear();
                foreach (var e in esc) { if (e.tl == null) continue; var s = Onde(e.tl, t); if (s == null) continue; cont.TryGetValue(s, out int c); cont[s] = c + 1; }
                foreach (var c in cont.Values) if (c >= 3) custo += c - 2;
                ns++;
            }
            return ns > 0 ? custo / ns : 0f;
        }

        /// <summary>Rotas candidatas pelas regiões até o destino (como Brain.PlanejaRota) e o caminho direto, com a hora de chegada.</summary>
        static List<(string[] regs, List<(string, float)> tl, float w)> Candidatas(Team lado, float t, V2 pos, V2 destino, string de, string ate, bool pl)
        {
            var r = new List<(string[] regs, List<(string, float)> tl, float w)>();
            if (de == null || ate == null || de == ate || (destino - pos).Len < 350) return r;
            float vel = MapCheck.RUN * 0.9f, direto = MathF.Max(1, (destino - pos).Len);
            List<(string, float)> Linha(string[] regs)
            {   var tl = new List<(string, float)> { (de, t) }; V2 q = pos; float tt = t;
                foreach (var rg in regs) { V2 cc = Estrategias.Centro(rg); tt += (cc - q).Len / vel; q = cc; tl.Add((rg, tt)); }
                tt += (destino - q).Len / vel; tl.Add((ate, tt)); return tl;
            }
            var pilha = new List<string> { de }; var achou = new List<(string[] regs, float p)>();
            void Dfs(string cur, float prob)
            {
                if (achou.Count >= 40 || pilha.Count > 7) return;
                foreach (var (rg, w) in RotaHumana.Proximas(lado, cur, t, pl, pilha.Count > 1 ? pilha[pilha.Count - 2] : null))
                {
                    if (w < 0.03f || pilha.Contains(rg)) continue;
                    if (rg == ate) { achou.Add((pilha.Skip(1).ToArray(), prob * w)); continue; }
                    pilha.Add(rg); Dfs(rg, prob * w); pilha.RemoveAt(pilha.Count - 1);
                }
            }
            Dfs(de, 1f);
            foreach (var (regs, p) in achou.OrderByDescending(x => x.p).Take(CAND))
            {
                float L = 0; V2 q = pos; foreach (var rg in regs) { V2 cc = Estrategias.Centro(rg); L += (cc - q).Len; q = cc; } L += (destino - q).Len;
                if (L < 1.9f * direto) r.Add((regs, Linha(regs), p));
            }
            float pm = r.Count > 0 ? r.Max(x => x.w) : 1f;
            r.Add((new string[0], Linha(new string[0]), 0.15f * pm));   // o caminho direto (A*) também é opção, com pouco peso
            return r;
        }
    }
}
