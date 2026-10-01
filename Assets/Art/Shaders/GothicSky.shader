// Procedural night sky for the Deadline:4sec afterlife: violet gradient, a huge
// pale moon with halo, and drifting cloud bands near the horizon.
Shader "Deadline4Sec/GothicSky"
{
    Properties
    {
        _TopColor ("Zenith", Color) = (0.05, 0.03, 0.09, 1)
        _MidColor ("Mid Sky", Color) = (0.16, 0.09, 0.22, 1)
        _HorizonColor ("Horizon Haze", Color) = (0.55, 0.42, 0.62, 1)
        _CloudColor ("Clouds", Color) = (0.42, 0.3, 0.5, 1)
        _MoonColor ("Moon", Color) = (1, 0.93, 0.92, 1)
        _MoonGlow ("Moon Glow", Color) = (0.95, 0.55, 0.75, 1)
        _MoonDir ("Moon Direction", Vector) = (0.25, 0.32, 1, 0)
        _MoonSize ("Moon Size", Range(0.01, 0.4)) = 0.16
        _CloudSpeed ("Cloud Speed", Float) = 0.01
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TopColor, _MidColor, _HorizonColor, _CloudColor, _MoonColor, _MoonGlow;
                float4 _MoonDir;
                float _MoonSize, _CloudSpeed;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 dir : TEXCOORD0; };

            Varyings Vert(Attributes i)
            {
                Varyings o;
                o.positionCS = TransformObjectToHClip(i.positionOS.xyz);
                o.dir = i.positionOS.xyz;
                return o;
            }

            float Hash(float2 p) { return frac(sin(dot(p, float2(41.3, 289.1))) * 15731.743); }
            float Noise(float2 p)
            {
                float2 i = floor(p), f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x),
                            lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }
            float Fbm(float2 p)
            {
                float v = 0, a = 0.5;
                for (int k = 0; k < 4; k++) { v += Noise(p) * a; p *= 2.03; a *= 0.5; }
                return v;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                float3 d = normalize(i.dir);
                float h = d.y;
                half3 col = lerp(_HorizonColor.rgb, _MidColor.rgb, smoothstep(-0.02, 0.25, h));
                col = lerp(col, _TopColor.rgb, smoothstep(0.25, 0.85, h));
                col = lerp(col, _HorizonColor.rgb * 0.6, smoothstep(0.0, -0.3, h));

                // Moon disk, soft limb, faint craters and a pink halo.
                float3 md = normalize(_MoonDir.xyz);
                float cosA = dot(d, md);
                float ang = acos(saturate(cosA));
                float disk = 1 - smoothstep(_MoonSize * 0.97, _MoonSize, ang);
                float3 tangent = normalize(cross(md, float3(0, 1, 0)));
                float3 bitangent = cross(tangent, md);
                float2 moonUV = float2(dot(d, tangent), dot(d, bitangent)) / _MoonSize;
                float craters = Fbm(moonUV * 3.0 + 7.0);
                half3 moon = _MoonColor.rgb * (0.82 + 0.18 * craters);
                float halo = exp(-ang / (_MoonSize * 1.6)) * 0.55 + exp(-ang / (_MoonSize * 6)) * 0.25;
                col += _MoonGlow.rgb * halo;
                col = lerp(col, moon, disk);

                // Cloud bands hugging the horizon, drifting slowly; they pass in front of the moon.
                float2 cuv = d.xz / max(h + 0.25, 0.08);
                float clouds = Fbm(cuv * 0.9 + float2(_Time.y * _CloudSpeed, 0));
                clouds = smoothstep(0.45, 0.8, clouds) * smoothstep(0.55, 0.05, h) * smoothstep(-0.1, 0.03, h);
                half3 cloudCol = _CloudColor.rgb + _MoonGlow.rgb * halo * 0.6;
                col = lerp(col, cloudCol, clouds * 0.85);

                // Sparse stars high up.
                float2 sp = d.xz / (abs(h) + 0.3) * 120.0;
                float star = step(0.997, Hash(floor(sp))) * smoothstep(0.3, 0.7, h) * (1 - disk);
                col += star * 0.6;
                return half4(col, 1);
            }
            ENDHLSL
        }
    }
}
