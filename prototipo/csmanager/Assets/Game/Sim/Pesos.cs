using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace CSM.Sim
{
    /// <summary>Faixa que o treino pode mexer num peso, e o que ele faz.</summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class FaixaAttribute : Attribute
    {
        public readonly float min, max; public readonly string doc;
        public FaixaAttribute(float min, float max, string doc) { this.min = min; this.max = max; this.doc = doc; }
    }

    /// <summary>
    /// Os "botões" do comportamento dos bots: quanto vale cada jogada na hora de escolher o que fazer, tempos do IGL,
    /// spray. É isto que o treino ajusta (MatchCli.Treino): roda muitos rounds, mede (Metricas) e mexe nos pesos até os
    /// números ficarem perto dos alvos de pro. Cada time tem os seus — o timeout do manager mexe no <see cref="estilo"/>.
    /// O arquivo Assets/Game/Sim/pesos.txt (se existir) é o resultado aprovado do treino e vira o padrão.
    /// </summary>
    public class Pesos
    {
        // ---- segurar ativo: utilidade de cada jogada quando está segurando uma posição. As jogadas têm piso: o treino
        // decide quanto, mas não pode sumir com elas (com ruído de poucos rounds ele "aprendia" a zerar o jiggle)
        [Faixa(0.4f, 3, "jiggle peek: sai do cover, olha o ângulo e volta")] public float jiggle = 1.2f;
        [Faixa(0.2f, 3, "wide peek: sai largo pra pegar info ou duelo")] public float wide = 0.5f;
        [Faixa(0.2f, 3, "ajeitar: anda devagarinho sem perder a mira")] public float ajeitar = 0.8f;
        [Faixa(0.25f, 3, "trocar de ângulo")] public float trocar = 0.7f;
        [Faixa(0.2f, 3, "avançar pra tomar espaço")] public float avancar = 0.6f;
        [Faixa(0, 3, "ficar quieto segurando apertado")] public float quieto = 0.6f;
        [Faixa(0.3f, 3, "abrir ângulo com bang (pra mim ou pedindo pro colega)")] public float bang = 1.2f;
        [Faixa(0.2f, 3, "pausa média entre uma jogada e outra (s)")] public float pausa = 1.0f;
        [Faixa(8, 40, "até onde vai o jiggle/wide (unidades)")] public float peekDist = 20;
        [Faixa(3, 20, "segundos no mesmo lugar até querer trocar")] public float cansaLugar = 8;
        [Faixa(0, 3, "quanto a chance de ter inimigo (calor) puxa pra pegar info")] public float sedeInfo = 1;
        [Faixa(0, 1, "segurar do cover e dar peek (1) ou segurar aberto (0)")] public float cover = 0.7f;
        [Faixa(3, 25, "segundos pra uma área limpa voltar a ser suspeita (memória do time)")] public float memoria = 10;
        // ---- combate
        [Faixa(1, 6, "a partir de qual bala do spray a mira desce pro peito")] public float sprayPeito = 3;
        [Faixa(0, 1, "chance de strafe entre as rajadas")] public float strafeRajada = 0.6f;
        // ---- TR
        // ritmo de pro: toma o mapa antes e entra no bomb lá pra 1:05-0:45 do relógio (round de 115 s)
        [Faixa(40, 80, "default: hora de decidir o bomb (s)")] public float tExecDefault = 58;
        [Faixa(25, 65, "execute: hora de entrar (s)")] public float tExec = 44;
        [Faixa(35, 75, "fake: hora da execução de verdade (s)")] public float tFake = 54;
        // ---- CT
        // piso 0,25: o treino tem que deixar a agressão boa, não sumir com ela (variedade é parte do jogo)
        [Faixa(0.25f, 1, "chance de alguém fazer jogada agressiva cedo")] public float ctAgressao = 0.35f;
        [Faixa(6, 30, "agressão cedo: volta pra posição depois de (s)")] public float ctRecua = 15;
        [Faixa(1, 4, "contatos num bomb pra girar")] public float ctGiro = 2;
        // ---- estilo do time (o manager mexe no timeout; o treino não)
        /// <summary>-1 defensivo … 0 normal … +1 agressivo: escala wide/avançar/agressão e diminui o segurar quieto.</summary>
        public float estilo = 0;
        /// <summary>Estilo dos planos que o IGL prefere no banco de estratégias (decisão 0014). Equilibrada = sem preferência.</summary>
        public Estrategias.Categoria categoria = Estrategias.Categoria.Equilibrada;

        public float Agr(float skillAggression) => MathX.Clamp(skillAggression * (1 + 0.6f * estilo), 0, 1.5f);

        // ================================================================ treino / arquivo
        public static readonly FieldInfo[] Campos = typeof(Pesos).GetFields().Where(f => f.GetCustomAttribute<FaixaAttribute>() != null).ToArray();
        public static FaixaAttribute FaixaDe(FieldInfo f) => f.GetCustomAttribute<FaixaAttribute>();

        public Pesos Clone() => (Pesos)MemberwiseClone();

        /// <summary>Valor do campo i normalizado 0..1 na faixa (o treino anda nesse espaço).</summary>
        public float GetN(int i) { var f = Campos[i]; var r = FaixaDe(f); return ((float)f.GetValue(this) - r.min) / (r.max - r.min); }
        public void SetN(int i, float v) { var f = Campos[i]; var r = FaixaDe(f); f.SetValue(this, r.min + MathX.Clamp(v, 0, 1) * (r.max - r.min)); }

        public string Texto()
        {
            var sb = new StringBuilder();
            foreach (var f in Campos) sb.AppendLine($"{f.Name,-14} = {((float)f.GetValue(this)).ToString("0.###", CultureInfo.InvariantCulture),-8} # {FaixaDe(f).doc}");
            return sb.ToString();
        }

        public static Pesos De(string texto)
        {
            var p = new Pesos();
            foreach (var raw in texto.Split('\n'))
            {
                var l = raw.Split('#')[0].Trim(); int e = l.IndexOf('='); if (e < 0) continue;
                var f = typeof(Pesos).GetField(l.Substring(0, e).Trim());
                if (f == null || !float.TryParse(l.Substring(e + 1).Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) continue;
                var r = FaixaDe(f); if (r != null) v = MathX.Clamp(v, r.min, r.max);   // respeita os pisos atuais
                f.SetValue(p, v);
            }
            return p;
        }

        public const string ARQUIVO = "Assets/Game/Sim/pesos.txt";
        static Pesos padrao;
        /// <summary>Os pesos aprovados (pesos.txt) ou os de fábrica. Sempre devolve uma cópia.</summary>
        public static Pesos Padrao()
        {
            if (padrao == null) padrao = File.Exists(ARQUIVO) ? De(File.ReadAllText(ARQUIVO)) : new Pesos();
            return padrao.Clone();
        }
    }
}
