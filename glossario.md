# Glossário

Termos oficiais do jogo. Agentes e humanos usam **só** estes termos. Termo novo entra aqui no mesmo PR em que aparece pela primeira vez.

| Termo | Definição | Não usar | Seção |
|---|---|---|---|
| Manager | O jogador: técnico/dono do time, que gerencia e assiste. | treinador (até decidirmos) | 00-visao |
| Bot | Jogador do time controlado pela IA durante a partida. | NPC | 02-mecanicas |
| Partida | Confronto completo entre dois times, composto por rounds. | jogo, match | 02-mecanicas |
| Round | Uma rodada da partida, com compra no início. | rodada | 02-mecanicas |
| Transmissão | O modo de assistir à partida em 3D, com HUD de campeonato. | replay (replay é outra coisa) | 08-ui-ux |
| Economia do round | Dinheiro de cada bot para comprar armas e utilitárias dentro da partida. | — | 03-progressao |
| Economia do clube | Dinheiro do time para salários, contratações e melhorias. | — | 03-progressao |
| Melhoria | Algo que o manager compra para evoluir o time. | upgrade | 03-progressao |
| Gaming House | A instalação do time. Subir o nível dela libera outras melhorias, como treinos e scouts. | CT, base, sede, instalação | 03-progressao |
| Scout | Olheiro que encontra jogadores para contratar. | olheiro | 03-progressao |
| Abrir pixel | Expor o mínimo do corpo para ver/atirar num ângulo. | — | 02-mecanicas |
| Bomba | Objeto que o ataque planta para vencer o round (nome próprio a definir). | C4 | 02-mecanicas |
| Bombsite | Local do mapa onde a bomba pode ser plantada. | site | 02-mecanicas |
| Pular partida | Ver só o resultado, calculado por probabilidade, sem assistir. | simular (ambíguo) | 01-core-loop |
| Falência | Fim do jogo quando o dinheiro do clube acaba. | game over (usar só como descrição) | 03-progressao |
| Elenco | Todos os jogadores contratados pelo clube (titulares e reservas). | squad, plantel | 03-progressao |
| Titular | Jogador do elenco que joga a próxima partida (5 por vez). | — | 03-progressao |
| Reserva | Jogador do elenco que não joga a próxima partida. | banco | 03-progressao |
| Mercado | Lista de jogadores livres que o manager pode contratar. | — | 03-progressao |
| Escalação | Os 5 titulares que o manager escolhe e as funções deles. Trocar a escalação muda a força do time. | lineup, line-up, formação | 08-ui-ux |
| Força | Número que resume o time: a média do geral dos 5 titulares. Entra no cálculo da chance de vitória. | overall do time, rating | 08-ui-ux |
| Chance de vitória | Probabilidade de vencer a próxima partida, calculada a partir da força dos dois times. É a mesma usada ao pular a partida. | odds | 08-ui-ux |
| Campeonato | Competição com times, formato (liga, grupos, suíço, mata-mata...) e premiação. | torneio, evento (usar só como sinônimo informal) | 01-core-loop |
| Série | Confronto entre dois times decidido em um número de mapas: MD1, MD3 (melhor de 3) ou MD5 (melhor de 5). Cada mapa é uma partida. | md, bo3 | 01-core-loop |
| Temporada | Um ano do jogo, com 52 semanas e o calendário de campeonatos. | ano, season | 01-core-loop |
| Circuito | Grupo de campeonatos de um mesmo nível: Amador, Pro ou Elite. | tier, divisão | 01-core-loop |
| Ranking | Lista dos 48 times por pontos de colocação em campeonatos; decide o circuito do clube. | classificação | 01-core-loop |
| Veto | Fase antes da série em que os times banem e escolhem mapas. | pick/ban | 08-ui-ux |
| Pool de mapas | Os 7 mapas que podem entrar no veto de uma série. | map pool | 08-ui-ux |
| Mapa jogável | Mapa do pool que tem geometria 3D e os dados da IA, e por isso pode ser assistido. Hoje só um. | mapa 3D (ambíguo) | 10-interface-vs-jogo |
| Afinidade | O quanto um time domina um mapa (0 a 100); entra na habilidade na partida. | domínio | 02-mecanicas |
| Estratégia | Conjunto de ajustes de estilo da IA do time (agressiva, defensiva, execuções rápidas...) escolhido na pré-partida. | tática, plano de jogo | 02-mecanicas |
| Plano de round | Um round pro gravado no banco da IA (rotas e tempos dos 5 jogadores) que o IGL segue. Cada plano tem um dos 5 estilos da estratégia. | tática, play | 02-mecanicas |
| Treino | Plano semanal que o manager define para cada jogador (foco e frequência). Evolui atributos e gera preparo e cansaço. | prática | 02-mecanicas |
| Preparo | Bônus temporário de habilidade nas partidas que vem do treino recente. | buff | 02-mecanicas |
| Cansaço | Desgaste dos jogadores por treino e partidas; acima de um limite reduz a habilidade. | fadiga, stamina | 02-mecanicas |
| Potencial | Teto de evolução de um atributo de um jogador; só se vê com scout. | talento | 03-progressao |
| Empréstimo | Dinheiro que o banco adianta ao clube, pago em parcelas semanais com juros. | dívida (usar só para o saldo negativo) | 03-progressao |
| Pré-partida | Tela antes da partida: os dois times frente a frente e a escolha entre assistir e pular. | lobby, prematch | 08-ui-ux |
| Pós-partida | Tela depois da partida: resultado, dinheiro e desempenho de cada jogador. | resumo final | 08-ui-ux |
| Painel do clube | Tela inicial do manager, fora da partida: próxima partida, elenco, finanças. | dashboard, hub | 08-ui-ux |
| Utilitária | Granadas e equipamentos táticos (smoke, flash, molotov...). | utility, nade | 02-mecanicas |
| Timeout | Pausa pedida pelo manager durante a partida (3 por partida) para mudar o estilo do time; única intervenção na partida. | tempo técnico, pausa tática | 01-core-loop |
| Traço | Característica de personalidade de um jogador (ex.: clutch, líder) que muda como ele joga em certas situações. | perk, habilidade especial | 11-ideias |
| Moral | Ânimo do jogador; sobe e desce com resultados e decisões do manager. | humor, motivação | 11-ideias |
| Entrosamento | O quanto dois jogadores do elenco jogam bem juntos; muda a simulação (ex.: trade kill). | química, sinergia | 11-ideias |
| Playbook | Escolhas do manager, por mapa, de planos de round favoritos e proibidos que o IGL respeita. | livro de jogadas | 11-ideias |
| Melhores momentos | Lances-chave de um round ou partida, mostrados em replay na transmissão e no pós-partida. | highlights | 08-ui-ux |
| Rivalidade | Adversário com histórico marcante contra o clube (finais, eliminações, sequências). | clássico | 11-ideias |
| Modo resumo | Forma de assistir só os rounds decisivos de uma partida e pular o resto. | modo rápido | 11-ideias |
