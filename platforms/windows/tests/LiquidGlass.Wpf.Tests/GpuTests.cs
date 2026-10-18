using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using LiquidGlass.Wpf.Rendering;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Xunit;

namespace LiquidGlass.Wpf.Tests;

/// <summary>The HLSL library on a GPU (Direct3D 11 WARP), against the golden vectors.</summary>
public class GpuTests
{
    [Fact]
    public void ShaderLibraryMatchesTheVectorsOnTheGpu()
    {
        // Every glass pixel case of spec/vectors.json (menu panels with their legibility backing among them), evaluated
        // by the HLSL library on Direct3D 11 (WARP), within the GPU tolerance of OPTICS.md; the same run checks the
        // shape field the shader computes in float32.
        var pixelCases = Vectors.Root.GetProperty("glassPixel").GetProperty("cases").EnumerateArray().ToArray();
        Assert.Contains(pixelCases, c => c.TryGetProperty("legibility", out var amount) && amount.GetDouble() > 0 && c.Flag("dark"));
        Assert.Contains(pixelCases, c => c.TryGetProperty("legibility", out var amount) && amount.GetDouble() > 0 && !c.Flag("dark"));
        var fieldCases = Vectors.Root.GetProperty("shapeField").EnumerateArray().ToArray();
        var input = new List<float>();
        foreach (var c in pixelCases)
        {
            var tint = c.TryGetProperty("tint", out var t) ? t.EnumerateArray().Select(v => (float)v.GetDouble()).ToArray() : null;
            var legibility = c.TryGetProperty("legibility", out var l) ? (float)l.GetDouble() : 0f;
            input.AddRange(new[]
            {
                (float)c.Number("x"), (float)c.Number("y"), (float)c.Number("width"), (float)c.Number("height"),
                (float)c.ShaderRadius(), c.GetProperty("material").GetString() == "regular" ? 1f : 0f, c.Flag("dark") ? 1f : 0f, (float)c.Number("refraction"),
                tint?[0] ?? 0, tint?[1] ?? 0, tint?[2] ?? 0, tint is null ? 0 : 1,
                1f / 3, legibility, 0, 0,
            });
        }
        foreach (var c in fieldCases)
        {
            input.AddRange(new[]
            {
                (float)c.Number("x"), (float)c.Number("y"), (float)c.Number("width"), (float)c.Number("height"),
                (float)c.ShaderRadius(), 0, 0, 60, 0, 0, 0, 0, 1f / 3, 0, 0, 0,
            });
        }
        var results = RunOnWarp(input.ToArray(), pixelCases.Length + fieldCases.Length);
        for (var i = 0; i < pixelCases.Length; i++)
        {
            var expected = pixelCases[i].GetProperty("value").EnumerateArray().Select(v => v.GetDouble()).ToArray();
            for (var k = 0; k < 4; k++)
                Assert.True(Math.Abs(expected[k] - results[i * 8 + k]) <= Vectors.GpuTolerance,
                    $"glass pixel #{i} channel {k}: expected {expected[k]}, GPU {results[i * 8 + k]}");
        }
        for (var j = 0; j < fieldCases.Length; j++)
        {
            var i = pixelCases.Length + j;
            var c = fieldCases[j];
            Assert.True(Math.Abs(c.Number("distance") - results[i * 8 + 4]) <= Vectors.GpuTolerance * Math.Max(1, Math.Abs(c.Number("distance"))), $"field #{j} distance");
            Assert.True(Math.Abs(c.Number("nx") - results[i * 8 + 5]) <= Vectors.GpuTolerance, $"field #{j} nx");
            Assert.True(Math.Abs(c.Number("ny") - results[i * 8 + 6]) <= Vectors.GpuTolerance, $"field #{j} ny");
        }
    }

    private static float[] RunOnWarp(float[] input, int count)
    {
        var directory = Path.Combine(AppContext.BaseDirectory, "Shaders");
        var source = File.ReadAllText(Path.Combine(directory, "GpuCheck.hlsl"))
            .Replace("#include \"LiquidGlass.hlsli\"", File.ReadAllText(Path.Combine(directory, "LiquidGlass.hlsli")));
        var bytecode = D3DShaderCompiler.Compile(source, "GpuCheck.hlsl", "main", "cs_5_0");

        D3D11.D3D11CreateDevice(null, DriverType.Warp, DeviceCreationFlags.None, new[] { FeatureLevel.Level_11_0 }, out ID3D11Device device, out ID3D11DeviceContext context).CheckError();
        using (device)
        using (context)
        using (var shader = device.CreateComputeShader(bytecode))
        using (var cases = device.CreateBuffer(input, BindFlags.ShaderResource, ResourceUsage.Default, CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, structureByteStride: 64))
        using (var counts = device.CreateBuffer(new[] { (uint)count, 0u, 0u, 0u }, BindFlags.ConstantBuffer))
        using (var output = device.CreateBuffer(new BufferDescription((uint)(count * 32), BindFlags.UnorderedAccess, ResourceUsage.Default, CpuAccessFlags.None, ResourceOptionFlags.BufferStructured, 16)))
        using (var staging = device.CreateBuffer(new BufferDescription((uint)(count * 32), BindFlags.None, ResourceUsage.Staging, CpuAccessFlags.Read, ResourceOptionFlags.None, 0)))
        using (var casesView = device.CreateShaderResourceView(cases, new ShaderResourceViewDescription(cases, Format.Unknown, 0, (uint)count)))
        using (var outputView = device.CreateUnorderedAccessView(output, new UnorderedAccessViewDescription(output, Format.Unknown, 0, (uint)(count * 2))))
        {
            context.CSSetShader(shader);
            context.CSSetShaderResource(0, casesView);
            context.CSSetUnorderedAccessView(0, outputView);
            context.CSSetConstantBuffer(0, counts);
            context.Dispatch((uint)((count + 63) / 64), 1, 1);
            context.CopyResource(staging, output);
            var mapped = context.Map(staging, 0, MapMode.Read, Vortice.Direct3D11.MapFlags.None);
            try
            {
                var results = new float[count * 8];
                Marshal.Copy(mapped.DataPointer, results, 0, results.Length);
                return results;
            }
            finally
            {
                context.Unmap(staging, 0);
            }
        }
    }
}
