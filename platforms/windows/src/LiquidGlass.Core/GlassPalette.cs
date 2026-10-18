using System;

namespace LiquidGlass;

/// <summary>Colors of the accessibility modes and foregrounds.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#8-accessibility">Liquid Glass optics specification: Accessibility</seealso>
public static class GlassPalette
{
    /// <summary>Opaque fill that replaces the material when transparency is reduced, light appearance (<c>#F2F2F7</c> at 0.94).</summary>
    public static Rgba ReducedTransparencyLight { get; } = new(Rgb.FromBytes(0xF2, 0xF2, 0xF7), 0.94);

    /// <summary>Opaque fill that replaces the material when transparency is reduced, dark appearance (<c>#1C1C1E</c> at 0.94).</summary>
    public static Rgba ReducedTransparencyDark { get; } = new(Rgb.FromBytes(0x1C, 0x1C, 0x1E), 0.94);

    /// <summary>The 1-unit inner border added when contrast is increased, light appearance (black at 0.18).</summary>
    public static Rgba ContrastBorderLight { get; } = new(0, 0, 0, 0.18);

    /// <summary>The 1-unit inner border added when contrast is increased, dark appearance (white at 0.28).</summary>
    public static Rgba ContrastBorderDark { get; } = new(1, 1, 1, 0.28);

    /// <summary>Text and icons on light glass (<c>#111113</c>).</summary>
    public static Rgb ForegroundLight { get; } = Rgb.FromBytes(0x11, 0x11, 0x13);

    /// <summary>Text and icons on dark glass (<c>#FFFFFF</c>).</summary>
    public static Rgb ForegroundDark { get; } = new(1, 1, 1);

    /// <summary>Reduced-transparency fill for an appearance.</summary>
    public static Rgba ReducedTransparency(bool dark) => dark ? ReducedTransparencyDark : ReducedTransparencyLight;

    /// <summary>Increased-contrast border for an appearance.</summary>
    public static Rgba ContrastBorder(bool dark) => dark ? ContrastBorderDark : ContrastBorderLight;

    /// <summary>Foreground for an appearance.</summary>
    public static Rgb Foreground(bool dark) => dark ? ForegroundDark : ForegroundLight;

    /// <summary>
    /// Foreground on glass tinted with <paramref name="tint"/>, in either appearance: <see cref="ForegroundDark"/>
    /// (white) or <see cref="ForegroundLight"/> (<c>#111113</c>), whichever has the higher WCAG contrast with the tint as
    /// the glass draws it over a mid-tone backdrop, <c>tint · (body + (1 − body)·0.5)</c> (white on blue, red or
    /// purple; <c>#111113</c> on yellow, mint, green or orange).
    /// </summary>
    public static Rgb ForegroundOnTint(Rgb tint, GlassMaterial material)
    {
        var body = GlassMaterials.TintBody(material);
        var pass = body + (1 - body) * 0.5;
        var glass = RelativeLuminance(new Rgb(tint.R * pass, tint.G * pass, tint.B * pass));
        var onWhite = 1.05 / (glass + 0.05);
        var onDark = (glass + 0.05) / (RelativeLuminance(ForegroundLight) + 0.05);
        return onDark > onWhite ? ForegroundLight : ForegroundDark;
    }

    /// <summary>WCAG 2 relative luminance of a gamma-encoded sRGB color.</summary>
    public static double RelativeLuminance(Rgb color)
        => 0.2126 * Linear(color.R) + 0.7152 * Linear(color.G) + 0.0722 * Linear(color.B);

    private static double Linear(double c) => c <= 0.04045 ? c / 12.92 : Math.Pow((c + 0.055) / 1.055, 2.4);
}

/// <summary>
/// A gray veil with an opacity: painted over the backdrop it maps each channel <c>v</c> to
/// <c>Gray · Opacity + v · (1 − Opacity)</c>.
/// </summary>
public readonly struct GlassVeil
{
    /// <summary>Creates a veil.</summary>
    public GlassVeil(double gray, double opacity)
    {
        Gray = gray;
        Opacity = opacity;
    }

    /// <summary>Gray level of the veil (sRGB).</summary>
    public double Gray { get; }

    /// <summary>Opacity of the veil.</summary>
    public double Opacity { get; }

    /// <summary>The channel value seen through the veil.</summary>
    public double Apply(double v) => Gray * Opacity + v * (1 - Opacity);
}

/// <summary>
/// The fallback material drawn where the pixel shader cannot run:
/// the platform blur for Regular glass plus a veil fitted to the material's tone curve, and the rim light.
/// </summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#3-materials">Liquid Glass optics specification: Materials</seealso>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/COMPONENTS.md#behavior-every-port-implements">Liquid Glass component contract: fallbacks</seealso>
public static class GlassFallback
{
    private static readonly GlassVeil ClearLightVeil = Fit(v => GlassMaterialOptics.ClearTone(v, false));
    private static readonly GlassVeil ClearDarkVeil = Fit(v => GlassMaterialOptics.ClearTone(v, true));
    private static readonly GlassVeil RegularLightVeil = Fit(v => GlassMaterialOptics.RegularTone(v, false));
    private static readonly GlassVeil RegularDarkVeil = Fit(v => GlassMaterialOptics.RegularTone(v, true));

    /// <summary>
    /// The veil that best reproduces a material's tone curve (least squares over the channel range): a gray of the
    /// right level at the right opacity is the closest a plain translucent fill can get to the curve.
    /// </summary>
    public static GlassVeil Veil(GlassMaterial material, bool dark) => material == GlassMaterial.Regular
        ? (dark ? RegularDarkVeil : RegularLightVeil)
        : (dark ? ClearDarkVeil : ClearLightVeil);

    private static GlassVeil Fit(Func<double, double> tone)
    {
        // Least-squares line tone(v) ≈ α + β·v over v ∈ [0, 1]; a veil has α = gray·opacity and β = 1 − opacity.
        const int samples = 256;
        double sv = 0, st = 0, svv = 0, svt = 0;
        for (var i = 0; i < samples; i++)
        {
            var v = i / (samples - 1.0);
            var t = tone(v);
            sv += v;
            st += t;
            svv += v * v;
            svt += v * t;
        }
        var beta = (samples * svt - sv * st) / (samples * svv - sv * sv);
        var alpha = (st - beta * sv) / samples;
        var opacity = GlassMath.Sat(1 - beta);
        var gray = opacity > 1e-6 ? GlassMath.Sat(alpha / opacity) : 0;
        return new GlassVeil(gray, opacity);
    }
}
