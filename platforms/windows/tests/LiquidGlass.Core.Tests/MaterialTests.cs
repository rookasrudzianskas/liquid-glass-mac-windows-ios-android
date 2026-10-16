using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace LiquidGlass.Tests;

public class MaterialTests
{
    public static IEnumerable<object[]> MaterialColorCases => Vectors.Cases("materialColor").Select((_, i) => new object[] { i });

    public static IEnumerable<object[]> TintColorCases => Vectors.Cases("tintColor").Select((_, i) => new object[] { i });

    public static IEnumerable<object[]> FrostColorCases => Vectors.Cases("frostColor").Select((_, i) => new object[] { i });

    [Fact]
    public void MaterialConstantsMatchTheReference()
    {
        var m = Vectors.Root.GetProperty("materials");
        var clear = m.GetProperty("clear");
        var regular = m.GetProperty("regular");
        Assert.Equal(GlassMaterials.ClearBlur, clear.Number("blur"));
        Assert.Equal(GlassMaterials.ClearTintBody, clear.Number("tintBody"));
        Assert.Equal(clear.GetProperty("light").Number("shadowLift"), GlassMaterials.ClearLight.ShadowLift);
        Assert.Equal(clear.GetProperty("light").Number("highlightRolloff"), GlassMaterials.ClearLight.HighlightRolloff);
        Assert.Equal(clear.GetProperty("dark").Number("shadowLift"), GlassMaterials.ClearDark.ShadowLift);
        Assert.Equal(clear.GetProperty("dark").Number("highlightRolloff"), GlassMaterials.ClearDark.HighlightRolloff);
        Assert.Equal(GlassMaterials.RegularBlur, regular.Number("blur"));
        Assert.Equal(GlassMaterials.RegularTintBody, regular.Number("tintBody"));
        foreach (var (name, spec) in new[] { ("light", GlassMaterials.RegularLight), ("dark", GlassMaterials.RegularDark) })
        {
            var e = regular.GetProperty(name);
            Assert.Equal(e.Number("fillSigma"), spec.FillSigma);
            Assert.Equal(e.Number("fillOpacity"), spec.FillOpacity);
            Assert.Equal(e.Numbers("tone"), new[] { spec.A, spec.B, spec.Q });
            Assert.Equal(e.Number("chroma"), spec.Chroma);
            Assert.Equal(e.Number("toneWeight"), spec.ToneWeight);
        }
    }

    [Fact]
    public void ToneCurvesMatchTheReference()
    {
        foreach (var c in Vectors.Cases("tone"))
        {
            var v = c.Number("v");
            Vectors.Near(c.Number("clearLight"), GlassMaterialOptics.ClearTone(v, false), $"clear light {v}");
            Vectors.Near(c.Number("clearDark"), GlassMaterialOptics.ClearTone(v, true), $"clear dark {v}");
            Vectors.Near(c.Number("clearDarkNested"), GlassMaterialOptics.ClearTone(v, true, true), $"clear dark nested {v}");
            Vectors.Near(c.Number("regularLight"), GlassMaterialOptics.RegularTone(v, false), $"regular light {v}");
            Vectors.Near(c.Number("regularDark"), GlassMaterialOptics.RegularTone(v, true), $"regular dark {v}");
        }
    }

    [Theory]
    [MemberData(nameof(MaterialColorCases))]
    public void MaterialColorMatchesTheReference(int index)
    {
        var c = Vectors.Cases("materialColor").ElementAt(index);
        var value = GlassMaterialOptics.MaterialColor(c.Color("color"), c.Material(), c.Flag("dark"));
        Vectors.Near(c.Numbers("value"), value, $"material color #{index}");
    }

    [Theory]
    [MemberData(nameof(TintColorCases))]
    public void TintColorMatchesTheReference(int index)
    {
        var c = Vectors.Cases("tintColor").ElementAt(index);
        var value = GlassMaterialOptics.TintColor(c.Color("color"), c.Color("tint"), c.Material());
        Vectors.Near(c.Numbers("value"), value, $"tint color #{index}");
    }

    [Theory]
    [MemberData(nameof(FrostColorCases))]
    public void FrostColorMatchesTheReference(int index)
    {
        var c = Vectors.Cases("frostColor").ElementAt(index);
        var value = GlassMaterialOptics.FrostColor(c.Color("frost"), c.Color("fill"), c.Flag("dark"));
        Vectors.Near(c.Numbers("value"), value, $"frost color #{index}");
    }

    [Fact]
    public void SaturateKeepsGraysAndIsTheIdentityAtOne()
    {
        var gray = new Rgb(0.4, 0.4, 0.4);
        var saturated = GlassMaterialOptics.Saturate(gray, 1.7);
        Vectors.Near(0.4, saturated.R, "r");
        Vectors.Near(0.4, saturated.G, "g");
        Vectors.Near(0.4, saturated.B, "b");
        var color = new Rgb(0.9, 0.2, 0.1);
        var same = GlassMaterialOptics.Saturate(color, 1);
        Vectors.Near(color.R, same.R, "r");
        Vectors.Near(color.G, same.G, "g");
        Vectors.Near(color.B, same.B, "b");
    }

    [Fact]
    public void FallbackVeilsFollowTheToneCurves()
    {
        foreach (var material in new[] { GlassMaterial.Clear, GlassMaterial.Regular })
            foreach (var dark in new[] { false, true })
            {
                var veil = GlassFallback.Veil(material, dark);
                Assert.InRange(veil.Gray, 0, 1);
                Assert.InRange(veil.Opacity, 0.02, 0.6);
                for (var v = 0.0; v <= 1; v += 0.05)
                {
                    var tone = material == GlassMaterial.Regular ? GlassMaterialOptics.RegularTone(v, dark) : GlassMaterialOptics.ClearTone(v, dark);
                    Assert.True(Math.Abs(veil.Apply(v) - tone) < 0.07, $"{material} dark={dark}: veil {veil.Apply(v)} vs tone {tone} at {v}");
                }
            }
        // Light glass brightens, dark glass deepens.
        foreach (var material in new[] { GlassMaterial.Clear, GlassMaterial.Regular })
        {
            Assert.True(GlassFallback.Veil(material, false).Gray > 0.75);
            Assert.True(GlassFallback.Veil(material, true).Gray < 0.45);
        }
    }
}
