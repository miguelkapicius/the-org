# Cara italiana da Brasa: o que aplicar depois do exe 11

Os rascunhos foram escritos em 2026-11-09, antes do exe 11, e não foram compilados. Entram no projeto só depois que o
exe 11 estiver gerado e guardado.

## 1. Peças

- Copiar a fonte das peças pro projeto: de
  `D:\unity\import_packs\medieval_village_megakit\Medieval Village MegaKit[Standard]\FBX\` para
  `Assets/Game/Art/Italia/`. Copiar só as peças usadas:
  - Window_Thin_Round1, Window_Wide_Round1;
  - WindowShutters_{Thin,Wide}_Round_{Open,Closed};
  - Door_1_Round, Door_2_Round, DoorFrame_Round_Brick;
  - Prop_MetalFence_Simple, Prop_MetalFence_Ornament;
  - Prop_Vine1, Prop_Vine4, Prop_Vine6;
  - Roof_RoundTiles_6x6, Roof_RoundTiles_4x4;
  - Prop_Chimney.
- Copiar também as texturas `T_*_BaseColor.png`, `T_VineLeaf.png`, `T_WindowGradient.png` e `T_MetalOrnaments_*`, que
  estão na pasta glTF.
- Copiar a licença: `License_Standard.txt` vira `LICENSE_Quaternius_CC0.txt`.
- Da natureza (zip em Downloads), copiar Pine_1, Pine_2, Pine_3, TwistedTree_1 e TwistedTree_3 (.fbx) para
  `Assets/Game/Art/Natureza/`, se faltarem.
- Os arquivos de código:
  - `rascunho/Editor/ItaliaImport.cs` vai pra `Assets/Game/Editor/`.
  - `rascunho/Visual/Italia3D.cs` e `rascunho/Visual/ArredoresItalia.cs` vão pra `Assets/Game/Visual/`.
- `Visual3D.Setup` (Editor) chama `ItaliaImport.Montar()` ao lado do `NaturezaImport.Montar()`.

## 2. Ligações (só na Brasa; a Vila não muda)

- **`Cenario3D.Montar`:**
  - logo depois de `var G = MapVoxels.Build();`: `Italia3D.Reboco(G);`
  - `var enfeites = Italia3D.Ativo ? Italia3D.Montar(G, matMapa, transform) : Fachada3D.Montar(G, matMapa, transform);`
  - `if (Italia3D.Ativo) ArredoresItalia.Montar(matMapa, matCor, transform); else Arredores.Montar(matMapa, matCor, transform);`
  - Na Brasa, a cor de fundo da câmera é verde-oliva enevoado, `new Color(0.70f, 0.76f, 0.62f)`, no lugar da areia.
- **`Paleta3D.UsarBrasa`:**
  - reboco por lote: 6 ocre `0xE9B067`, 52 salmão `0xE79A7E`, 53 creme `0xF1DDB4`, 54 amarelo `0xF2C66E`;
  - rodapé 7 de pedra `0xCFC3AE`;
  - parapeito e beirada 8 de pedra clara `0xE7DCC6`;
  - chão de paralelepípedo 14 `0x9E9A92` e 15 `0x8C8880` (cinza), e o rejunte 3 mais escuro;
  - telha 50 `0xC4562E` e 51 `0xA6452A`, como já estava.
- **`Brasa3D` / `Packs3D`:** os barris do Survival Kit e o resto dos props da Kenney não se repetem na Vila, então
  ficam. Rever depois da primeira captura.

- **`MapVoxels` (View/MapVoxels.cs), telhado de duas águas da Brasa:**
  - O desenho foi testado no Blender. Na prévia o terraço chato com borda clara ficava igual à Vila.
  - Em `Topo(x, y)`, depois do arredondamento pra múltiplo de 12 da Brasa:
    `if (MAPA == "brasa" && CSM.View3D.Italia3D.Ativo) t = Math.Min(NZ, t + CSM.View3D.TelhadoItalia.Extra(x, y));`
  - No laço dos telhados, na Brasa (Italia3D.Ativo):
    - a telha vem de `TelhadoItalia.Telha(x, y)`, no lugar de `edge ? 8 : 50/51`;
    - **não** põe a mureta (8) em cima, porque telhado italiano tem beiral e não parapeito.
  - `Paleta3D.UsarBrasa`: `Set(55, 0xD8683A)` (o capa-canal mais claro).
  - Cuidado: `Topo` também é usado pelo Fachada3D, Adornos3D e Italia3D pra altura da parede (H). Na beirada o
    acréscimo é 0, então a fachada não muda.
  - Sem acréscimo, a Vila fica igual.

## 3. Calibrar na primeira captura (CapturarMapaBatch -mapa brasa)

- `Italia3D.GiroPack`: a frente da janela tem que apontar pra rua. Testar 0 e 180.
- `METRO = 12.5`: a janela tem que caber entre o rodapé e o telhado nas fachadas de 36.
- A altura e a escala do telhado do lote (`0.18`).
- A escala dos ciprestes e oliveiras no ArredoresItalia: cipreste com ~2 a 3 unidades Unity de altura.
