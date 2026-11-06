#ifndef CSM_FUMACA_INCLUDED
#define CSM_FUMACA_INCLUDED
// Smoke por volume (no jeito da do CS2): a densidade da simulação (grade de voxels da SmokeVolume, células de 4) vem numa
// textura 3D (uma fatia por smoke no atlas, Fumaca3D) e é lida ponto a ponto no raymarch; ruído 3D de couve-flor na borda;
// tiro abre um túnel em cone (fino na entrada, largo na saída) e a HE abre uma bolha que volta a encher da borda pro meio,
// os dois com a borda rasgada pelo ruído. Coordenadas do jogo (x, y no chão, z pra cima). Usado pela smoke (FumacaVolume)
// e pela silhueta do boneco dentro dela (Silhueta.shader).
#define FUM_MAX 8
#define FUM_NZ 18
#define FUM_FUROS 24
#define FUM_HES 4
#define FUM_SIGMA 0.9   // quanto tapa por unidade do jogo (smoke cheia fica opaca em ~3 unidades: borda nítida de desenho)

TEXTURE3D(_FumAtlas); SAMPLER(sampler_FumAtlas);
TEXTURE3D(_FumRuido); SAMPLER(sampler_FumRuido);
float4 _FumMin[FUM_MAX];                          // xyz canto da grade (jogo), w = 1 se a fatia está em uso
float4 _FumTam;                                   // tamanho da grade (jogo)
float4 _FuroA[FUM_FUROS], _FuroB[FUM_FUROS];      // pontas do túnel (jogo), w = raio agora na entrada / na saída
float4 _HeP[FUM_HES];                             // centro da HE (jogo), w = raio agora
float _FumT, _FumEsc;

float3 FumJogo(float3 w) { return float3(w.x, -w.z, w.y) / _FumEsc; }
float3 FumJogoDir(float3 d) { return normalize(float3(d.x, -d.z, d.y)); }

// entrada/saída do raio na caixa (t0 > t1 = não cruza)
float2 FumCaixa(float3 o, float3 d, float3 mn, float3 mx)
{
    float3 inv = 1.0 / (abs(d) > 1e-5 ? d : 1e-5 * (d >= 0 ? 1 : -1));
    float3 a = (mn - o) * inv, b = (mx - o) * inv;
    float3 lo = min(a, b), hi = max(a, b);
    return float2(max(max(lo.x, lo.y), lo.z), min(min(hi.x, hi.y), hi.z));
}

float FumBase(float3 g, int i)
{
    float3 u = (g - _FumMin[i].xyz) / _FumTam.xyz;
    if (any(u < 0) || any(u > 1)) return 0;
    u.z = (u.z * FUM_NZ + i * (FUM_NZ + 1)) / (FUM_MAX * (FUM_NZ + 1));   // fatias com uma camada vazia entre elas
    return SAMPLE_TEXTURE3D_LOD(_FumAtlas, sampler_FumAtlas, u, 0).r;
}

// ruído de couve-flor (0..1), sobe devagar
// (0,012 por segundo era ~0,5 do jogo/s: parecia parada; 0,025 sobe ~1/s, ainda calmo)
float FumRuido(float3 g) { return SAMPLE_TEXTURE3D_LOD(_FumRuido, sampler_FumRuido, g * (1.0 / 40.0) + float3(0, 0, -_FumT * 0.025), 0).r; }

// 1 = smoke, 0 = aberto (túnel de tiro / bolha da HE)
float FumAberto(float3 g, float n)
{
    float k = 1;
    [loop] for (int j = 0; j < FUM_FUROS; j++)
    {
        float4 A = _FuroA[j], B = _FuroB[j];
        if (B.w <= 0.01) continue;
        float3 ab = B.xyz - A.xyz; float u = saturate(dot(g - A.xyz, ab) / max(dot(ab, ab), 1e-4));
        float r = lerp(A.w, B.w, u);
        float d = length(g - (A.xyz + ab * u)) - r + (n - 0.5) * r * 0.9;
        k = min(k, smoothstep(0, 1.6, d));
    }
    [loop] for (int h = 0; h < FUM_HES; h++)
    {
        float4 H = _HeP[h]; if (H.w <= 0.01) continue;
        float3 q = g - H.xyz; q.z *= q.z > 0 ? 0.5 : 1;   // pra cima vai o dobro: rompe o topo do domo e fecha por último
        float d = length(q) - H.w + (n - 0.5) * H.w * 0.5;
        k = min(k, smoothstep(0, 4, d));
    }
    return k;
}

// Quanto a smoke fica cheia neste raio de visão (1 = cheia): o raio que passa perto de um túnel de tiro vê a smoke rala
// por inteiro. A câmera olha de cima e o túnel passa na altura do peito, escondido no meio da nuvem: assim quem assiste vê
// a fresta que a bala abriu (e enxerga o chão e os bonecos por ela), de qualquer ângulo.
float FumRaioFuro(float3 o, float3 d)
{
    float k = 1;
    [loop] for (int j = 0; j < FUM_FUROS; j++)
    {
        float4 A = _FuroA[j], B = _FuroB[j];
        if (B.w <= 0.01) continue;
        // ponto do túnel mais perto do raio (reta x segmento)
        float3 e = B.xyz - A.xyz, w0 = o - A.xyz;
        float b = dot(d, e), c = dot(e, e), dd = dot(d, w0), ee = dot(e, w0);
        float u = saturate((ee - b * dd) / max(c - b * b, 1e-4));
        float3 q = A.xyz + e * u;
        float3 p = o + d * max(dot(q - o, d), 0);
        // da largura do túnel de ponta a ponta: afinar nas pontas fazia o rastro "começar no meio da smoke" (usuário);
        // o túnel sai da borda da nuvem dos dois lados, como no CS2
        float r = max(lerp(A.w, B.w, u), 0.01);
        // borda rasgada: ruído no ponto do túnel e no ponto do raio (a fresta não sai reta como corte de faca)
        float n = FumRuido(q * 1.3) * 0.6 + FumRuido(p * 2.9 + 17) * 0.4;
        // borda definida (rasgada pelo ruído) em vez de borrão: lia como risco de sujeira, não como túnel
        k = min(k, smoothstep(0.55, 1.1, length(p - q) / max(r, 0.01) + (n - 0.5) * 0.6));
    }
    // só afina a fumaça (não abre até o chão: de cima virava uma risca escura)
    // túnel de verdade (CS2): quase vazio por dentro, dá pra ver o que está do outro lado
    return lerp(0.1, 1, k);
}

// densidade desenhada (0..1) da smoke i no ponto g: as esferas das bolotas (Fumaca3D) com a borda ondulada pelo ruído
// grande e comida em fiapos pelo ruído fino (só na borda: o miolo fica cheio); a rampa macia deixa a borda rala, sem
// cara de massinha
float FumDens(float3 g, int i)
{
    float b = FumBase(g, i);
    if (b < 0.02) return 0;
    // movimento enquanto ativa (usuário: "a smoke ter um pouco de movimento"): um ruído grande e lento empurra o ponto
    // numa direção que gira devagar (a borda revira, ±4 do jogo) e o fiapo fino escorre pro lado. Só a borda e a textura
    // mexem: o miolo e o tamanho (o que a Sim usa pra tapar a visão, SmokeVolume.SampleVis) ficam iguais
    float w = FumRuido(g * 0.4 + float3(0, 0, _FumT * 0.35)) - 0.5;
    float ang = _FumT * 0.25;
    float3 gw = g + w * float3(cos(ang), sin(ang), 0.6) * 8.0;
    float n = FumRuido(gw), nd = FumRuido(gw * 2.7 + 31 + float3(_FumT * 0.9, _FumT * 0.5, 0));
    float d = b * FumAberto(g, n) + (n - 0.5) * 0.35;
    d -= nd * 0.5 * (1 - saturate(d * 1.15));
    return saturate((d - 0.26) / 0.42);
}

// quanto da linha câmera -> alvo está tampado por smoke (0..1)
float FumCobertura(float3 o, float3 alvo)
{
    float3 d = alvo - o; float L = length(d); d /= max(L, 1e-4);
    float tau = 0, cheia = FumRaioFuro(o, d);
    [loop] for (int i = 0; i < FUM_MAX; i++)
    {
        if (_FumMin[i].w < 0.5) continue;
        float2 tt = FumCaixa(o, d, _FumMin[i].xyz, _FumMin[i].xyz + _FumTam.xyz);
        float t0 = max(tt.x, 0), t1 = min(tt.y, L - 0.4);
        if (t1 <= t0) continue;
        float dt = (t1 - t0) / 24;
        [loop] for (int s = 0; s < 24; s++) tau += FumDens(o + d * (t0 + (s + 0.5) * dt), i) * dt;
    }
    return 1 - exp(-tau * cheia * FUM_SIGMA);
}
#endif
