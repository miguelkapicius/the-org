using System;
using System.Linq;

namespace CSM.ManagerUI
{
    // ============================================================================================================
    // Sons da interface (decisão 0029): sintetizados em código, sem arquivos de terceiros. Identidade "sala de operações de uma transmissão
    // tática": estalos de relé e de tecla mecânica, rádio tático, ping de radar, carimbo e moedas, um sopro de painel deslizando, e um zumbido de
    // sala de controle ao fundo. Cada som é uma receita de "vozes" (ruído filtrado, notas, timbres FM) com eco e reverb de sala. Sem
    // dependência do Unity: os testes geram os mesmos números e gravam .wav em Logs/sons para ouvir. Para mudar o caráter de um som, mexa só na
    // receita dele em SomCatalogo.
    // ============================================================================================================

    public enum Som { Hover, Clique, Principal, Aba, Alternar, Sucesso, Erro, Dinheiro, Aviso, Receber, Semana }

    public enum Onda { Seno, Triangulo, Quadrada, Ruido, FM }
    public enum Filtro { Nenhum, PassaBaixa, PassaAlta, PassaFaixa }

    /// <summary>Uma voz do som. <c>f0</c> a <c>f1</c> é o deslize de altura em Hz (f1 menor que 0 = fica em f0); no ruído as alturas não contam, só o filtro.
    /// "queda" é a fração da duração em que o volume cai a 37 % (menor = mais seco). No FM, <c>razao</c> é modulador/portadora e <c>indice</c> a força do brilho metálico.</summary>
    public class Voz
    {
        public Onda onda = Onda.Seno;
        public float inicio, dur = 0.1f, f0 = 440f, f1 = -1f, ataque = 0.002f, queda = 0.4f, vol = 0.3f;
        public Filtro filtro = Filtro.Nenhum; public float corte, corteFim = -1f, q = 0.9f;
        public float razao = 2f, indice = 0f;
    }

    /// <summary>O som inteiro: as vozes, o volume final (pico), o eco e a reverb de sala (0 a 1) e quanto tempo a cauda dura depois da última voz.</summary>
    public class Definicao
    {
        public Voz[] vozes; public float pico = 0.5f, reverb, eco, ecoTempo = 0.18f, ecoRealim = 0.4f, cauda = 0.3f;
    }

    public static class SomCatalogo
    {
        const float C4 = 261.63f, G4 = 392f, C5 = 523.25f, G5 = 783.99f, C6 = 1046.5f;

        static Voz Ruido(float ini, float dur, Filtro f, float corte, float q, float vol, float atk = 0.0005f, float queda = 0.35f, float corteFim = -1f)
            => new Voz { onda = Onda.Ruido, inicio = ini, dur = dur, filtro = f, corte = corte, corteFim = corteFim, q = q, vol = vol, ataque = atk, queda = queda };
        static Voz Nota(Onda o, float ini, float dur, float f0, float f1, float vol, float queda = 0.4f, float atk = 0.002f, Filtro f = Filtro.Nenhum, float corte = 0f, float q = 0.9f)
            => new Voz { onda = o, inicio = ini, dur = dur, f0 = f0, f1 = f1, vol = vol, queda = queda, ataque = atk, filtro = f, corte = corte, q = q };
        static Voz Sino(float ini, float dur, float f, float indice, float vol, float razao = 3.5f, float queda = 0.5f)
            => new Voz { onda = Onda.FM, inicio = ini, dur = dur, f0 = f, f1 = f, razao = razao, indice = indice, vol = vol, queda = queda, ataque = 0.002f };

        public static Definicao Receita(Som s)
        {
            switch (s)
            {
                // passar o mouse: tique de relé, quase só textura
                case Som.Hover: return new Definicao { pico = 0.2f, vozes = new[] {
                    Ruido(0f, 0.012f, Filtro.PassaFaixa, 4200f, 5f, 0.9f),
                    Nota(Onda.Seno, 0f, 0.02f, 2600f, 2200f, 0.12f, 0.3f) } };

                // clique: tecla mecânica (estalo agudo, plástico e o corpo grave batendo no fundo)
                case Som.Clique: return new Definicao { pico = 0.4f, vozes = new[] {
                    Ruido(0f, 0.008f, Filtro.PassaAlta, 2800f, 0.8f, 0.8f, 0.0003f, 0.3f),
                    Ruido(0.004f, 0.02f, Filtro.PassaFaixa, 1400f, 2f, 0.3f),
                    Nota(Onda.Seno, 0f, 0.06f, 150f, 85f, 0.9f, 0.3f, 0.001f) } };

                // ação principal: ordem confirmada no rádio (chiado, dois bipes de rádio tático e uma pancada grave que dá peso)
                case Som.Principal: return new Definicao { pico = 0.5f, reverb = 0.12f, cauda = 0.25f, vozes = new[] {
                    Ruido(0f, 0.045f, Filtro.PassaFaixa, 1700f, 1.2f, 0.5f, 0.004f, 0.6f, 3200f),
                    Nota(Onda.Quadrada, 0.05f, 0.06f, 1200f, -1f, 0.35f, 0.8f, 0.002f, Filtro.PassaBaixa, 3200f),
                    Nota(Onda.Quadrada, 0.115f, 0.08f, 1800f, -1f, 0.35f, 0.8f, 0.002f, Filtro.PassaBaixa, 3600f),
                    Nota(Onda.Seno, 0.05f, 0.12f, 90f, 45f, 0.7f, 0.4f, 0.002f) } };

                // trocar de tela ou aba: um toque curto e macio, quase só um "tum" surdo
                case Som.Aba: return new Definicao { pico = 0.22f, vozes = new[] {
                    Nota(Onda.Seno, 0f, 0.07f, 240f, 170f, 0.6f, 0.35f, 0.003f),
                    Ruido(0f, 0.02f, Filtro.PassaBaixa, 1200f, 0.8f, 0.15f, 0.004f) } };

                // alternar uma opção: relé duplo (dois estalos e dois "tum" de triângulo)
                case Som.Alternar: return new Definicao { pico = 0.34f, vozes = new[] {
                    Ruido(0f, 0.008f, Filtro.PassaFaixa, 3200f, 3f, 0.8f),
                    Nota(Onda.Triangulo, 0f, 0.04f, 420f, 300f, 0.5f, 0.4f),
                    Ruido(0.035f, 0.008f, Filtro.PassaFaixa, 2400f, 3f, 0.6f),
                    Nota(Onda.Triangulo, 0.035f, 0.03f, 300f, 240f, 0.3f, 0.4f) } };

                // sucesso: duas notas suaves e curtas, sem pancada nem brilho (neutro)
                case Som.Sucesso: return new Definicao { pico = 0.3f, reverb = 0.12f, cauda = 0.25f, vozes = new[] {
                    Nota(Onda.Seno, 0f, 0.22f, C5, -1f, 0.5f, 0.5f, 0.006f), Nota(Onda.Seno, 0.1f, 0.3f, G5, -1f, 0.4f, 0.55f, 0.006f) } };

                // erro: acesso negado (dois pulsos graves de painel, arredondados) e um zumbido curto
                case Som.Erro: return new Definicao { pico = 0.38f, reverb = 0.08f, cauda = 0.15f, vozes = new[] {
                    Nota(Onda.Quadrada, 0f, 0.09f, 130f, -1f, 0.5f, 1f, 0.002f, Filtro.PassaBaixa, 700f),
                    Nota(Onda.Quadrada, 0.11f, 0.14f, 98f, -1f, 0.5f, 1f, 0.002f, Filtro.PassaBaixa, 600f),
                    Ruido(0f, 0.05f, Filtro.PassaBaixa, 500f, 0.8f, 0.15f) } };

                // dinheiro: contrato carimbado (pancada surda do carimbo) e duas moedas metálicas, com eco curto
                case Som.Dinheiro: return new Definicao { pico = 0.5f, reverb = 0.12f, eco = 0.15f, ecoTempo = 0.09f, ecoRealim = 0.3f, cauda = 0.35f, vozes = new[] {
                    Nota(Onda.Seno, 0f, 0.12f, 110f, 55f, 0.9f, 0.3f, 0.001f), Ruido(0f, 0.03f, Filtro.PassaBaixa, 900f, 0.8f, 0.5f),
                    Sino(0.06f, 0.35f, 1046.5f, 2.2f, 0.3f, 2.76f, 0.35f), Sino(0.13f, 0.45f, 1568f, 2f, 0.25f, 2.76f, 0.4f) } };

                // aviso: ping de radar (nota longa com eco que se repete, como um sonar)
                case Som.Aviso: return new Definicao { pico = 0.35f, reverb = 0.15f, eco = 0.5f, ecoTempo = 0.22f, ecoRealim = 0.45f, cauda = 0.7f, vozes = new[] {
                    Nota(Onda.Seno, 0f, 0.45f, 1480f, -1f, 0.5f, 0.3f), Nota(Onda.Seno, 0f, 0.2f, 2960f, -1f, 0.08f, 0.3f),
                    Ruido(0f, 0.004f, Filtro.PassaFaixa, 5000f, 2f, 0.3f) } };

                // mensagem recebida: rajada de dados cifrados (três chilreios curtos de rádio) e uma nota de confirmação
                case Som.Receber: return new Definicao { pico = 0.3f, reverb = 0.1f, cauda = 0.15f, vozes = new[] {
                    Ruido(0f, 0.015f, Filtro.PassaFaixa, 3000f, 2f, 0.2f),
                    Nota(Onda.Quadrada, 0f, 0.028f, 1900f, -1f, 0.4f, 0.7f, 0.001f, Filtro.PassaBaixa, 3800f),
                    Nota(Onda.Quadrada, 0.04f, 0.03f, 2500f, -1f, 0.4f, 0.7f, 0.001f, Filtro.PassaBaixa, 3800f),
                    Nota(Onda.Quadrada, 0.08f, 0.03f, 2100f, -1f, 0.4f, 0.7f, 0.001f, Filtro.PassaBaixa, 3800f),
                    Nota(Onda.Seno, 0.12f, 0.06f, 440f, -1f, 0.15f, 0.5f) } };

                // avançar a semana: o tempo passando (tique-taque, um sopro grave que cresce e uma queda de grave)
                default: return new Definicao { pico = 0.42f, reverb = 0.25f, cauda = 0.45f, vozes = new[] {
                    Ruido(0f, 0.008f, Filtro.PassaFaixa, 3000f, 4f, 0.8f), Ruido(0.12f, 0.01f, Filtro.PassaFaixa, 1800f, 4f, 0.7f),
                    Ruido(0.05f, 0.32f, Filtro.PassaBaixa, 400f, 0.9f, 0.5f, 0.12f, 0.9f, 1400f),
                    Nota(Onda.Seno, 0.1f, 0.3f, 120f, 40f, 0.6f, 0.5f, 0.02f) } };
            }
        }
    }

    public static class SomSintese
    {
        public const int TAXA = 44100;

        // ---------------------------------------------------------------- filtro biquad (RBJ)
        sealed class Biquad
        {
            float b0, b1, b2, a1, a2, z1, z2;
            public void Ajusta(Filtro f, float fc, float q)
            {
                fc = Math.Max(20f, Math.Min(fc, TAXA * 0.45f));
                double w = 2.0 * Math.PI * fc / TAXA, cs = Math.Cos(w), al = Math.Sin(w) / (2.0 * Math.Max(0.1f, q)), a0 = 1.0 + al;
                switch (f)
                {
                    case Filtro.PassaBaixa: b0 = (float)((1 - cs) / 2 / a0); b1 = (float)((1 - cs) / a0); b2 = b0; break;
                    case Filtro.PassaAlta: b0 = (float)((1 + cs) / 2 / a0); b1 = (float)(-(1 + cs) / a0); b2 = b0; break;
                    default: b0 = (float)(al / a0); b1 = 0f; b2 = -b0; break;   // passa-faixa
                }
                a1 = (float)(-2.0 * cs / a0); a2 = (float)((1.0 - al) / a0);
            }
            public float Passa(float x) { float y = b0 * x + z1; z1 = b1 * x - a1 * y + z2; z2 = b2 * x - a2 * y; return y; }
        }

        /// <summary>Mistura as vozes, aplica eco e reverb de sala e leva o pico ao valor pedido. Determinística: o ruído usa sempre a mesma semente.</summary>
        public static float[] Renderizar(Definicao d)
        {
            float cauda = d.reverb > 0f || d.eco > 0f ? d.cauda : 0.02f;
            float total = d.vozes.Max(v => v.inicio + v.dur) + cauda;
            var buf = new float[(int)(total * TAXA)];
            uint rng = 2463534242u;
            foreach (var v in d.vozes) MisturaVoz(buf, v, ref rng);
            if (d.eco > 0f) AplicaEco(buf, d);
            if (d.reverb > 0f) buf = AplicaReverb(buf, d.reverb);
            int fade = (int)(0.025f * TAXA);   // some em 25 ms no fim: nunca termina com estalo
            for (int i = 0; i < fade && i < buf.Length; i++) buf[buf.Length - 1 - i] *= i / (float)fade;
            float pico = 0f; foreach (var x in buf) pico = Math.Max(pico, Math.Abs(x));
            float k = pico > 1e-6f ? d.pico / pico : 1f;
            for (int i = 0; i < buf.Length; i++) buf[i] = (float)Math.Tanh(buf[i] * k);   // limitador suave
            return buf;
        }

        public static float[] Renderizar(Som s) => Renderizar(SomCatalogo.Receita(s));

        static void MisturaVoz(float[] buf, Voz v, ref uint rng)
        {
            int i0 = (int)(v.inicio * TAXA), n = (int)(v.dur * TAXA); double fase = 0, faseMod = 0;
            var bq = new Biquad(); bool filtra = v.filtro != Filtro.Nenhum && v.corte > 0f;
            if (filtra) bq.Ajusta(v.filtro, v.corte, v.q);
            float f1 = v.f1 < 0f ? v.f0 : v.f1;
            for (int k = 0; k < n && i0 + k < buf.Length; k++)
            {
                float t = k / (float)TAXA, u = t / v.dur;
                if (filtra && v.corteFim > 0f && (k & 31) == 0) bq.Ajusta(v.filtro, (float)(v.corte * Math.Pow(v.corteFim / v.corte, u)), v.q);   // filtro que varre
                double f = v.f0 * Math.Pow(f1 / v.f0, u);
                fase += 2.0 * Math.PI * f / TAXA;
                float s;
                switch (v.onda)
                {
                    case Onda.Triangulo: s = (float)(2.0 / Math.PI * Math.Asin(Math.Sin(fase))); break;
                    case Onda.Quadrada: s = (float)Math.Tanh(4.0 * Math.Sin(fase)); break;
                    case Onda.FM:
                        faseMod += 2.0 * Math.PI * f * v.razao / TAXA;
                        s = (float)Math.Sin(fase + v.indice * Math.Exp(-t / (v.dur * v.queda * 0.6f)) * Math.Sin(faseMod)); break;
                    case Onda.Ruido:
                        rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5;
                        s = rng / (float)uint.MaxValue * 2f - 1f; break;
                    default: s = (float)Math.Sin(fase); break;
                }
                if (filtra) s = bq.Passa(s);
                float env = (t < v.ataque ? t / v.ataque : 1f) * (float)Math.Exp(-t / (v.dur * v.queda));
                float fim = Math.Min(1f, (v.dur - t) / 0.004f);   // some em 4 ms no fim da voz
                buf[i0 + k] += s * env * fim * v.vol;
            }
        }

        static void AplicaEco(float[] buf, Definicao d)
        {
            int atraso = Math.Max(1, (int)(d.ecoTempo * TAXA)); var eco = new float[buf.Length]; float lp = 0f;
            for (int i = 0; i < buf.Length; i++)
            {
                float volta = i >= atraso ? eco[i - atraso] : 0f; lp += 0.35f * (volta - lp);   // cada repetição fica mais escura
                eco[i] = buf[i] + d.ecoRealim * lp;
            }
            for (int i = atraso; i < buf.Length; i++) buf[i] += d.eco * eco[i - atraso];
        }

        /// <summary>Reverb de sala pequena (4 filtros-pente em paralelo e 2 passa-tudo em série, no estilo Schroeder).</summary>
        static float[] AplicaReverb(float[] seco, float molhado)
        {
            int[] pentes = { 1116, 1188, 1277, 1356 }, passa = { 556, 441 };
            var saida = new float[seco.Length];
            var pb = pentes.Select(p => new float[p]).ToArray(); var pi = new int[pentes.Length]; var amort = new float[pentes.Length];
            var ab = passa.Select(p => new float[p]).ToArray(); var ai = new int[passa.Length];
            for (int n = 0; n < seco.Length; n++)
            {
                float x = seco[n] * 0.25f, soma = 0f;
                for (int c = 0; c < pentes.Length; c++)
                {
                    float y = pb[c][pi[c]]; amort[c] += 0.7f * (y - amort[c]);   // amortecimento: a cauda perde agudos
                    pb[c][pi[c]] = x + amort[c] * 0.8f; pi[c] = (pi[c] + 1) % pentes[c]; soma += y;
                }
                for (int a = 0; a < passa.Length; a++)
                {
                    float atrasado = ab[a][ai[a]], entra = soma + atrasado * 0.5f;
                    ab[a][ai[a]] = entra; ai[a] = (ai[a] + 1) % passa[a]; soma = atrasado - soma * 0.5f;
                }
                saida[n] = seco[n] + molhado * soma;
            }
            return saida;
        }

        /// <summary>O ambiente da sala de controle, em laço de 8 s: um zumbido grave de 55 Hz, o sopro suave de uma ventilação que respira e
        /// um murmúrio distante, tudo bem baixo. As modulações fecham um número inteiro de ciclos no laço, então não há emenda.</summary>
        public static float[] Ambiente(float segundos = 8f)
        {
            int n = (int)(segundos * TAXA), extra = (int)(0.5f * TAXA), total = n + extra;
            var bruto = new float[total]; uint rng = 88172645u;
            var grave = new Biquad(); grave.Ajusta(Filtro.PassaBaixa, 300f, 0.7f); var grave2 = new Biquad(); grave2.Ajusta(Filtro.PassaBaixa, 300f, 0.7f);
            var vento = new Biquad(); vento.Ajusta(Filtro.PassaFaixa, 800f, 1.0f);
            var murmurio = new Biquad(); murmurio.Ajusta(Filtro.PassaFaixa, 420f, 1.2f);
            for (int i = 0; i < total; i++)
            {
                double t = i / (double)TAXA;
                rng ^= rng << 13; rng ^= rng >> 17; rng ^= rng << 5; float b = rng / (float)uint.MaxValue * 2f - 1f;
                float respira = 0.55f + 0.45f * (float)Math.Sin(2.0 * Math.PI * t / segundos);           // 1 ciclo no laço
                float lento = 0.5f + 0.5f * (float)Math.Sin(2.0 * Math.PI * 2.0 * t / segundos + 1.3);   // 2 ciclos
                float s = grave2.Passa(grave.Passa(b)) * 1.6f + vento.Passa(b) * 0.10f * respira + murmurio.Passa(b) * 0.12f * lento;
                s += (float)(Math.Sin(2.0 * Math.PI * 55.0 * t) * 0.12 + Math.Sin(2.0 * Math.PI * 110.0 * t) * 0.04);   // 55 Hz e 110 Hz fecham ciclos inteiros em 8 s
                bruto[i] = s;
            }
            var laco = new float[n];
            for (int i = 0; i < n; i++)
            {
                if (i < extra) { float w = i / (float)extra; laco[i] = bruto[i] * w + bruto[n + i] * (1f - w); }   // a cauda extra vira o começo: emenda sem estalo
                else laco[i] = bruto[i];
            }
            float pico = 0f; foreach (var x in laco) pico = Math.Max(pico, Math.Abs(x));
            for (int i = 0; i < n; i++) laco[i] = laco[i] / pico * 0.5f;
            return laco;
        }

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
