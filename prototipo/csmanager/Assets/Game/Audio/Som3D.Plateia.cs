using System.Collections.Generic;
using UnityEngine;
using CSM.Sim;

namespace CSM.Audio
{
    /// <summary>
    /// Torcida de campeonato LAN (usuário 2026-11-07): cama de multidão o tempo todo + gritos nos lances.
    /// Cama: 15 trechos de murmúrio de arena (torcida/murmurio_0-14, ~20 s, laço sem emenda). A semente da partida sorteia a
    /// ordem; a cada ~9 s entra um trecho novo (fade de 3 s) e o mais velho sai, então tocam sempre ~2 camadas diferentes
    /// cruzando: a mesma combinação quase nunca se repete e cada partida tem uma sequência própria.
    /// Gritos: início da partida, multi-kill (3ª e 4ª morte do mesmo jogador no round, na hora), ace (5ª, na hora; "absurdo"
    /// se as 5 vieram em até 10 s ou num 1v5), jogada (duas mortes em 1 s, desarme no último segundo ou ninja), "ohhh" (o
    /// clutch que tinha começado bem e morreu), clutch (ganhou 1vX com X ≥ 2), aplauso de round, e a ovação do fim.
    /// Volume dos gritos subiu de 0,5 pra 0,9 (usuário: "muito baixo").
    /// Uso (PartidaAoVivo3D): PlateiaComecar(seed, !Vitrine) na partida nova; PlateiaQuadro(m) a cada quadro ao vivo (fora do
    /// replay); PlateiaFimRound(m) no fim do round (no lugar do Torcida() antigo); PlateiaFimPartida() quando a partida acaba.
    /// </summary>
    public partial class Som3D
    {
        /// <summary>Hoje ligada na partida normal (usuário: "implementa na partida que a gente tem hoje"). Quando existirem
        /// campeonatos, quem monta a partida liga só pra LAN/campeonato grande.</summary>
        public static bool plateiaLigada = true;
        /// <summary>Mix (usuário 2026-11-08: "a torcida está muito alta e o resto do jogo fica baixo"). Medido: tiro de AK na
        /// distância da câmera chega ~-25 dBFS; a cama (2 camadas) estava ~-22 o tempo todo e os gritos ~-11. Agora: cama ~-31 (nível
        /// do ambiente da Vila), -6 dB a mais durante tiroteio (como na transmissão) e gritos ~-16/-18 (ainda acima dos antigos -17
        /// só pro clutch/ace, que eram "muito baixos").</summary>
        public static float VOL_CAMA = 0.3f, VOL_REACAO = 0.45f, DUCK_TIRO_DB = -6f;
        float plUltTiro = -99, plDuck = 1f;
        const int CAMADAS = 3;

        class Camada { public AudioSource src; public float t0, dur, g; }
        Camada[] cama; List<int> ordemCama; int proxCama; float proxEntrada; bool plOn; System.Random plRng;

        Match plM; int plFeed; int plVivosT, plVivosCT; bool plAce, plQuase, plDefVisto;
        readonly Dictionary<string, List<float>> plAbates = new Dictionary<string, List<float>>();
        string plClutcher; Team plClutchTime; int plClutchX, plClutchAbates; float plUltReacao = -99;

        public static void PlateiaComecar(int seed, bool ligar = true)
        {
            var s = Eu(); if (s == null) return;
            s.PararCama(); s.plM = null;
            if (!ligar || !plateiaLigada) return;
            var cs = s.Clipes("torcida/murmurio"); if (cs.Length == 0) return;
            s.plRng = new System.Random(seed * 7919 + 17);
            s.Embaralhar(cs.Length);
            if (s.cama == null)
            {
                s.cama = new Camada[CAMADAS];
                for (int k = 0; k < CAMADAS; k++)
                {
                    var src = new GameObject("plateia_" + k).AddComponent<AudioSource>(); src.transform.SetParent(s.transform);
                    src.loop = true; src.spatialBlend = 0; src.priority = 190; src.playOnAwake = false;
                    s.ambientes["plateia_" + k] = src;   // o "mudo" do LateUpdate zera junto com o ambiente
                    s.cama[k] = new Camada { src = src };
                }
            }
            float agora = Time.unscaledTime;
            s.EntraCama(agora - 3f); s.EntraCama(agora - 9f);   // já começa cheio: duas camadas no ar
            s.proxEntrada = agora + 4f; s.plOn = true;
            Debug.Log($"[Som3D] torcida: cama ligada, {cs.Length} trechos, ordem {string.Join(",", s.ordemCama)}");
            s.Reacao("inicio", 1f);
        }

        public static void PlateiaParar() => Eu()?.PararCama();

        void PararCama()
        {
            plOn = false;
            if (cama != null) foreach (var c in cama) { c.src.Stop(); c.dur = 0; }
        }

        void Embaralhar(int n)
        {
            ordemCama = new List<int>();
            for (int i = 0; i < n; i++) ordemCama.Insert(plRng.Next(ordemCama.Count + 1), i);
            proxCama = 0;
        }

        bool EntraCama(float t0)
        {
            var cs = Clipes("torcida/murmurio"); if (cs.Length == 0) return false;
            Camada livre = null;
            foreach (var c in cama) if (!c.src.isPlaying) { livre = c; break; }
            if (livre == null) return false;
            if (proxCama >= ordemCama.Count) Embaralhar(cs.Length);
            var clip = cs[ordemCama[proxCama++] % cs.Length];
            livre.src.clip = clip; livre.src.time = (float)plRng.NextDouble() * clip.length * 0.95f;
            livre.t0 = t0; livre.dur = 16f + (float)plRng.NextDouble() * 5f; livre.g = 0.7f + (float)plRng.NextDouble() * 0.3f;
            livre.src.volume = 0; livre.src.Play();
            return true;
        }

        /// <summary>Do LateUpdate (tempo real: a cama segue no replay e na pausa, como numa arena).</summary>
        void PlateiaTick()
        {
            if (!plOn || cama == null) return;
            float agora = Time.unscaledTime;
            if (agora >= proxEntrada) proxEntrada = agora + (EntraCama(agora) ? 8f + (float)plRng.NextDouble() * 2.5f : 0.5f);
            // tiroteio: a cama desce DUCK_TIRO_DB em ~0,3 s e volta em ~1,5 s depois do último tiro
            float alvo = agora - plUltTiro < 1.2f ? Mathf.Pow(10, DUCK_TIRO_DB / 20f) : 1f;
            plDuck = Mathf.MoveTowards(plDuck, alvo, Time.unscaledDeltaTime * (alvo < plDuck ? 1.6f : 0.35f));
            foreach (var c in cama)
            {
                if (!c.src.isPlaying) continue;
                float idade = agora - c.t0;
                if (idade >= c.dur) { c.src.Stop(); continue; }
                float env = Mathf.Clamp01(idade / 3f) * Mathf.Clamp01((c.dur - idade) / 3f);
                c.src.volume = env * c.g * VOL_CAMA * plDuck * volumeGeral;
            }
        }

        void Reacao(string nome, float g)
        {
            if (!plateiaLigada) return;
            TocarNoOuvido("torcida/" + nome, VOL_REACAO * g); plUltReacao = Time.unscaledTime;
            string quando = plM != null ? " round t=" + plM.t.ToString("0.0") : "";
            Debug.Log("[Som3D] torcida: " + nome + " (vol " + (VOL_REACAO * g).ToString("0.00") + ")" + quando);   // validação pelo log do exe
        }

        /// <summary>Todo quadro ao vivo: lê o killfeed novo e grita na hora (multi-kill, ace, jogada, "ohhh").</summary>
        public static void PlateiaQuadro(Match m)
        {
            var s = Eu(); if (s == null || !s.plOn || m == null) return;
            s.LerRound(m);
        }

        void LerRound(Match m)
        {
            if (m != plM)
            {
                plM = m; plFeed = 0; plAbates.Clear(); plAce = plQuase = plDefVisto = false; plClutcher = null;
                plVivosT = plVivosCT = 0; foreach (var a in m.agents) { if (a.IsT) plVivosT++; else plVivosCT++; }
            }
            float agora = Time.unscaledTime;
            while (plFeed < m.feed.Count)
            {
                var (t, s0) = m.feed[plFeed++];
                int a0 = s0.IndexOf(" ["), a1 = s0.IndexOf("] "); if (a0 <= 0 || a1 <= a0) continue;
                string k = s0.Substring(0, a0), v = s0.Substring(a1 + 2).Replace(" (HS)", "");
                var va = m.agents.Find(z => z.name == v); var ka = m.agents.Find(z => z.name == k);
                if (va == null) continue;
                if (va.IsT) plVivosT--; else plVivosCT--;
                bool inimigo = ka != null && ka.IsT != va.IsT;
                if (inimigo && k == plClutcher) plClutchAbates++;
                if (v == plClutcher && plClutchAbates >= 1 && !plQuase) { plQuase = true; Reacao("quase", 0.9f); }
                if (plClutcher == null)
                {   // um time ficou com 1 contra 2+: começou um clutch (o último vivo é o "clutcher")
                    foreach (var (time, vivos, outros) in new[] { (Team.T, plVivosT, plVivosCT), (Team.CT, plVivosCT, plVivosT) })
                        if (vivos == 1 && outros >= 2)
                        {
                            var c = m.agents.Find(z => z.alive && z.team == time && z.name != v);
                            if (c != null) { plClutcher = c.name; plClutchTime = time; plClutchX = outros; plClutchAbates = 0; }
                        }
                }
                if (!inimigo) continue;
                if (!plAbates.TryGetValue(k, out var l)) plAbates[k] = l = new List<float>();
                l.Add(t); int n = l.Count;
                if (n == 5)
                {
                    bool absurdo = l[4] - l[0] <= 10f || (k == plClutcher && plClutchX >= 5);
                    plAce = true; Reacao(absurdo && Clipes("torcida/ace_absurdo").Length > 0 ? "ace_absurdo" : "ace", 1f);
                }
                else if (n == 3 || n == 4) Reacao("multikill", n == 4 ? 1f : 0.85f);
                else if (n == 2 && t - l[0] <= 1f && agora - plUltReacao > 6f) Reacao("jogada", 0.8f);
            }
            if (m.defused && !plDefVisto)
            {   // desarme no último segundo, ou com TR vivo (ninja): a arena reage
                plDefVisto = true;
                if (m.BombLeft <= 2f || plVivosT > 0) Reacao("jogada", 1f);
            }
        }

        /// <summary>Fim do round ao vivo (no lugar do Torcida() antigo do PartidaAoVivo3D): clutch ou aplauso do round.</summary>
        public static void PlateiaFimRound(Match m)
        {
            var s = Eu(); if (s == null || !s.plOn || m == null) return;
            s.LerRound(m);
            bool venceuT = m.winner == "TR";
            var c = s.plClutcher != null ? m.agents.Find(z => z.name == s.plClutcher) : null;
            if (s.plAce) return;   // o ace já levantou a arena na 5ª morte (inclusive o 1v5): não grita de novo
            if (c != null && c.alive && (s.plClutchTime == Team.T) == venceuT && s.plClutchX >= 2) s.Reacao("clutch", 1f);
            else if (Time.unscaledTime - s.plUltReacao > 2.5f) s.Reacao("round", 0.75f);
        }

        /// <summary>A partida acabou: ovação.</summary>
        public static void PlateiaFimPartida() { var s = Eu(); if (s != null && s.plOn) s.Reacao("fim", 1f); }
    }
}
