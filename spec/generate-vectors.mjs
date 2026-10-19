// Generates spec/vectors.json from spec/reference.mjs.
// Run: node spec/generate-vectors.mjs
import { writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import * as R from "./reference.mjs";

const round = (v) => (typeof v === "number" ? Math.round(v * 1e6) / 1e6 : Array.isArray(v) ? v.map(round) : v);
const roundAll = (o) => Object.fromEntries(Object.entries(o).map(([k, v]) => [k, round(v)]));

const shapes = [
  [200, 64, "capsule"], [64, 64, "circle"], [320, 200, 28], [320, 200, 8], [120, 44, 12],
  [300, 120, 0], [90, 60, 40], [44, 44, 22],
];

const shapeParams = shapes.map(([width, height, radius]) => ({ width, height, radius, ...roundAll(R.shapeParams(width, height, radius)) }));

const shapeField = [];
for (const [width, height, radius] of shapes) {
  const { extent, exponent } = R.shapeParams(width, height, radius);
  for (const fx of [-0.55, -0.5, -0.47, -0.3, 0, 0.21, 0.44, 0.49, 0.5, 0.52]) {
    for (const fy of [-0.52, -0.5, -0.45, -0.2, 0, 0.33, 0.48, 0.5]) {
      const x = fx * width, y = fy * height;
      shapeField.push({ x: round(x), y: round(y), width, height, radius, ...roundAll(R.shapeField(x, y, width, height, extent, exponent)) });
    }
  }
}

const distances = [-40, -20, -15, -10, -5, -2, -1, -0.5, 0, 0.5, 3];
const edgeProfile = distances.map((distance) => ({ distance, value: round(R.edgeProfile(distance)) }));
const defaultRefraction = [[200, 64], [320, 200], [80, 80], [120, 34], [44, 44], [160, 52], [400, 62], [28, 28], [0, 40]]
  .map(([width, height]) => ({ width, height, value: round(R.defaultRefraction(width, height)) }));
const refractionOffset = [
  [-5, 0, -1, 60], [-1, 1, 0, 60], [-12, 0.6, 0.8, 30], [-25, 0, 1, 60], [0, -0.7071, 0.7071, 7.75],
].map(([distance, nx, ny, refraction]) => ({ distance, nx, ny, refraction, ...roundAll(R.refractionOffset(distance, nx, ny, refraction)) }));

const levels = [0, 0.05, 0.1, 0.25, 0.5, 0.75, 0.9, 1];
const tone = levels.map((v) => ({
  v,
  clearLight: round(R.clearTone(v, false)), clearDark: round(R.clearTone(v, true)),
  clearDarkNested: round(R.clearTone(v, true, true)),
  regularLight: round(R.regularTone(v, false)), regularDark: round(R.regularTone(v, true)),
}));

const colors = [[0, 0, 0], [1, 1, 1], [0.5, 0.5, 0.5], [0.9, 0.2, 0.1], [0.1, 0.4, 0.95], [0.2, 0.8, 0.35]];
const materialColor = [];
for (const color of colors) for (const material of ["clear", "regular"]) for (const dark of [false, true])
  materialColor.push({ color, material, dark, value: round(R.materialColor(color, material, dark)) });

const tints = [[0, 0.478, 1], [1, 0.231, 0.188], [0.204, 0.78, 0.349]];
const tintColor = [];
for (const color of colors.slice(0, 4)) for (const tint of tints) for (const material of ["clear", "regular"])
  tintColor.push({ color, tint, material, value: round(R.tintColor(color, tint, material)) });

const frostColor = [
  [[0.2, 0.4, 0.6], [0.5, 0.3, 0.7]], [[0.9, 0.9, 0.9], [0.1, 0.2, 0.3]], [[0.5, 0.5, 0.5], [0.5, 0.5, 0.5]],
].flatMap(([frost, fill]) => [false, true].map((dark) => ({ frost, fill, dark, value: round(R.frostColor(frost, fill, dark)) })));

const rimOverlay = [];
for (const dark of [false, true]) for (const pixel of [1 / 3, 1 / 2, 1]) for (const [nx, ny] of [[0, -1], [0, 1], [1, 0], [-1, 0], [0.7071, -0.7071], [0.3, 0.954]])
  for (const distance of [-8, -3, -1.2, -0.9, -0.5, -0.2, 0, 0.3, 1]) {
    rimOverlay.push({ distance, nx, ny, dark, pixel: round(pixel), value: round(R.rimOverlay(distance, nx, ny, dark, pixel)) });
  }
for (const [nx, ny] of [[0, -1], [1, 0]]) for (const distance of [-0.5, -3])
  rimOverlay.push({ distance, nx, ny, dark: false, pixel: round(1 / 3), tint: tints[0], value: round(R.rimOverlay(distance, nx, ny, false, 1 / 3, tints[0])) });

// A linear backdrop that every platform can reproduce in its tests:
// rgb = (0.5 + px/400, 0.5 + py/400, 0.6), alpha 1, px/py in surface-local points.
const backdrop = (px, py) => [R.sat(0.5 + px / 400), R.sat(0.5 + py / 400), 0.6, 1];
const glassPixel = [];
for (const [width, height, radius] of [[200, 64, "capsule"], [320, 200, 28]]) for (const material of ["clear", "regular"]) for (const dark of [false, true]) {
  for (const [fx, fy] of [[0, 0], [0.45, 0], [0.49, 0.1], [0, -0.48], [-0.3, 0.46], [0.5, 0.5], [0.6, 0]]) {
    const x = fx * width, y = fy * height;
    const base = backdrop(x, y);
    const value = R.glassPixel({ x, y, width, height, radius, material, dark, refraction: 60, pixel: 1 / 3, sample: (dx, dy) => backdrop(x + dx, y + dy), base });
    glassPixel.push({ x: round(x), y: round(y), width, height, radius, material, dark, refraction: 60, pixel: round(1 / 3), value: round(value) });
  }
}
glassPixel.push((() => {
  const x = 0, y = -30, width = 200, height = 64, radius = "capsule";
  const base = backdrop(x, y);
  return { x, y, width, height, radius, material: "regular", dark: false, refraction: 60, pixel: round(1 / 3), tint: tints[0],
    value: round(R.glassPixel({ x, y, width, height, radius, material: "regular", dark: false, refraction: 60, pixel: 1 / 3, tint: tints[0], sample: (dx, dy) => backdrop(x + dx, y + dy), base })) };
})());

// Menu legibility: the backing mix, and the worst-case label contrast it guarantees.
const contrast = (a, b) => { const x = R.relativeLuminance(a) + 0.05, y = R.relativeLuminance(b) + 0.05; return Math.max(x, y) / Math.min(x, y); };
const ink = [0x11 / 255, 0x11 / 255, 0x13 / 255];
const legibility = {
  menuAmount: R.MENU_LEGIBILITY,
  backing: { light: round(R.legibilityBacking(false)), dark: round(R.legibilityBacking(true)) },
  highlight: R.MENU_HIGHLIGHT,
  destructive: { light: round(R.menuDestructive(false)), dark: round(R.menuDestructive(true)) },
  cases: [[[0.2, 0.4, 0.6], false, 0.6], [[0.2, 0.4, 0.6], true, 0.6], [[1, 1, 1], true, 0.6], [[0, 0, 0], false, 0.6], [[0.9, 0.2, 0.1], true, 0.25], [[0.5, 0.5, 0.5], false, 0]]
    .map(([color, dark, amount]) => ({ color, dark, amount, value: round(R.legibilityColor(color, dark, amount)) })),
  menuForeground: [[null, "regular", false], [null, "regular", true], [[0, 0.478, 1], "regular", false], [[0, 0.478, 1], "regular", true],
    [[1, 0.8, 0], "regular", true], [[1, 0.8, 0], "regular", false], [[0.204, 0.78, 0.349], "clear", true], [[1, 0.231, 0.188], "clear", false]]
    .map(([tint, material, dark]) => ({ tint, material, dark, value: round(R.menuForeground(tint, material, dark)) })),
  worstCase: (() => {
    // Labels over a 6x6x6 grid of backdrops, with and without the row highlight.
    const steps = [0, 0.2, 0.4, 0.6, 0.8, 1];
    const grid = steps.flatMap((r) => steps.flatMap((g) => steps.map((b) => [r, g, b])));
    // A row's text (the label color, or `text` for a destructive item) over the glass, plain and highlighted; the
    // highlight always mixes in the label color.
    const floor = (material, dark, tint, text) => {
      const fg = R.menuForeground(tint, material, dark);
      const ink = text ?? fg;
      let low = Infinity;
      for (const bg of grid) {
        let rgb = R.materialColor(bg, material, dark);
        if (tint) rgb = R.tintColor(rgb, tint, material);
        rgb = R.legibilityColor(rgb, dark, R.MENU_LEGIBILITY);
        low = Math.min(low, contrast(rgb, ink), contrast(rgb.map((c, i) => c + (fg[i] - c) * R.MENU_HIGHLIGHT), ink));
      }
      return low;
    };
    // Apple's system colors in their light and dark variants (the most luminous of the platforms' palettes).
    const system = [[0, 122, 255], [52, 199, 89], [255, 59, 48], [255, 149, 0], [255, 204, 0], [175, 82, 222], [255, 45, 85], [88, 86, 214], [90, 200, 250], [0, 199, 190],
      [10, 132, 255], [48, 209, 88], [255, 69, 58], [255, 159, 10], [255, 214, 10], [191, 90, 242], [255, 55, 95], [94, 92, 230], [100, 210, 255], [102, 212, 207]].map((c) => c.map((x) => x / 255));
    const untinted = Math.min(...["regular", "clear"].flatMap((m) => [false, true].map((d) => floor(m, d))));
    const tinted = Math.min(...["regular", "clear"].flatMap((m) => [false, true].flatMap((d) => system.map((t) => floor(m, d, t)))));
    const destructive = (dark, tints) => Math.min(...["regular", "clear"].flatMap((m) => tints.map((t) => floor(m, dark, t, R.menuDestructive(dark)))));
    return {
      darkOverWhite: round(contrast(R.legibilityColor(R.materialColor([1, 1, 1], "regular", true), true, R.MENU_LEGIBILITY), [1, 1, 1])),
      lightOverBlack: round(contrast(R.legibilityColor(R.materialColor([0, 0, 0], "regular", false), false, R.MENU_LEGIBILITY), ink)),
      untintedWithHighlight: round(untinted),
      systemTintsWithHighlight: round(tinted),
      // Destructive items (menuDestructive), untinted and on the system tints, highlighted rows included.
      destructiveLightWithHighlight: round(destructive(false, [undefined])),
      destructiveDarkWithHighlight: round(destructive(true, [undefined])),
      destructiveSystemTintsWithHighlight: round(Math.min(destructive(false, system), destructive(true, system))),
    };
  })(),
};
for (const dark of [false, true]) {
  const width = 240, height = 180, radius = 28;
  for (const [fx, fy] of [[0, 0], [0.48, 0], [0, -0.49]]) {
    const x = fx * width, y = fy * height;
    const base = backdrop(x, y);
    const value = R.glassPixel({ x, y, width, height, radius, material: "regular", dark, refraction: 60, pixel: 1 / 3, legibility: R.MENU_LEGIBILITY, sample: (dx, dy) => backdrop(x + dx, y + dy), base });
    glassPixel.push({ x: round(x), y: round(y), width, height, radius, material: "regular", dark, refraction: 60, pixel: round(1 / 3), legibility: R.MENU_LEGIBILITY, value: round(value) });
  }
}

const springSamples = Object.fromEntries(Object.entries(R.springs).map(([name, options]) => {
  let state = { value: 0, velocity: 0, target: 1 };
  const values = [];
  for (let frame = 1; frame <= 90; frame++) {
    state = R.springStep(state, options, 1 / 60);
    if (frame % 3 === 0) values.push(round(state.value));
  }
  return [name, { ...options, from: 0, to: 1, dt: round(1 / 60), sampleEvery: 3, values }];
}));

const rubberBand = [[0, 10], [5, 10], [20, 10], [-40, 10], [100, 4], [3, 0]].map(([offset, limit]) => ({ offset, limit, value: round(R.rubberBand(offset, limit)) }));
const stretchLimit = [[28, 28], [44, 44], [72, 40], [120, 44], [320, 200], [1000, 40]].map(([width, height]) => ({ width, height, value: round(R.stretchLimit(width, height)) }));
const pressScale = [
  [44, 44, 1, 0, 0, 0, 0], [120, 44, 1, 6, 0, 0, 0], [120, 44, 0.5, 0, -4, 0, 300], [320, 200, 1, 10, 3, 100, 50], [44, 44, 1, 0, 0, 0, 0, true],
].map(([width, height, press, tx, ty, vx, vy, reduced = false]) => ({ width, height, press, tx, ty, vx, vy, reduced, ...roundAll(R.pressScale(width, height, press, tx, ty, vx, vy, reduced)) }));
const glow = [[28, 28, false], [120, 44, true], [400, 300, false]].map(([width, height, dark]) => ({ width, height, dark, ...roundAll(R.glow(width, height, dark)) }));

const progressive = [];
for (const u of [0, 0.1, 0.25, 0.5, 0.75, 0.9, 1]) progressive.push({ u, weights: round(Array.from({ length: R.PROGRESSIVE_LEVELS + 1 }, (_, level) => R.progressiveWeight(level, u))) });
const progressiveBlur = Array.from({ length: R.PROGRESSIVE_LEVELS + 1 }, (_, level) => round(R.progressiveLevelBlur(level, 20)));

const outline = { width: 120, height: 44, radius: 12, segments: 4, points: round(R.shapeOutline(120, 44, 12, 4)) };

const vectors = {
  $comment: "Generated by spec/generate-vectors.mjs from spec/reference.mjs. Do not edit by hand.",
  constants: { bevel: R.BEVEL, defaultRefraction: R.DEFAULT_REFRACTION, continuousExtent: R.CONTINUOUS_EXTENT, continuousExponent: R.CONTINUOUS_EXPONENT, luma: [0.213, 0.715, 0.072] },
  materials: R.materials,
  shapeParams, shapeField, outline, edgeProfile, defaultRefraction, refractionOffset, tone, materialColor, tintColor, frostColor, legibility, rimOverlay,
  glassPixel: { backdrop: "rgb = (sat(0.5 + px/400), sat(0.5 + py/400), 0.6), alpha 1; px/py relative to the surface center", cases: glassPixel },
  springs: springSamples, rubberBand, stretchLimit, pressScale, glow,
  progressive: { levels: R.PROGRESSIVE_LEVELS, blurForLevelsAt20: progressiveBlur, weights: progressive },
};

const out = fileURLToPath(new URL("./vectors.json", import.meta.url));
writeFileSync(out, JSON.stringify(vectors, null, 1) + "\n");
console.log(`wrote ${out}: ${shapeField.length} field, ${rimOverlay.length} rim, ${glassPixel.length} pixel cases`);
