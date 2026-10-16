using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Threading;

namespace LiquidGlass.Wpf.Tests;

/// <summary>Runs WPF test code on its own STA thread with a dispatcher.</summary>
internal static class Sta
{
    public static void Run(Action body)
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                body();
            }
            catch (Exception e)
            {
                failure = e;
            }
            finally
            {
                Dispatcher.CurrentDispatcher.InvokeShutdown();
            }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (failure is not null) ExceptionDispatchInfo.Capture(failure).Throw();
    }

    /// <summary>Lets layout, loading and rendering run to completion.</summary>
    public static void Pump()
    {
        for (var i = 0; i < 3; i++)
        {
            var frame = new DispatcherFrame();
            Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => frame.Continue = false));
            Dispatcher.PushFrame(frame);
        }
    }

    /// <summary>Shows <paramref name="content"/> in a borderless window and waits until it is loaded.</summary>
    public static Window Show(UIElement content, double width = 640, double height = 420)
    {
        var window = new Window
        {
            Content = content,
            Width = width,
            Height = height,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = 20,
            Top = 20,
        };
        window.Show();
        Pump();
        return window;
    }
}
