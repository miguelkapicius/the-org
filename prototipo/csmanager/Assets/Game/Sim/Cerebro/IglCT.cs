using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using static CSM.Sim.Places;

namespace CSM.Sim
{
    /// <summary>Tarefa de um jogador dentro da jogada do time (PLANO_SIMULACAO.md §4.1): o que ele faz, com quem, e o gatilho.</summary>
    public class Tarefa
    {
        public string tipo;            // ancora · apoio · meio · girar · cruzar · recuar
        public char site = 'M';        // A, B ou M (meio)
        public Hold hold; public Agent parceiro; public string porque;
        public float desde;
    }

    /// <summary>
    /// CÉREBRO NOVO — IGL do CT antes do plant (flag cerebroNovoCT; só Vila por enquanto). Substitui o StartCT e a parte pré-plant
    /// do TickCT do Igl velho. Diferenças de princípio (PLANO_SIMULACAO.md §3):
    ///  - o setup é ESCOLHIDO pelo valor (leitura do adversário, o que vem dando certo, custo de repetir), não sorteado;
    ///  - cada jogador recebe uma TAREFA com parceiro e gatilho ("cruzo com o âncora; se ele cair, seguro a linha de quem matou");
    ///  - o IGL só mexe quando a SITUAÇÃO muda (baixa, informação de execução, C4 visto, vantagem/desvantagem), e a decisão
    ///    fica valendo até mudar de novo;
    ///  - junto só com função: ninguém fica num bomb sem tarefa, e quem estava de apoio age quando o parceiro cai.
    /// O retake, o corte de plant e o C4 caído continuam no Igl velho até serem refeitos.
    /// </summary>
    public class IglCT
    {
        readonly Match m; readonly TeamBrain tb; readonly Coletivo col; readonly Rng rng; readonly Team team = Team.CT;
        public readonly Dictionary<int, Tarefa> tarefa = new Dictionary<int, Tarefa>(); readonly Caminho.Memoria caminhos = new Caminho.Memoria();
        int baixasTratadas; readonly Dictionary<int, float> ocupadoAte = new Dictionary<int, float>(); char giradoPara = ' '; bool giroTotal; string posturaAnt = ""; float proxPares;

        public IglCT(Match m, TeamBrain tb, Coletivo col, Rng rng) { this.m = m; this.tb = tb; this.col = col; this.rng = rng; }

        static Hold[] DoSite(char s) => s == 'A' ? new[] { CT_A_Plataforma, CT_A_Caixa, CT_A_Canto, CT_A_RuaCT } : s == 'B' ? new[] { CT_B_Banca, CT_B_Caixa, CT_B_Pilar, CT_B_Viela } : new[] { CT_Janelao, CT_Praca, CT_MeioCaixa, CT_Conector, CT_Rato };
        static readonly (string nome, int a, int mid, int b)[] SETUPS = { ("novo: 2-1-2", 2, 1, 2), ("novo: A forte 3-1-1", 3, 1, 1), ("novo: B forte 1-1-3", 1, 1, 3), ("novo: meio forte 1-2-2", 1, 2, 2), ("novo: meio forte 2-2-1", 2, 2, 1) };

        /// <summary>Mania fixa do jogador (princípio 5: jeito próprio): um número estável por jogador e assunto, sem depender do round.</summary>
        static float Mania(Agent a, string assunto) => Jeito.De(a.name + "|" + assunto);

        void Dar(Agent a, Tarefa t, bool correr = true)
        {
            t.desde = m.t; tarefa[a.id] = t; bool giro = t.tipo == "girar" || t.tipo == "recuar";
            a.brain.SetOrder(new Order { name = $"{t.tipo}: {t.hold.name}", hold = t.hold, why = t.porque, run = correr, urgente = giro });
            if (giro && Caminho.Ligado) Caminho.Aplica(m, tb, a, t.hold.pos, caminhos);   /* por onde girar é escolha de cada um, pela crença e por onde os colegas já foram */
        }

        // ---------------------------------------------------------------- compra: escolher o setup pelo valor
        public void Start()
        {
            var L = m.leitura[(int)team]; var meus = m.agents.Where(a => a.alive && a.team == team).ToList();
            bool pistolas = m.compra[(int)team] != "full";
            float lado = 0; if (L != null && L.Adapta) { L.AtualizaCT(); lado = L.intencao.lado; }   // > 0: o TR deles vem mais no A
            int melhor = 0; float mv = -99; var notas = new float[SETUPS.Length];
            for (int i = 0; i < SETUPS.Length; i++)
            {
                var s = SETUPS[i];
                float v = i == 0 ? 1f : 0.82f;                                   // o equilibrado é a base; reforçar um lado é aposta
                v += 0.35f * lado * (s.a - s.b) / 2f;                            // a leitura do adversário puxa o reforço pro lado que eles atacam
                if (pistolas && i > 0 && i < 3) v += 0.12f;                      // sem arma, apostar num lado vale mais
                if (L != null) { v += 0.3f * (L.Sucesso(s.nome) - 0.5f); v -= 0.18f * L.Repetido(s.nome); }   // o que vem dando certo; repetir é ser lido
                v += 0.06f * (Mania(meus.FirstOrDefault(a => a.role == Role.Igl) ?? meus[0], s.nome) - 0.5f);                     // gosto do IGL (fixo): desempata sem sorteio
                notas[i] = v; if (v > mv) { mv = v; melhor = i; }
            }
            var set = SETUPS[melhor]; tb.plan = set.nome; tb.stage = "setup (novo)";
            // posições de cada zona: a menos queimada primeiro (onde já mataram/morreram dali o TR pré-mira)
            Hold[] Pos(char z, int n)
            {
                var c = DoSite(z).ToList();
                if (z == 'M' && !meus.Any(a => a.gunId == "awp")) c.Remove(CT_Janelao);   // janelão é linha longa: sem AWP, meio de perto
                return c.OrderBy(h => (L != null ? L.Queimado(h.pos, team, 90) : 0) + 0.01f * Array.IndexOf(DoSite(z), h)).Take(n).ToArray();
            }
            var vagas = new List<(Hold h, char z, int ordem)>();
            int k = 0; foreach (var h in Pos('A', set.a)) vagas.Add((h, 'A', k++)); k = 0; foreach (var h in Pos('M', set.mid)) vagas.Add((h, 'M', k++)); k = 0; foreach (var h in Pos('B', set.b)) vagas.Add((h, 'B', k++));
            // quem vai onde: o AWP na linha longa, o âncora no bomb dele, o resto pelo caminho mais curto e pelo gosto do jogador
            var livres = meus.ToList(); var dono = new Dictionary<Hold, Agent>();
            bool temJanelao = vagas.Any(v => v.h == CT_Janelao); var awper = meus.FirstOrDefault(a => a.gunId == "awp");
            if (temJanelao && awper != null) { dono[CT_Janelao] = awper; livres.Remove(awper); }   // a linha longa é do AWP antes de qualquer outra vaga
            foreach (var (h, z, ordem) in vagas.OrderBy(v => v.ordem))
            {
                if (livres.Count == 0) break; if (dono.ContainsKey(h)) continue;
                Agent q = livres.OrderBy(a =>
                    NavGrid.PathLen(a.pos, h.pos) / 400f
                    - (a.gunId == "awp" && (h == CT_A_Plataforma || h == CT_B_Banca) ? 3f : 0)
                    - (a.role == Role.Ancora && ordem == 0 && z != 'M' && a.brain.homeSite == z ? 2f : 0)
                    - (a.role == Role.Rotacao && z == 'M' ? 1.5f : 0)
                    - 0.8f * Mania(a, h.name)).First();
                livres.Remove(q); dono[h] = q;
            }
            foreach (var (h, z, ordem) in vagas)
            {
                if (!dono.TryGetValue(h, out var q)) continue;
                var ancora = vagas.Where(v => v.z == z && v.ordem == 0 && dono.ContainsKey(v.h)).Select(v => dono[v.h]).FirstOrDefault();
                if (z == 'M') Dar(q, new Tarefa { tipo = "meio", site = 'M', hold = h, porque = "info do meio e primeiro a girar pros dois lados" });
                else if (ordem == 0) Dar(q, new Tarefa { tipo = "âncora", site = z, hold = h, porque = $"seguro o {z}; não saio daqui por barulho do outro lado" });
                else Dar(q, new Tarefa { tipo = "apoio", site = z, hold = h, parceiro = ancora, porque = $"cruzo com o {ancora?.name}: se ele cair, a linha de quem matou é minha" });
            }
            tb.Diz($"setup {set.nome.Replace("novo: ", "")}{(pistolas ? " (" + m.compra[(int)team] + ")" : "")}{(MathF.Abs(lado) > 0.25f ? $" — eles vêm mais no {(lado > 0 ? 'A' : 'B')}" : "")}");
            if (Match.Auditar) m.Aud("IGL novo CT: setup", set.nome);
        }

        // ---------------------------------------------------------------- round: reagir quando a situação muda
        public void Tick(List<Agent> mine)
        {
            if (m.bombPlanted || mine.Count == 0) return;
            float t = m.t;
            foreach (var id in tarefa.Keys.ToList()) if (!m.agents[id].alive) tarefa.Remove(id);
            while (baixasTratadas < col.baixas.Count && t - col.baixas[baixasTratadas].t > 0.5f) Baixa(mine, t, col.baixas[baixasTratadas++]);   /* cada baixa é tratada, não só a última */
            if (CtVivo) Recolhe(mine, t);
            Reconcilia(mine);
            Execucao(mine, t);
            if (col.postura != posturaAnt) { string antes = posturaAnt; posturaAnt = col.postura; Postura(mine, t, antes); }
            if (t > proxPares && t > 20) { proxPares = t + 3; SemFuncao(mine, t); }
            if (CtVivo) Vida(mine, t);
        }

        // ---------------------------------------------------------------- vida antes do contato (ctVivoLigado)
        /// <summary>
        /// O CT profissional não fica estátua esperando o contato (Tools/pro/PROS_EM_PALAVRAS.md, 20 rounds lidos por situação: em 18 de 20
        /// no máximo 2 dos 5 ficam no mesmo lugar o round inteiro; alguém avança cedo em curta, rato, meio ou conector e volta; o jogador
        /// do meio faz ida e volta de informação). Aqui, enquanto o round está sem contato, cada jogador tem o SEU relógio (mania fixa) e,
        /// quando ele vence sem informação nova, faz a parte dele: o do meio muda de linha, o apoio vai buscar informação no acesso do bomb
        /// dele e volta, o âncora troca de ângulo dentro do bomb. Um de cada vez, e para tudo no primeiro contato. Não é sorteio: o mesmo
        /// jogador, na mesma situação, faz o mesmo; o que varia é o relógio e o gosto de cada um.
        /// </summary>
        public static bool CtVivo => TeamBrain.EXPF.Contains("ctVivoLigado");
        /// <summary>Entrou em contato ou caiu alguém: quem estava buscando informação volta na hora pro lugar dele e fica livre pro IGL
        /// (a viagem é pra round parado; com o bomb sendo atacado o parceiro dele está sozinho).</summary>
        void Recolhe(List<Agent> mine, float t)
        {
            if (!(col.momento == "contato" || col.momento == "execução" || t - col.ultimaBaixaT < 2f)) return;
            foreach (var a in mine)
            {
                var o = a.brain.order; if (o?.name == null || !o.name.StartsWith("info: ") || o.then == null) continue;
                ocupadoAte[a.id] = 0; a.brain.SetOrder(o.then); a.brain.Think("o round esquentou: largo a informação e volto pro meu lugar");
            }
        }
        readonly Dictionary<int, float> proxVida = new Dictionary<int, float>(); float vidaLivreEm; int vidaN;
        void Vida(List<Agent> mine, float t)
        {
            if (t < 16 || t < vidaLivreEm || col.postura != "setup" || col.momento == "contato" || col.momento == "execução" || col.Vantagem != 0) return;
            if (t - col.ultimoContatoT < 10 || t - col.ultimaBaixaT < 12) return;
            foreach (var a in mine)
            {
                var tf = T(a); if (tf == null || tf.hold == null || !Livre(a)) continue;
                if (!proxVida.TryGetValue(a.id, out float em)) { proxVida[a.id] = em = 18f + 14f * Mania(a, "ritmo de mexer") + 12f * Mania(a, "ritmo do round " + m.seed);   /* o jeito do jogador + o round: nao sai sempre no mesmo segundo */ }
                if (t < em || (a.pos - tf.hold.pos).Len > 170 || a.Speed > 20) continue;   /* só quem já está assentado no lugar dele */
                proxVida[a.id] = t + 24f + 20f * Mania(a, "ritmo de mexer " + (++vidaN));
                if (tf.tipo == "meio" && a.gunId != "awp")   /* o AWP fica na linha longa dele: não flutua */
                {   // ida e volta de informação: outra linha do meio que ninguém está olhando
                    var h = DoSite('M').Where(x => x != tf.hold && x != CT_Janelao && !mine.Any(o => o != a && ((o.brain.order.hold?.pos ?? o.pos) - x.pos).Len < 70))
                                       .OrderBy(x => NavGrid.PathLen(a.pos, x.pos) / 600f - Mania(a, x.name)).FirstOrDefault();
                    if (h == null) continue;
                    Dar(a, new Tarefa { tipo = "meio", site = 'M', hold = h, porque = $"{t - MathF.Max(col.ultimoContatoT, 0):0} s sem informação: troco de linha no meio pra ver outro lado" }, false);
                    tb.Diz($"{a.name} troca de linha no meio: {h.name}"); vidaLivreEm = t + 7; return;
                }
                if (tf.tipo == "apoio" && tf.site != 'M')
                {   // busca informação no acesso do bomb dele (conector pro A, rato pro B) e volta pro lugar: quem fica é o âncora
                    var info = tf.site == 'A' ? CT_Conector : CT_Rato;
                    if (mine.Any(o => o != a && ((o.brain.order.hold?.pos ?? o.pos) - info.pos).Len < 90)) continue;
                    float ida = NavGrid.PathLen(a.pos, info.pos); if (float.IsNaN(ida) || ida > 480f) continue;   /* só se o acesso é perto: ir longe é largar o âncora sozinho */
                    float fica = 5f + 3f * Mania(a, "quanto fico na info");
                    var volta = a.brain.order;
                    a.brain.SetOrder(new Order { name = $"info: {info.name}", hold = info, why = "sem informação do lado deles: vou ver o acesso e volto pro meu lugar", until = t + fica + ida / 55f, then = volta, paciente = true });
                    ocupadoAte[a.id] = t + fica + ida / 55f + 1f;
                    tb.Diz($"{a.name} dá uma olhada {Places.Na(info.name)} e volta"); vidaLivreEm = t + 9; return;
                }
                if (tf.tipo == "âncora")
                {   // o âncora não sai do bomb, mas não segura o round inteiro do mesmo pixel: troca pro outro ângulo do bomb dele
                    var outros = (tf.site == 'A' ? AncoraA : AncoraB).Where(x => x != tf.hold && !mine.Any(o => o != a && ((o.brain.order.hold?.pos ?? o.pos) - x.pos).Len < 70)).ToList();
                    if (outros.Count == 0 || Mania(a, "troco de ângulo " + m.seed) < 0.45f) continue;
                    var h = outros[0];
                    Dar(a, new Tarefa { tipo = "âncora", site = tf.site, hold = h, parceiro = tf.parceiro, porque = "mesmo ângulo há muito tempo: quem vier já sabe onde eu estou, troco" }, false);
                    vidaLivreEm = t + 7; return;
                }
            }
        }

        bool Ocupado(Agent a) => ocupadoAte.TryGetValue(a.id, out float ate) && m.t < ate;
        /// <summary>O Brain velho (ou outra peça) trocou a ordem do jogador pra outro lugar de segurar: a tarefa acompanha, pra o IGL não decidir com mapa errado.</summary>
        void Reconcilia(List<Agent> mine)
        {
            foreach (var a in mine)
            {
                var tf = T(a); var o = a.brain.order; if (tf == null || Ocupado(a) || o.hold == null || o.then != null || o.until < 900) continue;
                if (o.hold != tf.hold && (o.hold.pos - tf.hold.pos).Len > 60) { tf.hold = o.hold; string zz = Places.Zona(o.hold.pos); tf.site = zz == "A" ? 'A' : zz == "B" ? 'B' : 'M'; if (tf.site == 'M' && tf.tipo != "meio") tf.tipo = "meio"; }
            }
        }
        bool Livre(Agent a) => !Ocupado(a) && a.brain.target == null && !a.guardando && !a.brain.order.defuse && !a.brain.order.name.StartsWith("cortar") && !a.brain.order.name.Contains("C4 caído");
        Tarefa T(Agent a) => tarefa.TryGetValue(a.id, out var x) ? x : null;
        Hold VagaNo(char z, List<Agent> mine, Agent pra, V2? longeDe = null)
        {
            var ocup = mine.Where(x => x != pra).Select(x => x.brain.order.hold?.pos ?? x.pos).ToList();
            return DoSite(z).Where(h => !ocup.Any(o => (o - h.pos).Len < 70))
                            .OrderBy(h => (longeDe is V2 l ? -MathF.Min((h.pos - l).Len, 500) / 500f : 0) + NavGrid.PathLen(pra.pos, h.pos) / 1500f).FirstOrDefault();
        }

        /// <summary>Um colega caiu. Quem estava com ele tem função: trocar (o Brain tenta nos primeiros instantes) ou segurar a linha
        /// de quem matou, de cobertura — nunca ficar olhando pra outro lado, nunca entrar na mesma linha sozinho. O resto do time
        /// só se mexe se a conta do bomb mudou.</summary>
        void Baixa(List<Agent> mine, float t, (float t, V2 pos, Agent por, int zona) bx)
        {
            V2 caiu = bx.pos; var matador = bx.por; int z = bx.zona; char sz = z == 0 ? 'A' : z == 2 ? 'B' : 'M';
            if (matador == null || !matador.alive) return;
            V2 linha = m.intel[(int)team].contacts.TryGetValue(matador.id, out var c) && t - c.t < 4 ? c.pos : caiu;
            var perto = mine.Where(a => Livre(a) && NavGrid.PathLenAte(a.pos, caiu, 700) < 620).OrderBy(a => (a.pos - caiu).Len).ToList();
            var falas = new List<string>(); bool jogou = false;
            foreach (var a in perto.Take(2))
            {
                /* O jogador escolhe, pela situação DELE (PLANO §3.2: avaliação, não sorteio): segurar a linha de quem matou, de cobertura,
                   ou recuar pra dentro do bomb sabendo que tem gente ali. Pesa quantos ele acredita vindo, a vida, a arma, se tem colega
                   do lado e o jeito dele. Com molotov/HE na mão, joga na linha antes — atrasa a entrada e tira o tempo deles */
                float crDeles = MathF.Max(1f, col.vistosNa[z]);   /* segurar a linha ou recuar pesa quem está NA LINHA (visto); os ouvidos no lado decidem o giro, não isto */ bool colega = mine.Any(x => x != a && (x.pos - a.pos).Len < 400);
                bool armaBoa = !WeaponStats.Pistola(a.gunId) && a.gunId != "mp9" && a.gunId != "mac10";
                const float kd = 0.25f;   /* peso de cada inimigo a mais na linha */
                float vSegura = 0.6f + (armaBoa ? 0.15f : -0.15f) - kd * (crDeles - 1) - (a.hp < 50 ? 0.2f : 0) + (colega ? 0.2f : 0) + 0.25f * (Mania(a, "segura a linha") - 0.5f);
                float vRecua = 0.4f + kd * (crDeles - 1) + (a.hp < 50 ? 0.2f : 0) + (colega ? 0 : 0.15f) + 0.25f * (Mania(a, "recua pro bomb") - 0.5f);
                string gr = a.nades.Contains("molotov") ? "molotov" : a.nades.Contains("he") ? "he" : null;
                var antes = a.brain.order; Order o; var tfBase = T(a);
                if (vSegura >= vRecua || sz == 'M')
                {
                    /* segura do lugar DELE quando já está nele ou perto (a posição do setup é cobertura de verdade); senão, de onde está */
                    V2 daqui = tfBase?.hold != null && (tfBase.hold.pos - a.pos).Len < 150 ? tfBase.hold.pos : a.pos;
                    var h = new Hold("linha do " + matador.name, daqui, linha, caiu);
                    o = new Order { name = $"cruzar: linha do {matador.name}", hold = h, why = "meu parceiro caiu ali: quem matou tem que passar pela minha mira", until = t + 9, then = antes, paciente = true };
                    ocupadoAte[a.id] = t + 9;   /* temporário: a tarefa dele (âncora, apoio...) continua valendo e a ordem volta sozinha */
                    falas.Add($"{a.name} segura a linha do {matador.name}");
                }
                else
                {
                    var hRec = VagaNo(sz, mine, a, linha) ?? new Hold("fundo do " + sz, a.pos, linha);
                    var h = new Hold(hRec.name, hRec.pos, linha, caiu);
                    o = new Order { urgente = true, name = $"recuar: {h.name}", hold = h, why = $"não dá pra trocar e são {crDeles:0.#} ali: recuo pro bomb e espero eles entrarem", paciente = true };
                    tarefa[a.id] = new Tarefa { tipo = tfBase?.tipo ?? "apoio", site = sz, hold = h, parceiro = tfBase?.parceiro, desde = t, porque = "recuei pro fundo do bomb sabendo de onde vêm" };   /* o âncora continua âncora, só mais fundo */
                    falas.Add($"{a.name} recua pro {h.name}");
                }
                /* granada na entrada: um só joga, e só se o ponto está longe de mim e de qualquer colega (fogo/HE em cima do time não) */
                if (gr != null && !jogou && (a.pos - caiu).Len > 260 && !mine.Any(x => (x.pos - caiu).Len < 220))
                { jogou = true; o.nades.Add(new Lineup($"{gr} na linha do {matador.name}", gr, a.pos, caiu)); falas[falas.Count - 1] += $" e joga {gr} na entrada"; }
                a.brain.SetOrder(o);
            }
            if (falas.Count > 0) tb.Diz(string.Join("; ", falas));
            // o bomb ficou sem ninguém (ou com um só contra vários que a gente acredita lá)? um reforço, o mais perto que não é âncora de outro bomb
            if (sz == 'M') return;
            int ficaram = mine.Count(a => T(a)?.site == sz); float deles = col.delesNa[z];
            if (ficaram >= 2 || col.Vantagem <= -2) return;   // em desvantagem grande quem decide é a postura (recuar junto)
            var reforco = mine.Where(a => Livre(a) && T(a)?.site != sz && T(a)?.tipo != "âncora").OrderBy(a => NavGrid.PathLen(a.pos, Plant(sz))).FirstOrDefault();
            if (reforco == null) return;
            var hv = VagaNo(sz, mine, reforco, linha); if (hv == null) return;
            Dar(reforco, new Tarefa { tipo = "girar", site = sz, hold = hv, parceiro = perto.FirstOrDefault(), porque = $"caiu um no {sz}{(deles >= 1 ? " e eles estão lá" : "")}: fecho o bomb de onde não me pegam na mesma linha" });
            tb.Diz($"{reforco.name} gira pro {sz} ({hv.name})");
        }

        /// <summary>Informação de execução num bomb (2+ acreditados lá, ou o C4 visto): giram os que não são âncora; o âncora do outro
        /// lado só larga quando o C4 foi visto (pode ser fake).</summary>
        void Execucao(List<Agent> mine, float t)
        {
            var it = m.intel[(int)team];
            int zc = t - it.bombaT < 6 ? Coletivo.ZonaIdx(it.bombaPos) : -1; if (zc == 1) zc = -1;
            int z = zc >= 0 ? zc : col.delesNa[0] >= col.delesNa[2] ? 0 : 2; int oz = 2 - z; char sz = z == 0 ? 'A' : 'B';
            bool c4La = zc == z;
            bool exec = c4La || col.delesNa[z] >= col.ExecMin && col.delesNa[z] >= col.delesNa[oz] + 1f;
            if (!exec) return;
            if (giradoPara != sz)
            {
                var vao = mine.Where(a => Livre(a) && T(a)?.site != sz && T(a)?.tipo != "âncora").ToList(); int foram = 0;
                foreach (var a in vao) { var h = VagaNo(sz, mine, a); if (h == null) continue; foram++; Dar(a, new Tarefa { tipo = "girar", site = sz, hold = h, porque = $"{col.delesNa[z]:0.#} deles no {sz}{(c4La ? " com o C4" : "")}" }); }
                if (foram > 0) { giradoPara = sz; giroTotal = false; tb.Diz($"é {sz}{(c4La ? " (C4 visto)" : "")}: {string.Join(", ", vao.Select(a => a.name))} gira{(vao.Count > 1 ? "m" : "")}; o âncora do outro lado segura"); }
            }
            if (c4La && !giroTotal)
            {
                int foram = 0;
                foreach (var a in mine.Where(a => Livre(a) && T(a)?.site != sz).ToList()) { var h = VagaNo(sz, mine, a); if (h == null) continue; foram++; Dar(a, new Tarefa { tipo = "girar", site = sz, hold = h, porque = $"o C4 está no {sz}: aqui não planta" }); tb.Diz($"{a.name} larga o bomb: o C4 está no {sz}"); }
                if (foram > 0) giroTotal = true;
            }
        }

        /// <summary>A postura do time mudou (Coletivo): vantagem se segura em par e de cobertura; desvantagem grande recua junto.</summary>
        void Postura(List<Agent> mine, float t, string antes)
        {
            if (col.postura == "segurar a vantagem")
            {
                foreach (var a in mine)
                {   // quem estava fora do lugar tomando espaço volta pra tarefa: o relógio é nosso
                    var o = a.brain.order; if (!Livre(a) || !(o.zona || o.push)) continue;
                    var tf = T(a); if (tf?.hold == null) continue;
                    a.brain.SetOrder(new Order { name = $"{tf.tipo}: {tf.hold.name}", hold = tf.hold, why = $"{col.nos}x{col.eles}: seguro a vantagem, eles é que têm que vir", paciente = true });
                }
                tb.Diz($"{col.nos}x{col.eles}: segura, ninguém sai sozinho");
            }
            else if (col.postura == "recuar e jogar junto")
            {
                // junta todo mundo no bomb onde a gente acredita ter MENOS deles (se eles plantarem no outro, é retake junto)
                char sz = col.delesNa[0] <= col.delesNa[2] ? 'A' : 'B';
                foreach (var a in mine.Where(Livre)) { var h = VagaNo(sz, mine, a); if (h == null) continue; Dar(a, new Tarefa { tipo = "recuar", site = sz, hold = h, porque = $"{col.nos}x{col.eles}: sozinho não segura bomb — junto no {sz}" }); }
                tb.Diz($"{col.nos}x{col.eles}: recua e joga junto no {sz}");
            }
        }

        /// <summary>Ninguém fica sem função: quem está sem tarefa, ou sozinho sem troca num lugar onde não acreditamos em ninguém, vira
        /// par de quem mais precisa (o bomb com mais deles acreditados e menos nossos).</summary>
        void SemFuncao(List<Agent> mine, float t)
        {
            foreach (var a in mine)
            {
                if (!Livre(a)) continue; var tf = T(a);
                if (tf != null) continue;
                char sz = col.delesNa[0] - col.nossosNa[0] >= col.delesNa[2] - col.nossosNa[2] ? 'A' : 'B';
                var h = VagaNo(sz, mine, a); if (h == null) continue;
                var par = mine.Where(x => x != a && T(x)?.site == sz).OrderBy(x => (x.pos - h.pos).Len).FirstOrDefault();
                Dar(a, new Tarefa { tipo = "apoio", site = sz, hold = h, parceiro = par, porque = par != null ? $"fico de par com o {par.name} no {sz}" : $"o {sz} é onde falta gente" });
            }
        }
    }
}
