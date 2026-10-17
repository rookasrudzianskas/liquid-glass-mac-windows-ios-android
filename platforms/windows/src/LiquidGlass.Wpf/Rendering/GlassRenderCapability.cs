using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>Decides whether the glass pixel shader can run for a visual.</summary>
internal static class GlassRenderCapability
{
    /// <summary>
    /// Instruction slots the surface shader needs (fxc reports about 600). Every DirectX 10-class GPU and WARP offer
    /// 32768 for pixel shader 3.0; the minimum the standard guarantees is 512.
    /// </summary>
    public const int RequiredInstructionSlots = 1024;

    /// <summary>
    /// True when pixel shader 3.0 runs in hardware for <paramref name="visual"/>: a compiled shader exists, the GPU
    /// supports it, the driver has not rejected it, and neither the process nor the visual's window renders in software.
    /// </summary>
    public static bool CanUseShader(Visual visual)
    {
        if (GlassShaderLibrary.SurfacePixelShader is null || GlassShaderLibrary.ShaderRejected) return false;
        if (RenderOptions.ProcessRenderMode == RenderMode.SoftwareOnly) return false;
        if (RenderCapability.Tier >> 16 < 2) return false;
        if (!RenderCapability.IsPixelShaderVersionSupported(3, 0)) return false;
        if (RenderCapability.MaxPixelShaderInstructionSlots(3, 0) < RequiredInstructionSlots) return false;
        if (PresentationSource.FromVisual(visual) is HwndSource { CompositionTarget: { } target } && target.RenderMode == RenderMode.SoftwareOnly)
            return false;
        return true;
    }
}
