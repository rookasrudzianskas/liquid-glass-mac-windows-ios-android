using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

public class ControlTests
{
    private static GlassScene Scene(params UIElement[] children)
    {
        var scene = new GlassScene
        {
            Content = new Border { Background = new LinearGradientBrush(Color.FromRgb(40, 90, 200), Color.FromRgb(240, 120, 60), 0) },
        };
        foreach (var child in children) scene.Children.Add(child);
        return scene;
    }

    [Fact]
    public void ForegroundFollowsAppearanceAndTint()
    {
        Sta.Run(() =>
        {
            var light = new GlassSurface { Appearance = GlassAppearance.Light, Content = new TextBlock(), Width = 100, Height = 40 };
            var dark = new GlassSurface { Appearance = GlassAppearance.Dark, Content = new TextBlock(), Width = 100, Height = 40 };
            var tinted = new GlassSurface { Appearance = GlassAppearance.Light, Tint = Color.FromRgb(0, 122, 255), Content = new TextBlock(), Width = 100, Height = 40 };
            var button = new GlassButton { Appearance = GlassAppearance.Dark, Content = "OK" };
            var panel = new StackPanel { Children = { light, dark, tinted, button } };
            var window = Sta.Show(Scene(panel));
            Assert.Equal(Color.FromRgb(0x11, 0x11, 0x13), ((SolidColorBrush)((TextBlock)light.Content).Foreground).Color);
            Assert.Equal(Colors.White, ((SolidColorBrush)((TextBlock)dark.Content).Foreground).Color);
            Assert.Equal(Colors.White, ((SolidColorBrush)((TextBlock)tinted.Content).Foreground).Color);
            Assert.Equal(Colors.White, ((SolidColorBrush)button.Foreground).Color);
            Assert.True(GlassScene.GetIsDark(dark));
            // A foreground set by the application wins.
            dark.Foreground = Brushes.Orange;
            Assert.Equal(Colors.Orange, ((SolidColorBrush)((TextBlock)dark.Content).Foreground).Color);
            window.Close();
        });
    }

    [Fact]
    public void LensModesFollowTheSceneAndSettings()
    {
        Sta.Run(() =>
        {
            var shader = new GlassSurface { Width = 120, Height = 60 };
            var fallback = new GlassSurface { Width = 120, Height = 60 };
            GlassScene.SetRenderMode(fallback, GlassRenderMode.Fallback);
            var opaque = new GlassSurface { Width = 120, Height = 60 };
            GlassScene.SetReduceTransparency(opaque, true);
            var scene = Scene(new StackPanel { Children = { shader, fallback, opaque } });
            var inBackdrop = new GlassSurface { Width = 120, Height = 60 };
            ((Border)scene.Content!).Child = inBackdrop;
            var outside = new GlassSurface { Width = 120, Height = 60 };
            var window = Sta.Show(new StackPanel { Children = { scene, outside } }, 640, 520);
            scene.Height = 400;
            Sta.Pump();
            var expectShader = GlassDiagnostics.CanUseShader(window) ? GlassLensMode.Shader : GlassLensMode.Fallback;
            Assert.Equal(expectShader, shader.RenderedMode);
            Assert.Equal(GlassLensMode.Fallback, fallback.RenderedMode);
            Assert.Equal(GlassLensMode.Opaque, opaque.RenderedMode);
            Assert.Equal(GlassLensMode.Opaque, inBackdrop.RenderedMode);
            Assert.Equal(GlassLensMode.Opaque, outside.RenderedMode);
            window.Close();
        });
    }

    [Fact]
    public void FallbackRendersTheRimLight()
    {
        Sta.Run(() =>
        {
            var surface = new GlassSurface { Width = 200, Height = 64, Radius = GlassRadius.Capsule, Appearance = GlassAppearance.Light };
            var scene = Scene(surface);
            GlassScene.SetRenderMode(scene, GlassRenderMode.Fallback);
            var window = Sta.Show(scene, 300, 200);
            var bitmap = new RenderTargetBitmap(300, 200, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(scene);
            var pixels = new byte[300 * 200 * 4];
            bitmap.CopyPixels(pixels, 300 * 4, 0);
            // The surface is centered: its top edge is at y = 68. The specular line there is brighter than the glass below it.
            int Luma(int x, int y)
            {
                var i = (y * 300 + x) * 4;
                return pixels[i] + pixels[i + 1] + pixels[i + 2];
            }
            Assert.True(Luma(150, 68) > Luma(150, 90) + 60, $"rim {Luma(150, 68)} vs inside {Luma(150, 90)}");
            window.Close();
        });
    }

    [Fact]
    public void SurfaceHitTestsItsOutline()
    {
        Sta.Run(() =>
        {
            var surface = new GlassSurface { Width = 200, Height = 64, Radius = GlassRadius.Capsule, HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top };
            var window = Sta.Show(Scene(surface), 300, 200);
            Assert.Same(surface, Hit(surface, new Point(100, 32)));
            var corner = Hit(surface, new Point(2, 2));
            Assert.True(corner is null, $"the corner outside the outline hit {Describe(corner)}");
            window.Close();
        });
    }

    private static string Describe(DependencyObject? node)
    {
        var chain = new System.Collections.Generic.List<string>();
        for (; node is not null; node = VisualTreeHelper.GetParent(node)) chain.Add(node.GetType().Name + (node is FrameworkElement { Name.Length: > 0 } f ? "#" + f.Name : ""));
        return string.Join(" < ", chain);
    }

    /// <summary>What mouse input would hit, as WPF finds it (honoring IsHitTestVisible).</summary>
    private static DependencyObject? Hit(UIElement element, Point point) => element.InputHitTest(point) as DependencyObject;

    [Fact]
    public void ButtonsHaveTheWindowsSizes()
    {
        Sta.Run(() =>
        {
            var small = new GlassButton { Content = "S", Size = GlassButtonSize.Small };
            var regular = new GlassButton { Content = "R" };
            var large = new GlassButton { Content = "L", Size = GlassButtonSize.Large };
            var icon = new GlassButton { Content = "+", IsIconOnly = true };
            var window = Sta.Show(Scene(new StackPanel { Children = { small, regular, large, icon } }));
            Assert.Equal(24, small.ActualHeight);
            Assert.Equal(32, regular.ActualHeight);
            Assert.Equal(40, large.ActualHeight);
            Assert.Equal(icon.ActualHeight, icon.ActualWidth);
            Assert.Equal(GlassRadius.Circle, icon.Radius);
            Assert.Equal(AutomationControlType.Button, UIElementAutomationPeer.CreatePeerForElement(regular).GetAutomationControlType());
            window.Close();
        });
    }

    [Fact]
    public void SwitchTogglesAndMovesItsThumb()
    {
        Sta.Run(() =>
        {
            var toggle = new GlassSwitch { Content = "Wi-Fi" };
            var window = Sta.Show(Scene(toggle));
            var peer = UIElementAutomationPeer.CreatePeerForElement(toggle);
            var pattern = (IToggleProvider)peer.GetPattern(PatternInterface.Toggle);
            pattern.Toggle();
            Assert.True(toggle.IsChecked);
            Assert.Equal(ToggleState.On, pattern.ToggleState);
            GlassScene.SetMotion(toggle, GlassMotion.None);
            pattern.Toggle();
            Sta.Pump();
            Assert.False(toggle.IsChecked);
            window.Close();
        });
    }

    [Fact]
    public void SliderFillReachesTheThumb()
    {
        Sta.Run(() =>
        {
            var slider = new GlassSlider { Width = 220, Minimum = 0, Maximum = 100, Value = 50 };
            var window = Sta.Show(Scene(slider));
            var fill = (Border)slider.Template.FindName("PART_Fill", slider);
            Assert.Equal(110, fill.Width, 1);
            slider.Value = 100;
            Assert.Equal(210, fill.Width, 1);
            Assert.Equal(AutomationControlType.Slider, UIElementAutomationPeer.CreatePeerForElement(slider).GetAutomationControlType());
            window.Close();
        });
    }

    [Fact]
    public void TabBarIsATabListAndFollowsTheKeyboard()
    {
        Sta.Run(() =>
        {
            var tabs = new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" }, new GlassTabBarItem { Content = "Three" } }, SelectedIndex = 0 };
            var window = Sta.Show(Scene(tabs));
            var peer = (SelectorAutomationPeer)UIElementAutomationPeer.CreatePeerForElement(tabs);
            Assert.Equal(AutomationControlType.Tab, peer.GetAutomationControlType());
            Assert.All(peer.GetChildren(), child => Assert.Equal(AutomationControlType.TabItem, child.GetAutomationControlType()));
            ((GlassTabBarItem)tabs.Items[0]).Focus();
            var right = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(tabs), 0, Key.Right) { RoutedEvent = Keyboard.KeyDownEvent };
            ((GlassTabBarItem)tabs.Items[0]).RaiseEvent(right);
            Assert.Equal(1, tabs.SelectedIndex);
            window.Close();
        });
    }

    [Fact]
    public void ToolbarSplitsIntoClustersAtSpacers()
    {
        Sta.Run(() =>
        {
            var toolbar = new GlassToolbar { Items = { new Button { Content = "A" }, new Button { Content = "B" }, new GlassToolbarSpacer(), new Button { Content = "C" } } };
            var window = Sta.Show(Scene(toolbar));
            var host = (Grid)toolbar.Template.FindName("PART_Host", toolbar);
            Assert.Equal(2, host.Children.OfType<GlassSurface>().Count());
            Assert.Equal(AutomationControlType.ToolBar, UIElementAutomationPeer.CreatePeerForElement(toolbar).GetAutomationControlType());
            toolbar.Items.Insert(1, new Button { Content = "A2" });
            Sta.Pump();
            Assert.Equal(2, host.Children.OfType<GlassSurface>().Count());
            Assert.Equal(4, host.Children.OfType<GlassSurface>().Sum(c => ((Panel)c.Content).Children.Count));
            window.Close();
        });
    }

    [Fact]
    public void MenuOpensInTheSceneAndClosesOnEscape()
    {
        Sta.Run(() =>
        {
            var chosen = 0;
            var item = new GlassMenuItem { Header = "Copy" };
            item.Click += (_, _) => chosen++;
            var menu = new GlassMenu { Header = "Edit", Items = { item, new Separator(), new GlassMenuItem { Header = "Paste" } } };
            var scene = Scene(menu);
            var window = Sta.Show(scene);
            menu.IsOpen = true;
            Sta.Pump();
            var panel = scene.PopupLayer.Children.OfType<GlassSurface>().Single();
            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(GlassMaterial.Regular, panel.Material);
            Assert.Equal(ExpandCollapseState.Expanded, ((IExpandCollapseProvider)UIElementAutomationPeer.CreatePeerForElement(menu).GetPattern(PatternInterface.ExpandCollapse)).ExpandCollapseState);
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(item).GetPattern(PatternInterface.Invoke)).Invoke();
            Assert.Equal(1, chosen);
            menu.IsOpen = true;
            Sta.Pump();
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(panel), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            panel.RaiseEvent(escape);
            Assert.False(menu.IsOpen);
            window.Close();
        });
    }

    [Fact]
    public void ProgressiveBlurRegistersAsABackdropLayer()
    {
        Sta.Run(() =>
        {
            var blur = new GlassProgressiveBlur { Edge = GlassEdge.Bottom, Size = 60 };
            var scene = Scene(blur);
            var window = Sta.Show(scene);
            Assert.Contains(blur, scene.BackdropLayers);
            Assert.Equal(60, blur.DesiredSize.Height);
            window.Close();
        });
    }

    [Fact]
    public void GeometryMatchesTheOutline()
    {
        Sta.Run(() =>
        {
            var geometry = GlassGeometry.Create(new Rect(10, 20, 120, 44), GlassRadius.FromValue(12));
            Assert.True(geometry.IsFrozen);
            Assert.Equal(new Rect(10, 20, 120, 44), geometry.Bounds);
            var grown = GlassGeometry.Inflate(new Rect(0, 0, 120, 44), GlassRadius.Capsule, 2);
            Assert.Equal(new Rect(-2, -2, 124, 48), grown.Bounds);
            Assert.True(geometry.FillContains(new Point(70, 42)));
            Assert.False(geometry.FillContains(new Point(11, 21)));
            Assert.Same(Geometry.Empty, GlassGeometry.Create(new Size(0, 10), GlassRadius.Capsule));
        });
    }
}
