using System;

namespace LiquidGlass;

/// <summary>Colors of the accessibility modes and foregrounds (OPTICS §8).</summary>
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
/// The fallback material drawn where the pixel shader cannot run (OPTICS §3 and COMPONENTS "Fallbacks"):
/// the platform blur for Regular glass plus a veil fitted to the material's tone curve, and the rim light.
/// </summary>
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
