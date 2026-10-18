using Xunit;

namespace LiquidGlass.Tests;

/// <summary>Labels on tinted glass take white or #111113, whichever contrasts more with the tinted glass (OPTICS §8).</summary>
public class ForegroundTests
{
    private static Rgb Hex(int rgb) => Rgb.FromBytes((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb);

    [Theory]
    [InlineData(0x30D158)] // green (dark variant)
    [InlineData(0x34C759)] // green
    [InlineData(0xFF9500)] // orange
    [InlineData(0xFF9F0A)] // orange (dark variant)
    [InlineData(0xFFCC00)] // yellow
    [InlineData(0x00C7BE)] // mint
    [InlineData(0x32ADE6)] // cyan
    [InlineData(0xFFFFFF)]
    public void LightTintsTakeTheDarkLabel(int tint)
    {
        Assert.Equal(GlassPalette.ForegroundLight, GlassPalette.ForegroundOnTint(Hex(tint), GlassMaterial.Clear));
        Assert.Equal(GlassPalette.ForegroundLight, GlassPalette.ForegroundOnTint(Hex(tint), GlassMaterial.Regular));
    }

    [Theory]
    [InlineData(0x0A84FF)] // blue (dark variant)
    [InlineData(0x007AFF)] // blue
    [InlineData(0xFF3B30)] // red
    [InlineData(0xAF52DE)] // purple
    [InlineData(0x5856D6)] // indigo
    [InlineData(0xFF2D55)] // pink
    [InlineData(0x000000)]
    public void SaturatedTintsTakeWhite(int tint)
    {
        Assert.Equal(GlassPalette.ForegroundDark, GlassPalette.ForegroundOnTint(Hex(tint), GlassMaterial.Clear));
        Assert.Equal(GlassPalette.ForegroundDark, GlassPalette.ForegroundOnTint(Hex(tint), GlassMaterial.Regular));
    }

    [Fact]
    public void TheChosenLabelHasTheHigherContrast()
    {
        var dark = GlassPalette.RelativeLuminance(GlassPalette.ForegroundLight);
        foreach (var material in new[] { GlassMaterial.Clear, GlassMaterial.Regular })
        {
            var body = GlassMaterials.TintBody(material);
            var pass = body + (1 - body) * 0.5;
            for (var step = 0; step <= 40; step++)
            {
                var v = step / 40.0;
                foreach (var tint in new[] { new Rgb(v, v, v), new Rgb(v, 0.4, 1 - v), new Rgb(1, v, 0) })
                {
                    var glass = GlassPalette.RelativeLuminance(new Rgb(tint.R * pass, tint.G * pass, tint.B * pass));
                    var expected = (glass + 0.05) / (dark + 0.05) > 1.05 / (glass + 0.05)
                        ? GlassPalette.ForegroundLight
                        : GlassPalette.ForegroundDark;
                    Assert.Equal(expected, GlassPalette.ForegroundOnTint(tint, material));
                }
            }
        }
    }

    [Fact]
    public void RelativeLuminanceIsWcag2()
    {
        Assert.InRange(GlassPalette.RelativeLuminance(new Rgb(1, 1, 1)), 1 - 1e-9, 1 + 1e-9);
        Assert.InRange(GlassPalette.RelativeLuminance(new Rgb(0, 0, 0)), -1e-9, 1e-9);
        // #808080 under the WCAG 2 sRGB transfer: 0.2158605.
        Assert.InRange(GlassPalette.RelativeLuminance(Rgb.FromBytes(128, 128, 128)), 0.2158600, 0.2158610);
    }
}
