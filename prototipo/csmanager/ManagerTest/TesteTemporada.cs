using System;
using System.Linq;
using CSM.Manager;
using CSM.Sim;

/// <summary>Fim de temporada (decisão 0040): o resumo do ano, a lista do que muda e o salvamento.</summary>
static class TesteTemporada
{
    public static int falhas;
    static void Ok(bool c, string o) { if (!c) { falhas++; Console.WriteLine("FALHOU: " + o); } else Console.WriteLine("ok: " + o); }

    /// <summary>Pula as séries e avança até a temporada virar; devolve o resumo da semana em que virou.</summary>
    static ResumoSemana AteVirar(Carreira c, bool reporElenco = false)
    {
        int t0 = c.Temporada, g = 0;
        while (g++ < 80 && !c.Falido)
        {
            c.dinheiro = Math.Max(c.dinheiro, 300000);   // o teste não quer falência no meio
            // rivais podem pagar a cláusula de alguém no meio do ano: repor mantém o elenco acima dos 5 titulares até o fim
            while (reporElenco && c.elenco.Count < 7 && c.mercado.Count > 0) { c.dinheiro += 1000000; c.Contratar(c.mercado.OrderBy(j => j.Preco).First()); }
            int k = 0; while (c.TemJogoAgora && k++ < 50) c.PularSerie();
            var r = c.AvancarSemana();
            if (r != null && r.virouTemporada) return r;
        }
        return null;
    }

    public static void Rodar()
    {
        Console.WriteLine("------ fim de temporada ------");
        var c = Carreira.Nova(5);
        Ok(c.temInicioTemporada && c.inicioPartida == 0 && c.inicioCircuito == (int)c.CircuitoClube && c.temporadas.Count == 0, "carreira nova marca o começo da temporada 1");
        foreach (var j in c.elenco) j.contratoSemanas = 500;   // ninguém sai por contrato no meio do ano: o elenco chega inteiro ao fim
        var veterano = c.elenco[Provisorio.TITULARES]; veterano.idade = Carreira.IDADE_APOSENTADORIA - 1;
        var r = AteVirar(c, true);
        Ok(r != null && r.temporadaEncerrada != null, "a semana que vira a temporada traz o resumo");
        var t = c.UltimaTemporada;
        string dump = Environment.GetEnvironmentVariable("CSM_RESUMO_JSON");   // grava o resumo para a prévia em HTML da tela
        if (!string.IsNullOrEmpty(dump) && t != null) System.IO.File.WriteAllText(dump, System.Text.Json.JsonSerializer.Serialize(t, new System.Text.Json.JsonSerializerOptions { IncludeFields = true, WriteIndented = true }));
        Ok(t != null && c.temporadas.Count == 1 && c.temporadaNaoVista, "o resumo fica guardado e marcado como não visto");
        Ok(t.temporada == 1 && t.ano == 2027 && c.Temporada == 2, "resumo da temporada 1 (" + t.ano + "); a carreira já está na 2");
        Ok(t.mapas == c.historico.Count && t.vitorias + t.derrotas == t.mapas && t.mapas > 0, "campanha: " + t.vitorias + "V " + t.derrotas + "D em " + t.mapas + " mapas (" + t.PctVitorias + " %)");
        Ok(t.seriesVencidas <= t.series && t.melhorSequencia <= t.vitorias, "séries " + t.seriesVencidas + "/" + t.series + ", melhor sequência " + t.melhorSequencia);
        Ok(t.campeonatos.Count == c.titulos.Count && t.premios == c.titulos.Sum(x => x.premio), t.campeonatos.Count + " campeonatos disputados, " + Fmt.Dinheiro(t.premios) + " em prêmios");
        Ok(t.circuitoAntes == (int)Circuito.Pro && t.circuitoDepois == (int)c.CircuitoClube, "circuito " + Mundo.NomeCircuito(t.Antes) + " → " + Mundo.NomeCircuito(t.Depois) + " (ranking #" + t.ranking + " de " + t.times + "): " + t.Manchete);
        Ok(t.ranking == c.PosicaoRanking, "o ranking do resumo é o que decidiu o circuito novo");
        Ok(t.fasInicio == 1000 || t.fasInicio > 0, "fãs " + t.fasInicio + " → " + t.fasFim);
        Ok(t.elenco.Where(e => !e.aposentou).All(e => c.elenco.Any(j => j.nome == e.nome && j.idade == e.idade)), "o elenco do resumo está um ano mais velho, como o elenco de verdade");
        var ev = t.elenco.First(e => e.nome == veterano.nome);
        Ok(ev.aposentou && !c.elenco.Contains(veterano), veterano.nome + " fez " + ev.idade + ", aparece como aposentado e saiu do elenco");
        Ok(t.mudancas.Any(m => m.Contains(veterano.nome) && m.Contains("aposenta")), "a lista de mudanças anuncia a aposentadoria");
        Ok(c.elenco.Count >= Provisorio.TITULARES, "a aposentadoria nunca deixa o elenco sem 5 titulares");
        Ok(t.mudancas.Any(m => m.Contains("Pontos de ranking")) && t.mudancas.Any(m => m.Contains("psicólogos")) && t.mudancas.Last().Contains("campeonatos"), "a lista de mudanças tem ranking, mercados e o calendário novo (" + t.mudancas.Count + " itens)");
        Ok(t.mvp == null || (t.mvpVezes > 0 && t.elenco.Any(e => e.nome == t.mvp)), "MVP da temporada: " + (t.mvp ?? "nenhum") + " (" + t.mvpVezes + ")");
        Ok(t.evolucao == null || t.evolucaoGanho > 0, "maior evolução: " + (t.evolucao ?? "nenhuma") + " (+" + t.evolucaoGanho + ")");
        Ok(c.inicioPartida == c.numPartida && c.inicioTitulos == c.titulos.Count && c.inicioDinheiro == c.dinheiro, "a temporada nova começa a contar do zero");
        Ok(c.elenco.All(j => Math.Abs(j.Valor(0) - j.inicioTemporada[0]) < 1e-3), "a evolução dos atributos volta a contar do zero");

        // a temporada 2 só conta o que aconteceu nela
        int mapas1 = t.mapas, titulos1 = t.campeonatos.Count;
        var r2 = AteVirar(c);
        var t2 = c.UltimaTemporada;
        Ok(r2 != null && c.temporadas.Count == 2 && t2.temporada == 2 && t2.mapas == c.historico.Count - mapas1 && t2.campeonatos.Count == c.titulos.Count - titulos1, "temporada 2: " + t2.mapas + " mapas e " + t2.campeonatos.Count + " campeonatos, só os dela");
        Ok(t2.circuitoAntes == t.circuitoDepois, "a temporada 2 começa no circuito em que a 1 terminou");

        // salvamento
        var opt = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var back = SalvamentoDados.De(System.Text.Json.JsonSerializer.Deserialize<CarreiraD>(System.Text.Json.JsonSerializer.Serialize(SalvamentoDados.Para(c), opt), opt));
        var b0 = back.UltimaTemporada;
        Ok(back.temporadas.Count == 2 && b0.temporada == 2 && b0.mapas == t2.mapas && b0.Manchete == t2.Manchete && b0.mudancas.Count == t2.mudancas.Count && b0.elenco.Count == t2.elenco.Count, "salvamento guarda os resumos das temporadas");
        Ok(back.temporadaNaoVista == c.temporadaNaoVista && back.inicioPartida == c.inicioPartida && back.inicioCircuito == c.inicioCircuito, "salvamento guarda o começo da temporada e o 'não visto'");
        var antigo = SalvamentoDados.Para(c); antigo.temTemporadas = false;
        var va = SalvamentoDados.De(antigo);
        Ok(va.temporadas.Count == 0 && va.temInicioTemporada && va.inicioPartida == va.numPartida, "salvamento antigo: sem resumos e começa a contar de onde abriu");
    }
}
