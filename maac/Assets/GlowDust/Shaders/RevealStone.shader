// Stage surface shader: stays black unless glow powder density exists near the surface.
// Density volume (Texture3D) is produced by PowderVolume.cs from particles + reveal sources.
Shader "GlowDust/RevealStone"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (0.62, 0.6, 0.66, 1)
        _BlockSize ("Block Size (x=width, y=height)", Vector) = (1.1, 0.55, 0, 0)
        _MortarWidth ("Mortar Width", Range(0.005, 0.2)) = 0.06
        _Variation ("Stone Variation", Range(0, 1)) = 0.5
        _DensityGain ("Density Gain", Float) = 0.5
        _Exposure ("Exposure", Float) = 2.2
        [HDR] _ColorDense ("Dense Glow Color", Color) = (0.62, 0.45, 1.0, 1)
        [HDR] _ColorThin ("Thin Glow Color", Color) = (0.2, 0.75, 1.0, 1)
        _RimStrength ("Rim Strength (faint outline)", Float) = 1.0
        _NearRadius ("Viewer Aura Radius", Float) = 2.5
        _NearIntensity ("Viewer Aura Intensity", Float) = 0.025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _BlockSize;
            float _MortarWidth;
            float _Variation;
            float _DensityGain;
            float _Exposure;
            half4 _ColorDense;
            half4 _ColorThin;
            float _RimStrength;
            float _NearRadius;
            float _NearIntensity;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "GlowDustReveal"
            Tags { "LightMode"="UniversalForward" }
            ZWrite On
            Cull Back

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "GlowDustCommon.hlsl"

            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 positionWS : TEXCOORD0; float3 normalWS : TEXCOORD1; };

            Varyings vert (Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS;
                o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                float3 N = normalize(i.normalWS);
                float3 P = i.positionWS;
                float3 V = normalize(GetWorldSpaceViewDir(P));

                float mortar, variation;
                GD_StonePattern(P, N, _BlockSize.xy, _MortarWidth, mortar, variation);
                float3 albedo = _BaseColor.rgb * lerp(1.0, 0.45 + variation * 1.1, _Variation);

                // Editor preview (not playing): show geometry dimly so the level can be edited.
                if (_GD_Enabled < 0.5)
                {
                    // editor / concept preview: simple key + sky light so the layout is readable
                    float3 Lk = normalize(float3(0.45, 0.8, -0.35));
                    float key = saturate(dot(N, Lk));
                    float sky = saturate(N.y * 0.5 + 0.5);
                    float3 lit = albedo * (0.22 + 0.35 * sky + 0.75 * key) * (1.0 - mortar * 0.45);
                    return half4(lit, 1);
                }

                // --- Powder density around this surface point ---
                float3 grad;
                float3 Q = P + N * (_GD_CellSize.x * 0.6);
                float d = GD_DensityAndGradient(Q, grad);
                float I = 1.0 - exp(-pow(max(d, 0.0), 0.65) * _DensityGain);   // 0..1 reveal intensity (pow<1 -> long soft tail)

                // three visibility tiers
                float faint  = smoothstep(0.025, 0.15, I);   // C: something is there
                float mid    = smoothstep(0.12, 0.45, I);    // B: outlines / block seams
                float strong = smoothstep(0.45, 0.82, I);    // A: full stone detail + shading

                // light direction = toward the powder (density gradient)
                float gl = length(grad);
                float3 L = gl > 1e-4 ? grad / gl : N;
                float ndl = saturate(dot(N, L) * 0.65 + 0.35);
                float rim = pow(1.0 - saturate(dot(N, V)), 2.5);

                float3 glow = lerp(_ColorThin.rgb, _ColorDense.rgb, saturate(I * 1.3));

                // C : silhouette glow + rim (no surface detail)
                float3 cFaint  = glow * (0.25 + rim * 0.6 * _RimStrength);
                // B : flat shading + glowing seams so you can read the layout
                float3 cMid    = glow * (0.30 + 0.35 * ndl + mortar * 0.55 + rim * 0.5 * _RimStrength);
                // A : real stone look lit by the powder
                float3 cStrong = albedo * glow * (0.35 + 1.5 * ndl) * (1.0 - mortar * 0.75)
                               + glow * (rim * 0.25 + mortar * 0.05);

                float3 col = cFaint * faint;
                col = lerp(col, cMid, mid);
                col = lerp(col, cStrong, strong);
                col *= _Exposure * (0.12 * faint + 0.95 * I * I);

                // tiny aura around the viewer (almost nothing)
                float dist = distance(P, _GD_PlayerPos.xyz);
                float aura = saturate(1.0 - dist / max(_NearRadius, 0.01));
                col += albedo * float3(0.6, 0.65, 1.0) * (aura * aura * _NearIntensity) * (1.0 - mortar * 0.5);

                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask R
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings  { float4 positionCS : SV_POSITION; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); return o; }
            half frag (Varyings i) : SV_Target { return 0; }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode"="DepthNormals" }
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; };
            struct Varyings  { float4 positionCS : SV_POSITION; float3 normalWS : TEXCOORD0; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.normalWS = TransformObjectToWorldNormal(v.normalOS); return o; }
            half4 frag (Varyings i) : SV_Target { return half4(normalize(i.normalWS), 0); }
            ENDHLSL
        }
    }
}
