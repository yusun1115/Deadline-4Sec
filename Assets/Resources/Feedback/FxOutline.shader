// Inverted-hull outline drawn as an extra material on enemies so they read
// against the dark track. Width is in world units (meters).
Shader "Deadline4Sec/FxOutline"
{
    Properties
    {
        _Color ("Outline", Color) = (1, 0.35, 0.7, 1)
        _Width ("Width (m)", Range(0, 0.1)) = 0.035
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+5" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Width;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };
            Varyings Vert(Attributes i)
            {
                Varyings o;
                // World-space thickness: thin at a distance, so far enemies keep
                // their shape instead of turning into pink blobs.
                float3 positionWS = TransformObjectToWorld(i.positionOS.xyz);
                float3 normalWS = normalize(TransformObjectToWorldNormal(i.normalOS));
                float4 clip = TransformWorldToHClip(positionWS + normalWS * _Width);
                o.positionCS = clip;
                o.fog = ComputeFogFactor(clip.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                // Outlines fade into fog less than the body so far enemies still read.
                return half4(MixFog(_Color.rgb, i.fog * 0.5 + 0.5), 1);
            }
            ENDHLSL
        }
    }
}
