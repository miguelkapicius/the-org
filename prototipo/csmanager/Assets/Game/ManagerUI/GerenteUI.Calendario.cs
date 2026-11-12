using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Calendário (decisão 0012): uma grade de mês de verdade, com datas e dias da semana, onde aparecem os campeonatos do clube e as
    /// séries de cada um. A lista de campeonatos, o detalhe de cada um e o ranking ficam em abas à parte para a tela principal ficar limpa.</summary>
    public partial class GerenteUI
    {
        Campeonato campAberto;   // campeonato aberto na tela de calendário (null = as abas)
        int calAba;              // 0 calendário, 1 campeonatos, 2 ranking
        int mesCal = -1;         // mês mostrado (0..11); -1 = o mês da semana atual

        static readonly string[] MESES = { "Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro" };
        static readonly string[] DIAS_SEMANA = { "Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom" };

        string NomeTime(int id) => c.mundo.equipes[id].nome;

        /// <summary>"qui, 5 mar": a data por extenso curto (sem depender da cultura do computador).</summary>
        static string DataCurta(DateTime d) => DIAS_SEMANA[((int)d.DayOfWeek + 6) % 7].ToLowerInvariant() + ", " + d.Day + " " + MESES[d.Month - 1].Substring(0, 3).ToLowerInvariant();

        (string texto, string cor) StatusCampeonato(Campeonato k)
        {
            if (k.terminado)
            {
                var t = c.titulos.FirstOrDefault(x => x.nome == k.nome);
                return (t != null ? Fmt.Ordinal(t.posicao) + " lugar · " + Fmt.Dinheiro(t.premio) : "Encerrado", t != null && t.posicao <= 3 ? "verde" : "t");
            }
            if (k.iniciado)
            {
                var p = k.PendenteDoClube;
                return (p != null ? "Em andamento · sua série: " + Fmt.Rotulo(k.nome, p.rotulo) : "Em andamento", p != null ? "laranja" : "suave");
            }
            return ("Começa " + Quando(k.semanaInicio, c.Semana), "suave");
        }

        void TelaCalendario()
        {
            if (campAberto != null) { DetalheCampeonato(campAberto); return; }
            conteudo.Add(Abas(new[] { "Calendário", "Campeonatos", "Ranking" }, calAba, i => { calAba = i; Mostrar(); }));
            if (c.UltimaTemporada != null)   // rever o fim da temporada passada (decisão 0040)
            {
                var ult = c.UltimaTemporada;
                var rev = BotaoMini("Rever a temporada " + ult.temporada + ": " + ult.Manchete, () => AbrirFimDeTemporada(ult), false, true, "trophy");
                rev.style.alignSelf = Align.FlexStart; rev.style.marginBottom = 12;
                conteudo.Add(rev);
            }
            if (calAba == 1) ListaCampeonatos();
            else if (calAba == 2) Leaderboard();
            else CalendarioMensal();
        }

        // ================================================================================================ grade de mês
        void CalendarioMensal()
        {
            int temp = c.Temporada, ano = Mundo.AnoDaTemporada(temp);
            var hojeSemana = Mundo.DataDe(temp, c.Semana, 0);   // a segunda da semana atual
            if (mesCal < 0) mesCal = hojeSemana.Year == ano ? hojeSemana.Month - 1 : 0;
            mesCal = Math.Max(0, Math.Min(11, mesCal));
            var meus = c.CampeonatosDoClube.ToList();

            // o que cai em cada data: as séries do clube (de cada campeonato) e as semanas de cada campeonato
            var porData = new Dictionary<DateTime, List<(Campeonato k, Confronto f)>>();
            foreach (var k in meus)
                foreach (var f in k.TodosConfrontos.Where(f => f.Envolve(0)))
                {
                    var dt = Mundo.DataDe(temp, f.semana, Mundo.DiaDaSemanaDaSerie(k.formato, f.dia));
                    if (!porData.ContainsKey(dt)) porData[dt] = new List<(Campeonato, Confronto)>();
                    porData[dt].Add((k, f));
                }
            var porSemana = new Dictionary<int, List<Campeonato>>();
            foreach (var k in meus) for (int w = k.semanaInicio; w <= k.SemanaFim; w++) { if (!porSemana.ContainsKey(w)) porSemana[w] = new List<Campeonato>(); porSemana[w].Add(k); }

            // cabeçalho: mês, navegação e a semana atual
            var topo = E("cal-topo");
            var nav = E("cal-nav");
            nav.Add(BotaoIcone("up", () => { mesCal = Math.Max(0, mesCal - 1); Mostrar(); }, mesCal > 0).WithClass("gira-esq"));
            var titulo = L(MESES[mesCal] + " " + ano, "cal-mes"); nav.Add(titulo);
            nav.Add(BotaoIcone("down", () => { mesCal = Math.Min(11, mesCal + 1); Mostrar(); }, mesCal < 11).WithClass("gira-dir"));
            topo.Add(nav);
            var dir = E("linha-h");
            dir.Add(L("Semana " + c.Semana + " de " + Mundo.SEMANAS_POR_TEMPORADA + " · temporada " + temp, "f15", "suave")); dir.Children().First().style.marginRight = 14;
            dir.Add(BotaoMini("Hoje", () => { mesCal = -1; Mostrar(); }, false, true, "calendar"));
            topo.Add(dir);
            conteudo.Add(topo);

            var layout = E("duas-colunas");
            var esquerda = E("grow");
            esquerda.Add(Grade(ano, temp, hojeSemana, porData, porSemana));
            var leg = E("legenda-cal");
            foreach (var (txt, cls) in new[] { ("série marcada", "pend"), ("vitória", "venceu"), ("derrota", "perdeu") }) { var ch = E("cal-chip", cls); ch.Add(L(txt)); leg.Add(ch); }
            var faixa = E("cal-chip", "camp-leg"); faixa.Add(L("semanas de campeonato")); leg.Add(faixa);
            leg.Add(L("A semana atual tem a borda em destaque. As próximas rodadas aparecem conforme o campeonato avança.", "f13", "apagado"));
            esquerda.Add(leg);
            layout.Add(esquerda);
            layout.Add(ProximosCompromissos(temp));
            conteudo.Add(layout);
        }

        VisualElement Grade(int ano, int temp, DateTime hojeSemana, Dictionary<DateTime, List<(Campeonato k, Confronto f)>> porData, Dictionary<int, List<Campeonato>> porSemana)
        {
            var grade = E("cal-grade");
            var cab = E("cal-dias-sem");
            foreach (var d in DIAS_SEMANA) cab.Add(L(d.ToUpperInvariant()));
            grade.Add(cab);

            var primeiro = new DateTime(ano, mesCal + 1, 1);
            int deslocamento = ((int)primeiro.DayOfWeek + 6) % 7;          // segunda = 0
            var inicioGrade = primeiro.AddDays(-deslocamento);
            int linhas = (deslocamento + DateTime.DaysInMonth(ano, mesCal + 1) + 6) / 7;
            for (int r = 0; r < linhas; r++)
            {
                var linha = E("cal-semana");
                var segunda = inicioGrade.AddDays(r * 7);
                bool noTemporada = Mundo.SemanaDeData(temp, segunda, out int sem, out _);
                if (noTemporada && sem == c.Semana) linha.AddToClassList("atual");
                for (int i = 0; i < 7; i++)
                {
                    var data = segunda.AddDays(i);
                    bool dentro = Mundo.SemanaDeData(temp, data, out int s2, out _);
                    var cel = E("cal-dia");
                    if (data.Month != mesCal + 1) cel.AddToClassList("fora");
                    if (dentro && porSemana.ContainsKey(s2)) cel.AddToClassList("camp");
                    if (dentro && s2 < c.Semana) cel.AddToClassList("passada");
                    var num = E("linha-h"); num.Add(L(data.Day.ToString(), "cal-num"));
                    // no 1º dia de cada campeonato (a segunda da semana em que começa), o nome dele
                    if (dentro && i == 0 && porSemana.ContainsKey(s2))
                        foreach (var k in porSemana[s2].Where(k => k.semanaInicio == s2).Take(1)) { var tag = L(k.nome, "cal-tag"); var kk = k; Clicavel(tag, () => { campAberto = kk; Mostrar(); }); tag.pickingMode = PickingMode.Position; num.Add(tag); }
                    cel.Add(num);
                    if (porData.TryGetValue(data, out var jogos))
                    {
                        foreach (var (k, f) in jogos.Take(2))
                        {
                            var adv = f.a == 0 ? f.b : f.a;
                            string cls = !f.feito ? "pend" : ((f.a == 0 ? f.vitA > f.vitB : f.vitB > f.vitA) ? "venceu" : "perdeu");
                            string res = f.feito ? " " + (f.a == 0 ? f.vitA + "x" + f.vitB : f.vitB + "x" + f.vitA) : "";
                            var chip = E("cal-chip", cls); chip.Add(L("vs " + NomeTime(adv) + res));
                            var kk = k; Clicavel(chip, () => { campAberto = kk; Mostrar(); });
                            ComDica(chip, k.nome + " · " + Fmt.Rotulo(k.nome, f.rotulo) + " · " + Fmt.Serie(f.md) + (f.feito ? " · terminou " + (f.a == 0 ? f.vitA + " x " + f.vitB : f.vitB + " x " + f.vitA) : " · " + DataCurta(data)));
                            cel.Add(chip);
                        }
                        if (jogos.Count > 2) cel.Add(L("+" + (jogos.Count - 2) + " série(s)", "f12", "apagado"));
                    }
                    linha.Add(cel);
                }
                grade.Add(linha);
            }
            return grade;
        }

        VisualElement ProximosCompromissos(int temp)
        {
            var lat = E("cal-lateral");
            lat.Add(L("PRÓXIMOS COMPROMISSOS", "treino-card-tit"));
            var itens = new List<(DateTime data, string titulo, string sub, Campeonato k)>();
            foreach (var k in c.CampeonatosDoClube.Where(x => !x.terminado))
            {
                var p = k.PendenteDoClube;
                if (k.iniciado && p != null) itens.Add((Mundo.DataDe(temp, p.semana, Mundo.DiaDaSemanaDaSerie(k.formato, p.dia)), "Série contra " + NomeTime(p.a == 0 ? p.b : p.a), k.nome + " · " + Fmt.Serie(p.md), k));
                else if (!k.iniciado) itens.Add((Mundo.DataDe(temp, k.semanaInicio, Mundo.DiaDaSemanaDaSerie(k.formato, 0)), "Começa: " + k.nome, NomeFormato(k.formato), k));
            }
            foreach (var (data, titulo, sub, k) in itens.OrderBy(x => x.data).Take(6))
            {
                var it = E("prox-item");
                it.Add(L(DataCurta(data), "f13", "negrito", "laranja"));
                it.Add(L(titulo, "f16", "negrito", "quebra"));
                it.Add(L(sub, "f13", "suave", "quebra"));
                var kk = k; Clicavel(it, () => { campAberto = kk; Mostrar(); });
                lat.Add(it);
            }
            if (itens.Count == 0) lat.Add(L("Nenhum campeonato pela frente nesta temporada.", "f14", "suave", "quebra"));
            var pos = E("prox-resumo");
            pos.Add(L(Mundo.NomeCircuito(c.CircuitoClube) + " · ranking #" + c.PosicaoRanking + " de " + Mundo.TIMES, "f14", "suave"));
            lat.Add(pos);
            return lat;
        }

        // ================================================================================================ campeonatos e ranking (abas)
        void ListaCampeonatos()
        {
            var meus = c.CampeonatosDoClube.ToList();
            conteudo.Add(L("Campeonatos do circuito " + Mundo.NomeCircuito(c.CircuitoClube) + " em " + Mundo.AnoDaTemporada(c.Temporada), "h2", "topo-h2"));
            foreach (var k in meus)
            {
                var (st, cor) = StatusCampeonato(k);
                var l = E("cal-linha"); if (k.iniciado && !k.terminado && k.PendenteDoClube != null && c.TemJogoAgora) l.AddToClassList("agora");
                var sem = E("cal-sem"); sem.Add(L(k.semanaInicio == k.SemanaFim ? "Sem " + k.semanaInicio : "Sem " + k.semanaInicio + "–" + k.SemanaFim, "f15", "suave")); l.Add(sem);
                var nome = E("cal-nome"); nome.Add(L(k.nome, "f17", "negrito")); nome.Add(L("Premiação total " + Fmt.Dinheiro(k.premio) + (k.major ? " · MAJOR" : ""), "f13", "suave")); l.Add(nome);
                var fmt = E("cal-fmt"); fmt.Add(L(NomeFormato(k.formato), "f14")); fmt.Add(L(k.times.Count + " times", "f13", "suave")); l.Add(fmt);
                var stt = E("cal-st"); stt.Add(L(st, "f15", cor)); l.Add(stt);
                l.Add(L("Ver", "f14", "suave"));
                var kk = k; Clicavel(l, () => { campAberto = kk; Mostrar(); });
                conteudo.Add(l);
            }
        }

    }
}
