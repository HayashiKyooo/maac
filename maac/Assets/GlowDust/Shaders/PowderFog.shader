// Volumetric glow smoke: ray-marches the powder density volume (same data that reveals the stage).
// Drawn on a box that covers the PowderVolume (Cull Front so it works with the camera inside).
// Stops at opaque geometry using the camera depth texture.
Shader "GlowDust/PowderFog"
{
    Properties
    {
        [HDR] _ColorDense ("Dense Smoke Color", Color) = (0.6, 0.38, 1.0, 1)
        [HDR] _ColorThin ("Thin Smoke Color", Color) = (0.2, 0.7, 1.0, 1)
        _Intensity ("Intensity", Float) = 0.07
        _DensityPower ("Density Power (<1 = longer soft tail)", Range(0.3, 1.5)) = 0.7
        _DenseAt ("Density considered 'dense'", Float) = 8
        _NoiseScale ("Smoke Noise Scale", Float) = 0.8
        _NoiseStrength ("Smoke Noise Strength", Range(0, 1)) = 0.9
        _NoiseSpeed ("Smoke Noise Speed", Float) = 0.35
        _Steps ("Ray Steps", Range(8, 64)) = 40
        _MaxDistance ("Max March Distance", Float) = 32
    }
    SubShader
    {
        Tags { "Queue"="Transparent-50" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        ZTest Always
        Cull Front

        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "GlowDustCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _ColorDense;
                half4 _ColorThin;
                float _Intensity;
                float _DensityPower;
                float _DenseAt;
                float _NoiseScale;
                float _NoiseStrength;
                float _NoiseSpeed;
                float _Steps;
                float _MaxDistance;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                return o;
            }

            // domain-warped 2-octave value noise -> billowy smoke wisps (0..1)
            float SmokeNoise(float3 p, float t)
            {
                float3 q = p * _NoiseScale + float3(t * 0.31, t * 0.17, t * -0.23);
                float3 warp = float3(GD_ValueNoise(q * 0.5 + 3.1), GD_ValueNoise(q * 0.5 + 11.7), GD_ValueNoise(q * 0.5 + 23.3)) - 0.5;
                q += warp * 1.6;
                float n = GD_ValueNoise(q) * 0.62;
                n += GD_ValueNoise(q * 2.27 + float3(-t * 0.4, t * 0.25, t * 0.1) + 17.0) * 0.38;
                return n;
            }

            half4 frag (Varyings i) : SV_Target
            {
                if (_GD_Enabled < 0.5) return 0;

                float3 ro = GetCameraPositionWS();
                float3 rd = normalize(i.positionWS - ro);

                // ray / volume box intersection
                float3 bmin = _GD_VolumeOrigin.xyz;
                float3 bmax = bmin + 1.0 / _GD_VolumeInvSize.xyz;
                float3 inv = 1.0 / (abs(rd) > 1e-5 ? rd : 1e-5);
                float3 ta = (bmin - ro) * inv;
                float3 tb = (bmax - ro) * inv;
                float3 tmin3 = min(ta, tb), tmax3 = max(ta, tb);
                float t0 = max(max(tmin3.x, tmin3.y), max(tmin3.z, 0.0));
                float t1 = min(min(tmax3.x, tmax3.y), tmax3.z);

                // stop at opaque geometry
                float2 uv = i.positionCS.xy / _ScaledScreenParams.xy;
                float eye = LinearEyeDepth(SampleSceneDepth(uv), _ZBufferParams);
                float3 camFwd = -UNITY_MATRIX_V[2].xyz;
                float tScene = eye / max(dot(rd, camFwd), 1e-3);
                t1 = min(t1, min(tScene, t0 + _MaxDistance));
                if (t1 <= t0) return 0;

                int steps = (int)_Steps;
                float dt = (t1 - t0) / steps;
                float jitter = GD_Hash13(float3(i.positionCS.xy, frac(_Time.y * 7.0) * 61.0));
                float t = t0 + dt * jitter;
                float time = _Time.y * _NoiseSpeed;

                float3 acc = 0;
                [loop]
                for (int s = 0; s < steps; s++)
                {
                    float3 p = ro + rd * t;
                    float d = GD_SampleDensity(p);
                    if (d > 0.002)
                    {
                        float n = SmokeNoise(p, time);
                        // noise carves wisps; dense cores stay more solid
                        float wisp = saturate(n * 2.4 - 0.7);
                        wisp *= wisp;
                        float carve = lerp(1.0, wisp * 1.8, _NoiseStrength * lerp(1.0, 0.6, saturate(d / (_DenseAt * 2.0))));
                        float dd = pow(d, _DensityPower) * carve;
                        float3 c = lerp(_ColorThin.rgb, _ColorDense.rgb, saturate(d / _DenseAt));
                        acc += c * dd * dt;
                    }
                    t += dt;
                }
                float3 col = acc * _Intensity;
                col = col / (1.0 + max(col.r, max(col.g, col.b)) * 0.35); // soft limit, avoid white-out
                return half4(col, 0);
            }
            ENDHLSL
        }
    }
}
