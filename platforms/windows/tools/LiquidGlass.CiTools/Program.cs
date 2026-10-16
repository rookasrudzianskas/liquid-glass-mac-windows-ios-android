using System;
using System.Collections.Generic;
using System.Linq;

namespace LiquidGlass.CiTools;

internal static class Program
{
    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Length == 0) return Usage();
        var options = Options.Parse(args.Skip(1));
        try
        {
            return args[0] switch
            {
                "probe" => RenderProbe.Run(options.Require("out")),
                _ => Usage(),
            };
        }
        catch (Exception e)
        {
            Console.Error.WriteLine(e);
            return 1;
        }
    }

    private static int Usage()
    {
        Console.Error.WriteLine("usage: LiquidGlass.CiTools probe --out <dir>");
        return 2;
    }
}

/// <summary>Minimal <c>--name value</c> argument parsing.</summary>
internal sealed class Options
{
    private readonly Dictionary<string, string> _values;

    private Options(Dictionary<string, string> values) => _values = values;

    public static Options Parse(IEnumerable<string> args)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        string? name = null;
        foreach (var arg in args)
        {
            if (arg.StartsWith("--", StringComparison.Ordinal))
            {
                if (name is not null) values[name] = "true";
                name = arg.Substring(2);
            }
            else if (name is not null)
            {
                values[name] = arg;
                name = null;
            }
        }
        if (name is not null) values[name] = "true";
        return new Options(values);
    }

    public string Require(string name) => _values.TryGetValue(name, out var value)
        ? value
        : throw new ArgumentException($"--{name} is required.");

    public string? Get(string name) => _values.TryGetValue(name, out var value) ? value : null;
}
