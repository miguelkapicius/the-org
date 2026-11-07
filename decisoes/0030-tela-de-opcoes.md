# 0030 — Tela de opções (som, tamanho da interface e janela)

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/07-audio.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O único ajuste do jogador era o botão de som no menu (liga e desliga). Depois de afinar os sons da interface (decisão 0029), o autor percebeu que o ideal de volume muda de pessoa para pessoa, e a interface (feita para 1600×900) precisa servir a telas pequenas e grandes.

## Opções consideradas
1. **Só o liga e desliga de som** — simples, mas não resolve volume nem tamanho do texto.
2. **Uma tela de opções com chips** (passos fixos) — combina com o resto da interface, é fácil de usar com o mouse e não exige um controle novo.
3. **Deslizadores contínuos** — mais finos, mas pedem um controle novo, estilo próprio e mais cuidado de teste.

## Decisão
Opção 2: uma tela **Opções** no menu (ícone de engrenagem, no lugar do botão de som), com cada escolha valendo **na hora** e guardada no computador (vale para todas as carreiras).

## Detalhamento (proposta do agente, a validar)
1. **Som:** sons da interface ligados ou desligados; **volume dos efeitos** e **zumbido de fundo** à parte, cada um em Mudo, 25, 50, 75 e 100 % (50 % é o padrão; o zumbido nasce com teto bem baixo).
2. **Imagem:** **tamanho da interface** (pequena 90 %, normal, grande 115 %, mudando a resolução de referência da interface), **modo de tela** (janela ou tela cheia) e **tamanho da janela** (1280×720, 1600×900, 1920×1080 e 2560×1440, só os que cabem no monitor).
3. **Geral:** rever o tutorial e restaurar os padrões.
4. Só o conteúdo da tela é refeito a cada escolha, para a tela não piscar. Depois de mudar um volume, toca um clique para o jogador ouvir o resultado.
5. Quem nunca mexeu em modo de tela ou janela mantém o que a Unity já guarda; nada é imposto na primeira abertura.

## Por quê
- Pilar 5: o jogador ajusta o jogo ao seu gosto sem sair dele, com a mesma linguagem visual do resto.
- Acessibilidade básica (texto maior, som baixo ou mudo) sem depender de arquivos de configuração.

## Consequências
- Fora desta decisão: volume de música (ainda não há música), teclas configuráveis, idioma, qualidade gráfica e HUD da partida configurável.
- O teste real de tela cheia e de troca de janela só existe no jogo compilado; no Editor a Unity ignora a troca.
