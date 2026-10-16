using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A glass container: its content draws above glass that refracts the scene's backdrop at the surface's exact
/// position. Material and appearance are inherited from the scene unless set on the surface.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassSurface Material="Regular" Radius="28" Padding="20" IsInteractive="True">
///   <TextBlock Text="Drag me" FontSize="22" FontWeight="SemiBold" />
/// </lg:GlassSurface>
/// ]]></code>
/// </example>
[TemplatePart(Name = RootPart, Type = typeof(FrameworkElement))]
[TemplatePart(Name = LensPart, Type = typeof(GlassLens))]
public class GlassSurface : ContentControl
{
    /// <summary>Material of this surface and the glass inside it (inherited).</summary>
    public static readonly DependencyProperty MaterialProperty = GlassScene.MaterialProperty.AddOwner(typeof(GlassSurface),
        new FrameworkPropertyMetadata(GlassMaterial.Clear, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Appearance of this surface and the glass inside it (inherited).</summary>
    public static readonly DependencyProperty AppearanceProperty = GlassScene.AppearanceProperty.AddOwner(typeof(GlassSurface),
        new FrameworkPropertyMetadata(GlassAppearance.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Motion of this surface and the glass inside it (inherited).</summary>
    public static readonly DependencyProperty MotionProperty = GlassScene.MotionProperty.AddOwner(typeof(GlassSurface),
        new FrameworkPropertyMetadata(GlassMotion.Full, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Corner radius: a number of units (continuous corners), <c>capsule</c> or <c>circle</c>. Default 16.</summary>
    public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register(
        nameof(Radius), typeof(GlassRadius), typeof(GlassSurface), new FrameworkPropertyMetadata(GlassRadius.FromValue(16)));

    /// <summary>Optical travel at the outline, in units. Default 60.</summary>
    public static readonly DependencyProperty RefractionProperty = DependencyProperty.Register(
        nameof(Refraction), typeof(double), typeof(GlassSurface), new FrameworkPropertyMetadata(GlassOptics.DefaultRefraction),
        v => v is double d && d >= 0 && !double.IsInfinity(d));

    /// <summary>Colors the glass (never the content). Null for untinted glass.</summary>
    public static readonly DependencyProperty TintProperty = DependencyProperty.Register(
        nameof(Tint), typeof(Color?), typeof(GlassSurface), new FrameworkPropertyMetadata(null, OnTintChanged));

    /// <summary>Responds to press, drag, hover and keyboard with grow, stretch and glow. Default false.</summary>
    public static readonly DependencyProperty IsInteractiveProperty = DependencyProperty.Register(
        nameof(IsInteractive), typeof(bool), typeof(GlassSurface), new FrameworkPropertyMetadata(false, OnInteractiveChanged));

    /// <summary>Springs the outline to its new box when the surface's layout changes. Default false.</summary>
    public static readonly DependencyProperty IsFluidProperty = DependencyProperty.Register(
        nameof(IsFluid), typeof(bool), typeof(GlassSurface), new FrameworkPropertyMetadata(false, OnFluidChanged));

    /// <summary>Draws the keyboard focus ring around the outline. Controls bind it to their keyboard focus.</summary>
    public static readonly DependencyProperty IsFocusRingVisibleProperty = DependencyProperty.Register(
        nameof(IsFocusRingVisible), typeof(bool), typeof(GlassSurface), new FrameworkPropertyMetadata(false));

    private static readonly DependencyPropertyKey IsPressedPropertyKey = DependencyProperty.RegisterReadOnly(
        nameof(IsPressed), typeof(bool), typeof(GlassSurface), new FrameworkPropertyMetadata(false));

    /// <summary>Whether a pointer or key is holding the (interactive) surface down.</summary>
    public static readonly DependencyProperty IsPressedProperty = IsPressedPropertyKey.DependencyProperty;

    private const string RootPart = "PART_Root";
    private const string LensPart = "PART_Lens";

    private readonly GlassAppearanceTracker _appearance;
    private FrameworkElement? _root;
    private GlassInteraction? _interaction;
    private UIElement? _inputSource;
    private FluidShape? _fluid;

    static GlassSurface()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassSurface), new FrameworkPropertyMetadata(typeof(GlassSurface)));
        GlassScene.AddContentForegroundOwner(typeof(GlassSurface));
        FocusableProperty.OverrideMetadata(typeof(GlassSurface), new FrameworkPropertyMetadata(false));
        IsTabStopProperty.OverrideMetadata(typeof(GlassSurface), new FrameworkPropertyMetadata(false));
    }

    /// <summary>Creates a surface.</summary>
    public GlassSurface()
    {
        _appearance = new GlassAppearanceTracker(this, () => Tint);
    }

    /// <summary>Material of this surface and the glass inside it.</summary>
    public GlassMaterial Material
    {
        get => (GlassMaterial)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    /// <summary>Appearance of this surface and the glass inside it.</summary>
    public GlassAppearance Appearance
    {
        get => (GlassAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>Motion of this surface and the glass inside it.</summary>
    public GlassMotion Motion
    {
        get => (GlassMotion)GetValue(MotionProperty);
        set => SetValue(MotionProperty, value);
    }

    /// <summary>Corner radius.</summary>
    public GlassRadius Radius
    {
        get => (GlassRadius)GetValue(RadiusProperty);
        set => SetValue(RadiusProperty, value);
    }

    /// <summary>Optical travel at the outline, in units.</summary>
    public double Refraction
    {
        get => (double)GetValue(RefractionProperty);
        set => SetValue(RefractionProperty, value);
    }

    /// <summary>Tint color, or null.</summary>
    public Color? Tint
    {
        get => (Color?)GetValue(TintProperty);
        set => SetValue(TintProperty, value);
    }

    /// <summary>Responds to press, drag, hover and keyboard.</summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Springs the outline to its new box when layout changes.</summary>
    public bool IsFluid
    {
        get => (bool)GetValue(IsFluidProperty);
        set => SetValue(IsFluidProperty, value);
    }

    /// <summary>Draws the keyboard focus ring.</summary>
    public bool IsFocusRingVisible
    {
        get => (bool)GetValue(IsFocusRingVisibleProperty);
        set => SetValue(IsFocusRingVisibleProperty, value);
    }

    /// <summary>Whether the surface is held down.</summary>
    public bool IsPressed => (bool)GetValue(IsPressedProperty);

    /// <summary>How the surface's glass currently draws, or null before its template is applied.</summary>
    public GlassLensMode? RenderedMode => Lens?.Mode;

    /// <summary>The lens in the template.</summary>
    internal GlassLens? Lens { get; private set; }

    /// <summary>
    /// The element whose input drives the press response: the control a surface belongs to (for example a button
    /// whose template it is), or the surface itself.
    /// </summary>
    internal UIElement InputSource
    {
        get => _inputSource ?? this;
        set
        {
            _inputSource = value;
            AttachInteraction();
        }
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        _root = GetTemplateChild(RootPart) as FrameworkElement;
        Lens = GetTemplateChild(LensPart) as GlassLens;
        AttachInteraction();
        _fluid?.Attach();
    }

    internal void SetIsPressed(bool pressed) => SetValue(IsPressedPropertyKey, pressed);

    /// <inheritdoc />
    protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
    {
        // The glass takes input across its rounded outline (it draws nothing hit-testable itself); the corners outside
        // the outline do not.
        var p = hitTestParameters.HitPoint;
        var size = RenderSize;
        if (size.Width <= 0 || size.Height <= 0) return null;
        var shape = GlassShape.Parameters(size.Width, size.Height, Radius);
        var field = GlassShape.Field(p.X - size.Width / 2, p.Y - size.Height / 2, size.Width, size.Height, shape);
        return field.Distance <= 0 ? new PointHitTestResult(this, p) : null;
    }

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassSurface)d)._appearance.Update();

    private static void OnInteractiveChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassSurface)d).AttachInteraction();

    private static void OnFluidChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var surface = (GlassSurface)d;
        if ((bool)e.NewValue)
        {
            surface._fluid ??= new FluidShape(surface);
            surface._fluid.Attach();
        }
        else
        {
            surface._fluid?.Detach();
            surface._fluid = null;
        }
    }

    private void AttachInteraction()
    {
        _interaction?.Detach();
        _interaction = null;
        if (_root is null) return;
        if (!IsInteractive)
        {
            _root.RenderTransform = Transform.Identity;
            return;
        }
        _interaction = new GlassInteraction(this, InputSource);
        _root.RenderTransform = _interaction.Transform;
    }

    /// <summary>Springs the lens outline from its previous box to the surface's new one (layout spring).</summary>
    private sealed class FluidShape : IGlassAnimation
    {
        private readonly GlassSurface _surface;
        private readonly Spring _x = new(0, GlassSprings.Layout);
        private readonly Spring _y = new(0, GlassSprings.Layout);
        private readonly Spring _width = new(0, GlassSprings.Layout);
        private readonly Spring _height = new(0, GlassSprings.Layout);
        private Point? _origin;
        private Size _size;
        private bool _attached;

        public FluidShape(GlassSurface surface) => _surface = surface;

        public void Attach()
        {
            if (_attached) return;
            _attached = true;
            _surface.LayoutUpdated += OnLayoutUpdated;
        }

        public void Detach()
        {
            if (!_attached) return;
            _attached = false;
            _surface.LayoutUpdated -= OnLayoutUpdated;
            GlassFrameLoop.Stop(this);
            _surface.Lens?.SetShape(null);
        }

        public bool Tick(double dt)
        {
            foreach (var spring in new[] { _x, _y, _width, _height }) spring.Step(dt);
            var settled = _x.IsSettled && _y.IsSettled && _width.IsSettled && _height.IsSettled;
            _surface.Lens?.SetShape(settled ? null : new Rect(_x.Value, _y.Value, Math.Max(0, _width.Value), Math.Max(0, _height.Value)));
            GlassScene.FindScene(_surface)?.RequestRealign();
            return !settled;
        }

        private void OnLayoutUpdated(object? sender, EventArgs e)
        {
            var scene = GlassScene.FindScene(_surface);
            if (scene is null || !_surface.IsLoaded || !_surface.IsDescendantOf(scene)) return;
            var origin = _surface.TransformToAncestor(scene).Transform(default);
            var size = _surface.RenderSize;
            if (_origin is not { } previous)
            {
                _origin = origin;
                _size = size;
                return;
            }
            if (Near(previous, origin) && size == _size) return;
            // The current outline, expressed in the surface's new coordinates, springs to the new bounds.
            var shift = previous - origin;
            var running = GlassFrameLoop.IsRunning(this);
            var oldSize = _size;
            _origin = origin;
            _size = size;
            if (GlassResolve.Motion(_surface) != GlassMotion.Full || size.Width <= 0 || size.Height <= 0 || oldSize.Width <= 0)
            {
                GlassFrameLoop.Stop(this);
                _surface.Lens?.SetShape(null);
                return;
            }
            if (running)
            {
                // Keep the velocity of the outline in flight.
                _x.Shift(shift.X);
                _y.Shift(shift.Y);
            }
            else
            {
                _x.Jump(shift.X);
                _y.Jump(shift.Y);
                _width.Jump(oldSize.Width);
                _height.Jump(oldSize.Height);
            }
            _x.Animate(0, GlassSprings.Layout);
            _y.Animate(0, GlassSprings.Layout);
            _width.Animate(size.Width, GlassSprings.Layout);
            _height.Animate(size.Height, GlassSprings.Layout);
            // Draw the old outline in this very frame, before the new layout renders.
            _surface.Lens?.SetShape(new Rect(_x.Value, _y.Value, Math.Max(0, _width.Value), Math.Max(0, _height.Value)));
            GlassFrameLoop.Start(this);
        }

        private static bool Near(Point a, Point b) => Math.Abs(a.X - b.X) < 0.01 && Math.Abs(a.Y - b.Y) < 0.01;
    }
}
