using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media.Imaging;

namespace LiquidGlass.Studio.Shell;

/// <summary>The shared wallpapers (assets/wallpapers), embedded in the app.</summary>
internal static class Wallpapers
{
    public const string Aurora = "aurora";
    public const string Dunes = "dunes";
    public const string Ocean = "ocean";
    public const string Midnight = "midnight";

    private static readonly Dictionary<string, BitmapImage> Cache = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> All { get; } = new[] { Dunes, Ocean, Aurora, Midnight };

    /// <summary>
    /// How a page aligns its wallpaper, which is taller than the page in a landscape window: in dark appearance its
    /// deep lower part lies under the dark glass, so the white labels on it stay legible (every wallpaper darkens
    /// toward the bottom); in light appearance its bright top lies under the light glass and its dark labels.
    /// </summary>
    public static VerticalAlignment Alignment(bool dark) => dark ? VerticalAlignment.Bottom : VerticalAlignment.Top;

    /// <summary>The 2048-pixel wallpaper, decoded once.</summary>
    public static BitmapImage Get(string name)
    {
        if (Cache.TryGetValue(name, out var image)) return image;
        image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/Wallpapers/{name}-2048.jpg", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.EndInit();
        image.Freeze();
        Cache[name] = image;
        return image;
    }
}
