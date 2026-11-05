using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Enfeites de fachada de verdade no lugar dos "pintados" na parede pelo MapVoxels (no 3D eles eram tinta chapada).
    /// Refaz a MESMA conta de vão/porta/sorteio do MapVoxels (mesmos lugares do 2D), devolve a parede lisa (material 6)
    /// nas voxels pintadas e monta objetos que combinam com a vila marroquina:
    ///   tapete pendurado de verdade (listras, varão, franja), vaso de barro em suporte de ferro com planta caindo,
    ///   prateleira com tajine/bule de latão/potes, janela em arco com venezianas de madeira e floreira (no lugar do
    ///   varal), lanterna de latão em mão-francesa com painel de zellige (no lugar do nicho), toldo listrado inclinado
    ///   em cima da porta e lanterna pendurada do lado dela.
    /// Tudo encostado na parede e acima da canela (sai no máximo ~3 do muro): a folga do navmesh (raio 4) não muda.
    /// </summary>
    public static class Fachada3D
    {
        static readonly int[] PINTADOS = { 5, 8, 9, 11, 12, 13, 17, 18, 19, 30, 32, 41 };

        /// <summary>Onde vão as plantas de verdade (Natureza3D): ponto da boca do vaso (Unity), largura (jogo), modelos, semente.</summary>
        public static readonly List<(Vector3 pos, float largura, string[] opcoes, int sem)> Plantas = new List<(Vector3, float, string[], int)>();
        static readonly string[] PENDENTES = { "Fern_1" };   // vaso de parede: só a samambaia caindo (a flor roxa deitada ficava solta na parede)
        static readonly string[] DE_PORTA = { "Bush_Common", "Bush_Common_Flowers", "Plant_1", "Flower_3_Group", "Flower_4_Group" };

        /// <summary>Tira a pintura dos enfeites (volta a parede) e monta os enfeites 3D. G é alterado.</summary>
        public static GameObject Montar(byte[] G, Material mat, Transform pai)
        {
            Plantas.Clear();
            int NX = MapData.NX, NY = MapData.NY, NZ = MapVoxels.NZ;
            var walk = MapData.walk; var floor = MapData.floor;
            int Top(int x, int y) => walk[x, y] ? floor[x, y] : (MapData.solid[x, y] > 0 ? MapData.solid[x, y] : MapData.BlockH(x, y));
            bool In(int x, int y) => x >= 0 && y >= 0 && x < NX && y < NY;
            var o = new Obra3D();
            for (int x = 0; x < NX - 1; x++)
                for (int y = 0; y < NY - 1; y++)
                {
                    if (walk[x, y]) continue;
                    int fz = -1;
                    if (walk[x + 1, y]) fz = floor[x + 1, y];
                    if (walk[x, y + 1]) fz = Mathf.Max(fz, floor[x, y + 1]);
                    int top = Top(x, y), H = top - fz;
                    if (fz < 0 || H < 20) continue;
                    int s = x + y, k = s % 60, bay = s / 60;
                    bool fx = walk[x + 1, y];
                    bool dbay = bay % 5 == 2 && H >= 34 && k >= 6 && k <= 25;
                    if (dbay)
                        for (int d = 6 - k - 14; d <= 25 - k + 14 && dbay; d++)
                        {
                            int xx = fx ? x : x + d, yy = fx ? y + d : y, ux = fx ? xx + 1 : xx, uy = fx ? yy : yy + 1;
                            dbay = In(xx, yy) && In(ux, uy) && !walk[xx, yy] && walk[ux, uy] && floor[ux, uy] == fz && Top(xx, yy) - fz >= 34;
                        }
                    int v = (int)(((uint)(bay * 73 + (fx ? x : y) / 25 * 31 + (fx ? 0 : 7)) * 2654435761u >> 16) % 6);
                    // devolve a parede lisa onde o MapVoxels pintou toldo, lanterna e enfeite
                    void Limpa(int z0, int z1)
                    {
                        for (int z = Mathf.Max(0, z0); z < z1 && z < NZ && z < top; z++)
                        {
                            int i = (x * NY + y) * NZ + z;
                            if (System.Array.IndexOf(PINTADOS, (int)G[i]) >= 0) G[i] = 6;
                        }
                    }
                    if (dbay && k < 24) Limpa(fz + 30, fz + 31);
                    if (dbay && k == 25) Limpa(fz + 23, fz + 25);
                    if (H >= 30 && k >= 30 && k < 54 && v <= 4) Limpa(fz + 8, fz + 27);
                    // referencial da parede (jogo): n = pra rua, t = ao longo, ponto na face da parede
                    var n = fx ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0);
                    var t = fx ? new Vector3(0, 1, 0) : new Vector3(1, 0, 0);
                    var face = new Vector3(x + (fx ? 1f : 0.5f), y + (fx ? 0.5f : 1f), fz);
                    int semente = x * 7919 + y * 104729;
                    if (dbay && k == 12) Toldo(o, face, n, t, semente);
                    // enfeite atrás de caixa/barril encostado na parede não aparece (ficava uma flor saindo atrás da caixa)
                    bool tapado = Tapado(face, n, t);
                    // parede reta dos dois lados (10 casas): na quina as duas paredes caíam no mesmo sorteio e ganhavam o
                    // enfeite grudado, um de cada lado (usuário: janela "duplicada" na quina do bomb B)
                    bool reta = true;
                    for (int d = -10; d <= 10 && reta; d++)
                    {
                        int xx = fx ? x : x + d, yy = fx ? y + d : y, ux = fx ? xx + 1 : xx, uy = fx ? yy : yy + 1;
                        reta = In(xx, yy) && In(ux, uy) && !walk[xx, yy] && walk[ux, uy] && floor[ux, uy] == fz;
                    }
                    if (dbay && (k == 6 || k == 23) && !tapado) VasoPorta(o, face, n, t, semente);
                    // (ar-condicionado saiu: usuário achou feio; parede fica lisa)
                    if (dbay && k == 25) Lanterna(o, face + Vector3.forward * 23, n, t, true);
                    if (H >= 30 && k == 42 && !tapado && reta)
                        switch (v)
                        {
                            case 0: Tapete(o, face, n, t, semente); break;
                            case 1: Vaso(o, face, n, t, semente); break;
                            case 2: Prateleira(o, face, n, t, semente); break;
                            case 3: break;   // a janelinha com floreira saiu (usuário: "horrível"); a parede fica lisa
                            case 4: Lanterna(o, face + Vector3.forward * 19, n, t, false); Zellige(o, face, n, t, semente); break;
                        }
                }
            return o.Montar("Enfeites de fachada", mat, pai);
        }

        /// <summary>Tem cobertura (caixa, barril, sacos...) encostada na parede na frente deste ponto?</summary>
        static bool Tapado(Vector3 face, Vector3 n, Vector3 t)
        {
            var a = face + n * 0.5f - t * 9; var b = face + n * 12f + t * 9;
            float x0 = Mathf.Min(a.x, b.x), x1 = Mathf.Max(a.x, b.x), y0 = Mathf.Min(a.y, b.y), y1 = Mathf.Max(a.y, b.y);
            foreach (var c in MapData.covers)
                if (c.r.x0 < x1 && c.r.x1 > x0 && c.r.y0 < y1 && c.r.y1 > y0) return true;
            return false;
        }

        // ------------------------------------------------------------------ jogo -> Unity
        static Vector3 P(Vector3 g) => MalhaMapa.ParaUnity(g.x, g.y, g.z);
        static Vector3 D(Vector3 g) => new Vector3(g.x, g.z, -g.y) * MalhaMapa.ESC;
        /// <summary>Bloco em coordenadas do jogo: centro e meia-medida ao longo de t (parede), n (pra fora) e z.</summary>
        static void B(Obra3D o, Vector3 c, Vector3 t, Vector3 n, float mt, float mn, float mz, Color cor)
            => o.Bloco(P(c), D(t * mt), D(n * mn), D(Vector3.forward * mz), cor);

        static Color Hex(int h) => new Color(((h >> 16) & 255) / 255f, ((h >> 8) & 255) / 255f, (h & 255) / 255f);
        static readonly Color MADEIRA = Hex(0xB07A44), MADEIRA_E = Hex(0x7A4E2C), FERRO = Hex(0x3A3632), LATAO = Hex(0xD9A441), LATAO_E = Hex(0x9C6E22),
            BARRO = Hex(0xC8683C), BARRO_E = Hex(0x9E4A2A), VERDE = Hex(0x4FA544), VERDE_E = Hex(0x357A38), CREME = Hex(0xF2E3C2), LUZ = Hex(0xFFE6A0),
            AZUL = Hex(0x2F7FBF), TURQUESA = Hex(0x2FA59A), VERMELHO = Hex(0xC9433A), LARANJA = Hex(0xE8892E), AZUL_E = Hex(0x23466E);

        static void Tapete(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            var rng = new System.Random(sem);
            Color[] pal = rng.Next(2) == 0 ? new[] { VERMELHO, LARANJA, CREME, AZUL_E } : new[] { AZUL_E, TURQUESA, CREME, VERMELHO };
            // varão de madeira com ponteiras
            B(o, f + n * 1.4f + Vector3.forward * 27f, t, n, 7.2f, 0.5f, 0.5f, MADEIRA_E);
            foreach (int sd in new[] { -1, 1 }) o.Bola(P(f + n * 1.4f + Vector3.forward * 27f + t * sd * 7.6f), Vector3.one * 0.7f * MalhaMapa.ESC, LATAO, 8);
            // faixas: barra, listras e losangos do meio (cores alternadas), barriga leve embaixo
            int nf = 12; float h = 13.5f / nf;
            for (int i = 0; i < nf; i++)
            {
                float z = 26.2f - h * (i + 0.5f), sai = 0.9f + 0.05f * i;
                Color c = i == 0 || i == nf - 1 ? pal[3] : (i % 3 == 1 ? pal[0] : i % 3 == 2 ? pal[1] : pal[2]);
                B(o, f + n * sai + Vector3.forward * z, t, n, 6f, 0.3f, h / 2, c);
                if (i >= 3 && i <= nf - 4 && i % 2 == 1)   // losango do meio
                    B(o, f + n * (sai + 0.32f) + Vector3.forward * z, t, n, 1.4f + (i == nf / 2 ? 0.8f : 0), 0.06f, h * 0.38f, pal[3]);
            }
            for (int i = -5; i <= 5; i++) B(o, f + n * 1.4f + t * (i * 1.1f) + Vector3.forward * 12.2f, t, n, 0.22f, 0.22f, 0.9f, CREME);   // franja
        }

        static void Vaso(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            var rng = new System.Random(sem);
            // suporte de ferro (mão-francesa) e argola
            B(o, f + n * 1.5f + Vector3.forward * 14.6f, t, n, 0.3f, 1.5f, 0.3f, FERRO);
            B(o, f + n * 0.9f + Vector3.forward * 13.4f, t, n, 0.3f, 0.3f, 1.4f, FERRO);
            var b0 = f + n * 2.6f + Vector3.forward * 14.9f;
            o.Cilindro(P(b0), D(Vector3.forward * 3.4f), 1.4f * MalhaMapa.ESC, 2.0f * MalhaMapa.ESC, 10, BARRO, BARRO_E);
            o.Cilindro(P(b0 + Vector3.forward * 3.1f), D(Vector3.forward * 0.6f), 2.15f * MalhaMapa.ESC, 2.15f * MalhaMapa.ESC, 10, BARRO_E);
            // planta de verdade (pack da Quaternius) saindo da boca do vaso — no lugar das bolotas verdes geradas
            Plantas.Add((P(b0 + Vector3.forward * 3.4f), 7f, PENDENTES, sem));
        }

        static void Prateleira(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            B(o, f + n * 1.3f + Vector3.forward * 13f, t, n, 8f, 1.3f, 0.45f, MADEIRA);
            foreach (int sd in new[] { -1, 1 }) B(o, f + n * 0.6f + t * sd * 6f + Vector3.forward * 11.6f, t, n, 0.35f, 0.6f, 1.2f, MADEIRA_E);
            var z0 = Vector3.forward * 13.45f;
            // tajine (prato + tampa cônica), bule de latão e dois potes
            var tj = f + n * 1.3f + t * -5f + z0;
            o.Cilindro(P(tj), D(Vector3.forward * 0.7f), 1.9f * MalhaMapa.ESC, 1.9f * MalhaMapa.ESC, 12, BARRO_E);
            o.Cilindro(P(tj + Vector3.forward * 0.7f), D(Vector3.forward * 2.6f), 1.6f * MalhaMapa.ESC, 0.15f * MalhaMapa.ESC, 12, TURQUESA);
            o.Bola(P(tj + Vector3.forward * 3.4f), Vector3.one * 0.35f * MalhaMapa.ESC, TURQUESA, 6);
            var bu = f + n * 1.3f + t * -0.8f + z0;
            o.Bola(P(bu + Vector3.forward * 1.3f), new Vector3(1.3f, 1.2f, 1.3f) * MalhaMapa.ESC, LATAO, 10);
            o.Cilindro(P(bu + Vector3.forward * 2.3f), D(Vector3.forward * 1.1f), 0.6f * MalhaMapa.ESC, 0.25f * MalhaMapa.ESC, 8, LATAO_E);
            o.Cilindro(P(bu + Vector3.forward * 1.0f + t * 1.0f), D(t * 1.4f + Vector3.forward * 1.0f), 0.28f * MalhaMapa.ESC, 0.15f * MalhaMapa.ESC, 6, LATAO_E);   // bico
            var p1 = f + n * 1.3f + t * 3f + z0;
            o.Cilindro(P(p1), D(Vector3.forward * 2.4f), 0.9f * MalhaMapa.ESC, 1.2f * MalhaMapa.ESC, 10, BARRO);
            var p2 = f + n * 1.3f + t * 5.8f + z0;
            o.Bola(P(p2 + Vector3.forward * 1.2f), new Vector3(1.1f, 1.3f, 1.1f) * MalhaMapa.ESC, AZUL, 10);
            o.Cilindro(P(p2 + Vector3.forward * 2.2f), D(Vector3.forward * 0.8f), 0.45f * MalhaMapa.ESC, 0.55f * MalhaMapa.ESC, 8, AZUL);
        }

        static void Janela(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            var rng = new System.Random(sem);
            Color tinta = rng.Next(3) switch { 0 => AZUL, 1 => TURQUESA, _ => Hex(0x2E8B57) };
            // vão escuro, batente claro (arco em ferradura por blocos), venezianas abertas de lado, peitoril e floreira
            B(o, f + n * 0.15f + Vector3.forward * 16f, t, n, 3.6f, 0.15f, 5.5f, Hex(0x3B2A22));
            foreach (int sd in new[] { -1, 1 }) B(o, f + n * 0.4f + t * sd * 4.1f + Vector3.forward * 15.2f, t, n, 0.6f, 0.4f, 4.8f, CREME);
            for (int i = 0; i <= 8; i++)
            {
                float a = Mathf.PI * i / 8; var c = f + n * 0.4f + Vector3.forward * (20f + Mathf.Sin(a) * 3.2f) + t * (Mathf.Cos(a) * 4.1f);
                B(o, c, t, n, 0.75f, 0.4f, 0.75f, CREME);
            }
            foreach (int sd in new[] { -1, 1 })
            {
                // veneziana meio aberta: gira pra fora pela dobradiça
                var dob = f + n * 0.5f + t * sd * 4.7f; var dir = (t * sd * 0.82f + n * 0.57f).normalized;
                var c = dob + dir * 1.9f + Vector3.forward * 15.6f;
                o.Bloco(P(c), D(dir * 1.9f), D(Vector3.Cross(Vector3.forward, dir).normalized * 0.25f), D(Vector3.forward * 4.6f), tinta);
                for (int r = 0; r < 6; r++)   // ripas
                    o.Bloco(P(c + Vector3.forward * (-3.6f + r * 1.45f) + Vector3.Cross(Vector3.forward, dir).normalized * 0.3f * -sd), D(dir * 1.6f), D(Vector3.Cross(Vector3.forward, dir).normalized * 0.12f), D(Vector3.forward * 0.25f), tinta * 0.8f);
            }
            B(o, f + n * 1.1f + Vector3.forward * 10.6f, t, n, 4.6f, 1.1f, 0.4f, CREME);
            B(o, f + n * 1.4f + Vector3.forward * 11.6f, t, n, 3.6f, 0.9f, 0.7f, BARRO);
            for (int i = -2; i <= 2; i++)
                o.Bola(P(f + n * 1.6f + t * (i * 1.5f) + Vector3.forward * 12.8f), Vector3.one * 1.0f * MalhaMapa.ESC, i % 2 == 0 ? (i == 0 ? Hex(0xE85A7A) : VERDE) : VERDE_E, 6);
        }

        static void Lanterna(Obra3D o, Vector3 b, Vector3 n, Vector3 t, bool porta)
        {
            // mão-francesa de ferro saindo da parede e a lanterna pendurada (corpo de latão de 6 lados, vidro claro, domo e
            // pináculo)
            B(o, b + n * 1.6f + Vector3.forward * 4.2f, t, n, 0.3f, 1.6f, 0.3f, FERRO);
            o.Bloco(P(b + n * 0.9f + Vector3.forward * 3.1f), D(t * 0.25f), D((n + Vector3.forward).normalized * 1.2f), D((Vector3.forward - n).normalized * 0.25f), FERRO);
            var c = b + n * 2.9f;
            o.Cilindro(P(c + Vector3.forward * 3.0f), D(Vector3.forward * 1.2f), 0.12f * MalhaMapa.ESC, 0.12f * MalhaMapa.ESC, 4, FERRO);
            o.Cilindro(P(c - Vector3.forward * 0.4f), D(Vector3.forward * 0.5f), 0.9f * MalhaMapa.ESC, 1.3f * MalhaMapa.ESC, 6, LATAO_E);
            o.Cilindro(P(c + Vector3.forward * 0.1f), D(Vector3.forward * 1.9f), 1.25f * MalhaMapa.ESC, 1.25f * MalhaMapa.ESC, 6, LUZ, null, false);
            for (int i = 0; i < 6; i++)   // varetas do corpo
            {
                float a = i * Mathf.PI / 3; var r = (t * Mathf.Cos(a) + n * Mathf.Sin(a)) * 1.3f;
                B(o, c + r + Vector3.forward * 1.05f, t, n, 0.16f, 0.16f, 0.95f, LATAO);
            }
            o.Cilindro(P(c + Vector3.forward * 2.0f), D(Vector3.forward * 1.1f), 1.45f * MalhaMapa.ESC, 0.25f * MalhaMapa.ESC, 6, LATAO);
            o.Bola(P(c + Vector3.forward * 3.2f), Vector3.one * 0.32f * MalhaMapa.ESC, LATAO, 6);
            if (porta) o.Bola(P(c - Vector3.forward * 0.6f), Vector3.one * 0.3f * MalhaMapa.ESC, LATAO, 6);
        }

        static void Zellige(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            // painel de azulejo em estrela (xadrez de cores com borda), embaixo da lanterna
            Color[] pal = { AZUL, CREME, TURQUESA, CREME, AZUL_E };
            for (int i = -3; i <= 3; i++)
                for (int j = 0; j < 6; j++)
                {
                    bool borda = Mathf.Abs(i) == 3 || j == 0 || j == 5;
                    var cor = borda ? AZUL_E : pal[(Mathf.Abs(i) + j) % pal.Length];
                    if (!borda && (i == 0 && (j == 2 || j == 3))) cor = LARANJA;
                    B(o, f + n * 0.2f + t * (i * 1.3f) + Vector3.forward * (6.2f + j * 1.3f), t, n, 0.6f, 0.2f, 0.6f, cor);
                }
        }

        /// <summary>Vaso de barro com arbusto do lado da porta (no pé da parede; sai no máximo 2,9 do muro).</summary>
        static void VasoPorta(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            var rng = new System.Random(sem);
            var b = f + n * 1.5f;   // encostado: sai no máximo 2,9 do muro (o boneco passa a 4 dele)
            o.Cilindro(P(b), D(Vector3.forward * 4.2f), 0.9f * MalhaMapa.ESC, 1.3f * MalhaMapa.ESC, 10, BARRO, BARRO_E);
            o.Cilindro(P(b + Vector3.forward * 3.9f), D(Vector3.forward * 0.6f), 1.4f * MalhaMapa.ESC, 1.4f * MalhaMapa.ESC, 10, BARRO_E);
            // moita/planta/flor de verdade (pack da Quaternius) na boca do vaso
            Plantas.Add((P(b + Vector3.forward * 4.2f), 9f, DE_PORTA, sem));
        }

        /// <summary>Ar-condicionado velho no alto da parede (caixa clara, grade escura, cano descendo).</summary>
        static void ArCondicionado(Obra3D o, Vector3 b, Vector3 n, Vector3 t)
        {
            B(o, b + n * 1.6f, t, n, 3.2f, 1.6f, 2.1f, Hex(0xE4E0D6));
            B(o, b + n * 3.25f, t, n, 2.6f, 0.06f, 1.6f, Hex(0x8F8B84));
            o.Cilindro(P(b + n * 3.33f + t * -1f), D(n * 0.1f), 1.2f * MalhaMapa.ESC, 1.2f * MalhaMapa.ESC, 12, Hex(0x6E6A64));
            foreach (int sd in new[] { -1, 1 }) B(o, b + n * 0.8f + t * sd * 2.6f - Vector3.forward * 2.6f, t, n, 0.25f, 0.8f, 0.5f, FERRO);
            B(o, b + n * 0.5f + t * 3.6f - Vector3.forward * 6f, t, n, 0.25f, 0.25f, 4f, Hex(0xBDB7AA));
        }

        static void Toldo(Obra3D o, Vector3 f, Vector3 n, Vector3 t, int sem)
        {
            // toldo de pano listrado inclinado (sai da parede em cima da porta e desce pra rua), barra de madeira e
            // franja recortada na frente; listras como a pintura do 2D (vermelho e creme)
            var alto = f + Vector3.forward * 31.5f; const float SAI = 5.5f, CAI = 3.2f;
            var desce = (n * SAI - Vector3.forward * CAI); float L = desce.magnitude; var dir = desce / L;
            var esp = Vector3.Cross(t, dir).normalized; if (Vector3.Dot(esp, Vector3.forward) < 0) esp = -esp;
            int nl = 10; float larg = 22f / nl;
            for (int i = 0; i < nl; i++)
            {
                var c = alto + t * (-11f + larg * (i + 0.5f)) + desce * 0.5f;
                o.Bloco(P(c), D(t * (larg / 2)), D(dir * (L / 2)), D(esp * 0.2f), i % 2 == 0 ? VERMELHO : CREME);
                // franja: aba curta pendurada na frente
                o.Bloco(P(alto + desce + t * (-11f + larg * (i + 0.5f)) - Vector3.forward * 0.9f), D(t * (larg / 2 - 0.1f)), D(n * 0.12f), D(Vector3.forward * 0.9f), i % 2 == 0 ? CREME : VERMELHO);
            }
            B(o, alto + n * 0.3f, t, n, 11.3f, 0.35f, 0.45f, MADEIRA_E);
            foreach (int sd in new[] { -1, 1 })   // braços de ferro
                o.Bloco(P(alto + t * sd * 10.6f + desce * 0.5f - esp * 0.4f), D(t * 0.18f), D(dir * (L / 2)), D(esp * 0.18f), FERRO);
        }
    }
}
