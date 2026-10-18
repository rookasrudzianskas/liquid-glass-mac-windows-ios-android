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

    /// <summary>The solid backing menu panels mix their glass toward, light appearance (<c>#F5F5F7</c>).</summary>
    public static Rgb LegibilityBackingLight { get; } = Rgb.FromBytes(0xF5, 0xF5, 0xF7);

    /// <summary>The solid backing menu panels mix their glass toward, dark appearance (<c>#1C1C1E</c>).</summary>
    public static Rgb LegibilityBackingDark { get; } = Rgb.FromBytes(0x1C, 0x1C, 0x1E);

    /// <summary>Reduced-transparency fill for an appearance.</summary>
    public static Rgba ReducedTransparency(bool dark) => dark ? ReducedTransparencyDark : ReducedTransparencyLight;

    /// <summary>Increased-contrast border for an appearance.</summary>
    public static Rgba ContrastBorder(bool dark) => dark ? ContrastBorderDark : ContrastBorderLight;

    /// <summary>Foreground for an appearance.</summary>
    public static Rgb Foreground(bool dark) => dark ? ForegroundDark : ForegroundLight;

    /// <summary>
    /// The legibility backing for an appearance: <c>#F5F5F7</c> light, <c>#1C1C1E</c> dark. Open menu panels mix their
    /// glass toward it by <see cref="GlassOptics.MenuLegibility"/> (<see cref="GlassMaterialOptics.LegibilityColor"/>).
    /// </summary>
    public static Rgb LegibilityBacking(bool dark) => dark ? LegibilityBackingDark : LegibilityBackingLight;

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

    /// <summary>
    /// Label color of a menu (OPTICS §3): the appearance's foreground (<see cref="Foreground"/>) when
    /// <paramref name="tint"/> is null; on a tinted menu, <see cref="ForegroundDark"/> (white) or
    /// <see cref="ForegroundLight"/> (<c>#111113</c>), whichever has the higher WCAG contrast with the tint as the menu
    /// draws it over a mid-tone backdrop, mixed toward the legibility backing:
    /// <c>mix(tint · (body + (1 − body)·0.5), backing, legibility)</c>. The backing lightens light glass and darkens dark
    /// glass, so where the tint alone (<see cref="ForegroundOnTint"/>) gives white labels on blue, a light menu tinted
    /// blue takes <c>#111113</c>; at <see cref="GlassOptics.MenuLegibility"/> every tint takes the appearance's
    /// foreground. A highlighted row mixes this color into the glass by <see cref="GlassOptics.MenuHighlight"/>.
    /// </summary>
    /// <param name="tint">The menu's tint, or null.</param>
    /// <param name="material">The panel's material.</param>
    /// <param name="dark">Dark appearance.</param>
    /// <param name="legibility">How far the panel's glass mixes toward the legibility backing when open.</param>
    /// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#legibility-backing-menus">Liquid Glass optics specification: Legibility backing</seealso>
    public static Rgb MenuForeground(Rgb? tint, GlassMaterial material, bool dark, double legibility = GlassOptics.MenuLegibility)
    {
        if (tint is not { } t) return Foreground(dark);
        var body = GlassMaterials.TintBody(material);
        var pass = body + (1 - body) * 0.5;
        var glass = RelativeLuminance(GlassMaterialOptics.LegibilityColor(new Rgb(t.R * pass, t.G * pass, t.B * pass), dark, legibility));
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
/// The fallback material drawn where the pixel shader cannot run: the platform blur for Regular glass plus a veil
/// fitted to the material's tone curve (on tinted glass, a tint fill instead), on a menu panel the legibility backing
/// over that, and the rim light. Its fills are 8-bit brushes.
/// </summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#3-materials">Liquid Glass optics specification: Materials</seealso>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/COMPONENTS.md#behavior-every-port-implements">Liquid Glass component contract: fallbacks</seealso>
public static class GlassFallback
{
    /// <summary>The opacity of the tint fill (<see cref="TintFill"/>) over the backdrop.</summary>
    public const double TintFillOpacity = 0.9;

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

    /// <summary>
    /// The fill of tinted glass, painted at <see cref="TintFillOpacity"/> over the backdrop: tinted glass is nearly
    /// opaque pigment that transmits the backdrop's luminance, and the pigment at its body strength over a mid-tone
    /// backdrop, <c>tint · (body + (1 − body)·0.5)</c>, reads the same way.
    /// </summary>
    public static Rgb TintFill(Rgb tint, GlassMaterial material)
    {
        var body = GlassMaterials.TintBody(material);
        var pass = body + (1 - body) * 0.5;
        return new Rgb(tint.R * pass, tint.G * pass, tint.B * pass);
    }

    /// <summary>
    /// The opacity at which the fallback paints the legibility backing (<see cref="GlassPalette.LegibilityBacking"/>)
    /// over its veil or tint fill (and the opaque material over its fill, the same tint fill when tinted), on glass
    /// that mixes toward it by <paramref name="legibility"/> (a menu panel: up to
    /// <see cref="GlassOptics.MenuLegibility"/>, following its open progress). Painted over the fill, the backing at the
    /// amount itself would be the shader's mix, but the fill only approximates the material: where the labels
    /// (<see cref="GlassPalette.MenuForeground"/>) contrast least, on a highlighted row over a white backdrop under white
    /// labels or a black one under <c>#111113</c>, it draws lighter or darker than the shader's glass. So at
    /// <see cref="GlassOptics.MenuLegibility"/> the opacity is the least 8-bit level at which the labels there contrast
    /// at least as much as on the shader's menu glass; the fill and the backing are both linear in the backdrop, so
    /// that is the fallback's worst case, and its labels keep the shader's floor or more over any backdrop (untinted:
    /// 0.592 light and 0.620 dark for Regular, 0.627 and 0.616 for Clear). Smaller amounts scale it down.
    /// </summary>
    /// <param name="material">The glass material.</param>
    /// <param name="dark">Dark appearance.</param>
    /// <param name="tint">The glass's tint, or null.</param>
    /// <param name="legibility">How far the glass mixes toward the legibility backing now; 0 paints no backing.</param>
    /// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#legibility-backing-menus">Liquid Glass optics specification: Legibility backing</seealso>
    public static double LegibilityOpacity(GlassMaterial material, bool dark, Rgb? tint, double legibility)
    {
        if (legibility <= 0) return 0;
        var matched = MenuLegibilityLevel(material, dark, tint) / 255.0;
        // Up to the next 8-bit level, so an amount that scales to a level (the menu amount itself) keeps it.
        return Math.Min(1, Math.Ceiling(legibility / GlassOptics.MenuLegibility * matched * 255 - 1e-6) / 255);
    }

    /// <summary>
    /// The least 8-bit opacity of the backing at which the fallback's menu labels contrast at least as much as on the
    /// shader's menu glass, where they contrast least (see <see cref="LegibilityOpacity"/>).
    /// </summary>
    private static int MenuLegibilityLevel(GlassMaterial material, bool dark, Rgb? tint)
    {
        var label = GlassPalette.MenuForeground(tint, material, dark);
        var level = label == GlassPalette.ForegroundDark ? 1.0 : 0.0;
        var backdrop = new Rgb(level, level, level);
        var glass = GlassMaterialOptics.MaterialColor(backdrop, material, dark);
        if (tint is { } t) glass = GlassMaterialOptics.TintColor(glass, t, material);
        var target = Contrast(Highlighted(GlassMaterialOptics.LegibilityColor(glass, dark, GlassOptics.MenuLegibility), label), label);

        // The fill as its 8-bit brush paints it over the backdrop, then the backing at each 8-bit opacity in turn.
        Rgb fill;
        if (tint is { } tinted)
        {
            var color = TintFill(tinted, material);
            var opacity = Byte(TintFillOpacity);
            fill = new Rgb(Over(Byte(color.R), opacity, level), Over(Byte(color.G), opacity, level), Over(Byte(color.B), opacity, level));
        }
        else
        {
            var veil = Veil(material, dark);
            var veiled = Over(Byte(veil.Gray), Byte(veil.Opacity), level);
            fill = new Rgb(veiled, veiled, veiled);
        }
        var backing = GlassPalette.LegibilityBacking(dark);
        for (var k = 0; k < 255; k++)
        {
            var a = k / 255.0;
            var drawn = new Rgb(Over(backing.R, a, fill.R), Over(backing.G, a, fill.G), Over(backing.B, a, fill.B));
            if (Contrast(Highlighted(drawn, label), label) >= target) return k;
        }
        return 255;
    }

    /// <summary>A highlighted menu row: the label color mixed into the glass by <see cref="GlassOptics.MenuHighlight"/>.</summary>
    private static Rgb Highlighted(Rgb glass, Rgb label) => new(
        GlassMath.Mix(glass.R, label.R, GlassOptics.MenuHighlight), GlassMath.Mix(glass.G, label.G, GlassOptics.MenuHighlight),
        GlassMath.Mix(glass.B, label.B, GlassOptics.MenuHighlight));

    private static double Over(double color, double opacity, double beneath) => color * opacity + beneath * (1 - opacity);

    private static double Byte(double v) => Math.Round(GlassMath.Sat(v) * 255) / 255;

    private static double Contrast(Rgb a, Rgb b)
    {
        var x = GlassPalette.RelativeLuminance(a) + 0.05;
        var y = GlassPalette.RelativeLuminance(b) + 0.05;
        return x > y ? x / y : y / x;
    }

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
