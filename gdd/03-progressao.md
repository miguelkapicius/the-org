# Progressão e economia

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0007, 0010

_Como o jogador evolui e quais recursos circulam._

> **[Sugestão]** = proposta do agente, ainda não validada.

Existem **duas economias** diferentes:

1. **Economia do round** (dentro da partida): dinheiro para comprar armas e utilitárias.
2. **Economia do clube** (manager): dinheiro do time para salários, contratações e melhorias da Gaming House.

## Recursos

- Dinheiro do round.
- Dinheiro do clube.
- **[Sugestão]** Reputação/prestígio do time, moral dos jogadores, fãs.

## Fontes e sumidouros

**[Sugestão] Clube:**
- Fontes: prêmios de campeonato, patrocínios, venda de jogadores.
- Sumidouros: salários, contratações, melhorias da Gaming House, viagens/bootcamps.

## Falência

Se o dinheiro do clube acabar, o time **pode falir** e o jogo acaba (**game over**).

## Evolução dos jogadores

A evolução dos jogadores é **multifatorial**: o jogo mistura vários dados, como **treino**, **experiência em partida** e **idade**.

## Curva de dificuldade

_A definir._

## Desbloqueios

### Gaming House (decisão 0007)

O time tem **uma** instalação: a **Gaming House**. O manager investe para subir o nível dela.

O nível da Gaming House **libera** outras coisas que o manager pode melhorar, como **treinos** e **scouts**. Sem subir a Gaming House, essas opções ficam bloqueadas.

- **Níveis:** quantidade a definir. A evolução deve ser **gradativa**: cada nível libera um pouco mais, sem saltos grandes.
- **Efeito na partida:** o nível da Gaming House aumenta a chance de um timeout funcionar (ver `01-core-loop.md`).
- **Custo:** precisa ser **balanceado**: nem tão fácil de subir, nem tão difícil.

## Perguntas em aberto

- Quantos níveis a Gaming House tem e o que cada nível libera?
- Qual a curva de custo por nível (para ficar balanceada)?
- A Gaming House tem custo de manutenção mensal?
- Elenco: como funcionam contratações, vendas, salários e contratos?
- Falência: o jogador recebe avisos antes? Existe empréstimo ou alguma saída antes do game over?
- Evolução: quanto pesa cada fator (treino, experiência, idade)? Existe pico de idade e declínio?
