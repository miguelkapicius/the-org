# Progressão e economia

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0007, 0010, 0012, 0013, 0015, 0016, 0024, 0026, 0035, 0037, 0038, 0039

_Como o jogador evolui e quais recursos circulam._

> **[Sugestão]** = proposta do agente, ainda não validada.

Existem **duas economias** diferentes:

1. **Economia do round** (dentro da partida): dinheiro para comprar armas e utilitárias.
2. **Economia do clube** (manager): dinheiro do time para salários, contratações e melhorias da Gaming House.

## Recursos

- Dinheiro do round.
- Dinheiro do clube.
- Moral e entrosamento dos jogadores (decisão 0019, regras em `02-mecanicas.md`).
- **[Sugestão]** Reputação/prestígio do time, fãs.

## Fontes e sumidouros

**[Sugestão] Clube:**
- Fontes: prêmios de campeonato, patrocínios, venda de jogadores.
- Sumidouros: salários, contratações, melhorias da Gaming House, viagens/bootcamps.

## Salários, empréstimo e falência (decisão 0015)

Os jogadores têm **salários**. Se o dinheiro do clube acabar, o time **pode falir** e o jogo acaba (**game over**), mas há **avisos antes** e um **empréstimo** como saída.

**[Sugestão]** Salários **semanais**; receitas de prêmios de campeonato, patrocínio semanal e venda de jogadores; **empréstimo bancário** até um limite, em parcelas semanais com juros; saldo **negativo** abre um prazo de **4 semanas** para se recuperar, passado o qual vem a falência. Cada nível da Gaming House custa uma manutenção semanal. Todos os valores são provisórios.

## Evolução dos jogadores

A evolução dos jogadores é **multifatorial**: o jogo mistura vários dados, como **treino**, **experiência em partida** e **idade** (decisão 0013).

- **Idade:** jovens evoluem mais e ainda têm potencial a descobrir; o auge fica por volta dos 21 a 26 anos; depois há declínio gradual (o reflexo cai antes).
- **Treino configurável:** o manager escolhe o foco e a frequência de cada jogador por semana. Treinar evolui o atributo (permanente, com teto no **potencial**) e dá um **preparo** (bônus temporário nas partidas), mas gera **cansaço**, que reduz a habilidade na partida. É um contrapeso.
- **Treino por lado (decisão 0024):** além do foco, o manager escolhe se a semana treina **os dois lados, só o TR ou só o CT**. Treinar um lado sobe o **domínio** dele (até 1,8 ponto por sessão), mas o atributo em foco rende só 60 % nessas sessões; o lado que ninguém treina esfria 1 % por semana em direção ao neutro. Assim um jogador pode virar especialista no TR ou ficar completo.
- Todos os números são provisórios; os pesos finais ficam para o balanceamento.

## Contratação por negociação (decisão 0026)

Os jogadores do mercado **não têm preço fixo**. Cada um pede **luvas** (pagamento único) e **salário por semana**, e o quanto pede depende do que pesa para ele: **prestígio** do clube (circuito e ranking), se terá **vaga** de titular, a **estrutura** (Gaming House), as **finanças** (semanas de despesa no caixa) e o **clima** do time (moral e entrosamento). O manager negocia numa conversa: propõe luvas e salário (e pode garantir a vaga de titular), e o jogador aceita, faz contraproposta ou recusa, perdendo a paciência a cada resposta ruim. O salário acordado passa a ser o salário do jogador. Números provisórios.

## Contratos (decisão 0035)

**[Sugestão]** Todo jogador do elenco tem **contrato** de 1 a 3 temporadas (o prazo é escolhido na negociação; jovem prefere 3, veterano 1). Cada contrato tem uma **cláusula de rescisão** (preço de tabela × (1 + 0,5 por ano)) que um rival pode pagar para levar o jogador cobiçado, mais fácil se ele está infeliz ou parado no banco. **Rescindir** antes do fim custa **metade dos salários que faltam**. A **venda** rende menos quando o contrato está perto do fim. Com 13 semanas para acabar, o painel avisa; no fim, o jogador **sai de graça**, a não ser que o elenco ficasse com menos de 5 (aí ele fica mais um ano por +15 % de salário). A **renovação** é uma resposta calculada: ele pede salário e 30 % das luvas e **recusa** se a moral está abaixo de 30 ou se não quer ficar (prestígio, vaga, estrutura, finanças, clima). Números provisórios.

**[Sugestão]** Todo contrato pode acabar por **pedido do jogador** (decisão 0038): com a moral abaixo de 25 (6 % por semana), ou sendo um bom reserva parado e desmotivado (4 % por semana), ele **pede para sair**. O manager tem 4 semanas para **vender**, **pagar o bônus de permanência** (4 salários, moral +15) ou ignorar, e aí ele sai de graça. O manager também pode **rescindir** pelo botão do cartão (multa de metade dos salários que faltam, com Desfazer). Números provisórios.

## Fãs do clube (decisão 0037)

**[Sugestão]** Os **fãs** são uma moeda de progressão, mostrada ao lado do saldo. O clube começa com 2.000 e o topo chega a alguns **milhões**. Cada **mapa ganho** rende fãs (mais num circuito maior, contra um favorito e em sequência de vitórias; o circuito pesa 1, 5 e 40 do Amador ao Elite); cada **mapa perdido** tira (mais se o rival era mais fraco); vencer a **série** vale 3 mapas e ficar no pódio de um **campeonato** rende muito mais (campeão 40 mapas, vice 15, terceiro 6; um título do Elite vale cerca de 64 mil fãs). Os fãs **não se gastam**: cada nível da **Gaming House** pede um mínimo (2: 8 mil, 3: 60 mil, 4: 400 mil, 5: 2 milhões), além do dinheiro. Números provisórios (decisão 0039).

**[Sugestão]** Os fãs também **mexem no patrocínio** (decisões 0038 e 0039): o patrocínio semanal é multiplicado por um fator de 0,8 a 3,0 (1,0 com os fãs iniciais, +0,2 a cada vez que a base dobra: cerca de 1,9 com 100 mil, 2,6 com 1 milhão) e **atraem contratados**: a torcida entra como um fator na negociação (de -1 com cerca de 600 fãs a +1 com cerca de 640 mil; zero em 20 mil; estrela e jovem ligam mais). A torcida ganha um nome: Desconhecido, Local, Regional, Nacional, Mundial.

## Curva de dificuldade

_A definir._

## Desbloqueios

### Gaming House (decisão 0007)

O time tem **uma** instalação: a **Gaming House**. O manager investe para subir o nível dela.

O nível da Gaming House **libera** outras coisas que o manager pode melhorar, como **treinos** e **scouts**. Sem subir a Gaming House, essas opções ficam bloqueadas.

- **Níveis (decisão 0016, proposta do agente):** cinco, cada um com vantagens: 1 Alojamento, 2 Sala de treino (libera treinos), 3 Sala de análise (libera **scouts**), 4 Centro de performance (recuperação de cansaço e sessões coletivas), 5 Complexo de elite (analista de demos, bootcamp, mais patrocínio). A evolução é **gradativa**.
- **Scouts:** o manager dá uma tarefa a cada scout por semana: **estudar um adversário** (revela como ele joga: estilo, mapas fortes e fracos, estratégia) ou **procurar no mercado** (revela o potencial dos jogadores).
- **Efeito na partida:** o nível da Gaming House aumenta a chance de um timeout funcionar (ver `01-core-loop.md`).
- **Custo:** precisa ser **balanceado**: nem tão fácil de subir, nem tão difícil.

## Perguntas em aberto

- Qual a curva de custo por nível (para ficar balanceada)?
- Contratos (decisões 0035 e 0038, proposta): pesos da cláusula, da multa, da chance de um rival levar o jogador, da recusa de renovar, do pedido de saída e do bônus de permanência.
- Fãs (decisões 0037 e 0038, proposta): os pesos de ganho e perda, os mínimos de cada nível da Gaming House, a curva do patrocínio (0,8 a 3,0) e da atração de contratados, e quanto tempo deve levar chegar a cada faixa. Fãs devem ser gastos como moeda, em vez de só liberar?
- Espectadores da transmissão (decisão 0037): fórmula do número de pessoas assistindo a partir dos fãs (e do campeonato e do momento), onde aparece no HUD e se o adversário também tem o seu.
- Evolução: quanto pesa cada fator (treino, experiência, idade)? (o pico de idade e o declínio já estão decididos)
- Lados: o quanto o domínio de lado deve valer (hoje até ±8 % de habilidade) e quanto custa treinar um lado (hoje 1,8 ponto por sessão, atributo a 60 %)? (decisão 0024)
- Negociação: os pesos dos fatores, o quanto o desejo muda luvas e salário e a paciência (valores provisórios da decisão 0026).
- Balancear salários, juros, limite de empréstimo, prazo de falência e manutenção da Gaming House (valores provisórios das decisões 0015 e 0016).
