# Progressão e economia

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0007, 0010, 0012, 0013, 0015, 0016

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
- Todos os números são provisórios; os pesos finais ficam para o balanceamento.

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
- Elenco: os jogadores têm **contrato** (duração, renovação, cláusula)? Quantos jogadores no elenco (titulares + reservas)?
- Evolução: quanto pesa cada fator (treino, experiência, idade)? (o pico de idade e o declínio já estão decididos)
- Balancear salários, juros, limite de empréstimo, prazo de falência e manutenção da Gaming House (valores provisórios das decisões 0015 e 0016).
