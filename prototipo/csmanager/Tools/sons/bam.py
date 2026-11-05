# BAM do match point mais impactante e mais alto (usuário, 2026-11-04: "muito baixo, tem que ser mais impactante, ~1,5 s").
# Parte do BAM aprovado (nº 6 = _opcoes/matchpoint_5): corta em 1,5 s, comprime (mais alto por igual) e soma um "tum" grave
# que cai de ~90 pra 38 Hz no golpe. 3 níveis -> _opcoes/bam_K.wav. O v19 usa bam_1 por baixo das falas.
import os, numpy as np, soundfile as sf
import motor
from motor import SR

OP = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'saida', '_opcoes')


def bam(nivel):
    x = sf.read(os.path.join(OP, 'matchpoint_5.wav'))[0][:int(1.5 * SR)].copy()
    f = int(0.35 * SR); x[-f:] *= np.linspace(1, 0, f) ** 1.5
    pk = int(np.argmax(np.abs(x[:int(0.4 * SR)])))
    if nivel > 0:
        n = int(1.1 * SR); t = np.arange(n) / SR
        fr = 38 + 52 * np.exp(-t / 0.06)
        tum = np.sin(2 * np.pi * np.cumsum(fr) / SR) * np.exp(-t / (0.25 + 0.12 * nivel)) * np.clip(t / 0.003, 0, 1)
        m = min(n, len(x) - pk); x[pk:pk + m] += tum[:m] * (0.45 + 0.3 * nivel) * np.abs(x).max()
    x = motor.compressor(x / np.abs(x).max(), -18, 4, 10.0)
    return motor.acabamento(x, -0.5, 60)


if __name__ == '__main__':
    for k in range(3):
        y = bam(k); motor.salvar(os.path.join(OP, f'bam_{k}.wav'), y)
        print(k + 1, f'{len(y) / SR:.2f} s', f'rms {20 * np.log10(np.sqrt((y ** 2).mean())):.1f} dB', flush=True)
    os._exit(0)
