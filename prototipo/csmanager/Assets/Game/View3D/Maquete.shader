// Maquete: material do cenário e dos bonecos no visual 3D. Cor vem da cor de vértice (paleta do MapVoxels) ou de
// _BaseColor; brilho de plástico/argila (PBR do URP), sombra macia, SSAO, variação suave de tom no mundo pra não
// ficar chapado e luz de borda leve (o "brinquedo" iluminado).
Shader "CSM/Maquete"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Smoothness ("Brilho", Range(0, 1)) = 0.32
        _Variacao ("Variação de tom", Range(0, 0.3)) = 0.07
        _Borda ("Luz de borda", Range(0, 1)) = 0.18
        [Toggle] _UsarVertice ("Usar cor de vértice", Float) = 1
        _Flash ("Piscar branco", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness, _Variacao, _Borda, _UsarVertice, _Flash;
        CBUFFER_END

        // ruído de valor 3D barato (variação de tom no mundo)
        float Hash3(float3 p) { p = frac(p * 0.3183099 + 0.1); p *= 17.0; return frac(p.x * p.y * p.z * (p.x + p.y + p.z)); }
        float Ruido(float3 x)
        {
            float3 i = floor(x), f = frac(x); f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(lerp(Hash3(i), Hash3(i + float3(1, 0, 0)), f.x), lerp(Hash3(i + float3(0, 1, 0)), Hash3(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash3(i + float3(0, 0, 1)), Hash3(i + float3(1, 0, 1)), f.x), lerp(Hash3(i + float3(0, 1, 1)), Hash3(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings
            {
                float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1;
                half4 color : COLOR; half fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.color = lerp(half4(1, 1, 1, 1), v.color, _UsarVertice) * _BaseColor;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                float rz = Ruido(i.positionWS * 1.7) * 0.6 + Ruido(i.positionWS * 5.3) * 0.4;
                half3 albedo = i.color.rgb * (1.0 + (rz - 0.5) * 2.0 * _Variacao);

                InputData d = (InputData)0;
                d.positionWS = i.positionWS; d.normalWS = n; d.viewDirectionWS = v;
                d.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                d.fogCoord = i.fog;
                d.bakedGI = SampleSH(n);
                d.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                d.shadowMask = half4(1, 1, 1, 1);

                SurfaceData s = (SurfaceData)0;
                s.albedo = albedo; s.metallic = 0; s.specular = 0; s.smoothness = _Smoothness;
                s.occlusion = 1; s.alpha = 1; s.normalTS = half3(0, 0, 1);

                half4 c = UniversalFragmentPBR(d, s);
                // luz de borda: contorno claro e quente no lado do céu (leitura de volume de longe)
                half fres = pow(1.0 - saturate(dot(n, v)), 3.0) * saturate(n.y * 0.5 + 0.5);
                c.rgb += albedo * fres * _Borda;
                c.rgb = lerp(c.rgb, half3(1, 0.97, 0.92), _Flash);   // levou tiro: pisca branco
                c.rgb = MixFog(c.rgb, i.fog);
                return half4(c.rgb, 1);
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
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
            float3 _LightDirection; float3 _LightPosition;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 Vert(Attributes v) : SV_POSITION
            {
                UNITY_SETUP_INSTANCE_ID(v);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 l = normalize(_LightPosition - p);
                #else
                    float3 l = _LightDirection;
                #endif
                return ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(p, n, l)));
            }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            struct Attributes { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            float4 Vert(Attributes v) : SV_POSITION { UNITY_SETUP_INSTANCE_ID(v); return TransformObjectToHClip(v.positionOS.xyz); }
            half Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings Vert(Attributes v)
            {
                UNITY_SETUP_INSTANCE_ID(v);
                Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 oct = PackNormalOctQuadEncode(n);
                    return half4(PackFloat2To888(saturate(oct * 0.5 + 0.5)), 0);
                #else
                    return half4(n, 0);
                #endif
            }
            ENDHLSL
        }
    }
}
