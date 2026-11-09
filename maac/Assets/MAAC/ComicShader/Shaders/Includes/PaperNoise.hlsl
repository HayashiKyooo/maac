#ifndef COMIC_PAPER_NOISE_INCLUDED
#define COMIC_PAPER_NOISE_INCLUDED

// ---------------------------------------------------------------------------------
// PaperNoise.hlsl  (紙の粒子感・インクのムラ・微細ノイズ)
// Printed-page texture computed from hash/value noise (no textures). Screen anchored,
// like a sheet of paper in front of the camera (1 unit = 1 px @1080p).
//  - Paper grain     : fine value noise darkening the paper slightly
//  - Ink unevenness  : low frequency mottling that lifts solid ink towards paper
//  - Dot roughness   : jitters halftone coverage so dots get rough printed edges
// ---------------------------------------------------------------------------------

#include "ComicCommon.hlsl"

struct ComicNoiseSample
{
    half grain;    // 0..1 fine grain
    half mottle;   // 0..1 low frequency ink mottling
    half speckle;  // 0..1 per-pixel white noise
};

ComicNoiseSample ComicSampleNoise(float2 screenUV)
{
    float2 px = screenUV * _ScaledScreenParams.xy * (COMIC_REFERENCE_SCREEN_HEIGHT / _ScaledScreenParams.y);
    ComicNoiseSample n;
    float2 gp = px / max(_NoiseScale, 0.01);
    n.grain = (half)ComicValueNoise(gp);
    n.speckle = (half)ComicHash12(floor(gp));
    n.mottle = (half)(ComicValueNoise(px / _InkMottleScale) * 0.65 + ComicValueNoise(px / (_InkMottleScale * 0.31) + 17.0) * 0.35);
    return n;
}

// Multiplicative jitter for halftone / hatching coverage (rough printed edges).
half ComicNoiseCoverageJitter(ComicNoiseSample n)
{
    return 1.0h + (n.speckle - 0.5h) * 0.2h * _NoiseStrength;
}

half3 ComicApplyPaperNoise(half3 color, ComicNoiseSample n)
{
    half lumInk = Luminance(_InkColor.rgb);
    half lumPaper = Luminance(ComicPaperColor());
    half inkAmount = 1.0h - saturate((Luminance(color) - lumInk) / max(lumPaper - lumInk, 1e-3h));

    // paper grain: slight darkening in paper regions
    half grainDark = (n.grain * 0.7h + n.speckle * 0.3h) * _PaperGrain * 0.22h * _NoiseStrength;
    color *= 1.0h - grainDark * (1.0h - inkAmount);

    // ink unevenness: ink is lifted towards the paper colour in blotches
    // (inkAmount^2 keeps mid-gray halftone areas clean; mostly solid ink is affected)
    half lift = saturate(n.mottle * 1.3h - 0.3h) * _InkUnevenness * 0.35h * _NoiseStrength
              + n.grain * _InkUnevenness * 0.10h * _NoiseStrength;
    color = lerp(color, ComicPaperColor(), saturate(lift) * inkAmount * inkAmount);
    return color;
}

#endif // COMIC_PAPER_NOISE_INCLUDED
