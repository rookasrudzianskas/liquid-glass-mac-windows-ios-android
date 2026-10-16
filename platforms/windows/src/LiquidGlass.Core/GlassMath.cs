using System;

namespace LiquidGlass;

/// <summary>Scalar helpers with the exact semantics of the shared reference model.</summary>
public static class GlassMath
{
    /// <summary>Clamps <paramref name="x"/> to <c>[lo, hi]</c>.</summary>
    public static double Clamp(double x, double lo, double hi) => Math.Min(hi, Math.Max(lo, x));

    /// <summary>Clamps <paramref name="x"/> to <c>[0, 1]</c> (GLSL <c>clamp(x, 0, 1)</c>).</summary>
    public static double Sat(double x) => Clamp(x, 0, 1);

    /// <summary>Linear interpolation from <paramref name="a"/> to <paramref name="b"/> (GLSL <c>mix</c>).</summary>
    public static double Mix(double a, double b, double t) => a + (b - a) * t;

    /// <summary>Hermite smoothstep between two edges (GLSL <c>smoothstep</c>).</summary>
    public static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Sat((x - edge0) / (edge1 - edge0));
        return t * t * (3 - 2 * t);
    }
}
