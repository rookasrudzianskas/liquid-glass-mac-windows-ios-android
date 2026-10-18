namespace LiquidGlass;

/// <summary>Constants of the optical model.</summary>
/// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md">Liquid Glass optics specification</seealso>
public static class GlassOptics
{
    /// <summary>Depth of the refracting bevel inside the outline, in layout units.</summary>
    public const double Bevel = 20;

    /// <summary>
    /// The full optical travel at the outline, in layout units: what a surface that sets no refraction uses once it is
    /// at least 80 units on its shorter side (see <see cref="GlassRefraction.Default"/>).
    /// </summary>
    public const double DefaultRefraction = 60;

    /// <summary>Continuous corners reach this far along each edge, relative to the radius.</summary>
    public const double ContinuousExtent = 1.528;

    /// <summary>Superellipse exponent of a continuous corner (2 is a circular arc).</summary>
    public const double ContinuousExponent = 3.26;

    /// <summary>Red weight of the luma used by tint and saturation (SVG <c>feColorMatrix</c>).</summary>
    public const double LumaRed = 0.213;

    /// <summary>Green weight of the luma used by tint and saturation.</summary>
    public const double LumaGreen = 0.715;

    /// <summary>Blue weight of the luma used by tint and saturation.</summary>
    public const double LumaBlue = 0.072;

    /// <summary>
    /// How far an open menu panel mixes its glass toward the solid legibility backing
    /// (<see cref="GlassPalette.LegibilityBacking"/>), after the material color and tint. The amount applies as
    /// <c>MenuLegibility · morph</c>, where <c>morph</c> is the menu's open progress clamped to <c>[0, 1]</c> (0 at the
    /// trigger, 1 open, never more while the morph spring overshoots), so the glass leaves its trigger as the trigger's
    /// own glass. Open, the labels of an untinted menu (Regular or Clear, in either appearance) keep at least 5.2:1
    /// contrast over any backdrop in <see cref="GlassPalette.MenuForeground"/>, and those of a menu tinted with a system
    /// color 5.0:1, highlighted rows (<see cref="MenuHighlight"/>) included; the panel still refracts and frosts what is
    /// behind it, at 40% strength. Every other surface uses 0.
    /// </summary>
    /// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#legibility-backing-menus">Liquid Glass optics specification: Legibility backing</seealso>
    public const double MenuLegibility = 0.6;

    /// <summary>
    /// How far a highlighted menu row (pointer over it, keyboard focus or a press) mixes its label color
    /// (<see cref="GlassPalette.MenuForeground"/>) into the glass beneath it.
    /// </summary>
    /// <seealso href="https://github.com/rookasrudzianskas/liquid-glass-mac-windows-ios-android/blob/main/spec/OPTICS.md#legibility-backing-menus">Liquid Glass optics specification: Legibility backing</seealso>
    public const double MenuHighlight = 0.08;
}

/// <summary>The two glass materials.</summary>
public enum GlassMaterial
{
    /// <summary>Clear glass: the refracted backdrop with a light tone curve and veil.</summary>
    Clear,

    /// <summary>Regular (frosted) glass: the backdrop is blurred and toned before refraction.</summary>
    Regular,
}

/// <summary>How much a glass surface moves.</summary>
public enum GlassMotion
{
    /// <summary>Springs with grow, stretch and overshoot. The system's reduce-motion setting still applies.</summary>
    Full,

    /// <summary>No grow, stretch or overshoot: springs become critically damped crossfades.</summary>
    Reduced,

    /// <summary>No transitions: every change applies immediately.</summary>
    None,
}
