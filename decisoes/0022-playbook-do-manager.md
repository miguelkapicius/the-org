# 0022 — Playbook do manager: jogadas favoritas e proibidas

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/02-mecanicas.md, gdd/08-ui-ux.md, gdd/11-ideias.md
- **Substitui:** — (detalha a 0017)

## Contexto
Prioridade 5 de `gdd/11-ideias.md`. Pela decisão 0017 o IGL escolhe cada round um plano do banco de rounds pro, preferindo o estilo da estratégia. O manager não tem como dizer "faça mais rush pela Rampa" ou "nunca deixe o B sem ninguém". O banco tem 1.286 planos com nomes de demo, que o manager não entenderia.

## Decisão (palavras do autor)
Trabalhar na prioridade 5: o manager marca favoritos e proibidos e o IGL respeita.

## Detalhamento (proposta do agente, a validar; números provisórios)
1. **Jogadas:** os planos do banco são agrupados em jogadas com nome legível, calculadas dos dados:
   - **Ataque:** tipo (Rush, Execução, Default), bomb e por onde a maioria entra (ex.: "Rush A pela Rampa", "Default B pelo Casarão"); quando duas rotas têm 2+ jogadores, vira **Split** (ex.: "Split A (Palácio + Rampa)"); quando ninguém entra no bomb, "(sem entrar)".
   - **Defesa:** quantos jogadores ficam no A, no meio/base e no B aos 20 s (ex.: "Setup 2-1-2"), com "com avanço" quando alguém avança para o lado do TR antes dos 20 s.
   - A tela mostra para cada jogada: quantos planos, quanto o time pro venceu com ela, o estilo mais comum e a força medida.
   - **Desenho da jogada** (pedido do autor: "mostrar o mapa com mais ou menos o desenho da jogada" e os pontos fortes e fracos): clicar na jogada abre a planta do mapa com a rota de cada um dos 5 jogadores de um plano típico (o que mais se parece com os outros do grupo), da base até o plant (ataque) ou nos primeiros 30 s (defesa), pelo chão; bolinha onde para, X onde morre; a rota de cada um em palavras ("base TR → entrada do topo → casarão → bomb B"); "Outro exemplo" troca o plano.
   - **Pontos fortes e fracos** tirados dos dados: hora de entrada, split, sem entrar, avanço, bomb empilhado ou vazio, quanto o time pro venceu, força na nossa simulação e mortes cedo.
2. **Playbook por lado** (ataque e defesa): jogadas **favoritas** pesam 4 vezes mais na escolha do IGL; jogadas **proibidas** não são escolhidas. Se proibir tudo o que cabe numa compra (pistol, eco...), o IGL ignora as proibições naquele round.
3. **Vagas pela Gaming House:** 1 favorita e 1 proibida por lado no nível 1, +1 de cada por nível, até 5 (decisão 0016).
4. **Por mapa:** o playbook vale para o mapa jogável. Hoje só existe um (a Vila); quando houver mais, cada mapa terá o seu.
5. O playbook combina com a estratégia (decisão 0017): os pesos se multiplicam.
6. **Medido** (16 partidas com e sem playbook): a favorita do ataque passou de 8,2 % para 22,9 % dos rounds, a da defesa de 9,3 % para 25,0 %, e a proibida de 12,1 % para 0 %.

## Por quê
Profundidade para quem gosta de "pensar o jogo", com efeito visível na partida (pilar 3), e uma razão a mais para subir a Gaming House.

## Consequências
- Responde a pergunta "o manager escolhe planos por mapa e por lado? Quantos favoritos e proibidos?".
- O salvamento guarda o playbook.
- Cada mapa novo precisa das próprias regras de rota para nomear as jogadas (entra no checklist de mapa novo em `gdd/10-interface-vs-jogo.md`).
