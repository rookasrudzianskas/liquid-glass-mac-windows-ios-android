using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// A live copy of a visual, drawn by a rectangle so that it lines up exactly with what lies beneath the rectangle on
/// screen. Glass lenses and progressive blurs read the backdrop through these.
/// </summary>
internal sealed class BackdropCopy
{
    private readonly Visual _target;
    private readonly VisualBrush _brush;
    private Matrix? _mapping;
    private Size _size;

    public BackdropCopy(Visual target)
    {
        _target = target;
        _brush = new VisualBrush(target)
        {
            ViewboxUnits = BrushMappingMode.Absolute,
            ViewportUnits = BrushMappingMode.RelativeToBoundingBox,
            Viewport = new Rect(0, 0, 1, 1),
            Stretch = Stretch.Fill,
            TileMode = TileMode.None,
            AutoLayoutContent = false,
        };
        Rectangle = new Rectangle { Fill = _brush, IsHitTestVisible = false };
    }

    /// <summary>The rectangle that draws the copy; it fills its parent.</summary>
    public Rectangle Rectangle { get; }

    /// <summary>
    /// Maps the target onto the copy drawn across <paramref name="area"/> (of <paramref name="size"/>), whose
    /// coordinates must be the rectangle's own. A visual brush draws its target with the target's own offset and
    /// transform, so the viewbox is expressed in the coordinates of the target's parent; the targets used here sit at
    /// their parent's origin, untransformed.
    /// </summary>
    /// <remarks>
    /// The full affine mapping from <paramref name="area"/> to that space is honored, not only its bounds: a
    /// right-to-left subtree (whose mirror WPF applies as a negative horizontal scale), a rotation or a skew between
    /// the scene and the glass leaves the copy showing exactly what lies beneath each of its pixels.
    /// </remarks>
    public void Align(Visual area, Size size)
    {
        if (VisualTreeHelper.GetParent(_target) is not Visual space) return;
        GeneralTransform transform;
        try
        {
            transform = area.TransformToVisual(space);
        }
        catch (InvalidOperationException)
        {
            return; // not in the same tree, for example while unloading
        }
        // An effect with a non-affine mapping between the two leaves only the bounds to go by.
        var m = Affine(transform) ?? BoundsMapping(transform.TransformBounds(new Rect(size)), size);
        if (_mapping is { } previous && Near(previous, m) && Math.Abs(size.Width - _size.Width) < 1e-3 && Math.Abs(size.Height - _size.Height) < 1e-3) return;
        var bounds = Rect.Transform(new Rect(size), m);
        if (bounds.IsEmpty || !m.HasInverse) return;
        _mapping = m;
        _size = size;
        if (m.M12 == 0 && m.M21 == 0 && m.M11 > 0 && m.M22 > 0)
        {
            // Translation and positive scale (the usual case): the brush maps the copied bounds onto the rectangle.
            _brush.ViewportUnits = BrushMappingMode.RelativeToBoundingBox;
            _brush.Viewport = new Rect(0, 0, 1, 1);
            _brush.Viewbox = bounds;
            _brush.Transform = Transform.Identity;
            return;
        }
        // Mirrored, rotated or skewed: draw the target in its parent's coordinates (viewbox and viewport coincide),
        // then map them back into the rectangle's with the inverse transform, so the point q of the target lands at
        // M⁻¹(q), the rectangle point that sits over it on screen.
        var inverse = m;
        inverse.Invert();
        _brush.ViewportUnits = BrushMappingMode.Absolute;
        _brush.Viewbox = bounds;
        _brush.Viewport = bounds;
        _brush.Transform = new MatrixTransform(inverse);
    }

    /// <summary>Releases the target.</summary>
    public void Detach() => _brush.Visual = null;

    private static Matrix? Affine(GeneralTransform transform)
    {
        switch (transform)
        {
            case Transform affine:
                return affine.Value;
            case GeneralTransformGroup group:
                var m = Matrix.Identity;
                foreach (var child in group.Children)
                {
                    if (Affine(child) is not { } step) return null;
                    m *= step;
                }
                return m;
            default:
                return null;
        }
    }

    private static Matrix BoundsMapping(Rect bounds, Size size)
        => bounds.IsEmpty || size.Width <= 0 || size.Height <= 0
            ? Matrix.Identity
            : new Matrix(bounds.Width / size.Width, 0, 0, bounds.Height / size.Height, bounds.X, bounds.Y);

    private static bool Near(Matrix a, Matrix b)
        => Math.Abs(a.M11 - b.M11) < 1e-6 && Math.Abs(a.M12 - b.M12) < 1e-6 && Math.Abs(a.M21 - b.M21) < 1e-6
           && Math.Abs(a.M22 - b.M22) < 1e-6 && Math.Abs(a.OffsetX - b.OffsetX) < 1e-3 && Math.Abs(a.OffsetY - b.OffsetY) < 1e-3;
}
