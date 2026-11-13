# Jogadores estilo FM — desenho (2026-11-07)

> Atualizado: a Pistola saiu dos atributos. A pistola usa a Mira, e quem é bom de pistola tem a característica "Bom de pistola". A AWP já tem atributo próprio ligado na mira com ela. Times, staff e escalação estão em `Sim/Clubes.cs`.

**Código:**

- `Assets/Game/Sim/Jogadores.cs`: ficha, atributos, funções, características e momento.
- `Assets/Game/Sim/BaseJogadores.cs`: gerador da base, evolução e treino.
- Na `Partida.cs` mudaram 3 pontos pequenos.

**Testes:** `Headless/Fichas.cs`, com `-base N -seed S` e `-fichas CASO`.

Sem ficha, a partida é a mesma de antes. Com a ficha padrão (tudo 15) também dá o mesmo jogo, e o teste `eq` confere isso round a round.

## 1. Atributos (1 a 20, como no FM)

O número **15** é o jogador padrão da sim de hoje, um pro médio de tier 1. Uma ficha com tudo em 15 dá exatamente o bot atual.

| Grupo | Atributos | Hoje mexe em |
|---|---|---|
| Técnicos | Mira, Reflexo, Flick, Controle de spray, Mira posicionada, Movimentação, Utilitária, AWP | erro da mira (pistola também usa a Mira), erro com AWP, tempo de reação, velocidade de virar, recuo, mira na altura da cabeça, acerto da granada |
| Mentais | Agressividade, Calma, Decisões, Leitura de jogo, Posicionamento, Trabalho em equipe, Comunicação, Liderança, Concentração, Determinação | agressão, frieza no clutch e sob pressão |
| Físico | Resistência | (séries longas, depois) |
| Ocultos | Consistência, Jogos grandes, Profissionalismo, Ambição, Temperamento, Adaptabilidade | o dia do jogador na partida, evolução, treino, quanto o momento ruim afunda |

**Como o número vira jogo:** o atributo não cria regra nova. Ele muda os parâmetros que o cérebro já usa. A conta é d = (atributo − 15)/5:

- **Mira:** 20 dá −18% de erro de mira, 10 dá +18%.
- **Reflexo:** 20 reage em 0,167 s, 10 em 0,213 s.
- **Agressividade:** é jeito, não qualidade. 15 = 0,5, 20 = 1 e 1 = 0.

**Ainda sem ligação no cérebro:** Decisões, Leitura de jogo, Posicionamento, Movimentação, Comunicação, Liderança, Trabalho em equipe, Concentração e Resistência.

- Eles já contam na nota da função, na evolução e no treino.
- Cada ligação nova no Brain passa pelo A/B, como sempre. Por exemplo: AWP mexendo na mira com a luneta, Liderança no IGL, Leitura de jogo na crença.

## 2. Funções e familiaridade

As funções são as 7 do jogo: Entry, Suporte, Lurker, AWP, IGL, Âncora e Rotação.

**Nota na função** (as "estrelas" do FM): é a média ponderada dos atributos que a função usa, menos o que falta de familiaridade. É isso que faz um jogador ser bom de IGL e ruim de lurker.

| Função | O que mais pesa |
|---|---|
| IGL | Liderança 4, Leitura de jogo 3, Decisões 3, Comunicação 3 |
| Lurker | Leitura de jogo 3, Posicionamento 3, Decisões 2, Calma 2, Concentração 2 |
| Entry | Reflexo 3, Movimentação 3, Flick 2, Mira 2, Determinação 2 |
| AWP | AWP 4, Reflexo 2, Calma 2, Posicionamento 2 |
| Suporte | Utilitária 3, Trabalho em equipe 3, Comunicação 2, Posicionamento 2 |
| Âncora | Posicionamento 3, Calma 3, Utilitária 2, Concentração 2 |
| Rotação | Leitura de jogo 3, Movimentação 2, Decisões 2, Comunicação 2 |

**Familiaridade (1 a 20) por função:**

- **Fora da função:** o jogador erra o que é da função, ou seja, ângulos, granadas, decisões, leitura e posicionamento. Perde até ~4 pontos nesses atributos com familiaridade 1.
- **Jogando na função:** a familiaridade sobe, e sobem os atributos que a função usa, até o teto oculto. Sobe mais rápido em quem é novo e profissional.
- **Função que não joga:** ele esquece devagar.
- **Idade:** a mecânica (reflexo, flick, movimentação e mira) cai depois dos ~28.

## 3. Características (as "preferências de jogo" do FM)

São 19, aprendidas no treino. Só "Bom de pistola" já mexe na partida (erra 12% menos de pistola); as outras ficam guardadas:

- Busca o 1º abate
- Tenta o clutch × Salva a arma
- Lurka sozinho × Joga colado no time
- AWP agressiva
- Segura ângulo fechado × Joga de longe
- Deagle no eco
- Bom de pistola
- Granada antes de entrar
- Jogada de risco
- Âncora teimosa × Rotaciona cedo
- Dá drop
- Pre-fire nos cantos
- Spray na smoke
- Segue a call × Improvisa

As marcadas com × são opostas: aprender uma tira a outra. Cada função tem as suas típicas, e o gerador usa isso.

Por enquanto as características ficam guardadas. Cada uma vira peso de decisão no cérebro com A/B. Por exemplo, "Tenta o clutch" aumenta a vontade de jogar o 1vX em vez de salvar. É peso, nunca regra.

## 4. Momento: do time e individual, limitado a ±10%

- **Do time:** o clima do elenco na carreira (humor e química, de −1 a 1) e o embalo na partida (os últimos 4 rounds, o mais recente pesando mais).
- **Individual:** moral e forma na carreira, mais a confiança na partida (o saldo de abates e mortes).
- **Soma:** time e individual entram meio a meio. O total vai **no máximo a ±2 pontos de atributo (±10% da escala)**: ajuda, não decide.
- **Moderadores:** quem tem temperamento ruim afunda mais no momento ruim, e quem é determinado segura.
- **O dia (consistência):** é sorteado uma vez por partida com um sorteio próprio do jogador, então não mexe no sorteio da partida. Consistência 20 = sempre igual; 1 = varia ~±1,5 ponto.
- **Desligar:** `-exp momentoDesligado` deixa só o número da ficha.

## 5. Base fake e evolução

- **Tamanho:** `BaseJogadores.Gerar(semente, 5000)`. A mesma semente dá a mesma base, então o jogo recria sem precisar de arquivo.
- **Divisão:** tier 1 (elite, 4%), tier 2 (12%), tier 3 (34%) e tier 4 (academia/semi, 50%).
- **Países:** 28, com pesos aproximados do cenário. Nomes e sobrenomes genéricos de cada país.
- **Nicks:** inventados por sílaba, com uma lista de nicks reais famosos pra nunca sair igual. Nunca nome real.
- **Cada jogador tem:** função natural de TR e de CT, idade, atributos pelo perfil da função, teto oculto (o jovem tem espaço; o "wonderkid" da academia pode chegar na elite), familiaridade e 1 a 3 características.
- **Evolução:** `Evolucao.AposPartida(ficha, rounds por função, rating, venceu)`.
- **Treino:** `Treino.Atributo` e `Treino.Caracteristica`. Uma característica leva ~15 a 25 semanas.
- **Conferência:** CSV de toda a base em `Logs/jogadores/base_77.csv`, resumo em `base_77_resumo.txt`.

## 6. Próximos passos

1. Ligar os atributos que faltam no cérebro, um por vez com A/B:
      - Movimentação: peek e contra-strafe.
   - Decisões e Leitura de jogo: a crença.
   - Liderança e Comunicação: o IGL e as calls.
   - Concentração: o fim de partida.
2. Características como pesos de decisão, com A/B.
3. Momento: depois ligar o "cortar o momentum" na pausa do coach (o timeout redesenhado).
4. Interface, junto com a Visual e o Miguel:
   - ficha do jogador (atributos, estrelas por função, características, momento);
   - lista da base com filtro;
   - tela de treino.
