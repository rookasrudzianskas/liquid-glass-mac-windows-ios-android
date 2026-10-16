using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using LiquidGlass.Studio.Shell;

namespace LiquidGlass.Studio.Pages;

/// <summary>Progressive blur: a scrolling journal whose top and bottom edges blur under floating glass.</summary>
public partial class BlurPage : StudioPageBase
{
    private static readonly (string Title, string Text, string Wallpaper, Rect Crop)[] Stories =
    {
        ("Light that bends", "A glass edge draws its image from deeper inside, so content near the rim swims toward the center like a thick lens.", Wallpapers.Dunes, new Rect(0.05, 0.25, 0.5, 0.35)),
        ("Frost without fog", "Regular glass blurs what lies behind it and lifts it toward a soft white, keeping color but losing detail.", Wallpapers.Ocean, new Rect(0.4, 0.1, 0.55, 0.38)),
        ("A rim of light", "A thin specular line runs along the top and bottom of every outline; the sides carry a gentle shade.", Wallpapers.Aurora, new Rect(0.2, 0.45, 0.6, 0.4)),
        ("Springs, not timelines", "Every motion is a damped spring solved in closed form, so a press can reverse mid-flight without a jump.", Wallpapers.Midnight, new Rect(0.1, 0.3, 0.55, 0.38)),
        ("Edges that soften", "Six blur levels rise toward the edge of a scrolling view, so text slides under glass without clashing with it.", Wallpapers.Dunes, new Rect(0.35, 0.5, 0.6, 0.38)),
        ("One model, four platforms", "SwiftUI, Jetpack Compose, WPF and the web share the same shapes, materials and springs, checked by the same vectors.", Wallpapers.Ocean, new Rect(0.0, 0.55, 0.6, 0.38)),
    };

    private readonly SolidColorBrush _paper = new();
    private readonly SolidColorBrush _ink = new();
    private readonly SolidColorBrush _subtle = new();

    public BlurPage()
    {
        InitializeComponent();
        Paper.Background = _paper;
        foreach (var story in Stories) Articles.Children.Add(Article(story.Title, story.Text, story.Wallpaper, story.Crop));
    }

    /// <inheritdoc />
    public override void SetDark(bool dark)
    {
        _paper.Color = dark ? Color.FromRgb(0x14, 0x14, 0x17) : Color.FromRgb(0xF6, 0xF5, 0xF2);
        _ink.Color = dark ? Colors.White : Color.FromRgb(0x16, 0x16, 0x19);
        _subtle.Color = dark ? Color.FromRgb(0xB8, 0xB8, 0xC0) : Color.FromRgb(0x5A, 0x5A, 0x62);
    }

    private UIElement Article(string title, string text, string wallpaper, Rect crop)
    {
        var photo = new Border
        {
            Height = 240,
            CornerRadius = new CornerRadius(18),
            Background = new ImageBrush(Wallpapers.Get(wallpaper)) { Stretch = Stretch.UniformToFill, Viewbox = crop },
        };
        var heading = new TextBlock
        {
            Text = title,
            FontFamily = (FontFamily)FindResource("Studio.DisplayFont"),
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = _ink,
            Margin = new Thickness(4, 16, 4, 6),
        };
        var body = new TextBlock
        {
            Text = text,
            FontSize = 15,
            LineHeight = 23,
            TextWrapping = TextWrapping.Wrap,
            Foreground = _subtle,
            Margin = new Thickness(4, 0, 4, 0),
        };
        var panel = new StackPanel { Margin = new Thickness(0, 0, 0, 40) };
        panel.Children.Add(photo);
        panel.Children.Add(heading);
        panel.Children.Add(body);
        return panel;
    }
}
