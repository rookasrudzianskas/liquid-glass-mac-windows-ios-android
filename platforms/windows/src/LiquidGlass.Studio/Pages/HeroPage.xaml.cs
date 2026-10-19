using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.Studio.Shell;

namespace LiquidGlass.Studio.Pages;

/// <summary>Hero: a vivid wallpaper under a draggable Regular card, a floating toolbar and a tab bar.</summary>
public partial class HeroPage : StudioPageBase
{
    private Point? _grab;
    // Where the card's center sits, as a fraction of the stage, so it keeps its place as the window resizes.
    private Point _anchor = new(0.5, 0.47);

    public HeroPage()
    {
        InitializeComponent();
        Stage.SizeChanged += (_, _) => PlaceCard();
        Card.SizeChanged += (_, _) => PlaceCard();
        Card.PreviewMouseLeftButtonDown += OnCardDown;
        Card.PreviewMouseMove += OnCardMove;
        Card.PreviewMouseLeftButtonUp += OnCardUp;
        Card.LostMouseCapture += (_, _) => _grab = null;
    }

    /// <inheritdoc />
    public override void SetDark(bool dark)
    {
        Wallpaper.Source = Wallpapers.Get(dark ? Wallpapers.Midnight : Wallpapers.Dunes);
        Wallpaper.VerticalAlignment = Wallpapers.Alignment(dark);
    }

    private void PlaceCard()
    {
        if (Stage.ActualWidth <= 0 || Card.ActualWidth <= 0) return;
        var x = _anchor.X * Stage.ActualWidth - Card.ActualWidth / 2;
        var y = _anchor.Y * Stage.ActualHeight - Card.ActualHeight / 2;
        Canvas.SetLeft(Card, Clamp(x, 0, Stage.ActualWidth - Card.ActualWidth));
        Canvas.SetTop(Card, Clamp(y, 0, Stage.ActualHeight - Card.ActualHeight));
    }

    private void OnCardDown(object sender, MouseButtonEventArgs e)
    {
        // Buttons on the card keep their clicks; the rest of the card is a handle.
        for (var node = e.OriginalSource as DependencyObject; node is not null && !ReferenceEquals(node, Card); node = VisualTreeHelper.GetParent(node))
            if (node is ButtonBase) return;
        _grab = e.GetPosition(Card);
        Card.CaptureMouse();
    }

    private void OnCardMove(object sender, MouseEventArgs e)
    {
        if (_grab is not { } grab || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(Stage);
        var x = Clamp(p.X - grab.X, 0, Stage.ActualWidth - Card.ActualWidth);
        var y = Clamp(p.Y - grab.Y, 0, Stage.ActualHeight - Card.ActualHeight);
        Canvas.SetLeft(Card, x);
        Canvas.SetTop(Card, y);
        _anchor = new Point((x + Card.ActualWidth / 2) / Stage.ActualWidth, (y + Card.ActualHeight / 2) / Stage.ActualHeight);
    }

    private void OnCardUp(object sender, MouseButtonEventArgs e)
    {
        if (_grab is null) return;
        _grab = null;
        Card.ReleaseMouseCapture();
    }

    private static double Clamp(double v, double lo, double hi) => hi < lo ? lo : v < lo ? lo : v > hi ? hi : v;
}
