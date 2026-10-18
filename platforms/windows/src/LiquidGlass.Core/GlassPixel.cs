using System;
using System.ComponentModel;

namespace LiquidGlass;

/// <summary>Everything that determines how one glass surface looks, independent of its backdrop.</summary>
public readonly struct GlassSurfaceOptics
{
    /// <summary>Creates surface optics.</summary>
    /// <param name="width">Width of the outline in layout units.</param>
    /// <param name="height">Height of the outline in layout units.</param>
    /// <param name="radius">Corner radius.</param>
    /// <param name="material">Material.</param>
    /// <param name="dark">Dark appearance.</param>
    /// <param name="refraction">Optical travel at the outline, in layout units.</param>
    /// <param name="tint">Tint color, if any.</param>
    /// <param name="pixel">One device pixel in layout units.</param>
    /// <param name="nested">The surface sits inside other glass.</param>
    /// <param name="legibility">
    /// How far the glass mixes toward the solid legibility backing: <see cref="GlassOptics.MenuLegibility"/> on menu
    /// panels, 0 elsewhere.
    /// </param>
    public GlassSurfaceOptics(double width, double height, GlassRadius radius, GlassMaterial material = GlassMaterial.Clear,
        bool dark = false, double refraction = GlassOptics.DefaultRefraction, Rgb? tint = null, double pixel = 1.0 / 3, bool nested = false,
        double legibility = 0)
    {
        Width = width;
        Height = height;
        Radius = radius;
        Material = material;
        Dark = dark;
        Refraction = refraction;
        Tint = tint;
        Pixel = pixel;
        Nested = nested;
        Legibility = legibility;
    }

    /// <summary>Creates surface optics without the legibility backing (the 1.0.0 signature, kept for compiled callers).</summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    public GlassSurfaceOptics(double width, double height, GlassRadius radius, GlassMaterial material, bool dark, double refraction, Rgb? tint,
        double pixel, bool nested)
        : this(width, height, radius, material, dark, refraction, tint, pixel, nested, 0)
    {
    }

    /// <summary>Width of the outline in layout units.</summary>
    public double Width { get; }

    /// <summary>Height of the outline in layout units.</summary>
    public double Height { get; }

    /// <summary>Corner radius.</summary>
    public GlassRadius Radius { get; }

    /// <summary>Material.</summary>
    public GlassMaterial Material { get; }

    /// <summary>Dark appearance.</summary>
    public bool Dark { get; }

    /// <summary>Optical travel at the outline, in layout units.</summary>
    public double Refraction { get; }

    /// <summary>Tint color, if any.</summary>
    public Rgb? Tint { get; }

    /// <summary>One device pixel in layout units.</summary>
    public double Pixel { get; }

    /// <summary>The surface sits inside other glass.</summary>
    public bool Nested { get; }

    /// <summary>
    /// How far the glass mixes toward the solid legibility backing, after the material color and tint
    /// (<see cref="GlassMaterialOptics.LegibilityColor"/>): <see cref="GlassOptics.MenuLegibility"/> on menu panels, 0 elsewhere.
    /// </summary>
    public double Legibility { get; }
}

/// <summary>Composites one pixel of a glass surface exactly as the shaders do.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#5-composite">Liquid Glass optics specification: Composite</seealso>
public static class GlassPixel
{
    /// <summary>Composites the glass pixel whose center is at (<paramref name="x"/>, <paramref name="y"/>) relative to the surface center.</summary>
    /// <param name="x">Pixel center, x, relative to the surface center.</param>
    /// <param name="y">Pixel center, y (down), relative to the surface center.</param>
    /// <param name="optics">The surface.</param>
    /// <param name="sample">
    /// Backdrop color (unpremultiplied) at the pixel offset by (dx, dy). Regular glass samples its frosted backdrop.
    /// </param>
    /// <param name="backdrop">The unrefracted backdrop at the pixel, premultiplied.</param>
    /// <returns>The premultiplied result.</returns>
    public static Rgba Compose(double x, double y, in GlassSurfaceOptics optics, Func<double, double, Rgba> sample, Rgba backdrop)
    {
        if (sample is null) throw new ArgumentNullException(nameof(sample));
        var shape = GlassShape.Parameters(optics.Width, optics.Height, optics.Radius);
        var field = GlassShape.Field(x, y, optics.Width, optics.Height, shape);
        var cover = GlassRefraction.Coverage(field.Distance, optics.Pixel);
        var offset = GlassRefraction.Offset(field.Distance, field.NormalX, field.NormalY, optics.Refraction);
        var src = sample(offset.X, offset.Y);
        var rgb = GlassMaterialOptics.MaterialColor(src.Rgb, optics.Material, optics.Dark, optics.Nested);
        var a = src.A;
        if (optics.Tint is { } tint)
        {
            rgb = GlassMaterialOptics.TintColor(rgb, tint, optics.Material);
            a = GlassMaterialOptics.TintAlpha(a, optics.Material);
        }
        if (optics.Legibility > 0) rgb = GlassMaterialOptics.LegibilityColor(rgb, optics.Dark, optics.Legibility);
        var ga = a * cover;
        var glass = new Rgba(rgb.R * ga + backdrop.R * (1 - ga), rgb.G * ga + backdrop.G * (1 - ga), rgb.B * ga + backdrop.B * (1 - ga), ga + backdrop.A * (1 - ga));
        var rim = GlassRimLight.Overlay(field.Distance, field.NormalX, field.NormalY, optics.Dark, optics.Pixel, optics.Tint);
        return new Rgba(rim.R + glass.R * (1 - rim.A), rim.G + glass.G * (1 - rim.A), rim.B + glass.B * (1 - rim.A), rim.A + glass.A * (1 - rim.A));
    }

    /// <summary>
    /// The pointer glow (premultiplied white) at a point, before clipping to the glass: alpha 0.30 light or 0.16 dark
    /// times <paramref name="amount"/>, falling linearly to 0 at <paramref name="radius"/>.
    /// </summary>
    public static double GlowAlpha(double x, double y, double centerX, double centerY, double radius, bool dark, double amount)
    {
        var dx = x - centerX;
        var dy = y - centerY;
        return (dark ? 0.16 : 0.3) * amount * (1 - GlassMath.Sat(Math.Sqrt(dx * dx + dy * dy) / radius));
    }
}
