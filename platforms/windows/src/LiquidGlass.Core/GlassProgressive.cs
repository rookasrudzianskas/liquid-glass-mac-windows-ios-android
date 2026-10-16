using System;

namespace LiquidGlass;

/// <summary>Progressive edge blur: six blur levels plus the sharp backdrop (OPTICS §7).</summary>
public static class GlassProgressive
{
    /// <summary>Number of blurred levels (level 0 is the sharp backdrop).</summary>
    public const int Levels = 6;

    /// <summary>Default depth of the blurred band, in layout units.</summary>
    public const double DefaultSize = 80;

    /// <summary>Default σ of the strongest level, in layout units.</summary>
    public const double DefaultBlur = 20;

    /// <summary>The easing of depth: <c>u²(3 − 2u)</c>.</summary>
    public static double Ease(double u) => u * u * (3 - 2 * u);

    /// <summary>Gaussian σ of blur level <paramref name="level"/> (0 is sharp).</summary>
    public static double LevelBlur(int level, double blur)
    {
        var t = (double)level / Levels;
        return blur * t * t;
    }

    /// <summary>Weight of level <paramref name="level"/> at depth <paramref name="u"/> (0 = sharp side, 1 = the edge). Weights sum to 1.</summary>
    public static double Weight(int level, double u) => Math.Max(0, 1 - Math.Abs(Levels * Ease(GlassMath.Sat(u)) - level));

    /// <summary>
    /// Opacity at depth <paramref name="u"/> of level <paramref name="level"/> (1..6) when the levels are painted in order,
    /// each over the previous ones, on top of the sharp backdrop. Painting with these opacities reproduces the weighted
    /// sum of <see cref="Weight"/> exactly, because at most two adjacent levels have weight at any depth.
    /// </summary>
    public static double LayerOpacity(int level, double u)
    {
        if (level < 1 || level > Levels) throw new ArgumentOutOfRangeException(nameof(level), level, "Painted levels run from 1 to 6.");
        return GlassMath.Sat(Levels * Ease(GlassMath.Sat(u)) - (level - 1));
    }
}
