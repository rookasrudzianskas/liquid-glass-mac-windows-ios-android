using System;
using System.Windows;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>Resolves the inherited glass options of an element against the Windows settings.</summary>
internal static class GlassResolve
{
    /// <summary>Whether <paramref name="element"/> draws dark glass.</summary>
    public static bool IsDark(DependencyObject element) => GlassScene.GetAppearance(element) switch
    {
        GlassAppearance.Dark => true,
        GlassAppearance.Light => false,
        _ => GlassSystemSettings.AppsUseDarkTheme,
    };

    /// <summary>The motion level after the system's animation setting is applied.</summary>
    public static GlassMotion Motion(DependencyObject element)
    {
        var motion = GlassScene.GetMotion(element);
        if (motion == GlassMotion.Full && !GlassSystemSettings.AnimationsEnabled) return GlassMotion.Reduced;
        return motion;
    }

    /// <summary>Whether the material is replaced by an opaque fill.</summary>
    public static bool ReduceTransparency(DependencyObject element)
        => GlassScene.GetReduceTransparency(element) ?? !GlassSystemSettings.TransparencyEnabled;

    /// <summary>The material after increased contrast (which uses Regular) is applied.</summary>
    public static GlassMaterial Material(DependencyObject element)
        => Material(element, GlassScene.GetMaterial(element));

    /// <summary><paramref name="material"/>, or Regular when <paramref name="element"/> increases contrast.</summary>
    public static GlassMaterial Material(DependencyObject element, GlassMaterial material)
        => GlassScene.GetIncreaseContrast(element) ? GlassMaterial.Regular : material;

    /// <summary>A spring for the motion level: reduced motion removes overshoot.</summary>
    public static SpringOptions Spring(SpringOptions spring, GlassMotion motion)
        => motion == GlassMotion.Full ? spring : GlassSprings.Reduced(spring);

    /// <summary>Device pixels per layout unit for <paramref name="visual"/>.</summary>
    public static double Scale(Visual visual)
    {
        try
        {
            return Math.Max(1e-3, VisualTreeHelper.GetDpi(visual).DpiScaleX);
        }
        catch (InvalidOperationException)
        {
            return 1;
        }
    }

    /// <summary>Rounds a length up to whole device pixels.</summary>
    public static double CeilToPixels(double length, double scale) => Math.Ceiling(length * scale - 1e-6) / scale;
}

/// <summary>
/// Keeps <see cref="GlassScene.IsDarkProperty"/> and <see cref="GlassScene.ContentForegroundProperty"/> current on a
/// glass control as its appearance, tint or the Windows settings change.
/// </summary>
internal sealed class GlassAppearanceTracker
{
    private readonly FrameworkElement _owner;
    private readonly Func<Color?> _tint;
    private readonly Action? _changed;
    private readonly Func<GlassMaterial>? _material;
    private readonly double _legibility;

    /// <param name="owner">The glass control.</param>
    /// <param name="tint">Its tint.</param>
    /// <param name="changed">Called after every update.</param>
    /// <param name="material">The material its content sits on, when that is not the control's own (a menu's panel).</param>
    /// <param name="legibility">How far that glass mixes toward the legibility backing: the menu amount for menu panels.</param>
    public GlassAppearanceTracker(FrameworkElement owner, Func<Color?> tint, Action? changed = null, Func<GlassMaterial>? material = null, double legibility = 0)
    {
        _owner = owner;
        _tint = tint;
        _changed = changed;
        _material = material;
        _legibility = legibility;
        owner.Loaded += (_, _) =>
        {
            GlassSystemSettings.Changed -= OnSettingsChanged;
            GlassSystemSettings.Changed += OnSettingsChanged;
            GlassSystemSettings.Watch(owner);
            Update();
        };
        owner.Unloaded += (_, _) => GlassSystemSettings.Changed -= OnSettingsChanged;
        Update();
    }

    /// <summary>Recomputes after a property that affects appearance changed.</summary>
    public void Update()
    {
        var dark = GlassResolve.IsDark(_owner);
        if (GlassScene.GetIsDark(_owner) != dark) GlassScene.SetIsDark(_owner, dark);
        var foreground = GlassBrushes.Foreground(dark, _tint(), _material?.Invoke() ?? GlassResolve.Material(_owner), _legibility);
        if (!ReferenceEquals(GlassScene.GetContentForeground(_owner), foreground)) GlassScene.SetContentForeground(_owner, foreground);
        _changed?.Invoke();
    }

    /// <summary>True for the properties whose change requires <see cref="Update"/>.</summary>
    public static bool Affects(DependencyProperty property)
        => property == GlassScene.AppearanceProperty || property == GlassScene.ReduceTransparencyProperty
           || property == GlassScene.IncreaseContrastProperty || property == GlassScene.MaterialProperty
           || property == GlassScene.MotionProperty || property == GlassScene.RenderModeProperty;

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (_owner.Dispatcher.CheckAccess()) Update();
        else _owner.Dispatcher.BeginInvoke(new Action(Update));
    }
}
