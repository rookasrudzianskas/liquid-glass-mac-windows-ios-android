using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace LiquidGlass.Tests;

/// <summary>
/// Open menu panels mix their glass toward a solid backing (OPTICS §3, Legibility backing), by 0.6, and draw their labels
/// in <see cref="GlassPalette.MenuForeground"/>; a highlighted row mixes the label color in at 8%. Untinted menus
/// (Regular or Clear, both appearances) and menus tinted with system colors keep at least 5.2:1 over any backdrop,
/// highlighted rows included.
/// </summary>
public class LegibilityTests
{
    /// <summary>The system colors of the reference's floor (spec/generate-vectors.mjs), in its order.</summary>
    private static readonly Rgb[] SystemTints = new (byte R, byte G, byte B)[]
    {
        (0, 122, 255), (52, 199, 89), (255, 59, 48), (255, 149, 0), (255, 204, 0),
        (175, 82, 222), (255, 45, 85), (88, 86, 214), (90, 200, 250), (0, 199, 190),
    }.Select(c => Rgb.FromBytes(c.R, c.G, c.B)).ToArray();

    /// <summary>The 48 accent colors of Windows' Settings › Personalization › Colors, and the Windows 11 control accent.</summary>
    private static readonly Rgb[] WindowsAccents = new[]
    {
        "FFB900", "FF8C00", "F7630C", "CA5010", "DA3B01", "EF6950", "D13438", "FF4343",
        "E74856", "E81123", "EA005E", "C30052", "E3008C", "BF0077", "C239B3", "9A0089",
        "0078D4", "0063B1", "8E8CD8", "6B69D6", "8764B8", "744DA9", "B146C2", "881798",
        "0099BC", "2D7D9A", "00B7C3", "038387", "00B294", "018574", "00CC6A", "10893E",
        "7A7574", "5D5A58", "68768A", "515C6B", "567C73", "486860", "498205", "107C10",
        "767676", "4C4A48", "69797E", "4A5459", "647C64", "525E54", "847545", "7E735F",
        "0067C0",
    }.Select(Hex).ToArray();

    private static readonly GlassMaterial[] Materials = { GlassMaterial.Regular, GlassMaterial.Clear };
    private static readonly bool[] Appearances = { false, true };

    public static IEnumerable<object[]> ColorCases => Section.GetProperty("cases").EnumerateArray().Select((_, i) => new object[] { i });

    public static IEnumerable<object[]> ForegroundCases => Section.GetProperty("menuForeground").EnumerateArray().Select((_, i) => new object[] { i });

    private static JsonElement Section => Vectors.Root.GetProperty("legibility");

    [Fact]
    public void ConstantsMatchTheReference()
    {
        Assert.Equal(GlassOptics.MenuLegibility, Section.Number("menuAmount"));
        Assert.Equal(0.6, GlassOptics.MenuLegibility);
        Assert.Equal(GlassOptics.MenuHighlight, Section.Number("highlight"));
        Assert.Equal(0.08, GlassOptics.MenuHighlight);
        var backing = Section.GetProperty("backing");
        Vectors.Near(backing.Numbers("light"), GlassPalette.LegibilityBackingLight, "light backing");
        Vectors.Near(backing.Numbers("dark"), GlassPalette.LegibilityBackingDark, "dark backing");
        Assert.Equal(Rgb.FromBytes(0xF5, 0xF5, 0xF7), GlassPalette.LegibilityBacking(false));
        Assert.Equal(Rgb.FromBytes(0x1C, 0x1C, 0x1E), GlassPalette.LegibilityBacking(true));
    }

    [Theory]
    [MemberData(nameof(ColorCases))]
    public void LegibilityColorMatchesTheReference(int index)
    {
        var c = Section.GetProperty("cases").EnumerateArray().ElementAt(index);
        var value = GlassMaterialOptics.LegibilityColor(c.Color("color"), c.Flag("dark"), c.Number("amount"));
        Vectors.Near(c.Numbers("value"), value, $"legibility color #{index}");
    }

    [Theory]
    [MemberData(nameof(ForegroundCases))]
    public void MenuForegroundMatchesTheReference(int index)
    {
        var c = Section.GetProperty("menuForeground").EnumerateArray().ElementAt(index);
        var tint = c.GetProperty("tint").ValueKind == JsonValueKind.Null ? (Rgb?)null : c.Color("tint");
        var value = GlassPalette.MenuForeground(tint, c.Material(), c.Flag("dark"));
        Vectors.Near(c.Numbers("value"), value, $"menu foreground #{index}");
        // The foreground is exactly one of the two label colors.
        Assert.True(value == GlassPalette.ForegroundLight || value == GlassPalette.ForegroundDark, $"menu foreground #{index}: {value}");
    }

    [Fact]
    public void MenuForegroundJudgesTheTintMixedTowardTheBacking()
    {
        // Without a tint, the appearance's foreground.
        foreach (var material in Materials)
        {
            Assert.Equal(GlassPalette.ForegroundLight, GlassPalette.MenuForeground(null, material, dark: false));
            Assert.Equal(GlassPalette.ForegroundDark, GlassPalette.MenuForeground(null, material, dark: true));
        }
        // The tint alone gives white labels on blue; mixed toward the light backing, the menu glass takes #111113.
        var blue = new Rgb(0, 0.478, 1);
        Assert.Equal(GlassPalette.ForegroundDark, GlassPalette.ForegroundOnTint(blue, GlassMaterial.Regular));
        Assert.Equal(GlassPalette.ForegroundLight, GlassPalette.MenuForeground(blue, GlassMaterial.Regular, dark: false));
        // With no backing the rule is the tint's own (§8).
        foreach (var tint in SystemTints.Concat(WindowsAccents))
            foreach (var material in Materials)
                foreach (var dark in Appearances)
                    Assert.Equal(GlassPalette.ForegroundOnTint(tint, material), GlassPalette.MenuForeground(tint, material, dark, legibility: 0));
        // At the menu amount the backing decides for every tint: the appearance's foreground (every channel in steps of 15).
        for (var r = 0; r <= 255; r += 15)
            for (var g = 0; g <= 255; g += 15)
                for (var b = 0; b <= 255; b += 15)
                    foreach (var material in Materials)
                        foreach (var dark in Appearances)
                        {
                            var tint = Rgb.FromBytes((byte)r, (byte)g, (byte)b);
                            Assert.Equal(GlassPalette.Foreground(dark), GlassPalette.MenuForeground(tint, material, dark));
                        }
    }

    [Fact]
    public void WorstCaseContrastMatchesTheReference()
    {
        var worst = Section.GetProperty("worstCase");
        var white = new Rgb(1, 1, 1);
        var black = new Rgb(0, 0, 0);
        var darkOverWhite = Contrast(MenuGlass(white, GlassMaterial.Regular, true), GlassPalette.ForegroundDark);
        var lightOverBlack = Contrast(MenuGlass(black, GlassMaterial.Regular, false), GlassPalette.ForegroundLight);
        Vectors.Near(worst.Number("darkOverWhite"), darkOverWhite, "white labels over white, dark appearance");
        Vectors.Near(worst.Number("lightOverBlack"), lightOverBlack, "#111113 labels over black, light appearance");
        // The floors over the reference's 6 × 6 × 6 grid of backdrops, highlighted rows included.
        var untinted = Materials.SelectMany(m => Appearances.Select(d => Floor(m, d, null, 5))).Min();
        var tinted = Materials.SelectMany(m => Appearances.SelectMany(d => SystemTints.Select(t => Floor(m, d, t, 5)))).Min();
        Vectors.Near(worst.Number("untintedWithHighlight"), untinted, "untinted menus, highlighted rows included");
        Vectors.Near(worst.Number("systemTintsWithHighlight"), tinted, "menus tinted with system colors, highlighted rows included");
        // Without the backing, the same labels fall well below 4.5:1.
        Assert.True(Contrast(GlassMaterialOptics.MaterialColor(white, GlassMaterial.Regular, true), GlassPalette.ForegroundDark) < 2);
        Assert.True(Contrast(GlassMaterialOptics.MaterialColor(black, GlassMaterial.Regular, false), GlassPalette.ForegroundLight) < 2.5);
    }

    /// <summary>
    /// The floor of every untinted menu over an 11-step grid of backdrops, highlighted rows included: the labels contrast
    /// least over white in dark appearance and over black in light, and a highlighted row a little less than a plain one.
    /// The guides give these figures.
    /// </summary>
    [Theory]
    [InlineData(GlassMaterial.Regular, false, 8.74, 7.54)]
    [InlineData(GlassMaterial.Regular, true, 6.51, 5.36)]
    [InlineData(GlassMaterial.Clear, false, 7.74, 6.71)]
    [InlineData(GlassMaterial.Clear, true, 6.35, 5.24)]
    public void UntintedMenusKeepTheFloorOverAnyBackdrop(GlassMaterial material, bool dark, double plain, double highlighted)
    {
        Assert.Equal(plain, Floor(material, dark, null, 10, highlight: false), 2);
        Assert.Equal(highlighted, Floor(material, dark, null, 10), 2);
        Assert.True(Floor(material, dark, null, 10) >= 5.2);
        var level = dark ? 1.0 : 0.0;
        var extreme = new Rgb(level, level, level);
        var foreground = GlassPalette.MenuForeground(null, material, dark);
        Assert.Equal(highlighted, Contrast(Highlight(MenuGlass(extreme, material, dark), foreground), foreground), 2);
    }

    [Fact]
    public void SystemTintsKeepTheFloorOverAnyBackdrop()
    {
        // The reference's system colors (its floor, 5.26:1, is yellow on a highlighted dark Regular row over white) and
        // the Windows accent palette (5.64:1 at worst, its gold #FFB900), over an 11-step grid.
        var system = Materials.SelectMany(m => Appearances.SelectMany(d => SystemTints.Select(t => Floor(m, d, t, 10)))).Min();
        var accents = Materials.SelectMany(m => Appearances.SelectMany(d => WindowsAccents.Select(t => Floor(m, d, t, 10)))).Min();
        Assert.Equal(5.26, system, 2);
        Assert.Equal(5.64, accents, 2);
        Assert.Equal(accents, Floor(GlassMaterial.Regular, true, Hex("FFB900"), 10));
    }

    [Theory]
    [InlineData("FFFF00", 4.37)]
    [InlineData("FFFFFF", 4.13)]
    public void VeryLightTintsAreNotCovered(string hex, double worst)
    {
        // A pure yellow or white menu in dark appearance keeps the white labels the backing calls for, but over a white
        // backdrop a highlighted Regular row falls below 4.5:1: the guides advise a deeper tint.
        var lowest = Floor(GlassMaterial.Regular, true, Hex(hex), 10);
        Assert.Equal(worst, lowest, 2);
        Assert.True(lowest < 4.5);
        Assert.True(Floor(GlassMaterial.Regular, false, Hex(hex), 10) > 10, "in light appearance they keep their labels legible");
    }

    /// <summary>
    /// The fallback material paints the backing over its veil at the 8-bit opacity that keeps the labels of an untinted
    /// menu at least as legible as on the shader's glass where they contrast least, so its floor over any backdrop,
    /// highlighted rows included, is the shader's or a little more. The guides give these figures.
    /// </summary>
    [Theory]
    [InlineData(GlassMaterial.Regular, false, 151, 7.56)]
    [InlineData(GlassMaterial.Regular, true, 158, 5.37)]
    [InlineData(GlassMaterial.Clear, false, 160, 6.75)]
    [InlineData(GlassMaterial.Clear, true, 157, 5.24)]
    public void FallbackBackingKeepsTheShadersFloor(GlassMaterial material, bool dark, int level, double floor)
    {
        var opacity = GlassFallback.LegibilityOpacity(material, dark, null, GlassOptics.MenuLegibility);
        Assert.Equal(level / 255.0, opacity, 12);
        var fallback = FallbackFloor(material, dark, null, opacity, 10);
        Assert.Equal(floor, fallback, 2);
        Assert.True(fallback >= Floor(material, dark, null, 10), $"{material} {(dark ? "dark" : "light")}: fallback {fallback:F3}:1");
        // One 8-bit level less falls below the shader's floor: the opacity is the least that keeps it.
        Assert.True(FallbackFloor(material, dark, null, (level - 1) / 255.0, 10) < Floor(material, dark, null, 10));
    }

    [Fact]
    public void TintedFallbackMenusKeepTheShadersFloor()
    {
        // The tint fill only approximates tinted glass (lighter over black, a little darker over white), so the backing
        // over it takes its own opacity per tint, which keeps every system color and Windows accent at the shader's floor.
        var levels = new List<int>();
        foreach (var tint in SystemTints.Concat(WindowsAccents))
            foreach (var material in Materials)
                foreach (var dark in Appearances)
                {
                    var opacity = GlassFallback.LegibilityOpacity(material, dark, tint, GlassOptics.MenuLegibility);
                    var level = (int)Math.Round(opacity * 255);
                    Assert.Equal(level / 255.0, opacity, 12);
                    levels.Add(level);
                    var shader = Floor(material, dark, tint, 10);
                    var fallback = FallbackFloor(material, dark, tint, opacity, 10);
                    Assert.True(fallback >= shader, $"{tint} {material} {(dark ? "dark" : "light")}: fallback {fallback:F3}:1, shader {shader:F3}:1");
                }
        // Between 0.57 and 0.72: the panel keeps refracting what is behind it.
        Assert.InRange(levels.Min(), 145, 155);
        Assert.InRange(levels.Max(), 175, 185);
        // The fallback's floor over the system colors is the shader's, yellow on a highlighted dark Regular row.
        var system = Materials.SelectMany(m => Appearances.SelectMany(d => SystemTints.Select(t =>
            FallbackFloor(m, d, t, GlassFallback.LegibilityOpacity(m, d, t, GlassOptics.MenuLegibility), 10)))).Min();
        Assert.Equal(5.27, system, 2);
    }

    [Fact]
    public void FallbackBackingFollowsTheAmount()
    {
        foreach (var material in Materials)
            foreach (var dark in Appearances)
                foreach (var tint in new Rgb?[] { null, SystemTints[0], SystemTints[4] })
                {
                    var open = GlassFallback.LegibilityOpacity(material, dark, tint, GlassOptics.MenuLegibility);
                    Assert.Equal(0, GlassFallback.LegibilityOpacity(material, dark, tint, 0));
                    var previous = 0.0;
                    for (var step = 1; step <= 20; step++)
                    {
                        // While the menu opens, the backing scales with its progress, in 8-bit levels rounded up.
                        var amount = GlassOptics.MenuLegibility * step / 20;
                        var opacity = GlassFallback.LegibilityOpacity(material, dark, tint, amount);
                        Assert.Equal(Math.Ceiling(open * 255 * step / 20 - 1e-6) / 255, opacity, 12);
                        Assert.True(opacity >= previous);
                        previous = opacity;
                    }
                    Assert.Equal(open, previous);
                }
        // The tint fill is the tint at its body strength over a mid-tone backdrop.
        var blue = SystemTints[0];
        Vectors.Near(new[] { blue.R * 0.89, blue.G * 0.89, blue.B * 0.89 }, GlassFallback.TintFill(blue, GlassMaterial.Regular), "Regular tint fill");
        Vectors.Near(new[] { blue.R * 0.84, blue.G * 0.84, blue.B * 0.84 }, GlassFallback.TintFill(blue, GlassMaterial.Clear), "Clear tint fill");
        Assert.Equal(0.9, GlassFallback.TintFillOpacity);
    }

    [Fact]
    public void FullLegibilityIsTheBackingAndZeroIsTheGlass()
    {
        var color = new Rgb(0.9, 0.2, 0.1);
        Assert.Equal(color, GlassMaterialOptics.LegibilityColor(color, true, 0));
        Vectors.Near(new[] { 0x1C / 255.0, 0x1C / 255.0, 0x1E / 255.0 }, GlassMaterialOptics.LegibilityColor(color, true, 1), "dark backing");
        Vectors.Near(new[] { 0xF5 / 255.0, 0xF5 / 255.0, 0xF7 / 255.0 }, GlassMaterialOptics.LegibilityColor(color, false, 1), "light backing");
    }

    /// <summary>
    /// The lowest contrast of a menu's labels (<see cref="GlassPalette.MenuForeground"/>) over a grid of opaque backdrops
    /// with <paramref name="steps"/> + 1 levels per channel, on plain rows and, with <paramref name="highlight"/>, on
    /// highlighted ones.
    /// </summary>
    private static double Floor(GlassMaterial material, bool dark, Rgb? tint, int steps, bool highlight = true)
    {
        var foreground = GlassPalette.MenuForeground(tint, material, dark);
        var lowest = double.MaxValue;
        for (var r = 0; r <= steps; r++)
            for (var g = 0; g <= steps; g++)
                for (var b = 0; b <= steps; b++)
                {
                    var glass = MenuGlass(new Rgb(r / (double)steps, g / (double)steps, b / (double)steps), material, dark, tint);
                    lowest = Math.Min(lowest, Contrast(glass, foreground));
                    if (highlight) lowest = Math.Min(lowest, Contrast(Highlight(glass, foreground), foreground));
                }
        return lowest;
    }

    /// <summary>
    /// The lowest contrast of a menu's labels on the fallback material over the grid of <see cref="Floor"/>, plain and
    /// highlighted rows: the veil (or tint fill) as its 8-bit brush paints it over the backdrop, then the backing at
    /// <paramref name="opacity"/>.
    /// </summary>
    private static double FallbackFloor(GlassMaterial material, bool dark, Rgb? tint, double opacity, int steps)
    {
        static double Byte(double v) => Math.Round(v * 255) / 255;
        var foreground = GlassPalette.MenuForeground(tint, material, dark);
        var backing = GlassPalette.LegibilityBacking(dark);
        var veil = GlassFallback.Veil(material, dark);
        var fill = tint is { } t ? GlassFallback.TintFill(t, material).Map(Byte) : new Rgb(Byte(veil.Gray), Byte(veil.Gray), Byte(veil.Gray));
        var fillOpacity = Byte(tint is null ? veil.Opacity : GlassFallback.TintFillOpacity);
        var lowest = double.MaxValue;
        for (var r = 0; r <= steps; r++)
            for (var g = 0; g <= steps; g++)
                for (var b = 0; b <= steps; b++)
                {
                    var backdrop = new Rgb(r / (double)steps, g / (double)steps, b / (double)steps);
                    var filled = new Rgb(GlassMath.Mix(backdrop.R, fill.R, fillOpacity), GlassMath.Mix(backdrop.G, fill.G, fillOpacity), GlassMath.Mix(backdrop.B, fill.B, fillOpacity));
                    var glass = GlassMaterialOptics.LegibilityColor(filled, dark, opacity);
                    lowest = Math.Min(lowest, Math.Min(Contrast(glass, foreground), Contrast(Highlight(glass, foreground), foreground)));
                }
        return lowest;
    }

    /// <summary>Menu glass over an opaque backdrop: the material color, tinted, mixed toward the backing.</summary>
    private static Rgb MenuGlass(Rgb backdrop, GlassMaterial material, bool dark, Rgb? tint = null)
    {
        var rgb = GlassMaterialOptics.MaterialColor(backdrop, material, dark);
        if (tint is { } t) rgb = GlassMaterialOptics.TintColor(rgb, t, material);
        return GlassMaterialOptics.LegibilityColor(rgb, dark, GlassOptics.MenuLegibility);
    }

    /// <summary>A highlighted row: the label color mixed into the glass by <see cref="GlassOptics.MenuHighlight"/>.</summary>
    private static Rgb Highlight(Rgb glass, Rgb label)
        => new(GlassMath.Mix(glass.R, label.R, GlassOptics.MenuHighlight), GlassMath.Mix(glass.G, label.G, GlassOptics.MenuHighlight),
            GlassMath.Mix(glass.B, label.B, GlassOptics.MenuHighlight));

    private static Rgb Hex(string hex) => Rgb.FromBytes(
        Convert.ToByte(hex.Substring(0, 2), 16), Convert.ToByte(hex.Substring(2, 2), 16), Convert.ToByte(hex.Substring(4, 2), 16));

    private static double Contrast(Rgb a, Rgb b)
    {
        var x = GlassPalette.RelativeLuminance(a) + 0.05;
        var y = GlassPalette.RelativeLuminance(b) + 0.05;
        return x > y ? x / y : y / x;
    }
}
