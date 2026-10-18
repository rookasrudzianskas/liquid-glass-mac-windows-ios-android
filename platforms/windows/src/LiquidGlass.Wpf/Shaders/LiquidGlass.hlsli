// Liquid Glass Engineering: shader library (HLSL, shader model 3.0 and later).
//
// A line-by-line translation of spec/shaders/liquid-glass.glsl, the canonical
// GLSL; keep it in sync with that file and with spec/vectors.json. The WPF
// pixel shader (GlassSurface.fx, ps_3_0) and the GPU verification kernel
// (tests, cs_5_0 on WARP) both include this file.
//
// Units: layout units (device-independent pixels). `pixel` is one device pixel
// in layout units. Colors: sRGB gamma-encoded. "premul" means premultiplied alpha.

#ifndef LIQUID_GLASS_HLSLI
#define LIQUID_GLASS_HLSLI

static const float LG_BEVEL = 20.0;
static const float LG_CONTINUOUS_EXTENT = 1.528;
static const float LG_CONTINUOUS_EXPONENT = 3.26;
static const float3 LG_LUMA = float3(0.213, 0.715, 0.072);

float lgSat(float x) { return saturate(x); }

// ---------------------------------------------------------------------------
// Shape

// Corner extent and superellipse exponent. A negative radius means capsule.
float2 lgShapeParams(float2 size, float radius)
{
    float halfMin = max(0.0, min(size.x, size.y) * 0.5);
    if (radius < 0.0) return float2(halfMin, 2.0);
    float raw = LG_CONTINUOUS_EXTENT * radius;
    float extent = min(raw, halfMin);
    float t = halfMin > 0.0 ? smoothstep(0.75, 1.0, raw / halfMin) : 1.0;
    return float2(extent, lerp(LG_CONTINUOUS_EXPONENT, 2.0, t));
}

// Signed distance (negative inside) and outward normal at p, measured from
// the shape center with y down. Returns float3(distance, normal.x, normal.y).
float3 lgShapeField(float2 p, float2 size, float2 shape)
{
    float e = shape.x;
    float n = shape.y;
    float2 s = float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
    float2 q = abs(p) - (size * 0.5 - e);
    if (q.x > 0.0 && q.y > 0.0)
    {
        // Normalize by the larger component so pow() never overflows.
        float k = max(q.x, q.y);
        float2 u = abs(q / k);
        float m = k * pow(pow(u.x, n) + pow(u.y, n), 1.0 / n);
        if (m < 1e-6) return float3(-e, s * 0.70710678);
        float2 g = pow(abs(q / m), (n - 1.0).xx);
        float gl = length(g);
        return float3((m - e) / gl, s * g / gl);
    }
    if (q.x > q.y) return float3(q.x - e, s.x, 0.0);
    return float3(q.y - e, 0.0, s.y);
}

// ---------------------------------------------------------------------------
// Refraction

float lgEdgeProfile(float distance)
{
    float depth = lgSat(-distance / LG_BEVEL);
    return 1.0 - lgSat(sqrt((2.0 - depth) * depth));
}

// Offset from the pixel to the backdrop point it shows.
float2 lgRefractionOffset(float3 field, float refraction)
{
    return -field.yz * lgEdgeProfile(field.x) * refraction;
}

float lgCoverage(float distance, float pixel)
{
    return lgSat(0.5 - distance / pixel);
}

// ---------------------------------------------------------------------------
// Color

float3 lgSaturate(float3 c, float s)
{
    return saturate(float3(
        (0.213 + 0.787 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 - 0.072 * s) * c.b,
        (0.213 - 0.213 * s) * c.r + (0.715 + 0.285 * s) * c.g + (0.072 - 0.072 * s) * c.b,
        (0.213 - 0.213 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 + 0.928 * s) * c.b));
}

float3 lgClearTone(float3 v, float dark, float nested)
{
    float lift = lerp(0.18, 0.15, dark);
    float rolloff = lerp(0.0, 0.2, dark);
    float3 inv = 1.0 - v;
    float3 tone = v + lift * inv * inv * inv - rolloff * v * v * v;
    float3 veil = lerp(0.78 * v + 0.26, 0.38 * v + 0.2, dark);
    float weight = lerp(0.06, 0.14, dark);
    float3 layerLight = dark * nested * 0.08 * inv;
    return saturate((1.0 - weight) * tone + weight * veil + layerLight);
}

float3 lgRegularTone(float3 v, float dark)
{
    float3 abq = lerp(float3(0.36, 0.67, -0.1113), float3(0.105, 0.83, -0.2091), dark);
    float weight = lerp(0.8, 0.9, dark);
    float3 toned = saturate(weight * (abq.x + abq.y * v + abq.z * v * v) + (1.0 - weight) * v);
    return lgSaturate(toned, lerp(1.1031, 1.0648, dark));
}

// material: 0 = clear, 1 = regular.
float3 lgMaterialColor(float3 c, float material, float dark, float nested)
{
    return material > 0.5 ? lgRegularTone(c, dark) : lgClearTone(c, dark, nested);
}

float3 lgTintColor(float3 c, float3 tint, float material)
{
    float body = material > 0.5 ? 0.78 : 0.68;
    return saturate(tint * (body + (1.0 - body) * dot(c, LG_LUMA)));
}

// Regular glass: `frost` is the backdrop blurred with sigma 7.33, `fill` with
// sigma 23 (light) or 21.8 (dark).
float3 lgFrostColor(float3 frost, float3 fill, float dark)
{
    float3 blended = dark > 0.5 ? min(frost, fill) : max(frost, fill);
    float opacity = lerp(0.34, 0.65, dark);
    return opacity * blended + (1.0 - opacity) * frost;
}

// Menu panels mix their glass toward a solid backing so text always reads:
// #F5F5F7 light, #1C1C1E dark. amount is 0.6 on menu panels (times the open
// progress of the menu), 0 elsewhere.
float3 lgLegibility(float3 c, float dark, float amount)
{
    float3 backing = lerp(float3(0.960784, 0.960784, 0.968627), float3(0.109804, 0.109804, 0.117647), dark);
    return lerp(c, backing, amount);
}

// ---------------------------------------------------------------------------
// Rim light (premul). tintOn: 0 or 1.

float4 lgRimOverlay(float3 field, float dark, float pixel, float3 tint, float tintOn)
{
    float d = field.x;
    float2 n = field.yz;
    float aa = max(pixel, 1e-6);
    float cover = lgSat(0.5 - d / aa);
    static const float spread = 1.08;
    float directional = pow(lgSat((abs(n.y) - cos(spread)) / (1.0 - cos(spread))), 1.25);
    float diffuseDir = pow(lgSat((abs(n.y) - cos(spread * 0.65)) / (1.0 - cos(spread * 0.65))), 1.25);
    float stroke = lgSat((d + 0.9) / aa + 0.5) * lgSat(0.5 - d / aa);
    float specular = directional * stroke;
    specular = specular / (1.0 + 0.5 * (1.0 - specular));
    float inner = 1.0 - lgSat(-d / 4.5);
    float diffuse = diffuseDir * 0.18 * inner * inner * cover;
    float highlight = lgSat(specular * lerp(1.0, 0.82, dark) + diffuse * lerp(1.0, 0.4, dark));
    float adjusted = d - 0.5;
    float falloff = 1.0 - lgSat(-adjusted / 0.5);
    float ramp = lerp(falloff > 0.0 ? 1.0 : 0.0, falloff, 0.75);
    float outline = ramp * lgSat((adjusted + 0.5) / aa + 0.5) * lgSat(-adjusted / aa + 0.5);
    static const float sideSpread = 0.98;
    float2 w = saturate((float2(n.x, -n.x) - cos(sideSpread)) / (1.0 - cos(sideSpread))) * outline;
    float2 shaded = w / (1.0 + 0.4 * (1.0 - w));
    float shade = (shaded.x + shaded.y) * lerp(0.48, 0.6, dark);
    float alpha = highlight + shade * (1.0 - highlight);
    float3 light = tintOn > 0.5 ? highlight * (0.45 + 0.55 * tint) : (lerp(1.0, 0.82, dark) * highlight).xxx;
    return float4(light, alpha);
}

// ---------------------------------------------------------------------------
// Composite

// The glass body without the rim light: lgCompose up to (not including) the
// rim, so that a port can paint layers between the two (the WPF shader puts
// the increased-contrast border there, as the other ports do). Not in the
// canonical GLSL, where lgCompose is a single function; lgCompose below is
// exactly lgComposeBody followed by the rim.
float4 lgComposeBody(float4 src, float4 base, float3 field, float material, float dark, float nested,
                     float3 tint, float tintOn, float pixel, float legibility)
{
    float3 rgb = lgMaterialColor(src.rgb, material, dark, nested);
    float a = src.a;
    if (tintOn > 0.5)
    {
        float body = material > 0.5 ? 0.78 : 0.68;
        rgb = lgTintColor(rgb, tint, material);
        a = body + (1.0 - body) * a;
    }
    rgb = lgLegibility(rgb, dark, legibility);
    float ga = a * lgCoverage(field.x, pixel);
    return float4(rgb * ga, ga) + base * (1.0 - ga);
}

// src: refracted backdrop at p + lgRefractionOffset (unpremul; regular glass
// passes lgFrostColor). base: unrefracted backdrop at p (premul).
float4 lgCompose(float4 src, float4 base, float3 field, float material, float dark, float nested,
                 float3 tint, float tintOn, float pixel, float legibility)
{
    float4 result = lgComposeBody(src, base, field, material, dark, nested, tint, tintOn, pixel, legibility);
    float4 rim = lgRimOverlay(field, dark, pixel, tint, tintOn);
    return rim + result * (1.0 - rim.a);
}

// Pointer glow for interactive glass, premul white. Paint after lgCompose,
// clipped to coverage. glowAmount is the glow spring value (0..1).
float4 lgGlow(float2 p, float2 center, float radius, float dark, float glowAmount, float cover)
{
    float alpha = lerp(0.3, 0.16, dark) * glowAmount * (1.0 - lgSat(length(p - center) / radius)) * cover;
    return float4(alpha.xxx, alpha);
}

#endif
