# Visão

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0002, 0003, 0004, 0007

_A base de tudo. Toda outra seção deve respeitar os pilares definidos aqui._

> Convenção desta seção: texto normal = ideia dos autores. **[Sugestão]** = proposta do agente, ainda não validada.

## Pitch (uma frase)

Um jogo de **manager de um time de tático-FPS competitivo**, em que você monta e gerencia sua equipe e **assiste às partidas como uma transmissão ao vivo**, com bots que jogam como profissionais de verdade.

## Gênero e referências

- **Gênero:** manager esportivo (e-sports) + simulação de partida de FPS tático 5v5.
- **Inspiração de jogabilidade:** Counter-Strike (estilo de partida, ritmo, economia de rounds). O jogo é **inspirado**, não uma réplica (ver decisão 0004).
- **Inspiração de formato:** transmissões competitivas de CS e demos/GOTV: câmera seguindo jogadores, visão em primeira pessoa, vida do time, minimapa.
- **[Sugestão] Referências de manager:** Football Manager (profundidade de gestão e partida assistida), Motorsport Manager (assistir e reagir).

## Público-alvo

- **[Sugestão]** Fãs de CS e de e-sports que acompanham campeonatos e gostam de "pensar o jogo" mais do que mirar.
- **[Sugestão]** Jogadores de manager esportivo que querem um tema novo.

## Plataformas

_A definir (ver perguntas em aberto)._

## Pilares de design (3 a 5)

1. **Bots que jogam como profissionais.** A feature principal. Os bots seguram ângulos, abrem pixel, usam utilitária, rotacionam e erram como humanos erram, não como bots de jogo. (decisão 0003)
2. **Assistir é o prêmio.** A partida é apresentada como uma transmissão competitiva em 3D, com cara de demo: câmera, primeira pessoa, vida dos jogadores, minimapa. (decisões 0001 e 0002)
3. **Gestão que aparece na partida.** Economia, melhorias e escolhas do manager precisam ter efeito visível no que acontece no servidor. **[Sugestão]** Se uma decisão do manager não muda nada que dá para ver na partida, ela é suspeita.
4. **Identidade própria.** Parece CS no ritmo e na sensação, mas mapas, armas, nomes e visual são nossos. (decisão 0004)

## O que o jogo NÃO é

- Não é um FPS jogável: o jogador **não controla** os bots com mouse e teclado durante a partida. (decisão 0001). No começo o manager só assiste; mais para frente poderá pedir timeouts, que será a única intervenção durante a partida.
- Não é uma cópia do CS: não usamos mapas, armas, marcas ou assets da Valve.
- **[Sugestão]** Não é um manager de planilha: a partida em 3D é o centro, não um resumo em texto.

## Perguntas em aberto

- Plataforma: só PC? Steam?
- Single-player apenas, ou terá algum modo online (ligas entre amigos, ranking)?
- Nome do jogo e do "esporte" fictício.
- Modelo de negócio: jogo pago, free-to-play, early access?
- Tamanho do escopo do primeiro lançamento (MVP).
