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
        [NoScaleOffset] _Topo ("Mapa visto de cima (rgb cor, a altura)", 2D) = "black" {}
        [Toggle] _UsarTopo ("Usar mapa de cima no que olha pra cima", Float) = 0
        _Esc ("Unidade Unity por unidade do jogo", Float) = 0.04
        _SombraGorda ("Engorda só a sombra (unid. Unity)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Smoothness, _Variacao, _Borda, _UsarVertice, _Flash, _UsarTopo;
            float _Esc, _SombraGorda; float4 _Topo_TexelSize;
        CBUFFER_END
        TEXTURE2D(_Topo);

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
                half3 base = i.color.rgb;
                // chão/topo: cor do mapa de cima por pixel (a cor de vértice dá escadinha de voxel). Nos 4x4 texels em volta,
                // peso gaussiano estreito (alisa o degrau do voxel e ainda guarda o detalhe de 1 voxel) e só os da MESMA altura (não puxa a cor da parede/telhado
                // vizinho); cada cor soma o peso dos seus texels e ganha a maior -> borda nítida que segue a curva suave,
                // não o degrau do voxel; transição de 1 pixel na fronteira. Mais um granulado fino.
                if (_UsarTopo > 0.5 && n.y > 0.86)
                {
                    float3 g = float3(i.positionWS.x, -i.positionWS.z, i.positionWS.y) / _Esc;   // x, y, z do jogo
                    float2 q = g.xy - 0.5;
                    // 3x3 em volta do texel mais perto: os 7 que sobravam no 4x4 ficam a 1,5+ texel (peso gaussiano < 1%)
                    int2 q0 = int2(floor(q + 0.5)), lim = int2(_Topo_TexelSize.zw) - 1;
                    // texel marcado (alfa + 512, MalhaMapa.Topo): os 5x5 em volta têm a mesma altura e a mesma cor, então a
                    // votação daria a própria cor dele (até 3/255 de diferença) — pula as leituras
                    float4 s0 = LOAD_TEXTURE2D(_Topo, clamp(int2(floor(g.xy)), 0, lim));
                    bool liso = s0.a > 256 && abs(s0.a - 512 - g.z) < 0.34;
                    if (liso) base = lerp(base, s0.rgb, smoothstep(0.86, 0.97, n.y));
                    float3 cor[4] = { base, base, base, base }; float peso[4] = { 0, 0, 0, 0 }; int nc = 0; float pesos = 0;
                    if (!liso)
                    {
                    [unroll] for (int k = 0; k < 9; k++)
                    {
                        int2 o = int2(k % 3, k / 3) - 1;
                        float2 d = q - float2(q0 + o);
                        float4 s = LOAD_TEXTURE2D(_Topo, clamp(q0 + o, 0, lim));
                        float wk = exp(-dot(d, d) / (2 * 0.5 * 0.5)) * saturate(1.5 - abs((s.a > 256 ? s.a - 512 : s.a) - g.z) * 1.5);
                        if (wk < 1e-3) continue;
                        pesos += wk;
                        bool achou = false, novo = false;
                        [unroll] for (int c = 0; c < 4; c++)
                            if (!achou && c < nc && distance(cor[c] / peso[c], s.rgb) < 0.012) { cor[c] += s.rgb * wk; peso[c] += wk; achou = true; }
                        [unroll] for (int c2 = 0; c2 < 4; c2++)
                            if (!achou && c2 == nc) { cor[c2] = s.rgb * wk; peso[c2] = wk; achou = true; novo = true; }
                        if (novo) nc++;
                    }
                    if (pesos > 1e-3 && nc > 0)
                    {
                        // 1ª e 2ª colocadas
                        float p1 = -1, p2 = 0; float3 c1 = base, c2 = base;
                        [unroll] for (int c = 0; c < 4; c++)
                        {
                            if (c >= nc) continue;
                            if (peso[c] > p1) { p2 = max(p1, 0); c2 = c1; p1 = peso[c]; c1 = cor[c] / peso[c]; }
                            else if (peso[c] > p2) { p2 = peso[c]; c2 = cor[c] / peso[c]; }
                        }
                        float3 f = c1;
                        // placar da vencedora: 0,5 = empate (na fronteira) -> mistura meio a meio; ~1 pixel até a cor pura
                        float sc = p1 / max(p1 + p2, 1e-5);
                        float t = saturate((sc - 0.5) / max(fwidth(sc), 1e-4));
                        if (p2 > 0) f = lerp(lerp(c2, c1, 0.5), c1, t);
                        // peso total baixo = só tem texel de outra altura em volta (quina/rampa): fica a cor de vértice
                        float tot = pesos / 1.57;   // soma do gaussiano cheio ~ 2*pi*0,5^2
                        base = lerp(base, f, smoothstep(0.3, 0.7, tot) * smoothstep(0.86, 0.97, n.y));
                    }
                    }
                    float grao = Ruido(g * 0.9) * 0.5 + Ruido(g * 2.3) * 0.5;
                    base *= 1.0 + (grao - 0.5) * 0.08;
                }
                half3 albedo = base * (1.0 + (rz - 0.5) * 2.0 * _Variacao);

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
            ZWrite On ZTest LEqual ColorMask 0 Cull Off   // dos dois lados: peça fina (cano, vara, raio) não some da sombra
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
                float3 n = TransformObjectToWorldNormal(v.normalOS);
                float3 p = TransformObjectToWorld(v.positionOS.xyz);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                    float3 l = normalize(_LightPosition - p);
                #else
                    float3 l = _LightDirection;
                #endif
                // boneco/arma: a sombra sai um tiquinho mais gorda que a peça (cano, alça, dedo têm 1-2 texels de sombra e
                // sumiam no filtro macio). Só pro lado (perpendicular à luz): pra frente da luz a peça se sombrearia sozinha
                p += (n - dot(n, l) * l) * _SombraGorda;
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
