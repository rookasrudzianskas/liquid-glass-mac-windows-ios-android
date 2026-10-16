// Liquid Glass Engineering: canonical optical reference.
//
// Every platform port (SwiftUI/Metal, Jetpack Compose/AGSL, WPF/HLSL and the
// website's WebGL2 playground) implements exactly these functions. Units are
// layout points (pt on Apple, dp on Android, DIP on Windows, CSS px on the
// web). Colors are sRGB gamma-encoded channels in [0, 1], unpremultiplied
// unless a name says otherwise. `spec/vectors.json` is generated from this
// file and every platform's unit tests check their port against it.
//
// The constants are ported from the MIT-licensed Glass HQ liquid-glass web
// library (packages/liquid-glass: gpu/maps.wgsl, core/materials.ts,
// dom/material-graph.ts, core/spring.ts, dom/interaction.ts).

/** Depth of the refracting bevel inside the outline. */
export const BEVEL = 20;
/** Default optical travel at the outline. */
export const DEFAULT_REFRACTION = 60;
/** Continuous corners reach this far along each edge, relative to the radius. */
export const CONTINUOUS_EXTENT = 1.528;
/** Superellipse exponent of a continuous corner (2 is a circular arc). */
export const CONTINUOUS_EXPONENT = 3.26;

export const clamp = (x, lo, hi) => Math.min(hi, Math.max(lo, x));
export const sat = (x) => clamp(x, 0, 1);
export const mix = (a, b, t) => a + (b - a) * t;
export const smoothstep = (e0, e1, x) => {
  const t = sat((x - e0) / (e1 - e0));
  return t * t * (3 - 2 * t);
};

export const materials = {
  clear: {
    blur: 0,
    light: { shadowLift: 0.18, highlightRolloff: 0 },
    dark: { shadowLift: 0.15, highlightRolloff: 0.2 },
    tintBody: 0.68,
  },
  regular: {
    blur: 7.33,
    light: { fillSigma: 23, fillOpacity: 0.34, tone: [0.36, 0.67, -0.1113], chroma: 1.1031, toneWeight: 0.8 },
    dark: { fillSigma: 21.8, fillOpacity: 0.65, tone: [0.105, 0.83, -0.2091], chroma: 1.0648, toneWeight: 0.9 },
    tintBody: 0.78,
  },
};

// ---------------------------------------------------------------------------
// Shape

/**
 * Corner geometry of a glass outline.
 * `radius` is a nonnegative number, "capsule" or "circle" (both fully round).
 * Returns the corner `extent` along each edge and the superellipse `exponent`.
 * Large radii blend toward a circular capsule so animating the radius never
 * jumps.
 */
export function shapeParams(width, height, radius) {
  const half = Math.max(0, Math.min(width, height) / 2);
  if (radius === "capsule" || radius === "circle") return { extent: half, exponent: 2 };
  if (typeof radius !== "number" || !Number.isFinite(radius) || radius < 0)
    throw new RangeError("radius must be a finite nonnegative number, 'capsule' or 'circle'");
  const raw = CONTINUOUS_EXTENT * radius;
  const extent = Math.min(raw, half);
  const t = half > 0 ? smoothstep(0.75, 1, raw / half) : 1;
  return { extent, exponent: mix(CONTINUOUS_EXPONENT, 2, t) };
}

/**
 * Signed distance (negative inside) and outward unit normal of the outline at
 * (x, y), measured from the shape's center with y pointing down.
 * Straight edges are exact; superellipse corners use the first-order
 * distance f / |∇f|, which is exact on the outline and for exponent 2.
 */
export function shapeField(x, y, width, height, extent, exponent) {
  const sx = x < 0 ? -1 : 1;
  const sy = y < 0 ? -1 : 1;
  const qx = Math.abs(x) - (width / 2 - extent);
  const qy = Math.abs(y) - (height / 2 - extent);
  if (qx > 0 && qy > 0) {
    const n = exponent;
    const m = Math.pow(Math.pow(qx, n) + Math.pow(qy, n), 1 / n);
    if (m < 1e-6) return { distance: -extent, nx: sx * Math.SQRT1_2, ny: sy * Math.SQRT1_2 };
    const gx = Math.pow(qx / m, n - 1);
    const gy = Math.pow(qy / m, n - 1);
    const g = Math.hypot(gx, gy);
    return { distance: (m - extent) / g, nx: (sx * gx) / g, ny: (sy * gy) / g };
  }
  if (qx > qy) return { distance: qx - extent, nx: sx, ny: 0 };
  return { distance: qy - extent, nx: 0, ny: sy };
}

/** Outline polygon, clockwise from the top edge's start, for clipping paths.
 * `segments` points per corner. Platforms build their clip Shape from this. */
export function shapeOutline(width, height, radius, segments = 24) {
  const { extent: e, exponent: n } = shapeParams(width, height, radius);
  const points = [];
  // Corner centers in y-down coordinates, with the quadrant signs of each.
  const corners = [
    [width - e, e, 1, -1, -Math.PI / 2, 0], // top-right: from top edge to right edge
    [width - e, height - e, 1, 1, 0, Math.PI / 2], // bottom-right
    [e, height - e, -1, 1, Math.PI / 2, Math.PI], // bottom-left
    [e, e, -1, -1, Math.PI, (3 * Math.PI) / 2], // top-left
  ];
  for (const [cx, cy, , , a0, a1] of corners) {
    for (let i = 0; i <= segments; i++) {
      const a = a0 + ((a1 - a0) * i) / segments;
      const c = Math.cos(a), s = Math.sin(a);
      // Superellipse |x|^n + |y|^n = e^n in parametric form.
      const px = e * Math.sign(c) * Math.pow(Math.abs(c), 2 / n);
      const py = e * Math.sign(s) * Math.pow(Math.abs(s), 2 / n);
      points.push([cx + px, cy + py]);
    }
  }
  return points;
}

// ---------------------------------------------------------------------------
// Refraction

/** Displacement weight: 1 on the outline, falling to 0 at BEVEL inside. */
export function edgeProfile(distance, bevel = BEVEL) {
  const depth = sat(-distance / bevel);
  return 1 - sat(Math.sqrt((2 - depth) * depth));
}

/** Where a glass pixel samples its backdrop, relative to itself. */
export function refractionOffset(distance, nx, ny, refraction = DEFAULT_REFRACTION) {
  const e = edgeProfile(distance);
  return { dx: -nx * e * refraction, dy: -ny * e * refraction };
}

/** Antialiased coverage of the outline; `pixel` is one device pixel in points. */
export function coverage(distance, pixel) {
  return sat(0.5 - distance / pixel);
}

// ---------------------------------------------------------------------------
// Color

const LUMA = [0.213, 0.715, 0.072];
export const luma = (r, g, b) => LUMA[0] * r + LUMA[1] * g + LUMA[2] * b;

/** SVG feColorMatrix type="saturate". */
export function saturate([r, g, b], s) {
  return [
    sat((0.213 + 0.787 * s) * r + (0.715 - 0.715 * s) * g + (0.072 - 0.072 * s) * b),
    sat((0.213 - 0.213 * s) * r + (0.715 + 0.285 * s) * g + (0.072 - 0.072 * s) * b),
    sat((0.213 - 0.213 * s) * r + (0.715 - 0.715 * s) * g + (0.072 + 0.928 * s) * b),
  ];
}

/** Clear material: lift shadows, roll off highlights, add a faint veil. */
export function clearTone(v, dark, nested = false) {
  const spec = dark ? materials.clear.dark : materials.clear.light;
  const tone = v + spec.shadowLift * (1 - v) ** 3 - spec.highlightRolloff * v ** 3;
  const veil = dark ? 0.38 * v + 0.2 : 0.78 * v + 0.26;
  const weight = dark ? 0.14 : 0.06;
  const layerLight = dark && nested ? 0.08 * (1 - v) : 0;
  return sat((1 - weight) * tone + weight * veil + layerLight);
}

/** Regular material: the calibrated per-channel curve, before saturation. */
export function regularTone(v, dark) {
  const spec = dark ? materials.regular.dark : materials.regular.light;
  const [a, b, q] = spec.tone;
  return sat(spec.toneWeight * (a + b * v + q * v * v) + (1 - spec.toneWeight) * v);
}

/** Full material color response on an unpremultiplied sRGB color. */
export function materialColor([r, g, b], material, dark, nested = false) {
  if (material === "regular") {
    const spec = dark ? materials.regular.dark : materials.regular.light;
    return saturate([regularTone(r, dark), regularTone(g, dark), regularTone(b, dark)], spec.chroma);
  }
  return [clearTone(r, dark, nested), clearTone(g, dark, nested), clearTone(b, dark, nested)];
}

/** Colored glass transmits luminance through the pigment. */
export function tintColor([r, g, b], tint, material) {
  const body = material === "regular" ? materials.regular.tintBody : materials.clear.tintBody;
  const l = luma(r, g, b);
  return tint.map((t) => sat(t * (body + (1 - body) * l)));
}

/**
 * Regular glass refracts a frosted backdrop: a σ = 7.33 blur, brightened
 * (light) or deepened (dark) by a wide fill blur.
 * `frost` and `fill` are the two blurred backdrop colors at the same point.
 */
export function frostColor(frost, fill, dark) {
  const spec = dark ? materials.regular.dark : materials.regular.light;
  return frost.map((f, i) => {
    const blended = dark ? Math.min(f, fill[i]) : Math.max(f, fill[i]);
    return spec.fillOpacity * blended + (1 - spec.fillOpacity) * f;
  });
}

// ---------------------------------------------------------------------------
// Rim light

/**
 * The rim overlay painted over the glass: a specular line along the top and
 * bottom, a soft inner diffuse glow, and a shade on the sides.
 * Returns premultiplied [r, g, b, a].
 */
export function rimOverlay(distance, nx, ny, dark, pixel, tint) {
  const d = distance;
  const aa = Math.max(pixel, 1e-6);
  const k = dark ? 1 : 0;
  const cover = sat(0.5 - d / aa);
  const spread = 1.08;
  const directional = Math.pow(sat((Math.abs(ny) - Math.cos(spread)) / (1 - Math.cos(spread))), 1.25);
  const diffuseDir = Math.pow(sat((Math.abs(ny) - Math.cos(spread * 0.65)) / (1 - Math.cos(spread * 0.65))), 1.25);
  const stroke = sat((d + 0.9) / aa + 0.5) * sat(0.5 - d / aa);
  let specular = directional * stroke;
  specular = specular / (1 + 0.5 * (1 - specular));
  const diffuse = diffuseDir * 0.18 * Math.pow(1 - sat(-d / 4.5), 2) * cover;
  const highlight = sat(specular * mix(1, 0.82, k) + diffuse * mix(1, 0.4, k));
  const adjusted = d - 0.5;
  const falloff = 1 - sat(-adjusted / 0.5);
  const ramp = mix(falloff > 0 ? 1 : 0, falloff, 0.75);
  const outline = ramp * sat((adjusted + 0.5) / aa + 0.5) * sat(-adjusted / aa + 0.5);
  const cs = Math.cos(0.98);
  const w1 = sat((nx - cs) / (1 - cs)) * outline;
  const w2 = sat((-nx - cs) / (1 - cs)) * outline;
  const shade = (w1 / (1 + 0.4 * (1 - w1)) + w2 / (1 + 0.4 * (1 - w2))) * mix(0.48, 0.6, k);
  const alpha = highlight + shade * (1 - highlight);
  if (tint) return [...tint.map((t) => highlight * (0.45 + 0.55 * t)), alpha];
  const light = mix(1, 0.82, k) * highlight;
  return [light, light, light, alpha];
}

// ---------------------------------------------------------------------------
// One glass pixel

/**
 * Composite one pixel of a glass surface.
 * - `local`: pixel center relative to the surface center, y down.
 * - `sample(dx, dy)`: backdrop color (unpremultiplied [r, g, b, a]) at the
 *   pixel offset by (dx, dy). Regular glass samples its frosted backdrop.
 * - `base`: the unrefracted backdrop at the pixel, premultiplied.
 * Returns the premultiplied result.
 */
export function glassPixel({ x, y, width, height, radius, material = "clear", dark = false, refraction = DEFAULT_REFRACTION, tint, pixel = 1 / 3, nested = false, sample, base }) {
  const { extent, exponent } = shapeParams(width, height, radius);
  const f = shapeField(x, y, width, height, extent, exponent);
  const cover = coverage(f.distance, pixel);
  const o = refractionOffset(f.distance, f.nx, f.ny, refraction);
  const src = sample(o.dx, o.dy);
  let rgb = materialColor(src.slice(0, 3), material, dark, nested);
  let a = src[3];
  if (tint) {
    rgb = tintColor(rgb, tint, material);
    const body = material === "regular" ? materials.regular.tintBody : materials.clear.tintBody;
    a = body + (1 - body) * a;
  }
  const ga = a * cover;
  let out = [rgb[0] * ga, rgb[1] * ga, rgb[2] * ga, ga].map((g, i) => g + base[i] * (1 - ga));
  const rim = rimOverlay(f.distance, f.nx, f.ny, dark, pixel, tint);
  out = out.map((v, i) => rim[i] + v * (1 - rim[3]));
  return out;
}

// ---------------------------------------------------------------------------
// Motion

/** Named springs: perceptual duration (s) and bounce in (-1, 1). */
export const springs = {
  press: { duration: 0.24, bounce: 0.2 },
  release: { duration: 0.5, bounce: 0.42 },
  glow: { duration: 0.42, bounce: 0 },
  morph: { duration: 0.42, bounce: 0.18 },
  layout: { duration: 0.4, bounce: 0.16 },
  track: { duration: 0.18, bounce: 0 },
};

export function springCoefficients({ duration, bounce }) {
  const omega = (2 * Math.PI) / duration;
  const stiffness = omega * omega;
  const damping = bounce >= 0 ? (4 * Math.PI * (1 - bounce)) / duration : (4 * Math.PI) / (duration * (1 + bounce));
  return { stiffness, damping };
}

/** Advance a damped spring by dt seconds, in closed form. */
export function springStep(state, options, dt) {
  const { value, velocity, target } = state;
  if ((value === target && velocity === 0) || !(dt > 0)) return { ...state };
  const { stiffness, damping } = springCoefficients(options);
  const omega = Math.sqrt(stiffness);
  const zeta = damping / (2 * omega);
  const x0 = value - target, v0 = velocity;
  let x, v;
  if (zeta < 1) {
    const wd = omega * Math.sqrt(1 - zeta * zeta);
    const decay = Math.exp(-zeta * omega * dt);
    const a = x0, b = (v0 + zeta * omega * x0) / wd;
    const c = Math.cos(wd * dt), s = Math.sin(wd * dt);
    x = decay * (a * c + b * s);
    v = decay * ((b * wd - zeta * omega * a) * c - (a * wd + zeta * omega * b) * s);
  } else if (zeta === 1) {
    const decay = Math.exp(-omega * dt);
    const b = v0 + omega * x0;
    x = decay * (x0 + b * dt);
    v = decay * (b - omega * (x0 + b * dt));
  } else {
    const root = omega * Math.sqrt(zeta * zeta - 1);
    const r1 = -zeta * omega + root, r2 = -zeta * omega - root;
    const c2 = (v0 - r1 * x0) / (r2 - r1), c1 = x0 - c2;
    const e1 = Math.exp(r1 * dt), e2 = Math.exp(r2 * dt);
    x = c1 * e1 + c2 * e2;
    v = c1 * r1 * e1 + c2 * r2 * e2;
  }
  const scale = Math.max(1, Math.abs(target), Math.abs(x0));
  if (Math.abs(x) < 1e-4 * scale && Math.abs(v) < 1e-3 * scale) return { value: target, velocity: 0, target };
  return { value: target + x, velocity: v, target };
}

/** iOS-style rubber band: follows a short drag, then resists. */
export function rubberBand(offset, limit, coefficient = 0.55) {
  if (limit <= 0) return 0;
  const m = Math.abs(offset);
  return Math.sign(offset) * limit * (1 - 1 / ((m * coefficient) / limit + 1));
}

/** How far a held surface may stretch toward the pointer before resisting. */
export function stretchLimit(width, height) {
  const size = Math.max(width, height);
  return size <= 72 ? 4 + 0.15 * size : Math.min(10, 3 + 0.04 * size);
}

/**
 * Press and drag response. `press` is the press spring's value (0..1),
 * `tx`/`ty` the drag spring values and `vx`/`vy` their velocities.
 * Volume is preserved: stretching one axis narrows the other.
 */
export function pressScale(width, height, press, tx = 0, ty = 0, vx = 0, vy = 0, reduced = false) {
  if (reduced) return { sx: 1, sy: 1 };
  const size = Math.max(width, height);
  const grow = Math.min(0.16, 8 / size) * press;
  const cap = size <= 72 ? 0.2 : 0.12;
  const ex = Math.min(cap, (0.5 * Math.abs(tx)) / width + 0.00007 * Math.abs(vx));
  const ey = Math.min(cap, (0.5 * Math.abs(ty)) / height + 0.00007 * Math.abs(vy));
  return {
    sx: ((1 + grow) * (1 + ex)) / Math.sqrt(1 + ey),
    sy: ((1 + grow) * (1 + ey)) / Math.sqrt(1 + ex),
  };
}

/** Light that blooms under the finger of an interactive surface. */
export function glow(width, height, dark) {
  return {
    radius: clamp(Math.max(width, height) * 0.9, 36, 180),
    alpha: dark ? 0.16 : 0.3,
    hover: 0.35,
  };
}

// ---------------------------------------------------------------------------
// Progressive blur

export const PROGRESSIVE_LEVELS = 6;
const ease = (u) => u * u * (3 - 2 * u);

/** Gaussian σ of blur level i (0 is sharp). */
export function progressiveLevelBlur(level, blur) {
  return blur * (level / PROGRESSIVE_LEVELS) ** 2;
}

/** Weight of blur level i at depth u (0 = sharp side, 1 = the edge). Weights sum to 1. */
export function progressiveWeight(level, u) {
  return Math.max(0, 1 - Math.abs(PROGRESSIVE_LEVELS * ease(sat(u)) - level));
}
