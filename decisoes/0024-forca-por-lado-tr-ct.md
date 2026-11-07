# 0024 — Força por lado (TR e CT) e treino de um jogador

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/02-mecanicas.md, gdd/03-progressao.md, gdd/06-arte.md, gdd/08-ui-ux.md
- **Substitui:** — (estende a 0013: o treino por atributo continua igual)

## Contexto
Hoje cada jogador tem uma força só, igual no ataque e na defesa. No jogo de verdade um jogador pode ser muito bom como TR e só razoável como CT, e isso é parte da graça de montar e escalar um time. Além disso, a tela de Treino é só uma tabela com todos os jogadores, e o cartão do elenco usa um boneco 3D igual para todos.

## Decisão (palavras do autor)
- O **treino tem diferença entre os lados**: "um bot pode ser bom no TR e não tanto no CT".
- Haverá uma **tela de treino específica de cada jogador**, no layout de uma ficha de atleta (foto grande, nome, números e tabela de estatísticas).
- A **foto** do jogador deve ser mais interessante que o boneco 3D, e o cartão do elenco também passa a ter uma foto diferente, "do boneco TR ou CT".

## Detalhamento (proposta do agente, a validar)
1. **Domínio de lado:** cada jogador tem dois números de 0 a 100, o **domínio do TR** e o **domínio do CT** (50 é neutro). Na simulação, a habilidade dele (mira, reflexo e sangue-frio) muda conforme o lado do round: **até ±8 %** a cada 50 pontos de diferença do domínio para a média dos dois lados. A média dos dois lados é a própria força dele, então o geral e a chance de vitória não mudam; muda quem rende mais em cada metade da partida (e a troca de lado no half passa a importar).
2. **Origem:** todo jogador nasce com uma especialidade sorteada (o domínio de um lado sobe até 20 pontos e o do outro desce o mesmo tanto).
3. **Treino por lado:** na semana, além do foco (atributo) e das sessões, o manager escolhe o **lado**: os dois (padrão), só TR ou só CT. Treinar um lado sobe o domínio dele em até **1,8 ponto por sessão** (menos perto de 100 e com cansaço alto), mas nessas sessões o atributo em foco rende **60 %** do normal. Um lado que ninguém treina **esfria 1 % por semana** em direção a 50, então especializar tem preço.
4. **Tela do jogador:** abre ao clicar no nome na tela de Treino. Mostra a foto grande, nome, função e idade, geral, cansaço e preparo, a tabela com os 5 atributos e o geral **no TR e no CT** e a evolução da temporada, e o plano da semana (foco, sessões, lado) com a conclusão do que a semana rende.
5. **Fotos:** retratos ilustrados de um operador do ataque (TR) e de um da defesa (CT), no mesmo estilo da identidade própria (decisão 0004). O cartão do elenco e a tela do jogador mostram a foto do **lado em que ele rende mais** (se rende igual, um dos dois pelo nome); metade das fotos é espelhada para o elenco não parecer clonado. Sem o arquivo, a tela cai para o boneco 3D.

## Por quê
- Pilar 3 (gestão que aparece na partida): o lado muda a habilidade que a simulação usa, e dá para ver na hora (cartão, tela do jogador, resultado do half).
- Pilar 5 (complexo por dentro, simples de entender): o jogador vê a conclusão ("melhor no TR") e a tabela por lado; o número do domínio fica na dica.
- Dá uma decisão nova para o manager (escalar e treinar pensando nos dois lados) sem mudar a força total do time.

## Consequências
- Números provisórios (±8 %, 1,8 por sessão, 60 %, 1 % de esfriar, ±20 de sorteio).
- O salvamento guarda o domínio dos dois lados e o lado em treino; salvamentos antigos começam neutros (50 e 50).
- Os jogadores dos times adversários continuam sem lado (o adversário ainda é um número de força, ver pergunta aberta sobre adversários).
- As fotos atuais são **só duas** (TR e CT), geradas por IA para o protótipo; arte individual por jogador fica em aberto.
