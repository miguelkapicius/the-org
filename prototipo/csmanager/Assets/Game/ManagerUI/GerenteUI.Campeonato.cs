using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Tela de um campeonato (referência: página de detalhes de torneio): capa com o título, um cartão de resumo flutuante (datas,
    /// premiação, pontos, contagem ou ação) e as abas Visão geral, Prêmios, Participantes, Partidas e Chaves (ou Classificação, na liga).
    /// Também o leaderboard do ranking geral, com a mesma capa.</summary>
    public partial class GerenteUI
    {
        int campAba; Campeonato campAbaDe;   // aba aberta e de qual campeonato (volta à primeira ao abrir outro)
        int rkPagina = -1;                   // página do leaderboard (-1 = a do clube)
        const int RK_POR_PAGINA = 12;

        // ------------------------------------------------------------------------------------------ capa
        /// <summary>A capa de uma tela: a arte, o título grande e a trilha ("Calendário › Campeonato").</summary>
        VisualElement CapaTela(string titulo, Action voltar, string pai, string atual)
        {
            var capa = E("camp-capa");
            var img = Resources.Load<Texture2D>("ManagerUI/capa");
            if (img != null)
            {
                capa.style.backgroundImage = new StyleBackground(img);
                capa.style.backgroundSize = new BackgroundSize(BackgroundSizeType.Cover);
                capa.style.backgroundPositionX = new BackgroundPosition(BackgroundPositionKeyword.Center);
                capa.style.backgroundPositionY = new BackgroundPosition(BackgroundPositionKeyword.Center);
            }
            var veu = E("hub-veu"); veu.pickingMode = PickingMode.Ignore;
            veu.style.backgroundImage = new StyleBackground(VeuLateral()); veu.style.unityBackgroundScaleMode = ScaleMode.StretchToFill; capa.Add(veu);
            capa.Add(L(titulo, "camp-titulo"));
            var mig = E("camp-crumb");
            var a = L(pai, "camp-crumb-link"); a.pickingMode = PickingMode.Position; Clicavel(a, voltar); mig.Add(a);
            mig.Add(L("›", "camp-crumb-sep")); mig.Add(L(atual, "camp-crumb-atual"));
            capa.Add(mig);
            return capa;
        }

        // ------------------------------------------------------------------------------------------ o campeonato
        void DetalheCampeonato(Campeonato k)
        {
            if (campAbaDe != k) { campAba = 0; campAbaDe = k; }
            conteudo.Add(CapaTela(k.nome, () => { campAberto = null; Mostrar(); }, "Calendário", k.nome));
            conteudo.Add(ResumoCampeonato(k));

            var nomes = new[] { "Visão geral", "Prêmios", "Participantes", "Partidas", k.formato == Formato.Liga ? "Classificação" : "Chaves" };
            var abas = Abas(nomes, campAba, i => { campAba = i; Mostrar(); }); abas.style.marginTop = 24;
            conteudo.Add(abas);
            switch (campAba)
            {
                case 1: AbaPremios(k); break;
                case 2: AbaParticipantes(k); break;
                case 3: AbaPartidas(k); break;
                case 4: if (k.formato == Formato.Liga) AbaClassificacao(k); else AbaChaves(k); break;
                default: AbaVisao(k); break;
            }
        }

        VisualElement ResumoCampeonato(Campeonato k)
        {
            var card = E("camp-resumo");
            var esq = E("camp-r-esq");
            esq.Add(L(k.nome, "camp-r-nome"));
            var d1 = Mundo.DataDe(c.Temporada, k.semanaInicio, 0); var d2 = Mundo.DataDe(c.Temporada, k.SemanaFim, 6);
            var chips = E("linha-h"); chips.style.marginTop = 12; chips.style.flexWrap = Wrap.Wrap;
            chips.Add(Selo("Começa " + DataCurta(d1), "laranja"));
            var s2 = Selo("Termina " + DataCurta(d2), "verde"); s2.style.marginLeft = 8; chips.Add(s2);
            var s3 = Selo(NomeFormato(k.formato).ToUpperInvariant(), null); s3.style.marginLeft = 8; chips.Add(s3);
            if (k.major) { var s4 = Selo("MAJOR", "laranja"); s4.style.marginLeft = 8; chips.Add(s4); }
            esq.Add(chips);
            card.Add(esq);

            card.Add(StatCamp("PREMIAÇÃO", Fmt.Dinheiro(k.premio), "verde", "Ver prêmios", () => { campAba = 1; Mostrar(); }));
            card.Add(StatCamp("PONTOS DE RANKING", "+" + (int)k.PontosPara(1), "t", null, null));
            card.Add(AcaoCamp(k, d1));
            return card;
        }

        static VisualElement StatCamp(string rotulo, string valor, string cor, string link, Action acao)
        {
            var s = E("camp-stat");
            s.Add(L(rotulo, "dado-rot"));
            s.Add(L(valor, "camp-stat-v", cor));
            if (link != null) { var l = L(link, "f14", "verde", "camp-link"); l.pickingMode = PickingMode.Position; Clicavel(l, acao); s.Add(l); }
            return s;
        }

        VisualElement AcaoCamp(Campeonato k, DateTime inicio)
        {
            var a = E("camp-acao");
            if (k.terminado)
            {
                var t = c.titulos.FirstOrDefault(x => x.nome == k.nome);
                a.Add(L("SEU RESULTADO", "dado-rot"));
                a.Add(L(t != null ? Fmt.Ordinal(t.posicao) + " lugar" : k.times.Contains(0) ? "Encerrado" : "Não jogou", "camp-stat-v", t != null && t.posicao <= 3 ? "verde" : "t"));
                if (t != null) a.Add(L(Fmt.Dinheiro(t.premio) + " de prêmio", "f14", "suave"));
                return a;
            }
            if (k.iniciado)
            {
                var p = k.PendenteDoClube;
                if (p == null) { a.Add(L("SITUAÇÃO", "dado-rot")); a.Add(L("Em andamento", "camp-stat-v")); return a; }
                a.Add(L("SUA SÉRIE", "dado-rot"));
                a.Add(L("contra " + NomeTime(p.a == 0 ? p.b : p.a), "camp-stat-v", "laranja"));
                a.Add(L(Fmt.Rotulo(k.nome, p.rotulo) + " · " + Fmt.Serie(p.md), "f14", "suave"));
                if (c.TemJogoAgora && c.Proximo().camp == k)
                {
                    var b = Botao("Preparar série", () => Ir(Aba.Serie), !c.Falido, true, "play"); b.style.marginTop = 10; a.Add(b);
                }
                return a;
            }
            int dias = Math.Max(0, (inicio - Mundo.DataDe(c.Temporada, c.Semana, 0)).Days);
            a.Add(L("COMEÇA EM", "dado-rot"));
            var numero = E("linha-h"); numero.style.alignItems = Align.FlexEnd;
            numero.Add(L(dias.ToString(), "camp-contagem")); var un = L(dias == 1 ? "dia" : "dias", "f16", "suave"); un.style.marginLeft = 8; un.style.marginBottom = 6; numero.Add(un);
            a.Add(numero);
            a.Add(L(k.times.Contains(0) ? "Seu clube está inscrito" : "Seu clube não participa", "f14", k.times.Contains(0) ? "verde" : "suave"));
            return a;
        }

        // ------------------------------------------------------------------------------------------ visão geral
        static string DescricaoFormato(Campeonato k) => k.formato switch
        {
            Formato.Liga => "Todos contra todos, uma série por semana. Vence quem somar mais pontos: 3 por série vencida.",
            Formato.GruposMataMata => "Os times são divididos em grupos de 4 que jogam todos contra todos. Os 2 melhores de cada grupo seguem para o mata-mata, com chaveamento cruzado.",
            Formato.GruposGslMataMata => "Grupos de dupla eliminação (estilo GSL), todos em MD3: quem perde a abertura ainda tem uma segunda chance. Os 2 melhores de cada grupo seguem para o mata-mata.",
            Formato.SuicoMataMata => "Sistema suíço, como num Major: 3 vitórias classificam e 3 derrotas eliminam. As rodadas decisivas são em MD3. Os 8 melhores seguem para o mata-mata.",
            _ => "Eliminação simples: quem perde sai. MD1 nas primeiras fases, MD3 nas decisivas e MD5 na final.",
        };

        VisualElement CartaoCamp(string titulo)
        {
            var cd = E("camp-card");
            if (titulo != null) cd.Add(L(titulo, "camp-card-tit"));
            return cd;
        }

        static void Ponto(VisualElement pai, string texto, string cor = null)
        {
            var lin = E("pb-item"); lin.Add(E("pb-item-q", cor ?? "neutro"));
            var t = L(texto, "f15", "quebra"); t.style.flexShrink = 1; lin.Add(t); pai.Add(lin);
        }

        void AbaVisao(Campeonato k)
        {
            var linha = E("camp-visao");

            var main = E("camp-col-principal");
            var sobre = CartaoCamp("Sobre o campeonato");
            sobre.Add(L(DescricaoFormato(k), "camp-txt", "quebra"));
            sobre.Add(L("Como funciona", "camp-sub"));
            Ponto(sobre, k.times.Count + " times, circuito " + Mundo.NomeCircuito(k.circuito) + ".");
            Ponto(sobre, "Cada confronto é uma série: o mapa e a estratégia valem em todas, e você só assiste às do seu clube.");
            Ponto(sobre, "As séries do seu clube aparecem no Calendário no dia em que caem; as dos outros times são resolvidas pela chance de vitória.");
            sobre.Add(L("Premiação e ranking", "camp-sub"));
            Ponto(sobre, "Premiação total de " + Fmt.Dinheiro(k.premio) + ", dividida pela colocação final (aba Prêmios).", "verde");
            Ponto(sobre, "Vale até +" + (int)k.PontosPara(1) + " pontos no ranking para o campeão" + (k.major ? " (é um Major: pesa mais)." : "."));
            main.Add(sobre);
            linha.Add(main);

            var lado = E("camp-col-lado");
            var part = CartaoCamp("Sua participação");
            if (!k.times.Contains(0)) part.Add(L("Seu clube não está neste campeonato.", "camp-txt", "quebra"));
            else
            {
                var (st, cor) = StatusCampeonato(k);
                part.Add(L(st, "f16", "negrito", cor, "quebra"));
                var (v, d) = Campanha(k, 0);
                part.Add(L("Campanha: " + v + " vitória" + (v == 1 ? "" : "s") + " e " + d + " derrota" + (d == 1 ? "" : "s") + " em séries.", "f14", "suave", "quebra"));
                part.Add(L("Ranking do clube: #" + c.PosicaoRanking + " de " + Mundo.TIMES, "f14", "suave"));
            }
            lado.Add(part);

            var ficha = CartaoCamp("Ficha");
            void Dado(string r, string v2) { var l = E("camp-dado"); l.Add(L(r, "f14", "apagado")); l.Add(L(v2, "f15", "negrito")); ficha.Add(l); }
            Dado("Circuito", Mundo.NomeCircuito(k.circuito)); Dado("Formato", NomeFormato(k.formato)); Dado("Times", k.times.Count.ToString());
            Dado("Semanas", k.semanaInicio + " a " + k.SemanaFim); Dado("Peso no ranking", "x" + k.pesoRanking.ToString("0.0"));
            lado.Add(ficha);

            var lid = CartaoCamp(k.terminado ? "Pódio" : k.formato == Formato.Liga && k.iniciado ? "Líderes" : null);
            if (k.terminado) { for (int i = 0; i < Math.Min(3, k.colocacao.Count); i++) LinhaMini(lid, Fmt.Ordinal(i + 1), k.colocacao[i]); lado.Add(lid); }
            else if (k.formato == Formato.Liga && k.iniciado) { int i = 1; foreach (var (id, pts, _, _) in k.TabelaLiga().Take(3)) LinhaMini(lid, Fmt.Ordinal(i++), id, pts + " pts"); lado.Add(lid); }
            linha.Add(lado);
            conteudo.Add(linha);
        }

        void LinhaMini(VisualElement pai, string pos, int id, string extra = null)
        {
            var l = E("camp-mini"); if (id == 0) l.AddToClassList("meu");
            l.Add(L(pos, "camp-mini-pos")); l.Add(Avatar(NomeTime(id))); var n = L(NomeTime(id), "f15", id == 0 ? "negrito" : "t"); n.style.marginLeft = 10; n.style.flexGrow = 1; l.Add(n);
            if (extra != null) l.Add(L(extra, "f14", "suave"));
            pai.Add(l);
        }

        /// <summary>Séries vencidas e perdidas por um time neste campeonato.</summary>
        static (int v, int d) Campanha(Campeonato k, int id)
        {
            int v = 0, d = 0;
            foreach (var f in k.TodosConfrontos.Where(x => x.feito && x.Envolve(id))) { if (f.Vencedor == id) v++; else d++; }
            return (v, d);
        }

        // ------------------------------------------------------------------------------------------ prêmios
        void AbaPremios(Campeonato k)
        {
            int n = k.times.Count;
            var cab = E("camp-linha-cab");
            cab.Add(L("Colocação", "camp-c1")); cab.Add(L("Premiação", "camp-c2")); cab.Add(L("Pontos de ranking", "camp-c2"));
            conteudo.Add(cab);
            int meu = k.terminado ? k.Colocacao(0) : -1;
            // agrupa as colocações que valem o mesmo (3º e 4º, 5º ao 8º...)
            int i = 1;
            while (i <= n)
            {
                int j = i; while (j + 1 <= n && k.PremioPara(j + 1) == k.PremioPara(i) && Math.Abs(k.PontosPara(j + 1) - k.PontosPara(i)) < 0.001f) j++;
                var l = E("camp-linha"); if (meu >= i && meu <= j) l.AddToClassList("meu");
                string cor = i == 1 ? "verde" : i == 2 ? "laranja" : i <= 4 ? "azul" : "t";
                l.Add(L(i == j ? Fmt.Ordinal(i) : Fmt.Ordinal(i) + " ao " + Fmt.Ordinal(j), "camp-c1", "negrito", cor));
                l.Add(L(Fmt.Dinheiro(k.PremioPara(i)), "camp-c2", "camp-mono"));
                l.Add(L("+" + (int)k.PontosPara(i), "camp-c2", "camp-mono"));
                conteudo.Add(l);
                i = j + 1;
            }
            conteudo.Add(L("Os valores são provisórios e mudam com o tamanho do campeonato.", "legenda"));
        }

        // ------------------------------------------------------------------------------------------ participantes
        void AbaParticipantes(Campeonato k)
        {
            var topo = E("camp-topo-aba");
            topo.Add(L("Participantes ( " + k.times.Count + " )", "h2", "topo-h2"));
            conteudo.Add(topo);
            var cab = E("camp-linha-cab");
            cab.Add(L("Time", "camp-p1")); cab.Add(L("Circuito", "camp-p2")); cab.Add(L("Ranking", "camp-p3")); cab.Add(L("Força", "camp-p3")); cab.Add(L("Campanha", "camp-p3")); cab.Add(L("Situação", "camp-p2"));
            conteudo.Add(cab);
            foreach (var id in k.times.OrderBy(x => c.mundo.PosicaoRanking(x)))
            {
                var e = c.mundo.equipes[id]; var (v, d) = Campanha(k, id);
                var l = E("camp-linha"); if (id == 0) l.AddToClassList("meu");
                var nome = E("camp-p1", "linha-h"); nome.Add(Avatar(e.nome)); var nt = L(e.nome, "f16", id == 0 ? "negrito" : "t"); nt.style.marginLeft = 12; nome.Add(nt);
                l.Add(nome);
                l.Add(L(Mundo.NomeCircuito(e.circuito), "camp-p2", "suave"));
                l.Add(L("#" + c.mundo.PosicaoRanking(id), "camp-p3", "camp-mono"));
                l.Add(L(e.Forca.ToString(), "camp-p3", "camp-mono"));
                l.Add(L(k.iniciado ? v + " – " + d : "—", "camp-p3", "camp-mono"));
                string sit = k.terminado ? Fmt.Ordinal(k.Colocacao(id)) + " lugar" : !k.iniciado ? "Inscrito" : "Em disputa";
                l.Add(L(sit, "camp-p2", k.terminado && k.Colocacao(id) == 1 ? "verde" : "suave"));
                conteudo.Add(l);
            }
        }

        // ------------------------------------------------------------------------------------------ partidas
        void AbaPartidas(Campeonato k)
        {
            var lista = k.TodosConfrontos.ToList();
            if (lista.Count == 0) { conteudo.Add(L(k.iniciado ? "Nenhuma série jogada ainda." : "O campeonato ainda não começou: as séries aparecem aqui quando as rodadas forem sorteadas.", "f15", "suave", "quebra")); return; }
            var card = E("camp-card", "camp-jogos");
            string ult = null;
            foreach (var f in lista.Skip(Math.Max(0, lista.Count - 80)))
            {
                if (f.rotulo != ult) { card.Add(L(Fmt.Rotulo(k.nome, f.rotulo) + (f.md > 1 ? " · " + Fmt.Serie(f.md) : ""), "camp-rodada")); ult = f.rotulo; }
                var l = E("camp-jogo"); bool meu = f.Envolve(0); if (meu) l.AddToClassList("meu");
                l.Add(L(DataCurta(Mundo.DataDe(c.Temporada, f.semana, Mundo.DiaDaSemanaDaSerie(k.formato, f.dia))), "camp-jogo-data"));
                string ga = f.feito && f.vitA > f.vitB ? "verde" : "t", gb = f.feito && f.vitB > f.vitA ? "verde" : "t";
                l.Add(L(NomeTime(f.a), "camp-jogo-a", "f16", f.a == 0 ? "negrito" : ga));
                l.Add(L(f.feito ? f.vitA + " x " + f.vitB : "vs", "camp-jogo-placar", f.feito ? "t" : "apagado"));
                l.Add(L(NomeTime(f.b), "camp-jogo-b", "f16", f.b == 0 ? "negrito" : gb));
                var est = E("camp-jogo-est");
                if (!f.feito) est.Add(Selo(meu ? "SUA SÉRIE" : "AGUARDANDO", meu ? "laranja" : null)); else est.Add(L("Encerrada", "f13", "apagado"));
                l.Add(est);
                card.Add(l);
            }
            conteudo.Add(card);
        }

        // ------------------------------------------------------------------------------------------ classificação (liga)
        void AbaClassificacao(Campeonato k)
        {
            if (!k.iniciado) { conteudo.Add(L("A tabela aparece quando a liga começar.", "f15", "suave")); return; }
            var cab = E("camp-linha-cab");
            cab.Add(L("#", "camp-p3")); cab.Add(L("Time", "camp-p1")); cab.Add(L("Vitórias", "camp-p3")); cab.Add(L("Derrotas", "camp-p3")); cab.Add(L("Pontos", "camp-p3"));
            conteudo.Add(cab);
            int pos = 1;
            foreach (var (id, pts, vit, der) in k.TabelaLiga())
            {
                var l = E("camp-linha"); if (id == 0) l.AddToClassList("meu");
                l.Add(L(pos++.ToString("00"), "camp-p3", "camp-mono"));
                var nome = E("camp-p1", "linha-h"); nome.Add(Avatar(NomeTime(id))); var nt = L(NomeTime(id), "f16", id == 0 ? "negrito" : "t"); nt.style.marginLeft = 12; nome.Add(nt);
                l.Add(nome);
                l.Add(L(vit.ToString(), "camp-p3", "camp-mono")); l.Add(L(der.ToString(), "camp-p3", "camp-mono")); l.Add(L(pts.ToString(), "camp-p3", "camp-mono", "negrito"));
                conteudo.Add(l);
            }
        }

        // ------------------------------------------------------------------------------------------ chaves (mata-mata)
        static bool RodadaEliminatoria(string rotulo) => rotulo.EndsWith("Final") || rotulo.Contains("Semifinal") || rotulo.Contains("Quartas de final") || rotulo.Contains("Oitavas de final");

        void AbaChaves(Campeonato k)
        {
            var rodadas = new List<List<Confronto>>(); var nomes = new List<string>(); string ult = null;
            foreach (var f in k.TodosConfrontos.Where(x => RodadaEliminatoria(x.rotulo)))
            {
                if (f.rotulo != ult) { rodadas.Add(new List<Confronto>()); string r = f.rotulo; nomes.Add(r.Substring(r.LastIndexOf(" · ") + 3)); ult = f.rotulo; }
                rodadas[rodadas.Count - 1].Add(f);
            }
            if (rodadas.Count == 0)
            {
                conteudo.Add(L(k.formato == Formato.SuicoMataMata ? "As chaves aparecem quando o suíço terminar e os 8 melhores forem definidos."
                    : "As chaves aparecem quando a fase classificatória terminar. Acompanhe os jogos na aba Partidas.", "f15", "suave", "quebra"));
                return;
            }
            int n0 = rodadas[0].Count, colunas = 1; while ((1 << (colunas - 1)) < n0) colunas++;
            conteudo.Add(new Chave(this, k, rodadas, nomes, n0, colunas));
        }

        /// <summary>A chave do mata-mata: uma coluna por fase, cartões com os dois times e linhas ligando cada par à fase seguinte.</summary>
        class Chave : VisualElement
        {
            const float CW = 210f, CH = 64f, GAP = 60f, SLOT = 84f, TOPO = 54f;
            readonly int n0, colunas;
            public Chave(GerenteUI ui, Campeonato k, List<List<Confronto>> rodadas, List<string> nomes, int n0, int colunas)
            {
                this.n0 = n0; this.colunas = colunas;
                AddToClassList("camp-chave");
                style.width = colunas * CW + (colunas - 1) * GAP; style.height = TOPO + n0 * SLOT + 10;
                for (int col = 0; col < colunas; col++)
                {
                    int n = n0 >> col; float x = col * (CW + GAP);
                    string titulo = col < nomes.Count ? nomes[col] : n == 1 ? "Final" : n == 2 ? "Semifinal" : n == 4 ? "Quartas de final" : "Oitavas de final";
                    var cab = new VisualElement(); cab.AddToClassList("camp-chave-cab"); cab.style.left = x; cab.style.width = CW;
                    var t = new Label(titulo); t.AddToClassList("t"); t.AddToClassList("camp-chave-tit"); cab.Add(t);
                    var s = new Label(n + (n == 1 ? " série" : " séries")); s.AddToClassList("t"); s.AddToClassList("f13"); s.AddToClassList("apagado"); cab.Add(s);
                    Add(cab);
                    float slot = n0 * SLOT / n;
                    for (int i = 0; i < n; i++)
                    {
                        Confronto f = col < rodadas.Count && i < rodadas[col].Count ? rodadas[col][i] : null;
                        var card = ui.CartaoChave(f, k);
                        card.style.left = x; card.style.top = TOPO + i * slot + (slot - CH) / 2f;
                        Add(card);
                    }
                }
                generateVisualContent += Linhas;
                pickingMode = PickingMode.Position;
            }

            void Linhas(MeshGenerationContext mgc)
            {
                var p = mgc.painter2D; p.strokeColor = new Color(1f, 1f, 1f, 0.06f); p.lineWidth = 1.5f; p.lineJoin = LineJoin.Round;
                for (int col = 0; col < colunas - 1; col++)
                {
                    int n = n0 >> col; float slot = n0 * SLOT / n, slotProx = slot * 2f;
                    for (int i = 0; i < n; i++)
                    {
                        float y1 = TOPO + i * slot + slot / 2f, y2 = TOPO + (i / 2) * slotProx + slotProx / 2f;
                        float x1 = col * (CW + GAP) + CW, xm = x1 + GAP / 2f, x2 = x1 + GAP;
                        p.BeginPath(); p.MoveTo(new Vector2(x1, y1)); p.LineTo(new Vector2(xm, y1)); p.LineTo(new Vector2(xm, y2)); p.LineTo(new Vector2(x2, y2)); p.Stroke();
                    }
                }
            }
        }

        VisualElement CartaoChave(Confronto f, Campeonato k)
        {
            var card = E("camp-chave-card"); card.style.position = Position.Absolute; card.style.width = 210; card.style.height = 64;
            if (f != null && f.Envolve(0)) card.AddToClassList("meu");
            for (int lado = 0; lado < 2; lado++)
            {
                var l = E("camp-chave-lado");
                int id = f == null ? -1 : lado == 0 ? f.a : f.b;
                int v = f == null ? 0 : lado == 0 ? f.vitA : f.vitB, vo = f == null ? 0 : lado == 0 ? f.vitB : f.vitA;
                bool venceu = f != null && f.feito && v > vo;
                var n = L(id < 0 ? "A definir" : NomeTime(id), "camp-chave-nome", id < 0 ? "apagado" : id == 0 ? "laranja" : venceu ? "verde" : f != null && f.feito ? "suave" : "t");
                l.Add(n);
                l.Add(L(f != null && f.feito ? v.ToString() : "", "camp-chave-v", venceu ? "verde" : "suave"));
                card.Add(l);
            }
            return card;
        }

        // ------------------------------------------------------------------------------------------ leaderboard do ranking geral
        void Leaderboard()
        {
            conteudo.Add(CapaTela("Ranking", () => { calAba = 0; Mostrar(); }, "Calendário", "Ranking geral"));
            var rk = c.mundo.Ranking();
            int paginas = (rk.Count + RK_POR_PAGINA - 1) / RK_POR_PAGINA;
            if (rkPagina < 0) { int meu = rk.FindIndex(e => e.clube); rkPagina = Math.Max(0, meu) / RK_POR_PAGINA; }
            rkPagina = Math.Min(rkPagina, paginas - 1);

            var cab = E("camp-linha-cab", "rk-cab");
            cab.Add(L("Posição", "rk-c1")); cab.Add(L("Time", "rk-c2")); cab.Add(L("Elenco", "rk-c3")); cab.Add(L("Força", "rk-c4")); cab.Add(L("Pontos da temporada", "rk-c5")); cab.Add(L("Temporada anterior", "rk-c5")); cab.Add(L("Ranking", "rk-c4"));
            conteudo.Add(cab);
            for (int i = rkPagina * RK_POR_PAGINA; i < Math.Min(rk.Count, (rkPagina + 1) * RK_POR_PAGINA); i++)
            {
                var e = rk[i];
                var l = E("camp-linha", "rk-lin"); if (e.clube) l.AddToClassList("meu");
                l.Add(L((i + 1).ToString("00"), "rk-c1", "camp-mono", i < 3 ? "laranja" : "t"));
                var time = E("rk-c2", "linha-h"); time.Add(Avatar(e.nome));
                var tx = E(); tx.style.marginLeft = 12;
                tx.Add(L(Mundo.NomeCircuito(e.circuito), "f12", e.circuito == Circuito.Elite ? "laranja" : e.circuito == Circuito.Pro ? "azul" : "apagado"));
                tx.Add(L(e.nome, "f16", e.clube ? "negrito" : "t")); time.Add(tx); l.Add(time);
                var elenco = E("rk-c3", "linha-h");
                foreach (var (nome, _) in e.jogadores.Take(5)) { var av = Avatar(nome); av.AddToClassList("rk-av"); elenco.Add(av); }
                l.Add(elenco);
                l.Add(L(e.Forca.ToString(), "rk-c4", "camp-mono"));
                l.Add(L(((int)e.pontos).ToString(), "rk-c5", "camp-mono")); l.Add(L(((int)e.pontosAnt).ToString(), "rk-c5", "camp-mono"));
                l.Add(L(((int)e.PontosRanking).ToString(), "rk-c4", "camp-mono", "negrito"));
                conteudo.Add(l);
            }

            // páginas
            var pg = E("camp-paginas");
            for (int p = 0; p < paginas; p++) { int pp = p; var b = E("camp-pag"); if (p == rkPagina) b.AddToClassList("on"); b.Add(L((p + 1).ToString())); Clicavel(b, () => { rkPagina = pp; Mostrar(); }); pg.Add(b); }
            conteudo.Add(pg);
            conteudo.Add(L("Os 16 primeiros do ranking jogam o circuito Elite, do 17º ao 32º o Pro e os demais o Amador. No fim da temporada o ranking decide quem sobe e quem desce.", "legenda"));
        }
    }
}
