using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;

namespace CSM.View3D
{
    /// <summary>
    /// Enfeites miúdos do mapa como objeto de verdade (no borrão da malha do mapa eles derretiam em "blobs"): barris
    /// (no lugar dos jarros e vasos, que saíram), sacos, cestos e barris das coberturas, caixas d'água e tralhas dos telhados, e as rodas, o eixo e
    /// os varais da carroça. Os voxels deles saem da malha borrada (Limpar) e cada um vira peça de quina viva (Montar)
    /// nas MESMAS posições do MapVoxels (mesmas sementes). Só desenho: colisão e navmesh não mudam.
    /// </summary>
    public static class Adornos3D
    {
        const int NZ = CSM.View.MapVoxels.NZ;
        static int I(int x, int y, int z) => (x * MapData.NY + y) * NZ + z;
        static bool Dentro(int x, int y, int z) => x >= 0 && y >= 0 && x < MapData.NX && y < MapData.NY && z >= 0 && z < NZ;
        static readonly string[] KINDS = { "jars", "plants", "sacks", "baskets", "barrel" };

        static int Base(MapData.Cover c) => MapData.walk[c.r.x0, c.r.y0] ? MapData.floor[c.r.x0, c.r.y0] : MapData.GZ;

        static int Topo(int x, int y) => CSM.View.MapVoxels.Topo(x, y);

        /// <summary>Caixas d'água (tanque) e caixotes dos telhados: a mesma sorte do MapVoxels (Rng 4, mesma ordem).</summary>
        internal static List<(int cx, int cy, int h0, bool tanque)> Tralhas()
        {
            var l = new List<(int, int, int, bool)>();
            int NX = MapData.NX, NY = MapData.NY;
            var rng = new Rng(4);
            for (int k = 0; k < 500; k++)
            {
                int cx = 4 + rng.Int(NX - 8), cy = 4 + rng.Int(NY - 8);
                bool ok = true; int t0 = Topo(cx, cy);
                for (int x = cx - 5; x <= cx + 5 && ok; x++)
                    for (int y = cy - 5; y <= cy + 5 && ok; y++)
                        ok = MapData.InMap(x, y) && !MapData.walk[x, y] && Topo(x, y) == t0;
                if (!ok || rng.Rand() < 0.75f) continue;
                if (t0 + 6 >= NZ) continue;
                l.Add((cx, cy, t0, rng.Rand() < 0.5f));
            }
            return l;
        }

        static void Apaga(byte[] g, int x0, int x1, int y0, int y1, int z0, int z1, params byte[] quais) => Apaga(g, false, x0, x1, y0, y1, z0, z1, quais);
        /// <summary>soChao: só nas casas andáveis (o enfeite está no chão; a parede do lado tem porta pintada nas mesmas
        /// cores 8/11 e sumia junto: usuário, "uma portinha que desapareceu").</summary>
        static void Apaga(byte[] g, bool soChao, int x0, int x1, int y0, int y1, int z0, int z1, params byte[] quais)
        {
            for (int x = x0; x <= x1; x++)
                for (int y = y0; y <= y1; y++)
                    for (int z = z0; z < z1; z++)
                    {
                        if (!Dentro(x, y, z)) continue;
                        if (soChao && !MapData.walk[x, y] && MapData.coverH[x, y] == 0) continue;   // parede (prédio), não a cobertura
                        int i = I(x, y, z); if (System.Array.IndexOf(quais, g[i]) >= 0) g[i] = 0;
                    }
        }

        /// <summary>Tira das voxels (cópia) os enfeites que viram objeto aqui.</summary>
        public static byte[] Limpar(byte[] G)
        {
            var g = (byte[])G.Clone();
            foreach (var c in MapData.covers)
            {
                if (System.Array.IndexOf(KINDS, CSM.View.MapVoxels.Tipo(c.kind)) < 0) continue;
                int f = Base(c);
                Apaga(g, true, c.r.x0 - 5, c.r.x1 + 5, c.r.y0 - 5, c.r.y1 + 5, f, f + Mathf.Max(c.h, 16) + 1, 12, 13, 17, 43, 38, 10, 11, 41, 30);
            }
            foreach (var t in Tralhas())
            {
                if (t.tanque) Apaga(g, t.cx - 3, t.cx + 3, t.cy - 3, t.cy + 3, t.h0, t.h0 + 6, 15, 8);
                else Apaga(g, t.cx - 2, t.cx + 2, t.cy - 2, t.cy + 2, t.h0, t.h0 + 3, 10, 11);
            }
            foreach (var s in MapData.areas)
            {
                if (s.name == "Esc_janelao") { var lb = s.box; Apaga(g, lb.x0, lb.x1 - 1, lb.y0, lb.y1 - 1, MapData.GZ, MapData.UP + 8, 10, 11); continue; }
                if (s.name != "Carroca") continue;
                var cb = s.box; int bed = MapData.floor[cb.x0, cb.y0];
                Apaga(g, cb.x0, cb.x1 - 1, cb.y0 + 1, cb.y1 - 1, MapData.GZ, bed - 5, 8, 10, 11);   // rodas e eixos (o vão embaixo da caçamba)
                Apaga(g, cb.x0, cb.x1 - 1, cb.y0 - 31, cb.y0 - 1, MapData.GZ, bed, 11);              // varais
            }
            return g;
        }

        // ------------------------------------------------------------------ peças (coordenadas do jogo -> Unity)
        static Vector3 P(float x, float y, float z) => MalhaMapa.ParaUnity(x, y, z);
        static Vector3 D(float x, float y, float z) => new Vector3(x, z, -y) * MalhaMapa.ESC;   // vetor do jogo -> Unity
        static Vector3 R(float x, float y, float z) => new Vector3(x, z, y) * MalhaMapa.ESC;    // raios/meias-medidas (positivos)
        static Color Cor(int m) => Paleta3D.Srgb(m);
        static Color Mul(Color c, float k) => new Color(c.r * k, c.g * k, c.b * k, 1);

        static void Saco(Obra3D o, float x, float y, float zb, System.Random r)
        {
            var juta = Mul(Cor(43), 0.9f + 0.15f * (float)r.NextDouble());
            // corpo do saco: travesseiro (bola achatada) + boca amarrada pra cima
            o.Bola(P(x, y, zb + 2.4f), R(3.3f, 2.8f, 2.3f), juta, 14);
            o.Cilindro(P(x, y, zb + 4.3f), D(0, 0, 1.0f), 0.9f * MalhaMapa.ESC, 0.5f * MalhaMapa.ESC, 8, Mul(juta, 0.92f));
            o.Cilindro(P(x, y, zb + 4.1f), D(0, 0, 0.45f), 1.05f * MalhaMapa.ESC, 1.05f * MalhaMapa.ESC, 8, Cor(38));    // cordinha
            o.Bola(P(x, y, zb + 5.4f), R(1.1f, 1.1f, 0.6f), Mul(juta, 1.05f), 8);                                        // topo franzido
        }

        static void Cesto(Obra3D o, float x, float y, float f, System.Random r)
        {
            o.Cilindro(P(x, y, f), D(0, 0, 4.6f), 2.4f * MalhaMapa.ESC, 3.1f * MalhaMapa.ESC, 14, Cor(10), Cor(11));
            o.Cilindro(P(x, y, f + 4.3f), D(0, 0, 0.7f), 3.25f * MalhaMapa.ESC, 3.25f * MalhaMapa.ESC, 14, Cor(11), Cor(11));   // borda
            o.Cilindro(P(x, y, f + 1.4f), D(0, 0, 0.5f), 2.65f * MalhaMapa.ESC, 2.75f * MalhaMapa.ESC, 14, Cor(11), null, false); // trama
            int[] frutas = { 41, 30, 17 }; int fr = frutas[r.Next(3)];
            for (int i = 0; i < 5; i++)
            {
                float a = i * 1.26f, rr = i == 4 ? 0 : 1.5f;
                o.Bola(P(x + Mathf.Cos(a) * rr, y + Mathf.Sin(a) * rr, f + 5.2f + (i == 4 ? 0.8f : 0)), R(1.1f, 1.1f, 1.1f), Mul(Cor(fr), 0.9f + 0.2f * (float)r.NextDouble()), 8);
            }
        }

        static void Barril(Obra3D o, float x, float y, float f, float h)
        {
            var madeira = Cor(46); float rb = 2.5f * MalhaMapa.ESC, rm = 3.0f * MalhaMapa.ESC;
            o.Cilindro(P(x, y, f), D(0, 0, h * 0.5f), rb, rm, 16, madeira, null, true);
            o.Cilindro(P(x, y, f + h * 0.5f), D(0, 0, h * 0.5f), rm, rb, 16, madeira, Cor(47));
            foreach (float k in new[] { 0.14f, 0.8f })
                o.Cilindro(P(x, y, f + h * k), D(0, 0, 0.8f), Mathf.Lerp(rb, rm, k < 0.5f ? k * 2 : (1 - k) * 2) + 0.12f * MalhaMapa.ESC,
                    Mathf.Lerp(rb, rm, k < 0.5f ? k * 2 : (1 - k) * 2) + 0.12f * MalhaMapa.ESC, 16, Cor(19), null, false);
        }

        static void Tanque(Obra3D o, int cx, int cy, int h0)
        {
            o.Cilindro(P(cx, cy, h0), D(0, 0, 0.6f), 3.2f * MalhaMapa.ESC, 3.2f * MalhaMapa.ESC, 16, Cor(48));           // base
            o.Cilindro(P(cx, cy, h0 + 0.6f), D(0, 0, 4.6f), 2.9f * MalhaMapa.ESC, 2.9f * MalhaMapa.ESC, 16, Cor(15), Cor(8));
            o.Cilindro(P(cx, cy, h0 + 5.2f), D(0, 0, 0.8f), 1.4f * MalhaMapa.ESC, 1.0f * MalhaMapa.ESC, 12, Cor(48));     // tampa
        }

        static void Caixote(Obra3D o, int cx, int cy, int h0)
        {
            var c = P(cx + 0.5f, cy + 0.5f, h0 + 1.5f);
            o.Caixa(c, R(2.4f, 2.4f, 1.5f), Cor(10));
            foreach (var (dx, dy) in new[] { (-2.35f, 0f), (2.35f, 0f), (0f, -2.35f), (0f, 2.35f) })   // moldura de cima
                o.Caixa(P(cx + 0.5f + dx, cy + 0.5f + dy, h0 + 2.85f), R(dx == 0 ? 2.5f : 0.35f, dy == 0 ? 2.5f : 0.35f, 0.25f), Cor(11));
        }

        /// <summary>Roda raiada (aro em gomos, 8 raios, cubo) no plano y-z do jogo, com eixo ao longo de x.</summary>
        static void Roda(Obra3D o, float x, float wy, float wz, float raio, float largura)
        {
            var c = P(x, wy, wz); var eixoX = D(1, 0, 0).normalized;
            const int GOMOS = 18;
            for (int i = 0; i < GOMOS; i++)
            {
                float a = (i + 0.5f) * Mathf.PI * 2 / GOMOS;
                var rad = D(0, Mathf.Cos(a), Mathf.Sin(a)).normalized; var tan = Vector3.Cross(eixoX, rad);
                float rm = (raio - 0.8f) * MalhaMapa.ESC, comp = 2 * Mathf.PI * raio / GOMOS * 0.56f * MalhaMapa.ESC;
                o.Bloco(c + rad * rm, rad * 0.8f * MalhaMapa.ESC, tan * comp, eixoX * largura * 0.5f * MalhaMapa.ESC, Cor(11));
            }
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4;
                var rad = D(0, Mathf.Cos(a), Mathf.Sin(a)).normalized; var tan = Vector3.Cross(eixoX, rad);
                float meio = (raio - 1.6f) * 0.5f * MalhaMapa.ESC;
                o.Bloco(c + rad * (meio + 0.6f * MalhaMapa.ESC), rad * meio, tan * 0.4f * MalhaMapa.ESC, eixoX * 0.45f * MalhaMapa.ESC, Cor(10));
            }
            o.Cilindro(c - eixoX * (largura * 0.8f * MalhaMapa.ESC), eixoX * (largura * 1.6f * MalhaMapa.ESC), 1.9f * MalhaMapa.ESC, 1.9f * MalhaMapa.ESC, 12, Cor(11), Cor(48));
        }

        static void Carroca(Obra3D o)
        {
            foreach (var s in MapData.areas)
            {
                if (s.name != "Carroca") continue;
                var cb = s.box; int bed = MapData.floor[cb.x0, cb.y0];
                float wz = MapData.GZ + 7.5f; const float RW = 7.5f;
                foreach (int wy in new[] { cb.y0 + 11, cb.y1 - 11 })
                {
                    float y = wy + 1f;
                    o.Cilindro(P(cb.x0, y, wz), D(cb.x1 - cb.x0, 0, 0), 0.9f * MalhaMapa.ESC, 0.9f * MalhaMapa.ESC, 10, Cor(19));   // eixo
                    foreach (float x in new[] { cb.x0 + 1.5f, cb.x1 - 1.5f }) Roda(o, x, y, wz, RW, 2.4f);
                }
                // varais: do fundo da caçamba até o chão, pro norte
                foreach (float x in new[] { cb.x0 + 5f, cb.x1 - 4f })
                {
                    var a = P(x, cb.y0, bed - 5.5f); var b = P(x, cb.y0 - 30, MapData.GZ + 0.6f);
                    var dir = b - a; var q = Quaternion.LookRotation(dir.normalized, Vector3.up);
                    o.Caixa((a + b) * 0.5f, new Vector3(0.7f, 0.7f, dir.magnitude / MalhaMapa.ESC * 0.5f) * MalhaMapa.ESC, q, Cor(11));
                }
            }
        }

        /// <summary>Escada de mão do janelão: dois montantes de madeira inclinados (pé na praça, topo passa do piso de
        /// cima) com degraus redondos a cada 4 de altura (mesma geometria do MapVoxels, sem o borrão).</summary>
        static void Escada(Obra3D o)
        {
            foreach (var s in MapData.areas)
            {
                if (s.name != "Esc_janelao") continue;
                var lb = s.box; int ZT = MapData.UP + 6; float w = lb.x1 - lb.x0;
                float ZofX(float x) => MapData.GZ + (ZT - MapData.GZ) * (lb.x1 - x) / w;
                foreach (float y in new[] { lb.y0 + 2f, lb.y1 - 2f })
                {
                    var pe = P(lb.x1 - 0.5f, y, MapData.GZ); var topo = P(lb.x0 + 0.5f, y, ZT + 1.5f);
                    var d = topo - pe; var q = Quaternion.LookRotation(d.normalized, Vector3.up);
                    o.Caixa((pe + topo) * 0.5f, new Vector3(0.75f, 1.1f, d.magnitude / MalhaMapa.ESC * 0.5f) * MalhaMapa.ESC, q, Cor(11));
                }
                for (int zr = MapData.GZ + 3; zr < ZT; zr += 4)
                {
                    float x = lb.x1 - (zr - MapData.GZ) * w / (ZT - MapData.GZ);
                    o.Cilindro(P(x, lb.y0 + 2f, zr), D(0, lb.y1 - lb.y0 - 4f, 0), 0.55f * MalhaMapa.ESC, 0.55f * MalhaMapa.ESC, 8, Cor(10), Cor(10));
                }
            }
        }

        // (modelo, maior medida no chão em unidades do jogo)
        static readonly (string, float)[] OLARIA = { ("fp_Vase_2", 6.6f), ("fp_Vase_4", 4.2f), ("fp_Pot_1", 5.2f), ("fp_Vase_2", 5.2f), ("fp_Vase_4", 5f) };
        static readonly (string, float)[] FEIRA = { ("fp_FarmCrate_Apple", 7f), ("fp_FarmCrate_Carrot", 7f), ("fp_Barrel_Apples", 6.6f), ("fp_Bag", 5.4f), ("fp_FarmCrate_Empty", 7f) };
        static readonly (string, float)[] ARMAZEM = { ("fp_Bag", 6f), ("fp_Bag", 5.2f), ("fp_Crate_Wooden", 7f), ("fp_Barrel", 6.6f), ("fp_Bag", 5.6f) };
        static readonly (string, float)[] ADEGA = { ("fp_Barrel", 7.2f), ("fp_Barrel", 6.4f), ("fp_Bucket_Wooden_1", 4f), ("fp_Barrel_Apples", 6.8f), ("fp_Barrel", 6.8f) };

        /// <summary>Carga de cada cobertura pequena (usuário: props "mal colocados, muito repetidos", "não melhorou os props"):
        /// modelos do Fantasy Props MegaKit na proporção deles, em grupo solto dentro da MESMA área da cobertura — tema pelo
        /// tipo (jarros = olaria, cestos = feira, sacos = armazém, barril = adega) com uma peça de outro tema de vez em
        /// quando, tamanhos e giros diferentes, casa vazia aqui e ali e duas peças pequenas juntas. Colisão não muda.</summary>
        static void Carga(MapData.Cover c, int f)
        {
            var r = new System.Random(c.r.x0 * 7349 + c.r.y0 * 113 + 5);
            float R(float a, float b) => a + (float)r.NextDouble() * (b - a);
            string k = CSM.View.MapVoxels.Tipo(c.kind);
            var tema = k == "jars" || k == "plants" ? OLARIA : k == "baskets" ? FEIRA : k == "sacks" ? ARMAZEM : ADEGA;
            var outros = new[] { OLARIA, FEIRA, ARMAZEM, ADEGA };
            float w = c.r.x1 - c.r.x0, d = c.r.y1 - c.r.y0; const float PASSO = 7.2f;
            int nx = Mathf.Max(1, Mathf.FloorToInt(w / PASSO)), ny = Mathf.Max(1, Mathf.FloorToInt(d / PASSO));
            float px = w / nx, py = d / ny; int vazio = nx * ny >= 4 ? r.Next(nx * ny) : -1;
            for (int i = 0; i < nx; i++)
                for (int j = 0; j < ny; j++)
                {
                    if (i * ny + j == vazio) continue;
                    var lista = r.NextDouble() < 0.22 ? outros[r.Next(4)] : tema;
                    var (nome, lado) = lista[r.Next(lista.Length)];
                    if (nome == "fp_FarmCrate_Empty") nome = "fp_FarmCrate_Apple";
                    lado = Mathf.Min(lado * 1.22f * R(0.92f, 1.08f), Mathf.Min(px, py) * 1.12f);   // um tico maior que o tamanho real: lê melhor de cima
                    float cx = c.r.x0 + (i + 0.5f) * px, cy = c.r.y0 + (j + 0.5f) * py, folga = Mathf.Max(0, (Mathf.Min(px, py) - lado) * 0.5f);
                    bool quadrado = nome.Contains("Crate");
                    float giro = quadrado ? r.Next(4) * 90f + R(-12f, 12f) : R(0f, 360f);
                    if (lado < 6.2f && Mathf.Max(px, py) > 6.5f)
                    {   // peça pequena: duas juntas, de tamanhos diferentes
                        bool emX = px >= py; float s = 1.9f;
                        Packs3D.Modelo(nome, cx - (emX ? s : 0), cy - (emX ? 0 : s) + R(-0.4f, 0.4f), f, lado, giro);
                        var (n2, l2) = tema[r.Next(tema.Length)]; if (n2 == "fp_FarmCrate_Empty") n2 = "fp_FarmCrate_Carrot";
                        Packs3D.Modelo(n2, cx + (emX ? s : 0), cy + (emX ? 0 : s) + R(-0.4f, 0.4f), f, Mathf.Min(l2 * 1.1f, 5.4f) * R(0.85f, 1f), R(0f, 360f));
                    }
                    else Packs3D.Modelo(nome, cx + R(-folga, folga), cy + R(-folga, folga), f, lado, giro);
                }
        }

        /// <summary>Monta os enfeites (uma malha só) filho de <paramref name="pai"/>.</summary>
        public static GameObject Montar(Material mat, Transform pai)
        {
            var o = new Obra3D(); var rnd = new System.Random(7);
            // barris, sacos e cestos viram props de pack (Packs3D) quando os modelos estão no Resources; aqui só marca onde
            bool pack = Packs3D.Pronto; Packs3D.Limpar();
            foreach (var c in MapData.covers)
            {
                int f = Base(c);
                if (pack && Packs3D.TemModelos && MapData.MAPA != "brasa" && System.Array.IndexOf(KINDS, CSM.View.MapVoxels.Tipo(c.kind)) >= 0) { Carga(c, f); continue; }
                switch (CSM.View.MapVoxels.Tipo(c.kind))
                {
                    case "jars":
                    case "plants":
                    {
                        // os jarros e vasos saíram (usuário: "cara de IA"); o obstáculo do Sim continua, agora como barris
                        // lado a lado preenchendo a cobertura (sem eles o boneco bateria em nada visível)
                        var jr = new Rng(c.r.x0 * 31 + c.r.y0);
                        const float PASSO = 7.8f;   // barril de pack tem 7,4 de diâmetro (Packs3D): sem encavalar
                        int nx = Mathf.Max(1, Mathf.FloorToInt((c.r.x1 - c.r.x0 - 0.4f) / PASSO)), ny = Mathf.Max(1, Mathf.FloorToInt((c.r.y1 - c.r.y0 - 0.4f) / PASSO));
                        float x0 = (c.r.x0 + c.r.x1) * 0.5f - (nx - 1) * PASSO * 0.5f, y0 = (c.r.y0 + c.r.y1) * 0.5f - (ny - 1) * PASSO * 0.5f;
                        for (int i = 0; i < nx; i++)
                            for (int j = 0; j < ny; j++)
                            {
                                float bx = x0 + i * PASSO + jr.Uniform(-0.5f, 0.5f), by = y0 + j * PASSO + jr.Uniform(-0.5f, 0.5f), bh = Mathf.Max(7, c.h) * jr.Uniform(0.85f, 1.05f);
                                // cada nicho tem uma carga diferente (usuário: "muito repetidos"): só barris com um saco no meio,
                                // sacaria com barril nas pontas, ou caixotes de feira com sacos — nas mesmas casas da cobertura
                                int comp = (c.r.x0 * 7 + c.r.y0 * 3) % 3, pos = i + j * nx, ult = nx * ny - 1;
                                bool saco = comp == 0 ? (nx * ny >= 3 && pos % 3 == 1) : comp == 1 ? (pos != 0 && pos != ult) : (pos == ult && ult > 0);
                                bool feira = comp == 2 && !saco;
                                if (pack && saco) Packs3D.Sacos(bx, by, f, c.r.x0 * 13 + pos);
                                else if (pack && feira) Packs3D.Caixote(bx, by, f, c.r.y0 * 7 + pos);
                                else if (pack) Packs3D.Barril(bx, by, f, bh); else Barril(o, bx, by, f, bh);
                            }
                        break;
                    }
                    case "sacks":
                    case "baskets":
                    {
                        if (Brasa3D.Cuida(c.kind)) break;   // na Brasa os sacos de cimento são fardos do kit (Brasa3D)
                        var sr = new Rng(c.r.x0 * 17 + c.r.y0 * 5);
                        int n = Mathf.Max(2, (c.r.x1 - c.r.x0) * (c.r.y1 - c.r.y0) / 14);
                        if (pack)
                        {   // pack: pilhas de sacos (cada uma já tem 3) e caixotes de feira lado a lado, sem se encavalar
                            float lado = c.kind == "sacks" ? 9f : 7.4f;
                            int nx = Mathf.Max(1, Mathf.FloorToInt((c.r.x1 - c.r.x0) / lado)), ny = Mathf.Max(1, Mathf.FloorToInt((c.r.y1 - c.r.y0) / lado));
                            float px0 = (c.r.x0 + c.r.x1) * 0.5f - (nx - 1) * lado * 0.5f, py0 = (c.r.y0 + c.r.y1) * 0.5f - (ny - 1) * lado * 0.5f;
                            for (int i = 0; i < nx; i++)
                                for (int j = 0; j < ny; j++)
                                {
                                    float jx = px0 + i * lado + sr.Uniform(-0.6f, 0.6f), jy = py0 + j * lado + sr.Uniform(-0.6f, 0.6f); int sem = c.r.x0 * 131 + c.r.y0 * 17 + i * 7 + j;
                                    // mistura (usuário: "5 sacos de areia" iguais em fila): a 1ª peça é a do tipo da cobertura e as outras
                                    // alternam com barril e caixote/saco, pra pilha parecer carga de verdade e não um carimbo
                                    int qual = (i + j * 2 + c.r.x0 + c.r.y0) % 3;
                                    if (i + j == 0) qual = 0;
                                    if (qual == 1) Packs3D.Barril(jx, jy, f, Mathf.Max(7, c.h) * sr.Uniform(0.85f, 1.0f));
                                    else if ((qual == 0) == (c.kind == "sacks")) Packs3D.Sacos(jx, jy, f, sem);
                                    else Packs3D.Caixote(jx, jy, f, sem);
                                }
                        }
                        else
                        for (int i = 0; i < n; i++)
                        {
                            float jx = sr.Uniform(c.r.x0 + 3, c.r.x1 - 3), jy = sr.Uniform(c.r.y0 + 3, c.r.y1 - 3);
                            if (c.kind == "sacks") Saco(o, jx, jy, f + (i >= n * 2 / 3 ? 4 : 0), rnd); else Cesto(o, jx, jy, f, rnd);
                        }
                        break;
                    }
                    case "barrel":
                    {
                        // cobertura de barril larga (Brasa: 1 casa = 25 ou mais) vira grupo de barris lado a lado; a da
                        // Vila (estreita) segue com 1 barril no meio, igual a antes
                        // Brasa: barril maior (2 por casa de 25, como os da Inferno); passo 8,2 com o de 7,4 da Vila
                        bool grande = MapData.MAPA == "brasa";
                        float PB = grande ? 12.4f : 8.2f, DB = grande ? 11f : 7.4f;
                        int nx = Mathf.Max(1, Mathf.FloorToInt((c.r.x1 - c.r.x0 - 0.4f) / PB)), ny = Mathf.Max(1, Mathf.FloorToInt((c.r.y1 - c.r.y0 - 0.4f) / PB));
                        if (nx * ny == 1)
                        {
                            if (pack) Packs3D.Barril((c.r.x0 + c.r.x1) / 2f, (c.r.y0 + c.r.y1) / 2f, f, c.h);
                            else Barril(o, (c.r.x0 + c.r.x1) / 2f, (c.r.y0 + c.r.y1) / 2f, f, c.h);
                            break;
                        }
                        var br = new Rng(c.r.x0 * 37 + c.r.y0 * 11);
                        float bx0 = (c.r.x0 + c.r.x1) * 0.5f - (nx - 1) * PB * 0.5f, by0 = (c.r.y0 + c.r.y1) * 0.5f - (ny - 1) * PB * 0.5f;
                        for (int i = 0; i < nx; i++)
                            for (int j = 0; j < ny; j++)
                            {
                                float bx = bx0 + i * PB + br.Uniform(-0.6f, 0.6f), by = by0 + j * PB + br.Uniform(-0.6f, 0.6f), bh = c.h * br.Uniform(0.88f, 1.02f);
                                if (pack) Packs3D.Barril(bx, by, f, bh, DB); else Barril(o, bx, by, f, bh);
                            }
                        break;
                    }
                }
            }
            // (altura do telhado como o Pisos3D desenhou: no quarteirão do palácio os blocos de cima foram nivelados)
            // (no telhado do palácio não vai caixa nem caixa d'água: usuário, "essas caixas e caixa da água não!")
            foreach (var t in Tralhas()) { if (Pisos3D.NoPalacio(t.cx, t.cy)) continue; int hz = Pisos3D.Alt != null && Pisos3D.CasaDe[t.cx, t.cy] >= 0 ? Pisos3D.Alt[t.cx, t.cy] : t.h0; if (t.tanque) Tanque(o, t.cx, t.cy, hz); else Caixote(o, t.cx, t.cy, hz); }
            Carroca(o); Escada(o);
            return o.Montar("Adornos (jarros, sacos, cestos, barris, telhados, carroça)", mat, pai);
        }

    }
}
