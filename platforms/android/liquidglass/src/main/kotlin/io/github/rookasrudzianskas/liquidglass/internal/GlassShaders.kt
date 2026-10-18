package io.github.rookasrudzianskas.liquidglass.internal

import io.github.rookasrudzianskas.liquidglass.GlassDefaults

/** Surfaces one shader pass refracts; the uniform arrays below are sized to it. */
internal const val MAX_SURFACES: Int = GlassDefaults.MaxSurfacesPerScene

/** Material codes the shader understands. */
internal object ShaderMaterial {
    const val CLEAR = 0f
    const val REGULAR = 1f

    /** Reduce Transparency: an opaque fill in place of the material, rim kept. */
    const val SOLID = 2f
}

/** Which surfaces a pass draws: clear and solid glass over the sharp content, or Regular glass over a frost. */
internal object ShaderPass {
    const val SHARP = 0f
    const val FROST_LIGHT = 1f
    const val FROST_DARK = 2f
}

/**
 * AGSL translation of spec/shaders/liquid-glass.glsl, the canonical shader
 * (itself a line-by-line port of spec/reference.mjs).
 *
 * One program draws every registered surface of a scene. It runs as a
 * RenderEffect on the scene's content layer, so `content` is that layer:
 *
 * - The sharp pass (uPass 0) samples the content itself and draws Clear and
 *   solid glass over it; pixels no glass touches pass through untouched.
 * - A frost pass (uPass 1 light, 2 dark) runs on the frosted content (the
 *   σ 7.33 blur blended with the wide fill blur) and outputs only Regular glass
 *   of that appearance, premultiplied.
 *
 * The passes are added together. Each surface composites over everything below
 * it (the spec's z-order): `acc = own + acc · (1 − alpha)`. So every pass also
 * walks the surfaces the other passes draw and lets them hide, by their alpha
 * (glass, contrast border, rim light and its side shade, glow), what it drew
 * beneath them. Summed, the passes give exactly the z-ordered composite, also
 * for Clear glass over Regular glass, whose side shade lies outside its outline.
 *
 * AGSL hands colors to the shader premultiplied, in the working color space
 * of the destination. The optics are defined on sRGB gamma-encoded,
 * unpremultiplied values, so every sample is unpremultiplied, taken to linear
 * sRGB with toLinearSrgb() and encoded with the sRGB curve, and every result is
 * taken back the same way. On an ordinary sRGB window this round trip is exact;
 * on a wide-gamut window the glass itself is in the sRGB gamut, and pixels no
 * glass term reaches are passed through unconverted.
 *
 * Geometry is in px (layer space); the optics run in dp, as in the spec.
 *
 * One addition to the canonical shader: the refracted sample is filtered
 * (bilinear, and box-filtered along the normal where the bevel compresses the
 * backdrop), because the content image is point-sampled and the rim would
 * otherwise alias. On smooth backdrops the result equals the point sample.
 */
internal const val GLASS_SHADER: String = """
uniform shader content;

uniform float2 uSize;
uniform float uDensity;
uniform float uPass;
uniform int uCount;
uniform float4 uRect[$MAX_SURFACES];
uniform float4 uClip[$MAX_SURFACES];
uniform float4 uShape[$MAX_SURFACES];
uniform float4 uLook[$MAX_SURFACES];
uniform float4 uTint[$MAX_SURFACES];
uniform float4 uGlow[$MAX_SURFACES];
uniform float uLegibility[$MAX_SURFACES];

const float3 LG_LUMA = float3(0.213, 0.715, 0.072);

float lgSat(float x) { return clamp(x, 0.0, 1.0); }

// --- Color space -------------------------------------------------------------

float3 lgEncode(float3 l) {
  l = clamp(l, 0.0, 1.0);
  float3 lo = l * 12.92;
  float3 hi = 1.055 * pow(l, float3(1.0 / 2.4)) - 0.055;
  return mix(lo, hi, step(float3(0.0031308), l));
}

float3 lgDecode(float3 c) {
  float3 lo = c / 12.92;
  float3 hi = pow((c + 0.055) / 1.055, float3(2.4));
  return mix(lo, hi, step(float3(0.04045), c));
}

// Premultiplied working-space color to unpremultiplied gamma sRGB.
float4 lgToSrgb(half4 c) {
  float a = float(c.a);
  if (a <= 0.0) return float4(0.0);
  float3 rgb = float3(c.rgb) / a;
  return float4(lgEncode(float3(toLinearSrgb(half3(rgb)))), a);
}

float4 lgToSrgbPremul(half4 c) {
  float4 s = lgToSrgb(c);
  return float4(s.rgb * s.a, s.a);
}

// Premultiplied gamma sRGB to premultiplied working-space color.
half4 lgFromSrgb(float4 premul) {
  float a = premul.a;
  if (a <= 0.0) return half4(0.0);
  float3 rgb = clamp(premul.rgb / a, 0.0, 1.0);
  float3 w = float3(fromLinearSrgb(half3(lgDecode(rgb))));
  return half4(half3(w * a), half(a));
}

// --- Sampling ----------------------------------------------------------------

// The content image is point-sampled; refraction moves sample points smoothly,
// so filter bilinearly (on premultiplied values) to keep grain from aliasing.
half4 lgBilinear(float2 at) {
  float2 p = clamp(at, float2(0.5), uSize - 0.5) - 0.5;
  float2 f = fract(p);
  float2 i = floor(p) + 0.5;
  float2 hi = min(i + 1.0, uSize - 0.5);
  half4 a = content.eval(i);
  half4 b = content.eval(float2(hi.x, i.y));
  half4 c = content.eval(float2(i.x, hi.y));
  half4 d = content.eval(hi);
  return mix(mix(a, b, half(f.x)), mix(c, d, half(f.x)), half(f.y));
}

// The refracted sample. Toward the rim the bevel compresses the backdrop
// along the normal (one glass pixel spans `compression` content pixels), so
// box-filter across that footprint too.
half4 lgRefracted(float2 at, float2 normal, float compression) {
  if (compression < 1.5) return lgBilinear(at);
  float span = min(compression, 12.0);
  if (span < 3.0) {
    float2 delta = normal * (0.25 * span);
    return (lgBilinear(at - delta) + lgBilinear(at + delta)) * 0.5;
  }
  float2 near = normal * (0.125 * span);
  float2 far = normal * (0.375 * span);
  return (lgBilinear(at - far) + lgBilinear(at - near) + lgBilinear(at + near) + lgBilinear(at + far)) * 0.25;
}

// --- Shape -------------------------------------------------------------------

// Signed distance (negative inside) and outward normal at p from the center,
// y down. shape = (corner extent, superellipse exponent).
float3 lgShapeField(float2 p, float2 size, float2 shape) {
  float e = shape.x;
  float n = shape.y;
  float2 s = float2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
  float2 q = abs(p) - (size * 0.5 - e);
  if (q.x > 0.0 && q.y > 0.0) {
    float k = max(q.x, q.y);
    float2 u = q / k;
    float m = k * pow(pow(u.x, n) + pow(u.y, n), 1.0 / n);
    if (m < 1e-6) return float3(-e, s * 0.70710678);
    float2 g = pow(q / m, float2(n - 1.0));
    float gl = length(g);
    return float3((m - e) / gl, s * g / gl);
  }
  if (q.x > q.y) return float3(q.x - e, s.x, 0.0);
  return float3(q.y - e, 0.0, s.y);
}

// --- Refraction --------------------------------------------------------------

float lgEdgeProfile(float distance) {
  float depth = lgSat(-distance / 20.0);
  return 1.0 - lgSat(sqrt((2.0 - depth) * depth));
}

// Where a glass pixel samples its backdrop, relative to itself (dp).
float2 lgRefractionOffset(float3 field, float refraction) {
  return -field.yz * lgEdgeProfile(field.x) * refraction;
}

// How many backdrop pixels one bevel pixel spans along the normal:
// |1 - refraction * d(edgeProfile)/d(distance)|.
float lgCompression(float distance, float refraction) {
  float depth = lgSat(-distance / 20.0);
  if (depth <= 0.0 || depth >= 1.0) return 1.0;
  float slope = (1.0 - depth) / max(sqrt((2.0 - depth) * depth), 0.05) / 20.0;
  return abs(1.0 - refraction * slope);
}

float lgCoverage(float distance, float pixel) {
  return lgSat(0.5 - distance / pixel);
}

// --- Color -------------------------------------------------------------------

float3 lgSaturate(float3 c, float s) {
  return clamp(float3(
    (0.213 + 0.787 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 - 0.072 * s) * c.b,
    (0.213 - 0.213 * s) * c.r + (0.715 + 0.285 * s) * c.g + (0.072 - 0.072 * s) * c.b,
    (0.213 - 0.213 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 + 0.928 * s) * c.b), 0.0, 1.0);
}

float3 lgClearTone(float3 v, float dark, float nested) {
  float lift = mix(0.18, 0.15, dark);
  float rolloff = mix(0.0, 0.2, dark);
  float3 inv = 1.0 - v;
  float3 tone = v + lift * inv * inv * inv - rolloff * v * v * v;
  float3 veil = mix(0.78 * v + 0.26, 0.38 * v + 0.2, dark);
  float weight = mix(0.06, 0.14, dark);
  float3 layerLight = dark * nested * 0.08 * inv;
  return clamp((1.0 - weight) * tone + weight * veil + layerLight, 0.0, 1.0);
}

float3 lgRegularTone(float3 v, float dark) {
  float3 abq = mix(float3(0.36, 0.67, -0.1113), float3(0.105, 0.83, -0.2091), dark);
  float weight = mix(0.8, 0.9, dark);
  float3 toned = clamp(weight * (abq.x + abq.y * v + abq.z * v * v) + (1.0 - weight) * v, 0.0, 1.0);
  return lgSaturate(toned, mix(1.1031, 1.0648, dark));
}

// material: 0 = clear, 1 = regular.
float3 lgMaterialColor(float3 c, float material, float dark, float nested) {
  return material > 0.5 ? lgRegularTone(c, dark) : lgClearTone(c, dark, nested);
}

// Share of the pigment in tinted glass: Regular (and solid) 0.78, Clear 0.68.
float lgTintBody(float material) {
  return material > 0.5 ? 0.78 : 0.68;
}

// Colored glass transmits luminance through the pigment.
float3 lgTintColor(float3 c, float3 tint, float material) {
  float body = lgTintBody(material);
  return clamp(tint * (body + (1.0 - body) * dot(c, LG_LUMA)), 0.0, 1.0);
}

// Menu panels mix their glass toward a solid backing so text always reads:
// #F5F5F7 light, #1C1C1E dark. amount is 0.5 on menu panels, 0 elsewhere.
float3 lgLegibility(float3 c, float dark, float amount) {
  float3 backing = mix(float3(0.960784, 0.960784, 0.968627), float3(0.109804, 0.109804, 0.117647), dark);
  return mix(c, backing, amount);
}

// --- Rim light (premultiplied) -------------------------------------------------

float4 lgRimOverlay(float3 field, float dark, float pixel, float3 tint, float tintOn) {
  float d = field.x;
  float2 n = field.yz;
  float aa = max(pixel, 1e-6);
  float cover = lgSat(0.5 - d / aa);
  const float spread = 1.08;
  float directional = pow(lgSat((abs(n.y) - cos(spread)) / (1.0 - cos(spread))), 1.25);
  float diffuseDir = pow(lgSat((abs(n.y) - cos(spread * 0.65)) / (1.0 - cos(spread * 0.65))), 1.25);
  float stroke = lgSat((d + 0.9) / aa + 0.5) * lgSat(0.5 - d / aa);
  float specular = directional * stroke;
  specular = specular / (1.0 + 0.5 * (1.0 - specular));
  float inner = 1.0 - lgSat(-d / 4.5);
  float diffuse = diffuseDir * 0.18 * inner * inner * cover;
  float highlight = lgSat(specular * mix(1.0, 0.82, dark) + diffuse * mix(1.0, 0.4, dark));
  float adjusted = d - 0.5;
  float falloff = 1.0 - lgSat(-adjusted / 0.5);
  float ramp = mix(falloff > 0.0 ? 1.0 : 0.0, falloff, 0.75);
  float outline = ramp * lgSat((adjusted + 0.5) / aa + 0.5) * lgSat(-adjusted / aa + 0.5);
  const float sideSpread = 0.98;
  float2 w = clamp((float2(n.x, -n.x) - cos(sideSpread)) / (1.0 - cos(sideSpread)), 0.0, 1.0) * outline;
  float2 shaded = w / (1.0 + 0.4 * (1.0 - w));
  float shade = (shaded.x + shaded.y) * mix(0.48, 0.6, dark);
  float alpha = highlight + shade * (1.0 - highlight);
  float3 light = tintOn > 0.5 ? highlight * (0.45 + 0.55 * tint) : float3(mix(1.0, 0.82, dark) * highlight);
  return float4(light, alpha);
}

// --- Composite -----------------------------------------------------------------

// src: refracted backdrop (unpremultiplied); base: what lies beneath (premultiplied).
// material: 0 clear, 1 regular, 2 solid. look = (dark, nested, opacity, contrast).
// legibility: the share of the legibility backing (0.5 on menu panels, 0 elsewhere).
float4 lgCompose(float4 src, float4 base, float3 field, float material, float4 look, float4 tint, float pixel,
                 float legibility) {
  float dark = look.x;
  float opacity = look.z;
  bool solid = material > 1.5;
  // Solid glass (Reduce Transparency) takes the Regular tint body.
  float regular = material > 0.5 ? 1.0 : 0.0;
  float3 rgb;
  float a = src.a;
  if (solid) {
    rgb = mix(float3(0.94902, 0.94902, 0.96863), float3(0.10980, 0.10980, 0.11765), dark);
    a = 0.94;
  } else {
    rgb = lgMaterialColor(src.rgb, regular, dark, look.y);
  }
  if (tint.a > 0.5) {
    rgb = lgTintColor(rgb, tint.rgb, regular);
    float body = lgTintBody(regular);
    a = body + (1.0 - body) * a;
  }
  rgb = lgLegibility(rgb, dark, legibility);
  float ga = a * lgCoverage(field.x, pixel) * opacity;
  float4 result = float4(rgb * ga, ga) + base * (1.0 - ga);
  if (look.w > 0.5) {
    // Increase Contrast: a 1 dp inner border.
    float band = (lgSat(0.5 - field.x / pixel) - lgSat(0.5 - (field.x + 1.0) / pixel)) * opacity;
    float4 border = mix(float4(0.0, 0.0, 0.0, 0.18), float4(0.28, 0.28, 0.28, 0.28), dark) * band;
    result = border + result * (1.0 - border.a);
  }
  float4 rim = lgRimOverlay(field, dark, pixel, tint.rgb, tint.a) * opacity;
  return rim + result * (1.0 - rim.a);
}

// The pointer glow's alpha (white, over the composite). glow = (center, radius, amount) in dp.
float lgGlowAlpha(float4 glow, float2 p, float3 field, float4 look, float pixel) {
  if (glow.w <= 0.0) return 0.0;
  return mix(0.3, 0.16, look.x) * glow.w * (1.0 - lgSat(length(p - glow.xy) / glow.z))
      * lgCoverage(field.x, pixel) * look.z;
}

half4 main(float2 fragCoord) {
  half4 raw = content.eval(fragCoord);
  float pixel = 1.0 / uDensity;
  // Coverage, the contrast border, the rim light (and its side shade) and the glow are all zero
  // from this distance outside an outline on, so pixels beyond it are left exactly as they are.
  float edge = 0.5 + 0.5 * pixel;
  float reach = edge * uDensity + 1.0;
  bool frostPass = uPass > 0.5;
  float4 acc = float4(0.0);
  bool touched = false;
  for (int i = 0; i < $MAX_SURFACES; i++) {
    if (i >= uCount) break;
    float4 rect = uRect[i];
    float2 rel = fragCoord - rect.xy;
    float2 outside = abs(rel) - rect.zw;
    if (max(outside.x, outside.y) > reach) continue;
    float4 clipRect = uClip[i];
    if (fragCoord.x < clipRect.x || fragCoord.y < clipRect.y || fragCoord.x > clipRect.z || fragCoord.y > clipRect.w) continue;
    float4 shape = uShape[i];
    float4 look = uLook[i];
    float2 p = rel / uDensity;
    float3 field = lgShapeField(p, rect.zw * (2.0 / uDensity), shape.xy);
    if (field.x >= edge) continue;
    float material = shape.w;
    bool regular = material > 0.5 && material < 1.5;
    bool own = frostPass ? (regular && abs(look.x - (uPass - 1.0)) < 0.5) : !regular;
    if (!touched) {
      // A frost pass has nothing beneath glass it does not draw itself.
      if (frostPass && !own) continue;
      if (!frostPass) acc = lgToSrgbPremul(raw);
      touched = true;
    }
    if (!own) {
      // Glass another pass draws hides, by its own alpha, what this pass drew beneath it
      // (the backdrop's alpha stands in for its sample's: equal on opaque content).
      float hide = lgCompose(float4(0.0, 0.0, 0.0, float(raw.a)), float4(0.0), field, material, look, uTint[i], pixel, uLegibility[i]).a;
      float glowAlpha = lgGlowAlpha(uGlow[i], p, field, look, pixel);
      acc *= (1.0 - hide) * (1.0 - glowAlpha);
      continue;
    }
    bool refracts = material < 1.5;
    float2 offset = refracts ? lgRefractionOffset(field, shape.z) : float2(0.0);
    float compression = refracts ? lgCompression(field.x, shape.z) : 1.0;
    float4 src = lgToSrgb(lgRefracted(fragCoord + offset * uDensity, field.yz, compression));
    acc = lgCompose(src, acc, field, material, look, uTint[i], pixel, uLegibility[i]);
    float glowAlpha = lgGlowAlpha(uGlow[i], p, field, look, pixel);
    acc = float4(glowAlpha) + acc * (1.0 - glowAlpha);
  }
  if (!touched) return frostPass ? half4(0.0) : raw;
  return lgFromSrgb(acc);
}
"""
