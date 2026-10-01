// Additive soft particle used by every combat effect. The falloff is computed
// from UVs, so no texture asset is needed; stretched billboards become streaks.
Shader "Deadline4Sec/FxAdditive"
{
    Properties
    {
        _Softness ("Softness", Range(0.5, 6)) = 2.2
        _Core ("Core Boost", Range(0, 4)) = 1.4
        _Ribbon ("Ribbon (trail) Mode", Range(0, 1)) = 0
        // 0 keeps a transparent render target's alpha untouched (gift chest stage).
        _AlphaWrite ("Alpha Write", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Softness, _Core, _Ribbon, _AlphaWrite;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float d = lerp(length(i.uv * 2 - 1), abs(i.uv.y * 2 - 1), _Ribbon);
                float a = pow(saturate(1 - d), _Softness);
                float core = pow(saturate(1 - d * 1.8), 3) * _Core;
                half3 col = i.color.rgb * a + core * i.color.a;
                return half4(col * i.color.a, _AlphaWrite);
            }
            ENDHLSL
        }
    }
}
