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
    /// <summary>Tela de treino de UM jogador (decisão 0024): foto grande do lado em que ele rende mais, os atributos em cada lado
    /// (TR e CT) e o plano da semana (foco, sessões e lado).</summary>
    public partial class GerenteUI
    {
        Jogador treinoJogador;   // null = a lista do time

        void TelaTreinoJogador(Jogador j)
        {
            // trocar de jogador sem voltar para a lista
            var nav = E("tj-nav");
            nav.Add(BotaoMini("Todos", () => { treinoJogador = null; Mostrar(); }, false, true, "users"));
            foreach (var o in c.elenco) { var oo = o; nav.Add(BotaoMini(o.nome, () => { treinoJogador = oo; Mostrar(); }, o == j)); }
            conteudo.Add(nav);

            bool tr = FotoTR(j);
            var hero = E("tj-hero");

            // foto à direita, com o véu escuro do lado do texto (a UI Toolkit não tem gradiente)
            var foto = E("tj-foto"); foto.pickingMode = PickingMode.Ignore;
            var tex = FotoLado(tr);
            if (tex != null) { foto.style.backgroundImage = new StyleBackground(tex); AlinharAoTopo(foto); }
            var veu = E("tj-veu"); veu.pickingMode = PickingMode.Ignore;
            veu.style.backgroundImage = new StyleBackground(VeuLateral()); veu.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            foto.Add(veu);
            var ladoFoto = Selo(tr ? "ATAQUE · TR" : "DEFESA · CT", tr ? "laranja" : "azul"); ladoFoto.AddToClassList("tj-lado-foto");
            ComDica(ladoFoto, "A foto mostra o lado em que " + j.nome + " rende mais" + (j.LadoForte == null ? " (aqui ele rende igual nos dois, e o lado da foto é só um dos dois)." : "."));
            foto.Add(ladoFoto);
            hero.Add(foto);

            // coluna da esquerda: nome grande e os números principais
            var info = E("tj-info");
            info.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "tj-pre"));
            info.Add(L(j.nome.ToUpperInvariant(), "tj-nome"));
            info.Add(E("tj-sub"));

            var nums = E("tj-nums");
            nums.Add(TjNumero("Geral", j.Overall.ToString(), ClasseNota(j.Overall), COLS[0].dica));
            int cs = (int)j.cansaco;
            nums.Add(TjNumero("Cansaço", cs.ToString(), cs >= 60 ? "vermelho" : cs >= 35 ? "laranja" : "verde", "0 a 100. Acima de 35 reduz a habilidade na partida."));
            nums.Add(TjNumero("Preparo", ((int)j.preparo).ToString(), "verde", "Bônus temporário na partida, que vem do treino."));
            info.Add(nums);
            info.Add(E("tj-sub"));

            var forte = j.LadoForte;
            TjDado(info, "Lado forte", forte == null ? "Equilibrado" : forte.Value ? "TR (ataque)" : "CT (defesa)");
            TjDado(info, "Potencial", j.potencial.ToString());
            TjDado(info, "Salário", Fmt.Dinheiro(j.Salario) + "/sem");
            TjDado(info, "Traço", j.traco != Traco.Nenhum ? Tracos.Nome(j.traco) : "Nenhum");
            hero.Add(info);

            // faixa de baixo (como a tabela de estatísticas do jogador): atributos em cada lado e evolução
            var faixa = E("tj-faixa");
            var cab = E("tj-linha-tab", "cab");
            cab.Add(Celula("tj-c-rot", null));
            for (int a = 0; a < 5; a++) cab.Add(Celula("tj-c-num", L(a == 3 ? "Utilitária" : Jogador.NomeAtrib(a))));
            cab.Add(Celula("tj-c-num", L("Geral")));
            faixa.Add(cab);
            faixa.Add(TjLinhaLado(j, true));
            faixa.Add(TjLinhaLado(j, false));
            var evo = E("tj-linha-tab");
            var rotEvo = L("Evolução na temporada", "f14", "suave"); evo.Add(Celula("tj-c-rot", rotEvo));
            float soma = 0;
            for (int a = 0; a < 5; a++)
            {
                float d = j.Valor(a) - j.inicioTemporada[a]; soma += d;
                evo.Add(Celula("tj-c-num", L(Sinal(d), "f17", "negrito", d > 0.05f ? "verde" : d < -0.05f ? "vermelho" : "suave")));
            }
            evo.Add(Celula("tj-c-num", L(Sinal(soma) + " pts", "f17", "negrito", soma > 0.05f ? "verde" : soma < -0.05f ? "vermelho" : "suave")));
            ComDica(evo, "Quanto cada atributo subiu desde o começo da temporada.");
            faixa.Add(evo);
            hero.Add(faixa);
            conteudo.Add(hero);

            // o que a semana vai render (a conclusão primeiro)
            var (ganho, preparoDepois, cansacoDepois) = c.PreverSemana(j);
            float gl = c.PreverLado(j);
            var partes = new List<string>();
            if (j.foco >= 0 && j.sessoes > 0) partes.Add(ganho > 0.005f ? Jogador.NomeAtrib(j.foco) + " " + (ganho >= 0.1f ? Sinal(ganho) : "+" + ganho.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"))) : Jogador.NomeAtrib(j.foco) + " sem ganho");
            if (j.ladoFoco >= 0 && j.sessoes > 0) partes.Add("domínio do " + (j.ladoFoco == 0 ? "TR" : "CT") + " " + Sinal(gl));
            if (partes.Count == 0) partes.Add(j.sessoes == 0 ? "Sem treino: o cansaço cai e o preparo também" : "Treino sem foco");
            bool exausto = cansacoDepois >= 60, cansado = cansacoDepois >= 35 && j.cansaco < 35;
            string aviso = exausto ? " · vai ficar exausto" : cansado ? " · vai ficar cansado" : "";
            var ef = E("efeito-time", exausto ? "ruim" : cansado ? "neutro" : "bom");
            ef.Add(L("Esta semana: " + string.Join(" · ", partes) + aviso, "quebra"));
            ComDica(ef, "Cansaço " + cs + " → " + (int)cansacoDepois + " · preparo " + (int)j.preparo + " → " + (int)preparoDepois + " no fim da semana.");
            ef.style.marginTop = 16;
            conteudo.Add(ef);

            // plano da semana
            var plano = E("treino-card", "tj-plano");
            plano.Add(L("PLANO DA SEMANA", "treino-card-tit"));

            var rFoco = TjLinhaPlano("Foco", "O que ele treina: sobe um atributo por semana, até o potencial.");
            var chips = E("tarefas"); chips.style.flexGrow = 1;
            chips.Add(BotaoMini("Descanso", () => DefinirTreinoUI(j, -1, 0), j.foco < 0));
            for (int a = 0; a < 5; a++) { int aa = a; chips.Add(BotaoMini(a == 3 ? "Utilitária" : Jogador.NomeAtrib(a), () => DefinirTreinoUI(j, aa, Math.Max(1, j.sessoes)), j.foco == a)); }
            rFoco.Add(chips); plano.Add(rFoco);

            var rSess = TjLinhaPlano("Sessões", "Até " + c.MaxSessoes + " por semana na sua Gaming House (" + c.GH.nome + "). Mais sessões evoluem mais e cansam mais.");
            var st = E("stepper");
            st.Add(BotaoIcone("minus", () => DefinirTreinoUI(j, j.foco, j.sessoes - 1), j.sessoes > 0));
            st.Add(L(j.sessoes.ToString()));
            st.Add(BotaoIcone("plus", () => DefinirTreinoUI(j, j.foco >= 0 ? j.foco : MaisFraco(j), j.sessoes + 1), j.sessoes < c.MaxSessoes));
            rSess.Add(st); plano.Add(rSess);

            var rLado = TjLinhaPlano("Lado", "Treinar um lado sobe o domínio dele (o jogador rende mais nesse lado). Nessas sessões o atributo rende " + (int)(Provisorio.LADO_FOCO_ATRIB * 100) + "%. O lado que ninguém treina esfria devagar para a média.");
            var lc = E("tarefas"); lc.style.flexGrow = 1;
            var opcoes = new[] { ("Os dois", -1), ("TR", 0), ("CT", 1) };
            foreach (var (nome, v) in opcoes)
            {
                int vv = v;
                lc.Add(BotaoMini(nome, () => { var er = c.DefinirLadoTreino(j, vv); if (er != null) { Toast(er, true); return; } Salvar(); Mostrar(); }, j.ladoFoco == v, j.sessoes > 0));
            }
            rLado.Add(lc); plano.Add(rLado);
            conteudo.Add(plano);
        }

        VisualElement TjNumero(string rotulo, string valor, string cor, string dica)
        {
            var e = E("tj-num");
            e.Add(L(rotulo, "f13", "apagado"));
            e.Add(L(valor, "tj-num-v", cor));
            e.pickingMode = PickingMode.Position; ComDica(e, dica);
            return e;
        }

        void TjDado(VisualElement pai, string rotulo, string valor)
        {
            var l = E("tj-dado");
            var r = L(rotulo + ":", "f14", "apagado"); r.AddToClassList("tj-dado-rot");
            l.Add(r); l.Add(L(valor, "f15", "negrito"));
            pai.Add(l);
        }

        VisualElement TjLinhaPlano(string rotulo, string dica)
        {
            var l = E("tj-plano-linha");
            var r = L(rotulo, "f15", "suave"); r.AddToClassList("tj-plano-rot"); r.pickingMode = PickingMode.Position; ComDica(r, dica);
            l.Add(r);
            return l;
        }

        /// <summary>Linha da tabela de baixo: os cinco atributos e o geral do jogador num lado (TR ou CT).</summary>
        VisualElement TjLinhaLado(Jogador j, bool tr)
        {
            var l = E("tj-linha-tab");
            var rot = E("tj-rot-lado");
            rot.Add(L(tr ? "No TR" : "No CT", "f17", "negrito", tr ? "laranja" : "azul"));
            rot.Add(L("domínio " + (int)j.Lado(tr), "f13", "apagado"));
            var cel = Celula("tj-c-rot", rot); l.Add(cel);
            for (int a = 0; a < 5; a++)
            {
                int v = (int)Math.Round(j.ValorNoLado(a, tr));
                var t = L(v.ToString(), "tj-valor", ClasseNota(v)); l.Add(Celula("tj-c-num", t));
            }
            int g = j.OverallNoLado(tr);
            l.Add(Celula("tj-c-num", L(g.ToString(), "tj-valor", "negrito", ClasseNota(g))));
            float b = j.BonusLado(tr) * 100f;
            ComDica(l, (tr ? "Atacando (TR)" : "Defendendo (CT)") + ": " + (Math.Abs(b) < 0.05f ? "rende o mesmo que a média dos dois lados" : "rende " + Sinal(b) + "% " + (b > 0 ? "acima" : "abaixo") + " da média dos dois lados") + ".\nDomínio " + (int)j.Lado(tr) + " de 100 (50 é neutro).");
            return l;
        }
    }
}
