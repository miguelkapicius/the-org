using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using CSM.Sim;

namespace CSM.View3D
{
    /// <summary>
    /// O que a partida 3D herdou do visual 2D antigo (TelaJogo, removido): o diretor de câmera que segue a ação, o
    /// estado de animação de cada bot, a arma padrão de cada lado e quanto o corpo anda num ciclo de passo.
    /// </summary>
    public static class Transmissao
    {
        public static string Padrao(bool ct) => ct ? "M4A4" : "AK47";

        // quanto o corpo anda num ciclo de passo (unidades do jogo), medido no Blender (era o passo.txt dos sprites 2D):
        // o ciclo toca na velocidade do boneco e o pé não patina
        static readonly Dictionary<string, float> passo = new Dictionary<string, float> { ["andar"] = 24.29f, ["agachado"] = 10f, ["faca_andar"] = 28.12f };
        public static float Ciclo(string anim) => passo.TryGetValue(anim, out float c) ? c : 24.29f;

        /// <summary>Estado da animação de cada bot (fase do passo, quando começou a ação, a recarga, o tiro).</summary>
        public class Anim { public float passo; public string acao; public float acaoIni, acaoDur; public V2 ultPos; public bool tem; }

        /// <summary>Câmera automática: onde está a ação (duelo) > granada voando (a mais nova, até estourar e um pouco
        /// depois) > bomb plantado > quem leva o bomb > meio dos TR; nunca o vazio.</summary>
        public static void Mirar(Match m, ref V2 cam, ref V2 foco, ref float focoAte, float dt)
        {
            var duelo = m.agents.Where(a => a.alive && a.brain.target != null).OrderBy(a => a.id).FirstOrDefault();
            Match.Nade nova = null;
            foreach (var n in m.flying) if (nova == null || n.t0 > nova.t0) nova = n;
            if (duelo != null) { foco = (duelo.pos + duelo.brain.target.pos) / 2; focoAte = m.t + 1.5f; }
            else if (nova != null) { foco = nova.P; focoAte = m.t + 1.2f; }   // acompanha o voo; segura no estouro
            else if (m.t > focoAte)
            {
                var vivosT = m.agents.Where(a => a.alive && a.IsT).ToList();
                var carrier = vivosT.FirstOrDefault(a => a.hasBomb);
                foco = m.bombPlanted ? m.bombPos : carrier != null ? carrier.pos : vivosT.Count > 0 ? vivosT.Aggregate(new V2(0, 0), (s, a) => s + a.pos) / vivosT.Count : cam;
            }
            V2 f = foco;
            var vivos = m.agents.Where(a => a.alive).ToList();
            if (vivos.Count > 0 && !vivos.Any(a => (a.pos - f).Len < 150))
            {
                var perto = vivos.OrderBy(a => (a.pos - f).Len).First();
                foco = m.bombPlanted && (perto.pos - f).Len < 400 ? (perto.pos + f) / 2 : perto.pos;
            }
            cam = cam + (foco - cam) * (1 - MathF.Exp(-2.2f * dt));
        }
    }

    /// <summary>
    /// Diretor de transmissão (substitui o Mirar): como o observer de campeonato, sempre tem um PROTAGONISTA (o jogador
    /// cuja "POV" está no ar; o HUD mostra ele como observado) e troca de protagonista pela nota de interesse de cada um:
    /// troca de tiro > plantando/desarmando > contato iminente (inimigo à vista ou muito perto, antes do 1º tiro) >
    /// clutch > quem leva a C4 > estrela do round. Regras de diretor: plano mínimo de 3 s (1,2 s se a outra luta é
    /// muito melhor), nunca sai do meio da troca nem logo depois do abate, quando o protagonista morre segura ~1 s no
    /// lance (quem morreu + quem matou) e passa pro matador se ele continua relevante; troca longe = corte seco, perto =
    /// panorâmica. O quadro enquadra o protagonista com espaço pra onde ele mira + quem está no duelo com ele + o colega
    /// trocando do lado + a C4 quando é a ação. No freeze mostra a base de um time e depois corta pra do outro.
    /// </summary>
    public class Diretor
    {
        /// <summary>A/B: true volta pro diretor antigo (Transmissao.Mirar).</summary>
        public static bool Velho;
        public const float CORTE = 380f;              // trocou de plano pra mais longe que isso: corte seco

        public int prota = -1;                        // id do protagonista (= índice em m.agents)
        public V2 centro;                             // pra onde a câmera vai
        public float x0, x1, y0, y1;                  // caixa que tem que caber no quadro (unidades do jogo)
        public bool luta;                             // tem troca de tiro no quadro (câmera mais rápida e mais fechada)
        public bool corte;                            // trocou de plano longe: a câmera corta (quem lê zera)
        public string plano = "";                     // pra log/teste: freeze, luta, contato, bomba, clutch, seguindo, morte
        public int planos, cortes;                    // contagem (teste)

        readonly float[] nota = new float[10], ultLuta = new float[10];
        readonly int[] abatesRound = new int[10];
        Match mAtual; int abatesVistos;
        float agora, desde, avaliarEm, mortoEm = -1; V2 mortoOnde, lead; int matador = -1; bool freezeT;

        void Reiniciar(Match m)
        {
            mAtual = m; prota = -1; abatesVistos = 0; mortoEm = -1; matador = -1; avaliarEm = 0; lead = new V2(0, 0);
            for (int i = 0; i < 10; i++) { nota[i] = 0; ultLuta[i] = -99; abatesRound[i] = 0; }
        }

        /// <summary>Um quadro do diretor. freezeResta &gt; 0 = tempo de compra (a Sim parada no spawn).</summary>
        public void Atualizar(Match m, float freezeResta, float dt)
        {
            if (m != mAtual) Reiniciar(m);
            agora += dt;
            int n = Math.Min(10, m.agents.Count);
            if (n == 0) return;

            if (freezeResta > 0)
            {
                // freeze: a base do TR na 1ª metade, corte pra base do CT na 2ª (os itens comprados sobem das cabeças)
                bool t = freezeResta > Match.FREEZE / 2;
                if (t != freezeT || prota < 0) { freezeT = t; var q = m.agents.FirstOrDefault(a => a.IsT == t && a.hasBomb) ?? m.agents.First(a => a.IsT == t); Trocar(m, q.id, true); }
                plano = "freeze";
                Caixa(); foreach (var a in m.agents) if (a.IsT == freezeT) Inclui(a.pos);
                Fechar(false);
                return;
            }

            // quem está trocando tiro (os dois lados da troca) e os abates novos
            for (int i = 0; i < n; i++)
            {
                var a = m.agents[i];
                if (!a.alive || a.brain.target == null || !a.brain.target.alive) continue;
                ultLuta[i] = agora; if (a.brain.target.id < 10) ultLuta[a.brain.target.id] = agora;
            }
            for (; abatesVistos < m.abates.Count; abatesVistos++)
            {
                var ab = m.abates[abatesVistos];
                if (ab.by != null && ab.by != ab.v && !ab.aliado && ab.by.id < 10) { abatesRound[ab.by.id]++; ultLuta[ab.by.id] = agora; }
                if (ab.v.id == prota) { mortoEm = agora; mortoOnde = ab.v.pos; matador = ab.by != null && ab.by != ab.v ? ab.by.id : -1; }
            }

            if (agora >= avaliarEm) { avaliarEm = agora + 0.2f; for (int i = 0; i < n; i++) nota[i] = Nota(m, m.agents[i]); }
            int melhor = -1; for (int i = 0; i < n; i++) if (m.agents[i].alive && (melhor < 0 || nota[i] > nota[melhor])) melhor = i;

            var cur = prota >= 0 && prota < n ? m.agents[prota] : null;
            if (cur != null && !cur.alive)
            {
                if (mortoEm < 0) { mortoEm = agora; mortoOnde = cur.pos; }
                if (agora - mortoEm < 1.1f || melhor < 0)
                {
                    // segura no lance: onde ele caiu + quem matou
                    plano = "morte"; Caixa(); Inclui(mortoOnde);
                    if (matador >= 0 && matador < n && m.agents[matador].alive && (m.agents[matador].pos - mortoOnde).Len < 900) Inclui(m.agents[matador].pos);
                    Fechar(true);
                    return;
                }
                int prox = melhor;
                if (matador >= 0 && matador < n && m.agents[matador].alive && nota[matador] >= nota[melhor] - 25) prox = matador;
                Trocar(m, prox, false);
            }
            else if (cur == null) { if (melhor >= 0) Trocar(m, melhor, false); }
            else if (melhor >= 0 && melhor != prota)
            {
                bool travado = agora - ultLuta[prota] < 1.4f;   // no meio da troca ou logo depois do abate: não sai
                float ganho = nota[melhor] - nota[prota], dur = agora - desde;
                if (!travado && ((dur > 3f && ganho > 25) || (dur > 1.2f && ganho > 70))) Trocar(m, melhor, false);
            }
            if (prota < 0) return;
            Enquadrar(m, m.agents[prota], dt);
        }

        /// <summary>Nota de interesse de um jogador agora (quanto mais, mais vale estar na POV dele).</summary>
        float Nota(Match m, Agent a)
        {
            if (!a.alive) return -999;
            float s = 0;
            if (a.brain.target != null && a.brain.target.alive) s += 100;
            else if (agora - ultLuta[a.id] < 2.5f) s += 55;
            // contato iminente: inimigo à vista (ainda sem tiro) ou muito perto atrás da parede
            float cont = 0; int perto = 0;
            foreach (var e in m.agents)
            {
                if (!e.alive || e.IsT == a.IsT) continue;
                float d = (e.pos - a.pos).Len; if (d > 1000) continue;
                float c = MapData.LosWalk(a.pos, a.z, e.pos, e.z, NavGrid.EYE) ? 55 + 45 * (1 - d / 1000) : d < 420 ? 15 + 30 * (1 - d / 420) : 0;
                if (c > 0) perto++;
                cont = Math.Max(cont, c);
            }
            s += cont + 6 * Math.Max(0, perto - 1);
            string ac = a.brain.action;
            if (ac == "plantar" || ac == "desarmar") s += 95;
            if (a.hasBomb) s += 12;
            if (m.bombPlanted) s += (a.pos - m.bombPos).Len < (a.IsT ? 500 : 650) ? (a.IsT ? 10 : 22) : 0;
            int meus = 0, deles = 0; foreach (var o in m.agents) if (o.alive) { if (o.IsT == a.IsT) meus++; else deles++; }
            if (meus == 1 && deles >= 1) s += 30 + 5 * deles;   // clutch
            if (a.gunId == "awp") s += 6;
            s += 4 * abatesRound[a.id];
            if (a.id == prota) s += 20;                         // inércia: não pula por qualquer coisa
            return s;
        }

        void Trocar(Match m, int novo, bool freeze)
        {
            if (novo == prota) return;
            var a = m.agents[novo];
            bool longe = prota < 0 || (a.pos - centro).Len > CORTE;
            if (prota >= 0 && longe) { corte = true; cortes++; }
            prota = novo; desde = agora; mortoEm = -1; matador = -1; planos++;
            lead = new V2(MathF.Cos(a.aim), MathF.Sin(a.aim)) * 110;
            if (prota >= 0 && longe) centro = a.pos;   // o próximo Fechar recalcula; aqui só pro teste de distância seguinte
        }

        void Enquadrar(Match m, Agent a, float dt)
        {
            // espaço na frente de quem está no ar (pra onde mira), suavizado pra não balançar a cada virada de mira
            var olha = new V2(MathF.Cos(a.aim), MathF.Sin(a.aim)) * 110;
            lead = lead + (olha - lead) * (1 - MathF.Exp(-2.5f * dt));
            Caixa(); Inclui(a.pos); Inclui(a.pos + lead);
            bool briga = false;
            foreach (var e in m.agents)
            {
                if (!e.alive || e == a) continue;
                if (e.IsT != a.IsT)
                {
                    bool duelo = a.brain.target == e || e.brain.target == a;
                    float d = (e.pos - a.pos).Len;
                    if (duelo || (d < 520 && MapData.LosWalk(a.pos, a.z, e.pos, e.z, NavGrid.EYE))) { Inclui(e.pos); briga |= duelo; }
                }
                else if ((e.pos - a.pos).Len < 300 && e.brain.target != null && e.brain.target.alive && (e.brain.target.pos - a.pos).Len < 650)
                { Inclui(e.pos); Inclui(e.brain.target.pos); briga = true; }   // colega trocando do lado
            }
            string ac = a.brain.action;
            if ((ac == "plantar" || ac == "desarmar") && m.bombPlanted) Inclui(m.bombPos);
            // caixa grande demais (alguém do outro lado): fica só com ele, a mira e o alvo
            if (x1 - x0 > 1000 || y1 - y0 > 850)
            {
                Caixa(); Inclui(a.pos); Inclui(a.pos + lead);
                if (a.brain.target != null && a.brain.target.alive) Inclui(a.brain.target.pos);
            }
            briga |= a.brain.target != null && a.brain.target.alive;
            plano = briga ? "luta" : ac == "plantar" || ac == "desarmar" ? "bomba" : nota[a.id] >= 70 ? "contato" : m.agents.Count(o => o.alive && o.IsT == a.IsT) == 1 ? "clutch" : "seguindo";
            Fechar(briga);
        }

        void Caixa() { x0 = y0 = 1e9f; x1 = y1 = -1e9f; }
        void Inclui(V2 p) { x0 = Math.Min(x0, p.x); x1 = Math.Max(x1, p.x); y0 = Math.Min(y0, p.y); y1 = Math.Max(y1, p.y); }
        void Fechar(bool briga) { luta = briga; centro = new V2((x0 + x1) / 2, (y0 + y1) / 2); }
    }
}
