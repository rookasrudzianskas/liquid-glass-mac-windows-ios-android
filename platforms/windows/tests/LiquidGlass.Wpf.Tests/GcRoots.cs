using System;
using System.Collections.Generic;
using System.Threading;
#if NET10_0_OR_GREATER
using System.Linq;
using Microsoft.Diagnostics.Runtime;
#endif

namespace LiquidGlass.Wpf.Tests;

/// <summary>
/// Explains why an object is still alive: the chain of references from a GC root to it, read from a snapshot of this
/// process with ClrMD (on .NET 10; elsewhere it says nothing).
/// </summary>
internal static class GcRoots
{
    /// <summary>The reference chain from a root to the first live instance of <paramref name="type"/>, as text to append to a message.</summary>
    public static string PathTo(Type type)
    {
#if NET10_0_OR_GREATER
        string? result = null;
        // Reading the snapshot is independent of this thread's dispatcher; bound it in time so a failure only reports.
        var worker = new Thread(() =>
        {
            try
            {
                result = Find(type.Name);
            }
            catch (Exception e)
            {
                result = $" (the GC roots could not be read: {e.GetType().Name}: {e.Message})";
            }
        });
        worker.Start();
        return worker.Join(TimeSpan.FromMinutes(2)) ? result ?? string.Empty : " (reading the GC roots timed out)";
#else
        _ = type;
        return string.Empty;
#endif
    }

#if NET10_0_OR_GREATER
    private static string Find(string typeName)
    {
        using var target = DataTarget.CreateSnapshotAndAttach(Environment.ProcessId);
        using var runtime = target.ClrVersions[0].CreateRuntime();
        var heap = runtime.Heap;
        var targets = new HashSet<ulong>(heap.EnumerateObjects()
            .Where(o => o.Type?.Name is { } name && (name.EndsWith("+" + typeName, StringComparison.Ordinal) || name.EndsWith("." + typeName, StringComparison.Ordinal)))
            .Select(o => o.Address));
        if (targets.Count == 0) return $" (no {typeName} on the heap in the snapshot)";

        // Breadth first from every strong root, so the chain found is a shortest one.
        var from = new Dictionary<ulong, (ulong Parent, string Via)>();
        var queue = new Queue<ulong>();
        foreach (var root in heap.EnumerateRoots())
        {
            var address = root.Object.Address;
            if (address == 0 || from.ContainsKey(address)) continue;
            from[address] = (0, $"{root.RootKind} root");
            queue.Enqueue(address);
        }
        while (queue.Count > 0)
        {
            var address = queue.Dequeue();
            if (targets.Contains(address)) return Describe(heap, from, address);
            var obj = heap.GetObject(address);
            if (!obj.IsValid) continue;
            foreach (var reference in obj.EnumerateReferencesWithFields(carefully: true, considerDependantHandles: true))
            {
                var child = reference.Object.Address;
                if (child == 0 || from.ContainsKey(child)) continue;
                var via = reference.IsDependentHandle ? "dependent handle" : reference.IsArrayElement ? "array element" : reference.Field?.Name ?? "field";
                from[child] = (address, via);
                queue.Enqueue(child);
            }
        }
        return $" ({targets.Count} {typeName} on the heap, but no strong root reaches it)";
    }

    private static string Describe(ClrHeap heap, Dictionary<ulong, (ulong Parent, string Via)> from, ulong address)
    {
        var chain = new List<string>();
        for (var current = address; current != 0; current = from[current].Parent)
            chain.Add($"{heap.GetObjectType(current)?.Name ?? "?"} ({from[current].Via})");
        chain.Reverse();
        return ", from a GC root:\n      " + string.Join("\n   -> ", chain);
    }
#endif
}
