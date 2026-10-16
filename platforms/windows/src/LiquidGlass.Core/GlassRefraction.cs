using System;

namespace LiquidGlass;

/// <summary>Refraction through the 20-unit bevel inside the outline (OPTICS §2).</summary>
public static class GlassRefraction
{
    /// <summary>Displacement weight: 1 on the outline, falling to 0 at <paramref name="bevel"/> inside.</summary>
    public static double EdgeProfile(double distance, double bevel = GlassOptics.Bevel)
    {
        var depth = GlassMath.Sat(-distance / bevel);
        return 1 - GlassMath.Sat(Math.Sqrt((2 - depth) * depth));
    }

    /// <summary>Where a glass pixel samples its backdrop, relative to itself.</summary>
    public static GlassVector Offset(double distance, double normalX, double normalY, double refraction = GlassOptics.DefaultRefraction)
    {
        var e = EdgeProfile(distance);
        return new GlassVector(-normalX * e * refraction, -normalY * e * refraction);
    }

    /// <summary>Antialiased coverage of the outline; <paramref name="pixel"/> is one device pixel in layout units.</summary>
    public static double Coverage(double distance, double pixel) => GlassMath.Sat(0.5 - distance / pixel);
}
