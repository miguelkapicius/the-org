#!/usr/bin/env bash
# Roda ao abrir o Claude Code. Tudo que for impresso entra no contexto do agente.
cd "${CLAUDE_PROJECT_DIR:-.}" || exit 0
git rev-parse --is-inside-work-tree >/dev/null 2>&1 || exit 0

git fetch origin --quiet 2>/dev/null
branch=$(git rev-parse --abbrev-ref HEAD)
echo "## Estado do repositório (GDD)"
echo "Branch atual: $branch"

if [ -n "$(git status --porcelain)" ]; then
  echo "AVISO: há mudanças não commitadas."
elif [ "$branch" = "main" ]; then
  git pull --ff-only --quiet 2>/dev/null && echo "main atualizada."
fi

behind=$(git rev-list --count HEAD..origin/main 2>/dev/null || echo 0)
[ "$behind" != "0" ] && echo "Esta branch está $behind commit(s) atrás da origin/main. Sugira /sincronizar."

echo
echo "Últimas mudanças na main:"
git log origin/main -n 5 --pretty='- %h %an: %s' 2>/dev/null

propostas=$(grep -l 'Status:\*\* proposta' decisoes/[0-9]*.md 2>/dev/null | grep -v 0000-modelo)
if [ -n "$propostas" ]; then
  echo; echo "Decisões em discussão:"; echo "$propostas" | sed 's/^/- /'
fi

if command -v gh >/dev/null 2>&1; then
  prs=$(gh pr list --limit 10 2>/dev/null)
  [ -n "$prs" ] && { echo; echo "PRs abertos:"; echo "$prs"; }
fi
exit 0
