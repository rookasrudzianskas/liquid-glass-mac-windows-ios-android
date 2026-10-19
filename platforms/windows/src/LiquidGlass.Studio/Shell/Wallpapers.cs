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
    private static readonly Dictionary<string, BitmapImage> Thumbnails = new(StringComparer.Ordinal);

    public static IReadOnlyList<string> All { get; } = new[] { Dunes, Ocean, Aurora, Midnight };

    /// <summary>
    /// How a page aligns its wallpaper, which is taller than the page in a landscape window: in dark appearance its
    /// deep lower part lies under the dark glass, so the white labels on it stay legible (every wallpaper darkens
    /// toward the bottom); in light appearance its bright top lies under the light glass and its dark labels.
    /// </summary>
    public static VerticalAlignment Alignment(bool dark) => dark ? VerticalAlignment.Bottom : VerticalAlignment.Top;

    /// <summary>The 2048-pixel wallpaper, decoded once.</summary>
    public static BitmapImage Get(string name) => Load(name, Cache, 0);

    /// <summary>
    /// The wallpaper decoded 256 pixels wide, for pickers: the decoder averages it down (an eighth of the JPEG), so its
    /// thin rim lines stay continuous where the full image shrunk about 28 times on screen breaks them into specks.
    /// </summary>
    public static BitmapImage Thumbnail(string name) => Load(name, Thumbnails, 256);

    private static BitmapImage Load(string name, Dictionary<string, BitmapImage> cache, int width)
    {
        if (cache.TryGetValue(name, out var image)) return image;
        image = new BitmapImage();
        image.BeginInit();
        image.UriSource = new Uri($"pack://application:,,,/Wallpapers/{name}-2048.jpg", UriKind.Absolute);
        image.CacheOption = BitmapCacheOption.OnLoad;
        if (width > 0) image.DecodePixelWidth = width;
        image.EndInit();
        image.Freeze();
        cache[name] = image;
        return image;
    }
}
