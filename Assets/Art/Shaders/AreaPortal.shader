// The sheet of light at the end of an area transition tunnel. Vertex colour is
// the glow gradient; vertex alpha is 0 at the centre and 1 at the rim, so raising
// _Open opens a hole from the centre outward until nothing is left.
Shader "Deadline4Sec/AreaPortal"
{
    Properties
    {
        _Tint ("Tint", Color) = (1, 0.2, 0.55, 1)
        _Glow ("Emissive Boost", Float) = 2.2
        _Open ("Open", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Tint;
                float _Glow, _Open;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Open 0: every point opaque. Open 1: every point clear. A soft edge between.
                half alpha = saturate((i.color.a - _Open * 1.25 + 0.25) / 0.25);
                return half4(i.color.rgb * _Tint.rgb * _Glow, alpha);
            }
            ENDHLSL
        }
    }
}
