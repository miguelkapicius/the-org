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
        public const string DIR = "Assets/Game/Visual/Render";
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
            // SSAO de maquete: só o contato curto e liso (o ruído azul granulava e o raio grande sujava o pé da parede de "fuligem")
            var aoF = rend.rendererFeatures.Find(f => f is ScreenSpaceAmbientOcclusion);
            if (aoF != null)
            {
                var so = new SerializedObject(aoF);
                void F2(string p, float v) { var q = so.FindProperty("m_Settings." + p); if (q != null) q.floatValue = v; }
                var met = so.FindProperty("m_Settings.AOMethod"); if (met != null) met.intValue = 1;   // gradiente intercalado
                F2("Intensity", 1.25f); F2("Radius", 0.42f); F2("DirectLightingStrength", 0.2f);
                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(rend);
            }
            Silhuetas(rend);
            IncluirShaders();
            NaturezaImport.Montar();   // prefabs das plantas do pack (Resources/Natureza)
            GranadasImport.Montar();   // prefabs das granadas do kit (Resources/Granadas)
            var pipe = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PIPE);
            if (pipe == null)
            {
                pipe = UniversalRenderPipelineAsset.Create(rend);
                AssetDatabase.CreateAsset(pipe, PIPE);
            }
            // sombra macia de maquete: cascata única cobrindo o enquadramento, resolução alta
            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;
            // a câmera de jogo fica a ~21 (prints a ~40): 1ª cascata até 30 cobre o jogo, a 2ª (30-75) os prints;
            // com 90 a 2ª cascata era larga demais e a borda da sombra saía em degrau (com 55 o fim do print já esmaecia)
            pipe.shadowDistance = 75f;
            pipe.shadowCascadeCount = 2;
            pipe.cascade2Split = 0.4f;
            pipe.cascadeBorder = 0.08f;
            pipe.mainLightShadowmapResolution = 8192;   // arma e vara de carroça têm ~2 cm: com 4096 mal davam 2 texels
            pipe.supportsHDR = true;
            pipe.supportsCameraDepthTexture = true;   // a smoke volumétrica para na parede/chão pela profundidade
            pipe.msaaSampleCount = 4;
            pipe.renderScale = 1f;
            var ps = new SerializedObject(pipe);
            var soft = ps.FindProperty("m_SoftShadowsSupported"); if (soft != null) soft.boolValue = true;
            var sq = ps.FindProperty("m_SoftShadowQuality"); if (sq != null) sq.intValue = 3;
            var sd = ps.FindProperty("m_ShadowDepthBias"); if (sd != null) sd.floatValue = 0.5f;   // bias baixo: arma, vara da carroça e raio de roda são finos (com 1,2 / 0,8 a sombra deles sumia picotada)
            var sn = ps.FindProperty("m_ShadowNormalBias"); if (sn != null) sn.floatValue = 0.3f;
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
            // 3) silhueta por cima da smoke (depois dos transparentes, na medida da smoke na frente: Silhueta.shader passe 1)
            const string S = "Silhueta dos bonecos", B = "Bonecos", F = "Silhueta na smoke";
            var faltam = new System.Collections.Generic.List<(string nome, int passe)>();
            if (rend.rendererFeatures.Find(f => f != null && f.name == S) == null) { faltam.Add((S, 0)); faltam.Add((B, -1)); }
            if (rend.rendererFeatures.Find(f => f != null && f.name == F) == null) faltam.Add((F, 1));
            if (faltam.Count > 0)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(DIR + "/Silhueta.mat");
                if (mat == null) { mat = new Material(Shader.Find("CSM/Silhueta")); AssetDatabase.CreateAsset(mat, DIR + "/Silhueta.mat"); }
                foreach (var (nome, passe) in faltam)
                {
                    var ro = ScriptableObject.CreateInstance<RenderObjects>();
                    ro.name = nome;
                    ro.settings.Event = passe == 1 ? RenderPassEvent.AfterRenderingTransparents : RenderPassEvent.AfterRenderingOpaques;
                    ro.settings.filterSettings.RenderQueueType = RenderQueueType.Opaque;
                    ro.settings.filterSettings.LayerMask = 1 << L;
                    if (passe >= 0)
                    {
                        ro.settings.overrideMode = RenderObjects.RenderObjectsSettings.OverrideMaterialMode.Material;
                        ro.settings.overrideMaterial = mat; ro.settings.overrideMaterialPassIndex = passe;
                        ro.settings.overrideDepthState = true; ro.settings.enableWrite = false;
                        ro.settings.depthCompareFunction = passe == 1 ? UnityEngine.Rendering.CompareFunction.LessEqual : UnityEngine.Rendering.CompareFunction.Greater;
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
            // a silhueta na smoke mede a smoke na frente do boneco (Fumaca.hlsl): só a parte visível dele (antes era Always + stencil)
            if (rend.rendererFeatures.Find(f => f != null && f.name == F) is RenderObjects rf)
                rf.settings.depthCompareFunction = UnityEngine.Rendering.CompareFunction.LessEqual;
            EditorUtility.SetDirty(rend);
        }

        /// <summary>Shaders que o jogo acha pelo nome (Shader.Find) entram sempre na build: sem isso a build abria sem mapa,
        /// sem bonecos e sem smoke (nada referencia esses shaders num asset da cena).</summary>
        static void IncluirShaders()
        {
            var gs = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var lista = gs.FindProperty("m_AlwaysIncludedShaders");
            bool mudou = false;
            foreach (var nome in new[] { "CSM/Maquete", "CSM/Nuvem", "CSM/Brilho", "CSM/Disco", "CSM/FumacaVolume", "CSM/Silhueta", "CSM/Mancha", "TextMeshPro/Distance Field" })
            {
                var sh = Shader.Find(nome);
                if (sh == null) { Debug.LogWarning("[Visual3D] shader não achado: " + nome); continue; }
                bool tem = false;
                for (int i = 0; i < lista.arraySize; i++) if (lista.GetArrayElementAtIndex(i).objectReferenceValue == sh) { tem = true; break; }
                if (tem) continue;
                lista.InsertArrayElementAtIndex(lista.arraySize); lista.GetArrayElementAtIndex(lista.arraySize - 1).objectReferenceValue = sh; mudou = true;
            }
            if (mudou) { gs.ApplyModifiedPropertiesWithoutUndo(); AssetDatabase.SaveAssets(); }
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
            // -v3d_semtopo 1: chão só com a cor de vértice (comparar com a textura de cima)
            if (Arg("-v3d_semtopo", 0) > 0) CSM.View3D.Cenario3D.matMapa.SetFloat("_UsarTopo", 0);
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

        /// <summary>Laboratório do apoio no chão (PartidaAoVivo3D.LabApoio): granadas, kit e armas caem, assentam e cada um
        /// loga a folga do ponto mais baixo pro chão ("[Visual3D] APOIO"); print em Logs/visual3d/apoio.png.</summary>
        public static void CapturarApoioBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22, A.y + 18);
                p.LabCamera(c, Arg("-v3d_zoom", 1.7f));
                p.LabApoio(c);
                for (int f = 0; f < 150; f++) p.LabPasso(1 / 30f);
                p.LabApoioMedir();
                Directory.CreateDirectory("Logs/visual3d");
                Salvar(p.Cenario.cam, "Logs/visual3d/apoio.png", 1920, 1080);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Laboratório de tiros (PartidaAoVivo3D.LabTiros): 5 CTs levando um tiro cada — capacete, cabeça sem capacete,
        /// corpo, morte por HS, morte no corpo — em quadros (Logs/visual3d/tiros). -v3d_giro, -v3d_zoom, -v3d_quadros.</summary>
        public static void CapturarTirosBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22 + Arg("-v3d_dx", 0), A.y + 18 + Arg("-v3d_dy", 0));
                p.LabCamera(c, Arg("-v3d_zoom", 2.4f));
                var ar = System.Environment.GetCommandLineArgs(); int qi = System.Array.IndexOf(ar, "-v3d_casos");
                p.LabTiros(c, Arg("-v3d_giro", 120), 0.3f, qi >= 0 && qi + 1 < ar.Length ? ar[qi + 1] : "0,1,2,3,4");
                string dir = "Logs/visual3d/tiros";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                int N = (int)Arg("-v3d_quadros", 75);
                for (int f = 0; f < N; f++) { p.LabPasso(1 / 30f); Salvar(p.Cenario.cam, $"{dir}/q_{f:0000}.png", 1280, 720); }
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Vídeo da partida 3D: simula a 30 qps e grava os quadros em Logs/visual3d/video (depois o frames_to_mp4.py do
        /// EsportsPoc junta). Args: -v3d_seed 15 -v3d_de 18 (s do round) -v3d_dur 20 (s) -v3d_zoom 7.5
        /// </summary>
        public static void CapturarVideoBatch() => CapturarVideo();

        /// <summary>
        /// Laboratório de lance real, com gatilho na mesma execução (a Sim não repete a partida igual entre execuções):
        /// roda a partida a partir de -v3d_round e, quando acontece o lance, para a câmera em cima e grava -v3d_dur s em
        /// Logs/visual3d/video. -v3d_lance item (granada/kit caindo de quem morreu; grava o rolo e quem passa pegando) ou
        /// jump (bot armando um jump throw). -v3d_rounds N: quantos rounds procura. Log "[Visual3D] LANCE ...".
        /// </summary>
        public static void CapturarLanceBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.seed = (int)Arg("-v3d_seed", 15); p.zoom = Arg("-v3d_zoom", 1.8f);
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                int r0 = (int)Arg("-v3d_round", 2), rounds = (int)Arg("-v3d_rounds", 6);
                p.PularAte(r0);
                var ar = System.Environment.GetCommandLineArgs(); int li = System.Array.IndexOf(ar, "-v3d_lance");
                string lance = li >= 0 && li + 1 < ar.Length ? ar[li + 1] : "item";
                float dur = Arg("-v3d_dur", 6); const float DT = 1 / 30f;
                CSM.View3D.Hud3D.SemEconomia = true;
                string dir = "Logs/visual3d/video";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                bool achou = false; int guarda = 0;
                // a venda é no tempo de compra: pula round a round sem desenhar até um que tenha venda (no loop desenhado, a
                // fumaça sobe textura pra GPU a cada passo sem fechar quadro e o d3d12 cai depois de uns milhares de passos)
                if (lance == "venda")
                    while (p.Partida != null && p.Partida.vendas.Count == 0 && p.Round < r0 + rounds) p.PularAte(p.Round + 1);
                while (!achou && p.Round < r0 + rounds && guarda++ < 30 * 60 * 25)
                {
                    p.Simular(DT, DT);
                    var m = p.Partida; if (m == null || m.over) continue;
                    if (lance == "item")
                    {
                        var it = m.itensChao.Find(x => x.morto != null && m.t - x.t < 0.1f);
                        if (it != null) { achou = true; p.camFixa = it.origem; Debug.Log($"[Visual3D] LANCE item round {p.Round} t={m.t:0.0}: {it.nade} de {it.morto.name} em ({it.origem.x:0},{it.origem.y:0})"); }
                    }
                    else if (lance == "venda")
                    {
                        // 0,7 s antes de vender: segurando a SMG -> some, "$" sobe, saca a pistola
                        var vd = m.vendas.Find(x => m.t >= x.t - 0.7f && m.t < x.t);
                        if (vd.quem != null) { achou = true; p.camFixa = vd.quem.pos; p.seguir = vd.quem.id; Debug.Log($"[Visual3D] LANCE venda round {p.Round} t={m.t:0.0}: {vd.quem.name} vende {vd.item} em t={vd.t:0.0} ({vd.quem.pos.x:0},{vd.quem.pos.y:0})"); }
                    }
                    else
                    {
                        var a = m.agents.Find(x => x.alive && x.brain.action == "granada" && x.brain.PlanoGranada.HasValue
                                                   && (x.brain.PlanoGranada.Value.tipo == CSM.Sim.Arremesso.Tipo.Pulo || x.brain.PlanoGranada.Value.tipo == CSM.Sim.Arremesso.Tipo.CorrePulo));
                        if (a != null) { achou = true; p.camFixa = a.pos; Debug.Log($"[Visual3D] LANCE jump round {p.Round} t={m.t:0.0}: {a.name} {a.brain.PlanoGranada.Value.tipo} em ({a.pos.x:0},{a.pos.y:0})"); }
                    }
                }
                if (!achou) { Debug.Log($"[Visual3D] LANCE {lance}: nenhum em {rounds} rounds"); EditorApplication.Exit(0); return; }
                int n = Mathf.RoundToInt(dur * 30), W = (int)Arg("-v3d_w", 1280), H = (int)Arg("-v3d_h", 720);
                for (int i = 0; i < n; i++) { p.Simular(DT, DT); Salvar(p.Cenario.cam, $"{dir}/q_{i:0000}.png", W, H, p.Hud); }
                Debug.Log($"[Visual3D] LANCE gravado: {n} quadros");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static void CapturarVideo()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.seed = (int)Arg("-v3d_seed", 15);
                p.zoom = Arg("-v3d_zoom", p.zoom);
                // -v3d_cam_x/-v3d_cam_y: câmera parada nesse ponto do mapa (laboratório de lances reais)
                if (Arg("-v3d_cam_x", -1) >= 0) p.camFixa = new CSM.Sim.V2(Arg("-v3d_cam_x", 0), Arg("-v3d_cam_y", 0));
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                p.PularAte((int)Arg("-v3d_round", 1));
                // -v3d_seguir NOME: a câmera segue esse jogador (ex.: quem sobe a escada)
                var ar = System.Environment.GetCommandLineArgs();
                int si = System.Array.IndexOf(ar, "-v3d_seguir");
                string seguirNome = si >= 0 && si + 1 < ar.Length ? ar[si + 1] : null; float seguirAte = Arg("-v3d_seguir_ate", 9999), seguirDe = Arg("-v3d_seguir_de", 0);
                float de = Arg("-v3d_de", 18), dur = Arg("-v3d_dur", 20);
                const float DT = 1 / 30f;
                int W = (int)Arg("-v3d_w", 1280), H = (int)Arg("-v3d_h", 720);   // -v3d_w 1920 -v3d_h 1080: quadro em Full HD
                CSM.View3D.Hud3D.SemEconomia = Arg("-v3d_semeco", 0) > 0;
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
                    if (seguirNome != null && i * DT >= seguirDe && i * DT < seguirAte) { int id = p.Partida.agents.FindIndex(a => a.name == seguirNome); p.seguir = id >= 0 && p.Partida.agents[id].alive ? id : -1; }
                    else if (seguirNome != null) p.seguir = -1;   // fora de -v3d_seguir_de..-v3d_seguir_ate: diretor automático
                    p.Simular(DT, DT);
                    Salvar(p.Cenario.cam, $"{dir}/q_{i:0000}.png", W, H, p.Hud);
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
                // -v3d_armasnovas: as 6 do Meshy (2026-11-04) pintadas, na mira, dos dois times
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-v3d_armasnovas") >= 0)
                    itens = new (string, string, string, string)[]
                    {
                        ("ct", "mira_Deagle", null, "Deagle"), ("tr", "mira_Tec9", null, "Tec-9"), ("ct", "mira_FiveSeven", null, "Five-SeveN"),
                        ("tr", "mira_P250", null, "P250"), ("ct", "mira_Famas", null, "FAMAS"), ("tr", "mira_Galil", null, "Galil"),
                        ("tr", "tiro_Galil", "rajada", "tiro Galil"), ("ct", "recarga_Famas", null, "recarga FAMAS"),
                    };
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
        /// fecham); HE dentro (bolha que volta a encher); molotov do lado apagada por uma smoke jogada em cima; armas no chão e C4
        /// explodindo do lado (itens voam). Mesmo desenho do jogo, sem depender de um round acontecer.
        /// </summary>
        /// <summary>LED da C4 piscando (Logs/visual3d/c4/q_NNNN.png): C4 plantada no meio do A, 1,5 s faltando 9 s.</summary>
        public static void CapturarC4Batch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                foreach (var b in p.GetComponentsInChildren<CSM.View3D.Boneco3D>()) b.gameObject.SetActive(false);
                var A = CSM.Map.MapData.SITES["A"].Center; var pc = new CSM.Sim.V2(A.x, A.y);
                p.LabCamera(pc, Arg("-v3d_zoom", 1.2f));
                string dir = "Logs/visual3d/c4";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                for (int f = 0; f < 45; f++)
                {
                    p.LabPasso(1 / 30f); p.LabBomba(pc, 9f - f / 30f);
                    Salvar(p.Cenario.cam, $"{dir}/q_{f:0000}.png", 1280, 720);
                }
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

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
                    // molotov queimando do lado e, 1 s depois, uma smoke jogada em cima: apaga inteira quando a smoke encosta
                    if (!feito3 && t >= 10.0f) { feito3 = true; fogo = p.LabFogo(new CSM.View.V3(o.x - 40, o.y - 45, z)); }
                    if (!feito4 && t >= 11.2f) { feito4 = true; p.LabSmoke(new CSM.View.V3(o.x - 36, o.y - 40, z)); }
                    if (!feito5 && t >= 14.5f) { feito5 = true; p.LabC4(new CSM.View.V3(A.x + 55, A.y - 25, z)); }
                    p.LabPasso(DT);
                    Salvar(p.Cenario.cam, $"{dir}/q_{f:0000}.png", (int)Arg("-v3d_w", 1280), (int)Arg("-v3d_h", 720));
                }
                Debug.Log($"[Visual3D] laboratório: {N} quadros");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Quanto a smoke por volume custa por quadro (Logs: "[Visual3D] DESEMPENHO"): cena do laboratório com 2 smokes
        /// cheias, 1920x1080, mede o render com e sem as smokes e com e sem a sombra delas. Ler 1 pixel força a GPU a
        /// terminar o quadro, então o tempo é o da GPU de verdade (aproximado).
        /// </summary>
        public static void MedirSmokeBatch()
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
                p.LabCamera(new CSM.Sim.V2(A.x - 5, A.y + 15), 8.5f);
                p.LabSmoke(new CSM.View.V3(A.x - 10, A.y + 10, z)); p.LabSmoke(new CSM.View.V3(A.x - 46, A.y - 35, z));
                for (int f = 0; f < 90; f++) p.LabPasso(1 / 30f);
                var cam = p.Cenario.cam;
                var rt = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
                var um = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                var smokes = Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "smoke").ToArray();
                double Medir()
                {
                    cam.targetTexture = rt;
                    for (int i = 0; i < 5; i++) cam.Render();   // aquece
                    var sw = System.Diagnostics.Stopwatch.StartNew();
                    const int N = 30;
                    for (int i = 0; i < N; i++)
                    {
                        p.LabPasso(1 / 30f); cam.Render();
                        RenderTexture.active = rt; um.ReadPixels(new Rect(0, 0, 1, 1), 0, 0); um.Apply(); RenderTexture.active = null;
                    }
                    cam.targetTexture = null;
                    return sw.Elapsed.TotalMilliseconds / N;
                }
                double com = Medir();
                foreach (var r in smokes) r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                double semSombra = Medir();
                foreach (var r in smokes) r.enabled = false;
                double sem = Medir();
                Debug.Log($"[Visual3D] DESEMPENHO 1920x1080, {smokes.Length} smokes: com tudo {com:0.0} ms, sem a sombra da smoke {semSombra:0.0} ms, sem smoke {sem:0.0} ms");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Build de Windows da partida 3D pra medir FPS de verdade (CSM.View3D.Desempenho, ligado por "-csm_perf saida.txt").
        /// -v3d_build pasta (padrão Logs/perfbuild). Liga as estatísticas de tempo de quadro só durante a build.
        /// </summary>
        public static void BuildPerfBatch()
        {
            string pasta = ArgS("-v3d_build", "Logs/perfbuild");
            bool antes = PlayerSettings.enableFrameTimingStats;
            try
            {
                Setup();
                PlayerSettings.enableFrameTimingStats = true;
                var r = BuildPipeline.BuildPlayer(new[] { CENA3D }, Path.Combine(pasta, "CSManager.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
                Debug.Log($"[Visual3D] BUILD {r.summary.result} {r.summary.totalTime.TotalSeconds:0}s {r.summary.totalErrors} erros");
                PlayerSettings.enableFrameTimingStats = antes;
                EditorApplication.Exit(r.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1);
            }
            catch (System.Exception e) { PlayerSettings.enableFrameTimingStats = antes; Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public const string PASTA_JOGO = "Build";

        /// <summary>O jogo pra jogar fora do editor: Build/CSManager.exe (tela cheia, partida 3D, dados dos bonecos e dos bots
        /// copiados pra dentro por BuildDados). Roda bem mais leve que o Play do editor.</summary>
        [MenuItem("CS Manager/Gerar jogo (.exe)")]
        public static void GerarJogo()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            var r = Gerar(PASTA_JOGO);
            if (r == UnityEditor.Build.Reporting.BuildResult.Succeeded) EditorUtility.RevealInFinder(Path.Combine(PASTA_JOGO, "CSManager.exe"));
        }

        public static void GerarJogoBatch()
        {
            try { EditorApplication.Exit(Gerar(ArgS("-v3d_build", PASTA_JOGO)) == UnityEditor.Build.Reporting.BuildResult.Succeeded ? 0 : 1); }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        static UnityEditor.Build.Reporting.BuildResult Gerar(string pasta)
        {
            Setup();
            var r = BuildPipeline.BuildPlayer(new[] { CENA3D }, Path.Combine(pasta, "CSManager.exe"), BuildTarget.StandaloneWindows64, BuildOptions.None);
            Debug.Log($"[Visual3D] JOGO {r.summary.result} em {Path.GetFullPath(pasta)} ({r.summary.totalTime.TotalSeconds:0}s, {r.summary.totalErrors} erros)");
            return r.summary.result;
        }

        static string ArgS(string nome, string padrao)
        {
            var a = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(a, nome);
            return i >= 0 && i + 1 < a.Length ? a[i + 1] : padrao;
        }

        /// <summary>Print do laboratório de arremesso (PartidaAoVivo3D.LabArremesso): fases x jeitos, CT e TR, granada do
        /// kit na mão. -v3d_giro graus (pra onde os bonecos olham), -v3d_zoom. Saída Logs/visual3d/arremesso.png.</summary>
        public static void CapturarArremessoBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22, A.y + 18);
                bool tr = Arg("-v3d_tr", 0) > 0;
                p.LabCamera(c, Arg("-v3d_zoom", 3.0f));
                p.LabArremesso(c, Arg("-v3d_giro", 150), tr, Arg("-v3d_passo", 11));
                for (int f = 0; f < 3; f++) p.LabPasso(1 / 30f);
                Directory.CreateDirectory("Logs/visual3d");
                Salvar(p.Cenario.cam, $"Logs/visual3d/arremesso_{(tr ? "tr" : "ct")}.png", 1920, 1080);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Print do laboratório do body block (PartidaAoVivo3D.LabEncosto): pares a -v3d_d unidades (padrão 8),
        /// -v3d_giro, -v3d_zoom. Saída Logs/visual3d/encosto.png.</summary>
        /// <summary>Laboratório das mortes (PartidaAoVivo3D.LabMortes): 8 casos morrendo juntos, quadros em Logs/visual3d/mortes e
        /// resumo "[Visual3D] MORTE" (giro do tronco, pra onde a cabeça caiu). -v3d_giro, -v3d_zoom, -v3d_quadros.</summary>
        public static void CapturarMortesBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                foreach (var b0 in p.GetComponentsInChildren<CSM.View3D.Boneco3D>()) b0.gameObject.SetActive(false);
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22 + Arg("-v3d_dx", 0), A.y + 18 + Arg("-v3d_dy", 0));
                p.LabCamera(c, Arg("-v3d_zoom", 3.4f));
                var arM = System.Environment.GetCommandLineArgs(); int qm = System.Array.IndexOf(arM, "-v3d_casos");
                CSM.View3D.Ragdoll3D.EmbaloVelho = System.Array.IndexOf(arM, "-v3d_embalo_velho") >= 0;
                p.LabMortes(c, Arg("-v3d_giro", 120), 0.3f, qm >= 0 && qm + 1 < arM.Length ? arM[qm + 1] : "0,1,2,3,4,5,6,7");
                string dir = "Logs/visual3d/mortes";
                if (Directory.Exists(dir)) Directory.Delete(dir, true);
                Directory.CreateDirectory(dir);
                int N = (int)Arg("-v3d_quadros", 100);
                for (int f = 0; f < N; f++) { p.LabPasso(1 / 30f); Salvar(p.Cenario.cam, $"{dir}/q_{f:0000}.png", 1280, 720); }
                Debug.Log(p.LabMortesResumo());
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Laboratório da mira inclinada (Boneco3D: cadeia girando nos ombros): mede a junta mais aberta de cada
        /// animação a -60..+60° contra a mesma pose reta e fotografa a luneta da AWP e o agachado. Log "[Visual3D] PITCH".
        /// Fotos em Logs/visual3d/pitch_*.png.</summary>
        public static void CapturarPitchBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                foreach (var b0 in p.GetComponentsInChildren<CSM.View3D.Boneco3D>()) b0.gameObject.SetActive(false);
                float[] graus = { -60, -30, 0, 30, 60 };
                string[] anims = { "mira_AWP", "awp_mirando", "tiro_AK47", "agachado_M4A4", "andar_AK47", "recarga_AWP", "devagar_MP9", "mira_Glock", "agachado_USP", "sacar_AWP" };
                float piorGeral = 0; string piorQual = "";
                foreach (var time in new[] { "ct", "tr" })
                    foreach (var an in anims)
                    {
                        var b = CSM.View3D.Boneco3D.Criar(time, p.transform);
                        if (!b.Tem(an)) { Object.DestroyImmediate(b.gameObject); continue; }
                        b.pitch = 0; b.Pose(an, 0.3f, 0); float g0 = b.PiorJunta(out _);
                        string linha = $"[Visual3D] PITCH {time} {an}: reta {g0 * 100:0.0} cm |";
                        foreach (var gr in graus)
                        {
                            if (gr == 0) continue;
                            b.pitch = gr; b.Pose(an, 0.3f, 0); float g = b.PiorJunta(out string q);
                            linha += $" {gr:+0;-0}° +{(g - g0) * 100:0.0} ({q})";
                            if (g - g0 > piorGeral) { piorGeral = g - g0; piorQual = $"{time} {an} {gr}° {q}"; }
                        }
                        Debug.Log(linha);
                        Object.DestroyImmediate(b.gameObject);
                    }
                Debug.Log($"[Visual3D] PITCH pior junta a mais: {piorGeral * 100:0.0} cm ({piorQual})");
                // fotos: fileira de -60 a +60 com a câmera de lado
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22, A.y + 18);
                Directory.CreateDirectory("Logs/visual3d");
                foreach (var (time, an) in new[] { ("ct", "awp_mirando"), ("tr", "awp_mirando"), ("ct", "agachado_M4A4"), ("tr", "tiro_AK47") })
                {
                    var feitos = new System.Collections.Generic.List<CSM.View3D.Boneco3D>();
                    for (int i = 0; i < graus.Length; i++)
                    {
                        var b = CSM.View3D.Boneco3D.Criar(time, p.transform);
                        var q = new CSM.Sim.V2(c.x + (i - 2) * 13, c.y);
                        b.transform.SetPositionAndRotation(CSM.View3D.MalhaMapa.ParaUnity(q.x, q.y, CSM.Map.NavGrid.ZAt(q)), Quaternion.Euler(0, 180, 0));
                        b.pitch = graus[i]; b.Pose(an, 0.3f, 0); feitos.Add(b);
                    }
                    p.LabCamera(c, Arg("-v3d_zoom", 2.2f));
                    Salvar(p.Cenario.cam, $"Logs/visual3d/pitch_{time}_{an}.png", 1600, 900);
                    foreach (var b in feitos) Object.DestroyImmediate(b.gameObject);
                }
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        public static void CapturarEncostoBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 22, A.y + 18);
                p.LabCamera(c, Arg("-v3d_zoom", 3.0f));
                p.LabEncosto(c, Arg("-v3d_giro", 120), Arg("-v3d_d", 8));
                for (int f = 0; f < 3; f++) p.LabPasso(1 / 30f);
                Directory.CreateDirectory("Logs/visual3d");
                Salvar(p.Cenario.cam, "Logs/visual3d/encosto.png", 1920, 1080);
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Print de um ponto do mapa (sem partida rodando): -v3d_x -v3d_y (unidades do jogo), -v3d_zoom, -v3d_nome.
        /// Saída Logs/visual3d/ponto_NOME.png.</summary>
        public static void CapturarPontoBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                CSM.View3D.Cenario3D.SigmaTeste = Arg("-v3d_sigma", -1);
                CSM.View3D.Cenario3D.QuinaViva = Arg("-v3d_quina", 1);
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                foreach (var b in p.GetComponentsInChildren<CSM.View3D.Boneco3D>()) b.gameObject.SetActive(false);
                var ar = System.Environment.GetCommandLineArgs();
                // -v3d_cover TIPO: centro da primeira cobertura desse tipo (ex.: fountain), no lugar de -v3d_x/-v3d_y
                int ci = System.Array.IndexOf(ar, "-v3d_cover");
                var alvo = new CSM.Sim.V2(Arg("-v3d_x", 0), Arg("-v3d_y", 0));
                if (ci >= 0 && ci + 1 < ar.Length)
                {
                    int k = CSM.Map.MapData.covers.FindIndex(c => c.kind == ar[ci + 1]);
                    if (k >= 0) { var cv = CSM.Map.MapData.covers[k]; alvo = new CSM.Sim.V2((cv.r.x0 + cv.r.x1) / 2f, (cv.r.y0 + cv.r.y1) / 2f); }
                }
                int ni = System.Array.IndexOf(ar, "-v3d_nome");
                string nome = ni >= 0 && ni + 1 < ar.Length ? ar[ni + 1] : "ponto";
                Directory.CreateDirectory("Logs/visual3d");
                // -v3d_pontos "nome:x:y:zoom;nome:x:y:zoom": vários prints numa rodada só (a montagem do mapa é a parte cara)
                // -v3d_ab_quina: cada print sai duas vezes, quina viva desligada (_q0) e ligada (_q1) — só a global do shader
                int pi = System.Array.IndexOf(ar, "-v3d_pontos");
                var lista = new System.Collections.Generic.List<(string n, CSM.Sim.V2 a, float z)>();
                if (pi >= 0 && pi + 1 < ar.Length)
                    foreach (var s in ar[pi + 1].Split(';'))
                    {
                        var q = s.Split(':'); var ci2 = System.Globalization.CultureInfo.InvariantCulture;
                        // "nome:cover:TIPO:zoom" = centro da 1ª cobertura desse tipo
                        if (q[1] == "cover")
                        {
                            int k = CSM.Map.MapData.covers.FindIndex(c => c.kind == q[2]); if (k < 0) continue;
                            var cv = CSM.Map.MapData.covers[k];
                            lista.Add((q[0], new CSM.Sim.V2((cv.r.x0 + cv.r.x1) / 2f, (cv.r.y0 + cv.r.y1) / 2f), float.Parse(q[3], ci2)));
                        }
                        else lista.Add((q[0], new CSM.Sim.V2(float.Parse(q[1], ci2), float.Parse(q[2], ci2)), float.Parse(q[3], ci2)));
                    }
                else lista.Add((nome, alvo, Arg("-v3d_zoom", 2.0f)));
                bool ab = System.Array.IndexOf(ar, "-v3d_ab_quina") >= 0;
                foreach (var (n, a, z) in lista)
                {
                    p.LabCamera(a, z);
                    for (int f = 0; f < 3; f++) p.LabPasso(1 / 30f);
                    if (!ab) { Salvar(p.Cenario.cam, $"Logs/visual3d/ponto_{n}.png", 1920, 1080); continue; }
                    for (int q = 0; q < 2; q++)
                    {
                        Shader.SetGlobalFloat("_QuinaViva", q);
                        Salvar(p.Cenario.cam, $"Logs/visual3d/ponto_{n}_q{q}.png", 1920, 1080);
                    }
                }
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>Print das granadas do kit no chão do bomb A, de perto, com um boneco do lado (Logs/visual3d/granadas.png).</summary>
        public static void CapturarGranadasBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                new GameObject("Câmera 3D") { tag = "MainCamera" }.AddComponent<Camera>();
                var p = new GameObject("Partida 3D").AddComponent<CSM.View3D.PartidaAoVivo3D>();
                p.Iniciar(); p.Cenario.cam.aspect = 16f / 9f;
                var A = CSM.Map.MapData.SITES["A"].Center; var c = new CSM.Sim.V2(A.x - 30, A.y + 20);
                p.LabCamera(new CSM.Sim.V2(c.x + 10, c.y), 2.2f);
                p.LabGranadas(c);
                for (int f = 0; f < 3; f++) p.LabPasso(1 / 30f);
                Directory.CreateDirectory("Logs/visual3d");
                Salvar(p.Cenario.cam, "Logs/visual3d/granadas.png", 1920, 1080);
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

        /// <summary>Só simulação (body block): a menor distância entre dois vivos na mesma altura (|dz| &lt; 18) em cada round,
        /// fora dos 2 s do spawn, e os 3 encostos mais apertados (round, tempo, quem). -v3d_rounds N (padrão 6).</summary>
        public static void AcharEncostoBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15), rounds = (int)Arg("-v3d_rounds", 6);
            var pt = new CSM.Sim.Partida(seed, null, null);
            var top = new System.Collections.Generic.List<(float d, int r, float t, string a, string b)>();
            while (!pt.acabou && pt.r < rounds)
            {
                var m = pt.Preparar(); int r = pt.r + 1; float menor = 999;
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    if (m.t < 2) continue;
                    for (int i = 0; i < m.agents.Count; i++)
                        for (int j = i + 1; j < m.agents.Count; j++)
                        {
                            var a = m.agents[i]; var b = m.agents[j];
                            if (!a.alive || !b.alive || System.Math.Abs(a.z - b.z) >= 18) continue;
                            float d = (a.pos - b.pos).Len;
                            if (d < menor) menor = d;
                            if (d < 9) top.Add((d, r, m.t, a.name, b.name));
                        }
                }
                Debug.Log($"[Visual3D] ENCOSTO round {r}: menor distância {menor:0.00}");
                pt.Fechar(m);
            }
            top.Sort((x, y) => x.d.CompareTo(y.d));
            var vistos = new System.Collections.Generic.HashSet<string>();
            foreach (var e in top)
            {
                string k = e.r + e.a + e.b + (int)(e.t / 3);
                if (!vistos.Add(k)) continue;
                Debug.Log($"[Visual3D] ENCOSTO apertado: {e.d:0.00} round {e.r} t={e.t:0.0} {e.a} x {e.b}");
                if (vistos.Count >= 5) break;
            }
            EditorApplication.Exit(0);
        }

        /// <summary>
        /// Só simulação: o trecho de -v3d_dur segundos (padrão 25) de cada round com mais coisa acontecendo — bunny hop,
        /// granada de cada tipo, abate — pra escolher o vídeo "com tudo". Log: "[Visual3D] CENA seed s round r de t ...".
        /// </summary>
        public static void AcharCenaBatch() => AcharCena();

        /// <summary>Só simulação: cada granada jogada (round, hora que saiu da mão, quem, tipo, jeito e força), pra escolher
        /// os quadros das animações de arremesso. -v3d_rounds N (padrão 4).</summary>
        /// <summary>Só simulação: lances pra conferir no visual — granada/kit caindo do morto (onde, rolou até onde, quem pegou e
        /// quando), arma/C4 pegas do chão e jump throws. -v3d_rounds N (padrão 6). Log "[Visual3D] LAB ...".</summary>
        public static void AcharLabBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15), rounds = (int)Arg("-v3d_rounds", 6);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou && pt.r < rounds)
            {
                var m = pt.Preparar(); int r = pt.r + 1;
                var itens = new System.Collections.Generic.HashSet<CSM.Sim.Match.ItemChao>();
                var vivos = new System.Collections.Generic.Dictionary<CSM.Sim.Match.ItemChao, (float t, CSM.Sim.V2 p)>();
                var drops = new System.Collections.Generic.HashSet<CSM.Sim.Match.Drop>();
                var nades = new System.Collections.Generic.HashSet<CSM.Sim.Match.Nade>();
                bool c4Chao = false;
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    foreach (var it in m.itensChao)
                        if (it.morto != null && itens.Add(it)) Debug.Log($"[Visual3D] LAB caiu round {r} t={m.t:0.00} {it.nade} de {it.morto.name} em ({it.origem.x:0},{it.origem.y:0})");
                    foreach (var it in itens)
                        if (!m.itensChao.Contains(it) && !vivos.ContainsKey(it)) { vivos[it] = (m.t, it.pos); Debug.Log($"[Visual3D] LAB pegou round {r} t={m.t:0.00} {it.nade} por {it.pegouPor?.name} em ({it.pos.x:0},{it.pos.y:0}) rolou {(it.pos - it.origem).Len:0.0}"); }
                    foreach (var d in m.drops) drops.Add(d);
                    foreach (var d in drops)
                        if (!m.drops.Contains(d) && d.pegouPor != null && d.pegouT >= m.t - 0.05f) Debug.Log($"[Visual3D] LAB pegou-arma round {r} t={m.t:0.00} {d.gun} por {d.pegouPor.name} em ({d.pos.x:0},{d.pos.y:0})");
                    if (m.bombDropped && !m.bombPlanted) c4Chao = true;
                    else if (c4Chao && !m.bombDropped) { c4Chao = false; var q = m.agents.Find(a => a.hasBomb); if (q != null) Debug.Log($"[Visual3D] LAB pegou-c4 round {r} t={m.t:0.00} por {q.name} em ({q.pos.x:0},{q.pos.y:0})"); }
                    foreach (var n in m.flying)
                        if (nades.Add(n) && (n.tipo == CSM.Sim.Arremesso.Tipo.Pulo || n.tipo == CSM.Sim.Arremesso.Tipo.CorrePulo))
                            Debug.Log($"[Visual3D] LAB jumpthrow round {r} t={n.t0:0.00} {n.by?.name} {n.l.kind} {n.tipo} em ({n.from.x:0},{n.from.y:0})");
                }
                pt.Fechar(m);
            }
            EditorApplication.Exit(0);
        }

        public static void AcharArremessosBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15), rounds = (int)Arg("-v3d_rounds", 4);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou && pt.r < rounds)
            {
                var m = pt.Preparar(); int r = pt.r + 1;
                var vistas = new System.Collections.Generic.HashSet<CSM.Sim.Match.Nade>();
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    foreach (var n in m.flying)
                        if (vistas.Add(n)) Debug.Log($"[Visual3D] ARREMESSO round {r} t={n.t0:0.00} {n.by?.name} {n.l.kind} {n.tipo} forca {n.forca:0.0} up {n.up:0}");
                }
                pt.Fechar(m);
            }
            EditorApplication.Exit(0);
        }

        static void AcharCena()
        {
            int seed = (int)Arg("-v3d_seed", 15); float dur = Arg("-v3d_dur", 25);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou)
            {
                var m = pt.Preparar(); int r = pt.r + 1;
                var ev = new System.Collections.Generic.List<(float t, string k, string quem)>();
                var vistas = new System.Collections.Generic.HashSet<CSM.Sim.Match.Nade>(); var livre = new bool[m.agents.Count]; int feed = 0;
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    foreach (var n in m.flying) if (vistas.Add(n)) ev.Add((m.t, n.l.kind, n.by?.name));
                    for (int i = 0; i < m.agents.Count; i++)
                    {
                        var a = m.agents[i];
                        if (a.puloLivre && !livre[i] && a.pulosSeguidos >= 2) ev.Add((m.t, "bhop", a.name));
                        livre[i] = a.puloLivre;
                    }
                    for (; feed < m.feed.Count; feed++) ev.Add((m.t, "abate", m.feed[feed].Item2));
                }
                pt.Fechar(m);
                float melhor = -1, de = 0; string resumo = "";
                foreach (var (t0, _, _) in ev)
                {
                    var j = ev.Where(e => e.t >= t0 - 2 && e.t < t0 - 2 + dur).ToList();
                    int bh = j.Count(e => e.k == "bhop"), ab = j.Count(e => e.k == "abate"), nd = j.Count(e => e.k != "bhop" && e.k != "abate");
                    int tipos = j.Where(e => e.k != "bhop" && e.k != "abate").Select(e => e.k).Distinct().Count();
                    float s = (bh > 0 ? 6 : 0) + bh + nd * 1.5f + tipos * 3 + ab;
                    if (s > melhor) { melhor = s; de = Mathf.Max(0, t0 - 2); resumo = $"bhop {bh}, granadas {nd} ({string.Join("/", j.Where(e => e.k != "bhop" && e.k != "abate").Select(e => e.k).Distinct())}), abates {ab}; bhop de {string.Join(",", j.Where(e => e.k == "bhop").Select(e => e.quem).Distinct())}"; }
                }
                Debug.Log($"[Visual3D] CENA seed {seed} round {r} de {de:0.0}s (nota {melhor:0}): {resumo}");
            }
            EditorApplication.Exit(0);
        }

        /// <summary>Só simulação: drops de propósito entre colegas por round (arma, granada, C4), pra escolher o vídeo.</summary>
        public static void AcharDropsBatch()
        {
            int seed = (int)Arg("-v3d_seed", 15);
            var pt = new CSM.Sim.Partida(seed, null, null);
            while (!pt.acabou)
            {
                var m = pt.Preparar(); int r = pt.r + 1;
                var vD = new System.Collections.Generic.HashSet<CSM.Sim.Match.Drop>(); var vI = new System.Collections.Generic.HashSet<CSM.Sim.Match.ItemChao>();
                var ev = new System.Collections.Generic.List<string>(); float ultC4 = -1;
                // o round roda até o fim (senão a partida nunca acaba); só os primeiros 12 s contam
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    if (m.t > 12) continue;
                    foreach (var d in m.drops) if (d.de != null && vD.Add(d)) ev.Add($"{m.t:0.0}s arma {d.gun} {d.de.name}->{d.para?.name ?? "chão"}");
                    foreach (var it in m.itensChao) if (it.de != null && vI.Add(it)) ev.Add($"{m.t:0.0}s {it.nade} {it.de.name}->{it.para?.name ?? "chão"}");
                    if (m.bombDe != null && m.bombDeT > ultC4) { ultC4 = m.bombDeT; ev.Add($"{m.t:0.0}s C4 {m.bombDe.name}"); }
                }
                pt.Fechar(m);
                if (ev.Count > 0) Debug.Log($"[Visual3D] DROPS seed {seed} round {r}: {string.Join(" | ", ev)}");
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
                var livre = new bool[m.agents.Count]; var naSmoke = new bool[m.agents.Count];
                while (!m.over && m.t < 200)
                {
                    m.Step();
                    for (int i = 0; i < m.agents.Count; i++)
                    {
                        var a = m.agents[i];
                        if (a.puloLivre && !livre[i]) Debug.Log($"[Visual3D] PULO seed {seed} round {r} t={m.t:0.0} {a.name} seguidos={a.pulosSeguidos}");
                        livre[i] = a.puloLivre;
                        bool dentro = a.alive && m.smokes.Any(s => s.Sample(new CSM.View.V3(a.pos.x, a.pos.y, a.z + 8), m.t) > 0.5f);
                        if (dentro && !naSmoke[i]) Debug.Log($"[Visual3D] SMOKE seed {seed} round {r} t={m.t:0.0} {a.name}");
                        naSmoke[i] = dentro;
                    }
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

        /// <summary>Radar do mapa (estilo CS): piso onde anda claro (mais claro quanto mais alto), escada um tom no meio,
        /// prédio/parede escuro, contorno escuro entre piso e parede, zona dos bombs em vermelho. Sai
        /// Assets/Game/Interface/Resources/Icones/radar.png (o HUD usa) — 1 pixel = 2 unidades do mapa.</summary>
        public static void RadarBatch()
        {
            try
            {
                Setup();
                int NX = CSM.Map.MapData.NX, NY = CSM.Map.MapData.NY, S = 2, W = NX / S, H = NY / S;
                var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
                var px = new Color32[W * H];
                bool Anda(int x, int y) => x >= 0 && y >= 0 && x < NX && y < NY && CSM.Map.MapData.walk[x, y];
                Color32 C(int r, int g, int b, int a = 255) => new Color32((byte)r, (byte)g, (byte)b, (byte)a);
                int GZ = CSM.Map.MapData.GZ;
                for (int py = 0; py < H; py++)
                    for (int px_ = 0; px_ < W; px_++)
                    {
                        int x = px_ * S + S / 2, y = py * S + S / 2;
                        Color32 c;
                        if (!Anda(x, y)) c = C(38, 34, 32, 235);
                        else
                        {
                            bool borda = !Anda(x - 3, y) || !Anda(x + 3, y) || !Anda(x, y - 3) || !Anda(x, y + 3);
                            int alt = CSM.Map.MapData.floor[x, y] - GZ;
                            if (borda) c = C(24, 21, 19, 255);
                            else if (CSM.Map.MapData.stair[x, y]) c = C(170, 152, 128);
                            else if (alt > 20) c = C(232, 219, 196);
                            else if (alt > 4) c = C(214, 198, 172);
                            else c = C(196, 178, 150);
                        }
                        foreach (var kv in CSM.Map.MapData.SITES)
                        {
                            var r = kv.Value;
                            if (x >= r.x0 && x < r.x1 && y >= r.y0 && y < r.y1 && Anda(x, y))
                            {
                                bool b = x < r.x0 + 4 || x >= r.x1 - 4 || y < r.y0 + 4 || y >= r.y1 - 4;
                                c = b ? C(200, 52, 44) : new Color32((byte)((c.r + 230) / 2), (byte)((c.g + 110) / 2), (byte)((c.b + 96) / 2), 255);
                            }
                        }
                        px[(H - 1 - py) * W + px_] = c;   // y do jogo cresce pro sul: linha de cima da imagem = norte
                    }
                tex.SetPixels32(px); tex.Apply();
                System.IO.Directory.CreateDirectory("Assets/Game/Interface/Resources/Icones");
                System.IO.File.WriteAllBytes("Assets/Game/Interface/Resources/Icones/radar.png", tex.EncodeToPNG());
                Debug.Log($"[Visual3D] radar {W}x{H}; A {CSM.Map.MapData.SITES["A"].x0},{CSM.Map.MapData.SITES["A"].y0} B {CSM.Map.MapData.SITES["B"].x0},{CSM.Map.MapData.SITES["B"].y0}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
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

        /// <summary>
        /// Tamanho da smoke: raio (unidades da Sim, média de 16 direções, na altura do olho) da smoke que os bots usam
        /// (células, Sample) e da que a tela desenha (esferas do Fumaca3D) com vários RAIO/ESCALA. Smoke cheia no meio do A.
        /// </summary>
        public static void MedirTamanhoSmokeBatch()
        {
            try
            {
                var c = CSM.Map.MapData.SITES["A"].Center;
                float z0 = CSM.Map.MapData.floor[(int)c.x, (int)c.y];
                var s = new CSM.View.SmokeVolume(new CSM.View.V3(c.x, c.y, z0));
                for (int i = 0; i < 60; i++) s.Update(0.05f);
                float t = s.age, zOlho = z0 + 18;
                float Raio(System.Func<CSM.View.V3, float> dens, float lim)
                {
                    float soma = 0;
                    for (int k = 0; k < 16; k++)
                    {
                        float a = k * Mathf.PI / 8, ult = 0;
                        for (float d = 0; d < 90; d += 0.5f)
                            if (dens(new CSM.View.V3(c.x + Mathf.Cos(a) * d, c.y + Mathf.Sin(a) * d, zOlho)) >= lim) ult = d;
                        soma += ult;
                    }
                    return soma / 16;
                }
                var centro = new CSM.View.V3(c.x, c.y, z0 + 4);
                float Vis(CSM.View.V3 q0, float raio, float escala)
                {
                    float m = 0;
                    foreach (var p in s.puffs)
                    {
                        float r = s.PuffR(p, t); if (r <= 0.3f) continue;
                        var pc = s.PuffC(p, t);
                        float d = s.Sample(pc, t);
                        float alfa = Mathf.SmoothStep(0, 1, Mathf.Clamp01((d - 0.02f) / 0.14f)); if (alfa < 0.02f) continue;
                        float qx = centro.x + (pc.x - centro.x) * escala, qy = centro.y + (pc.y - centro.y) * escala, qz = centro.z + (pc.z - centro.z) * escala;
                        float R = r * raio * (0.7f + 0.7f * Mathf.Repeat(p.ph * 0.37f, 1f));
                        float dd = Mathf.Sqrt((q0.x - qx) * (q0.x - qx) + (q0.y - qy) * (q0.y - qy) + (q0.z - qz) * (q0.z - qz));
                        m = Mathf.Max(m, alfa * Mathf.Clamp01((1 - dd / R) / 0.45f));
                    }
                    return m;
                }
                foreach (float lim in new[] { 0.3f, 0.5f })
                    Debug.Log($"[Visual3D] SMOKE bots (células) lim {lim}: raio {Raio(q => s.Sample(q, t), lim):0.0}");
                foreach (var (ra, es) in new[] { (1.4f, 1.05f), (1.2f, 1.0f), (1.0f, 1.0f), (0.9f, 1.0f), (0.8f, 1.0f), (0.8f, 0.95f), (0.7f, 0.95f), (0.6f, 0.95f) })
                    Debug.Log($"[Visual3D] SMOKE tela RAIO {ra} ESCALA {es}: raio lim 0.3 {Raio(q => Vis(q, ra, es), 0.3f):0.0}  lim 0.5 {Raio(q => Vis(q, ra, es), 0.5f):0.0}");
                EditorApplication.Exit(0);
            }
            catch (System.Exception e) { Debug.LogException(e); EditorApplication.Exit(1); }
        }

        /// <summary>
        /// Kit da Brasa (2º mapa): os props de pack que vão vestir o mapa no clima da Inferno, mais amostras da paleta
        /// (reboco, telha, madeira, veneziana), num estúdio com um boneco pra escala. Logs/visual3d/kit_brasa.png.
        /// </summary>
        public static void CapturarKitBrasaBatch()
        {
            try
            {
                Setup();
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                var cg = new GameObject("Câmera 3D") { tag = "MainCamera" }; cg.AddComponent<Camera>();
                var cen = new GameObject("Cenário 3D").AddComponent<CSM.View3D.Cenario3D>();
                cen.Montar(); cen.cam.aspect = 16f / 9f;
                cen.mapa.SetActive(false);
                foreach (Transform f in cen.transform) if (f.name == "Arredores") f.gameObject.SetActive(false);
                CSM.View3D.Paleta3D.UsarBrasa();
                bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
                Color Pal(int m) => CSM.View3D.Paleta3D.Vertice(m, linear);
                var sh = Shader.Find("CSM/Maquete");
                Material Liso(Color c) { var m = new Material(sh); m.SetFloat("_UsarVertice", 0); m.SetColor("_BaseColor", c); return m; }
                var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
                chao.transform.localScale = Vector3.one * 6; chao.GetComponent<MeshRenderer>().sharedMaterial = Liso(Pal(1));
                var mv = new Material(sh) { name = "kit vértice" }; mv.SetFloat("_UsarTopo", 0); mv.SetFloat("_UsarVertice", 1);
                const float E = CSM.View3D.MalhaMapa.ESC;
                // (arquivo em Resources/Packs, altura alvo em unidades da Sim; negativo = largura alvo (coisa deitada), rótulo)
                var kit = new (string arq, float alvo, string rot)[]
                {
                    // banana
                    ("ksv_barrel", 13, "barris (carro do banana)"), ("ksv_barrel-open", 12, "barril aberto"), ("ksv_tree-log", -22, "toras"),
                    ("ksv_resource-planks", -16, "tábuas (meio muro)"), ("kenney_fence-broken", 12, "cerca quebrada"), ("kenney_tree-crooked", 45, "cipreste"),
                    // bomb B
                    ("kgy_coffin", -22, "caixão"), ("kgy_coffin-old", -24, "caixão velho"), ("kenney_fountain-square-detail", -34, "fonte do B"),
                    ("ksv_box-large", 14, "caixote grande (tripla)"), ("ksv_box", 9, "caixote (1ª caixa)"), ("kgy_hay-bale", 10, "fardo (cimento)"),
                    // construção (igreja em ruína)
                    ("kgy_stone-wall-damaged", 26, "muro quebrado"), ("kgy_column-large", 40, "coluna da igreja"), ("kgy_cross-column", 30, "cruz de pedra"),
                    ("kgy_debris", 6, "entulho"), ("ksv_workbench", 12, "bancada de obra"), ("ksv_structure", 30, "andaime"),
                    // meio e MF
                    ("kenney_stall-bench", -22, "banco do meio"), ("kgy_lightpost-single", 34, "poste"), ("kgy_lantern-glass", 10, "lanterna"),
                    ("ksv_bucket", 6, "balde"), ("kgy_urn-round", 10, "vaso de pedra"), ("ksv_resource-wood", 8, "lenha"),
                    // A, arco e cemitério
                    ("kenney_cart-high", 22, "carroça alta (curta)"), ("ksv_rock-sand-a", 8, "areia"), ("kenney_wall-arch", 26, "arco de pedra"),
                    ("kgy_gravestone-cross", 14, "lápide (cemitério)"), ("kgy_iron-fence", 14, "grade do cemitério"), ("kgy_pine-crooked", 40, "oliveira"),
                    // dentro de casa (tapete, apê, biblioteca)
                    ("kenney_rugRectangle", -30, "tapete (no tapete)"), ("kenney_bedSingle", 10, "cama (apê)"), ("kenney_table", 12, "mesa (apê)"),
                    ("kenney_chair", 14, "cadeira (apê)"), ("kenney_bookcaseOpen", 26, "estante (biblioteca)"), ("kenney_balcony-wall-fence", 14, "parapeito da varanda"),
                };
                var pai = new GameObject("Kit Brasa").transform; var fonte = CSM.View3D.UiKit.Fonte;
                var eixo = new Vector3(1, 0, 1).normalized; var fundo = new Vector3(-1, 0, 1).normalized;
                const int COLS = 6; const float PASSO = 1.75f, LINHA = 1.9f;
                var rotulos = new System.Collections.Generic.List<Transform>(); int falta = 0;
                int nl = (kit.Length + COLS - 1) / COLS + 1;
                Vector3 Pos(int col, int lin) => eixo * ((col - (COLS - 1) / 2f) * PASSO) + fundo * (((nl - 1) / 2f - lin) * LINHA);
                void Rotulo(Vector3 p, string t)
                {
                    var tg = new GameObject("rótulo"); tg.transform.SetParent(pai, false); tg.transform.position = p - fundo * 0.62f + Vector3.up * 0.02f;
                    var tm = tg.AddComponent<TextMesh>(); tm.text = t; tm.font = fonte; tm.fontSize = 64; tm.characterSize = 0.03f;
                    tm.anchor = TextAnchor.MiddleCenter; tm.color = new Color(0.25f, 0.18f, 0.12f);
                    tg.GetComponent<MeshRenderer>().sharedMaterial = fonte.material; rotulos.Add(tg.transform);
                }
                Mesh Malha(string arq)
                {
                    var pf = Resources.Load<GameObject>("Packs/" + arq);
                    if (pf == null) { Debug.LogWarning("[Visual3D] kit: faltou " + arq); falta++; return null; }
                    var mfs = pf.GetComponentsInChildren<MeshFilter>(); var cis = new System.Collections.Generic.List<CombineInstance>(); var w2l = pf.transform.worldToLocalMatrix;
                    foreach (var mf in mfs) cis.Add(new CombineInstance { mesh = mf.sharedMesh, transform = pf.transform.localToWorldMatrix * (w2l * mf.transform.localToWorldMatrix) });
                    var me = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; me.CombineMeshes(cis.ToArray(), true, true);
                    var cs = me.colors; if (cs == null || cs.Length != me.vertexCount) { cs = new Color[me.vertexCount]; for (int k = 0; k < cs.Length; k++) cs[k] = Color.white; }
                    if (linear) for (int k = 0; k < cs.Length; k++) cs[k] = cs[k].linear;
                    me.colors = cs; me.RecalculateBounds(); return me;
                }
                GameObject Por(Mesh me, string nome)
                {
                    var go = new GameObject(nome); go.transform.SetParent(pai, false);
                    go.AddComponent<MeshFilter>().sharedMesh = me; go.AddComponent<MeshRenderer>().sharedMaterial = mv; return go;
                }
                for (int i = 0; i < kit.Length; i++)
                {
                    var me = Malha(kit[i].arq); if (me == null) continue;
                    var b = me.bounds; var go = Por(me, kit[i].arq);
                    float k2 = kit[i].alvo < 0 ? -kit[i].alvo * E / Mathf.Max(b.size.x, b.size.z) : kit[i].alvo * E / Mathf.Max(1e-4f, b.size.y);
                    var giro = Quaternion.Euler(0, kit[i].arq.Contains("arch") ? 290 : 200, 0);
                    go.transform.localScale = Vector3.one * k2; go.transform.rotation = giro;
                    var p = Pos(i % COLS, i / COLS); go.transform.position = p - Vector3.up * (b.min.y * k2);
                    if (kit[i].arq == "kenney_wall-arch")
                    {   // o arco do Kenney vem em duas peças: os pés e o topo, que encaixa em cima
                        var mt = Malha("kenney_wall-arch-top-detail");
                        if (mt != null)
                        {
                            var gt = Por(mt, "topo do arco"); gt.transform.localScale = Vector3.one * k2; gt.transform.rotation = giro;
                            gt.transform.position = go.transform.position + Vector3.up * ((b.max.y - mt.bounds.min.y) * k2);
                        }
                    }
                    Rotulo(p, kit[i].rot);
                }
                // última fileira: a paleta em blocos (paredes, telha, madeira, veneziana) e um boneco pra escala
                var amostras = new (int m, float h, string rot)[] { (6, 36, "reboco ocre"), (8, 36, "reboco creme"), (50, 14, "telha queimada"), (10, 20, "madeira escura"), (16, 20, "veneziana oliva") };
                int lb = nl - 1;
                for (int i = 0; i < amostras.Length; i++)
                {
                    var c = GameObject.CreatePrimitive(PrimitiveType.Cube); c.transform.SetParent(pai, false);
                    c.transform.localScale = new Vector3(0.9f, amostras[i].h * E, 0.9f); c.transform.position = Pos(i, lb) + Vector3.up * amostras[i].h * E / 2;
                    c.GetComponent<MeshRenderer>().sharedMaterial = Liso(Pal(amostras[i].m)); Rotulo(Pos(i, lb), amostras[i].rot);
                }
                var bo = CSM.View3D.Boneco3D.Criar("tr", pai); bo.transform.position = Pos(5, lb); bo.transform.rotation = Quaternion.Euler(0, 105, 0);
                bo.Pose("mira_AK47", 0, 0); Rotulo(Pos(5, lb), "boneco (escala)");
                cen.alvoJogo = new Vector2(0, 0); cen.enquadrar = Arg("-v3d_zoom", 11f); cen.Enquadrar();
                cen.cam.transform.position -= Vector3.up * (CSM.Map.MapData.GZ * E);
                foreach (var r in rotulos) r.rotation = cen.cam.transform.rotation;
                Directory.CreateDirectory("Logs/visual3d");
                Salvar(cen.cam, "Logs/visual3d/kit_brasa.png", 1920, 1080);
                Debug.Log($"[Visual3D] kit da Brasa: {kit.Length - falta} props, {falta} faltando");
                EditorApplication.Exit(falta == 0 ? 0 : 2);
            }
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
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };   // MSAA igual ao jogo (com 1 o print saía serrilhado)
            c.targetTexture = rt;
            c.Render(); if (!aquecido) c.Render();   // o 1º print renderiza 2x (aquece sombra/SSAO); no vídeo o quadro anterior já aqueceu
            aquecido = videoRodando;
            var old = RenderTexture.active; RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            RenderTexture.active = old; c.targetTexture = null;
            if (hud != null)
            {
                // HUD renderizado 2x (fundo preto e fundo branco): a diferença dá a cobertura exata de cada pixel e o preto dá a
                // cor já multiplicada, então a borda suave do texto/painel compõe certo por cima da cena (a alfa da textura do
                // HUD sozinho sai ao quadrado com o blend da UI e a borda ficava dura e serrilhada). Conta em espaço linear.
                var rh = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32) { antiAliasing = 1 };
                hud.canvas.renderMode = RenderMode.ScreenSpaceCamera; hud.canvas.worldCamera = hud.cam;
                var fundo0 = hud.cam.backgroundColor;
                Color[] Hud(Color fundo)
                {
                    hud.cam.backgroundColor = fundo; hud.cam.targetTexture = rh; Canvas.ForceUpdateCanvases(); hud.cam.Render(); hud.cam.targetTexture = null;
                    RenderTexture.active = rh;
                    var th = new Texture2D(w, h, TextureFormat.RGBA32, false); th.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                    RenderTexture.active = old;
                    var px = th.GetPixels(); Object.DestroyImmediate(th); return px;
                }
                var kp = Hud(Color.black); var kb = Hud(Color.white);
                hud.cam.backgroundColor = fundo0; hud.canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var cena = tex.GetPixels();
                static float Lin(float v) => Mathf.GammaToLinearSpace(v);
                for (int i = 0; i < cena.Length; i++)
                {
                    Color p = kp[i], q = kb[i];
                    float cob = 1 - ((q.r - p.r) + (q.g - p.g) + (q.b - p.b)) / 3f;   // em gama dá ~igual; refina abaixo
                    if (cob <= 0.001f) continue;
                    float pr = Lin(p.r), pg = Lin(p.g), pb = Lin(p.b);
                    float fica = Mathf.Clamp01(((Lin(q.r) - pr) + (Lin(q.g) - pg) + (Lin(q.b) - pb)) / 3f);   // 1 - cobertura
                    var cc = cena[i];
                    cena[i] = new Color(Mathf.LinearToGammaSpace(pr + fica * Lin(cc.r)), Mathf.LinearToGammaSpace(pg + fica * Lin(cc.g)), Mathf.LinearToGammaSpace(pb + fica * Lin(cc.b)), 1);
                }
                tex.SetPixels(cena); tex.Apply();
                rh.Release(); Object.DestroyImmediate(rh);
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
