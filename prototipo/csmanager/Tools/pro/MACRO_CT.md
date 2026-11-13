# Caixa-preta dos pros — macro do CT (Mirage) × nossos bots (Vila)

Feito em 2026-11-11/12 (PLANO_SIMULACAO.md §7.1). Fonte dos pros: `tactics_playbook.json`, 334 rounds de compra cheia dos
dois lados na Mirage, por região. Fonte dos bots: a partida do Gabriel (`Logs/partidas/2026-11-11_22-20-31`, exe 16 com
`CSManager_CT_NOVO.bat`) e 200 rounds do mesmo cérebro no Headless. As duas fontes passam pelo mesmo script, com os mesmos
postos (A · conector · meio · B · base · terreno TR).

Como refazer:

```
python -I Tools/pro/macro_ct.py pro                 > Logs/bots/macro_ct_pro.txt
python -I Tools/pro/macro_ct.py bot <pasta caixas>  > Logs/bots/macro_ct_bot.txt
python -I Tools/pro/crenca_x_verdade.py <pasta>     # quanto o CT acredita do que está lá de verdade
python -I Tools/pro/retake_lento.py <pasta>         # o que o CT faz enquanto junta pro retake
python -I Tools/pro/resumo_cx.py <pasta> [...]      # plant, vitória, ordens: só pra ver se algo quebrou
```

Cada situação sai com 20 rounds em linha do tempo (posto e hora de cada CT) pra ler no olho. Os números abaixo servem
pra achar onde olhar; o limite deles: a região do playbook é larga (o "CTSpawn" da Mirage pega atrás do A), então
posto a posto a comparação é aproximada.

## O que os pros fazem, situação por situação

1. **Setup.** Não existe um setup. A divisão mais comum (2 no lado A, 2 no B, 0 no meio aos 20 s) aparece em 11% dos
   rounds; as oito mais comuns juntas não chegam a 60%. Quase sempre há alguém fora de bomb aos 20 s (base, conector,
   avançado no terreno do TR).
2. **Antes do contato o CT se mexe.** Entre os 20 s e a primeira baixa, em 94% dos rounds pelo menos um CT troca de posto;
   o normal é 2 ou 3 trocarem. Os movimentos mais comuns: B → meio, A → base, avançado → volta pro A, A ↔ conector.
   É o jogador de rotação flutuando, a agressão cedo que volta, o âncora mudando de ângulo.
3. **Caiu o primeiro CT.** Quem estava no mesmo posto do morto fica (75%). Do resto, em 9 de 10 rounds pelo menos um muda
   de posto em 10 s: o do outro bomb desce pra base/meio (fica a meio caminho), raramente alguém entra direto no posto
   do morto.
4. **CT em vantagem (5x4).** Quase sempre (97%) alguém se reposiciona nos 15 s seguintes; em 1 de 4 rounds um CT avança
   no terreno do TR. Ninguém fica estátua, mas a maioria não sai pra caçar.
5. **O TR junta 3+ num lado.** O bomb atacado tem 1 ou 2 CT quando o TR pisa nele; oito segundos depois o outro lado tem
   0 ou 1 CT. Ou seja: o lado atacado segura com pouca gente e o resto do time já veio.
6. **Plantou.** Em 94% dos plants não há CT vivo dentro do bomb. Em dois terços dos casos ninguém de fora entra (guardam
   ou morrem no caminho): com menos gente o pro quase não retoma (2x3: 7% · 3x4: 0% · 1 contra 2+: 0%), em igualdade
   pouco (2x2: 18% · 3x3: 22%), com vantagem sim (3x2: 67% · 2x1: 71%). Quando retomam, o 1º e o 2º entram com até 3 s
   de diferença em 61% dos casos, e o primeiro de fora pisa no bomb 12 s depois do plant (mediana).

## O que os nossos bots faziam (exe 16, CT novo)

| Situação | Pro | Bot (exe 16) |
|---|---|---|
| Setup aos 20 s | dezenas de divisões | 97% dos rounds o mesmo 2-1-2 |
| Trocam de posto antes do contato | 94% dos rounds (2–3 jogadores) | 25% dos rounds |
| Depois da 1ª baixa, alguém muda de posto | 89% | 43% |
| Em vantagem 5x4, alguém se reposiciona | 97% | 52% |
| 8 s depois de o TR entrar num lado, gente no outro | 0–1 | 2 em um quarto dos casos |
| Primeiro de fora no bomb depois do plant | 12 s | 25 s |
| Em desvantagem depois do plant | guardam | cada um decidia sozinho (sorteio): um ia, outro guardava |

Na partida do Gabriel, round 1697974463 (o exemplo mais claro): quatro TR entram no A; o CT acreditava em **1** inimigo
lá. Um CT do B larga o giro pra buscar uma AK no casarão (10 s, tomou molotov), outro para pra jogar molotov no tapete
e smoke na sacada antes de girar. Plantou. O time junta na praça andando de shift na própria base (16 u/s), um vai
sozinho e morre contra quatro, a postura do time diz "guardar arma" e dois dizem "3 contra 5, mas vou tentar". Morrem
um por vez.

## Causas achadas e o que mudou (flags, tudo no atalho de teste)

**Crença (`crenca2Ligado`).** O CT acreditava em 8% dos TR que estavam de verdade no lado do bomb.
- O som ia pro rastro "mais plausível": quatro correndo juntos viravam um; quando o dono do som morria ficava fantasma.
  Agora o som é do rastro de quem fez o som.
- O quadro do time contava só inimigo **visto**. Agora conta visto, ouvido e cantado, por zona.
- Call atrasada apagava som mais novo; a célula em volta do jogador era "limpa" através da parede; olhar um pedaço da
  célula apagava o contato colado na quina; o rastro escorria mais devagar que quem corre. Corrigidos.
- Depois do plant o rastro sem notícia se espalhava pelo mapa todo: o CT achava que podia ter TR na própria base e
  andava de shift. Agora quem não deu notícia depois do plant está perto do C4; quem for visto/ouvido longe (lurker)
  fica livre.
- Resultado medido: acredita em ~30% (era 8%), sem fantasma (0,1–0,4% dos instantes).
- Não feito: noção de pra onde o adversário costuma ir antes do primeiro contato (o calor continua plano até lá).

**Macro (`macro2Ligado`).** Girando, recuando ou juntando pro retake, o jogador não desvia pra buscar arma nem pra jogar
granada no lugar que está largando; arma do chão só existe pra quem a viu; juntar pro retake é correndo.

**Retake (`retake2Ligado`).** Guardar ou retomar é uma decisão do time, pela chance do placar de vivos (a régua acima);
se o placar melhora, quem guardava volta.

## Cenas que saem daqui (resultado esperado em frase)

- Três ou mais TR ouvidos/vistos entrando num lado: o time diz "é A/B" e quem não é âncora do outro lado chega em até 8 s.
- Girando com o bomb caindo: ninguém para pra pegar arma ou jogar granada no lugar que largou.
- Plantou com 2 contra 4: o time inteiro guarda. Plantou com 3 contra 2: o time inteiro vai, e o 1º e o 2º entram com
  até 3 s de diferença.
- Juntando pro retake: corre até o ponto de juntar; shift só na entrada.
- Round sem contato até os 40 s: pelo menos um CT trocou de posto (ainda NÃO feito: o CT continua estátua antes do contato).

## Falta (macro do CT)

- Vida antes do contato: flutuar o jogador de rotação, agressão cedo que volta, troca de ângulo (itens 1 e 2 da tabela).
- Variedade de setup no Headless (no jogo a leitura do adversário já varia um pouco).
- Reação à baixa do resto do time (ficar a meio caminho) e reposicionar em vantagem.
- Micro dos pros (posição exata, mira, granada): precisa de arquivos .dem e de um leitor instalado.
