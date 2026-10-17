using System;
using System.Windows.Media;
using LiquidGlass.Wpf.Rendering;

namespace LiquidGlass.Wpf;

/// <summary>How glass renders on this machine, for about boxes, logs and support.</summary>
public static class GlassDiagnostics
{
    /// <summary>Where the glass pixel shader came from.</summary>
    public static GlassShaderSource ShaderSource => GlassShaderLibrary.SurfaceSource;

    /// <summary>A one-line description of how the shader was loaded, or why it could not be.</summary>
    public static string ShaderStatus => GlassShaderLibrary.SurfaceStatus;

    /// <summary>Whether glass in <paramref name="visual"/>'s window can use the pixel shader.</summary>
    public static bool CanUseShader(Visual visual) => GlassRenderCapability.CanUseShader(visual ?? throw new ArgumentNullException(nameof(visual)));

    /// <summary>A summary of the rendering tier, pixel shader support and shader status.</summary>
    public static string Describe()
        => $"tier {RenderCapability.Tier >> 16}, ps_3_0 {(RenderCapability.IsPixelShaderVersionSupported(3, 0) ? "supported" : "unsupported")} " +
           $"({RenderCapability.MaxPixelShaderInstructionSlots(3, 0)} slots), shader {ShaderSource}: {ShaderStatus}" +
           (GlassShaderLibrary.ShaderRejected ? "; rejected by the graphics driver, so glass draws the fallback material" : "");
}
