# O que a interface do manager já comporta e o jogo ainda não entrega

Lista de trabalho do protótipo. Cada item é algo que **já aparece ou já é aceito na interface** (ou no modelo da carreira), mas
que **não existe de verdade no jogo** (conteúdo, simulação ou sistema). Foi conferido no código em 2026-11-06; se algo mudar,
atualize a linha. Isto é lista de tarefas, não contrato: o que vale como design continua em `gdd/` e `decisoes/`.

Legenda de tamanho: **P** = dias, **M** = semanas, **G** = projeto grande.

## 1. Conteúdo

| Item | Onde a interface já mostra | O que falta | Tam. |
|---|---|---|---|
| **Mais mapas** | Pool de 7 mapas (`Mapas.NOMES` em `Manager/Mundo.cs`): veto da série, afinidade de cada time com cada mapa, estudo de mapa no treino, cartões de mapa na série | Só **Forja** (mapa 0) tem geometria (`Mapas.Tem3D`). Os outros 6 rodam na geometria da Forja: o mapa só muda a afinidade e a ordem do veto, nunca o que se vê. Faltam geometria, navegação (`NavGrid`), posições e rotas da IA (`rotas.bin`, `posicoes.txt`, `aparece.bin`) e visual 3D de cada um. A `MontarPartida` calcula o mapa mas não o passa à simulação | G (por mapa) |
| **Imagem de cada mapa** | Os cartões de mapa na série mostram só nome e a etiqueta "mapa 3D disponível" ou "ainda sem 3D" | Uma miniatura (render do 3D ou planta) por mapa, para o veto parecer um veto | P |
| **Aparência por jogador** | Cartões de jogador no painel, no elenco, no mercado e na pré-partida | Todos os jogadores usam o **mesmo boneco 3D** (`BonecosPreview`) e o retrato é um círculo com a inicial. Faltam variações de rosto, cabelo, uniforme e cor, e o retrato 3D por jogador | M |
| **Brasão e uniforme do clube** | Brasão no menu, na pré-partida e no calendário | O brasão é um quadrado com a inicial do clube. Falta arte de brasão e escolha do clube na criação da carreira (nome, cores) | M |
| **Times adversários com elenco de verdade** | Pré-partida, scouts e calendário mostram nome, função e estilo de 48 times | Os adversários têm só **nome e função** sorteados (`Mundo.cs`). Na partida, os 5 jogadores têm a **mesma habilidade** (a força do time, com ±4 de ruído). Faltam atributos, idade e história por jogador adversário, e roubar/contratar jogador de outro time | M |
| **Contexto do mundo** | Nomes de campeonatos e times | Os 48 times e as copas têm nomes gerados, sem identidade (cidade, torcida, rivalidade) | M |

## 2. Simulação e IA

| Item | Onde a interface já mostra | O que falta | Tam. |
|---|---|---|---|
| **Estilos de jogo personalizados** | A tela de série lista 5 estilos fixos (`Estilo`: Equilibrada, Agressiva, Defensiva, Execuções rápidas, Controle de mapa) e a decisão 0014 fala em "presets" | Não há como o manager **criar ou ajustar** um estilo. Falta um editor de plano de jogo (sliders de ritmo, agressividade, uso de utilitária, quem faz entry, estratégia por lado), salvar estilos com nome e passar tudo para `Pesos`. O botão "Plano de jogo" da pré-partida estava desligado por isso | G |
| **Estilo por lado (ataque e defesa)** | Um único estilo por time | Hoje o mesmo estilo vale para o lado TR e o CT. Falta escolher um estilo de ataque e outro de defesa | M |
| **Estilos mais fundos** | Descrições longas de cada estilo (`Estilos.Descricao`) | Cada preset mexe em poucos pesos da IA (`Estilos.Pesos`: agressão, tempo de execução, quietude, finta, avanço). Falta comportamento próprio (ex.: Controle de mapa realmente tomando áreas, Execuções rápidas com pacotes de utilitária) e **medir no Headless** se o ciclo "Agressiva > Defensiva > Execuções rápidas > Controle de mapa > Agressiva" aparece de fato nos resultados, em vez de só valer na fórmula de chance (`Estilos.Relacao`) | G |
| **Bônus de contra-estratégia** | Selo "contra ele" no veto e na escolha de estratégia, e a chance de vitória da pré-partida | O bônus é um número na fórmula da chance e na habilidade dos jogadores (`MultEfetivo`). Falta decidir se ele deve vir só do comportamento da IA | M |
| **Informação nível 3 do adversário** | Os scouts prometem "estratégia detalhada e jogador de destaque" (`GerenteUI.Scouts.cs`) | Só aparece o jogador de destaque, escolhido por uma conta (`(id + temporada) % jogadores`), sem relação com quem joga melhor. A "estratégia detalhada" não existe: falta mostrar, por exemplo, as execuções que o time mais usa | M |
| **Pós-partida de mapa pulado** | Tabela de desempenho dos jogadores com abates, mortes, K/D e dano | Quando o mapa é pulado, os números são **estimados** (a tela avisa), pois não houve simulação. Falta calibrar essa estimativa rodando muitas partidas no Headless (já pedido em `gdd/09-tecnico.md`) | M |
| **Timeouts** | A decisão 0001 prevê 3 por partida, com escolha de estilo | Não há botão nem tela na transmissão. A partida precisa de pausa entre rounds com o menu de estilo | M |
| **Replays** | A pergunta aberta em `gdd/09-tecnico.md` | A simulação não é gravada. Falta decidir se precisa ser determinística | G |

## 3. Sistemas de gestão

| Item | Onde a interface já mostra | O que falta | Tam. |
|---|---|---|---|
| **Sala de imprensa (patrocínio)** | Texto em Finanças: "Outras ideias para depois" | Só a ideia (decisão 0016). Não há tela, custo nem efeito | M |
| **Academia de base** | Idem | Jogadores jovens contratáveis de graça ou barato, com potencial alto | M |
| **Laboratório de demos** | Idem | Treino contra a IA de um adversário específico | M |
| **Contrato de jogador e cláusula** | Idem | Hoje o jogador só tem salário. Faltam duração, renovação, cláusula de rescisão e propostas de outros times pelos seus jogadores | M |
| **Negociação no mercado** | Mercado com "Contratar" a preço fixo | Falta negociar salário e valor, disputar com outros times e vender para um time específico | M |
| **Resultado da temporada** | Calendário, circuitos (Amador, Pro, Elite), ranking e a tela animada de **fim de temporada** (decisão 0040: promoção e rebaixamento, resumo do ano, aposentadorias e o que muda) | Faltam as **metas do clube** (o que a diretoria espera da temporada). A tela de fim de temporada ainda não foi aberta no Unity | P |

## 4. Interface

| Item | Situação | O que falta | Tam. |
|---|---|---|---|
| **Busca, notificações e filtros** | O mockup (`prototipo/mockup-ui`) tem caixa de busca, sino e botão de filtros | A interface na Unity não tem busca nem sino. Os avisos existem (`Carreira.Avisos`) mas aparecem só no painel | P |
| **Vários salvamentos** | Um único arquivo `carreira.json` (`Salvamento.cs`) | Falta tela de carreiras salvas com nome, data e botão de apagar | P |
| **Menu principal e opções** | O jogo abre direto no painel | Faltam menu inicial (nova carreira, continuar, opções), volume, resolução e idioma | M |
| **Som da interface** | A transmissão tem som; o manager não | Cliques, avisos, resultado de partida e música | M |
| **Tamanhos de tela** | Escala fixa de 1600x900 com `ScaleWithScreenSize` | Falta conferir telas ultrawide e 4K e o toque em tela pequena | P |
| **Textos e idioma** | Tudo em português dentro do código | Falta tirar os textos do código para tabelas, caso o jogo ganhe outro idioma | M |

## 5. Build e infraestrutura

| Item | Situação | O que falta | Tam. |
|---|---|---|---|
| **A build precisa ser regenerada** | `CSManager.exe` e `CSManager_Data` em `Build/` foram gerados **antes** de a build incluir a cena do manager: a build versionada abre direto na simulação | Rodar **CS Manager ▸ Gerar jogo (.exe)** no Unity e commitar o resultado. O gerador agora cria a cena `Manager`, deixa o Build Settings com `Manager` em primeiro e `Partida3D` em segundo, e usa as duas | P |
| **Cena `Manager.unity` não versionada** | É criada pelo menu e pelo gerador de build, mas o arquivo da cena não está no repositório | Commitar `Assets/Scenes/Manager.unity` (e o `.meta`) depois de gerar uma vez | P |
| **Teste da interface** | As regras são testadas em `ManagerTest` e no `TestarCarreiraBatch`; a interface não | Falta um teste que abra cada tela e confira que nenhuma gera erro (hoje só `CapturarBatch` salva um PNG por tela) | M |
| **Sem CI** | Nenhum check roda nos PRs | Rodar `ManagerTest` e o Headless nos PRs | P |

## Como esta lista se liga ao GDD

- Quantos mapas no MVP e a lista de armas: `perguntas-abertas.md`, seção 3.
- Estilos, veto e estratégia: decisão 0014. Scouts e Gaming House: 0016. Treino: 0013. Finanças: 0015. Campeonatos: 0012.
- Ao resolver um item, atualize esta lista **e** a seção do GDD correspondente (e remova a pergunta aberta, se houver).
