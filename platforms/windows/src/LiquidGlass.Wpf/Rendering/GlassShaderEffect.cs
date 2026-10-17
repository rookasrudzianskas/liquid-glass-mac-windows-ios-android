using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Media3D;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// The glass pixel shader (<c>Shaders/GlassSurface.fx</c>) as a WPF effect. Each property maps to one float4
/// constant register; WPF supplies the derivatives of the texture coordinates in c7, from which the shader measures
/// one device pixel in layout units.
/// </summary>
internal sealed class GlassShaderEffect : ShaderEffect
{
    public static readonly DependencyProperty InputProperty =
        RegisterPixelShaderSamplerProperty(nameof(Input), typeof(GlassShaderEffect), 0, SamplingMode.Bilinear);

    /// <summary>Lens size (x, y) and surface size (z, w), in layout units.</summary>
    public static readonly DependencyProperty LensGeometryProperty = Constant(nameof(LensGeometry), 0);

    /// <summary>Corner extent, corner exponent, refraction.</summary>
    public static readonly DependencyProperty ShapeOpticsProperty = Constant(nameof(ShapeOptics), 1);

    /// <summary>Regular (0/1), dark (0/1), nested (0/1), increased contrast (0/1).</summary>
    public static readonly DependencyProperty MaterialOpticsProperty = Constant(nameof(MaterialOptics), 2);

    /// <summary>Tint color and whether it is on.</summary>
    public static readonly DependencyProperty TintOpticsProperty = Constant(nameof(TintOptics), 3);

    /// <summary>Glow center (from the surface center), radius and amount.</summary>
    public static readonly DependencyProperty GlowOpticsProperty = Constant(nameof(GlowOptics), 4);

    /// <summary>Regular fill: 1-D weights of the taps at offsets 0..3.</summary>
    public static readonly DependencyProperty FillWeightsProperty = Constant(nameof(FillWeights), 5);

    /// <summary>Regular fill: weight of the taps at offset 4 and the tap spacing.</summary>
    public static readonly DependencyProperty FillOpticsProperty = Constant(nameof(FillOptics), 6);

    public GlassShaderEffect(PixelShader shader)
    {
        PixelShader = shader;
        DdxUvDdyUvRegisterIndex = 7;
        UpdateShaderValue(InputProperty);
        UpdateShaderValue(LensGeometryProperty);
        UpdateShaderValue(ShapeOpticsProperty);
        UpdateShaderValue(MaterialOpticsProperty);
        UpdateShaderValue(TintOpticsProperty);
        UpdateShaderValue(GlowOpticsProperty);
        UpdateShaderValue(FillWeightsProperty);
        UpdateShaderValue(FillOpticsProperty);
    }

    public Brush Input
    {
        get => (Brush)GetValue(InputProperty);
        set => SetValue(InputProperty, value);
    }

    public Point4D LensGeometry
    {
        get => (Point4D)GetValue(LensGeometryProperty);
        set => SetIfChanged(LensGeometryProperty, value);
    }

    public Point4D ShapeOptics
    {
        get => (Point4D)GetValue(ShapeOpticsProperty);
        set => SetIfChanged(ShapeOpticsProperty, value);
    }

    public Point4D MaterialOptics
    {
        get => (Point4D)GetValue(MaterialOpticsProperty);
        set => SetIfChanged(MaterialOpticsProperty, value);
    }

    public Point4D TintOptics
    {
        get => (Point4D)GetValue(TintOpticsProperty);
        set => SetIfChanged(TintOpticsProperty, value);
    }

    public Point4D GlowOptics
    {
        get => (Point4D)GetValue(GlowOpticsProperty);
        set => SetIfChanged(GlowOpticsProperty, value);
    }

    public Point4D FillWeights
    {
        get => (Point4D)GetValue(FillWeightsProperty);
        set => SetIfChanged(FillWeightsProperty, value);
    }

    public Point4D FillOptics
    {
        get => (Point4D)GetValue(FillOpticsProperty);
        set => SetIfChanged(FillOpticsProperty, value);
    }

    /// <summary>
    /// Lets go of the shared shader. WPF subscribes every effect to its shader's bytecode-changed event, so while an
    /// effect holds the process-wide shader, that shader (a static) keeps the effect, its input brush and the window
    /// the brush copies alive.
    /// </summary>
    public void Release() => PixelShader = null;

    protected override Freezable CreateInstanceCore() => new GlassShaderEffect(PixelShader);

    private void SetIfChanged(DependencyProperty property, Point4D value)
    {
        if (!((Point4D)GetValue(property)).Equals(value)) SetValue(property, value);
    }

    private static DependencyProperty Constant(string name, int register)
        => DependencyProperty.Register(name, typeof(Point4D), typeof(GlassShaderEffect),
            new UIPropertyMetadata(new Point4D(0, 0, 0, 0), PixelShaderConstantCallback(register)));
}
