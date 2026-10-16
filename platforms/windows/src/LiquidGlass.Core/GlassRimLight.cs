using System;

namespace LiquidGlass;

/// <summary>
/// The rim overlay painted over the glass (OPTICS §4): a specular line along the top and bottom of the outline,
/// a soft inner diffuse glow, and a shade on the left and right sides.
/// </summary>
public static class GlassRimLight
{
    private const double Spread = 1.08;
    private const double SideSpread = 0.98;
    private static readonly double SpecularCos = Math.Cos(Spread);
    private static readonly double DiffuseCos = Math.Cos(Spread * 0.65);
    private static readonly double SideCos = Math.Cos(SideSpread);

    /// <summary>The premultiplied rim color at a point of the field.</summary>
    /// <param name="distance">Signed distance to the outline (negative inside).</param>
    /// <param name="normalX">Outward normal, x.</param>
    /// <param name="normalY">Outward normal, y (down).</param>
    /// <param name="dark">Dark appearance.</param>
    /// <param name="pixel">One device pixel in layout units.</param>
    /// <param name="tint">The glass tint, if any.</param>
    public static Rgba Overlay(double distance, double normalX, double normalY, bool dark, double pixel, Rgb? tint = null)
    {
        var d = distance;
        var aa = Math.Max(pixel, 1e-6);
        var k = dark ? 1.0 : 0.0;
        var cover = GlassMath.Sat(0.5 - d / aa);
        var directional = Math.Pow(GlassMath.Sat((Math.Abs(normalY) - SpecularCos) / (1 - SpecularCos)), 1.25);
        var diffuseDir = Math.Pow(GlassMath.Sat((Math.Abs(normalY) - DiffuseCos) / (1 - DiffuseCos)), 1.25);
        var stroke = GlassMath.Sat((d + 0.9) / aa + 0.5) * GlassMath.Sat(0.5 - d / aa);
        var specular = directional * stroke;
        specular /= 1 + 0.5 * (1 - specular);
        var inner = 1 - GlassMath.Sat(-d / 4.5);
        var diffuse = diffuseDir * 0.18 * inner * inner * cover;
        var highlight = GlassMath.Sat(specular * GlassMath.Mix(1, 0.82, k) + diffuse * GlassMath.Mix(1, 0.4, k));
        var adjusted = d - 0.5;
        var falloff = 1 - GlassMath.Sat(-adjusted / 0.5);
        var ramp = GlassMath.Mix(falloff > 0 ? 1 : 0, falloff, 0.75);
        var outline = ramp * GlassMath.Sat((adjusted + 0.5) / aa + 0.5) * GlassMath.Sat(-adjusted / aa + 0.5);
        var w1 = GlassMath.Sat((normalX - SideCos) / (1 - SideCos)) * outline;
        var w2 = GlassMath.Sat((-normalX - SideCos) / (1 - SideCos)) * outline;
        var shade = (w1 / (1 + 0.4 * (1 - w1)) + w2 / (1 + 0.4 * (1 - w2))) * GlassMath.Mix(0.48, 0.6, k);
        var alpha = highlight + shade * (1 - highlight);
        if (tint is { } t)
            return new Rgba(highlight * (0.45 + 0.55 * t.R), highlight * (0.45 + 0.55 * t.G), highlight * (0.45 + 0.55 * t.B), alpha);
        var light = GlassMath.Mix(1, 0.82, k) * highlight;
        return new Rgba(light, light, light, alpha);
    }
}
