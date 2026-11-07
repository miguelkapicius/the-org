using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Sim;

namespace CSM.Manager
{
    // Dados do salvamento (só campos públicos simples, para o JsonUtility da Unity e para o System.Text.Json dos testes) e a
    // conversão de/para a Carreira. A leitura e escrita do arquivo ficam em CSM.ManagerUI.Salvamento (usa a Unity).
    //
    // Os campeonatos NÃO são salvos por inteiro: guarda-se a semente e os resultados das séries do clube, e o resto é refeito
    // (a simulação dos outros times é determinística a partir da semente). A sequência de sorteios da Carreira não é salva.

    [Serializable] public class SkillD { public float reaction, aimSigma, flick, spray, crosshair, pitchErr, aggression, composure, utilitaria; }
    [Serializable] public class JogadorD { public string nome; public int idade, funcao, potencial, foco, sessoes; public bool revelado; public float cansaco, preparo; public SkillD skill; public float[] inicio;
        public bool temPersonalidade; public int traco; public float moral, entrosamento;
        public bool temLados; public float ladoTR = 50, ladoCT = 50; public int ladoFoco = -1;   // decisão 0024; salvamento antigo = neutro nos dois lados
        public int mvps, mvpsSerie; public List<string> jogadas = new List<string>(); }   // decisão 0020   // decisão 0019 (salvamentos antigos: temPersonalidade = false)
    [Serializable] public class LancD { public int semana, valor; public string descricao; }
    [Serializable] public class DesempD { public string nome; public int funcao, k, d, hs, dano, fk, rounds; }
    [Serializable] public class MapaSerieD { public string mapa; public int meus, deles; }
    [Serializable]
    public class ResultadoD
    {
        public int numPartida, semana, meus, deles, saldoAntes, forcaMinha, forcaDeles, serieMeus, serieDeles, serieMd;
        public string adversario, campeonato, rotulo, mapa; public bool venceu, assistida, estimado, serieEncerrada, venceuSerie;
        public string jogada, mvpSerie; public bool jogadaNossa;   // decisão 0020
        public List<DesempD> desempenho = new List<DesempD>(); public List<MapaSerieD> mapasDaSerie = new List<MapaSerieD>();
    }
    [Serializable] public class TituloD { public string nome; public int posicao, times, premio, semana; public float pontos; }
    [Serializable] public class ScoutD { public int tipo, alvo; }
    [Serializable] public class IntelD { public int id; public float semanas; }
    [Serializable] public class EmprestimoD { public int principal, parcela, restantes; }
    [Serializable] public class JogAdvD { public string nome; public int funcao; }
    [Serializable] public class EquipeD { public int id, estilo, circuito; public string nome; public float forca, pontos, pontosAnt; public int[] afinidade; public List<JogAdvD> jogadores = new List<JogAdvD>(); }
    [Serializable] public class ResSerieD { public int a, b; }
    [Serializable]
    public class CampeonatoD
    {
        public int id, formato, circuito, semanaInicio, premio, seed; public string nome; public bool major, iniciado, apurado; public float peso;
        public List<int> times = new List<int>(); public List<ResSerieD> resultados = new List<ResSerieD>();
    }
    [Serializable] public class PassoD { public bool pick; public int quem, mapa; }
    [Serializable] public class SerieD { public int campId, md, vetoI; public List<PassoD> passos = new List<PassoD>(); public List<ResSerieD> resultados = new List<ResSerieD>(); }

    [Serializable]
    public class CarreiraD
    {
        public int versao = 2, seed, dinheiro, numPartida, nivelGH, estilo, coletivas, estudoMapa, semanasNegativo, semana, temporada, proxId, mundoSeed;
        public bool falido, temSerie; public string clube;
        public List<JogadorD> elenco = new List<JogadorD>(), mercado = new List<JogadorD>();
        public List<LancD> extrato = new List<LancD>(); public List<ResultadoD> historico = new List<ResultadoD>(); public List<TituloD> titulos = new List<TituloD>();
        public List<string> diario = new List<string>(); public List<ScoutD> scouts = new List<ScoutD>(); public List<IntelD> intel = new List<IntelD>(); public List<EmprestimoD> emprestimos = new List<EmprestimoD>();
        public List<EquipeD> equipes = new List<EquipeD>(); public List<CampeonatoD> campeonatos = new List<CampeonatoD>();
        public SerieD serie = new SerieD();
        public List<string> favoritasTR = new List<string>(), proibidasTR = new List<string>(), favoritasCT = new List<string>(), proibidasCT = new List<string>();   // decisão 0022
    }

    public static class SalvamentoDados
    {
        public const int VERSAO = 2;

        static SkillD Para(Skill s) => new SkillD { reaction = s.reaction, aimSigma = s.aimSigma, flick = s.flick, spray = s.spray, crosshair = s.crosshair, pitchErr = s.pitchErr, aggression = s.aggression, composure = s.composure, utilitaria = s.utilitaria };
        static Skill De(SkillD d) => new Skill { reaction = d.reaction, aimSigma = d.aimSigma, flick = d.flick, spray = d.spray, crosshair = d.crosshair, pitchErr = d.pitchErr, aggression = d.aggression, composure = d.composure, utilitaria = d.utilitaria };
        static JogadorD Para(Jogador j) => new JogadorD { nome = j.nome, idade = j.idade, funcao = (int)j.funcao, potencial = j.potencial, foco = j.foco, sessoes = j.sessoes, revelado = j.potencialRevelado, cansaco = j.cansaco, preparo = j.preparo, skill = Para(j.skill), inicio = (float[])j.inicioTemporada.Clone(),
            temPersonalidade = true, traco = (int)j.traco, moral = j.moral, entrosamento = j.entrosamento,
            temLados = true, ladoTR = j.ladoTR, ladoCT = j.ladoCT, ladoFoco = j.ladoFoco,
            mvps = j.mvps, mvpsSerie = j.mvpsSerie, jogadas = j.jogadas.ToList() };
        static Jogador De(JogadorD d)
        {
            var j = new Jogador { nome = d.nome, idade = d.idade, funcao = (Role)d.funcao, potencial = d.potencial, foco = d.foco, sessoes = d.sessoes, potencialRevelado = d.revelado, cansaco = d.cansaco, preparo = d.preparo, skill = De(d.skill), inicioTemporada = d.inicio != null && d.inicio.Length == 5 ? (float[])d.inicio.Clone() : new float[5] };
            if (d.temPersonalidade) { j.traco = (Traco)d.traco; j.moral = d.moral; j.entrosamento = d.entrosamento; }   // antes da 0019: valores iniciais, sem traço
            if (d.temLados) { j.ladoTR = d.ladoTR; j.ladoCT = d.ladoCT; j.ladoFoco = d.ladoFoco; }
            j.mvps = d.mvps; j.mvpsSerie = d.mvpsSerie; if (d.jogadas != null) j.jogadas.AddRange(d.jogadas);
            return j;
        }

        public static CarreiraD Para(Carreira c)
        {
            var d = new CarreiraD
            {
                seed = c.Seed, dinheiro = c.dinheiro, numPartida = c.numPartida, nivelGH = c.nivelGH, estilo = (int)c.estilo, coletivas = c.sessoesColetivas, estudoMapa = c.estudoMapa,
                semanasNegativo = c.semanasNegativo, falido = c.falido, clube = c.clube, semana = c.mundo.semana, temporada = c.mundo.temporada, proxId = c.mundo.ProxId, mundoSeed = c.mundo.Seed,
            };
            d.elenco = c.elenco.Select(Para).ToList(); d.mercado = c.mercado.Select(Para).ToList();
            d.favoritasTR = c.favoritasTR.ToList(); d.proibidasTR = c.proibidasTR.ToList(); d.favoritasCT = c.favoritasCT.ToList(); d.proibidasCT = c.proibidasCT.ToList();
            d.extrato = c.extrato.Select(l => new LancD { semana = l.semana, valor = l.valor, descricao = l.descricao }).ToList();
            d.historico = c.historico.Select(r => new ResultadoD
            {
                numPartida = r.numPartida, semana = r.semana, meus = r.meus, deles = r.deles, saldoAntes = r.saldoAntes, forcaMinha = r.forcaMinha, forcaDeles = r.forcaDeles, serieMeus = r.serieMeus, serieDeles = r.serieDeles, serieMd = r.serieMd,
                adversario = r.adversario, campeonato = r.campeonato, rotulo = r.rotulo, mapa = r.mapa, venceu = r.venceu, assistida = r.assistida, estimado = r.estimado, serieEncerrada = r.serieEncerrada, venceuSerie = r.venceuSerie,
                jogada = r.jogada, jogadaNossa = r.jogadaNossa, mvpSerie = r.mvpSerie,
                desempenho = r.desempenho.Select(p => new DesempD { nome = p.nome, funcao = (int)p.funcao, k = p.k, d = p.d, hs = p.hs, dano = p.dano, fk = p.fk, rounds = p.rounds }).ToList(),
                mapasDaSerie = r.mapasDaSerie.Select(m => new MapaSerieD { mapa = m.mapa, meus = m.meus, deles = m.deles }).ToList(),
            }).ToList();
            d.titulos = c.titulos.Select(t => new TituloD { nome = t.nome, posicao = t.posicao, times = t.times, premio = t.premio, semana = t.semana, pontos = t.pontos }).ToList();
            d.diario = c.diario.ToList();
            d.scouts = c.scouts.Select(s => new ScoutD { tipo = s.tipo, alvo = s.alvo }).ToList();
            d.intel = c.intel.Select(kv => new IntelD { id = kv.Key, semanas = kv.Value }).ToList();
            d.emprestimos = c.emprestimos.Select(e => new EmprestimoD { principal = e.principal, parcela = e.parcela, restantes = e.restantes }).ToList();
            d.equipes = c.mundo.equipes.Select(e => new EquipeD
            {
                id = e.id, nome = e.nome, forca = e.forca, estilo = (int)e.estilo, circuito = (int)e.circuito, pontos = e.pontos, pontosAnt = e.pontosAnt, afinidade = (int[])e.afinidade.Clone(),
                jogadores = e.jogadores.Select(j => new JogAdvD { nome = j.nome, funcao = (int)j.funcao }).ToList(),
            }).ToList();
            d.campeonatos = c.mundo.campeonatos.Select(k => new CampeonatoD
            {
                id = k.id, nome = k.nome, formato = (int)k.formato, circuito = (int)k.circuito, semanaInicio = k.semanaInicio, premio = k.premio, seed = k.Seed, major = k.major, iniciado = k.iniciado, apurado = k.apurado, peso = k.pesoRanking,
                times = k.times.ToList(), resultados = k.resultadosClube.Select(r => new ResSerieD { a = r.vitA, b = r.vitB }).ToList(),
            }).ToList();
            if (c.serie != null)
            {
                d.temSerie = true;
                d.serie = new SerieD
                {
                    campId = c.serie.camp.id, md = c.serie.Md, vetoI = c.serie.veto.i,
                    passos = c.serie.veto.passos.Select(p => new PassoD { pick = p.pick, quem = p.quem, mapa = p.mapa }).ToList(),
                    resultados = c.serie.resultados.Select(r => new ResSerieD { a = r.meus, b = r.deles }).ToList(),
                };
            }
            return d;
        }

        /// <summary>Reconstrói a carreira; null se o arquivo é de outra versão ou está incompleto.</summary>
        public static Carreira De(CarreiraD d)
        {
            if (d == null || d.versao != VERSAO || d.elenco == null || d.elenco.Count < Provisorio.TITULARES || d.equipes == null || d.equipes.Count < 2) return null;
            var c = Carreira.ParaRestaurar(d.seed, d.numPartida);
            c.clube = d.clube; c.dinheiro = d.dinheiro; c.numPartida = d.numPartida; c.nivelGH = d.nivelGH; c.estilo = (Estilo)d.estilo; c.sessoesColetivas = d.coletivas; c.estudoMapa = d.estudoMapa;
            if (d.favoritasTR != null) c.favoritasTR.AddRange(d.favoritasTR); if (d.proibidasTR != null) c.proibidasTR.AddRange(d.proibidasTR);
            if (d.favoritasCT != null) c.favoritasCT.AddRange(d.favoritasCT); if (d.proibidasCT != null) c.proibidasCT.AddRange(d.proibidasCT);
            c.semanasNegativo = d.semanasNegativo; c.falido = d.falido;
            c.elenco.AddRange(d.elenco.Select(De)); c.mercado.AddRange(d.mercado.Select(De));
            c.extrato.AddRange(d.extrato.Select(l => new Lancamento { semana = l.semana, valor = l.valor, descricao = l.descricao }));
            c.historico.AddRange(d.historico.Select(r => new Resultado
            {
                numPartida = r.numPartida, semana = r.semana, meus = r.meus, deles = r.deles, saldoAntes = r.saldoAntes, forcaMinha = r.forcaMinha, forcaDeles = r.forcaDeles, serieMeus = r.serieMeus, serieDeles = r.serieDeles, serieMd = r.serieMd,
                adversario = r.adversario, campeonato = r.campeonato, rotulo = r.rotulo, mapa = r.mapa, venceu = r.venceu, assistida = r.assistida, estimado = r.estimado, serieEncerrada = r.serieEncerrada, venceuSerie = r.venceuSerie,
                jogada = string.IsNullOrEmpty(r.jogada) ? null : r.jogada, jogadaNossa = r.jogadaNossa, mvpSerie = string.IsNullOrEmpty(r.mvpSerie) ? null : r.mvpSerie,
                desempenho = r.desempenho.Select(p => new Desempenho { nome = p.nome, funcao = (Role)p.funcao, k = p.k, d = p.d, hs = p.hs, dano = p.dano, fk = p.fk, rounds = p.rounds }).ToList(),
                mapasDaSerie = r.mapasDaSerie.Select(m => (m.mapa, m.meus, m.deles)).ToList(),
            }));
            c.titulos.AddRange(d.titulos.Select(t => new ResultadoCampeonato { nome = t.nome, posicao = t.posicao, times = t.times, premio = t.premio, semana = t.semana, pontos = t.pontos }));
            c.diario.AddRange(d.diario);
            c.scouts.AddRange(d.scouts.Select(s => new Scout { tipo = s.tipo, alvo = s.alvo }));
            foreach (var i in d.intel) c.intel[i.id] = i.semanas;
            c.emprestimos.AddRange(d.emprestimos.Select(e => new Emprestimo { principal = e.principal, parcela = e.parcela, restantes = e.restantes }));

            var m = Mundo.ParaRestaurar(d.mundoSeed, d.temporada);
            foreach (var e in d.equipes.OrderBy(e => e.id))
                m.equipes.Add(new Equipe
                {
                    id = e.id, nome = e.nome, forca = e.forca, estilo = (Estilo)e.estilo, circuito = (Circuito)e.circuito, pontos = e.pontos, pontosAnt = e.pontosAnt, clube = e.id == 0,
                    afinidade = e.afinidade != null && e.afinidade.Length == Mapas.TOTAL ? (int[])e.afinidade.Clone() : Enumerable.Repeat(50, Mapas.TOTAL).ToArray(),
                    jogadores = e.jogadores.Select(j => (j.nome, (Role)j.funcao)).ToList(),
                });
            m.semana = d.semana; m.temporada = d.temporada; m.ProxId = d.proxId;
            foreach (var k in d.campeonatos)
            {
                var camp = new Campeonato(k.id, k.nome, (Formato)k.formato, (Circuito)k.circuito, k.semanaInicio, k.times, k.premio, k.peso, k.seed, k.major);
                m.Adicionar(camp);
                if (k.iniciado) { camp.iniciado = true; camp.Reproduzir(k.times.Contains(0) ? 0 : -1, k.resultados.Select(r => (r.a, r.b))); }
                camp.apurado = k.apurado;
            }
            c.mundo = m;

            if (d.temSerie)
            {
                var camp = m.campeonatos.FirstOrDefault(x => x.id == d.serie.campId);
                var conf = camp?.PendenteDoClube;
                if (conf != null)
                {
                    int primeiro = d.serie.passos.Count > 0 ? d.serie.passos[0].quem : 0;
                    var veto = new Veto(d.serie.md, primeiro);
                    for (int i = 0; i < veto.passos.Count && i < d.serie.passos.Count; i++)
                    {
                        veto.passos[i].mapa = d.serie.passos[i].mapa;
                        if (veto.passos[i].mapa >= 0) veto.restantes.Remove(veto.passos[i].mapa);
                    }
                    veto.i = d.serie.vetoI;
                    var s = new Serie { camp = camp, conf = conf, adv = m.equipes[conf.a == 0 ? conf.b : conf.a], veto = veto };
                    if (veto.Completo) s.mapas = veto.OrdemDeJogo();
                    s.resultados.AddRange(d.serie.resultados.Select(r => (r.a, r.b)));
                    c.serie = s;
                }
            }
            return c;
        }
    }
}
