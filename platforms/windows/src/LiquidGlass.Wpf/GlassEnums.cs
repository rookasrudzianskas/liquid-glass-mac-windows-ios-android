namespace LiquidGlass.Wpf;

/// <summary>Light or dark glass.</summary>
public enum GlassAppearance
{
    /// <summary>Follows the Windows app theme (Settings › Personalization › Colors › Choose your mode).</summary>
    Automatic,

    /// <summary>Light glass with dark foreground.</summary>
    Light,

    /// <summary>Dark glass with light foreground.</summary>
    Dark,
}

/// <summary>How glass is drawn.</summary>
public enum GlassRenderMode
{
    /// <summary>
    /// The pixel shader where it can run (pixel shader 3.0 in hardware, render tier 2, a window that does not render in
    /// software, a shader the driver accepted), otherwise the fallback material.
    /// </summary>
    Automatic,

    /// <summary>
    /// The same as <see cref="Automatic"/>: the pixel shader wherever it can run, otherwise the fallback material. Glass
    /// never forces the shader where it cannot run, because there it would draw nothing; use this value to state the
    /// intent in a style or to undo an inherited <see cref="Fallback"/>.
    /// </summary>
    Shader,

    /// <summary>Always draw the fallback material: platform blur, a fitted veil and the exact rim light.</summary>
    Fallback,
}

/// <summary>Standard glass button sizes (heights 24, 32 and 40 on Windows).</summary>
public enum GlassButtonSize
{
    /// <summary>24 units tall.</summary>
    Small,

    /// <summary>32 units tall.</summary>
    Regular,

    /// <summary>40 units tall.</summary>
    Large,
}

/// <summary>An edge of a region.</summary>
public enum GlassEdge
{
    /// <summary>The top edge.</summary>
    Top,

    /// <summary>The bottom edge.</summary>
    Bottom,

    /// <summary>The left edge.</summary>
    Left,

    /// <summary>The right edge.</summary>
    Right,
}

/// <summary>Where a menu opens relative to its trigger.</summary>
public enum GlassMenuPlacement
{
    /// <summary>Below the trigger, or above it when there is no room below.</summary>
    Bottom,

    /// <summary>Above the trigger, or below it when there is no room above.</summary>
    Top,
}

/// <summary>How a toolbar spacer sizes itself.</summary>
public enum GlassSpacerSizing
{
    /// <summary>Fills the available space, pushing the clusters apart.</summary>
    Flexible,

    /// <summary>A standard gap between two clusters.</summary>
    Fixed,
}

/// <summary>Where the glass pixel shader's bytecode came from.</summary>
public enum GlassShaderSource
{
    /// <summary>Bytecode compiled by fxc at build time and embedded in the assembly.</summary>
    Precompiled,

    /// <summary>Compiled from the embedded HLSL with D3DCompile when the library loaded.</summary>
    CompiledAtRuntime,

    /// <summary>Neither was possible; glass draws the fallback material.</summary>
    Unavailable,
}
