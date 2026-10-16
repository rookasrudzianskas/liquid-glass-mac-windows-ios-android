// Evaluates the shader library on the GPU for the golden vectors (cs_5_0, run on WARP by the tests).
#include "LiquidGlass.hlsli"

struct GlassCase
{
    float4 geometry;   // x, y (from the center), width, height
    float4 optics;     // radius (< 0: capsule), material, dark, refraction
    float4 tint;       // rgb, on
    float4 pixel;      // device pixel in layout units, unused...
};

StructuredBuffer<GlassCase> Cases : register(t0);
RWStructuredBuffer<float4> Results : register(u0);

cbuffer Counts : register(b0)
{
    uint CaseCount;
    uint3 Padding;
};

// The linear backdrop of spec/vectors.json.
float4 Backdrop(float2 p)
{
    return float4(saturate(0.5 + p.x / 400.0), saturate(0.5 + p.y / 400.0), 0.6, 1.0);
}

[numthreads(64, 1, 1)]
void main(uint3 id : SV_DispatchThreadID)
{
    if (id.x >= CaseCount) return;
    GlassCase c = Cases[id.x];
    float2 p = c.geometry.xy;
    float2 size = c.geometry.zw;
    float3 field = lgShapeField(p, size, lgShapeParams(size, c.optics.x));
    float4 src = Backdrop(p + lgRefractionOffset(field, c.optics.w));
    Results[id.x * 2] = lgCompose(src, Backdrop(p), field, c.optics.y, c.optics.z, 0.0, c.tint.rgb, c.tint.a, c.pixel.x);
    Results[id.x * 2 + 1] = float4(field, 0.0);
}
