# Protótipo CS manager

Protótipo em Unity 6000.6.7f1 (URP): mapa original "Vila" em maquete 3D, bots simulados e transmissão com som. Isto é código de teste, não contrato: o que vale como design continua em `gdd/` e `decisoes/`.

## O que tem aqui

| Caminho | Para quê |
|---|---|
| `Assets/Game/Sim` | Simulação e bots (Brain, Igl, Match, Partida, Leitura, Aparece) |
| `Assets/Game/Map` | Mapa, navmesh e validadores |
| `Assets/Game/View` | Sobrou só o que a simulação usa do visual antigo 2D (constantes de fumaça e projeção) |
| `Assets/Game/Partida` | A partida ao vivo (`PartidaAoVivo3D`, dividido em arquivos por assunto), transmissão e medição de desempenho |
| `Assets/Game/Visual` | Visual 3D de maquete (estilo Link's Awakening): cenário, bonecos, armas, efeitos, shaders e render |
| `Assets/Game/Interface` | HUD da transmissão (`Hud`) e menus (`Menu`) |
| `Assets/Game/Audio` | Som da partida (`Som3D.cs`) e os sons em `Resources/Sons`: tiros, granadas, C4, passos, narrador, torcida |
| `Assets/Game/Art` | Modelos de packs CC0 (Kenney, Quaternius) e granadas autorais; cada pasta guarda a licença ou a origem |
| `Assets/Game/Manager` | Regras de gestão do clube (elenco, mercado, finanças, Gaming House). Sem Unity nem UI; valores provisórios em `Provisorio` |
| `Assets/Game/ManagerUI` | Interface do manager em UI Toolkit, montada em código (decisão 0011) |
| `Assets/Game/Editor` | Ferramentas de linha de comando (replay, rotas, estatísticas) |
| `Headless` | Roda os mesmos `.cs` da simulação sem o Unity, cerca de 4 rounds/s |
| `Tools` | Scripts da planta do mapa, dos bonecos, dos ícones e dos sons (`Tools/sons`; a chave da API e os sons crus não vêm junto) |

## Rodar sem o Unity

Precisa do .NET 9 (com o SDK do .NET 10, compile com `-p:LangVersion=13` e rode com `DOTNET_ROLL_FORWARD=Major`). Rode da raiz deste protótipo:

```bash
dotnet build Headless -c Release
```

```bash
dotnet Headless/bin/Release/net9.0/Headless.dll -n 900 -seed 900 -auditar -saida teste
```

O relatório sai em `Logs/bots/teste.txt`. Para partidas inteiras:

```bash
dotnet Headless/bin/Release/net9.0/Headless.dll -partidas 100 -seed 100 -modoB semAdaptar -saida adapta
```

Para medir o efeito da estratégia do manager (decisão 0017: cada estilo, antes e depois de preferir os planos do estilo):

```bash
dotnet Headless/bin/Release/net9.0/Headless.dll -estilos 18 -seed 500
```

O relatório sai em `Logs/bots/estilos.txt` (cerca de 15 minutos com 18 partidas por estilo).

## Interface do manager

Menu **CS Manager ▸ Manager ▸ Rodar o manager** (cria a cena `Assets/Scenes/Manager.unity` se não existir, deixa ela e a
`Partida3D` no Build Settings e entra no Play). Dá para contratar e vender, reordenar os titulares, subir a Gaming House e
conferir as finanças. A **Pré-partida** (tela com os dois times) leva a **Assistir partida**, que abre a transmissão 3D com o seu elenco (nome e habilidade de cada titular entram
na simulação); no fim, **Voltar ao manager** paga o prêmio e os salários. **Pular partida** calcula o resultado por
probabilidade. A carreira vive em memória: ainda não há salvamento.

Sem Unity: `Assets/Game/Manager/Carreira.cs` só depende de `Sim`, então roda no Headless.

Todos os números (salários, preços, prêmios, custo da Gaming House) são provisórios: o GDD ainda não decidiu esse lado
(`perguntas-abertas.md`, seção 2).

## Jogar sem abrir a Unity

`Build/CSManager.exe` é a build de Windows mais recente (06/11/2026, 14:15): é só abrir. A pasta `Build` inteira precisa ir junto,
porque o jogo lê os dados de `Build/CSManager_Data`. Ela não tem a integração com o manager; essa só existe no
projeto.

## Ver uma partida em 3D no Unity

Abra `Assets/Scenes/Partida3D.unity` (já é a primeira cena do Build Settings) e aperte Play. Atalhos: espaço pausa,
1 a 4 mudam a velocidade, N pula o round, P abre o painel, a rodinha do mouse dá zoom e clicar num card do HUD segue
o jogador. Na carga aparece "Aquecendo a simulação..." por uns 5 s: a Sim roda um round descartável pra o 1º round
real não engasgar (no Mono, o 1º passo caía de ~165 para ~28 ms); a partida sai igual com ou sem esse aquecimento.

Os bonecos 3D (`Logs/bonecos3d/boneco_ct|tr.json/.bin`, ~175 MB) vão junto no repositório, então o Play funciona
logo depois de clonar. Depois de mexer no `Tools/sprites/agentes.py`, gere de novo com o Blender 5.2 a partir da raiz
deste protótipo:

```bash
blender -b --factory-startup --python Tools/sprites/exportar3d.py -- Logs/bonecos3d ct
```

```bash
blender -b --factory-startup --python Tools/sprites/exportar3d.py -- Logs/bonecos3d tr
```

Para conferir as animações depois de mexer nelas: `python Tools/sprites/validar_anims.py Logs/bonecos3d ambos` (precisa
de numpy) aponta braço atravessando o corpo, mão fora da arma, saltos e o que mais; `Tools/sprites/olhar_pose.py` monta
uma folha com a pose de frente, de lado, na câmera do jogo e de costas.

## O que não está aqui

Os replays, vídeos e relatórios ficam em `Logs/` e não foram versionados; o Play não precisa deles. `Library` e `Temp` a
Unity gera sozinha na primeira abertura (a primeira importação demora alguns minutos).

Se o mapa ou as posições mudarem, `Assets/Game/Sim/rotas.bin`, `posicoes.txt` e `aparece.bin` precisam ser gerados de novo.

## Estado medido

- **TR vence:** 48-52% em round avulso e 58% em partida inteira (pros: 45,6%).
- **Mira ao ver o inimigo:** mediana de 5,3° (pros: 2,5°).
- **Adaptação:** existe em plano, lado, ritmo, meio, pixel e ponto queimado; não existe em pós-plant, retake, granadas e economia. O ganho contra um time previsível ainda está dentro do ruído.
