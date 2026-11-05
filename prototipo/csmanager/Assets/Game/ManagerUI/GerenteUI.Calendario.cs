using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using CSM.Manager;
using static CSM.ManagerUI.Ui;

namespace CSM.ManagerUI
{
    /// <summary>Calendário da temporada, campeonatos do clube, detalhes de cada campeonato e o ranking (decisão 0012).</summary>
    public partial class GerenteUI
    {
        Campeonato campAberto;   // campeonato aberto na tela de calendário (null = a lista)

        string NomeTime(int id) => c.mundo.equipes[id].nome;

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

            var resumos = E("resumos");
            resumos.Add(Resumo("Circuito", Mundo.NomeCircuito(c.CircuitoClube), "t"));
            resumos.Add(Resumo("Ranking", "#" + c.PosicaoRanking + " de " + Mundo.TIMES, "t"));
            resumos.Add(Resumo("Pontos de ranking", ((int)c.EquipeClube.PontosRanking).ToString(), "t"));
            conteudo.Add(resumos);

            // as 52 semanas: onde o clube joga e onde estamos
            conteudo.Add(L("A temporada em 52 semanas", "h2"));
            var meus = c.CampeonatosDoClube.ToList();
            var faixa = E("faixa-semanas");
            for (int w = 1; w <= Mundo.SEMANAS_POR_TEMPORADA; w++)
            {
                var cel = E("sem-cel");
                var nesta = meus.Where(k => w >= k.semanaInicio && w <= k.SemanaFim).ToList();
                if (nesta.Count > 0) cel.AddToClassList("meu");
                if (w < c.Semana) cel.AddToClassList("passada");
                if (w == c.Semana) cel.AddToClassList("atual");
                ComDica(cel, "Semana " + w + (w == c.Semana ? " (agora)" : "") + (nesta.Count > 0 ? ": " + string.Join(", ", nesta.Select(k => k.nome)) : ": sem campeonato do clube (treino e gestão)"));
                faixa.Add(cel);
            }
            conteudo.Add(faixa);
            var leg = E("legenda-faixa");
            leg.Add(L("Laranja: semanas com campeonato do clube")); leg.Add(L("Borda lima: a semana atual")); leg.Add(L("Semanas vazias: treino, scouts e gestão"));
            conteudo.Add(leg);

            conteudo.Add(L("Campeonatos do circuito " + Mundo.NomeCircuito(c.CircuitoClube), "h2", "topo-h2"));
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

            // ranking
            conteudo.Add(L("Ranking dos 48 times", "h2"));
            var rk = c.mundo.Ranking();
            var duas = E("duas-colunas");
            for (int col = 0; col < 2; col++)
            {
                var cl = E("coluna"); if (col == 1) cl.AddToClassList("ultima");
                for (int i = col * 8; i < col * 8 + 8; i++)
                {
                    var e = rk[i];
                    var l = E("rk-linha"); if (e.clube) l.AddToClassList("meu");
                    l.Add(L("#" + (i + 1), "rk-pos")); l.Add(L(e.nome, "rk-nome", e.clube ? "negrito" : "t"));
                    l.Add(L(Mundo.NomeCircuito(e.circuito), "f13", "suave")); var pts = L(((int)e.PontosRanking).ToString(), "rk-pts"); pts.style.marginLeft = 12; l.Add(pts);
                    cl.Add(l);
                }
                duas.Add(cl);
            }
            conteudo.Add(duas);
            if (c.PosicaoRanking > 16) conteudo.Add(L("O clube está em #" + c.PosicaoRanking + ": " + (c.PosicaoRanking <= 32 ? "faltam " + (c.PosicaoRanking - 16) + " posições para o circuito Elite." : "o circuito Pro está logo acima."), "legenda"));
            conteudo.Add(L("Os 16 primeiros do ranking jogam o circuito Elite, do 17º ao 32º o Pro e os demais o Amador. No fim da temporada o ranking decide quem sobe e quem desce.", "legenda"));
        }

        void DetalheCampeonato(Campeonato k)
        {
            var topo = E("linha-h"); topo.style.marginBottom = 14;
            var voltar = Botao("Voltar ao calendário", () => { campAberto = null; Mostrar(); }, true, false, "up"); voltar.style.marginRight = 14; topo.Add(voltar);
            conteudo.Add(topo);

            var (st, cor) = StatusCampeonato(k);
            var resumos = E("resumos");
            var rf = Resumo("Formato", NomeFormato(k.formato), "t"); rf.Children().Last().AddToClassList("menor"); resumos.Add(rf);
            resumos.Add(Resumo("Premiação total", Fmt.Dinheiro(k.premio), "verde"));
            var rs = Resumo("Situação", st, cor); rs.Children().Last().AddToClassList("menor"); resumos.Add(rs);
            conteudo.Add(resumos);

            if (!k.iniciado) { conteudo.Add(L("O campeonato ainda não começou. Participam: " + string.Join(", ", k.times.Select(NomeTime)) + ".", "f15", "suave", "quebra")); return; }

            if (k.formato == Formato.Liga)
            {
                conteudo.Add(L("Classificação", "h2"));
                var h = E("rk-linha"); h.Add(L("#", "rk-pos", "apagado")); h.Add(L("Time", "rk-nome", "apagado")); h.Add(L("V", "rk-pts", "apagado")); h.Add(L("D", "rk-pts", "apagado")); h.Add(L("Pts", "rk-pts", "apagado")); conteudo.Add(h);
                int pos = 1;
                foreach (var (id, pts, vit, der) in k.TabelaLiga())
                {
                    var l = E("rk-linha"); if (id == 0) l.AddToClassList("meu");
                    l.Add(L(pos++ + "º", "rk-pos")); l.Add(L(NomeTime(id), "rk-nome", id == 0 ? "negrito" : "t"));
                    l.Add(L(vit.ToString(), "rk-pts")); l.Add(L(der.ToString(), "rk-pts")); l.Add(L(pts.ToString(), "rk-pts", "negrito"));
                    conteudo.Add(l);
                }
            }

            conteudo.Add(L("Confrontos", "h2"));
            string ultimo = null; int mostrados = 0;
            foreach (var f in k.TodosConfrontos.Reverse().Take(60).Reverse())   // os mais recentes
            {
                if (f.rotulo != ultimo) { conteudo.Add(L(Fmt.Rotulo(k.nome, f.rotulo) + (f.md > 1 ? " · " + Fmt.Serie(f.md) : ""), "rodada-tit")); ultimo = f.rotulo; }
                var l = E("cf-linha"); bool meu = f.Envolve(0); if (meu) l.AddToClassList("meu");
                string ga = f.feito && f.vitA > f.vitB ? "verde" : "t", gb = f.feito && f.vitB > f.vitA ? "verde" : "t";
                var a = L(NomeTime(f.a), "cf-a", "f16", f.a == 0 ? "negrito" : ga); var b = L(NomeTime(f.b), "cf-b", "f16", f.b == 0 ? "negrito" : gb);
                l.Add(a); l.Add(L(f.feito ? f.vitA + " x " + f.vitB : "—", "cf-placar", f.feito ? "t" : "laranja")); l.Add(b);
                conteudo.Add(l); mostrados++;
            }
            if (mostrados == 0) conteudo.Add(L("Nenhum confronto ainda.", "f15", "suave"));

            if (k.terminado)
            {
                conteudo.Add(L("Colocação final", "h2"));
                for (int i = 0; i < Math.Min(8, k.colocacao.Count); i++)
                {
                    var id = k.colocacao[i];
                    var l = E("rk-linha"); if (id == 0) l.AddToClassList("meu");
                    l.Add(L(Fmt.Ordinal(i + 1), "rk-pos")); l.Add(L(NomeTime(id), "rk-nome", id == 0 ? "negrito" : "t"));
                    l.Add(L(Fmt.Dinheiro(k.PremioPara(i + 1)), "f15", "verde")); var pts = L("+" + (int)k.PontosPara(i + 1), "rk-pts"); pts.style.marginLeft = 14; l.Add(pts);
                    conteudo.Add(l);
                }
            }
        }
    }
}
