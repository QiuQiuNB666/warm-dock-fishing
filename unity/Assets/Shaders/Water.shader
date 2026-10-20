// 手写 URP 水面：顶点正弦起伏 + 半透明 + _Color。无 compute/geometry。
Shader "Warm/Water"
{
    Properties
    {
        _Color ("Color", Color) = (0.25, 0.8, 0.78, 0.55)
        _WaveAmp ("Wave Amplitude", Float) = 0.08
        _WaveLen ("Wave Length", Float) = 6
        _WaveSpeed ("Wave Speed", Float) = 1.2
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

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _WaveAmp;
                float _WaveLen;
                float _WaveSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float wave : TEXCOORD0; float fog : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                float3 ws = TransformObjectToWorld(v.positionOS.xyz);
                float k = 6.2831853 / max(_WaveLen, 0.01);
                float t = _Time.y * _WaveSpeed;
                float w = sin(ws.x * k + t) * 0.6 + sin(ws.z * k * 0.7 + t * 1.3) * 0.4;
                ws.y += w * _WaveAmp;
                o.positionCS = TransformWorldToHClip(ws);
                o.wave = w;
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                half3 c = _Color.rgb * (1.0 + i.wave * 0.08);
                c = MixFog(c, i.fog);
                return half4(c, _Color.a);
            }
            ENDHLSL
        }
    }
}
