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
            if (intel == 0) conteudo.Add(L("Sem scout, a estratégia é um palpite.", "f15", "laranja", "quebra", "aviso-linha"));
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
            CarregarPartida();
        }

        bool carregandoPartida;
        VisualElement carregandoEl;   // a capa de carregando (só a captura de telas a tira de novo)

        /// <summary>Tela de carregando antes de entrar na simulação (decisão 0031): a Unity trava por alguns segundos ao montar a maquete, então a tela aparece
        /// primeiro (com a barra correndo) e só então a cena da partida é carregada. A partida desenha a mesma tela e ela some num fade.</summary>
        void CarregarPartida(bool entrar = true)
        {
            if (carregandoPartida) return;
            carregandoPartida = true;
            var s = c.serie;
            var cv = E("carregando"); cv.pickingMode = PickingMode.Position;
            cv.Add(L("PREPARANDO A PARTIDA", "car-peq"));
            cv.Add(L(c.clube + "  ×  " + s.adv.nome, "car-tit"));
            cv.Add(L(s.camp.nome + (s.MapaAtual >= 0 ? "  ·  " + Mapas.NOMES[s.MapaAtual] : ""), "car-sub"));
            var trilho = E("car-trilho"); var seg = E("car-seg"); trilho.Add(seg); cv.Add(trilho);
            cv.Add(L("Montando o mapa…", "car-sub", "car-estado"));
            tela.Add(cv); carregandoEl = cv;
            cv.schedule.Execute(() => cv.AddToClassList("visivel")).StartingIn(1);
            seg.schedule.Execute(() => { float t = (Time.realtimeSinceStartup * 0.9f) % 1f; seg.style.left = (360f + 110f) * t - 110f; }).Every(16);
            if (entrar) cv.schedule.Execute(() => SceneManager.LoadScene(CENA_PARTIDA)).StartingIn(450);   // dá tempo de o carregando aparecer antes da trava
        }

        // ================================================================================================ pré-partida
        /// <summary>Tela cheia de confronto (decisão 0025): os dois times lado a lado com as escalações em retratos, a chance de vencer o mapa
        /// num anel grande no centro e, embaixo, a ação principal (assistir) entre as secundárias.</summary>
        void TelaPreMatch()
        {
            var s = c.serie; var adv = s.adv; int mapa = Math.Max(0, s.MapaAtual);
            if (preview != null) { Destroy(preview.gameObject); preview = null; }
            var raiz = E("prematch");
            janela.Add(raiz);

            var topo = E("pm2-topo");
            var rel = E("pm2-rel"); rel.Add(new Icone("clock", 20));
            rel.Add(L(Mapas.NOMES[mapa] + " · " + Fmt.Serie(s.Md) + " · série " + s.MinhasVit + " x " + s.DelesVit));
            topo.Add(rel);
            raiz.Add(topo);

            var corpo = E("pm2-corpo");
            int fa = c.Forca, fb = adv.Forca;
            corpo.Add(LadoPreMatch(c.clube, "VOCÊ · " + Estilos.Nome(c.estilo).ToUpperInvariant(), fa, fa > fb ? "up" : fa < fb ? "down" : null, true,
                c.Titulares.Select(j => (j.nome, j.funcao, j)).ToList()));

            // o centro: a chance de vencer este mapa (as mesmas contas de pular: decisão 0008)
            double chMapa = c.ChanceMapa(adv, mapa); int pct = (int)Math.Round(chMapa * 100);
            var centro = E("pm2-centro");
            centro.Add(L("CHANCE DE VENCER ESTE MAPA", "dado-rot"));
            var anel = new Anel(pct / 100f, pct >= 55 ? new Color(0.294f, 0.749f, 0.549f) : pct >= 45 ? new Color(0.384f, 0.659f, 0.933f) : new Color(0.925f, 0.416f, 0.373f), pct + "%", 250f, "pm2-anel-v");
            anel.style.marginTop = 14; centro.Add(anel);
            var duelo = E("pm2-duelo");
            var fe = L(c.ForcaEfetiva(mapa, adv).ToString("0.0"), "pm2-duelo-v", "laranja"); var fd = L(c.ForcaEfetivaAdv(adv, mapa).ToString("0.0"), "pm2-duelo-v", "azul");
            duelo.Add(fe); duelo.Add(L("força efetiva", "f13", "apagado", "centro")); duelo.Add(fd);
            ComDica(duelo, "Força efetiva de cada time neste mapa: preparo, cansaço, afinidade e estratégia incluídos.");
            centro.Add(duelo);
            var barra0 = E("pm-chance-barra");
            var ba = E("pm-chance-a"); ba.style.flexGrow = Mathf.Max(1, pct); barra0.Add(ba);
            var bb = E("pm-chance-b"); bb.style.flexGrow = Mathf.Max(1, 100 - pct); barra0.Add(bb);
            centro.Add(barra0);
            centro.Add(L("Chance de vencer a série: " + Pct(Carreira.ChanceSerie(chMapa, s.Md)), "pm2-chance-sub"));
            corpo.Add(centro);

            corpo.Add(LadoPreMatch(adv.nome, c.NivelIntel(adv.id) >= 1 ? Estilos.Nome(adv.estilo).ToUpperInvariant() : "ESTILO ?", fb, fb > fa ? "up" : fb < fa ? "down" : null, false,
                adv.jogadores.Select(x => (x.nome, x.funcao, (Jogador)null)).ToList()));
            raiz.Add(corpo);

            var barra = E("pm2-barra");
            var esq = E("pm2-acoes");
            esq.Add(Botao("Voltar", () => Ir(Aba.Serie)));
            esq.Add(Botao("Estratégia", () => Ir(Aba.Serie)));
            barra.Add(esq);
            var comecar = Botao("Assistir mapa", Assistir, !c.Falido, true, "playf", "cheio"); comecar.AddToClassList("pm2-comecar");
            barra.Add(comecar);
            var dir = E("pm2-acoes");
            dir.Add(Botao("Pular mapa", PularMapaUI, !c.Falido));
            dir.Add(Botao("Pular série", PularSerieUI, !c.Falido));
            barra.Add(dir);
            raiz.Add(barra);

            var dicas = E("pm-dicas");
            foreach (var (tecla, txt) in new[] { ("Enter", "ASSISTIR"), ("Esc", "VOLTAR") })
            {
                var it = E("pm-dicas-item"); var tk = E("tecla"); tk.Add(L(tecla)); it.Add(tk); it.Add(L(txt)); dicas.Add(it);
            }
            raiz.Add(dicas);
        }

        /// <summary>Um time na pré-partida: brasão, nome, estilo, força e os 5 jogadores com retrato (os nossos mostram o geral).</summary>
        static VisualElement LadoPreMatch(string nome, string sub, int forca, string seta, bool nosso, List<(string nome, Role funcao, Jogador j)> jogadores)
        {
            var l = E("pm2-lado"); if (!nosso) l.AddToClassList("dir");
            var cab = E("pm2-cab");
            var br = Brasao(nome, nosso ? "laranja" : "azul"); br.AddToClassList("pm2-brasao"); cab.Add(br);
            var tx = E("pm2-tx");
            tx.Add(L(nome.ToUpperInvariant(), "pm2-nome")); tx.Add(L(sub, "pm2-sub"));
            cab.Add(tx);
            var f = E("pm2-forca");
            f.Add(L("FORÇA", "dado-rot"));
            var fv = E("linha-h"); fv.Add(L(forca.ToString(), "pm2-forca-v", nosso ? "laranja" : "azul")); if (seta != null) { var ic = new Icone(seta, 18); ic.style.marginLeft = 4; fv.Add(ic); }
            f.Add(fv);
            cab.Add(f);
            l.Add(cab);
            foreach (var (jn, jf, j) in jogadores)
            {
                var r = E("pm2-j");
                r.Add(j != null ? Retrato(j, "pm2-foto") : RetratoNome(jn, "pm2-foto"));
                var t = E("pm2-j-tx");
                t.Add(L(jn.ToUpperInvariant(), "pm2-j-nome")); t.Add(L(Fmt.Funcao(jf), "f13", "suave"));
                r.Add(t);
                if (j != null) r.Add(L(j.Overall.ToString(), "pm2-j-ovr", ClasseNota(j.Overall)));
                l.Add(r);
            }
            return l;
        }
    }
}
