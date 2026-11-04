using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSM.Sim;
using CSM.Map;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// A partida ao vivo no visual de maquete: a MESMA simulação da PartidaAoVivo (2D), desenhada em 3D — bonecos do
    /// Blender (Boneco3D) com a escolha de animação do TelaJogo, smoke em bolotas (as mesmas do SmokeVolume), fogo do
    /// molotov, rastro de tiro, granada voando, armas no chão e C4. Câmera segue a ação como no 2D (TelaJogo.Mirar).
    /// Teclas: espaço pausa, 1-4 velocidade, N pula o round, P painel, Esc câmera automática, roda do mouse = zoom.
    /// </summary>
    public class PartidaAoVivo3D : MonoBehaviour
    {
        public int seed = 15;
        public float velocidade = 1;

        Sim.Partida pt; Match m; int rn;
        Cenario3D cen;
        readonly Dictionary<int, Boneco3D> bonecos = new Dictionary<int, Boneco3D>();
        readonly Dictionary<int, TelaJogo.Anim> est = new Dictionary<int, TelaJogo.Anim>();
        readonly Dictionary<int, float> rumo = new Dictionary<int, float>();
        V2 cam, foco; float focoAte = -9, relogio, fimResta;
        bool pausado, painel, carregando = true, acabou;
        public int seguir = -1;
        public float zoom = 8.4f;   // um pouco mais aberto (usuário: as trocas ficavam fora do quadro)
        Pool fumaca, fumacaCasca, fogo, rastro, coisas;
        Material matFumaca, matFogo, matRastro, matCoisa, matC4;
        GUIStyle sTxt, sCaixa, sBotao;

        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            yield return null; yield return null;
            Iniciar();
        }

        static void Apagar(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>Monta a maquete e a partida (o Start chama; o editor chama direto pra tirar print sem Play).</summary>
        public void Iniciar()
        {
            Brain.Explicar = true;
            cen = new GameObject("Cenário 3D").AddComponent<Cenario3D>();
            cen.cam = Camera.main;
            cen.Montar();
            cen.enquadrar = zoom;
            Boneco3D.GarantirMateriais();
            matFumaca = new Material(Shader.Find("CSM/Maquete")) { name = "fumaça" };
            matFumaca.SetFloat("_UsarVertice", 0); matFumaca.SetFloat("_Smoothness", 0.05f); matFumaca.SetFloat("_Borda", 0.5f);
            matFumaca.SetColor("_BaseColor", new Color(0.92f, 0.92f, 0.95f));
            matFogo = new Material(Shader.Find("CSM/Brilho")) { name = "fogo" }; matFogo.SetColor("_BaseColor", new Color(1f, 0.45f, 0.12f)); matFogo.SetFloat("_Intensidade", 3f);
            matRastro = new Material(Shader.Find("CSM/Brilho")) { name = "rastro" }; matRastro.SetColor("_BaseColor", new Color(1f, 0.9f, 0.55f)); matRastro.SetFloat("_Intensidade", 4f);
            matCoisa = new Material(Shader.Find("CSM/Maquete")) { name = "coisa" }; matCoisa.SetFloat("_UsarVertice", 0); matCoisa.SetColor("_BaseColor", new Color(0.22f, 0.23f, 0.25f));
            matC4 = new Material(Shader.Find("CSM/Maquete")) { name = "c4" }; matC4.SetFloat("_UsarVertice", 0); matC4.SetColor("_BaseColor", new Color(0.36f, 0.42f, 0.24f));
            var esf = Malha(PrimitiveType.Sphere); var cubo = Malha(PrimitiveType.Cube);
            fumaca = new Pool("Fumaça", esf, matFumaca, transform, true);
            var matCasca = new Material(Shader.Find("CSM/Nuvem")) { name = "fumaça casca" };
            fumacaCasca = new Pool("Fumaça casca", esf, matCasca, transform, false);
            fogo = new Pool("Fogo", esf, matFogo, transform, false);
            rastro = new Pool("Rastro", cubo, matRastro, transform, false);
            coisas = new Pool("Coisas", cubo, matCoisa, transform, true);
            fx = new Efeitos3D(transform, esf, cubo);
            float W = MapData.NX * MalhaMapa.ESC, D = MapData.NY * MalhaMapa.ESC;
            var mini = Hud3D.Minimapa(new Vector3(W / 2, 0, -D / 2), D / 2, W / D);
            hud = new Hud3D(transform, cen.cam, mini);
            hud.modeloArmas = Boneco3D.Carregar("ct");
            hud.Cabeca = ag => bonecos.TryGetValue(ag.id, out var bb) ? bb.transform.position + Vector3.up * (ag.crouch ? 0.95f : 1.2f) : Vector3.zero;
            NovaPartida();
            carregando = false;
        }

        Hud3D hud;
        public Hud3D Hud => hud;
        float hudDt;   // tempo acumulado desde o último quadro do HUD (Simular avança vários passos por quadro)

        /// <summary>Avança a simulação e o desenho em passos de dt (print/teste fora do Play). Mesmo caminho do Play:
        /// fim de round, replay e round seguinte acontecem aqui também.</summary>
        public void Simular(float segundos, float dt = 1 / 30f)
        {
            for (float t = 0; t < segundos; t += dt)
            {
                Passo(dt, true, false);
                hudDt += dt;
                if (segundos <= dt * 1.5f) { hud.Atualizar(pt, m, rn, seguir, hudDt); hudDt = 0; }
                if (acabou) break;
            }
            if (hudDt > 0) { hud.Atualizar(pt, m, rn, seguir, Mathf.Min(hudDt, 0.5f)); hudDt = 0; }
            Efeitos();
            hud.ForcarLayout();
        }

        public bool EmReplay => replay != null;
        public int Round => rn;

        /// <summary>Um quadro: simulação (ou fim de round/replay), câmera, bonecos, partículas.</summary>
        void Passo(float dt, bool anda, bool limitarTempo)
        {
            if (replay != null) { if (anda) ReplayPasso(dt); return; }
            if (anda)
            {
                if (m.over || m.t > 200)
                {
                    // fim de round: o jogo segue POS_ROUND segundos (como no CS: quem perdeu salva a arma, quem ganhou caça
                    // o save; ninguém comemora parado), depois o replay do lance que decidiu e o próximo round
                    if (fimT == 0) tFim = m.t;
                    fimT += dt;
                    if (m.over)
                    {
                        relogio = Mathf.Min(relogio + dt * velocidade, m.t + 0.5f);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while (m.t < relogio && (!limitarTempo || sw.ElapsedMilliseconds < 25)) m.StepPosRound();
                    }
                    else m.t += dt;
                    if (!replayTentado && fimT > Match.POS_ROUND) { replayTentado = true; IniciarReplay(); if (replay != null) return; }
                    if (fimT > Match.POS_ROUND + 0.3f) { FechaRound(); return; }
                }
                else
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    relogio = Mathf.Min(relogio + dt * velocidade, m.t + 0.5f);
                    while (m.t < relogio && !m.over && (!limitarTempo || sw.ElapsedMilliseconds < 25)) m.Step();
                }
            }
            var alvo = seguir >= 0 ? m.agents[seguir] : null;
            if (alvo != null) { foco = alvo.pos; cam = cam + (foco - cam) * (1 - Mathf.Exp(-5f * dt)); }
            else TelaJogo.Mirar(m, ref cam, ref foco, ref focoAte, dt);
            MoverCamera(dt);
            Bonecos(dt);
            Gravar();
            if (anda) { Detectar(); Eventos(dt); }
        }

        // ------------------------------------------------------------------ replay do último abate
        // cada quadro ao vivo guarda o que foi desenhado (pose, posição, giro); no fim do round o lance que decidiu
        // (o último abate) volta em câmera lenta, com a câmera fechada em quem matou e quem morreu
        struct Pose1 { public string anim, pernaAnim; public float u, pernaU, yawPernas, pitch; public string armaPitch; public bool semArma, vivo; public Vector3 p; public Quaternion rot; }
        class Quadro { public float t; public Pose1[] b; }
        class TiroRec { public float t; public Vector3 boca, dir, ate; public bool pistola, awp, temBoca, hit, hs, prot; public int quem = -1; }
        readonly List<Quadro> gravado = new List<Quadro>();
        readonly List<TiroRec> tirosGravados = new List<TiroRec>();
        class Replay { public float de, ate, t; public int matou = -1, morreu = -1; public int prox; }
        Replay replay; bool replayTentado; float fimT, tFim;
        const float REPLAY_VEL = 0.42f;

        void Gravar()
        {
            var q = new Quadro { t = m.t, b = new Pose1[m.agents.Count] };
            for (int i = 0; i < m.agents.Count; i++)
            {
                var a = m.agents[i]; var b = bonecos[a.id];
                q.b[i] = new Pose1 { anim = b.animAtual, u = b.uAtual, pernaAnim = b.pernaAnim, pernaU = b.pernaU, yawPernas = b.yawPernas, pitch = b.pitch,
                                     armaPitch = b.armaPitch, semArma = b.semArma, vivo = a.alive, p = b.transform.position, rot = b.transform.rotation };
            }
            gravado.Add(q);
            while (gravado.Count > 0 && gravado[0].t < m.t - 16) gravado.RemoveAt(0);
            while (tirosGravados.Count > 0 && tirosGravados[0].t < m.t - 16) tirosGravados.RemoveAt(0);
        }

        void IniciarReplay()
        {
            // o último abate do round (quem matou e quem morreu, pelo nome no killfeed)
            for (int i = m.feed.Count - 1; i >= 0; i--)
            {
                var (t, s) = m.feed[i];
                if (fimT > 0 && t > tFim + 0.05f) continue;   // o lance que decidiu (não o abate do pós-round)
                int a0 = s.IndexOf(" ["), a1 = s.IndexOf("] ");
                if (a0 <= 0 || a1 <= a0) continue;
                if (t < m.t - 13 || gravado.Count < 30) return;
                string k = s.Substring(0, a0), v = s.Substring(a1 + 2).Replace(" (HS)", "");
                var r = new Replay { de = Mathf.Max(gravado[0].t, t - 2.3f), ate = t + 0.9f };
                r.t = r.de;
                r.matou = m.agents.FindIndex(x => x.name == k); r.morreu = m.agents.FindIndex(x => x.name == v);
                r.prox = tirosGravados.FindIndex(x => x.t >= r.de); if (r.prox < 0) r.prox = tirosGravados.Count;
                replay = r;
                string arma = s.Substring(a0 + 2, a1 - a0 - 2).ToLower();
                hud.Replay(true, $"{k}  <size=22>com a</size>  {Hud3D.NomeArma(arma)}{(s.EndsWith("(HS)") ? "  <size=22>na cabeça</size>" : "")}");
                return;
            }
        }

        void ReplayPasso(float dt)
        {
            var r = replay;
            r.t += dt * REPLAY_VEL;
            if (r.t >= r.ate) { replay = null; hud.Replay(false); FechaRound(); return; }
            // quadros em volta do tempo do replay
            int i1 = gravado.FindIndex(q => q.t >= r.t); if (i1 < 0) i1 = gravado.Count - 1; int i0 = Mathf.Max(0, i1 - 1);
            var q0 = gravado[i0]; var q1 = gravado[i1];
            float w = q1.t > q0.t ? Mathf.Clamp01((r.t - q0.t) / (q1.t - q0.t)) : 1;
            for (int i = 0; i < m.agents.Count; i++)
            {
                var b = bonecos[m.agents[i].id]; var p0 = q0.b[i]; var p1 = q1.b[i]; var pp = w < 0.5f ? p0 : p1;
                b.transform.position = Vector3.Lerp(p0.p, p1.p, w);
                b.transform.rotation = Quaternion.Slerp(p0.rot, p1.rot, w);   // rotação inteira (a inclinação da escada vem junto)
                b.pernaAnim = pp.pernaAnim; b.pernaU = p0.pernaAnim == p1.pernaAnim ? Mathf.LerpUnclamped(p0.pernaU, p1.pernaU, w) : pp.pernaU;
                b.yawPernas = Mathf.LerpAngle(p0.yawPernas, p1.yawPernas, w); b.pitch = Mathf.Lerp(p0.pitch, p1.pitch, w); b.armaPitch = pp.armaPitch;
                // a vítima do lance mantém a silhueta no replay (cair atrás de parede ainda aparece)
                b.semArma = pp.semArma; b.Silhueta(pp.vivo || i == r.morreu); Costas(m.agents[i].id, b, null);   // (costas só ao vivo)
                if (!pp.vivo && ragdolls.TryGetValue(m.agents[i].id, out var rg)) rg.Aplicar(r.t - m.agents[i].diedAt);
                else if (pp.anim != null) b.Pose(pp.anim, p0.anim == p1.anim ? Mathf.LerpUnclamped(p0.u, p1.u, w) : pp.u, dt * REPLAY_VEL);
            }
            // tiros do lance de novo (clarão, cápsula, impacto) e partículas em câmera lenta
            while (r.prox < tirosGravados.Count && tirosGravados[r.prox].t <= r.t)
            {
                var s = tirosGravados[r.prox++];
                if (s.temBoca) fx.Tiro(s.boca, s.dir, Vector3.Cross(Vector3.up, s.dir), s.pistola, s.awp);
                if (s.quem >= 0 && bonecos.TryGetValue(s.quem, out var bq)) bq.Disparo(s.awp ? 1f : s.pistola ? 0.7f : 0.5f);
                if (s.temBoca) fx.Rastro(s.boca, s.ate);
                fx.Impacto(s.ate, s.ate - s.boca, s.hit, s.hs, s.prot);
            }
            fx.Atualizar(dt * REPLAY_VEL); fx.FimLasers();   // (laser só ao vivo)
            foreach (var ac in armasChao.Values) ac.go.SetActive(ac.nasceu <= r.t);
            // câmera: entre quem matou e quem morreu, mais perto
            Vector3 a = r.matou >= 0 ? bonecos[m.agents[r.matou].id].transform.position : Vector3.zero;
            Vector3 v = r.morreu >= 0 ? bonecos[m.agents[r.morreu].id].transform.position : a;
            if (r.matou < 0) a = v;
            // mais perto da vítima (a queda é o lance) e abrindo o zoom pra caber os dois com folga da faixa de cinema
            var meio = Vector3.Lerp(a, v, 0.62f);
            var alvo = new Vector2(meio.x / MalhaMapa.ESC, -meio.z / MalhaMapa.ESC);
            float dist = (a - v).magnitude;
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, 0.35f, 600f, dt);
            zoomSuave = Mathf.SmoothDamp(zoomSuave, Mathf.Clamp(dist * 1.7f + 2.8f, 3.4f, zoom * 1.1f), ref zoomVel, 0.5f, 100f, dt);
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar();
            AplicarTremor(dt * REPLAY_VEL);
        }

        Efeitos3D fx;
        readonly HashSet<Match.ShotFx> tirosVistos = new HashSet<Match.ShotFx>();
        readonly HashSet<FireArea> fogosVistos = new HashSet<FireArea>();
        readonly Dictionary<FireArea, float> idadeFogo = new Dictionary<FireArea, float>(); int vaporesVistos;
        float ultHE = -1, ultFlash = -1; bool c4Explodiu;
        readonly Dictionary<int, int> passoVisto = new Dictionary<int, int>();

        /// <summary>O que aconteceu na simulação desde o último quadro vira partícula: tiro, impacto, HE, flash, molotov.</summary>
        void Eventos(float dt)
        {
            foreach (var s in m.shots)
            {
                if (!tirosVistos.Add(s)) continue;
                var de = MalhaMapa.ParaUnity(s.a.x, s.a.y, s.za); var ate = MalhaMapa.ParaUnity(s.b.x, s.b.y, s.zb);
                // quem atirou: o vivo do time mais perto da origem do tiro
                Agent quem = null; float melhor = 30;
                foreach (var a in m.agents) { float d = (a.pos - s.a).Len; if (a.team == s.team && d < melhor) { melhor = d; quem = a; } }
                var rec = new TiroRec { t = s.t, ate = ate, boca = de, hit = s.hit, hs = s.hs };
                if (quem != null && bonecos.TryGetValue(quem.id, out var b))
                {
                    var tr = b.transform;
                    var dir = (ate - de); dir.y = 0; dir = dir.sqrMagnitude > 1e-6f ? dir.normalized : tr.forward;
                    var boca = tr.position + dir * (quem.gunId == "awp" ? 0.62f : WeaponStats.Pistola(quem.gunId) ? 0.36f : 0.5f) + Vector3.up * (quem.crouch ? 0.36f : 0.5f);
                    // o fuzil fica do lado direito do colete (SMG um pouco, pistola no meio); a AWP de luneta vai no olho
                    float lado = quem.gunId == "awp" && quem.scoped ? 0.06f : WeaponStats.Pistola(quem.gunId) ? 0.02f : quem.gunId == "mp9" || quem.gunId == "mac10" ? 0.065f : 0.17f;
                    boca += Vector3.Cross(Vector3.up, dir) * lado;
                    fx.Tiro(boca, dir, Vector3.Cross(Vector3.up, dir), WeaponStats.Pistola(quem.gunId), quem.gunId == "awp");
                    rec.temBoca = true; rec.boca = boca; rec.dir = dir; rec.pistola = WeaponStats.Pistola(quem.gunId); rec.awp = quem.gunId == "awp";
                    rec.quem = quem.id; b.Disparo(rec.awp ? 1f : rec.pistola ? 0.7f : 0.5f);   // o coice passa pro corpo
                }
                // quem levou: capacete (HS) / colete (tronco) abafam o sangue
                if (s.hit)
                {
                    Agent vit = null; float mv = 24;
                    foreach (var a in m.agents) { float d = (a.pos - s.b).Len; if (a.team != s.team && d < mv) { mv = d; vit = a; } }
                    rec.prot = vit != null && (s.hs ? vit.helmet : vit.armor > 0);
                }
                if (rec.temBoca) fx.Rastro(rec.boca, ate);
                fx.Impacto(ate, ate - de, s.hit, s.hs, rec.prot);
                tirosGravados.Add(rec);
                var a3 = new V3(s.a.x, s.a.y, s.za); var b3 = new V3(s.b.x, s.b.y, s.zb);
                foreach (var f in fumacas.Values) if (f.Cruza(a3, b3)) f.Furo(a3, b3, m.t);
                if (rec.awp) Tremer(s.a, 0.22f);
            }
            foreach (var h in m.hes) if (h.t > ultHE)
            {
                ultHE = h.t; var hp = MalhaMapa.ParaUnity(h.p.x, h.p.y, h.z);
                fx.HE(hp); Tremer(h.p, 0.9f); Empurrar(hp, 2.4f, 6.5f);
            }
            if (m.exploded && !c4Explodiu)
            {
                c4Explodiu = true;
                var bp = MalhaMapa.ParaUnity(m.bombPos.x, m.bombPos.y, m.bombZ);
                fx.ExplosaoC4(bp); Tremer(m.bombPos, 2.5f); Empurrar(bp, 9f, 13f);
                if (c4 != null) c4.SetActive(false);
            }
            foreach (var f in m.flashes) if (f.t > ultFlash) { ultFlash = f.t; fx.Flash(MalhaMapa.ParaUnity(f.p.x, f.p.y, f.z)); Tremer(f.p, 0.3f); }
            foreach (var f in m.fires) if (fogosVistos.Add(f)) fx.Molotov(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z));
            // smoke apagou o fogo / molotov caiu na smoke e nem pegou (Match.fogoApagado): chiado de vapor
            if (m.fogoApagado.Count < vaporesVistos) vaporesVistos = 0;
            for (; vaporesVistos < m.fogoApagado.Count; vaporesVistos++)
            {
                var (vp, vz, _) = m.fogoApagado[vaporesVistos];
                fx.Vapor(MalhaMapa.ParaUnity(vp.x, vp.y, vz), 1.1f);
            }
            // poeirinha a cada passo correndo (o passo vira inteiro a cada meio ciclo)
            foreach (var a in m.agents)
            {
                if (!a.alive || a.Speed < a.MaxSpeed * 0.62f || a.noAr || !est.TryGetValue(a.id, out var e)) continue;
                int q = Mathf.FloorToInt(e.passo * 2);
                if (passoVisto.TryGetValue(a.id, out int q0) && q != q0 && bonecos.TryGetValue(a.id, out var b))
                {
                    var tr = b.transform; float lado = (q & 1) == 0 ? 0.07f : -0.07f;
                    fx.Passo(tr.position + tr.right * lado, new Vector3(a.vel.x, 0, -a.vel.y) * MalhaMapa.ESC);
                }
                passoVisto[a.id] = q;
            }
            fx.Fogos(m.fires, dt);
            fx.Atualizar(dt);
            Quedas(dt);
        }

        // diretor da transmissão: segue o foco do TelaJogo.Mirar (duelo > C4 > grupo do TR) com mola (acelera e freia);
        // quando o foco pula pra longe (outro duelo do outro lado do mapa) é corte seco com piscada preta, como um
        // diretor trocando de câmera, em vez de varrer o mapa. Zoom fecha no duelo e abre fora dele.
        Vector2 camSuave, camVel; bool camPronta; float zoomSuave, zoomVel, ultCorte = -9;
        const float CORTE_DIST = 420f;
        void MoverCamera(float dt)
        {
            var alvo = new Vector2(foco.x, foco.y);   // o foco cru (o cam do Mirar já vem amortecido e nunca "pula")
            // troca de tiro perto do foco: enquadra OS DOIS LADOS (quem atira, o alvo e quem está no duelo junto) — o
            // centro vai pro meio deles e o zoom abre até caber todo mundo com folga (antes um dos lados ficava fora)
            float zDuelo = 0;
            if (seguir < 0)
            {
                float x0 = 1e9f, x1 = -1e9f, y0 = 1e9f, y1 = -1e9f; int n = 0;
                void Inclui(V2 p) { x0 = Mathf.Min(x0, p.x); x1 = Mathf.Max(x1, p.x); y0 = Mathf.Min(y0, p.y); y1 = Mathf.Max(y1, p.y); n++; }
                foreach (var a in m.agents)
                {
                    if (!a.alive || a.brain.target == null || !a.brain.target.alive) continue;
                    if ((a.pos - foco).Len > 520 && (a.brain.target.pos - foco).Len > 520) continue;
                    Inclui(a.pos); Inclui(a.brain.target.pos);
                }
                if (n >= 2)
                {
                    var meio = new Vector2((x0 + x1) / 2, (y0 + y1) / 2);
                    alvo = Vector2.Lerp(alvo, meio, 0.75f);
                    // altura do quadro (unidades Unity) pra caber: largura conta 16:9, a profundidade encolhe na isométrica
                    float precisa = Mathf.Max((x1 - x0 + (y1 - y0) * 0.5f) / 1.55f, (y1 - y0 + (x1 - x0) * 0.4f) * 0.9f) * MalhaMapa.ESC;
                    zDuelo = Mathf.Clamp(precisa * 1.35f + 2.6f, zoom * 0.8f, zoom * 2.1f);
                }
            }
            if (!camPronta) { camSuave = alvo; zoomSuave = zoom; camPronta = true; }
            if ((alvo - camSuave).magnitude > CORTE_DIST && m.t - ultCorte > 2.5f)
            {
                camSuave = alvo; camVel = Vector2.zero; ultCorte = m.t; hud?.Corte();
            }
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, 0.55f, 420f, dt);
            bool duelo = m.agents.Any(a => a.alive && a.brain.target != null);
            float z = seguir >= 0 ? zoom * 0.85f : zDuelo > 0 ? zDuelo : duelo ? zoom * 0.95f : zoom * 1.12f;
            zoomSuave = Mathf.SmoothDamp(zoomSuave, z, ref zoomVel, 0.9f, 100f, dt);
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar();
            AplicarTremor(dt);
        }

        // tremida de câmera: explosão perto do que a câmera mostra chacoalha forte, flash e AWP de leve
        float tremor, tremorT;
        void Tremer(V2 onde, float forca)
        {
            float d = (new Vector2(onde.x, onde.y) - camSuave).magnitude;
            tremor = Mathf.Max(tremor, forca * Mathf.Clamp01(1.2f - d / 700f));
        }
        void AplicarTremor(float dt)
        {
            tremorT += dt; tremor *= Mathf.Exp(-5.5f * dt);
            if (tremor < 0.002f) return;
            var o = new Vector3(Mathf.PerlinNoise(tremorT * 28, 1.3f) - 0.5f, Mathf.PerlinNoise(4.1f, tremorT * 28) - 0.5f, Mathf.PerlinNoise(tremorT * 24, 9.7f) - 0.5f);
            cen.cam.transform.position += o * tremor * 0.5f;
        }

        public Match Partida => m;
        public Cenario3D Cenario => cen;

        static Mesh Malha(PrimitiveType t) { var g = GameObject.CreatePrimitive(t); var me = g.GetComponent<MeshFilter>().sharedMesh; Apagar(g); return me; }

        // Vindo do manager (CSM.Manager), a partida já vem montada com o elenco dele; sozinha, a cena usa o time padrão.
        void NovaPartida() { pt = CSM.Manager.Carreira.TomarPendente() ?? new Sim.Partida(seed, null, null); acabou = false; NovoRound(); }

        void NovoRound()
        {
            m = pt.Preparar(); rn = pt.r + 1;
            est.Clear(); rumo.Clear(); sacando.Clear(); mortes.Clear(); ultHp.Clear(); luneta.Clear(); escadaVista.Clear(); antes.Clear(); evento.Clear(); callsVistas = 0; iglCallsVistas[0] = iglCallsVistas[1] = 0; gravado.Clear(); tirosGravados.Clear();
            replay = null; replayTentado = false; fimT = 0; hud?.Replay(false); c4Explodiu = false;
            foreach (var ac in armasChao.Values) Apagar(ac.go);
            armasChao.Clear();
            foreach (var g in props.Values) Apagar(g);
            foreach (var f in fumacas.Values) f.Apagar();
            fumacas.Clear(); idadeFogo.Clear(); vaporesVistos = 0; ragdolls.Clear();
            props.Clear(); if (c4 != null) c4.SetActive(false); tirosVistos.Clear(); fogosVistos.Clear(); passoVisto.Clear(); ultHE = ultFlash = -1;
            foreach (var b in bonecos.Values) if (b) Apagar(b.gameObject);
            bonecos.Clear();
            foreach (var a in m.agents)
                bonecos[a.id] = Boneco3D.Criar(a.IsT ? "tr" : "ct", transform);
            cam = foco = m.agents.Where(a => a.IsT).Aggregate(new V2(0, 0), (s, a) => s + a.pos) / 5;
            focoAte = -9; relogio = 0; fimResta = 3; camPronta = false;
        }

        void FechaRound() { pt.Fechar(m); if (pt.acabou) { acabou = true; pausado = true; } else NovoRound(); }

        /// <summary>Pula direto pro round n (simula os anteriores sem desenhar).</summary>
        public void PularAte(int n) { while (rn < n && !acabou) PulaRound(); }

        void PulaRound()
        {
            if (m == null || acabou) return;
            while (!m.over && m.t < 200) m.Step();
            FechaRound();
        }

        void Update()
        {
            if (carregando) return;
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            Teclado();
            Passo(dt, !pausado && !acabou, true);
            Efeitos();
            hud.Atualizar(pt, m, rn, seguir, dt);
        }

        static string Arma(Agent a) => a.gunId switch
        {
            "ak" => "AK47", "m4" => "M4A4", "m4s" => "M4A1S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC10",
            "usp" => "USP", "glock" => "Glock", "deagle" => "Deagle", _ => WeaponStats.Pistola(a.gunId) ? (a.IsT ? "Glock" : "USP") : TelaJogo.Padrao(!a.IsT)
        };

        // corpo andado por ciclo de passo (unidades do jogo) das passadas do exportar3d.py: 2 x passada / fração no chão x 25
        const float CICLO_DEVAGAR = 2 * 0.12f / 0.5f * 25, CICLO_AGACHADO = 2 * 0.1f / 0.5f * 25;

        readonly Dictionary<int, (float ini, float ate)> sacando = new Dictionary<int, (float, float)>();
        readonly Dictionary<int, int> ultHp = new Dictionary<int, int>();
        readonly Dictionary<int, float> luneta = new Dictionary<int, float>();
        readonly HashSet<int> escadaVista = new HashSet<int>();
        class Antes { public string gun; public int nades, troca; public bool bomba, noAr, vivo = true; public float quedaDe; }
        readonly Dictionary<int, Antes> antes = new Dictionary<int, Antes>();
        readonly Dictionary<int, (string anim, float ini, float dur)> evento = new Dictionary<int, (string, float, float)>();
        readonly Dictionary<int, float> trocouEm = new Dictionary<int, float>(), costasEm = new Dictionary<int, float>();

        readonly Dictionary<int, (string arma, GameObject go, Vector3 lp, Quaternion lr)> costas = new Dictionary<int, (string, GameObject, Vector3, Quaternion)>();
        /// <summary>Primária pendurada nas costas presa no tronco (null = esconde).</summary>
        void Costas(int id, Boneco3D b, string gunId)
        {
            string nome = gunId != null ? Armas3D.DoJogo(gunId) : null;
            costas.TryGetValue(id, out var c);
            if (nome == null) { if (c.go != null) c.go.SetActive(false); return; }
            if (c.go == null || c.arma != nome)
            {
                if (c.go != null) Apagar(c.go);
                var go = Armas3D.NasCostas(b.modelo, nome, Boneco3D.matCorpo, b.gameObject.layer, out var lp, out var lr);
                go.transform.SetParent(transform, false); c = (nome, go, lp, lr); costas[id] = c;
            }
            var tr = b.Peca("tronco"); if (tr == null) { c.go.SetActive(false); return; }
            c.go.SetActive(true);
            c.go.transform.SetPositionAndRotation(tr.position + tr.rotation * (c.lp * Boneco3D.ESCALA), tr.rotation * c.lr);
        }

        void Laser(Agent a, Boneco3D b)
        {
            V2 dir = V2.Ang(a.aim);
            float olho = a.z + NavGrid.EYE, len = MapData.Raio(a.pos, a.z, dir, 1600, NavGrid.EYE);
            if (m.SmokeBlocks(a.pos, olho, a.pos + dir * len, olho))
            {   // corta na borda da smoke (bisseção)
                float lo = 0, hi = len;
                for (int i = 0; i < 7; i++) { float mid = (lo + hi) * 0.5f; if (m.SmokeBlocks(a.pos, olho, a.pos + dir * mid, olho)) hi = mid; else lo = mid; }
                len = hi;
            }
            V2 f = a.pos + dir * len;
            float h = a.crouch ? 0.44f : 0.58f;
            var pe = b.transform.position; var fim = MalhaMapa.ParaUnity(f.x, f.y, a.z) + Vector3.up * h;
            var plano = fim - pe; plano.y = 0; plano = plano.sqrMagnitude > 1e-6f ? plano.normalized : b.transform.forward;
            fim.y += Mathf.Tan(b.pitch * Mathf.Deg2Rad) * (len * MalhaMapa.ESC);
            fx.Laser(a.id, pe + Vector3.up * h + plano * 0.62f, fim, a.Speed < 5 ? 1f : 0.35f);
        }

        /// <summary>Sem duelo: mira pra cima/baixo pro lugar que ele segura (alvoMira do Brain, quando é pra lá que ele
        /// olha: telhado, janela, 2º andar) ou, andando, pro chão uns 90 à frente (escada, rampa). Desnível pequeno = reto.</summary>
        float PitchMira(Agent a)
        {
            V2 olha = V2.Ang(a.aim);
            float Ang(V2 p, float l) { float dz = NavGrid.ZAt(p) - a.z; return Mathf.Abs(dz) < 5 ? 0 : Mathf.Clamp(Mathf.Atan2(dz, l) * Mathf.Rad2Deg, -60, 60); }
            if (a.brain.alvoMira is V2 am && NavGrid.Movable(am))
            {
                V2 d = am - a.pos; float l = d.Len;
                if (l > 30 && l < 900 && (d / l).Dot(olha) > 0.97f) return Ang(am, l);
            }
            V2 f = a.pos + olha * 90;
            if (NavGrid.Movable(f) && MapData.LosWalk(a.pos, a.z, f, NavGrid.ZAt(f), 18f)) return Ang(f, 90);
            return 0;
        }

        /// <summary>Indo pegar algo do chão: olha pra baixo pro item (como quem mira nele pro "E" do CS). Arma: ele encara
        /// e anda pra ela (o Brain faz Face no drop); granada pra ele e C4 (TR): anda por cima. Devolve o pitch (graus, ≤ 0).</summary>
        bool OlhaItem(Agent a, out float pitch)
        {
            V2 olha = V2.Ang(a.aim), anda = a.Speed > 5 ? a.vel / a.Speed : olha;
            float melhor = float.MaxValue, pv = 0;
            void Ver(V2 p, float z, bool encara)
            {
                V2 d = p - a.pos; float l = d.Len;
                if (l < 3 || l > 70 || l >= melhor || Mathf.Abs(z - a.z) > 10) return;
                V2 u = d / l;
                if (u.Dot(anda) < 0.8f && l > 14) return;               // não está indo pra lá
                if (encara && u.Dot(olha) < 0.9f) return;               // arma: tem que estar encarando (~25°)
                melhor = l; pv = Mathf.Atan2(z - (a.z + NavGrid.EYE), l) * Mathf.Rad2Deg;
            }
            foreach (var d in m.drops) Ver(d.pos, d.z, true);
            foreach (var it in m.itensChao)
                if (a.team == it.time && (it.para == a || m.t - it.t > 3 && a != it.de) && Match.CabeGranada(a, it.nade)) Ver(it.pos, it.z, false);
            if (m.bombDropped && !m.bombPlanted && a.IsT) Ver(m.droppedBomb, NavGrid.ZAt(m.droppedBomb), false);
            pitch = Mathf.Clamp(pv, -45, 0);
            return melhor < float.MaxValue;
        }
        int callsVistas; readonly int[] iglCallsVistas = new int[2];

        /// <summary>Eventos que viram animação curta: pegou arma/granada/C4 do chão ou largou o C4 (pegar), cantou no
        /// rádio (apontar), caiu de altura (aterrissar = agacha um instante).</summary>
        void Detectar()
        {
            foreach (var a in m.agents)
            {
                if (!antes.TryGetValue(a.id, out var o)) { antes[a.id] = new Antes { gun = a.gunId, nades = a.nades.Count, bomba = a.hasBomb, noAr = a.noAr, troca = a.trocouArma }; continue; }
                if (a.alive && m.t > 0.5f)
                {
                    bool devagar = a.Speed < a.MaxSpeed * 0.35f;
                    // trocou de arma no chão (o "E" do CS): a dele sai jogada pra frente e a nova vem sacando, olhando pra baixo
                    if (a.gunId != o.gun && a.trocouArma == o.troca)   // (trocou pelo Q = só sacar, não é pegar do chão)
                    {
                        evento[a.id] = ("trocar", m.t, 0.6f); trocouEm[a.id] = m.t;
                        Debug.Log($"[Visual3D] trocou arma no chão: {a.name} {o.gun} -> {a.gunId} round {Round} t={m.t:0.0}");
                    }
                    else if (devagar && (a.nades.Count > o.nades || (a.hasBomb && !o.bomba) || (!a.hasBomb && o.bomba && !m.bombPlanted)))
                        evento[a.id] = ("pegar", m.t, 0.7f);
                    if (o.noAr && !a.noAr && o.quedaDe - a.z > 6) evento[a.id] = ("aterrissar", m.t, 0.2f);
                }
                o.gun = a.gunId; o.troca = a.trocouArma; o.nades = a.nades.Count; o.bomba = a.hasBomb; if (a.noAr && !o.noAr) o.quedaDe = a.quedaDe; o.noAr = a.noAr;
            }
            // calls no rádio (cada jogador) e do IGL: aponta pra frente
            if (m.callsFeitas.Count < callsVistas) callsVistas = 0;
            for (; callsVistas < m.callsFeitas.Count; callsVistas++)
            {
                var (t, by) = m.callsFeitas[callsVistas];
                if (by != null && by.alive && !evento.ContainsKey(by.id)) evento[by.id] = ("apontar", m.t, 0.9f);
            }
            for (int tm = 0; tm < 2; tm++)
            {
                var ig = m.igl[tm]; if (ig == null) continue;
                if (ig.calls.Count < iglCallsVistas[tm]) iglCallsVistas[tm] = 0;
                for (; iglCallsVistas[tm] < ig.calls.Count; iglCallsVistas[tm]++)
                {
                    var lider = m.agents.FirstOrDefault(x => x.alive && x.role == Role.Igl && (int)x.team == tm);
                    if (lider != null && !evento.ContainsKey(lider.id)) evento[lider.id] = ("apontar", m.t, 0.9f);
                }
            }
            foreach (var id in evento.Keys.ToList()) if (m.t - evento[id].ini > evento[id].dur) evento.Remove(id);
        }

        /// <summary>Como cai: de costas pra quem atirou de frente; de cara no chão se o tiro veio pelas costas.</summary>
        (Vector3 dir, float yaw, float forca, bool frente) Morte(Agent a, Boneco3D b)
        {
            if (mortes.TryGetValue(a.id, out var mo)) return mo;
            V2 de = a.killedBy != null ? a.killedBy.pos : a.pos - new V2(Mathf.Cos(a.aim), Mathf.Sin(a.aim));
            var d = new Vector3(a.pos.x - de.x, 0, -(a.pos.y - de.y)); d = d.sqrMagnitude > 1e-6f ? d.normalized : -b.transform.forward;
            var olha = b.transform.forward; olha.y = 0;
            bool frente = Vector3.Angle(olha, -d) > 110;   // quem matou está atrás dele
            float forca = a.killedBy != null && a.killedBy.gunId == "awp" ? 0.45f : 0.28f;
            mo = (d, Quaternion.LookRotation(frente ? d : -d).eulerAngles.y, forca, frente);
            mortes[a.id] = mo;
            return mo;
        }

        /// <summary>Escada de mão do janelão: a animação foi feita pra escada reta na frente do boneco; a do mapa é
        /// inclinada (sobe 36 em 20 de chão, ~29° da vertical). Inclina o corpo junto (gira pelo pé) e encosta ele nos
        /// degraus (o pé da animação fica 0,1 à frente da origem).</summary>
        public static void NaEscada(Transform t)
        {
            var esc = MapData.areas.Find(s => s.name == "Esc_janelao");
            float w = esc != null ? esc.box.x1 - esc.box.x0 : 20, h = MapData.UP + 6 - MapData.GZ;
            float incl = Mathf.Atan2(w, h) * Mathf.Rad2Deg;
            t.rotation = Quaternion.Euler(0, 270, 0) * Quaternion.Euler(incl, 0, 0);
            t.position -= Quaternion.Euler(0, 270, 0) * Vector3.forward * (0.1f * Boneco3D.ESCALA);
        }
        readonly Dictionary<int, (Vector3 dir, float yaw, float forca, bool frente)> mortes = new Dictionary<int, (Vector3, float, float, bool)>();
        readonly Dictionary<int, Ragdoll3D> ragdolls = new Dictionary<int, Ragdoll3D>();

        // ------------------------------------------------------------------ itens no chão com física
        // arma solta, granada largada pro colega e C4 caído: nascem na mão de quem soltou e caem de verdade — gravidade,
        // giro, quique com perda, rolam e param pelo atrito, batem em parede (o piso do lado mais alto que o item = parede,
        // a velocidade daquele eixo volta), e no fim deitam de lado. Explosão (HE, C4) empurra pra longe e faz voar.
        class ArmaCaindo { public GameObject go; public Vector3 p, v, w; public Quaternion r; public bool parada; public float deitar, nasceu, meiaAltura = 0.035f; }
        readonly Dictionary<object, ArmaCaindo> armasChao = new Dictionary<object, ArmaCaindo>();
        const string C4_CAIDO = "c4 caído";

        ArmaCaindo NovoItem(GameObject go, Vector3 mao, Vector3 empurra, float yaw)
        {
            go.transform.SetParent(transform, false);
            return new ArmaCaindo
            {
                go = go, nasceu = m.t, p = mao,
                v = empurra + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(1.3f, 2.0f), UnityEngine.Random.Range(-0.4f, 0.4f)),
                r = Quaternion.Euler(0, yaw, 0),
                w = new Vector3(UnityEngine.Random.Range(-400, 400), UnityEngine.Random.Range(-250, 250), UnityEngine.Random.Range(200, 500))
            };
        }

        /// <summary>Mão de quem está perto do ponto (de onde o item sai) e pra onde ele empurra.</summary>
        (Vector3 mao, Vector3 empurra, float yaw) MaoDe(V2 pos, float z)
        {
            var dono = m.agents.Where(a => (a.pos - pos).Len < 40).OrderBy(a => (a.pos - pos).Len).FirstOrDefault();
            if (dono != null && bonecos.TryGetValue(dono.id, out var bd))
            {
                var e = mortes.TryGetValue(dono.id, out var mo) ? mo.dir * 1.1f : bd.transform.forward * 0.9f;
                return (bd.transform.position + Vector3.up * 0.45f, e, bd.transform.eulerAngles.y);
            }
            return (MalhaMapa.ParaUnity(pos.x, pos.y, z) + Vector3.up * 0.45f, Vector3.zero, UnityEngine.Random.Range(0, 360f));
        }

        /// <summary>Explosão: tudo que está solto no raio voa pra longe (mais perto = mais forte) girando.</summary>
        void Empurrar(Vector3 centro, float raio, float forca)
        {
            foreach (var ac in armasChao.Values)
            {
                var d = ac.p - centro; float dist = d.magnitude; if (dist > raio) continue;
                float k = 1 - dist / raio; d.y = 0; d = d.sqrMagnitude > 1e-4f ? d.normalized : Vector3.forward;
                ac.v += (d + Vector3.up * 0.9f) * forca * k * UnityEngine.Random.Range(0.8f, 1.2f);
                ac.w += new Vector3(UnityEngine.Random.Range(-900, 900), UnityEngine.Random.Range(-600, 600), UnityEngine.Random.Range(-900, 900)) * k;
                ac.parada = false; ac.deitar = 0;
            }
        }

        void Quedas(float dt)
        {
            foreach (var d in m.drops)
            {
                if (armasChao.ContainsKey(d)) continue;
                string nome = Armas3D.DoJogo(d.gun); if (nome == null) continue;
                var (mao, emp, yaw) = MaoDe(d.pos, d.z);
                var dono = m.agents.Where(a => (a.pos - d.pos).Len < 40).OrderBy(a => (a.pos - d.pos).Len).FirstOrDefault();
                // trocou no chão: a dele sai da mão pra frente num arco (o G do CS), não cai no pé
                bool jogou = dono != null && dono.alive && trocouEm.TryGetValue(dono.id, out float tt) && m.t - tt < 0.5f && bonecos.ContainsKey(dono.id);
                if (jogou) mao = bonecos[dono.id].transform.position + Vector3.up * 0.5f + bonecos[dono.id].transform.forward * 0.15f;
                var nova = NovoItem(Armas3D.Objeto(Boneco3D.Carregar(dono != null && !dono.IsT ? "ct" : "tr"), nome, Boneco3D.matCorpo), mao, emp, yaw);
                if (jogou)
                {   // arco até onde o Sim pôs (uns 20 à frente): o 1º baque a 3/4 do caminho, o resto ela escorrega
                    var alvo = MalhaMapa.ParaUnity(d.pos.x, d.pos.y, d.z);
                    float vy = 1.6f, h = Mathf.Max(0.05f, mao.y - alvo.y), tq = (vy + Mathf.Sqrt(vy * vy + 24f * h)) / 12f;
                    var hor = alvo - mao; hor.y = 0;
                    nova.v = hor * (0.75f / tq) + Vector3.up * vy;
                }
                armasChao[d] = nova;
            }
            foreach (var it in m.itensChao)
            {
                if (armasChao.ContainsKey(it)) continue;
                var (mao, emp, yaw) = MaoDe(it.pos, it.z);
                var ac = NovoItem(Granada(it.nade, it.time == Team.T), mao, emp * 0.6f, yaw); ac.meiaAltura = 0.03f;
                armasChao[it] = ac;
            }
            if (m.bombDropped && !m.bombPlanted && !armasChao.ContainsKey(C4_CAIDO))
            {
                var (mao, emp, yaw) = MaoDe(m.droppedBomb, NavGrid.ZAt(m.droppedBomb));
                var ac = NovoItem(Armas3D.Prop(Boneco3D.Carregar("tr"), "plantar", 0.97f, Boneco3D.matCorpo), mao, emp * 0.6f, yaw); ac.meiaAltura = 0.045f;
                armasChao[C4_CAIDO] = ac;
            }
            // pegaram do chão: some
            foreach (var k in armasChao.Keys.ToList())
            {
                bool existe = labItens.Contains(k) || (k is Match.Drop dr ? m.drops.Contains(dr) : k is Match.ItemChao ic ? m.itensChao.Contains(ic) : m.bombDropped && !m.bombPlanted);
                if (!existe) { Apagar(armasChao[k].go); armasChao.Remove(k); }
            }
            foreach (var ac in armasChao.Values) Fisica(ac, dt);
        }

        void Fisica(ArmaCaindo ac, float dt)
        {
            if (!ac.parada)
            {
                ac.v.y -= 12f * dt;
                var novo = ac.p + ac.v * dt;
                // parede: o piso no lugar novo é bem mais alto que o item -> bate e volta naquele eixo
                if (Efeitos3D.Piso(new Vector3(novo.x, ac.p.y, ac.p.z)) > ac.p.y + 0.06f) { ac.v.x = -ac.v.x * 0.4f; novo.x = ac.p.x; ac.w *= 0.7f; }
                if (Efeitos3D.Piso(new Vector3(novo.x, ac.p.y, novo.z)) > ac.p.y + 0.06f) { ac.v.z = -ac.v.z * 0.4f; novo.z = ac.p.z; ac.w *= 0.7f; }
                ac.p = novo;
                ac.r = Quaternion.Euler(ac.w * dt) * ac.r;
                float piso = Efeitos3D.Piso(ac.p) + ac.meiaAltura;
                if (ac.p.y < piso)
                {
                    ac.p.y = piso;
                    if (Mathf.Abs(ac.v.y) > 1.2f) fx.Passo(ac.p, Vector3.zero);   // poeirinha do baque
                    ac.v = new Vector3(ac.v.x * 0.62f, -ac.v.y * 0.32f, ac.v.z * 0.62f); ac.w *= 0.55f;
                    // rolando/escorregando no chão até parar
                    if (Mathf.Abs(ac.v.y) < 0.5f)
                    {
                        ac.v.y = 0;
                        var h = new Vector3(ac.v.x, 0, ac.v.z);
                        if (h.magnitude < 0.08f) { ac.parada = true; ac.deitar = 0; }
                    }
                }
                if (ac.p.y <= piso + 0.001f && ac.v.y == 0)
                {   // atrito no chão (desliza e para) e o giro acompanha o rolamento
                    ac.v.x *= Mathf.Exp(-4f * dt); ac.v.z *= Mathf.Exp(-4f * dt); ac.w *= Mathf.Exp(-5f * dt);
                    if (new Vector3(ac.v.x, 0, ac.v.z).magnitude < 0.08f) { ac.parada = true; ac.deitar = 0; }
                }
            }
            else if (ac.deitar < 1)
            {
                // assenta deitado de lado, mantendo pra onde o cano aponta
                ac.deitar = Mathf.Min(1, ac.deitar + dt * 5);
                var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) fw = Vector3.forward;
                ac.r = Quaternion.Slerp(ac.r, Quaternion.LookRotation(fw.normalized) * Quaternion.Euler(0, 0, 90), ac.deitar);
                ac.p.y = Mathf.Lerp(ac.p.y, Efeitos3D.Piso(ac.p) + ac.meiaAltura, ac.deitar);
            }
            ac.go.transform.SetPositionAndRotation(ac.p, ac.r);
        }

        void Bonecos(float dt)
        {
            foreach (var a in m.agents)
            {
                var b = bonecos[a.id];
                if (!est.TryGetValue(a.id, out var e)) { e = new TelaJogo.Anim { ultPos = a.pos }; est[a.id] = e; }
                if (a.brain.action != e.acao)
                {
                    if (e.acao == "granada" && m.t - e.acaoIni >= e.acaoDur * 0.85f && a.alive) evento[a.id] = ("granada_fim", m.t, 0.4f);
                    e.acao = a.brain.action; e.acaoIni = m.t; e.acaoDur = Mathf.Max(0.2f, a.brain.ActionEnd - m.t);
                }
                float dist = (a.pos - e.ultPos).Len; e.ultPos = a.pos;
                string arma = Arma(a), anim; float u = 0; bool porTempo = false;
                float tiroDur = arma == "AWP" ? 1.1f : 0.25f;
                string acao = a.brain.action;
                // pernas: correm pra onde o boneco anda; tronco e arma ficam na mira. De costas = ciclo ao contrário.
                float alvoPerna = 0; bool anda = a.Speed > 6 && a.alive;
                if (anda)
                {
                    float d = Mathf.DeltaAngle(a.aim * Mathf.Rad2Deg, Mathf.Atan2(a.vel.y, a.vel.x) * Mathf.Rad2Deg);
                    bool costas = Mathf.Abs(d) > 105;
                    if (costas) d = Mathf.DeltaAngle(0, d + 180);
                    alvoPerna = Mathf.Clamp(d, -75, 75);
                    if (costas) dist = -dist;
                }
                bool naEscada = a.alive && !a.noAr && NavGrid.Escada(a.pos) && a.z > MapData.GZ + 1.5f;
                // passada do momento (pra camada das pernas quando a animação de cima é uma ação)
                string marcha = anda && !a.faca && !naEscada && !a.noAr ? (a.crouch ? "agachado" : a.Speed < a.MaxSpeed * 0.62f ? "devagar" : "andar") : null;
                float passoAntes = e.passo;
                evento.TryGetValue(a.id, out var ev);
                if (!a.alive)
                {
                    // morreu: ragdoll de verdade a partir da pose em que estava (o corpo fica onde caiu; a arma voa à parte)
                    if (!ragdolls.TryGetValue(a.id, out var rg))
                    {
                        var mo = Morte(a, b);
                        // o piscar do último tiro zera: o corpo não posa mais (só o ragdoll), o branco ficaria preso nele
                        b.Golpe(0);
                        b.semArma = true; b.Pose(b.animAtual ?? "mira_" + arma, b.uAtual, 0);   // esconde a arma da mão
                        rg = new Ragdoll3D(b, mo.dir, mo.forca, a.id * 31 + rn); ragdolls[a.id] = rg;
                    }
                    rg.Passo(dt); Costas(a.id, b, null);
                    b.noChao = false; b.Silhueta(false); ultHp[a.id] = a.hp;
                    continue;
                }
                // escada de mão: pés degrau a degrau pela altura subida (2 degraus de 4 = 8 unidades por ciclo), arma nas costas
                if (naEscada)
                {
                    anim = "escada"; u = (a.z - MapData.GZ) / 8f;
                    if (escadaVista.Add(a.id)) Debug.Log($"[Visual3D] escada: {a.name} round {rn} t={m.t:0.0}");
                }
                else if (acao == "plantar" || acao == "fake plant") { anim = "plantar"; u = (m.t - e.acaoIni) / (acao == "plantar" ? e.acaoDur : 3.2f); }
                else if (acao == "desarmar" || acao == "fake defuse") { anim = "desarmar"; u = m.t - e.acaoIni; porTempo = true; }
                else if (acao == "granada")
                {
                    string g = a.brain.AcaoGranada ?? "smoke";
                    anim = "granada_" + (g == "incendiaria" ? "molotov" : g); u = (m.t - e.acaoIni) / e.acaoDur;
                }
                else if (ev.anim == "granada_fim") { anim = "granada_fim"; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "costas" && !a.faca) { anim = "sacar_costas_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (a.Blind(m.t)) { anim = b.Tem("cego_" + arma) ? "cego_" + arma : "cego"; u = m.t; porTempo = true; }
                else if (ev.anim == "pegar" && !a.faca) { anim = "pegar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "trocar" && !a.faca) { anim = "sacar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (!a.faca && a.gun.Reloading) { anim = "recarga_" + arma; u = 1 - a.gun.reloadLeft / a.gun.s.Reload; }
                else if (!a.faca && m.t - a.lastShotT < tiroDur) { anim = "tiro_" + arma; u = (m.t - a.lastShotT) / tiroDur; }
                else if (a.noAr) anim = (a.faca ? "faca_" : "") + (a.vz > 0 ? "no_ar_sobe" : "no_ar_cai") + (a.faca ? "" : "_" + arma);
                // meio cego (flash de lado): aba com a mão na testa, rosto virado, arma ainda na frente
                else if (!a.faca && a.MeioCego(m.t) && b.Tem("meio_cego_" + arma)) { anim = "meio_cego_" + arma; u = m.t; porTempo = true; }
                else if (a.faca) { anim = anda ? "faca_andar" : "faca_parado"; e.passo += dist / TelaJogo.Ciclo("faca_andar"); u = e.passo; }
                else if (anda && a.crouch) { anim = "agachado_" + arma; e.passo += dist / CICLO_AGACHADO; u = e.passo; }
                else if (anda && a.Speed < a.MaxSpeed * 0.62f) { anim = "devagar_" + arma; e.passo += dist / CICLO_DEVAGAR; u = e.passo; }
                else if (anda) { anim = "andar_" + arma; e.passo += dist / TelaJogo.Ciclo("andar"); u = e.passo; }
                else if (arma == "AWP" && a.scoped)
                {
                    // entrando na luneta (0,27 s) e depois parado nela
                    if (!luneta.TryGetValue(a.id, out float li)) { li = m.t; luneta[a.id] = li; }
                    float ul = (m.t - li) / 0.27f;
                    if (ul < 1) { anim = "awp_mirar"; u = ul; } else anim = "awp_mirando";
                }
                else if (ev.anim == "apontar" && !a.faca) { anim = "apontar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "aterrissar") anim = (a.faca ? "faca_parado" : "agachar_" + arma);
                else anim = (a.crouch ? "agachar_" : "mira_") + arma;
                if (!(arma == "AWP" && a.scoped)) luneta.Remove(a.id);
                // sacando a arma (trocou ou guardou a faca): a pose de sacar por cima de parado/andando
                if (!a.faca && a.alive && !anda && a.sacaAte > m.t && string.IsNullOrEmpty(acao) && !a.gun.Reloading && !a.noAr
                    && !a.Blind(m.t) && m.t - a.lastShotT > tiroDur && !anim.StartsWith("sacar_costas"))
                {
                    if (!sacando.TryGetValue(a.id, out var sc) || sc.ate != a.sacaAte) { sc = (m.t, a.sacaAte); sacando[a.id] = sc; }
                    // primária com inventário (veio da pistola pelo Q): sai das costas por cima do ombro
                    bool dasCostas = !WeaponStats.Pistola(a.gunId) && a.TemOutra && b.Tem("sacar_costas_" + arma);
                    anim = (dasCostas ? "sacar_costas_" : "sacar_") + arma; u = (m.t - sc.ini) / Mathf.Max(0.1f, sc.ate - sc.ini);
                }
                // mira pra cima/baixo: pro alvo do duelo (telhado, 2º andar, escada)
                float pAlvo = 0;
                var alvoA = a.alive ? a.brain.target : null;
                if (alvoA != null)
                {
                    float hz = (alvoA.pos - a.pos).Len;
                    pAlvo = Mathf.Clamp(Mathf.Atan2(alvoA.z - a.z, Mathf.Max(hz, 1)) * Mathf.Rad2Deg, -60, 60);
                }
                // sem duelo: indo pegar item olha pra ele no chão; trocando a arma ainda olha pra baixo até a nova subir
                else if (a.alive && string.IsNullOrEmpty(acao) && OlhaItem(a, out float pItem)) pAlvo = pItem;
                else if (a.alive) pAlvo = PitchMira(a);
                if (alvoA == null && ev.anim == "trocar" && (m.t - ev.ini) / ev.dur < 0.55f) pAlvo = -40;
                b.pitch = Mathf.Lerp(b.pitch, pAlvo, 1 - Mathf.Exp(-10f * dt)); b.armaPitch = arma;
                if (!b.Tem(anim)) anim = "mira_" + TelaJogo.Padrao(!a.IsT);
                // ação de tronco andando: as pernas seguem a passada (recarregar/atirar/sacar/jogar granada correndo)
                bool acaoTronco = anim.StartsWith("recarga") || anim.StartsWith("tiro") || anim.StartsWith("sacar") || anim.StartsWith("granada") || anim.StartsWith("apontar") || anim.StartsWith("cego") || anim.StartsWith("meio_cego");
                if (marcha != null && acaoTronco)
                {
                    float ciclo = marcha == "agachado" ? CICLO_AGACHADO : marcha == "devagar" ? CICLO_DEVAGAR : TelaJogo.Ciclo("andar");
                    if (e.passo == passoAntes) e.passo += dist / ciclo;
                    string pa = marcha + "_" + arma; if (!b.Tem(pa)) pa = marcha + "_" + TelaJogo.Padrao(!a.IsT);
                    b.pernaAnim = pa; b.pernaU = e.passo;
                }
                else if (a.crouch && a.alive && acaoTronco && !a.noAr)
                {   // agachado parado atirando/recarregando: pernas da pose agachada e o corpo de cima desce junto
                    string pa = "agachar_" + arma; if (!b.Tem(pa)) pa = "agachar_" + TelaJogo.Padrao(!a.IsT);
                    b.pernaAnim = pa; b.pernaU = 0;
                }
                else b.pernaAnim = null;
                b.troncoY = a.crouch && acaoTronco ? -0.1f : 0;
                // meio cego (flash de lado): um pouco da pose de cego por cima, sumindo
                b.misturaW = 0;
                // cego não pinta o boneco de branco (personagem é personagem): quem está perto da flash recebe a luz do estouro
                b.brancoCego = 0;
                if (a.alive && m.FireAt(a.pos)) fx.Queimando(b.transform.position, dt);
                // perna só gira em animação de passo (na ação/no ar volta pro corpo)
                bool passo = anim.StartsWith("andar") || anim.StartsWith("devagar") || anim.StartsWith("agachado") || anim == "faca_andar" || b.pernaAnim != null;
                b.yawPernas = Mathf.LerpAngle(b.yawPernas, passo ? alvoPerna : 0, 1 - Mathf.Exp(-12f * dt));
                // arma grande nas costas (plantar, desarmar, escada, faca): na volta ele puxa das costas por cima do ombro
                bool nasCostas = anim.StartsWith("plantar") || anim.StartsWith("desarmar") || anim == "escada" || anim.StartsWith("faca");
                if (nasCostas) costasEm[a.id] = m.t;
                else if (!a.faca && costasEm.TryGetValue(a.id, out float tc) && m.t - tc < 0.25f && b.Tem("sacar_costas_" + arma) && !anim.StartsWith("tiro"))
                {
                    costasEm.Remove(a.id); evento[a.id] = ("costas", m.t, 0.7f);
                    anim = "sacar_costas_" + arma; u = 0; porTempo = false;
                }
                else costasEm.Remove(a.id);
                if (porTempo) b.Tocar(anim, u, dt); else b.Pose(anim, u, dt);
                // primária guardada nas costas (de pistola na mão; no saque das costas a própria animação leva ela)
                string prim = a.Primaria;
                Costas(a.id, b, prim != null && prim != a.gunId && !anim.StartsWith("sacar_costas") ? prim : null);
                // molotov acesa na mão: o mesmo fogo do chão no pano + luz que ilumina quem segura
                if (anim == "granada_molotov" && b.PecaVisivel("fogo0", out var pano)) fx.Chama(pano, dt, Mathf.Clamp01((u - 0.3f) / 0.15f));
                // laser da AWP na luneta (como nas transmissões): da arma até a parede ou a smoke; forte travado parado
                if (arma == "AWP" && a.scoped) Laser(a, b);
                b.noChao = !naEscada && !a.noAr;
                b.Silhueta(a.alive);
                // levou tiro (vida caiu): tranco + piscar, pela força do dano
                if (ultHp.TryGetValue(a.id, out int hp0) && a.hp < hp0 && a.alive) b.Golpe(0.35f + (hp0 - a.hp) / 60f);
                ultHp[a.id] = a.hp;
                // posição do pé e rumo da mira (suaviza a virada de quadro de simulação)
                b.transform.position = MalhaMapa.ParaUnity(a.pos.x, a.pos.y, a.z);
                float alvoR = naEscada ? 270f : Mathf.Atan2(Mathf.Cos(a.aim), -Mathf.Sin(a.aim)) * Mathf.Rad2Deg;
                float r = rumo.TryGetValue(a.id, out float r0) ? Mathf.LerpAngle(r0, alvoR, 1 - Mathf.Exp(-25f * dt)) : alvoR;
                if (!a.alive) r = r0;
                rumo[a.id] = r;
                b.transform.rotation = Quaternion.Euler(0, r, 0);
                if (naEscada) NaEscada(b.transform);
                b.semArma = false;   // (morto vira ragdoll lá em cima)
            }
        }

        void Efeitos()
        {
            fumaca.Inicio(); fumacaCasca.Inicio(); fogo.Inicio(); rastro.Inicio(); coisas.Inicio();
            // smoke volumétrica: a densidade da simulação (crescimento, bolha da HE, desmanche) + furos de bala do desenho
            Fumacas();
            // rastro de tiro: risco fino que corre do cano pro alvo (não um bastão inteiro piscando)
            foreach (var s in m.shots)
            {
                float idade = m.t - s.t; if (idade > 0.09f || idade < 0) continue;
                var a = MalhaMapa.ParaUnity(s.a.x, s.a.y, s.za); var b = MalhaMapa.ParaUnity(s.b.x, s.b.y, s.zb);
                var d = b - a; float L = d.magnitude; if (L < 1e-3f) continue;
                float cab = Mathf.Clamp01(idade / 0.05f), rabo = Mathf.Clamp01((idade - 0.025f) / 0.065f);
                var p0 = a + d * rabo; var p1 = a + d * cab; float l = Mathf.Max(0.02f, (p1 - p0).magnitude);
                rastro.Por((p0 + p1) * 0.5f, Quaternion.LookRotation(d), new Vector3(0.018f, 0.018f, l));
            }
            Props();

            if (m.bombPlanted)
            {
                V2 bp = m.bombPos;
                float z = m.bombPlanted ? m.bombZ : NavGrid.ZAt(bp);
                if (c4 == null) { c4 = Armas3D.Prop(Boneco3D.Carregar("tr"), "plantar", 0.97f, Boneco3D.matCorpo); c4.transform.SetParent(transform, false); }
                c4.SetActive(true);
                c4.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(bp.x, bp.y, z) + Vector3.up * 0.045f, Quaternion.Euler(0, 20, 0));
                if (m.bombPlanted && !m.defused && !m.exploded && Mathf.Repeat(m.t, m.BombLeft < 10 ? 0.4f : 1f) < 0.15f)
                    fogo.Por(MalhaMapa.ParaUnity(bp.x, bp.y, z + 5.5f), Quaternion.identity, Vector3.one * 0.08f);
            }
            fumaca.Fim(); fumacaCasca.Fim(); fogo.Fim(); rastro.Fim(); coisas.Fim();
            fx.FimChamas();
            fx.FimLasers();
        }

        readonly Dictionary<SmokeVolume, Fumaca3D> fumacas = new Dictionary<SmokeVolume, Fumaca3D>();
        Shader shFumaca; Mesh cuboFumaca;
        void Fumacas() => Fumacas(m.smokes, m.t);
        void Fumacas(List<SmokeVolume> lista, float t)
        {
            if (shFumaca == null) { shFumaca = Shader.Find("CSM/FumacaParticula"); cuboFumaca = null; }
            foreach (var s in lista)
            {
                if (!fumacas.TryGetValue(s, out var f)) { f = new Fumaca3D(s, transform, cuboFumaca, shFumaca); fumacas[s] = f; }
                f.Atualizar(t, cen.cam);
            }
            foreach (var s in fumacas.Keys.ToList())
                if (!lista.Contains(s)) { fumacas[s].Apagar(); fumacas.Remove(s); }
        }

        // ------------------------------------------------------------------ laboratório (Visual3D.CapturarLabBatch)
        // smoke, tiro, HE, molotov, C4 e itens no chão sem depender da partida: mesmo desenho e mesma física do jogo
        readonly List<SmokeVolume> labSmokes = new List<SmokeVolume>();
        readonly List<FireArea> labFogos = new List<FireArea>();
        readonly HashSet<object> labItens = new HashSet<object>();
        readonly List<(Vector3 a, Vector3 b, float t)> labTiros = new List<(Vector3, Vector3, float)>();
        public float LabT;
        public SmokeVolume LabSmoke(V3 o) { var s = new SmokeVolume(o); labSmokes.Add(s); return s; }
        public void LabTiro(V3 a, V3 b)
        {
            var ua = MalhaMapa.ParaUnity(a.x, a.y, a.z); var ub = MalhaMapa.ParaUnity(b.x, b.y, b.z);
            var dir = ub - ua; dir.y = 0; dir.Normalize();
            fx.Tiro(ua, dir, Vector3.Cross(Vector3.up, dir), false, false);
            fx.Impacto(ub, ub - ua, false, false);
            foreach (var f in fumacas.Values) if (f.Cruza(a, b)) f.Furo(a, b, LabT);
            labTiros.Add((ua, ub, LabT));
        }
        public void LabHE(V3 p)
        {
            foreach (var s in labSmokes) if ((new V2(s.origin.x, s.origin.y) - new V2(p.x, p.y)).Len < Match.HE_FURO + 60) s.Clear(p, Match.HE_FURO, 2.1f);
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.HE(u); Tremer(new V2(p.x, p.y), 0.9f); Empurrar(u, 2.4f, 6.5f);
        }
        public void LabC4(V3 p)
        {
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.ExplosaoC4(u); Tremer(new V2(p.x, p.y), 2.5f); Empurrar(u, 9f, 13f);
        }
        public FireArea LabFogo(V3 o) { var f = new FireArea(o); labFogos.Add(f); fx.Molotov(MalhaMapa.ParaUnity(o.x, o.y, o.z)); return f; }
        public void LabApagaFogo(FireArea f) { labFogos.Remove(f); fx.Vapor(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z), 1.1f); }
        public void LabItem(string gunId, V2 pos)
        {
            var k = new object(); labItens.Add(k);
            float z = NavGrid.ZAt(pos);
            var ac = NovoItem(Armas3D.Objeto(Boneco3D.Carregar("tr"), Armas3D.DoJogo(gunId), Boneco3D.matCorpo), MalhaMapa.ParaUnity(pos.x, pos.y, z) + Vector3.up * 0.05f, Vector3.zero, UnityEngine.Random.Range(0, 360f));
            ac.v = Vector3.zero; ac.w = Vector3.zero;
            armasChao[k] = ac;
        }
        public void LabCamera(V2 alvo, float zoom) { camSuave = new Vector2(alvo.x, alvo.y); zoomSuave = zoom; camPronta = true; cen.alvoJogo = camSuave; cen.enquadrar = zoom; cen.Enquadrar(); }
        public void LabPasso(float dt)
        {
            LabT += dt;
            foreach (var s in labSmokes) s.Update(dt);
            foreach (var f in labFogos) f.Update(dt);
            Fumacas(labSmokes, LabT);
            fx.Fogos(labFogos, dt); fx.Atualizar(dt);
            foreach (var ac in armasChao.Values) Fisica(ac, dt);
            rastro.Inicio();
            foreach (var (a, b, t0) in labTiros)
            {
                float idade = LabT - t0; if (idade > 0.09f) continue;
                var d = b - a; float cab = Mathf.Clamp01(idade / 0.05f), rabo = Mathf.Clamp01((idade - 0.025f) / 0.065f);
                var p0 = a + d * rabo; var p1 = a + d * cab; float l = Mathf.Max(0.02f, (p1 - p0).magnitude);
                rastro.Por((p0 + p1) * 0.5f, Quaternion.LookRotation(d), new Vector3(0.018f, 0.018f, l));
            }
            rastro.Fim();
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar(); AplicarTremor(dt);
        }

        // granadas voando e no chão e o C4: os modelos de verdade (peças da animação de jogar/plantar)
        GameObject c4;
        readonly Dictionary<object, GameObject> props = new Dictionary<object, GameObject>();
        readonly HashSet<object> propsVistos = new HashSet<object>();
        GameObject Granada(string tipo, bool tr)
        {
            string k = tipo == "incendiaria" ? "molotov" : tipo;
            var g = Armas3D.Prop(Boneco3D.Carregar(tr ? "tr" : "ct"), "granada_" + k, 0.5f, Boneco3D.matCorpo, new[] { "sm_", "fl_", "mo_", "he_" });
            g.transform.SetParent(transform, false);
            return g;
        }
        void Props()
        {
            propsVistos.Clear();
            foreach (var n in m.flying)
            {
                propsVistos.Add(n);
                if (!props.TryGetValue(n, out var g)) { g = Granada(n.l.kind, n.by != null && n.by.IsT); props[n] = g; }
                // gira pelo voo (a física não guarda rotação: a fase sai do tempo e da velocidade)
                g.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(n.x, n.y, n.z), Quaternion.Euler((m.t - n.t0) * 720, (m.t - n.t0) * 300 + n.from.x, 0));
                if (n.l.kind == "incendiaria" || n.l.kind == "molotov") fx.Chama(g.transform.position, 1 / 30f, 1f);
            }
            foreach (var k in props.Keys.ToList())
                if (!propsVistos.Contains(k)) { Apagar(props[k]); props.Remove(k); }
            if (c4 != null && !m.bombPlanted) c4.SetActive(false);
        }

        /// <summary>Objetos reaproveitados por quadro (bolotas, chamas, rastros, coisas no chão).</summary>
        class Pool
        {
            readonly Transform raiz; readonly Mesh malha; readonly Material mat; readonly bool sombra;
            readonly List<(Transform t, MeshRenderer r)> itens = new List<(Transform, MeshRenderer)>(); int usados;
            public Pool(string nome, Mesh me, Material mt, Transform pai, bool sombra)
            {
                raiz = new GameObject(nome).transform; raiz.SetParent(pai, false); malha = me; mat = mt; this.sombra = sombra;
            }
            public void Inicio() => usados = 0;
            readonly MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            public void Por(Vector3 p, Quaternion q, Vector3 s, Material outro = null, Color? cor = null)
            {
                if (usados == itens.Count)
                {
                    var g = new GameObject("i"); g.transform.SetParent(raiz, false);
                    g.AddComponent<MeshFilter>().sharedMesh = malha;
                    var r = g.AddComponent<MeshRenderer>();
                    r.shadowCastingMode = sombra ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    itens.Add((g.transform, r));
                }
                var it = itens[usados++];
                it.r.enabled = true; it.r.sharedMaterial = outro ? outro : mat;
                it.t.SetPositionAndRotation(p, q); it.t.localScale = s;
                if (cor.HasValue)
                {
                    var c = QualitySettings.activeColorSpace == ColorSpace.Linear ? cor.Value.linear : cor.Value; c.a = cor.Value.a;
                    mpb.SetColor("_BaseColor", c); it.r.SetPropertyBlock(mpb);
                }
            }
            public void Fim() { for (int i = usados; i < itens.Count; i++) itens[i].r.enabled = false; }
        }

        void Teclado()
        {
            if (Input.GetKeyDown(KeyCode.Space)) pausado = !pausado && !acabou;
            if (Input.GetKeyDown(KeyCode.Alpha1)) velocidade = 1; if (Input.GetKeyDown(KeyCode.Alpha2)) velocidade = 2;
            if (Input.GetKeyDown(KeyCode.Alpha3)) velocidade = 4; if (Input.GetKeyDown(KeyCode.Alpha4)) velocidade = 8;
            if (Input.GetKeyDown(KeyCode.N)) PulaRound();
            if (Input.GetKeyDown(KeyCode.P)) painel = !painel;
            if (Input.GetKeyDown(KeyCode.Escape)) seguir = -1;
            float w = Input.mouseScrollDelta.y; if (w != 0) zoom = Mathf.Clamp(zoom * (1 - 0.1f * w), 4f, 40f);
        }

        void OnGUI()
        {
            if (sTxt == null)
            {
                sTxt = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = true };
                sCaixa = new GUIStyle(GUI.skin.box); sBotao = new GUIStyle(GUI.skin.button) { fontSize = 12 };
                var fundo = new Texture2D(1, 1); fundo.SetPixel(0, 0, new Color(0.05f, 0.05f, 0.06f, 0.6f)); fundo.Apply(); sCaixa.normal.background = fundo;
            }
            if (carregando) { GUI.Label(new Rect(20, 20, 400, 30), "Montando a maquete e a partida..."); return; }
            // clicar no card do HUD segue o jogador (o HUD é uGUI sem EventSystem: botão invisível por cima)
            for (int i = 0; i < 5; i++)
                foreach (bool timeA in new[] { true, false })
                {
                    var j = (timeA ? pt.A : pt.B).js[i];
                    var a = m.agents.FirstOrDefault(x => x.name == j.nome);
                    if (a != null && GUI.Button(hud.TelaDoCard(timeA, i), GUIContent.none, GUIStyle.none)) seguir = seguir == a.id ? -1 : a.id;
                }
            float k = Mathf.Max(1, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            float W = Screen.width / k, H = Screen.height / k;
            // controles pequenos embaixo do minimapa (o canto de baixo é dos cards)
            float x = 14, y = 270;
            bool B(string t, float w) { bool r = GUI.Button(new Rect(x, y, w, 22), t, sBotao); x += w + 4; return r; }
            if (B(pausado ? "Continuar" : "Pausar", 70)) pausado = !pausado && !acabou;
            foreach (float v in new[] { 1f, 2f, 4f, 8f }) if (B((velocidade == v ? ">" : "") + v + "x", 36)) velocidade = v;
            x = 14; y += 26;
            if (B("Próximo round", 96)) PulaRound();
            if (B(painel ? "Sem pensamento" : "Pensamento (P)", 110)) painel = !painel;
            // partida vinda do manager: no fim, fecha a partida da carreira (prêmio, salários) e volta ao painel do clube
            var carreira = CSM.Manager.Carreira.Atual;
            if (acabou && carreira != null && carreira.emPartida)
            {
                bool venceu = pt.vencedor == pt.A;
                var r = new Rect(W / 2 - 190, H / 2 - 60, 380, 120);
                GUI.Box(r, GUIContent.none, sCaixa);
                GUI.Label(new Rect(r.x + 12, r.y + 10, r.width - 24, 50), $"<b>{(venceu ? "Vitória" : "Derrota")}</b>  {pt.A.nome} {pt.A.placar} x {pt.B.placar} {pt.B.nome}", sTxt);
                if (GUI.Button(new Rect(r.x + 12, r.y + 66, r.width - 24, 40), "Voltar ao manager", sBotao))
                {
                    carreira.Registrar(venceu, pt.A.placar, pt.B.placar, true);
                    UnityEngine.SceneManagement.SceneManager.LoadScene("Manager");
                    return;
                }
            }
            // o que o jogador seguido está pensando (explicação dos bots)
            if (!painel || seguir < 0) return;
            var s = m.agents[seguir];
            const float PW = 300;
            GUI.Box(new Rect(W / 2 - PW / 2, H - 230, PW, 118), GUIContent.none, sCaixa);
            GUI.Label(new Rect(W / 2 - PW / 2 + 6, H - 227, PW - 12, 112), $"<b>{s.name}</b> — {s.brain.order.name}\n{s.brain.thought}\n<b>Por quê:</b> {s.brain.porqueAgora}", sTxt);
        }
    }
}
