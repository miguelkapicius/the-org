using System.Linq;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Pós-partida (decisão 0033): o resultado numa faixa, o MVP e a jogada em destaque, e a tabela de desempenho inteira na largura da tela.</summary>
    public partial class GerenteUI
    {
        void TelaPosPartida()
        {
            var r = resumoAberto ?? c.naoVisto;
            if (r == null) { aba = Aba.Painel; Mostrar(); return; }
            string cor = r.venceu ? "verde" : "vermelho";

            // ---- a faixa do resultado: o que aconteceu, o placar e onde a série está
            var hero = E("hero", r.venceu ? "venceu" : "perdeu");
            var esq = E("hero-lado");
            esq.Add(L(r.venceu ? "VITÓRIA NO MAPA" : "DERROTA NO MAPA", "hero-res", cor));
            esq.Add(L(c.clube + " contra " + r.adversario, "f20", "negrito"));
            esq.Add(L(r.campeonato + " · " + r.mapa + " · " + (r.assistida ? "assistida" : "pulada"), "f15", "suave"));
            hero.Add(esq);
            var meio = E("hero-meio"); meio.Add(L(r.meus + "  x  " + r.deles, "hero-placar")); hero.Add(meio);
            var dir = E("hero-lado", "hero-dir");
            dir.Add(L("Série " + Fmt.Serie(r.serieMd) + ": " + r.PlacarSerie, "f18", "negrito", r.serieEncerrada ? (r.venceuSerie ? "verde" : "vermelho") : "t"));
            dir.Add(r.serieEncerrada
                ? L(r.venceuSerie ? "Você venceu a série" : "Você perdeu a série", "f15", r.venceuSerie ? "verde" : "vermelho")
                : L("A série continua no próximo mapa", "f15", "suave"));
            dir.Add(L((r.fas >= 0 ? "+" : "−") + Fmt.Fas(System.Math.Abs(r.fas)) + " fãs", "f15", r.fas >= 0 ? "verde" : "vermelho"));
            if (r.mapasDaSerie.Count > 1)
            {
                var mp = E("pp-mapas");
                foreach (var m in r.mapasDaSerie) mp.Add(Selo(m.mapa + " " + m.meus + " x " + m.deles, m.meus > m.deles ? "verde" : "vermelho"));
                dir.Add(mp);
            }
            var fc = L("Força " + r.forcaMinha + " contra " + r.forcaDeles, "f14", "suave"); fc.style.marginTop = 6; dir.Add(fc);
            hero.Add(dir);
            conteudo.Add(hero);

            if (r.serieEncerrada)
            {
                var fim = c.titulos.FirstOrDefault(t => t.nome == r.campeonato);
                if (fim != null) { var fl = L("Campeonato encerrado: " + Fmt.Ordinal(fim.posicao) + " lugar, prêmio " + Fmt.Dinheiro(fim.premio) + ".", "f17", "negrito", "verde"); fl.style.marginBottom = 14; conteudo.Add(fl); }
            }

            // ---- os destaques (decisão 0020): o MVP do mapa e a jogada da partida
            var melhor = r.Melhor;
            var dest = E("cols2");
            if (melhor != null)
            {
                var cm = E("cartao", "pp-dest");
                cm.Add(L("MVP DO MAPA", "cartao-tit"));
                var ln = E("linha-h"); ln.Add(Avatar(melhor.nome));
                var tx = E("pessoa-txt"); tx.Add(L(melhor.nome, "f20", "negrito"));
                tx.Add(L(Fmt.Funcao(melhor.funcao) + " · " + melhor.k + " abates · K/D " + melhor.KD.ToString("0.00") + " · " + melhor.DanoPorRound + " de dano por round", "f15", "suave", "quebra"));
                ln.Add(tx); cm.Add(ln);
                if (r.mvpSerie != null) { var ms = L("MVP da série: " + r.mvpSerie, "f15", "negrito", "verde"); ms.style.marginTop = 8; cm.Add(ms); }
                dest.Add(cm);
            }
            if (r.assistida || r.jogada != null)   // mapa pulado não tem jogada: o cartão some
            {
                var cj = E("cartao", "pp-dest");
                cj.Add(L("JOGADA DA PARTIDA", "cartao-tit"));
                if (r.jogada != null)
                {
                    cj.Add(Selo(r.jogadaNossa ? "DO SEU TIME" : "DO ADVERSÁRIO", r.jogadaNossa ? "verde" : "vermelho"));
                    var tj = L(r.jogada, "f17", "negrito", "quebra"); tj.style.marginTop = 8; cj.Add(tj);
                }
                else cj.Add(L("Nenhum abate marcante nesta partida.", "f15", "suave"));
                dest.Add(cj);
            }
            if (dest.childCount == 1) dest.Add(E());   // sem jogada (mapa pulado): o MVP fica na metade esquerda
            if (dest.childCount > 0) conteudo.Add(dest);

            // ---- o desempenho de cada jogador, na largura toda
            var cab = E("linha-h"); cab.Add(L("Desempenho dos jogadores", "h2"));
            conteudo.Add(cab);
            if (r.estimado) conteudo.Add(L("Números estimados: o mapa foi pulado.", "nota-estimada"));
            var tab = E("pp");
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")).WithClass("largo"));
            foreach (var (n, dica) in new[] { ("Abates", ""), ("Mortes", ""), ("K/D", "Abates por morte."), ("HS %", "Parte dos abates feita com tiro na cabeça."), ("Dano/round", "Dano médio causado por round."), ("Aberturas", "Primeiros abates do round (o jogador que abre a troca)."), ("Nota", "Nota simples de desempenho (1,0 é um jogo normal): decide o MVP do mapa.") })
            {
                var cel = Celula("c-n", L(n)); cel.AddToClassList("cab"); if (dica != "") { cel.pickingMode = PickingMode.Position; ComDica(cel, dica); } h.Add(cel);
            }
            tab.Add(h);
            foreach (var d in r.desempenho)
            {
                var linha = E("tr"); if (d == melhor) linha.AddToClassList("mvp");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(d.nome));
                var nomes = E("pessoa-txt");
                nomes.Add(L(d.nome, "f17", "negrito"));
                nomes.Add(L(Fmt.Funcao(d.funcao), "f14", "suave"));
                pessoa.Add(nomes);
                if (d == melhor) { var s = Selo("MVP", "verde"); s.style.marginLeft = 12; ComDica(s, "MVP do mapa: a melhor nota de desempenho (abates, mortes e dano por round)."); pessoa.Add(s); }
                linha.Add(Celula("c-j", pessoa).WithClass("largo"));
                linha.Add(Celula("c-n", L(d.k.ToString(), "negrito")));
                linha.Add(Celula("c-n", L(d.d.ToString())));
                linha.Add(Celula("c-n", L(d.KD.ToString("0.00"), "negrito", d.KD >= 1.2f ? "verde" : d.KD >= 0.8f ? "t" : "vermelho")));
                linha.Add(Celula("c-n", L(d.HsPct + "%")));
                linha.Add(Celula("c-n", L(d.DanoPorRound.ToString())));
                linha.Add(Celula("c-n", L(d.fk.ToString())));
                linha.Add(Celula("c-n", L(d.Nota.ToString("0.0"), d == melhor ? "verde" : "suave")));
                tab.Add(linha);
            }
            conteudo.Add(tab);

            // ---- o que fazer agora
            var botoes = E("linha-h"); botoes.style.marginTop = 22;
            bool eNovo = r == c.naoVisto;
            bool continua = eNovo && c.serie != null && !c.serie.Encerrada && !c.Falido;
            var cont = Botao(continua ? "Próximo mapa" : eNovo ? "Continuar" : "Voltar ao painel", Continuar, true, true, continua ? "play" : null);
            cont.style.marginRight = 12; botoes.Add(cont);
            if (continua) { var pularR = Botao("Pular o resto da série", () => { Continuar0(); PularSerieUI(); }); pularR.style.marginRight = 12; botoes.Add(pularR); }
            botoes.Add(Botao("Ver finanças", () => { Continuar0(); Ir(Aba.Financas); }));
            conteudo.Add(botoes);
        }
    }
}
