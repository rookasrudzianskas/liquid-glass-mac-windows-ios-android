using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace LiquidGlass.CiTools;

/// <summary>
/// Measures the Gaussian σ that WPF's <see cref="BlurEffect"/> renders, in device pixels: over a hard black | white
/// edge, for single and nested effects, under a world scale of 1, 2 and 3 (what 100%, 200% and 300% displays apply),
/// on screen (WPF's hardware pipeline) and in <see cref="RenderTargetBitmap"/> (its software renderer). The library
/// plans its progressive blur and Regular frost from this model, so the report shows whether the model holds.
/// </summary>
internal static class BlurProbe
{
    /// <summary>Height of each sample row and of the gap after it, in device pixels at every scale.</summary>
    private const int RowPixels = 90;
    private const int GapPixels = 10;

    /// <summary>Width of each row, in device pixels; the edge is in the middle.</summary>
    private const int WidthPixels = 600;

    /// <summary>Where the dark and light levels are read, in device pixels from the edge.</summary>
    private const int ReferencePixels = 150;

    private static readonly (double Scale, double[][] Cases)[] Plan =
    {
        (1, new[] { new[] { 15.0 }, new[] { 30.0 }, new[] { 45.0 }, new[] { 60.0 }, new[] { 90.0 }, new[] { 100.5 }, new[] { 150.0 }, new[] { 60.5, 60.5 }, new[] { 100.5, 100.5 } }),
        (2, new[] { new[] { 10.0 }, new[] { 22.25 }, new[] { 30.0 }, new[] { 42.75 }, new[] { 50.25 }, new[] { 60.0 }, new[] { 42.75, 42.75 }, new[] { 30.0, 30.0 } }),
        (3, new[] { new[] { 10.0 }, new[] { 20.0 }, new[] { 22.17 }, new[] { 30.17 }, new[] { 33.5 }, new[] { 40.0 }, new[] { 30.17, 30.17, 30.17, 30.17 } }),
    };

    public static int Run(string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var report = new StringBuilder();
        report.AppendLine("σ in device pixels from the 10%–90% width of a blurred black | white edge (2.563 σ).");
        report.AppendLine("model: each effect blurs by min(100, trunc(Radius × scale)) / 3 device pixels; nested effects add variances.");
        var exit = 0;
        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
        app.Startup += (_, _) =>
        {
            try
            {
                foreach (var (scale, cases) in Plan) Measure(scale, cases, outDirectory, report);
            }
            catch (Exception e)
            {
                report.AppendLine(e.ToString());
                exit = 1;
            }
            app.Shutdown();
        };
        app.Run();
        File.WriteAllText(Path.Combine(outDirectory, "blur.txt"), report.ToString());
        Console.WriteLine(report);
        return exit;
    }

    private static void Measure(double scale, double[][] cases, string outDirectory, StringBuilder report)
    {
        var rows = new List<FrameworkElement>();
        var stack = new StackPanel();
        foreach (var radii in cases)
        {
            var edge = new Grid { Width = WidthPixels / scale, Height = RowPixels / scale };
            edge.ColumnDefinitions.Add(new ColumnDefinition());
            edge.ColumnDefinitions.Add(new ColumnDefinition());
            var white = new Border { Background = Brushes.White };
            Grid.SetColumn(white, 1);
            edge.Children.Add(new Border { Background = Brushes.Black });
            edge.Children.Add(white);
            FrameworkElement content = edge;
            for (var pass = 0; pass < radii.Length; pass++)
            {
                var blur = new BlurEffect { Radius = radii[pass], KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
                if (pass == 0) edge.Effect = blur;
                else content = new Border { Child = content, Effect = blur };
            }
            content.Margin = new Thickness(0, 0, 0, GapPixels / scale);
            rows.Add(edge);
            stack.Children.Add(content);
        }
        // The window's own scale is 1 on the runner; the layout transform supplies the display scale.
        var scaled = new Border { Child = stack, LayoutTransform = new ScaleTransform(scale, scale), Margin = new Thickness(0, 0, 0, 0) };
        var root = new Border { Background = new SolidColorBrush(Color.FromRgb(128, 128, 128)), Padding = new Thickness(40), Child = scaled };
        var window = new Window
        {
            Title = $"Liquid Glass blur probe {scale}x",
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            SizeToContent = SizeToContent.WidthAndHeight,
            Left = 0,
            Top = 0,
            Topmost = true,
            Content = root,
        };
        window.Show();
        Wait(1.5);

        // Without a GPU, WPF draws these blurs in software, and a large window's first frame can take seconds to reach
        // the screen (the capture is black until then): wait for the gray margin around the rows to show.
        var hwnd = new WindowInteropHelper(window).Handle;
        var origin = root.PointToScreen(default);
        Image screen;
        var deadline = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            var shot = WindowCapture.Capture(hwnd);
            screen = new Image(shot.Pixels, shot.Width, shot.Height, (int)Math.Round(origin.X) - shot.Left, (int)Math.Round(origin.Y) - shot.Top);
            if (screen.Green(10, 10) is > 100 and < 160 || DateTime.UtcNow > deadline) break;
            Wait(0.5);
        }
        WindowCapture.SavePng(hwnd, Path.Combine(outDirectory, $"blur-{scale.ToString(CultureInfo.InvariantCulture)}x.png"));

        var width = (int)Math.Ceiling(root.ActualWidth);
        var height = (int)Math.Ceiling(root.ActualHeight);
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(root);
        var pixels = new byte[width * height * 4];
        bitmap.CopyPixels(pixels, width * 4, 0);
        var software = new Image(pixels, width, height, 0, 0);

        report.AppendLine();
        report.AppendLine($"scale {scale.ToString(CultureInfo.InvariantCulture)} (window DPI scale {VisualTreeHelper.GetDpi(window).DpiScaleX.ToString(CultureInfo.InvariantCulture)}, render tier {RenderCapability.Tier >> 16})");
        for (var i = 0; i < cases.Length; i++)
        {
            var bounds = rows[i].TransformToAncestor(root).TransformBounds(new Rect(rows[i].RenderSize));
            var y = (int)Math.Round(bounds.Top + bounds.Height / 2);
            var x = (int)Math.Round(bounds.Left + bounds.Width / 2);
            var model = Math.Sqrt(cases[i].Sum(r => Math.Pow(Math.Min(100, Math.Floor(r * scale)) / 3, 2)));
            var radii = string.Join(" + ", cases[i].Select(r => $"{r.ToString("0.##", CultureInfo.InvariantCulture)} ({(r * scale).ToString("0.##", CultureInfo.InvariantCulture)} px)"));
            report.AppendLine($"  {radii}: screen σ {Format(screen.Sigma(x, y))}, bitmap σ {Format(software.Sigma(x, y))}, model σ {Format(model)}");
        }
        window.Close();
    }

    private static string Format(double sigma) => double.IsNaN(sigma) ? "n/a" : sigma.ToString("0.00", CultureInfo.InvariantCulture);

    private static void Wait(double seconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }

    /// <summary>BGRA pixels with the position of the measured layout's origin in them.</summary>
    private sealed class Image
    {
        private readonly byte[] _pixels;
        private readonly int _width;
        private readonly int _height;
        private readonly int _originX;
        private readonly int _originY;

        public Image(byte[] pixels, int width, int height, int originX, int originY)
        {
            _pixels = pixels;
            _width = width;
            _height = height;
            _originX = originX;
            _originY = originY;
        }

        /// <summary>The green channel at (<paramref name="x"/>, <paramref name="y"/>) from the origin, or -1 outside the image.</summary>
        public int Green(int x, int y)
        {
            int px = _originX + x, py = _originY + y;
            return px < 0 || py < 0 || px >= _width || py >= _height ? -1 : _pixels[(py * _width + px) * 4 + 1];
        }

        /// <summary>σ of the edge at layout pixel (<paramref name="x"/>, <paramref name="y"/>), from its 10%–90% width.</summary>
        public double Sigma(int x, int y)
        {
            var row = _originY + y;
            var center = _originX + x;
            if (row < 0 || row >= _height || center - ReferencePixels < 0 || center + ReferencePixels >= _width) return double.NaN;
            double Green(int px) => _pixels[(row * _width + px) * 4 + 1];
            var dark = Green(center - ReferencePixels);
            var light = Green(center + ReferencePixels);
            if (light - dark < 20) return double.NaN;
            double Cross(double fraction)
            {
                var level = dark + (light - dark) * fraction;
                for (var px = center - ReferencePixels; px < center + ReferencePixels; px++)
                {
                    double a = Green(px), b = Green(px + 1);
                    if (a <= level && b > level) return px + (level - a) / (b - a);
                }
                return double.NaN;
            }
            return (Cross(0.9) - Cross(0.1)) / 2.5631;
        }
    }
}
