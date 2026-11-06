using System;
using System.Collections.Generic;
using System.Linq;

namespace CSM.Sim
{
    /// <summary>
    /// O que o time quer jogar agora. O IGL preenche lendo a partida; no futuro o coach escreve aqui nos pauses. Tudo que muda
    /// o jogo do time passa por aqui, então uma mudança sempre tem motivo e dá pra ver no replay.
    /// </summary>
    public class Intencao
    {
        public float lado;            // -1 = puxar pro B, +1 = puxar pro A
        public float ritmo = 0.5f;    // 0 = lento, 1 = rápido
        public float meio = 1f;       // o quanto disputa o meio (0..1,3)
        public float agressao = 1f;   // jogadas agressivas cedo (0..1,3)
        public bool giroCauteloso;
        public bool guardaSmoke;      // TR: perdendo o pós-plant, segura smoke pra cortar o retake
        public string motivo = "";
    }

    /// <summary>
    /// O que um time aprendeu do adversário ao longo da partida. Só entra o que o time percebeu: quem viu, onde morreu, de onde
    /// matou, granada que caiu e o bomb plantado. O plano deles é dedução e pode estar errada. Round avulso não tem leitura.
    /// </summary>
    public class Leitura
    {
        public string modo = "";   // "" adapta; "semAdaptar" e "previsivel" são adversários de teste (Headless -partidas)
        public bool Adapta => modo == "";
        public readonly Intencao intencao = new Intencao();

        // o adversário jogando de TR (deduzido)
        public int trRounds, trA, trB, trFake, trRush, trLento;
        // o adversário jogando de CT (deduzido)
        public int ctRounds, ctStackA, ctStackB, ctAgressivo;
        // nossos planos (n, vitórias) e os últimos que usamos
        public readonly Dictionary<string, (int n, int w)> meus = new Dictionary<string, (int, int)>();
        public readonly List<string> ultimos = new List<string>();
        public readonly List<(char site, bool ganhou)> meusSites = new List<(char, bool)>();
        public readonly List<int> mortesNoMeio = new List<int>();
        int mortesMeioRound;

        public class Ponto { public V2 p; public Team lado; public int rodada; public float peso; }
        public readonly List<Ponto> deles = new List<Ponto>(), nossos = new List<Ponto>();   // onde eles costumam ficar; de onde a gente matou
        public readonly List<(V2 p, float t, int id)> vistos = new List<(V2, float, int)>();   // quem vimos neste round
        readonly float[] vistoEm = new float[10];
        public readonly int[,] repete = new int[4, 2];   // [quantas vezes esse ponto já matou, 0 = matou de novo / 1 = morreu ali]

        /// <summary>De onde o adversário matou a gente (lado que a gente jogava, rodada): no próximo round, prefire ali é dedução.</summary>
        public readonly List<(V2 p, Team lado, int rodada)> mortes = new List<(V2, Team, int)>();
        public int rodada;

        public Leitura() { Array.Fill(vistoEm, -9f); }

        static void Poe(List<Ponto> l, V2 p, Team lado, int rodada, float peso) { l.Add(new Ponto { p = p, lado = lado, rodada = rodada, peso = peso }); if (l.Count > 40) l.RemoveAt(0); }

        public void MorremosDali(V2 p, Team lado)
        {
            mortes.Add((p, lado, rodada)); if (mortes.Count > 24) mortes.RemoveAt(0);
            Poe(deles, p, lado == Team.T ? Team.CT : Team.T, rodada, 1f);
        }

        /// <summary>Alguém nosso morreu perto desse pixel nos últimos rounds (jogando do mesmo lado)?</summary>
        public bool Lembra(V2 p, Team lado, float raio, int rounds = 3)
        {
            foreach (var x in mortes) if (x.lado == lado && rodada - x.rodada <= rounds && rodada > x.rodada && (x.p - p).Len < raio) return true;
            return false;
        }

        /// <summary>Vimos alguém: entra no que sabemos do round; parado é posição que ele costuma segurar.</summary>
        public void Vi(V2 p, float t, Team ladoDele, bool parado, int id)
        {
            if (t - vistoEm[id] < 1.5f) return;
            vistoEm[id] = t; vistos.Add((p, t, id));
            if (parado) Poe(deles, p, ladoDele, rodada, 0.3f);
        }

        /// <summary>Um dos nossos matou daqui. Conta quantas vezes esse ponto já funcionou antes (pra medir se ele vai queimando).</summary>
        public void MatamosDali(V2 p, Team lado)
        {
            repete[Math.Min(3, UsosDe(p, lado)), 0]++;
            Poe(nossos, p, lado, rodada, 1f);
        }
        public void MorremosAqui(V2 p, Team lado) { repete[Math.Min(3, UsosDe(p, lado)), 1]++; if (Places.Side(p) == 'M') mortesMeioRound++; }
        int UsosDe(V2 p, Team lado) => nossos.Count(x => x.lado == lado && x.rodada < rodada && (x.p - p).Len < 50);

        /// <summary>O quanto essa direção (vista de `de`) lembra lugar deles em rounds passados. Coisa velha vai sendo esquecida.</summary>
        public float Lembranca(V2 de, float ang, Team ladoDeles)
        {
            float s = 0;
            foreach (var x in deles)
            {
                if (x.lado != ladoDeles || x.rodada >= rodada) continue;
                V2 d = x.p - de; float dl = d.Len; if (dl < 40 || dl > 700) continue;
                if (MathF.Abs(MathX.AngDiff(d.Atan2, ang)) < 0.12f) s += x.peso * MathF.Pow(0.75f, rodada - x.rodada - 1);
            }
            return s;
        }

        /// <summary>O quanto esse ponto nosso já foi visto trabalhando: quem matou daqui pode esperar que mirem aqui.</summary>
        public float Queimado(V2 p, Team lado, float raio = 60)
        {
            float s = 0;
            foreach (var x in nossos) if (x.lado == lado && x.rodada < rodada && (x.p - p).Len < raio) s += x.peso * MathF.Pow(0.8f, rodada - x.rodada - 1);
            return s;
        }

        public void MeuPlano(string plano, bool ganhou, char site = ' ')
        {
            rodada++;
            if (site == 'A' || site == 'B') histA = 0.6f * histA + 0.4f * (site == 'A' ? 1 : 0);   /* bx: o quanto o CT deles tem motivo pra nos esperar no A */
            meus.TryGetValue(plano, out var x); meus[plano] = (x.n + 1, x.w + (ganhou ? 1 : 0));
            ultimos.Add(plano); if (ultimos.Count > 4) ultimos.RemoveAt(0);
            meusSites.Add((site, ganhou)); if (meusSites.Count > 4) meusSites.RemoveAt(0);
            mortesNoMeio.Add(mortesMeioRound); if (mortesNoMeio.Count > 3) mortesNoMeio.RemoveAt(0);
            mortesMeioRound = 0;
        }

        /// <summary>Acabou o round: deduz o que o TR deles fez só com o que a gente viu.</summary>
        /// <summary>Resultado de uma escolha dentro do round (estilo de pós-plant, porta do retake): soma no histórico sem contar como plano.</summary>
        public void Anota(string chave, bool ganhou)
        {
            meus.TryGetValue(chave, out var x); meus[chave] = (x.n + 1, x.w + (ganhou ? 1 : 0));
            if (chave.StartsWith("pp:")) { posPlants++; if (!ganhou) posPlantsPerdidos++; }
        }
        public int posPlants, posPlantsPerdidos;
        /// <summary>O quanto essa escolha vem dando certo: sem amostra fica neutra; 2 fracassos já pesam.</summary>
        public float Peso(string chave) { if (!Adapta) return 1; meus.TryGetValue(chave, out var x); return (x.w + 1f) / (x.n + 2f) * 2f; }

        public void DeduzTR(Match m, Team eu)
        {
            var eles = eu == Team.T ? Team.CT : Team.T; trRounds++;
            int perto(V2 c, float ate) => vistos.Where(v => v.t < ate && (v.p - c).Len < 520).Select(v => v.id).Distinct().Count();
            char site = m.bombPlanted ? m.planSite : ' ';
            if (site == ' ') { int a = perto(Places.PlantA, 999), b = perto(Places.PlantB, 999); site = a > b ? 'A' : b > a ? 'B' : ' '; }
            if (site == 'A') trA++; else if (site == 'B') trB++;
            if (perto(Places.PlantA, 22) >= 3 || perto(Places.PlantB, 22) >= 3) trRush++;
            if (site != ' ') { V2 outro = Places.Plant(site == 'A' ? 'B' : 'A'); if (m.bombPlanted && m.pousos.Any(p => p.tm == eles && (p.p - outro).Len < 480)) trFake++; }
            if (!vistos.Any(v => v.t < 40)) trLento++;
            FimRound();
        }

        /// <summary>Acabou o round: deduz como o CT deles se montou só com o que a gente viu.</summary>
        public void DeduzCT(Match m)
        {
            ctRounds++;
            int perto(V2 c) => vistos.Where(v => v.t < 40 && (v.p - c).Len < 520).Select(v => v.id).Distinct().Count();
            if (perto(Places.PlantA) >= 3) ctStackA++; else if (perto(Places.PlantB) >= 3) ctStackB++;
            {   /* bx: CT em cada bomb pelo que a gente viu. Só se vê o bomb onde se foi: se lá tinha n, o outro tem ~4-n (5 menos
                   quem segura o meio); o bomb que ninguém viu é deduzido, não "vazio". Média que esquece (os últimos pesam mais). */
                float nA = perto(Places.PlantA), nB = perto(Places.PlantB);
                if (nA > 0 && nB == 0) nB = MathF.Max(0, 4 - nA); else if (nB > 0 && nA == 0) nA = MathF.Max(0, 4 - nB);
                if (nA + nB > 0) { ctEmA = 0.65f * ctEmA + 0.35f * nA; ctEmB = 0.65f * ctEmB + 0.35f * nB; ctVisto++; }
            }
            if (vistos.Any(v => v.t < 18 && v.p.y > 15 * CSM.Map.MapData.Q)) ctAgressivo++;
            FimRound();
        }
        void FimRound() { vistos.Clear(); Array.Fill(vistoEm, -9f); }

        /* bx: crença contínua do IGL TR (sem limiar de "3 vistos"): quantos CT a gente viu em cada bomb nos rounds passados (os
           últimos pesam mais) e o quanto o CT deles tem motivo pra nos esperar em cada bomb (nossos últimos bombs) */
        public float ctEmA = 1.5f, ctEmB = 1.5f, histA = 0.5f; public int ctVisto;
        /// <summary>
        /// bx: pra que lado a crença puxa o ataque (+1 = A, -1 = B), pensando como IGL pro: o bomb onde a gente VIU menos CT é
        /// o mais fraco (confiança cresce com os rounds vistos); e o bomb onde eles mais nos esperam (fomos lá seguido) é o
        /// que eles reforçam — não ser previsível. A intenção do time (perdeu 2 seguidas, stack lido) entra junto.
        /// </summary>
        public float LadoCrenca()
        {
            float conf = Math.Min(1f, ctVisto / 4f);
            float fraco = conf * Math.Clamp(0.55f * (ctEmB - ctEmA), -0.8f, 0.8f);   /* viu mais CT no B → puxa A */
            float previsivel = -1.1f * (histA - 0.5f);                                  /* fomos muito A → eles esperam A → puxa B */
            return Math.Clamp(0.5f * intencao.lado + fraco + previsivel, -0.9f, 0.9f);
        }

        /// <summary>Taxa com "prior" (começa em meio a meio; leva uns rounds pra acreditar).</summary>
        public static float Taxa(int x, int n, float prior = 0.5f, float peso = 3) => (x + prior * peso) / (n + peso);

        /// <summary>Planos do banco que a gente já usou nesta partida (não repetir a mesma jogada; o adversário anota).</summary>
        public readonly List<int> bancoUsados = new List<int>();

        /// <summary>Quanto o plano vem dando certo pra nós (0..1, começa em 0,5).</summary>
        public float Sucesso(string plano) { meus.TryGetValue(plano, out var x); return Taxa(x.w, x.n); }

        /// <summary>Usamos esse plano nos últimos k rounds seguidos?</summary>
        public int Repetido(string plano) { int k = 0; for (int i = ultimos.Count - 1; i >= 0 && ultimos[i] == plano; i--) k++; return k; }

        /// <summary>Perdemos os dois últimos rounds indo nesse bomb?</summary>
        char PerdeuDuasEm()
        {
            if (meusSites.Count < 2) return ' ';
            var a = meusSites[meusSites.Count - 1]; var b = meusSites[meusSites.Count - 2];
            return !a.ganhou && !b.ganhou && a.site == b.site ? a.site : ' ';
        }

        /// <summary>Início do round de TR: o IGL revê a intenção. Puxa pouco e devagar: os pros quase não mudam a preferência de bomb.</summary>
        public void AtualizaTR()
        {
            var i = intencao; var mot = new List<string>();
            i.lado *= 0.8f; i.ritmo += (0.5f - i.ritmo) * 0.2f;
            if (ctRounds >= 3)
            {
                float sA = Taxa(ctStackA, ctRounds, 0.2f), sB = Taxa(ctStackB, ctRounds, 0.2f), ag = Taxa(ctAgressivo, ctRounds, 0.3f);
                if (sB > 0.35f) { i.lado += 0.25f; mot.Add($"eles enchem o B ({ctStackB}/{ctRounds})"); }
                if (sA > 0.35f) { i.lado -= 0.25f; mot.Add($"eles enchem o A ({ctStackA}/{ctRounds})"); }
                if (ag > 0.45f) { i.ritmo -= 0.2f; mot.Add($"o CT deles sai cedo ({ctAgressivo}/{ctRounds}): jogo lento pra punir"); }
            }
            i.guardaSmoke = posPlantsPerdidos >= 2 && posPlantsPerdidos * 2 >= posPlants;
            if (i.guardaSmoke) mot.Add($"perdemos {posPlantsPerdidos} de {posPlants} depois de plantar: guardo smoke pro retake deles");
            char p = PerdeuDuasEm();
            if (p == 'A') { i.lado -= 0.3f; mot.Add("perdemos 2 seguidas no A"); } else if (p == 'B') { i.lado += 0.3f; mot.Add("perdemos 2 seguidas no B"); }
            i.lado = MathX.Clamp(i.lado, -0.7f, 0.7f); i.ritmo = MathX.Clamp(i.ritmo, 0.1f, 0.9f);
            i.motivo = string.Join("; ", mot);
        }

        /// <summary>Início do round de CT: o IGL revê a intenção pelo que deduziu do TR deles.</summary>
        public void AtualizaCT()
        {
            var i = intencao; var mot = new List<string>();
            i.lado *= 0.8f; i.agressao += (1f - i.agressao) * 0.3f; i.meio += (1f - i.meio) * 0.3f;
            if (trRounds >= 3)
            {
                float a = Taxa(trA, trA + trB), fake = Taxa(trFake, trRounds, 0.15f), lento = Taxa(trLento, trRounds, 0.15f), rush = Taxa(trRush, trRounds, 0.15f);
                if (a > 0.6f) { i.lado += (a - 0.5f); mot.Add($"foram A em {trA} de {trA + trB}"); }
                if (a < 0.4f) { i.lado -= (0.5f - a); mot.Add($"foram B em {trB} de {trA + trB}"); }
                i.giroCauteloso = fake > 0.3f; if (i.giroCauteloso) mot.Add($"parece fake ({trFake}/{trRounds}): gira devagar");
                if (lento > 0.3f) { i.agressao *= 0.6f; mot.Add($"jogam lento ({trLento}/{trRounds}): ninguém dá peek à toa"); }
                if (rush > 0.3f) { i.agressao *= 0.6f; mot.Add($"chegam rápido ({trRush}/{trRounds}): todo mundo perto do bomb"); }
            }
            if (mortesNoMeio.Sum() >= 4) { i.meio -= 0.3f; mot.Add($"morremos {mortesNoMeio.Sum()} no meio: seguro mais atrás"); }
            i.lado = MathX.Clamp(i.lado, -0.7f, 0.7f); i.meio = MathX.Clamp(i.meio, 0.3f, 1.3f); i.agressao = MathX.Clamp(i.agressao, 0.2f, 1.3f);
            i.motivo = string.Join("; ", mot);
        }
    }
}
