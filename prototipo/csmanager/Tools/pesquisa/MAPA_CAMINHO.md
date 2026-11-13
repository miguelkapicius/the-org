Mapa do caminho dos bots (só leitura, nada foi rodado nem alterado). Raiz usada abaixo: `D:\unity\CSManager\Assets\Game\` (NavGrid = `Map\NavGrid.cs`; Brain, Igl, Places, Perigo, Intel, RotaHumana, PlanoRegioes, Cerebro\* = `Sim\...`).

**Resposta curta.** O A* só conhece distância (mais 1,2x perto de parede e um custo fixo por pulo ou queda) e uma camada de perigo estática. Nenhuma crença entra no custo. As flags de rota estão desligadas por padrão e só valem em `-exp`. O CT quase não tem desvio nenhum. E o vício vem mais do DESTINO do que do A*. Os pontos de juntar e de girar do CT ficam todos na base CT (ou colados nela). Se o destino está na base, qualquer caminho "termina" lá.

## 1. Fluxo: do destino até a lista de pontos

**1.1 Quem pede.** A ordem (`Order.dest` ou `hold.pos`) vira `MoveTo(dest, run)` em `Brain.cs:2455` e `Brain.cs:2474`. São 51 chamadas a `MoveTo` no Brain; todas passam pelo mesmo funil, `Brain.MoveTo` em `Brain.cs:7452`.

**1.2 Escolha do alvo antes do A*, em `MoveTo` (7455).**
- `RotaSubGoal` (`Brain.cs:7322`) pode trocar o destino por um ponto intermediário. Só age se `RotaH` (`rotaHumanaLigado`, 7309) ou se o IGL deu um plano (`rotaDoTime`).
- `CercaBaseCT` (7456) é o pós-plant do TR.
- `AlvoAlcancavel` (7458; `NavGrid.cs:780`) troca um destino em cima de pilha de caixa pelo chão do lado.

**1.3 Cálculo do caminho (`Brain.cs:7464-7473`).** Chama `NavGrid.Path(me.pos, goal, Perigo.Camada(me.team))`. Se vier vazio, tenta de novo com `Snap(goal)`. Recalcula quando:
- não há `path`;
- o destino andou mais de 8;
- saiu do caminho (mais de 60 dos dois pontos vizinhos);
- passaram 5 s (`rotina`). A rotina espera quando o passo já estourou o orçamento e ainda há caminho válido.
- Também recalcula no "destravar" (`Brain.cs:7500-7502`: `pathAt=-9` e `desenroscaAte`).

**1.4 `NavGrid.Path` (`NavGrid.cs:1161`).**
- Se o campo de distância do destino (com a camada) está no cache (`CampoNoCache`, 977): desce o campo célula a célula, escolhendo `min(custo + f[to])` (1192-1204). O empate cai sempre no primeiro vizinho de `NB8` (146), então é determinístico.
- Se não está: A* de `cur` até `g` com `layers[layer]` como custo extra (1183). Se o A* devolve NaN, enfileira o campo e `Path` devolve `null`.
- O pedido de campo inteiro só vira real na 3ª vez (`ValeCampo`, 992-998). Fica numa fila de 24.000 pops por passo (`Enfileira` 666, `AvancaFila` 700, `FILA_POPS_POR_PASSO`).
- A saída passa por `Cantos` (1209): corta cantos com `Straight` (1239, confere a largura do corpo, o degrau e a escada) e usa busca binária. Pulo, queda e escada viram waypoints próprios. O último ponto é forçado a ser o `b` exato (1232).

**1.5 Seguir a lista.** O avanço de `wp` está em `Brain.cs:7489-7490`. Tem tratamento de salto (7519), de degrau (7530) e de molotov no caminho (7559). A velocidade desejada sai em `Brain.cs:7561`.

**1.6 Função de custo.**
- A malha é montada em `NavGrid.cs:301-327`. Custo da aresta = `octil * CELL(=2) * wallK * mul + extra`.
  - `wallK` = 1,2 se a folga for menor que `RADIUS+3`.
  - `extra` = 4 (queda), 8 (pulo), 14 (pulo agachado). Salto sobre vão = distância + 10 (386).
- A altura só entra para liberar ou bloquear a ligação (STEP, DROP, JUMP). Não pesa o custo.
- A camada soma `ex[célula de onde sai]` (`NavGrid.cs:602`, `1078`). O array é `layers[4]` (145): 0 = nenhuma, 1 = perigo pro TR, 2 = perigo pro CT. O índice 3 está livre.
- `Perigo.Build` (`Perigo.cs:21-31`) preenche as camadas uma vez por processo. Cada célula custa `CUSTO(2,5) * visto`, com visto = 1 (rifle) ou 2 (linha da AWP, só na camada T). Uma célula andada custa ~2, então uma célula vista custa ~4,5 (2,25x).
  - A camada do CT vem de 8 posições de segurar do TR (`Perigo.cs:29`): topo do meio, meio, rampa de baixo, dobra da rampa, sala do palácio, dobra do casarão, curta de cima e conector.
  - Cobre meio, conector e curta. Não cobre base CT, mercado, viela nem rua do CT.
  - Resultado: para o CT, ir pelo meio custa mais e ir pela própria base sai de graça.
  - A camada também aceita `anguloCustoLigado` (`Perigo.cs:53-57`, opt-in), que encarece quem abre muitos ângulos de uma vez.

**1.7 Cache e orçamento (`NavGrid.cs`).**
- Cache de campos: `CACHE_MAX=400` (127), por thread no headless (`PorRound`, 106-109) e zerado por round (`NovoRound` 120).
- `fixos` (129, 135): campos da camada 0 de plants, centros dos bombs, spawns, **todo `Hold` do Places e o centro de toda área**. O cálculo está em `Match.cs:509-520`. Qualquer "distância até o centro de uma área" ou "até uma posição de segurar" é leitura, sem Dijkstra. Esses campos são só da camada 0.
- Memória de campos entre rounds (621-647).
- Orçamento de A* por passo: `ORC_NOS_PASSO=30000` (870). `Estourou` (874) faz o `PathLen` virar `Estimativa` (882) e o `Path` com `soSeCouber` devolver `null`.
- `DaOrigem` (898): vários destinos da mesma origem no mesmo passo viram um Dijkstra contínuo.
- Cache de pares `pares` (815, até 200 mil).
- Heurística ALT: 16 marcos, 4 ativos (402-403, 1031). Cada camada tem os seus marcos (408, `MarcosDaCamada` 434).
  - **Atenção:** `SetLayer` (554) refaz 16 Dijkstras do mapa inteiro. Não serve para uma camada dinâmica de crença.

## 2. Onde já existe alguma noção de alternativa, perigo ou desvio

| O que | Onde | Faz o quê | Flag |
|---|---|---|---|
| Camada de perigo | `Perigo.cs:21-60`, usada em `Brain.cs:7471` (mais 3461, 4907, 6788, 7274) | Custo extra por célula vista de onde o inimigo costuma segurar. Estático. | Sempre ligada |
| Ângulo-custo | `Perigo.cs:53` | Penaliza abrir muitos ângulos de uma vez | `anguloCustoLigado` |
| Rota humana | `Brain.cs:7305-7335` e `7389-7424` (`PlanejaRota`) | Para destino a mais de 350 de distância reta, sorteia uma sequência de regiões com as transições de pros (`RotaHumana.Proximas`, `RotaHumana.cs:127`). Tem filtro anti-colega (`usadas`, 7396, peso `exp(-0,9*sobre)`) e filtro de volta "< 1,9x a reta" (7417). A* só entre as regiões. | `rotaHumanaLigado` (está no exe de teste, `Tools\bots\flags_teste_exe15.txt`) |
| Exceções da rota humana | `Brain.cs:7327-7329` | Não se aplica a: TR com push, plant ou C4; qualquer `order.defuse`; `target != null`; `order.doPlano`; fases de troca, ajuda e fogo; destino a menos de 350 (reta). | — |
| Plano do time por regiões | `Igl.cs:1069-1080`, `PlanoRegioes.cs:208`, `Candidatas` em 261, `Amontoa` em 246 | O IGL distribui rotas por jogador sem 3+ na mesma região (anti hive mind). Candidatas = rotas do pro + o "caminho direto" com peso 0,15. Chamado no setup (1061), no retake (1871) e na rotação (2186). | `planoRegioesLigado` (não aparece em `flags_teste_exe15.txt`) |
| Desvio da base CT (`c4Desvio`) | `Brain.cs:7337-7388` | **Só TR** (7327). Se o caminho direto passa por área "CT" e a crença de CT lá é ≥ 0,2, testa 10 pontos (`DESVIO_PONTOS`, 7343). Escolhe o menor `PathLen(a,q)+PathLen(q,b)` sem área "CT" nas pernas. Aceita se ≤ 1,6x (1,9 a 3,3x com certeza alta e relógio). A crença é `Esperados`, contatos e `PerigoRegiao("CT")` (7357-7359). | `c4DesvioLigado` (está no exe de teste) |
| Cerca da base CT | `Brain.cs:7431-7450` | Pós-plant do TR: para na borda da base CT | `ppAdultoLigado` |
| Retake por portas | `Places.cs:83` (`PortasDoBomb`), `Igl.cs:1839-1869`, `1936-1950` | Portas tiradas do mapa. Custo da porta = `PathLen` + penalidade de dividir + `Esperados(junta,220)` + `CruzaBomb` (+3000) + ETA impossível (+5000). Depois da ordem "entrando pela X" (dest = `pt.entra`), que é um via-point por encadeamento de ordens. | `bxRetakeEntradasLigado` (não aparece em `flags_teste_exe15.txt`) |
| `CruzaBomb` | `Igl.cs:2645` | Amostra o `Path` de 8 em 8 e vê se cruza a área do bomb | `bxRetakeCruzaVelho` desliga |
| Roda pela crença | `Igl.cs:2592-2644` | Decide **se** gira, não por onde | `bxRodaCrencaLigado` |
| Pontos de peek | `Brain.cs:7272` (`PontoNoCaminho`) | Ponto a d unidades no caminho | — |

Observações sobre as flags:
- O padrão sem `-exp` é nenhuma flag (`Igl.cs:17`, `ExpDaLinha`).
- Eu só pude conferir `Tools\bots\flags_teste_exe15.txt`. Não confirmei as flags dos exes 16 e 17: `mk_bat16.py` e `mk_bat17.py` só acrescentam `cerebroNovoCT`, `crenca2Ligado`, `macro2Ligado` e `retake2Ligado` à lista do exe anterior.
- **O CT não tem `c4Desvio` nem equivalente.** Pelas flags que conferi, a única rota "humana" que alcança o CT é a `rotaHumanaLigado` com dados pro do CT, mais o `planoRegioesLigado` se estiver ligado.

## 3. Por que A↔B e o retake do CT passam pela base CT

**3.1 O destino já é a base CT.**
- `Places.RetakeStage` (`Places.cs:112`) é a lista fixa que o retake usa quando `bxRetakeEntradasLigado` está desligado (`Igl.cs:1839`).
  - A: rua do CT (28,2) ≈ (712,62), plataforma (36,2.6) ≈ (912,77), praça (22,7.5) ≈ (562,200).
  - B: viela (6,1.5) ≈ (162,50) e mercado (11,4) ≈ (287,112).
  - Todos com y ≤ 200 (a metade CT; o spawn CT é (528,70)).
- O mesmo `RetakeStage` é o destino da rotação:
  - `Igl.cs:2184`, `2237`, `2637` (`st[rng.Int(st.Length)]`);
  - `2117` (giro sozinho);
  - `3080` (âncora; a versão "devagar" pega o ponto mais longe).
  - Ou seja, sorteio puro entre pontos que ficam na base CT ou colados nela. Nenhum é "o ponto onde eu quero chegar".
- `Igl.cs:1869` manda `juntar pra retomar o X` para esse ponto. Depois `retomar limpando` (1938) entra pelo lado do ponto de junta (`bombPos + dir*(35..80)`). Sem a flag, todo mundo entra do lado CT.
- Do B para esses destinos, o caminho mais curto da malha é pela viela ou mercado e pela base. É o `rotAB`/`rotBA` = 13,2 s (≈ 825 un a 62,5 un/s) em `Logs\bots\analise_mapa.txt:21` (código em `AnaliseMapa.cs:141-142`, "por onde for").

**3.2 Mesmo com `bxRetakeEntradasLigado`, a junta tende ao lado CT.**
- `PortasDoBomb` (`Places.cs:83-111`) acha cada porta pelo `Path` camada 0 do centro de **cada área do mapa** até o bomb, e fica com a primeira porta vista (`Places.cs:105` mantém a primeira junta, sem escolher a melhor).
- Filtra com `n >= 2` (107). Se nada sobrar, cai em `RetakeStage` (108).
- A junta fica ~100 antes da entrada (103).
- Na análise (`analise_mapa.txt`):
  - A tem 5 entradas, 2 "lado CT" (descida do CT e plataforma).
  - B tem 5 entradas, 2 "lado CT" (descida do mercado e escada da banca).
  - As outras juntas ficam no lado TR (por exemplo rampa (880,402), tapete (125,450)). O CT só as escolhe se `CruzaBomb` e o ETA deixarem.

**3.3 A camada de perigo do CT reforça a base.** Ver 1.6: mercado, viela, rua do CT e base CT não são vistos pelas 8 posições do TR, então custam 2 por célula. Meio e conector custam 4,5.

**3.4 Alternativas e números.**
- Caminhos entre os bombs que existem no mapa (`Places.cs`):
  - praça/meio (`CT_Praca`, `CT_MeioCaixa`, `Meio_Sul`, `Meio_Norte`);
  - janelão (`CT_Janelao`);
  - conector (`CT_Conector`, `T_Conector`);
  - rato (`CT_Rato`);
  - mercado e viela (base);
  - curta, tapete e casarão (no lado TR).
- **Não há no código nem em `Logs\bots\analise_mapa.txt` o comprimento A↔B por cada via.** A análise só dá a rotação mais curta (13,2 s) e os tempos de spawn (CT: janelão 3,3 s, meio norte 3,7 s, praça 3,3-ish; TR: meio sul 5,5 s, meio norte 9,5 s).
- Há um número real indireto: `Logs\bots\ao0_8100.txt` (linhas ~1018-1028, seção "desvio da base CT"). É o TR, não o CT, mas mede a mesma geometria.
  - Em 69 casos em que o caminho direto cruza a base CT, a volta pelo Conector deu 0,9x a 1,5x o caminho direto.
  - Distribuição: 1,0x em 24,6%, 0,9x em 11,6%, 1,1x em 10,1%, 1,2-1,5x nos poucos restantes.
  - Em 33% dos casos não houve volta boa.
  - Isso sugere que meio/conector/rato custam quase o mesmo que a base; o que muda é a camada de perigo.
- Limite inferior em linha reta (coordenadas do log, sem parede): plant A (850,200) → conector do CT (638,325) → rato (398,288) → entrada da curta (248,312) → plant B (162,262) dá ≈ 740 un. É só um limite. O comprimento real precisa ser medido com `PathLen(A,q)+PathLen(q,B)` para `q` ∈ {Conector, Rato, Meio, Praca, Janelao}, que são leituras de campo fixo.

## 4. Onde encaixar uma escolha de caminho por avaliação

**4.1 O que já existe e serve.**
- **Via-point.** Não há `PathVia`. O padrão é compor duas chamadas, como em `Brain.cs:7366` (`Path(a,q)` e `Path(q,b)`) e `7372` (soma de `PathLen`).
  - Para fazer o bot andar por `q` sem juntar listas, existem:
    - `Brain.DefinirRota(regs, goal)` (`Brain.cs:7315`) e `rotaWps` (7310, 7322-7335): lista de waypoints por nome de região (`Estrategias.Centro`, `Estrategias.cs:113`), consumidos waypoint a waypoint em `RotaSubGoal` com A* entre eles;
    - o padrão de estado `desvioUsa/desvioPonto/desvioPassou` (`Brain.cs:7344-7387`);
    - o encadeamento de ordens "entrando pela" (`Igl.cs:1940-1958`).
- **Medir comprimento (camada 0).**
  - `NavGrid.PathLen` (800), `PathLenAte` (830) e `DistanciasAte` (1111, vários alvos de uma origem).
  - `DistField(alvo)` (558) com `NoCampo` (768): uma origem de cada vez contra um alvo.
  - Os centros de área e as posições de segurar já têm campo fixo (`Match.cs:509-520`), então perna1 + perna2 por via-point sai de graça.
  - `AnaliseMapa.Tempo(de, ate)` (`AnaliseMapa.cs:121`) devolve segundos (usa `MapCheck.RUN`).
- **Custo com camada.** Só `Path` (que devolve a lista) e `DistField(alvo, layer)`. Não existe `PathLen` com camada. Para somar custo de perigo hoje é preciso amostrar a lista.
- **Amostrar um caminho.** Modelos prontos: `Places.cs:97-98`, `Igl.cs:2649-2653` (`CruzaBomb`) e `AnaliseMapa.Fino` (184).
  - Como o `Cantos` devolve poucos pontos, é preciso interpolar entre waypoints a cada ~8-20 un.
- **Crença ao longo do caminho (nada soma isso hoje, só lê num ponto).**
  - `Intel.Esperados(p, r)` (`Intel.cs:356`): por inimigo, por célula, mais caro. Com C=12 e r=220 são ~39x39 células x 5 inimigos por chamada.
  - `Intel.HeatAt` (114): leitura O(1) no calor.
  - `Intel.HeatAvg` (383), `HeatIn` (397), `Provaveis` (338), `PorZona` (321, por zona A/Mid/B).
  - `TeamBrain.PerigoCrenca` (`Igl.cs:637`): `Esperados` + colega morto recente com matador vivo (+0,4) + sons recentes (+0,15 cada, teto 0,6) − colega vivo ali (−0,3). O comentário do código diz "pra escolher caminho… uma vez por destino". Hoje só tem um chamador (`Brain.cs:5298`, decisão do C4). Variantes: `LadoControlado` (652) e `PerigoRegiao` (654).
  - A crença é do time (`m.intel[(int)team]`, `m.igl[(int)team]`), disponível no Brain.
- **Risco estático.** `Perigo.Visto(team, p)` (`Perigo.cs:36`) e `Pixels.Em` (ângulos).
- **Previsibilidade.**
  - `Rotas.Perto(inim, p, t, raio)` (`Rotas.cs:39`): onde o inimigo costuma estar naquele instante (tabela dos pros).
  - `RotaHumana.Proximas` (`RotaHumana.cs:127`): probabilidade de cada rota (quanto menor, mais surpreendente).
  - `PlanoRegioes.Amontoa` e `usadas` (`Brain.cs:7396`): o que os colegas já escolheram.

**4.2 Três pontos de encaixe, do menos ao mais invasivo.**
1. **`Brain.PlanejaRota` / `DesviaBaseCT` (`Brain.cs:7345-7424`).** É por jogador, uma vez por destino (replano a cada 15 s ou se o goal andou 40). É o lugar com crença e relógio à mão. `DesviaBaseCT` já é um protótipo de "candidatos por via-point, comprimento medido, aceitar se ≤ k x direto, limite subindo com a crença", só falta tirar do `me.IsT` (7327) e dar valor. Isso exige tratar as exceções de 7327-7329 (`order.defuse`, `doPlano`, `target`, `< 350`) que hoje deixam o CT em retake e giro sem desvio.
2. **`PlanoRegioes.Candidatas` (`PlanoRegioes.cs:261`) e `DistribuirAte` (208).** Os candidatos e o peso `w` já existem para o time inteiro. É o lugar para somar tempo + risco pela crença + previsibilidade ao `w`. Só vale para quem recebe `PlanoDoTime` (setup, retake, rotação; e só com `planoRegioesLigado`).
3. **A escolha do próprio destino de juntar (`Igl.cs:1845-1866`).** Hoje o custo é `PathLen(a.pos, st[k])` + `Esperados(st[k], 220)` só no ponto final. Para o retake é aqui que o caminho de verdade é decidido. Dá para trocar `PathLen` pela avaliação do caminho inteiro, ou mudar o `RetakeStage` para pontos dos dois lados. A rotação (`Igl.cs:2184/2237/2637`) escolhe `st[rng.Int]` sem avaliar nada.

**4.3 Sem reescrever o A*.**
- `AEstrela` já recebe o array de custo extra por parâmetro (`NavGrid.cs:1010/1019`, uso em 1183), mas `Path` só aceita o índice `layer` (1161).
- Uma sobrecarga de `Path` com `float[] ex` usaria o A* com heurística octil (os marcos ALT só valem para arrays registrados em `layers`, 1034). Perde cache de campo e usa orçamento de A* (30.000 nós/passo).
- Por isso, para comparar 2-3 candidatos, o caminho barato é via-point com campos fixos mais amostragem da crença, em vez de A* com camada dinâmica.
- Não use `SetLayer` para crença: refaz marcos e zera o cache da camada.

## 5. Coisas estranhas

1. **`PathLen` e o caminho andado usam custos diferentes.** O `PathLen` (ETA do IGL em `Igl.cs:1858`, `VagaNo` em `Cerebro\IglCT.cs:131`, custo de porta em `Igl.cs:1859`) é sempre a camada 0. O bot anda pela camada de perigo (`Brain.cs:7471`). O caminho que o IGL "mediu" pode não ser o que o bot anda. `DesviaBaseCT` também testa se "cruza CT" com `Path` sem camada (7351, 7366) e depois o `MoveTo` anda com camada.
2. **`PathLen` não é uma métrica consistente.** Com o orçamento estourado devolve `Estimativa` (`NavGrid.cs:819`, `848`, `882`): reta x razão aprendida, ou x razão média global. Isso viola a desigualdade triangular. Os "0,9x o direto" do log (`Logs\bots\ao0_8100.txt`) são `best/direto` menor que 1, provavelmente desse efeito. Além disso o `PathLenAte` devolve infinito além do limite (839-851).
3. **A junta do retake vem da primeira área iterada** (`Places.cs:105`). Não é a melhor nem a mediana. Com a flag desligada todos os pontos ficam na base CT (3.1). O `retomar limpando` entra do lado do ponto de junta (`Igl.cs:1936-1939`).
4. **A rotação pega ponto aleatório** (`st[rng.Int(st.Length)]`, `Igl.cs:2184`, `2237`, `2637`) sem olhar caminho nem crença. A versão `devagar` (`3080`) escolhe o mais longe (`Longe`).
5. **O filtro "volta absurda" do `PlanejaRota` e do `Candidatas` é em linha reta.** `PlanoRegioes.cs:285` e `Brain.cs:7417` somam distâncias euclidianas entre centros de região, sem considerar parede. Uma rota "por regiões" pode ser muito mais longa de verdade. O mesmo vale para `(goal - me.pos).Len < 350` (`Brain.cs:7328`; `PlanoRegioes.cs:264`).
6. **`DesviaBaseCT` só reconhece a área "CT"** (`Brain.cs:7353`, `7369`, `7370`). Já `TeamBrain.NaBaseCT` (`Igl.cs:2372`) inclui "Mercado". Um caminho que passa pelo mercado sem pisar em "CT" não conta como cruzar.
7. **A camada de perigo do CT é fixa**, calculada de 8 posições de segurar do TR (`Perigo.cs:29`). Não reage a onde o time acredita que o inimigo está e é igual todo round. Só a camada do TR inclui `CT_A_RuaCT` e `CT_B_Viela` (`Perigo.cs:28`).
8. **Empates de rota são determinísticos** (primeiro vizinho de `NB8`, `NavGrid.cs:1196-1201`). Quando duas rotas têm o mesmo custo, todo mundo escolhe a mesma. É a origem do "hive mind" que `rotaHumana` tenta quebrar.
9. **`bxRetakeEntradasLigado` e `planoRegioesLigado` estão desligados** no exe15 (única lista de flags que conferi), onde o retake usa o `RetakeStage` fixo.
10. **Há 3 `layers` vazios no código** (índice 3 sem uso) e `marcosCamada` com tamanho 4 (`NavGrid.cs:145`, `408`). Há espaço para uma 3ª camada, mas só estática (ver 1.7).

## Arquivos principais
- `D:\unity\CSManager\Assets\Game\Map\NavGrid.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Brain.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Igl.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Places.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Perigo.cs`
- `D:\unity\CSManager\Assets\Game\Sim\PlanoRegioes.cs`
- `D:\unity\CSManager\Assets\Game\Sim\RotaHumana.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Intel.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Cerebro\IglCT.cs`
- `D:\unity\CSManager\Assets\Game\Sim\Cerebro\AnaliseMapa.cs`
- `D:\unity\CSManager\Logs\bots\analise_mapa.txt`
- `D:\unity\CSManager\Logs\bots\ao0_8100.txt`
- `D:\unity\CSManager\Tools\bots\flags_teste_exe15.txt`
- `D:\unity\CSManager\Tools\pro\MACRO_CT.md`