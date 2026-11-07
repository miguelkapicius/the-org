# 0026 — Contratação por negociação (sem preço fixo)

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/03-progressao.md, gdd/08-ui-ux.md, glossario.md
- **Substitui:** o preço fixo de contratação do mercado (a venda continua pela metade do valor de mercado)

## Contexto
No mercado, todo jogador tinha um preço fixo (função só da habilidade) e o salário saía de uma fórmula. Bastava ter o dinheiro e clicar em Contratar, sem nenhuma decisão do jogador contratado.

## Decisão (palavras do autor)
- Os bots do mercado **não têm preço fixo**: eles **levam em conta outras coisas** e **negociam com o manager**, numa conversa de chat (referência: uma tela de chats com lista de conversas e balões).

## Detalhamento (proposta do agente, a validar)
1. **O que o jogador pede:** **luvas** (pagamento único na assinatura, tirado do caixa) e **salário por semana**. O ponto de partida é o valor de mercado e o salário de tabela, ajustados pelo **desejo de jogar no seu clube**: de ±35 % nas luvas e ±20 % no salário.
2. **O que pesa no desejo** (cada fator vai de contra a favor e tem um peso):
   - **Prestígio:** circuito do clube e posição no ranking; jogador muito bom exige clube à altura.
   - **Vaga:** se ele entraria como titular (melhoraria o seu time). Estrela pesa o dobro; jovens pesam mais.
   - **Estrutura:** nível da Gaming House (jovens valorizam mais).
   - **Finanças:** quantas semanas de despesa o caixa cobre (salário em dia).
   - **Clima:** moral e entrosamento médios do elenco (o Líder pesa mais).
   - Veterano (28+) abre mão de luvas por salário; Estrela pede 10 % mais de salário.
3. **A conversa:** o manager monta a proposta em degraus (luvas e salário) e pode **garantir a vaga de titular**. O jogador responde: **aceita** (a proposta cobre 100 % do que ele pede), **faz contraproposta** (80 % a 100 %: parte do caminho; 55 % a 80 %: o mínimo dele, citando o fator que mais pesa contra) ou **recusa**. Cada resposta ruim gasta **paciência** (3); sem paciência ele encerra e só volta a conversar **2 semanas** depois.
4. **Promessa de titular:** o jogador aceita menos se tiver a vaga garantida, mas por 4 semanas, cada semana no banco tira 6 de moral; cumprir a promessa dá +5.
5. **Tela:** a aba **Negociações** do Mercado: lista de conversas, balões, caixa de proposta (com "sua proposta cobre N % do que ele pede" antes de enviar) e o painel **"o que pesa para ele"** (interesse alto, médio ou baixo e uma barra por fator, com a explicação na dica). Os cartões da vitrine mostram o que ele **pede hoje** em vez de um preço.

## Por quê
- Pilar 3 (gestão que aparece na partida): subir a Gaming House, montar um time forte e cuidar do caixa agora barateiam as contratações, e escalar bem passa a importar para trazer alguém.
- Pilar 5 (complexo por dentro, simples de entender): a conclusão vem primeiro (interesse e cobertura da proposta); a conta fica nas dicas e no painel.
- Responde parte da pergunta sobre contratos: o salário acordado fica guardado no jogador.

## Consequências
- Números provisórios (pesos, ±35 %, ±20 %, 3 de paciência, 2 semanas, 6 e 5 de moral).
- `Jogador.Salario` passa a ser o acordado (ou o de tabela); o salvamento guarda o salário acordado e a promessa. As conversas em andamento **não** são salvas.
- A venda continua pela metade do valor de mercado, sem negociação.
- Ainda não há duração de contrato, renovação nem outros clubes disputando o mesmo jogador (perguntas abertas).
