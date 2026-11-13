# -*- coding: utf-8 -*-
"""python -I planta_svg.py <saida.svg>  -> planta da v10 com as coberturas (vermelho = esconde em pé, amarelo = só agachado)"""
import sys, os
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import brasa10 as M
px = 24
COR = {'T': '#e9c9a0', 'C': '#b9c6dc', 'a': '#f3e3b0', 'b': '#f3e3b0', 'r': '#d9d3c7', 'k': '#c7b89a', '~': '#c7a070', 'n': '#cdbb9a', 's': '#c4bcae'}
o = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {M.W * px} {M.H * px}" font-family="Segoe UI, Arial, sans-serif">',
     f'<rect width="{M.W * px}" height="{M.H * px}" fill="#8a5a44"/>']
for zona, letra, poli, z, mat in M.P:
    pts = ' '.join(f'{x * px:.1f},{y * px:.1f}' for x, y in poli)
    o.append(f'<polygon points="{pts}" fill="{COR.get(letra, "#d9d3c7")}" stroke="{COR.get(letra, "#d9d3c7")}" stroke-width="1"/>')
for s_, (x0, x1, y0, y1) in M.SITES.items():
    o.append(f'<rect x="{x0 * px}" y="{y0 * px}" width="{(x1 - x0) * px}" height="{(y1 - y0) * px}" fill="none" stroke="#b03a2e" stroke-width="2" stroke-dasharray="8 6"/>')
for n, x, y in M.ROT:
    o.append(f'<text x="{x * px:.1f}" y="{y * px + 5:.1f}" font-size="13" text-anchor="middle" fill="#5a4a3a" font-weight="600">{n}</text>')
for nome, poli, h, tipo in M.COB:
    pts = ' '.join(f'{x * px:.1f},{y * px:.1f}' for x, y in poli)
    o.append(f'<polygon points="{pts}" fill="{"#c0392b" if h >= 22 else "#e2b93b"}" stroke="#3a2a20" stroke-width="1"/>')
for nome, poli, h, tipo in M.COB:
    if tipo == 'stall': continue
    x, y = M.centro(poli)
    o.append(f'<text x="{x * px:.1f}" y="{min(p[1] for p in poli) * px - 3:.1f}" font-size="10" text-anchor="middle" fill="#1e1610">{nome}</text>')
for (x, y) in M.SPAWN_T: o.append(f'<circle cx="{x * px}" cy="{y * px}" r="5" fill="#d98a2b"/>')
for (x, y) in M.SPAWN_CT: o.append(f'<circle cx="{x * px}" cy="{y * px}" r="5" fill="#3b6fb0"/>')
o.append(f'<g font-size="13" fill="#fff"><rect x="10" y="{M.H * px - 62}" width="330" height="52" fill="#000" opacity="0.45" rx="6"/>'
         f'<rect x="20" y="{M.H * px - 52}" width="14" height="14" fill="#c0392b"/><text x="42" y="{M.H * px - 40}">esconde em pé (altura 22 ou mais)</text>'
         f'<rect x="20" y="{M.H * px - 30}" width="14" height="14" fill="#e2b93b"/><text x="42" y="{M.H * px - 18}">só agachado / dá cabecinha (12 a 16)</text></g>')
o.append('</svg>')
open(sys.argv[1], 'w', encoding='utf-8').write(''.join(o))
