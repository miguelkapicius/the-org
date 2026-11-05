using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Manager;
using CSM.Sim;

class TesteCampeonatos
{
    public static int falhas;
    static void Ok(bool c, string o) { if (!c) { falhas++; Console.WriteLine("FALHOU: " + o); } else Console.WriteLine("ok: " + o); }

    static Campeonato Cria(Formato f, int n, int seed, int clube)
    {
        var tt = Enumerable.Range(0, n).ToList();
        var c = new Campeonato(1, "Teste", f, Circuito.Pro, 1, tt, 100000, 100, seed);
        c.ChanceMapa = (a, b) => 1.0 / (1.0 + Math.Exp(-((50 - a) - (50 - b)) / 6.0));   // times de id menor são mais fortes
        c.Iniciar(clube);
        return c;
    }

    static void JogaClube(Campeonato c, Random rng)
    {
        int guarda = 0;
        while (!c.terminado && c.PendenteDoClube != null && guarda++ < 500)
        {
            var f = c.PendenteDoClube; int a = 0, b = 0;
            while (a < f.Alvo && b < f.Alvo) { if (rng.NextDouble() < 0.5) a++; else b++; }
            c.RegistrarClube(f, a, b);
        }
    }

    public static void Rodar()
    {
        // ---------- formatos sem o clube
        var casos = new (Formato f, int n, int esperado, string nome)[]
        {
            (Formato.Liga, 8, 28, "liga de 8"), (Formato.GruposMataMata, 16, 24 + 7, "grupos + mata-mata"),
            (Formato.GruposGslMataMata, 16, 20 + 7, "grupos GSL + mata-mata"), (Formato.MataMata, 16, 15, "mata-mata de 16"),
        };
        foreach (var (f, n, esp, nome) in casos)
        {
            var c = Cria(f, n, 7, -1);
            int total = c.TodosConfrontos.Count();
            Ok(c.terminado, nome + ": termina");
            Ok(total == esp, nome + ": " + esp + " séries (teve " + total + ")");
            Ok(c.colocacao.Distinct().Count() == n && c.colocacao.Count == n, nome + ": colocação com os " + n + " times, sem repetir");
            Ok(c.TodosConfrontos.All(x => x.feito && x.vitA != x.vitB), nome + ": toda série tem vencedor");
            Ok(c.TodosConfrontos.All(x => Math.Max(x.vitA, x.vitB) == x.Alvo), nome + ": toda série vai até o número de vitórias do MD");
        }
        {
            var c = Cria(Formato.SuicoMataMata, 16, 9, -1);
            var suico = c.TodosConfrontos.Count(x => x.rotulo != null && !x.rotulo.Contains("playoffs"));
            var play = c.TodosConfrontos.Count(x => x.rotulo != null && x.rotulo.Contains("playoffs"));
            Ok(c.terminado && play == 7, "suíço + mata-mata: playoffs com 7 séries (teve " + play + ")");
            Ok(suico >= 24 && suico <= 40, "suíço: fase com entre 24 e 40 séries (teve " + suico + ")");
            Ok(c.colocacao.Distinct().Count() == 16, "suíço: colocação com 16 times");
        }
        // ---------- a final é MD5, a semifinal MD3, as primeiras rodadas MD1
        {
            var c = Cria(Formato.MataMata, 16, 3, -1);
            Ok(c.TodosConfrontos.Last().md == 5, "mata-mata: a final é MD5");
            Ok(c.TodosConfrontos.Take(8).All(x => x.md == 1), "mata-mata: as primeiras rodadas são MD1");
        }
        // ---------- com o clube participando
        for (int seed = 1; seed <= 25; seed++)
            foreach (var f in new[] { Formato.Liga, Formato.GruposMataMata, Formato.GruposGslMataMata, Formato.SuicoMataMata, Formato.MataMata })
            {
                int n = f == Formato.Liga ? 8 : 16; var rng = new Random(seed);
                var c = Cria(f, n, seed, 3);
                JogaClube(c, rng);
                if (!c.terminado || c.colocacao.Distinct().Count() != n) { Ok(false, "com clube: " + f + " seed " + seed + " não terminou direito"); goto fim1; }
            }
        Ok(true, "com o clube jogando: 5 formatos x 25 sementes terminam e dão colocação completa");
        fim1:;

        // ---------- o mundo e a temporada
        var jog = new List<(string, Role)> { ("A", Role.Entry), ("B", Role.Suporte), ("C", Role.Lurker), ("D", Role.Awper), ("E", Role.Igl) };
        var m = Mundo.Novo(11, "VULCAO", 77, jog);
        Ok(m.equipes.Count == 48, "48 times");
        Ok(m.equipes.GroupBy(e => e.circuito).All(g => g.Count() == 16), "16 times por circuito");
        Ok(m.equipes.Select(e => e.nome).Distinct().Count() == 48, "nomes de times únicos");
        Ok(m.equipes.SelectMany(e => e.jogadores.Select(j => j.nome)).Distinct().Count() == 47 * 5 + 5, "nicks de jogadores únicos");
        Console.WriteLine("clube: circuito " + m.Clube.circuito + ", ranking " + m.PosicaoRanking(0));
        Ok(m.Clube.circuito == Circuito.Pro, "o clube começa no circuito Pro");
        Ok(m.campeonatos.Count == 3 * (CountSlots(false) + 2 * 2), "campeonatos da temporada para os 3 circuitos (" + m.campeonatos.Count + ")");

        // anda 2 temporadas jogando tudo do clube ao acaso
        var r2 = new Random(5); int jogos = 0; int semanasComJogo = 0; var porSem = new Dictionary<int, int>();
        for (int s = 0; s < 104; s++)
        {
            bool jogou = false;
            while (true)
            {
                var (camp, conf, sem) = m.ProximoCompromisso();
                if (conf == null || conf.semana > m.semana) break;
                int a = 0, b = 0; while (a < conf.Alvo && b < conf.Alvo) { if (r2.NextDouble() < 0.5) a++; else b++; }
                camp.RegistrarClube(conf, conf.a == 0 ? a : b, conf.a == 0 ? b : a);   // o clube é o "a" ou o "b"
                if (conf.a == 0) { conf.vitA = a; conf.vitB = b; } else { conf.vitA = b; conf.vitB = a; }
                jogos++; jogou = true;
                porSem[m.temporada * 100 + m.semana] = porSem.GetValueOrDefault(m.temporada * 100 + m.semana) + 1;
                m.ApurarFim();
            }
            if (jogou) semanasComJogo++;
            m.AvancarSemana();
        }
        Console.WriteLine("em 2 temporadas: " + jogos + " séries do clube, em " + semanasComJogo + " semanas com jogo");
        Ok(jogos > 40, "o clube joga uma quantidade razoável de séries (" + jogos + " em 104 semanas)");
        Ok(m.temporada == 3 && m.semana == 1, "virou duas temporadas");
        Ok(m.equipes.GroupBy(e => e.circuito).All(g => g.Count() == 16), "depois de 2 temporadas, ainda 16 por circuito");
        Ok(m.equipes.Any(e => e.PontosRanking > 0), "o ranking tem pontos");
        Console.WriteLine("clube agora: circuito " + m.Clube.circuito + ", ranking " + m.PosicaoRanking(0) + ", pontos " + m.Clube.PontosRanking);
        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        
    }

    static int CountSlots(bool liga) => Mundo.CALENDARIO.Count(s => s.liga == liga);
}
