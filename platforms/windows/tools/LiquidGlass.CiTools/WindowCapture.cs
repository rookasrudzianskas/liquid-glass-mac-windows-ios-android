using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiquidGlass.CiTools;

/// <summary>Captures a window's composed pixels, including hardware-rendered WPF content.</summary>
internal static class WindowCapture
{
    private const uint PwRenderFullContent = 2;

    private const int ExtendedFrameBounds = 9;

    /// <summary>
    /// Captures <paramref name="hwnd"/> with PrintWindow(PW_RENDERFULLCONTENT) and saves a PNG of its visible frame
    /// (without the invisible resize borders).
    /// </summary>
    public static (int Width, int Height) SavePng(IntPtr hwnd, string path)
    {
        var shot = Capture(hwnd);
        var width = shot.Width;
        var height = shot.Height;
        BitmapSource image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, shot.Pixels, width * 4);
        if (DwmGetWindowAttribute(hwnd, ExtendedFrameBounds, out var frame, Marshal.SizeOf<Rect>()) == 0)
        {
            var crop = new Int32Rect(frame.Left - shot.Left, frame.Top - shot.Top, frame.Right - frame.Left, frame.Bottom - frame.Top);
            if (crop.X >= 0 && crop.Y >= 0 && crop.Width > 0 && crop.Height > 0 && crop.X + crop.Width <= width && crop.Y + crop.Height <= height)
            {
                image = new CroppedBitmap(image, crop);
                width = crop.Width;
                height = crop.Height;
            }
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        using var file = File.Create(path);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        encoder.Save(file);
        return (width, height);
    }

    /// <summary>
    /// The composed pixels of <paramref name="hwnd"/>'s whole window rectangle (opaque BGRA, top row first), with the
    /// rectangle's screen position.
    /// </summary>
    public static Shot Capture(IntPtr hwnd)
    {
        if (!GetWindowRect(hwnd, out var rect)) throw new InvalidOperationException("GetWindowRect failed.");
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var info = new BitmapInfo
        {
            Size = 40, // sizeof(BITMAPINFOHEADER)
            Width = width,
            Height = -height,
            Planes = 1,
            BitCount = 32,
        };
        var bitmap = CreateDIBSection(memory, ref info, 0, out var bits, IntPtr.Zero, 0);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(hwnd, memory, PwRenderFullContent))
                throw new InvalidOperationException("PrintWindow failed.");
            var pixels = new byte[width * 4 * height];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            // The DIB is BGRX; make it opaque.
            for (var i = 3; i < pixels.Length; i += 4) pixels[i] = 255;
            return new Shot(pixels, width, height, rect.Left, rect.Top);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>A window capture: BGRA pixels and the window rectangle's screen position.</summary>
    public sealed record Shot(byte[] Pixels, int Width, int Height, int Left, int Top);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
        public int Colors;
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetWindowRect(IntPtr hwnd, out Rect rect);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(IntPtr hwnd, int attribute, out Rect value, int size);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool PrintWindow(IntPtr hwnd, IntPtr hdc, uint flags);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hwnd, IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo info, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr obj);
}
