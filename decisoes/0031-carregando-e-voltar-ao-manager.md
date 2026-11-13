# 0031 — Tela de carregando da partida e volta ao manager

- **Status:** proposta
- **Data:** 2026-11-07
- **Autor:** miguelkapicius (com Claude)
- **Seções afetadas:** gdd/08-ui-ux.md
- **Substitui:** —

## Contexto
Ao clicar em Assistir mapa, a Unity carregava a cena da partida, montava a maquete e aquecia a simulação (alguns segundos de tela travada), e o jogador só via um texto pequeno no canto. Além disso, a partida vinda do manager abria o **menu do protótipo** (INICIAR e SAIR) antes de começar, e no fim o placar cobria o botão **Voltar ao manager**, que por isso nunca aparecia.

## Opções consideradas
1. **Só um texto de carregando** — o que existia; parece travado.
2. **Tela de carregando cheia, nos dois lados da troca de cena** — o manager mostra a tela antes de trocar de cena e a partida a desenha de novo enquanto monta, com um fade no fim.
3. **Cena de carregando separada** — mais um passo e mais uma cena para manter.

## Decisão
Opção 2, mais: partida vinda do manager **entra direto**, sem o menu do protótipo, e o placar do fim traz um botão **VOLTAR AO MANAGER**.

## Detalhamento (proposta do agente, a validar)
1. **No manager:** ao assistir, uma tela cheia grafite aparece com "PREPARANDO A PARTIDA", os dois times, o campeonato e o mapa, uma barra que corre e "Montando o mapa…"; 450 ms depois a cena da partida carrega.
2. **Na partida:** a mesma tela é desenhada enquanto a maquete é montada e a simulação aquece (e mostra as teclas: espaço pausa, N próximo round, P pensamento dos bots, Tab placar, clique num jogador para segui-lo). Quando termina, some num fade de 0,6 s. A barra só anda nos quadros em que a Unity deixa.
3. **Sem menu do protótipo** quando a partida vem de uma série da carreira; o menu (INICIAR e SAIR) continua só para quem abre a cena direto.
4. **No fim:** o placar mostra um botão único, **VOLTAR AO MANAGER**, que fecha a série da carreira (prêmio, salários, desempenho de cada jogador) e abre a pós-partida. REINICIAR e MENU ficam só na partida avulsa.

## Por quê
- Pilar 5: o jogador sempre sabe o que está acontecendo (carregando) e como sair (voltar), sem telas estranhas no meio.

## Consequências
- A tela de carregando é desenhada em duas tecnologias (UI Toolkit no manager, IMGUI na partida) com o mesmo visual; mudar uma pede mudar a outra.
- A troca de cena e o fade só podem ser conferidos jogando; as capturas em batch cobrem só a tela do manager.
