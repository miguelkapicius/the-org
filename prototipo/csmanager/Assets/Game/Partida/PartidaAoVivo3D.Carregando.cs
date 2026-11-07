using System.Collections.Generic;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>Tela de carregando da partida (decisão 0031) e a volta ao manager. O carregamento trava a Unity por alguns segundos (monta a maquete
    /// e aquece a simulação), então a tela é desenhada cheia, na mesma linguagem do manager, antes de cada trava; depois some num fade.</summary>
    public partial class PartidaAoVivo3D
    {
        const float FADE_CARREGANDO = 0.6f;
        float fimCarregar = -1f;                 // quando o carregamento acabou (para o fade da tela)
        GUIStyle sCarSmall, sCarTitulo, sCarSub, sCarDica;

        /// <summary>A partida veio do manager (série da carreira): sem menu inicial, com carregando e com o botão de voltar no fim.</summary>
        public static bool DoManager { get { var c = CSM.Manager.Carreira.Atual; return Application.isPlaying && c != null && c.emPartida; } }

        /// <summary>Opacidade da tela de carregando: 1 enquanto carrega, some em meio segundo depois.</summary>
        float AlfaCarregando()
        {
            if (carregando) return 1f;
            if (fimCarregar < 0f || !DoManager) return 0f;
            return Mathf.Clamp01(1f - (Time.realtimeSinceStartup - fimCarregar) / FADE_CARREGANDO);
        }

        void MontarEstilosCarregando()
        {
            var barlowC = Resources.Load<Font>("ManagerUI/Fontes/BarlowCondensed-SemiBold");
            var barlow = Resources.Load<Font>("ManagerUI/Fontes/Barlow-Medium");
            GUIStyle Novo(Font f, int tam, Color cor, FontStyle est = FontStyle.Normal)
            {
                var s = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = tam, wordWrap = true, richText = false, fontStyle = est };
                if (f != null) s.font = f; s.normal.textColor = cor; return s;
            }
            sCarSmall = Novo(barlow, 16, new Color(0.96f, 0.647f, 0.141f));
            sCarTitulo = Novo(barlowC, 72, Color.white);
            sCarSub = Novo(barlow, 20, new Color(0.62f, 0.65f, 0.69f));
            sCarDica = Novo(barlow, 14, new Color(0.45f, 0.48f, 0.52f));
        }

        /// <summary>Desenha a tela de carregando com a opacidade <paramref name="a"/>.</summary>
        void DesenharCarregando(float a)
        {
            if (a <= 0f) return;
            if (sCarTitulo == null) MontarEstilosCarregando();
            var c = CSM.Manager.Carreira.Atual; var s = DoManager ? c.serie : null;
            string titulo = s != null ? c.clube + "  ×  " + s.adv.nome : "CS MANAGER";
            string sub = s != null ? s.camp.nome + (s.MapaAtual >= 0 ? "  ·  " + CSM.Manager.Mapas.NOMES[s.MapaAtual] : "") : "Montando a partida";
            string estado = aquecendo ? "Aquecendo a simulação…" : "Montando o mapa…";

            float k = Mathf.Max(0.5f, Screen.height / 900f);
            float W = Screen.width / k, H = Screen.height / k;
            var antesCor = GUI.color; var antesM = GUI.matrix;
            GUI.matrix = Matrix4x4.Scale(new Vector3(k, k, 1));
            GUI.color = new Color(0.043f, 0.051f, 0.063f, a);
            GUI.DrawTexture(new Rect(0, 0, W, H), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);

            float cy = H * 0.5f;
            GUI.Label(new Rect(0, cy - 120, W, 24), "PREPARANDO A PARTIDA", sCarSmall);
            GUI.Label(new Rect(40, cy - 90, W - 80, 100), titulo, sCarTitulo);
            GUI.Label(new Rect(0, cy + 14, W, 30), sub, sCarSub);

            // barra que corre (a Unity trava durante a montagem; ela anda nos quadros em que dá)
            float bw = 360, bx = (W - bw) / 2, by = cy + 70;
            GUI.color = new Color(1f, 1f, 1f, 0.08f * a); GUI.DrawTexture(new Rect(bx, by, bw, 3), Texture2D.whiteTexture);
            float t = (Time.realtimeSinceStartup * 0.9f) % 1f, seg = 110, px = bx + (bw + seg) * t - seg;
            float x0 = Mathf.Max(bx, px), x1 = Mathf.Min(bx + bw, px + seg);
            GUI.color = new Color(0.925f, 0.933f, 0.945f, a);
            if (x1 > x0) GUI.DrawTexture(new Rect(x0, by, x1 - x0, 3), Texture2D.whiteTexture);
            GUI.color = new Color(1f, 1f, 1f, a);
            GUI.Label(new Rect(0, by + 14, W, 24), estado, sCarSub);

            GUI.Label(new Rect(0, H - 60, W, 24), "ESPAÇO pausa  ·  N próximo round  ·  P pensamento dos bots  ·  TAB placar  ·  clique num jogador para segui-lo", sCarDica);
            GUI.color = antesCor; GUI.matrix = antesM;
        }

        /// <summary>Fecha a partida da carreira (prêmio, salários, desempenho de cada jogador) e volta ao manager.</summary>
        public void VoltarAoManager()
        {
            var carreira = CSM.Manager.Carreira.Atual;
            if (!acabou || carreira == null || !carreira.emPartida) return;
            bool venceu = pt.vencedor == pt.A;
            // números reais de cada jogador, vindos da simulação (viram a tela de pós-partida)
            var desempenho = new List<CSM.Manager.Desempenho>();
            for (int jj = 0; jj < pt.A.js.Length; jj++)
            {
                var pj = pt.A.js[jj];
                desempenho.Add(new CSM.Manager.Desempenho { nome = pj.nome, funcao = pj.roleT, k = pj.k, d = pj.d, hs = pj.hs, dano = pj.dano, fk = pj.fk, rounds = Mathf.Max(1, pj.rounds) });
            }
            carreira.Registrar(venceu, pt.A.placar, pt.B.placar, true, desempenho, pt.jogada);
            CSM.ManagerUI.Salvamento.Salvar(carreira);
            UnityEngine.SceneManagement.SceneManager.LoadScene("Manager");
        }
    }
}
