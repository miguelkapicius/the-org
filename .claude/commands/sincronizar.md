---
description: Atualiza a branch com a main e resume o que mudou no GDD
---
1. Rode `git fetch origin` e veja a branch atual (`git status -sb`).
2. Se houver mudanças não commitadas, avise e pare.
3. Se estiver na `main`, faça `git pull --ff-only`. Se estiver em outra branch, faça `git merge origin/main` (resolva conflitos preservando as decisões `aceita`; em caso de dúvida, pergunte).
4. Resuma o que mudou na `main` desde o último sync (`git log` e `git diff --stat` dos arquivos em `gdd/`, `decisoes/`, `glossario.md`):
   - seções que mudaram de status
   - decisões novas ou alteradas
   - termos novos no glossário
5. Se disponível, liste PRs e issues abertas (`gh pr list`, `gh issue list`).
6. Aponte qualquer conflito entre o que mudou e o trabalho atual desta branch.
