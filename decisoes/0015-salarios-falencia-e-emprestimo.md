# 0015 — Salários, empréstimo e falência com avisos

- **Status:** proposta
- **Data:** 2026-11-04
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
O GDD diz que o dinheiro acabando é falência e game over, mas deixou em aberto se há avisos antes e se existe empréstimo ou outra saída.

## Decisão (palavras do autor)
- Os jogadores têm **salários**.
- Há **falência** e **empréstimo**.

## Detalhamento (proposta do agente, a validar)
1. **Salários semanais** de cada jogador (dependem do geral). Receitas: prêmios de evento, **patrocínio semanal** (cresce com o circuito, o ranking e o nível da Gaming House) e venda de jogadores.
2. **Avisos antes da falência:** o painel avisa quando o saldo paga poucas semanas de salário e quando fica negativo.
3. **Empréstimo bancário:** o clube pega um valor até um **limite** (cresce com o circuito) e paga em parcelas semanais com juros; dá para quitar antes. Parcelas entram nas despesas.
4. **Falência:** saldo **negativo** abre um prazo de **4 semanas** para se recuperar (vender jogador, empréstimo, ganhar prêmio). Passado o prazo, o clube **vai à falência** e é game over.
5. **Manutenção da Gaming House:** cada nível custa uma manutenção semanal (responde a pergunta em aberto sobre custo mensal).

## Por quê
O empréstimo e o prazo dão uma saída arriscada e justa antes do game over, em vez de uma falência súbita.

## Consequências
- Todos os valores (juros, limite, prazo, manutenção) são **provisórios**.
- O prêmio por partida deixa de existir; o dinheiro vem dos eventos (decisão 0012).
