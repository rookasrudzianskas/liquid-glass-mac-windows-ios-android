using System;
using System.Collections.Generic;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>Something that advances once per rendered frame while it is in motion.</summary>
internal interface IGlassAnimation
{
    /// <summary>Advances by <paramref name="dt"/> seconds; returns false once it has come to rest.</summary>
    bool Tick(double dt);
}

/// <summary>
/// Drives glass animations from <see cref="CompositionTarget.Rendering"/>, subscribed only while something moves so
/// an idle window renders no frames. One loop per UI thread.
/// </summary>
internal static class GlassFrameLoop
{
    [ThreadStatic] private static State? _state;

    /// <summary>Raised after every animation has advanced in a frame, before layout and rendering.</summary>
    [ThreadStatic] private static Action? _afterTick;

    /// <summary>Starts animating <paramref name="animation"/> (no effect if it already is).</summary>
    public static void Start(IGlassAnimation animation)
    {
        var state = _state ??= new State();
        if (state.Active.Contains(animation)) return;
        state.Active.Add(animation);
        if (state.Subscribed) return;
        state.Subscribed = true;
        state.Last = null;
        CompositionTarget.Rendering += OnRendering;
    }

    /// <summary>Stops animating <paramref name="animation"/> immediately.</summary>
    public static void Stop(IGlassAnimation animation) => _state?.Active.Remove(animation);

    /// <summary>True while <paramref name="animation"/> is being driven.</summary>
    public static bool IsRunning(IGlassAnimation animation) => _state?.Active.Contains(animation) == true;

    /// <summary>Runs <paramref name="callback"/> after the animations of every frame on this thread.</summary>
    public static void AfterTick(Action callback) => _afterTick += callback;

    /// <summary>Removes a callback added with <see cref="AfterTick"/>.</summary>
    public static void RemoveAfterTick(Action callback) => _afterTick -= callback;

    private static void OnRendering(object? sender, EventArgs e)
    {
        var state = _state;
        if (state is null) return;
        // Several Rendering events can arrive for one frame; only a new frame time advances the springs.
        var time = e is RenderingEventArgs rendering ? rendering.RenderingTime : TimeSpan.FromTicks(DateTime.UtcNow.Ticks);
        if (state.Last == time) return;
        var dt = state.Last is { } last ? (time - last).TotalSeconds : 1.0 / 60;
        state.Last = time;
        if (dt <= 0) return;
        state.Ticking.Clear();
        state.Ticking.AddRange(state.Active);
        foreach (var animation in state.Ticking)
        {
            if (!state.Active.Contains(animation)) continue;
            if (!animation.Tick(dt)) state.Active.Remove(animation);
        }
        // Holding the frame's animations past it would keep a closed window alive until the next frame on this thread.
        state.Ticking.Clear();
        _afterTick?.Invoke();
        if (state.Active.Count > 0) return;
        CompositionTarget.Rendering -= OnRendering;
        state.Subscribed = false;
    }

    private sealed class State
    {
        public HashSet<IGlassAnimation> Active { get; } = new();

        public List<IGlassAnimation> Ticking { get; } = new();

        public bool Subscribed { get; set; }

        public TimeSpan? Last { get; set; }
    }
}
