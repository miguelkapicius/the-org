// Silhueta: parte do boneco escondida atrás de parede aparece na cor do time (pedido do usuário no 2D). Desenhada pelo
// RenderObjects do renderer (camada Bonecos, teste de profundidade Greater); cor por boneco em _CorTime
// (MaterialPropertyBlock), tracejado diagonal pra ler como "atrás" e não como boneco de verdade.
// Passe 1 (SilhuetaFumaca, RenderObjects depois dos transparentes): o mesmo desenho por cima da smoke, na medida de quanto
// de smoke tem entre a câmera e o boneco (Fumaca.hlsl) — boneco dentro ou atrás da smoke continua aparecendo.
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
            // cada pixel recebe silhueta UMA vez: sem isso braço, tronco e arma somavam a transparência um por cima do
            // outro e apareciam as costuras das peças dentro da silhueta (bit 7 do stencil, que o URP forward não usa)
            Stencil { Ref 128 ReadMask 128 WriteMask 128 Comp NotEqual Pass Replace }
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
                // chapada (a luz de borda por peça desenhava o contorno de cada pedaço por dentro)
                half a = lerp(0.6, 0.85, listra) * _CorTime.a;
                return half4(saturate(_CorTime.rgb * 1.12), saturate(a));
            }
            ENDHLSL
        }
        Pass
        {
            Name "SilhuetaFumaca"
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off ZTest LEqual Cull Back
            // uma vez por pixel (bit 4): braço, tronco e arma não somam transparência um por cima do outro
            Stencil { Ref 4 ReadMask 4 WriteMask 4 Comp NotEqual Pass Replace }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Assets/Game/View3D/Fumaca.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _CorTime;
            CBUFFER_END
            struct A { float4 p : POSITION; };
            struct V { float4 p : SV_POSITION; float3 w : TEXCOORD0; };
            V Vert(A a) { V o; o.w = TransformObjectToWorld(a.p.xyz); o.p = TransformWorldToHClip(o.w); return o; }
            half4 Frag(V i) : SV_Target
            {
                // quanto de smoke tem entre a câmera e este ponto do boneco: a silhueta aparece na mesma medida
                float cob = FumCobertura(FumJogo(GetCameraPositionWS()), FumJogo(i.w));
                if (cob < 0.08) discard;
                half listra = step(0.5, frac((i.p.x + i.p.y) / 6.0));
                half a = lerp(0.6, 0.85, listra) * _CorTime.a * smoothstep(0.08, 0.6, cob);
                return half4(saturate(_CorTime.rgb * 1.12), saturate(a));
            }
            ENDHLSL
        }
    }
}
