using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using LiquidGlass.Studio.Shell;
using LiquidGlass.Wpf;

namespace LiquidGlass.Studio.Pages;

/// <summary>Materials lab: live controls for material, appearance, refraction, tint and radius, with a draggable lens.</summary>
public partial class MaterialsPage : StudioPageBase
{
    private string? _wallpaper;
    private Point? _grab;
    private Point _anchor = new(0.5, 0.5);

    public MaterialsPage()
    {
        InitializeComponent();
        foreach (var name in Wallpapers.All)
        {
            var chip = new Button
            {
                Width = 44,
                Height = 30,
                Margin = new Thickness(0, 0, 8, 0),
                Cursor = Cursors.Hand,
                Tag = name,
                Template = ChipTemplate(name),
            };
            System.Windows.Automation.AutomationProperties.SetName(chip, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name));
            chip.Click += (_, _) => Show((string)chip.Tag);
            WallpaperChips.Children.Add(chip);
        }
        MaterialPicker.SelectionChanged += (_, _) => Lens.Material = MaterialPicker.SelectedIndex == 1 ? GlassMaterial.Regular : GlassMaterial.Clear;
        AppearancePicker.SelectionChanged += (_, _) =>
        {
            if (AppearancePicker.SelectedIndex == 0) Lens.ClearValue(GlassSurface.AppearanceProperty);
            else Lens.Appearance = AppearancePicker.SelectedIndex == 1 ? GlassAppearance.Light : GlassAppearance.Dark;
        };
        RefractionSlider.ValueChanged += (_, e) => Lens.Refraction = e.NewValue;
        RadiusSlider.ValueChanged += (_, _) => UpdateRadius();
        FallbackSwitch.Checked += (_, _) => GlassScene.SetRenderMode(Lens, GlassRenderMode.Fallback);
        FallbackSwitch.Unchecked += (_, _) => Lens.ClearValue(GlassScene.RenderModeProperty);
        ContrastSwitch.Checked += (_, _) => GlassScene.SetIncreaseContrast(Lens, true);
        ContrastSwitch.Unchecked += (_, _) => Lens.ClearValue(GlassScene.IncreaseContrastProperty);
        Stage.SizeChanged += (_, _) => PlaceLens();
        Lens.SizeChanged += (_, _) => PlaceLens();
        Lens.PreviewMouseLeftButtonDown += (_, e) =>
        {
            _grab = e.GetPosition(Lens);
            Lens.CaptureMouse();
        };
        Lens.PreviewMouseMove += OnLensMove;
        Lens.PreviewMouseLeftButtonUp += (_, _) =>
        {
            _grab = null;
            Lens.ReleaseMouseCapture();
        };
        UpdateRadius();
    }

    /// <inheritdoc />
    public override void SetDark(bool dark)
    {
        if (_wallpaper is null) Show(dark ? Wallpapers.Aurora : Wallpapers.Ocean, remember: false);
    }

    private void Show(string name, bool remember = true)
    {
        if (remember) _wallpaper = name;
        Wallpaper.Source = Wallpapers.Get(name);
    }

    private void UpdateRadius()
    {
        // The top of the range is a capsule: radii that large blend into it anyway (OPTICS §1).
        var capsule = RadiusSlider.Value >= RadiusSlider.Maximum - 0.5;
        Lens.Radius = capsule ? GlassRadius.Capsule : GlassRadius.FromValue(RadiusSlider.Value);
        RadiusLabel.Text = capsule ? "capsule" : RadiusSlider.Value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void OnTint(object sender, RoutedEventArgs e)
    {
        var tag = (string)((FrameworkElement)sender).Tag;
        Lens.Tint = string.IsNullOrEmpty(tag) ? null : (Color)ColorConverter.ConvertFromString(tag);
    }

    private void PlaceLens()
    {
        if (Stage.ActualWidth <= 0 || Lens.ActualWidth <= 0) return;
        Canvas.SetLeft(Lens, Clamp(_anchor.X * Stage.ActualWidth - Lens.ActualWidth / 2, 0, Stage.ActualWidth - Lens.ActualWidth));
        Canvas.SetTop(Lens, Clamp(_anchor.Y * Stage.ActualHeight - Lens.ActualHeight / 2, 0, Stage.ActualHeight - Lens.ActualHeight));
    }

    private void OnLensMove(object sender, MouseEventArgs e)
    {
        if (_grab is not { } grab || e.LeftButton != MouseButtonState.Pressed) return;
        var p = e.GetPosition(Stage);
        var x = Clamp(p.X - grab.X, 0, Stage.ActualWidth - Lens.ActualWidth);
        var y = Clamp(p.Y - grab.Y, 0, Stage.ActualHeight - Lens.ActualHeight);
        Canvas.SetLeft(Lens, x);
        Canvas.SetTop(Lens, y);
        _anchor = new Point((x + Lens.ActualWidth / 2) / Stage.ActualWidth, (y + Lens.ActualHeight / 2) / Stage.ActualHeight);
    }

    /// <summary>A rounded thumbnail of a wallpaper, with a ring when it has keyboard focus.</summary>
    private static ControlTemplate ChipTemplate(string wallpaper)
    {
        var template = new ControlTemplate(typeof(ButtonBase));
        var root = new FrameworkElementFactory(typeof(Grid));
        var image = new FrameworkElementFactory(typeof(Border));
        image.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        image.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        image.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)));
        image.SetValue(Border.BackgroundProperty, new ImageBrush(Wallpapers.Get(wallpaper)) { Stretch = Stretch.UniformToFill, Viewbox = new Rect(0.2, 0.25, 0.6, 0.5) });
        root.AppendChild(image);
        template.VisualTree = root;
        return template;
    }

    private static double Clamp(double v, double lo, double hi) => hi < lo ? lo : v < lo ? lo : v > hi ? hi : v;
}
