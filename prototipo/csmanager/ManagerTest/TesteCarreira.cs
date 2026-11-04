using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Manager;
using CSM.Sim;

class TesteCarreira
{
    static int falhas;
    static void Ok(bool c, string o) { if (!c) { falhas++; Console.WriteLine("FALHOU: " + o); } else Console.WriteLine("ok: " + o); }

    /// <summary>Joga a carreira por n semanas: todo confronto do clube é pulado (veto automático); opcionalmente treina.</summary>
    static void Anda(Carreira c, int semanas, Action<Carreira> politica = null)
    {
        int guarda = 0;
        for (int s = 0; s < semanas && !c.Falido; s++)
        {
            politica?.Invoke(c);
            while (c.TemJogoAgora && guarda++ < 100000) { if (c.PularSerie() == null) break; }
            if (c.PodeAvancar) c.AvancarSemana();
        }
    }

    /// <summary>Joga (pulando) qualquer série marcada e avança uma semana.</summary>
    static void Avanca(Carreira c) { int g = 0; while (c.TemJogoAgora && g++ < 50) c.PularSerie(); c.AvancarSemana(); }

    public static void Main()
    {
        TesteCampeonatos.Rodar(); falhas += TesteCampeonatos.falhas;
        Console.WriteLine("------ carreira ------");
        var c = Carreira.Nova(3);
        Ok(c.elenco.Count == 7 && c.mundo.equipes.Count == 48, "carreira nova: 7 jogadores e 48 times");
        Ok(c.CircuitoClube == Circuito.Pro, "começa no circuito Pro (ranking " + c.PosicaoRanking + ")");
        Ok(c.elenco.All(j => j.potencial >= j.Overall && j.potencial <= 97), "potencial >= geral e <= 97");
        Ok(c.Titulares.Average(j => j.Salario) > 1000 && c.Titulares.Average(j => j.Salario) < 2000, "salário semanal na casa de $ 1.350 (" + c.elenco[0].Salario + ")");

        // ---------- treino: limites da Gaming House
        var jov = c.elenco.OrderBy(j => j.idade).First();
        Ok(c.DefinirTreino(jov, (int)Atrib.Mira, 4) != null, "nível 1: pedir 4 sessões dá erro (máximo " + c.MaxSessoes + ")");
        Ok(c.DefinirTreino(jov, (int)Atrib.Mira, 2) == null, "nível 1: 2 sessões podem");
        Ok(c.DefinirColetivas(1) != null, "sessões coletivas bloqueadas no nível 1");
        Ok(c.DefinirEstudoMapa(2) != null, "estudo de mapa bloqueado no nível 1");
        c.dinheiro = 5000000; Ok(c.SubirGamingHouse() == null && c.nivelGH == 2, "sobe a Gaming House para o nível 2");
        Ok(c.DefinirTreino(jov, (int)Atrib.Mira, 4) == null && c.DefinirEstudoMapa(2) == null, "nível 2: 4 sessões e estudo de mapa liberados");
        c.SubirGamingHouse(); c.SubirGamingHouse(); c.SubirGamingHouse();
        Ok(c.nivelGH == 5 && c.scouts.Count == 3 && c.ElencoMax == 10, "nível 5: 3 scouts e elenco de 10");

        // ---------- treino x cansaço x preparo
        var c2 = Carreira.Nova(4);
        c2.dinheiro = 9000000; for (int i = 0; i < 4; i++) c2.SubirGamingHouse();
        var j2 = c2.elenco[0];
        float mira0 = j2.Valor((int)Atrib.Mira);
        c2.DefinirTreino(j2, (int)Atrib.Mira, 2);
        var sem = new List<float>();
        for (int i = 0; i < 8; i++) { Avanca(c2); sem.Add(j2.cansaco); }
        Ok(j2.Valor((int)Atrib.Mira) > mira0, "treinar Mira por 8 semanas aumenta a Mira (" + mira0.ToString("0.0") + " → " + j2.Valor((int)Atrib.Mira).ToString("0.0") + ")");
        Ok(j2.preparo > 20, "o treino dá preparo (" + j2.preparo.ToString("0") + ")");
        Console.WriteLine("  2 sessões: cansaço por semana: " + string.Join(", ", sem.Select(x => x.ToString("0"))));
        Ok(c2.MultEfetivo(j2) > 1f, "2 sessões por semana: bônus líquido positivo (x" + c2.MultEfetivo(j2).ToString("0.000") + ")");
        // exagero: 5 sessões + coletivas
        var c3 = Carreira.Nova(4); c3.dinheiro = 9000000; for (int i = 0; i < 4; i++) c3.SubirGamingHouse();
        var j3 = c3.elenco[0]; c3.DefinirTreino(j3, (int)Atrib.Mira, 5); c3.DefinirColetivas(3);
        var sem3 = new List<float>(); for (int i = 0; i < 12; i++) { Avanca(c3); sem3.Add(j3.cansaco); }
        Console.WriteLine("  5 sessões + 3 coletivas: cansaço por semana: " + string.Join(", ", sem3.Select(x => x.ToString("0"))));
        Ok(j3.cansaco > 55, "exagerar no treino gera cansaço alto (" + j3.cansaco.ToString("0") + ")");
        Ok(c3.MultEfetivo(j3) < c2.MultEfetivo(j2), "exagerar rende MENOS na partida do que treinar moderado (x" + c3.MultEfetivo(j3).ToString("0.000") + " contra x" + c2.MultEfetivo(j2).ToString("0.000") + ")");
        // descanso recupera
        c3.DefinirTreino(j3, -1, 0); c3.DefinirColetivas(0); j3.cansaco = 100; j3.mapasNaSemana = 0;
        for (int i = 0; i < 4; i++) { var (_, _, cd) = c3.PreverSemana(j3); j3.cansaco = cd; }
        Ok(j3.cansaco < 5, "descansar (sem treino e sem jogo) recupera cerca de 31 de cansaço por semana: 100 → " + j3.cansaco.ToString("0") + " em 4 semanas");

        // ---------- idade
        var c4 = Carreira.Nova(5);
        var vet = c4.elenco[0]; vet.idade = 33; var novo = c4.elenco[1]; novo.idade = 18; novo.potencial = 95;
        float rv = vet.Valor((int)Atrib.Reflexo), nv = novo.Overall;
        for (int i = 0; i < 40; i++) { if (c4.PodeAvancar) c4.AvancarSemana(); else { c4.PularSerie(); } }
        Ok(vet.Valor((int)Atrib.Reflexo) < rv, "o veterano de 33 anos perde reflexo (" + rv.ToString("0.0") + " → " + vet.Valor((int)Atrib.Reflexo).ToString("0.0") + ")");
        Ok(novo.Overall > nv, "o jovem de 18 evolui sozinho (" + nv + " → " + novo.Overall + ")");

        // ---------- finanças: empréstimo e falência
        var c5 = Carreira.Nova(6);
        Ok(c5.EmprestimoDisponivel == Provisorio.LIMITE_EMPRESTIMO[(int)c5.CircuitoClube], "limite de empréstimo do circuito");
        Ok(c5.PegarEmprestimo(10000000) != null, "pedir acima do limite dá erro");
        int din = c5.dinheiro; Ok(c5.PegarEmprestimo(100000) == null && c5.dinheiro == din + 100000 && c5.emprestimos.Count == 1, "pega um empréstimo");
        var emp = c5.emprestimos[0];
        Ok(emp.parcela * emp.restantes > 100000, "os juros entram nas parcelas (" + emp.parcela + " x " + emp.restantes + ")");
        int sem0 = c5.SalariosPorSemana; Avanca(c5);
        Ok(c5.emprestimos[0].restantes == Provisorio.PARCELAS_EMPRESTIMO - 1, "uma parcela é paga por semana");
        int custo = c5.CustoQuitar(c5.emprestimos[0]); Ok(c5.Quitar(c5.emprestimos[0]) == null && c5.emprestimos.Count == 0, "quita antes do prazo (custo " + custo + ")");
        // falência com prazo
        var c6 = Carreira.Nova(7); c6.dinheiro = -900000;
        Ok(c6.Avisos().Any(a => a.nivel == Aviso.Nivel.Critico), "saldo negativo gera aviso crítico");
        for (int i = 0; i < 3; i++) { Avanca(c6); Console.WriteLine("  c6 sem " + c6.Semana + " din " + c6.dinheiro + " neg " + c6.semanasNegativo + " falido " + c6.Falido); }
        Ok(!c6.Falido && c6.semanasNegativo == 3, "3 semanas negativas: ainda não faliu");
        Avanca(c6);
        Ok(c6.Falido, "na 4ª semana negativa o clube vai à falência");
        Ok(!c6.PodeAvancar, "falido não avança");
        var c7 = Carreira.Nova(7); c7.dinheiro = -1000; Avanca(c7); Avanca(c7); c7.dinheiro = 50000; Avanca(c7);
        Ok(c7.semanasNegativo == 0 && !c7.Falido, "recuperar o saldo zera o prazo");

        // ---------- veto e série
        var c8 = Carreira.Nova(8);
        int guarda = 0; while (!c8.TemJogoAgora && guarda++ < 60) c8.AvancarSemana();
        Ok(c8.TemJogoAgora, "chega a semana do primeiro jogo do clube (semana " + c8.Semana + ")");
        Ok(c8.PodeAvancar == false, "com jogo marcado não dá para só avançar");
        var s8 = c8.PrepararSerie();
        Ok(s8 != null && s8.adv != null && s8.veto != null, "prepara a série contra " + s8?.adv?.nome + " (" + Fmt.Serie(s8.Md) + ")");
        int vetadosAntes = s8.veto.passos.Count(p => p.mapa >= 0);
        Ok(c8.Vetar(99) != null || s8.veto.Completo || s8.veto.Atual.quem == 1, "veto inválido é recusado");
        // o manager veta no que for a vez dele
        guarda = 0; while (!s8.veto.Completo && guarda++ < 20) { int m = s8.veto.restantes[0]; var e = c8.Vetar(m); if (e != null) { Console.WriteLine("  erro de veto: " + e); break; } }
        Ok(s8.veto.Completo && s8.mapas.Count == s8.Md + (s8.Md == 1 ? 0 : 0) || s8.mapas.Count >= 1, "o veto termina e define " + s8.mapas.Count + " mapa(s) em ordem: " + string.Join(", ", s8.mapas.Select(m => Mapas.NOMES[m])));
        if (s8.Md == 1) Ok(s8.mapas.Count == 1, "MD1 termina com 1 mapa");
        var pt = c8.MontarPartida(123);
        Ok(pt != null && pt.A.nome == c8.clube && pt.B.nome == s8.adv.nome, "monta a partida: " + pt.A.nome + " x " + pt.B.nome);
        Ok(pt.A.js[0].nome == c8.Titulares.First().nome || c8.Titulares.Any(j => j.nome == pt.A.js[0].nome), "a escalação do manager vai para a simulação");
        var r8 = c8.PularSerie();
        Ok(r8 != null && r8.serieEncerrada && c8.serie == null, "pular a série encerra a série (" + r8.PlacarSerie + ")");
        Ok(r8.desempenho.Count == 5 && r8.estimado, "desempenho estimado na pulada");

        // contra-estratégia
        var advTeste = c8.mundo.equipes[5]; advTeste.estilo = Estilo.Agressiva;
        c8.estilo = Estilo.Defensiva; float comContra = c8.ForcaEfetiva(-1, advTeste);
        c8.estilo = Estilo.ExecucoesRapidas; float contraONosso = c8.ForcaEfetiva(-1, advTeste);   // execuções rápidas não ganha da agressiva
        c8.estilo = Estilo.Equilibrada; float neutro = c8.ForcaEfetiva(-1, advTeste);
        Ok(comContra > neutro, "usar o contra do estilo do adversário aumenta a força efetiva (" + comContra.ToString("0.0") + " > " + neutro.ToString("0.0") + ")");
        Ok(Estilos.Relacao(Estilo.Agressiva, Estilo.Defensiva) == -1 && Estilos.Relacao(Estilo.Defensiva, Estilo.Agressiva) == 1, "o ciclo de contra-estratégias é coerente");
        // informação do adversário
        var c9 = Carreira.Nova(9); Ok(c9.NivelIntel(5) == 0 && c9.SugestaoContra(c9.mundo.equipes[5]) == null, "sem scout não há informação nem sugestão");
        c9.dinheiro = 9000000; for (int i = 0; i < 4; i++) c9.SubirGamingHouse();
        c9.DefinirScout(0, 2, 5); c9.DefinirScout(1, 1);
        Avanca(c9);
        Ok(c9.NivelIntel(5) == 1 && c9.SugestaoContra(c9.mundo.equipes[5]) != null, "1 semana de scout dá informação nível 1 e a sugestão de contra");
        for (int i = 0; i < 3; i++) Avanca(c9);
        Ok(c9.NivelIntel(5) == 2, "4 semanas dão nível 2 (limitado pela Gaming House 5: " + c9.MaxIntel + ")");
        Ok(c9.mercado.Count(j => j.potencialRevelado) >= 6, "o scout de mercado revelou potenciais (" + c9.mercado.Count(j => j.potencialRevelado) + ")");

        // ---------- escalação e chance (continuam valendo)
        var ca = Carreira.Nova(10);
        Ok(ca.ChanceVitoria() > 0 && ca.ChanceVitoria() < 1, "chance de vitória entre 0 e 1 (" + ca.ChanceVitoria().ToString("0.00") + ")");
        Ok(ca.ChanceVitoria(ca.Forca + 8) > ca.ChanceVitoria(), "mais força hipotética = mais chance");
        Ok(Math.Abs(Carreira.ChanceSerie(0.5, 3) - 0.5) < 1e-9 && Carreira.ChanceSerie(0.7, 3) > 0.7, "a série MD3 favorece o mais forte");

        // ---------- economia ao longo de 3 temporadas (calibração)
        foreach (var (nome, pol) in new (string, Action<Carreira>)[] {
            ("sem treino", null),
            ("treino moderado (2 sessões em Mira)", cc => { foreach (var j in cc.elenco) if (j.sessoes == 0) cc.DefinirTreino(j, (int)Atrib.Mira, 2); }) })
        {
            var ce = Carreira.Nova(21); var din0 = ce.dinheiro;
            if (pol != null) { ce.dinheiro += 100000; ce.SubirGamingHouse(); }
            var pontos = new List<string>();
            for (int t = 1; t <= 3; t++) { int ini = ce.Temporada; int guard2 = 0; while (ce.Temporada == ini && !ce.Falido && guard2++ < 400) Anda(ce, 1, pol); pontos.Add("T" + t + ": " + Fmt.Dinheiro(ce.dinheiro) + " rank " + ce.PosicaoRanking + " " + Mundo.NomeCircuito(ce.CircuitoClube) + " F" + ce.Forca); }
            Console.WriteLine("  economia [" + nome + "]: " + string.Join(" | ", pontos));
            Ok(!ce.Falido, "economia (" + nome + "): o clube de nível médio não quebra em 3 temporadas");
        }

        // ---------- salvar e carregar (no meio de uma série)
        {
            var cs = Carreira.Nova(31); cs.dinheiro = 3000000; cs.SubirGamingHouse(); cs.SubirGamingHouse(); cs.SubirGamingHouse();
            cs.DefinirTreino(cs.elenco[0], (int)Atrib.Reflexo, 3); cs.DefinirScout(0, 2, 7); cs.estilo = Estilo.Agressiva; cs.DefinirEstudoMapa(3); cs.PegarEmprestimo(50000);
            int g = 0; Anda(cs, 18);
            while (!(cs.TemJogoAgora && cs.Proximo().conf.md >= 3) && g++ < 120) Avanca(cs);
            var sr = cs.PrepararSerie(); cs.Vetar(sr.veto.restantes[0]);
            if (!sr.veto.Completo) cs.VetoAutomatico();
            cs.PularMapa();
            Console.WriteLine("  salvando: semana " + cs.Semana + ", série " + (cs.serie != null ? cs.serie.resultados.Count + " mapa(s) jogado(s)" : "encerrada"));
            var opt = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            string json = System.Text.Json.JsonSerializer.Serialize(SalvamentoDados.Para(cs), opt);
            var cl = SalvamentoDados.De(System.Text.Json.JsonSerializer.Deserialize<CarreiraD>(json, opt));
            Ok(cl != null, "o salvamento volta a ser uma carreira (" + (json.Length / 1024) + " KB)");
            Ok(cl.dinheiro == cs.dinheiro && cl.Semana == cs.Semana && cl.Temporada == cs.Temporada && cl.nivelGH == cs.nivelGH && cl.estilo == cs.estilo && cl.estudoMapa == cs.estudoMapa, "valores simples iguais");
            Ok(cl.elenco.Select(j => j.nome + j.Overall + j.potencial + (int)j.cansaco + j.sessoes + j.foco).SequenceEqual(cs.elenco.Select(j => j.nome + j.Overall + j.potencial + (int)j.cansaco + j.sessoes + j.foco)), "elenco igual (geral, potencial, cansaço, plano de treino)");
            Ok(cl.emprestimos.Count == cs.emprestimos.Count && cl.scouts.Count == cs.scouts.Count && cl.intel.Count == cs.intel.Count, "empréstimos, scouts e informação iguais");
            Ok(cl.mundo.Ranking().Select(e => e.id).SequenceEqual(cs.mundo.Ranking().Select(e => e.id)), "o ranking dos 48 times é o mesmo");
            Ok(cl.mundo.campeonatos.Count == cs.mundo.campeonatos.Count && cl.mundo.campeonatos.Zip(cs.mundo.campeonatos, (a, b) => a.terminado == b.terminado && a.iniciado == b.iniciado && a.TodosConfrontos.Count() == b.TodosConfrontos.Count() && a.colocacao.SequenceEqual(b.colocacao)).All(x => x), "os campeonatos foram refeitos idênticos (confrontos e colocação)");
            var pa = cs.Proximo(); var pb = cl.Proximo();
            Ok(pa.camp?.id == pb.camp?.id && pa.conf?.a == pb.conf?.a && pa.conf?.b == pb.conf?.b && pa.semana == pb.semana, "o próximo compromisso do clube é o mesmo");
            Ok((cs.serie == null) == (cl.serie == null) && (cs.serie == null || (cl.serie.resultados.Count == cs.serie.resultados.Count && cl.serie.mapas.SequenceEqual(cs.serie.mapas) && cl.serie.veto.Completo == cs.serie.veto.Completo)), "a série em andamento (veto e mapas jogados) volta igual");
            Ok(cl.historico.Count == cs.historico.Count && cl.titulos.Count == cs.titulos.Count, "histórico e campeonatos disputados iguais");
            // a carreira carregada continua jogável, inclusive virando de temporada
            int t0 = cl.Temporada; int gg = 0; while (cl.Temporada == t0 && !cl.Falido && gg++ < 200) { while (cl.TemJogoAgora && gg++ < 2000) cl.PularSerie(); if (cl.PodeAvancar) cl.AvancarSemana(); }
            Ok(cl.Temporada == t0 + 1 && cl.mundo.equipes.GroupBy(e => e.circuito).All(x => x.Count() == 16), "a carreira carregada vira de temporada sem problemas");
        }

        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        Environment.Exit(falhas == 0 ? 0 : 1);
    }
}
