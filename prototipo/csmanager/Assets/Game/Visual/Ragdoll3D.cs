using System.Collections.Generic;
using UnityEngine;

namespace CSM.View3D
{
    /// <summary>
    /// Ragdoll de verdade pro boneco que morreu (física própria, sem PhysX: igual no Play e na gravação em batch).
    /// No instante da morte o esqueleto sai das PEÇAS do boneco na pose em que ele estava (ombro, cotovelo, mão, quadril,
    /// joelho, tornozelo, peito, cabeça — pontas das peças de segmento), vira 15 partículas com Verlet e restrições de
    /// distância (tronco rígido, pescoço meio mole, braços e pernas soltos, joelho que não dobra pra trás nem fecha
    /// inteiro, cotovelo que não fecha inteiro). O tiro empurra o peito/cabeça pra longe de quem matou e os joelhos
    /// cedem; gravidade, chão e parede do mapa (Efeitos3D.Piso: piso mais alto que um degrau = parede), atrito no chão;
    /// dorme parado. Cada peça segue o osso dela (posição/rotação relativas guardadas na hora da morte).
    /// Guarda a pose de cada quadro pro replay do abate (Aplicar).
    /// </summary>
    public class Ragdoll3D
    {
        enum P { Pelve, Peito, Cabeca, OmbroL, CotoveloL, MaoL, OmbroR, CotoveloR, MaoR, QuadrilL, JoelhoL, TornozeloL, QuadrilR, JoelhoR, TornozeloR, N }
        enum Osso { Tronco, Cabeca, BracoL, AnteL, BracoR, AnteR, CoxaL, CanelaL, CoxaR, CanelaR, N }

        readonly Boneco3D b; readonly Transform raiz;
        readonly Vector3[] x = new Vector3[(int)P.N], xa = new Vector3[(int)P.N];
        readonly float[] raio = new float[(int)P.N];
        readonly List<(int a, int b, float d, float k)> liga = new List<(int, int, float, float)>();
        readonly List<(int a, int b, float min)> minimo = new List<(int, int, float)>();
        // peça -> osso, posição e rotação no referencial do osso
        readonly int[] osso; readonly Vector3[] lp; readonly Quaternion[] lr; readonly bool[] usa;
        readonly Transform[] pecas;
        float t, parado; public bool dormindo;
        public const float GRAV = 7.5f;
        /// <summary>Chão fixo (estúdio da galeria, sem mapa); null = piso do mapa.</summary>
        public static float? PisoFixo;
        static float Piso(Vector3 p) => PisoFixo ?? Efeitos3D.Piso(p);
        static Vector3 Varre(Vector3 de, Vector3 ate, float alto) => PisoFixo.HasValue ? ate : Efeitos3D.Varre(de, ate, alto);
        // gravação pro replay: (tempo, pose local de cada peça)
        readonly List<(float t, Vector3[] p, Quaternion[] r)> fita = new List<(float, Vector3[], Quaternion[])>();

        static string Nome(Transform t) => t.name.Length > 2 ? t.name.Substring(2) : t.name;
        static readonly string[] CABECA = { "cabeca", "mascara", "focinho", "filtro", "tampa", "aro", "lente", "reflexo", "furo", "boca", "labio", "branco", "olho", "brilho", "estrela" };
        static readonly string[] TRONCO = { "tronco", "colete", "gola", "pente", "bolsinho", "radio", "antena", "alca", "placa", "quadril", "cinto", "bolsa", "fivela" };

        /// <summary>Pontas de uma peça de segmento (place_seg: elipsoide esticado no eixo local Y do Unity).</summary>
        static (Vector3 a, Vector3 b) Pontas(Transform t)
        {
            var s = t.localScale; float meio = Mathf.Max(0.001f, s.y - 0.6f * s.x);
            var e = t.localRotation * (Vector3.up * meio);
            return (t.localPosition - e, t.localPosition + e);
        }

        /// <summary>vel: velocidade do boneco na hora da morte (unidades do Unity/s): quem morre correndo cai com o embalo.</summary>
        public Ragdoll3D(Boneco3D boneco, Vector3 empurrao, float forca, int semente, bool cabeca = false, Vector3 vel = default)
        {
            b = boneco; raiz = b.transform;
            pecas = new Transform[raiz.childCount];
            var por = new Dictionary<string, Transform>();
            int n = 0;
            for (int i = 0; i < raiz.childCount; i++)
            {
                var c = raiz.GetChild(i); pecas[n++] = c;
                if (c.TryGetComponent<MeshRenderer>(out var mr) && mr.enabled) por[Nome(c)] = c;
            }
            System.Array.Resize(ref pecas, n);
            Vector3 W(Vector3 local) => raiz.TransformPoint(local);
            Transform T(string k) => por.TryGetValue(k, out var t) ? t : null;
            bool tr = T("pulsoL") != null;
            var quadril = T("quadril"); var tronco = T("tronco"); var cab = T("cabeca");
            x[(int)P.Pelve] = W(quadril ? quadril.localPosition : new Vector3(0, 0.3f, 0));
            x[(int)P.Peito] = W(tronco ? tronco.localPosition : new Vector3(0, 0.45f, 0));
            x[(int)P.Cabeca] = W(cab ? cab.localPosition : new Vector3(0, 0.72f, 0));
            foreach (var (sd, ombro, cotovelo, mao, quad, joelho, torno) in new[] {
                ("L", P.OmbroL, P.CotoveloL, P.MaoL, P.QuadrilL, P.JoelhoL, P.TornozeloL),
                ("R", P.OmbroR, P.CotoveloR, P.MaoR, P.QuadrilR, P.JoelhoR, P.TornozeloR) })
            {
                var peito = x[(int)P.Peito];
                var br = T("braco" + sd);
                Vector3 om = peito, co = peito;
                if (br) { var (a0, a1) = Pontas(br); a0 = W(a0); a1 = W(a1); if ((a0 - peito).sqrMagnitude > (a1 - peito).sqrMagnitude) (a0, a1) = (a1, a0); om = a0; co = a1; }
                var cotov = tr ? T("ante" + sd) : null;   // TR: a pele vai até o cotovelo
                if (cotov) { var (c0, c1) = Pontas(cotov); c0 = W(c0); c1 = W(c1); co = (c0 - om).sqrMagnitude > (c1 - om).sqrMagnitude ? c0 : c1; }
                var mt = T("mao" + sd);
                x[(int)ombro] = om; x[(int)cotovelo] = co; x[(int)mao] = mt ? W(mt.localPosition) : co;
                var cx = T("coxa" + sd); var cn = T("canela" + sd);
                Vector3 qd = x[(int)P.Pelve], jo = qd, tz = qd;
                if (cx) { var (k0, k1) = Pontas(cx); k0 = W(k0); k1 = W(k1); if ((k0 - qd).sqrMagnitude > (k1 - qd).sqrMagnitude) (k0, k1) = (k1, k0); qd = k0; jo = k1; }
                if (cn) { var (k0, k1) = Pontas(cn); k0 = W(k0); k1 = W(k1); tz = (k0 - jo).sqrMagnitude > (k1 - jo).sqrMagnitude ? k0 : k1; }
                x[(int)quad] = qd; x[(int)joelho] = jo; x[(int)torno] = tz;
            }
            float e = raiz.lossyScale.x;
            float[] r = { 0.1f, 0.12f, 0.17f, 0.05f, 0.04f, 0.035f, 0.05f, 0.04f, 0.035f, 0.055f, 0.05f, 0.045f, 0.055f, 0.05f, 0.045f };
            for (int i = 0; i < r.Length; i++) raio[i] = r[i] * e;
            // restrições: tronco rígido (todos com todos), pescoço, braços, pernas
            int[] bloco = { (int)P.Pelve, (int)P.Peito, (int)P.OmbroL, (int)P.OmbroR, (int)P.QuadrilL, (int)P.QuadrilR };
            for (int i = 0; i < bloco.Length; i++) for (int j = i + 1; j < bloco.Length; j++) Liga(bloco[i], bloco[j], 1f);
            Liga((int)P.Cabeca, (int)P.Peito, 1f); Liga((int)P.Cabeca, (int)P.OmbroL, 0.35f); Liga((int)P.Cabeca, (int)P.OmbroR, 0.35f);
            foreach (var (o, c, m, q, j, tz) in new[] { (P.OmbroL, P.CotoveloL, P.MaoL, P.QuadrilL, P.JoelhoL, P.TornozeloL), (P.OmbroR, P.CotoveloR, P.MaoR, P.QuadrilR, P.JoelhoR, P.TornozeloR) })
            {
                Liga((int)o, (int)c, 1f); Liga((int)c, (int)m, 1f); Liga((int)q, (int)j, 1f); Liga((int)j, (int)tz, 1f);
                float lb = (x[(int)o] - x[(int)c]).magnitude + (x[(int)c] - x[(int)m]).magnitude;
                float lp_ = (x[(int)q] - x[(int)j]).magnitude + (x[(int)j] - x[(int)tz]).magnitude;
                minimo.Add(((int)o, (int)m, 0.4f * lb)); minimo.Add(((int)q, (int)tz, 0.55f * lp_));
                minimo.Add(((int)m, (int)P.Cabeca, raio[(int)P.Cabeca] + raio[(int)m]));   // a mão não entra na cabeça
            }
            // velocidade inicial (xa = x - v dt). Morrer = desligar: o corpo cai pelo próprio peso (joelhos cedem pra FRENTE
            // do corpo, que é pra onde dobram, e a pelve desce), com o embalo de quem estava correndo; a bala só dá um
            // empurrão no lugar onde pegou (cabeça dá o tranco pra trás e "quica", peito leva o tronco). Nada sorteado por
            // partícula: empurrão de lado diferente em cada mão/cotovelo fazia o corpo girar no próprio eixo (usuário).
            var rng = new System.Random(semente);
            float R(float a, float c) => a + (float)rng.NextDouble() * (c - a);
            empurrao.y = 0; empurrao = empurrao.sqrMagnitude > 1e-6f ? empurrao.normalized : -raiz.forward;
            var lado = Vector3.Cross(Vector3.up, empurrao);
            var frenteCorpo = raiz.forward; frenteCorpo.y = 0; frenteCorpo = frenteCorpo.sqrMagnitude > 1e-6f ? frenteCorpo.normalized : -empurrao;
            var ladoCorpo = Vector3.Cross(Vector3.up, frenteCorpo);   // pra esquerda do boneco? (sinal não importa: simétrico)
            vel.y = 0;
            float deLado = R(-0.18f, 0.18f);              // o corpo todo tomba um pouco pra um lado (mesmo pra todas: sem torque)
            float joelhoMais = R(-0.12f, 0.12f);          // um joelho cede antes do outro: tomba de lado, não gira
            const float DT = 1 / 60f;
            for (int i = 0; i < (int)P.N; i++)
            {
                Vector3 v = vel * 0.85f + lado * deLado;
                float alto = Mathf.Clamp01((x[i].y - raiz.position.y) / (0.75f * e));
                v += empurrao * (0.12f + 0.3f * forca) * alto;   // o tiro mexe pouco no corpo inteiro
                if (cabeca)
                {
                    if (i == (int)P.Cabeca) v += empurrao * (1.5f + 1.8f * forca) + Vector3.up * 0.3f;
                    else if (i == (int)P.Peito || i == (int)P.OmbroL || i == (int)P.OmbroR) v += empurrao * (0.55f + 0.5f * forca);
                }
                else
                {
                    if (i == (int)P.Peito) v += empurrao * (0.9f + 1.1f * forca);
                    else if (i == (int)P.OmbroL || i == (int)P.OmbroR) v += empurrao * (0.45f + 0.5f * forca);
                    else if (i == (int)P.Cabeca) v += empurrao * (0.35f + 0.4f * forca);
                }
                if (i == (int)P.JoelhoL) v += frenteCorpo * (0.75f + joelhoMais) + Vector3.down * 0.5f;
                if (i == (int)P.JoelhoR) v += frenteCorpo * (0.75f - joelhoMais) + Vector3.down * 0.5f;
                // joelho vai pra frente, quadril vai pra trás e desce (desmorona dobrando); o tronco cai pra onde a bala empurrou
                if (i == (int)P.Pelve || i == (int)P.QuadrilL || i == (int)P.QuadrilR) v += Vector3.down * 0.55f - frenteCorpo * 0.35f;
                // braços amolecem: caem e abrem um pouco pra fora (os dois lados iguais: sem torque)
                if (i == (int)P.MaoL || i == (int)P.CotoveloL) v += Vector3.down * 0.35f + ladoCorpo * 0.3f;
                if (i == (int)P.MaoR || i == (int)P.CotoveloR) v += Vector3.down * 0.35f - ladoCorpo * 0.3f;
                xa[i] = x[i] - v * DT;
            }
            frente0 = Horizontal(TroncoFrente()); FrenteConfere = Vector3.Dot(frente0, frenteCorpo);
            // peças -> osso
            osso = new int[pecas.Length]; lp = new Vector3[pecas.Length]; lr = new Quaternion[pecas.Length]; usa = new bool[pecas.Length];
            var Rb = new Quaternion[(int)Osso.N]; var Ob = new Vector3[(int)Osso.N];
            for (int k = 0; k < (int)Osso.N; k++) (Ob[k], Rb[k]) = Frame(k);
            for (int i = 0; i < pecas.Length; i++)
            {
                var c = pecas[i]; string nm = Nome(c);
                if (!c.TryGetComponent<MeshRenderer>(out var mr) || !mr.enabled) continue;
                int k = OssoDe(nm, tr);
                if (k < 0) { mr.enabled = false; continue; }   // arma, granada, clarão: saem do corpo
                osso[i] = k; usa[i] = true;
                var pw = raiz.TransformPoint(c.localPosition); var rw = raiz.rotation * c.localRotation;
                var inv = Quaternion.Inverse(Rb[k]);
                lp[i] = inv * (pw - Ob[k]); lr[i] = inv * rw;
            }
            Gravar();
        }

        void Liga(int a, int c, float k) => liga.Add((a, c, (x[a] - x[c]).magnitude, k));

        static int OssoDe(string nm, bool tr)
        {
            bool Comeca(string[] l) { foreach (var s in l) if (nm.StartsWith(s)) return true; return false; }
            if (Comeca(CABECA)) return (int)Osso.Cabeca;
            if (nm.StartsWith("coldre") || nm.StartsWith("perneira")) return (int)Osso.CoxaR;   // coldre, aba, cabo, alça e as duas faixas
            if (Comeca(TRONCO)) return (int)Osso.Tronco;
            char sd = nm.Length > 0 ? nm[nm.Length - 1] : ' ';
            bool L = sd == 'L';
            if (nm.StartsWith("braco")) return L ? (int)Osso.BracoL : (int)Osso.BracoR;
            if (nm.StartsWith("ante")) return tr ? (L ? (int)Osso.BracoL : (int)Osso.BracoR) : (L ? (int)Osso.AnteL : (int)Osso.AnteR);
            if (nm.StartsWith("pulso") || nm.StartsWith("mao")) return L ? (int)Osso.AnteL : (int)Osso.AnteR;
            if (nm.StartsWith("coxa")) return L ? (int)Osso.CoxaL : (int)Osso.CoxaR;
            if (nm.StartsWith("canela") || nm.StartsWith("bota") || nm.StartsWith("joelheira")) return L ? (int)Osso.CanelaL : (int)Osso.CanelaR;
            return -1;
        }

        (Vector3 o, Quaternion r) Frame(int k)
        {
            var cima = (x[(int)P.Peito] - x[(int)P.Pelve]).normalized;
            var lado = x[(int)P.OmbroL] - x[(int)P.OmbroR]; lado = (lado - cima * Vector3.Dot(lado, cima)).normalized;
            var frente = Vector3.Cross(lado, cima);
            Quaternion Seg(int a, int c)
            {
                var ax = x[c] - x[a]; if (ax.sqrMagnitude < 1e-8f) ax = -cima;
                ax.Normalize();
                var refv = frente - ax * Vector3.Dot(frente, ax);
                if (refv.sqrMagnitude < 1e-6f) refv = cima - ax * Vector3.Dot(cima, ax);
                return Quaternion.LookRotation(refv.normalized, ax);
            }
            switch ((Osso)k)
            {
                case Osso.Tronco: return (x[(int)P.Pelve], Quaternion.LookRotation(frente, cima));
                case Osso.Cabeca:
                {
                    var up = (x[(int)P.Cabeca] - x[(int)P.Peito]).normalized;
                    var fr = frente - up * Vector3.Dot(frente, up);
                    return (x[(int)P.Cabeca], Quaternion.LookRotation(fr.sqrMagnitude > 1e-6f ? fr.normalized : frente, up));
                }
                case Osso.BracoL: return (x[(int)P.OmbroL], Seg((int)P.OmbroL, (int)P.CotoveloL));
                case Osso.AnteL: return (x[(int)P.CotoveloL], Seg((int)P.CotoveloL, (int)P.MaoL));
                case Osso.BracoR: return (x[(int)P.OmbroR], Seg((int)P.OmbroR, (int)P.CotoveloR));
                case Osso.AnteR: return (x[(int)P.CotoveloR], Seg((int)P.CotoveloR, (int)P.MaoR));
                case Osso.CoxaL: return (x[(int)P.QuadrilL], Seg((int)P.QuadrilL, (int)P.JoelhoL));
                case Osso.CanelaL: return (x[(int)P.JoelhoL], Seg((int)P.JoelhoL, (int)P.TornozeloL));
                case Osso.CoxaR: return (x[(int)P.QuadrilR], Seg((int)P.QuadrilR, (int)P.JoelhoR));
                default: return (x[(int)P.JoelhoR], Seg((int)P.JoelhoR, (int)P.TornozeloR));
            }
        }

        /// <summary>Um quadro de física (subpassos de 1/60 s) e as peças no lugar.</summary>
        public void Passo(float dt)
        {
            if (dormindo || dt <= 0) return;
            t += dt;
            int sub = Mathf.Clamp(Mathf.CeilToInt(dt * 60), 1, 6); float h = dt / sub;
            for (int s = 0; s < sub; s++) Sub(h);
            // dorme quando quase não mexe mais (corpo assentado)
            float mov = 0; for (int i = 0; i < (int)P.N; i++) mov = Mathf.Max(mov, (x[i] - xa[i]).magnitude / h);
            // medição (laboratório): quanto o tronco girou em volta da vertical desde a morte
            // (pela linha dos ombros, só enquanto ela está deitada: de lado ela fica em pé e o ângulo no chão não diz nada)
            var lado = x[(int)P.OmbroL] - x[(int)P.OmbroR]; float hz = new Vector2(lado.x, lado.z).magnitude / Mathf.Max(1e-6f, lado.magnitude);
            if (hz > 0.6f)
            {
                var fz = Horizontal(lado);
                if (fUlt.sqrMagnitude > 0) { float gy = Vector3.SignedAngle(fUlt, fz, Vector3.up); Giro += Mathf.Abs(gy); GiroLiq += gy; }
                fUlt = fz;
            }
            parado = mov < 0.05f ? parado + dt : 0;
            // assentou (ou passou de 3 s): congela de vez — corpo do CS2 fica parado, não treme nem desliza
            if (parado > 0.4f || t > 3f) dormindo = true;
            Aplicar();
            Gravar();
        }

        void Sub(float h)
        {
            var frente = TroncoFrente();
            // freio do giro em volta da vertical (o corpo inteiro rodando como pião): tira 12% da velocidade angular de yaw
            // por subpasso, sem mexer na queda nem no tombo (usuário: "gira no próprio eixo")
            {
                Vector3 c = Vector3.zero; for (int i = 0; i < (int)P.N; i++) c += x[i]; c /= (int)P.N;
                float L = 0, I = 0;
                for (int i = 0; i < (int)P.N; i++)
                {
                    var r = x[i] - c; r.y = 0; var v = (x[i] - xa[i]) / h;
                    L += r.z * v.x - r.x * v.z; I += r.sqrMagnitude;   // L = ω·I com v = ω (r.z, 0, -r.x)
                }
                if (I > 1e-6f)
                {
                    float w = L / I * 0.12f;
                    for (int i = 0; i < (int)P.N; i++) { var r = x[i] - c; r.y = 0; xa[i] += new Vector3(r.z, 0, -r.x) * w * h; }
                }
            }
            for (int i = 0; i < (int)P.N; i++)
            {
                var v = (x[i] - xa[i]) * 0.996f;
                xa[i] = x[i];
                x[i] += v + Vector3.down * GRAV * h * h;
            }
            for (int it = 0; it < 10; it++)
            {
                foreach (var (a, c, d, k) in liga)
                {
                    var dd = x[c] - x[a]; float L = dd.magnitude; if (L < 1e-6f) continue;
                    var corr = dd * ((L - d) / L * 0.5f * k);
                    x[a] += corr; x[c] -= corr;
                }
                foreach (var (a, c, mn) in minimo)
                {
                    var dd = x[c] - x[a]; float L = dd.magnitude; if (L >= mn || L < 1e-6f) continue;
                    var corr = dd * ((L - mn) / L * 0.5f);
                    x[a] += corr; x[c] -= corr;
                }
                // joelho não dobra pra trás (pra frente do tronco é o lado que dobra)
                foreach (var (q, j, tz) in new[] { (P.QuadrilL, P.JoelhoL, P.TornozeloL), (P.QuadrilR, P.JoelhoR, P.TornozeloR) })
                {
                    var meio = (x[(int)q] + x[(int)tz]) * 0.5f; float f = Vector3.Dot(x[(int)j] - meio, frente);
                    // corrige sem criar movimento: o joelho vai pra frente e quadril/tornozelo recuam metade cada (empurrar só o
                    // joelho, a cada subpasso, injetava giro e o corpo deitado ficava rodando no chão)
                    if (f < 0) { var c = frente * (-f * 0.5f); x[(int)j] += c; x[(int)q] -= c * 0.5f; x[(int)tz] -= c * 0.5f; }
                }
                Chao();
            }
        }

        Vector3 frente0, fUlt;
        /// <summary>Laboratório: giro acumulado do tronco em volta da vertical (graus) e para onde a cabeça foi em relação à
        /// pelve (mundo).</summary>
        public float Giro, GiroLiq, FrenteConfere;
        public Vector3 CabecaMenosPelve => x[(int)P.Cabeca] - x[(int)P.Pelve];
        static Vector3 Horizontal(Vector3 v) { v.y = 0; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.zero; }

        Vector3 TroncoFrente()
        {
            var cima = (x[(int)P.Peito] - x[(int)P.Pelve]).normalized;
            var lado = x[(int)P.OmbroL] - x[(int)P.OmbroR]; lado = (lado - cima * Vector3.Dot(lado, cima)).normalized;
            // (cima x lado: lado aponta pra esquerda do boneco, -X; lado x cima dava as COSTAS e o "joelho não dobra pra trás"
            // forçava o joelho a dobrar pra trás: todo mundo tombava de cara)
            return Vector3.Cross(cima, lado);
        }

        void Chao()
        {
            for (int i = 0; i < (int)P.N; i++)
            {
                // parede: varre do começo do passo até aqui e para onde o piso fica mais alto que um degrau acima da
                // partícula (o caminho todo, não só o ponto final: parede fina não é atravessada num passo grande)
                float alto = Mathf.Max(xa[i].y, x[i].y) - raio[i] + 0.12f;
                if (Piso(xa[i]) <= alto)
                {
                    var livre = Varre(xa[i], x[i], alto);
                    x[i].x = livre.x; x[i].z = livre.z;
                }
                float piso = Piso(x[i]);
                if (x[i].y < piso + raio[i])
                {
                    float vy = x[i].y - xa[i].y;   // velocidade vertical do passo (negativa caindo)
                    x[i].y = piso + raio[i];
                    // atrito: no chão a partícula quase não escorrega
                    var v = x[i] - xa[i]; xa[i].x = x[i].x - v.x * 0.55f; xa[i].z = x[i].z - v.z * 0.55f;
                    // tronco e cabeça batendo forte dão um quique baixo (corpo pesado, não bola); o resto assenta
                    bool quica = (i == (int)P.Peito || i == (int)P.Cabeca || i == (int)P.Pelve) && vy < -1.2f * (1 / 60f);
                    xa[i].y = quica ? x[i].y + vy * 0.22f : Mathf.Max(xa[i].y, x[i].y);
                }
            }
        }

        /// <summary>Põe cada peça no osso dela (pose atual da física).</summary>
        MeshRenderer[] rends;
        /// <summary>Peça que não é do corpo (arma, clarão da boca, granada) fica apagada: no replay o boneco ainda posava até
        /// a morte e a pose de tiro deixava o clarão e a arma da mão acesos, parados no ar em cima do ragdoll (usuário:
        /// "a arma cai com o efeito dela atirando congelado").</summary>
        void Apaga()
        {
            if (rends == null) { rends = new MeshRenderer[pecas.Length]; for (int i = 0; i < pecas.Length; i++) pecas[i].TryGetComponent(out rends[i]); }
            for (int i = 0; i < pecas.Length; i++) if (!usa[i] && rends[i] != null && rends[i].enabled) rends[i].enabled = false;
        }
        public void Aplicar()
        {
            Apaga();
            var O = new Vector3[(int)Osso.N]; var Rq = new Quaternion[(int)Osso.N];
            for (int k = 0; k < (int)Osso.N; k++) (O[k], Rq[k]) = Frame(k);
            var inv = Quaternion.Inverse(raiz.rotation);
            for (int i = 0; i < pecas.Length; i++)
            {
                if (!usa[i]) continue;
                int k = osso[i];
                var pw = O[k] + Rq[k] * lp[i]; var rw = Rq[k] * lr[i];
                pecas[i].localPosition = raiz.InverseTransformPoint(pw); pecas[i].localRotation = inv * rw;
            }
        }

        void Gravar()
        {
            var p = new Vector3[pecas.Length]; var r = new Quaternion[pecas.Length];
            for (int i = 0; i < pecas.Length; i++) { p[i] = pecas[i].localPosition; r[i] = pecas[i].localRotation; }
            fita.Add((t, p, r));
        }

        /// <summary>Replay: a pose gravada no instante tm depois da morte (interpola entre os quadros).</summary>
        public void Aplicar(float tm)
        {
            if (fita.Count == 0) return;
            int i1 = fita.FindIndex(f => f.t >= tm); if (i1 < 0) i1 = fita.Count - 1; int i0 = Mathf.Max(0, i1 - 1);
            var a = fita[i0]; var c = fita[i1]; float w = c.t > a.t ? Mathf.Clamp01((tm - a.t) / (c.t - a.t)) : 1;
            Apaga();
            for (int i = 0; i < pecas.Length; i++)
            {
                if (!usa[i]) continue;
                pecas[i].localPosition = Vector3.Lerp(a.p[i], c.p[i], w); pecas[i].localRotation = Quaternion.Slerp(a.r[i], c.r[i], w);
            }
        }
    }
}
