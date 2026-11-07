using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CSM.Manager;
using CSM.Sim;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Preparação da série (veto de mapas e estratégia, decisão 0014) e a pré-partida de cada mapa.</summary>
    public partial class GerenteUI
    {
        // ================================================================================================ preparação da série
        void TelaSerie()
        {
            var s = c.PrepararSerie();
            if (s == null) { aba = Aba.Painel; abaAnterior = aba; Mostrar(); return; }
            var adv = s.adv; var v = s.veto; int intel = c.NivelIntel(adv.id);

            // o adversário
            var hero = E("compromisso", "agora");
            var esq = E();
            esq.Add(L(s.camp.nome.ToUpperInvariant(), "compromisso-tit"));
            esq.Add(L(c.clube + " contra " + adv.nome, "compromisso-nome"));
            esq.Add(L(Fmt.Serie(s.Md) + (s.MinhasVit + s.DelesVit > 0 ? " · série " + s.MinhasVit + " x " + s.DelesVit : "") + " · adversário #" + c.mundo.PosicaoRanking(adv.id) + " do ranking", "compromisso-sub"));
            // retrospecto e clássico (decisão 0021)
            var rt = c.Contra(adv.nome);
            var linhaRt = E(); linhaRt.style.flexDirection = FlexDirection.Row; linhaRt.style.alignItems = Align.Center; linhaRt.style.marginTop = 6;
            if (rt.Rival) { var sc = Selo("CLÁSSICO", "laranja"); sc.style.marginRight = 8; ComDica(sc, "Rival do clube: a moral dos titulares muda o dobro nesta série, e vencê-la rende um bônus de patrocínio."); linhaRt.Add(sc); }
            linhaRt.Add(L("Retrospecto: " + rt.Texto + (rt.ultimaEliminacao != null ? " · eliminou o clube na " + rt.ultimaEliminacao : ""), "f14", "suave"));
            esq.Add(linhaRt);
            hero.Add(esq);
            int mapaRef = s.MapaAtual >= 0 ? s.MapaAtual : -1;
            double chMapa = c.ChanceMapa(adv, mapaRef);
            var dir = E(); dir.style.alignItems = Align.FlexEnd;
            var ch = L(Pct(Carreira.ChanceSerie(chMapa, s.Md)), "serie-chance", chMapa >= 0.5 ? "verde" : "laranja"); ch.pickingMode = PickingMode.Position;
            ComDica(ch, "Força efetiva " + c.ForcaEfetiva(mapaRef, adv).ToString("0.0") + " contra " + c.ForcaEfetivaAdv(adv, mapaRef).ToString("0.0") + " (preparo, cansaço, afinidade e estratégia incluídos).");
            dir.Add(ch);
            dir.Add(L("de chance de vencer a série", "f14", "suave"));
            dir.Add(L(intel >= 1 ? "Joga " + Estilos.Nome(adv.estilo).ToLowerInvariant() : "Estilo desconhecido: estude-o com um scout", "f14", intel >= 1 ? "azul" : "apagado"));
            hero.Add(dir);
            conteudo.Add(hero);

            // ------------------------------------------------------------------ veto
            conteudo.Add(L("Veto de mapas", "h2", "topo-h2"));
            string instrucao;
            if (!v.Completo)
            {
                int minhas = 0; for (int i = v.i; i < v.passos.Count; i++) if (v.passos[i].quem == 0) minhas++;
                instrucao = "Sua vez: " + (v.Atual.pick ? "clique no mapa que você quer JOGAR." : "clique no mapa que você quer BANIR.") + " Faltam " + minhas + ".";
            }
            else instrucao = "Veto concluído. Os mapas serão jogados nesta ordem:";
            var linhaIns = E("linha-h"); linhaIns.style.marginBottom = 12;
            var txtIns = L(instrucao, "f16", "negrito", v.Completo ? "verde" : "t"); txtIns.style.flexGrow = 1; linhaIns.Add(txtIns);
            if (!v.Completo) linhaIns.Add(Botao("Veto automático", () => { c.VetoAutomatico(); Salvar(); Mostrar(); Toast("O jogo vetou pelo time: " + string.Join(", ", c.serie.mapas.Select(m => Mapas.NOMES[m])) + "."); }));
            conteudo.Add(linhaIns);

            var pool = E("pool");
            for (int m = 0; m < Mapas.TOTAL; m++) pool.Add(CartaoMapa(m, s));
            conteudo.Add(pool);

            if (v.Completo)
            {
                var ord = E("ordem-mapas");
                for (int i = 0; i < s.mapas.Count; i++)
                {
                    var passo = v.passos.FirstOrDefault(p => p.pick && p.mapa == s.mapas[i]);
                    string quem = passo == null ? "decisivo" : passo.quem == 0 ? "escolha nossa" : "escolha deles";
                    ord.Add(Selo("Mapa " + (i + 1) + ": " + Mapas.NOMES[s.mapas[i]] + " (" + quem + ")", passo == null ? "verde" : passo.quem == 0 ? "laranja" : "azul"));
                }
                conteudo.Add(ord);
            }

            // ------------------------------------------------------------------ estratégia
            conteudo.Add(L("Estratégia", "h2"));
            if (intel == 0) conteudo.Add(L("Sem scout, a estratégia é um palpite.", "f15", "laranja", "quebra"));
            var estilos = E("estilos");
            foreach (Estilo e in Enum.GetValues(typeof(Estilo)))
            {
                var ee = e;
                var card = E("estilo-card"); if (c.estilo == e) card.AddToClassList("sel");
                var topo = E("estilo-topo");
                topo.Add(L(Estilos.Nome(e), "estilo-nome"));
                if (intel >= 1)
                {
                    int rel = Estilos.Relacao(e, adv.estilo);
                    if (rel > 0) topo.Add(Selo("CONTRA O ESTILO DELE · +" + (int)(Provisorio.ESTRATEGIA_CONTRA * 100) + "% de habilidade", "verde"));
                    else if (rel < 0) topo.Add(Selo("O ESTILO DELE GANHA DESSA · −" + (int)(Provisorio.ESTRATEGIA_CONTRA * 100) + "%", "vermelho"));
                    else if (e != Estilo.Equilibrada) topo.Add(Selo("neutra contra ele"));
                }
                card.Add(topo);
                if (c.estilo == e) card.Add(L(Estilos.Descricao(e), "estilo-desc")); else ComDica(card, Estilos.Descricao(e));
                Clicavel(card, () => { c.estilo = ee; Salvar(); Mostrar(); });
                estilos.Add(card);
            }
            conteudo.Add(estilos);

            // ------------------------------------------------------------------ ações
            var botoes = E("linha-h"); botoes.style.marginTop = 16;
            var ir = Botao(v.Completo ? "Ir para a pré-partida" : "Termine o veto para continuar", () => Ir(Aba.PreMatch), v.Completo && !c.Falido, true, "play"); ir.style.marginRight = 12; botoes.Add(ir);
            var pular = Botao("Pular série", PularSerieUI, !c.Falido); botoes.Add(pular);
            conteudo.Add(botoes);
        }

        VisualElement CartaoMapa(int m, Serie s)
        {
            var v = s.veto; var adv = s.adv; int intel = c.NivelIntel(adv.id);
            var passo = v.passos.FirstOrDefault(p => p.mapa == m);
            bool decisivo = v.Completo && v.restantes.Contains(m);
            bool minhaVez = !v.Completo && v.Atual.quem == 0 && v.restantes.Contains(m);

            var card = E("mapa-card");
            string estado = null, cor = null;
            if (passo != null)
            {
                if (!passo.pick) { card.AddToClassList("banido"); estado = passo.quem == 0 ? "BANIDO POR NÓS" : "BANIDO POR ELES"; }
                else if (passo.quem == 0) { card.AddToClassList("escolha-nos"); estado = "ESCOLHA NOSSA"; cor = "laranja"; }
                else { card.AddToClassList("escolha-eles"); estado = "ESCOLHA DELES"; cor = "azul"; }
            }
            else if (decisivo) { card.AddToClassList("decisivo"); estado = "DECISIVO"; cor = "verde"; }
            else if (minhaVez) { card.AddToClassList("livre"); card.AddToClassList("clicavel"); estado = v.Atual.pick ? "CLIQUE PARA ESCOLHER" : "CLIQUE PARA BANIR"; }

            card.Add(L(Mapas.NOMES[m], "mapa-nome"));
            card.Add(L(Mapas.Tem3D(m) ? "mapa 3D disponível" : "ainda sem 3D", "mapa-tag"));
            var nos = E("mapa-af"); nos.Add(L("Nós")); nos.Add(Barra(c.EquipeClube.afinidade[m], true)); nos.Add(L(c.EquipeClube.afinidade[m].ToString(), "num", "laranja")); card.Add(nos);
            var eles = E("mapa-af"); eles.Add(L("Eles"));
            if (intel >= 2) { eles.Add(Barra(adv.afinidade[m], true)); eles.Add(L(adv.afinidade[m].ToString(), "num", "azul")); }
            else { var b = E("barra", "fina"); eles.Add(b); eles.Add(L("?", "num", "apagado")); }
            card.Add(eles);
            if (estado != null && !minhaVez) { var sel = Selo(estado, cor); sel.AddToClassList("mapa-estado"); card.Add(sel); }   // "clique para banir" já está na instrução de cima
            if (minhaVez) Clicavel(card, () => VetarUI(m));
            return card;
        }

        void VetarUI(int m)
        {
            bool eraPick = c.serie.veto.Atual.pick;
            var erro = c.Vetar(m);
            Salvar(); Mostrar();
            if (erro != null) Toast(erro, true);
            else if (c.serie.veto.Completo) Toast("Veto concluído. " + (c.serie.mapas.Count > 0 ? "Primeiro mapa: " + Mapas.NOMES[c.serie.mapas[0]] + "." : ""));
        }

        // ================================================================================================ pular e assistir
        void PularSerieUI()
        {
            var r = c.PularSerie();
            if (r == null) { Toast("Não há série para pular.", true); return; }
            Salvar(); aba = Aba.Painel; Mostrar();   // o resultado aparece na pós-partida
        }

        void PularMapaUI()
        {
            var r = c.PularMapa();
            if (r == null) { Toast("Não há mapa para pular.", true); return; }
            Salvar(); aba = Aba.Painel; Mostrar();
        }

        void Assistir()
        {
            if (c.serie == null || c.serie.MapaAtual < 0 || !c.serie.veto.Completo) { Toast("Termine o veto antes de assistir.", true); return; }
            if (!Application.CanStreamedLevelBeLoaded(CENA_PARTIDA))
            {
                Toast("A cena " + CENA_PARTIDA + " não está no Build Settings. Rode o menu CS Manager > Manager > Recriar cena do manager.", true);
                return;
            }
            Salvar();
            c.PrepararAssistir(1000 + c.numPartida * 17 + c.Semana);
            SceneManager.LoadScene(CENA_PARTIDA);
        }

        // ================================================================================================ pré-partida
        /// <summary>Tela cheia como a referência de pré-partida: os dois times no centro (brasão, força, lista), os bonecos 3D
        /// do jogo nas laterais, a chance de vitória do mapa e a barra de ações embaixo.</summary>
        void TelaPreMatch()
        {
            var s = c.serie; var adv = s.adv; int mapa = Math.Max(0, s.MapaAtual);
            var raiz = E("prematch");
            try { raiz.style.unityFontDefinition = FontDefinition.FromFont(Font.CreateDynamicFontFromOSFont(new[] { "Bahnschrift", "Arial Narrow", "Segoe UI" }, 40)); }
            catch (Exception) { /* sem a fonte condensada, fica a padrão */ }
            janela.Add(raiz);

            raiz.Add(new Quadriculado { name = "pixels" }.WithClass("pm-pixels"));

            // bonecos 3D reais (RenderTexture); sem eles a tela funciona do mesmo jeito
            if (preview != null) Destroy(preview.gameObject);
            preview = BonecosPreview.Criar();
            if (preview != null)
            {
                raiz.Add(BonecoImagem(preview.rtEsq, "esq"));
                raiz.Add(BonecoImagem(preview.rtDir, "dir"));
            }

            var rel = E("pm-relogio"); rel.Add(new Icone("clock", 22));
            rel.Add(L(Mapas.NOMES[mapa] + " · " + Fmt.Serie(s.Md) + " · série " + s.MinhasVit + " x " + s.DelesVit)); raiz.Add(rel);
            raiz.Add(TagTime(c.clube, "VOCÊ · " + Estilos.Nome(c.estilo).ToUpperInvariant(), "esq"));
            raiz.Add(TagTime(adv.nome, c.NivelIntel(adv.id) >= 1 ? Estilos.Nome(adv.estilo).ToUpperInvariant() : "ESTILO ?", "dir"));

            var centro = E("pm-centro");
            int fa = c.Forca, fb = adv.Forca;
            centro.Add(LadoPreMatch(c.clube, fa, fa > fb ? "up" : fa < fb ? "down" : null, "laranja", c.Titulares.Select(j => (j.nome, j.funcao)).ToList()));
            centro.Add(E("pm-bola"));
            centro.Add(LadoPreMatch(adv.nome, fb, fb > fa ? "up" : fb < fa ? "down" : null, "azul", adv.jogadores));
            raiz.Add(centro);

            // chance de vitória do mapa e da série (as mesmas usadas ao pular: decisão 0008)
            double chMapa = c.ChanceMapa(adv, mapa); int pct = (int)Math.Round(chMapa * 100);
            var ch = E("pm-chance");
            ch.Add(L("CHANCE DE VENCER ESTE MAPA", "pm-chance-tit"));
            ch.Add(L(pct + "%", "pm-chance-num"));
            var barra0 = E("pm-chance-barra");
            var ba = E("pm-chance-a"); ba.style.flexGrow = Mathf.Max(1, pct); barra0.Add(ba);
            var bb = E("pm-chance-b"); bb.style.flexGrow = Mathf.Max(1, 100 - pct); barra0.Add(bb);
            ch.Add(barra0);
            ch.Add(L("Série: " + Pct(Carreira.ChanceSerie(chMapa, s.Md)) + " · força efetiva " + c.ForcaEfetiva(mapa, adv).ToString("0.0") + " contra " + c.ForcaEfetivaAdv(adv, mapa).ToString("0.0") + " (preparo, cansaço, afinidade e estratégia incluídos)", "pm-chance-sub"));
            raiz.Add(ch);

            var barra = E("pm-barra");
            var esq = E("seg");
            esq.Add(BotaoSeg("Voltar", () => Ir(Aba.Serie)));
            esq.Add(BotaoSeg("Estratégia", () => Ir(Aba.Serie)));
            barra.Add(esq);
            var comecar = E("comecar"); if (c.Falido) comecar.AddToClassList("desligado");
            comecar.Add(L("ASSISTIR MAPA"));
            var quad = E("quad"); quad.Add(new Icone("playf", 14)); comecar.Add(quad);
            if (!c.Falido) Clicavel(comecar, Assistir);
            barra.Add(comecar);
            var dir = E("seg");
            dir.Add(BotaoSeg("Pular mapa", PularMapaUI, !c.Falido));
            dir.Add(BotaoSeg("Pular série", PularSerieUI, !c.Falido));
            barra.Add(dir);
            raiz.Add(barra);

            var dicas = E("pm-dicas");
            foreach (var (tecla, txt) in new[] { ("Enter", "ASSISTIR"), ("Esc", "VOLTAR") })
            {
                var it = E("pm-dicas-item"); var tk = E("tecla"); tk.Add(L(tecla)); it.Add(tk); it.Add(L(txt)); dicas.Add(it);
            }
            raiz.Add(dicas);
        }

        static VisualElement BonecoImagem(RenderTexture rt, string lado)
        {
            var e = E("pm-boneco", lado);
            e.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(rt));
            e.pickingMode = PickingMode.Ignore;
            return e;
        }

        static VisualElement TagTime(string nome, string sub, string lado)
        {
            var t = E("pm-tag", lado);
            var n = E("pm-tag-nome"); n.Add(L(nome.ToUpperInvariant())); t.Add(n);
            var s = E("pm-tag-sub"); s.Add(new Icone("house", 16)); s.Add(L(sub)); t.Add(s);
            return t;
        }

        static VisualElement BotaoSeg(string texto, Action acao, bool ativo = true)
        {
            var b = E("seg-b"); if (!ativo) b.AddToClassList("desligado");
            b.Add(L(texto.ToUpperInvariant()));
            if (ativo) Clicavel(b, acao);
            return b;
        }

        static VisualElement LadoPreMatch(string nome, int forca, string seta, string cor, List<(string nome, Role funcao)> jogadores)
        {
            var l = E("pm-time");
            var crest = E("crest", cor + "-bg"); crest.Add(L(nome.Substring(0, 1)));
            var f = E("forca"); f.Add(L(forca.ToString())); if (seta != null) f.Add(new Icone(seta, 16));
            crest.Add(f);
            l.Add(crest);
            l.Add(L(nome.ToUpperInvariant(), "pm-nome"));
            foreach (var (jn, jf) in jogadores)
            {
                var r = E("pm-j");
                var fn = E("pm-j-fn"); fn.Add(L(Fmt.FuncaoCurta(jf))); r.Add(fn);
                r.Add(L(jn.ToUpperInvariant()));
                l.Add(r);
            }
            return l;
        }
    }
}
