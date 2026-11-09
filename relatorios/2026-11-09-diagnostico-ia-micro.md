# Relatório: diagnóstico da IA no micro (2026-11-09)

- **Autor:** Claude (agente do Miguel), a pedido do Miguel
- **Para:** outros agentes e os autores, para analisar e decidir **como** corrigir
- **Base:** código da `main` em `fc08529`, medições em partidas inteiras da simulação sem a Unity
- **Lista de trabalho viva:** `gdd/12-ia-micro.md` (este relatório traz o raciocínio, os números e as impressões; a seção do GDD é o que muda conforme for corrigido)

Caminhos de código relativos a `prototipo/csmanager/Assets/Game/`.

## Como ler

Cada problema tem:
- **O que os autores viram** e **minha impressão** (o que eu acho que está acontecendo, em palavras simples);
- **Confiança** na conclusão: alta (confirmado no código e medido), média (confirmado no código, sem número) ou baixa (hipótese);
- **Números** e de onde saíram;
- **Causas no código**, com arquivo e linha;
- **Solução que eu proporia**, riscos e como validar.

Nenhum número tem referência de jogo profissional: não temos dados de demo pro no mesmo formato. Eles servem para comparar **antes e depois** de cada correção. Onde eu cito "o pro faria", é impressão de como o CS competitivo é jogado, não medida.

## Resumo para quem tem pouco tempo

| # | Problema | Confiança | Número principal | Minha impressão do tamanho |
|---|---|---|---|---|
| 1 | Re-peek no mesmo pixel depois de abate de AWP | Alta | 33,1 % dos abates de AWP têm outro do mesmo AWP no mesmo lugar em até 6 s (18,5 % nas outras armas) | **Grande**: é o que mais parece "bot" |
| 2 | Retake do CT sempre pelas mesmas portas | Alta | 79 % das entradas por duas portas; 15 % pelas costas, aparentemente por acaso | **Grande**: deixa o retake previsível e mais difícil do que devia |
| 3 | Todos no mesmo ângulo | Alta | 17,5 % das amostras de dois colegas andando juntos | Médio-grande |
| 4 | Fila indiana, visão tampada | Alta | colega no cone da mira em 9,2 % do tempo andando (6,1 % parado) | Médio |
| 5 | Sempre a mesma rota | Alta (código) | sem número: é determinístico por construção | Médio-grande, cresce com as horas de jogo |
| 6 | Plano seguido como trilho | Média | sem número | Médio |
| 7 | IGL repete a tática de maior win rate | **Não confirmado** | 156 planos diferentes em 187 rounds de compra cheia | Pequeno: o problema real está em 5 e 6 |
| 8 | Falta personalidade por função | Alta (código) | sem número | Médio |

## Como foi medido

```bash
dotnet Headless/bin/Release/net9.0/Headless.dll -micro 12 -seed 9100
```

Itens 1, 3, 4 e 7 (`Headless/MedirMicro.cs`, 12 partidas, ~2.000 abates, ~47 mil amostras de dupla andando). O item 2 foi medido à parte, em 16 partidas (57 rounds com retake, 94 entradas no bomb); o programa está descrito na seção 2 e vale entrar no `-micro` quando for corrigido.

Definições:
- **Visão tampada:** bot fora de combate com um colega vivo a menos de 350 unidades e a menos de 7° da direção da mira.
- **Mesmo ângulo:** dois colegas fora de combate, os dois andando, a menos de 160 um do outro, com a mira a menos de 10° de diferença.
- **Morreu no mesmo lugar:** depois de um abate, o mesmo atirador mata outro jogador do mesmo time em até 6 s, a menos de 250 unidades de onde o primeiro caiu (só até o round ser decidido).
- **Entrada pelas costas (retake):** a área de onde o CT pisou no bomb depois do plant é do lado do TR (A: Rampa e Palácio; B: Curta e Casarão).

---

## 1. Re-peek no mesmo pixel depois de abate de AWP

**O que os autores viram:** quando um AWP mata alguém, o próximo põe a cara no mesmo pixel e morre igual; ninguém trata o AWP como "um tiro mata".

**Minha impressão:** o bot sabe que **alguém** matou o colega ali, mas não sabe que era **uma AWP**. A reação dele é a mesma de um abate de rifle: às vezes espera, às vezes joga flash, às vezes vai assim mesmo. E quem está perto vai direto trocar, entrando no mesmo ângulo. Contra rifle isso funciona (o pro troca o colega assim), contra AWP é suicídio. Some a isso que a memória do time de "morremos aqui" só começa a valer no round seguinte.

**Confiança:** alta. Está no código e a diferença AWP × outras armas é grande (33 % contra 18,5 %), com 266 abates de AWP na amostra.

**Causas no código:**
- `Sim/Brain.cs:3756` (`CuidadoComMatador`): depois de um abate, escolhe esperar o colega, jogar flash ou "ir assim mesmo". Os pesos não mudam pela arma do matador. O "ir" vale `0,25 + 0,6 × agressividade` (mais pressa e vida cheia): um bot médio entra seco entre um quarto (com flash) e 40 % (sem flash) das vezes.
- `Sim/Brain.cs:1343` (chance de trocar): quem está a até 420 vai trocar; só é reduzido quando **quem troca** está de AWP. O AWP inimigo não reduz nada.
- `Sim/Leitura.cs:64` (`Lembra`): só lembra mortes de rounds anteriores (`rodada > x.rodada`).
- `Sim/Perigo.cs:28`: a camada de perigo do caminho é fixa; o AWP visto agora não entra nela.
- Já existe e funciona em parte: segurando posição, AWP visto nos últimos 10 s aumenta o jiggle e corta o wide peek (`Sim/Brain.cs:4198`).

**Solução que eu proporia:**
1. **"Linha da AWP" no time:** quando uma AWP mata ou é vista, o time guarda para o round a posição do AWPer e o ponto da morte. Ela entra (a) como custo temporário no caminho, nas células que ele enxerga, e (b) como "lembrado" já no mesmo round.
2. **Cautela por arma:** contra AWP, o "ir assim mesmo" cai para algo como ×0,2, e entram duas opções novas: **jiggle para gastar o tiro** (mostrar o ombro, esperar o tiro e entrar nos cerca de 1,5 s de recarga) e **outra entrada** (outro ponto da área, ou esperar smoke/flash).
3. **Troca contra AWP:** se trocar exige entrar no mesmo ângulo de um AWP, a troca vira jiggle ou flash, não peek seco.

**Riscos:** o CT com AWP pode ficar fraco demais se o TR passar a "respeitar" sempre; o TR pode demorar e perder no tempo. Calibrar para o AWP continuar letal num erro.

**Como validar:** "morreu no mesmo lugar (AWP)" deve cair para perto do das outras armas (≈ 20 %), sem a vitória do TR por round sair da faixa atual.

---

## 2. Retake do CT sempre pelas mesmas portas

**O que os autores viram:** com a bomba plantada, o CT sempre vem pelo lado do CT e pelos mesmos caminhos; ninguém vai pelas costas; não há variação. Isso deixa o retake mais difícil e não é o que pessoas fariam, mesmo que às vezes aconteça.

**Minha impressão:** é **estrutural**. O IGL do CT só conhece pontos de encontro do lado do CT. Ele junta o time num desses pontos e entra junto. Quando um CT está do outro lado (lurk, rotação), ele é mandado de volta para uma porta do lado do CT em vez de aproveitar onde está. Os 15 % de entradas pelas costas parecem acaso (quem já estava ali entra pelo caminho mais curto), não uma decisão de flanquear.

**Confiança:** alta. Está no código (não existe ponto de retake pelo lado do TR) e a medição mostra a concentração.

**Números (16 partidas, 57 rounds com retake, 94 entradas):**

| Por onde o CT entrou no bomb | Entradas |
|---|---|
| B pela descida do mercado | 43 % |
| A pela descida do CT | 36 % |
| A pela Rampa | 6 % |
| A pela saída do conector | 6 % |
| B pela entrada da curta | 5 % |
| A pela sacada do palácio | 2 % |
| B pelo tapete | 1 % |

- Entradas pelas costas (lado do TR): **14,9 %**; rounds com pelo menos uma: **21,1 %**.
- Pontos de encontro existentes: A = rua do CT, plataforma e praça; B = viela e descida do mercado. Todos do lado do CT.

**Causas no código:**
- `Sim/Places.cs:73` (`RetakeStage`): 3 pontos no A e 2 no B, **todos do lado do CT**.
- `Sim/Igl.cs:1477-1503`: cada CT vai para a porta mais perto **dele** dentre essas; com folga de 2+ jogadores o time se divide entre elas. Há um pouco de ruído (`rng.Uniform(0, 40)`) e a leitura encarece a porta que deu errado antes, mas sempre dentro das mesmas portas.
- `Sim/Igl.cs:1549`: na hora de entrar, cada um limpa o lado do bomb da porta dele, pelo caminho mais curto (o item 5 vale aqui também).
- `Sim/Igl.cs:1606` em diante: quem recua do bomb para "jogar pelo retake" também vai para um desses pontos.
- O flanco existe em outros lugares (lurker do TR, `Sim/Igl.cs:851-895`; "vários contra um", `Sim/Igl.cs:1814`), mas não no retake.

**Solução que eu proporia:**
1. **Portas pelas costas:** acrescentar pontos de retake do lado do TR (A: rampa e palácio; B: curta e casarão), marcados como "flanco".
2. **Quem está do outro lado entra de lá:** o CT que já está do lado do TR (lurk, rotação) fica com a porta pelas costas se ela for mais perto, em vez de voltar.
3. **Flanco planejado:** com vantagem (ex.: 3+ CTs, ou 2+ a mais que os TRs), um CT pode ser mandado pelas costas de propósito, com chance que depende da agressividade dele e de quanto tempo sobra (dar a volta custa segundos). Entra junto no call, com timing.
4. **Variar a porta principal:** a escolha entre as portas do lado do CT ganha mais aleatoriedade e memória (não repetir a porta do último retake contra o mesmo time).

**Riscos:** dar a volta custa tempo; com pouco tempo de bomba, o flanco chega tarde. A chance de flanquear tem que cair quando o tempo aperta. O TR do pós-plant precisa passar a olhar as costas (hoje ele pode não olhar, porque nunca vem ninguém).

**Como validar:** entradas pelas costas devem subir para uma faixa que os autores acharem realista (ex.: 20-35 % das entradas e retakes de 3+ com pelo menos uma entrada de outro lado com frequência), sem a vitória do CT em retake despencar ou disparar.

---

## 3. Todos no mesmo ângulo

**O que os autores viram:** os bots marcam o mesmo pixel.

**Minha impressão:** cada bot decide para onde olhar como se estivesse sozinho. Dois juntos olham a mesma quina "mais perigosa" e deixam as outras sem ninguém. O pro divide ("pega o perto, eu pego o longe").

**Confiança:** alta.

**Números:** dois colegas andando juntos olham para o mesmo ângulo em **17,5 %** das amostras (47 mil amostras de dupla).

**Causas no código:**
- `Sim/Brain.cs:5323` (`ClearAngles`): escolhe o ponto de maior calor à frente; não sabe o que o colega já está olhando.
- `Sim/Brain.cs:2392` (`Pixel`): mira na quina do caminho mais curto até o lugar suspeito; mesmo caminho, mesmo pixel.
- Já existe para quem está parado: `LookAlvo` e `PixelSemColega` (`Sim/Brain.cs:1934`) e `Espalhado` (`Sim/Estrategias.cs:493`).

**Solução que eu proporia:** **divisão de ângulos no time.** O ponto que um bot está conferindo fica "com dono" por cerca de 1 s na inteligência do time; o próximo escolhe o melhor ponto sem dono, com bônus para o lado dele (quem está à esquerda olha a esquerda).

**Riscos:** baixo; pode deixar um ângulo importante sem ninguém se o "dono" morrer, então o dono precisa sair quando morre ou entra em combate.

**Como validar:** "mesmo ângulo" deve cair bem (ex.: para menos de 8 %), sem aumentar morte "de costas".

---

## 4. Fila indiana e visão tampada

**O que os autores viram:** um atrás do outro, sem se importar de tampar a visão do colega.

**Minha impressão:** todos seguem exatamente o mesmo caminho e só a colisão os separa, então andam em fila. O cuidado de "não ficar na frente da mira do colega" existe, mas só para quem está parado esperando ou segurando.

**Confiança:** alta.

**Números:** colega no cone da mira em **9,2 %** do tempo andando e **6,1 %** parado.

**Causas no código:**
- O mesmo `NavGrid.Path` (`Map/NavGrid.cs:1160`, chamado em `Sim/Brain.cs:5224`) para o mesmo destino: caminho idêntico.
- O cuidado com colega na frente só existe parado (`LookAlvo`, e `Livre` na espera).

**Solução que eu proporia:**
1. **Faixa lateral:** quem segue um colega no mesmo caminho anda deslocado 40 a 80 unidades para o lado, se houver espaço, ou guarda 1 a 2 s de distância.
2. **Não mirar através do colega:** com colega no cone da mira e ninguém em combate, muda de ângulo ou dá um passo para o lado.

**Riscos:** corredores estreitos não têm espaço lateral; aí vale a distância.

**Como validar:** "visão tampada andando" deve cair (ex.: para menos de 4 %).

---

## 5. Sempre a mesma rota (A*/Dijkstra)

**O que os autores viram:** usam o caminho mais curto e nunca variam.

**Minha impressão:** correto. Do mesmo ponto ao mesmo lugar, o caminho é sempre igual: o cálculo é exato e a única coisa que o muda é uma camada de perigo **fixa**. E o destino de cada lugar do plano também é um ponto só. Com o tempo, o jogador decora os caminhos.

**Confiança:** alta (código). Não medi porque é determinístico por construção.

**Causas no código:**
- `Map/NavGrid.cs:1160`: A* exato.
- `Sim/Perigo.cs:28`: camada de perigo fixa por lado.
- `Sim/Estrategias.cs:438` (`Centro`) e `:548` (`Hold`): um ponto por área, guardado em cache.

**Solução que eu proporia:**
1. **Ruído suave por bot e por round** no custo do caminho, gerado pela semente: muda qual corredor é "o melhor" sem criar caminho absurdo (no máximo 10-15 % mais longo).
2. **Evitar o caminho do colega:** quem escolhe depois paga um custo leve nas células do caminho de quem escolheu antes; o grupo se espalha naturalmente.
3. **Destino variado:** sortear o ponto entre os bons pontos da área, não sempre o meio.
4. **Perigo dinâmico:** a "linha da AWP" (item 1) e onde o time morreu neste round entram como custo temporário.

**Riscos:** custo de processamento (o A* hoje usa caches; ruído por bot pode quebrar o cache). Medir o tempo de passo.

**Como validar:** dois rounds iguais (mesmo plano, mesmos jogadores) devem produzir caminhos diferentes; o tempo médio de passo não pode piorar muito.

---

## 6. Plano seguido como trilho

**O que os autores viram:** definem a tática global e eles sempre seguem as mesmas rotas, fielmente.

**Minha impressão:** o plano pro vira trilho. Cada bot refaz lugar por lugar a rota do jogador pro dele, sem decidir nada por conta própria (esperar mais, pegar outro ângulo, adiantar). É bom para o macro parecer real, mas mata a independência no micro.

**Confiança:** média. Está claro no código, mas não medi "quanto" eles se desviam.

**Causa no código:** `Sim/Igl.cs:636` (`SeguirPlano`): o próximo lugar vem da sequência do pro; só troca, giro, call do IGL e granada têm prioridade.

**Solução que eu proporia:** **o plano vira intenção.** Cada bot pode atrasar ou adiantar alguns segundos, trocar o ponto por outro da mesma área ou parar para segurar quando a informação contradiz o plano. Quanto de liberdade depende da função (lurker mais, entry menos), da agressividade e dos traços (decisão 0019).

**Riscos:** o maior de todos para o macro: liberdade demais desmonta a execução. Começar com pouca e medir vitória por lado e hora do plant.

**Como validar:** medir quanto cada bot se afasta do horário e do ponto do pro; plant médio e vitória por lado não podem sair da faixa.

---

## 7. "Pega sempre a tática de maior win rate"

**O que os autores viram:** o IGL pega a tática com mais win rate e segue fielmente.

**Minha impressão:** a **escolha** não é o problema. O IGL varia bastante (há penalidade para repetir plano e um pouco de sorte). O que faz parecer repetitivo é a execução (itens 5 e 6): planos diferentes executados com os mesmos caminhos e os mesmos pontos ficam parecidos. Há um exagero menor: a "força" de cada plano vai de 0,36 a 2,91 e foi medida com poucos rounds.

**Confiança:** alta para "não está concentrado"; baixa para "a força é ruído" (é hipótese pelo tamanho das amostras).

**Números:** em 187 rounds de compra cheia, o TR usou **156 planos diferentes** de 440; nenhum mais de 3 vezes; os 10 mais usados somam 12 % dos rounds. O CT: 154 de 477. Por jogada (decisão 0022), as mais usadas ficam em 7-8 % cada.

**Causa no código:** `Sim/Igl.cs:381` multiplica pela força medida (`Sim/EstrategiasForca.cs:931`).

**Solução que eu proporia:** encolher a força para perto de 1 quando a amostra é pequena (`(vitórias + k) / (rounds + 2k)`, k ≈ 5). Prioridade baixa.

---

## 8. Personalidade por função desligada

**Minha impressão:** parte da "falta de independência" é que todos jogam com o mesmo jeito. O código tem estilos por função (entry agressivo, AWPer calmo, lurker paciente), mas vêm desligados.

**Confiança:** alta (código).

**Causa no código:** `Sim/Agent.cs:39`: `Skill.Estilo` só vale com `-exp estilos`; o comentário diz que, sozinho, piorava o retake ("−7").

**Solução que eu proporia:** religar junto com as correções acima (principalmente a do retake, item 2) e ajustar o estilo do âncora se o retake piorar. Os traços (decisão 0019) também podem mudar o micro.

---

## Ordem que eu sugiro

1. **AWP** (item 1): o mais visível e o mais "anti-humano".
2. **Retake** (item 2): estrutural e localizado (pontos de retake e escolha de porta), dá para corrigir sem mexer no resto.
3. **Ângulos e fila** (itens 3 e 4): juntos, mexem na mesma movimentação em grupo.
4. **Rotas variadas e perigo dinâmico** (item 5): reaproveita a "linha da AWP".
5. **Plano como intenção e personalidade** (itens 6 e 8): o de maior risco para o macro; por último.
6. **Força dos planos** (item 7): pequeno e independente.

Cada correção atrás de uma opção `-exp`, medida com `Headless -micro` e com o validador de sempre (vitória por lado, plants, trocas, retake), e só então padrão.

## Perguntas para quem for analisar

1. Concordam com a ordem? Retake antes de ângulos/fila, por ser mais isolado?
2. Quanto de eficiência aceitamos perder em troca de variedade (rotas mais longas, jogadas fora do plano)?
3. Quanto cada bot pode se afastar do plano do IGL? Igual para todos ou pela função, agressividade e traços?
4. Qual é a faixa "realista" de retakes com alguém pelas costas? (sem dados pro, é decisão de design)
5. Vale buscar dados de demos pro (por exemplo, de onde o CT entra no retake) para ter referência, em vez de ajustar só no olho?
