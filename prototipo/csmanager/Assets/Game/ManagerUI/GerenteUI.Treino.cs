using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Tela de treino (decisão 0013): o manager escolhe foco e frequência de cada jogador e vê o contrapeso entre
    /// evolução e preparo de um lado e cansaço do outro.</summary>
    public partial class GerenteUI
    {
        static string Dec(float v) => v.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("pt-BR"));
        static string Sinal(float v) => (v >= 0 ? "+" : "") + Dec(v);

        /// <summary>O atributo mais fraco do jogador (foco automático).</summary>
        static int MaisFraco(Jogador j) { int pior = 0; for (int a = 1; a < 5; a++) if (j.Valor(a) < j.Valor(pior)) pior = a; return pior; }

        void DefinirTreinoUI(Jogador j, int foco, int sessoes)
        {
            var erro = c.DefinirTreino(j, foco, sessoes);
            if (erro != null) { Toast(erro, true); return; }
            Salvar(); Mostrar();
        }

        void TelaTreino()
        {
            if (treinoJogador != null && c.elenco.Contains(treinoJogador)) { TelaTreinoJogador(treinoJogador); return; }
            treinoJogador = null;
            // a conclusão primeiro (decisão 0023): o treino desta semana ajuda ou atrapalha o time na partida?
            var tit = c.Titulares.ToList();
            float bonus = tit.Average(j => j.preparo) / 100f * Provisorio.PREPARO_MAX_BONUS * 100f;
            float pena = tit.Average(j => Math.Max(0f, (j.cansaco - Provisorio.CANSACO_LIMIAR) / (100f - Provisorio.CANSACO_LIMIAR)) * Provisorio.CANSACO_MAX_PENALIDADE) * 100f;
            bool pesado = pena > bonus, fraco = !pesado && bonus - pena < 0.6f && bonus < 1f;
            var ef = E("efeito-time", pesado ? "ruim" : fraco ? "neutro" : "bom");
            ef.Add(L((pesado ? "O cansaço está pesando mais que o preparo: reduza as sessões." : fraco ? "Quase sem preparo: treinar um pouco ajuda." : "Bom equilíbrio: o preparo compensa o cansaço.")
                + "  Titulares na partida: " + Sinal(bonus - pena) + "%", "quebra"));
            ComDica(ef, "Preparo +" + Dec(bonus) + "%, cansaço −" + Dec(pena) + "%.\n\nO treino evolui o atributo em foco (até o potencial, mais devagar com a idade) e dá preparo, um bônus temporário nas partidas. Cada sessão e cada partida geram cansaço; acima de 35 ele reduz a habilidade. Descansar recupera.");
            conteudo.Add(ef);

            var topo = E("treino-topo");

            // sessões coletivas (entrosamento): Gaming House nível 4
            var cc = E("treino-card"); if (c.MaxColetivas == 0) cc.AddToClassList("bloq");
            cc.Add(L("SESSÕES COLETIVAS", "treino-card-tit"));
            if (c.MaxColetivas == 0) { var lk = E("linha-h"); lk.Add(new Icone("lock", 18)); lk.Add(L("  Centro de performance (nível 4)", "f15", "suave")); cc.Add(lk); cc.Add(L("Treino do time junto: dá preparo extra a todos.", "f14", "apagado", "quebra")); }
            else
            {
                var st = E("stepper");
                st.Add(BotaoIcone("minus", () => { var e = c.DefinirColetivas(c.sessoesColetivas - 1); if (e == null) { Salvar(); Mostrar(); } }, c.sessoesColetivas > 0));
                st.Add(L(c.sessoesColetivas.ToString()));
                st.Add(BotaoIcone("plus", () => { var e = c.DefinirColetivas(c.sessoesColetivas + 1); if (e != null) Toast(e, true); else { Salvar(); Mostrar(); } }, c.sessoesColetivas < c.MaxColetivas));
                cc.Add(st);
                cc.Add(L("Preparo e entrosamento para todos.", "f14", "suave", "quebra"));
                ComDica(cc, "Sessões do time junto: preparo extra e entrosamento para todo o elenco, com um pouco de cansaço.");
            }
            topo.Add(cc);

            // estudo de mapa: Gaming House nível 2
            var em = E("treino-card"); if (!c.GH.estudoMapa) em.AddToClassList("bloq");
            em.Add(L("ESTUDO DE MAPA", "treino-card-tit"));
            if (!c.GH.estudoMapa) { var lk = E("linha-h"); lk.Add(new Icone("lock", 18)); lk.Add(L("  Sala de treino (nível 2)", "f15", "suave")); em.Add(lk); }
            else
            {
                var fl = E("tarefas");
                fl.Add(BotaoMini("Nenhum", () => { c.DefinirEstudoMapa(-1); Salvar(); Mostrar(); }, c.estudoMapa < 0));
                for (int m = 0; m < Mapas.TOTAL; m++)
                {
                    int mm = m;
                    fl.Add(BotaoMini(Mapas.NOMES[m] + " " + c.EquipeClube.afinidade[m], () => { c.DefinirEstudoMapa(mm); Salvar(); Mostrar(); }, c.estudoMapa == m));
                }
                em.Add(fl);
                ComDica(em, "Sobe a afinidade do time com o mapa escolhido (+3 por semana). A afinidade entra na habilidade quando o mapa é jogado. O número ao lado do mapa é a afinidade atual.");
            }
            topo.Add(em);

            conteudo.Add(topo);

            // atalhos
            var pre = E("presets");
            var rot = L("Todo o elenco:", "f14", "suave"); rot.style.marginRight = 10; pre.Add(rot);
            var p1 = BotaoMini("Descanso", () => { foreach (var j in c.elenco) c.DefinirTreino(j, -1, 0); Salvar(); Mostrar(); }); ComDica(p1, "Ninguém treina: o cansaço cai, o preparo também."); pre.Add(p1);
            var p2 = BotaoMini("Moderado", () => { foreach (var j in c.elenco) c.DefinirTreino(j, MaisFraco(j), 2); Salvar(); Mostrar(); }); ComDica(p2, "2 sessões por semana no atributo mais fraco de cada um."); pre.Add(p2);
            var p3 = BotaoMini("Intenso", () => { foreach (var j in c.elenco) c.DefinirTreino(j, j.foco >= 0 ? j.foco : MaisFraco(j), c.MaxSessoes); Salvar(); Mostrar(); }); ComDica(p3, c.MaxSessoes + " sessões por semana (o máximo da sua Gaming House). Evolui mais e cansa mais."); pre.Add(p3);
            conteudo.Add(pre);

            // tabela
            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")));
            h.Add(Celula("c-foco", L("Foco")));
            var hl = Celula("c-lado", L("Lado")); ComDica(hl, "Treinar um lado (TR ou CT) sobe o domínio dele, e o jogador rende mais nesse lado. As sessões desse lado dão menos atributo."); h.Add(hl);
            var hs = Celula("c-sess", L("Sessões/semana")); ComDica(hs, "Até " + c.MaxSessoes + " por jogador na sua Gaming House (" + c.GH.nome + "). Recuperação x" + Dec(c.GH.recuperacao) + (c.GH.bootcamp ? ", bootcamp (+20% de ganho)." : ".")); h.Add(hs);
            var hc = Celula("c-bar", L("Cansaço")); ComDica(hc, "0 a 100. Acima de 35 reduz a habilidade na partida."); h.Add(hc);
            var hp = Celula("c-bar", L("Preparo")); ComDica(hp, "Bônus temporário na partida, que vem do treino."); h.Add(hp);
            h.Add(Celula("c-proj", L("Nesta semana")));
            var ht = Celula("c-bar", L("Na temporada")); ComDica(ht, "Quanto os 5 atributos somados subiram desde o começo da temporada."); h.Add(ht);
            tab.Add(h);
            foreach (var j in c.elenco)
            {
                var r = E("tr");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(j.nome));
                var nomes = E("pessoa-txt");
                nomes.Add(L(j.nome, "f17", "negrito"));
                nomes.Add(L(Fmt.Funcao(j.funcao), "f14", "suave"));
                pessoa.Add(nomes);
                var jx = j;
                Clicavel(pessoa, () => { treinoJogador = jx; Mostrar(); }); pessoa.AddToClassList("clicavel-l");
                ComDica(pessoa, "Abrir o plano de " + j.nome + ": fotos, atributos por lado e treino.");
                r.Add(Celula("c-j", pessoa));

                // foco: clicar troca de atributo (descanso, mira, reflexo, spray, utilitária, sangue-frio)
                var jj = j;
                var foco = Celula("c-foco", BotaoMini(j.foco < 0 ? "Descanso" : Jogador.NomeAtrib(j.foco), () =>
                {
                    int prox = jj.foco + 1; if (prox > 4) prox = -1;
                    DefinirTreinoUI(jj, prox, prox < 0 ? 0 : Math.Max(1, jj.sessoes));
                }, j.foco >= 0, true, "bolt"));
                r.Add(foco);

                // lado: clicar alterna entre os dois lados, TR e CT
                string nomeLado = j.ladoFoco < 0 ? "Os dois" : j.ladoFoco == 0 ? "TR" : "CT";
                var lado = Celula("c-lado", BotaoMini(nomeLado, () =>
                {
                    int prox = jj.ladoFoco + 1; if (prox > 1) prox = -1;
                    var er = c.DefinirLadoTreino(jj, prox);
                    if (er != null) { Toast(er, true); return; }
                    Salvar(); Mostrar();
                }, j.ladoFoco >= 0, j.sessoes > 0));
                r.Add(lado);

                var st = E("stepper");
                st.Add(BotaoIcone("minus", () => DefinirTreinoUI(jj, jj.foco, jj.sessoes - 1), j.sessoes > 0));
                st.Add(L(j.sessoes.ToString()));
                st.Add(BotaoIcone("plus", () => DefinirTreinoUI(jj, jj.foco >= 0 ? jj.foco : MaisFraco(jj), jj.sessoes + 1), j.sessoes < c.MaxSessoes));
                r.Add(Celula("c-sess", st));

                var cf = E("c", "c-bar"); cf.Add(L(((int)j.cansaco).ToString(), "f16", "negrito", j.cansaco >= 60 ? "vermelho" : j.cansaco >= 35 ? "laranja" : "t")); cf.Add(BarraCansaco(j.cansaco)); r.Add(cf);
                var cp = E("c", "c-bar"); cp.Add(L(((int)j.preparo).ToString(), "f16", "negrito", "verde")); var bp = E("barra", "fina"); var fp = E("fill", "verde"); fp.style.width = Length.Percent(Mathf.Clamp(j.preparo, 0, 100)); bp.Add(fp); cp.Add(bp); r.Add(cp);

                var (ganho, preparoDepois, cansacoDepois) = c.PreverSemana(j);
                var pj = E("c", "c-proj"); var col = E();
                string l1 = j.foco >= 0 && j.sessoes > 0 ? (ganho > 0.005f ? Jogador.NomeAtrib(j.foco) + " " + Sinal(ganho) : Jogador.NomeAtrib(j.foco) + " sem ganho (perto do potencial ou muito cansado)") : "Sem treino";
                col.Add(L(l1, "f14", ganho > 0.005f ? "verde" : "suave"));
                if (cansacoDepois >= 60) col.Add(L("vai ficar exausto", "f13", "vermelho")); else if (cansacoDepois >= 35 && j.cansaco < 35) col.Add(L("vai ficar cansado", "f13", "laranja"));
                pj.Add(col); r.Add(pj);
                ComDica(pj, "Cansaço " + (int)j.cansaco + " → " + (int)cansacoDepois + " · preparo " + (int)j.preparo + " → " + (int)preparoDepois + " no fim da semana.");

                float evo = 0; for (int a = 0; a < 5; a++) evo += j.Valor(a) - j.inicioTemporada[a];
                var te = E("c", "c-bar"); te.Add(L(Sinal(evo) + " pts", "f15", "negrito", evo > 0.05f ? "verde" : evo < -0.05f ? "vermelho" : "suave")); r.Add(te);
                tab.Add(r);
            }
            conteudo.Add(tab);
        }
    }
}
