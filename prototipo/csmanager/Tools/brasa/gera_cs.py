# Gera Assets/Game/Map/MapDataBrasa.cs a partir de brasa_mapa.json (sessão sim-apoio, 2026-11-07).
# Rodar:  python -I Tools/brasa/gera_cs.py   (da raiz do CSManager)
# Regras de conversão: Tools/brasa/PEDIDO_BOTS.md. Exceção: casa 'o' (cobertura) vira piso andável com a cobertura em
# cima (coverH), igual às coberturas da Vila — a navegação já trata (pula em cima se der, senão contorna).
import json, io, os, sys
R = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
d = json.load(open(os.path.join(R, 'Tools/brasa/brasa_mapa.json'), encoding='utf-8'))
W, H = d['W'], d['H']
assert d['Q'] == 25 and d['GZ'] == 8
def rnd(v): return int(round(float(v))) if v is not None else -999
MAT = {'Stone': 'S', 'Tile': 'T', 'Dirt': 'D', 'Wood': 'W', 'Cave': 'C', None: '.'}
def cstr(s): return '"' + s.replace('\\', '\\\\').replace('"', '\\"') + '"'
L = []
L.append('// GERADO por Tools/brasa/gera_cs.py a partir de Tools/brasa/brasa_mapa.json — não editar à mão.')
L.append('using System;\nusing System.Collections.Generic;\nusing CSM.Sim;\n\nnamespace CSM.Map\n{\n    public static partial class MapData\n    {')
L.append(f'        public const int BRASA_NX = {d["NX"]}, BRASA_NY = {d["NY"]}, BRASA_W = {W}, BRASA_H = {H};')
L.append('        static readonly string[] B_CEL = {\n' + ',\n'.join('            ' + cstr(r) for r in d['celulas']) + '\n        };')
L.append('        static readonly string[] B_RAMPA = {\n' + ',\n'.join('            ' + cstr(r) for r in d['rampa']) + '\n        };')
L.append('        static readonly string[] B_MAT = {\n' + ',\n'.join('            ' + cstr(''.join(MAT[m] for m in r)) for r in d['material']) + '\n        };')
L.append('        static readonly int[][] B_PISO = {\n' + ',\n'.join('            new[] {' + ','.join(str(rnd(v)) for v in r) + '}' for r in d['piso']) + '\n        };')
L.append('        static readonly int[][] B_TOPO = {\n' + ',\n'.join('            new[] {' + ','.join(str(rnd(v)) for v in r) + '}' for r in d['topo']) + '\n        };')
L.append('        static readonly int[][] B_ZONA = {\n' + ',\n'.join('            new[] {' + ','.join(str(int(v)) for v in r) + '}' for r in d['zona']) + '\n        };')
L.append('        public static readonly string[] BRASA_ZONAS = { ' + ', '.join(cstr(z) for z in d['zonas']) + ' };')
L.append('        // coberturas: x0, x1, y0, y1 (casas, inclusive), h acima do piso, piso, tipo, nome')
L.append('        static readonly (int x0, int x1, int y0, int y1, int h, int piso, string tipo, string nome)[] B_COB = {\n' + ',\n'.join(
    f'            ({c["x0"]}, {c["x1"]}, {c["y0"]}, {c["y1"]}, {rnd(c["h"])}, {rnd(c["piso"])}, {cstr(c["tipo"])}, {cstr(c["nome"])})' for c in d['coberturas']) + '\n        };')
L.append('        static readonly (string site, int x0, int x1, int y0, int y1)[] B_SITES = { ' + ', '.join(
    f'({cstr(k)}, {v["x0"]}, {v["x1"]}, {v["y0"]}, {v["y1"]})' for k, v in d['sites'].items()) + ' };')
L.append('        static readonly int[][] B_SPAWN_T = { ' + ', '.join(f'new[] {{{p[0]}, {p[1]}}}' for p in d['spawnT']) + ' };')
L.append('        static readonly int[][] B_SPAWN_CT = { ' + ', '.join(f'new[] {{{p[0]}, {p[1]}}}' for p in d['spawnCT']) + ' };')
L.append('        /// <summary>Nomes de lugar pra placa/legenda (texto, x e y em casas, fracionário).</summary>')
L.append('        public static readonly (string nome, float x, float y)[] BRASA_NOMES = { ' + ', '.join(
    f'({cstr(n[0])}, {float(n[1])}f, {float(n[2])}f)' for n in d['nomes']) + ' };')
L.append(r'''
        /// <summary>
        /// Monta a Brasa (mapa 2) no lugar da Vila: casa (x, y) cobre [x*25, x*25+25) × [y*25, y*25+25); walk = letra ≠ '#';
        /// floor = GZ + piso; stair = rampa '1'; fmat = material (nulo = Stone); prédio '#': solid = GZ + topo. Casa 'o'
        /// (cobertura) = piso da cobertura + coverH. Zonas viram áreas (nome = callout). Sites e spawns em casas.
        /// </summary>
        static void MontaBrasa()
        {
            Floor Mat(char c) => c switch { 'T' => Floor.Tile, 'D' => Floor.Dirt, 'W' => Floor.Wood, 'C' => Floor.Cave, _ => Floor.Stone };
            // piso da casa 'o' = piso da cobertura que a cobre
            var pisoO = new Dictionary<(int, int), int>();
            foreach (var c in B_COB) for (int cx = c.x0; cx <= c.x1; cx++) for (int cy = c.y0; cy <= c.y1; cy++) pisoO[(cx, cy)] = c.piso;
            for (int cy = 0; cy < BRASA_H; cy++)
                for (int cx = 0; cx < BRASA_W; cx++)
                {
                    char l = B_CEL[cy][cx];
                    bool anda = l != '#';
                    int piso = B_PISO[cy][cx];
                    if (l == 'o') piso = pisoO.TryGetValue((cx, cy), out int po) ? po : 0;
                    for (int x = cx * Q; x < cx * Q + Q; x++)
                        for (int y = cy * Q; y < cy * Q + Q; y++)
                        {
                            if (anda) { walk[x, y] = true; floor[x, y] = GZ + Math.Max(0, piso); stair[x, y] = B_RAMPA[cy][cx] == '1'; fmat[x, y] = Mat(B_MAT[cy][cx]); solid[x, y] = 0; }
                            else { walk[x, y] = false; solid[x, y] = GZ + Math.Max(0, B_TOPO[cy][cx]); fmat[x, y] = Floor.Stone; }
                        }
                }
            // zonas -> áreas (uma por zona; a casa diz a zona)
            for (int zi = 0; zi < BRASA_ZONAS.Length; zi++)
            {
                int x0 = int.MaxValue, x1 = -1, y0 = int.MaxValue, y1 = -1;
                for (int cy = 0; cy < BRASA_H; cy++) for (int cx = 0; cx < BRASA_W; cx++) if (B_ZONA[cy][cx] == zi) { x0 = Math.Min(x0, cx); x1 = Math.Max(x1, cx); y0 = Math.Min(y0, cy); y1 = Math.Max(y1, cy); }
                if (x1 < 0) continue;
                int z = zi; string call = BRASA_ZONAS[zi];
                string nome = call switch { "Bomb A" => "A", "Bomb B" => "B", "Base TR" => "TR", "Base CT" => "CT", _ => call };   // nome interno no padrão da Vila (o banco e o IGL procuram "A"/"B"/"TR"/"CT")
                var s = new Shape { name = nome, box = Cells(x0, x1, y0, y1), has = (x, y) => x >= 0 && y >= 0 && x < NX && y < NY && B_ZONA[y / Q][x / Q] == z };
                areas.Add(s); CALLOUT[nome] = call;
            }
            /* caixa/barril alto (14+) de 1 casa é pilha que não se sobe (padrão 2026-11-08: destravou 11,7 -> 5,0/round, -humano 583 -> 547; apBrasaPilhaVelho volta) */
            bool pilha = !Velho("apBrasaPilhaVelho");
            foreach (var c in B_COB) AddCover(Cells(c.x0, c.x1, c.y0, c.y1), c.h, pilha && c.h >= 14 && (c.tipo == "crate" || c.tipo == "barrel") ? c.tipo + "_pilha" : c.tipo);
            foreach (var s in B_SITES) SITES[s.site] = Cells(s.x0, s.x1, s.y0, s.y1);
            V2 Centro(int[] p) => new V2(p[0] * Q + Q / 2f, p[1] * Q + Q / 2f);
            SPAWN_T = Array.ConvertAll(B_SPAWN_T, Centro);
            SPAWN_CT = Array.ConvertAll(B_SPAWN_CT, Centro);
        }
    }
}''')
out = os.path.join(R, 'Assets/Game/Map/MapDataBrasa.cs')
io.open(out, 'w', encoding='utf-8-sig', newline='\r\n').write('\n'.join(L) + '\n')
print('ok', out)
