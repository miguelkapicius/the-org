using System;
using System.Linq;

namespace CSM.ManagerUI
{
    // ============================================================================================================
    // Sons da interface (decisão 0029): sintetizados em código, sem arquivos de terceiros. Cada som é uma receita curta de "vozes" (uma nota
    // ou um sopro com início, duração, altura inicial e final, ataque e queda). Sem dependência do Unity: os testes geram os mesmos números
    // e gravam arquivos .wav em Logs/sons para ouvir. Para mudar o caráter de um som, mexa só na receita dele em SomCatalogo.
    // ============================================================================================================

    public enum Som { Hover, Clique, Principal, Aba, Alternar, Sucesso, Erro, Dinheiro, Aviso, Receber, Semana }

    public enum Onda { Seno, Triangulo, Quadrada, Ruido }

    /// <summary>Uma voz do som: <c>f0</c> a <c>f1</c> é o deslize de altura em Hz (no ruído, <c>corte</c> é o filtro passa-baixa em Hz).</summary>
    public struct Voz
    {
        public Onda onda; public float inicio, dur, f0, f1, ataque, queda, vol, corte;
        public Voz(Onda onda, float inicio, float dur, float f0, float f1, float ataque, float queda, float vol, float corte = 0f)
        { this.onda = onda; this.inicio = inicio; this.dur = dur; this.f0 = f0; this.f1 = f1; this.ataque = ataque; this.queda = queda; this.vol = vol; this.corte = corte; }
    }

    public static class SomCatalogo
    {
        // notas (Hz) em volta de C5, para os acordes e arpejos
        const float C5 = 523.25f, D5 = 587.33f, E5 = 659.25f, G5 = 783.99f, A5 = 880f, B5 = 987.77f;

        /// <summary>A receita de cada som. "queda" é a fração da duração em que o volume cai para 37 % (menor = mais seco).</summary>
        public static Voz[] Receita(Som s)
        {
            switch (s)
            {
                // passar o mouse: um tique minúsculo e agudo, quase só textura
                case Som.Hover: return new[] { new Voz(Onda.Seno, 0f, 0.035f, 2100f, 1900f, 0.002f, 0.25f, 0.32f) };
                // clique comum: estalo curto de ruído e um "toc" que desce
                case Som.Clique: return new[] { new Voz(Onda.Ruido, 0f, 0.012f, 0f, 0f, 0.001f, 0.4f, 0.22f, 6000f), new Voz(Onda.Seno, 0f, 0.075f, 760f, 520f, 0.002f, 0.35f, 0.42f) };
                // ação principal: duas notas subindo (E5 e B5), com um brilho uma oitava acima
                case Som.Principal: return new[] { new Voz(Onda.Seno, 0f, 0.13f, E5, E5, 0.004f, 0.4f, 0.34f), new Voz(Onda.Seno, 0.06f, 0.17f, B5, B5, 0.004f, 0.45f, 0.34f), new Voz(Onda.Seno, 0.06f, 0.13f, B5 * 2f, B5 * 2f, 0.004f, 0.3f, 0.07f) };
                // trocar de tela ou aba: um sopro que sobe e um tique
                case Som.Aba: return new[] { new Voz(Onda.Ruido, 0f, 0.09f, 0f, 0f, 0.025f, 0.5f, 0.2f, 2600f), new Voz(Onda.Seno, 0.05f, 0.04f, 1400f, 1400f, 0.002f, 0.3f, 0.2f) };
                // ligar e desligar uma opção: um "pop" curto de triângulo
                case Som.Alternar: return new[] { new Voz(Onda.Triangulo, 0f, 0.055f, 520f, 640f, 0.002f, 0.4f, 0.4f) };
                // deu certo: arpejo maior C5 E5 G5
                case Som.Sucesso: return new[] { new Voz(Onda.Seno, 0f, 0.14f, C5, C5, 0.004f, 0.45f, 0.3f), new Voz(Onda.Seno, 0.09f, 0.14f, E5, E5, 0.004f, 0.45f, 0.3f), new Voz(Onda.Seno, 0.18f, 0.26f, G5, G5, 0.004f, 0.5f, 0.32f), new Voz(Onda.Seno, 0.18f, 0.2f, G5 * 2f, G5 * 2f, 0.004f, 0.4f, 0.07f) };
                // não pode: duas notas graves descendo, arredondadas (triângulo)
                case Som.Erro: return new[] { new Voz(Onda.Triangulo, 0f, 0.12f, 220f, 185f, 0.004f, 0.5f, 0.45f), new Voz(Onda.Triangulo, 0.11f, 0.17f, 175f, 140f, 0.004f, 0.5f, 0.45f) };
                // dinheiro: dois tinidos agudos e longos, com um estalo na frente
                case Som.Dinheiro: return new[] { new Voz(Onda.Ruido, 0f, 0.01f, 0f, 0f, 0.001f, 0.4f, 0.15f, 7000f), new Voz(Onda.Seno, 0f, 0.22f, 1568f, 1568f, 0.002f, 0.5f, 0.28f), new Voz(Onda.Seno, 0.07f, 0.3f, 2349f, 2349f, 0.002f, 0.6f, 0.26f) };
                // aviso na tela: duas notas suaves
                case Som.Aviso: return new[] { new Voz(Onda.Seno, 0f, 0.14f, A5, A5, 0.004f, 0.45f, 0.3f), new Voz(Onda.Seno, 0.05f, 0.12f, 1318.5f, 1318.5f, 0.004f, 0.45f, 0.2f) };
                // mensagem recebida: duas notinhas, como uma bolha
                case Som.Receber: return new[] { new Voz(Onda.Seno, 0f, 0.08f, 740f, 740f, 0.003f, 0.45f, 0.3f), new Voz(Onda.Seno, 0.06f, 0.1f, 988f, 988f, 0.003f, 0.45f, 0.3f) };
                // avançar a semana: um sopro grave e uma nota baixa que cai (o tempo passando)
                default: return new[] { new Voz(Onda.Ruido, 0f, 0.22f, 0f, 0f, 0.06f, 0.5f, 0.25f, 1800f), new Voz(Onda.Seno, 0f, 0.2f, 330f, 300f, 0.01f, 0.5f, 0.2f) };
            }
        }
    }

    public static class SomSintese
    {
        public const int TAXA = 44100;

        /// <summary>Mistura as vozes numa só forma de onda (mono, de -1 a 1). Determinística: o ruído usa sempre a mesma semente.</summary>
        public static float[] Renderizar(Voz[] vozes, float ganho = 0.9f)
        {
            float total = vozes.Max(v => v.inicio + v.dur) + 0.02f;
            var buf = new float[(int)(total * TAXA)];
            uint rng = 2463534242u;
            foreach (var v in vozes)
            {
                int i0 = (int)(v.inicio * TAXA), n = (int)(v.dur * TAXA); double fase = 0; float lp = 0f;
                float a = v.corte > 0f ? 1f - (float)Math.Exp(-2.0 * Math.PI * v.corte / TAXA) : 1f;
                for (int k = 0; k < n && i0 + k < buf.Length; k++)
                {
                    float t = k / (float)TAXA, u = t / v.dur;
                    double f = v.f0 > 0f ? v.f0 * Math.Pow(v.f1 / v.f0, u) : 0.0;
                    fase += 2.0 * Math.PI * f / TAXA;
                    float s;
                    switch (v.onda)
                    {
                        case Onda.Triangulo: s = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(fase))); break;
                        case Onda.Quadrada: s = (float)Math.Tanh(4.0 * Math.Sin(fase)); break;
                        case Onda.Ruido:
                            rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                            s = rng / (float)uint.MaxValue * 2f - 1f;
                            lp += a * (s - lp); s = v.corte > 0f ? lp * 2f : s;
                            break;
                        default: s = (float)Math.Sin(fase); break;
                    }
                    float env = (t < v.ataque ? t / v.ataque : 1f) * (float)Math.Exp(-t / (v.dur * v.queda));
                    float fim = Math.Min(1f, (v.dur - t) / 0.004f);   // some em 4 ms no fim, para não estalar
                    buf[i0 + k] += s * env * fim * v.vol;
                }
            }
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * ganho);   // limitador suave: nunca passa de 1
            return buf;
        }

        public static float[] Renderizar(Som s) => Renderizar(SomCatalogo.Receita(s));

        /// <summary>Grava um arquivo WAV de 16 bits, mono (para ouvir fora do jogo).</summary>
        public static void GravarWav(string caminho, float[] amostras)
        {
            using (var fs = new System.IO.FileStream(caminho, System.IO.FileMode.Create))
            using (var w = new System.IO.BinaryWriter(fs))
            {
                int bytes = amostras.Length * 2;
                w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes); w.Write(new[] { 'W', 'A', 'V', 'E', 'f', 'm', 't', ' ' });
                w.Write(16); w.Write((short)1); w.Write((short)1); w.Write(TAXA); w.Write(TAXA * 2); w.Write((short)2); w.Write((short)16);
                w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
                foreach (var x in amostras) w.Write((short)Math.Round(Math.Max(-1f, Math.Min(1f, x)) * 32000f));
            }
        }
    }
}
