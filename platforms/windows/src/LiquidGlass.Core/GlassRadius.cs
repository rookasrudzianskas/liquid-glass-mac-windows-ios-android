using System;
using System.ComponentModel;
using System.Globalization;

namespace LiquidGlass;

/// <summary>The kind of corner a <see cref="GlassRadius"/> describes.</summary>
public enum GlassRadiusKind
{
    /// <summary>A continuous corner with a fixed radius in layout units.</summary>
    Fixed,

    /// <summary>Fully rounded ends (a pill).</summary>
    Capsule,

    /// <summary>Fully rounded; used for square controls such as icon buttons.</summary>
    Circle,
}

/// <summary>
/// Corner radius of a glass outline: a nonnegative number of layout units, <c>capsule</c> or <c>circle</c>.
/// Fixed radii are drawn as continuous (superellipse) corners. In XAML write <c>16</c>, <c>capsule</c> or <c>circle</c>.
/// </summary>
[TypeConverter(typeof(GlassRadiusConverter))]
public readonly struct GlassRadius : IEquatable<GlassRadius>
{
    private GlassRadius(GlassRadiusKind kind, double value)
    {
        Kind = kind;
        Value = value;
    }

    /// <summary>Fully rounded ends.</summary>
    public static GlassRadius Capsule { get; } = new(GlassRadiusKind.Capsule, 0);

    /// <summary>Fully rounded, for square controls.</summary>
    public static GlassRadius Circle { get; } = new(GlassRadiusKind.Circle, 0);

    /// <summary>The kind of corner.</summary>
    public GlassRadiusKind Kind { get; }

    /// <summary>The radius in layout units when <see cref="Kind"/> is <see cref="GlassRadiusKind.Fixed"/>; otherwise 0.</summary>
    public double Value { get; }

    /// <summary>True for <see cref="Capsule"/> and <see cref="Circle"/>.</summary>
    public bool IsFullyRounded => Kind != GlassRadiusKind.Fixed;

    /// <summary>A continuous corner with the given radius.</summary>
    /// <exception cref="ArgumentOutOfRangeException">The radius is negative, infinite or NaN.</exception>
    public static GlassRadius FromValue(double radius)
    {
        if (double.IsNaN(radius) || double.IsInfinity(radius) || radius < 0)
            throw new ArgumentOutOfRangeException(nameof(radius), radius, "A glass radius must be a finite nonnegative number, 'capsule' or 'circle'.");
        return new GlassRadius(GlassRadiusKind.Fixed, radius);
    }

    /// <summary>Converts a number of layout units to a fixed radius.</summary>
    public static implicit operator GlassRadius(double radius) => FromValue(radius);

    /// <summary>Parses <c>capsule</c>, <c>circle</c> or a number (invariant culture).</summary>
    /// <exception cref="FormatException">The text is not a radius.</exception>
    public static GlassRadius Parse(string text)
    {
        if (TryParse(text, out var radius)) return radius;
        throw new FormatException($"'{text}' is not a glass radius. Use a nonnegative number, 'capsule' or 'circle'.");
    }

    /// <summary>Parses <c>capsule</c>, <c>circle</c> or a number (invariant culture).</summary>
    public static bool TryParse(string? text, out GlassRadius radius)
    {
        radius = default;
        if (text is null) return false;
        var trimmed = text.Trim();
        if (string.Equals(trimmed, "capsule", StringComparison.OrdinalIgnoreCase)) { radius = Capsule; return true; }
        if (string.Equals(trimmed, "circle", StringComparison.OrdinalIgnoreCase)) { radius = Circle; return true; }
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            && !double.IsNaN(value) && !double.IsInfinity(value) && value >= 0)
        {
            radius = new GlassRadius(GlassRadiusKind.Fixed, value);
            return true;
        }
        return false;
    }

    /// <inheritdoc />
    public bool Equals(GlassRadius other) => Kind == other.Kind && Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is GlassRadius other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => ((int)Kind * 397) ^ Value.GetHashCode();

    /// <summary>Equality.</summary>
    public static bool operator ==(GlassRadius left, GlassRadius right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(GlassRadius left, GlassRadius right) => !left.Equals(right);

    /// <summary><c>capsule</c>, <c>circle</c> or the number in invariant culture.</summary>
    public override string ToString() => Kind switch
    {
        GlassRadiusKind.Capsule => "capsule",
        GlassRadiusKind.Circle => "circle",
        _ => Value.ToString("R", CultureInfo.InvariantCulture),
    };
}

/// <summary>Converts XAML and designer text (<c>16</c>, <c>capsule</c>, <c>circle</c>) to <see cref="GlassRadius"/>.</summary>
public sealed class GlassRadiusConverter : TypeConverter
{
    /// <inheritdoc />
    public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        => sourceType == typeof(string) || sourceType == typeof(double) || sourceType == typeof(int) || base.CanConvertFrom(context, sourceType);

    /// <inheritdoc />
    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
        => destinationType == typeof(string) || base.CanConvertTo(context, destinationType);

    /// <inheritdoc />
    public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value) => value switch
    {
        string text => GlassRadius.Parse(text),
        double number => GlassRadius.FromValue(number),
        int number => GlassRadius.FromValue(number),
        _ => base.ConvertFrom(context, culture, value),
    };

    /// <inheritdoc />
    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
        => value is GlassRadius radius && destinationType == typeof(string)
            ? radius.ToString()
            : base.ConvertTo(context, culture, value, destinationType);
}
