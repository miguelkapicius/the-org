# CT dos pros, em palavras (Mirage)

Fonte: `tactics_playbook.json`, 334 rounds de compra cheia dos dois lados (de 643). Leitura feita com `Tools/pro/casos_pro.py <1..7>`, que imprime cada caso com as regiões pelo nome real e com onde estão os TR de 5 em 5 s. Para cada situação li 20 rounds escolhidos a intervalos regulares da lista de rounds que se encaixam (então vêm de times e mapas variados, não os primeiros). Quando digo "em 13 dos 20", é contagem desses 20 casos lidos, conferida por script depois da leitura. Os números só servem para dizer onde olhei; o que vale são os casos.

Nomes das regiões (Mirage → função na Vila): CTSpawn = base CT · SnipersNest = janelão · Ladder = escada do janelão · Middle = meio · TopofMid = topo do meio · Underpass = rato · Catwalk = curta · Connector = conector · Jungle = selva · Stairs = escada do A · Scaffolding = sacada do palácio · PalaceInterior / PalaceAlley / TRamp = palácio / hall / rampa (terreno do TR no A) · Shop = mercado · Truck = carroça do B · Apartments / BackAlley / House / SideAlley / TSpawn = terreno do TR no lado B e saída do TR.

Cuidado de leitura: o dado tem região e hora, nada mais. "siteB > Truck > siteB > Truck" em poucos segundos é o jogador parado no fundo do B cruzando a divisa entre duas regiões, não correria. Quem morre some do dado; quem não tem passo gravado aparece em branco.

---

## 1. Abertura e setup do CT (0 a 25 s)

### O que eles fazem
Todo mundo sai da base entre 4 e 7 s, quase juntos, e se divide em três caminhos: nos 100 jogadores dos 20 rounds, 38 foram direto ao bomb A, 34 ao mercado (caminho do B), 21 ao janelão e 7 pela selva. A forma mais comum, em 9 dos 20 rounds, é dois no mercado, dois no A e um no janelão, meio ou conector; o resto é variação em torno disso (dois no mercado em 12 dos 20, um só em 7, três em 1; três ou quatro indo ao A em 6). Aos 20 s, dos 97 CT vivos, 46 estão do lado A (29 no bomb, 12 já dentro do palácio, rampa ou sacada, 5 no conector), 38 do lado B (19 no bomb, carroça ou mercado, 14 em curta ou rato, 5 nos apartamentos), 7 no meio e 6 ainda na base. Aos 10 s há CT no A e no B nos 20 rounds, e em 15 também no meio ou conector; aos 20 s nunca houve mais de 3 no lado A nem mais de 2 no bomb B. Quase sempre há alguém na frente em cada corredor: em 15 dos 20 rounds alguém passou por curta, rato, meio ou escada do janelão até os 20 s; em 10 dos 20 alguém entrou no palácio, sacada ou rampa até os 25 s; só em 4 dos 20 alguém foi aos apartamentos até os 30 s. O jogador do janelão é o mais "vai e volta" (dos 21 que passaram por lá antes dos 12 s, só 3 ainda estavam lá aos 25 s): em 12 dos 20 rounds alguém foi ao janelão e voltou à base antes dos 30 s, e em 7 desses 12 seguiu pela selva para o A (olhou o meio, voltou e reforçou o A). Ainda há alguém na base aos 20 s em 5 dos 20 rounds (reserva que sai tarde ou volta), mas quase sempre um só. No B, o jogador do fundo (carroça) fica quase o round todo no mesmo ponto; o segundo do B é o que sobe para curta, rato ou apartamentos.

### Três casos contados
- **furia-vs-mouz-m2-mirage-820850#10** (146 s, TR planta B aos 105). Aos 4-5 s dois CT saem para o bomb A e um deles aos 17 s entra no palácio; um terceiro vai pela selva ao A aos 7-8 s e volta para a selva aos 16 s. No B, dois chegam ao mercado aos 5 s e ao bomb aos 7 s, um fica na carroça (11-19 s) e o outro vai aos apartamentos aos 19 s, exatamente onde os TR chegam aos 15 s. Resultado do setup: A com 3 (um adiantado no palácio), B com 2 (um adiantado nos apartamentos), ninguém no meio.
- **mouz-vs-vitality-m2-mirage-00353e#17** (132 s, TR planta B aos 91). Aos 5 s dois entram no mercado e chegam ao bomb B aos 8-9 s; um deles aos 12 s desce para curta e aos 13 s já está no rato. O jogador do janelão (6 s) volta à base aos 11 s, passa pela selva aos 12 s, pisa no A aos 13 s, vai ao conector aos 14 s e chega ao meio aos 21 s: fez um circuito completo de informação em 15 s. Outro só sai da base aos 15 s (reserva), e o quinto vai ao A aos 5 s. Aos 20 s: 2 no A (um chegou só aos 15 s), 1 no B, 1 no rato e 1 no conector indo ao meio.
- **aurora-vs-spirit-m1-mirage-7ce30d#22** (116 s, TR planta B aos 105). Dois CT disputam o palácio logo no início: um chega à sacada aos 8 s e ao interior aos 9 s, e o outro ao interior aos 13 s; os TR (dois no interior aos 10 s) o encontram e aos 22 s o segundo CT cai (e um TR cai junto, aos 22). O do janelão volta à base aos 13 s, entra pela selva aos 14 s, pisa no A aos 15 s e recua para a selva aos 18 s. Dois no B: um vai da carroça ao bomb algumas vezes, o outro desce para curta aos 11 s e escada do janelão aos 21 s.

### Cenas
1. Situação: início do round CT, 5 vivos, nenhuma informação. Esperado: todos saem da base entre 4 e 8 s e, aos 10 s, há CT no A e no B (nos 20 casos sempre), e em geral (15 de 20) também no meio ou conector.
2. Situação: aos 20 s, mesmos 5 vivos. Esperado: no máximo 3 do lado A, no máximo 2 no bomb B, ao menos um adiantado (palácio, curta, rato ou meio) e no máximo 1 ainda na base (nos 20 casos, 19 respeitam).
3. Situação: o jogador que foi ao janelão antes de 8 s. Esperado: ele olha o meio e em até 15 s volta pela base e entra pelo A (ou desce pelo meio); ficar no janelão aos 25 s é raro (3 de 21 jogadores).
4. Situação: dois CT vão ao mercado. Esperado: um fica no fundo (carroça ou bomb) e o outro sobe para curta, rato, escada do janelão ou apartamentos nos primeiros 30 s (12 de 13 casos).

### O que o dado não mostra
Se o jogador do janelão tinha granada, AWP ou só olhou; quem recebeu a call de A pesado ou de B pesado; se o que está "na frente" está de pixel ou só de passagem; qual era a arma de cada um (AWP no janelão muda tudo); posição exata dentro da região (palácio é grande); e se a ida e volta do janelão é por informação, por troca de função ou por ter recebido uma call.

---

## 2. Antes do primeiro contato (20 s até a primeira baixa)

Base: 170 rounds em que o primeiro contato vem depois dos 30 s; li 20 deles (janela dos 15 s até a primeira baixa ou plant).

### O que eles fazem
O CT não fica parado esperando: em 18 dos 20 rounds, três ou mais dos cinco trocam de região durante essa espera, e só 31 dos 100 jogadores ficam numa região só (em 18 dos 20 rounds, no máximo 2 ficam assim; só 2 rounds tiveram 3). Os que ficam são as âncoras: dois do A parados a round inteiro (shinden-vs-legacy-m2-mirage-1340e9#21) ou o jogador do fundo do B. Os que se mexem fazem três coisas. (a) Ida e volta de informação: em 7 dos 20 rounds alguém fez conector → meio → conector, e no caso mais claro (1340e9#21) repetiu isso a cada ~40 s o round inteiro. (b) Rotação de um lado a outro sempre pela base: em 4 dos 20 rounds um jogador foi do A ao B ou o contrário via base (selva ↔ mercado), levando de 6 a 17 s nos casos que li; em 17 dos 20 alguém passa pela base nesse intervalo. Isso nunca é teleporte: sai do bomb, passa pelo corredor da base, entra no outro lado. (c) Avançar para tomar espaço: em 12 dos 20 rounds alguém entrou no terreno do TR (palácio, rampa, sacada, apartamentos, topo do meio) depois dos 15 s. O que parece motivar (inferência de um ou dois casos, não regra medida) é onde os TR estão: em e9c5c8#15 os TR ficam 30 s no lado B e os CT do A sobem ao palácio, e um deles chega até a saída do TR; em 1340e9#21 o TR espera 50 s no topo do meio e o CT quase não se mexe. Sinal de leitura errada: em 824ffe#3 o CT rotacionou do A para o B quando o TR juntou no meio, e o TR entrou pelo A (caso abaixo).

### Três casos contados
- **mouz-vs-m80-m2-mirage-e9c5c8#15** (89 s, CT vence por eliminação). Aos 15-35 s os TR estão no B (SideAl, Under, BackAl). Aos 27 s o conector vai ao bomb A e aos 41 s dois CT entram no palácio; um vai à rampa (44-45 s), ao hall (47-50 s) e chega à saída do TR aos 59 s, o outro faz o palácio-rampa-hall e volta ao interior aos 61-67. Um do B vai à base aos 32-39 s, passa pela selva (49 s) e pisa no A aos 56 s, voltando à base aos 63 s: reforça o A enquanto o TR está no B. Primeira baixa só aos 72 s.
- **legacy-vs-falcons-m1-mirage-824ffe#3** (118 s, TR vence, planta A aos 95). Os TR ficam no lado B (SideAl, Apts, BackAl, Under) até 30 s e no topo do meio/rato até 45 s. Aos 45 s três TR estão no meio; aos 46 s o jogador que rodava no A (selva, conector, escada) vai para a base, depois ao mercado (52 s) e chega ao B aos 58 s. Os TR passam pelo janelão e chegam ao conector aos 55-65 s, pisam no A aos 70 s, e a primeira baixa só vem aos 71 s: o A ficou com dois (um no bomb, outro na base).
- **shinden-vs-legacy-m2-mirage-1340e9#21** (102 s, TR vence). TR esperam 50 s no topo do meio, palácio e rato. No A dois CT ficam parados do começo ao fim. Um do B vai aos apartamentos aos 18 s e fica lá até os 85 s (67 s no terreno do TR). Um jogador do janelão volta à base (17 s), vai pela selva ao A (20 s) e ao conector (21 s), olha o meio aos 29 s e já está de volta ao conector aos 30 s; depois base (49 s), selva (62 s), A (65 s), conector (67 s) e meio de novo aos 71 s, de volta ao conector aos 73 s: olha o meio a cada ~40 s sem sair do alcance do A. Os TR só pisam no meio aos 80 s e a primeira baixa é aos 86 s.

### Cenas
1. Situação: round sem baixa até os 40 s com os TR no lado B. Esperado: ao menos um CT do A entra no palácio ou na rampa (e volta ao A depois), em vez de os dois do A ficarem parados (visto em e9c5c8#15; é um caso, não regra medida).
2. Situação: round sem baixa até os 40 s, 5 CT vivos. Esperado: pelo menos 3 dos 5 CT trocam de região (18 dos 20 casos), e no máximo 2 ficam na mesma região o tempo todo (18 dos 20).
3. Situação: um CT é o "olho do meio". Esperado: ele faz conector → meio → conector mais de uma vez, com intervalo na casa de 40 s (visto em 1340e9#21), e volta ao conector em segundos.
4. Situação: um CT precisa trocar de lado. Esperado: ele passa pela base (selva ↔ mercado), levando de 6 a 17 s entre sair de um bomb e pisar no outro.

### O que o dado não mostra
Se a ida e volta do conector é uma espiada de verdade ou só a posição de espera; granadas lançadas (flash, smoke, molotov) para fazer o espaço; o que o jogador sabia de som ou de aliado (nunca vemos as calls); se o CT que "sobe ao palácio" estava dando smoke ou só entrou; e qualquer coisa de mira ou de pixel.

---

## 3. Caiu o primeiro CT antes do plant

Base: 195 rounds; li 20 (a primeira baixa CT entre os 12 s e 10 s antes do plant, com 4 ou 5 TR vivos).

### O que eles fazem
Quem estava na mesma região do morto quase nunca sai dali: em 13 dos 20 rounds havia colega na mesma região, e em 11 desses 13 ele ficou na região nos 12 s seguintes (em 8 dos 20 rounds um TR cai a até 5 s da morte do CT, provável troca; não dá para saber quem matou quem). Quem está em outro lado também não rotaciona em massa: nos 12 s seguintes, em 7 dos 20 rounds alguém foi para o lado do morto (um, raramente dois), em 8 dos 20 alguém recuou à base (reagrupar, passar por ela para ir a outro lado, ou ajudar de longe) e dos 59 jogadores "de fora" 33 não mudaram de região, 12 se mexeram para algum lugar que não é o lado do morto (flancos, meio), 6 recuaram à base e 8 foram ao lado do morto. Ordem que apareceu: o colega mais perto do morto fica na região; quem vem de fora vai pela base e chega um de cada vez; o do mesmo bomb às vezes recua à base e volta (45 s → 52 s em 5eb4e3#9). Os que ajudam chegam um de cada vez: no caso 00353e#9 o primeiro cai aos 18 s, o segundo chega ao B aos 17 s e cai aos 24 s, o terceiro sai do A, espera na base de 15 a 25 s e chega ao B aos 31 s, caindo aos 32 s. Um ou outro faz o movimento contrário: em vez de ajudar, vai pelo meio ou pela curta para o lado do TR; em 3 dos 20 rounds alguém chegou à área de saída do TR (Tsp, SideAl, House ou BackAl) nos 15 s seguintes, e mais alguns desceram ao rato ou ao meio.

### Três casos contados
- **mouz-vs-vitality-m2-mirage-00353e#9** (77 s, TR planta B aos 36). Os TR descem pelo BackAl (3 aos 12 s) e chegam aos apartamentos aos 17 s. O CT da carroça cai aos 18 s e um TR cai aos 17 s. O do conector cai aos 20 s. Um da base chega ao mercado aos 15 s e ao bomb aos 17 s: cai aos 24 s. Um do A volta para a base aos 15 s, espera até 25 s e só então entra pelo mercado; cai aos 32 s. Um da escada do janelão vai à curta aos 21 s e sobrevive. Os TR perdem 3 e ainda plantam com 2.
- **furia-vs-aurora-m2-mirage-5eb4e3#9** (73 s, CT vence). O primeiro CT cai no B aos 48 s, com um TR nos apartamentos. O outro do B já tinha recuado à base aos 45 s, volta ao mercado aos 52 s e ao bomb aos 57 s. O do janelão passa pela base (45 s) e chega ao mercado aos 53 s. O do conector vai ao meio aos 56 s e ao rato aos 61 s. O do A só mexe entre palácio e A. Aos 62 s os TR estão no BackAl e na carroça; o CT elimina todos e fecha aos 73 s.
- **furia-vs-betboom-m1-mirage-8de4ec#10** (84 s, TR planta A aos 43). Cinco TR no palácio e rampa aos 26-31 s. O CT do A cai aos 32 s; o do conector passa pela escada (35 s), chega ao A aos 39 s e cai aos 39 s; o da base cai aos 41 s sem sair dela. O do B só sai do bomb aos 39 s (mercado), vai à base aos 42 s e volta ao bomb B aos 46 s. O do rato vai à curta (29 s), ao topo do meio (39 s) e à saída do TR (42 s). Resultado: em 7 s o A caiu e o resto chegou depois de o plant ser feito aos 43 s.

### Cenas
1. Situação: o primeiro CT cai no bomb, com um colega na mesma região e o TR que o matou também morto. Esperado: o colega fica na região nos 12 s seguintes.
2. Situação: o primeiro CT cai e os outros estão no lado oposto. Esperado: nos primeiros 12 s só 1 ou 2 saem para o lado do morto (7 dos 20 casos tiveram alguém) e chegam um de cada vez; os demais mantêm posição ou passam pela base.
3. Situação: o primeiro CT cai no B, com 4 TR vivos. Esperado: o CT que vem do A para o B passa pela base e chega depois, não junto, de quem já estava (visto em 00353e#9 e 5eb4e3#9).
4. Situação: o primeiro CT cai e o time ainda tem 3 ou mais jogadores vivos. Esperado: em vez de todos correrem ao morto, um jogador pode ir pelo meio ou curta para o lado do TR (flanco), como em 8de4ec#10; é minoria (3 dos 20).

### O que o dado não mostra
Quem matou o CT (se foi um TR que morreu junto, uma troca limpa ou uma baixa sem troca): só inferi pela hora de morte do TR; se o colega "ficou" atirando, trocando de pixel ou de boa; quem tinha informação do plant ou do número de TR; se a ida à base foi para recuar, trocar de arma ou pegar o kit; e a granada (flash/molotov) que o time jogou para sair.

---

## 4. CT em vantagem 5x4 antes do plant

Base: 184 rounds; li 20 (primeiro TR cai sem que nenhum CT tenha caído, até 10 s antes do plant).

### O que eles fazem
Os pros não despejam o time inteiro para cima do TR quando ganham a primeira baixa. Nos 20 s seguintes, em 6 dos 20 rounds alguém entra no terreno do TR (palácio, apartamentos, topo do meio, saída do TR); em 12 dos 20 três ou mais trocam de região; em 11 dos 20 alguém passa pela base; só em 1 dos 20 ninguém mudou de região. Ou seja, reposicionam em vez de parar ou correr. Nos casos lidos, o padrão é tomar espaço com um ou dois jogadores (palácio, curta, rato) e manter o resto no A e no B. Nos casos que li, o CT tomou espaço quando a vantagem veio cedo (7aab15#12) e segurou o bomb com reforço quando veio tarde com o TR em bloco de 4 (4fecf6#5). A vantagem não garante: nos 20 casos o TR ainda ganhou 11 (9 deles com plant), o CT 9. O erro mais visível é o par que entra junto no palácio logo após a primeira baixa (c1eb89#21): os dois caem em 2 s, transformando 5x4 em 3x4. Quem avança, nos casos lidos, é quem já estava na frente (rato, curta, A com palácio). Em c1eb89#21 o jogador do B foi sozinho ao terreno do TR (carroça → apartamentos aos 20 s → BackAl aos 26 s).

### Três casos contados
- **mouz-vs-furia-m2-mirage-4fecf6#5** (55 s, CT vence). Aos 36 s três TR estão no BackAl e um nos apartamentos, e aos 41 s os quatro estão nos apartamentos; o primeiro TR cai aos 41 s e depois aos 46 s (dois) e aos 55 s. O CT do B fica no bomb o tempo todo; o do janelão passa pela base (43 s) e chega ao B aos 53 s; o da curta não sai; o do conector sobe ao A (42 s) e vai ao meio aos 52 s; o do A vai à base aos 51 s. Resultado: segurar o bomb com dois, reforçar um e olhar o meio com outro.
- **legacy-vs-furia-m1-mirage-7aab15#12** (82 s, CT vence). O primeiro TR cai aos 14 s. O CT do rato vai ao meio aos 10 s, conector aos 13 s, A aos 16 s e entra no palácio aos 34 s; outro do A entra no palácio aos 17 s. Um do B sobe para curta (22 s) e rato (23 s). Os TR restantes recuam à saída (Tsp, House) e depois voltam ao BackAl. O CT toma o palácio e a curta e termina aos 82 s.
- **vitality-vs-furia-m1-mirage-c1eb89#21** (67 s, TR planta A aos 60). O primeiro TR cai aos 14 s, e dois CT do A entram no palácio aos 15 e 16 s e morrem aos 15 e 17 s. O do B vai aos apartamentos (20 s) e ao BackAl (26 s), sozinho. O da selva faz selva-A-escada-A-selva (17 a 33 s) e o da escada do janelão volta à base (22 s) e à selva (24 s). Com 3 CT contra 4 TR, o TR planta aos 60 s.

### Cenas
1. Situação: o primeiro TR cai antes dos 20 s, 5 CT vivos. Esperado: um ou dois jogadores tomam o espaço mais próximo (palácio, curta ou rato) em até 25 s e os outros mantêm A e B (caso 7aab15#12).
2. Situação: o primeiro TR cai e o TR vem em bloco de 4 num lado. Esperado: os do bomb atacado ficam, e um terceiro chega do outro lado pela base uns 12 s depois da queda (caso 4fecf6#5).
3. Situação: o primeiro TR cai. Esperado: dois CT não entram juntos no palácio nos 3 s seguintes sem saber onde estão os TR.
4. Situação: o primeiro TR cai e os CT ficam parados. Esperado: pelo menos 3 CT mudam de região nos 20 s seguintes (12 dos 20 casos); ficar todos parados é raro (1 dos 20).

### O que o dado não mostra
Se os CT sabiam onde estavam os 4 TR restantes, ou estavam atrás de informação; se o avanço tinha granada na frente; se a saída ao palácio foi uma tática de dois ensaiada ou duas iniciativas soltas; e se o primeiro TR foi baixa limpa ou troca.

---

## 5. O TR junta 3 ou mais num lado e entra no bomb

Base: 216 rounds; li 20 (3 ou mais TR no mesmo lado, um já no bomb, antes do plant). Cuidado: a regra pega também momentos tardios do round, com TR e CT já reduzidos.

### O que eles fazem
Na hora em que o TR pisa no bomb, o CT quase nunca tem muita gente lá: em 11 dos 20 rounds havia 1 CT no lado atacado (considerando bomb e conector para o A, bomb para o B), em 5 havia 2, em 3 havia 0 e em 1 havia 3. A ajuda vem depois e é pequena: em 9 dos 20 rounds ao menos um CT de fora chegou ao lado em até 12 s (em 2 desses rounds chegaram dois, em 7 foi um só; quem chegou, chegou entre 0 e 10 s depois, mediana 2 s), e em 11 dos 20 ninguém chegou. Em 9 dos 20 ao menos um CT ficou no outro bomb nos 10 s seguintes. Os que vêm de outro bomb passam pela base e pela selva ou pelo mercado. O CT venceu 9 dos 20 (4 deles com um só CT no lado atacado na hora, um deles com nenhum), então ter pouca gente na hora não decide sozinho; no caso a73f7a#6 o TR entrou aos poucos e o A segurou 4 TR em 7 s. Um CT do lado não atacado às vezes vai para o flanco em vez de ajudar (conector → meio → curta → topo do meio → saída do TR, em 74df48#3), e foi tarde: chegou à saída do TR aos 37 s, com a bomba plantada aos 31 s.

### Três casos contados
- **furia-vs-betboom-m4-mirage-74df48#3** (72 s, TR planta B aos 31). Quatro TR no BackAl aos 14 s, três nos apartamentos aos 24 s, três no bomb B aos 29 s. O do B cai aos 25 s. O que estava na base (14 a 23 s) vai ao janelão e volta, e só aos 25 s vai ao mercado e aos 28 s ao bomb: cai aos 30 s. O do A sai pelo conector aos 24 s, vai ao meio (27 s), curta (32 s), topo do meio (32 s) e saída do TR (37 s): foi tarde. Os dois do A (palácio e escada) ficam entre A e palácio. Plant aos 31, TR vence.
- **vitality-vs-furia-m1-mirage-c1eb89#5** (67 s, TR planta B aos 39). Aos 20 s há dois TR nos apartamentos, um no BackAl e um no topo do meio; o CT do meio cai aos 23 s. Aos 30 s três TR (SideAl, bomb B, Apts); o do B cai aos 29 s. O CT da selva vai ao conector (19 s), ao meio (21 s) e volta ao A aos 23 s; só aos 29 s vai à selva, à base (31 s) e ao mercado (35 s), quando o bomb já tinha caído. O do A deixa o A aos 34 s, vai ao meio (36 s) e ao rato (40 s). O do rato só chega à curta aos 39 s. TR planta aos 39 s.
- **legacy-vs-furia-m3-mirage-a73f7a#6** (33 s, CT vence). Cinco TR correm ao A: aos 6 s 4 já estão no hall do palácio, aos 11 s três no hall e um na rampa, aos 16 s dois no palácio, dois no hall e um no bomb. O CT do A fica no bomb; um da base passa pelo janelão (7 s), volta (14 s) e chega ao A pela selva aos 17-24 s (cai aos 27); um do A vai ao palácio aos 29 s. Os dois do B saem do bomb para a base aos 20-23 s, uns 4 s depois de os TR pisarem no A (16 s). Os TR caem aos 20, 21, 23, 27 e o último perto dos 33 s; o CT vence.

### Cenas
1. Situação: três ou mais TR no bomb A, com 1 CT lá. Esperado: o CT do outro bomb, se vier, passa pela base e chega em até 12 s; nos casos lidos chegou 0 ou 1 (raramente 2).
2. Situação: rush de 5 no A, 2 CT no A e 1 na selva. Esperado: o CT do A segura (não recua) e os dois do B recuam à base poucos segundos depois de o TR pisar no A (caso a73f7a#6).
3. Situação: TR entra no B com 4 e o CT do B é um só. Esperado: em até 12 s chega no máximo mais um CT (raramente dois).
4. Situação: o CT do meio ou rato precisa chegar ao A. Esperado: leva uns 23 a 25 s depois do plant (rato/curta → meio → conector → A, como nos retakes 987d44#21, 0bbf16#8 e d750a6#11) e é o último a chegar.

### O que o dado não mostra
Se os CT do outro bomb se moveram por som, por call ou por ver o TR passar; quantos TR o CT viu antes de rotacionar; qual granada o TR usou para entrar; e a ordem de entrada dentro do bomb (quem foi primeiro, quem entra com flash).

---

## 6. Depois do plant: retake e guarda

Base: 160 rounds com plant; 65 deles com 3 ou mais CT vivos na hora do plant (li 20 destes, mais os 20 gerais impressos). Nesses 65 o CT ganhou 24 (37%).

### O que eles fazem
Quem retoma raramente entra logo. Dos 20 retakes lidos com 3+ CT, em 9 pelo menos um CT pisou no bomb e em 11 nenhum chegou a pisar no dado (morreu antes, ou o dado não tem o passo: em 4 desses 11 o CT desarmou, então a entrada simplesmente não foi gravada). Nos 65 casos, o primeiro CT que estava fora do bomb pisa nele 11 s depois do plant (1/4 dos casos em até 4 s, 3/4 em até 16 s; 27 casos com entrada visível). A segunda entrada vem em até 3 s da primeira em 13 dos 21 casos de duas entradas (mediana de 2,8 s): entram em dupla. O caminho, nas 21 entradas dos 9 retakes lidos com entrada: no A, 17 de 18 vieram pela base (9), conector (4) ou selva (4), só uma pelo palácio (flanco); no B, 3 de 4 pelo mercado e uma pelos apartamentos. Quem retoma costuma esperar: em 218bcf#12, 987d44#21 e 8de4ec#6 os CT ficaram juntos na base por mais de 10 s antes de entrar. Dos 5 retakes lidos com 4 ou 5 CT vivos, três tinham alguém na selva ou no conector na hora do plant ou logo depois (218bcf#12, 987d44#21, 0bbf16#8). O jogador da curta, rato ou meio é o último a chegar: 13 s em 218bcf#12 e 23 a 25 s em 987d44#21, 0bbf16#8 e d750a6#11. Quatro dos 20 retakes lidos tinham um CT chegando por trás (palácio, rampa, apartamentos, saída do TR); os mais fortes combinam o flanco com a entrada frontal a poucos segundos de diferença.

### Três casos contados
- **parivision-vs-furia-m2-mirage-218bcf#12** (80 s, plant A aos 56, CT vence com defuse). 4 CT contra 3 TR. Aos 52 s três CT na base e um na curta. Aos 58 s um CT espia a selva e aos 60 s cai um TR. O da curta vai à escada do janelão (54 s), janelão (58 s), base (63 s), selva (66 s) e pisa no A aos 69 s, caindo aos 71 s com um TR caindo aos 70 s. Os três que restam (os dois da base e o da primeira espiada) pisam no A aos 73-74 s, 17-18 s depois do plant, com o último TR caindo aos 72 s. Defuse e vitória aos 80 s.
- **furia-vs-aurora-m2-mirage-5eb4e3#18** (84 s, plant B aos 47, CT vence). 3 CT contra 5 TR (quatro no bomb B, um nos apartamentos). Um CT estava na saída do TR aos 43 s (lurker), segue SideAl (47 s), House (49 s), BackAl (57 s), apartamentos (65 s) e pisa no bomb aos 78 s: chega por trás. Outro CT sai do mercado aos 43 s, vai à base (45 s), volta ao mercado (53 s) e cai aos 57 s. O terceiro entra pelo mercado e pisa o bomb aos 75 s. Os TR caem aos 66, 70, 70, 73 e 76 s, ou seja, quatro dos cinco antes de o CT da frente pisar o bomb (75 s), a partir de quando o lurker chega aos apartamentos (65 s). Defuse e vitória aos 84 s.
- **legacy-vs-g2-m2-mirage-3d8096#20** (99 s, plant A aos 88, CT vence com defuse). 4 CT contra 4 TR; um CT já está no bomb A. O CT do conector cai aos 88 s e o da base cai aos 91 s. O quarto estava no hall do palácio (por trás do TR) e aos 88 s está na rampa, aos 91 no interior e aos 92 no bomb. Os quatro TR caem em 88, 89, 90 e 92 s, 4 s juntos. Defuse e vitória aos 99 s.

### Cenas
1. Situação: plant A, 4 CT contra 3 TR, CT na base e na curta. Esperado: alguém olha a selva ou o conector logo após o plant e o grupo entra no bomb entre 15 e 22 s depois, dois pisando com até 3 s de diferença (218bcf#12; a regra dos 3 s vale para 13 de 21 entradas duplas).
2. Situação: plant B, 3 CT contra 5 TR, um CT já na saída do TR. Esperado: ele chega por trás (apartamentos) entre 15 e 30 s depois do plant e a entrada frontal vem pelo mercado nos 15 s seguintes.
3. Situação: plant A, o CT tem um jogador no palácio ou rampa. Esperado: o flanco do palácio e a entrada frontal chegam ao bomb com até 6 s de diferença.
4. Situação: o jogador da curta, rato ou meio num retake do A. Esperado: ele é o último a chegar, uns 23 a 25 s após o plant, passando pelo conector.

### O que o dado não mostra
Quanto tempo falta para a bomba explodir ou o defuse começar (só vi o "defuse" no motivo do round); quem tinha o kit e quando começou a desarmar; qual granada abriu o bomb; se o CT que fica no bomb está dentro ou a poucos metros (a região do bomb é grande); e os "buracos" no dado: alguns CT vivos aparecem sem passo gravado depois do plant (3 em 20 casos), então "ninguém pisou" pode ser lacuna.

---

## O que mais chamou atenção

Cinco coisas que um CT profissional faz e um bot parado no lugar não faria:

1. A abertura não é "cada um no seu posto": todos saem da base entre 4 e 7 s em 2 mercado + 2 A + 1 janelão/meio, mas o do janelão volta à base e entra pelo A em 7 dos 20 rounds, sempre há alguém adiantado em curta, rato, meio ou palácio (15 de 20 e 10 de 20) e 28 de 100 jogadores avançam e voltam a A, B ou base antes dos 35 s.
2. Quem não vai trocar tiro faz ida e volta de informação: conector → meio → conector em 7 dos 20 rounds (no caso mais claro, a cada ~40 s); os que seguram o mesmo lugar (A e fundo do B) existem, mas em 18 dos 20 rounds são no máximo 2 dos 5.
3. Rotação é caminho, não teleporte: de um bomb ao outro passa pela base (17 de 20 rounds têm alguém na base na espera), leva de 6 a 17 s nos casos lidos, e quem vem de curta, rato ou meio demora uns 23 a 25 s e chega por último.
4. Quem cai ou vê o colega cair não corre: em 11 dos 13 rounds com colega na mesma região, todos ficam; a ajuda chega um de cada vez, e de vez em quando (3 dos 20) alguém vai pelo flanco até a área de saída do TR. Com vantagem 5x4, 6 dos 20 rounds têm alguém entrando no terreno do TR, 12 de 20 reposicionam 3 ou mais, e o par que entra junto no palácio morre junto.
5. O retake espera e junta: o primeiro CT pisa o bomb 11 s depois (mediana), o segundo em até 3 s do primeiro (13 de 21), a entrada vem pela base, selva e conector, e os mais fortes combinam um flanco por trás (palácio, apartamentos, saída do TR) com a entrada frontal.

## Limites principais do dado (resumo)
Só região e hora: sem posição exata, mira, arma, granada, som, call ou autoria de baixas. Região do bomb e do palácio é grande; carroça/bomb B e Apts/BackAl têm oscilação de divisa. Algumas faixas têm jogador vivo sem passo gravado. A leitura de motivo ("por que o CT se mexeu") é inferência a partir de onde estavam os TR, não fato. Cada situação teve 160-216 rounds qualificados e li 20 de cada; a de retake com 3+ CT teve só 65 qualificados, e várias contagens são de 9 a 13 casos, então valem como "o que apareceu" e não como regra.
