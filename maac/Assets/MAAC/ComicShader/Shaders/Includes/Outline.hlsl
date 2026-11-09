#ifndef COMIC_OUTLINE_INCLUDED
#define COMIC_OUTLINE_INCLUDED

// ---------------------------------------------------------------------------------
// Outline.hlsl  -  inverted hull outline pass (LightMode = SRPDefaultUnlit)
// The back faces of the mesh are pushed outwards along the normal IN SCREEN SPACE, so the
// line width is specified in pixels (@1080p, resolution independent) and does not depend
// on the object's scale. "Distance-based Width" blends towards a width that shrinks with
// camera distance (perspective-like ink weight). Disabled materials skip this pass entirely
// (Material.SetShaderPassEnabled), so it costs nothing when off.
// ---------------------------------------------------------------------------------

#include "ComicInput.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct OutlineAttributes
{
    float4 positionOS     : POSITION;
    float3 normalOS       : NORMAL;
    float2 texcoord       : TEXCOORD0;
    float3 smoothNormalOS : TEXCOORD3;   // optional, written by "Bake Smoothed Outline Normals"
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct OutlineVaryings
{
    float4 positionCS : SV_POSITION;
    float2 uv         : TEXCOORD0;
    float3 positionWS : TEXCOORD1;
    half   fogFactor  : TEXCOORD2;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

OutlineVaryings ComicOutlineVertex(OutlineAttributes input)
{
    OutlineVaryings output = (OutlineVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    // normal source (uniform branch): smoothed UV3 normal if requested and present
    float3 normalOS = input.normalOS;
    if (_OutlineNormalSource > 0.5 && dot(input.smoothNormalOS, input.smoothNormalOS) > 1e-6)
        normalOS = input.smoothNormalOS;

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(normalOS);

    // optional push-back in view space to hide the hull behind concave front geometry
    float3 positionVS = TransformWorldToView(positionWS);
    positionVS.z -= _OutlineZOffset;
    float4 positionCS = TransformWViewToHClip(positionVS);

    // extrusion direction in pixel space
    float3 normalCS = TransformWorldToHClipDir(normalWS);
    float2 dirPx = normalCS.xy * _ScaledScreenParams.xy;
    float lenPx = length(dirPx);
    dirPx = lenPx > 1e-5 ? dirPx / lenPx : float2(0, 0);

    // width in pixels
    float dist = max(distance(positionWS, GetCameraPositionWS()), 1e-3);
    float distScale = clamp(_OutlineReferenceDistance / dist, _OutlineMinScale, 1.0);
    float widthPx = _OutlineWidth * lerp(1.0, distScale, _OutlineDistanceScale)
                  * (_ScaledScreenParams.y / 1080.0);

    positionCS.xy += dirPx * (widthPx * 2.0) / _ScaledScreenParams.xy * positionCS.w;

    output.positionCS = positionCS;
    output.positionWS = positionWS;
    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
#if defined(_FOG_FRAGMENT)
    output.fogFactor = 0;
#else
    output.fogFactor = ComputeFogFactor(positionCS.z);
#endif
    return output;
}

half4 ComicOutlineFragment(OutlineVaryings input) : SV_Target
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

#if defined(_ALPHATEST_ON)
    half alpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)).a * _BaseColor.a;
    clip(alpha - _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif

    half3 color = _OutlineColor.rgb;
    float fogCoord = InitializeInputDataFog(float4(input.positionWS, 1.0), input.fogFactor);
    color = MixFog(color, fogCoord);
    return half4(color, 1.0h);
}

#endif // COMIC_OUTLINE_INCLUDED
