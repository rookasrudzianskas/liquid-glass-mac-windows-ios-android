using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

/// <summary>The press response in a live window: springs driven by the frame loop, applied as transforms.</summary>
public class InteractionTests
{
    [Fact]
    public void PressGrowsAndReleaseSettlesWithOvershoot()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var button = new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
                scene.Children.Add(button);
                var window = Sta.Show(scene, 400, 200);
                var surface = (GlassSurface)button.Template.FindName("PART_Surface", button);
                var root = (FrameworkElement)surface.Template.FindName("PART_Root", surface);
                // Area scale: the press grows both axes by (1 + grow); any stretch toward the (real) cursor only adds
                // to it, because stretch preserves volume (sx · sy = (1 + grow)² · √((1 + ex)(1 + ey))).
                var scales = new List<double>();
                EventHandler sample = (_, _) =>
                {
                    var scale = ((TransformGroup)root.RenderTransform).Children.OfType<ScaleTransform>().Single();
                    scales.Add(scale.ScaleX * scale.ScaleY);
                };
                CompositionTarget.Rendering += sample;

                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                Wait(0.45);
                Assert.True(surface.IsPressed);
                // grow = min(0.16, 8 / 120) = 0.0667 at full press (OPTICS §6), so the area grows by at least 1.138.
                Assert.True(scales.Last() > 1.13, $"area scale {scales.Last()}");

                scales.Clear();
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                // The release spring decays as exp(−7.3 t); it is within its settle threshold and snaps to rest by 1.4 s.
                Wait(2.0);
                Assert.False(surface.IsPressed);
                // Without overshoot the area never drops below 1 (grow and stretch are never negative); the release
                // spring (bounce 0.42) carries the press about 11% past rest, shrinking the area by about 1.4%.
                Assert.True(scales.Min() < 0.998, $"no overshoot: min {scales.Min()}");
                Assert.Equal(1, scales.Last(), 6);
                CompositionTarget.Rendering -= sample;
                window.Close();
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    [Fact]
    public void ReducedMotionPressesWithoutGrowing()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var button = new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                GlassScene.SetMotion(button, GlassMotion.Reduced);
                var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
                scene.Children.Add(button);
                var window = Sta.Show(scene, 400, 200);
                var surface = (GlassSurface)button.Template.FindName("PART_Surface", button);
                var root = (FrameworkElement)surface.Template.FindName("PART_Root", surface);
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                Wait(0.45);
                Assert.True(surface.IsPressed);
                Assert.Equal(1, ((TransformGroup)root.RenderTransform).Children.OfType<ScaleTransform>().Single().ScaleX, 6);
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                window.Close();
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    [Fact]
    public void TabLensSlidesToTheSelection()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var tabs = new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" }, new GlassTabBarItem { Content = "Three" } }, SelectedIndex = 0 };
                var window = Sta.Show(new GlassScene { Content = new Border { Background = Brushes.Teal }, Children = { tabs } }, 420, 200);
                Wait(0.3);
                var lens = (GlassSurface)tabs.Template.FindName("PART_Lens", tabs);
                var third = (GlassTabBarItem)tabs.ItemContainerGenerator.ContainerFromIndex(2);
                tabs.SelectedIndex = 2;
                Wait(0.05);
                var host = (Canvas)tabs.Template.FindName("PART_LensHost", tabs);
                var target = third.TransformToVisual(host).Transform(default).X;
                Assert.True(Canvas.GetLeft(lens) < target - 1, "the lens should still be travelling");
                Wait(0.6);
                Assert.Equal(target, Canvas.GetLeft(lens), 1);
                Assert.Equal(third.ActualWidth, lens.Width, 1);
                window.Close();
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    [Fact]
    public void LosingTheMouseCaptureReleasesAPress()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var button = new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
                scene.Children.Add(button);
                var window = Sta.Show(scene, 400, 200);
                var surface = (GlassSurface)button.Template.FindName("PART_Surface", button);
                var interaction = surface.Interaction;
                Assert.NotNull(interaction);

                // A button captures the mouse itself before the glass sees the press (ButtonBase does on a real press).
                Assert.True(button.CaptureMouse(), "the test needs the mouse capture");
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                Wait(0.3);
                Assert.True(surface.IsPressed);

                // The capture goes (the window was deactivated, another element took the mouse) and no button-up follows.
                button.ReleaseMouseCapture();
                Wait(2.0);
                Assert.False(surface.IsPressed);
                Assert.False(GlassFrameLoop.IsRunning(interaction!), "the press should settle and stop the frame loop");
                window.Close();
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    /// <summary>
    /// A closed window is collected after a press on glass in it, as it is after a press on a plain button and after
    /// no press at all (the baselines, which rule out WPF's own input state). On failure the message names the chain
    /// of references that keeps the window alive.
    /// </summary>
    [Theory]
    [InlineData("glass button, pressed")]
    [InlineData("glass button, never pressed")]
    [InlineData("plain button in a scene, pressed")]
    public void APressDoesNotKeepAClosedWindowAlive(string variant)
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var (window, press) = PressAndClose(variant);
                for (var i = 0; i < 3; i++)
                {
                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                    GC.Collect();
                    Sta.Pump();
                }
                Assert.False(press is { IsAlive: true }, $"{variant}: the press event is still referenced after the window closed");
                if (window.IsAlive) Assert.Fail($"{variant}: the closed window is still referenced{GcRoots.PathTo(typeof(LeakProbeWindow))}");
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private static (WeakReference Window, WeakReference? Press) PressAndClose(string variant)
    {
        ButtonBase button = variant.StartsWith("plain", StringComparison.Ordinal)
            ? new Button { Content = "Press", Width = 120, Height = 32, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
            : new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
        var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
        scene.Children.Add(button);
        var window = new LeakProbeWindow
        {
            Content = scene,
            Width = 400,
            Height = 200,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            ShowInTaskbar = false,
            ShowActivated = false,
            Left = 20,
            Top = 20,
        };
        window.Show();
        Sta.Pump();
        MouseButtonEventArgs? down = null;
        if (variant.EndsWith("pressed", StringComparison.Ordinal) && !variant.EndsWith("never pressed", StringComparison.Ordinal))
        {
            down = new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent };
            button.RaiseEvent(down);
            Wait(0.3);
            button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
            // Let the release settle, so the frame loop stops after its last frame.
            Wait(1.6);
        }
        // Nothing the press started may outlive it: the frame loop has stopped and the mouse is free.
        if (button is GlassButton glass && glass.Template.FindName("PART_Surface", glass) is GlassSurface { Interaction: { } interaction })
            Assert.False(GlassFrameLoop.IsRunning(interaction), $"{variant}: the press is still animating");
        Assert.Null(Mouse.Captured);
        Keyboard.ClearFocus();
        window.Close();
        Sta.Pump();
        return (new WeakReference(window), down is null ? null : new WeakReference(down));
    }

    /// <summary>A window type of its own, so the leak check can find exactly this window on the heap.</summary>
    private sealed class LeakProbeWindow : Window
    {
    }

    private static void Wait(double seconds) => Sta.Wait(seconds);
}
