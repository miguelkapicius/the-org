using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Primeiros passos (decisão 0028): um tour guiado e sem bloqueio pelas telas principais. Um cartão fica no canto com o passo atual,
    /// o ícone da tela destacado no menu e os botões Próximo (que leva à tela do passo seguinte) e Pular. O progresso é salvo na carreira;
    /// o botão de ajuda do menu reinicia o tour.</summary>
    public partial class GerenteUI
    {
        struct PassoTutorial { public Aba alvo; public string titulo, texto; }

        static readonly PassoTutorial[] TUTORIAL =
        {
            new PassoTutorial { alvo = Aba.Painel, titulo = "Você é o manager",
                texto = "Você monta o time, treina, negocia e assiste às séries. Este é o painel: o cartão SÉRIE é o seu próximo compromisso." },
            new PassoTutorial { alvo = Aba.Elenco, titulo = "Escale os 5 titulares",
                texto = "Só os titulares jogam. Passe o mouse em Escalar para ver como a força e a chance de vitória mudam, antes de trocar." },
            new PassoTutorial { alvo = Aba.Treino, titulo = "Treine, mas sem exagerar",
                texto = "Escolha o foco e as sessões de cada jogador. Treino evolui, mas cansa, e cansaço derruba a habilidade na partida. Clique no nome de um jogador para ver o plano dele, inclusive o TR e o CT." },
            new PassoTutorial { alvo = Aba.Mercado, titulo = "Contrate negociando",
                texto = "Ninguém tem preço fixo. Clique em Negociar: o jogador decide por prestígio do clube, vaga, estrutura, finanças e clima, e você faz propostas de luvas e salário." },
            new PassoTutorial { alvo = Aba.Financas, titulo = "Cuide do caixa",
                texto = "Salários e manutenção saem toda semana. Se o caixa acabar, o jogo termina. Dá para pegar empréstimo, mas ele cobra juros." },
            new PassoTutorial { alvo = Aba.Serie, titulo = "Jogue a série",
                texto = "Vete os mapas, escolha a estratégia e depois assista ou pule. O tempo só passa quando você avança a semana." },
        };

        bool TutorialAtivo => c.tutorial >= 0 && c.tutorial < Carreira.TUTORIAL_PASSOS;

        /// <summary>A tela do passo. A série só existe quando há jogo marcado: sem ela o passo aponta para o Calendário.</summary>
        Aba AlvoTutorial(int passo) { var a = TUTORIAL[passo].alvo; return a == Aba.Serie && !c.TemJogoAgora ? Aba.Calendario : a; }

        static string NomeAba(Aba a) => a switch
        {
            Aba.Painel => "o Painel", Aba.Elenco => "o Elenco", Aba.Treino => "o Treino", Aba.Mercado => "o Mercado", Aba.Financas => "as Finanças",
            Aba.Calendario => "o Calendário", Aba.Serie => "a Série", _ => "a tela",
        };

        void AvancarTutorial()
        {
            c.tutorial++;
            Salvar();
            if (!TutorialAtivo) { Ir(Aba.Painel); proximoSomToast = Som.Sucesso; Toast("Tutorial concluído. O botão de ajuda do menu mostra de novo."); return; }
            Ir(AlvoTutorial(c.tutorial));
        }

        VisualElement CartaoTutorial()
        {
            if (!TutorialAtivo) return null;
            int n = Carreira.TUTORIAL_PASSOS, i = c.tutorial; var p = TUTORIAL[i]; var alvo = AlvoTutorial(i);
            var cd = E("tut");
            var topo = E("tut-topo");
            topo.Add(L("PRIMEIROS PASSOS", "dado-rot"));
            var pips = E("tut-pips");
            for (int k = 0; k < n; k++) { var pp = E("tut-pip"); if (k <= i) pp.AddToClassList("on"); pips.Add(pp); }
            topo.Add(pips);
            cd.Add(topo);
            cd.Add(L(p.titulo, "tut-titulo"));
            string texto = p.alvo == Aba.Serie && !c.TemJogoAgora ? "Não há série marcada esta semana. Veja o Calendário e avance a semana quando estiver pronto; a série aparece no painel quando chegar a hora." : p.texto;
            cd.Add(L(texto, "tut-texto", "quebra"));
            var bt = E("tut-botoes");
            var pular = BotaoMini("Pular tutorial", () => { c.tutorial = Carreira.TUTORIAL_PASSOS; Salvar(); Mostrar(); Toast("Tutorial pulado. O botão de ajuda do menu mostra de novo."); });
            bt.Add(pular);
            var fl = E(); fl.style.flexGrow = 1; bt.Add(fl);
            if (aba != alvo) { var ir = BotaoMini("Ir para " + NomeAba(alvo).Substring(2), () => Ir(alvo)); ir.style.marginRight = 8; bt.Add(ir); }
            bt.Add(Botao(i == n - 1 ? "Concluir" : "Próximo", AvancarTutorial, true, true));
            cd.Add(bt);
            return cd;
        }
    }
}
