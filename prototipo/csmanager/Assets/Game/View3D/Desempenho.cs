using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CSM.Sim;
using CSM.Map;

namespace CSM.View3D
{
    /// <summary>
    /// Medição de FPS na build (só com "-csm_perf saida.txt" na linha de comando; no jogo normal não existe): sem VSync,
    /// espera a partida esquentar, mede a base (tempo do quadro, picos, CPU por parte do código, coleta de lixo) e depois
    /// testa cada custo pesado ligando e desligando a cada 20 quadros no mesmo trecho da partida (a cena muda devagar, então
    /// a diferença é do recurso e não do lugar). Anota tudo e fecha.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class Desempenho : MonoBehaviour
    {
        // cronômetro das partes do código (PartidaAoVivo3D.Update chama Soma quando Ativo)
        public static bool Ativo;
        static readonly System.Diagnostics.Stopwatch relogioCpu = System.Diagnostics.Stopwatch.StartNew();
        public static long Agora => relogioCpu.ElapsedTicks;
        public static readonly string[] PARTES = { "simulação", "passo visual (bonecos, câmera)", "efeitos e smoke", "HUD", "troca de round: sim", "troca de round: visual" };
        static readonly double[] soma = new double[6], pico = new double[6];
        public static void Soma(int parte, ref long t0)
        {
            long t = Agora; double ms = (t - t0) * 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            soma[parte] += ms; if (ms > pico[parte]) pico[parte] = ms; t0 = t;
            if (parte >= 4) TrocaDeRound(parte, ms);
        }

        // ---- recorte do Step nº1 de cada round (pedido da sim-apoio): GC, alocação, Match.Tempos, Brain.Perfil, NavGrid ----
        // A foto "antes" é tirada no Soma(4) do NovoRound (logo depois do pt.Preparar): Tempos/Perfil só mudam dentro do Step,
        // então a diferença até o fim do Step nº1 é desse Step. O GC é contado em três trechos: troca de round (Fechar +
        // Preparar), preparo visual (bonecos etc.) e do fim do preparo até o fim do Step nº1 (inclui o resto do quadro).
        static readonly Dictionary<string, double> temposFoto = new Dictionary<string, double>();
        static readonly Dictionary<string, long> perfilFoto = new Dictionary<string, long>();
        static bool fotoTirada; static int roundJogo, etapaAtual = -2; static readonly List<StepRec> primeiroRound = new List<StepRec>();
        static string aquecerResumo; static double aquecerMs = -1; static int aquecerPassos;   // -csm_aquecer [passos] / -csm_aquecer_passos N: Match.Aquecer(passos) na carga, antes do 1º Preparar
        static int gcFoto, gcFotoVisual, gcFimStepAnt; static long memFoto; static double msCampoFoto, msAFoto; static int nCampoFoto, nAFoto;
        static readonly List<string> trocas = new List<string>(); static readonly StringBuilder recorte = new StringBuilder(); static int nRecortes;
        static void TrocaDeRound(int parte, double ms)
        {
            int gc = System.GC.CollectionCount(0);
            trocas.Add($"{(parte == 4 ? "sim" : "visual")} {ms:0.0} ms (GC +{gc - gcFimStepAnt})"); gcFimStepAnt = gc;
            if (parte == 4)
            {
                temposFoto.Clear(); foreach (var kv in Match.Tempos) temposFoto[kv.Key] = kv.Value;
                perfilFoto.Clear(); foreach (var kv in Brain.Perfil) perfilFoto[kv.Key] = kv.Value;
                gcFoto = gc; memFoto = System.GC.GetTotalMemory(false); fotoTirada = true;
                msCampoFoto = NavGrid.msCampo; msAFoto = NavGrid.msAEstrela; nCampoFoto = NavGrid.nCampo; nAFoto = NavGrid.nAEstrela;
            }
            else gcFotoVisual = gc;
        }
        static void RecorteStep1(double tot, float t)
        {
            int gc = System.GC.CollectionCount(0); long mem = System.GC.GetTotalMemory(false);
            nRecortes++; fotoTirada = false;
            recorte.AppendLine($"  round {roundJogo} do jogo{(Ativo && etapaAtual == -2 ? " (no aquecimento da medição)" : "")}: Step nº1 {tot:0.0} ms (t {t:0.00}) | partes: " + string.Join(", ", Enumerable.Range(0, 6).Select(i => $"{PARTES_SIM[i]} {Match.perfMs[i]:0.0}")));
            recorte.AppendLine($"    troca de round: {string.Join(" → ", trocas)}");
            recorte.AppendLine($"    GC(0/1/2 = mesmo contador no Mono): troca sim→visual +{gcFotoVisual - gcFoto}, visual→fim do Step nº1 +{gc - gcFotoVisual} | memória gerenciada {memFoto / 1048576.0:0.0} → {mem / 1048576.0:0.0} MB ({(mem - memFoto) / 1048576.0:+0.0;-0.0} MB)");
            recorte.AppendLine($"    NavGrid no Step: {NavGrid.nCampo - nCampoFoto} campos inteiros = {NavGrid.msCampo - msCampoFoto:0.0} ms | {NavGrid.nAEstrela - nAFoto} A* = {NavGrid.msAEstrela - msAFoto:0.0} ms");
            var dt = Match.Tempos.Select(kv => (kv.Key, d: kv.Value - (temposFoto.TryGetValue(kv.Key, out var v) ? v : 0))).Where(x => x.d > 0.05).OrderByDescending(x => x.d).Take(14);
            recorte.AppendLine("    Match.Tempos (ms): " + string.Join(" | ", dt.Select(x => $"{x.Key} {x.d:0.0}")));
            double f = 1000.0 / System.Diagnostics.Stopwatch.Frequency;
            var dp = Brain.Perfil.Select(kv => (kv.Key, d: (kv.Value - (perfilFoto.TryGetValue(kv.Key, out var v) ? v : 0)) * f)).Where(x => x.d > 0.05).OrderByDescending(x => x.d).Take(14);
            recorte.AppendLine("    Brain.Perfil (ms): " + string.Join(" | ", dp.Select(x => $"{x.Key} {x.d:0.0}")));
            trocas.Clear(); gcFimStepAnt = gc;
        }
        public static void Desconta(int parte, double ms) => soma[parte] -= ms;
        // cronômetros da Sim por parte do m.Step (Match.Perf): o pior Step e a soma dos Steps lentos (> 33 ms)
        static readonly string[] PARTES_SIM = { "intel (visão e calor)", "IGL", "cérebros", "física (andar, colisão)", "armas, itens, granadas", "detector de suspeitas" };
        static readonly double[] piorStep = new double[6], somaLentos = new double[6];
        static double piorTotal; static int nSteps, nLentos;
        // cada Step: ms, tempo da partida, nº do Step no round, A* e estimativas (orçamento de A* estourado) dentro dele
        struct StepRec { public float ms, t; public int noRound, aEstrelas, negados, nos; }
        static readonly List<StepRec> steps = new List<StepRec>();
        static System.Reflection.FieldInfo campoM; static PartidaAoVivo3D partida; static object ultMatch; static int stepNoRound;
        static int aEstAntes, negAntes, nosAntes, negados0, origem0, nos0, aEst0;
        public static void DepoisDoStep()
        {
            nSteps++; double tot = 0; for (int i = 0; i < 6; i++) tot += Match.perfMs[i];
            if (tot > piorTotal) { piorTotal = tot; System.Array.Copy(Match.perfMs, piorStep, 6); }
            if (tot > 33) { nLentos++; for (int i = 0; i < 6; i++) somaLentos[i] += Match.perfMs[i]; }
            // de que round é este Step (o Match é trocado a cada round; lido por reflexão pra não mexer na PartidaAoVivo3D)
            if (partida == null) { partida = FindAnyObjectByType<PartidaAoVivo3D>(); campoM = typeof(PartidaAoVivo3D).GetField("m", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance); }
            var mm = partida != null && campoM != null ? campoM.GetValue(partida) as Match : null;
            if (!ReferenceEquals(mm, ultMatch)) { ultMatch = mm; stepNoRound = 0; roundJogo++; }
            stepNoRound++;
            int a = NavGrid.AEstrelas, n = NavGrid.Negados, c = NavGrid.NosAEstrela;
            var rec = new StepRec { ms = (float)tot, t = mm != null ? mm.t : -1, noRound = stepNoRound, aEstrelas = a - aEstAntes, negados = n - negAntes, nos = c - nosAntes };
            steps.Add(rec); if (roundJogo == 1) primeiroRound.Add(rec);
            aEstAntes = a; negAntes = n; nosAntes = c;
            if (stepNoRound == 1 && fotoTirada) RecorteStep1(tot, mm != null ? mm.t : -1);
            else if (stepNoRound > 1) gcFimStepAnt = System.GC.CollectionCount(0);
        }
        static void ZerarSim()
        {
            Match.Perf = true; NavGrid.Medir = true; Match.Tempos.Clear();
            Match.Perfil = true;   // o recorte do Step nº1 vem desde o 1º round do jogo (não zera aqui)
            NavGrid.msCampo = NavGrid.msAEstrela = 0; NavGrid.nCampo = NavGrid.nAEstrela = 0;
            System.Array.Clear(piorStep, 0, 6); System.Array.Clear(somaLentos, 0, 6); piorTotal = 0; nSteps = nLentos = 0;
            NavGrid.Gigantes = new List<string>();   // A* acima de 40k células: quem pediu (pilha só nesses, raros)
            steps.Clear(); aEstAntes = aEst0 = NavGrid.AEstrelas; negAntes = negados0 = NavGrid.Negados; nosAntes = nos0 = NavGrid.NosAEstrela; origem0 = NavGrid.DaOrigemRespondeu;
        }
        static float Pct(List<float> ord, float p) => ord.Count == 0 ? 0 : ord[Mathf.Min(ord.Count - 1, (int)(ord.Count * p))];
        static void RelatorioSteps(StringBuilder log)
        {
            log.AppendLine($"  carga dos marcos (NavGrid.msMarcos, desde o início do jogo): {NavGrid.msMarcos:0} ms | experimento: \"{TeamBrain.EXP}\"");
            var gig = NavGrid.Gigantes; NavGrid.Gigantes = null;
            if (gig != null)
            {
                log.AppendLine($"  A* acima de 40k células: {gig.Count}");
                foreach (var gq in gig.GroupBy(x => x.Substring(x.IndexOf('←') + 1).Trim()).OrderByDescending(gq => gq.Count()).Take(6)) log.AppendLine($"    {gq.Count(),3}× ← {gq.Key}");
                foreach (var x in gig.Take(8)) log.AppendLine($"    {x}");
            }
            log.AppendLine("  aquecimento da Sim: " + (aquecerResumo ?? "NÃO (sem -csm_aquecer)"));
            if (primeiroRound.Count > 0)
            {
                var o1 = primeiroRound.Select(s => s.ms).OrderBy(x => x).ToList();
                log.AppendLine($"  1º ROUND DO JOGO ({primeiroRound.Count} Steps, medido desde o início): Step nº1 {primeiroRound[0].ms:0.0} ms | p99 {Pct(o1, 0.99f):0.0} | pior {o1[o1.Count - 1]:0.0} | >33 ms: {o1.Count(x => x > 33)} | >100 ms: {o1.Count(x => x > 100)} | soma dos Steps > 33 ms: {o1.Where(x => x > 33).Sum():0} ms");
                foreach (var s in primeiroRound.OrderByDescending(s => s.ms).Take(10))
                    log.AppendLine($"    {s.ms,6:0.0} ms | t {s.t,6:0.00} | nº {s.noRound,5} | {s.aEstrelas,3} A* | {s.nos,6} cél | {s.negados,3} est");
            }
            if (nRecortes > 0) { log.AppendLine($"  1º passo de cada round no Mono ({nRecortes} rounds, desde o 1º do jogo; Match.Perfil ligado):"); log.Append(recorte); }
            if (steps.Count == 0) return;
            var ord = steps.Select(s => s.ms).OrderBy(x => x).ToList();
            log.AppendLine($"  Steps: p50 {Pct(ord, 0.5f):0.00} | p90 {Pct(ord, 0.9f):0.00} | p99 {Pct(ord, 0.99f):0.0} | p99.9 {Pct(ord, 0.999f):0.0} | pior {ord[ord.Count - 1]:0.0} ms | >33 ms: {ord.Count(x => x > 33)} | >100 ms: {ord.Count(x => x > 100)}");
            var ini = steps.Where(s => s.noRound <= 5).ToList(); var resto = steps.Where(s => s.noRound > 5).Select(s => s.ms).OrderBy(x => x).ToList();
            int rounds = steps.Count(s => s.noRound == 1);
            if (ini.Count > 0) log.AppendLine($"  começo do round (5 primeiros Steps, {rounds} rounds): pior {ini.Max(s => s.ms):0.0} ms | média {ini.Average(s => s.ms):0.00} | >100 ms: {ini.Count(s => s.ms > 100)} | resto do round: pior {(resto.Count > 0 ? resto[resto.Count - 1] : 0):0.0}, p99 {Pct(resto, 0.99f):0.0}");
            int neg = NavGrid.Negados - negados0, aE = NavGrid.AEstrelas - aEst0, ori = NavGrid.DaOrigemRespondeu - origem0, nos = NavGrid.NosAEstrela - nos0;
            log.AppendLine($"  orçamento A* ({(NavGrid.OrcamentoA ? $"ligado, {NavGrid.ORC_NOS_PASSO} células/Step" : "DESLIGADO")}): {aE} A* ({aE / (float)steps.Count:0.00}/Step), {nos} células ({nos / (float)steps.Count:0}/Step) | estimativas {neg} ({neg / (float)steps.Count:0.00}/Step, em {steps.Count(s => s.negados > 0)} Steps) | respondidas pela busca da origem {ori}");
            var nosOrd = steps.Select(s => (float)s.nos).OrderBy(x => x).ToList();
            log.AppendLine($"  células de A* por Step: p50 {Pct(nosOrd, 0.5f):0} | p99 {Pct(nosOrd, 0.99f):0} | pior {nosOrd[nosOrd.Count - 1]:0} | Steps que passaram do orçamento: {steps.Count(s => s.nos >= NavGrid.ORC_NOS_PASSO)}");
            log.AppendLine("  10 piores Steps (ms | t da partida | Step nº do round | A* | células | estimativas):");
            foreach (var s in steps.OrderByDescending(s => s.ms).Take(10))
                log.AppendLine($"    {s.ms,6:0.0} ms | t {s.t,6:0.00} | nº {s.noRound,5} | {s.aEstrelas,3} A* | {s.nos,6} cél | {s.negados,3} est");
        }
        static void RelatorioSim(StringBuilder log, int quadros)
        {
            log.AppendLine($"SIM por parte ({nSteps} Steps, {quadros} quadros) — ms por Step | ms por quadro:");
            foreach (var kv in Match.Tempos.Where(k => !k.Key.StartsWith("cérebro ·")).OrderByDescending(k => k.Value))
                log.AppendLine($"  {kv.Key,-30} {kv.Value / System.Math.Max(1, nSteps),7:0.000} | {kv.Value / System.Math.Max(1, quadros),6:0.00}");
            log.AppendLine("  cérebros por fase:");
            foreach (var kv in Match.Tempos.Where(k => k.Key.StartsWith("cérebro ·")).OrderByDescending(k => k.Value).Take(10))
                log.AppendLine($"    {kv.Key.Substring(10),-28} {kv.Value / System.Math.Max(1, nSteps),7:0.000} | {kv.Value / System.Math.Max(1, quadros),6:0.00}");
            log.AppendLine($"  NavGrid: {NavGrid.nCampo} campos inteiros = {NavGrid.msCampo:0} ms ({NavGrid.msCampo / System.Math.Max(1, NavGrid.nCampo):0.0} ms cada) | {NavGrid.nAEstrela} A* = {NavGrid.msAEstrela:0} ms ({NavGrid.msAEstrela / System.Math.Max(1, NavGrid.nAEstrela):0.00} ms cada)");
            log.AppendLine($"  pior Step {piorTotal:0.0} ms: " + string.Join(", ", Enumerable.Range(0, 6).Select(i => $"{PARTES_SIM[i]} {piorStep[i]:0.0}")));
            log.AppendLine($"  {nLentos} Steps acima de 33 ms, média deles: " + string.Join(", ", Enumerable.Range(0, 6).Select(i => $"{PARTES_SIM[i]} {somaLentos[i] / System.Math.Max(1, nLentos):0.0}")));
            RelatorioSteps(log);
        }
        public static void Junta(int parte, double ms) { soma[parte] += ms; if (ms > pico[parte]) pico[parte] = ms; }

        string saida; const float ESQUENTA = 10f, TESTE = 8f;
        float BASE = 45f;   // -csm_base_s N: duração da base em segundos
        readonly List<(string nome, System.Action liga, System.Action desliga)> testes = new List<(string, System.Action, System.Action)>();
        readonly StringBuilder log = new StringBuilder();
        readonly FrameTiming[] ft = new FrameTiming[1];
        int etapa = -2, quadro; float relogio; bool desligado;
        readonly List<float>[] q = { new List<float>(), new List<float>() };
        readonly List<double>[] g = { new List<double>(), new List<double>() };
        int gc0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Iniciar()
        {
            var args = System.Environment.GetCommandLineArgs();
            int i = System.Array.IndexOf(args, "-csm_perf");
            if (i < 0 || i + 1 >= args.Length) return;
            var go = new GameObject("Desempenho"); DontDestroyOnLoad(go);
            var d = go.AddComponent<Desempenho>(); d.saida = args[i + 1];
            int b = System.Array.IndexOf(args, "-csm_base_s");
            if (b >= 0 && b + 1 < args.Length && float.TryParse(args[b + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) d.BASE = s;
            int aq = System.Array.IndexOf(args, "-csm_aquecer");
            if (aq >= 0) aquecerMs = aq + 1 < args.Length && float.TryParse(args[aq + 1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float am) ? am : 300;
            int ap = System.Array.IndexOf(args, "-csm_aquecer_passos");
            if (ap >= 0 && ap + 1 < args.Length && int.TryParse(args[ap + 1], out int np)) { aquecerPassos = np; if (aquecerMs < 0) aquecerMs = 0; }
            // mede a Sim desde o 1º round do jogo (o custo de primeira vez cai nele, antes do aquecimento da medição)
            Ativo = true; Match.Perf = true; NavGrid.Medir = true; Match.Perfil = true;
        }

        // PartidaAoVivo3D.Start espera 2 quadros antes do Iniciar/NovaPartida: no 2º quadro o FixarCampos já saiu e o 1º Preparar
        // ainda não, então é aqui que o aquecimento da Sim entra (só com -csm_aquecer; no jogo a chamada fica na PartidaAoVivo3D)
        System.Collections.IEnumerator AquecerSim()
        {
            yield return null;
            var sw = System.Diagnostics.Stopwatch.StartNew();
            // passos do round descartável: -csm_aquecer_passos N, senão -csm_aquecer N (>0), senão o padrão da Sim (300)
            int passos = aquecerPassos > 0 ? aquecerPassos : aquecerMs > 0 ? (int)aquecerMs : 300; aquecerMs = passos;
            Match.Perf = false; Match.Perfil = false; NavGrid.Medir = false;
            try { aquecerResumo = Match.Aquecer(passos); } catch (System.Exception e) { aquecerResumo = "ERRO " + e.Message; }
            aquecerResumo = $"Match.Aquecer({aquecerMs:0}) levou {sw.Elapsed.TotalMilliseconds:0} ms no relógio{(primeiroRound.Count > 0 ? $" — ATENÇÃO: rodou DEPOIS de {primeiroRound.Count} Steps do 1º round" : " (antes do 1º Step)")} | {aquecerResumo}";
            Match.Perf = true; NavGrid.Medir = true; Match.Perfil = true;
            aEstAntes = NavGrid.AEstrelas; negAntes = NavGrid.Negados; nosAntes = NavGrid.NosAEstrela;
            Debug.Log("[Desempenho] " + aquecerResumo);
        }

        void Start()
        {
            QualitySettings.vSyncCount = 0; Application.targetFrameRate = -1;
            if (aquecerMs >= 0) StartCoroutine(AquecerSim());
            var pipe = (UniversalRenderPipelineAsset)GraphicsSettings.currentRenderPipeline;
            var cam = Camera.main; var cd = cam != null ? cam.GetUniversalAdditionalCameraData() : null;
            ScriptableRendererFeature Recurso(string tipo)
            {
                var f = typeof(UniversalRenderPipelineAsset).GetField("m_RendererDataList", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                var lista = f?.GetValue(pipe) as ScriptableRendererData[];
                return lista?.Where(d => d != null).SelectMany(d => d.rendererFeatures).FirstOrDefault(r => r != null && r.GetType().Name == tipo);
            }
            T Pos<T>() where T : VolumeComponent { var v = FindAnyObjectByType<Volume>(); return v != null && v.sharedProfile.TryGet(out T c) ? c : null; }
            void Smokes(bool sombra) { foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)) if (r.name == "smoke") r.shadowCastingMode = sombra ? ShadowCastingMode.On : ShadowCastingMode.Off; }
            int res = pipe.mainLightShadowmapResolution, msaa = pipe.msaaSampleCount;
            var ssao = Recurso("ScreenSpaceAmbientOcclusion");
            var cen = FindAnyObjectByType<Cenario3D>();
            testes.Add(("chão: topo (16 leituras)", () => Cenario3D.matMapa?.SetFloat("_UsarTopo", 0), () => Cenario3D.matMapa?.SetFloat("_UsarTopo", 1)));
            testes.Add(("sombras (todas)", () => { if (cen && cen.sol) cen.sol.shadows = LightShadows.None; }, () => { if (cen && cen.sol) cen.sol.shadows = LightShadows.Soft; }));
            testes.Add(("mapa de sombra 8192 -> 4096", () => pipe.mainLightShadowmapResolution = 4096, () => pipe.mainLightShadowmapResolution = res));
            testes.Add(("sombra da smoke", () => Smokes(false), () => Smokes(true)));
            testes.Add(("SSAO", () => ssao?.SetActive(false), () => ssao?.SetActive(true)));
            testes.Add(("MSAA 4x -> 1x", () => pipe.msaaSampleCount = 1, () => pipe.msaaSampleCount = msaa));
            testes.Add(("SMAA", () => { if (cd) cd.antialiasing = AntialiasingMode.None; }, () => { if (cd) cd.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing; }));
            testes.Add(("desfoque (DOF)", () => { var d = Pos<DepthOfField>(); if (d) d.active = false; }, () => { var d = Pos<DepthOfField>(); if (d) d.active = true; }));
            testes.Add(("bloom", () => { var b = Pos<Bloom>(); if (b) b.active = false; }, () => { var b = Pos<Bloom>(); if (b) b.active = true; }));
            testes.Add(("HUD", () => { foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = false; }, () => { foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None)) c.enabled = true; }));
            log.AppendLine($"GPU {SystemInfo.graphicsDeviceName} | CPU {SystemInfo.processorType} | {Screen.width}x{Screen.height} | sombra {res} | MSAA {msaa}");
        }

        static float Media(List<float> l) => l.Count > 0 ? l.Average() : 0;
        static float Pior(List<float> l, float p) { if (l.Count == 0) return 0; var o = l.OrderBy(x => x).ToList(); return o[Mathf.Min(o.Count - 1, (int)(o.Count * p))]; }

        void Update()
        {
            relogio += Time.unscaledDeltaTime; quadro++; etapaAtual = etapa;
            float ms = Time.unscaledDeltaTime * 1000f;
            double gpu = 0;
            FrameTimingManager.CaptureFrameTimings();
            if (FrameTimingManager.GetLatestTimings(1, ft) > 0) gpu = ft[0].gpuFrameTime;
            if (etapa == -2) { if (relogio >= ESQUENTA) { etapa = -1; relogio = 0; Ativo = true; ZerarSim(); System.Array.Clear(soma, 0, 6); System.Array.Clear(pico, 0, 6); gc0 = System.GC.CollectionCount(0); quadro = 0; } return; }
            if (etapa == -1)
            {
                q[0].Add(ms); g[0].Add(gpu);
                if (relogio < BASE) return;
                int n = q[0].Count;
                log.AppendLine($"BASE {n} quadros: média {Media(q[0]):0.00} ms ({1000 / Mathf.Max(Media(q[0]), 0.01f):0} fps) | pior 5% {Pior(q[0], 0.95f):0.0} ms | pior 1% {Pior(q[0], 0.99f):0.0} ms | pior {q[0].Max():0.0} ms | GPU {g[0].Average():0.00} ms");
                log.AppendLine($"  quadros acima de 16,7 ms: {q[0].Count(x => x > 16.7f) * 100f / n:0}% | acima de 33 ms: {q[0].Count(x => x > 33f)} | coletas de lixo: {System.GC.CollectionCount(0) - gc0}");
                for (int i = 0; i < 6; i++) log.AppendLine($"  CPU {PARTES[i],-32} média {soma[i] / n:0.00} ms | pico {pico[i]:0.0} ms");
                Ativo = false; RelatorioSim(log, n); Match.Perf = false; NavGrid.Medir = false; Match.Perfil = false;
                // -csm_base: só a base (comparar versões da Sim), sem os testes de recurso
                if (System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-csm_base") >= 0) { System.IO.File.WriteAllText(saida, log.ToString()); Application.Quit(); return; }
                Proximo(); return;
            }
            // teste: alterna ligado/desligado a cada 20 quadros, ignora os 4 primeiros depois da troca
            if (quadro % 20 >= 4) { int k = desligado ? 1 : 0; q[k].Add(ms); g[k].Add(gpu); }
            if (quadro % 20 == 19) { desligado = !desligado; if (desligado) testes[etapa].liga(); else testes[etapa].desliga(); }
            if (relogio >= TESTE && !desligado) Proximo();
        }

        void Proximo()
        {
            if (etapa >= 0)
                log.AppendLine($"{testes[etapa].nome,-30} com {Media(q[0]),6:0.00} ms (GPU {(g[0].Count > 0 ? g[0].Average() : 0),5:0.00}) | sem {Media(q[1]),6:0.00} ms (GPU {(g[1].Count > 0 ? g[1].Average() : 0),5:0.00}) | custa {Media(q[0]) - Media(q[1]),5:0.00} ms");
            q[0].Clear(); q[1].Clear(); g[0].Clear(); g[1].Clear(); relogio = 0; quadro = 0; desligado = false; etapa++;
            if (etapa >= testes.Count) { System.IO.File.WriteAllText(saida, log.ToString()); Application.Quit(); }
        }
    }
}
