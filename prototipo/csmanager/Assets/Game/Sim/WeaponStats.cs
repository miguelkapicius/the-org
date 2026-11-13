using System;
using System.Collections.Generic;

namespace CSM.Sim
{
    /// <summary>
    /// Números das armas: base do CS2 (tabela da tradeit.gg conferida em jul/2026, já com a regra de 2026 de descartar
    /// o pente) e o nosso ajuste: tudo 10% mais rápido (recarga e intervalo de tiro), porque o jogo é um pouco mais
    /// dinâmico. A recarga é punitiva como no CS: não atira até terminar, e recarregar antes de esvaziar joga fora o
    /// que sobrou no pente. Os tempos das animações (Tools/sprites/agentes.py, WEAP) usam estes mesmos valores.
    /// </summary>
    public static class WeaponStats
    {
        public const float SPEEDUP = 0.9f;   // multiplica recarga e intervalo de tiro

        /// <summary>Nome de exibição da arma (HUD, killfeed, jogada da partida).</summary>
        public static string Nome(string g) => g switch
        {
            "ak" => "AK-47", "m4" => "M4A4", "m4s" => "M4A1-S", "awp" => "AWP", "mp9" => "MP9", "mac10" => "MAC-10",
            "usp" => "USP-S", "glock" => "Glock-18", "deagle" => "Desert Eagle", "faca" => "faca",
            "tec9" => "Tec-9", "fiveseven" => "Five-SeveN", "p250" => "P250", "galil" => "Galil AR", "famas" => "FAMAS", _ => g?.ToUpper() ?? "",
        };

        public class Stat
        {
            public string id, name;
            public float rpmCs2, reloadCs2;   // CS2
            public int mag, reserveMags, price;   // preço = CS2 (vale pegar do chão / guardar)
            // precisão do CS2 (scripts/weapons.vdata; radianos): imprecisão parado/agachado, por tiro (acumula e recupera),
            // andando (cheia na velocidade máxima), spread da arma, tempos de recuperação (do 1º ao "final" entre as balas
            // de transição), recoil (força e variação de ângulo) e multiplicador de cabeça
            public float spread, inStand, inCrouch, inFire, inMove, recStand, recCrouch, recStandFin, recCrouchFin, recoil, recoilVar, hsMult = 4;
            public int transIni = 2, transFim = 5;
            public float inStandAlt, inCrouchAlt;   // AWP na luneta
            // AWP fica com a cadência do CS2 (1,46 s): com o "10% mais rápido" ela dava tiro demais
            public float Interval => 60f / rpmCs2 * (id == "awp" && !TeamBrain.EXPF.Contains("awpRapida") ? 1f : SPEEDUP);   // segundos entre tiros (nosso)
            public float Reload => reloadCs2 * SPEEDUP;        // segundos de recarga (nosso)
            public float Rpm => 60f / Interval;
        }

        public static readonly Dictionary<string, Stat> ALL = new Dictionary<string, Stat>
        {
            ["ak"] = new Stat { id = "ak", name = "AK-47", rpmCs2 = 600f, reloadCs2 = 2.4f, mag = 30, reserveMags = 3, price = 2700,
                spread = 0.0006f, inStand = 0.00641f, inCrouch = 0.00481f, inFire = 0.0078f, inMove = 0.17506f, recStand = 0.368f, recCrouch = 0.305f, recStandFin = 0.506f, recCrouchFin = 0.42f, recoil = 30, recoilVar = 70 },
            ["m4"] = new Stat { id = "m4", name = "M4A4", rpmCs2 = 666.67f, reloadCs2 = 3.1f, mag = 30, reserveMags = 4, price = 2900,   // weapons.vdata (2026): 2900
                spread = 0.0006f, inStand = 0.0049f, inCrouch = 0.0041f, inFire = 0.007f, inMove = 0.13788f, recStand = 0.339f, recCrouch = 0.242f, recStandFin = 0.466f, recCrouchFin = 0.333f, recoil = 23, recoilVar = 70 },
            ["m4s"] = new Stat { id = "m4s", name = "M4A1-S", rpmCs2 = 600f, reloadCs2 = 3.1f, mag = 20, reserveMags = 3, price = 2900,
                spread = 0.0006f, inStand = 0.0049f, inCrouch = 0.0041f, inFire = 0.012f, inMove = 0.09288f, recStand = 0.339f, recCrouch = 0.242f, recStandFin = 0.466f, recCrouchFin = 0.333f, recoil = 25, recoilVar = 65, hsMult = 3.475f },
            ["awp"] = new Stat { id = "awp", name = "AWP", rpmCs2 = 41.24f, reloadCs2 = 3.7f, mag = 5, reserveMags = 2, price = 4750,
                spread = 0.0002f, inStand = 0.0808f, inCrouch = 0.0606f, inStandAlt = 0.002f, inCrouchAlt = 0.0015f, inFire = 0.05385f, inMove = 0.17648f, recStand = 0.345f, recCrouch = 0.247f, recStandFin = 0.345f, recCrouchFin = 0.247f, recoil = 78, recoilVar = 20 },
            ["mp9"] = new Stat { id = "mp9", name = "MP9", rpmCs2 = 857.14f, reloadCs2 = 2.1f, mag = 30, reserveMags = 2, price = 1250,
                spread = 0.0006f, inStand = 0.009f, inCrouch = 0.008f, inFire = 0.0037f, inMove = 0.02904f, recStand = 0.258f, recCrouch = 0.184f, recStandFin = 0.258f, recCrouchFin = 0.184f, recoil = 21, recoilVar = 70 },
            ["mac10"] = new Stat { id = "mac10", name = "MAC-10", rpmCs2 = 800f, reloadCs2 = 2.6f, mag = 30, reserveMags = 3, price = 1050,
                spread = 0.0006f, inStand = 0.0133f, inCrouch = 0.00998f, inFire = 0.00476f, inMove = 0.01399f, recStand = 0.4f, recCrouch = 0.286f, recStandFin = 0.4f, recCrouchFin = 0.286f, recoil = 18, recoilVar = 70 },
            ["usp"] = new Stat { id = "usp", name = "USP-S", rpmCs2 = 352.94f, reloadCs2 = 2.2f, mag = 12, reserveMags = 2, price = 200,
                spread = 0.0015f, inStand = 0.0049f, inCrouch = 0.00368f, inFire = 0.052f, inMove = 0.01387f, recStand = 0.35f, recCrouch = 0.291f, recStandFin = 0.35f, recCrouchFin = 0.291f, recoil = 23, recoilVar = 0, transIni = 3, transFim = 10 },
            ["glock"] = new Stat { id = "glock", name = "Glock-18", rpmCs2 = 400f, reloadCs2 = 2.3f, mag = 20, reserveMags = 3, price = 200,
                spread = 0.002f, inStand = 0.0056f, inCrouch = 0.0042f, inFire = 0.056f, inMove = 0.01f, recStand = 0.2f, recCrouch = 0.2f, recStandFin = 0.33f, recCrouchFin = 0.33f, recoil = 18, recoilVar = 20, transIni = 0 },
            // pistolas de eco (weapons.vdata do CS2): a Deagle mata com um na cabeça mesmo de capacete (o eco que dá certo)
            ["deagle"] = new Stat { id = "deagle", name = "Desert Eagle", rpmCs2 = 266.67f, reloadCs2 = 2.2f, mag = 7, reserveMags = 3, price = 700,
                spread = 0.002f, inStand = 0.0042f, inCrouch = 0.00218f, inFire = 0.07223f, inMove = 0.0481f, recStand = 0.8112f, recCrouch = 0.45f, recStandFin = 0.8112f, recCrouchFin = 0.45f, recoil = 48.2f, recoilVar = 18, hsMult = 3.9f, transIni = 0, transFim = 3 },
            ["p250"] = new Stat { id = "p250", name = "P250", rpmCs2 = 400f, reloadCs2 = 2.2f, mag = 13, reserveMags = 3, price = 300,
                spread = 0.002f, inStand = 0.0091f, inCrouch = 0.00683f, inFire = 0.05245f, inMove = 0.02f, recStand = 0.345f, recCrouch = 0.288f, recStandFin = 0.345f, recCrouchFin = 0.288f, recoil = 26, recoilVar = 3, transIni = 0 },
            // armas novas (pedido do Gabriel, nov/2026). Preço, dano, penetração, cadência, pente, recarga e velocidade: csdb.gg;
            // spread, imprecisão (parado/agachado/tiro/andando) e recuperação (em pé/agachado/final): weapons.vdata do CS2, 1º
            // valor de cada array (sessão dos bots, 04/11). Só recoil e recoilVar seguem do csdb.gg. FAMAS: $1.950 (Premier S2,
            // jan/2025, com melhora de precisão); a rajada não existe aqui.
            ["tec9"] = new Stat { id = "tec9", name = "Tec-9", rpmCs2 = 500f, reloadCs2 = 2.42f, mag = 18, reserveMags = 3, price = 500,
                spread = 0.002f, inStand = 0.0049f, inCrouch = 0.00368f, inFire = 0.045f, inMove = 0.00381f, recStand = 0.391f, recCrouch = 0.315f, recStandFin = 0.391f, recCrouchFin = 0.315f, recoil = 23, recoilVar = 60, transIni = 0 },   // inMove 0,00381: é o do weapons.vdata (a Tec-9 é a pistola de andar atirando)
            ["fiveseven"] = new Stat { id = "fiveseven", name = "Five-SeveN", rpmCs2 = 400f, reloadCs2 = 2.2f, mag = 20, reserveMags = 2, price = 500,
                spread = 0.002f, inStand = 0.0091f, inCrouch = 0.00683f, inFire = 0.025f, inMove = 0.04f, recStand = 0.2f, recCrouch = 0.2f, recStandFin = 0.5f, recCrouchFin = 0.5f, recoil = 25, recoilVar = 5, transIni = 0 },
            ["galil"] = new Stat { id = "galil", name = "Galil AR", rpmCs2 = 666.67f, reloadCs2 = 3.0f, mag = 35, reserveMags = 4, price = 1800,
                spread = 0.0006f, inStand = 0.00877f, inCrouch = 0.00658f, inFire = 0.007f, inMove = 0.12356f, recStand = 0.3f, recCrouch = 0.15f, recStandFin = 0.5f, recCrouchFin = 0.47f, recoil = 21, recoilVar = 70 },
            ["famas"] = new Stat { id = "famas", name = "FAMAS", rpmCs2 = 666.67f, reloadCs2 = 3.3f, mag = 25, reserveMags = 4, price = 1950,
                spread = 0.0006f, inStand = 0.00759f, inCrouch = 0.0055f, inFire = 0.00605f, inMove = 0.09934f, recStand = 0.25f, recCrouch = 0.12f, recStandFin = 0.5f, recCrouchFin = 0.48f, recoil = 20, recoilVar = 60 },
        };

        /// <summary>
        /// Força no meta (0-10): o que importa pra escolher arma do chão ou o que guardar — AK acima de M4, M4A1-S um
        /// pouco acima da M4A4, SMG bem abaixo de rifle, pistola por último (USP um pouco acima da Glock). A AWP vale
        /// pela função (Brain.ValorArma): muito pro AWPer, pouco pra quem já tem AWP no time.
        /// </summary>
        public static float Forca(string g) => g switch
        {
            "ak" => 10f, "m4s" => 9.3f, "m4" => 9f, "awp" => 9.5f, "famas" => 7.8f, "galil" => 7.6f, "mp9" => 6f, "mac10" => 5.5f, "deagle" => 4.5f, "tec9" => 3.8f, "fiveseven" => 3.7f, "p250" => 3.6f, "usp" => 3.3f, "glock" => 3f, _ => 0f,
        };
        public static bool Pistola(string g) => g == "usp" || g == "glock" || g == "deagle" || g == "p250" || g == "tec9" || g == "fiveseven";

        /// <summary>Estado da arma na mão: munição, recarga (bloqueia o tiro até acabar) e cadência.</summary>
        public class Gun
        {
            public readonly Stat s; public int inMag, mags; public float reloadLeft, nextShot;
            /// <summary>Penalidade de tiro acumulada (radianos): cada bala soma inFire e ela cai sozinha (CS2: 10% no tempo de recuperação).</summary>
            public float penalidade;
            public void Recupera(float dt, bool agachado, int balaDoSpray)
            {
                if (penalidade <= 0) return;
                float u = MathX.Clamp((balaDoSpray - s.transIni) / (float)Math.Max(1, s.transFim - s.transIni), 0, 1);
                float rec = agachado ? s.recCrouch + (s.recCrouchFin - s.recCrouch) * u : s.recStand + (s.recStandFin - s.recStand) * u;
                penalidade *= MathF.Exp(-dt * 2.302585f / MathF.Max(0.05f, rec));
            }
            public Gun(Stat s) { this.s = s; inMag = s.mag; mags = s.reserveMags; }
            public bool Reloading => reloadLeft > 0;
            public bool CanFire(float now) => !Reloading && inMag > 0 && now >= nextShot;
            public void Fire(float now) { inMag--; nextShot = now + s.Interval; }
            /// <summary>Começa a recarga: o que sobrou no pente vai fora (regra do CS2 de 2026).</summary>
            public bool StartReload() { if (Reloading || mags <= 0 || inMag == s.mag) return false; inMag = 0; reloadLeft = s.Reload; return true; }
            public void Tick(float dt)
            {
                if (reloadLeft <= 0) return;
                reloadLeft -= dt;
                if (reloadLeft <= 0) { reloadLeft = 0; mags--; inMag = s.mag; }
            }
        }
    }
}
