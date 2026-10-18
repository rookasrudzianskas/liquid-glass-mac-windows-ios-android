# Changelog

All notable changes to Liquid Glass Engineering are recorded here. The project follows [Semantic Versioning](https://semver.org); the libraries, the demo apps, the website and the agent skills share one version.

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

[1.0.0]: https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/releases/tag/1.0.0
