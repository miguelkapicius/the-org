# Virais e comunidades: o que postar, onde, e onde pagar para empurrar

> **Status:** rascunho · **Responsável:** miguelkapicius · **Escrito em:** 2026-11-11 · **Complementa:** `marketing/plano-de-lancamento.md` e `marketing/pesquisa-2026-11-09/` (PR 25)

_Proposta do agente, a validar. Cada afirmação está marcada: **[dado]** vem de fonte citada; **[opinião]** é conselho de quem escreve sobre o assunto; **[inferência]** é conclusão do agente, sem teste. A pesquisa do PR 25 já cobre criadores, anúncios pagos, Next Fest e cena brasileira; aqui só entra o que falta: que conteúdo espalha, em quais comunidades, e como isso muda do Brasil para fora._

## Resumo

1. **O que espalha é um clipe que se entende sem contexto.** Os casos de indie que viralizaram no TikTok mostram o gancho nos 3 primeiros segundos e foram feitos para a plataforma, não recortados do trailer **[dado]**. Para nós, o gancho é um só: **bots que jogam como gente**. Todo clipe tem de mostrar isso de cara.
2. **O jogo já tem as peças de viral.** Melhores momentos e MVP (0020), timeout (0018), rivalidades e notícias (0021), personalidade (0019), fãs (0037) e a partida como transmissão (0002). O trabalho é transformar cada uma num formato que o jogador *queira* postar **[inferência]**.
3. **Brasil e fora pedem conteúdo diferente, não só tradução.** No Brasil, o CS é torcida, narração e zoeira, e circula em cortes de live, YouTube, X e TikTok. Lá fora, o mesmo público de manager quer profundidade e história de save, e circula em Reddit, YouTube e X **[inferência sobre dados parciais; ver seção 4]**.
4. **Pagar só para empurrar o que já funcionou de graça.** Impulsionar um post que já performou (TikTok, Reddit) custa menos e mede melhor que anúncio feito do zero **[opinião]**. O Brasil tem impressão e clique 50–80 % mais baratos, mas a wishlist brasileira vale menos em receita **[dado, fontes de fornecedor]**.
5. **O risco que mata tudo é a marca da Valve** (decisão 0004). Nenhum clipe, hashtag ou título com CS, Counter-Strike, Major, nome de arma, mapa, time ou jogador real. A comparação vem dos criadores, nunca de nós.

## 1. O que a literatura diz sobre o que se espalha

**STEPPS (Jonah Berger, *Contagious*).** Seis motivos pelos quais as pessoas compartilham **[dado: resumo do livro]**:

| Princípio | O que é | Como aparece no nosso jogo **[inferência]** |
|---|---|---|
| Moeda social | Compartilhar faz quem compartilha parecer bem | "Meu time de desconhecidos ganhou do favorito", "olha o que meu IGL fez" |
| Gatilhos | Algo do dia a dia lembra o produto | Campeonato real de CS no ar = lembrar do jogo. Postar nos dias de evento, sem citar time real |
| Emoção | Emoção que **agita** (euforia, raiva, susto) espalha mais que a que acalma | Clutch, virada, o bot errando feio, a derrota no último round |
| Público | O que se vê sendo usado se espalha | Tela de pós-partida e "card" do MVP feitos para print |
| Valor prático | Dica útil | "Como montar um time barato que vence", "a estratégia que quebra o retake" |
| Histórias | A mensagem dentro de uma história | A saga do save: do Amador à Elite |

**Gancho e fechamento (Mike Rose, citado por segwise).** Um começo que prende e um fim que vende a ideia central **[opinião]**. No nosso caso: começa no lance (o bot abre o pixel), fecha na conta do manager ("e eu paguei 2 mil por esse cara").

**Feito para a plataforma, gancho em 3 segundos.** No caso *The Matriarch*, um vídeo de 4,5 milhões de views deu ~10 mil wishlists; repostar o trailer "foi bem mal", e o que funcionou foi conteúdo próprio do TikTok, com o gancho explicado nos 3 primeiros segundos e resposta em vídeo aos comentários **[dado, relato do dev]**. O TikTok "vem em ondas": cada onda cresce da anterior **[opinião do mesmo dev]**.

**Compartilhamento se desenha dentro do jogo.** A análise da Brain Jar defende desenhar o compartilhamento no jogo antes da campanha **[opinião]**. Para nós, isso vira requisito de produto (seção 3).

**Alerta: o alcance orgânico caiu.** Um dev relatou 15× menos alcance orgânico de um fevereiro para o outro, e outro diz ter ~0 views no TikTok **[dado anedótico]**. Viral é loteria; o plano não pode depender dele.

## 2. Os virais que precisamos fazer

Em ordem de prioridade. Todos saem da **câmera de transmissão** (decisão 0002), que é o que nenhum concorrente tem: os managers de CS na Steam mostram a partida em 2D (pesquisa do PR 25).

| # | Formato | O gancho (3 s) | Por que espalha | Brasil | Fora |
|---|---|---|---|---|---|
| 1 | **"O bot fez o que um pro faria"** | Retake 1v3, abrir pixel, trade perfeito, em câmera de transmissão | Emoção + a prova do pilar 1 | Com narração em português no estilo de transmissão, empolgada | Legenda seca: "these are bots. no one is playing." |
| 2 | **"O bot errou como gente"** | O bot erra o que um humano erraria (pânico, flash no time, ego peek) | Humor; mostra que a IA não é bot de jogo | Zoeira: "o meu entry é o meu amigo no domingo" | "AI tilted after losing pistol round" |
| 3 | **A saga do save** | "Dia 1: comprei um time de 5 desconhecidos" | História, série que pede o próximo episódio | Série em Shorts/TikTok, com virada e drama de torcida | Post de história no Reddit e vídeo longo no YouTube, como os saves de Football Manager |
| 4 | **Previsão vs resultado** | "Chance de vitória: 12 %" e o upset logo depois | Emoção + moeda social | Postar no dia de jogo grande da cena real, sem citar time | Igual, em inglês |
| 5 | **O timeout que virou o jogo** | Placar 3–10, o manager pede timeout, a estratégia muda | Mostra o lado manager mexendo na partida (pilar 3) | Narração de "virada histórica" | "One timeout. 9 rounds in a row." |
| 6 | **A notícia do jogo** | Print de manchete das rivalidades (0021): "Jogador X pede para sair após derrota" | Moeda social; parece real e diverte | Tom de portal de esports brasileiro | Tom de site de cena internacional |
| 7 | **Bastidores do dev** | "Fiz a IA aprender a abrir pixel" com antes e depois | Valor prático + história; funciona com devs e curiosos | Menos alcance aqui **[inferência]** | r/gamedev, r/IndieDev, X de devs (só como bônus: postar para outros devs rende pouco, plano do PR 25) |
| 8 | **Desafios de criador** | "Montei o time do meu chat", "liga de criadores" | Criador vira personagem; o chat participa | Encaixa na cultura de live e cortes | Encaixa em YouTube de manager |

**Regras para todos [opinião, das fontes da seção 1]:**
- O lance aparece no primeiro segundo; nada de logo ou abertura antes.
- Vertical 9:16, legenda queimada (a maioria assiste sem som), 10–30 s.
- Um clipe, uma ideia.
- Feito para o TikTok/Shorts, nunca o trailer recortado.
- Responder comentário com vídeo: é a segunda onda.
- Cada post com link UTM próprio para a Steam.

**Ritmo [inferência]:** 3 clipes curtos por semana, 1 post longo por mês (Reddit ou YouTube), e um formato novo testado por mês. Dobrar o que der certo; largar o que não der em 3 tentativas.

## 3. O que o jogo precisa ter para isso funcionar

Requisitos de produto que saem dos formatos acima **[inferência]**. Quase todos já estão previstos em decisões; o que falta é o botão.

- **Gravar o lance.** Um botão "salvar clipe" nos melhores momentos (0020), em 9:16 e 16:9, com o HUD de transmissão.
- **Câmera de transmissão vertical.** Um enquadramento que funcione em 9:16 sem perder o lance.
- **Card de pós-partida para print.** Placar, MVP e a frase do momento, com a logo, pronto para postar (princípio "Público").
- **Notícias em formato de print** (0021), com a marca do jogo discreta.
- **Nomes editáveis de times e jogadores.** Criador e jogador recriam a cena real por conta própria; nós não (plano do PR 25 e decisão 0004).
- **IA sem cara de bot.** Re-peek no mesmo pixel e fila indiana viram clipe contra nós. Os itens de `gdd/12-ia-micro.md` vêm antes de divulgar.

## 4. Brasil e fora: o que muda no conteúdo

O pedido era saber se há diferença. **Há, mas os dados específicos são poucos:** não achei pesquisa pública sobre como o gamer brasileiro descobre jogos por plataforma, nem sobre humor ou memes no marketing de indie. A tabela junta o que tem dado com o que é inferência, separando os dois.

| | Brasil | Fora (inglês) |
|---|---|---|
| **O que é o CS para o público** | Torcida e identidade nacional: a audiência dos campeonatos sobe e desce com os times brasileiros (IEM Rio passou de 800 mil espectadores; um evento caiu 44 % sem o principal time brasileiro) **[dado]** | Cena global, interesse no jogo e na análise **[inferência]** |
| **Tom** | Emoção, narração empolgada, zoeira, "o brasileiro jogando" **[inferência]** | Seco e analítico; humor de "a IA tiltou" **[inferência]** |
| **Onde circula** | Lives multiplataforma (Gaules em YouTube, Twitch e Kick; os maiores picos de 2025 foram no YouTube), cortes de live, X (25,7 % do tráfego do Dust2 Brasil vem do X) e TikTok **[dado]**. Reddit fraco **[inferência]** | Reddit de nicho, YouTube longo, X da cena, TikTok **[dado parcial]** |
| **O que o público de manager quer** | Ver o time vencer e a virada **[inferência]** | Profundidade, sistemas e a história do save (o Football Manager cresceu em comunidade online e no meme de identificação) **[dado]** |
| **Preço** | Sensível a preço; o preço regional pesa **[dado, PR 25]** | Menos sensível **[inferência]** |
| **Idioma** | Português do Brasil, não de Portugal: a tradução europeia soa antiga **[opinião de empresas de localização]**. Localização é o 4º critério de compra no país **[dado de fornecedor, citando Newzoo]** | Inglês |
| **Valor da wishlist** | Volume barato, receita menor por cópia **[dado, PR 25]** | É de onde vem a maior parte da receita **[dado, PR 25]** |

**Na prática [inferência]:**
- **Duas contas ou duas linhas, nunca o mesmo post traduzido.** No Brasil, o mesmo clipe ganha narração em português e legenda de zoeira; lá fora, legenda seca em inglês.
- **No Brasil, o calendário é a cena real.** Postar nos dias de jogo de time brasileiro em campeonato grande, sem citar o time.
- **No Brasil, o canal é o criador médio de CS e os canais de cortes.** O Gaules provavelmente está fora de alcance (PR 25); os canais de cortes vivem de clipe curto e podem pegar o nosso se ele for bom.
- **Lá fora, o canal é a história.** Saves contados em post no Reddit e em vídeo no YouTube, que é onde 52 % dos gamers dizem descobrir jogos (pesquisa de 2024, sem recorte por país) **[dado]**.
- **Twitch é para comunidade, não descoberta:** só 20 % dizem descobrir jogos nela **[dado, 2024]**.

## 5. Comunidades: onde postar

**Reddit (principalmente fora).** Cada sub tem regra própria e muda sem aviso: ler a barra lateral no dia **[opinião]**. A regra dos ~10 % de autopromoção ainda vale em muitos **[opinião]**. Ter histórico de participação antes de postar o jogo **[opinião]**.
- **Prováveis:** subs de manager e simulação, os mesmos onde anúncios tiveram CTR alto no caso *This Grand Life 2* (r/tycoon, r/IndieGaming e parecidos; PR 25) **[dado]**.
- **Com cuidado:** subs de CS. Não confirmei as regras; tratar como "proibido autopromoção" até ler **[inferência]**.

**Brasil.**
- **X:** onde a notícia de CS brasileira circula **[dado]**.
- **Discord:** servidores de comunidade de CS e de times amadores **[dado: existem; não verifiquei regras]**.
- **Gamers Club:** a maior plataforma competitiva de CS do Brasil, com parcerias com marcas (Intel, Logitech G, Ubisoft) **[dado]**. Não achei caminho para indie; vale um contato depois que a demo existir **[inferência]**.
- **Imprensa:** Dust2 Brasil e DRAFT5, com pauta exclusiva (PR 25).

**Nosso Discord.** Começa com a demo. É onde o jogador posta o clipe e a gente reposta (com crédito) **[inferência]**.

## 6. Onde impulsionar

A pesquisa do PR 25 traz os custos por wishlist (Reddit US$ 0,40–2,50; X ~US$ 0,34–0,70 num caso) e as regras de teste. O que este estudo acrescenta **[inferência, salvo onde marcado]**:

| Canal | Onde | Como usar |
|---|---|---|
| **TikTok (impulsionar post que já deu certo)** | Brasil e fora | Pagar só em clipe que já passou da média orgânica. No Brasil o CPM e o CPC saem 50–80 % abaixo dos EUA, mas os números são de blogs de fornecedores e sem recorte de games **[dado fraco]** |
| **Reddit Ads** | Fora | O canal com mais casos documentados para indie (PR 25). Subs de nicho, nunca interesses amplos; mobile ligado |
| **YouTube Shorts / anúncio em vídeo** | Brasil e fora | Sem dado confiável de custo por wishlist; testar só depois do TikTok |
| **Meta (Instagram)** | Evitar no começo | Custo por clique de jogos triplicou de 2025 para 2026 (PR 25) |
| **Criador pago** | Brasil primeiro | Mais barato que nos EUA **[inferência]**; testar 1–2 antes de fechar vários (plano do PR 25) |

**Regras:**
- Brasil e fora em grupos de anúncio separados, cada um com verba própria.
- O criativo do anúncio é o clipe que já viralizou, não uma peça nova.
- Medir por linha de base de wishlists, não só pelo UTM (PR 25).

## 7. Riscos

- **Marca da Valve** (0004): nada de CS, Counter-Strike, Major, nome de arma, mapa, time ou jogador real em clipe, título ou hashtag. Mapa visto de cima que lembre um mapa conhecido também é risco (pergunta aberta em `gdd/05-conteudo.md`).
- **O clipe promete o que o jogo entrega.** Se o bot fizer "fila indiana" na demo, o clipe vira crítica.
- **Arte gerada por IA** no material público foi crítica ao concorrente (PR 25).
- **Viral não é plano.** A base continua sendo página, demo, Next Fest e criadores (plano do PR 25).

## 8. Lacunas

- Não há dado público sobre como o gamer brasileiro descobre jogos por plataforma, nem sobre Kwai.
- Não há dado de custo por wishlist de TikTok, YouTube ou Meta, nem recorte de games no Brasil.
- Não confirmei as regras de autopromoção dos subs de CS e de manager.
- Não achei como *Teamfight Manager* ou *Esports Manager 2026* se espalharam.
- O caminho para parceria com a Gamers Club é desconhecido.

## Perguntas em aberto

- Quem grava, edita e posta os clipes? São 3 por semana, em duas versões (Brasil e fora).
- Teremos narração em português nos clipes? De quem?
- O botão de salvar clipe, a câmera vertical e o card de pós-partida entram no escopo da demo?

## Fontes

**Viral e literatura**
- [Game Developer: de 400 a 20 mil wishlists com um vídeo (The Matriarch)](https://www.gamedeveloper.com/game-platforms/from-400-to-20-000-wishlists-with-1-video-how-tiktok-can-make-your-game-go-viral)
- [How To Market A Game: como viralizar no TikTok](https://howtomarketagame.com/2022/02/14/how-to-go-viral-on-tiktok/)
- [How To Market A Game: influenciadores de TikTok](https://howtomarketagame.com/2024/02/07/how-to-get-tiktok-influencers-to-cover-your-game-and-earn-millions-of-impressions/)
- [Everything PR: indies que viralizaram no TikTok](https://everything-pr.com/indie-games-viral-tiktok)
- [Game World Observer: DREADMOOR e as 200 mil wishlists](https://gameworldobserver.com/2026/05/21/case-study-how-dreadmoor-reached-200000-wishlists-and-where-they-came-from)
- [Recognizing Patterns (Brain Jar): compartilhamento dentro do jogo](https://recognizingpatterns.substack.com/p/i-told-investors-this-game-would)
- [segwise: ganchos de criativo para simulação (gancho e fechamento)](https://segwise.ai/blog/best-fb-ad-creative-hooks-simulation-games)
- [Search Laboratory: STEPPS](https://www.searchlaboratory.com/us/?p=39445)
- [Martech Zone: Contagious](https://martech.zone/contagious-content/)
- [NME: como as redes fizeram do Football Manager um jogo cult](https://www.nme.com/features/how-social-media-helped-football-manager-become-one-of-the-biggest-cult-games-on-the-planet-3140519)
- [The Drum: a marca Football Manager](https://www.thedrum.com/news/2021/05/21/how-build-and-rebuild-brand-football-manager-s-20-year-route-the-top)

**Brasil e descoberta**
- [Streams Charts: maiores streamers brasileiros de 2025](https://streamscharts.com/news/top-brazilian-streamers-2025)
- [games.gg: Twitch pouco importante para descoberta](https://games.gg/pt-BR/news/twitch-plataforma-pouco-importante-para-descoberta-de-jogos/)
- [games.gg: YouTube Shorts e descoberta de jogos](https://games.gg/pt-BR/news/youtube-shorts-moldando-a-descoberta-de-jogos/)
- [Meio & Mensagem: influenciadores mais lembrados entre gamers](https://www.meioemensagem.com.br/marketing/os-influenciadores-mais-lembrados-entre-os-gamers)
- [Exitlag: Gamers Club e CS2 em 2026](https://www.exitlag.com/blog/pt/gamers-club-counter-strike/)
- [Game Developer: localizar para o Brasil](https://www.gamedeveloper.com/audio/now-is-the-time-to-localize-your-game-for-brazil)
- [Level Up Translation: Brasil e localização](https://www.leveluptranslation.com/single-post/now-is-the-time-to-localize-your-game-for-brazil)
- [DiGRA: reviews de um indie brasileiro na Steam](https://dl.digra.org/index.php/dl/article/download/3099/3083/3142)

**Reddit e anúncios**
- [Steam Page Analyzer: Reddit para indie](https://www.steampageanalyzer.com/blog/reddit-indie-game-marketing)
- [presskit.gg: Reddit para indie](https://presskit.gg/field-guides/reddit-marketing-indie-games)
- [bugnet.io: Reddit sem ser banido](https://bugnet.io/blog/reddit-marketing-for-indie-games-without-getting-banned)
- [Stackmatix: custo de TikTok Ads por país](https://stackmatix.com/blog/tiktok-ads-cost-by-country-2026)
- [Stackmatix: custo de anúncio em mercados emergentes](https://stackmatix.com/blog/emerging-market-ad-costs-brazil-india-sea)
- [influee: CPM do TikTok em 2026](https://influee.co/br/blog/tiktok-cpm)

As demais fontes (custos de Reddit e X, criadores, Next Fest, cena brasileira) estão em `marketing/pesquisa-2026-11-09/`, no PR 25.
