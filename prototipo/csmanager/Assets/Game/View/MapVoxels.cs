using System;
using CSM.Sim;
using static CSM.Map.MapData;

namespace CSM.View
{
    /// <summary>Voxels do visual a partir do MapData: chão com ladrilho/terra/madeira/pedra, prédios com telhado e
    /// tralhas, fachadas com janela e toldo, rotas elevadas/rebaixadas, escadas, coberturas e marcação dos bombs.
    /// Valor = material da paleta (0 = vazio). Grade N x N x NZ.</summary>
    public static class MapVoxels
    {
        public const int NZ = 80;
        static int I(int x, int y, int z) => (x * NY + y) * NZ + z;

        /// <summary>Tipo de desenho da cobertura: "crate_pilha"/"barrel_pilha" (Brasa: pilha que não se sobe) se desenham
        /// igual a "crate"/"barrel" — só muda a navegação dos bots.</summary>
        public static string Tipo(string kind) => kind != null && kind.EndsWith("_pilha") ? kind.Substring(0, kind.Length - 6) : kind;

        /// <summary>Altura desenhada do topo (piso onde anda, telhado onde não anda). Na Brasa o telhado sobe pro
        /// múltiplo de 12 acima (cada casa da planta tem o seu topo: sem isso o telhado virava colcha de degrauzinhos);
        /// nunca fica mais baixo que o da Sim. A Vila fica igual. Fachada3D e Adornos3D usam esta mesma.</summary>
        public static int Topo(int x, int y)
        {
            if (walk[x, y]) return floor[x, y];
            int t = solid[x, y] > 0 ? solid[x, y] : BlockH(x, y);
            if (MAPA == "brasa" && t > GZ) t = Math.Min(NZ, GZ + (int)MathF.Ceiling((t - GZ) / 12f) * 12);
            return t;
        }

        public static byte[] Build()
        {
            var G = new byte[NX * NY * NZ];
            var nz = new float[NX, NY];
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    float X = x + 0.5f, Y = y + 0.5f;
                    nz[x, y] = MathF.Sin(X * 0.21f) + MathF.Sin(Y * 0.17f + 1.3f) + MathF.Sin((X + Y) * 0.13f + 0.4f) + 0.6f * MathF.Sin(X * 0.7f - Y * 0.5f);
                }
            var top = new int[NX, NY];
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                    top[x, y] = Topo(x, y);

            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    int t = top[x, y];
                    if (walk[x, y])
                    {
                        // coluna do piso: terra embaixo, parede de prédio nas laterais de níveis altos, piso no topo
                        // carroça: caixa de madeira até o topo (sobe pulando); níveis altos: parede de prédio embaixo do piso
                        bool cart = fmat[x, y] == Floor.Cart;
                        if (stair[x, y] && AreaAt(x, y) == "Esc_janelao")
                        {
                            // embaixo da escada de mão é chão da praça; a escada vem depois
                            for (int z = 0; z < GZ - 1; z++) G[I(x, y, z)] = 4;
                            G[I(x, y, GZ - 1)] = FloorMat(x, y, nz[x, y]);
                            continue;
                        }
                        if (cart && t >= GZ + 20)
                        {
                            // caçamba da carroça: tábuas no alto, vão embaixo (rodas e eixo vêm depois)
                            for (int z = 0; z < GZ; z++) G[I(x, y, z)] = 4;
                            for (int z = t - 5; z < t - 1; z++) G[I(x, y, z)] = (byte)(y % 6 == 0 || z == t - 5 ? 11 : 10);
                            G[I(x, y, t - 1)] = FloorMat(x, y, nz[x, y]);
                            continue;
                        }
                        for (int z = 0; z < t - 1; z++) G[I(x, y, z)] = (byte)(z < GZ ? 4 : cart ? ((x + z) % 5 == 0 ? 11 : 10) : 6);
                        G[I(x, y, t - 1)] = FloorMat(x, y, nz[x, y]);
                    }
                    else
                    {
                        for (int z = 0; z < t && z < NZ; z++) G[I(x, y, z)] = 6;
                        // rodapé
                        for (int z = GZ; z < Math.Min(GZ + 2, t); z++) G[I(x, y, z)] = 7;
                    }
                }
            // telhados: grade de lajotas, borda clara (parapeito) onde o vizinho é mais baixo
            for (int x = 0; x < NX; x++)
                for (int y = 0; y < NY; y++)
                {
                    if (walk[x, y]) continue;
                    int t = top[x, y];
                    if (t < 1 || t > NZ) continue;
                    bool edge = false;
                    for (int dx = -2; dx <= 2 && !edge; dx++)
                        for (int dy = -2; dy <= 2 && !edge; dy++)
                        {
                            int u = x + dx, v = y + dy;
                            edge = !InMap(u, v) || walk[u, v] || top[u, v] < t;
                        }
                    G[I(x, y, t - 1)] = edge ? (byte)8 : (byte)((x + 2 * y) % 7 == 0 ? 51 : 50);
                    // mureta (parapeito) só na beirada que dá pra rua; entre casas é só o degrau de altura
                    bool street = false;
                    for (int dx = -2; dx <= 2 && !street; dx++)
                        for (int dy = -2; dy <= 2 && !street; dy++) { int u = x + dx, v = y + dy; street = InMap(u, v) && walk[u, v]; }
                    if (street && t < NZ) G[I(x, y, t)] = 8;
                }
            // fachadas visíveis (+x / +y) com janelas e toldos, na altura do piso vizinho
            for (int x = 0; x < NX - 1; x++)
                for (int y = 0; y < NY - 1; y++)
                {
                    if (walk[x, y]) continue;
                    int fz = -1;
                    if (walk[x + 1, y]) fz = floor[x + 1, y];
                    if (walk[x, y + 1]) fz = Math.Max(fz, floor[x, y + 1]);
                    int H = top[x, y] - fz;
                    if (fz < 0 || H < 20) continue;
                    // fachada na escala do boneco (25 de altura): porta 28 x 14 com batente, toldo e lanterna de vez em
                    // quando; pontas de viga embaixo do telhado; enfeites de parede variados
                    int s = x + y, k = s % 60, bay = s / 60;
                    void Put(int z, byte m) { if (z >= 0 && z < NZ && z < top[x, y]) G[I(x, y, z)] = m; }
                    // porta só a cada 5 vãos (~300): rua de vila tem mais parede que porta
                    // e só em parede comprida e reta: a fachada segue inteira 14 além de cada lado do batente
                    // (nada de porta em ponta de muro, quina ou parede curta)
                    bool fx = walk[x + 1, y];
                    bool dbay = bay % 5 == 2 && H >= 34 && k >= 6 && k <= 25;
                    if (dbay)
                        for (int d = 6 - k - 14; d <= 25 - k + 14 && dbay; d++)
                        {
                            int xx = fx ? x : x + d, yy = fx ? y + d : y, ux = fx ? xx + 1 : xx, uy = fx ? yy : yy + 1;
                            dbay = InMap(xx, yy) && InMap(ux, uy) && !walk[xx, yy] && walk[ux, uy] && floor[ux, uy] == fz && top[xx, yy] - fz >= 34;
                        }
                    bool door = k >= 8 && k < 22 && dbay;
                    if (door)
                        for (int z = fz; z < fz + 28; z++) Put(z, (byte)(k == 8 || k == 21 || z == fz + 27 ? 8 : 11));
                    if (dbay && k < 24)
                        Put(fz + 30, (byte)(k % 4 < 2 ? 5 : 8));                                  // toldo listrado em cima da porta
                    if (dbay && k == 25) { Put(fz + 23, 30); Put(fz + 24, 32); }              // lanterna do lado da porta
                    // sem janelas: cada vão tem um enfeite de parede diferente (sorteado por vão e por fachada)
                    if (H >= 30 && k % 12 == 0) { Put(top[x, y] - 6, 11); Put(top[x, y] - 5, 11); }   // pontas de viga
                    int v =(int)(((uint)(bay * 73 + (fx ? x : y) / 25 * 31 + (fx ? 0 : 7)) * 2654435761u >> 16) % 6);
                    if (H >= 30 && k >= 30 && k < 54)
                        switch (v)
                        {
                            case 0:   // tapete pendurado
                                if (k >= 40 && k < 50) for (int z = fz + 12; z < fz + 26; z++) Put(z, (byte)((z - fz) % 3 == 0 ? 30 : ((z - fz) % 3 == 1 ? 5 : 9)));
                                break;
                            case 1:   // vaso pendurado com planta caindo
                                if (k >= 37 && k <= 41) { Put(fz + 16, 12); Put(fz + 17, 12); Put(fz + 18, 13); }
                                if (k >= 35 && k <= 43) for (int z = fz + 19; z < fz + 23 - Math.Abs(k - 39) / 2; z++) Put(z, 17);
                                if (k == 35 || k == 43 || k == 38) for (int z = fz + 12; z < fz + 19; z++) Put(z, 17);
                                break;
                            case 2:   // prateleira de madeira com potes
                                if (k >= 32 && k < 49) Put(fz + 13, 11);
                                if (k == 34 || k == 35 || k == 39 || k == 40 || k == 44 || k == 45) for (int z = fz + 14; z < fz + 17 + k % 2; z++) Put(z, (byte)(k % 5 == 0 ? 13 : 12));
                                break;
                            case 3:   // varal com panos
                                Put(fz + 25, 11);
                                if (k >= 33 && k < 38) for (int z = fz + 18; z < fz + 25; z++) Put(z, 9);
                                if (k >= 40 && k < 45) for (int z = fz + 19; z < fz + 25; z++) Put(z, 41);
                                if (k >= 47 && k < 51) for (int z = fz + 17; z < fz + 25; z++) Put(z, 18);
                                break;
                            case 4:   // nicho em arco com jarro
                                if (k >= 38 && k <= 44 && !(k == 38 || k == 44)) for (int z = fz + 8; z < fz + 19 + (k >= 40 && k <= 42 ? 2 : 0); z++) Put(z, 19);
                                if (k >= 40 && k <= 42) for (int z = fz + 8; z < fz + 13; z++) Put(z, (byte)(z == fz + 10 ? 13 : 12));
                                break;
                        }
                }
            // carroça: rodas raiadas dos dois lados (a do leste é a que a câmera vê), eixos, guarda baixa e varais
            // apoiados no chão pra frente (norte)
            foreach (var s in areas)
            {
                if (s.name != "Carroca") continue;
                var cb = s.box; int bed = floor[cb.x0, cb.y0];
                int wz = GZ + 7; const float RW = 7.5f;
                foreach (int wy in new[] { cb.y0 + 11, cb.y1 - 11 })
                {
                    for (int x = cb.x0; x < cb.x1; x++) { G[I(x, wy, wz)] = 11; G[I(x, wy + 1, wz)] = 11; }
                    foreach (int x0 in new[] { cb.x0, cb.x1 - 3 })
                        for (int x = x0; x < x0 + 3; x++)
                            for (int y = wy - 9; y <= wy + 9; y++)
                                for (int z = GZ; z < GZ + 16; z++)
                                {
                                    float dy = y + 0.5f - wy, dz = z + 0.5f - wz, d = MathF.Sqrt(dy * dy + dz * dz);
                                    bool rim = d <= RW && d >= RW - 1.7f, hub = d < 1.8f;
                                    bool spoke = d < RW && (MathF.Abs(dy) < 0.8f || MathF.Abs(dz) < 0.8f || MathF.Abs(MathF.Abs(dy) - MathF.Abs(dz)) < 0.9f);
                                    if ((rim || hub || spoke) && InMap(x, y)) G[I(x, y, z)] = (byte)(hub ? 8 : rim ? 11 : 10);
                                }
                }
                for (int y = cb.y0; y < cb.y1; y++)
                    for (int z = bed; z < bed + 3; z++) { G[I(cb.x0, y, z)] = 11; G[I(cb.x1 - 1, y, z)] = 11; }
                for (int x = cb.x0; x < cb.x1; x++) for (int z = bed; z < bed + 3; z++) G[I(x, cb.y0, z)] = 11;
                for (int i = 0; i < 30; i++)
                {
                    int y = cb.y0 - 1 - i, z = bed - 6 - i * (bed - 6 - GZ) / 30;
                    if (!InMap(cb.x0, y)) break;
                    foreach (int x in new[] { cb.x0 + 4, cb.x1 - 5 }) { G[I(x, y, z)] = 11; G[I(x + 1, y, z)] = 11; }
                }
            }
            // escada de mão do janelão: dois montantes de madeira inclinados (pé na praça, topo passa do piso de
            // cima) e degraus a cada 4 de altura
            foreach (var s in areas)
            {
                if (s.name != "Esc_janelao") continue;
                var lb = s.box; int w = lb.x1 - lb.x0;
                int ZT = UP + 6;
                float ZofX(float x) => GZ + (ZT - GZ) * (lb.x1 - x) / w;   // x1 = pé, x0 = topo
                for (int x = lb.x0; x < lb.x1; x++)
                {
                    int za = (int)ZofX(x + 1), zb = (int)ZofX(x);
                    for (int z = za; z <= zb && z < NZ; z++)
                        foreach (int y in new[] { lb.y0 + 1, lb.y0 + 2, lb.y1 - 3, lb.y1 - 2 }) G[I(x, y, z)] = 11;
                }
                for (int zr = GZ + 3; zr < ZT; zr += 4)
                {
                    int x = (int)(lb.x1 - (zr - GZ) * w / (float)(ZT - GZ));
                    if (x < lb.x0 || x >= lb.x1) continue;
                    for (int y = lb.y0 + 1; y < lb.y1 - 1; y++) G[I(x, y, zr)] = 10;
                }
            }
            // caixas d'água e tralhas nos telhados
            var rng = new Rng(4);
            for (int k = 0; k < 500; k++)
            {
                int cx = 4 + rng.Int(NX - 8), cy = 4 + rng.Int(NY - 8);
                bool ok = true;
                for (int x = cx - 5; x <= cx + 5 && ok; x++)
                    for (int y = cy - 5; y <= cy + 5 && ok; y++)
                        ok = InMap(x, y) && !walk[x, y] && top[x, y] == top[cx, cy];
                if (!ok || rng.Rand() < 0.75f) continue;
                int h0 = top[cx, cy];
                if (h0 + 6 >= NZ) continue;
                if (rng.Rand() < 0.5f)
                {
                    for (int x = cx - 3; x <= cx + 3; x++)
                        for (int y = cy - 3; y <= cy + 3; y++)
                        {
                            float dd = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                            if (dd > 3f) continue;
                            for (int z = h0; z < h0 + 6; z++) G[I(x, y, z)] = (byte)(z < h0 + 5 ? 15 : 8);
                        }
                }
                else
                    for (int x = cx - 2; x < cx + 3; x++)
                        for (int y = cy - 2; y < cy + 3; y++)
                        {
                            for (int z = h0; z < h0 + 3; z++) G[I(x, y, z)] = 10;
                            G[I(x, y, h0 + 2)] = 11;
                        }
            }
            // marcação dos bombs: borda tracejada e letra no chão
            string[] LA = { "01110", "10001", "10001", "11111", "10001", "10001", "10001" };
            string[] LB = { "11110", "10001", "10001", "11110", "10001", "10001", "11110" };
            foreach (var kv in SITES)
            {
                var r = kv.Value;
                void Mark(int x, int y) { if (walk[x, y]) G[I(x, y, floor[x, y] - 1)] = 5; }
                for (int xx = r.x0; xx < r.x1; xx++) if ((xx / 2) % 2 == 0) { Mark(xx, r.y0); Mark(xx, r.y1 - 1); }
                for (int yy = r.y0; yy < r.y1; yy++) if ((yy / 2) % 2 == 0) { Mark(r.x0, yy); Mark(r.x1 - 1, yy); }
                var bm = kv.Key == "A" ? LA : LB;
                float cx = (r.x0 + r.x1) / 2f, cy = (r.y0 + r.y1) / 2f;
                for (int x = r.x0; x < r.x1; x++)
                    for (int y = r.y0; y < r.y1; y++)
                    {
                        float U = ((x + 0.5f - cx) - (y + 0.5f - cy)) / MathF.Sqrt(2), V = ((x + 0.5f - cx) + (y + 0.5f - cy)) / MathF.Sqrt(2);
                        int col = (int)MathF.Floor(U / 3.2f + 2.5f), row = (int)MathF.Floor(V / (3.2f * 1.3f) + 3.5f);
                        if (col >= 0 && col < 5 && row >= 0 && row < 7 && bm[row][col] == '1') Mark(x, y);
                    }
            }
            // coberturas (em cima do piso local)
            foreach (var c in covers)
            {
                int f = walk[c.r.x0, c.r.y0] ? floor[c.r.x0, c.r.y0] : GZ;
                int z1 = f + c.h;
                if (CSM.View3D.Brasa3D.Cuida(c.kind)) continue;   // Brasa: vira modelo do kit (Brasa3D)
                switch (Tipo(c.kind))
                {
                    case "crate":
                    case "crate_top":
                    {
                        // pilha alta (h > 16) = duas caixas: uma embaixo do tamanho todo, outra menor e meio girada em cima
                        void Box(int bx0, int bx1, int by0, int by1, int z0, int zt)
                        {
                            int nx = bx1 - bx0, ny = by1 - by0, nzz = zt - z0;
                            for (int ix = 0; ix < nx; ix++)
                                for (int iy = 0; iy < ny; iy++)
                                    for (int iz = 0; iz < nzz; iz++)
                                    {
                                        int ex = ix == 0 || ix == nx - 1 ? 1 : 0, ey = iy == 0 || iy == ny - 1 ? 1 : 0, ez = iz == 0 || iz == nzz - 1 ? 1 : 0;
                                        bool diag = MathF.Abs(ix * nzz / (float)nx - iz) < 0.8f && ey > 0;
                                        if (InMap(bx0 + ix, by0 + iy) && z0 + iz < NZ) G[I(bx0 + ix, by0 + iy, z0 + iz)] = (byte)(ex + ey + ez >= 2 || diag ? 11 : 10);
                                    }
                        }
                        if (c.kind == "crate_top") Box(c.r.x0, c.r.x1, c.r.y0, c.r.y1, f + 16, z1);
                        else if (c.h > 18)
                        {
                            Box(c.r.x0, c.r.x1, c.r.y0, c.r.y1, f, f + 16);
                            int ix0 = c.r.x0 + 2, ix1 = c.r.x1 - 3, iy0 = c.r.y0 + 3, iy1 = c.r.y1 - 2;
                            if (ix1 - ix0 >= 4 && iy1 - iy0 >= 4) Box(ix0, ix1, iy0, iy1, f + 16, Math.Min(z1, f + 30));
                        }
                        else Box(c.r.x0, c.r.x1, c.r.y0, c.r.y1, f, z1);
                        break;
                    }
                    case "lowwall":
                    case "halfwall":   // meio muro (Brasa)
                    case "wall":       // muro inteiro (Brasa, escuro do B)
                        for (int x = c.r.x0; x < c.r.x1; x++)
                            for (int y = c.r.y0; y < c.r.y1; y++)
                            {
                                for (int z = f; z < z1; z++) G[I(x, y, z)] = 6;
                                G[I(x, y, z1 - 1)] = 8;
                            }
                        break;
                    case "cart":
                        for (int x = c.r.x0; x < c.r.x1; x++)
                            for (int y = c.r.y0; y < c.r.y1; y++)
                            {
                                for (int z = f + 2; z < z1; z++) G[I(x, y, z)] = 10;
                                G[I(x, y, z1 - 1)] = 11;
                            }
                        break;
                    case "logs":    // pilha de toras (Brasa, banana): rolos deitados no eixo comprido, 2 camadas
                    {
                        bool ax = c.r.x1 - c.r.x0 >= c.r.y1 - c.r.y0;
                        int L0 = ax ? c.r.x0 : c.r.y0, L1 = ax ? c.r.x1 : c.r.y1, W0 = ax ? c.r.y0 : c.r.x0, W1 = ax ? c.r.y1 : c.r.x1;
                        int camadas = c.h >= 8 ? 2 : 1;
                        float rl = c.h / (2f + 1.7f * (camadas - 1));
                        for (int k = 0; k < camadas; k++)
                        {
                            int n = Math.Max(1, (int)((W1 - W0) / (2 * rl)) - k);
                            float zc = f + rl + k * rl * 1.7f, w0 = (W0 + W1) / 2f - (n - 1) * rl;
                            for (int j = 0; j < n; j++)
                            {
                                float wc = w0 + j * 2 * rl;
                                for (int l = L0; l < L1; l++)
                                    for (int w = (int)(wc - rl - 1); w <= (int)(wc + rl + 1); w++)
                                        for (int z = (int)(zc - rl - 1); z <= (int)(zc + rl + 1); z++)
                                        {
                                            float dw = w + 0.5f - wc, dz = z + 0.5f - zc, d2 = dw * dw + dz * dz;
                                            int x = ax ? l : w, y = ax ? w : l;
                                            if (d2 > rl * rl || !InMap(x, y) || z < 0 || z >= NZ) continue;
                                            bool ponta = l == L0 || l == L1 - 1;
                                            G[I(x, y, z)] = (byte)(ponta ? (d2 < rl * rl * 0.4f ? 43 : 38) : ((l + z) % 7 == 0 ? 11 : 10));
                                        }
                            }
                        }
                        break;
                    }
                    case "jars":    // jarros de barro de alturas variadas
                    case "plants":  // vasos com planta
                    {
                        var jr = new Sim.Rng(c.r.x0 * 31 + c.r.y0);
                        for (int n = 0; n < (c.r.x1 - c.r.x0) * (c.r.y1 - c.r.y0) / 10; n++)
                        {
                            float jx = jr.Uniform(c.r.x0 + 2, c.r.x1 - 2), jy = jr.Uniform(c.r.y0 + 2, c.r.y1 - 2);
                            int jh = (int)jr.Uniform(7, 12);
                            for (int x = (int)jx - 2; x <= (int)jx + 2; x++)
                                for (int y = (int)jy - 2; y <= (int)jy + 2; y++)
                                {
                                    float dd = MathF.Sqrt((x + 0.5f - jx) * (x + 0.5f - jx) + (y + 0.5f - jy) * (y + 0.5f - jy));
                                    if (!InMap(x, y)) continue;
                                    for (int z = f; z < f + jh; z++)
                                    {
                                        float rz = c.kind == "jars" ? (z - f < jh - 1 ? 2.6f : 1.4f) : 2.2f;
                                        if (dd <= rz) G[I(x, y, z)] = (byte)(c.kind == "jars" ? (z == f + jh / 2 ? 13 : 12) : 12);
                                    }
                                    if (c.kind == "plants" && dd <= 2.6f) for (int z = f + jh; z < f + jh + 4; z++) if (dd <= 2.6f - (z - f - jh) * 0.5f) G[I(x, y, z)] = 17;
                                }
                        }
                        break;
                    }
                    case "sacks":   // pilha de sacos de juta
                    case "baskets": // cestos de vime com fruta
                    {
                        var sr = new Sim.Rng(c.r.x0 * 17 + c.r.y0 * 5);
                        int n = Math.Max(2, (c.r.x1 - c.r.x0) * (c.r.y1 - c.r.y0) / 14);
                        for (int i = 0; i < n; i++)
                        {
                            float jx = sr.Uniform(c.r.x0 + 3, c.r.x1 - 3), jy = sr.Uniform(c.r.y0 + 3, c.r.y1 - 3);
                            int zb = f + (c.kind == "sacks" && i >= n * 2 / 3 ? 4 : 0);   // sacos: alguns em cima dos outros
                            for (int x = (int)jx - 4; x <= (int)jx + 4; x++)
                                for (int y = (int)jy - 4; y <= (int)jy + 4; y++)
                                {
                                    if (!InMap(x, y)) continue;
                                    float dx = x + 0.5f - jx, dy = y + 0.5f - jy;
                                    if (c.kind == "sacks")
                                        for (int z = zb; z < zb + 6; z++)
                                        {
                                            float dz = (z + 0.5f - zb - 2.5f) / 2.6f, e = dx * dx / 12f + dy * dy / 9f + dz * dz;
                                            if (e <= 1) G[I(x, y, z)] = (byte)(MathF.Abs(dy) < 0.6f && z == zb + 4 ? 38 : 43);
                                        }
                                    else if (dx * dx + dy * dy <= 9)
                                    {
                                        for (int z = f; z < f + 5; z++) G[I(x, y, z)] = (byte)(dx * dx + dy * dy > 4.4f ? ((z + x + y) % 2 == 0 ? 10 : 11) : 10);
                                        if (dx * dx + dy * dy <= 4.4f) G[I(x, y, f + 5)] = (byte)(i % 3 == 0 ? 41 : i % 3 == 1 ? 30 : 17);
                                    }
                                }
                        }
                        break;
                    }
                    case "stall":   // banca do mercado: mesa de madeira, mercadoria e toldo listrado
                        for (int x = c.r.x0; x < c.r.x1; x++)
                            for (int y = c.r.y0; y < c.r.y1; y++)
                            {
                                for (int z = f; z < f + 8; z++) G[I(x, y, z)] = 10;
                                G[I(x, y, f + 8)] = (byte)((x * 7 + y * 3) % 5 < 2 ? 30 : ((x * 7 + y * 3) % 5 < 4 ? 17 : 41));
                                G[I(x, y, f + 26)] = (byte)(((x + y) / 3) % 2 == 0 ? 5 : 8);
                                bool corner = (x == c.r.x0 || x == c.r.x1 - 1) && (y == c.r.y0 || y == c.r.y1 - 1);
                                if (corner) for (int z = f + 8; z < f + 26; z++) G[I(x, y, z)] = 11;
                            }
                        break;
                    default:   // pillar, barrel, well: cilindros
                    {
                        float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f;
                        int rr = Math.Max(5, (c.r.x1 - c.r.x0) / 2 + 1);
                        for (int x = (int)cx - rr; x <= (int)cx + rr; x++)
                            for (int y = (int)cy - rr; y <= (int)cy + rr; y++)
                            {
                                float dd = MathF.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                                for (int z = f; z < z1; z++)
                                {
                                    if (c.kind == "pillar" && dd <= (f + 2 < z && z < z1 - 2 ? 2.4f : 3.2f)) G[I(x, y, z)] = 15;
                                    if (Tipo(c.kind) == "barrel" && dd <= 2.8f) G[I(x, y, z)] = (byte)(z == f + c.h / 2 ? 13 : 12);
                                    if (c.kind == "well" && dd <= 3.2f && dd > 2) G[I(x, y, z)] = 15;
                                }
                                if (c.kind == "well" && dd <= 2) G[I(x, y, f)] = 19;
                                // fonte da praça: borda de pedra e água
                                float fr = (c.r.x1 - c.r.x0) / 2f;
                                if (c.kind == "fountain" && dd <= fr)
                                {
                                    for (int z = f; z < f + 3; z++) G[I(x, y, z)] = (byte)(dd > fr - 2.2f ? 15 : 9);
                                    if (dd <= 3) for (int z = f + 3; z < z1; z++) G[I(x, y, z)] = 15;
                                }
                            }
                        break;
                    }
                }
            }
            return G;
        }

        // material do piso: ladrilho xadrez com rejunte, terra com pedrinhas, tábuas de madeira, pedra, caverna
        static byte FloorMat(int x, int y, float n)
        {
            switch (fmat[x, y])
            {
                case Floor.Tile:
                {
                    if (n > 1.75f) return 14;
                    if (x % Iso.TL == 0 || y % Iso.TL == 0) return 3;
                    return (byte)(((x / Iso.TL) + (y / Iso.TL)) % 2 == 0 ? 1 : 2);
                }
                case Floor.Wood: return (byte)(y % 4 == 0 ? 47 : 46);
                case Floor.Stone: return (byte)((x / 6 + y / 6) % 2 == 0 ? 15 : 14);
                case Floor.Cave: return (byte)(n > 0.8f ? 49 : 48);
                case Floor.Cart: return 11;
                default: return (byte)(n > 1.1f ? 14 : 18);
            }
        }
    }
}
