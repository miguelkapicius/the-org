# 0012 — Calendário e campeonatos de todos os formatos

- **Status:** proposta
- **Data:** 2026-11-04
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/03-progressao.md, gdd/08-ui-ux.md
- **Substitui:** — (detalha a 0010)

## Contexto
A decisão 0010 diz que a carreira é inspirada no circuito profissional de CS, mas deixou em aberto o formato dos campeonatos e o calendário. Até aqui o protótipo só tinha "partida 1, 2, 3...".

## Decisão (palavras do autor)
- Haverá campeonatos de **todos os tipos**, com séries **MD1 e MD3** (e final em MD5), **fase de grupos** e **mata-mata**.
- A **frequência** deve ser razoável, **parecida com a vida real do CS2**.

## Detalhamento (proposta do agente, a validar)
1. **Tempo em semanas.** A temporada tem 52 semanas. Entre um compromisso e outro o manager avança semanas (e é aí que o treino acontece, decisão 0013).
2. **Formatos** (todos com nomes próprios, decisão 0004):
   - **Liga** (pontos corridos, uma partida por semana);
   - **Grupos + mata-mata** (grupos de 4 em pontos corridos, depois chave eliminatória);
   - **Grupos de dupla eliminação** (estilo GSL: 5 séries por grupo de 4) + mata-mata;
   - **Suíço** (estilo Major: 3 vitórias classificam, 3 derrotas eliminam) + mata-mata;
   - **Mata-mata simples** (copas e qualificatórias abertas).
3. **Séries:** rodadas iniciais em **MD1**, decisivas (avanço, eliminação, mata-mata) em **MD3**, final em **MD5**. Cada mapa de uma série é uma partida completa (assistida ou pulada).
4. **Frequência (referência CS2):** um time de elite joga cerca de um evento a cada 2 a 3 semanas, com pausas (festas e verão) em que só treina; são **2 Majors por temporada**. Os eventos são agrupados em **circuitos por nível**.
5. **Circuitos e ranking:** três circuitos (**Amador**, **Pro**, **Elite**). Um **ranking** de 48 times, com pontos por colocação em cada evento e janela móvel de 52 semanas, decide em que circuito o clube joga; **sobe e desce no fim da temporada**. Os Majors reúnem os 16 melhores do ranking.
6. **Premiação** por colocação final no evento (substitui o prêmio fixo por partida do protótipo).
7. Times adversários são **fictícios e gerados** pelo jogo (responde parte da pergunta "times e jogadores fictícios gerados ou criados à mão?"): têm força e estilo, e suas partidas são resolvidas por probabilidade.

## Por quê
Dá estrutura ao loop de longo prazo (pilar 3: o que o manager decide aparece nos resultados) e liga calendário, treino e finanças.

## Consequências
- Todos os números (quantos eventos, premiações, pontos) são **provisórios**.
- A partida simulada continua sendo uma partida de um mapa; séries MD3/MD5 são várias partidas.
- Exige salvamento do estado dos torneios (decisão registrada em gdd/09-tecnico.md).
