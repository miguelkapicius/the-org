using System;
using UnityEngine;
using CSM.View3D;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Renderiza os bonecos 3D do jogo (os mesmos da partida: TR e CT do Blender) em duas RenderTextures, uma por lado,
    /// para a tela de pré-partida mostrá-los como imagem dentro da UI Toolkit. O "estúdio" fica longe da origem
    /// (y = 2000) e só as câmeras de preview o enxergam, porque a câmera da cena do manager não desenha nada.
    /// </summary>
    public class BonecosPreview : MonoBehaviour
    {
        public const int LARG = 768, ALT = 1032;   // mesma proporção do elemento .pm-boneco (640x860)
        public RenderTexture rtEsq, rtDir;
        Boneco3D esq, dir; Camera camEsq, camDir;

        /// <summary>Cria o estúdio; devolve null (e avisa) se os bonecos não puderem ser carregados.</summary>
        public static BonecosPreview Criar()
        {
            try
            {
                var go = new GameObject("Estúdio dos bonecos") { hideFlags = HideFlags.DontSave };
                go.transform.position = new Vector3(0, 2000, 0);
                var p = go.AddComponent<BonecosPreview>();
                p.Montar();
                return p;
            }
            catch (Exception e)
            {
                Debug.LogWarning("Pré-partida sem bonecos 3D (" + e.Message + "). Eles vêm de Logs/bonecos3d.");
                return null;
            }
        }

        void Montar()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.5f, 0.5f, 0.55f);

            var sol = new GameObject("Luz principal").AddComponent<Light>();
            sol.transform.SetParent(transform, false); sol.transform.rotation = Quaternion.Euler(35, -25, 0);
            sol.type = LightType.Directional; sol.intensity = 1.5f; sol.shadows = LightShadows.None;
            var contra = new GameObject("Luz de contorno").AddComponent<Light>();
            contra.transform.SetParent(transform, false); contra.transform.rotation = Quaternion.Euler(20, 160, 0);
            contra.type = LightType.Directional; contra.intensity = 0.9f; contra.shadows = LightShadows.None;

            // esquerda = TR (nosso time, laranja) olhando um pouco pro centro; direita = CT (adversário, azul)
            (esq, camEsq, rtEsq) = Lado("tr", 0, 24f, "mira_AK47");
            (dir, camDir, rtDir) = Lado("ct", 30, -24f, "mira_M4A4");
        }

        (Boneco3D, Camera, RenderTexture) Lado(string time, float x, float giro, string anim)
        {
            var b = Boneco3D.Criar(time, transform);
            b.transform.localPosition = new Vector3(x, 0, 0);
            b.transform.localRotation = Quaternion.Euler(0, giro, 0);
            if (!b.Tem(anim)) anim = "mira_AK47";
            b.Pose(anim, 0, 0);
            b.animAtual = anim;

            var rt = new RenderTexture(LARG, ALT, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4, name = "Boneco " + time };
            // enquadra o corpo inteiro: a câmera se ajusta à altura do boneco (ele é um "chibi", bem mais baixo que 1,8 m).
            // O elemento da UI (.pm-boneco) corta a parte de baixo, então na tela aparecem cabeça e tronco, grandes.
            Bounds? caixa = null;
            foreach (var r in b.GetComponentsInChildren<Renderer>())
            {
                if (!r.enabled || r.sharedMaterial != Boneco3D.matCorpo) continue;
                if (caixa == null) caixa = r.bounds; else { var cx = caixa.Value; cx.Encapsulate(r.bounds); caixa = cx; }
            }
            float altura = caixa.HasValue ? caixa.Value.size.y : 1.2f;
            float topo = (caixa.HasValue ? caixa.Value.max.y : 1.2f) - transform.position.y;
            const float FOV = 22f, OCUPA = 0.66f, MARGEM = 0.06f;   // o boneco ocupa 66% da altura da textura, com 6% de folga em cima
            float viewH = altura / OCUPA;
            float centroY = topo + MARGEM * viewH - viewH / 2f;
            float dist = viewH / 2f / Mathf.Tan(FOV * 0.5f * Mathf.Deg2Rad);
            var cam = new GameObject("Câmera " + time).AddComponent<Camera>();
            cam.transform.SetParent(transform, false);
            cam.transform.localPosition = new Vector3(x, centroY, dist);
            cam.transform.localRotation = Quaternion.Euler(0, 180, 0);
            cam.fieldOfView = FOV; cam.nearClipPlane = 0.1f; cam.farClipPlane = 60;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0, 0, 0, 0);
            cam.cullingMask = ~0; cam.targetTexture = rt; cam.enabled = false;
            return (b, cam, rt);
        }

        void LateUpdate()
        {
            // respira parado (a própria pose já faz o peito subir e descer) e redesenha a textura
            if (esq != null) esq.Pose(esq.animAtual, 0, Time.deltaTime);
            if (dir != null) dir.Pose(dir.animAtual, 0, Time.deltaTime);
            if (camEsq != null) camEsq.Render();
            if (camDir != null) camDir.Render();
        }

        void OnDestroy()
        {
            if (rtEsq != null) { rtEsq.Release(); Destroy(rtEsq); }
            if (rtDir != null) { rtDir.Release(); Destroy(rtDir); }
        }
    }
}
