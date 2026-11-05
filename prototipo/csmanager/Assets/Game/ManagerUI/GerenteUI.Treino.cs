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
                cc.Add(L("Entrosamento: preparo extra para todo o elenco (e um pouco de cansaço).", "f14", "suave", "quebra"));
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
                em.Add(L("Sobe a afinidade do time com o mapa (+3 por semana). A afinidade entra na habilidade quando o mapa é jogado.", "f14", "suave", "quebra"));
            }
            topo.Add(em);

            var gh = E("treino-card");
            gh.Add(L("SUA GAMING HOUSE", "treino-card-tit"));
            gh.Add(L(c.GH.nome + " (nível " + c.nivelGH + ")", "f17", "negrito"));
            gh.Add(L("Até " + c.MaxSessoes + " sessões por semana por jogador. Recuperação x" + Dec(c.GH.recuperacao) + (c.GH.bootcamp ? ", bootcamp (+20% de ganho)." : "."), "f14", "suave", "quebra"));
            topo.Add(gh);
            conteudo.Add(topo);

            // atalhos
            var pre = E("presets");
            pre.Add(BotaoMini("Todos descansam", () => { foreach (var j in c.elenco) c.DefinirTreino(j, -1, 0); Salvar(); Mostrar(); }));
            pre.Add(BotaoMini("Moderado: 2 sessões no atributo mais fraco", () => { foreach (var j in c.elenco) c.DefinirTreino(j, MaisFraco(j), 2); Salvar(); Mostrar(); }));
            pre.Add(BotaoMini("Intenso: " + c.MaxSessoes + " sessões", () => { foreach (var j in c.elenco) c.DefinirTreino(j, j.foco >= 0 ? j.foco : MaisFraco(j), c.MaxSessoes); Salvar(); Mostrar(); }));
            conteudo.Add(pre);

            // tabela
            var tab = E();
            var h = E("tr", "th");
            h.Add(Celula("c-j", L("Jogador")));
            h.Add(Celula("c-foco", L("Foco")));
            h.Add(Celula("c-sess", L("Sessões por semana")));
            h.Add(Celula("c-bar", L("Cansaço")));
            h.Add(Celula("c-bar", L("Preparo")));
            h.Add(Celula("c-proj", L("Efeito desta semana")));
            h.Add(Celula("c-bar", L("Na temporada")));
            tab.Add(h);
            foreach (var j in c.elenco)
            {
                var r = E("tr");
                var pessoa = E("linha-h");
                pessoa.Add(Avatar(j.nome));
                var nomes = E("pessoa-txt");
                nomes.Add(L(j.nome, "f17", "negrito"));
                nomes.Add(L(Fmt.Funcao(j.funcao) + " · " + j.idade + " anos", "f14", "suave"));
                pessoa.Add(nomes);
                r.Add(Celula("c-j", pessoa));

                // foco: clicar troca de atributo (descanso, mira, reflexo, spray, utilitária, sangue-frio)
                var jj = j;
                var foco = Celula("c-foco", BotaoMini(j.foco < 0 ? "Descanso" : Jogador.NomeAtrib(j.foco), () =>
                {
                    int prox = jj.foco + 1; if (prox > 4) prox = -1;
                    DefinirTreinoUI(jj, prox, prox < 0 ? 0 : Math.Max(1, jj.sessoes));
                }, j.foco >= 0, true, "bolt"));
                r.Add(foco);

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
                col.Add(L("cansaço " + (int)j.cansaco + " → " + (int)cansacoDepois + " · preparo " + (int)j.preparo + " → " + (int)preparoDepois, "f13", cansacoDepois >= 60 ? "vermelho" : cansacoDepois >= 35 ? "laranja" : "suave"));
                pj.Add(col); r.Add(pj);

                float evo = 0; for (int a = 0; a < 5; a++) evo += j.Valor(a) - j.inicioTemporada[a];
                var te = E("c", "c-bar"); te.Add(L(Sinal(evo) + " pts", "f15", "negrito", evo > 0.05f ? "verde" : evo < -0.05f ? "vermelho" : "suave")); te.Add(L("soma dos 5 atributos", "f12", "apagado")); r.Add(te);
                tab.Add(r);
            }
            conteudo.Add(tab);

            // o efeito no time (o contrapeso)
            var tit = c.Titulares.ToList();
            float bonus = tit.Average(j => j.preparo) / 100f * Provisorio.PREPARO_MAX_BONUS * 100f;
            float pena = tit.Average(j => Math.Max(0f, (j.cansaco - Provisorio.CANSACO_LIMIAR) / (100f - Provisorio.CANSACO_LIMIAR)) * Provisorio.CANSACO_MAX_PENALIDADE) * 100f;
            var ef = E("efeito-time");
            ef.Add(L("Habilidade dos titulares na partida: " + Sinal(bonus - pena) + "% (preparo +" + Dec(bonus) + "%, cansaço −" + Dec(pena) + "%). "
                + (pena > bonus ? "O cansaço está pesando mais que o preparo: reduza as sessões." : bonus - pena < 0.6f && bonus < 1f ? "Quase sem preparo: treinar um pouco ajuda." : "Bom equilíbrio: o preparo compensa o cansaço."), "quebra"));
            conteudo.Add(ef);
            conteudo.Add(L("O treino evolui o atributo em foco (até o potencial, mais devagar com a idade) e dá preparo, um bônus temporário nas partidas. Cada sessão e cada partida geram cansaço; acima de 35 ele reduz a habilidade. Dormir (descanso) recupera.", "legenda"));
        }
    }
}
