using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Tela de opções (decisão 0030): som, tamanho da interface e janela. Cada escolha vale na hora; só o conteúdo é refeito (a tela não pisca).</summary>
    public partial class GerenteUI
    {
        PanelSettings painel;   // o mesmo do UIDocument: o tamanho da interface muda a referência dele

        void AplicarTamanhoInterface() { if (painel != null) painel.referenceResolution = Opcoes.Referencia(); }

        void AtualizarOpcoes() { if (dicaEl != null) dicaEl.style.display = DisplayStyle.None; conteudo.Clear(); TelaOpcoes(); }

        /// <summary>Uma linha da tela: o nome e uma frase à esquerda, as escolhas (chips) à direita.</summary>
        VisualElement LinhaOpcao(string nome, string ajuda, params VisualElement[] escolhas)
        {
            var l = E("opc-linha");
            var esq = E("opc-esq"); esq.Add(L(nome, "f16", "negrito")); if (ajuda != null) esq.Add(L(ajuda, "f13", "apagado", "quebra"));
            l.Add(esq);
            var dir = E("opc-chips"); foreach (var e in escolhas) dir.Add(e);
            l.Add(dir);
            return l;
        }

        VisualElement ChipsVolume(float atual, Action<float> definir)
        {
            var g = E("opc-chips-g");
            foreach (var v in Opcoes.PassosVolume)
            {
                float vv = v;
                g.Add(BotaoMini(vv <= 0f ? "Mudo" : Mathf.RoundToInt(vv * 100) + "%", () => { definir(vv); AtualizarOpcoes(); SomManager.Tocar(Som.Clique); }, Mathf.Abs(atual - vv) < 0.01f));
            }
            return g;
        }

        void TelaOpcoes()
        {
            // ---- som
            conteudo.Add(L("Som", "h2"));
            var som = E("cartao");
            som.Add(LinhaOpcao("Sons da interface", "Cliques, avisos e a negociação. Passar o mouse não toca nada.",
                BotaoMini("Ligado", () => { SomManager.Mudo = false; AtualizarOpcoes(); SomManager.Tocar(Som.Clique); }, !SomManager.Mudo),
                BotaoMini("Desligado", () => { SomManager.Mudo = true; AtualizarOpcoes(); }, SomManager.Mudo)));
            som.Add(LinhaOpcao("Volume dos efeitos", "Já nascem leves; 50 % é o padrão.",
                ChipsVolume(Opcoes.VolumeEfeitos, v => { Opcoes.VolumeEfeitos = v; SomManager.AplicarVolumes(); })));
            som.Add(LinhaOpcao("Zumbido de fundo", "A sala de controle ao fundo, bem baixo, enquanto o manager está aberto.",
                ChipsVolume(Opcoes.VolumeAmbiente, v => { Opcoes.VolumeAmbiente = v; SomManager.AplicarVolumes(); })));
            conteudo.Add(som);

            // ---- imagem
            conteudo.Add(L("Imagem", "h2"));
            var img = E("cartao");
            var tam = E("opc-chips-g");
            for (int i = 0; i < Opcoes.NomesTamanho.Length; i++)
            {
                int ii = i;
                tam.Add(BotaoMini(Opcoes.NomesTamanho[i], () => { Opcoes.Tamanho = ii; AplicarTamanhoInterface(); AtualizarOpcoes(); }, Opcoes.Tamanho == i));
            }
            img.Add(LinhaOpcao("Tamanho da interface", "Aumenta ou diminui textos e botões de uma vez. Telas pequenas pedem a pequena; a grande ajuda a ler de longe.", tam));

            var modo = E("opc-chips-g");
            modo.Add(BotaoMini("Janela", () => { Opcoes.TelaCheia = false; Opcoes.AplicarTela(true); AtualizarOpcoes(); }, !Opcoes.TelaCheia));
            modo.Add(BotaoMini("Tela cheia", () => { Opcoes.TelaCheia = true; Opcoes.AplicarTela(true); AtualizarOpcoes(); }, Opcoes.TelaCheia));
            img.Add(LinhaOpcao("Modo de tela", null, modo));

            if (!Opcoes.TelaCheia)
            {
                var jan = E("opc-chips-g");
                for (int i = 0; i < Opcoes.Janelas.Length; i++)
                {
                    var j = Opcoes.Janelas[i]; if (!Opcoes.Cabe(j)) continue;
                    int ii = i;
                    jan.Add(BotaoMini(j.x + "×" + j.y, () => { Opcoes.Janela = ii; Opcoes.AplicarTela(true); AtualizarOpcoes(); }, Opcoes.Janela == i));
                }
                img.Add(LinhaOpcao("Tamanho da janela", "Só os tamanhos que cabem no seu monitor.", jan));
            }
            conteudo.Add(img);

            // ---- partida
            conteudo.Add(L("Partida", "h2"));
            var par = E("cartao");
            var hud = E("opc-chips-g");
            hud.Add(BotaoMini("Novo", () => { Opcoes.HudNovo = true; AtualizarOpcoes(); }, Opcoes.HudNovo));
            hud.Add(BotaoMini("Clássico", () => { Opcoes.HudNovo = false; AtualizarOpcoes(); }, !Opcoes.HudNovo));
            par.Add(LinhaOpcao("Visual do HUD da partida", "O Novo segue o visual do manager (grafite, cantos retos). O Clássico é o de antes, em tons quentes. Vale na próxima partida.", hud));
            conteudo.Add(par);

            // ---- geral
            conteudo.Add(L("Geral", "h2"));
            var ger = E("cartao");
            ger.Add(LinhaOpcao("Primeiros passos", "O tour guiado das telas do manager.",
                BotaoMini("Rever o tutorial", () => { c.tutorial = 0; Salvar(); Ir(AlvoTutorial(0)); })));
            ger.Add(LinhaOpcao("Padrões", "Volta som, tamanho da interface e janela ao que vem de fábrica.",
                BotaoMini("Restaurar", () => { Opcoes.RestaurarPadroes(); SomManager.Mudo = false; AplicarTamanhoInterface(); Opcoes.AplicarTela(true); SomManager.AplicarVolumes(); AtualizarOpcoes(); Toast("Opções restauradas."); })));
            conteudo.Add(ger);
        }
    }
}
