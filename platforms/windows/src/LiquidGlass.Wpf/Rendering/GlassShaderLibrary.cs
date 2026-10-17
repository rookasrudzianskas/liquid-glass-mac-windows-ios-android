using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media.Effects;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Loads the glass pixel shader once per process: the embedded fxc bytecode when it was compiled from exactly the
/// embedded sources (their SHA-256 is stored next to it), otherwise the sources compiled with D3DCompile on a
/// background thread, while glass draws the fallback material.
/// </summary>
internal static class GlassShaderLibrary
{
    private const string ResourcePrefix = "LiquidGlass.Wpf.Shaders.";
    private const string SurfaceShader = "GlassSurface.fx";
    private const string Library = "LiquidGlass.hlsli";
    private const string IncludeDirective = "#include \"" + Library + "\"";

    private static readonly object Gate = new();
    private static SurfaceLoad? _surface;

    // WPF keeps InvalidPixelShaderEncountered per UI thread (on its media context), so each thread subscribes once.
    [ThreadStatic] private static bool _handlerAdded;
    private static volatile bool _rejected;

    /// <summary>The frozen surface shader, or null while it is unavailable or still compiling.</summary>
    public static PixelShader? SurfacePixelShader => Surface.Shader;

    /// <summary>Where the surface shader came from.</summary>
    public static GlassShaderSource SurfaceSource => Surface.Source;

    /// <summary>A one-line description of how the shader was loaded, or why it could not be.</summary>
    public static string SurfaceStatus => Surface.Status;

    /// <summary>
    /// True once the graphics driver refused to create a pixel shader on any UI thread of the process. WPF does not
    /// say which shader failed, so glass assumes its own and draws the fallback material from then on.
    /// </summary>
    public static bool ShaderRejected => _rejected;

    private static SurfaceLoad Surface
    {
        get
        {
            lock (Gate) return _surface ??= SurfaceLoad.Start(usePrecompiled: true, GlassSystemSettings.NotifyChanged);
        }
    }

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

    /// <summary>
    /// One load of the surface shader. The precompiled bytecode is ready at once; compiling the HLSL takes D3DCompile
    /// tens of seconds (the 9 × 9 fill unrolls), so it runs on a thread-pool thread and reports through
    /// <c>changed</c> when it is done. The <see cref="PixelShader"/> itself is created (and frozen) on the first UI
    /// thread that asks for it, never on the compiling thread, which has no dispatcher.
    /// </summary>
    internal sealed class SurfaceLoad
    {
        private readonly object _gate = new();
        private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private byte[]? _bytecode;
        private PixelShader? _shader;
        private GlassShaderSource _source;
        private string _status;

        private SurfaceLoad(GlassShaderSource source, string status)
        {
            _source = source;
            _status = status;
        }

        /// <summary>Completes once the shader is final: compiled, precompiled or unavailable.</summary>
        public Task Ready => _ready.Task;

        public GlassShaderSource Source
        {
            get
            {
                lock (_gate) return _source;
            }
        }

        public string Status
        {
            get
            {
                lock (_gate) return _status;
            }
        }

        /// <summary>The bytecode, once it is available.</summary>
        public byte[]? Bytecode
        {
            get
            {
                lock (_gate) return _bytecode;
            }
        }

        /// <summary>The frozen shader, created on first use; null until the bytecode is available.</summary>
        public PixelShader? Shader
        {
            get
            {
                lock (_gate)
                {
                    if (_shader is null && _bytecode is not null)
                    {
                        var shader = new PixelShader();
                        using (var stream = new MemoryStream(_bytecode, false)) shader.SetStreamSource(stream);
                        shader.Freeze();
                        _shader = shader;
                    }
                    return _shader;
                }
            }
        }

        /// <summary>
        /// Loads the precompiled bytecode when <paramref name="usePrecompiled"/> and it matches the sources; otherwise
        /// starts compiling them in the background and calls <paramref name="changed"/> (on that thread) when done.
        /// </summary>
        public static SurfaceLoad Start(bool usePrecompiled, Action? changed)
        {
            string hash;
            try
            {
                hash = SurfaceSourceHash();
            }
            catch (Exception e) when (e is IOException or InvalidOperationException)
            {
                return Final(null, GlassShaderSource.Unavailable, "the shader sources are missing from the assembly: " + e.Message);
            }

            var precompiled = TryReadResource("Compiled.GlassSurface.ps");
            var recorded = TryReadResource("Compiled.GlassSurface.sha256");
            if (usePrecompiled && precompiled is not null && recorded is not null && IsPixelShader30(precompiled)
                && string.Equals(Encoding.ASCII.GetString(recorded).Trim(), hash, StringComparison.OrdinalIgnoreCase))
                return Final(precompiled, GlassShaderSource.Precompiled, "precompiled by fxc");

            var reason = !usePrecompiled ? "precompiled bytecode not used" : precompiled is null ? "no precompiled bytecode" : "precompiled bytecode is stale";
            var load = new SurfaceLoad(GlassShaderSource.CompiledAtRuntime,
                $"compiling at run time with D3DCompile ({reason}); glass draws the fallback material until it is ready");
            Task.Run(() =>
            {
                load.Compile(reason);
                changed?.Invoke();
            });
            return load;
        }

        private static SurfaceLoad Final(byte[]? bytecode, GlassShaderSource source, string status)
        {
            var load = new SurfaceLoad(source, status) { _bytecode = bytecode };
            load._ready.TrySetResult(true);
            return load;
        }

        private void Compile(string reason)
        {
            byte[]? bytecode = null;
            GlassShaderSource source;
            string status;
            try
            {
                bytecode = D3DShaderCompiler.Compile(ExpandedSurfaceSource(), SurfaceShader, "main", "ps_3_0");
                if (IsPixelShader30(bytecode))
                {
                    source = GlassShaderSource.CompiledAtRuntime;
                    status = $"compiled at run time with D3DCompile ({reason})";
                }
                else
                {
                    bytecode = null;
                    source = GlassShaderSource.Unavailable;
                    status = $"{reason}, and D3DCompile did not produce a ps_3_0 program";
                }
            }
            catch (Exception e)
            {
                // Whatever went wrong (no d3dcompiler_47.dll, a compiler error), glass keeps the fallback material; an
                // exception escaping this thread-pool thread would end the process.
                bytecode = null;
                source = GlassShaderSource.Unavailable;
                status = $"{reason}, and D3DCompile failed: {e.Message}";
            }
            lock (_gate)
            {
                _bytecode = bytecode;
                _source = source;
                _status = status;
            }
            _ready.TrySetResult(true);
        }
    }
}
