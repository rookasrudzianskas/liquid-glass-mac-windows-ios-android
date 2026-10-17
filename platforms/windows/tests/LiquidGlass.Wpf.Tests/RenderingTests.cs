using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiquidGlass.Wpf.Rendering;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

/// <summary>How glass lines its copy of the backdrop up with the screen, and how it follows the display's scale.</summary>
public class RenderingTests
{
    /// <summary>
    /// Glass over a red | blue seam shows, at every one of its pixels, the color beneath that pixel: across a
    /// right-to-left boundary (WPF mirrors the subtree), under rotation and under skew.
    /// </summary>
    [Theory]
    [InlineData("right-to-left")]
    [InlineData("rotated 90")]
    [InlineData("rotated 45")]
    [InlineData("skewed")]
    [InlineData("right-to-left and rotated 30")]
    public void GlassShowsTheBackdropBeneathEachOfItsPixels(string variant)
    {
        Sta.Run(() =>
        {
            const int width = 400, height = 260;
            var backdrop = new Grid();
            backdrop.ColumnDefinitions.Add(new ColumnDefinition());
            backdrop.ColumnDefinitions.Add(new ColumnDefinition());
            var red = new Border { Background = Brushes.Red };
            var blue = new Border { Background = Brushes.Blue };
            Grid.SetColumn(blue, 1);
            backdrop.Children.Add(red);
            backdrop.Children.Add(blue);

            var surface = new GlassSurface
            {
                Width = 160,
                Height = 100,
                Radius = 20,
                Refraction = 0,
                Material = GlassMaterial.Clear,
                Appearance = GlassAppearance.Light,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                RenderTransformOrigin = new Point(0.5, 0.5),
            };
            UIElement child = surface;
            if (variant.StartsWith("right-to-left", StringComparison.Ordinal)) child = new Border { FlowDirection = FlowDirection.RightToLeft, Child = surface };
            surface.RenderTransform = variant switch
            {
                "rotated 90" => new RotateTransform(90),
                "rotated 45" => new RotateTransform(45),
                "skewed" => new SkewTransform(20, 8),
                "right-to-left and rotated 30" => new RotateTransform(30),
                _ => Transform.Identity,
            };
            var scene = new GlassScene { Content = backdrop };
            // RenderTargetBitmap renders in software, where the pixel shader cannot run: check the fallback's copy.
            GlassScene.SetRenderMode(scene, GlassRenderMode.Fallback);
            scene.Children.Add(child);
            var root = new Border { Child = scene };
            var window = Sta.Show(root, width, height);
            Sta.Pump();
            Assert.Equal(GlassLensMode.Fallback, surface.RenderedMode);

            var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var pixels = new byte[width * height * 4];
            bitmap.CopyPixels(pixels, width * 4, 0);
            var toRoot = surface.TransformToAncestor(root);
            var checkedPoints = 0;
            for (var u = 0.15; u <= 0.851; u += 0.1)
                for (var v = 0.3; v <= 0.701; v += 0.2)
                {
                    var p = toRoot.Transform(new Point(u * surface.Width, v * surface.Height));
                    if (Math.Abs(p.X - width / 2.0) < 12) continue;
                    var i = ((int)p.Y * width + (int)p.X) * 4;
                    int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                    var expectRed = p.X < width / 2.0;
                    Assert.True(expectRed ? r > b + 60 : b > r + 60,
                        $"{variant}: the glass at ({p.X:F0}, {p.Y:F0}) shows rgb({r}, {g}, {b}), but the backdrop beneath it is {(expectRed ? "red" : "blue")}");
                    checkedPoints++;
                }
            Assert.True(checkedPoints >= 8, $"only {checkedPoints} points checked");
            window.Close();
        });
    }

    [Fact]
    public void RimLightFollowsADpiChange()
    {
        Sta.Run(() =>
        {
            // A lens outside a scene draws the opaque material with the rasterized rim light.
            var lens = new GlassLens { Radius = GlassRadius.Capsule };
            var root = new Grid { Width = 200, Height = 64, Children = { lens } };
            // Start from a known scale. Before the process has created any window, WPF's table of DPI values is empty
            // and the first SetRootDpi would enter the new scale as the system one, so old and new would compare equal
            // and no element would be told of the change.
            VisualTreeHelper.SetRootDpi(root, new DpiScale(1, 1));
            root.Measure(new Size(200, 64));
            root.Arrange(new Rect(0, 0, 200, 64));
            // Size changes reach the lens (OnRenderSizeChanged) only when the layout manager runs its queue.
            root.UpdateLayout();
            Assert.Equal(GlassLensMode.Opaque, lens.Mode);
            Assert.NotNull(lens.Overlay.RimBitmap);

            // WPF notifies the lens of the new DPI before its children: the rim must be rasterized at the new scale anyway.
            Assert.Equal(96, lens.Overlay.RimBitmap!.DpiX, 3);
            const double target = 1.5;
            VisualTreeHelper.SetRootDpi(root, new DpiScale(target, target));
            var rim = lens.Overlay.RimBitmap;
            Assert.NotNull(rim);
            Assert.Equal(96 * target, rim!.DpiX, 3);
            var expected = GlassRimRaster.Render(200, 64, GlassRadius.Capsule, lens.IsDark, target, null);
            Assert.Equal(expected.PixelWidth, rim.PixelWidth);
            Assert.Equal(expected.PixelHeight, rim.PixelHeight);
        });
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(1.75)]
    [InlineData(2.0)]
    [InlineData(2.5)]
    [InlineData(3.0)]
    [InlineData(5.0)]
    public void BlurPassesRenderTheSigmaAskedFor(double scale)
    {
        foreach (var blur in new[] { GlassProgressive.DefaultBlur, GlassProgressiveBlur.MaxBlur })
            for (var level = 1; level <= GlassProgressive.Levels; level++)
            {
                var sigma = GlassProgressive.LevelBlur(level, blur);
                var radii = BlurPasses.Radii(sigma, scale);
                Assert.All(radii, r => Assert.True(Math.Floor(r * scale) <= BlurPasses.MaxDeviceRadius, $"pass of {r * scale} device pixels"));
                var rendered = BlurPasses.RenderedSigma(radii, scale);
                Assert.True(Math.Abs(rendered - sigma) < 0.25, $"blur {blur}, level {level} at {scale}: σ {rendered:F3} instead of {sigma:F3}");
            }
        // The frost of Regular glass, too.
        Assert.True(Math.Abs(BlurPasses.RenderedSigma(BlurPasses.Radii(22 / 3.0, scale), scale) - 22 / 3.0) < 0.25);
    }

    /// <summary>
    /// The progressive blur as it reaches the screen at 100%, 200% and 300% display scale: the strongest level and level
    /// 5 render their σ. The window's own scale is 1 on the test machine, so a layout transform applies the display's
    /// scale (WPF applies a display's as a root transform the same way) and the blur plans its passes for it. At 100%
    /// the strongest level of the largest blur is one effect of 99 device pixels, the most one renders.
    /// </summary>
    [Theory]
    [InlineData(1.0, GlassProgressiveBlur.MaxBlur)]
    [InlineData(2.0, 20.0)]
    [InlineData(3.0, 20.0)]
    public void ProgressiveBlurRendersItsSigmaAtHighDisplayScales(double scale, double edgeBlur)
    {
        Sta.Run(() =>
        {
            // A vertical hard edge, black | white, under a band 80 units deep at the bottom of a 240-unit area; the
            // source continues below the band so the blur has content on every side.
            const int size = 300;
            var source = new Grid { Width = size, Height = size, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            source.ColumnDefinitions.Add(new ColumnDefinition());
            source.ColumnDefinitions.Add(new ColumnDefinition());
            var white = new Border { Background = Brushes.White };
            Grid.SetColumn(white, 1);
            source.Children.Add(new Border { Background = Brushes.Black });
            source.Children.Add(white);
            var blur = new GlassProgressiveBlur { Edge = GlassEdge.Bottom, Size = 80, Blur = edgeBlur, Source = source, Height = 240, VerticalAlignment = VerticalAlignment.Top };
            var content = new Grid { Width = size, Height = size, Children = { source, blur } };
            var scaled = new Border { Child = content, LayoutTransform = new ScaleTransform(scale, scale) };
            var root = new Border { Child = scaled };
            var px = (int)Math.Round(size * scale);
            var window = Sta.Show(root, px, px);
            // Plan the passes for this scale, as the element does on such a display.
            blur.ScaleOverride = scale;
            Sta.Pump();
            Sta.Wait(0.5);
            Assert.Equal(GlassProgressive.Levels, blur.LevelRadii.Count);
            if (scale > 1) Assert.True(blur.LevelRadii[GlassProgressive.Levels - 1].Count > 1, "the strongest level should need more than one pass");

            // Above the band the source is sharp: black on the left, white on the right once the frame is on screen.
            var screen = ScreenPixels.CaptureWhen(window, root,
                s => s.Green((int)(280 * scale), (int)(20 * scale)) - s.Green((int)(20 * scale), (int)(20 * scale)) > 200, 20);
            var bitmap = new RenderTargetBitmap(px, px, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(root);
            var pixels = new byte[px * px * 4];
            bitmap.CopyPixels(pixels, px * 4, 0);

            // σ from the 10%–90% width of the blurred step: 2.563 σ for a Gaussian.
            double Sigma(Func<int, int, double> green, double y)
            {
                var row = (int)Math.Round(y * scale);
                int from = (int)(50 * scale), to = (int)(250 * scale);
                var dark = green(from, row);
                var light = green(to, row);
                double Cross(double fraction)
                {
                    var level = dark + (light - dark) * fraction;
                    for (var x = from; x < to; x++)
                    {
                        double a = green(x, row), b = green(x + 1, row);
                        if (a <= level && b > level) return x + (level - a) / (b - a);
                    }
                    return double.NaN;
                }
                return (Cross(0.9) - Cross(0.1)) / 2.5631 / scale;
            }
            double InBitmap(int x, int y) => pixels[(y * px + x) * 4 + 1];

            // At the edge only the strongest level shows; where level 5 has all the weight, only it does.
            var level5 = SolveEase(5.0 / 6);
            var expected6 = GlassProgressive.LevelBlur(6, edgeBlur);
            var expected5 = GlassProgressive.LevelBlur(5, edgeBlur);
            var screen6 = screen is null ? double.NaN : Sigma((x, y) => screen.Green(x, y), 239);
            var screen5 = screen is null ? double.NaN : Sigma((x, y) => screen.Green(x, y), 160 + level5 * 80);
            var bitmap6 = Sigma(InBitmap, 239);
            var bitmap5 = Sigma(InBitmap, 160 + level5 * 80);
            var passes = string.Join("; ", blur.LevelRadii.Select(level => string.Join(" + ", level.Select(r => (r * scale).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture)))));
            var detail = $"on screen σ {screen6:F2} (level 6) and {screen5:F2} (level 5){(screen is null ? ", the frame never reached the screen" : "")}; " +
                         $"RenderTargetBitmap σ {bitmap6:F2} and {bitmap5:F2}; passes in device pixels: {passes}";
            // WPF's software renderer (RenderTargetBitmap) and its hardware pipeline draw BlurEffect alike (the CI blur
            // probe measures both); both must render the plan. A runner whose screen never shows the frame leaves only
            // the bitmap to check.
            foreach (var (where, sigma6, sigma5) in new[] { ("in the bitmap", bitmap6, bitmap5), ("on screen", screen6, screen5) })
            {
                if (where == "on screen" && screen is null) continue;
                Assert.True(Math.Abs(sigma6 - expected6) < 1.5, $"level 6 at {scale}× {where}: σ {sigma6:F2} instead of {expected6:F2} ({detail})");
                Assert.True(Math.Abs(sigma5 - expected5) < 1.2, $"level 5 at {scale}× {where}: σ {sigma5:F2} instead of {expected5:F2} ({detail})");
                Assert.True(sigma6 > sigma5 + 3, $"the edge ({sigma6:F2}) should be blurred more than level 5 ({sigma5:F2}) {where} ({detail})");
            }
            if (scale <= 1) Assert.True(screen is not null, $"the window never reached the screen at {scale}× ({detail})");
            window.Close();
        });
    }

    /// <summary>The depth u at which <see cref="GlassProgressive.Ease"/> reaches <paramref name="value"/>.</summary>
    private static double SolveEase(double value)
    {
        double lo = 0, hi = 1;
        for (var i = 0; i < 60; i++)
        {
            var mid = (lo + hi) / 2;
            if (GlassProgressive.Ease(mid) < value) lo = mid;
            else hi = mid;
        }
        return (lo + hi) / 2;
    }

    [Fact]
    public void ProgressiveBlurPlansForItsOwnScale()
    {
        Sta.Run(() =>
        {
            var source = new Border { Background = Brushes.Teal, Width = 300, Height = 300 };
            var blur = new GlassProgressiveBlur { Source = source, Blur = 20 };
            var window = Sta.Show(new Grid { Children = { source, blur } }, 300, 300);
            var scale = GlassResolve.Scale(blur);
            var strongest = blur.LevelRadii.Last();
            Assert.True(BlurPasses.Radii(20, scale).SequenceEqual(strongest), $"strongest level: {string.Join(", ", strongest)} at {scale}×");
            window.Close();
        });
    }
}
