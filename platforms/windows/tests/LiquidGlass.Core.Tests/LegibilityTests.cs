using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiquidGlass.Tests;

/// <summary>
/// Menu panels mix their glass halfway toward a solid backing (OPTICS §3, Legibility backing), so text on a Regular
/// menu keeps at least 5:1 contrast over any backdrop (a Clear menu about 4.9:1).
/// </summary>
public class LegibilityTests
{
    public static IEnumerable<object[]> ColorCases => Vectors.Root.GetProperty("legibility").GetProperty("cases").EnumerateArray().Select((_, i) => new object[] { i });

    private static System.Text.Json.JsonElement Section => Vectors.Root.GetProperty("legibility");

    [Fact]
    public void ConstantsMatchTheReference()
    {
        Assert.Equal(GlassOptics.MenuLegibility, Section.Number("menuAmount"));
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

    [Fact]
    public void WorstCaseContrastMatchesTheReference()
    {
        var worst = Section.GetProperty("worstCase");
        var white = new Rgb(1, 1, 1);
        var black = new Rgb(0, 0, 0);
        var darkOverWhite = Contrast(MenuGlass(white, dark: true), GlassPalette.ForegroundDark);
        var lightOverBlack = Contrast(MenuGlass(black, dark: false), GlassPalette.ForegroundLight);
        Vectors.Near(worst.Number("darkOverWhite"), darkOverWhite, "white labels over white, dark appearance");
        Vectors.Near(worst.Number("lightOverBlack"), lightOverBlack, "#111113 labels over black, light appearance");
        // Without the backing, the same labels fall well below 4.5:1.
        Assert.True(Contrast(GlassMaterialOptics.MaterialColor(white, GlassMaterial.Regular, true), GlassPalette.ForegroundDark) < 2);
        Assert.True(Contrast(GlassMaterialOptics.MaterialColor(black, GlassMaterial.Regular, false), GlassPalette.ForegroundLight) < 2.5);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void MenuTextKeepsFiveToOneOverAnyBackdrop(bool dark)
    {
        // Regular menu glass over every backdrop color of an 11-step RGB grid, with the appearance's foreground.
        const int steps = 10;
        var foreground = GlassPalette.Foreground(dark);
        var lowest = double.MaxValue;
        for (var r = 0; r <= steps; r++)
            for (var g = 0; g <= steps; g++)
                for (var b = 0; b <= steps; b++)
                {
                    var backdrop = new Rgb(r / (double)steps, g / (double)steps, b / (double)steps);
                    var contrast = Contrast(MenuGlass(backdrop, dark), foreground);
                    if (contrast < lowest) lowest = contrast;
                    Assert.True(contrast >= 5.0, $"{(dark ? "dark" : "light")} menu glass over {backdrop}: {contrast:F3}:1");
                }
        Assert.InRange(lowest, 5.0, dark ? 5.1 : 7.2);
    }

    [Theory]
    [InlineData(false, 6.07)]
    [InlineData(true, 4.94)]
    public void ClearMenuTextStaysNearFiveToOne(bool dark, double worst)
    {
        // Clear glass tones its backdrop less than Regular, so with the same backing white labels over white in dark
        // appearance stay just under 5:1; the guides give this figure for Clear panels.
        const int steps = 10;
        var foreground = GlassPalette.Foreground(dark);
        var lowest = double.MaxValue;
        for (var r = 0; r <= steps; r++)
            for (var g = 0; g <= steps; g++)
                for (var b = 0; b <= steps; b++)
                {
                    var backdrop = new Rgb(r / (double)steps, g / (double)steps, b / (double)steps);
                    lowest = System.Math.Min(lowest, Contrast(MenuGlass(backdrop, dark, GlassMaterial.Clear), foreground));
                }
        Assert.Equal(worst, lowest, 2);
    }

    [Fact]
    public void FullLegibilityIsTheBackingAndZeroIsTheGlass()
    {
        var color = new Rgb(0.9, 0.2, 0.1);
        Assert.Equal(color, GlassMaterialOptics.LegibilityColor(color, true, 0));
        Vectors.Near(new[] { 0x1C / 255.0, 0x1C / 255.0, 0x1E / 255.0 }, GlassMaterialOptics.LegibilityColor(color, true, 1), "dark backing");
        Vectors.Near(new[] { 0xF5 / 255.0, 0xF5 / 255.0, 0xF7 / 255.0 }, GlassMaterialOptics.LegibilityColor(color, false, 1), "light backing");
    }

    /// <summary>Menu glass (Regular by default) over an opaque backdrop: the material color mixed toward the backing.</summary>
    private static Rgb MenuGlass(Rgb backdrop, bool dark, GlassMaterial material = GlassMaterial.Regular)
        => GlassMaterialOptics.LegibilityColor(GlassMaterialOptics.MaterialColor(backdrop, material, dark), dark, GlassOptics.MenuLegibility);

    private static double Contrast(Rgb a, Rgb b)
    {
        var x = GlassPalette.RelativeLuminance(a) + 0.05;
        var y = GlassPalette.RelativeLuminance(b) + 0.05;
        return x > y ? x / y : y / x;
    }
}
