using System;
using System.Collections.Generic;
using CSM.Map;
using CSM.View;

namespace CSM.Sim
{
    /// <summary>
    /// bx: detectores de HUMANIDADE das granadas (o que um pro nunca faz), pro validador de humanidade (Humano.cs).
    /// Só mede a verdade do jogo, não muda nada. Cada cena vira contagem por round (Ev "humano: ...") e exemplo
    /// (seed, t, jogador, região, fase, detalhe). Cinco classes:
    ///  1. smoke que não cobriu nada: a vida inteira (2,5 s até 13 s) sem tapar uma linha inimigo↔nós que a parede deixaria
    ///     e sem inimigo passando perto dela (é a choke que importa);
    ///  (2. granada exposta: já existe no Humano.cs — "granada exposto" / "com inimigo na cara" — não duplica);
    ///  3. bang → peek → matou o cegado? (cego total de um inimigo: alguém do time abriu nele e matou enquanto ele estava cego?);
    ///  4. reação à smoke inimiga: quem olhava pela área que a smoke fechou virou ou saiu do lugar em 2 s?;
    ///  5. erro do lineup: distância do pouso até o alvo (por tipo, sabida x no olho).
    /// </summary>
    public sealed class HumanoGranada
    {
        readonly Match m;
        public HumanoGranada(Match m) { this.m = m; }
        /// <summary>Exemplos (no máximo alguns por tipo e por round) pro relatório do validador.</summary>
        public readonly List<(string tipo, int seed, float t, string quem, string onde, string fase, string detalhe)> exemplos = new List<(string, int, float, string, string, string, string)>();
        readonly Dictionary<string, int> porTipo = new Dictionary<string, int>();
        /// <summary>Quem quiser os exemplos em outro lugar (o esqueleto do Humano.cs) assina aqui.</summary>
        public static Action<Match, string, Agent, V2, string> Cena;
        /// <summary>Liga sem a auditoria (o -humano do Headless).</summary>
        public static bool Ligado;

        void Marca(string tipo, Agent a, V2 onde, string detalhe)
        {
            m.Ev("humano: " + tipo + "/round");
            porTipo.TryGetValue(tipo, out int k); porTipo[tipo] = k + 1;
            if (k < 3) exemplos.Add((tipo, m.seed, m.t, a?.name ?? "-", Places.Region(onde), a?.brain?.fase ?? "-", detalhe));
            Cena?.Invoke(m, tipo, a, onde, detalhe);
            if (TeamBrain.EXPF.Contains("hgDump"))   /* depuração: todo exemplo num arquivo (ler ~10 por cena antes de confiar) */
                lock (trava) System.IO.File.AppendAllText("Logs/bots/hg_dump.txt", $"{tipo} | semente {m.seed} {m.t:0.0}s | {a?.name} | {(a == null ? "-" : a.IsT ? "TR" : "CT")} | {Places.Region(onde)} | {a?.brain?.fase} | {detalhe}" + Environment.NewLine);
        }
        static readonly object trava = new object();
        void Total(string tipo) => m.Ev("humano: " + tipo + " (total)/round");

        // ───────────────────────── 5. erro do lineup (no pouso, antes do alvo virar o ponto da física) ─────────────────────────
        float ultErro; bool ultSabe;
        public void Pousou(Match.Nade n, V2 alvo)
        {
            float e = (n.P - alvo).Len; string k = n.l.kind;
            /* tolerância = o que ainda faz o trabalho: smoke ~raio da fumaça da tela (~45), molotov ~raio do fogo, HE/flash maiores */
            float tol = k == "smoke" ? 45 : k == "molotov" ? 40 : k == "he" ? 60 : 150;
            /* v2 (lido 10 exemplos): no olho o pro também espalha com a distância (~3% dela); flash não tem pixel (cega largo) */
            if (!n.sabe) tol += 0.03f * (alvo - n.from).Len;
            ultErro = e; ultSabe = n.sabe;
            Total("granada (" + k + ")");
            m.Aud("humano: erro do lineup (" + k + ")", (n.sabe ? "sabida · " : "no olho · ") + (e < tol * 0.5f ? "a no alvo" : e < tol ? "b perto" : e < tol * 2 ? "c errou" : "d errou feio"));
            if (e >= tol && k != "flash")
                m.Aud("humano: errou o lineup — por quê", k + " · " + (n.sabe ? "sabida · " : "no olho · ") + (n.erroPlano >= 25 ? "a o plano perfeito já não chegava (25+)" : n.erroPlano >= 10 ? "b plano 10-25 + a mão" : n.paredes > 0 ? "c a mão errou e bateu na parede" : "d a mão errou") + (n.distPlano > 700 ? " · longe" : ""));
            if (e >= tol && k != "flash")
                m.Aud("humano: errou o lineup — teto x parede", (n.tetos > 0 ? "bateu no TETO" : "não bateu no teto") + " · " + (n.paredes > 0 ? "bateu na parede" : "sem parede") + (n.erroPlano >= 25 ? (n.planoTeto ? " · plano 25+ batendo no teto" : " · plano 25+ sem teto") : ""));
            if (k != "flash") m.Aud("humano: granada (smoke/molotov/HE) passou por cima de prédio?", n.sobreCasa ? "sim" : "não");
            if (k != "flash") m.Aud("humano: granada (smoke/molotov/HE) bateu no teto?", (n.tetos > 0 ? "sim" : "não") + (n.planoTeto ? " · o plano já batia" : ""));
            if (e >= tol && (k != "flash" || n.paredes > 0)) Marca("granada errou o lineup", n.by, n.P, $"{k} {(n.sabe ? "sabida" : "no olho")} caiu a {e:0} do alvo ({Places.Region(alvo)}){(n.paredes > 0 ? " · bateu na parede" : "")}");
        }

        // ───────────────────────── 1. smoke que não cobriu nada ─────────────────────────
        public static float CORTE_W = 0.02f, CORTE_ESP = 0.3f;
        sealed class SmokeReg { public float wMax, espMax; public float erro; public bool sabe, crenca; public string nome, fase; public SmokeVolume s; public Agent by; public Team time; public bool cobriu, viuIni; public float prox; public int amostras, tapou; public bool fim; }
        readonly List<SmokeReg> smokes = new List<SmokeReg>();
        public void SmokeNasceu(SmokeVolume s, Agent by, string nome = "") { smokes.Add(new SmokeReg { erro = ultErro, sabe = ultSabe, nome = nome, fase = by.brain?.fase, s = s, by = by, time = by.team, prox = m.t + 1.2f }); }   /* v3: smoke de cruzar trabalha ainda crescendo */

        /// <summary>Esta smoke (só ela) tapa a linha a→b? (a conta do SmokeBlocks, uma smoke)</summary>
        bool Tapa(SmokeVolume s, V2 a, float za, V2 b, float zb)
        {
            var (mn, mx) = s.Bounds;
            /* corte rápido: a linha passa perto da caixa? */
            float L = (b - a).Len; int n = (int)(L / 6) + 1; float passo = L / n, tau = 0;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n; V2 p = a + (b - a) * u; float z = za + (zb - za) * u;
                if (p.x < mn.x || p.y < mn.y || p.x > mx.x || p.y > mx.y) continue;
                tau += s.SampleVis(new V3(p.x, p.y, z), m.t) * passo;
                if (tau * 0.9f > 2.3f) return true;
            }
            return false;
        }

        void SmokesTick()
        {
            foreach (var r in smokes)
            {
                if (r.fim || m.t < r.prox) continue;
                r.prox = m.t + 0.5f;
                if (r.s.age > SmokeVolume.LIFE - 2f || !m.smokes.Contains(r.s)) { FechaSmoke(r); continue; }
                bool temIni = false, temMeu = false;
                foreach (var x in m.agents) if (x.alive) { if (x.team == r.time) temMeu = true; else temIni = true; }
                if (!temIni || !temMeu) continue;   // round decidido: não conta
                r.amostras++;
                V2 o = new V2(r.s.origin.x, r.s.origin.y); bool tapou = false;
                foreach (var e in m.agents)
                {
                    if (!e.alive || e.team == r.time) continue;
                    if ((e.pos - o).Len < 250) { r.viuIni = true; tapou = true; }   // inimigo passando/na choke da smoke
                    foreach (var a in m.agents)
                    {
                        if (tapou) break;
                        if (!a.alive || a.team != r.time || (a.pos - e.pos).Len > 2000) continue;
                        if (!MapData.LosWalk(a.pos, a.z + a.Eye, e.pos, e.z + e.Eye, 0)) continue;   // a parede já tapava
                        if (Tapa(r.s, a.pos, a.z + a.Eye, e.pos, e.z + e.Eye)) tapou = true;
                    }
                }
                if (!tapou && !r.crenca)
                {   /* v2: smoke de stall/execução onde o time acreditava ter inimigo é legítima (o pro joga pela informação) */
                    /* v4: a crença é a linha tapada até onde o time acredita ter inimigo: soma do peso das células prováveis (até 900
                       dela) cuja linha a partir de um colega a smoke tapa. v2 (célula >= 0,01) legitimava 1,6%; v3 (esperados em
                       700) 98%. Mede a distribuição e escolhe o corte */
                    var it = m.intel[(int)r.time]; float wTapa = 0;
                    foreach (var (c, w) in it.Provaveis(o, 900, 16))
                    {
                        float zc = Intel.Z(Intel.Idx(c)) + 18;
                        foreach (var a in m.agents)
                            if (a.alive && a.team == r.time && (a.pos - c).Len < 2000 && MapData.LosWalk(a.pos, a.z + a.Eye, c, zc, 0) && Tapa(r.s, a.pos, a.z + a.Eye, c, zc)) { wTapa += w; break; }
                    }
                    r.wMax = MathF.Max(r.wMax, wTapa); r.espMax = MathF.Max(r.espMax, it.Esperados(o, 300));
                    /* v5 (distribuição de 118 smokes sem verdade): a linha tapada até a crença quase nunca aparece (96% < 0,005), mas 71%
                       caíram onde o time esperava 0,3+ inimigos a 300 — smoke em cima de onde ele acredita que estão é jogada de pro
                       (a crença estava errada, não a smoke). Sobra a smoke sem verdade e sem crença */
                    if (r.wMax >= CORTE_W || r.espMax >= CORTE_ESP) r.crenca = true;
                }
                if (tapou) { r.cobriu = true; r.tapou++; }
            }
        }
        void FechaSmoke(SmokeReg r)
        {
            r.fim = true;
            if (r.amostras == 0) return;   // round acabou/decidido antes de ela formar
            Total("smoke");
            float f = r.tapou / (float)r.amostras;
            m.Aud("humano: smoke cobriu (parte da vida)", r.tapou == 0 ? "a nada" : f < 0.25f ? "b pouco (<25%)" : f < 0.6f ? "c parte" : "d quase toda");
            if (!r.cobriu)
            {
                m.Aud("humano: smoke sem verdade — peso da crença tapado (máx)", r.wMax < 0.005f ? "a <0,005" : r.wMax < 0.01f ? "b 0,005-0,01" : r.wMax < 0.02f ? "c 0,01-0,02" : r.wMax < 0.05f ? "d 0,02-0,05" : r.wMax < 0.1f ? "e 0,05-0,1" : "f 0,1+");
                m.Aud("humano: smoke sem verdade — inimigos esperados a 300 (máx)", r.espMax < 0.05f ? "a <0,05" : r.espMax < 0.15f ? "b 0,05-0,15" : r.espMax < 0.3f ? "c 0,15-0,3" : r.espMax < 0.6f ? "d 0,3-0,6" : "e 0,6+");
            }
            if (!r.cobriu) m.Aud("humano: smoke que não cobriu nada — e pela crença?", r.crenca ? "a tapava onde o time acreditava (legítima)" : r.erro > 45 ? "b caiu longe do alvo" : "c no alvo, mas o alvo não tapava nada");
            if (!r.cobriu && !r.crenca) Marca("smoke que não cobriu nada", r.by, new V2(r.s.origin.x, r.s.origin.y), $"{r.nome} (jogou em: {r.fase}) · {(r.sabe ? "sabida" : "no olho")}, caiu a {r.erro:0} do alvo · crença tapada {r.wMax:0.000}, esperados a 300 {r.espMax:0.00} · {r.amostras} amostras");
        }

        // ───────────────────────── 3. bang → peek → matou o cegado? ─────────────────────────
        sealed class BangReg { public string chega; public int ticksLargou, ticksOutro, ticksNulo; public float tiro1 = -1; public int ticksVendo, ticksAlvo, ticksCanSee; public string porqueSemTiro; public int tiros0; public V2 posCego0; public bool peekMeio; public float peekDist, hpCego0; public float peekT, alvoT = -1; public string peekFase; public string faseMeio; public string nome; public Agent cego, por; public float t, ate; public bool peek; public Agent quemPeek; public float dAliado; public bool fim; }
        readonly List<BangReg> bangs = new List<BangReg>();
        /// <summary>Flash cegou (cego total) um inimigo.</summary>
        public void Cegou(Agent cego, Agent por, float total, string nome = "")
        {
            if (cego.team == por.team || total <= 0) return;
            float dA = 1e9f; foreach (var x in m.agents) if (x.alive && x.team == por.team && !x.Blind(m.t)) dA = MathF.Min(dA, (x.pos - cego.pos).Len);
            bangs.RemoveAll(b => b.cego == cego && !b.fim);   // vale a última
            if (Brain.AbreBang) m.Aud("abre na bang: decisão nas flashes que CEGARAM", m.abreDecisao.TryGetValue(por.id, out var ad) && MathF.Abs(ad.t1 - m.t) < 0.5f ? ad.d : "sem decisão (flash com dono/outro fluxo)");
            bangs.Add(new BangReg { nome = nome, cego = cego, por = por, t = m.t, ate = m.t + total + 0.4f, dAliado = dA, chega = QuemChega(cego, por, total) });
        }
        /* diagnóstico do "ninguém abriu": no estouro, quem do time (vivo, enxergando, a <900) chega mais cedo num ponto que VÊ o
           cego (caminho de verdade, na corrida), e esse sabia da flash (lançador / pediu a bang) ou não (ninguém avisou)? */
        string QuemChega(Agent cego, Agent por, float total)
        {
            Agent best = null; float bt = 1e9f;
            foreach (var x in m.agents)
            {
                if (!x.alive || x.team != por.team || x.Blind(m.t) || (x.pos - cego.pos).Len > 900) continue;
                if (MapData.LosWalk(x.pos, x.z + x.Eye, cego.pos, cego.z + cego.Eye, 0)) { best = x; bt = 0; break; }
                float vel = MathF.Max(x.MaxSpeed, 1);
                for (float r = 30; r <= 360 && r / vel < bt; r += 30)
                {
                    bool achou = false;
                    for (int k = 0; k < 16; k++)
                    {
                        V2 q = NavGrid.Snap(x.pos + V2.Ang(k * 0.3927f) * r);
                        if (!NavGrid.Movable(q) || !MapData.LosWalk(q, NavGrid.ZAt(q) + x.Eye, cego.pos, cego.z + cego.Eye, 0)) continue;
                        float pl = NavGrid.PathLenAte(x.pos, q, 450);
                        if (pl >= 450) continue;
                        if (pl / vel < bt) { bt = pl / vel; best = x; } achou = true;
                    }
                    if (achou) break;
                }
            }
            if (best == null) return "ninguém chega (sem ponto a 360)";
            string tempo = bt == 0 ? "a já via" : bt < 1f ? "b chega <1 s" : bt < total - 0.3f ? "c chega na cegueira (1 s+)" : "d só chega depois da cegueira";
            bool pediu = false; foreach (var p in m.pedidos) if (p.quem == best && p.atendido == por) pediu = true;
            return tempo + " · " + (best == por ? "o lançador" : pediu ? "pediu a bang" : "não sabia (outro)");
        }
        void BangsTick()
        {
            foreach (var b in bangs)
            {
                if (b.fim) continue;
                if (!b.cego.alive) { FechaBang(b); continue; }
                if (!b.peek)
                    foreach (var x in m.agents)
                        if (x.alive && x.team == b.por.team && !x.Blind(m.t) && (x.pos - b.cego.pos).Len < 1500 && MapData.LosWalk(x.pos, x.z + x.Eye, b.cego.pos, b.cego.z + b.cego.Eye, 0) && !m.SmokeBlocks(x.pos, x.z + x.Eye, b.cego.pos, b.cego.z + b.cego.Eye))
                        { b.peek = true; b.quemPeek = x; b.peekT = m.t; b.peekFase = Fase(x.brain?.fase); b.peekMeio = x.MeioCego(m.t); b.peekDist = (x.pos - b.cego.pos).Len; b.hpCego0 = b.cego.hp; b.tiros0 = x.shots; b.posCego0 = b.cego.pos; break; }
                if (b.peek && b.alvoT < 0 && b.quemPeek != null && b.quemPeek.brain?.target == b.cego) b.alvoT = m.t;
                if (b.peek && b.quemPeek != null && b.quemPeek.alive && b.cego.alive && m.t <= b.ate)
                {   /* diagnóstico do tiro que não sai: a cada 0,25 s, com linha nele, o abridor o tem como alvo? o CanSee dele vê? atirou? */
                    var q = b.quemPeek;
                    if (MapData.LosWalk(q.pos, q.z + q.Eye, b.cego.pos, b.cego.z + b.cego.Eye, 0))
                    {
                        b.ticksVendo++;
                        if (q.brain?.target == b.cego) b.ticksAlvo++;
                        if (q.brain != null && q.brain.CanSee(b.cego)) b.ticksCanSee++;
                        if (q.brain != null && q.brain.target != b.cego) { if (q.brain.LargouAgora(b.cego)) b.ticksLargou++; else if (q.brain.target == null) b.ticksNulo++; else b.ticksOutro++; }
                        if (b.tiro1 < 0 && q.shots > b.tiros0) b.tiro1 = m.t;
                        if (b.porqueSemTiro == null && q.brain?.target == b.cego && q.shots == b.tiros0 && m.t - b.peekT > 0.6f)
                            b.porqueSemTiro = !q.gun.CanFire(m.t) ? (q.gun.Reloading ? "recarregando" : "arma não pronta (cadência/saque)") : !q.ArmaPronta(m.t) ? "sacando" : "arma pronta e não atirou (" + (q.brain.fase ?? "-") + ")";
                    }
                }
                if (b.faseMeio == null && m.t >= b.t + 0.75f)   /* o que o time fazia no meio da cegueira (diagnóstico do "ninguém abriu") */
                {
                    Agent q = null; float dq = 1e9f;
                    foreach (var x in m.agents) if (x.alive && x.team == b.por.team && (x.pos - b.cego.pos).Len < dq) { dq = (x.pos - b.cego.pos).Len; q = x; }
                    string f = q == null ? "ninguém vivo" : (q.Blind(m.t) ? "cego · " : "") + (q == b.por ? "o lançador: " : "outro: ") + Fase(q.brain?.fase);
                    b.faseMeio = f;
                }
                if (m.t > b.ate) FechaBang(b);
            }
        }
        void FechaBang(BangReg b)
        {
            b.fim = true; Total("cegou inimigo");
            Match.Abate ab = null; foreach (var x in m.abates) if (x.v == b.cego && x.t >= b.t && x.t <= b.ate + 0.05f) ab = x;
            bool matou = ab != null && ab.by != null && ab.by.team == b.por.team;
            string r = matou ? "a matou o cegado" : b.peek ? "b abriu nele e não matou" : b.dAliado < 900 ? "c ninguém abriu (colega perto)" : "d ninguém abriu (ninguém perto)";
            m.Aud("humano: bang → peek → matou?", r);
            if (!matou && b.peek)
            {   /* abriu no cegado e não matou: por quê? (mirou nele? quanto tempo de cegueira sobrava? o que fazia) */
                float sobra = b.ate - 0.4f - b.peekT;
                m.Aud("humano: abriu no cegado e não matou — o abridor", (b.alvoT < 0 ? "nunca mirou nele" : $"mirou em {b.alvoT - b.peekT:0.0}s") + " · " + (sobra < 0.5f ? "cegueira acabando (<0,5 s)" : sobra < 1.5f ? "sobrava 0,5-1,5 s" : "sobrava 1,5+ s") + (b.quemPeek != null && !b.quemPeek.alive ? " · morreu" : ""));
                m.Aud("humano: abriu no cegado e não matou — fase do abridor", b.peekFase ?? "-");
                if (b.ticksVendo > 0)
                {
                    m.Aud("humano: peek no cego sem abate — com linha: tinha alvo / CanSee", $"alvo {(100 * b.ticksAlvo / b.ticksVendo) / 25 * 25}% · CanSee {(100 * b.ticksCanSee / b.ticksVendo) / 25 * 25}%");
                    int sem = b.ticksLargou + b.ticksNulo + b.ticksOutro;
                    if (sem > 0) m.Aud("humano: peek no cego sem abate — sem ele de alvo, por quê (maioria)", b.ticksLargou >= b.ticksNulo && b.ticksLargou >= b.ticksOutro ? "largou o duelo com ele (dueloCob)" : b.ticksOutro >= b.ticksNulo ? "mirando OUTRO inimigo" : "sem alvo nenhum");
                    m.Aud("humano: peek no cego sem abate — 1º tiro depois de ter linha", b.tiro1 < 0 ? "nunca atirou" : b.tiro1 - b.peekT < 0.3f ? "a <0,3 s" : b.tiro1 - b.peekT < 0.7f ? "b 0,3-0,7 s" : "c 0,7+ s");
                    if (b.porqueSemTiro != null) m.Aud("humano: peek no cego sem abate — com alvo e sem tiro (0,6 s+), por quê", b.porqueSemTiro);
                }
                int tiros = b.quemPeek != null ? b.quemPeek.shots - b.tiros0 : 0; float andou = (b.cego.pos - b.posCego0).Len;
                m.Aud("humano: abriu no cegado e não matou — tiros do abridor · o cego andou", (tiros == 0 ? "a nenhum tiro" : tiros <= 3 ? "b 1-3 tiros" : "c 4+ tiros") + " · " + (andou < 15 ? "cego parado" : andou < 60 ? "cego andou pouco" : "cego saiu (60+)"));
                m.Aud("humano: abriu no cegado e não matou — abridor meio cego? distância? tirou vida?", (b.peekMeio ? "meio cego" : "enxergando") + " · " + (b.peekDist < 300 ? "perto (<300)" : b.peekDist < 700 ? "médio" : "longe (700+)") + " · " + (b.cego.hp < b.hpCego0 ? "acertou" : "não acertou"));
            }
            if (!matou && !b.peek && b.dAliado < 900 && b.chega != null) m.Aud("humano: ninguém abriu — quem chegava antes num ponto que vê o cego · sabia?", b.chega);
            if (b.chega != null) m.Aud("humano: bang (todas) — quem chegava antes · sabia?", b.chega);
            if (!matou && !b.peek && b.dAliado < 900) m.Aud("humano: ninguém abriu — o mais perto fazia", (b.nome.StartsWith("bang pro") ? "bang pedido · " : b.nome.Contains("pra mim") ? "pop pra mim · " : b.nome.Contains("retake") ? "retake · " : "utilidade · ") + (b.faseMeio ?? "-"));
            if (!matou && b.peek) Marca("bang: abriu no cegado e não matou", b.quemPeek, b.cego.pos, $"{b.nome}: cegou {b.cego.name}; {b.quemPeek?.name} teve linha");
            bool pedeAbrir = b.nome.StartsWith("bang pro") || b.nome.Contains("pra mim") || b.nome.Contains("retake") || b.nome.Contains("antes de passar");
            if (!matou && !b.peek && b.dAliado < 900 && !pedeAbrir) m.Aud("humano: bang de utilidade sem ninguém abrir", "flash de utilidade (não pedia peek)");
            else if (!matou && b.dAliado < 900) Marca("bang: ninguém abriu no cegado", b.por, b.cego.pos, $"{b.nome}: {b.cego.name} cego {b.ate - b.t - 0.4f:0.0}s, colega a {b.dAliado:0}");
        }

        // ───────────────────────── 4. reação à smoke inimiga ─────────────────────────
        sealed class OlhoReg { public Agent a; public SmokeVolume s; public float aim, t; public V2 pos; public bool fim; }
        readonly List<OlhoReg> olhos = new List<OlhoReg>();
        readonly HashSet<SmokeVolume> smokeVista = new HashSet<SmokeVolume>();
        void OlhosTick()
        {
            /* a smoke fechou (2,5 s): quem olhava pela área dela (a linha da mira passa na fumaça antes da parede) */
            foreach (var s in m.smokes)
            {
                if (s.age < 2.5f || smokeVista.Contains(s)) continue;
                smokeVista.Add(s);
                if (!m.smokeDe.TryGetValue(s, out var dono)) continue;
                V2 o = new V2(s.origin.x, s.origin.y);
                foreach (var a in m.agents)
                {
                    if (!a.alive || a.team == dono || a.Blind(m.t)) continue;
                    float d = (o - a.pos).Len; if (d > 1500 || d < 60) continue;
                    V2 dir = V2.Ang(a.aim);
                    float alcance = MapData.Raio(a.pos, a.z, dir, 1500, a.Eye);
                    float ao = (o - a.pos).Dot(dir); if (ao < 0 || ao > alcance + 40) continue;
                    if (!Tapa(s, a.pos, a.z + a.Eye, a.pos + dir * MathF.Min(alcance, ao + 120), a.z + a.Eye)) continue;
                    olhos.Add(new OlhoReg { a = a, s = s, aim = a.aim, t = m.t, pos = a.pos });
                }
            }
            foreach (var r in olhos)
            {
                if (r.fim || m.t < r.t + 2f) continue;
                r.fim = true;
                if (!r.a.alive) continue;
                Total("olhava pela smoke inimiga");
                V2 dir2 = V2.Ang(r.a.aim); float alc2 = MapData.Raio(r.a.pos, r.a.z, dir2, 1500, r.a.Eye);
                bool aindaNela = m.smokes.Contains(r.s) && Tapa(r.s, r.a.pos, r.a.z + r.a.Eye, r.a.pos + dir2 * alc2, r.a.z + r.a.Eye);
                bool segura = (r.a.brain?.fase ?? "").Contains("smoke");
                bool virou = !aindaNela || MathF.Abs(MathX.AngDiff(r.a.aim, r.aim)) > 0.45f, saiu = (r.a.pos - r.pos).Len > 60, briga = r.a.brain?.target != null;
                m.Aud("humano: smoke inimiga fechou a linha que ele olhava", virou ? "a virou a mira" : saiu ? "b saiu do lugar" : briga ? "c em combate" : segura ? "d segurando a smoke de propósito" : "e continuou olhando a smoke");
                if (!virou && !saiu && !briga && !segura) Marca("smoke inimiga: não reagiu em 2 s", r.a, r.a.pos, $"seguiu olhando a smoke em {Places.Region(new V2(r.s.origin.x, r.s.origin.y))}");
            }
        }

        static string Fase(string f) { if (string.IsNullOrEmpty(f)) return "-"; int i = f.IndexOfAny(new[] { ' ', '(' }); return i > 0 && f.Length > 18 ? f.Substring(0, Math.Min(f.Length, 18)) : f; }
        float prox;
        public void Tick()
        {
            if (m.t < prox) return;
            prox = m.t + 0.25f;
            SmokesTick(); BangsTick(); OlhosTick();
        }
        /// <summary>Fim do round: fecha o que ficou aberto (smoke viva, bang em andamento).</summary>
        public void Fim()
        {
            foreach (var r in smokes) if (!r.fim) FechaSmoke(r);
            foreach (var b in bangs) if (!b.fim) FechaBang(b);
        }
    }
}
