// Low fog sheets under and beside the track (AreaAtmosphere, 2026-11-15).
// One mesh of stacked horizontal sheets riding with the camera; the noise is
// sampled in world space so the fog stays put while the camera moves through
// it. Vertex colour: r = sheet height 0..1 (bottom..top), a = sheet strength.
// The strip over the track is kept clear so the lanes and pits read as before.
Shader "Deadline4Sec/LowFog"
{
    Properties
    {
        _NoiseTex ("Noise (R, G)", 2D) = "gray" {}
        _Color ("Fog", Color) = (0.32, 0.16, 0.4, 1)
        _GlowColor ("Fog Glow", Color) = (0.85, 0.25, 0.65, 1)
        _Alpha ("Alpha", Range(0, 1)) = 0.7
        _Scale ("Clump Size", Float) = 26
        _Drift ("Drift (xy)", Vector) = (0.6, -1.2, 0, 0)
        _TrackX ("Track X", Float) = 0
        _ClearHalfWidth ("Clear Half Width", Float) = 4.4
        _ClearFade ("Clear Fade", Float) = 3
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent-50" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                half4 _Color, _GlowColor;
                float4 _Drift;
                float _Alpha, _Scale, _TrackX, _ClearHalfWidth, _ClearFade;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                half4 color : COLOR;
                float3 positionWS : TEXCOORD1;
                float fog : TEXCOORD2;
            };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.color = i.color;
                float2 world = p.positionWS.xz + _Drift.xy * _Time.y * (0.6 + i.color.r);
                // Each sheet its own offset, so the stack doesn't line up.
                o.uv.xy = world / _Scale + i.color.r * float2(0.37, 0.71);
                o.uv.zw = world / (_Scale * 2.7) - float2(0.21, 0.13) * i.color.r - _Time.y * 0.004;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv.xy).r;
                half b = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, i.uv.zw).g;
                half n = a * 0.55 + b * 0.45;
                // Lower sheets denser and calmer, upper ones thin wisps.
                half body = smoothstep(0.18 + i.color.r * 0.3, 0.75, n);
                half alpha = body * _Alpha * i.color.a;
                float side = abs(i.positionWS.x - _TrackX);
                alpha *= smoothstep(_ClearHalfWidth, _ClearHalfWidth + _ClearFade, side);
                // No hard cut where a sheet passes the camera.
                float dist = distance(i.positionWS, _WorldSpaceCameraPos);
                alpha *= saturate((dist - 3.0) / 10.0);
                half3 col = lerp(_Color.rgb, _GlowColor.rgb, saturate(b * b * 1.6) * (1 - i.color.r * 0.5));
                col = MixFog(col, i.fog);
                return half4(col, saturate(alpha));
            }
            ENDHLSL
        }
    }
}
