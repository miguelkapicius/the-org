# 0034 — Atributos dos jogadores, formato da partida e contratos

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/02-mecanicas.md
- **Substitui:** — (fecha perguntas que o protótipo já respondia na prática; complementa 0013, 0019, 0024 e 0026)

## Contexto
Três perguntas da lista de prioridade ficaram abertas enquanto o protótipo foi andando e já tem uma resposta de fato: quais atributos o jogador tem, quanto dura uma partida e se há contrato. Sem registro, o próximo agente pode mudar uma delas sem saber. Esta decisão escreve o que o protótipo faz e propõe o que falta (contrato), para o autor confirmar.

## Opções consideradas
**Atributos**
1. **Os 5 de hoje** (mira, reflexo, spray, utilitária, sangue-frio), leitura direta do que a IA dos bots usa. Prós: já funciona e o manager vê o que muda na partida (pilar 3). Contras: não há posicionamento nem comunicação, que a seção de mecânicas citava.
2. **Mais atributos** (posicionamento, leitura de jogo, comunicação). Prós: mais profundidade. Contras: cada um exige comportamento novo na IA; sem isso seria número sem efeito, contra o pilar 3.

**Formato da partida**
1. **MR12 com prorrogação** (quem faz 13 vence; 12×12 vai para prorrogação), como no CS2 e como a simulação já roda. Prós: ritmo atual, familiar ao público. Contras: partidas longas para assistir.
2. **MR15 ou rounds mais curtos.** Prós/contras: mais longo ou fora do ritmo do público; a IA e a economia foram afinadas em MR12.

**Contratos (histórico, substituído pela 0035)**
1. **Sem contrato** (como hoje: só o salário acordado). Prós: simples. Contras: o jogador nunca sai nem renova, a gestão do elenco não tem tensão ao longo das temporadas.
2. **Contrato por temporadas, sem cláusula.** Prós: dá motivo para renovar e negociar de novo (a negociação da 0026 já existe); pouco texto na interface. Contras: um sistema a mais.
3. **Contrato com cláusula de rescisão e disputa de outros clubes.** Prós: realismo. Contras: muito peso para o primeiro lançamento (pilar 5).

## Decisão
- **Atributos:** opção 1. Os 5 atributos ficam; só entra um novo quando a IA tiver um comportamento que ele muda.
- **Formato:** opção 1, MR12 com prorrogação.
- **Contratos:** substituído pela decisão **0035** (contratos realistas, com multa, cláusula e renovação). O que está abaixo em "Contratos" ficou só como histórico.

## Detalhamento (proposta do agente, a validar)
**Contratos (histórico, substituído pela 0035)**
1. Ao contratar, o contrato dura **1 a 3 temporadas** (o jogador pede; o manager pode oferecer mais ou menos como parte da proposta da 0026).
2. O contrato **vence no fim da temporada**. Um aviso no painel aparece com algumas semanas de antecedência ("2 titulares terminam o contrato").
3. Na **renovação** abre-se a mesma negociação da 0026, com o salário novo; se a conversa falha, o jogador vai para o mercado.
4. **Sem cláusula de rescisão** nesta versão. Vender continua possível a qualquer hora.
5. **Concorrência:** outros clubes não disputam a mesma contratação em tempo real. Um jogador do mercado pode ser **contratado por um rival entre uma semana e outra** (sorteio pela força do rival), e a interface avisa quando o jogador some.
6. O **tamanho do elenco** continua vindo do nível da Gaming House (5 titulares mais as reservas que o nível permite).

## Por quê
- Pilar 3: o atributo só existe se a IA o usa; o contrato só existe se muda o que o manager decide.
- Pilar 5: o contrato entra como um prazo e um aviso, sem cláusulas e sem tela nova de jurídico.
- Pilar 4: ritmo próximo do CS2 sem copiar mapa nem arma.

## Consequências
- `perguntas-abertas.md` perde as perguntas "quais atributos", "quanto dura uma partida" e a de contrato/concorrência; ganha "pesos de contrato" no balanceamento.
- Implementar o contrato exige: campo de validade no jogador, aviso no painel, renovação reaproveitando a negociação, e o sorteio de contratação por rivais no avanço de semana. Nada disso foi feito ainda.
- Se um dia se quiser MR15 ou outros atributos, entra uma decisão nova que substitui esta.
