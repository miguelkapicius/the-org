# Lê o soundevents do CS2 (decompilado) e tira a "receita" de cada evento das nossas armas -> receita_cs2.json
# CS2 é só REFERÊNCIA: nenhum arquivo de som dele entra no jogo.
import re, json, sys
src = sys.argv[1]; out = sys.argv[2]
txt = open(src, encoding='utf-8', errors='replace').read()
ev = {}
for m in re.finditer(r'^\t([A-Za-z0-9_.]+) = \n\t\{', txt, re.M):
    i = m.end(); d = 1
    while d: 
        c = txt[i]; d += (c == '{') - (c == '}'); i += 1
    ev[m.group(1)] = txt[m.end():i-1]
def num(b, k):
    m = re.search(r'\n\t\t' + k + r' = ([-0-9.]+)', b); return float(m.group(1)) if m else None
def curva(b, k):
    m = re.search(r'\n\t\t' + k + r' = \n\t\t\[(.*?)\n\t\t\]', b, re.S)
    if not m: return None
    return [[float(x) for x in re.findall(r'[-0-9.e]+', p)[:2]] for p in re.findall(r'\[(.*?)\]', m.group(1), re.S)]
def lista(b, k):
    m = re.search(r'\n\t\t' + k + r' = \n\t\t\[(.*?)\]', b, re.S)
    return re.findall(r'"([^"]+)"', m.group(1)) if m else []
ARMAS = ['ak47','m4a1','m4a1_silencer','awp','mp9','mac10','usp','hkp2000','glock','deagle','p250','hegrenade','flashbang','smokegrenade','molotov','incgrenade','c4','knife','sensor']
res = {}
for n, b in ev.items():
    low = n.lower()
    if not low.startswith(('weapon_','c4.','flashbang','hegrenade','smokegrenade','molotov','inferno','basegrenade','bomb','player.','c4')): continue
    if not any(a in low for a in ARMAS + ['grenade','inferno','bomb','c4','knife','molotov']): continue
    res[n] = dict(volume=num(b,'volume'), pitch_min=num(b,'pitch_random_min'), pitch_max=num(b,'pitch_random_max'),
                  mix=(re.search(r'mixgroup = "([^"]+)"', b) or [None,None])[1], limite=num(b,'instance_limit'),
                  oclusao=num(b,'occlusion_intensity'), dist=curva(b,'distance_volume_mapping_curve'),
                  arquivos=lista(b,'vsnd_files_track_01') or lista(b,'vsnd_files'), filhos=lista(b,'soundevent_01') + lista(b,'soundevent_02'))
json.dump(res, open(out,'w',encoding='utf-8'), indent=1, ensure_ascii=False)
print(len(ev), 'eventos;', len(res), 'das nossas armas')
for n in sorted(res):
    r = res[n]
    if any(s in n for s in ['Single','Distant','Detonate','Explode','Beep','Plant']):
        print(f"{n:40s} vol {r['volume']} mix {r['mix']} dist {r['dist']} n={len(r['arquivos'])} filhos {r['filhos']}")
