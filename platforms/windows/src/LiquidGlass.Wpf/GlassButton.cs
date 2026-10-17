using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>
/// A button made of interactive glass: it grows under a press, stretches toward a drag, settles with overshoot and
/// lights up under the pointer. Text buttons are capsules; icon-only buttons are circles. Heights are 24, 32 and 40.
/// </summary>
/// <example>
/// <code language="xml"><![CDATA[
/// <lg:GlassButton Content="Share" Click="OnShare" />
/// <lg:GlassButton Size="Large" Tint="#0A84FF" Content="Continue" />
/// <lg:GlassButton IsIconOnly="True" AutomationProperties.Name="Favorite" Content="&#xE734;" FontFamily="Segoe Fluent Icons, Segoe MDL2 Assets" />
/// ]]></code>
/// </example>
[TemplatePart(Name = SurfacePart, Type = typeof(GlassSurface))]
public class GlassButton : Button
{
    /// <summary>Material of the button's glass (inherited).</summary>
    public static readonly DependencyProperty MaterialProperty = GlassSurface.MaterialProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(GlassMaterial.Clear, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Appearance of the button's glass (inherited).</summary>
    public static readonly DependencyProperty AppearanceProperty = GlassSurface.AppearanceProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(GlassAppearance.Automatic, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Motion of the button: <see cref="GlassMotion.Reduced"/> keeps transitions without grow, stretch or overshoot; <see cref="GlassMotion.None"/> removes them (inherited). The system's animation setting always applies.</summary>
    public static readonly DependencyProperty MotionProperty = GlassSurface.MotionProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(GlassMotion.Full, FrameworkPropertyMetadataOptions.Inherits));

    /// <summary>Corner radius. Default <c>capsule</c> (icon-only buttons default to <c>circle</c>).</summary>
    public static readonly DependencyProperty RadiusProperty = GlassSurface.RadiusProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(GlassRadius.Capsule));

    /// <summary>Optical travel at the outline. Default NaN: min(60, 0.75 × the shorter side), 24 for a 32-unit button.</summary>
    public static readonly DependencyProperty RefractionProperty = GlassSurface.RefractionProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(double.NaN));

    /// <summary>Colors the glass, or null.</summary>
    public static readonly DependencyProperty TintProperty = GlassSurface.TintProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(null, OnTintChanged));

    /// <summary>Press, drag and glow response. Default true.</summary>
    public static readonly DependencyProperty IsInteractiveProperty = GlassSurface.IsInteractiveProperty.AddOwner(typeof(GlassButton),
        new FrameworkPropertyMetadata(true));

    /// <summary>Standard height: 24, 32 (default) or 40 units.</summary>
    public static readonly DependencyProperty SizeProperty = DependencyProperty.Register(
        nameof(Size), typeof(GlassButtonSize), typeof(GlassButton), new FrameworkPropertyMetadata(GlassButtonSize.Regular));

    /// <summary>A square button with a circular outline, for an icon without a label. Give it an automation name.</summary>
    public static readonly DependencyProperty IsIconOnlyProperty = DependencyProperty.Register(
        nameof(IsIconOnly), typeof(bool), typeof(GlassButton), new FrameworkPropertyMetadata(false));

    private const string SurfacePart = "PART_Surface";

    private readonly GlassAppearanceTracker _appearance;

    static GlassButton()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(GlassButton), new FrameworkPropertyMetadata(typeof(GlassButton)));
        GlassScene.AddContentForegroundOwner(typeof(GlassButton));
    }

    /// <summary>Creates a button.</summary>
    public GlassButton()
    {
        _appearance = new GlassAppearanceTracker(this, () => Tint);
        GlassFocus.Track(this);
    }

    /// <summary>Material of the button's glass.</summary>
    public GlassMaterial Material
    {
        get => (GlassMaterial)GetValue(MaterialProperty);
        set => SetValue(MaterialProperty, value);
    }

    /// <summary>Appearance of the button's glass.</summary>
    public GlassAppearance Appearance
    {
        get => (GlassAppearance)GetValue(AppearanceProperty);
        set => SetValue(AppearanceProperty, value);
    }

    /// <summary>Motion of the button.</summary>
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

    /// <summary>Optical travel at the outline.</summary>
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

    /// <summary>Press, drag and glow response.</summary>
    public bool IsInteractive
    {
        get => (bool)GetValue(IsInteractiveProperty);
        set => SetValue(IsInteractiveProperty, value);
    }

    /// <summary>Standard height.</summary>
    public GlassButtonSize Size
    {
        get => (GlassButtonSize)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    /// <summary>A circular icon button.</summary>
    public bool IsIconOnly
    {
        get => (bool)GetValue(IsIconOnlyProperty);
        set => SetValue(IsIconOnlyProperty, value);
    }

    /// <inheritdoc />
    public override void OnApplyTemplate()
    {
        base.OnApplyTemplate();
        if (GetTemplateChild(SurfacePart) is GlassSurface surface) surface.InputSource = this;
    }

    /// <inheritdoc />
    /// <remarks>The trigger of a <see cref="GlassMenu"/> also reports the menu's ExpandCollapse state.</remarks>
    protected override AutomationPeer OnCreateAutomationPeer()
        => TemplatedParent is GlassMenu menu ? new GlassMenuTriggerAutomationPeer(this, menu) : base.OnCreateAutomationPeer();

    /// <inheritdoc />
    protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
    {
        base.OnPropertyChanged(e);
        if (GlassAppearanceTracker.Affects(e.Property)) _appearance.Update();
    }

    private static void OnTintChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((GlassButton)d)._appearance.Update();
}
