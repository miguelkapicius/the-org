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
        [NoScaleOffset] _Escada ("Escadas (r madeira + 2 x piso, gb descida, a degrau)", 2D) = "black" {}
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
        TEXTURE2D(_Escada);   // Cenario3D/MalhaMapa.Escada: por coluna, r = madeira?, gb = direção de descida no jogo, a = altura do degrau (0 = não é escada)
        float4 _EscPedra, _EscMadeira;   // globais (MalhaMapa.Escada): cor do degrau de pedra / de madeira
        float _QuinaViva;   // global (Cenario3D): quina viva no mapa (0 = arredondado de antes)

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
                // quina viva (usuário: parede e telhado "massinha", a falta de nitidez aparece nas bordas e quinas): o borrão do
                // surface nets faz toda quina virar uma curva de ~1,5 voxel e a luz/cor escorrem por ela. Onde a curvatura é
                // de quina (curva curta: normal muda rápido por distância), a normal encaixa no eixo dominante -> a luz vira
                // seco no meio da quina, como aresta de verdade. Curva larga (cúpula, torre redonda, rampa) não mexe.
                float quina = 0;
                if (_UsarTopo > 0.5 && _QuinaViva > 0.5)
                {
                    float curv = length(fwidth(n)) / max(length(fwidth(i.positionWS)), 1e-5);   // ~1/raio (unid. Unity)
                    quina = smoothstep(5.0, 9.0, curv);
                    float3 an = abs(n);
                    float3 eixo = an.y >= an.x && an.y >= an.z ? float3(0, sign(n.y), 0) : (an.x >= an.z ? float3(sign(n.x), 0, 0) : float3(0, 0, sign(n.z)));
                    n = normalize(lerp(n, eixo, quina));
                }
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
                    // na quina (já encaixada pra cima) o ponto fica até ~1,5 abaixo do topo: tolerância de altura maior
                    float tolA = lerp(0.34, 1.7, quina), escA = lerp(1.5, 0.75, quina);
                    bool liso = s0.a > 256 && abs(s0.a - 512 - g.z) < tolA;
                    if (liso) base = lerp(base, s0.rgb, smoothstep(0.86, 0.97, n.y));
                    float3 cor[4] = { base, base, base, base }; float peso[4] = { 0, 0, 0, 0 }; int nc = 0; float pesos = 0;
                    if (!liso)
                    {
                    [unroll] for (int k = 0; k < 9; k++)
                    {
                        int2 o = int2(k % 3, k / 3) - 1;
                        float2 d = q - float2(q0 + o);
                        float4 s = LOAD_TEXTURE2D(_Topo, clamp(q0 + o, 0, lim));
                        float wk = exp(-dot(d, d) / (2 * 0.5 * 0.5)) * saturate(1.5 - abs((s.a > 256 ? s.a - 512 : s.a) - g.z) * escA);
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
                // detalhe nítido no mapa (usuário: parede e telhado "massinha", "colocar alguma coisa ali nítida"): telha em
                // fileiras com sulco e reboco descascado mostrando o adobe. Material pela cor (paleta linear: telha laranja
                // g/r 0,15-0,32; reboco creme g/r 0,6-0,95). Bordas de 1 pixel (fwidth). Desliga junto com a quina viva.
                if (_UsarTopo > 0.5 && _QuinaViva > 0.5)
                {
                    float3 g2 = float3(i.positionWS.x, -i.positionWS.z, i.positionWS.y) / _Esc;   // x, y, z do jogo
                    float rr = max(base.r, 1e-3), gr = base.g / rr, br = base.b / rr;
                    // escada (usuário: "melhora visual nas rampas/escadas"): o borrão do surface nets faz a escada virar
                    // rampa lisa com o xadrez esticado. Na rampa (só nas colunas de escada, MalhaMapa.Escada) cada faixa de
                    // altura H vira um degrau: piso (normal pra cima) + espelho (normal pra frente, ~40% da faixa), sombra no
                    // canto do piso com o espelho de cima, risco claro no bico e juntas entre as lajes. A luz faz o resto.
                    float4 es = LOAD_TEXTURE2D(_Escada, clamp(int2(floor(g2.xy)), 0, int2(_Topo_TexelSize.zw) - 1));
                    float2 d2 = normalize(es.gb + float2(1e-5, 0));                // pra onde desce (jogo)
                    // rodapé da escada no muro do lado (usuário: "bugadinha na parede"): a curvinha do borrão entre a rampa e
                    // a parede também cai nas colunas de escada; com degrau ela picotava em dentinhos claros. Lá (normal
                    // tombada pro lado, não pra frente da escada) fica pedra lisa da cor do degrau, sem trocar a normal
                    // r = madeira (0/1) + 2 x piso da escada; a < 0 = coluna de parede do lado (só rodapé)
                    bool madE = fmod(es.r, 2.0) > 0.5; float pisoE = floor(es.r / 2.0);
                    half3 corE = madE ? _EscMadeira.rgb : _EscPedra.rgb;
                    // a parede que entra na coluna de escada (pé do muro, uns 1,5 voxel do borrão) também vira essa pedra, e
                    // nas 2 colunas de parede do lado só até 3 acima do piso da escada: o encontro com o reboco fica numa
                    // linha limpa, não no serrilhado dos degraus de voxel
                    // (as facetas do encontro eram metade pedra, metade terra/reboco, cada uma com a luz pra um lado: viram a
                    // cor e a normal da própria parede — pé do reboco, Tom 0,74 — e o que é quase chão segue o degrau)
                    // só o pé do muro (até 3 acima do piso da escada ali): acima disso a parede fica como é (usuário: "todas as
                    // paredes com escada ficaram escuras" — a face inteira do muro cai dentro das colunas de escada no borrão)
                    float rodape = es.a < -0.5 || es.a > 0.5 ? 1 - saturate((g2.z - pisoE - 3.0) / max(fwidth(g2.z), 1e-4)) : 0;
                    // normal de parede = eixo da própria parede (muro do mapa é reto em x ou em z); a perpendicular da escada
                    // virava a luz no muro do fundo dela e escurecia tudo
                    float3 nParede = abs(n.x) >= abs(n.z) ? float3(sign(n.x), 0, 0) : float3(0, 0, sign(n.z));
                    // cor do reboco 6 com o MESMO escurecido de pé de muro do MalhaMapa.Tom (0,74 no chão até 1 com 16 de
                    // altura): emenda sem degrau de cor com a parede de cima; na escada de madeira a lateral é madeira escura
                    float altP = g2.z - 8.0;
                    half3 pe = madE ? _EscMadeira.rgb * 0.62 : half3(0.871, 0.693, 0.423) * lerp(0.74, 1.0, saturate(altP / 16.0)) * lerp(1.0, 1.05, saturate((altP - 20.0) / 20.0));
                    // (só no que já é parede/terra: a ponta de viga marrom e outros enfeites no muro do lado passam batido)
                    bool pareceParede = base.r > 0.28 && base.g > 0.12;
                    if (!pareceParede) rodape = 0;
                    if (rodape > 0 && n.y <= 0.6)
                    {
                        base = lerp(base, pe, rodape);
                        n = normalize(lerp(n, nParede, 0.7 * rodape));
                    }
                    else if (es.a > 0.5 && n.y > 0.6)   // (quina também: a normal é trocada pela do degrau de qualquer jeito)
                    {
                        float H = es.a, kz = (g2.z - 8.0) / H, fk = frac(kz), ak = max(fwidth(kz), 1e-4);   // 8 = MapData.GZ
                        float3 hor = float3(d2.x, 0, -d2.y);                       // (y do jogo = -z da Unity)
                        float rf = 0.4;
                        float espelho = saturate((fk - (1 - rf)) / ak);
                        float fundo = saturate(1 - ((1 - rf) - fk) / (ak * 3.0)) * (1 - espelho);   // canto do piso com o espelho
                        float bico = saturate(1 - fk / (ak * 1.5));                                // risco claro na quina
                        float baixoBico = saturate((fk - 0.9) / ak);                                 // sombra do bico no alto do espelho
                        float w = dot(g2.xy, float2(-d2.y, d2.x)) / 9.0 + floor(kz) * 0.5;
                        float lj = 1 - saturate((min(frac(w), 1 - frac(w)) - 0.02) / max(fwidth(w), 1e-4));
                        float tomD = 0.94 + 0.1 * Hash3(float3(floor(kz), floor(w), 5));
                        half3 corD = corE * tomD * (1 - 0.4 * fundo) * (1 + 0.14 * bico) * (1 - 0.3 * baixoBico) * (1 - 0.25 * lj * (1 - espelho));
                        corD *= lerp(1.0, 1.1, espelho);   // espelho um tico mais claro (na sombra ficava marrom chapado)
                        base = corD;
                        n = normalize(lerp(float3(0, 1, 0), hor, espelho));
                    }
                    else if (n.y > 0.8 && base.r > 0.3 && gr > 0.15 && gr < 0.32 && br < 0.12)
                    {   // telha canal (usuário: "arruma a textura das tampas"; a de 3 x 3,2 com os pontinhos do material 51 lia
                        // como papel quadriculado): canudo de 5 de largura ao longo do y, telha de 6 desencontrada meia por
                        // coluna, vão escuro entre canudos, sombra da beira da telha de cima, e o abaulado pela normal
                        float col = g2.x / 5.0, fc = frac(col), ci = floor(col);
                        float lin = g2.y / 6.0 + fmod(abs(ci), 2.0) * 0.5, fl = frac(lin), li = floor(lin);
                        float ac = max(fwidth(col), 1e-4), al = max(fwidth(lin), 1e-4);
                        // cor única (sem os pontinhos do 51) com brilho e matiz por quarteirão (casas diferentes)
                        float qt = Ruido(float3(g2.xy / 70.0, 2.3)), mt = Ruido(float3(g2.xy / 110.0, 7.1));
                        half3 telha = lerp(half3(0.745, 0.162, 0.05), half3(0.70, 0.205, 0.06), mt) * (0.86 + 0.24 * qt) * (0.96 + 0.08 * Ruido(g2 / 45.0));
                        float tom = 0.9 + 0.16 * Hash3(float3(ci, li, 7));
                        float velha = step(0.9, Hash3(float3(ci, li, 13)));                      // ~10% mais queimada
                        float canal = 1 - saturate((min(fc, 1 - fc) - 0.06) / ac);
                        float sombra = 1 - saturate((fl - 0.14) / al);
                        float beira = saturate((fl - 0.93) / al);
                        base = telha * tom * (1 - 0.18 * velha) * (1 - 0.45 * canal) * (1 - 0.3 * sombra) * (1 + 0.1 * beira);
                        n = normalize(n + float3(-cos(fc * 3.14159) * 0.6, 0, 0) * (1 - canal));
                    }
                    else if (n.y > 0.8 && base.r > 0.8 && gr > 0.82 && gr < 0.93 && br > 0.53 && br < 0.7)
                    {   // arremate de pedra no rebordo creme (material 8 por cima): pedras irregulares (voronoi de 6) com
                        // junta de 1 pixel, cada pedra um tom, e lasquinhas
                        float2 pp = g2.xy / 6.0, ip = floor(pp); float f1 = 9, f2 = 9; float2 id = ip;
                        [unroll] for (int k2 = 0; k2 < 9; k2++)
                        {
                            float2 c = ip + float2(k2 % 3, k2 / 3) - 1;
                            float2 o = float2(Hash3(float3(c, 1)), Hash3(float3(c, 2))) * 0.8 + 0.1;
                            float dd = length(c + o - pp);
                            if (dd < f1) { f2 = f1; f1 = dd; id = c; } else if (dd < f2) f2 = dd;
                        }
                        float e = f2 - f1, ae = max(fwidth(e), 1e-4);
                        float juntaP = 1 - saturate((e - 0.06) / ae);
                        float tomP = 0.93 + 0.1 * Hash3(float3(id, 3));
                        float pinta = Ruido(float3(g2.xy * 0.9, 1.7)), ap = max(fwidth(pinta), 1e-4);
                        float lasca = saturate((pinta - 0.78) / ap);
                        base *= tomP * (1 - 0.3 * juntaP) * (1 - 0.12 * lasca);
                    }
                    else if (abs(n.y) < 0.35 && base.r > 0.45 && gr > 0.6 && gr < 0.95 && br > 0.3 && br < 0.72)
                    {   // reboco descascado: mancha esparsa (ruído, borda de 1 px e contorno escuro) com tijolo de adobe 5 x 2,4
                        float u = abs(n.x) > abs(n.z) ? g2.y : g2.x, vv = g2.z;
                        float mk = Ruido(float3(u / 11.0, vv / 7.0, 3.1)) * 0.75 + Ruido(float3(u / 4.0, vv / 3.0, 9.7)) * 0.25;
                        float am = max(fwidth(mk), 1e-4);
                        float dentro = saturate((mk - 0.675) / am);   // ~8% da parede (0,64 dava ~15%: muita mancha)
                        float contorno = saturate(1 - abs(mk - 0.675) / (am * 1.6));
                        float lv = vv / 2.4, fv = frac(lv), lu = u / 5.0 + floor(lv) * 0.5, fu = frac(lu);
                        float av = max(fwidth(lv), 1e-4), au = max(fwidth(lu), 1e-4);
                        float jt = max(1 - saturate((min(fv, 1 - fv) - 0.07) / av), 1 - saturate((min(fu, 1 - fu) - 0.035) / au));
                        half3 tijolo = base * half3(0.86, 0.72, 0.6) * (0.9 + 0.16 * Hash3(float3(floor(lu), floor(lv), 1)));
                        tijolo = lerp(tijolo, base * 0.55, jt * 0.8);
                        base = lerp(base, tijolo, dentro);
                        base *= 1 - 0.3 * contorno;
                    }
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
