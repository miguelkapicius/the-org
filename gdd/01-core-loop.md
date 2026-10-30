# Core loop

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0008, 0010

_O que o jogador faz minuto a minuto, sessão a sessão e ao longo do jogo._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Loop de segundos

Durante a partida, o jogador **assiste**: troca de câmera entre jogadores, vê a visão em primeira pessoa, acompanha a vida do time e o minimapa.

- **Primeira versão:** só assistir, sem interferir (decisão 0001).
- **Mais para frente:** o manager poderá pedir **timeouts** (**3 por partida**). Depois de um timeout, o time tem uma **chance** de melhorar e de mudar a forma de jogar. Não é garantido.
  - **A chance depende de:** atributos dos jogadores, nível da Gaming House e momento da partida.
  - **O que pode mudar:** o estilo do time, por exemplo ficar **mais agressivo** ou **mais defensivo**. **O manager escolhe** o estilo durante o timeout.
  - **Peso de cada fator:** a definir no balanceamento. É a **única** intervenção durante a partida; fora isso, ele só assiste.

### Pular partida (decisão 0008)

O manager pode **pular** uma partida sem assistir. Nesse caso, o resultado é **calculado por probabilidade**, com base nos mesmos fatores que influenciam a partida assistida.

## Loop de sessão

**[Sugestão]** Um ciclo por partida:

1. **Antes da partida:** ver adversário, escolher veto de mapas, definir estratégias e compras padrão.
2. **Partida:** assistir (ou simular rápido) e reagir nos intervalos.
3. **Depois da partida:** resultado, dinheiro, desempenho individual, moral.
4. **Gestão:** treinos, scouts, contratações, melhorias da Gaming House (decisão 0007), economia do time.

## Loop de longo prazo

Carreira com **ligas, campeonatos e temporadas inspirados no circuito profissional de CS** (decisão 0010), com nomes próprios (decisão 0004).

**[Sugestão]** Subir de divisão, ganhar prestígio, atrair patrocinadores e jogadores melhores, evoluir a Gaming House.

## Fantasia do jogador

Ser o técnico/dono de um time de e-sports e **ver seu time jogar como profissional** na tela, como numa transmissão de campeonato.

## Perguntas em aberto

- Balanceamento: quanto pesa cada fator (atributos, Gaming House, momento) na chance de o timeout funcionar?
- Quais estilos o manager pode escolher além de agressivo e defensivo?
- Quanto dura uma partida assistida? (MR12? MR15? rounds mais curtos?)
- Carreira: formato dos campeonatos (MD1/MD3, grupos, mata-mata) e calendário.
