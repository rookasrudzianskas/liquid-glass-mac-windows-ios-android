using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LiquidGlass.Wpf.Rendering;
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

    /// <summary>
    /// Every default style and template in Themes/Generic.xaml loads: a control whose style cannot be built fails only
    /// when it is first shown, so show one of each (with a toolbar's button and toggle styles and an open menu's items).
    /// </summary>
    [Fact]
    public void EveryDefaultStyleLoads()
    {
        Sta.Run(() =>
        {
            var toolbarButton = new Button { Content = "A" };
            var toolbarToggle = new ToggleButton { Content = "B" };
            var item = new GlassMenuItem { Header = "Item", IsCheckable = true, IsChecked = true, Icon = "", InputGestureText = "Ctrl+C" };
            var menu = new GlassMenu { Header = "Menu", Items = { item, new Separator() } };
            var controls = new FrameworkElement[]
            {
                new GlassSurface { Width = 80, Height = 40 },
                new GlassButton { Content = "Button" },
                new GlassButton { Content = "", IsIconOnly = true },
                new GlassSlider { Width = 120, Value = 0.5, Maximum = 1 },
                new GlassSwitch { Content = "Switch" },
                new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" } }, SelectedIndex = 0 },
                new GlassToolbar { Items = { toolbarButton, new GlassToolbarSpacer(), toolbarToggle } },
                menu,
                new GlassProgressiveBlur { Height = 40 },
            };
            var window = Sta.Show(Scene(new StackPanel { Children = { new WrapPanel() } }), 900, 420);
            var panel = (WrapPanel)((StackPanel)((GlassScene)window.Content).Children[0]).Children[0];
            foreach (var control in controls) panel.Children.Add(control);
            Sta.Pump();
            menu.IsOpen = true;
            Sta.Pump();
            foreach (var control in controls.OfType<Control>().Append(toolbarButton).Append(toolbarToggle).Append(item))
            {
                Assert.True(control.IsLoaded, $"{control.GetType().Name} did not load");
                Assert.True(VisualTreeHelper.GetChildrenCount(control) > 0, $"{control.GetType().Name} has no template");
            }
            Assert.IsType<Grid>(VisualTreeHelper.GetChild(item, 0));
            window.Close();
        });
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
            // A toggle switch, as Windows' own: a button with the Toggle pattern.
            Assert.Equal(AutomationControlType.Button, peer.GetAutomationControlType());
            Assert.Equal("toggle switch", peer.GetLocalizedControlType());
            Assert.Equal("Wi-Fi", peer.GetName());
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
            var wrap = new GlassMenuItem { Header = "Wrap lines", IsCheckable = true };
            // An item container style applies to the items and leaves the separator alone.
            var spaced = new Style(typeof(GlassMenuItem));
            spaced.Setters.Add(new Setter(FrameworkElement.MinHeightProperty, 36.0));
            var menu = new GlassMenu
            {
                Header = "Edit",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ItemContainerStyle = spaced,
                Items = { item, wrap, new Separator(), new GlassMenuItem { Header = "Paste" } },
            };
            var scene = Scene(menu);
            var window = Sta.Show(scene);

            // UI Automation: the trigger, which takes keyboard focus, is the button that expands and collapses; the menu
            // itself stays out of the control view, so there is one button named "Edit", not two.
            var triggerPeer = UIElementAutomationPeer.CreatePeerForElement(menu.Trigger!);
            Assert.Equal(AutomationControlType.Button, triggerPeer.GetAutomationControlType());
            Assert.Equal("Edit", triggerPeer.GetName());
            Assert.True(menu.Trigger!.Focusable);
            var expand = (IExpandCollapseProvider)triggerPeer.GetPattern(PatternInterface.ExpandCollapse);
            Assert.NotNull(expand);
            Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);
            Assert.False(UIElementAutomationPeer.CreatePeerForElement(menu).IsControlElement());
            expand.Expand();
            Sta.Pump();
            Assert.True(menu.IsOpen);
            Assert.Equal(ExpandCollapseState.Expanded, expand.ExpandCollapseState);

            var panel = scene.PopupLayer.Children.OfType<GlassSurface>().Single();
            Assert.Equal(Visibility.Visible, panel.Visibility);
            Assert.Equal(GlassMaterial.Regular, panel.Material);
            Assert.Equal(36.0, item.MinHeight);
            // The open panel is a Menu whose items are MenuItems.
            var panelPeer = UIElementAutomationPeer.CreatePeerForElement(panel);
            Assert.Equal(AutomationControlType.Menu, panelPeer.GetAutomationControlType());
            Assert.Equal(3, Descendants(panelPeer).Count(p => p.GetAutomationControlType() == AutomationControlType.MenuItem && p.IsControlElement()));

            // Invoke chooses an item exactly as a click does: Click runs and the menu closes.
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(item).GetPattern(PatternInterface.Invoke)).Invoke();
            Sta.Pump();
            Assert.Equal(1, chosen);
            Assert.False(menu.IsOpen);
            Assert.Equal(ExpandCollapseState.Collapsed, expand.ExpandCollapseState);

            // A checkable item toggles when invoked, and the menu closes.
            menu.IsOpen = true;
            Sta.Pump();
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(wrap).GetPattern(PatternInterface.Invoke)).Invoke();
            Sta.Pump();
            Assert.True(wrap.IsChecked);
            Assert.False(menu.IsOpen);

            menu.IsOpen = true;
            Sta.Pump();
            var escape = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(panel), 0, Key.Escape) { RoutedEvent = Keyboard.PreviewKeyDownEvent };
            panel.RaiseEvent(escape);
            Assert.False(menu.IsOpen);
            window.Close();
        });
    }

    [Fact]
    public void MenuItemLabelFollowsItsHeader()
    {
        Sta.Run(() =>
        {
            var item = new GlassMenuItem { Header = "Name" };
            var menu = new GlassMenu { Header = "Sort", HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Items = { item } };
            var window = Sta.Show(Scene(menu));
            menu.IsOpen = true;
            Sta.Pump();
            Assert.Contains("Name", Texts(item));

            item.Header = "Date modified";
            Sta.Pump();
            Assert.Contains("Date modified", Texts(item));
            Assert.DoesNotContain("Name", Texts(item));
            Assert.Equal("Date modified", UIElementAutomationPeer.CreatePeerForElement(item).GetName());

            // Content the application sets takes over from the header.
            item.Content = "Size";
            item.Header = "Kind";
            Sta.Pump();
            Assert.Contains("Size", Texts(item));
            window.Close();
        });
    }

    [Fact]
    public void MenuGeneratesItemsFromItsItemsSource()
    {
        Sta.Run(() =>
        {
            var bold = new Style(typeof(GlassMenuItem));
            bold.Setters.Add(new Setter(Control.FontWeightProperty, FontWeights.Bold));
            var menu = new GlassMenu
            {
                Header = "Sort",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                ItemsSource = new[] { "Name", "Date modified", "Size" },
                ItemContainerStyle = bold,
            };
            var window = Sta.Show(Scene(menu));
            menu.IsOpen = true;
            Sta.Pump();
            var host = Visuals<ItemsControl>(menu.Panel!).Single();
            var containers = Enumerable.Range(0, 3).Select(i => host.ItemContainerGenerator.ContainerFromIndex(i)).ToList();
            Assert.All(containers, c => Assert.IsType<GlassMenuItem>(c));
            var first = (GlassMenuItem)containers[0];
            Assert.Equal(FontWeights.Bold, first.FontWeight);
            Assert.Equal("Name", UIElementAutomationPeer.CreatePeerForElement(first).GetName());
            Assert.Contains("Name", Texts(first));

            // A generated item chooses and closes like a written-out one.
            ((IInvokeProvider)UIElementAutomationPeer.CreatePeerForElement(first).GetPattern(PatternInterface.Invoke)).Invoke();
            Sta.Pump();
            Assert.False(menu.IsOpen);
            window.Close();
        });
    }

    [Fact]
    public void MenuItemsUseTheGlassTextFontWhateverTheTriggerUses()
    {
        Sta.Run(() =>
        {
            var item = new GlassMenuItem { Header = "Rename" };
            var menu = new GlassMenu
            {
                IsIconOnly = true,
                Header = "\uE712",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Items = { item },
            };
            var window = Sta.Show(Scene(menu));
            menu.IsOpen = true;
            Sta.Pump();
            var font = (FontFamily)item.FindResource(new ComponentResourceKey(typeof(GlassScene), "Font"));
            Assert.Equal(font.Source, item.FontFamily.Source);
            Assert.Equal(menu.FontFamily.Source, menu.Trigger!.FontFamily.Source);
            window.Close();
        });
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void MenuOpensFromItsStartEdgeInRightToLeftLayouts(bool sceneIsRightToLeft)
    {
        Sta.Run(() =>
        {
            GlassSystemSettings.AnimationsOverride = true;
            try
            {
                var menu = new GlassMenu
                {
                    Header = "Sort",
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(40, 30, 40, 0),
                    Items = { new GlassMenuItem { Header = "Name" }, new GlassMenuItem { Header = "Date modified" } },
                };
                var scene = Scene();
                if (sceneIsRightToLeft)
                {
                    // A right-to-left window: the scene, its popup layer and the menu all run right to left.
                    scene.FlowDirection = FlowDirection.RightToLeft;
                    scene.Children.Add(menu);
                }
                else
                {
                    // A right-to-left menu in a left-to-right scene: WPF mirrors the panel inside the popup layer.
                    scene.Children.Add(new StackPanel { FlowDirection = FlowDirection.RightToLeft, Children = { menu } });
                }
                // Measured in a left-to-right root, so x runs as on screen.
                var root = new Border { Child = scene };
                var window = Sta.Show(root, 640, 420);
                Rect InRoot(Visual element, Rect rect) => element.TransformToAncestor(root).TransformBounds(rect);
                var trigger = menu.Trigger!;
                var triggerRect = InRoot(trigger, new Rect(trigger.RenderSize));
                Assert.True(triggerRect.Left > 320, $"the trigger should sit at the right (start) edge: {triggerRect}");

                // The morph starts at the trigger: the glass first covers exactly the trigger.
                menu.IsOpen = true;
                root.UpdateLayout();
                var panel = menu.Panel!;
                var lens = panel.Lens!;
                AssertNear(triggerRect, InRoot(lens, lens.ShapeBounds));

                // Open, the panel lines up with the trigger's start (right) edge and grows to its left.
                Sta.Wait(1.2);
                var panelRect = InRoot(panel, new Rect(panel.RenderSize));
                Assert.Equal(triggerRect.Right, panelRect.Right, 1);
                Assert.True(panelRect.Left < triggerRect.Left || panelRect.Width <= triggerRect.Width + 0.5, $"panel {panelRect}, trigger {triggerRect}");
                Assert.True(panelRect.Top >= triggerRect.Bottom, $"panel {panelRect}, trigger {triggerRect}");

                // Closing shrinks the glass back into the trigger.
                menu.IsOpen = false;
                Sta.Wait(0.05);
                var closing = InRoot(lens, lens.ShapeBounds);
                Assert.True(Math.Abs(closing.Right - triggerRect.Right) < Math.Abs(closing.Left - triggerRect.Left) + 1,
                    $"the closing glass {closing} should stay anchored at the trigger's right edge {triggerRect}");
                window.Close();
            }
            finally
            {
                GlassSystemSettings.AnimationsOverride = null;
            }
        });
    }

    private static void AssertNear(Rect expected, Rect actual)
        => Assert.True(Math.Abs(expected.X - actual.X) < 0.75 && Math.Abs(expected.Y - actual.Y) < 0.75
                       && Math.Abs(expected.Width - actual.Width) < 0.75 && Math.Abs(expected.Height - actual.Height) < 0.75,
            $"expected {expected}, got {actual}");

    [Fact]
    public void TabBarKeepsItsSelection()
    {
        Sta.Run(() =>
        {
            var tabs = new GlassTabBar { Items = { new GlassTabBarItem { Content = "One" }, new GlassTabBarItem { Content = "Two" }, new GlassTabBarItem { Content = "Three" } }, SelectedIndex = 1 };
            var window = Sta.Show(Scene(tabs));
            // What Ctrl+click on the selected item does in a ListBox: the tab bar keeps it selected.
            ((GlassTabBarItem)tabs.Items[1]).IsSelected = false;
            Sta.Pump();
            Assert.Equal(1, tabs.SelectedIndex);
            tabs.SelectedIndex = -1;
            Sta.Pump();
            Assert.Equal(1, tabs.SelectedIndex);
            var lens = (GlassSurface)tabs.Template.FindName("PART_Lens", tabs);
            Assert.Equal(Visibility.Visible, lens.Visibility);

            var selection = (ISelectionProvider)UIElementAutomationPeer.CreatePeerForElement(tabs).GetPattern(PatternInterface.Selection);
            Assert.True(selection.IsSelectionRequired);
            Assert.False(selection.CanSelectMultiple);
            Assert.Single(selection.GetSelection());

            // Removing the selected item selects its neighbor.
            tabs.Items.RemoveAt(1);
            Sta.Pump();
            Assert.Equal(1, tabs.SelectedIndex);
            Assert.Equal("Three", ((GlassTabBarItem)tabs.SelectedItem).Content);
            window.Close();
        });
    }

    [Fact]
    public void ToolbarFocusRingTakesTheGlassForeground()
    {
        Sta.Run(() =>
        {
            // The window keeps WPF's default (black) foreground while the glass is dark.
            var button = new Button { Content = "Edit" };
            var toolbar = new GlassToolbar { Appearance = GlassAppearance.Dark, Items = { button } };
            var window = Sta.Show(Scene(toolbar), 320, 160);
            Assert.Null(button.FocusVisualStyle);
            var ring = (Border)button.Template.FindName("FocusRing", button);
            Assert.Equal(Visibility.Collapsed, ring.Visibility);

            // A navigation key on the button (as after tabbing to it) shows the ring, in the glass's light foreground.
            var tab = new KeyEventArgs(Keyboard.PrimaryDevice, PresentationSource.FromVisual(button), 0, Key.Tab) { RoutedEvent = Keyboard.KeyDownEvent };
            button.RaiseEvent(tab);
            Assert.True(GlassScene.GetIsFocusVisible(button));
            Assert.Equal(Visibility.Visible, ring.Visibility);
            Assert.Equal(Colors.White, ((SolidColorBrush)ring.BorderBrush).Color);
            window.Close();
        });
    }

    [Fact]
    public void ARejectedShaderFallsBackInsteadOfCrashing()
    {
        Sta.Run(() =>
        {
            var surface = new GlassSurface { Width = 160, Height = 80 };
            var window = Sta.Show(Scene(surface));
            var expected = GlassDiagnostics.CanUseShader(window) ? GlassLensMode.Shader : GlassLensMode.Fallback;
            Assert.Equal(expected, surface.RenderedMode);
            try
            {
                // What the library's InvalidPixelShaderEncountered handler does when the driver refuses the shader.
                GlassShaderLibrary.MarkShaderRejected();
                Sta.Pump();
                Assert.False(GlassDiagnostics.CanUseShader(window));
                Assert.Equal(GlassLensMode.Fallback, surface.RenderedMode);
                Assert.Contains("rejected by the graphics driver", GlassDiagnostics.Describe(), StringComparison.Ordinal);
            }
            finally
            {
                GlassShaderLibrary.ResetShaderRejected();
            }
            Sta.Pump();
            Assert.Equal(expected, surface.RenderedMode);
            window.Close();
        });
    }

    private static IEnumerable<AutomationPeer> Descendants(AutomationPeer peer)
    {
        foreach (var child in peer.GetChildren() ?? new List<AutomationPeer>())
        {
            yield return child;
            foreach (var descendant in Descendants(child)) yield return descendant;
        }
    }

    private static IEnumerable<T> Visuals<T>(DependencyObject root) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is T match) yield return match;
            foreach (var descendant in Visuals<T>(child)) yield return descendant;
        }
    }

    /// <summary>The text an element shows (its text blocks and access-key text).</summary>
    private static List<string> Texts(DependencyObject root)
        => Visuals<FrameworkElement>(root).Select(e => e switch { AccessText a => a.Text, TextBlock t => t.Text, _ => null }).OfType<string>().ToList();

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
