using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Elastic, illuminated response to pointer, touch and keyboard: the surface grows under a press,
/// stretches toward a drag with constant volume, settles with gel-like overshoot on release, and lights up beneath
/// the pointer. Input is read from <c>input</c> (the surface, or the control whose template it belongs to); the
/// innermost interactive surface claims a press.
/// </summary>
internal sealed class GlassInteraction : IGlassAnimation
{
    // The press that the innermost interactive surface claimed, so enclosing surfaces the same event bubbles through
    // leave it alone. Held weakly: the event's source must not keep a closed window alive until the next press.
    [ThreadStatic] private static WeakReference<RoutedEventArgs>? _claimed;

    private static readonly double HoverGlow = GlassInteractionModel.Glow(1, 1, false).Hover;

    private readonly GlassSurface _surface;
    private readonly UIElement _input;
    private readonly Spring _tx = new(0, GlassSprings.Track);
    private readonly Spring _ty = new(0, GlassSprings.Track);
    private readonly Spring _press = new(0, GlassSprings.Press);
    private readonly Spring _glow = new(0, GlassSprings.Glow);
    private readonly ScaleTransform _scale = new();
    private readonly TranslateTransform _translate = new();
    private bool _pointer;
    private bool _keyboard;
    private bool _hovering;
    private bool _capturedByUs;
    private Point _pressPoint;
    private Point _glowPoint;
    private Point? _lastScreen;
    private Vector _carried;

    public GlassInteraction(GlassSurface surface, UIElement input)
    {
        _surface = surface;
        _input = input;
        Transform = new TransformGroup { Children = { _scale, _translate } };
        input.AddHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnDown), true);
        input.AddHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnUp), true);
        input.AddHandler(UIElement.MouseMoveEvent, new MouseEventHandler(OnMove), true);
        input.AddHandler(UIElement.MouseEnterEvent, new MouseEventHandler(OnEnter), true);
        input.AddHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnLeave), true);
        input.AddHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostCapture), true);
        input.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown), true);
        input.AddHandler(UIElement.KeyUpEvent, new KeyEventHandler(OnKeyUp), true);
        input.AddHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnLostFocus), true);
        input.IsEnabledChanged += OnEnabledChanged;
    }

    /// <summary>The press transform; the surface applies it to its template root.</summary>
    public Transform Transform { get; }

    /// <summary>A pointer or key is holding the surface down.</summary>
    public bool IsHeld => _pointer || _keyboard;

    /// <summary>The press spring's value: 0 at rest, 1 held, below 0 while the release overshoots.</summary>
    internal double PressAmount => _press.Value;

    public void Detach()
    {
        _input.RemoveHandler(UIElement.MouseLeftButtonDownEvent, new MouseButtonEventHandler(OnDown));
        _input.RemoveHandler(UIElement.MouseLeftButtonUpEvent, new MouseButtonEventHandler(OnUp));
        _input.RemoveHandler(UIElement.MouseMoveEvent, new MouseEventHandler(OnMove));
        _input.RemoveHandler(UIElement.MouseEnterEvent, new MouseEventHandler(OnEnter));
        _input.RemoveHandler(UIElement.MouseLeaveEvent, new MouseEventHandler(OnLeave));
        _input.RemoveHandler(UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostCapture));
        _input.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(OnKeyDown));
        _input.RemoveHandler(UIElement.KeyUpEvent, new KeyEventHandler(OnKeyUp));
        _input.RemoveHandler(UIElement.LostKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(OnLostFocus));
        _input.IsEnabledChanged -= OnEnabledChanged;
        Release();
        GlassFrameLoop.Stop(this);
        Reset();
    }

    /// <summary>Lets go of any press and returns the surface to rest immediately.</summary>
    public void Cancel()
    {
        _keyboard = false;
        _hovering = false;
        if (_pointer)
        {
            _pointer = false;
            if (_capturedByUs && ReferenceEquals(Mouse.Captured, _input)) _input.ReleaseMouseCapture();
            _capturedByUs = false;
        }
        GlassFrameLoop.Stop(this);
        Reset();
        _surface.SetIsPressed(false);
    }

    /// <summary>Returns the surface to rest immediately.</summary>
    public void Reset()
    {
        foreach (var spring in new[] { _tx, _ty, _press, _glow }) spring.Jump(0);
        _scale.ScaleX = _scale.ScaleY = 1;
        _translate.X = _translate.Y = 0;
        _surface.Lens?.SetGlow(_glowPoint, 0);
    }

    bool IGlassAnimation.Tick(double dt)
    {
        var width = Math.Max(1, _surface.ActualWidth);
        var height = Math.Max(1, _surface.ActualHeight);
        var size = Math.Max(width, height);
        var motion = GlassResolve.Motion(_surface);
        if (_pointer)
        {
            var p = Mouse.GetPosition(_surface);
            var small = size <= GlassInteractionModel.SmallControlSize;
            // Controls give a little under the finger; surfaces only at their edges.
            var ox = small ? p.X - _pressPoint.X : p.X - GlassMath.Clamp(p.X, 0, width);
            var oy = small ? p.Y - _pressPoint.Y : p.Y - GlassMath.Clamp(p.Y, 0, height);
            var limit = GlassInteractionModel.StretchLimit(width, height);
            _tx.Target = GlassInteractionModel.RubberBand(ox, limit);
            _ty.Target = GlassInteractionModel.RubberBand(oy, limit);
            _glowPoint = LensPoint();
            // Velocity of the surface itself, for surfaces the application drags with the pointer.
            var screen = ScreenOrigin();
            if (screen is { } now && _lastScreen is { } before && dt > 0)
                _carried += ((now - before) / dt - _carried) * Math.Min(1, dt * 12);
            _lastScreen = screen;
        }
        else
        {
            _carried *= Math.Exp(-dt * 10);
        }
        foreach (var spring in new[] { _tx, _ty, _press, _glow })
        {
            if (motion == GlassMotion.None) spring.Jump(spring.Target);
            else spring.Step(dt);
        }

        var elastic = motion == GlassMotion.Full;
        var grow = elastic ? Math.Min(0.16, 8 / size) * _press.Value : 0;
        var cap = size <= GlassInteractionModel.SmallControlSize ? 0.2 : 0.12;
        var ex = elastic ? Math.Min(cap, 0.5 * Math.Abs(_tx.Value) / width + 0.00007 * Math.Abs(_tx.Velocity) + 0.00005 * Math.Abs(_carried.X)) : 0;
        var ey = elastic ? Math.Min(cap, 0.5 * Math.Abs(_ty.Value) / height + 0.00007 * Math.Abs(_ty.Velocity) + 0.00005 * Math.Abs(_carried.Y)) : 0;
        _scale.CenterX = width / 2;
        _scale.CenterY = height / 2;
        _scale.ScaleX = (1 + grow) * (1 + ex) / Math.Sqrt(1 + ey);
        _scale.ScaleY = (1 + grow) * (1 + ey) / Math.Sqrt(1 + ex);
        _translate.X = elastic ? _tx.Value : 0;
        _translate.Y = elastic ? _ty.Value : 0;
        _surface.Lens?.SetGlow(_glowPoint, Math.Max(0, _glow.Value));
        GlassScene.FindScene(_surface)?.RequestRealign();
        var active = _pointer || _keyboard || !_tx.IsSettled || !_ty.IsSettled || !_press.IsSettled || !_glow.IsSettled;
        if (!active) _lastScreen = null;
        return active;
    }

    private void OnDown(object sender, MouseButtonEventArgs e)
    {
        if (IsClaimed(e) || !_input.IsEnabled || !_surface.IsInteractive) return;
        Claim(e);
        _pointer = true;
        _pressPoint = e.GetPosition(_surface);
        _glowPoint = LensPoint();
        _carried = default;
        _lastScreen = null;
        var motion = GlassResolve.Motion(_surface);
        var follow = GlassSprings.TrackWhileDragging(_surface.ActualWidth, _surface.ActualHeight);
        _tx.Animate(0, follow);
        _ty.Animate(0, follow);
        _press.Animate(1, GlassResolve.Spring(GlassSprings.Press, motion));
        // The light settles on the glow spring (0.42 s, no bounce), not the press spring, so it never passes full.
        Light(1, GlassSprings.Glow);
        if (Mouse.Captured is null) _capturedByUs = _input.CaptureMouse();
        _surface.SetIsPressed(true);
        GlassFrameLoop.Start(this);
    }

    private void OnUp(object sender, MouseButtonEventArgs e)
    {
        if (!_pointer) return;
        Release();
    }

    private void OnMove(object sender, MouseEventArgs e)
    {
        if (_pointer) return;
        if (e.StylusDevice is not null || !_hovering) return;
        _glowPoint = LensPoint();
        if (_glow.Value > 1e-3) GlassFrameLoop.Start(this);
    }

    private void OnEnter(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice is not null || !_surface.IsInteractive || !_input.IsEnabled) return;
        _hovering = true;
        _glowPoint = LensPoint();
        if (!IsHeld) Light(HoverGlow, GlassSprings.Glow);
    }

    private void OnLeave(object sender, MouseEventArgs e)
    {
        if (e.StylusDevice is not null) return;
        _hovering = false;
        if (!IsHeld) Light(0, GlassSprings.Glow);
    }

    private void OnLostCapture(object sender, MouseEventArgs e)
    {
        // Whoever held the capture (this surface, or a button that captured the press before it), losing it ends the
        // press: the window was deactivated, or another element took the mouse, and no button-up will arrive here. On
        // an ordinary release the button lets go of the capture first and the button-up then finds nothing to do.
        if (_pointer && !_input.IsMouseCaptureWithin) Release();
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Space or Key.Enter) || e.IsRepeat || IsClaimed(e) || IsHeld || !_input.IsEnabled || !_surface.IsInteractive) return;
        Claim(e);
        _keyboard = true;
        _glowPoint = new Point(_surface.ActualWidth / 2, _surface.ActualHeight / 2);
        var motion = GlassResolve.Motion(_surface);
        _press.Animate(1, GlassResolve.Spring(GlassSprings.Press, motion));
        // The light settles on the glow spring (0.42 s, no bounce), not the press spring, so it never passes full.
        Light(1, GlassSprings.Glow);
        _surface.SetIsPressed(true);
        GlassFrameLoop.Start(this);
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        if (!_keyboard || e.Key is not (Key.Space or Key.Enter)) return;
        _keyboard = false;
        Settle();
    }

    private void OnLostFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (!_keyboard) return;
        _keyboard = false;
        Settle();
    }

    private void OnEnabledChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_input.IsEnabled) return;
        _keyboard = false;
        _hovering = false;
        Release();
    }

    private void Release()
    {
        if (!_pointer) return;
        _pointer = false;
        if (_capturedByUs && ReferenceEquals(Mouse.Captured, _input)) _input.ReleaseMouseCapture();
        _capturedByUs = false;
        Settle();
    }

    private void Settle()
    {
        if (IsHeld) return;
        var motion = GlassResolve.Motion(_surface);
        var release = GlassResolve.Spring(GlassSprings.Release, motion);
        _press.Animate(0, release);
        _tx.Animate(0, release);
        _ty.Animate(0, release);
        Light(_hovering ? HoverGlow : 0, GlassSprings.Glow);
        _surface.SetIsPressed(false);
        GlassFrameLoop.Start(this);
    }

    private void Light(double target, SpringOptions spring)
    {
        _glow.Animate(target, spring);
        GlassFrameLoop.Start(this);
    }

    private static bool IsClaimed(RoutedEventArgs e) => _claimed is not null && _claimed.TryGetTarget(out var claimed) && ReferenceEquals(claimed, e);

    private static void Claim(RoutedEventArgs e)
    {
        if (_claimed is null) _claimed = new WeakReference<RoutedEventArgs>(e);
        else _claimed.SetTarget(e);
    }

    private Point LensPoint() => _surface.Lens is { } lens ? Mouse.GetPosition(lens) : Mouse.GetPosition(_surface);

    private Point? ScreenOrigin()
    {
        var root = PresentationSource.FromVisual(_surface)?.RootVisual;
        if (root is null || !_surface.IsDescendantOf(root)) return null;
        return _surface.TransformToAncestor(root).Transform(default);
    }
}
