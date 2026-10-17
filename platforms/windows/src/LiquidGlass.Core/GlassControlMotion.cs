using System;

namespace LiquidGlass;

/// <summary>
/// Motion of the native-style value controls (slider and switch): the measured switch spring, the press
/// crossfade that lifts a thumb into glass, and the thumb geometry.
/// </summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#6-interaction">Liquid Glass optics specification: Interaction</seealso>
public static class GlassControlMotion
{
    /// <summary>Slider thumb width at rest.</summary>
    public const double SliderThumbWidth = 20;

    /// <summary>Slider thumb height at rest.</summary>
    public const double SliderThumbHeight = 16;

    /// <summary>Horizontal growth of the pressed slider thumb, per side.</summary>
    public const double SliderThumbGrowX = 2.5;

    /// <summary>Vertical growth of the pressed slider thumb, per side.</summary>
    public const double SliderThumbGrowY = 2;

    /// <summary>Refraction of the lifted slider thumb.</summary>
    public const double SliderThumbRefraction = 5;

    /// <summary>Switch track width.</summary>
    public const double SwitchTrackWidth = 54;

    /// <summary>Switch track height.</summary>
    public const double SwitchTrackHeight = 24;

    /// <summary>Switch thumb width at rest.</summary>
    public const double SwitchThumbWidth = 32;

    /// <summary>Switch thumb height at rest.</summary>
    public const double SwitchThumbHeight = 20;

    /// <summary>Horizontal growth of the pressed switch thumb, per side.</summary>
    public const double SwitchThumbGrowX = 9;

    /// <summary>Vertical growth of the pressed switch thumb, per side.</summary>
    public const double SwitchThumbGrowY = 5.5;

    /// <summary>Refraction of the lifted switch thumb.</summary>
    public const double SwitchThumbRefraction = 7.75;

    /// <summary>Duration of the press crossfade in seconds.</summary>
    public const double PressCrossfadeDuration = 0.2;

    /// <summary>A press shorter than this (seconds) plays the tap envelope instead of a crossfade.</summary>
    public const double TapThreshold = 0.08;

    /// <summary>Response of the measured switch spring, in seconds.</summary>
    public const double SwitchResponse = 0.47;

    /// <summary>Damping ratio of the measured switch spring.</summary>
    public const double SwitchDamping = 0.7;

    private static readonly double[,] TapEnvelopeSamples =
    {
        { 0, 0 }, { 0.06, 0 }, { 0.087, 0.120633 }, { 0.103, 0.204144 }, { 0.120, 0.280965 }, { 0.137, 0.334618 },
        { 0.154, 0.353436 }, { 0.170, 0.333683 }, { 0.187, 0.281121 }, { 0.204, 0.209108 }, { 0.220, 0.133635 },
        { 0.237, 0.068474 }, { 0.254, 0.022840 }, { 0.270, 0.001445 }, { 0.287, 0 },
    };

    /// <summary>
    /// Advances the measured switch spring (response 0.47 s, damping 0.7) in closed form.
    /// <paramref name="value"/> and <paramref name="velocity"/> are updated in place.
    /// </summary>
    public static void SwitchSpringStep(ref double value, ref double velocity, double target, double dt)
    {
        if (!(dt > 0)) return;
        var frequency = 2 * Math.PI / SwitchResponse;
        var decayRate = SwitchDamping * frequency;
        var oscillation = frequency * Math.Sqrt(1 - SwitchDamping * SwitchDamping);
        var offset = value - target;
        var amplitude = (velocity + decayRate * offset) / oscillation;
        var decay = Math.Exp(-decayRate * dt);
        var sine = Math.Sin(oscillation * dt);
        var cosine = Math.Cos(oscillation * dt);
        value = target + decay * (offset * cosine + amplitude * sine);
        velocity = decay * ((amplitude * oscillation - decayRate * offset) * cosine - (decayRate * amplitude + oscillation * offset) * sine);
        if (Math.Abs(value - target) < 0.001 && Math.Abs(velocity) < 0.01)
        {
            value = target;
            velocity = 0;
        }
    }

    /// <summary>The native 200 ms interaction curve, <c>cubic-bezier(0.42, 0, 0.58, 1)</c>, at progress <paramref name="x"/>.</summary>
    public static double EaseInOut(double x)
    {
        if (x <= 0) return 0;
        if (x >= 1) return 1;
        double lo = 0, hi = 1, t = x;
        for (var i = 0; i < 12; i++)
        {
            var bx = 3 * (1 - t) * (1 - t) * t * 0.42 + 3 * (1 - t) * t * t * 0.58 + t * t * t;
            if (bx < x) lo = t; else hi = t;
            t = (lo + hi) / 2;
        }
        return 3 * (1 - t) * t * t + t * t * t;
    }

    /// <summary>The measured material envelope of a short tap, <paramref name="seconds"/> after the pointer went down.</summary>
    public static double TapEnvelope(double seconds)
    {
        var count = TapEnvelopeSamples.GetLength(0);
        for (var i = 1; i < count; i++)
        {
            var t = TapEnvelopeSamples[i, 0];
            if (seconds <= t)
            {
                var before = TapEnvelopeSamples[i - 1, 0];
                var value = TapEnvelopeSamples[i - 1, 1];
                var p = TapEnvelopeSamples[i, 1];
                return value + (p - value) * Math.Max(0, (seconds - before) / (t - before));
            }
        }
        return 0;
    }

    /// <summary>Total length of the tap envelope in seconds.</summary>
    public static double TapEnvelopeDuration => TapEnvelopeSamples[TapEnvelopeSamples.GetLength(0) - 1, 0];

    /// <summary>
    /// Stretch of a lifted thumb moving at <paramref name="speed"/> layout units per second, scaled by the press
    /// <paramref name="progress"/>: the lens lengthens along its path and narrows across it.
    /// </summary>
    public static GlassScale ThumbStretch(double speed, double progress)
    {
        var stretch = Math.Min(0.28, Math.Abs(speed) * 0.0007) * progress;
        return stretch > 1e-4 ? new GlassScale(1 + stretch, 1 / Math.Sqrt(1 + stretch)) : GlassScale.Identity;
    }
}
