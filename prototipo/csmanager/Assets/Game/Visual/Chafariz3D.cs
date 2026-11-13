using System.Collections.Generic;
using UnityEngine;
using CSM.Map;
using CSM.Sim;

namespace CSM.View3D
{
    /// <summary>
    /// Chafariz da base TR (cobertura "fountain" do MapData): o de voxel (bacia de pedra + água + pilar) saía do borrão
    /// "sem resolução"; sai das voxels antes da malha e entra o fountain-round-detail do Kenney Fantasy Town Kit (CC0,
    /// Resources/Kenney), pintado com a Paleta3D (pedra 15, água 9) pela cor de vértice que o kenney_cor.py gravou
    /// (branco = pedra, azul = água). Mesma pegada no chão e altura do pilar que a cobertura da Sim (a colisão não muda).
    /// </summary>
    public static class Chafariz3D
    {
        const byte PEDRA = 15, AGUA = 9;

        static IEnumerable<MapData.Cover> Fontes()
        {
            foreach (var c in MapData.covers) if (c.kind == "fountain" && !Brasa3D.Cuida(c.kind)) yield return c;   // na Brasa a fonte é a quadrada do kit (Brasa3D)
        }

        static float Chao(MapData.Cover c)
        {
            // fora da bacia (no meio dela o pilar não é piso)
            float cy = (c.r.y0 + c.r.y1) / 2f;
            return NavGrid.ZAt(new V2(c.r.x0 - 3, cy));
        }

        /// <summary>Tira as voxels do chafariz (pedra e água dentro do círculo, do piso pra cima).</summary>
        public static byte[] SemChafariz(byte[] G)
        {
            var g = G; int NY = MapData.NY, NZ = CSM.View.MapVoxels.NZ; bool copiou = false;
            foreach (var c in Fontes())
            {
                if (!copiou) { g = (byte[])G.Clone(); copiou = true; }
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f, fr = (c.r.x1 - c.r.x0) / 2f + 0.6f;
                int f = Mathf.RoundToInt(Chao(c));
                for (int x = (int)(cx - fr) - 1; x <= (int)(cx + fr) + 1; x++)
                    for (int y = (int)(cy - fr) - 1; y <= (int)(cy + fr) + 1; y++)
                    {
                        if (x < 0 || y < 0 || x >= MapData.NX || y >= NY) continue;
                        float dd = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                        if (dd > fr) continue;
                        for (int z = Mathf.Max(0, f); z < NZ && z < f + c.h + 2; z++)
                        {
                            int i = (x * NY + y) * NZ + z;
                            if (g[i] == PEDRA || g[i] == AGUA) g[i] = 0;
                        }
                    }
            }
            return g;
        }

        public static GameObject Montar(Material mat, Transform pai, bool linear)
        {
            // Vila marroquina: a fonte redonda de pedra do Kenney destoava (usuário: "não tem mais coesão visual"). Agora é
            // uma fonte de pátio de azulejo (zellige): degrau largo de 16 lados, bacia oitavada branca com barra verde-água e
            // friso azul, água, e no meio o pedestal com a taça. Mesmo diâmetro e altura da cobertura da Sim.
            var o = new Obra3D(); float E = MalhaMapa.ESC; int n = 0;
            Color cal = new Color(0xF2 / 255f, 0xEA / 255f, 0xD8 / 255f), verde = new Color(0x3F / 255f, 0xA6 / 255f, 0xA0 / 255f), azul = new Color(0x2F / 255f, 0x5E / 255f, 0x8E / 255f);
            Color agua = new Color(0x4F / 255f, 0xA8 / 255f, 0xD8 / 255f), barro = Paleta3D.Srgb(Paleta3D.ADOBE + 2);
            foreach (var c in Fontes())
            {
                float cx = (c.r.x0 + c.r.x1) / 2f, cy = (c.r.y0 + c.r.y1) / 2f, R = (c.r.x1 - c.r.x0) / 2f, z0 = Chao(c), h = Mathf.Max(8f, c.h);
                Vector3 U(float dz) => MalhaMapa.ParaUnity(cx, cy, z0 + dz);
                Vector3 Cima(float dz) => Vector3.up * dz * E;
                o.Cilindro(U(0), Cima(0.9f), R * 1.06f * E, R * 1.06f * E, 16, barro);                       // degrau
                o.Cilindro(U(0.9f), Cima(1.4f), R * 0.96f * E, R * 0.96f * E, 8, cal);                       // bacia: cal
                o.Cilindro(U(2.3f), Cima(0.9f), R * 0.97f * E, R * 0.97f * E, 8, verde);                     // barra verde-água
                o.Cilindro(U(3.2f), Cima(0.5f), R * 0.96f * E, R * 0.96f * E, 8, azul, cal);                 // friso azul, borda de cal
                o.Cilindro(U(3.2f), Cima(0.52f), R * 0.80f * E, R * 0.80f * E, 8, agua, agua);               // água
                o.Cilindro(U(3.2f), Cima(h * 0.55f), R * 0.16f * E, R * 0.12f * E, 8, cal);                  // pedestal
                o.Cilindro(U(3.2f + h * 0.55f), Cima(0.5f), R * 0.15f * E, R * 0.15f * E, 8, verde);
                o.Cilindro(U(3.7f + h * 0.55f), Cima(1.6f), R * 0.13f * E, R * 0.40f * E, 12, cal, agua);    // taça com água
                o.Cilindro(U(5.3f + h * 0.55f), Cima(Mathf.Max(0.6f, h - 5.3f - h * 0.55f)), R * 0.06f * E, R * 0.03f * E, 6, azul);   // bico
                n++;
                Debug.Log($"[Chafariz3D] fonte de azulejo em ({cx:0},{cy:0}) raio {R:0.0} do jogo");
            }
            if (n == 0) return null;
            mat = new Material(mat) { name = "Maquete chafariz" }; mat.SetFloat("_UsarTopo", 0); mat.SetFloat("_UsarVertice", 1);
            return o.Montar("Chafariz (zellige)", mat, pai);
        }
    }
}
