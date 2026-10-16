// Liquid Glass Engineering: the WPF glass pixel shader (ps_3_0).
//
// Applied by GlassShaderEffect to a surface's lens: a live copy of the scene's
// backdrop (and of any glass beneath) laid out `margin` beyond the surface on
// every side, frosted by WPF's BlurEffect for Regular glass. The shader
// refracts and tones it, adds the rim light, the pointer glow and the
// increased-contrast border, and returns premultiplied glass that WPF
// composites over the real backdrop (OPTICS §5 with `base` drawn by WPF).

#include "LiquidGlass.hlsli"

sampler2D Input : register(s0);          // premultiplied backdrop (frosted for Regular)

float4 LensGeometry : register(c0);      // lens size (xy) and surface size (zw), layout units
float4 ShapeOptics : register(c1);       // corner extent, corner exponent, refraction, unused
float4 MaterialOptics : register(c2);    // regular (0/1), dark (0/1), nested (0/1), increased contrast (0/1)
float4 TintOptics : register(c3);        // tint rgb, tint on (0/1)
float4 GlowOptics : register(c4);        // glow center (xy, from the surface center), radius, amount
float4 FillWeights : register(c5);       // Regular fill: 1-D tap weights for offsets 0..3
float4 FillOptics : register(c6);        // Regular fill: weight for offset 4, tap spacing (layout units), unused, unused
float4 DdxUvDdyUv : register(c7);        // supplied by WPF: d(uv)/dx (xy) and d(uv)/dy (zw) per device pixel

float4 Unpremultiply(float4 c)
{
    return float4(c.rgb / max(c.a, 1e-5), c.a);
}

// The wide fill blur, sampled from the frost over a sparse 9 x 9 grid (FrostFillKernel).
float4 FillAt(float2 uv, float2 tapStep)
{
    float weights[5] = { FillWeights.x, FillWeights.y, FillWeights.z, FillWeights.w, FillOptics.x };
    float4 fill = 0;
    [unroll] for (int j = -4; j <= 4; j++)
    {
        float4 row = 0;
        [unroll] for (int i = -4; i <= 4; i++)
            row += weights[abs(i)] * tex2Dlod(Input, float4(uv + float2(i, j) * tapStep, 0, 0));
        fill += weights[abs(j)] * row;
    }
    return fill;
}

float4 main(float2 uv : TEXCOORD) : COLOR
{
    float2 lens = LensGeometry.xy;
    float2 size = LensGeometry.zw;
    // One device pixel in layout units, measured from the texture itself, so
    // DPI and render transforms are always accounted for.
    float pixel = max(lens.x * abs(DdxUvDdyUv.x), lens.y * abs(DdxUvDdyUv.w));
    float2 p = uv * lens - 0.5 * lens;
    float3 field = lgShapeField(p, size, ShapeOptics.xy);
    // The rim's outer shade ends one unit (plus a pixel) outside the outline.
    if (field.x > 1.0 + pixel) return 0;

    float regular = MaterialOptics.x;
    float dark = MaterialOptics.y;
    float2 suv = uv + lgRefractionOffset(field, ShapeOptics.z) / lens;
    float4 src = Unpremultiply(tex2Dlod(Input, float4(suv, 0, 0)));
    if (regular > 0.5)
    {
        float4 fill = Unpremultiply(FillAt(suv, FillOptics.yy / lens));
        src.rgb = lgFrostColor(src.rgb, fill.rgb, dark);
    }

    float4 glass = lgCompose(src, 0, field, regular, dark, MaterialOptics.z, TintOptics.rgb, TintOptics.a, pixel);

    float cover = lgCoverage(field.x, pixel);
    float4 glow = lgGlow(p, GlowOptics.xy, max(GlowOptics.z, 1e-3), dark, GlowOptics.w, cover);
    glass = glow + glass * (1.0 - glow.a);

    // Increased contrast: a 1-unit inner border (black 0.18 light, white 0.28 dark).
    float band = (cover - lgCoverage(field.x + 1.0, pixel)) * MaterialOptics.w;
    float4 border = float4(dark.xxx, 1.0) * lerp(0.18, 0.28, dark) * band;
    return border + glass * (1.0 - border.a);
}
