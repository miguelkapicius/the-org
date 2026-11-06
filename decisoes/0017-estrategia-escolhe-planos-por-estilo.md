# 0017 — A estratégia escolhe os planos de round pelo estilo

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/02-mecanicas.md, gdd/10-interface-vs-jogo.md, gdd/05-conteudo.md
- **Substitui:** — (detalha a 0014)

## Contexto
A decisão 0014 diz que a estratégia escolhida na pré-partida muda o jeito de o time jogar. No protótipo, ela só mexia em pesos da IA, mas o plano de cada round vem de um banco de rounds pro, e a escolha do plano ignorava a estratégia. A diferença entre os estilos era pequena (plant médio entre 56 s e 64 s em todos).

## Opções consideradas
1. **Só ajustar pesos** (como estava) — simples; efeito pequeno e pouco visível.
2. **Classificar os planos do banco nos 5 estilos e preferir os do estilo escolhido** — usa dados de rounds reais, efeito visível; depende de o banco ter planos de todos os estilos em cada mapa.
3. **Escrever planos à mão por estilo** — controle total; muito trabalho por mapa.

## Decisão (palavras do autor)
Classificar as táticas do banco, com base nos dados, nos cinco estilos; dependendo do que o manager escolhe, o jogo escolhe **preferencialmente** os planos daquele estilo.

## Detalhamento (proposta do agente, a validar)
- A classificação é automática, pelos dados de cada round (hora de entrar no bomb, mortes cedo, tempo no meio e nas áreas disputadas, avanço do CT, rotações). As regras estão em `gdd/02-mecanicas.md`.
- Na defesa, "Execuções rápidas" vira **rotação rápida** entre os bombs.
- Preferência: peso 6 para os planos do estilo (os outros continuam possíveis). Número provisório.
- Os pesos de cada preset continuam valendo junto.

## Por quê
Pilar 3: a escolha do manager tem de aparecer na partida. Medido, a parte dos rounds com plano do estilo passou de 10–38 % para 47–78 %, e o plant médio passou a ir de 54 s a 69 s conforme o estilo.

## Consequências
- Cada mapa novo precisa de um banco de planos com **todos os estilos** representados (entra no checklist de mapa novo em `gdd/10-interface-vs-jogo.md`).
- As áreas usadas na classificação são da Vila; outro mapa precisa da própria lista.
- O ciclo de contra entre estilos (bônus fixo de 2 %) continua sem medição.
