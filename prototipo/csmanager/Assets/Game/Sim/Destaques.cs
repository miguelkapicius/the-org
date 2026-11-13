using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Sim
{
    /// <summary>
    /// Os lances de um round e a nota de cada um (decisão 0020): a mesma conta do replay de melhores momentos da transmissão,
    /// aqui na simulação para a jogada da partida valer fora da cena 3D. Lance = abates a menos de 3,5 s um do outro; nota pelo
    /// que o killfeed do CS2 destaca (HS, sem luneta, pela smoke/parede, no ar, cego), AWP, clutch, abertura e multi-kill.
    /// </summary>
    public static class Destaques
    {
        public const float JANELA = 3.5f;

        /// <summary>Nota de um abate. <paramref name="valido"/>: abate de inimigo (não suicídio nem aliado).</summary>
        public static float NotaAbate(Match.Abate ab, bool valido, bool abertura, bool clutch, int multi)
        {
            if (!valido) return 4;
            float nt = 10;
            if (ab.hs) nt += 3; if (ab.semLuneta) nt += 6; if (ab.atravesSmoke) nt += 4; if (ab.parede) nt += 6;
            if (ab.noAr) nt += 5; if (ab.cegoQuemMatou) nt += 5; if (ab.arma == "awp") nt += 5;
            if (clutch) nt += 8;
            if (abertura) nt += 6;
            if (multi > 1) nt += 8;
            return nt;
        }

        /// <summary>Um lance de um round, pelo jogador que mais matou nele.</summary>
        public class Lance
        {
            public int round; public string jogador; public bool tr;   // tr: o jogador estava no TR nesse round
            public float nota; public int abates, clutchContra; public string arma; public bool abertura;
            public readonly List<string> marcas = new List<string>();
            public Partida.Time time;                                  // preenchido pela Partida

            public string Titulo => abates >= 5 ? "ACE" : abates >= 2 ? abates + " abates" : "Abate";
            /// <summary>"ACE de BRASA no round 17 · AK-47 · clutch 1v3 · na cabeça".</summary>
            public string Descricao
            {
                get
                {
                    var p = new List<string> { WeaponStats.Nome(arma) };
                    if (clutchContra > 0) p.Add("clutch 1v" + clutchContra);
                    if (abertura) p.Add("abriu o round");
                    p.AddRange(marcas);
                    return Titulo + " de " + jogador + " no round " + (round + 1) + " · " + string.Join(" · ", p);
                }
            }
        }

        /// <summary>Os lances do round que acabou, com a nota de cada um (só os que têm abate de inimigo).</summary>
        public static List<Lance> DoRound(Match m)
        {
            var res = new List<Lance>();
            // só o que aconteceu até o round ser decidido (o pós-round, caçando quem salva a arma, não conta)
            var abs = m.abates.Where(x => x.v != null && (m.fimEm < 0 || x.t <= m.fimEm + 0.05f)).OrderBy(x => x.t).ToList();
            int vT = m.agents.Count(a => a.IsT), vC = m.agents.Count - vT;
            var grupo = new List<(Match.Abate ab, float nota, bool valido, bool clutch, int vivosInimigos, bool abertura)>();
            var deQuem = new Dictionary<Agent, int>();
            float ultimo = -99; bool primeiro = true;
            void Fecha()
            {
                if (grupo.Count == 0) return;
                var porJogador = grupo.Where(g => g.valido).GroupBy(g => g.ab.by).OrderByDescending(g => g.Count()).ThenByDescending(g => g.Sum(x => x.nota)).FirstOrDefault();
                if (porJogador != null)
                {
                    // a nota é só dos abates DESTE jogador no lance (o replay soma a troca de tiros inteira; a jogada é de uma pessoa)
                    var l = new Lance { jogador = porJogador.Key.name, tr = porJogador.Key.IsT, nota = porJogador.Sum(g => g.nota), abates = porJogador.Count(), arma = porJogador.Last().ab.arma };
                    var cl = porJogador.FirstOrDefault(g => g.clutch); if (cl.ab != null) l.clutchContra = cl.vivosInimigos;
                    l.abertura = porJogador.Any(g => g.abertura);
                    void Marca(bool tem, string txt) { if (tem && !l.marcas.Contains(txt)) l.marcas.Add(txt); }
                    foreach (var g in porJogador)
                    {
                        Marca(g.ab.semLuneta, "sem luneta"); Marca(g.ab.atravesSmoke, "pela smoke"); Marca(g.ab.parede, "pela parede");
                        Marca(g.ab.noAr, "no ar"); Marca(g.ab.cegoQuemMatou, "cego");
                    }
                    if (porJogador.All(g => g.ab.hs)) l.marcas.Add(porJogador.Count() > 1 ? "tudo na cabeça" : "na cabeça");
                    res.Add(l);
                }
                grupo.Clear(); deQuem.Clear();
            }
            foreach (var ab in abs)
            {
                if (ab.t - ultimo > JANELA) Fecha();
                ultimo = ab.t;
                bool valido = ab.by != null && ab.by != ab.v && !ab.aliado;
                int vivosMatador = !valido ? 0 : ab.by.IsT ? vT : vC, vivosInimigos = !valido ? 0 : ab.by.IsT ? vC : vT;
                if (ab.v.IsT) vT--; else vC--;
                int n = 0; if (valido) deQuem[ab.by] = n = (deQuem.TryGetValue(ab.by, out var q) ? q : 0) + 1;
                bool abertura = valido && primeiro && n == 1, clutch = valido && vivosMatador == 1;
                grupo.Add((ab, NotaAbate(ab, valido, abertura, clutch, n), valido, clutch, vivosInimigos, abertura));
                primeiro = false;
            }
            Fecha();
            return res;
        }
    }
}
