using System.Diagnostics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CSM.Map;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// O cenário do visual de maquete: mapa 3D (MalhaMapa), sol aprovado do 2D (Iso.SUN), luz ambiente de três cores
    /// (céu, horizonte quente, chão rebatido), câmera de lente longa na elevação do jogo (50°, olhando pro noroeste) e
    /// pós-processamento de miniatura: desfoque de profundidade nas bordas (tilt-shift), bloom, cor viva.
    /// </summary>
    public class Cenario3D : MonoBehaviour
    {
        [Header("Câmera")]
        public float fov = 20f;
        public float elevacao = 50f;
        public float enquadrar = 14f;          // altura do quadro em unidades Unity (boneco = 1)
        public Vector2 alvoJogo = new Vector2(837, 187);   // ponto do mapa no centro (x, y do jogo)
        [Header("Miniatura")]
        public float abertura = 3.6f, focal = 300f;   // (era 1,8: usuário achou tudo sem nitidez)
        public float arredondado = 1.1f;

        public Camera cam; public Light sol; public Volume vol; public GameObject mapa;
        public static Material matMapa, matCor;
        public static double msMalha;

        void Awake() { if (mapa == null) Montar(); }

        public void Montar()
        {
            var sw = Stopwatch.StartNew();
            if (matMapa == null) matMapa = new Material(Shader.Find("CSM/Maquete")) { name = "Maquete mapa" };
            bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
            // caixas de madeira saem do borrão (viravam saco de areia) e entram como caixote de quina viva
            // enfeites de fachada "pintados" viram objetos 3D (a pintura sai das voxels antes do borrão)
            var G = MapVoxels.Build();
            var enfeites = Fachada3D.Montar(G, matMapa, transform);
            Natureza3D.Plantar(Fachada3D.Plantas, transform);   // plantas do pack nos vasos que a fachada marcou
            // enfeites miúdos (jarros, sacos, cestos, barris, tralhas de telhado, rodas da carroça) também saem do borrão
            // (viravam "blobs") e entram como peça de quina viva
            // o chafariz da base TR também sai do borrão e entra como modelo (Kenney, Chafariz3D)
            var GM = Adornos3D.Limpar(Caixas3D.SemCaixas(Pilar3D.SemPilares(Chafariz3D.SemChafariz(G))));   // pilares também (Kenney, Pilar3D)
            var ps = MalhaMapa.Gerar(GM, arredondado, 100, linear);
            matMapa.SetTexture("_Topo", MalhaMapa.Topo(GM, linear)); matMapa.SetFloat("_UsarTopo", 1); matMapa.SetFloat("_Esc", MalhaMapa.ESC);
            mapa = MalhaMapa.Montar(ps, matMapa, transform);
            Caixas3D.Montar(matMapa, mapa.transform, linear);
            Chafariz3D.Montar(matMapa, mapa.transform, linear);
            Pilar3D.Montar(matMapa, mapa.transform, linear);
            Adornos3D.Montar(matMapa, mapa.transform);
            Packs3D.Montar(matMapa, mapa.transform, linear);   // barris, sacos e caixotes de pack (o Adornos3D marcou onde)
            enfeites.transform.SetParent(mapa.transform, true);
            msMalha = sw.Elapsed.TotalMilliseconds;
            if (matCor == null)
            {
                matCor = new Material(Shader.Find("CSM/Maquete")) { name = "Maquete cor" };
                matCor.SetFloat("_UsarVertice", 0); matCor.SetFloat("_Smoothness", 0.22f); matCor.SetFloat("_Variacao", 0.12f);
            }
            Arredores.Montar(matMapa, matCor, transform);

            // sol: mesma direção do 2D aprovado (SUN aponta PRO sol, em coordenadas do jogo)
            var go = new GameObject("Sol"); go.transform.SetParent(transform, false);
            sol = go.AddComponent<Light>(); sol.type = LightType.Directional;
            var s = Iso.SUN; var paraSol = new Vector3(s.x, s.z, -s.y).normalized;
            sol.transform.rotation = Quaternion.LookRotation(-paraSol);
            sol.color = new Color(1f, 0.93f, 0.80f); sol.intensity = 1.35f;
            sol.shadows = LightShadows.Soft; sol.shadowStrength = 0.86f;
            sol.shadowBias = 0.02f; sol.shadowNormalBias = 0.25f;

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.70f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.78f, 0.66f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.46f, 0.36f, 0.26f);
            RenderSettings.ambientIntensity = 0.9f;
            RenderSettings.fog = false;

            if (cam == null) cam = Camera.main;
            if (cam == null) { var cg = new GameObject("Câmera 3D") { tag = "MainCamera" }; cam = cg.AddComponent<Camera>(); }
            cam.orthographic = false; cam.fieldOfView = fov;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.86f, 0.72f, 0.5f);   // areia (o fundo era o verde do campo antigo)
            cam.nearClipPlane = 1f; cam.farClipPlane = 400f; cam.cullingMask = ~0;
            var cd = cam.GetUniversalAdditionalCameraData();
            cd.renderPostProcessing = true; cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;
            cd.antialiasingQuality = AntialiasingQuality.High;
            Enquadrar();

            var vg = new GameObject("Pós (miniatura)"); vg.transform.SetParent(transform, false);
            vol = vg.AddComponent<Volume>(); vol.isGlobal = true;
            var p = ScriptableObject.CreateInstance<VolumeProfile>();
            var dof = p.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Bokeh);
            dof.focusDistance.Override(Distancia()); dof.focalLength.Override(focal); dof.aperture.Override(abertura);
            // bloom só no que brilha de verdade (clarão, fogo, laser): antes pegava a areia clara e lavava a imagem
            var bl = p.Add<Bloom>(true); bl.threshold.Override(1.35f); bl.intensity.Override(0.22f); bl.scatter.Override(0.55f);
            var tm = p.Add<Tonemapping>(true); tm.mode.Override(TonemappingMode.Neutral);
            var ca = p.Add<ColorAdjustments>(true); ca.saturation.Override(12f); ca.contrast.Override(18f); ca.postExposure.Override(-0.25f);   // menos claro, mais contraste
            var wb = p.Add<WhiteBalance>(true); wb.temperature.Override(6f);
            var vi = p.Add<Vignette>(true); vi.intensity.Override(0.22f); vi.smoothness.Override(0.5f);
            vol.sharedProfile = p;
        }

        float Distancia() => enquadrar * 0.5f / Mathf.Tan(fov * 0.5f * Mathf.Deg2Rad);

        /// <summary>Põe a câmera olhando pro alvo (x, y do jogo, no chão) na elevação do jogo, vindo do sudeste.</summary>
        public void Enquadrar()
        {
            var alvo = MalhaMapa.ParaUnity(alvoJogo.x, alvoJogo.y, MapData.GZ);
            float el = elevacao * Mathf.Deg2Rad;
            var deAlvo = new Vector3(Mathf.Cos(el) / Mathf.Sqrt(2), Mathf.Sin(el), -Mathf.Cos(el) / Mathf.Sqrt(2));
            cam.transform.position = alvo + deAlvo * Distancia();
            cam.transform.rotation = Quaternion.LookRotation(-deAlvo, Vector3.up);
            // desfoque de miniatura constante na tela: com a câmera mais perto a faixa nítida encolheria (o CoC cresce com
            // 1/distância), então a abertura fecha na mesma proporção do zoom (7,5 = enquadramento de jogo)
            if (vol != null && vol.sharedProfile.TryGet(out DepthOfField d))
            {
                d.focusDistance.Override(Distancia());
                d.aperture.Override(Mathf.Clamp(abertura * 7.5f / Mathf.Max(1f, enquadrar), 1f, 32f));
            }
        }
    }
}
