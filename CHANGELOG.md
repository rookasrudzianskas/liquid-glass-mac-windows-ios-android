# Changelog

All notable changes to Liquid Glass Engineering are recorded here. The project follows [Semantic Versioning](https://semver.org); the libraries, the demo apps, the website and the agent skills share one version.

## [Unreleased]

### Optical model

- Menu panels mix their glass, after the material color and tint, 60% toward a solid backing (`#F5F5F7` light, `#1C1C1E` dark; OPTICS §3, Legibility backing), applied as 0.6 times the menu's open progress clamped to [0, 1], so the glass leaves its trigger as the trigger's own glass and the backing fades in as the panel opens. Items take `menuForeground`: the appearance foreground on an untinted menu; on a tinted one, white or `#111113`, whichever contrasts more with the tint mixed toward the backing. A highlighted row (hover, keyboard focus, press) mixes the label color into the glass at 8%. Untinted menus, Regular or Clear, keep at least 5.2:1 over any backdrop in both appearances, highlighted rows included (white labels over white in dark appearance had 1.8:1 on plain Regular glass), and menus tinted with the platforms' system colors of either appearance at least 5.0:1 (5.07:1 at worst, the dark-appearance yellow over white); very light tints are not covered. `reference.mjs`, the shader library and `vectors.json` (the `legibility` section, six menu glass pixels) carry the rule; every other surface passes 0.

### Windows: WPF and HLSL

- Open menu panels draw the backing in the shader, fading in and out with the morph (never past 0.6 while the morph spring overshoots). Items take `GlassPalette.MenuForeground`, which at 0.6 is the appearance's foreground for every tint (a light Regular menu in the Windows accent gets `#111113` labels, 7.6:1 at worst on a highlighted row, where white labels fell to 2.4:1); the trigger keeps its tint's own foreground and takes the menu's `Foreground` only when the application sets one. A hovered, focused or pressed item mixes its label color in at 8% (a press drew 14%). Untinted menus keep 5.2:1, menus tinted with the system colors 5.0:1 (5.07:1 at worst), Windows accent colors 5.6:1.
- The fallback material paints the backing over its veil or tint fill at `GlassFallback.LegibilityOpacity`, the least 8-bit opacity at which labels on a highlighted row keep the shader's contrast where they contrast least, so its floors are the shader's or higher (5.11:1 over the system colors). The opaque material (transparency effects off, `ReduceTransparency`, a menu outside a scene) paints the backing over its fill the same way: a tinted menu there showed the labels chosen for the backed glass on the bare tint fill (1.6:1 for white on the dark-appearance yellow over white) and now keeps the fallback's floors; untinted menus keep 12.6:1.
- Shortcut text (`InputGestureText`) is drawn in the label's color instead of at 55% of it (about 2.8:1), so the menu's floors hold for it too.
- A menu opened a second time draws again: its panel came back with no height.
- New in `LiquidGlassEngineering.Core`: `GlassOptics.MenuLegibility` and `MenuHighlight`, `GlassPalette.LegibilityBackingLight`, `LegibilityBackingDark`, `LegibilityBacking(bool)` and `MenuForeground`, `GlassMaterialOptics.LegibilityColor`, `GlassFallback.LegibilityOpacity`, `TintFill` and `TintFillOpacity`, and `GlassSurfaceOptics.Legibility` with a `legibility` constructor parameter (the 1.0.0 constructor stays for compiled callers).

## [1.0.0] - 2026-10-18

The first release: native Liquid Glass for iOS, macOS, Android and Windows from one optical model.

### Optical model

- `spec/OPTICS.md`: continuous-corner shapes, bevel refraction, the Clear and Regular materials, tint, rim light, the composite, interaction springs, progressive blur and accessibility, ported from the MIT-licensed Glass HQ liquid-glass library.
- `spec/COMPONENTS.md`: the component contract every port implements, with shared options, sizes per platform and the Liquid Glass Studio demo.
- `spec/reference.mjs` (executable reference), `spec/shaders/liquid-glass.glsl` (canonical shader library) and `spec/vectors.json` (golden test vectors every port reproduces).
- Refraction left unset follows the surface's resting size, `min(60, 0.75 × shorter side)`, so small controls never pull backdrop edges in from outside their outline. Labels on tinted glass take white or `#111113`, whichever has the higher WCAG contrast.

### Apple: SwiftUI and Metal

- Swift package `LiquidGlass` for iOS 17+ and macOS 14+: `GlassScene`, `GlassContent`, `GlassSurface` and `.liquidGlass()`, `GlassButton`, `GlassToolbar`, `GlassTabBar`, `GlassSlider`, `GlassSwitch` and `GlassSwitchStyle`, `GlassMenu`, `GlassProgressiveBlur`, `GlassShape`, `GlassScrollView`, `GlassOptics` and `GlassSpring`.
- The Metal shader is checked against the golden vectors on the GPU, and render tests compare a real scene with the model.
- Liquid Glass Studio for iPhone, iPad and Mac, with UI tests.

### Android: Jetpack Compose and AGSL

- `io.github.rookasrudzianskas:liquid-glass-compose` (minSdk 24): GPU glass on Android 13 and later, a fallback material on Android 7 to 12. Served from the project's Maven repository, through JitPack and as an AAR.
- One AGSL pass draws up to 16 surfaces per scene; instrumented tests compare the GPU output with the reference.
- Liquid Glass Studio for Android.

### Windows: WPF and HLSL

- `LiquidGlassEngineering.Wpf` and `LiquidGlassEngineering.Core` for .NET Framework 4.8, .NET 8 and .NET 10 on Windows 10 1809 and later, on the project's NuGet feed.
- A pixel shader 3.0 glass shader, precompiled with a run-time compile path, UI Automation peers, keyboard focus, contrast themes and a fallback material.
- Liquid Glass Studio for Windows on x64 and ARM64.

### Examples

- Hello Glass, the smallest complete app, for SwiftUI (iOS and macOS), Jetpack Compose and WPF: the same screen on every platform, built in CI.

### Website

- Developer guides, an interactive WebGL2 playground, downloads with SHA-256 checksums, the Maven repository and NuGet feed, and `llms.txt`.

### Agent skills

- A plugin marketplace with the `liquid-glass` plugin and its skills: `liquid-glass` (design), `liquid-glass-swiftui`, `liquid-glass-compose`, `liquid-glass-wpf` and `liquid-glass-port`.

[Unreleased]: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/compare/1.0.0...HEAD
[1.0.0]: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/releases/tag/1.0.0
