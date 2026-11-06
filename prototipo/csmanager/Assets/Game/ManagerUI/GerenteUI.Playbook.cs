using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using CSM.Sim;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Playbook (decisão 0022): as jogadas do banco de planos, por lado, com favoritas (o IGL prefere) e proibidas (o IGL
    /// não escolhe). As vagas crescem com o nível da Gaming House.</summary>
    public partial class GerenteUI
    {
        string jogadaAberta; int exemploAberto;   // a jogada com o desenho aberto e qual dos planos dela está desenhado

        void TelaPlaybook()
        {
            conteudo.Add(L("O IGL escolhe a cada round uma jogada de um banco de rounds de times profissionais, preferindo o estilo da estratégia. Aqui você marca as jogadas que ele deve preferir (favoritas, peso " +
                           Estrategias.PREFERENCIA_PLAYBOOK.ToString("0") + "x) e as que ele nunca deve escolher (proibidas). Vale para o mapa jogável de hoje; cada mapa novo terá o seu playbook.", "f15", "suave", "quebra"));
            SecaoPlaybook(true);
            SecaoPlaybook(false);
            conteudo.Add(L("Vitória pro = quanto o time profissional venceu os rounds daquela jogada. Força = o quanto a jogada rende na nossa simulação (1,0 = média). " +
                           "Vagas: " + c.VagasPlaybook + " favorita(s) e " + c.VagasPlaybook + " proibida(s) por lado no nível " + c.nivelGH + " da Gaming House (+1 por nível, até " + Carreira.VAGAS_PLAYBOOK_MAX + ").", "legenda"));
        }

        void SecaoPlaybook(bool tr)
        {
            var fav = tr ? c.favoritasTR : c.favoritasCT; var proi = tr ? c.proibidasTR : c.proibidasCT;
            var topo = E("linha-h"); topo.style.alignItems = Align.Center; topo.style.marginTop = 18;
            topo.Add(L(tr ? "Ataque (TR)" : "Defesa (CT)", "h2"));
            var vagas = L("   favoritas " + fav.Count + "/" + c.VagasPlaybook + " · proibidas " + proi.Count + "/" + c.VagasPlaybook, "f15", "suave"); topo.Add(vagas);
            conteudo.Add(topo);

            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogada")).WithClass("largo"));
            foreach (var n in new[] { "Planos", "Vitória pro", "Força", "Estilo" }) { var cel = Celula("c-n", L(n)); cel.AddToClassList("cab"); h.Add(cel); }
            h.Add(Celula("c-j", L("No playbook")));
            tab.Add(h);
            foreach (var j in Estrategias.Jogadas(tr))
            {
                var jj = j; var marca = c.Marca(j.nome, tr);
                var r = E("tr");
                bool aberta = jogadaAberta == j.nome;
                var nome = E("linha-h"); nome.style.alignItems = Align.Center;
                nome.Add(new Icone(aberta ? "down" : "chev", 14));
                var ln = L(j.nome, "f16", "negrito"); ln.style.marginLeft = 6; nome.Add(ln);
                Clicavel(nome, () => { jogadaAberta = aberta ? null : jj.nome; exemploAberto = 0; Mostrar(); });
                ComDica(nome, "Clique para ver a jogada desenhada no mapa e os pontos fortes e fracos.");
                if (marca == Carreira.MarcaJogada.Favorita) { var s = Selo("FAVORITA", "verde"); s.style.marginLeft = 8; nome.Add(s); }
                else if (marca == Carreira.MarcaJogada.Proibida) { var s = Selo("PROIBIDA", "vermelho"); s.style.marginLeft = 8; nome.Add(s); }
                r.Add(Celula("c-j", nome).WithClass("largo"));
                r.Add(Celula("c-n", L(j.planos.ToString())));
                r.Add(Celula("c-n", L(Mathf.RoundToInt(j.vitoriaPro * 100) + "%", j.vitoriaPro >= 0.55f ? "verde" : j.vitoriaPro < 0.45f ? "vermelho" : "t")));
                r.Add(Celula("c-n", L(j.forca.ToString("0.00"), j.forca >= 1.05f ? "verde" : j.forca < 0.95f ? "vermelho" : "t")));
                r.Add(Celula("c-n", L(NomeCategoria(j.estilo), "f14", "suave")));
                var acoes = E("linha-h");
                acoes.Add(BotaoMini("Favorita", () => Marcar(jj.nome, tr, marca == Carreira.MarcaJogada.Favorita ? Carreira.MarcaJogada.Nenhuma : Carreira.MarcaJogada.Favorita), marca == Carreira.MarcaJogada.Favorita));
                acoes.Add(BotaoMini("Proibir", () => Marcar(jj.nome, tr, marca == Carreira.MarcaJogada.Proibida ? Carreira.MarcaJogada.Nenhuma : Carreira.MarcaJogada.Proibida), marca == Carreira.MarcaJogada.Proibida));
                r.Add(Celula("c-j", acoes));
                tab.Add(r);
                if (aberta) tab.Add(DetalheJogada(j));
            }
            conteudo.Add(tab);
        }

        /// <summary>A jogada desenhada no mapa (um dos planos dela, do mais típico para o menos) e os pontos fortes e fracos.</summary>
        VisualElement DetalheJogada(Estrategias.InfoJogada j)
        {
            var plano = j.exemplos[exemploAberto % j.exemplos.Count];
            var det = E("cartao"); det.style.flexDirection = FlexDirection.Row; det.style.marginTop = 8; det.style.marginBottom = 18; det.style.alignItems = Align.FlexStart;
            det.Add(new PlantaJogada(plano));
            var lado = E(); lado.style.marginLeft = 22; lado.style.flexGrow = 1; lado.style.flexShrink = 1;
            lado.Add(L(j.nome, "f20", "negrito"));
            lado.Add(L("Exemplo " + (exemploAberto % j.exemplos.Count + 1) + " de " + j.exemplos.Count + ": um round de time profissional (" + (plano.ganhou ? "venceu" : "perdeu") + "). " +
                       (j.tr ? "Rotas da base até o plant." : "Onde cada um vai nos primeiros 30 s."), "f14", "suave", "quebra"));
            var legenda = E(); legenda.style.marginTop = 10;
            for (int k = 0; k < 5; k++)
            {
                var lin = E(); lin.style.flexDirection = FlexDirection.Row; lin.style.alignItems = Align.FlexStart; lin.style.marginTop = 4;
                var bola = E(); bola.style.width = 12; bola.style.height = 12; bola.style.marginTop = 3; bola.style.marginRight = 8; bola.style.flexShrink = 0;
                bola.style.borderTopLeftRadius = bola.style.borderTopRightRadius = bola.style.borderBottomLeftRadius = bola.style.borderBottomRightRadius = 6;
                bola.style.backgroundColor = PlantaJogada.CORES[k];
                lin.Add(bola);
                var t = L(Estrategias.ResumoRota(plano, k), "f14", "quebra"); t.style.flexShrink = 1; lin.Add(t);
                legenda.Add(lin);
            }
            lado.Add(legenda);
            void Lista(string titulo, List<string> itens, string cor)
            {
                var h = L(titulo, "f16", "negrito", cor); h.style.marginTop = 14; lado.Add(h);
                if (itens.Count == 0) { lado.Add(L("Nada que se destaque nos dados.", "f14", "suave")); return; }
                foreach (var it in itens) { var l = L("• " + it, "f14", "quebra"); l.style.marginTop = 3; lado.Add(l); }
            }
            Lista("Pontos fortes", j.fortes, "verde");
            Lista("Pontos fracos", j.fracos, "vermelho");
            var bt = E("linha-h"); bt.style.marginTop = 14;
            if (j.exemplos.Count > 1) { var b1 = BotaoMini("Outro exemplo", () => { exemploAberto++; Mostrar(); }); b1.style.marginRight = 8; bt.Add(b1); }
            bt.Add(BotaoMini("Fechar", () => { jogadaAberta = null; Mostrar(); }));
            lado.Add(bt);
            det.Add(lado);
            return det;
        }

        void Marcar(string jogada, bool tr, Carreira.MarcaJogada m)
        {
            var erro = c.MarcarJogada(jogada, tr, m);
            Salvar(); Mostrar();
            if (erro != null) Toast(erro, true);
        }

        static string NomeCategoria(Estrategias.Categoria k) => k switch
        {
            Estrategias.Categoria.Agressiva => "Agressiva", Estrategias.Categoria.Defensiva => "Defensiva",
            Estrategias.Categoria.ExecucoesRapidas => "Exec. rápidas", Estrategias.Categoria.ControleDeMapa => "Controle", _ => "Equilibrada",
        };
    }
}
