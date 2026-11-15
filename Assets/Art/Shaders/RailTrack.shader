// Procedural gothic rail track for the runtime floor cubes. World-space Z tiling
// keeps every spawned floor seamless; lane X is measured from each floor's own
// center so rails line up with the three gameplay lanes.
// Visual upgrade (2026-11-15): a per-area surface pattern between the rails
// (Railway: neon panel seams and energy running along the edges; Cathedral:
// dark tiles, cracks and red light seams; Clockwork: riveted plates, gold
// circuit traces and etched gears). Rails, lane dividers and edges stay on
// top and unchanged, so the lanes read as before.
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
        [KeywordEnum(Railway, Cathedral, Clockwork)] _Pattern ("Floor Pattern", Float) = 0
        _PatternColor ("Pattern Glow", Color) = (0.75, 0.25, 1, 1)
        _PatternStrength ("Pattern Strength", Range(0, 2)) = 0.6
        _FlowSpeed ("Energy Flow Speed", Float) = 0.9
        _Ambient ("Ambient", Color) = (0.35, 0.28, 0.45, 1)
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
            #pragma shader_feature_local_fragment _ _PATTERN_CATHEDRAL _PATTERN_CLOCKWORK
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _StoneLight, _SleeperColor, _RailColor, _GlowColor, _PatternColor, _Ambient;
                float _LaneWidth, _RailGauge, _RailWidth, _SleeperSpacing, _EdgeGlow, _GlowPulse, _PatternStrength, _FlowSpeed;
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

                float lane = round(x / _LaneWidth);
                float laneX = x - lane * _LaneWidth;
                bool onTrack = abs(lane) <= 1.0;
                // Where the pattern may show: not under the rails and sleepers.
                float open = smoothstep(_RailGauge + 0.12, _RailGauge + 0.3, abs(laneX));
                half3 patternGlow = 0;

            #if defined(_PATTERN_CATHEDRAL)
                // Dark square tiles, alternating tone; some cracked, some with red light in the seams.
                float2 slab = float2(x / 1.25, z / 1.25);
                float2 cell = floor(slab);
                float2 f = frac(slab);
                float grout = smoothstep(0.0, 0.05, f.x) * smoothstep(0.0, 0.05, 1 - f.x) *
                              smoothstep(0.0, 0.05, f.y) * smoothstep(0.0, 0.05, 1 - f.y);
                float h = Hash(cell);
                half3 col = lerp(_BaseColor.rgb, _StoneLight.rgb, h * 0.5);
                col *= lerp(0.72, 1.0, fmod(abs(cell.x + cell.y), 2.0));
                col *= lerp(0.4, 1.0, grout);
                float seam = (1 - grout) * step(0.78, Hash(cell + 7.1));
                float2 dir = float2(cos(h * 6.283), sin(h * 6.283));
                float2 q = f - 0.5;
                float wobble = 0.05 * sin(dot(q, dir) * 22.0 + h * 9.0);
                float crack = Band(dot(q, float2(-dir.y, dir.x)) + wobble, 0, 0.012) *
                              step(0.62, h) * (1 - smoothstep(0.32, 0.5, abs(dot(q, dir))));
                col *= 1 - crack * 0.7;
                float ember = 0.65 + 0.35 * sin(_Time.y * 1.3 + h * 20.0);
                patternGlow = _PatternColor.rgb * (seam * 0.55 + crack * 0.9) * ember * open;
            #elif defined(_PATTERN_CLOCKWORK)
                // Riveted plates (one per lane), gold circuit traces, an etched gear here and there.
                float2 slab = float2(x / _LaneWidth + 0.5, z / 2.5);
                float2 cell = floor(slab);
                float2 f = frac(slab);
                float grout = smoothstep(0.0, 0.025, f.x) * smoothstep(0.0, 0.025, 1 - f.x) *
                              smoothstep(0.0, 0.03, f.y) * smoothstep(0.0, 0.03, 1 - f.y);
                float h = Hash(cell);
                half3 col = lerp(_BaseColor.rgb, _StoneLight.rgb, 0.25 + h * 0.35);
                col *= lerp(0.5, 1.0, grout);
                float2 c = abs(f - 0.5);
                float rivet = 1 - smoothstep(0.03, 0.045, length(c - float2(0.42, 0.42)));
                col += _StoneLight.rgb * rivet * 0.8;
                // An L-shaped trace from the plate edge to a node, flowing.
                float ty = 0.2 + 0.6 * Hash(cell + 3.3), tx = 0.12 + 0.2 * h;
                float trace = max(Band(f.y, ty, 0.012) * step(f.x, 1 - tx) * step(tx, f.x),
                                  Band(f.x, tx, 0.012 * _LaneWidth / 2.5) * step(min(ty, 0.5), f.y) * step(f.y, max(ty, 0.5)));
                float node = 1 - smoothstep(0.025, 0.04, length((f - float2(tx, 0.5)) * float2(_LaneWidth / 2.5, 1)));
                float flow = 0.45 + 0.55 * pow(frac(f.x * 0.7 + f.y * 0.5 - _Time.y * _FlowSpeed * 0.5 + h), 3);
                float circuit = (trace * flow + node) * step(0.35, h);
                // Gear etch.
                float2 g = (f - 0.5) * float2(_LaneWidth / 2.5, 1);
                float r = length(g);
                float ang = atan2(g.y, g.x) + _Time.y * 0.6 * (h - 0.5);
                float gear = (Band(r, 0.24, 0.022) + Band(r, 0.3, 0.035) * step(0.0, sin(ang * 10.0)) + Band(r, 0.07, 0.03))
                             * step(0.86, Hash(cell + 11.7));
                col = lerp(col, col * 0.55, gear);
                patternGlow = _PatternColor.rgb * (circuit * 0.8 + gear * 0.45) * open;
            #else
                // Stone slabs: staggered blocks with per-slab tone variation.
                float2 slab = float2(x / 1.4, z / 2.2);
                slab.x += floor(slab.y) * 0.5;
                float2 cell = floor(slab);
                float2 f = frac(slab);
                float grout = smoothstep(0.0, 0.04, f.x) * smoothstep(0.0, 0.04, 1 - f.x) *
                              smoothstep(0.0, 0.03, f.y) * smoothstep(0.0, 0.03, 1 - f.y);
                half3 col = lerp(_BaseColor.rgb, _StoneLight.rgb, Hash(cell) * 0.6);
                col *= lerp(0.45, 1.0, grout);
                // Neon panel seams across the track every third slab row.
                float panel = Band(frac(z / 6.6), 0.0, 0.012);
                patternGlow = _PatternColor.rgb * panel * 0.45 * open;
            #endif

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
                half3 lit = col * (light.color * ndl * light.shadowAttenuation * 0.9 + _Ambient.rgb);
                lit += _RailColor.rgb * railTop * 0.25 * light.shadowAttenuation;

                // Pink glow seams: track edges and between lanes; sides of the slab glow too.
                float edge = Band(abs(x), i.halfWidth - _EdgeGlow, _EdgeGlow * 0.6);
                float divider = Band(abs(x), _LaneWidth * 0.5, 0.025) * (1 - Band(frac(z / 3.0), 0.5, 0.2));
                // Energy running along the edges towards the runner.
                float run = pow(frac(z * 0.035 + _Time.y * _FlowSpeed), 6.0);
                half3 glow = _GlowColor.rgb * (edge * (1.8 + run * 1.6) + divider * 0.9) * pulse;
                glow += patternGlow * _PatternStrength * (onTrack ? 1.0 : 0.6);
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
