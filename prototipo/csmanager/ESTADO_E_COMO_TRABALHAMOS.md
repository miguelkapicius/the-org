# CSManager — onde paramos e como trabalhamos (13/11/2026)

Documento para quem pega o projeto agora (o Miguel e o agente dele) validar o que subiu. Quem escreve é a sessão que cuida dos bots (a simulação). O plano completo da simulação está em `PLANO_SIMULACAO.md`; este arquivo é o resumo operacional.

## 1. O que subiu

- **Código da simulação** (`Assets/Game/Sim/`), com o cérebro novo dos bots em `Assets/Game/Sim/Cerebro/`.
- **Exe 22** em `Build/` (gerado em 12/11, ~13h), com os atalhos `.bat` de teste.
- **Headless** (`Headless/`): o executável .NET que roda rounds sem o Unity e tem os medidores.
- **Ferramentas** (`Tools/`): scripts dos bots, dos pros, da pesquisa, do mapa Brasa e dos sons.
- **O trabalho das outras sessões que estava sem commit** no PC do Gabriel: visual (`Assets/Game/Visual`), áudio (`Assets/Game/Audio`), Brasa (`Tools/brasa`) e sons (`Tools/sons`). O Gabriel pediu para subir tudo. Esse material não foi revisado pela sessão dos bots.

## 2. Como abrir e o que cada atalho faz

O jogo roda por `Build/CSManager.exe`. As mudanças de comportamento dos bots ficam atrás de flags (`-exp a_b_c`), então:

| Atalho | O que liga |
|---|---|
| `CSManager.exe` direto | Nenhuma flag. É o jogo "principal", só com o que já foi aprovado. |
| `CSManager_TESTE.bat` | O pacote de teste antigo (41 flags). É o que o Gabriel joga no dia a dia. |
| `CSManager_CT_NOVO.bat` … `CT_NOVO5.bat` | Cada etapa do cérebro novo, uma em cima da outra (exes 16 a 21). |
| `CSManager_CT_NOVO6.bat` | **O mais recente (exe 22).** Tudo do CT_NOVO5 mais `semDado5Ligado`, `crencaJog5Ligado` e `situ5Ligado`. |

Cada `.bat` tem linhas `rem` explicando as flags dele. Com as flags novas desligadas, o jogo é idêntico ao exe anterior: isso foi conferido rodada contra rodada no Headless.

## 3. Onde paramos nos bots

### O que o cérebro novo já faz (flag entre parênteses)

- **Crença 2** (`crenca2Ligado`): o som é do inimigo que fez o som, call atrasada não apaga informação mais nova, e depois do plant quem não deu notícia é acreditado perto do C4.
- **Macro do CT e retake** (`cerebroNovoCT`, `macro2Ligado`, `retake2Ligado`): IGL do CT em `Cerebro/IglCT.cs`; retake com juntar, estágios e guardar a arma quando a chance pelo placar é baixa.
- **Escolha de caminho** (`caminho2Ligado`): direto ou por uma via, pelo tempo, pelo risco acreditado e pelo gosto do jogador (`Cerebro/Caminho.cs`).
- **Abrir quina e duelo** (`decisao3Ligado`, `duelo3Ligado`): abrir seco, bangar, pedir bang ou esperar o colega; no duelo, ficar, avançar ou largar.
- **Peça única de decisão** (`situ4Ligado`, `Cerebro/Decisao.cs`): `Escolha` lista as jogadas possíveis, dá valor a cada uma, faz a de maior valor e explica as três melhores no pensamento do bot. `Avaliador` é o juiz: chance de ganhar um duelo pela situação acreditada. Usada em: smoke do inimigo no caminho (nove jogadas, incluindo dar a volta) e o que fazer segurando um lugar.
- **Sem dado** (`semDado5Ligado`, `Cerebro/Vontade.cs`): em 65 lugares do `Brain.cs` o "sorteio menor que a chance" virou "o valor da situação passa do ponto fixo deste jogador". As 14 escolhas por peso viraram "maior peso × gosto do jogador ÷ vezes repetidas".
- **Crença por jogador** (`crencaJog5Ligado`): tabela pequena por jogador com o que ele mesmo viu e ouviu; o mapa de calor continua do time. Toda conta de "quantos eu acredito ali" passa por `Brain.Acho`.
- **Colega caiu perto** (`situ5Ligado`): trocar agora, segurar o ângulo ou sair da linha, pelo valor de cada uma.

### Números do exe 22 (pacote do CT_NOVO6)

Medidos no Headless. Número aqui serve para ver se algo quebrou; quem aprova comportamento é o Gabriel jogando.

| Medida | Pacote antigo | Exe 21 | Exe 22 |
|---|---|---|---|
| Defeitos de "jeito humano", total por 100 rounds | 352 | 284 | 285 |
| Travado com ordem de andar | 107 | 83 | 83 |
| Um de cada vez | — | 55 | 70 |
| Abre, toma tiro, volta (por round) | 0,96 | 0,34–0,40 | 0,39 |
| CT vence o round, partidas com economia | 36% | 37% | 40% |
| CT vence depois do plant | — | 12% | 20% |

### O que está ruim ou não foi feito

- **"Um de cada vez" piorou no exe 22** (55 para 70 por 100 rounds). Causa: no sem dado, o jogador com o ponto baixo num assunto faz sempre a jogada rara daquele assunto.
- **O valor ainda é a conta antiga na maior parte dos assuntos.** Só smoke, segurar um lugar e "colega caiu" usam o `Avaliador`. Os outros comparam a chance velha (calibrada como frequência) com o ponto do jogador.
- **O IGL ainda sorteia a tática no começo do round**, de propósito, até existir memória da partida. `semDadoIgl5Ligado` e `semRuido5Ligado` existem e ficam desligadas: aumentaram o travamento.
- **Equilíbrio:** o TR vence perto de 60% dos rounds e o retake está em 20% (alvo 30–35%). Não foi calibrado; a ordem é comportamento antes de número.
- **Ações que o bot não sabe executar:** andar agachado, fake de passo, tiro através de parede, dropar arma no meio do round, escolher onde plantar.
- **Economia da AWP** está quebrada (compram AWP demais, não dropam para o AWPer). Ficou para depois por decisão do Gabriel.
- **Catálogo:** `Tools/pesquisa/SITUACOES_E_OPCOES.md` lista 92 situações com as jogadas de cada uma. Três estão ensinadas pela peça nova.

### Próximo passo

Refazer o valor de cada jogada pelo `Avaliador`, assunto por assunto, começando pelos que causam o "um de cada vez": ajudar no duelo, levei tiro, abrir na bang. Depois granada proativa e decisões do C4.

## 4. Estrutura do projeto

| Caminho | O que é |
|---|---|
| `Assets/Game/Sim/` | A simulação: regras (`Match.cs`), corpo do jogador (`Agent.cs`), crença do time (`Intel.cs`), leitura coletiva (`Coletivo.cs`), caixa-preta (`CaixaPreta.cs`). |
| `Assets/Game/Sim/Brain.cs`, `Igl.cs` | Cérebro velho. Não recebe regra nova, só ganchos para o novo. |
| `Assets/Game/Sim/Cerebro/` | Cérebro novo: `Decisao.cs`, `Vontade.cs`, `Jeito.cs`, `Abertura.cs`, `Duelo3.cs`, `Caminho.cs`, `IglCT.cs`. |
| `Assets/Game/Visual`, `Audio`, `Interface`, `Partida` | Das outras sessões (visual 3D, som, HUD, partida ao vivo). A sessão dos bots não mexe nelas. |
| `Assets/Game/Manager`, `ManagerUI` | Do Miguel (carreira, treino, playbook, timeout). |
| `Headless/` | Medidores sem Unity. |
| `Tools/bots/` | `build_exe.ps1`, `mk_batNN.py` (gera os atalhos), `ab.ps1`. |
| `Tools/pro/` | Scripts que comparam o bot com os pros na mesma régua e leem a caixa-preta. |
| `Tools/pesquisa/` | `SITUACOES_E_OPCOES.md` (catálogo) e `gabriel_falas_10a12nov.txt` (o que o Gabriel pediu, em palavras dele). |
| `PLANO_SIMULACAO.md` | Fonte da verdade da simulação: princípios, camadas, marcos e, no §9.1, as decisões do Gabriel. |

## 5. Como trabalhamos

### Princípios que valem para qualquer mudança nos bots

1. **Crença, não verdade.** O bot decide só com o que viu, ouviu ou recebeu por call.
2. **Avaliação, não sorteio.** O bot faz a melhor jogada pela crença e pelo jeito dele. Nenhuma jogada é proibida por regra; o que muda é o valor dela na situação.
3. **Cada jogador é um jogador.** Jeito fixo por jogador (`Jeito.De`), sem cinco bots decidindo igual no mesmo instante.
4. **Comportamento antes de número.** Uma mudança passa quando o bot faz a coisa inteligente na cena. Equilíbrio de lado e porcentagem de plant são calibragem e vêm depois.
5. **Matar a classe do erro.** Um defeito que o Gabriel aponta é exemplo; conserta-se a causa, não o caso.
6. **Sem regressão.** O que piorou volta ao nível anterior antes de fechar o bloco, ou fica anotado e vai num atalho separado para o Gabriel olhar.

### Ciclo de um bloco de trabalho

1. Ler `PLANO_SIMULACAO.md` e `Tools/pesquisa/gabriel_falas_10a12nov.txt`.
2. Escrever o código novo em `Assets/Game/Sim/Cerebro/`, atrás de uma flag `algoLigado` (`TeamBrain.EXPF.Contains("algoLigado")`).
3. Compilar o Headless e conferir que, com a flag desligada, o resultado é idêntico ao de antes.
4. Medir com a flag ligada: validador de jeito humano, cenas de comportamento, partidas com economia, e ler rounds na caixa-preta.
5. Revisão de código (correção de lógica) e corrigir os achados.
6. Gerar o exe, rodar 150 s dentro do jogo, trocar as pastas e criar o atalho novo.
7. Avisar o Gabriel com o que entrou, o que mediu e o que não está bom.

### Regras práticas

- Os `.cs` de `Assets` são CRLF com BOM. Editar preservando isso (nada de `sed -i`).
- Enquanto existir `Logs/unity_ocupado.txt` há um build do Unity rodando: não gravar `.cs` em `Assets`.
- Comentários e textos em português, no estilo do arquivo vizinho.
- Builds antigos não são apagados: `Build` vira `Build_novoN` (essas pastas ficam só no PC do Gabriel).

## 6. Como validar

Comandos a partir da pasta do projeto. O Headless usa .NET 9.

```bash
dotnet build Headless/Headless.csproj -c Release -o Headless/binval
```

As flags do pacote mais recente estão na última linha de `Build/CSManager_CT_NOVO6.bat`, depois de `-exp`. Nos comandos abaixo, `FLAGS` é esse texto.

| Para ver | Comando |
|---|---|
| Se algo quebrou (métricas gerais) | `dotnet Headless/binval/Headless.dll -n 60 -seed 500 -auditar -exp FLAGS -saida nome` |
| Defeitos de jeito humano, com exemplos | `dotnet Headless/binval/Headless.dll -humano 120 -seed 70000 -reguaRampa -exp FLAGS -saida nome` |
| Cenas de comportamento | `dotnet Headless/binval/Headless.dll -cenas 100 -seed 3000 -exp FLAGS -saida nome` |
| Partidas inteiras com economia | `dotnet Headless/binval/Headless.dll -partidas 6 -seed 500 -caixaPreta -exp FLAGS -saida nome` |

- As saídas caem em `Logs/bots/` (`nome.txt`, `humano_nome.txt`, `cenas_nome.txt`).
- Com `-caixaPreta`, cada round vira um `.tsv` em `Logs/partidas/<data-hora>/` ao lado do executável do dotnet. `python -I Tools/pro/duelo_vicios.py <pasta>` conta duelo parado e "abre, toma tiro, volta"; `python -I Tools/pro/ler_round.py <pasta> <semente>` mostra a linha do tempo de um round com o pensamento de cada bot.
- A mesma semente dá partida diferente no jogo e no Headless. Para investigar algo visto no jogo, usar a caixa-preta da partida jogada (`Logs/partidas/` ao lado do exe).

### O que conferir primeiro

1. O projeto compila no Unity e o Headless compila.
2. `-n 40 -seed 900` sem nenhuma flag roda até o fim.
3. `-humano 120 -seed 70000 -reguaRampa` com as flags do CT_NOVO6 dá total perto de 285, travado perto de 83 e "um de cada vez" perto de 70.
4. Abrir `Build/CSManager_CT_NOVO6.bat` e assistir alguns rounds: o pensamento dos bots mostra as três melhores jogadas com o valor de cada uma nas situações novas ("smoke no caminho: …", "FULANO caiu do SICRANO: …").

## 7. Junção com o trabalho do Miguel

Ver a seção no fim deste arquivo, preenchida depois da junção.
