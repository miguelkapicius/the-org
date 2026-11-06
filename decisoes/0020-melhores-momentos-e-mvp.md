# 0020 — Jogada da partida e MVP no pós-partida

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md, gdd/11-ideias.md, gdd/03-progressao.md
- **Substitui:** —

## Contexto
Prioridade 3 de `gdd/11-ideias.md`. A transmissão já mostra, no fim de cada round, um replay de melhores momentos com uma nota para cada lance, mas nada disso sobra depois do round: o pós-partida só marca o "MELHOR" do mapa e o jogador não guarda histórico.

## Decisão (palavras do autor)
Trabalhar na prioridade 3: eleger a jogada da partida e o MVP e guardar no histórico do jogador.

## Detalhamento (proposta do agente, a validar)
1. **Jogada da partida:** o lance de maior nota da partida inteira (dos dois times), somando só os abates do jogador que mais matou no lance, com a mesma nota por abate do replay de melhores momentos (abate na cabeça, sem luneta, pela smoke ou parede, no ar, cego, AWP, clutch, abertura do round, multi-kill). A nota passa a ser calculada na simulação, então replay e jogada da partida usam a mesma conta. Só a partida **assistida** tem jogada (a pulada não é simulada, decisão 0008).
2. **MVP do mapa:** o jogador do clube com a maior nota de desempenho no mapa (o antigo "MELHOR"). Vale também na partida pulada, com os números estimados.
3. **MVP da série:** no fim da série, quem somou a maior nota nos mapas.
4. **Histórico do jogador:** cada jogador do clube guarda quantas vezes foi MVP de mapa e de série e as últimas 5 jogadas da partida que fez. Aparece no elenco e no painel.
5. **Notícia:** a jogada da partida do clube e o MVP da série entram nas notícias do clube (base para a prioridade 4, rivalidades e notícias com memória).
6. **Fica para depois:** rever a jogada da partida em 3D no pós-partida (exige guardar o trecho do replay entre rounds).

## Por quê
Pilar 2 (assistir é o prêmio): o que se vê na partida vira memória do clube. Dá motivo para assistir em vez de pular e cria ídolos no elenco.

## Consequências
- O salvamento guarda MVPs e jogadas de cada jogador e a jogada de cada resultado.
- O nome de exibição das armas passa para a simulação (`WeaponStats.Nome`), usado pelo HUD e pela jogada da partida.
