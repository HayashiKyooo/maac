#ifndef COMIC_HATCHING_INCLUDED
#define COMIC_HATCHING_INCLUDED

// ---------------------------------------------------------------------------------
// Hatching.hlsl  (斜線 / クロスハッチング)
// Procedural parallel lines from a triangle wave - no textures.
//  layer 1 : appears below _HatchingThreshold1, lines get thicker as it gets darker
//  layer 2 : crossing lines (angle + _HatchingCrossAngle) below _HatchingThreshold2
// Uses the same pattern space as the halftone (Screen / World / Object / UV).
// Lines are anti-aliased with fwidth and fade to an equivalent flat value when the line
// period drops below ~2-4 pixels (no moire / flicker at distance).
// ---------------------------------------------------------------------------------

#include "ComicCommon.hlsl"

half ComicHatchLayer(float2 p, half angle, half width)
{
    // coordinate across the lines, in line periods
    float x = ComicRotate2D(p, angle).x * (_HatchingDensity * 0.01);
    float fw = max(fwidth(x), 1e-5);
    float v = abs(frac(x) - 0.5) * 2.0;          // 0 = line center, 1 = middle between lines
    float aa = fw;                                // v changes 2x per period -> +-0.5 px edge
    half ink = 1.0h - (half)smoothstep(width - aa, width + aa, v);
    ink *= (half)saturate(width / aa);            // width 0 -> no ink at all

    half fade = (half)saturate((1.0 / fw - 2.0) * 0.5);   // >=4 px per period -> lines
    return lerp(width, ink, fade);
}

// tone : continuous tone 0 (black) .. 1 (white)
half ComicHatchingInk2D(float2 p, half tone)
{
    const half onset = 0.06h;   // how quickly a layer fades in below its threshold
    // layer activation (0..1) and line weight growing with darkness
    half a1 = 1.0h - smoothstep(_HatchingThreshold1 - onset, _HatchingThreshold1, tone);
    half a2 = 1.0h - smoothstep(_HatchingThreshold2 - onset, _HatchingThreshold2, tone);
    half d1 = saturate((_HatchingThreshold1 - tone) / max(_HatchingThreshold1, 1e-3h));
    half d2 = saturate((_HatchingThreshold2 - tone) / max(_HatchingThreshold2, 1e-3h));
    half l1 = ComicHatchLayer(p, _HatchingAngle, _HatchingWidth * (0.45h + 0.55h * d1)) * a1;
    half l2 = ComicHatchLayer(p, _HatchingAngle + _HatchingCrossAngle, _HatchingWidth * (0.45h + 0.55h * d2)) * a2;
    return max(l1, l2);
}

half ComicHatching(ComicPatternCoords pc, half tone)
{
#if defined(COMIC_PATTERN_TRIPLANAR)
    half ink = ComicHatchingInk2D(pc.p0, tone) * pc.weights.x
             + ComicHatchingInk2D(pc.p1, tone) * pc.weights.y
             + ComicHatchingInk2D(pc.p2, tone) * pc.weights.z;
#else
    half ink = ComicHatchingInk2D(pc.p0, tone);
#endif
    return ink * _HatchingStrength;
}

#endif // COMIC_HATCHING_INCLUDED
