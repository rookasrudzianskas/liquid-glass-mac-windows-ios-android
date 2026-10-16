// Liquid Glass Engineering: SwiftUI layer effects.
//
// SwiftUI hands layer effects premultiplied, sRGB gamma-encoded colors in its
// default (non-linear) rendering mode, and positions in points from the top-left
// of the layer. The glass effect unpremultiplies, runs the optical model from
// LiquidGlassCore.h on sRGB values, and returns a premultiplied overlay that
// SwiftUI composites over the unmodified content beneath the surface. That is
// exactly the spec's composite `rim + glass(1 - rim.a)` with `glass = (rgb·ga,
// ga) + base(1 - ga)`, because source-over adds the `base(1 - ga)(1 - rim.a)`
// term.

#include <metal_stdlib>
#include <SwiftUI/SwiftUI_Metal.h>
#include "LiquidGlassCore.h"
using namespace metal;

// Reduce Transparency fills (spec/OPTICS.md §8), sRGB.
constant float3 LG_FALLBACK_LIGHT = float3(0.949020, 0.949020, 0.968627); // #F2F2F7
constant float3 LG_FALLBACK_DARK = float3(0.109804, 0.109804, 0.117647);  // #1C1C1E

inline float4 lgSample(SwiftUI::Layer layer, float2 p, float4 valid) {
    return float4(layer.sample(clamp(p, valid.xy, valid.zw)));
}

inline float4 lgUnpremultiply(float4 c) {
    return c.a > 1e-5 ? float4(clamp(c.rgb / c.a, 0.0, 1.0), c.a) : float4(0.0);
}

// The Regular material's wide fill blur, approximated from the frost layer
// (already blurred with sigma 7.33) as OPTICS §3 allows: a 2D Gaussian with
// `sigma` = sqrt(fillSigma² − 7.33²), integrated with two-node Gauss–Laguerre
// quadrature in r² (rings at 1.0824σ and 2.6131σ, weights 0.8536 and 0.1464),
// which preserves the kernel's variance. Taps clamp to the valid region.
inline float4 lgFill(SwiftUI::Layer layer, float2 q, float sigma, float4 valid) {
    float4 sum = float4(0.0);
    const float r1 = 1.0824 * sigma;
    const float r2 = 2.6131 * sigma;
    for (int i = 0; i < 12; i++) {
        float a = (float(i) + 0.5) * (2.0 * M_PI_F / 12.0);
        sum += 0.8536 / 12.0 * lgSample(layer, q + r1 * float2(cos(a), sin(a)), valid);
    }
    for (int i = 0; i < 16; i++) {
        float a = float(i) * (2.0 * M_PI_F / 16.0);
        sum += 0.1464 / 16.0 * lgSample(layer, q + r2 * float2(cos(a), sin(a)), valid);
    }
    return sum;
}

// One glass surface drawn over a copy of its backdrop.
//
// frame:    outline center in layer points (xy) and its unscaled size (zw)
// shape:    radius (negative = capsule), refraction, press/drag scale (zw)
// material: material (0 clear, 1 regular), dark, nested, pixel
// tint:     sRGB tint (rgb) and whether it is on (w)
// glow:     pointer position in layer points (xy), radius, amount
// extras:   opacity, contrast border, mode (0 glass, 1 opaque fallback,
//           2 rim only over a platform material), fill kernel sigma
// valid:    rectangle the backdrop copy is valid in (minX, minY, maxX, maxY)
[[ stitchable ]] half4 liquidGlass(float2 position, SwiftUI::Layer layer,
                                   float4 frame, float4 shape, float4 material,
                                   float4 tint, float4 glow, float4 extras, float4 valid) {
    float2 center = frame.xy;
    float2 size = frame.zw;
    float2 scale = max(shape.zw, float2(1e-3));
    float dark = material.y;
    float pixel = material.w;
    float opacity = extras.x;
    float mode = extras.z;

    // The outline is evaluated in its own unscaled space, the way a transform
    // would draw it; coverage and the rim use the screen-space distance so
    // edges stay one device pixel wide while the glass grows or stretches.
    float3 local = lgShapeField((position - center) / scale, size, lgShapeParams(size, shape.x));
    float2 gradient = local.yz / scale;
    float gradientLength = max(length(gradient), 1e-6);
    float3 field = float3(local.x / gradientLength, gradient / gradientLength);
    if (field.x > 1.0 + pixel) return half4(0.0);

    float4 src;
    if (mode > 1.5) {
        src = float4(0.0);
    } else if (mode > 0.5) {
        src = float4(dark > 0.5 ? LG_FALLBACK_DARK : LG_FALLBACK_LIGHT, 0.94);
    } else {
        float2 q = position + lgRefractionOffset(local, shape.y) * scale;
        src = lgUnpremultiply(lgSample(layer, q, valid));
        if (material.x > 0.5 && extras.w > 0.0) {
            float4 fill = lgUnpremultiply(lgFill(layer, q, extras.w, valid));
            src.rgb = lgFrostColor(src.rgb, fill.rgb, dark);
        }
    }

    float3 rgb = mode > 0.5 ? src.rgb : lgMaterialColor(src.rgb, material.x, dark, material.z);
    float a = src.a;
    if (tint.w > 0.5 && mode < 1.5) {
        float body = material.x > 0.5 ? 0.78 : 0.68;
        rgb = lgTintColor(rgb, tint.rgb, material.x);
        a = body + (1.0 - body) * a;
    }
    float cover = lgCoverage(field.x, pixel);
    float ga = a * cover * opacity;
    float4 result = float4(rgb * ga, ga);

    if (extras.y > 0.5) {
        // Increase Contrast: a one-point inner border.
        float band = cover - lgCoverage(field.x + 1.0, pixel);
        float4 border = (dark > 0.5 ? float4(0.28, 0.28, 0.28, 0.28) : float4(0.0, 0.0, 0.0, 0.18)) * band * opacity;
        result = border + result * (1.0 - border.a);
    }

    float4 rim = lgRimOverlay(field, dark, pixel, tint.rgb, tint.w) * opacity;
    result = rim + result * (1.0 - rim.a);

    if (glow.w > 0.0) {
        float4 light = lgGlow(position, glow.xy, glow.z, dark, glow.w, cover) * opacity;
        result = light + result * (1.0 - light.a);
    }
    return half4(result);
}
