# Música e vinhetas (ElevenLabs Music, instrumental). Cache: o que já existe não é gerado de novo.
# ~900 créditos por minuto (levantamento 2026). Uso: python musica.py [--so nome,...] [--orcamento 6000] [--seco]
import os, sys, json, urllib.request, urllib.error
from eleven import chave, AQUI

BRUTO = os.path.join(AQUI, 'eleven', 'bruto', 'musica')
URL = 'https://api.elevenlabs.io/v1/music?output_format=mp3_44100_128'
CRED_MIN = 900
ESTILO = ('instrumental, modern esports tactical shooter soundtrack with North African flavor: oud, darbuka and frame drums '
          'mixed with tight electronic drums, deep sub bass and dark synth pads, cinematic, punchy, polished mix')
MUSICAS = {  # nome: (descrição, segundos, candidatos)
    'menu': ('main menu theme, confident and stylish, steady groove, loopable ending', 90, 1),
    'vitoria_tr': ('short victory sting for the attacking team, gritty, triumphant, aggressive percussion hit ending', 6, 2),
    'vitoria_ct': ('short victory sting for the defending team, disciplined, heroic, clean brass-like synth hit ending', 6, 2),
    'derrota': ('short round lost sting, falling, somber, quiet ending', 5, 2),
    'bomba_10s': ('ten seconds of rising tension, pulse accelerating like a ticking bomb, builds to a sudden stop', 10, 2),
    'mvp': ('short celebratory MVP anthem, energetic, memorable hook', 12, 2),
    'match_point': ('dramatic short match point sting, heavy drums, rising tension', 6, 2),
    'intervalo': ('half time break loop, relaxed but tense groove, loopable', 30, 1),
}


def main():
    if '--forcar' not in sys.argv:
        print('música adiada pelo usuário (2026-11-04): rode com --forcar quando ele pedir'); return
    so = sys.argv[sys.argv.index('--so') + 1].split(',') if '--so' in sys.argv else None
    orc = float(sys.argv[sys.argv.index('--orcamento') + 1]) if '--orcamento' in sys.argv else 6000
    falta = [(n, d, s, os.path.join(BRUTO, f'{n}_c{c}.mp3')) for n, (d, s, nc) in MUSICAS.items() if not so or n in so
             for c in range(nc) if not os.path.exists(os.path.join(BRUTO, f'{n}_c{c}.mp3'))]
    custo = sum(round(s / 60 * CRED_MIN) for _, _, s, _ in falta)
    print(f'{len(falta)} músicas a gerar, ~{custo} créditos (orçamento {orc:.0f})')
    if '--seco' in sys.argv or not falta: return
    k = chave()
    if not k: print('sem ELEVENLABS_API_KEY'); sys.exit(2)
    gasto = 0
    for n, d, s, dest in falta:
        c = round(s / 60 * CRED_MIN)
        if gasto + c > orc: print('orçamento atingido'); break
        corpo = json.dumps(dict(prompt=f'{d}. {ESTILO}', music_length_ms=int(s * 1000), force_instrumental=True)).encode()
        req = urllib.request.Request(URL, data=corpo, method='POST', headers={'Content-Type': 'application/json', 'xi-api-key': k, 'User-Agent': 'csmanager-sons/1.0'})
        try:
            with urllib.request.urlopen(req, timeout=300) as r: dados = r.read()
            os.makedirs(BRUTO, exist_ok=True); open(dest, 'wb').write(dados); gasto += c
            print(f'  {os.path.basename(dest)} (+{c}, total {gasto})', flush=True)
        except urllib.error.HTTPError as e:
            print('  ERRO', e.code, e.read()[:300].decode('utf-8', 'replace'))
    print('gasto ~', gasto)


if __name__ == '__main__':
    main()
