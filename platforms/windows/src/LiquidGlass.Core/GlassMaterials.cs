using System;

namespace LiquidGlass;

/// <summary>Tone parameters of Clear glass in one appearance.</summary>
public sealed class ClearToneSpec
{
    internal ClearToneSpec(double shadowLift, double highlightRolloff, double veilScale, double veilOffset, double veilWeight)
    {
        ShadowLift = shadowLift;
        HighlightRolloff = highlightRolloff;
        VeilScale = veilScale;
        VeilOffset = veilOffset;
        VeilWeight = veilWeight;
    }

    /// <summary>Lift applied to shadows: <c>lift · (1 − v)³</c>.</summary>
    public double ShadowLift { get; }

    /// <summary>Roll-off applied to highlights: <c>rolloff · v³</c>.</summary>
    public double HighlightRolloff { get; }

    /// <summary>Veil slope: <c>veil = scale · v + offset</c>.</summary>
    public double VeilScale { get; }

    /// <summary>Veil offset.</summary>
    public double VeilOffset { get; }

    /// <summary>How much of the veil is mixed in.</summary>
    public double VeilWeight { get; }
}

/// <summary>Frost and tone parameters of Regular glass in one appearance.</summary>
public sealed class RegularToneSpec
{
    internal RegularToneSpec(double fillSigma, double fillOpacity, double a, double b, double q, double chroma, double toneWeight)
    {
        FillSigma = fillSigma;
        FillOpacity = fillOpacity;
        A = a;
        B = b;
        Q = q;
        Chroma = chroma;
        ToneWeight = toneWeight;
    }

    /// <summary>Gaussian σ of the wide fill blur, in layout units.</summary>
    public double FillSigma { get; }

    /// <summary>Opacity of the fill blend over the frost.</summary>
    public double FillOpacity { get; }

    /// <summary>Constant term of the tone curve <c>a + b·v + q·v²</c>.</summary>
    public double A { get; }

    /// <summary>Linear term of the tone curve.</summary>
    public double B { get; }

    /// <summary>Quadratic term of the tone curve.</summary>
    public double Q { get; }

    /// <summary>Saturation applied after toning (SVG <c>feColorMatrix type="saturate"</c>).</summary>
    public double Chroma { get; }

    /// <summary>How much of the tone curve is mixed in.</summary>
    public double ToneWeight { get; }
}

/// <summary>Material constants, ported from the Glass HQ web library.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#3-materials">Liquid Glass optics specification: Materials</seealso>
public static class GlassMaterials
{
    /// <summary>Clear glass is not frosted.</summary>
    public const double ClearBlur = 0;

    /// <summary>Gaussian σ of the Regular frost, in layout units.</summary>
    public const double RegularBlur = 7.33;

    /// <summary>Body of tinted Clear glass.</summary>
    public const double ClearTintBody = 0.68;

    /// <summary>Body of tinted Regular glass.</summary>
    public const double RegularTintBody = 0.78;

    /// <summary>Clear glass, light appearance.</summary>
    public static ClearToneSpec ClearLight { get; } = new(0.18, 0, 0.78, 0.26, 0.06);

    /// <summary>Clear glass, dark appearance.</summary>
    public static ClearToneSpec ClearDark { get; } = new(0.15, 0.2, 0.38, 0.2, 0.14);

    /// <summary>Regular glass, light appearance.</summary>
    public static RegularToneSpec RegularLight { get; } = new(23, 0.34, 0.36, 0.67, -0.1113, 1.1031, 0.8);

    /// <summary>Regular glass, dark appearance.</summary>
    public static RegularToneSpec RegularDark { get; } = new(21.8, 0.65, 0.105, 0.83, -0.2091, 1.0648, 0.9);

    /// <summary>Clear tone parameters for an appearance.</summary>
    public static ClearToneSpec Clear(bool dark) => dark ? ClearDark : ClearLight;

    /// <summary>Regular parameters for an appearance.</summary>
    public static RegularToneSpec Regular(bool dark) => dark ? RegularDark : RegularLight;

    /// <summary>Tint body of a material: how much pigment the glass carries.</summary>
    public static double TintBody(GlassMaterial material) => material == GlassMaterial.Regular ? RegularTintBody : ClearTintBody;

    /// <summary>Frost blur σ of a material, in layout units.</summary>
    public static double Blur(GlassMaterial material) => material == GlassMaterial.Regular ? RegularBlur : ClearBlur;

    /// <summary>
    /// The extra Gaussian σ that turns the frost (σ = <see cref="RegularBlur"/>) into the fill
    /// (σ = <see cref="RegularToneSpec.FillSigma"/>): Gaussian variances add.
    /// </summary>
    public static double FillSigmaOverFrost(bool dark)
    {
        var fill = Regular(dark).FillSigma;
        return Math.Sqrt(fill * fill - RegularBlur * RegularBlur);
    }
}
