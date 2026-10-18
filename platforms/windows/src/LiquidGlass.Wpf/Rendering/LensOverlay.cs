using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Draws what a <see cref="GlassLens"/> paints over its copy of the backdrop when the pixel shader is not drawing it:
/// the veil or opaque fill, a menu panel's legibility backing, the increased-contrast border, the rim light (rasterized
/// exactly as the shader computes it), the pointer glow (in the shader's order) and the keyboard focus ring.
/// </summary>
internal sealed class LensOverlay : FrameworkElement
{
    private readonly GlassLens _lens;
    private Geometry _outline = Geometry.Empty;
    private RimKey _rimKey;
    private BitmapSource? _rim;
    private RimImage? _rimImage;

    public LensOverlay(GlassLens lens)
    {
        _lens = lens;
        IsHitTestVisible = false;
        Focusable = false;
    }

    /// <summary>The rasterized rim light, while the lens draws one.</summary>
    internal BitmapSource? RimBitmap => _rim;

    /// <summary>Recomputes the cached outline and rim after the lens changed.</summary>
    public void Refresh()
    {
        var shape = _lens.ShapeBounds;
        _outline = GlassGeometry.Create(shape, _lens.Radius);
        if (_lens.Mode is GlassLensMode.Fallback or GlassLensMode.Opaque && shape.Width > 0 && shape.Height > 0)
        {
            // The lens's scale, not this element's: after a DPI change WPF notifies a parent before it updates its
            // children, so while the lens refreshes from its own notification this element still reports the old DPI.
            var scale = GlassResolve.Scale(_lens);
            var key = new RimKey(shape.Size, _lens.Radius, _lens.IsDark, scale, _lens.EffectiveTint);
            if (_rim is null || !key.Equals(_rimKey))
            {
                _rimKey = key;
                var tint = _lens.EffectiveTint is { } t ? GlassBrushes.ToRgb(t) : (Rgb?)null;
                _rimImage = GlassRimRaster.Render(shape.Width, shape.Height, _lens.Radius, _lens.IsDark, scale, tint);
                var bitmap = BitmapSource.Create(_rimImage.PixelWidth, _rimImage.PixelHeight, 96 * scale, 96 * scale,
                    PixelFormats.Pbgra32, null, _rimImage.Pixels, _rimImage.Stride);
                bitmap.Freeze();
                _rim = bitmap;
            }
        }
        else
        {
            _rim = null;
            _rimImage = null;
        }
        InvalidateVisual();
    }

    /// <inheritdoc />
    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        Refresh();
    }

    protected override void OnRender(DrawingContext dc)
    {
        var shape = _lens.ShapeBounds;
        if (shape.Width <= 0 || shape.Height <= 0) return;
        var dark = _lens.IsDark;
        switch (_lens.Mode)
        {
            case GlassLensMode.HighContrast:
            {
                var border = new Pen(SystemColors.ControlTextBrush, 1);
                dc.DrawGeometry(SystemColors.ControlBrush, null, _outline);
                dc.DrawGeometry(null, border, GlassGeometry.Inflate(shape, _lens.Radius, -0.5));
                break;
            }
            case GlassLensMode.Fallback:
                dc.DrawGeometry(FallbackFill(dark), null, _outline);
                // The legibility backing over the veil (or tint) at the lens's amount: backing·L + glass·(1 − L) is
                // the shader's mix(glass, backing, L).
                if (_lens.Legibility > 0)
                    dc.DrawGeometry(new SolidColorBrush(GlassBrushes.ToColor(GlassPalette.LegibilityBacking(dark), _lens.Legibility)), null, _outline);
                DrawLight(dc, shape, dark);
                break;
            case GlassLensMode.Opaque:
                dc.DrawGeometry(OpaqueFill(dark), null, _outline);
                DrawLight(dc, shape, dark);
                break;
        }
        if (_lens.IsFocusRingVisible)
        {
            var color = _lens.Mode == GlassLensMode.HighContrast ? SystemColors.HighlightColor : dark ? Colors.White : GlassBrushes.ToColor(GlassPalette.ForegroundLight);
            dc.DrawGeometry(null, new Pen(new SolidColorBrush(color), 2), GlassGeometry.Inflate(shape, _lens.Radius, 2));
        }
    }

    private void DrawLight(DrawingContext dc, Rect shape, bool dark)
    {
        // Border, rim, glow: the order of the shader and of the other ports.
        if (_lens.IncreaseContrast)
        {
            var pen = new Pen(new SolidColorBrush(GlassBrushes.ToColor(GlassPalette.ContrastBorder(dark))), 1);
            dc.DrawGeometry(null, pen, GlassGeometry.Inflate(shape, _lens.Radius, -0.5));
        }
        if (_rim is not null && _rimImage is not null)
        {
            var pixel = _rimImage.Pixel;
            dc.DrawImage(_rim, new Rect(shape.X - _rimImage.Padding * pixel, shape.Y - _rimImage.Padding * pixel,
                _rimImage.PixelWidth * pixel, _rimImage.PixelHeight * pixel));
        }
        if (_lens.GlowAmount > 1e-3)
        {
            var glow = GlassInteractionModel.Glow(shape.Width, shape.Height, dark);
            var alpha = glow.Alpha * GlassMath.Sat(_lens.GlowAmount);
            var brush = new RadialGradientBrush
            {
                MappingMode = BrushMappingMode.Absolute,
                Center = _lens.GlowCenter,
                GradientOrigin = _lens.GlowCenter,
                RadiusX = glow.Radius,
                RadiusY = glow.Radius,
            };
            brush.GradientStops.Add(new GradientStop(Color.FromArgb((byte)Math.Round(alpha * 255), 255, 255, 255), 0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(0, 255, 255, 255), 1));
            dc.DrawGeometry(brush, null, _outline);
        }
    }

    private Brush FallbackFill(bool dark)
    {
        if (_lens.EffectiveTint is { } tint) return TintFill(tint);
        var veil = GlassFallback.Veil(_lens.Material, dark);
        return new SolidColorBrush(GlassBrushes.ToColor(new Rgb(veil.Gray, veil.Gray, veil.Gray), veil.Opacity));
    }

    private Brush OpaqueFill(bool dark)
    {
        if (_lens.EffectiveTint is { } tint) return TintFill(tint);
        // Glass inside opaque glass (a tab bar's selection, a button on a card) is a step lighter so it stays visible.
        if (_lens.IsNested) return new SolidColorBrush(dark ? Color.FromArgb(0xF0, 0x3A, 0x3A, 0x3C) : Color.FromArgb(0xF0, 0xFF, 0xFF, 0xFF));
        return new SolidColorBrush(GlassBrushes.ToColor(GlassPalette.ReducedTransparency(dark)));
    }

    /// <summary>
    /// Tinted glass is nearly opaque pigment that transmits the backdrop's luminance; without the shader,
    /// the pigment at its body strength over the frosted copy reads the same way.
    /// </summary>
    private Brush TintFill(Color tint)
    {
        var body = GlassMaterials.TintBody(_lens.Material);
        return new SolidColorBrush(GlassBrushes.ToColor(GlassBrushes.ToRgb(tint).Map(c => c * (body + (1 - body) * 0.5)), 0.9));
    }

    private readonly struct RimKey : IEquatable<RimKey>
    {
        private readonly Size _size;
        private readonly GlassRadius _radius;
        private readonly bool _dark;
        private readonly double _scale;
        private readonly Color? _tint;

        public RimKey(Size size, GlassRadius radius, bool dark, double scale, Color? tint)
        {
            _size = size;
            _radius = radius;
            _dark = dark;
            _scale = scale;
            _tint = tint;
        }

        public bool Equals(RimKey other) => _size == other._size && _radius == other._radius && _dark == other._dark
                                            && _scale.Equals(other._scale) && Nullable.Equals(_tint, other._tint);

        public override bool Equals(object? obj) => obj is RimKey other && Equals(other);

        public override int GetHashCode() => _size.GetHashCode() ^ _radius.GetHashCode() ^ _scale.GetHashCode();
    }
}
