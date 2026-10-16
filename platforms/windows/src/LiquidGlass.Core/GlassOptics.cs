namespace LiquidGlass;

/// <summary>Constants of the optical model (<c>spec/OPTICS.md</c>).</summary>
public static class GlassOptics
{
    /// <summary>Depth of the refracting bevel inside the outline, in layout units.</summary>
    public const double Bevel = 20;

    /// <summary>Default optical travel at the outline, in layout units.</summary>
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
