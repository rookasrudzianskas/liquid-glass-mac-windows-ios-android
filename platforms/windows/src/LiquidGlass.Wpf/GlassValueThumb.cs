using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// The thumb of a <see cref="GlassSlider"/> or <see cref="GlassSwitch"/>. At rest it is an opaque white capsule with
/// a soft shadow (in a contrast theme, a solid system-color capsule); while <see cref="IsLifted"/> it grows by <see cref="GrowX"/> × <see cref="GrowY"/> per
/// side and crossfades into Clear glass that refracts the track beneath it, stretching along its path with its speed.
/// </summary>
/// <remarks>A template part of the slider and switch templates; it sizes itself to its rest size.</remarks>
public sealed class GlassValueThumb : FrameworkElement, IGlassAnimation
{
    /// <summary>Whether the thumb is held and lifted into glass.</summary>
    public static readonly DependencyProperty IsLiftedProperty = DependencyProperty.Register(
        nameof(IsLifted), typeof(bool), typeof(GlassValueThumb), new FrameworkPropertyMetadata(false, OnLiftedChanged));

    /// <summary>Horizontal growth per side when lifted. Default 2.5 (slider).</summary>
    public static readonly DependencyProperty GrowXProperty = DependencyProperty.Register(
        nameof(GrowX), typeof(double), typeof(GlassValueThumb), new FrameworkPropertyMetadata(GlassControlMotion.SliderThumbGrowX, FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>Vertical growth per side when lifted. Default 2 (slider).</summary>
    public static readonly DependencyProperty GrowYProperty = DependencyProperty.Register(
        nameof(GrowY), typeof(double), typeof(GlassValueThumb), new FrameworkPropertyMetadata(GlassControlMotion.SliderThumbGrowY, FrameworkPropertyMetadataOptions.AffectsArrange));

    /// <summary>Refraction of the lifted glass. Default 5 (slider).</summary>
    public static readonly DependencyProperty LensRefractionProperty = DependencyProperty.Register(
        nameof(LensRefraction), typeof(double), typeof(GlassValueThumb), new FrameworkPropertyMetadata(GlassControlMotion.SliderThumbRefraction, OnLensChanged));

    /// <summary>The part of the control beneath the thumb that the lifted glass refracts (the track).</summary>
    public static readonly DependencyProperty BackdropProperty = DependencyProperty.Register(
        nameof(Backdrop), typeof(UIElement), typeof(GlassValueThumb), new FrameworkPropertyMetadata(null, OnLensChanged));

    private readonly Grid _body = new();
    private readonly Border _face = new() { Background = Brushes.White };
    private readonly GlassSurface _lens = new()
    {
        Material = GlassMaterial.Clear,
        Radius = GlassRadius.Capsule,
        IsHitTestVisible = false,
        Visibility = Visibility.Hidden,
    };
    private readonly ScaleTransform _stretch = new();
    private readonly DropShadowEffect _shadow = new() { BlurRadius = 10, ShadowDepth = 1.5, Direction = 270, Opacity = 0.2, Color = Colors.Black, RenderingBias = RenderingBias.Quality };
    private double _clock;
    private double _progress;
    private double _pressStart;
    private double _transitionTime;
    private double _transitionFrom;
    private double _transitionTo;
    private bool _pulse;
    private Vector _velocity;
    private Point? _lastCenter;
    private Brush? _contrastFace;

    static GlassValueThumb()
    {
        IsHitTestVisibleProperty.OverrideMetadata(typeof(GlassValueThumb), new UIPropertyMetadata(false));
    }

    /// <summary>Creates a thumb.</summary>
    public GlassValueThumb()
    {
        _face.Effect = _shadow;
        _body.Children.Add(_face);
        _body.Children.Add(_lens);
        _body.RenderTransform = _stretch;
        AddVisualChild(_body);
        // The layout manager holds LayoutUpdated handlers strongly: subscribe only while loaded.
        Loaded += (_, _) =>
        {
            LayoutUpdated -= OnLayoutUpdated;
            LayoutUpdated += OnLayoutUpdated;
            GlassSystemSettings.Changed -= OnSettingsChanged;
            GlassSystemSettings.Changed += OnSettingsChanged;
            UpdateVisuals();
        };
        Unloaded += (_, _) =>
        {
            LayoutUpdated -= OnLayoutUpdated;
            GlassSystemSettings.Changed -= OnSettingsChanged;
            GlassFrameLoop.Stop(this);
        };
        UpdateVisuals();
    }

    /// <summary>Whether the thumb is held and lifted into glass.</summary>
    public bool IsLifted
    {
        get => (bool)GetValue(IsLiftedProperty);
        set => SetValue(IsLiftedProperty, value);
    }

    /// <summary>Horizontal growth per side when lifted.</summary>
    public double GrowX
    {
        get => (double)GetValue(GrowXProperty);
        set => SetValue(GrowXProperty, value);
    }

    /// <summary>Vertical growth per side when lifted.</summary>
    public double GrowY
    {
        get => (double)GetValue(GrowYProperty);
        set => SetValue(GrowYProperty, value);
    }

    /// <summary>Refraction of the lifted glass.</summary>
    public double LensRefraction
    {
        get => (double)GetValue(LensRefractionProperty);
        set => SetValue(LensRefractionProperty, value);
    }

    /// <summary>The part of the control beneath the thumb that the lifted glass refracts.</summary>
    public UIElement? Backdrop
    {
        get => (UIElement?)GetValue(BackdropProperty);
        set => SetValue(BackdropProperty, value);
    }

    /// <summary>How far the thumb has lifted into glass, 0..1.</summary>
    internal double Progress => _progress;

    /// <summary>The face in a contrast theme (default: the system text color); a switch uses the highlight text color when on.</summary>
    internal Brush? ContrastFace
    {
        get => _contrastFace;
        set
        {
            _contrastFace = value;
            UpdateVisuals();
        }
    }

    /// <summary>The brush the thumb's face is painted with.</summary>
    internal Brush FaceBrush => _face.Background;

    /// <inheritdoc />
    protected override int VisualChildrenCount => 1;

    /// <inheritdoc />
    protected override Visual GetVisualChild(int index) => index == 0 ? _body : throw new ArgumentOutOfRangeException(nameof(index));

    /// <inheritdoc />
    protected override Size MeasureOverride(Size availableSize)
    {
        _body.Measure(availableSize);
        return default;
    }

    /// <inheritdoc />
    protected override Size ArrangeOverride(Size finalSize)
    {
        // The thumb grows only under full motion; reduced motion crossfades at the resting size.
        var grow = GlassResolve.Motion(this) == GlassMotion.Full ? _progress : 0;
        var gx = GrowX * grow;
        var gy = GrowY * grow;
        var rect = new Rect(-gx, -gy, finalSize.Width + 2 * gx, finalSize.Height + 2 * gy);
        _body.Arrange(rect);
        _face.CornerRadius = new CornerRadius(rect.Height / 2);
        _stretch.CenterX = rect.Width / 2;
        _stretch.CenterY = rect.Height / 2;
        return finalSize;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (e.Property == GlassScene.AppearanceProperty || e.Property == GlassScene.MotionProperty) UpdateVisuals();
        if (e.Property == GlassScene.MotionProperty) InvalidateArrange();
    }

    // Advanced by the frame loop; not part of the public surface.
    bool IGlassAnimation.Tick(double dt)
    {
        _clock += dt;
        var motion = GlassResolve.Motion(this);
        var full = motion == GlassMotion.Full;
        var elapsed = _clock - _transitionTime;
        // Reduced motion keeps the crossfade into glass (at the resting size, see ArrangeOverride); none removes it.
        if (motion == GlassMotion.None) _progress = _transitionTo;
        else if (_pulse) _progress = GlassControlMotion.TapEnvelope(_clock - _pressStart);
        else _progress = _transitionFrom + (_transitionTo - _transitionFrom) * GlassControlMotion.EaseInOut(Math.Min(1, elapsed / GlassControlMotion.PressCrossfadeDuration));

        var center = Center();
        if (center is { } now && _lastCenter is { } before && dt > 0)
            _velocity += ((now - before) / dt - _velocity) * Math.Min(1, dt * 18);
        _lastCenter = center;
        if (!full) _velocity = default;
        UpdateVisuals();
        InvalidateArrange();
        GlassScene.FindScene(this)?.RequestRealign();

        var transitioning = _pulse ? _clock - _pressStart < GlassControlMotion.TapEnvelopeDuration : elapsed < GlassControlMotion.PressCrossfadeDuration;
        if (_pulse && !transitioning)
        {
            _pulse = false;
            _progress = _transitionTo;
        }
        var moving = _velocity.Length > 1;
        if (!moving && !IsLifted) _velocity = default;
        var active = transitioning || moving || IsLifted && Math.Abs(_progress - 1) > 1e-6;
        if (!active)
        {
            _lastCenter = null;
            UpdateVisuals();
        }
        return active;
    }

    private static void OnLiftedChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassValueThumb)d).Lift((bool)e.NewValue);

    private static void OnLensChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassValueThumb)d).UpdateLens();

    private void Lift(bool lifted)
    {
        if (lifted)
        {
            _pressStart = _clock;
            _pulse = false;
        }
        else
        {
            // A tap too short to lift the thumb plays the measured material envelope instead.
            _pulse = _clock - _pressStart < GlassControlMotion.TapThreshold && _progress < 0.5 && GlassResolve.Motion(this) == GlassMotion.Full;
        }
        _transitionTime = _clock;
        _transitionFrom = _progress;
        _transitionTo = lifted ? 1 : 0;
        UpdateLens();
        GlassFrameLoop.Start(this);
    }

    private void UpdateLens()
    {
        _lens.Refraction = LensRefraction;
        if (_lens.Lens is { } lens) lens.LocalBackdrop = Backdrop;
        else _lens.Loaded += AttachBackdrop;
    }

    private void AttachBackdrop(object sender, RoutedEventArgs e)
    {
        _lens.Loaded -= AttachBackdrop;
        if (_lens.Lens is { } lens) lens.LocalBackdrop = Backdrop;
    }

    private void OnSettingsChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.CheckAccess()) UpdateVisuals();
        else Dispatcher.BeginInvoke(new Action(UpdateVisuals));
    }

    private void UpdateVisuals()
    {
        var highContrast = SystemParameters.HighContrast;
        _face.Background = highContrast ? _contrastFace ?? SystemColors.ControlTextBrush : Brushes.White;
        _face.Effect = highContrast ? null : _shadow;
        var glass = highContrast ? 0 : _progress;
        _face.Opacity = 1 - glass;
        _lens.Opacity = glass;
        _lens.Visibility = glass > 1e-3 ? Visibility.Visible : Visibility.Hidden;
        // The lens lengthens along its path, which is the axis it moves on.
        var stretch = GlassControlMotion.ThumbStretch(_velocity.Length, glass);
        var vertical = Math.Abs(_velocity.Y) > Math.Abs(_velocity.X);
        _stretch.ScaleX = vertical ? stretch.Sy : stretch.Sx;
        _stretch.ScaleY = vertical ? stretch.Sx : stretch.Sy;
    }

    private Point? Center()
    {
        // Measured in the window, so movement by layout (slider) and by transform (switch) both count.
        if (PresentationSource.FromVisual(this)?.RootVisual is not { } root || !IsDescendantOf(root)) return null;
        try
        {
            return TransformToAncestor(root).Transform(new Point(RenderSize.Width / 2, RenderSize.Height / 2));
        }
        catch (InvalidOperationException)
        {
            return null;
        }
    }

    private void OnLayoutUpdated(object? sender, EventArgs e)
    {
        // A held thumb that the control moves keeps its stretch current.
        if (IsLifted && Center() is { } center && _lastCenter is { } last && (center - last).Length > 0.01) GlassFrameLoop.Start(this);
    }
}
