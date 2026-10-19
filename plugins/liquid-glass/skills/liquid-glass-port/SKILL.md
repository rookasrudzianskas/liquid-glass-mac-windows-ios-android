---
name: liquid-glass-port
description: Port the Liquid Glass Engineering optical model (continuous-corner SDF, bevel refraction, Clear and Regular materials, rim light, springs) to a new platform or renderer such as Flutter, React Native Skia, Unity, Godot, Avalonia, a game engine or a WebGL/WebGPU canvas, and verify it against the golden vectors. Use when no ready-made Liquid Glass Engineering library exists for the target, or when writing a custom glass shader that must match the official ports.
license: MIT
metadata:
  homepage: https://liquid-glass-engineering.vercel.app
  repository: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android
---

# Porting Liquid Glass to a new platform

The official ports (SwiftUI/Metal, Compose/AGSL, WPF/HLSL, WebGL2) are translations of one model. A new port is correct when it reproduces `references/vectors.json` and looks identical side by side.

Bundled references, all copied from the repository's `spec/` folder:

- [references/OPTICS.md](references/OPTICS.md): the model in prose, with every constant.
- [references/COMPONENTS.md](references/COMPONENTS.md): component names, defaults and behaviors to match.
- [references/reference.mjs](references/reference.mjs): executable reference (plain JavaScript, no dependencies).
- [references/liquid-glass.glsl](references/liquid-glass.glsl): the canonical shader library (GLSL ES 3.00). Translate this, don't re-derive it.
- [references/vectors.json](references/vectors.json): golden test data generated from `reference.mjs`.

The model is ported from the MIT-licensed Glass HQ liquid-glass library. A port that copies or translates `reference.mjs` or the shader is a copy of substantial portions of it: ship [LICENSE](LICENSE) and [NOTICE.md](NOTICE.md) (the Glass HQ copyright and permission notice) with it.

## Steps

1. **Read OPTICS.md fully.** Note the conventions: layout units, y down, coordinates relative to the surface center, sRGB gamma-encoded unpremultiplied color math, `pixel = 1 / displayScale`.
2. **Decide how glass sees the content.** The renderer must give the glass shader the backdrop pixels at arbitrary offsets around each surface (up to `refraction + 3·blur` away). Options, best first:
   - Apply one shader to the content layer, passing every surface's geometry as uniforms (Compose `RenderEffect`, Skia `SkRuntimeEffect`, a full-screen pass in a game engine).
   - Render the content to a texture once per frame and draw each surface as a quad sampling it (WebGL, Unity, Godot).
   - Give each surface a live mirror of the content aligned to its position (WPF `VisualBrush`, Firefox `-moz-element`).
   Avoid re-rendering stateful content per surface: duplicates drift apart.
3. **Port the scalar math first** (`shapeParams`, `shapeField`, `shapeOutline`, `edgeProfile`, `defaultRefraction`, `refractionOffset`, tones, `saturate`, `materialColor`, `tintColor`, `frostColor`, `legibilityBacking`, `legibilityColor`, `menuForeground`, `menuDestructive`, `rimOverlay`, `glassPixel`, `springStep`, `rubberBand`, `pressScale`, `glow`, progressive weights) in the platform's main language and test it against `vectors.json` with tolerance 1e-4. This catches sign and convention errors before they hide inside a shader.
4. **Translate the shader** from `liquid-glass.glsl` line by line. Keep function names. Watch for:
   - `mix`/`lerp`, `fract`/`frac`, `clamp`/`saturate` naming; HLSL's `saturate` is GLSL's `clamp(x, 0, 1)`, not the color matrix.
   - Integer vs float literals (`2` vs `2.0`) in strict languages.
   - `pow` with a zero base and the corner normalization by `max(q.x, q.y)` (keep it: it prevents float32 overflow).
   - Uniform layout and alignment (float4 packing, std140, arrays of float4).
5. **Color pipeline.** Find out what the sampler returns: premultiplied or not, sRGB-encoded or linear, extended range or clamped. Convert to sRGB unpremultiplied before the tone curves and back after. Getting this wrong makes Clear glass look gray or washed out.
6. **Frost (Regular).** Precompute two Gaussian blurs of the backdrop (σ 7.33 and σ 23 light / 21.8 dark, in layout units times the display scale) using the platform's GPU blur, then combine with `lgFrostColor` after sampling at the refracted point. Check what the platform's blur parameter means: CSS `blur()` takes σ, Skia and Android `RenderEffect` take a radius with `σ = 0.57735·radius + 0.5`, and other APIs differ again; when the documentation is vague, blur a 1-pixel line and measure the spread. An approximation of the wide fill blur is acceptable if smooth backdrops stay within 2e-3.
7. **Composite** with `lgCompose`, then the pointer glow (`lgGlow`) for interactive surfaces, then the surface's own children on top. `lgCompose` takes the legibility amount (OPTICS §3, `lgLegibility`): 0 on every surface except menu panels, which pass `MENU_LEGIBILITY` (0.6) times the menu's open progress, clamped to [0, 1] so it never passes 0.6 while the morph spring overshoots.
8. **Interaction.** Port `springStep` exactly (closed form, stable for any frame time). Drive it from the display link only while something moves. Apply `pressScale` to the glass shape and the children together, around the surface center; keep sampling the backdrop at true screen positions.
9. **Verify visually.** Render the `glassPixel` cases with the linear backdrop described in `vectors.json` and compare GPU readback within 2e-3. Then render a surface over a striped or wallpaper backdrop and check: highlights along the top and bottom rim, shade on the sides, content near the rim pulled from further inside, crisp antialiased edge at 1 device pixel, no seam at the corner/edge boundary, correct light and dark.
10. **Menus.** Menu items take `menuForeground` (the appearance foreground untinted; on a tint, white or `#111113` by contrast with the tint mixed toward the backing), and a highlighted row (hover, keyboard focus, press) mixes that color into the glass at `MENU_HIGHLIGHT` (8%). A destructive item, if the port has one, takes `menuDestructive` on the side of the labels (`#560D0A` where they are `#111113`, `#FFEEED` where they are white), not system red, and its highlight still mixes the label color. The fallback and opaque (reduced transparency) materials of a menu must mix toward the backing too, so their labels keep the floors. Without the backing, white labels over white in dark appearance get 1.8:1.
11. **Fallbacks and accessibility.** Without the GPU path, draw a translucent fill plus the rim light. Honor reduced motion, reduced transparency (opaque fill) and increased contrast (border).

## Done when

- Scalar tests pass against every section of `vectors.json`, `legibility` included.
- GPU readback matches the `glassPixel` cases, including the six menu pixels at legibility 0.6.
- Menu labels reproduce `legibility.worstCase` on the GPU path (5.24:1 untinted and 5.07:1 on the system tints at worst, highlighted rows included; destructive items 5.12:1 light and 4.67:1 dark), and the fallback and opaque materials keep at least 5.2:1 and 5.0:1.
- Side-by-side screenshots with an official port at the same size, material and radius are indistinguishable.
- The port ships LICENSE and NOTICE.md.
