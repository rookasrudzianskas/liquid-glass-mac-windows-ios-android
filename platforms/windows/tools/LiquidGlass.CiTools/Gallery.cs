using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using LiquidGlass.Wpf;

namespace LiquidGlass.CiTools;

/// <summary>
/// Renders a matrix of glass variants over a wallpaper (shader and fallback, light and dark, Clear and Regular,
/// tints and radii), plus orientation probes: glass over a red | blue seam, across a right-to-left boundary, rotated
/// and skewed, which must show red on its left and blue on its right on screen. Captures the window and reports how
/// each surface rendered.
/// </summary>
internal static class Gallery
{
    public static int Run(string outDirectory, string wallpaper)
    {
        Directory.CreateDirectory(outDirectory);
        var report = new StringBuilder();
        var surfaces = new List<(string Name, GlassSurface Surface)>();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var columns = new Grid { Margin = new Thickness(24) };
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        columns.ColumnDefinitions.Add(new ColumnDefinition());
        foreach (var (column, mode) in new[] { (0, GlassRenderMode.Shader), (1, GlassRenderMode.Fallback) })
        {
            var stack = new StackPanel { Margin = new Thickness(12, 0, 12, 0) };
            GlassScene.SetRenderMode(stack, mode);
            Grid.SetColumn(stack, column);
            stack.Children.Add(Label(mode.ToString()));
            foreach (var appearance in new[] { GlassAppearance.Light, GlassAppearance.Dark })
                foreach (var material in new[] { GlassMaterial.Clear, GlassMaterial.Regular })
                {
                    var row = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
                    GlassScene.SetAppearance(row, appearance);
                    GlassScene.SetMaterial(row, material);
                    var name = $"{mode}-{appearance}-{material}";
                    row.Children.Add(Track(surfaces, name + "-capsule", new GlassSurface { Width = 200, Height = 64, Radius = GlassRadius.Capsule, Margin = new Thickness(6), Content = Text($"{appearance} {material}") }));
                    row.Children.Add(Track(surfaces, name + "-card", new GlassSurface { Width = 180, Height = 110, Radius = 28, Margin = new Thickness(6), Content = Text("Radius 28") }));
                    row.Children.Add(Track(surfaces, name + "-circle", new GlassSurface { Width = 64, Height = 64, Radius = GlassRadius.Circle, Margin = new Thickness(6), Content = Text("◎") }));
                    row.Children.Add(Track(surfaces, name + "-tint", new GlassSurface { Width = 120, Height = 44, Radius = GlassRadius.Capsule, Margin = new Thickness(6), Tint = Color.FromRgb(0, 122, 255), Content = Text("Tint") }));
                    stack.Children.Add(row);
                }
            var probes = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };
            foreach (var (label, place) in new (string, Func<GlassSurface, UIElement>)[]
            {
                ("RTL", glass => new Border { FlowDirection = FlowDirection.RightToLeft, Child = glass }),
                ("45°", glass => Transformed(glass, new RotateTransform(45))),
                ("90°", glass => Transformed(glass, new RotateTransform(90))),
                ("Skew", glass => Transformed(glass, new SkewTransform(18, 6))),
            })
            {
                var glass = new GlassSurface { Width = 100, Height = 64, Radius = 22, Refraction = 18, Content = Text(label), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                Track(surfaces, $"{mode}-orientation-{label}", glass);
                probes.Children.Add(Probe(place(glass)));
            }
            stack.Children.Add(probes);
            columns.Children.Add(stack);
        }
        var scene = new GlassScene
        {
            Content = new Image { Source = new BitmapImage(new Uri(Path.GetFullPath(wallpaper))), Stretch = Stretch.UniformToFill },
        };
        scene.Children.Add(columns);
        var window = new Window
        {
            Title = "Liquid Glass gallery",
            Width = 1440,
            Height = 900,
            UseLayoutRounding = true,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Content = scene,
        };
        var exit = 0;
        window.ContentRendered += (_, _) =>
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    report.AppendLine(GlassDiagnostics.Describe());
                    report.AppendLine($"can use shader here: {GlassDiagnostics.CanUseShader(window)}");
                    foreach (var (name, surface) in surfaces)
                    {
                        var source = DependencyPropertyHelper.GetValueSource(surface, Control.ForegroundProperty);
                        report.AppendLine($"{name}: {surface.RenderedMode}, dark {GlassScene.GetIsDark(surface)}, content foreground {GlassScene.GetContentForeground(surface)}, " +
                            $"foreground {surface.Foreground} from {source.BaseValueSource}{(source.IsExpression ? " (expression)" : "")}, " +
                            $"binding {System.Windows.Data.BindingOperations.GetBindingExpression(surface, Control.ForegroundProperty)?.Status}");
                    }
                    var hwnd = new WindowInteropHelper(window).Handle;
                    WindowCapture.SavePng(hwnd, Path.Combine(outDirectory, "gallery.png"));
                }
                catch (Exception e)
                {
                    report.AppendLine(e.ToString());
                    exit = 1;
                }
                window.Close();
            };
            timer.Start();
        };
        app.Run(window);
        File.WriteAllText(Path.Combine(outDirectory, "gallery.txt"), report.ToString());
        Console.WriteLine(report);
        return exit;
    }

    private static UIElement Transformed(GlassSurface glass, Transform transform)
    {
        glass.RenderTransformOrigin = new Point(0.5, 0.5);
        glass.RenderTransform = transform;
        return glass;
    }

    /// <summary>A small scene of its own: red on the left, blue on the right, marked L and R, with the glass over the seam.</summary>
    private static GlassScene Probe(UIElement glass)
    {
        var backdrop = new Grid();
        backdrop.ColumnDefinitions.Add(new ColumnDefinition());
        backdrop.ColumnDefinitions.Add(new ColumnDefinition());
        var left = new Border { Background = new SolidColorBrush(Color.FromRgb(0xE5, 0x48, 0x4D)), Child = Mark("L", HorizontalAlignment.Left) };
        var right = new Border { Background = new SolidColorBrush(Color.FromRgb(0x3E, 0x63, 0xDD)), Child = Mark("R", HorizontalAlignment.Right) };
        Grid.SetColumn(right, 1);
        backdrop.Children.Add(left);
        backdrop.Children.Add(right);
        // Four probes (162 units each with their margins) fit one row of a column, so every one is captured whole.
        var scene = new GlassScene { Width = 150, Height = 124, Margin = new Thickness(6), Content = backdrop };
        GlassScene.SetMaterial(scene, GlassMaterial.Clear);
        scene.Children.Add(glass);
        return scene;
    }

    private static TextBlock Mark(string text, HorizontalAlignment alignment) => new()
    {
        Text = text,
        FontSize = 13,
        FontWeight = FontWeights.Bold,
        Foreground = Brushes.White,
        Margin = new Thickness(6, 4, 6, 0),
        HorizontalAlignment = alignment,
        VerticalAlignment = VerticalAlignment.Top,
    };

    private static GlassSurface Track(List<(string, GlassSurface)> surfaces, string name, GlassSurface surface)
    {
        surfaces.Add((name, surface));
        return surface;
    }

    private static TextBlock Text(string text) => new()
    {
        Text = text,
        FontSize = 15,
        FontWeight = FontWeights.SemiBold,
        HorizontalAlignment = HorizontalAlignment.Center,
        VerticalAlignment = VerticalAlignment.Center,
    };

    private static TextBlock Label(string text) => new()
    {
        Text = text,
        FontSize = 22,
        FontWeight = FontWeights.Bold,
        Foreground = Brushes.White,
        Margin = new Thickness(6, 0, 0, 4),
    };
}
