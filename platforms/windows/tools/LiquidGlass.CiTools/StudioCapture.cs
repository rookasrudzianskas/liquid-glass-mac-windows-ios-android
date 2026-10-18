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
/// through UI Automation and synthetic input: an open menu (checking that the focused trigger carries the menu's
/// ExpandCollapse state), a held press, increased contrast, a keyboard focus ring, the first Tab stop, and the Controls
/// page under a Windows contrast theme.
/// </summary>
internal static class StudioCapture
{
    private const string Size = "1280x820";

    private sealed record Shot(string Name, string Args, Func<IntPtr, AutomationElement, string?>? Interact = null, bool Contrast = false);

    public static int Run(string exe, string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var log = new StringBuilder();
        var failures = 0;
        var shots = new List<Shot>
        {
            new("hero-light", "--page hero --appearance light"),
            new("hero-dark", "--page hero --appearance dark"),
            new("controls-light", "--page controls --appearance light"),
            new("controls-dark", "--page controls --appearance dark"),
            new("materials-light", "--page materials --appearance light"),
            new("materials-dark", "--page materials --appearance dark"),
            new("blur-light", "--page blur --appearance light"),
            new("blur-dark", "--page blur --appearance dark"),
            new("hero-fallback-light", "--page hero --appearance light --render-mode fallback"),
            new("controls-fallback-dark", "--page controls --appearance dark --render-mode fallback"),
            new("controls-menu-light", "--page controls --appearance light", (_, root) => OpenFromFocusedTrigger(root, "Sort by name")),
            // Menus mix their glass toward the legibility backing: in dark appearance too, and in the fallback material.
            new("controls-menu-dark", "--page controls --appearance dark", (_, root) => OpenFromFocusedTrigger(root, "Sort by name")),
            new("controls-menu-fallback-dark", "--page controls --appearance dark --render-mode fallback", (_, root) => OpenFromFocusedTrigger(root, "Sort by name")),
            new("hero-press-light", "--page hero --appearance light", (_, root) => Press(root, "Liquid Glass card")),
            new("materials-contrast-light", "--page materials --appearance light", (_, root) => Toggle(root, "Increase contrast")),
            new("hero-focus-dark", "--page hero --appearance dark", (_, root) => FocusByKeyboard(root, "Back", VirtualKeyRight)),
            new("hero-first-tab-light", "--page hero --appearance light", (_, _) => FirstTabStop()),
            new("controls-contrast", "--page controls --appearance light", Contrast: true),
            new("controls-menu-contrast", "--page controls --appearance light", (_, root) => FocusByKeyboard(root, "Sort by name", VirtualKeyDown), Contrast: true),
        };
        foreach (var shot in shots)
        {
            var contrastOn = false;
            try
            {
                if (shot.Contrast)
                {
                    contrastOn = HighContrast.TrySet(true, out var note);
                    log.AppendLine($"{shot.Name}: contrast theme {(contrastOn ? "on" : "could not be turned on")} ({note})");
                    if (!contrastOn) continue;
                }
                var (width, height, result) = Capture(exe, $"{shot.Args} --size {Size} --exit-after 60", Path.Combine(outDirectory, shot.Name + ".png"), shot.Interact);
                log.AppendLine($"{shot.Name}: {width}x{height}{(result is null ? "" : "; " + result)}");
            }
            catch (Exception e)
            {
                failures++;
                log.AppendLine($"{shot.Name}: FAILED {e.Message}");
                var crash = Path.Combine(Path.GetTempPath(), "LiquidGlassStudio-crash.txt");
                if (File.Exists(crash))
                {
                    log.AppendLine(File.ReadAllText(crash));
                    File.Delete(crash);
                }
            }
            finally
            {
                if (contrastOn) HighContrast.TrySet(false, out _);
            }
        }
        File.WriteAllText(Path.Combine(outDirectory, "studio.txt"), log.ToString());
        Console.WriteLine(log);
        return failures == 0 ? 0 : 1;
    }

    private static (int, int, string?) Capture(string exe, string args, string path, Func<IntPtr, AutomationElement, string?>? interact)
    {
        using var process = Process.Start(new ProcessStartInfo(exe, args) { UseShellExecute = false, RedirectStandardError = true })
            ?? throw new InvalidOperationException("could not start Studio");
        var errors = new StringBuilder();
        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data is not null) lock (errors) errors.AppendLine(e.Data);
        };
        process.BeginErrorReadLine();
        try
        {
            IntPtr hwnd;
            try
            {
                hwnd = WaitForWindow(process, TimeSpan.FromSeconds(30));
            }
            catch (InvalidOperationException e)
            {
                process.WaitForExit(2000);
                lock (errors) throw new InvalidOperationException($"{e.Message}\n{errors}");
            }
            SetForegroundWindow(hwnd);
            // Fonts, wallpapers and the first shader frames settle.
            Thread.Sleep(3500);
            string? result = null;
            if (interact is not null)
            {
                result = interact(hwnd, AutomationElement.FromHandle(hwnd));
                Thread.Sleep(900);
            }
            var (width, height) = WindowCapture.SavePng(hwnd, path);
            if (interact is not null) ReleaseMouse();
            return (width, height, result);
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

    /// <summary>The process's main window, once it shows one.</summary>
    internal static IntPtr WaitForWindow(Process process, TimeSpan timeout)
    {
        var watch = Stopwatch.StartNew();
        while (watch.Elapsed < timeout)
        {
            process.Refresh();
            if (process.HasExited) throw new InvalidOperationException($"{Path.GetFileName(process.StartInfo.FileName)} exited with code {process.ExitCode}");
            if (process.MainWindowHandle != IntPtr.Zero) return process.MainWindowHandle;
            Thread.Sleep(200);
        }
        throw new TimeoutException($"{Path.GetFileName(process.StartInfo.FileName)} did not show a window");
    }

    internal static AutomationElement Find(AutomationElement root, string name)
        => root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name))
           ?? throw new InvalidOperationException($"no element named '{name}'");

    /// <summary>
    /// Focuses the menu's trigger, as Tab does, and opens the menu through the focused element's own ExpandCollapse
    /// pattern: what a screen reader reads and uses is the element that has focus.
    /// </summary>
    private static string OpenFromFocusedTrigger(AutomationElement root, string name)
    {
        Find(root, name).SetFocus();
        Thread.Sleep(300);
        var focused = AutomationElement.FocusedElement;
        var description = $"focused '{focused.Current.Name}' ({focused.Current.LocalizedControlType})";
        if (!focused.TryGetCurrentPattern(ExpandCollapsePattern.Pattern, out var found))
            throw new InvalidOperationException($"{description} has no ExpandCollapse pattern");
        var pattern = (ExpandCollapsePattern)found;
        var before = pattern.Current.ExpandCollapseState;
        pattern.Expand();
        // Wait until the panel and its items are in the tree (a loaded runner can take a while), then let the panel
        // finish growing out of its trigger, so the shot shows it open rather than a fixed delay that may fall short.
        AutomationElementCollection menus;
        var items = 0;
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);
        do
        {
            Thread.Sleep(100);
            menus = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Menu));
            items = menus.Count > 0 ? menus[0].FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.MenuItem)).Count : 0;
        }
        while (items == 0 && DateTime.UtcNow < deadline);
        Thread.Sleep(600);
        var after = pattern.Current.ExpandCollapseState;
        if (after != ExpandCollapseState.Expanded) throw new InvalidOperationException($"{description}: {after} after Expand");
        if (items == 0) throw new InvalidOperationException($"{description}: Expanded, but no menu items appeared");
        var buttons = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.NameProperty, name)).Count;
        return $"{description}, ExpandCollapse {before} -> {after}; {menus.Count} menu with {items} items; {buttons} element(s) named '{name}'";
    }

    /// <summary>Flips a toggle (a switch) through UI Automation.</summary>
    private static string Toggle(AutomationElement root, string name)
    {
        var element = Find(root, name);
        var pattern = (TogglePattern)element.GetCurrentPattern(TogglePattern.Pattern);
        pattern.Toggle();
        Thread.Sleep(400);
        return $"'{name}' ({element.Current.LocalizedControlType}) toggled {pattern.Current.ToggleState}";
    }

    /// <summary>Focuses an element, then presses a key, so focus is shown as it is for keyboard users.</summary>
    private static string FocusByKeyboard(AutomationElement root, string name, byte key)
    {
        Find(root, name).SetFocus();
        Thread.Sleep(300);
        PressKey(key);
        Thread.Sleep(600);
        var focused = AutomationElement.FocusedElement;
        return $"after the key, focus is on '{focused.Current.Name}' ({focused.Current.LocalizedControlType})";
    }

    /// <summary>Presses Tab once in the freshly opened window and reports where focus lands.</summary>
    private static string FirstTabStop()
    {
        PressKey(VirtualKeyTab);
        Thread.Sleep(500);
        var focused = AutomationElement.FocusedElement;
        return $"first Tab lands on '{focused.Current.Name}' ({focused.Current.LocalizedControlType})";
    }

    /// <summary>Holds the left button down over the center of an element (released after the capture).</summary>
    private static string? Press(AutomationElement root, string name)
    {
        var bounds = Find(root, name).Current.BoundingRectangle;
        var x = (int)(bounds.Left + bounds.Width * 0.62);
        var y = (int)(bounds.Top + bounds.Height * 0.22);
        SetCursorPos(x, y);
        Thread.Sleep(150);
        mouse_event(MouseLeftDown, 0, 0, 0, UIntPtr.Zero);
        return null;
    }

    private static void ReleaseMouse() => mouse_event(MouseLeftUp, 0, 0, 0, UIntPtr.Zero);

    private static void PressKey(byte key)
    {
        keybd_event(key, 0, 0, UIntPtr.Zero);
        Thread.Sleep(40);
        keybd_event(key, 0, KeyEventKeyUp, UIntPtr.Zero);
    }

    private const uint MouseLeftDown = 0x0002;
    private const uint MouseLeftUp = 0x0004;
    private const uint KeyEventKeyUp = 0x0002;
    private const byte VirtualKeyTab = 0x09;
    private const byte VirtualKeyRight = 0x27;
    private const byte VirtualKeyDown = 0x28;

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte key, byte scan, uint flags, UIntPtr extraInfo);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool SetForegroundWindow(IntPtr hwnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetCursorPos(int x, int y);

    [DllImport("user32.dll")]
    private static extern void mouse_event(uint flags, uint dx, uint dy, uint data, UIntPtr extraInfo);
}

/// <summary>Turns the Windows contrast theme on or off for the captures that need it (best effort on a CI machine).</summary>
internal static class HighContrast
{
    private const uint GetHighContrast = 0x0042;
    private const uint SetHighContrast = 0x0043;
    private const uint SendChange = 0x0002;
    private const int On = 0x0001;

    public static bool TrySet(bool on, out string note)
    {
        try
        {
            var state = new HighContrastInfo { Size = Marshal.SizeOf<HighContrastInfo>() };
            if (!SystemParametersInfo(GetHighContrast, (uint)state.Size, ref state, 0))
            {
                note = $"reading the setting failed ({Marshal.GetLastWin32Error()})";
                return false;
            }
            var scheme = Marshal.PtrToStringUni(state.DefaultScheme);
            state.Flags = on ? state.Flags | On : state.Flags & ~On;
            if (!SystemParametersInfo(SetHighContrast, (uint)state.Size, ref state, SendChange))
            {
                note = $"changing the setting failed ({Marshal.GetLastWin32Error()})";
                return false;
            }
            // Windows applies the theme's colors asynchronously.
            Thread.Sleep(on ? 4000 : 2500);
            var check = new HighContrastInfo { Size = Marshal.SizeOf<HighContrastInfo>() };
            SystemParametersInfo(GetHighContrast, (uint)check.Size, ref check, 0);
            note = $"scheme '{scheme ?? "default"}'";
            return ((check.Flags & On) != 0) == on;
        }
        catch (Exception e) when (e is EntryPointNotFoundException or DllNotFoundException or ExternalException)
        {
            note = e.Message;
            return false;
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct HighContrastInfo
    {
        public int Size;
        public int Flags;
        public IntPtr DefaultScheme;
    }

    [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SystemParametersInfo(uint action, uint parameter, ref HighContrastInfo info, uint winIni);
}
