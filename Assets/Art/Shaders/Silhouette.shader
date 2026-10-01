// Unlit fogged silhouettes for background architecture. Vertex color carries
// the tint; alpha > 0.5 marks emissive window/banner geometry that ignores fog.
Shader "Deadline4Sec/Silhouette"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _HazeColor ("Base Haze", Color) = (0.42, 0.3, 0.5, 1)
        _HazeHeight ("Haze Height", Float) = 6
        _HazeBottom ("Haze Bottom", Float) = -12
        _Glow ("Emissive Boost", Float) = 2.2
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
                float _HazeHeight, _HazeBottom, _Glow;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float y : TEXCOORD0; float fog : TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(i.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.color = i.color;
                o.y = p.positionWS.y;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half emissive = step(0.5, i.color.a);
                half3 col = i.color.rgb * _Tint.rgb;
                // Low parts sink into the cloud sea.
                half haze = saturate((_HazeHeight - i.y) / (_HazeHeight - _HazeBottom));
                col = lerp(col, _HazeColor.rgb, haze * haze * (1 - emissive));
                half3 lit = MixFog(col, i.fog);
                half3 glow = lerp(lit, col * _Glow, 0.7);
                return half4(lerp(lit, glow, emissive), 1);
            }
            ENDHLSL
        }
    }
}
