using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace LiquidGlass.Tests;

/// <summary>Access to <c>spec/vectors.json</c>, generated from <c>spec/reference.mjs</c>.</summary>
internal static class Vectors
{
    /// <summary>Tolerance for CPU code (OPTICS.md).</summary>
    public const double Tolerance = 1e-4;

    private static readonly Lazy<JsonDocument> Document = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors.json"))));

    public static JsonElement Root => Document.Value.RootElement;

    public static IEnumerable<JsonElement> Cases(string name) => Root.GetProperty(name).EnumerateArray();

    public static double Number(this JsonElement element, string name) => element.GetProperty(name).GetDouble();

    public static bool Flag(this JsonElement element, string name) => element.TryGetProperty(name, out var value) && value.GetBoolean();

    public static double[] Numbers(this JsonElement element, string name)
        => element.GetProperty(name).EnumerateArray().Select(e => e.GetDouble()).ToArray();

    public static Rgb Color(this JsonElement element, string name)
    {
        var c = element.Numbers(name);
        return new Rgb(c[0], c[1], c[2]);
    }

    public static Rgb? OptionalColor(this JsonElement element, string name)
        => element.TryGetProperty(name, out _) ? element.Color(name) : null;

    public static GlassMaterial Material(this JsonElement element)
        => element.GetProperty("material").GetString() == "regular" ? GlassMaterial.Regular : GlassMaterial.Clear;

    public static GlassRadius Radius(this JsonElement element)
    {
        var radius = element.GetProperty("radius");
        return radius.ValueKind == JsonValueKind.String ? GlassRadius.Parse(radius.GetString()!) : GlassRadius.FromValue(radius.GetDouble());
    }

    /// <summary>
    /// The generator evaluates with pixel = 1/3 (and dt = 1/60) and stores those values rounded to six decimals;
    /// recover the exact fraction so rounding does not compound through the model.
    /// </summary>
    public static double Exact(double rounded)
    {
        var reciprocal = Math.Round(1 / rounded);
        return Math.Abs(1 / reciprocal - rounded) < 1e-6 ? 1 / reciprocal : rounded;
    }

    public static void Near(double expected, double actual, string what, double tolerance = Tolerance)
        => Assert.True(Math.Abs(expected - actual) <= tolerance, $"{what}: expected {expected}, got {actual} (|Δ| = {Math.Abs(expected - actual)})");

    public static void Near(double[] expected, Rgb actual, string what)
    {
        Near(expected[0], actual.R, what + " r");
        Near(expected[1], actual.G, what + " g");
        Near(expected[2], actual.B, what + " b");
    }

    public static void Near(double[] expected, Rgba actual, string what, double tolerance = Tolerance)
    {
        Near(expected[0], actual.R, what + " r", tolerance);
        Near(expected[1], actual.G, what + " g", tolerance);
        Near(expected[2], actual.B, what + " b", tolerance);
        Near(expected[3], actual.A, what + " a", tolerance);
    }
}
