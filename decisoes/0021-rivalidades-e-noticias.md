# 0021 — Rivalidades e notícias com memória

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/08-ui-ux.md, gdd/11-ideias.md
- **Substitui:** —

## Contexto
Prioridade 4 de `gdd/11-ideias.md`. O clube enfrenta os mesmos times várias vezes por temporada, mas cada série é esquecida: as notícias do clube só registram fatos soltos ("empréstimo recebido", "campeonato encerrado") e não há histórico de confrontos.

## Decisão (palavras do autor)
Trabalhar na prioridade 4: rivalidades e notícias que contam a história do clube.

## Detalhamento (proposta do agente, a validar; números provisórios)
1. **Histórico de confrontos** contra cada adversário: séries e mapas vencidos e perdidos, sequência atual e a última eliminação. Sai do histórico de partidas que já é salvo (nada de estado novo).
2. **Pontos de rivalidade** por série contra o mesmo time: +0,5 por série; +2 se for mata-mata (oitavas, quartas, semifinal ou decisão de grupo); +4 se for final; +3 se o clube foi eliminado por ele; +1 se a série foi até o último mapa; +2 se foi num Major.
3. **Rival:** os **3** adversários de mais pontos, desde que tenham **10 ou mais**. O de mais pontos é o **arquirrival**. (Medido: com 1 ponto por série e limite 8, ligas e grupos repetem tanto os adversários que 15 de 25 viravam rivais em 2 temporadas.) Responde a pergunta "o que transforma um adversário em rival".
4. **Clássico** (série contra um rival): a moral dos titulares muda o dobro (+6 na vitória, -6 na derrota por mapa) e vencer a série dá um bônus de uma semana de patrocínio. Como a moral entra na habilidade (decisão 0019), o clássico pesa na partida.
5. **Notícias com memória** (nas notícias do clube):
   - antes da série: clássico (com o retrospecto) ou revanche (quando aquele time eliminou o clube na última vez);
   - depois da série: sequência de 3 ou mais séries vencidas contra o mesmo time, fim de tabu (primeira vitória depois de 2 ou mais derrotas), novo rival;
   - jogadores: marcos de MVP (5, 10, 20 vezes) e revelação (jogador de até 21 anos MVP da série).
6. **Telas:** retrospecto e selo CLÁSSICO na preparação da série; lista de rivais no painel do clube.

## Por quê
Dá significado aos resultados no médio e longo prazo: uma derrota na semifinal vira motivo para a próxima série. Pilar 3: o clássico mexe na moral, que muda a habilidade dos bots.

## Consequências
- Rivalidades com os mais de 40 outros times só existem do ponto de vista do clube (os outros times não têm rivais entre si).
- Remove a pergunta aberta sobre o que faz um rival; os valores entram no balanceamento.
