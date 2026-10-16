using System;
using System.Runtime.InteropServices;
using System.Text;

namespace LiquidGlass.Wpf.Rendering;

/// <summary>
/// Compiles HLSL with <c>D3DCompile</c> from <c>d3dcompiler_47.dll</c>, which ships with Windows 10 and 11.
/// The result and error messages come back as <c>ID3DBlob</c> COM objects, read through their vtables.
/// </summary>
internal static class D3DShaderCompiler
{
    private const uint OptimizationLevel3 = 1 << 15;

    // ID3DBlob : IUnknown { QueryInterface, AddRef, Release, GetBufferPointer, GetBufferSize }
    private const int GetBufferPointerSlot = 3;
    private const int GetBufferSizeSlot = 4;

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufferPointerMethod(IntPtr blob);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate UIntPtr GetBufferSizeMethod(IntPtr blob);

    /// <summary>Compiles <paramref name="source"/> and returns the bytecode.</summary>
    /// <exception cref="ShaderCompilationException">The compiler reported errors.</exception>
    /// <exception cref="DllNotFoundException">d3dcompiler_47.dll is not available.</exception>
    public static byte[] Compile(string source, string sourceName, string entryPoint, string profile)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var hr = D3DCompile(bytes, (UIntPtr)bytes.Length, sourceName, IntPtr.Zero, IntPtr.Zero, entryPoint, profile,
            OptimizationLevel3, 0, out var code, out var errors);
        try
        {
            var messages = errors == IntPtr.Zero ? string.Empty : Encoding.UTF8.GetString(ReadBlob(errors)).TrimEnd('\0', '\r', '\n');
            if (hr < 0 || code == IntPtr.Zero)
                throw new ShaderCompilationException($"D3DCompile failed (0x{hr:X8}): {messages}");
            return ReadBlob(code);
        }
        finally
        {
            if (code != IntPtr.Zero) Marshal.Release(code);
            if (errors != IntPtr.Zero) Marshal.Release(errors);
        }
    }

    private static byte[] ReadBlob(IntPtr blob)
    {
        var vtable = Marshal.ReadIntPtr(blob);
        var getPointer = Marshal.GetDelegateForFunctionPointer<GetBufferPointerMethod>(Marshal.ReadIntPtr(vtable, GetBufferPointerSlot * IntPtr.Size));
        var getSize = Marshal.GetDelegateForFunctionPointer<GetBufferSizeMethod>(Marshal.ReadIntPtr(vtable, GetBufferSizeSlot * IntPtr.Size));
        var pointer = getPointer(blob);
        var size = checked((int)getSize(blob).ToUInt64());
        var result = new byte[size];
        if (size > 0) Marshal.Copy(pointer, result, 0, size);
        return result;
    }

    [DllImport("d3dcompiler_47.dll", CharSet = CharSet.Ansi, ExactSpelling = true, BestFitMapping = false, ThrowOnUnmappableChar = true)]
    private static extern int D3DCompile(
        byte[] srcData,
        UIntPtr srcDataSize,
        string sourceName,
        IntPtr defines,
        IntPtr include,
        string entryPoint,
        string target,
        uint flags1,
        uint flags2,
        out IntPtr code,
        out IntPtr errorMessages);
}

/// <summary>The HLSL compiler rejected a shader.</summary>
internal sealed class ShaderCompilationException : Exception
{
    public ShaderCompilationException(string message) : base(message)
    {
    }
}
