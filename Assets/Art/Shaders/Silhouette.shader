// Unlit fogged silhouettes for background architecture. Vertex color carries
// the tint; alpha > 0.5 marks emissive window/banner geometry that ignores fog.
// Visual upgrade (2026-11-15): each area lights its background with a side
// light and a back light (globals set by AreaAtmosphere), and a material can
// take less fog (_FogAmount: landmark, far layer).
Shader "Deadline4Sec/Silhouette"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Base Haze", Color) = (0.42, 0.3, 0.5, 1)
        _HazeHeight ("Haze Height", Float) = 6
        _HazeBottom ("Haze Bottom", Float) = -12
        _Glow ("Emissive Boost", Float) = 2.2
        _FogAmount ("Fog Amount", Range(0, 1)) = 1
        _LightAmount ("Area Light Amount", Range(0, 2)) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+10" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint, _HazeColor;
                float _HazeHeight, _HazeBottom, _Glow, _FogAmount, _LightAmount;
            CBUFFER_END
            half4 _AreaSideLight, _AreaBackLight;
            float4 _AreaSideDir;
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float y : TEXCOORD0; float fog : TEXCOORD1; half3 normalWS : TEXCOORD2; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.color = i.color;
                o.y = p.positionWS.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half emissive = step(0.5, i.color.a);
                half3 col = i.color.rgb * _Tint.rgb;
                // Area light: a coloured side light on the faces turned to it,
                // a back light catching the tops against the sky.
                half3 n = normalize(i.normalWS);
                half side = saturate(dot(n, _AreaSideDir.xyz));
                half top = saturate(n.y);
                half3 light = _AreaSideLight.rgb * side * 0.55 + _AreaBackLight.rgb * top * 0.45;
                col += (col * 2.5 + 0.04) * light * _LightAmount * (1 - emissive);
                // Low parts sink into the cloud sea.
                half haze = saturate((_HazeHeight - i.y) / (_HazeHeight - _HazeBottom));
                col = lerp(col, _HazeColor.rgb, haze * haze * (1 - emissive));
                half3 lit = lerp(col, MixFog(col, i.fog), _FogAmount);
                half3 glow = lerp(lit, col * _Glow, 0.7);
                return half4(lerp(lit, glow, emissive), 1);
            }
            ENDHLSL
        }
    }
}
