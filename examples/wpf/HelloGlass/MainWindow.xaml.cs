using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using LiquidGlass.Wpf;

namespace HelloGlass;

/// <summary>One glass screen over the aurora wallpaper: a toolbar, a card with two buttons and a switch, a tab bar.</summary>
public partial class MainWindow : Window
{
    private const string Guide = "https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/docs/windows.md";

    // DWMWA_USE_IMMERSIVE_DARK_MODE: a dark title bar and frame (Windows 10 version 2004 and later).
    private const int UseImmersiveDarkMode = 20;

    public MainWindow()
    {
        InitializeComponent();
        // The title bar follows the card: the Windows app mode until the toolbar switches it.
        DependencyPropertyDescriptor.FromProperty(GlassScene.IsDarkProperty, typeof(GlassSurface))
            .AddValueChanged(Card, (_, _) => UpdateTitleBar());
        SourceInitialized += (_, _) => UpdateTitleBar();
    }

    private void OnSwitchAppearance(object sender, RoutedEventArgs e)
        => Card.Appearance = GlassScene.GetIsDark(Card) ? GlassAppearance.Light : GlassAppearance.Dark;

    private void OnProfile(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 2;

    private void OnGetStarted(object sender, RoutedEventArgs e) => Tabs.SelectedIndex = 1;

    private void OnLearnMore(object sender, RoutedEventArgs e)
        => Process.Start(new ProcessStartInfo(Guide) { UseShellExecute = true })?.Dispose();

    private void UpdateTitleBar()
    {
        var hwnd = new WindowInteropHelper(this).Handle;
        if (hwnd == IntPtr.Zero) return;
        var dark = GlassScene.GetIsDark(Card) ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref dark, sizeof(int));
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);
}
