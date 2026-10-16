using System;
using System.IO;
using System.Text.Json;

namespace LiquidGlass.Wpf.Tests;

internal static class Vectors
{
    /// <summary>Tolerance for values read back from a GPU (OPTICS.md).</summary>
    public const double GpuTolerance = 2e-3;

    private static readonly Lazy<JsonDocument> Document = new(() =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "vectors.json"))));

    public static JsonElement Root => Document.Value.RootElement;

    public static double Number(this JsonElement e, string name) => e.GetProperty(name).GetDouble();

    public static bool Flag(this JsonElement e, string name) => e.TryGetProperty(name, out var v) && v.GetBoolean();

    /// <summary>The shader's radius encoding: a negative radius means capsule.</summary>
    public static double ShaderRadius(this JsonElement e)
    {
        var radius = e.GetProperty("radius");
        return radius.ValueKind == JsonValueKind.String ? -1 : radius.GetDouble();
    }
}
