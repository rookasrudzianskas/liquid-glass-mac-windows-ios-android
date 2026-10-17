using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using LiquidGlass.Wpf.Rendering;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

public class ShaderTests
{
    [Fact]
    public void PrecompiledBytecodeMatchesTheSources()
    {
        // The library embeds fxc's bytecode with the SHA-256 of the sources it came from; a stale copy would
        // fall back to compiling at run time, so keep it current.
        Assert.Equal(GlassShaderSource.Precompiled, GlassDiagnostics.ShaderSource);
        Assert.Contains("precompiled", GlassDiagnostics.ShaderStatus, StringComparison.Ordinal);
    }

    [Fact]
    public async Task D3DCompileBuildsThePixelShaderInTheBackground()
    {
        // What the library does when the precompiled bytecode is stale: D3DCompile takes tens of seconds for this
        // shader, so it must not hold up the thread that first shows glass.
        var changed = 0;
        var watch = Stopwatch.StartNew();
        var load = GlassShaderLibrary.SurfaceLoad.Start(usePrecompiled: false, () => Interlocked.Increment(ref changed));
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5), $"starting the load took {watch.Elapsed}");
        Assert.Equal(GlassShaderSource.CompiledAtRuntime, load.Source);
        if (!load.Ready.IsCompleted) Assert.Contains("draws the fallback material until it is ready", load.Status, StringComparison.Ordinal);

        Assert.Same(load.Ready, await Task.WhenAny(load.Ready, Task.Delay(TimeSpan.FromMinutes(4))));
        // The change notification follows the result, on the compiling thread.
        for (var i = 0; i < 100 && Volatile.Read(ref changed) == 0; i++) Thread.Sleep(20);
        Assert.Equal(1, Volatile.Read(ref changed));
        Assert.Equal(GlassShaderSource.CompiledAtRuntime, load.Source);
        Assert.Contains("compiled at run time with D3DCompile", load.Status, StringComparison.Ordinal);
        var bytecode = load.Bytecode;
        Assert.NotNull(bytecode);
        Assert.True(GlassShaderLibrary.IsPixelShader30(bytecode!));
        Assert.True(bytecode!.Length > 1024);
        // The shader itself is made on a UI thread and frozen, so every UI thread can use it.
        Sta.Run(() =>
        {
            var shader = load.Shader;
            Assert.NotNull(shader);
            Assert.True(shader!.IsFrozen);
        });
    }

    [Fact]
    public void D3DCompileReportsErrors()
    {
        var error = Assert.Throws<ShaderCompilationException>(() => D3DShaderCompiler.Compile("float4 main() : COLOR { return undefined; }", "Broken.fx", "main", "ps_3_0"));
        Assert.Contains("undefined", error.Message, StringComparison.Ordinal);
    }
}
