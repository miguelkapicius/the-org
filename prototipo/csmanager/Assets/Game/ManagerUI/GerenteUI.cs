using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>
    /// A interface do manager: onde o jogador cuida do clube entre as partidas (elenco, mercado, Gaming House, finanças)
    /// e escolhe assistir ou pular a próxima partida. É o "fora da partida" que faltava ao protótipo.
    ///
    /// UI Toolkit montado em código (sem UXML/USS), estilo inline. As regras vêm de CSM.Manager.Carreira; aqui só desenho
    /// e clique. Tudo que a tela mostra de número (salário, preço, custo da Gaming House) é provisório — ver Provisorio.
    /// A cena é criada pelo menu CS Manager > Manager > Recriar cena do manager.
    /// </summary>
    public class GerenteUI : MonoBehaviour
    {
        public int seed = 7;
        public const string CENA_PARTIDA = "Partida3D";

        enum Aba { Painel, Elenco, Mercado, GamingHouse, Financas }
        Aba aba = Aba.Painel;
        Carreira c;
        VisualElement tela, topo, menu, conteudo;
        string aviso; bool avisoErro;

        IEnumerator Start()
        {
            Application.targetFrameRate = 60;
            if (Carreira.Atual == null) Carreira.Atual = Carreira.Nova(seed);
            c = Carreira.Atual;

            var ps = ScriptableObject.CreateInstance<PanelSettings>();
            ps.scaleMode = PanelScaleMode.ScaleWithScreenSize;
            ps.referenceResolution = new Vector2Int(1920, 1080);
            ps.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
            ps.match = 0.5f;
#if UNITY_EDITOR
            // se o projeto tiver um tema de runtime, usa (a interface não depende dele: tudo é estilo inline)
            foreach (var g in UnityEditor.AssetDatabase.FindAssets("t:ThemeStyleSheet"))
            {
                var t = UnityEditor.AssetDatabase.LoadAssetAtPath<ThemeStyleSheet>(UnityEditor.AssetDatabase.GUIDToAssetPath(g));
                if (t != null) { ps.themeStyleSheet = t; break; }
            }
#endif
            var doc = gameObject.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            yield return null;   // o UIDocument cria a raiz no quadro seguinte

            var raiz = doc.rootVisualElement;
            tela = Box(FlexDirection.Column, Fundo);
            tela.style.position = Position.Absolute;
            tela.style.left = 0; tela.style.top = 0; tela.style.right = 0; tela.style.bottom = 0;
            raiz.Add(tela);

            topo = Box(FlexDirection.Row, Painel); topo.style.height = 72; Pad(topo, 28, 0);
            topo.style.alignItems = Align.Center; topo.style.justifyContent = Justify.SpaceBetween; topo.style.flexShrink = 0;
            tela.Add(topo);

            var corpo = Box(FlexDirection.Row); corpo.style.flexGrow = 1; tela.Add(corpo);
            menu = Box(FlexDirection.Column, Painel, 0); menu.style.width = 260; Pad(menu, 16, 20); menu.style.flexShrink = 0;
            conteudo = Box(FlexDirection.Column); conteudo.style.flexGrow = 1; Pad(conteudo, 36, 28);
            corpo.Add(menu); corpo.Add(conteudo);

            Mostrar();
        }

        // ================================================================================================ estrutura
        void Mostrar()
        {
            // topo
            topo.Clear();
            var esq = Linha_();
            esq.Add(Txt(c.clube, 26, Laranja, true));
            var rod = Txt("Partida " + c.numPartida, 16, Suave); rod.style.marginLeft = 20; esq.Add(rod);
            topo.Add(esq);
            var dir = Linha_();
            dir.Add(Txt("Salários por partida  " + Fmt.Dinheiro(c.SalariosPorPartida), 15, Suave));
            var saldo = Txt(Fmt.Dinheiro(c.dinheiro), 26, c.dinheiro < 0 ? Vermelho : Texto, true); saldo.style.marginLeft = 32;
            dir.Add(saldo);
            topo.Add(dir);

            // menu
            menu.Clear();
            Item("Painel", Aba.Painel); Item("Elenco", Aba.Elenco); Item("Mercado", Aba.Mercado);
            Item("Gaming House", Aba.GamingHouse); Item("Finanças", Aba.Financas);
            var folga = Box(); folga.style.flexGrow = 1; menu.Add(folga);
            menu.Add(Paragrafo("Protótipo. Salários, preços, prêmios e custos da Gaming House são valores provisórios: o GDD ainda não decidiu como funcionam.", 12, Apagado));

            // conteúdo
            conteudo.Clear();
            if (aviso != null)
            {
                var b = Box(FlexDirection.Row, new Color(0, 0, 0, 0.25f), 0, 8); Pad(b, 18, 12);
                b.style.borderLeftWidth = 4; b.style.borderLeftColor = avisoErro ? Vermelho : Verde; b.style.marginBottom = 18;
                b.Add(Txt(aviso, 16, avisoErro ? Vermelho : Verde, true));
                conteudo.Add(b); aviso = null;
            }
            switch (aba)
            {
                case Aba.Painel: TelaPainel(); break;
                case Aba.Elenco: TelaElenco(); break;
                case Aba.Mercado: TelaMercado(); break;
                case Aba.GamingHouse: TelaGamingHouse(); break;
                case Aba.Financas: TelaFinancas(); break;
            }
        }

        void Item(string texto, Aba a)
        {
            bool atual = aba == a;
            var it = Linha_(); it.style.height = 48; Pad(it, 16, 0); Raio(it, 8); it.style.marginBottom = 4;
            it.style.backgroundColor = atual ? Painel2 : new Color(0, 0, 0, 0);
            it.style.borderLeftWidth = 4; it.style.borderLeftColor = atual ? Laranja : new Color(0, 0, 0, 0);
            it.Add(Txt(texto, 18, atual ? Texto : Suave, atual));
            it.RegisterCallback<PointerEnterEvent>(_ => { if (aba != a) it.style.backgroundColor = new Color(1, 1, 1, 0.04f); });
            it.RegisterCallback<PointerLeaveEvent>(_ => { if (aba != a) it.style.backgroundColor = new Color(0, 0, 0, 0); });
            it.RegisterCallback<ClickEvent>(_ => { aba = a; Mostrar(); });
            menu.Add(it);
        }

        void Titulo(string t, string sub = null)
        {
            conteudo.Add(Txt(t, 30, Texto, true));
            if (sub != null) { var s = Paragrafo(sub, 15); s.style.marginTop = 4; conteudo.Add(s); }
            conteudo.Children().Last().style.marginBottom = 22;
        }

        void Resposta(string erro, string ok)
        {
            if (erro != null) { aviso = erro; avisoErro = true; } else { aviso = ok; avisoErro = false; }
            Mostrar();
        }

        // ================================================================================================ painel
        void TelaPainel()
        {
            Titulo("Painel do clube");

            if (c.naoVisto != null)
            {
                var r = c.naoVisto; c.naoVisto = null;
                var b = Box(FlexDirection.Row, new Color(0, 0, 0, 0.25f), 0, 8); Pad(b, 18, 12); b.style.marginBottom = 18;
                b.style.borderLeftWidth = 4; b.style.borderLeftColor = r.venceu ? Verde : Vermelho;
                b.Add(Txt((r.venceu ? "Vitória " : "Derrota ") + r.Placar + " contra " + r.adversario + (r.assistida ? "" : " (partida pulada)")
                    + "   ·   prêmio " + Fmt.Dinheiro(r.premio) + "   ·   salários " + Fmt.Dinheiro(-r.salarios), 16, r.venceu ? Verde : Vermelho, true));
                conteudo.Add(b);
            }
            if (c.Falido)
            {
                Cartao("Falência", out var fc);
                fc.Add(Txt("O dinheiro do clube acabou.", 22, Vermelho, true));
                var p = Paragrafo("O GDD define que o jogo acaba aqui. Ainda está em aberto se haverá aviso antes ou saída como empréstimo.", 15); p.style.marginTop = 8; p.style.marginBottom = 16; fc.Add(p);
                fc.Add(Botao("Começar nova carreira", () => { Carreira.Atual = Carreira.Nova(seed + c.numPartida); c = Carreira.Atual; aba = Aba.Painel; Mostrar(); }, true, Laranja, 260));
                conteudo.Add(fc.parent);
                return;
            }

            var linha = Box(FlexDirection.Row);

            // próxima partida
            var cartaoPartida = Cartao("Partida " + c.numPartida + " · próxima partida", out var cp, 28);
            cartaoPartida.style.flexGrow = 3; cartaoPartida.style.marginRight = 16;
            var confronto = Linha_(); confronto.style.justifyContent = Justify.SpaceBetween; confronto.style.marginBottom = 8;
            confronto.Add(Lado(c.clube, c.Forca, Laranja, false));
            confronto.Add(Txt("VS", 20, Apagado, true));
            confronto.Add(Lado(c.proximo.nome, c.proximo.forca, Azul, true));
            cp.Add(confronto);
            int dif = c.Forca - c.proximo.forca;
            var leitura = Paragrafo(dif > 2 ? "Seu time é favorito." : dif < -2 ? "O adversário é favorito." : "Confronto equilibrado.", 15);
            leitura.style.unityTextAlign = TextAnchor.MiddleCenter; leitura.style.marginBottom = 22;
            cp.Add(leitura);
            var botoes = Linha_(); botoes.style.justifyContent = Justify.Center;
            var assistir = Botao("Assistir partida", Assistir, true, Laranja, 260); assistir.style.marginRight = 12;
            botoes.Add(assistir);
            botoes.Add(Botao("Pular partida", PularPartida, true, null, 220));
            cp.Add(botoes);
            var nota = Paragrafo("Pular mostra só o resultado, calculado por probabilidade (decisão 0008).", 13, Apagado);
            nota.style.unityTextAlign = TextAnchor.MiddleCenter; nota.style.marginTop = 12;
            cp.Add(nota);
            linha.Add(cartaoPartida);

            // titulares
            var cartaoTit = Cartao("Titulares", out var ct);
            cartaoTit.style.flexGrow = 2;
            foreach (var j in c.Titulares)
            {
                var r = Linha_(); r.style.height = 36;
                var n = Txt(j.nome, 17, Texto, true); n.style.width = 120; r.Add(n);
                var f = Txt(Fmt.Funcao(j.funcao), 14, Suave); f.style.flexGrow = 1; r.Add(f);
                r.Add(Txt(j.Overall.ToString(), 18, CorNota(j.Overall), true));
                ct.Add(r);
            }
            linha.Add(cartaoTit);
            conteudo.Add(linha);

            // histórico
            var hist = Cartao("Últimas partidas", out var ch);
            if (c.historico.Count == 0) ch.Add(Paragrafo("Nenhuma partida jogada ainda.", 15));
            foreach (var r in c.historico.Take(5))
            {
                var l = Linha_(); l.style.height = 32;
                var rod = Txt("Partida " + r.numPartida, 14, Suave); rod.style.width = 110; l.Add(rod);
                var res = Txt(r.venceu ? "Vitória" : "Derrota", 15, r.venceu ? Verde : Vermelho, true); res.style.width = 90; l.Add(res);
                var pl = Txt(r.Placar, 15, Texto, true); pl.style.width = 80; l.Add(pl);
                l.Add(Txt("contra " + r.adversario + (r.assistida ? "" : " · pulada"), 15, Suave));
                ch.Add(l);
            }
            conteudo.Add(hist);
        }

        static VisualElement Lado(string nome, int forca, Color cor, bool direita)
        {
            var l = Box(); l.style.width = 280; l.style.alignItems = direita ? Align.FlexEnd : Align.FlexStart;
            l.Add(Txt(nome, 34, cor, true));
            l.Add(Txt("Força " + forca, 16, Suave));
            return l;
        }

        void Assistir()
        {
            if (!Application.CanStreamedLevelBeLoaded(CENA_PARTIDA))
            {
                Resposta("A cena " + CENA_PARTIDA + " não está no Build Settings. Rode o menu CS Manager > Manager > Recriar cena do manager.", null);
                return;
            }
            c.PrepararAssistir(1000 + c.numPartida * 17);
            SceneManager.LoadScene(CENA_PARTIDA);
        }

        void PularPartida()
        {
            c.Pular();
            Mostrar();   // o painel mostra o resultado (naoVisto)
        }

        // ================================================================================================ elenco e mercado
        static readonly (string nome, float larg)[] COLUNAS =
        {
            ("", 96), ("JOGADOR", 120), ("IDADE", 56), ("FUNÇÃO", 90), ("GERAL", 64),
            ("MIRA", 92), ("REFLEXO", 92), ("SPRAY", 92), ("UTILITÁRIA", 92), ("SANGUE-FRIO", 92), ("SALÁRIO", 104),
        };

        VisualElement Cabecalho(string ultima = "")
        {
            var h = LinhaTabela(true);
            foreach (var (n, w) in COLUNAS) h.Add(Celula(Txt(n, 12, Suave, true), w));
            h.Add(Celula(Txt(ultima, 12, Suave, true), 0));
            return h;
        }

        VisualElement LinhaJogador(Jogador j, VisualElement primeira)
        {
            var r = LinhaTabela();
            r.style.height = 56;
            r.Add(Celula(primeira, COLUNAS[0].larg));
            r.Add(Celula(Txt(j.nome, 17, Texto, true), COLUNAS[1].larg));
            r.Add(Celula(Txt(j.idade.ToString(), 15, Suave), COLUNAS[2].larg));
            r.Add(Celula(Txt(Fmt.Funcao(j.funcao), 15, Suave), COLUNAS[3].larg));
            r.Add(Celula(Txt(j.Overall.ToString(), 20, CorNota(j.Overall), true), COLUNAS[4].larg));
            r.Add(Celula(Atributo(j.Mira), COLUNAS[5].larg));
            r.Add(Celula(Atributo(j.Reflexo), COLUNAS[6].larg));
            r.Add(Celula(Atributo(j.Spray), COLUNAS[7].larg));
            r.Add(Celula(Atributo(j.Utilitaria), COLUNAS[8].larg));
            r.Add(Celula(Atributo(j.SangueFrio), COLUNAS[9].larg));
            r.Add(Celula(Txt(Fmt.Dinheiro(j.Salario), 15, Suave), COLUNAS[10].larg));
            return r;
        }

        void TelaElenco()
        {
            Titulo("Elenco", "Os " + Provisorio.TITULARES + " primeiros da lista são os titulares: são eles que jogam a próxima partida. Use Subir e Descer para mudar a ordem.");
            Cartao(null, out var corpo, 8);
            var cartao = corpo.parent;
            corpo.Add(Cabecalho("AÇÕES"));
            foreach (var j in c.elenco.ToList())
            {
                bool tit = c.Titular(j);
                var r = LinhaJogador(j, Selo(tit ? "TITULAR" : "RESERVA", tit ? Laranja : Suave));
                var acoes = Linha_();
                var sobe = Botao("Subir", () => { c.Mover(j, -1); Mostrar(); }, c.elenco.IndexOf(j) > 0, null, 72); sobe.style.marginRight = 6;
                var desce = Botao("Descer", () => { c.Mover(j, 1); Mostrar(); }, c.elenco.IndexOf(j) < c.elenco.Count - 1, null, 84); desce.style.marginRight = 12;
                acoes.Add(sobe); acoes.Add(desce);
                acoes.Add(Botao("Vender  +" + Fmt.Dinheiro(j.Venda), () => Resposta(c.Vender(j), j.nome + " foi vendido."), c.elenco.Count > Provisorio.TITULARES));
                r.Add(Celula(acoes, 0));
                corpo.Add(r);
            }
            conteudo.Add(cartao);
            conteudo.Add(Paragrafo("Elenco " + c.elenco.Count + "/" + Provisorio.ELENCO_MAX + ". Os atributos são a leitura do que a IA do bot usa na partida: mira, reflexo, spray, utilitária e sangue-frio.", 13, Apagado));
        }

        void TelaMercado()
        {
            Titulo("Mercado", "Jogadores livres para contratar. A lista se renova a cada partida.");
            Cartao(null, out var corpo, 8);
            var cartao = corpo.parent;
            corpo.Add(Cabecalho("CONTRATAÇÃO"));
            bool cheio = c.elenco.Count >= Provisorio.ELENCO_MAX;
            foreach (var j in c.mercado.ToList())
            {
                var r = LinhaJogador(j, Txt("", 1));
                bool pode = !cheio && c.dinheiro >= j.Valor;
                var v = Txt(Fmt.Dinheiro(j.Valor), 15, c.dinheiro >= j.Valor ? Texto : Vermelho, true); v.style.width = 110;
                var a = Linha_(); a.Add(v);
                a.Add(Botao("Contratar", () => Resposta(c.Contratar(j), j.nome + " entrou no elenco (reserva)."), pode, Laranja));
                r.Add(Celula(a, 0));
                corpo.Add(r);
            }
            conteudo.Add(cartao);
            if (cheio) conteudo.Add(Paragrafo("Elenco cheio: venda alguém para contratar.", 14, Laranja));
        }

        // ================================================================================================ gaming house
        void TelaGamingHouse()
        {
            Titulo("Gaming House", "A instalação do clube. Subir o nível libera outras melhorias (decisão 0007).");
            Cartao("Nível", out var corpo, 28);
            var cartao = corpo.parent;
            var niveis = Linha_(); niveis.style.marginBottom = 18;
            for (int n = 1; n <= Provisorio.NIVEL_GH_MAX; n++)
            {
                bool tem = n <= c.nivelGH;
                var q = Box(FlexDirection.Row, tem ? Laranja : Painel2, 0, 8);
                q.style.width = 84; q.style.height = 64; q.style.marginRight = 10; q.style.alignItems = Align.Center; q.style.justifyContent = Justify.Center;
                q.Add(Txt(n.ToString(), 26, tem ? Hex("1a1306") : Apagado, true));
                niveis.Add(q);
            }
            corpo.Add(niveis);
            if (c.nivelGH >= Provisorio.NIVEL_GH_MAX) corpo.Add(Txt("Nível máximo.", 18, Verde, true));
            else
            {
                var l = Linha_();
                var t = Txt("Subir para o nível " + (c.nivelGH + 1) + "  ·  " + Fmt.Dinheiro(c.CustoProximoNivelGH), 18, Texto, true); t.style.marginRight = 20; l.Add(t);
                l.Add(Botao("Subir de nível", () => Resposta(c.SubirGamingHouse(), "Gaming House no nível " + c.nivelGH + "."), c.dinheiro >= c.CustoProximoNivelGH, Laranja));
                corpo.Add(l);
            }
            conteudo.Add(cartao);

            Cartao("O que ela libera", out var lib);
            var libCartao = lib.parent;
            foreach (var (nome, nivel) in Provisorio.LIBERA_GH)
            {
                bool aberto = c.nivelGH >= nivel;
                var l = Linha_(); l.style.height = 40;
                var n = Txt(nome, 18, aberto ? Texto : Suave, true); n.style.width = 160; l.Add(n);
                l.Add(aberto ? Selo("LIBERADO", Verde) : Selo("BLOQUEADO · nível " + nivel, Apagado));
                lib.Add(l);
            }
            lib.Add(Paragrafo("Treinos e scouts ainda não existem no protótipo: aqui só mostramos o bloqueio. Quantos níveis são, o que cada um libera e o custo ainda são perguntas abertas.", 13, Apagado));
            conteudo.Add(libCartao);
        }

        // ================================================================================================ finanças
        void TelaFinancas()
        {
            Titulo("Finanças", "Economia do clube: prêmios e vendas entram; salários, contratações e melhorias saem.");
            var linha = Box(FlexDirection.Row);
            linha.Add(Resumo("Saldo", Fmt.Dinheiro(c.dinheiro), c.dinheiro < 0 ? Vermelho : Texto));
            linha.Add(Resumo("Salários por partida", Fmt.Dinheiro(-c.SalariosPorPartida), Vermelho));
            linha.Add(Resumo("Prêmio (vitória / derrota)", Fmt.Dinheiro(Provisorio.PREMIO_VITORIA) + " / " + Fmt.Dinheiro(Provisorio.PREMIO_DERROTA), Verde));
            conteudo.Add(linha);

            Cartao("Extrato", out var ex);
            var cartao = ex.parent;
            if (c.extrato.Count == 0) ex.Add(Paragrafo("Sem movimentações ainda.", 15));
            foreach (var l in c.extrato.Take(14))
            {
                var r = Linha_(); r.style.height = 32;
                var rd = Txt("Partida " + l.numPartida, 14, Suave); rd.style.width = 110; r.Add(rd);
                var d = Txt(l.descricao, 15, Texto); d.style.flexGrow = 1; r.Add(d);
                r.Add(Txt((l.valor > 0 ? "+" : "") + Fmt.Dinheiro(l.valor), 15, l.valor >= 0 ? Verde : Vermelho, true));
                ex.Add(r);
            }
            conteudo.Add(cartao);
        }

        static VisualElement Resumo(string titulo, string valor, Color cor)
        {
            var cartao = Cartao(titulo, out var corpo);
            cartao.style.flexGrow = 1; cartao.style.marginRight = 16;
            corpo.Add(Txt(valor, 28, cor, true));
            return cartao;
        }
    }
}
