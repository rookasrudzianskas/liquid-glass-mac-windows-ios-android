using System.Diagnostics;
using System.Windows;
using LiquidGlass.Wpf;

namespace HelloGlass;

/// <summary>One glass screen over the aurora wallpaper: a toolbar, a card with two buttons and a switch, a tab bar.</summary>
public partial class MainWindow : Window
{
    private const string Site = "https://liquid-glass-engineering.vercel.app";
    private const string Guide = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/docs/windows.md";

    public MainWindow() => InitializeComponent();

    private void OnSwitchAppearance(object sender, RoutedEventArgs e)
        => Card.Appearance = GlassScene.GetIsDark(Card) ? GlassAppearance.Light : GlassAppearance.Dark;

    private void OnProfile(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 2;

    private void OnGetStarted(object sender, RoutedEventArgs e) => Open(Guide);

    private void OnLearnMore(object sender, RoutedEventArgs e) => Open(Site);

    private static void Open(string url) => Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })?.Dispose();
}
