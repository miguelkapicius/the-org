using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using System.Collections.Generic;

namespace CSM.Audio
{
    /// <summary>
    /// Som de interface (sessão "Sons das armas"): estalinho ao passar o mouse num botão e clique ao apertar.
    /// Liga SOZINHO em todo botão uGUI (qualquer Selectable interativo debaixo do mouse); ninguém precisa chamar nada.
    /// Botões IMGUI (GUI.Button) não passam pelo EventSystem: quem desenha chama SomUI.Gui(rect, clicou) no OnGUI.
    /// Toca 2D, fora do Som3D: o menu deixa o Som3D mudo (vitrine) e o botão tem que soar mesmo assim.
    /// Clipes: Resources/Sons/ui/passar_K.wav e clicar_K.wav (Tools/sons/ui_botao.py).
    /// </summary>
    public class SomUI : MonoBehaviour
    {
        public static float VOL_PASSAR = 0.5f, VOL_CLICAR = 0.7f;
        static SomUI eu;
        AudioSource src; AudioClip[] passar, clicar; int ultP = -1, ultC = -1;
        Object sobre;                 // botão uGUI debaixo do mouse no quadro anterior
        int guiAgora, guiAntes;       // id do botão IMGUI debaixo do mouse (este OnGUI / quadro anterior)
        float ultimoPassar = -1;
        readonly List<RaycastResult> acertos = new List<RaycastResult>();
        PointerEventData ponteiro;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Ligar()
        {
            if (eu) return;
            var go = new GameObject("SomUI"); DontDestroyOnLoad(go); eu = go.AddComponent<SomUI>();
        }

        void Awake()
        {
            src = gameObject.AddComponent<AudioSource>(); src.playOnAwake = false; src.spatialBlend = 0; src.priority = 10;
            src.ignoreListenerPause = true; src.bypassReverbZones = true;
            passar = Carregar("passar"); clicar = Carregar("clicar");
        }

        static AudioClip[] Carregar(string nome)
        {
            var l = new List<AudioClip>();
            for (int k = 0; k < 8; k++) { var a = Resources.Load<AudioClip>("Sons/ui/" + nome + "_" + k); if (a == null) break; l.Add(a); }
            return l.ToArray();
        }

        void Tocar(AudioClip[] cs, ref int ult, float vol)
        {
            if (cs.Length == 0) return;
            if (!FindAnyObjectByType<AudioListener>()) Som3D.GarantirOuvido();   // sem ouvido a Unity não toca nada
            int i = cs.Length == 1 ? 0 : Random.Range(0, cs.Length - 1); if (cs.Length > 1 && i >= ult) i++;
            ult = i; src.PlayOneShot(cs[i], vol * Som3D.volumeGeral);
        }

        void Passou()
        {
            if (Time.unscaledTime - ultimoPassar < 0.03f) return;   // varrer vários botões colados não vira metralhadora
            ultimoPassar = Time.unscaledTime; Tocar(passar, ref ultP, VOL_PASSAR);
        }

        void Update()
        {
            var es = EventSystem.current; Selectable s = null;
            if (es && Input.mousePresent)
            {
                ponteiro ??= new PointerEventData(es);
                ponteiro.position = Input.mousePosition; acertos.Clear(); es.RaycastAll(ponteiro, acertos);
                if (acertos.Count > 0 && acertos[0].gameObject)
                {
                    s = acertos[0].gameObject.GetComponentInParent<Selectable>();
                    if (s && !(s.IsInteractable() && s.isActiveAndEnabled)) s = null;
                }
            }
            if (s && s != sobre) Passou();
            if (s && Input.GetMouseButtonDown(0)) Tocar(clicar, ref ultC, VOL_CLICAR);
            sobre = s;
            // IMGUI: o id do quadro anterior vale até o próximo OnGUI
            if (guiAgora != 0 && guiAgora != guiAntes) Passou();
            guiAntes = guiAgora; guiAgora = 0;
        }

        /// <summary>Pra botão IMGUI: chamar no OnGUI com o retângulo do botão (mesmo espaço do GUI.Button) e o que ele
        /// devolveu. Ex.: bool r = GUI.Button(rect, t, estilo); SomUI.Gui(rect, r);</summary>
        public static void Gui(Rect r, bool clicou)
        {
            if (!eu) return;
            if (clicou) eu.Tocar(eu.clicar, ref eu.ultC, VOL_CLICAR);
            if (Event.current != null && Event.current.type == EventType.Repaint && r.Contains(Event.current.mousePosition))
                eu.guiAgora = (r.x, r.y, r.width, r.height).GetHashCode() | 1;
        }
    }
}
