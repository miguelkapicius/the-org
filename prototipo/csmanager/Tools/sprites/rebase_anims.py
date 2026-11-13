# -*- coding: utf-8 -*-
"""Encaixa as animações de fuzil na pose de mira (usuário: "no começo do tiro o boneco mexe o bracinho e começa a
atirar, toda vez"). Medido: nos fuzis o 1º e o último quadro de tiro_<arma> ficam 3-4 cm (escala do boneco) fora de
mira_<arma>, e o fim de sacar_<arma> fica 2,6-6,3 cm fora; nas pistolas bate certinho. Então a cada tiro o braço pula
pra outra pose e volta, e no fim do saque a arma dá um tranco.

  tiro_<arma>:  cada peça leva a correção C = mira(0) · tiro(0)⁻¹ em todos os quadros (o coice fica igual, só que a
                partir da mira). Peça que não aparece no 1º quadro (clarão, cápsula...) leva a correção da arma.
  sacar_<arma> / sacar_costas_<arma>: a correção C = mira(0) · sacar(fim)⁻¹ entra aos poucos (suave, de 0 no meio
                do saque até 1 no fim), então o saque termina exatamente na mira.

    python rebase_anims.py [ct|tr|ambos] [--medir]     (grava por cima do .bin; cópia em Logs/bonecos3d/_antes_rebase)
"""
import json, os, shutil, sys, re
import numpy as np

PASTA = r"D:\unity\CSManager\Logs\bonecos3d"
ARMAS_FUZIL = ["AK47", "AWP", "M4A1S", "M4A4", "Galil", "Famas", "MP9", "MAC10"]


def m4(M34):
    M = np.eye(4); M[:3, :] = M34; return M


def quat(R):
    R = R / np.cbrt(max(abs(np.linalg.det(R)), 1e-9))
    t = np.trace(R)
    if t > 0:
        s = np.sqrt(t + 1) * 2; return np.array([(R[2, 1] - R[1, 2]) / s, (R[0, 2] - R[2, 0]) / s, (R[1, 0] - R[0, 1]) / s, s / 4])
    i = np.argmax(np.diag(R)); j, k = (i + 1) % 3, (i + 2) % 3
    s = np.sqrt(1 + R[i, i] - R[j, j] - R[k, k]) * 2
    q = np.zeros(4); q[i] = s / 4; q[j] = (R[j, i] + R[i, j]) / s; q[k] = (R[k, i] + R[i, k]) / s; q[3] = (R[k, j] - R[j, k]) / s
    return q


def matq(q):
    x, y, z, w = q / np.linalg.norm(q)
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def parcial(C, s):
    """Correção rígida C aplicada só na fração s (0 = nada, 1 = inteira): rotação por slerp a partir da identidade."""
    if s >= 1: return C
    q = quat(C[:3, :3]); q = q if q[3] >= 0 else -q
    ang = 2 * np.arccos(np.clip(q[3], -1, 1))
    if ang < 1e-6: R = np.eye(3)
    else:
        eixo = q[:3] / np.sin(ang / 2); a = ang * s
        R = matq(np.concatenate([eixo * np.sin(a / 2), [np.cos(a / 2)]]))
    out = np.eye(4); out[:3, :3] = R; out[:3, 3] = C[:3, 3] * s
    return out


def vis(M34):
    return np.abs(M34).sum() > 1e-6


def processar(time, medir=False):
    h = json.load(open(os.path.join(PASTA, f"boneco_{time}.json"), encoding="utf-8"))
    blob = np.fromfile(os.path.join(PASTA, f"boneco_{time}.bin"), dtype="<f4")
    nomes = [p["nome"] for p in h["pecas"]]; n = len(nomes); idx = {k: i for i, k in enumerate(nomes)}
    A = {a["nome"]: a for a in h["anims"]}
    Q = lambda a: blob[a["q"]:a["q"] + a["n"] * n * 12].reshape(a["n"], n, 3, 4)
    feitos = []
    for w in ARMAS_FUZIL:
        ref = A.get(f"mira_{w}")
        if ref is None: continue
        mira0 = Q(ref)[0]
        corr_arma = None
        for nome in (f"tiro_{w}", f"sacar_{w}", f"sacar_costas_{w}"):
            a = A.get(nome)
            if a is None: continue
            q = Q(a); fim = nome.startswith("sacar")
            base = q[-1] if fim else q[0]
            C = [None] * n
            for p in range(n):
                if vis(base[p]) and vis(mira0[p]):
                    C[p] = m4(mira0[p]) @ np.linalg.inv(m4(base[p]))
            iw = idx.get(w)
            Cw = C[iw] if iw is not None and C[iw] is not None else np.eye(4)
            antes = max(np.abs(base[p] - mira0[p]).max() for p in range(n) if vis(base[p]) and vis(mira0[p]))
            if medir:
                feitos.append((nome, antes)); continue
            N = len(q)
            for f in range(N):
                s = 1.0 if not fim else float(np.clip((f / max(N - 1, 1) - 0.5) / 0.5, 0, 1))
                s = s * s * (3 - 2 * s) if fim else 1.0
                for p in range(n):
                    if not vis(q[f, p]): continue
                    Cp = C[p] if C[p] is not None else Cw
                    q[f, p] = (parcial(Cp, s) @ m4(q[f, p]))[:3, :]
            depois = max(np.abs((q[-1] if fim else q[0])[p] - mira0[p]).max() for p in range(n) if vis(base[p]) and vis(mira0[p]))
            feitos.append((nome, antes, depois))
    if not medir:
        dst = os.path.join(PASTA, "_antes_rebase"); os.makedirs(dst, exist_ok=True)
        orig = os.path.join(PASTA, f"boneco_{time}.bin")
        if not os.path.exists(os.path.join(dst, f"boneco_{time}.bin")):
            shutil.copy2(orig, dst); shutil.copy2(os.path.join(PASTA, f"boneco_{time}.json"), dst)
        blob.astype("<f4").tofile(orig)
    return feitos


if __name__ == '__main__':
    times = ["ct", "tr"] if len(sys.argv) < 2 or sys.argv[1] == "ambos" else [sys.argv[1]]
    medir = "--medir" in sys.argv
    for t in times:
        for x in processar(t, medir):
            print(t, x[0], "antes %.4f" % x[1] + ("" if medir else "  depois %.4f" % x[2]))
