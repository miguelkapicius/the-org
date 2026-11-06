# 0019 — Jogadores com personalidade: traços, moral e entrosamento

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/02-mecanicas.md, gdd/03-progressao.md, gdd/08-ui-ux.md, gdd/11-ideias.md
- **Substitui:** —

## Contexto
Prioridade 2 de `gdd/11-ideias.md`. Hoje os jogadores só têm 5 atributos e uma função: dois jogadores com o mesmo geral são iguais. A "moral" é citada no loop de sessão mas não tem regra, e as sessões coletivas prometem "entrosamento" mas só dão preparo (item T2 de `gdd/10-interface-vs-jogo.md`).

## Decisão (palavras do autor)
Trabalhar na prioridade 2: traços de personalidade, moral e entrosamento que mudam a simulação.

## Detalhamento (proposta do agente, a validar; números provisórios)
1. **Traços:** cerca de metade dos jogadores tem **um** traço. Os do elenco são conhecidos; os do mercado só aparecem com scout (como o potencial). O adversário ainda não tem jogadores de verdade (item J3), então não tem traços.

   | Traço | Efeito na partida |
   |---|---|
   | Decisivo | Nos rounds decisivos (match point de qualquer time, último round do half, prorrogação): +15 % de sangue-frio |
   | Esquentado | No round seguinte a um em que morreu primeiro: mira 8 % pior e mais agressivo |
   | Líder | Escalado, dá +5 % de sangue-frio aos outros 4 titulares |
   | Estrela | Mira 6 % melhor sempre, mas troca menos os colegas (entrosamento dele -15) e a moral cai mais rápido no banco |
   | Pistoleiro | Nos rounds de pistol: mira e reflexo 12 % melhores |
   | Embalado | Quando o time venceu os 3 últimos rounds: reflexo 6 % melhor |

2. **Moral** (0 a 100; elenco começa em 65, contratado em 70): +3 por mapa vencido e -3 por mapa perdido (titulares); por semana, +1 para titular e -2 para reserva (-4 se Estrela). Vale na habilidade da partida: de -4,5 % (moral 0) a +3 % (moral 100), neutra em 60. Moral abaixo de 30 vira aviso no painel do clube.
3. **Entrosamento** (0 a 100 por jogador: o quanto ele está integrado ao time; elenco começa em 60, contratado em 20): por semana, +5 para titular, +1 para reserva e +3 por sessão coletiva para os titulares. O **entrosamento do time** é a média dos titulares e muda a simulação: a chance de um bot **trocar** o colega que morreu vai de ×0,6 (entrosamento 0) a ×1,4 (100) e a distância de onde ele vai trocar de 340 a 500, neutras em 50. Medido: as mortes trocadas em até 5 s vão de 21 % (entrosamento 10) a 25 % (90). Também entra na força (±2 %) para a partida pulada.

## Por quê
Pilar 3: cada novidade muda o que os bots fazem, não só um número. Cria histórias (o Decisivo que aparece no match point, o Esquentado que perde a cabeça) e decisões de elenco além do geral (trocar um titular custa entrosamento).

## Consequências
- Responde, como proposta, a pergunta "quais traços existem, quantos por jogador, e o manager os vê desde o início ou só com scout".
- Responde a parte "moral" do loop de sessão (`gdd/01-core-loop.md`).
- As sessões coletivas passam a dar entrosamento (item T2).
- Salvamento ganha traço, moral e entrosamento; carreiras salvas antes recebem os valores iniciais.
