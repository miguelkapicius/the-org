# Core loop

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0008, 0010, 0012, 0014, 0018, 0021

_O que o jogador faz minuto a minuto, sessão a sessão e ao longo do jogo._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Loop de segundos

Durante a partida, o jogador **assiste**: troca de câmera entre jogadores, vê a visão em primeira pessoa, acompanha a vida do time e o minimapa.

- **Primeira versão:** só assistir, sem interferir (decisão 0001).
- **Mais para frente:** o manager poderá pedir **timeouts** (**3 por partida**). Depois de um timeout, o time tem uma **chance** de melhorar e de mudar a forma de jogar. Não é garantido.
  - **A chance depende de:** atributos dos jogadores, nível da Gaming House e momento da partida.
  - **O que pode mudar:** o estilo do time, por exemplo ficar **mais agressivo** ou **mais defensivo**. **O manager escolhe** o estilo durante o timeout.
  - **Peso de cada fator:** a definir no balanceamento. É a **única** intervenção durante a partida; fora isso, ele só assiste.
  - **No protótipo (decisão 0018, proposta do agente):** os estilos são os mesmos 5 da estratégia; a transmissão pausa enquanto o manager escolhe e mostra a chance antes (base 50 %, sangue-frio dos titulares ±15 %, +5 % por nível da Gaming House, +4 % por round perdido em sequência até +12 %, -10 % se vence por 4 ou mais; entre 20 % e 90 %). Se funciona, o time joga no novo estilo a partir do próximo round e ganha foco (+10 % de sangue-frio por 3 rounds); se não, o timeout é gasto e nada muda. Tecla **T** ou botão "Timeout" na transmissão.

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

### Rivalidades e notícias (decisão 0021)

**[Sugestão]** O clube guarda o retrospecto contra cada adversário. Séries pesam pontos de rivalidade (mais em mata-mata, final, eliminação, série até o último mapa e Major); os 3 times de mais pontos (com 10 ou mais) são **rivais**, e o primeiro é o **arquirrival**. Série contra rival é **clássico**: a moral dos titulares muda o dobro e vencer rende um bônus de patrocínio. As notícias do clube lembram a história: clássico, revanche, sequência, fim de tabu, novo rival, marcos de MVP e revelações.

### Calendário e campeonatos (decisão 0012)

O tempo corre em **semanas** (52 por temporada). Há campeonatos de **todos os formatos** (liga, grupos + mata-mata, grupos de dupla eliminação, suíço, mata-mata simples), com séries **MD1** (rodadas iniciais), **MD3** (decisivas) e **MD5** (final), numa **frequência parecida com a do CS2**: um time de elite joga um campeonato a cada 2 a 3 semanas, com pausas em que só treina, e há **2 Majors por temporada**. Os campeonatos são agrupados em **circuitos** (Amador, Pro, Elite) e um **ranking** de 48 times decide em qual o clube joga; sobe e desce no fim da temporada. Os adversários são fictícios e gerados pelo jogo. Nas semanas sem jogo o manager cuida do treino (decisão 0013).

### Antes de cada partida de uma série (decisão 0014)

O manager pode fazer o **veto de mapas** (ou deixar o jogo vetar, ou pular a série) e escolher a **estratégia**. Com informação sobre o adversário (scouts, decisão 0016), vê como ele joga e escolhe o contra.

## Fantasia do jogador

Ser o técnico/dono de um time de e-sports e **ver seu time jogar como profissional** na tela, como numa transmissão de campeonato.

## Perguntas em aberto

- Balanceamento: quanto pesa cada fator (atributos, Gaming House, momento) na chance de o timeout funcionar?
- Quanto dura uma partida assistida? (MR12? MR15? rounds mais curtos?)
- Balancear o calendário: quantos campeonatos por temporada em cada circuito, premiações e pontos do ranking (valores provisórios da decisão 0012).
