// Liquid Glass Engineering: canonical shader library (GLSL ES 3.00).
//
// A line-by-line port of spec/reference.mjs. The Metal (SwiftUI), AGSL
// (Compose), HLSL (WPF) and WebGL2 (website) shaders are translations of this
// file; keep them in sync with it and with spec/vectors.json.
//
// Units: layout points. `pixel` is one device pixel in points (1 / scale).
// Colors: sRGB gamma-encoded. "premul" means premultiplied alpha.

const float LG_BEVEL = 20.0;
const float LG_CONTINUOUS_EXTENT = 1.528;
const float LG_CONTINUOUS_EXPONENT = 3.26;
const vec3 LG_LUMA = vec3(0.213, 0.715, 0.072);

float lgSat(float x) { return clamp(x, 0.0, 1.0); }

// ---------------------------------------------------------------------------
// Shape

// Corner extent and superellipse exponent. A negative radius means capsule.
vec2 lgShapeParams(vec2 size, float radius) {
  float halfMin = max(0.0, min(size.x, size.y) * 0.5);
  if (radius < 0.0) return vec2(halfMin, 2.0);
  float raw = LG_CONTINUOUS_EXTENT * radius;
  float extent = min(raw, halfMin);
  float t = halfMin > 0.0 ? smoothstep(0.75, 1.0, raw / halfMin) : 1.0;
  return vec2(extent, mix(LG_CONTINUOUS_EXPONENT, 2.0, t));
}

// Signed distance (negative inside) and outward normal at p, measured from
// the shape center with y down. Returns vec3(distance, normal.x, normal.y).
vec3 lgShapeField(vec2 p, vec2 size, vec2 shape) {
  float e = shape.x;
  float n = shape.y;
  vec2 s = vec2(p.x < 0.0 ? -1.0 : 1.0, p.y < 0.0 ? -1.0 : 1.0);
  vec2 q = abs(p) - (size * 0.5 - e);
  if (q.x > 0.0 && q.y > 0.0) {
    // Normalize by the larger component so pow() never overflows.
    float k = max(q.x, q.y);
    vec2 u = q / k;
    float m = k * pow(pow(u.x, n) + pow(u.y, n), 1.0 / n);
    if (m < 1e-6) return vec3(-e, s * 0.70710678);
    vec2 g = pow(q / m, vec2(n - 1.0));
    float gl = length(g);
    return vec3((m - e) / gl, s * g / gl);
  }
  if (q.x > q.y) return vec3(q.x - e, s.x, 0.0);
  return vec3(q.y - e, 0.0, s.y);
}

// ---------------------------------------------------------------------------
// Refraction

float lgEdgeProfile(float distance) {
  float depth = lgSat(-distance / LG_BEVEL);
  return 1.0 - lgSat(sqrt((2.0 - depth) * depth));
}

// Offset from the pixel to the backdrop point it shows.
vec2 lgRefractionOffset(vec3 field, float refraction) {
  return -field.yz * lgEdgeProfile(field.x) * refraction;
}

float lgCoverage(float distance, float pixel) {
  return lgSat(0.5 - distance / pixel);
}

// ---------------------------------------------------------------------------
// Color

vec3 lgSaturate(vec3 c, float s) {
  return clamp(vec3(
    (0.213 + 0.787 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 - 0.072 * s) * c.b,
    (0.213 - 0.213 * s) * c.r + (0.715 + 0.285 * s) * c.g + (0.072 - 0.072 * s) * c.b,
    (0.213 - 0.213 * s) * c.r + (0.715 - 0.715 * s) * c.g + (0.072 + 0.928 * s) * c.b), 0.0, 1.0);
}

vec3 lgClearTone(vec3 v, float dark, float nested) {
  float lift = mix(0.18, 0.15, dark);
  float rolloff = mix(0.0, 0.2, dark);
  vec3 inv = 1.0 - v;
  vec3 tone = v + lift * inv * inv * inv - rolloff * v * v * v;
  vec3 veil = mix(0.78 * v + 0.26, 0.38 * v + 0.2, dark);
  float weight = mix(0.06, 0.14, dark);
  vec3 layerLight = dark * nested * 0.08 * inv;
  return clamp((1.0 - weight) * tone + weight * veil + layerLight, 0.0, 1.0);
}

vec3 lgRegularTone(vec3 v, float dark) {
  vec3 abq = mix(vec3(0.36, 0.67, -0.1113), vec3(0.105, 0.83, -0.2091), dark);
  float weight = mix(0.8, 0.9, dark);
  vec3 toned = clamp(weight * (abq.x + abq.y * v + abq.z * v * v) + (1.0 - weight) * v, 0.0, 1.0);
  return lgSaturate(toned, mix(1.1031, 1.0648, dark));
}

// material: 0 = clear, 1 = regular.
vec3 lgMaterialColor(vec3 c, float material, float dark, float nested) {
  return material > 0.5 ? lgRegularTone(c, dark) : lgClearTone(c, dark, nested);
}

vec3 lgTintColor(vec3 c, vec3 tint, float material) {
  float body = material > 0.5 ? 0.78 : 0.68;
  return clamp(tint * (body + (1.0 - body) * dot(c, LG_LUMA)), 0.0, 1.0);
}

// Regular glass: `frost` is the backdrop blurred with sigma 7.33, `fill` with
// sigma 23 (light) or 21.8 (dark).
vec3 lgFrostColor(vec3 frost, vec3 fill, float dark) {
  vec3 blended = dark > 0.5 ? min(frost, fill) : max(frost, fill);
  float opacity = mix(0.34, 0.65, dark);
  return opacity * blended + (1.0 - opacity) * frost;
}

// Menu panels mix their glass toward a solid backing so text always reads:
// #F5F5F7 light, #1C1C1E dark. amount is 0.6 on menu panels (times the open
// progress of the menu), 0 elsewhere.
vec3 lgLegibility(vec3 c, float dark, float amount) {
  vec3 backing = mix(vec3(0.960784, 0.960784, 0.968627), vec3(0.109804, 0.109804, 0.117647), dark);
  return mix(c, backing, amount);
}

// ---------------------------------------------------------------------------
// Rim light (premul). tintOn: 0 or 1.

vec4 lgRimOverlay(vec3 field, float dark, float pixel, vec3 tint, float tintOn) {
  float d = field.x;
  vec2 n = field.yz;
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
  vec2 w = clamp((vec2(n.x, -n.x) - cos(sideSpread)) / (1.0 - cos(sideSpread)), 0.0, 1.0) * outline;
  vec2 shaded = w / (1.0 + 0.4 * (1.0 - w));
  float shade = (shaded.x + shaded.y) * mix(0.48, 0.6, dark);
  float alpha = highlight + shade * (1.0 - highlight);
  vec3 light = tintOn > 0.5 ? highlight * (0.45 + 0.55 * tint) : vec3(mix(1.0, 0.82, dark) * highlight);
  return vec4(light, alpha);
}

// ---------------------------------------------------------------------------
// Composite

// src: refracted backdrop at p + lgRefractionOffset (unpremul; regular glass
// passes lgFrostColor). base: unrefracted backdrop at p (premul).
vec4 lgCompose(vec4 src, vec4 base, vec3 field, float material, float dark, float nested,
               vec3 tint, float tintOn, float pixel, float legibility) {
  vec3 rgb = lgMaterialColor(src.rgb, material, dark, nested);
  float a = src.a;
  if (tintOn > 0.5) {
    float body = material > 0.5 ? 0.78 : 0.68;
    rgb = lgTintColor(rgb, tint, material);
    a = body + (1.0 - body) * a;
  }
  rgb = lgLegibility(rgb, dark, legibility);
  float ga = a * lgCoverage(field.x, pixel);
  vec4 result = vec4(rgb * ga, ga) + base * (1.0 - ga);
  vec4 rim = lgRimOverlay(field, dark, pixel, tint, tintOn);
  return rim + result * (1.0 - rim.a);
}

// Pointer glow for interactive glass, premul white. Paint after lgCompose,
// clipped to coverage. glowAmount is the glow spring value (0..1).
vec4 lgGlow(vec2 p, vec2 center, float radius, float dark, float glowAmount, float cover) {
  float alpha = mix(0.3, 0.16, dark) * glowAmount * (1.0 - lgSat(length(p - center) / radius)) * cover;
  return vec4(vec3(alpha), alpha);
}
