# Ideias de features (backlog)

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0002, 0003, 0004, 0013, 0014, 0016, 0017, 0018, 0019

_Banco de ideias para deixar o jogo mais envolvente ("só mais uma semana"). Nada aqui é decisão: cada ideia vira decisão em `decisoes/` antes de ser implementada. Ideias do agente, priorizadas pelo autor em 2026-11-06._

> Tudo nesta seção é **[Sugestão]**: proposta do agente, ainda não validada.

## Como usar

- As ideias da **prioridade 1** são as próximas a detalhar e implementar, nesta ordem.
- Ao começar uma ideia: proponha a decisão (`/propor-decisao`), mova o detalhe para a seção do GDD certa e deixe aqui só o link.
- Ideia nova entra no grupo certo. Ideia descartada fica riscada, com o motivo, para não ser proposta de novo.

## O que faz um manager envolvente

Três camadas, cada uma com uma razão para continuar jogando:

1. **Curto prazo:** uma partida tensa logo à frente, com decisões que aparecem na tela (pilar 3).
2. **Médio prazo:** um objetivo na temporada (campeonato, meta da diretoria, jogador para revelar).
3. **Longo prazo:** a história do clube, que só aquele jogador viveu (rivalidades, ídolos, troféus).

**Limite:** o envolvimento vem de histórias e de decisões com consequência. **Nada de loot box, dinheiro real ou mecânica de aposta**: vai contra o público que gosta de "pensar o jogo" e cria risco na Steam.

## Prioridade 1 — as cinco primeiras

| # | Ideia | O que é | Por que vale | Liga com | Depende de |
|---|---|---|---|---|---|
| 1 | **Timeout de verdade** — ✅ primeira versão feita (decisão 0018, `01-core-loop.md`) | O manager pede um timeout (3 por partida); a transmissão pausa, ele escolhe o estilo e o time muda na hora, com chance de funcionar | É a única intervenção na partida (decisão 0001) e ainda não existe (item E5 de `10-interface-vs-jogo.md`). Agora o estilo muda os planos de round, então o efeito é real | `01-core-loop.md` (timeout), decisões 0014 e 0017 | Pesos da chance de funcionar (balanceamento). Falta: timeout do adversário |
| 2 | **Jogadores com personalidade** — ✅ primeira versão feita (decisão 0019, `02-mecanicas.md`) | **Traços** (ex.: clutch, tilta depois de morrer cedo, líder, estrela vaidosa), **moral** e **entrosamento** entre duplas que muda a simulação (ex.: velocidade do trade kill) | Cria histórias ("ele sempre aparece no Major") e decisões de elenco além do número geral | `02-mecanicas.md` (atributos), `03-progressao.md`, sessões coletivas da Gaming House (hoje só dão preparo, item T2) | Pergunta aberta: quais atributos o jogador tem. Falta: traços no adversário (depende de J3) |
| 3 | **Melhores momentos no pós-partida** | Eleger a jogada da partida e o MVP da série a partir do replay de melhores momentos que a transmissão já tem; guardar no histórico do jogador | Reforça "assistir é o prêmio" e alimenta as histórias da ideia 4 | `08-ui-ux.md` (pós-partida), `Partida/PartidaAoVivo3D.Replay.cs` | Só a partida assistida tem lances; a pulada não |
| 4 | **Rivalidades e notícias com memória** | O painel de notícias passa a contar a história do clube: revanche de finais, sequências contra um rival, jovem que virou destaque | Dá significado aos resultados no médio e longo prazo | Notícias do painel do clube (`08-ui-ux.md`), histórico de partidas salvo | Regra do que vira rivalidade (número de confrontos? finais?) |
| 5 | **Playbook do manager** | Por mapa (e lado), o manager marca planos de round **favoritos** e **proibidos**; o IGL respeita | Evolução natural da decisão 0017; profundidade para quem gosta de pensar o jogo, com efeito visível (pilar 3) | Decisão 0017, banco de planos (`Sim/Estrategias.cs`) | Só há um mapa jogável; planos precisam de nome legível para o manager |

## Outras ideias

### Na partida (assistir é o prêmio)

- **Modo resumo:** assistir só os rounds decisivos (pistol, match point, clutch) e pular o resto, para não cansar com 24 rounds.
- **Palpite do round:** antes do round, o manager palpita onde o ataque vai ("vão no B"); acertar dá reputação ao manager. Sem aposta de dinheiro.
- **Narração com contexto:** o narrador usa a história ("5 rounds sem abate", "primeira final dele").
- **Pressão nos Majors:** torcida e pressão no Major, com peso maior do sangue-frio; o Major parece especial.

### Jogadores e elenco

- **O jovem que vira craque:** a revelação do potencial pelo scout é a recompensa "de baú", só que merecida.
- **Propostas e pedidos de saída:** outros clubes fazem propostas pelos seus jogadores; jogadores insatisfeitos pedem para sair (depende da pergunta sobre contratos).
- **Fim de carreira:** aposentadoria, Hall da Fama do clube, ex-jogadores que voltam como scouts.

### Gestão

- **Estudar a demo do adversário:** preparar o contra de um adversário específico (liga scouts, estilos e IA).
- **Metas da diretoria e dos patrocinadores:** objetivos por temporada ("chegar ao Pro", "top 8 no Major"), com bônus ou pressão.
- **Bootcamp antes do Major:** gasta dinheiro e semanas em troca de preparo.

### Longo prazo

- **Dinastia:** sala de troféus e linha do tempo do clube.
- **Cenários de desafio:** "salve um clube à beira da falência", "do Amador ao Major em 3 temporadas".
- **Recordes e conquistas:** maior sequência de vitórias, maior virada, e assim por diante.

### Social (depende da pergunta "single-player ou online")

- **Liga assíncrona entre amigos:** cada um gerencia seu time; as partidas rodam na simulação.
- **Compartilhar:** replay de uma jogada ou o seed de uma carreira.

## Perguntas em aberto

- Playbook: o manager escolhe planos por mapa e por lado? Quantos favoritos e proibidos pode marcar?
- O que transforma um adversário em rival (número de confrontos, finais, eliminações)?
