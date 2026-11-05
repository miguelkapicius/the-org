using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;

namespace CSM.Sim
{
    /// <summary>
    /// Detector do que um humano vendo o replay chamaria de estranho. Cada tipo nasceu de uma classe de reclamação real e
    /// mede o defeito, não o sintoma: parado junto do time esperando a hora não marca, parado longe de todo mundo marca.
    /// Grave = quase certo que é erro; Atenção = olhar o replay, pode ter motivo. Cada tipo marca uma vez por jogador
    /// (ou por time) no round, com o instante, pra ir direto ver (replay) e contar (relatório, com exemplos por semente).
    /// </summary>
    public class Suspeitas
    {
        public const string GRAVE = "grave", ATENCAO = "atenção";
        const int N = 10;
        readonly Match m;
        readonly HashSet<string> uma = new HashSet<string>();
        readonly List<float> maosC4 = new List<float>();
        readonly List<float>[] ordens = new List<float>[N];
        readonly float[] apanhou = new float[N]; readonly Agent[] quemAtirou = new Agent[N];
        readonly int[] miraParede = new int[N], miraLonge = new int[N], miraColega = new int[N], noFogo = new int[N], amontoado = new int[N];
        readonly List<(float t, int matador, int vitima)> mortes = new List<(float, int, int)>();
        readonly List<(float t, Team time)> caidos = new List<(float, Team)>();
        float c4Dist0 = -1, c4Indo = -99; bool reguaBase; int c4ParadoBase;
        float vantagemDesde = -1, c4CaiuEm = -1, plantDesde = -1, ultimoTiro; Agent plantador; int giros;
        public int pontos;   // o tamanho do problema do round: grave 3, atenção 1
        public int pontosErro, pontosProTambem;   // separado: o que o pro não faz x o que o pro também faz (comparar a frequência)
        /// <summary>
        /// Tipos que o pro também faz (régua pro nos 643 rounds de Mirage ou leitura do jogo): cada caso é jogo normal; o
        /// defeito, se houver, é a frequência — por isso vão pra nota de atenção, não pra de erros.
        /// </summary>
        public static readonly HashSet<string> PRO_TAMBEM = new HashSet<string>
        {
            "um segurou o time sozinho",   // pro: 45% dos rounds têm 3 mortes seguidas de um lado sem troca
            "time amontoado",              // stack pra entrar junto
            "giro desfeito", "vantagem sem plantar", "IGL trocou de bomb",
            "C4 na base",                  // pro: 24% dos rounds têm TR na base aos 35 s
            "tempo com TR vivo",           // tempo acabando: 7,6% dos rounds no real
            "C4 morreu no controle de mapa",   // portador caindo na disputa do meio/entradas antes de executar
        };
        /// <summary>Maior trecho sem morte depois dos 50 s em 90% dos rounds pro (régua gap.py): mais que isso é round parado.</summary>
        const float PARADO_PRO = 74f;

        public Suspeitas(Match m)
        {
            this.m = m;
            for (int i = 0; i < N; i++) { ordens[i] = new List<float>(); apanhou[i] = -99; }
        }

        void Marca(Agent a, string tipo, string grau, string texto)
        {
            if (!uma.Add(tipo + a.id)) return;
            Registra(a.id, tipo, grau, texto);
        }
        /// <summary>Defeito do time (não repete uma linha por jogador).</summary>
        void MarcaTime(Team time, string tipo, string grau, string texto)
        {
            if (!uma.Add(tipo + time)) return;
            int id = 0; foreach (var a in m.agents) if (a.team == time) { id = a.id; if (a.alive) break; }
            Registra(id, tipo, grau, texto);
        }
        void Registra(int id, string tipo, string grau, string texto)
        {
            bool pro = PRO_TAMBEM.Contains(tipo);
            m.suspeitas.Add((m.t, id, tipo, $"[{grau}] {(pro ? "(o pro também faz) " : "")}{texto}")); m.Ev($"SUSPEITA {tipo}/round");
            int v = grau == GRAVE ? 3 : 1;
            pontos += v; if (pro) pontosProTambem += v; else pontosErro += v;
            if (!pro && Match.Auditar)
            {   /* de onde vem a nota de erros: tipo e se quem errou está no pezinho (subiu, subindo ou dando) */
                Agent q = null; foreach (var x in m.agents) if (x.id == id) q = x;
                bool pz = q != null && (q.brain.boostEstado != 0 || q.dandoPezinho || q.subindoPezinho || NavGrid.SobrePilha(q.pos));
                for (int k = 0; k < v; k++) { m.Aud("nota de erros: tipo (peso)", tipo); m.Aud("nota de erros: de quem (peso)", pz ? "no pezinho" : "outro"); }
            }
            Match.GuardaExemplo(tipo, m.seedRound, m.t, texto);
        }

        // ---- eventos ----
        public void MudouMaoC4(Agent a)
        {
            float t = m.t; maosC4.Add(t);
            if (maosC4.Count(x => t - x < 10) >= 3) MarcaTime(Team.T, "C4 de mão em mão", GRAVE, $"o C4 trocou de mão 3 vezes em 10 s (agora com {a.name}): time gastando tempo sem levar ele a lugar nenhum");
        }
        public void CaiuC4(Agent v, int colegas, bool alguemNaFrente)
        {
            c4CaiuEm = m.t; c4Dist0 = -1;
            // não é "abrir o bomb": plantando, no meio do bando (rush, entrada junto); depois que o time já trocou tiro ali é só atenção
            bool plantando = (v.pos - m.PlantSpot).Len < 60;
            bool noBando = m.agents.Any(x => x != v && x.alive && x.team == v.team && (x.pos - v.pos).Len < 130);
            bool jaTeveContato = m.agents.Any(x => x != v && x.team == v.team && !x.alive && m.t - x.diedAt < 8);
            if (colegas >= 2 && !alguemNaFrente && !m.bombPlanted && !plantando && !noBando)
            {
                // "abrir o bomb" é na entrada: o time já executando, ou o portador na zona do bomb. Antes disso é disputa de
                // mapa — o portador morrer no meio aos 15 s acontece no pro também (vai pra nota de atenção)
                var ig = m.igl[(int)Team.T];
                bool entrando = ig.Executou || Places.Zona(v.pos) == m.planSite.ToString() || ig.EntraEm > 0 && m.t > ig.EntraEm - 5 || TeamBrain.EXP.Contains("c4AbriuVelho");
                if (entrando) Marca(v, "C4 abriu o bomb", jaTeveContato ? ATENCAO : GRAVE, $"{v.name} morreu com o C4 na frente de {colegas} colegas em {Places.Region(v.pos)}: o C4 entra atrás de quem abre");
                else Marca(v, "C4 morreu no controle de mapa", ATENCAO, $"{v.name} morreu com o C4 em {Places.Region(v.pos)} aos {m.t:0} s, antes da entrada, sem ninguém mais perto do bomb");
            }
        }
        public void Ordem(Agent a, string nome)
        {
            var l = ordens[a.id]; float t = m.t;
            if (l.Count > 0 && t - l[l.Count - 1] < Match.DT * 0.5f) return;   // várias no mesmo instante: ele só executa a última
            l.Add(t); l.RemoveAll(x => t - x > 3);
            if (l.Count >= 4) Marca(a, "ordens trocando", ATENCAO, $"{a.name} recebeu {l.Count} ordens em 3 s (última: {nome}): não dá pra executar nada assim");
        }
        public void Dano(Agent v, Agent por) { if (por == null) return; apanhou[v.id] = m.t; quemAtirou[v.id] = por; ultimoTiro = m.t; }
        public void Morte(Agent v, Agent por)
        {
            ultimoTiro = m.t; caidos.Add((m.t, v.team));
            if (caidos.Count == 1 && Match.Auditar)
            {
                string on = v.brain.order.name; int sp = on.IndexOf(':'); if (sp > 0) on = on.Substring(0, sp);
                m.Aud("1º abate: quem caiu (antes dos 20 s)", m.t < 20 ? $"{(v.IsT ? "TR" : "CT")} {Places.Region(v.pos)} · {on}" : "depois dos 20 s");
                if (m.t < 20 && por != null && por.team != v.team)
                {   /* quem forçou o contato cedo: o matador (lado, lugar, arma, estado) e se a vítima via ele antes */
                    m.Aud("1º abate cedo: matador", $"{(por.IsT ? "TR" : "CT")} {Places.Region(por.pos)} · {por.gunId} · {Estado(por)} · {por.brain.order.name.Split(':')[0]}");
                    m.Aud("1º abate cedo: lugar do duelo", $"{Places.Region(por.pos)} × {Places.Region(v.pos)} ({(por.pos - v.pos).Len:0})");
                }
            }
            if (por == null) return;
            if (v.guardando && (m.t - apanhou[v.id]) > 1.2f) Marca(v, "morreu guardando", ATENCAO, $"{v.name} morreu guardando a arma em {Places.Region(v.pos)} pro {por.name}");
            mortes.Add((m.t, por.id, v.id));
            if (Match.Auditar && por.team != v.team) { m.Aud("duelo: postura de quem matou", Postura(por, v)); m.Aud("duelo: postura de quem morreu", Postura(v, por)); }
            if (Match.Auditar && por.team != v.team) m.Aud("duelo: estado de quem matou x de quem morreu", Estado(por) + " mata " + Estado(v));
            if (Match.Auditar && por.team != v.team && (por.gunId == "awp" || v.gunId == "awp"))
            {   /* AWP no duelo por distância: quantos ela ganha e perde (o nerf de longe, awpLonge) */
                float dd = (por.pos - v.pos).Len; string fx = dd < 150 ? "a perto <150" : dd < 300 ? "b médio 150-300" : dd < 500 ? "c longe 300-500" : "d muito longe 500+";
                m.Aud("AWP: duelo por distância", fx + (por.gunId == "awp" ? " · AWP venceu" : " · AWP perdeu"));
            }
            if (Match.Auditar && por.team != v.team) { if (por.brain.AgachouNoPeek) m.Aud("agachou no peek: resultado do duelo", "matou"); if (v.brain.AgachouNoPeek) m.Aud("agachou no peek: resultado do duelo", "morreu"); }
            int seguidas = mortes.Count(x => x.matador == por.id && m.t - x.t < 25);
            bool semTroca = por.alive;
            if (seguidas >= 3 && semTroca && por.team != v.team) Marca(por, "um segurou o time sozinho", ATENCAO, $"{por.name} matou {seguidas} em 25 s em {Places.Region(por.pos)} sem levar troca: entraram um de cada vez");
        }
        /// <summary>
        /// A mesma conta nos rounds pro (scratchpad regua_suspeitas.py sobre EstrategiasDados): só com a hora das mortes,
        /// que é o que os dados pro têm. Diz se "round parado" e "um segurou sozinho" acontecem mais que no jogo de verdade.
        /// </summary>
        public void Regua()
        {
            float fim = m.bombPlanted ? m.plantedAt : m.t;
            bool parado = false;
            for (float s = 50; s <= MathF.Min(fim, Match.ROUND - 8); s += 1)
            {
                int trVivos = 5 - caidos.Count(x => x.time == Team.T && x.t <= s);
                if (trVivos >= 3 && !caidos.Any(x => x.t > s - 25 && x.t <= s)) { parado = true; break; }
            }
            m.Aud("régua pro: 25 s sem morte depois dos 50 s (3+ TR, sem plant)", parado ? "sim" : "não");
            bool seguidas = false;
            for (int i = 0; i + 2 < caidos.Count; i++)
                if (caidos[i].time == caidos[i + 1].time && caidos[i].time == caidos[i + 2].time && caidos[i + 2].t - caidos[i].t <= 25) seguidas = true;
            m.Aud("régua pro: 3 mortes seguidas de um lado em 25 s sem troca", seguidas ? "sim" : "não");
        }
        /// <summary>Como ele estava pro outro: em cima de caixa, só a cabeça aparecendo (caixa tapa o corpo), agachado ou em pé.</summary>
        /// <summary>Auditoria do duelo: segurando (com posição) parado ou se mexendo, ou andando/parado sem posição.</summary>
        static string Estado(Agent a) => a.brain.Segurando ? (a.Speed < a.MaxSpeed * 0.2f ? "segurando parado" : "segurando se mexendo") : a.Speed < a.MaxSpeed * 0.2f ? "parado" : "andando";
        static string Postura(Agent a, Agent outro)
        {
            var (cx, cy) = NavGrid.CellOf(a.pos);
            if (NavGrid.onTop[cx, cy]) return "em cima de caixa";
            // tronco dele (meio do corpo) visto do olho do outro: tapado = só a cabeça aparecia
            float tronco = a.z + 13f * (1f - 0.28f * a.duck);
            bool corpo = MapData.LosWalk(a.pos, tronco, outro.pos, outro.z + outro.Eye, 0f);
            return !corpo ? "só a cabeça (caixa na frente)" : a.duck > 0.5f ? "agachado" : "em pé";
        }
        public void InicioPlant(Agent a) { plantDesde = m.t; plantador = a; }
        public void Giro(Team time, char site, bool desfez)
        {
            giros++;
            if (desfez) MarcaTime(time, "giro desfeito", ATENCAO, $"CT girou e voltou atrás pro {site} sem ter visto nada novo");
            if (giros >= 4) MarcaTime(time, "gira demais", ATENCAO, $"CT já girou {giros} vezes neste round");
        }
        public void LeituraMudou(char site) => MarcaTime(Team.T, "IGL trocou de bomb", ATENCAO, $"IGL do TR mudou de ideia pro {site}: a estimativa virou, não a informação");

        // ---- a cada segundo ----
        public void Passo()
        {
            if (m.over) return;
            float t = m.t;
            if (!reguaBase && t >= 35)
            {   // régua pro (scratchpad base35.py): algum TR vivo na base aos 35 s, nos rounds sem plant até ali
                reguaBase = true;
                if (!m.bombPlanted)
                {
                    bool tr = m.agents.Any(x => x.alive && x.IsT && Places.Region(x.pos) == "base TR");
                    bool c4 = m.agents.Any(x => x.alive && x.hasBomb && Places.Region(x.pos) == "base TR");
                    m.Aud("régua pro: TR vivo na base aos 35 s (sem plant)", tr ? (c4 ? "sim, e o C4 também" : "sim, sem o C4") : "não");
                }
            }
            Vantagem(t); TimeC4(t); Parado(t); Plant(t); Retake(t);
            foreach (var a in m.agents)
            {
                if (!a.alive) continue;
                Mira(a, t);
                Inventario(a);
                Apanhando(a, t);
                var b = a.brain;
                noFogo[a.id] = m.FireAt(a.pos) && b.action == null && b.target == null && a.Speed < 15 ? noFogo[a.id] + 1 : 0;
                if (noFogo[a.id] >= 2) Marca(a, "parado no fogo", GRAVE, $"{a.name} parado na molotov há {noFogo[a.id]} s em {Places.Region(a.pos)}");
                // segurando ângulo (as micro-ações do segurar: quieto, pausa, jiggle...), esperando a vez/o colega/a smoke ou o
                // C4 esperando quem abre não é "sem posição" — ele está jogando; o defeito é parado sem fazer nenhuma dessas
                string f = b.fase ?? "";
                bool jogando = !TeamBrain.EXP.Contains("semPosicaoVelho") && (f.StartsWith("quieto") || f.StartsWith("pausa") || f.StartsWith("esperando") || f.StartsWith("segurando")
                    || f == "jiggle" || f == "wide" || f == "ajeitar" || f == "trocar de ângulo" || f == "bang e peek" || f.StartsWith("C4 atrás")
                    || f == "dando pezinho" || f == "indo pro pezinho" || f == "subindo no pezinho");   /* boost montando: é jogada */
                if (a.paradoHa > 10 && !jogando && b.order.hold == null && !b.order.malote && t >= b.order.notBefore && !a.guardando && !b.order.name.StartsWith("girando") && !b.order.name.StartsWith("juntar") && !b.order.name.StartsWith("levando"))
                {
                    string on = b.order.name; int sp = on.IndexOf(':'); if (sp > 0) on = on.Substring(0, sp);
                    if (!uma.Contains("parado sem posição" + a.id)) m.Aud("parado sem posição: fase · ordem", $"{b.fase} · {on}{(b.order.waitSmoke ? " (esperando smoke)" : "")}");
                    Marca(a, "parado sem posição", ATENCAO, $"{a.name} parado há {a.paradoHa:0} s sem segurar nada: {b.order.name} ({b.fase})");
                }
                if (a.IsT && a.team == Team.T && t > 15 && !m.bombPlanted && !b.order.push)
                {
                    int perto = 0; foreach (var x in m.agents) if (x.alive && x.team == a.team && (x.pos - a.pos).Len < 70) perto++;
                    // junto esperando a entrada (a vez, a smoke, a hora do plano, o execute) é stack de pro, não amontoado
                    var ig = m.igl[(int)Team.T];
                    bool esperandoEntrada = !TeamBrain.EXP.Contains("amontoadoVelho") && (b.fase.StartsWith("esperando") || b.order.waitSmoke || t < b.order.notBefore
                        || ig.Executou || ig.EntraEm > 0 && ig.EntraEm - t < 15 || b.order.name.StartsWith("juntando") || b.order.name.StartsWith("juntar"));
                    amontoado[a.id] = perto >= 3 && a.Speed < 30 && !esperandoEntrada ? amontoado[a.id] + 1 : 0;
                    if (amontoado[a.id] >= 5) MarcaTime(Team.T, "time amontoado", ATENCAO, $"{perto} TR colados há 5 s em {Places.Region(a.pos)}, sem ninguém pegando espaço");
                }
                if (a.guardando && m.bombPlanted && m.BombLeft < 5 && (a.pos - m.bombPos).Len < Match.RAIO_BOMB)
                    Marca(a, "guardou no raio", GRAVE, $"{a.name} foi guardar a arma dentro do raio do bomb");
            }
        }

        /// <summary>
        /// Inventário do CS2: até 4 granadas, até 2 flashes e 1 de cada outra (smoke, molotov, HE); uma primária e uma
        /// pistola (mais faca e C4). Fora disso é bug de compra/drop/pegar — grave.
        /// </summary>
        void Inventario(Agent a)
        {
            int fl = 0; bool dup = false; var visto = new HashSet<string>();
            foreach (var n in a.nades) { if (n == "flash") fl++; else if (!visto.Add(n)) dup = true; }
            string erro = a.nades.Count > 4 ? $"{a.nades.Count} granadas" : fl > 2 ? $"{fl} flashes" : dup ? "granada repetida (" + string.Join(",", a.nades) + ")" : null;
            if (erro == null && a.outraId != null)
            {
                bool p1 = WeaponStats.Pistola(a.gunId), p2 = WeaponStats.Pistola(a.outraId);
                if (p1 == p2) erro = p1 ? $"duas pistolas ({a.gunId}, {a.outraId})" : $"duas primárias ({a.gunId}, {a.outraId})";
            }
            if (erro != null) Marca(a, "inventário fora da regra", GRAVE, $"{a.name} com {erro}");
        }

        void Vantagem(float t)
        {
            bool vant = !m.bombPlanted && m.Alive(Team.T) - m.Alive(Team.CT) >= 2;
            if (!vant) { vantagemDesde = -1; return; }
            if (vantagemDesde < 0) vantagemDesde = t;
            Agent port = null; foreach (var a in m.agents) if (a.alive && a.hasBomb) port = a;
            // o portador indo pro bomb é o jogo acontecendo; só vale se ninguém está levando ou já passou demais da conta
            bool levando = port != null && port.brain.order.plant && port.Speed > 30;
            float limite = levando ? 40 : 20;
            if (t - vantagemDesde > limite)
                MarcaTime(Team.T, "vantagem sem plantar", ATENCAO, $"{m.Alive(Team.T)}x{m.Alive(Team.CT)} há {t - vantagemDesde:0} s sem plantar; C4 {(port != null ? $"com {port.name} ({port.brain.order.name})" : "no chão")}");
        }

        void TimeC4(float t)
        {
            if (m.bombPlanted) return;
            int vivos = m.Alive(Team.T);
            foreach (var a in m.agents)
            {
                if (!a.alive || !a.hasBomb) continue;
                var b = a.brain; float aqui = a.c4Desde >= 0 ? t - a.c4Desde : 0;
                bool comTime = false;
                foreach (var x in m.agents) if (x != a && x.alive && x.team == a.team && (x.pos - a.pos).Len < 450) comTime = true;
                if (aqui > 10 && !b.order.plant && !comTime) Marca(a, "C4 parado longe do time", GRAVE, $"{a.name} há {aqui:0} s no mesmo lugar com o C4, longe de todo mundo ({b.fase}) em {Places.Region(a.pos)}");
                // atravessar o spawn rodando de um lado pro outro é jogo (pro: 24% dos rounds tem TR na base aos 35 s);
                // erro é o C4 parado lá, ou ainda lá depois dos 45 s
                bool naBaseC4 = Places.Region(a.pos) == "base TR";
                c4ParadoBase = naBaseC4 && a.Speed < 15 ? c4ParadoBase + 1 : 0;
                if (t > 35 && vivos >= 2 && !b.order.plant && naBaseC4 && (c4ParadoBase >= 3 || t > 45 || TeamBrain.EXP.Contains("c4BaseDetVelho"))) Marca(a, "C4 na base", GRAVE, $"C4 ainda na base TR aos {t:0} s com {a.name} ({b.order.name}, {b.fase})");
            }
            if (m.bombDropped)
            {
                if (c4CaiuEm < 0) c4CaiuEm = t;
                if (c4Dist0 < 0) { c4Dist0 = 0; foreach (var a in m.agents) if (a.alive && a.IsT) { float pl = NavGrid.PathLen(a.pos, m.droppedBomb); if (pl > 0 && !float.IsInfinity(pl)) c4Dist0 = c4Dist0 == 0 ? pl : MathF.Min(c4Dist0, pl); } }
                bool base_ = Places.Region(m.droppedBomb) == "base TR";
                bool vigiado = false; foreach (var c in m.intel[(int)Team.T].contacts.Values) if (t - c.t < 10 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 650) vigiado = true;
                // o prazo é o de quem estava mais perto ir andando (shift) até ele, com folga — longe demora, e tudo bem
                float prazo = MathF.Max(15, c4Dist0 / (MapCheck.RUN * 0.45f) + 8);
                // esquecido é ninguém ir buscar: alguém indo (ordem de pegar), alguém que morreu tentando há pouco, ou o time
                // esperando CT segurando ele (no pro o C4 na mira do CT também fica no chão) não é esquecer
                foreach (var a in m.agents) if (a.alive && a.IsT && a.brain.order.name.StartsWith("pegar o bomb")) c4Indo = t;
                foreach (var a in m.agents) if (!a.alive && a.IsT && t - a.diedAt < 1.1f && (a.pos - m.droppedBomb).Len < 300) c4Indo = t;
                bool disputado = !TeamBrain.EXP.Contains("c4EsquecidoVelho") && (t - c4Indo < 15 || m.intel[(int)Team.T].Esperados(m.droppedBomb, 400) > 0.6f);
                if (t - c4CaiuEm > prazo && vivos >= 2 && m.RoundLeft > 10 && !vigiado && !disputado && !(m.bombGuardado && t < 50)) MarcaTime(Team.T, "C4 esquecido no chão", GRAVE, $"C4 há {t - c4CaiuEm:0} s no chão em {Places.Region(m.droppedBomb)} com {vivos} TR vivos e ninguém indo buscar");
                if (t > 35 && base_ && vivos >= 2) MarcaTime(Team.T, "C4 na base", GRAVE, $"C4 no chão da base TR aos {t:0} s");
                if (vivos == 1 && m.RoundLeft < 20)
                    foreach (var a in m.agents)
                        if (a.alive && a.IsT && a.brain.order.name.StartsWith("pegar o bomb") && m.intel[(int)Team.T].contacts.Values.Any(c => t - c.t < 8 && m.agents[c.id].alive && (c.pos - m.droppedBomb).Len < 500))
                            Marca(a, "sozinho no C4 vigiado", ATENCAO, $"{a.name} sozinho indo no C4 vigiado com {m.RoundLeft:0} s: morre sem chance de plantar");
            }
            else { c4CaiuEm = -1; c4Dist0 = -1; }
        }

        void Parado(float t)
        {
            if (t < 50 || m.bombPlanted || m.Alive(Team.T) < 3 || m.RoundLeft < 8) return;
            if (!TeamBrain.EXP.Contains("paradoVelho"))
            {   // régua pro: sem ninguém morrer por mais tempo que em 90% dos rounds pro (contando da última morte)
                float ultMorte = caidos.Count > 0 ? caidos[caidos.Count - 1].t : 0;
                if (t - ultMorte >= PARADO_PRO) MarcaTime(Team.T, "round parado", ATENCAO, $"{t - ultMorte:0} s sem ninguém morrer com {m.Alive(Team.T)} TR vivos (90% dos rounds pro têm menos que {PARADO_PRO:0} s)");
                return;
            }
            if (t - ultimoTiro < 25) return;
            float ultimo = 0;
            foreach (var it in m.intel) foreach (var c in it.contacts.Values) ultimo = MathF.Max(ultimo, c.t);
            if (t - ultimo > 20) MarcaTime(Team.T, "round parado", ATENCAO, $"{t - MathF.Max(ultimo, ultimoTiro):0} s sem tiro nem contato com {m.Alive(Team.T)} TR vivos");
        }

        void Plant(float t)
        {
            if (plantDesde < 0 || m.bombPlanted || plantador == null || !plantador.alive || plantador.brain.action != "plantar") { plantDesde = -1; return; }
            float resta = plantDesde + Match.PLANT - t;
            foreach (var a in m.agents)
            {
                if (!a.alive || a.IsT || a.guardando || a.brain.target != null) continue;
                float chega = NavGrid.PathLen(a.pos, plantador.pos) / MathF.Max(a.MaxSpeed, 1);
                if (chega < resta + 0.6f && a.Speed < 30) Marca(a, "viu o plant passar", GRAVE, $"{a.name} a {chega:0.0} s do {plantador.name} plantando ({resta:0.0} s de plant) e ficou parado");
            }
        }

        void Retake(float t)
        {
            if (!m.bombPlanted || m.over) return;
            Agent melhor = null; float eta = float.MaxValue; bool algumIndo = false; int vivos = 0;
            foreach (var a in m.agents)
            {
                if (!a.alive || a.IsT || a.guardando) continue;
                vivos++;
                float e = NavGrid.PathLen(a.pos, m.bombPos) / MathF.Max(a.MaxSpeed, 1) + Match.DefuseTime(a);
                if (e < eta) { eta = e; melhor = a; }
                if (a.brain.order.defuse || a.Speed > 60) algumIndo = true;
            }
            if (melhor != null && vivos >= 2 && !algumIndo && m.BombLeft < eta + 4 && m.BombLeft > eta)
                MarcaTime(Team.CT, "retake não saiu", GRAVE, $"{vivos} CT vivos, {m.BombLeft:0} s de bomb e {eta:0} s pra desarmar: era a última hora e ninguém foi");
        }

        void Mira(Agent a, float t)
        {
            var b = a.brain;
            bool livre = b.target == null && b.action == null;
            V2 olho = V2.Ang(a.aim); var it = m.intel[(int)a.team];
            bool perto = false, longe = false;
            if (livre)
                foreach (var c in it.contacts.Values)
                {
                    if (t - c.t > 4 || !m.agents[c.id].alive) continue;
                    V2 d = c.pos - a.pos; float dl = d.Len;
                    if (t - c.t < 2 && dl < 350) perto = true;
                    // olhando uma quina na mesma direção é só coincidência; o defeito é a mira colada numa parede perto
                    if (dl > 450 && a.Speed < 30 && MathF.Abs(MathX.AngDiff(d.Atan2, a.aim)) < 0.12f && MapData.Raio(a.pos, a.z, olho, 100, a.Eye) < 95) longe = true;
                }
            bool parede = livre && perto && !MapData.LosWalk(a.pos, a.z, a.pos + olho * 60, a.z, a.Eye);
            miraParede[a.id] = parede ? miraParede[a.id] + 1 : 0;
            if (miraParede[a.id] >= 3) Marca(a, "mira na parede com inimigo conhecido", ATENCAO, $"{a.name} em {Places.Region(a.pos)} mirando a parede há 3 s com inimigo visto perto ({b.fase})");
            miraLonge[a.id] = longe ? miraLonge[a.id] + 1 : 0;
            if (miraLonge[a.id] >= 3) Marca(a, "mirando através do mapa", ATENCAO, $"{a.name} parado em {Places.Region(a.pos)} há 3 s mirando por dentro da parede em quem está longe ({b.order.name})");
            bool colega = false;
            if (livre && a.Speed < 30)
                foreach (var x in m.agents)
                {
                    if (x == a || !x.alive || x.team != a.team) continue;
                    V2 d = x.pos - a.pos; float dl = d.Len;
                    if (dl > 40 && dl < 200 && MathF.Abs(MathX.AngDiff(d.Atan2, a.aim)) < 0.12f && MapData.LosWalk(a.pos, a.z, x.pos, x.z, a.Eye)) colega = true;
                }
            miraColega[a.id] = colega ? miraColega[a.id] + 1 : 0;
            if (miraColega[a.id] == 5) m.Aud("olhando pro colega: quem e fase", $"{(a.IsT ? "TR" : "CT")} {a.brain.fase} · {(a.brain.order.name.StartsWith("plano") ? "plano" : a.brain.order.name)}{(a.brain.order.waitSmoke ? " (espera smoke)" : "")}");
            if (miraColega[a.id] >= 5) Marca(a, "olhando pro colega", ATENCAO, $"{a.name} há 5 s com a mira nas costas de um colega em {Places.Region(a.pos)} ({a.brain.fase}, {a.brain.thought})");
        }

        void Apanhando(Agent a, float t)
        {
            float ha = t - apanhou[a.id];
            if (ha < 1 || ha > 2.2f) return;
            var b = a.brain;
            if (b.target != null || b.action != null || a.Speed > 30 || a.Blind(t)) return;
            var por = quemAtirou[a.id];   // já mirando pra quem atirou é reação, mesmo sem ele à vista
            if (por == null || por.team == a.team && !TeamBrain.EXP.Contains("tiroAmigoConta")) return;   // bala de colega (fogo amigo): não tem pra onde reagir
            if (MathF.Abs(MathX.AngDiff((por.pos - a.pos).Atan2, a.aim)) < 0.8f) return;
            if (!uma.Contains("levou tiro e não reagiu" + a.id)) m.Aud("levou tiro e não reagiu: fase · ainda reagindo · viu o atirador há", $"{b.fase} · {(b.Reagindo ? "sim" : "não")} · {(t - b.ViuEm(por) < 3 ? "<3 s" : "não viu")}");
            if (a.guardando) Marca(a, "guardando sob fogo", GRAVE, $"{a.name} levou tiro guardando a arma e ficou parado em {Places.Region(a.pos)}");
            else Marca(a, "levou tiro e não reagiu", GRAVE, $"{a.name} levou tiro em {Places.Region(a.pos)} e {ha:0.0} s depois nem virou nem saiu ({b.fase})");
        }

        // ---- fim do round ----
        public void Fim(string motivo)
        {
            if (motivo != null && motivo.Contains("tempo") && m.Alive(Team.T) >= 2)
            {
                int gu = 0; foreach (var a in m.agents) if (a.alive && a.IsT && a.guardando) gu++;
                if (gu < m.Alive(Team.T)) MarcaTime(Team.T, "tempo com TR vivo", GRAVE, $"tempo acabou com {m.Alive(Team.T)} TR vivos, {gu} guardando");
            }
            m.Ev("rounds com suspeita grave", m.suspeitas.Any(s => s.texto.StartsWith("[grave]")) ? 1 : 0);
            m.Ev("pontos de suspeita/round", pontos);
            m.Ev("nota de erros (o pro não faz)/round", pontosErro);
            m.Ev("nota de atenção (o pro também faz)/round", pontosProTambem);
        }
    }
}
