// Brilho: peças que emitem luz (clarão do tiro, luneta, visor do C4, rastro): cor chapada x intensidade (HDR, pega
// o bloom), sem sombra.
Shader "CSM/Brilho"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Intensidade ("Intensidade", Range(0, 8)) = 2.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "Unlit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half _Intensidade;
            CBUFFER_END
            struct A { float4 p : POSITION; half4 c : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; half4 c : COLOR; };
            V Vert(A a) { UNITY_SETUP_INSTANCE_ID(a); V o; o.p = TransformObjectToHClip(a.p.xyz); o.c = a.c * _BaseColor; return o; }
            half4 Frag(V i) : SV_Target { return half4(i.c.rgb * _Intensidade, 1); }
            ENDHLSL
        }
    }
}
