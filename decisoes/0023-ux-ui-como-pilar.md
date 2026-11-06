# 0023 — UX e UI como pilar: complexo por dentro, simples de entender

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/00-visao.md, gdd/08-ui-ux.md
- **Substitui:** — (complementa a 0011)

## Contexto
O jogo acumula sistemas profundos (IA dos bots, treino e cansaço, moral, entrosamento, veto, playbook, finanças, scouts). A interface do protótipo cresceu tela a tela e ficou densa e "artificial" para os autores: o painel empilhava seis seções, a mesma informação aparecia em três lugares e textos de jogador citavam números de decisões do GDD. O autor pediu que a UX e a UI sejam um pilar do projeto: "extremamente complexo no funcionamento e extremamente simples de aprender e entender".

## Opções consideradas
1. **Interface como camada de apresentação** (como até agora) — cada sistema novo ganha a sua tela e os seus números; rápido de fazer. Contras: a profundidade vira planilha e afasta quem não é fã de manager; contradiz o "não é um manager de planilha" da visão.
2. **UX e UI como pilar** — todo sistema precisa caber numa leitura simples antes de entrar no jogo; a profundidade fica por baixo (dicas, detalhes sob demanda). Contras: mais trabalho de design em cada feature; às vezes vai esconder números que um jogador experiente queria ver de cara.

## Decisão
UX e UI viram o **pilar 5** da visão: **o funcionamento pode ser tão complexo quanto quisermos, mas o que o jogador vê tem de ser simples de aprender e de entender**. A complexidade mora na simulação; a interface mostra a conclusão (o que fazer, o que muda) e deixa o detalhe a um passo (dica, tela própria).

## Por quê
Liga os outros pilares: a IA dos bots (pilar 1) e a gestão que aparece na partida (pilar 3) só valem se o jogador entender o que está decidindo. Assistir é o prêmio (pilar 2), e a interface de gestão não pode ser o castigo entre uma partida e outra.

## Consequências
Regras de interface (detalhadas em `gdd/08-ui-ux.md`):
- **Uma informação, um lugar.** Se o hub mostra a série da semana, o aviso e o menu não repetem.
- **Uma ação principal por tela**, em destaque (índigo); o resto é secundário.
- **Conclusão antes do número:** "5 titulares cansados: reduza o treino" antes da tabela de cansaço. Números completos ficam na dica ou na tela própria.
- **Texto curto**, na língua do jogador: nada de termos internos (números de decisão, nomes de sistema do código).
- **Uma ideia nova por vez:** sistema novo entra com uma explicação curta na primeira vez e um aviso acionável quando importa.
- Toda feature nova responde no PR: "como o jogador entende isso em 5 segundos?".
