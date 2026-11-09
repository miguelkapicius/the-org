# Ideias de features (backlog)

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0002, 0003, 0004, 0013, 0014, 0016, 0017, 0018, 0019, 0020, 0021, 0022

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
| 3 | **Melhores momentos no pós-partida** — ✅ primeira versão feita (decisão 0020; falta rever a jogada em 3D) | Eleger a jogada da partida e o MVP da série a partir do replay de melhores momentos que a transmissão já tem; guardar no histórico do jogador | Reforça "assistir é o prêmio" e alimenta as histórias da ideia 4 | `08-ui-ux.md` (pós-partida), `Partida/PartidaAoVivo3D.Replay.cs` | Só a partida assistida tem lances; a pulada não |
| 4 | **Rivalidades e notícias com memória** — ✅ primeira versão feita (decisão 0021, `01-core-loop.md`) | O painel de notícias passa a contar a história do clube: revanche de finais, sequências contra um rival, jovem que virou destaque | Dá significado aos resultados no médio e longo prazo | Notícias do painel do clube (`08-ui-ux.md`), histórico de partidas salvo | Respondido pela decisão 0021 (pontos por série; 3 rivais no máximo) |
| 5 | **Playbook do manager** — ✅ primeira versão feita (decisão 0022, `02-mecanicas.md`) | Por mapa (e lado), o manager marca planos de round **favoritos** e **proibidos**; o IGL respeita | Evolução natural da decisão 0017; profundidade para quem gosta de pensar o jogo, com efeito visível (pilar 3) | Decisão 0017, banco de planos (`Sim/Estrategias.cs`) | Só há um mapa jogável; planos precisam de nome legível para o manager |

## Prioridade 2 — ideias do autor (2026-11-09)

Pedidas pelo autor. O texto normal é a ideia dele; o que está marcado **[Sugestão]** é detalhe do agente, a validar.

### 6. Psicólogo da equipe e relatório de cada jogador

**Ideia do autor:** cada jogador tem sessões com o psicólogo do time, que entrega um **relatório periódico** (semanal, mensal ou outro prazo, **a definir**) dizendo o que o jogador está pensando, a emoção dele e como isso afeta a partida e o time. O manager acompanha e controla esse parâmetro sempre. Na interface, uma **tela do psicólogo**, com o relatório de cada jogador.

**Contratar o psicólogo (ideia do autor, 2026-11-09):** o clube **contrata** o psicólogo, e há psicólogos de **salários diferentes**. O psicólogo **influencia bastante o mental do time**: um psicólogo melhor, com salário maior, mantém o time melhor mentalmente, e qualquer problema fica mais fácil de resolver.

**O que já existe e liga com isso:**
- **Moral** (0 a 100, decisão 0019): sobe e desce com resultados e banco, e muda a habilidade na partida. É o número que o relatório explica.
- **Traços** (decisão 0019): Esquentado, Estrela, Líder... dão o "jeito" de cada um reagir.
- **Psicólogo na Gaming House** (nível 4, decisão 0016): hoje só dá +30 % de recuperação de cansaço.
- **Rivalidades e clássicos** (decisão 0021): mexem na moral em dobro, bom assunto para o relatório.

**[Sugestão] Como pode funcionar:**
- **O relatório**, por jogador: a emoção do momento (ex.: confiante, ansioso, frustrado, desmotivado, com saudade de jogar), o porquê (derrotas seguidas, banco, clássico perdido, contrato no fim, MVP recente) e o efeito na partida em números (ex.: "-2 % de habilidade, troca menos os colegas").
- **Ações do manager** a partir do relatório: conversa individual, folga, prometer vaga de titular (que vira compromisso), mais sessões com o psicólogo. Cada uma com efeito e custo (tempo de treino, dinheiro, risco de não cumprir a promessa).
- **Efeito no time:** um jogador muito abalado puxa o entrosamento ou a moral dos outros para baixo; um Líder confiante puxa para cima.
- **Mercado de psicólogos:** algumas opções por vez (ex.: 3 a 5, renovadas a cada temporada), cada uma com **qualidade** (ex.: 1 a 5 estrelas), **salário semanal** que cresce com a qualidade e um contrato (duração e multa para demitir, como os contratos dos jogadores, decisão 0035). Sem psicólogo, o clube não tem relatório nem as ações.
- **O que a qualidade muda** (quanto melhor, mais forte):
  - **Proteção:** amortece as quedas de moral (ex.: a derrota que tira 3 tira 2) e acelera a volta ao normal;
  - **Resolver problemas:** mais chance de as ações do manager funcionarem (conversa, folga, promessa) e de uma crise (jogador desmotivado, briga, Estrela no banco) acabar logo;
  - **Relatório:** o bom psicólogo diz a causa e prevê o que vem (ex.: "vai pedir para sair se seguir no banco"); o fraco só diz a emoção;
  - **Time:** ajuda a segurar o entrosamento e o efeito do clássico (decisões 0019 e 0021).
- **Custo:** o salário entra nas despesas semanais (decisão 0015) junto com os dos jogadores; um psicólogo caro num clube pequeno pesa no caixa. É uma escolha de gestão: gastar no mental ou em jogador.
- **Gaming House:** a sala do psicólogo pode ser o que o nível 4 libera (hoje o nível 4 já cita o psicólogo, decisão 0016), ou o psicólogo pode ser contratado desde o início e o nível 4 só melhorar o trabalho dele (mais sessões por semana). A definir.
- **Depois:** o mesmo modelo serve para outros profissionais (fisioterapeuta, analista de demos, preparador), se fizer sentido.

**Liga com:** `02-mecanicas.md` (moral e traços), `03-progressao.md` (Gaming House), `08-ui-ux.md` (tela nova), decisões 0016, 0019 e 0021.

### 7. Central de notificações na tela principal

**Ideia do autor:** na tela principal, uma **aba de notificações** com o resumo do que está acontecendo de mais importante e do que o manager precisa mexer agora, para não ter que entrar em cada tela. **Clicar numa notificação leva para o lugar onde aquilo se resolve.**

**O que já existe e liga com isso:**
- O painel do clube tem a lista **"O que merece atenção"**: avisos acionáveis, cada um com um botão que leva à tela certa (saldo baixo, cansaço, função sem titular, scout parado, moral baixa...).
- O hub tem **selos** nos cartões de Treino, Calendário e Mercado com quantos avisos pedem atenção ali (decisão 0033).
- As **notícias do clube** contam o que aconteceu (decisões 0020 e 0021), mas não levam a nenhuma tela.

**[Sugestão] Como pode funcionar:**
- Uma aba/painel único que junta avisos (o que fazer) e acontecimentos importantes (o que aconteceu: contrato vencendo, proposta por jogador, relatório novo do psicólogo, clássico na semana), por **urgência**.
- Cada notificação tem **lida/não lida**, um contador no menu e o atalho para a tela certa, já no item certo (ex.: abre o jogador no elenco, não só o elenco).
- As que se resolvem sozinhas somem; as informativas ficam num histórico curto.

**Liga com:** `08-ui-ux.md` (painel do clube, princípio "o jogo diz o que fazer agora"), decisões 0015, 0028 e 0033.

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

As perguntas das 5 primeiras prioridades foram respondidas pelas decisões 0018 a 0022 (propostas, a validar).

- Psicólogo: de quanto em quanto tempo sai o relatório de cada jogador (semanal, quinzenal, mensal)?
- Psicólogo: além da moral, quais emoções/estados o relatório mostra e como cada um muda a partida e o time?
- Psicólogo: o manager pode agir a partir do relatório (conversa, folga, promessa de vaga)? Com que custo?
- Psicólogo: pode ser contratado desde o início ou só com o nível 4 da Gaming House (onde o psicólogo já está)?
- Psicólogo: quanto o melhor psicólogo vale a mais que o pior (o tamanho do efeito de cada nível de qualidade) e quanto custa cada nível?
- Psicólogo: tem contrato (duração, multa para demitir) como os jogadores? Dá para ter mais de um?
- Notificações: a central substitui a lista "O que merece atenção" do painel ou fica ao lado dela? Guarda histórico e lida/não lida?
