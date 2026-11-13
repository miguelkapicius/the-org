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
    /// Teclas: espaço pausa, 1-4 velocidade, N pula o round, P painel, R rádio do time (A/B/desligado), Esc câmera automática, roda do mouse = zoom.
    /// </summary>
    public partial class PartidaAoVivo3D : MonoBehaviour
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
            // semente da partida: no jogo é sorteada de verdade (~2 bilhões de valores, sem depender da hora), então não dá pra
            // repetir uma partida por acaso (antes era sempre 15 e toda partida saía igual, round por round). -csm_seed N é
            // ferramenta nossa pra repetir um bug; o jogador nunca escolhe. O editor (prints sem Play) segue na 15, pra comparar.
            // Folga no topo: a vitrine soma 7919 e o Reiniciar soma 1.
            var argv = System.Environment.GetCommandLineArgs(); int ia = System.Array.IndexOf(argv, "-csm_seed");
            if (ia >= 0 && ia + 1 < argv.Length && int.TryParse(argv[ia + 1], out int sv)) seed = sv;
            else if (Application.isPlaying) seed = System.Security.Cryptography.RandomNumberGenerator.GetInt32(1, int.MaxValue - 100000);
            Debug.Log($"[Visual3D] partida com semente {seed} (pra repetir: -csm_seed {seed})");
            Brain.Explicar = true;
            Diretor.Velho = System.Array.Exists(System.Environment.GetCommandLineArgs(), s => s == "-diretor_velho");   // A/B do diretor
            Diretor.InicioVelho = System.Array.Exists(System.Environment.GetCommandLineArgs(), s => s == "-diretor_inicio_velho");   // A/B da leitura do round calmo
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
            // menu inicial (sessão HUDs/interface, Menu3D): no Play abre o menu com um round rodando atrás (vitrine);
            // em batch, print e medição (-csm_*) vai direto pra partida como antes
            if (Menu3D.Ligado) menu = new Menu3D(this); else NovaPartida();   // (o Menu3D começa a vitrine)
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
        string vozVitoria; float vozVitoriaEm;   // anúncio de quem venceu (espera o "bomba desarmada" terminar)
        void Passo(float dt, bool anda, bool limitarTempo)
        {
            Som3D.Relogio(replay != null ? replay.t : m.t);   // recarga agendada no tempo do jogo (em 4x o pente batia depois)
            if (replay != null) { if (anda) ReplayPasso(dt); return; }
            // torcida de arena (Som3D.Plateia): multi-kill, ace, jogada e "ohhh" na hora
            Som3D.PlateiaQuadro(m);
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
                        if (m.over) { if (m.defused) Som3D.Anuncio("bomba_desarmada"); vozVitoria = m.winner == "TR" ? "vitoria_tr" : "vitoria_ct"; vozVitoriaEm = m.defused ? 1.6f : 0f; Som3D.PlateiaFimRound(m); }
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

        public Match Partida => m;
        public Cenario3D Cenario => cen;

        static Mesh Malha(PrimitiveType t) { var g = GameObject.CreatePrimitive(t); var me = g.GetComponent<MeshFilter>().sharedMesh; Apagar(g); return me; }

        void NovaPartida() { pt = new Sim.Partida(seed, null, null); Som3D.PlateiaComecar(seed, !Vitrine); acabou = false; NovoRound(); }

        // ---- menu (Menu3D): vitrine = partida de outra semente rodando atrás do menu, sem HUD e sem controles
        Menu3D menu;
        public bool Vitrine { get; private set; }
        public bool Acabou => acabou;
        public Sim.Partida PartidaAtual => pt;
        public Match RoundAtual => m;
        public void ComecarVitrine() { Vitrine = true; int s0 = seed; seed = s0 + 7919; NovaPartida(); seed = s0; pausado = false; velocidade = 1; seguir = -1; }
        public void ComecarPartida() { Vitrine = false; NovaPartida(); pausado = false; velocidade = 1; seguir = -1; }
        /// <summary>Outra partida (semente seguinte: a mesma semente repetiria a partida igual).</summary>
        public void Reiniciar() { seed++; ComecarPartida(); }

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
            // round novo zera a agenda do bip: com a C4 explodindo, o round ia direto pro replay sem o Som3D ver a C4 "desarmada"
            // e a agenda ficava presa no último segundo: no round seguinte ela só bipava no fim (usuário, 2026-11-05)
            Som3D.C4(Vector3.zero, false, 0);
            props.Clear(); if (c4 != null) c4.SetActive(false); tirosVistos.Clear(); fogosVistos.Clear(); fogosAcesos.Clear(); c4PlantadoVisto = false; c4PlantouEm = c4ExplodiuEm = c4DesarmouEm = -1; animSom.Clear(); arremesso.Clear(); centrosFogo.Clear(); Som3D.Fogos(centrosFogo); passoVisto.Clear(); ultHE = ultFlash = -1;
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

        void FechaRound() { long tm = Desempenho.Agora; pt.Fechar(m); if (Desempenho.Ativo) Desempenho.Soma(4, ref tm); if (pt.acabou) { acabou = true; pausado = true; Som3D.PlateiaFimPartida(); } else NovoRound(); }

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
            if (!Vitrine) Teclado();
            bool med = Desempenho.Ativo; long t0 = med ? Desempenho.Agora : 0; simMs = 0;
            Passo(dt, !pausado && !acabou, true);
            if (med) { Desempenho.Soma(1, ref t0); Desempenho.Desconta(1, simMs); Desempenho.Junta(0, simMs); }
            Efeitos();
            if (med) Desempenho.Soma(2, ref t0);
            hud.Atualizar(pt, m, rn, Observado, dt);
            if (med) Desempenho.Soma(3, ref t0);
            menu?.Atualizar(dt);
        }
        double simMs;   // tempo da simulação neste quadro (medição de desempenho)

        static string Arma(Agent a) => a.gunId switch
        {
            "ak" => "AK47", "m4" => "M4A4", "m4s" => "M4A1S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC10",
            "usp" => "USP", "glock" => "Glock", "deagle" => "Deagle",
            "tec9" => "Tec9", "fiveseven" => "FiveSeven", "p250" => "P250", "galil" => "Galil", "famas" => "Famas",
            _ => WeaponStats.Pistola(a.gunId) ? (a.IsT ? "Glock" : "USP") : Transmissao.Padrao(!a.IsT)
        };


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
            if (Input.GetKeyDown(KeyCode.R)) Hud3D.RadioTime = Hud3D.RadioTime == 0 ? 1 : Hud3D.RadioTime == 1 ? -1 : 0;   // rádio: time A, time B, desligado
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
            if (Vitrine || (menu != null && menu.Cobrindo)) return;   // menu/placar por cima: sem controles do jogo
            // clicar no card do HUD segue o jogador (o HUD é uGUI sem EventSystem: botão invisível por cima)
            for (int i = 0; i < 5; i++)
                foreach (bool timeA in new[] { true, false })
                {
                    var j = (timeA ? pt.A : pt.B).js[i];
                    var a = m.agents.FirstOrDefault(x => x.name == j.nome);
                    if (a != null && GUI.Button(hud.TelaDoCard(timeA, i), GUIContent.none, GUIStyle.none))
                    {
                        seguir = seguir == a.id ? -1 : a.id;
                        CSM.Audio.SomUI.Gui(Rect.zero, true);   // só o clique (sem estalinho passando por cima dos cards)
                    }
                }
            float k = Mathf.Max(1, Screen.height / 720f);
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            float W = Screen.width / k, H = Screen.height / k;
            // controles pequenos embaixo do minimapa (o canto de baixo é dos cards)
            float x = 14, y = 270;
            bool B(string t, float w) { var rr = new Rect(x, y, w, 22); bool r = GUI.Button(rr, t, sBotao); CSM.Audio.SomUI.Gui(rr, r); x += w + 4; return r; }
            if (B(pausado ? "Continuar" : "Pausar", 70)) pausado = !pausado && !acabou;
            foreach (float v in new[] { 1f, 2f, 4f, 8f }) if (B((velocidade == v ? ">" : "") + v + "x", 36)) velocidade = v;
            x = 14; y += 26;
            if (B("Próximo round", 96)) PulaRound();
            if (B(painel ? "Sem pensamento" : "Pensamento (P)", 110)) painel = !painel;
            // o que o jogador seguido está pensando (explicação dos bots)
            if (!painel || seguir < 0) return;
            var s = m.agents[seguir];
            const float PW = 300;
            GUI.Box(new Rect(W / 2 - PW / 2, H - 230, PW, 118), GUIContent.none, sCaixa);
            GUI.Label(new Rect(W / 2 - PW / 2 + 6, H - 227, PW - 12, 112), $"<b>{s.name}</b> — {s.brain.order.name}\n{s.brain.thought}\n<b>Por quê:</b> {s.brain.porqueAgora}", sTxt);
        }
    }
}
