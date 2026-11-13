using System;
using System.Collections.Generic;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO — o CAMINHO é uma escolha (flag caminho2Ligado; PLANO_SIMULACAO.md §9.1, fala do Gabriel de 12/11: "é sempre pela
    /// base CT, eles pensam pelo caminho mais rápido em vez de um caminho mais seguro, que surpreenda, que pela crença seja melhor").
    /// O cérebro velho só tinha o caminho mais curto da malha, e os pontos de girar e de juntar do CT ficavam todos na base CT.
    /// Aqui o jogador compara o caminho direto com as voltas possíveis (praça, conector, rato, mercado, base) e fica com o de MENOR
    /// CUSTO, em segundos: o tempo de andar + o risco que ele acredita ter NO CAMINHO (o que está no destino vale igual pra todas as
    /// opções e não entra) + o quanto os colegas já foram por ali (chegar de dois lados é o cruzado; todos pela mesma porta é o que o
    /// adversário espera) − o gosto fixo do jogador por aquele caminho. Avaliação, não sorteio: com a mesma situação o mesmo jogador
    /// faz o mesmo; muda quando a crença, o relógio ou os colegas mudam.
    /// </summary>
    public static class Caminho
    {
        public static bool Ligado => TeamBrain.EXPF.Contains("caminho2Ligado");
        /// <summary>Lugares por onde dá pra dar a volta entre os dois lados do mapa (nome da área no MapData). Só Vila por enquanto.
        /// O janelão é sala sem saída: não é passagem.</summary>
        static readonly string[] VIAS = { "CT", "Praca", "Conector", "Rato", "Mercado" };
        const float SEG_POR_INIMIGO = 6f;    // um inimigo acreditado no caminho vale 6 s de volta
        const float SEG_POR_COLEGA = 2f;     // cada colega que já foi por ali há pouco vale 2 s (variar e chegar de outro lado)
        const float SEG_DO_GOSTO = 1.5f;     // mania do jogador: até ±0,75 s
        const float PERTO_DO_DESTINO = 280f; // o que está a menos disso do destino é risco do destino, igual pra todas as opções

        /// <summary>O que o time lembra das escolhas de caminho: quem foi por onde há pouco, e o risco de cada via neste instante
        /// (a crença é do time: calcula-se uma vez por tique, não por jogador).</summary>
        public class Memoria
        {
            public readonly List<(float t, string via)> recentes = new List<(float, string)>();
            public float riscoT = -99; public readonly Dictionary<string, float> risco = new Dictionary<string, float>();
            public int Ja(string v) { int n = 0; foreach (var x in recentes) if (x.via == v) n++; return n; }
        }

        static float Mania(Agent a, string assunto) => Jeito.De(a.name + "|caminho|" + assunto);

        /// <summary>Pontos de juntar pro retake com um ponto a mais do OUTRO lado do bomb (o A pelo conector, o B pelo rato): com ele o
        /// retake pode vir de dois lados de verdade; antes os pontos eram todos na base CT.</summary>
        static V2[] estA, estB;
        public static V2[] EstagiosRetake(char site)
        {
            if (MapData.MAPA != "vila") return Places.RetakeStage(site);
            var g = site == 'A' ? estA : estB;
            if (g != null) return g;
            var b = Places.RetakeStage(site); var r = new V2[b.Length + 1]; Array.Copy(b, r, b.Length);
            r[b.Length] = site == 'A' ? Places.CT_Conector.pos : Places.CT_Rato.pos;
            if (site == 'A') estA = r; else estB = r;
            return r;
        }

        /// <summary>DAR A VOLTA: a via mais curta até o destino que NÃO passa a menos de <paramref name="raio"/> de um lugar (a smoke
        /// do inimigo, o fogo, a linha onde o colega caiu). Devolve o nome da via (ou null se não há volta) e quantos segundos ela
        /// custa a mais que o caminho direto.</summary>
        public static string Volta(Agent a, V2 destino, V2 evita, float raio, out float segAMais)
        {
            segAMais = 0; if (MapData.MAPA != "vila") return null;
            float direto = NavGrid.PathLen(a.pos, destino); if (float.IsInfinity(direto) || float.IsNaN(direto)) return null;
            string melhor = null; float ml = float.PositiveInfinity;
            foreach (var v in VIAS)
            {
                V2 c = Estrategias.Centro(v);
                if ((c - a.pos).Len < 160 || (c - destino).Len < 160 || (c - evita).Len < raio + 60) continue;
                float l = NavGrid.PathLen(a.pos, c) + NavGrid.PathLen(c, destino);
                if (float.IsInfinity(l) || float.IsNaN(l) || l > direto * 2.2f + 250f || l >= ml) continue;
                var c1 = NavGrid.Path(a.pos, c, 0); var c2 = NavGrid.Path(c, destino, 0); if (c1 == null || c2 == null) continue;
                bool passa = false;
                foreach (var w in c1) if ((w.p - evita).Len < raio) { passa = true; break; }
                if (!passa) foreach (var w in c2) if ((w.p - evita).Len < raio) { passa = true; break; }
                if (passa) continue;
                ml = l; melhor = v;
            }
            if (melhor != null) segAMais = MathF.Max(0f, (ml - direto) / MapCheck.RUN);
            return melhor;
        }

        /// <summary>Pontos do caminho a 1/3 e a 2/3 do comprimento (não do número de pontos: a lista vem com os cantos cortados).</summary>
        static void Tercos(List<NavGrid.Waypoint> cam, V2 de, out V2 p1, out V2 p2)
        {
            float tot = 0; V2 ant = de; foreach (var w in cam) { tot += (w.p - ant).Len; ant = w.p; }
            p1 = p2 = de; float ac = 0; ant = de; bool tem1 = false;
            foreach (var w in cam)
            {
                float s = (w.p - ant).Len;
                if (!tem1 && ac + s >= tot / 3f) { p1 = s > 0.01f ? ant + (w.p - ant) * ((tot / 3f - ac) / s) : w.p; tem1 = true; }
                if (ac + s >= 2f * tot / 3f) { p2 = s > 0.01f ? ant + (w.p - ant) * ((2f * tot / 3f - ac) / s) : w.p; return; }
                ac += s; ant = w.p;
            }
            p2 = ant;
        }

        /// <summary>
        /// Escolhe por onde o jogador vai até o destino e manda o Brain seguir. <paramref name="sobra"/> = quantos segundos a mais que
        /// o caminho direto ele pode gastar (no retake é o que o relógio da bomba deixa). Devolve o nome do caminho.
        /// </summary>
        public static string Aplica(Match m, TeamBrain tb, Agent a, V2 destino, Memoria mem, float sobra = 99f)
        {
            if (MapData.MAPA != "vila") return "direto";
            float t = m.t; mem.recentes.RemoveAll(x => t - x.t > 12f);
            if (mem.riscoT != t) { mem.riscoT = t; mem.risco.Clear(); }
            float direto = NavGrid.PathLen(a.pos, destino);
            if (float.IsInfinity(direto) || float.IsNaN(direto) || direto < 420f) return "direto";   // perto: não há volta que valha
            float run = MapCheck.RUN;
            float Risco(V2 p, float raio) => (p - destino).Len < PERTO_DO_DESTINO ? 0f : tb.PerigoCrenca(p, raio);
            // caminho direto: o risco em dois pontos dele, e por qual via ele já passa (o "direto" de cada um é um caminho diferente)
            float riscoD; string rotulo = "direto"; var cam = NavGrid.Path(a.pos, destino, 0);
            if (cam != null && cam.Count >= 1)
            {
                Tercos(cam, a.pos, out V2 p1, out V2 p2); riscoD = MathF.Max(Risco(p1, 220), Risco(p2, 220));
                foreach (var v in VIAS) { V2 c = Estrategias.Centro(v); if ((c - p1).Len < 150 || (c - p2).Len < 150) { rotulo = v; break; } }
            }
            else riscoD = Risco((a.pos + destino) * 0.5f, 260);
            string melhor = rotulo; float mc = direto / run + SEG_POR_INIMIGO * riscoD + SEG_POR_COLEGA * mem.Ja(rotulo) - SEG_DO_GOSTO * (Mania(a, rotulo) - 0.5f);
            float tempoMelhor = direto / run, riscoMelhor = riscoD; bool volta = false;
            foreach (var v in VIAS)
            {
                if (v == rotulo) continue;
                V2 c = Estrategias.Centro(v);
                if ((c - a.pos).Len < 160 || (c - destino).Len < 160) continue;            // já estou nela ou ela é o destino
                float l = NavGrid.PathLen(a.pos, c) + NavGrid.PathLen(c, destino);
                if (float.IsInfinity(l) || float.IsNaN(l) || l > direto * 1.7f + 180f) continue;   // volta absurda
                if (l < direto * 1.04f) continue;                                           // é o próprio caminho direto
                if ((l - direto) / run > sobra) continue;                                   // o relógio não deixa
                if (!mem.risco.TryGetValue(v, out float rv)) mem.risco[v] = rv = tb.PerigoCrenca(c, 240);
                float risco = (c - destino).Len < PERTO_DO_DESTINO ? 0f : rv;
                float custo = l / run + SEG_POR_INIMIGO * risco + SEG_POR_COLEGA * mem.Ja(v) - SEG_DO_GOSTO * (Mania(a, v) - 0.5f);
                if (custo < mc) { mc = custo; melhor = v; tempoMelhor = l / run; riscoMelhor = risco; volta = true; }
            }
            mem.recentes.Add((t, melhor));
            if (!volta) { a.brain.DefinirRota(null, destino); return melhor; }
            a.brain.DefinirRota(new[] { melhor }, destino);
            string onde = MapData.CALLOUT.TryGetValue(melhor, out var cl) ? cl : melhor;
            float amais = MathF.Max(0f, tempoMelhor - direto / run);
            a.brain.Think($"passo {Places.Na(onde)} ({amais:0.#} s de volta): "
                + (riscoD - riscoMelhor > 0.25f ? $"acredito em {riscoD:0.#} deles no caminho direto" : mem.Ja(rotulo) > 0 ? "os colegas já foram pelo outro: chego de outro lado" : "é o caminho que eu gosto"));
            if (Match.Auditar) m.Aud("caminho escolhido (cérebro novo)", melhor);
            return melhor;
        }
    }
}
