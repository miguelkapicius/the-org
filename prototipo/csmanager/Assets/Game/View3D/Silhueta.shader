// Silhueta: parte do boneco escondida atrás de parede aparece na cor do time (pedido do usuário no 2D). Desenhada pelo
// RenderObjects do renderer (camada Bonecos, teste de profundidade Greater); cor por boneco em _CorTime
// (MaterialPropertyBlock), tracejado diagonal pra ler como "atrás" e não como boneco de verdade.
Shader "CSM/Silhueta"
{
    Properties { _CorTime ("Cor do time", Color) = (1, 0.7, 0.3, 1) }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Silhueta"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest Greater Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorTime;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; };
            struct V { float4 p : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; };
            // o teste de profundidade usa o ponto puxado 0,3 pra câmera (no próprio raio de visão: na tela não mexe): pé ou
            // corpo caído encostando no chão arredondado não vira silhueta, só o que está atrás de parede de verdade
            V Vert(A a)
            {
                V o; o.w = TransformObjectToWorld(a.p.xyz);
                float3 pc = o.w + normalize(GetCameraPositionWS() - o.w) * 0.3;
                o.p = TransformWorldToHClip(pc); o.n = TransformObjectToWorldNormal(a.n); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float2 sp = i.p.xy;
                half listra = step(0.5, frac((sp.x + sp.y) / 6.0));
                float3 v = GetWorldSpaceNormalizeViewDir(i.w);
                half borda = 1 - saturate(dot(normalize(i.n), v));
                half a = (lerp(0.35, 0.75, listra) + borda * 0.25) * _CorTime.a;
                return half4(_CorTime.rgb * (0.85 + borda * 0.4), saturate(a));
            }
            ENDHLSL
        }
    }
}
