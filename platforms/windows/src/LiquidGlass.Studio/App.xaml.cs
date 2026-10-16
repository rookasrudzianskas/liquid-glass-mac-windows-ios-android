using System.Windows;
using LiquidGlass.Studio.Shell;

namespace LiquidGlass.Studio;

/// <summary>Liquid Glass Studio: the Windows demo of Liquid Glass Engineering.</summary>
public partial class App : Application
{
    /// <inheritdoc />
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var window = new MainWindow(StudioOptions.Parse(e.Args));
        MainWindow = window;
        window.Show();
    }
}
