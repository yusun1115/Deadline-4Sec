// Procedural gothic rail track for the runtime floor cubes. World-space Z tiling
// keeps every spawned floor seamless; lane X is measured from each floor's own
// center so rails line up with the three gameplay lanes.
Shader "Deadline4Sec/RailTrack"
{
    Properties
    {
        _BaseColor ("Stone", Color) = (0.11, 0.09, 0.14, 1)
        _StoneLight ("Stone Highlight", Color) = (0.2, 0.16, 0.25, 1)
        _SleeperColor ("Sleeper", Color) = (0.06, 0.05, 0.07, 1)
        _RailColor ("Rail", Color) = (0.62, 0.58, 0.7, 1)
        _GlowColor ("Glow", Color) = (1, 0.18, 0.5, 1)
        _LaneWidth ("Lane Width", Float) = 2.5
        _RailGauge ("Rail Half Gauge", Float) = 0.55
        _RailWidth ("Rail Width", Float) = 0.07
        _SleeperSpacing ("Sleeper Spacing", Float) = 1.1
        _EdgeGlow ("Edge Glow Width", Float) = 0.12
        _GlowPulse ("Glow Pulse Speed", Float) = 1.6
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _StoneLight, _SleeperColor, _RailColor, _GlowColor;
                float _LaneWidth, _RailGauge, _RailWidth, _SleeperSpacing, _EdgeGlow, _GlowPulse;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 centerWS : TEXCOORD2;
                float halfWidth : TEXCOORD3;
                float fog : TEXCOORD4;
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.centerWS = TransformObjectToWorld(float3(0, 0, 0));
                o.halfWidth = length(TransformObjectToWorldDir(float3(1, 0, 0), false)) * 0.5;
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453); }

            float Band(float x, float center, float width)
            {
                float d = abs(x - center);
                float aa = fwidth(x) + 1e-4;
                return 1.0 - smoothstep(width - aa, width + aa, d);
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 n = normalize(i.normalWS);
                float x = i.positionWS.x - i.centerWS.x;
                float z = i.positionWS.z;
                float pulse = 0.75 + 0.25 * sin(_Time.y * _GlowPulse + z * 0.08);

                // Stone slabs: staggered blocks with per-slab tone variation.
                float2 slab = float2(x / 1.4, z / 2.2);
                slab.x += floor(slab.y) * 0.5;
                float2 cell = floor(slab);
                float2 f = frac(slab);
                float grout = smoothstep(0.0, 0.04, f.x) * smoothstep(0.0, 0.04, 1 - f.x) *
                              smoothstep(0.0, 0.03, f.y) * smoothstep(0.0, 0.03, 1 - f.y);
                half3 col = lerp(_BaseColor.rgb, _StoneLight.rgb, Hash(cell) * 0.6);
                col *= lerp(0.45, 1.0, grout);

                float lane = round(x / _LaneWidth);
                float laneX = x - lane * _LaneWidth;
                bool onTrack = abs(lane) <= 1.0;

                // Sleepers under each lane's rails.
                float sleeperZ = frac(z / _SleeperSpacing);
                float sleeper = Band(sleeperZ, 0.5, 0.11) * Band(laneX, 0, _RailGauge + 0.25);
                if (onTrack)
                    col = lerp(col, _SleeperColor.rgb, sleeper * 0.85);

                // Twin steel rails with a bright top highlight.
                float rail = max(Band(laneX, -_RailGauge, _RailWidth), Band(laneX, _RailGauge, _RailWidth));
                float railTop = max(Band(laneX, -_RailGauge, _RailWidth * 0.35), Band(laneX, _RailGauge, _RailWidth * 0.35));
                if (onTrack)
                {
                    col = lerp(col, _RailColor.rgb * 0.55, rail);
                    col = lerp(col, _RailColor.rgb, railTop);
                }

                // Lighting: main light with shadows, plus a purple ambient floor.
                float4 shadowCoord = TransformWorldToShadowCoord(i.positionWS);
                Light light = GetMainLight(shadowCoord);
                half ndl = saturate(dot(n, light.direction));
                half3 lit = col * (light.color * ndl * light.shadowAttenuation * 0.9 + half3(0.35, 0.28, 0.45));
                lit += _RailColor.rgb * railTop * 0.25 * light.shadowAttenuation;

                // Pink glow seams: track edges and between lanes; sides of the slab glow too.
                float edge = Band(abs(x), i.halfWidth - _EdgeGlow, _EdgeGlow * 0.6);
                float divider = Band(abs(x), _LaneWidth * 0.5, 0.025) * (1 - Band(frac(z / 3.0), 0.5, 0.2));
                half3 glow = _GlowColor.rgb * (edge * 1.8 + divider * 0.9) * pulse;
                if (n.y < 0.5)
                    lit = col * 0.3 + _GlowColor.rgb * 0.35 * pulse;

                half3 final = lit + glow * step(0.5, n.y);
                final = MixFog(final, i.fog);
                return half4(final, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On
            ColorMask 0
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            float4 Vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS : POSITION; };
            float4 Vert(Attributes i) : SV_POSITION { return TransformObjectToHClip(i.positionOS.xyz); }
            half4 Frag() : SV_Target { return 0; }
            ENDHLSL
        }
    }
}
