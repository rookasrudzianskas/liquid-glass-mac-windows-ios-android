using System;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// An on/off switch: a 54 × 24 track with a white thumb that moves on the measured switch spring (response 0.47 s,
/// damping 0.7) and lifts into Clear glass while pressed. The thumb can be dragged; Space toggles.
/// <see cref="ContentControl.Content"/> is the label, drawn before the track. UI Automation: a toggle switch (a button
/// with the Toggle pattern).
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassSwitch Content="Wi-Fi" IsChecked="{Binding WifiEnabled}" />
/// ]]></code>
/// </example>
[TemplatePart(Name = TrackPart, Type = typeof(Border))]
[TemplatePart(Name = ThumbPart, Type = typeof(GlassValueThumb))]
public class GlassSwitch : ToggleButton, IGlassAnimation
{
    /// <summary>Track color when on. Default: the Windows accent color.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassSwitch),
        new FrameworkPropertyMetadata(null, OnTintChanged));

    private const string TrackPart = "PART_Track";
    private const string ThumbPart = "PART_Thumb";
    private const double DragThreshold = 4;

    private static readonly Color OffColor = Color.FromArgb(0x66, 0x80, 0x80, 0x80);

    private readonly TranslateTransform _offset = new();
    private readonly SolidColorBrush _trackBrush = new(OffColor);
    private readonly GlassAppearanceTracker _appearance;
    private Border? _track;
    private GlassValueThumb? _thumb;
    private double _position;
    private double _velocity;
    private bool _positioned;
    private Point? _pressPoint;
    private double _pressPosition;
    private bool _dragging;
    private bool _suppressToggle;

    static GlassSwitch()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassSwitch), new FrameworkPropertyMetadata(typeof(GlassSwitch)));
        GlassScene.AddContentForegroundOwner(typeof(GlassSwitch));
    }

    /// <summary>Creates a switch.</summary>
    public GlassSwitch()
    {
        _appearance = new GlassAppearanceTracker(this, () => null, UpdateTrack);
        GlassFocus.Track(this);
        Unloaded += (_, _) => GlassFrameLoop.Stop(this);
    }

    /// <summary>Track color when on, or null for the Windows accent color.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Distance the thumb travels between off and on.</summary>
    private static double Travel => GlassControlMotion.SwitchTrackWidth - 2 * 2 - GlassControlMotion.SwitchThumbWidth;

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _track = GetTemplateChild(TrackPart) as Border;
        _thumb = GetTemplateChild(ThumbPart) as GlassValueThumb;
        if (_track is not null) _track.Background = _trackBrush;
        if (_thumb is not null)
        {
            _thumb.RenderTransform = _offset;
            _thumb.Backdrop = _track;
        }
        _positioned = false;
        MoveThumb(animate: false);
        UpdateTrack();
    }

    // Advanced by the frame loop; not part of the public surface.
    bool IGlassAnimation.Tick(double dt)
    {
        if (_dragging) return true;
        var target = Target();
        GlassControlMotion.SwitchSpringStep(ref _position, ref _velocity, target, dt);
        _offset.X = _position;
        GlassScene.FindScene(this)?.RequestRealign();
        return !(_position.Equals(target) && _velocity.Equals(0));
    }

    /// <inheritdoc />
    protected override AutomationPeer OnCreateAutomationPeer() => new GlassSwitchAutomationPeer(this);

    /// <inheritdoc />
    protected override void OnChecked(RoutedEventArgs e)
    {
        base.OnChecked(e);
        MoveThumb(animate: true);
        UpdateTrack();
    }

    /// <inheritdoc />
    protected override void OnUnchecked(RoutedEventArgs e)
    {
        base.OnUnchecked(e);
        MoveThumb(animate: true);
        UpdateTrack();
    }

    /// <inheritdoc />
    protected override void OnIndeterminate(RoutedEventArgs e)
    {
        base.OnIndeterminate(e);
        MoveThumb(animate: true);
        UpdateTrack();
    }

    /// <inheritdoc />
    protected override void OnIsPressedChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnIsPressedChanged(e);
        UpdateLift();
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        _pressPoint = e.GetPosition(this);
        _pressPosition = _position;
        _dragging = false;
        base.OnMouseLeftButtonDown(e);
    }

    /// <inheritdoc />
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (_pressPoint is not { } start || e.LeftButton != MouseButtonState.Pressed || !IsMouseCaptured) return;
        var dx = e.GetPosition(this).X - start.X;
        if (!_dragging && Math.Abs(dx) < DragThreshold) return;
        if (!_dragging)
        {
            _dragging = true;
            UpdateLift();
        }
        _position = GlassMath.Clamp(_pressPosition + dx, 0, Travel);
        _velocity = 0;
        _offset.X = _position;
        GlassScene.FindScene(this)?.RequestRealign();
        GlassFrameLoop.Start(this);
    }

    /// <inheritdoc />
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        var dragged = _dragging;
        _pressPoint = null;
        _dragging = false;
        if (dragged)
        {
            // The drag decides the state; the click that follows must not toggle it back.
            _suppressToggle = true;
            SetCurrentValue(IsCheckedProperty, _position > Travel / 2);
        }
        base.OnMouseLeftButtonUp(e);
        _suppressToggle = false;
        UpdateLift();
        MoveThumb(animate: true);
    }

    /// <inheritdoc />
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (!_dragging) return;
        _dragging = false;
        _pressPoint = null;
        UpdateLift();
        MoveThumb(animate: true);
    }

    /// <inheritdoc />
    protected override void OnToggle()
    {
        if (_suppressToggle) return;
        base.OnToggle();
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassSwitch)d).UpdateTrack();

    private double Target() => IsChecked == true ? Travel : IsChecked == null ? Travel / 2 : 0;

    private void MoveThumb(bool animate)
    {
        if (_dragging) return;
        if (!animate || !_positioned || GlassResolve.Motion(this) != GlassMotion.Full)
        {
            _positioned = true;
            _position = Target();
            _velocity = 0;
            _offset.X = _position;
            GlassFrameLoop.Stop(this);
            GlassScene.FindScene(this)?.RequestRealign();
            return;
        }
        GlassFrameLoop.Start(this);
    }

    private void UpdateLift()
    {
        if (_thumb is not null) _thumb.IsLifted = IsEnabled && (IsPressed || _dragging);
    }

    private void UpdateTrack()
    {
        var on = IsChecked == true;
        var highContrast = SystemParameters.HighContrast;
        if (_track is not null)
        {
            // In a contrast theme the off track is the window color: outline it in the text color, as Windows does.
            _track.BorderBrush = highContrast ? SystemColors.ControlTextBrush : null;
            _track.BorderThickness = new Thickness(highContrast ? 1 : 0);
        }
        if (_thumb is not null) _thumb.ContrastFace = on ? SystemColors.HighlightTextBrush : SystemColors.ControlTextBrush;
        var color = highContrast
            ? on ? SystemColors.HighlightColor : SystemColors.ControlColor
            : on ? Tint is { A: > 0 } tint ? tint : GlassSystemSettings.AccentColor : OffColor;
        if (_trackBrush.Color == color) return;
        var motion = GlassResolve.Motion(this);
        if (motion == GlassMotion.None || !IsLoaded)
        {
            _trackBrush.BeginAnimation(SolidColorBrush.ColorProperty, null);
            _trackBrush.Color = color;
            return;
        }
        _trackBrush.BeginAnimation(SolidColorBrush.ColorProperty, new ColorAnimation(color, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut },
        });
    }
}

/// <summary>Exposes a <see cref="GlassSwitch"/> to UI Automation as a toggle switch: a button with the Toggle pattern.</summary>
public sealed class GlassSwitchAutomationPeer : ToggleButtonAutomationPeer
{
    /// <summary>Creates the peer.</summary>
    public GlassSwitchAutomationPeer(GlassSwitch owner) : base(owner)
    {
    }

    /// <inheritdoc />
    protected override string GetLocalizedControlTypeCore() => "toggle switch";

    /// <inheritdoc />
    protected override string GetClassNameCore() => nameof(GlassSwitch);
}
