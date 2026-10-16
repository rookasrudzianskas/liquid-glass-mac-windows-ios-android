using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiquidGlass.Tests;

public class ShapeTests
{
    public static IEnumerable<object[]> ParameterCases => Vectors.Cases("shapeParams").Select((_, i) => new object[] { i });

    public static IEnumerable<object[]> FieldCases => Vectors.Cases("shapeField").Select((_, i) => new object[] { i });

    [Fact]
    public void ConstantsMatchTheReference()
    {
        var constants = Vectors.Root.GetProperty("constants");
        Assert.Equal(GlassOptics.Bevel, constants.Number("bevel"));
        Assert.Equal(GlassOptics.DefaultRefraction, constants.Number("defaultRefraction"));
        Assert.Equal(GlassOptics.ContinuousExtent, constants.Number("continuousExtent"));
        Assert.Equal(GlassOptics.ContinuousExponent, constants.Number("continuousExponent"));
        Assert.Equal(new[] { GlassOptics.LumaRed, GlassOptics.LumaGreen, GlassOptics.LumaBlue }, constants.Numbers("luma"));
    }

    [Theory]
    [MemberData(nameof(ParameterCases))]
    public void ShapeParametersMatchTheReference(int index)
    {
        var c = Vectors.Cases("shapeParams").ElementAt(index);
        var p = GlassShape.Parameters(c.Number("width"), c.Number("height"), c.Radius());
        Vectors.Near(c.Number("extent"), p.Extent, $"extent #{index}");
        Vectors.Near(c.Number("exponent"), p.Exponent, $"exponent #{index}");
    }

    [Theory]
    [MemberData(nameof(FieldCases))]
    public void ShapeFieldMatchesTheReference(int index)
    {
        var c = Vectors.Cases("shapeField").ElementAt(index);
        var width = c.Number("width");
        var height = c.Number("height");
        var shape = GlassShape.Parameters(width, height, c.Radius());
        var f = GlassShape.Field(c.Number("x"), c.Number("y"), width, height, shape);
        Vectors.Near(c.Number("distance"), f.Distance, $"distance #{index}");
        Vectors.Near(c.Number("nx"), f.NormalX, $"nx #{index}");
        Vectors.Near(c.Number("ny"), f.NormalY, $"ny #{index}");
    }

    [Fact]
    public void OutlineMatchesTheReference()
    {
        var o = Vectors.Root.GetProperty("outline");
        var points = GlassShape.Outline(o.Number("width"), o.Number("height"), o.Radius(), (int)o.Number("segments"));
        var expected = o.GetProperty("points").EnumerateArray().Select(p => p.EnumerateArray().Select(v => v.GetDouble()).ToArray()).ToArray();
        Assert.Equal(expected.Length, points.Count);
        for (var i = 0; i < expected.Length; i++)
        {
            Vectors.Near(expected[i][0], points[i].X, $"outline x #{i}");
            Vectors.Near(expected[i][1], points[i].Y, $"outline y #{i}");
        }
    }

    [Fact]
    public void OutlinePointsLieOnTheZeroDistanceContour()
    {
        foreach (var radius in new GlassRadius[] { 0, 8, 16, 28, 40, GlassRadius.Capsule })
        {
            const double width = 180, height = 90;
            var shape = GlassShape.Parameters(width, height, radius);
            foreach (var p in GlassShape.Outline(width, height, radius, 32))
            {
                var f = GlassShape.Field(p.X - width / 2, p.Y - height / 2, width, height, shape);
                Assert.True(Math.Abs(f.Distance) < 1e-6, $"radius {radius}: point {p} is {f.Distance} from the outline");
            }
        }
    }

    [Fact]
    public void NormalsAreUnitLength()
    {
        var shape = GlassShape.Parameters(120, 44, 12);
        for (var x = -70.0; x <= 70; x += 3.7)
            for (var y = -30.0; y <= 30; y += 2.9)
            {
                var f = GlassShape.Field(x, y, 120, 44, shape);
                Assert.True(Math.Abs(Math.Sqrt(f.NormalX * f.NormalX + f.NormalY * f.NormalY) - 1) < 1e-9);
            }
    }

    [Fact]
    public void LargeRadiiBlendIntoACapsuleWithoutJumping()
    {
        // Animating the radius must never jump: parameters are continuous in the radius.
        var previous = GlassShape.Parameters(200, 64, 0);
        for (var r = 0.25; r <= 40; r += 0.25)
        {
            var p = GlassShape.Parameters(200, 64, r);
            Assert.True(Math.Abs(p.Extent - previous.Extent) <= 1.528 * 0.25 + 1e-9);
            Assert.True(Math.Abs(p.Exponent - previous.Exponent) < 0.12);
            previous = p;
        }
        Assert.Equal(GlassShape.Parameters(200, 64, GlassRadius.Capsule), GlassShape.Parameters(200, 64, 40));
    }

    [Theory]
    [InlineData("capsule", GlassRadiusKind.Capsule, 0)]
    [InlineData(" Circle ", GlassRadiusKind.Circle, 0)]
    [InlineData("16", GlassRadiusKind.Fixed, 16)]
    [InlineData("12.5", GlassRadiusKind.Fixed, 12.5)]
    [InlineData("0", GlassRadiusKind.Fixed, 0)]
    public void RadiusParses(string text, GlassRadiusKind kind, double value)
    {
        var radius = GlassRadius.Parse(text);
        Assert.Equal(kind, radius.Kind);
        Assert.Equal(value, radius.Value);
        Assert.Equal(radius, GlassRadius.Parse(radius.ToString()));
    }

    [Theory]
    [InlineData("-1")]
    [InlineData("pill")]
    [InlineData("")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    public void InvalidRadiiAreRejected(string text)
    {
        Assert.False(GlassRadius.TryParse(text, out _));
        Assert.Throws<FormatException>(() => GlassRadius.Parse(text));
    }

    [Fact]
    public void RadiusConverterRoundTrips()
    {
        var converter = new GlassRadiusConverter();
        Assert.Equal(GlassRadius.Capsule, converter.ConvertFrom("capsule"));
        Assert.Equal(GlassRadius.FromValue(28), converter.ConvertFrom(28.0));
        Assert.Equal("circle", converter.ConvertTo(GlassRadius.Circle, typeof(string)));
        Assert.Throws<ArgumentOutOfRangeException>(() => GlassRadius.FromValue(-2));
    }
}
