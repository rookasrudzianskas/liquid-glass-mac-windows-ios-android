using System;
using Xunit;

namespace LiquidGlass.Tests;

/// <summary>The pieces the WPF renderer derives from the model: the fill kernel and the rim raster.</summary>
public class RenderingModelTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FillKernelIsNormalizedAndHasTheFillVariance(bool dark)
    {
        var kernel = FrostFillKernel.For(dark);
        double sum = kernel.Weights[0];
        for (var k = 1; k <= FrostFillKernel.Radius; k++) sum += 2 * kernel.Weights[k];
        Assert.Equal(1, sum, 12);
        var sigma = GlassMaterials.FillSigmaOverFrost(dark);
        Assert.Equal(sigma * sigma, kernel.Variance, 6);
        // Frost (σ 7.33) followed by the kernel equals the fill blur (σ 23 light, 21.8 dark) in variance.
        Assert.Equal(GlassMaterials.Regular(dark).FillSigma * GlassMaterials.Regular(dark).FillSigma,
            GlassMaterials.RegularBlur * GlassMaterials.RegularBlur + kernel.Variance, 6);
    }

    [Theory]
    [InlineData(false, 400, 300)]
    [InlineData(true, 400, 300)]
    [InlineData(false, 240, 900)]
    [InlineData(true, 180, 180)]
    public void FillKernelMatchesTheTrueGaussianOnSmoothBackdrops(bool dark, double periodX, double periodY)
    {
        // A smooth backdrop: a sinusoid. A Gaussian multiplies each frequency by exp(−2π²σ²f²) per axis; the frost
        // has already been applied, so compare the kernel against the remaining σₑ.
        var kernel = FrostFillKernel.For(dark);
        var sigma = kernel.Sigma;
        double Wave(double x, double y) => 0.5 + 0.3 * Math.Sin(2 * Math.PI * x / periodX) * Math.Cos(2 * Math.PI * y / periodY);
        double Exact(double x, double y)
        {
            var fx = 1 / periodX;
            var fy = 1 / periodY;
            var damping = Math.Exp(-2 * Math.PI * Math.PI * sigma * sigma * (fx * fx + fy * fy));
            return 0.5 + 0.3 * damping * Math.Sin(2 * Math.PI * x / periodX) * Math.Cos(2 * Math.PI * y / periodY);
        }
        for (var x = -200.0; x <= 200; x += 37)
            for (var y = -150.0; y <= 150; y += 29)
            {
                double approx = 0;
                for (var i = -FrostFillKernel.Radius; i <= FrostFillKernel.Radius; i++)
                    for (var j = -FrostFillKernel.Radius; j <= FrostFillKernel.Radius; j++)
                        approx += kernel.Weights[Math.Abs(i)] * kernel.Weights[Math.Abs(j)] * Wave(x + i * kernel.Spacing, y + j * kernel.Spacing);
                // The GPU tolerance of OPTICS.md.
                Assert.True(Math.Abs(approx - Exact(x, y)) < 2e-3, $"({x}, {y}): {approx} vs {Exact(x, y)}");
            }
    }

    [Fact]
    public void FillKernelTapsAreDenseEnoughForTheFrost()
    {
        // Aliasing of the frosted texture at the tap frequency is exp(−2π²σ²/s²); keep it near one percent (it is further scaled by the fill opacity).
        foreach (var dark in new[] { false, true })
        {
            var s = FrostFillKernel.For(dark).Spacing;
            var alias = Math.Exp(-2 * Math.PI * Math.PI * GlassMaterials.RegularBlur * GlassMaterials.RegularBlur / (s * s));
            Assert.True(alias < 0.02, $"alias {alias} at spacing {s}");
        }
    }

    [Theory]
    [InlineData(120, 44, "capsule", false, 1.0)]
    [InlineData(200, 120, "28", true, 1.5)]
    [InlineData(64, 64, "circle", false, 2.0)]
    [InlineData(300, 90, "16", true, 1.25)]
    public void RimRasterIsTheRimLightAtEveryPixelCenter(double width, double height, string radiusText, bool dark, double scale)
    {
        var radius = GlassRadius.Parse(radiusText);
        var tint = dark ? (Rgb?)null : new Rgb(0, 0.478, 1);
        var image = GlassRimRaster.Render(width, height, radius, dark, scale, tint);
        Assert.Equal(image.PixelWidth * 4 * image.PixelHeight, image.Pixels.Length);
        var shape = GlassShape.Parameters(width, height, radius);
        var lit = 0;
        for (var row = 0; row < image.PixelHeight; row++)
            for (var col = 0; col < image.PixelWidth; col++)
            {
                var x = (col - image.Padding + 0.5) / scale - width / 2;
                var y = (row - image.Padding + 0.5) / scale - height / 2;
                var f = GlassShape.Field(x, y, width, height, shape);
                var rim = GlassRimLight.Overlay(f.Distance, f.NormalX, f.NormalY, dark, 1 / scale, tint);
                var i = (row * image.PixelWidth + col) * 4;
                Assert.True(Math.Abs(image.Pixels[i + 3] - rim.A * 255) <= 0.51, $"alpha at ({col}, {row}): {image.Pixels[i + 3]} vs {rim.A * 255}");
                Assert.True(Math.Abs(image.Pixels[i + 2] - rim.R * 255) <= 0.51, $"red at ({col}, {row})");
                Assert.True(Math.Abs(image.Pixels[i] - rim.B * 255) <= 0.51, $"blue at ({col}, {row})");
                if (image.Pixels[i + 3] > 0) lit++;
            }
        Assert.True(lit > 0);
    }

    [Fact]
    public void RimRasterKeepsTheShadeOutsideAndTheHighlightOnTop()
    {
        var image = GlassRimRaster.Render(200, 64, GlassRadius.Capsule, false, 2);
        int Alpha(int col, int row) => image.Pixels[(row * image.PixelWidth + col) * 4 + 3];
        int Red(int col, int row) => image.Pixels[(row * image.PixelWidth + col) * 4 + 2];
        var center = image.PixelWidth / 2;
        // The specular line along the top edge is bright white.
        var top = image.Padding;
        Assert.True(Red(center, top) > 120 && Alpha(center, top) > 120);
        // The side shade sits just outside the left edge and is dark (alpha without light).
        var middle = image.PixelHeight / 2;
        var outside = image.Padding;
        var shaded = Alpha(outside, middle) + Alpha(outside - 1, middle);
        Assert.True(shaded > 60);
        Assert.True(Red(outside - 1, middle) < 10);
        // The middle of the glass carries no rim.
        Assert.Equal(0, Alpha(center, middle));
    }
}
