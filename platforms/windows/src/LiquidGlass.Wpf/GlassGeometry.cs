using System;
using System.Windows;
using System.Windows.Media;

namespace LiquidGlass.Wpf;

/// <summary>
/// Continuous-corner outlines as WPF geometry: the exact outline glass is drawn with, for clips, borders,
/// focus rings and hit shapes.
/// </summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#1-shape">Liquid Glass optics specification: Shape</seealso>
public static class GlassGeometry
{
    /// <summary>A frozen outline of <paramref name="size"/> at the origin.</summary>
    public static Geometry Create(Size size, GlassRadius radius) => Create(new Rect(size), radius);

    /// <summary>A frozen outline filling <paramref name="bounds"/>.</summary>
    public static Geometry Create(Rect bounds, GlassRadius radius)
    {
        if (bounds.IsEmpty || bounds.Width <= 0 || bounds.Height <= 0) return Geometry.Empty;
        var shape = GlassShape.Parameters(bounds.Width, bounds.Height, radius);
        var segments = (int)GlassMath.Clamp(Math.Ceiling(shape.Extent / 1.5), 6, 64);
        var outline = GlassShape.Outline(bounds.Width, bounds.Height, radius, segments);
        var geometry = new StreamGeometry { FillRule = FillRule.Nonzero };
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(bounds.X + outline[0].X, bounds.Y + outline[0].Y), true, true);
            var points = new Point[outline.Count - 1];
            for (var i = 1; i < outline.Count; i++) points[i - 1] = new Point(bounds.X + outline[i].X, bounds.Y + outline[i].Y);
            context.PolyLineTo(points, true, true);
        }
        geometry.Freeze();
        return geometry;
    }

    /// <summary>
    /// The outline of <paramref name="bounds"/> grown by <paramref name="amount"/> on every side, keeping the corners
    /// concentric: a fixed radius grows so the corner reaches <paramref name="amount"/> further along each edge.
    /// </summary>
    public static Geometry Inflate(Rect bounds, GlassRadius radius, double amount)
    {
        var grown = new Rect(bounds.X - amount, bounds.Y - amount, Math.Max(0, bounds.Width + 2 * amount), Math.Max(0, bounds.Height + 2 * amount));
        return Create(grown, InflateRadius(radius, amount));
    }

    /// <summary>The radius of an outline grown by <paramref name="amount"/> (see <see cref="Inflate"/>).</summary>
    public static GlassRadius InflateRadius(GlassRadius radius, double amount)
        => radius.IsFullyRounded ? radius : GlassRadius.FromValue(Math.Max(0, radius.Value + amount / GlassOptics.ContinuousExtent));
}
