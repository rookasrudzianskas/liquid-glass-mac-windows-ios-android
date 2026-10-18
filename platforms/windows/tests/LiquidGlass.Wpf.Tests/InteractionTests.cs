using System;
using System.Collections.Generic;
using System.Diagnostics;
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

/// <summary>
/// The press response in a live window: springs driven by the frame loop, applied as transforms. Tests that follow a
/// spring through its frames advance it by one fixed step per frame (<see cref="GlassFrameLoop.FixedStep"/>) and sample
/// it after each step (<see cref="GlassFrameLoop.AfterTick"/>), so a loaded runner that delays or drops frames cannot
/// carry a spring past what they check; they wait for the animation to come to rest, not for a set time.
/// </summary>
public class InteractionTests
{
    private const double Step = 1.0 / 60;

    [Fact]
    public void PressGrowsAndReleaseSettlesWithOvershoot()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            GlassFrameLoop.FixedStep = Step;
            var presses = new List<double>();
            var areas = new List<double>();
            Action? sample = null;
            try
            {
                var button = new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
                scene.Children.Add(button);
                var window = Sta.Show(scene, 400, 200);
                var surface = (GlassSurface)button.Template.FindName("PART_Surface", button);
                var root = (FrameworkElement)surface.Template.FindName("PART_Root", surface);
                var interaction = surface.Interaction!;
                // The press spring itself, and the area scale it gives: the press grows both axes by (1 + grow); any
                // stretch toward the (real) cursor only adds to the area, because stretch preserves volume
                // (sx · sy = (1 + grow)² · √((1 + ex)(1 + ey))).
                sample = () =>
                {
                    presses.Add(interaction.PressAmount);
                    var scale = ((TransformGroup)root.RenderTransform).Children.OfType<ScaleTransform>().Single();
                    areas.Add(scale.ScaleX * scale.ScaleY);
                };
                GlassFrameLoop.AfterTick(sample);

                presses.Clear();
                areas.Clear();
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                // 0.45 s of spring time: the press spring (0.24 s) has reached full.
                Assert.True(Sta.WaitUntil(() => presses.Count >= 27), $"the press advanced {presses.Count} frames");
                Assert.True(surface.IsPressed);
                // grow = min(0.16, 8 / 120) = 0.0667 at full press (OPTICS §6), so the area grows by at least 1.138.
                Assert.True(areas.Last() > 1.13, $"area scale {areas.Last()}");

                presses.Clear();
                areas.Clear();
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                Assert.True(Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(interaction)), "the release should settle and stop the frame loop");
                Assert.False(surface.IsPressed);
                // The release spring (0.5 s, bounce 0.42) carries the press about 10% past rest over several frames
                // (shrinking the area by about 1.4%, less any stretch), then settles exactly at rest: about 1.4 s.
                Assert.True(presses.Min() < -0.08, $"no overshoot: the press reached {presses.Min():F4}");
                Assert.True(presses.Count(p => p < -0.02) >= 5, $"the overshoot lasted {presses.Count(p => p < -0.02)} frames");
                Assert.InRange(presses.Count, 40, 240);
                Assert.Equal(0, presses.Last());
                Assert.Equal(1, areas.Last(), 6);
                window.Close();
            }
            finally
            {
                if (sample is not null) GlassFrameLoop.RemoveAfterTick(sample);
                GlassFrameLoop.FixedStep = null;
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    /// <summary>A press drives the light on the glow spring (0.42 s, no bounce): it rises to full and never passes it.</summary>
    [Fact]
    public void PressLightRisesOnTheGlowSpringWithoutOvershoot()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            GlassFrameLoop.FixedStep = Step;
            var glow = new List<double>();
            Action? sample = null;
            try
            {
                var button = new GlassButton { Content = "Press", Width = 120, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
                var scene = new GlassScene { Content = new Border { Background = Brushes.SteelBlue } };
                scene.Children.Add(button);
                var window = Sta.Show(scene, 400, 200);
                var lens = ((GlassSurface)button.Template.FindName("PART_Surface", button)).Lens!;
                sample = () => glow.Add(lens.GlowAmount);
                GlassFrameLoop.AfterTick(sample);
                glow.Clear();
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonDownEvent });
                // 1.2 s of spring time, held down.
                Assert.True(Sta.WaitUntil(() => glow.Count >= 72), $"the light advanced {glow.Count} frames");
                Assert.True(glow.Count(g => g > 0.05 && g < 0.95) >= 2, "the light should rise over several frames");
                Assert.True(glow.Max() <= 1 + 1e-9, $"the light overshot: {glow.Max():F4}");
                Assert.True(glow.Last() > 0.99, $"the light should reach full: {glow.Last():F4}");
                button.RaiseEvent(new MouseButtonEventArgs(Mouse.PrimaryDevice, Environment.TickCount, MouseButton.Left) { RoutedEvent = UIElement.MouseLeftButtonUpEvent });
                window.Close();
            }
            finally
            {
                if (sample is not null) GlassFrameLoop.RemoveAfterTick(sample);
                GlassFrameLoop.FixedStep = null;
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
            GlassFrameLoop.FixedStep = Step;
            var lefts = new List<double>();
            Action? sample = null;
            try
            {
                var tabs = new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" }, new GlassTabBarItem { Content = "Three" } }, SelectedIndex = 0 };
                var window = Sta.Show(new GlassScene { Content = new Border { Background = Brushes.Teal }, Children = { tabs } }, 420, 200);
                Wait(0.3);
                Assert.True(Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(tabs)), "the tab lens should come to rest on the first item");
                var lens = (GlassSurface)tabs.Template.FindName("PART_Lens", tabs);
                var host = (Canvas)tabs.Template.FindName("PART_LensHost", tabs);
                var third = (GlassTabBarItem)tabs.ItemContainerGenerator.ContainerFromIndex(2);
                sample = () => lefts.Add(Canvas.GetLeft(lens));
                GlassFrameLoop.AfterTick(sample);
                tabs.SelectedIndex = 2;
                Assert.True(Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(tabs)), "the tab lens should come to rest on the selection");
                var target = third.TransformToVisual(host).Transform(default).X;
                // Three frames (0.05 s) into the slide the lens is still on its way; then it lands on the selection.
                Assert.True(lefts.Count >= 3, $"the slide took {lefts.Count} frames");
                Assert.True(lefts[2] < target - 1, $"the lens should still be travelling: {lefts[2]:F2} of {target:F2}");
                Assert.Equal(target, Canvas.GetLeft(lens), 1);
                Assert.Equal(third.ActualWidth, lens.Width, 1);
                window.Close();
            }
            finally
            {
                if (sample is not null) GlassFrameLoop.RemoveAfterTick(sample);
                GlassFrameLoop.FixedStep = null;
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    /// <summary>
    /// Reduced motion keeps transitions, critically damped, without stretch or overshoot (only <see cref="GlassMotion.None"/>
    /// removes them): the tab lens slides, the switch thumb travels, the menu panel grows out of its trigger.
    /// </summary>
    [Fact]
    public void ReducedMotionKeepsTransitionsWithoutOvershoot()
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            GlassFrameLoop.FixedStep = Step;
            try
            {
                var tabs = new GlassTabBar
                {
                    Motion = GlassMotion.Reduced,
                    Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" }, new GlassTabBarItem { Content = "Three" } },
                    SelectedIndex = 0,
                    HorizontalAlignment = HorizontalAlignment.Left,
                };
                var toggle = new GlassSwitch { Content = "Wi-Fi", HorizontalAlignment = HorizontalAlignment.Left };
                GlassScene.SetMotion(toggle, GlassMotion.Reduced);
                var menu = new GlassMenu { Header = "Sort", Motion = GlassMotion.Reduced, HorizontalAlignment = HorizontalAlignment.Left, Items = { new GlassMenuItem { Header = "Name" }, new GlassMenuItem { Header = "Size" } } };
                var scene = new GlassScene { Content = new Border { Background = Brushes.Teal }, Children = { new StackPanel { Children = { tabs, toggle, menu } } } };
                var window = Sta.Show(scene, 420, 360);
                Wait(0.3);
                Assert.True(Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(tabs) && !GlassFrameLoop.IsRunning(toggle)), "the controls should come to rest");

                var lens = (GlassSurface)tabs.Template.FindName("PART_Lens", tabs);
                var host = (Canvas)tabs.Template.FindName("PART_LensHost", tabs);
                var third = (GlassTabBarItem)tabs.ItemContainerGenerator.ContainerFromIndex(2);
                var start = Canvas.GetLeft(lens);
                var target = third.TransformToVisual(host).Transform(default).X;
                var thumb = (GlassValueThumb)toggle.Template.FindName("PART_Thumb", toggle);
                var lefts = new List<double>();
                var stretches = new List<double>();
                var offsets = new List<double>();
                var widths = new List<double>();
                // Each transition is watched on its own, after every step of its spring until it comes to rest.
                void Watch(IGlassAnimation animation, Action change, Action sample)
                {
                    GlassFrameLoop.AfterTick(sample);
                    try
                    {
                        change();
                        Assert.True(Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(animation)), $"{animation.GetType().Name} should come to rest");
                    }
                    finally
                    {
                        GlassFrameLoop.RemoveAfterTick(sample);
                    }
                }
                Watch(tabs, () => tabs.SelectedIndex = 2, () =>
                {
                    lefts.Add(Canvas.GetLeft(lens));
                    stretches.Add(((ScaleTransform)lens.RenderTransform).ScaleX);
                });
                Watch(toggle, () => toggle.IsChecked = true, () => offsets.Add(((TranslateTransform)thumb.RenderTransform).X));
                Watch(menu, () => menu.IsOpen = true, () =>
                {
                    if (menu.Panel?.Lens is { } panelLens) widths.Add(panelLens.ShapeBounds.Width);
                });

                Assert.True(lefts.Count(l => l > start + 1 && l < target - 1) >= 2, "the tab lens should slide through intermediate positions");
                Assert.True(lefts.Max() <= target + 0.05, $"the tab lens overshot: {lefts.Max():F2} past {target:F2}");
                Assert.Equal(target, Canvas.GetLeft(lens), 1);
                Assert.All(stretches, s => Assert.Equal(1, s, 6));

                var travel = offsets.Last();
                Assert.True(travel > 1, "the switch thumb should travel");
                Assert.True(offsets.Count(o => o > 0.5 && o < travel - 0.5) >= 2, "the switch thumb should move over several frames");
                Assert.True(offsets.Max() <= travel + 0.05, $"the switch thumb overshot: {offsets.Max():F2} past {travel:F2}");

                // The reduced morph (0.42 s, critically damped) takes about 27 steps to come within a unit of the panel.
                var panelWidth = menu.Panel!.ActualWidth;
                Assert.True(widths.Count(w => w < panelWidth - 1) >= 10, $"the menu panel should grow out of its trigger over several frames: {widths.Count(w => w < panelWidth - 1)}");
                Assert.True(widths.Max() <= panelWidth + 0.05, $"the menu panel overshot: {widths.Max():F2} past {panelWidth:F2}");
                window.Close();
            }
            finally
            {
                GlassFrameLoop.FixedStep = null;
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
                Sta.WaitUntil(() => !GlassFrameLoop.IsRunning(interaction!));
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
        }
        // Nothing the press started may outlive it: the frame loop stops once the release has settled (about 1.3 s, and
        // later on a loaded machine, whose frames arrive late) and the mouse is free.
        if (button is GlassButton glass && glass.Template.FindName("PART_Surface", glass) is GlassSurface { Interaction: { } interaction })
        {
            var settle = Stopwatch.StartNew();
            while (GlassFrameLoop.IsRunning(interaction) && settle.Elapsed < TimeSpan.FromSeconds(8)) Wait(0.1);
            Assert.False(GlassFrameLoop.IsRunning(interaction), $"{variant}: the press is still animating {settle.Elapsed.TotalSeconds:0.0} s after the release");
        }
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
