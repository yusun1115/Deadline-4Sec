// Full-screen rotating sunburst behind the gift chest (UI Image material).
// Rays alternate hot pink / violet, brighten toward the center and darken at
// the edges. _Burst flashes the whole field when a box opens.
Shader "Deadline4Sec/UISunburst"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite", 2D) = "white" {}
        _ColorA ("Ray A", Color) = (0.95, 0.2, 0.55, 1)
        _ColorB ("Ray B", Color) = (0.32, 0.12, 0.55, 1)
        _Core ("Core", Color) = (1, 0.85, 0.95, 1)
        _Rays ("Ray Count", Float) = 14
        _Speed ("Spin Speed", Float) = 0.12
        _Burst ("Burst", Range(0, 1)) = 0
        _Aspect ("Height / Width", Float) = 1.78
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" "PreviewType"="Plane" }
        Cull Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _ColorA, _ColorB, _Core;
                float _Rays, _Speed, _Burst, _Aspect;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; half4 color : COLOR; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.uv = i.uv;
                o.color = i.color;
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv - float2(0.5, 0.55);
                p.y *= _Aspect;
                float r = length(p);
                float a = atan2(p.y, p.x) / 6.2831853 + 0.5 + _Time.y * _Speed;
                float ray = smoothstep(0.42, 0.58, frac(a * _Rays));
                half3 col = lerp(_ColorB.rgb, _ColorA.rgb, ray);
                half core = saturate(1 - r * 1.6);
                col = lerp(col, _Core.rgb, core * core * (0.7 + _Burst * 0.3));
                col *= lerp(1, 0.35, saturate(r * 0.9));
                col += _Core.rgb * _Burst * 0.6;
                return half4(col, i.color.a);
            }
            ENDHLSL
        }
    }
}
