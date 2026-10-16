using System.Windows;
using LiquidGlass.Studio.Shell;
using LiquidGlass.Wpf;

namespace LiquidGlass.Studio.Pages;

/// <summary>Controls: buttons of every size and tint, icon buttons, sliders, switches, a tab bar and menus.</summary>
public partial class ControlsPage : StudioPageBase
{
    public ControlsPage()
    {
        InitializeComponent();
    }

    /// <inheritdoc />
    public override void SetDark(bool dark) => Wallpaper.Source = Wallpapers.Get(dark ? Wallpapers.Aurora : Wallpapers.Ocean);

    private void OnSort(object sender, RoutedEventArgs e)
    {
        var chosen = (GlassMenuItem)sender;
        foreach (var item in new[] { SortName, SortDate, SortSize }) item.IsChecked = ReferenceEquals(item, chosen);
        SortMenu.Header = "Sort by " + chosen.Header?.ToString()?.ToLowerInvariant();
    }
}
