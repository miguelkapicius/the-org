// Smoke por volume (Fumaca.hlsl): caixa da grade da smoke, raymarch da face da frente até a saída da caixa ou a parede/chão
// (profundidade da cena). Luz: sombra da própria smoke na direção do sol (3 amostras), contínua, base mais escura. Cor já
// multiplicada pela alfa. ShadowCaster: a smoke faz sombra no chão e nos bonecos (pontilhada onde é rala, o filtro da
// sombra suaviza), na profundidade do primeiro ponto cheio do raio do sol.
Shader "CSM/FumacaVolume"
{
    Properties
    {
        _CorLuz ("Cor iluminada", Color) = (0.93, 0.93, 0.92, 1)
        _CorSombra ("Cor da sombra", Color) = (0.6, 0.63, 0.7, 1)
        _Indice ("Fatia no atlas", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "FumacaVolume"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/Game/Visual/Fumaca.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorLuz, _CorSombra;
                float _Indice;
            CBUFFER_END
            struct A { float4 p : POSITION; };
            struct V { float4 p : SV_POSITION; float3 w : TEXCOORD0; float4 s : TEXCOORD1; };
            V Vert(A a)
            {
                V o; o.w = TransformObjectToWorld(a.p.xyz); o.p = TransformWorldToHClip(o.w); o.s = ComputeScreenPos(o.p); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                int k = (int)_Indice;
                float3 camW = GetCameraPositionWS();
                float3 o = FumJogo(camW), dW = normalize(i.w - camW), d = FumJogoDir(dW);
                float3 mn = _FumMin[k].xyz, mx = mn + _FumTam.xyz;
                float2 tt = FumCaixa(o, d, mn, mx);
                float t0 = max(tt.x, 0), t1 = tt.y;
                // para na parede/chão: profundidade da cena convertida em distância no raio (unidades do jogo)
                float eye = LinearEyeDepth(SampleSceneDepth(i.s.xy / i.s.w), _ZBufferParams);
                float3 frente = -UNITY_MATRIX_V[2].xyz;
                t1 = min(t1, eye / max(dot(dW, frente), 1e-3) / _FumEsc);
                if (t1 <= t0) discard;
                Light sol = GetMainLight();
                float3 L = FumJogoDir(sol.direction);
                float passo = max((t1 - t0) / 72, 0.55);
                float jit = frac(52.9829189 * frac(dot(i.p.xy, float2(0.06711056, 0.00583715))));   // sem faixas de passo
                float T = 1; float3 cor = 0;
                float cheia = FumRaioFuro(o, d);   // raio passando pela fresta de um tiro: smoke rala
                float t = t0 + passo * jit;
                [loop] for (int n = 0; n < 110 && t < t1; n++, t += passo)
                {
                    float3 p = o + d * t;
                    float s = FumDens(p, k);
                    if (s < 0.003) continue;
                    float a = 1 - exp(-s * cheia * FUM_SIGMA * passo);
                    float sh = FumDens(p + L * 2, k) + FumDens(p + L * 5, k) * 0.8 + FumDens(p + L * 10, k) * 0.6;
                    float luz = smoothstep(0.04, 0.9, exp(-sh * 0.95));
                    float baixo = saturate((p.z - mn.z) / 18.0);
                    half3 c = lerp(_CorSombra.rgb, _CorLuz.rgb * sol.color, luz) * lerp(0.78, 1.0, baixo);
                    // rolos de luz e sombra andando na superfície (o mesmo fluxo do FumDens, ruído mais miúdo): a smoke
                    // ativa lê como fumaça revirando, não como algodão parado
                    c *= lerp(0.86, 1.07, FumRola(p, k, 1.8));
                    c += SampleSH(float3(0, 1, 0)) * 0.07 + (1 - s) * 0.05;   // fiapo ralo da borda clareia
                    cor += T * a * c; T *= 1 - a;
                    if (T < 0.015) { T = 0; break; }
                }
                float alfa = 1 - T;
                if (alfa < 0.003) discard;
                return half4(cor, alfa);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Back
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex VertS
            #pragma fragment FragS
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Game/Visual/Fumaca.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorLuz, _CorSombra;
                float _Indice;
            CBUFFER_END
            float3 _LightDirection;
            struct A { float4 p : POSITION; };
            struct VS { float4 p : SV_POSITION; float3 w : TEXCOORD0; };
            VS VertS(A a) { VS o; o.w = TransformObjectToWorld(a.p.xyz); o.p = TransformWorldToHClip(o.w); return o; }
            float4 FragS(VS i, out float prof : SV_Depth) : SV_Target
            {
                int k = (int)_Indice;
                float3 o = FumJogo(i.w), d = FumJogoDir(-_LightDirection);
                float2 tt = FumCaixa(o, d, _FumMin[k].xyz, _FumMin[k].xyz + _FumTam.xyz);
                float t0 = max(tt.x, 0), t1 = tt.y;
                if (t1 <= t0) discard;
                // limiar pontilhado: onde a smoke é rala só parte dos texels faz sombra
                float lim = 0.15 + 0.75 * frac(52.9829189 * frac(dot(i.p.xy, float2(0.06711056, 0.00583715))));
                float passo = max((t1 - t0) / 56, 0.8), tau = 0, t = t0; bool achou = false;
                [loop] for (int n = 0; n < 80 && t < t1; n++, t += passo)
                {
                    tau += FumDens(o + d * t, k) * passo;
                    if (1 - exp(-tau * FUM_SIGMA) > lim) { achou = true; break; }
                }
                if (!achou) discard;
                float3 g = o + d * t;
                float4 c = TransformWorldToHClip(float3(g.x, g.z, -g.y) * _FumEsc);
                prof = c.z / c.w;
                return 0;
            }
            ENDHLSL
        }
    }
}
