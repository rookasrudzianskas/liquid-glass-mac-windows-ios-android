using System;

namespace LiquidGlass;

/// <summary>A damped spring described by its perceptual duration and bounce (OPTICS §6).</summary>
public readonly struct SpringOptions : IEquatable<SpringOptions>
{
    /// <summary>Creates spring options.</summary>
    /// <param name="duration">Perceptual duration in seconds; positive and finite.</param>
    /// <param name="bounce">Bounce in (−1, 1): 0 is critically damped, positive overshoots.</param>
    public SpringOptions(double duration, double bounce)
    {
        if (!(duration > 0) || double.IsInfinity(duration))
            throw new ArgumentOutOfRangeException(nameof(duration), duration, "Spring duration must be positive and finite.");
        if (!(bounce > -1 && bounce < 1))
            throw new ArgumentOutOfRangeException(nameof(bounce), bounce, "Spring bounce must be greater than -1 and less than 1.");
        Duration = duration;
        Bounce = bounce;
    }

    /// <summary>Perceptual duration in seconds.</summary>
    public double Duration { get; }

    /// <summary>Bounce in (−1, 1).</summary>
    public double Bounce { get; }

    /// <summary><c>(2π / duration)²</c>.</summary>
    public double Stiffness
    {
        get
        {
            var omega = 2 * Math.PI / Duration;
            return omega * omega;
        }
    }

    /// <summary><c>4π(1 − bounce)/duration</c> for bounce ≥ 0, else <c>4π/(duration(1 + bounce))</c>.</summary>
    public double Damping => Bounce >= 0 ? 4 * Math.PI * (1 - Bounce) / Duration : 4 * Math.PI / (Duration * (1 + Bounce));

    /// <inheritdoc />
    public bool Equals(SpringOptions other) => Duration.Equals(other.Duration) && Bounce.Equals(other.Bounce);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SpringOptions other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => unchecked(Duration.GetHashCode() * 397 ^ Bounce.GetHashCode());

    /// <summary>Equality.</summary>
    public static bool operator ==(SpringOptions left, SpringOptions right) => left.Equals(right);

    /// <summary>Inequality.</summary>
    public static bool operator !=(SpringOptions left, SpringOptions right) => !left.Equals(right);
}

/// <summary>The named springs shared by every glass interaction, so motion reads as one material.</summary>
public static class GlassSprings
{
    /// <summary>Finger down: the material catches it.</summary>
    public static SpringOptions Press { get; } = new(0.24, 0.2);

    /// <summary>Finger up: gel-like overshoot back to rest.</summary>
    public static SpringOptions Release { get; } = new(0.5, 0.42);

    /// <summary>Light settling in and out.</summary>
    public static SpringOptions Glow { get; } = new(0.42, 0);

    /// <summary>Glass growing out of another shape (menus).</summary>
    public static SpringOptions Morph { get; } = new(0.42, 0.18);

    /// <summary>A surface flowing to a new size.</summary>
    public static SpringOptions Layout { get; } = new(0.4, 0.16);

    /// <summary>Selection lenses and drag follow.</summary>
    public static SpringOptions Track { get; } = new(0.18, 0);

    /// <summary>The track spring for a surface being dragged: larger glass is heavier and follows more slowly.</summary>
    public static SpringOptions TrackWhileDragging(double width, double height)
        => new(Track.Duration + 0.0005 * Math.Max(width, height), 0);

    /// <summary>A critically damped crossfade with the same duration, used when motion is reduced.</summary>
    public static SpringOptions Reduced(SpringOptions spring) => new(spring.Duration, 0);
}

/// <summary>Position, velocity and target of a spring.</summary>
public readonly struct SpringState
{
    /// <summary>Creates a state.</summary>
    public SpringState(double value, double velocity, double target)
    {
        Value = value;
        Velocity = velocity;
        Target = target;
    }

    /// <summary>Current value.</summary>
    public double Value { get; }

    /// <summary>Current velocity in units per second.</summary>
    public double Velocity { get; }

    /// <summary>Value the spring settles at.</summary>
    public double Target { get; }

    /// <summary>At its target and at rest.</summary>
    public bool IsSettled => Value.Equals(Target) && Velocity.Equals(0);
}

/// <summary>Closed-form damped harmonic oscillator: stable for any frame time.</summary>
public static class SpringSolver
{
    /// <summary>Advances a spring by <paramref name="dt"/> seconds.</summary>
    public static SpringState Step(SpringState state, SpringOptions options, double dt)
    {
        var value = state.Value;
        var velocity = state.Velocity;
        var target = state.Target;
        if ((value.Equals(target) && velocity.Equals(0)) || !(dt > 0)) return state;
        var omega = Math.Sqrt(options.Stiffness);
        var zeta = options.Damping / (2 * omega);
        var x0 = value - target;
        var v0 = velocity;
        double x, v;
        if (zeta < 1)
        {
            var wd = omega * Math.Sqrt(1 - zeta * zeta);
            var decay = Math.Exp(-zeta * omega * dt);
            var a = x0;
            var b = (v0 + zeta * omega * x0) / wd;
            var c = Math.Cos(wd * dt);
            var s = Math.Sin(wd * dt);
            x = decay * (a * c + b * s);
            v = decay * ((b * wd - zeta * omega * a) * c - (a * wd + zeta * omega * b) * s);
        }
        else if (zeta.Equals(1))
        {
            var decay = Math.Exp(-omega * dt);
            var b = v0 + omega * x0;
            x = decay * (x0 + b * dt);
            v = decay * (b - omega * (x0 + b * dt));
        }
        else
        {
            var root = omega * Math.Sqrt(zeta * zeta - 1);
            var r1 = -zeta * omega + root;
            var r2 = -zeta * omega - root;
            var c2 = (v0 - r1 * x0) / (r2 - r1);
            var c1 = x0 - c2;
            var e1 = Math.Exp(r1 * dt);
            var e2 = Math.Exp(r2 * dt);
            x = c1 * e1 + c2 * e2;
            v = c1 * r1 * e1 + c2 * r2 * e2;
        }
        var scale = Math.Max(1, Math.Max(Math.Abs(target), Math.Abs(x0)));
        if (Math.Abs(x) < 1e-4 * scale && Math.Abs(v) < 1e-3 * scale) return new SpringState(target, 0, target);
        return new SpringState(target + x, v, target);
    }
}

/// <summary>
/// One mutable spring. Retargeting keeps the current velocity, so every animation built on it can be
/// interrupted and reversed mid-flight.
/// </summary>
public sealed class Spring
{
    /// <summary>Creates a spring at rest at <paramref name="value"/>.</summary>
    public Spring(double value, SpringOptions options)
    {
        Value = value;
        Target = value;
        Options = options;
    }

    /// <summary>Current value.</summary>
    public double Value { get; private set; }

    /// <summary>Current velocity in units per second.</summary>
    public double Velocity { get; private set; }

    /// <summary>Value the spring moves toward.</summary>
    public double Target { get; set; }

    /// <summary>The spring's duration and bounce.</summary>
    public SpringOptions Options { get; set; }

    /// <summary>At its target and at rest.</summary>
    public bool IsSettled => Value.Equals(Target) && Velocity.Equals(0);

    /// <summary>Changes the spring options and returns this spring.</summary>
    public Spring Configure(SpringOptions options)
    {
        Options = options;
        return this;
    }

    /// <summary>Moves toward <paramref name="target"/> with <paramref name="options"/>, keeping the current velocity.</summary>
    public void Animate(double target, SpringOptions options)
    {
        Options = options;
        Target = target;
    }

    /// <summary>Moves to <paramref name="value"/> immediately and stops.</summary>
    public void Jump(double value)
    {
        Value = value;
        Target = value;
        Velocity = 0;
    }

    /// <summary>Advances by <paramref name="dt"/> seconds with the closed-form solution.</summary>
    public void Step(double dt)
    {
        var next = SpringSolver.Step(new SpringState(Value, Velocity, Target), Options, dt);
        Value = next.Value;
        Velocity = next.Velocity;
    }
}
