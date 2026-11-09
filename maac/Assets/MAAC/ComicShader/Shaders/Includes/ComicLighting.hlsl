#ifndef COMIC_LIGHTING_INCLUDED
#define COMIC_LIGHTING_INCLUDED

// ---------------------------------------------------------------------------------
// ComicLighting.hlsl
// Gathers URP lighting (main light + shadows, additional lights + shadows, Forward+
// cluster loop, light layers, SSAO, ambient SH / lightmaps) and reduces it to a single
// scalar "received light" value. Color is irrelevant for monochrome manga, so every
// light contributes Luminance(color) * attenuation * N.L.
// Uses only functions that exist in URP 17.6 ShaderLibrary (RealtimeLights.hlsl /
// Lighting.hlsl / AmbientOcclusion.hlsl).
// ---------------------------------------------------------------------------------

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

half ComicLightTerm(Light light, half3 normalWS)
{
    half nDotL = saturate(dot(normalWS, light.direction));
    half atten = (half)light.distanceAttenuation * light.shadowAttenuation;
    return Luminance(light.color) * atten * nDotL;
}

// Returns the total scalar light reaching the surface (not multiplied by albedo).
// NOTE: the LIGHT_LOOP_BEGIN macro (Forward+) requires a variable named "inputData".
half ComicComputeLight(InputData inputData)
{
    uint meshRenderingLayers = GetMeshRenderingLayer();
    half4 shadowMask = CalculateShadowMask(inputData);
    AmbientOcclusionFactor aoFactor = CreateAmbientOcclusionFactor(inputData.normalizedScreenSpaceUV, 1.0h);
    // SSAO is pixel-noisy; for flat manga tones it is attenuated by default (_OcclusionStrength = 0)
    aoFactor.directAmbientOcclusion   = lerp(1.0h, aoFactor.directAmbientOcclusion, _OcclusionStrength);
    aoFactor.indirectAmbientOcclusion = lerp(1.0h, aoFactor.indirectAmbientOcclusion, _OcclusionStrength);

    Light mainLight = GetMainLight(inputData, shadowMask, aoFactor);
    half3 bakedGI = inputData.bakedGI;
    MixRealtimeAndBakedGI(mainLight, inputData.normalWS, bakedGI, aoFactor);

    half direct = 0.0h;
#ifdef _LIGHT_LAYERS
    if (IsMatchingLightLayer(mainLight.layerMask, meshRenderingLayers))
#endif
    {
        direct += ComicLightTerm(mainLight, inputData.normalWS);
    }

    half additional = 0.0h;
#if defined(_ADDITIONAL_LIGHTS)
    uint pixelLightCount = GetAdditionalLightsCount();

    #if USE_CLUSTER_LIGHT_LOOP
    // Forward+: additional directional lights are not part of the cluster list.
    [loop] for (uint lightIndex = 0; lightIndex < min(URP_FP_DIRECTIONAL_LIGHTS_COUNT, MAX_VISIBLE_LIGHTS); lightIndex++)
    {
        CLUSTER_LIGHT_LOOP_SUBTRACTIVE_LIGHT_CHECK
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additional += ComicLightTerm(light, inputData.normalWS);
        }
    }
    #endif

    LIGHT_LOOP_BEGIN(pixelLightCount)
        Light light = GetAdditionalLight(lightIndex, inputData, shadowMask, aoFactor);
    #ifdef _LIGHT_LAYERS
        if (IsMatchingLightLayer(light.layerMask, meshRenderingLayers))
    #endif
        {
            additional += ComicLightTerm(light, inputData.normalWS);
        }
    LIGHT_LOOP_END
#endif

#if defined(_ADDITIONAL_LIGHTS_VERTEX)
    additional += Luminance(inputData.vertexLighting);
#endif

    half ambient = Luminance(bakedGI) * aoFactor.indirectAmbientOcclusion;

    return direct + additional * _AdditionalLightStrength + ambient * _AmbientStrength;
}

#endif // COMIC_LIGHTING_INCLUDED
