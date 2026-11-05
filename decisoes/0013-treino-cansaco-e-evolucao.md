# 0013 — Treino configurável, cansaço e evolução dos jogadores

- **Status:** proposta
- **Data:** 2026-11-04
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/02-mecanicas.md, gdd/03-progressao.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O GDD diz que a evolução dos jogadores é multifatorial (treino, experiência, idade), mas não define como. Os atributos hoje nunca mudam.

## Decisão (palavras do autor)
- As **estatísticas dos jogadores mudam** com o tempo, dependendo de **idade**, **treino** etc.
- O jogador (manager) **configura o treino** e a **frequência** dele.
- O treino **aumenta o cansaço** dos jogadores e também **dá um bônus nas partidas**: é um **contrapeso**.

## Detalhamento (proposta do agente, a validar)
1. **Plano de treino por semana:** para cada jogador, um **foco** (mira, reflexo, spray, utilitária, sangue-frio ou descanso) e a **frequência** (0 a 5 sessões por semana, limitada pelo nível da Gaming House). Há também **sessões coletivas** (entrosamento) e o **estudo** de um mapa ou do próximo adversário.
2. **Dois efeitos do treino:**
   - **Evolução (permanente):** o atributo em foco sobe um pouco por sessão, com retorno decrescente perto do teto (**potencial** do jogador) e variando com a idade.
   - **Preparo (temporário):** dá um bônus de habilidade nas partidas e decai se o treino parar.
3. **Cansaço:** cada sessão e cada partida acumulam cansaço; descansar recupera. Acima de um limite o cansaço **reduz** a habilidade na partida (e pode apagar o bônus do preparo). O manager equilibra: treinar pouco não evolui, treinar demais cansa.
4. **Idade:** jogadores jovens evoluem mais rápido e ainda têm potencial a descobrir; o auge fica entre cerca de 21 e 26 anos; depois há declínio gradual (reflexo cai antes). A idade sobe a cada temporada.
5. **Efeito visível:** o bônus e a penalidade entram na habilidade que a **simulação** usa, então aparecem no jogo dos bots (pilar 3).

## Por quê
Cria um sistema de gestão no meio da temporada (o que fazer nas semanas sem jogo) e dá consequência a quem exagera.

## Consequências
- Números provisórios (ganho por sessão, recuperação, limiar de cansaço).
- Atributos passam a ser guardados no salvamento.
- Responde "existe idade de pico e declínio?" (sim) e dá o primeiro peso para treino, experiência e idade; os pesos finais ficam para o balanceamento.
