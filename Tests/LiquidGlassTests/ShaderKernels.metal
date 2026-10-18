// Test kernels: evaluate the library's shader functions on the GPU so the
// tests can check them against spec/vectors.json, plus a backdrop for the
// end-to-end SwiftUI render test.

#include <metal_stdlib>
#include <SwiftUI/SwiftUI_Metal.h>
#include "../../Sources/LiquidGlass/Shaders/LiquidGlassCore.h"
using namespace metal;

constant uint LG_TEST_STRIDE = 16;

// The linear backdrop of the glassPixel vectors, relative to the surface center.
inline float4 lgTestBackdropColor(float2 p) {
    return float4(lgSat(0.5 + p.x / 400.0), lgSat(0.5 + p.y / 400.0), 0.6, 1.0);
}

// Each case is 16 floats of input; `mode` picks the function.
kernel void lgTestEvaluate(device const float *input [[buffer(0)]],
                           device float4 *output [[buffer(1)]],
                           constant uint &mode [[buffer(2)]],
                           uint id [[thread_position_in_grid]]) {
    device const float *a = input + id * LG_TEST_STRIDE;
    switch (mode) {
    case 0: { // shape field: x, y, width, height, radius (negative = capsule)
        float2 size = float2(a[2], a[3]);
        output[id] = float4(lgShapeField(float2(a[0], a[1]), size, lgShapeParams(size, a[4])), 0.0);
        break;
    }
    case 1: { // edge profile and refraction offset: distance, nx, ny, refraction
        float3 field = float3(a[0], a[1], a[2]);
        output[id] = float4(lgEdgeProfile(a[0]), lgRefractionOffset(field, a[3]), 0.0);
        break;
    }
    case 2: { // material color: rgb, material, dark, nested
        output[id] = float4(lgMaterialColor(float3(a[0], a[1], a[2]), a[3], a[4], a[5]), 0.0);
        break;
    }
    case 3: { // tint: rgb, tint rgb, material
        output[id] = float4(lgTintColor(float3(a[0], a[1], a[2]), float3(a[3], a[4], a[5]), a[6]), 0.0);
        break;
    }
    case 4: { // frost: frost rgb, fill rgb, dark
        output[id] = float4(lgFrostColor(float3(a[0], a[1], a[2]), float3(a[3], a[4], a[5]), a[6]), 0.0);
        break;
    }
    case 5: { // rim: distance, nx, ny, dark, pixel, tint rgb, tint on
        output[id] = lgRimOverlay(float3(a[0], a[1], a[2]), a[3], a[4], float3(a[5], a[6], a[7]), a[8]);
        break;
    }
    case 6: { // glass pixel: x, y, width, height, radius, material, dark, refraction, pixel, tint rgb, tint on, legibility
        float2 p = float2(a[0], a[1]);
        float2 size = float2(a[2], a[3]);
        float3 field = lgShapeField(p, size, lgShapeParams(size, a[4]));
        float4 src = lgTestBackdropColor(p + lgRefractionOffset(field, a[7]));
        output[id] = lgCompose(src, lgTestBackdropColor(p), field, a[5], a[6], 0.0, float3(a[9], a[10], a[11]), a[12], a[8], a[13]);
        break;
    }
    case 7: { // legibility backing mix: rgb, dark, amount
        output[id] = float4(lgLegibility(float3(a[0], a[1], a[2]), a[3], a[4]), 0.0);
        break;
    }
    case 8: { // menu glass: rgb, material, dark, legibility (material color, then the backing mix)
        output[id] = float4(lgLegibility(lgMaterialColor(float3(a[0], a[1], a[2]), a[3], a[4], 0.0), a[4], a[5]), 0.0);
        break;
    }
    case 9: { // menu glass, in the shader's order: rgb, material, dark, tint rgb, tint on, legibility, material color on
        float3 rgb = float3(a[0], a[1], a[2]);
        if (a[10] > 0.5) rgb = lgMaterialColor(rgb, a[3], a[4], 0.0);
        if (a[8] > 0.5) rgb = lgTintColor(rgb, float3(a[5], a[6], a[7]), a[3]);
        output[id] = float4(lgLegibility(rgb, a[4], a[9]), 0.0);
        break;
    }
    default:
        output[id] = float4(NAN);
    }
}

// The same backdrop as a SwiftUI color effect, centered on `center`.
[[ stitchable ]] half4 lgTestBackdrop(float2 position, half4 color, float2 center) {
    return half4(lgTestBackdropColor(position - center));
}

// A smooth backdrop for the Regular material: a horizontal sine of
// `wavelength` points in red, whose Gaussian blurs are known in closed form.
[[ stitchable ]] half4 lgTestWave(float2 position, half4 color, float wavelength) {
    float r = 0.5 + 0.3 * sin(2.0 * M_PI_F * position.x / wavelength);
    return half4(r, 0.45, 0.6, 1.0);
}

