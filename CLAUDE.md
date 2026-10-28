# CLAUDE.md — Regras do projeto

Este repositório é o **GDD (Game Design Document)** de um jogo escrito por duas pessoas, cada uma com seu próprio Claude Code. O repositório é a **única memória compartilhada** entre os agentes. Nada que foi dito em um chat existe para o outro agente até estar escrito aqui.

> **Regra de ouro:** se não está no repositório, não existe.

## Antes de qualquer trabalho

1. Leia `gdd/00-visao.md` (pilares do jogo — tudo deve respeitá-los).
2. Leia `glossario.md` e use **somente** os termos definidos lá.
3. Liste `decisoes/` e leia as decisões com status `aceita` que tocam no assunto da tarefa.
4. Verifique se há PRs ou issues abertas sobre a mesma seção (`gh pr list`, `gh issue list`, se disponível). Se houver, avise o usuário antes de editar a mesma seção.

## Hierarquia de verdade

1. Decisões `aceita` em `decisoes/` (mais forte)
2. Seções do GDD com status `aprovado`
3. Seções com status `em-revisao`
4. Seções com status `rascunho` (mais fraco — podem mudar livremente)

Nunca contradiga um nível mais forte. Se a tarefa exigir isso, **pare e proponha uma nova decisão** (`/propor-decisao`) que substitua a antiga, em vez de editar silenciosamente.

## Como editar

- Trabalhe sempre em uma branch: `<pessoa>/<assunto>` (ex.: `miguel/sistema-combate`). Nunca commite direto na `main`.
- Uma seção do GDD por arquivo em `gdd/`. Mantenha o cabeçalho de metadados (status, responsável, decisões relacionadas) atualizado.
- Ao mudar uma seção `aprovado`, volte o status para `em-revisao`.
- Só o humano revisor muda uma seção para `aprovado` (no merge do PR).
- Termo novo? Adicione ao `glossario.md` no mesmo PR.
- Escolha relevante (afeta outras seções, difícil de desfazer, ou já foi debatida)? Registre em `decisoes/`.
- Dúvida sem resposta? Não invente: registre em "Perguntas em aberto" da seção ou abra uma issue.
- Commits pequenos, mensagem em português, dizendo **o quê** e **por quê**.

## Ao terminar uma sessão

Rode `/fechar-sessao`: garante que tudo que foi decidido no chat foi escrito nos arquivos, commitado e enviado, e que o PR descreve a mudança para o outro revisor.

## Comandos disponíveis

- `/sincronizar` — atualiza a branch e resume o que mudou desde a última vez
- `/propor-decisao <assunto>` — cria um registro de decisão
- `/revisar-pr <número>` — revisa um PR do parceiro contra o GDD e as decisões
- `/fechar-sessao` — consolida o trabalho da sessão no repositório
