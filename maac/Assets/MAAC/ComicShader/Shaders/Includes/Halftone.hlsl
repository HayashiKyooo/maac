#ifndef COMIC_HALFTONE_INCLUDED
#define COMIC_HALFTONE_INCLUDED

// ---------------------------------------------------------------------------------
// Halftone.hlsl  (manga screen tone / 網点)
// Procedural AM halftone: a rotated grid of round dots whose radius follows the ink
// coverage (dark = big dots, white = no dots). Pure ALU, no texture sampling.
//  - Anti-aliased with screen-space derivatives (no shimmering at edges)
//  - When cells get smaller than ~2-4 pixels (far away in World/Object/UV space) the
//    pattern fades to the equivalent flat gray -> avoids moire & temporal flicker.
// ---------------------------------------------------------------------------------

#include "ComicCommon.hlsl"

// p        : pattern coordinate (see ComicGetPatternCoords)
// coverage : ink coverage 0..1 (1 - tone)
half ComicHalftoneDots(float2 p, half coverage)
{
    float2 q = ComicRotate2D(p, _HalftoneAngle) / max(_HalftoneScale, 1e-3);   // cell units

    // pixel footprint in cell units
    float2 fw = fwidth(q);
    float footprint = max(max(fw.x, fw.y), 1e-5);

    float2 cell = frac(q) - 0.5;
    float d = length(cell);                                   // 0 (center) .. 0.707 (corner)
    float r = sqrt(saturate(coverage)) * 0.7072 * _HalftoneDotSize;

    // Halftone Contrast: 1 = razor sharp dot edges, 0 = soft blurry dots
    float aa = footprint * lerp(4.0, 0.6, _HalftoneContrast);
    half ink = 1.0h - (half)smoothstep(r - aa, r + aa, d);
    ink *= (half)saturate(coverage * 64.0);                   // no stray dot centers on paper white

    // moire guard: >= 4 px per cell -> dots, <= 2 px per cell -> flat tone
    half fade = (half)saturate(0.5 / footprint - 1.0);
    return lerp(coverage, ink, fade);
}

half ComicHalftone(ComicPatternCoords pc, half coverage)
{
#if defined(COMIC_PATTERN_TRIPLANAR)
    return ComicHalftoneDots(pc.p0, coverage) * pc.weights.x
         + ComicHalftoneDots(pc.p1, coverage) * pc.weights.y
         + ComicHalftoneDots(pc.p2, coverage) * pc.weights.z;
#else
    return ComicHalftoneDots(pc.p0, coverage);
#endif
}

#endif // COMIC_HALFTONE_INCLUDED
