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
| Painel do clube | Próxima partida (confronto, força dos dois times) com os botões **Assistir** e **Pular partida**; titulares; últimas partidas; resultado da partida que acabou de acontecer |
| Elenco | Todos os jogadores com atributos; os 5 primeiros são os **titulares**; reordenar; vender |
| Mercado | Jogadores livres para contratar, com atributos, salário e preço |
| Gaming House | Nível atual, custo do próximo, o que cada nível libera (treinos e scouts aparecem bloqueados; ainda sem efeito) |
| Finanças | Saldo, salários, prêmios e extrato |

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
- A interface do manager é densa em tabelas (estilo Football Manager) ou mais visual, com cartões e imagens?
