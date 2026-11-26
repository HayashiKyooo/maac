#ifndef GLOWDUST_COMMON_INCLUDED
#define GLOWDUST_COMMON_INCLUDED

// Globals written by PowderVolume.cs every frame
TEXTURE3D(_GD_DensityTex);
SAMPLER(sampler_GD_DensityTex);
float4 _GD_VolumeOrigin;   // xyz = world origin of the volume
float4 _GD_VolumeInvSize;  // xyz = 1 / world size of the volume
float4 _GD_CellSize;       // x = cell size (m)
float4 _GD_PlayerPos;      // xyz = viewer position
float  _GD_Enabled;        // 1 while the powder volume is running (play mode)

float GD_SampleDensity(float3 positionWS)
{
    float3 uvw = (positionWS - _GD_VolumeOrigin.xyz) * _GD_VolumeInvSize.xyz;
    if (any(uvw < 0.0) || any(uvw > 1.0)) return 0.0;
    return SAMPLE_TEXTURE3D_LOD(_GD_DensityTex, sampler_GD_DensityTex, uvw, 0).r;
}

// 7-tap blurred density + gradient (gradient points toward the powder)
float GD_DensityAndGradient(float3 p, out float3 grad)
{
    float c = _GD_CellSize.x * 1.5;
    float d0  = GD_SampleDensity(p);
    float dxp = GD_SampleDensity(p + float3(c, 0, 0));
    float dxn = GD_SampleDensity(p - float3(c, 0, 0));
    float dyp = GD_SampleDensity(p + float3(0, c, 0));
    float dyn = GD_SampleDensity(p - float3(0, c, 0));
    float dzp = GD_SampleDensity(p + float3(0, 0, c));
    float dzn = GD_SampleDensity(p - float3(0, 0, c));
    grad = float3(dxp - dxn, dyp - dyn, dzp - dzn);
    return (d0 * 2.0 + dxp + dxn + dyp + dyn + dzp + dzn) * 0.125;
}

float GD_Hash13(float3 p)
{
    p = frac(p * 0.1031);
    p += dot(p, p.yzx + 33.33);
    return frac((p.x + p.y) * p.z);
}

float GD_ValueNoise(float3 p)
{
    float3 i = floor(p);
    float3 f = frac(p);
    f = f * f * (3.0 - 2.0 * f);
    float n000 = GD_Hash13(i);
    float n100 = GD_Hash13(i + float3(1, 0, 0));
    float n010 = GD_Hash13(i + float3(0, 1, 0));
    float n110 = GD_Hash13(i + float3(1, 1, 0));
    float n001 = GD_Hash13(i + float3(0, 0, 1));
    float n101 = GD_Hash13(i + float3(1, 0, 1));
    float n011 = GD_Hash13(i + float3(0, 1, 1));
    float n111 = GD_Hash13(i + float3(1, 1, 1));
    return lerp(lerp(lerp(n000, n100, f.x), lerp(n010, n110, f.x), f.y),
                lerp(lerp(n001, n101, f.x), lerp(n011, n111, f.x), f.y), f.z);
}

// Procedural stone blocks. Returns mortar mask (1 on seams) and per-block variation.
void GD_StonePattern(float3 p, float3 n, float2 blockSize, float mortarWidth, out float mortar, out float variation)
{
    float3 an = abs(n);
    float2 uv;
    float2 size = blockSize;
    if (an.y >= an.x && an.y >= an.z) { uv = p.xz; size = blockSize.xx; }
    else if (an.x >= an.z)            { uv = p.zy; }
    else                              { uv = p.xy; }

    float2 g = uv / size;
    float row = floor(g.y);
    g.x += row * 0.5;
    float2 cell = floor(g);
    float2 f = frac(g);
    float ex = min(f.x, 1.0 - f.x) * size.x;
    float ey = min(f.y, 1.0 - f.y) * size.y;
    float e = min(ex, ey);
    mortar = 1.0 - smoothstep(mortarWidth * 0.4, mortarWidth, e);
    float blockRand = GD_Hash13(float3(cell, 7.13));
    float grain = GD_ValueNoise(p * 3.1) * 0.6 + GD_ValueNoise(p * 9.7) * 0.4;
    variation = saturate(blockRand * 0.55 + grain * 0.45);
}

#endif
