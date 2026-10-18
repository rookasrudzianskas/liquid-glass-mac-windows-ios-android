# Liquid Glass optics specification

This is the single source of truth for how Liquid Glass Engineering renders glass on every platform. The executable form is [`reference.mjs`](reference.mjs); the shader form is [`shaders/liquid-glass.glsl`](shaders/liquid-glass.glsl); the golden test data is [`vectors.json`](vectors.json) (regenerate with `node spec/generate-vectors.mjs`). Each platform port must reproduce `vectors.json` in its unit tests (tolerance `1e-4` on CPU code, `2e-3` for values read back from a GPU).

The model is a native port of the MIT-licensed [Glass HQ liquid-glass](https://github.com/Glass-HQ/liquid-glass) web library: its WGSL material maps (`gpu/maps.wgsl`), SVG filter graph (`dom/material-graph.ts`, `dom/lens-layers.ts`), materials (`core/materials.ts`) and springs (`core/spring.ts`, `dom/interaction.ts`). The web library precomputes displacement and rim maps per shape and applies them with SVG filters; the native ports evaluate the same functions per pixel in a GPU shader.

## Units and color

- Geometry is in layout units: points (Apple), dp (Android), device-independent pixels (Windows), CSS pixels (web). `pixel` is the size of one device pixel in those units (`1 / displayScale`).
- Color math runs on **sRGB gamma-encoded, unpremultiplied** channels in `[0, 1]`, matching SVG's `color-interpolation-filters="sRGB"`. If a platform hands the shader linear or extended-range values, convert to sRGB first and back after.
- Coordinates inside a surface are measured from its center, **y pointing down**.

## 1. Shape

A glass outline is a rectangle `width × height` with four identical corners.

`radius` is a nonnegative number, or `capsule` / `circle` (fully rounded ends).

```
half     = min(width, height) / 2
capsule:   extent = half, exponent = 2
number r:  raw = 1.528 · r
           extent = min(raw, half)
           exponent = mix(3.26, 2, smoothstep(0.75, 1, raw / half))
```

Each corner is a superellipse quadrant `|x|ⁿ + |y|ⁿ = extentⁿ` centered `extent` in from both edges. With `n = 3.26` it meets the straight edges with continuous curvature (a "continuous corner", like Apple's), and its diagonal inset equals that of a circular corner of radius `r`. Large radii blend smoothly into a circular capsule, so animating the radius never jumps.

The **signed distance field** (negative inside) and outward normal:

```
q = |p| − (size/2 − extent)
if q.x > 0 and q.y > 0:                  // corner
    m = (q.xⁿ + q.yⁿ)^(1/n)               // normalize by max(q) first in float32
    g = ((q.x/m)^(n−1), (q.y/m)^(n−1))
    distance = (m − extent) / |g|
    normal   = sign(p) · g / |g|
else if q.x > q.y: distance = q.x − extent, normal = (sign(p.x), 0)
else:              distance = q.y − extent, normal = (0, sign(p.y))
```

Clip paths, borders and hit shapes must use the same outline: `shapeOutline()` in the reference samples it as a polygon (or draw each corner as the parametric superellipse `x = e·sgn(cos t)|cos t|^(2/n)`, `y = e·sgn(sin t)|sin t|^(2/n)`).

## 2. Refraction

The outline bends light through a 20-unit bevel:

```
depth  = sat(−distance / 20)
edge   = 1 − sat(sqrt((2 − depth) · depth))     // 1 at the outline, 0 at 20 inside
offset = −normal · edge · refraction
```

**Default refraction.** A surface that does not set `refraction` uses `min(60, 0.75 · min(width, height))`, measured on its resting size. Large surfaces get the full 60; a control shorter than 80 travels at most three quarters of its own height, so it never pulls in backdrop from far outside its outline (a 34-tall button uses 25.5, a 52-tall toolbar 39, a 62-tall tab bar 46.5). An explicit value, including the slider and switch thumbs' 5 and 7.75, is used as given.

A glass pixel at `p` shows the backdrop at `p + offset`: content near the rim is drawn from deeper inside, the way a thick convex lens magnifies toward its edge. Small controls never reach full depth, so they refract across their whole face.

Coverage (antialiasing): `coverage = sat(0.5 − distance / pixel)`.

## 3. Materials

### Clear

Per channel `v` of the refracted backdrop:

```
light: tone = v + 0.18(1−v)³                  veil = 0.78v + 0.26   weight = 0.06
dark:  tone = v + 0.15(1−v)³ − 0.20v³         veil = 0.38v + 0.20   weight = 0.14
out = sat((1 − weight)·tone + weight·veil + (dark and nested ? 0.08(1−v) : 0))
```

### Regular (frosted)

The backdrop is frosted before refraction:

```
frost = gaussian(backdrop, σ = 7.33)
fill  = gaussian(backdrop, σ = 23 light | 21.8 dark)
blend = light ? max(frost, fill) : min(frost, fill)
frosted = fillOpacity·blend + (1 − fillOpacity)·frost      // fillOpacity 0.34 light | 0.65 dark
```

Then per channel, with `(a, b, q, weight, chroma)` = `(0.36, 0.67, −0.1113, 0.80, 1.1031)` light or `(0.105, 0.83, −0.2091, 0.90, 1.0648)` dark:

```
toned = sat(weight·(a + b·v + q·v²) + (1 − weight)·v)
out   = saturate(toned, chroma)       // SVG feColorMatrix type="saturate"
```

`saturate(c, s)` uses the SVG matrix with luma `(0.213, 0.715, 0.072)`. A platform may approximate the σ = 23 fill blur (for example by sampling the σ = 7.33 result over a wider kernel) if the result stays within the GPU tolerance on smooth backdrops.

### Tint

Optional color `t`. Colored glass transmits luminance through the pigment:

```
body = 0.78 regular | 0.68 clear
rgb  = t · (body + (1 − body)·luma(rgb))
alpha = body + (1 − body)·alpha
```

### Legibility backing (menus)

A menu carries text over whatever lies behind it, and Regular glass alone cannot guarantee that text reads: over a white backdrop, dark Regular glass gives white labels only 1.8:1, and over a black backdrop, light Regular glass gives `#111113` labels 2.1:1. Menu panels therefore mix their glass, after the material color and tint, toward a solid backing:

```
backing = light ? #F5F5F7 : #1C1C1E
rgb     = mix(rgb, backing, legibility · morph)   // legibility 0.6 on menu panels, 0 elsewhere
```

`morph` is the menu's open progress (0 at the trigger, 1 open, clamped to 1 while the morph spring overshoots), so the glass leaves its trigger as the trigger's own glass and the backing fades in as the panel opens.

**Labels.** Untinted menus draw their items in the appearance foreground (`#111113` light, white dark). A tinted menu draws them in whichever of white and `#111113` has the higher WCAG 2 contrast with the tint as the menu draws it over a mid-tone backdrop: `mix(t · (body + (1 − body)·0.5), backing, legibility)` (`menuForeground` in the reference). A highlighted row (hover, keyboard focus, press) mixes the label color into the glass at 8%.

**Floor.** With `legibility = 0.6`, every untinted menu label, Regular or Clear, keeps at least 5.2:1 over any backdrop in both appearances, highlighted rows included (6.35:1 at worst without the highlight). Menus tinted with the platform's system colors, in the variants of either appearance, keep at least 5.0:1, highlighted rows included (5.07:1 at worst: the dark-appearance system yellow over a white backdrop; the light-appearance system colors keep 5.26:1). Very light tints (pure yellow, lime, white) can fall below 4.5:1 over a white backdrop in dark appearance: tint menus with a deeper color, or not at all. The panel still refracts and frosts what is behind it, at 40% strength. Other surfaces use 0; a port may expose the amount for text-heavy panels.

## 4. Rim light

Painted over the glass (premultiplied), from the same field. `k = 1` in dark appearance, else 0; `aa = pixel`.

```
cover      = sat(0.5 − d/aa)
directional= sat((|n.y| − cos 1.08) / (1 − cos 1.08))^1.25
diffuseDir = sat((|n.y| − cos 0.702) / (1 − cos 0.702))^1.25
stroke     = sat((d + 0.9)/aa + 0.5) · sat(0.5 − d/aa)
specular   = directional·stroke;  specular /= 1 + 0.5(1 − specular)
diffuse    = diffuseDir · 0.18 · (1 − sat(−d/4.5))² · cover
highlight  = sat(specular·mix(1, 0.82, k) + diffuse·mix(1, 0.40, k))
adjusted   = d − 0.5;  falloff = 1 − sat(−adjusted/0.5)
ramp       = mix(falloff > 0 ? 1 : 0, falloff, 0.75)
outline    = ramp · sat((adjusted + 0.5)/aa + 0.5) · sat(−adjusted/aa + 0.5)
w          = sat((±n.x − cos 0.98)/(1 − cos 0.98)) · outline        // both sides
shade      = Σ w/(1 + 0.4(1 − w)) · mix(0.48, 0.60, k)
alpha      = highlight + shade(1 − highlight)
rgb        = tint ? highlight·(0.45 + 0.55·t) : mix(1, 0.82, k)·highlight
```

A thin specular line runs along the top and bottom of the outline, a soft diffuse glow sits just inside it, and the left and right sides carry a dark shade.

## 5. Composite

```
src    = sample(material == regular ? frosted : backdrop, p + offset)     // unpremul
rgb    = material color (+ tint) of src, then mixed toward the legibility backing (menus)
ga     = alpha · coverage
glass  = (rgb·ga, ga) + base·(1 − ga)          // base: unrefracted backdrop at p, premul
result = rim + glass·(1 − rim.a)
```

Surface `opacity` multiplies the glass contribution (`ga` and `rim`) so glass can fade in and out.

## 6. Interaction

Interactive glass (buttons, toolbars, tab bars, slider and switch thumbs) responds to touch and pointer with damped springs. All springs are `(duration, bounce)` pairs solved in closed form (`springStep` in the reference):

| Spring | duration | bounce | Use |
| --- | --- | --- | --- |
| press | 0.24 | 0.20 | Finger down: the material catches it. |
| release | 0.50 | 0.42 | Finger up: gel-like overshoot back to rest. |
| glow | 0.42 | 0 | Light settling in and out. |
| morph | 0.42 | 0.18 | Glass growing out of another shape (menus). |
| layout | 0.40 | 0.16 | A surface flowing to a new size. |
| track | 0.18 (+0.0005·max(w,h) while dragging) | 0 | Selection lenses and drag follow. |

```
stiffness = (2π/duration)²
damping   = bounce ≥ 0 ? 4π(1 − bounce)/duration : 4π/(duration(1 + bounce))
```

- **Press:** `grow = min(0.16, 8/max(w,h)) · press`. Dragging while pressed stretches toward the finger with `rubberBand(offset, stretchLimit(w,h))`; stretch preserves volume: `sx = (1+grow)(1+ex)/√(1+ey)`, `sy = (1+grow)(1+ey)/√(1+ex)`, `ex = min(cap, 0.5|tx|/w + 0.00007|vx|)`, `cap = 0.2` for controls ≤ 72 units, else 0.12. Release returns with the release spring.
- **Glow:** a radial light under the pointer, white at alpha `0.30` (light) / `0.16` (dark) times the glow spring, falling linearly to 0 at `radius = clamp(0.9·max(w,h), 36, 180)`, clipped to the glass and painted beneath the surface's content. Press drives the glow to 1; hover (pointer devices) to 0.35.
- **Reduced motion:** no grow, stretch or overshoot (springs become critically damped crossfades). `motion = none` removes transitions.
- The slider and switch thumbs lift into Clear glass while pressed: the slider thumb (20×16 at rest) grows by 2.5 × 2 per side with refraction 5; the switch thumb (32×20 at rest, in a 54×24 track) grows by 9 × 5.5 per side with refraction 7.75. At rest the thumb is an opaque white capsule with a soft shadow; pressed, it crossfades into glass. A touch port may use larger thumbs with the same refraction: iOS uses a 36×24 slider thumb that grows by 5 × 4 per side and a 37×24 switch thumb in a 63×28 track that grows by 10 × 6.5 per side (the per-platform sizes are in [`COMPONENTS.md`](COMPONENTS.md#sizing-per-platform)).

## 7. Progressive blur

Edge blur over the backdrop. Six blur levels plus the sharp backdrop; level `i` has `σᵢ = blur·(i/6)²` and weight `max(0, 1 − |6·ease(u) − i|)` where `ease(u) = u²(3 − 2u)` and `u` runs from 0 at the sharp side to 1 at the edge. Defaults: `size = 80`, `blur = 20`. Weights sum to 1 at every depth.

## 8. Accessibility

- **Reduce transparency:** replace the material with an opaque fill (`#F2F2F7` at 0.94 light, `#1C1C1E` at 0.94 dark), keep the rim light.
- **Increase contrast:** add a 1-unit inner border (`black 0.18` light, `white 0.28` dark) and use the Regular material.
- **Foreground:** text and icons on glass default to `#111113` (light) and `#FFFFFF` (dark). On tinted glass, in either appearance, they take whichever of the two has the higher WCAG 2 contrast ratio with the tint as the glass draws it over a mid-tone backdrop, `t · (body + (1 − body)·0.5)` (§3, `body` 0.78 Regular, 0.68 Clear): white on blue, red or purple, `#111113` on yellow, mint, green or orange.
- Glass never carries meaning by itself: controls keep their platform accessibility roles, labels, focus and keyboard behavior.
