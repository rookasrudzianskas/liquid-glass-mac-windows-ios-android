using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiquidGlass.Studio.Shell;

/// <summary>Windows 11 window chrome: the Mica backdrop and a title bar that follows light or dark appearance.</summary>
internal static class WindowBackdrop
{
    private const int UseImmersiveDarkMode = 20;
    private const int SystemBackdropType = 38;
    private const int BackdropMainWindow = 2;

    /// <summary>Whether this Windows draws Mica behind windows that ask for it (Windows 11 22H2 and later).</summary>
    public static bool SupportsMica => Environment.OSVersion.Version.Build >= 22621;

    /// <summary>Asks DWM for Mica behind <paramref name="window"/>; true when the request succeeded.</summary>
    public static bool TryApplyMica(Window window)
    {
        if (!SupportsMica) return false;
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return false;
        var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
        if (DwmExtendFrameIntoClientArea(hwnd, ref margins) != 0) return false;
        var backdrop = BackdropMainWindow;
        if (DwmSetWindowAttribute(hwnd, SystemBackdropType, ref backdrop, sizeof(int)) != 0) return false;
        if (HwndSource.FromHwnd(hwnd)?.CompositionTarget is { } target) target.BackgroundColor = Colors.Transparent;
        return true;
    }

    /// <summary>Draws the title bar and window frame light or dark.</summary>
    public static void SetDarkTitleBar(Window window, bool dark)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == IntPtr.Zero) return;
        var value = dark ? 1 : 0;
        _ = DwmSetWindowAttribute(hwnd, UseImmersiveDarkMode, ref value, sizeof(int));
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left;
        public int Right;
        public int Top;
        public int Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);
}
