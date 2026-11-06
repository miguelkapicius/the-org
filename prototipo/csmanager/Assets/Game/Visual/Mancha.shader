// Mancha de sangue no chão (Efeitos3D.Respingo): quad deitado, gota de borda irregular (sorteada por _Semente) com dois
// pinguinhos em volta; sem luz (sangue escuro lê igual em sol e sombra); some pela alfa de _BaseColor.
Shader "CSM/Mancha"
{
    Properties { _BaseColor ("Cor", Color) = (0.42, 0.03, 0.04, 0.85) _Semente ("Semente", Float) = 0 }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-20" }
        Pass
        {
            Name "Mancha"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; float _Semente;
            CBUFFER_END
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A a) { V o; o.p = TransformObjectToHClip(a.p.xyz); o.uv = a.uv; return o; }
            float H(float x) { return frac(sin(x * 127.1 + _Semente * 311.7) * 43758.5453); }
            half4 Frag(V i) : SV_Target
            {
                float2 q = (i.uv - 0.5) * 2;
                float ang = atan2(q.y, q.x) / 6.2832 * 7;
                float k = floor(ang), f = frac(ang); f = f * f * (3 - 2 * f);
                float r = 0.55 + 0.18 * lerp(H(k), H(fmod(k + 1, 7)), f);
                half a = 1 - smoothstep(r - 0.06, r, length(q));
                // dois pinguinhos soltos perto da borda
                for (int j = 0; j < 2; j++)
                {
                    float an = H(j + 11) * 6.2832, dd = 0.72 + 0.16 * H(j + 17);
                    float2 c = float2(cos(an), sin(an)) * dd;
                    a = max(a, 1 - smoothstep(0.07, 0.11, length(q - c)));
                }
                return half4(_BaseColor.rgb, a * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
