// Disco: anel na cor do time no pé do boneco (leitura de longe, de que lado é cada um). Anel com borda suave e um
// miolo bem fraco, desenhado por cima do chão sem escrever profundidade.
Shader "CSM/Disco"
{
    Properties { _BaseColor ("Cor", Color) = (1, 1, 1, 0.8) }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent-10" }
        Pass
        {
            Name "Disco"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off Cull Off Offset -1, -1
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
            CBUFFER_END
            struct A { float4 p : POSITION; float2 uv : TEXCOORD0; };
            struct V { float4 p : SV_POSITION; float2 uv : TEXCOORD0; };
            V Vert(A a) { V o; o.p = TransformObjectToHClip(a.p.xyz); o.uv = a.uv; return o; }
            half4 Frag(V i) : SV_Target
            {
                float d = length(i.uv - 0.5) * 2;
                half anel = smoothstep(0.66, 0.76, d) * (1 - smoothstep(0.88, 1.0, d));
                half miolo = 0.18 * (1 - smoothstep(0.2, 0.8, d));
                return half4(_BaseColor.rgb, saturate(anel + miolo) * _BaseColor.a);
            }
            ENDHLSL
        }
    }
}
