using System;
using System.Collections.Generic;
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
