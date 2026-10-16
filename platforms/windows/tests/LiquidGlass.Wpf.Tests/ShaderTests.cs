using System;
using LiquidGlass.Wpf.Rendering;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

public class ShaderTests
{
    [Fact]
    public void PrecompiledBytecodeMatchesTheSources()
    {
        // The library embeds fxc's bytecode with the SHA-256 of the sources it came from; a stale copy would
        // silently fall back to compiling at run time, so keep it current.
        Assert.Equal(GlassShaderSource.Precompiled, GlassDiagnostics.ShaderSource);
        Assert.Contains("precompiled", GlassDiagnostics.ShaderStatus, StringComparison.Ordinal);
    }

    [Fact]
    public void D3DCompileBuildsThePixelShaderAtRunTime()
    {
        var bytecode = D3DShaderCompiler.Compile(GlassShaderLibrary.ExpandedSurfaceSource(), "GlassSurface.fx", "main", "ps_3_0");
        Assert.True(GlassShaderLibrary.IsPixelShader30(bytecode));
        Assert.True(bytecode.Length > 1024);
    }

    [Fact]
    public void D3DCompileReportsErrors()
    {
        var error = Assert.Throws<ShaderCompilationException>(() => D3DShaderCompiler.Compile("float4 main() : COLOR { return undefined; }", "Broken.fx", "main", "ps_3_0"));
        Assert.Contains("undefined", error.Message, StringComparison.Ordinal);
    }
}
