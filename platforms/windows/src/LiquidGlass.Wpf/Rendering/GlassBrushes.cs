using System.Windows;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>Frozen brushes and colors derived from the model's palette.</summary>
internal static class GlassBrushes
{
    public static readonly Brush ForegroundLight = Freeze(new SolidColorBrush(ToColor(GlassPalette.ForegroundLight)));

    public static readonly Brush ForegroundDark = Freeze(new SolidColorBrush(ToColor(GlassPalette.ForegroundDark)));

    /// <summary>The default foreground of glass with the given appearance and tint.</summary>
    public static Brush Foreground(bool dark, Color? tint)
    {
        if (SystemParameters.HighContrast) return SystemColors.ControlTextBrush;
        if (tint is { } t && t.A > 0 && new Rgb(t.R / 255.0, t.G / 255.0, t.B / 255.0).Luma < 0.62) return ForegroundDark;
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
