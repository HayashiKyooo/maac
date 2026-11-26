// Soft additive glow for powder particles, sparks, wind motes and trails. No texture needed.
Shader "GlowDust/GlowAdditive"
{
    Properties
    {
        _Intensity ("Intensity", Float) = 3
        _Softness ("Softness", Float) = 2
        _RadialX ("Radial X (1=particle, 0=trail)", Float) = 1
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Blend One One
        ZWrite Off
        Cull Off
        Pass
        {
            Tags { "LightMode"="SRPDefaultUnlit" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Intensity;
                float _Softness;
                float _RadialX;
            CBUFFER_END
            struct Attributes { float4 positionOS : POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            struct Varyings  { float4 positionCS : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; };
            Varyings vert (Attributes v) { Varyings o; o.positionCS = TransformObjectToHClip(v.positionOS.xyz); o.color = v.color; o.uv = v.uv; return o; }
            half4 frag (Varyings i) : SV_Target
            {
                float2 c = i.uv * 2.0 - 1.0;
                float r2 = c.x * c.x * _RadialX + c.y * c.y;
                float a = pow(saturate(1.0 - r2), _Softness);
                return half4(i.color.rgb * i.color.a * a * _Intensity, 0);
            }
            ENDHLSL
        }
    }
}
