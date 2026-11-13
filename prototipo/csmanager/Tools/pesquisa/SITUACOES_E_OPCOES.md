# Catálogo de situações e opções de um jogador num round de CS competitivo

Base para a inteligência dos bots do CSManager. Escrito em 2026-11-12 a partir das falas do Gabriel (`gabriel_falas_10a12nov.txt`), do `PLANO_SIMULACAO.md` (seções 2, 3, 4.1, 5 e 9.1), de `Tools/pro/PROS_EM_PALAVRAS.md` e da pesquisa em `taticas_cs.md` e `reports/`. Nomes de lugar seguem um mapa tipo Mirage (meio, janelão, conector, rampa, palácio, casarão/apartamentos, tapete, curta, mercado, base CT).

## 1. Como ler este catálogo

- Cada **situação** (S1, S2, ...) é um momento do round que dispara uma decisão, descrito pelo que o jogador viu, ouviu ou recebeu por call. O bot só sabe isso: é crença, não verdade.
- Cada ficha lista **todas** as jogadas possíveis. Nenhuma é proibida: toda opção é certa no momento certo, e a ficha diz qual é o momento e o que a jogada custa.
- A decisão é por **avaliação**: o bot lista as opções, estima o valor de cada uma pela crença (usando os fatores de "O que pesa na escolha"), faz a melhor e fica nela até a situação mudar. Nunca por sorteio. Quando duas valem quase o mesmo, o estilo do jogador desempata.
- "Erros de quem avalia mal" vira o **erro com causa** do bot (crença errada, não percebeu, avaliou mal sob pressão). "Cena" é o teste: dá para conferir olhando um round.
- Cada linha de opção termina com um comentário `<!--b:...-->` com os ids das **jogadas-base** que ela usa. A seção 4 junta essas jogadas-base: é a lista de ações que o bot precisa saber executar.

## 2. Lista de situações

**Compra e freeze**

- S1. Escolher o tipo de compra do round
- S2. Dropar, pedir e distribuir arma (AWP e rifle)
- S3. Escolher granadas, kit e o que guardar
- S4. Receber a call do IGL e montar a minha parte
- S5. Pistol round: como abrir

**Abertura (primeiros 15 a 20 s)**

- S6. TR: saída do spawn numa call de rush
- S7. TR: saída do spawn num default
- S8. TR: primeiro a chegar num ponto de controle (meio, conector) sem informação
- S9. CT: saída da base e setup inicial
- S10. CT: agressão cedo por informação
- S11. CT: AWPer escolhe o ângulo de abertura
- S12. TR: AWPer ou jogador de pick abre o round
- S13. TR: o lurker começa o round

**Controle de mapa e default**

- S14. TR em default: segurando o espaço conquistado
- S15. CT: ida e volta de informação (o olho do meio)
- S16. Na espera: posição de pixel, strafe e a quem olhar
- S17. CT: decidir rotação por informação
- S18. TR: converter o default em jogada
- S19. Lurker: quando atacar

**Contato e duelo**

- S20. Inimigo aparece de frente, contato aberto sem preparação
- S21. Sei (ou acho) que há inimigo atrás da quina
- S22. Duelo parado de longe (troca de tiro à distância)
- S23. Levei o primeiro tiro e estou ferido (abre e volta)
- S24. Inimigo com AWP (visto, ouvido ou suspeito)
- S25. Dois ou mais inimigos ao mesmo tempo
- S26. Corpo a corpo e duelo de perto
- S27. Tomei tiro (ou dano) sem ver quem
- S28. Acabei de matar alguém: e agora
- S29. Estou atrás de quem vai abrir (o segundo da dupla)
- S30. A flash me pegou (estou cego)
- S31. Minha arma é pior que a deles (SMG/pistola contra rifle/AWP)

**Depois de uma baixa**

- S32. Meu colega morreu e eu estava perto
- S33. Meu colega morreu e eu estava longe
- S34. O entry TR caiu na entrada sem troca
- S35. Morri: o que o morto faz
- S36. Uma arma caiu no chão (AWP, rifle, kit) e eu vejo ou sei onde está
- S37. O IGL caiu no meio do round: quem decide

**Execução e entrada no bomb (TR) e defesa da entrada (CT)**

- S38. TR: execução ensaiada, esperar o go e cumprir a minha granada
- S39. TR: entrando no bomb e há quina com possível defensor
- S40. TR: sem flash e sem tempo para a entrada
- S41. TR: split e segundo grupo
- S42. TR: o meu papel num fake
- S43. TR: a execução travou, o CT segura
- S44. CT: defender a entrada contra uma execução (smoke e flash do TR)
- S45. CT: âncora sozinho com três ou mais TR chegando
- S46. CT: o rotador chega ao bomb pressionado
- S47. CT: o TR rusha e eu estou longe

**Informação nova (som, tiro, recarga, plant, defuse, granada, call)**

- S48. Ouvi passos
- S49. Ouvi tiro (de longe, de colega ou inimigo)
- S50. Ouvi um inimigo recarregando
- S51. Ouvi o plant
- S52. Ouvi o defuse (TR)
- S53. Granada vindo ou caindo (HE, flash, molotov, smoke) e de onde veio
- S54. Colega cantou uma informação (call recebida)
- S55. A crença não bate com o que vejo (ninguém apareceu / informação velha)

**Utilitária no caminho**

- S56. TR: indo para o bomb e uma smoke inimiga fecha a passagem
- S57. TR: o CT smokou o meio (decisão de quem tem a posição do meio)
- S58. CT: eu smokei (ou fui smokado) e agora o que faço enquanto ela está de pé
- S59. Molotov (ou incendiária) caiu no meu caminho ou no meu canto
- S60. Escolher qual granada jogar, onde e quando
- S61. Flash para o colega (e a minha própria)

**Pós-plant (TR), retake e save (CT)**

- S62. TR: acabei de plantar, onde me posiciono
- S63. TR pós-plant: ouvi o CT chegando
- S64. TR pós-plant: ouvi o defuse
- S65. TR pós-plant: o tempo e os vivos
- S66. CT: a bomba foi plantada, retomar ou salvar
- S67. CT: aproximação do retake
- S68. CT: entrada do retake e o portador do kit
- S69. CT: colega morreu no retake e fiquei sozinho
- S70. Desarmar com gente viva ao redor

**Vantagem e desvantagem numérica**

- S71. TR: ganhei o primeiro abate (5v4)
- S72. CT: ganhei o primeiro abate (5v4)
- S73. TR: estamos em desvantagem (4v5 ou pior)
- S74. CT: estamos em desvantagem (4v5 ou pior)
- S75. Vantagem pequena tardia (3v2, 2v1, 4v3)

**Clutch (1 contra N)**

- S76. Clutch 1v1
- S77. Clutch 1vN sem plant (TR) ou antes do plant
- S78. Clutch CT 1vN com a bomba plantada
- S79. Clutch TR pós-plant 1vN

**Fim de round**

- S80. Relógio apertado, TR sem plant
- S81. CT: relógio apertando, o TR não plantou
- S82. Salvar: decidir cedo e junto
- S83. TR: caçar o save
- S84. Pegar a arma do chão (e o kit)
- S85. A bomba vai explodir ou o round está decidido: o que cada um faz

**C4 (levar, cair, pegar, vigiar, plantar, cortar, desarmar, fake)**

- S86. Levar o C4
- S87. O C4 caiu (o portador morreu): quem pega
- S88. CT: o C4 está no chão e o TR vai buscar
- S89. Plantar com gente perto (e escolher o lugar)
- S90. CT: cortar o plant
- S91. Fake de defuse e como reagir a ele
- S92. Passar ou largar o C4 (e desistir de plantar)

## 3. Fichas

### Grupo: Compra e freeze

### S1. Escolher o tipo de compra do round
**Quando:** o freeze começou; o jogador vê o próprio dinheiro, o placar, a sequência de derrotas e a call de compra do IGL (ou a ausência dela). Sabe, pelo que viu, a arma e o colete que o adversário tinha no round anterior.
**Opções:**
- **Compra cheia** — rifle (ou AWP se é a função), colete, capacete, kit/granadas — melhor quando o time todo tem dinheiro para isso e o adversário também vem cheio; custa ficar sem reserva se perder. <!--b:comprar-->
- **Eco (não comprar)** — guardar o dinheiro e, no máximo, pistola ou colete leve — melhor quando o time não fecha uma compra cheia no próximo round e o adversário está rico; custa o round quase entregue, mas a economia seguinte vem inteira. <!--b:comprar,guardar_arma-->
- **Forçada** — arma barata + colete, pouca granada — melhor depois de vitória sobre compra fraca deles, em match point contra ou quando o adversário também está se reconstruindo; custa o dinheiro dos próximos dois rounds se falhar. <!--b:comprar-->
- **Meia compra** — SMG ou rifle barato com colete — melhor em mapa/lado com linhas curtas (CT na entrada do bomb, TR em rush); custa desvantagem em linha longa. <!--b:comprar-->
- **Comprar para o colega** — completar o que falta a quem está sem dinheiro (drop) — melhor quando só um não fecha a compra e o time precisa dos cinco armados; custa o próprio equipamento. <!--b:comprar,pegar_arma-->
- **Esperar a call e comprar o que o IGL pediu** — seguir o plano do time — melhor quando o IGL já leu a economia dos dois lados; custa não corrigir um erro do IGL. <!--b:esperar_call,comprar-->
- **Anti-eco** — comprar SMG/escopeta e granada barata, não rifle caro — melhor quando a crença é que o adversário veio de eco; custa perder o alcance se ele veio de arma cheia. <!--b:comprar-->
**O que pesa na escolha:** meu dinheiro e o do time (ouvido por call); o que o adversário comprou no round anterior e o dinheiro que ele provavelmente tem (derrotas seguidas, plant no round passado); placar e metade da partida; função (AWPer precisa de mais); se a compra cheia no round seguinte fica possível; o que o adversário já viu de mim (se eu sempre forço depois de ganhar, ele espera).
**Erros de quem avalia mal:** compra sozinho a arma cara sem olhar o dinheiro do colega e o time fica metade armado; ou força porque "dá" no bolso e não porque o round pede. Estimar mal o dinheiro do adversário (esquece o bônus de derrota) faz tratar eco como compra cheia.
**Cena:** Situação: time com 2 de 5 sem dinheiro para rifle e adversário rico. / Esperado: ou os cinco fazem eco juntos, ou os que podem dropam para os outros; não saem 3 de rifle e 2 de pistola sem plano.

### S2. Dropar, pedir e distribuir arma (AWP e rifle)
**Quando:** no freeze o jogador vê o que cada colega tem (por call ou por ver o drop); um colega com função de AWPer está sem AWP, ou já há uma AWP no time, ou ele está com dinheiro sobrando.
**Opções:**
- **Dropar rifle para o AWPer/colega** — jogar a arma no chão perto dele — melhor quando o colega pediu ou não consegue comprar e eu tenho dinheiro; custa meu dinheiro e minha arma até comprar outra. <!--b:pegar_arma-->
- **Pedir drop** — chamar "preciso de arma" para um colega específico — melhor quando eu sei que ele tem sobra; custa o tempo de freeze e pode ser ignorado. <!--b:call,pegar_arma-->
- **Comprar a AWP** — pegar a arma da função — melhor quando sou o AWPer, o time ainda não tem uma (ou tem só uma e a leitura da partida pede segunda); custa o equilíbrio de dinheiro do time. <!--b:comprar-->
- **Não comprar AWP** — ficar de rifle mesmo tendo dinheiro — melhor quando o time já tem uma (ou duas) AWP; custa deixar dinheiro parado, mas evita três AWPs e um AWPer de pistola. <!--b:comprar-->
- **Receber o drop e devolver o dinheiro à arma** — pegar a AK dropada e não comprar a AWP, ou comprar a AWP e dropar a AK de volta/para quem não tem — melhor quando ninguém mais está sem arma; custa o drop se eu deixo a AK no chão sem avisar. <!--b:pegar_arma,call-->
- **Dropar a arma ao morrer ou no fim do round** — deixar rifle/AWP onde o colega vai passar — melhor quando o colega sobrevivente pode pegá-la; custa nada, mas só vale se alguém vê. <!--b:pegar_arma-->
- **Guardar a arma comprada para o próximo round** — não usar em round perdido — melhor em save; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** quantas AWPs o time já tem (o dono do jogo quer uma, no máximo duas); quem é o AWPer e se ele já tem AWP; dinheiro de cada um; se há arma no chão que ninguém pegou; a chance de round seguinte; o que eu vi de gente sem arma.
**Erros de quem avalia mal:** compra AWP porque tem dinheiro, não porque a função pede; larga a AK no chão sem avisar e ninguém vê, o dinheiro é desperdiçado; não percebe que o colega ficou de USP.
**Cena:** Situação: time com 3 compras de AWP possíveis. / Esperado: no máximo 2 AWPs saem da compra; quem recebe rifle de drop não deixa a AK no chão sem avisar.

### S3. Escolher granadas, kit e o que guardar
**Quando:** o jogador tem dinheiro depois da arma e do colete; conhece a call (execução, default, rush) e as granadas que o time precisa para ela.
**Opções:**
- **Smoke** — comprar para a tarefa de corte de linha ou de bloqueio de rotação — melhor quando a call ou o setup pede e eu sei o lineup; custa o dinheiro de uma flash. <!--b:comprar,smoke-->
- **Molotov** — nega canto e atrasa avanço/retake — melhor para quem vai limpar canto de perto ou segurar entrada; custa mais caro e é difícil de usar sem lineup. <!--b:comprar,molotov-->
- **Flash (uma ou duas)** — para entrar ou para contra-flash — melhor para entry/suporte e para quem vai duelar atrás de colega; custa pouco, mas cega o time se mal usada. <!--b:comprar,flash-->
- **HE** — dano em quem foi ouvido ou empilhado — melhor com informação de grupo parado; custa uma vaga de granada. <!--b:comprar,he-->
- **Kit de defuse** — para quem vai ao retake — melhor para CT que fica perto do bomb ou é o último a girar; custa dinheiro que não vira dano. <!--b:comprar-->
- **Guardar granada para o pós-plant/retake** — não gastar tudo na entrada — melhor para uma smoke e um molotov do time; custa menos poder na execução. <!--b:guardar_arma-->
- **Pedir granada a um colega** — "me dá uma flash" — melhor quando sobrou dinheiro a ele e falta a mim; custa tempo. <!--b:pedir_granada-->
**O que pesa na escolha:** a call (que granada é minha tarefa); função (suporte leva mais, entry quase não joga granada); o que o time já tem (não dobrar a mesma smoke); dinheiro restante; limite de 4 granadas e 2 flashes; o que vou precisar depois do plant; se o adversário costuma gastar granada cedo.
**Erros de quem avalia mal:** compra HE por hábito em vez da granada que a tarefa exige; dois jogadores compram a mesma smoke para o mesmo lugar e falta a que corta a outra linha; ninguém guarda granada para depois do plant.
**Cena:** Situação: execução de A pede smoke de janela, smoke de conector e molotov. / Esperado: cada granada tem dono e ninguém dobra; sobra pelo menos uma smoke ou um molotov do time para depois do plant.

### S4. Receber a call do IGL e montar a minha parte
**Quando:** o IGL anuncia a jogada do round (rush B, execução A, default, stack, joga o meio) com o papel de cada um; o jogador conhece o que viu no round anterior e o que ele mesmo acha do adversário.
**Opções:**
- **Cumprir exatamente a minha tarefa** — fazer o que a call diz, no tempo dela — melhor quando a call faz sentido com o que eu sei; custa a flexibilidade se a call está errada. <!--b:esperar_call-->
- **Cumprir a tarefa e avisar a dúvida** — dizer "acho que tem AWP lá" e seguir — melhor quando tenho um indício que o IGL pode não ter; custa tempo de comunicação. <!--b:call,esperar_call-->
- **Contrariar a call por leitura própria** — fazer outra coisa porque acredito mais na minha leitura — melhor quando vi ou ouvi algo que invalida a call e o IGL não sabe; custa romper a coordenação se eu errar. <!--b:mudar_angulo,virar_round-->
- **Pedir a troca do papel** — "me dá a flash, eu entro" — melhor quando sei que sou melhor numa parte; custa o acordo de função. <!--b:call-->
- **Ficar perto de quem a call manda eu trocar** — me posicionar atrás do entry/colega — melhor quando a tarefa é trocar; custa ficar sem outra opção se ele cair rápido. <!--b:ficar_perto-->
- **Segurar a granada até o gatilho** — esperar o "go" ou o sinal para gastar — melhor em execução ensaiada; custa tempo se o gatilho não vem. <!--b:esperar-->
- **Assumir a tarefa que sobrou** — pegar o que o colega não pode fazer (smoke que faltou) — melhor quando o plano precisa e ninguém assumiu; custa a tarefa original. <!--b:smoke,flash,call-->
**O que pesa na escolha:** o que cada colega vai fazer; minha função; minha crença sobre onde o adversário joga; a confiança que o time tem nessa jogada (falhou duas vezes?); o que o adversário já viu desta jogada; o relógio e o que já foi comprado.
**Erros de quem avalia mal:** executa a call mesmo depois de ter visto algo que a invalida; ou discorda por mania e quebra o plano; pensa igual aos outros (todos fazem a mesma coisa ao mesmo tempo) e ninguém cobre a tarefa que sobrou.
**Cena:** Situação: IGL chama "A com smoke de janela e conector" e o suporte não tem a segunda smoke. / Esperado: outro jogador assume a segunda smoke ou o IGL é avisado; o time não vai com uma linha sem corte.

### S5. Pistol round: como abrir
**Quando:** primeiro round da metade, todos com pistola (e talvez uma granada); o jogador sabe o lado e o mapa, e que o adversário também tem pistola.
**Opções:**
- **Bloco de 4–5 por uma entrada (TR)** — andar juntos e fechar distância com flash — melhor contra CT espalhado de pistola; custa o round se o CT empilha. <!--b:ir_correndo,reagrupar,flash-->
- **Default pequeno e espaço (TR)** — duas ou três posições e esperar o CT errar — melhor com granadas de sobra; custa o tempo do relógio. <!--b:tomar_espaco,esperar-->
- **Rush cedo com Glock (TR)** — chegar ao bomb antes do CT estar posicionado — melhor quando o CT é dividido; custa o round se alguém segura de perto. <!--b:ir_correndo-->
- **Ficar em dupla no bomb (CT)** — dois por bomb e um no meio — melhor para não ficar sozinho num bomb; custa a cobertura do meio. <!--b:ficar_perto,cruzado-->
- **Agressão cedo por informação (CT)** — um sobe ao meio ou ao palácio e volta — melhor para contar o TR; custa o jogador se pego sozinho. <!--b:jiggle,tomar_espaco-->
- **Posição de rota de fuga (CT)** — ficar onde dá para recuar e trocar — melhor para o jogador mais cuidadoso; custa o tempo de chegada ao bomb. <!--b:recuar_cobertura-->
- **Deixar o colega fazer o primeiro contato e entrar em seguida (CT)** — esperar o parceiro bater e trocar — melhor em pistol, onde a troca decide; custa o espaço se o parceiro cai e eu não troco. <!--b:trocar,ficar_perto-->
**O que pesa na escolha:** o que o adversário fez no pistol da outra metade (mesmo bomb, mesma entrada); número de granadas; colete vale mais que granada; função; quem é o colega ao lado; o tempo de chegada de cada spawn.
**Erros de quem avalia mal:** corre sozinho na frente do bloco e perde a troca; repete o pistol do round passado sem ver que o adversário já conhece; joga longe do colega e não troca.
**Cena:** Situação: pistol TR em Mirage, o mesmo bomb foi usado no pistol anterior e perdeu. / Esperado: o time muda a entrada ou o ritmo e não repete a mesma corrida.

### Grupo: Abertura (primeiros 15 a 20 s)

### S6. TR: saída do spawn numa call de rush
**Quando:** o IGL chamou "rush A/B"; o jogador sabe a rota e o tempo, e que o CT leva alguns segundos para chegar e se posicionar.
**Opções:**
- **Correr direto com o grupo** — chegar ao bomb o mais rápido possível — melhor quando o CT é fraco de compra ou o rush é por surpresa; custa o round se houver defensor de mão pronta. <!--b:ir_correndo,reagrupar-->
- **Correr com a flash/smoke de entrada** — usar a granada ao chegar — melhor quando o time compra e quer proteger a entrada; custa um segundo de espera. <!--b:ir_correndo,flash,smoke-->
- **Correr em duas rotas e juntar na entrada** — metade pelo meio, metade direto — melhor quando o mapa permite chegar junto; custa a coordenação do tempo. <!--b:dar_volta,ir_correndo-->
- **Parar na linha do primeiro contato e ouvir** — rush até o ponto e ouvir antes de entrar — melhor quando suspeito de stack; custa a surpresa. <!--b:ouvir,esperar-->
- **Mudar de ideia no caminho** — virar para o outro bomb ao ouvir/ver o CT empilhado — melhor quando a informação nova mostra o CT forte ali; custa o tempo gasto e a coordenação. <!--b:virar_round,rotacionar-->
- **Rush com isca na frente** — um corre à frente para tirar a primeira mira e os outros entram — melhor quando há AWP no CT; custa o jogador isca. <!--b:isca_troca,ir_correndo-->
- **Atrasar a saída de quem está mais perto** — esperar quem sai de longe para chegar junto — melhor quando o tempo de chegada importa; custa nada se todos sincronizam. <!--b:esperar,reagrupar-->
**O que pesa na escolha:** o tempo de cada spawn até a entrada; o que o CT mostrou nos últimos rounds (stack, agressão cedo); minhas granadas e as do time; quem é o primeiro; a arma do CT provável; o relógio.
**Erros de quem avalia mal:** corre cada um no seu ritmo e chega separado; continua o rush ao ouvir um stack claro; chega sem saber que a entrada tem AWP.
**Cena:** Situação: rush B e no caminho ouvem 3 CT no B. / Esperado: o time não entra um a um; segura, joga granada ou vira o round, nenhum deles segue correndo cego.

### S7. TR: saída do spawn num default
**Quando:** a call é "default" ou "pega espaço e vê"; o jogador tem uma posição inicial, uma tarefa de informação e um prazo (decidir aos 30–50 s).
**Opções:**
- **Ir à posição e segurar com jiggle** — chegar ao ponto e fazer peeks curtos para ouvir/ver — melhor quando o prazo permite; custa o risco de ser puxado. <!--b:tomar_espaco,jiggle-->
- **Ir à posição em silêncio (shift)** — não dar barulho até o contato — melhor quando o CT gira por som; custa tempo de chegada. <!--b:ir_silencio-->
- **Gastar uma granada de presença** — soltar uma smoke/molotov para forçar o CT a gastar — melhor com granada sobrando e CT agressivo; custa menos granada na execução. <!--b:smoke,molotov-->
- **Chegar cedo e esperar o espaço do colega** — não avançar além da linha até o colega tomar o ponto — melhor em par; custa a velocidade. <!--b:esperar,ficar_perto-->
- **Ir pelo caminho incomum** — rota diferente da de sempre porque o adversário já a conhece — melhor quando a crença diz que o CT sabe o caminho; custa tempo. <!--b:dar_volta-->
- **Antecipar o contato (tomar espaço rápido)** — correr até o ponto antes do CT — melhor com spawn bom ou quando sei que o CT sobe tarde; custa risco de dar de cara. <!--b:ir_correndo,tomar_espaco-->
- **Esperar o relógio e a call** — ficar no spawn ou perto até o IGL chamar — melhor em round de leitura; custa o espaço. <!--b:esperar,esperar_call-->
**O que pesa na escolha:** onde o CT costuma estar nos primeiros 15 s (memória da partida); a função (entry, suporte, lurker); o que o adversário já viu de mim; se tem AWP deles no meio; as granadas que tenho; o prazo do default.
**Erros de quem avalia mal:** corre ao ponto mais próximo sem checar o que o CT mostrou; fica parado "segurando" onde não ameaça ninguém; faz tudo no mesmo ritmo do round anterior.
**Cena:** Situação: default TR, o CT subiu para o palácio nos dois últimos rounds. / Esperado: pelo menos um TR toma a posição de forma diferente (rota, ritmo ou granada) e não repete o mesmo caminho.

### S8. TR: primeiro a chegar num ponto de controle (meio, conector) sem informação
**Quando:** o jogador vai tomar o meio ou outro ponto; não viu nem ouviu o CT; sabe que o CT pode estar à frente ou ainda saindo da base.
**Opções:**
- **Jiggle antes de entrar** — peek curto para ver se está ocupado — melhor quando o ponto é perigoso (meio com AWP provável); custa o tempo de um segundo. <!--b:jiggle-->
- **Entrar em silêncio e ouvir** — shift até a linha e parar — melhor para saber se há alguém; custa um segundo de chegada. <!--b:ir_silencio,ouvir-->
- **Flashar o ponto antes** — granada para dentro do ponto — melhor quando suspeito que o CT está ali e tenho flash; custa uma flash. <!--b:flash-->
- **Smoke para cruzar** — cobrir o trecho aberto — melhor com AWP provável; custa uma smoke e some o campo. <!--b:smoke-->
- **Esperar um colega para entrar junto** — não ser o primeiro sozinho — melhor com apoio perto; custa o tempo. <!--b:esperar,abrir_dupla-->
- **Pular a quina (jump peek)** — passar pulando para tirar a mira — melhor para atravessar a linha de um AWP possível; custa precisão. <!--b:pular_peek-->
- **Atravessar rápido (correndo)** — cruzar a linha em um segundo — melhor quando o CT chega tarde; custa se ele chega cedo. <!--b:ir_correndo-->
**O que pesa na escolha:** quanto tempo o CT leva de lá até o ponto; se eu acredito em AWP no meio; quantos colegas estão perto; minhas granadas; o hábito do CT na partida (sobe cedo?); o som que eu ouvi.
**Erros de quem avalia mal:** entra correndo e dá de cara com o AWP já mirado; fica no ponto parado e vira alvo de granada; não avisa o time do que viu.
**Cena:** Situação: início de round, TR chega ao meio, CT AWP já apareceu dois rounds atrás nessa linha. / Esperado: o TR usa smoke, pula ou espera o colega em vez de atravessar a linha seco.

### S9. CT: saída da base e setup inicial
**Quando:** o round começou; o jogador conhece o setup do round (2-1-2, aposta, B forte) e sua tarefa; ainda não ouviu nem viu o TR.
**Opções:**
- **Ir à posição do setup e segurar** — chegar e esperar — melhor para âncora e para quem tem a posição fixa; custa a informação. <!--b:esperar-->
- **Ir à posição e fazer jiggle por informação** — espiar sem se expor — melhor para quem tem o papel de olhar o meio/rampa; custa o risco de ser puxado. <!--b:jiggle,tomar_espaco-->
- **Avançar até um ponto de informação e voltar** — subir ao palácio/meio e recuar em segundos — melhor quando o prazo é curto e o TR provavelmente ainda não chegou; custa a posição se o TR já está lá. <!--b:tomar_espaco,recuar_cobertura-->
- **Ficar na base e esperar a primeira informação** — não se comprometer — melhor para o jogador de rotação; custa o tempo para chegar a um bomb pressionado. <!--b:esperar,rotacionar-->
- **Jogar granada de atraso na entrada provável** — smoke/molotov no rush — melhor em CT contra rush forçado; custa menos granada para o retake. <!--b:molotov,smoke-->
- **Pegar o ângulo de AWP** — posicionar-se na linha longa — melhor para o AWPer; custa estar longe se o TR vem pelo outro lado. <!--b:esperar,prefire-->
- **Mudar a posição que sempre uso** — ir ao ângulo diferente — melhor quando o TR já pré-mirou meu ponto; custa ficar sem a cobertura de sempre. <!--b:mudar_angulo-->
**O que pesa na escolha:** o setup do round (2-1-2, stack); o que o TR fez nos últimos rounds (rush? default?); a economia dele (eco = rush); minha função (âncora, rotação, AWP); onde meus colegas vão estar (cruzado); o tempo de rotação do mapa.
**Erros de quem avalia mal:** vai à mesma posição de sempre (previsível); fica parado onde ninguém ameaça; vai todo mundo pelo mesmo caminho para a base e se concentra em um canto.
**Cena:** Situação: início do round CT com 5 vivos. / Esperado: aos 10 s há CT nos dois bombs e alguém adiantado; o setup varia de um round para outro e nem sempre passa pela base.

### S10. CT: agressão cedo por informação
**Quando:** o jogador está num ponto adiantado (janelão, meio, palácio) nos primeiros 15–20 s; ainda não viu o TR; ouve ou não ouve sinal de passos.
**Opções:**
- **Peek curto e volta** — espiar e recuar em um segundo — melhor para só confirmar presença; custa pouca informação. <!--b:jiggle,recuar_cobertura-->
- **Avançar com flash/granada** — jogar granada e ganhar o espaço — melhor com apoio e flash; custa gastar granada. <!--b:flash,tomar_espaco-->
- **Ficar no ponto e ouvir** — parar e esperar o som — melhor quando o ponto é seguro e tenho cobertura; custa o tempo. <!--b:ouvir,esperar-->
- **Voltar ao setup e passar a informação** — chamar "vi nada/ouvi tantos" e recuar — melhor quando a informação é o objetivo; custa o espaço. <!--b:call,recuar_cobertura-->
- **Seguir em frente e dar o flanco** — subir pelo caminho oposto e pegar o TR por trás — melhor quando o TR já está comprometido num bomb; custa estar longe do bomb que pode cair. <!--b:dar_volta-->
- **Pedir flash ao colega e abrir** — combinar com quem está atrás — melhor quando há par; custa depender do colega. <!--b:pedir_granada,bangar_entrar-->
- **Montar uma isca e trocar** — aparecer e sumir para o colega matar quem seguir — melhor com par pronto; custa o colega mal posicionado. <!--b:isca_troca-->
**O que pesa na escolha:** tempo desde o início do round (o TR já chegou ao meio?); granadas que tenho; se o colega está perto; o que o TR fez na partida (empurra cedo?); se eu tenho informação nova; se sou o AWPer (eu não subo sozinho).
**Erros de quem avalia mal:** sobe sozinho sem flash e morre sem troca; fica no ponto avançado depois do tempo útil; não volta e deixa o bomb sem informação.
**Cena:** Situação: CT sobe ao meio aos 12 s e vê dois TR. / Esperado: ele chama a informação, usa granada se tem ou recua, e o time ajusta o setup; não duela sozinho no meio.

### S11. CT: AWPer escolhe o ângulo de abertura
**Quando:** o AWPer está no início do round; conhece os ângulos preferidos, os que já usou na partida e os que o TR já mirou.
**Opções:**
- **Ângulo de linha longa padrão** — o ponto de sempre — melhor quando o TR ainda não o puniu; custa a previsibilidade. <!--b:esperar,prefire-->
- **Ângulo diferente do round passado** — mudar de ponto porque o TR já me viu ali — melhor depois de um abate ou de uma flash/smoke no mesmo lugar; custa estar sem a cobertura do colega. <!--b:mudar_angulo-->
- **Jogar recuado/segundo homem de cruzado** — ficar em retaguarda e deixar o colega fazer o primeiro — melhor quando estou sendo sufocado; custa menos impacto. <!--b:cruzado,recuar_cobertura-->
- **Abrir agressivo (pick cedo)** — peek com apoio para a primeira baixa — melhor com flash do colega e spawn bom; custa o AWP se a troca falha. <!--b:flash,bangar_entrar,isca_ombro-->
- **Jogar pelo bomb com rota de fuga** — posição com saída e colega perto — melhor no CT típico; custa menos alcance. <!--b:recuar_cobertura,cruzado-->
- **Responder a granada com granada** — se me jogaram flash/smoke no ponto, jogar a minha ou mudar de ponto — melhor quando sou sufocado; custa a granada. <!--b:smoke,mudar_angulo-->
- **Salvar a AWP em round perdido** — não arriscar — melhor em round decidido; custa o abate possível. <!--b:guardar_arma-->
**O que pesa na escolha:** onde atirei nos rounds anteriores; se o TR joga flash/smoke no meu ponto; o setup do time; meu colega de troca; o lado que o TR parece querer; se há rifle de troca ao lado.
**Erros de quem avalia mal:** fica sempre no mesmo ângulo; abre sozinho sem flash; atira na isca de ombro.
**Cena:** Situação: o AWPer CT morreu no mesmo ângulo no round anterior. / Esperado: ele muda de ponto, recua ou joga com granada; não repete o mesmo peek.

### S12. TR: AWPer ou jogador de pick abre o round
**Quando:** o jogador tem AWP (ou rifle com flash de colega) e a call pede um pick de abertura; sabe o ângulo, o tempo e quem o apoia.
**Opções:**
- **Pegar o ângulo de abertura com apoio** — peek com flash do colega — melhor quando há ponto de troca e vantagem de ângulo; custa a AWP se perde. <!--b:bangar_entrar,isca_ombro-->
- **Segurar a linha longa em vez de abrir** — ficar de apoio atrás — melhor quando o time vai executar e preciso cobrir; custa o pick. <!--b:esperar,cruzado-->
- **Isca de ombro e swing** — mostrar o ombro e abrir de verdade na janela do ferrolho — melhor contra AWP deles; custa se ele atira no corpo. <!--b:isca_ombro,pular_peek-->
- **Mudar de ponto depois do tiro** — atirar e reposicionar — melhor sempre que atirei; custa o ângulo bom. <!--b:reposicionar-->
- **Abrir em dupla (um pula, outro mata)** — combinar com colega — melhor contra AWP; custa depender do colega. <!--b:abrir_dupla,pular_peek-->
- **Segurar o tiro e esperar o corpo inteiro** — não atirar no ombro — melhor contra isca; custa o tempo. <!--b:segurar_tiro,esperar-->
- **Não abrir, jogar pelo tempo** — esperar a informação do time — melhor quando o ângulo foi pré-mirado; custa o pick. <!--b:esperar,jogar_relogio-->
**O que pesa na escolha:** onde o AWP deles joga (crença); se o ponto foi punido antes; o colega que troca; o spawn; a flash disponível; o momento do round (cedo vale mais).
**Erros de quem avalia mal:** abre seco contra um ângulo pré-mirado; repete o peek depois de um colega cair ali; perde a AWP cedo.
**Cena:** Situação: AWPer TR abre o meio e o CT já o matou ali no round anterior. / Esperado: ele troca de ponto, usa flash do colega ou joga outro papel; não faz o mesmo peek seco.

### S13. TR: o lurker começa o round
**Quando:** a call manda o lurker para o lado oposto ao time; o jogador sabe o tempo do time e o que cobre.
**Opções:**
- **Ir ao ponto de lurk em silêncio** — chegar sem barulho — melhor para ser invisível; custa tempo de chegada. <!--b:ir_silencio,dar_volta-->
- **Ficar parado e ouvir** — esperar o som do CT rotacionando — melhor para decidir o momento; custa a ação se o time precisa. <!--b:ouvir,esperar-->
- **Tomar um espaço e prender o CT** — avançar um pouco para fixar defensores — melhor quando o CT é passivo; custa o risco de ser achado. <!--b:tomar_espaco-->
- **Ir ao flanco do bomb alvo** — esperar no lado de trás da execução — melhor com o time executando; custa a janela perdida. <!--b:dar_volta,esperar-->
- **Fingir (barulho num lado)** — fazer som para puxar a rotação — melhor para ajudar o fake; custa o silêncio. <!--b:fingir-->
- **Voltar ao time** — se o lado oposto está vazio e sem tempo — melhor quando a informação diz que o time precisa; custa o lurk. <!--b:reagrupar-->
- **Chamar "ninguém passou"** — avisar que o lado oposto está quieto — melhor para dar informação ao IGL; custa nada. <!--b:call-->
**O que pesa na escolha:** onde o CT está; o tempo da execução do time; minha arma; se há AWP no lado; o quanto o CT rotaciona por som; a distância ao bomb.
**Erros de quem avalia mal:** lurkeia sem combinar o tempo e chega depois da execução; faz barulho e se entrega; não passa a informação.
**Cena:** Situação: execução TR em A e o lurker está no mercado. / Esperado: ele ataca quando o CT gira para o A (som de rotação), pega a rotação ou entra no vazio; não chega depois do plant.

### Grupo: Controle de mapa e default

### S14. TR em default: segurando o espaço conquistado
**Quando:** o time tomou um espaço (meio, conector) e o jogador está nele; o relógio corre; não há contato ainda.
**Opções:**
- **Segurar com micro-movimentos** — strafes e jiggle pequenos, mira na altura da cabeça — melhor quando o espaço é perigoso; custa se alguém me vê. <!--b:strafe_espera,esperar-->
- **Gastar uma granada de presença** — soltar uma granada no CT para forçá-lo — melhor com granada sobrando; custa menos granada depois. <!--b:smoke,molotov,he-->
- **Avançar um passo e tomar o próximo espaço** — passar do ponto — melhor quando a informação diz que o CT está fraco ali; custa o risco. <!--b:tomar_espaco-->
- **Recuar e deixar o CT vir** — esperar o CT empurrar para puni-lo — melhor com CT agressivo; custa o espaço. <!--b:recuar_cobertura,esperar-->
- **Trocar de posição** — mudar para outro ângulo do mesmo espaço — melhor quando fui visto ou ouvido; custa a pré-mira. <!--b:mudar_angulo-->
- **Chamar o que vejo/ouço** — passar informação para o IGL — melhor sempre que há algo; custa nada. <!--b:call-->
- **Avisar que o espaço está seguro e pedir o próximo passo** — esperar a call de conversão — melhor para o IGL decidir; custa o relógio. <!--b:call,esperar_call-->
**O que pesa na escolha:** o prazo do default; o que ouvi (rotação, passos); as granadas; os colegas perto; o que o CT fez na partida; a arma (AWP segura linha longa).
**Erros de quem avalia mal:** fica parado sem função até acabar o tempo; avança sem apoio; fica pré-mirado no mesmo ponto até morrer.
**Cena:** Situação: o TR tem o meio há 20 s e nada aconteceu. / Esperado: ele não é estátua: faz micro-movimentos, joga granada de presença, passa informação ou o IGL converte; não espera sem função.

### S15. CT: ida e volta de informação (o olho do meio)
**Quando:** o jogador cobre o conector/meio sem contato; o setup quer saber se o TR vem pelo meio.
**Opções:**
- **Olhar o meio e voltar** — espiar e recuar ao conector — melhor para checar a cada ~40 s; custa a posição por 2–3 s. <!--b:jiggle,recuar_cobertura-->
- **Esperar o som no conector** — ouvir passos do meio sem ir — melhor quando o meio é perigoso; custa a visão. <!--b:ouvir,esperar-->
- **Subir ao meio com flash** — checar com apoio — melhor quando suspeito que há TR; custa uma flash. <!--b:flash,tomar_espaco-->
- **Jogar uma granada no meio** — smoke/HE/molotov para cobrir ou machucar — melhor quando ouvi gente no meio; custa granada. <!--b:he,molotov,smoke-->
- **Avisar "meio livre/ocupado"** — dar a informação ao time — melhor sempre; custa nada. <!--b:call-->
- **Fingir (barulho)** — fazer som para o TR achar que subo — melhor para atrair e trocar; custa se não há troca. <!--b:fingir-->
- **Deixar de olhar** — ficar no conector quieto — melhor quando a informação diz que o meio está vazio; custa a atualização. <!--b:esperar-->
**O que pesa na escolha:** há quanto tempo olhei; o que ouvi; se o TR tem AWP no meio; granadas; o colega do A (cobertura); o prazo (o TR decide por volta de 40–50 s).
**Erros de quem avalia mal:** olha no mesmo ritmo e ponto, previsível; sobe demais; não avisa o que viu.
**Cena:** Situação: CT no conector, 40 s sem informação. / Esperado: ele olha o meio uma vez com um pico curto, avisa o resultado e volta; não fica estátua.

### S16. Na espera: posição de pixel, strafe e a quem olhar
**Quando:** o jogador está segurando um ângulo ou esperando a vez; não vê ninguém; sabe de onde o inimigo pode sair.
**Opções:**
- **Segurar com strafes curtos e mira na cabeça** — trefadinhas curtas, mira na altura da cabeça no pixel de saída — melhor quando espero contato e não quero ser acertado de graça; custa estar menos preciso no primeiro tiro. <!--b:strafe_espera,esperar-->
- **Mirar na quina do colega** — olhar para o ponto que o colega não cobre — melhor em cruzado; custa nada. <!--b:cruzado,esperar-->
- **Pré-fire num ponto comum** — atirar antes de ver em lugar provável — melhor quando a crença é forte; custa munição e revela posição. <!--b:prefire-->
- **Mudar de pixel a cada tantos segundos** — trocar levemente de posição — melhor quando fui visto ou há granada; custa a pré-mira. <!--b:mudar_angulo-->
- **Escutar e não atirar** — segurar o tiro para não dar posição — melhor com AWP deles; custa a oportunidade. <!--b:segurar_tiro,ouvir-->
- **Bangar para o colega que vai abrir** — jogar flash para quem está na frente — melhor quando o colega vai abrir; custa uma flash. <!--b:flash-->
- **Cobrir a retaguarda** — olhar atrás/flanco — melhor quando ninguém olha; custa o ângulo da frente. <!--b:cobrir_colega-->
**O que pesa na escolha:** a crença de onde sai o inimigo; se tenho apoio; minha arma (AWP segura mais parado); se há granada inimiga recente; o que o colega está fazendo; o relógio.
**Erros de quem avalia mal:** fica de lado olhando para nada; mira baixa e atira no peito de um inimigo que sai; fica atrás do colega sem função.
**Cena:** Situação: dois CT esperando o TR entrar; um está atrás do outro. / Esperado: o de trás cobre outro ângulo ou joga a flash para o da frente; ninguém fica olhando a mesma parede.

### S17. CT: decidir rotação por informação
**Quando:** o jogador ouve execução, granada ou tiro no outro bomb, ou recebe call de TRs; não sabe se é fake.
**Opções:**
- **Rotacionar completo** — ir ao bomb pressionado — melhor com C4 visto/plantado ou vários TR confirmados; custa abrir buraco no meu bomb. <!--b:rotacionar-->
- **Rotacionar parcialmente (meio caminho)** — ir até o meio e esperar mais informação — melhor quando a informação é parcial; custa chegar tarde. <!--b:rotacionar,esperar-->
- **Ficar** — manter minha posição — melhor quando suspeito de fake (barulho sem presença); custa o outro bomb. <!--b:esperar-->
- **Pedir confirmação** — "quantos aí?" antes de sair — melhor quando o colega pode dizer; custa tempo. <!--b:esperar_call,call-->
- **Girar com granada** — levar flash/smoke para entrar no bomb — melhor quando há tempo; custa o tempo de preparar. <!--b:rotacionar,flash-->
- **Sair por rota diferente (flanco)** — chegar por trás do TR — melhor se o TR já está comprometido e a rota está livre; custa tempo e risco. <!--b:dar_volta,rotacionar-->
- **Avisar "saí de X"** — chamar ao sair — melhor sempre que rotacionar; custa nada. <!--b:call-->
**O que pesa na escolha:** quantos TR eu acredito estarem no bomb; se o barulho foi granada ou passos reais; o tempo de rotação do mapa; quem mais pode rotacionar; o C4 visto; o que o TR fez nos rounds anteriores (fake?).
**Erros de quem avalia mal:** gira por qualquer barulho e abre buraco; não gira com C4 já plantado; todo mundo gira ao mesmo tempo.
**Cena:** Situação: ouvem molotov e passos no B, o jogador do A não viu ninguém. / Esperado: um rotador sai pela base e chega no B depois do primeiro defensor; o A não fica vazio.

### S18. TR: converter o default em jogada
**Quando:** o IGL (ou o jogador) tem informação suficiente (espaço tomado, CT localizado, prazo chegando); decide entre executar, tomar e segurar, ou reiniciar.
**Opções:**
- **Converter na execução ensaiada** — chamar a jogada pelo nome — melhor quando o CT está localizado e as granadas estão inteiras; custa o tempo de reposicionar. <!--b:virar_round-->
- **Tomar e segurar** — consolidar o espaço e jogar o relógio — melhor quando o quadro é incerto mas há espaço; custa o tempo. <!--b:tomar_espaco,jogar_relogio-->
- **Mudar de bomb** — trocar o alvo pela informação — melhor quando o CT stackou o bomb planejado; custa a rotação. <!--b:virar_round,rotacionar-->
- **Fake e girar** — barulho num lado e execução no outro — melhor quando o CT gira por som; custa granadas. <!--b:fingir,rotacionar-->
- **Rush tardio** — correr com o que tem — melhor quando o tempo apertou; custa a coordenação. <!--b:ir_correndo-->
- **Split** — dois grupos em duas entradas — melhor quando a ligação está tomada; custa a coordenação do tempo. <!--b:dar_volta,abrir_dupla-->
- **Pausa de ritmo** — esperar 20–30 s antes — melhor para quebrar o tempo de rotação do CT; custa o relógio. <!--b:esperar,jogar_relogio-->
**O que pesa na escolha:** defensores estimados em cada bomb; granadas CT gastas; picks; AWP localizado; o relógio; o que o adversário viu desta jogada; a economia.
**Erros de quem avalia mal:** converte sem ter o espaço; decide aos 20 s sem informação ou espera até 20 s restantes; escolhe o mesmo bomb de sempre.
**Cena:** Situação: aos 45 s o TR viu o CT gastar três granadas no A. / Esperado: o IGL converte no A (ou faz fake) com motivo; não segue esperando.

### S19. Lurker: quando atacar
**Quando:** o lurker está no flanco; o time executa ou o CT rotaciona; o jogador ouve sons.
**Opções:**
- **Entrar no vazio atrás do CT** — atacar a rotação que o time provocou — melhor quando ouvi CT sair do bomb alvo do time; custa o jogador se pego. <!--b:dar_volta,ir_correndo-->
- **Esperar o go do time** — atacar junto com a execução — melhor quando o tempo está combinado; custa a janela. <!--b:esperar-->
- **Cortar o retake** — ir ao C4 pelas costas depois do plant — melhor para pós-plant; custa não estar no plant. <!--b:dar_volta,tomar_espaco-->
- **Ficar quieto e esconder-se** — não fazer nada, manter a ameaça — melhor quando o lado oposto precisa do CT fixo ali; custa o ataque. <!--b:esconder,esperar-->
- **Fingir** — barulho para puxar o CT — melhor para ajudar o time; custa o silêncio. <!--b:fingir-->
- **Voltar ao time** — reagrupar — melhor quando a informação diz que a ação é lá; custa o lurk. <!--b:reagrupar-->
- **Chamar o que viu** — passar a informação do flanco — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** o tempo do time; o som que ouvi; o quanto o CT rotaciona; o relógio; minha arma e granadas; a distância ao alvo.
**Erros de quem avalia mal:** ataca cedo ou tarde demais; faz barulho e é achado; não conta o tempo do time.
**Cena:** Situação: execução TR em B e o lurker está no A. / Esperado: ele ataca o A quando o CT gira para o B ou fixa o CT ali; não fica sem função.

### Grupo: Contato e duelo

### S20. Inimigo aparece de frente, contato aberto sem preparação
**Quando:** o jogador vê um inimigo (ou toma o primeiro tiro) em linha aberta; não sabia que ele estava ali; vê quantos são, a distância e se o inimigo já o tinha na mira.
**Opções:**
- **Responder o duelo ali mesmo** — mirar e atirar (tap ou spray conforme distância/arma) — melhor quando estou na vantagem de mira, vida e arma, ou quando recuar é pior (sem cobertura); custa a vida se perder. <!--b:esperar,fechar_distancia-->
- **Recuar para a cobertura e esperar** — sair da linha e segurar o ângulo — melhor quando o inimigo me viu primeiro ou tem arma melhor; custa a posição e a informação. <!--b:recuar_cobertura,esperar-->
- **Strafe e atirar** — mover lateralmente para errar a mira dele — melhor quando estou confiante e o inimigo está longe; custa precisão. <!--b:strafe_espera-->
- **Fechar a distância** — correr para perto e matar de perto — melhor quando minha arma é melhor de perto (SMG, shotgun) ou ele troca tiros de longe; custa tomar dano na aproximação. <!--b:fechar_distancia,ir_correndo-->
- **Jogar granada na posição dele** — flash/HE/molotov e sair — melhor quando tenho tempo e cobertura; custa um segundo. <!--b:flash,he,molotov-->
- **Pedir ajuda e esperar o colega** — chamar e segurar para o colega trocar — melhor quando o colega está perto; custa tempo. <!--b:call,trocar-->
- **Sair e aparecer em outro lugar** — largar a troca de tiro e reaparecer em ângulo diferente — melhor quando ele está pré-mirado no meu lugar; custa o tempo de reposicionar. <!--b:mudar_angulo,dar_volta-->
- **Pular o peek** — contra AWP ou ângulo estreito — melhor quando ele tem AWP; custa precisão. <!--b:pular_peek-->
**O que pesa na escolha:** quem viu quem primeiro; vida minha e dele (se sei); arma minha e provável dele; distância; cobertura ao meu lado; colega perto e o que faz; granada; o relógio e a posição do objetivo.
**Erros de quem avalia mal:** troca tiro de longe parado até morrer; abre-toma-volta várias vezes; não percebe que o inimigo tem arma melhor; fica na linha sem cobertura.
**Cena:** Situação: o jogador com SMG vê um rifle a 30 m. / Esperado: ele não troca tiro de longe; recua, fecha a distância com cobertura ou muda de ângulo; se for trocar, é por vantagem real.

### S21. Sei (ou acho) que há inimigo atrás da quina
**Quando:** o jogador viu o inimigo entrar atrás de uma quina, ouviu passos ou recebeu call; vai passar por ali; tem ou não flash.
**Opções:**
- **Bangar e abrir** — jogar a flash por cima e entrar no estouro — melhor quando tenho flash e o inimigo provavelmente está lá há pouco tempo; custa a flash e um segundo e meio. <!--b:bangar_entrar,flash-->
- **Abrir seco** — peek largo sem apoio — melhor quando não há alternativa e o relógio aperta; custa a vida se ele está pré-mirado. <!--b:abrir_seco-->
- **Esperar o colega e abrir em dupla** — um pula, outro mata — melhor quando há colega perto; custa três segundos. <!--b:abrir_dupla,esperar-->
- **Dar a volta** — outro caminho para chegar ao alvo — melhor quando há rota e tempo; custa oito segundos ou mais. <!--b:dar_volta-->
- **Pré-fire/spray pela parede** — atirar na quina antes de abrir — melhor quando o som é nítido e o material atravessa; custa munição e posição. <!--b:prefire,spray_atraves-->
- **Granada (HE/molotov) na quina** — limpar o canto antes — melhor quando a crença é forte; custa a granada. <!--b:he,molotov-->
- **Jiggle/isca de ombro** — mostrar o ombro para ver se atiram — melhor contra AWP; custa um segundo. <!--b:jiggle,isca_ombro-->
- **Pezinho (boost)** — subir no colega e olhar a quina de cima, por um ângulo que o defensor não pré-mira — melhor quando o colega está parado ao lado, o ponto é combinado e o defensor espera na altura do chão; custa dois jogadores parados e fora da troca por um instante. <!--b:pezinho-->
- **Esperar ele vir** — segurar e deixar o inimigo decidir — melhor quando o tempo corre a meu favor; custa o tempo. <!--b:esperar,jogar_relogio-->
**O que pesa na escolha:** há quanto tempo foi a informação; se o inimigo se moveu (som); flash e colega perto; relógio; arma dele (AWP?); minha função.
**Erros de quem avalia mal:** trata a informação velha como atual e abre seco; não usa a flash que tem; abre e volta pelo mesmo ponto.
**Cena:** Situação: sabe de inimigo atrás da quina há 2 s e tem flash e tempo. / Esperado: ele bangar e entra correndo; sem flash e com colega perto, espera; sem tempo, abre seco.

### S22. Duelo parado de longe (troca de tiro à distância)
**Quando:** o jogador e o inimigo se veem de longe (SMG ou pistola contra rifle, ou os dois de rifle) e trocam tiros; a bala falha; ninguém avança.
**Opções:**
- **Fechar a distância com cobertura** — avançar por trás de caixas/quinas e chegar perto — melhor quando minha arma rende de perto ou o inimigo não vai fugir; custa a exposição na aproximação. <!--b:fechar_distancia-->
- **Largar o duelo e sair da linha** — recuar para a cobertura — melhor quando não vale a pena trocar nesta distância; custa o espaço. <!--b:recuar_cobertura-->
- **Mudar de ângulo e reaparecer perto** — sair e voltar por outro ponto mais próximo — melhor quando há ângulo; custa tempo. <!--b:mudar_angulo,dar_volta-->
- **Jogar granada para abrir a distância** — smoke/flash para entrar — melhor com flash e tempo; custa a granada. <!--b:flash,smoke-->
- **Pedir o colega** — chamar e deixar ele agir — melhor quando o colega pode flanquear; custa tempo. <!--b:call,cobrir_colega-->
- **Continuar o duelo com tap** — atirar pouco e certo — melhor quando minha arma é precisa de longe; custa mais tempo. <!--b:esperar-->
- **Ignorar e ir ao objetivo** — não duelar, seguir — melhor quando o objetivo é o C4 e o inimigo está fora da rota; custa a vida se ele atira nas costas. <!--b:largar_objetivo-->
**O que pesa na escolha:** a distância e a minha arma; se estou pré-mirado; a cobertura; o objetivo e o relógio; o que o colega faz; a vida.
**Erros de quem avalia mal:** troca tiro de SMG a 40 m até o tempo acabar; fica no lugar achando que vai ganhar; não percebe que o duelo não vale.
**Cena:** Situação: bot de SMG e inimigo de rifle trocando tiro de longe. / Esperado: em poucos segundos ele fecha a distância com cobertura, sai do duelo ou muda de ângulo; não fica trocando tiro.

### S23. Levei o primeiro tiro e estou ferido (abre e volta)
**Quando:** o jogador abriu uma linha, tomou dano e recuou; o inimigo sabe onde ele estava; o jogador está com pouca vida.
**Opções:**
- **Recuar e não reabrir** — esperar atrás da cobertura — melhor com vida baixa e sem apoio; custa a posição. <!--b:recuar_cobertura,esperar-->
- **Reabrir com apoio** — esperar o colega e entrar em dupla — melhor quando há colega perto; custa tempo. <!--b:abrir_dupla,trocar-->
- **Reabrir com granada** — flash/molotov e abrir — melhor com granada; custa a granada. <!--b:bangar_entrar-->
- **Mudar de ângulo e abrir de outro lugar** — surgir onde ele não espera — melhor quando ele está mirado no meu ponto; custa tempo. <!--b:mudar_angulo-->
- **Passar a informação e ceder o duelo ao colega** — chamar "um aqui com tal arma" e deixar o colega — melhor quando estou ferido; custa a vaga de ação. <!--b:call-->
- **Ir para outro objetivo** — largar e ir ao plant/defuse — melhor quando a vida é pouca e o objetivo é maior; custa a sombra do inimigo. <!--b:largar_objetivo-->
- **Entrar pulando (jump)** — tirar a mira e matar — melhor se sei a posição dele; custa a precisão. <!--b:pular_peek-->
**O que pesa na escolha:** minha vida e a dele (se sei); se ele sabe onde estou; colega perto; granada; relógio; a posição dele (quanto sei).
**Erros de quem avalia mal:** abre, toma um tiro, volta, abre, toma um tiro, volta até morrer; não muda nada entre as tentativas.
**Cena:** Situação: o jogador abriu duas vezes e tomou dano nas duas. / Esperado: na terceira tentativa muda alguma coisa (ângulo, granada, colega, ou desiste); não repete a mesma abertura.

### S24. Inimigo com AWP (visto, ouvido ou suspeito)
**Quando:** o jogador sabe (por call, por ter visto, ou por ter morrido ali) que há um AWP numa linha; precisa passar ou segurar por ela.
**Opções:**
- **Smoke na linha dele** — cortar a visão — melhor quando sei onde ele está; custa uma smoke. <!--b:smoke-->
- **Flash e abrir** — cegar e entrar — melhor com flash e tempo; custa a flash. <!--b:bangar_entrar,flash-->
- **Isca de ombro e swing** — mostrar o ombro, abrir depois — melhor contra AWP que atira no ombro; custa se ele não atira. <!--b:isca_ombro-->
- **Abrir em dupla por ângulos diferentes** — dois ao mesmo tempo, ele mata só um — melhor com colega; custa um jogador. <!--b:abrir_dupla-->
- **Pular a quina** — jump peek para estragar a mira — melhor sozinho; custa precisão. <!--b:pular_peek-->
- **Fechar a distância com molotov na posição dele** — tirar do ponto e chegar perto — melhor quando ele está perto o suficiente; custa a granada. <!--b:molotov,fechar_distancia-->
- **Mudar de caminho** — ir por outra rota — melhor quando o AWP cobre só uma; custa tempo. <!--b:dar_volta-->
- **Pezinho (boost)** — subir no colega para pegar o ângulo alto que o AWP não cobre — melhor quando conheço a posição e o colega está ao lado; custa o tempo de montar e dois jogadores expostos. <!--b:pezinho-->
- **Avançar logo depois do tiro** — aproveitar a janela do ferrolho — melhor quando ele atirou e não matou (ou matou o colega) e sei que está no ferrolho; custa se ele tem rifle de troca. <!--b:abrir_seco,fechar_distancia-->
- **Esperar ele errar ou sair** — não dar o peek — melhor quando o relógio está a meu favor; custa o tempo. <!--b:esperar,jogar_relogio-->
**O que pesa na escolha:** se tenho a posição do AWP (crença atualizada); quem morreu ali; flash/smoke; colega; relógio; se ele tem rifle de troca ao lado; o que ele já mostrou (atira no ombro?).
**Erros de quem avalia mal:** peeka a mesma linha seco depois de um colega morrer ali; ignora o AWP ouvido; abre sem usar a granada que tem.
**Cena:** Situação: colega morreu para AWP no meio. / Esperado: o próximo não abre a mesma linha seco; usa smoke, flash, isca, dupla ou outro caminho.

### S25. Dois ou mais inimigos ao mesmo tempo
**Quando:** o jogador vê ou ouve duas ou mais pessoas na mesma região, ou aparecem em ângulos diferentes; está sozinho ou com um colega.
**Opções:**
- **Recuar e puxar para o colega** — sair da linha e chamar o número — melhor quando estou sem troca; custa a posição. <!--b:recuar_cobertura,call-->
- **Granada no grupo** — HE/molotov/flash na posição — melhor quando estão juntos e tenho granada; custa a granada. <!--b:he,molotov,flash-->
- **Matar o primeiro e recuar** — um abate e sair — melhor com vantagem de mira; custa o espaço. <!--b:reposicionar,recuar_cobertura-->
- **Esperar o colega e abrir em dupla** — bater junto — melhor com colega a poucos segundos; custa tempo. <!--b:abrir_dupla,esperar-->
- **Mudar de ângulo para separar** — pegar um por vez em ângulos diferentes — melhor quando estão separados; custa tempo. <!--b:mudar_angulo-->
- **Spray no grupo** — descarregar na posição — melhor de perto com arma de cadência; custa a munição. <!--b:fechar_distancia-->
- **Usar a smoke para cortar um e matar o outro** — dividir o problema — melhor com smoke; custa a smoke. <!--b:smoke-->
**O que pesa na escolha:** quantos estão ali; se estão juntos ou espalhados; minha cobertura; colegas perto; granada; vida; o objetivo.
**Erros de quem avalia mal:** duela dois ao mesmo tempo no aberto; não chama o número.
**Cena:** Situação: dois TR no meio e um CT. / Esperado: ele chama, recua ou usa granada; não sai para duelar os dois.

### S26. Corpo a corpo e duelo de perto
**Quando:** o jogador e o inimigo estão a poucos metros (na quina, no bomb); o inimigo pode ter ouvido; ambos com armas na mão.
**Opções:**
- **Spray no peito/cabeça** — descarregar — melhor com arma de cadência; custa precisão. <!--b:fechar_distancia-->
- **Tap rápido na cabeça** — um tiro e esperar — melhor com rifle ou pistola precisa; custa se erra. <!--b:esperar-->
- **Strafe e atirar** — mover para errar a mira — melhor quando confiante; custa a mira. <!--b:strafe_espera-->
- **Recuar para a quina e esperar ele vir** — sair da linha — melhor quando ele está com vida baixa ou ouço passos; custa o espaço. <!--b:recuar_cobertura-->
- **Pular e atirar** — jump para tirar a mira — melhor contra AWP; custa precisão. <!--b:pular_peek-->
- **Granada de perto** — HE/molotov/flash no pé — melhor quando ele está atrás de cobertura; custa o risco de me atingir. <!--b:he,molotov-->
- **Faca/mudar arma** — trocar para a pistola ou faca para ganhar velocidade — melhor para chegar e fugir; custa o dano. <!--b:ir_correndo-->
**O que pesa na escolha:** vida; arma; ele me viu?; cobertura; colega; ruído.
**Erros de quem avalia mal:** fica parado em linha reta; recua demais; dá spray no ar.
**Cena:** Situação: o jogador está a 4 m de um inimigo na quina do bomb. / Esperado: ele não troca tiro parado; usa strafe, spray ou recua.

### S27. Tomei tiro (ou dano) sem ver quem
**Quando:** o jogador levou dano ou ouviu tiro dirigido a ele sem ver o atirador; pode ser de um ângulo longe, de trás da smoke, pela parede ou de AWP.
**Opções:**
- **Sair da linha de imediato** — recuar para fora da linha — melhor quando não sei de onde veio; custa a posição. <!--b:recuar_cobertura-->
- **Spray no suspeito** — atirar na provável posição — melhor quando a direção é clara (smoke, parede); custa munição e revela posição. <!--b:spray_atraves-->
- **Granada na provável posição** — HE/molotov/flash — melhor com granada e direção; custa a granada. <!--b:he,molotov,flash-->
- **Chamar a direção** — dizer de onde veio — melhor sempre; custa nada. <!--b:call-->
- **Mudar de ângulo** — sair e voltar de outro lugar — melhor quando o tiro vem de longe; custa tempo. <!--b:mudar_angulo-->
- **Fechar a distância pelo outro lado** — dar a volta e achar o atirador — melhor quando conheço o mapa; custa tempo. <!--b:dar_volta-->
- **Esperar o próximo tiro para triangular** — parar e ouvir — melhor se o atirador está fora de alcance; custa a segurança. <!--b:ouvir-->
**O que pesa na escolha:** vida; direção do tiro; material; colegas; granada; se há AWP.
**Erros de quem avalia mal:** fica parado na linha; não chama; atira para o lado errado.
**Cena:** Situação: o jogador toma tiro de AWP de longe. / Esperado: ele sai da linha, chama, e o time usa smoke ou muda o plano; não fica parado.

### S28. Acabei de matar alguém: e agora
**Quando:** o jogador abateu um inimigo; os demais podem ter ouvido o tiro e saber onde ele está.
**Opções:**
- **Sair da linha (reposicionar)** — mudar de ângulo logo após o abate — melhor quando fiz barulho e os colegas dele sabem onde estou; custa o ângulo bom. <!--b:reposicionar,mudar_angulo-->
- **Ficar e segurar o ângulo** — esperar o próximo — melhor quando ninguém sabe onde estou ou há cobertura; custa o risco de pré-mira. <!--b:esperar-->
- **Avançar e pegar o espaço** — aproveitar a vantagem — melhor quando o time está atrás e o inimigo está fraco; custa a exposição. <!--b:tomar_espaco-->
- **Chamar o abate** — dizer "matei um em X" — melhor sempre; custa nada. <!--b:call-->
- **Pegar a arma do morto** — pegar rifle/AWP melhor — melhor quando ela é melhor e o lugar é seguro; custa o tempo. <!--b:pegar_arma-->
- **Recarregar em segurança** — reload atrás da cobertura — melhor quando a munição é pouca; custa o ângulo. <!--b:recuar_cobertura-->
- **Esperar o colega** — reagrupar — melhor quando o abate abre espaço; custa a velocidade. <!--b:reagrupar,ficar_perto-->
**O que pesa na escolha:** quem sabe onde estou; o AWP deles; meu colega; o relógio; a vida; o objetivo; quantos faltam.
**Erros de quem avalia mal:** fica parado no mesmo ponto; vai atrás do abate sem cobertura; não chama.
**Cena:** Situação: o jogador abateu o entry e fez barulho. / Esperado: ele muda de posição ou recua para a cobertura; não fica no mesmo pixel.

### S29. Estou atrás de quem vai abrir (o segundo da dupla)
**Quando:** o colega está na frente e vai peekar, ou já peekou; o jogador está logo atrás, sem linha direta ou com linha lateral.
**Opções:**
- **Bangar para ele** — jogar flash por cima para cegar o inimigo — melhor quando o colega vai abrir; custa uma flash. <!--b:flash,bangar_entrar-->
- **Trocar** — preparar o tiro na linha dele e matar quem o matar — melhor sempre que há risco de queda; custa a posição. <!--b:trocar,ficar_perto-->
- **Cobrir outro ângulo** — olhar o que ele não cobre — melhor quando há dois ângulos; custa a troca direta. <!--b:cobrir_colega-->
- **Abrir junto** — dar o peek ao mesmo tempo, em ângulo diferente — melhor contra AWP ou entrada; custa a coordenação. <!--b:abrir_dupla-->
- **Jogar outra granada** — smoke/molotov para o colega entrar — melhor com granada; custa a granada. <!--b:smoke,molotov-->
- **Esperar o sinal** — aguardar a call — melhor quando o colega vai chamar; custa um segundo. <!--b:esperar_call,esperar-->
- **Recuar se ele cai** — sair da linha — melhor quando ele cai sem troca; custa a vingança. <!--b:recuar_cobertura-->
**O que pesa na escolha:** a tarefa do colega; minhas granadas; a distância ao colega; a linha de visão; o inimigo conhecido; o relógio.
**Erros de quem avalia mal:** fica atrás sem fazer nada; joga a flash tarde; olha o mesmo ângulo que o colega.
**Cena:** Situação: dois CT esperando atrás de caixa, um prestes a abrir. / Esperado: o de trás joga a flash ou fica na posição de troca; não fica olhando a parede.

### S30. A flash me pegou (estou cego)
**Quando:** o jogador foi atingido por flash (ou fica sem visão) e sabe por quanto tempo; pode acreditar que há inimigo chegando ou não.
**Opções:**
- **Sair para a cobertura** — recuar ou andar até um ponto protegido — melhor quando acredito que alguém vem; custa o tempo cego. <!--b:recuar_cobertura-->
- **Ficar parado** — não se mover — melhor quando entendo que foi só uma bang e ninguém vem; custa se alguém vem. <!--b:esperar-->
- **Atirar na provável posição** — spray em direção ao inimigo — melhor quando sei de onde ele entra; custa a munição e a posição. <!--b:spray_atraves-->
- **Chamar o time** — avisar "estou cego" — melhor sempre; custa nada. <!--b:call-->
- **Virar de costas e esperar** — olhar para outro lado — melhor para encurtar a cegueira; custa a visão. <!--b:esperar-->
- **Sair para outro ângulo** — mudar de ponto — melhor quando o inimigo sabe onde estou; custa a pré-mira. <!--b:mudar_angulo-->
- **Pedir cobertura ao colega** — o colega cobre meu ângulo — melhor com colega perto; custa a atenção dele. <!--b:cobrir_colega,call-->
**O que pesa na escolha:** se acredito que alguém chega (crença); quanto tempo de cegueira; a cobertura mais próxima; colegas; o que ouvi; a arma.
**Erros de quem avalia mal:** fica parado quando o inimigo vem; foge quando não precisava; não avisa.
**Cena:** Situação: o jogador tomou flash e acredita que o TR está entrando. / Esperado: ele usa o que sabe do mapa e procura cobertura; se acha que ninguém vem, fica.

### S31. Minha arma é pior que a deles (SMG/pistola contra rifle/AWP)
**Quando:** o jogador está de pistola/SMG (forçada, eco, anti-eco) e o inimigo tem rifle; ele sabe disso pela call ou por tê-lo visto.
**Opções:**
- **Evitar o duelo de longe** — não dar linha longa — melhor sempre que a arma não rende; custa o espaço. <!--b:recuar_cobertura-->
- **Fechar a distância com cobertura** — chegar perto — melhor porque a arma rende de perto; custa a exposição. <!--b:fechar_distancia-->
- **Esperar o inimigo vir** — segurar de quina — melhor com o relógio a meu favor; custa o tempo. <!--b:esperar-->
- **Jogar em dupla/pack** — ficar perto de colegas — melhor em eco; custa a posição individual. <!--b:ficar_perto,reagrupar-->
- **Pegar a arma melhor do chão** — pegar rifle caído — melhor quando o lugar é seguro; custa o tempo. <!--b:pegar_arma-->
- **Usar a granada para compensar** — flash/molotov na posição — melhor com granada; custa a granada. <!--b:flash,molotov-->
- **Fingir/isca** — desviar a atenção e matar de perto — melhor em dupla; custa o isca. <!--b:isca_troca-->
**O que pesa na escolha:** a arma dele; a distância típica do lugar; colegas; o objetivo (eco quer plantar/roubar arma); o relógio; vida.
**Erros de quem avalia mal:** troca tiro de longe com SMG; entra sozinho; não pega a arma caída.
**Cena:** Situação: bot de pistola e inimigo de rifle numa linha longa. / Esperado: ele não troca tiro; sai da linha, fecha a distância ou espera de perto.

### Grupo: Depois de uma baixa

### S32. Meu colega morreu e eu estava perto
**Quando:** o jogador ouviu ou viu o colega cair a poucos metros; sabe de onde veio o tiro (ou acha); pode trocar.
**Opções:**
- **Trocar imediatamente** — mirar na linha de quem matou e abrir em segundos — melhor quando estou a ≤2 s e tenho vantagem; custa a vida se ele está pronto. <!--b:trocar,abrir_seco-->
- **Trocar com flash** — jogar flash e abrir — melhor com flash; custa um segundo. <!--b:bangar_entrar,trocar-->
- **Segurar a linha de quem matou** — esperar ele vir — melhor quando ele tende a empurrar; custa o espaço. <!--b:segurar_linha,esperar-->
- **Recuar e jogar granada** — sair e arremessar na posição — melhor quando a troca falhou; custa o espaço. <!--b:recuar_cobertura,he,molotov-->
- **Recuar para o bomb e esperar o retake** — largar o lugar — melhor quando o grupo está fraco; custa o espaço. <!--b:recuar_cobertura-->
- **Mudar de ângulo** — sair e voltar por outro lado — melhor quando o inimigo sabe onde estou; custa tempo. <!--b:mudar_angulo-->
- **Chamar o que ouvi** — dizer onde caiu e quantos — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** a distância do colega; o tempo desde a queda; vida; se o inimigo é um ou mais; granada; o objetivo; o relógio.
**Erros de quem avalia mal:** troca depois de 5 s (já virou duelo novo); fica olhando para outro lado; abre a mesma linha seco.
**Cena:** Situação: o colega cai a 5 m e o jogador tem flash. / Esperado: ele bangar e troca em 2 s, ou recua com motivo; não fica parado.

### S33. Meu colega morreu e eu estava longe
**Quando:** o jogador recebe a call ou ouve o tiro que mata o colega em outro lugar; não pode trocar.
**Opções:**
- **Segurar minha posição** — manter o que cubro — melhor quando a queda é longe e o inimigo não vem a mim; custa a ajuda. <!--b:esperar-->
- **Rotacionar para ajudar** — ir ao lado do morto — melhor quando o lado vai cair; custa o meu lado. <!--b:rotacionar-->
- **Recuar para o bomb/base** — formar uma defesa — melhor quando perdi o primeiro e o grupo precisa se juntar; custa o espaço. <!--b:recuar_cobertura,reagrupar-->
- **Jogar granada na entrada** — atrasar o avanço — melhor quando sei o caminho do inimigo; custa a granada. <!--b:smoke,molotov,he-->
- **Dar a volta pelo flanco** — ir pelo lado do TR — melhor quando o TR já está comprometido no lado do morto; custa tempo. <!--b:dar_volta-->
- **Mudar de ângulo** — reposicionar para uma linha mais útil — melhor quando a queda mostra onde o inimigo está; custa a posição. <!--b:mudar_angulo-->
- **Avisar o que sei** — dizer "está lá" — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** onde o colega caiu (informação de que há inimigo ali); quantos estão perto; o tempo de rotação; meu bomb; o relógio; o IGL.
**Erros de quem avalia mal:** todos correm ao morto; ninguém muda nada; fica olhando para outro lado sabendo que há inimigo ali.
**Cena:** Situação: o colega morre no B e o jogador está no A. / Esperado: ele escolhe uma saída (segurar, recuar, jogar granada na entrada ou mudar de ângulo) e faz; não fica sem reagir.

### S34. O entry TR caiu na entrada sem troca
**Quando:** o primeiro a entrar morreu e os colegas atrás viram ou ouviram; o inimigo está pronto na linha que matou.
**Opções:**
- **Não reabrir a mesma linha seco** — recuar ou esperar — melhor porque o inimigo está pré-mirado; custa o tempo. <!--b:recuar_cobertura,esperar-->
- **Granada na posição de quem matou** — flash/molotov/HE — melhor com granada; custa a granada. <!--b:flash,molotov,he-->
- **Trocar de entrada** — outra entrada do bomb — melhor quando há rota; custa o tempo. <!--b:dar_volta-->
- **Abrir em dupla** — dois ao mesmo tempo — melhor com colega perto; custa um jogador. <!--b:abrir_dupla-->
- **Fingir** — barulho para puxar o CT — melhor quando o CT é paciente; custa o silêncio. <!--b:fingir-->
- **Trocar o plano** — virar para o outro bomb — melhor quando perdi a entrada e o outro está vazio; custa o tempo. <!--b:virar_round-->
- **Esperar a smoke acabar** — deixar passar — melhor se o CT gastou granada; custa o relógio. <!--b:esperar_smoke-->
**O que pesa na escolha:** quanto tempo e quantos CT; granadas; quem ainda tem flash; o relógio; o placar de vivos; o que o CT mostrou.
**Erros de quem avalia mal:** manda o segundo para a mesma linha; entra um a um; não percebe que o CT está na linha.
**Cena:** Situação: o entry morre na entrada A e o CT está pronto. / Esperado: o segundo não abre seco; usa granada, outra entrada ou muda o plano.

### S35. Morri: o que o morto faz
**Quando:** o jogador morreu; vê os colegas pela câmera do time; sabe quem o matou, onde e com que arma.
**Opções:**
- **Dar a informação primeiro** — chamar "um AWP em X, matou de Y" — melhor sempre; custa nada. <!--b:call-->
- **Ficar em silêncio** — não falar mais — melhor em clutch, para não atrapalhar; custa a informação extra. <!--b:esconder-->
- **Avisar quem morreu e onde** — chamar a contagem — melhor para evitar que o colega avance achando que há mais gente; custa nada. <!--b:call-->
- **Ajudar o IGL** — passar leitura (o que vi, o que acho) — melhor quando o IGL morreu ou está ocupado; custa a atenção. <!--b:call-->
- **Dropar a arma ou deixar granada** — o morto já larga a arma ao morrer, mas posso escolher onde — melhor quando morro perto do colega; custa nada. <!--b:pegar_arma-->
- **Não falar de coisa irrelevante** — calar quando o colega está em duelo — melhor em clutch; custa nada. <!--b:esconder-->
**O que pesa na escolha:** a importância da informação; se o time está em clutch; o que o colega precisa; o IGL vivo?
**Erros de quem avalia mal:** não diz quem matou nem de onde; fala muito e abafa o som do passo.
**Cena:** Situação: o jogador morre para AWP no meio. / Esperado: ele chama a posição e a arma; o time muda de plano com base nisso.

### S36. Uma arma caiu no chão (AWP, rifle, kit) e eu vejo ou sei onde está
**Quando:** um jogador morreu e a arma dele (ou a dele e o kit) ficou no chão; o jogador viu cair, ouviu a call ou passa por ali.
**Opções:**
- **Pegar e trocar pela minha** — apanhar a arma melhor e largar a pior — melhor quando o lugar está seguro e a minha arma é claramente inferior (Mac-10 contra AWP caída); custa uns dois segundos parado e a minha arma antiga. <!--b:pegar_arma-->
- **Pegar e entregar a quem tem a função** — apanhar a AWP e dropar para o AWPer, ou o kit para o CT que vai desarmar — melhor quando o colega está perto e sem arma adequada; custa o tempo e deixar minha mão ocupada. <!--b:pegar_arma,call-->
- **Vigiar a arma com linha sobre ela** — segurar o ângulo e deixar o inimigo vir buscá-la — melhor quando sei que o inimigo vai querer a AWP e há tempo; custa ficar preso ali. <!--b:vigiar_c4,esperar-->
- **Usar a arma como isca** — deixá-la onde está e esperar quem se expõe para pegá-la — melhor quando tenho colega em cruzado; custa a arma se o inimigo chega primeiro. <!--b:isca_troca,cruzado-->
- **Pedir ao colega que pegue** — chamar "pega a AWP em X" — melhor quando estou longe, ferido ou com tarefa maior; custa a atenção dele. <!--b:call,pegar_arma-->
- **Pegar depois do objetivo** — seguir o plano e voltar à arma quando o lugar estiver livre — melhor quando o relógio manda; custa correr o risco de ninguém pegar. <!--b:largar_objetivo-->
- **Ignorar** — a minha arma serve e a caída fica — melhor quando a diferença é pequena e a arma está em área exposta; custa dinheiro parado no chão. <!--b:esperar-->
**O que pesa na escolha:** minha arma e a do morto; se o lugar tem inimigo ou linha de tiro; função (AWPer deve ficar com a AWP); economia do time; relógio; se o colega que precisa está vivo e perto; se o time vai perder o round (arma no chão sem dono é dinheiro perdido).
**Erros de quem avalia mal:** ninguém pega a AWP e o último TR termina de Mac-10; pega no meio da linha do inimigo e morre com duas armas; o AWPer compra outra AWP em vez de pegar a que está no chão.
**Cena:** Situação: sobrou um TR de Mac-10 e há uma AWP caída a poucos metros, sem inimigo à vista. / Esperado: ele pega a AWP antes de seguir; se há inimigo na linha, vigia ou espera com motivo.

### S37. O IGL caiu no meio do round: quem decide
**Quando:** o IGL morreu (ou ficou sem canal) com o round ainda em andamento; o jogador percebe que a call parou de chegar.
**Opções:**
- **Assumir a call** — quem tem melhor leitura passa a chamar — melhor quando eu vi mais que os outros ou sou quem sobrou com visão do quadro; custa a minha atenção no duelo. <!--b:call-->
- **Seguir o plano que já estava dado** — continuar a jogada e a tarefa — melhor quando o plano está em curso e nada mudou; custa a adaptação se algo mudar. <!--b:esperar_call-->
- **Cada um joga pela informação que tem** — autonomia dentro da jogada — melhor em time com disciplina e jogada simples; custa a coordenação. <!--b:esperar-->
- **Ouvir o IGL morto** — ele ainda vê o time pela câmera e pode dar a leitura — melhor sempre que ele tem o que dizer; custa o canal ocupado. <!--b:esperar_call-->
- **Reagrupar e esperar uma decisão** — juntar o grupo e escolher junto — melhor quando o plano dependia dele; custa o relógio. <!--b:reagrupar-->
- **Simplificar a jogada** — trocar a execução complexa por algo que os quatro entendam (rush, segurar) — melhor com pouco tempo; custa poder da jogada. <!--b:virar_round-->
**O que pesa na escolha:** em que fase da jogada o IGL caiu; o que o jogador sabe do plano; a complexidade da jogada; o relógio; quem lê melhor (atributo, não função); o canal de voz.
**Erros de quem avalia mal:** ninguém chama e cada um faz outra coisa; todos esperam a call que não vem; um jogador desenha um plano novo sem avisar.
**Cena:** Situação: o IGL morre em plena execução de A. / Esperado: alguém assume a call ou o plano continua como estava; ninguém fica parado esperando.

### Grupo: Execução e entrada no bomb (TR) e defesa da entrada (CT)

### S38. TR: execução ensaiada, esperar o go e cumprir a minha granada
**Quando:** a call é uma execução com tarefas (quem joga smoke, molotov, flash, quem entra); o time está nas posições de saída; falta o "go" (ou o relógio).
**Opções:**
- **Jogar a minha granada no tempo** — lançar no lineup no instante combinado — melhor sempre que sei o lineup e o tempo; custa a exposição ao lançar. <!--b:smoke,molotov,flash-->
- **Esperar o go na posição de saída** — segurar sem se mostrar — melhor para sincronizar; custa a espera. <!--b:esperar-->
- **Jogar com improviso** — lançar de outro ponto porque o lineup está bloqueado — melhor quando o ponto normal está queimado; custa a precisão. <!--b:smoke,mudar_angulo-->
- **Cancelar a minha granada** — não jogar porque a informação mudou — melhor quando o CT já gastou/saiu; custa a coordenação. <!--b:esperar-->
- **Pedir ao colega para jogar no meu lugar** — passar a tarefa — melhor quando fui visto ou estou sem linha; custa o aviso. <!--b:pedir_granada,call-->
- **Antecipar a granada** — jogar mais cedo — melhor quando o CT está saindo; custa a sincronia. <!--b:smoke,flash-->
- **Entrar atrás da granada** — seguir a smoke/flash para dentro — melhor depois de jogar; custa a segunda função. <!--b:bangar_entrar-->
**O que pesa na escolha:** a tarefa dada; o tempo de cada granada (smoke abre, fogo dura menos); o que o CT mostrou; a informação nova (CT saiu do bomb); meu próprio erro (lineup errou?).
**Erros de quem avalia mal:** joga a granada fora do tempo; joga e se expõe; não avisa que a granada falhou e o time entra sem ela.
**Cena:** Situação: execução em A com smoke do jogador X. / Esperado: a smoke abre antes do primeiro entrar; se errou, avisa; o time não entra sem a corte da linha.

### S39. TR: entrando no bomb e há quina com possível defensor
**Quando:** o jogador está dentro do bomb ou na entrada; a quina à frente pode ter CT (ouvi, vi recentemente, ou é posição típica).
**Opções:**
- **Bangar e entrar** — flash por cima e entrar no estouro — melhor com flash e CT provavelmente ali; custa a flash. <!--b:bangar_entrar,flash-->
- **Abrir seco** — peek largo sem apoio — melhor quando o relógio aperta ou o CT já foi cegado; custa a vida. <!--b:abrir_seco-->
- **Abrir em dupla** — dois peekando em ângulos diferentes — melhor com colega a ≤1 s; custa um jogador no pior caso. <!--b:abrir_dupla-->
- **Jogar molotov/HE na quina** — limpar o canto — melhor quando o canto está fechado e perto; custa a granada. <!--b:molotov,he-->
- **Spray pela parede** — atirar através do material — melhor se o som é nítido; custa munição. <!--b:spray_atraves-->
- **Pré-fire** — atirar antes de ver — melhor quando a crença é forte; custa a posição. <!--b:prefire-->
- **Esperar ele vir** — segurar fora da linha — melhor quando o tempo me favorece; custa o relógio. <!--b:esperar-->
- **Jiggle para tirar a granada** — espiar para forçar o CT a gastar — melhor com muitas granadas; custa um segundo. <!--b:jiggle-->
- **Pezinho (boost) sobre a quina** — um sobe no outro e olha por cima do canto — melhor quando o defensor segura a altura do chão e o par está combinado; custa os dois parados e o barulho. <!--b:pezinho-->
- **Fingir a entrada** — barulho para puxar o CT — melhor com colega pronto a trocar; custa o silêncio. <!--b:fingir-->
**O que pesa na escolha:** onde acredito que ele está; há quanto tempo; minha flash; colega a trocar; relógio; a arma dele; o que foi visto.
**Erros de quem avalia mal:** abre seco contra uma linha pré-mirada; entra sem usar a flash; entra um a um.
**Cena:** Situação: TR com flash na mão e CT visto atrás da quina há 2 s. / Esperado: ele bangar e entra correndo; não espera.

### S40. TR: sem flash e sem tempo para a entrada
**Quando:** o jogador é o entry mas gastou ou perdeu a flash; o time espera; o relógio corre.
**Opções:**
- **Pedir flash ao colega** — chamar e esperar — melhor quando o colega tem e está perto; custa 2–3 s. <!--b:pedir_granada,esperar-->
- **Abrir em dupla** — com o colega ao lado — melhor quando o colega está a ≤1 s; custa a coordenação. <!--b:abrir_dupla-->
- **Pular o peek** — jump peek — melhor contra AWP; custa precisão. <!--b:pular_peek-->
- **Jogar a smoke/molotov para criar cobertura** — usar outra granada — melhor se tenho; custa a granada. <!--b:smoke,molotov-->
- **Entrar correndo (rush)** — aproveitar o impulso — melhor quando o CT está fraco; custa a vida. <!--b:ir_correndo-->
- **Trocar de entrada** — outra entrada — melhor quando há rota; custa o tempo. <!--b:dar_volta-->
- **Passar o papel ao colega** — deixar outro entrar — melhor se ele tem a granada; custa o aviso. <!--b:call,trocar-->
**O que pesa na escolha:** o que o colega tem; o relógio; o CT visto; a distância; a função.
**Erros de quem avalia mal:** entra seco por impaciência; espera demais e perde o tempo.
**Cena:** Situação: entry sem flash, suporte com duas. / Esperado: pede a flash e entra no estouro; ou o suporte entra.

### S41. TR: split e segundo grupo
**Quando:** a call é um split; o grupo A entra por uma entrada e o B por outra; os dois precisam chegar juntos.
**Opções:**
- **Esperar o go do outro grupo** — segurar até o sinal — melhor para sincronizar; custa a espera. <!--b:esperar,esperar_call-->
- **Entrar pelo tempo (contagem)** — contar e entrar — melhor com cronometragem; custa o erro de cálculo. <!--b:ir_correndo-->
- **Entrar quando o primeiro estoura** — usar a primeira granada como sinal — melhor com granada; custa depender dela. <!--b:esperar_smoke-->
- **Ajustar o tempo pela informação** — se o grupo B foi visto, antecipar ou segurar — melhor quando o CT reagiu; custa a coordenação. <!--b:esperar_call-->
- **Cancelar o split e juntar** — virar um grupo só — melhor quando um grupo foi parado; custa a surpresa. <!--b:reagrupar-->
- **Mudar a entrada** — ir pela entrada que está livre — melhor quando uma está fechada; custa tempo. <!--b:dar_volta-->
- **Chamar a posição ao outro grupo** — dizer o que vejo — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** quem chega primeiro; o que o CT mostrou nas duas entradas; o tempo de cada grupo; granadas; o relógio.
**Erros de quem avalia mal:** chega separado e dá ao CT duas lutas vencíveis; não ouve a call.
**Cena:** Situação: split em B com dois grupos. / Esperado: os dois chegam com ≤2 s de diferença; se um é parado, o outro não entra sozinho.

### S42. TR: o meu papel num fake
**Quando:** a call manda um jogador fazer barulho e gastar granada num lado, e o resto ir ao outro; o jogador sabe quanto tem de custo para ser crível.
**Opções:**
- **Fazer o barulho e voltar** — jogar a granada e recuar — melhor para puxar a rotação; custa a presença. <!--b:fingir,recuar_cobertura-->
- **Fazer o barulho e esperar a rotação** — ouvir se o CT gira — melhor para decidir se vira; custa o tempo. <!--b:fingir,ouvir-->
- **Converter o fake em ataque** — entrar de verdade se o CT não reagiu — melhor se o CT ficou; custa a coordenação. <!--b:virar_round,abrir_seco-->
- **Ir ao outro lado** — juntar com o time — melhor depois do barulho; custa o fake. <!--b:rotacionar-->
- **Fazer o fake do fake** — mostrar que saiu e voltar — melhor contra CT que lê o fake; custa o tempo. <!--b:fingir,rotacionar-->
- **Gastar granada cara** — jogar uma granada de execução de verdade — melhor para ser crível; custa a granada. <!--b:smoke,molotov-->
- **Chamar o que o CT fez** — dizer se giraram — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** o CT gira por som?; o controle de mapa; quantas granadas sobram; o relógio; a repetição do fake.
**Erros de quem avalia mal:** fake barato é ignorado; fake repetido é lido; esquece de avisar se o CT girou.
**Cena:** Situação: fake em A e execução em B. / Esperado: o fake tem granada e presença; o time só entra no B depois do sinal de rotação.

### S43. TR: a execução travou, o CT segura
**Quando:** o time tentou entrar, perdeu um ou dois, não pisou no bomb; o CT mostra firmeza; o relógio corre.
**Opções:**
- **Recuar ao espaço tomado e reiniciar** — voltar ao default — melhor com tempo; custa o relógio. <!--b:recuar_cobertura,reagrupar-->
- **Mudar de bomb** — girar para o outro lado — melhor se o outro está fraco; custa o tempo de rotação. <!--b:virar_round,rotacionar-->
- **Insistir com mais granadas** — gastar o que sobra e entrar — melhor com granadas e tempo; custa a vida. <!--b:smoke,molotov,flash-->
- **Fingir uma retirada** — recuar para atrair o CT — melhor para puni-lo; custa o tempo. <!--b:fingir,recuar_cobertura-->
- **Esperar a smoke deles acabar** — deixar passar — melhor se eles gastaram; custa o relógio. <!--b:esperar_smoke-->
- **Dar a volta/lurk** — outro ângulo — melhor com o lurker no lugar; custa tempo. <!--b:dar_volta-->
- **Salvar** — recuar e guardar armas — melhor sem chance; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** baixas; granadas; relógio; onde o CT está; o placar de vivos.
**Erros de quem avalia mal:** insiste na mesma entrada; entra um a um; perde o tempo sem decidir.
**Cena:** Situação: 2 baixas na entrada A, 50 s no relógio. / Esperado: o time muda de plano, recua ou gira; não repete a mesma entrada.

### S44. CT: defender a entrada contra uma execução (smoke e flash do TR)
**Quando:** o TR joga granadas na entrada (smoke, flash, molotov); o jogador ouve a execução e vê parte dela; segura um ângulo.
**Opções:**
- **Recuar para a posição de segunda linha** — sair do ponto queimado — melhor se a granada o pega; custa o ângulo. <!--b:recuar_cobertura-->
- **Ficar e atirar através da smoke** — spray na borda — melhor se acho que eles entram por ali; custa a posição. <!--b:spray_atraves-->
- **Contra-flash** — jogar flash na entrada antes de eles entrarem — melhor com flash; custa uma flash. <!--b:flash-->
- **Molotov na entrada** — atrasar — melhor para negar chão; custa uma granada. <!--b:molotov-->
- **Pedir a ajuda do rotador** — chamar o rotador — melhor quando a execução é real; custa tempo. <!--b:call,rotacionar-->
- **Mudar de ângulo** — sair do ponto pré-mirado — melhor quando a granada mostra onde estou; custa a pré-mira. <!--b:mudar_angulo-->
- **Esperar a entrada e punir** — segurar e matar o primeiro — melhor com cruzado; custa a exposição. <!--b:esperar,cruzado-->
- **Largar o bomb e ir ao retake** — recuar para o retake — melhor se a execução vence; custa o bomb. <!--b:recuar_cobertura,reagrupar-->
**O que pesa na escolha:** quantas granadas o TR gastou; quantos acredito que são; meus colegas; granadas minhas; o tempo de rotação; o C4.
**Erros de quem avalia mal:** fica no ponto queimado e morre; gasta tudo antes de ver a entrada; não chama.
**Cena:** Situação: smoke e molotov caem no ponto do CT. / Esperado: ele muda de ponto ou recua; não fica no fogo.

### S45. CT: âncora sozinho com três ou mais TR chegando
**Quando:** o âncora vê ou ouve três ou mais TR indo ao bomb; a ajuda vem em segundos; o jogador está sozinho.
**Opções:**
- **Atrasar com granada** — molotov/smoke na entrada e recuar — melhor para ganhar tempo; custa a granada. <!--b:molotov,smoke-->
- **Um e recua** — pegar o primeiro e recuar — melhor para dano e vida; custa o bomb. <!--b:reposicionar,recuar_cobertura-->
- **Entregar o bomb e esperar o retake** — recuar vivo — melhor quando são muitos; custa o plant. <!--b:recuar_cobertura,guardar_arma-->
- **Segurar até a ajuda** — manter o ângulo — melhor se a ajuda chega em segundos; custa a vida. <!--b:esperar-->
- **Chamar o número e a arma** — informar — melhor sempre; custa nada. <!--b:call-->
- **Pedir flash ao rotador** — chegar com apoio — melhor se há tempo; custa o tempo. <!--b:pedir_granada-->
- **Mudar de posição** — pegar um ângulo que eles não limparam — melhor depois de um tiro; custa o ângulo. <!--b:mudar_angulo-->
**O que pesa na escolha:** número; tempo de rotação; minha arma; granadas; o que posso causar; o ponto de retake.
**Erros de quem avalia mal:** faz herói e cai cedo; não chama; entrega sem atrasar.
**Cena:** Situação: âncora vê 4 TR. / Esperado: ele atrasa com granada e/ou um abate e recua vivo, e chama; não morre na primeira troca.

### S46. CT: o rotador chega ao bomb pressionado
**Quando:** o jogador vem de outro bomb; o TR já está na entrada; o jogador chega pela base/selva/mercado.
**Opções:**
- **Entrar em dupla com quem está lá** — combinar com o âncora — melhor com âncora vivo; custa o tempo. <!--b:abrir_dupla,trocar-->
- **Chegar atrás e flanquear** — pegar o TR por trás — melhor se ele está de costas; custa tempo e risco. <!--b:dar_volta-->
- **Esperar o primeiro e entrar na troca** — ficar a poucos metros — melhor quando o âncora cai; custa o espaço. <!--b:ficar_perto,trocar-->
- **Jogar granada na entrada** — smoke/flash/HE ao chegar — melhor com granada; custa a granada. <!--b:smoke,flash,he-->
- **Segurar o bomb de fora** — ficar na rota de retake — melhor se o bomb já caiu; custa o plant. <!--b:segurar_linha-->
- **Reagrupar com outros rotadores** — juntar antes — melhor quando há mais de um; custa o tempo. <!--b:reagrupar-->
- **Chamar o que vi** — informar — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** o que sobrou do bomb; o C4; quantos TR; granadas; tempo; meus colegas.
**Erros de quem avalia mal:** chega sozinho e cai sem troca; chega junto e morre junto; chega sem informar.
**Cena:** Situação: rotador chega ao B com 3 TR na entrada. / Esperado: ele chega pela base, não sozinho na linha, e combina com quem está lá.

### S47. CT: o TR rusha e eu estou longe
**Quando:** o TR vai ao bomb em rush cedo; o jogador está longe do bomb atacado.
**Opções:**
- **Rotacionar na hora** — correr ao bomb — melhor se o rush é real; custa o meu lado. <!--b:rotacionar,ir_correndo-->
- **Segurar minha posição** — manter e esperar o fake — melhor se o rush pode ser fake; custa o outro bomb. <!--b:esperar-->
- **Jogar granada de atraso** — smoke/molotov na rota — melhor para ganhar tempo; custa a granada. <!--b:smoke,molotov-->
- **Preparar o retake** — ir à posição de retake — melhor se o bomb cai; custa o bomb. <!--b:rotacionar,reagrupar-->
- **Surpreender por trás** — ir ao flanco — melhor com o TR comprometido; custa o tempo. <!--b:dar_volta-->
- **Pedir informação ao âncora** — "quantos?" — melhor se o âncora vê; custa o tempo. <!--b:esperar_call-->
- **Chamar o time** — avisar o rush — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** quantos TR; o tempo de rotação; a economia do TR; o âncora; granadas.
**Erros de quem avalia mal:** chega tarde; não reage; todos giram.
**Cena:** Situação: rush de 5 no B e o jogador do A está na selva. / Esperado: ele gira pela base e chega ao B em 6–17 s com apoio.

### Grupo: Informação nova (som, tiro, recarga, plant, defuse, granada, call)

### S48. Ouvi passos
**Quando:** o jogador ouve passos de inimigo (direção e distância aproximadas); não vê ninguém.
**Opções:**
- **Parar e ouvir mais** — ficar quieto para triangular — melhor quando os passos estão perto e posso ser surpreendido; custa o tempo. <!--b:ouvir,esperar-->
- **Pré-fire no ponto provável** — atirar antes de ver — melhor se a crença é forte; custa a posição. <!--b:prefire-->
- **Esperar ele aparecer** — segurar o ângulo — melhor quando a direção é clara; custa o tempo. <!--b:esperar-->
- **Jogar HE/molotov na direção** — granada no som — melhor se está parado; custa a granada. <!--b:he,molotov-->
- **Chamar o som** — dizer "passos em X" — melhor sempre; custa nada. <!--b:call-->
- **Mudar de ângulo** — sair do ponto antes de ele chegar — melhor se me ouviu; custa o ponto. <!--b:mudar_angulo-->
- **Ir atrás em silêncio** — shift e achá-lo — melhor se estou na vantagem; custa a posição. <!--b:ir_silencio,fechar_distancia-->
- **Ignorar** — passos de colega ou distante — melhor quando é outro lugar; custa o risco. <!--b:esperar-->
**O que pesa na escolha:** a distância e a direção; se o inimigo andava de shift (silencioso); meus colegas; minha arma; o relógio; o que acredito (um ou mais).
**Erros de quem avalia mal:** ouve e não faz nada; atira sem confirmar; não chama.
**Cena:** Situação: o jogador ouve passos no meio. / Esperado: ele chama, ajusta o ângulo ou usa granada; não ignora.

### S49. Ouvi tiro (de longe, de colega ou inimigo)
**Quando:** o jogador ouve tiros e identifica (colega ou inimigo, direção, arma).
**Opções:**
- **Ir ajudar (trocar)** — se o colega está em duelo perto — melhor com colega perto; custa a posição. <!--b:trocar,rotacionar-->
- **Chamar e manter** — avisar e esperar — melhor quando longe; custa a ajuda. <!--b:call,esperar-->
- **Mudar de ângulo** — reposicionar para onde o duelo vai — melhor se já sei onde; custa tempo. <!--b:mudar_angulo-->
- **Jogar granada** — HE/flash na posição — melhor com granada e posição; custa a granada. <!--b:he,flash-->
- **Spray através da parede** — atirar no som — melhor se perto; custa munição. <!--b:spray_atraves-->
- **Usar o tiro para atualizar a crença** — marcar o lugar — melhor sempre; custa nada. <!--b:esperar_call-->
- **Ignorar** — tiro distante — melhor se não importa; custa o risco. <!--b:esperar-->
**O que pesa na escolha:** de quem é o tiro; a distância; o duelo que vi; o objetivo; colegas; o relógio.
**Erros de quem avalia mal:** corre para o tiro sem saber de quem; ignora o sinal.
**Cena:** Situação: ouve tiros de AWP no meio. / Esperado: atualiza a crença de que há AWP lá e muda o plano.

### S50. Ouvi um inimigo recarregando
**Quando:** o jogador ouve recarga de inimigo (som distinto); sabe que ele está vulnerável por alguns segundos.
**Opções:**
- **Abrir agora** — aproveitar a janela — melhor quando estou perto e pronto; custa a vida se ele já terminou. <!--b:abrir_seco,fechar_distancia-->
- **Bangar e entrar** — flash e entrar — melhor com flash e tempo; custa a flash. <!--b:bangar_entrar-->
- **Chamar o time** — avisar "recarga em X" — melhor para o colega aproveitar; custa nada. <!--b:call-->
- **Esperar mais** — ouvir se é só ele — melhor se acho que há mais gente; custa a janela. <!--b:ouvir,esperar-->
- **Pré-fire** — atirar no ponto — melhor com crença; custa a posição. <!--b:prefire-->
- **Ir em silêncio até perto** — chegar e abrir — melhor se ele não me ouve; custa tempo. <!--b:ir_silencio,fechar_distancia-->
- **Ignorar** — recarga longe — melhor se o objetivo é outro; custa a oportunidade. <!--b:largar_objetivo-->
**O que pesa na escolha:** a distância; se há outros inimigos; flash; o colega; minha arma; o tempo da recarga.
**Erros de quem avalia mal:** chega depois da recarga; abre sem considerar o colega dele.
**Cena:** Situação: ouve recarga a 8 m. / Esperado: ele avança agora (com flash se tem) ou chama; não ignora.

### S51. Ouvi o plant
**Quando:** o jogador (CT) ouve o som do plant; sabe o bomb pelo som; começa o relógio de 40 s.
**Opções:**
- **Rotacionar para o retake** — ir ao bomb — melhor sempre que posso; custa o lado. <!--b:rotacionar-->
- **Reunir o time** — juntar antes de entrar — melhor se são vários TR; custa o tempo. <!--b:reagrupar-->
- **Chamar "plantou em X"** — avisar — melhor sempre; custa nada. <!--b:call-->
- **Jogar granada no C4** — molotov/HE/smoke — melhor para atrasar o TR; custa a granada. <!--b:molotov,he,smoke-->
- **Dar a volta/flanco** — chegar por outro lado — melhor se há rota; custa o tempo. <!--b:dar_volta-->
- **Segurar e salvar** — não ir — melhor se o retake é ruim; custa o bomb. <!--b:guardar_arma-->
- **Esperar a call** — ouvir o IGL — melhor se há call; custa o tempo. <!--b:esperar_call-->
**O que pesa na escolha:** onde fica o plant; quantos TR; tempo; kit; placar; a dificuldade do bomb.
**Erros de quem avalia mal:** todos correm ao mesmo tempo; não escutam o plant e ficam; chegam um a um.
**Cena:** Situação: plant em A, 4 CT vivos. / Esperado: 15–22 s depois entram em dupla pelo bomb; ninguém chega sozinho.

### S52. Ouvi o defuse (TR)
**Quando:** o jogador (TR) ouve o som de defuse; sabe o tempo restante (5 ou 10 s).
**Opções:**
- **Abrir agora** — aproveitar o som para matar — melhor se tenho linha e tempo; custa a vida. <!--b:abrir_seco-->
- **Jogar granada no C4** — molotov/HE para parar o defuse — melhor se tenho; custa a granada. <!--b:molotov,he-->
- **Spray pela parede/através da caixa** — atirar no defusador — melhor se tenho linha; custa munição. <!--b:spray_atraves-->
- **Pedir ao colega** — chamar para abrir junto — melhor se há colega; custa o tempo. <!--b:call,abrir_dupla-->
- **Esperar até o limite** — aguardar o último instante — melhor para pegar o defusador desprotegido; custa o risco. <!--b:esperar-->
- **Dar a volta** — pegar por trás — melhor se há tempo; custa o tempo. <!--b:dar_volta-->
- **Fingir** — barulho para ele parar — melhor para quebrar o defuse; custa a posição. <!--b:fingir-->
**O que pesa na escolha:** o tempo restante do defuse e da bomba; vivos; minha arma; linha para o C4.
**Erros de quem avalia mal:** abre cedo demais; espera demais; não considera o falso defuse.
**Cena:** Situação: TR ouve defuse com 3 s. / Esperado: ele age agora (granada, spray ou abre); não fica olhando.

### S53. Granada vindo ou caindo (HE, flash, molotov, smoke) e de onde veio
**Quando:** o jogador vê ou ouve uma granada indo em sua direção, ou a vê passar e sabe de onde veio.
**Opções:**
- **Sair da área** — correr para fora do raio — melhor para HE/molotov; custa a posição. <!--b:recuar_cobertura-->
- **Virar de costas** — evitar a flash — melhor para flash; custa a visão. <!--b:esperar-->
- **Responder com granada** — jogar a minha na origem — melhor se sei de onde veio; custa a granada. <!--b:he,flash,molotov-->
- **Usar a origem como informação** — marcar a posição do inimigo — melhor sempre; custa nada. <!--b:esperar_call-->
- **Chamar a origem** — dizer "HE veio de X" — melhor sempre; custa nada. <!--b:call-->
- **Aproveitar o instante** — atacar a posição de quem jogou — melhor se ele se expôs; custa a vida. <!--b:abrir_seco-->
- **Mudar de ângulo** — sair do ponto pré-mirado — melhor se a granada mostra onde estou; custa a pré-mira. <!--b:mudar_angulo-->
**O que pesa na escolha:** tipo de granada; trajetória (origem); distância; colegas; o que acho do adversário.
**Erros de quem avalia mal:** ignora a origem; fica no raio; não chama.
**Cena:** Situação: uma HE vem do corredor. / Esperado: o bot sabe que o inimigo está ali e age (granada, flash, mudar ângulo).

### S54. Colega cantou uma informação (call recebida)
**Quando:** o jogador recebe a call de um colega (lugar, número, arma); ela chega com atraso e pode estar errada.
**Opções:**
- **Confiar e agir** — atualizar a crença e agir — melhor quando o colega é confiável e a informação nova; custa se está errada. <!--b:esperar_call-->
- **Confirmar** — pedir "quantos?" — melhor se a informação é crítica e incompleta; custa um segundo. <!--b:esperar_call,call-->
- **Ignorar** — a informação é velha ou irrelevante — melhor quando já tenho melhor; custa a perda. <!--b:esperar-->
- **Agir sobre a informação** — mudar o ângulo, usar granada, rotacionar — melhor quando muda meu plano; custa o plano. <!--b:mudar_angulo,rotacionar-->
- **Passar adiante** — repetir ao time — melhor se outros não ouviram; custa o canal. <!--b:call-->
- **Contrariar** — achar que o colega errou — melhor se vi o contrário; custa a coordenação. <!--b:esperar-->
**O que pesa na escolha:** a idade da informação; a confiança no colega; o que eu vi; o atraso; o canal ocupado.
**Erros de quem avalia mal:** trata informação velha como atual; ignora; todos agem do mesmo jeito.
**Cena:** Situação: o colega canta "dois no B há 10 s". / Esperado: o jogador pesa a idade e age de acordo.

### S55. A crença não bate com o que vejo (ninguém apareceu / informação velha)
**Quando:** o jogador esperava inimigo num lugar e não há; ou a informação tem tempo; ou apareceu onde não esperava.
**Opções:**
- **Atualizar a crença** — marcar o lugar como vazio — melhor sempre; custa nada. <!--b:esperar_call-->
- **Avançar para o próximo lugar provável** — seguir a busca — melhor se há tempo; custa o risco. <!--b:tomar_espaco-->
- **Esperar mais** — a ausência pode ser paciência — melhor se o lugar é bom para eles; custa o relógio. <!--b:esperar-->
- **Chamar "vazio"** — passar ao time — melhor sempre; custa nada. <!--b:call-->
- **Voltar ao setup** — reposicionar — melhor se estou exposto; custa o espaço. <!--b:recuar_cobertura-->
- **Procurar a causa** — achar por que não estão — melhor se há suspeita de fake; custa tempo. <!--b:jiggle-->
- **Mudar o plano** — trocar o alvo — melhor se o vazio muda a leitura; custa o plano. <!--b:virar_round-->
**O que pesa na escolha:** o tempo; o que acredito; o relógio; o IGL; o que o inimigo costuma fazer.
**Erros de quem avalia mal:** insiste na crença velha; não avisa; fica olhando o mesmo canto.
**Cena:** Situação: o time esperava o TR no meio e nada. / Esperado: o bot atualiza e o time ajusta; não continua mirando o meio.

### Grupo: Utilitária no caminho

### S56. TR: indo para o bomb e uma smoke inimiga fecha a passagem
**Quando:** o jogador está a caminho de um bomb (ou de um espaço) e uma smoke do CT estoura na passagem; ele não vê o que há do outro lado.
**Opções:**
- **Esperar a smoke acabar** — segurar fora da linha — melhor quando o relógio permite e o CT gastou a granada de propósito; custa uns 18 s. <!--b:esperar_smoke,esperar-->
- **Atravessar sozinho em silêncio** — entrar de shift pela smoke — melhor quando acredito que o CT já girou, o barulho do outro lado cobre meu passo ou a smoke está abrindo; custa a vida se ele está esperando atrás. <!--b:atravessar_sozinho,ir_silencio-->
- **Atravessar junto com flash por cima** — dois ou mais entram, flash na saída — melhor com colega e flash; custa a flash e a coordenação. <!--b:atravessar_sozinho,abrir_dupla,flash-->
- **Spray na smoke** — atirar na provável posição de quem está atrás — melhor quando a crença é de alguém atrás (som, colega) e tenho munição; custa revelar a posição. <!--b:spray_atraves-->
- **HE ou molotov na saída** — granada no outro lado — melhor quando ouvi gente ali; custa a granada. <!--b:he,molotov-->
- **Ler a smoke como informação** — ela diz onde o CT quer evitar a linha ou onde ele espera — melhor sempre; custa nada. <!--b:esperar_call,ouvir-->
- **Largar o caminho e virar o round** — ir a outro bomb — melhor quando a smoke indica que o CT stackou ali; custa o tempo de rotação. <!--b:virar_round,rotacionar-->
- **Jogar minha smoke/one-way na borda** — fechar a linha para eles e entrar por outro ângulo — melhor com smoke; custa a smoke. <!--b:smoke,mudar_angulo-->
- **Fingir que atravesso** — fazer barulho e voltar — melhor para puxar a mira dele; custa o tempo. <!--b:fingir-->
- **Ficar de lado e punir quem empurra por dentro** — esperar o CT sair da smoke — melhor quando o CT costuma empurrar; custa o relógio. <!--b:esperar,segurar_linha-->
**O que pesa na escolha:** há quanto tempo a smoke está de pé; onde acredito que o CT está (girou? ainda ali?); se há barulho em outro lugar que cobre meu passo; meu colega e minhas granadas; o relógio; minha arma; o que o CT já fez nesta partida (atravessou antes? segura atrás da smoke?).
**Erros de quem avalia mal:** atravessa correndo e fazendo barulho no instante em que o CT está atrás; espera a smoke inteira acabar com o relógio apertado; ignora que a smoke diz algo.
**Cena:** Situação: smoke do CT fecha o meio, o TR não ouve ninguém girar. / Esperado: ele escolhe com motivo uma opção (esperar, atravessar em silêncio, spray, granada ou virar o round); nenhuma é descartada por regra, e a escolhida depende do que ele acredita.

### S57. TR: o CT smokou o meio (decisão de quem tem a posição do meio)
**Quando:** o meio ficou coberto por smoke do CT no início do round; o TR que estava indo ao meio ou ao topo do meio está na borda.
**Opções:**
- **Esperar abrir e segurar a saída** — ficar fora da linha — melhor quando o CT deve sair; custa o espaço. <!--b:esperar_smoke,esperar-->
- **Atravessar sozinho de shift** — passar silenciosamente — melhor quando o CT provavelmente já girou ou o barulho cobre; custa o jogador. <!--b:atravessar_sozinho,ir_silencio-->
- **Atravessar junto com flash** — dois entram, uma flash por cima — melhor com colega; custa a coordenação. <!--b:atravessar_sozinho,abrir_dupla,flash-->
- **Spray na smoke** — atirar nas posições — melhor com som; custa munição. <!--b:spray_atraves-->
- **HE/molotov na saída** — granada na saída — melhor com som; custa a granada. <!--b:he,molotov-->
- **Usar o tempo da smoke para ir por outro caminho** — o CT está preso na smoke; dar a volta — melhor quando há rota; custa o tempo. <!--b:dar_volta-->
- **Virar o round** — mudar o plano para o lado onde a smoke não está — melhor quando a smoke mostra o CT no meio; custa a rotação. <!--b:virar_round-->
- **Fingir** — barulho para puxar o CT — melhor com colega em outro lugar; custa o barulho. <!--b:fingir-->
**O que pesa na escolha:** o que a smoke diz do CT (ele gastou para sair ou para segurar); onde ouço passos; o relógio; as minhas granadas; os colegas; o que o CT fez em rounds anteriores.
**Erros de quem avalia mal:** entra pela smoke sem pensar; fica parado sem fazer nada; não nota que o CT saiu.
**Cena:** Situação: smoke no meio e passos do CT indo ao A. / Esperado: o TR atravessa sozinho (shift) ou vira o round para o lado vazio; não espera a smoke acabar à toa.

### S58. CT: eu smokei (ou fui smokado) e agora o que faço enquanto ela está de pé
**Quando:** uma smoke está cobrindo a linha que eu seguro ou a linha que o TR tem que passar; faltam uns segundos para ela abrir.
**Opções:**
- **Segurar atrás da smoke** — esperar quem atravessar — melhor quando a smoke protege o meu lado; custa se ele atira através. <!--b:esperar-->
- **Girar para o outro lado** — usar a smoke para sair — melhor quando a informação diz que o TR vem do outro lado; custa o ponto. <!--b:rotacionar-->
- **Atirar através da smoke** — spray na borda — melhor com som; custa revelar a posição. <!--b:spray_atraves-->
- **Mudar de ângulo antes que ela acabe** — sair do ponto — melhor porque o TR vai estar mirando ali; custa o ponto. <!--b:mudar_angulo-->
- **Flash/molotov para quando abrir** — preparar granada — melhor com granada; custa a granada. <!--b:flash,molotov-->
- **Recuar** — usar a smoke para trocar de posição — melhor quando a linha é perigosa; custa o espaço. <!--b:recuar_cobertura-->
- **Chamar a smoke e o que vejo** — informar — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** quanto resta de smoke; o que o TR faz; minha cobertura; colegas; a crença do número.
**Erros de quem avalia mal:** fica no mesmo ponto quando ela acaba; revela a posição sem motivo.
**Cena:** Situação: CT smokou o meio. / Esperado: ele reposiciona antes de abrir ou prepara granada para a saída; não fica no mesmo lugar olhando.

### S59. Molotov (ou incendiária) caiu no meu caminho ou no meu canto
**Quando:** o fogo cobre a passagem ou o canto onde eu estou; ele dura uns 7 s (molotov) e tira vida.
**Opções:**
- **Sair do fogo** — recuar ou andar para fora — melhor quando estou nele; custa a posição. <!--b:recuar_cobertura-->
- **Esperar o fogo acabar** — segurar do outro lado — melhor quando o fogo bloqueia o inimigo também; custa o relógio. <!--b:esperar-->
- **Atravessar rápido** — correr pelo fogo — melhor se a vida permite e o tempo aperta; custa a vida. <!--b:ir_correndo-->
- **Dar a volta** — outro caminho — melhor se há rota; custa o tempo. <!--b:dar_volta-->
- **Punir quem está do outro lado** — atirar nos que o fogo empurrou — melhor porque o fogo tira o canto; custa a posição. <!--b:segurar_linha-->
- **Usar a smoke para apagar o fogo** — apagar com smoke — melhor se o jogo permite; custa a smoke. <!--b:smoke-->
- **Jogar granada** — HE/flash para entrar — melhor com granada; custa a granada. <!--b:he,flash-->
**O que pesa na escolha:** tempo restante; minha vida; onde o fogo bloqueia o inimigo; relógio; granadas.
**Erros de quem avalia mal:** fica no fogo; tenta passar com pouca vida.
**Cena:** Situação: molotov cobre o canto onde o CT estava. / Esperado: ele sai do fogo, muda o ponto e avisa.

### S60. Escolher qual granada jogar, onde e quando
**Quando:** o jogador tem granadas e precisa decidir o uso: lineup, improviso ou guardar; o mapa e a situação pedem alguma.
**Opções:**
- **Lineup conhecido** — jogar no ponto ensaiado — melhor sempre que sei o lineup e a situação o pede; custa a exposição. <!--b:smoke,molotov,flash-->
- **Improvisar** — jogar no olho — melhor quando não há lineup e a ação é urgente; custa a precisão (o erro existe). <!--b:he,molotov-->
- **Guardar** — não gastar — melhor para o pós-plant/retake; custa menos poder agora. <!--b:guardar_arma-->
- **Granada de presença** — gastar uma para obrigar o adversário a reagir — melhor com granada sobrando; custa a granada. <!--b:smoke,molotov-->
- **Granada em quem foi ouvido** — HE no som — melhor se está parado; custa a granada. <!--b:he-->
- **Granada para o colega** — flash/smoke para ele entrar — melhor quando o colega entra; custa a granada. <!--b:flash,smoke-->
- **Pedir a granada ao colega** — chamar quem tem — melhor quando a minha acabou; custa o aviso. <!--b:pedir_granada-->
**O que pesa na escolha:** a tarefa; o lineup que conheço; a informação; o número de granadas; o adversário (já viu isso?); o relógio.
**Erros de quem avalia mal:** erra a granada por pressa; joga a que não serve; gasta tudo.
**Cena:** Situação: o suporte tem lineup e o entry vai entrar. / Esperado: ele joga no tempo; se errou, avisa.

### S61. Flash para o colega (e a minha própria)
**Quando:** o colega vai abrir ou entrar e tem a mão livre para atirar; o jogador tem flash.
**Opções:**
- **Pop flash por cima** — estourar na entrada — melhor com colega pronto; custa a flash e a exposição. <!--b:flash,bangar_entrar-->
- **Avisar a flash** — cantar "flash" antes — melhor sempre; custa nada. <!--b:call,flash-->
- **Flash para mim** — jogar e entrar eu — melhor quando sou o entry; custa a flash. <!--b:flash,bangar_entrar-->
- **Contra-flash** — cegar o inimigo que vai peekar — melhor para defesa; custa a flash. <!--b:flash-->
- **Segunda flash de fundo** — para a segunda leva — melhor com 2 flashes; custa a segunda flash. <!--b:flash-->
- **Guardar a flash** — não jogar — melhor se o colega não vai abrir; custa a oportunidade. <!--b:guardar_arma-->
- **Não cegar o time** — esperar o colega sair da linha — melhor para evitar flash no aliado; custa o tempo. <!--b:esperar-->
**O que pesa na escolha:** a posição do colega; o momento do peek; a chance de cegar o time; o que sei do inimigo.
**Erros de quem avalia mal:** cega o próprio time; joga cedo e o inimigo se esconde; joga tarde.
**Cena:** Situação: colega vai abrir a quina e o jogador tem flash. / Esperado: ele avisa e joga no instante certo; o colega abre no estouro.

### Grupo: Pós-plant (TR), retake e save (CT)

### S62. TR: acabei de plantar, onde me posiciono
**Quando:** o C4 foi plantado; o jogador sabe o lugar do plant e quantos vivos tem de cada lado.
**Opções:**
- **Posição em cruzado sobre o C4** — dois ângulos diferentes cobrindo o C4 e as entradas do retake — melhor com vivos suficientes; custa a exposição. <!--b:cruzado,esperar-->
- **Posição de passagem (rota de retake)** — cobrir a entrada pela qual o CT chega — melhor para atrasar; custa o C4 ficar menos coberto. <!--b:segurar_linha,esperar-->
- **Ficar escondido e ouvir** — silêncio até o CT chegar — melhor para pegar o defusador; custa o tempo. <!--b:esconder,ouvir-->
- **Posição avançada para flanquear** — ir ao lado de onde o CT vem — melhor com menos vivos; custa o risco. <!--b:dar_volta,tomar_espaco-->
- **Gastar granada cedo para queimar relógio** — obrigar o CT a agir — melhor com mais vivos; custa a granada. <!--b:molotov,smoke-->
- **Contar uns 3 s antes de abrir** — não abrir logo depois de plantar — melhor porque o CT vai pré-mirar; custa nada. <!--b:esperar-->
- **Chamar onde estou e onde plantei** — avisar — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** vivos dos dois lados; o tempo do C4; minhas granadas; as entradas do retake; o que o CT fez em retakes anteriores; a arma (AWP segura linha longa).
**Erros de quem avalia mal:** todo mundo no mesmo cantinho; planta e fica sem plano; abre logo.
**Cena:** Situação: TR planta A com 4 vivos. / Esperado: cada um assume uma linha diferente (cruzado), não ficam todos juntos.

### S63. TR pós-plant: ouvi o CT chegando
**Quando:** o jogador ouve passos ou vê CT vindo ao C4; tem tempo no relógio da bomba.
**Opções:**
- **Esperar e matar o primeiro** — segurar o ângulo — melhor com cruzado; custa a exposição. <!--b:esperar,cruzado-->
- **Granada na entrada** — molotov/smoke/HE — melhor para atrasar; custa a granada. <!--b:molotov,he,smoke-->
- **Flash para mim e abrir** — sair e matar — melhor com menos vivos; custa o risco. <!--b:flash,bangar_entrar-->
- **Mudar de ângulo** — sair do ponto pré-mirado — melhor se o CT sabe onde estou; custa o ponto. <!--b:mudar_angulo-->
- **Chamar o número e o lugar** — informar — melhor sempre; custa nada. <!--b:call-->
- **Dar a volta e pegar por trás** — flanquear — melhor com tempo; custa tempo. <!--b:dar_volta-->
- **Jogar pelo relógio** — evitar duelo e deixar o tempo correr — melhor com pouco tempo; custa a exposição. <!--b:jogar_relogio-->
**O que pesa na escolha:** número; tempo da bomba; vivos; granada; ângulos.
**Erros de quem avalia mal:** sai do ponto e perde a vantagem; fica onde foi visto.
**Cena:** Situação: dois CT entram pelo conector. / Esperado: o TR atira da posição segura ou muda de ângulo; não vai ao encontro.

### S64. TR pós-plant: ouvi o defuse
**Quando:** o jogador ouve o início do defuse; sabe o tempo da bomba e do defuse.
**Opções:**
- **Abrir agora** — matar o defusador — melhor com linha e tempo; custa a vida. <!--b:abrir_seco-->
- **Granada no C4** — molotov/HE — melhor para cancelar; custa a granada. <!--b:molotov,he-->
- **Spray através de caixa/parede** — atirar no defusador — melhor se tenho linha; custa munição. <!--b:spray_atraves-->
- **Pedir ao colega** — combinar abertura — melhor com colega; custa o aviso. <!--b:call,abrir_dupla-->
- **Esperar o último segundo** — aguardar — melhor se o CT está sozinho; custa o risco. <!--b:esperar-->
- **Fingir** — barulho para ele parar — melhor se ele está nervoso; custa a posição. <!--b:fingir-->
- **Dar a volta** — por trás — melhor se há tempo; custa o tempo. <!--b:dar_volta-->
**O que pesa na escolha:** tempo do defuse; tempo da bomba; vivos; granada.
**Erros de quem avalia mal:** abre cedo; fica parado olhando.
**Cena:** Situação: defuse começa com 5 s, TR com linha. / Esperado: age já.

### S65. TR pós-plant: o tempo e os vivos
**Quando:** o C4 está plantado; o jogador tem mais ou menos vivos que o CT.
**Opções:**
- **Com mais vivos: jogar passivo e queimar relógio** — esperar e gastar granada — melhor porque o CT tem que agir; custa nada. <!--b:jogar_relogio,esperar-->
- **Com menos vivos: buscar igualar** — flash na passagem, posição avançada — melhor porque jogar passivo perde; custa risco. <!--b:tomar_espaco,flash-->
- **Dividir o CT** — um barulho e outro posição — melhor com colega; custa o barulho. <!--b:fingir-->
- **Guardar granada para o final** — smoke para o defuse — melhor no fim; custa nada. <!--b:smoke,guardar_arma-->
- **Chamar o tempo** — avisar quanto resta — melhor sempre; custa nada. <!--b:call-->
- **Trocar de posição na metade** — mudar ao longo do tempo — melhor para surpreender; custa a pré-mira. <!--b:mudar_angulo-->
- **Pegar o CT no meio da rota** — interceptar — melhor se sei a rota; custa o risco. <!--b:tomar_espaco-->
**O que pesa na escolha:** vivos; tempo; granadas; qual lado precisa do round.
**Erros de quem avalia mal:** agressão com vantagem entrega o que o CT precisa; passividade com desvantagem perde.
**Cena:** Situação: 3 TR contra 4 CT com 25 s. / Esperado: o TR joga junto e busca um abate; não espera sem plano.

### S66. CT: a bomba foi plantada, retomar ou salvar
**Quando:** o C4 foi plantado; o jogador sabe quantos CT e TR estão vivos, quanto falta da bomba, quem tem o kit e qual é o bomb.
**Opções:**
- **Retake em grupo** — juntar e entrar em dupla por uma ou duas entradas — melhor com vivos suficientes, kit e tempo; custa 15 a 22 s de preparo. <!--b:reagrupar,abrir_dupla-->
- **Retake com flanco** — um grupo frontal e um jogador por trás (palácio, apartamentos, saída do TR) — melhor com 3 ou mais vivos; custa a coordenação. <!--b:dar_volta,abrir_dupla-->
- **Esperar o TR se expor** — segurar de fora e deixar o relógio apertar quem plantou — melhor quando o TR está espalhado; custa o tempo do defuse. <!--b:esperar,jogar_relogio-->
- **Mandar só o kit com cobertura** — o portador entra protegido por um ou dois — melhor quando a janela de tempo é curta; custa expor o portador. <!--b:defusar,cobrir_colega-->
- **Salvar a arma** — recuar e guardar rifle e AWP — melhor quando a chance é pequena (3v5 num bomb difícil) ou o tempo não fecha; custa o round. <!--b:guardar_arma-->
- **Decidir junto e chamar** — "vamos" ou "salvo" para todos — melhor em qualquer caso; custa nada, mas dois salvando e um indo sozinho é o resultado mais caro. <!--b:call-->
- **Jogar granada no C4 para ganhar tempo** — smoke/molotov para cobrir o defuse — melhor com granada guardada; custa a granada. <!--b:smoke,molotov-->
**O que pesa na escolha:** placar de vivos; segundos de bomba e de defuse (kit cinco, sem kit dez); dificuldade do bomb; onde acredito que os TR estão; granadas guardadas; economia (salvar duas armas vale quase um retake de 8%).
**Erros de quem avalia mal:** um vai sozinho e morre sem troca; ninguém decide e o tempo passa; salva quem devia estar no retake.
**Cena:** Situação: 3 CT contra 4 TR, 30 s de bomba. / Esperado: decidem juntos entre retake e save; não saem uns sozinhos e outros fugindo.

### S67. CT: aproximação do retake
**Quando:** o time decidiu retomar; o jogador sabe o plano (entradas, granadas) e o tempo.
**Opções:**
- **Esperar o grupo juntar** — ficar na base e juntar — melhor para entrar em dupla; custa 10–15 s. <!--b:reagrupar,esperar-->
- **Entrar por duas entradas** — frente e flanco — melhor com tempo; custa a coordenação. <!--b:dar_volta,abrir_dupla-->
- **Smoke para o C4** — cobrir o defuse — melhor para defusar; custa a smoke. <!--b:smoke-->
- **Molotov no canto** — limpar o canto — melhor perto; custa a granada. <!--b:molotov-->
- **Flash e entrar** — bang e entrar — melhor com flash; custa a flash. <!--b:bangar_entrar-->
- **Jiggle para ver** — checar o TR — melhor se há tempo; custa o risco. <!--b:jiggle-->
- **Anunciar a granada** — chamar antes — melhor para não dobrar; custa nada. <!--b:call-->
- **Tap no C4** — encostar cedo para dúvida — melhor para tirar o TR do esconderijo; custa o risco. <!--b:fake_defuse-->
**O que pesa na escolha:** tempo; kit; número; bomb; onde acho que o TR está.
**Erros de quem avalia mal:** um a um; sem granada; muito cedo.
**Cena:** Situação: retake em A com 4 CT. / Esperado: dois entram em ≤3 s um do outro.

### S68. CT: entrada do retake e o portador do kit
**Quando:** o time entra no bomb; o kit está com um colega.
**Opções:**
- **Proteger o portador** — cobrir o defusador — melhor sempre; custa a exposição dos outros. <!--b:cobrir_colega,defusar-->
- **Entrar e limpar** — passo a passo — melhor com tempo; custa tempo. <!--b:tomar_espaco-->
- **Smoke no C4** — cobrir o defuse — melhor para defusar; custa a smoke. <!--b:smoke-->
- **Defuse com cobertura** — começar com colegas cobrindo — melhor com tempo; custa o tempo. <!--b:defusar-->
- **Fake de defuse** — tap e sair — melhor para tirar o TR; custa o tempo. <!--b:fake_defuse-->
- **Entregar o kit** — passar a quem está perto — melhor se o portador cai; custa o aviso. <!--b:pegar_arma,call-->
- **Abandonar** — salvar — melhor sem chance; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** tempo; vivos; kit; granadas.
**Erros de quem avalia mal:** o portador do kit está longe do bomb; começa o defuse com inimigo vivo.
**Cena:** Situação: CT vence o retake e o kit está longe. / Esperado: o kit vai ao bomb ou passa a quem está perto.

### S69. CT: colega morreu no retake e fiquei sozinho
**Quando:** o time entrou; um ou mais caíram; o jogador vê que o TR está forte.
**Opções:**
- **Trocar imediato** — matar quem matou — melhor com troca possível; custa a vida. <!--b:trocar-->
- **Recuar e esperar** — sair da linha — melhor com poucos; custa o plant. <!--b:recuar_cobertura-->
- **Granada e entrar** — molotov/smoke/flash — melhor com granada; custa a granada. <!--b:molotov,smoke,flash-->
- **Salvar** — guardar a arma — melhor sem chance; custa o round. <!--b:guardar_arma-->
- **Dar a volta** — outro ângulo — melhor com tempo; custa tempo. <!--b:dar_volta-->
- **Fake de defuse** — tentar distrair — melhor para puxar o TR; custa o risco. <!--b:fake_defuse-->
- **Chamar** — informar o que viu — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** tempo; kit; número; o que sei do TR.
**Erros de quem avalia mal:** vai sozinho; fica sem decidir.
**Cena:** Situação: 2 CT de 4 morreram no retake. / Esperado: decidem entre salvar e tentar com cobertura.

### S70. Desarmar com gente viva ao redor
**Quando:** o C4 está plantado, o jogador quer defusar mas há TR possível por perto; o tempo é limitado.
**Opções:**
- **Defusar com cobertura** — colegas olham os ângulos — melhor com tempo; custa a atenção deles. <!--b:defusar,cobrir_colega-->
- **Smoke e defusar** — cobrir o C4 — melhor para esconder; custa a smoke (ela pode não bloquear bala). <!--b:smoke,defusar-->
- **Fake de defuse** — começar e parar — melhor para tirar o TR do canto; custa tempo. <!--b:fake_defuse-->
- **Limpar antes** — matar os TR antes — melhor com tempo; custa tempo. <!--b:tomar_espaco-->
- **Defusar no último segundo** — esperar — melhor para não ser pego; custa o risco da bomba. <!--b:esperar,defusar-->
- **Esperar a bomba (kit)** — usar o tempo do kit — melhor com kit e tempo; custa nada. <!--b:defusar-->
- **Deixar outro defusar** — passar a tarefa — melhor se estou ferido; custa o aviso. <!--b:call-->
**O que pesa na escolha:** tempo; kit (5 s vs 10 s); número de TR; granadas; cobertura.
**Erros de quem avalia mal:** defusa à vista; defusa sem kit e perde.
**Cena:** Situação: CT com kit e 18 s de bomba, TR possível. / Esperado: limpa ou defusa com cobertura; não defusa à vista.

### Grupo: Vantagem e desvantagem numérica

### S71. TR: ganhei o primeiro abate (5v4)
**Quando:** o TR matou o primeiro CT; o time está em vantagem; o jogador sabe o lugar da baixa.
**Opções:**
- **Não devolver a vantagem** — jogar em dupla pela troca — melhor porque 5v4 vale 75%; custa a velocidade. <!--b:ficar_perto,trocar-->
- **Bater rápido no bomb que perdeu o defensor** — aproveitar — melhor com a baixa em um bomb específico; custa o risco. <!--b:ir_correndo,virar_round-->
- **Pressionar o espaço que ele deixava** — tomar o espaço — melhor porque o CT reage; custa o tempo. <!--b:tomar_espaco-->
- **Desacelerar** — esperar o CT errar — melhor com tempo; custa o relógio. <!--b:esperar,jogar_relogio-->
- **Fake para pegar rotação** — barulho e girar — melhor para dividir o CT; custa o barulho. <!--b:fingir-->
- **Duelo isolado com apoio** — só com troca — melhor com colega; custa a exposição. <!--b:abrir_dupla-->
- **Chamar a baixa** — informar — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** onde caiu; tempo; granadas; o CT.
**Erros de quem avalia mal:** corre e dá duelo isolado; perde a troca e vira 4v4.
**Cena:** Situação: TR 5v4 aos 30 s. / Esperado: o time anda em dupla; ninguém duela sozinho.

### S72. CT: ganhei o primeiro abate (5v4)
**Quando:** o CT matou o primeiro TR sem perder ninguém; o time está em vantagem.
**Opções:**
- **Voltar ao setup** — manter e parar a agressão — melhor para segurar; custa o espaço. <!--b:recuar_cobertura,esperar-->
- **Tomar espaço com um ou dois** — palácio/curta/rato — melhor se vantagem cedo; custa o risco. <!--b:tomar_espaco-->
- **Reforçar o bomb atacado** — mandar gente — melhor se o TR vem em bloco; custa o outro bomb. <!--b:rotacionar-->
- **Informar** — dizer onde caiu — melhor sempre; custa nada. <!--b:call-->
- **Não entrar em dupla no palácio logo** — evitar perder dois — melhor porque o par que entra junto cai junto; custa o espaço. <!--b:esperar-->
- **Flanquear** — um vai pelo lado do TR — melhor com info; custa o risco. <!--b:dar_volta-->
- **Jogar o tempo** — esperar o TR vir — melhor com CT; custa o relógio. <!--b:jogar_relogio-->
**O que pesa na escolha:** tempo; onde caiu; granada; o TR.
**Erros de quem avalia mal:** vai todo mundo; ninguém muda.
**Cena:** Situação: 5v4 CT aos 20 s. / Esperado: um ou dois tomam espaço, o resto mantém.

### S73. TR: estamos em desvantagem (4v5 ou pior)
**Quando:** o TR perdeu um ou mais sem abater; ainda há plano possível.
**Opções:**
- **Jogar junto pela troca** — dupla — melhor porque 1x1 vale; custa o espaço. <!--b:ficar_perto,trocar-->
- **Execução com o que sobrou** — rush — melhor com tempo; custa o risco. <!--b:ir_correndo-->
- **Pick do outro lado** — buscar um abate — melhor com AWP; custa o risco. <!--b:isca_ombro-->
- **Ângulo inesperado** — surpreender — melhor porque variância alta; custa a vida. <!--b:mudar_angulo-->
- **Esperar o CT errar** — jogar o relógio — melhor se o CT é impaciente; custa o relógio. <!--b:esperar,jogar_relogio-->
- **Lurk e puxar** — um lado distrai — melhor com par; custa o par. <!--b:dar_volta-->
- **Salvar** — guardar — melhor sem chance; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** vivos; tempo; granada; economia.
**Erros de quem avalia mal:** cada um por si; não muda nada.
**Cena:** Situação: 4v5 TR aos 40 s. / Esperado: o time se junta ou tenta um ângulo inesperado; não repete a jogada.

### S74. CT: estamos em desvantagem (4v5 ou pior)
**Quando:** o CT perdeu um sem abater; o bomb pode estar mais fraco.
**Opções:**
- **Informação e aposta (stack)** — colher e apostar — melhor porque desvantagem; custa o outro bomb. <!--b:esperar,jiggle-->
- **Jogar pelo retake** — recuar e juntar — melhor se o bomb é fraco; custa o plant. <!--b:recuar_cobertura,reagrupar-->
- **Agressão com apoio** — só com flash/par — melhor para recuperar; custa a vida. <!--b:bangar_entrar,tomar_espaco-->
- **Esperar** — jogar o relógio — melhor porque o TR tem pressa; custa o espaço. <!--b:esperar,jogar_relogio-->
- **Mudar de posição** — varia — melhor porque o TR leu; custa o ponto. <!--b:mudar_angulo-->
- **Ficar perto do colega** — dupla — melhor para trocar; custa a cobertura. <!--b:ficar_perto-->
- **Salvar** — guardar — melhor sem chance; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** vivos; tempo; granada; o TR.
**Erros de quem avalia mal:** sai sozinho para recuperar; cada um no seu canto.
**Cena:** Situação: 4v5 CT. / Esperado: o time se junta ou joga pelo retake; ninguém sai sozinho.

### S75. Vantagem pequena tardia (3v2, 2v1, 4v3)
**Quando:** o round avançou, sobraram poucos de cada lado e o jogador está com um a mais.
**Opções:**
- **Juntar e andar em dupla** — nunca deixar o pior caso virar duelo isolado — melhor porque o pior caso vira 1 por 1; custa tempo. <!--b:ficar_perto,abrir_dupla-->
- **Deixar o adversário vir** — segurar e jogar o relógio — melhor quando o tempo trabalha a meu favor (CT sem plant, TR com bomba); custa o espaço. <!--b:esperar,jogar_relogio-->
- **Forçar com granada** — flash/molotov/HE para tirar o outro do canto — melhor com granada sobrando; custa a granada. <!--b:flash,molotov,he-->
- **Pegar o objetivo** — plantar ou defusar com a vantagem — melhor quando a bomba decide o round; custa o risco. <!--b:plantar,defusar-->
- **Dar a volta com um e fixar com o outro** — um prende, o outro pega por trás — melhor com informação da posição; custa o tempo. <!--b:dar_volta,cobrir_colega-->
- **Informar o que sobrou** — chamar quem vive e onde — melhor sempre; custa nada. <!--b:call-->
- **Evitar duelo isolado** — sair da linha e esperar a troca — melhor quando o outro está pré-mirado; custa o relógio. <!--b:recuar_cobertura-->
**O que pesa na escolha:** relógio e bomba; granadas; posição do adversário (crença); quem está perto de quem; lado (o 5v4 vale mais ao TR que ao CT).
**Erros de quem avalia mal:** acha que "está ganhando" e dá duelo isolado; cada um joga por si; esquece a bomba.
**Cena:** Situação: 2v1 sem plant. / Esperado: os dois ficam juntos; não dão duelo isolado.

### Grupo: Clutch (1 contra N)

### S76. Clutch 1v1
**Quando:** sobraram um de cada lado; o jogador sabe (ou acha) onde o outro está; há bomba plantada ou não.
**Opções:**
- **Esperar em ângulo seguro e ouvir** — silêncio e ouvir — melhor quando o tempo me favorece (CT sem bomba, TR com bomba plantada); custa o tempo. <!--b:esperar,ouvir,jogar_relogio-->
- **Fazer barulho para puxar** — fingir e voltar — melhor para tirar o outro do ponto; custa a posição. <!--b:fingir-->
- **Pegar por trás** — dar a volta — melhor se sei onde ele está; custa o tempo. <!--b:dar_volta-->
- **Jogar granada** — flash/HE/molotov e entrar — melhor com granada; custa a granada. <!--b:flash,he,molotov,bangar_entrar-->
- **Peek rápido com jump** — pular e atirar — melhor contra mira pré-posicionada; custa precisão. <!--b:pular_peek-->
- **Atacar o objetivo** — plantar ou defusar com cobertura — melhor quando a bomba decide; custa o risco. <!--b:plantar,defusar-->
- **Fake de defuse/plant** — enganar — melhor para tirar o outro; custa o risco. <!--b:fake_defuse-->
- **Mudar de lugar** — sair e esperar de outro — melhor depois de fazer barulho; custa o ponto. <!--b:mudar_angulo-->
**O que pesa na escolha:** o relógio e a bomba; onde acho que ele está; granadas; vida; arma; se há som.
**Erros de quem avalia mal:** corre cego; faz barulho demais; fica parado esperando sem tempo.
**Cena:** Situação: CT 1v1 com a bomba plantada e 15 s. / Esperado: ele usa granada ou barulho e entra com cobertura; não espera o tempo acabar.

### S77. Clutch 1vN sem plant (TR) ou antes do plant
**Quando:** o jogador está sozinho contra 2 ou mais CT; o C4 ainda não foi plantado; há relógio.
**Opções:**
- **Plantar com cobertura** — plantar e jogar o pós-plant — melhor porque muda o jogo; custa o risco. <!--b:plantar-->
- **Esconder-se e esperar** — silêncio — melhor para o CT vir um a um; custa o relógio. <!--b:esconder,esperar-->
- **Pegar um de cada vez** — usar o som para separar — melhor se eles se espalham; custa o ponto. <!--b:ouvir,mudar_angulo-->
- **Barulho para puxar** — fingir num lado e ir ao outro — melhor para dividir; custa a posição. <!--b:fingir,dar_volta-->
- **Granada para quebrar o grupo** — HE/molotov/flash — melhor se juntos; custa a granada. <!--b:he,molotov,flash-->
- **Salvar** — guardar a arma — melhor sem chance; custa o round. <!--b:guardar_arma-->
- **Ir ao C4 caído/ao bomb** — foco no objetivo — melhor se o relógio apertou; custa a vida. <!--b:largar_objetivo,plantar-->
**O que pesa na escolha:** vivos; tempo; granada; posição.
**Erros de quem avalia mal:** corre ao encontro; fica no mesmo lugar.
**Cena:** Situação: TR 1v3 sem plant, 50 s. / Esperado: ele separa os CT com som, planta com cobertura ou salva; não corre contra os três.

### S78. Clutch CT 1vN com a bomba plantada
**Quando:** o jogador é o último CT; o C4 está plantado; os TR estão perto da bomba.
**Opções:**
- **Defusar com cobertura de granada** — smoke no C4 e defuse — melhor com smoke e kit; custa o risco. <!--b:smoke,defusar-->
- **Esperar o TR sair do ponto** — pegar quem se mexe — melhor para tirar os TR; custa o tempo. <!--b:esperar,jogar_relogio-->
- **Fake de defuse** — tap e sair — melhor para tirar o TR do canto; custa o tempo. <!--b:fake_defuse-->
- **Pegar um por vez** — atirar no primeiro e recuar — melhor se eles estão separados; custa o ponto. <!--b:reposicionar,esperar-->
- **Dar a volta e pegar por trás** — flanquear — melhor com tempo; custa o tempo. <!--b:dar_volta-->
- **Salvar** — guardar — melhor com muito pouco tempo e muitos TR; custa o round. <!--b:guardar_arma-->
- **Ir ao C4 a todo custo** — correr — melhor se tem pouco tempo; custa a vida. <!--b:ir_correndo,defusar-->
**O que pesa na escolha:** tempo da bomba; kit; número de TR; granada.
**Erros de quem avalia mal:** defusa à vista; sem calcular o tempo.
**Cena:** Situação: CT 1v3, bomba com 20 s. / Esperado: ele usa smoke/fake e tenta separar; não defusa à vista.

### S79. Clutch TR pós-plant 1vN
**Quando:** o jogador é o último TR; o C4 está plantado; o CT chega.
**Opções:**
- **Posição de silêncio** — ficar e ouvir — melhor para pegar o defusador; custa o tempo. <!--b:esconder,ouvir-->
- **Jogar pelo relógio** — evitar duelo — melhor porque o tempo vence; custa a exposição. <!--b:jogar_relogio-->
- **Pegar o primeiro** — matar quem entra e recuar — melhor porque o CT entra um a um; custa o ponto. <!--b:esperar,reposicionar-->
- **Granada no C4** — molotov/HE — melhor para parar o defuse; custa a granada. <!--b:molotov,he-->
- **Mudar de ponto** — sair do pré-mirado — melhor se vi que sabem; custa o ponto. <!--b:mudar_angulo-->
- **Fingir** — barulho para afastar — melhor com 1 defusador; custa o ponto. <!--b:fingir-->
- **Abrir quando o defuse começa** — usar o som — melhor porque ele está vulnerável; custa a vida. <!--b:abrir_seco-->
**O que pesa na escolha:** tempo; número; granadas; posição.
**Erros de quem avalia mal:** sai do ponto e perde a vantagem; abre cedo.
**Cena:** Situação: TR 1v2 pós-plant. / Esperado: ele espera o defuse e age; não vai ao encontro.

### Grupo: Fim de round

### S80. Relógio apertado, TR sem plant
**Quando:** o relógio do round passou de 1:15 sem plant; o time precisa decidir.
**Opções:**
- **Rush final** — correr com tudo — melhor porque é a última chance; custa o round. <!--b:ir_correndo-->
- **Plantar em qualquer lugar** — largar e plantar — melhor porque plantar dá bônus; custa o risco. <!--b:plantar,largar_objetivo-->
- **Fake e executar** — barulho e ataque — melhor se o CT gira; custa o tempo. <!--b:fingir-->
- **Salvar** — guardar armas — melhor sem chance; custa o round. <!--b:guardar_arma-->
- **Cobrir o plantador** — dois protegem — melhor com tempo; custa os dois. <!--b:cobrir_colega-->
- **Granada de tempo** — smoke para plantar — melhor com smoke; custa a smoke. <!--b:smoke-->
- **Chamar o tempo** — avisar — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** tempo; vivos; C4; granadas.
**Erros de quem avalia mal:** espera demais; não planta.
**Cena:** Situação: 30 s restantes sem plant. / Esperado: o time decide e executa; não fica esperando.

### S81. CT: relógio apertando, o TR não plantou
**Quando:** o round passou de uns 1:30 sem plant e o TR ainda não decidiu; o jogador segura uma posição.
**Opções:**
- **Segurar e jogar o tempo** — manter a posição e deixar o relógio vencer — melhor porque o TR é quem tem pressa; custa nada. <!--b:esperar,jogar_relogio-->
- **Punir o rush final** — esperar o TR correr no fim e matar quem vem — melhor porque o TR é obrigado a vir; custa a vigilância em todos os lados. <!--b:segurar_linha,esperar-->
- **Vigiar o C4 caído** — se o C4 está no chão, ficar de linha sobre ele — melhor porque o TR precisa buscá-lo; custa a posição. <!--b:vigiar_c4-->
- **Granada para negar o plant** — molotov/smoke no bomb — melhor perto do fim, com granada guardada; custa a granada. <!--b:molotov,smoke-->
- **Mudar de ângulo antes do fim** — variar o ponto — melhor porque o TR já pré-mirou meu ponto; custa a pré-mira. <!--b:mudar_angulo-->
- **Chamar o tempo** — avisar quanto falta — melhor sempre; custa nada. <!--b:call-->
- **Caçar o TR** — sair para tirar alguém — melhor quando o tempo ainda sobra e há vantagem; custa a posição e o risco de ser pego sozinho. <!--b:tomar_espaco-->
**O que pesa na escolha:** relógio; vivos; onde acredito que o TR está; granadas guardadas; se o C4 foi visto; quem tem pressa.
**Erros de quem avalia mal:** sai para caçar com 20 s e perde; cada um por si; esquece de olhar o C4.
**Cena:** Situação: 25 s restantes, sem plant, 5v5. / Esperado: o CT segura e não sai; o TR é que precisa vir.

### S82. Salvar: decidir cedo e junto
**Quando:** o round está perdido (ou quase) para o jogador e ele tem arma cara; os colegas vivos também decidem.
**Opções:**
- **Salvar a arma e fugir** — sair da área e esperar o fim — melhor sem chance real de retake; custa o round. <!--b:guardar_arma-->
- **Salvar primeiro o AWP e o rifle** — priorizar a arma de maior valor — melhor porque dinheiro parado define a compra seguinte; custa nada. <!--b:guardar_arma-->
- **Tentar o retake mesmo assim** — arriscar a arma — melhor quando o C4 está plantado e há chance razoável; custa a arma se falhar. <!--b:reagrupar-->
- **Combinar com o time** — chamar "salvo" ou "vamos" antes de agir — melhor sempre, porque dois salvando e um forçando é o pior resultado; custa nada. <!--b:call-->
- **Esconder-se** — ficar em lugar onde o TR não vai passar — melhor para não ser caçado; custa o tempo. <!--b:esconder-->
- **Fazer lurk do save** — esperar o TR relaxar e punir — melhor se o TR está distraído com o objetivo; custa o risco. <!--b:esconder,dar_volta-->
- **Guardar também as granadas** — não gastá-las num round perdido — melhor porque sobram para o próximo; custa o ganho pequeno agora. <!--b:guardar_arma-->
**O que pesa na escolha:** chance de retake (vivos, tempo, kit, bomb); economia do time; se é AWP; onde está o TR; o placar da partida.
**Erros de quem avalia mal:** decide tarde, quando o TR já está em cima; um vai e os outros salvam; salva em todo round (vira hábito).
**Cena:** Situação: 2v4 com 15 s de bomba. / Esperado: os dois salvam juntos; ninguém vai sozinho.

### S83. TR: caçar o save
**Quando:** o round está ganho (bomba explodiu ou tempo) e um CT está fugindo com a arma; o jogador pode ir atrás antes de o round acabar.
**Opções:**
- **Ir atrás em dupla** — dois juntos para o abate do sobrevivente — melhor porque a arma dele vale dinheiro; custa o tempo e a atenção. <!--b:cacar_save,ficar_perto-->
- **Esperar na rota provável** — ficar onde ele vai passar — melhor quando conheço o caminho; custa tempo parado. <!--b:esperar-->
- **Ouvir os passos** — achar pela direção — melhor com som; custa o tempo. <!--b:ouvir-->
- **Cortar o caminho** — ir pelo flanco — melhor com mapa conhecido; custa tempo. <!--b:dar_volta-->
- **Granada na saída** — HE/molotov no ponto onde ele deve estar — melhor se sei; custa a granada. <!--b:he,molotov-->
- **Ignorar** — seguir o objetivo — melhor quando o tempo acaba ou o risco é maior que o ganho; custa a arma dele ficar viva. <!--b:largar_objetivo-->
**O que pesa na escolha:** tempo restante; se o sobrevivente tem arma cara; risco (ele pode matar um); relógio; vivos.
**Erros de quem avalia mal:** vai sozinho e morre para o save; ignora o AWP que foge.
**Cena:** Situação: CT foge com uma AWP e o round acaba em 15 s. / Esperado: dois TR cortam o caminho ou todos o ignoram; ninguém vai sozinho em linha reta.

### S84. Pegar a arma do chão (e o kit)
**Quando:** há uma arma melhor que a minha no chão (AWP, rifle) ou um kit; o jogador está sem inimigo na linha.
**Opções:**
- **Pegar e trocar** — pegar a melhor e largar a pior — melhor quando o lugar está seguro; custa dois segundos. <!--b:pegar_arma-->
- **Pegar para o colega** — pegar e dropar para quem tem a função — melhor se ele está perto; custa o tempo. <!--b:pegar_arma,call-->
- **Vigiar até ser seguro** — esperar o inimigo sair da linha — melhor se há ameaça; custa o relógio. <!--b:vigiar_c4,esperar-->
- **Deixar para depois do objetivo** — seguir a tarefa e voltar — melhor com o relógio em risco; custa o risco de perder. <!--b:largar_objetivo-->
- **Chamar o colega** — pedir que pegue — melhor se estou longe; custa o aviso. <!--b:call-->
- **Ignorar** — a minha serve — melhor se a diferença é pequena; custa a arma. <!--b:esperar-->
**O que pesa na escolha:** minha arma; inimigos; tempo; economia; função.
**Erros de quem avalia mal:** não pega a AWP e termina com Mac-10; pega no meio do fogo.
**Cena:** Situação: sobrou um TR com Mac-10 e há AWP no chão sem inimigo. / Esperado: ele pega a AWP antes de seguir.

### S85. A bomba vai explodir ou o round está decidido: o que cada um faz
**Quando:** a bomba tem poucos segundos para explodir sem chance de defuse, ou o round já foi decidido; os vivos têm armas e pouca função.
**Opções:**
- **Cada um para um lado diferente** — espalhar para proteger armas e evitar ser caçado — melhor porque todos juntos no mesmo canto é o defeito (colmeia); custa nada. <!--b:esconder,guardar_arma-->
- **Salvar a arma** — fugir da área — melhor sem chance de retake; custa nada. <!--b:guardar_arma-->
- **Continuar o objetivo enquanto há chance** — defuse se ainda cabe — melhor se o tempo permite; custa o risco. <!--b:defusar-->
- **Pegar kit e arma caídos** — apanhar o que dá — melhor porque a economia seguinte agradece; custa um pouco de tempo. <!--b:pegar_arma-->
- **Ficar em posição de segurança** — olhar o ângulo em caso de inimigo — melhor se há TR vivo perto; custa nada. <!--b:esperar-->
- **Chamar o estado** — avisar quem está vivo e onde — melhor sempre; custa nada. <!--b:call-->
**O que pesa na escolha:** estado do round; armas de cada um; inimigos vivos; posição do jogador.
**Erros de quem avalia mal:** todos correm para o mesmo cantinho; ninguém salva; alguém fica no meio do caminho para morrer.
**Cena:** Situação: bomba com 5 s sem defuse possível. / Esperado: os CT vivos vão para lugares diferentes; não se juntam.

### Grupo: C4 (levar, cair, pegar, vigiar, plantar, cortar, desarmar, fake)

### S86. Levar o C4
**Quando:** o jogador é o portador do C4 e o time vai atacar; ele conhece o plano e a ordem de entrada.
**Opções:**
- **Entrar depois do espaço seguro** — ser o 3º ou 4º — melhor porque o C4 não deve cair em lugar difícil de recuperar; custa o tempo. <!--b:ficar_perto,esperar-->
- **Ir com o grupo** — andar no meio do pack — melhor para proteção; custa a velocidade. <!--b:reagrupar-->
- **Passar o C4 a quem vai plantar** — dropar para o colega — melhor quando sou o entry ou estou ferido; custa o aviso. <!--b:passar_c4-->
- **Plantar cedo se o espaço está livre** — oportunidade — melhor porque economiza tempo; custa a cobertura. <!--b:plantar-->
- **Chamar o estado do C4** — avisar onde está — melhor sempre; custa nada. <!--b:call-->
- **Ficar escondido enquanto o espaço não é tomado** — esperar na retaguarda — melhor se o caminho está perigoso; custa o tempo. <!--b:esconder-->
- **Largar o C4 em ponto combinado** — deixar para outro buscar — melhor quando vou entrar; custa o risco de ninguém pegar. <!--b:passar_c4,call-->
**O que pesa na escolha:** quem entra primeiro; o plano de plant; o tempo; a ameaça no caminho; minha vida e arma.
**Erros de quem avalia mal:** o portador entra primeiro e morre; a bomba cai e ninguém pega; o portador está longe do pack.
**Cena:** Situação: execução em A com o C4 no terceiro. / Esperado: o portador entra depois de o espaço estar tomado.

### S87. O C4 caiu (o portador morreu): quem pega
**Quando:** o portador morreu e o C4 está no chão; o jogador sabe onde e vê ou não vê inimigo ali.
**Opções:**
- **Pegar logo** — correr e apanhar — melhor se o lugar é seguro; custa o risco. <!--b:pegar_arma,ir_correndo-->
- **Vigiar e pegar com cobertura** — um olha, outro pega — melhor com CT por perto; custa o tempo. <!--b:vigiar_c4,cobrir_colega-->
- **Granada para cobrir** — smoke para pegar — melhor se há linha; custa a smoke. <!--b:smoke-->
- **Chamar o colega mais perto** — pedir para pegar — melhor se estou longe; custa o aviso. <!--b:call,passar_c4-->
- **Mudar o plano sem o C4** — virar o round — melhor quando a bomba está perdida; custa o plant. <!--b:virar_round-->
- **Fazer do C4 a tarefa nº 1 do grupo mais próximo** — o grupo recupera — melhor porque sem C4 o TR não vence por explosão; custa o resto. <!--b:reagrupar-->
- **Esperar o CT se afastar** — deixar o CT sair da linha — melhor se o relógio permite; custa o relógio. <!--b:esperar-->
**O que pesa na escolha:** onde caiu; quem está perto; o CT; o tempo; granadas.
**Erros de quem avalia mal:** o C4 fica esquecido; vão um a um.
**Cena:** Situação: o portador morre no meio e o C4 fica no chão. / Esperado: alguém pega com cobertura ou o time muda o plano; o C4 não fica esquecido.

### S88. CT: o C4 está no chão e o TR vai buscar
**Quando:** o C4 caiu (ou foi largado) e o CT sabe onde; os TR querem recuperá-lo.
**Opções:**
- **Vigiar com linha sobre o C4** — segurar do ângulo — melhor porque quem vem pegar se expõe; custa ficar preso ao ponto. <!--b:vigiar_c4,esperar-->
- **Granada no C4** — molotov/HE — melhor para negar a recuperação; custa a granada. <!--b:molotov,he-->
- **Pegar o C4 e levar para longe** — apanhar e afastar — melhor se seguro; custa o risco. <!--b:pegar_arma-->
- **Emboscar quem vem** — esperar fora da linha — melhor porque o TR precisa passar; custa a posição. <!--b:esperar,cruzado-->
- **Ignorar** — seguir o plano — melhor se há ameaça maior; custa o C4 dar a volta. <!--b:largar_objetivo-->
- **Chamar a posição do C4** — avisar — melhor sempre; custa nada. <!--b:call-->
- **Dar a volta e pegar por trás** — flanquear quem vigia — melhor com tempo; custa o tempo. <!--b:dar_volta-->
**O que pesa na escolha:** onde o C4 está; o TR; o tempo; granadas.
**Erros de quem avalia mal:** esquece o C4; vai sozinho pegar à vista.
**Cena:** Situação: C4 no chão vigiado por um CT. / Esperado: granada ou espera; o CT não corre para pegar à vista.

### S89. Plantar com gente perto (e escolher o lugar)
**Quando:** o portador está no bomb com possíveis CT por perto; precisa plantar em alguns segundos.
**Opções:**
- **Smoke e plantar** — cobrir e plantar — melhor porque o plant é rápido; custa a smoke. <!--b:smoke,plantar-->
- **Plantar com cobertura de colega** — os outros olham — melhor com apoio; custa a atenção. <!--b:plantar,cobrir_colega-->
- **Plantar recuado** — em lugar protegido — melhor porque o pós-plant é bom; custa o tempo. <!--b:plantar-->
- **Plantar à vista** — rápido — melhor quando o CT não vê; custa o risco. <!--b:plantar-->
- **Esperar limpar** — limpar antes — melhor com tempo; custa o tempo. <!--b:esperar,tomar_espaco-->
- **Fake de plant** — fingir plantar para tirar — melhor com colega; custa o tempo. <!--b:fingir-->
- **Passar o C4** — outro planta — melhor se estou ferido; custa o aviso. <!--b:passar_c4-->
- **Plantar "para fora"** — no lado visível do corredor onde vou segurar — melhor para o pós-plant; custa a segurança. <!--b:plantar-->
**O que pesa na escolha:** quantos CT; pós-plant; granadas; tempo.
**Erros de quem avalia mal:** planta à vista e morre; planta no lugar ruim.
**Cena:** Situação: TR no bomb, CT possível na janela. / Esperado: smoke ou cobertura; planta num lugar com pós-plant.

### S90. CT: cortar o plant
**Quando:** o CT vê ou ouve o TR plantando; ainda dá para parar.
**Opções:**
- **Atirar no plantador** — spray — melhor se há linha; custa a posição. <!--b:abrir_seco,spray_atraves-->
- **Granada no C4** — molotov/HE — melhor para cancelar; custa a granada. <!--b:molotov,he-->
- **Abrir em dupla** — dois — melhor com colega; custa o aviso. <!--b:abrir_dupla-->
- **Chamar** — avisar — melhor sempre; custa nada. <!--b:call-->
- **Flash e entrar** — bang e matar — melhor com flash; custa a flash. <!--b:bangar_entrar-->
- **Deixar plantar e retomar** — esperar o plant — melhor se o retake é bom; custa o plant. <!--b:esperar-->
- **Mudar de ângulo** — outro ponto para ver o C4 — melhor se o atual é ruim; custa o tempo. <!--b:mudar_angulo-->
**O que pesa na escolha:** tempo para plantar; número; linha; granada.
**Erros de quem avalia mal:** vai tarde; não vê.
**Cena:** Situação: TR planta com CT na janela. / Esperado: o CT atira ou joga granada; não deixa passar à toa.

### S91. Fake de defuse e como reagir a ele
**Quando:** o CT começa o defuse para tirar o TR do esconderijo (ou o TR ouve o som e desconfia); o som de defuse existe.
**Opções:**
- **CT: tap e sair** — começar e parar — melhor para puxar o TR e matá-lo; custa o tempo da bomba. <!--b:fake_defuse-->
- **CT: defuse real com cobertura** — ficar de verdade — melhor se o TR não vem e há tempo; custa o risco. <!--b:defusar-->
- **CT: smoke antes do fake** — esconder o C4 — melhor para o fake funcionar; custa a smoke. <!--b:smoke-->
- **TR: abrir ao primeiro som** — correr ao C4 — melhor quando o tempo é pouco; custa a vida se for fake. <!--b:abrir_seco-->
- **TR: esperar o segundo som** — confirmar que o defuse continua — melhor porque o fake é curto; custa o risco do defuse real. <!--b:esperar,ouvir-->
- **TR: granada no C4** — molotov/HE — melhor para cancelar sem se expor; custa a granada. <!--b:molotov,he-->
- **TR: mudar de lugar** — sair do ponto pré-mirado — melhor se o CT sabe onde estou; custa o ponto. <!--b:mudar_angulo-->
**O que pesa na escolha:** tempo do defuse e da bomba; vivos; o que o CT já fez nesta partida; granadas; a posição de cada um.
**Erros de quem avalia mal:** abre ao primeiro som e cai no fake; defusa à vista sem cobrir.
**Cena:** Situação: som de defuse que para. / Esperado: o TR não abre ao primeiro; espera o segundo som ou usa granada.

### S92. Passar ou largar o C4 (e desistir de plantar)
**Quando:** o portador vai entrar na linha, está ferido ou o plano mudou; o C4 pode ir para outro jogador.
**Opções:**
- **Passar ao colega que vai plantar** — dropar para ele — melhor quando sou o entry ou estou ferido; custa o aviso. <!--b:passar_c4,call-->
- **Plantar logo** — em qualquer lugar seguro — melhor se o tempo aperta; custa o plant bom. <!--b:plantar-->
- **Levar até o fim** — manter o C4 — melhor se o caminho é seguro; custa o risco. <!--b:ir_silencio-->
- **Largar e proteger** — deixar o C4 e cobrir o colega que o pega — melhor se vou lutar; custa o risco de ninguém pegar. <!--b:passar_c4,cobrir_colega-->
- **Esperar o espaço** — aguardar o espaço ser tomado — melhor porque o plant precisa de cobertura; custa o tempo. <!--b:esperar-->
- **Chamar onde está o C4** — avisar — melhor sempre; custa nada. <!--b:call-->
- **Desistir do plant e salvar** — guardar armas — melhor sem chance; custa o round. <!--b:guardar_arma-->
**O que pesa na escolha:** tempo; vivos; função; plano; quem está mais perto do bomb.
**Erros de quem avalia mal:** o C4 fica com quem morre; ninguém sabe onde está.
**Cena:** Situação: o portador está ferido e vai entrar. / Esperado: ele passa o C4 antes de entrar.

## 4. Opções que se repetem (as jogadas-base)

Cada linha: jogada-base, o que é, e as situações em que aparece como opção (números S). Esta é a lista de ações que o bot precisa saber executar. Ordenadas das que aparecem em mais situações para as que aparecem em menos.

- **Esperar** (`esperar`, 78 situações) — segurar a posição com a mira pronta e deixar o tempo ou o adversário vir. Aparece em: S4, S5, S6, S7, S8, S9, S10, S11, S12, S13, S14, S15, S16, S17, S18, S19, S20, S21, S22, S23, S24, S25, S26, S28, S29, S30, S31, S32, S33, S34, S36, S37, S38, S39, S40, S41, S44, S45, S47, S48, S49, S50, S52, S53, S54, S55, S56, S57, S58, S59, S61, S62, S63, S64, S65, S66, S67, S70, S71, S72, S73, S74, S75, S76, S77, S78, S79, S81, S83, S84, S85, S86, S87, S88, S89, S90, S91, S92.
- **Dar call** (`call`, 60 situações) — informar o time (lugar, número, arma, idade) ou pedir algo a alguém. Aparece em: S2, S4, S10, S13, S14, S15, S17, S19, S20, S22, S23, S25, S27, S28, S30, S32, S33, S35, S36, S37, S38, S40, S41, S42, S44, S45, S46, S47, S48, S49, S50, S51, S52, S53, S54, S55, S58, S61, S62, S63, S64, S65, S66, S67, S68, S69, S70, S71, S72, S75, S80, S81, S82, S84, S85, S86, S87, S88, S90, S92.
- **Jogar molotov** (`molotov`, 47 situações) — negar chão, tirar de canto, atrasar avanço ou cancelar defuse. Aparece em: S3, S7, S9, S14, S15, S20, S21, S24, S25, S26, S27, S29, S31, S32, S33, S34, S38, S39, S40, S42, S43, S44, S45, S47, S48, S51, S52, S53, S56, S57, S58, S60, S62, S63, S64, S66, S67, S69, S75, S76, S77, S79, S81, S83, S88, S90, S91.
- **Jogar smoke** (`smoke`, 39 situações) — fechar ou cobrir uma linha, uma rotação, um plant ou um defuse. Aparece em: S3, S4, S6, S7, S8, S9, S11, S14, S15, S22, S24, S25, S29, S33, S38, S40, S42, S43, S45, S46, S47, S51, S56, S59, S60, S62, S63, S65, S66, S67, S68, S69, S70, S78, S80, S81, S87, S89, S91.
- **Dar a volta** (`dar_volta`, 38 situações) — chegar ao alvo por outro caminho (flanco, rota alternativa, por trás). Aparece em: S6, S7, S10, S13, S17, S18, S19, S20, S21, S22, S24, S27, S33, S34, S40, S41, S43, S46, S47, S51, S52, S57, S59, S62, S63, S64, S66, S67, S69, S72, S73, S75, S76, S77, S78, S82, S83, S88.
- **Jogar flash** (`flash`, 38 situações) — cegar o adversário para mim, para o colega ou como contra-flash. Aparece em: S3, S4, S5, S6, S8, S10, S11, S15, S16, S17, S20, S21, S22, S24, S25, S27, S29, S31, S34, S38, S39, S43, S44, S46, S49, S53, S56, S57, S58, S59, S60, S61, S63, S65, S69, S75, S76, S77.
- **Mudar de ângulo** (`mudar_angulo`, 33 situações) — sair do ponto onde já fui visto e reaparecer em outro, na mesma região. Aparece em: S4, S9, S11, S14, S16, S20, S22, S23, S25, S27, S28, S30, S32, S33, S38, S44, S45, S48, S49, S53, S54, S56, S58, S63, S65, S73, S74, S76, S77, S79, S81, S90, S91.
- **Jogar HE** (`he`, 32 situações) — dano em quem foi ouvido ou está empilhado. Aparece em: S3, S14, S15, S20, S21, S25, S26, S27, S32, S33, S34, S39, S46, S48, S49, S51, S52, S53, S56, S57, S59, S60, S63, S64, S75, S76, S77, S79, S83, S88, S90, S91.
- **Recuar pra cobertura** (`recuar_cobertura`, 31 situações) — sair da linha e se proteger atrás de quina, caixa ou bomb, sem largar a tarefa. Aparece em: S5, S9, S10, S11, S14, S15, S20, S22, S23, S25, S26, S27, S28, S29, S30, S31, S32, S33, S34, S42, S43, S44, S45, S53, S55, S58, S59, S69, S72, S74, S75.
- **Abrir em dupla** (`abrir_dupla`, 21 situações) — dois peekam juntos, por ângulos diferentes, ou um pula e o outro mata. Aparece em: S8, S12, S18, S21, S23, S24, S25, S29, S34, S39, S40, S46, S52, S56, S57, S64, S66, S67, S71, S75, S90.
- **Guardar arma (save)** (`guardar_arma`, 21 situações) — não gastar, fugir ou guardar arma e granada para o próximo round. Aparece em: S1, S2, S3, S11, S43, S45, S51, S60, S61, S65, S66, S68, S69, S73, S74, S77, S78, S80, S82, S85, S92.
- **Reagrupar** (`reagrupar`, 20 situações) — juntar o grupo antes de agir. Aparece em: S5, S6, S13, S19, S28, S31, S33, S37, S41, S43, S44, S46, S47, S51, S66, S67, S74, S82, S86, S87.
- **Tomar espaço** (`tomar_espaco`, 20 situações) — avançar passo a passo e ganhar terreno ou informação, com função. Aparece em: S5, S7, S9, S10, S13, S14, S15, S18, S19, S28, S55, S62, S65, S68, S70, S71, S72, S74, S81, S89.
- **Fingir** (`fingir`, 19 situações) — fazer barulho, granada ou jiggle falso para puxar a atenção ou a rotação do adversário. Aparece em: S13, S15, S18, S19, S34, S39, S42, S43, S52, S56, S57, S64, S65, S71, S76, S77, S79, S80, S89.
- **Bangar e entrar** (`bangar_entrar`, 17 situações) — jogar a flash (a própria ou a do colega) e entrar no estouro. Aparece em: S10, S11, S12, S21, S23, S24, S29, S32, S38, S39, S50, S61, S63, S67, S74, S76, S90.
- **Ir correndo** (`ir_correndo`, 17 situações) — ir à velocidade cheia, aceitando o barulho, quando o tempo vale mais que o silêncio. Aparece em: S5, S6, S7, S8, S18, S19, S20, S26, S40, S41, S47, S59, S71, S73, S78, S80, S87.
- **Parar e ouvir** (`ouvir`, 17 situações) — ficar parado para triangular passos, recargas e granadas. Aparece em: S6, S8, S10, S13, S15, S16, S27, S42, S48, S50, S56, S62, S76, S77, S79, S83, S91.
- **Jogar o relógio** (`jogar_relogio`, 16 situações) — deixar o tempo trabalhar a favor, sem forçar duelo. Aparece em: S12, S18, S21, S24, S63, S65, S66, S71, S72, S73, S74, S75, S76, S78, S79, S81.
- **Esperar a informação ou a call do time** (`esperar_call`, 15 situações) — aguardar, confirmar ou atualizar a crença com o que chega do time. Aparece em: S1, S4, S7, S14, S17, S29, S37, S41, S47, S49, S51, S53, S54, S55, S56.
- **Rotacionar** (`rotacionar`, 15 situações) — ir de uma região a outra por informação, com caminho e tempo reais. Aparece em: S6, S9, S17, S18, S33, S42, S43, S44, S47, S49, S51, S54, S56, S58, S72.
- **Ficar perto do colega pra trocar** (`ficar_perto`, 13 situações) — me manter na distância de troca do colega com ângulo próprio. Aparece em: S4, S5, S7, S28, S29, S31, S46, S71, S73, S74, S75, S83, S86.
- **Abrir seco** (`abrir_seco`, 12 situações) — peek largo sem granada nem apoio, assumindo o risco. Aparece em: S21, S24, S32, S39, S42, S50, S52, S53, S64, S79, S90, S91.
- **Cobrir o colega** (`cobrir_colega`, 12 situações) — olhar o ângulo, o flanco ou as costas que o colega não vê. Aparece em: S16, S22, S29, S30, S66, S68, S70, S75, S80, S87, S89, S92.
- **Spray através de smoke/parede** (`spray_atraves`, 12 situações) — atirar em posição provável através de smoke, parede ou caixa. Aparece em: S21, S27, S30, S39, S44, S49, S52, S56, S57, S58, S64, S90.
- **Virar o round** (`virar_round`, 12 situações) — trocar de bomb ou de plano por informação nova. Aparece em: S4, S6, S18, S34, S37, S42, S43, S55, S56, S57, S71, S87.
- **Jiggle / peek curto de informação** (`jiggle`, 11 situações) — exposição de meio segundo para ver ou ser visto e voltar. Aparece em: S5, S7, S8, S9, S10, S15, S21, S39, S55, S67, S74.
- **Pegar, dropar ou pedir arma** (`pegar_arma`, 11 situações) — arma do chão, drop para o colega, pedido de drop, kit. Aparece em: S1, S2, S28, S31, S35, S36, S68, S84, S85, S87, S88.
- **Trocar o colega** (`trocar`, 11 situações) — matar quem matou o colega, em poucos segundos. Aparece em: S5, S20, S23, S29, S32, S40, S46, S49, S69, S71, S73.
- **Montar cruzado** (`cruzado`, 9 situações) — dois ou mais em ângulos que só se enfrentam juntos. Aparece em: S5, S11, S12, S16, S36, S44, S62, S63, S88.
- **Largar e ir pro objetivo** (`largar_objetivo`, 9 situações) — deixar o duelo, a arma ou o inimigo e ir ao C4, ao bomb ou ao plant. Aparece em: S22, S23, S36, S50, S77, S80, S83, S84, S88.
- **Desarmar** (`defusar`, 8 situações) — começar ou terminar o defuse, com cobertura e conta de tempo. Aparece em: S66, S68, S70, S75, S76, S78, S85, S91.
- **Esconder-se e ficar em silêncio** (`esconder`, 8 situações) — ficar quieto em lugar seguro para ser esquecido. Aparece em: S19, S35, S62, S77, S79, S82, S85, S86.
- **Fechar a distância** (`fechar_distancia`, 8 situações) — chegar perto, com cobertura, para o duelo que minha arma ganha. Aparece em: S20, S22, S24, S25, S26, S31, S48, S50.
- **Ir em silêncio** (`ir_silencio`, 8 situações) — andar de shift para chegar sem dar som. Aparece em: S7, S8, S13, S48, S50, S56, S57, S92.
- **Peek com pulo** (`pular_peek`, 8 situações) — passar pulando para estragar a mira de quem espera. Aparece em: S8, S12, S20, S23, S24, S26, S40, S76.
- **Fake de defuse** (`fake_defuse`, 7 situações) — tap no C4 para tirar o inimigo do esconderijo. Aparece em: S67, S68, S69, S70, S76, S78, S91.
- **Plantar** (`plantar`, 7 situações) — plantar o C4 em lugar e momento escolhidos, com ou sem cobertura. Aparece em: S75, S76, S77, S80, S86, S89, S92.
- **Pré-fire** (`prefire`, 7 situações) — atirar num ponto provável antes de ver o inimigo. Aparece em: S9, S11, S16, S21, S39, S48, S50.
- **Pedir granada ao colega** (`pedir_granada`, 6 situações) — chamar um colega específico e pedir a flash, smoke ou molotov que falta. Aparece em: S3, S10, S38, S40, S45, S60.
- **Reposicionar depois do tiro** (`reposicionar`, 6 situações) — mudar de ponto logo depois de atirar ou abater. Aparece em: S12, S25, S28, S45, S78, S79.
- **Segurar a linha de quem matou** (`segurar_linha`, 6 situações) — manter a mira no lugar de onde veio a baixa ou o avanço. Aparece em: S32, S46, S56, S59, S62, S81.
- **Esperar a smoke acabar ou abrir** (`esperar_smoke`, 5 situações) — segurar fora da linha até a granada abrir. Aparece em: S34, S41, S43, S56, S57.
- **Isca de ombro** (`isca_ombro`, 5 situações) — mostrar só o ombro (shoulder peek) para tirar o tiro do AWP e abrir de verdade depois. Aparece em: S11, S12, S21, S24, S73.
- **Vigiar C4 ou arma no chão** (`vigiar_c4`, 5 situações) — segurar a linha sobre o C4 ou a arma caída até alguém vir. Aparece em: S36, S81, S84, S87, S88.
- **Isca e troca** (`isca_troca`, 4 situações) — um aparece e some para o colega matar quem o seguir. Aparece em: S6, S10, S31, S36.
- **Passar ou largar o C4** (`passar_c4`, 4 situações) — entregar o C4 a quem vai plantar ou deixá-lo para o colega. Aparece em: S86, S87, S89, S92.
- **Strafe curto na espera** (`strafe_espera`, 4 situações) — micro-movimentos com a mira na altura da cabeça do pixel de saída. Aparece em: S14, S16, S20, S26.
- **Comprar** (`comprar`, 3 situações) — escolher a compra (tipo, arma, granadas, kit). Aparece em: S1, S2, S3.
- **Pezinho (boost)** (`pezinho`, 3 situações) — subir no colega para ganhar o ângulo alto. Aparece em: S21, S24, S39.
- **Atravessar a smoke** (`atravessar_sozinho`, 2 situações) — entrar pela smoke inimiga, sozinho ou em grupo, quando a crença permite. Aparece em: S56, S57.
- **Segurar o tiro** (`segurar_tiro`, 2 situações) — não atirar para não dar a posição, ou esperar o corpo inteiro. Aparece em: S12, S16.
- **Caçar o save** (`cacar_save`, 1 situações) — ir atrás de quem fugiu com a arma, sozinho ou em dupla. Aparece em: S83.
