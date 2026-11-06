# 0018 — Timeout do manager na partida

- **Status:** proposta
- **Data:** 2026-11-06
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/01-core-loop.md, gdd/08-ui-ux.md, gdd/10-interface-vs-jogo.md, gdd/11-ideias.md
- **Substitui:** — (detalha o timeout já descrito em gdd/01-core-loop.md e na decisão 0001)

## Contexto
O GDD já diz que o manager pode pedir **3 timeouts por partida**, que o time tem uma **chance** de melhorar e mudar o estilo, e que a chance depende de atributos, nível da Gaming House e momento da partida. Nada disso existia no jogo (item E5 de `gdd/10-interface-vs-jogo.md`). É a prioridade 1 de `gdd/11-ideias.md`.

## Decisão (palavras do autor)
Já registradas no GDD: 3 timeouts por partida; chance de melhorar e mudar a forma de jogar, sem garantia; o manager escolhe o estilo; é a única intervenção durante a partida.

## Detalhamento (proposta do agente, a validar)
1. **Estilos do timeout:** os mesmos 5 da estratégia (decisão 0014): Equilibrada, Agressiva, Defensiva, Execuções rápidas, Controle de mapa. Escolher o estilo atual vale como "acalmar o time".
2. **Quando vale:** o timeout pode ser pedido a qualquer momento da partida assistida; a transmissão pausa enquanto o manager escolhe, e o efeito começa no **próximo round** (como no CS, o timeout acontece no tempo de compra).
3. **Chance de funcionar** (números provisórios, entre 20 % e 90 %):
   - base 50 %;
   - **sangue-frio** médio dos titulares: de -15 % (0) a +15 % (100);
   - **Gaming House:** +5 % por nível acima do 1 (até +20 %);
   - **momento:** +4 % por round perdido em sequência (até +12 %); -10 % se o time já vence por 4 ou mais (o time no embalo não quer mudar).
4. **Se funciona:** o time passa a jogar no estilo escolhido (pesos da IA e preferência dos planos de round, decisão 0017) e ganha **foco**: +10 % de sangue-frio nos próximos 3 rounds.
5. **Se não funciona:** o timeout é gasto e o time segue como estava.
6. A tela mostra a chance **antes** de confirmar (princípio de experiência 1 de `gdd/08-ui-ux.md`).
7. O adversário (IA) ainda não pede timeout; fica para depois.

## Por quê
Pilar 2 (assistir é o prêmio) ganha um momento de decisão dentro da partida, sem virar um FPS (decisão 0001). Pilar 3: o efeito aparece no jogo dos bots no round seguinte.

## Consequências
- Partida pulada não tem timeout (o resultado é por probabilidade, decisão 0008).
- Os pesos da chance e o tamanho do foco ficam para o balanceamento (pergunta já aberta em `perguntas-abertas.md`).
- Responde a pergunta "quais estilos além de agressivo e defensivo": os 5 da estratégia.
