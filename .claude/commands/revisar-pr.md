---
description: Revisa um PR do parceiro contra o GDD, as decisões e o glossário
argument-hint: <número do PR>
---
Revise o PR #$ARGUMENTS.

1. Obtenha o diff (`gh pr diff $ARGUMENTS`, ou `git fetch origin` + `git diff origin/main...origin/<branch>`).
2. Leia `gdd/00-visao.md`, `glossario.md` e as decisões `aceita` relacionadas.
3. Verifique e liste:
   - **Contradições** com decisões aceitas ou seções aprovadas (bloqueia)
   - **Pilares**: algo vai contra a visão? (bloqueia)
   - **Termos** fora do glossário ou termos novos não adicionados
   - **Impacto** em outras seções que o PR não atualizou
   - **Lacunas**: escolhas relevantes sem registro em `decisoes/`
   - **Perguntas** que o autor deveria responder
4. Dê um parecer: aprovar, aprovar com ajustes, ou pedir mudanças. Não aprove nem faça merge sozinho: a decisão final é do humano.
