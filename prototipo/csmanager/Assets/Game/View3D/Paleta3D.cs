using UnityEngine;
using CSM.View;

namespace CSM.View3D
{
    /// <summary>
    /// Cor de cada material do MapVoxels no visual de maquete: parte do tom claro aprovado da paleta 2D (Iso.RAMP, banda
    /// 2) com mais saturação e luz (brinquedo pintado), e alguns materiais repintados à mão. Devolve cor LINEAR.
    /// </summary>
    public static class Paleta3D
    {
        static readonly Color[] COR = new Color[64];

        static Paleta3D()
        {
            for (int i = 1; i < 64; i++)
            {
                var c = Iso.RAMP[i, 2];
                COR[i] = Realce(new Color(c.r / 255f, c.g / 255f, c.b / 255f), 1.3f, 1.08f);
            }
            // repintura: chão de ladrilho creme quente, rejunte suave, parede de reboco clara, telha laranja viva,
            // madeira caramelo, planta verde de desenho, pano vermelho/azul saturados
            Set(1, 0xE9C98F); Set(2, 0xDDB97E); Set(3, 0xC49A68); Set(14, 0xD8B27A); Set(18, 0xE4C690);
            Set(4, 0xA07450); Set(6, 0xF0D9AE); Set(7, 0xC99B6C); Set(8, 0xFBEBC8);
            Set(50, 0xE0703F); Set(51, 0xC85A34);
            Set(10, 0xC98B4E); Set(11, 0x8F5A32); Set(46, 0xB97E48); Set(47, 0x925C34);
            Set(17, 0x5DB548); Set(5, 0xE2463A); Set(9, 0x3E8FE0); Set(41, 0xEF5A4A); Set(30, 0xF59A2E);
            Set(12, 0xD06A3E); Set(13, 0xA94C2C); Set(15, 0xC9C4B8); Set(16, 0x3FA6A0); Set(19, 0x4A3328);
            Set(48, 0x8A8078); Set(49, 0x726A64); Set(43, 0xCFA875); Set(38, 0xA9855A);
        }

        static void Set(int i, int hex) => COR[i] = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        static Color Realce(Color c, float sat, float luz)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * sat), Mathf.Clamp01(v * luz));
        }

        /// <summary>Cor do material (sRGB).</summary>
        public static Color Srgb(int m) => m > 0 && m < 64 ? COR[m] : Color.magenta;

        /// <summary>Cor do material pronta pro vértice (linear = projeto em espaço linear; ler no main thread).</summary>
        public static Color Vertice(int m, bool linear) => linear ? Srgb(m).linear : Srgb(m);
    }
}
