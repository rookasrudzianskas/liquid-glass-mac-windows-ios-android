using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Windows.Automation;
using Microsoft.Win32;

namespace LiquidGlass.CiTools;

/// <summary>
/// Launches the Hello Glass example (examples/wpf/HelloGlass) in the light Windows app mode, checks that its controls
/// carry their accessible names and that no image reaches UI Automation without one (the wallpaper is decorative), and
/// captures its window three times: as it opens (the frosted card; the README's screenshot.png), with the card turned
/// Clear by its "Frosted" switch, and dark after the toolbar's appearance button (screenshot-dark.png). Then "Get
/// started" must select the Explore tab and the toolbar's profile button the Settings tab, and the window must close
/// cleanly.
/// </summary>
internal static class ExampleCapture
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private const string AppsUseLightTheme = "AppsUseLightTheme";

    private static readonly string[] Names =
        { "Switch appearance", "Profile", "Get started", "Learn more", "Frosted", "Sections", "Home", "Explore", "Settings" };

    // The card's own name; its title text has the same, so the card is found as a GlassSurface.
    private const string CardName = "Hello, Glass";

    public static int Run(string exe, string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var log = new StringBuilder();
        var failed = false;
        // The example follows the Windows app mode, so the first capture is light whatever the runner's setting.
        var previous = Registry.GetValue($@"HKEY_CURRENT_USER\{PersonalizeKey}", AppsUseLightTheme, null);
        Registry.SetValue($@"HKEY_CURRENT_USER\{PersonalizeKey}", AppsUseLightTheme, 1, RegistryValueKind.DWord);
        try
        {
            using var process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = false })
                ?? throw new InvalidOperationException($"could not start {exe}");
            try
            {
                var hwnd = StudioCapture.WaitForWindow(process, TimeSpan.FromSeconds(30));
                StudioCapture.SetForegroundWindow(hwnd);
                // Fonts, the wallpaper and the first shader frames settle.
                Thread.Sleep(3500);
                var root = AutomationElement.FromHandle(hwnd);
                foreach (var name in Names) StudioCapture.Find(root, name);
                var card = new AndCondition(
                    new PropertyCondition(AutomationElement.NameProperty, CardName),
                    new PropertyCondition(AutomationElement.ClassNameProperty, "GlassSurface"));
                if (root.FindFirst(TreeScope.Descendants, card) is null) throw new InvalidOperationException($"no GlassSurface named '{CardName}'");
                log.AppendLine($"accessible names present: card '{CardName}', '{string.Join("', '", Names)}'");
                var images = root.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Image));
                foreach (AutomationElement image in images)
                    if (string.IsNullOrEmpty(image.Current.Name)) throw new InvalidOperationException($"an image without a name ({image.Current.ClassName}) is a control element");
                log.AppendLine($"images in the control view: {images.Count}, none without a name");
                var unnamed = new List<string>();
                foreach (AutomationElement element in root.FindAll(TreeScope.Descendants, Condition.TrueCondition))
                    if (string.IsNullOrEmpty(element.Current.Name)) unnamed.Add($"{element.Current.LocalizedControlType} ({element.Current.ClassName})");
                log.AppendLine($"control elements without a name: {(unnamed.Count == 0 ? "none" : string.Join(", ", unnamed))}");

                Save(hwnd, outDirectory, "hello-glass-light", log);

                var frosted = (TogglePattern)StudioCapture.Find(root, "Frosted").GetCurrentPattern(TogglePattern.Pattern);
                frosted.Toggle();
                Thread.Sleep(900);
                log.AppendLine($"Frosted toggled {frosted.Current.ToggleState}");
                Save(hwnd, outDirectory, "hello-glass-clear-light", log);
                frosted.Toggle();

                Invoke(root, "Switch appearance");
                Thread.Sleep(900);
                Save(hwnd, outDirectory, "hello-glass-dark", log);

                ExpectTab(root, "Get started", "Explore", log);
                ExpectTab(root, "Profile", "Settings", log);
            }
            finally
            {
                if (!process.HasExited)
                {
                    process.CloseMainWindow();
                    if (!process.WaitForExit(5000)) process.Kill();
                }
            }
            process.WaitForExit();
            log.AppendLine($"closed with exit code {process.ExitCode}");
            if (process.ExitCode != 0) failed = true;
        }
        catch (Exception e)
        {
            failed = true;
            log.AppendLine($"FAILED {e.Message}");
        }
        finally
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey, writable: true);
            if (previous is null) key?.DeleteValue(AppsUseLightTheme, throwOnMissingValue: false);
            else key?.SetValue(AppsUseLightTheme, previous, RegistryValueKind.DWord);
        }
        File.WriteAllText(Path.Combine(outDirectory, "example.txt"), log.ToString());
        Console.WriteLine(log);
        return failed ? 1 : 0;
    }

    private static void Save(IntPtr hwnd, string outDirectory, string name, StringBuilder log)
    {
        var (width, height) = WindowCapture.SavePng(hwnd, Path.Combine(outDirectory, name + ".png"));
        log.AppendLine($"{name}: {width}x{height}");
    }

    private static void Invoke(AutomationElement root, string name)
        => ((InvokePattern)StudioCapture.Find(root, name).GetCurrentPattern(InvokePattern.Pattern)).Invoke();

    /// <summary>Invokes the button <paramref name="button"/>; the tab <paramref name="tab"/> must then be selected.</summary>
    private static void ExpectTab(AutomationElement root, string button, string tab, StringBuilder log)
    {
        Invoke(root, button);
        Thread.Sleep(900);
        var item = (SelectionItemPattern)StudioCapture.Find(root, tab).GetCurrentPattern(SelectionItemPattern.Pattern);
        if (!item.Current.IsSelected) throw new InvalidOperationException($"{button} did not select the {tab} tab");
        log.AppendLine($"{button} selects the {tab} tab");
    }
}
