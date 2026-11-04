using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using CSM.Manager;
using CSM.Sim;

namespace CSM.ManagerUI
{
    /// <summary>
    /// Salva e carrega a carreira em disco (JSON em Application.persistentDataPath). O GDD ainda não definiu o salvamento
    /// (gdd/09-tecnico.md): isto é o mínimo para o protótipo não perder a carreira ao fechar. O estado do clube é salvo por
    /// inteiro; a sequência do sorteio não (recomeça de uma semente derivada), então "recarregar" não repete sorteios.
    /// </summary>
    public static class Salvamento
    {
        public static string Caminho => Path.Combine(Application.persistentDataPath, "carreira.json");
        public static bool Existe => File.Exists(Caminho);

        [Serializable] class SkillD { public float reaction, aimSigma, flick, spray, crosshair, pitchErr, aggression, composure, utilitaria; }
        [Serializable] class JogadorD { public string nome; public int idade, funcao; public SkillD skill; }
        [Serializable] class LancD { public int numPartida, valor; public string descricao; }
        [Serializable] class DesempD { public string nome; public int funcao, k, d, hs, dano, fk, rounds; }
        [Serializable]
        class ResultD
        {
            public int numPartida, meus, deles, premio, salarios, saldoAntes, forcaMinha, forcaDeles;
            public string adversario; public bool venceu, assistida, estimado; public List<DesempD> desempenho = new List<DesempD>();
        }
        [Serializable] class JogAdvD { public string nome; public int funcao; }
        [Serializable]
        class CarreiraD
        {
            public int versao = 1, seed, dinheiro, numPartida, nivelGH; public string clube;
            public List<JogadorD> elenco = new List<JogadorD>(), mercado = new List<JogadorD>();
            public List<LancD> extrato = new List<LancD>(); public List<ResultD> historico = new List<ResultD>();
            public string advNome; public int advForca; public List<JogAdvD> advJogadores = new List<JogAdvD>();
        }

        static SkillD Para(Skill s) => new SkillD { reaction = s.reaction, aimSigma = s.aimSigma, flick = s.flick, spray = s.spray, crosshair = s.crosshair, pitchErr = s.pitchErr, aggression = s.aggression, composure = s.composure, utilitaria = s.utilitaria };
        static Skill De(SkillD d) => new Skill { reaction = d.reaction, aimSigma = d.aimSigma, flick = d.flick, spray = d.spray, crosshair = d.crosshair, pitchErr = d.pitchErr, aggression = d.aggression, composure = d.composure, utilitaria = d.utilitaria };
        static JogadorD Para(Jogador j) => new JogadorD { nome = j.nome, idade = j.idade, funcao = (int)j.funcao, skill = Para(j.skill) };
        static Jogador De(JogadorD d) => new Jogador { nome = d.nome, idade = d.idade, funcao = (Role)d.funcao, skill = De(d.skill) };

        public static void Salvar(Carreira c)
        {
            try
            {
                var d = new CarreiraD { seed = c.Seed, dinheiro = c.dinheiro, numPartida = c.numPartida, nivelGH = c.nivelGH, clube = c.clube, advNome = c.proximo.nome, advForca = c.proximo.forca };
                d.elenco = c.elenco.Select(Para).ToList(); d.mercado = c.mercado.Select(Para).ToList();
                d.extrato = c.extrato.Select(l => new LancD { numPartida = l.numPartida, valor = l.valor, descricao = l.descricao }).ToList();
                d.advJogadores = c.proximo.jogadores.Select(x => new JogAdvD { nome = x.nome, funcao = (int)x.funcao }).ToList();
                d.historico = c.historico.Select(r => new ResultD
                {
                    numPartida = r.numPartida, meus = r.meus, deles = r.deles, premio = r.premio, salarios = r.salarios, saldoAntes = r.saldoAntes, forcaMinha = r.forcaMinha, forcaDeles = r.forcaDeles,
                    adversario = r.adversario, venceu = r.venceu, assistida = r.assistida, estimado = r.estimado,
                    desempenho = r.desempenho.Select(p => new DesempD { nome = p.nome, funcao = (int)p.funcao, k = p.k, d = p.d, hs = p.hs, dano = p.dano, fk = p.fk, rounds = p.rounds }).ToList(),
                }).ToList();
                Directory.CreateDirectory(Application.persistentDataPath);
                string tmp = Caminho + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(d));
                if (File.Exists(Caminho)) File.Delete(Caminho);
                File.Move(tmp, Caminho);   // escreve num arquivo temporário e troca: uma queda no meio não corrompe o salvamento
            }
            catch (Exception e) { Debug.LogWarning("Não foi possível salvar a carreira: " + e.Message); }
        }

        /// <summary>Carrega a carreira salva; null se não houver ou se o arquivo estiver ilegível.</summary>
        public static Carreira Carregar()
        {
            try
            {
                if (!Existe) return null;
                var d = JsonUtility.FromJson<CarreiraD>(File.ReadAllText(Caminho));
                if (d == null || d.elenco == null || d.elenco.Count < Provisorio.TITULARES || string.IsNullOrEmpty(d.advNome)) return null;
                var c = Carreira.ParaRestaurar(d.seed, d.numPartida);
                c.clube = d.clube; c.dinheiro = d.dinheiro; c.numPartida = d.numPartida; c.nivelGH = d.nivelGH;
                c.elenco.AddRange(d.elenco.Select(De)); c.mercado.AddRange(d.mercado.Select(De));
                c.extrato.AddRange(d.extrato.Select(l => new Lancamento { numPartida = l.numPartida, valor = l.valor, descricao = l.descricao }));
                c.proximo = new Adversario { nome = d.advNome, forca = d.advForca };
                foreach (var x in d.advJogadores) c.proximo.jogadores.Add((x.nome, (Role)x.funcao));
                foreach (var r in d.historico)
                    c.historico.Add(new Resultado
                    {
                        numPartida = r.numPartida, meus = r.meus, deles = r.deles, premio = r.premio, salarios = r.salarios, saldoAntes = r.saldoAntes, forcaMinha = r.forcaMinha, forcaDeles = r.forcaDeles,
                        adversario = r.adversario, venceu = r.venceu, assistida = r.assistida, estimado = r.estimado,
                        desempenho = r.desempenho.Select(p => new Desempenho { nome = p.nome, funcao = (Role)p.funcao, k = p.k, d = p.d, hs = p.hs, dano = p.dano, fk = p.fk, rounds = p.rounds }).ToList(),
                    });
                return c;
            }
            catch (Exception e) { Debug.LogWarning("Salvamento ilegível, começando uma carreira nova: " + e.Message); return null; }
        }

        public static void Apagar() { try { if (Existe) File.Delete(Caminho); } catch (Exception) { } }
    }
}
