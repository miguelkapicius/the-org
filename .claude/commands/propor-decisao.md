---
description: Cria um registro de decisão em decisoes/
argument-hint: <assunto da decisão>
---
Crie um registro de decisão sobre: $ARGUMENTS

1. Leia `decisoes/` e confira se já existe decisão sobre isso (inclusive `rejeitada`). Se existir, mostre ao usuário antes de continuar.
2. Leia `gdd/00-visao.md` (pilares) e as seções afetadas.
3. Pegue o próximo número livre e copie `decisoes/0000-modelo.md` para `decisoes/NNNN-titulo-curto.md`.
4. Preencha com o usuário: contexto, pelo menos duas opções com prós e contras, a escolha e o porquê ligado aos pilares. Status `proposta`.
5. Se substituir uma decisão antiga, preencha "Substitui" e mude o status da antiga para `substituida-por-NNNN`.
6. Atualize o cabeçalho "Decisões" das seções afetadas.
