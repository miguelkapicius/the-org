using System;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>A folha do contrato (decisão 0042): o contrato do jogador mostrado como um documento de verdade, em papel, com as cláusulas
    /// em linguagem simples (prazo, salário, vaga, cláusula de rescisão, multa, fim) e as assinaturas. Aparece como minuta na negociação, como
    /// "assinado" logo depois de fechar e como "em vigor" a partir do cartão do elenco (com renovar e rescindir).</summary>
    public partial class GerenteUI
    {
        enum EstadoFolha { Minuta, Assinado, EmVigor }

        /// <summary>Os números que entram na folha: o que o jogador tem agora ou o que está sendo proposto.</summary>
        struct DadosFolha
        {
            public Jogador j; public EstadoFolha estado;
            public int semanas;            // duração a contar de hoje
            public int salario, luvas;     // luvas = -1 quando não é conhecida (contrato antigo)
            public int clausula, multa;
            public bool vaga;              // vaga de titular garantida na proposta
        }

        VisualElement contratoEl;

        void FecharContrato() { contratoEl?.RemoveFromHierarchy(); contratoEl = null; }

        /// <summary>Abre o papel por cima da tela, com os botões abaixo dele. <paramref name="acoes"/> são botões extras (além de Fechar).</summary>
        void AbrirContrato(DadosFolha d, params VisualElement[] acoes)
        {
            FecharContrato();
            var cena = E("contrato-cena"); cena.pickingMode = PickingMode.Position;
            var rolo = new ScrollView(ScrollViewMode.Vertical); rolo.AddToClassList("contrato-rolo"); rolo.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
            rolo.Add(FolhaContrato(d));
            cena.Add(rolo);
            var barra = E("contrato-barra");
            foreach (var a in acoes) if (a != null) { a.style.marginRight = 10; barra.Add(a); }
            barra.Add(Botao("Fechar", FecharContrato, true, false, "check"));
            cena.Add(barra);
            tela.Add(cena); contratoEl = cena;
            cena.schedule.Execute(() => cena.AddToClassList("visivel")).StartingIn(1);
        }

        /// <summary>Contrato do jogador do elenco, em vigor.</summary>
        void AbrirContratoDoElenco(Jogador j)
        {
            var d = new DadosFolha { j = j, estado = EstadoFolha.EmVigor, semanas = j.contratoSemanas, salario = j.Salario, luvas = -1, clausula = j.clausula, multa = Contratos.Multa(j) };
            var jj = j; var pr = c.PedirRenovacao(j);
            var renovar = Botao("Renovar", () => { FecharContrato(); RenovarJogador(jj); }, pr.aceita, true, "check");
            ComDica(renovar, pr.aceita ? "Renova por " + Contratos.Prazo(pr.anos * Contratos.SEMANAS_ANO) + ": " + Fmt.Dinheiro(pr.luvas) + " de luvas e " + Fmt.Dinheiro(pr.salario) + " por semana."
                : j.nome + " não quer renovar agora: " + pr.motivo);
            bool pode = c.elenco.Count > Provisorio.TITULARES;
            var rescindir = Botao("Rescindir", () => { FecharContrato(); Dispensar(jj); }, pode, false, "minus", "perigo");
            ComDica(rescindir, pode ? "Paga a multa de " + Fmt.Dinheiro(d.multa) + " e " + j.nome + " sai do clube. Dá para desfazer logo depois." : "É preciso manter pelo menos " + Provisorio.TITULARES + " jogadores.");
            if (j.querSair > 0)
            {
                var seg = Botao("Pagar bônus", () => { FecharContrato(); SegurarJogador(jj); }, true, true, "check");
                ComDica(seg, "Paga " + Fmt.Dinheiro(j.Salario * Contratos.SEMANAS_PERMANENCIA) + " de bônus de permanência: ele fica e a moral sobe.");
                AbrirContrato(d, seg, renovar, rescindir);
            }
            else AbrirContrato(d, renovar, rescindir);
        }

        // ------------------------------------------------------------------------------------------ o papel
        VisualElement FolhaContrato(DadosFolha d)
        {
            var j = d.j;
            var f = E("folha");

            // selo carimbado no canto
            string carimbo = d.estado == EstadoFolha.Minuta ? "MINUTA" : d.estado == EstadoFolha.Assinado ? "ASSINADO" : "EM VIGOR";
            var sel = E("folha-carimbo", d.estado == EstadoFolha.Minuta ? "minuta" : "assinado"); sel.Add(L(carimbo, "folha-carimbo-t")); f.Add(sel);

            f.Add(L("CONTRATO DE ATLETA", "folha-tit"));
            f.Add(L("Prestação de serviços esportivos", "folha-sub"));
            f.Add(L("Nº " + c.Temporada.ToString("00") + "-" + c.Semana.ToString("00") + "-" + j.nome.Aggregate(7, (h, ch) => (h * 31 + ch) % 10000).ToString("0000"), "folha-num"));
            f.Add(E("folha-regua"));

            // as partes
            var partes = E("folha-partes");
            partes.Add(Parte("CONTRATANTE", c.clube, "Clube · circuito " + Mundo.NomeCircuito(c.CircuitoClube)));
            partes.Add(Parte("CONTRATADO", j.nome, Fmt.Funcao(j.funcao) + " · " + j.idade + " anos · geral " + j.Overall));
            f.Add(partes);

            // vigência: de hoje até a semana tal da temporada tal
            int total = (c.Semana - 1) + Mathf.Max(0, d.semanas);
            int tFim = c.Temporada + total / Contratos.SEMANAS_ANO, sFim = total % Contratos.SEMANAS_ANO + 1;
            string quando = d.estado == EstadoFolha.EmVigor ? "Restam " + Contratos.Prazo(d.semanas) + ", até a semana " + sFim + " da temporada " + tFim + "."
                : Contratos.Prazo(d.semanas) + ", da semana " + c.Semana + " da temporada " + c.Temporada + " até a semana " + sFim + " da temporada " + tFim + ".";

            f.Add(Clausula("1ª · DO OBJETO", "O CONTRATADO joga pelo CONTRATANTE como " + Fmt.Funcao(j.funcao) + ". "
                + (d.estado == EstadoFolha.EmVigor ? (c.Titular(j) ? "Hoje é titular." : "Hoje é reserva.") : d.vaga ? "O CONTRATANTE garante a vaga de titular; se ele ficar " + Negociador.PROMESSA_SEMANAS + " semanas sem jogar, a moral cai." : "Sem garantia de vaga de titular.")));
            f.Add(Clausula("2ª · DA VIGÊNCIA", quando));
            string rem = "O CONTRATANTE paga " + Fmt.Dinheiro(d.salario) + " por semana ao CONTRATADO."
                + (d.luvas >= 0 ? " Na assinatura, paga também " + Fmt.Dinheiro(d.luvas) + " de luvas, uma só vez." : "");
            f.Add(Clausula("3ª · DA REMUNERAÇÃO", rem));
            f.Add(Clausula("4ª · DA CLÁUSULA DE RESCISÃO", "Outro clube pode levar o CONTRATADO antes do fim pagando " + Fmt.Dinheiro(d.clausula) + " ao CONTRATANTE. Jogador infeliz ou parado no banco é mais cobiçado."));
            f.Add(Clausula("5ª · DA MULTA", "Se o CONTRATANTE rescindir antes do fim, paga " + Fmt.Dinheiro(d.multa) + " (metade dos salários que faltam) e os outros jogadores perdem moral. Vender o contrato costuma sair melhor."));
            f.Add(Clausula("6ª · DO FIM DO CONTRATO", "No fim, o CONTRATADO sai sem custo, a não ser que renove. Aviso " + Contratos.SEMANAS_AVISO + " semanas antes. Com o elenco no mínimo, ele fica mais um ano com 15% de aumento."));
            if (j.querSair > 0 && d.estado == EstadoFolha.EmVigor)
                f.Add(Clausula("7ª · DO PEDIDO DE SAÍDA", "O CONTRATADO pediu para sair (" + j.motivoSaida + "). Se nada for feito, vai embora de graça em " + j.querSair + " semana(s). O CONTRATANTE pode vender, ou pagar " + Fmt.Dinheiro(j.Salario * Contratos.SEMANAS_PERMANENCIA) + " de bônus de permanência.", true));

            // assinaturas
            var ass = E("folha-assinaturas");
            ass.Add(Assinatura("Diretoria · " + c.clube, d.estado != EstadoFolha.Minuta, "O MANAGER"));
            ass.Add(Assinatura(j.nome, d.estado != EstadoFolha.Minuta, "O ATLETA"));
            f.Add(ass);
            f.Add(L(d.estado == EstadoFolha.Minuta ? "Minuta: vale só depois que o jogador aceitar a proposta." : "Assinado na semana " + c.Semana + " da temporada " + c.Temporada + (d.estado == EstadoFolha.EmVigor ? " ou antes (contrato em vigor)." : "."), "folha-rodape"));
            return f;
        }

        VisualElement Parte(string papel, string nome, string detalhe)
        {
            var p = E("folha-parte");
            p.Add(L(papel, "folha-rot")); p.Add(L(nome, "folha-nome")); p.Add(L(detalhe, "folha-det", "quebra"));
            return p;
        }

        VisualElement Clausula(string titulo, string texto, bool alerta = false)
        {
            var cl = E("folha-cl"); if (alerta) cl.AddToClassList("alerta");
            cl.Add(L(titulo, "folha-cl-tit"));
            cl.Add(L(texto, "folha-cl-txt", "quebra"));
            return cl;
        }

        VisualElement Assinatura(string nome, bool assinada, string papel)
        {
            var a = E("folha-ass");
            var linha = E("folha-ass-linha");
            if (assinada) linha.Add(L(nome, "folha-ass-nome"));
            a.Add(linha);
            a.Add(L(papel, "folha-rot")); a.Add(L(assinada ? "assinado digitalmente" : "aguardando assinatura", "folha-det"));
            return a;
        }
    }
}
