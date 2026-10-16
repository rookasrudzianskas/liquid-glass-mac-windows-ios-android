using System;

namespace LiquidGlass;

/// <summary>Horizontal and vertical scale of a pressed or stretched surface.</summary>
public readonly struct GlassScale
{
    /// <summary>Creates a scale.</summary>
    public GlassScale(double sx, double sy)
    {
        Sx = sx;
        Sy = sy;
    }

    /// <summary>The identity scale.</summary>
    public static GlassScale Identity { get; } = new(1, 1);

    /// <summary>Horizontal scale.</summary>
    public double Sx { get; }

    /// <summary>Vertical scale.</summary>
    public double Sy { get; }
}

/// <summary>The light that blooms under the pointer of an interactive surface.</summary>
public readonly struct GlowParameters
{
    /// <summary>Creates glow parameters.</summary>
    public GlowParameters(double radius, double alpha, double hover)
    {
        Radius = radius;
        Alpha = alpha;
        Hover = hover;
    }

    /// <summary>Radius at which the glow falls to zero, in layout units.</summary>
    public double Radius { get; }

    /// <summary>Peak alpha of the white glow at full strength.</summary>
    public double Alpha { get; }

    /// <summary>Glow strength while a pointer hovers (press drives it to 1).</summary>
    public double Hover { get; }
}

/// <summary>Press, drag and glow response of interactive glass (OPTICS §6).</summary>
public static class GlassInteractionModel
{
    /// <summary>Controls at most this large give a little under the finger; larger surfaces only at their edges.</summary>
    public const double SmallControlSize = 72;

    /// <summary>iOS-style rubber band: follows a short drag, then resists.</summary>
    public static double RubberBand(double offset, double limit, double coefficient = 0.55)
    {
        if (limit <= 0) return 0;
        var m = Math.Abs(offset);
        return Math.Sign(offset) * limit * (1 - 1 / (m * coefficient / limit + 1));
    }

    /// <summary>How far a held surface may stretch toward the pointer before resisting.</summary>
    public static double StretchLimit(double width, double height)
    {
        var size = Math.Max(width, height);
        return size <= SmallControlSize ? 4 + 0.15 * size : Math.Min(10, 3 + 0.04 * size);
    }

    /// <summary>
    /// Press and drag response. <paramref name="press"/> is the press spring's value (0..1), <paramref name="tx"/> and
    /// <paramref name="ty"/> the drag spring values and <paramref name="vx"/>, <paramref name="vy"/> their velocities.
    /// Volume is preserved: stretching one axis narrows the other.
    /// </summary>
    public static GlassScale PressScale(double width, double height, double press, double tx = 0, double ty = 0,
        double vx = 0, double vy = 0, bool reduced = false)
    {
        if (reduced) return GlassScale.Identity;
        var size = Math.Max(width, height);
        var grow = Math.Min(0.16, 8 / size) * press;
        var cap = size <= SmallControlSize ? 0.2 : 0.12;
        var ex = Math.Min(cap, 0.5 * Math.Abs(tx) / width + 0.00007 * Math.Abs(vx));
        var ey = Math.Min(cap, 0.5 * Math.Abs(ty) / height + 0.00007 * Math.Abs(vy));
        return new GlassScale((1 + grow) * (1 + ex) / Math.Sqrt(1 + ey), (1 + grow) * (1 + ey) / Math.Sqrt(1 + ex));
    }

    /// <summary>Glow radius and strength for a surface.</summary>
    public static GlowParameters Glow(double width, double height, bool dark)
        => new(GlassMath.Clamp(Math.Max(width, height) * 0.9, 36, 180), dark ? 0.16 : 0.3, 0.35);
}
