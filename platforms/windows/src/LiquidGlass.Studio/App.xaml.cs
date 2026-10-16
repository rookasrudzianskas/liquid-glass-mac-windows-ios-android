using System;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using LiquidGlass.Studio.Shell;

namespace LiquidGlass.Studio;

/// <summary>Liquid Glass Studio: the Windows demo of Liquid Glass Engineering.</summary>
public partial class App : Application
{
    /// <summary>Where an unexpected error is written before the app closes.</summary>
    public static string CrashLogPath { get; } = Path.Combine(Path.GetTempPath(), "LiquidGlassStudio-crash.txt");

    private bool _automated;

    /// <summary>Creates the app; errors from here on, including while its resources load, reach the crash log.</summary>
    public App()
    {
        AppDomain.CurrentDomain.UnhandledException += (_, args) => Report(args.ExceptionObject as Exception);
    }

    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        base.OnStartup(e);
        var options = StudioOptions.Parse(e.Args);
        // Scripted runs (--exit-after) have no one to dismiss a message box.
        _automated = options.ExitAfter is not null;
        var window = new MainWindow(options);
        MainWindow = window;
        window.Show();
    }

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        Report(e.Exception);
        if (!_automated)
            MessageBox.Show($"Liquid Glass Studio ran into a problem and will close.\n\n{e.Exception.Message}\n\nDetails: {CrashLogPath}",
            "Liquid Glass Studio", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
        Shutdown(1);
    }

    private static void Report(Exception? exception)
    {
        if (exception is null) return;
        try
        {
            File.WriteAllText(CrashLogPath, $"{DateTime.Now:O}\n{exception}\n");
        }
        catch (IOException)
        {
            // Nothing else to do; the message box still explains what happened.
        }
        catch (UnauthorizedAccessException)
        {
            // As above.
        }
    }
}
