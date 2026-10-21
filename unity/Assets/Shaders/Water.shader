// 手写 URP 水面：顶点正弦起伏（公式与 Floater.Wave 一致）+ 近岸浅远处深 + 太阳高光/菲涅耳 + 半透明。无 compute/geometry。
// 影子圆片也复用本 shader：_WaveAmp=_Lift=_Spec=0 时就是纯色半透明。
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
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        Pass
        {
            Name "Forward"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WaveAmp, _WaveLen, _WaveSpeed, _Lift, _ShoreFade, _Spec;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 ws : TEXCOORD0; float3 n : TEXCOORD1; float wave : TEXCOORD2; float fog : TEXCOORD3; };

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
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float t = _Time.y * _WaveSpeed;
                float2 ripple = float2(sin(i.ws.x * 1.7 + i.ws.z * 0.9 + t * 2.1), sin(i.ws.z * 1.3 - i.ws.x * 0.6 + t * 1.7)) * 0.12 * saturate(_WaveAmp * 20);
                float3 n = normalize(i.n + float3(ripple.x, 0, ripple.y));
                float d = saturate(i.ws.z / max(_ShoreFade, 0.01));   // 0 = 岸边, 1 = 远海
                float lift = _Lift * (1 - d);
                half3 c = saturate(_Color.rgb * (1 + lift) + lift * 0.15) * (1.0 + i.wave * 0.08);
                half a = _Color.a * (1 - 0.4 * lift);   // 近岸更透，始终看得见海底

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
