using System;

namespace LiquidGlass;

/// <summary>Color response of the materials on sRGB, unpremultiplied channels.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#3-materials">Liquid Glass optics specification: Materials</seealso>
public static class GlassMaterialOptics
{
    /// <summary>Luma with the SVG <c>feColorMatrix</c> weights.</summary>
    public static double Luma(Rgb color) => color.Luma;

    /// <summary>SVG <c>feColorMatrix type="saturate"</c>, clamped to <c>[0, 1]</c>.</summary>
    public static Rgb Saturate(Rgb c, double s) => new(
        GlassMath.Sat((0.213 + 0.787 * s) * c.R + (0.715 - 0.715 * s) * c.G + (0.072 - 0.072 * s) * c.B),
        GlassMath.Sat((0.213 - 0.213 * s) * c.R + (0.715 + 0.285 * s) * c.G + (0.072 - 0.072 * s) * c.B),
        GlassMath.Sat((0.213 - 0.213 * s) * c.R + (0.715 - 0.715 * s) * c.G + (0.072 + 0.928 * s) * c.B));

    /// <summary>Clear material on one channel: lift shadows, roll off highlights, add a faint veil.</summary>
    /// <param name="v">The channel value.</param>
    /// <param name="dark">Dark appearance.</param>
    /// <param name="nested">The surface sits inside other glass (adds layer light in dark appearance).</param>
    public static double ClearTone(double v, bool dark, bool nested = false)
    {
        var spec = GlassMaterials.Clear(dark);
        var inv = 1 - v;
        var tone = v + spec.ShadowLift * inv * inv * inv - spec.HighlightRolloff * v * v * v;
        var veil = spec.VeilScale * v + spec.VeilOffset;
        var layerLight = dark && nested ? 0.08 * inv : 0;
        return GlassMath.Sat((1 - spec.VeilWeight) * tone + spec.VeilWeight * veil + layerLight);
    }

    /// <summary>Regular material on one channel: the calibrated curve, before saturation.</summary>
    public static double RegularTone(double v, bool dark)
    {
        var spec = GlassMaterials.Regular(dark);
        return GlassMath.Sat(spec.ToneWeight * (spec.A + spec.B * v + spec.Q * v * v) + (1 - spec.ToneWeight) * v);
    }

    /// <summary>Full material color response on an unpremultiplied sRGB color.</summary>
    public static Rgb MaterialColor(Rgb color, GlassMaterial material, bool dark, bool nested = false)
    {
        if (material == GlassMaterial.Regular)
        {
            var toned = new Rgb(RegularTone(color.R, dark), RegularTone(color.G, dark), RegularTone(color.B, dark));
            return Saturate(toned, GlassMaterials.Regular(dark).Chroma);
        }
        return new Rgb(ClearTone(color.R, dark, nested), ClearTone(color.G, dark, nested), ClearTone(color.B, dark, nested));
    }

    /// <summary>Colored glass transmits luminance through the pigment.</summary>
    public static Rgb TintColor(Rgb color, Rgb tint, GlassMaterial material)
    {
        var body = GlassMaterials.TintBody(material);
        var transmitted = body + (1 - body) * color.Luma;
        return new Rgb(GlassMath.Sat(tint.R * transmitted), GlassMath.Sat(tint.G * transmitted), GlassMath.Sat(tint.B * transmitted));
    }

    /// <summary>Alpha of tinted glass over a backdrop of alpha <paramref name="alpha"/>.</summary>
    public static double TintAlpha(double alpha, GlassMaterial material)
    {
        var body = GlassMaterials.TintBody(material);
        return body + (1 - body) * alpha;
    }

    /// <summary>
    /// Mixes a glass color toward the solid legibility backing of the appearance (<see cref="GlassPalette.LegibilityBacking"/>)
    /// by <paramref name="amount"/>: <see cref="GlassOptics.MenuLegibility"/> (0.5) on menu panels, 0 (the color unchanged)
    /// elsewhere. Applied after the material color and tint.
    /// </summary>
    /// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#legibility-backing-menus">Liquid Glass optics specification: Legibility backing</seealso>
    public static Rgb LegibilityColor(Rgb color, bool dark, double amount)
    {
        var backing = GlassPalette.LegibilityBacking(dark);
        return new Rgb(GlassMath.Mix(color.R, backing.R, amount), GlassMath.Mix(color.G, backing.G, amount), GlassMath.Mix(color.B, backing.B, amount));
    }

    /// <summary>
    /// Regular glass refracts a frosted backdrop: the σ = 7.33 <paramref name="frost"/> brightened (light) or
    /// deepened (dark) by the wide <paramref name="fill"/> blur at the same point.
    /// </summary>
    public static Rgb FrostColor(Rgb frost, Rgb fill, bool dark)
    {
        var opacity = GlassMaterials.Regular(dark).FillOpacity;
        double Blend(double f, double w) => opacity * (dark ? Math.Min(f, w) : Math.Max(f, w)) + (1 - opacity) * f;
        return new Rgb(Blend(frost.R, fill.R), Blend(frost.G, fill.G), Blend(frost.B, fill.B));
    }
}
