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
        static readonly Color[] COR = new Color[128];

        static Paleta3D() => UsarVila();

        /// <summary>Paleta da Brasa (2º mapa, clima da Inferno): pedra bege no chão, reboco ocre e creme, telha mais
        /// queimada, madeira escura, verde-oliva. Ainda não é chamada no jogo: entra quando o MapData montar a Brasa.</summary>
        public static void UsarBrasa()
        {
            UsarVila();
            Set(1, 0xDCC7A0); Set(2, 0xCDB58C); Set(3, 0xB39A74); Set(14, 0xC8AE84); Set(18, 0xD6BF95);
            Set(6, 0xEDD3A0); Set(7, 0xC99560); Set(8, 0xF6E4BE);
            Set(50, 0xC4562E); Set(51, 0xA6452A);
            Set(10, 0x9A6236); Set(11, 0x6E4325); Set(46, 0x8C5A33); Set(47, 0x6A4224);
            Set(17, 0x7E9A4A); Set(5, 0xC8402F); Set(16, 0x5F7A4A);
        }

        /// <summary>Paleta da Vila (a de sempre).</summary>
        public static void UsarVila()
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
            Set(52, 0xC2A27A); Set(53, 0xA98A64);
            Set(54, 0xF1ECE2); Set(55, 0xDCD6CB); Set(56, 0xDDCBA8); Set(57, 0xB59C78);
            Set(58, 0xF4DDAC); Set(59, 0xBE9A6C); Set(63, 0xCF8A62);   // lajota de barro dos terraços (mais calma que a telha)
            Adobe();
            Set(60, 0xCFC7B8); Set(61, 0xA9A194); Set(62, 0xC2BAAB);   // 60-62 cimento do janelão (pano, junta/trinca, mancha)   // areia: crista clara das ondinhas de vento e pedrinha (Pisos3D.Areia)
            // 54/55 mármore do palácio (laje e veio/junta), 56/57 lajota da plataforma do A (Pisos3D.Pedras)   // deck e escada de madeira: tábua desbotada de sol, puxada pro tom da areia (a caramelo brigava com a telha)
        }

        /// <summary>Adobe do ksar (Vila refeita como vila marroquina de terra, usuário mandou foto de Aït Benhaddou): 8 tons de
        /// parede (64-71), cada casa com um (Pisos3D.Ksar), e o piso de terra batida do terraço de cada tom (72-79), mais
        /// claro e poeirento. O alfa marca a classe pro Maquete.shader: 0,30 = parede de adobe, 0,15 = terraço.</summary>
        public const int ADOBE = 64, TERRACO = 72, NTONS = 8;
        /// <summary>Chão de jogo da Vila (Pisos3D.Chao): calçamento de lajes claras (alfa 0,55) e areia (alfa 0,70). É o mais
        /// claro da cena, de propósito: onde se joga é claro, o que é prédio é terra escura.</summary>
        public const int LAJE = 80, AREIA = 81;
        /// <summary>Piso do casarão (era tábua caramelo; usuário: "ser de madeira não faz tanto sentido", "contraste ruim" com
        /// as casas escuras): ladrilho de barro claro em diagonal com estrelinha verde-água (alfa 0,05, desenho no shader).</summary>
        public const int LADRILHO = 82;
        /// <summary>Palácio (usuário: "alguma forma que queira dizer que ali é o palácio"): parede de cal rosada lisa (alfa
        /// 0,36: o shader põe a barra de azulejo no pé e o friso no alto) e a capa de telha verde vidrada das ameias.</summary>
        public const int PALACIO = 83, TELHA_VERDE = 84, TELHADO_VERDE = 85;   // 85 = telhado do palácio (classe de terraço; o shader desenha a telha)
        static readonly int[] TONS = { 0xC98B60, 0xB4764E, 0xD6A070, 0xC08257, 0xD2A062, 0xA96C48, 0xDDAE80, 0xC88C74 };
        static void Adobe()
        {
            var po = new Color(0xE2 / 255f, 0xC4 / 255f, 0x9A / 255f);
            for (int t = 0; t < NTONS; t++)
            {
                if (t == 0) { Set(PALACIO, 0xEFD9BE); var pl = COR[PALACIO]; pl.a = 0.36f; COR[PALACIO] = pl; Set(TELHA_VERDE, 0x2E8B6A); Set(TELHADO_VERDE, 0x3C9A78); var tv = COR[TELHADO_VERDE]; tv.a = 0.15f; COR[TELHADO_VERDE] = tv; }
                if (t == 0) { Set(LADRILHO, 0xEDCDAE); var ld = COR[LADRILHO]; ld.a = 0.05f; COR[LADRILHO] = ld; }
                if (t == 0) { Set(LAJE, 0xEFE2C6); var l = COR[LAJE]; l.a = 0.55f; COR[LAJE] = l; Set(AREIA, 0xF1D7A4); var ar = COR[AREIA]; ar.a = 0.70f; COR[AREIA] = ar; }
                Set(ADOBE + t, TONS[t]); var c = COR[ADOBE + t]; c.a = 0.30f; COR[ADOBE + t] = c;
                // terraço bem mais escuro e avermelhado que o chão de jogo (usuário: "difícil saber onde os bots jogam e o que é cenário")
                var f = Color.Lerp(c, po, 0.06f) * 0.88f; f.a = 0.15f; COR[TERRACO + t] = f;
            }
        }

        static void Set(int i, int hex) => COR[i] = new Color(((hex >> 16) & 255) / 255f, ((hex >> 8) & 255) / 255f, (hex & 255) / 255f);

        static Color Realce(Color c, float sat, float luz)
        {
            Color.RGBToHSV(c, out float h, out float s, out float v);
            return Color.HSVToRGB(h, Mathf.Clamp01(s * sat), Mathf.Clamp01(v * luz));
        }

        /// <summary>Cor do material (sRGB).</summary>
        public static Color Srgb(int m) => m > 0 && m < 128 ? COR[m] : Color.magenta;

        /// <summary>Cor do material pronta pro vértice (linear = projeto em espaço linear; ler no main thread).</summary>
        public static Color Vertice(int m, bool linear) { var c = Srgb(m); float a = c.a; if (linear) c = c.linear; c.a = a; return c; }
    }
}
