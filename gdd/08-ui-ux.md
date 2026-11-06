# UI / UX

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0002, 0011, 0012, 0013, 0014, 0015, 0016, 0018, 0019, 0020, 0021, 0022, 0023

_Telas, HUD e fluxo de navegação._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Fluxo de telas

**[Sugestão]** Menu principal → painel do clube (elenco, finanças, calendário, melhorias) → pré-partida → partida (transmissão) → pós-partida.

### Interface do manager (primeira versão no protótipo)

**[Sugestão]** O protótipo (`prototipo/csmanager`, cena `Manager`, decisão 0011) implementa o painel do clube com navegação lateral e barra superior (nome do clube, número da partida, dinheiro do clube):

| Tela | O que mostra e permite |
|---|---|
| Painel do clube | **Hub** de 4 cartões com ilustração em tela cheia (o compromisso da semana, com o único botão; treino, calendário e mercado, em que o cartão inteiro é o botão e o texto diz só o essencial); **o que merece atenção** (avisos acionáveis, só quando há algum e sem repetir o que o hub já mostra); **notícias** e **últimas partidas** lado a lado (cada partida abre o resumo; o detalhe fica na dica); rivais. O elenco tem tela própria e saiu do painel. No painel, o cartão de próximo passo do menu some, porque o hub já faz esse papel |
| Calendário | **Grade de mês** com datas reais e dias da semana (a temporada 1 é 2027): a semana atual com borda lima, as semanas de campeonato com fundo marcado, e cada série do clube no dia em que cai (ligas na quarta; campeonatos de quinta a sábado) com o resultado depois de jogada. Navegação entre os meses, botão Hoje e os próximos compromissos ao lado. Os campeonatos (lista e detalhe de cada um) e o ranking dos 48 times ficam em abas à parte, para a tela principal ficar limpa (decisão 0012) |
| Elenco | **Só cartões, enxutos** (decisão 0023): os 5 titulares e as reservas; cada cartão tem retrato com a função e o geral, nome, idade e salário (potencial e preço de venda na dica), o **estado em palavras** (EM FORMA, CANSADO, EXAUSTO; MOTIVADO, MORAL BAIXA, DESMOTIVADO; MVP), com o número na dica, os 5 atributos que a IA usa (explicação de cada um na dica) e **uma ação principal larga** (Escalar, em índigo, ou Para a reserva) mais comparar e vender só com ícone. No topo, uma linha: **força** e **chance de vitória**; à direita só o que pede atenção (FALTA uma função, POUCO ENTROSADO) ou, se nada falta, "funções completas". Passar o mouse em Escalar ou Para a reserva troca essa linha pela prévia da troca (força e chance antes e depois), sem a tela pular. A comparação de dois jogadores abre sob o resumo. Vender tem desfazer |
| Treino | Foco e sessões por semana de cada jogador, sessões coletivas e estudo de mapa (conforme a Gaming House), cansaço e preparo de cada um, o efeito previsto da semana e o efeito líquido no time (decisão 0013) |
| Mercado | Jogadores livres, com abas por função e colunas ordenáveis; **prévia** ao passar o mouse (custo, saldo depois, salários, e o que muda na força e na chance de vitória); potencial só aparece com scout; comparar com qualquer jogador; contratar (com atalho para escalar) |
| Scouts | Tarefas dos scouts (estudar um adversário ou procurar talentos no mercado) e o que se sabe de cada time, por nível de informação (decisão 0016) |
| Playbook | As jogadas do banco de planos, por lado (ataque e defesa), com quantos planos, vitória do time pro, força medida e estilo; marcar **favoritas** (o IGL prefere) e **proibidas** (o IGL não escolhe), com vagas pela Gaming House; clicar na jogada abre a **planta do mapa com a rota de cada jogador** (uma cor por jogador, X onde morre), a rota em palavras, os **pontos fortes e fracos** e "Outro exemplo" (decisão 0022) |
| Preparar série | Veto de mapas (banir e escolher, ou veto automático), afinidade com cada mapa, estratégia com a dica do contra quando há informação do adversário, chance de vencer a série (decisão 0014) |
| Pré-partida | Tela cheia de cada mapa: os dois times frente a frente (brasão redondo, força, os 5 jogadores), a **chance de vencer o mapa**, os **bonecos 3D do jogo** nas laterais e a barra de ações: **Assistir mapa** (lima), **Pular mapa**, **Pular série**, voltar, estratégia |
| Gaming House | Os 5 níveis, o que cada um libera, custo e manutenção, e o botão de subir (decisão 0016) |
| Finanças | Saldo, fluxo semanal (patrocínio, salários, manutenção, parcelas), **empréstimo** (pegar e quitar), campeonatos disputados e extrato; aviso do prazo de falência (decisão 0015) |
| Pós-partida | Resultado do mapa e da série, campeonato e prêmio quando acaba; **desempenho de cada jogador** (abates, mortes, K/D, HS %, dano por round, primeiro abate). Na partida assistida os números vêm da simulação; na pulada são **estimados** e avisados como tal. **Jogada da partida** (só na assistida: o lance de maior nota, dizendo se foi do seu time ou do adversário), selo **MVP** no melhor do mapa e **MVP da série** no último mapa (decisão 0020) |

**Direção visual da interface do manager: "vidro noturno"** (2026-11-06; referências dos autores: um dashboard de tarefas em vidro sobre céu noturno, um lobby de jogo com cartões escuros e personagens saindo do cartão, um conceito de menu de CS:GO com abas sublinhadas e cantos quase retos, e um app de estudos com janela translúcida sobre foto). A interface anterior (preto puro, cartões bem arredondados, lima) ficou "artificial" para os autores. Agora: **fundo azul-noite em degradê** com brilho índigo e poucas estrelas (gerado em código); **painéis translúcidos** (a janela, o menu e os cartões deixam o fundo aparecer) com **borda fina** azulada; **cantos pouco arredondados** (3 a 6 px; só avatares, brasões e pontos continuam redondos); **abas sublinhadas** (a escolhida tem o traço em destaque); item ativo do menu com **barra de destaque à esquerda**. A cor de destaque passou de lima para **índigo** (`--destaque`, #5b7bff), reservada à ação principal, à aba escolhida e à semana atual; o **hub** do painel virou cartões de vidro (o compromisso é o cartão índigo). Laranja continua sendo o nosso time e azul-claro o adversário, como na partida; o índigo do destaque é mais escuro e mais roxo que o azul do adversário para não confundir. É uma interface **visual, de cartões**, não uma planilha densa. Os retratos dos jogadores são o render 3D do boneco do jogo; hoje todos usam o **mesmo** modelo (a aparência por jogador é pergunta de arte). Ainda não há brasões: o protótipo usa a inicial. O visual de referência está em `prototipo/mockup-ui/` (HTML/CSS, só para aprovar o desenho) e é implementado em USS na Unity (decisão 0011). **Atenção:** o "vidro noturno" foi feito direto no `Manager.uss` (conferido pelas capturas do `CapturarBatch`); o mockup HTML ainda está no visual antigo.

**Ilustrações do hub:** os cartões do painel usam artes geradas com a IA do Figma (arquivo "CS Manager — artes do hub" do autor), no estilo noturno índigo com os bonecos do jogo (balaclava, colete); ficam em `Assets/Resources/ManagerUI/Hub/` (JPG 640×800) e recebem um véu escuro embaixo, gerado em código, para o texto ficar legível. São provisórias: a arte final do jogo é pergunta em aberto.

**Regras de simplicidade** (decisão 0023, pilar 5: complexo por dentro, simples de entender): uma informação, um lugar (nada repetido na mesma tela); uma ação principal por tela, em índigo; conclusão antes do número (o detalhe vai para a dica ou para a tela própria); texto curto, sem termos internos (nada de "decisão 0016" na tela do jogador); subtítulo de tela em uma frase curta; o saldo no cabeçalho, a despesa na dica.

**Regras de nitidez** (a interface é feature principal do jogo; **[Sugestão]**): texto nunca abaixo de 13 px na resolução de referência (1600×900); texto secundário com contraste mínimo de ~4,5:1 sobre o cartão; camadas bem separadas (fundo < janela < menu < cartão < item ativo) com bordas visíveis; item ativo do menu e aba escolhida bem marcados (aba escolhida = preenchida, com borda); botões sempre com borda; conteúdo rola em vez de ser cortado.

**Princípios de experiência do manager** (**[Sugestão]**, aplicados no protótipo): (1) o efeito de uma decisão aparece **antes** de confirmá-la (troca de escalação, contratação), ligando a gestão ao que se vê na partida (pilar 3); (2) o jogo diz **o que fazer agora** (avisos no painel), em vez de exigir que o jogador procure; (3) ações que custam dinheiro têm **desfazer** quando possível; (4) avisos de resultado são temporários e não tomam a tela; (5) dicas ao passar o mouse explicam cada atributo; (6) teclas 1 a 9 trocam de tela, Enter assiste e Esc volta na pré-partida; (7) a carreira é **salva sozinha** a cada ação. **Personalidade** (decisão 0019): o traço aparece como selo ao lado do nome (explicação ao passar o mouse; no mercado só depois do scout), a moral aparece embaixo do nome no elenco e como barra no cartão do painel, e o entrosamento do time fica no topo da escalação. Moral baixa e time pouco entrosado viram avisos no painel. **Rivalidades** (decisão 0021): a preparação da série mostra o retrospecto contra o adversário e o selo CLÁSSICO quando ele é rival; o painel do clube lista os rivais (o primeiro como ARQUIRRIVAL) com retrospecto e sequência.

Os atributos mostrados no elenco e no mercado são uma leitura direta do que a IA dos bots já usa (mira, reflexo, spray, utilitária, sangue-frio), para que o que o manager vê seja o que muda na partida (pilar 3). Os titulares escolhidos entram na partida assistida com o nome e a habilidade deles.

**O que a interface mostra e o jogo ainda não faz** (só um mapa jogável, estratégias com efeito não medido, adversários sem jogadores de verdade, timeout etc.) está listado em `10-interface-vs-jogo.md`. Consulte antes de mexer nessas telas.

Todos os números da interface (salários, preços, prêmios, custo da Gaming House, tamanho do elenco) são **provisórios**: servem para o protótipo andar e não são decisão de design.

## HUD

HUD da partida no estilo **transmissão competitiva** (decisão 0002):

- visão em primeira pessoa do jogador acompanhado;
- vida (e colete) do jogador acompanhado;
- vida dos companheiros de time;
- minimapa;
- **[Sugestão]** placar, tempo do round, dinheiro de cada jogador, arma equipada, killfeed.
- **[Sugestão]** **Timeout** (decisão 0018): botão "Timeout (T)" com quantos restam; abre um painel que pausa a partida, mostra a chance de o time absorver (e de onde ela vem) e os 5 estilos com a descrição; o resultado aparece num aviso no alto da tela por alguns segundos.

## Feedback ao jogador

_A definir._

## Acessibilidade

_A definir._

## Perguntas em aberto

- O HUD de transmissão é fixo ou o jogador pode configurar?
- Mockup HTML (`prototipo/mockup-ui/`): atualizar para o visual "vidro noturno" ou aposentar e aprovar o visual direto pelas capturas da Unity? (muda o fluxo da decisão 0011)
- Pré-partida: o manager também configura as **compras padrão** do time? (veto de mapas e estratégia já estão decididos, decisão 0014)
