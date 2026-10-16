using System;
using System.Windows;
using System.Windows.Controls;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>A stack panel with a gap between its visible children, so items keep their own margins.</summary>
internal sealed class SpacedStackPanel : Panel
{
    public static readonly DependencyProperty OrientationProperty = StackPanel.OrientationProperty.AddOwner(typeof(SpacedStackPanel),
        new FrameworkPropertyMetadata(Orientation.Horizontal, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public static readonly DependencyProperty SpacingProperty = DependencyProperty.Register(
        nameof(Spacing), typeof(double), typeof(SpacedStackPanel), new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsMeasure));

    public Orientation Orientation
    {
        get => (Orientation)GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    public double Spacing
    {
        get => (double)GetValue(SpacingProperty);
        set => SetValue(SpacingProperty, value);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        var constraint = horizontal ? new Size(double.PositiveInfinity, availableSize.Height) : new Size(availableSize.Width, double.PositiveInfinity);
        double along = 0, across = 0;
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            child.Measure(constraint);
            if (child.Visibility == Visibility.Collapsed) continue;
            var size = child.DesiredSize;
            along += (visible++ > 0 ? Spacing : 0) + (horizontal ? size.Width : size.Height);
            across = Math.Max(across, horizontal ? size.Height : size.Width);
        }
        return horizontal ? new Size(along, across) : new Size(across, along);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var horizontal = Orientation == Orientation.Horizontal;
        double offset = 0;
        var visible = 0;
        foreach (UIElement child in InternalChildren)
        {
            if (child.Visibility == Visibility.Collapsed)
            {
                child.Arrange(new Rect());
                continue;
            }
            if (visible++ > 0) offset += Spacing;
            var size = child.DesiredSize;
            child.Arrange(horizontal
                ? new Rect(offset, 0, size.Width, finalSize.Height)
                : new Rect(0, offset, finalSize.Width, size.Height));
            offset += horizontal ? size.Width : size.Height;
        }
        return finalSize;
    }
}
