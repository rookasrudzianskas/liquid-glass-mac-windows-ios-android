using System;
using System.Globalization;

namespace LiquidGlass;

/// <summary>An sRGB gamma-encoded color with channels in <c>[0, 1]</c>.</summary>
public readonly struct Rgb : IEquatable<Rgb>
{
    /// <summary>Creates a color.</summary>
    public Rgb(double r, double g, double b)
    {
        R = r;
        G = g;
        B = b;
    }

    /// <summary>Red.</summary>
    public double R { get; }

    /// <summary>Green.</summary>
    public double G { get; }

    /// <summary>Blue.</summary>
    public double B { get; }

    /// <summary>Luma with the SVG <c>feColorMatrix</c> weights <c>(0.213, 0.715, 0.072)</c>.</summary>
    public double Luma => GlassOptics.LumaRed * R + GlassOptics.LumaGreen * G + GlassOptics.LumaBlue * B;

    /// <summary>Creates a color from 8-bit channels.</summary>
    public static Rgb FromBytes(byte r, byte g, byte b) => new(r / 255.0, g / 255.0, b / 255.0);

    /// <summary>Applies <paramref name="f"/> to each channel.</summary>
    public Rgb Map(Func<double, double> f) => new(f(R), f(G), f(B));

    /// <inheritdoc />
    public bool Equals(Rgb other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rgb other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked((R.GetHashCode() * 397 ^ G.GetHashCode()) * 397 ^ B.GetHashCode());

    /// <summary>Equality.</summary>
    public static bool operator ==(Rgb left, Rgb right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(Rgb left, Rgb right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "rgb({0:0.####}, {1:0.####}, {2:0.####})", R, G, B);
}

/// <summary>A color with alpha; whether it is premultiplied is stated where it is used.</summary>
public readonly struct Rgba : IEquatable<Rgba>
{
    /// <summary>Creates a color.</summary>
    public Rgba(double r, double g, double b, double a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    /// <summary>Creates a color from a color and an alpha.</summary>
    public Rgba(Rgb rgb, double a) : this(rgb.R, rgb.G, rgb.B, a)
    {
    }

    /// <summary>Red.</summary>
    public double R { get; }

    /// <summary>Green.</summary>
    public double G { get; }

    /// <summary>Blue.</summary>
    public double B { get; }

    /// <summary>Alpha.</summary>
    public double A { get; }

    /// <summary>The color channels.</summary>
    public Rgb Rgb => new(R, G, B);

    /// <inheritdoc />
    public bool Equals(Rgba other) => R.Equals(other.R) && G.Equals(other.G) && B.Equals(other.B) && A.Equals(other.A);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Rgba other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked(((R.GetHashCode() * 397 ^ G.GetHashCode()) * 397 ^ B.GetHashCode()) * 397 ^ A.GetHashCode());

    /// <summary>Equality.</summary>
    public static bool operator ==(Rgba left, Rgba right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(Rgba left, Rgba right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "rgba({0:0.####}, {1:0.####}, {2:0.####}, {3:0.####})", R, G, B, A);
}

/// <summary>A displacement or position in layout units, y pointing down.</summary>
public readonly struct GlassVector : IEquatable<GlassVector>
{
    /// <summary>Creates a vector.</summary>
    public GlassVector(double x, double y)
    {
        X = x;
        Y = y;
    }

    /// <summary>Horizontal component.</summary>
    public double X { get; }

    /// <summary>Vertical component (down is positive).</summary>
    public double Y { get; }

    /// <inheritdoc />
    public bool Equals(GlassVector other) => X.Equals(other.X) && Y.Equals(other.Y);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GlassVector other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked(X.GetHashCode() * 397 ^ Y.GetHashCode());

    /// <summary>Equality.</summary>
    public static bool operator ==(GlassVector left, GlassVector right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(GlassVector left, GlassVector right) => !left.Equals(right);

    /// <inheritdoc />
    public override string ToString() => string.Format(CultureInfo.InvariantCulture, "({0:0.####}, {1:0.####})", X, Y);
}
