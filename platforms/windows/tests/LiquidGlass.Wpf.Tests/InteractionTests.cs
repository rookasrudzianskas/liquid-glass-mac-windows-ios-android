using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
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
                Wait(1.2);
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

    /// <summary>Runs the dispatcher (and so rendering and the frame loop) for <paramref name="seconds"/>.</summary>
    private static void Wait(double seconds)
    {
        var frame = new DispatcherFrame();
        var timer = new DispatcherTimer(TimeSpan.FromSeconds(seconds), DispatcherPriority.Background, (_, _) => frame.Continue = false, Dispatcher.CurrentDispatcher);
        timer.Start();
        Dispatcher.PushFrame(frame);
        timer.Stop();
    }
}
