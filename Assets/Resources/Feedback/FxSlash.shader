// Crescent scythe slash. UV.x runs along the arc, UV.y across its width.
// _Progress sweeps the bright leading edge; the tail fades behind it.
// Weapon skin VFX (2026-11-21): _Width moves the bright rim; a material set to
// alpha blending (_Dark 1, _SrcBlend 5 / _DstBlend 10) draws a dark arc
// (ECLIPSE void, BLOOD MOON afterimage) with the _Color rim.
Shader "Deadline4Sec/FxSlash"
{
    Properties
    {
        _Color ("Edge Color", Color) = (1, 0.25, 0.6, 1)
        _Progress ("Progress", Range(0, 1.5)) = 0.5
        _Fade ("Fade", Range(0, 1)) = 1
        _DarkColor ("Dark Core", Color) = (0.03, 0, 0.06, 1)
        _Tail ("Tail Length", Range(0.1, 1.5)) = 0.6
        [HideInInspector] _Dark ("Dark", Float) = 0
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+25" "RenderType"="Transparent" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _DarkColor;
                float _Progress, _Fade, _Tail, _Dark, _SrcBlend, _DstBlend;
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
                float tail = saturate(1 - behind / _Tail) * step(0, behind);
                // Thick at the outer rim, tapering toward the inner edge.
                float rim = smoothstep(0.0, 0.35, i.uv.y) * (1 - smoothstep(0.85, 1.0, i.uv.y));
                float edge = smoothstep(0.55, 0.95, i.uv.y);
                float a = tail * rim * _Fade;
                half3 col = lerp(_Color.rgb, half3(1, 0.95, 1), edge * tail) * a * 1.6;
                if (_Dark > 0.5)
                    return half4(lerp(_DarkColor.rgb, _Color.rgb, edge * 0.85), saturate(a * 1.3));
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
