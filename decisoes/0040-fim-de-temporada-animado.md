# 0040 — Fim de temporada com resumo animado

- **Status:** proposta
- **Data:** 2026-11-11
- **Autor:** miguelkapicius (pedido: "fim de temporada com animação, resumo e o reset tudo animado") e Claude (detalhamento)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
A virada de temporada já existia nas regras (decisão 0012). Os pontos de ranking são zerados e metade fica de herança, os circuitos são recalculados, o calendário novo é criado, o elenco envelhece, quem chega a 36 anos se aposenta e os mercados se renovam. Mas o manager só via um aviso de canto ("Nova temporada!"). `FALTA-NO-JOGO.md` pedia uma tela de fim de temporada.

## Decisão
Quando a semana vira a temporada, uma **tela cheia e animada** conta o ano e depois mostra o que muda. Tudo é proposta do agente.

1. **O resumo** é montado na virada, antes de o elenco envelhecer, e fica guardado no salvamento. As temporadas passadas podem ser revistas pelo Calendário. Ele traz:
   - a manchete (promovido, rebaixado, campeão ou mantido);
   - o circuito de antes e o de depois, e o ranking final;
   - a campanha: mapas, % de vitórias, séries vencidas e melhor sequência;
   - os campeonatos, com colocação e prêmio;
   - o MVP da temporada (quem mais foi MVP de mapa) e a maior evolução de geral;
   - o caixa, os prêmios e os fãs, do começo ao fim do ano;
   - o elenco, um ano mais velho, com o geral de antes e o de agora e quem se aposenta.
2. **O "reset"** é a lista do que muda para a temporada nova, na ordem da animação:
   - pontos de ranking zerados, com metade de herança;
   - o circuito novo e o patrocínio dele, se o clube mudou de circuito;
   - o aniversário do elenco e as aposentadorias;
   - a evolução dos atributos voltando a contar do zero;
   - a informação dos scouts caindo pela metade;
   - os mercados de jogadores e de psicólogos renovados;
   - o calendário novo.
3. **A animação** vai em etapas, em cerca de 10 segundos:
   1. manchete;
   2. circuito, com o ranking contando até a posição final;
   3. campanha, com os números contando;
   4. campeonatos, entrando um a um;
   5. destaques e dinheiro;
   6. elenco, com a idade virando e a barra do geral andando até o valor novo;
   7. reset, com cada item da lista sendo marcado.

   No fim aparece **Começar a temporada N**. **Pular animação** (ou Esc / espaço) mostra tudo de uma vez; Enter começa.
4. **Se o jogo fechar antes de ver**, a tela abre na próxima vez que a carreira carregar.

## Por quê
- **Pilar 2 (assistir é o prêmio):** a virada do ano é o maior momento da carreira, e merece ser vista, não só lida num aviso.
- **Longo prazo:** promoção, rebaixamento, títulos e o elenco envelhecendo contam a história do clube (decisão 0021).
- **Pilar 5:** a lista do reset explica as regras da virada, que antes eram invisíveis.

## Consequências
- As regras estão em `Assets/Game/Manager/Temporada.cs` e são testadas em `ManagerTest/TesteTemporada.cs`. A carreira guarda como o clube começou cada temporada.
- A tela é `Assets/Game/ManagerUI/GerenteUI.FimDeTemporada.cs`, com o estilo `.ft-*` em `Manager.uss`. Ela compila contra as regras com o compilador do .NET, mas **ainda não foi aberta no Unity**: falta ver o visual e o ritmo da animação.
- Salvamentos antigos abrem sem resumos e começam a contar a temporada de onde abriram.
- Continuam faltando as metas do clube (o que a diretoria espera de cada temporada), que entram nas perguntas em aberto.
