using System;

namespace LiquidGlass;

/// <summary>Refraction through the 20-unit bevel inside the outline.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#2-refraction">Liquid Glass optics specification: Refraction</seealso>
public static class GlassRefraction
{
    /// <summary>Displacement weight: 1 on the outline, falling to 0 at <paramref name="bevel"/> inside.</summary>
    public static double EdgeProfile(double distance, double bevel = GlassOptics.Bevel)
    {
        var depth = GlassMath.Sat(-distance / bevel);
        return 1 - GlassMath.Sat(Math.Sqrt((2 - depth) * depth));
    }

    /// <summary>
    /// The refraction a surface uses when it sets none: the full <see cref="GlassOptics.DefaultRefraction"/>, limited
    /// for small controls to three quarters of their shorter side so they never pull in backdrop from far outside their
    /// outline. Measured on the resting size, so a press does not change it.
    /// </summary>
    public static double Default(double width, double height)
        => Math.Min(GlassOptics.DefaultRefraction, 0.75 * Math.Max(0, Math.Min(width, height)));

    /// <summary>Where a glass pixel samples its backdrop, relative to itself.</summary>
    public static GlassVector Offset(double distance, double normalX, double normalY, double refraction = GlassOptics.DefaultRefraction)
    {
        var e = EdgeProfile(distance);
        return new GlassVector(-normalX * e * refraction, -normalY * e * refraction);
    }

    /// <summary>Antialiased coverage of the outline; <paramref name="pixel"/> is one device pixel in layout units.</summary>
    public static double Coverage(double distance, double pixel) => GlassMath.Sat(0.5 - distance / pixel);
}
