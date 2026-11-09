#ifndef COMIC_INPUT_INCLUDED
#define COMIC_INPUT_INCLUDED

// ---------------------------------------------------------------------------------
// ComicInput.hlsl
// Material property declarations shared by EVERY pass of the Comic shader.
// All per-material values live in one UnityPerMaterial CBUFFER with an identical
// layout in every pass -> required for SRP Batcher compatibility.
// ---------------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
// SurfaceInput.hlsl declares _BaseMap / sampler_BaseMap, SampleAlbedoAlpha(), Alpha(), AlphaDiscard().
// It is also what URP's ShadowCasterPass / DepthOnlyPass / DepthNormalsPass expect to be available.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/SurfaceInput.hlsl"

CBUFFER_START(UnityPerMaterial)
    // --- Surface ---
    float4 _BaseMap_ST;
    half4  _BaseColor;
    half   _Cutoff;

    // --- Tone (基本設定) ---
    half4  _InkColor;
    half4  _PaperColor;
    half   _PaperDarkness;
    half   _Grayscale;
    half   _Contrast;
    half   _Brightness;
    half   _ToneSteps;
    half   _ShadowThreshold;
    half   _ToneSmoothness;
    half   _MidToneLightness;
    half   _AmbientStrength;
    half   _AdditionalLightStrength;
    half   _OcclusionStrength;

    // --- Pattern space (shared by halftone & hatching) ---
    float  _PatternWorldScale;
    float  _PatternUVScale;

    // --- Halftone ---
    half4  _HalftoneColor;
    float  _HalftoneScale;
    half   _HalftoneDotSize;
    half   _HalftoneStrength;
    half   _HalftoneAngle;
    half   _HalftoneContrast;
    half   _HalftoneAnchorToObject;

    // --- Hatching ---
    half   _HatchingStrength;
    float  _HatchingDensity;
    half   _HatchingWidth;
    half   _HatchingAngle;
    half   _HatchingCrossAngle;
    half   _HatchingThreshold1;
    half   _HatchingThreshold2;

    // --- Noise / paper ---
    half   _NoiseStrength;
    float  _NoiseScale;
    half   _PaperGrain;
    half   _InkUnevenness;
    float  _InkMottleScale;

    // --- Outline (inverted hull) ---
    half4  _OutlineColor;
    float  _OutlineWidth;
    half   _OutlineDistanceScale;
    float  _OutlineReferenceDistance;
    half   _OutlineMinScale;
    float  _OutlineZOffset;
    half   _OutlineNormalSource;
CBUFFER_END

// DOTS instancing (Entities Graphics / BRG). Mirrors the pattern used by URP's SimpleLitInput.hlsl.
#ifdef UNITY_DOTS_INSTANCING_ENABLED
UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
    UNITY_DOTS_INSTANCED_PROP(float4, _BaseColor)
    UNITY_DOTS_INSTANCED_PROP(float , _Cutoff)
UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

static float4 unity_DOTS_Sampled_BaseColor;
static float  unity_DOTS_Sampled_Cutoff;

void SetupDOTSComicMaterialPropertyCaches()
{
    unity_DOTS_Sampled_BaseColor = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float4, _BaseColor);
    unity_DOTS_Sampled_Cutoff    = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float , _Cutoff);
}

#undef UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES
#define UNITY_SETUP_DOTS_MATERIAL_PROPERTY_CACHES() SetupDOTSComicMaterialPropertyCaches()

#define _BaseColor unity_DOTS_Sampled_BaseColor
#define _Cutoff    unity_DOTS_Sampled_Cutoff
#endif

#endif // COMIC_INPUT_INCLUDED
