// Partícula de smoke (no jeito da smoke do CS2): billboards grandes e macios que se fundem numa massa só — sem bola
// aparecendo. A luz é da smoke INTEIRA (normal de forma = do centro da smoke pra partícula, vinda no vértice), com só um
// pouco da curvatura da própria partícula; borda sem contorno, comida por ruído fractal (fiapos de fumaça) que gira devagar;
// base mais escura; fade onde encosta na parede/chão. Alfa por partícula na cor do vértice (densidade da simulação: some no
// furo de bala e na bolha da HE). uv2.x = semente. Passe MarcaFumaca: só stencil (bit 2) onde a smoke cobre de verdade,
// pra silhueta do boneco aparecer por cima dela (Silhueta.shader, passe SilhuetaFumaca).
Shader "CSM/FumacaParticula"
{
    Properties
    {
        _CorLuz ("Cor iluminada", Color) = (0.93, 0.93, 0.92, 1)
        _CorSombra ("Cor da sombra", Color) = (0.6, 0.63, 0.7, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "FumacaParticula"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Off
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorLuz, _CorSombra;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; half4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float semente : TEXCOORD1; half4 c : COLOR; float4 s : TEXCOORD2; float prof : TEXCOORD3; float3 forma : TEXCOORD4; };
            V Vert(A a)
            {
                V o; float3 w = TransformObjectToWorld(a.p.xyz);
                o.p = TransformWorldToHClip(w); o.uv = a.uv; o.semente = a.uv2.x; o.c = a.c; o.forma = a.n;
                o.s = ComputeScreenPos(o.p); o.prof = -TransformWorldToView(w).z;
                return o;
            }
            float H2(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }
            float R2(float2 p) { float2 i = floor(p), f = frac(p); f = f * f * (3 - 2 * f);
                return lerp(lerp(H2(i), H2(i + float2(1, 0)), f.x), lerp(H2(i + float2(0, 1)), H2(i + float2(1, 1)), f.x), f.y); }
            float Fbm(float2 p) { return R2(p) * 0.5 + R2(p * 2.03 + 7.1) * 0.3 + R2(p * 4.1 + 3.7) * 0.2; }
            float Hash1(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float RuidoAng(float a, float s)
            {
                float x = a * 0.6366 + s * 13.0;   // 4 lóbulos grandes por volta
                float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(Hash1(i + s * 7.0), Hash1(i + 1 + s * 7.0), f);
            }
            half4 Frag(V i) : SV_Target
            {
                // contorno NÍTIDO em lóbulos grandes (estilo da maquete) e miolo cheio: a silhueta lê como nuvem de desenho
                float2 q = i.uv * 2 - 1; float r = length(q);
                float ang = atan2(q.y, q.x) + i.semente * 6.28;
                float borda = 0.82 + 0.18 * RuidoAng(ang, i.semente);
                float a = 1 - smoothstep(borda - 0.045, borda, r);
                if (a <= 0.002) discard;
                // luz pela FORMA da smoke inteira (partículas vizinhas pegam a mesma luz: não aparece bola por dentro) + um
                // pouco do volume da partícula (ondulação), em 3 tons suaves como o resto do jogo
                float3 nPart = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(q * 0.7, 1)));
                float3 nrm = normalize(normalize(i.forma) * 0.75 + nPart * 0.3);
                Light sol = GetMainLight();
                float l = saturate(dot(nrm, sol.direction) * 0.5 + 0.5);
                float luz = smoothstep(0.34, 0.46, l) * 0.55 + smoothstep(0.62, 0.74, l) * 0.45;
                float baixo = saturate(nrm.y * 0.5 + 0.6);
                half3 c = lerp(_CorSombra.rgb, _CorLuz.rgb * sol.color, luz) * lerp(0.85, 1.0, baixo);
                c += SampleSH(nrm) * 0.06;
                float cena = LinearEyeDepth(SampleSceneDepth(i.s.xy / i.s.w), _ZBufferParams);
                a *= saturate((cena - i.prof) / 0.25);
                return half4(c * i.c.rgb, a * i.c.a);
            }
            ENDHLSL
        }
        Pass
        {
            Name "MarcaFumaca"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            ColorMask 0 ZWrite Off ZTest LEqual Cull Off
            Stencil { Ref 2 WriteMask 2 Comp Always Pass Replace }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment FragMarca
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorLuz, _CorSombra;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; float2 uv : TEXCOORD0; float2 uv2 : TEXCOORD1; half4 c : COLOR; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; float semente : TEXCOORD1; half4 c : COLOR; float4 s : TEXCOORD2; float prof : TEXCOORD3; float3 forma : TEXCOORD4; };
            V Vert(A a)
            {
                V o; float3 w = TransformObjectToWorld(a.p.xyz);
                o.p = TransformWorldToHClip(w); o.uv = a.uv; o.semente = a.uv2.x; o.c = a.c; o.forma = a.n;
                o.s = ComputeScreenPos(o.p); o.prof = -TransformWorldToView(w).z;
                return o;
            }
            float Hash1(float x) { return frac(sin(x * 127.1) * 43758.5453); }
            float RuidoAng(float a, float s)
            {
                float x = a * 0.6366 + s * 13.0;
                float i = floor(x), f = frac(x); f = f * f * (3 - 2 * f);
                return lerp(Hash1(i + s * 7.0), Hash1(i + 1 + s * 7.0), f);
            }
            float Alfa(V i)
            {
                float2 q = i.uv * 2 - 1; float r = length(q);
                float ang = atan2(q.y, q.x) + i.semente * 6.28;
                float borda = 0.82 + 0.18 * RuidoAng(ang, i.semente);
                float a = 1 - smoothstep(borda - 0.045, borda, r);
                float cena = LinearEyeDepth(SampleSceneDepth(i.s.xy / i.s.w), _ZBufferParams);
                return a * saturate((cena - i.prof) / 0.25) * i.c.a;
            }
            half4 FragMarca(V i) : SV_Target { clip(Alfa(i) - 0.45); return 0; }
            ENDHLSL
        }
    }
}
