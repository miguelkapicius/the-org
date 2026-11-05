# 0014 — Pré-partida: veto de mapas e estratégia

- **Status:** proposta
- **Data:** 2026-11-04
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/02-mecanicas.md, gdd/08-ui-ux.md, gdd/05-conteudo.md
- **Substitui:** —

## Contexto
A pré-partida só tinha botões desligados ("mapas e plano de jogo") porque o que o manager configura antes da partida era pergunta aberta.

## Decisão (palavras do autor)
- Dá para fazer o **veto de mapas** se o jogador quiser, **ou pular a partida normalmente**.
- Dá para **definir a estratégia** antes da partida.

## Detalhamento (proposta do agente, a validar)
1. **Veto:** segue o formato da série (MD1: só banimentos até sobrar um mapa; MD3: bane, bane, escolhe, escolhe, bane, bane, sobra o decisivo; MD5: bane, bane e escolhas alternadas). O manager pode vetar ele mesmo, deixar o **veto automático** (o jogo escolhe pela afinidade do time) ou **pular a série**.
2. **Pool de 7 mapas** com nomes próprios. Cada time tem **afinidade** (0 a 100) com cada mapa, que sobe estudando o mapa (treino) e jogando nele, e **entra na habilidade** dos jogadores na partida.
3. **Estratégia:** presets que ajustam os pesos da IA do time (Equilibrada, Agressiva, Defensiva, Execuções rápidas, Controle de mapa). Efeito visível na partida.
4. **Contra-estratégia:** com informação sobre o adversário (decisão 0016), o jogo mostra o estilo dele e o preset recomendado; escolher o contra certo dá um pequeno bônus.
5. **Limitação atual:** só existe a geometria de **um mapa** no 3D. Os outros mapas entram como nomes, afinidade e veto; a partida assistida roda na geometria disponível até haver mais mapas (pergunta aberta de conteúdo: quantos mapas no MVP).

## Por quê
Dá ao manager decisões antes da partida que mudam o que se vê nela (pilar 3), sem obrigar quem prefere só assistir ou pular.

## Consequências
- Números provisórios (bônus de afinidade e de contra-estratégia).
- Precisa de mais de um mapa jogável para o veto ter efeito completo.
