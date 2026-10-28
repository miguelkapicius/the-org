# Mecânicas

> **Status:** rascunho · **Responsável:** miguelkapicius · **Decisões:** 0001, 0003, 0004

_Regras e sistemas do jogo, com números quando possível._

> **[Sugestão]** = proposta do agente, ainda não validada.

## Controles

Na partida, o jogador controla a **transmissão**, não os bots (decisão 0001):

- trocar de jogador acompanhado (primeira pessoa);
- **[Sugestão]** câmera livre / câmera de diretor automática;
- **[Sugestão]** velocidade da partida (1x, 2x, pular round).

## Movimento

_Movimento dos bots: ver "IA dos bots" abaixo._

## Sistemas principais

### Partida (simulação)
- **[Sugestão]** Formato 5v5, ataque x defesa, objetivo de plantar/desarmar (ou equivalente com identidade própria, ver decisão 0004).
- Economia por round: dinheiro ganho por vitória, derrota, abates, objetivo; compra de armas e utilitárias.

### IA dos bots (feature principal, decisão 0003)
Os bots devem jogar como profissionais jogariam. Comportamentos que precisam existir:

- **Abrir pixel / jiggle peek**, segurar ângulo, pré-mirar onde o inimigo vai aparecer.
- **[Sugestão]** Posicionamento por função: entry, suporte, AWPer/sniper, lurker, IGL (líder).
- **[Sugestão]** Uso de utilitárias: smoke, flash, molotov em lugares que fazem sentido taticamente.
- **[Sugestão]** Jogo em equipe: trade kill, rotação, execução de estratégias, retake.
- **[Sugestão]** Erro humano crível: tempo de reação, mira imperfeita, decisões ruins sob pressão.
- **[Sugestão]** Atributos do jogador mudam como o bot joga (mira, reação, posicionamento, utilitária, comunicação).

### Gestão do time
- **Economia do time** (dinheiro do clube, salários, prêmios).
- **Melhorias** para o time.
- **[Sugestão]** Elenco: contratar, vender, treinar jogadores com atributos.

## Regras e fórmulas

_A definir._

## Condições de vitória e derrota

_A definir (de partida e de carreira)._

## Perguntas em aberto

- O modo de jogo é o mesmo do CS (bomba) ou criamos um objetivo próprio?
- Quais atributos um jogador tem e como cada um afeta a IA?
- "Melhorias para o time": o que exatamente? (estrutura, bootcamp, equipamento, staff?)
- A economia do round segue valores próximos aos do CS ou própria?
