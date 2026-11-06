# Matéria-prima da ElevenLabs (texto -> efeito sonoro) pro híbrido. Gera CANDIDATOS crus; quem escolhe, corta, soma
# camadas e nivela é o hibrido.py (régua do CS2). Cache: candidato que já existe não é gerado de novo (não gasta crédito).
# Chave: variável ELEVENLABS_API_KEY ou arquivo Tools/sons/.env (ELEVENLABS_API_KEY=...). Nunca imprimir a chave.
# Uso: python eleven.py [--so grupo/nome,grupo] [--orcamento 12000] [--seco]   (--seco = só mostra o custo)
import os, sys, json, time, urllib.request, urllib.error

AQUI = os.path.dirname(os.path.abspath(__file__))
BRUTO = os.path.join(AQUI, 'eleven', 'bruto')
URL = 'https://api.elevenlabs.io/v1/sound-generation?output_format=mp3_44100_128'
CRED_SEG = 11.3          # REAL medido em 2026-11-04 (API /v1/user/subscription: 7980 créditos / 705 s gerados); o help center dizia 40
MODELO = 'eleven_text_to_sound_v2'
SECO = ', dry close-mic recording, no reverb, no echo, no music, isolated sound effect'

TIRO = 'one single {arma} gunshot, close range, {carater}, sharp transient, just one shot, nothing else' + SECO
ARMAS = {  # descrição de caráter (nada de nome de jogo: é uma arma real descrita)
    'ak': ('AK-47 7.62mm assault rifle', 'deep heavy punchy boom with chunky mechanical bolt clank'),
    'm4': ('M4 5.56mm carbine rifle', 'tight snappy crack with bright punch and short metallic bolt'),
    'm4s': ('suppressed M4 carbine with a silencer', 'muffled thump, quiet puff of gas, loud metallic bolt cycling clack'),
    'awp': ('.338 magnum bolt-action sniper rifle', 'massive thunderous blast, huge low end, powerful supersonic crack'),
    'deagle': ('.50 caliber Desert Eagle magnum pistol', 'loud heavy bark, big low boom, sharp crack'),
    'glock': ('9mm Glock polymer pistol', 'light snappy pop, crisp and dry, plastic slide click'),
    'usp': ('suppressed .45 pistol with a silencer', 'soft muffled thud, small puff, crisp slide clack'),
    'p250': ('compact 9mm pistol', 'medium sharp pop, punchy and dry'),
    'mp9': ('MP9 9mm submachine gun', 'light high-pitched snappy pop, fast bolt'),
    'mac10': ('MAC-10 .45 submachine gun', 'rattly buzzy pop with loose metal rattle'),
    'tec9': ('TEC-9 9mm semi-automatic pistol', 'loud harsh snappy pop with a rattly stamped steel body, slightly boomy'),
    'fiveseven': ('FN Five-seven 5.7mm pistol', 'sharp high-velocity crack, bright and punchy, tight short body'),
    'galil': ('Galil 5.56mm assault rifle', 'punchy mid-heavy bang, grittier than an M4, solid bolt clank'),
    'famas': ('FAMAS bullpup 5.56mm assault rifle', 'bright sharp snappy crack with a quick metallic bullpup action'),
}
FOLEY_ARMA = {
    'pente_sai': ('{arma} magazine release button click and metal magazine sliding out of the gun', 1.0),
    'pente_entra': ('{arma} magazine inserted and locked into the gun with a firm metallic click', 1.0),
    'ferrolho': ('{arma} charging handle pulled back and released, bolt slams forward with a heavy metallic clack', 1.0),
    'ferrolho_frente': ('{arma} slide released, slide snaps forward with a sharp metallic click', 0.8),
    'ferrolho_tras': ('bolt-action sniper rifle bolt lifted and pulled back, metal sliding', 0.9),
    'pente_bate': ('palm slapping the bottom of a rifle magazine into place, solid thunk', 0.6),
    'sacar': ('drawing a {arma} from gear, fabric rustle and a light metal handling click', 0.8),
}
NOME_FOLEY = {'ak': 'AK-47 rifle', 'm4': 'M4 rifle', 'm4s': 'M4 rifle', 'awp': 'bolt-action sniper rifle', 'deagle': 'heavy .50 pistol',
              'glock': 'polymer 9mm pistol', 'usp': 'steel .45 pistol', 'p250': 'compact pistol', 'mp9': 'compact polymer SMG', 'mac10': 'boxy metal SMG', 'tec9': 'stamped steel 9mm pistol', 'fiveseven': 'polymer 5.7mm pistol',
              'galil': 'Galil rifle', 'famas': 'bullpup rifle'}
PECAS_ARMA = {'awp': ['pente_sai', 'pente_entra', 'pente_bate', 'ferrolho_tras', 'ferrolho_frente', 'sacar'],
              'pist': ['pente_sai', 'pente_entra', 'ferrolho_frente', 'sacar'], 'fuzil': ['pente_sai', 'pente_entra', 'ferrolho', 'sacar']}
MUNDO = {  # (grupo, nome): (texto, duração s, candidatos, laço)
    ('impacto', 'parede'): ('bullet impact hitting a stone wall, sharp crack with small debris and dust', 0.7, 3, False),
    ('impacto', 'capacete'): ('bullet striking a steel combat helmet on a head, short crunchy metallic clank with a dull thud, no ringing, no bell tone', 0.6, 4, False),
    ('c4', 'bip'): ('single short soft electronic beep from a bomb countdown timer, clean digital tone', 0.5, 4, False),
    ('c4', 'bip_rapido'): ('single very short high electronic beep from a bomb timer about to explode, urgent', 0.5, 3, False),
    ('granada', 'he'): ('fragmentation hand grenade explosion outdoors, powerful blast, deep boom, debris falling', 3.0, 3, False),
    ('granada', 'flash'): ('flashbang stun grenade detonation, extremely sharp loud bang with a metallic ring', 2.5, 3, False),
    ('granada', 'smoke'): ('smoke grenade igniting, small pop then long continuous hissing of smoke pouring out', 3.0, 2, False),
    ('granada', 'molotov'): ('molotov cocktail glass bottle shattering on stone and gasoline igniting into fire with a whoosh', 2.5, 3, False),
    ('granada', 'molotov_ar'): ('glass bottle breaking with a short fire whoosh that dies out', 1.5, 2, False),
    ('granada', 'quique_metal'): ('metal grenade bouncing once on a stone floor, short clink', 0.5, 3, False),
    ('granada', 'quique_vidro'): ('glass bottle bouncing once on stone without breaking, short clunk', 0.5, 3, False),
    ('granada', 'pino'): ('pulling the pin out of a hand grenade, metal ring and spoon click', 0.8, 3, False),
    ('granada', 'arremesso'): ('throwing an object hard, quick arm whoosh with clothing rustle', 0.7, 3, False),
    ('granada', 'fogo_loop'): ('burning fire on the ground, crackling flames, steady', 5.0, 2, True),
    ('c4', 'explode'): ('huge bomb explosion, massive deep blast shaking the ground, rumble and debris raining down, outdoors', 4.0, 3, False),
    ('faca', 'golpe'): ('quick knife slash swinging through the air, swoosh', 0.6, 3, False),
    ('faca', 'acerto'): ('knife stab hitting a body, slash and dull impact', 0.8, 3, False),
    ('passo', 'pedra'): ('single footstep of a combat boot running on dusty stone pavement', 0.5, 4, False),
    # --- extras de arma ---
    ('arma', 'vazio_fuzil'): ('dry fire click of an empty rifle, trigger click with no shot', 0.5, 3, False),
    ('arma', 'vazio_pistola'): ('dry fire click of an empty pistol, light trigger click', 0.5, 3, False),
    ('arma', 'zoom'): ('sniper scope zoom adjust, small mechanical click', 0.5, 3, False),
    ('arma', 'cai_fuzil'): ('rifle dropped on a stone floor, heavy metal and polymer clatter', 1.0, 3, False),
    ('arma', 'cai_pistola'): ('pistol dropped on a stone floor, light metal clatter', 0.8, 3, False),
    ('arma', 'pegar'): ('picking up a weapon from the ground, quick handling rustle and click', 0.6, 3, False),
    # --- bala ---
    ('bala', 'capsula_fuzil'): ('single brass rifle shell casing falling and tinkling on stone', 0.6, 4, False),
    ('bala', 'capsula_pistola'): ('single small brass pistol casing falling and tinkling on stone', 0.5, 4, False),
    ('bala', 'capsula_awp'): ('large brass sniper casing falling and bouncing on stone, heavy clink', 0.7, 3, False),
    ('bala', 'passando'): ('supersonic bullet flying past close to the ear, sharp crack and whiz', 0.6, 4, False),
    ('bala', 'ricochete'): ('bullet ricochet off stone, whining ricochet zing', 0.8, 3, False),
    # --- granadas extras ---
    ('granada', 'zumbido'): ('high pitched ear ringing tinnitus after a loud blast, steady tone fading out slowly', 4.0, 2, False),
    ('granada', 'queimando'): ('person stepping into fire, flames flaring up around clothing', 1.0, 2, False),
    # --- C4 extras ---
    ('c4', 'pegar'): ('picking up a small plastic explosive package, rustle and plastic click', 0.6, 2, False),
    ('c4', 'largar'): ('dropping a small plastic explosive package on stone, dull plastic thud', 0.6, 2, False),
    ('c4', 'alerta10'): ('urgent rising electronic warning alarm tone, short, tense', 1.5, 3, False),
    # --- corpo ---
    ('passo', 'terra'): ('single footstep of a combat boot running on dry dirt and sand', 0.5, 4, False),
    ('passo', 'madeira'): ('single footstep of a combat boot running on wooden planks', 0.5, 4, False),
    ('passo', 'metal'): ('single footstep of a combat boot running on a metal sheet', 0.5, 4, False),
    ('passo', 'telha'): ('single footstep of a combat boot on clay roof tiles', 0.5, 4, False),
    ('corpo', 'aterrissa'): ('combat boots landing on stone after a jump, heavy thud with gear rattle', 0.6, 3, False),
    ('corpo', 'pulo'): ('soldier jumping, short gear rattle and cloth movement', 0.5, 3, False),
    ('corpo', 'escada'): ('single step climbing a metal ladder with boots', 0.5, 4, False),
    ('corpo', 'cai'): ('body falling down onto a stone floor with gear, heavy thud', 1.0, 3, False),
    # --- ambiente da vila (laços) ---
    ('ambiente', 'vento'): ('warm desert wind blowing through a stone village, gentle and steady', 30.0, 1, True),
    ('ambiente', 'passaros'): ('sparse birds chirping in a quiet sunny Mediterranean village, distant', 30.0, 1, True),
    ('ambiente', 'toldos'): ('fabric awnings and flags flapping softly in the wind', 30.0, 1, True),
    ('ambiente', 'mercado'): ('very distant busy town market murmur, far away, no clear voices', 30.0, 1, True),
    # --- transmissão / HUD ---
    ('hud', 'inicio_round'): ('short punchy esports broadcast transition whoosh and hit', 1.5, 3, False),
    ('hud', 'fim_freeze'): ('short tense rising swell into a soft hit, esports broadcast', 1.5, 3, False),
    ('hud', 'compra'): ('short satisfying UI purchase click with a light metallic cash register tick', 0.5, 3, False),
    ('hud', 'pause_coach'): ('esports broadcast timeout sting, short clean whoosh and chime', 2.0, 3, False),
    ('hud', 'match_point'): ('dramatic short esports broadcast sting, deep hit with rising tension', 2.5, 3, False),
}


def lista():
    itens = []
    for a, (arma, car) in ARMAS.items():
        itens.append((a, 'tiro', TIRO.format(arma=arma, carater=car), {'awp': 3.0, 'deagle': 2.0}.get(a, 1.5), 4, False))
        pc = PECAS_ARMA['awp'] if a == 'awp' else PECAS_ARMA['pist'] if a in ('glock', 'usp', 'p250', 'deagle', 'tec9', 'fiveseven') else PECAS_ARMA['fuzil']
        for p in pc:
            txt, dur = FOLEY_ARMA[p]
            itens.append((a, p, txt.format(arma=NOME_FOLEY[a]) + SECO, dur, 2 if p == 'sacar' else 3, False))
    for (g, n), (txt, dur, nc, laco) in MUNDO.items():
        itens.append((g, n, txt + ('' if laco else SECO), dur, nc, laco))
    return itens

# v3 (notas da página v2): pedidos refeitos, gerados como candidatos novos (índice inicial evita sobrescrever)
V3 = [  # (grupo, nome, [textos alternados], duração, candidatos, índice inicial)
    ('m4s', 'tiro', ['real suppressed 5.56 rifle shot recorded up close: a dull muffled low thump and a loud sharp mechanical bolt clack, gas puff, no tone, no whine, no laser',
                     'silenced assault rifle firing one round, deep soft thud with metallic action slap, realistic, not sci-fi, no pitched sound',
                     'one shot from a rifle with a suppressor: quick muffled pop like a heavy book slammed shut plus metal bolt cycling clack'], 1.2, 6, 10),
    ('usp', 'tiro', ['real suppressed .45 pistol shot recorded up close: a soft muffled thump and a crisp slide clack, no tone, no whine, no laser',
                     'silenced handgun firing one round, short deep thud with a metallic slide snap, realistic, not sci-fi, no pitched sound',
                     'one shot from a pistol with a suppressor: quick muffled pop like a hand slapping a table plus slide cycling click'], 1.0, 6, 10),
    ('mac10', 'tiro', ['MAC-10 .45 ACP machine pistol single shot, heavy low chunky bark, boxy steel receiver rattle, deeper and thicker than a 9mm',
                       'one shot from a cheap open-bolt .45 submachine gun, fat low thump with loose metal rattle, gritty and boomy',
                       'compact .45 caliber smg single gunshot close, punchy bassy pop with rattling stamped metal'], 1.2, 6, 10),
    ('glock', 'tiro', ['9mm Glock pistol single gunshot recorded close, crisp sharp snappy crack with a short punchy body, dry',
                       'one shot from a 9mm handgun, bright tight pop with a quick plastic slide click, realistic',
                       'polymer 9mm pistol firing once, sharp clean bang, short, not boomy'], 1.2, 6, 10),
    ('mp9', 'sacar', ['drawing a compact submachine gun: a short sharp grab of the polymer grip and a quick metallic charging click, no fabric rustle'], 0.7, 3, 10),
    ('mac10', 'sacar', ['drawing a small boxy steel submachine gun: quick metal grab and a hard bolt click, no fabric rustle'], 0.7, 3, 10),
    ('impacto', 'capacete', ['headshot through a helmet: sharp crunchy bone crack with a short dull metallic tick, gory crunch, very short',
                             'bullet cracking a skull through a helmet, crunchy snap like a bone breaking, with a dull clank'], 0.6, 6, 10),
    ('faca', 'acerto', ['knife stabbed deep into flesh, wet squelch and thick meat puncture, visceral',
                        'blade plunging into meat, wet thick stab with a squishy pull, gory'], 0.8, 6, 10),
]


V4 = [  # capacete no formato do CS2: a camada "dink" (o som principal); a carne embaixo é o impacto/cabeca aprovado
    ('impacto', 'dink', ['bullet hitting a steel helmet: one short crunchy clonk, mid-pitched hollow metallic knock with a cracking crunch, like a bone snapping inside a metal pot, very short, no ringing',
                         'short hard metallic tunk of a rifle round denting a combat helmet, crisp crunch on the attack, dies out quickly',
                         'crunchy hollow knock, a skull and a helmet cracked by a bullet, dry crack and dull metal thunk',
                         'helmet hit that is short and crunchy, more knock than bell, gritty and punchy'], 0.5, 8, 0),
    ('impacto', 'crocante', ['gory crunchy bone crack, skull crunching like celery and gristle, dense crackling crunch lasting a quarter second',
                             'bone breaking with a thick crunchy splintering crackle, wet and gritty, short',
                             'crushing bone and cartilage, dense crunchy cracking texture, visceral foley'], 0.6, 6, 0),
    # HS = bala entrando e destroçando a cabeça; com capacete = o mesmo + metal perfurado (explicação do usuário)
    ('impacto', 'cabeca_gore', ['bullet blasting through a human skull, gory wet crunch and bone splintering splatter, head destroyed',
                                'gruesome headshot impact, skull bursting with wet crunchy cracking and splatter',
                                'visceral head blown apart by a gunshot, crunchy bone shatter with a wet gore spray'], 0.8, 6, 0),
    # v5: HS como foley de cinema (melancia/repolho + estalo), sem "crunch/bone" (a IA vira cascalho/terra)
    ('impacto', 'hs', ['a watermelon smashed by a heavy hammer, sharp crack then a wet splat, gunshot headshot sound effect for a video game',
                       'video game headshot sound: hard punchy crack followed by a short wet squelch, very quick and impactful',
                       'cabbage burst by a bullet, sharp snap and wet splatter, quick, punchy',
                       'bullet hitting a head in an action game, tight wet pop with a sharp cracking snap, no dirt, no gravel'], 0.6, 12, 0),
    ('impacto', 'hs_capacete', ['video game headshot through a metal helmet: sharp metallic clank and ping of the helmet being pierced, then a wet splat',
                                'bullet piercing a steel helmet and hitting the head, hard metal tink followed by a short wet squelch, punchy',
                                'action game headshot on a helmeted enemy, bright metallic crack then a watermelon-like splat, quick',
                                'a hammer smashing through a metal pot into a watermelon, metallic clang then wet burst, short and punchy'], 0.6, 12, 0),
    # v6: só o capacete quebrando (curto); depois emenda a cabeça aprovada (ideia do usuário, total <= 0,8 s)
    ('impacto', 'capacete_quebra', ['metal combat helmet cracking and breaking from a bullet impact, sharp metallic crack and short clatter, very short',
                                    'steel helmet hit by a bullet and splitting, hard bright metal crack with a clank, quick',
                                    'bullet smashing through a metal helmet, crisp metallic break and tear, short punchy hit',
                                    'video game armor break sound on a helmet, sharp metal crack and shatter, short'], 0.5, 12, 0),
    # v7 C4 (usuário): chiado de "timer acabou" antes de explodir; digitar = teclado mecânico / máquina de escrever
    ('c4', 'final', ['bomb timer running out: a fast rising high-pitched electronic whine with a hissing static sizzle, one second, ends abruptly',
                     'countdown device final second before detonation, sharp continuous electronic tone rising in pitch with a hiss, tense',
                     'electronic timer expiring, piercing beeping that merges into a rising whine and hiss, very short'], 1.2, 6, 0),
    ('c4', 'digita', ['typing seven keys on a clicky mechanical keyboard at a steady pace, crisp key clacks, close',
                      'old typewriter keys being typed quickly, seven sharp mechanical clacks',
                      'entering a code on a chunky mechanical keypad, seven tactile clicky key presses'], 2.0, 6, 10),
    # v8: o que falta comparando com os soundevents do CS2 (inventário)
    ('passo', 'ct', ['single footstep of a heavy tactical combat boot running on stone, firm heel strike with slight gear jingle'], 0.5, 6, 0),
    ('passo', 'tr', ['single footstep of a light worn sneaker running on dusty stone, softer scuff'], 0.5, 6, 0),
    ('corpo', 'aterrissa_pedra', ['landing from a jump on stone pavement with combat boots, heavy double thud and gear rattle'], 0.6, 4, 0),
    ('corpo', 'aterrissa_madeira', ['landing from a jump on a wooden crate, hollow wooden thump with gear rattle'], 0.6, 4, 0),
    ('corpo', 'aterrissa_metal', ['landing from a jump on a metal sheet roof, loud metallic boom with gear rattle'], 0.6, 4, 0),
    ('corpo', 'equipamento', ['soldier gear rattling while moving, rifle sling and pouches jingling softly, short'], 0.6, 4, 0),
    ('corpo', 'queda', ['person taking fall damage landing hard from a height, heavy body impact and pained grunt'], 0.8, 3, 0),
    ('granada', 'sacar', ['pulling a hand grenade out of a vest pouch, quick fabric rustle and a metal spoon clink'], 0.6, 4, 0),
    ('granada', 'isqueiro', ['flicking a zippo lighter and lighting a cloth rag soaked in fuel, flame whoosh catching'], 1.2, 4, 0),
    ('granada', 'molotov_voando', ['a burning bottle flying through the air, steady roaring flame flutter, loop'], 2.0, 2, 0),
    ('granada', 'incendiaria', ['incendiary grenade detonating, sharp metallic pop then a burst of fire igniting on the ground'], 2.0, 4, 0),
    ('granada', 'fogo_fim', ['ground fire dying out, flames sputtering and fading away to embers'], 1.5, 3, 0),
    ('granada', 'smoke_fim', ['thick smoke cloud slowly dissipating, soft fading airy hiss'], 2.0, 3, 0),
    ('granada', 'acertou', ['a hand grenade hitting a person, dull thud against clothing and body'], 0.5, 3, 0),
    ('c4', 'desarme_inicio', ['starting to defuse a bomb: unclipping a plastic cover and a quick electronic beep, short'], 1.0, 4, 0),
    ('c4', 'sacar', ['pulling out a small plastic explosive device, quick plastic handling and a soft electronic chirp'], 0.8, 3, 0),
    ('faca', 'sacar', ['drawing a combat knife from a sheath, quick metallic slide and blade ring'], 0.7, 4, 0),
    ('faca', 'estocada', ['heavy knife stab swing, strong fast whoosh with a forceful thrust'], 0.7, 4, 0),
    ('faca', 'parede', ['knife blade striking a stone wall, sharp metal scrape and clank'], 0.6, 4, 0),
    ('arma', 'quase_vazio', ['a small hollow click inside a rifle when the magazine is almost empty, subtle mechanical tick'], 0.5, 3, 0),
    ('bala', 'passando_sub', ['a subsonic pistol bullet whizzing past close to the ear, soft zip without a crack'], 0.6, 4, 0),
    ('hud', 'colete', ['putting on a kevlar vest and helmet, velcro rip and heavy fabric rustle, quick'], 1.0, 3, 0),
    # v9 (notas da página v9): refeitos pra ESCOLHA no ouvido (8 cada), índice inicial 20 (não mistura com os reprovados)
    ('arma', 'cai_fuzil', ['an assault rifle dropped onto a concrete floor, heavy metal and polymer clatter, two bounces then settles',
                           'rifle falling flat on hard stone ground, loud metallic clack and rattle of the magazine, short'], 1.0, 8, 20),
    ('arma', 'cai_pistola', ['a pistol dropped onto a concrete floor, sharp light metal clack and a small bounce',
                             'handgun falling on hard stone ground, quick metallic clatter, short'], 0.8, 8, 20),
    ('bala', 'passando', ['a rifle bullet flying past very close: one sharp supersonic crack like a whip snap, no whistle, no tone',
                          'close bullet pass-by recorded downrange: violent sharp snap crack and a quick airy tail'], 0.6, 8, 20),
    ('bala', 'passando_sub', ['a slow pistol bullet passing close by the ear: quick soft air whoosh flutter, no whistle, no tone',
                              'subsonic bullet flyby, short buzzy air rip, realistic, no laser'], 0.6, 8, 20),
    ('bala', 'ricochete', ['realistic bullet ricochet off a stone wall: sharp crack with a short fading zing, not cartoon',
                           'bullet glancing off concrete and flying away, gritty spark crack and a brief whizz'], 0.8, 8, 20),
    ('c4', 'largar', ['a small heavy plastic box dropped onto a concrete floor, solid thud and a short clatter',
                      'dropping a bomb package on stone ground, dull plastic thump and rattle'], 0.6, 8, 20),
    ('c4', 'sacar', ['quickly grabbing a small plastic electronic device out of a backpack, plastic handling and a firm grip',
                     'pulling a bomb package out of a bag, quick fabric rustle and plastic clack'], 0.6, 8, 20),
    ('faca', 'sacar', ['a combat knife pulled out of its sheath: sharp metallic shing of the blade',
                       'drawing a steel knife quickly, bright metal blade ring and scrape'], 0.6, 8, 20),
    ('granada', 'molotov_ar', ['a molotov cocktail bursting into flames in mid-air: glass pop and a big whoosh of fire that quickly dies out, no impact on ground',
                               'fireball bursting in the air and fizzling out, whoosh and crackle, no glass hitting anything'], 1.2, 8, 20),
    ('granada', 'queimando', ['short sizzling burn tick, flame licking a person, quick hiss and crackle',
                              'fire damage tick: brief searing sizzle with a small crackle, short'], 0.5, 8, 20),
    ('granada', 'sacar', ['pulling out a metal hand grenade: metallic ring and spoon jingle clink, quick',
                          'grenade taken from a vest, small metal clinks of the pin ring, short'], 0.6, 8, 20),
    ('hud', 'compra', ['short satisfying buy menu purchase sound for a tactical shooter, crisp mechanical click with a soft metallic tick',
                       'clean subtle UI confirm click, tactile and short, game shop purchase'], 0.5, 8, 20),
    ('hud', 'inicio_round', ['round start signal in a competitive tactical shooter broadcast, short punchy whoosh into a firm hit',
                             'esports round begin sting, quick rising swoosh and a tight impact, clean'], 1.5, 8, 20),
    # v10: arma caindo como foley de cinema/jogo (camadas: baque do peso + chacoalhar curto; sem metal ressoando)
    ('arma', 'cai_fuzil', ['foley, rifle dropped on concrete: heavy dull thud of a polymer and wood body, a short muffled magazine rattle, dampened, no ringing',
                           'game sound effect, assault rifle hits the ground: weighty thump, small bounce, quick rattle of the sling swivels, no metallic ring'], 1.0, 6, 40),
    ('arma', 'cai_pistola', ['foley, pistol dropped on concrete: compact dull clack of a polymer frame, one small bounce, dampened, no ringing',
                             'game sound effect, handgun hits the ground: short solid thunk and a tiny rattle, no metallic ring'], 0.8, 6, 40),
    ('arma', 'camada_baque', ['a heavy wooden toolbox dropped flat on a concrete floor, single dull deep thud, no ringing'], 0.6, 6, 0),
    ('arma', 'camada_chacoalho', ['a few small loose metal parts rattling briefly as they settle after a drop, short, muffled'], 0.5, 6, 0),
    # v11: bala por material (o CS tem impacto por superfície) e torcida de campeonato (clutch, ace)
    ('impacto', 'madeira', ['a bullet hitting a wooden crate, sharp splintering crack with a dull wooden thud, short',
                            'gunshot impact into wood planks, crisp crack and small splinter debris, short'], 0.6, 6, 0),
    ('impacto', 'terra', ['a bullet hitting dry dirt and sand on the ground, dull thump with a soil spray, short',
                          'gunshot impact into packed earth, quick puff of dirt and a muffled thud, short'], 0.6, 6, 0),
    ('impacto', 'metal', ['a bullet hitting a thin sheet metal wall, sharp metallic clank and short ping, short',
                          'gunshot impact on a steel plate, hard bright metal crack, quick decay, short'], 0.6, 6, 0),
    ('torcida', 'clutch', ['esports arena crowd erupting in cheers after a clutch play, big rising roar and applause, live tournament',
                           'large live audience at a gaming tournament exploding into cheers and clapping, excited, 4 seconds'], 4.5, 6, 0),
    ('torcida', 'ace', ['massive esports arena crowd going wild for an incredible play, screaming roar, chanting and applause, live tournament',
                        'huge stadium crowd explosion of cheering at a gaming final, ecstatic roar, 5 seconds'], 5.0, 6, 0),
    ('impacto', 'metal_furo', ['bullet piercing a thin steel helmet, sharp hard metallic puncture crack, very short',
                               'rifle round punching through sheet metal, crisp metal tear and clank, short',
                               'steel plate pierced by a bullet, quick bright metallic snap and thud'], 0.5, 6, 0),
]


def lista_v3():
    for g, n, txts, dur, nc, ini in V3:
        for c in range(nc): yield g, n, txts[c % len(txts)] + SECO, dur, ini + c


def chave():
    k = os.environ.get('ELEVENLABS_API_KEY')
    env = os.path.join(AQUI, '.env')
    if not k and os.path.exists(env):
        for linha in open(env, encoding='utf-8-sig'):
            if linha.strip().startswith('ELEVENLABS_API_KEY='): k = linha.split('=', 1)[1].strip().strip('"\'')
    return k


def gerar_um(k, texto, dur, laco, destino):
    corpo = json.dumps(dict(text=texto, duration_seconds=dur, prompt_influence=0.7, loop=laco, model_id=MODELO)).encode()
    req = urllib.request.Request(URL, data=corpo, method='POST', headers={'Content-Type': 'application/json', 'xi-api-key': k, 'User-Agent': 'csmanager-sons/1.0'})
    for tentativa in range(3):
        try:
            with urllib.request.urlopen(req, timeout=120) as r:
                dados = r.read()
            os.makedirs(os.path.dirname(destino), exist_ok=True)
            open(destino, 'wb').write(dados); return True
        except urllib.error.HTTPError as e:
            msg = e.read()[:300].decode('utf-8', 'replace')
            if e.code == 429: time.sleep(5 * (tentativa + 1)); continue
            print(f'  ERRO {e.code}: {msg}'); return False
        except Exception as e:
            print('  ERRO', type(e).__name__, e); time.sleep(3)
    return False


def main():
    so = sys.argv[sys.argv.index('--so') + 1].split(',') if '--so' in sys.argv else None
    orc = float(sys.argv[sys.argv.index('--orcamento') + 1]) if '--orcamento' in sys.argv else 12000
    seco = '--seco' in sys.argv
    falta = []
    if '--v4' in sys.argv:
        for g, n, txts, dur, nc, ini in V4:
            for c in range(nc):
                dest = os.path.join(BRUTO, g, f'{n}_c{ini + c}.mp3')
                if not os.path.exists(dest): falta.append((g, n, txts[c % len(txts)] + SECO, dur, False, dest))
    elif '--v3' in sys.argv:
        for g, n, txt, dur, c in lista_v3():
            dest = os.path.join(BRUTO, g, f'{n}_c{c}.mp3')
            if not os.path.exists(dest): falta.append((g, n, txt, dur, False, dest))
    else:
      for g, n, txt, dur, nc, laco in lista():
        if so and not any(s == g or s == f'{g}/{n}' for s in so): continue
        for c in range(nc):
            dest = os.path.join(BRUTO, g, f'{n}_c{c}.mp3')
            if not os.path.exists(dest): falta.append((g, n, txt, dur, laco, dest))
    custo = sum(round(d * CRED_SEG) for _, _, _, d, _, _ in falta)
    print(f'{len(falta)} candidatos a gerar, ~{custo} créditos (orçamento {orc:.0f})')
    if seco or not falta: return
    k = chave()
    if not k: print('sem ELEVENLABS_API_KEY (variável ou Tools/sons/.env)'); sys.exit(2)
    # orçamento decidido ANTES de disparar (paralelo não pode passar do teto)
    fila, soma = [], 0
    for item in falta:
        c = round(item[3] * CRED_SEG)
        if soma + c > orc: print('orçamento atingido: ficam de fora', len(falta) - len(fila)); break
        fila.append(item); soma += c
    from concurrent.futures import ThreadPoolExecutor
    gasto = [0]
    def um(item):
        g, n, txt, dur, laco, dest = item
        if gerar_um(k, txt, dur, laco, dest):
            gasto[0] += round(dur * CRED_SEG); print(f'  {g}/{os.path.basename(dest)}  (total {gasto[0]})', flush=True)
    with ThreadPoolExecutor(4) as ex: list(ex.map(um, fila))
    print('gasto ~', gasto[0], 'créditos')


if __name__ == '__main__':
    main()
