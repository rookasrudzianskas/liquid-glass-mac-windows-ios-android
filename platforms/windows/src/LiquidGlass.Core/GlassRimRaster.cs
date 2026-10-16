using System;

namespace LiquidGlass;

/// <summary>A premultiplied BGRA image of the rim light, aligned to the device-pixel grid of a surface.</summary>
public sealed class RimImage
{
    internal RimImage(int pixelWidth, int pixelHeight, int padding, double pixel, byte[] pixels)
    {
        PixelWidth = pixelWidth;
        PixelHeight = pixelHeight;
        Padding = padding;
        Pixel = pixel;
        Pixels = pixels;
    }

    /// <summary>Width in device pixels.</summary>
    public int PixelWidth { get; }

    /// <summary>Height in device pixels.</summary>
    public int PixelHeight { get; }

    /// <summary>Device pixels the image extends beyond the outline's bounds on every side.</summary>
    public int Padding { get; }

    /// <summary>One device pixel in layout units.</summary>
    public double Pixel { get; }

    /// <summary>Row stride in bytes.</summary>
    public int Stride => PixelWidth * 4;

    /// <summary>Premultiplied BGRA bytes, row by row from the top.</summary>
    public byte[] Pixels { get; }
}

/// <summary>
/// Rasterizes the rim light (OPTICS §4) on the CPU. The fallback material uses it where the pixel shader cannot run,
/// so the rim is exactly the one the shader draws.
/// </summary>
public static class GlassRimRaster
{
    /// <summary>The rim reaches into the glass this far (the diffuse glow), in layout units.</summary>
    private const double InnerReach = 4.5;

    /// <summary>
    /// Renders the rim of a <paramref name="width"/> × <paramref name="height"/> outline whose top-left corner sits on a
    /// device-pixel boundary, at <paramref name="scale"/> device pixels per layout unit.
    /// </summary>
    public static RimImage Render(double width, double height, GlassRadius radius, bool dark, double scale, Rgb? tint = null)
    {
        if (!(scale > 0) || double.IsInfinity(scale)) throw new ArgumentOutOfRangeException(nameof(scale), scale, "Scale must be positive and finite.");
        width = Math.Max(0, width);
        height = Math.Max(0, height);
        var pixel = 1 / scale;
        // The side shade lies in a band just outside the outline that ends half a pixel past 1 unit.
        var padding = (int)Math.Ceiling(1.5 * scale) + 1;
        var pw = (int)Math.Ceiling(width * scale) + 2 * padding;
        var ph = (int)Math.Ceiling(height * scale) + 2 * padding;
        var pixels = new byte[pw * ph * 4];
        var shape = GlassShape.Parameters(width, height, radius);
        var halfW = width / 2;
        var halfH = height / 2;
        var reach = InnerReach + 2 * pixel;
        for (var row = 0; row < ph; row++)
        {
            var y = (row - padding + 0.5) * pixel - halfH;
            // Away from the top and bottom (and the corners), only the vertical edges carry any light.
            var sidesOnly = Math.Abs(y) < halfH - Math.Max(shape.Extent, reach);
            for (var col = 0; col < pw; col++)
            {
                var x = (col - padding + 0.5) * pixel - halfW;
                if (sidesOnly && Math.Abs(x) < halfW - reach)
                {
                    // Jump to the first column of the right-hand band: x ≥ halfW − reach.
                    var next = (int)Math.Ceiling((width - reach) * scale + padding - 0.5);
                    col = Math.Max(col, next - 1);
                    continue;
                }
                var field = GlassShape.Field(x, y, width, height, shape);
                if (field.Distance < -reach) continue;
                var rim = GlassRimLight.Overlay(field.Distance, field.NormalX, field.NormalY, dark, pixel, tint);
                if (rim.A <= 0) continue;
                var i = (row * pw + col) * 4;
                pixels[i] = ToByte(rim.B);
                pixels[i + 1] = ToByte(rim.G);
                pixels[i + 2] = ToByte(rim.R);
                pixels[i + 3] = ToByte(rim.A);
            }
        }
        return new RimImage(pw, ph, padding, pixel, pixels);
    }

    private static byte ToByte(double v) => (byte)Math.Round(GlassMath.Sat(v) * 255);
}
