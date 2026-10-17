using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// Progressive edge blur over the scene's backdrop: six blur levels whose strength rises toward
/// <see cref="Edge"/> across a band <see cref="Size"/> units deep (level <c>i</c> has σ = <see cref="Blur"/> × (i/6)²),
/// so content scrolling under a toolbar or tab bar softens instead of clashing with it. Glass above the band refracts
/// the blurred backdrop.
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

    /// <summary>Gaussian σ at the edge, in units, from 0 to <see cref="MaxBlur"/>. Default 20.</summary>
    public static readonly DependencyProperty BlurProperty = DependencyProperty.Register(
        nameof(Blur), typeof(double), typeof(GlassProgressiveBlur),
        new FrameworkPropertyMetadata(GlassProgressive.DefaultBlur, OnBandChanged), v => v is double d && d >= 0 && d <= MaxBlur);

    /// <summary>
    /// The largest <see cref="Blur"/>: 33 units. It renders exactly at every display scale (WPF's blur is split into
    /// nested passes where one is not wide enough), and keeps the strongest level to a few passes even at 300%.
    /// </summary>
    public const double MaxBlur = 33;

    /// <summary>The element to blur. Default: the enclosing scene's backdrop.</summary>
    public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
        nameof(Source), typeof(UIElement), typeof(GlassProgressiveBlur), new FrameworkPropertyMetadata(null, OnSourceChanged));

    private const int GradientStops = 32;

    private readonly Grid _host = new() { IsHitTestVisible = false };
    // The earlier passes of levels that need several: laid out over the band like the level they feed, never drawn
    // (an ancestor at zero opacity is skipped, while a brush still renders its target).
    private readonly Grid _stages = new() { Opacity = 0, IsHitTestVisible = false };
    private readonly List<Level> _levels = new();
    private bool _nestPasses;
    private GlassScene? _scene;
    private double? _scaleOverride;

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

    /// <summary>Gaussian σ at the edge, in units (at most <see cref="MaxBlur"/>), at every display scale.</summary>
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

    /// <summary>Plans the blur passes for this display scale instead of the element's own (tests that render at another DPI).</summary>
    internal double? ScaleOverride
    {
        get => _scaleOverride;
        set
        {
            _scaleOverride = value;
            Rebuild();
        }
    }

    /// <summary>
    /// Nests a level's passes inside one another instead of chaining them through brushes (tests compare the two). On
    /// screen WPF realizes a nested effect only as far as the clip around the level reaches, so the inner passes lose
    /// what lies beyond the band and the edge shows a weaker blur; through a brush each pass sees all of its input.
    /// </summary>
    internal bool NestPasses
    {
        get => _nestPasses;
        set
        {
            _nestPasses = value;
            Rebuild();
        }
    }

    /// <summary>The <c>BlurEffect</c> radii of each level, strongest last.</summary>
    internal IReadOnlyList<IReadOnlyList<double>> LevelRadii
    {
        get
        {
            var result = new List<IReadOnlyList<double>>();
            foreach (var level in _levels) result.Add(level.Radii);
            return result;
        }
    }

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
            if (level.MaskSize != band.Size)
            {
                level.MaskSize = band.Size;
                level.Clip.OpacityMask = Mask(level.Index, band.Size);
            }
            // Earlier passes cover the band and the reach around it, exactly where the pass that reads them lies.
            var reach = level.Reach;
            var covered = new Rect(band.X - reach, band.Y - reach, band.Width + 2 * reach, band.Height + 2 * reach);
            foreach (var stage in level.Stages)
            {
                stage.Margin = new Thickness(covered.X, covered.Y, 0, 0);
                stage.Width = covered.Width;
                stage.Height = covered.Height;
                stage.Brush.Viewbox = new Rect(covered.Size);
            }
        }
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        // WPF scales a blur's radius with the display: plan the passes again for the new scale.
        Rebuild();
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
        LayoutUpdated -= OnLayoutUpdated;
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
        foreach (var level in _levels)
        {
            level.Copy.Detach();
            foreach (var stage in level.Stages) stage.Brush.Visual = null;
        }
        _levels.Clear();
        _stages.Children.Clear();
        _host.Children.Clear();
    }

    private void Rebuild()
    {
        Clear();
        var source = Source ?? _scene?.BackdropHost;
        if (source is null || !IsLoaded) return;
        var scale = _scaleOverride ?? GlassResolve.Scale(this);
        _host.Children.Add(_stages);
        for (var i = 1; i <= GlassProgressive.Levels; i++)
        {
            // WPF renders one blur of at most 100 device pixels of radius (σ = radius / 3): a level wider than that at
            // this display scale is drawn in several passes whose variances add up. Each pass after the first reads
            // the one before through a brush; the copy (the first pass) and every pass reach past the band as far as
            // the passes together read.
            var radii = BlurPasses.Radii(GlassProgressive.LevelBlur(i, Blur), scale);
            var reach = BlurPasses.Reach(radii) + 1;
            var copy = new BackdropCopy(source);
            var stages = new List<Stage>();
            FrameworkElement content = copy.Rectangle;
            for (var pass = 0; pass < radii.Length; pass++)
            {
                var blur = new BlurEffect { Radius = radii[pass], KernelType = KernelType.Gaussian, RenderingBias = RenderingBias.Quality };
                if (pass > 0 && _nestPasses) content = new Border { Child = content, IsHitTestVisible = false };
                else if (pass > 0)
                {
                    var stage = new Stage(content);
                    stages.Add(stage);
                    _stages.Children.Add(stage);
                    content = new System.Windows.Shapes.Rectangle { Fill = stage.Brush, IsHitTestVisible = false };
                }
                content.Effect = blur;
            }
            content.Margin = new Thickness(-reach);
            var clip = new Border
            {
                ClipToBounds = true,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top,
                Child = content,
            };
            _levels.Add(new Level(i, clip, copy, radii, reach, stages));
            _host.Children.Add(clip);
        }
        InvalidateArrange();
    }

    /// <summary>
    /// Opacity of level <paramref name="level"/> across a band of <paramref name="band"/>, from the sharp side to the
    /// edge. The gradient is placed in the band's own coordinates: relative to the bounding box it would span the
    /// level's content, which reaches past the band by the blur's reach and more, and so would fade the level out
    /// well before the edge.
    /// </summary>
    private Brush Mask(int level, System.Windows.Size band)
    {
        double w = band.Width, h = band.Height;
        var (start, end) = Edge switch
        {
            GlassEdge.Bottom => (new Point(0, 0), new Point(0, h)),
            GlassEdge.Left => (new Point(w, 0), new Point(0, 0)),
            GlassEdge.Right => (new Point(0, 0), new Point(w, 0)),
            _ => (new Point(0, h), new Point(0, 0)),
        };
        var brush = new LinearGradientBrush { StartPoint = start, EndPoint = end, MappingMode = BrushMappingMode.Absolute };
        for (var k = 0; k <= GradientStops; k++)
        {
            var u = (double)k / GradientStops;
            var alpha = GlassProgressive.LayerOpacity(level, u);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255), 0, 0, 0), u));
        }
        brush.Freeze();
        return brush;
    }

    /// <summary>
    /// An earlier pass of a level, off screen in the stages: its target sits at its origin, so a brush with the
    /// stage's size as its viewbox maps it exactly onto the pass that reads it.
    /// </summary>
    private sealed class Stage : Border
    {
        public Stage(FrameworkElement pass)
        {
            HorizontalAlignment = HorizontalAlignment.Left;
            VerticalAlignment = VerticalAlignment.Top;
            IsHitTestVisible = false;
            Child = pass;
            Brush = new VisualBrush(pass)
            {
                ViewboxUnits = BrushMappingMode.Absolute,
                ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
                Viewport = new Rect(0, 0, 1, 1),
                Stretch = Stretch.Fill,
                TileMode = TileMode.None,
                AutoLayoutContent = false,
            };
        }

        public VisualBrush Brush { get; }
    }

    private sealed class Level
    {
        public Level(int index, Border clip, BackdropCopy copy, double[] radii, double reach, List<Stage> stages)
        {
            Index = index;
            Clip = clip;
            Copy = copy;
            Radii = radii;
            Reach = reach;
            Stages = stages;
        }

        /// <summary>How far the level's passes reach past the band, in units.</summary>
        public double Reach { get; }

        /// <summary>The level's earlier passes, read through brushes (none for a single pass).</summary>
        public List<Stage> Stages { get; }

        /// <summary>The blur level, 1 to 6.</summary>
        public int Index { get; }

        /// <summary>The band size the opacity mask was laid out for.</summary>
        public System.Windows.Size MaskSize { get; set; } = System.Windows.Size.Empty;

        public Border Clip { get; }

        public BackdropCopy Copy { get; }

        public double[] Radii { get; }
    }
}
