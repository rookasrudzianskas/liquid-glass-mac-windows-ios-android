using System;
using System.Collections.Generic;

namespace LiquidGlass;

/// <summary>
/// The wide fill blur of Regular glass, approximated in the shader by sampling the frost over a sparse grid
/// (OPTICS §3 allows this). Gaussian variances add, so blurring the σ = 7.33 frost by
/// <c>σₑ = √(σ_fill² − 7.33²)</c> gives the σ_fill fill. The kernel is a separable 9 × 9 grid whose 1-D weights are a
/// discrete Gaussian, chosen so the grid's variance equals σₑ² exactly; the result therefore matches the true
/// Gaussian to second order on smooth backdrops. The taps are about 2.1 frost σ apart: the already-frosted
/// texture keeps about 1% of the energy at that spacing, so the sparse grid shows no visible aliasing.
/// </summary>
public sealed class FrostFillKernel
{
    /// <summary>Taps on each side of the center (the grid is (2·Radius + 1)² taps).</summary>
    public const int Radius = 4;

    /// <summary>Tap spacing as a fraction of σₑ.</summary>
    private const double SpacingPerSigma = 1 / 1.4;

    private static readonly FrostFillKernel LightKernel = Create(GlassMaterials.FillSigmaOverFrost(false));
    private static readonly FrostFillKernel DarkKernel = Create(GlassMaterials.FillSigmaOverFrost(true));

    private FrostFillKernel(double sigma, double spacing, double[] weights)
    {
        Sigma = sigma;
        Spacing = spacing;
        Weights = weights;
    }

    /// <summary>The Gaussian σ this kernel reproduces, in layout units.</summary>
    public double Sigma { get; }

    /// <summary>Distance between taps, in layout units.</summary>
    public double Spacing { get; }

    /// <summary>
    /// 1-D weights of the taps at offsets 0, ±1, ±2, ±3, ±4 (one value per distance); the 2-D weight of tap (i, j) is
    /// <c>Weights[|i|] · Weights[|j|]</c>. They sum to 1 over the 9 taps.
    /// </summary>
    public IReadOnlyList<double> Weights { get; }

    /// <summary>The kernel for an appearance.</summary>
    public static FrostFillKernel For(bool dark) => dark ? DarkKernel : LightKernel;

    /// <summary>Variance of the 1-D kernel in layout units².</summary>
    public double Variance
    {
        get
        {
            double variance = 0;
            for (var k = 1; k <= Radius; k++) variance += 2 * Weights[k] * k * k;
            return variance * Spacing * Spacing;
        }
    }

    private static FrostFillKernel Create(double sigma)
    {
        var spacing = sigma * SpacingPerSigma;
        var target = 1 / (SpacingPerSigma * SpacingPerSigma); // variance in tap units
        double lo = 0.05, hi = 50;
        for (var i = 0; i < 200; i++)
        {
            var mid = (lo + hi) / 2;
            if (TapVariance(mid) < target) lo = mid; else hi = mid;
        }
        return new FrostFillKernel(sigma, spacing, Normalized((lo + hi) / 2));
    }

    private static double[] Normalized(double tapSigma)
    {
        var weights = new double[Radius + 1];
        double sum = 0;
        for (var k = 0; k <= Radius; k++)
        {
            weights[k] = Math.Exp(-k * k / (2 * tapSigma * tapSigma));
            sum += k == 0 ? weights[k] : 2 * weights[k];
        }
        for (var k = 0; k <= Radius; k++) weights[k] /= sum;
        return weights;
    }

    private static double TapVariance(double tapSigma)
    {
        var w = Normalized(tapSigma);
        double variance = 0;
        for (var k = 1; k <= Radius; k++) variance += 2 * w[k] * k * k;
        return variance;
    }
}
