using System;
using System.Linq;
using CSM.Manager;
using CSM.Sim;

/// <summary>Psicólogo do time (decisão 0039): mercado, contrato, custo, proteção da moral, conversas, relatório e salvamento.</summary>
static class TestePsicologo
{
    public static int falhas;
    static void Ok(bool c, string o) { if (!c) { falhas++; Console.WriteLine("FALHOU: " + o); } else Console.WriteLine("ok: " + o); }
    static void Avanca(Carreira c) { int g = 0; while (c.TemJogoAgora && g++ < 50) c.PularSerie(); c.AvancarSemana(); }

    /// <summary>Contrata um psicólogo da qualidade pedida, pondo-o no mercado se precisar.</summary>
    static Psicologo Contrata(Carreira c, int q)
    {
        var p = c.psicologosMercado.FirstOrDefault(x => x.qualidade == q);
        if (p == null) { p = new Psicologo { nome = "Teste", qualidade = q, salario = Psicologos.SALARIO[q] }; c.psicologosMercado.Add(p); }
        c.ContratarPsicologo(p);
        return p;
    }

    public static void Rodar()
    {
        Console.WriteLine("------ psicólogo ------");
        var c = Carreira.Nova(11);
        var m = c.psicologosMercado;
        Ok(c.psicologo == null && m.Count == Psicologos.OPCOES, "carreira nova: sem psicólogo e " + Psicologos.OPCOES + " no mercado");
        Ok(m.Select(p => p.qualidade).Distinct().Count() == m.Count, "qualidades diferentes no mercado (" + string.Join(", ", m.Select(p => p.qualidade)) + ")");
        Ok(m.Zip(m.Skip(1), (a, b) => b.salario > a.salario).All(x => x), "quanto melhor, mais caro (" + string.Join(", ", m.Select(p => p.salario)) + ")");
        Ok(m.All(p => p.nome.StartsWith("Dr.") || p.nome.StartsWith("Dra.")), "nomes inventados com Dr. ou Dra.");

        // sem psicólogo: nada muda
        Ok(c.FatorQuedaMoral == 1f && c.Relatorio().Count == 0 && c.ConversasPorSemana == 0, "sem psicólogo: sem proteção, sem relatório e sem conversa");
        Ok(c.Conversar(c.elenco[0], out _) != null, "sem psicólogo, conversar dá erro");

        // contratar
        int despesa = c.DespesaFixaPorSemana;
        var p3 = Contrata(c, 3);
        Ok(c.psicologo == p3 && !c.psicologosMercado.Contains(p3), "contrata e tira do mercado");
        Ok(c.psicologo.contratoSemanas == Contratos.SEMANAS_ANO, "contrato de 1 temporada (" + c.psicologo.contratoSemanas + " semanas)");
        Ok(c.DespesaFixaPorSemana == despesa + p3.salario, "o salário entra na despesa fixa (+" + p3.salario + ")");
        Ok(c.ContratarPsicologo(c.psicologosMercado[0]) != null, "com psicólogo, contratar outro dá erro");
        Ok(Math.Abs(c.FatorQuedaMoral - (1f - 3 * Psicologos.PROTECAO_POR_ESTRELA)) < 1e-4, "3 estrelas: as quedas de moral ficam em " + (c.FatorQuedaMoral * 100).ToString("0") + " %");
        Avanca(c);
        Ok(c.extrato.Any(l => l.descricao == "Salário do psicólogo" && l.valor == -p3.salario), "a semana paga o salário do psicólogo");

        // sem caixa não contrata
        var pobre = Carreira.Nova(12); pobre.dinheiro = 100;
        Ok(pobre.ContratarPsicologo(pobre.psicologosMercado[0]) != null, "sem caixa para um mês de salário, não contrata");

        // proteção: o mesmo reserva perde menos moral com um bom psicólogo
        float MoralReserva(bool comPsi)
        {
            var k = Carreira.Nova(21); if (comPsi) Contrata(k, 5);
            var r = k.elenco[Provisorio.TITULARES]; r.moral = 70f;
            for (int s = 0; s < 4; s++) Avanca(k);
            return r.moral;
        }
        float sem = MoralReserva(false), com = MoralReserva(true);
        Ok(com > sem, "reserva por 4 semanas: moral " + sem.ToString("0.0") + " sem psicólogo, " + com.ToString("0.0") + " com um de 5 estrelas");

        var bancoAbaixo = Carreira.Nova(24); Contrata(bancoAbaixo, 5); var rb = bancoAbaixo.elenco[Provisorio.TITULARES]; rb.moral = 50f; rb.traco = Traco.Nenhum;
        for (int s = 0; s < 6; s++) Avanca(bancoAbaixo);
        Ok(rb.moral < 50f, "mesmo com 5 estrelas, o reserva abaixo do normal ainda perde moral no banco (50 → " + rb.moral.ToString("0.0") + ")");

        // recuperação: quem está abaixo do normal volta mais rápido
        float Recupera(bool comPsi)
        {
            var k = Carreira.Nova(22); if (comPsi) Contrata(k, 4);
            var t = k.elenco[0]; t.moral = 20f;
            Avanca(k); Avanca(k);
            return t.moral;
        }
        float r0 = Recupera(false), r1 = Recupera(true);
        Ok(r1 > r0, "titular abalado (20) em 2 semanas: " + r0.ToString("0.0") + " sem psicólogo, " + r1.ToString("0.0") + " com um de 4 estrelas");
        var neutra = Carreira.Nova(23); Contrata(neutra, 5); var nj = neutra.elenco[Provisorio.TITULARES]; nj.moral = 59f; nj.traco = Traco.Nenhum;
        Avanca(neutra);
        Ok(nj.moral <= Provisorio.MORAL_NEUTRA + 1e-3, "o psicólogo puxa só até a moral neutra (" + nj.moral.ToString("0.0") + ")");

        // conversas: limite por semana e chance pela qualidade
        var cv = Carreira.Nova(31); Contrata(cv, 2);
        var alvo = cv.elenco[0]; alvo.moral = 40f;
        Ok(cv.ConversasPorSemana == 1 && cv.Conversar(alvo, out _) == null, "1 conversa por semana antes da sala do psicólogo");
        Ok(cv.Conversar(alvo, out _) != null, "a segunda conversa na mesma semana dá erro");
        Avanca(cv);
        Ok(cv.ConversasRestantes == 1, "a semana nova devolve a conversa");
        alvo.moral = 85f; Ok(cv.Conversar(alvo, out _) != null, "acima de 80 de moral não há o que conversar");
        cv.dinheiro = 9000000; cv.fas = 99999999; while (cv.nivelGH < Psicologos.NIVEL_GH_SALA) cv.SubirGamingHouse();
        Ok(cv.ConversasPorSemana == 2, "a sala do psicólogo (Gaming House nível " + Psicologos.NIVEL_GH_SALA + ") dá 2 conversas por semana");

        int Acertos(int q)
        {
            int n = 0;
            for (int s = 0; s < 60; s++)
            {
                var k = Carreira.Nova(100 + s); Contrata(k, q);
                var j = k.elenco[0]; j.moral = 40f;
                k.Conversar(j, out bool deu); if (deu) { n++; if (j.moral <= 40f) n = -999; }
            }
            return n;
        }
        int a1 = Acertos(1), a5 = Acertos(5);
        Ok(a1 > 0 && a5 > a1, "conversas que dão certo em 60: " + a1 + " com 1 estrela, " + a5 + " com 5 estrelas");

        // relatório: o que cada qualidade conta
        RelatorioJogador RelatorioCom(int q)
        {
            var k = Carreira.Nova(41); Contrata(k, q);
            var r = k.elenco[Provisorio.TITULARES]; r.moral = 50f; r.traco = Traco.Estrela;   // a estrela no banco cai rápido: dá o que prever
            return k.RelatorioDe(r);
        }
        var q1 = RelatorioCom(1); var q2 = RelatorioCom(2); var q4 = RelatorioCom(4);
        Ok(q1.causa == null && q1.previsao == null && q1.efeito.Contains("%"), "1 estrela: só a emoção e o efeito (" + Psicologos.NomeEmocao(q1.emocao) + "; " + q1.efeito + ")");
        Ok(q2.causa != null && q2.previsao == null, "2 estrelas: diz a causa (" + q2.causa + ")");
        Ok(q4.causa != null && q4.previsao != null, "4 estrelas: também prevê (" + q4.previsao + ")");
        var rel = c.Relatorio();
        Ok(rel.Count == c.elenco.Count && rel.Zip(rel.Skip(1), (a, b) => a.jogador.moral <= b.jogador.moral).All(x => x), "relatório com todo o elenco, do mais abalado para o mais confiante");
        Ok(Psicologos.EmocaoDe(90) == Emocao.Confiante && Psicologos.EmocaoDe(60) == Emocao.Tranquilo && Psicologos.EmocaoDe(20) == Emocao.Desmotivado, "faixas de emoção pela moral");

        // contrato: renovar só no fim, terminar, demitir com multa
        var ct = Carreira.Nova(51); Contrata(ct, 3);
        Ok(ct.RenovarPsicologo() != null, "renovar no começo do contrato dá erro");
        ct.psicologo.contratoSemanas = Contratos.SEMANAS_AVISO; int salAntes = ct.psicologo.salario;
        Ok(ct.RenovarPsicologo() == null && ct.psicologo.contratoSemanas > Contratos.SEMANAS_ANO && ct.psicologo.salario > salAntes, "renovar no fim soma uma temporada e dá aumento");
        ct.psicologo.contratoSemanas = 1; Avanca(ct);
        Ok(ct.psicologo == null, "contrato que termina: o psicólogo sai");
        var dm = Carreira.Nova(52); var pd = Contrata(dm, 4); int din = dm.dinheiro, multa = Psicologos.Multa(pd);
        Ok(multa == (int)(pd.salario * Contratos.SEMANAS_ANO * Contratos.PARTE_MULTA) && dm.DemitirPsicologo() == null && dm.psicologo == null && dm.dinheiro == din - multa, "demitir paga metade dos salários que faltam (" + Fmt.Dinheiro(multa) + ")");

        // avisos
        var av = Carreira.Nova(61); av.elenco[0].moral = 10f;
        Ok(av.Avisos().Any(a => a.destino == "psicologo"), "sem psicólogo e com jogador desmotivado: aviso para contratar");

        // mercado novo a cada temporada
        var tp = Carreira.Nova(71); var antes = string.Join("|", tp.psicologosMercado.Select(p => p.nome + p.qualidade));
        int g = 0; while (tp.Temporada == 1 && !tp.Falido && g++ < 80) { tp.dinheiro = Math.Max(tp.dinheiro, 1000000); Avanca(tp); }
        Ok(tp.Temporada == 2 && tp.psicologosMercado.Count == Psicologos.OPCOES && string.Join("|", tp.psicologosMercado.Select(p => p.nome + p.qualidade)) != antes, "temporada nova: mercado de psicólogos renovado");

        // salvamento
        var sv = Carreira.Nova(81); var ps = Contrata(sv, 5); sv.elenco[0].moral = 30f; sv.Conversar(sv.elenco[0], out _);
        var opt = new System.Text.Json.JsonSerializerOptions { IncludeFields = true };
        var back = SalvamentoDados.De(System.Text.Json.JsonSerializer.Deserialize<CarreiraD>(System.Text.Json.JsonSerializer.Serialize(SalvamentoDados.Para(sv), opt), opt));
        Ok(back.psicologo != null && back.psicologo.nome == ps.nome && back.psicologo.qualidade == 5 && back.psicologo.salario == ps.salario && back.psicologo.contratoSemanas == ps.contratoSemanas, "salvamento guarda o psicólogo");
        Ok(back.conversasNaSemana == 1 && back.psicologosMercado.Count == sv.psicologosMercado.Count, "salvamento guarda as conversas da semana e o mercado de psicólogos");
        var antigo = SalvamentoDados.Para(sv); antigo.temPsicologo = false; antigo.psicologosMercado.Clear();
        var va = SalvamentoDados.De(antigo);
        Ok(va.psicologo == null && va.psicologosMercado.Count == Psicologos.OPCOES, "salvamento antigo: sem psicólogo e com mercado novo");
    }
}
