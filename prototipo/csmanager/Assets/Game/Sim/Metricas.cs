using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CSM.Sim
{
    /// <summary>
    /// Mede um monte de rounds e compara com o que pro faz. Os números de partida (vitória TR, plant, HS, trocas) são
    /// médias públicas de CS pro arredondadas; os de atividade (parado, peeks, trocas de ângulo) são estimativa minha
    /// olhando demo — viram números de verdade quando a gente tiver demos parseadas. A nota (perda) é o quanto cada
    /// métrica sai da faixa, ao quadrado, com peso: 0 = tudo dentro.
    /// </summary>
    public class Metricas
    {
        public class Alvo
        {
            public string nome; public float min, max, peso; public bool estimativa;
            public Alvo(string n, float a, float b, float p, bool est = false) { nome = n; min = a; max = b; peso = p; estimativa = est; }
            // abaixo do piso mede em relação ao piso (0 peeks com piso 3 = 1,0 inteiro, não 3/6): o treino não pode
            // comprar equilíbrio TR/CT deixando o bot passivo
            public float Fora(float v) => v < min ? (min - v) / Math.Max(1e-3f, min > 0 ? Math.Min(min, max - min) : max - min) : v > max ? (v - max) / Math.Max(1e-3f, max - min) : 0;
        }

        public static readonly Alvo[] ALVOS =
        {
            // números REAIS: 143 partidas / 2.931 rounds de pro (cs-simulator, contract/artifacts/calibration_targets.json)
            new Alvo("TR vence %", 42, 49, 2),            // real 45,6 (Mirage 44)
            new Alvo("tempo acabou %", 4, 9, 1.5f),       // real 6,6
            new Alvo("plant %", 43, 53, 1),               // real 49,7 (Mirage 46)
            new Alvo("1º abate (s)", 33, 44, 2),          // real 38
            new Alvo("HS % (rifle)", 46, 56, 1),          // real 51
            new Alvo("1º abate é do CT %", 55, 65, 1),    // real 60
            new Alvo("1º abate vence o round %", 68, 78, 1),   // real 73
            new Alvo("abates/round", 6, 6.9f, 1),         // real 6,4
            new Alvo("duração média (s)", 82, 98, 1),     // real 91
            new Alvo("trocas % dos abates", 22, 38, 2, true),   // pro troca muito: entry morre e o suporte troca na hora
            new Alvo("morreu sem ver %", 0, 20, 1),   // ser pego de lado/costas ou cego acontece; o que preocupa é demais disso
            new Alvo("parado % do tempo", 0, 30, 4, true),
            new Alvo("parado >2s % do tempo", 0, 10, 4, true),
            new Alvo("peeks/jogador/round", 3, 9, 4, true),
            new Alvo("trocas de ângulo/jog/round", 0.7f, 3, 2, true),
            new Alvo("FK do AWP CT %", 10, 28, 0.7f, true),
            new Alvo("mira no contato, mediana (°)", 1.5f, 3.5f, 2),   // real 2,5 (p25 1,1, p75 5,9, p90 14)
            new Alvo("flashes/time/round", 2.4f, 3.8f, 2),   // real Mirage: CT 2,5, TR 3,7
            new Alvo("smokes/time/round", 3, 3.7f, 2),       // real: CT 3,1, TR 3,6
            new Alvo("molotovs/time/round", 2.2f, 2.8f, 1),  // real: CT 2,6, TR 2,3
            new Alvo("HE/time/round", 0.9f, 2.4f, 1),        // real: CT 2,3, TR 0,9
            new Alvo("flash cega aliado %", 0, 20, 1, true),
            // ritmo: pro toma o mapa antes e entra no bomb lá pra 1:05-0:45 do relógio (rush é exceção)
            new Alvo("1º TR no bomb (s)", 40, 70, 1.5f, true),
            new Alvo("hora do plant (s)", 68, 80, 1),     // real 75
        };
        int fkCt, fkVenceu;
        float chegouSum, plantSum; int chegouN, plantN, c4caiu, c4guardado, c4cedo;
        int tiroCego, recuoCego, agachaCego, cantoIgn, prefMem, callsF, callsP, vvF, vvL, retakes, retakesW, portasR, popF, afob, frio;
        readonly int[] clN = new int[4], clW = new int[4];
        int flashes, smokesJ, molotovsJ, hesJ, heDano, heFuro, errada, errou, jogadas, pixelS, revelou, puniu, flashIni, flashAli, pedidos, atendidos; float semCaminho;

        // somatórios
        int rounds, trWins, timeouts, plants, kills, hsRifle, killsRifle, trades, deaths, unseen, players, fkAwpCt, fks;
        float firstKillSum; int firstKillN; float vivo, parado, paradoLongo, peeks, trocas, avancos, info, exposto, dur;
        public readonly Dictionary<string, (int n, int win)> planos = new Dictionary<string, (int, int)>();
        readonly Dictionary<string, float> paradoPor = new Dictionary<string, float>();
        readonly Dictionary<string, int> vaiVoltaPor = new Dictionary<string, int>();
        readonly List<Match.Acerto> acertos = new List<Match.Acerto>();
        readonly Dictionary<string, (int parede, int total)> miraPor = new Dictionary<string, (int, int)>();
        readonly List<(float flick, string fase)> contatos = new List<(float, string)>(); readonly List<float> contatosTiro = new List<float>();
        readonly Dictionary<string, int> ev = new Dictionary<string, int>();

        /// <summary>Anatomia do HS: de onde vêm os abates na cabeça (rifles), por situação.</summary>
        public string AnatomiaHS()
        {
            var sb = new StringBuilder();
            var rif = acertos.Where(a => a.arma != "awp").ToList();
            var ab = rif.Where(a => a.matou).ToList();
            sb.AppendLine($"ANATOMIA DO HS (rifles/SMG/pistola: {rif.Count} acertos, {ab.Count} abates)");
            sb.AppendLine($"  acertos na cabeça: {Pct(rif.Count(a => a.parte == 1), rif.Count):0}% · abates na cabeça: {Pct(ab.Count(a => a.parte == 1), ab.Count):0}% · abate com 1 bala só (cheio de vida): {Pct(ab.Count(a => a.hpAntes >= 100), ab.Count):0}%");
            void Linha(string nome, Func<Match.Acerto, bool> f)
            {
                var a = rif.Where(f).ToList(); var k = a.Where(x => x.matou).ToList();
                sb.AppendLine($"  {nome,-30} acertos {a.Count,5} (cabeça {Pct(a.Count(x => x.parte == 1), a.Count),3:0}%)   abates {k.Count,4} (HS {Pct(k.Count(x => x.parte == 1), k.Count),3:0}%)");
            }
            Linha("perto (<150)", a => a.dist < 150); Linha("médio (150-300)", a => a.dist >= 150 && a.dist < 300);
            Linha("longe (300-500)", a => a.dist >= 300 && a.dist < 500); Linha("muito longe (500+)", a => a.dist >= 500);
            Linha("mira já no lugar (<6°)", a => a.flick < 6); Linha("flick (6-25°)", a => a.flick >= 6 && a.flick < 25); Linha("flick grande (25°+)", a => a.flick >= 25);
            Linha("alvo parado (<15)", a => a.vAlvo < 15); Linha("alvo andando", a => a.vAlvo >= 15);
            Linha("1ª bala", a => a.spray <= 1); Linha("2ª-3ª bala", a => a.spray >= 2 && a.spray <= 3); Linha("4ª+ bala", a => a.spray >= 4);
            // duelos: quem mata quem, e em que situação
            var abs = acertos.Where(a => a.matou).ToList();
            sb.AppendLine($"DUELOS ({abs.Count} abates)");
            foreach (bool tr in new[] { true, false })
            {
                var k = abs.Where(a => a.atiradorTR == tr).ToList(); string lado = tr ? "TR mata CT" : "CT mata TR";
                sb.AppendLine($"  {lado}: {k.Count}  · vítima tinha visto quem matou: {Pct(k.Count(a => a.vitimaViu), k.Count):0}% · matador parado: {Pct(k.Count(a => a.vAtirador < 15), k.Count):0}% · mira já no lugar (<6°): {Pct(k.Count(a => a.flick < 6), k.Count):0}% · dist média {(k.Count > 0 ? k.Average(a => a.dist) : 0):0}");
                sb.AppendLine("     vítima estava: " + string.Join(", ", k.GroupBy(a => a.faseVitima ?? "?").OrderByDescending(g => g.Count()).Take(7).Select(g => $"{g.Key} {Pct(g.Count(), k.Count):0}%")));
                sb.AppendLine("     matador vinha de: " + string.Join(", ", k.GroupBy(a => a.faseAtirador ?? "?").OrderByDescending(g => g.Count()).Take(7).Select(g => $"{g.Key} {Pct(g.Count(), k.Count):0}%")));
                var sv = k.Where(a => !a.vitimaViu).ToList();
                sb.AppendLine($"     morreu SEM VER ({sv.Count}): arma {string.Join(", ", sv.GroupBy(a => a.arma).OrderByDescending(g => g.Count()).Select(g => $"{g.Key} {g.Count()}"))} · dist média {(sv.Count > 0 ? sv.Average(a => a.dist) : 0):0} · ângulo vítima→matador fora da visão: {Pct(sv.Count(a => a.foraDaVisao), sv.Count):0}% · smoke no meio: {Pct(sv.Count(a => a.smoke), sv.Count):0}% · cego: {Pct(sv.Count(a => a.cego), sv.Count):0}%");
                sb.AppendLine("       vítima estava: " + string.Join(", ", sv.GroupBy(a => a.faseVitima ?? "?").OrderByDescending(g => g.Count()).Take(8).Select(g => $"{g.Key} {g.Count()}")));
            }
            return sb.ToString();
        }

        public void Add(Match m)
        {
            rounds++; dur += m.t;
            if (m.tTRnoBomb >= 0) { chegouSum += m.tTRnoBomb; chegouN++; }
            if (m.bombPlanted) { plantSum += m.plantedAt; plantN++; }
            c4caiu += m.quedasC4; c4guardado += m.c4Guardado; c4cedo += m.c4PegoNaHora;
            if (m.winner == "TR") trWins++;
            if (m.reason == "tempo acabou") timeouts++;
            if (m.bombPlanted) plants++;
            if (m.firstKillT >= 0) { firstKillSum += m.firstKillT; firstKillN++; }
            if (m.firstKill != null)
            {
                fks++; if (m.firstKill.team == Team.CT && m.firstKill.gunId == "awp") fkAwpCt++;
                if (m.firstKill.team == Team.CT) fkCt++;
                if (m.winner == (m.firstKill.IsT ? "TR" : "CT")) fkVenceu++;
            }
            string pl = "TR " + m.igl[0].plan; planos.TryGetValue(pl, out var x); planos[pl] = (x.n + 1, x.win + (m.winner == "TR" ? 1 : 0));
            pl = "CT " + m.igl[1].plan; planos.TryGetValue(pl, out x); planos[pl] = (x.n + 1, x.win + (m.winner == "CT" ? 1 : 0));
            foreach (var kv in m.paradoPor) { paradoPor.TryGetValue(kv.Key, out float v); paradoPor[kv.Key] = v + kv.Value; }
            foreach (var kv in m.vaiVoltaPor) { vaiVoltaPor.TryGetValue(kv.Key, out int v); vaiVoltaPor[kv.Key] = v + kv.Value; }
            acertos.AddRange(m.acertos);
            contatos.AddRange(m.contatos); contatosTiro.AddRange(m.contatosTiro);
            foreach (var kv in m.ev) { ev.TryGetValue(kv.Key, out int n); ev[kv.Key] = n + kv.Value; }
            foreach (var kv in m.miraPor) { miraPor.TryGetValue(kv.Key, out var c); miraPor[kv.Key] = (c.parede + kv.Value.parede, c.total + kv.Value.total); }
            unseen += m.acertos.Count(x => x.matou && !x.vitimaViu);   // morreu sem estar mirando em quem matou (medida do duelo)
            callsF += m.callsFeitos; callsP += m.callsPerdidos;
            foreach (var tm in new[] { Team.T, Team.CT })
            {
                int contra = m.clutchContra[(int)tm]; if (contra < 1) continue; contra = Math.Min(contra, 3);
                clN[contra]++; if (m.winner == (tm == Team.T ? "TR" : "CT")) clW[contra]++;
            }
            // retake de verdade: bomb plantado com 2+ CT vivos e não mais TR que CT (situação que pro converte ~40-50%)
            if (m.bombPlanted && m.ctVivosNoPlant >= 2 && m.trVivosNoPlant <= m.ctVivosNoPlant)
            { retakes++; if (m.winner == "CT") retakesW++; portasR += m.igl[1].portasRetake; }
            foreach (var a in m.agents)
            {
                players++;
                kills += a.kills; trades += a.tradeKills;
                if (a.gunId != "awp") { killsRifle += a.kills; hsRifle += a.hs; }
                if (a.diedAt > 0) deaths++;
                vivo += a.tVivo; parado += a.tParado; paradoLongo += a.tParadoLongo; exposto += a.tExposto;
                peeks += a.peeks; trocas += a.trocas; avancos += a.avancos; info += a.info;
                semCaminho += a.semCaminho;
                vvF += a.vaiVoltaFrente; vvL += a.vaiVoltaLado; popF += a.popFlash; afob += a.afobado; frio += a.frio;
                tiroCego += a.tiroCego; recuoCego += a.recuoCego; agachaCego += a.agachaCego; cantoIgn += a.cantoIgnorado; prefMem += a.prefireMemoria;
                flashes += a.flashes; smokesJ += a.smokes; molotovsJ += a.molotovs; hesJ += a.hes; heDano += a.heDano; heFuro += a.heFuro; errada += a.nadeErrada; errou += a.nadeErrou; pixelS += a.pixelSmoke; revelou += a.revelou; puniu += a.puniu; jogadas += a.flashes + a.smokes + a.molotovs + a.hes; flashIni += a.flashInimigo; flashAli += a.flashAliado; pedidos += a.pediuBang; atendidos += a.bangou;
            }
        }

        static float Pct(float a, float b) => b > 0 ? 100f * a / b : 0;

        public Dictionary<string, float> Valores() => new Dictionary<string, float>
        {
            ["TR vence %"] = Pct(trWins, rounds),
            ["mira no contato, mediana (°)"] = contatosTiro.Count > 0 ? contatosTiro.OrderBy(x => x).ElementAt(contatosTiro.Count / 2) : 99,
            ["  mira <2° % todos os avistamentos / real 45"] = contatos.Count > 0 ? 100f * contatos.Count(c => c.flick < 2) / contatos.Count : 0,
            ["  mira >8° % todos os avistamentos / real 16"] = contatos.Count > 0 ? 100f * contatos.Count(c => c.flick > 8) / contatos.Count : 0,
            ["  avistamentos/round / real 10,2"] = rounds > 0 ? contatos.Count / (float)rounds : 0,
            ["  mira no contato p25 / real 1,1"] = contatosTiro.Count > 0 ? contatosTiro.OrderBy(x => x).ElementAt(contatosTiro.Count / 4) : 99,
            ["  mira no contato p75 / real 5,9"] = contatosTiro.Count > 0 ? contatosTiro.OrderBy(x => x).ElementAt(contatosTiro.Count * 3 / 4) : 99,
            ["  mira no contato p90 / real 14"] = contatosTiro.Count > 0 ? contatosTiro.OrderBy(x => x).ElementAt(contatosTiro.Count * 9 / 10) : 99,
            ["  avistamentos com tiro/round / real 4,8"] = rounds > 0 ? contatosTiro.Count / (float)rounds : 0,
            ["tempo acabou %"] = Pct(timeouts, rounds),
            ["plant %"] = Pct(plants, rounds),
            ["1º abate (s)"] = firstKillN > 0 ? firstKillSum / firstKillN : 99,
            ["HS % (rifle)"] = Pct(hsRifle, killsRifle),
            ["trocas % dos abates"] = Pct(trades, kills),
            ["morreu sem ver %"] = Pct(unseen, deaths),
            ["parado % do tempo"] = Pct(parado, vivo),
            ["parado >2s % do tempo"] = Pct(paradoLongo, vivo),
            ["peeks/jogador/round"] = players > 0 ? peeks / players : 0,
            ["trocas de ângulo/jog/round"] = players > 0 ? trocas / players : 0,
            ["FK do AWP CT %"] = Pct(fkAwpCt, fks),
            ["1º abate é do CT %"] = Pct(fkCt, fks),
            ["1º abate vence o round %"] = Pct(fkVenceu, fks),
            // só informativos (sem alvo ainda)
            ["avanços/jog/round"] = players > 0 ? avancos / players : 0,
            ["info limpa/jog/round"] = players > 0 ? info / players : 0,
            ["exposto % do tempo"] = Pct(exposto, vivo),
            ["duração média (s)"] = rounds > 0 ? dur / rounds : 0,
            ["abates/round"] = rounds > 0 ? kills / (float)rounds : 0,
            ["BUG: s sem caminho/round"] = rounds > 0 ? semCaminho / rounds : 0,
            ["flashes/time/round"] = rounds > 0 ? flashes / (2f * rounds) : 0,
            ["smokes/time/round"] = rounds > 0 ? smokesJ / (2f * rounds) : 0,
            ["molotovs/time/round"] = rounds > 0 ? molotovsJ / (2f * rounds) : 0,
            ["1º TR no bomb (s)"] = chegouN > 0 ? chegouSum / chegouN : 0,
            ["hora do plant (s)"] = plantN > 0 ? plantSum / plantN : 0,
            ["HE/time/round"] = rounds > 0 ? hesJ / (2f * rounds) : 0,
            ["granada que errou o pixel %"] = Pct(errou, jogadas),
            ["pixel novo por causa de smoke/round"] = rounds > 0 ? pixelS / (float)rounds : 0,
            ["C4 caiu (portador morreu)/round"] = rounds > 0 ? c4caiu / (float)rounds : 0,
            ["C4 deixado num canto % rounds"] = Pct(c4guardado, rounds),
            ["C4 pego antes da execução % rounds"] = Pct(c4cedo, rounds),
            ["passos que o inimigo ouviu/jog/round"] = players > 0 ? revelou / (float)players : 0,
            ["puniu barulho (peek/granada no som)/round"] = rounds > 0 ? puniu / (float)rounds : 0,
            ["granada que bateu na parede %"] = Pct(errada, jogadas),
            ["clutch 1v1 ganho % (pro ~50)"] = Pct(clW[1], clN[1]),
            ["  clutch 1v2 ganho % (pro ~25)"] = Pct(clW[2], clN[2]),
            ["  clutch 1v3+ ganho % (pro ~10)"] = Pct(clW[3], clN[3]),
            ["  peek afobado no pânico/round"] = rounds > 0 ? afob / (float)rounds : 0,
            ["  jogada fria no clutch/round"] = rounds > 0 ? frio / (float)rounds : 0,
            ["CT ganha retake (2+ CT, CT >= TR) %"] = Pct(retakesW, retakes),
            ["  retakes desses/round %"] = Pct(retakes, rounds),
            ["  entradas usadas no retake (média)"] = retakes > 0 ? portasR / (float)retakes : 0,
            ["vai-e-volta na linha da mira (W-S)/jog/round"] = players > 0 ? vvF / (float)players : 0,
            ["  vai-e-volta de lado (A-D)/jog/round"] = players > 0 ? vvL / (float)players : 0,
            ["calls no rádio/round"] = rounds > 0 ? callsF / (float)rounds : 0,
            ["  dead call que não saiu %"] = Pct(callsP, callsF + callsP),
            ["atirou cego no vento/round"] = rounds > 0 ? tiroCego / (float)rounds : 0,
            ["  recuou cego/round"] = rounds > 0 ? recuoCego / (float)rounds : 0,
            ["  agachou cego/round"] = rounds > 0 ? agachaCego / (float)rounds : 0,
            ["canto cego que passou reto/round"] = rounds > 0 ? cantoIgn / (float)rounds : 0,
            ["prefire por memória do round passado/round"] = rounds > 0 ? prefMem / (float)rounds : 0,
            ["  dano por HE (inimigo)"] = hesJ > 0 ? heDano / (float)hesJ : 0,
            ["  HE pra abrir smoke/round"] = rounds > 0 ? heFuro / (float)rounds : 0,
            ["flash cega inimigo (por flash)"] = flashes > 0 ? flashIni / (float)flashes : 0,
            ["flash cega aliado %"] = Pct(flashAli, flashIni + flashAli),
            ["  pop flash (estoura no ar) %"] = Pct(popF, flashes),
            ["pediu bang/round"] = rounds > 0 ? pedidos / (float)rounds : 0,
            ["bangou pro colega/round"] = rounds > 0 ? atendidos / (float)rounds : 0,
        };

        /// <summary>Nota do conjunto (0 = tudo dentro dos alvos).</summary>
        public float Perda()
        {
            var v = Valores(); float s = 0;
            foreach (var a in ALVOS) { float f = a.Fora(v[a.nome]); s += a.peso * f * f; }
            return s;
        }

        public int Rounds => rounds;

        public string Relatorio()
        {
            var sb = new StringBuilder(); var v = Valores();
            sb.AppendLine($"VALIDADOR ({rounds} rounds) — nota {Perda():0.000} (0 = tudo dentro dos alvos)");
            foreach (var a in ALVOS)
            {
                float x = v[a.nome]; bool ok = a.Fora(x) == 0;
                sb.AppendLine($"  {(ok ? "ok  " : "FORA")} {a.nome,-28} {x,7:0.0}   alvo {a.min:0.#}–{a.max:0.#}{(a.estimativa ? "  (estimativa, falta demo)" : "")}");
            }
            foreach (var kv in v) if (!ALVOS.Any(a => a.nome == kv.Key)) sb.AppendLine($"       {kv.Key,-28} {kv.Value,7:0.0}");
            sb.Append(AnatomiaHS());
            sb.AppendLine("  PARADO POR QUÊ (% do tempo vivo):");
            foreach (var kv in paradoPor.OrderByDescending(k => k.Value).Take(10)) sb.AppendLine($"       {kv.Key,-32} {Pct(kv.Value, vivo),5:0.0}%");
            sb.AppendLine("  VAI-E-VOLTA W-S POR FASE (vezes):");
            foreach (var kv in vaiVoltaPor.OrderByDescending(k => k.Value).Take(10)) sb.AppendLine($"       {kv.Key,-32} {kv.Value,6}");
            float Med(IEnumerable<float> xs) { var l = xs.OrderBy(x => x).ToList(); return l.Count > 0 ? l[l.Count / 2] : 0; }
            sb.AppendLine($"  MIRA NO 1º CONTATO ({contatos.Count}): já no lugar (<6°) {Pct(contatos.Count(c => c.flick < 6), contatos.Count):0}% · <15° {Pct(contatos.Count(c => c.flick < 15), contatos.Count):0}% · 45°+ {Pct(contatos.Count(c => c.flick >= 45), contatos.Count):0}% · mediana {Med(contatos.Select(c => c.flick)):0}°");
            foreach (var g in contatos.GroupBy(c => c.fase).OrderByDescending(g => g.Count()).Take(18))
                sb.AppendLine($"       {g.Key,-32} {g.Count(),5}  <6° {Pct(g.Count(c => c.flick < 6), g.Count()),3:0}%  45°+ {Pct(g.Count(c => c.flick >= 45), g.Count()),3:0}%  mediana {Med(g.Select(c => c.flick)),3:0}°");
            int mp = miraPor.Values.Sum(c => c.parede), mt = miraPor.Values.Sum(c => c.total);
            sb.AppendLine("  EVENTOS (% rounds = uma vez por round; senão por round):");
            foreach (var kv in ev.Where(k => k.Key.StartsWith("FIM ") || k.Key.StartsWith("PLANT com")).OrderBy(k => k.Key)) sb.AppendLine($"       CONTA {kv.Key,-44} {kv.Value}");
            foreach (var kv in ev.Where(k => !k.Key.StartsWith("AUD ") && !k.Key.StartsWith("FIM ") && !k.Key.StartsWith("PLANT com")).OrderBy(k => k.Key)) sb.AppendLine($"       {kv.Key,-44} {(kv.Key.EndsWith("% rounds") ? Pct(kv.Value, rounds) : kv.Value / (float)Math.Max(1, rounds)),7:0.0}");
            sb.AppendLine($"  MIRA COLADA NA PAREDE (<90, fora de duelo): {Pct(mp, mt):0.0}% do tempo");
            foreach (var kv in miraPor.OrderByDescending(k => k.Value.parede).Take(8))
                sb.AppendLine($"       {kv.Key,-32} {Pct(kv.Value.parede, kv.Value.total),5:0}% de {Pct(kv.Value.total, mt),4:0}% do tempo");
            sb.Append(Auditoria());
            return sb.ToString();
        }

        /// <summary>Grupos do auditor em % (Match.Aud) e quem ganha duelo em cada situação/posição.</summary>
        string Auditoria()
        {
            var sb = new StringBuilder();
            var aud = ev.Where(k => k.Key.StartsWith("AUD ")).Select(k => { var p = k.Key.Substring(4).Split('|'); return (g: p[0], i: p[1], n: k.Value); }).ToList();
            if (aud.Count > 0) sb.AppendLine("  AUDITORIA:");
            foreach (var g in aud.GroupBy(a => a.g).OrderBy(g => g.Key))
            {
                int tot = g.Sum(a => a.n);
                sb.AppendLine($"    {g.Key} ({tot})");
                foreach (var a in g.OrderByDescending(a => a.n).Take(9)) sb.AppendLine($"       {a.i,-46} {Pct(a.n, tot),5:0.0}%");
            }
            var abs = acertos.Where(a => a.matou).ToList();
            void Tabela(string nome, Func<Match.Acerto, string> matador, Func<Match.Acerto, string> vitima, int minimo)
            {
                sb.AppendLine($"  {nome} (abates / mortes / % de duelos ganhos)");
                var w = abs.Where(a => matador(a) != null).GroupBy(matador).ToDictionary(g => g.Key, g => g.Count());
                var l = abs.Where(a => vitima(a) != null).GroupBy(vitima).ToDictionary(g => g.Key, g => g.Count());
                foreach (var k in w.Keys.Union(l.Keys).OrderByDescending(k => (w.GetValueOrDefault(k) + 0.01f) / (w.GetValueOrDefault(k) + l.GetValueOrDefault(k) + 0.02f)))
                {
                    int a = w.GetValueOrDefault(k), b = l.GetValueOrDefault(k);
                    if (a + b >= minimo) sb.AppendLine($"       {k,-40} {a,5} / {b,5} / {Pct(a, a + b),3:0}%");
                }
            }
            sb.AppendLine("  QUEM SEGURA x QUEM VEM (abates):");
            bool Seg(string x) => x == "segurando quieto" || x == "segurando aberto" || x == "no cover";
            foreach (var (nome, f) in new (string, Func<Match.Acerto, bool>)[] { ("quem segura matou", a => Seg(a.situAtirador) && !Seg(a.situVitima)), ("quem vinha matou", a => !Seg(a.situAtirador) && Seg(a.situVitima)) })
            {
                var l = abs.Where(f).ToList(); if (l.Count == 0) continue;
                sb.AppendLine($"       {nome,-22} {l.Count,5}  flick do matador {l.Average(a => a.flick):0.0}°, dist {l.Average(a => a.dist):0}, vítima tinha visto {Pct(l.Count(a => a.vitimaViu), l.Count):0}%, vítima atirou {Pct(l.Count(a => a.vitimaAtirou), l.Count):0}%, vítima andando {Pct(l.Count(a => a.vitAndando), l.Count):0}%, tempo do duelo {l.Where(a => a.matViuHa >= 0).Select(a => a.matViuHa).DefaultIfEmpty(0).Average():0.00} s");
            }
            Tabela("DUELOS POR SITUAÇÃO", a => (a.atiradorTR ? "TR " : "CT ") + a.situAtirador, a => (a.atiradorTR ? "CT " : "TR ") + a.situVitima, 30);
            sb.AppendLine("  QUEM MATA QUEM NAS POSIÇÕES (top 14: abates, distância, mira do matador, TR parado/vítima andando):");
            foreach (var g in abs.Where(a => a.holdAtirador != null).GroupBy(a => $"{a.holdAtirador} > {a.holdVitima ?? a.faseVitima}").OrderByDescending(g => g.Count()).Take(14))
                sb.AppendLine($"       {g.Key,-52} {g.Count(),4}  dist {g.Average(a => a.dist),3:0}  flick {g.Average(a => a.flick),3:0}°  vítima viu {Pct(g.Count(a => a.vitimaViu), g.Count()),3:0}%  costas {Pct(g.Count(a => a.foraDaVisao), g.Count()),3:0}%  de ({g.Average(a => a.posAtirador.x) / 25:0.0},{g.Average(a => a.posAtirador.y) / 25:0.0}) em ({g.Average(a => a.posVitima.x) / 25:0.0},{g.Average(a => a.posVitima.y) / 25:0.0})");
            Tabela("DUELOS POR POSIÇÃO", a => a.holdAtirador, a => a.holdVitima, 20);
            return sb.ToString();
        }
    }
}
