// WebGL2 programs for the website's live glass. The optical functions come
// verbatim from spec/shaders/liquid-glass.glsl (the canonical GLSL ES 3.00
// library every native port is translated from); this file only adds the
// passes a browser needs around it: backdrop fit, downsample, separable
// Gaussian blur, copy, and the per-surface composite.

import library from "@lg/shader?raw";

const HEADER = "#version 300 es\nprecision highp float;\nprecision highp sampler2D;\n";

/** Fullscreen triangle. vPos is in CSS pixels with y down; vUV is GL texture space. */
export const FULLSCREEN_VS = `${HEADER}
in vec2 aPos;
uniform vec2 uViewport;
out vec2 vPos;
out vec2 vUV;
void main() {
  vUV = aPos * 0.5 + 0.5;
  vPos = vec2(vUV.x, 1.0 - vUV.y) * uViewport;
  gl_Position = vec4(aPos, 0.0, 1.0);
}`;

/** Cover-fits up to two images (cross-fading between them) into the scene. */
export const BACKDROP_FS = `${HEADER}
uniform sampler2D uImageA;
uniform sampler2D uImageB;
uniform vec4 uFitA;
uniform vec4 uFitB;
uniform float uMix;
in vec2 vPos;
in vec2 vUV;
out vec4 outColor;
vec3 fitted(sampler2D image, vec4 fit) {
  return texture(image, (vPos - fit.xy) / fit.zw).rgb;
}
void main() {
  vec3 a = fitted(uImageA, uFitA);
  vec3 color = uMix > 0.0 ? mix(a, fitted(uImageB, uFitB), uMix) : a;
  outColor = vec4(color, 1.0);
}`;

/** 4-tap box downsample: each bilinear tap averages 2x2 source texels. */
export const DOWNSAMPLE_FS = `${HEADER}
uniform sampler2D uSrc;
uniform vec2 uOffset;
in vec2 vPos;
in vec2 vUV;
out vec4 outColor;
void main() {
  outColor = 0.25 * (
    texture(uSrc, vUV + vec2(-uOffset.x, -uOffset.y)) +
    texture(uSrc, vUV + vec2(uOffset.x, -uOffset.y)) +
    texture(uSrc, vUV + vec2(-uOffset.x, uOffset.y)) +
    texture(uSrc, vUV + uOffset));
}`;

export const MAX_BLUR_TAPS = 24;

/** One axis of a Gaussian, using linear-filtered paired taps. */
export const BLUR_FS = `${HEADER}
uniform sampler2D uSrc;
uniform vec2 uStep;
uniform float uCenter;
uniform float uWeights[${MAX_BLUR_TAPS}];
uniform float uOffsets[${MAX_BLUR_TAPS}];
uniform int uCount;
in vec2 vPos;
in vec2 vUV;
out vec4 outColor;
void main() {
  vec4 color = texture(uSrc, vUV) * uCenter;
  for (int i = 0; i < ${MAX_BLUR_TAPS}; i++) {
    if (i >= uCount) break;
    vec2 o = uStep * uOffsets[i];
    color += (texture(uSrc, vUV + o) + texture(uSrc, vUV - o)) * uWeights[i];
  }
  outColor = color;
}`;

export const COPY_FS = `${HEADER}
uniform sampler2D uSrc;
in vec2 vPos;
in vec2 vUV;
out vec4 outColor;
void main() {
  outColor = vec4(texture(uSrc, vUV).rgb, 1.0);
}`;

/** A quad covering one glass surface (CSS pixels, y down). */
export const GLASS_VS = `${HEADER}
in vec2 aPos;
uniform vec4 uRect;
uniform vec2 uViewport;
out vec2 vPos;
void main() {
  vPos = uRect.xy + aPos * uRect.zw;
  gl_Position = vec4(vPos.x / uViewport.x * 2.0 - 1.0, 1.0 - vPos.y / uViewport.y * 2.0, 0.0, 1.0);
}`;

/**
 * One glass surface, composited over the target with premultiplied blending
 * (ONE, ONE_MINUS_SRC_ALPHA). Because blending supplies the unrefracted
 * backdrop, lgCompose gets base = 0 and the result is identical to
 * OPTICS.md section 5: rim + (glass + base * (1 - ga)) * (1 - rim.a).
 */
export const GLASS_FS = `${HEADER}
${library}
uniform sampler2D uScene;
uniform sampler2D uFrost;
uniform sampler2D uFill;
uniform vec2 uViewport;
uniform vec2 uCenter;
uniform vec2 uSize;
uniform float uRadius;
uniform float uRefraction;
uniform float uMaterial;
uniform float uDark;
uniform float uNested;
uniform vec3 uTint;
uniform float uTintOn;
uniform float uOpacity;
uniform float uPixel;
uniform vec3 uGlow;
uniform float uGlowRadius;
uniform float uContrast;
in vec2 vPos;
out vec4 outColor;

vec2 toUV(vec2 p) {
  return vec2(p.x / uViewport.x, 1.0 - p.y / uViewport.y);
}

float hash(vec2 p) {
  vec3 p3 = fract(vec3(p.xyx) * 0.1031);
  p3 += dot(p3, p3.yzx + 33.33);
  return fract((p3.x + p3.y) * p3.z);
}

void main() {
  vec2 p = vPos - uCenter;
  vec3 field = lgShapeField(p, uSize, lgShapeParams(uSize, uRadius));
  if (field.x > 1.0 + 2.0 * uPixel) discard;

  vec2 uv = toUV(vPos + lgRefractionOffset(field, uRefraction));
  vec4 src;
  if (uMaterial > 0.5) {
    vec3 frost = texture(uFrost, uv).rgb;
    vec3 fill = texture(uFill, uv).rgb;
    src = vec4(lgFrostColor(frost, fill, uDark), 1.0);
  } else {
    src = vec4(texture(uScene, uv).rgb, 1.0);
  }

  vec4 color = lgCompose(src, vec4(0.0), field, uMaterial, uDark, uNested, uTint, uTintOn, uPixel);

  // Increase contrast (OPTICS.md section 8): a 1-unit inner border.
  if (uContrast > 0.5) {
    float inside = lgCoverage(field.x, uPixel);
    float ring = inside * lgSat((field.x + 1.0) / uPixel + 0.5);
    vec4 border = uDark > 0.5 ? vec4(vec3(0.28), 0.28) : vec4(0.0, 0.0, 0.0, 0.18);
    color = border * ring + color * (1.0 - border.a * ring);
  }

  float cover = lgCoverage(field.x, uPixel);
  vec4 glow = lgGlow(p, uGlow.xy, uGlowRadius, uDark, uGlow.z, cover);
  color = glow + color * (1.0 - glow.a);

  // Dither the smooth frosted gradients so 8-bit output never bands.
  color.rgb += (hash(gl_FragCoord.xy) - 0.5) / 255.0 * color.a;
  outColor = color * uOpacity;
}`;

/** Solid shapes drawn into the scene (tracks, fills), so glass refracts them. */
export const CONTENT_FS = `${HEADER}
${library}
uniform vec2 uCenter;
uniform vec2 uSize;
uniform float uRadius;
uniform float uPixel;
uniform vec4 uColor;
in vec2 vPos;
out vec4 outColor;
void main() {
  vec3 field = lgShapeField(vPos - uCenter, uSize, lgShapeParams(uSize, uRadius));
  float cover = lgCoverage(field.x, uPixel);
  if (cover <= 0.0) discard;
  outColor = vec4(uColor.rgb * uColor.a, uColor.a) * cover;
}`;
