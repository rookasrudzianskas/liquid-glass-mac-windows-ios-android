using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A floating toolbar: its items sit on capsule glass 40 units tall with a 4-unit inset. A
/// <see cref="GlassToolbarSpacer"/> splits the toolbar into separate glass clusters; clusters spring to their new size
/// when items come and go. Plain <see cref="System.Windows.Controls.Button"/>s and
/// <see cref="System.Windows.Controls.Primitives.ToggleButton"/>s inside take the toolbar button style (no glass of
/// their own, a soft highlight in the foreground color, and a focus ring in that color for keyboard focus); pressing
/// anywhere on a cluster presses its glass.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassToolbar>
///   <Button Content="&#xE72B;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" AutomationProperties.Name="Back" ToolTip="Back" />
///   <Button Content="&#xE72A;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" AutomationProperties.Name="Forward" ToolTip="Forward" />
///   <lg:GlassToolbarSpacer />
///   <Button Content="&#xE72D;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" AutomationProperties.Name="Share" ToolTip="Share" />
/// </lg:GlassToolbar>
/// ]]></code>
/// </example>
[ContentProperty(nameof(Items))]
[TemplatePart(Name = HostPart, Type = typeof(Grid))]
public class GlassToolbar : Control
{
    /// <summary>Material of the clusters (inherited).</summary>
    public static readonly DependencyProperty MaterialProperty = GlassSurface.MaterialProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(GlassMaterial.Clear, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Appearance of the clusters (inherited).</summary>
    public static readonly DependencyProperty AppearanceProperty = GlassSurface.AppearanceProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(GlassAppearance.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Motion of the clusters (inherited).</summary>
    public static readonly DependencyProperty MotionProperty = GlassSurface.MotionProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(GlassMotion.Full, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Corner radius of the clusters. Default <c>capsule</c>.</summary>
    public static readonly DependencyProperty RadiusProperty = GlassSurface.RadiusProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(GlassRadius.Capsule, OnClusterOptionChanged));

    /// <summary>Refraction of the clusters. Default 60.</summary>
    public static readonly DependencyProperty RefractionProperty = GlassSurface.RefractionProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(GlassOptics.DefaultRefraction, OnClusterOptionChanged));

    /// <summary>Tint of the clusters, or null.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(null, OnClusterOptionChanged));

    /// <summary>Press, drag and glow response of the clusters. Default true.</summary>
    public static readonly DependencyProperty IsInteractiveProperty = GlassSurface.IsInteractiveProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(true, OnClusterOptionChanged));

    /// <summary>Lays the items out in a row (default) or a column.</summary>
    public static readonly DependencyProperty OrientationProperty = StackPanel.OrientationProperty.AddOwner(typeof(GlassToolbar),
        new FrameworkPropertyMetadata(Orientation.Horizontal, OnLayoutChanged));

    /// <summary>Space between the glass and the items inside it. Default 4.</summary>
    public static readonly DependencyProperty InsetProperty = DependencyProperty.Register(
        nameof(Inset), typeof(double), typeof(GlassToolbar), new FrameworkPropertyMetadata(4.0, OnClusterOptionChanged));

    /// <summary>Space between neighboring items. Default 4.</summary>
    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(GlassToolbar), new FrameworkPropertyMetadata(4.0, OnLayoutChanged));

    private const string HostPart = "PART_Host";

    private readonly GlassAppearanceTracker _appearance;
    private Grid? _host;

    static GlassToolbar()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassToolbar), new FrameworkPropertyMetadata(typeof(GlassToolbar)));
        GlassScene.AddContentForegroundOwner(typeof(GlassToolbar));
        FocusableProperty.OverrideMetadata(typeof(GlassToolbar), new FrameworkPropertyMetadata(false));
        KeyboardNavigation.TabNavigationProperty.OverrideMetadata(typeof(GlassToolbar), new FrameworkPropertyMetadata(KeyboardNavigationMode.Continue));
        // A focus scope, as WPF's ToolBar is: a button's routed command (Copy, Paste) that nothing in the toolbar handles
        // goes on to the element that has focus in the window, and clicking a button leaves that element focused there.
        FocusManager.IsFocusScopeProperty.OverrideMetadata(typeof(GlassToolbar), new FrameworkPropertyMetadata(true));
        // Plain buttons in the toolbar draw their focus ring in their template (in the glass's foreground); show it for
        // keyboard focus only, as the glass controls do.
        EventManager.RegisterClassHandler(typeof(GlassToolbar), Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnItemGotFocus));
        EventManager.RegisterClassHandler(typeof(GlassToolbar), Keyboard.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnItemLostFocus));
        EventManager.RegisterClassHandler(typeof(GlassToolbar), Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(OnItemPreviewMouseDown));
        EventManager.RegisterClassHandler(typeof(GlassToolbar), Keyboard.KeyDownEvent, new KeyEventHandler(OnItemKeyDown), true);
    }

    /// <summary>Creates a toolbar.</summary>
    public GlassToolbar()
    {
        _appearance = new GlassAppearanceTracker(this, () => Tint);
        Items.CollectionChanged += OnItemsChanged;
    }

    /// <summary>The toolbar's controls and spacers, in order.</summary>
    public ObservableCollection<UIElement> Items { get; } = new();

    /// <summary>Material of the clusters.</summary>
    public GlassMaterial Material
    {
        get => (GlassMaterial)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    /// <summary>Appearance of the clusters.</summary>
    public GlassAppearance Appearance
    {
        get => (GlassAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>Motion of the clusters.</summary>
    public GlassMotion Motion
    {
        get => (GlassMotion)GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    /// <summary>Corner radius of the clusters.</summary>
    public GlassRadius Radius
    {
        get => (GlassRadius)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Refraction of the clusters.</summary>
    public double Refraction
    {
        get => (double)GetValue(RefractionProperty);
        set => SetValue(RefractionProperty, value);
    }

    /// <summary>Tint of the clusters, or null.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Press, drag and glow response of the clusters.</summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Row or column.</summary>
    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    /// <summary>Space between the glass and the items inside it.</summary>
    public double Inset
    {
        get => (double)GetValue(InsetProperty);
        set => SetValue(InsetProperty, value);
    }

    /// <summary>Space between neighboring items.</summary>
    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _host = GetTemplateChild(HostPart) as Grid;
        Rebuild();
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlassToolbarAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    private static void OnItemGotFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.NewFocus is ButtonBase button) GlassScene.SetIsFocusVisible(button, InputManager.Current.MostRecentInputDevice is KeyboardDevice);
    }

    private static void OnItemLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (e.OldFocus is ButtonBase button) GlassScene.SetIsFocusVisible(button, false);
    }

    private static void OnItemPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (Keyboard.FocusedElement is ButtonBase button && ((GlassToolbar)sender).IsAncestorOf(button)) GlassScene.SetIsFocusVisible(button, false);
    }

    private static void OnItemKeyDown(object sender, KeyEventArgs e)
    {
        // Keys go to the focused element: a navigation key on a button that was focused with the mouse shows its ring.
        if (e.Key is Key.Tab or Key.Left or Key.Right or Key.Up or Key.Down && e.OriginalSource is ButtonBase button)
            GlassScene.SetIsFocusVisible(button, true);
    }

    private static void OnClusterOptionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var toolbar = (GlassToolbar)d;
        if (e.Property == TintProperty) toolbar._appearance.Update();
        toolbar.Rebuild();
    }

    private static void OnLayoutChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassToolbar)d).Rebuild();

    private void OnItemsChanged(object? sender, NotifyCollectionChangedEventArgs e) => Rebuild();

    /// <summary>Lays the clusters out again (a spacer changed).</summary>
    internal void Refresh() => Rebuild();

    /// <summary>Groups the items between spacers into glass clusters, reusing clusters so their glass can spring.</summary>
    private void Rebuild()
    {
        if (_host is null) return;
        var horizontal = Orientation == Orientation.Horizontal;
        var existing = new System.Collections.Generic.List<GlassSurface>();
        foreach (UIElement child in _host.Children)
        {
            if (child is GlassSurface cluster)
            {
                ((Panel)cluster.Content).Children.Clear();
                existing.Add(cluster);
            }

        }
        _host.Children.Clear();
        _host.ColumnDefinitions.Clear();
        _host.RowDefinitions.Clear();

        var index = 0;
        var clusterCount = 0;
        SpacedStackPanel? panel = null;
        void Add(UIElement element, GridLength length)
        {
            if (horizontal)
            {
                _host.ColumnDefinitions.Add(new ColumnDefinition { Width = length });
                Grid.SetColumn(element, index);
                Grid.SetRow(element, 0);
            }
            else
            {
                _host.RowDefinitions.Add(new RowDefinition { Height = length });
                Grid.SetRow(element, index);
                Grid.SetColumn(element, 0);
            }
            _host.Children.Add(element);
            index++;
        }
        foreach (var item in Items)
        {
            if (item is GlassToolbarSpacer spacer)
            {
                panel = null;
                Add(spacer, spacer.Sizing == GlassSpacerSizing.Flexible ? new GridLength(1, GridUnitType.Star) : GridLength.Auto);
                continue;
            }
            if (panel is null)
            {
                var cluster = clusterCount < existing.Count ? existing[clusterCount] : NewCluster();
                clusterCount++;
                Configure(cluster);
                panel = (SpacedStackPanel)cluster.Content;
                panel.Orientation = Orientation;
                panel.Spacing = Spacing;
                Add(cluster, GridLength.Auto);
            }
            panel.Children.Add(item);
        }
    }

    private static GlassSurface NewCluster() => new()
    {
        IsFluid = true,
        Content = new SpacedStackPanel(),
        HorizontalContentAlignment = HorizontalAlignment.Left,
        VerticalContentAlignment = VerticalAlignment.Center,
    };

    private void Configure(GlassSurface cluster)
    {
        cluster.Radius = Radius;
        cluster.Refraction = Refraction;
        cluster.Tint = Tint;
        cluster.IsInteractive = IsInteractive;
        cluster.Padding = new Thickness(Inset);
        cluster.MinHeight = Orientation == Orientation.Horizontal ? 40 : 0;
        cluster.MinWidth = Orientation == Orientation.Vertical ? 40 : 0;
    }
}

/// <summary>
/// Splits a <see cref="GlassToolbar"/> into separate glass clusters: a flexible spacer fills the available space,
/// a fixed one leaves a standard 8-unit gap.
/// </summary>
public class GlassToolbarSpacer : FrameworkElement
{
    /// <summary>Flexible (default) or fixed.</summary>
    public static readonly DependencyProperty SizingProperty = DependencyProperty.Register(
        nameof(Sizing), typeof(GlassSpacerSizing), typeof(GlassToolbarSpacer),
        new FrameworkPropertyMetadata(GlassSpacerSizing.Flexible, FrameworkPropertyMetadataOptions.AffectsParentMeasure, OnSizingChanged));

    /// <summary>Width (or height) of a fixed spacer.</summary>
    public const double FixedGap = 8;

    static GlassToolbarSpacer()
    {
        FocusableProperty.OverrideMetadata(typeof(GlassToolbarSpacer), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Flexible or fixed.</summary>
    public GlassSpacerSizing Sizing
    {
        get => (GlassSpacerSizing)GetValue(SizingProperty);
        set => SetValue(SizingProperty, value);
    }

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize) => new(FixedGap, FixedGap);

    private static void OnSizingChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        // The toolbar sizes the spacer's column: star for flexible spacers, the fixed gap otherwise.
        if (VisualTreeHelper.GetParent(d) is Grid { TemplatedParent: GlassToolbar toolbar }) toolbar.Refresh();
    }
}

/// <summary>Exposes a <see cref="GlassToolbar"/> to UI Automation as a tool bar.</summary>
public sealed class GlassToolbarAutomationPeer : FrameworkElementAutomationPeer
{
    /// <summary>Creates the peer.</summary>
    public GlassToolbarAutomationPeer(GlassToolbar owner) : base(owner)
    {
    }

    /// <inheritdoc />
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.ToolBar;

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassToolbar);
}
