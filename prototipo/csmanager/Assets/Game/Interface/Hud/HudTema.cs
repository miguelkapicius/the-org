using UnityEngine;

namespace CSM.View3D
{
    /// <summary>As duas caras do HUD da partida (decisão 0032): <b>Clássico</b> (o visual de antes, tons quentes de marrom e creme, cantos redondos) e
    /// <b>Novo</b> (a "mesa de transmissão" do manager: grafite neutro, branco frio, cantos de 3 px, sem sombra). A escolha vem das Opções
    /// (<see cref="CSM.ManagerUI.Opcoes.HudNovo"/>), é lida uma vez ao montar a partida e pode ser forçada por argumento de linha de comando
    /// (<c>-hud_tema novo</c> ou <c>-hud_tema classico</c>, usado nas capturas). Voltar ao visual de antes é só trocar essa opção; nenhuma
    /// peça do HUD foi apagada, as duas caras só leem cores diferentes daqui.</summary>
    public static class HudTema
    {
        static bool novo = true;

        /// <summary>O HUD novo está ligado.</summary>
        public static bool Novo => novo;

        /// <summary>Lê a escolha (argumento de linha de comando primeiro, depois as Opções). Chamar antes de montar o HUD.</summary>
        public static void Carregar()
        {
            novo = CSM.ManagerUI.Opcoes.HudNovo;
            var a = System.Environment.GetCommandLineArgs();
            for (int i = 0; i < a.Length - 1; i++)
                if (a[i] == "-hud_tema") novo = a[i + 1] != "classico";
        }

        /// <summary>Força o tema (editor e testes).</summary>
        public static void Definir(bool ligarNovo) => novo = ligarNovo;

        static Color Hx(string h) { ColorUtility.TryParseHtmlString(h, out var c); return c; }
        static Color Es(string antigo, string novoHex) => Hx(Novo ? novoHex : antigo);

        // ---- forma
        /// <summary>Raio dos cantos dos painéis (px da referência 1920×1080).</summary>
        public static float Raio => Novo ? 3f : 10f;
        /// <summary>Painéis com sombra projetada (só no clássico).</summary>
        public static bool Sombra => !Novo;

        // ---- lados
        public static Color CT => Es("#5B9BFF", "#62A8EE");
        public static Color TR => Es("#F5A524", "#F0A640");
        public static Color PlacarTR => Es("#FFB43C", "#F0A640");
        public static Color PlacarCT => Es("#6FA8FF", "#62A8EE");

        // ---- superfícies e texto
        public static Color Escuro => Novo ? new Color(11 / 255f, 13 / 255f, 16 / 255f, 0.9f) : new Color(31 / 255f, 27 / 255f, 24 / 255f, 0.92f);
        public static Color Creme => Es("#FFF4E2", "#ECEEF1");
        public static Color Cinza => Novo ? new Color(0.643f, 0.667f, 0.706f, 0.6f) : new Color(1f, 0.957f, 0.886f, 0.45f);
        public static Color Vazio => Novo ? new Color(1f, 1f, 1f, 0.08f) : new Color(1f, 0.957f, 0.886f, 0.07f);
        public static Color Verde => Es("#8BE79C", "#4BBF8C");
        public static Color Vermelho => Es("#E5382E", "#EC6A5F");

        // ---- vida e caixas dos cards
        public static Color VidaTR => Es("#D9831A", "#2F3238");
        public static Color VidaCT => Es("#2F6FD6", "#2F3238");
        public static Color VidaBaixa => Es("#D63A2F", "#5E2724");
        public static Color CaixaTR => Es("#A85A00", "#33260F");
        public static Color CaixaCT => Es("#1E4FB0", "#12294A");

        // ---- minimapa
        public static Color MapaPiso => Es("#2B2622", "#181B20");
        public static Color MapaAlto => Es("#3D3631", "#23272E");
        public static Color MapaLinha => Es("#D9CCB8", "#AEB4BD");
        public static Color MapaCaixa => Es("#8A8079", "#5A606A");
        public static Color MapaSite => Es("#F2C200", "#F0A640");
        public static Color MarcaTxt => Es("#1A1714", "#0B0D10");
        public static Color MarcaT => Es("#F2C76B", "#F5BB66");
        public static Color MarcaCT => Es("#8EC0F5", "#8EBFF0");

        // ---- C4
        public static Color C4Vermelho => Es("#E5382E", "#EC6A5F");
        public static Color C4Ambar => Es("#F5A524", "#F0A640");
        public static Color C4Azul => Es("#5B9BFF", "#62A8EE");

        // ---- killfeed
        public static Color KfT => Es("#eabe54", "#F0B25A");
        public static Color KfCT => Es("#6f9ce6", "#76B0EE");
        public static Color KfFundo => Novo ? new Color(0.043f, 0.051f, 0.063f, 0.82f) : new Color(0.07f, 0.065f, 0.06f, 0.78f);

        // ---- placar do fim (Menu3D) e botão principal
        /// <summary>Cor do botão de ação principal: laranja no clássico, claro sólido no novo (como no manager).</summary>
        public static Color Acao => Es("#F5A524", "#ECEEF1");
        public static Color AcaoRealce => Es("#FFC14D", "#FFFFFF");
        public static Color AcaoApertada => Es("#C9831A", "#C3C7CD");
        public static Color Tinta => Es("#1A1714", "#0B0D10");
    }
}
