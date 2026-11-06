// Nuvem: poeira, fumaça de explosão e faíscas que somem aos poucos. Luz suave de desenho (difusa "embrulhada" + céu),
// transparência pela alpha de _BaseColor (MaterialPropertyBlock por partícula), _Brilho > 0 = emissivo (fogo/faísca).
Shader "CSM/Nuvem"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Brilho ("Brilho", Range(0, 8)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "Queue" = "Transparent" }
        Pass
        {
            Name "Nuvem"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half _Brilho;
            CBUFFER_END
            struct A { float4 p : POSITION; float3 n : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 p : SV_POSITION; float3 n : TEXCOORD0; float3 w : TEXCOORD1; };
            V Vert(A a)
            {
                UNITY_SETUP_INSTANCE_ID(a);
                V o; o.w = TransformObjectToWorld(a.p.xyz); o.p = TransformWorldToHClip(o.w); o.n = TransformObjectToWorldNormal(a.n); return o;
            }
            half4 Frag(V i) : SV_Target
            {
                float3 n = normalize(i.n);
                Light l = GetMainLight();
                half dif = saturate(dot(n, l.direction) * 0.5 + 0.5);
                half3 c = _BaseColor.rgb * (l.color * dif * 0.85 + SampleSH(n) * 0.6);
                c = lerp(c, _BaseColor.rgb * _Brilho, saturate(_Brilho));
                // borda mais transparente (bolota macia)
                float3 v = GetWorldSpaceNormalizeViewDir(i.w);
                half borda = saturate(dot(n, v) * 1.6);
                return half4(c, _BaseColor.a * borda);
            }
            ENDHLSL
        }
    }
}
