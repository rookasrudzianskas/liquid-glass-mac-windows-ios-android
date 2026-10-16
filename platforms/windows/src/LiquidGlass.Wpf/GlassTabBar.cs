using System;
using System.Linq;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A capsule tab bar: a Clear glass lens slides between the items on the track spring and stretches with its speed
/// (COMPONENTS §4). The lens can be dragged across the bar to choose an item; arrow keys move the selection; the
/// selected label is at full opacity and the others at 0.85. Exposes the Tab and TabItem automation roles.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassTabBar SelectedIndex="0">
///   <lg:GlassTabBarItem Content="Library" />
///   <lg:GlassTabBarItem Content="For You" />
///   <lg:GlassTabBarItem Content="Search" />
/// </lg:GlassTabBar>
/// ]]></code>
/// </example>
[TemplatePart(Name = SurfacePart, Type = typeof(GlassSurface))]
[TemplatePart(Name = LensPart, Type = typeof(GlassSurface))]
[TemplatePart(Name = LensHostPart, Type = typeof(Canvas))]
public class GlassTabBar : ListBox, IGlassAnimation
{
    /// <summary>Material of the bar (inherited).</summary>
    public static readonly DependencyProperty MaterialProperty = GlassSurface.MaterialProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(GlassMaterial.Clear, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Appearance of the bar (inherited).</summary>
    public static readonly DependencyProperty AppearanceProperty = GlassSurface.AppearanceProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(GlassAppearance.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Corner radius of the bar. Default <c>capsule</c>.</summary>
    public static readonly DependencyProperty RadiusProperty = GlassSurface.RadiusProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(GlassRadius.Capsule));

    /// <summary>Refraction of the bar. Default 60.</summary>
    public static readonly DependencyProperty RefractionProperty = GlassSurface.RefractionProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(GlassOptics.DefaultRefraction));

    /// <summary>Tint of the bar and its lens, or null.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(null, OnTintChanged));

    /// <summary>Press response of the bar. Default true.</summary>
    public static readonly DependencyProperty IsInteractiveProperty = GlassSurface.IsInteractiveProperty.AddOwner(typeof(GlassTabBar),
        new FrameworkPropertyMetadata(true));

    private const string SurfacePart = "PART_Surface";
    private const string LensPart = "PART_Lens";
    private const string LensHostPart = "PART_LensHost";
    private const double DragThreshold = 4;

    private readonly Spring _pos = new(0, GlassSprings.Track);
    private readonly Spring _cross = new(0, GlassSprings.Track);
    private readonly Spring _length = new(0, GlassSprings.Track);
    private readonly Spring _thickness = new(0, GlassSprings.Track);
    private readonly ScaleTransform _stretch = new();
    private readonly GlassAppearanceTracker _appearance;
    private GlassSurface? _lens;
    private Canvas? _lensHost;
    private bool _ready;
    private Drag? _drag;

    static GlassTabBar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassTabBar), new FrameworkPropertyMetadata(typeof(GlassTabBar)));
        GlassScene.AddContentForegroundOwner(typeof(GlassTabBar));
        SelectionModeProperty.OverrideMetadata(typeof(GlassTabBar), new FrameworkPropertyMetadata(SelectionMode.Single));
        KeyboardNavigation.DirectionalNavigationProperty.OverrideMetadata(typeof(GlassTabBar), new FrameworkPropertyMetadata(KeyboardNavigationMode.Contained));
    }

    /// <summary>Creates a tab bar.</summary>
    public GlassTabBar()
    {
        _appearance = new GlassAppearanceTracker(this, () => Tint);
        LayoutUpdated += (_, _) => Follow();
        Unloaded += (_, _) => GlassFrameLoop.Stop(this);
    }

    /// <summary>Material of the bar.</summary>
    public GlassMaterial Material
    {
        get => (GlassMaterial)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    /// <summary>Appearance of the bar.</summary>
    public GlassAppearance Appearance
    {
        get => (GlassAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>Corner radius of the bar.</summary>
    public GlassRadius Radius
    {
        get => (GlassRadius)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Refraction of the bar.</summary>
    public double Refraction
    {
        get => (double)GetValue(RefractionProperty);
        set => SetValue(RefractionProperty, value);
    }

    /// <summary>Tint of the bar, or null.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Press response of the bar.</summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    private bool Vertical => Items.Count > 0 && ItemsPanelOrientation() == Orientation.Vertical;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(SurfacePart) is GlassSurface surface) surface.InputSource = this;
        _lens = GetTemplateChild(LensPart) as GlassSurface;
        _lensHost = GetTemplateChild(LensHostPart) as Canvas;
        if (_lens is not null) _lens.RenderTransform = _stretch;
        _ready = false;
    }

    /// <inheritdoc />
    public bool Tick(double dt)
    {
        var target = SelectedBox();
        if (target is not { } box || _lens is null) return false;
        var motion = GlassResolve.Motion(this);
        if (!_ready || motion != GlassMotion.Full)
        {
            Jump(box);
            _ready = box.Length > 0;
        }
        else if (_drag is { Moved: true } drag)
        {
            // Follow the finger, taking on the size of the item passing beneath it.
            var boxes = ItemBoxes();
            var at = Along(Mouse.GetPosition(_lensHost)) - drag.Grab;
            var under = Nearest(at);
            var size = under is { } u ? u.Length : box.Length;
            var min = boxes.Min(b => b.Pos);
            var max = boxes.Max(b => b.Pos + b.Length);
            var center = GlassMath.Clamp(at, min + size / 2, max - size / 2);
            var follow = GlassSprings.Track;
            _pos.Animate(center - size / 2, follow);
            _length.Animate(size, follow);
            _cross.Animate(box.Cross, follow);
            _thickness.Animate(box.Thickness, follow);
        }
        else
        {
            _pos.Animate(box.Pos, GlassSprings.Track);
            _cross.Animate(box.Cross, GlassSprings.Track);
            _length.Animate(box.Length, GlassSprings.Track);
            _thickness.Animate(box.Thickness, GlassSprings.Track);
        }
        foreach (var spring in new[] { _pos, _cross, _length, _thickness }) spring.Step(dt);
        Place();
        // The lens stretches along its path in proportion to its speed.
        var stretch = motion == GlassMotion.Full ? Math.Min(0.2, Math.Abs(_pos.Velocity) * 0.00035) * (_drag is not null ? 1 : 0.6) : 0;
        var along = 1 + stretch;
        var across = 1 / Math.Sqrt(1 + stretch);
        _stretch.ScaleX = Vertical ? across : along;
        _stretch.ScaleY = Vertical ? along : across;
        GlassScene.FindScene(this)?.RequestRealign();
        return !_ready || _drag is not null || new[] { _pos, _cross, _length, _thickness }.Any(s => !s.IsSettled);
    }

    /// <inheritdoc />
    protected override DependencyObject GetContainerForItemOverride() => new GlassTabBarItem();

    /// <inheritdoc />
    protected override bool IsItemItsOwnContainerOverride(object item) => item is GlassTabBarItem;

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlassTabBarAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnSelectionChanged(SelectionChangedEventArgs e)
    {
        base.OnSelectionChanged(e);
        GlassFrameLoop.Start(this);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonDown(e);
        if (_lensHost is null || GlassResolve.Motion(this) == GlassMotion.None) return;
        if (ContainerFromElement((DependencyObject)e.OriginalSource) is not GlassTabBarItem item || !item.IsEnabled) return;
        var at = Along(e.GetPosition(_lensHost));
        // Grab the lens where it was touched; pressing another item pulls the lens there.
        var grab = item.IsSelected ? at - (_pos.Value + _length.Value / 2) : 0;
        _drag = new Drag(at, grab);
        GlassFrameLoop.Start(this);
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseMove(MouseEventArgs e)
    {
        base.OnPreviewMouseMove(e);
        if (_drag is not { } drag || _lensHost is null) return;
        if (e.LeftButton != MouseButtonState.Pressed)
        {
            _drag = null;
            return;
        }
        if (!drag.Moved && Math.Abs(Along(e.GetPosition(_lensHost)) - drag.Start) > DragThreshold)
        {
            drag.Moved = true;
            CaptureMouse();
        }
    }

    /// <inheritdoc />
    protected override void OnPreviewMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnPreviewMouseLeftButtonUp(e);
        if (_drag is not { } drag) return;
        _drag = null;
        if (!drag.Moved) return;
        if (IsMouseCaptured) ReleaseMouseCapture();
        // The lens decides where it lands: select the item beneath its center.
        if (Nearest(_pos.Value + _length.Value / 2) is { } choice) SelectedIndex = choice.Index;
        e.Handled = true;
        GlassFrameLoop.Start(this);
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_drag is { Moved: true })
        {
            _drag = null;
            GlassFrameLoop.Start(this);
        }
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassTabBar)d)._appearance.Update();

    /// <summary>Restarts the lens animation when layout moved the selected item.</summary>
    private void Follow()
    {
        if (GlassFrameLoop.IsRunning(this) || SelectedBox() is not { } box) return;
        if (!_ready || Math.Abs(box.Pos - _pos.Value) > 0.01 || Math.Abs(box.Length - _length.Value) > 0.01
            || Math.Abs(box.Cross - _cross.Value) > 0.01 || Math.Abs(box.Thickness - _thickness.Value) > 0.01)
            GlassFrameLoop.Start(this);
    }

    private void Jump(ItemBox box)
    {
        _pos.Jump(box.Pos);
        _cross.Jump(box.Cross);
        _length.Jump(box.Length);
        _thickness.Jump(box.Thickness);
    }

    private void Place()
    {
        if (_lens is null) return;
        var vertical = Vertical;
        Canvas.SetLeft(_lens, vertical ? _cross.Value : _pos.Value);
        Canvas.SetTop(_lens, vertical ? _pos.Value : _cross.Value);
        _lens.Width = Math.Max(0, vertical ? _thickness.Value : _length.Value);
        _lens.Height = Math.Max(0, vertical ? _length.Value : _thickness.Value);
        _lens.Visibility = SelectedIndex >= 0 ? Visibility.Visible : Visibility.Hidden;
        _stretch.CenterX = _lens.Width / 2;
        _stretch.CenterY = _lens.Height / 2;
    }

    private double Along(Point p) => Vertical ? p.Y : p.X;

    private ItemBox? SelectedBox() => SelectedIndex >= 0 ? Box(SelectedIndex) : null;

    private ItemBox? Box(int index)
    {
        if (_lensHost is null || ItemContainerGenerator.ContainerFromIndex(index) is not GlassTabBarItem item || !item.IsVisible) return null;
        if (!item.IsDescendantOf(this) || !_lensHost.IsDescendantOf(this)) return null;
        Rect rect;
        try
        {
            rect = item.TransformToVisual(_lensHost).TransformBounds(new Rect(item.RenderSize));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
        return Vertical
            ? new ItemBox(index, rect.Y, rect.X, rect.Height, rect.Width)
            : new ItemBox(index, rect.X, rect.Y, rect.Width, rect.Height);
    }

    private ItemBox[] ItemBoxes()
    {
        var boxes = Enumerable.Range(0, Items.Count).Select(Box).Where(b => b is not null).Select(b => b!.Value).ToArray();
        return boxes.Length > 0 ? boxes : new[] { new ItemBox(-1, 0, 0, 0, 0) };
    }

    /// <summary>The enabled item whose center is nearest <paramref name="center"/> along the bar.</summary>
    private ItemBox? Nearest(double center)
    {
        ItemBox? best = null;
        foreach (var box in ItemBoxes())
        {
            if (box.Index < 0 || ItemContainerGenerator.ContainerFromIndex(box.Index) is not UIElement { IsEnabled: true }) continue;
            if (best is null || Math.Abs(box.Pos + box.Length / 2 - center) < Math.Abs(best.Value.Pos + best.Value.Length / 2 - center)) best = box;
        }
        return best;
    }

    private Orientation ItemsPanelOrientation()
    {
        var container = ItemContainerGenerator.ContainerFromIndex(0) as FrameworkElement;
        return container is not null && VisualTreeHelper.GetParent(container) is StackPanel panel ? panel.Orientation : Orientation.Horizontal;
    }

    private readonly struct ItemBox
    {
        public ItemBox(int index, double pos, double cross, double length, double thickness)
        {
            Index = index;
            Pos = pos;
            Cross = cross;
            Length = length;
            Thickness = thickness;
        }

        public int Index { get; }

        public double Pos { get; }

        public double Cross { get; }

        public double Length { get; }

        public double Thickness { get; }
    }

    private sealed class Drag
    {
        public Drag(double start, double grab)
        {
            Start = start;
            Grab = grab;
        }

        public double Start { get; }

        public double Grab { get; }

        public bool Moved { get; set; }
    }
}

/// <summary>An item of a <see cref="GlassTabBar"/>: a label at 0.85 opacity, full opacity when selected.</summary>
public class GlassTabBarItem : ListBoxItem
{
    static GlassTabBarItem()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassTabBarItem), new FrameworkPropertyMetadata(typeof(GlassTabBarItem)));
    }

    /// <summary>Creates an item.</summary>
    public GlassTabBarItem()
    {
        GlassFocus.Track(this);
    }
}

/// <summary>Exposes a <see cref="GlassTabBar"/> to UI Automation as a tab list.</summary>
public sealed class GlassTabBarAutomationPeer : ListBoxAutomationPeer
{
    /// <summary>Creates the peer.</summary>
    public GlassTabBarAutomationPeer(GlassTabBar owner) : base(owner)
    {
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Tab;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassTabBar);

    /// <inheritdoc />
    protected override ItemAutomationPeer CreateItemAutomationPeer(object item) => new GlassTabBarItemAutomationPeer(item, this);
}

/// <summary>Exposes an item of a <see cref="GlassTabBar"/> to UI Automation as a tab item.</summary>
public sealed class GlassTabBarItemAutomationPeer : ListBoxItemAutomationPeer
{
    /// <summary>Creates the peer.</summary>
    public GlassTabBarItemAutomationPeer(object owner, SelectorAutomationPeer selectorAutomationPeer) : base(owner, selectorAutomationPeer)
    {
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.TabItem;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassTabBarItem);
}
