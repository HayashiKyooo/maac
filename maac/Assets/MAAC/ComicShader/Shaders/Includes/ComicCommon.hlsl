#ifndef COMIC_COMMON_INCLUDED
#define COMIC_COMMON_INCLUDED

// ---------------------------------------------------------------------------------
// ComicCommon.hlsl
// Small, texture-free math helpers shared by all comic features:
//  - 2D rotation, anti-aliased step
//  - Hash / value noise (no texture sampling)
//  - Tone mapping: light value -> stepped manga tone (cel shading)
//  - Pattern coordinate generation for Screen / World / Object / UV spaces
// ---------------------------------------------------------------------------------

#define COMIC_REFERENCE_SCREEN_HEIGHT 1080.0

// ---------------------------------------------------------------- math helpers ---
float2 ComicRotate2D(float2 p, float angleDeg)
{
    float s, c;
    sincos(radians(angleDeg), s, c);
    return float2(c * p.x - s * p.y, s * p.x + c * p.y);
}

// Anti-aliased "x < edge" test. Returns 1 inside, 0 outside, with a soft band of +-aa.
half ComicAAStepInside(float x, float edge, float aa)
{
    return 1.0h - (half)smoothstep(edge - aa, edge + aa, x);
}

// ---------------------------------------------------------------- hash / noise ---
// Integer-free hash (works on all targets, stable for large coordinates).
float ComicHash12(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * 0.1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}

// Value noise in [0,1]. 4 hashes, no texture.
float ComicValueNoise(float2 p)
{
    float2 i = floor(p);
    float2 f = frac(p);
    float2 u = f * f * (3.0 - 2.0 * f);
    float a = ComicHash12(i);
    float b = ComicHash12(i + float2(1, 0));
    float c = ComicHash12(i + float2(0, 1));
    float d = ComicHash12(i + float2(1, 1));
    return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
}

// ---------------------------------------------------------------- tone (cel) ---
// Converts a scalar lighting value into a manga tone value in [0,1]
// (0 = black "beta", 1 = paper white), quantized into _ToneSteps levels.
//
//  shade      : albedo luminance * received light
//  continuous : (out) un-quantized tone, used by hatching for smooth density changes
//
// Shadow Threshold semantics: "shade == threshold" is the border between paper white
// and the first shadow step. Contrast controls how fast deeper steps are reached.
half ComicToneFromShade(half shade, out half continuous)
{
    half steps = max(2.0h, round(_ToneSteps));
    half x = (shade - _ShadowThreshold) * _Contrast + (1.0h - 1.0h / steps);
    x = saturate(x + _Brightness);
    continuous = x;

    float y = min(x * steps, steps - 1e-3);
    float level = floor(y);
    // Smooth (anti-aliased) step border. _ToneSmoothness = 0 -> perfectly hard border.
    // clamped so that pixel-frequency lighting noise can never turn the steps into a gradient
    float aa = clamp(fwidth(y) * _ToneSmoothness, 1e-5, 0.35);
    level += smoothstep(1.0 - aa, 1.0, frac(y));
    half q = (half)saturate(level / (steps - 1.0));
    // Mid Tone Lightness: lightens the intermediate (gray) steps only; black (0) and white (1) stay.
    return pow(q, lerp(1.0h, 0.25h, _MidToneLightness));
}

// ---------------------------------------------------------------- pattern space ---
// Halftone & hatching share one coordinate system ("pattern units").
//  Screen : 1 unit = 1 pixel at 1080p (resolution independent, constant on screen)
//  World  : positionWS * _PatternWorldScale  (triplanar)
//  Object : positionOS * _PatternWorldScale  (triplanar, sticks to the object)
//  UV     : uv * _PatternUVScale
#if defined(_HALFTONESPACE_WORLD) || defined(_HALFTONESPACE_OBJECT)
    #define COMIC_PATTERN_TRIPLANAR 1
#endif

struct ComicPatternCoords
{
    float2 p0;          // screen / uv / triplanar X projection
#if defined(COMIC_PATTERN_TRIPLANAR)
    float2 p1;          // triplanar Y projection
    float2 p2;          // triplanar Z projection
    half3  weights;     // triplanar blend weights
#endif
};

// screenUV  : normalized screen uv of the fragment (GetNormalizedScreenSpaceUV)
// anchorUV  : normalized screen uv of the object pivot (vertex stage), used to make the
//             screen-space pattern follow moving objects (_HalftoneAnchorToObject)
ComicPatternCoords ComicGetPatternCoords(float3 positionWS, half3 normalWS, float2 uv, float2 screenUV, float2 anchorUV)
{
    ComicPatternCoords c = (ComicPatternCoords)0;

#if defined(_HALFTONESPACE_WORLD) || defined(_HALFTONESPACE_OBJECT)
    #if defined(_HALFTONESPACE_OBJECT)
        float3 pos = TransformWorldToObject(positionWS);
        half3  n   = (half3)TransformWorldToObjectDir(normalWS, true);
    #else
        float3 pos = positionWS;
        half3  n   = normalWS;
    #endif
    pos *= _PatternWorldScale;
    half3 w = pow(abs(n), 4.0h);
    w /= max(w.x + w.y + w.z, 1e-4h);
    c.p0 = pos.zy;
    c.p1 = pos.xz;
    c.p2 = pos.xy;
    c.weights = w;
#elif defined(_HALFTONESPACE_UV)
    c.p0 = uv * _PatternUVScale;
#else // _HALFTONESPACE_SCREEN (default)
    float2 rel = screenUV - anchorUV * _HalftoneAnchorToObject;
    float2 screenSize = _ScaledScreenParams.xy;
    c.p0 = rel * screenSize * (COMIC_REFERENCE_SCREEN_HEIGHT / screenSize.y);
#endif
    return c;
}

// Normalized screen uv of the object pivot. Same convention as positionNDC in URP.
float2 ComicComputeAnchorUV()
{
    float4 pivotCS = TransformWorldToHClip(TransformObjectToWorld(float3(0, 0, 0)));
    float4 ndc = pivotCS * 0.5;
    ndc.xy = float2(ndc.x, ndc.y * _ProjectionParams.x) + ndc.w;
    return ndc.xy / max(pivotCS.w, 1e-4);
}

#endif // COMIC_COMMON_INCLUDED
