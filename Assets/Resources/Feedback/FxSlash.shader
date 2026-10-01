// Crescent scythe slash. UV.x runs along the arc, UV.y across its width.
// _Progress sweeps the bright leading edge; the tail fades behind it.
Shader "Deadline4Sec/FxSlash"
{
    Properties
    {
        _Color ("Edge Color", Color) = (1, 0.25, 0.6, 1)
        _Progress ("Progress", Range(0, 1.5)) = 0.5
        _Fade ("Fade", Range(0, 1)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" "RenderType"="Transparent" }
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
                half4 _Color;
                float _Progress, _Fade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float behind = _Progress - i.uv.x;
                float tail = saturate(1 - behind / 0.6) * step(0, behind);
                // Thick at the outer rim, tapering toward the inner edge.
                float rim = smoothstep(0.0, 0.35, i.uv.y) * (1 - smoothstep(0.85, 1.0, i.uv.y));
                float edge = smoothstep(0.55, 0.95, i.uv.y);
                float a = tail * rim * _Fade;
                half3 col = lerp(_Color.rgb, half3(1, 0.95, 1), edge * tail) * a * 1.6;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
