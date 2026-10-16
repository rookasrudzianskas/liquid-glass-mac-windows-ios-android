using System;
using System.Collections;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// The region glass may refract. The scene draws its <see cref="Content"/> once as the backdrop; every glass surface
/// among its <see cref="Children"/> (at any depth) refracts that backdrop at its exact on-screen position, including
/// while it moves, scales or animates. The scene's attached properties set the defaults its glass inherits.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassScene lg:GlassScene.Material="Regular">
///   <lg:GlassScene.Content>
///     <Image Source="wallpaper.jpg" Stretch="UniformToFill" />
///   </lg:GlassScene.Content>
///   <lg:GlassSurface Width="320" Height="200">
///     <TextBlock Text="Hello, glass" />
///   </lg:GlassSurface>
/// </lg:GlassScene>
/// ]]></code>
/// </example>
[ContentProperty(nameof(Children))]
public class GlassScene : FrameworkElement
{
    /// <summary>The backdrop the glass refracts.</summary>
    public static readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(UIElement), typeof(GlassScene), new FrameworkPropertyMetadata(null, OnContentChanged));

    /// <summary>Material of the glass inside the element (inherited). Default <see cref="GlassMaterial.Clear"/>.</summary>
    public static readonly DependencyProperty MaterialProperty = DependencyProperty.RegisterAttached(
        "Material", typeof(GlassMaterial), typeof(GlassScene),
        new FrameworkPropertyMetadata(GlassMaterial.Clear, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Appearance of the glass inside the element (inherited). Default <see cref="GlassAppearance.Automatic"/>.</summary>
    public static readonly DependencyProperty AppearanceProperty = DependencyProperty.RegisterAttached(
        "Appearance", typeof(GlassAppearance), typeof(GlassScene),
        new FrameworkPropertyMetadata(GlassAppearance.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Motion of the glass inside the element (inherited). The system's animation setting always applies.</summary>
    public static readonly DependencyProperty MotionProperty = DependencyProperty.RegisterAttached(
        "Motion", typeof(GlassMotion), typeof(GlassScene),
        new FrameworkPropertyMetadata(GlassMotion.Full, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>How the glass inside the element is drawn (inherited). Default <see cref="GlassRenderMode.Automatic"/>.</summary>
    public static readonly DependencyProperty RenderModeProperty = DependencyProperty.RegisterAttached(
        "RenderMode", typeof(GlassRenderMode), typeof(GlassScene),
        new FrameworkPropertyMetadata(GlassRenderMode.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>
    /// Replaces the material with an opaque fill (inherited). <c>null</c> (the default) follows the Windows
    /// transparency effects setting.
    /// </summary>
    public static readonly DependencyProperty ReduceTransparencyProperty = DependencyProperty.RegisterAttached(
        "ReduceTransparency", typeof(bool?), typeof(GlassScene),
        new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Adds a 1-unit inner border and uses the Regular material (inherited). Default false.</summary>
    public static readonly DependencyProperty IncreaseContrastProperty = DependencyProperty.RegisterAttached(
        "IncreaseContrast", typeof(bool), typeof(GlassScene),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.Inherits));

    private static readonly DependencyPropertyKey IsDarkPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsDark", typeof(bool), typeof(GlassScene), new FrameworkPropertyMetadata(false));

    /// <summary>
    /// Whether a glass control currently draws dark glass: its <c>Appearance</c> resolved against the Windows app theme.
    /// Glass controls maintain it on themselves; use it in styles and triggers.
    /// </summary>
    public static readonly DependencyProperty IsDarkProperty = IsDarkPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey ContentForegroundPropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "ContentForeground", typeof(Brush), typeof(GlassScene), new FrameworkPropertyMetadata(GlassBrushes.ForegroundLight));

    /// <summary>
    /// The default text and icon color on a glass control (<c>#111113</c> on light glass, white on dark or tinted glass).
    /// Glass control styles bind <c>Foreground</c> to it; setting <c>Foreground</c> overrides it.
    /// </summary>
    public static readonly DependencyProperty ContentForegroundProperty = ContentForegroundPropertyKey.DependencyProperty;

    private static readonly DependencyPropertyKey IsFocusVisiblePropertyKey = DependencyProperty.RegisterAttachedReadOnly(
        "IsFocusVisible", typeof(bool), typeof(GlassScene), new FrameworkPropertyMetadata(false));

    /// <summary>
    /// Whether a glass control has keyboard focus that arrived from the keyboard; its template shows the focus ring
    /// while this is true.
    /// </summary>
    public static readonly DependencyProperty IsFocusVisibleProperty = IsFocusVisiblePropertyKey.DependencyProperty;

    private readonly Border _contentHost = new() { ClipToBounds = true };
    private readonly Grid _overlay = new();
    private readonly Canvas _popupLayer = new();
    private readonly HashSet<GlassLens> _lenses = new();
    private readonly List<GlassProgressiveBlur> _backdropLayers = new();
    private bool _realignRequested;

    static GlassScene()
    {
        ClipToBoundsProperty.OverrideMetadata(typeof(GlassScene), new FrameworkPropertyMetadata(true));
        SnapsToDevicePixelsProperty.OverrideMetadata(typeof(GlassScene), new FrameworkPropertyMetadata(true));
    }

    /// <summary>Creates an empty scene.</summary>
    public GlassScene()
    {
        AddVisualChild(_contentHost);
        AddLogicalChild(_contentHost);
        AddVisualChild(_overlay);
        AddLogicalChild(_overlay);
        AddVisualChild(_popupLayer);
        AddLogicalChild(_popupLayer);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>The backdrop the glass refracts. Glass must not be placed inside it.</summary>
    public UIElement? Content
    {
        get => (UIElement?)GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>Elements drawn over the backdrop: glass surfaces, controls and any other content. Laid out like a single-cell <see cref="Grid"/>.</summary>
    public UIElementCollection Children => _overlay.Children;

    /// <summary>The element that holds <see cref="Content"/>; glass refracts what it draws.</summary>
    internal UIElement BackdropHost => _contentHost;

    /// <summary>The top layer that hosts menus and other glass that floats above the scene's children.</summary>
    internal Canvas PopupLayer => _popupLayer;

    /// <summary>Progressive blurs that draw over the backdrop, in the order they were registered.</summary>
    internal IReadOnlyList<GlassProgressiveBlur> BackdropLayers => _backdropLayers;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 3;

    /// <inheritdoc />
    protected override IEnumerator LogicalChildren => new UIElement[] { _contentHost, _overlay, _popupLayer }.GetEnumerator();

    /// <summary>Gets the inherited material.</summary>
    public static GlassMaterial GetMaterial(DependencyObject element) => (GlassMaterial)Require(element).GetValue(MaterialProperty);

    /// <summary>Sets the material for the glass inside <paramref name="element"/>.</summary>
    public static void SetMaterial(DependencyObject element, GlassMaterial value) => Require(element).SetValue(MaterialProperty, value);

    /// <summary>Gets the inherited appearance.</summary>
    public static GlassAppearance GetAppearance(DependencyObject element) => (GlassAppearance)Require(element).GetValue(AppearanceProperty);

    /// <summary>Sets the appearance for the glass inside <paramref name="element"/>.</summary>
    public static void SetAppearance(DependencyObject element, GlassAppearance value) => Require(element).SetValue(AppearanceProperty, value);

    /// <summary>Gets the inherited motion.</summary>
    public static GlassMotion GetMotion(DependencyObject element) => (GlassMotion)Require(element).GetValue(MotionProperty);

    /// <summary>Sets the motion for the glass inside <paramref name="element"/>.</summary>
    public static void SetMotion(DependencyObject element, GlassMotion value) => Require(element).SetValue(MotionProperty, value);

    /// <summary>Gets the inherited render mode.</summary>
    public static GlassRenderMode GetRenderMode(DependencyObject element) => (GlassRenderMode)Require(element).GetValue(RenderModeProperty);

    /// <summary>Sets how the glass inside <paramref name="element"/> is drawn.</summary>
    public static void SetRenderMode(DependencyObject element, GlassRenderMode value) => Require(element).SetValue(RenderModeProperty, value);

    /// <summary>Gets the inherited reduce-transparency override (<c>null</c> follows Windows).</summary>
    public static bool? GetReduceTransparency(DependencyObject element) => (bool?)Require(element).GetValue(ReduceTransparencyProperty);

    /// <summary>Overrides the Windows transparency setting for the glass inside <paramref name="element"/>.</summary>
    public static void SetReduceTransparency(DependencyObject element, bool? value) => Require(element).SetValue(ReduceTransparencyProperty, value);

    /// <summary>Gets the inherited increase-contrast setting.</summary>
    public static bool GetIncreaseContrast(DependencyObject element) => (bool)Require(element).GetValue(IncreaseContrastProperty);

    /// <summary>Turns increased contrast on or off for the glass inside <paramref name="element"/>.</summary>
    public static void SetIncreaseContrast(DependencyObject element, bool value) => Require(element).SetValue(IncreaseContrastProperty, value);

    /// <summary>Whether a glass control draws dark glass.</summary>
    public static bool GetIsDark(DependencyObject element) => (bool)Require(element).GetValue(IsDarkProperty);

    /// <summary>The default foreground of a glass control.</summary>
    public static Brush GetContentForeground(DependencyObject element) => (Brush)Require(element).GetValue(ContentForegroundProperty);

    /// <summary>Whether a glass control shows its keyboard focus ring.</summary>
    public static bool GetIsFocusVisible(DependencyObject element) => (bool)Require(element).GetValue(IsFocusVisibleProperty);

    internal static void SetIsFocusVisible(DependencyObject element, bool value) => element.SetValue(IsFocusVisiblePropertyKey, value);

    internal static void SetIsDark(DependencyObject element, bool value) => element.SetValue(IsDarkPropertyKey, value);

    internal static void SetContentForeground(DependencyObject element, Brush value) => element.SetValue(ContentForegroundPropertyKey, value);

    /// <summary>The nearest scene that contains <paramref name="element"/>, or null.</summary>
    public static GlassScene? FindScene(DependencyObject? element)
    {
        for (var node = element; node is not null; node = ParentOf(node))
            if (node is GlassScene scene) return scene;
        return null;
    }

    /// <summary>
    /// Re-aligns every surface with the backdrop. Layout changes and the library's own animations do this
    /// automatically; call it after moving glass with a render transform animated outside the library.
    /// </summary>
    public void InvalidateGlass() => Realign();

    internal void Register(GlassLens lens)
    {
        if (_lenses.Add(lens)) lens.Realign();
    }

    internal void Unregister(GlassLens lens) => _lenses.Remove(lens);

    internal void Register(GlassProgressiveBlur layer)
    {
        if (_backdropLayers.Contains(layer)) return;
        _backdropLayers.Add(layer);
        foreach (var lens in _lenses) lens.InvalidateSources();
    }

    internal void Unregister(GlassProgressiveBlur layer)
    {
        if (!_backdropLayers.Remove(layer)) return;
        foreach (var lens in _lenses) lens.InvalidateSources();
    }

    /// <summary>Marks every other lens's copies for rebuilding (a lens started or stopped drawing glass).</summary>
    internal void InvalidateSourcesExcept(GlassLens changed)
    {
        foreach (var lens in _lenses)
            if (!ReferenceEquals(lens, changed)) lens.MarkSourcesDirty();
        InvalidateArrange();
    }

    /// <summary>Asks for the surfaces to be re-aligned after the current animation frame.</summary>
    internal void RequestRealign() => _realignRequested = true;

    /// <summary>Whether <paramref name="element"/> is part of the backdrop (glass there would refract itself).</summary>
    internal bool IsInBackdrop(DependencyObject element)
    {
        for (var node = element; node is not null && !ReferenceEquals(node, this); node = ParentOf(node))
            if (ReferenceEquals(node, _contentHost)) return true;
        return false;
    }

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => _contentHost,
        1 => _overlay,
        2 => _popupLayer,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _contentHost.Measure(availableSize);
        _overlay.Measure(availableSize);
        _popupLayer.Measure(availableSize);
        return new Size(
            Math.Max(_contentHost.DesiredSize.Width, _overlay.DesiredSize.Width),
            Math.Max(_contentHost.DesiredSize.Height, _overlay.DesiredSize.Height));
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        var bounds = new Rect(finalSize);
        _contentHost.Arrange(bounds);
        _overlay.Arrange(bounds);
        _popupLayer.Arrange(bounds);
        return finalSize;
    }

    private static DependencyObject? ParentOf(DependencyObject node)
        => (node is Visual ? VisualTreeHelper.GetParent(node) : null) ?? LogicalTreeHelper.GetParent(node);

    private static DependencyObject Require(DependencyObject element)
        => element ?? throw new ArgumentNullException(nameof(element));

    private static void OnContentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var scene = (GlassScene)d;
        scene._contentHost.Child = (UIElement?)e.NewValue;
        foreach (var lens in scene._lenses) lens.InvalidateSources();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        LayoutUpdated += OnLayoutUpdated;
        GlassFrameLoop.AfterTick(OnAfterTick);
        GlassSystemSettings.Watch(this);
        Realign();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        GlassFrameLoop.RemoveAfterTick(OnAfterTick);
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Realign();

    private void OnAfterTick()
    {
        if (!_realignRequested) return;
        Realign();
    }

    private void Realign()
    {
        _realignRequested = false;
        foreach (var lens in _lenses) lens.Realign();
        foreach (var layer in _backdropLayers) layer.Realign();
    }
}
