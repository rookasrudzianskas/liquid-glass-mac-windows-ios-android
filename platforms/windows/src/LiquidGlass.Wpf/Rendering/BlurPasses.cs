using System;
using System.Collections.Generic;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Plans Gaussian blurs for WPF's <see cref="System.Windows.Media.Effects.BlurEffect"/>, which renders
/// <c>trunc(Radius × world scale)</c> device pixels of radius, at most 100, with σ = radius / 3. A blur too wide for one
/// effect at the display's scale is split into nested effects of equal radius; their variances add, so together they
/// blur by the σ asked for at every display scale.
/// </summary>
internal static class BlurPasses
{
    /// <summary>The largest radius, in device pixels, one <c>BlurEffect</c> renders.</summary>
    public const int MaxDeviceRadius = 100;

    /// <summary>
    /// The <c>Radius</c> of each nested effect (layout units) that together blur by <paramref name="sigma"/> layout
    /// units at <paramref name="scale"/> device pixels per unit. Empty when the blur rounds to nothing.
    /// </summary>
    public static double[] Radii(double sigma, double scale)
    {
        if (!(sigma > 0) || !(scale > 0) || double.IsInfinity(sigma) || double.IsInfinity(scale)) return Array.Empty<double>();
        var device = 3 * sigma * scale;
        var count = Math.Max(1, (int)Math.Ceiling(device * device / ((double)MaxDeviceRadius * MaxDeviceRadius) - 1e-9));
        // Equal passes: √count of them add up to the whole radius, and each stays within the limit.
        var whole = Math.Round(device / Math.Sqrt(count));
        if (whole < 1) return Array.Empty<double>();
        // Half a device pixel above the whole radius, so WPF's truncation lands exactly on it.
        var radius = (whole + 0.5) / scale;
        var radii = new double[count];
        for (var i = 0; i < count; i++) radii[i] = radius;
        return radii;
    }

    /// <summary>The σ, in layout units, that WPF renders for nested effects of <paramref name="radii"/> at <paramref name="scale"/>.</summary>
    public static double RenderedSigma(IEnumerable<double> radii, double scale)
    {
        var variance = 0.0;
        foreach (var radius in radii)
        {
            var device = Math.Min(MaxDeviceRadius, Math.Floor(radius * scale));
            variance += device / 3 * (device / 3);
        }
        return Math.Sqrt(variance) / scale;
    }

    /// <summary>How far, in layout units, the source must extend past what is drawn for nested effects of <paramref name="radii"/>.</summary>
    public static double Reach(IEnumerable<double> radii)
    {
        var reach = 0.0;
        foreach (var radius in radii) reach += radius;
        return reach;
    }
}
