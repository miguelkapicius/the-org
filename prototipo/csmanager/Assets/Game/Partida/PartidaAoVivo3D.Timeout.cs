using UnityEngine;
using CSM.Manager;

namespace CSM.View3D
{
    // Timeout do manager (decisão 0018): T ou o botão abre o painel, a transmissão pausa, o manager vê a chance de o time
    // absorver e escolhe o estilo. O efeito começa no próximo round; o aviso do resultado fica alguns segundos na tela.
    public partial class PartidaAoVivo3D
    {
        bool timeoutAberto, pausadoAntesDoTimeout;
        string avisoTimeout; bool avisoTimeoutOk; float avisoTimeoutAte;

        bool PodePedirTimeout => !Vitrine && !acabou && pt != null && pt.A.timeouts > 0 && replay == null;
        static int NivelGH => Carreira.Atual?.nivelGH ?? 1;

        void AbrirTimeout()
        {
            if (timeoutAberto) { FecharTimeout(); return; }
            if (!PodePedirTimeout) return;
            timeoutAberto = true; pausadoAntesDoTimeout = pausado; pausado = true;
        }

        void FecharTimeout() { timeoutAberto = false; pausado = pausadoAntesDoTimeout && !acabou; }

        void EscolherTimeout(Estilo e)
        {
            var p = Timeouts.Pedir(pt, true, e, NivelGH);
            FecharTimeout();
            if (p == null) return;
            avisoTimeoutOk = p.funcionou;
            avisoTimeout = p.funcionou
                ? $"<b>TIMEOUT</b>  O time absorveu: joga <b>{Estilos.Nome(e)}</b> a partir do próximo round, com foco por {Timeouts.ROUNDS_FOCO} rounds."
                : "<b>TIMEOUT</b>  O time não absorveu a conversa e segue jogando como estava.";
            avisoTimeoutAte = Time.unscaledTime + 6f;
        }

        static string Pct(float v) => (v >= 0 ? "+" : "") + Mathf.RoundToInt(v * 100) + "%";

        /// <summary>Painel do timeout e aviso do resultado (chamado no OnGUI, já na escala da tela de 720 de altura).</summary>
        void TimeoutGUI(float W, float H)
        {
            if (avisoTimeout != null && Time.unscaledTime < avisoTimeoutAte)
            {
                var ra = new Rect(W / 2 - 300, 92, 600, 44);
                GUI.Box(ra, GUIContent.none, sCaixa);
                var s = new GUIStyle(sTxt) { fontSize = 14, alignment = TextAnchor.MiddleCenter };
                s.normal.textColor = avisoTimeoutOk ? new Color(0.78f, 0.95f, 0.35f) : new Color(1f, 0.62f, 0.45f);
                GUI.Label(ra, avisoTimeout, s);
            }
            if (!timeoutAberto) return;
            if (!PodePedirTimeout) { FecharTimeout(); return; }

            var c = Timeouts.Calcular(pt, true, NivelGH);
            var atual = Timeouts.EstiloAtual(pt, true);
            var r = new Rect(W / 2 - 260, H / 2 - 210, 520, 420);
            GUI.Box(r, GUIContent.none, sCaixa); GUI.Box(r, GUIContent.none, sCaixa);   // duas camadas: mais opaco que o resto do HUD
            float y = r.y + 12, x = r.x + 16, w = r.width - 32;
            var tit = new GUIStyle(sTxt) { fontSize = 18 };
            GUI.Label(new Rect(x, y, w, 26), $"<b>Timeout</b>  ·  restam {pt.A.timeouts} de {Sim.Partida.TIMEOUTS}", tit); y += 30;
            GUI.Label(new Rect(x, y, w, 22), $"Chance de o time absorver: <b>{Mathf.RoundToInt(c.total * 100)}%</b>", new GUIStyle(sTxt) { fontSize = 14 }); y += 22;
            GUI.Label(new Rect(x, y, w, 36),
                $"sangue-frio médio {Mathf.RoundToInt(c.sangueFrioMedio)} ({Pct(c.sangueFrio)}) · Gaming House nível {NivelGH} ({Pct(c.gamingHouse)}) · " +
                $"momento: {c.derrotasSeguidas} round(s) perdido(s) seguido(s){(c.vantagem >= Timeouts.EMBALO_VANTAGEM ? ", vencendo bem" : "")} ({Pct(c.momento)})", sTxt);
            y += 40;
            GUI.Label(new Rect(x, y, w, 20), "Escolha como o time vai jogar a partir do próximo round:", sTxt); y += 22;
            foreach (Estilo e in System.Enum.GetValues(typeof(Estilo)))
            {
                var rb = new Rect(x, y, w, 44);
                string rot = $"<b>{Estilos.Nome(e)}</b>{(e == atual ? "  (estilo atual: acalma o time)" : "")}\n<size=11>{Estilos.Descricao(e)}</size>";
                var sb = new GUIStyle(sBotao) { richText = true, alignment = TextAnchor.MiddleLeft, wordWrap = true, fontSize = 12 };
                bool clicou = GUI.Button(rb, rot, sb); CSM.Audio.SomUI.Gui(rb, clicou);
                if (clicou) { EscolherTimeout(e); return; }
                y += 48;
            }
            var rc = new Rect(r.xMax - 116, r.yMax - 32, 100, 22);
            bool cancela = GUI.Button(rc, "Cancelar (T)", sBotao); CSM.Audio.SomUI.Gui(rc, cancela);
            if (cancela) FecharTimeout();
        }
    }
}
