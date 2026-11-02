// 手写 URP 水面：顶点正弦起伏（公式与 Floater.Wave 一致）+ 近岸浅远处深 + 太阳高光/菲涅耳 + 半透明。无 compute/geometry。
// 影子圆片也复用本 shader：_WaveAmp=_Lift=_Spec=0 时就是纯色半透明。
// loop-22：_Foam>0 时岸边一条泡沫线（只在 z>0 的海面上：河、瀑布、影子圆片都不画）；_Mode=1 泡沫环（码头桩、礁石入水处，uv.x 0 内沿→1 外沿）；
//          _Mode=2 湿沙（材质设成乘法混合 DstColor Zero）；_Mode=3 沙上泡沫（沙台顶面 z<0 的薄片）。泡沫亮度跟主光 + 环境光走、颜色只留三成，夜里自然压暗。
Shader "Warm/Water"
{
    Properties
    {
        _Color ("Deep Color", Color) = (0.25, 0.8, 0.78, 0.55)
        _WaveAmp ("Wave Amplitude", Float) = 0.08
        _WaveLen ("Wave Length", Float) = 6
        _WaveSpeed ("Wave Speed", Float) = 1.2
        _Lift ("Shallow Lift", Float) = 0.6
        _ShoreFade ("Shore Fade Distance", Float) = 30
        _Spec ("Specular", Float) = 1
        _Foam ("Shore Foam", Float) = 0
        _Mode ("Mode (0 water, 1 foam ring, 2 wet sand, 3 sand foam)", Float) = 0
        _WetW ("Wet Sand Width", Float) = 2.5
        [Enum(UnityEngine.Rendering.BlendMode)] _SrcBlend ("Src Blend", Float) = 5
        [Enum(UnityEngine.Rendering.BlendMode)] _DstBlend ("Dst Blend", Float) = 10
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WaveAmp, _WaveLen, _WaveSpeed, _Lift, _ShoreFade, _Spec, _Foam, _Mode, _WetW;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float wave : TEXCOORD2; float fog : TEXCOORD3; float2 uv : TEXCOORD4; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float k = 6.2831853 / max(_WaveLen, 0.01);
                float t = _Time.y * _WaveSpeed;
                float w = sin(ws.x * k + t) * 0.6 + sin(ws.z * k * 0.7 + t * 1.3) * 0.4;
                float dx = cos(ws.x * k + t) * 0.6 * k, dz = cos(ws.z * k * 0.7 + t * 1.3) * 0.4 * k * 0.7;
                ws.y += w * _WaveAmp;
                o.positionCS = TransformWorldToHClip(ws);
                o.ws = ws;
                o.n = float3(-dx * _WaveAmp, 1, -dz * _WaveAmp);
                o.wave = w;
                o.fog = ComputeFogFactor(o.positionCS.z);
                o.uv = v.uv;
                return o;
            }

            float Hash(float2 p)   // 不用 sin 的哈希（Dave Hoskins hash12），网页端精度也稳
            {
                float3 p3 = frac(p.xyx * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float Noise(float2 p)   // 值噪声 0..1
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            // 主光 + 环境光只取亮度、颜色留三成（l22p2：直接乘主光色，黄昏成了沙色的橙、夜里成了紫圈）：午间奶白、黄昏暖米白、夜里暗灰白；封顶 0.92，不进 Bloom
            // recv=1 主光项乘实时阴影（沙上泡沫，l22f1p2：不接阴影时岸边人影上半截被抹白）；海上泡沫和泡沫环 recv=0，和水一样不接（l22f2：接了黄昏长影只在泡沫上显出灰褐斑）。
            // 环境光用 SampleSH（同 Lit 沙），影子里的泡沫不比影子里的沙暗；网页 Medium 没实时阴影，衰减恒为 1
            half3 FoamLit(float3 n, float3 ws, half recv)
            {
                Light l = GetMainLight(TransformWorldToShadowCoord(ws));
                half3 c = l.color * (0.5 + 0.5 * saturate(dot(n, l.direction))) * lerp(1, l.shadowAttenuation, recv) + SampleSH(n) * 0.9;
                return min(lerp(dot(c, half3(0.299, 0.587, 0.114)).xxx, c, 0.3), 0.92);
            }

            float FoamEdge(float x, float t) { return 1.2 + 0.2 * sin(t * 0.5) + 0.15 * sin(x * 0.8 + t * 0.3) + 0.08 * sin(x * 2.3 - t * 0.45); }   // 泡沫外沿：随时间前后涨落、沿岸起伏

            // 岸边泡沫：海岸线 z=0（沙台北沿，近岸约半米被沙台挡住）。外沿 FoamEdge，噪声打碎；外侧一条细线时有时无。z<=0 为 0
            float ShoreFoam(float3 ws)
            {
                float t = _Time.y, s = ws.z;
                float edge = FoamEdge(ws.x, t);
                float n = Noise(float2(ws.x * 1.8 + t * 0.12, s * 2.5 - t * 0.25));
                float band = smoothstep(0, 0.1, s) * (1 - smoothstep(edge - 0.06, edge, s)) * smoothstep(0.44, 0.52, n + 0.5 * (1 - s / edge) + 0.4 * (1 - smoothstep(0.6, 0.9, s)));   // 窄过渡：边是干净的形，不是一片糊白（l22p1）；台沿外露出来的头 0.2 m 实心，和沙上泡沫连成一片，台沿直线不从泡沫的洞里露出来
                float thin = (1 - smoothstep(0.02, 0.05, abs(s - edge - 0.35 - 0.1 * sin(ws.x * 0.5 - t * 0.35)))) * smoothstep(0.58, 0.66, n) * 0.6;
                return saturate(band + thin) * _Foam * 0.85;
            }

            // 泡沫环：uv.x 0 = 内沿（藏在桩、礁石底下）→ 1 = 外沿；外沿一呼一吸，各环按位置错开
            float RingFoam(float2 uv, float3 ws)
            {
                float t = _Time.y, r = uv.x;
                float breathe = 0.1 * sin(t * 1.1 + ws.x * 1.7 + ws.z * 2.3);
                float n = Noise(ws.xz * 3.5 + t * 0.25);
                return (1 - smoothstep(0.66 + breathe, 0.74 + breathe, r)) * smoothstep(0.42, 0.5, n + 0.35 * (1 - r)) * 0.75;
            }

            // 沙上泡沫：浪漫过台沿压到沙上，盖住台沿那条直线（l22p2）。台沿处最密，内沿跟 FoamEdge 一起涨落（约 0.25–0.65 m），噪声和海里那条同一套
            float SandFoam(float3 ws)
            {
                float t = _Time.y; float s = -ws.z; float reach = 0.5 * FoamEdge(ws.x, t) - 0.15;
                float n = Noise(float2(ws.x * 1.8 + t * 0.12, ws.z * 2.5 - t * 0.25));
                return (1 - smoothstep(reach - 0.06, reach, s)) * smoothstep(0.44, 0.52, n + 0.5 * (1 - s / reach)) * 0.85;
            }

            // 湿沙：乘在干沙上。最深处（_Color，深约 18%、略偏冷）在台沿往内陆约 0.4 m，紧贴台沿只有一半深（台沿直线两侧反差小，l22p2）；往内陆 _WetW 米渐隐成 1，内沿用噪声打乱
            half3 Wet(float3 ws)
            {
                float d = -ws.z; float k = saturate(d / max(_WetW, 0.01)) + (Noise(ws.xz * float2(0.7, 1.4)) - 0.5) * 0.3;
                return lerp(half3(1, 1, 1), _Color.rgb, (1 - smoothstep(0.1, 1, k)) * lerp(0.45, 1, smoothstep(0, 0.4, d)));
            }

            half4 frag (Varyings i) : SV_Target
            {
                if (_Mode > 2.5) return half4(MixFog(FoamLit(float3(0, 1, 0), i.ws, 1), i.fog), SandFoam(i.ws));
                if (_Mode > 1.5) return half4(Wet(i.ws), 1);
                float t = _Time.y * _WaveSpeed;
                float2 ripple = float2(sin(i.ws.x * 1.7 + i.ws.z * 0.9 + t * 2.1), sin(i.ws.z * 1.3 - i.ws.x * 0.6 + t * 1.7)) * 0.12 * saturate(_WaveAmp * 20);
                float3 n = normalize(i.n + float3(ripple.x, 0, ripple.y));
                if (_Mode > 0.5) return half4(MixFog(FoamLit(n, i.ws, 0), i.fog), RingFoam(i.uv, i.ws));
                float d = saturate(i.ws.z / max(_ShoreFade, 0.01));   // 0 = 岸边, 1 = 远海
                float lift = _Lift * (1 - d);
                half3 c = saturate(_Color.rgb * (1 + lift) + lift * 0.15) * (1.0 + i.wave * 0.08);
                half a = _Color.a * (1 - 0.4 * lift);   // 近岸更透，始终看得见海底
                float f = ShoreFoam(i.ws);
                c = lerp(c, FoamLit(n, i.ws, 0), f); a = lerp(a, 0.9, f);

                Light l = GetMainLight();
                float3 v = normalize(_WorldSpaceCameraPos - i.ws);
                float3 h = normalize(l.direction + v);
                half spec = pow(saturate(dot(n, h)), 60) * _Spec;
                half fres = pow(1 - saturate(dot(n, v)), 4) * 0.5 * _Spec;
                c += l.color * spec + fres * _GlossyEnvironmentColor.rgb;
                c = MixFog(c, i.fog);
                return half4(c, saturate(a + spec * 0.5 + fres * 0.3));
            }
            ENDHLSL
        }
    }
}
