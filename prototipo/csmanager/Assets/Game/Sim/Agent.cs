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
        public float miraAwp = 1f, miraPistola = 1f;   /* status (Jogadores.cs): erro de mira com AWP/pistola relativo à mira de rifle (1 = igual) */

        /// <summary>
        /// Estilo de jogador pela função (arquétipos do cenário pro): entry agressivo que abre (tipo apEX), AWPer calmo e
        /// preciso (tipo FalleN), lurker paciente, suporte da utilitária, IGL que pensa pelo time (mira um pouco pior),
        /// âncora do CT que segura calmo. A MÉDIA de cada time fica igual ao jogador padrão — muda o jeito, não a força.
        /// estilosVelho = todo mundo igual (como era).
        /// </summary>
        public static Skill Estilo(Role r)
        {
            var s = new Skill();
            if (!TeamBrain.EXPF.Contains("estilos") || TeamBrain.EXPF.Contains("estilosVelho")) return s;   /* opt-in: sozinho mexia o retake (−7); volta com os status dos jogadores */
            switch (r)
            {
                case Role.Entry: s.aggression = 0.82f; s.composure = 0.66f; s.aimSigma = 0.62f; s.reaction = 0.18f; s.utilitaria = 0.7f; break;
                case Role.Awper: s.aggression = 0.38f; s.composure = 0.9f; s.aimSigma = 0.61f; s.utilitaria = 0.75f; break;
                case Role.Lurker: s.aggression = 0.3f; s.composure = 0.9f; s.utilitaria = 0.75f; break;
                case Role.Suporte: s.aggression = 0.45f; s.composure = 0.8f; s.aimSigma = 0.68f; s.utilitaria = 0.95f; break;
                case Role.Igl: s.aggression = 0.55f; s.composure = 0.78f; s.aimSigma = 0.72f; s.reaction = 0.2f; s.utilitaria = 0.9f; break;
                case Role.Ancora: s.aggression = 0.35f; s.composure = 0.88f; s.utilitaria = 0.85f; break;
                case Role.Rotacao: s.aggression = 0.6f; s.composure = 0.75f; s.utilitaria = 0.8f; break;
            }
            return s;
        }
    }

    /// <summary>Um jogador: corpo, arma, movimento, mira, estado. O que ele decide está no Brain.</summary>
    public class Agent
    {
        public int id; public string name; public Team team; public Role role; public Skill skill = new Skill();
        public Brain brain;
        // corpo
        public bool alive = true; public int hp = 100, armor = 100; public bool helmet = true;
        public V2 pos, vel; public float z;
        public Agent travadoPor; public float travadoDesde = -9, travadoUlt = -9, daPassagemAte = -9; public V2 daPassagemDir;   // body block
        public bool noAr, puloAgachado; public float vz, quedaDe;
        /* pezinho (boost): dandoPezinho = agachado encostado na pilha servindo de degrau; subindoPezinho = quem vai subir
           (a física deixa o pulo chegar no topo da pilha só com o colega agachado colado). O visual lê os dois */
        public bool dandoPezinho, subindoPezinho; public float naPilhaAte = -9, saltandoAte = -9; public V2 saltoAlvo; public float saltoZ;   /* saltandoAte: pulo de salto por cima de vão (a física deixa passar sobre o mais baixo) */
        /* pezinho como no CS: o boneco é chão pra quem está em cima dele. emCimaDe = em quem estou em pé (o z já é o do topo
           dele); AlturaCorpo = onde fica a cabeça (em pé 25; agachado ~18,75: 54/72 do CS) */
        public Agent emCimaDe;
        public float AlturaCorpo => 25f - 6.25f * duck;   // pulando/caindo: velocidade vertical e de que altura saiu
        public float subindoDegrauAte = -9;            // pediu pulo pro degrau há pouco: a folga da parede não empurra (borda estreita da caixa)
        public float pularPara;                       // o cérebro pediu pulo pra essa altura (degrau logo à frente)
        public float aim, aimPitch;            // pra onde olha (rad) e altura da mira (unidades, relativo ao olho)
        public float aimV;                     /* ângulo vertical da mira (rad; + pra cima): na altura da cabeça de quem estaria onde ele olha */
        public Move move = Move.Parado; public bool crouch;
        public float blindUntil = -1; public V2 flashDe;   // onde estourou a flash que cegou
        // pro killfeed (Match.abates): quem jogou a flash que me cegou (total), dano que levei de cada um neste round e se a
        // AWP estava na luneta no último tiro (o tiro tira da luneta antes do dano)
        public Agent flashPor; public readonly int[] danoDe = new int[10]; public bool lunetaNoTiro;
        public V2 dirAnt; public float dirAntT = -9; public int vaiVoltaFrente, vaiVoltaLado;   // inverteu o rumo em <0,7 s (validador)
        public int popFlash;
        public float panico;                    // 0..1: situação ruim x sangue-frio (Brain.Panico)
        public int afobado, frio;               // peek afobado no pânico / jogada fria no clutch (validador)
        public int tiroCego, recuoCego, agachaCego, cantoIgnorado, prefireMemoria;   // validador
        // arma
        public string gunId; public WeaponStats.Gun gun; public bool scoped;
        // inventário como no CS: a primária e a pistola (a que não está na mão fica guardada). Sem primária, outraId = null
        public string outraId; public WeaponStats.Gun outraGun; public int trocouArma;
        public bool somRecarga, somFaca; public int somTrocou = -1;   // sons de ação: o estado no passo anterior (Match.SonsDeEstado)
        public bool TemOutra => outraId != null;
        /// <summary>A primária (rifle/SMG/AWP), esteja na mão ou guardada; null se ele só tem pistola.</summary>
        public string Primaria => !WeaponStats.Pistola(gunId) ? gunId : outraId != null && !WeaponStats.Pistola(outraId) ? outraId : null;
        public string Pistola => WeaponStats.Pistola(gunId) ? gunId : outraId != null && WeaponStats.Pistola(outraId) ? outraId : null;
        /// <summary>Troca pra arma guardada (Q do CS): sai da luneta, a recarga em andamento se perde e a nova leva o tempo de sacar.</summary>
        public void TrocarArma(float t)
        {
            if (outraId == null) return;
            gun.reloadLeft = 0;
            (gunId, outraId) = (outraId, gunId); (gun, outraGun) = (outraGun, gun);
            scoped = false; spray = 0; faca = false; sacaAte = t + (TeamBrain.EXPF.Contains("sacarTrocaCurta") ? TempoSacarCurto : TempoSacar); trocouArma++;
        }
        public int spray;                      // bala do spray atual (zera quando para de atirar)
        public float aimErrV;                  // erro vertical atual da mira (graus), vem do Brain ao achar um alvo
        public float lastShotT = -9, cliqueT = -9; public bool killedByFire;
        public List<string> nades = new List<string>();
        /// <summary>O que ele comprou pro colega e ainda vai dropar (começo do round): pra quem e o quê (arma ou granada).</summary>
        public List<(Agent para, string item)> entregar = new List<(Agent, string)>();
        public bool hasBomb, hasKit = true;
        // estatística do round/jogo
        public int kills, deaths, hs, shots, hits, damage, firstKills, firstDeaths, traded, tradeKills;
        public int ganho;                       // dinheiro ganho no round (abates, plant, defuse) — a Partida paga
        public float apanhouEm = -99;           // último tiro que levou
        public Agent apanhouDe; public V2 apanhouDePos;   // quem deu esse tiro e de onde (crença 3: o colega que toma tiro diz de onde veio)
        public bool guardando;                  // decidiu guardar a arma neste round (não procura duelo)
        public bool vivoNoFim, salvaPos;         // vivo no instante em que o round acabou (dinheiro); salvando no pós-round (não é "guardou")
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
        public float Eye => 18f - 5f * duck;
        /// <summary>Agachar como no CS2: crouch é a vontade (o cérebro aperta); duck é o corpo (0 em pé, 1 agachado) e anda
        /// com a velocidade duckVel. Cada apertada nova tira 2 da velocidade (spam de agachar fica lento: 1ª normal, 2ª e 3ª
        /// mais lentas, depois bem lento) e ela volta 3/s até 8 (CS: CS_PLAYER_DUCK_SPEED_IDEAL). Mira e recuo de agachado só
        /// valem com o corpo inteiro embaixo (FL_DUCKING).</summary>
        public float duckCorpo, duckVel = DUCK_IDEAL; bool crouchAntes;
        public bool CrouchAntes => crouchAntes;
        public static bool AgacharVelho => AgVelho;
        /// <summary>Quanto o corpo está agachado (0..1). Com agacharVelho é o botão na hora (como era antes).</summary>
        public float duck => AgVelho ? (crouch ? 1 : 0) : duckCorpo;
        static string agExp; static bool agVelho;
        static bool AgVelho { get { var e = TeamBrain.EXP; if (!ReferenceEquals(e, agExp)) { agExp = e; agVelho = e.Contains("agacharVelho"); } return agVelho; } }
        public const float DUCK_IDEAL = 8f, DUCK_POR_APERTO = 2f, DUCK_MIN = 1.5f, DUCK_VOLTA = 3f, DUCK_RITMO = 0.6f;
        public bool Agachado => AgVelho ? crouch : duckCorpo >= 0.999f;
        public int apertosAgachar;   // validador: quantas vezes apertou no round
        /// <returns>0 nada, 1 apertou, 2 apertou já com o agachar lento (spam)</returns>
        public int Agachar(float dt)
        {
            if (AgVelho) return 0;
            int r = 0;
            if (crouch && !crouchAntes) { r = duckVel < 5f ? 2 : 1; duckVel = MathF.Max(DUCK_MIN, duckVel - DUCK_POR_APERTO); apertosAgachar++; }
            crouchAntes = crouch;
            float passo = duckVel * (TeamBrain.EXPF.Contains("duckRapido") ? 100f : DUCK_RITMO) * dt, alvo = crouch ? 1 : 0;
            duckCorpo = duckCorpo < alvo ? MathF.Min(alvo, duckCorpo + passo) : MathF.Max(alvo, duckCorpo - passo);
            duckVel = MathF.Min(DUCK_IDEAL, duckVel + DUCK_VOLTA * dt);
            return r;
        }
        /// <summary>Velocidade máxima com a arma (CS2: faca 250, AK 215, M4 225, AWP 200, SMG/pistola 240) na nossa escala.</summary>
        public float MaxSpeed => MapCheck.RUN * (faca ? 1f : gunId switch { "ak" => 0.86f, "m4" => 0.9f, "m4s" => 0.9f, "galil" => 0.86f, "famas" => 0.88f, "awp" => scoped ? 0.4f : 0.8f, _ => 0.96f });   // Galil 215, FAMAS 220
        /// <summary>Faca na mão (corre mais: CS2 faca 250 x AK 215) — longe de onde o inimigo pode estar. Pra atirar
        /// precisa sacar a arma, e sacar leva um tempo.</summary>
        public bool faca; public float sacaAte = -9, ultArremesso = -9;
        /// <summary>Granada já sacada chegando na lineup (pro chega com ela na mão): qual e desde quando. Enquanto isso a arma
        /// não está na mão — se aparecer alguém, tem que sacar.</summary>
        public string nadeNaMao; public float nadeNaMaoDesde = -9;
        /// <summary>Sacar até poder atirar: CS2 weapons.vdata m_flDeployDuration (quase tudo 1,0; M4 1,13, AWP 1,27, MP9 1,2,
        /// Galil 1,1). sacarVelho = os tempos curtos de antes.</summary>
        public float TempoSacarCurto => gunId == "awp" ? 0.75f : gunId == "deagle" ? 0.6f : WeaponStats.Pistola(gunId) || gunId == "mp9" || gunId == "mac10" ? 0.45f : 0.55f;
        public float TempoSacar => TeamBrain.EXPF.Contains("sacarVelho")
            ? TempoSacarCurto
            : gunId switch { "awp" => 1.267f, "m4" or "m4s" => 1.133f, "mp9" => 1.2f, "galil" => 1.1f, _ => 1.0f };
        public bool ArmaPronta(float t) => !faca && t >= sacaAte;
        public float Speed => vel.Len;
        /// <summary>Andando rápido demais pra atirar com precisão (CS: acima de ~34% da velocidade máxima).</summary>
        public bool Inaccurate => Speed > MaxSpeed * 0.34f;
        /// <summary>Atira andando sem pagar caro: SMG e pistola (menos Deagle). Rifle, AWP e Deagle precisam parar e assentar a mira.</summary>
        public bool ArmaLeve => gunId == "mp9" || gunId == "mac10" || WeaponStats.Pistola(gunId) && gunId != "deagle";
        public V2 c4Pos; public float c4Desde = -1;   // com o C4: desde quando não sai de perto do mesmo lugar
        public float rapidoEm = -9;   // última vez acima da velocidade de tiro preciso (a mira leva um instante pra assentar)
        // pulo de deslocamento (strafe jump / bunny hop): o cérebro pede, a física do ar é a do Source
        public float pedidoAcum;   // quanto o cérebro pediu pra andar desde a última vez que saiu do lugar (detector de travado)
        public bool querPular, puloLivre; public float pousouEm = -9; public int pulosSeguidos, pulos;
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
            ["tec9"] = (33, 0.906f, 0.55f, 1.5f), ["fiveseven"] = (32, 0.9115f, 0.5f, 1.6f),
            ["galil"] = (30, 0.775f, 0.40f, 4.2f), ["famas"] = (30, 0.70f, 0.38f, 4.0f),
        };

        public const float H = 25f;   // altura do boneco em pé
        /// <summary>
        /// Escala da imprecisão da arma. As proporções vêm do CS2 (weapons.vdata: AK x M4 x SMG, parado x agachado x
        /// andando, penalidade e recuperação), mas o nosso mundo é outro (mapa ~1/4 do CS, boneco chibi maior em
        /// proporção, câmera de cima): a escala absoluta é calibrada pelo resultado (HS, precisão, tempo pra matar).
        /// </summary>
        public static bool ArmaExataParada => TeamBrain.EXPF.Contains("armaExataParada");   /* opt-in até calibrar a mira da mão */
        public static float ESCALA_ARMA = 5f;   // calibrado: 1 = HS 85%; 5 = ~65% com AWP equilibrada; 7 = 61% mas a AWP domina
        public enum Part { Nada, Cabeca, Tronco, Perna }

        /// <summary>Altura (acima do pé) onde o atirador mira: cabeça perto/médio, peito longe ou de AWP, e no spray
        /// desce pro peito depois das primeiras balas (o recuo sobe; pro segura o spray no corpo).</summary>
        public static float AimHeight(Agent a, float dist, bool targetCrouch) => AimHeight(a, dist, targetCrouch ? 1f : 0f);
        /// <summary>Altura da mira pelo quanto o alvo está agachado (0..1): a cabeça desce junto com o corpo.</summary>
        public static float AimHeight(Agent a, float dist, float duckAlvo)
        {
            bool head = a.gunId != "awp" && dist < 450 && a.spray < a.pesos.sprayPeito;
            if (HitboxTela) return head ? CabecaZ(duckAlvo) : PeitoZ(duckAlvo);
            float k = 1f - 0.28f * duckAlvo;
            return (head ? 21.5f : 15f) * k;
        }
        /// <summary>
        /// Hitbox no boneco que a TELA desenha (medido no modelo exportado, unidades da Sim, z do pé): cabeça com centro
        /// 19,5 em pé e 16,8 agachado; tronco z 7-16,5 (centro 11,7); pernas até ~7. Agachar desce tudo ~2,7. A área que
        /// conta como cabeça fica DENTRO da cabeça chibi desenhada, centrada nela, no tamanho do CS (o HS não vira o
        /// cabeção). -exp hitboxTelaVelho: a antiga (cabeça 19,6-23,4, centro 21,5, acima da desenhada).
        /// </summary>
        public static bool HitboxTela => !TeamBrain.EXPF.Contains("hitboxTelaVelho");
        public static float CabecaZ(float duck) => 19.5f - 2.7f * duck;
        public static float PeitoZ(float duck) => 12.5f - 2.6f * duck;

        static (Agent, Part, V2, float) Acertou(Match m, Agent a, Agent b, Part p, V2 fim, float z)
        {
            if (a.gunId == "awp") { m.Ev("AWP acertou/round"); if (a.brain.target != null) m.Aud("AWP: acerto por distância", Faixa((a.brain.target.pos - a.pos).Len) + " acertou"); }
            return (b, p, fim, z);
        }
        static string Faixa(float d) => d < 150 ? "a perto <150" : d < 300 ? "b médio 150-300" : d < 500 ? "c longe 300-500" : "d muito longe 500+";

        /// <summary>Atira em direção ao ponto mirado; devolve onde a bala passou e quem (e onde) acertou.</summary>
        public static (Agent victim, Part part, V2 end, float endZ) Fire(Match m, Agent a, V2 aimPoint, float aimZ, Rng rng)
        {
            var s = a.gun.s;
            float dist = MathF.Max(1, (aimPoint - a.pos).Len);
            // ---- a ARMA (modelo do CS2, números do weapons.vdata): imprecisão parado/agachado (AWP: na luneta ou não)
            // + penalidade de tiro acumulada + andando (cheia na velocidade máxima, começa a partir de 34% dela); a bala
            // sai num ponto aleatório do cone (raio sorteado) + o spread da arma
            bool luneta = a.gunId == "awp" && a.scoped;
            float inac = (a.Agachado ? (luneta ? s.inCrouchAlt : s.inCrouch) : (luneta ? s.inStandAlt : s.inStand)) + a.gun.penalidade;
            float vr = MathX.Clamp((a.Speed - 0.34f * a.MaxSpeed) / (0.61f * a.MaxSpeed), 0, 1);
            if (vr > 0) inac += s.inMove * MathF.Pow(vr, 0.25f);
            /* -exp assentaFisicaLigado: no CS2 a precisão depende só da velocidade NA HORA do tiro. Esta penalidade (0,2 s depois de
               andar rápido) foi criada pra quem já está parado ganhar de quem chega correndo — mas o ADAD do duelo (anda de lado
               e solta a bala na virada) cai sempre dentro dela: com as flags de duelo a precisão a 10-20 m ia de 35% pra 27% (pro
               37%) e a 20-30 m de 22% pra 15% (pro 26%); sem ela volta a 32% e 22%. Quem chega correndo continua pagando na
               reação (ReactionTime) e no tempo de frear, que é o que acontece no jogo */
            else if (!a.ArmaLeve && !TeamBrain.EXPF.Contains("semAssentar") && !TeamBrain.EXPF.Contains("assentaFisicaLigado"))
                inac += 0.35f * s.inMove * MathF.Max(0, 1 - (m.t - a.rapidoEm) / 0.2f);   // acabou de frear: rifle ainda não assentou
            if (a.gunId == "awp" && a.brain.target != null) m.Aud("AWP: acerto por distância", Faixa((a.brain.target.pos - a.pos).Len) + " tiro");
            if (a.gunId == "awp") m.Ev(!luneta ? "AWP tiro sem luneta/round" : vr > 0 ? "AWP tiro na luneta andando/round" : "AWP tiro na luneta parado/round");
            if (luneta && vr <= 0 && !TeamBrain.EXPF.Contains("awpVelha")) inac = (a.Agachado ? s.inCrouchAlt : s.inStandAlt) / ESCALA_ARMA;   // parada na luneta é exata (cancela a escala abaixo): quem erra é a mão
            bool rifleMao = Brain.MaoAssenta && (a.gunId == "ak" || a.gunId == "m4" || a.gunId == "m4s" || a.gunId == "galil" || a.gunId == "famas");   /* bx: mão que assenta (só rifle) */
            if (ArmaExataParada && a.gunId != "awp" || rifleMao)
            {   /* teste armaExataParada: parado, a 1ª bala sai como no CS2 (o cone de parado/agachado sem a escala do nosso mundo);
                   a escala fica só no que é da arma se mexendo e atirando (penalidade do tiro e andando). Quem erra o tiro parado
                   é a mão (aimSigma = status Mira), não a arma: assim um mira 20 faz a jogada de spray/pinada que um 10 não faz */
                float baseParado = a.Agachado ? s.inCrouch : s.inStand;
                inac = baseParado * TeamBrain.ExpNum("escalaParada", 1f) + (inac - baseParado) * ESCALA_ARMA;
            }
            else inac *= ESCALA_ARMA;   // o nosso mundo não é o do CS: proporções entre armas/situações do CS2, escala calibrada aqui
            float r1 = rng.Rand() * inac, t1 = rng.Uniform(0, 6.2832f), r2 = rng.Rand() * s.spread, t2 = rng.Uniform(0, 6.2832f);
            const float RAD = 57.29578f;
            float armaL = (r1 * MathF.Cos(t1) + r2 * MathF.Cos(t2)) * RAD, armaV = (r1 * MathF.Sin(t1) + r2 * MathF.Sin(t2)) * RAD;
            // ---- o JOGADOR: erro de mira (habilidade), meio cego treme, recoil que ele não compensou (força do recoil da arma)
            float sig = a.skill.aimSigma * (a.gunId == "awp" ? a.skill.miraAwp : WeaponStats.Pistola(a.gunId) ? a.skill.miraPistola : 1f);   /* sem ficha os dois são 1 */
            if (a.MeioCego(m.t)) sig *= 1 + 1.5f * a.flashI * a.flashI;   // meio cego: a mira treme (fraca: só um pouquinho)
            sig *= 1 + 0.5f * a.panico;                                     // pânico: mão tremendo
            if (rifleMao)
            {   /* bx: o cone parado ficou exato: o erro que ele tinha (raio uniforme até R×ESCALA; por eixo R²/6) vai pra mão, e a
                   mão pesa a situação (apressada com ameaça, assenta sem) */
                float bp = (a.Agachado ? s.inCrouch : s.inStand) * RAD * TeamBrain.ExpNum("escalaParada", 1f);
                float extra = bp * MathF.Sqrt(MathF.Max(0f, ESCALA_ARMA * ESCALA_ARMA - 1f) / 6f);
                sig = MathF.Sqrt(sig * sig + extra * extra) * a.brain.MaoPressa();
            }
            // AWP: um tiro só e sem spray pra corrigir — pré-mirado é mortal, mas flick de luneta erra bem mais (a AWP
            // que dá peek e precisa achar o alvo perde pra quem já estava mirando; a que segura o ângulo continua forte)
            if (a.gunId == "awp" && m.t - a.brain.alvoDesde < 0.9f) sig *= 1 + MathX.Clamp((a.brain.flickAlvo - 4f) / 12f, 0, 1.6f);   // 2º tiro, já em cima: normal
            // AWP de longe: a arma é exata, a mão não — o boneco vira um risco na luneta e qualquer tremida passa do lado.
            // Perto/médio quase não muda; de 250 em diante o erro cresce (de muito longe acertar é raro)
            if (a.gunId == "awp" && luneta)
            {
                float k = TeamBrain.ExpNum("awpLonge", 0f);   // desligado: 0,35/0,7/1,4 deixam o TR em 55-59% (a AWP sustenta o CT — volta junto com ajuste do CT, ver backlog)
                sig *= 1 + k * MathX.Clamp((dist - 250f) / 250f, 0, 2);
            }
            float recoilV = 0, recoilL = 0;
            if (a.spray > 0 && a.gunId != "awp")
            {
                int i = Math.Min(a.spray, 12);
                float mag = 0.55f * s.recoil / 30f;   // AK = referência
                recoilV = i * mag * (1 - a.skill.spray);
                recoilL = MathF.Sin(a.spray * 0.7f) * 1.4f * (s.recoilVar / 70f) * (1 - a.skill.spray) * MathF.Min(1, a.spray / 8f);
            }
            float maoL = rng.Normal(0, sig), maoV = rng.Normal(0, sig);
            float eL = armaL + maoL + recoilL, eV = armaV + maoV + recoilV + a.aimErrV;
            if (Match.Auditar && a.brain.target != null && a.brain.target.Blind(m.t) && a.gunId != "awp" && !WeaponStats.Pistola(a.gunId))
            {   /* bx: tiro no cego — qual parte do erro passa do tamanho da cabeça (arma = cone; mão = aimSigma; recuo; altura do flick) */
                float cab = MathF.Atan2(1.8f, dist) * RAD;
                float vArma = MathF.Sqrt(armaL * armaL + armaV * armaV), vMao = MathF.Sqrt(maoL * maoL + maoV * maoV), vRec = MathF.Sqrt(recoilL * recoilL + recoilV * recoilV), vAlt = MathF.Abs(a.aimErrV);
                string maior = vArma >= vMao && vArma >= vRec && vArma >= vAlt ? "arma (cone)" : vMao >= vRec && vMao >= vAlt ? "mão (mira)" : vRec >= vAlt ? "recuo" : "altura (flick)";
                float vMax = MathF.Max(MathF.Max(vArma, vMao), MathF.Max(vRec, vAlt));
                m.Aud("tiro no cego: maior parte do erro", maior + (vMax > cab ? " · passa da cabeça" : " · cabe na cabeça") + (dist < 140 ? " · <10m" : dist < 280 ? " · 10-20m" : " · 20m+"));
                m.Aud("tiro no cego: cone da arma x cabeça", (vArma / cab < 1 ? "a cone < cabeça" : vArma / cab < 2 ? "b 1-2x" : vArma / cab < 4 ? "c 2-4x" : "d 4x+") + (dist < 140 ? " · <10m" : dist < 280 ? " · 10-20m" : " · 20m+") + (a.Speed > a.MaxSpeed * 0.34f ? " · andando" : m.t - a.rapidoEm < 0.2f ? " · freando" : " · parado"));
            }
            // alvo cruzando de lado: a mão segue o alvo com atraso (~0,06 s), então quem anda de lado é mais difícil de acertar
            // (o que faz o jiggle e o strafe valerem, sobretudo contra AWP). Erro horizontal = velocidade angular x atraso.
            // apRastreioVelho desliga (não sorteia nada: o jogo fica como antes); ExpNum("apRastreio") ajusta (100 = atraso de 0,06 s, o padrão)
            float kRastreio = TeamBrain.EXPF.Contains("apRastreioVelho") ? 0f : TeamBrain.ExpNum("apRastreio", 1f);   /* A/B 900: neutro, nota de erros 0,98 -> 0,84 */
            var alvoR = a.brain.target;
            if (kRastreio > 0 && alvoR != null && alvoR.alive)
            {
                V2 dir0 = (alvoR.pos - a.pos) / MathF.Max(1, (alvoR.pos - a.pos).Len);
                float vLat = MathF.Abs(alvoR.vel.x * dir0.y - alvoR.vel.y * dir0.x);
                float omega = vLat / MathF.Max(30, (alvoR.pos - a.pos).Len) * RAD;   // graus por segundo
                eL += rng.Normal(0, omega * 0.06f * kRastreio);
            }
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
                    float hz = zz - b.z, k = 1f - 0.28f * b.duck;
                    float lat = MathF.Abs(rel.x * -dir.y + rel.y * dir.x);
                    // hitbox interna: o desenho chibi tem a cabeça grande, mas a área que conta como cabeça no jogo fica
                    // dentro dela, na proporção do CS (~35% da largura do tronco) — senão o HS vai lá pra cima
                    if (HitboxTela)
                    {
                        float dz = 2.7f * b.duck, cab = CabecaZ(b.duck);
                        if (MathF.Abs(hz - cab) < 1.9f && lat < 1.7f) return Acertou(m, a, b, Part.Cabeca, p, zz);
                        if (hz > 7f - dz && hz <= cab - 1.9f && lat < 5f) return Acertou(m, a, b, Part.Tronco, p, zz);
                        if (hz >= 0 && hz <= 7f - dz && lat < 3f) return Acertou(m, a, b, Part.Perna, p, zz);
                        continue;
                    }
                    if (hz > 19.6f * k && hz < 23.4f * k && lat < 1.7f) return Acertou(m, a, b, Part.Cabeca, p, zz);
                    if (hz > 9f * k && hz <= 19.3f * k && lat < 4.6f) return Acertou(m, a, b, Part.Tronco, p, zz);
                    if (hz >= 0 && hz <= 9f * k && lat < 3.6f) return Acertou(m, a, b, Part.Perna, p, zz);
                }
            }
            return (null, Part.Nada, a.pos + dir * 1400, zAt(1400));
        }

        /// <summary>Dano no corpo com colete/capacete (CS2: cabeça x4, perna x0,75, colete reduz pelo armor pen).</summary>
        public static int Damage(Agent shooter, Agent victim, Part part)
        {
            var w = W[shooter.gunId];
            float d = w.dmg * (part == Part.Cabeca ? shooter.gun.s.hsMult : part == Part.Perna ? 0.75f : 1f);   // M4A1-S: 3,475 na cabeça
            if (!TeamBrain.EXPF.Contains("quedaDanoVelha"))
            {   // CS2 weapons.vdata m_flRangeModifier: o dano cai rm^(distância/500) — pesa em pistola e SMG (Glock 0,85, Tec-9
                // 0,79, MAC-10 0,80), quase nada em rifle (AK 0,98). Distância em unidades do CS (a nossa horizontal x4)
                bool rifleQ = TeamBrain.EXPF.Contains("quedaDanoRifle");   /* rifle/AWP: opt-in — com a calibração atual o TR subia +5 (M4 cai mais que a AK) */
                float rm = shooter.gunId switch { "ak" => rifleQ ? 0.98f : 1f, "m4" => rifleQ ? 0.97f : 1f, "m4s" => rifleQ ? 0.94f : 1f, "awp" => rifleQ ? 0.99f : 1f, "galil" => rifleQ ? 0.98f : 1f, "famas" => rifleQ ? 0.96f : 1f, "mp9" => 0.87f, "mac10" => 0.80f, "glock" => 0.85f, "usp" => 0.91f, "deagle" => 0.85f, "p250" => 0.90f, "tec9" => 0.79f, "fiveseven" => 0.81f, _ => 1f };
                float distCs = (victim.pos - shooter.pos).Len * 4f;
                d *= MathF.Pow(rm, distCs / 500f);
            }
            bool armored = part == Part.Tronco ? victim.armor > 0 : part == Part.Cabeca && victim.helmet;
            if (armored) d *= w.pen;
            return (int)MathF.Round(d);
        }
    }
}
