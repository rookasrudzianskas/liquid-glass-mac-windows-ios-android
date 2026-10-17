using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Media.Effects;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Loads the glass pixel shader once per process: the embedded fxc bytecode when it was compiled from exactly the
/// embedded sources (their SHA-256 is stored next to it), otherwise the sources compiled with D3DCompile.
/// </summary>
internal static class GlassShaderLibrary
{
    private const string ResourcePrefix = "LiquidGlass.Wpf.Shaders.";
    private const string SurfaceShader = "GlassSurface.fx";
    private const string Library = "LiquidGlass.hlsli";
    private const string IncludeDirective = "#include \"" + Library + "\"";

    private static readonly Lazy<LoadedShader> Surface = new(LoadSurface);

    // WPF keeps InvalidPixelShaderEncountered per UI thread (on its media context), so each thread subscribes once.
    [ThreadStatic] private static bool _handlerAdded;
    private static volatile bool _rejected;

    /// <summary>The frozen surface shader, or null when it is unavailable.</summary>
    public static PixelShader? SurfacePixelShader => Surface.Value.Shader;

    /// <summary>Where the surface shader came from.</summary>
    public static GlassShaderSource SurfaceSource => Surface.Value.Source;

    /// <summary>A one-line description of how the shader was loaded, or why it could not be.</summary>
    public static string SurfaceStatus => Surface.Value.Status;

    /// <summary>
    /// True once the graphics driver refused to create a pixel shader on any UI thread of the process. WPF does not
    /// say which shader failed, so glass assumes its own and draws the fallback material from then on.
    /// </summary>
    public static bool ShaderRejected => _rejected;

    /// <summary>
    /// Subscribes to <see cref="PixelShader.InvalidPixelShaderEncountered"/> on the calling UI thread, once. Without a
    /// handler WPF throws on the UI thread when a driver rejects a shader (after drawing the effect as a pass-through,
    /// which for glass would be nothing); with it, every lens switches to the fallback material.
    /// </summary>
    public static void EnsureBadShaderHandler()
    {
        if (_handlerAdded) return;
        _handlerAdded = true;
        PixelShader.InvalidPixelShaderEncountered += (_, _) => MarkShaderRejected();
    }

    /// <summary>Records that the shader cannot run here and tells every lens to draw the fallback material.</summary>
    internal static void MarkShaderRejected()
    {
        if (_rejected) return;
        _rejected = true;
        GlassSystemSettings.NotifyChanged();
    }

    /// <summary>Forgets a rejection (tests only).</summary>
    internal static void ResetShaderRejected()
    {
        if (!_rejected) return;
        _rejected = false;
        GlassSystemSettings.NotifyChanged();
    }

    /// <summary>SHA-256 (hex) of the embedded surface shader sources, as fxc's output records it.</summary>
    public static string SurfaceSourceHash() => Hash(ReadResource(SurfaceShader), ReadResource(Library));

    /// <summary>The surface shader source with its include expanded, ready for D3DCompile.</summary>
    public static string ExpandedSurfaceSource()
    {
        var main = Encoding.UTF8.GetString(ReadResource(SurfaceShader));
        var library = Encoding.UTF8.GetString(ReadResource(Library));
        if (!main.Contains(IncludeDirective)) throw new InvalidOperationException($"{SurfaceShader} must include {Library}.");
        return main.Replace(IncludeDirective, library);
    }

    /// <summary>Validates that <paramref name="bytecode"/> is a pixel shader 3.0 program.</summary>
    public static bool IsPixelShader30(byte[] bytecode)
        => bytecode.Length >= 8 && BitConverter.ToUInt32(bytecode, 0) == 0xFFFF0300;

    private static LoadedShader LoadSurface()
    {
        string hash;
        try
        {
            hash = SurfaceSourceHash();
        }
        catch (Exception e) when (e is IOException or InvalidOperationException)
        {
            return LoadedShader.Unavailable("the shader sources are missing from the assembly: " + e.Message);
        }

        var precompiled = TryReadResource("Compiled.GlassSurface.ps");
        var recorded = TryReadResource("Compiled.GlassSurface.sha256");
        if (precompiled is not null && recorded is not null && IsPixelShader30(precompiled)
            && string.Equals(Encoding.ASCII.GetString(recorded).Trim(), hash, StringComparison.OrdinalIgnoreCase))
            return LoadedShader.From(precompiled, GlassShaderSource.Precompiled, "precompiled by fxc");

        var reason = precompiled is null ? "no precompiled bytecode" : "precompiled bytecode is stale";
        try
        {
            var bytecode = D3DShaderCompiler.Compile(ExpandedSurfaceSource(), SurfaceShader, "main", "ps_3_0");
            if (!IsPixelShader30(bytecode)) return LoadedShader.Unavailable("D3DCompile did not produce a ps_3_0 program");
            return LoadedShader.From(bytecode, GlassShaderSource.CompiledAtRuntime, $"compiled at run time with D3DCompile ({reason})");
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException
                                      or ShaderCompilationException or InvalidOperationException or System.Runtime.InteropServices.SEHException)
        {
            return LoadedShader.Unavailable($"{reason}, and D3DCompile failed: {e.Message}");
        }
    }

    private static string Hash(byte[] main, byte[] library)
    {
        using var sha = SHA256.Create();
        var combined = new byte[main.Length + library.Length];
        Buffer.BlockCopy(main, 0, combined, 0, main.Length);
        Buffer.BlockCopy(library, 0, combined, main.Length, library.Length);
        var digest = sha.ComputeHash(combined);
        var text = new StringBuilder(digest.Length * 2);
        foreach (var b in digest) text.Append(b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        return text.ToString();
    }

    private static byte[] ReadResource(string name)
        => TryReadResource(name) ?? throw new IOException($"Embedded resource {ResourcePrefix}{name} is missing.");

    private static byte[]? TryReadResource(string name)
    {
        using var stream = typeof(GlassShaderLibrary).Assembly.GetManifestResourceStream(ResourcePrefix + name);
        if (stream is null) return null;
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private sealed class LoadedShader
    {
        private LoadedShader(PixelShader? shader, GlassShaderSource source, string status)
        {
            Shader = shader;
            Source = source;
            Status = status;
        }

        public PixelShader? Shader { get; }

        public GlassShaderSource Source { get; }

        public string Status { get; }

        public static LoadedShader From(byte[] bytecode, GlassShaderSource source, string status)
        {
            var shader = new PixelShader();
            using (var stream = new MemoryStream(bytecode, false)) shader.SetStreamSource(stream);
            shader.Freeze();
            return new LoadedShader(shader, source, status);
        }

        public static LoadedShader Unavailable(string status) => new(null, GlassShaderSource.Unavailable, status);
    }
}
