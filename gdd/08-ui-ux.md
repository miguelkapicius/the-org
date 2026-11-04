# UI / UX

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0002, 0011

_Telas, HUD e fluxo de navegação._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Fluxo de telas

**[Sugestão]** Menu principal → painel do clube (elenco, finanças, calendário, melhorias) → pré-partida → partida (transmissão) → pós-partida.

### Interface do manager (primeira versão no protótipo)

**[Sugestão]** O protótipo (`prototipo/csmanager`, cena `Manager`, decisão 0011) implementa o painel do clube com navegação lateral e barra superior (nome do clube, número da partida, dinheiro do clube):

| Tela | O que mostra e permite |
|---|---|
| Painel do clube | **O que merece atenção** (avisos acionáveis); cartões de ação (pré-partida, pular partida com a chance de vitória, contratar, Gaming House); elenco em cartões com atributos; últimas partidas (cada uma abre o resumo) |
| Pré-partida | Tela cheia com os dois times frente a frente (brasão redondo, força, os 5 jogadores com função), a **chance de vitória**, os **bonecos 3D do jogo** (TR e CT) grandes nas laterais, cortados pela barra de ações: **Assistir partida** (destaque, lima), **Pular partida**, voltar, elenco. Mapas e plano de jogo aparecem desligados, porque o que se configura aqui é pergunta aberta |
| Elenco | **Escalação visual** (5 titulares e reservas em cartões; clicar em dois jogadores troca de lugar, mostrando antes o efeito na força e na chance de vitória); cobertura de funções; **comparação** de dois jogadores; tabela com **colunas ordenáveis**; escalar, tornar reserva, comparar e vender (com **desfazer**) pelo menu "⋮" |
| Mercado | Jogadores livres, com abas por função e colunas ordenáveis; **prévia** ao passar o mouse (custo, saldo depois, salários, e o que muda na força e na chance de vitória); comparar com qualquer jogador; contratar (com atalho para escalar) |
| Gaming House | Nível atual, custo do próximo, o que cada nível libera (treinos e scouts aparecem bloqueados; ainda sem efeito) |
| Finanças | Saldo, salários, prêmios, extrato e quantas partidas de salários o saldo paga |
| Pós-partida | Resultado, prêmio, salários e saldo antes e depois; **desempenho de cada jogador** (abates, mortes, K/D, HS %, dano por round, primeiro abate) com o melhor da partida. Na partida assistida os números vêm da simulação; na pulada são **estimados** e aparecem avisados como tal |

**Direção visual da interface do manager** (referências dos autores: apps modernos de interface escura e a tela de pré-partida de um jogo de futebol): tema escuro, cartões bem arredondados com borda fina, barra lateral com contadores, abas segmentadas, tabelas só com linhas finas, e **uma** cor de destaque (lima) reservada à ação principal. Laranja é o nosso time e azul o adversário, como na partida. É uma interface **visual, de cartões**, não uma planilha densa. Os retratos dos jogadores são o render 3D do boneco do jogo; hoje todos usam o **mesmo** modelo (a aparência por jogador é pergunta de arte). Ainda não há brasões: o protótipo usa a inicial. O visual de referência está em `prototipo/mockup-ui/` (HTML/CSS, só para aprovar o desenho) e é implementado em USS na Unity (decisão 0011).

**Regras de nitidez** (a interface é feature principal do jogo; **[Sugestão]**): texto nunca abaixo de 13 px na resolução de referência (1600×900); texto secundário com contraste mínimo de ~4,5:1 sobre o cartão; camadas bem separadas (fundo < janela < menu < cartão < item ativo) com bordas visíveis; item ativo do menu e aba escolhida bem marcados (aba escolhida = preenchida, com borda); botões sempre com borda; conteúdo rola em vez de ser cortado.

**Princípios de experiência do manager** (**[Sugestão]**, aplicados no protótipo): (1) o efeito de uma decisão aparece **antes** de confirmá-la (troca de escalação, contratação), ligando a gestão ao que se vê na partida (pilar 3); (2) o jogo diz **o que fazer agora** (avisos no painel), em vez de exigir que o jogador procure; (3) ações que custam dinheiro têm **desfazer** quando possível; (4) avisos de resultado são temporários e não tomam a tela; (5) dicas ao passar o mouse explicam cada atributo; (6) teclas 1 a 5 trocam de tela, Enter assiste e Esc volta na pré-partida; (7) a carreira é **salva sozinha** a cada ação. A "moral" do jogador (citada no loop de sessão) **não** foi implementada: não há regra para ela no GDD.

Os atributos mostrados no elenco e no mercado são uma leitura direta do que a IA dos bots já usa (mira, reflexo, spray, utilitária, sangue-frio), para que o que o manager vê seja o que muda na partida (pilar 3). Os titulares escolhidos entram na partida assistida com o nome e a habilidade deles.

Todos os números da interface (salários, preços, prêmios, custo da Gaming House, tamanho do elenco) são **provisórios**: servem para o protótipo andar e não são decisão de design.

## HUD

HUD da partida no estilo **transmissão competitiva** (decisão 0002):

- visão em primeira pessoa do jogador acompanhado;
- vida (e colete) do jogador acompanhado;
- vida dos companheiros de time;
- minimapa;
- **[Sugestão]** placar, tempo do round, dinheiro de cada jogador, arma equipada, killfeed.

## Feedback ao jogador

_A definir._

## Acessibilidade

_A definir._

## Perguntas em aberto

- O HUD de transmissão é fixo ou o jogador pode configurar?
- Pré-partida: o que o manager configura antes de assistir (veto de mapas, estratégia, compras padrão)?
