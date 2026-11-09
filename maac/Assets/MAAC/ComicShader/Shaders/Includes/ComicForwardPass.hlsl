#ifndef COMIC_FORWARD_PASS_INCLUDED
#define COMIC_FORWARD_PASS_INCLUDED

// ---------------------------------------------------------------------------------
// ComicForwardPass.hlsl
// Main "ForwardLit" pass: URP lighting -> scalar light -> stepped manga tone ->
// (halftone / hatching / paper noise) -> ink/paper colors -> fog.
// Structure (Attributes, GI, fog, shadow coords) mirrors URP 17.6 SimpleLitForwardPass.
// ---------------------------------------------------------------------------------

#include "ComicInput.hlsl"
#include "ComicCommon.hlsl"
#include "ComicLighting.hlsl"
#include "Halftone.hlsl"
#include "Hatching.hlsl"
#include "PaperNoise.hlsl"
#if defined(LOD_FADE_CROSSFADE)
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/LODCrossFade.hlsl"
#endif

struct Attributes
{
    float4 positionOS        : POSITION;
    float3 normalOS          : NORMAL;
    float2 texcoord          : TEXCOORD0;
    float2 staticLightmapUV  : TEXCOORD1;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float2 uv                : TEXCOORD0;
    float3 positionWS        : TEXCOORD1;
    half3  normalWS          : TEXCOORD2;
#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half4  fogFactorAndVertexLight : TEXCOORD3; // x: fog, yzw: vertex light
#else
    half   fogFactor         : TEXCOORD3;
#endif
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord       : TEXCOORD4;
#endif
    DECLARE_LIGHTMAP_OR_SH(staticLightmapUV, vertexSH, 5);
    float2 anchorUV          : TEXCOORD6;
#ifdef USE_APV_PROBE_OCCLUSION
    float4 probeOcclusion    : TEXCOORD7;
#endif
    float4 positionCS        : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

void ComicInitializeInputData(Varyings input, out InputData inputData)
{
    inputData = (InputData)0;
    inputData.positionWS = input.positionWS;
#if defined(DEBUG_DISPLAY)
    inputData.positionCS = input.positionCS;
#endif
    inputData.normalWS = NormalizeNormalPerPixel(input.normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(input.positionWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = input.shadowCoord;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(inputData.positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactorAndVertexLight.x);
    inputData.vertexLighting = input.fogFactorAndVertexLight.yzw;
#else
    inputData.fogCoord = InitializeInputDataFog(float4(inputData.positionWS, 1.0), input.fogFactor);
    inputData.vertexLighting = half3(0, 0, 0);
#endif

    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);

    // Baked GI (same branches as URP SimpleLitForwardPass, minus dynamic lightmaps / SS irradiance)
#if !defined(LIGHTMAP_ON) && (defined(PROBE_VOLUMES_L1) || defined(PROBE_VOLUMES_L2))
    inputData.bakedGI = SAMPLE_GI(input.vertexSH,
        GetAbsolutePositionWS(inputData.positionWS),
        inputData.normalWS,
        inputData.viewDirectionWS,
        input.positionCS.xy,
        input.probeOcclusion,
        inputData.shadowMask);
#else
    inputData.bakedGI = SAMPLE_GI(input.staticLightmapUV, input.vertexSH, inputData.normalWS);
    inputData.shadowMask = SAMPLE_SHADOWMASK(input.staticLightmapUV);
#endif
}

Varyings ComicPassVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    VertexPositionInputs vertexInput = GetVertexPositionInputs(input.positionOS.xyz);
    VertexNormalInputs normalInput = GetVertexNormalInputs(input.normalOS);

#if defined(_FOG_FRAGMENT)
    half fogFactor = 0;
#else
    half fogFactor = ComputeFogFactor(vertexInput.positionCS.z);
#endif

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionWS = vertexInput.positionWS;
    output.positionCS = vertexInput.positionCS;
    output.normalWS = NormalizeNormalPerVertex(normalInput.normalWS);
    output.anchorUV = ComicComputeAnchorUV();

    OUTPUT_LIGHTMAP_UV(input.staticLightmapUV, unity_LightmapST, output.staticLightmapUV);
    OUTPUT_SH4(vertexInput.positionWS, output.normalWS.xyz, GetWorldSpaceNormalizeViewDir(vertexInput.positionWS), output.vertexSH, output.probeOcclusion);

#ifdef _ADDITIONAL_LIGHTS_VERTEX
    half3 vertexLight = VertexLighting(vertexInput.positionWS, normalInput.normalWS);
    output.fogFactorAndVertexLight = half4(fogFactor, vertexLight);
#else
    output.fogFactor = fogFactor;
#endif

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = GetShadowCoord(vertexInput);
#endif
    return output;
}

void ComicPassFragment(
    Varyings input
    , out half4 outColor : SV_Target0
#ifdef _WRITE_RENDERING_LAYERS
    , out uint outRenderingLayers : SV_Target1
#endif
)
{
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

    // ---- Surface ------------------------------------------------------------
    half4 albedoAlpha = SampleAlbedoAlpha(input.uv, TEXTURE2D_ARGS(_BaseMap, sampler_BaseMap)) * _BaseColor;
#if defined(_ALPHATEST_ON)
    clip(albedoAlpha.a - _Cutoff);
#endif
#if defined(LOD_FADE_CROSSFADE)
    LODFadeCrossFade(input.positionCS);
#endif
    half albedoLum = Luminance(albedoAlpha.rgb);

    // ---- Lighting -> tone ---------------------------------------------------
    InputData inputData;
    ComicInitializeInputData(input, inputData);
    half light = ComicComputeLight(inputData);

    half continuousTone;
    half tone = ComicToneFromShade(albedoLum * light, continuousTone);   // 0 = black, 1 = white

    // ink coverage, 0 = paper, 1 = full ink
    half flatInk = 1.0h - tone;
    half beta = step(tone, 1e-3h);          // solid black fill (ベタ)

#if defined(_HALFTONE_ON) || defined(_HATCHING_ON)
    ComicPatternCoords pattern = ComicGetPatternCoords(input.positionWS, inputData.normalWS, input.uv,
                                                       inputData.normalizedScreenSpaceUV, input.anchorUV);
#endif

#if defined(_NOISE_ON)
    ComicNoiseSample noise = ComicSampleNoise(inputData.normalizedScreenSpaceUV);
    half coverageJitter = ComicNoiseCoverageJitter(noise);
#else
    half coverageJitter = 1.0h;
#endif

#if defined(_HALFTONE_ON)
    // Gray tone levels are printed as screen tone dots; beta stays solid ink.
    half dots = ComicHalftone(pattern, saturate(flatInk * coverageJitter));
    half toneInk = lerp(flatInk, dots, _HalftoneStrength);
    half3 color = lerp(ComicPaperColor(), _HalftoneColor.rgb, toneInk);
    color = lerp(color, _InkColor.rgb, beta);
#else
    half3 color = lerp(ComicPaperColor(), _InkColor.rgb, flatInk);
#endif

#if defined(_HATCHING_ON)
    // hatching follows the continuous (un-stepped) tone so the line weight changes smoothly
    half hatch = ComicHatching(pattern, continuousTone);
    color = lerp(color, _InkColor.rgb, hatch);
#endif

#if defined(_NOISE_ON)
    color = ComicApplyPaperNoise(color, noise);
#endif

    // ---- Grayscale <-> albedo tint -----------------------------------------
    half3 chroma = albedoAlpha.rgb / max(albedoLum, 1e-3h);
    color = lerp(saturate(color * chroma), color, _Grayscale);

    color = MixFog(color, inputData.fogCoord);
    outColor = half4(color, 1.0h);

#ifdef _WRITE_RENDERING_LAYERS
    outRenderingLayers = EncodeMeshRenderingLayer();
#endif
}

#endif // COMIC_FORWARD_PASS_INCLUDED
