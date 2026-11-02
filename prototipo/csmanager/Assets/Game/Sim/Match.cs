using System;
using System.Collections.Generic;
using System.Linq;
using CSM.Map;
using CSM.View;

namespace CSM.Sim
{
    /// <summary>
    /// Um round: 10 bots (5 TR, 5 CT), relógio de 1:55, bomb (plant 3,2 s, explode em 40 s, defuse 5 s com kit),
    /// granadas voando (smoke/molotov/flash), sons (passo correndo ~260 de alcance, tiro o mapa todo), visão com
    /// smoke bloqueando, dano e mortes (killfeed, trocas), e as condições de vitória. Passo de 1/32 s.
    /// </summary>
    public class Match
    {
        public const float DT = 1f / 32, ROUND = 115f, BOMB = 40f, PLANT = 3.2f;
        public float t;
        public readonly List<Agent> agents = new List<Agent>();
        public readonly Intel[] intel = new Intel[2];
        public readonly TeamBrain[] igl = new TeamBrain[2];
        public readonly Rng rng;
        public readonly List<SmokeVolume> smokes = new List<SmokeVolume>();
        public readonly List<FireArea> fires = new List<FireArea>();
        public readonly List<(V2 p, float z, float t)> flashes = new List<(V2, float, float)>();
        public readonly List<(V2 p, float z, float t)> hes = new List<(V2, float, float)>();   // explosões de HE (desenho/replay)
        /// <summary>HE: raio do dano, sino do dano (CS2 350 u ~ nosso 200: boneco 18 x 32) e o raio da bolha na smoke.</summary>
        public const float HE_RAIO = 170f, HE_SIGMA = 52f, HE_FURO = 46f;
        public class Nade
        {
            public Places.Lineup l; public Agent by; public V2 from; public float z0, t0, t1;   // t1 = quando deve estourar (estimativa pros bots)
            // física: posição e velocidade de verdade — quica na parede, quica e rola no chão, cai da borda
            public float x, y, z, vx, vy, vz; public bool noChao, tocouChao, noAr; public int quiques, paredes; public float paradoDesde = -1;
            public V2 P => new V2(x, y);
        }
        // CS2: flash/HE estouram no tempo (no ar, se for o caso: pop flash), smoke quando para, molotov ao bater no chão
        // (ou estoura no ar sem fogo se o tempo acabar voando)
        public const float GN = 400f, QUICA_PAREDE = 0.45f, QUICA_CHAO = 0.3f, ATRITO_QUIQUE = 0.5f, ROLAR = 350f;
        public const float FUSE_FLASH = 1.55f, FUSE_HE = 1.6f, FUSE_MOLOTOV = 2.0f;
        public readonly List<Nade> flying = new List<Nade>();
        public class ShotFx { public V2 a, b; public float za, zb, t; public bool hit, hs; public Team team; }
        public readonly List<ShotFx> shots = new List<ShotFx>();
        public readonly List<(float t, string s)> feed = new List<(float, string)>();
        public readonly List<string> log = new List<string>();
        public readonly List<(float t, V2 p, Team who, string kind)> sounds = new List<(float, V2, Team, string)>();
        // bomb
        public bool bombPlanted, bombDropped, defuserActive, exploded, defused;
        public V2 bombPos, droppedBomb; public float bombZ, plantedAt; public char planSite = 'A'; Agent planter, defuser;
        public string winner, reason; public bool over;
        public Agent firstKill, firstDeath; public float firstKillT = -1, firstSightT = -1;
        public readonly Pesos[] pesos = new Pesos[2];
        public readonly HashSet<string> utilUsada = new HashSet<string>();   // lineups que o time já gastou neste round
        /// <summary>Arma no chão (de quem morreu ou de quem trocou): qualquer um pode pegar.</summary>
        public class Drop { public string gun; public V2 pos; public float z; public WeaponStats.Gun estado; }
        public readonly List<Drop> drops = new List<Drop>();
        /// <summary>Granada no chão dropada pra um colega (ele vai lá pegar; depois de 3 s qualquer um do time pega).</summary>
        public class ItemChao { public string nade; public V2 pos; public float z; public Team time; public Agent para, de; public float t; }
        public readonly List<ItemChao> itensChao = new List<ItemChao>();
        /// <summary>O C4 foi deixado de propósito num lugar seguro (o portador foi buscar espaço): ninguém corre pegar agora.</summary>
        public bool bombGuardado;

        V2 Jogo(Agent de, V2 alvo) { V2 d = alvo - de.pos; float l = d.Len; return NavGrid.Snap(l < 1 ? de.pos : de.pos + d / l * MathF.Min(28, l)); }
        /// <summary>Dropa uma granada pro colega (cai na frente, na direção dele).</summary>
        public bool DropaGranada(Agent de, Agent para, string nade)
        {
            if (!de.nades.Remove(nade)) return false;
            V2 p = Jogo(de, para.pos);
            itensChao.Add(new ItemChao { nade = nade, pos = p, z = NavGrid.ZAt(p), time = de.team, para = para, de = de, t = t });
            de.dropou++; Log($"{de.name} dropou {nade} pro {para.name}"); de.brain.Think($"dropei {nade} pro {para.name}"); para.brain.Think($"o {de.name} dropou {nade} pra mim");
            return true;
        }
        /// <summary>Deixa o C4 no chão de propósito (lugar seguro) pra ir buscar espaço sem ele.</summary>
        public void GuardaBomb(Agent a, V2? onde = null)
        {
            if (!a.hasBomb) return;
            a.hasBomb = false; bombDropped = true; bombGuardado = true; droppedBomb = NavGrid.Snap(onde ?? a.pos); if (float.IsInfinity(NavGrid.PathLen(droppedBomb, Places.PlantA))) droppedBomb = NavGrid.Snap(a.pos);   // nunca num ponto sem caminho
            guardouC4 = a; guardouC4T = t; c4Guardado++;
            a.dropou++; Log($"{a.name} deixou o bomb em {Places.Region(droppedBomb)} (seguro) pra buscar espaço");
        }
        Agent guardouC4; float guardouC4T; public int c4Guardado, c4PegoNaHora;
        static int Flashes(Agent a) => a.nades.Count(n => n == "flash");
        public static bool CabeGranada(Agent a, string nade) => a.nades.Count < 4 && (nade != "flash" ? !a.nades.Contains(nade) : Flashes(a) < 2);
        /// <summary>Cada acerto (anatomia do HS): distância, onde pegou, se a mira já estava ali, alvo andando, bala do spray.</summary>
        public struct Acerto { public float dist, flick, vAlvo, vAtirador; public int parte, spray, hpAntes; public bool matou, atiradorTR, vitimaViu, foraDaVisao, smoke, cego; public string arma, faseVitima, faseAtirador, situAtirador, situVitima, holdAtirador, holdVitima; public V2 posAtirador, posVitima; public int seed; public string matador, vitima; public float t, vitViuHa, matViuHa; public bool plantado, vitimaAtirou, vitAndando; }
        public readonly List<Acerto> acertos = new List<Acerto>();
        public readonly int seed;

        /// <summary>Troca a arma da mão pela do chão (a dele fica no lugar).</summary>
        public void Pegar(Agent a, Drop d)
        {
            if (!drops.Remove(d)) return;
            drops.Add(new Drop { gun = a.gunId, pos = a.pos, z = a.z, estado = a.gun });
            a.gunId = d.gun; a.gun = d.estado ?? new WeaponStats.Gun(WeaponStats.ALL[d.gun]); a.scoped = false; a.spray = 0;
            Log($"{a.name} pegou {WeaponStats.ALL[d.gun].name} do chão ({Places.Region(a.pos)})");
        }
        public readonly Dictionary<string, float> paradoPor = new Dictionary<string, float>();   // tempo parado por fase do Brain
        public readonly Dictionary<string, int> vaiVoltaPor = new Dictionary<string, int>();     // vai-e-volta W-S por fase
        public readonly Dictionary<string, (int parede, int total)> miraPor = new Dictionary<string, (int, int)>();   // mira colada na parede, por fase
        public readonly List<(Team tm, V2 p, float t, bool plantado, float ang)> aparecidos = new List<(Team, V2, float, bool, float)>();   // de onde o inimigo apareceu pra quem (aprendizado do Aparece)
        public readonly List<float> contatosTiro = new List<float>();   // só avistamentos em que ele atirou em até 2 s (é o que o dado real mede)
        public readonly List<(float flick, string fase)> contatos = new List<(float, string)>();   // graus fora no 1º contato
        /// <summary>Eventos contados (validador): Ev soma toda vez, Ev1 uma vez por round.</summary>
        public readonly Dictionary<string, int> ev = new Dictionary<string, int>();
        readonly HashSet<string> evUma = new HashSet<string>();
        public void Ev(string k, int n = 1) { ev.TryGetValue(k, out int v); ev[k] = v + n; }
        public static bool Auditar; Auditoria aud;
        public void Aud(string grupo, string item) { if (Auditar) Ev($"AUD {grupo}|{item}"); }
        public void Ev1(string k) { if (evUma.Add(k)) Ev(k); }
        float nextIntel, nextFireDmg;
        readonly Dictionary<int, float> stepAt = new Dictionary<int, float>();

        public static readonly (string name, Role role, string gun, char site)[] T_TEAM =
            { ("VULCAO", Role.Entry, "ak", 'M'), ("FAISCA", Role.Suporte, "ak", 'M'), ("SOMBRA", Role.Lurker, "ak", 'M'), ("MIRA", Role.Awper, "awp", 'M'), ("TROVAO", Role.Igl, "ak", 'M') };
        public static readonly (string name, Role role, string gun, char site)[] CT_TEAM =
            { ("ROCHA", Role.Ancora, "m4", 'A'), ("ESCUDO", Role.Rotacao, "m4s", 'A'), ("FALCAO", Role.Awper, "awp", 'M'), ("ANCORA", Role.Ancora, "m4", 'B'), ("RADAR", Role.Suporte, "m4s", 'B') };

        /// <summary>Com o que um jogador entra no round (a Partida decide: compra, arma que sobreviveu, drop do time).</summary>
        public class Loadout
        {
            public string name; public Role role; public char site = 'M'; public string gun;
            public bool armor = true, helmet = true, kit; public List<string> nades = new List<string>();
        }

        /// <summary>Experimento: rifle do CT trocado (ex.: "ak" pra isolar o efeito da arma). null = normal.</summary>
        public static string ArmaCT;
        /// <summary>Round avulso (treino/estatística): todo mundo de compra cheia.</summary>
        public static Loadout[] CompraCheia()
        {
            var l = new List<Loadout>();
            // HE: uns 3 de cada time (quem sobra dinheiro depois do resto)
            foreach (var (n, r, g, s) in T_TEAM) l.Add(new Loadout { name = n, role = r, gun = g, site = s, nades = { "smoke", "flash", "molotov" } });
            foreach (var (n, r, g, s) in CT_TEAM) l.Add(new Loadout { name = n, role = r, gun = ArmaCT != null && g != "awp" ? ArmaCT : g, site = s, kit = true, nades = { "smoke", "flash", "molotov" } });
            foreach (int i in new[] { 0, 2, 4, 5, 7, 9 }) l[i].nades.Add("he");
            if (TeamBrain.EXP.Contains("semHE")) foreach (var x in l) x.nades.Remove("he");
            return l.ToArray();
        }

        public readonly string[] compra = { "full", "full" };   // tipo de compra de cada time neste round (o IGL lê)
        public readonly float[] aperto = new float[2];          // 0 = rico; 1 = perder as armas agora quebra o próximo round
        public readonly bool[] semAmanha = new bool[2];
        public float tTRnoBomb = -1;
        public int quedasC4;                                    // portador do C4 morreu (o bomb caiu) neste round                            // quando o primeiro TR pisou num bomb (ritmo)         // guardar não adianta: dinheiro reseta depois ou perder acaba o jogo
        public readonly Leitura[] leitura = new Leitura[2];     // o que cada time aprendeu do adversário na partida (null = round avulso)

        /// <param name="pT">pesos do TR (null = os aprovados)</param><param name="pCT">pesos do CT</param>
        /// <param name="setup">10 jogadores (5 TR e depois 5 CT); null = compra cheia</param>
        public Match(int seed, Pesos pT = null, Pesos pCT = null, Loadout[] setup = null, string[] compra = null, float[] aperto = null, Leitura lT = null, Leitura lCT = null)
        {
            leitura[(int)Team.T] = lT; leitura[(int)Team.CT] = lCT;
            rng = new Rng(seed); this.seed = seed;
            Perigo.Build();
            pesos[(int)Team.T] = pT ?? Pesos.Padrao(); pesos[(int)Team.CT] = pCT ?? Pesos.Padrao();
            setup ??= CompraCheia();
            if (compra != null) { this.compra[0] = compra[0]; this.compra[1] = compra[1]; }
            if (aperto != null) { this.aperto[0] = aperto[0]; this.aperto[1] = aperto[1]; }
            for (int id = 0; id < 10; id++)
            {
                var l = setup[id]; var tm = id < 5 ? Team.T : Team.CT;
                var a = new Agent(id, l.name, tm, l.role, l.gun, pesos[(int)tm]) { pos = (tm == Team.T ? MapData.SPAWN_T : MapData.SPAWN_CT)[id % 5] };
                a.armor = l.armor ? 100 : 0; a.helmet = l.helmet && l.armor; a.hasKit = tm == Team.CT && l.kit; a.nades.AddRange(l.nades);
                a.brain = new Brain(a, this, seed * 31 + id) { homeSite = l.site };
                agents.Add(a);
            }
            foreach (var a in agents) { a.z = NavGrid.ZAt(a.pos); a.aim = a.IsT ? -MathF.PI / 2 : MathF.PI / 2; }
            intel[(int)Team.T] = new Intel(Team.T, agents.Where(a => a.team == Team.CT));
            intel[(int)Team.CT] = new Intel(Team.CT, agents.Where(a => a.team == Team.T));
            igl[(int)Team.T] = new TeamBrain(this, Team.T, seed * 7 + 1);
            igl[(int)Team.CT] = new TeamBrain(this, Team.CT, seed * 7 + 2);
            igl[0].Start(); igl[1].Start();
        }

        public void Log(string s) => log.Add($"{t,6:0.00}  {s}");
        public int Alive(Team tm) => agents.Count(a => a.team == tm && a.alive);
        public bool ClutchFor(Team tm) => Alive(tm) == 1;
        public float RoundLeft => ROUND - t;
        public float BombLeft => bombPlanted ? BOMB - (t - plantedAt) : 99;
        /// <summary>Desarmar: 5 s com kit, 10 s sem (CS).</summary>
        public static float DefuseTime(Agent a) => a != null && a.hasKit ? 5f : 10f;
        /// <summary>Dinheiro por abate (CS2): AWP 100, SMG 600, rifle/pistola 300.</summary>
        public static int KillReward(string gun) => gun == "awp" ? 100 : gun == "mp9" || gun == "mac10" ? 600 : 300;
        public V2 PlantSpot => Places.Plant(planSite);

        // ================================================================ passo
        public void Step()
        {
            if (over) return;
            t += DT;
            foreach (var s in smokes) s.Update(DT);
            smokes.RemoveAll(s => !s.Alive);
            foreach (var f in fires) f.Update(DT);
            fires.RemoveAll(f => !f.Alive);
            flashes.RemoveAll(f => t - f.t > 0.5f);
            if (tTRnoBomb < 0)
                foreach (var a in agents)
                    if (a.alive && a.IsT && (MapData.SITES["A"].Contains(a.pos.x, a.pos.y) || MapData.SITES["B"].Contains(a.pos.x, a.pos.y))) { tTRnoBomb = t; break; }
            hes.RemoveAll(f => t - f.t > 0.6f);
            shots.RemoveAll(s => t - s.t > 0.15f);
            // granadas chegando
            for (int i = flying.Count - 1; i >= 0; i--)
            {
                var n = flying[i]; if (!Voa(n)) continue;
                flying.RemoveAt(i);
                if ((n.P - n.l.to).Len > 20 && n.paredes > 0) { n.by.nadeErrada++; Log($"{n.by.name}: {n.l.name} bateu na parede e parou em {Places.Region(n.P)}"); }
                n.l = new Places.Lineup(n.l.name, n.l.kind, n.l.from, n.P);
                Land(n);
            }
            long c0 = Perfil ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            // informação dos times (4x por segundo): calor escorre e o que cada um vê limpa
            if (t >= nextIntel) { nextIntel = t + 0.25f; UpdateIntel(); }
            long c1 = Marca(0, c0);
            igl[0].Tick(); igl[1].Tick();
            long c2 = Marca(4, c1);
            foreach (var a in agents) if (a.alive) { a.brain.Tick(DT); a.brain.TurnAim(DT); }
            long c3 = Marca(5, c2);
            foreach (var a in agents) if (a.alive) Integrate(a);
            Marca(6, c3);
            foreach (var a in agents) if (a.alive) { a.gun.Tick(DT); a.gun.Recupera(DT, a.crouch, a.spray); Activity(a); }
            if (bombDropped)   // TR passando em cima do bomb pega
                foreach (var a in agents)
                    if (a.alive && a.IsT && (a.pos - droppedBomb).Len < 12 && !(bombGuardado && a == guardouC4 && t - guardouC4T < 6)) { a.hasBomb = true; bombDropped = false; bombGuardado = false; a.brain.Think("peguei o bomb"); Log($"{a.name} pegou o bomb"); break; }
            // granada dropada: quem ela é pra (ou qualquer um do time depois de 3 s) passando em cima pega, se cabe
            for (int i = itensChao.Count - 1; i >= 0; i--)
            {
                var it = itensChao[i];
                foreach (var a in agents)
                    if (a.alive && a.team == it.time && (it.para == a || t - it.t > 3 && a != it.de) && (a.pos - it.pos).Len < 12 && CabeGranada(a, it.nade))
                    { a.nades.Add(it.nade); itensChao.RemoveAt(i); a.brain.Think($"peguei a {it.nade}{(it.de != null ? " do " + it.de.name : "")}"); Log($"{a.name} pegou {it.nade} do chão"); break; }
            }
            // fogo machuca
            if (t >= nextFireDmg)
            {
                nextFireDmg = t + 0.25f;
                foreach (var a in agents)
                    if (a.alive && FireAt(a.pos)) { Hurt(a, null, 9, Combat.Part.Perna, "molotov"); if (!a.alive) { a.killedByFire = true; Ev("morreu na molotov/round"); } }
            }
            if (t >= proxSuspeita) { proxSuspeita = t + 1; Suspeitas(); }
            if (Auditar && ((int)(t / DT)) % 8 == 0) (aud ??= new Auditoria(this)).Passo();
            CheckEnd();
            if (Auditar && over) (aud ??= new Auditoria(this)).Fim();
        }

        /// <summary>
        /// Detector de comportamento estranho: o que um humano vendo o replay chamaria de bug. Cada tipo marca uma vez por
        /// jogador por round, com o instante, pra ir direto ver (replay) e contar (relatório).
        /// </summary>
        public readonly List<(float t, int id, string tipo, string texto)> suspeitas = new List<(float, int, string, string)>();
        readonly HashSet<string> suspeitaUma = new HashSet<string>(); readonly int[] miraRuim = new int[10];
        float proxSuspeita, vantagemDesde = -1;
        void Marca(Agent a, string tipo, string texto)
        {
            if (!suspeitaUma.Add(tipo + a.id)) return;
            suspeitas.Add((t, a.id, tipo, texto)); Ev($"SUSPEITA {tipo}/round");
        }
        void Suspeitas()
        {
            if (over) return;
            bool vant = !bombPlanted && Alive(Team.T) - Alive(Team.CT) >= 2;
            if (!vant) vantagemDesde = -1; else if (vantagemDesde < 0) vantagemDesde = t;
            foreach (var a in agents)
            {
                if (!a.alive) continue;
                var b = a.brain;
                if (!a.hasBomb || bombPlanted) a.c4Desde = -1;
                else if (a.c4Desde < 0 || (a.pos - a.c4Pos).Len > 100) { a.c4Pos = a.pos; a.c4Desde = t; }
                if (a.hasBomb && !bombPlanted)
                {
                    float c4Aqui = t - a.c4Desde;
                    Aud("segundos com o C4", c4Aqui > 10 ? "preso no mesmo lugar há 10 s+" : c4Aqui > 6 ? "preso há 6-10 s" : "se mexendo");
                    if (c4Aqui > 10 && !b.order.plant) Marca(a, "C4 parado", $"{a.name} há {c4Aqui:0} s no mesmo lugar com o C4 ({b.fase}) em {Places.Region(a.pos)}");
                }
                if (a.IsT && vant && t - vantagemDesde > 20) Marca(a, "vantagem sem plantar", $"{Alive(Team.T)}x{Alive(Team.CT)} há {t - vantagemDesde:0} s sem plantar; {a.name}: {b.order.name} ({b.fase})");
                if (a.paradoHa > 10 && b.order.hold == null && !b.order.malote && t >= b.order.notBefore) Marca(a, "parado sem posição", $"{a.name} parado há {a.paradoHa:0} s sem segurar nada: {b.order.name} ({b.fase})");
                // inimigo conhecido perto e a mira colada na parede por 3 s seguidos
                bool ruim = false;
                if (b.target == null && b.action == null)
                {
                    bool perto = false;
                    foreach (var c in intel[(int)a.team].contacts.Values) if (t - c.t < 2 && agents[c.id].alive && (c.pos - a.pos).Len < 350) perto = true;
                    if (perto) ruim = !MapData.LosWalk(a.pos, a.z, a.pos + V2.Ang(a.aim) * 60, a.z, a.Eye);
                }
                miraRuim[a.id] = ruim ? miraRuim[a.id] + 1 : 0;
                if (miraRuim[a.id] >= 3) Marca(a, "mira na parede com inimigo conhecido", $"{a.name} em {Places.Region(a.pos)} mirando a parede há 3 s com inimigo visto perto ({b.fase})");
            }
        }

        /// <summary>Gravidade (unidades/s²): o pulo de 18-20 unidades (~70% do boneco, como o do CS) leva ~0,3 s pra subir.</summary>
        public const float GRAV = 390f;

        void Integrate(Agent a)
        {
            V2 want = a.brain.wantVel;
            V2 dv = want - a.vel; float l = dv.Len, acc = 900 * DT;
            a.vel = l <= acc ? want : a.vel + dv / l * acc;
            V2 np = a.pos + a.vel * DT, antes = a.pos;
            // altura: degrau pequeno anda; mais alto (caixa, carroça) é PULO — sobe primeiro, passa por cima quando os pés
            // chegam lá; pra baixo de uma quina, cai (gravidade). Dá pra pular com a arma na mão (a faca só corre mais)
            bool escada = NavGrid.Escada(a.pos) || NavGrid.Escada(np);   // escada de mão: degrau a degrau (até 8), sem pulo
            float degrau = escada ? 8.5f : 4f;
            bool Pode(V2 q) => NavGrid.Movable(q) && (NavGrid.ZAt(q) <= a.z + degrau || a.noAr && NavGrid.ZAt(q) <= a.z + 1);
            float zAlvo = NavGrid.Movable(np) ? NavGrid.ZAt(np) : a.z;
            if (!a.noAr && !escada && a.pularPara > a.z + 4) zAlvo = a.pularPara;   // pulo pedido (degrau logo à frente)
            if (!a.noAr && !escada && (NavGrid.Movable(np) || a.pularPara > 0) && zAlvo > a.z + 4 && zAlvo - a.z <= NavGrid.JUMP + 0.6f)
            { a.noAr = true; a.vz = MathF.Sqrt(2 * GRAV * (zAlvo - a.z + 3)); a.quedaDe = a.z; }
            a.pularPara = 0;
            if (Pode(np)) a.pos = np;
            else
            {   // desliza na parede (no eixo que der mais)
                V2 nx = new V2(np.x, a.pos.y), ny = new V2(a.pos.x, np.y);
                bool okx = Pode(nx), oky = Pode(ny);
                if (okx && oky) a.pos = MathF.Abs(np.x - a.pos.x) >= MathF.Abs(np.y - a.pos.y) ? nx : ny;
                else if (okx) a.pos = nx; else if (oky) a.pos = ny;
                // a velocidade é o que ele andou de verdade: batendo na parede ela cai (antes ficava "50" parado e ninguém via o bot travado)
                a.vel = (a.pos - antes) / DT;
            }
            float zt = NavGrid.ZAt(a.pos);
            if (a.noAr)
            {
                a.vz -= GRAV * DT; a.z += a.vz * DT;
                if (a.z <= zt && a.vz <= 0)
                {   // aterrissou (queda de cima faz barulho)
                    a.noAr = false; a.vz = 0;
                    if (a.quedaDe - zt > 12) Sound(a.pos, a.team, "passos", 300, 30);
                    a.z = zt;
                }
            }
            else if (zt < a.z - 4 && !NavGrid.Escada(a.pos)) { a.noAr = true; a.vz = 0; a.quedaDe = a.z; }   // saiu da quina: cai
            else a.z += (zt - a.z) * MathF.Min(1, DT * 18);                          // rampa/degrau: acompanha o chão
            if (a.Speed > a.MaxSpeed * 0.34f) a.rapidoEm = t;
            if (a.Speed < 2) a.move = Move.Parado;
            // passo correndo faz barulho
            if (a.Speed > a.MaxSpeed * 0.6f)
            {
                stepAt.TryGetValue(a.id, out float last);
                if (t - last > 0.38f)
                {
                    stepAt[a.id] = t; Sound(a.pos, a.team, "passos", 260, 35);
                    if (agents.Any(b => b.alive && b.team != a.team && (b.pos - a.pos).Len < 260)) a.revelou++;   // entregou a posição
                }
            }
        }

        /// <summary>Validador de atividade: parado (fora de ação/duelo), parado há mais de 2 s, visto pelo inimigo.</summary>
        void Activity(Agent a)
        {
            a.tVivo += DT;
            // janelão: quem sobe (CT pela escada da praça, TR pelo caminho de rato)
            if (((int)(t / DT) + a.id) % 8 == 0 && a.z > MapData.UP - 6 && MapData.AreaAt(a.pos.x, a.pos.y) == "Janelao")
            {
                Ev1($"{(a.IsT ? "TR" : "CT")} subiu no janelão % rounds");
                if (!a.IsT && a.gunId == "awp") Ev1("CT AWP no janelão % rounds");
                if (!a.IsT && t < 25) Ev1("CT no janelão antes dos 25 s % rounds");
                if (!a.IsT) Ev("CT no janelão (x0,25 s)/round");
            }
            // dois parados colados olhando o mesmo pixel (um ângulo aberto em algum lugar)
            if (((int)(t / DT) + a.id) % 8 == 0 && a.Speed < 3 && a.brain.target == null)
                foreach (var b in agents)
                    if (b.id > a.id && b.alive && b.team == a.team && b.Speed < 3 && b.brain.target == null && (b.pos - a.pos).Len < 60
                        && MathF.Abs(MathX.AngDiff(a.aim, b.aim)) < 0.26f) { Ev("par parado no mesmo pixel (amostras)"); break; }
            if (a.vistoAgora) { a.tExposto += DT; a.vistoAgora = false; }
            bool ocupado = a.brain.action != null || a.brain.target != null;
            if (a.Speed < 3 && !ocupado)
            {
                a.tParado += DT; a.paradoHa += DT; if (a.paradoHa > 2) a.tParadoLongo += DT;
                paradoPor.TryGetValue(a.brain.fase, out float v); paradoPor[a.brain.fase] = v + DT;
            }
            else a.paradoHa = 0;
            // mira olhando pro nada: o raio da mira bate na parede a menos de 90 (amostra a cada 8 ticks, fora de duelo/ação)
            if (!ocupado && ((int)(t / DT) + a.id) % 8 == 0)
            {
                V2 dir = V2.Ang(a.aim); float d = 12;
                while (d < 90 && MapData.LosWalk(a.pos, a.z, a.pos + dir * d, a.z, a.Eye)) d += 12;
                string k = a.brain.fase; miraPor.TryGetValue(k, out var c); miraPor[k] = (c.parede + (d < 90 ? 1 : 0), c.total + 1);
            }
            // vai-e-volta: andou, parou e voltou pro lado oposto logo em seguida. Na linha da mira (W-S) é esquisito; de lado
            // (A-D) é peek/duelo normal
            if (a.Speed > a.MaxSpeed * 0.3f)
            {
                V2 v = a.vel / a.Speed;
                if (t - a.dirAntT < 0.7f && v.Dot(a.dirAnt) < -0.5f && t - a.dirAntT > DT * 1.5f)
                {
                    if (MathF.Abs(v.Dot(V2.Ang(a.aim))) > 0.7f)
                    { a.vaiVoltaFrente++; string k = a.brain.fase + (a.brain.micro != Brain.Micro.Nada ? "/" + a.brain.micro : ""); vaiVoltaPor.TryGetValue(k, out int n); vaiVoltaPor[k] = n + 1; }
                    else a.vaiVoltaLado++;
                }
                a.dirAnt = v; a.dirAntT = t;
            }
        }

        void Sound(V2 p, Team who, string kind, float radius, float uncertainty)
        {
            sounds.Add((t, p, who, kind));
            var other = who == Team.T ? Team.CT : Team.T;
            // cada um ouve o que chega no ouvido dele; o time só sabe depois que alguém falar (tiro e bomb todo mundo ouve)
            bool heard = radius > 5000;
            foreach (var b in agents)
                if (b.alive && b.team == other && (b.pos - p).Len < MathF.Min(radius, 700)) { b.brain.Ouviu(p + rng.Normal2(uncertainty * 0.5f), t, kind); heard = true; }
            if (radius > 5000) intel[(int)other].Heard(p + rng.Normal2(uncertainty * 0.5f), t, kind, uncertainty);
            else if (heard) somFila.Add((t + rng.Uniform(0.5f, 1.1f), other, p + rng.Normal2(uncertainty * 0.5f), kind, uncertainty));
        }

        // ---- perfil (onde vai o tempo da simulação): MatchCli.Stats -perfil
        public static bool Perfil;
        public static readonly long[] Tempo = new long[8];
        public static readonly string[] TEMPO_NOME = { "intel (total)", "intel: calor escorre", "intel: visão", "intel: juntar rastros", "IGL", "cérebros (Brain.Tick)", "movimento", "" };
        static long Marca(int i, long desde)
        {
            if (!Perfil) return 0;
            long agora = System.Diagnostics.Stopwatch.GetTimestamp();
            System.Threading.Interlocked.Add(ref Tempo[i], agora - desde);
            return agora;
        }

        void UpdateIntel()
        {
            long c0 = Perfil ? System.Diagnostics.Stopwatch.GetTimestamp() : 0;
            Radio();
            foreach (var it in intel) it.Spread(t, pesos[(int)it.team].memoria);
            c0 = Marca(1, c0);
            foreach (var a in agents)
            {
                if (!a.alive || a.Blind(t)) continue;
                var it = intel[(int)a.team];
                void Limpa(int i) { if (it.heat[i] > 0.05f) a.info += it.heat[i]; it.Clear(i, t); }
                // o que está do lado (até ~20, qualquer direção) e o leque de visão
                int cx = (int)(a.pos.x / Intel.C), cy = (int)(a.pos.y / Intel.C);
                for (int x = cx - 1; x <= cx + 1; x++) for (int y = cy - 1; y <= cy + 1; y++) if (x >= 0 && y >= 0 && x < Intel.GX && y < Intel.GY && Intel.Pass(x * Intel.GY + y)) Limpa(x * Intel.GY + y);
                // parado olhando o mesmo lugar (e sem smoke): vê as mesmas células — reaproveita em vez de lançar os raios de novo
                bool igual = a.visCache != null && smokes.Count == 0 && a.visSmokes == 0 && (a.pos - a.visPos).Len < 1.5f && MathF.Abs(MathX.AngDiff(a.aim, a.visAim)) < 0.03f;
                if (igual) foreach (int i in a.visCache) Limpa(i);
                else
                {
                    var lista = a.visCache ??= new List<int>(); lista.Clear();
                    // limpo é o que ele checou de verdade (em volta da mira), não tudo que cabe na tela: assim as quinas são fatiadas uma a uma
                    Intel.Viewshed(a.pos, a.z + 18, a.aim, (TeamBrain.EXP.Contains("leque60") ? 60f : 28f) * MathF.PI / 180, 450, smokes.Count > 0 ? SmokeDense : (Func<V2, float, bool>)null, i => { lista.Add(i); Limpa(i); });
                    a.visPos = a.pos; a.visAim = a.aim; a.visSmokes = smokes.Count;
                }
            }
            c0 = Marca(2, c0);
            // inimigo visto agora segue aceso onde está; depois junta os rastros
            foreach (var it in intel) { it.ReacendeVistos(t); it.Recalc(); }
            Marca(3, c0);
        }

        // ================================================================ ações dos bots
        /// <summary>
        /// Rádio do time: quem vê reage na hora (o Brain dele já tem o alvo), mas o resto do time só sabe depois do call
        /// (0,4–1,2 s: falar no microfone; pior com pouco sangue-frio). O call diz onde ele ESTAVA. Enquanto o mesmo
        /// inimigo segue à vista já "cantado", a info vai sendo atualizada ("tá pushando, tá pushando"). Dead call: quem
        /// morreu depois de ver ainda fala na maioria das vezes.
        /// </summary>
        readonly Dictionary<(int, int), (float desde, float callAt, float ultimo)> radio = new Dictionary<(int, int), (float, float, float)>();
        readonly List<(float at, Team team, int id, V2 pos, float z, bool bomba, float visto, Agent by)> calls = new List<(float, Team, int, V2, float, bool, float, Agent)>();
        public int callsFeitos, callsPerdidos;

        public void Spot(Agent by, Agent e)
        {
            e.vistoAgora = true;
            leitura[(int)by.team]?.Vi(e.pos, t, e.team, e.Speed < 5, e.id);
            if (firstSightT < 0) firstSightT = t;
            Chamar(by, e);
        }

        void Chamar(Agent by, Agent e)
        {
            if (TeamBrain.EXP.Contains("semRadio")) { intel[(int)by.team].Seen(e, t, by.name); return; }
            var k = ((int)by.team, e.id);
            if (!radio.TryGetValue(k, out var r) || t - r.ultimo > 2f)
            {
                float atraso = rng.Uniform(0.4f, 0.9f) + 0.3f * (1 - by.skill.composure);
                r = (t, t + atraso, t);
                calls.Add((t + atraso, by.team, e.id, e.pos, e.z, e.hasBomb, t, by));
            }
            r.ultimo = t; radio[k] = r;
            if (t >= r.callAt) intel[(int)by.team].Seen(e, t, by.name);   // já cantado: o time acompanha
        }

        readonly List<(float at, Team team, V2 p, string kind, float inc)> somFila = new List<(float, Team, V2, string, float)>();
        void Radio()
        {
            for (int i = somFila.Count - 1; i >= 0; i--)
                if (t >= somFila[i].at) { var s = somFila[i]; intel[(int)s.team].Heard(s.p, t, s.kind, s.inc); somFila.RemoveAt(i); }
            for (int i = calls.Count - 1; i >= 0; i--)
            {
                var c = calls[i]; if (t < c.at) continue;
                calls.RemoveAt(i);
                if (!agents[c.id].alive) continue;
                if (!c.by.alive && rng.Rand() > 0.75f) { callsPerdidos++; continue; }   // morreu sem conseguir falar
                callsFeitos++;
                intel[(int)c.team].Seen(c.id, c.pos, c.z, c.bomba, c.visto, c.by.name);
            }
        }

        public void Shoot(Agent a, V2 aimPt, float aimZ)
        {
            if (!a.gun.CanFire(t) || !a.ArmaPronta(t)) return;
            a.gun.Fire(t);
            a.shots++;
            if (t - a.lastShotT > 0.35f) a.spray = 0;
            var (victim, part, end, endZ) = Combat.Fire(this, a, aimPt, aimZ, rng);
            a.spray++; a.lastShotT = t;
            shots.Add(new ShotFx { a = a.pos, za = a.z + a.Eye, b = end, zb = endZ, t = t, hit = victim != null, hs = part == Combat.Part.Cabeca, team = a.team });
            Sound(a.pos, a.team, "tiro", 99999, 40);
            a.brain.Entregou();
            if (a.gunId == "awp") a.scoped = false;   // AWP sai da luneta depois do tiro (puxa o ferrolho)
            if (victim != null && victim.team != a.team)
            {
                a.hits++; int hpAntes = victim.hp;
                bool viu = victim.brain.target == a;
                bool fora = MathF.Abs(MathX.AngDiff((a.pos - victim.pos).Atan2, victim.aim)) > 55f * MathF.PI / 180;
                bool smk = SmokeBlocks(victim.pos, victim.z + victim.Eye, a.pos, a.z + a.Eye), cego = victim.Blind(t);
                string fv = victim.brain.micro != Brain.Micro.Nada ? victim.brain.micro.ToString() : victim.brain.fase, fa = a.brain.lastMicroOuFase;
                Hurt(victim, a, Combat.Damage(a, victim, part), part, a.gunId);
                acertos.Add(new Acerto { dist = (victim.pos - a.pos).Len, flick = a.brain.flickAlvo, vAlvo = victim.Speed, vAtirador = a.Speed, parte = (int)part, spray = a.spray, hpAntes = hpAntes, matou = !victim.alive, arma = a.gunId,
                    atiradorTR = a.IsT, vitimaViu = viu, faseVitima = fv, faseAtirador = fa, situAtirador = a.brain.Situacao, situVitima = victim.brain.Situacao, holdAtirador = a.brain.PosicaoAgora, holdVitima = victim.brain.PosicaoAgora, foraDaVisao = fora, smoke = smk, cego = cego, posAtirador = a.pos, posVitima = victim.pos, t = t, plantado = bombPlanted, seed = seed, matador = a.name, vitima = victim.name,
                    vitViuHa = viu ? t - victim.brain.alvoDesde : -1, matViuHa = a.brain.target == victim ? t - a.brain.alvoDesde : -1,
                    vitimaAtirou = t - victim.lastShotT < 1f, vitAndando = victim.Speed > victim.MaxSpeed * 0.3f });
            }
        }

        void Hurt(Agent v, Agent by, int dmg, Combat.Part part, string what)
        {
            if (!v.alive) return;
            v.hp -= dmg; if (by != null) by.damage += Math.Min(dmg, v.hp + dmg);
            if (part == Combat.Part.Tronco && v.armor > 0) v.armor = Math.Max(0, v.armor - dmg / 2);
            if (by != null && what == by.gunId) Chamar(v, by);   // quem levou tiro sabe de onde veio e canta (HE não diz)
            v.brain.Entregou();
            if (v.hp > 0 && by != null && what == by.gunId) v.brain.Atingido(by);   // tiro (não fogo/explosão) e ainda vivo
            // call de dano: quem acertou sabe quanto tirou ("ele tá com 20")
            if (by != null && v.hp > 0 && intel[(int)by.team].contacts.TryGetValue(v.id, out var cv)) { cv.hp = v.hp; cv.hpT = t; if (v.hp < 45) by.brain.Think($"call: {v.name} tá com {v.hp}!"); }
            if (v.hp > 0) { v.brain.Think($"levei {dmg} {(part == Combat.Part.Cabeca ? "na cabeça" : part == Combat.Part.Perna ? "na perna" : "no corpo")}{(by != null ? " do " + by.name : "")}"); return; }
            v.hp = 0; v.alive = false; v.diedAt = t; v.killedBy = by; v.deaths++;
            drops.Add(new Drop { gun = v.gunId, pos = NavGrid.Snap(v.pos), z = v.z, estado = v.gun });   // a arma cai onde ele morreu
            v.brain.Think($"morri{(by != null ? " pro " + by.name : " na " + what)}");
            if (by != null)
            {
                by.kills++; if (part == Combat.Part.Cabeca) by.hs++;
                if (by.team != v.team) by.ganho += KillReward(what == by.gunId ? by.gunId : what);
                if (firstKill == null) { firstKill = by; firstDeath = v; firstKillT = t; by.firstKills++; v.firstDeaths++; }
                if (by.team != v.team) { leitura[(int)v.team]?.MorremosDali(by.pos, v.team); leitura[(int)v.team]?.MorremosAqui(v.pos, v.team); leitura[(int)by.team]?.MatamosDali(by.pos, by.team); }
                // troca: matou alguém que tinha matado um aliado nos últimos 3 s
                var traded = agents.FirstOrDefault(x => !x.alive && x.team == by.team && x.killedBy == v && t - x.diedAt < 3f);
                if (traded != null) { by.tradeKills++; traded.traded++; }
                if (agents.Any(x => !x.alive && x.team == by.team && x.killedBy == v && t - x.diedAt < 5f)) Ev("troca em 5 s (abates)/round");
                feed.Add((t, $"{by.name} [{by.gunId.ToUpper()}] {v.name}{(part == Combat.Part.Cabeca ? " (HS)" : "")}"));
                Log($"{by.name} matou {v.name} com {by.gunId}{(part == Combat.Part.Cabeca ? " na cabeça" : "")} ({Places.Region(v.pos)}){(traded != null ? " — TROCA do " + traded.name : "")}");
            }
            else { feed.Add((t, $"{v.name} morreu ({what})")); Log($"{v.name} morreu ({what})"); }
            foreach (var mate in agents) if (mate != v && mate.team == v.team) mate.brain.OnTeammateDied(v, by);
            // clutch: um time ficou com 1 contra X (validador: quanto converte por X)
            foreach (var tm in new[] { Team.T, Team.CT })
            {
                var o = tm == Team.T ? Team.CT : Team.T;
                if (clutchContra[(int)tm] < 0 && Alive(tm) == 1 && Alive(o) > 0) clutchContra[(int)tm] = Alive(o);
            }
            intel[(int)(v.team == Team.T ? Team.CT : Team.T)].contacts.Remove(v.id);
            intel[(int)(v.team == Team.T ? Team.CT : Team.T)].Morreu(v.id);   // morto não está em lugar nenhum
            // o bomb cai no chão andável (em borda/caixa ficava inalcançável e o TR ficava parado até o tempo acabar)
            if (v.hasBomb) { v.hasBomb = false; bombDropped = true; droppedBomb = NavGrid.Snap(v.pos); quedasC4++; Log($"bomb caiu ({Places.Region(v.pos)})"); }
            if (v == defuser) { defuserActive = false; defuser = null; }
        }

        /// <summary>Granada no ar. Flash: quem jogou e os aliados perto ouvem "bang!" e viram a cara quando estourar.</summary>
        public float Throw(Agent a, Places.Lineup l)
        {
            // acertar o pixel: o jogador SABE o pixel da lineup treinada (cai quase no lugar), mas às vezes erra a
            // execução (mira escorregou, pulou torto) — mais quem tem utilitária fraca e sob pressão. Jogada inventada
            // na hora espalha mais (e mais longe, mais erro). Prédio no caminho do arco: bate e cai antes
            bool treinada = Places.TREINADAS.Contains(l);
            float u = a.skill.utilitaria, dist = (l.to - a.pos).Len;
            float pErra = ERRO_GRANADA * (treinada ? 0.03f + 0.12f * (1 - u) : 0.06f + 0.2f * (1 - u)) * (a.MeioCego(t) || a.hp < 40 ? 2f : 1f);
            // todo arremesso sai da física: a mira que faz parar no pixel (ERRO_GRANADA devolve o erro humano)
            V2 mira = l.to;
            if ((l.kind == "smoke" || l.kind == "molotov") && !TeamBrain.EXP.Contains("semRolagem")) mira = MiraCerta(a.pos, a.z, l.to, l.kind).mira;
            V2 alvo = mira + rng.Normal2(ERRO_GRANADA * (treinada ? 2f + 3f * (1 - u) : 5f + (0.02f + 0.03f * (1 - u)) * dist));
            if (rng.Rand() < pErra)
            {   // errou o pixel: cai do lado (o quanto depende da distância do arremesso)
                alvo = mira + V2.Ang(rng.Uniform(0, 6.2832f)) * rng.Uniform(25, 35 + 0.12f * dist);
                a.nadeErrou++; Log($"{a.name} errou o pixel: {l.name}");
            }
            // arremesso: a mesma parábola que o validador de lineup usa (sobe ~45% da distância, até 120) — o que bate em
            // prédio no caminho agora volta de verdade (e pode cair no pé de quem jogou)
            if (TopG((int)alvo.x, (int)alvo.y) > NavGrid.ZAt(NavGrid.Snap(alvo)) + 20) alvo = NavGrid.Snap(alvo);   // mira não é em cima do prédio
            var n = new Nade { l = l, by = a, from = a.pos, z0 = a.z + 20, t0 = t };
            float L = (alvo - a.pos).Len, H = MathF.Min(0.45f * L, 120f), T = MathF.Max(0.25f, MathF.Sqrt(8 * H / GN));
            float zDe = a.z + 22, zAlvo = MapData.Top((int)alvo.x, (int)alvo.y) + 2;
            Lanca(n, a.pos, zDe, alvo, zAlvo, T);
            // pop flash: em vez de cair e estourar no chão (dá tempo de virar), estoura no ar em cima do alvo — se o arco
            // até lá passa livre
            if (l.kind == "flash" && !TeamBrain.EXP.Contains("semPop") && rng.Rand() < 0.5f + 0.4f * u)
            {
                var teste = new Nade(); float Tf = FUSE_FLASH - 0.05f, popH = rng.Uniform(30, 55);
                Lanca(teste, a.pos, zDe, alvo, zAlvo + popH, Tf);
                if (ArcoLimpo(teste, Tf)) { Lanca(n, a.pos, zDe, alvo, zAlvo + popH, Tf); a.popFlash++; }
            }
            float t1 = l.kind == "flash" ? t + FUSE_FLASH : l.kind == "he" ? t + FUSE_HE : l.kind == "smoke" ? t + T + 0.5f : t + T;
            n.t1 = t1;
            flying.Add(n);
            Log($"{a.name} jogou {l.name}");
            if (l.kind == "flash")
            {   // "vou bangar!": o time perto vira a cara na hora e quem ia abrir o pixel pra lá espera estourar
                a.flashes++;
                foreach (var x in agents) if (x.alive && x.team == a.team && (x.pos - a.pos).Len < 700) { x.avisoFlashAte = t1 + 0.3f; if (x != a) { x.bangAmigaEm = t1; x.bangAmigaPos = l.to; x.bangAmigaDe = a; } }
            }
            else if (l.kind == "smoke") a.smokes++; else if (l.kind == "molotov") a.molotovs++; else if (l.kind == "he") a.hes++;
            return t1;
        }

        static void Lanca(Nade n, V2 de, float zDe, V2 alvo, float zAlvo, float T)
        {
            n.x = de.x; n.y = de.y; n.z = zDe;
            n.vx = (alvo.x - de.x) / T; n.vy = (alvo.y - de.y) / T; n.vz = (zAlvo - zDe) / T + GN * T / 2;
        }

        /// <summary>Teste da física (MatchCli.Lineups): joga daqui pro alvo e diz onde e quando estoura, quantas paredes bateu.</summary>
        public static (V2 p, float z, float t, int paredes, int quiques, bool noAr, bool pop) Simula(V2 de, float zChao, V2 alvo, string kind)
        {
            var n = new Nade { l = new Places.Lineup("teste", kind, de, alvo) };
            float L = (alvo - de).Len, H = MathF.Min(0.45f * L, 120f), T = MathF.Max(0.25f, MathF.Sqrt(8 * H / GN));
            float zDe = zChao + 22, zAlvo = MapData.Top((int)alvo.x, (int)alvo.y) + 2; bool pop = false;
            Lanca(n, de, zDe, alvo, zAlvo, T);
            if (kind == "flash") { var te = new Nade(); Lanca(te, de, zDe, alvo, zAlvo + 40, FUSE_FLASH - 0.05f); if (ArcoLimpo(te, FUSE_FLASH - 0.05f)) { Lanca(n, de, zDe, alvo, zAlvo + 40, FUSE_FLASH - 0.05f); pop = true; } }
            float tt = 0, parado = -1;
            while (tt < 8)
            {
                float sp = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy + n.vz * n.vz);
                int k = Math.Max(1, (int)MathF.Ceiling(sp * DT / 3f));
                for (int s = 0; s < k; s++) Passo(n, DT / k);
                tt += DT;
                if (kind == "molotov" && n.tocouChao) break;
                if (kind == "flash" && tt >= FUSE_FLASH || kind == "he" && tt >= FUSE_HE) break;
                if (kind == "molotov" && tt >= FUSE_MOLOTOV) { n.noAr = true; break; }
                if (kind == "smoke") { float vh = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy); if (n.noChao && vh < 4) { if (parado < 0) parado = tt; } else parado = -1; if (parado >= 0 && tt - parado >= 0.15f && tt > 0.6f || tt > 6) break; }
            }
            return (n.P, n.z, tt, n.paredes, n.quiques, n.noAr, pop);
        }

        /// <summary>
        /// Saber a lineup de smoke é saber quanto ela rola: mira antes do pixel pra parar nele (4 tentativas na física).
        /// </summary>
        public static V2 MiraSmoke(V2 de, float zChao, V2 alvo)
        {
            V2 mira = alvo, melhor = alvo; float be = float.MaxValue;
            for (int i = 0; i < 9; i++)
            {
                var r = Simula(de, zChao, mira, "smoke");
                V2 erro = alvo - r.p; if (erro.Len < be) { be = erro.Len; melhor = mira; }
                if (erro.Len < 6) break;
                mira = mira + erro * (i < 4 ? 0.85f : 0.5f);   // rampa: a resposta não é linear, passo menor
            }
            return melhor;
        }

        /// <summary>0 = bot perfeito no arremesso; 1 = erro humano por Skill.utilitaria. Humanização fica pra depois do treino.</summary>
        public static float ERRO_GRANADA = 0;
        static readonly System.Collections.Concurrent.ConcurrentDictionary<long, (V2 mira, float erro)> tabelaNade = new System.Collections.Concurrent.ConcurrentDictionary<long, (V2, float)>();
        /// <summary>
        /// Tabela de arremessos: de onde estou, pra esse alvo, qual mira para no pixel e quanto sobra de erro, medido na
        /// física. Vale pra qualquer ponto do mapa e vai enchendo sozinha (cada par de/alvo é calculado uma vez).
        /// </summary>
        public static (V2 mira, float erro) MiraCerta(V2 de, float zChao, V2 alvo, string kind)
        {
            long k = ((long)Intel.Idx(de) << 28) | ((long)Math.Clamp((int)(alvo.x / 6), 0, 16383) << 14) | (long)Math.Clamp((int)(alvo.y / 6), 0, 8191) << 1 | (kind == "smoke" ? 1L : 0L);
            return tabelaNade.GetOrAdd(k, _ =>
            {
                V2 mira = alvo, melhor = alvo; float be = float.MaxValue;
                for (int i = 0; i < 9; i++)
                {
                    var r = Simula(de, zChao, mira, kind);
                    V2 erro = alvo - r.p; if (erro.Len < be) { be = erro.Len; melhor = mira; }
                    if (erro.Len < 6) break;
                    mira = mira + erro * (i < 4 ? 0.85f : 0.5f);
                }
                return (melhor, be);
            });
        }

        /// <summary>O arco chega até o tempo T sem bater em nada?</summary>
        static bool ArcoLimpo(Nade n, float T)
        {
            float h = DT / 4;
            for (float s = 0; s < T; s += h) { Passo(n, h); if (n.paredes > 0 || n.tocouChao) return false; }
            return true;
        }

        /// <summary>Um passo da granada voando: estoura agora?</summary>
        bool Voa(Nade n)
        {
            float idade = t - n.t0;
            float sp = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy + n.vz * n.vz);
            int k = Math.Max(1, (int)MathF.Ceiling(sp * DT / 3f)); float h = DT / k;   // passos de até 3 unidades (não atravessa parede fina)
            for (int s = 0; s < k; s++)
            {
                Passo(n, h);
                if (n.l.kind == "molotov" && n.tocouChao) return true;   // quebrou no chão: pega fogo ali
            }
            switch (n.l.kind)
            {
                case "flash": return idade >= FUSE_FLASH;
                case "he": return idade >= FUSE_HE;
                case "molotov": if (idade >= FUSE_MOLOTOV) { n.noAr = true; return true; } return false;
                default:   // smoke: estoura quando para
                    float vh = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy);
                    if (n.noChao && vh < 4) { if (n.paradoDesde < 0) n.paradoDesde = t; } else n.paradoDesde = -1;
                    return n.paradoDesde >= 0 && t - n.paradoDesde >= 0.15f && idade > 0.6f || idade > 6f;
            }
        }

        static float TopG(int x, int y) => (uint)x >= MapData.NX || (uint)y >= MapData.NY ? 9999 : MapData.Top(x, y);

        static void Passo(Nade n, float h)
        {
            float nx = n.x + n.vx * h, ny = n.y + n.vy * h, nz = n.z + n.vz * h;
            if (!n.noChao) n.vz -= GN * h;
            int ix = (int)nx, iy = (int)ny;
            float top = TopG(ix, iy);
            if (nz < top)
            {
                if (n.z >= top - 1.5f)
                {   // veio de cima: chão (ou topo de caixa/telhado) — quica baixo e perde velocidade; devagar, rola
                    nz = top; n.tocouChao = true;
                    if (n.vz < 0) n.vz = -n.vz * QUICA_CHAO;
                    n.vx *= ATRITO_QUIQUE; n.vy *= ATRITO_QUIQUE;
                    if (n.vz < 30) { n.vz = 0; n.noChao = true; } else n.quiques++;
                }
                else
                {   // de lado: parede (ou a lateral da caixa) — volta pelo eixo que bateu
                    bool bx = n.z < TopG(ix, (int)n.y), by = n.z < TopG((int)n.x, iy);
                    if (bx || !by) n.vx = -n.vx * QUICA_PAREDE;
                    if (by || !bx) n.vy = -n.vy * QUICA_PAREDE;
                    nx = n.x; ny = n.y; n.paredes++; n.quiques++;
                }
            }
            if (n.noChao)
            {
                float aqui = TopG((int)nx, (int)ny);
                if (nz > aqui + 1.5f) n.noChao = false;   // rolou pra fora da borda: cai
                else
                {
                    nz = aqui;
                    float vh = MathF.Sqrt(n.vx * n.vx + n.vy * n.vy);
                    if (vh > 0) { float nv = MathF.Max(0, vh - ROLAR * h); n.vx *= nv / vh; n.vy *= nv / vh; }
                }
            }
            n.x = nx; n.y = ny; n.z = nz;
        }

        /// <summary>Onde a granada cai jogada de 'de' pra 'alvo': arco em parábola (sobe até ~45% da distância, no máximo
        /// 120 — uns 4 bonecos) por cima de caixa, muro e telhado baixo; se o telhado é alto demais pra esse arco, bate e
        /// cai no ponto mais longe da linha que dá pra alcançar.</summary>
        public static V2 Arco(V2 de, float zDe, V2 alvo)
        {
            if (ArcoLivre(de, zDe, alvo)) return alvo;
            V2 d = alvo - de; float L = d.Len;
            for (float s = L - 8; s > 8; s -= 8)
            {
                V2 q = de + d * (s / L);
                if (NavGrid.Movable(q) && ArcoLivre(de, zDe, q)) return q;
            }
            return NavGrid.Snap(de);
        }

        static bool ArcoLivre(V2 de, float zDe, V2 alvo)
        {
            V2 d = alvo - de; float L = d.Len; if (L < 4) return true;
            float z0 = zDe + 22, z1 = NavGrid.ZAt(alvo) + 4, H = MathF.Min(0.45f * L, 120f);
            for (float s = 3; s < L - 3; s += 3)
            {
                float u = s / L; V2 q = de + d * u;
                float z = z0 + (z1 - z0) * u + 4 * H * u * (1 - u);
                if (MapData.Top((int)q.x, (int)q.y) > z) return false;
            }
            return true;
        }

        public readonly Dictionary<SmokeVolume, Team> smokeDe = new Dictionary<SmokeVolume, Team>();
        /// <summary>Ponto dentro de uma smoke do time adversário (densa na altura do olho)?</summary>
        public SmokeVolume SmokeInimiga(V2 p, Team meu)
        {
            foreach (var s in smokes)
            {
                if (smokeDe.TryGetValue(s, out var tm) && tm == meu) continue;
                var (mn, mx) = s.Bounds; if (p.x < mn.x || p.y < mn.y || p.x > mx.x || p.y > mx.y) continue;
                if (s.Sample(new V3(p.x, p.y, NavGrid.ZAt(p) + 18), t) > 0.35f) return s;
            }
            return null;
        }

        /// <summary>Pedido de bang ("me banga X"): um aliado perto com flash pode atender.</summary>
        public class PedidoBang { public Agent quem; public V2 alvo; public float t, estouraEm = -1; public Agent atendido; }
        public readonly List<PedidoBang> pedidos = new List<PedidoBang>();

        /// <summary>Granadas que caíram (onde, de quem, quando): granada caindo num bomb é o barulho da execução.</summary>
        public readonly List<(V2 p, Team tm, string kind, float t)> pousos = new List<(V2, Team, string, float)>();

        void Land(Nade n)
        {
            float z = n.z;   // onde ela está de verdade (no ar, na caixa, no telhado)
            pousos.Add((n.l.to, n.by.team, n.l.kind, t));
            if (n.l.kind == "smoke") { var s = new SmokeVolume(new V3(n.l.to.x, n.l.to.y, z)); smokes.Add(s); smokeDe[s] = n.by.team; }
            else if (n.l.kind == "molotov")
            {
                if (n.noAr) Log($"molotov do {n.by.name} estourou no ar (sem fogo)");
                else fires.Add(new FireArea(new V3(n.l.to.x, n.l.to.y, z)));
            }
            else if (n.l.kind == "he")
            {
                // smoke volumétrica: a explosão empurra a fumaça e abre uma bolha que fica limpa ~1,5 s e volta a encher
                // em ~1,5 s — quem está olhando vê (e pega) quem esperava atrás
                var p3 = new V3(n.l.to.x, n.l.to.y, z + 10);
                foreach (var s in smokes) if ((new V2(s.origin.x, s.origin.y) - n.l.to).Len < HE_FURO + 60) s.Clear(p3, HE_FURO, 2.1f);
                // estoura onde a física deixou (quicou e rolou até o tempo acabar)
                V2 at = n.l.to; z -= 6;
                hes.Add((at, z + 10, t));
                // dano (CS2: 98 no centro, cai em sino até ~0 no raio; colete segura metade); parede protege
                foreach (var a in agents)
                {
                    if (!a.alive) continue;
                    float d = (a.pos - at).Len; if (d > HE_RAIO) continue;
                    if (!MapData.LosWalk(at, z + 10, a.pos, a.z + 14, 18f)) continue;
                    float raw = 98f * MathF.Exp(-d * d / (2 * HE_SIGMA * HE_SIGMA));
                    int dmg = (int)(a.armor > 0 ? raw * 0.5f : raw);
                    if (a.team == n.by.team) dmg = (int)(dmg * 0.33f);   // dano em aliado reduzido
                    if (dmg < 1) continue;
                    if (a.team != n.by.team) n.by.heDano += Math.Min(dmg, a.hp);
                    Log($"HE do {n.by.name} tirou {dmg} do {a.name}{(a.team == n.by.team ? " (aliado!)" : "")}");
                    Hurt(a, n.by, dmg, Combat.Part.Tronco, "he");
                }
            }
            else if (n.l.kind == "flash")
            {
                flashes.Add((n.l.to, z + 4, t));
                foreach (var a in agents)
                {
                    if (!a.alive) continue;
                    V2 d = n.l.to - a.pos; float dl = d.Len; if (dl > 700) continue;   // flash cega longe (cai com a distância)
                    if (!MapData.LosWalk(a.pos, a.z, n.l.to, z, 18f)) continue;   // estourou no ar (pop flash) se vê de mais lugares
                    // ouviu o "bang!": vira a cara (às vezes não dá tempo — erro humano)
                    if (a.avisoFlashAte >= t && rng.Rand() < 0.95f) continue;
                    float ang = MathF.Abs(MathX.AngDiff(d.Atan2, a.aim)) * 180 / MathF.PI;
                    if (ang > 110) continue;
                    // intensidade: na cara e perto = 1; virando, cai (60° ~0,55, 90° ~0,2); de longe, cai também
                    float fa = ang <= 20 ? 1 : ang <= 60 ? 1 - 0.45f * (ang - 20) / 40 : ang <= 90 ? 0.55f - 0.35f * (ang - 60) / 30 : 0.2f - 0.15f * (ang - 90) / 20;
                    float I = fa * MathX.Clamp(1 - MathF.Pow(dl / 760f, 1.5f), 0, 1);
                    if (I < 0.05f) continue;
                    // cego total (não vê nada) só com flash forte; depois (ou com flash fraca) fica meio cego
                    float total = I > 0.35f ? 0.4f + 3.6f * I : 0;
                    float parcial = (total > 0 ? 1.2f : 0.6f) + 2f * I;
                    a.blindUntil = MathF.Max(a.blindUntil, t + total);
                    if (total > 0) a.flashDe = n.l.to;
                    a.flashI = a.MeioCego(t) ? MathF.Max(a.flashI, I) : I;
                    a.flashParcialAte = MathF.Max(a.flashParcialAte, t + total + parcial);
                    a.brain.Think(total > 0 ? $"cego {total:0.0}s pela flash{(ang < 25 ? " (na cara)" : "")}{(a.team == n.by.team ? $" do {n.by.name} (aliado!)" : "")}"
                                            : $"meio cego {parcial:0.0}s (flash pegou de lado){(a.team == n.by.team ? $" — do {n.by.name}" : "")}");
                    float dur = total + parcial;
                    if (a.team != n.by.team) { n.by.flashInimigo++; Log($"flash do {n.by.name} cegou {a.name} {dur:0.0}s"); }
                    else { n.by.flashAliado++; Log($"flash do {n.by.name} cegou o aliado {a.name} {dur:0.0}s"); }
                }
            }
        }

        public void PlantStart(Agent a) { Sound(a.pos, a.team, "plantando", 99999, 30); Log($"{a.name} começou a plantar no {planSite}"); }
        public void Plant(Agent a)
        {
            bombPlanted = true; bombPos = a.pos; bombZ = a.z; plantedAt = t; a.hasBomb = false; a.ganho += 300; planter = a;
            planSite = MapData.SITES["A"].Contains(a.pos.x, a.pos.y) || (a.pos - MapData.SITES["A"].Center).Len < (a.pos - MapData.SITES["B"].Center).Len ? 'A' : 'B';
            Log($"BOMB PLANTADO no {planSite} por {a.name} ({RoundLeft:0}s no relógio)");
            ctVivosNoPlant = Alive(Team.CT); trVivosNoPlant = Alive(Team.T);
            intel[(int)Team.CT].Seen(a, t, "bomb");   // o CT sabe onde o bomb foi plantado (e quem estava ali)
        }
        float defuseEnd;
        public int ctVivosNoPlant = -1, trVivosNoPlant = -1;
        public readonly int[] clutchContra = { -1, -1 };   // [time]: ficou 1 contra quantos (−1 = não houve)
        /// <summary>O defuse em andamento termina antes do bomb explodir? (senão o TR nem precisa se expor)</summary>
        public bool DefuseVaiDar => defuserActive && defuseEnd < plantedAt + BOMB;
        /// <summary>Quantos segundos o defuse termina depois da explosão (positivo = não dá tempo pra ele).</summary>
        public float DefuseFolga => defuseEnd - (plantedAt + BOMB);
        public void DefuseStart(Agent a) { defuserActive = true; defuser = a; defuseEnd = t + DefuseTime(a); Sound(a.pos, a.team, "desarmando", 900, 20); Log($"{a.name} começou a desarmar ({BombLeft:0.0}s no bomb)"); }
        public void Defuse(Agent a) { if (!bombPlanted || exploded) return; defused = true; a.ganho += 300; Log($"BOMB DESARMADO por {a.name} ({BombLeft:0.0}s sobrando)"); }

        // ================================================================ ambiente
        public bool SmokeBlocks(V2 a, float za, V2 b, float zb)
        {
            if (smokes.Count == 0) return false;
            float L = (b - a).Len; int n = (int)(L / 6) + 1;
            foreach (var s in smokes)
            {
                var (mn, mx) = s.Bounds;
                for (int i = 1; i < n; i++)
                {
                    float u = i / (float)n; V2 p = a + (b - a) * u; float z = za + (zb - za) * u;
                    if (p.x < mn.x || p.y < mn.y || p.x > mx.x || p.y > mx.y) continue;
                    if (s.Sample(new V3(p.x, p.y, z), t) > 0.35f) return true;
                }
            }
            return false;
        }

        bool SmokeDense(V2 p, float z)
        {
            foreach (var s in smokes)
            {
                var (mn, mx) = s.Bounds;
                if (p.x < mn.x || p.y < mn.y || p.x > mx.x || p.y > mx.y) continue;
                if (s.Sample(new V3(p.x, p.y, z), t) > 0.35f) return true;
            }
            return false;
        }

        public bool SmokeNear(V2 p, float r) => smokes.Any(s => (new V2(s.origin.x, s.origin.y) - p).Len < r + 30 && s.age > 1.2f);

        public bool FireAt(V2 p)
        {
            foreach (var f in fires)
            {
                if (f.Strength <= 0) continue;
                int vis = f.Visible, cx = (int)(p.x / FireArea.C), cy = (int)(p.y / FireArea.C);
                foreach (var c in f.cells) if (c.order < vis && c.x == cx && c.y == cy) return true;
            }
            return false;
        }

        /// <summary>Raio do bomb: dano cai com a distância como no CS (500 no centro, sigma de ~580 unidades do CS = 146
        /// nossas). Colete segura um pouco. Quem morre aqui perde as armas (a Partida não deixa levar).</summary>
        public const float BOMB_SIGMA = 146f;
        public static float DanoBomb(float d, bool colete) => 500f * MathF.Exp(-d * d / (2 * BOMB_SIGMA * BOMB_SIGMA)) * (colete ? 0.75f : 1f);
        void Explosao()
        {
            foreach (var a in agents)
            {
                if (!a.alive) continue;
                int dmg = (int)DanoBomb((a.pos - bombPos).Len, a.armor > 0);
                if (dmg >= a.hp) { Ev(a.IsT ? "TR morreu na explosão/round" : "CT morreu na explosão/round"); a.hp = 0; a.alive = false; a.diedAt = t; a.deaths++; feed.Add((t, $"{a.name} morreu na explosão")); Log($"{a.name} morreu na explosão ({(a.pos - bombPos).Len:0} do bomb)"); }
                else if (dmg > 0) a.hp -= dmg;
            }
        }

        void CheckEnd()
        {
            if (defused) { End(Team.CT, "bomb desarmado"); return; }
            if (bombPlanted && BombLeft <= 0) { exploded = true; Explosao(); End(Team.T, "bomb explodiu"); return; }
            if (Alive(Team.T) == 0 && !bombPlanted) { End(Team.CT, "TR eliminados"); return; }
            if (Alive(Team.CT) == 0) { End(Team.T, "CT eliminados"); return; }
            if (!bombPlanted && t >= ROUND) End(Team.CT, "tempo acabou");
        }

        void End(Team w, string why) { over = true; winner = w == Team.T ? "TR" : "CT"; reason = why; Log($"FIM: {winner} ganhou — {why}"); }
    }
}
