using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace CSM.EditorTools
{
    /// <summary>
    /// Visual 3D (estilo maquete): cria o URP do projeto (asset + renderer com SSAO) e liga nas Graphics/Quality.
    /// Batch: Unity -batchmode -projectPath . -executeMethod CSM.EditorTools.Visual3D.SetupBatch -quit
    /// </summary>
    public static class Visual3D
    {
        public const string DIR = "Assets/Game/View3D/Render";
        public const string PIPE = DIR + "/Maquete_URP.asset", REND = DIR + "/Maquete_Renderer.asset";

        [MenuItem("CS Manager/Visual 3D/Configurar URP")]
        public static void Setup()
        {
            Directory.CreateDirectory(DIR);
            var rend = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(REND);
            if (rend == null)
            {
                rend = ScriptableObject.CreateInstance<UniversalRendererData>();
                var get = typeof(PostProcessData).GetMethod("GetDefaultPostProcessData",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Public);
                rend.postProcessData = (PostProcessData)get?.Invoke(null, null);
                AssetDatabase.CreateAsset(rend, REND);
            }
            if (rend.rendererFeatures.Find(f => f is ScreenSpaceAmbientOcclusion) == null)
            {
                var ao = ScriptableObject.CreateInstance<ScreenSpaceAmbientOcclusion>();
                ao.name = "SSAO";
                AssetDatabase.AddObjectToAsset(ao, rend);
                rend.rendererFeatures.Add(ao);
                var so = new SerializedObject(ao);
                void F(string p, float v) { var q = so.FindProperty("m_Settings." + p); if (q != null) q.floatValue = v; }
                void I(string p, int v) { var q = so.FindProperty("m_Settings." + p); if (q != null) q.intValue = v; }
                F("Intensity", 1.6f); F("Radius", 0.6f); F("DirectLightingStrength", 0.2f); F("Falloff", 400f);
                I("Samples", 0);   // alta
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rend);
                AssetDatabase.SaveAssets();
                var rs = new SerializedObject(rend);
                var map = rs.FindProperty("m_RendererFeatureMap");
                if (map != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(ao, out string _, out long lid))
                {
                    map.arraySize = rend.rendererFeatures.Count;
                    map.GetArrayElementAtIndex(map.arraySize - 1).longValue = lid;
                    rs.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            Silhuetas(rend);
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PIPE);
            if (pipe == null)
            {
                pipe = UniversalRenderPipelineAsset.Create(rend);
                AssetDatabase.CreateAsset(pipe, PIPE);
            }
            // sombra macia de maquete: cascata única cobrindo o enquadramento, resolução alta
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            pipe.shadowDistance = 90f;
            pipe.shadowCascadeCount = 2;
            pipe.mainLightShadowmapResolution = 4096;
            pipe.supportsHDR = true;
            pipe.supportsCameraDepthTexture = true;   // a smoke volumétrica para na parede/chão pela profundidade
            pipe.msaaSampleCount = 4;
            pipe.renderScale = 1f;
            var ps = new SerializedObject(pipe);
            var soft = ps.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
            var sq = ps.FindProperty("m_SoftShadowQuality"); if (sq != null) sq.intValue = 3;
            var sd = ps.FindProperty("m_ShadowDepthBias"); if (sd != null) sd.floatValue = 1.2f;
            var sn = ps.FindProperty("m_ShadowNormalBias"); if (sn != null) sn.floatValue = 0.8f;
            ps.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipe);

            GraphicsSettings.defaultRenderPipeline = pipe;
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipe;
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[Visual3D] URP configurado: " + PIPE);
        }

        public static void SetupBatch() { Setup(); EditorApplication.Exit(0); }

        /// <summary>Camada "Bonecos" fora do opaco normal + dois RenderObjects: 1) silhueta na cor do time onde o mapa
        /// tapa (ZTest Greater contra a profundidade só do mapa), 2) o boneco de verdade por cima. Assim a silhueta não
        /// pega o próprio corpo (o boneco ainda não está na profundidade quando ela é desenhada).</summary>
        static void Silhuetas(UniversalRendererData rend)
        {
            int L = Camada(CSM.View3D.Boneco3D.CAMADA);
            if (L < 0) { Debug.LogWarning("[Visual3D] sem camada livre pros bonecos"); return; }
            rend.opaqueLayerMask &= ~(1 << L);
            rend.transparentLayerMask &= ~(1 << L);
            const string S = "Silhueta dos bonecos", B = "Bonecos";
            if (rend.rendererFeatures.Find(f => f != null && f.name == S) == null)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Silhueta.mat");
                if (mat == null) { mat = new Material(Shader.Find("CSM/Silhueta")); AssetDatabase.CreateAsset(mat, DIR + "/Silhueta.mat"); }
                foreach (var (nome, sil) in new[] { (S, true), (B, false) })
                {
                    var ro = ScriptableObject.CreateInstance<RenderObjects>();
                    ro.name = nome;
                    ro.settings.Event = RenderPassEvent.AfterRenderingOpaques;
                    ro.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                    ro.settings.filterSettings.LayerMask = 1 << L;
                    if (sil)
                    {
                        ro.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
                        ro.settings.overrideMaterial = mat;
                        ro.settings.overrideDepthState = true; ro.settings.depthCompareFunction = UnityEngine.Rendering.CompareFunction.Greater; ro.settings.enableWrite = false;
                    }
                    else ro.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.None;
                    AssetDatabase.AddObjectToAsset(ro, rend);
                    rend.rendererFeatures.Add(ro);
                }
                EditorUtility.SetDirty(rend);
                AssetDatabase.SaveAssets();
                var rs = new SerializedObject(rend);
                var map = rs.FindProperty("m_RendererFeatureMap");
                if (map != null)
                {
                    map.arraySize = rend.rendererFeatures.Count;
                    for (int i = 0; i < rend.rendererFeatures.Count; i++)
                        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(rend.rendererFeatures[i], out string _, out long lid))
                            map.GetArrayElementAtIndex(i).longValue = lid;
                    rs.ApplyModifiedPropertiesWithoutUndo();
                }
            }
            EditorUtility.SetDirty(rend);
        }

        /// <summary>Índice da camada com esse nome (cria na primeira livre a partir da 8).</summary>
        static int Camada(string nome)
        {
            int l = LayerMask.NameToLayer(nome);
            if (l >= 0) return l;
            var tm = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
            var layers = tm.FindProperty("layers");
            for (int i = 8; i < layers.arraySize; i++)
            {
                var p = layers.GetArrayElementAtIndex(i);
                if (string.IsNullOrEmpty(p.stringValue)) { p.stringValue = nome; tm.ApplyModifiedPropertiesWithoutUndo(); return i; }
            }
            return -1;
        }

        /// <summary>
        /// Prints do visual 3D em Logs/visual3d (cena montada só na memória). Batch:
        /// Unity -batchmode -projectPath . -executeMethod CSM.EditorTools.Visual3D.CapturarBatch -logFile x.log
        /// (sem -nographics: precisa da GPU). Args opcionais: -v3d_sigma 1.1 -v3d_abertura 2.8 -v3d_focal 200
        /// </summary>
        [MenuItem("CS Manager/Visual 3D/Capturar prints")]
        public static void Capturar()
        {
            Setup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var cg = new GameObject("Câmera 3D") { tag = "MainCamera" };
            cg.AddComponent<Camera>();
            var cen = new GameObject("Cenário 3D").AddComponent<CSM.View3D.Cenario3D>();
            cen.arredondado = Arg("-v3d_sigma", cen.arredondado);
            cen.abertura = Arg("-v3d_abertura", cen.abertura);
            cen.focal = Arg("-v3d_focal", cen.focal);
            cen.Montar();
            Debug.Log($"[Visual3D] malha do mapa: {CSM.View3D.Cenario3D.msMalha:F0} ms, " +
                      $"{Tris(cen.mapa)} triângulos");
            Directory.CreateDirectory("Logs/visual3d");
            var A = CSM.Map.MapData.SITES["A"].Center; var B = CSM.Map.MapData.SITES["B"].Center;
            // bonecos de teste no bomb A: CT segurando/recarregando, TR entrando andando/agachado
            var bon = new GameObject("Bonecos").transform;
            void Por(string time, string anim, float u, float gx, float gy, float rumo)
            {
                if (!File.Exists($"{CSM.View3D.Boneco3D.PASTA}/boneco_{time}.json")) return;
                var b = CSM.View3D.Boneco3D.Criar(time, bon);
                int ix = Mathf.Clamp((int)gx, 0, CSM.Map.MapData.NX - 1), iy = Mathf.Clamp((int)gy, 0, CSM.Map.MapData.NY - 1);
                b.transform.position = CSM.View3D.MalhaMapa.ParaUnity(gx, gy, CSM.Map.MapData.floor[ix, iy]);
                b.transform.rotation = Quaternion.Euler(0, rumo, 0);
                if (!b.Tem(anim)) Debug.LogWarning("[Visual3D] sem animação " + anim);
                b.Pose(anim, u);
            }
            Por("ct", "mira_M4A4", 0, A.x + 30, A.y - 20, 225);
            Por("ct", "recarga_M4A1S", 0.45f, A.x + 48, A.y + 8, 250);
            Por("ct", "agachado_AWP", 0.3f, A.x - 10, A.y - 40, 200);
            Por("tr", "andar_AK47", 0.25f, A.x - 45, A.y + 35, 45);
            Por("tr", "tiro_AK47", 0.3f, A.x - 30, A.y + 55, 30);
            Por("tr", "granada_smoke", 0.55f, A.x - 62, A.y + 20, 60);
            // escada de mão do janelão: um CT subindo (a escada sobe pro oeste)
            var esc = CSM.Map.MapData.areas.Find(s => s.name == "Esc_janelao").box;
            Por("ct", "escada", 0.3f, (esc.x0 + esc.x1) / 2f, (esc.y0 + esc.y1) / 2f, 270);
            // na escada o pé fica na altura do degrau (o piso ali é a praça embaixo)
            var bEsc = bon.GetChild(bon.childCount - 1);
            bEsc.position += Vector3.up * ((CSM.Map.MapData.UP + 6 - CSM.Map.MapData.GZ) * 0.5f * CSM.View3D.MalhaMapa.ESC);
            CSM.View3D.PartidaAoVivo3D.NaEscada(bEsc);
            var tiros = new (string nome, Vector2 alvo, float quadro)[]
            {
                ("escada", esc.Center is var ec ? new Vector2(ec.x, ec.y) : Vector2.zero, 4.5f),
                ("perto_a", new Vector2(A.x - 5, A.y + 5), 5f),
                ("bomb_a", new Vector2(A.x, A.y), 12f),
                ("bomb_b", new Vector2(B.x, B.y), 12f),
                ("meio", new Vector2(CSM.Map.MapData.NX * 0.5f, CSM.Map.MapData.NY * 0.5f), 14f),
                ("mapa", new Vector2(CSM.Map.MapData.NX * 0.5f, CSM.Map.MapData.NY * 0.55f), 46f),
            };
            foreach (var t in tiros)
            {
                cen.alvoJogo = t.alvo; cen.enquadrar = t.quadro; cen.Enquadrar();
                Salvar(cen.cam, $"Logs/visual3d/{t.nome}.png", 1920, 1080);
            }
            Debug.Log("[Visual3D] prints em Logs/visual3d");
        }

        /// <summary>Partida de verdade no visual 3D, fora do Play: simula e tira print em vários momentos do round.</summary>
        [MenuItem("CS Manager/Visual 3D/Capturar partida")]
        public static void CapturarPartida()
        {
            Setup();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
            var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
            p.seed = (int)Arg("-v3d_seed", 15);
            p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
            Directory.CreateDirectory("Logs/visual3d");
            float[] em = { 6, 14, 24, 34, 44, 54, 64, 74 };
            float t = 0;
            for (int i = 0; i < em.Length; i++)
            {
                // metade dos prints segue um jogador vivo (mostra o painel do observado e a borda no card)
                var vivo = p.Partida.agents.FirstOrDefault(a => a.alive && a.brain.target != null) ?? p.Partida.agents.FirstOrDefault(a => a.alive);
                p.seguir = i % 2 == 1 && vivo != null ? vivo.id : -1;
                p.Simular(em[i] - t); t = em[i];
                Salvar(p.Cenario.cam, $"Logs/visual3d/partida_{i}_{p.Partida.t:00}s.png", 1920, 1080, p.Hud);
                if (p.Partida.over) { p.Simular(1.2f); Salvar(p.Cenario.cam, $"Logs/visual3d/partida_fim.png", 1920, 1080, p.Hud); break; }
            }
            Debug.Log("[Visual3D] partida capturada");
        }

        /// <summary>
        /// Vídeo da partida 3D: simula a 30 qps e grava os quadros em Logs/visual3d/video (depois o frames_to_mp4.py do
        /// EsportsPoc junta). Args: -v3d_seed 15 -v3d_de 18 (s do round) -v3d_dur 20 (s) -v3d_zoom 7.5
        /// </summary>
        public static void CapturarVideoBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.seed = (int)Arg("-v3d_seed", 15);
                p.zoom = Arg("-v3d_zoom", p.zoom);
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                p.PularAte((int)Arg("-v3d_round", 1));
                // -v3d_seguir NOME: a câmera segue esse jogador (ex.: quem sobe a escada)
                var ar = System.Environment.GetCommandLineArgs();
                int si = System.Array.IndexOf(ar, "-v3d_seguir");
                string seguirNome = si >= 0 && si + 1 < ar.Length ? ar[si + 1] : null;
                float de = Arg("-v3d_de", 18), dur = Arg("-v3d_dur", 20);
                const float DT = 1 / 30f;
                p.Simular(de, DT);
                string dir = "Logs/visual3d/video";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                var sw = System.Diagnostics.Stopwatch.StartNew();
                int n = Mathf.RoundToInt(dur * 30);
                int r0 = p.Round; videoRodando = true;
                for (int i = 0; i < n; i++)
                {
                    // segue pelo fim do round, o replay e o começo do round seguinte (painel de economia)
                    if (p.Round > r0 && p.Partida.t > 8f) break;
                    if (seguirNome != null) { int id = p.Partida.agents.FindIndex(a => a.name == seguirNome); p.seguir = id >= 0 && p.Partida.agents[id].alive ? id : -1; }
                    p.Simular(DT, DT);
                    Salvar(p.Cenario.cam, $"{dir}/q_{i:0000}.png", 1280, 720, p.Hud);
                }
                Debug.Log($"[Visual3D] vídeo: {n} quadros em {sw.Elapsed.TotalSeconds:F0} s");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Galeria: uma fila de bonecos no bomb A, cada um repetindo uma animação (nome em cima), gravada em vídeo
        /// (Logs/visual3d/galeria). Serve pra conferir cada animação de perto, sem depender da partida acontecer.
        /// </summary>
        public static void CapturarGaleriaBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cg = new GameObject("Câmera 3D") { tag = "MainCamera" }; cg.AddComponent<Camera>();
                var cen = new GameObject("Cenário 3D").AddComponent<CSM.View3D.Cenario3D>();
                cen.Montar(); cen.cam.aspect = 16f / 9f;
                // estúdio: some o mapa e os arredores, fica um chão liso creme com a mesma luz de maquete
                cen.mapa.SetActive(false);
                foreach (Transform f in cen.transform) if (f.name == "Arredores") f.gameObject.SetActive(false);
                var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
                chao.transform.position = new Vector3(0, 0, 0); chao.transform.localScale = Vector3.one * 4;
                var mc = new Material(Shader.Find("CSM/Maquete")); mc.SetFloat("_UsarVertice", 0); mc.SetColor("_BaseColor", new Color(0.93f, 0.86f, 0.72f).linear);
                chao.GetComponent<MeshRenderer>().sharedMaterial = mc;
                // (time, animação, extra, rótulo)
                var itens = new (string time, string anim, string extra, string rotulo)[]
                {
                    // o que mudou nesta rodada (validador de animação + pedidos do usuário)
                    ("tr", "sacar_costas_AK47", null, "saca das costas"), ("tr", "plantar", null, "plantar C4"),
                    ("tr", "faca_parado", null, "faca de lado"), ("ct", "awp_mirando", null, "AWP luneta CT"),
                    ("ct", "mira_M4A4", null, "máscara CT"), ("tr", "awp_mirando", null, "AWP luneta TR"),
                    ("ct", "desarmar", null, "desarmar C4"), ("tr", "faca_parado", null, "faca parado"),
                    ("ct", "faca_andar", null, "faca correndo"), ("tr", "sacar_Deagle", null, "sacar do coldre"),
                    ("tr", "andar_AK47", null, "coldre andando"), ("ct", "pegar_M4A4", null, "pegar do chão"),
                    ("ct", "no_ar_cai_M4A4", null, "caindo (janelão)"), ("tr", "mira_AK47", "ragdoll", "morte (ragdoll)"),
                };
                // -v3d_pagina 0..3: check-up de TODAS as animações (12 por página)
                var todas = new (string time, string anim, string extra, string rotulo)[]
                {
                    ("ct", "mira_M4A4", null, "parado (respira)"), ("tr", "andar_AK47", null, "correndo"), ("ct", "devagar_M4A4", null, "andando (shift)"),
                    ("tr", "agachado_AK47", null, "agachado andando"), ("ct", "agachar_M4A4", null, "agachado parado"), ("tr", "andar_AK47", "strafe", "strafe (A/D)"),
                    ("ct", "andar_M4A4", "costas", "de costas"), ("tr", "pular", null, "pulo"), ("ct", "no_ar_cai_M4A4", null, "caindo"),
                    ("tr", "escada", null, "escada de mão"), ("ct", "faca_andar", null, "faca correndo"), ("tr", "faca_parado", null, "faca parado"),

                    ("tr", "tiro_AK47", "rajada", "tiro AK"), ("ct", "tiro_AWP", null, "tiro AWP (ferrolho)"), ("tr", "tiro_Glock", "rajada", "tiro Glock"),
                    ("tr", "recarga_AK47", null, "recarga AK"), ("ct", "recarga_AWP", null, "recarga AWP"), ("ct", "recarga_USP", null, "recarga USP"),
                    ("ct", "sacar_M4A4", null, "sacar fuzil"), ("tr", "sacar_Deagle", null, "sacar Deagle"), ("ct", "awp_mirar", null, "luneta AWP"),
                    ("ct", "mira_M4A4", "pitchcima", "mira pra cima"), ("tr", "faca_m1", null, "faca corte"), ("ct", "faca_m2", null, "faca estocada"),

                    ("ct", "granada_smoke", null, "smoke"), ("tr", "granada_flash", null, "flash"), ("tr", "granada_molotov", null, "molotov"),
                    ("ct", "granada_he", null, "HE"), ("tr", "plantar", null, "plantar"), ("ct", "desarmar", null, "desarmar"),
                    ("tr", "pegar_AK47", null, "pegar do chão"), ("ct", "apontar_M4A4", null, "call (aponta)"), ("tr", "cego_AK47", null, "cego"),
                    ("ct", "meio_cego_M4A4", null, "meio cego"), ("tr", "morrer", null, "morre de costas"), ("ct", "morrer_frente", null, "morre de frente"),

                    ("tr", "comemorar_AK47", null, "comemorar"), ("ct", "lamentar_M4A4", null, "lamentar"), ("ct", "mira_M4A4", "golpe", "levou tiro"),
                    ("tr", "tiro_AK47", "agachado", "tiro agachado"), ("ct", "recarga_M4A4", "andar_M4A4", "recarga correndo"), ("tr", "sacar_faca", null, "saca a faca"),
                    ("ct", "no_ar_sobe_AWP", null, "subindo (pulo)"), ("tr", "plantar_pe", null, "plantar em pé"), ("ct", "desarmar_pe", null, "desarmar em pé"),
                    ("tr", "devagar_Glock", null, "shift de pistola"), ("ct", "agachado_MP9", null, "agachado de MP9"), ("tr", "andar_MAC10", null, "correndo de MAC-10"),
                };
                int pg = (int)Arg("-v3d_pagina", -1);
                if (pg >= 0) itens = todas.Skip(pg * 12).Take(12).ToArray();
                // -v3d_itens 0,1,2,3: só esses (mais perto); -v3d_zoom: enquadramento
                var argv = System.Environment.GetCommandLineArgs(); int ai = System.Array.IndexOf(argv, "-v3d_itens");
                if (ai >= 0 && ai + 1 < argv.Length) itens = argv[ai + 1].Split(',').Select(x => itens[int.Parse(x)]).ToArray();
                var bs = new CSM.View3D.Boneco3D[itens.Length];
                var rags = new CSM.View3D.Ragdoll3D[itens.Length];
                CSM.View3D.Ragdoll3D.PisoFixo = 0f;   // estúdio: chão liso em y = 0
                var rotulos = new Transform[itens.Length];
                var pai = new GameObject("Galeria").transform;
                var fonte = CSM.View3D.UiKit.Fonte;
                for (int i = 0; i < itens.Length; i++)
                {
                    int col = i % 4, lin = i / 4;
                    if (itens.Length <= 4) { col = i; lin = 1; }
                    // grade 4 x 3 de frente pra câmera (a câmera olha pro noroeste: direita da tela = nordeste)
                    var eixo = new Vector3(1, 0, 1).normalized; var fundo = new Vector3(-1, 0, 1).normalized;
                    var b = CSM.View3D.Boneco3D.Criar(itens[i].time, pai);
                    b.transform.position = eixo * ((col - (System.Math.Min(itens.Length, 4) - 1) / 2f) * (itens.Length <= 4 ? 1.1f : 2.0f)) + fundo * ((1 - lin) * 1.6f);
                    b.transform.rotation = Quaternion.Euler(0, Arg("-v3d_rot", 105), 0);   // três quartos: vê a arma e as pernas
                    var tg = new GameObject("rótulo"); tg.transform.SetParent(pai, false);
                    tg.transform.position = b.transform.position - fundo * 0.55f + Vector3.up * 0.02f;
                    var tm = tg.AddComponent<TextMesh>(); tm.text = itens[i].rotulo; tm.font = fonte; tm.fontSize = 64; tm.characterSize = 0.034f;
                    tm.anchor = TextAnchor.MiddleCenter; tm.color = itens[i].time == "ct" ? new Color(0.75f, 0.88f, 1f) : new Color(1f, 0.85f, 0.6f);
                    tg.GetComponent<MeshRenderer>().sharedMaterial = fonte.material;
                    bs[i] = b; rotulos[i] = tg.transform;
                }
                cen.alvoJogo = new Vector2(0, -0.3f / CSM.View3D.MalhaMapa.ESC); cen.enquadrar = Arg("-v3d_zoom", itens.Length <= 4 ? 3.6f : 6.4f); cen.Enquadrar();
                // o Enquadrar mira no chão do mapa (GZ); no estúdio o chão está em 0
                cen.cam.transform.position -= Vector3.up * (CSM.Map.MapData.GZ * CSM.View3D.MalhaMapa.ESC);
                foreach (var r in rotulos) r.rotation = cen.cam.transform.rotation;
                string dir = "Logs/visual3d/galeria";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                const float DT = 1 / 30f; int N = (int)Arg("-v3d_quadros", 180);
                videoRodando = true;
                for (int f = 0; f < N; f++)
                {
                    float t = f * DT;
                    for (int i = 0; i < bs.Length; i++)
                    {
                        var b = bs[i]; var it = itens[i];
                        float dur = b.Duracao(it.anim); bool loop = b.modelo.anims[it.anim].loop;
                        float u = loop ? t / dur : Mathf.Repeat(t, dur + 0.8f) / dur;   // ação: toca, segura 0,8 s, repete
                        b.pernaAnim = null; b.troncoY = 0; b.misturaW = 0; b.yawPernas = 0; b.pitch = 0;
                        if (it.extra == "strafe") b.yawPernas = 75;
                        if (it.extra == "costas") u = -t / dur;
                        if (it.extra == "pitchcima") { b.pitch = 25; b.armaPitch = "M4A4"; }
                        if (it.extra == "rajada" || it.extra == "coice")
                        {   // tiro atrás de tiro; cada tiro dá o coice no corpo
                            float ciclo = it.extra == "coice" ? dur + 0.4f : dur + 0.12f;
                            u = Mathf.Repeat(t, ciclo) / dur;
                            if (Mathf.Repeat(t, ciclo) < DT) b.Disparo(it.extra == "coice" ? 1f : 0.5f);
                        }
                        if (it.extra == "fim")
                        {   // arremesso (o Sim solta em u = 1) e a continuação do braço, segura e repete
                            float ta = Mathf.Repeat(t, 0.9f + 0.4f + 0.7f);
                            if (ta < 0.9f) { b.Pose(it.anim, ta / 0.9f, DT); continue; }
                            b.Pose("granada_fim", Mathf.Min(1, (ta - 0.9f) / 0.4f), DT); continue;
                        }
                        if (it.extra == "ragdoll")
                        {   // de pé/correndo, leva o tiro, cai em ragdoll; repete a cada 3,4 s
                            float ta = Mathf.Repeat(t, 3.4f);
                            if (ta < 0.7f)
                            {
                                if (rags[i] != null) rags[i] = null;
                                b.semArma = false; b.Pose(it.anim, it.anim.StartsWith("andar") ? t * 1.5f : 0, DT); continue;
                            }
                            if (rags[i] == null)
                            {
                                b.semArma = true; b.Pose(b.animAtual, b.uAtual, 0);
                                rags[i] = new CSM.View3D.Ragdoll3D(b, -b.transform.forward, it.anim.StartsWith("andar") ? 0.45f : 0.28f, i * 13 + f);
                            }
                            rags[i].Passo(DT); continue;
                        }
                        if (it.extra == "agachado") { b.pernaAnim = "agachar_AK47"; b.troncoY = -0.1f; u = Mathf.Repeat(t, 0.3f) / 0.25f; }
                        if (it.extra == "andar_M4A4") { b.pernaAnim = it.extra; b.pernaU = t * 2.4f; }
                        if (it.extra == "meiocego") { b.misturaAnim = "cego"; b.misturaU = t / b.Duracao("cego"); b.misturaW = 0.5f; }
                        if (it.extra == "golpe" && Mathf.Repeat(t, 1.2f) < DT) b.Golpe(0.9f);
                        b.brancoCego = 0;
                        if (it.anim == "awp_mirar")
                        {
                            float ta = Mathf.Repeat(t, 2.6f);
                            if (ta > 0.3f) { b.Pose("awp_mirando", 0, DT); continue; }
                            u = ta / 0.27f;
                        }
                        b.Pose(it.anim, u, DT);
                    }
                    Salvar(cen.cam, $"{dir}/q_{f:0000}.png", 1280, 720);
                }
                Debug.Log($"[Visual3D] galeria: {N} quadros");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Laboratório de smoke e física (Logs/visual3d/lab): smoke estoura no bomb A; rajada atravessando (furos que
        /// fecham); HE dentro (bolha que volta a encher); molotov do lado que é apagada (vapor); armas no chão e C4
        /// explodindo do lado (itens voam). Mesmo desenho do jogo, sem depender de um round acontecer.
        /// </summary>
        public static void CapturarLabBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center;
                float z = CSM.Map.NavGrid.ZAt(new CSM.Sim.V2(A.x, A.y));
                var o = new CSM.View.V3(A.x - 10, A.y + 10, z);
                p.LabCamera(new CSM.Sim.V2(A.x - 5, A.y + 15), 8.5f);
                string dir = "Logs/visual3d/lab";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                const float DT = 1 / 30f; int N = (int)Arg("-v3d_quadros", 540);
                CSM.View.FireArea fogo = null; bool feito1 = false, feito2 = false, feito3 = false, feito4 = false, feito5 = false;
                int tiros = 0; videoRodando = true;
                for (int f = 0; f < N; f++)
                {
                    float t = f * DT;
                    if (!feito1 && t >= 0.2f) { feito1 = true; p.LabSmoke(o); foreach (var (dx, dy) in new[] { (60, -40), (75, -20), (55, -60) }) p.LabItem(dx == 60 ? "ak" : dx == 75 ? "awp" : "glock", new CSM.Sim.V2(A.x + dx, A.y + dy)); }
                    // rajada atravessando a smoke de oeste pra leste, subindo um pouco (5 balas a cada 0,1 s)
                    if (t >= 4.0f && tiros < 9 && t >= 4.0f + tiros * 0.11f)
                    {
                        float h = z + 14 + (tiros % 3) * 4; float yy = o.y - 12 + tiros * 3;
                        p.LabTiro(new CSM.View.V3(o.x - 110, yy, h), new CSM.View.V3(o.x + 110, yy + 8, h - 2)); tiros++;
                    }
                    if (!feito2 && t >= 7.0f) { feito2 = true; p.LabHE(new CSM.View.V3(o.x + 4, o.y - 4, z + 8)); }
                    if (!feito3 && t >= 10.5f) { feito3 = true; fogo = p.LabFogo(new CSM.View.V3(o.x - 40, o.y - 45, z)); }
                    if (!feito4 && t >= 12.6f) { feito4 = true; p.LabApagaFogo(fogo); }
                    if (!feito5 && t >= 14.5f) { feito5 = true; p.LabC4(new CSM.View.V3(A.x + 55, A.y - 25, z)); }
                    p.LabPasso(DT);
                    Salvar(p.Cenario.cam, $"{dir}/q_{f:0000}.png", 1280, 720);
                }
                Debug.Log($"[Visual3D] laboratório: {N} quadros");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Só simulação: em que round/tempo alguém sobe a escada de mão do janelão (pra escolher o vídeo).</summary>
        public static void AcharEscadaBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou)
            {
                var m = pt.Preparar(); int r = pt.r + 1; var vistos = new System.Collections.Generic.HashSet<int>();
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    foreach (var a in m.agents)
                        if (a.alive && !a.noAr && CSM.Map.NavGrid.Escada(a.pos) && a.z > CSM.Map.MapData.GZ + 1.5f && vistos.Add(a.id))
                            Debug.Log($"[Visual3D] ESCADA seed {seed} round {r} t={m.t:0.0} {a.name} ({(a.IsT ? "TR" : "CT")})");
                }
                pt.Fechar(m);
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Só simulação: em que round/tempo alguém pega arma/granada/C4 do chão (pra escolher o vídeo da troca).</summary>
        public static void AcharTrocaBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou)
            {
                var m = pt.Preparar(); int r = pt.r + 1, vistos = 0;
                var trocas = m.agents.Select(a => a.trocouArma).ToArray();
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    for (int i = 0; i < m.agents.Count; i++)
                        if (m.agents[i].trocouArma != trocas[i]) { trocas[i] = m.agents[i].trocouArma; Debug.Log($"[Visual3D] TROCA(Q) seed {seed} round {r} t={m.t:0.0} {m.agents[i].name} -> {m.agents[i].gunId}"); }
                    for (; vistos < m.log.Count; vistos++)
                        if (m.log[vistos].Contains("do chão") || m.log[vistos].Contains("pegou o bomb"))
                            Debug.Log($"[Visual3D] PEGOU seed {seed} round {r} t={m.t:0.0} {m.log[vistos]}");
                }
                pt.Fechar(m);
            }
            EditorApplication.Exit(0);
        }

        public static void CapturarPartidaBatch()
        {
            try { CapturarPartida(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public const string CENA3D = "Assets/Scenes/Partida3D.unity";

        [MenuItem("CS Manager/Visual 3D/Rodar partida ao vivo 3D")]
        public static void Rodar3D()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(CENA3D)) CriarCena3D();
            EditorSceneManager.OpenScene(CENA3D);
            EditorApplication.isPlaying = true;
        }

        [MenuItem("CS Manager/Visual 3D/Recriar cena 3D")]
        public static void CriarCena3D()
        {
            Setup();
            Directory.CreateDirectory("Assets/Scenes");
            var s = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
            new GameObject("Partida ao vivo 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
            EditorSceneManager.SaveScene(s, CENA3D);
        }

        /// <summary>Deixa o projeto pronto pro Play: recria a cena 3D, põe ela em primeiro no Build Settings e abre ela
        /// (o editor reabre a última cena aberta).</summary>
        public static void PrepararPlay3DBatch()
        {
            try
            {
                CriarCena3D();
                var cenas = new System.Collections.Generic.List<EditorBuildSettingsScene> { new EditorBuildSettingsScene(CENA3D, true) };
                cenas.AddRange(EditorBuildSettings.scenes.Where(c => c.path != CENA3D));
                EditorBuildSettings.scenes = cenas.ToArray();
                EditorSceneManager.OpenScene(CENA3D);
                AssetDatabase.SaveAssets();
                Debug.Log("[Visual3D] cena 3D pronta pro Play: " + CENA3D);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CapturarBatch()
        {
            try { Capturar(); EditorApplication.Exit(0); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static float Arg(string nome, float padrao)
        {
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == nome && float.TryParse(a[i + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) return v;
            return padrao;
        }

        static long Tris(GameObject g)
        {
            long n = 0;
            foreach (var mf in g.GetComponentsInChildren<MeshFilter>()) n += mf.sharedMesh.triangles.LongLength / 3;
            return n;
        }

        static bool aquecido, videoRodando;
        static void Salvar(Camera c, string arq, int w, int h, CSM.View3D.Hud3D hud = null)
        {
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
            c.targetTexture = rt;
            c.Render(); if (!aquecido) c.Render();   // o 1º print renderiza 2x (aquece sombra/SSAO); no vídeo o quadro anterior já aqueceu
            aquecido = videoRodando;
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = old; c.targetTexture = null;
            if (hud != null)
            {
                // HUD numa textura transparente (câmera própria, sem pós) e composto por cima pela alfa
                var rh = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                hud.canvas.renderMode = RenderMode.ScreenSpaceCamera; hud.canvas.worldCamera = hud.cam;
                hud.cam.targetTexture = rh; Canvas.ForceUpdateCanvases(); hud.cam.Render(); hud.cam.targetTexture = null;
                RenderTexture.active = rh;
                var th = new Texture2D(w, h, TextureFormat.RGBA32, false); th.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                RenderTexture.active = old;
                hud.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var a = tex.GetPixels32(); var b = th.GetPixels32();
                for (int i = 0; i < a.Length; i++)
                {
                    int al = b[i].a; if (al == 0) continue;
                    a[i] = new Color32((byte)((b[i].r * al + a[i].r * (255 - al)) / 255), (byte)((b[i].g * al + a[i].g * (255 - al)) / 255), (byte)((b[i].b * al + a[i].b * (255 - al)) / 255), 255);
                }
                tex.SetPixels32(a); tex.Apply();
                Object.DestroyImmediate(th); rh.Release(); Object.DestroyImmediate(rh);
            }
            // PNG sem alfa (a alfa do render 3D não é 1 em todo pixel)
            var opaco = new Texture2D(w, h, TextureFormat.RGB24, false);
            opaco.SetPixels32(tex.GetPixels32()); opaco.Apply();
            Object.DestroyImmediate(tex); tex = opaco;
            File.WriteAllBytes(arq, tex.EncodeToPNG());
            Object.DestroyImmediate(tex); rt.Release(); Object.DestroyImmediate(rt);
        }
    }
}
