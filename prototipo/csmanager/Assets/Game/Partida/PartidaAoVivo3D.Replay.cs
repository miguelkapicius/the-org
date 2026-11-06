using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using CSM.Sim;
using CSM.Map;
using CSM.View;
using CSM.Audio;   // sons (Som3D, da sessão de som): chamados junto de cada efeito visual

namespace CSM.View3D
{
    // PartidaAoVivo3D, parte: replay do último abate: gravação dos quadros, lances, câmeras do replay.
    public partial class PartidaAoVivo3D
    {
        // ------------------------------------------------------------------ replay do último abate
        // cada quadro ao vivo guarda o que foi desenhado (pose, posição, giro); no fim do round o lance que decidiu
        // (o último abate) volta em câmera lenta, com a câmera fechada em quem matou e quem morreu
        struct Pose1 { public string anim, pernaAnim, agachaAnim; public float u, pernaU, yawPernas, pitch, agachaW, troncoY; public bool agachaSoPernas; public string armaPitch; public bool semArma, vivo; public Vector3 p; public Quaternion rot; }
        class Quadro { public float t; public Pose1[] b; public (SmokeVolume s, float age)[] smk; public (FireArea f, float age)[] fogo; }
        static readonly (SmokeVolume, float)[] SEM_SMK = new (SmokeVolume, float)[0];
        static readonly (FireArea, float)[] SEM_FOGO = new (FireArea, float)[0];
        class TiroRec { public float t; public Vector3 boca, dir, ate; public bool pistola, awp, temBoca, hit, hs, prot; public int quem = -1; public string gun; }
        readonly List<Quadro> gravado = new List<Quadro>();
        readonly List<TiroRec> tirosGravados = new List<TiroRec>();
        // um lance do replay: trecho gravado, velocidade, quem matou/morreu (pra silhueta e câmera) e o ângulo da câmera;
        // o final (o lance que decidiu) é em câmera lenta e troca de ângulo perto do abate
        class Lance { public float de, ate, vel = 1, tAbate; public int matou = -1, morreu = -1, angulo, multi; public string legenda; public bool final, valido; public V2? foco; public float nota; }
        class Replay
        {
            public float de, ate, t; public int matou = -1, morreu = -1; public int prox;
            public List<Lance> lances = new List<Lance>(); public int i = -1; public float noLance, giro; public int anguloAgora = -1; public bool novo;
            public Lance L => lances[i];
        }
        Replay replay; bool replayTentado; float fimT, tFim;
        const float REPLAY_VEL = 0.42f;

        void Gravar()
        {
            // carimbo = o tempo que está DESENHADO (o boneco fica entre o passo de antes e o atual, ver Visivel), não o
            // m.t do passo: a 60+ fps dois quadros seguidos tinham o mesmo m.t com poses diferentes e o replay andava aos
            // trancos (corre, para, corre a 32 Hz), o que na câmera lenta ficava bem visível
            float tVis = m.t - Match.DT * (1 - Mathf.Clamp01(1 - (m.t - relogio) / Match.DT));
            if (gravado.Count > 0) tVis = Mathf.Max(tVis, gravado[gravado.Count - 1].t);
            var q = new Quadro { t = tVis, b = new Pose1[m.agents.Count] };
            for (int i = 0; i < m.agents.Count; i++)
            {
                var a = m.agents[i]; var b = bonecos[a.id];
                q.b[i] = new Pose1 { anim = b.animAtual, u = b.uAtual, pernaAnim = b.pernaAnim, pernaU = b.pernaU, yawPernas = b.yawPernas, pitch = b.pitch,
                                     armaPitch = b.armaPitch, semArma = b.semArma, vivo = a.alive, p = b.transform.position, rot = b.transform.rotation,
                                     agachaAnim = b.agachaAnim, agachaW = b.agachaW, agachaSoPernas = b.agachaSoPernas, troncoY = b.troncoY };
            }
            // smokes e fogos com a idade deste quadro: o replay desenha o que existia NA HORA do lance (não os do fim do round)
            q.smk = m.smokes.Count == 0 ? SEM_SMK : m.smokes.Select(x => (x, x.age)).ToArray();
            q.fogo = m.fires.Count == 0 ? SEM_FOGO : m.fires.Select(x => (x, x.age)).ToArray();
            gravado.Add(q);
            // o round inteiro fica gravado (os melhores momentos podem ser do começo); teto de segurança de 230 s
            while (gravado.Count > 0 && gravado[0].t < m.t - 230) gravado.RemoveAt(0);
            while (tirosGravados.Count > 0 && tirosGravados[0].t < m.t - 230) tirosGravados.RemoveAt(0);
        }

        // replay do fim do round: MELHORES MOMENTOS (até 3 lances-chave do round inteiro, ~14 s no máximo, velocidade normal, um ângulo de câmera por lance, corte
        // entre eles) e, por último, o lance que decidiu em CÂMERA LENTA (atrás do ombro de quem matou e, perto do
        // abate, corta pra uma órbita em volta da vítima). Lance = abates a menos de 3,5 s um do outro; nota pelo que o
        // killfeed do CS2 destaca (HS, sem luneta, pela smoke/parede, no ar, cego), multi-kill, clutch, abertura e AWP.
        const float MOMENTOS_MAX = 14f; const int MOMENTOS_N = 3;
        void IniciarReplay()
        {
            if (gravado.Count < 30) return;
            float g0 = gravado[0].t;
            var abs = m.abates.Where(x => x.v != null && x.t <= tFim + 0.05f && x.t >= g0).ToList();
            int vT = m.agents.Count(a => a.IsT), vC = m.agents.Count - vT;
            var lances = new List<Lance>(); Lance cur = null; var deQuem = new Dictionary<Agent, int>();
            for (int k = 0; k < abs.Count; k++)
            {
                var ab = abs[k];
                bool valido = ab.by != null && ab.by != ab.v && !ab.aliado;
                int vivosMatador = !valido ? 0 : ab.by.IsT ? vT : vC;
                if (ab.v.IsT) vT--; else vC--;
                float nt = 4;
                if (valido)
                {
                    nt = 10;
                    if (ab.hs) nt += 3; if (ab.semLuneta) nt += 6; if (ab.atravesSmoke) nt += 4; if (ab.parede) nt += 6;
                    if (ab.noAr) nt += 5; if (ab.cegoQuemMatou) nt += 5; if (ab.arma == "awp") nt += 5;
                    if (vivosMatador == 1) nt += 8;   // clutch
                    if (k == 0) nt += 6;              // abertura: a primeira kill do round
                }
                if (cur == null || ab.t - cur.tAbate > 3.5f) { cur = new Lance { de = ab.t - 1.8f }; lances.Add(cur); deQuem.Clear(); }
                int n = 0;
                if (valido) { deQuem[ab.by] = n = (deQuem.TryGetValue(ab.by, out var q) ? q : 0) + 1; if (n > 1) nt += 8; cur.valido = true; cur.multi = Mathf.Max(cur.multi, n); }
                cur.tAbate = ab.t; cur.ate = ab.t + 0.7f; cur.nota += nt;
                cur.matou = valido ? m.agents.IndexOf(ab.by) : -1; cur.morreu = m.agents.IndexOf(ab.v);
                cur.legenda = LegendaAbate(ab, n, valido && k == 0 && n == 1, valido && vivosMatador == 1);
            }
            // o lance que decidiu: o último abate; se a C4 foi desarmada bem depois dele, o desarme
            Lance fin;
            if (m.defused && (abs.Count == 0 || tFim - abs[abs.Count - 1].t > 2.5f))
            {
                var d = m.agents.Where(a => a.alive && !a.IsT).OrderBy(a => (a.pos - m.bombPos).Len).FirstOrDefault();
                fin = new Lance { tAbate = tFim, de = tFim - 3.2f, ate = tFim + 0.8f, matou = d == null ? -1 : m.agents.IndexOf(d), foco = m.bombPos,
                                  legenda = d == null ? "C4 desarmada" : $"{d.name}  <size=22>desarmou a C4</size>" };
            }
            else if (lances.Count > 0)
            {
                fin = lances[lances.Count - 1]; lances.RemoveAt(lances.Count - 1);
                fin.de = fin.tAbate - 2.3f; fin.ate = fin.tAbate + 0.9f;
            }
            else return;
            fin.final = true; fin.vel = REPLAY_VEL; fin.de = Mathf.Max(fin.de, g0); fin.angulo = 1;
            // melhores momentos: os 3 de maior nota (só lance com kill de verdade), na ordem em que aconteceram;
            // lance simples até 4 s, multi-kill até 6 s
            foreach (var l in lances) { l.de = Mathf.Max(l.de, g0); float cap = l.multi >= 2 ? 6f : 4f; if (l.ate - l.de > cap) l.de = l.ate - cap; }
            var escolhidos = new List<Lance>(); float total = 0;
            foreach (var l in lances.Where(l => l.valido).OrderByDescending(l => l.nota))
            {
                float dur = l.ate - l.de; if (escolhidos.Count >= MOMENTOS_N || total + dur > MOMENTOS_MAX) continue;
                escolhidos.Add(l); total += dur;
            }
            escolhidos.Sort((x, y) => x.de.CompareTo(y.de));
            int[] giro = { 2, 3, 4, 1 };   // lateral, drone, órbita, ombro: um ângulo diferente por lance
            for (int k = 0; k < escolhidos.Count; k++) escolhidos[k].angulo = giro[k % giro.Length];
            var r = new Replay(); r.lances.AddRange(escolhidos); r.lances.Add(fin); rq0 = rq1 = null;
            replay = r;
            ProximoLance(r);
            Debug.Log($"[Visual3D] replay round {rn}: {escolhidos.Count} melhores momentos ({total:0.0} s) + final em câmera lenta ({(fin.ate - fin.de) / REPLAY_VEL:0.0} s)");
        }

        static string LegendaAbate(Match.Abate ab, int n, bool abertura = false, bool clutch = false)
        {
            if (ab.by == null || ab.by == ab.v) return $"{ab.v.name}  <size=22>morreu</size>";
            var extra = new List<string>();
            if (abertura) extra.Add("abriu o round"); if (clutch) extra.Add("clutch");
            if (n >= 2) extra.Add($"{n} abates");
            if (ab.semLuneta) extra.Add("sem luneta"); if (ab.atravesSmoke) extra.Add("pela smoke"); if (ab.parede) extra.Add("pela parede");
            if (ab.noAr) extra.Add("no ar"); if (ab.cegoQuemMatou) extra.Add("cego"); if (ab.hs) extra.Add("na cabeça");
            return $"{ab.by.name}  <size=22>com a</size>  {Hud3D.NomeArma(ab.arma)}" + (extra.Count > 0 ? $"  <size=22>{string.Join(" · ", extra)}</size>" : "");
        }

        void ProximoLance(Replay r)
        {
            r.i++;
            var L = r.L;
            r.t = r.de = L.de; r.ate = L.ate; r.matou = L.matou; r.morreu = L.morreu; r.noLance = 0; r.giro = 0; r.anguloAgora = -1; r.novo = true;
            r.prox = tirosGravados.FindIndex(x => x.t >= L.de); if (r.prox < 0) r.prox = tirosGravados.Count;
            hud.Replay(true, L.legenda, L.final ? "REPLAY" : "MELHORES MOMENTOS");
            hud.Corte();   // piscada de corte também na entrada (o quadro de antes ainda é o fim do round ao vivo)
            // o tempo pula: a smoke 3D guarda o quanto já cresceu, então recomeça do zero e cresce até a idade do lance
            foreach (var f in fumacas.Values) f.Apagar();
            fumacas.Clear();
        }

        void ReplayPasso(float dt)
        {
            var r = replay;
            float vel = r.L.vel;
            r.t += dt * vel; r.noLance += dt;
            if (r.t >= r.ate)
            {
                if (r.i + 1 >= r.lances.Count) { replay = null; hud.Replay(false); FechaRound(); return; }
                ProximoLance(r); vel = r.L.vel;
            }
            // quadros em volta do tempo do replay
            // (busca binária: o round inteiro gravado passa de 30 mil quadros a 144 fps; o carimbo só cresce)
            int lo = 0, hi = gravado.Count;
            while (lo < hi) { int mid = (lo + hi) >> 1; if (gravado[mid].t >= r.t) hi = mid; else lo = mid + 1; }
            int i1 = lo < gravado.Count ? lo : gravado.Count - 1; int i0 = Mathf.Max(0, i1 - 1);
            var q0 = gravado[i0]; var q1 = gravado[i1];
            float w = q1.t > q0.t ? Mathf.Clamp01((r.t - q0.t) / (q1.t - q0.t)) : 1;
            rq0 = q0; rq1 = q1; rw = w;
            // 1º quadro do lance: pose seca. Misturando, o boneco sairia da pose em que estava (deitado, morto no fim do
            // round) e "levantava" no começo do lance, parecendo cair no mapa
            float dtPose = r.novo ? 0 : dt * vel; r.novo = false;
            for (int i = 0; i < m.agents.Count; i++)
            {
                var b = bonecos[m.agents[i].id]; var p0 = q0.b[i]; var p1 = q1.b[i]; var pp = w < 0.5f ? p0 : p1;
                b.transform.position = Vector3.Lerp(p0.p, p1.p, w);
                b.transform.rotation = Quaternion.Slerp(p0.rot, p1.rot, w);   // rotação inteira (a inclinação da escada vem junto)
                b.pernaAnim = pp.pernaAnim; b.pernaU = p0.pernaAnim == p1.pernaAnim ? Mathf.LerpUnclamped(p0.pernaU, p1.pernaU, w) : pp.pernaU;
                b.yawPernas = Mathf.LerpAngle(p0.yawPernas, p1.yawPernas, w); b.pitch = Mathf.Lerp(p0.pitch, p1.pitch, w); b.armaPitch = pp.armaPitch;
                b.agachaAnim = pp.agachaAnim; b.agachaSoPernas = pp.agachaSoPernas; b.agachaW = Mathf.Lerp(p0.agachaW, p1.agachaW, w); b.troncoY = Mathf.Lerp(p0.troncoY, p1.troncoY, w);
                // a vítima do lance mantém a silhueta no replay (cair atrás de parede ainda aparece)
                // desarmando: a C4 da animação some (a que vale é a plantada no chão); antes ficava o estado do fim do
                // round e o replay mostrava duas C4
                b.semArma = pp.semArma; b.semC4 = pp.anim != null && (pp.anim.StartsWith("desarmar") || pp.anim.StartsWith("plantar") && c4PlantouEm >= 0 && r.t >= c4PlantouEm);   // (plantar: depois que a C4 foi pro chão)
                b.Silhueta(pp.vivo || i == r.morreu); Costas(m.agents[i].id, b, null);   // (costas só ao vivo)
                if (!pp.vivo && ragdolls.TryGetValue(m.agents[i].id, out var rg)) rg.Aplicar(r.t - m.agents[i].diedAt);
                else if (pp.anim != null) b.Pose(pp.anim, p0.anim == p1.anim ? Mathf.LerpUnclamped(p0.u, p1.u, w) : pp.u, dtPose);
            }
            MaoGranadas();
            // tiros do lance de novo (clarão, cápsula, impacto) e partículas em câmera lenta
            while (r.prox < tirosGravados.Count && tirosGravados[r.prox].t <= r.t)
            {
                var s = tirosGravados[r.prox++];
                if (s.temBoca) { fx.Tiro(s.boca, s.dir, Vector3.Cross(Vector3.up, s.dir), s.pistola, s.awp); if (s.gun != null) Som3D.Tiro(s.boca, s.gun, s.quem); }
                if (s.quem >= 0 && bonecos.TryGetValue(s.quem, out var bq)) bq.Disparo(s.awp ? 1f : s.pistola ? 0.7f : 0.5f);
                if (s.temBoca) fx.Rastro(s.boca, s.ate);
                fx.Impacto(s.ate, s.ate - s.boca, s.hit, s.hs, s.prot); Som3D.Impacto(s.ate, s.hit, s.hs, s.prot);
            }
            // fogo do molotov como estava na hora (a área guarda a idade; empresta a gravada e devolve)
            replayFogos.Clear(); var fq = (w < 0.5f ? q0 : q1).fogo;
            if (fq != null) foreach (var (f, idade) in fq) { replayIdade.Add(f.age); f.age = idade; replayFogos.Add(f); }
            fx.Fogos(replayFogos, dt * vel);
            for (int k = 0; k < replayFogos.Count; k++) replayFogos[k].age = replayIdade[k];
            replayIdade.Clear();
            fx.Atualizar(dt * vel); fx.FimLasers();   // (laser só ao vivo)
            foreach (var ac in armasChao.Values) ItemNoReplay(ac, r.t);
            ReplayCamera(r, dt);
            AplicarTremor(dt * vel);
        }

        // ---- câmeras do replay: ângulos de verdade em volta do lance (a do jogo é sempre do sudeste a 50°)
        // 0 jogo, 1 ombro (atrás de quem matou, olhando a vítima), 2 lateral (de lado pro duelo), 3 drone (quase de cima,
        // girando), 4 órbita (em volta, girando). Cada ângulo testa a linha até a câmera contra a altura dos prédios
        // (MapData.Top) e desvia (gira/sobe) até achar uma vista livre; a escolha vale o ângulo inteiro (não pula).
        Quadro rq0, rq1; float rw;
        readonly List<FireArea> replayFogos = new List<FireArea>();
        readonly List<SmokeVolume> replaySmokes = new List<SmokeVolume>();
        readonly List<float> replayIdade = new List<float>();

        /// <summary>Smokes do replay: as que existiam no quadro gravado, com a idade de então (interpolada); a idade
        /// verdadeira volta logo depois de desenhar (a simulação não vê nada).</summary>
        void FumacasReplay()
        {
            replaySmokes.Clear(); replayIdade.Clear();
            var base0 = rw < 0.5f ? rq0 : rq1;
            if (base0?.smk != null)
                foreach (var (s, idade) in base0.smk)
                {
                    float a = idade;
                    var outro = rw < 0.5f ? rq1 : rq0;
                    if (outro?.smk != null) foreach (var (s2, i2) in outro.smk) if (s2 == s) { a = rw < 0.5f ? Mathf.Lerp(idade, i2, rw) : Mathf.Lerp(i2, idade, rw); break; }
                    replayIdade.Add(s.age); s.age = a; replaySmokes.Add(s);
                }
            Fumacas(replaySmokes, replay.t);
            for (int k = 0; k < replaySmokes.Count; k++) replaySmokes[k].age = replayIdade[k];
            replayIdade.Clear();
        }

        float rAzS, rElS, rAzVel, rElVel, rDAz, rDEl, rAltS, rAltVel;
        static readonly (float az, float el)[] DESVIOS = { (0, 0), (35, 0), (-35, 0), (0, 16), (180, 0), (70, 10), (-70, 10), (0, 30) };

        void ReplayCamera(Replay r, float dt)
        {
            var L = r.L;
            Vector3 a = L.matou >= 0 ? bonecos[m.agents[L.matou].id].transform.position : Vector3.zero;
            Vector3 v = L.morreu >= 0 ? bonecos[m.agents[L.morreu].id].transform.position : a;
            if (L.matou < 0) a = v;
            if (L.foco.HasValue && L.morreu < 0) v = MalhaMapa.ParaUnity(L.foco.Value.x, L.foco.Value.y, NavGrid.ZAt(L.foco.Value));
            if (L.matou < 0 && L.morreu < 0) a = v;
            int ang = L.final && r.t >= L.tAbate - 0.6f ? 4 : L.angulo;   // final: do ombro pra órbita pouco antes do abate
            // mais perto da vítima (a queda é o lance); o ombro fica no meio dos dois
            var meio = Vector3.Lerp(a, v, ang == 1 ? 0.5f : 0.62f);
            var alvo = new Vector2(meio.x / MalhaMapa.ESC, -meio.z / MalhaMapa.ESC);
            float dist = (a - v).magnitude;
            float enq = ang == 1 ? Mathf.Clamp(dist * 1.45f + 2.2f, 3.0f, zoom)
                      : ang == 3 ? Mathf.Clamp(dist * 1.9f + 4f, 5f, zoom * 1.3f)
                      : Mathf.Clamp(dist * 1.7f + 2.8f, 3.4f, zoom * 1.1f);
            var kv = new Vector2(a.x - v.x, a.z - v.z);
            if (kv.sqrMagnitude < 1e-4f) kv = new Vector2(1, -1);
            float azKV = Mathf.Atan2(kv.y, kv.x) * Mathf.Rad2Deg;   // da vítima pra quem matou (plano xz do Unity)
            float az, el;
            switch (ang)
            {
                case 1: az = azKV; el = 32; break;
                case 2: az = azKV + 90; el = 36; break;
                case 3: az = -45 + r.noLance * 6; el = 76; break;
                case 4: az = azKV + 130 + r.giro; el = 44; r.giro += dt * 12; break;
                default: az = -45; el = 50; break;
            }
            float alt = meio.y + 0.75f;   // peito (no 2º andar a câmera mira no andar certo)
            bool novo = ang != r.anguloAgora;
            if (novo)
            {
                // vista livre: o primeiro desvio que não bate em prédio vale o ângulo inteiro
                var alvoU = new Vector3(meio.x, alt, meio.z);
                rDAz = 0; rDEl = 0;
                foreach (var (dAz, dEl) in DESVIOS)
                    if (Livre(alvoU, enq, az + dAz, Mathf.Min(82, el + dEl))) { rDAz = dAz; rDEl = dEl; break; }
                    else if (dAz == 0 && dEl == 30) { rDEl = 82 - el; }   // nada livre: quase de cima
                if (r.anguloAgora >= 0) hud.Corte();
                r.anguloAgora = ang;
                camSuave = alvo; camVel = Vector2.zero; zoomSuave = enq; zoomVel = 0; rAltS = alt; rAltVel = 0;
                rAzS = az + rDAz; rElS = Mathf.Min(82, el + rDEl); rAzVel = rElVel = 0;
            }
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, 0.35f, 600f, dt);
            zoomSuave = Mathf.SmoothDamp(zoomSuave, enq, ref zoomVel, 0.5f, 100f, dt);
            rAltS = Mathf.SmoothDamp(rAltS, alt, ref rAltVel, 0.4f, 100f, dt);
            rAzS = Mathf.SmoothDampAngle(rAzS, az + rDAz, ref rAzVel, 0.6f, 400f, dt);
            rElS = Mathf.SmoothDamp(rElS, Mathf.Min(82, el + rDEl), ref rElVel, 0.6f, 200f, dt);
            PorAngulo(camSuave, rAltS, zoomSuave, rAzS, rElS);
        }

        static Vector3 DirCam(float az, float el)
        {
            float a = az * Mathf.Deg2Rad, e = el * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(e) * Mathf.Cos(a), Mathf.Sin(e), Mathf.Cos(e) * Mathf.Sin(a));
        }
        float DistCam(float enq) => enq * 0.5f / Mathf.Tan(cen.fov * 0.5f * Mathf.Deg2Rad);

        /// <summary>Câmera em volta de um ponto (x, y do jogo, altura Unity) com azimute/elevação livres; o Enquadrar do
        /// cenário roda antes só pro foco do desfoque de miniatura (a distância é a mesma, então o foco continua certo).</summary>
        void PorAngulo(Vector2 alvoJogo, float altura, float enq, float az, float el)
        {
            cen.alvoJogo = alvoJogo; cen.enquadrar = enq; cen.Enquadrar();
            var alvo = new Vector3(alvoJogo.x * MalhaMapa.ESC, altura, -alvoJogo.y * MalhaMapa.ESC);
            var dir = DirCam(az, el);
            cen.cam.transform.position = alvo + dir * DistCam(enq);
            cen.cam.transform.rotation = Quaternion.LookRotation(-dir, Vector3.up);
        }

        /// <summary>Linha do alvo até a câmera passa livre por cima dos prédios/coberturas (MapData.Top)?</summary>
        bool Livre(Vector3 alvoU, float enq, float az, float el)
        {
            var dir = DirCam(az, el); float d = DistCam(enq);
            for (float s = 0.6f; s < d; s += 0.2f)
            {
                var p = alvoU + dir * s;
                float z = p.y / MalhaMapa.ESC;
                if (z > 140) return true;   // acima de qualquer prédio
                if (MapData.Top((int)(p.x / MalhaMapa.ESC), (int)(-p.z / MalhaMapa.ESC)) > z) return false;
            }
            return true;
        }
    }
}
