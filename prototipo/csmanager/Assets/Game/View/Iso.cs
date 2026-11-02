using System;

namespace CSM.View
{
    /// <summary>Vetor 3D simples (mundo: x leste, y sul, z cima).</summary>
    public struct V3
    {
        public float x, y, z;
        public V3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static V3 operator +(V3 a, V3 b) => new V3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static V3 operator -(V3 a, V3 b) => new V3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static V3 operator -(V3 a) => new V3(-a.x, -a.y, -a.z);
        public static V3 operator *(V3 a, float k) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator *(float k, V3 a) => new V3(a.x * k, a.y * k, a.z * k);
        public static V3 operator /(V3 a, float k) => new V3(a.x / k, a.y / k, a.z / k);
        public float Dot(V3 b) => x * b.x + y * b.y + z * b.z;
        public float Len => MathF.Sqrt(x * x + y * y + z * z);
        public V3 Norm { get { float l = Len; return l > 1e-12f ? this / l : this; } }
        public static V3 Lerp(V3 a, V3 b, float u) => a + (b - a) * u;
        public static readonly V3 Zero = new V3(0, 0, 0);
    }

    public struct C3
    {
        public float r, g, b;
        public C3(float r, float g, float b) { this.r = r; this.g = g; this.b = b; }
        public static C3 operator *(C3 a, float k) => new C3(a.r * k, a.g * k, a.b * k);
        public static C3 operator *(C3 a, C3 k) => new C3(a.r * k.r, a.g * k.g, a.b * k.b);
        public static C3 operator +(C3 a, C3 b) => new C3(a.r + b.r, a.g + b.g, a.b + b.b);
        public static C3 operator -(C3 a, C3 b) => new C3(a.r - b.r, a.g - b.g, a.b - b.b);
        public static C3 Lerp(C3 a, C3 b, float u) => a + (b - a) * u;
    }

    /// <summary>
    /// Projeção isométrica ortográfica (elevação 50°, S = 2 px por unidade, quadro interno 256x192 ampliado x5),
    /// paleta em rampas de 4 tons por material, sol, dithering Bayer e vinheta quantizada: as decisões visuais aprovadas.
    /// </summary>
    public static class Iso
    {
        public const int W = 256, H = 192, SC = 5;
        public const float S = 2f, FT = CSM.Map.MapData.GZ, LRAY = 2000f;
        public static readonly float EL = 50f * MathF.PI / 180f;
        static readonly float R2 = MathF.Sqrt(2f);
        public static readonly V3 RV = new V3(1f / R2, -1f / R2, 0f);
        public static readonly V3 DV = new V3(-MathF.Cos(EL) / R2, -MathF.Cos(EL) / R2, -MathF.Sin(EL));
        public static readonly V3 UV = new V3(-MathF.Sin(EL) / R2, -MathF.Sin(EL) / R2, MathF.Cos(EL));
        // imagem estática inteira do mapa (1000 x 875 x 80 voxels)
        public const int BW = 2660, BH = 2144;
        public const float CXs = 1242, CYs = 106;
        public const int TL = 8;
        public static readonly C3 OUTC = new C3(42, 26, 22), BGC = new C3(26, 20, 22);
        public static readonly V3 SUN = new V3(-0.55f, -0.28f, 0.79f).Norm;
        public static readonly float[,] BAY = { { -0.075f, 0.025f }, { 0.075f, -0.025f } };
        public static readonly float[,] BAY4 = new float[4, 4];
        public static readonly float[] VQ = new float[W * H];

        public static readonly C3[,] RAMP = new C3[64, 4];
        public static readonly short[] OBJ_TAB = new short[64];

        static void Ramp(int i, int obj, params int[] c)
        {
            for (int k = 0; k < 4; k++) RAMP[i, k] = new C3(c[k * 3], c[k * 3 + 1], c[k * 3 + 2]);
            OBJ_TAB[i] = (short)obj;
        }

        public static int BandOf(float v) => v < 0.02f ? 0 : (v < 0.46f ? 1 : (v < 0.9f ? 2 : 3));

        static Iso()
        {
            // cenário desértico original
            Ramp(1, 1, 138, 100, 64, 168, 128, 84, 200, 160, 110, 228, 196, 146);
            Ramp(2, 1, 130, 94, 62, 160, 120, 80, 190, 150, 104, 220, 186, 138);
            Ramp(3, 1, 96, 66, 44, 116, 82, 54, 138, 100, 66, 166, 126, 86);
            Ramp(4, 1, 70, 48, 34, 90, 62, 42, 110, 78, 52, 140, 104, 70);
            Ramp(5, 1, 128, 44, 30, 160, 62, 40, 190, 82, 52, 220, 120, 84);
            Ramp(18, 1, 156, 122, 80, 186, 150, 102, 214, 180, 128, 238, 212, 162);
            Ramp(6, 2, 150, 106, 70, 180, 136, 92, 212, 170, 120, 238, 206, 160);
            Ramp(7, 2, 118, 82, 54, 140, 100, 66, 166, 122, 82, 196, 152, 108);
            Ramp(8, 2, 178, 140, 98, 206, 170, 124, 230, 200, 154, 250, 228, 188);
            Ramp(9, 2, 24, 58, 96, 34, 82, 132, 52, 112, 168, 100, 156, 204);
            Ramp(16, 2, 26, 76, 84, 38, 104, 112, 58, 138, 142, 110, 182, 178);
            Ramp(19, 2, 36, 24, 22, 48, 32, 28, 60, 42, 36, 80, 58, 48);
            Ramp(10, 3, 96, 60, 32, 138, 92, 50, 180, 128, 74, 226, 178, 112);
            Ramp(11, 3, 60, 36, 20, 88, 56, 30, 118, 78, 44, 156, 108, 64);
            Ramp(12, 3, 120, 56, 34, 156, 78, 46, 190, 104, 64, 224, 146, 98);
            Ramp(13, 3, 86, 38, 24, 112, 52, 32, 140, 70, 44, 176, 102, 68);
            Ramp(14, 1, 138, 104, 68, 166, 130, 88, 192, 158, 112, 218, 188, 142);
            Ramp(15, 3, 110, 100, 92, 140, 128, 116, 170, 158, 144, 204, 194, 180);
            Ramp(17, 3, 60, 96, 40, 80, 128, 52, 108, 160, 68, 150, 196, 98);
            // personagens (TR: macacão azul; capacete muda por jogador)
            Ramp(20, 10, 22, 52, 70, 34, 84, 108, 58, 124, 150, 120, 186, 204);
            Ramp(21, 10, 150, 86, 24, 222, 142, 40, 255, 190, 70, 255, 236, 150);
            Ramp(22, 10, 150, 96, 72, 204, 140, 104, 240, 184, 142, 255, 222, 190);
            Ramp(23, 10, 30, 40, 62, 48, 64, 94, 72, 94, 132, 116, 140, 180);
            Ramp(24, 10, 50, 62, 88, 78, 94, 128, 110, 130, 168, 160, 180, 214);
            Ramp(25, 10, 26, 34, 54, 40, 52, 80, 60, 76, 112, 92, 112, 152);
            Ramp(27, 10, 16, 16, 22, 28, 28, 36, 46, 46, 58, 80, 80, 98);
            Ramp(28, 10, 18, 14, 14, 32, 26, 24, 52, 44, 40, 84, 74, 66);
            Ramp(29, 10, 14, 14, 18, 24, 24, 30, 38, 38, 48, 66, 68, 84);
            Ramp(30, 10, 150, 66, 20, 214, 104, 34, 244, 140, 56, 255, 196, 110);
            Ramp(31, 10, 28, 30, 38, 48, 52, 62, 76, 82, 96, 120, 128, 146);
            Ramp(32, 10, 16, 18, 24, 30, 32, 42, 50, 54, 68, 86, 92, 112);
            Ramp(33, 10, 108, 114, 130, 156, 164, 180, 202, 210, 222, 242, 246, 252);
            Ramp(34, 10, 8, 8, 12, 14, 14, 20, 22, 22, 30, 60, 60, 76);
            // CT: capacete vermelho, roupa cáqui
            Ramp(35, 10, 78, 22, 22, 114, 34, 30, 150, 52, 42, 200, 92, 74);
            Ramp(36, 10, 20, 20, 22, 34, 34, 38, 56, 56, 62, 110, 110, 120);
            Ramp(37, 10, 62, 62, 34, 88, 88, 48, 116, 116, 66, 158, 156, 98);
            Ramp(38, 10, 96, 78, 52, 128, 106, 72, 160, 136, 96, 198, 176, 132);
            Ramp(39, 10, 88, 72, 50, 118, 98, 68, 150, 128, 92, 190, 168, 128);
            // bomba
            Ramp(40, 10, 46, 54, 34, 66, 78, 48, 90, 104, 64, 130, 146, 96);
            Ramp(41, 10, 150, 36, 30, 196, 52, 40, 230, 80, 60, 255, 150, 120);
            // capacetes dos TRs
            Ramp(42, 10, 60, 70, 34, 88, 102, 50, 118, 134, 70, 166, 182, 108);
            Ramp(43, 10, 110, 84, 40, 150, 116, 58, 190, 152, 84, 226, 196, 128);
            Ramp(44, 10, 30, 32, 40, 46, 50, 62, 68, 74, 90, 104, 110, 130);     // CT capuz grafite-azulado
            Ramp(45, 10, 22, 20, 20, 34, 32, 32, 50, 48, 48, 78, 74, 72);        // TR balaclava quase preta
            // pisos novos: tábua de madeira (casarão), junta da tábua, pedra da caverna (2 tons)
            Ramp(46, 1, 104, 66, 38, 132, 88, 52, 160, 110, 68, 190, 140, 92);
            Ramp(47, 1, 78, 48, 28, 100, 64, 38, 124, 82, 50, 150, 104, 66);
            Ramp(48, 1, 66, 58, 54, 84, 74, 68, 104, 92, 84, 128, 116, 104);
            Ramp(49, 1, 54, 48, 46, 70, 62, 58, 88, 78, 72, 110, 98, 90);
            // telhado de telha (bem diferente do chão, pra ler o que é rua e o que é telhado)
            Ramp(50, 2, 120, 62, 46, 146, 78, 58, 170, 96, 70, 196, 120, 90);
            Ramp(51, 2, 100, 50, 38, 124, 64, 48, 146, 80, 58, 170, 100, 74);
            // agentes: CT operador de máscara de gás (azul-marinho + colete oliva), TR mercenário de balaclava
            Ramp(52, 10, 26, 32, 50, 42, 52, 78, 64, 78, 110, 100, 116, 150);    // CT farda azul-marinho
            Ramp(53, 10, 46, 50, 30, 70, 76, 44, 98, 104, 62, 140, 146, 96);     // CT colete oliva
            Ramp(54, 10, 20, 22, 24, 34, 36, 40, 54, 56, 62, 90, 92, 98);        // borracha preta (máscara, luva)
            Ramp(55, 10, 12, 18, 22, 22, 38, 44, 48, 84, 94, 150, 200, 210);     // lente
            Ramp(56, 10, 30, 22, 18, 46, 34, 28, 66, 50, 40, 98, 78, 64);        // TR balaclava marrom
            Ramp(57, 10, 92, 80, 58, 124, 110, 82, 156, 142, 108, 196, 184, 146); // TR calça cáqui
            Ramp(58, 10, 50, 50, 52, 74, 74, 78, 102, 102, 106, 142, 142, 146);  // TR camiseta cinza
            Ramp(59, 10, 26, 26, 28, 40, 40, 44, 58, 58, 64, 88, 88, 96);        // TR colete grafite
            Ramp(60, 10, 40, 26, 16, 62, 40, 24, 88, 58, 36, 124, 86, 56);       // couro marrom (bota, cinto)
            Ramp(61, 10, 70, 34, 16, 104, 52, 24, 140, 74, 36, 180, 108, 60);    // madeira de arma (AK)
            Ramp(62, 10, 30, 30, 32, 46, 46, 50, 66, 66, 72, 104, 104, 112);     // metal da arma (cinza)
            Ramp(63, 10, 52, 64, 46, 76, 92, 66, 104, 124, 90, 144, 164, 126);   // verde da AWP
            Ramp(26, 10, 40, 70, 40, 60, 104, 56, 90, 140, 80, 150, 200, 130);   // vidro verde (molotov)

            int[,] b4 = { { 0, 8, 2, 10 }, { 12, 4, 14, 6 }, { 3, 11, 1, 9 }, { 15, 7, 13, 5 } };
            for (int i = 0; i < 4; i++) for (int j = 0; j < 4; j++) BAY4[i, j] = (b4[i, j] + 0.5f) / 16f - 0.5f;
            for (int jj = 0; jj < H; jj++)
                for (int ii = 0; ii < W; ii++)
                {
                    float rx = (ii + 0.5f - W / 2f) / (W / 2f), ry = (jj + 0.5f - H / 2f) / (H / 2f);
                    float rr2 = rx * rx + ry * ry;
                    float vf = Math.Clamp(1.15f - 0.45f * rr2, 0.45f, 1f);
                    VQ[jj * W + ii] = Math.Clamp(MathF.Floor(vf * 6f + 0.5f + BAY4[jj % 4, ii % 4] * 0.9f) / 6f, 0.45f, 1f);
                }
        }

        /// <summary>Ponto do mundo -> pixel na imagem estática inteira (BW x BH).</summary>
        public static (float x, float y) Project(V3 p) => (CXs + p.Dot(RV) * S, CYs - p.Dot(UV) * S);
    }

    /// <summary>Materiais dos personagens por time (índice na paleta).</summary>
    public class TeamMats
    {
        public int helm, visor, skin, vest, plate, suit, glove, boot, belt, accent, gun, gundk, can, eye;
        // visual de agente: ct = máscara de gás; senão balaclava. sleeve = braço de cima, fore = antebraço
        public bool ct; public int mask = 54, lens = 55, sleeve, fore, pants, wood = 61, metal = 62, green = 63;
        public TeamMats Clone() => (TeamMats)MemberwiseClone();
        public int this[string k] => k switch
        {
            "helm" => helm, "visor" => visor, "skin" => skin, "vest" => vest, "plate" => plate, "suit" => suit, "glove" => glove,
            "boot" => boot, "belt" => belt, "accent" => accent, "gun" => gun, "gundk" => gundk, "can" => can, _ => eye,
        };
        // TR: balaclava, camiseta cinza de manga curta, colete grafite, calça cáqui, bota de couro
        public static readonly TeamMats P = new TeamMats { helm = 45, visor = 56, skin = 22, vest = 59, plate = 59, suit = 58, glove = 54, boot = 60, belt = 60, accent = 62, gun = 31, gundk = 32, can = 33, eye = 34, sleeve = 58, fore = 22, pants = 57 };
        // CT: capuz + máscara de gás, farda azul-marinho, colete oliva, luva e bota pretas
        public static readonly TeamMats E = new TeamMats { ct = true, helm = 44, visor = 54, skin = 22, vest = 53, plate = 53, suit = 52, glove = 54, boot = 28, belt = 53, accent = 54, gun = 31, gundk = 32, can = 33, eye = 34, sleeve = 52, fore = 52, pants = 52 };
        public static readonly TeamMats[] TEAMS_T;
        static TeamMats()
        {
            int[] balas = { 45, 45, 45, 45, 45 };   // cabelo igual pra todos (time lê pela roupa)
            TEAMS_T = new TeamMats[5];
            for (int i = 0; i < 5; i++) { TEAMS_T[i] = P.Clone(); TEAMS_T[i].helm = balas[i]; }
        }
    }
}
