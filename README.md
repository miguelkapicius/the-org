# the-org — GDD

Game Design Document do jogo. Este repositório é o **contrato** do que o jogo vai ser: o que está na branch `main` foi aprovado pelos dois autores.

## Estrutura

| Caminho | Para quê |
|---|---|
| `CLAUDE.md` | Regras que os dois Claude Code seguem (lido automaticamente) |
| `gdd/` | O GDD, uma seção por arquivo |
| `decisoes/` | Registro de decisões: o que foi decidido e **por quê** |
| `glossario.md` | Termos oficiais do jogo |
| `perguntas-abertas.md` | Tudo que ainda falta decidir, por prioridade |
| `.claude/commands/` | Comandos compartilhados (`/sincronizar`, `/propor-decisao`, ...) |
| `.claude/hooks/` | Hook que sincroniza o repo ao abrir uma sessão |

## Fluxo de trabalho

1. **Pegue uma tarefa** nas Issues (ou crie uma) e atribua a você.
2. **Abra o Claude Code** no repo. O hook faz `git fetch` e mostra o que mudou.
3. **Crie uma branch**: `seu-nome/assunto`.
4. **Trabalhe com o Claude**: ele edita `gdd/`, registra decisões e termos.
5. **`/fechar-sessao`**: o Claude consolida, commita e dá push.
6. **Abra um PR**. O outro autor revisa (pode usar `/revisar-pr <n>`).
7. **Merge na `main`** = virou contrato.

## Status das seções

`rascunho` → `em-revisao` → `aprovado`

Só vira `aprovado` com a revisão dos dois. Mudou algo aprovado? Volta para `em-revisao`.

## Configuração recomendada no GitHub

- Proteger a `main`: exigir PR com 1 aprovação antes do merge (Settings → Branches).
- Labels por seção: `visao`, `core-loop`, `mecanicas`, `narrativa`, `arte`, `audio`, `ui-ux`, `progressao`, `tecnico`.
