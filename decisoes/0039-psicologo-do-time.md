# 0039 — Psicólogo do time: contrato, proteção da moral, relatório e conversa individual

- **Status:** proposta
- **Data:** 2026-11-11
- **Autor:** miguelkapicius (ideia e pedido de implementar) e Claude (detalhamento e números)
- **Seções afetadas:** gdd/11-ideias.md (ideia 6), gdd/02-mecanicas.md (moral), gdd/03-progressao.md (Gaming House), gdd/08-ui-ux.md (tela nova)
- **Substitui:** —

## Contexto
A ideia 6 do autor (`gdd/11-ideias.md`) pede um psicólogo contratado, com salários e qualidades diferentes, que cuide do mental do time e entregue um relatório de cada jogador. Seis perguntas estavam abertas: prazo do relatório, emoções, ações do manager, relação com a Gaming House, tamanho do efeito e custo, e contrato. O autor pediu para implementar.

## Opções consideradas
1. **Psicólogo só no nível 4 da Gaming House** — simples, mas tira do começo da carreira justamente a escolha "gastar no mental ou em jogador".
2. **Psicólogo contratável desde o início, com o nível 4 melhorando o trabalho** — a escolha existe desde a primeira temporada e a Gaming House continua valendo.

## Decisão
Opção 2. Detalhamento (proposta do agente, números provisórios em `Psicologos` de `Assets/Game/Manager/Psicologo.cs`):

1. **Mercado de psicólogos:** 4 opções por vez, com qualidades diferentes (1 a 5 estrelas), renovadas a cada temporada. Nomes inventados.
2. **Custo:** salário semanal pela qualidade: $ 400, 800, 1.400, 2.200 e 3.200 (±10 %). Entra nas despesas da semana (decisão 0015). Para contratar, o caixa precisa pagar um mês de salário.
3. **Contrato:** 1 ou 2 temporadas. Renovar só nas últimas 13 semanas, com 5 % de aumento. Demitir antes paga metade dos salários que faltam, como nos jogadores (decisão 0035). Um psicólogo por vez.
4. **O que a qualidade muda:**
   - **Proteção:** toda queda de moral (derrota, banco, promessa não cumprida) fica 8 % menor por estrela (5 estrelas = 40 % menor).
   - **Recuperação:** quem está abaixo da moral neutra (60) sobe 0,2 por estrela por semana, só até a neutra. É menos que a queda do banco, para o banco continuar pesando.
   - **Conversa individual:** 1 por semana (2 com a sala do psicólogo, Gaming House nível 4). Dá certo em 40 % + 10 % por estrela das vezes e sobe a moral em 4 + estrelas. Não vale para quem está com moral 80 ou mais.
   - **Relatório:** semanal, de todo o elenco, do mais abalado para o mais confiante. Sempre diz a emoção e o efeito da moral na partida, em números. Com 2 estrelas ou mais, diz a causa. Com 4 ou mais, prevê o que vem.
5. **Emoções** (pela moral): Confiante (75+), Tranquilo (55+), Ansioso (42+), Frustrado (30+) e Desmotivado (abaixo de 30).
6. **Causas** que o relatório reconhece: promessa de vaga não cumprida, banco (a Estrela reclama mais), derrotas seguidas, clássico perdido (decisão 0021), contrato no fim, MVP recente (decisão 0020), vitórias seguidas e pouco entrosamento.
7. **Previsões:** quantas semanas até o reserva ficar desmotivado, risco de recusar renovação, e quando uma conversa pode resolver.
8. **Gaming House nível 4:** o fisioterapeuta segue dando +30 % de recuperação de cansaço; a sala do psicólogo passa a dar +1 conversa por semana.
9. **Sem psicólogo:** a moral anda como na decisão 0019; não há relatório nem conversa. Um aviso no painel sugere contratar quando alguém fica desmotivado.

## Por quê
- **Pilar 3 (gestão que aparece na partida):** a moral já muda a habilidade na partida (decisão 0019). O psicólogo mexe nesse número, e o relatório mostra o efeito em porcentagem.
- **Pilar 5 (complexo por dentro, simples de entender):** o relatório explica em palavras por que um jogador rende menos.
- **Escolha de gestão:** o salário pesa no caixa de um clube pequeno, então é escolher entre o mental e um jogador melhor.

## Consequências
- Ficou de fora desta versão e entrou nas perguntas em aberto:
  - o efeito de um jogador abalado (ou de um Líder confiante) no resto do time;
  - as outras ações a partir do relatório (folga, prometer vaga);
  - outros profissionais no mesmo modelo.
- As regras estão em `Manager/Psicologo.cs` e são testadas em `ManagerTest/TestePsicologo.cs`.
- A tela é `ManagerUI/GerenteUI.Psicologo.cs`. Foi checada contra as regras com o compilador do .NET, mas **ainda não foi aberta no Unity**.
- O salvamento guarda o psicólogo, o mercado de psicólogos e as conversas da semana. Salvamentos antigos abrem sem psicólogo.
