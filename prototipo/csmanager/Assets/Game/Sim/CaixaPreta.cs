using System;
using System.Globalization;
using System.IO;
using System.Text;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Caixa-preta da partida (sessão a fundo, 2026-11-11): o que o Gabriel vê no jogo não se repete no Headless (o jogo tem
    /// cache global e orçamento de caminho por tempo real), então o próprio jogo grava cada round: 4x por segundo, a posição,
    /// a mira, a vida, a arma, a fase, a ordem e o pensamento de cada bot. Um arquivo por round em
    /// Logs/partidas/&lt;sessão&gt;/&lt;semente do round&gt;.tsv (ao lado da pasta do exe). Quando ele filmar algo estranho, abre-se o
    /// round exato. Só lê o estado; ligada no jogo (fora do modo NavGrid.PorRound) ou com -caixaPreta; -semCaixaPreta desliga.
    ///
    /// Linhas: "R seed" · "T t bombPlantada bx by" · "A id nome time arma x y z miraGraus hp vel fonteMira" ·
    /// "F id fase" / "O id ordem" / "P id pensamento" (só quando muda) · "X t" (o round acabou aqui; o que vem depois é pós-round) · "FIM vencedor motivo".
    /// </summary>
    public static class CaixaPreta
    {
        static readonly bool pedida, negada; static string pasta; static bool falhou;
        [ThreadStatic] static StringBuilder sb; [ThreadStatic] static Match atual; [ThreadStatic] static float proxT;
        [ThreadStatic] static string[] fase, ordem, pensa; [ThreadStatic] static bool fechado, gravouNoFim;

        static CaixaPreta()
        {
            try { var a = Environment.GetCommandLineArgs(); pedida = Array.IndexOf(a, "-caixaPreta") >= 0; negada = Array.IndexOf(a, "-semCaixaPreta") >= 0; } catch { }
        }

        public static bool Ligada => !negada && !falhou && (pedida || !NavGrid.PorRound);

        static string F(float v) => v.ToString("0.#", CultureInfo.InvariantCulture);
        static string L(string s) => s == null ? "" : s.Replace('\t', ' ').Replace('\n', ' ').Replace('\r', ' ');

        /// <summary>Chamado no fim de cada passo do Match (round correndo ou pós-round).</summary>
        public static void Passo(Match m)
        {
            if (!Ligada) return;
            try
            {
                if (m != atual)
                {
                    if (atual != null && !fechado) Fecha(atual);   /* o round anterior não chegou ao fim do pós-round: grava o que tem */
                    atual = m; sb = new StringBuilder(1 << 18); proxT = -999; fechado = false; gravouNoFim = false;
                    fase = new string[m.agents.Count]; ordem = new string[m.agents.Count]; pensa = new string[m.agents.Count];
                    sb.Append("R\t").Append(m.seed).Append('\n');
                }
                if (fechado) return;
                if (m.t >= proxT)
                {
                    proxT = m.t + 0.25f;
                    sb.Append("T\t").Append(F(m.t)).Append('\t').Append(m.bombPlanted ? 1 : 0).Append('\t').Append(F(m.bombPos.x)).Append('\t').Append(F(m.bombPos.y)).Append('\n');
                    for (int k = 0; k < 2; k++) { string ct = m.igl[k]?.col?.TextoSeMudou(); if (ct != null) sb.Append("C\t").Append(k == (int)Team.T ? "TR" : "CT").Append('\t').Append(L(ct)).Append('\n'); }
                    for (int i = 0; i < m.agents.Count; i++)
                    {
                        var a = m.agents[i]; if (!a.alive) continue; var b = a.brain;
                        sb.Append("A\t").Append(a.id).Append('\t').Append(a.name).Append('\t').Append(a.IsT ? "TR" : "CT").Append('\t').Append(a.gunId).Append('\t')
                          .Append(F(a.pos.x)).Append('\t').Append(F(a.pos.y)).Append('\t').Append(F(a.z)).Append('\t').Append(F(a.aim * 57.29578f)).Append('\t')
                          .Append(a.hp).Append('\t').Append(F(a.Speed)).Append('\t').Append(L(b.fonteMira)).Append('\n');
                        if (b.fase != fase[i]) { fase[i] = b.fase; sb.Append("F\t").Append(a.id).Append('\t').Append(L(b.fase)).Append('\n'); }
                        if (b.order.name != ordem[i]) { ordem[i] = b.order.name; sb.Append("O\t").Append(a.id).Append('\t').Append(L(b.order.name)).Append('\n'); }
                        if (b.thought != pensa[i]) { pensa[i] = b.thought; sb.Append("P\t").Append(a.id).Append('\t').Append(L(b.thought)).Append('\n'); }
                    }
                }
                if (m.over && !gravouNoFim) { gravouNoFim = true; sb.Append("X\t").Append(F(m.t)).Append('\n'); Grava(m); }   /* já fica no disco no fim do round; o pós-round regrava por cima */
                if (m.over && m.posRound && m.t - m.posRoundDesde > Match.POS_ROUND - 0.3f) Fecha(m);
            }
            catch { falhou = true; }
        }

        /// <summary>Fecha o round (grava a versão final, com o pós-round).</summary>
        public static void Fecha(Match m)
        {
            if (!Ligada || m != atual || fechado || sb == null) return;
            fechado = true; Grava(m); sb = null;
        }

        static void Grava(Match m)
        {
            try
            {
                if (pasta == null)
                {
                    string exe = System.Diagnostics.Process.GetCurrentProcess().MainModule.FileName;
                    string raiz = Path.GetDirectoryName(Path.GetDirectoryName(exe));   /* a pasta do exe fica dentro do projeto (Build, Build_novoN) */
                    if (!Directory.Exists(Path.Combine(raiz, "Logs"))) raiz = Path.GetDirectoryName(exe);
                    pasta = Path.Combine(raiz, "Logs", "partidas", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss"));
                    Directory.CreateDirectory(pasta);
                    try { File.WriteAllText(Path.Combine(pasta, "_exp.txt"), TeamBrain.EXP ?? ""); } catch { }
                }
                File.WriteAllText(Path.Combine(pasta, m.seed.ToString(CultureInfo.InvariantCulture) + ".tsv"),
                    sb.ToString() + "FIM\t" + L(m.winner) + "\t" + L(m.reason) + "\n");
            }
            catch { falhou = true; }
        }
    }
}
