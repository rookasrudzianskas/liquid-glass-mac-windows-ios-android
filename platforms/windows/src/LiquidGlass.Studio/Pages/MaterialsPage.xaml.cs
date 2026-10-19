using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LiquidGlass.Studio.Shell;
using LiquidGlass.Wpf;

namespace LiquidGlass.Studio.Pages;

/// <summary>Materials lab: live controls for material, appearance, refraction, tint and radius, with a draggable lens.</summary>
public partial class MaterialsPage : StudioPageBase
{
    private readonly Dictionary<string, RadioButton> _wallpaperChips = new();
    private string? _wallpaper;
    private bool _showing;
    private Point? _grab;
    private Point _anchor = new(0.5, 0.5);

    public MaterialsPage()
    {
        InitializeComponent();
        // Wallpaper thumbnails are a radio group: the chosen one carries a ring, and keyboard focus a wider one.
        var focusVisual = ChipFocusVisual();
        foreach (var name in Wallpapers.All)
        {
            var chip = new RadioButton
            {
                Width = 44,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                Cursor = Cursors.Hand,
                GroupName = "Wallpaper",
                Tag = name,
                Template = ChipTemplate(name),
                FocusVisualStyle = focusVisual,
            };
            AutomationProperties.SetName(chip, CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name));
            chip.Checked += (_, _) =>
            {
                if (!_showing) Show((string)chip.Tag);
            };
            _wallpaperChips[name] = chip;
            WallpaperChips.Children.Add(chip);
        }
        if (Tints.Children[0] is Grid { Children.Count: > 1 } first) MarkTint(first.Children[1]);
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
        // Midnight's deep lower part keeps the panel's white labels legible in dark appearance.
        if (_wallpaper is null) Show(dark ? Wallpapers.Midnight : Wallpapers.Ocean, remember: false);
        Wallpaper.VerticalAlignment = Wallpapers.Alignment(dark);
    }

    private void Show(string name, bool remember = true)
    {
        if (remember) _wallpaper = name;
        Wallpaper.Source = Wallpapers.Get(name);
        if (!_wallpaperChips.TryGetValue(name, out var chip)) return;
        _showing = true;
        try
        {
            chip.IsChecked = true;
        }
        finally
        {
            _showing = false;
        }
    }

    private void UpdateRadius()
    {
        // The top of the range is a capsule: radii that large blend into it anyway.
        var capsule = RadiusSlider.Value >= RadiusSlider.Maximum - 0.5;
        Lens.Radius = capsule ? GlassRadius.Capsule : GlassRadius.FromValue(RadiusSlider.Value);
        RadiusLabel.Text = capsule ? "capsule" : RadiusSlider.Value.ToString("N0", CultureInfo.CurrentCulture);
    }

    private void OnTint(object sender, RoutedEventArgs e)
    {
        var tag = (string)((FrameworkElement)sender).Tag;
        Lens.Tint = string.IsNullOrEmpty(tag) ? null : (Color)ColorConverter.ConvertFromString(tag);
        MarkTint((UIElement)sender);
    }

    /// <summary>Rings the chosen tint chip and tells screen readers which one is selected.</summary>
    private void MarkTint(UIElement chosen)
    {
        foreach (var slot in Tints.Children)
        {
            if (slot is not Grid { Children.Count: > 1 } grid || grid.Children[0] is not Ellipse ring) continue;
            var chip = grid.Children[1];
            var selected = ReferenceEquals(chip, chosen);
            ring.Visibility = selected ? Visibility.Visible : Visibility.Hidden;
            AutomationProperties.SetItemStatus(chip, selected ? "Selected" : string.Empty);
        }
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

    /// <summary>A rounded thumbnail of a wallpaper, ringed while it is the chosen one.</summary>
    private static ControlTemplate ChipTemplate(string wallpaper)
    {
        var template = new ControlTemplate(typeof(RadioButton));
        var root = new FrameworkElementFactory(typeof(Grid));
        var ring = new FrameworkElementFactory(typeof(Border), "Selected");
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(11));
        ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-3));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ring.SetResourceReference(Border.BorderBrushProperty, "Studio.Foreground");
        ring.SetValue(UIElement.VisibilityProperty, Visibility.Hidden);
        var image = new FrameworkElementFactory(typeof(Border));
        image.SetValue(Border.CornerRadiusProperty, new CornerRadius(8));
        image.SetValue(Border.BorderThicknessProperty, new Thickness(1));
        image.SetValue(Border.BorderBrushProperty, new SolidColorBrush(Color.FromArgb(0x55, 0xFF, 0xFF, 0xFF)));
        // The 2048-pixel wallpaper shrinks about 28 times into the chip: filtered at high quality, its thin rim lines stay
        // continuous instead of breaking into specks.
        var thumbnail = new ImageBrush(Wallpapers.Get(wallpaper)) { Stretch = Stretch.UniformToFill, Viewbox = new Rect(0.2, 0.25, 0.6, 0.5) };
        RenderOptions.SetBitmapScalingMode(thumbnail, BitmapScalingMode.HighQuality);
        thumbnail.Freeze();
        image.SetValue(Border.BackgroundProperty, thumbnail);
        root.AppendChild(ring);
        root.AppendChild(image);
        template.VisualTree = root;
        var selected = new Trigger { Property = ToggleButton.IsCheckedProperty, Value = true };
        selected.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "Selected"));
        template.Triggers.Add(selected);
        return template;
    }

    /// <summary>The keyboard focus ring of a wallpaper chip: rounded like the thumbnail, outside its selection ring.</summary>
    private static Style ChipFocusVisual()
    {
        var template = new ControlTemplate();
        var ring = new FrameworkElementFactory(typeof(Border));
        ring.SetValue(Border.CornerRadiusProperty, new CornerRadius(14));
        ring.SetValue(FrameworkElement.MarginProperty, new Thickness(-6));
        ring.SetValue(Border.BorderThicknessProperty, new Thickness(2));
        ring.SetResourceReference(Border.BorderBrushProperty, "Studio.Foreground");
        template.VisualTree = ring;
        var style = new Style(typeof(Control));
        style.Setters.Add(new Setter(Control.TemplateProperty, template));
        return style;
    }

    private static double Clamp(double v, double lo, double hi) => hi < lo ? lo : v < lo ? lo : v > hi ? hi : v;
}
