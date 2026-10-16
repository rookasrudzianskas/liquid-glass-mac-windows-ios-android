using System;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LiquidGlass.CiTools;

/// <summary>Reports how WPF renders on this machine and checks that window capture works.</summary>
internal static class RenderProbe
{
    public static int Run(string outDirectory)
    {
        Directory.CreateDirectory(outDirectory);
        var report = new StringBuilder();
        var app = new Application { ShutdownMode = ShutdownMode.OnMainWindowClose };
        var window = new Window
        {
            Title = "Liquid Glass render probe",
            Width = 480,
            Height = 300,
            WindowStartupLocation = WindowStartupLocation.CenterScreen,
            Background = new LinearGradientBrush(Color.FromRgb(0x5B, 0x2E, 0xD9), Color.FromRgb(0x19, 0xC3, 0xC9), 45),
            Content = new TextBlock
            {
                Text = "Render probe",
                Foreground = Brushes.White,
                FontSize = 32,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
            },
        };
        var exit = 0;
        window.ContentRendered += (_, _) =>
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();
                try
                {
                    var hwnd = new WindowInteropHelper(window).Handle;
                    var source = HwndSource.FromHwnd(hwnd);
                    var dpi = VisualTreeHelper.GetDpi(window);
                    var values = new
                    {
                        tier = RenderCapability.Tier >> 16,
                        pixelShader20 = RenderCapability.IsPixelShaderVersionSupported(2, 0),
                        pixelShader30 = RenderCapability.IsPixelShaderVersionSupported(3, 0),
                        pixelShader20Software = RenderCapability.IsPixelShaderVersionSupportedInSoftware(2, 0),
                        pixelShader30Software = RenderCapability.IsPixelShaderVersionSupportedInSoftware(3, 0),
                        maxPixelShader30Slots = RenderCapability.MaxPixelShaderInstructionSlots(3, 0),
                        maxTextureSize = RenderCapability.MaxHardwareTextureSize.ToString(),
                        processRenderMode = RenderOptions.ProcessRenderMode.ToString(),
                        windowRenderMode = source?.CompositionTarget.RenderMode.ToString(),
                        dpiScale = dpi.DpiScaleX,
                        screen = $"{SystemParameters.PrimaryScreenWidth}x{SystemParameters.PrimaryScreenHeight}",
                        workArea = SystemParameters.WorkArea.ToString(CultureInfo.InvariantCulture),
                        highContrast = SystemParameters.HighContrast,
                        clientAreaAnimation = SystemParameters.ClientAreaAnimation,
                        os = Environment.OSVersion.VersionString,
                        runtime = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
                    };
                    var size = WindowCapture.SavePng(hwnd, Path.Combine(outDirectory, "probe-window.png"));
                    report.Append(JsonSerializer.Serialize(new { values, capture = $"{size.Width}x{size.Height}" }, new JsonSerializerOptions { WriteIndented = true }));
                }
                catch (Exception e)
                {
                    report.Append(e);
                    exit = 1;
                }
                window.Close();
            };
            timer.Start();
        };
        app.Run(window);
        File.WriteAllText(Path.Combine(outDirectory, "render-capability.json"), report.ToString());
        Console.WriteLine(report);
        return exit;
    }
}
