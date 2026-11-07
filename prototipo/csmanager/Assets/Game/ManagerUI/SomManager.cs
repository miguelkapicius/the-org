using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace CSM.ManagerUI
{
    /// <summary>Toca os sons da interface do manager (decisão 0029), que é em UI Toolkit: o SomUI de CSM.Audio só cobre botões uGUI e IMGUI. Os clipes são sintetizados na primeira vez que são usados (ver SomSintese) e tocados
    /// por um pequeno conjunto de fontes, cada toque com uma variação mínima de altura para o som não cansar. O som pode ser desligado
    /// pelo botão do menu; a escolha fica guardada. Em modo batch (capturas e testes) não toca nada.</summary>
    public static class SomManager
    {
        const string CHAVE_MUDO = "csm_som_mudo";
        const int FONTES = 8;

        static AudioSource[] fontes; static int proxima;
        static readonly Dictionary<Som, AudioClip> clips = new Dictionary<Som, AudioClip>();
        static float ultimoHover; static bool avisou;

        /// <summary>Volume geral dos sons da interface (0 a 1).</summary>
        public static float Volume = 0.6f;

        public static bool Mudo
        {
            get { try { return PlayerPrefs.GetInt(CHAVE_MUDO, 0) == 1; } catch (System.Exception) { return false; } }
            set { try { PlayerPrefs.SetInt(CHAVE_MUDO, value ? 1 : 0); PlayerPrefs.Save(); } catch (System.Exception) { } }
        }

        static float ultimaChecagem = -10f;

        /// <summary>Sem AudioListener na cena a Unity não toca nada (a interface do manager não tem câmera). O ouvido do jogo é o do Som3D: se não
        /// há nenhum, ele cria. Confere de vez em quando, porque a troca de cena pode levar o ouvido embora.</summary>
        static void GarantirOuvido()
        {
            if (Time.unscaledTime - ultimaChecagem < 1f) return;
            ultimaChecagem = Time.unscaledTime;
            if (!Object.FindAnyObjectByType<AudioListener>()) CSM.Audio.Som3D.GarantirOuvido();
        }

        static void Iniciar()
        {
            GarantirOuvido();
            if (fontes != null) return;
            var go = new GameObject("SomManager"); Object.DontDestroyOnLoad(go);
            fontes = new AudioSource[FONTES];
            for (int i = 0; i < FONTES; i++) { var f = go.AddComponent<AudioSource>(); f.playOnAwake = false; f.spatialBlend = 0f; fontes[i] = f; }
        }

        static AudioClip Clip(Som s)
        {
            if (clips.TryGetValue(s, out var c) && c != null) return c;
            var dados = SomSintese.Renderizar(s);
            c = AudioClip.Create("ui_" + s, dados.Length, 1, SomSintese.TAXA, false); c.SetData(dados, 0);
            return clips[s] = c;
        }

        /// <param name="vol">Volume deste som em relação ao geral.</param>
        /// <param name="variacao">Quanto a altura pode variar a cada toque (0,04 = 4 %).</param>
        public static void Tocar(Som s, float vol = 1f, float variacao = 0.04f)
        {
            if (Application.isBatchMode || Mudo) return;
            if (s == Som.Hover) { float agora = Time.unscaledTime; if (agora - ultimoHover < 0.05f) return; ultimoHover = agora; }   // não metralha ao passar o mouse rápido
            Iniciar();
            var f = fontes[proxima++ % FONTES];
            f.pitch = 1f + Random.Range(-variacao, variacao); f.volume = Mathf.Clamp01(Volume * vol * CSM.Audio.Som3D.volumeGeral); f.clip = Clip(s); f.Play();
            if (!avisou) { avisou = true; Debug.Log("SomManager: primeiro som (" + s + "), ouvinte " + (Object.FindAnyObjectByType<AudioListener>() ? "ok" : "AUSENTE") + ", volume " + f.volume.ToString("0.00") + ", áudio " + (AudioListener.pause ? "pausado" : "ativo")); }
        }

        /// <summary>O som do clique de um elemento, pela função dele: ação principal, troca de tela, opção ou clique comum.</summary>
        public static Som DoClique(VisualElement e)
        {
            if (e.ClassListContains("destaque")) return Som.Principal;
            if (e.ClassListContains("item") || e.ClassListContains("aba") || e.ClassListContains("pb-seg-b") || e.ClassListContains("camp-pag")) return Som.Aba;
            if (e.ClassListContains("btn-mini") || e.ClassListContains("btn-ic")) return Som.Alternar;
            return Som.Clique;
        }
    }
}
