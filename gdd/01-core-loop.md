# Core loop

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001

_O que o jogador faz minuto a minuto, sessão a sessão e ao longo do jogo._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Loop de segundos

Durante a partida, o jogador **assiste**: troca de câmera entre jogadores, vê a visão em primeira pessoa, acompanha a vida do time e o minimapa.

- **Primeira versão:** só assistir, sem interferir (decisão 0001).
- **Mais para frente:** intervenções de técnico, como pedir timeouts.
- **[Sugestão]** Outras intervenções futuras possíveis: mudar a estratégia do próximo round, definir a compra do próximo round.

## Loop de sessão

**[Sugestão]** Um ciclo por partida:

1. **Antes da partida:** ver adversário, escolher veto de mapas, definir estratégias e compras padrão.
2. **Partida:** assistir (ou simular rápido) e reagir nos intervalos.
3. **Depois da partida:** resultado, dinheiro, desempenho individual, moral.
4. **Gestão:** treinos, scouts, contratações, melhorias de instalações (decisão 0007), economia do time.

## Loop de longo prazo

**[Sugestão]** Temporadas e campeonatos: subir de divisão, ganhar prestígio, atrair patrocinadores e jogadores melhores, evoluir a estrutura do time.

## Fantasia do jogador

Ser o técnico/dono de um time de e-sports e **ver seu time jogar como profissional** na tela, como numa transmissão de campeonato.

## Perguntas em aberto

- Quais intervenções o manager terá na fase futura, além do timeout? Quantos timeouts por partida?
- Dá para pular/simular rápido uma partida sem assistir?
- Quanto dura uma partida assistida? (MR12? MR15? rounds mais curtos?)
