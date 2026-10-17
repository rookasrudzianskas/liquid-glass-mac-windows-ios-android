using System;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A glass button (<see cref="HeaderedItemsControl.Header"/> is its content) that opens a menu: a Regular glass panel
/// that grows out of the button with the morph spring and collapses back into it. Items highlight in the foreground
/// color at 8%; the menu closes when an item is chosen, on Escape and on a click outside it. The panel floats in the
/// enclosing <see cref="GlassScene"/>, refracting its backdrop; outside a scene it opens in a popup with the opaque
/// material.
/// </summary>
/// <remarks>
/// Items are <see cref="GlassMenuItem"/>s and <see cref="Separator"/>s, written out or generated from
/// <see cref="ItemsControl.ItemsSource"/> (each data item becomes a <see cref="GlassMenuItem"/> that shows it with
/// <see cref="ItemsControl.ItemTemplate"/>; <see cref="ItemsControl.ItemContainerStyle"/> applies to every item).
/// The trigger button carries the menu's UI Automation semantics (a button with ExpandCollapse); the open panel is a
/// Menu of MenuItems.
/// </remarks>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassMenu Header="Sort">
///   <lg:GlassMenuItem Header="Name" IsCheckable="True" IsChecked="True" />
///   <lg:GlassMenuItem Header="Date modified" IsCheckable="True" />
///   <Separator />
///   <lg:GlassMenuItem Header="Delete" InputGestureText="Del" Command="{Binding Delete}" />
/// </lg:GlassMenu>
/// ]]></code>
/// </example>
[TemplatePart(Name = TriggerPart, Type = typeof(GlassButton))]
public class GlassMenu : HeaderedItemsControl, IGlassAnimation
{
    /// <summary>Whether the menu is open.</summary>
    public static readonly DependencyProperty IsOpenProperty = DependencyProperty.Register(
        nameof(IsOpen), typeof(bool), typeof(GlassMenu),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsOpenChanged));

    /// <summary>Where the menu opens. Default <see cref="GlassMenuPlacement.Bottom"/>.</summary>
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(GlassMenuPlacement), typeof(GlassMenu), new FrameworkPropertyMetadata(GlassMenuPlacement.Bottom));

    /// <summary>Material of the menu panel. Default <see cref="GlassMaterial.Regular"/>.</summary>
    public static readonly DependencyProperty PanelMaterialProperty = DependencyProperty.Register(
        nameof(PanelMaterial), typeof(GlassMaterial), typeof(GlassMenu), new FrameworkPropertyMetadata(GlassMaterial.Regular));

    /// <summary>Corner radius of the menu panel: units, <c>capsule</c> or <c>circle</c>. Default 28.</summary>
    public static readonly DependencyProperty PanelRadiusProperty = DependencyProperty.Register(
        nameof(PanelRadius), typeof(GlassRadius), typeof(GlassMenu), new FrameworkPropertyMetadata(GlassRadius.FromValue(28)));

    /// <summary>Tint of the trigger and the panel, or null.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassMenu),
        new FrameworkPropertyMetadata(null, OnTintChanged));

    /// <summary>Size of the trigger button. Default <see cref="GlassButtonSize.Regular"/>.</summary>
    public static readonly DependencyProperty SizeProperty = GlassButton.SizeProperty.AddOwner(typeof(GlassMenu),
        new FrameworkPropertyMetadata(GlassButtonSize.Regular));

    /// <summary>Whether the trigger is an icon-only circle.</summary>
    public static readonly DependencyProperty IsIconOnlyProperty = GlassButton.IsIconOnlyProperty.AddOwner(typeof(GlassMenu),
        new FrameworkPropertyMetadata(false));

    /// <summary>Raised after the menu opened.</summary>
    public static readonly RoutedEvent OpenedEvent = EventManager.RegisterRoutedEvent(
        nameof(Opened), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GlassMenu));

    /// <summary>Raised after the menu closed.</summary>
    public static readonly RoutedEvent ClosedEvent = EventManager.RegisterRoutedEvent(
        nameof(Closed), RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(GlassMenu));

    private const string TriggerPart = "PART_Trigger";
    private const double Gap = 6;
    private const double Inset = 4;
    private const double MinPanelWidth = 160;

    private readonly Spring _morph = new(0, GlassSprings.Morph);
    private readonly GlassAppearanceTracker _appearance;
    private GlassButton? _trigger;
    private GlassMenuPanel? _panel;
    private GlassMenuItemsHost? _list;
    private Canvas? _layer;
    private Popup? _popup;
    private Window? _window;
    private Rect _from;
    private Rect _to;
    private bool _mirrored;
    private bool _focusFirst;

    static GlassMenu()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassMenu), new FrameworkPropertyMetadata(typeof(GlassMenu)));
        GlassScene.AddContentForegroundOwner(typeof(GlassMenu));
        FocusableProperty.OverrideMetadata(typeof(GlassMenu), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Creates a menu.</summary>
    public GlassMenu()
    {
        _appearance = new GlassAppearanceTracker(this, () => Tint);
        Unloaded += (_, _) =>
        {
            Close(immediately: true);
            if (_panel is not null) RemoveFromParent(_panel);
        };
    }

    /// <summary>Raised after the menu opened.</summary>
    public event RoutedEventHandler Opened
    {
        add => AddHandler(OpenedEvent, value);
        remove => RemoveHandler(OpenedEvent, value);
    }

    /// <summary>Raised after the menu closed.</summary>
    public event RoutedEventHandler Closed
    {
        add => AddHandler(ClosedEvent, value);
        remove => RemoveHandler(ClosedEvent, value);
    }

    /// <summary>Whether the menu is open.</summary>
    public bool IsOpen
    {
        get => (bool)GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    /// <summary>Where the menu opens.</summary>
    public GlassMenuPlacement Placement
    {
        get => (GlassMenuPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>Material of the menu panel.</summary>
    public GlassMaterial PanelMaterial
    {
        get => (GlassMaterial)GetValue(PanelMaterialProperty);
        set => SetValue(PanelMaterialProperty, value);
    }

    /// <summary>Corner radius of the menu panel: units, <c>capsule</c> or <c>circle</c>.</summary>
    public GlassRadius PanelRadius
    {
        get => (GlassRadius)GetValue(PanelRadiusProperty);
        set => SetValue(PanelRadiusProperty, value);
    }

    /// <summary>Tint of the trigger and the panel, or null.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Size of the trigger button.</summary>
    public GlassButtonSize Size
    {
        get => (GlassButtonSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Whether the trigger is an icon-only circle.</summary>
    public bool IsIconOnly
    {
        get => (bool)GetValue(IsIconOnlyProperty);
        set => SetValue(IsIconOnlyProperty, value);
    }

    /// <summary>The trigger button in the template.</summary>
    internal GlassButton? Trigger => _trigger;

    /// <summary>The panel, once the menu has opened.</summary>
    internal GlassSurface? Panel => _panel;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (_trigger is not null)
        {
            _trigger.Click -= OnTriggerClick;
            _trigger.PreviewKeyDown -= OnTriggerKeyDown;
        }
        _trigger = GetTemplateChild(TriggerPart) as GlassButton;
        if (_trigger is not null)
        {
            _trigger.Click += OnTriggerClick;
            _trigger.PreviewKeyDown += OnTriggerKeyDown;
        }
    }

    // Advanced by the frame loop; not part of the public surface.
    bool IGlassAnimation.Tick(double dt)
    {
        if (_panel is null) return false;
        if (GlassResolve.Motion(this) == GlassMotion.None) _morph.Jump(_morph.Target);
        else _morph.Step(dt);
        Morph(_morph.Value);
        var settled = _morph.IsSettled;
        if (settled && _morph.Target.Equals(0)) Detach();
        return !settled;
    }

    /// <summary>Closes the menu (with the morph back into the trigger) and returns focus to the trigger.</summary>
    public void Close() => SetCurrentValue(IsOpenProperty, false);

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlassMenuAutomationPeer(this);

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is UIElement;

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new GlassMenuItem();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    /// <summary>Called by an item when it was chosen.</summary>
    internal void OnItemChosen() => Close();

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassMenu)d)._appearance.Update();

    private static void OnIsOpenChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var menu = (GlassMenu)d;
        if ((bool)e.NewValue) menu.Open();
        else menu.Close(immediately: false);
        var (oldValue, newValue) = ((bool)e.OldValue, (bool)e.NewValue);
        (UIElementAutomationPeer.FromElement(menu) as GlassMenuAutomationPeer)?.RaiseExpandCollapseChanged(oldValue, newValue);
        // The trigger has keyboard focus, so screen readers announce the state change from its peer.
        if (menu._trigger is not null)
            (UIElementAutomationPeer.FromElement(menu._trigger) as GlassMenuTriggerAutomationPeer)?.RaiseExpandCollapseChanged(oldValue, newValue);
    }

    private void OnTriggerClick(object sender, RoutedEventArgs e)
    {
        _focusFirst = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
        SetCurrentValue(IsOpenProperty, !IsOpen);
    }

    private void OnTriggerKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Down || IsOpen) return;
        _focusFirst = true;
        SetCurrentValue(IsOpenProperty, true);
        e.Handled = true;
    }

    private void Open()
    {
        if (_trigger is null || !IsLoaded) return;
        var panel = EnsurePanel();
        var scene = GlassScene.FindScene(this);
        if (scene is not null && !scene.IsInBackdrop(this))
        {
            if (_popup is not null) _popup.IsOpen = false;
            _layer = scene.PopupLayer;
            if (!_layer.Children.Contains(panel))
            {
                RemoveFromParent(panel);
                _layer.Children.Add(panel);
            }
            Place(scene);
        }
        else
        {
            _layer = null;
            _popup ??= new Popup { AllowsTransparency = true, StaysOpen = true, Placement = PlacementMode.Bottom };
            _popup.PlacementTarget = _trigger;
            _popup.Placement = Placement == GlassMenuPlacement.Top ? PlacementMode.Top : PlacementMode.Bottom;
            _popup.VerticalOffset = Placement == GlassMenuPlacement.Top ? -Gap : Gap;
            if (_popup.Child is null)
            {
                RemoveFromParent(panel);
                _popup.Child = panel;
            }
            panel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            _to = new Rect(panel.DesiredSize);
            _from = _to;
            _mirrored = false;
            _popup.IsOpen = true;
        }
        panel.Visibility = Visibility.Visible;
        HookDismiss();
        if (GlassResolve.Motion(this) == GlassMotion.Full && _layer is not null)
        {
            _morph.Animate(1, GlassSprings.Morph);
            Morph(_morph.Value);
            GlassFrameLoop.Start(this);
        }
        else
        {
            _morph.Jump(1);
            Morph(1);
        }
        Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsOpen || _list is null) return;
            if (_focusFirst) _list.MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            else panel.Focus();
        }), DispatcherPriority.Input);
        RaiseMenuEvent(panel, AutomationEvents.MenuOpened);
        RaiseEvent(new RoutedEventArgs(OpenedEvent, this));
    }

    private void Close(bool immediately)
    {
        UnhookDismiss();
        if (_panel is null) return;
        if (_panel.Visibility == Visibility.Visible && _panel.IsHitTestVisible) RaiseMenuEvent(_panel, AutomationEvents.MenuClosed);
        var hadFocus = _panel.IsKeyboardFocusWithin;
        if (immediately || GlassResolve.Motion(this) != GlassMotion.Full || _layer is null)
        {
            _morph.Jump(0);
            Detach();
        }
        else
        {
            _morph.Animate(0, GlassSprings.Morph);
            GlassFrameLoop.Start(this);
            _panel.IsHitTestVisible = false;
        }
        if (hadFocus) _trigger?.Focus();
        if (!immediately) RaiseEvent(new RoutedEventArgs(ClosedEvent, this));
        if (IsOpen) SetCurrentValue(IsOpenProperty, false);
    }

    private static void RaiseMenuEvent(UIElement panel, AutomationEvents automationEvent)
    {
        if (AutomationPeer.ListenerExists(automationEvent)) UIElementAutomationPeer.CreatePeerForElement(panel)?.RaiseAutomationEvent(automationEvent);
    }

    private GlassMenuPanel EnsurePanel()
    {
        if (_panel is not null)
        {
            _panel.IsHitTestVisible = true;
            ConfigurePanel(_panel);
            return _panel;
        }
        _list = new GlassMenuItemsHost(this);
        KeyboardNavigation.SetDirectionalNavigation(_list, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetTabNavigation(_list, KeyboardNavigationMode.Cycle);
        var panel = new GlassMenuPanel
        {
            Padding = new Thickness(Inset),
            MinWidth = MinPanelWidth,
            Focusable = true,
            Content = _list,
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top,
        };
        KeyboardNavigation.SetTabNavigation(panel, KeyboardNavigationMode.Cycle);
        KeyboardNavigation.SetControlTabNavigation(panel, KeyboardNavigationMode.Cycle);
        AutomationProperties.SetName(panel, AutomationProperties.GetName(this) is { Length: > 0 } name ? name : Header?.ToString() ?? "Menu");
        panel.PreviewKeyDown += OnPanelKeyDown;
        _panel = panel;
        ConfigurePanel(panel);
        return panel;
    }

    private void ConfigurePanel(GlassSurface panel)
    {
        // The panel lives in the scene's popup layer, not under the menu, so nothing is inherited: carry the options over.
        // (Items written out in the menu are its logical children and inherit from it; generated items inherit from the
        // panel, so it carries the text size too.)
        panel.Material = PanelMaterial;
        panel.Appearance = GlassScene.GetIsDark(this) ? GlassAppearance.Dark : GlassAppearance.Light;
        panel.Motion = GlassScene.GetMotion(this);
        GlassScene.SetRenderMode(panel, GlassScene.GetRenderMode(this));
        GlassScene.SetReduceTransparency(panel, GlassScene.GetReduceTransparency(this));
        GlassScene.SetIncreaseContrast(panel, GlassScene.GetIncreaseContrast(this));
        panel.Tint = Tint;
        panel.Radius = PanelRadius;
        panel.FlowDirection = FlowDirection;
        panel.FontSize = FontSize;
    }

    /// <summary>Positions the panel next to the trigger, inside the scene, flipping when there is no room.</summary>
    private void Place(GlassScene scene)
    {
        if (_panel is null || _trigger is null || _layer is null) return;
        _panel.Measure(new Size(Math.Max(0, scene.ActualWidth - 2 * Gap), Math.Max(0, scene.ActualHeight - 2 * Gap)));
        var size = new Size(Math.Max(_panel.DesiredSize.Width, Math.Max(MinPanelWidth, _trigger.ActualWidth)), _panel.DesiredSize.Height);
        var trigger = _trigger.TransformToVisual(_layer).TransformBounds(new Rect(_trigger.RenderSize));
        var below = trigger.Bottom + Gap;
        var above = trigger.Top - Gap - size.Height;
        var fitsBelow = below + size.Height <= scene.ActualHeight - Gap;
        var fitsAbove = above >= Gap;
        var top = Placement == GlassMenuPlacement.Top ? (fitsAbove || !fitsBelow ? above : below) : (fitsBelow || !fitsAbove ? below : above);
        // The panel lines up with the trigger's start edge. Layer coordinates run in the layer's own flow direction
        // (from the right in a right-to-left layer), so the start edge is the trigger's left in them, unless the panel
        // flows the other way than the layer: then WPF mirrors the panel inside its slot and the start edge is the right.
        _mirrored = FlowDirection != _layer.FlowDirection;
        var left = _mirrored ? trigger.Right - size.Width : trigger.Left;
        left = GlassMath.Clamp(left, Gap, Math.Max(Gap, scene.ActualWidth - Gap - size.Width));
        top = GlassMath.Clamp(top, Gap, Math.Max(Gap, scene.ActualHeight - Gap - size.Height));
        _to = new Rect(left, top, size.Width, size.Height);
        _from = trigger;
        Canvas.SetLeft(_panel, left);
        Canvas.SetTop(_panel, top);
        _panel.Width = size.Width;
        _panel.Height = size.Height;
    }

    /// <summary>Draws the panel's glass between the trigger (0) and its own box (1); content fades in late.</summary>
    private void Morph(double progress)
    {
        if (_panel is null) return;
        var p = Math.Max(0, progress);
        // The trigger, in the panel's coordinates; a panel mirrored inside its slot counts x from its right edge.
        var from = new Rect(_from.X - _to.X, _from.Y - _to.Y, _from.Width, _from.Height);
        if (_mirrored) from.X = _to.Width - from.X - from.Width;
        var to = new Rect(_to.Size);
        var rect = new Rect(
            GlassMath.Mix(from.X, to.X, p), GlassMath.Mix(from.Y, to.Y, p),
            Math.Max(0, GlassMath.Mix(from.Width, to.Width, p)), Math.Max(0, GlassMath.Mix(from.Height, to.Height, p)));
        if (p >= 0.999)
        {
            _panel.Radius = PanelRadius;
            _panel.Lens?.SetShape(null);
        }
        else
        {
            // Both ends as continuous radii: a capsule's is the one whose corner extent reaches half the short side.
            var triggerRadius = Math.Min(_from.Width, _from.Height) / 2 / GlassOptics.ContinuousExtent;
            var panelRadius = PanelRadius.IsFullyRounded ? Math.Min(_to.Width, _to.Height) / 2 / GlassOptics.ContinuousExtent : PanelRadius.Value;
            _panel.Radius = GlassRadius.FromValue(Math.Max(0, GlassMath.Mix(triggerRadius, panelRadius, GlassMath.Sat(p))));
            _panel.Lens?.SetShape(rect);
        }
        if (_list is not null) _list.Opacity = GlassMath.Sat((p - 0.35) / 0.5);
        _panel.Opacity = GlassMath.Sat(p * 4);
        GlassScene.FindScene(_panel)?.RequestRealign();
    }

    private void Detach()
    {
        if (_panel is null) return;
        _panel.Visibility = Visibility.Collapsed;
        if (_popup is not null) _popup.IsOpen = false;
    }

    private void OnPanelKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape || (e.Key == Key.F4 && Keyboard.Modifiers == ModifierKeys.Alt))
        {
            Close();
            _trigger?.Focus();
            e.Handled = true;
        }
    }

    private void HookDismiss()
    {
        _window = Window.GetWindow(this);
        if (_window is null) return;
        _window.PreviewMouseDown += OnWindowMouseDown;
        _window.Deactivated += OnWindowDeactivated;
    }

    private void UnhookDismiss()
    {
        if (_window is null) return;
        _window.PreviewMouseDown -= OnWindowMouseDown;
        _window.Deactivated -= OnWindowDeactivated;
        _window = null;
    }

    private void OnWindowMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source) return;
        if (_panel is not null && IsWithin(source, _panel)) return;
        // The trigger toggles the menu itself.
        if (_trigger is not null && IsWithin(source, _trigger)) return;
        Close();
    }

    private void OnWindowDeactivated(object? sender, EventArgs e) => Close();

    private static bool IsWithin(DependencyObject node, DependencyObject ancestor)
    {
        for (var current = node; current is not null; current = current is Visual ? VisualTreeHelper.GetParent(current) : LogicalTreeHelper.GetParent(current))
            if (ReferenceEquals(current, ancestor)) return true;
        return false;
    }

    private static void RemoveFromParent(FrameworkElement element)
    {
        switch (element.Parent)
        {
            case Panel panel:
                panel.Children.Remove(element);
                break;
            case Popup popup:
                popup.Child = null;
                break;
        }
    }
}

/// <summary>An item of a <see cref="GlassMenu"/>: a command with an optional icon, check mark and shortcut text.</summary>
/// <remarks>
/// <see cref="Header"/> is the label (it fills <see cref="ContentControl.Content"/> unless content is set as well).
/// Items draw in the glass text font whatever font the menu's trigger uses, so an icon font on an icon-only menu does
/// not reach them; set <see cref="Control.FontFamily"/> on an item (or in <see cref="ItemsControl.ItemContainerStyle"/>)
/// to change it.
/// </remarks>
public class GlassMenuItem : ButtonBase
{
    /// <summary>The item's label.</summary>
    public static readonly DependencyProperty HeaderProperty = HeaderedContentControl.HeaderProperty.AddOwner(typeof(GlassMenuItem),
        new FrameworkPropertyMetadata(null, OnHeaderChanged));

    /// <summary>An icon drawn before the label.</summary>
    public static readonly DependencyProperty IconProperty = DependencyProperty.Register(
        nameof(Icon), typeof(object), typeof(GlassMenuItem), new FrameworkPropertyMetadata(null));

    /// <summary>Shortcut text drawn after the label, for example <c>Ctrl+C</c>.</summary>
    public static readonly DependencyProperty InputGestureTextProperty = DependencyProperty.Register(
        nameof(InputGestureText), typeof(string), typeof(GlassMenuItem), new FrameworkPropertyMetadata(string.Empty));

    /// <summary>Whether choosing the item toggles <see cref="IsChecked"/>.</summary>
    public static readonly DependencyProperty IsCheckableProperty = DependencyProperty.Register(
        nameof(IsCheckable), typeof(bool), typeof(GlassMenuItem), new FrameworkPropertyMetadata(false));

    /// <summary>Whether the item shows a check mark.</summary>
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool), typeof(GlassMenuItem),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnIsCheckedChanged));

    /// <summary>Whether choosing the item leaves the menu open. Default false.</summary>
    public static readonly DependencyProperty StaysOpenOnClickProperty = DependencyProperty.Register(
        nameof(StaysOpenOnClick), typeof(bool), typeof(GlassMenuItem), new FrameworkPropertyMetadata(false));

    private bool _contentFromHeader;
    private bool _copyingHeader;

    static GlassMenuItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassMenuItem), new FrameworkPropertyMetadata(typeof(GlassMenuItem)));
    }

    /// <summary>Creates an item.</summary>
    public GlassMenuItem()
    {
        GlassFocus.Track(this);
        MouseEnter += (_, _) =>
        {
            if (IsEnabled) Focus();
        };
    }

    /// <summary>The item's label.</summary>
    public object? Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    /// <summary>An icon drawn before the label.</summary>
    public object? Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    /// <summary>Shortcut text drawn after the label.</summary>
    public string InputGestureText
    {
        get => (string)GetValue(InputGestureTextProperty);
        set => SetValue(InputGestureTextProperty, value);
    }

    /// <summary>Whether choosing the item toggles <see cref="IsChecked"/>.</summary>
    public bool IsCheckable
    {
        get => (bool)GetValue(IsCheckableProperty);
        set => SetValue(IsCheckableProperty, value);
    }

    /// <summary>Whether the item shows a check mark.</summary>
    public bool IsChecked
    {
        get => (bool)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    /// <summary>Whether choosing the item leaves the menu open.</summary>
    public bool StaysOpenOnClick
    {
        get => (bool)GetValue(StaysOpenOnClickProperty);
        set => SetValue(StaysOpenOnClickProperty, value);
    }

    /// <inheritdoc />
    protected override void OnClick()
    {
        if (IsCheckable) SetCurrentValue(IsCheckedProperty, !IsChecked);
        base.OnClick();
        if (!StaysOpenOnClick) Menu()?.OnItemChosen();
    }

    /// <inheritdoc />
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.Handled || e.Key != Key.Enter) return;
        OnClick();
        e.Handled = true;
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlassMenuItemAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnContentChanged(object oldContent, object newContent)
    {
        base.OnContentChanged(oldContent, newContent);
        // Content set by the application takes over from the header.
        if (!_copyingHeader) _contentFromHeader = false;
    }

    /// <summary>Chooses the item as a click does (UI Automation's Invoke): toggles, raises Click, runs the command, closes the menu.</summary>
    internal void AutomationClick() => OnClick();

    /// <summary>
    /// The menu the item belongs to: its logical parent when it is written out in the menu (the panel only hosts it
    /// visually), or the menu whose items host generated it from <see cref="ItemsControl.ItemsSource"/>.
    /// </summary>
    private GlassMenu? Menu()
    {
        for (var node = LogicalTreeHelper.GetParent(this); node is not null; node = LogicalTreeHelper.GetParent(node))
            if (node is GlassMenu menu) return menu;
        return ItemsControl.ItemsControlFromItemContainer(this) is GlassMenuItemsHost host ? host.Owner : null;
    }

    private static void OnHeaderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // The template shows Content: the header fills it, and keeps it current, unless the application set content.
        var item = (GlassMenuItem)d;
        if (item.Content is not null && !item._contentFromHeader) return;
        item._copyingHeader = true;
        try
        {
            item.SetCurrentValue(ContentProperty, e.NewValue);
        }
        finally
        {
            item._copyingHeader = false;
        }
        item._contentFromHeader = e.NewValue is not null;
    }

    private static void OnIsCheckedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => (UIElementAutomationPeer.FromElement((GlassMenuItem)d) as GlassMenuItemAutomationPeer)?.RaiseToggleChanged((bool)e.OldValue, (bool)e.NewValue);
}

/// <summary>
/// Exposes a <see cref="GlassMenu"/> to UI Automation. The menu's focusable trigger button is the element screen readers
/// see (a button with ExpandCollapse); this peer wraps it in the raw view only and supports ExpandCollapse as well.
/// </summary>
public sealed class GlassMenuAutomationPeer : FrameworkElementAutomationPeer, IExpandCollapseProvider
{
    /// <summary>Creates the peer.</summary>
    public GlassMenuAutomationPeer(GlassMenu owner) : base(owner)
    {
    }

    private GlassMenu Menu => (GlassMenu)Owner;

    /// <inheritdoc />
    public ExpandCollapseState ExpandCollapseState => Menu.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    /// <inheritdoc />
    public void Expand() => Menu.SetCurrentValue(GlassMenu.IsOpenProperty, true);

    /// <inheritdoc />
    public void Collapse() => Menu.SetCurrentValue(GlassMenu.IsOpenProperty, false);

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
        => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Button;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassMenu);

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? Menu.Header?.ToString() ?? string.Empty : name;
    }

    /// <inheritdoc />
    /// <remarks>False: the trigger button inside is the control, so the control view has one button, not two.</remarks>
    protected override bool IsControlElementCore() => false;

    /// <inheritdoc />
    protected override bool IsContentElementCore() => false;
}

/// <summary>The trigger of a <see cref="GlassMenu"/>: a button with the menu's ExpandCollapse state, where keyboard focus is.</summary>
internal sealed class GlassMenuTriggerAutomationPeer : ButtonAutomationPeer, IExpandCollapseProvider
{
    private readonly GlassMenu _menu;

    public GlassMenuTriggerAutomationPeer(GlassButton owner, GlassMenu menu) : base(owner) => _menu = menu;

    public ExpandCollapseState ExpandCollapseState => _menu.IsOpen ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed;

    public void Expand()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        _menu.SetCurrentValue(GlassMenu.IsOpenProperty, true);
    }

    public void Collapse()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        _menu.SetCurrentValue(GlassMenu.IsOpenProperty, false);
    }

    public override object? GetPattern(PatternInterface patternInterface)
        => patternInterface == PatternInterface.ExpandCollapse ? this : base.GetPattern(patternInterface);

    internal void RaiseExpandCollapseChanged(bool oldValue, bool newValue)
        => RaisePropertyChangedEvent(ExpandCollapsePatternIdentifiers.ExpandCollapseStateProperty,
            oldValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed,
            newValue ? ExpandCollapseState.Expanded : ExpandCollapseState.Collapsed);

    protected override string GetClassNameCore() => nameof(GlassMenu);
}

/// <summary>The panel of an open <see cref="GlassMenu"/>: glass in the scene's popup layer, a Menu to UI Automation.</summary>
internal sealed class GlassMenuPanel : GlassSurface
{
    protected override AutomationPeer OnCreateAutomationPeer() => new PanelPeer(this);

    private sealed class PanelPeer : FrameworkElementAutomationPeer
    {
        public PanelPeer(GlassMenuPanel owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Menu;

        protected override string GetClassNameCore() => nameof(GlassMenu);
    }
}

/// <summary>
/// Shows a <see cref="GlassMenu"/>'s items in its panel: items written out are their own containers; data items from
/// <see cref="ItemsControl.ItemsSource"/> get <see cref="GlassMenuItem"/> containers with the menu's item template and
/// container style.
/// </summary>
internal sealed class GlassMenuItemsHost : ItemsControl
{
    public GlassMenuItemsHost(GlassMenu owner)
    {
        Owner = owner;
        Focusable = false;
        ItemsPanel = new ItemsPanelTemplate(new FrameworkElementFactory(typeof(StackPanel)));
        foreach (var property in new[] { ItemTemplateProperty, ItemTemplateSelectorProperty, ItemStringFormatProperty, ItemContainerStyleProperty, ItemContainerStyleSelectorProperty })
            SetBinding(property, new Binding(property.Name) { Source = owner, Mode = BindingMode.OneWay });
        ItemsSource = owner.Items;
    }

    public GlassMenu Owner { get; }

    protected override bool IsItemItsOwnContainerOverride(object item) => item is UIElement;

    protected override DependencyObject GetContainerForItemOverride() => new GlassMenuItem();

    /// <summary>The item container style is for items: separators and other elements written out keep their own.</summary>
    protected override bool ShouldApplyItemContainerStyle(DependencyObject container, object item)
        => container is GlassMenuItem && base.ShouldApplyItemContainerStyle(container, item);

    /// <summary>The items sit directly in the Menu: this host is not an element of its own.</summary>
    protected override AutomationPeer OnCreateAutomationPeer() => new HostPeer(this);

    private sealed class HostPeer : FrameworkElementAutomationPeer
    {
        public HostPeer(GlassMenuItemsHost owner) : base(owner)
        {
        }

        protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Group;

        protected override bool IsControlElementCore() => false;

        protected override bool IsContentElementCore() => false;
    }
}

/// <summary>Exposes a <see cref="GlassMenuItem"/> to UI Automation as a menu item that can be invoked and, when checkable, toggled.</summary>
public sealed class GlassMenuItemAutomationPeer : FrameworkElementAutomationPeer, IInvokeProvider, IToggleProvider
{
    /// <summary>Creates the peer.</summary>
    public GlassMenuItemAutomationPeer(GlassMenuItem owner) : base(owner)
    {
    }

    private GlassMenuItem Item => (GlassMenuItem)Owner;

    /// <inheritdoc />
    public ToggleState ToggleState => Item.IsChecked ? ToggleState.On : ToggleState.Off;

    /// <inheritdoc />
    /// <remarks>Chooses the item exactly as a click does, on the dispatcher (as WPF's own button peer does).</remarks>
    public void Invoke()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        Item.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(Item.AutomationClick));
    }

    /// <inheritdoc />
    public void Toggle()
    {
        if (!IsEnabled()) throw new ElementNotEnabledException();
        Item.SetCurrentValue(GlassMenuItem.IsCheckedProperty, !Item.IsChecked);
    }

    /// <inheritdoc />
    public override object? GetPattern(PatternInterface patternInterface) => patternInterface switch
    {
        PatternInterface.Invoke => this,
        PatternInterface.Toggle when Item.IsCheckable => this,
        _ => base.GetPattern(patternInterface),
    };

    internal void RaiseToggleChanged(bool oldValue, bool newValue)
        => RaisePropertyChangedEvent(TogglePatternIdentifiers.ToggleStateProperty,
            oldValue ? ToggleState.On : ToggleState.Off, newValue ? ToggleState.On : ToggleState.Off);

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.MenuItem;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassMenuItem);

    /// <inheritdoc />
    protected override string GetNameCore()
    {
        var name = base.GetNameCore();
        return string.IsNullOrEmpty(name) ? Item.Header?.ToString() ?? Item.Content?.ToString() ?? string.Empty : name;
    }
}
