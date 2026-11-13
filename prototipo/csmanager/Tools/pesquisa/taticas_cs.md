# Como táticas de CS são construídas — pesquisa para o gerador

Pesquisa feita em 2026-11-11 em material público de Counter-Strike competitivo (guias de IGL, artigos de análise, entrevistas de coach). Complementa as seções 2, 3, 5 e 5.1 do `PLANO_SIMULACAO.md`. Nada aqui foi copiado de demo; são princípios para o gerador e para as tarefas dos jogadores.

**Como ler cada bullet:** princípio → por quê → como vira regra/dado no gerador ou tarefa do jogador.

**Marcas de confiança**
- **[C]** consenso amplo: aparece em duas ou mais fontes independentes da lista final.
- **[1F]** opinião ou formulação de uma fonte só (a fonte vem entre parênteses).
- **[SF]** sem fonte consultada nesta pesquisa: é conhecimento corrente do jogo competitivo que eu trouxe para fechar a lacuna. Tratar como hipótese a conferir no olho e na régua das demos.

**Números de referência usados no texto**
- Round 1:55; C4 40 s; defuse 10 s sem kit, 5 s com kit. [C]
- Smoke ~18 s (valor dado pelos guias; conferir com a camada 0). Fogo dura menos que a smoke. Flash cega de ~1 a 5 s conforme ângulo e distância. [C]
- Carga máxima: 4 granadas, no máximo 2 flashes e 1 de cada uma das outras. [C]
- Fases típicas de um round de compra cheia (metabot): controle inicial 1:55–1:35, default 1:35–0:50, execução 0:50–0:15, pós-plant depois. [1F] Outra fonte (guildorder) diz que o default tem posições fixas aos 15 s, pode virar execução já aos ~20 s, e que enrolar até sobrar ~40 s para então correr é erro comum. [1F] As duas concordam que o default tem prazo.

---

## 1. Default do TR

- **O default é a abertura padrão quando não há alvo decidido** → compra informação e espaço sem gastar a jogada; não compromete com bomb nenhum [C] → `Jogada` do tipo "não comprometida", com campo obrigatório `prazo` e `jogada_de_saida` (ver abaixo).
- **Default não é passivo** → o time ocupa pontos, mostra presença, pune avanço do CT e força o CT a gastar granada cedo [C] → cada posição do default tem tarefa ativa (segurar avanço, jiggle para informação, gastar uma granada "de presença"), nunca "esperar".
- **O default responde quatro perguntas** (guildorder) [1F] → posição de cada um aos ~15 s; que informação ele compra e a que custo; que granada é gasta e qual fica guardada; o que fazer se o contato vier antes → são quatro campos do dado `Default`: `posicoes_15s`, `info_buscada`, `granadas_gastas/guardadas`, `fallback_contato`.
- **Estrutura "núcleo de 3 + 2 pontas"** (dignitas) [1F] → o IGL fica no núcleo como centro que recebe informação; dois agressivos do núcleo pegam espaço e buscam pick; as duas pontas seguram flanco e costumam ser bons de fim de round, porque morrem menos → o gerador escolhe 1 zona de espaço principal (núcleo) e 2 zonas de borda; atribui às bordas os jogadores de estilo passivo/clutch.
- **Formações são distribuição por corredor, e mudam por mapa e por round** [C] → exemplos nas fontes: um em cada extremo, três no meio (1-3-1) para tomar o meio; um para um bomb, um para o outro, um no meio e dois "flutuantes" que se encaixam conforme o round; quatro segurando pontos e um "agente livre" que soma num lado para dar troca → o gerador produz o default como vetor de ocupação por corredor (ex.: A=1, meio=3, B=1), com 0 a 2 jogadores marcados `flutuante`.
- **Onde pôr mais gente** [SF] → no corredor que dá mais opções depois (o meio, quando o mapa tem meio que liga aos dois bombs) e onde o CT costuma avançar → peso do corredor = quantos bombs/entradas ele abre + frequência de agressão do CT ali (memória da partida).
- **O que cada posição busca** [C]:
  - *espaço*: chegar a um ponto de estrangulamento e ficar onde o inimigo é obrigado a respeitar; não avançar além dele → tarefa `tomar_e_segurar(ponto)` com condição "não passar da linha X sem call".
  - *informação*: jiggle/shoulder para ver se a posição está ocupada, ouvir passo e granada, notar de onde NÃO veio tiro → tarefa `sondar(angulo)` que gera eventos de crença, com peek curto e nunca aberto.
  - *pick*: só com apoio (flash de colega e alguém em posição de trocar) → tarefa `buscar_pick` exige `parceiro` e `flash` disponíveis; sem os dois, o avaliador derruba o valor.
- **Não morrer no default vale mais que matar** → cada baixa abre um corredor inteiro e tira opção de jogada [C] → no avaliador, durante a fase default, o peso de "morrer" é maior que na execução.
- **Espaço que não ameaça bomb nem custa tempo ao CT é desperdício** (metabot) [1F] → o gerador só inclui no default pontos que (a) dão linha para uma entrada de bomb ou (b) cortam uma rota de rotação.
- **A ponta/lurker cria a segunda ameaça** → se o CT respeita, o bomb principal fica mais fraco; se não respeita, aquele flanco está livre [C] → o default sempre mantém ao menos um jogador ameaçando o bomb oposto ao núcleo.
- **O que conta como informação** [C] (lista para o `Chamador`):
  1. inimigo visto (quantos, onde, que arma) — não dizer "eles", dizer o número;
  2. som: passo, tiro, recarga, granada sendo jogada;
  3. granada do CT gasta e onde (muitas smokes num bomb = bomb mais fraco depois);
  4. trajetória de granada (de onde veio diz onde o jogador está);
  5. ausência: ponto onde ninguém atirou, ângulo que estava vazio;
  6. agressão do CT (onde ele avançou) e posição do AWP;
  7. baixas dos dois lados e onde aconteceram;
  8. rotação ouvida ou vista.
  → cada item é um tipo de evento de `Percepcao`/`Comunicacao` com lugar, hora e confiança.
- **Como o IGL decide o bomb a partir do default** [C] → ele pesa informação colhida, picks e baixas, e granada que sobrou; as saídas são três (guildorder [1F]): **converter** (a informação sustenta uma jogada ensaiada: chama pelo nome), **tomar e segurar** (quadro incerto mas há espaço: consolida e joga o relógio), **reiniciar** (plano morreu: volta ao fallback) → `Chamador.avaliarMeioRound()` devolve um desses três, sempre com valor esperado calculado.
- **Sinais que apontam o bomb** [C]/[SF] → bomb onde o CT gastou granada; bomb de onde saiu rotação; bomb com um só defensor confirmado; lado onde conseguimos pick; lado oposto ao AWP → pontuação por bomb = defensores estimados (crença) − granadas CT restantes ali − picks nossos ali + tempo de rotação do CT até lá.
- **Condição ideal para executar** (dignitas) [1F] → CT empurrado para dentro do bomb e com posições conhecidas → gatilho de conversão: espaço externo tomado + ≥N posições de defensor com crença recente.
- **Decidir rápido vale mais que decidir perfeito** (guildorder) [1F] → IGL inexperiente perde no relógio → a qualidade do IGL entra como atraso até a decisão e ruído no valor, não como bônus fixo (coerente com a seção 6 do plano).
- **Risco do default é a deriva** → sem prazo o time chega ao fim sem tempo de executar [C] → prazo padrão ~0:50–0:40 no relógio para já estar comprometido; passado o prazo, o `Chamador` é forçado a escolher.
- **Variar o default** → posições repetidas viram pré-mira do CT [C] → custo de previsibilidade (seção 4.1 do plano) aplicado também às posições do default.

---

## 2. Execução (round executado)

**Estrutura**

- **Execução é ataque ensaiado a um alvo conhecido** → granadas combinadas e ordem de entrada definida; depende de compra cheia [C] → `Jogada` do tipo "comprometida" com requisito de inventário; se o time já gastou as granadas no controle de mapa, a execução perde valor ou cai para versão reduzida.
- **Toda jogada ensaiada tem quatro coisas** (guildorder) [1F] → nome curto que não se confunde com callout; granada atribuída a jogador específico (não "ao time"); gatilho (relógio, informação do default ou estado de economia); ramo de falha → campos do dado: `nome`, `tarefas[]` com dono, `gatilho`, `ramo_de_falha`.
- **Poucas jogadas bem feitas valem mais que muitas** (guildorder: 3–4 por mapa/lado) [1F] → repertório inicial pequeno por mapa; crescer é evento de carreira.
- **Sequência padrão** [C] → posições de saída → granadas → entrada → troca → plant → pós-plant → as fases da `Jogada` são exatamente essas, cada uma com condição de avanço.
- **Posições de saída** → cada um para no ponto de onde joga a sua granada ou de onde entra, fora das linhas do defensor [C]/[SF] → o gerador busca pontos sem linha de tiro das posições de defesa conhecidas, a ≤T segundos da entrada.
- **O "go"** → a execução precisa de gatilho único: contagem, call do IGL ou a primeira granada estourando [C] → evento `go` na `Comunicacao`; cada tarefa tem `atraso_apos_go` próprio para que tudo aterrisse junto.
- **Tempo é o que decide** → granada cedo demais é desperdiçada (smoke acaba, flash é esperada); entrar antes de a smoke abrir vira peek seco sozinho [C] → regra dura: `t_entrada >= t_smoke_aberta` e `t_flash_estoura ≈ t_primeiro_aparece`.
- **Quem sai de mais longe sai antes** [SF] (já está no plano) → `t_saida = t_go − tempo_de_percurso`.

**Ordem de entrada e quem joga o quê**

- **1º: entry** → entra atrás da flash, prioriza caminho e espaço, não para para duelar tudo; o trabalho é criar uma situação conhecida (matar, ou morrer onde o colega troca e o espaço é tomado mesmo assim) [C] → tarefa `abrir(caminho)` com lista ordenada de ângulos a limpar.
- **2º e 3º: troca** → entram colados o bastante para trocar, olhando o ângulo que o entry não limpou [C] → tarefa `trocar(parceiro)` com gatilho "parceiro caiu → mato quem matou".
- **4º: suporte** → joga smoke/molotov/flash de fora, depois entra; segura contra-avanço e lurk do CT; guarda uma flash para "contra-flash" [C] → tarefa `apoiar` com lista de arremessos e depois `cobrir_flanco`.
- **5º: lurker ou retaguarda** → fica no outro lado ou cobre o flanco da execução (seção 4).
- **C4** [SF] → vai com quem não é o primeiro a entrar (3º ou 4º), para não cair em lugar difícil de recuperar → campo `portador` com restrição "não é entry".
- **Granada atribuída ao jogador, não ao time** [C] → o suporte normalmente é dono da maioria dos lineups; o entry quase não joga → custo de atribuir arremesso ao entry é alto no gerador.

**Propósito de cada granada**

- **Smoke: reduz o número de ângulos que quem entra enfrenta ao mesmo tempo** [C] → não tira ninguém do lugar, só corta visão (e não para bala) → o gerador escolhe a smoke pela linha de tiro, não pelo lugar.
  - *corta linha longa / de AWP*: defensor distante com visão sobre a entrada → smoke na frente dele ou no meio da linha. [C]
  - *corta rotação*: smoke na passagem por onde chega a ajuda (porta do CT, conector) → isola o bomb e atrasa quem gira. [C]
  - *para cruzar*: smoke que cobre um trecho aberto que o time precisa atravessar. [C]
  - *cobre o plant* (e, no retake, cobre o defuse). [C]
  - Critério de escolha [SF]: ordenar linhas do defensor por (distância × abertura × probabilidade de estar ocupada); smoke vai nas mais longas e mais prováveis, porque as curtas se resolvem com molotov e flash.
- **Molotov/incendiária: nega chão** → tira defensor de canto, atrasa avanço, impede defuse [C] → tipo de posição alvo: canto fechado, perto da entrada, onde o defensor ficaria escondido atrás da smoke ou fora do alcance da flash.
  - Mostra de onde o time vem (custo de informação). [1F: metabot]
  - Fogo dura menos que smoke: jogar perto do "go", não antes. [C]
  - Fogo em cima de smoke apaga/derruba a smoke (detalhe de regra da camada 0; fontes divergem no detalhe). [1F]
- **Flash: cobre o instante em que o primeiro aparece** [C].
  - *Pop flash*: estoura sem dar tempo de virar; quem jogou (ou o colega) abre logo atrás. [C]
  - Cedo demais o defensor espera passar; tarde demais o entry aparece sem cobertura. [C]
  - *De onde*: de trás ou de cima de quem entra, por cima de parede/teto, para estourar no campo de visão do defensor e fora do campo de quem entra. [SF]
  - Avisar a flash para não cegar o próprio time. [C]
  - Segunda flash "de fundo" para quem está mais dentro do bomb e para a segunda leva. [SF]
- **HE: finalizadora, não abertura** [1F: metabot; coerente com o plano] → em inimigo ferido ou ouvido e não visto; em canto empilhado logo antes de abrir; contra eco agrupado; guardar uma para o retake → tarefa `he(alvo)` só com crença recente (som/dano) no alvo.
- **Guardar granada para o pós-plant** → uma smoke ou molotov guardada pode sozinha parar o retake [C] → o gerador reserva ≥1 molotov e ≥1 smoke do time para depois do plant.
- **Executar quando o CT já gastou granada** (hotspawn) [1F] → contagem de granadas CT vistas entra no valor de "executar agora".

**Lugar de plantar**

- **O plant padrão nem sempre é o melhor** → planta-se onde o C4 fica visível de vários ângulos que o time já limpou ou controla, e onde quem defende fica seguro [C] → o gerador pontua pontos de plant por: nº de posições de pós-plant com linha para o C4 × segurança dessas posições × distância das entradas de retake.
- **Quem planta define o pós-plant e avisa onde plantou** [1F: refrag] → call obrigatória "plantei para X"; os outros se posicionam em função disso.
- **Plant "para fora"** [SF] → quando o time vai segurar de longe (do corredor de entrada), planta-se no lado visível de lá; quando vai segurar por dentro, planta-se coberto → campo `estilo_pos_plant` (dentro/fora) escolhido antes do plant conforme vivos e granadas.
- **Ramo de falha** → se a entrada travar: recuar para o espaço tomado e reiniciar, ou girar se o lurker tiver informação do outro lado [1F: guildorder] → todo `Jogada` executada carrega `ramo_de_falha` com gatilho (ex.: 2 baixas sem entrar, ou smokes acabando).

---

## 3. Rush, contato, split, fake, jogada lenta, pick

- **Rush** → todos por uma entrada, imediatamente, com pouca granada; aposta no tempo: chegar antes de o CT terminar de se armar ou de girar [C].
  - *Quando*: forçada e pistol (tem arma, não tem granada); CT com economia fraca; defesa passiva e baseada em informação; para mudar o ritmo depois de rounds lentos; quando alguém tem spawn bom. [C]
  - *Falha*: contra stack e contra granada de atraso (incendiária/smoke na entrada); rush que falha falha inteiro (perde round e equipamento). [C]
  - Gerador: `t_chegada_TR < t_setup_CT` na entrada escolhida; 1–2 flashes na frente; valor cai forte se a crença indicar ≥3 defensores ou se o CT usou molotov ali nos últimos rounds.
- **Contato (jogada lenta de contato)** → o time anda em silêncio até o primeiro tiro e só então usa granada e explode [SF, definição corrente; refrag cita "contact play" como a exceção em que se entra sem granada antes].
  - *Quando*: contra CT que gira por som e por granada; quando se quer chegar perto sem dar informação; com poucas granadas. [SF]
  - *Falha*: contra defensor avançado que pega o grupo andando; se alguém faz barulho; se demora e o relógio aperta. [SF]
  - Gerador: fase de aproximação com `andar_silencioso`, gatilho `primeiro_contato` → fase explosão com as granadas que estavam guardadas.
- **Split** → o mesmo bomb por duas entradas ao mesmo tempo, para dividir a atenção do defensor [C].
  - *Funciona se*: os dois grupos chegam juntos (vira cruzado em cima do defensor); há gatilho comum (contagem); a rota de ligação está tomada (em geral exige controle de meio). [C]
  - *Falha*: chegadas separadas dão ao defensor duas lutas vencíveis; sem o meio, é fácil de parar. [C]
  - É muitas vezes a segunda metade de um default. [1F: csdb] Indicado contra 2-1-2, onde dois defensores cobrem bomb e passagem. [1F: dignitas]
  - Gerador: duas `entradas` com `t_chegada` igual (tolerância ≤1–2 s), pré-requisito "zona de ligação = nossa".
- **Fake** → fazer o barulho e gastar a granada de um ataque num lado, e bater no outro quando o CT girar [C].
  - *Funciona se*: custa o bastante para ser crível (granada de execução de verdade, presença, tiros); o time tem controle de mapa e sabe onde o CT está; ainda sobra granada e tempo para o ataque real. [C]
  - *Falha*: fake barato é ignorado; fake repetido é lido; time bom não gira por barulho; sem controle de mapa o CT já sabe que é oco. [C]
  - Poucos jogadores fazem o fake (1–2) enquanto o resto prepara o ataque real. [1F: bo3.gg, vi só o resumo da busca]
  - "Fake do fake": pressiona A, mostra que saiu para B, e executa A mesmo. [1F: khelnow]
  - Gerador: o fake é uma `Jogada` com duas metades; a metade real só dispara se a crença registrar rotação (som, visto, ou ausência no bomb real). Sem sinal de rotação, o `Chamador` decide entre converter o fake em ataque de verdade ou seguir.
- **Jogada lenta (default longo)** → espalhar, esperar as smokes do CT acabarem e ganhar o fim do round [C].
  - *Quando*: time com mais granada que o adversário; CT agressivo por informação (deixa ele vir e pune); suspeita de stack; para sentir a defesa. [1F: hotspawn]
  - *Falha*: deriva, falta de tempo; CT que não gasta nada. [C]
- **Pick de abertura** → um jogador (AWP ou rifler com flash de colega) busca uma baixa cedo [C].
  - *Funciona se*: há apoio (flash, colega para trocar) e ângulo com vantagem; o alvo tem hábito conhecido (memória da partida). [C]
  - *Falha*: peek seco sozinho; repetir o mesmo ângulo; perder o AWP cedo deixa o time em 4x5 sem a arma mais cara. [C]
  - Depois do pick: virar 5x4 muda a call (seção 6).
- **Regra geral de escolha** → a compra decide a família (comprometida ou não) antes da preferência; economia ruim empurra para velocidade e surpresa, então ler a compra do adversário prevê a jogada dele [1F: csdb] → primeiro filtro do `Chamador`: jogadas viáveis com o inventário; primeiro filtro do CT: tipo de compra provável do TR.
- **Controlar o ritmo** → o maior erro é jogar todo round no mesmo ritmo; quem só executa é decifrado, quem só faz default fica sem tempo [C] → custo de previsibilidade por *ritmo* (rápido/lento), além de por bomb e por jogada.
- **Truque de tempo** [1F: khelnow] → tomar o espaço e esperar ~30 s antes de executar quebra o tempo de rotação e de retake do CT → variação "pausa" do gerador.

---

## 4. Funções

**Entry**
- *Antes*: aceita a call sem hesitar; alinha com quem vai bangar; não faz contato antes de a granada chegar nem antes de o colega poder seguir. [C]
- *Durante*: é o primeiro; passa pelo ângulo disputado, cria espaço, aceita morrer. Meio-peek, recuar e re-peek estragam o tempo de todo mundo. [C]
- *Depois do primeiro abate*: não se estende; o objetivo é o pé dentro do bomb, não a contagem. Segura o chão tomado e limpa o próximo trecho com granada. [1F: refrag]
- Não precisa ser o primeiro todo round: entra quando a situação pede, de preferência atrás de granada ou distração. [1F: refrag]
- → Tarefa `abrir`: gatilho `go`+flash; fim = ponto de apoio alcançado ou morte; pós-condição "segurar ângulo X".

**Segundo / troca**
- *Distância*: perto o bastante para trocar, longe o bastante para que um rifle só não pegue os dois no mesmo spray e uma granada não pegue os dois. [C] As fontes não dão número.
- *Não entupir a passagem*: o entry sai da porta antes de o segundo aparecer; parado na porta, morrem os dois. [1F: dignitas]
- *Tempo de troca* [SF]: a janela útil é curta — o plano já usa "menos de 2 s"; análises costumam contar como trocada a baixa respondida em até ~5 s. Sugestão: troca ideal ≤2 s, aceitável ≤4–5 s; passou disso é duelo novo.
- *Regra de bolso para distância* [SF]: seguir a ~1–1,5 s de percurso atrás (algo como 3–6 m), deslocado para o lado para ter linha própria.
- "2 contra X é sempre melhor que 1 contra X"; o time que troca melhor tende a ganhar com mira igual. [1F: dignitas]
- Sistema de duplas: pares fixos por mapa/lado aprendem a trocar um ao outro; o 5º fica livre para bangar as duplas, servir de isca ou lurkar. [1F: dignitas]
- → Tarefa `trocar(parceiro)`: mantém distância-alvo e ângulo complementar; gatilho "parceiro caiu" → abre na linha de quem matou em ≤2 s; gatilho "parceiro matou" → passa a ser o próximo a entrar.

**Suporte**
- *Antes*: dono dos lineups; cronometra a flash para o peek do entry e avisa o arremesso. [C]
- *Durante*: smokes da tomada, disciplina de smoke do time; costuma ser o primeiro a reagir (trocar) quando alguém cai. [1F: guildorder]
- *Depois*: molotov/incendiária de pós-plant, controle de passagem, contra-flash. [C]
- Erros: caçar abate em vez de impacto de granada, flash que cega o time, smoke do lado errado da parede, granada sem aviso. [1F: guildorder] → são os erros "com causa" do suporte (atributo utilitária/comunicação).

**Lurker**
- *Antes*: começa passivo na borda do mapa, do lado oposto ao time; impede o CT de tomar espaço e de flanquear. [C]
- *Durante*: o tempo dele é amarrado ao do time. Ataca quando a ação puxou os defensores: pega quem está girando, ou entra pelo vão que a rotação deixou. [C]
- *Depois*: corta a rotação/retake pelas costas ou se esconde em posição de impedir defuse. [1F: blast]
- Lurk sem tempo combinado com o time é o erro clássico; lurker só vale se o time sabe o que ele está fazendo. [C]
- O lurker dá informação: se ninguém passou por ele, o CT não girou. [SF]
- → Tarefa `lurk(zona)`: fase 1 `segurar_flanco` silencioso; gatilho `go` do time ou som de rotação → fase 2 `avancar_no_vazio`; relatar "passou N" / "ninguém passou" como call.

**AWPer**
- *Abertura*: pega o ângulo de abertura e busca o primeiro pick, com apoio; não troca cedo. [C]
- *Durante*: segura o ângulo em que o time espera que o inimigo apareça, para o time trocar em cima; precisa de rota de saída. [1F: guildorder]
- *Depois de atirar*: muda de posição; AWPer parado no mesmo ângulo é previsível. [C]
- *Pós-plant*: segura a linha longa perigosa. [1F: guildorder]
- Economia: AWP custa 4750 e rende só 100 por abate; salvar o AWP vale mais que um abate de saída; não comprar/forçar AWP em round fraco. [C]
- → opções do AWPer no avaliador: `segurar_linha_longa`, `pick_com_apoio`, `reposicionar_apos_tiro`; penalidade forte para duelo de perto e para repetir ângulo.

**IGL**
- *Compra*: escolhe jogada, bomb, quem vai primeiro e que granada se gasta; decide a compra antes do tipo de round e anuncia as duas coisas juntas. [C]
- *Meio do round*: ajusta quando o plano quebra (trocar de bomb, fake, converter). [C]
- *Entre rounds*: lê o que aconteceu e define o ajuste do próximo. [C]
- Diz *onde* e *o quê olhar*, não *como* cada um joga a posição; conhece as tendências de cada jogador para planejar em volta. [1F: dignitas]
- Divergência entre fontes: uma defende que, no meio do round, quem está vivo decide e o IGL morto só passa informação [1F: dignitas]; outra, que durante a execução só o IGL chama e os demais só relatam fatos [1F: guildorder]. → No jogo: parâmetro de estilo do time (`comando_central` ↔ `autonomia`), ligado à questão em aberto do plano sobre disciplina tática.

**Âncora (CT)**
- Defensor principal do bomb, muitas vezes sozinho enquanto o parceiro disputa espaço; último a girar; cai muito em clutch. [C]
- Objetivo é tempo, não abate: ficar vivo mais tempo fortalece a defesa quando a ajuda chega; o que ele faz é dano/atraso para tornar o retake viável. [C]
- Três jeitos de jogar (hotspawn) [1F]: (1) posição de informação: vê, joga granada, chama ajuda; (2) ângulo inesperado: pega o primeiro, pede flash ou usa a própria granada para o segundo antes de ser trocado; (3) ângulo passivo: um abate e recua; ameaçado, entrega o bomb para ficar vivo.
- Morte cedo do âncora é o pior resultado; evitar jogada de herói. [1F: hotspawn]
- Varia a posição de round para round para não ser pré-mirado. [C]
- → Tarefa `ancorar(bomb)`: opções `atrasar_com_granada`, `um_e_recua`, `ceder_e_esperar_retake`; nunca `avancar_sozinho` sem informação.

**Rotador (CT)**
- Primeiro a sair da sua área quando o outro bomb é pressionado; guarda 1–2 flashes para ajudar na chegada; avisa que girou para o colega saber do buraco. [1F: hotspawn]
- Times de nível alto deixam granada no spawn para quem gira pegar. [1F: hotspawn]
- → Tarefa `rotacionar`: gatilho por informação (seção 5), rota segura conforme controle de mapa, call "saí de X".

---

## 5. Lado CT

**Setups**
- **2-1-2 é o padrão**, mas o setup muda de round para round porque o adversário se adapta. [C] → gerador de setup = distribuição por zona (A, meio, B) + posições dentro da zona.
- **Posições se completam; não se avalia jogador isolado** [1F: hotspawn] → um faz jiggle por informação enquanto o outro segura a granada para jogar no contato; depois reposicionam em cruzado → o setup é gerado em pares de posições, não em cinco posições soltas.
- **Stack / aposta** → mais gente num bomb, aceitando o outro fino. Versão segura: um jogador sozinho no outro bomb só espia (pulo) com smoke na mão e, ao ver gente, joga a smoke para atrasar e chama. [C]
  - *Quando*: leitura de tendência (bomb preferido deles, round forçado/eco deles); em desvantagem numérica (4x5: colher informação e apostar). [C]
  - → valor do stack = P(TR vem aqui | memória da partida, economia) × ganho − P(vem no outro) × custo do retake.
- **Reforçar o meio** quando o TR toma o meio cedo: mais gente ali, cruzado e granada para manter o espaço. [1F: dignitas]
- **Jogar com vantagem (5x4)**: continuar no setup padrão e parar a agressão. [C]

**Cruzado (crossfire)**
- Dois ou mais defensores no mesmo espaço de modo que o TR só entra na linha dos dois ao mesmo tempo: todo peek vira 2x1; o pior caso é troca 1 por 1. [C]
- Bom cruzado cumpre ≥3 de 5 (dignitas) [1F]: uma flash só não cega os dois; dá para se esconder depois do primeiro duelo; difícil de flanquear; difícil de tirar com molotov/smoke; cobre o bomb inteiro. → **função de pontuação pronta para o gerador de posições CT.**
- "Divisão de atenção" (dois em quinas fazendo jiggle) quando não dá para segurar o bomb todo: compra tempo e sobrevive a vários contatos; critérios parecidos (os dois conseguem se esconder 1–2 s, não são cegados juntos, difícil de flanquear). [1F: dignitas]
- "Um e pronto" (jogador sozinho em posição inesperada que escolhe o início da luta e provavelmente troca 1 por 1): só com vantagem numérica. [1F: dignitas]
- Isca e troca: um aparece e some para o colega matar quem veio atrás; ou um escondido atrás do que aparece. [C]

**Agressão de início de round**
- *Para tomar espaço*: contra time que se arma na frente do bomb, esconde informação até explodir ou vive avançando. [1F: dignitas]
- *Para quebrar a execução*: contra time que executa muito e usa todos os jogadores; abate cedo ou na fase de preparação desmonta o plano. [1F: dignitas]
- *Em desvantagem*: agredir do lado oposto do mapa por informação ou abate, só com vantagem na mão (flash de colega, 2x1). [1F: dignitas] Outra fonte diz o contrário em tom: depois de perder um, o erro comum é sair agredindo para "recuperar"; o certo é colher informação e apostar. [1F: scope.gg] → as duas convergem em "agressão só com apoio e com motivo"; o avaliador exige flash ou parceiro.
- *Voltar*: pegou a informação ou o abate, recua para o setup; AWP que matou muda de ângulo. [C]
- Espiar com pulo e smoke na mão é a forma barata de informação. [1F: hotspawn]

**Rotação**
- **Não girar por barulho ou granada do outro lado** → pode ser fake. [C]
- **Informação suficiente para girar inteiro**: C4 visto perto do bomb / plantado; vários colegas mortos num lado; call do IGL. [C] Parcial: colega confirma que o lado dele está vazio → pode mandar mais gente. [1F: esportsrambles]
- Sinais intermediários (giro parcial do rotador): som de execução, granadas de execução, contagem de TR vistos (≥3 num lado), baixa do entry deles (o resto vem atrás). [C]
- Se a execução do outro lado "trava e fica quieta" e o C4 não apareceu, ficar. [1F: esportsrambles]
- **Ordem**: rotador primeiro, meio depois, âncora por último. [C]
- **Rota**: caminho seguro conforme quem controla o mapa; cuidado com o lurker esperando a rotação; avisar ao sair. Rotação rápida abre buraco explorável. [C]
- **Tempo depende do mapa** (bombs próximos giram fácil). [C] → `AnaliseMapa.tempo_rotacao[zona→bomb]` define o quanto se pode esperar antes de girar.
- → Regra de crença: `giro = f(TR confirmados no bomb X, C4 visto, granadas de execução gastas, TR não localizados)`; quanto mais TR "não localizados", menos gente sai.

**Granada defensiva**
- Granada no CT serve para atrasar e ganhar tempo no ponto de estrangulamento, não para ganhar duelo evitável. [C]
- Uma smoke e uma incendiária por jogador: usar com critério; pedir "resmoke" ao colega quando a sua acaba. [1F: dignitas]
- O TR vai tentar fazer você gastar (jiggle para tirar molotov e flash). [C]
- Guardar uma ou duas smokes do time para o retake/fim de round e um molotov para limpar canto no retake; guardar uma flash para a entrada deles. [C]
- Incendiária do CT pega chão mais rápido: é a ferramenta contra rush. [1F: metabot]
- Dropar granada para o âncora. [1F: hotspawn]

**Jogar pelo retake e recuar**
- Recuar quando segurar custa a vida sem ganho: ameaçado, o âncora entrega o bomb vivo. [C]
- Bombs diferentes têm chance de retake diferente; em bomb de retake ruim, vale mais salvar. [1F: refrag] → `AnaliseMapa.dificuldade_retake[bomb]`.
- Defensivo com vantagem e quando o TR está ficando sem opção/tempo; agressivo quando precisa de espaço ou está em desvantagem. [1F: dignitas]
- Ser imprevisível: uma ou duas surpresas por metade. [1F: dignitas]

---

## 6. Meio do round

**Eventos que fazem o IGL reavaliar** [C]
- Primeiro contato; baixa de qualquer lado (e onde); informação do default que contradiz o plano; granada-chave gasta (nossa ou deles); rotação detectada; AWP localizado; C4 visto/caído; marcos do relógio.
- → É exatamente a lista de gatilhos do passo 5 do laço (seção 4.1 do plano). Fora desses eventos, vale o compromisso.

**Saídas possíveis** [1F: guildorder; lista ampliada por guildorder/role-archetypes]
- Converter em jogada ensaiada; tomar e segurar; reiniciar. Ou, em termos de ritmo: acelerar uma vantagem, desacelerar para fazer o defensor esperar, reagrupar para bater junto, girar, abandonar uma área fraca, preservar equipamento (save).

**Protocolos comuns ("se acontecer X, fazemos Y")** — a maioria é [SF] na forma de regra; a ideia de ter protocolo e ramo de falha combinados é [C].
- *Ganhamos o pick de abertura (5x4 TR)*: não devolver a vantagem; jogar em dupla pela troca; ou bater rápido no bomb que perdeu o jogador, ou pressionar o espaço que ele deixava e forçar o CT a reagir. [C: scope.gg, dignitas buddy]
- *Perdemos um no default (4x5 TR)*: reagrupar; fechar o corredor que abriu; jogar por troca em dupla; decidir entre execução com o que sobrou ou pick do outro lado. [C na ideia de jogar por troca]
- *Entry caiu sem troca na entrada*: não abrir a mesma linha seco; granada na posição de quem matou, ou trocar de entrada. [C com o plano e com estnn]
- *C4 caiu*: recuperar vira a tarefa nº 1 do grupo mais próximo, com granada ou espera. [SF]
- *Ouvimos rotação saindo do bomb X*: lurker/ponta avisa; IGL converte para X se houver tempo. [C]
- *CT avançou e morreu*: aquele lado está com um a menos; converter para lá. [C]
- *CT (lado defensor) perdeu o âncora cedo*: bomb provavelmente perdido; reagrupar para retake ou salvar. [1F: hotspawn]
- *CT matou o entry e ouviu o lurker longe*: sabe onde estão os outros (4 de um lado, sem meio); pode adiantar ajuda. [1F: dignitas anti-strat II]

**Vantagem e desvantagem numérica**
- Referência: em matchmaking ~60% dos 5x4 terminam com vitória de quem tem 5; times de topo passam de 75% (TR e CT), e os melhores em 4x5 ganham ~33–37%. [1F: scope.gg, dados de 2020] → régua para calibrar depois (não para forçar agora).
- **Com vantagem, CT**: segue o setup, para a agressão, usa granada para atrasar; em 5x3 joga por informação e abre em dupla. [C]
- **Com vantagem, TR**: não dá duelo isolado; anda em dupla; usa o lado que ficou fraco; fake para pegar rotação; quer que o pior caso seja 1 por 1. [C]
- **Em desvantagem, CT**: informação e aposta (stack), agressão medida com apoio, jogar pelo retake; não sair sozinho para "recuperar". [C]
- **Em desvantagem, TR**: jogar junto pela troca; o entry pode jogar mais solto; buscar o adversário relaxado em ângulo inesperado (quem está na frente erra mais por excesso de confiança). [1F: scope.gg; dignitas clutch via busca]
- → No avaliador: o valor de "duelo isolado" depende do placar de vivos. Com vantagem, o peso de morrer sobe; em desvantagem, sobe o valor de opções de variância alta (ângulo inesperado, flanco).

**Pós-plant (TR)**
- 40 s são o recurso principal. [C]
- Posicionar cedo; avisar onde está; ângulos complementares (cruzado sobre o C4 e sobre as entradas do retake). [C]
- **Com mais vivos**: passivo, gastar granada cedo para queimar relógio, obrigar o CT a agir; agressão aqui entrega o que o CT precisa (abates cedo). [1F: refrag]
- **Com menos vivos**: buscar igualar — flash na passagem, posição avançada para flanquear, aceitar duelo; jogar passivo por troca perde, porque o CT tem gente para trocar. [1F: refrag]
- Não abrir logo depois de plantar; contar ~3 s. [1F: hotspawn]
- Encadear smokes na passagem (combinar quem joga primeiro); incendiária no C4 para negar defuse. [1F: refrag]
- Não se expor para jogar a granada. [1F: refrag]
- Jogar "pelo C4": com pouco tempo, quem tem linha para a bomba espera o som do defuse para abrir. [SF]

**Retake (CT)**
- Agrupar, não ir um a um; quem chega primeiro não faz contato sozinho. Morrer sozinho sem troca revela ao TR que o resto ainda não chegou. [C]
- Dividir: um joga granada, um limpa ângulos, um cobre o peek; limpar passo a passo. [1F: hotspawn]
- Anunciar a granada antes, para não dobrar smoke/molotov no mesmo lugar. [1F: refrag]
- Entrar por rota que o TR não identificou (flanco tardio). [1F: refrag]
- Conta de tempo: 40 s − defuse (5 com kit, 10 sem) = janela de luta; saber quem tem kit; sem kit e com pouco tempo não vale. [C]
- Encostar no C4 cedo (tap) cria dúvida e tira o TR do esconderijo; defuse falso como isca. [C]
- **Salvar**: sem tempo, bomb de retake ruim, ou perdeu os primeiros duelos do retake; salvar primeiro AWP e rifle. Decidir cedo e junto: dois salvando e um forçando é o pior resultado. Não transformar save em hábito. [C]
- Retake é "execução do defensor contra o relógio" [1F: csdb] → **usar o mesmo gerador da execução, com alvo = posições de pós-plant do TR e prazo = tempo do C4.**

---

## 7. Economia e tipo de round

Números (csdb; conferir com a camada 0): início 800; vitória 3250 (3500 por explosão/defuse); bônus de derrota 1400→1900→2400→2900→3400, e a derrota do pistol paga 1900; plant em round perdido dá +800 por TR; abate 300 (rifle/pistola), 600 (SMG), 900 (escopeta), 100 (AWP); compra cheia TR ~4400–5100, CT ~5500–5900; AWP 4750. [1F para os valores exatos; estrutura é C]

- **Pistol** → jogo de número e de distância. TR: andar em bloco, fechar distância com flash (Glock é precisa correndo), o da frente pula na quina para tirar a mira, smoke para cruzar e para plantar. CT: ficar perto de colega, deixar o colega fazer o primeiro contato e entrar em seguida, posição com rota de fuga, combinar um protocolo com o parceiro de bomb; colete costuma valer mais que granada; kit é luxo. [1F: refrag] → jogadas próprias de pistol: bloco de 4–5 por uma entrada, 1–2 granadas no total.
- **Eco** → gasta quase nada, junto. Empilhar os cinco, buscar um contato de perto (pistola melhorada mata de perto), e no TR plantar para ganhar o bônus. Compra solitária em eco só entrega arma. [C] → jogada "eco": 5 juntos, ponto de contato curto escolhido pela geometria (menor distância de engajamento), objetivo secundário = plant/roubar arma.
- **Forçada** → tem arma, não tem granada: favorece rush e contato de perto em vez de execução; precisa de plano dito, porque é o round que mais se dispersa. Justificada em match point contra, logo depois de vitória contra compra fraca deles, ou quando eles estão reconstruindo. Forçar round sim, round não é o vazamento mais comum. [C]
- **Meia compra** (SMG/rifle barato + colete) → funciona em linha curta; ruim contra rifle em linha longa. [1F: csdb] → o gerador escolhe caminhos com distância de engajamento baixa.
- **Anti-eco (CT ou TR com compra contra eco)** → manter distância, não dar duelo de perto, negar o plant, não entrar isolado em canto; SMG/escopeta rendem mais por abate. Esperar empilhamento e rush do lado pobre; a rodada seguinte ao eco deles costuma ser compra cheia. [C] → setup CT anti-eco: posições recuadas e em dupla, linhas longas, granada na entrada provável do bloco.
- **Compra cheia** → os cinco juntos; quatro rifles e uma pistola troca como meia compra. Só com compra cheia cabem default longo e execução com muita granada. [C]
- **Ler a compra do outro prevê a jogada** → pobre = rápido e junto; rico = default e execução. [C] → `Chamador` do CT estima a compra do TR (derrotas seguidas, armas vistas, plant no round anterior) e ajusta a probabilidade de rush/stack.
- **Depois de perder o pistol** → linha mais comum: eco e compra no 3º; alternativa: forçada com colete no 2º. Fontes divergem. [divergência entre guias] → traço do IGL/coach, não regra fixa.
- **Compra e call andam juntas** → decide-se a compra, depois o tipo de round, e anuncia-se as duas. [1F: guildorder]
- **Salvar** → arma cara primeiro; CT com C4 plantado muitas vezes ganha mais tentando o retake do que se escondendo; TR sem plant e sem chance salva. [1F: csdb]

---

## 8. Adaptação na partida e anti-strat

**Ler tendências**
- O que se anota do adversário [C]: bomb preferido e frequência; ritmo (rápido/lento) e a que altura do relógio executa; primeiro movimento do round; quando e onde joga granada; quem peeka o mesmo ângulo na mesma hora; onde o AWP joga; reação a gatilho (ex.: "depois da smoke no meio, sempre sobem dois por tal lado"); como reagem sob pressão e no fim do mapa [1F: entrevista do coach ash].
- → `MemoriaPartida` por adversário: contadores por (tipo de round, bomb, ritmo, entrada), posição do AWP por round, granadas por tempo, e pares gatilho→resposta. Recalcular a cada poucos rounds, não a cada round. [1F: nowloading]
- **Papéis que entregam o plano** (dignitas II) [1F]: lurker, AWPer e entry são os três que dizem onde o resto está; IGL e suporte dizem pouco.
  - Lurker fixo num canto → o resto está do outro lado. Dá para confirmar em três níveis de risco: ouvir; avançar num ponto vizinho; avançar em cima dele (de preferência em dois).
  - Entry apareceu/morreu → o resto vem logo atrás: adiantar ajuda para lá.
  - AWP deles fora → nosso time pode andar: libera espaço.
  - → regras de inferência da crença coletiva CT: `visto(lurker em Z)` desloca a massa de probabilidade dos outros para longe de Z.

**Ajustar**
- CT: stack no bomb preferido e giro adiantado; molotov para atrasar time que executa em tempo marcado; agressão no ponto e na hora em que eles se armam. [C]
- TR: granada cedo no bomb onde eles sempre se armam igual; segurar a smoke até ouvir passo; alternar ritmo; mudar de corredor porque o AWP joga ali. [C]
- Depois de round grande (clutch, virada), mudar ritmo ou posição para explorar a hesitação. [1F: nowloading]
- **Risco do anti-strat**: se o adversário percebe, vira contra (ele faz o gatilho de propósito). [1F: dignitas] → o próprio contador vira tendência legível; aplicar custo de previsibilidade também às respostas.
- Variação e aleatoriedade deliberadas tornam o estudo inútil; times trocam até funções e economia para não ter padrão. [1F: khelnow] → reforça "ser previsível custa" do plano.
- No nível mais alto todos conhecem as mesmas jogadas e granadas; o que separa é disciplina, confiança e lidar com pressão. [1F: ash] → bom argumento para os atributos mentais pesarem na execução, não só o repertório.

**Contra AWP**
- Regra zero: não dar duelo seco de rifle contra AWP armado em linha longa; não re-peekar o ângulo onde o colega acabou de morrer; não re-peekar depois de errar. [C]
- **Smoke** na linha dele (primeiro descobrir onde ele está). [C]
- **Flash** e abrir: melhor opção quando existe. [C]
- **Shoulder peek / isca**: mostrar só o ombro para tirar o tiro; abrir de verdade na janela do ferrolho. [C]
- **Colega pula/cruza** para puxar a mira e o outro mata (o plano já prevê). [C]
- **Abrir largo** para passar da mira pré-posicionada, de preferência em dois ao mesmo tempo por ângulos diferentes (troca dupla: ele só mata um). [C para o peek largo; SF para o duplo coordenado]
- **Janela depois do tiro**: logo após ele atirar (e matar) ele se esconde ou está no ferrolho: é a hora de avançar em cima. Arriscado. [1F: estnn]
- **Fechar distância / molotov** na posição dele: AWP de perto e em movimento perde. [SF]
- **Trocar de corredor** no round seguinte: tática, não só micro. [SF, coerente com o plano]
- → Na crença: `awp_em(linha)` troca o conjunto de opções de quem precisa cruzar aquela linha para {smoke, flash+abrir, isca+abrir, cruzar em dupla, outro caminho}; "abrir seco" fica com valor muito baixo.

**Como o AWPer se adapta**
- Muda de ângulo depois de cada tiro e de round para round. [C]
- Não atira na isca: segura o tiro para o corpo inteiro (leitura/disciplina). [SF]
- Ângulo com rota de saída e colega perto para trocar; pede flash para re-peekar. [C]
- Se está sendo sufocado com flash/smoke sempre no mesmo lugar: joga mais recuado, troca de bomb, ou vira o segundo homem de um cruzado. [SF]
- → traços do AWPer no save: `disciplina_isca`, `mobilidade`, lista de ângulos preferidos com contador de uso na partida.

---

## 9. Como times criam táticas novas

Esta foi a parte com menos fonte direta; o que há é coerente entre si, mas raso. Marcado de acordo.

- **A base é o default, não o livro de jogadas** → times novos não devem depender de jogada pronta cedo; primeiro o default, depois 3–4 jogadas por mapa/lado, por último o meio do round. [C: guildorder; documento-guia citado no resultado da busca] → ordem de crescimento do repertório do save: default → poucas execuções → variações.
- **Escada de formação do IGL** (guildorder) [1F]: 1) chama só o default; 2) escolhe jogadas ensaiadas; 3) assume o meio do round (com queda de resultado por algumas semanas); 4) economia e veto. → progressão natural do atributo/experiência de IGL no modo carreira.
- **De onde vêm as ideias** [C, com fontes rasas]:
  - observar a tendência do meta e o que outro time fez, e decidir entre adotar ou criar o contra;
  - rever as próprias partidas (principalmente derrotas) e a reação do adversário a cada jogada;
  - remixar jogada conhecida de um jeito pouco ortodoxo;
  - partir do adversário: um hábito dele vira uma jogada feita para puni-lo.
- **Foco em granada como motor de inovação** → um coach campeão resume a contribuição dele como "pensar a granada, não só jogar" [1F: entrevista zonic, vi só o trecho no resultado de busca; a página não abriu] → no gerador, variação nova nasce principalmente de (a) uma granada nova para uma linha que antes ficava aberta, (b) reatribuir quem joga o quê.
- **Processo de montar e testar** [1F: refrag] → acertar em servidor vazio granadas, teoria, tempos e espaçamento; o IGL mostra a visão dele ao time ou cada um refina o seu arremesso e tempo; depois testar ao vivo, porque jogada que parece boa na teoria pode desmontar em jogo; rever em 2D trajetórias, tempos e rotações. → ciclo do gerador: `montar` → `ensaiar` (simulação sem adversário: tudo aterrissa no tempo?) → `pracc` (contra setups variados) → `repertório`.
- **Critério de aprovação** (guildorder) [1F] → quatro colegas conseguem rodar a jogada sob pressão de tempo sem perguntar nada; jogada que pede tempo esquisito ou decisão no meio por quem não é o IGL falha em jogo. → métrica de `complexidade` da jogada (nº de arremessos sincronizados, nº de gatilhos condicionais) contra a `disciplina/coletivo` do time: complexidade acima da capacidade = erro de execução com causa.
- **Treino com objetivo** [1F: ash] → cada sessão tem um alvo (comunicação, refinar execução, confiança). → o treino do modo carreira escolhe o que treinar; não é "treino genérico".
- **Estrutura com liberdade** [1F: ash] → se tudo é roteirizado, mata-se a criatividade; o jogador precisa entender o porquê. Dá suporte ao princípio 8 do plano (toda ação tem motivo) e à discordância.
- **Testar em jogo que vale** [1F: khelnow] → algumas variações viram padrão, outras somem depois de um campeonato. → confiança da jogada sobe/desce com resultado em partida oficial mais do que em treino.
- **Eixos de variação de uma jogada base** [SF como lista; cada item aparece disperso nas fontes]:
  1. ritmo (mesma jogada mais cedo/mais tarde; com pausa antes);
  2. fake antes, ou fake do fake;
  3. posição e tempo do lurker;
  4. entrada trocada, ou virar split;
  5. conjunto de granadas (trocar smoke por molotov numa posição; versão "barata" para forçada);
  6. quem entra primeiro (troca de papéis);
  7. versão de contato (sem granada até o primeiro tiro);
  8. lugar do plant e estilo de pós-plant;
  9. resposta a hábito específico do adversário (AWP ali, stack ali).
  → cada eixo é um operador do gerador (o plano cita operadores em `Taticas.cs`); uma variação = jogada base + 1 ou 2 operadores + motivo registrado.
- **Condicionamento** → repetir um padrão para criar uma expectativa e depois quebrá-la (default que acostuma o CT, isca de rotação, fake). [1F: documento-guia, só pelo resultado da busca] → o `Chamador` pode escolher uma jogada de valor um pouco menor agora porque aumenta o valor de outra depois (valor = resultado esperado + ganho de condicionamento).

---

## 10. Comunicação

- **O que se chama** [C]:
  - inimigo: lugar pelo nome + número + arma, e dano se souber ("um AWP em X, com 40 de vida");
  - meu estado: perdi o ângulo / saí da posição / estou cego ou na smoke;
  - granada do inimigo vista; granada minha antes de jogar (flash para não cegar, smoke/molotov para não dobrar);
  - abate, não só morte (o lugar está limpo, não gastar granada ali);
  - kit, arma no chão, C4 (visto, caído, onde plantou);
  - mudança de plano antes de agir;
  - contagem para abrir junto.
- **Formato** → o mais importante primeiro; curto; nome de lugar, nunca "esquerda/atrás"; número exato, nunca "eles"; dizer quando é palpite; informação velha vem com a idade ("visto em B há 20 s"). [1F: prosettings, coerente com as demais] → estrutura da call: `{tipo, lugar, quantidade, arma, confianca, hora}`; a crença de quem recebe guarda a fonte e a idade (já previsto em `CrencaJogador`).
- **Quem fala o quê** → IGL: call da compra, o "go", conversões no meio do round. Jogadores: fatos (contato, número, granada). Durante a execução, só o IGL chama; opinião fica para o intervalo. [1F: guildorder] Variante: vivos decidem e se falam no meio do round. [1F: dignitas] → mesmo parâmetro `comando_central ↔ autonomia`.
- **Morto** → dá a informação antes de qualquer outra coisa e depois se cala; não narra o clutch do colega. [C] → o jogador morto ainda emite 1 call (com atraso) sobre quem o matou; depois disso não alimenta mais a crença do time (sem "fantasma").
- **Clutch** → silêncio; calls mínimas. [C]
- **Informação não comunicada é perdida** [1F: hotspawn] → evento visto e não chamado fica só na crença de quem viu (princípio 5 do plano).
- **Fontes de atraso e ruído** [C nas causas; os valores são sugestão SF]:
  - quem está em duelo fala depois (ou não fala);
  - canal ocupado: duas calls ao mesmo tempo → uma atrasa ou se perde (falar demais abafa passo do inimigo);
  - palpite dito como fato; "eles" em vez de número; lugar errado;
  - informação envelhecida tratada como atual;
  - call de direção ambígua.
  → modelo: atraso base 0,3–1,0 s por call; +atraso se o autor está em combate; fila de 1 call por vez no canal do time; probabilidade de perda/erro de campo (lugar vizinho, quantidade ±1) ligada ao atributo de comunicação e à pressão.
- **Pedido dirigido** → pedir a um colega específico ("fulano, flash para mim em X"), porque pedido ao grupo ninguém atende; quem inicia diz exatamente o que precisa. [1F: dignitas] → `pedido(parceiro, tipo, alvo)` como tipo de call que cria tarefa no parceiro.
- **Nomes de jogada não podem colidir com nomes de lugar** [1F: guildorder] → detalhe de interface/narração, mas vale para o painel.

---

## Regras para o gerador

Passo a passo para montar uma **execução** a partir da geometria. Cada passo indica de onde veio o princípio.

1. **Escolher o objetivo e o alvo.** Bomb (ou espaço) alvo sai do `Chamador`: defensores estimados, granadas CT gastas ali, picks, tempo de rotação do CT até lá, posição do AWP, custo de previsibilidade (bomb, ritmo, entrada já usados). (§1, §3, §8)
2. **Conferir o inventário.** Compra cheia → execução completa. Forçada/meia compra → versão rápida ou de contato. Eco/pistol → bloco por caminho curto. Se as granadas já foram gastas no default, reduzir a jogada. (§3, §7)
3. **Listar as entradas do alvo e o tempo de cada spawn/posição atual até cada uma.** Escolher 1 entrada (execução simples) ou 2 (split, só se a zona de ligação estiver tomada e as chegadas puderem coincidir em ≤1–2 s). (§2, §3)
4. **Listar as posições de defesa com linha de tiro sobre cada entrada**, com probabilidade de ocupação: conhecimento comum do mapa + crença do round + hábitos do adversário na partida. Classificar cada uma: *longa/AWP*, *de rotação* (por onde chega ajuda), *canto perto*, *posição elevada/escondida*, *cruzado* (par). (§2, §5)
5. **Ordenar as linhas por perigo** = probabilidade de ocupação × tempo de exposição de quem entra × vantagem do defensor (distância, cobertura, AWP).
6. **Atribuir granadas, da linha mais perigosa para a menos:**
   - linha longa/AWP e linha de rotação → **smoke** (corta visão; a de rotação também isola e atrasa);
   - canto perto / posição que ficaria atrás da smoke → **molotov** (tira do lugar);
   - posições que sobram com linha direta para a porta no instante da entrada → **flash** (estoura quando o primeiro aparece);
   - alvo ouvido/ferido ou canto empilhado → **HE**;
   - regra de parada: o entry deve enfrentar no máximo 1–2 ângulos ao mesmo tempo ao aparecer. Se sobrar mais que isso e não houver granada, trocar de entrada, virar split ou descartar a jogada.
   - Usar a física de arremesso para achar de onde cada granada cai no ponto; descartar as que não têm lineup seguro. (§2)
7. **Quebrar o cruzado do defensor.** Para cada par em cruzado, garantir que ao menos um dos dois esteja cortado (smoke), deslocado (molotov) ou cego (flash separada: um bom cruzado não é cegado por uma flash só). (§5)
8. **Reservar o pós-plant.** Guardar ≥1 smoke e ≥1 molotov do time para depois do plant; não gastar tudo na entrada. (§2, §6)
9. **Atribuir papéis.**
   - Entry: estilo agressivo, sem arremesso longo; lista ordenada de ângulos a limpar.
   - Troca (2º/3º): par do entry, distância de troca, ângulo complementar.
   - Suporte: maior parte dos arremessos; depois cobre flanco e guarda contra-flash.
   - Portador do C4: 3º ou 4º a entrar.
   - Lurker/retaguarda: lado oposto ou flanco; gatilho de avanço no `go` ou em som de rotação.
   - AWPer: linha longa de apoio, ou pick de abertura com flash de colega.
   - Cada granada tem dono; ninguém fica sem tarefa nomeada (princípio 7 do plano). (§2, §4)
10. **Definir posições de saída.** Pontos fora das linhas dos defensores, de onde cada um joga a sua granada ou entra, a poucos segundos da porta. (§2)
11. **Montar a linha do tempo de trás para a frente a partir do `go`:**
    - `t_entrada` do entry = instante de referência;
    - smoke aberta antes de `t_entrada` (tempo de voo + abertura);
    - molotov caindo pouco antes (fogo dura menos que smoke);
    - flash estourando em `t_entrada` (±0,2–0,3 s);
    - 2º entra a ~1–1,5 s do 1º; 3º logo atrás; portador depois do primeiro espaço seguro;
    - quem sai de mais longe sai antes;
    - tudo dentro do prazo do round: comprometer até ~0:50–0:40, salvo jogada de tempo curto. (§1, §2)
12. **Definir o plant e o pós-plant.** Ponto de plant = melhor pontuação de (posições de pós-plant com linha para o C4 × segurança × distância das entradas de retake). Estilo: passivo e de relógio com mais vivos; busca de igualar com menos vivos. Posições em pares cruzados sobre o C4 e sobre as rotas de retake. (§2, §6)
13. **Escrever gatilhos e ramo de falha.** Para cada tarefa: "se o parceiro cair, troco em ≤2 s"; "se a flash não saiu, não abro seco". Para a jogada: "2 baixas sem pé no bomb → recuar ao espaço tomado e reiniciar/girar"; "rotação ouvida saindo do outro lado → converter". (§2, §6)
14. **Medir complexidade.** Nº de arremessos sincronizados + nº de gatilhos condicionais. Acima da capacidade do time (coletivo/disciplina/utilitária), simplificar. (§9)
15. **Ensaiar e pontuar.** Simular sem adversário (os tempos fecham?) e contra setups variados (a jogada sobrevive a stack, a agressão cedo, a AWP na linha?). Guardar com confiança inicial; a confiança sobe e desce com o uso em partida. (§9)
16. **Gerar variações com motivo.** Aplicar 1–2 operadores (ritmo, fake antes, lurk trocado, entrada trocada, conjunto de granadas, ordem de entrada, contato, plant) e registrar o motivo ("AWP deles joga na linha X", "já batemos rápido duas vezes"). (§3, §8, §9)

**Mesmo processo, espelhado, para o CT** (setup e retake):
- **Setup**: para cada bomb, gerar pares de posições e pontuar pelo critério do cruzado (≥3 de 5); distribuir por zona conforme tempo de rotação e tendência do TR; marcar âncora (último a girar) e rotador (primeiro); definir granada de atraso por entrada e o que fica guardado para o retake; definir a regra de giro por nível de informação.
- **Retake**: é uma execução com alvo = posições de pós-plant do TR e prazo = 40 s − tempo de defuse; entradas simultâneas, granada anunciada, portador do kit protegido; gatilho de save por tempo, vivos e dificuldade do bomb.

**Default** (para o gerador cobrir default, execução e rush, como pede a seção 5.1):
- escolher corredores que ameaçam bomb ou cortam rotação; vetor de ocupação (ex.: 1-3-1) com núcleo no corredor de mais opções e pontas em quem joga passivo;
- por posição: tarefa ativa, o que sonda, granada de presença permitida, granada guardada, fallback de contato;
- prazo e lista de conversões possíveis (que execução/split/fake sai de cada estado de informação).

---

## Fontes

Consultadas e lidas (resumidas com minhas palavras):
- Guild Order — estrutura de round do IGL: https://guildorder.com/games/csgo/guides/igl-round-structure
- Guild Order — arquétipos de função: https://guildorder.com/games/csgo/guides/role-archetypes
- Dignitas — Defaults in CSGO: An Explanation: https://dignitas.gg/articles/defaults-in-csgo-an-explanation
- Dignitas — Default strategies on T-side: Why, How and When: https://dignitas.gg/articles/default-strategies-in-csgo-on-t-side-why-how-and-when
- Dignitas — How to Play a Better CT Side: https://dignitas.gg/articles/how-to-play-a-better-ct-side
- Dignitas — Ultimate Defensive Set-Up Guide: https://dignitas.gg/articles/csgo-ultimate-defensive-set-up-guide
- Dignitas — The Buddy System: https://dignitas.gg/articles/blogs/CSGO/14294/csgo-concepts-the-buddy-system
- Dignitas — The Unwritten Rules of In-Game Leading: https://dignitas.gg/articles/the-unwritten-rules-of-in-game-leading
- Dignitas — The Art of Anti-Stratting: https://dignitas.gg/articles/the-art-of-anti-stratting
- Dignitas — Art of Anti-Stratting II (T-side players): https://dignitas.gg/articles/art-of-anti-stratting-ii-anti-stratting-t-side-players
- Refrag — What is a default: https://refrag.gg/blog/what-is-a-default-cs2
- Refrag — pós-plant (5 dicas): https://refrag.gg/blog/hold-the-line-5-tips-to-improve-your-afterplant-play-in-cs2
- Refrag — retake (5 dicas): https://refrag.gg/blog/reclaim-control-5-tips-to-improve-your-retakes-in-cs2
- Refrag — erros de entry: https://refrag.gg/blog/4-of-the-worst-cs2-entry-fragging-mistakes-that-you-probably-make/
- Refrag — pistol rounds: https://refrag.gg/blog/mastering-pistol-rounds-in-cs2-8-essential-tips-for-victory
- Refrag — uso da ferramenta pelo IGL (processo de teste de jogada): https://refrag.gg/blog/how-to-use-refrag-as-an-igl
- CSDB — tipos de tática: https://csdb.gg/types-of-counter-strike-tactics-explained
- CSDB — guia de economia: https://csdb.gg/guides/economy-guide/
- Hotspawn — How to Play T-Side in CS2: https://www.hotspawn.com/counter-strike/guide/how-to-play-t-side-in-cs2
- Hotspawn — How to Play CT-Side in CS2: https://www.hotspawn.com/counter-strike/guide/how-to-play-ct-side-in-cs2
- MetaBot — controle de mapa e fases do round: https://metabot.gg/en/cs2/guides/cs2-map-control-round-phases-default
- MetaBot — guia de granadas: https://metabot.gg/en/cs2/guides/cs2-utility-guide-grenades
- BLAST — guia do âncora: https://blast.tv/article/cs2-anchor-guide
- BLAST — guia do lurker: https://blast.tv/article/cs2-lurker-guide
- Esports Rambles — guia de rotação: https://esportsrambles.com/blog/comprehensive-guide-to-rotating
- SCOPE.GG — realizar a vantagem numérica (dados de 2020): https://blog.scope.gg/realizing-the-advantage-en
- ESTNN — como enfrentar o AWP: https://estnn.com/csgo-how-to-counter-the-enemy-teams-awper
- ProSettings — comunicação: https://prosettings.net/blog/communication-in-cs2-guide/
- Now Loading — ler padrões do adversário: https://nowloading.co/cs-2-advanced-strategies-reading-opponent-patterns-and-adaptive-play
- Khel Now — estratégias não convencionais: https://khelnow.com/gaming/competitive-cs2-unconventional-strategies
- Skin.Club — entrevista com o coach ash: https://community.skin.club/en/articles/exclusive-interview-with-ash-the-mind-behind-gamerlegion-success

Vistas só pelo resumo do resultado de busca (a página não abriu ou veio vazia) — peso menor:
- Documento-guia de montagem de time/playbook (Google Docs; condicionamento pelo default): https://docs.google.com/document/d/1cVbKN6hXXo-Qr8P9rSl7deavAanRxuC5S7SkQi4oyv8/export?format=pdf
- bo3.gg — fakes (poucos jogadores no fake): https://bo3.gg/news/cs2-fake-execute-feints-odds-on-baited-rotations
- bo3.gg — split (depende de controle de meio): https://bo3.gg/news/cs2-t-side-split-pushes-strategies-for-multi-site-pressure
- Escorenews — entrevista zonic (foco em granada): https://escorenews.com/en/csgo/news/59131-zonic-maybe-one-thing-that-i-contributed-to-cs-was-to-focus-on-utility-not-just-throwing-it
- MetaBot — tipos de peek (shoulder peek contra AWP): https://metabot.gg/en/cs2/guides/cs2-peeking-guide-wide-jiggle-shoulder
- Guias de retake/defuse (tempos de kit, tap, defuse falso): https://csdb.gg/guides/how-to-defuse-cs2/

**Limites desta pesquisa**
- Quase tudo é guia escrito para jogador/time amador ou semi-pro, vários da era CS:GO (2016–2020). Os princípios se mantêm; números e mapas podem ter mudado.
- Não achei fonte boa com número para distância e tempo de troca, para a posição exata de arremesso de flash, para protocolos de meio de round em forma de "se X então Y", nem para o processo real de criação de jogada em time profissional. Esses pontos estão marcados [SF] e são os primeiros a conferir na régua das demos e no olho.
- Vídeo (onde está a maior parte do material de IGL e de coach) não foi consultado.
