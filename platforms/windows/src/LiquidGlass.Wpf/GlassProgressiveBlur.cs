using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// Progressive edge blur over the scene's backdrop (OPTICS §7): six blur levels whose strength rises toward
/// <see cref="Edge"/> across a band <see cref="Size"/> units deep, so content scrolling under a toolbar or tab bar
/// softens instead of clashing with it. Glass above the band refracts the blurred backdrop.
/// </summary>
/// <remarks>
/// Place it among a <see cref="GlassScene"/>'s children, stretched over the area whose edge should blur; it draws only
/// the band at <see cref="Edge"/> and takes no input. Outside a scene, set <see cref="Source"/>.
/// </remarks>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassProgressiveBlur Edge="Top" Size="96" Blur="20" />
/// ]]></code>
/// </example>
public class GlassProgressiveBlur : FrameworkElement
{
    /// <summary>The edge that is blurred the most. Default <see cref="GlassEdge.Top"/>.</summary>
    public static readonly DependencyProperty EdgeProperty = DependencyProperty.Register(
        nameof(Edge), typeof(GlassEdge), typeof(GlassProgressiveBlur),
        new FrameworkPropertyMetadata(GlassEdge.Top, FrameworkPropertyMetadataOptions.AffectsMeasure, OnBandChanged));

    /// <summary>Depth of the blurred band, in units. Default 80.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(double), typeof(GlassProgressiveBlur),
        new FrameworkPropertyMetadata(GlassProgressive.DefaultSize, FrameworkPropertyMetadataOptions.AffectsMeasure, OnBandChanged),
        v => v is double d && d >= 0 && !double.IsInfinity(d));

    /// <summary>Gaussian σ at the edge, in units. Default 20.</summary>
    public static readonly DependencyProperty BlurProperty = DependencyProperty.Register(
        nameof(Blur), typeof(double), typeof(GlassProgressiveBlur),
        new FrameworkPropertyMetadata(GlassProgressive.DefaultBlur, OnBandChanged), v => v is double d && d >= 0 && d <= 33);

    /// <summary>The element to blur. Default: the enclosing scene's backdrop.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(UIElement), typeof(GlassProgressiveBlur), new FrameworkPropertyMetadata(null, OnSourceChanged));

    private const int GradientStops = 32;

    private readonly Grid _host = new() { IsHitTestVisible = false };
    private readonly List<Level> _levels = new();
    private GlassScene? _scene;

    static GlassProgressiveBlur()
    {
        IsHitTestVisibleProperty.OverrideMetadata(typeof(GlassProgressiveBlur), new UIPropertyMetadata(false));
    }

    /// <summary>Creates a progressive blur.</summary>
    public GlassProgressiveBlur()
    {
        AddVisualChild(_host);
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    /// <summary>The edge that is blurred the most.</summary>
    public GlassEdge Edge
    {
        get => (GlassEdge)GetValue(EdgeProperty);
        set => SetValue(EdgeProperty, value);
    }

    /// <summary>Depth of the blurred band, in units.</summary>
    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>Gaussian σ at the edge, in units (at most 33, the largest blur WPF renders at 100% scale).</summary>
    public double Blur
    {
        get => (double)GetValue(BlurProperty);
        set => SetValue(BlurProperty, value);
    }

    /// <summary>The element to blur, or null for the scene's backdrop.</summary>
    public UIElement? Source
    {
        get => (UIElement?)GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>The visual glass copies to see the blurred band; it sits at this element's origin.</summary>
    internal Visual BrushTarget => _host;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? _host : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc />
    protected override System.Windows.Size MeasureOverride(System.Windows.Size availableSize)
    {
        _host.Measure(availableSize);
        return Edge is GlassEdge.Top or GlassEdge.Bottom ? new System.Windows.Size(0, Size) : new System.Windows.Size(Size, 0);
    }

    /// <inheritdoc />
    protected override System.Windows.Size ArrangeOverride(System.Windows.Size finalSize)
    {
        _host.Arrange(new Rect(finalSize));
        var band = Band(finalSize);
        foreach (var level in _levels)
        {
            level.Clip.Margin = new Thickness(band.X, band.Y, Math.Max(0, finalSize.Width - band.Right), Math.Max(0, finalSize.Height - band.Bottom));
            level.Clip.Width = band.Width;
            level.Clip.Height = band.Height;
        }
        return finalSize;
    }

    /// <summary>Re-aligns the blurred copies with the source.</summary>
    internal void Realign()
    {
        foreach (var level in _levels) level.Copy.Align(level.Copy.Rectangle, level.Copy.Rectangle.RenderSize);
    }

    private static void OnBandChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassProgressiveBlur)d).Rebuild();

    private static void OnSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassProgressiveBlur)d).Rebuild();

    private Rect Band(System.Windows.Size size)
    {
        var depth = Math.Min(Size, Edge is GlassEdge.Top or GlassEdge.Bottom ? size.Height : size.Width);
        return Edge switch
        {
            GlassEdge.Bottom => new Rect(0, size.Height - depth, size.Width, depth),
            GlassEdge.Left => new Rect(0, 0, depth, size.Height),
            GlassEdge.Right => new Rect(size.Width - depth, 0, depth, size.Height),
            _ => new Rect(0, 0, size.Width, depth),
        };
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        _scene = GlassScene.FindScene(this);
        Rebuild();
        if (_scene is not null && !_scene.IsInBackdrop(this)) _scene.Register(this);
        LayoutUpdated += OnLayoutUpdated;
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        LayoutUpdated -= OnLayoutUpdated;
        _scene?.Unregister(this);
        _scene = null;
        Clear();
    }

    private void OnLayoutUpdated(object? sender, EventArgs e) => Realign();

    private void Clear()
    {
        foreach (var level in _levels) level.Copy.Detach();
        _levels.Clear();
        _host.Children.Clear();
    }

    private void Rebuild()
    {
        Clear();
        var source = Source ?? _scene?.BackdropHost;
        if (source is null || !IsLoaded) return;
        for (var i = 1; i <= GlassProgressive.Levels; i++)
        {
            // WPF's Gaussian uses σ = radius / 3 with a whole-unit radius.
            var radius = Math.Round(3 * GlassProgressive.LevelBlur(i, Blur));
            var copy = new BackdropCopy(source);
            copy.Rectangle.Margin = new Thickness(-(radius + 1));
            if (radius >= 1) copy.Rectangle.Effect = new BlurEffect { Radius = radius, KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
            var clip = new Border
            {
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                OpacityMask = Mask(i),
                Child = copy.Rectangle,
            };
            _levels.Add(new Level(clip, copy));
            _host.Children.Add(clip);
        }
        InvalidateArrange();
    }

    /// <summary>Opacity of level <paramref name="level"/> across the band, from the sharp side to the edge.</summary>
    private Brush Mask(int level)
    {
        var (start, end) = Edge switch
        {
            GlassEdge.Bottom => (new Point(0, 0), new Point(0, 1)),
            GlassEdge.Left => (new Point(1, 0), new Point(0, 0)),
            GlassEdge.Right => (new Point(0, 0), new Point(1, 0)),
            _ => (new Point(0, 1), new Point(0, 0)),
        };
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end, MappingMode = BrushMappingMode.RelativeToBoundingBox };
        for (var k = 0; k <= GradientStops; k++)
        {
            var u = (double)k / GradientStops;
            var alpha = GlassProgressive.LayerOpacity(level, u);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0), u));
        }
        brush.Freeze();
        return brush;
    }

    private sealed class Level
    {
        public Level(Border clip, BackdropCopy copy)
        {
            Clip = clip;
            Copy = copy;
        }

        public Border Clip { get; }

        public BackdropCopy Copy { get; }
    }
}
