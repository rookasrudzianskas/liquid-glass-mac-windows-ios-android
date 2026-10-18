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
    /// Glass that mixes toward the legibility backing when open (a menu panel, <paramref name="legibility"/>
    /// <see cref="GlassOptics.MenuLegibility"/>) takes its labels' color, judged on the tint mixed toward the backing
    /// (<see cref="GlassPalette.MenuForeground"/>).
    /// </summary>
    public static Brush Foreground(bool dark, Color? tint, GlassMaterial material, double legibility = 0)
    {
        if (SystemParameters.HighContrast) return SystemColors.ControlTextBrush;
        if (tint is { } t && t.A > 0)
        {
            var foreground = legibility > 0
                ? GlassPalette.MenuForeground(ToRgb(t), material, dark, legibility)
                : GlassPalette.ForegroundOnTint(ToRgb(t), material);
            return foreground == GlassPalette.ForegroundLight ? ForegroundLight : ForegroundDark;
        }
        return dark ? ForegroundDark : ForegroundLight;
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
