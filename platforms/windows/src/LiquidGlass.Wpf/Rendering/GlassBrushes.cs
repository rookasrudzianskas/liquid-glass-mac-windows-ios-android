using System.Windows;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>Frozen brushes and colors derived from the model's palette.</summary>
internal static class GlassBrushes
{
    public static readonly Brush ForegroundLight = Freeze(new SolidColorBrush(ToColor(GlassPalette.ForegroundLight)));

    public static readonly Brush ForegroundDark = Freeze(new SolidColorBrush(ToColor(GlassPalette.ForegroundDark)));

    /// <summary>
    /// The default foreground of glass with the given appearance, tint and material: on a tint, in either appearance,
    /// white or <c>#111113</c>, whichever contrasts more with the tinted glass (<see cref="GlassPalette.ForegroundOnTint"/>).
    /// Glass that mixes toward the legibility backing (a menu panel, <paramref name="legibility"/> 0.5) is judged as it
    /// draws, the tint mixed toward the backing (<see cref="ForegroundOnBackedTint"/>).
    /// </summary>
    public static Brush Foreground(bool dark, Color? tint, GlassMaterial material, double legibility = 0)
    {
        if (SystemParameters.HighContrast) return SystemColors.ControlTextBrush;
        if (tint is { } t && t.A > 0)
        {
            var foreground = legibility > 0
                ? ForegroundOnBackedTint(ToRgb(t), material, dark, legibility)
                : GlassPalette.ForegroundOnTint(ToRgb(t), material);
            return foreground == GlassPalette.ForegroundLight ? ForegroundLight : ForegroundDark;
        }
        return dark ? ForegroundDark : ForegroundLight;
    }

    /// <summary>
    /// Foreground on tinted glass that mixes toward the legibility backing by <paramref name="legibility"/>: white or
    /// <c>#111113</c>, whichever has the higher WCAG contrast with the tint as the glass draws it over a mid-tone
    /// backdrop, then mixed toward the backing, <c>mix(t · (body + (1 − body)·0.5), backing, legibility)</c>. The backing
    /// lightens light glass and darkens dark glass, so the tint alone would pick white labels on a blue menu lightened
    /// toward <c>#F5F5F7</c> (2.4:1 at worst) and <c>#111113</c> on a green one darkened toward <c>#1C1C1E</c>.
    /// </summary>
    internal static Rgb ForegroundOnBackedTint(Rgb tint, GlassMaterial material, bool dark, double legibility)
    {
        var body = GlassMaterials.TintBody(material);
        var pass = body + (1 - body) * 0.5;
        var glass = GlassPalette.RelativeLuminance(GlassMaterialOptics.LegibilityColor(tint.Map(c => c * pass), dark, legibility));
        var onWhite = 1.05 / (glass + 0.05);
        var onDark = (glass + 0.05) / (GlassPalette.RelativeLuminance(GlassPalette.ForegroundLight) + 0.05);
        return onDark > onWhite ? GlassPalette.ForegroundLight : GlassPalette.ForegroundDark;
    }

    public static Color ToColor(Rgb rgb, double alpha = 1) => Color.FromArgb(
        ToByte(alpha), ToByte(rgb.R), ToByte(rgb.G), ToByte(rgb.B));

    public static Color ToColor(Rgba rgba) => ToColor(rgba.Rgb, rgba.A);

    public static Rgb ToRgb(Color color) => new(color.R / 255.0, color.G / 255.0, color.B / 255.0);

    public static T Freeze<T>(T freezable) where T : Freezable
    {
        freezable.Freeze();
        return freezable;
    }

    private static byte ToByte(double v) => (byte)System.Math.Round(GlassMath.Sat(v) * 255);
}
