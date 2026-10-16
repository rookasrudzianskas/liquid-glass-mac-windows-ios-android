# LiquidGlassEngineering.Wpf

Native Liquid Glass for WPF: refraction, the Clear and Regular materials, rim light and elastic interaction,
rendered by an HLSL pixel shader with the same optical model as the SwiftUI and Jetpack Compose ports of
[Liquid Glass Engineering](https://liquid-glass-engineering.vercel.app).

```
dotnet add package LiquidGlassEngineering.Wpf
```

```xml
<Window xmlns:lg="https://liquid-glass-engineering.vercel.app/wpf" ...>
  <lg:GlassScene>
    <lg:GlassScene.Content>
      <Image Source="wallpaper.jpg" Stretch="UniformToFill" />
    </lg:GlassScene.Content>

    <lg:GlassSurface Material="Regular" Radius="28" Width="360" Padding="24"
                     HorizontalAlignment="Center" VerticalAlignment="Center">
      <StackPanel>
        <TextBlock Text="Hello, glass" FontSize="28" FontWeight="SemiBold" />
        <lg:GlassButton Content="Continue" Tint="#0A84FF" Margin="0,16,0,0" />
      </StackPanel>
    </lg:GlassSurface>
  </lg:GlassScene>
</Window>
```

**Controls:** `GlassScene`, `GlassSurface`, `GlassButton`, `GlassToolbar` / `GlassToolbarSpacer`, `GlassTabBar` /
`GlassTabBarItem`, `GlassSlider`, `GlassSwitch`, `GlassMenu` / `GlassMenuItem`, `GlassProgressiveBlur`, and
`GlassGeometry` for continuous-corner outlines.

**Rendering:** each surface refracts a live, aligned copy of the scene's backdrop with a pixel shader 3.0 program
(precompiled by fxc, compiled with D3DCompile as a fallback). Where pixel shader 3.0 cannot run (software
rendering, Remote Desktop without acceleration) glass draws a fallback material: the platform blur, a veil fitted to
the material's tone curve and the exact rim light. Contrast themes, reduced transparency and animation effects are
honored; every control has a UI Automation peer and a keyboard focus ring.

**Requirements:** .NET Framework 4.8, .NET 8 or .NET 10 on Windows 10 (1809) or Windows 11.

Documentation: [docs/windows.md](https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/docs/windows.md).
MIT licensed; the optical model is ported from the MIT-licensed Glass HQ liquid-glass web library.
