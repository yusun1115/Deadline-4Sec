// Textured soft sprite for the weapon skin effects (2026-11-21): stars, ice
// crystals, shards, 0/1 digits, pixels, drops, trail strips. The texture is an
// alpha shape (white); the vertex colour tints it. Blend from the material:
// additive (One One, default) for light, alpha (SrcAlpha OneMinusSrcAlpha) for
// the dark smoke of ECLIPSE.
Shader "Deadline4Sec/FxSprite"
{
    Properties
    {
        _MainTex ("Shape (A)", 2D) = "white" {}
        _Boost ("Brightness", Range(0, 4)) = 1.4
        [HideInInspector] _SrcBlend ("Src", Float) = 1
        [HideInInspector] _DstBlend ("Dst", Float) = 1
        [HideInInspector] _Additive ("Additive", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+20" "RenderType"="Transparent" "IgnoreProjector"="True" }
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
            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Boost, _Additive, _SrcBlend, _DstBlend;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.color = i.color;
                o.uv = TRANSFORM_TEX(i.uv, _MainTex);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a * i.color.a;
                // Additive: light only (alpha unused). Alpha: the colour itself, see-through by a.
                half3 add = i.color.rgb * a * _Boost;
                return _Additive > 0.5 ? half4(add, 1) : half4(i.color.rgb, a);
            }
            ENDHLSL
        }
    }
}
