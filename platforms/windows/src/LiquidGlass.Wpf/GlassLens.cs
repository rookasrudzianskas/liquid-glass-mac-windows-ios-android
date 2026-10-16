using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;
using System.Windows.Shapes;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// The glass of one surface, drawn beneath the surface's content. It holds live copies (<see cref="VisualBrush"/>es)
/// of what lies beneath the surface in its <see cref="GlassScene"/>: the scene's backdrop, progressive blurs and the
/// glass of enclosing surfaces, aligned to the surface's on-screen position and extended past it as far as refraction
/// and frost reach. Where pixel shader 3.0 runs, the glass shader refracts and tones that copy; elsewhere the lens
/// draws the fallback material (platform blur, a fitted veil and the exact rim light).
/// </summary>
/// <remarks>
/// Glass control templates place a <see cref="GlassLens"/> beneath their content and bind its <see cref="Radius"/>,
/// <see cref="Refraction"/> and <see cref="Tint"/>; material, appearance and the accessibility options are inherited.
/// </remarks>
public sealed class GlassLens : FrameworkElement
{
    /// <summary>Corner radius of the outline. Default 16.</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(GlassRadius), typeof(GlassLens), new FrameworkPropertyMetadata(GlassRadius.FromValue(16), OnOpticsChanged));

    /// <summary>Optical travel at the outline, in layout units. Default 60.</summary>
    public static readonly DependencyProperty RefractionProperty = DependencyProperty.Register(
        nameof(Refraction), typeof(double), typeof(GlassLens),
        new FrameworkPropertyMetadata(GlassOptics.DefaultRefraction, OnOpticsChanged), v => v is double d && d >= 0 && !double.IsInfinity(d));

    /// <summary>Tint color of the glass, or null. The color's alpha is ignored; a transparent color means no tint.</summary>
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color?), typeof(GlassLens), new FrameworkPropertyMetadata(null, OnOpticsChanged));

    /// <summary>Draws the keyboard focus ring around the outline.</summary>
    public static readonly DependencyProperty IsFocusRingVisibleProperty = DependencyProperty.Register(
        nameof(IsFocusRingVisible), typeof(bool), typeof(GlassLens), new FrameworkPropertyMetadata(false, OnOverlayChanged));

    /// <summary>Space past the outline the side shade and the safety clip cover, in layout units.</summary>
    private const double RimReach = 3;

    /// <summary>The frost blur: WPF's Gaussian uses σ = radius / 3, so 22 gives σ = 7.33 (OPTICS §3).</summary>
    private const double FrostRadius = 22;

    // Shader path: the effect runs over an empty rectangle and reads the frosted copy through a brush, so WPF
    // realizes the copy (and its blur) as a brush input instead of nesting the blur inside the effect's layer.
    private readonly Border _clipHost = new() { IsHitTestVisible = false };
    private readonly Border _effectHost = new();
    private readonly System.Windows.Shapes.Rectangle _effectBounds = new() { Fill = Brushes.Transparent, IsHitTestVisible = false };
    private readonly VisualBrush _input = new() { ViewboxUnits = BrushMappingMode.Absolute, Stretch = Stretch.Fill };

    // The copy of what lies beneath: invisible on screen in the shader path (a brush input), clipped to the
    // outline in the fallback path.
    private readonly Border _copyHost = new() { IsHitTestVisible = false };
    private readonly Border _copyRoot = new();
    private readonly Border _frame = new() { ClipToBounds = true };
    private readonly Grid _sources = new();
    private readonly LensOverlay _overlay;
    private readonly BlurEffect _frost = new() { Radius = FrostRadius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
    private readonly List<BackdropCopy> _layers = new();
    private GlassShaderEffect? _shader;
    private GlassScene? _scene;
    private Rect? _shapeOverride;
    private Visual? _localBackdrop;
    private Point _glowCenter;
    private double _glowAmount;
    private bool _sourcesDirty = true;
    private bool _nested;
    private Rect _lensRect;

    static GlassLens()
    {
        IsHitTestVisibleProperty.OverrideMetadata(typeof(GlassLens), new UIPropertyMetadata(false));
        FocusableProperty.OverrideMetadata(typeof(GlassLens), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Creates a lens.</summary>
    public GlassLens()
    {
        _overlay = new LensOverlay(this);
        _frame.Child = _sources;
        _copyRoot.Child = _frame;
        _copyHost.Child = _copyRoot;
        _input.Visual = _copyRoot;
        _effectHost.Child = _effectBounds;
        _clipHost.Child = _effectHost;
        AddVisualChild(_copyHost);
        AddVisualChild(_clipHost);
        AddVisualChild(_overlay);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>Corner radius of the outline.</summary>
    public GlassRadius Radius
    {
        get => (GlassRadius)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Optical travel at the outline, in layout units.</summary>
    public double Refraction
    {
        get => (double)GetValue(RefractionProperty);
        set => SetValue(RefractionProperty, value);
    }

    /// <summary>Tint color of the glass, or null.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Draws the keyboard focus ring around the outline.</summary>
    public bool IsFocusRingVisible
    {
        get => (bool)GetValue(IsFocusRingVisibleProperty);
        set => SetValue(IsFocusRingVisibleProperty, value);
    }

    /// <summary>How the lens currently draws: the pixel shader, the fallback material or an opaque fill.</summary>
    public GlassLensMode Mode { get; private set; } = GlassLensMode.Fallback;

    /// <summary>The outline, in the lens's coordinates: its own bounds unless a morph or layout spring moves it.</summary>
    internal Rect ShapeBounds => _shapeOverride ?? new Rect(RenderSize);

    internal bool IsDark { get; private set; }

    internal GlassMaterial Material { get; private set; }

    internal bool IncreaseContrast { get; private set; }

    internal Color? EffectiveTint => Tint is { A: > 0 } tint ? tint : null;

    internal Point GlowCenter => _glowCenter;

    internal double GlowAmount => _glowAmount;

    /// <summary>Visual children: the copy, the shader glass and the overlay.</summary>
    protected override int VisualChildrenCount => 3;

    /// <summary>
    /// Part of the control the surface belongs to that lies beneath the glass and should be refracted with the
    /// backdrop: the track under a slider or switch thumb. Null for none.
    /// </summary>
    internal Visual? LocalBackdrop
    {
        get => _localBackdrop;
        set
        {
            if (ReferenceEquals(_localBackdrop, value)) return;
            _localBackdrop = value;
            InvalidateSources();
        }
    }

    /// <summary>Draws the outline at <paramref name="bounds"/> (lens coordinates) instead of the lens's own bounds; null restores them.</summary>
    internal void SetShape(Rect? bounds)
    {
        if (Nullable.Equals(_shapeOverride, bounds)) return;
        _shapeOverride = bounds;
        Refresh();
    }

    /// <summary>Sets the pointer glow: its center in lens coordinates and its strength (0..1).</summary>
    internal void SetGlow(Point center, double amount)
    {
        if (_glowCenter == center && _glowAmount.Equals(amount)) return;
        _glowCenter = center;
        _glowAmount = amount;
        UpdateGlow();
        if (Mode != GlassLensMode.Shader) _overlay.InvalidateVisual();
    }

    /// <summary>Rebuilds the copies of what lies beneath (the scene, a layer or an enclosing surface changed).</summary>
    internal void InvalidateSources()
    {
        _sourcesDirty = true;
        Refresh();
    }

    /// <summary>Rebuilds the copies at the next alignment.</summary>
    internal void MarkSourcesDirty() => _sourcesDirty = true;

    /// <summary>Aligns every copy with what it copies; cheap when nothing moved.</summary>
    internal void Realign()
    {
        if (_sourcesDirty) RebuildSources();
        var size = _sources.RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return;
        foreach (var layer in _layers) layer.Align(_sources, size);
    }

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index switch
    {
        0 => _copyHost,
        1 => _clipHost,
        2 => _overlay,
        _ => throw new ArgumentOutOfRangeException(nameof(index)),
    };

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _copyHost.Measure(_lensRect.Size);
        _clipHost.Measure(_lensRect.Size);
        _overlay.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        _copyHost.Arrange(_lensRect);
        _clipHost.Arrange(_lensRect);
        _input.Viewbox = new Rect(_lensRect.Size);
        _overlay.Arrange(new Rect(finalSize));
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Refresh();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) Refresh();
    }

    private static void OnOpticsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassLens)d).Refresh();

    private static void OnOverlayChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassLens)d)._overlay.InvalidateVisual();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        GlassSystemSettings.Changed += OnSettingsChanged;
        GlassSystemSettings.Watch(this);
        var scene = GlassScene.FindScene(this);
        if (!ReferenceEquals(scene, _scene))
        {
            _scene?.Unregister(this);
            _scene = scene;
        }
        _sourcesDirty = true;
        Refresh();
        _scene?.Register(this);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        GlassSystemSettings.Changed -= OnSettingsChanged;
        _scene?.Unregister(this);
        _scene = null;
        ClearSources();
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) Refresh();
        else Dispatcher.BeginInvoke(new Action(Refresh));
    }

    /// <summary>Recomputes the mode, geometry, effects and overlay after anything that affects them changed.</summary>
    private void Refresh()
    {
        IsDark = GlassResolve.IsDark(this);
        IncreaseContrast = GlassScene.GetIncreaseContrast(this);
        Material = GlassResolve.Material(this);
        var mode = ResolveMode();
        if (mode != Mode)
        {
            Mode = mode;
            _sourcesDirty = true;
            // Glass inside this surface copies this lens only while it draws glass.
            _scene?.InvalidateSourcesExcept(this);
        }

        var shape = ShapeBounds;
        var scale = GlassResolve.Scale(this);
        var regular = Material == GlassMaterial.Regular;
        var frostPad = regular ? GlassResolve.CeilToPixels(FrostRadius + 1, scale) : 0;
        switch (Mode)
        {
            case GlassLensMode.Shader:
            {
                var reach = Math.Max(0, Refraction - Math.Min(shape.Width, shape.Height)) + 2;
                if (regular) reach += FrostFillKernel.Radius * FrostFillKernel.For(IsDark).Spacing + 1;
                var margin = GlassResolve.CeilToPixels(Math.Max(reach, RimReach), scale);
                _lensRect = Inflate(shape, margin);
                _effectHost.Effect = EnsureShader();
                _clipHost.Clip = GlassGeometry.Inflate(Offset(shape, _lensRect), Radius, RimReach);
                _clipHost.Visibility = Visibility.Visible;
                // Kept in the tree for layout and alignment, but never drawn on screen: an ancestor at zero opacity is
                // skipped by the renderer, while the brush renders the copy from its own root.
                _copyHost.Opacity = 0;
                _copyHost.Clip = null;
                _copyHost.Visibility = Visibility.Visible;
                break;
            }
            case GlassLensMode.Fallback:
                _lensRect = shape;
                _effectHost.Effect = null;
                _clipHost.Visibility = Visibility.Collapsed;
                _copyHost.Opacity = 1;
                _copyHost.Clip = GlassGeometry.Create(Offset(shape, _lensRect), Radius);
                _copyHost.Visibility = Visibility.Visible;
                break;
            default:
                _lensRect = shape;
                _effectHost.Effect = null;
                _clipHost.Visibility = Visibility.Collapsed;
                _copyHost.Visibility = Visibility.Collapsed;
                break;
        }
        _sources.Margin = new Thickness(-frostPad);
        _sources.Effect = regular ? _frost : null;
        UpdateShaderConstants(shape);
        _overlay.Refresh();
        InvalidateMeasure();
        InvalidateArrange();
        if (_sourcesDirty) RebuildSources();
        _scene?.RequestRealign();
    }

    private GlassLensMode ResolveMode()
    {
        if (SystemParameters.HighContrast) return GlassLensMode.HighContrast;
        var scene = _scene;
        if (scene is null || GlassResolve.ReduceTransparency(this) || scene.IsInBackdrop(this)) return GlassLensMode.Opaque;
        var renderMode = GlassScene.GetRenderMode(this);
        if (renderMode != GlassRenderMode.Fallback && GlassRenderCapability.CanUseShader(this)) return GlassLensMode.Shader;
        return GlassLensMode.Fallback;
    }

    private GlassShaderEffect? EnsureShader()
    {
        if (_shader is not null) return _shader;
        var pixelShader = GlassShaderLibrary.SurfacePixelShader;
        if (pixelShader is null) return null;
        _shader = new GlassShaderEffect(pixelShader) { Input = _input };
        return _shader;
    }

    private void UpdateShaderConstants(Rect shape)
    {
        if (_shader is null || Mode != GlassLensMode.Shader) return;
        var parameters = GlassShape.Parameters(shape.Width, shape.Height, Radius);
        _shader.LensGeometry = new Point4D(_lensRect.Width, _lensRect.Height, shape.Width, shape.Height);
        _shader.ShapeOptics = new Point4D(parameters.Extent, parameters.Exponent, Refraction, 0);
        _shader.MaterialOptics = new Point4D(Material == GlassMaterial.Regular ? 1 : 0, IsDark ? 1 : 0, _nested ? 1 : 0, IncreaseContrast ? 1 : 0);
        _shader.TintOptics = EffectiveTint is { } tint ? new Point4D(tint.R / 255.0, tint.G / 255.0, tint.B / 255.0, 1) : new Point4D(0, 0, 0, 0);
        var kernel = FrostFillKernel.For(IsDark);
        _shader.FillWeights = new Point4D(kernel.Weights[0], kernel.Weights[1], kernel.Weights[2], kernel.Weights[3]);
        _shader.FillOptics = new Point4D(kernel.Weights[4], kernel.Spacing, 0, 0);
        UpdateGlow();
    }

    private void UpdateGlow()
    {
        if (_shader is null || Mode != GlassLensMode.Shader) return;
        var shape = ShapeBounds;
        var glow = GlassInteractionModel.Glow(shape.Width, shape.Height, IsDark);
        _shader.GlowOptics = new Point4D(
            _glowCenter.X - (shape.X + shape.Width / 2), _glowCenter.Y - (shape.Y + shape.Height / 2), glow.Radius, _glowAmount);
    }

    private void RebuildSources()
    {
        _sourcesDirty = false;
        ClearSources();
        var scene = _scene;
        var ancestors = scene is null ? new List<GlassLens>() : EnclosingLenses(scene);
        var nested = ancestors.Count > 0;
        if (nested != _nested)
        {
            _nested = nested;
            UpdateShaderConstants(ShapeBounds);
        }
        if (scene is null || Mode is not (GlassLensMode.Shader or GlassLensMode.Fallback)) return;
        AddLayer(scene.BackdropHost);
        foreach (var layer in scene.BackdropLayers)
            if (IsPaintedBefore(layer, this, scene)) AddLayer(layer.BrushTarget);
        foreach (var lens in ancestors) AddLayer(lens);
        if (_localBackdrop is not null) AddLayer(_localBackdrop);
    }

    private void AddLayer(Visual target)
    {
        var layer = new BackdropCopy(target);
        _layers.Add(layer);
        _sources.Children.Add(layer.Rectangle);
    }

    private void ClearSources()
    {
        foreach (var layer in _layers) layer.Detach();
        _layers.Clear();
        _sources.Children.Clear();
    }

    /// <summary>Lenses of the glass surfaces that enclose this one, outermost first.</summary>
    private List<GlassLens> EnclosingLenses(GlassScene scene)
    {
        var result = new List<GlassLens>();
        for (var node = VisualTreeHelper.GetParent(this); node is not null && !ReferenceEquals(node, scene); node = VisualTreeHelper.GetParent(node))
        {
            if (node is not Visual visual) continue;
            for (var i = 0; i < VisualTreeHelper.GetChildrenCount(visual); i++)
                if (VisualTreeHelper.GetChild(visual, i) is GlassLens lens && !ReferenceEquals(lens, this) && lens.Mode is GlassLensMode.Shader or GlassLensMode.Fallback)
                    result.Insert(0, lens);
        }
        return result;
    }

    /// <summary>Whether <paramref name="first"/> is painted before (beneath) <paramref name="second"/> within <paramref name="root"/>.</summary>
    private static bool IsPaintedBefore(Visual first, Visual second, Visual root)
    {
        var a = PathFrom(root, first);
        var b = PathFrom(root, second);
        for (var i = 0; i < Math.Min(a.Count, b.Count); i++)
        {
            if (ReferenceEquals(a[i], b[i])) continue;
            if (i == 0) return false;
            var parent = a[i - 1];
            int ia = -1, ib = -1;
            for (var c = 0; c < VisualTreeHelper.GetChildrenCount(parent); c++)
            {
                var child = VisualTreeHelper.GetChild(parent, c);
                if (ReferenceEquals(child, a[i])) ia = c;
                if (ReferenceEquals(child, b[i])) ib = c;
            }
            return ia >= 0 && ib >= 0 && ia < ib;
        }
        return false;
    }

    private static List<DependencyObject> PathFrom(Visual root, Visual element)
    {
        var path = new List<DependencyObject>();
        for (DependencyObject? node = element; node is not null; node = VisualTreeHelper.GetParent(node))
        {
            path.Insert(0, node);
            if (ReferenceEquals(node, root)) return path;
        }
        return new List<DependencyObject>();
    }

    private static Rect Inflate(Rect rect, double amount) => new(rect.X - amount, rect.Y - amount, rect.Width + 2 * amount, rect.Height + 2 * amount);

    private static Rect Offset(Rect rect, Rect origin) => new(rect.X - origin.X, rect.Y - origin.Y, rect.Width, rect.Height);
}

/// <summary>How a <see cref="GlassLens"/> draws.</summary>
public enum GlassLensMode
{
    /// <summary>The glass pixel shader refracts and tones the backdrop.</summary>
    Shader,

    /// <summary>The fallback material: platform blur for Regular, a veil fitted to the tone curve and the exact rim light.</summary>
    Fallback,

    /// <summary>An opaque fill with the rim light: transparency is reduced, or there is no scene backdrop.</summary>
    Opaque,

    /// <summary>A Windows contrast theme is active: system colors, no transparency.</summary>
    HighContrast,
}
