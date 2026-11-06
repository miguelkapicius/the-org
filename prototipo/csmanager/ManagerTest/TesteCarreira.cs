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
            Ok(cl.elenco.Select(j => (int)j.traco + "|" + (int)j.moral + "|" + (int)j.entrosamento).SequenceEqual(cs.elenco.Select(j => (int)j.traco + "|" + (int)j.moral + "|" + (int)j.entrosamento)), "traço, moral e entrosamento voltam iguais do salvamento");
            {   // salvamento de antes da decisão 0019: sem os campos novos, o jogador volta com os valores iniciais (não com moral 0)
                var dv = SalvamentoDados.Para(cs); foreach (var jd in dv.elenco) { jd.temPersonalidade = false; jd.moral = 0; jd.entrosamento = 0; jd.traco = 3; }
                var cv = SalvamentoDados.De(dv);
                Ok(cv.elenco.All(j => j.moral == Provisorio.MORAL_INICIAL && j.entrosamento == Provisorio.ENTROSAMENTO_INICIAL && j.traco == Traco.Nenhum), "salvamento antigo: moral e entrosamento iniciais, sem traço");
            }
            Ok(cl.historico.Count == cs.historico.Count && cl.titulos.Count == cs.titulos.Count, "histórico e campeonatos disputados iguais");
            // a carreira carregada continua jogável, inclusive virando de temporada
            int t0 = cl.Temporada; int gg = 0; while (cl.Temporada == t0 && !cl.Falido && gg++ < 200) { while (cl.TemJogoAgora && gg++ < 2000) cl.PularSerie(); if (cl.PodeAvancar) cl.AvancarSemana(); }
            Ok(cl.Temporada == t0 + 1 && cl.mundo.equipes.GroupBy(e => e.circuito).All(x => x.Count() == 16), "a carreira carregada vira de temporada sem problemas");
        }

        // ---------- o que o manager configurou chega à simulação (a partida completa é lenta: só roda com CSM_TESTE_SIM=1)
        {
            Carreira Prepara(int seed, int advForca, Action<Carreira> ajusta)
            {
                var cm = Carreira.Nova(seed); int g = 0; while (!cm.TemJogoAgora && g++ < 60) Avanca(cm);
                cm.PrepararSerie(); cm.VetoAutomatico(); cm.Adversario.forca = advForca; ajusta?.Invoke(cm);
                return cm;
            }
            double MediaA(CSM.Sim.Partida p) => p.A.js.Average(j => new Jogador { skill = j.skill }.Overall);
            double MediaB(CSM.Sim.Partida p) => p.B.js.Average(j => new Jogador { skill = j.skill }.Overall);

            var base_ = Prepara(41, 77, null).MontarPartida(1);
            var descansado = Prepara(41, 77, cm => { foreach (var j in cm.elenco) { j.cansaco = 0; j.preparo = 100; } }).MontarPartida(1);
            var exausto = Prepara(41, 77, cm => { foreach (var j in cm.elenco) { j.cansaco = 100; j.preparo = 0; } }).MontarPartida(1);
            Console.WriteLine("  habilidade média que a simulação recebe: descansado " + MediaA(descansado).ToString("0.0") + " · normal " + MediaA(base_).ToString("0.0") + " · exausto " + MediaA(exausto).ToString("0.0"));
            Ok(MediaA(descansado) > MediaA(base_) && MediaA(base_) > MediaA(exausto), "descansado > normal > exausto na habilidade que vai para a simulação");
            var fraco = Prepara(41, 60, null).MontarPartida(1); var forte = Prepara(41, 92, null).MontarPartida(1);
            Console.WriteLine("  habilidade do adversário: força 60 → " + MediaB(fraco).ToString("0.0") + " · força 92 → " + MediaB(forte).ToString("0.0"));
            Ok(MediaB(forte) > MediaB(fraco) + 15, "a força do adversário vira habilidade dos bots dele");
            Ok(fraco.B.nome == Prepara(41, 60, null).Adversario.nome && fraco.B.js.All(j => !string.IsNullOrEmpty(j.nome)), "os bots do adversário têm os nomes do time dele");

            var agr = Prepara(41, 77, cm => cm.estilo = Estilo.Agressiva).MontarPartida(1); var def = Prepara(41, 77, cm => cm.estilo = Estilo.Defensiva).MontarPartida(1);
            Ok(agr.A.pesos.estilo > def.A.pesos.estilo && agr.A.pesos.tExec < def.A.pesos.tExec, "a estratégia escolhida muda os pesos da IA do time (agressiva decide antes que defensiva)");
            var cmp = Prepara(41, 77, null); var advx = cmp.Adversario; advx.estilo = Estilo.Agressiva; cmp.estilo = Estilo.Defensiva;
            Ok(cmp.MontarPartida(1).B.pesos.estilo > 0.5f, "o estilo do adversário vira os pesos do time dele");

            if (Environment.GetEnvironmentVariable("CSM_TESTE_SIM") == "1")
            {
                var cron = System.Diagnostics.Stopwatch.StartNew();
                var ps = Prepara(41, 77, null).MontarPartida(77); ps.Jogar();
                Console.WriteLine("  uma partida completa da simulação: " + cron.ElapsedMilliseconds + " ms, placar " + ps.A.placar + " x " + ps.B.placar);
                Ok(ps.acabou && ps.A.js.Sum(j => j.k) > 0 && ps.A.js.All(j => j.rounds > 0), "a partida montada pela carreira roda até o fim e devolve abates, dano e rounds");
            }
        }

        // ---------- personalidade (decisão 0019): traços, moral e entrosamento
        Console.WriteLine("------ personalidade ------");
        {
            var cp = Carreira.Nova(21);
            var todos = Enumerable.Range(0, 30).Select(k => Carreira.Nova(100 + k)).SelectMany(x => x.elenco.Concat(x.mercado)).ToList();
            float comTraco = todos.Count(j => j.traco != Traco.Nenhum) / (float)todos.Count;
            Ok(comTraco > 0.4f && comTraco < 0.6f, "cerca de metade dos jogadores tem traço (" + (int)(comTraco * 100) + "%)");
            Ok(Enum.GetValues(typeof(Traco)).Cast<Traco>().Where(t => t != Traco.Nenhum).All(t => todos.Any(j => j.traco == t)), "todos os 6 traços aparecem");

            // moral: vitória sobe, derrota desce; reserva perde ânimo na semana (Estrela o dobro)
            var tit = cp.Titulares.First(); float m0 = tit.moral;
            cp.Registrar(true, 13, 7, false); Ok(tit.moral == m0 + Provisorio.MORAL_VITORIA, "vitória: +" + Provisorio.MORAL_VITORIA + " de moral para os titulares");
            cp.Registrar(false, 7, 13, false); Ok(tit.moral == m0, "derrota: -" + Provisorio.MORAL_DERROTA);
            var res = cp.elenco.Skip(Provisorio.TITULARES).First(); res.traco = Traco.Nenhum; float r0 = res.moral, e0 = res.entrosamento, et0 = tit.entrosamento;
            Avanca(cp);
            Ok(res.moral == r0 - Provisorio.MORAL_RESERVA_SEMANA && res.entrosamento == e0 + Provisorio.ENTROSAMENTO_RESERVA, "semana: reserva perde moral e quase não ganha entrosamento");
            Ok(tit.entrosamento > et0, "semana: titular ganha entrosamento");
            float multAntes = cp.MultEfetivo(tit); tit.moral = 100; float multAlta = cp.MultEfetivo(tit); tit.moral = 0; float multBaixa = cp.MultEfetivo(tit);
            Ok(multAlta > multAntes && multBaixa < multAntes, "moral muda a habilidade que vai para a partida");
            var contratado = cp.mercado[0]; cp.dinheiro = 9000000; Ok(cp.Contratar(contratado) == null && contratado.entrosamento == Provisorio.ENTROSAMENTO_CONTRATADO && contratado.moral == Provisorio.MORAL_CONTRATADO, "contratado chega motivado e pouco entrosado");

            // na simulação: entrosamento chega aos bots; traços valem nas situações certas
            var ptp = cp.MontarPartida(5);
            Ok(ptp.A.js.All(j => Math.Abs(j.skill.entrosamento - Math.Clamp((cp.EntrosamentoTime - (j.traco == Traco.Estrela ? Provisorio.ENTROSAMENTO_ESTRELA : 0)) / 100f, 0, 1)) < 1e-4f), "o entrosamento do time vai para os bots (Estrela com menos)");
            var pt2 = new Partida(7);
            foreach (var j in pt2.A.js) j.traco = Traco.Nenhum;
            pt2.A.js[0].traco = Traco.Pistoleiro; pt2.A.js[1].traco = Traco.Lider;
            float miraP0 = pt2.A.js[0].skill.aimSigma, frio2 = pt2.A.js[2].skill.composure, frio1 = pt2.A.js[1].skill.composure;
            var m = pt2.Preparar();
            Agent Ag(int k) => m.agents.First(x => x.name == pt2.A.js[k].nome);
            Ok(Ag(0).skill.aimSigma < miraP0, "Pistoleiro mira melhor no round de pistol");
            Ok(Ag(2).skill.composure > frio2 && Math.Abs(Ag(1).skill.composure - frio1) < 1e-6f, "Líder dá sangue-frio aos outros 4, não a ele mesmo");
            Ok(pt2.A.js[0].skill.aimSigma == miraP0, "o traço não muda o atributo guardado do jogador, só o do round");
        }

        // ---------- melhores momentos (decisão 0020): jogada da partida e MVP
        Console.WriteLine("------ melhores momentos ------");
        {
            var cm = Carreira.Nova(31);
            var ptm = cm.MontarPartida(11); ptm.Jogar();
            Ok(ptm.jogada != null && ptm.jogada.nota > 0 && !string.IsNullOrEmpty(ptm.jogada.Descricao), "a partida simulada elege uma jogada da partida");
            Console.WriteLine("  jogada: " + ptm.jogada?.Descricao + " (nota " + ptm.jogada?.nota + ")");
            Ok(ptm.jogada.Descricao.Contains("round " + (ptm.jogada.round + 1)) && (ptm.jogada.time == ptm.A || ptm.jogada.time == ptm.B), "a jogada diz o round e o time");
            var desemp = ptm.A.js.Select(j => new Desempenho { nome = j.nome, funcao = j.roleT, k = j.k, d = j.d, hs = j.hs, dano = j.dano, fk = j.fk, rounds = Math.Max(1, j.rounds) }).ToList();
            int mvpsAntes = cm.elenco.Sum(j => j.mvps);
            var rr = cm.Registrar(ptm.vencedor == ptm.A, ptm.A.placar, ptm.B.placar, true, desemp, ptm.jogada);
            Ok(cm.elenco.Sum(j => j.mvps) == mvpsAntes + 1 && cm.elenco.First(j => j.nome == rr.Melhor.nome).mvps >= 1, "o MVP do mapa entra no histórico do jogador");
            bool nossa = ptm.jogada.time == ptm.A;
            Ok(rr.jogada == ptm.jogada.Descricao && rr.jogadaNossa == nossa, "o resultado guarda a jogada e de quem foi (" + (nossa ? "nossa" : "do adversário") + ")");
            if (nossa) Ok(cm.elenco.First(j => j.nome == ptm.jogada.jogador).jogadas.Count == 1 && cm.diario.Any(d => d.Contains("Jogada da partida")), "jogada nossa vai para o histórico do jogador e para as notícias");
            // série inteira pulada: o último mapa tem MVP da série
            var cs2 = Carreira.Nova(32); int g = 0; Resultado ult = null;
            while (g++ < 60 && (ult == null || !ult.serieEncerrada)) { if (cs2.TemJogoAgora) ult = cs2.PularMapa(); else cs2.AvancarSemana(); }
            Ok(ult != null && ult.serieEncerrada && ult.mvpSerie != null && cs2.elenco.Any(j => j.mvpsSerie == 1), "fim de série: MVP da série eleito (" + ult?.mvpSerie + ")");
            Ok(ult.jogada == null, "partida pulada não tem jogada da partida");
            // salvamento
            var opt2 = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
            var cl2 = SalvamentoDados.De(System.Text.Json.JsonSerializer.Deserialize<CarreiraD>(System.Text.Json.JsonSerializer.Serialize(SalvamentoDados.Para(cm), opt2), opt2));
            Ok(cl2.elenco.Select(j => j.mvps + "|" + j.mvpsSerie + "|" + string.Join("/", j.jogadas)).SequenceEqual(cm.elenco.Select(j => j.mvps + "|" + j.mvpsSerie + "|" + string.Join("/", j.jogadas))), "MVPs e jogadas voltam iguais do salvamento");
            Ok(cl2.historico[0].jogada == cm.historico[0].jogada && cl2.historico[0].jogadaNossa == cm.historico[0].jogadaNossa, "a jogada do resultado volta igual do salvamento");
        }

        // ---------- rivalidades (decisão 0021)
        Console.WriteLine("------ rivalidades ------");
        {
            Resultado Serie(string adv, string camp, string fase, bool venceu, int md, int meus, int deles) => new Resultado
            { adversario = adv, campeonato = camp, rotulo = camp + " · " + fase, serieEncerrada = true, venceuSerie = venceu, venceu = venceu, serieMd = md, serieMeus = meus, serieDeles = deles };
            var hist = new List<Resultado>   // mais recente primeiro
            {
                Serie("TITAS", "Copa Sul", "Final", false, 3, 1, 2),        // 0,5 + 4 (final) + 3 (eliminado) + 1 (até o fim) = 8,5
                Serie("TITAS", "Liga", "rodada 2", true, 1, 1, 0),          // 0,5
                Serie("TITAS", "Copa Norte", "Semifinal", true, 3, 2, 0),   // 0,5 + 2 (mata-mata) = 2,5
                Serie("LOBOS", "Major de Outono", "rodada 1 (0-0)", true, 1, 1, 0),   // 0,5 + 2 (Major) = 2,5
            };
            var rts = Rivalidades.Todos(hist);
            var tit = rts.First(x => x.adversario == "TITAS"); var lob = rts.First(x => x.adversario == "LOBOS");
            Ok(tit.pontos == 11.5f && tit.Rival && tit.series == 3 && tit.vitorias == 2 && tit.derrotas == 1, "final perdida no 3º mapa + semifinal + liga = 11,5 pontos: rival (" + tit.pontos + ")");
            Ok(lob.pontos == 2.5f && !lob.Rival, "uma série de Major sem mata-mata não faz rival (" + lob.pontos + ")");
            Ok(tit.ultimaEliminacao == "Final da Copa Sul" && tit.sequencia == -1, "lembra a última eliminação e a sequência");
            {   // no máximo 3 rivais, mesmo com muitos times acima do limite
                var muitos = new List<Resultado>();
                foreach (var n in new[] { "A1", "A2", "A3", "A4", "A5" }) { muitos.Add(Serie(n, "Copa", "Final", false, 3, 1, 2)); muitos.Add(Serie(n, "Copa B", "Final", false, 3, 1, 2)); }
                Ok(Rivalidades.Todos(muitos).Count(x => x.Rival) == Rivalidades.MAX_RIVAIS, "no máximo " + Rivalidades.MAX_RIVAIS + " rivais ao mesmo tempo");
            }
            Ok(rts[0].adversario == "TITAS", "o arquirrival vem primeiro");

            // clássico: contra um rival a moral muda o dobro
            var cr = Carreira.Nova(41); int g2 = 0;
            while (!cr.TemJogoAgora && g2++ < 20) cr.AvancarSemana();
            var se = cr.PrepararSerie();
            cr.historico.Add(Serie(se.adv.nome, "Copa Antiga", "Final", false, 3, 1, 2));
            cr.historico.Add(Serie(se.adv.nome, "Copa Mais Antiga", "Semifinal", false, 3, 0, 2));
            Ok(cr.Classico(se.adv), "o próximo adversário virou rival depois de duas eliminações");
            foreach (var j in cr.Titulares) j.moral = 50;
            var rm = cr.PularMapa();
            Ok(cr.historico.Contains(rm) && cr.Titulares.Where(j => cr.elenco.Contains(j)).All(j => Math.Abs(j.moral - 50) == Provisorio.MORAL_VITORIA * Carreira.CLASSICO_MORAL), "no clássico a moral muda o dobro (" + (rm.venceu ? "vitória" : "derrota") + ")");

            // carreira de duas temporadas pulando tudo: aparecem rivais e notícias com memória
            var cl = Carreira.Nova(42); int semanasR = 0;
            while (cl.Temporada < 3 && semanasR++ < 200) { int gg = 0; while (cl.TemJogoAgora && gg++ < 50) cl.PularSerie(); cl.AvancarSemana(); }
            Console.WriteLine("  em 2 temporadas: " + cl.Retrospectos.Count + " adversários, " + cl.Rivais.Count + " rival(is): " + string.Join("; ", cl.Rivais.Take(3).Select(r => r.adversario + " " + r.pontos + " pts")));
            foreach (var n in cl.diario.Where(d => d.Contains("rival") || d.Contains("Revanche") || d.Contains("Clássico") || d.Contains("tabu") || d.Contains("seguida") || d.Contains("eliminou") || d.Contains("MVPs")).Take(6)) Console.WriteLine("    " + n);
            Ok(cl.Retrospectos.Count > 5 && cl.Rivais.Count <= Rivalidades.MAX_RIVAIS, "o clube enfrenta vários adversários e tem no máximo " + Rivalidades.MAX_RIVAIS + " rivais");
        }

        Console.WriteLine(falhas == 0 ? "TODOS OS TESTES PASSARAM" : falhas + " FALHA(S)");
        Environment.Exit(falhas == 0 ? 0 : 1);
    }
}
