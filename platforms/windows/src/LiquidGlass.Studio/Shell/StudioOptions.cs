using System;
using System.Globalization;
using LiquidGlass.Wpf;

namespace LiquidGlass.Studio.Shell;

/// <summary>Command-line options: deep links into a page and appearance, used for screenshots and smoke tests.</summary>
/// <remarks>
/// <c>LiquidGlassStudio.exe [--page hero|controls|materials|blur] [--appearance system|light|dark] [--size 1280x820]
/// [--render-mode automatic|fallback] [--exit-after seconds]</c>
/// </remarks>
internal sealed class StudioOptions
{
    public StudioPage Page { get; private set; } = StudioPage.Hero;

    public GlassAppearance Appearance { get; private set; } = GlassAppearance.Automatic;

    public GlassRenderMode RenderMode { get; private set; } = GlassRenderMode.Automatic;

    public double? Width { get; private set; }

    public double? Height { get; private set; }

    /// <summary>Close the window after this many seconds (smoke tests).</summary>
    public double? ExitAfter { get; private set; }

    public static StudioOptions Parse(string[] args)
    {
        var options = new StudioOptions();
        for (var i = 0; i < args.Length; i++)
        {
            var value = i + 1 < args.Length ? args[i + 1] : string.Empty;
            switch (args[i].ToLowerInvariant())
            {
                case "--page":
                    options.Page = value.ToLowerInvariant() switch
                    {
                        "controls" => StudioPage.Controls,
                        "materials" => StudioPage.Materials,
                        "blur" => StudioPage.ProgressiveBlur,
                        _ => StudioPage.Hero,
                    };
                    i++;
                    break;
                case "--appearance":
                    options.Appearance = value.ToLowerInvariant() switch
                    {
                        "light" => GlassAppearance.Light,
                        "dark" => GlassAppearance.Dark,
                        _ => GlassAppearance.Automatic,
                    };
                    i++;
                    break;
                case "--render-mode":
                    options.RenderMode = string.Equals(value, "fallback", StringComparison.OrdinalIgnoreCase) ? GlassRenderMode.Fallback : GlassRenderMode.Automatic;
                    i++;
                    break;
                case "--size":
                    var parts = value.Split('x');
                    if (parts.Length == 2 && double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var w)
                        && double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var h))
                    {
                        options.Width = w;
                        options.Height = h;
                    }
                    i++;
                    break;
                case "--exit-after":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds)) options.ExitAfter = seconds;
                    i++;
                    break;
            }
        }
        return options;
    }
}

/// <summary>The demo's pages, as the component contract's demo app lists them.</summary>
internal enum StudioPage
{
    Hero,
    Controls,
    Materials,
    ProgressiveBlur,
}
