using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace LiquidGlass.Wpf.Tests;

/// <summary>
/// What a window shows on screen, as WPF's hardware pipeline composed it (PrintWindow with PW_RENDERFULLCONTENT), read
/// in device pixels from the top-left corner of one of its elements.
/// </summary>
internal sealed class ScreenPixels
{
    private const uint PwRenderFullContent = 2;

    private readonly byte[] _pixels;
    private readonly int _originX;
    private readonly int _originY;

    private ScreenPixels(byte[] pixels, int width, int height, int originX, int originY)
    {
        _pixels = pixels;
        Width = width;
        Height = height;
        _originX = originX;
        _originY = originY;
    }

    public int Width { get; }

    public int Height { get; }

    /// <summary>Captures <paramref name="window"/>; coordinates count from <paramref name="origin"/>'s top-left corner.</summary>
    public static ScreenPixels Capture(Window window, UIElement origin)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (!GetWindowRect(hwnd, out var rect)) throw new InvalidOperationException("GetWindowRect failed.");
        var width = rect.Right - rect.Left;
        var height = rect.Bottom - rect.Top;
        var screen = GetDC(IntPtr.Zero);
        var memory = CreateCompatibleDC(screen);
        var info = new BitmapInfo { Size = 40, Width = width, Height = -height, Planes = 1, BitCount = 32 };
        var bitmap = CreateDIBSection(memory, ref info, 0, out var bits, IntPtr.Zero, 0);
        var previous = SelectObject(memory, bitmap);
        try
        {
            if (!PrintWindow(hwnd, memory, PwRenderFullContent)) throw new InvalidOperationException("PrintWindow failed.");
            var pixels = new byte[width * 4 * height];
            Marshal.Copy(bits, pixels, 0, pixels.Length);
            var corner = origin.PointToScreen(default);
            return new ScreenPixels(pixels, width, height, (int)Math.Round(corner.X) - rect.Left, (int)Math.Round(corner.Y) - rect.Top);
        }
        finally
        {
            SelectObject(memory, previous);
            DeleteObject(bitmap);
            DeleteDC(memory);
            _ = ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>
    /// Captures <paramref name="window"/> once <paramref name="drawn"/> holds for the capture, waiting up to
    /// <paramref name="seconds"/>: on a runner without a GPU, WPF draws heavy effects in software and the first frames
    /// of a large window can take seconds to reach the screen (until then the capture is black). Null if it never does.
    /// </summary>
    public static ScreenPixels? CaptureWhen(Window window, UIElement origin, Func<ScreenPixels, bool> drawn, double seconds)
    {
        var deadline = DateTime.UtcNow.AddSeconds(seconds);
        while (true)
        {
            var capture = Capture(window, origin);
            if (drawn(capture)) return capture;
            if (DateTime.UtcNow > deadline) return null;
            Sta.Wait(0.5);
        }
    }

    /// <summary>The color at device pixel (<paramref name="x"/>, <paramref name="y"/>) from the origin.</summary>
    public (byte R, byte G, byte B) Rgb(int x, int y)
    {
        var px = _originX + x;
        var py = _originY + y;
        if (px < 0 || py < 0 || px >= Width || py >= Height) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the capture");
        var i = (py * Width + px) * 4;
        return (_pixels[i + 2], _pixels[i + 1], _pixels[i]);
    }

    /// <summary>The green channel at device pixel (<paramref name="x"/>, <paramref name="y"/>) from the origin.</summary>
    public double Green(int x, int y)
    {
        var px = _originX + x;
        var py = _originY + y;
        if (px < 0 || py < 0 || px >= Width || py >= Height) throw new ArgumentOutOfRangeException(nameof(x), $"({x}, {y}) is outside the capture");
        return _pixels[(py * Width + px) * 4 + 1];
    }

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
