// Força medida de cada plano do banco no headless (Headless -banco). Gerado; vazio = todos iguais.
using System.Collections.Generic;

namespace CSM.Sim
{
    public static partial class Estrategias
    {
        static readonly (int idx, float forca)[] FORCA = { };
        static Dictionary<int, float> forcaMap;
        /// <summary>Peso de escolha pela força medida (1 = sem medida).</summary>
        public static float Forca(int idx)
        {
            if (forcaMap == null) { var d = new Dictionary<int, float>(); foreach (var (i, f) in FORCA) d[i] = f; forcaMap = d; }
            return forcaMap.TryGetValue(idx, out var v) ? v : 1f;
        }
    }
}
