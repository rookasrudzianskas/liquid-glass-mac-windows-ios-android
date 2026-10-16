using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A slider with a 6-unit track, an accent fill and a 20 × 16 white thumb that lifts into Clear glass while it is
/// dragged (OPTICS §6). All of <see cref="Slider"/>'s behavior applies: keyboard steps, ticks, snapping, vertical
/// orientation and UI Automation's RangeValue pattern. Clicking the track moves the thumb there.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassSlider Minimum="0" Maximum="100" Value="{Binding Volume}" AutomationProperties.Name="Volume" />
/// ]]></code>
/// </example>
[TemplatePart(Name = TrackVisualPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = FillPart, Type = typeof(Border))]
public class GlassSlider : Slider
{
    /// <summary>Fill color. Default: the Windows accent color.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassSlider),
        new FrameworkPropertyMetadata(null, OnTintChanged));

    private const string TrackVisualPart = "PART_TrackVisual";
    private const string FillPart = "PART_Fill";

    private readonly SolidColorBrush _fillBrush = new(GlassSystemSettings.DefaultAccent);
    private readonly GlassAppearanceTracker _appearance;
    private FrameworkElement? _trackVisual;
    private Border? _fill;
    private Track? _track;
    private GlassValueThumb? _thumbVisual;

    static GlassSlider()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassSlider), new FrameworkPropertyMetadata(typeof(GlassSlider)));
        GlassScene.AddContentForegroundOwner(typeof(GlassSlider));
        IsMoveToPointEnabledProperty.OverrideMetadata(typeof(GlassSlider), new FrameworkPropertyMetadata(true));
    }

    /// <summary>Creates a slider.</summary>
    public GlassSlider()
    {
        _appearance = new GlassAppearanceTracker(this, () => null, UpdateFill);
        GlassFocus.Track(this);
    }

    /// <summary>Fill color, or null for the Windows accent color.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _trackVisual = GetTemplateChild(TrackVisualPart) as FrameworkElement;
        _fill = GetTemplateChild(FillPart) as Border;
        _track = GetTemplateChild("PART_Track") as Track;
        if (_fill is not null) _fill.Background = _fillBrush;
        if (_track?.Thumb is { } thumb)
        {
            thumb.ApplyTemplate();
            _thumbVisual = thumb.Template?.FindName("PART_Visual", thumb) as GlassValueThumb;
            if (_thumbVisual is not null) _thumbVisual.Backdrop = _trackVisual;
            thumb.DragStarted += (_, _) => Lift(true);
            thumb.DragCompleted += (_, _) => Lift(false);
            thumb.LostMouseCapture += (_, _) => Lift(false);
        }
        UpdateFill();
    }

    /// <inheritdoc />
    protected override void OnValueChanged(double oldValue, double newValue)
    {
        base.OnValueChanged(oldValue, newValue);
        UpdateFillLength();
        GlassScene.FindScene(this)?.RequestRealign();
    }

    /// <inheritdoc />
    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        UpdateFillLength();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
        if (e.Property == MinimumProperty || e.Property == MaximumProperty || e.Property == OrientationProperty || e.Property == IsDirectionReversedProperty)
            UpdateFillLength();
    }

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassSlider)d).UpdateFill();

    private void Lift(bool lifted)
    {
        if (_thumbVisual is not null) _thumbVisual.IsLifted = lifted && IsEnabled;
    }

    private void UpdateFill()
    {
        _fillBrush.Color = SystemParameters.HighContrast
            ? SystemColors.HighlightColor
            : Tint is { A: > 0 } tint ? tint : GlassSystemSettings.AccentColor;
    }

    /// <summary>The fill runs from the start of the track to the thumb's center, beneath the thumb.</summary>
    private void UpdateFillLength()
    {
        if (_fill is null || _trackVisual is null || _track is not { } track) return;
        var range = Maximum - Minimum;
        var fraction = range > 0 ? GlassMath.Sat((Value - Minimum) / range) : 0;
        var horizontal = Orientation == Orientation.Horizontal;
        var thumb = track.Thumb is { } t ? (horizontal ? t.Width : t.Height) : 0;
        if (double.IsNaN(thumb)) thumb = horizontal ? GlassControlMotion.SliderThumbWidth : GlassControlMotion.SliderThumbHeight;
        var length = horizontal ? _trackVisual.ActualWidth : _trackVisual.ActualHeight;
        var filled = Math.Max(0, fraction * (length - thumb) + thumb / 2);
        if (horizontal)
        {
            _fill.Width = filled;
            _fill.Height = double.NaN;
            _fill.HorizontalAlignment = IsDirectionReversed ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            _fill.VerticalAlignment = VerticalAlignment.Stretch;
        }
        else
        {
            _fill.Height = filled;
            _fill.Width = double.NaN;
            _fill.VerticalAlignment = IsDirectionReversed ? VerticalAlignment.Top : VerticalAlignment.Bottom;
            _fill.HorizontalAlignment = HorizontalAlignment.Stretch;
        }
    }
}
