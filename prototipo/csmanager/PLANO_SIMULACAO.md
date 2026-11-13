# Plano da simulação — CSManager

Escrito em 2026-11-11 a partir do que o Gabriel explicou. É a fonte da verdade para qualquer sessão que mexa nos bots: leia antes de alterar `Assets/Game/Sim`. Se algo aqui estiver errado ou faltando, corrija aqui primeiro e depois no código. O GDD do jogo inteiro fica no repositório the-org; este documento cobre só a simulação da partida.

## 1. O que estamos fazendo

A simulação é a base do jogo. Tudo que vem depois (atributos estilo Football Manager, treino, coach, mercado) se apoia nela. O alvo desta fase é um time de cinco jogadores "15 em tudo": um jogador bom padrão, sem nada especial e sem nada de ruim, que erra às vezes.

O que a partida precisa parecer: uma partida de CS de verdade. Jogo coletivo, com tática escolhida na compra, leitura no meio do round e adaptação ao longo da partida. Dinâmica e gostosa de assistir, com rounds diferentes uns dos outros e jogadores que pensam e jogam de jeitos diferentes.

## 2. Como é um round de CS (o modelo que seguimos)

1. **Compra.** O IGL passa a call do round enquanto o time compra. A call sai da economia, do placar, do que o adversário vem fazendo e do spawn de cada um.
2. **TR.** A call pode ser um rush, uma execução ensaiada com granadas, um default para pegar espaço e informação, uma jogada pelo meio, um split, um fake, um jogador avançado para pick. Cada caminho tem variações (no B: pelo meio cortando pelo caminho de rato; no A: smoke na frente, gente pelo conector e pelo palácio).
3. **CT.** Joga um setup: default 2-1-2 em muitos rounds, aposta num bomb em outros, agressão cedo para informação, sempre com âncoras. Gira conforme a informação, tenta adivinhar onde o TR está. Na maioria dos mapas segurar é mais fácil.
4. **Meio do round.** Tudo pode mudar. O IGL de cada lado lê o que aconteceu (baixa, barulho, granada, quem apareceu onde) e mantém, força ou troca a jogada. Jogadores também contribuem com informação e ideia.
5. **Fim.** Plant, pós-plant, retake, save. Cada um com decisão própria de time.

Cada jogador executa a tática com o **micro dele**: função (entry, suporte, lurker, AWPer, rifler, âncora), arma na mão e estilo pessoal. Um AWPer se porta diferente de um rifler, e um AWPer é diferente de outro.

## 3. Princípios

Estes valem para todo código de bot. Quando uma mudança contrariar um deles, a mudança está errada.

1. **Crença, não verdade.** O bot só sabe o que viu, ouviu ou foi avisado. Ninguém tem hack. A crença pode estar errada, e é daí que vem boa parte dos erros.
2. **Decisão por avaliação, não por sorteio.** O bot lista o que pode fazer, estima o resultado de cada opção com a crença que tem e faz a melhor. Se a melhor jogada é bangar, ele banga. O cérebro antigo sorteava entre opções com pesos, e por isso saía "vou assim mesmo" quando o certo era a flash.
3. **O erro tem causa.** Jogador erra porque a crença estava errada, porque não percebeu algo, porque não conhece a jogada, porque avaliou mal sob pressão, porque a mão falhou ou porque o estilo dele puxa para um lado. Nunca porque um dado mandou. Jogador melhor erra menos em cada uma dessas causas.
4. **Coletivo primeiro.** A decisão de cada um parte do que o time está fazendo. Par para troca, cruzado, granada de um para o outro entrar, um abre pulando para tirar a mira do AWP enquanto o outro mata.
5. **Cada jogador é um jogador (sem mente de colmeia).** Mesmo com status iguais, cinco jogadores não pensam a mesma coisa na mesma hora. Vale desde já, antes dos atributos:
   - **Crença própria.** Cada um sabe o que ele viu e ouviu. O que o colega viu só chega por call, com atraso, e pode chegar incompleto.
   - **Relógio próprio.** Ninguém decide no mesmo instante: cada um percebe, reage à call e muda de ideia no seu tempo.
   - **Jeito próprio.** Cada jogador tem manias fixas no save (ângulo preferido, ritmo, quanto arrisca, granada que gosta), mesmo sendo "15 em tudo".
   - **Tarefas complementares.** Para o mesmo objetivo, cada um faz uma parte diferente: enquanto um joga a smoke, outro segura o flanco e outro se posiciona para trocar. Se o colega já está fazendo algo, eu faço o que falta, não a mesma coisa.
   - **Jogar junto é normal, desde que com função.** O IGL pode mandar todo mundo para o mesmo lugar, ou pedir "fica perto para me trocar". Estar junto não é o defeito; o defeito é estar junto e não cumprir a função. Quem está ali para trocar troca quando o colega cai; quem está de apoio joga a granada ou cobre o outro ângulo. Toda tarefa diz o que o jogador faz quando o gatilho dela acontece.
   - **Discordância.** Um jogador pode ler a situação diferente do colega e do IGL, e às vezes agir pela leitura dele.
6. **Compromisso.** Decisão tomada vale até a situação mudar (baixa, informação nova, relógio, granada). Não se reavalia no vazio a cada segundo.
7. **Ninguém parado sem função.** Quem está parado está segurando algo que importa, esperando algo que tem nome, ou não devia estar ali.
8. **Toda ação tem motivo que dá para dizer em uma frase.** Se o bot anda de shift, é porque quer chegar sem barulho. Se vai bangar para si mesmo, não vai de shift.
9. **Memória.** O que o time aprendeu no round e na partida continua valendo: tem AWP no meio, aquele canto costuma ter gente, a jogada X deu errado duas vezes.
10. **Adaptação.** Os dois lados se ajustam ao longo da partida. Um jogador pode cair cinco vezes na mesma coisa, mas a chance de tentar outra saída cresce a cada vez.
11. **Comportamento antes de número.** Uma mudança é boa quando o bot passa a fazer a coisa inteligente na cena. Equilíbrio de lado, % de plant e afins são calibragem e vêm depois. CT um pouco mais forte é normal.

## 4. As camadas

Cada camada só conversa com a de cima e a de baixo. É isso que impede o cérebro de virar outra pilha de regras.

| Camada | O que faz | Estado hoje |
|---|---|---|
| 0. Regras do jogo | Tiro (spray, tap, precisão andando por arma), movimento, granadas, som, economia. É a verdade do mundo. | Existe e fica. Faltam bunny hop e detalhes por arma. |
| 1. Percepção e memória | O que cada jogador vê e ouve: inimigo, passo, tiro, recarga, plant, granada vindo e de onde. Guarda por tempo realista. | Existe em parte. Memória curta e sons recentes; revisar. |
| 2. Crença | Onde o time acha que cada inimigo está e o que tem na mão. Junta o que cada um percebeu e o que foi comunicado. Inclui hábitos do adversário na partida. | Existe (Intel). Fica, com revisão: crença por jogador e hábitos. |
| 3. Macro (IGL) | Call na compra, leitura no meio do round, adaptação entre rounds. CT: setup, rotação, âncora, retake. | Existe como regras espalhadas (Igl.cs). Refazer em cima da leitura coletiva. |
| 4. Função e tarefa | Quem faz o quê dentro da jogada: quem smoka, quem abre, quem troca, quem leva o C4, quem fica de lurk. | Parcial. Refazer. |
| 5. Decisão individual | Com a tarefa na mão, escolher a ação: opções, avaliação pela crença, a melhor. | É o Brain.cs (8 mil linhas de regra). Refazer. |
| 6. Execução | Mira, reação, spray, counter-strafe, tipo de peek, movimento fino. | Existe e está razoável (troca tardia ganha 47%). Fica, ganha técnicas. |

### Sobre crença e "juiz"

A crença fica: é o jeito certo de modelar jogador sem hack. O "juiz" (estimar a chance de um plano dar certo) também fica, e vira a peça central da decisão. O que sai é o sorteio por pesos. A técnica que entra é a combinação de:

- **jogadas como planos de time** com tarefas e ordem (a call vira um plano com papéis, não cinco ordens soltas);
- **quadro do time** (a leitura coletiva, já iniciada em `Coletivo.cs`);
- **escolha por valor esperado** em cada jogador, usando o juiz para comparar "abro seco", "bango e abro", "espero o colega", "saio da linha";
- **demos só como régua**: servem para conferir se o formato do round parece o de um pro (tempos, espaçamento). Não são a fonte das táticas (seção 5.1).

Aprendizado de máquina de ponta a ponta não entra agora: não daria para explicar a decisão nem ligar aos atributos do jogador.

## 4.1 Arquitetura técnica

Código novo em `Assets/Game/Sim/Cerebro/`, ao lado do cérebro velho. Uma flag por time escolhe qual cérebro joga, então dá para rodar novo contra velho no Headless.

**O laço de um passo da partida**
1. O jogo calcula visão, som e granadas (já existe).
2. **Percepção** transforma isso em eventos de cada jogador: vi fulano, ouvi passo ali, granada veio de lá, colega caiu.
3. **Crença do jogador** é atualizada com os eventos dele.
4. **Comunicação** entrega as calls que estavam a caminho.
5. **IGL** só age quando algo muda (baixa, informação, relógio, granada gasta): mantém, ajusta ou troca a jogada.
6. Cada jogador, no relógio dele ou quando recebe um evento, roda a **Decisão**.
7. A **Ação** escolhida segue executando até terminar ou até a situação mudar.

**Peças**

| Peça | O que guarda e faz |
|---|---|
| `Percepcao` | Fila de eventos por jogador. É a única porta de entrada de informação no cérebro. |
| `CrencaJogador` | Para cada inimigo: onde acho que está, há quanto tempo, de onde veio a informação (vi, ouvi, call), que arma tem. O mapa de calor difuso continua por time, como conhecimento comum do jogo. |
| `Comunicacao` | Calls com tipo, lugar, autor e hora. Chegam com atraso e podem se perder (jogador em duelo, comunicação ruim). É o que separa "eu sei" de "o time sabe". |
| `Coletivo` | Quadro do time: vivos, momento, postura, quem está sem troca. Já existe. |
| `Jogada` | Objetivo + fases + tarefas. Cada **tarefa** tem tipo, alvo, parceiro, gatilhos ("se o parceiro cair, troco") e condição de fim. É dado, não código. |
| `Chamador` (IGL) | Na compra, compara as jogadas do repertório pelo resultado esperado contra o que leu do adversário e escolhe. No round, reage a eventos. |
| `AnaliseMapa` | Calculada uma vez por mapa: entradas de cada bomb, tempo de cada spawn, posições de defesa e a linha de tiro de cada uma sobre cada entrada. |
| `GeradorTaticas` | Monta `Jogada` a partir da `AnaliseMapa`: escolhe caminho, granadas que cortam as linhas, papéis e tempos. Usa a física de arremesso que já existe para achar a granada. |
| `Decisao` | Lista as opções que a tarefa permite, pede o valor de cada uma ao `Avaliador`, faz a de maior valor e se compromete até a situação mudar. |
| `Avaliador` (o juiz) | Estima chance de ganhar o duelo, de morrer, de cumprir a tarefa e o tempo gasto, usando a crença do jogador. |
| `Acoes` | Executa: ir, segurar ângulo, abrir (tipo de peek), bangar e abrir, trocar, recuar, plantar. Chama as peças de execução que já existem. |

**Como a decisão escolhe (exemplo da quina)**
Tarefa: entrar no bomb. Crença: um inimigo atrás da quina, visto há 2 s. Opções e o que o avaliador devolve:

| Opção | Chance de sair vivo com o abate | Custo |
|---|---|---|
| Abrir seco | baixa (ele está pré-mirado) | nenhum |
| Bangar e abrir | alta | uma flash, 1,5 s |
| Esperar o colega e abrir em dupla | média-alta (tem troca) | 3 s |
| Dar a volta | depende do caminho | 8 s |

Com tempo e flash na mão, "bangar e abrir" ganha, e ele entra correndo porque a ação é uma só. Sem flash e com colega perto, espera. Sem tempo, abre seco. Nenhum sorteio.

**Outro exemplo: o colega caiu e não deu para trocar**
A informação "tem inimigo ali" abre várias saídas, e quem escolhe é o jogador, pela situação dele: segurar a linha de quem matou, jogar uma granada na entrada, recuar para dentro do bomb e esperar, ou mudar de ângulo. Nenhuma é obrigatória; ficar olhando para outro lado é que não é opção.

**Por que o jogador não faz sempre a mesma coisa**
- A situação nunca se repete igual: crença, posição, tempo, vida, granadas e colegas mudam o valor de cada opção.
- Cada jogador avalia com a crença, o ruído e o jeito dele.
- **Ser previsível custa.** O valor de uma opção cai quando o adversário já viu aquilo nesta partida (mesmo ângulo, mesma jogada, mesma granada). Variar passa a ser a melhor jogada, com motivo.
- Quando duas opções valem quase o mesmo, a escolha entre elas varia, puxada pelo estilo do jogador. Quando uma é claramente melhor, ele faz essa. A diferença para o cérebro velho é essa: lá o dado decidia mesmo quando havia uma opção claramente melhor.

**De onde vêm os números do avaliador**
Do próprio jogo: uma bancada de duelos roda milhares de confrontos (arma, distância, quem está pré-mirado, cego ou não, vida) e gera a tabela de chance. Assim o que o bot espera bate com o que o jogo faz. O atributo de leitura de jogo distorce essa tabela para mais ou para menos.

**Onde entra a individualidade**
- Crença: cada um tem a sua.
- Relógio: cada um pensa em intervalos próprios e reage à call com atraso próprio.
- Avaliação: ruído conforme a leitura de jogo e a pressão.
- Jeito: preferências fixas do jogador somam ou tiram valor de certas opções.
- Tarefa: a jogada dá a cada um uma parte diferente.

**Explicação de cada decisão**
Toda decisão registra as três melhores opções com o valor de cada uma. Isso vai para a caixa-preta e para o painel, então dá para ver por que o bot fez o que fez.

**Custo de processamento**
Decisão a cada 0,2–0,5 s por jogador ou por evento, não a cada passo. A crença por jogador começa como tabela pequena. Mapa de calor por jogador (5 vezes a parte mais pesada da simulação) só entra se a tabela não bastar para a individualidade aparecer, e depois de medir o custo no jogo.

## 5. Conteúdo que a simulação precisa ter

Listas iniciais. Cada item vira dado ou código e pode ser ampliado com pesquisa.

### 5.1 De onde vêm as táticas: geradas, não copiadas

Decisão do Gabriel (11/11): tática tirada de demo não faz sentido para este jogo. As táticas nascem dentro da simulação, do mesmo jeito que nascem no CS, e ficam guardadas como **repertório** do IGL, do time e de cada jogador. Cada save vira uma história própria em cada mapa.

**Como uma tática nasce (o que o gerador precisa saber fazer)**
1. **Objetivo.** Tomar um bomb, tomar um espaço (meio, conector), conseguir um pick, punir uma rotação.
2. **Leitura do mapa.** Por onde se chega, quanto tempo leva de cada spawn, de onde o defensor costuma segurar e que linha de tiro cada posição tem sobre a entrada.
3. **Granadas pela geometria.** Smoke corta a linha mais perigosa; molotov tira do canto de perto; flash cobre o instante em que o primeiro aparece; HE em quem foi ouvido. A granada existe por causa de uma posição do defensor, não por tradição.
4. **Papéis e ordem.** Quem joga cada granada, quem abre, quem troca, quem leva o C4, quem fica de lurk, onde planta para o pós-plant cruzado.
5. **Tempo.** Todo mundo chega junto: quem sai de mais longe sai antes, a granada estoura antes de o primeiro aparecer.
6. **Variação com motivo.** Mesma jogada mais rápida ou mais lenta, fake antes, lurk do outro lado, caminho trocado porque o AWP deles joga ali. Já existe um começo disso em `Taticas.cs`.
7. **Treino e resultado.** A ideia é testada; a que se segura entra no repertório, a que falha em jogo perde confiança.

**Repertório**
- Do **IGL e do time**: jogadas conhecidas por mapa, com confiança e histórico. Quem tem os atributos de tática e leitura mais altos cria mais e melhor (função não dá bônus, ver seção 6).
- Do **jogador**: granadas que sabe jogar, posições e truques que aprendeu.
- Cresce com a carreira: round que deu muito certo vira jogada guardada; granada nova aprendida fica.
- O setup do CT nasce pelo mesmo processo, ao contrário: que entradas existem, quanto tempo a rotação leva, onde vale apostar.

O banco de rounds de demo que existe hoje (`EstrategiasDados.cs`) deixa de ser a fonte e sai quando o gerador cobrir default, execução e rush nos dois mapas.

**Jogadas de TR**
- Rush (A, B, com variação de caminho e de granada).
- Execução ensaiada por bomb (smokes, molotov, flash de entrada, ordem de quem entra).
- Default: espalhar, pegar espaço e informação, decidir aos 30–50 s.
- Controle de meio e corte (meio → caminho de rato → B; meio → conector → A).
- Split por duas entradas ao mesmo tempo.
- Fake com granada e barulho, e ida para o outro lado.
- Pick: um jogador avançado ou AWP abrindo o round.
- Jogada lenta de contato (anda de shift até o primeiro tiro).
- Eco, forçada e anti-eco têm jogadas próprias.

**Setups de CT**
- Default 2-1-2, aposta (stack) num bomb, B forte ou A forte conforme o mapa.
- Agressão cedo para informação e volta.
- Âncora por bomb, jogador de rotação, AWP no ângulo do round.
- Rotação por informação, com quem fica segurando o fake.
- Recuar e jogar pelo retake quando segurar não dá.
- Retake junto, com granada, por uma ou duas entradas conforme a vantagem.

**Micro e técnicas**
- Peek aberto, peek curto, jiggle, shoulder peek para tirar o tiro do AWP.
- Pré-mira, pré-fire, counter-strafe, tap de longe e spray de perto, por arma.
- Strafe no duelo quando confiante; sair do ângulo depois de matar.
- Jogar diferente quando acredita em AWP do outro lado.
- Dupla: um abre pulando ou largo, o outro mata; troca em menos de 2 s.
- Pezinho (boost) combinado, bunny hop na rotação e na saída.
- Faca na mão quando ninguém chega, arma na mão na zona de contato.
- Pegar arma melhor do chão; dropar para o colega.

**Granadas**
- Lineups de execução e de retake por mapa.
- Flash para si mesmo e flash para o colega (com aviso).
- Molotov para tirar de canto ou segurar avanço; HE em quem foi ouvido.
- Smoke para cruzar, para isolar e para cortar rotação.
- Erro de granada existe e depende do jogador.
- Ler a granada do adversário: de onde veio diz onde ele está.

**Leitura e adaptação**
- No round: baixa, barulho, granada usada, quem apareceu, quanto tempo resta.
- Na partida: onde o AWP deles joga, que bomb eles preferem, que jogada nossa falhou, quem está jogando muito.
- Os dois IGLs fazem isso; o AWPer também se adapta a quem tenta tirá-lo.

## 6. O jogador

Nesta fase todos são 15. Mesmo assim o desenho já separa o que cada atributo vai mexer, para os atributos entrarem sem refazer nada.

| Grupo | Exemplos | Onde age |
|---|---|---|
| Mecânica | mira, reação, controle de spray, movimento | camada 6 |
| Leitura de jogo | posicionamento, antecipação, uso de informação | camadas 2 e 5 (qualidade da avaliação) |
| Utilitária | conhecimento de lineup, precisão da granada | camadas 4 e 6 |
| Coletivo | comunicação, troca, disciplina tática | camadas 3 e 4 |
| Mental | calma sob pressão, confiança no dia, clutch | ruído da avaliação e da mão |
| Estilo | agressivo, passivo, lurker, estilo de AWP | puxa a escolha entre opções de valor parecido |

A arma na mão muda as opções (AWP, rifle, SMG, pistola) em qualquer jogador.

**Função não é qualidade.** Ser IGL, entry, AWPer ou âncora diz o que o jogador faz no time, não quão bem faz. Quem lê melhor o jogo, cria tática melhor ou erra menos é quem tem o atributo alto, seja qual for a função. Nesta fase todos são 15, então o IGL lê o round tão bem quanto qualquer colega leria; ele só é quem decide a call. Nenhum código pode dar bônus por função.

**Experiência na função (depois).** Jogar numa função faz subir, com o tempo, os atributos que ela exige: quem joga de IGL desenvolve leitura e tática, quem joga de AWP desenvolve a mira de AWP, e assim por diante. Gancho na simulação: a partida registra, por jogador, a função exercida e o que ele fez nela (calls dadas e acertadas, entradas, trocas, granadas), para o sistema de evolução usar.

## 7. Como saber se está bom

1. **Cenas de comportamento.** Situações montadas com resultado esperado claro. Exemplos: tem flash, sabe de inimigo atrás da quina e tem tempo → banga antes de abrir; colega caiu na linha do AWP → não abre a mesma linha seco; 4 contra 2 de CT → ninguém sai para duelo sozinho; C4 no chão vigiado → granada ou espera; bangou para si → entra correndo, não de shift.
2. **Olho do Gabriel** no `CSManager_TESTE.bat`, com a caixa-preta gravando para abrir o round exato.
3. **Régua dos pros** (demos) como referência de formato de round, posições e tempos.
4. **Equilíbrio por último.** Só depois que o comportamento estiver certo.

"Reprovou" passa a significar: falhou numa cena ou o Gabriel viu burrice. Não significa "o % mudou".

### 7.1 Demos de pro como exemplos, não como tabela

Decisão de 11/11: as demos servem para olhar casos, não para gerar estatística.
- **Caixa-preta dos pros.** Ler rounds de pro como linha do tempo, situação por situação (uns vinte casos de cada), e descrever em palavras o que eles fazem e em que ordem.
- **Replay lado a lado.** A mesma situação em dois clipes, o pro e o nosso bot, para o Gabriel julgar no olho.
- **Cena.** Cada situação lida vira uma cena da bancada com o resultado esperado escrito em frase.
- Número só para achar onde olhar primeiro.

Dado disponível hoje: o playbook de rounds por região (`demos_cs/cs-simulator/contract/artifacts/tactics_playbook.json`), que cobre o macro. Para o micro (posição exata, mira, granada) faltam arquivos de demo e um leitor.

**Macro do CT lido nos pros (12/11):** está em `Tools/pro/MACRO_CT.md` (o que os pros fazem em seis situações, o que o bot fazia, as causas e as cenas que saem disso). Scripts em `Tools/pro/` (`macro_ct.py`, `crenca_x_verdade.py`, `retake_lento.py`, `resumo_cx.py`); o Headless ganhou `-gradeAreas` para os scripts lerem a caixa-preta por região. O que os pros mostram e ainda falta no bot: o CT se mexe antes do contato (em 94% dos rounds alguém troca de posto), o resto do time reage à baixa ficando a meio caminho, e o setup varia muito.

**Crença 2, macro 2 e retake 2 (12/11, flags `crenca2Ligado`, `macro2Ligado`, `retake2Ligado`):** o som passa a ser do rastro de quem fez o som, o quadro do time conta vistos + ouvidos por zona (`Coletivo.delesNa`; os só vistos ficam em `vistosNa`), execução é a maioria dos vivos deles num lado, depois do plant quem não deu notícia está perto do C4, quem gira não desvia para arma/granada, e guardar ou retomar é decisão do time pela chance do placar de vivos. Falta na crença: noção de para onde o adversário costuma ir antes do primeiro contato (hábitos).

**Prioridade depois do primeiro teste do CT novo (11/11):** o Gabriel sentiu o micro melhor e o macro ruim, com o mapa de calor errado e o CT bem mais burro que o TR. Então a crença (camada 2) e a leitura de time do CT vêm antes da decisão individual.

## 8. Ordem de implementação

| # | Marco | Pronto quando |
|---|---|---|
| 1 | Leitura coletiva do time (`Coletivo.cs`) | Feito em 11/11: grava na caixa-preta, ainda não manda em ninguém. |
| 2 | Bancada de cenas de comportamento | Feito em 11/11: `Headless -cenas N` (quina conhecida, linha quente, vantagem do CT, flash própria). Ampliar a cada marco. |
| 3 | Decisão individual nova (camada 5) | Opções + avaliação pelo juiz + compromisso, com crença, relógio e jeito próprios de cada jogador (princípio 5), passando nas cenas de duelo, quina, flash e linha quente e em duas cenas novas: "todos fazendo igual" e "estava perto para trocar, o colega caiu: trocou ou tentou em 2 s?". |
| 4 | Jogadas como planos de time (camadas 3 e 4), CT | Setup, reação à baixa, rotação, segurar vantagem, recuar, retake. |
| 5 | Gerador de táticas e jogadas de TR | Jogadas nascem pela seção 5.1 (objetivo, geometria, granadas, papéis, tempo), com call na compra e mudança no meio do round. |
| 6 | Memória, adaptação e repertório | AWP conhecido muda a jogada; jogada que falhou perde força; o que deu certo entra no repertório do save. |
| 7 | Técnicas que faltam | Bunny hop, dupla abrindo junto, shoulder contra AWP, detalhes por arma. |
| 8 | Limpeza | Regras do cérebro velho que a camada nova substituiu saem do código. |
| 9 | Calibragem | Equilíbrio por mapa, depois os atributos. |

O cérebro velho continua no exe principal até o novo passar nas cenas e no olho. Cada marco entra primeiro no TESTE.

## 9. O que vem em cima da simulação (e os ganchos que ela precisa ter)

Nada disto se implementa agora. Está aqui para a simulação nascer com as entradas certas, sem precisar refazer depois.

**O jogador humano é manager e coach.** Como manager, contrata, cuida de finanças, patrocínio, marca e staff (psicólogo, fisioterapeuta, analista, olheiro, de 0 a 5 estrelas). Como coach, decide como o time joga.

| O que o coach faz | Gancho na simulação |
|---|---|
| Combina com o IGL, antes da partida, como jogar contra aquele adversário | O IGL recebe um plano de jogo: jogadas preferidas e evitadas, ritmo, bomb a evitar. Ele pesa isso junto com a leitura dele. |
| Modo rápido: "mais agressivo", "evite tal coisa" | Diretrizes simples que viram ajustes no mesmo plano de jogo. |
| Modo fundo: passa táticas específicas | O catálogo de jogadas aceita jogada escolhida ou montada pelo coach. |
| Timeout: 3 por partida, mais 1 a cada prorrogação | A partida aceita pausa entre rounds com até 3 ações do coach. |
| Ações no timeout: acalmar o time, quebrar o embalo do adversário, mandar a jogada do round, dar uma dica (evitar um bomb, mudar o jeito de jogar) | Ações mexem no estado mental do time, no plano de jogo ou fixam a call do próximo round. |

**Estado do jogador e do time.** Personalidade, característica, temperamento e momento entram depois, no espírito do Football Manager: o gerenciamento interfere no resultado.

| Estado | Gancho |
|---|---|
| Momento (embalo) do time: confiança que cresce com rounds ganhos, além da economia | Um valor por time, visível ou interno, que entra como bônus ou ônus pequeno no ruído da avaliação e da mão (seção 6, grupo Mental). |
| Confiança e pressão por jogador | Mesmo lugar: o jogador confiante arrisca o strafe e o duelo; sob pressão avalia pior. |
| Humor, química, cansaço, preparo, afinidade com o mapa | Entram como modificadores dos atributos antes da partida. |

**Prorrogação.** Regra de campeonato de CS: 12–12 leva à prorrogação em blocos de 6 rounds (3 de cada lado), vence quem chegar a 4 no bloco; empatou 3–3, outro bloco. Cada bloco dá 1 timeout a mais.

## 9.1 O que o Gabriel disse e não estava escrito acima

As falas dele na sessão de 10 a 12/11 estão guardadas, na íntegra, em `Tools/pesquisa/gabriel_falas_10a12nov.txt`. Quem for mexer nos bots lê esse arquivo: é a fonte deste plano. O que estava lá e faltava aqui:

**Sobre como trabalhar (ele repetiu quatro vezes)**
- "Só com números não dá pra ver se os caras estão jogando de forma coerente." Número não aprova nem reprova nada. Não ajustar código para recuperar % de plant ou de vitória.
- Demos de pro: ler uns vinte casos da mesma situação e descrever **em palavras** o que fazem e em que ordem; mostrar a ele pro × bot lado a lado; cada situação vira cena com resultado esperado em frase. Tabela de porcentagem de pro não é o entregável.
- Defeito de crença se corrige um de cada vez, conferindo na partida gravada e nas cenas.
- Ele não quer status toda hora: exe novo no TESTE, replay quando precisar do olho dele, ou bloqueio.

**Sobre o jeito de jogar**
- **O jogador tem opinião sobre a partida** e joga por ela: "acho que tem AWP", "aquele cara está jogando bem", "vamos evitar o bomb de fulano", "pegamos vantagem aqui, vamos fazer algo com isso", "peguei uma informação, vou usar".
- **Mind game e counterplay no duelo:** fazer o pique e matar, pegar cobertura, largar a troca de tiro e aparecer em outro lugar. Parado no aberto trocando tiro até alguém morrer é o defeito número um que ele vê.
- **Na espera, o pro não fica estátua segurando um pixel:** dá strafes pequenos, e a mira fica na altura da cabeça de onde o inimigo sai. Esperando a vez, olha para algo que importa, nunca de lado para tudo.
- **Quem está atrás de quem vai abrir tem função:** bangar para ele. Estar atrás sem fazer nada é o mesmo defeito do "junto sem função".
- **Pontas e âncoras** têm jeito próprio de jogar e de reagir; hoje reagem mal.
- **Vícios a matar:** trocar tiro na base CT; todo mundo indo para o mesmo cantinho quando a bomba vai explodir; o jogador do kit longe do bomb quando o time ganha o retake; AWP comprada para o AWPer que já tem AWP; TR abrindo na base CT sabendo que tem gente em vez de segurar a vantagem.
- **Caminho pela situação, não o mais rápido (12/11):** os bots vão sempre pela base CT e pelo caminho mais curto. O caminho é uma escolha como as outras: o mais seguro, o que surpreende, o que a crença do jogador diz ser melhor naquela situação.
- **Economia quebrada (fica para depois):** todo mundo compra AWP (ele quer uma, no máximo duas por time); ninguém dropa para o AWPer no começo do round; o AWPer que recebe um rifle e tem dinheiro compra a AWP e larga o rifle no chão, e ninguém pega.
- **Ganhar espaço, não trocar tiro de longe (12/11, depois do CT_NOVO3):** parado na base trocando tiro de pistola ou SMG de longe até acabar o tempo não existe. O pro chega mais perto, ganha espaço, mata; o CT com a bomba plantada tem a prioridade de desarmar. "Abre, toma um tiro, volta" em loop até morrer é medo, não jogada. O exemplo era o CT na base, mas vale para a classe inteira do erro, nos dois lados.
- **Cego depende do que ele acredita (12/11):** se acha que vem alguém, usa o que sabe do mapa e procura cobertura; se acha que não tem ninguém perto, pode ficar onde está.
- **Nenhuma jogada é proibida (12/11):** não existe "isso o jogador não faz". Atravessar a smoke do meio sozinho, de shift, é jogada certa no momento certo (o CT já girou, o barulho do outro lado cobre o passo, a smoke está abrindo). Toda opção entra na lista; o que muda é o valor dela pela situação e pela crença. Ao listar opções de uma decisão, não cortar nenhuma por regra.
- **Meio smokado pelo CT, opções do TR** (para a decisão de smoke no caminho): esperar abrir segurando a saída; ler a smoke como informação; atravessar junto com flash por cima; atravessar sozinho em silêncio; spray na smoke; HE ou molotov na saída; ficar de lado para punir quem empurra por dentro; largar o meio e virar o round; fake; smoke própria ou one-way na borda.
- **Sem dado, na prática (12/11, `semDado5Ligado`, `Cerebro/Vontade.cs`):** o cérebro velho decidia em mais de cem lugares com "sorteio < chance". A chance sai da situação e da crença; o dado sai. No lugar dele entra o jogador: cada jogador tem, para cada assunto, um ponto fixo (0 a 1) a partir do qual ele faz; a conta de antes vira o valor da jogada, e ele faz quando o valor passa do ponto dele. Perguntado de novo sobre o mesmo assunto em pouco tempo, o ponto baixa (quem esperou uma hora vai). Entre várias opções com peso, faz a de maior peso × gosto dele ÷ vezes que já repetiu no round. O "ruído" somado nas comparações virou a inclinação fixa do jogador para cada opção. O que continua sorteado de propósito: execução do corpo (lado do strafe, tempo de pausa, tamanho da rajada, erro de mira) e a tática do IGL no começo do round (ser imprevisível é a jogada certa enquanto o IGL não tem memória da partida; vira avaliação quando o repertório e o histórico entrarem).
- **Crença por jogador (12/11, `crencaJog5Ligado`):** tabela pequena por jogador com o que ELE viu e ouviu. Vale na hora (antes da call), dura a memória dele (6 a 11 s), e o que veio só por call ele pesa pela confiança dele em call (0,75 a 1,0). O mapa de calor segue do time. Toda conta de "quantos eu acredito ali" do jogador passa por `Brain.Acho`.
- **Colega caiu perto (12/11, `situ5Ligado`):** três jogadas com valor — trocar agora (duelo que eu vou ter: arma dele pelo placar, se ele já mira por onde eu apareço, se chego em menos de ~1 s, se ele recarrega, quantos em volta), segurar o ângulo (vale pelo quanto ELE precisa vir: de quem é o relógio) e sair da linha.
- **Smoke no caminho:** entrou "dar a volta" (a via mais curta que não passa pela smoke, com o tempo a mais e o risco que ele acredita nela).
- **Pós-plant** tem conceitos próprios (cruzado, malote, jogar o tempo) que os bots não sabem.
- **Rounds executados** (execução ensaiada) são diferentes de rounds soltos e de rounds de leitura do IGL; os três existem.
- **Reações a informação** que ele perguntou uma a uma e viram cena: ouvir inimigo recarregando; ver de onde veio uma HE; ouvir o plant; saber que o colega morreu ali.
- **Movimento:** bunny hop e pezinho existem no código, mas os bots não sabem quando usar. Fica para depois dos pontos acima.

**Sobre o jogo que vem depois (não implementar agora)**
- É um Football Manager de FPS tático, isométrico. Memória entre partidas e entre séries; repertório de granadas e de táticas; treino por posição; mentoria; treinar uma habilidade específica do jogador (bom de pistola, spray, AWP agressivo).
- Jogadores muito bons e muito ruins (lendas 20). O alvo de agora continua sendo o 15 em tudo.

## 10. Em aberto

- Quanto da call o jogador pode contrariar por conta própria (disciplina tática vira atributo?).
- Pesquisar a fundo como times montam tática e protocolo de default (material de CS competitivo) para o gerador da seção 5.1.
- Como o coach (o jogador humano) interfere na call e na adaptação.
- Pesquisar e ampliar as listas da seção 5 com material de CS competitivo.
