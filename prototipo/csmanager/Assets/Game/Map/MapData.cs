using System;
using System.Collections.Generic;
using CSM.Sim;

namespace CSM.Map
{
    /// <summary>
    /// Mapa original "Vila" (MVP), construído da planta aprovada (Tools/planta.ps1: grade de 40 x 35 quadradinhos).
    /// ESCALA PELO BONECO (25 de altura, ~9 de largura): quadradinho da planta = 25 unidades; porta 28 x 14; casa
    /// 36-44; 2º andar +30; plataforma/banca +12; rua de 2-3 quadradinhos = 50-75 (5-8 bonecos lado a lado).
    /// Fonte única: o visual (voxels) e a navegação saem daqui. Coordenadas: x = leste, y = sul; alturas em voxels
    /// (o pé pisa no topo da coluna).
    /// </summary>
    public static class MapData
    {
        public const int Q = 25;                  // unidades por quadradinho da planta
        public const int NX = 40 * Q, NY = 35 * Q;
        public const int GZ = 8;                  // chão
        public const int UP = GZ + 30;            // 2º andar (palácio, janelão, casarão)
        public const int CAS = UP;
        public const int PLAT = GZ + 12;          // plataforma do A e banca do B (escada)
        public const int JUMP_STEP = 10;          // degraus de pulo: caixote +10, carroça +20, tapete +30
        public const float K = Q / 20f;           // posições finas (coberturas, nichos) escritas na base antiga de 20

        public struct Rect
        {
            public int x0, x1, y0, y1;
            public Rect(int x0, int x1, int y0, int y1) { this.x0 = x0; this.x1 = x1; this.y0 = y0; this.y1 = y1; }
            public bool Contains(float x, float y) => x0 <= x && x < x1 && y0 <= y && y < y1;
            public V2 Center => new V2((x0 + x1) / 2f, (y0 + y1) / 2f);
        }

        public enum Floor : byte { Tile, Dirt, Wood, Stone, Cave, Cart }

        public struct Cover { public Rect r; public int h; public string kind; }

        /// <summary>Forma de uma área (caixa envolvente + pertence). width = largura de rua (0 = área aberta).</summary>
        public class Shape { public string name; public Rect box; public Func<int, int, bool> has; public int width; public bool stairs; public V2[] pts; }

        // ------------------------------------------------------------- grades (1 unidade)
        public static readonly bool[,] walk = new bool[NX, NY];
        public static readonly int[,] floor = new int[NX, NY];       // z do piso
        public static readonly int[,] solid = new int[NX, NY];       // topo do prédio onde não anda (0 = automático)
        public static readonly Floor[,] fmat = new Floor[NX, NY];
        public static readonly int[,] coverH = new int[NX, NY];      // cobertura acima do piso (0 = livre)
        public static readonly bool[,] stair = new bool[NX, NY];     // escada/rampa (piso inclinado)
        public static readonly List<Cover> covers = new List<Cover>();
        public static readonly List<Shape> areas = new List<Shape>();
        public static readonly Dictionary<string, Rect> SITES = new Dictionary<string, Rect>();   // zona de plantar
        public static V2[] SPAWN_T, SPAWN_CT;
        public static readonly Dictionary<string, string> CALLOUT = new Dictionary<string, string>();

        static Rect Clip(Rect r) => new Rect(Math.Max(r.x0, 0), Math.Min(r.x1, NX), Math.Max(r.y0, 0), Math.Min(r.y1, NY));
        static int S(float v) => (int)MathF.Round(v * K);
        static Rect SR(int x0, int x1, int y0, int y1) => new Rect(S(x0), S(x1), S(y0), S(y1));

        static void Carve(Shape s, string callout, Func<int, int, int> z, Floor m)
        {
            areas.Add(s); CALLOUT[s.name] = callout;
            var b = Clip(s.box);
            for (int x = b.x0; x < b.x1; x++)
                for (int y = b.y0; y < b.y1; y++)
                    if (s.has(x, y)) { walk[x, y] = true; floor[x, y] = z(x, y); fmat[x, y] = m; solid[x, y] = 0; stair[x, y] = s.stairs; }
        }

        static void Area(string name, string callout, Rect r, int z, Floor m) =>
            Carve(new Shape { name = name, box = r, has = (x, y) => r.Contains(x, y) }, callout, (x, y) => z, m);

        /// <summary>Escada/rampa retangular: o piso vai de z0 a z1 ao longo do eixo; sign = +1 sobe no sentido do eixo.</summary>
        static void Stairs(string name, string callout, Rect r, char axis, int sign, int z0, int z1)
        {
            int len = axis == 'x' ? r.x1 - r.x0 : r.y1 - r.y0;
            Carve(new Shape { name = name, box = r, has = (x, y) => r.Contains(x, y), stairs = true }, callout, (x, y) =>
            {
                int i = axis == 'x' ? x - r.x0 : y - r.y0;
                float u = (i + 0.5f) / len;
                if (sign < 0) u = 1 - u;
                return (int)MathF.Round(z0 + (z1 - z0) * u);
            }, Floor.Stone);
        }

        // ------------------------------------------------------------- planta (quadradinhos)
        static Rect Cells(int cx0, int cx1, int cy0, int cy1) => new Rect(cx0 * Q, (cx1 + 1) * Q, cy0 * Q, (cy1 + 1) * Q);
        static void Blk(string name, string callout, int cx0, int cx1, int cy0, int cy1, int z, Floor m) =>
            Area(name, callout, Cells(cx0, cx1, cy0, cy1), z, m);
        static void BlkStairs(string name, string callout, int cx0, int cx1, int cy0, int cy1, char axis, int sign, int z0, int z1) =>
            Stairs(name, callout, Cells(cx0, cx1, cy0, cy1), axis, sign, z0, z1);

        static void Wall(Rect r, int h)
        {
            for (int x = r.x0; x < r.x1; x++)
                for (int y = r.y0; y < r.y1; y++)
                {
                    int f = walk[x, y] ? floor[x, y] : GZ;
                    walk[x, y] = false; solid[x, y] = f + h;
                }
        }

        /// <summary>Prédios colados numa área elevada ficam na altura de parapeito (a câmera enxerga o terraço).</summary>
        static void LowAround(string name, int margin, int top)
        {
            foreach (var s in areas)
            {
                if (s.name != name) continue;
                var b = Clip(new Rect(s.box.x0 - margin, s.box.x1 + margin, s.box.y0 - margin, s.box.y1 + margin));
                for (int x = b.x0; x < b.x1; x++)
                    for (int y = b.y0; y < b.y1; y++)
                    {
                        if (walk[x, y] || solid[x, y] != 0) continue;
                        bool near = false;
                        for (int dx = -margin; dx <= margin && !near; dx += 2)
                            for (int dy = -margin; dy <= margin && !near; dy += 2)
                            {
                                int u = x + dx, v = y + dy;
                                near = InMap(u, v) && walk[u, v] && s.has(u, v);
                            }
                        if (near) solid[x, y] = top;
                    }
            }
        }

        /// <summary>Casa avançando sobre a rua (ferramenta MapCheck.AutoChicanes): bloco orientado.</summary>
        public static void House(float cx, float cy, float dx, float dy, float along, float deep)
        {
            V2 c = new V2(cx, cy), d = new V2(dx, dy) / MathF.Max(MathF.Sqrt(dx * dx + dy * dy), 1e-6f), n = new V2(-d.y, d.x);
            float R = MathF.Max(along, deep);
            for (int x = (int)(cx - R) - 1; x <= (int)(cx + R) + 1; x++)
                for (int y = (int)(cy - R) - 1; y <= (int)(cy + R) + 1; y++)
                {
                    if (!InMap(x, y) || !walk[x, y]) continue;
                    V2 q = new V2(x + 0.5f, y + 0.5f) - c;
                    if (MathF.Abs(q.Dot(d)) > along / 2 || MathF.Abs(q.Dot(n)) > deep / 2) continue;
                    walk[x, y] = false; solid[x, y] = 0;
                }
        }

        /// <summary>Linha de visão de olho a olho pelo chão: prédio bloqueia tudo; onde anda, piso + cobertura; o andar
        /// de cima é bloco sólido com peitoril (não se enxerga rente ao piso dele).</summary>
        public static bool LosWalk(V2 a, float zA, V2 b, float zB, float eye)
        {
            V2 d = b - a;
            float L = d.Len;
            if (L < 1e-3f) return true;
            int n = (int)L + 2;
            float ea = zA + eye, eb = zB + eye;
            for (int i = 1; i < n - 1; i++)
            {
                float t = i / (float)(n - 1);
                int x = (int)MathF.Floor(a.x + d.x * t), y = (int)MathF.Floor(a.y + d.y * t);
                if (!InMap(x, y) || !walk[x, y]) return false;
                float h = ea + (eb - ea) * t;
                if (floor[x, y] + coverH[x, y] > h) return false;
                if (floor[x, y] > GZ + 1 && !stair[x, y] && fmat[x, y] != Floor.Cart && h < floor[x, y] + 8) return false;
            }
            return true;
        }

        /// <summary>Até onde o olho enxerga numa direção (na altura do olho, chão reto), de 2 em 2 unidades.</summary>
        public static float Raio(V2 a, float zA, V2 dir, float max, float eye)
        {
            float h = zA + eye;
            for (float s = 2; s < max; s += 2)
            {
                int x = (int)MathF.Floor(a.x + dir.x * s), y = (int)MathF.Floor(a.y + dir.y * s);
                if (!InMap(x, y) || !walk[x, y] || floor[x, y] + coverH[x, y] > h) return s;
                if (floor[x, y] > GZ + 1 && !stair[x, y] && fmat[x, y] != Floor.Cart && h < floor[x, y] + 8) return s;
            }
            return max;
        }

        /// <summary>Cobertura de jogo (posição na base antiga de 20, convertida; altura em unidades).</summary>
        static void Cov(int x0, int x1, int y0, int y1, int h, string kind)
        {
            var r = SR(x0, x1, y0, y1);
            covers.Add(new Cover { r = r, h = h, kind = kind });
            for (int x = r.x0; x < r.x1; x++) for (int y = r.y0; y < r.y1; y++) coverH[x, y] = Math.Max(coverH[x, y], h);
        }

        /// <summary>Nicho de cenário: recuo fora do caminho, cheio de props (jarros, banca, plantas). Não estreita a rua.</summary>
        static void Nook(int x0, int x1, int y0, int y1, string kind)
        {
            var r = SR(x0, x1, y0, y1);
            int z = GZ;
            for (int x = r.x0 - 1; x <= r.x1 && z == GZ; x++) for (int y = r.y0 - 1; y <= r.y1 && z == GZ; y++) if (InMap(x, y) && walk[x, y]) z = floor[x, y];
            for (int x = r.x0; x < r.x1; x++) for (int y = r.y0; y < r.y1; y++) { walk[x, y] = true; floor[x, y] = z; fmat[x, y] = Floor.Stone; solid[x, y] = 0; }
            covers.Add(new Cover { r = r, h = kind == "stall" ? 16 : 10, kind = kind });
            for (int x = r.x0; x < r.x1; x++) for (int y = r.y0; y < r.y1; y++) coverH[x, y] = kind == "stall" ? 16 : 10;
        }

        static V2 P(float x, float y) => new V2(x * K, y * K);

        static MapData()
        {
            // Mesma planta aprovada (Tools/planta.ps1), na mesma ordem. Função de cada lugar está na planta.
            // ---- spawns
            Blk("CT", "base CT", 16, 24, 1, 4, GZ, Floor.Tile);
            Blk("TR", "base TR", 13, 26, 29, 33, GZ, Floor.Tile);
            // ---- bomb A com plataforma do CT no fundo
            Blk("A", "bomb A", 31, 38, 4, 11, GZ, Floor.Tile);
            Blk("Plataforma", "plataforma", 34, 38, 2, 3, PLAT, Floor.Stone);
            BlkStairs("Esc_plataforma", "escada da plataforma", 32, 33, 1, 2, 'x', 1, GZ, PLAT);
            // ---- CT -> A
            Blk("Rua_CT", "rua do CT", 25, 31, 1, 3, GZ, Floor.Dirt);
            Blk("Descida_CT", "descida do CT", 29, 31, 3, 5, GZ, Floor.Dirt);
            // ---- TR -> A pela rampa
            Blk("Rampa_saida", "saída da rampa", 27, 33, 29, 31, GZ, Floor.Dirt);
            Blk("Rampa_baixo", "rampa de baixo", 31, 33, 19, 31, GZ, Floor.Dirt);
            Blk("Rampa_dobra", "dobra da rampa", 31, 37, 16, 18, GZ, Floor.Dirt);
            Blk("Rampa", "rampa", 35, 37, 12, 16, GZ, Floor.Dirt);
            // ---- TR -> A pelo palácio (2º andar, terraço com parapeito)
            Blk("Hall_palacio", "entrada do palácio", 25, 26, 28, 28, GZ, Floor.Stone);
            BlkStairs("Esc_palacio", "escada do palácio", 25, 26, 26, 27, 'y', -1, GZ, UP);
            Blk("Palacio", "palácio", 25, 26, 18, 25, UP, Floor.Stone);
            Blk("Palacio_sala", "sala do palácio", 25, 29, 16, 17, UP, Floor.Stone);
            Blk("Palacio_corredor", "corredor da sacada", 28, 29, 13, 15, UP, Floor.Stone);
            Area("Sacada_A", "sacada do palácio", new Rect(28 * Q, 33 * Q, 12 * Q, 13 * Q + 4), UP, Floor.Stone);   // 29 de fundo (mais que isso abre palácio <-> plataforma): 1 quadradinho só não cabe o boneco
            // ---- meio: topo (entra de lado), rua do meio larga, praça CT, janelão olhando a rua
            Blk("Topo_meio", "topo do meio", 22, 23, 25, 28, GZ, Floor.Dirt);
            Blk("Topo_entrada", "entrada do topo", 21, 23, 23, 24, GZ, Floor.Dirt);
            Blk("Meio", "meio", 17, 20, 8, 24, GZ, Floor.Tile);
            Blk("Praca", "praça", 21, 23, 4, 8, GZ, Floor.Tile);   // 3 de largura: a quina não encosta no conector
            // escada de mão de madeira encostada na parede do janelão (20 de largura), sobe da praça pro oeste
            Stairs("Esc_janelao", "escada do janelão", new Rect(21 * Q, 21 * Q + 20, 6 * Q + 12, 7 * Q + 7), 'x', -1, GZ, UP);
            for (int x = 21 * Q; x < 21 * Q + 20; x++) for (int y = 6 * Q + 12; y < 7 * Q + 7; y++) fmat[x, y] = Floor.Tile;   // chão da praça embaixo
            Blk("Janelao", "janelão", 16, 20, 6, 7, UP, Floor.Stone);
            // ---- caminho de rato: beco de 1 boneco saindo do fim da curta, escada colada no muro e chega por trás
            //      do janelão (fundos, 2º andar). Muro baixo do lado do meio pra câmera enxergar o beco.
            Area("Rato", "caminho de rato", new Rect(15 * Q, 16 * Q + 19, 10 * Q, 14 * Q), GZ, Floor.Dirt);   // ~44 de largura
            BlkStairs("Esc_rato", "escada do rato", 15, 15, 8, 9, 'y', -1, GZ, UP);
            Blk("Rato_fundo", "fundos do janelão", 15, 15, 6, 7, UP, Floor.Stone);
            for (int x = 16 * Q; x < 17 * Q; x++)
                for (int y = 8 * Q; y < 14 * Q; y++)
                    if (!walk[x, y]) solid[x, y] = y < 10 * Q ? UP + 8 : GZ + 20;   // casa do lado da escada, muro reto no beco
            // ---- conector meio -> A
            Blk("Conector", "conector", 21, 26, 12, 13, GZ, Floor.Dirt);
            Blk("Conector_dobra", "dobra do conector", 25, 26, 10, 11, GZ, Floor.Dirt);
            Blk("Conector_fim", "saída do conector", 25, 30, 9, 10, GZ, Floor.Dirt);
            // ---- curta meio -> B
            Blk("Curta", "curta", 12, 16, 20, 21, GZ, Floor.Dirt);
            Blk("Curta_sobe", "curta de cima", 12, 13, 14, 19, GZ, Floor.Dirt);
            Blk("Curta_fim", "entrada da curta", 9, 15, 12, 13, GZ, Floor.Dirt);
            // ---- bomb B com banca alta e carroça embaixo do tapete (sobe pulando: caixote +10, carroça +20, tapete +30)
            Blk("B", "bomb B", 2, 9, 6, 15, GZ, Floor.Tile);
            Blk("Banca", "banca", 2, 4, 6, 8, PLAT, Floor.Stone);
            BlkStairs("Esc_banca", "escada da banca", 5, 5, 7, 8, 'x', -1, GZ, PLAT);
            // carroça de pé (comprida no sentido norte-sul, rodas do lado leste que a câmera vê), traseira encostada
            // no tapete; caixote do lado oeste pra subir
            Area("Carroca", "carroça", new Rect(6 * Q, 7 * Q, 14 * Q, 16 * Q), GZ + 2 * JUMP_STEP, Floor.Cart);
            Area("Carroca_degrau", "caixote da carroça", new Rect(5 * Q + 3, 6 * Q, 14 * Q + 18, 15 * Q + 18), GZ + JUMP_STEP, Floor.Cart);
            // ---- CT -> B: mercado e viela
            Blk("Mercado", "mercado", 12, 18, 3, 4, GZ, Floor.Dirt);
            Blk("Descida_mercado", "descida do mercado", 9, 11, 3, 5, GZ, Floor.Dirt);
            Blk("Passagem_viela", "passagem da viela", 11, 12, 1, 2, GZ, Floor.Dirt);
            Blk("Viela", "viela", 3, 10, 1, 2, GZ, Floor.Dirt);
            BlkStairs("Esc_viela", "subida da viela", 3, 4, 3, 5, 'y', 1, GZ, PLAT);
            // ---- TR -> B pelo casarão (2º andar), entrando pelo fundo oeste: sai da base pela rua do casarão (ao sul dele),
            //      contorna pelo beco do lado oeste e sobe a escada no fundo. Antes a escada saía colada na base TR e o
            //      TR chegava no B em ~9 s, quase junto com o CT (7 s, com a rotação do A levando 14 s): o B era do TR.
            Blk("Rua_B", "rua do casarão", 2, 12, 32, 33, GZ, Floor.Dirt);   // 2 de largura: 1 só não cabe o boneco
            Blk("Beco_casarao", "beco do casarão", 1, 2, 26, 33, GZ, Floor.Dirt);
            BlkStairs("Esc_casarao", "escada do casarão", 3, 4, 26, 27, 'x', 1, GZ, CAS);
            Blk("Casarao", "casarão", 5, 9, 24, 31, CAS, Floor.Wood);
            Blk("Casarao_corredor", "corredor do casarão", 7, 8, 21, 23, CAS, Floor.Wood);
            Blk("Casarao_dobra", "dobra do casarão", 4, 8, 19, 20, CAS, Floor.Wood);
            Blk("Casarao_saida", "saída do casarão", 4, 5, 17, 18, CAS, Floor.Wood);
            Blk("Tapete", "tapete", 3, 9, 16, 16, CAS, Floor.Wood);
            // ---- arco no topo da rampa (choke ~37): pilares de casa dos dois lados
            Wall(SR(700, 715, 240, 246), 36); Wall(SR(745, 760, 240, 246), 36);
            // ---- prédios colados nos terraços do 2º andar ficam na altura do parapeito
            foreach (var n in new[] { "Casarao", "Casarao_corredor", "Casarao_dobra", "Casarao_saida", "Tapete" }) LowAround(n, 6, CAS + 8);
            foreach (var n in new[] { "Palacio", "Palacio_sala", "Palacio_corredor", "Sacada_A", "Esc_palacio" }) LowAround(n, 6, UP + 8);
            foreach (var n in new[] { "Janelao", "Rato_fundo" }) LowAround(n, 6, UP + 8);
            // ---- coberturas de jogo (alturas pelo boneco: 12 agachado, 16 caixa, 28 pilha, 30 pilar)
            // Regra: encostada em parede ou na quina de fora de uma curva; nunca na boca de corredor nem no meio do
            // caminho; a boca de cada entrada fica livre 2 quadradinhos pra dentro. (base antiga: 20 = 1 quadradinho)
            // meio (bocas: praça N-leste, conector leste 12-13, curta oeste 20-21, topo leste 23-24): pilhas alternadas
            // nas paredes pro TR atravessar a mira do janelão de cobertura em cobertura
            Cov(341, 358, 236, 258, 16, "crate");                                   // parede oeste, acima da curta
            Cov(400, 420, 190, 210, 28, "crate");                                   // parede leste, perto do janelão: corta a praça
            Cov(402, 419, 300, 324, 28, "crate");                                   // parede leste, meio do meio
            Cov(341, 358, 300, 324, 28, "crate");
            Cov(341, 350, 346, 356, 12, "barrel"); Cov(341, 350, 358, 368, 12, "barrel"); // parede oeste
            Cov(402, 419, 418, 440, 28, "crate");                                   // parede leste, saída do topo (TR)
            Cov(468, 478, 516, 526, 12, "barrel");                                  // topo do meio, parede leste
            Cov(464, 478, 144, 160, 28, "crate");                                   // praça: canto leste, o CT espia o meio
            // conector e palácio: só nas quinas de fora das curvas
            Cov(524, 538, 264, 278, 28, "crate"); Cov(502, 512, 182, 192, 12, "barrel");
            Cov(502, 518, 322, 338, 28, "crate"); Cov(582, 598, 344, 358, 10, "jars");
            // rampa: pilha na parede do lado de baixo, quinas das duas curvas
            Cov(620, 636, 480, 502, 28, "crate"); Cov(668, 678, 420, 430, 12, "barrel");
            Cov(620, 638, 320, 338, 28, "crate"); Cov(736, 760, 360, 380, 28, "crate"); Cov(664, 690, 362, 380, 28, "crate");
            Cov(272, 280, 340, 348, 12, "barrel");                                  // curta de cima, parede leste
            // A (bocas: descida NO, plataforma N, conector O 9-10, sacada SO, rampa S 35-37): caixote duplo no meio
            // (o "default"), pilha no canto SE fora da rampa, caixa baixa entre a sacada e a rampa, pilar a leste
            Cov(684, 708, 110, 134, 16, "crate"); Cov(687, 705, 113, 131, 28, "crate_top");   // fora da letra
            Cov(760, 780, 204, 228, 28, "crate"); Cov(732, 760, 148, 172, 28, "crate"); Cov(672, 688, 224, 240, 16, "crate"); Cov(764, 772, 140, 148, 30, "pillar");
            // B (bocas: banca/viela NO, descida do mercado NE, curta L 12-13, tapete/carroça S): caixote duplo no meio,
            // pilha a leste entre as duas bocas, pilar perto da banca, poço na parede oeste
            Cov(72, 102, 222, 252, 16, "crate"); Cov(75, 99, 225, 249, 28, "crate_top");    // lado esquerdo do bomb, solta: dá pra rodear
            Cov(156, 176, 184, 204, 28, "crate"); Cov(80, 88, 192, 200, 30, "pillar"); Cov(48, 56, 288, 296, 12, "well");
            // casarão: salão com colunas e tralha encostada, pra não ser um deck vazio
            Cov(118, 126, 516, 524, 30, "pillar"); Cov(118, 126, 590, 598, 30, "pillar"); Cov(176, 184, 516, 524, 30, "pillar");
            Cov(102, 124, 612, 636, 28, "crate"); Cov(148, 166, 556, 574, 16, "crate"); Cov(188, 198, 486, 496, 12, "barrel");
            // ---- cenário fora do caminho
            Nook(250, 280, 52, 60, "stall"); Nook(300, 330, 52, 60, "stall");      // bancas no mercado
            Nook(332, 348, 60, 68, "jars");                                         // base CT
            Nook(480, 496, 670, 684, "jars"); Nook(270, 286, 676, 684, "plants");   // base TR
            Nook(180, 196, 24, 32, "plants");                                       // viela
            // tralha variada encostada nas paredes (sacos de juta, cestos de fruta)
            Cov(284, 298, 60, 68, 8, "sacks"); Cov(232, 246, 60, 68, 7, "baskets");   // mercado, entre as bancas
            Cov(360, 376, 21, 29, 7, "baskets");                                      // base CT
            Cov(520, 538, 664, 680, 8, "sacks"); Cov(262, 276, 584, 598, 7, "baskets"); // base TR, cantos
            Cov(140, 160, 21, 29, 8, "sacks");                                        // viela
            Cov(102, 120, 484, 500, 8, "sacks");                                      // casarão, canto
            Cov(441, 455, 540, 556, 8, "sacks");                                      // topo do meio
            Cov(300, 314, 426, 440, 7, "baskets");                                    // curta
            Cov(380, 406, 606, 632, 12, "fountain");                                // fonte no meio da base TR
            Cov(440, 470, 20, 28, 10, "plants"); Cov(540, 570, 20, 28, 10, "jars"); // base CT e rua do CT, parede norte
            Cov(100, 130, 20, 28, 10, "jars");                                      // viela
            Cov(666, 676, 622, 634, 12, "barrel"); Cov(548, 570, 630, 638, 10, "plants"); // saída da rampa

            SITES["A"] = SR(636, 764, 92, 196);
            SITES["B"] = SR(100, 192, 180, 256);
            SPAWN_T = new[] { P(300, 610), P(360, 650), P(420, 620), P(470, 660), P(500, 610) };
            SPAWN_CT = new[] { P(350, 40), P(390, 70), P(430, 40), P(460, 80), P(480, 50) };
        }

        public static bool InMap(int x, int y) => x >= 0 && y >= 0 && x < NX && y < NY;

        // telhados: lotes retangulares alinhados com as ruas (50 x 38, fileiras deslocadas), altura própria por lote
        static readonly int[] HS = { 36, 40, 44, 40 };
        static int Hash(int a, int b) { unchecked { int h = a * 73856093 ^ b * 19349663; h ^= h >> 13; h *= 0x5bd1e995; return (h ^ (h >> 15)) & 0x7fffffff; } }
        /// <summary>Altura da casa (lote) que ocupa o ponto.</summary>
        public static int BlockH(int x, int y)
        {
            int row = y / 38;
            int col = (x + (row % 2) * 25) / 50;
            return GZ + HS[Hash(col + 11, row + 5) % HS.Length];
        }

        /// <summary>Topo do que ocupa a coluna (piso + cobertura onde anda, prédio onde não anda).</summary>
        public static int Top(int x, int y)
        {
            if (!InMap(x, y)) return 99;
            if (walk[x, y]) return floor[x, y] + coverH[x, y];
            return solid[x, y] > 0 ? solid[x, y] : BlockH(x, y);
        }

        /// <summary>Área (nome interno) que contém o ponto; a definida por último ganha.</summary>
        public static string AreaAt(float x, float y)
        {
            int ix = (int)x, iy = (int)y;
            for (int i = areas.Count - 1; i >= 0; i--)
                if (areas[i].box.Contains(ix, iy) && areas[i].has(ix, iy)) return areas[i].name;
            return null;
        }

        public static bool InArea(string name, int x, int y)
        {
            foreach (var s in areas) if (s.name == name && s.box.Contains(x, y) && s.has(x, y)) return true;
            return false;
        }

        public static string Callout(V2 p)
        {
            string a = AreaAt(p.x, p.y);
            return a != null && CALLOUT.TryGetValue(a, out var c) ? c : "?";
        }
    }
}
