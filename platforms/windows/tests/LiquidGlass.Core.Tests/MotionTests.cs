using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Xunit;

namespace LiquidGlass.Tests;

public class MotionTests
{
    public static IEnumerable<object[]> SpringNames => Vectors.Root.GetProperty("springs").EnumerateObject().Select(p => new object[] { p.Name });

    private static SpringOptions Named(string name) => name switch
    {
        "press" => GlassSprings.Press,
        "release" => GlassSprings.Release,
        "glow" => GlassSprings.Glow,
        "morph" => GlassSprings.Morph,
        "layout" => GlassSprings.Layout,
        "track" => GlassSprings.Track,
        _ => throw new ArgumentException(name),
    };

    [Theory]
    [MemberData(nameof(SpringNames))]
    public void SpringsMatchTheReference(string name)
    {
        var e = Vectors.Root.GetProperty("springs").GetProperty(name);
        var options = Named(name);
        Assert.Equal(e.Number("duration"), options.Duration);
        Assert.Equal(e.Number("bounce"), options.Bounce);
        var dt = Vectors.Exact(e.Number("dt"));
        var every = (int)e.Number("sampleEvery");
        var expected = e.Numbers("values");
        var state = new SpringState(e.Number("from"), 0, e.Number("to"));
        var spring = new Spring(e.Number("from"), options) { Target = e.Number("to") };
        for (var frame = 1; frame <= expected.Length * every; frame++)
        {
            state = SpringSolver.Step(state, options, dt);
            spring.Step(dt);
            if (frame % every != 0) continue;
            Vectors.Near(expected[frame / every - 1], state.Value, $"{name} frame {frame}");
            Assert.Equal(state.Value, spring.Value);
        }
        Assert.True(spring.IsSettled);
    }

    [Fact]
    public void SpringCoefficientsFollowTheFormula()
    {
        var press = GlassSprings.Press;
        Assert.Equal(Math.Pow(2 * Math.PI / 0.24, 2), press.Stiffness, 9);
        Assert.Equal(4 * Math.PI * 0.8 / 0.24, press.Damping, 9);
        var negative = new SpringOptions(0.3, -0.5);
        Assert.Equal(4 * Math.PI / (0.3 * 0.5), negative.Damping, 9);
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringOptions(0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SpringOptions(0.3, 1));
    }

    [Fact]
    public void SpringsAreStableAcrossLargeFramesAndKeepVelocityWhenRetargeted()
    {
        var spring = new Spring(0, GlassSprings.Release) { Target = 1 };
        spring.Step(10);
        Assert.True(spring.IsSettled);
        Assert.Equal(1, spring.Value);

        var moving = new Spring(0, GlassSprings.Press) { Target = 1 };
        moving.Step(0.05);
        var velocity = moving.Velocity;
        Assert.True(velocity > 0);
        moving.Animate(0, GlassSprings.Release);
        Assert.Equal(velocity, moving.Velocity);
        moving.Step(1.0 / 60);
        Assert.True(moving.Value > 0);
    }

    [Fact]
    public void OverdampedAndCriticalSpringsConverge()
    {
        foreach (var options in new[] { new SpringOptions(0.4, -0.6), new SpringOptions(0.4, 0) })
        {
            var state = new SpringState(0, 0, 10);
            for (var i = 0; i < 600 && !state.IsSettled; i++) state = SpringSolver.Step(state, options, 1.0 / 120);
            Assert.True(state.IsSettled);
            Assert.Equal(10, state.Value);
        }
    }

    [Fact]
    public void RubberBandMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("rubberBand"))
            Vectors.Near(c.Number("value"), GlassInteractionModel.RubberBand(c.Number("offset"), c.Number("limit")), $"rubber band {c.Number("offset")}");
    }

    [Fact]
    public void StretchLimitMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("stretchLimit"))
            Vectors.Near(c.Number("value"), GlassInteractionModel.StretchLimit(c.Number("width"), c.Number("height")), $"stretch limit {c.Number("width")}");
    }

    [Fact]
    public void PressScaleMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("pressScale"))
        {
            var s = GlassInteractionModel.PressScale(c.Number("width"), c.Number("height"), c.Number("press"), c.Number("tx"), c.Number("ty"),
                c.Number("vx"), c.Number("vy"), c.Flag("reduced"));
            Vectors.Near(c.Number("sx"), s.Sx, "sx");
            Vectors.Near(c.Number("sy"), s.Sy, "sy");
        }
    }

    [Fact]
    public void StretchPreservesVolume()
    {
        var s = GlassInteractionModel.PressScale(120, 44, 0, 8, 0);
        // Stretching one axis narrows the other two (a drop of constant volume): sx · sy² = 1.
        Assert.True(s.Sx > 1 && s.Sy < 1);
        Assert.Equal(1, s.Sx * s.Sy * s.Sy, 9);
    }

    [Fact]
    public void GlowMatchesTheReference()
    {
        foreach (var c in Vectors.Cases("glow"))
        {
            var g = GlassInteractionModel.Glow(c.Number("width"), c.Number("height"), c.Flag("dark"));
            Vectors.Near(c.Number("radius"), g.Radius, "radius");
            Vectors.Near(c.Number("alpha"), g.Alpha, "alpha");
            Vectors.Near(c.Number("hover"), g.Hover, "hover");
        }
    }

    [Fact]
    public void ProgressiveBlurMatchesTheReference()
    {
        var p = Vectors.Root.GetProperty("progressive");
        Assert.Equal(GlassProgressive.Levels, p.Number("levels"));
        var blur = p.Numbers("blurForLevelsAt20");
        for (var level = 0; level <= GlassProgressive.Levels; level++)
            Vectors.Near(blur[level], GlassProgressive.LevelBlur(level, 20), $"level {level} blur");
        foreach (var c in p.GetProperty("weights").EnumerateArray())
        {
            var u = c.Number("u");
            var weights = c.Numbers("weights");
            for (var level = 0; level <= GlassProgressive.Levels; level++)
                Vectors.Near(weights[level], GlassProgressive.Weight(level, u), $"weight {level} at {u}");
        }
    }

    [Fact]
    public void PaintedLayerOpacitiesReproduceTheWeights()
    {
        for (var u = 0.0; u <= 1.0001; u += 0.01)
        {
            // Paint levels 1..6 in order over the sharp level 0; each level's effective weight is its opacity
            // times the transparency of every level painted after it.
            var effective = new double[GlassProgressive.Levels + 1];
            var remaining = 1.0;
            for (var level = GlassProgressive.Levels; level >= 1; level--)
            {
                var alpha = GlassProgressive.LayerOpacity(level, u);
                effective[level] = alpha * remaining;
                remaining *= 1 - alpha;
            }
            effective[0] = remaining;
            for (var level = 0; level <= GlassProgressive.Levels; level++)
                Vectors.Near(GlassProgressive.Weight(level, u), effective[level], $"level {level} at {u}", 1e-9);
        }
    }

    [Fact]
    public void SwitchSpringSettlesWithASmallOvershoot()
    {
        double value = 0, velocity = 0, peak = 0;
        var frames = 0;
        while ((value != 18 || velocity != 0) && frames++ < 600)
        {
            GlassControlMotion.SwitchSpringStep(ref value, ref velocity, 18, 1.0 / 120);
            peak = Math.Max(peak, value);
        }
        Assert.Equal(18, value);
        Assert.InRange(peak, 18.05, 18 * 1.06);
        Assert.InRange(frames / 120.0, 0.4, 1.2);
    }

    [Fact]
    public void PressCrossfadeEasesAndTapEnvelopeReturnsToRest()
    {
        Assert.Equal(0, GlassControlMotion.EaseInOut(0));
        Assert.Equal(1, GlassControlMotion.EaseInOut(1));
        Assert.Equal(0.5, GlassControlMotion.EaseInOut(0.5), 3);
        var previous = 0.0;
        for (var x = 0.0; x <= 1; x += 0.02)
        {
            var y = GlassControlMotion.EaseInOut(x);
            Assert.True(y >= previous - 1e-9);
            previous = y;
        }
        Assert.Equal(0, GlassControlMotion.TapEnvelope(0));
        Assert.Equal(0.353436, GlassControlMotion.TapEnvelope(0.154), 6);
        Assert.Equal(0, GlassControlMotion.TapEnvelope(GlassControlMotion.TapEnvelopeDuration + 0.1));
        Assert.Equal(GlassScale.Identity.Sx, GlassControlMotion.ThumbStretch(0, 1).Sx);
        Assert.True(GlassControlMotion.ThumbStretch(300, 1).Sx > 1);
    }
}
