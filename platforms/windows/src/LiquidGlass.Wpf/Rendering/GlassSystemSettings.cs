using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using Microsoft.Win32;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// The Windows settings glass follows: app theme (light or dark), transparency effects, contrast themes, animation
/// effects and the render tier. Change notifications come from <see cref="SystemParameters"/>, from
/// <c>WM_SETTINGCHANGE</c> (hooked on every window that shows glass) and from <see cref="RenderCapability.TierChanged"/>
/// (on every UI thread that shows glass).
/// </summary>
internal static class GlassSystemSettings
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AccentKey = @"Software\Microsoft\Windows\CurrentVersion\Explorer\Accent";

    /// <summary>The Windows default accent (#0078D4), used when the setting cannot be read.</summary>
    public static readonly Color DefaultAccent = Color.FromRgb(0x00, 0x78, 0xD4);
    private const int WmSettingChange = 0x001A;
    private const int WmThemeChanged = 0x031A;
    private const int WmSysColorChange = 0x0015;
    private const int WmDwmColorizationColorChanged = 0x0320;

    private static readonly ConditionalWeakTable<HwndSource, object> Hooked = new();
    private static readonly object Gate = new();
    private static Snapshot _current = Read();

    // WPF raises RenderCapability.TierChanged per UI thread (from that thread's media context), so each thread that
    // shows glass subscribes once; SystemParameters.StaticPropertyChanged is process-wide.
    [ThreadStatic] private static bool _tierHooked;

    static GlassSystemSettings()
    {
        SystemParameters.StaticPropertyChanged += OnSystemParameterChanged;
    }

    /// <summary>Raised on the thread that observed a change; handlers marshal to their own dispatcher.</summary>
    public static event EventHandler? Changed;

    /// <summary>Windows apps are set to dark mode.</summary>
    public static bool AppsUseDarkTheme => _current.Dark;

    /// <summary>The Windows accent color (Settings › Personalization › Colors).</summary>
    public static Color AccentColor => _current.Accent;

    /// <summary>Transparency effects are on.</summary>
    public static bool TransparencyEnabled => _current.Transparency;

    /// <summary>A contrast (high contrast) theme is active.</summary>
    public static bool HighContrast => SystemParameters.HighContrast;

    /// <summary>Animation effects are on (Settings › Accessibility › Visual effects).</summary>
    public static bool AnimationsEnabled => AnimationsOverride ?? SystemParameters.ClientAreaAnimation;

    /// <summary>Replaces the animation setting, for tests on machines where animations are off.</summary>
    internal static bool? AnimationsOverride { get; set; }

    /// <summary>Raises <see cref="Changed"/>: something glass depends on changed (for example, the driver rejected the shader).</summary>
    internal static void NotifyChanged() => Changed?.Invoke(null, EventArgs.Empty);

    /// <summary>
    /// Subscribes to <see cref="RenderCapability.TierChanged"/> on the calling UI thread, once: a Remote Desktop
    /// connection or a driver reset moves that thread's windows between hardware and software rendering, and its glass
    /// between the shader and the fallback material.
    /// </summary>
    internal static void EnsureTierHandler()
    {
        if (_tierHooked) return;
        _tierHooked = true;
        RenderCapability.TierChanged += (_, _) => NotifyChanged();
    }

    /// <summary>Whether the calling thread is subscribed to <see cref="RenderCapability.TierChanged"/> (tests).</summary>
    internal static bool IsTierHandlerAttached => _tierHooked;

    /// <summary>Watches the window that hosts <paramref name="visual"/> for setting changes.</summary>
    public static void Watch(Visual visual)
    {
        if (PresentationSource.FromVisual(visual) is not HwndSource source) return;
        lock (Gate)
        {
            if (Hooked.TryGetValue(source, out _)) return;
            Hooked.Add(source, Gate);
        }
        source.AddHook(Hook);
    }

    /// <summary>Reads the settings again and notifies if any changed.</summary>
    public static void Refresh()
    {
        var next = Read();
        Snapshot previous;
        lock (Gate)
        {
            previous = _current;
            _current = next;
        }
        if (!previous.Equals(next)) Changed?.Invoke(null, EventArgs.Empty);
    }

    private static IntPtr Hook(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (message is WmSettingChange or WmThemeChanged or WmSysColorChange or WmDwmColorizationColorChanged) Refresh();
        return IntPtr.Zero;
    }

    private static void OnSystemParameterChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(SystemParameters.HighContrast) or nameof(SystemParameters.ClientAreaAnimation))
            Changed?.Invoke(null, EventArgs.Empty);
    }

    private static Snapshot Read()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            var light = key?.GetValue("AppsUseLightTheme") is int appsLight ? appsLight != 0 : true;
            var transparency = key?.GetValue("EnableTransparency") is int enabled ? enabled != 0 : true;
            using var accentKey = Registry.CurrentUser.OpenSubKey(AccentKey);
            // AccentColorMenu is stored as 0xAABBGGRR.
            var accent = accentKey?.GetValue("AccentColorMenu") is int abgr
                ? Color.FromRgb((byte)(abgr & 0xFF), (byte)((abgr >> 8) & 0xFF), (byte)((abgr >> 16) & 0xFF))
                : DefaultAccent;
            return new Snapshot(!light, transparency, accent);
        }
        catch (Exception e) when (e is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return new Snapshot(false, true, DefaultAccent);
        }
    }

    private readonly struct Snapshot : IEquatable<Snapshot>
    {
        public Snapshot(bool dark, bool transparency, Color accent)
        {
            Dark = dark;
            Transparency = transparency;
            Accent = accent;
        }

        public bool Dark { get; }

        public bool Transparency { get; }

        public Color Accent { get; }

        public bool Equals(Snapshot other) => Dark == other.Dark && Transparency == other.Transparency && Accent == other.Accent;

        public override bool Equals(object? obj) => obj is Snapshot other && Equals(other);

        public override int GetHashCode() => (Dark ? 1 : 0) | (Transparency ? 2 : 0) | Accent.GetHashCode() << 2;
    }
}
