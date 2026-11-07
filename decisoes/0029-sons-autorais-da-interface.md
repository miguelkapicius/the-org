# 0029 — Sons autorais da interface (sintetizados em código)

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/07-audio.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
A interface não tinha nenhum som. O autor quer **efeitos sonoros autorais** para ações como passar o mouse, clicar e confirmar, sem depender de bancos de som de terceiros.

## Opções consideradas
1. **Bancos de som gratuitos ou comprados** — rápido, mas com licença para conferir e sem identidade própria.
2. **Sons gravados ou feitos por um designer de som** — o melhor resultado final, mas ainda não há essa pessoa.
3. **Sons sintetizados em código** — autorais por construção (sem arquivo de terceiros), pequenos, ajustáveis em uma linha, e servem de rascunho até haver um designer.

## Decisão
Opção 3, como **rascunho autoral** da paleta sonora.

## Detalhamento (proposta do agente, a validar)
1. **Cada som é uma receita curta** de "vozes" (nota ou sopro, com início, duração, altura inicial e final, ataque e queda) em `SomCatalogo`. O som é gerado na primeira vez que é usado.
2. **A paleta (11 sons)**, no caráter "mesa de transmissão": limpa, seca e tátil.

   | Som | Quando | Caráter |
   |---|---|---|
   | Hover | passar o mouse | tique agudo de 35 ms, quase só textura |
   | Clique | clique comum | estalo e um "toc" que desce |
   | Principal | botão claro (ação principal) | duas notas subindo (E5 e B5) com brilho |
   | Aba | trocar de tela ou aba | sopro que sobe e um tique |
   | Alternar | chips, passos, opções | "pop" curto de triângulo |
   | Sucesso | concluir algo | arpejo maior C5 E5 G5 |
   | Erro | aviso de erro, proposta recusada | duas notas graves descendo, arredondadas |
   | Dinheiro | contrato assinado | dois tinidos agudos e longos |
   | Aviso | aviso na tela | duas notas suaves |
   | Receber | resposta do jogador na negociação | duas notinhas, como uma bolha |
   | Semana | avançar a semana | sopro grave e nota que cai |
3. **Cada toque varia a altura em até 4 %** e o hover tem intervalo mínimo de 50 ms, para não cansar nem metralhar.
4. **O som se liga e desliga** pelo botão do menu (a escolha fica guardada). Em modo batch (capturas e testes) não toca.
5. **Teste e escuta:** `TestarSonsBatch` confere cada som (sem NaN, pico seguro, até 1 s, sem estalo no começo e no fim) e grava um `.wav` de cada em `Logs/sons`, para ouvir fora do jogo.

## Por quê
- Dá identidade sonora própria sem risco de licença e sem arquivos pesados.
- Pilar 5: o som confirma a ação e diferencia ação principal, troca de tela e erro sem texto.

## Consequências
- Sons de sintetizador são um rascunho: a versão final pode vir de um designer de som, mantendo as mesmas ocasiões.
- Falta a tela de opções com volume (hoje só liga e desliga). Música e sons da partida estão fora desta decisão.
- Números provisórios (volume 0,6, variação de 4 %, intervalo do hover).
