// Boneco: material só dos bonecos, no clima "brinquedo macio" (usuário: "tipo os bonequinhos do Animal Crossing, mais
// soft, um brilhinho, mais bonitinho"). Mesma proporção e mesmas cores do modelo; muda só como a luz pega nele:
//   - luz enrolada (a sombra própria chega devagar, sem o corte seco do plástico) e sombra colorida/quente, não cinza;
//   - faixa mais viva na passagem luz -> sombra (cara de feltro/massinha, a luz "entra" um pouco no material);
//   - brilho aveludado na borda (o "brilhinho" de pelúcia/feltro), mais forte do lado da luz;
//   - brilho especular largo e fraco no tecido, mais firme na pele e bem pontudo no olho/lente/vidro;
//   - textura fina de tecido (trama + penugem) em espaço do objeto, que some sozinha quando o boneco fica pequeno na tela;
//   - cor um tico mais "pastel" (os escuros sobem sem perder o matiz).
// Tipo da peça no alfa da cor de vértice (Boneco3D.Carregar): 0 tecido, 0,33 pele, 0,66 olho/vidro, 1 duro (arma, metal).
Shader "CSM/Boneco"
{
    Properties
    {
        _BaseColor ("Cor", Color) = (1, 1, 1, 1)
        _Enrola ("Luz enrolada", Range(0, 1)) = 0.5
        _Degrau ("Degrau da luz (0 liso, 1 desenho)", Range(0, 1)) = 0.35
        _SombraCor ("Sombra colorida (0 cinza, 1 bem quente)", Range(0, 1)) = 0.6
        _Faixa ("Faixa viva na borda da sombra", Range(0, 1)) = 0.35
        _Veludo ("Brilho aveludado de borda", Range(0, 2)) = 0.6
        _VeludoPot ("Largura do aveludado (maior = mais fino)", Range(1, 8)) = 3
        _Brilho ("Brilho especular", Range(0, 1)) = 0.25
        _Tecido ("Textura de tecido", Range(0, 1)) = 0.6
        _FreqTecido ("Densidade da trama", Float) = 90
        _Pastel ("Pastel (escuros sobem)", Range(0, 1)) = 0.3
        _Borda ("Luz de borda do céu", Range(0, 1)) = 0.2
        _Ambiente ("Luz ambiente (menor = sombra mais funda)", Range(0, 1.5)) = 1
        _SombraMin ("Piso da sombra projetada", Range(0, 1)) = 0.35
        _Flash ("Piscar branco", Range(0, 1)) = 0
        _Queima ("Pegando fogo", Range(0, 1)) = 0
        _SombraGorda ("Engorda só a sombra (unid. Unity)", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half _Enrola, _Degrau, _SombraCor, _Faixa, _Veludo, _VeludoPot, _Brilho, _Tecido, _Pastel, _Borda, _Flash, _Ambiente, _SombraMin, _Queima;
            float _FreqTecido, _SombraGorda;
        CBUFFER_END

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
                float3 positionOS : TEXCOORD3; float3 normalOS : TEXCOORD4;
                half4 color : COLOR; half fog : TEXCOORD2; UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings Vert(Attributes v)
            {
                Varyings o = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(v); UNITY_TRANSFER_INSTANCE_ID(v, o);
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.positionOS = v.positionOS.xyz; o.normalOS = v.normalOS;
                o.color = half4(v.color.rgb * _BaseColor.rgb, v.color.a);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            // trama de tecido no plano da face (eixo dominante da normal no objeto): fios cruzados + penugem
            float Trama(float3 po, float3 no, out float vis)
            {
                float3 an = abs(no);
                float2 uv = an.x > an.y && an.x > an.z ? po.yz : (an.y > an.z ? po.xz : po.xy);
                uv *= _FreqTecido;
                // some quando a trama fica menor que ~3 pixels (senão vira serrilhado/moiré de longe)
                float px = max(length(fwidth(uv)), 1e-4);
                vis = 1 - smoothstep(0.12, 0.35, px);
                float fio1 = 0.5 + 0.5 * sin(uv.x * 6.2832 + sin(uv.y * 3.1416) * 0.6);
                float fio2 = 0.5 + 0.5 * sin(uv.y * 6.2832 + sin(uv.x * 3.1416) * 0.6);
                float xadrez = step(0.5, frac(floor(uv.x) * 0.5 + floor(uv.y) * 0.5));   // por cima/por baixo
                float trama = lerp(fio1, fio2, xadrez);
                float penugem = Ruido(po * _FreqTecido * 2.7) * 0.6 + Ruido(po * _FreqTecido * 0.35) * 0.4;
                return lerp(trama, penugem, 0.45);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                float3 n = normalize(i.normalWS);
                float3 v = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half tipo = i.color.a;
                half tecido = 1 - step(0.15, tipo), pele = step(0.15, tipo) * (1 - step(0.5, tipo));
                half vidro = step(0.5, tipo) * (1 - step(0.85, tipo)), duro = step(0.85, tipo);

                // cor: escuros sobem um pouco mantendo o matiz (pastel); preto chapado vira grafite macio
                half3 base = i.color.rgb;
                half3 pastel = pow(max(base, 1e-4), 0.72);
                base = lerp(base, pastel, _Pastel * (1 - vidro));
                // pele: o laranja vivo de brinquedo vira um pêssego macio
                half lp = dot(base, half3(0.299, 0.587, 0.114));
                base = lerp(base, lerp(lp.xxx, base, 0.7) * half3(1.08, 1.0, 0.95) + 0.03, saturate(pele * _Pastel * 1.5));

                // tecido: trama + penugem na cor e no relevo (bump pela derivada de tela)
                if (_Tecido > 0.001 && tecido + pele * 0.4 > 0)
                {
                    float vis; float h = Trama(i.positionOS, normalize(i.normalOS), vis);
                    float k = _Tecido * vis * (tecido + pele * 0.25);
                    base *= 1 + (h - 0.5) * 0.22 * k;
                    float3 dpx = ddx(i.positionWS), dpy = ddy(i.positionWS);
                    float dhx = ddx(h), dhy = ddy(h);
                    float3 r1 = cross(dpy, n), r2 = cross(n, dpx); float det = dot(dpx, r1);
                    float3 grad = sign(det) * (dhx * r1 + dhy * r2);
                    n = normalize(lerp(n, normalize(abs(det) * n - grad * 0.004), saturate(k * 0.8)));
                }
                // variação de tom bem leve (nada fica chapado)
                float rz = Ruido(i.positionOS * 7.0);
                base *= 1 + (rz - 0.5) * 0.06;

                InputData inputData = (InputData)0;
                inputData.positionWS = i.positionWS; inputData.normalWS = n; inputData.viewDirectionWS = v;
                inputData.shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                inputData.fogCoord = i.fog;
                inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask = half4(1, 1, 1, 1);
                AmbientOcclusionFactor ao = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1);

                // sombra colorida: o lado escuro puxa pro próprio matiz, mais saturado e um tico quente
                half lum = dot(base, half3(0.299, 0.587, 0.114));
                half3 sat = saturate(lerp(lum.xxx, base, 1.6));
                half3 tomSombra = lerp(half3(1, 1, 1), normalize(sat + 1e-3) * 1.732 * half3(1.05, 0.97, 0.92), _SombraCor);
                half3 ambiente = SampleSH(n) * ao.indirectAmbientOcclusion * _Ambiente;
                half3 cor = base * ambiente * tomSombra;

                Light L = GetMainLight(inputData.shadowCoord, i.positionWS, inputData.shadowMask);
                half ndl = dot(n, L.direction);
                half enrolada = saturate((ndl + _Enrola) / (1 + _Enrola));
                half degrau = smoothstep(0.18, 0.62, enrolada);
                half lam = lerp(enrolada, degrau, _Degrau);
                // a sombra projetada também chega macia (meio caminho): sem buraco preto embaixo do braço
                half somb = lerp(_SombraMin, 1, L.shadowAttenuation) * L.distanceAttenuation * ao.directAmbientOcclusion;
                half luz = lam * somb;
                cor += base * L.color * luz;
                // faixa viva na passagem (luz entrando no material: feltro, massinha, pele)
                half faixa = saturate(luz * (1 - luz) * 4);
                cor += sat * L.color * faixa * _Faixa * (0.35 + 0.65 * (tecido + pele)) * 0.35;
                // especular: largo e fraco no tecido, médio na pele, pontudo no olho/vidro, firme no duro
                half3 hv = normalize(L.direction + v);
                half nh = saturate(dot(n, hv));
                half pot = tecido * 10 + pele * 24 + vidro * 220 + duro * 60;
                half forca = _Brilho * (tecido * 0.35 + pele * 0.6 + vidro * 3.0 + duro * 0.9);
                cor += L.color * pow(nh, pot) * forca * (pot + 8) / 48 * somb * step(0, ndl);
                // aveludado: o "brilhinho" da borda, mais do lado da luz e um pouco no escuro também
                half fres = pow(1 - saturate(dot(n, v)), _VeludoPot);
                half ladoLuz = saturate(ndl * 0.5 + 0.5);
                half3 corVeludo = lerp(base * 1.4 + 0.12, half3(1, 0.97, 0.92), 0.45);
                cor += corVeludo * fres * _Veludo * (0.25 + 0.75 * ladoLuz) * (tecido + pele * 0.6 + duro * 0.3) * lerp(1, L.color.r, 0.5);

                #if defined(_ADDITIONAL_LIGHTS)
                uint nLuzes = GetAdditionalLightsCount();
                #if USE_CLUSTER_LIGHT_LOOP
                [loop] for (uint li = 0; li < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); li++)
                {
                    Light la = GetAdditionalLight(li, inputData.positionWS, inputData.shadowMask);
                    half la_l = saturate((dot(n, la.direction) + _Enrola) / (1 + _Enrola));
                    cor += base * la.color * la_l * la.distanceAttenuation * la.shadowAttenuation;
                }
                #endif
                LIGHT_LOOP_BEGIN(nLuzes)
                    Light la = GetAdditionalLight(lightIndex, inputData.positionWS, inputData.shadowMask);
                    half la_l = saturate((dot(n, la.direction) + _Enrola) / (1 + _Enrola));
                    cor += base * la.color * la_l * la.distanceAttenuation * la.shadowAttenuation;
                LIGHT_LOOP_END
                #endif

                // vidro (lente da máscara do CT, olhos; usuário: "lente com mais cara de vidro"): o tom da lente fica fundo e
                // escuro, por cima vai o reflexo do céu (fresnel: mais na borda), um gradiente de luz atravessando por
                // dentro (mais claro embaixo, como lente de verdade) e um brilho de vitrine fixo em relação à câmera (a
                // câmera é de cima: o brilho do sol quase nunca caía na lente)
                if (vidro > 0.5)
                {
                    half3 rv = reflect(-v, n);
                    half3 ceu = lerp(half3(0.45, 0.55, 0.68), half3(1.0, 0.98, 0.94), saturate(rv.y * 0.6 + 0.45));
                    half fv = 0.06 + 0.94 * pow(1 - saturate(dot(n, v)), 4);
                    float3 nv = mul((float3x3)UNITY_MATRIX_V, n);   // normal no espaço da câmera
                    half dentro = saturate(0.5 - nv.y * 0.6);         // embaixo da lente (vista) mais clara
                    half3 fundo = base * lerp(0.22, 1.0, dentro);
                    // (o branco do olho e o pontinho de reflexo já são claros: não escurecem, só ganham o brilho)
                    half escuro = 1 - smoothstep(0.55, 0.8, max(base.r, max(base.g, base.b)));
                    cor = lerp(cor, fundo, 0.8 * escuro) + ceu * fv * 0.75 * lerp(0.3, 1, escuro);
                    // lente desenhada (usuário: "não tá com cara de vidro/lente no CT ainda"): a lente é uma esfera achatada
                    // quase plana, então reflexo de luz não aparece nela. No espaço do objeto ela é o disco unitário (x, y;
                    // y = cima), aí dá pra pintar o vidro no estilo desenho: fundo escuro em cima clareando embaixo (luz
                    // atravessando), pontinho de brilho embaixo e a borda clara de lente. Só nas lentes
                    // (vidro azulado: máscara do CT, luneta), não nos olhos
                    // (pela cor de vértice crua, sem o ruído de tom: com o ruído a regra oscilava pixel a pixel e a lente
                    // saía em manchas)
                    half3 cv = i.color.rgb;
                    if (cv.b > cv.r * 1.6 && cv.b > 0.1 && cv.b < 0.75)
                    {
                        float2 dl = i.positionOS.xy; float rl = length(dl);
                        half3 tinta = base * lerp(1.15, 0.18, smoothstep(-0.7, 0.6, dl.y));
                        half aa = max(fwidth(rl), 1e-3) * 1.5;
                        // (o reflexo grande é a peça modelada "reflexo" em cima da lente: desenhar outro fazia 2 bolotas)
                        half ponto = 1 - smoothstep(0.11 - aa, 0.11 + aa, length(dl - float2(0.36, -0.38)));
                        half borda = smoothstep(0.78, 0.97, rl);
                        half3 vidroCor = tinta + half3(0.55, 0.85, 1.0) * borda * 0.35;
                        vidroCor = lerp(vidroCor, half3(1, 1, 1), ponto * 0.75);
                        cor = vidroCor * (0.6 + 0.4 * luz) + ceu * fv * 0.3;
                    }
                    // pontinho de reflexo modelado (peça branca em cima da lente): branco limpo, a luz quente o deixava bege
                    if (min(cv.r, min(cv.g, cv.b)) > 0.8) cor = lerp(cor, half3(1, 1, 1), 0.75);
                    float3 lv = normalize(mul((float3x3)UNITY_MATRIX_I_V, float3(-0.3, 0.4, 0.87)));
                    half3 hv2 = normalize(lv + v);
                    cor += pow(saturate(dot(n, hv2)), 160) * 1.6 + pow(saturate(dot(n, hv2)), 18) * 0.1 * escuro;
                }
                // luz de borda do céu (leitura de volume de longe, como no material do mapa)
                half fresC = pow(1.0 - saturate(dot(n, v)), 3.0) * saturate(n.y * 0.5 + 0.5);
                cor += base * fresC * _Borda;
                // pegando fogo (usuário: "não ele inteiro"): manchas de brasa laranja que sobem e tremulam, com brilho
                // próprio e um tico de borda quente
                if (_Queima > 0.001)
                {
                    // manchas menores e só ~1/4 do corpo (com tudo pegando fogo o corpo inteiro ficava rosa)
                    float3 pq = i.positionWS * 17.0 + float3(0, -_Time.y * 3.0, 0);   // (manchas sobem com o tempo)
                    float fq = Ruido(pq) * 0.65 + Ruido(pq * 2.3 + 5.1) * 0.35;
                    float tremor = 0.75 + 0.25 * sin(_Time.y * 17 + fq * 6);
                    float mq = smoothstep(0.6, 0.72, fq) * _Queima * tremor * (1 - vidro) * (1 - duro);
                    // (usuário: "o fogo é mais alaranjado do que rosado") brasa laranja com brilho próprio
                    half3 brasa = half3(1.0, 0.52, 0.12);
                    cor = lerp(cor, cor * half3(1.2, 0.75, 0.45) + brasa * 0.55, mq * 0.85);
                    cor += half3(1.0, 0.5, 0.15) * fres * _Queima * 0.35;
                }
                cor = lerp(cor, half3(1, 0.97, 0.92), _Flash);
                cor = MixFog(cor, i.fog);
                return half4(cor, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0 Cull Off
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
