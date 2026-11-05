using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;

namespace CSM.Audio
{
    /// <summary>
    /// Som da partida (sessão "Sons das armas"). Toca os sons autorais de Resources/Sons/&lt;grupo&gt;/&lt;nome&gt;_K.wav com a
    /// mesma lógica do CS2 (soundevents do jogo lidos como referência em Tools/sons/le_eventos.py):
    ///  - camada PERTO e camada DISTANTE separadas, cada uma com sua curva de volume pela distância em unidades do CS;
    ///  - variações sorteadas sem repetir a anterior, pitch ±2%;
    ///  - limite por atirador: o tiro novo apaga o anterior em 0,1 s (fadetime do CS2), teto global de vozes;
    ///  - oclusão: parede entre o ouvido e a fonte abafa (passa-baixa) e baixa o volume;
    ///  - ar: agudo cai com a distância.
    /// O OUVIDO é o ponto do chão que a câmera da transmissão olha (achado sozinho pelo raio da câmera), não a câmera:
    /// a câmera fica longe e alta; quem assiste "está" no lance.
    /// Chamadas são estáticas e seguras: sem clipe = silêncio (os sons vão chegando aos poucos).
    /// </summary>
    public class Som3D : MonoBehaviour
    {
        // 1 unidade Unity = 25 unidades do jogo; 1 do jogo = 2,9 do CS (Match.PULO = 301.99/2.9)
        public const float CS_POR_UNITY = 25f * 2.9f;
        public static float volumeGeral = 1f;
        public static bool mudo, oclusaoLigada = true;
        /// <summary>Cria o Som3D e o ouvido já no início (opcional: a 1ª chamada de som também cria).</summary>
        public static void Iniciar() => Eu();
        /// <summary>Cria o ouvido mesmo com mudo (o som de interface do menu precisa de AudioListener).</summary>
        internal static void GarantirOuvido() { if (!eu) { var go = new GameObject("Som3D"); eu = go.AddComponent<Som3D>(); eu.Montar(); } }

        static Som3D eu;
        Transform ouvido; Camera camUsada;
        readonly List<Voz> vozes = new List<Voz>();
        readonly Dictionary<string, AudioClip[]> clipes = new Dictionary<string, AudioClip[]>();
        readonly Dictionary<string, int> ultimo = new Dictionary<string, int>();
        readonly List<Agendado> agenda = new List<Agendado>();
        float relogio;
        const int VOZES = 48;

        // ---------------- curvas do CS2 (distância em unidades do CS -> volume) ----------------
        public static readonly Vector2[] C_TIRO = { new(0, 1), new(175.7f, 1), new(440, 0.735f), new(1288, 0.331f), new(2500, 0.015f), new(2600, 0) };
        public static readonly Vector2[] C_TIRO_AWP = { new(0, 1), new(175.7f, 1), new(440, 0.735f), new(1288, 0.331f), new(2800, 0.048f), new(3000, 0) };
        public static readonly Vector2[] C_DISTANTE = { new(800, 0), new(2335.8f, 0.54f), new(2868.6f, 0.096f) };
        public static readonly Vector2[] C_DISTANTE_PIST = { new(800, 0), new(2354.3f, 0.337f), new(2868.6f, 0.096f) };
        public static readonly Vector2[] C_FOLEY = { new(0, 1), new(396.9f, 1), new(1106.9f, 0) };
        public static readonly Vector2[] C_QUIQUE = { new(0, 1), new(1700, 0) };
        public static readonly Vector2[] C_HE = { new(0, 1), new(231.4f, 1), new(779.1f, 0.569f), new(2700, 0) };
        public static readonly Vector2[] C_HE_DIST = { new(504, 0), new(944, 1), new(2800, 0.343f) };
        public static readonly Vector2[] C_FLASH = { new(0, 1), new(84, 1), new(2740, 0) };
        public static readonly Vector2[] C_FLASH_DIST = { new(504, 0.71f), new(944, 1), new(2800, 0) };
        // C4: o espectador ouve de quase o mapa todo (usuário, 2026-11-04). Só áudio: a audição dos bots é da Sim, não muda.
        public static readonly Vector2[] C_C4 = { new(0, 1), new(600, 0.9f), new(1800, 0.6f), new(3500, 0.38f), new(6000, 0.22f), new(9000, 0.15f) };
        public static readonly Vector2[] C_C4_FOLEY = { new(0, 1), new(500, 0.9f), new(1800, 0.4f), new(3000, 0) };   // teclado e começo do desarme
        public static readonly Vector2[] C_C4_EXPLODE = { new(0, 1), new(800, 0.9f), new(3000, 0.6f) };
        public static readonly Vector2[] C_FOGO = { new(0, 1), new(300, 0.8f), new(1200, 0) };
        public static readonly Vector2[] C_IMPACTO = { new(0, 1), new(300, 0.7f), new(1200, 0) };

        static float Curva(Vector2[] c, float d)
        {
            if (d <= c[0].x) return c[0].y;
            for (int i = 1; i < c.Length; i++)
                if (d <= c[i].x) { float u = (d - c[i - 1].x) / Mathf.Max(1e-3f, c[i].x - c[i - 1].x); return Mathf.Lerp(c[i - 1].y, c[i].y, u); }
            return c[c.Length - 1].y;
        }

        class Voz
        {
            public AudioSource src; public AudioLowPassFilter lp; public Transform tr;
            public Vector2[] curva; public float vol, oclusao, fade = -1, fadeVel; public string dono; public int prioridade; public bool naCabeca;
            public bool Livre => !src.isPlaying;
        }

        class Agendado { public float t; public string nome; public Vector3 pos; public Vector2[] curva; public float vol; public string dono; }

        // ---------------- API (chamar do visual) ----------------

        /// <summary>Tiro: camada perto + distante. quem = id do atirador (limite por atirador).</summary>
        public static void Tiro(Vector3 boca, string arma, int quem = -1)
        {
            var s = Eu(); if (s == null || string.IsNullOrEmpty(arma)) return;
            var (vp, vd, cp, cd) = PerfilTiro(arma);
            string dono = "tiro" + quem;
            s.ApagarDono(dono, 0.1f);   // fadetime_volume_mapping_curve do CS2: o anterior some em 0,1 s
            s.Tocar(arma + "/tiro", boca, cp, vp, dono, 3);
            var vd2 = s.Tocar(arma + "/distante", boca, cd, vd, "dist" + quem, 2);
            // validação (Player.log): resumo a cada 40 tiros — "distante na tela" tem que ser sempre 0
            bool tela = s.NaTela(boca); if (tela) { s.tirosTela++; if (vd2 != null) s.distTela++; } else s.tirosFora++;
            if (s.tirosTela + s.tirosFora >= 40)
            {
                Debug.Log($"[Som3D] tiros: {s.tirosTela} na tela (distante tocou em {s.distTela}), {s.tirosFora} fora da tela");
                s.tirosTela = s.tirosFora = s.distTela = 0;
            }
        }
        int tirosTela, tirosFora, distTela;

        /// <summary>Recarga: as peças (pente sai/entra, ferrolho) agendadas na duração da recarga.</summary>
        public static void Recarga(Vector3 pos, string arma, float duracao, int quem = -1)
        {
            var s = Eu(); if (s == null) return;
            foreach (var (u, peca, v) in PecasRecarga(arma)) s.Agendar(duracao * u, arma + "/" + peca, pos, C_FOLEY, v, "rec" + quem);
        }

        public static void Sacar(Vector3 pos, string arma, int quem = -1) => Eu()?.Tocar(arma + "/sacar", pos, C_FOLEY, 0.5f, "sac" + quem, 1);
        /// <summary>Morreu/trocou de arma: corta recarga e sons de mão pendentes.</summary>
        public static void Parar(int quem) { var s = Eu(); if (s == null) return; s.agenda.RemoveAll(a => a.dono == "rec" + quem); s.ApagarDono("rec" + quem, 0.05f); }

        /// <summary>Bala chegando. Sem acerto, o som é do MATERIAL onde bateu (como no CS): "pedra" (padrão), "madeira",
        /// "terra", "metal" — o mesmo material que o passo usa (MapData.fmat).</summary>
        public static void Impacto(Vector3 p, bool acerto, bool cabeca, bool protegido, string material = null)
        {
            var s = Eu(); if (s == null) return;
            string n;
            if (acerto) n = cabeca ? (protegido ? "impacto/capacete" : "impacto/cabeca") : (protegido ? "impacto/colete" : "impacto/corpo");
            else n = material != null && material != "pedra" && s.Clipes("impacto/" + material).Length > 0 ? "impacto/" + material : "impacto/parede";
            s.Tocar(n, p, C_IMPACTO, cabeca ? 0.9f : 0.6f, null, 1);
        }

        public static void HE(Vector3 p) { var s = Eu(); if (s == null) return; s.Tocar("granada/he", p, C_HE, 1f, null, 4); s.Tocar("granada/he_distante", p, C_HE_DIST, 1f, null, 3); }
        public static void Flash(Vector3 p) { var s = Eu(); if (s == null) return; s.Tocar("granada/flash", p, C_FLASH, 0.8f, null, 4); s.Tocar("granada/flash_distante", p, C_FLASH_DIST, 0.8f, null, 3); }
        public static void Smoke(Vector3 p) => Eu()?.Tocar("granada/smoke", p, C_SMOKE, 0.7f, null, 2);   // sem camada de longe (usuário: não precisa)
        public static readonly Vector2[] C_SMOKE = { new(0, 1), new(800, 0.85f), new(2200, 0.45f), new(3800, 0.15f), new(5000, 0) };   // ouvida mais longe (espectador)
        public static readonly Vector2[] C_SMOKE_DIST = { new(500, 0), new(1100, 0.7f), new(2800, 0.1f) };
        /// <summary>Fogo nasceu. incendiaria = granada do CT (lata de metal estourando), senão garrafa da molotov.</summary>
        public static void Molotov(Vector3 p, bool incendiaria = false)
        {
            var s = Eu(); if (s == null) return; string n = incendiaria ? "granada/incendiaria" : "granada/molotov";
            s.Tocar(n, p, C_HE, 0.9f, null, 3); s.Tocar(n + "_distante", p, C_MOLOTOV_DIST, 0.8f, null, 2);
        }
        public static readonly Vector2[] C_MOLOTOV_DIST = { new(264, 0), new(920, 0.805f), new(2800, 0.048f) };
        public static void FogoFim(Vector3 p) => Eu()?.Tocar("granada/fogo_fim", p, C_FOGO, 0.6f, null, 1);
        public static void SmokeFim(Vector3 p) => Eu()?.Tocar("granada/smoke_fim", p, C_SMOKE, 0.45f, null, 0);
        public static void GranadaSacar(Vector3 p, int quem = -1) => Eu()?.Tocar("granada/sacar", p, C_FOLEY, 0.45f, "gsac" + quem, 1);
        /// <summary>Molotov: isqueiro acendendo o pano antes de jogar (no começo da anim granada_molotov).</summary>
        public static void Isqueiro(Vector3 p, int quem = -1) => Eu()?.Tocar("granada/isqueiro", p, C_FOLEY, 0.55f, "isq" + quem, 1);
        public static void GranadaAcertou(Vector3 p) => Eu()?.Tocar("granada/acertou", p, C_FOLEY, 0.6f, null, 1);
        /// <summary>Todo quadro: posição de cada molotov voando (laço de chama; lista vazia = silêncio).</summary>
        public static void MolotovsVoando(IList<Vector3> pos) { var s = Eu(); if (s == null) return; s.Lacos(s.voos, "granada/molotov_voando", pos, C_QUIQUE, 0.45f); }
        readonly List<Voz> voos = new List<Voz>();
        public static void MolotovNoAr(Vector3 p) => Eu()?.Tocar("granada/molotov_ar", p, C_HE, 0.8f, null, 3);
        public static void FogoApagou(Vector3 p) => Eu()?.Tocar("granada/apagou", p, C_FOGO, 0.8f, null, 2);
        public static void Quique(Vector3 p, string tipo, bool parede) => Eu()?.Tocar("granada/quique_" + (tipo == "molotov" ? "vidro" : "metal"), p, C_QUIQUE, parede ? 0.7f : 0.5f, null, 1);
        public static void Arremesso(Vector3 p, int quem = -1) => Eu()?.Tocar("granada/arremesso", p, C_FOLEY, 0.6f, "arr" + quem, 1);
        public static void Pino(Vector3 p, int quem = -1) => Eu()?.Tocar("granada/pino", p, C_FOLEY, 0.5f, "pin" + quem, 1);

        public static void C4Bip(Vector3 p, bool urgente)
        {
            var s = Eu(); if (s == null) return;
            string n = urgente ? "c4/bip_rapido" : "c4/bip";
            var v = s.Tocar(n, p, C_C4, 0.9f, "c4", 3);
            if (s.c4Logs < 6 || (urgente && s.c4Logs < 9))
            {
                s.c4Logs++;
                Debug.Log(v != null
                    ? $"[Som3D] C4 bip {n}: dist {s.Distancia(p):0} cs, volume {v.src.volume:0.00}, oclusão {v.oclusao:0}, clipe {v.src.clip.name} ({v.src.clip.length:0.000}s, {v.src.clip.loadState}), tocando={v.src.isPlaying}"
                    : $"[Som3D] C4 bip {n}: NÃO TOCOU (clipes={s.Clipes(n).Length}, dist {s.Distancia(p):0} cs, ganho {Curva(C_C4, s.Distancia(p)) * 0.9f:0.000})");
            }
        }
        int c4Logs;
        public static void C4Digita(Vector3 p) => Eu()?.Tocar("c4/digita", p, C_C4_FOLEY, 0.7f, "c4dig", 2);
        public static void C4Plantou(Vector3 p) => Eu()?.Tocar("c4/plantou", p, C_C4, 1f, "c4", 3);
        /// <summary>Começo do desarme (como no CS: tem o início, não tem o fim).</summary>
        public static void C4Desarme(Vector3 p, bool kit) => Eu()?.Tocar("c4/desarme_inicio", p, C_C4_FOLEY, 0.7f, "c4des", 2);
        public static void C4Sacar(Vector3 p) => Eu()?.Tocar("c4/sacar", p, C_FOLEY, 0.5f, null, 1);
        public static void C4Explode(Vector3 p) { var s = Eu(); if (s == null) return; s.Tocar("c4/explode", p, C_C4_EXPLODE, 1f, null, 5); s.Tocar("c4/explode_distante", p, C_HE_DIST, 1f, null, 4); }

        /// <summary>Chamar todo quadro: a C4 plantada bipa sozinha no ritmo do CS (intervalo encolhe com o tempo; nos
        /// últimos 10 s troca pro bip rápido). restante = segundos até explodir.</summary>
        /// <returns>true no quadro em que bipou (pisca o LED junto).</returns>
        /// <remarks>O ritmo é marcado pelo TEMPO QUE FALTA (relógio da simulação), não pelo da Unity: pausou, para;
        /// em 2x/4x/8x acelera junto. Intervalo do CS: max(0,15; 0,1 + 0,9·restante/total).</remarks>
        public static bool C4(Vector3 p, bool plantada, float restante, float total = 40f)
        {
            var s = Eu(); if (s == null) return false;
            if (!plantada || restante <= 0) { s.proxBip = -1; s.finalTocou = s.alerta10 = false; return false; }
            if (s.proxBip < 0 || Mathf.Abs(restante - s.proxBip) > 2f) { s.proxBip = restante - 1f; s.finalTocou = false; s.c4Logs = 0; Debug.Log($"[Som3D] C4 armada: faltam {restante:0.0}s de {total:0}s"); }   // 1º bip 1 s depois de plantar (ou C4 nova)
            // último segundo: os bips param e entra o chiado de "timer acabou" (pedido do usuário, como no CS2)
            if (restante <= FINAL_C4) { if (s.finalTocou) return false; s.finalTocou = true; s.Tocar("c4/final", p, C_C4_EXPLODE, 1f, "c4", 4); return true; }
            if (restante > s.proxBip) return false;
            C4Bip(p, restante < 10);
            s.proxBip = restante - Mathf.Max(0.15f, 0.1f + 0.9f * restante / total);
            return true;
        }
        float proxBip = -1;   // em segundos de 'restante' da C4
        bool finalTocou, alerta10; public static float FINAL_C4 = 1.0f;

        /// <summary>Chamar todo quadro com o tempo da partida (m.t, ou replay.t no replay): as peças agendadas (recarga)
        /// seguem a simulação (pausa e 2x/4x/8x). Sem essa chamada, usa o relógio da Unity.</summary>
        public static void Relogio(float tJogo) { var s = Eu(); if (s == null) return; s.relogioJogo = tJogo; s.usaJogo = true; }
        float relogioJogo; bool usaJogo;
        float Agora => usaJogo ? relogioJogo : relogio;

        /// <summary>Chamar todo quadro com as áreas de fogo acesas: um laço de fogo por molotov, no centro dela.</summary>
        public static void Fogos(IList<Vector3> centros) { var s = Eu(); if (s == null) return; s.Lacos(s.lacos, "granada/fogo_loop", centros, C_FOGO, 0.55f); }

        void Lacos(List<Voz> lista, string nome, IList<Vector3> pos, Vector2[] curva, float vol)
        {
            var cs = Clipes(nome); if (cs.Length == 0) return;
            while (lista.Count < pos.Count)
            {
                var g = new GameObject(nome); g.transform.SetParent(transform);
                var v = new Voz { tr = g.transform, src = g.AddComponent<AudioSource>(), lp = g.AddComponent<AudioLowPassFilter>(), curva = curva, vol = vol };
                v.src.clip = cs[0]; v.src.loop = true; v.src.dopplerLevel = 0; v.src.rolloffMode = AudioRolloffMode.Custom;
                v.src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1))); v.src.maxDistance = 100000;
                v.src.time = Random.Range(0, cs[0].length * 0.9f); lista.Add(v);
            }
            for (int i = 0; i < lista.Count; i++)
            {
                var v = lista[i];
                if (i < pos.Count) { v.tr.position = pos[i]; if (!v.src.isPlaying) v.src.Play(); Aplicar(v, Distancia(pos[i])); }
                else if (v.src.isPlaying) v.src.Stop();
            }
        }
        readonly List<Voz> lacos = new List<Voz>();

        // ---------- bala / arma / corpo ----------
        public static void Capsula(Vector3 p, string arma) => Eu()?.Agendar(Random.Range(0.25f, 0.45f), "bala/capsula_" + (arma == "awp" ? "awp" : WeaponStats.Pistola(arma) ? "pistola" : "fuzil"), p, C_QUIQUE, 0.25f, null);
        /// <summary>Bala que passou perto do ouvido (o lance que a câmera olha): estalo supersônico.</summary>
        public static void BalaPassando(Vector3 de, Vector3 ate) => BalaPassando(de, ate, null);
        public static void BalaPassando(Vector3 de, Vector3 ate, string arma)
        {
            var s = Eu(); if (s == null || !s.ouvido) return;
            var o = s.ouvido.position; var ab = ate - de; float u = Mathf.Clamp01(Vector3.Dot(o - de, ab) / Mathf.Max(1e-4f, ab.sqrMagnitude));
            var perto = de + ab * u; if (u <= 0.02f || u >= 0.98f) return;
            if ((new Vector2(perto.x - o.x, perto.z - o.z)).magnitude * CS_POR_UNITY < 140) s.Tocar(arma != null && (WeaponStats.Pistola(arma) || arma == "m4s" || arma == "usp") ? "bala/passando_sub" : "bala/passando", perto, C_IMPACTO, 0.6f, null, 1);
        }
        public static void Ricochete(Vector3 p) => Eu()?.Tocar("bala/ricochete", p, C_IMPACTO, 0.5f, null, 0);
        public static void Compra(Vector3 p) => Eu()?.Tocar("hud/compra", p, C_FOLEY, 0.4f, null, 0);   // item comprado na base (freeze): tic no lugar do jogador
        public static void Vazio(Vector3 p, string arma) => Eu()?.Tocar(WeaponStats.Pistola(arma) ? "arma/vazio_pistola" : "arma/vazio_fuzil", p, C_FOLEY, 0.6f, null, 1);
        public static void Zoom(Vector3 p) => Eu()?.Tocar("arma/zoom", p, C_FOLEY, 0.35f, null, 0);
        public static void ArmaCaiu(Vector3 p, string arma) => Eu()?.Tocar(WeaponStats.Pistola(arma) ? "arma/cai_pistola" : "arma/cai_fuzil", p, C_QUIQUE, 0.6f, null, 1);
        /// <summary>Quiques seguintes da arma no chão (n = 1 no 2º toque, 2 no 3º...): o som do quique, cada vez mais baixo.</summary>
        public static void ArmaQuicou(Vector3 p, string arma, int n = 1) =>
            Eu()?.Tocar(WeaponStats.Pistola(arma) ? "arma/cai_pistola_quique" : "arma/cai_fuzil_quique", p, C_QUIQUE, 0.45f * Mathf.Pow(0.6f, Mathf.Max(0, n - 1)), null, 0);
        public static void Pegar(Vector3 p) => Eu()?.Tocar("arma/pegar", p, C_FOLEY, 0.45f, null, 0);
        public static void Zumbido(float forca) => Eu()?.TocarNoOuvido("granada/zumbido", Mathf.Clamp01(forca) * 0.35f);
        public static void Queimando(Vector3 p, int quem) => Eu()?.Tocar("granada/queimando", p, C_FOLEY, 0.5f, "fogo" + quem, 1);
        public static void C4Pegar(Vector3 p) => Eu()?.Tocar("c4/pegar", p, C_FOLEY, 0.5f, null, 1);
        public static void C4Largar(Vector3 p) => Eu()?.Tocar("c4/largar", p, C_FOLEY, 0.5f, null, 1);
        /// <summary>Aterrissou de um pulo/queda. chao: pedra, madeira, metal (caixa de madeira, telhado de zinco...).</summary>
        public static void Aterrissa(Vector3 p, string chao = "pedra")
        {
            var s = Eu(); if (s == null) return;
            string n = s.Clipes("corpo/aterrissa_" + chao).Length > 0 ? "corpo/aterrissa_" + chao : "corpo/aterrissa";
            s.Tocar(n, p, C_PASSO, 0.65f, null, 0);
        }
        public static void Pulo(Vector3 p) => Eu()?.Tocar("corpo/pulo", p, C_PASSO, 0.5f, null, 0);
        // passos, pulo e aterrissagem: o espectador ouve um pouco mais longe que o foley comum (usuário, 2026-11-04)
        public static readonly Vector2[] C_PASSO = { new(0, 1), new(400, 1), new(1400, 0.35f), new(2000, 0) };
        public static void Escada(Vector3 p) => Eu()?.Tocar("corpo/escada", p, C_FOLEY, 0.4f, null, 0);
        public static void CorpoCaiu(Vector3 p) => Eu()?.Tocar("corpo/cai", p, C_FOLEY, 0.6f, null, 1);

        // ---------- transmissão (sem posição: toca "na cabeça" de quem assiste) ----------
        public static void Hud(string nome, float vol = 0.6f) => Eu()?.TocarNoOuvido("hud/" + nome, nome == "match_point" ? 0.5f : vol);   // match point = fala do narrador (sem BAM): no nível dos anúncios
        /// <summary>Torcida do campeonato (transmissão): "clutch" quando alguém vence um 1vX; "ace" quando um jogador mata os 5;
        /// "ace_absurdo" pro ace de highlight (ex.: ace em 1v5, ou as 5 mortes em poucos segundos).</summary>
        public static void Torcida(string nome, float vol = 0.5f) => Eu()?.TocarNoOuvido("torcida/" + nome, vol);
        /// <summary>Locutor (só os clássicos): "bomba_plantada", "bomba_desarmada", "vitoria_ct", "vitoria_tr".</summary>
        public static void Anuncio(string nome) { Debug.Log("[Som3D] narrador: " + nome); Eu()?.TocarNoOuvido("voz/anuncio_" + nome, 0.45f); }
        /// <summary>"Go, go, go!" no fim do freeze (voz de rádio do time; ct = qual time a câmera está seguindo, ou o CT por padrão).</summary>
        public static void GoGoGo(bool ct = true) { var s = Eu(); if (s == null) return; s.TocarNoOuvido(s.Clipes("voz/radio_" + (ct ? "ct" : "tr") + "_gogogo").Length > 0 ? "voz/radio_" + (ct ? "ct" : "tr") + "_gogogo" : "voz/radio_gogogo", 0.6f); }
        public static void Musica(string nome, float vol = 0.5f) => Eu()?.TocarNoOuvido("musica/" + nome, vol);

        /// <summary>Ambiente da Vila: laços tocando baixo o tempo todo (chamar uma vez; volume 0 desliga).
        /// Níveis (2026-11-04): antes tocava a -45..-67 dB e sumia; agora vento e mercado ~-36 dB, pássaros ~-41, toldos ~-47 (soma ~-32 dB).</summary>
        public static void Ambiente(float vol = 1f)
        {
            var s = Eu(); if (s == null) return;
            foreach (var (n, v) in new[] { ("ambiente/vento", 0.62f), ("ambiente/passaros", 1f), ("ambiente/toldos", 1f), ("ambiente/mercado", 0.5f) })
            {
                if (!s.ambientes.TryGetValue(n, out var src))
                {
                    var cs = s.Clipes(n); if (cs.Length == 0) continue;
                    src = new GameObject(n).AddComponent<AudioSource>(); src.transform.SetParent(s.transform); src.clip = cs[0]; src.loop = true; src.spatialBlend = 0; src.priority = 200;
                    src.time = Random.Range(0, cs[0].length * 0.8f); src.Play(); s.ambientes[n] = src;
                }
                src.volume = v * vol * volumeGeral;
            }
        }
        readonly Dictionary<string, AudioSource> ambientes = new Dictionary<string, AudioSource>();

        void TocarNoOuvido(string nome, float vol)
        {
            var cs = Clipes(nome); if (cs.Length == 0) return;
            var v = PegarVoz(5, 1); if (v == null) return;
            v.curva = new[] { new Vector2(0, 1) }; v.vol = vol; v.dono = null; v.prioridade = 5; v.fade = -1; v.oclusao = 0; v.naCabeca = true; v.src.priority = PrioUnity(5);
            v.tr.position = ouvido ? ouvido.position : Vector3.zero; v.src.clip = cs[Random.Range(0, cs.Length)]; v.src.pitch = 1;
            Aplicar(v, 0); v.src.spatialBlend = 0; v.src.Play();
        }

        public static void FacaSacar(Vector3 p, int quem = -1) => Eu()?.Tocar("faca/sacar", p, C_FOLEY, 0.5f, "faca" + quem, 1);
        public static void FacaEstocada(Vector3 p, int quem = -1) => Eu()?.Tocar("faca/estocada", p, C_FOLEY, 0.7f, "faca" + quem, 1);
        public static void FacaParede(Vector3 p) => Eu()?.Tocar("faca/parede", p, C_IMPACTO, 0.6f, null, 1);
        /// <summary>Pente quase vazio (o CS dá um clique quando sobra pouca bala).</summary>
        public static void QuaseVazio(Vector3 p) => Eu()?.Tocar("arma/quase_vazio", p, C_FOLEY, 0.35f, null, 0);
        public static void Equipamento(Vector3 p) => Eu()?.Tocar("corpo/equipamento", p, C_FOLEY, 0.2f, null, 0);
        public static void Queda(Vector3 p) => Eu()?.Tocar("corpo/queda", p, C_FOLEY, 0.7f, null, 1);

        public static void Faca(Vector3 p, bool acertou, int quem = -1) => Eu()?.Tocar(acertou ? "faca/acerto" : "faca/golpe", p, C_FOLEY, 0.7f, "faca" + quem, 1);
        /// <summary>Passo correndo (no CS andando de shift não faz barulho). Na pedra o passo é do TIME (CT bota pesada,
        /// TR tênis), como no CS; nos outros chãos (terra, madeira, metal, telha) é do chão.</summary>
        public static void Passo(Vector3 p, string chao = "pedra", bool? ct = null)
        {
            var s = Eu(); if (s == null) return;
            // bota do CT / tênis do TR quando o chão tem as duas versões ("pedra" -> passo/ct|tr; "marmore" -> passo/marmore_ct|tr)
            string time = ct.HasValue ? (ct.Value ? "ct" : "tr") : null, n;
            if (time != null && chao == "pedra" && s.Clipes("passo/" + time).Length > 0) n = "passo/" + time;
            else if (time != null && s.Clipes("passo/" + chao + "_" + time).Length > 0) n = "passo/" + chao + "_" + time;
            else n = s.Clipes("passo/" + chao).Length > 0 ? "passo/" + chao : "passo/pedra";
            s.Tocar(n, p, C_PASSO, 0.5f, null, 0);
        }

        // ---------------- perfis (volume/curva por arma — soundevents do CS2) ----------------
        static (float vp, float vd, Vector2[] cp, Vector2[] cd) PerfilTiro(string a) => a switch
        {
            "ak" => (1.0f, 0.5f, C_TIRO, C_DISTANTE),
            "m4" => (0.95f, 0.8f, C_TIRO, C_DISTANTE_PIST),
            "m4s" => (0.8f, 0.6f, C_TIRO, C_DISTANTE_PIST),
            "awp" => (1.0f, 1.0f, C_TIRO_AWP, C_DISTANTE_PIST),
            "deagle" => (0.9f, 1.0f, C_TIRO, C_DISTANTE_PIST),
            "glock" => (0.85f, 0.7f, C_TIRO, C_DISTANTE_PIST),
            "usp" => (0.75f, 0.5f, C_TIRO, C_DISTANTE_PIST),
            "p250" => (0.85f, 0.9f, C_TIRO, C_DISTANTE_PIST),
            "mp9" => (0.75f, 0.9f, C_TIRO, C_DISTANTE_PIST),
            "mac10" => (0.7f, 0.9f, C_TIRO, C_DISTANTE_PIST),
            "tec9" => (0.8f, 0.6f, C_TIRO, C_DISTANTE_PIST),   // pistola: eco de longe mais baixo (usuário: "muito potente")
            "fiveseven" => (0.85f, 0.9f, C_TIRO, C_DISTANTE_PIST),
            "galil" => (0.95f, 0.6f, C_TIRO, C_DISTANTE),
            "famas" => (0.95f, 0.8f, C_TIRO, C_DISTANTE_PIST),
            _ => (0.8f, 0.7f, C_TIRO, C_DISTANTE_PIST),
        };

        // fração da recarga em que cada peça toca: medida nas animações exportadas (Tools/sons/tempos_recarga.py, tempos_recarga.json):
        // o pente velho começa a sair em ~16% e o novo encaixa (para de se mexer) em ~67%. O PICO de cada clipe é que cai
        // nesse instante (Agendar adianta o começo pelo pico do clipe).
        static IEnumerable<(float, string, float)> PecasRecarga(string a)
        {
            switch (a)
            {
                case "awp": return new[] { (0.152f, "pente_sai", 0.75f), (0.667f, "pente_entra", 0.75f), (0.71f, "pente_bate", 0.6f), (0.8f, "ferrolho_tras", 0.9f), (0.88f, "ferrolho_frente", 0.9f) };
                case "glock": case "usp": case "p250": case "deagle": case "tec9": case "fiveseven":
                    return new[] { (0.17f, "pente_sai", 0.6f), (0.672f, "pente_entra", 0.6f), (0.85f, "ferrolho_frente", 0.7f) };
                default: return new[] { (0.157f, "pente_sai", 0.9f), (0.667f, "pente_entra", 0.8f), (0.84f, "ferrolho", 0.9f) };
            }
        }

        // ---------------- motor ----------------
        static Som3D Eu()
        {
            if (mudo) return null;
            if (eu) return eu;
            var go = new GameObject("Som3D"); eu = go.AddComponent<Som3D>(); eu.Montar();
            return eu;
        }

        void Montar()
        {
            // o ouvido é NOSSO (objeto Som3D/Ouvido); a câmera não deve ter AudioListener (a sessão Visual tira do Cenario3D)
            foreach (var l in FindObjectsByType<AudioListener>()) Debug.LogWarning("Som3D: já existe AudioListener em " + l.name + " — o ouvido do jogo é Som3D/Ouvido; remova esse.");
            // A Unity só MIXA 32 vozes reais (padrão); o resto vira "virtual": isPlaying=true e mudo. Temos 48 + ambiente, e o
            // bip da C4 sumia assim no meio do tiroteio (log dizia tocando=True). Sobe pra 80 antes de qualquer som tocar.
            var cfg = AudioSettings.GetConfiguration();
            if (cfg.numRealVoices < VOZES + 16) { cfg.numRealVoices = VOZES + 16; cfg.numVirtualVoices = Mathf.Max(cfg.numVirtualVoices, 512); AudioSettings.Reset(cfg); }
            Debug.Log($"[Som3D] vozes reais {AudioSettings.GetConfiguration().numRealVoices}");
            ouvido = new GameObject("Ouvido").transform; ouvido.SetParent(transform); ouvido.gameObject.AddComponent<AudioListener>();
            var plana = new AnimationCurve(new Keyframe(0, 1), new Keyframe(1, 1));
            for (int i = 0; i < VOZES; i++)
            {
                var g = new GameObject("voz" + i); g.transform.SetParent(transform);
                var v = new Voz { tr = g.transform, src = g.AddComponent<AudioSource>(), lp = g.AddComponent<AudioLowPassFilter>() };
                v.src.playOnAwake = false; v.src.spatialBlend = 0.85f; v.src.dopplerLevel = 0; v.src.rolloffMode = AudioRolloffMode.Custom;
                v.src.SetCustomCurve(AudioSourceCurveType.CustomRolloff, plana); v.src.maxDistance = 100000; v.src.spread = 40;
                v.lp.cutoffFrequency = 22000; v.lp.lowpassResonanceQ = 1;
                vozes.Add(v);
            }
        }

        AudioClip[] Clipes(string nome)
        {
            if (clipes.TryGetValue(nome, out var c)) return c;
            var lista = new List<AudioClip>();
            for (int k = 0; k < 8; k++) { var a = Resources.Load<AudioClip>("Sons/" + nome + "_" + k); if (a == null) break; lista.Add(a); }
            c = lista.ToArray(); clipes[nome] = c; return c;
        }

        void Agendar(float dt, string nome, Vector3 p, Vector2[] curva, float vol, string dono)
        {
            var cs = Clipes(nome); float pico = cs.Length > 0 ? Pico(cs[0]) : 0f;
            agenda.Add(new Agendado { t = Agora + Mathf.Max(0f, dt - pico), nome = nome, pos = p, curva = curva, vol = vol, dono = dono });
        }

        /// <summary>Segundos do começo do clipe até o maior pico (o clique do pente, o estalo do ferrolho). Calculado uma vez.</summary>
        float Pico(AudioClip c)
        {
            if (picos.TryGetValue(c, out float t)) return t;
            t = 0f;
            try
            {
                int n = Mathf.Min(c.samples, (int)(c.frequency * 1.5f)); var d = new float[n * c.channels];
                if (c.GetData(d, 0)) { int im = 0; float m = 0; for (int i = 0; i < d.Length; i++) { float v = Mathf.Abs(d[i]); if (v > m) { m = v; im = i; } } t = (im / c.channels) / (float)c.frequency; }
            }
            catch { t = 0f; }
            picos[c] = t; return t;
        }
        readonly Dictionary<AudioClip, float> picos = new Dictionary<AudioClip, float>();

        void ApagarDono(string dono, float tempo)
        {
            if (dono == null) return;
            foreach (var v in vozes) if (!v.Livre && v.dono == dono && v.fade < 0) { v.fade = 1; v.fadeVel = 1 / Mathf.Max(0.01f, tempo); }
        }

        Voz Tocar(string nome, Vector3 p, Vector2[] curva, float vol, string dono, int prioridade)
        {
            var cs = Clipes(nome); if (cs.Length == 0) return null;
            float d = Distancia(p); float g = Curva(curva, d) * vol;
            if (g < 0.003f) return null;   // longe demais: nem ocupa voz
            int k = Random.Range(0, cs.Length);
            if (cs.Length > 1 && ultimo.TryGetValue(nome, out int u) && u == k) k = (k + 1 + Random.Range(0, cs.Length - 1)) % cs.Length;
            ultimo[nome] = k;
            var v = PegarVoz(prioridade, g); if (v == null) return null;
            v.tr.position = p; v.curva = curva; v.vol = vol; v.dono = dono; v.prioridade = prioridade; v.fade = -1; v.naCabeca = false;
            v.src.priority = PrioUnity(prioridade);
            v.oclusao = Oclusao(p);
            v.src.clip = cs[k]; v.src.pitch = 1 + Random.Range(-0.02f, 0.02f);
            Aplicar(v, d); v.src.Play();
            return v;
        }

        /// <summary>Prioridade nossa (0 = foley ... 5 = narrador) -> AudioSource.priority (0 = nunca virtualiza, 256 = primeiro a sumir).
        /// Assim, se um dia faltar voz real, quem some é passo/cápsula, nunca C4/narrador.</summary>
        static int PrioUnity(int p) => Mathf.Clamp(128 - 24 * p, 0, 256);

        Voz PegarVoz(int prioridade, float g)
        {
            Voz pior = null; float notaPior = float.MaxValue;
            foreach (var v in vozes)
            {
                if (v.Livre) return v;
                float nota = v.prioridade * 2 + v.src.volume - v.src.time * 0.3f;
                if (nota < notaPior) { notaPior = nota; pior = v; }
            }
            return notaPior < prioridade * 2 + g ? pior : null;
        }

        float Distancia(Vector3 p)
        {
            if (!ouvido) return 0;
            float d = Vector3.Distance(new Vector3(p.x, 0, p.z), new Vector3(ouvido.position.x, 0, ouvido.position.z)) * CS_POR_UNITY * ESPECTADOR;
            return NaTela(p) ? Mathf.Min(d, NA_TELA) : d;
        }
        /// <summary>O que aparece na tela soa PERTO, sempre (usuário, 2026-11-05: "vejo a troca na minha cara e ela tem eco").
        /// 450 do CS fica antes de a camada distante começar (800) em qualquer curva de tiro; ângulo baixo do replay, zoom
        /// aberto ou andar de cima do palácio não jogam mais o lance da tela pro "longe".</summary>
        public static float NA_TELA = 450f;
        bool NaTela(Vector3 p)
        {
            if (!camUsada) return false;
            var v = camUsada.WorldToViewportPoint(p);
            return v.z > 0 && v.x > -0.03f && v.x < 1.03f && v.y > -0.03f && v.y < 1.03f;
        }
        /// <summary>Encolhe a distância pro espectador (câmera de cima): 0,4 = a borda da tela (~8 u = 580 do CS) soa a ~230,
        /// ainda "perto" nas curvas do CS2; o outro bomb (~30 u) soa a ~870, onde começa a camada distante.</summary>
        public static float ESPECTADOR = 0.4f;

        /// <summary>0 = livre, 1 = parede no meio (linha de visão do mapa, do ouvido à fonte).</summary>
        float Oclusao(Vector3 p)
        {
            if (!ouvido || !oclusaoLigada || NaTela(p)) return 0;   // visto de cima, o que está na tela não tem parede no meio
            var a = new V2(ouvido.position.x / MalhaMapaEsc, -ouvido.position.z / MalhaMapaEsc);
            var b = new V2(p.x / MalhaMapaEsc, -p.z / MalhaMapaEsc);
            if ((a - b).Len < 40) return 0;
            try { return NavGrid.Los(a, NavGrid.ZAt(a), b, p.y / MalhaMapaEsc) ? 0 : 1; }
            catch { return 0; }   // mapa ainda não montado (laboratório/galeria)
        }
        const float MalhaMapaEsc = 1f / 25f;

        void Aplicar(Voz v, float d)
        {
            if (v.naCabeca) { v.src.volume = Mathf.Clamp01(v.vol * volumeGeral * (v.fade >= 0 ? v.fade : 1)); v.src.spatialBlend = 0; v.lp.cutoffFrequency = 22000; return; }
            bool alcance = v.curva == C_C4 || v.curva == C_C4_FOLEY || v.curva == C_SMOKE;
            float g = Curva(v.curva, d) * v.vol * volumeGeral * (1 - (alcance ? 0.2f : 0.45f) * v.oclusao);
            if (v.fade >= 0) g *= v.fade;
            v.src.volume = Mathf.Clamp01(g);
            // ar come o agudo com a distância; parede abafa forte
            float f = Mathf.Lerp(22000, 5500, Mathf.Clamp01(d / 2500f));
            if (alcance) f = Mathf.Max(f, 9000);
            if (v.oclusao > 0) f = Mathf.Min(f, alcance ? 6000 : 2200);
            v.lp.cutoffFrequency = f;
            // perto é largo (quase sem panorâmica), longe é pontual
            v.src.spatialBlend = Mathf.Lerp(0.35f, 0.9f, Mathf.Clamp01(d / 700f));
        }

        void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime; relogio += Time.deltaTime;
            // mudo (menu inicial com round rodando atrás): cala o que já estava tocando e o ambiente — Eu() devolve null
            // com mudo, então Ambiente(0) não alcançava as fontes já criadas (voltar de uma partida pro menu ficava com som)
            if (mudo)
            {
                foreach (var v in vozes) if (!v.Livre) v.src.Stop();
                foreach (var a in ambientes.Values) a.volume = 0;
                agenda.Clear(); return;
            }
            PosicionarOuvido();
            for (int i = agenda.Count - 1; i >= 0; i--)
                if (agenda[i].t <= Agora) { var a = agenda[i]; agenda.RemoveAt(i); Tocar(a.nome, a.pos, a.curva, a.vol, a.dono, 1); }
            foreach (var v in vozes)
            {
                if (v.Livre) continue;
                if (v.fade >= 0) { v.fade -= v.fadeVel * dt; if (v.fade <= 0) { v.src.Stop(); continue; } }
                Aplicar(v, Distancia(v.tr.position));
            }
        }

        /// <summary>Ouvido = onde o centro da câmera bate no chão (plano da altura média do mapa), virado como a câmera.</summary>
        void PosicionarOuvido()
        {
            if (!camUsada || !camUsada.isActiveAndEnabled) camUsada = Camera.main;   // replay/diretor podem trocar de câmera
            if (!camUsada || !ouvido) return;
            var t = camUsada.transform; var f = t.forward;
            float y0 = 0.4f;
            Vector3 alvo = Mathf.Abs(f.y) > 1e-3f ? t.position + f * ((y0 - t.position.y) / f.y) : t.position;
            ouvido.position = alvo + Vector3.up * 0.6f;
            ouvido.rotation = Quaternion.Euler(0, t.eulerAngles.y, 0);
        }
    }
}
