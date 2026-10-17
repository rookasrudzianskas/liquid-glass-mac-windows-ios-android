using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiquidGlass.Tests;

public class OpticsTests
{
    public static IEnumerable<object[]> RimCases => Vectors.Cases("rimOverlay").Select((_, i) => new object[] { i });

    public static IEnumerable<object[]> PixelCases => Vectors.Root.GetProperty("glassPixel").GetProperty("cases").EnumerateArray().Select((_, i) => new object[] { i });

    [Fact]
    public void EdgeProfileMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("edgeProfile"))
            Vectors.Near(c.Number("value"), GlassRefraction.EdgeProfile(c.Number("distance")), $"edge profile at {c.Number("distance")}");
    }

    [Fact]
    public void RefractionOffsetMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("refractionOffset"))
        {
            var o = GlassRefraction.Offset(c.Number("distance"), c.Number("nx"), c.Number("ny"), c.Number("refraction"));
            Vectors.Near(c.Number("dx"), o.X, "dx");
            Vectors.Near(c.Number("dy"), o.Y, "dy");
        }
    }

    [Fact]
    public void DefaultRefractionMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("defaultRefraction"))
            Vectors.Near(c.Number("value"), GlassRefraction.Default(c.Number("width"), c.Number("height")), $"default refraction of {c.Number("width")} x {c.Number("height")}");
    }

    [Fact]
    public void RefractionPointsInwardAndVanishesBeyondTheBevel()
    {
        // The top edge's normal points up; the backdrop is drawn from deeper inside, below the pixel.
        var top = GlassRefraction.Offset(-1, 0, -1);
        Assert.True(top.Y > 0);
        Assert.Equal(0, top.X);
        Assert.Equal(new GlassVector(0, 0), GlassRefraction.Offset(-GlassOptics.Bevel, 0, -1));
        Assert.Equal(1, GlassRefraction.EdgeProfile(0));
    }

    [Fact]
    public void CoverageAntialiasesOverOnePixel()
    {
        Assert.Equal(1, GlassRefraction.Coverage(-1, 0.5));
        Assert.Equal(0.5, GlassRefraction.Coverage(0, 0.5));
        Assert.Equal(0, GlassRefraction.Coverage(0.25, 0.5));
    }

    [Theory]
    [MemberData(nameof(RimCases))]
    public void RimOverlayMatchesTheReference(int index)
    {
        var c = Vectors.Cases("rimOverlay").ElementAt(index);
        var rim = GlassRimLight.Overlay(c.Number("distance"), c.Number("nx"), c.Number("ny"), c.Flag("dark"), Vectors.Exact(c.Number("pixel")), c.OptionalColor("tint"));
        Vectors.Near(c.Numbers("value"), rim, $"rim #{index}");
    }

    [Theory]
    [MemberData(nameof(PixelCases))]
    public void GlassPixelMatchesTheReference(int index)
    {
        var c = Vectors.Root.GetProperty("glassPixel").GetProperty("cases").EnumerateArray().ElementAt(index);
        var x = c.Number("x");
        var y = c.Number("y");
        var optics = new GlassSurfaceOptics(c.Number("width"), c.Number("height"), c.Radius(), c.Material(), c.Flag("dark"),
            c.Number("refraction"), c.OptionalColor("tint"), Vectors.Exact(c.Number("pixel")));
        var value = GlassPixel.Compose(x, y, optics, (dx, dy) => Backdrop(x + dx, y + dy), Backdrop(x, y));
        Vectors.Near(c.Numbers("value"), value, $"glass pixel #{index}");
    }

    [Fact]
    public void GlowFallsLinearlyToTheRadius()
    {
        Assert.Equal(0.3, GlassPixel.GlowAlpha(5, 5, 5, 5, 40, false, 1), 12);
        Assert.Equal(0.08, GlassPixel.GlowAlpha(25, 5, 5, 5, 40, true, 1), 12);
        Assert.Equal(0, GlassPixel.GlowAlpha(60, 5, 5, 5, 40, false, 1));
    }

    /// <summary>The linear backdrop of vectors.json: rgb = (sat(0.5 + px/400), sat(0.5 + py/400), 0.6), alpha 1.</summary>
    private static Rgba Backdrop(double px, double py) => new(GlassMath.Sat(0.5 + px / 400), GlassMath.Sat(0.5 + py / 400), 0.6, 1);
}
