using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Automation;

namespace LiquidGlass.CiTools;

/// <summary>
/// Launches Liquid Glass Studio once per page and appearance, captures its window, and drives a few interactions
/// (an open menu, a held press) through UI Automation and synthetic input.
/// </summary>
internal static class StudioCapture
{
    private const string Size = "1280x820";

    public static int Run(string exe, string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var log = new StringBuilder();
        var failures = 0;
        var shots = new List<(string Name, string Args, Action<IntPtr, AutomationElement>? Interact)>
        {
            ("hero-light", "--page hero --appearance light", null),
            ("hero-dark", "--page hero --appearance dark", null),
            ("controls-light", "--page controls --appearance light", null),
            ("controls-dark", "--page controls --appearance dark", null),
            ("materials-light", "--page materials --appearance light", null),
            ("materials-dark", "--page materials --appearance dark", null),
            ("blur-light", "--page blur --appearance light", null),
            ("blur-dark", "--page blur --appearance dark", null),
            ("hero-fallback-light", "--page hero --appearance light --render-mode fallback", null),
            ("controls-fallback-dark", "--page controls --appearance dark --render-mode fallback", null),
            ("controls-menu-light", "--page controls --appearance light", (_, root) => Expand(root, "Sort")),
            ("hero-press-light", "--page hero --appearance light", (hwnd, root) => Press(root, "Liquid Glass card")),
        };
        foreach (var (name, args, interact) in shots)
        {
            try
            {
                var (width, height) = Capture(exe, $"{args} --size {Size} --exit-after 60", Path.Combine(outDirectory, name + ".png"), interact);
                log.AppendLine($"{name}: {width}x{height}");
            }
            catch (Exception e)
            {
                failures++;
                log.AppendLine($"{name}: FAILED {e.Message}");
            }
        }
        File.WriteAllText(Path.Combine(outDirectory, "studio.txt"), log.ToString());
        Console.WriteLine(log);
        return failures == 0 ? 0 : 1;
    }

    private static (int, int) Capture(string exe, string args, string path, Action<IntPtr, AutomationElement>? interact)
    {
        using var process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false })
            ?? throw new InvalidOperationException("could not start Studio");
        try
        {
            var hwnd = WaitForWindow(process, TimeSpan.FromSeconds(30));
            SetForegroundWindow(hwnd);
            // Fonts, wallpapers and the first shader frames settle.
            Thread.Sleep(3500);
            if (interact is not null)
            {
                interact(hwnd, AutomationElement.FromHandle(hwnd));
                Thread.Sleep(900);
            }
            var size = WindowCapture.SavePng(hwnd, path);
            if (interact is not null) ReleaseMouse();
            return size;
        }
        finally
        {
            if (!process.HasExited)
            {
                process.CloseMainWindow();
                if (!process.WaitForExit(5000)) process.Kill();
            }
        }
    }

    private static IntPtr WaitForWindow(Process process, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            process.Refresh();
            if (process.HasExited) throw new InvalidOperationException($"Studio exited with code {process.ExitCode}");
            if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
            Thread.Sleep(200);
        }
        throw new TimeoutException("Studio did not show a window");
    }

    private static AutomationElement Find(AutomationElement root, string name)
        => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name))
           ?? throw new InvalidOperationException($"no element named '{name}'");

    private static void Expand(AutomationElement root, string name)
    {
        var element = Find(root, name);
        ((ExpandCollapsePattern)element.GetCurrentPattern(ExpandCollapsePattern.Pattern)).Expand();
    }

    /// <summary>Holds the left button down over the center of an element (released after the capture).</summary>
    private static void Press(AutomationElement root, string name)
    {
        var bounds = Find(root, name).Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width * 0.62);
        var y = (int)(bounds.Top + bounds.Height * 0.22);
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
    }

    private static void ReleaseMouse() => mouse_event(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);

    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}
