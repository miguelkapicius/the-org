# Protótipo CS manager

Protótipo em Unity 6000.6.7f1 do jogo 2D isométrico: mapa original "Vila" e bots simulados. Isto é código de teste, não contrato: o que vale como design continua em `gdd/` e `decisoes/`.

## O que tem aqui

| Caminho | Para quê |
|---|---|
| `Assets/Game/Sim` | Simulação e bots (Brain, Igl, Match, Partida, Leitura, Aparece) |
| `Assets/Game/Map` | Mapa, navmesh e validadores |
| `Assets/Game/View` | Gráficos; a simulação não depende deles, só de constantes de fumaça |
| `Assets/Game/Editor` | Ferramentas de linha de comando (replay, rotas, estatísticas) |
| `Headless` | Roda os mesmos `.cs` da simulação sem o Unity, cerca de 4 rounds/s |
| `Tools` | Scripts da planta do mapa e dos sprites |

## Rodar sem o Unity

Precisa do .NET 9. Rode da raiz deste protótipo:

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

## O que não está aqui

Os sprites dos personagens e os replays ficam em `Logs/` e não foram versionados (cerca de 5 GB). Sem eles a simulação e o Headless funcionam, mas o Play no Unity e o vídeo não têm os bonecos.

Se o mapa ou as posições mudarem, `Assets/Game/Sim/rotas.bin`, `posicoes.txt` e `aparece.bin` precisam ser gerados de novo.

## Estado medido

- **TR vence:** 48-52% em round avulso e 58% em partida inteira (pros: 45,6%).
- **Mira ao ver o inimigo:** mediana de 5,3° (pros: 2,5°).
- **Adaptação:** existe em plano, lado, ritmo, meio, pixel e ponto queimado; não existe em pós-plant, retake, granadas e economia. O ganho contra um time previsível ainda está dentro do ruído.
