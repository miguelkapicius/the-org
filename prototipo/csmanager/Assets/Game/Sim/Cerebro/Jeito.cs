namespace CSM.Sim
{
    /// <summary>
    /// CÉREBRO NOVO — o JEITO FIXO de cada jogador (PLANO_SIMULACAO.md §3, princípio 5: "jeito próprio"): um número de 0 a 1, estável
    /// pra cada chave (nome do jogador + assunto), que não depende do round nem de sorteio. É o que desempata opções de valor parecido
    /// e dá a cada um o seu ritmo e o seu gosto. Quando os atributos entrarem, é aqui que eles passam a pesar.
    /// </summary>
    public static class Jeito
    {
        public static float De(string chave) { unchecked { int h = 17; foreach (char c in chave) h = h * 31 + c; h ^= h >> 13; return (h & 0xFFFF) / 65535f; } }
    }
}
