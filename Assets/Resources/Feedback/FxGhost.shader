// Additive afterimage for Reaper Rush: a faint body with a bright rim, tinted
// and faded through _Color (drawn with a property block per ghost).
Shader "Deadline4Sec/FxGhost"
{
    Properties
    {
        _Color ("Color (A = strength)", Color) = (1, 0.3, 0.7, 1)
        _RimPower ("Rim Power", Range(0.5, 6)) = 2
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One One
            ZWrite Off
            Cull Back
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _RimPower;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; float3 viewWS : TEXCOORD1; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                o.positionCS = TransformWorldToHClip(positionWS);
                o.normalWS = TransformObjectToWorldNormal(i.normalOS);
                o.viewWS = GetWorldSpaceViewDir(positionWS);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float rim = 1 - saturate(dot(normalize(i.normalWS), normalize(i.viewWS)));
                float glow = 0.18 + pow(rim, _RimPower) * 1.3;
                return half4(_Color.rgb * glow * _Color.a, 1);
            }
            ENDHLSL
        }
    }
}
