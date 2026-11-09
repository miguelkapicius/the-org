# Plano de marketing para o lançamento: wishlists na Steam

> **Status:** rascunho · **Responsável:** miguelkapicius · **Escrito em:** 2026-11-09

_Objetivo dos autores: juntar o máximo de wishlists possível, com tráfego pago e com streamers menores, para o jogo aparecer na página inicial da Steam no lançamento. Este plano é proposta do agente, a validar; os números de fora vêm das fontes no fim e são estimativas, não regras da Valve._

## Resumo

1. **A meta:** chegar ao lançamento com **15 mil wishlists ou mais**. A estimativa antiga para entrar em "Populares em breve" era de 5 a 7 mil (7 a 12 mil para ter folga), mas em junho de 2026 a Valve mudou a lista para o mês inteiro e ficou mais difícil para jogos de 3 a 10 mil.
2. **A página da Steam tem de ir ao ar o quanto antes.** Wishlist só existe com página. Para isso falta decidir o **nome do jogo**.
3. **O que vende o jogo são os bots jogando como profissionais e a partida com cara de transmissão.** O material principal são clipes curtos de jogadas da IA (clutch, retake, jiggle peek) e de decisões do manager mudando a partida. Por isso vale corrigir antes a IA no micro (`gdd/12-ia-micro.md`), porque fã de CS percebe bot na hora.
4. **O momento mais forte é o Steam Next Fest de junho de 2027** (14 a 21), com uma demo. Cada jogo só participa de um Next Fest antes de lançar, então não vale gastar o de fevereiro com o jogo cru.
5. **Streamers menores:** levar a demo a 100 a 200 criadores de CS, esports e manager (PT-BR e inglês), com um gancho: **o time do streamer dentro do jogo**.
6. **Tráfego pago só depois de testar:** começar com pouco no Reddit (referência pública: US$ 0,40 a 2 por wishlist quando bem feito), medir pela Steam e só aumentar se o custo ficar abaixo de US$ 2.

## 1. Meta e como a Steam funciona

**O que a Steam mostra.** A lista "Populares em breve" da página inicial usa as wishlists, segundo a própria Valve, mas ela não diz quanto é preciso. Estimativas de fora:
- Chris Zukowski (How To Market A Game): cerca de **5 a 7 mil** wishlists, **7 a 12 mil** para ter folga.
- Em **5 de junho de 2026** a Valve passou a mostrar os mais aguardados **do mês seguinte** (antes era cerca de uma semana). Relatos da comunidade dizem que ficou mais difícil para jogos com **3 a 10 mil** wishlists.
- Cada jogo entra na lista praticamente **uma vez**, e **adiar a data várias vezes** tira o jogo dela.

**Meta proposta (a validar):**

| Marco | Wishlists | Quando |
|---|---|---|
| Página no ar | começa a contar | dezembro de 2026 |
| Antes da demo | 3 mil | março de 2027 |
| Depois do Next Fest de junho | 8 a 10 mil | fim de junho de 2027 |
| No lançamento | **15 mil ou mais** | data a definir (ver seção 4) |

Além do número total, conta a **velocidade** (wishlists por dia) no mês antes do lançamento, por causa da janela mensal da lista.

## 2. O que precisa estar pronto antes de divulgar

| Item | Por quê | Situação |
|---|---|---|
| **Nome do jogo** e do "esporte" fictício | Sem nome não há página da Steam, domínio nem redes | Em aberto (`gdd/00-visao.md`) |
| **Sem marcas da Valve no material** | Decisão 0004. O protótipo se chama "CS Manager" e usa nomes de armas reais (AK-47, M4A4, AWP); nada disso pode aparecer em página, trailer ou anúncio. Dizer "FPS tático" e "esports", não "Counter-Strike" | Trocar nomes de armas e o título antes de gravar material público |
| **Página da Steam** | Onde nascem as wishlists | Precisa de: cápsulas (arte principal feita por um artista, é o que mais pesa no clique), trailer de 30 a 60 s começando pela partida, 5+ capturas, descrição curta com o gancho, etiquetas certas (Esportes, Gerenciamento, Simulação, Estratégia, Esports) |
| **IA sem "cara de bot"** | O material principal são jogadas da IA; re-peek no mesmo pixel e fila indiana viram meme negativo | Itens 1 a 4 de `gdd/12-ia-micro.md` |
| **Modelo de negócio e preço** | Muda a página (acesso antecipado ou não) e a mensagem | Em aberto (`gdd/00-visao.md`) |
| **Data-alvo** | O plano inteiro anda para trás a partir dela; adiar muito tira o jogo de "Populares em breve" | Em aberto |

## 3. O gancho: o que mostrar

O jogo tem um ângulo raro: **um manager em que a partida é uma transmissão de verdade, com bots que jogam como profissionais**. Para quem gosta de CS e de manager esportivo, a pergunta é "e se eu fosse o técnico?".

**Formatos que devem funcionar (a testar):**
- **Clipe da IA (15 a 30 s):** clutch 1v3, retake bem feito, jiggle peek, ACE. Texto na tela: "nenhum humano jogou isso".
- **Decisão do manager mudando a partida:** pedir timeout, mudar o estilo e o time virar o placar; o playbook desenhado no mapa.
- **História de carreira:** "do amador ao Major", o jovem do mercado que vira craque, o clássico contra o rival (as features 0019 a 0022 já dão material).
- **Bastidores da IA:** como os bots aprendem com rounds de times profissionais. Público de gamedev e de CS gosta.
- **Idiomas:** português e inglês. A cena brasileira de CS é enorme e engajada, e a página da Steam pode estar nos dois.

## 4. Calendário (a partir de hoje, 2026-11-09)

| Fase | Quando | O que fazer |
|---|---|---|
| **0. Base** | nov a dez de 2026 | Decidir nome, preço e data-alvo. Tirar marcas da Valve. Encomendar a cápsula. Abrir a **página da Steam** (taxa do Steam Direct de US$ 100) com um trailer de anúncio. Criar Discord, TikTok/Shorts/Reels, X/Bluesky e um perfil no Reddit. Começar a corrigir a IA (AWP e retake primeiro). |
| **1. Conteúdo toda semana** | dez de 2026 a mar de 2027 | 3 ou 4 vídeos curtos por semana; um devlog a cada 15 dias; posts nas comunidades certas, respeitando as regras de autopromoção de cada uma. Steam Playtest fechado para quem entra no Discord. |
| **Next Fest de fevereiro de 2027** | 22/2 a 1/3 (inscrição até 10/1) | **Sugestão: não entrar.** Cada jogo só participa de um Next Fest antes de lançar, e em janeiro o jogo ainda estará cru. |
| **2. Demo e criadores** | mar a jun de 2027 | Demo pública (ex.: a primeira temporada, ou alguns meses de carreira). Levar a demo a criadores (seção 5). Testar anúncios pagos (seção 6). |
| **Next Fest de junho de 2027** | **14 a 21 de junho** | O ponto alto antes do lançamento. Demo polida, live na página durante o festival, criadores jogando na mesma semana, anúncios ligados. |
| **3. Embalar** | jul a set de 2027 | Aumentar os anúncios que funcionaram. Imprensa e mostras (ex.: festivais de jogos independentes no Brasil e fora; conferir datas e custos). Anunciar a data de lançamento cerca de 2 meses antes. |
| **Lançamento** | data a definir (ex.: 4º trimestre de 2027) | Escolher uma semana sem grandes lançamentos. No mês antes, concentrar conteúdo, criadores e anúncios para subir a velocidade de wishlists. A Steam avisa por e-mail quem tem o jogo na wishlist quando ele lança. |

O Next Fest de outubro de 2027 é a alternativa se a demo não estiver pronta para junho, mas empurra o lançamento para depois.

## 5. Streamers e criadores menores

**Por que os menores:** eles respondem, jogam de verdade, e o público deles confia neles. Somados, rendem mais que um grande que não responde.

**Quem procurar** (100 a 200 nomes, numa planilha):
- streamers e youtubers de CS e esports com **50 a 1.000 espectadores** (Twitch, YouTube, Kick), em português e em inglês;
- criadores de jogos de **manager e simulação** (Football Manager, Motorsport Manager e parecidos);
- canais que comentam a cena competitiva (análise de partidas, táticas).

**Como abordar:**
- e-mail curto e pessoal: o que é o jogo em uma frase, um GIF de jogada da IA, a chave da demo ou do playtest e nenhuma obrigação de postar;
- plataformas de distribuição de chaves para criadores (como Keymailer e Lurkit) ajudam a achar e filtrar; conferir preço e alcance antes;
- o **Curator Connect** da Steam manda chaves para curadores.

**Ganchos para eles:**
- **o time do streamer dentro do jogo:** a equipe e os jogadores com o nome dele e da comunidade, no calendário e no ranking;
- **liga de streamers:** cada um gerencia o seu time e as partidas são transmitidas;
- material pronto: clipes, capturas e a cápsula num kit de imprensa.

**Medir:** um link da Steam com UTM por criador, para saber quem trouxe wishlist.

## 6. Tráfego pago

**Referências públicas (indies, 2025 e 2026):**
- **Reddit:** casos bem documentados ficam entre **US$ 0,40 e 2 por wishlist**. Um dev solo chegou a "Populares em breve" gastando US$ 4.365, a cerca de **US$ 1,10 por wishlist**. Também há casos ruins, com mais de US$ 30 por wishlist.
- **Meta, TikTok e YouTube:** não há número público confiável para wishlists de indie. Testar com pouco (US$ 5 a 10 por dia por grupo de anúncios).
- **Regra prática citada nos guias:** bom é de US$ 1 a 2 por wishlist; acima de US$ 3, parar e trocar o criativo ou o público.

**Como fazer:**
1. Usar o **rastreamento de UTM da Steam** para saber de onde vem cada visita e wishlist (ele pode subestimar o efeito; olhar também o total por dia).
2. Testar de 3 a 5 criativos (os clipes da seção 3) com US$ 300 a 500 no Reddit, mirando comunidades de CS, esports e manager.
3. Só aumentar o que ficar abaixo de US$ 2 por wishlist. Concentrar a verba no Next Fest de junho e no mês antes do lançamento.

**Cenários de verba (a decidir):**

| Verba em anúncios | A US$ 1 a 2 por wishlist |
|---|---|
| US$ 1.500 | 750 a 1.500 wishlists |
| US$ 5.000 | 2.500 a 5.000 |
| US$ 15.000 | 7.500 a 15.000 |

Anúncio sozinho raramente chega lá: ele soma ao orgânico, ao Next Fest e aos criadores.

## 7. Comunidade

- **Discord:** canal de clipes, feedback do playtest e um canal para votar features (sugestões como a do psicólogo da equipe viram engajamento).
- **Eventos da Steam:** cada atualização vira um evento na página, que aparece para quem segue o jogo.
- **Playtest:** os primeiros jogadores viram os primeiros divulgadores; dar crédito (nome num time do jogo, por exemplo).

## 8. O que medir toda semana

- wishlists por dia e total (relatório do Steamworks);
- de onde vieram (UTM por canal, por criador e por anúncio);
- visitas da página que viram wishlist (se cair, o problema é a página, não o tráfego);
- custo por wishlist de cada anúncio;
- seguidores, membros do Discord e visualizações dos clipes.

## 9. Riscos

| Risco | Como reduzir |
|---|---|
| Marca da Valve no material (nome, armas) | Nome próprio e armas próprias antes de qualquer material público (decisão 0004) |
| IA com "cara de bot" nos clipes | Corrigir os itens 1 a 4 de `gdd/12-ia-micro.md` antes de gravar |
| Data escorregar várias vezes | Anunciar a data só quando ela for firme; o jogo entra em "Populares em breve" uma vez só |
| Gastar o Next Fest cedo demais | Ir só quando a demo estiver boa (junho de 2027) |
| Anúncio caro | Testar barato, medir por UTM, parar acima de US$ 3 por wishlist |
| Pouca gente para produzir conteúdo | Rotina fixa de clipes (a ferramenta de replay do jogo pode gravar as jogadas) |

## 10. O que precisamos decidir

1. **Nome do jogo** (bloqueia a página da Steam).
2. **Modelo de negócio e preço** (pago, acesso antecipado, free-to-play).
3. **Data-alvo de lançamento** e se vamos ao Next Fest de junho de 2027.
4. **Verba de marketing** (anúncios, arte da cápsula, trailer).
5. **Quem cuida** de redes, criadores e Discord, e com que frequência.
6. **Idiomas:** português e inglês desde o início?

## Fontes

- [Steam Page Analyzer: Popular Upcoming](https://www.steampageanalyzer.com/blog/steam-popular-upcoming-list)
- [Theory-Craft: how to get more Steam wishlists in 2026](https://www.theorycraftmarketing.com/how-to-get-more-steam-wishlists-in-2026)
- [GameDev Report: mudança da Steam em junho de 2026](https://gamedevreport.beehiiv.com/p/steam-update-destroyed-indie-devs-and-summer-games-fest)
- [GameDiscoverCo: o calendário pessoal da Steam](https://newsletter.gamediscover.co/p/how-steams-personal-calendar-is-supercharging)
- [Steamworks: Next Fest de fevereiro de 2027](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/feb_2027)
- [Steamworks: Next Fest de junho de 2027](https://partner.steamgames.com/doc/marketing/upcoming_events/nextfest/june_2027)
- [PC Gamer: eventos da Steam no 1º semestre de 2027](https://www.pcgamer.com/games/steam-reveals-all-the-sale-dates-and-themed-events-for-the-first-half-of-2027/)
- [Notebookcheck: calendário da Steam de 2027](https://www.notebookcheck.net/Valve-announces-Steam-Sales-and-Festivals-for-first-half-of-2027.1344181.0.html)
- [WN Hub: US$ 4.365 em anúncios para chegar a Popular Upcoming](https://wnhub.io/news/investment/item-43597)
- [Steam Page Analyzer: custos de anúncio](https://www.steampageanalyzer.com/blog/video-game-advertising-costs)
- [Steam Page Analyzer: marketing com US$ 1.000](https://www.steampageanalyzer.com/blog/indie-game-marketing-1000-budget)
- [Danchi Days: 3.600 wishlists com anúncios no Reddit](https://buttondown.com/melodicambient/archive/danchi-days-3600-wishlists-through-reddit-ads-was)
