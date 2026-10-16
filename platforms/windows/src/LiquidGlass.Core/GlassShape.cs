using System;
using System.Collections.Generic;

namespace LiquidGlass;

/// <summary>Corner geometry of a glass outline (OPTICS §1).</summary>
public readonly struct ShapeParameters : IEquatable<ShapeParameters>
{
    /// <summary>Creates corner geometry.</summary>
    public ShapeParameters(double extent, double exponent)
    {
        Extent = extent;
        Exponent = exponent;
    }

    /// <summary>How far each corner reaches along the edges, in layout units.</summary>
    public double Extent { get; }

    /// <summary>Superellipse exponent of the corners (2 is circular).</summary>
    public double Exponent { get; }

    /// <inheritdoc />
    public bool Equals(ShapeParameters other) => Extent.Equals(other.Extent) && Exponent.Equals(other.Exponent);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is ShapeParameters other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked(Extent.GetHashCode() * 397 ^ Exponent.GetHashCode());

    /// <summary>Equality.</summary>
    public static bool operator ==(ShapeParameters left, ShapeParameters right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(ShapeParameters left, ShapeParameters right) => !left.Equals(right);
}

/// <summary>Signed distance (negative inside) and outward unit normal of an outline at one point.</summary>
public readonly struct ShapeSample
{
    /// <summary>Creates a sample.</summary>
    public ShapeSample(double distance, double normalX, double normalY)
    {
        Distance = distance;
        NormalX = normalX;
        NormalY = normalY;
    }

    /// <summary>Signed distance to the outline in layout units; negative inside.</summary>
    public double Distance { get; }

    /// <summary>Outward normal, horizontal component.</summary>
    public double NormalX { get; }

    /// <summary>Outward normal, vertical component (down is positive).</summary>
    public double NormalY { get; }
}

/// <summary>The continuous-corner outline shared by glass, clip paths, borders and hit shapes (OPTICS §1).</summary>
public static class GlassShape
{
    /// <summary>
    /// Corner geometry for a <paramref name="width"/> × <paramref name="height"/> outline. Large radii blend
    /// toward a circular capsule so animating the radius never jumps.
    /// </summary>
    public static ShapeParameters Parameters(double width, double height, GlassRadius radius)
    {
        var half = Math.Max(0, Math.Min(width, height) / 2);
        if (radius.IsFullyRounded) return new ShapeParameters(half, 2);
        var raw = GlassOptics.ContinuousExtent * radius.Value;
        var extent = Math.Min(raw, half);
        var t = half > 0 ? GlassMath.SmoothStep(0.75, 1, raw / half) : 1;
        return new ShapeParameters(extent, GlassMath.Mix(GlassOptics.ContinuousExponent, 2, t));
    }

    /// <summary>
    /// Signed distance and outward normal at (<paramref name="x"/>, <paramref name="y"/>), measured from the
    /// shape's center with y pointing down. Straight edges are exact; superellipse corners use the first-order
    /// distance f / |∇f|, which is exact on the outline and for exponent 2.
    /// </summary>
    public static ShapeSample Field(double x, double y, double width, double height, ShapeParameters shape)
    {
        var extent = shape.Extent;
        var sx = x < 0 ? -1.0 : 1.0;
        var sy = y < 0 ? -1.0 : 1.0;
        var qx = Math.Abs(x) - (width / 2 - extent);
        var qy = Math.Abs(y) - (height / 2 - extent);
        if (qx > 0 && qy > 0)
        {
            var n = shape.Exponent;
            var m = Math.Pow(Math.Pow(qx, n) + Math.Pow(qy, n), 1 / n);
            if (m < 1e-6) return new ShapeSample(-extent, sx * Math.Sqrt(0.5), sy * Math.Sqrt(0.5));
            var gx = Math.Pow(qx / m, n - 1);
            var gy = Math.Pow(qy / m, n - 1);
            var g = Math.Sqrt(gx * gx + gy * gy);
            return new ShapeSample((m - extent) / g, sx * gx / g, sy * gy / g);
        }
        if (qx > qy) return new ShapeSample(qx - extent, sx, 0);
        return new ShapeSample(qy - extent, 0, sy);
    }

    /// <summary>
    /// The outline as a closed polygon, clockwise from the start of the top edge, in coordinates relative to the
    /// shape's top-left corner. Each corner contributes <paramref name="segments"/> + 1 points.
    /// </summary>
    public static IReadOnlyList<GlassVector> Outline(double width, double height, GlassRadius radius, int segments = 24)
    {
        if (segments < 1) throw new ArgumentOutOfRangeException(nameof(segments), segments, "A corner needs at least one segment.");
        var shape = Parameters(width, height, radius);
        var e = shape.Extent;
        var n = shape.Exponent;
        var points = new List<GlassVector>(4 * (segments + 1));
        // Corner centers in y-down coordinates with the arc each corner sweeps.
        var corners = new[]
        {
            (cx: width - e, cy: e, a0: -Math.PI / 2, a1: 0.0),
            (cx: width - e, cy: height - e, a0: 0.0, a1: Math.PI / 2),
            (cx: e, cy: height - e, a0: Math.PI / 2, a1: Math.PI),
            (cx: e, cy: e, a0: Math.PI, a1: 3 * Math.PI / 2),
        };
        foreach (var (cx, cy, a0, a1) in corners)
        {
            for (var i = 0; i <= segments; i++)
            {
                var a = a0 + (a1 - a0) * i / segments;
                var c = Math.Cos(a);
                var s = Math.Sin(a);
                // Superellipse |x|^n + |y|^n = e^n in parametric form.
                var px = e * Math.Sign(c) * Math.Pow(Math.Abs(c), 2 / n);
                var py = e * Math.Sign(s) * Math.Pow(Math.Abs(s), 2 / n);
                points.Add(new GlassVector(cx + px, cy + py));
            }
        }
        return points;
    }
}
