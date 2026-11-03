"""Banco de estratégias da Vila a partir dos rounds pro de Mirage (contract/artifacts/tactics_playbook.json).

Cada round real vira um plano: a rota de cada jogador por região (traduzida pra região da Vila de mesma FUNÇÃO), o bomb
do plant (ou onde o time terminou) e a hora em que o time se comprometeu (3+ vivos na zona do bomb). Passagens curtas
(< 2,5 s numa região) são só caminho e saem. Sai Assets/Game/Sim/EstrategiasDados.cs.

Uso: python -I Tools/estrategias/converter.py <tactics_playbook.json> <tactics.json> <saída .cs>
"""
import json, sys, collections

# Mirage → Vila (nome da forma em MapData), pela função do lugar no round
MAPA = {
    'TSpawn': 'TR', 'CTSpawn': 'CT',
    'TRamp': 'Rampa_baixo', 'PalaceAlley': 'Hall_palacio', 'PalaceInterior': 'Palacio_sala', 'Scaffolding': 'Sacada_A',
    'BombsiteA': 'A', 'Stairs': 'Esc_plataforma', 'Jungle': 'Conector_fim', 'Connector': 'Conector',
    'Middle': 'Meio', 'TopofMid': 'Topo_meio', 'House': 'Topo_entrada', 'Underpass': 'Rato', 'Ladder': 'Esc_rato',
    'SnipersNest': 'Janelao', 'Catwalk': 'Curta_sobe', 'Shop': 'Mercado',
    'SideAlley': 'Rua_B', 'BackAlley': 'Casarao', 'Apartments': 'Casarao_corredor', 'BombsiteB': 'B', 'Truck': 'Viela',
}
TRANSITO = 2.5


def main(pb_path, tac_path, out_path):
    pb = json.load(open(pb_path, encoding='utf-8'))['maps']['de_mirage']
    tac = json.load(open(tac_path, encoding='utf-8'))['maps']['de_mirage']
    places = pb['places']
    zona = {i: tac['places'][n]['zone'] for i, n in enumerate(places)}

    def onde(p, t):
        if p['died_at'] is not None and t >= p['died_at']:
            return None
        for reg, a, b in p['steps']:
            if a <= t < b:
                return reg
        return None

    linhas, cont = [], collections.Counter()
    for r in pb['rounds']:
        for lado in ('T', 'CT'):
            ps = r[lado]
            pl = r.get('plant')
            fim = r['duration_s']
            if pl:
                site, t_plant = pl['site'], pl['t']
            else:
                # sem plant: o bomb é a zona onde o TR passou mais tempo (A/B) nos 15 s antes de acabar
                c = collections.Counter()
                for p in r['T']:
                    for reg, a, b in p['steps']:
                        if b > fim - 15 and zona[reg] in ('A', 'B'):
                            c[zona[reg]] += min(b, fim) - max(a, fim - 15)
                site, t_plant = (c.most_common(1)[0][0] if c else ('A' if pl is None else pl['site'])), -1
            comp = next((tt for tt in range(0, int(fim) + 1) if sum(1 for p in r['T'] if (lambda z: z is not None and zona[z] == site)(onde(p, tt))) >= 3), -1)
            jog = []
            for p in ps:
                passos = []
                for reg, a, b in p['steps']:
                    if b - a < TRANSITO and (reg, a, b) != tuple(p['steps'][-1]):
                        continue
                    v = MAPA[places[reg]]
                    if passos and passos[-1][0] == v:
                        passos[-1][2] = round(b, 1)
                    else:
                        passos.append([v, round(a, 1), round(b, 1)])
                morte = p['died_at']
                jog.append(','.join(f'{v}:{a:g}-{b:g}' for v, a, b in passos) + (f'|{morte:g}' if morte is not None else ''))
            ganhou = 1 if r['winner_side'] == lado else 0
            compra = r['t_buy'] if lado == 'T' else r['ct_buy']
            linhas.append(f"{lado};{compra};{r['id']};{ganhou};{site};{t_plant:g};{comp};{fim:g};" + ';'.join(jog))
            cont[(lado, compra)] += 1

    with open(out_path, 'w', encoding='utf-8') as f:
        f.write('// Gerado por Tools/estrategias/converter.py a partir de rounds pro de Mirage (tactics_playbook.json). Não editar à mão.\n')
        f.write('// Linha: lado;compra;id;ganhou;bomb;plant(s, -1 sem plant);comprometeu(s);duração;jogador1;...;jogador5\n')
        f.write('// Jogador: Região:início-fim,...[|morreu]\n')
        f.write('namespace CSM.Sim\n{\n    public static partial class Estrategias\n    {\n        static readonly string[] DADOS =\n        {\n')
        for l in linhas:
            f.write('            "' + l.replace('"', '') + '",\n')
        f.write('        };\n    }\n}\n')
    print(len(linhas), 'planos:', dict(cont))


if __name__ == '__main__':
    main(*sys.argv[1:4])
