dois toques suaves |










- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/07-audio.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
A interface não tinha nenhum som. O autor quer **efeitos sonoros autorais** (hover, cliques e outras ações), sem depender de bancos de som de terceiros, e com a **vibe do jogo: imersão, gerenciamento**. A primeira versão eram bipes de sintetizador genéricos; o autor pediu sons mais originais.

## Opções consideradas
1. **Bancos de som gratuitos ou comprados** — rápido, mas com licença para conferir e sem identidade própria.
2. **Sons feitos por um designer de som** — o melhor resultado final, mas ainda não há essa pessoa.
3. **Sons sintetizados em código** — autorais por construção (sem arquivo de terceiros), pequenos, ajustáveis em uma linha, e servem de rascunho até haver um designer.

## Decisão
Opção 3, como **rascunho autoral** da identidade sonora, com a ideia de uma **sala de operações** e uma regra: **tudo leve**. O jogador usa o mouse o tempo todo, então cada som é curto, macio e baixo, sem agudos nem pancadas, e passar o mouse não toca nada (testes iniciais com relés, rádio e sinos cansaram).

## Detalhamento (proposta do agente, a validar)
1. **Motor:** cada som é uma receita de "vozes" (ruído filtrado, notas, timbres metálicos FM) com **eco e reverb de sala** e um pico de volume próprio; o som é gerado na primeira vez que é usado (`SomCatalogo` e `SomSintese`).
2. **A paleta (11 sons):**

   | Som | Quando | Caráter |
   |---|---|---|
   | Hover | passar o mouse | tique de relé, quase só textura |
   | Clique | clique comum | tecla mecânica: estalo, plástico e o corpo batendo no fundo |
   | Principal | botão claro (ação principal) | confirmação firme e discreta: tecla pesada e uma nota curta e redonda subindo |
   | Aba | trocar de tela ou aba | toque curto e macio, um "tum" surdo |
   | Alternar | chips, passos, opções | relé duplo: dois estalos e dois "tum" |
   | Sucesso | concluir algo | duas notas suaves e curtas, neutro |
   | Erro | aviso de erro, proposta recusada | acesso negado: dois pulsos graves arredondados |
   | Dinheiro | contrato assinado | carimbo (pancada surda) e duas moedas metálicas |
   | Aviso | aviso na tela | ping de radar com eco que se repete |
   | Receber | resposta do jogador na negociação | rajada de dados cifrados: três chilreios de rádio |
   | Semana | avançar a semana | tique-taque, sopro grave que cresce e uma queda de grave |
3. **Ambiente:** um zumbido de sala de controle em laço de 8 s (55 Hz, uma ventilação que respira e um murmúrio distante), bem baixo, que toca enquanto o manager está aberto e para quando a partida começa. Fecha o laço sem emenda.
4. **Cada toque varia a altura em até 4 %** e o hover tem intervalo mínimo de 50 ms, para não cansar.
5. **O som se liga e desliga** pelo botão do menu (efeitos e ambiente juntos; a escolha fica guardada). Em modo batch (capturas e testes) não toca. A interface do manager não tem câmera, então o som garante o ouvinte de áudio pelo `Som3D`.
6. **Teste e escuta:** `TestarSonsBatch` confere cada som (sem NaN, pico seguro, duração, sem estalo no começo e no fim, emenda do ambiente) e grava um `.wav` de cada em `Logs/sons`, para ouvir fora do jogo.

## Por quê
- Dá identidade sonora própria, sem risco de licença e sem arquivos pesados.
- Pilar 5: o som confirma a ação e diferencia ação principal, troca de tela, erro e dinheiro sem texto. O ambiente e a reverb dão a sensação de estar numa sala de operações (imersão).

## Consequências
- Sons sintetizados são um rascunho: a versão final pode vir de um designer, mantendo as mesmas ocasiões. O que o autor ouvir e quiser mudar vira ajuste direto na receita.
- Falta a tela de opções com volume (hoje só liga e desliga). Música e sons da partida estão fora desta decisão.
- Números provisórios (volume 0,5, ambiente a 5 % desse volume, variação de 4 %, intervalo do hover).
