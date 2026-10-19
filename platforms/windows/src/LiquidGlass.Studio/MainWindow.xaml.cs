using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using System.Windows.Threading;
using LiquidGlass.Studio.Pages;
using LiquidGlass.Studio.Shell;
using LiquidGlass.Wpf;

namespace LiquidGlass.Studio;

/// <summary>The Studio window: a Windows 11 navigation rail and the four demo pages.</summary>
public partial class MainWindow : Window
{
    private const double ContentCorner = 8;
    private const int WmSysColorChange = 0x0015;

    private readonly StudioOptions _options;
    private readonly Dictionary<StudioPage, StudioPageBase> _pages = new();
    private bool _mica;
    private bool? _dark;

    internal MainWindow(StudioOptions options)
    {
        _options = options;
        InitializeComponent();
        if (options.Width is { } width) Width = width;
        if (options.Height is { } height) Height = height;
        GlassScene.SetRenderMode(this, options.RenderMode);
        SourceInitialized += OnSourceInitialized;
        Loaded += OnLoaded;
        Navigation.SelectionChanged += (_, _) => ShowPage((StudioPage)Math.Max(0, Navigation.SelectedIndex));
        AppearancePicker.SelectionChanged += (_, _) => GlassScene.SetAppearance(this, AppearancePicker.SelectedIndex switch
        {
            1 => GlassAppearance.Light,
            2 => GlassAppearance.Dark,
            _ => GlassAppearance.Automatic,
        });
        // The picker is glass too: its resolved appearance (including "System") drives the window chrome.
        DependencyPropertyDescriptor.FromProperty(GlassScene.IsDarkProperty, typeof(GlassTabBar))
            .AddValueChanged(AppearancePicker, (_, _) => UpdateChrome());
        AppearancePicker.SelectedIndex = options.Appearance switch
        {
            GlassAppearance.Light => 1,
            GlassAppearance.Dark => 2,
            _ => 0,
        };
        Navigation.SelectedIndex = (int)options.Page;
        PageHost.SizeChanged += (_, _) => ClipContent();
        // A contrast theme (or a change between them) repaints the chrome in the theme's colors.
        SystemParameters.StaticPropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(SystemParameters.HighContrast)) Dispatcher.BeginInvoke(new Action(UpdateChrome));
        };
        UpdateChrome();
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        HwndSource.FromHwnd(new WindowInteropHelper(this).Handle)?.AddHook(OnWindowMessage);
        _mica = WindowBackdrop.TryApplyMica(this);
        if (_mica)
        {
            WindowChrome.SetWindowChrome(this, new WindowChrome
            {
                CaptionHeight = 40,
                GlassFrameThickness = new Thickness(-1),
                ResizeBorderThickness = SystemParameters.WindowResizeBorderThickness,
                UseAeroCaptionButtons = true,
            });
            TitleRow.Height = new GridLength(40);
            TitleBar.Visibility = Visibility.Visible;
        }
        UpdateChrome();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        var shader = GlassDiagnostics.CanUseShader(this) && GlassScene.GetRenderMode(this) != GlassRenderMode.Fallback;
        RendererStatus.Text = shader
            ? $"Rendering with the glass shader ({(GlassDiagnostics.ShaderSource == GlassShaderSource.Precompiled ? "precompiled" : "compiled at run time")})."
            : "Rendering the fallback material.";
        if (_options.ExitAfter is { } seconds)
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(seconds) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                Close();
            };
            timer.Start();
        }
    }

    private void ShowPage(StudioPage page)
    {
        if (!_pages.TryGetValue(page, out var view))
        {
            view = page switch
            {
                StudioPage.Controls => new ControlsPage(),
                StudioPage.Materials => new MaterialsPage(),
                StudioPage.ProgressiveBlur => new BlurPage(),
                _ => new HeroPage(),
            };
            _pages[page] = view;
        }
        view.SetDark(_dark == true);
        PageHost.Content = view;
    }

    private IntPtr OnWindowMessage(IntPtr hwnd, int message, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        // The system colors changed (another contrast theme, or a custom one edited).
        if (message == WmSysColorChange && SystemParameters.HighContrast) Dispatcher.BeginInvoke(new Action(UpdateChrome));
        return IntPtr.Zero;
    }

    private void UpdateChrome()
    {
        var dark = GlassScene.GetIsDark(AppearancePicker);
        _dark = dark;
        var contrast = SystemParameters.HighContrast;
        var palette = contrast
            ? ContrastPalette()
            : new ResourceDictionary { Source = new Uri(dark ? "Shell/StudioDark.xaml" : "Shell/StudioLight.xaml", UriKind.Relative) };
        var merged = Application.Current.Resources.MergedDictionaries;
        merged[merged.Count - 1] = palette;
        WindowBackdrop.SetDarkTitleBar(this, dark);
        // Mica shows through the chrome; without it, and in a contrast theme, the chrome is a solid color.
        if (_mica && !contrast) Background = Brushes.Transparent;
        else SetResourceReference(BackgroundProperty, "Studio.WindowBackground");
        foreach (var page in _pages.Values) page.SetDark(dark);
    }

    /// <summary>The chrome in the contrast theme's colors: window, window text and highlight.</summary>
    private static ResourceDictionary ContrastPalette() => new()
    {
        ["Studio.WindowBackground"] = SystemColors.WindowBrush,
        ["Studio.Foreground"] = SystemColors.WindowTextBrush,
        ["Studio.Secondary"] = SystemColors.WindowTextBrush,
        ["Studio.NavHover"] = Brushes.Transparent,
        ["Studio.NavSelected"] = SystemColors.HighlightBrush,
        ["Studio.ContentStroke"] = SystemColors.WindowTextBrush,
        ["Studio.SectionHaze"] = Brushes.Transparent,
    };

    private void ClipContent()
    {
        var size = PageHost.RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        // Only the corner that meets the navigation rail and title bar is rounded, as in Windows 11 apps.
        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(new Point(ContentCorner, 0), true, true);
            context.LineTo(new Point(size.Width, 0), false, false);
            context.LineTo(new Point(size.Width, size.Height), false, false);
            context.LineTo(new Point(0, size.Height), false, false);
            context.LineTo(new Point(0, ContentCorner), false, false);
            context.ArcTo(new Point(ContentCorner, 0), new Size(ContentCorner, ContentCorner), 0, false, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        PageHost.Clip = geometry;
    }
}
