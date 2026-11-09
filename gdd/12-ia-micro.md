# IA no micro: diagnóstico e plano de correção

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0003, 0009, 0017, 0019, 0022

_Varredura pedida pelos autores (2026-11-09): os bots pensam bem a tática do time (macro), mas falta independência no jogo de cada um (micro). Este arquivo diz o que causa cada sintoma no código, quanto ele acontece e como corrigir. Caminhos relativos a `prototipo/csmanager/Assets/Game/`._

> As propostas são **[Sugestão]** do agente, a validar. Cada correção deve entrar atrás de uma opção `-exp`, ser medida (ver "Como medir") e só então virar o padrão, porque mexer no micro muda o equilíbrio TR × CT que o treino dos pesos ajustou.

## Resumo

| # | Sintoma que os autores viram | Confirmado? | Causa principal | Prioridade |
|---|---|---|---|---|
| 1 | Depois de um abate de AWP, o próximo põe a cara no mesmo pixel e morre | **Sim**: 33 % dos abates de AWP têm outro abate do mesmo AWP no mesmo lugar em até 6 s (18,5 % nas outras armas) | A cautela depois de um abate não olha a arma; a troca vai em cima do matador; a memória de "morremos aqui" só vale no round seguinte | 1 |
| 2 | Todos marcam o mesmo pixel | **Sim**: dois colegas andando juntos olham para o mesmo ângulo em 17,5 % das vezes | Cada bot escolhe sozinho o ângulo "mais quente" e a quina do caminho mais curto; não há divisão de ângulos andando | 2 |
| 3 | Ficam um atrás do outro e tampam a visão do colega | **Sim**: 9 % do tempo andando há um colega no cone da mira (6 % parado) | Todos seguem o mesmo caminho e só a colisão separa; o cuidado com o colega na frente só existe para quem está segurando posição | 2 |
| 4 | Sempre a mesma rota, a melhor (Dijkstra) | **Sim** | Um único A* com camada de perigo fixa; o destino de cada lugar do plano é um ponto fixo | 3 |
| 5 | Seguem a tática à risca | **Sim, na execução** | Cada bot refaz a sequência de lugares do jogador pro dele; só troca, giro, call e granada interrompem | 3 |
| 6 | Pegam sempre a tática de maior win rate | **Não, quase**: em 187 rounds de compra cheia o TR usou 156 planos diferentes, nenhum mais de 3 vezes | A escolha é variada; mas a "força" de cada plano vai de 0,36 a 2,91 (8 vezes), medida com 8 a 10 rounds cada: ruído vira preferência | 4 |
| 7 | Falta personalidade individual | **Sim** | O jeito de jogar por função (entry agressivo, AWPer calmo, lurker paciente) está desligado por padrão | 4 |

Números: 12 partidas inteiras, `Headless -micro 12 -seed 9100`. Não há referência de jogo profissional para eles: servem para comparar antes e depois de cada correção.

## 1. AWP: não reagem à ameaça de um tiro só

**O que acontece.** Quando um colega morre para uma AWP, quem vem depois pega o mesmo caminho até o mesmo ponto, mira na mesma quina e entra seco no ângulo dela.

**Causas no código:**
- `Sim/Brain.cs:3756` (`CuidadoComMatador`): depois de um abate, o bot escolhe entre esperar o colega, jogar flash ou "ir assim mesmo", com os mesmos pesos para rifle e AWP. O "ir" vale `0,25 + 0,6 × agressividade` (mais pressa e vida cheia), então um bot médio entra seco entre um quarto (com flash) e 40 % (sem flash) das vezes.
- `Sim/Brain.cs:1343` (chance de trocar o colega): quem está a até 420 vai em cima do matador. A única redução por AWP é quando **eu** estou de AWP; o fato de o matador estar de AWP não muda nada. Contra AWP, trocar entrando no mesmo ângulo é morrer igual.
- `Sim/Leitura.cs:64` (`Lembra`): o lugar onde o time morreu só é lembrado nos rounds seguintes (`rodada > x.rodada`). Dentro do round, os colegas não "sabem" que aquele ponto é a linha da AWP.
- `Sim/Perigo.cs:28`: a camada de perigo do caminho é fixa, calculada uma vez a partir de posições pré-definidas do CT. A AWP vista agora não entra nela.
- O que já existe: segurando posição, a AWP vista nos últimos 10 s aumenta o jiggle e corta o wide peek (`Sim/Brain.cs:4198`). Mas isso não vale para quem está andando ou entrando.

**[Sugestão] Correção:**
1. **Linha da AWP no time:** quando uma AWP mata (ou é vista), o time registra para o round a posição do AWPer e o ponto da morte. Vira (a) custo extra temporário no caminho (as células que ele enxerga) e (b) "lembrado" já no mesmo round.
2. **Cautela por arma:** contra AWP, o "ir assim mesmo" cai muito (ex.: ×0,2) e entram duas opções novas: **jiggle para gastar o tiro** (mostrar o ombro, esperar o tiro e entrar enquanto ele recarrega, cerca de 1,5 s) e **outra entrada** (outro ponto da área, ou esperar a smoke/flash).
3. **Troca contra AWP:** se o matador está de AWP e eu teria que entrar no mesmo ângulo, a troca vira jiggle ou flash, não peek seco.

## 2. Todos no mesmo pixel

**O que acontece.** Dois ou três bots andando juntos olham para a mesma quina, e os outros ângulos ficam sem ninguém.

**Causas no código:**
- `Sim/Brain.cs:5323` (`ClearAngles`): cada bot pega o ponto de maior calor à frente dele; não sabe o que o colega ao lado já está olhando.
- `Sim/Brain.cs:2392` (`Pixel`): a mira vai na quina do caminho mais curto até o lugar suspeito. Como o caminho é único, dois bots no mesmo lugar recebem o mesmo pixel.
- O que já existe: segurando posição, `LookAlvo` (`Sim/Brain.cs:1934`) e `PixelSemColega` evitam mirar nas costas do colega, e `Espalhado` (`Sim/Estrategias.cs:493`) espalha os pontos de segurar.

**[Sugestão] Correção:** **divisão de ângulos no time.** Cada ponto conferido por alguém fica marcado na inteligência do time por ~1 s; o próximo bot escolhe o melhor ponto **ainda sem dono**, com bônus para o lado dele (quem está à esquerda olha a esquerda). Pro chama isso de "você pega o perto, eu pego o longe".

## 3. Fila indiana e visão tampada

**O que acontece.** O grupo anda em fila no mesmo caminho; o da frente tampa a mira de quem vem atrás.

**Causas no código:**
- Todos usam o mesmo `NavGrid.Path` (`Map/NavGrid.cs:1160`, chamado em `Sim/Brain.cs:5224`) para o mesmo destino: o caminho é idêntico e só a colisão de corpo (`Sim/Match.cs`) separa.
- O cuidado com colega na frente existe para quem está **parado** (`LookAlvo`, `Livre` na espera em `Sim/Brain.cs`), não para quem anda.

**[Sugestão] Correção:**
1. **Faixa lateral:** quem vem atrás de um colega no mesmo caminho anda deslocado para o lado (40 a 80 unidades, se houver espaço) ou mantém 1 a 2 s de distância.
2. **Não mirar através do colega:** se há um colega no cone da mira e ninguém está em combate, o bot muda de ângulo ou dá um passo para o lado.

## 4. Sempre a mesma rota

**O que acontece.** Do mesmo ponto para o mesmo lugar, o bot faz sempre exatamente o mesmo caminho.

**Causas no código:**
- `Map/NavGrid.cs:1160`: A* exato; a única variação é a camada de perigo, que é **fixa** (`Sim/Perigo.cs:28`).
- `Sim/Estrategias.cs:438` (`Centro`) e `:548` (`Hold`): cada lugar do plano é **um ponto** (o meio da área, guardado em cache). Todos os bots que vão "para o Casarão" vão para o mesmo ponto.

**[Sugestão] Correção:**
1. **Ruído por bot e por round no custo do caminho:** um campo de custo extra pequeno e suave (gerado pela semente do round e do jogador), que muda qual corredor é "o melhor" sem criar caminhos absurdos. Limitar a, no máximo, 10 a 15 % mais longo que o mais curto.
2. **Evitar o caminho do colega:** depois que um colega escolhe um caminho, os próximos pagam um custo leve nas células dele, o que separa o grupo naturalmente.
3. **Ponto de destino variado:** o ponto de cada lugar do plano sorteado entre os bons pontos da área (cobertura, ângulo), e não sempre o meio dela.

## 5. Execução rígida do plano

**O que acontece.** O plano pro vira trilho: cada bot vai lugar por lugar da rota do jogador pro, na mesma ordem e no mesmo ponto.

**Causas no código:**
- `Sim/Igl.cs:636` (`SeguirPlano`): o próximo lugar vem da sequência do pro; só troca, giro, call do IGL e granada têm prioridade. Não há decisão individual de "esperar mais", "pegar outro ângulo" ou "mudar o tempo".

**[Sugestão] Correção:** **liberdade por jogador.** O plano vira intenção (aonde o time vai e quando), e cada bot pode: atrasar ou adiantar alguns segundos, trocar o ponto por outro da mesma área ou parar para segurar quando vê informação que contradiz o plano. Quanto de liberdade depende da função (o lurker mais, o entry menos), da agressividade e dos traços (decisão 0019; o Esquentado se precipita, o Líder segura o grupo).

## 6. Peso da força dos planos

**O que acontece.** A escolha **não** está concentrada (o IGL varia bastante), mas os pesos exageram diferenças que podem ser ruído.

**Causa no código:** `Sim/Igl.cs:381` multiplica pela força medida (`Sim/EstrategiasForca.cs:931`), que vai de 0,36 a 2,91, cada uma medida com poucos rounds ("venceu 4/8").

**[Sugestão] Correção:** encolher a força para perto de 1 quando a amostra é pequena (ex.: `(vitórias + k) / (rounds + 2k)` com k ≈ 5), para a preferência vir de diferença real e não de sorte.

## 7. Personalidade por função desligada

**Causa no código:** `Sim/Agent.cs:39`: o `Skill.Estilo` por função só vale com `-exp estilos`. O comentário diz que, ligado sozinho, ele piorava o retake ("−7"). Sem ele, os bots do adversário jogam todos com o mesmo jeito.

**[Sugestão] Correção:** religar junto com as correções 1 a 5 e medir; se o retake piorar, ajustar o estilo do âncora em vez de desligar tudo. Os traços da decisão 0019 também podem mudar o micro (o Esquentado faz mais wide peek, o Decisivo segura mais o ângulo no clutch).

## Ordem sugerida

1. AWP (item 1), porque é o sintoma mais visível e o mais "anti-humano".
2. Divisão de ângulos e faixa lateral (itens 2 e 3), juntos, porque mexem na mesma movimentação em grupo.
3. Rotas variadas e perigo dinâmico (item 4), que reaproveitam a "linha da AWP" do item 1.
4. Liberdade no plano e personalidade (itens 5 e 7).
5. Força dos planos (item 6), que é pequeno e independente.

## Como medir

```bash
dotnet Headless/bin/Release/net9.0/Headless.dll -micro 12 -seed 9100
```

Mede visão tampada, mesmo ângulo, morrer no mesmo lugar (AWP x outras armas) e a concentração da escolha de tática (`Headless/MedirMicro.cs`, relatório em `Logs/bots/micro.txt`). Rodar antes e depois de cada correção, junto com o validador de sempre (vitórias por lado, plants, trocas), para não melhorar o micro e quebrar o equilíbrio.

## Perguntas em aberto

- Quanto de "eficiência" aceitamos perder em troca de variedade? (rotas mais longas e jogadas fora do plano às vezes dão errado, como com pessoas de verdade)
- Quanto cada bot pode se afastar do plano do IGL (item 5)? Igual para todos ou pela função, agressividade e traços?
