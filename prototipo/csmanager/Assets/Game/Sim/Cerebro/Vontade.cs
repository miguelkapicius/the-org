using System;
using System.Collections.Generic;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO — SEM DADO (semDado5Ligado; o Gabriel, 12/11: "não quero que seja sorteado; é de acordo com cada jogador e com
    /// o que ele pensa"). O cérebro velho decide em mais de cem lugares com "sorteio &lt; chance": a chance sai da situação e da crença
    /// (distância, vida, relógio, quantos ele acredita que estão ali), e o dado escolhe. Aqui o dado sai e entra o JOGADOR:
    ///   • Limiar: cada jogador tem, pra cada assunto (trocar o colega, punir passo, atirar cego...), um ponto fixo de 0 a 1 a partir
    ///     do qual ele faz. A mesma conta de antes vira o VALOR da jogada naquela situação; ele faz quando o valor passa do ponto
    ///     dele. Um jogador troca quase sempre, outro só quando a troca é clara — e é sempre o mesmo jogador.
    ///   • Vontade que junta: perguntado de novo sobre o mesmo assunto em pouco tempo (a situação continua ali), o ponto dele
    ///     baixa — quem esperou, esperou, uma hora vai. A vontade esfria em ~20 s.
    ///   • Entre várias opções com peso: faz a de maior peso × o gosto dele por aquela opção ÷ quantas vezes já repetiu no round.
    /// Na média dos jogadores as frequências ficam perto das de antes (o ponto de cada um é espalhado de 0 a 1), mas ninguém joga dado.
    /// </summary>
    public sealed class Vontade
    {
        public static bool Ligado => TeamBrain.EXPF.Contains("semDado5Ligado");
        /// <summary>Em separado, pra medir: as decisões do IGL no meio do round e a inclinação fixa no lugar do ruído das comparações.</summary>
        public static bool LigadoIgl => TeamBrain.EXPF.Contains("semDadoIgl5Ligado");
        public static bool LigadoRuido => TeamBrain.EXPF.Contains("semRuido5Ligado");
        readonly string dono;
        readonly Dictionary<string, (float n, float t)> insiste = new Dictionary<string, (float, float)>();
        readonly Dictionary<string, int> vezes = new Dictionary<string, int>();
        readonly Dictionary<string, float> ponto = new Dictionary<string, float>();   /* o ponto de cada assunto, calculado uma vez */
        public Vontade(string dono) { this.dono = dono; }

        /// <summary>O ponto do jogador pra este assunto agora (0..1): o que o "dado" valeria. Faz quando o valor da situação passa dele.</summary>
        public float Limiar(string assunto, float t)
        {
            /* faixa larga de propósito (0,06 a 0,94). Medido em 12/11: com a faixa estreita (0,2 a 0,8) as jogadas de valor baixo somem e o
               time fica passivo (travado 68 → 110 por 100 rounds, retake 20% → 7%). O custo da faixa larga: quem tem o ponto baixo num
               assunto faz sempre a jogada rara dele — fica anotado como pendência (o valor de cada jogada tem que vir do Avaliador). */
            if (!ponto.TryGetValue(assunto, out float lim)) ponto[assunto] = lim = 0.06f + 0.88f * Jeito.De(dono + "|quer|" + assunto);
            /* a insistência é de quem ESPERA (a mesma pergunta de novo porque a situação continua). Reação a um fato novo (levei tiro, o
               duelo começou, fiquei cego, o colega caiu) não insiste: cada tiro é um fato, e com a insistência o ponto baixava a cada
               tiro até o jogador sair da linha sempre — o "abre, toma tiro, volta" subia de 0,39 pra 0,51 por round */
            if (Reacao(assunto)) return lim;
            insiste.TryGetValue(assunto, out var i);
            float n = i.n * MathF.Exp(-MathF.Max(0f, t - i.t) / 20f);
            if (i.n <= 0 || t - i.t >= 1f) insiste[assunto] = (n + 1f, t);   /* no máximo uma insistência por segundo: o ponto baixa até ~1/5, não mais */
            return lim * MathF.Max(TeamBrain.ExpNum("vontPiso", 0.19f), MathF.Exp(-n / 12f));   /* vontPisoNNN: até quanto a insistência baixa o ponto (100 = não baixa) */
        }

        static readonly string[] REACOES = { "Atingido", "LevouDecide", "Fight", "Tick", "CegoAnda", "ReacaoColega", "RecargaPro", "OnTeammateDied" };
        static bool Reacao(string assunto) { foreach (var r in REACOES) if (assunto.StartsWith(r, StringComparison.Ordinal)) return true; return false; }

        /// <summary>Entre opções com peso: a de maior peso × gosto do jogador, com custo de repetir a mesma no round.</summary>
        public int Melhor(string assunto, float[] w)
        {
            int m = w.Length - 1; float mv = float.NegativeInfinity;   /* nenhuma com peso: a última, como o sorteio fazia */
            for (int k = 0; k < w.Length; k++)
            {
                if (w[k] <= 0) continue;
                vezes.TryGetValue(assunto + "|" + k, out int vz);
                float v = w[k] * (0.75f + 0.5f * Jeito.De(dono + "|gosto|" + assunto + "|" + k)) / (1f + 0.5f * vz);
                if (v > mv) { mv = v; m = k; }
            }
            string ch = assunto + "|" + m; vezes.TryGetValue(ch, out int a); vezes[ch] = a + 1;
            return m;
        }

        /// <summary>O tipo de uma chave de "decide uma vez" (tira os números: id, tempo), pra o ponto do jogador ser o do assunto.</summary>
        public static string Tipo(string chave)
        {
            var sb = new System.Text.StringBuilder(chave.Length);
            foreach (char c in chave) if (!char.IsDigit(c) && c != '-') sb.Append(c);
            return sb.ToString().Trim().TrimEnd('_');
        }
    }

    /// <summary>
    /// CÉREBRO NOVO — CRENÇA POR JOGADOR (crencaJog5Ligado; PLANO_SIMULACAO.md §4: "a crença por jogador começa como tabela pequena").
    /// O mapa de calor continua do time (é o que foi falado no rádio). Cada jogador guarda, além dele, o que ELE viu e ouviu:
    ///   • vale na hora, antes de a call sair no rádio (0,4-1,2 s) — e vale mesmo se ele não conseguir falar;
    ///   • dura o que a memória dele dura (6 a 11 s, do jeito dele);
    ///   • o que veio só por call ele pesa pela confiança dele em call (0,75 a 1,0).
    /// Com isso dois jogadores do mesmo time, no mesmo instante, acreditam em coisas diferentes sobre o mesmo lugar.
    /// </summary>
    public sealed class CrencaJogador
    {
        public static bool Ligado => TeamBrain.EXPF.Contains("crencaJog5Ligado");
        struct Ev { public int id; public V2 p; public float t; public bool vi; }
        readonly Ev[] evs = new Ev[10]; int n;
        public readonly float memoria, confiaCall;
        public CrencaJogador(string dono)
        {
            memoria = 6f + 5f * Jeito.De(dono + "|memoria"); confiaCall = 0.75f + 0.25f * Jeito.De(dono + "|confia call");
            for (int i = 0; i < evs.Length; i++) evs[i].t = -999;
        }
        void Poe(int id, V2 p, float t, bool vi)
        {
            int k = -1;
            for (int i = 0; i < evs.Length; i++)
            {   /* o mesmo inimigo (ou, sem dono, o mesmo lugar há pouco) atualiza a linha dele */
                if (id >= 0 ? evs[i].id == id : evs[i].id < 0 && t - evs[i].t < 2f && (evs[i].p - p).Len < 120) { k = i; break; }
            }
            if (k < 0) { k = 0; for (int i = 1; i < evs.Length; i++) if (evs[i].t < evs[k].t) k = i; }
            if (evs[k].t <= -900) n++;
            evs[k] = new Ev { id = id, p = p, t = t, vi = vi };
        }
        public void Vi(int id, V2 p, float t) => Poe(id, p, t, true);
        public void Ouvi(V2 p, float t) => Poe(-1, p, t, false);
        public void Morreu(int id) { for (int i = 0; i < evs.Length; i++) if (evs[i].id == id) evs[i].t = -999; }

        /// <summary>Quantos inimigos EU acredito que estão num raio: o maior entre o que o time sabe (pesado pela minha confiança em
        /// call) e o que eu mesmo vi/ouvi e ainda lembro.</summary>
        public float Esperados(Intel time, V2 p, float raio, float t)
        {
            float doTime = time.Esperados(p, raio);
            if (n == 0) return doTime * confiaCall;
            float meu = 0; bool tenho = false;
            for (int i = 0; i < evs.Length; i++)
            {
                float idade = t - evs[i].t; if (idade > memoria * 2f || idade < 0) continue;
                /* ele pode ter andado: o raio cresce com a idade (60 u/s, até 250) */
                if ((evs[i].p - p).Len > raio + MathF.Min(250f, 60f * idade)) continue;
                if (evs[i].id >= 0 && time.Movido(evs[i].id, evs[i].p, evs[i].t)) continue;   /* o time já soube dele noutro lugar depois */
                if (!evs[i].vi)
                {   /* o passo de quem eu também vi ali não é outro inimigo */
                    bool mesmo = false;
                    for (int j = 0; j < evs.Length; j++) if (evs[j].vi && MathF.Abs(evs[j].t - evs[i].t) < 3f && (evs[j].p - evs[i].p).Len < 200) { mesmo = true; break; }
                    if (mesmo) continue;
                }
                meu += (evs[i].vi ? 1f : 0.8f) * MathF.Exp(-idade / memoria); tenho = true;
            }
            return tenho ? MathF.Max(doTime, meu) : doTime * confiaCall;
        }
    }
}
