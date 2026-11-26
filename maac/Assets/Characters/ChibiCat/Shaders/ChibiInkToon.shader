// Ink-and-wash toon shader for ChibiCat (URP).
// Pass 1: flat 2-band cel shading with a pale cool shadow tint (no specular), light color mostly
//         ignored so whites stay paper-white like the drawing.
// Pass 2: inverted-hull ink outline. Width (meters) * vertex color R (0 = no line: eyes, nose,
//         whiskers), with low-frequency jitter so the line breathes like a brush stroke.
Shader "ChibiCat/InkToon"
{
    Properties
    {
        [MainTexture] _BaseMap ("Base Map", 2D) = "white" {}
        [MainColor] _BaseColor ("Base Color", Color) = (1,1,1,1)
        _ShadowColor ("Shadow Tint", Color) = (0.9,0.91,0.97,1)
        _ShadowThreshold ("Shadow Threshold", Range(-1,1)) = -0.15
        _ShadowSoftness ("Shadow Softness", Range(0.001,0.5)) = 0.03
        _ReceiveShadowAmount ("Cast Shadow Strength", Range(0,1)) = 0.0
        _LightColorInfluence ("Light Color Influence", Range(0,1)) = 0.15
        _Brightness ("Brightness", Range(0.5,1.5)) = 1.0
        _RimColor ("Rim Color", Color) = (1,1,1,1)
        _RimStrength ("Rim Strength", Range(0,1)) = 0.0
        [Header(Outline)]
        _OutlineColor ("Outline Color", Color) = (0.035,0.035,0.04,1)
        _OutlineWidth ("Outline Width (m)", Range(0,0.03)) = 0.011
        _OutlineJitter ("Brush Jitter", Range(0,1)) = 0.4
        _OutlineNoiseScale ("Brush Noise Scale", Float) = 16
        _OutlineUseVertexColor ("Use Vertex Color R as width", Float) = 1
        _OutlineScreenComp ("Distance Compensation", Range(0,1)) = 0.6
        _OutlineZOffset ("Outline Depth Push (m)", Range(0,0.08)) = 0.025
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            half4 _ShadowColor;
            half _ShadowThreshold;
            half _ShadowSoftness;
            half _ReceiveShadowAmount;
            half _LightColorInfluence;
            half _Brightness;
            half4 _RimColor;
            half _RimStrength;
            half4 _OutlineColor;
            float _OutlineWidth;
            half _OutlineJitter;
            float _OutlineNoiseScale;
            half _OutlineUseVertexColor;
            half _OutlineScreenComp;
            float _OutlineZOffset;
        CBUFFER_END
        TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);

        float hash31(float3 p) { p = frac(p * 0.1031); p += dot(p, p.yzx + 33.33); return frac((p.x + p.y) * p.z); }
        float vnoise(float3 p)
        {
            float3 i = floor(p); float3 f = frac(p); f = f * f * (3 - 2 * f);
            float n000 = hash31(i), n100 = hash31(i + float3(1,0,0)), n010 = hash31(i + float3(0,1,0)), n110 = hash31(i + float3(1,1,0));
            float n001 = hash31(i + float3(0,0,1)), n101 = hash31(i + float3(1,0,1)), n011 = hash31(i + float3(0,1,1)), n111 = hash31(i + float3(1,1,1));
            return lerp(lerp(lerp(n000,n100,f.x), lerp(n010,n110,f.x), f.y), lerp(lerp(n001,n101,f.x), lerp(n011,n111,f.x), f.y), f.z);
        }
        ENDHLSL

        Pass
        {
            Name "ForwardToon"
            Tags { "LightMode"="UniversalForward" }
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float2 uv : TEXCOORD0; };
            struct Varyings { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; float3 normalWS : TEXCOORD1; float3 positionWS : TEXCOORD2; float fog : TEXCOORD3; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                VertexPositionInputs p = GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS = p.positionCS; o.positionWS = p.positionWS;
                o.normalWS = TransformObjectToWorldNormal(v.normalOS);
                o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
                o.fog = ComputeFogFactor(p.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target
            {
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, i.uv) * _BaseColor;
                float3 n = normalize(i.normalWS);
                float4 sc = TransformWorldToShadowCoord(i.positionWS);
                Light L = GetMainLight(sc);
                half ndl = dot(n, L.direction);
                half lit = smoothstep(_ShadowThreshold - _ShadowSoftness, _ShadowThreshold + _ShadowSoftness, ndl);
                half shadowAtt = lerp(1, L.shadowAttenuation, _ReceiveShadowAmount);
                lit *= smoothstep(0.45, 0.55, shadowAtt);
                half3 lightCol = L.color / max(max(L.color.r, max(L.color.g, L.color.b)), 1e-3);
                half3 tint = lerp(half3(1,1,1), lightCol, _LightColorInfluence);
                half3 band = lerp(_ShadowColor.rgb, half3(1,1,1), lit);
                half3 col = albedo.rgb * band * tint * _Brightness;
                float3 V = normalize(GetWorldSpaceViewDir(i.positionWS));
                half rim = smoothstep(0.65, 0.75, 1 - saturate(dot(n, V))) * _RimStrength * lit;
                col += _RimColor.rgb * rim;
                col = MixFog(col, i.fog);
                return half4(col, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "InkOutline"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Cull Front
            ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            struct Attributes { float4 positionOS : POSITION; float3 normalOS : NORMAL; float4 color : COLOR; };
            struct Varyings { float4 positionCS : SV_POSITION; float fog : TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;
                float w = _OutlineWidth * lerp(1.0, v.color.r, _OutlineUseVertexColor);
                float3 posWS = TransformObjectToWorld(v.positionOS.xyz);
                float n1 = vnoise(posWS * _OutlineNoiseScale);
                float n2 = vnoise(posWS * _OutlineNoiseScale * 2.7 + 17.0);
                w *= 1.0 + _OutlineJitter * ((n1 * 0.7 + n2 * 0.3) * 2.0 - 1.0);
                float dist = distance(posWS, GetCameraPositionWS());
                w *= lerp(1.0, clamp(dist / 2.5, 0.6, 2.5), _OutlineScreenComp);
                float3 nWS = TransformObjectToWorldNormal(v.normalOS);
                posWS += nWS * w;
                // push the hull away from the camera so it cannot poke through creases (leg/arm roots)
                posWS += normalize(posWS - GetCameraPositionWS()) * _OutlineZOffset;
                o.positionCS = TransformWorldToHClip(posWS);
                o.fog = ComputeFogFactor(o.positionCS.z);
                return o;
            }
            half4 frag(Varyings i) : SV_Target { return half4(MixFog(_OutlineColor.rgb, i.fog), 1); }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
