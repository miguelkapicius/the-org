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
    /// <summary>
    /// A partida ao vivo no visual de maquete: a MESMA simulação da PartidaAoVivo (2D), desenhada em 3D — bonecos do
    /// Blender (Boneco3D) com a escolha de animação (câmera e passo em Transmissao), smoke por volume (Fumaca3D), fogo do
    /// molotov, rastro de tiro, granada voando, armas no chão e C4. Câmera segue a ação como no 2D (Transmissao.Mirar).
    /// Teclas: espaço pausa, 1-4 velocidade, N pula o round, P painel, Esc câmera automática, roda do mouse = zoom.
    /// </summary>
    public class PartidaAoVivo3D : MonoBehaviour
    {
        public int seed = 15;
        public float velocidade = 1;

        Sim.Partida pt; Match m; int rn;
        Cenario3D cen;
        readonly Dictionary<int, Boneco3D> bonecos = new Dictionary<int, Boneco3D>();
        readonly Dictionary<int, Transmissao.Anim> est = new Dictionary<int, Transmissao.Anim>();
        readonly Dictionary<int, float> rumo = new Dictionary<int, float>();
        V2 cam, foco; float focoAte = -9, relogio, fimResta;
        bool pausado, painel, carregando = true, acabou, aquecendo;
        public int seguir = -1;
        /// <summary>Câmera parada nesse ponto (laboratório de lances reais: o diretor não muda o enquadramento).</summary>
        public V2? camFixa;
        public float zoom = 8.4f;   // um pouco mais aberto (usuário: as trocas ficavam fora do quadro)
        Pool fumaca, fumacaCasca, fogo, rastro, coisas;
        Material matFumaca, matFogo, matRastro, matCoisa, matC4;
        GUIStyle sTxt, sCaixa, sBotao;

        IEnumerator Start()
        {
            Application.runInBackground = true; Application.targetFrameRate = 60;
            // mapas de distância fixos da Sim (~1 s) em paralelo com a montagem da maquete (o Match espera se não acabou)
            System.Threading.Tasks.Task.Run(Match.FixarCampos);
            yield return null; yield return null;
            // aquece a Sim com um round descartável (o 1º passo do jogo caía de ~165 ms pra ~28 ms; a Sim zera o estado no fim,
            // a partida real fica igual). Trava ~5 s num quadro: o aviso de carregando já está na tela. Com -csm_aquecer quem
            // aquece é a medição (Desempenho), então não repete aqui.
            if (!System.Array.Exists(System.Environment.GetCommandLineArgs(), s => s.StartsWith("-csm_aquecer")))
            {
                aquecendo = true; yield return null;
                Match.Aquecer();   // padrão da Sim (300 passos)
                aquecendo = false;
            }
            Iniciar();
        }

        static void Apagar(UnityEngine.Object o) { if (Application.isPlaying) Destroy(o); else DestroyImmediate(o); }

        /// <summary>Monta a maquete e a partida (o Start chama; o editor chama direto pra tirar print sem Play).</summary>
        public void Iniciar()
        {
            Brain.Explicar = true;
            Diretor.Velho = System.Array.Exists(System.Environment.GetCommandLineArgs(), s => s == "-diretor_velho");   // A/B do diretor
            cen = new GameObject("Cenário 3D").AddComponent<Cenario3D>();
            cen.cam = Camera.main;
            cen.Montar();
            cen.enquadrar = zoom;
            Boneco3D.GarantirMateriais();
            Fumaca3D.Preparar();
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
            Som3D.Iniciar(); Som3D.Ambiente(1f);
            float W = MapData.NX * MalhaMapa.ESC, D = MapData.NY * MalhaMapa.ESC;
            var mini = Hud3D.Minimapa(new Vector3(W / 2, 0, -D / 2), D / 2, W / D);
            hud = new Hud3D(transform, cen.cam, mini);
            hud.modeloArmas = Boneco3D.Carregar("ct");
            hud.Cabeca = ag => bonecos.TryGetValue(ag.id, out var bb) ? bb.transform.position + Vector3.up * Mathf.Lerp(1.2f, 0.95f, Suave(ag.duck)) : Vector3.zero;
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
                if (segundos <= dt * 1.5f) { hud.Atualizar(pt, m, rn, Observado, hudDt); hudDt = 0; }
                if (acabou) break;
            }
            if (hudDt > 0) { hud.Atualizar(pt, m, rn, Observado, Mathf.Min(hudDt, 0.5f)); hudDt = 0; }
            Efeitos();
            hud.ForcarLayout();
        }

        public bool EmReplay => replay != null;
        public int Round => rn;

        /// <summary>Um quadro: simulação (ou fim de round/replay), câmera, bonecos, partículas.</summary>
        /// <summary>Torcida da transmissão no fim do round: clutch (o último vivo do time vencedor ganhou um 1vX com X ≥ 2),
        /// ace (5 abates de um jogador no round) e ace de destaque (1v5, ou os 5 em até 10 s). Lê o killfeed.</summary>
        void Torcida()
        {
            bool tr = m.winner == "TR";
            int vivosV = 0, vivosP = 0; foreach (var a in m.agents) { if (a.IsT == tr) vivosV++; else vivosP++; }
            int x = 0; bool viu1 = false;
            var abates = new Dictionary<string, List<float>>();
            foreach (var (t, s0) in m.feed)
            {
                int a0 = s0.IndexOf(" ["), a1 = s0.IndexOf("] "); if (a0 <= 0 || a1 <= a0) continue;
                string k = s0.Substring(0, a0), v = s0.Substring(a1 + 2).Replace(" (HS)", "");
                var va = m.agents.Find(z => z.name == v); var ka = m.agents.Find(z => z.name == k);
                if (va == null) continue;
                if (va.IsT == tr) vivosV--; else vivosP--;
                if (!viu1 && vivosV == 1) { viu1 = true; x = vivosP; }   // o time vencedor ficou com 1: contra quantos
                if (ka != null && ka.IsT != va.IsT) { if (!abates.TryGetValue(k, out var l)) abates[k] = l = new List<float>(); l.Add(t); }
            }
            foreach (var kv in abates)
                if (kv.Value.Count >= 5)
                {
                    bool absurdo = kv.Value[kv.Value.Count - 1] - kv.Value[kv.Value.Count - 5] <= 10f || (viu1 && x >= 5);
                    Som3D.Torcida(absurdo ? "ace_absurdo" : "ace"); return;
                }
            if (viu1 && x >= 2 && m.agents.Count(z => z.alive && z.IsT == tr) == 1) Som3D.Torcida("clutch");
        }
        string vozVitoria; float vozVitoriaEm;   // anúncio de quem venceu (espera o "bomba desarmada" terminar)
        void Passo(float dt, bool anda, bool limitarTempo)
        {
            Som3D.Relogio(replay != null ? replay.t : m.t);   // recarga agendada no tempo do jogo (em 4x o pente batia depois)
            if (replay != null) { if (anda) ReplayPasso(dt); return; }
            if (anda)
            {
                if (m.over || m.t > 200)
                {
                    // fim de round: o jogo segue POS_ROUND segundos (como no CS: quem perdeu salva a arma, quem ganhou caça
                    // o save; ninguém comemora parado), depois o replay do lance que decidiu e o próximo round
                    if (fimT == 0)
                    {
                        tFim = m.t;
                        // locutor: "bomba desarmada" (se foi) e depois quem venceu, como no CS
                        if (m.over) { if (m.defused) Som3D.Anuncio("bomba_desarmada"); vozVitoria = m.winner == "TR" ? "vitoria_tr" : "vitoria_ct"; vozVitoriaEm = m.defused ? 1.6f : 0f; Torcida(); }
                    }
                    fimT += dt;
                    if (vozVitoria != null && fimT >= vozVitoriaEm) { Som3D.Anuncio(vozVitoria); vozVitoria = null; }
                    if (m.over)
                    {
                        relogio = Mathf.Min(relogio + dt * velocidade, m.t + 0.5f);
                        var sw = System.Diagnostics.Stopwatch.StartNew();
                        while (m.t < relogio && (!limitarTempo || sw.ElapsedMilliseconds < 25)) { GuardarAntes(); m.StepPosRound(); }
                    }
                    else m.t += dt;
                    if (!replayTentado && fimT > Match.POS_ROUND) { replayTentado = true; IniciarReplay(); if (replay != null) return; }
                    if (fimT > Match.POS_ROUND + 0.3f) { FechaRound(); return; }
                }
                else if (freeze > 0)
                {
                    // freeze: bonecos parados no spawn, relógio de compra no HUD; cada compra (t de -FREEZE a 0) dá um tic no
                    // lugar de quem comprou, no máximo 1 a cada 0,12 s no geral (não floda quando o time compra junto)
                    freeze -= dt * velocidade;
                    float tc = -Mathf.Max(0, freeze);
                    while (compraIdx < m.compras.Count && m.compras[compraIdx].t <= tc)
                    {
                        var c = m.compras[compraIdx++];
                        hud.Comprou(m.agents.IndexOf(c.quem), c.item);   // o item sobe da etiqueta, em fila por jogador
                        // limite por base (a câmera mostra uma; a outra, longe, não pode comer os tics desta)
                        int ld = c.quem.IsT ? 0 : 1;
                        if (tc - ultTic[ld] >= 0.12f && bonecos.TryGetValue(c.quem.id, out var bc)) { Som3D.Compra(bc.transform.position); ultTic[ld] = tc; tics++; }
                    }
                    if (freeze <= 0)
                    {
                        freeze = 0; Som3D.Hud("fim_freeze"); Som3D.GoGoGo(); relogio = m.t;
                        Debug.Log($"[Visual3D] freeze round {rn}: {m.compras.Count} compras, {tics} tics");
                    }
                }
                else
                {
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    relogio = Mathf.Min(relogio + dt * velocidade, m.t + 0.5f);
                    while (m.t < relogio && !m.over && (!limitarTempo || sw.ElapsedMilliseconds < 25)) { GuardarAntes(); m.Step(); if (Desempenho.Ativo) Desempenho.DepoisDoStep(); }
                    simMs = sw.Elapsed.TotalMilliseconds;
                }
            }
            var alvo = seguir >= 0 ? m.agents[seguir] : null;
            if (camFixa.HasValue) { foco = camFixa.Value; cam = camFixa.Value; }
            else if (alvo != null) { foco = alvo.pos; cam = cam + (foco - cam) * (1 - Mathf.Exp(-5f * dt)); }
            else if (Diretor.Velho) Transmissao.Mirar(m, ref cam, ref foco, ref focoAte, dt);
            else { diretor.Atualizar(m, freeze, anda ? dt * velocidade : 0); foco = cam = diretor.centro; }
            MoverCamera(dt);
            Bonecos(dt);
            AfastarCorpos();
            MaoGranadas();
            KitsNoCorpo();
            Gravar();
            if (anda) { Detectar(); Eventos(dt); }
            // gatilho no pente vazio (Match.cliques): clique seco no lugar do boneco
            while (cliqueIdx < m.cliques.Count)
            {
                var c = m.cliques[cliqueIdx++];
                if (bonecos.TryGetValue(c.quem.id, out var bk)) { Som3D.Vazio(bk.transform.position, c.quem.gunId); Debug.Log($"[Visual3D] vazio: {c.quem.name} {c.quem.gunId} round {rn} t={c.t:0.0}"); }
            }
            hud.Freeze = freeze;
        }
        readonly Dictionary<int, V2> velViva = new Dictionary<int, V2>();   // última velocidade viva (embalo do ragdoll)
        const float U_NA_MAO = 0.13f;   // instante da animação "granada" com a granada na mão na frente do corpo (antes do pino/isqueiro)
        readonly Dictionary<int, float> preSaque = new Dictionary<int, float>();
        static float Suave(float x) { x = Mathf.Clamp01(x); return x * x * (3 - 2 * x); }
        float freeze; readonly float[] ultTic = new float[2]; int compraIdx, cliqueIdx, tics;   // tempo de compra que falta (s de jogo), compras e cliques já tocados

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
            var q = new Quadro { t = m.t, b = new Pose1[m.agents.Count] };
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
            int i1 = gravado.FindIndex(q => q.t >= r.t); if (i1 < 0) i1 = gravado.Count - 1; int i0 = Mathf.Max(0, i1 - 1);
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
                b.semArma = pp.semArma; b.Silhueta(pp.vivo || i == r.morreu); Costas(m.agents[i].id, b, null);   // (costas só ao vivo)
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
            foreach (var ac in armasChao.Values) ac.go.SetActive(ac.nasceu <= r.t);
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

        Efeitos3D fx;
        readonly HashSet<Match.ShotFx> tirosVistos = new HashSet<Match.ShotFx>();
        readonly HashSet<FireArea> fogosVistos = new HashSet<FireArea>();
        readonly List<FireArea> fogosAcesos = new List<FireArea>();
        readonly List<Vector3> centrosFogo = new List<Vector3>(); bool c4PlantadoVisto;
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
                    var boca = tr.position + dir * (quem.gunId == "awp" ? 0.62f : WeaponStats.Pistola(quem.gunId) ? 0.36f : 0.5f) + Vector3.up * Mathf.Lerp(0.5f, 0.36f, Suave(quem.duck));
                    // o fuzil fica do lado direito do colete (SMG um pouco, pistola no meio); a AWP de luneta vai no olho
                    float lado = quem.gunId == "awp" && quem.scoped ? 0.06f : WeaponStats.Pistola(quem.gunId) ? 0.02f : quem.gunId == "mp9" || quem.gunId == "mac10" ? 0.065f : 0.17f;
                    boca += Vector3.Cross(Vector3.up, dir) * lado;
                    // a ponta do cano de verdade (a peça da arma na mão), quando ela está visível
                    if (b.PontaDaArma(Arma(quem), dir, out var ponta)) boca = ponta;
                    fx.Tiro(boca, dir, Vector3.Cross(Vector3.up, dir), WeaponStats.Pistola(quem.gunId), quem.gunId == "awp");
                    Som3D.Tiro(boca, quem.gunId, quem.id); Som3D.Capsula(boca, quem.gunId); rec.gun = quem.gunId;
                    rec.temBoca = true; rec.boca = boca; rec.dir = dir; rec.pistola = WeaponStats.Pistola(quem.gunId); rec.awp = quem.gunId == "awp";
                    rec.quem = quem.id; b.Disparo(rec.awp ? 1f : rec.pistola ? 0.7f : 0.5f);   // o coice passa pro corpo
                }
                // quem levou: capacete (HS) / colete (tronco) abafam o sangue
                if (s.hit)
                {
                    Agent vit = null; float mv = 24;
                    foreach (var a in m.agents) { float d = (a.pos - s.b).Len; if (a.team != s.team && d < mv) { mv = d; vit = a; } }
                    rec.prot = vit != null && (s.hs ? vit.helmet : vit.armor > 0);
                    if (vit != null) ultAcerto[vit.id] = (s.hs, (ate - de).normalized, m.t);
                }
                if (rec.temBoca) fx.Rastro(rec.boca, ate);
                fx.Impacto(ate, ate - de, s.hit, s.hs, rec.prot);
                if (s.hit) Som3D.Impacto(ate, true, s.hs, rec.prot); else Som3D.Impacto(ate, false, false, false, MaterialEm(ate));
                Som3D.BalaPassando(rec.boca, ate, rec.gun);
                tirosGravados.Add(rec);
                var a3 = new V3(s.a.x, s.a.y, s.za); var b3 = new V3(s.b.x, s.b.y, s.zb);
                foreach (var f in fumacas.Values)
                    if (f.Cruza(a3, b3) && f.Furo(a3, b3, m.t) is Vector3 saiu) fx.TufoSmoke(saiu, ate - de);   // fumaça arrastada pela bala
                if (rec.awp) Tremer(s.a, 0.22f);
            }
            foreach (var h in m.hes) if (h.t > ultHE)
            {
                ultHE = h.t; var hp = MalhaMapa.ParaUnity(h.p.x, h.p.y, h.z);
                fx.HE(hp); Som3D.HE(hp); Tremer(h.p, 0.9f); Empurrar(hp, 2.4f, 6.5f);
                foreach (var f in fumacas.Values) f.Explosao(new V3(h.p.x, h.p.y, h.z), m.t);
            }
            if (m.exploded && !c4Explodiu)
            {
                c4Explodiu = true;
                var bp = MalhaMapa.ParaUnity(m.bombPos.x, m.bombPos.y, m.bombZ);
                fx.ExplosaoC4(bp); Som3D.C4Explode(bp); Tremer(m.bombPos, 2.5f); Empurrar(bp, 9f, 13f);
                if (c4 != null) c4.SetActive(false);
            }
            foreach (var f in m.flashes) if (f.t > ultFlash) { ultFlash = f.t; var fp = MalhaMapa.ParaUnity(f.p.x, f.p.y, f.z); fx.Flash(fp); Som3D.Flash(fp); Tremer(f.p, 0.3f); }
            foreach (var f in m.fires) if (fogosVistos.Add(f))
            {
                var mp = MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z); fx.Molotov(mp);
                bool inc = false; float melhor = 9;
                foreach (var (pp, ehInc, tt) in molotovsSumiram) if (m.t - tt < 0.8f && (pp - mp).sqrMagnitude < melhor) { melhor = (pp - mp).sqrMagnitude; inc = ehInc; }
                // (a granada ainda pode estar desenhada neste quadro: o fogo nasce no mesmo passo em que ela some)
                foreach (var kv in props)
                    if (kv.Key is Match.Nade nn && (nn.l.kind == "molotov" || nn.l.kind == "incendiaria") && kv.Value != null && (kv.Value.transform.position - mp).sqrMagnitude < melhor)
                    { melhor = (kv.Value.transform.position - mp).sqrMagnitude; inc = nn.l.kind == "incendiaria" || (nn.by != null && !nn.by.IsT); }
                Som3D.Molotov(mp, inc);
            }
            molotovsSumiram.RemoveAll(x => m.t - x.t > 1f);
            // fogo aceso: um laço de som por molotov (lista vazia silencia)
            centrosFogo.Clear(); foreach (var f in m.fires) centrosFogo.Add(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z));
            Som3D.Fogos(centrosFogo);
            // C4: plantou agora
            if (m.bombPlanted && !c4PlantadoVisto) { c4PlantadoVisto = true; Som3D.C4Plantou(MalhaMapa.ParaUnity(m.bombPos.x, m.bombPos.y, m.bombZ)); Som3D.Anuncio("bomba_plantada"); }
            // fogo que sumiu ainda queimando = a smoke apagou: fumacinha de apagar em cada chama que estava acesa
            foreach (var f in fogosAcesos)
            {
                if (m.fires.Contains(f)) continue;
                var fp = MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z);
                if (f.age < FireArea.LIFE - 1.05f) { fx.ApagouFogo(f); Som3D.FogoApagou(fp); }   // a smoke apagou
                else Som3D.FogoFim(fp);                                                         // queimou até o fim
            }
            fogosAcesos.Clear(); fogosAcesos.AddRange(m.fires);
            // smoke apagou o fogo / molotov caiu na smoke e nem pegou (Match.fogoApagado): chiado de vapor
            if (m.fogoApagado.Count < vaporesVistos) vaporesVistos = 0;
            for (; vaporesVistos < m.fogoApagado.Count; vaporesVistos++)
            {
                var (vp, vz, _) = m.fogoApagado[vaporesVistos];
                fx.Vapor(MalhaMapa.ParaUnity(vp.x, vp.y, vz), 1.1f); Som3D.FogoApagou(MalhaMapa.ParaUnity(vp.x, vp.y, vz));
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
                    Som3D.Passo(tr.position, Chao(a), !a.IsT);
                }
                passoVisto[a.id] = q;
            }
            fx.Fogos(m.fires, dt);
            fx.Atualizar(dt);
            Quedas(dt);
        }

        // diretor da transmissão: segue o foco do Transmissao.Mirar (duelo > C4 > grupo do TR) com mola (acelera e freia);
        // quando o foco pula pra longe (outro duelo do outro lado do mapa) é corte seco com piscada preta, como um
        // diretor trocando de câmera, em vez de varrer o mapa. Zoom fecha no duelo e abre fora dele.
        Vector2 camSuave, camVel; bool camPronta; float zoomSuave, zoomVel, ultCorte = -9;
        const float CORTE_DIST = 420f;

        // diretor novo (Diretor, em Transmissao.cs): escolhe o protagonista e a caixa do quadro; aqui só a câmera:
        // dentro do plano segue com mola (mais rápida na troca de tiro), troca de plano longe = corte seco com piscada,
        // zoom pela caixa (nunca mais aberto que 2,1x nem mais fechado que o padrão fora da luta). -diretor_velho volta o antigo.
        readonly Diretor diretor = new Diretor();
        /// <summary>Quem o HUD mostra como observado: o escolhido no card ou o protagonista do diretor.</summary>
        public int Observado => seguir >= 0 ? seguir : camFixa.HasValue || Diretor.Velho ? -1 : diretor.prota;
        public Diretor DiretorAtual => diretor;
        void MoverCamera(float dt)
        {
            if (seguir >= 0 || camFixa.HasValue || Diretor.Velho) { MoverCameraVelho(dt); return; }
            var alvo = new Vector2(diretor.centro.x, diretor.centro.y);
            float w = diretor.x1 - diretor.x0, h = diretor.y1 - diretor.y0;
            float precisa = Mathf.Max((w + h * 0.5f) / 1.55f, (h + w * 0.4f) * 0.9f) * MalhaMapa.ESC;
            float z = Mathf.Clamp(precisa * 1.35f + 2.6f, diretor.luta ? zoom * 0.85f : zoom, zoom * 2.1f);
            if (!camPronta) { camSuave = alvo; zoomSuave = z; camPronta = true; }
            if (diretor.corte || (alvo - camSuave).magnitude > 700)
            {
                diretor.corte = false; camSuave = alvo; camVel = Vector2.zero; zoomSuave = z; zoomVel = 0; hud?.Corte();
            }
            camSuave = Vector2.SmoothDamp(camSuave, alvo, ref camVel, diretor.luta ? 0.35f : 0.6f, 520f, dt);
            zoomSuave = Mathf.SmoothDamp(zoomSuave, z, ref zoomVel, 0.8f, 100f, dt);
            cen.alvoJogo = camSuave; cen.enquadrar = zoomSuave; cen.Enquadrar();
            AplicarTremor(dt);
        }

        void MoverCameraVelho(float dt)
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
            long tm = Desempenho.Agora;
            m = pt.Preparar(); rn = pt.r + 1;
            // tempo de compra só no visual (a Sim começa em t = 0): o "go go go" toca no fim do freeze (Passo)
            freeze = Match.FREEZE; compraIdx = cliqueIdx = tics = 0; ultTic[0] = ultTic[1] = -99;
            vozVitoria = null; escadaDegrau.Clear();
            {   // match point: um time a 1 round de fechar (13 no tempo normal; prorrogação MR3: 16, 19, ...)
                int ra = pt.A.placar, rb = pt.B.placar, total = ra + rb, alvo = total < 24 ? 13 : 16 + 3 * ((total - 24) / 6);
                if ((ra == alvo - 1) != (rb == alvo - 1)) Som3D.Hud("match_point");
            }
            noFogo.Clear(); cegoVisto.Clear(); quaseVazio.Clear(); vendeu.Clear();
            antesPasso.Clear(); nadeAntes.Clear();
            if (Desempenho.Ativo) Desempenho.Soma(4, ref tm);
            est.Clear(); rumo.Clear(); sacando.Clear(); mortes.Clear(); ultAcerto.Clear(); ultHp.Clear(); luneta.Clear(); escadaVista.Clear(); antes.Clear(); evento.Clear(); callsVistas = 0; iglCallsVistas[0] = iglCallsVistas[1] = 0; gravado.Clear(); tirosGravados.Clear();
            replay = null; rq0 = rq1 = null; replayTentado = false; fimT = 0; hud?.Replay(false); c4Explodiu = false;
            foreach (var ac in armasChao.Values) Apagar(ac.go);
            armasChao.Clear();
            foreach (var im in imas) if (im.go != null) Apagar(im.go);
            imas.Clear();
            foreach (var g in props.Values) Apagar(g);
            foreach (var (g, _) in latas) Apagar(g);
            latas.Clear(); voos.Clear(); ultTProps = -1; fx?.LimparManchas();
            foreach (var f in fumacas.Values) f.Apagar();
            fumacas.Clear(); idadeFogo.Clear(); vaporesVistos = 0; ragdolls.Clear(); velViva.Clear(); preSaque.Clear();
            props.Clear(); if (c4 != null) c4.SetActive(false); tirosVistos.Clear(); fogosVistos.Clear(); fogosAcesos.Clear(); c4PlantadoVisto = false; animSom.Clear(); arremesso.Clear(); centrosFogo.Clear(); Som3D.Fogos(centrosFogo); passoVisto.Clear(); ultHE = ultFlash = -1;
            foreach (var b in bonecos.Values) if (b) Apagar(b.gameObject);
            bonecos.Clear();
            foreach (var a in m.agents)
            {
                bonecos[a.id] = Boneco3D.Criar(a.IsT ? "tr" : "ct", transform);
                bonecos[a.id].nadeKit = true;   // granada da mão = modelo do kit (MaoGranadas)
            }
            cam = foco = m.agents.Where(a => a.IsT).Aggregate(new V2(0, 0), (s, a) => s + a.pos) / 5;
            focoAte = -9; relogio = 0; fimResta = 3; camPronta = false;
            if (Desempenho.Ativo) Desempenho.Soma(5, ref tm);
        }

        void FechaRound() { long tm = Desempenho.Agora; pt.Fechar(m); if (Desempenho.Ativo) Desempenho.Soma(4, ref tm); if (pt.acabou) { acabou = true; pausado = true; } else NovoRound(); }

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
            bool med = Desempenho.Ativo; long t0 = med ? Desempenho.Agora : 0; simMs = 0;
            Passo(dt, !pausado && !acabou, true);
            if (med) { Desempenho.Soma(1, ref t0); Desempenho.Desconta(1, simMs); Desempenho.Junta(0, simMs); }
            Efeitos();
            if (med) Desempenho.Soma(2, ref t0);
            hud.Atualizar(pt, m, rn, Observado, dt);
            if (med) Desempenho.Soma(3, ref t0);
        }
        double simMs;   // tempo da simulação neste quadro (medição de desempenho)

        static string Arma(Agent a) => a.gunId switch
        {
            "ak" => "AK47", "m4" => "M4A4", "m4s" => "M4A1S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC10",
            "usp" => "USP", "glock" => "Glock", "deagle" => "Deagle",
            "tec9" => "Tec9", "fiveseven" => "FiveSeven", "p250" => "P250", "galil" => "Galil", "famas" => "Famas",
            _ => WeaponStats.Pistola(a.gunId) ? (a.IsT ? "Glock" : "USP") : Transmissao.Padrao(!a.IsT)
        };

        // corpo andado por ciclo de passo (unidades do jogo) das passadas do exportar3d.py: 2 x passada / fração no chão x 25
        const float CICLO_DEVAGAR = 2 * 0.12f / 0.5f * 25, CICLO_AGACHADO = 2 * 0.1f / 0.5f * 25;

        readonly Dictionary<int, (float ini, float ate)> sacando = new Dictionary<int, (float, float)>();
        readonly Dictionary<int, int> ultHp = new Dictionary<int, int>();
        readonly Dictionary<int, float> luneta = new Dictionary<int, float>();
        readonly HashSet<int> escadaVista = new HashSet<int>();
        class Antes { public string gun; public int nades, troca, hp = 100; public bool bomba, noAr, vivo = true; public float quedaDe; }
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
            float h = Mathf.Lerp(0.58f, 0.44f, Suave(a.duck));
            var pe = b.transform.position; var fim = MalhaMapa.ParaUnity(f.x, f.y, a.z) + Vector3.up * h;
            var plano = fim - pe; plano.y = 0; plano = plano.sqrMagnitude > 1e-6f ? plano.normalized : b.transform.forward;
            fim.y += Mathf.Tan(b.pitch * Mathf.Deg2Rad) * (len * MalhaMapa.ESC);
            // sai da ponta do cano da AWP (a peça de verdade na mão) e vai até onde a bala pega
            var boca = b.PontaDaArma("AWP", plano, out var pc) ? pc : pe + Vector3.up * h + plano * 0.62f;
            fx.Laser(a.id, boca, fim, a.Speed < 5 ? 1f : 0.35f);
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
                if (l < 3 || l > 20 || l >= melhor || Mathf.Abs(z - a.z) > 10) return;   // só chegando nela (o E do CS)
                V2 u = d / l;
                if (u.Dot(anda) < 0.8f && l > 14) return;               // não está indo pra lá
                if (encara && u.Dot(olha) < 0.9f) return;               // arma: tem que estar encarando (~25°)
                melhor = l; pv = Mathf.Atan2(z - (a.z + NavGrid.EYE), l) * Mathf.Rad2Deg;
            }
            // só ARMA (pega com E, trocando a do slot): granada, kit e C4 se pegam passando por cima, sem olhar pro chão
            // (usuário: "no CS não dá pra usar o E, então é só passando em cima")
            foreach (var d in m.drops) Ver(d.pos, d.z, true);
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
                    // trocou de arma no chão (o "E" do CS): a dele sai jogada pra frente e a nova vem sacando, olhando pra baixo
                    if (a.gunId != o.gun && a.trocouArma == o.troca)   // (trocou pelo Q = só sacar, não é pegar do chão)
                    {
                        evento[a.id] = ("trocar", m.t, 0.6f); trocouEm[a.id] = m.t;
                        Debug.Log($"[Visual3D] trocou arma no chão: {a.name} {o.gun} -> {a.gunId} round {Round} t={m.t:0.0}");
                    }
                    // pegar item do chão não tem pose (usuário): o item vem como ímã (Imas). Largar a C4 também sem pose.
                    var pe = MalhaMapa.ParaUnity(a.pos.x, a.pos.y, a.z);
                    if (a.gunId != o.gun && a.trocouArma == o.troca) Som3D.Pegar(pe);
                    if (a.hasBomb && !o.bomba) Som3D.C4Pegar(pe); else if (!a.hasBomb && o.bomba && !m.bombPlanted) Som3D.C4Largar(pe);
                    if (!o.noAr && a.noAr) Som3D.Pulo(pe);
                    if (o.noAr && !a.noAr) { Som3D.Aterrissa(pe, Chao(a)); if (a.hp < o.hp) Som3D.Queda(pe); }   // perdeu vida ao cair = dano de queda
                    if (o.noAr && !a.noAr && o.quedaDe - a.z > 6) evento[a.id] = ("aterrissar", m.t, 0.2f);
                    // pousou (pulo de deslocamento, bunny hop ou queda): poeirinha nos dois pés
                    if (o.noAr && !a.noAr && bonecos.TryGetValue(a.id, out var bp))
                    {
                        var tr = bp.transform; var v = new Vector3(a.vel.x, 0, -a.vel.y) * MalhaMapa.ESC;
                        fx.Passo(tr.position + tr.right * 0.07f, v); fx.Passo(tr.position - tr.right * 0.07f, v);
                    }
                }
                o.gun = a.gunId; o.troca = a.trocouArma; o.nades = a.nades.Count; o.bomba = a.hasBomb; o.hp = a.hp; if (a.noAr && !o.noAr) o.quedaDe = a.quedaDe; o.noAr = a.noAr;
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
        // último tiro que cada um levou: na cabeça? de que direção (Unity)? quando — o tranco e o ragdoll reagem ao ponto
        readonly Dictionary<int, (bool hs, Vector3 dir, float t)> ultAcerto = new Dictionary<int, (bool, Vector3, float)>();
        readonly Dictionary<int, Ragdoll3D> ragdolls = new Dictionary<int, Ragdoll3D>();

        // ------------------------------------------------------------------ itens no chão com física
        // arma solta, granada largada pro colega e C4 caído: nascem na mão de quem soltou e caem de verdade — gravidade,
        // giro, quique com perda, rolam e param pelo atrito, batem em parede (o piso do lado mais alto que o item = parede,
        // a velocidade daquele eixo volta), e no fim deitam de lado. Explosão (HE, C4) empurra pra longe e faz voar.
        class ArmaCaindo { public GameObject go; public Vector3 p, v, w; public Quaternion r; public bool parada, soou; public float deitar, nasceu, meiaAltura = 0.035f, meioComp; public string gun;
            // granada do morto: a Sim rola ela a cada passo (ItemChao.pos/vel, dá pra pegar rolando, como no CS); o desenho
            // segue a posição atual suavizada e gira pela distância andada
            public bool rola, caixa; public int quiques; public Match.ItemChao item;
            // amostra dos vértices do item (espaço dele, sem escala) e a escala: o chão apoia no ponto mais baixo DE VERDADE
            // depois do giro (antes era uma meia altura fixa: lata deitada flutuava, arma inclinada afundava)
            public Vector3[] pts; public Vector3 esc = Vector3.one; public Vector3 eixo = Vector3.right; public float giro; }
        readonly Dictionary<object, ArmaCaindo> armasChao = new Dictionary<object, ArmaCaindo>();
        const string C4_CAIDO = "c4 caído";

        ArmaCaindo NovoItem(GameObject go, Vector3 mao, Vector3 empurra, float yaw)
        {
            go.transform.SetParent(transform, false);
            // meio comprimento do item (a ponta do cano/coronha não pode entrar na parede)
            float meio = 0.05f;
            foreach (var r in go.GetComponentsInChildren<Renderer>()) meio = Mathf.Max(meio, (r.bounds.max - go.transform.position).magnitude, (r.bounds.min - go.transform.position).magnitude);
            // vértices do item no espaço da raiz dele (até ~400, pulando), pro apoio no chão
            var pts = new List<Vector3>();
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>()) Amostra(pts, mf.sharedMesh, mf.transform, go.transform);
            // malha com esqueleto (o kit): os vértices na pose atual (BakeMesh), não os da pose de bind
            foreach (var sr in go.GetComponentsInChildren<SkinnedMeshRenderer>()) { var tmp = new Mesh(); sr.BakeMesh(tmp, false); Amostra(pts, tmp, sr.transform, go.transform); Apagar(tmp); }
            return new ArmaCaindo
            {
                go = go, nasceu = m.t, p = mao, meioComp = Mathf.Min(meio, 0.6f), pts = pts.ToArray(), esc = go.transform.localScale,
                v = empurra + new Vector3(UnityEngine.Random.Range(-0.4f, 0.4f), UnityEngine.Random.Range(1.3f, 2.0f), UnityEngine.Random.Range(-0.4f, 0.4f)),
                r = Quaternion.Euler(0, yaw, 0),
                w = new Vector3(UnityEngine.Random.Range(-400, 400), UnityEngine.Random.Range(-250, 250), UnityEngine.Random.Range(200, 500))
            };
        }

        static void Amostra(List<Vector3> pts, Mesh me, Transform t, Transform raiz)
        {
            if (me == null || !me.isReadable) return;
            var vs = me.vertices; int passo = Mathf.Max(1, vs.Length / 300);
            var esc = raiz.lossyScale; var inv = new Vector3(1 / Mathf.Max(1e-6f, esc.x), 1 / Mathf.Max(1e-6f, esc.y), 1 / Mathf.Max(1e-6f, esc.z));
            for (int i = 0; i < vs.Length; i += passo)
            {
                // ponto no mundo -> relativo à raiz, desfazendo giro e escala dela
                var w = t.TransformPoint(vs[i]);
                pts.Add(Vector3.Scale(Quaternion.Inverse(raiz.rotation) * (w - raiz.position), inv));
            }
        }

        /// <summary>Quanto o item desce abaixo do próprio centro com esse giro (o ponto mais baixo da malha): o centro fica
        /// a essa altura do chão. Sem malha legível, a meia altura antiga.</summary>
        static float Fundo(ArmaCaindo ac, Quaternion r)
        {
            if (ac.pts == null || ac.pts.Length == 0) return ac.meiaAltura;
            float mn = 0;
            foreach (var pt in ac.pts) { float y = (r * Vector3.Scale(ac.esc, pt)).y; if (y < mn) mn = y; }
            return -mn;
        }

        /// <summary>Item jogado da mão (o G do CS): arco até onde o Sim pôs; o 1º baque a 3/4 do caminho, o resto escorrega.</summary>
        static void Arco(ArmaCaindo ac, Vector3 mao, Vector3 alvo)
        {
            float vy = 1.6f, h = Mathf.Max(0.05f, mao.y - alvo.y), tq = (vy + Mathf.Sqrt(vy * vy + 24f * h)) / 12f;
            var hor = alvo - mao; hor.y = 0;
            ac.v = hor * (0.75f / tq) + Vector3.up * vy;
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
                // dropou de propósito (pro colega na compra, ou trocou no chão): Drop.de diz quem; sem ele, o dono é quem está
                // mais perto (arma de quem morreu cai do corpo)
                var dono = d.de ?? m.agents.Where(a => (a.pos - d.pos).Len < 40).OrderBy(a => (a.pos - d.pos).Len).FirstOrDefault();
                bool jogou = dono != null && dono.alive && bonecos.ContainsKey(dono.id)
                             && (d.de != null || trocouEm.TryGetValue(dono.id, out float tt) && m.t - tt < 0.5f);
                if (jogou) { var tr = bonecos[dono.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; yaw = tr.eulerAngles.y; }
                var nova = NovoItem(Armas3D.Objeto(Boneco3D.Carregar(dono != null && !dono.IsT ? "ct" : "tr"), nome, Boneco3D.matCorpo), mao, emp, yaw);
                if (jogou) Arco(nova, mao, MalhaMapa.ParaUnity(d.pos.x, d.pos.y, d.z));
                nova.gun = d.gun;
                armasChao[d] = nova;
            }
            foreach (var it in m.itensChao)
            {
                if (armasChao.ContainsKey(it)) continue;
                var (mao, emp, yaw) = MaoDe(it.pos, it.z);
                // dropou pro colega: sai da mão de quem dropou num arco até onde o Sim pôs (o colega vai lá pegar)
                bool deMao = it.de != null && it.de.alive && bonecos.ContainsKey(it.de.id);
                if (deMao) { var tr = bonecos[it.de.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; yaw = tr.eulerAngles.y; }
                // granadas de quem morreu: saem do corpo (altura do peito) e se espalham num arco até onde o Sim pôs cada uma
                bool doMorto = !deMao && it.morto != null && bonecos.ContainsKey(it.morto.id);
                if (doMorto) { var tr = bonecos[it.morto.id].transform; mao = tr.position + Vector3.up * 0.35f; yaw = UnityEngine.Random.Range(0f, 360f); }
                var gi = Granada(it.nade, it.time == Team.T); gi.transform.localScale = Vector3.one * ESC_GRANADA_CHAO;
                var ac = NovoItem(gi, mao, emp * 0.6f, yaw); ac.meiaAltura = 0.03f * ESC_GRANADA_CHAO;
                if (doMorto)   // sai do corpo (mao) e segue o rolo da Sim
                {
                    ac.rola = true; ac.item = it; ac.caixa = it.nade == "kit";
                    float ang = UnityEngine.Random.Range(0f, Mathf.PI * 2); ac.eixo = new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang));
                }
                else if (deMao || doMorto) Arco(ac, mao, MalhaMapa.ParaUnity(it.pos.x, it.pos.y, it.z));
                armasChao[it] = ac;
            }
            if (m.bombDropped && !m.bombPlanted && !armasChao.ContainsKey(C4_CAIDO))
            {
                var (mao, emp, yaw) = MaoDe(m.droppedBomb, NavGrid.ZAt(m.droppedBomb));
                var ac = NovoItem(Armas3D.Prop(Boneco3D.Carregar("tr"), "plantar", 0.97f, Boneco3D.matCorpo), mao, emp * 0.6f, yaw); ac.meiaAltura = 0.045f;
                // largou de propósito (m.bombDe agora, vivo): sai da mão dele num arco até o lugar do Sim; morreu: cai do corpo
                var dono = m.bombDe != null && m.bombDe.alive && m.t - m.bombDeT < 1f && bonecos.ContainsKey(m.bombDe.id) ? m.bombDe : null;
                if (dono != null)
                {
                    var tr = bonecos[dono.id].transform; mao = tr.position + Vector3.up * 0.5f + tr.forward * 0.15f; ac.p = mao;
                    Arco(ac, mao, MalhaMapa.ParaUnity(m.droppedBomb.x, m.droppedBomb.y, NavGrid.ZAt(m.droppedBomb)));
                }
                armasChao[C4_CAIDO] = ac;
            }
            // pegaram do chão: o item é puxado como ímã pra quem pegou e some nele (usuário: sem pose de pegar, qualquer item)
            foreach (var k in armasChao.Keys.ToList())
            {
                bool existe = labItens.Contains(k) || (k is Match.Drop dr ? m.drops.Contains(dr) : k is Match.ItemChao ic ? m.itensChao.Contains(ic) : m.bombDropped && !m.bombPlanted);
                if (existe) continue;
                Agent quem = k is Match.Drop d2 ? d2.pegouPor : k is Match.ItemChao i2 ? i2.pegouPor
                           : k == (object)C4_CAIDO && !m.bombPlanted ? (m.c4PegouPor ?? m.agents.FirstOrDefault(a => a.alive && a.hasBomb)) : null;
                var go = armasChao[k].go; armasChao.Remove(k);
                if (quem != null && quem.alive && bonecos.ContainsKey(quem.id) && go != null)
                    imas.Add(new Ima { go = go, quem = quem.id, de = go.transform.position, esc = go.transform.localScale });
                else Apagar(go);
            }
            foreach (var ac in armasChao.Values) Fisica(ac, dt);
            Imas(dt);
        }

        // ímã da coleta: o item sai do chão, acelera até o peito de quem pegou, encolhe e some (0,2 s)
        class Ima { public GameObject go; public int quem; public Vector3 de, esc; public float t, espera; }
        readonly List<Ima> imas = new List<Ima>();
        const float IMA_DUR = 0.2f;
        void Imas(float dt)
        {
            for (int i = imas.Count - 1; i >= 0; i--)
            {
                var im = imas[i];
                if (im.go == null || !bonecos.TryGetValue(im.quem, out var b)) { if (im.go != null) Apagar(im.go); imas.RemoveAt(i); continue; }
                var alvo = b.transform.position + Vector3.up * 0.32f;
                // a regra da Sim pega a 12; o desenho espera no chão até ele chegar a ~6 ("quase por cima", usuário), no
                // máximo 0,6 s, e aí puxa
                var dh = alvo - im.de; dh.y = 0;
                if (im.t == 0 && im.espera < 0.6f && dh.magnitude > 6f * MalhaMapa.ESC) { im.espera += dt; continue; }
                im.t += dt;
                float k = Mathf.Clamp01(im.t / IMA_DUR);
                if (k >= 1) { Apagar(im.go); imas.RemoveAt(i); continue; }
                float e = k * k;   // começa devagar e acelera (puxado)
                var pos = Vector3.Lerp(im.de, alvo, e); pos.y += Mathf.Sin(k * Mathf.PI) * 0.08f;   // um arquinho
                im.go.transform.position = pos;
                im.go.transform.localScale = im.esc * Mathf.Lerp(1f, 0.25f, e);
            }
        }

        void Fisica(ArmaCaindo ac, float dt)
        {
            if (ac.rola)
            {
                // cai do corpo e rola junto da Sim: a posição dela anda a cada passo (32 Hz); aqui segue suavizada (sem degrau),
                // na altura do chão, girando pelo chão conforme anda
                var it = ac.item;
                var alvo = MalhaMapa.ParaUnity(it.pos.x, it.pos.y, NavGrid.ZAt(it.pos));
                float idade = m.t - it.t;
                if (idade < 0.18f) alvo.y = Mathf.Lerp(ac.p.y, alvo.y, Mathf.Clamp01(idade / 0.18f));   // a queda do peito até o chão
                var novo = Vector3.Lerp(ac.p, alvo, 1 - Mathf.Exp(-22f * dt));
                var passo = novo - ac.p; passo.y = 0; float dist = passo.magnitude;
                if (ac.caixa)
                {
                    // caixa (kit) não rola (usuário): uma virada na queda e depois só escorrega, chapada no chão
                    if (idade < 0.25f) ac.r = Quaternion.AngleAxis(180f * dt / 0.25f, ac.eixo) * ac.r;
                    else
                    {
                        var fr = Vector3.ProjectOnPlane(ac.r * Vector3.forward, Vector3.up); if (fr.sqrMagnitude < 1e-6f) fr = Vector3.forward;
                        var chapada = Quaternion.LookRotation(fr, Vector3.Dot(ac.r * Vector3.up, Vector3.up) >= 0 ? Vector3.up : Vector3.down);
                        ac.r = Quaternion.Slerp(ac.r, chapada, 1 - Mathf.Exp(-14f * dt));
                    }
                }
                else
                {
                    // cilindro (granada) deita de lado com o eixo atravessado no movimento e rola em volta dele
                    if (dist > 1e-5f)
                    {
                        var at = Vector3.Cross(Vector3.up, passo / dist);
                        if (Vector3.Dot(at, ac.eixo) < 0) at = -at;
                        ac.eixo = Vector3.Slerp(ac.eixo, at, 1 - Mathf.Exp(-10f * dt)).normalized;
                        float sinal = Mathf.Sign(Vector3.Dot(Vector3.Cross(Vector3.up, passo / dist), ac.eixo));
                        ac.giro += sinal * dist / Mathf.Max(0.01f, ac.meiaAltura) * Mathf.Rad2Deg;
                    }
                    var deitada = Quaternion.AngleAxis(ac.giro, ac.eixo) * Quaternion.FromToRotation(Vector3.up, ac.eixo);
                    ac.r = idade < 0.18f ? Quaternion.Slerp(ac.r, deitada, 1 - Mathf.Exp(-20f * dt)) : deitada;
                }
                novo.y = Mathf.Max(novo.y, Efeitos3D.Piso(novo) + Fundo(ac, ac.r));   // apoiado pelo ponto mais baixo (não flutua nem afunda)
                if (idade >= 0.18f) novo.y = Efeitos3D.Piso(novo) + Fundo(ac, ac.r);
                ac.p = novo; ac.go.transform.SetPositionAndRotation(ac.p, ac.r);
                // parou na Sim e o desenho chegou: fica como está (deitada/chapada)
                if (it.vel.x == 0 && it.vel.y == 0 && (novo - alvo).sqrMagnitude < 1e-6f) { ac.rola = false; ac.parada = true; ac.v = Vector3.zero; ac.w = Vector3.zero; }
                return;
            }
            if (!ac.parada)
            {
                ac.v.y -= 12f * dt;
                var novo = ac.p + ac.v * dt;
                // parede: varre cada eixo do passo (de meio em meio voxel, parede fina não passa) -> bate e volta naquele eixo
                float alto = ac.p.y + 0.06f;
                var vx = Efeitos3D.Varre(ac.p, new Vector3(novo.x, ac.p.y, ac.p.z), alto);
                if (vx.x != novo.x) { ac.v.x = -ac.v.x * 0.4f; novo.x = vx.x; ac.w *= 0.7f; }
                var vz = Efeitos3D.Varre(new Vector3(novo.x, ac.p.y, ac.p.z), new Vector3(novo.x, ac.p.y, novo.z), alto);
                if (vz.z != novo.z) { ac.v.z = -ac.v.z * 0.4f; novo.z = vz.z; ac.w *= 0.7f; }
                ac.p = novo;
                ac.r = Quaternion.Euler(ac.w * dt) * ac.r;
                float piso = Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r);
                if (ac.p.y < piso)
                {
                    ac.p.y = piso;
                    if (Mathf.Abs(ac.v.y) > 1.2f) fx.Passo(ac.p, Vector3.zero);   // poeirinha do baque
                    // 1º baque: o clipe já traz baque + 1 quique; cada quique seguinte da física soa mais baixo
                    if (ac.gun != null) { if (!ac.soou) { ac.soou = true; Som3D.ArmaCaiu(ac.p, ac.gun); } else Som3D.ArmaQuicou(ac.p, ac.gun, ++ac.quiques); }
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
                    // arma não rola (usuário): no chão ela já deita de lado e só gira chapada em volta da vertical enquanto
                    // escorrega (giro dos outros eixos fazia ela "rolar" como barril)
                    if (ac.gun != null)
                    {
                        ac.w = new Vector3(0, ac.w.y, 0);
                        var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) fw = Vector3.forward;
                        ac.r = Quaternion.Slerp(ac.r, Quaternion.LookRotation(fw.normalized) * Quaternion.Euler(0, 0, 90), 1 - Mathf.Exp(-14f * dt));
                        ac.p.y = Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r);
                    }
                    if (new Vector3(ac.v.x, 0, ac.v.z).magnitude < 0.08f) { ac.parada = true; ac.deitar = 0; }
                }
            }
            else if (ac.deitar < 1)
            {
                // assenta deitado de lado, mantendo pra onde o cano aponta
                ac.deitar = Mathf.Min(1, ac.deitar + dt * 5);
                var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) fw = Vector3.forward;
                ac.r = Quaternion.Slerp(ac.r, Quaternion.LookRotation(fw.normalized) * Quaternion.Euler(0, 0, 90), ac.deitar);
                ac.p.y = Mathf.Lerp(ac.p.y, Efeitos3D.Piso(ac.p) + Fundo(ac, ac.r), ac.deitar);
            }
            ForaDaParede(ac);
            ac.go.transform.SetPositionAndRotation(ac.p, ac.r);
        }

        /// <summary>As pontas do item (cano e coronha, deitado no chão) não entram na parede: empurra pro lado livre.</summary>
        void ForaDaParede(ArmaCaindo ac)
        {
            var fw = ac.r * Vector3.forward; fw.y = 0; if (fw.sqrMagnitude < 1e-4f) return; fw.Normalize();
            var lado = new Vector3(fw.z, 0, -fw.x);
            float alto = ac.p.y - ac.meiaAltura + 0.04f, passo = MalhaMapa.ESC * 0.25f;
            foreach (var eixo in new[] { fw, -fw, lado, -lado })
            {
                float L = Mathf.Abs(Vector3.Dot(eixo, fw)) > 0.5f ? ac.meioComp : ac.meiaAltura * 2;
                for (int k = 0; k < 12 && Efeitos3D.Piso(ac.p + eixo * L) > alto && Efeitos3D.Piso(ac.p) <= alto; k++) ac.p -= eixo * passo;
            }
        }

        // interpolação: a simulação anda a 32 passos/s e o jogo desenha a 60+; o boneco fica entre a posição de antes do
        // último passo e a atual, na fração do tempo de desenho (relogio) dentro desse passo. Sem isso ele andava aos
        // trancos (quadro sem passo + quadro com dois) e parecia rodar a 20 fps.
        readonly Dictionary<int, (V2 p, float z, float mira)> antesPasso = new Dictionary<int, (V2, float, float)>();
        void GuardarAntes()
        {
            foreach (var a in m.agents) antesPasso[a.id] = (a.pos, a.z, a.aim);
            foreach (var n in m.flying) nadeAntes[n] = new Vector3(n.x, n.y, n.z);
        }
        // granada voando: a mesma interpolação entre passos (sem isso ela pulava a 32 Hz)
        readonly Dictionary<Match.Nade, Vector3> nadeAntes = new Dictionary<Match.Nade, Vector3>();
        Vector3 NadeVisivel(Match.Nade n)
        {
            var agora = new Vector3(n.x, n.y, n.z);
            if (!nadeAntes.TryGetValue(n, out var antes)) return agora;
            return Vector3.Lerp(antes, agora, Mathf.Clamp01(1 - (m.t - relogio) / Match.DT));
        }
        (V2 p, float z, float mira) Visivel(Agent a)
        {
            if (!antesPasso.TryGetValue(a.id, out var v)) return (a.pos, a.z, a.aim);
            float k = Mathf.Clamp01(1 - (m.t - relogio) / Match.DT);
            if ((a.pos - v.p).Len > 30) return (a.pos, a.z, a.aim);   // teleporte (round novo, escada): sem arrastar
            float d = Mathf.DeltaAngle(v.mira * Mathf.Rad2Deg, a.aim * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return (v.p + (a.pos - v.p) * k, Mathf.Lerp(v.z, a.z, k), v.mira + d * k);
        }

        readonly Dictionary<int, string> animSom = new Dictionary<int, string>();   // última animação que tocou som
        // sons por estado: degrau da escada já tocado, quem está pisando no fogo, quem já ficou cego (zumbido do seguido)
        readonly Dictionary<int, int> escadaDegrau = new Dictionary<int, int>();
        readonly HashSet<int> noFogo = new HashSet<int>(), cegoVisto = new HashSet<int>(), quaseVazio = new HashSet<int>(), vendeu = new HashSet<int>();
        readonly List<Vector3> molotovsNoAr = new List<Vector3>();
        readonly List<(Vector3 p, bool inc, float t)> molotovsSumiram = new List<(Vector3, bool, float)>();   // de quem é o fogo novo
        /// <summary>Material onde a bala bateu (ponto Unity) pro som do impacto: caixa/carroça = madeira, piso pelo MapData.fmat,
        /// parede = pedra.</summary>
        static string MaterialEm(Vector3 u)
        {
            float gx = u.x / MalhaMapa.ESC, gy = -u.z / MalhaMapa.ESC, gz = u.y / MalhaMapa.ESC;
            int x = Mathf.Clamp((int)gx, 0, MapData.NX - 1), y = Mathf.Clamp((int)gy, 0, MapData.NY - 1);
            foreach (var c in MapData.covers)
                if (x >= c.r.x0 && x < c.r.x1 && y >= c.r.y0 && y < c.r.y1 && (c.kind.StartsWith("crate") || c.kind == "cart")) return "madeira";
            float chao = NavGrid.ZAt(new V2(gx, gy));
            if (gz <= chao + 1.5f) return MapData.fmat[x, y] switch { MapData.Floor.Dirt => "terra", MapData.Floor.Wood => "madeira", MapData.Floor.Cart => "madeira", _ => "pedra" };
            return "pedra";
        }

        /// <summary>Chão debaixo do pé pro som do passo: material do piso do mapa; em cima de caixa/carroça = madeira.</summary>
        static string Chao(Agent a)
        {
            int x = Mathf.Clamp((int)a.pos.x, 0, MapData.NX - 1), y = Mathf.Clamp((int)a.pos.y, 0, MapData.NY - 1);
            if (MapData.coverH[x, y] > 0 && a.z >= MapData.GZ + MapData.coverH[x, y] - 1) return "madeira";
            if (Marmore()[x, y] && MapData.fmat[x, y] == MapData.Floor.Stone) return "marmore";   // palácio: piso de mármore (só som)
            return MapData.fmat[x, y] switch { MapData.Floor.Dirt => "terra", MapData.Floor.Wood => "madeira", MapData.Floor.Cart => "madeira", _ => "pedra" };
        }
        static bool[,] marmore;
        /// <summary>Células das áreas do palácio (Floor.Stone na Sim; o som de passo/pulo é de mármore).</summary>
        static bool[,] Marmore()
        {
            if (marmore != null) return marmore;
            marmore = new bool[MapData.NX, MapData.NY];
            var nomes = new HashSet<string> { "Hall_palacio", "Esc_palacio", "Palacio", "Palacio_sala", "Palacio_corredor", "Sacada_A" };
            foreach (var s in MapData.areas)
            {
                if (!nomes.Contains(s.name)) continue;
                int x0 = Mathf.Max(0, s.box.x0), x1 = Mathf.Min(MapData.NX, s.box.x1), y0 = Mathf.Max(0, s.box.y0), y1 = Mathf.Min(MapData.NY, s.box.y1);
                for (int x = x0; x < x1; x++) for (int y = y0; y < y1; y++) if (s.has == null || s.has(x, y)) marmore[x, y] = true;
                Debug.Log($"[Visual3D] mármore: {s.name} ({x0},{y0})-({x1},{y1})");
            }
            return marmore;
        }
        // jeito do arremesso em curso (sufixo da animação: "", "_meio", "_baixo") e se é jump throw
        readonly Dictionary<int, (string suf, bool pula)> arremesso = new Dictionary<int, (string, bool)>();
        void Bonecos(float dt)
        {
            foreach (var a in m.agents)
            {
                var b = bonecos[a.id];
                if (!est.TryGetValue(a.id, out var e)) { e = new Transmissao.Anim { ultPos = a.pos }; est[a.id] = e; }
                if (a.brain.action != e.acao)
                {
                    if (e.acao == "granada" && m.t - e.acaoIni >= e.acaoDur * 0.85f && a.alive)
                        evento[a.id] = ("granada_fim" + (arremesso.TryGetValue(a.id, out var jt) ? jt.suf : ""), m.t, 0.4f);
                    e.acao = a.brain.action; e.acaoIni = m.t; e.acaoDur = Mathf.Max(0.2f, a.brain.ActionEnd - m.t);
                }
                var vis = Visivel(a);
                float dist = (vis.p - e.ultPos).Len; e.ultPos = vis.p;
                string arma = Arma(a), anim; float u = 0; bool porTempo = false;
                // arrependeu da compra (Match.vendas): segura a SMG desde o freeze até vender; no instante some, o "$" sobe
                // da etiqueta e ele saca a pistola
                if (a.alive && !a.faca && replay == null)
                    foreach (var vd in m.vendas)
                    {
                        if (vd.quem != a) continue;
                        if (m.t < vd.t) { string sm = vd.item == "mac10" ? "MAC10" : vd.item == "mp9" ? "MP9" : null; if (sm != null && b.Tem("mira_" + sm)) arma = sm; }
                        else if (vendeu.Add(a.id))
                        {
                            hud.Vendeu(m.agents.IndexOf(a), vd.item);
                            evento[a.id] = ("trocar", m.t, 0.45f);
                            if (seguir == a.id) Som3D.Hud("compra", 0.35f);
                            Debug.Log($"[Visual3D] vendeu {vd.item}: {a.name} round {rn} t={m.t:0.0}");
                        }
                    }
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
                string marcha = anda && !a.faca && !naEscada && !a.noAr ? (a.duck > 0.5f ? "agachado" : a.Speed < a.MaxSpeed * 0.62f ? "devagar" : "andar") : null;
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
                        bool hsMorte = ultAcerto.TryGetValue(a.id, out var ua) && ua.hs && m.t - ua.t < 0.6f;
                        // embalo: velocidade de quando ainda estava vivo (a Sim pode zerar na morte)
                        velViva.TryGetValue(a.id, out var vv);
                        rg = new Ragdoll3D(b, mo.dir, mo.forca, a.id * 31 + rn, hsMorte, new Vector3(vv.x, 0, -vv.y) * MalhaMapa.ESC); ragdolls[a.id] = rg;
                        Som3D.Parar(a.id); Som3D.CorpoCaiu(b.transform.position);
                    }
                    rg.Passo(dt); Costas(a.id, b, null);
                    b.noChao = false; b.Silhueta(false); ultHp[a.id] = a.hp;
                    continue;
                }
                velViva[a.id] = a.vel;
                // escada de mão: pés degrau a degrau pela altura subida (2 degraus de 4 = 8 unidades por ciclo), arma nas costas
                if (naEscada)
                {
                    anim = "escada"; u = (a.z - MapData.GZ) / 8f;
                    // um som por degrau (2 degraus por ciclo de 8 de altura)
                    int dg = Mathf.FloorToInt(u * 2);
                    if (escadaDegrau.TryGetValue(a.id, out int dg0) && dg0 != dg) Som3D.Escada(b.transform.position);
                    escadaDegrau[a.id] = dg;
                    if (escadaVista.Add(a.id)) Debug.Log($"[Visual3D] escada: {a.name} round {rn} t={m.t:0.0}");
                }
                else if (acao == "plantar" || acao == "fake plant") { anim = "plantar"; u = (m.t - e.acaoIni) / (acao == "plantar" ? e.acaoDur : 3.2f); }
                else if (acao == "desarmar" || acao == "fake defuse") { anim = "desarmar"; u = m.t - e.acaoIni; porTempo = true; }
                else if (acao == "granada")
                {
                    string g = a.brain.AcaoGranada ?? "smoke";
                    // jeito do CS2 pela força que o Sim escolheu: esquerdo (1) por cima, os dois (0,5) meia força, direito por baixo;
                    // jump throw (Pulo/CorrePulo): a Sim pula na soltura (as pernas fazem a pose de pulo)
                    var pl = a.brain.PlanoGranada;
                    string suf = pl == null || pl.Value.forca >= 1 ? "" : pl.Value.forca >= 0.5f ? "_meio" : "_baixo";
                    bool pula = pl != null && (pl.Value.tipo == Arremesso.Tipo.Pulo || pl.Value.tipo == Arremesso.Tipo.CorrePulo);
                    arremesso[a.id] = (suf, pula);
                    anim = "granada_" + (g == "incendiaria" ? "molotov" : g) + suf; u = (m.t - e.acaoIni) / e.acaoDur;
                    // chegou na lineup com ela já na mão (pré-saque): o arremesso continua de onde a mão estava, sem sacar de novo
                    if (preSaque.TryGetValue(a.id, out float tn) && e.acaoIni - tn < 0.3f) u = U_NA_MAO + (1 - U_NA_MAO) * u;
                    if (!b.Tem(anim)) anim = "granada_" + (g == "incendiaria" ? "molotov" : g);
                }
                // pré-saque (Agent.nadeNaMao): andando até a lineup com a granada na mão, arma guardada — a pose de segurar do
                // começo da ação "granada" (sai da arma em ~0,35 s e fica). Se aparece inimigo a Sim guarda e o saque da arma vem
                else if (!string.IsNullOrEmpty(a.nadeNaMao) && !a.faca)
                {
                    string g = a.nadeNaMao;
                    anim = "granada_" + (g == "incendiaria" ? "molotov" : g);
                    u = U_NA_MAO * Mathf.Clamp01((m.t - a.nadeNaMaoDesde) / 0.35f);
                    preSaque[a.id] = m.t;
                }
                else if (ev.anim != null && ev.anim.StartsWith("granada_fim"))
                {
                    anim = b.Tem(ev.anim) ? ev.anim : "granada_fim"; u = (m.t - ev.ini) / ev.dur;
                    // a arma ficou guardada no fim do arremesso: emenda o saque (fuzil por cima do ombro, pistola do coldre)
                    if (u >= 1 && !a.faca)
                    {
                        bool costas = !WeaponStats.Pistola(a.gunId) && b.Tem("sacar_costas_" + arma);
                        evento[a.id] = (costas ? "costas" : "saque", m.t, costas ? 0.7f : 0.45f);
                        anim = (costas ? "sacar_costas_" : "sacar_") + arma; u = 0;
                    }
                }
                else if (ev.anim == "saque" && !a.faca) { anim = "sacar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "costas" && !a.faca) { anim = "sacar_costas_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (a.Blind(m.t)) { anim = b.Tem("cego_" + arma) ? "cego_" + arma : "cego"; u = m.t; porTempo = true; }
                else if (ev.anim == "pegar" && !a.faca) { anim = "pegar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "trocar" && !a.faca) { anim = "sacar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (!a.faca && a.gun.Reloading) { anim = "recarga_" + arma; u = 1 - a.gun.reloadLeft / a.gun.s.Reload; }
                else if (!a.faca && m.t - a.lastShotT < tiroDur) { anim = "tiro_" + arma; u = (m.t - a.lastShotT) / tiroDur; }
                else if (a.noAr) anim = (a.faca ? "faca_" : "") + (a.vz > 0 ? "no_ar_sobe" : "no_ar_cai") + (a.faca ? "" : "_" + arma);
                // meio cego (flash de lado): aba com a mão na testa, rosto virado, arma ainda na frente
                else if (!a.faca && a.MeioCego(m.t) && b.Tem("meio_cego_" + arma)) { anim = "meio_cego_" + arma; u = m.t; porTempo = true; }
                else if (a.faca) { anim = anda ? "faca_andar" : "faca_parado"; e.passo += dist / Transmissao.Ciclo("faca_andar"); u = e.passo; }
                else if (anda && a.duck > 0.5f) { anim = "agachado_" + arma; e.passo += dist / CICLO_AGACHADO; u = e.passo; }
                else if (anda && a.Speed < a.MaxSpeed * 0.62f) { anim = "devagar_" + arma; e.passo += dist / CICLO_DEVAGAR; u = e.passo; }
                else if (anda) { anim = "andar_" + arma; e.passo += dist / Transmissao.Ciclo("andar"); u = e.passo; }
                else if (arma == "AWP" && a.scoped)
                {
                    // entrando na luneta (0,27 s) e depois parado nela
                    if (!luneta.TryGetValue(a.id, out float li)) { li = m.t; luneta[a.id] = li; }
                    float ul = (m.t - li) / 0.27f;
                    if (ul < 1) { anim = "awp_mirar"; u = ul; } else anim = "awp_mirando";
                }
                else if (ev.anim == "apontar" && !a.faca) { anim = "apontar_" + arma; u = (m.t - ev.ini) / ev.dur; }
                else if (ev.anim == "aterrissar") anim = (a.faca ? "faca_parado" : "agachar_" + arma);
                else anim = (a.Agachado ? "agachar_" : "mira_") + arma;
                if (!(arma == "AWP" && a.scoped)) luneta.Remove(a.id);
                // sacando a arma (trocou ou guardou a faca): a pose de sacar por cima de parado/andando
                if (!a.faca && a.alive && !anda && a.sacaAte > m.t && string.IsNullOrEmpty(acao) && a.nadeNaMao == null && !a.gun.Reloading && !a.noAr
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
                // sem duelo: olha pro chão só chegando numa ARMA pra pegar com E (trocando a do slot); granada/kit/C4 não
                // (usuário: ficava "olhando pro chão o tempo todo" depois de uma troca, com os drops em volta)
                else if (a.alive && string.IsNullOrEmpty(acao) && OlhaItem(a, out float pItem)) pAlvo = pItem;
                else if (a.alive) pAlvo = PitchMira(a);
                // (trocar arma no chão não olha mais pra baixo: a nova vem como ímã, usuário)
                b.pitch = Mathf.Lerp(b.pitch, pAlvo, 1 - Mathf.Exp(-10f * dt)); b.armaPitch = arma;
                if (!b.Tem(anim)) anim = "mira_" + Transmissao.Padrao(!a.IsT);
                // agachando/levantando aos poucos (Agent.duck da Sim, com smoothstep): parado, a mira puxa pra pose agachada
                float dsu = Suave(a.duck);
                string agA = "agachar_" + arma; if (!b.Tem(agA)) agA = "agachar_" + Transmissao.Padrao(!a.IsT);
                b.agachaAnim = null; b.agachaW = 0;
                if (anim.StartsWith("mira_") && dsu > 0.001f && a.alive) { b.agachaAnim = agA; b.agachaW = dsu; b.agachaSoPernas = false; }
                // ação de tronco andando: as pernas seguem a passada (recarregar/atirar/sacar/jogar granada correndo)
                bool acaoTronco = anim.StartsWith("recarga") || anim.StartsWith("tiro") || anim.StartsWith("sacar") || anim.StartsWith("granada") || anim.StartsWith("apontar") || anim.StartsWith("cego") || anim.StartsWith("meio_cego");
                if (marcha != null && acaoTronco)
                {
                    float ciclo = marcha == "agachado" ? CICLO_AGACHADO : marcha == "devagar" ? CICLO_DEVAGAR : Transmissao.Ciclo("andar");
                    if (e.passo == passoAntes) e.passo += dist / ciclo;
                    string pa = marcha + "_" + arma; if (!b.Tem(pa)) pa = marcha + "_" + Transmissao.Padrao(!a.IsT);
                    b.pernaAnim = pa; b.pernaU = e.passo;
                }
                else
                {
                    b.pernaAnim = null;
                    // agachado parado atirando/recarregando: as pernas descem pra pose agachada com o duck e o tronco junto
                    if (dsu > 0.001f && a.alive && acaoTronco && !a.noAr) { b.agachaAnim = agA; b.agachaW = dsu; b.agachaSoPernas = true; }
                }
                b.troncoY = acaoTronco ? -0.1f * dsu : 0;
                // jump throw: a Sim pula de verdade no tique em que a granada sai da mão (me.noAr); o tronco segue o arremesso
                // (granada_fim) e as pernas fazem a pose de pulo, subindo e caindo. (O pulinho só visual daqui saiu: dobrava.)

                if (a.noAr && acaoTronco && b.Tem("pular")) { b.pernaAnim = "pular"; b.pernaU = a.vz > 0 ? 0.35f : 0.7f; }
                // pulo agachado do CS (Agent.puloAgachado: subindo degrau de 18-23, ex.: a carroça do B): no ar encolhe as
                // pernas na pose agachada
                if (a.alive && a.noAr && a.puloAgachado) { b.agachaAnim = agA; b.agachaW = 1; b.agachaSoPernas = true; }
                // pezinho (boost) é como no CS: o de baixo só agacha (Agent.duck, pode levantar pro pezinho em pé) e o de cima
                // fica em pé na cabeça dele na altura que a Sim der, sem pose própria
                // meio cego (flash de lado): um pouco da pose de cego por cima, sumindo
                b.misturaW = 0;
                // cego não pinta o boneco de branco (personagem é personagem): quem está perto da flash recebe a luz do estouro
                b.brancoCego = 0;
                // pente quase vazio (~20%): 1 vez por pente
                if (a.alive && !a.faca && a.gun != null && a.gun.s.mag > 4)
                {
                    if (a.gun.inMag > 0 && a.gun.inMag <= Mathf.CeilToInt(a.gun.s.mag * 0.2f)) { if (quaseVazio.Add(a.id)) Som3D.QuaseVazio(b.transform.position); }
                    else if (a.gun.inMag > a.gun.s.mag * 0.5f) quaseVazio.Remove(a.id);
                }
                bool queima = a.alive && m.FireAt(a.pos);
                if (queima) { fx.Queimando(b.transform.position, dt); if (noFogo.Add(a.id)) Som3D.Queimando(b.transform.position, a.id); }
                else noFogo.Remove(a.id);
                // zumbido da flash só de quem a câmera segue (é o "ouvido" do espectador)
                bool cego = a.alive && a.Blind(m.t);
                if (cego && seguir >= 0 && m.agents[seguir] == a && cegoVisto.Add(a.id)) Som3D.Zumbido(1f);
                else if (!cego) cegoVisto.Remove(a.id);
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
                b.semC4 = anim.StartsWith("desarmar");   // a C4 é a plantada no chão
                if (!animSom.TryGetValue(a.id, out var anAnt) || anAnt != anim)
                {
                    animSom[a.id] = anim; var pa = b.transform.position;
                    if (anim.StartsWith("recarga") && a.gun != null) Som3D.Recarga(pa, a.gunId, a.gun.s.Reload, a.id);
                    else if (anim == "sacar_faca") Som3D.FacaSacar(pa, a.id);
                    else if (anim.StartsWith("sacar") || anim.StartsWith("pegar")) Som3D.Sacar(pa, a.gunId, a.id);
                    else if (anim.StartsWith("granada_fim")) Som3D.Arremesso(pa, a.id);
                    else if (anim.StartsWith("granada_molotov") && a.IsT) { Som3D.GranadaSacar(pa, a.id); Som3D.Isqueiro(pa, a.id); }
                    else if (anim.StartsWith("granada_")) { Som3D.GranadaSacar(pa, a.id); Som3D.Pino(pa, a.id); }
                    else if (anim.StartsWith("plantar")) { Som3D.C4Sacar(pa); Som3D.C4Digita(pa); }
                    else if (anim.StartsWith("desarmar")) Som3D.C4Desarme(pa, a.hasKit);
                    else if (anim == "awp_mirar") Som3D.Zoom(pa);
                    else if (anim == "faca_m2") Som3D.FacaEstocada(pa, a.id);
                    else if (anim.StartsWith("faca_m")) Som3D.Faca(pa, false, a.id);
                    if (anAnt != null && anAnt.StartsWith("recarga") && !anim.StartsWith("recarga")) Som3D.Parar(a.id);   // trocou no meio
                }
                // arma guardada (granada, faca, plantar, desarmar, escada): essas animações foram exportadas com o fuzil
                // padrão do time (AK/M4) pendurado nas costas -> esconde o da pose e pendura a de VERDADE (usuário: de
                // pistola, vinha uma AK pras costas). Sem primária, nada nas costas (a pistola fica no coldre).
                bool guardada = anim.StartsWith("granada") || anim.StartsWith("faca") || anim.StartsWith("plantar") || anim.StartsWith("desarmar") || anim == "escada";
                if (guardada) b.semArma = true;
                if (porTempo) b.Tocar(anim, u, dt); else b.Pose(anim, u, dt);
                // primária guardada nas costas (de pistola na mão; no saque das costas a própria animação leva ela)
                string prim = a.Primaria;
                Costas(a.id, b, prim != null && (guardada || prim != a.gunId) && !anim.StartsWith("sacar_costas") ? prim : null);
                // molotov acesa na mão: o mesmo fogo do chão no pano + luz que ilumina quem segura
                if (anim.StartsWith("granada_molotov") && b.PecaVisivel("fogo0", out var pano)) fx.Chama(pano, dt, Mathf.Clamp01((u - 0.3f) / 0.15f));
                // laser da AWP na luneta (como nas transmissões): da arma até a parede ou a smoke; forte travado parado
                if (arma == "AWP" && a.scoped) Laser(a, b);
                b.noChao = !naEscada && !a.noAr;
                b.Silhueta(a.alive);
                // levou tiro (vida caiu): tranco + piscar, pela força do dano
                if (ultHp.TryGetValue(a.id, out int hp0) && a.hp < hp0 && a.alive)
                {
                    bool hsG = ultAcerto.TryGetValue(a.id, out var ug) && ug.hs && m.t - ug.t < 0.3f;
                    // de onde veio: pela frente joga pra trás, pelas costas joga pra frente
                    float sinal = ultAcerto.TryGetValue(a.id, out var ud) && m.t - ud.t < 0.3f ? (Vector3.Dot(b.transform.forward, ud.dir) < 0 ? 1 : -1) : 1;
                    b.Golpe(0.35f + (hp0 - a.hp) / 60f, hsG, sinal);
                }
                ultHp[a.id] = a.hp;
                // posição do pé e rumo da mira (interpolados entre passos da simulação: Visivel)
                b.transform.position = MalhaMapa.ParaUnity(vis.p.x, vis.p.y, vis.z);
                float alvoR = naEscada ? 270f : Mathf.Atan2(Mathf.Cos(vis.mira), -Mathf.Sin(vis.mira)) * Mathf.Rad2Deg;
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
            if (replay != null && rq0 != null) FumacasReplay(); else Fumacas();
            // rastro de tiro: risco fino que corre da boca do cano pro alvo (não um bastão inteiro piscando). Pelo registro dos
            // tiros (tirosGravados), no relógio da partida ou no do replay: antes saía do centro do boneco e sumia no replay
            float agora = replay != null ? replay.t : m.t;
            foreach (var s in tirosGravados)
            {
                float idade = agora - s.t; if (idade > 0.09f || idade < 0) continue;
                var a = s.boca; var b = s.ate;   // (sem atirador achado, boca = origem do tiro na simulação)
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
                // bip (Som3D, ritmo do CS) e o LED pisca junto com ele
                var posC4 = MalhaMapa.ParaUnity(bp.x, bp.y, z);
                if (Som3D.C4(posC4, m.bombPlanted && !m.defused && !m.exploded, m.BombLeft)) ultBip = m.t;
                if (m.bombPlanted && !m.defused && !m.exploded && m.t - ultBip < 0.15f)
                    fogo.Por(MalhaMapa.ParaUnity(bp.x, bp.y, z + 5.5f), Quaternion.identity, Vector3.one * 0.08f);
            }
            fumaca.Fim(); fumacaCasca.Fim(); fogo.Fim(); rastro.Fim(); coisas.Fim();
            fx.FimChamas();
            fx.FimLasers();
        }

        readonly Dictionary<SmokeVolume, Fumaca3D> fumacas = new Dictionary<SmokeVolume, Fumaca3D>();
        Shader shFumaca;
        void Fumacas() => Fumacas(m.smokes, m.t);
        void Fumacas(List<SmokeVolume> lista, float t)
        {
            if (shFumaca == null) shFumaca = Shader.Find("CSM/FumacaVolume");
            foreach (var s in fumacas.Keys.ToList())
                if (!lista.Contains(s)) { fumacas[s].Apagar(); fumacas.Remove(s); if (lista == m?.smokes) Som3D.SmokeFim(MalhaMapa.ParaUnity(s.origin.x, s.origin.y, s.origin.z)); }
            foreach (var s in lista)
            {
                if (!fumacas.TryGetValue(s, out var f))
                {
                    f = new Fumaca3D(s, transform, shFumaca); fumacas[s] = f;
                    // no replay só toca o estouro se a smoke nasce no lance (não quando ela é refeita no corte)
                    if (lista == m?.smokes || s.age < 0.3f) Som3D.Smoke(MalhaMapa.ParaUnity(s.origin.x, s.origin.y, s.origin.z));
                }
                f.Atualizar(t, cen.cam);
            }
            Fumaca3D.Publicar(t);
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
            foreach (var f in fumacas.Values) if (f.Cruza(a, b) && f.Furo(a, b, LabT) is Vector3 saiu) fx.TufoSmoke(saiu, ub - ua);
            labTiros.Add((ua, ub, LabT));
        }
        public void LabHE(V3 p)
        {
            foreach (var s in labSmokes) if ((new V2(s.origin.x, s.origin.y) - new V2(p.x, p.y)).Len < Match.HE_FURO + 60) s.Clear(p, Match.HE_FURO, 2.1f);
            foreach (var f in fumacas.Values) f.Explosao(p, LabT);
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.HE(u); Tremer(new V2(p.x, p.y), 0.9f); Empurrar(u, 2.4f, 6.5f);
        }
        public void LabC4(V3 p)
        {
            var u = MalhaMapa.ParaUnity(p.x, p.y, p.z); fx.ExplosaoC4(u); Tremer(new V2(p.x, p.y), 2.5f); Empurrar(u, 9f, 13f);
        }
        /// <summary>Vitrine: as 5 granadas em pé no chão, enfileiradas a partir de p (no tamanho do jogo) e um boneco do lado.</summary>
        public void LabGranadas(V2 p)
        {
            float z = NavGrid.ZAt(p); int i = 0;
            foreach (var (k, tr) in new[] { ("he", true), ("flash", true), ("smoke", true), ("molotov", false), ("molotov", true) })
            {
                var g = Granada(k, tr); g.transform.localScale = Vector3.one * ESC_GRANADA;
                g.transform.position = MalhaMapa.ParaUnity(p.x + i * 6, p.y, z) + Vector3.up * 0.08f; i++;
            }
            var b = Boneco3D.Criar("ct", transform); b.transform.position = MalhaMapa.ParaUnity(p.x - 8, p.y, z); b.Pose("mira_M4A4", 0, 0);
        }
        public FireArea LabFogo(V3 o) { var f = new FireArea(o); labFogos.Add(f); fx.Molotov(MalhaMapa.ParaUnity(o.x, o.y, o.z)); return f; }
        public void LabApagaFogo(FireArea f) { labFogos.Remove(f); fx.ApagouFogo(f); fx.Vapor(MalhaMapa.ParaUnity(f.origin.x, f.origin.y, f.origin.z), 1.1f); }
        public void LabItem(string gunId, V2 pos)
        {
            var k = new object(); labItens.Add(k);
            float z = NavGrid.ZAt(pos);
            var ac = NovoItem(Armas3D.Objeto(Boneco3D.Carregar("tr"), Armas3D.DoJogo(gunId), Boneco3D.matCorpo), MalhaMapa.ParaUnity(pos.x, pos.y, z) + Vector3.up * 0.05f, Vector3.zero, UnityEngine.Random.Range(0, 360f));
            ac.v = Vector3.zero; ac.w = Vector3.zero;
            armasChao[k] = ac;
        }
        /// <summary>Laboratório do apoio no chão: as 5 granadas, o kit e 4 armas caindo girando de meio metro numa fila.</summary>
        public void LabApoio(V2 p)
        {
            var itens = new List<(string nome, bool arma)> { ("smoke", false), ("flash", false), ("he", false), ("molotov", false), ("incendiaria", false), ("kit", false),
                                                            ("ak", true), ("awp", true), ("glock", true), ("deagle", true) };
            for (int i = 0; i < itens.Count; i++)
            {
                var q = new V2(p.x + (i - 4.5f) * (i >= 6 ? 9f : 6f) * 0.7071f + (i >= 6 ? 10f : 0f), p.y - (i - 4.5f) * (i >= 6 ? 9f : 6f) * 0.7071f + (i >= 6 ? 12f : 0f));
                var k = new object(); labItens.Add(k);
                var mao = MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)) + Vector3.up * 0.5f;
                ArmaCaindo ac;
                if (itens[i].arma) ac = NovoItem(Armas3D.Objeto(Boneco3D.Carregar("tr"), Armas3D.DoJogo(itens[i].nome), Boneco3D.matCorpo), mao, Vector3.zero, i * 37f);
                else
                {
                    var gi = Granada(itens[i].nome, itens[i].nome != "incendiaria"); gi.transform.localScale = Vector3.one * ESC_GRANADA_CHAO;
                    ac = NovoItem(gi, mao, Vector3.zero, i * 37f); ac.meiaAltura = 0.03f * ESC_GRANADA_CHAO;
                }
                ac.v = new Vector3(0.3f, 0, 0.2f);
                armasChao[k] = ac; labNomes[k] = itens[i].nome;
            }
        }
        readonly Dictionary<object, string> labNomes = new Dictionary<object, string>();
        /// <summary>Folga entre o ponto mais baixo de cada item do laboratório e o chão (unidades do jogo; + = flutuando,
        /// - = afundado). Log "[Visual3D] APOIO".</summary>
        public void LabApoioMedir()
        {
            foreach (var kv in armasChao)
            {
                if (!labNomes.TryGetValue(kv.Key, out var nome)) continue;
                var ac = kv.Value; float mn = float.MaxValue;
                foreach (var mf in ac.go.GetComponentsInChildren<MeshFilter>())
                {
                    if (mf.sharedMesh == null || !mf.sharedMesh.isReadable) continue;
                    foreach (var v in mf.sharedMesh.vertices) mn = Mathf.Min(mn, mf.transform.TransformPoint(v).y);
                }
                foreach (var sr in ac.go.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    var tmp = new Mesh(); sr.BakeMesh(tmp, false);
                    foreach (var v in tmp.vertices) mn = Mathf.Min(mn, sr.transform.TransformPoint(v).y);
                    Apagar(tmp);
                }
                float folga = (mn - Efeitos3D.Piso(ac.go.transform.position)) / MalhaMapa.ESC;
                var bb = new Bounds(ac.go.transform.position, Vector3.zero); int nr = 0;
                foreach (var rr in ac.go.GetComponentsInChildren<Renderer>()) { bb.Encapsulate(rr.bounds); nr++; }
                Debug.Log($"[Visual3D] APOIO {nome}: folga {folga:+0.00;-0.00} u (parada {ac.parada}, pts {ac.pts?.Length ?? 0}, {nr} renderers, tamanho {bb.size.x / MalhaMapa.ESC:0.0}x{bb.size.y / MalhaMapa.ESC:0.0}x{bb.size.z / MalhaMapa.ESC:0.0} u)");
            }
        }
        public void LabCamera(V2 alvo, float zoom) { camSuave = new Vector2(alvo.x, alvo.y); zoomSuave = zoom; camPronta = true; cen.alvoJogo = camSuave; cen.enquadrar = zoom; cen.Enquadrar(); }
        // laboratório de tiros: alvos parados levando um tiro cada (capacete, cabeça sem capacete, corpo, morte HS, morte corpo)
        class AlvoLab { public Boneco3D b; public bool hs, prot, mata, feito; public float t, ang, forca = 1f, velFrente; public Ragdoll3D rg; public string anim, nome; public Vector3 dir; }
        readonly List<AlvoLab> labAlvos = new List<AlvoLab>();
        /// <summary>Fila de 5 CTs (giro = pra onde olham), cada um leva um tiro pela frente em t0 + 0,25 s × i.</summary>
        public void LabTiros(V2 p, float giro, float t0 = 0.3f, string quais = "0,1,2,3,4")
        {
            var todos = new[] { (true, true, false), (true, false, false), (false, false, false), (true, false, true), (false, false, true) };
            var sel = quais.Split(','); var casos = new (bool, bool, bool)[sel.Length];
            for (int j = 0; j < sel.Length; j++) casos[j] = todos[int.Parse(sel[j])];
            for (int i = 0; i < casos.Length; i++)
            {
                float cx = (i - (casos.Length - 1) / 2f) * 16;
                var q = new V2(p.x + cx * 0.7071f, p.y - cx * 0.7071f);
                var b = Boneco3D.Criar("ct", transform);
                b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                b.Pose("mira_M4A4", 0, 0);
                labAlvos.Add(new AlvoLab { b = b, hs = casos[i].Item1, prot = casos[i].Item2, mata = casos[i].Item3, t = t0 + 0.25f * i, anim = "mira_M4A4" });
            }
        }
        void LabTirosPasso(float dt)
        {
            foreach (var al in labAlvos)
            {
                if (!al.feito && LabT >= al.t)
                {
                    al.feito = true;
                    var b = al.b; var dir = Quaternion.Euler(0, al.ang, 0) * -b.transform.forward;   // ang 0: a bala vem pela frente
                    al.dir = dir;
                    var alvoP = (al.hs ? b.Peca("cabeca") : b.Peca("tronco"))?.position ?? b.transform.position + Vector3.up * 0.4f;
                    var boca = alvoP - dir * 3f;
                    fx.Tiro(boca, dir, Vector3.Cross(Vector3.up, dir), false, false); fx.Rastro(boca, alvoP);
                    fx.Impacto(alvoP, dir, true, al.hs, al.prot);
                    if (al.mata) { b.Golpe(0); al.rg = new Ragdoll3D(b, dir, al.forca, 7 + labAlvos.IndexOf(al), al.hs, b.transform.forward * al.velFrente); }
                    else b.Golpe(0.35f + 0.6f, al.hs, 1);
                }
                if (al.rg != null) al.rg.Passo(dt);
                else if (al.velFrente > 0) { al.b.Pose(al.anim, LabT * 1.4f, dt); al.b.transform.position += al.b.transform.forward * al.velFrente * dt; }
                else al.b.Pose(al.anim, 0, dt);
            }
        }

        /// <summary>Laboratório das mortes: 8 CTs (giro = pra onde olham) morrendo juntos em t0 com força de fuzil, um caso
        /// cada: cabeça/peito × frente/costas/lado, cabeça correndo e peito agachado.</summary>
        public void LabMortes(V2 p, float giro, float t0 = 0.3f, string quais = "0,1,2,3,4,5,6,7")
        {
            var casos = new (string nome, bool hs, float ang, string anim, float vel)[] {
                ("HS frente", true, 0, "mira_M4A4", 0), ("HS costas", true, 180, "mira_M4A4", 0), ("HS lado", true, 90, "mira_M4A4", 0),
                ("peito frente", false, 0, "mira_M4A4", 0), ("peito costas", false, 180, "mira_M4A4", 0), ("peito lado", false, 90, "mira_M4A4", 0),
                ("HS correndo", true, 0, "andar_M4A4", 2.1f), ("peito agachado", false, 0, "agachar_M4A4", 0) };
            var sel = System.Array.ConvertAll(quais.Split(','), int.Parse);
            for (int k = 0; k < sel.Length; k++)
            {
                int i = sel[k];
                float cx = (k % 2 - 0.5f) * 34, cy = (k / 2 - (sel.Length > 2 ? 0.5f : 0)) * 34;
                var q = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                var b = Boneco3D.Criar("ct", transform);
                b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                b.Pose(casos[i].anim, 0, 0);
                labAlvos.Add(new AlvoLab { b = b, hs = casos[i].hs, mata = true, t = t0, ang = casos[i].ang, forca = 0.28f, velFrente = casos[i].vel, anim = casos[i].anim, nome = casos[i].nome });
            }
        }
        /// <summary>Resumo das mortes do laboratório: giro do tronco e pra onde a cabeça caiu (em relação à bala e à frente).</summary>
        public string LabMortesResumo()
        {
            var sb = new System.Text.StringBuilder();
            foreach (var al in labAlvos)
            {
                if (al.rg == null) continue;
                var cp = al.rg.CabecaMenosPelve; cp.y = 0; cp.Normalize();
                var fr = al.b.transform.forward; fr.y = 0; fr.Normalize();
                sb.AppendLine($"[Visual3D] MORTE {al.nome}: giro {al.rg.Giro:0}° (líquido {al.rg.GiroLiq:+0;-0}°), cabeça caiu {Vector3.Dot(cp, al.dir):+0.00;-0.00} na direção da bala, {Vector3.Dot(cp, fr):+0.00;-0.00} pra frente do corpo{(al.rg.dormindo ? "" : " (ainda mexendo)")} [frente do tronco x do boneco {al.rg.FrenteConfere:+0.00;-0.00}]");
            }
            return sb.ToString();
        }

        public void LabPasso(float dt)
        {
            LabT += dt;
            LabTirosPasso(dt);
            foreach (var s in labSmokes) s.Update(dt);
            foreach (var f in labFogos) f.Update(dt);
            // mesma regra da partida: chama acesa coberta pela smoke apaga o fogo inteiro
            foreach (var f in labFogos.ToList())
                for (int i = 0; i < f.Visible; i++)
                {
                    var c = f.cells[i]; var q = new V3(c.x * FireArea.C + 2, c.y * FireArea.C + 2, c.z + 6);
                    if (labSmokes.Exists(s => s.Sample(q, LabT) > 0.3f)) { LabApagaFogo(f); break; }
                }
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
        GameObject c4; float ultBip = -9;
        readonly Dictionary<object, GameObject> props = new Dictionary<object, GameObject>();
        readonly HashSet<object> propsVistos = new HashSet<object>();
        // estado do desenho de cada granada voando: giro acumulado, quiques já vistos, raio (pra apoiar no chão)
        class Voo { public Quaternion rot = Quaternion.identity; public int quiques, paredes; public float raio; public Vector3 ultPos; public bool tem; }
        readonly Dictionary<object, Voo> voos = new Dictionary<object, Voo>();
        // lata da smoke caída no chão enquanto a smoke dura (no CS2 a granada fica lá soltando a fumaça)
        readonly List<(GameObject g, float ate)> latas = new List<(GameObject, float)>();
        float ultTProps = -1;
        // tamanho da granada na tela (usuário: no celular não dava pra ver voando nem no chão)
        const float ESC_GRANADA = 2.2f, ESC_GRANADA_CHAO = ESC_GRANADA;   // no chão do mesmo tamanho que voando
        /// <summary>Granada solta no mundo (voando, caída, lata): o modelo do kit feito pelas medidas do CS2
        /// (Resources/Granadas); sem ele, as peças da animação de jogar como antes. CT de molotov = incendiária.</summary>
        GameObject Granada(string tipo, bool tr)
        {
            string k = tipo == "incendiaria" ? "molotov" : tipo;
            string kit = k switch { "he" => "GRN_HE", "flash" => "GRN_Flashbang", "smoke" => "GRN_Smoke", "kit" => "DefuseKit", _ => tr ? "GRN_Molotov" : "GRN_Incendiary" };
            var pf = Resources.Load<GameObject>("Granadas/" + kit);
            if (pf != null)
            {
                // embrulho: o modelo vem em metros com o centro do corpo na origem; ESC_KIT dá o tamanho de antes na tela
                var g0 = new GameObject("granada " + kit); g0.transform.SetParent(transform, false);
                // a garrafa tem 35 cm: no tamanho real ficava do tamanho do boneco chibi
                var m0 = Instantiate(pf, g0.transform, false); m0.transform.localScale = pf.transform.localScale * (kit == "GRN_Molotov" ? ESC_KIT * 0.55f : ESC_KIT);
                if (kit == "DefuseKit")
                {
                    // o kit vem do atlas de armas do EsportsPoc convertido pra cor por vértice (atlas_cor.py): o mesmo
                    // material dos bonecos (sem ele saía branco)
                    Boneco3D.GarantirMateriais(); bool lin = QualitySettings.activeColorSpace == ColorSpace.Linear;
                    // malha com esqueleto (vem do rig do kit de armas) não desenhava no chão: vira malha fixa na pose de agora
                    foreach (var sr in m0.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        var baked = new Mesh { name = "kit fixo" }; sr.BakeMesh(baked, false);
                        var fixo = new GameObject("kit fixo"); fixo.transform.SetParent(sr.transform, false);
                        fixo.AddComponent<MeshFilter>().sharedMesh = baked; fixo.AddComponent<MeshRenderer>();
                        sr.enabled = false; DestroyImmediate(sr);
                    }
                    foreach (var mf in m0.GetComponentsInChildren<MeshFilter>())
                    {
                        if (lin && mf.sharedMesh != null && mf.sharedMesh.colors.Length > 0)
                        {
                            var me = Instantiate(mf.sharedMesh); var cs = me.colors;
                            for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                            me.colors = cs; mf.sharedMesh = me;
                        }
                        var r = mf.GetComponent<MeshRenderer>(); if (r != null) r.sharedMaterial = Boneco3D.matCorpo;
                    }
                    // a escala do FBX convertido não é a das granadas: força a altura real (11 cm) na mesma régua (ESC_KIT)
                    float alt = 0; foreach (var rr in m0.GetComponentsInChildren<Renderer>()) alt = Mathf.Max(alt, rr.bounds.size.y, rr.bounds.size.x, rr.bounds.size.z);
                    if (alt > 1e-5f) m0.transform.localScale *= 0.11f * ESC_KIT * g0.transform.lossyScale.y / alt;
                    foreach (var sr in m0.GetComponentsInChildren<SkinnedMeshRenderer>())
                    {
                        if (lin && sr.sharedMesh != null && sr.sharedMesh.colors.Length > 0)
                        {
                            var me = Instantiate(sr.sharedMesh); var cs = me.colors;
                            for (int i = 0; i < cs.Length; i++) cs[i] = cs[i].linear;
                            me.colors = cs; sr.sharedMesh = me;
                        }
                        sr.sharedMaterial = Boneco3D.matCorpo;
                    }
                }
                return g0;
            }
            var g = Armas3D.Prop(Boneco3D.Carregar(tr ? "tr" : "ct"), "granada_" + k, 0.5f, Boneco3D.matCorpo, new[] { "sm_", "fl_", "mo_", "he_" });
            g.transform.SetParent(transform, false);
            return g;
        }
        const float ESC_KIT = 0.7f;   // unidades Unity por metro do modelo do kit (antes de ESC_GRANADA)

        // granada NA MÃO: o modelo do kit (o mesmo que voa e cai) no lugar da de elipsoides da animação — mesma posição,
        // em pé na direção do topo dela, um pouco maior que ela (o kit é mais fino e de longe sumia)
        static readonly string[] TIPO_MAO = { "smoke", "flash", "molotov", "he" };
        readonly Dictionary<int, (GameObject go, int tipo, bool tr, float alt)> naMao = new Dictionary<int, (GameObject, int, bool, float)>();
        /// <summary>Body block da Sim é de 8 (a escala do CS2), mas a cabeça chibi tem ~10 de largura: abaixo de 10 entre
        /// centros as cabeças se encostavam/entravam uma na outra. Só no DESENHO: cada boneco vivo é empurrado pra longe do
        /// outro, no máximo 1 unidade (a 8 de distância), gradual a partir de 10 (sem pulo). Na mesma altura só (|dz| &lt; 18).
        /// Usuário: "deixa 8 e arruma o visual".</summary>
        void AfastarCorpos()
        {
            const float LIVRE = 10f, MAX = 1f;
            var vivos = m.agents.Where(x => x.alive && bonecos.ContainsKey(x.id)).ToList();
            var desloc = new Dictionary<int, Vector3>();
            for (int i = 0; i < vivos.Count; i++)
                for (int j = i + 1; j < vivos.Count; j++)
                {
                    var a = vivos[i]; var c = vivos[j];
                    if (Mathf.Abs(a.z - c.z) >= 18) continue;
                    // pezinho (boost): a Sim sobrepõe os dois de propósito (quem sobe passa pelo ombro de quem está agachado)
                    if ((a.dandoPezinho || a.subindoPezinho) && (c.dandoPezinho || c.subindoPezinho)) continue;
                    Vector3 pa = bonecos[a.id].transform.position, pc = bonecos[c.id].transform.position;
                    var d = new Vector3(pa.x - pc.x, 0, pa.z - pc.z); float dist = d.magnitude / MalhaMapa.ESC;
                    if (dist >= LIVRE) continue;
                    var dir = dist > 0.01f ? d.normalized : Vector3.right;
                    float emp = Mathf.Min(MAX, (LIVRE - dist) / 2f) * MalhaMapa.ESC;
                    desloc[a.id] = (desloc.TryGetValue(a.id, out var da) ? da : Vector3.zero) + dir * emp;
                    desloc[c.id] = (desloc.TryGetValue(c.id, out var dc) ? dc : Vector3.zero) - dir * emp;
                }
            foreach (var kv in desloc)
                bonecos[kv.Key].transform.position += Vector3.ClampMagnitude(kv.Value, MAX * MalhaMapa.ESC);
        }

        // kit de desarme no corpo do CT que tem (Agent.hasKit): pendurado do lado de fora da coxa esquerda (o coldre é na
        // direita), seguindo a perna; morto não mostra (o kit cai no chão como item, ItemChao "kit")
        readonly Dictionary<int, GameObject> kitsCorpo = new Dictionary<int, GameObject>();
        void KitsNoCorpo()
        {
            foreach (var a in m.agents)
                if (bonecos.TryGetValue(a.id, out var b)) KitNoCorpo(a.id, b, a.alive && !a.IsT && a.hasKit);
        }
        void KitNoCorpo(int chave, Boneco3D b, bool tem)
        {
            kitsCorpo.TryGetValue(chave, out var g);
            Transform cL = tem ? b.Peca("coxaL") : null, cR = tem ? b.Peca("coxaR") : null;
            if (cL == null || cR == null) { if (g != null && g.activeSelf) g.SetActive(false); return; }
            if (g == null)
            {
                g = Granada("kit", false); g.name = "kit no corpo " + chave;
                g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); g.transform.localScale = Vector3.one;
                float alt = 0; foreach (var r in g.GetComponentsInChildren<Renderer>()) alt = Mathf.Max(alt, r.bounds.size.y);
                g.transform.localScale = Vector3.one * (0.085f / Mathf.Max(0.001f, alt));
                int camB = LayerMask.NameToLayer(Boneco3D.CAMADA);
                if (camB >= 0)
                {
                    var mp = new MaterialPropertyBlock(); mp.SetColor("_CorTime", Boneco3D.COR_CT);
                    foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camB;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mp);
                }
                kitsCorpo[chave] = g;
            }
            if (!g.activeSelf) g.SetActive(true);
            var lado = cL.position - cR.position; lado.y = 0;
            lado = lado.sqrMagnitude > 1e-8f ? lado.normalized : -b.transform.right;
            g.transform.SetPositionAndRotation(cL.position + lado * 0.045f + Vector3.up * 0.01f, Quaternion.LookRotation(lado, Vector3.up) * Quaternion.Euler(0, 90, 0));
        }

        void MaoGranadas()
        {
            foreach (var a in m.agents)
                if (bonecos.TryGetValue(a.id, out var b)) NaMao(a.id, b, a.IsT);
        }
        void NaMao(int chave, Boneco3D b, bool tr)
        {
            naMao.TryGetValue(chave, out var nm);
            if (!b.GranadaNaMao(out var c, out var cima, out float alt, out int tipo))
            {
                if (nm.go != null && nm.go.activeSelf) nm.go.SetActive(false);
                return;
            }
            if (nm.go == null || nm.tipo != tipo || nm.tr != tr)
            {
                if (nm.go != null) Apagar(nm.go);
                var g = Granada(TIPO_MAO[tipo], tr);
                g.name = "granada na mão " + chave;
                // na camada dos bonecos (como a arma das costas): fora da profundidade do mapa, então a mão atrás dela não
                // vira silhueta "atrás da parede"; atrás de parede de verdade ela ganha a silhueta na cor do time
                int camB = LayerMask.NameToLayer(Boneco3D.CAMADA);
                if (camB >= 0)
                {
                    var mpS = new MaterialPropertyBlock(); mpS.SetColor("_CorTime", tr ? Boneco3D.COR_TR : Boneco3D.COR_CT);
                    foreach (var t in g.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = camB;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) r.SetPropertyBlock(mpS);
                }
                g.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); g.transform.localScale = Vector3.one;
                float alt0 = 0;
                foreach (var r in g.GetComponentsInChildren<Renderer>()) alt0 = Mathf.Max(alt0, r.bounds.size.y);
                nm = (g, tipo, tr, Mathf.Max(0.001f, alt0)); naMao[chave] = nm;
            }
            if (!nm.go.activeSelf) nm.go.SetActive(true);
            var fr = Vector3.ProjectOnPlane(b.transform.forward, cima);
            var rot = fr.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(fr, cima) : Quaternion.FromToRotation(Vector3.up, cima);
            nm.go.transform.SetPositionAndRotation(c, rot);
            nm.go.transform.localScale = Vector3.one * (alt * 1.45f / nm.alt);   // maior que a bolota: lê de longe
        }

        /// <summary>Laboratório do arremesso: CT (fila de cima) e TR, cada um parado numa fase (armado / soltando /
        /// continuação) de cada jeito (por cima, meia força, por baixo), com a granada do kit na mão.</summary>
        public void LabArremesso(V2 p, float giro, bool tr, float passo = 11)
        {
            string[] tipos = { "smoke", "he", "flash", "molotov" }; string[] jeitos = { "", "_meio", "_baixo" };
            int chave = 1000;
            // grade na tela: coluna = jeito (por cima, meia, por baixo), linha = fase (armado, soltando, continuação).
            // Na câmera iso, +x desce pra direita e +y desce pra esquerda: direita na tela = (+x, -y), baixo = (+x, +y)
            for (int j = 0; j < 3; j++)
                for (int f = 0; f < 3; f++)
                {
                    float cx = (j - 1) * passo, cy = (f - 1) * passo * 1.1f;
                    var q = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                    var b = Boneco3D.Criar(tr ? "tr" : "ct", transform); b.nadeKit = true;
                    b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, giro, 0));
                    string tp = tipos[(j + f) % 4];
                    if (f < 2) b.Pose("granada_" + tp + jeitos[j], f == 0 ? 0.72f : 0.97f, 0);
                    else b.Pose("granada_fim" + jeitos[j], 0.3f, 0);
                    NaMao(chave++, b, tr);
                }
        }

        /// <summary>Laboratório do body block: pares de bonecos a exatamente <paramref name="d"/> unidades (a distância mínima
        /// entre centros da Sim) — lado a lado, um atrás do outro e de frente — na linha de cima TR, na de baixo CT.</summary>
        public void LabEncosto(V2 p, float giro, float d)
        {
            float g = giro * Mathf.Deg2Rad;
            var frente = new V2(Mathf.Sin(g), -Mathf.Cos(g)); var lado = new V2(Mathf.Cos(g), Mathf.Sin(g));
            for (int lin = 0; lin < 2; lin++)
                for (int k = 0; k < 3; k++)
                {
                    float cx = (k - 1) * 30, cy = (lin - 0.5f) * 30;
                    var c = new V2(p.x + (cx + cy) * 0.7071f, p.y + (cy - cx) * 0.7071f);
                    var off = k == 0 ? lado : frente;
                    var a = new V2(c.x - off.x * d / 2, c.y - off.y * d / 2); var b2 = new V2(c.x + off.x * d / 2, c.y + off.y * d / 2);
                    for (int i = 0; i < 2; i++)
                    {
                        var q = i == 0 ? a : b2; bool tr = lin == 0;
                        var b = Boneco3D.Criar(tr ? "tr" : "ct", transform);
                        float rot = giro + (k == 2 && i == 1 ? 180 : 0);   // de frente: o segundo vira pro primeiro
                        b.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(q.x, q.y, NavGrid.ZAt(q)), Quaternion.Euler(0, rot, 0));
                        b.Pose("andar_" + Transmissao.Padrao(!tr), 0.25f + 0.5f * i, 0);
                        if (!tr) KitNoCorpo(2000 + k * 2 + i, b, true);
                    }
                }
        }
        // cor do rastro de cada granada (lê de longe qual é qual)
        static Color CorRastro(string kind) => kind switch
        {
            "smoke" => new Color(0.86f, 0.88f, 0.9f), "flash" => new Color(1f, 0.97f, 0.8f), "he" => new Color(1f, 0.38f, 0.25f),
            _ => new Color(1f, 0.62f, 0.2f),
        };
        /// <summary>
        /// Granadas voando com a física da simulação (Match.Passo: quica na parede, quica e rola no chão, cai da borda):
        /// o modelo gira conforme a velocidade (rola quando está no chão) e fica apoiado pelo raio dele; rastro curto na
        /// cor da granada; cada quique solta poeira (chão) ou faísca (parede). Smoke que estourou deixa a lata no chão.
        /// </summary>
        void Props()
        {
            float dt = ultTProps < 0 ? 1 / 30f : Mathf.Clamp(m.t - ultTProps, 0, 0.1f); ultTProps = m.t;
            molotovsNoAr.Clear();
            propsVistos.Clear();
            foreach (var n in m.flying)
            {
                propsVistos.Add(n);
                if (!props.TryGetValue(n, out var g))
                {
                    g = Granada(n.l.kind, n.by != null && n.by.IsT); props[n] = g;
                    g.transform.localScale = Vector3.one * ESC_GRANADA;   // maior que a da mão: lê de longe na câmera de cima
                    var b = new Bounds(g.transform.position, Vector3.zero); bool achou = false;
                    foreach (var r in g.GetComponentsInChildren<Renderer>()) { if (!achou) { b = r.bounds; achou = true; } else b.Encapsulate(r.bounds); }
                    voos[n] = new Voo { raio = achou ? Mathf.Min(b.extents.x, Mathf.Min(b.extents.y, b.extents.z)) : 0.01f, rot = Quaternion.Euler(0, n.from.x * 7, 0), quiques = n.quiques, paredes = n.paredes };
                }
                var v = voos[n];
                var nv = NadeVisivel(n);
                var p = MalhaMapa.ParaUnity(nv.x, nv.y, nv.z) + Vector3.up * v.raio;
                // giro: rola em volta do eixo de lado ao movimento; no ar gira mais rápido (saiu da mão girando)
                var vel = new Vector3(n.vx, n.vz, -n.vy) * MalhaMapa.ESC; float sp = vel.magnitude;
                var vh = new Vector3(vel.x, 0, vel.z);
                if (sp > 1e-4f)
                {
                    var eixo = vh.sqrMagnitude > 1e-8f ? Vector3.Cross(Vector3.up, vh.normalized) : Vector3.right;
                    float graus = sp * dt / Mathf.Max(v.raio, 0.005f) * Mathf.Rad2Deg * (n.noChao ? 1f : 0.35f);
                    v.rot = Quaternion.AngleAxis(graus, eixo) * v.rot;
                }
                g.transform.SetPositionAndRotation(p, v.rot);
                // quique: chão (poeira) ou parede (faísca)
                if (n.paredes > v.paredes) { fx.QuiqueParede(p, vh); Som3D.Quique(p, n.l.kind, true); }
                else if (n.quiques > v.quiques) { fx.QuiqueChao(p - Vector3.up * v.raio); Som3D.Quique(p, n.l.kind, false); }
                v.quiques = n.quiques; v.paredes = n.paredes;
                // rastro só enquanto voa rápido (parada no chão não risca)
                if (v.tem && sp > 0.15f) fx.RastroGranada(v.ultPos, p, CorRastro(n.l.kind));
                v.ultPos = p; v.tem = true;
                // só a molotov do TR voa acesa (pano pegando fogo); a incendiária do CT é granada de pino, como no CS2
                if ((n.l.kind == "incendiaria" || n.l.kind == "molotov") && n.by != null && n.by.IsT) { fx.Chama(g.transform.position, 1 / 30f, 1f); molotovsNoAr.Add(g.transform.position); }
            }
            foreach (var k in props.Keys.ToList())
                if (!propsVistos.Contains(k))
                {
                    var n = (Match.Nade)k;
                    // molotov/incendiária que estourou: o fogo novo herda de quem era (som da garrafa x da incendiária)
                    if ((n.l.kind == "molotov" || n.l.kind == "incendiaria") && props[k] != null)
                        molotovsSumiram.Add((props[k].transform.position, n.l.kind == "incendiaria" || (n.by != null && !n.by.IsT), m.t));
                    // smoke estourou: a lata fica deitada no chão enquanto a fumaça dura
                    if (n.l.kind == "smoke" && voos.TryGetValue(k, out var v))
                    {
                        var g = props[k];
                        g.transform.SetPositionAndRotation(MalhaMapa.ParaUnity(n.x, n.y, n.z) + Vector3.up * v.raio, Quaternion.Euler(0, n.from.x * 7, 90) );
                        latas.Add((g, m.t + SmokeVolume.LIFE));
                    }
                    else Apagar(props[k]);
                    props.Remove(k); voos.Remove(k); nadeAntes.Remove((Match.Nade)k);
                }
            for (int i = latas.Count - 1; i >= 0; i--) if (m.t > latas[i].ate || m.t < latas[i].ate - SmokeVolume.LIFE - 1) { Apagar(latas[i].g); latas.RemoveAt(i); }
            if (c4 != null && !m.bombPlanted) c4.SetActive(false);
            Som3D.MolotovsVoando(molotovsNoAr);   // o pano pegando fogo voando (lista vazia para o som)
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
            if (carregando) { GUI.Label(new Rect(20, 20, 400, 30), aquecendo ? "Aquecendo a simulação..." : "Montando a maquete e a partida..."); return; }
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
                    // números reais de cada jogador, vindos da simulação (viram a tela de pós-partida)
                    var desempenho = new List<CSM.Manager.Desempenho>();
                    for (int jj = 0; jj < pt.A.js.Length; jj++)
                    {
                        var pj = pt.A.js[jj];
                        desempenho.Add(new CSM.Manager.Desempenho { nome = pj.nome, funcao = pj.roleT, k = pj.k, d = pj.d, hs = pj.hs, dano = pj.dano, fk = pj.fk, rounds = Mathf.Max(1, pj.rounds) });
                    }
                    carreira.Registrar(venceu, pt.A.placar, pt.B.placar, true, desempenho);
                    CSM.ManagerUI.Salvamento.Salvar(carreira);
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
