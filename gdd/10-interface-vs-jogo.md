# Interface × jogo: o que a interface promete e o jogo ainda não faz

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0004, 0008, 0011, 0013, 0014, 0016, 0017

_Lista de trabalho. A interface do manager (decisão 0011) já mostra mapas, estratégias, funções, scouts e outras coisas que a partida ainda não tem de verdade. Este arquivo diz o que é real, o que é aproximado e o que só existe na tela, e o que precisa ser feito no jogo para a interface corresponder._

Levantamento feito no código de `prototipo/csmanager` em 2026-11-06. Caminhos de arquivo são relativos a `prototipo/csmanager/Assets/Game/`.

## Como usar (agentes e humanos)

- Antes de mexer em mapas, estratégia, funções, scouts ou partida pulada, leia o item daqui.
- Ao terminar um item, **atualize a linha** (status e o que foi feito) no mesmo PR do código. Item resolvido sai da lista de pendências e fica só a linha ✅.
- Achou outra coisa que a interface mostra e o jogo não faz? **Adicione aqui.**
- Nada daqui é decisão nova: as regras continuam nas decisões e seções citadas. Dúvida de design vai para `perguntas-abertas.md`.

**Legenda:** ✅ funciona de verdade na partida · 🟡 aparece na interface, mas o efeito no jogo é aproximado (um número, não comportamento) ou não foi medido · ❌ só existe na interface

## Resumo: o que mais pesa

1. **Só existe um mapa jogável.** O veto e a afinidade usam 7 mapas, mas toda partida assistida roda no mesmo mapa (seção 1). Fazer um segundo mapa exige primeiro tirar o mapa "fixo no código" (item M1).
2. ~~A estratégia muda menos do que a descrição promete.~~ **Resolvido (item E2, decisão 0017):** os planos de round do banco foram classificados nos 5 estilos e o IGL prefere os do estilo escolhido. Falta medir o ciclo de contra (item E3).
3. **Os adversários não têm jogadores de verdade.** São um número de força copiado nos 5 bonecos; "jogador de destaque" do scout é sorteado (itens J3 e S2).
4. **A partida pulada não é calibrada com a simulação** e nunca dá 13 x 12 nem prorrogação (item P1).
5. ~~Timeout não existe.~~ **Resolvido (item E5, decisão 0018):** 3 timeouts na partida assistida, com chance de funcionar.

## 1. Mapas

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| M1 | Pool de 7 mapas: Forja, Terraço, Estaleiro, Mina, Vila Velha, Observatório, Duna (`Manager/Mundo.cs`, `Mapas.NOMES`) | Só o índice 0 tem 3D (`Mapas.Tem3D`). O mapa jogável é uma classe estática única (`Map/MapData.cs`), sem como escolher outro | ❌ (6 de 7) |
| M2 | Veto escolhe o mapa da partida; a pré-partida e o pós-partida mostram o nome dele | Qualquer mapa vetado roda na mesma geometria. A interface avisa ("ainda sem 3D") | 🟡 |
| M3 | O mapa jogável é "Forja" (índice 0) | No código e na transmissão ele se chama **"Vila"** (`MapData.cs`, `Interface/Menu/Menu3D.cs:258`, que escreve "Vila" fixo). E existe "Vila Velha" no pool como **outro** mapa | ❌ conflito de nome |
| M4 | Afinidade com cada mapa muda a força | Vale de verdade: até ±3 % na habilidade dos bots (`Carreira.MultEfetivo`), mesmo rodando na geometria errada | ✅ (como número) |

### O que um mapa novo exige

Hoje a IA depende de dados feitos **para a Vila**. Cada mapa novo precisa de tudo isto:

| Peça | Onde está hoje | Como foi feita |
|---|---|---|
| Planta, alturas, áreas com nome | `Map/MapData.cs` (estática) | À mão, da planta aprovada |
| Navegação | `Map/NavGrid.cs`, `Map/NavCheck.cs`, `Map/MapCheck.cs` | Gerada da planta |
| Lugares táticos (posições do CT, reunião do TR, lineups de granada, spots de plant e pós-plant) | `Sim/Places.cs`, `Map/Spots.cs` | À mão |
| Banco de estratégias (rotas dos 5 por round) | `Sim/EstrategiasDados.cs` | Convertido de rounds pro de **Mirage** (`Tools/estrategias/converter.py`), com as regiões da Vila. Precisa ter planos de **todos os 5 estilos** (decisão 0017) e regras de rota para nomear as jogadas do playbook (`ROTA_A`/`ROTA_B`, decisão 0022) |
| Áreas da classificação por estilo (lado do TR, disputadas, meio, bombs) | `Sim/Estrategias.cs` (`LADO_TR`, `DISPUTADAS`...) | Medidas nos dados da Vila |
| Força medida de cada estratégia | `Sim/EstrategiasForca.cs` | Medida rodando a simulação sem gráficos |
| Onde cada time costuma estar / de onde o inimigo aparece | `Sim/rotas.bin`, `Sim/aparece.bin`, `Sim/posicoes.txt` | Aprendidos rodando muitos rounds (`Editor/MatchCli.cs`) |
| Pesos da IA | `Sim/pesos.txt` | Treinados nesta geometria; reavaliar em cada mapa novo |
| Visual 3D | `Visual/Cenario3D.cs`, `Visual/MalhaMapa.cs`, `Visual/Fachada3D.cs`, props e natureza | À mão, por mapa |
| Radar da transmissão | `Interface/Hud/Hud3D.cs` (gerado do `MapData`) | Automático, se o `MapData` mudar |

**Ordem de trabalho sugerida:**

1. Tirar o mapa fixo do código: `MapData`, `Places`, `Spots` e os arquivos aprendidos passam a ser **dados de um mapa**, carregados pelo índice do pool. A Vila vira o primeiro desses dados, sem mudar o comportamento.
2. `Carreira.MontarPartida` passa o índice do mapa para a `Sim.Partida`; a cena 3D e o menu mostram o nome certo (resolve M3).
3. Decidir de onde vêm as estratégias de um mapa novo (pergunta aberta abaixo) e montar o processo.
4. Fazer o segundo mapa e ligar `Mapas.Tem3D` a "o mapa tem dados", não a `mapa == 0`.

## 2. Estratégias (estilos de jogo)

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| E1 | 5 presets na preparação da série: Equilibrada, Agressiva, Defensiva, Execuções rápidas, Controle de mapa, cada um com descrição (`Mundo.cs`, `Estilos.Descricao`) | O preset vira pesos da IA (`Estilos.Pesos`) e entra na partida dos dois times (`MontarPartida`) | ✅ chega à IA |
| E2 | "Execuções rápidas: decide o bomb cedo"; "Controle de mapa: só executa tarde" | Cada plano de round do banco tem um estilo, calculado dos dados (`Sim/Estrategias.cs`, `Classificar`), e o IGL dá peso 6 aos planos do estilo do time (`Sim/Igl.cs`, `EscolherDoBanco`). Medido (18 partidas por estilo): os rounds com plano do estilo subiram de 16–37 % para 50–74 % no TR e de 10–38 % para 47–78 % no CT; o plant médio vai de 54 s (Execuções rápidas) a 69 s (Defensiva). Detalhes em `02-mecanicas.md` | ✅ (CT agressivo ainda não aumenta as trocas cedo; ver 02) |
| E3 | Ciclo de contra (Agressiva perde para Defensiva, que perde para Execuções rápidas, que perde para Controle de mapa, que perde para Agressiva); o contra certo dá bônus | O bônus é um número fixo: ±2 % na habilidade (`Provisorio.ESTRATEGIA_CONTRA`). O ciclo foi escrito à mão, não saiu da simulação | 🟡 |
| E4 | Estilo do adversário (revelado pelo scout) | Sorteado na criação do mundo e fixo para sempre; o adversário nunca troca de estilo nem se adapta ao nosso | 🟡 |
| E5 | Timeout na transmissão: botão e tecla T, painel com a chance e os 5 estilos (decisão 0018) | Regra em `Manager/Timeouts.cs`, efeito em `Sim/Partida.cs` (`PedirTimeout`), painel em `Partida/PartidaAoVivo3D.Timeout.cs`. Testado sem a Unity: estilo e foco aplicados certo nos rounds seguintes; 4º timeout recusado. O adversário ainda não pede timeout | ✅ |
| E6 | — (pergunta aberta: compras padrão do time) | A compra é toda decidida pela simulação (`Sim/Partida.cs`) | ❌ (depende de decisão) |

**Para a interface corresponder:**

- ~~Fazer o banco de estratégias respeitar o preset.~~ Feito (decisão 0017). Para medir de novo: `Headless -estilos` (ver `prototipo/csmanager/README.md`).
- Medir se o ciclo de contra acontece na simulação (dá para estender o `Headless -estilos` para B com outros estilos). Se não acontecer, trocar o ciclo pelo que a simulação mostrar, ou ajustar os pesos até acontecer, e só então tirar o bônus fixo.
- Adversário com estilo por mapa ou que muda entre campeonatos (pede regra no GDD).

## 3. Jogadores, funções e adversários

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| J1 | 5 atributos (mira, reflexo, spray, utilitária, sangue-frio) | São leitura direta do `Skill` dos bots (`Manager/GamingHouse.cs`, classe `Jogador`). Treino, preparo, cansaço e afinidade mudam esses valores na partida | ✅ |
| J2 | Uma função por jogador (IGL, Entry, AWPer, Lurker, Suporte) e cobertura de funções | A função decide a vaga do jogador no TR (rota do plano, quem compra a AWP). A **função de CT** (âncora, rotação, qual bomb) é fixa pela vaga e o manager não vê nem escolhe. O jeito de jogar por função (`Skill.Estilo`) está **desligado** por padrão (só com `-exp estilos`) | 🟡 |
| J3 | Adversário com 5 jogadores nomeados e força | Os 5 bonecos do adversário recebem **o mesmo valor** em todos os atributos (força ± 4). As funções dos jogadores dele não entram na vaga | 🟡 |
| J4 | Retrato de cada jogador | Todos usam o mesmo boneco 3D (pergunta de arte) | 🟡 |
| J5 | Brasão dos times | Só a inicial do nome | 🟡 |

**Para a interface corresponder:** jogadores do adversário com atributos próprios (gerados como os do mercado) e escalados por função; decidir se o manager escolhe a função de CT; ligar o estilo por função quando os atributos dos jogadores estiverem estáveis.

## 4. Scouts e informação do adversário

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| S1 | Níveis de informação 1 e 2: estilo e mapas fortes/fracos | Vêm dos dados reais do adversário (`estilo`, `afinidade`) | ✅ |
| S2 | Nível 3: "estratégia detalhada e jogador de destaque" | O jogador de destaque é escolhido por fórmula (`(id + temporada) % 5`, `ManagerUI/GerenteUI.Scouts.cs:109`), não pelo melhor jogador. A "estratégia detalhada" **não é mostrada**: o nível 3 só acrescenta o destaque | ❌ |

**Para a interface corresponder:** depende de J3 (jogadores com atributos) e do item E2 (o que o adversário realmente faz por mapa). Com isso, o destaque é o melhor jogador dele e a estratégia detalhada pode mostrar os planos mais usados em cada mapa.

## 5. Partida pulada e resultados

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| P1 | Chance de vencer o mapa e a série; o resultado pulado usa "os mesmos fatores" da assistida (decisão 0008) | A chance é uma curva sobre a força efetiva (`Mundo.Chance`), **não calibrada** com partidas reais da simulação (sugestão em `09-tecnico.md`). O placar pulado é sempre 13 x 3 a 13 x 11: nunca 13 x 12, nunca prorrogação | 🟡 |
| P2 | Desempenho de cada jogador no pós-partida | Assistida: números da simulação. Pulada: estimados e avisados como tal | ✅ |
| P3 | Partidas dos outros times e campeonatos | Resolvidas por probabilidade, só com a força (sem afinidade nem estilo) | 🟡 (ok para o protótipo) |

**Para a interface corresponder:** rodar muitas partidas da simulação entre times de forças diferentes e ajustar a curva de `Mundo.Chance` e de `Carreira.ChanceMapa`; sortear o placar pulado a partir da distribuição medida.

## 6. Treino, Gaming House e transmissão

| # | O que a interface mostra | O que o jogo faz hoje | Status |
|---|---|---|---|
| T1 | Treino, preparo, cansaço, estudo de mapa, bootcamp, recuperação | Calculados no manager e levados à partida como multiplicador da habilidade | ✅ |
| T2 | Sessões coletivas: "entrosamento do time" | Somam preparo e, desde a decisão 0019, +3 de entrosamento por sessão para os titulares; o entrosamento muda a chance de trade kill dos bots | ✅ |
| T3 | Transmissão: HUD de campeonato, killfeed, economia, radar, replay de melhores momentos, velocidade 1 a 4, pausa, pular round | Existe na cena 3D (`Partida/`, `Interface/Hud/Hud3D.cs`) | ✅ |
| T4 | — | O painel de placar da partida (tecla P) mostra "Vila" fixo, mesmo quando a série diz outro mapa (ver M3) | ❌ |

## Perguntas em aberto

- O mapa jogável de hoje entra no pool com qual nome: "Vila" (como no código) ou "Forja" (como na interface)? E o "Vila Velha" do pool continua?
- De onde vêm as estratégias de um mapa novo? Hoje o banco vem de rounds pro de um mapa da Valve; para mapas próprios (decisão 0004), gerar pela própria simulação, montar à mão ou outro caminho?
- Os jogadores dos times adversários têm atributos próprios (como os do mercado) ou o adversário continua sendo só um número de força?
- O manager escolhe a função de CT de cada titular (âncora, rotação, qual bomb) ou ela sai da função de TR?
