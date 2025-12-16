using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;

namespace OpenSage.Diagnostics;

#nullable enable

/// <summary>
/// Hierarchical performance profiler that tracks nested timing operations.
/// Maintains both "gross" time (including sub-operations) and "net" time (excluding sub-operations).
/// Matches EA Generals PerfGather implementation pattern.
/// </summary>
public class PerfGather
{
    private readonly string _name;
    private readonly List<PerfGather> _children;
    private long _grossStartTick;
    private long _grossTotalTicks;
    private long _netTotalTicks;
    private int _callCount;
    private bool _isRunning;

    private static Stack<PerfGather> CurrentStack { get; } = new();
    private static Dictionary<string, PerfGather> RootGatherers { get; } = new();

    public string Name => _name;
    public IReadOnlyList<PerfGather> Children => _children.AsReadOnly();
    public int CallCount => _callCount;
    public double GrossTotalMs => (_grossTotalTicks * 1000.0) / Stopwatch.Frequency;
    public double GrossAverageMs => CallCount > 0 ? GrossTotalMs / CallCount : 0.0;
    public double NetTotalMs => (_netTotalTicks * 1000.0) / Stopwatch.Frequency;
    public double NetAverageMs => CallCount > 0 ? NetTotalMs / CallCount : 0.0;

    public PerfGather(string name)
    {
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _children = new List<PerfGather>();
        _grossStartTick = 0;
        _grossTotalTicks = 0;
        _netTotalTicks = 0;
        _callCount = 0;
        _isRunning = false;
    }

    /// <summary>
    /// Starts profiling this scope. Push onto stack to track hierarchy.
    /// </summary>
    public void Start()
    {
        if (_isRunning)
        {
            throw new InvalidOperationException($"PerfGather '{_name}' is already running");
        }

        _grossStartTick = Stopwatch.GetTimestamp();
        _isRunning = true;
        CurrentStack.Push(this);
    }

    /// <summary>
    /// Stops profiling this scope and pops from stack.
    /// Calculates gross (including children) and net (excluding children) times.
    /// </summary>
    public void Stop()
    {
        if (!_isRunning)
        {
            throw new InvalidOperationException($"PerfGather '{_name}' is not running");
        }

        var grossElapsedTicks = Stopwatch.GetTimestamp() - _grossStartTick;
        var childrenTicks = _children.Aggregate(0L, (sum, child) => sum + child._netTotalTicks);
        var netElapsedTicks = grossElapsedTicks - childrenTicks;

        _grossTotalTicks += grossElapsedTicks;
        _netTotalTicks += netElapsedTicks;
        _callCount++;
        _isRunning = false;

        if (CurrentStack.Count > 0)
        {
            CurrentStack.Pop();
        }
    }

    /// <summary>
    /// Finds or creates a child profiler with the given name.
    /// </summary>
    private PerfGather GetOrCreateChild(string name)
    {
        var existing = _children.Find(c => c._name == name);
        if (existing != null)
        {
            return existing;
        }

        var child = new PerfGather(name);
        _children.Add(child);
        return child;
    }

    /// <summary>
    /// Gets the current profiling context (top of stack), or null if no profile is active.
    /// </summary>
    public static PerfGather? Current => CurrentStack.Count > 0 ? CurrentStack.Peek() : null;

    /// <summary>
    /// Finds or creates a root profiler with the given name.
    /// </summary>
    public static PerfGather GetOrCreate(string name)
    {
        if (!RootGatherers.TryGetValue(name, out var gatherer))
        {
            gatherer = new PerfGather(name);
            RootGatherers[name] = gatherer;
        }

        return gatherer;
    }

    /// <summary>
    /// Executes an action within a profiling scope.
    /// Automatically manages hierarchy based on call stack.
    /// </summary>
    public static void Profile(string name, Action action)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        PerfGather gatherer;

        if (Current != null)
        {
            // Nested call - add as child of current
            gatherer = Current.GetOrCreateChild(name);
        }
        else
        {
            // Root call
            gatherer = GetOrCreate(name);
        }

        gatherer.Start();
        try
        {
            action();
        }
        finally
        {
            gatherer.Stop();
        }
    }

    /// <summary>
    /// Executes a function within a profiling scope.
    /// </summary>
    public static T Profile<T>(string name, Func<T> function)
    {
        if (string.IsNullOrEmpty(name))
        {
            throw new ArgumentException("Name cannot be null or empty", nameof(name));
        }

        if (function == null)
        {
            throw new ArgumentNullException(nameof(function));
        }

        PerfGather gatherer;

        if (Current != null)
        {
            gatherer = Current.GetOrCreateChild(name);
        }
        else
        {
            gatherer = GetOrCreate(name);
        }

        gatherer.Start();
        try
        {
            return function();
        }
        finally
        {
            gatherer.Stop();
        }
    }

    /// <summary>
    /// Resets all profiling data.
    /// </summary>
    public static void ResetAll()
    {
        foreach (var gatherer in RootGatherers.Values)
        {
            gatherer.ResetRecursive();
        }

        RootGatherers.Clear();
        CurrentStack.Clear();
    }

    /// <summary>
    /// Resets this gatherer and all children recursively.
    /// </summary>
    public void ResetRecursive()
    {
        _grossTotalTicks = 0;
        _netTotalTicks = 0;
        _callCount = 0;
        _isRunning = false;

        foreach (var child in _children)
        {
            child.ResetRecursive();
        }

        _children.Clear();
    }

    /// <summary>
    /// Returns a formatted string representation of profiling results.
    /// Recursively includes child profilers with indentation.
    /// </summary>
    public string GetReport(int indentLevel = 0)
    {
        var indent = new string(' ', indentLevel * 2);
        var sb = new StringBuilder();

        if (CallCount == 0)
        {
            sb.AppendLine($"{indent}{_name}: (no data)");
        }
        else
        {
            sb.AppendLine($"{indent}{_name}: gross={GrossAverageMs:F2}ms, net={NetAverageMs:F2}ms (x{CallCount})");
        }

        // Sort children by gross time (descending)
        var sortedChildren = new List<PerfGather>(_children);
        sortedChildren.Sort((a, b) => b.GrossTotalMs.CompareTo(a.GrossTotalMs));

        foreach (var child in sortedChildren)
        {
            sb.Append(child.GetReport(indentLevel + 1));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Exports profiling data to CSV format (similar to EA Generals format).
    /// Format: Name, GrossAvgMs, NetAvgMs, MinMs, MaxMs, CallCount
    /// </summary>
    public string ExportToCsv()
    {
        var sb = new StringBuilder();
        sb.AppendLine("Name,GrossAvgMs,NetAvgMs,CallCount");

        ExportToCsvRecursive(sb, "");

        return sb.ToString();
    }

    private void ExportToCsvRecursive(StringBuilder sb, string prefix)
    {
        if (CallCount > 0)
        {
            var fullName = string.IsNullOrEmpty(prefix) ? _name : $"{prefix}/{_name}";
            sb.AppendLine($"\"{fullName}\",{GrossAverageMs:F4},{NetAverageMs:F4},{CallCount}");
        }

        foreach (var child in _children)
        {
            child.ExportToCsvRecursive(sb, string.IsNullOrEmpty(prefix) ? _name : $"{prefix}/{_name}");
        }
    }

    public override string ToString()
    {
        return CallCount == 0
            ? $"{_name}: (no data)"
            : $"{_name}: gross={GrossAverageMs:F2}ms, net={NetAverageMs:F2}ms";
    }
}
