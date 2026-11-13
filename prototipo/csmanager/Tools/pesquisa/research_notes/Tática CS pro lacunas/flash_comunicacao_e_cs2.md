# Flashbang, comunicação e números atuais do CS2 (competitivo)

Nota de método (vale para o arquivo todo):
- **[ABERTA]** = página aberta e lida nesta pesquisa. **[SNIPPET]** = só o resumo do buscador foi visto, a página não foi aberta (confiança menor; conferir antes de cravar no simulador).
- Fonte primária mais forte deste arquivo: o `gamemode_competitive.cfg` do próprio jogo (espelho SteamDatabase/GameTracking-CS2) e o rulebook suplementar de Major da Valve no GitHub.
- A wiki da comunidade (counterstrike.fandom.com) recusou acesso (HTTP 402) e bo3.gg/dotesports voltaram vazios/403. Por isso vários números finos (fuse da flash, duração exata da smoke) ficaram sem fonte forte e estão em "Lacunas".
- Nada aqui foi inventado: o que não tem link está explicitamente em "Lacunas" como SEM FONTE.

---

## 1. Flashbangs: mecânica, pop flash, apoio ao entry, self-flash, não cegar o time, contra-flash e retake

### Takeaway
O que decide a flash é o **timing e o ângulo**, não a trajetória: ela tem de estourar no instante em que o entry aparece, fora do campo de reação do defensor, e a cegueira cai muito rápido com o ângulo (cheia só até ~53° da mira; de costas quase nada). Em CS2 os times de ponta jogam **~20% menos flashes** que em CS:GO, mas cada flash rende o mesmo; o motivo apontado é economia do MR12, não nerf.

### Cited Findings

**Mecânica e números**
- Tabela de cegueira por ângulo entre a mira e a explosão (rotulada como CS2 no guia; pode ter origem em dados da era CS:GO, o guia não cita teste): 0–53° = 1,88 s de cegueira total + 4,87 s de efeito no total; 53–72° = 0,45 s / 3,40 s; 72–101° = 0,08 s / 1,95 s; 101–180° = 0,08 s / 0,95 s. [ABERTA] — [Strafe, Flashbang Guide](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- A cegueira "diminui gradualmente" com a distância da explosão; o guia não dá números. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- Flash continua cegando dentro de smoke (smoke não protege de flash). [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- Caso raro: um corpo parado exatamente na frente da explosão pode bloquear a flash. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- Fuse e cegueira, valores CONFLITANTES entre guias: "fuse de 2,1 s e 1,4 s de cegueira total em alvo direto" [SNIPPET] — [GameHub, flashbang spots](https://gamehub-al9.pages.dev/guides/cs2-strategy-flashbang-spots); "até 5 s de cegueira total" [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/); contra 1,88 s de cegueira total / 4,87 s de efeito total do [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/). O número do csdb parece confundir "efeito total" (~4,87 s) com "cegueira total".
- Preço $200; limite de 2 flashes por jogador e 4 granadas no total. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/); confirmado no cfg do jogo: `ammo_grenade_limit_flashbang 2`, `ammo_grenade_limit_total 4` [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Com 3 outras granadas (HE, smoke, molotov/incendiária) só cabe 1 flash; o máximo de compra é 4 granadas por round e usar uma flash não libera recomprar depois de atingido esse limite. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- Tipos de arremesso: clique esquerdo = longo/força máxima; clique direito = curto por baixo; os dois juntos = médio. [SNIPPET] — busca em [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/) e guias afins
- Binds de várias ações (alias de jump-throw) estão bloqueados em servidores oficiais desde a atualização de 19/08/2024. [ABERTA] — [csdb.gg Movement Guide](https://csdb.gg/guides/movement-guide/)

**Pop flash: o que é e por que funciona**
- Pop flash = flash que estoura assim que entra no campo de visão do inimigo, normalmente quicando em parede ou por ângulo preciso; funciona porque deixa tempo mínimo de reação (não dá para virar o rosto). [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- "O fator decisivo é o timing, não a trajetória": arremesso perfeito não vale nada se o time entra tarde; a montagem padrão é a granada quicar em parede/teto e estourar quando o atacante começa o swing; se estoura cedo, o defensor espera o efeito passar. [SNIPPET] — [bo3.gg, Pop Flash Setups](https://bo3.gg/news/cs2-pop-flash-setups-strategies-for-self-blind-executes)
- Execução: clique direito solto quase na hora "estoura quase imediatamente"; variante clique direito e em seguida segurar o esquerdo para a flash não tocar o chão; outro guia fala em soltar ~0,5 s depois. Os guias não concordam. [SNIPPET] — [dotesports, how to pop flash](https://dotesports.com/counter-strike/news/how-to-pop-flash-in-cs2) e guias afins da mesma busca

**De onde o suporte joga e o timing até o entry**
- Jogar por trás de quina ou por cima de parede ao entrar em área com ângulos segurados, sobretudo corredores estreitos; jogar **sem aparecer** e só entrar **depois** que a flash estoura, porque aparecer logo após o arremesso deixa o adversário experiente dar pre-fire no ponto. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- "Sincronizar a entrada para o entry se mover quando a flash detona." [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Sequência sugerida: deixar a flash estourar e as smokes abrirem, depois entrar junto; entry sem utilitária derruba muito a taxa de sucesso; no flash-peek o suporte **canta o timing da flash**, o entry vira o rosto um instante e aparece quando ela estoura. [SNIPPET] — [Refrag, erros de entry](https://refrag.gg/blog/4-of-the-worst-cs2-entry-fragging-mistakes-that-you-probably-make/)
- Peek atrasado: esperar ~0,5 s depois do estouro, porque o inimigo que virou o rosto estará voltando a mira (off-timing). [SNIPPET] — [GameHub](https://gamehub-al9.pages.dev/guides/cs2-strategy-flashbang-spots)

**Não cegar o próprio time**
- Avisar o time antes de jogar, dizendo o local; não jogar flash por onde colegas estão avançando; "um colega totalmente cego fica pior do que se você não tivesse jogado". [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Cantar as próprias flashes e utilitárias para ninguém ser cegado nem dobrar smoke. [ABERTA] — [prosettings, Communication in CS2](https://prosettings.net/blog/communication-in-cs2-guide/)
- Fogo amigo de granada no competitivo: `ff_damage_reduction_grenade 0.85`, `mp_friendlyfire 1` (dano, não cegueira; a flash cega aliado normalmente). [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)

**Defesa contra flash, contra-flash e retake**
- Defesa: virar o rosto rápido e usar cobertura; em corredor apertado escapar é quase impossível. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)
- Anti-flash: manter ~90° de giro de distância dos ângulos de flash conhecidos. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Em execução ou retake, flash bem cronometrada cega vários de uma vez para o time avançar. [ABERTA] — [Strafe](https://www.strafe.com/news/read/counter-strike-flashbang-guide-blind-your-enemies-effectively/)

**Uso real em CS2 (dados HLTV, LAN, top 20, 2023)**
- Flashes jogadas caíram 22% no lado TR e 18% no lado CT de CS:GO para CS2; por flash jogada a eficácia é a mesma; tempo cego, tempo cegando e flash assists por round caíram; o CT segue sendo mais cegado do que cega; kills sem flash assist (dry peek) subiram um pouco; duelo de abertura não mudou de forma relevante. Causa é conjectura do autor: economia apertada do MR12 (sobretudo CT) e TR optando por dry peek. Artigo de 30/12/2023, logo retrata o começo do CS2. [ABERTA] — [backpackbrain, The Death Of The Flashbang](https://backpackbrain.substack.com/p/cs2-the-death-of-the-flashbang)

### Inferences
- Para o simulador, o modelo mais defensável é: cegueira = f(ângulo mira→explosão, distância, linha de visão), com degrau forte em ~53° e quase zero acima de ~100°. Um bot que vira ≥ 90–100° antes do estouro praticamente anula a flash; isso só é possível se ele **viu/ouviu** a flash com tempo, o que o pop flash justamente nega.
- Regra de entrada derivada das fontes: suporte joga escondido → canta → entry sai no estouro (ou ~0,5 s depois, se a intenção é pegar quem virou o rosto). Aparecer **antes** do estouro é erro; aparecer muito depois (defensor já recuperado) também. A janela útil é da ordem da cegueira total (≤ ~1,9 s no melhor caso, bem menos em ângulo ruim).
- Self-flash: como o próprio jogador precisa estar a > ~100° da explosão ou sem linha de visão, a flash para si mesmo tem de estourar atrás/acima dele ou depois de quicar para o lado inimigo; ele entra no estouro. (Derivado da tabela de ângulos; nenhuma fonte aberta descreve o self-flash passo a passo.)
- Como o CT compra menos flash no MR12, contra-flash de defesa deve ser recurso situacional do bot (economia cheia), não padrão de todo round.

### Gaps
- **Fuse exato da flashbang em CS2: SEM FONTE confiável.** Só achei "2,1 s" num guia fraco [SNIPPET]; a wiki da comunidade estava inacessível. Medir no próprio jogo ou conferir a wiki antes de usar.
- **Curva de distância** (raio máximo, queda por unidade): SEM FONTE numérica.
- Tabela de ângulos: não consegui confirmar se foi remedida em CS2 ou herdada de CS:GO.
- **Self-flash e contra-flash de defesa** descritos por pro/coach, com timings: não encontrei fonte aberta; o que há acima é inferência.
- Tempo medido entre arremesso do suporte e aparição do entry em demos pro: não encontrado.
- Efeito sonoro (surdez/zumbido) e duração: não encontrado.

---

## 2. Comunicação em times: o que se canta, quem fala, atrasos, ruído e mortos

### Takeaway
A call padrão é **local nomeado + número exato + arma + grau de certeza + idade da informação**, curta e com o mais importante primeiro; fora isso canta-se utilitária (própria e inimiga), kills, perda de ângulo e mudança de plano. O jogo permite que mortos falem com o time (`sv_deadtalk 1`) e os técnicos só podem falar em aquecimento, intervalo e timeout tático, o que concentra as calls de mid-round no IGL e nos jogadores.

### Cited Findings

**O que é cantado**
- Local: usar o nome do ponto (callout), nunca "esquerda"/"nas suas costas"; dentro do bomb, o ponto específico. [ABERTA] — [prosettings, Communication in CS2](https://prosettings.net/blog/communication-in-cs2-guide/)
- Número: dizer exatamente quantos viu; evitar "eles"; ao ouvir um só: "um na banana, pode ter mais"; viu 3 de 5: "vários no túnel", não "todos no túnel". [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Arma: informar quando souber ("AWP na rampa"), porque o time reage diferente a AWP e a eco. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Utilitária: cantar smoke/flash inimigas vistas e as próprias (evita smoke dobrada e colega cego). [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Situação própria: avisar quando perde a visão de um ângulo/passagem ou muda de posição. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Itens: kit de defuse e arma caída (com o local nomeado: "AWP caída na firebox", não "atrás de você"). [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Kills: cantar a kill, não só a morte, para o time saber que a posição está limpa e não gastar utilitária ali. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Intenção: anunciar desvio do combinado **antes** de agir; em vantagem numérica, contagem regressiva para o double peek. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Formato recomendado: local + número, informação cedo, terminologia única do time ("linguagem compartilhada vale mais que linguagem exata"). [SNIPPET] — [cs2pulse, communication guide](https://cs2pulse.com/cs2-communication-guide)
- Ritmo: começo de round com comunicação leve (spawn, peeks rápidos); mid-round focado em posições inimigas e rotações. [SNIPPET] — [cs2pulse](https://cs2pulse.com/cs2-communication-guide)
- Pros mantêm a comunicação "curta, calma e precisa". [SNIPPET] — [cs2.ad, callouts](https://blog.cs2.ad/cs2-warden-callouts/)

**Certeza, idade e erro da informação**
- Não afirmar palpite como fato: marcar incerteza ("pode ser rampa?"); só cravar posição quando tiver certeza. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Call errada faz o time cair em fake ou rotacionar mal; com informação duvidosa, precisão vale mais que velocidade. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Não passar informação velha como atual: dizer a idade ("visto no B há 20 segundos"). [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Ser rápido, com o mais importante primeiro (em geral local e direção do inimigo). [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)

**Ruído e excesso**
- Exemplo de ruído: "Droga, eu sabia que ele estava lá! Ele vem pela cabana, aliás" → o certo é "um cabana". [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Em clutch e momentos tensos: calls extremamente curtas e precisas; não dar instrução a quem está no clutch; crítica e dica só depois do round; conversa fora do assunto só no freeze time. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)
- Ao morrer: primeiro a call da morte, frustração depois. [ABERTA] — [prosettings](https://prosettings.net/blog/communication-in-cs2-guide/)

**Mortos e técnico: o que a regra permite**
- No cfg competitivo do jogo: `sv_deadtalk 1` (mortos falam com os vivos do próprio time), `sv_talk_enemy_living 0` e `sv_talk_enemy_dead 0` (sem voz entre times), `mp_forcecamera 1` (morto só assiste ao próprio time). [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Rulebook suplementar de Major (Valve): servidor deve usar os padrões do Premier com exceções listadas; nenhuma exceção altera `sv_deadtalk`; **não há regra explícita limitando a voz entre jogadores**. [ABERTA] — [Valve, major-supplemental-rulebook](https://raw.githubusercontent.com/ValveSoftware/counter-strike_rules_and_regs/main/major-supplemental-rulebook.md)
- Técnico: não pode se comunicar com os jogadores por nenhum meio durante a partida, exceto aquecimento, intervalo e timeouts táticos; para pedir timeout avisa o admin; em partidas sem admin presencial (online) ninguém da comissão pode estar presente nem se comunicar. [ABERTA] — [Valve, major-supplemental-rulebook](https://raw.githubusercontent.com/ValveSoftware/counter-strike_rules_and_regs/main/major-supplemental-rulebook.md)
- Motivo dado pela Valve em 2016 para restringir técnico: dividir o trabalho de 5 jogadores (economia, calls, mid-round, leitura da situação) entre 6 pessoas não estava de acordo com os objetivos dela; consequência: dependência do IGL para ajustes pré-round, mid-round e pós-plant. [SNIPPET] — [PC Gamer](https://www.pcgamer.com/au/csgo-coach-ban) e [ESPN](https://africa.espn.com/esports/story/_/id/20549258/a-year-later-valve-counter-strike-coaching-rules-change-remains-debate)
- Prática pro: um jogador de time de ponta disse que a equipe se força a falar depois de morta ("quando estamos mortos precisamos falar"), ou seja, morto ativo é usado como fonte de informação. [SNIPPET] — [Dot Esports](https://dotesports.com/?p=921288)
- Visão contrária (guias de outros FPS táticos, não de CS): morto só tem o que viu antes de morrer e essa informação envelhece a cada segundo; padrão seria silêncio, falando só quando perguntado. [SNIPPET] — [guildorder, comms discipline](https://guildorder.com/games/r6/guides/comms-discipline)

### Inferences
- Estrutura de call para os bots: {local nomeado, quantidade (exata / "um, pode ter mais" / "vários"), arma se vista, certeza (visto / ouvido / palpite), carimbo de tempo}. A decadência da informação com o tempo é parte explícita da boa prática, então a crença do time deve envelhecer a call em vez de tratá-la como estado atual.
- Como `sv_deadtalk 1` e `mp_forcecamera 1` valem no competitivo, o morto continua sendo um "sensor": vê pela câmera dos colegas vivos e fala. Limitar a comunicação do morto no simulador seria regra de estilo de time (disciplina), não regra do jogo.
- Fontes de ruído modeláveis a partir do que foi citado: número exagerado ("todos" quando viu 3), posição cravada sem certeza, informação velha dada como atual, call de morte atrasada por frustração, fala demais durante clutch.
- A restrição ao técnico implica que **calls de mid-round partem só dos 5 jogadores**; o técnico (ou o "coach" do jogo de gestão) só entra em timeout tático, intervalo e pré-jogo.

### Gaps
- **Tempo para uma call ser dita e processada (latência em ms/segundos): não encontrei nenhuma fonte com número.** Qualquer valor no simulador será arbitrário.
- **Dano causado ("lit 80", "baixo")**: o guia aberto não menciona; é prática corrente, mas fica SEM FONTE aqui.
- **Quem fala no mid-round** (IGL x lurker x âncora, peso de cada um): só há a afirmação geral de dependência do IGL; nenhuma fonte de pro/coach aberta.
- Não achei guia de técnico ou jogador profissional detalhando over-communication com exemplos de partida; as fontes são guias de comunidade.
- Regras de ESL/BLAST/PGL sobre comunicação de jogadores mortos: não localizadas (os rulebooks completos não foram abertos).

---

## 3. Números atuais do CS2 competitivo (formato, economia, tempos, granadas, movimento)

### Takeaway
O cfg competitivo do jogo fixa: MR12 (24 rounds), round de 1:55 (`1.92` min), freeze 15 s, compra 20 s, $800 iniciais, teto $16.000, vitória $3.250/$3.500, derrota $1.400 +$500 por derrota seguida (até $3.400) começando com 1 derrota na conta, plant/defuse $300 para o jogador. Prorrogação é MR3 com $10.000 no padrão do jogo (alguns torneios usam $12.500) e os timeouts táticos variam por organizador (3 ou 4 por mapa, 30 s).

### Cited Findings

**Formato, prorrogação e timeouts**
- `mp_maxrounds 24` (MR12: troca de lado após 12, vence quem faz 13), `mp_match_can_clinch 1`, `mp_halftime 1`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Mudança CS:GO → CS2: CS:GO competitivo era MR15 (30 rounds); CS2 passou a MR12. [SNIPPET] — [dmarket, MR12 in CS2](https://dmarket.com/blog/mr12-in-cs2)
- Major (Valve): servidor nos padrões do Premier com exceções; entre elas `mp_overtime_limit 0` (prorrogações sem limite) e `mp_halftime_pausematch 1`. O rulebook não escreve por extenso MR12, dinheiro de prorrogação nem quantidade de timeouts. [ABERTA] — [Valve, major-supplemental-rulebook](https://raw.githubusercontent.com/ValveSoftware/counter-strike_rules_and_regs/main/major-supplemental-rulebook.md)
- Prorrogação: metades de MR3 (6 rounds, vence quem faz 4) depois de 12–12, com $10.000 por jogador em cada prorrogação; plataformas e torneios podem usar outro valor. [ABERTA] — [csdb.gg Economy Guide](https://csdb.gg/guides/economy-guide/)
- Dinheiro de prorrogação, fontes divergem: $10.000 é o padrão do convar e "sempre $10.000 nos Majors" [SNIPPET] — [dathost, overtime settings](https://dathost.net/docs/cs2-overtime-settings); regulamentos de LAN menores usam $12.500 [SNIPPET] — [BaseLAN 2024 CS2 Rules](https://aybonline.com/wp-content/uploads/2024/05/BaseLAN2024-CS2-Rules.pdf); outros usam $10.000 [SNIPPET] — [PolyLAN43](https://storage.polylan.ch/public-files/reglements/p43/PolyLAN43_Reglement_CS2_EN.pdf)
- Timeouts táticos, por organizador (divergem):
  - Regra da Valve de 2016 (era CS:GO): quatro timeouts de 30 s por time. [SNIPPET] — [PC Gamer](https://www.pcgamer.com/au/csgo-coach-ban)
  - ESL (atualização de 2023 para CS2): três timeouts por time, que podem ser usados todos na mesma pausa; na prorrogação os não usados zeram e cada time recebe um por prorrogação. Duração não informada no artigo. [ABERTA] — [dust2.in, ESL updates rulebook](https://dust2.in/news/40253/esl-updates-rulebook-to-adapt-to-cs2)
  - FACEIT (configuração padrão): 3 timeouts por time, com acréscimo ao fim do tempo regulamentar. [SNIPPET] — [FACEIT support](https://support.faceit.com/hc/en-us/articles/17063642043292-Setup-Game-settings-Counter-Strike-2)
  - IESF (qualificatório 2024): 4 táticos por mapa, 30 s cada. [SNIPPET] — [IESF rulebook](https://nf.iesf.org/ynopsifa/2024/02/CS2-CS2-Female-Rulebook-National-Qualifier-2024.pdf)
- Timeout técnico no cfg competitivo: `mp_technical_timeout_per_team 1`, `mp_technical_timeout_duration_s 120`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)

**Tempos do round**
- `mp_roundtime_defuse 1.92` (minutos = 115 s = 1:55), `mp_freezetime 15`, `mp_buytime 20`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Bomba: 40 s após o plant; defuse 10 s sem kit e 5 s com kit; kit custa $400, só CT; `mp_defuser_allocation 0` (sem kit grátis). [SNIPPET, com o convar confirmado no cfg ABERTO] — [csdb.gg, How to Defuse](https://csdb.gg/guides/how-to-defuse-cs2/) e [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Fonte divergente (provável erro): um site diz 10 s com kit. [SNIPPET] — [esports.net](https://www.esports.net/news/csgo/how-to-defuse-bomb-csgo/)
- Morto solta arma, kit e granadas: `mp_death_drop_gun 1`, `mp_death_drop_defuser 1`, `mp_death_drop_grenade 3`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)

**Economia**
- `mp_startmoney 800`, `mp_maxmoney 16000`, `mp_afterroundmoney 0`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Vitória (por jogador): eliminação `cash_team_elimination_bomb_map 3250`; bomba explodiu `cash_team_terrorist_win_bomb 3500`; defuse `cash_team_win_by_defusing_bomb 3500`; tempo esgotado `cash_team_win_by_time_running_out_bomb 3250`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Derrota: `cash_team_loser_bonus 1400`, `cash_team_loser_bonus_consecutive_rounds 500`, `mp_starting_losses 1`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Escada resultante: $1.400 / $1.900 / $2.400 / $2.900 / $3.400 (teto); uma vitória **desce um degrau** no contador (não zera); cada metade começa um degrau acima (perder o pistol paga $1.900); o contador zera no intervalo. [ABERTA] — [csdb.gg Economy Guide](https://csdb.gg/guides/economy-guide/)
- Divergência: outro guia diz que vitória seguida de derrota "volta para $1.400" (reset). [SNIPPET] — [gamertagmythras, economy guide](https://gamertagmythras.com/blog/counter-strike-2/cs2-economy-guide). O modelo de degrau do csdb é coerente com `mp_starting_losses 1`.
- Jogador: plant `cash_player_bomb_planted 300`; defuse `cash_player_bomb_defused 300`; matar colega `cash_player_killed_teammate -300`; kill padrão `cash_player_killed_enemy_default 300` com `cash_player_killed_enemy_factor 1`. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- TR que planta e perde por defuse, valores CONFLITANTES: `cash_team_planted_bomb_but_defused 600` no cfg do jogo [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg); **$800** por TR segundo o guia (exemplo: $3.400 + $800 = $4.200) [ABERTA] — [csdb.gg Economy Guide](https://csdb.gg/guides/economy-guide/). O cfg é a fonte primária; conferir no jogo.
- `cash_team_bonus_shorthanded 1000` existe no cfg competitivo (bônus para time com jogador a menos); a condição de aplicação não está no arquivo. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)
- Recompensa de kill por arma: rifles (AK-47, M4, FAMAS, Galil, AUG, SG 553, SSG 08, autosnipers) $300; pistolas $300, exceto CZ75-Auto $100; SMGs (MP9, MAC-10, MP7, MP5-SD, UMP-45, PP-Bizon) $600, exceto P90 $300; escopetas $900; metralhadoras $300; AWP $100; Zeus $100; granadas (HE e fogo) $300; faca $1.500. [ABERTA] — [csdb.gg Economy Guide](https://csdb.gg/guides/economy-guide/)
- Divergências sobre kill: Zeus aparece como $0 em outro guia; um site cita bônus compartilhado de $50 para o time CT por kill, não confirmado pelas demais; assistência não paga; dinheiro de kill é pago ganhando ou perdendo o round. [SNIPPET] — [csskill, MR12 loss bonus](https://www.csskill.com/lab/economy-and-utility-masterclass/cs2-economy-explained/the-mr12-loss-bonus-system) e [csdb.gg economy calculator](https://csdb.gg/economy-calculator)
- Venda de volta na compra: `sv_sellback_enabled 1` (reembolso do que foi comprado no round). [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg)

**Granadas**
- Limites: 4 no total, 2 flashes, 1 de cada das demais. [ABERTA] — [gamemode_competitive.cfg](https://raw.githubusercontent.com/SteamDatabase/GameTracking-CS2/master/game/csgo/cfg/gamemode_competitive.cfg) e [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Preços: smoke $300, flash $200, HE $300, molotov $400, incendiária $500, decoy $50. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Smoke, duração CONFLITANTE: "~20 s, uns dois segundos a mais que os 18 s do CS:GO" [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/); "18 s" na maioria dos guias e "15 s" em um [SNIPPET] — [swap.gg, how long does smoke last](https://swap.gg/blog/how-long-does-smoke-last-in-cs2); um guia diz que o tempo conta a partir da smoke totalmente aberta, não do pouso [SNIPPET] — mesma busca.
- Smoke volumétrica (CS2): ocupa um volume e se expande em volta e para dentro do ambiente em vez de ser uma bolha fixa; elimina os one-ways do CS:GO e fecha melhor espaços pequenos. [ABERTA] — [Dignitas, CS2 smokes](https://dignitas.gg/articles/cs2-smokes-explanation-and-tips)
- Granada dentro da smoke limpa uma grande parte dela por pouco tempo, até a fumaça voltar; dá para ver através nesse intervalo. [ABERTA] — [Dignitas](https://dignitas.gg/articles/cs2-smokes-explanation-and-tips). Duração do buraco: "2–3 s" / "3 s" [SNIPPET] — [bo3.gg, how smokes work](https://bo3.gg/articles/how-smokes-work-in-cs2) e guias da mesma busca.
- Tiros abrem buracos pequenos e temporários que se fecham logo; atirar **de dentro** da smoke não limpa nada e entrega a posição; de fora, tiro na borda abre buraco grande e no centro revela pouco. As balas atravessam a smoke (ela bloqueia visão, não tiro). [ABERTA] — [Dignitas](https://dignitas.gg/articles/cs2-smokes-explanation-and-tips)
- HE: ~98 de dano sem colete e ~57 com colete à queima-roupa, raio de dano ~350 unidades com queda pela distância. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Fogo: molotov ~7 s, incendiária ~5,5 s, ~40 de dano por segundo parado nas chamas; smoke que cai no fogo apaga na hora. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)
- Patch citado: molotov passa a queimar pela duração prevista independentemente de sobreposição com smoke (resumo de notas de 26 de agosto; ano não confirmado). [SNIPPET] — [Dot Esports, Aug 26 patch notes](https://dotesports.com/counter-strike/news/counter-strike-2-aug-26-patch-notes-ancient-changes-knife-updates-and-more)
- Decoy: ~15 s de tiros falsos e explode causando ~5 de dano. [ABERTA] — [csdb.gg Grenade Guide](https://csdb.gg/guides/grenade-guide/)

**Movimento e precisão**
- Velocidade máxima correndo (unidades/s): faca/C4 250; pistolas (maioria) 240; Desert Eagle 230; SMGs 230–245; escopetas 215–225; AK-47 215; M4A4/M4A1-S 225; AWP 200; Negev/M249 195. [ABERTA] — [csdb.gg Movement Guide](https://csdb.gg/guides/movement-guide/)
- AWP com luneta: ~150 u/s. [SNIPPET] — [cs.money, movement guide](https://cs.money/blog/esports/based-movement-in-csgo/)
- Andar (Shift): cerca de metade da corrida; agachado é "bem mais lento que andar" (sem multiplicador exato na página). [ABERTA] — [csdb.gg Movement Guide](https://csdb.gg/guides/movement-guide/)
- Pulo ~54 unidades; pulo agachado até ~65; quase toda arma é extremamente imprecisa no ar; aterrissagem dá penalidade breve de precisão e velocidade. [ABERTA] — [csdb.gg Movement Guide](https://csdb.gg/guides/movement-guide/)
- Counter-strafe: apertar a tecla oposta para zerar a velocidade mais rápido do que só soltar a tecla; em CS2 (subtick) o input é registrado no instante exato e a janela é "marginalmente mais larga" que no CS:GO. [ABERTA] — [csdb.gg Movement Guide](https://csdb.gg/guides/movement-guide/)
- Limiar de precisão em movimento, valores CONFLITANTES: regra de ~34% da velocidade máxima da arma; um guia traduz isso em ~88 u/s para rifle; outro calcula 73,1 u/s para a AK-47 (34% de 215) e observa que a Valve nunca publicou o número; um guia afirma que o counter-strafe chega abaixo do limiar em 1–2 frames contra ~10 só soltando a tecla (não verificado). [SNIPPET] — [gamertagmythras, movement/counter-strafing](https://gamertagmythras.com/blog/counter-strike-2/cs2-movement-counter-strafing-guide) e [counterstrikewiki, Movement Mechanics](https://counterstrikewiki.com/Movement_Mechanics) (não consegui atribuir cada número a uma das duas páginas com certeza)

### Inferences
- 34% × 215 = 73,1 u/s; os "88 u/s" não fecham com a própria regra dos 34% para rifle (seriam 34% de ~259). Se o simulador usar um limiar, 34% da velocidade máxima da arma é a forma consistente, mas é valor de comunidade, não oficial.
- Perder o pistol paga $1.900 e não $1.400 por causa de `mp_starting_losses 1`; quem modela a escada começando em $1.400 no round 1 erra o segundo round.
- Dinheiro de TR após plant perdido: usar o valor do cfg ($600) como padrão e tratar os $800 do guia como possivelmente herdado do CS:GO, até conferir no jogo.
- Timeouts táticos não são número único "do CS2": é parâmetro de campeonato (3 na ESL e FACEIT, 4×30 s na regra antiga da Valve e em alguns regulamentos). O simulador deveria ter isso configurável por torneio, junto com o dinheiro de prorrogação ($10.000 padrão, $12.500 em alguns).
- A interação HE/tiro × smoke é temporária (segundos) e assimétrica: quem atira de fora na borda ganha visão; quem atira de dentro só se entrega.

### Gaps
- **Duração exata da smoke em CS2**: fontes dão 15, 18 e ~20 s; nenhuma cita a Valve nem teste. SEM FONTE primária.
- **Duração exata do buraco de HE na smoke** e tamanho do buraco de bala: só snippets ("2–3 s").
- **Tempo de plant da C4**: não apareceu em nenhuma fonte aberta. SEM FONTE.
- **Timer da bomba (40 s)**: não está no `gamemode_competitive.cfg` (fica no padrão do convar); só confirmado por guias em snippet.
- **Timeouts táticos e dinheiro de prorrogação nos rulebooks atuais de ESL, BLAST e PGL**: textos completos não foram abertos; o que há é artigo de 2023 sobre a ESL e regulamentos menores. O rulebook de Major da Valve não traz esses números por extenso (remete aos padrões do Premier).
- `mp_overtime_startmoney` e `mp_overtime_maxrounds` padrão: não estão no cfg lido; os valores MR3/$10.000 vêm de guias.
- **Duração da incendiária**: ~5,5 s só no csdb; não confirmei em nota de atualização oficial. Dano por segundo do fogo idem.
- Multiplicadores exatos de andar/agachar e velocidades arma a arma (o guia dá faixas para SMG/escopeta/pistola): SEM FONTE fina; a wiki estava inacessível.
- Recompensas de kill por arma vêm de guia de comunidade (ficam nos dados de arma do jogo, não no cfg lido).
