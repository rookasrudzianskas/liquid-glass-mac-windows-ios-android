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
    private Rect _viewbox = Rect.Empty;

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
    /// Maps the target onto the copy drawn across <paramref name="area"/> (of <paramref name="size"/>). A visual brush
    /// draws its target with the target's own offset and transform, so the viewbox is expressed in the coordinates of
    /// the target's parent; the targets used here sit at their parent's origin, untransformed.
    /// </summary>
    public void Align(Visual area, Size size)
    {
        if (VisualTreeHelper.GetParent(_target) is not Visual space) return;
        Rect rect;
        try
        {
            rect = area.TransformToVisual(space).TransformBounds(new Rect(size));
        }
        catch (InvalidOperationException)
        {
            return; // not in the same tree, for example while unloading
        }
        if (rect.IsEmpty || Near(rect, _viewbox)) return;
        _viewbox = rect;
        _brush.Viewbox = rect;
    }

    /// <summary>Releases the target.</summary>
    public void Detach() => _brush.Visual = null;

    private static bool Near(Rect a, Rect b)
        => !b.IsEmpty && Math.Abs(a.X - b.X) < 1e-3 && Math.Abs(a.Y - b.Y) < 1e-3
           && Math.Abs(a.Width - b.Width) < 1e-3 && Math.Abs(a.Height - b.Height) < 1e-3;
}
