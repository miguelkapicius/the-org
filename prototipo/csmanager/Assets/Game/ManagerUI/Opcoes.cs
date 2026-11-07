using System;
using UnityEngine;

namespace CSM.ManagerUI
{
    /// <summary>As preferências do jogador (decisão 0030): volumes, tamanho da interface e janela. Ficam em PlayerPrefs, valem para todas as carreiras
    /// e nunca quebram o jogo se o armazenamento falhar (voltam ao padrão).</summary>
    public static class Opcoes
    {
        const string K_EFEITOS = "csm_op_efeitos", K_AMBIENTE = "csm_op_ambiente", K_TAMANHO = "csm_op_tamanho", K_TELA_CHEIA = "csm_op_tela_cheia", K_JANELA = "csm_op_janela", K_HUD = "csm_op_hud_novo";

        /// <summary>Passos de volume mostrados na tela (50 % é o padrão: os sons já nascem leves, decisão 0029).</summary>
        public static readonly float[] PassosVolume = { 0f, 0.25f, 0.5f, 0.75f, 1f };
        public static readonly string[] NomesTamanho = { "Pequena", "Normal", "Grande" };
        public static readonly float[] EscalaTamanho = { 0.9f, 1f, 1.15f };
        /// <summary>Tamanhos de janela oferecidos (16:9); só aparecem os que cabem na tela.</summary>
        public static readonly Vector2Int[] Janelas = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080), new Vector2Int(2560, 1440) };

        static float LerF(string k, float padrao) { try { return PlayerPrefs.GetFloat(k, padrao); } catch (Exception) { return padrao; } }
        static int LerI(string k, int padrao) { try { return PlayerPrefs.GetInt(k, padrao); } catch (Exception) { return padrao; } }
        static void Gravar(string k, float v) { try { PlayerPrefs.SetFloat(k, v); PlayerPrefs.Save(); } catch (Exception) { } }
        static void Gravar(string k, int v) { try { PlayerPrefs.SetInt(k, v); PlayerPrefs.Save(); } catch (Exception) { } }
        static bool Tem(string k) { try { return PlayerPrefs.HasKey(k); } catch (Exception) { return false; } }

        public static float VolumeEfeitos { get => Mathf.Clamp01(LerF(K_EFEITOS, 0.5f)); set => Gravar(K_EFEITOS, Mathf.Clamp01(value)); }
        public static float VolumeAmbiente { get => Mathf.Clamp01(LerF(K_AMBIENTE, 0.5f)); set => Gravar(K_AMBIENTE, Mathf.Clamp01(value)); }
        /// <summary>0 = pequena, 1 = normal, 2 = grande.</summary>
        public static int Tamanho { get => Mathf.Clamp(LerI(K_TAMANHO, 1), 0, 2); set => Gravar(K_TAMANHO, Mathf.Clamp(value, 0, 2)); }
        public static bool TelaCheia { get => LerI(K_TELA_CHEIA, 0) == 1; set => Gravar(K_TELA_CHEIA, value ? 1 : 0); }
        /// <summary>HUD da partida no visual novo (grafite) ou no clássico (decisão 0032). O padrão é o clássico (o autor escolheu o clássico em 2026-11-07); o novo segue disponível nas Opções.</summary>
        public static bool HudNovo { get => LerI(K_HUD, 0) == 1; set => Gravar(K_HUD, value ? 1 : 0); }
        public static int Janela { get => LerI(K_JANELA, -1); set => Gravar(K_JANELA, value); }

        /// <summary>Referência da interface: quanto maior a escala, menor a referência (e mais a interface cresce na tela).</summary>
        public static Vector2Int Referencia() { float e = EscalaTamanho[Tamanho]; return new Vector2Int(Mathf.RoundToInt(1600f / e), Mathf.RoundToInt(900f / e)); }

        public static bool Cabe(Vector2Int j)
        {
            var maior = Vector2Int.zero;
            foreach (var r in Screen.resolutions) if ((long)r.width * r.height > (long)maior.x * maior.y) maior = new Vector2Int(r.width, r.height);
            if (maior == Vector2Int.zero) return true;
            return j.x <= maior.x && j.y <= maior.y;
        }

        /// <summary>Aplica o modo e o tamanho da janela. Não faz nada em modo batch nem enquanto o jogador não mexeu nisso (a Unity guarda a janela sozinha).</summary>
        public static void AplicarTela(bool forcar = false)
        {
            if (Application.isBatchMode) return;
            if (!forcar && !Tem(K_TELA_CHEIA) && !Tem(K_JANELA)) return;
            try
            {
                if (TelaCheia) { var m = Screen.mainWindowDisplayInfo; Screen.SetResolution(m.width, m.height, FullScreenMode.FullScreenWindow); }
                else
                {
                    int i = Janela; var j = i >= 0 && i < Janelas.Length ? Janelas[i] : new Vector2Int(Screen.width, Screen.height);
                    Screen.SetResolution(j.x, j.y, FullScreenMode.Windowed);
                }
            }
            catch (Exception e) { Debug.LogWarning("Opcoes.AplicarTela: " + e.Message); }
        }

        public static void RestaurarPadroes()
        {
            try { PlayerPrefs.DeleteKey(K_EFEITOS); PlayerPrefs.DeleteKey(K_AMBIENTE); PlayerPrefs.DeleteKey(K_TAMANHO); PlayerPrefs.DeleteKey(K_TELA_CHEIA); PlayerPrefs.DeleteKey(K_JANELA); PlayerPrefs.DeleteKey(K_HUD); PlayerPrefs.Save(); } catch (Exception) { }
        }
    }
}
