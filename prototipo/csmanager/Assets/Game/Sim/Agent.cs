using System;
using System.Collections.Generic;
using CSM.Map;

namespace CSM.Sim
{
    public enum Team { T, CT }
    public enum Role { Entry, Suporte, Lurker, Awper, Igl, Ancora, Rotacao }
    public enum Move { Parado, Andando, Correndo, Agachado }

    /// <summary>Habilidade do jogador (pro players: tudo alto; depois vira status de cada um).</summary>
    public class Skill
    {
        public float reaction = 0.19f;      // s pra reagir a um inimigo que aparece de frente
        public static float MIRA_PADRAO = 0.65f;
        public float aimSigma = MIRA_PADRAO;   // erro de mira natural do jogador (graus)
        public float flick = 900f;          // graus por segundo virando a mira
        public float spray = 0.85f;         // quanto compensa do recuo (0..1)
        public float crosshair = 0.8f;      // capricho em deixar a mira na altura da cabeça e no ângulo certo
        public float pitchErr = 2.4f;     // erro vertical (graus) da mira quando o inimigo aparece; corrige em ~0,3 s
        public float aggression = 0.5f;     // 0 = passivo, 1 = agressivo
        public float composure = 0.8f;      // erra menos no clutch / sob pressão
        public float utilitaria = 0.8f;     // execução de granada: sabe o pixel, mas quanto acerta (0..1)
    }

    /// <summary>Um jogador: corpo, arma, movimento, mira, estado. O que ele decide está no Brain.</summary>
    public class Agent
    {
        public int id; public string name; public Team team; public Role role; public Skill skill = new Skill();
        public Brain brain;
        // corpo
        public bool alive = true; public int hp = 100, armor = 100; public bool helmet = true;
        public V2 pos, vel; public float z;
        public bool noAr; public float vz, quedaDe;   // pulando/caindo: velocidade vertical e de que altura saiu
        public float pularPara;                       // o cérebro pediu pulo pra essa altura (degrau logo à frente)
        public float aim, aimPitch;            // pra onde olha (rad) e altura da mira (unidades, relativo ao olho)
        public Move move = Move.Parado; public bool crouch;
        public float blindUntil = -1; public V2 flashDe;   // onde estourou a flash que cegou
        public V2 dirAnt; public float dirAntT = -9; public int vaiVoltaFrente, vaiVoltaLado;   // inverteu o rumo em <0,7 s (validador)
        public int popFlash;
        public float panico;                    // 0..1: situação ruim x sangue-frio (Brain.Panico)
        public int afobado, frio;               // peek afobado no pânico / jogada fria no clutch (validador)
        public int tiroCego, recuoCego, agachaCego, cantoIgnorado, prefireMemoria;   // validador
        // arma
        public string gunId; public WeaponStats.Gun gun; public bool scoped;
        public int spray;                      // bala do spray atual (zera quando para de atirar)
        public float aimErrV;                  // erro vertical atual da mira (graus), vem do Brain ao achar um alvo
        public float lastShotT = -9; public bool killedByFire;
        public List<string> nades = new List<string>();
        public bool hasBomb, hasKit = true;
        // estatística do round/jogo
        public int kills, deaths, hs, shots, hits, damage, firstKills, firstDeaths, traded, tradeKills;
        public int ganho;                       // dinheiro ganho no round (abates, plant, defuse) — a Partida paga
        public bool guardando;                  // decidiu guardar a arma neste round (não procura duelo)
        public float avisoFlashAte = -9;        // ouviu "bang!" do aliado: vira a cara até esse tempo
        public float flashParcialAte = -9, flashI;   // meio cego (vê só de perto, reage devagar, mira treme) e quão forte foi
        public float bangAmigaEm = -9; public V2 bangAmigaPos; public Agent bangAmigaDe;   // aliado avisou "vou bangar"
        public bool MeioCego(float t) => t < flashParcialAte;
        public List<int> visCache; public V2 visPos; public float visAim; public int visSmokes;   // o que viu da última vez (Match.UpdateIntel)
        // utilitária (validador)
        public int flashes, smokes, molotovs, flashInimigo, flashAliado, pediuBang, bangou, hes, heDano, heFuro, nadeErrada, nadeErrou, pixelSmoke, dropou, revelou, puniu;
        public float semCaminho;                // tempo querendo ir a um lugar sem caminho (bug de navmesh/destino)
        public float diedAt = -1; public Agent killedBy;
        // atividade (validador): tempo vivo/parado/visto pelo inimigo, jogadas feitas, calor que limpou (info)
        public float tVivo, tParado, tParadoLongo, tExposto, paradoHa, info;
        public int peeks, trocas, avancos; public bool vistoAgora;
        public readonly Pesos pesos;

        public Agent(int id, string name, Team team, Role role, string gun, Pesos pesos)
        {
            this.id = id; this.name = name; this.team = team; this.role = role; gunId = gun; this.pesos = pesos;
            this.gun = new WeaponStats.Gun(WeaponStats.ALL[gun]);
        }

        public bool IsT => team == Team.T;
        public bool Blind(float t) => t < blindUntil;
        public float Eye => crouch ? 13f : 18f;
        /// <summary>Velocidade máxima com a arma (CS2: faca 250, AK 215, M4 225, AWP 200, SMG/pistola 240) na nossa escala.</summary>
        public float MaxSpeed => MapCheck.RUN * (faca ? 1f : gunId switch { "ak" => 0.86f, "m4" => 0.9f, "m4s" => 0.9f, "awp" => scoped ? 0.4f : 0.8f, _ => 0.96f });
        /// <summary>Faca na mão (corre mais: CS2 faca 250 x AK 215) — longe de onde o inimigo pode estar. Pra atirar
        /// precisa sacar a arma, e sacar leva um tempo.</summary>
        public bool faca; public float sacaAte = -9;
        public float TempoSacar => gunId == "awp" ? 0.75f : gunId == "deagle" ? 0.6f : WeaponStats.Pistola(gunId) || gunId == "mp9" || gunId == "mac10" ? 0.45f : 0.55f;
        public bool ArmaPronta(float t) => !faca && t >= sacaAte;
        public float Speed => vel.Len;
        /// <summary>Andando rápido demais pra atirar com precisão (CS: acima de ~34% da velocidade máxima).</summary>
        public bool Inaccurate => Speed > MaxSpeed * 0.34f;
        /// <summary>Atira andando sem pagar caro: SMG e pistola (menos Deagle). Rifle, AWP e Deagle precisam parar e assentar a mira.</summary>
        public bool ArmaLeve => gunId == "mp9" || gunId == "mac10" || WeaponStats.Pistola(gunId) && gunId != "deagle";
        public V2 c4Pos; public float c4Desde = -1;   // com o C4: desde quando não sai de perto do mesmo lugar
        public float rapidoEm = -9;   // última vez acima da velocidade de tiro preciso (a mira leva um instante pra assentar)
    }

    /// <summary>
    /// Tiro: precisão da arma + do jogador, recuo do spray (compensado pela habilidade), penalidade de andar, mira na
    /// cabeça/peito/pé e resolução no hitbox do boneco (cabeça, tronco, pernas) com dano de CS2 (colete e capacete).
    /// </summary>
    public static class Combat
    {
        // dano base, penetração de colete, precisão parado (graus) — CS2
        static readonly Dictionary<string, (int dmg, float pen, float acc, float runAcc)> W = new Dictionary<string, (int, float, float, float)>
        {
            ["ak"] = (36, 0.775f, 0.42f, 4.5f), ["m4"] = (33, 0.70f, 0.36f, 4.0f), ["m4s"] = (38, 0.70f, 0.32f, 3.8f),
            ["awp"] = (115, 0.975f, 0.06f, 6.0f), ["mp9"] = (26, 0.60f, 0.65f, 1.6f), ["mac10"] = (29, 0.575f, 0.8f, 1.8f),
            ["usp"] = (35, 0.505f, 0.4f, 1.6f), ["glock"] = (30, 0.47f, 0.55f, 1.5f),
            ["deagle"] = (53, 0.932f, 0.3f, 3.0f), ["p250"] = (38, 0.64f, 0.5f, 1.6f),
        };

        public const float H = 25f;   // altura do boneco em pé
        /// <summary>
        /// Escala da imprecisão da arma. As proporções vêm do CS2 (weapons.vdata: AK x M4 x SMG, parado x agachado x
        /// andando, penalidade e recuperação), mas o nosso mundo é outro (mapa ~1/4 do CS, boneco chibi maior em
        /// proporção, câmera de cima): a escala absoluta é calibrada pelo resultado (HS, precisão, tempo pra matar).
        /// </summary>
        public static float ESCALA_ARMA = 5f;   // calibrado: 1 = HS 85%; 5 = ~65% com AWP equilibrada; 7 = 61% mas a AWP domina
        public enum Part { Nada, Cabeca, Tronco, Perna }

        /// <summary>Altura (acima do pé) onde o atirador mira: cabeça perto/médio, peito longe ou de AWP, e no spray
        /// desce pro peito depois das primeiras balas (o recuo sobe; pro segura o spray no corpo).</summary>
        public static float AimHeight(Agent a, float dist, bool targetCrouch)
        {
            float k = targetCrouch ? 0.72f : 1f;
            bool head = a.gunId != "awp" && dist < 450 && a.spray < a.pesos.sprayPeito;
            return (head ? 21.5f : 15f) * k;
        }

        /// <summary>Atira em direção ao ponto mirado; devolve onde a bala passou e quem (e onde) acertou.</summary>
        public static (Agent victim, Part part, V2 end, float endZ) Fire(Match m, Agent a, V2 aimPoint, float aimZ, Rng rng)
        {
            var s = a.gun.s;
            float dist = MathF.Max(1, (aimPoint - a.pos).Len);
            // ---- a ARMA (modelo do CS2, números do weapons.vdata): imprecisão parado/agachado (AWP: na luneta ou não)
            // + penalidade de tiro acumulada + andando (cheia na velocidade máxima, começa a partir de 34% dela); a bala
            // sai num ponto aleatório do cone (raio sorteado) + o spread da arma
            bool luneta = a.gunId == "awp" && a.scoped;
            float inac = (a.crouch ? (luneta ? s.inCrouchAlt : s.inCrouch) : (luneta ? s.inStandAlt : s.inStand)) + a.gun.penalidade;
            float vr = MathX.Clamp((a.Speed - 0.34f * a.MaxSpeed) / (0.61f * a.MaxSpeed), 0, 1);
            if (vr > 0) inac += s.inMove * MathF.Pow(vr, 0.25f);
            else if (!a.ArmaLeve && !TeamBrain.EXP.Contains("semAssentar"))
                inac += 0.35f * s.inMove * MathF.Max(0, 1 - (m.t - a.rapidoEm) / 0.2f);   // acabou de frear: rifle ainda não assentou
            inac *= ESCALA_ARMA;   // o nosso mundo não é o do CS: proporções entre armas/situações do CS2, escala calibrada aqui
            float r1 = rng.Rand() * inac, t1 = rng.Uniform(0, 6.2832f), r2 = rng.Rand() * s.spread, t2 = rng.Uniform(0, 6.2832f);
            const float RAD = 57.29578f;
            float armaL = (r1 * MathF.Cos(t1) + r2 * MathF.Cos(t2)) * RAD, armaV = (r1 * MathF.Sin(t1) + r2 * MathF.Sin(t2)) * RAD;
            // ---- o JOGADOR: erro de mira (habilidade), meio cego treme, recoil que ele não compensou (força do recoil da arma)
            float sig = a.skill.aimSigma;
            if (a.MeioCego(m.t)) sig *= 1 + 1.5f * a.flashI * a.flashI;   // meio cego: a mira treme (fraca: só um pouquinho)
            sig *= 1 + 0.5f * a.panico;                                     // pânico: mão tremendo
            // AWP: um tiro só e sem spray pra corrigir — pré-mirado é mortal, mas flick de luneta erra bem mais (a AWP
            // que dá peek e precisa achar o alvo perde pra quem já estava mirando; a que segura o ângulo continua forte)
            if (a.gunId == "awp" && m.t - a.brain.alvoDesde < 0.9f) sig *= 1 + MathX.Clamp((a.brain.flickAlvo - 4f) / 12f, 0, 1.6f);   // 2º tiro, já em cima: normal
            float recoilV = 0, recoilL = 0;
            if (a.spray > 0 && a.gunId != "awp")
            {
                int i = Math.Min(a.spray, 12);
                float mag = 0.55f * s.recoil / 30f;   // AK = referência
                recoilV = i * mag * (1 - a.skill.spray);
                recoilL = MathF.Sin(a.spray * 0.7f) * 1.4f * (s.recoilVar / 70f) * (1 - a.skill.spray) * MathF.Min(1, a.spray / 8f);
            }
            float eL = armaL + rng.Normal(0, sig) + recoilL, eV = armaV + rng.Normal(0, sig) + recoilV + a.aimErrV;
            a.gun.penalidade += s.inFire;   // cada tiro abre o cone (recupera com o tempo: Gun.Recupera)
            // trajetória: reta do olho do atirador na direção mirada + erro
            float ang = (aimPoint - a.pos).Atan2 + eL * MathF.PI / 180f;
            V2 dir = V2.Ang(ang);
            float z0 = a.z + a.Eye;
            float zAt(float d) => z0 + (aimZ - z0) * (d / dist) + d * MathF.Tan(eV * MathF.PI / 180f);
            // anda pela linha até bater em parede ou num boneco
            for (float d = 4; d < 1400; d += 2)
            {
                V2 p = a.pos + dir * d; float zz = zAt(d);
                int x = (int)p.x, y = (int)p.y;
                if (!MapData.InMap(x, y) || MapData.Top(x, y) > zz) return (null, Part.Nada, p, zz);
                foreach (var b in m.agents)
                {
                    if (!b.alive || b == a) continue;
                    V2 rel = b.pos - p;
                    if (rel.Len > 5f) continue;
                    float hz = zz - b.z, k = b.crouch ? 0.72f : 1f;
                    float lat = MathF.Abs(rel.x * -dir.y + rel.y * dir.x);
                    // hitbox interna: o desenho chibi tem a cabeça grande, mas a área que conta como cabeça no jogo fica
                    // dentro dela, na proporção do CS (~35% da largura do tronco) — senão o HS vai lá pra cima
                    if (hz > 19.6f * k && hz < 23.4f * k && lat < 1.7f) return (b, Part.Cabeca, p, zz);
                    if (hz > 9f * k && hz <= 19.3f * k && lat < 4.6f) return (b, Part.Tronco, p, zz);
                    if (hz >= 0 && hz <= 9f * k && lat < 3.6f) return (b, Part.Perna, p, zz);
                }
            }
            return (null, Part.Nada, a.pos + dir * 1400, zAt(1400));
        }

        /// <summary>Dano no corpo com colete/capacete (CS2: cabeça x4, perna x0,75, colete reduz pelo armor pen).</summary>
        public static int Damage(Agent shooter, Agent victim, Part part)
        {
            var w = W[shooter.gunId];
            float d = w.dmg * (part == Part.Cabeca ? shooter.gun.s.hsMult : part == Part.Perna ? 0.75f : 1f);   // M4A1-S: 3,475 na cabeça
            bool armored = part == Part.Tronco ? victim.armor > 0 : part == Part.Cabeca && victim.helmet;
            if (armored) d *= w.pen;
            return (int)MathF.Round(d);
        }
    }
}
