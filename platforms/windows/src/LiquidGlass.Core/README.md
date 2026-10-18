# LiquidGlassEngineering.Core

The platform-independent optical model of [Liquid Glass Engineering](https://liquid-glass-engineering.vercel.app):
the same functions every port (SwiftUI, Jetpack Compose, WPF and the web playground) implements, verified against
the shared golden vectors.

- Continuous-corner shapes: corner parameters, signed distance field and outline polygon (`GlassShape`, `GlassRadius`).
- Refraction through the 20-unit bevel (`GlassRefraction`).
- Clear and Regular materials, tint, frost and the legibility backing of menu panels (`GlassMaterialOptics`,
  `GlassMaterials`, `GlassPalette`).
- Rim light (`GlassRimLight`) and the full per-pixel composite (`GlassPixel`).
- Closed-form springs (`Spring`, `SpringSolver`, `GlassSprings`), press and drag response (`GlassInteractionModel`) and
  value-control motion (`GlassControlMotion`).
- Progressive blur levels and weights (`GlassProgressive`).

```csharp
using LiquidGlass;

var shape = GlassShape.Parameters(200, 64, GlassRadius.Capsule);
var field = GlassShape.Field(0, -31, 200, 64, shape);              // near the top edge
var offset = GlassRefraction.Offset(field.Distance, field.NormalX, field.NormalY);
var tone = GlassMaterialOptics.MaterialColor(new Rgb(0.2, 0.5, 0.9), GlassMaterial.Regular, dark: false);
```

For WPF controls that render this model, install `LiquidGlassEngineering.Wpf`.
Targets .NET Standard 2.0. MIT licensed; the optical model is ported from the MIT-licensed Glass HQ liquid-glass web library
(`LICENSE` and `NOTICE.md` in the package).
