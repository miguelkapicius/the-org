# Trocas (refrags) e protocolos de meio de round no Counter-Strike profissional

Legenda de confiança usada nas notas:
- **[PRO-CS2]** dado ou fala de nível profissional, era CS2 (2023+)
- **[PRO-CSGO]** nível profissional, era CS:GO
- **[MM-CSGO]** matchmaking/amador, CS:GO
- **[GUIA]** guia de nível amador/semi-pro, sem dado medido
- **[SNIPPET]** texto visto só no trecho do buscador (a página deu 403 ao abrir); conferir antes de tratar como definitivo

Aviso geral: as páginas de estatística do HLTV (jogador, mapa, time) bloqueiam leitura automática (HTTP 403). Várias tabelas de artigos do HLTV e do Substack do nomiun estão em imagem e não puderam ser lidas. O que está abaixo é só o que apareceu em texto.

---

## 1. O que conta como troca nas estatísticas públicas (janela de tempo, taxas típicas)

### Takeaway
A janela padrão de mercado é **5 segundos** (HLTV, Rating 3.0, awpy); análises mais rígidas usam 3 s e o Leetify não publica a janela. Não achei em texto aberto a taxa média de "mortes trocadas" de times pro; o único número de referência pro é o **Trade Kill Success% de ~66% no topo do ranking** (ZywOo 66,5%, kyousuke 65,8%, Big Events 2026).

### Cited Findings
- HLTV, páginas de jogador: "Deaths Traded %" = porcentagem das mortes em que o matador do jogador também foi morto **em até cinco segundos**; "Opening Deaths Traded %" = mesma janela de 5 s só para as mortes de abertura; "Trade Kills %" = parcela dos abates do jogador que foram trocas; "Tradeable Deaths per Round" = mortes em que o matador esteve em outro duelo em até 5 s, tenha o colega matado ou não. **[PRO-CS2][SNIPPET]** — [HLTV, página de jogador (ZywOo)](https://www.hltv.org/stats/players/11893/zywoo)
- Rating 3.0 do HLTV (lançado em agosto de 2025) trata "trade denials" e "failed trades": a troca falha pune o segundo jogador e premia o primeiro **se os dois morrem em até 5 segundos**; trade denial = impedir a morte e negar ao adversário o abate fácil de troca. **[PRO-CS2]** — [Wikipedia: HLTV](https://en.wikipedia.org/wiki/HLTV); mesma janela citada em [Liquipedia: HLTV](https://liquipedia.net/counterstrike/HLTV)
- O HLTV não publica a fórmula inteira; o crédito do Round Swing de cada abate é dividido por dano final, parcela de dano, flash assist e **se foi abate de troca**. **[PRO-CS2]** — [Escorenews, como funciona o Rating 3.0](https://escorenews.com/en/csgo/article/71475-how-hltv-rating-3-0-formula-actually-works-round-swing-and-eco-adjustment-explained); [dust2.us, lançamento do Rating 3.0](https://www.dust2.us/news/64901/hltv-launches-rating-30)
- No artigo do HLTV sobre Round Swing (25/09/2025), um leitor perguntou se o crédito de troca decai dentro da janela de ~5 s; o autor (NER0cs) respondeu que "the 5 seconds thing is fair" e que flash assists já usam decaimento que as trocas poderiam adotar. **[PRO-CS2]** — [HLTV: Finding the most impactful CS2 players](https://www.hltv.org/news/42763/finding-the-most-impactful-cs2-players)
- awpy (biblioteca usada em análises de demos pro): `calculate_trades(demo, trade_length_in_seconds=5.0)`; troca = abate em que o matador de um jogador recém-morto é morto pouco depois. A função `kast` aparece com default 3.0 s na assinatura e 5 s no notebook de exemplo (inconsistência da própria documentação). — [awpy stats](https://awpy.readthedocs.io/en/latest/modules/stats.html); [awpy exemplo](https://awpy.readthedocs.io/en/latest/examples/demo_stats.html)
- Leetify separa seis contagens e **não publica janela nem distância**: Trade Kill Opportunities (colega morreu e você tinha chance de trocar), Attempts (você tentou), Successes (matou); e Traded Death Opportunities / Attempts / Successes (ao menos um colega teve chance / tentou / conseguiu trocar a sua morte). — [Leetify stats glossary](https://leetify.com/blog/leetify-stats-glossary)
- O Leetify Rating usa janela de **4 segundos** para encadear abates numa mesma "corrente" (mecânica do rating, não é a definição de troca). — [Leetify: what is Leetify Rating](https://blog.leetify.com/what-is-leetify-rating/) (visto via busca)
- HLTV reformulou os Atributos (29 métricas novas): em "Entrying" passou a usar **tradeable deaths** em vez de traded deaths, porque "não deve importar se o colega garantiu a troca ou não, o jogador fez a mesma ação". Métricas de Trading: Trade Denials, Trade Opportunities, Tradeable Deaths, Traded Death Success%, Trade Kill Success%, Bait Kills %, First Duel Rank, First Duel Time. **[PRO-CS2]** — [HLTV: Attributes overhaul](https://www.hltv.org/news/45755/hltv-attributes-receive-an-overhaul-with-29-new-metrics)
- Topo do ranking de Trade Kill Success% (Big Events 2026, contra top 20): **ZywOo 66,5%**, **kyousuke 65,8%**. **[PRO-CS2]** — [HLTV: Attributes overhaul](https://www.hltv.org/news/45755/hltv-attributes-receive-an-overhaul-with-29-new-metrics)
- "Alive for Post Plant": AWPers 63,3%, rifler médio 54,6%; rifler com 56,2% é descrito como bem passivo. **[PRO-CS2]** — [HLTV: Attributes overhaul](https://www.hltv.org/news/45755/hltv-attributes-receive-an-overhaul-with-29-new-metrics)
- "Saved teammate" no HLTV (artigo de 26/01/2023, dados de 2022): abate em adversário que estava atacando um colega **em até 1 segundo do último ataque**. O artigo monta o "Lone Wolf rating" (trade kills + traded deaths + saved teammates) para medir quem anda com o grupo: "Lambs" = entradas do pack, muito trocados; "Lone Wolves" = lurkers, pouco trocados. **[PRO-CSGO]** — [HLTV: How trading stats can show a team's structure](https://www.hltv.org/news/35389/how-trading-stats-can-show-a-teams-structure-and-why-heroic-stand-out)
- Estruturas descritas nesse artigo (2022): FaZe = karrigan e rain como par de entrada (pack), Twistzz e broky auxiliares, ropz lurker; G2 = HooXi/Aleksib/JACKZ no pack, NiKo lone wolf; Cloud9 = nafany e HObbit no pack; Heroic = os cinco perto do pack, "consistentemente no topo de deaths traded nos dois lados"; o melhor lugar para a estrela é o de **trocador** (quem mata na troca), exemplo ZywOo. **[PRO-CSGO]** — [HLTV: How trading stats can show a team's structure](https://www.hltv.org/news/35389/how-trading-stats-can-show-a-teams-structure-and-why-heroic-stand-out)
- Parcela dos abates que são de abertura (proxy de quem entra), T side, 2023: NiKo 27,8%, electroNic 27,1%, dupreeh 24,8%, stavn/sjuush ~17-18%, ropz 13,5%, broky 9,6%; YEKINDAR tentava a abertura em 34,4% dos rounds; karrigan + rain somavam 57,7% das tentativas de abertura da FaZe; apEX tinha 32,1% de sucesso em duelo de abertura. **[PRO-CSGO]** — [backpackbrain: What does space mean in CS:GO](https://backpackbrain.substack.com/p/what-does-space-mean-in-csgo)
- Janela de 3 s para "first death não trocada" aparece em análise de comunidade — mas é de **Valorant**, não CS. — [VLR.gg](https://www.vlr.gg/277233/different-ways-to-approach-first-kills)

### Inferences
- Para o gerador: usar **5 s** como janela oficial de troca (bate com HLTV/awpy) e, se quiser medir qualidade, uma janela curta de 2-3 s como "troca limpa". Separar três contagens como o Leetify/HLTV: oportunidade (colega perto quando o outro morreu), tentativa, sucesso.
- O teto observado de sucesso na troca quando há oportunidade é ~2/3 (melhor do mundo: 66,5%). Um time de bots que converte muito acima disso está irreal; a média pro deve ficar abaixo (valor exato não achado).
- "Tradeable death" é a métrica certa para avaliar o bot de entrada: ele morreu num lugar onde o matador ficou exposto a outro duelo em 5 s? Isso mede o posicionamento do entry independentemente da mira do segundo.
- Entrada é papel concentrado: dois jogadores fazem mais da metade das aberturas (FaZe 57,7%); o lurker tem poucas mortes trocadas por desenho.

### Gaps
- Taxa média de "Deaths Traded %" e "Opening Deaths Traded %" de times pro e de entry fraggers: existe nas páginas do HLTV, mas elas devolvem 403; os únicos números vistos (ZywOo 37%, s1mple 18%, Jame ~11%) vêm de comentários de leitores, não verificados — não usar.
- Definição textual exata de "Trade Kill Success%" e "Trade Opportunities" do HLTV (ficam em tooltip).
- Janela e critério de distância do Leetify: não publicados.

---

## 2. Espaçamento e distância do par de entrada para trocar

### Takeaway
Não achei **nenhum número publicado por fonte profissional** para distância ou atraso do segundo homem. O que existe são regras qualitativas consistentes: perto o bastante para trocar, longe o bastante para um spray/granada não pegar os dois; o entry tem de **sair da porta** antes do segundo aparecer; o contato só acontece depois da flash estourar e da smoke abrir.

### Cited Findings
- Regra de espaçamento: ficar perto o bastante para trocar o colega depois que ele cai, de preferência em posição complementar, **mas não tão perto que um único rifle acerte os dois**. **[GUIA][CSGO, 2020]** — [Dignitas: The Buddy System](https://dignitas.gg/articles/blogs/CSGO/14294/csgo-concepts-the-buddy-system)
- O entry tem de limpar e liberar o espaço: ficar parado na porta (ex.: Long Doors da Dust2) bloqueia quem vem trocar e pode matar os dois. O segundo só aparece depois que o primeiro saiu do batente. **[GUIA][CSGO]** — [Dignitas: The Buddy System](https://dignitas.gg/articles/blogs/CSGO/14294/csgo-concepts-the-buddy-system)
- Organização em duplas fixas ("buddies") escolhidas antes da partida e treinadas em scrim; em 5, duas duplas + um quinto livre para flashar, baitar ou lurkar; trio só quando o lugar exige (ex.: Connector da Overpass). Pedir a troca **antes** de sair ("vou no long, me troca"). **[GUIA][CSGO]** — [Dignitas: The Buddy System](https://dignitas.gg/articles/blogs/CSGO/14294/csgo-concepts-the-buddy-system)
- Erros de entrada (Refrag, 18/10/2025): (1) fazer contato antes de os colegas poderem acompanhar ou antes da utilitária cair — esperar a flash estourar e a smoke abrir e então avançar junto; (2) dry peek sem flash/smoke/molotov fora de jogada de contato planejada; (3) depois do abate de abertura, **não avançar fundo demais** — isola o entry; limpar o próximo trecho com utilitária e segurar o terreno ganho ("o objetivo da entrada não é o frag, é criar um ponto de apoio"); (4) o entry não precisa ser o primeiro em todo round. **[GUIA][CS2]** — [Refrag blog](https://refrag.gg/blog/4-of-the-worst-cs2-entry-fragging-mistakes-that-you-probably-make/)
- Resumo de glossário: longe demais não troca, perto demais uma granada mata os dois; não há distância única que sirva para toda porta/quina/área aberta — julgar pelo ângulo de aproximação do colega e pela rota que o segundo teria de fazer. **[GUIA][CS2]** — [csdb.gg glossary](https://csdb.gg/glossary/); [gametree.me](https://gametree.me/?p=14153) (vistos via busca)
- Timing de entrada: se o primeiro jogador entra numa zona 1 segundo depois do timing correto, provavelmente morre e perde a AK (artigo sobre entradas agressivas de rifle contra defaults de CT). **[GUIA][CS2]** — [Gameindustry.com](https://www.gameindustry.com/news-industry-happenings/aggressive-t-side-rifle-entries-disrupt-default-cs2-defenses/) (visto via busca)
- Dado pro indireto: no HLTV, o "pack" é identificado justamente por trade kills + traded deaths + saved teammates (salvar = matar quem ataca o colega em até **1 s**), ou seja, o segundo homem pro está em posição de atirar em ~1 s. **[PRO-CSGO]** — [HLTV: How trading stats can show a team's structure](https://www.hltv.org/news/35389/how-trading-stats-can-show-a-teams-structure-and-why-heroic-stand-out)

### Inferences
- Regra operacional derivada (não é número de fonte): o segundo homem deve ter linha de tiro para o ponto onde o matador do entry vai estar em **≤1 s** (alvo de "saved teammate") e no máximo dentro da janela de 5 s (troca estatística). Distância em unidades depende da geometria; a restrição dura é "não na mesma linha de spray nem no mesmo raio de HE".
- Sequência por porta: utilitária cai → flash estoura → entry cruza e sai do batente para um lado → segundo aparece no ângulo complementar → terceiro cobre o ângulo que os dois não olham.
- Depois do abate de abertura o par segura o terreno e espera o resto, em vez de continuar entrando.

### Gaps
- Nenhuma fala de coach/IGL pro com número (segundos atrás, metros/unidades). A entrevista do EliGE sobre teamplay (tl.net) deu 403.
- Nenhuma medição pública de distância média entre entry e segundo homem em demos pro (daria para medir com awpy em demos, mas não achei publicado).

---

## 3. Duelos de abertura e probabilidade de vitória por estado de jogadores

### Takeaway
Ganhar o abate de abertura leva o round de ~50% para **~70-75%**; no CS2 pro de 2025 o **5v4 de TR vale 75,2%** e o 5v3 de TR 92,0%. O 5v4 de TR vale mais que o 5v4 de CT (no CS:GO MM de 2018: 72,5% contra 68,3%).

### Cited Findings
- Matriz de vitória por jogadores vivos, Big Events de 2025 (Bounty, Katowice, Cluj-Napoca, EPL, Lisboa, Bucareste, Melbourne, Rivals, Astana, Dallas), parse com awpy — valores do lado TR citados no texto: início do round **TR 50,7% / CT 49,3%**; **5v4 TR = 75,2%**; **5v3 TR = 92,0%**; **1v2 TR = 21,4%**; **1v1 TR = 58,8%**; 1v0 TR = 98,4%. CT salvando no 3v5 soma +8 pontos para o TR. **[PRO-CS2]** — [nomiun: Flaws of win probability](https://nomiun.substack.com/p/flaws-of-win-probability)
- Mesmo autor: 5v4 no TR dá chance bem maior de vencer do que 5v4 no CT, porque o TR faz menos abates por round e cada um pesa mais; abates de abertura importam mais no TR. Nunca morrer no TR = mais de 90% de rounds ganhos independentemente de abates; zero abate e zero morte no CT ≈ dois terços de rounds ganhos; 2 abates e 1 morte no CT = pouco mais de 50%; 2 abates e 1 morte no TR = menos de 50%. **[PRO-CS2]** — [nomiun: The Cost of Dying](https://nomiun.substack.com/p/the-cost-of-dying)
- Win rate de TR por mapa em 2025 (Big Events): Anubis acima de 57%, Nuke em torno de 48%. **[PRO-CS2]** — [nomiun: Which players win the most rounds](https://nomiun.substack.com/p/which-players-win-the-most-rounds)
- Matriz completa de probabilidade de vitória do **CT** por vivos — CS:GO matchmaking, >400 mil rounds, meados de 2018 (**[MM-CSGO]**, não é pro) — [smabie: Analysis of CS:GO win-rates](https://cryptm.org/posts/2020/06/12/csgo.html):

| TR vivos \ CT vivos | 1 CT | 2 CT | 3 CT | 4 CT | 5 CT |
|---|---|---|---|---|---|
| 5 TR | 0,2% | 2,3% | 10,6% | 27,5% | 48,7% |
| 4 TR | 0,7% | 6,8% | 23,6% | 47,0% | 68,3% |
| 3 TR | 2,9% | 18,7% | 45,5% | 70,0% | 85,8% |
| 2 TR | 12,3% | 44,0% | 73,3% | 89,3% | 96,1% |
| 1 TR | 43,0% | 79,2% | 94,2% | 98,5% | 99,7% |

- Dessa matriz: CT com abate de abertura (5 CT x 4 TR) = 68,3%; TR com abate de abertura (5 TR x 4 CT) = 72,5% (100 − 27,5). Em confrontos iguais o CT cai conforme o round afunila: 5v5 48,7%, 4v4 47,0%, 3v3 45,5%, 2v2 44,0%, 1v1 43,0%. **[MM-CSGO]** — [smabie](https://cryptm.org/posts/2020/06/12/csgo.html)
- Páginas de time do HLTV mostram win rate de round após primeiro abate; valores vistos em trechos de busca: Aurora 76,6%, Legacy 80,3%, Balkan Outlaws 83,3% (recortes diferentes, não comparáveis); página de jogador: "team win percent after first kill" 73,8%. **[PRO-CS2][SNIPPET]** — [HLTV team: Aurora](https://www.hltv.org/team/11861/aurora); [HLTV team: Legacy](https://www.hltv.org/team/12468/legacy); [HLTV player individual](https://www.hltv.org/stats/players/individual/12840/hallzerk?csVersion=CS2)
- Exemplo de time pro (analista da mousesports, 24/07/2020): 5v4 = 80% de vitória, 4v5 = 22%; depois do abate de abertura o time "perde só 1 round em 5". O autor considera dado de time fraco e prefere o do jogador (ex.: jogador envolvido em 28% dos duelos de abertura com 35% de sucesso = alvo a explorar, porque entrega 4v5 com frequência). **[PRO-CSGO]** — [scope.gg blog](https://blog.scope.gg/usability-of-hltv-stats-for-pro-teams-first-duels-en/)
- Mirage (médias pro, fonte sem metodologia clara): CT 52,5% / TR 47,5%; vitória após primeiro abate CT 72%, TR 72-73%; após sofrer a primeira morte 27-28%. **[PRO-CS2, fonte fraca]** — [CritFeed](https://critfeed.com/is-mirage-ct-sided-or-t-sided/)
- Round Swing do HLTV (qualitativo): abates em situação igual (5v5, 3v3) "valem muito"; abate que transforma 5v2 "quase não mexe"; exit kill que vira 1v5 em 1v3 quase não soma. **[PRO-CS2]** — [HLTV: Finding the most impactful CS2 players](https://www.hltv.org/news/42763/finding-the-most-impactful-cs2-players)
- Expected Rounds (HLTV, 22/10/2025, Big Events 2025): "round jogado fora" = time com ≥80% de probabilidade por ≥5 s e perde; "round pescado" = ≤20% por ≥5 s e ganha. Round pescado acontece **1 a cada 27 rounds**; round "choked" (perder com xR > 0,5) **1 a cada 18**; taxa média de choke/ressuscitação 5,4%. Exemplo: TR em 5v3 chegou a 99% de probabilidade. The MongolZ ganhou 21 de 233 rounds (~9%) com xR abaixo de 0,5. **[PRO-CS2]** — [HLTV: Chokes, Throws and Resuscitations](https://www.hltv.org/news/42993/chokes-throws-and-resuscitations-what-expected-rounds-tells-us-about-2025s-top-teams)
- Correlação: conversão de 4v5 teria correlação maior com sucesso do time do que conversão de 5v4. **[fonte secundária, não aberta]** — [winio.ai](https://winio.ai/blog/articles/what-actually-predicts-wins-in-cs2-matches) (visto via busca)

### Inferences
- Calibração sugerida para os bots (pro CS2): 5v5 ≈ 50/50; TR com 5v4 ≈ 75%; TR com 5v3 ≈ 92%; CT com 5v4 algo abaixo do TR (68% no dado antigo de MM; valor pro CS2 exato não achado em texto, mas a direção "TR 5v4 > CT 5v4" é confirmada pelo nomiun).
- Em confronto igual que afunila (3v3, 2v2, 1v1) o TR fica favorito: 1v1 TR 58,8% no pro CS2 2025 e 57% no MM 2018 (os dois incluem rounds com bomba plantada).
- Vantagem de 2 é quase round ganho (92%): perder 5v3 é evento raro (ordem de 1 em 12), e "jogar fora" um round com ≥80% acontece mais ou menos 1 vez a cada 18-27 rounds.

### Gaps
- Matriz pro CS2 completa (4v4, 4v3, 3v5 etc. por lado): existe nos artigos do nomiun, mas só em imagem. Só os valores listados acima estão em texto.
- Win rate de duelo de abertura por lado (quem ganha mais a abertura, TR ou CT) em nível pro CS2: não achado em texto legível (página de mapa do HLTV deu 403).
- 5v4/4v5 por time no HLTV em 2025-2026: páginas bloqueadas.

---

## 4. Protocolos concretos de meio de round (IGLs e coaches)

### Takeaway
Esta é a parte mais fraca da pesquisa: **não achei nenhum IGL/coach profissional descrevendo em texto um protocolo do tipo "morreu fulano em tal área → fazemos X"**. O que há de sólido são falas pontuais (Aleksib, karrigan), a estrutura de pack/lurker medida pelo HLTV e guias de nível amador; tudo o mais é inferência.

### Cited Findings
- Aleksib (NAVI, 21/08/2025, após derrota para a 3DMAX na EWC): a NAVI perdeu vários 4v2 e 5v3; ele atribui a **falha de comunicação sobre quem estava morto e onde** e a um jogador que avançou achando que não havia mais ninguém. Também reclama de calls que mandaram jogadores para um bomb A vazio ("não dá para ganhar round assim"). A 3DMAX entrou agressiva, estendeu rápido e tomou o bomb sem oposição. **[PRO-CS2]** — [HLTV: entrevista do Aleksib](https://www.hltv.org/news/42493/aleksib-it-feels-so-gut-wrenching-to-be-in-this-situation-in-the-middle-of-the-season)
- karrigan: queria um estilo mais lento na FaZe para a visão macro do Twistzz ajudar nas calls de meio de round; **se ele morre, outro jogador assume a call de meio de round e dita o ritmo**. Na Falcons, ele e NiKo ficam juntos quando não há call definida. **[PRO-CS2][SNIPPET, via busca]** — [HLTV: karrigan (2025)](https://www.hltv.org/news/43317/karrigan-with-the-changes-we-made-its-not-about-2025-anymore); [HLTV: karrigan na Falcons](https://www.hltv.org/news/44529/karrigan-interview-leaving-faze-joining-falcons-and-first-impressions)
- Aleksib, karrigan e apEX: no MR12 há pouco tempo para adaptar taticamente e os estilos dos times ficaram parecidos ("não há meta no CS2"). **[PRO-CS2][SNIPPET, via busca; página deu 403]** — [Escorenews](https://escorenews.com/en/csgo/news/60961-there-is-no-meta-in-cs2-right-now-aleksib-karrigan-and-apex-discuss-igling)
- Quem chama no meio do round: ou quem está vivo, ou um jogador dedicado (normalmente o IGL). IGL morto chamando de fora tende a travar os vivos; passar informação é ok. **[GUIA][CSGO, 2018]** — [Dignitas: The Unwritten Rules of In-Game Leading](https://dignitas.gg/articles/the-unwritten-rules-of-in-game-leading)
- Opções de call de meio de round listadas em guia: acelerar antes de a defesa rotacionar, **desacelerar para preservar a vantagem numérica**, reagrupar para entrada coordenada, ou trocar de bomb; escolhida a direção, o IGL coordena espaçamento, ordem das utilitárias, timing e a sequência de quem pega cada duelo. O meio de round começa quando o quadro muda: primeiro contato, pick para qualquer lado, informação do default que contradiz o plano. **[GUIA][CS2]** — [itechguides](https://www.itechguides.com/what-does-an-in-game-leader-do-in-professional-cs2/); [guildorder: IGL round structure](https://guildorder.com/games/csgo/guides/igl-round-structure) (vistos via busca)
- Depois do abate de abertura: não estender; limpar o próximo trecho com utilitária e segurar o terreno. **[GUIA][CS2]** — [Refrag blog](https://refrag.gg/blog/4-of-the-worst-cs2-entry-fragging-mistakes-that-you-probably-make/)
- Bomba no chão: quem mata o portador chama na hora "bomb down" com o lugar. **[GUIA][CSGO]** — [Dignitas: Round by Round, CT pistol](https://dignitas.gg/articles/round-by-round-part-2-the-ct-pistol-round) (visto via busca)
- Defesa de bomb em gun round (HLTV + Skybox EDGE, top times, 2024; "hold" = impedir o plant): média de hold **~21%**; Nuke A 44% (mais fácil), Vertigo A 39%, Mirage B 35% conforme repost, Inferno B pouco acima de 21%, Dust2 B 20%, Inferno A 18%, Dust2 A 16%, Anubis B 21%. "Turtling" (recuar e segurar dentro do bomb) nega o plant em média **30%**. Se o âncora faz 2 abates num 4v4 e a bomba é plantada, conta como falha. Anubis B costuma ter 3 CTs presentes quando é atacada. **[PRO-CS2]** — [HLTV: Which bomb sites are hardest to hold](https://www.hltv.org/news/40195/which-bomb-sites-are-hardest-to-hold); repost com números extras em [csgo.com](https://csgo.com/news/118928-easiest-points-to-holdretake-on-cs2-maps-revealed) (o valor "Mirage B 35% hold" vem só do repost e conflita com o texto do HLTV, que chama a Mirage B de "horror show" para segurar — tratar como incerto)
- Estrutura de TR medida: pack de entrada de 2 jogadores + auxiliares + 1 lurker (FaZe 2022: karrigan/rain, Twistzz/broky, ropz); times como Heroic jogam com os cinco perto do pack e lideram em mortes trocadas. **[PRO-CSGO]** — [HLTV: How trading stats can show a team's structure](https://www.hltv.org/news/35389/how-trading-stats-can-show-a-teams-structure-and-why-heroic-stand-out)

### Inferences
(Tudo abaixo é inferência a partir dos números das seções 3 e 5 e das falas acima; nenhuma é protocolo citado por IGL.)
- **TR com vantagem (5v4 ≈ 75%)**: o round já está ganho em 3 de 4 casos; o protocolo coerente é desacelerar, não dar duelo isolado e manter as duplas de troca. O erro documentado em nível pro (NAVI) é perder a conta de quem está vivo/onde e alguém avançar sozinho.
- **TR em desvantagem (4v5 ≈ 25% no dado antigo)**: precisa de um abate que iguale; pick com troca garantida ou entrada rápida num bomb antes da rotação.
- **CT com vantagem (5v4 ≈ 68%)**: vale menos do que para o TR; não devolver o abate em repique — o pior resultado é a troca 1 por 1 que volta para 4v4 (47% CT).
- **CT em desvantagem**: como hold médio é só ~21% e turtling 30%, a defesa pro já aceita perder o plant na maioria dos gun rounds; em desvantagem o jogo vira retake ou save (seção 5).
- **Morte do IGL**: outro jogador vivo assume a call (karrigan). Para os bots: sempre existir um segundo "caller" designado.
- **Informação de mortos**: transmitir "quem morreu e onde" é pré-requisito para converter vantagem (Aleksib).

### Gaps
- Protocolo por área de morte ("perdemos o jogador do meio → ..."), portador da bomba morto (quem recupera, quando abandonar), perda de controle de mapa e utilitária gasta: **nenhuma fonte pro em texto**. Esse conhecimento circula em vídeo (análises de demo, voice comms de times) que não foi possível consultar aqui.
- Artigo do realsport101 com gla1ve/Aleksib/AZR sobre lados CT: 404.
- Entrevista do EliGE sobre teamplay (tl.net): 403.

---

## 5. Retake x save

### Takeaway
O único estudo quantitativo é do HLTV (CS:GO, 2022): **34,2% dos rounds perdidos de CT terminam com alguém salvando** (TR: 9,3%), e cada arma salva aumenta em ~4 pontos a chance de CT no round seguinte. No CS2 (2024), retake em gun round é raro de dar certo: **15% (Mirage B) a 31% (Nuke B)**. Não há limiar publicado por fonte pro do tipo "XvY com N segundos → salva".

### Cited Findings
- HLTV "Saving: how bad is it?" (28/02/2023; amostra jan/2022 a 26/01/2023): rounds perdidos com ao menos um sobrevivente do lado perdedor = **20,6% no geral, 34,2% no CT, 9,3% no TR, 25,8% em times tier 1**. Média de 0,80 sobrevivente por round perdido ≈ 6 jogadores salvando numa metade CT de 7-8 rounds perdidos; times tier 1 têm cerca de **4 rounds de save por metade**. **[PRO-CSGO]** — [HLTV: Saving: how bad is it?](https://www.hltv.org/news/35622/saving-how-bad-is-it)
- Efeito do save: um jogador salvando aumenta em **+4,3 pontos** a chance do CT ganhar o round seguinte; o segundo save soma **+3,9**. Conclusão do artigo: salvar é racional e maximiza vitória. **[PRO-CSGO]** — [HLTV: Saving: how bad is it?](https://www.hltv.org/news/35622/saving-how-bad-is-it)
- Custo do save: cada CT que salva dá ao TR **$250 a mais** se o round termina por detonação. Plant vale $2.400 + $800 de bônus para o TR; defuse $3.500 para o CT (valores do CS:GO na época). **[PRO-CSGO]** — [HLTV: Saving: how bad is it?](https://www.hltv.org/news/35622/saving-how-bad-is-it)
- Quem salva: AWPers salvam mais — taxa individual de save em rounds perdidos: Jame 23%, sh1ro 18%, degster 17%, device ~8%, Farlig 7,3%; ZywOo e s1mple salvam menos que o AWPer médio. Times: Cloud9 e Outsiders ~2x mais que G2 e ENCE (os que menos salvam). Inferno é o mapa com mais save no CT (0,78 sobrevivente por round perdido), por ter bombs difíceis de retomar. **[PRO-CSGO]** — [HLTV: Saving: how bad is it?](https://www.hltv.org/news/35622/saving-how-bad-is-it)
- Retake em gun round (equipamento médio > $3.250), top times, CS2 2024, dados Skybox EDGE: **Nuke B 31%**, **Ancient B 27%**, **Anubis B 22%**, **Mirage B 15%** (pior). Parte das falhas de retake na Anubis B se deve a save; Inferno é "a maior culpada por save". Correlação hold x retake fraca (−0,229). **[PRO-CS2]** — [HLTV: Which bomb sites are hardest to hold](https://www.hltv.org/news/40195/which-bomb-sites-are-hardest-to-hold); [csgo.com (repost)](https://csgo.com/news/118928-easiest-points-to-holdretake-on-cs2-maps-revealed)
- CT que salva no 3v5 entrega +8 pontos de probabilidade ao TR em relação a jogar (isto é, o 3v5 jogado vale ~8% para o CT). **[PRO-CS2]** — [nomiun: Flaws of win probability](https://nomiun.substack.com/p/flaws-of-win-probability)
- Referência de matriz (inclui rounds com e sem bomba, **[MM-CSGO]** 2018): CT em 3v5 = 10,6%; 2v4 = 6,8%; 2v3 = 18,7%; 3v4 = 23,6%; 2v2 = 44,0%; 1v2 = 12,3%; 1v1 = 43,0%. — [smabie](https://cryptm.org/posts/2020/06/12/csgo.html)
- Heurísticas de guia (sem dado): retake 3v3 costuma ter menos de 50%; tentar se a chance for >25% ([GameHub](https://gamehub-al9.pages.dev/guides/cs2-strategy-retake-strategies)); salvar se a chance de retake for <35% e checklist "tempo para organizar? jogadores ou kit suficientes? granada para limpar ângulo forte? mais de uma direção de entrada? dá para trocar?" — maioria sim, retoma; maioria não, salva com disciplina; retake ruim estraga também a compra seguinte ([Boosteria](https://boosteria.org/guides/cs2-retake-guide-2026-site-retake-strategy-teamplay)). **[GUIA][CS2]** — os dois limiares se contradizem (25% x 35%) e nenhum cita fonte.
- Retake: ponto de reagrupamento comum antes de entrar, sem deixar o relógio acabar; evitar duelos escalonados. **[GUIA][CS2]** — [RankForge](https://rankforge.gg/blog/cs2-retake-guide) (visto via busca)

### Inferences
- Frequência-alvo para os bots: no CT, cerca de **1 em 3 rounds perdidos** deve terminar com alguém salvando; no TR, ~1 em 10. AWPer salva 2-3x mais que rifler. (Base CS:GO MR15; no MR12 do CS2 a economia mudou e o número pode diferir — não achei atualização.)
- Retake de gun round contra bomb tomado dá 15-31% no agregado pro; em desvantagem numérica o valor cai para a casa de 7-19% (matriz antiga). Com o ganho de ~4 pontos por arma salva no round seguinte, o save de 2 armas (+8,2) empata com um retake de ~8% — coerente com o 3v5 (≈8% CT) ser o ponto em que pros salvam.
- Decisão situacional sugerida (inferência): entram na conta vivos de cada lado, tempo de bomba, kit (sem kit o retake exige matar todos ou ter 10 s), valor das armas vivas (AWP pesa para salvar), mapa/bomb (Mirage B e Inferno pesam para salvar; Nuke B e Ancient B pesam para retomar) e estado da economia do round seguinte.

### Gaps
- Win rate de retake por XvY (2v3, 3v3, 3v4...) em nível pro CS2: não achado em texto.
- Regra declarada por IGL/coach pro para salvar (vivos, tempo, kit): não achada.
- Taxa de save no CS2/MR12 (o estudo do HLTV é CS:GO/MR15).
- Tabela completa de hold/retake por bomb do artigo do HLTV: em imagem.
