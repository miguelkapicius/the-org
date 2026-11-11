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
| Domínio de lado | Número de 0 a 100 (50 é neutro) de cada jogador para o TR e para o CT. A diferença entre os dois muda a habilidade dele no lado em que joga o round; o treino pode focar em um lado. | afinidade de lado, rating de lado | 02-mecanicas |
| Luvas | Pagamento único ao jogador na assinatura do contrato, tirado do caixa do clube; negociado junto com o salário. | bônus de assinatura, preço (usar só para o valor de mercado) | 03-progressao |
| Negociação | Conversa do manager com um jogador do mercado para fechar luvas e salário; o jogador decide pelo conjunto (prestígio, vaga, estrutura, finanças, clima) e responde aceitando, contrapropondo ou recusando. | leilão, compra | 03-progressao |
| Empréstimo | Dinheiro que o banco adianta ao clube, pago em parcelas semanais com juros. | dívida (usar só para o saldo negativo) | 03-progressao |
| Pré-partida | Tela antes da partida: os dois times frente a frente e a escolha entre assistir e pular. | lobby, prematch | 08-ui-ux |
| Pós-partida | Tela depois da partida: resultado, dinheiro e desempenho de cada jogador. | resumo final | 08-ui-ux |
| Painel do clube | Tela inicial do manager, fora da partida: próxima partida, elenco, finanças. | dashboard, hub | 08-ui-ux |
| Utilitária | Granadas e equipamentos táticos (smoke, flash, molotov...). | utility, nade | 02-mecanicas |
| Cápsula | Imagem do jogo na loja e na biblioteca da Steam (principal, pequena, vertical e cabeçalho). Leva só arte, nome e subtítulo. | capa, thumbnail, banner (banner é só o de redes) | marketing/identidade |
| Painel da Steam | Faixa de imagem com título que abre cada seção da descrição na página da Steam. | banner, cabeçalho de seção | marketing/identidade |
| Símbolo | A forma reduzida da logo: o O de ORG sozinho, um anel com o ponto laranja aparecendo por dentro. No ícone do app vai sobre um campo de cor chapada (decisão 0038). | mascote, escudo, brasão (brasão é o dos times) | marketing/identidade |
| Padrão | O O da logo repetido em malha, cada um com o ponto virado para um lado. Quase todos apagados, alguns acesos em laranja (TR) e em azul (CT). Fundo para onde não há captura do jogo. | textura, estampa | marketing/identidade |
| Captura | Imagem tirada do jogo rodando, usada no fundo dos banners e das cápsulas e na galeria da página da Steam. | print, screenshot | marketing/identidade |
| Timeout | Pausa pedida pelo manager durante a partida (3 por partida) para mudar o estilo do time; única intervenção na partida. | tempo técnico, pausa tática | 01-core-loop |
| Traço | Característica de personalidade de um jogador (ex.: clutch, líder) que muda como ele joga em certas situações. | perk, habilidade especial | 11-ideias |
| Moral | Ânimo do jogador; sobe e desce com resultados e decisões do manager. | humor, motivação | 11-ideias |
| Entrosamento | O quanto dois jogadores do elenco jogam bem juntos; muda a simulação (ex.: trade kill). | química, sinergia | 11-ideias |
| Playbook | Escolhas do manager, por mapa e por lado, de jogadas favoritas e proibidas que o IGL respeita. | livro de jogadas | 02-mecanicas |
| Jogada | Grupo de planos de round parecidos, com nome legível ("Rush A pela Rampa", "Setup 2-1-2"); é o que o manager marca no playbook. | tática, play | 02-mecanicas |
| Melhores momentos | Lances-chave de um round ou partida, mostrados em replay na transmissão e no pós-partida. | highlights | 08-ui-ux |
| Rivalidade | Histórico marcante contra um adversário, medido em pontos por série (finais, eliminações, sequências). Os 3 de mais pontos (com 10 ou mais) são os rivais do clube. | — | 01-core-loop |
| Arquirrival | O rival com mais pontos de rivalidade. | — | 01-core-loop |
| Clássico | Série contra um rival: a moral muda o dobro e vencer rende bônus de patrocínio. | derby | 01-core-loop |
| Retrospecto | Séries e mapas vencidos e perdidos contra um adversário, a sequência atual e a última eliminação. | head-to-head, H2H | 01-core-loop |
| Modo resumo | Forma de assistir só os rounds decisivos de uma partida e pular o resto. | modo rápido | 11-ideias |
| Jogada da partida | O lance de maior nota de uma partida assistida (abates de um jogador, com destaque para clutch, multi-kill, AWP, na cabeça...). | jogada do jogo, play of the game | 08-ui-ux |
| MVP | O jogador do clube com a melhor nota de desempenho num mapa (MVP do mapa) ou na soma dos mapas de uma série (MVP da série). | craque do jogo, melhor em campo | 08-ui-ux |
| Contrato | Acordo de 1 a 3 temporadas entre o clube e o jogador, com salário e cláusula de rescisão. Vence sozinho no fim; antes disso só acaba por venda, rescisão ou cláusula paga por um rival. | vínculo, passe | 03-progressao |
| Cláusula de rescisão | Valor que outro clube paga para levar o jogador no meio do contrato; fixado na assinatura e pago ao nosso caixa. | multa (multa é a que o nosso clube paga) | 03-progressao |
| Multa de rescisão | O que o nosso clube paga para dispensar o jogador antes do fim do contrato: metade dos salários que faltam. | cláusula | 03-progressao |
| Renovação | Novo contrato com um jogador do elenco que o jogador pode recusar (moral baixa ou pouco desejo de ficar). Pede salário e 30 % das luvas. | prorrogação | 03-progressao |
| Fãs | Base de torcida do clube, uma moeda de progressão: sobe com vitórias, séries e campeonatos e cai com derrotas. Não se gasta; libera os níveis da Gaming House junto com o dinheiro. | seguidores, torcida, reputação | 03-progressao |
| Relatório do psicólogo | Relatório semanal de cada jogador: a emoção, a causa e o efeito da moral na partida; o psicólogo melhor diz mais e prevê o que vem (decisão 0039). | laudo, avaliação | 11-ideias |
| Central de notificações | Aba da tela principal que resume o que o manager precisa resolver e o que aconteceu de importante; cada notificação leva à tela certa (ideia 7, a definir). | inbox, caixa de entrada | 11-ideias |
| Psicólogo | Profissional que o clube contrata do mercado de psicólogos, com salário, qualidade (1 a 5 estrelas) e contrato; amortece as quedas de moral, faz conversas individuais e escreve o relatório de cada jogador (decisão 0039). | coach mental, terapeuta | 11-ideias |
| Conversa individual | Ação do psicólogo com um jogador, limitada por semana; se der certo, a moral dele sobe (decisão 0039). | sessão, terapia | 11-ideias |
| Emoção | O estado do jogador no relatório do psicólogo, pela moral: Confiante, Tranquilo, Ansioso, Frustrado ou Desmotivado (decisão 0039). | humor, sentimento | 11-ideias |
| Macro | O jogo do time: tática do round, execução, rotação e retake (decidido pelo IGL). | — | 12-ia-micro |
| Micro | O jogo de cada bot: rota, ângulo que confere, onde mira, peek e reação a uma ameaça. | — | 12-ia-micro |
