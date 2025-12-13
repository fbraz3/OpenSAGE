using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// Performance profiling infrastructure for Week 25 optimization
///
/// Captures:
/// - Frame timing (average, min, max, percentiles)
/// - Memory usage (heap, GC pressure)
/// - CPU hot paths
/// - GPU utilization metrics
/// - State change counts
/// - Draw call counts
///
/// Used to establish performance baseline and identify optimization opportunities
/// </summary>
public class PerformanceProfiler : IDisposable
{
    private readonly List<FrameMetrics> _frameMetrics = new();
    private readonly List<GCMetrics> _gcMetrics = new();
    private readonly Stopwatch _sessionTimer = new();
    private bool _isProfileing;
    private bool _disposed;

    /// <summary>
    /// Start performance profiling session
    /// </summary>
    public void StartSession()
    {
        if (_isProfileing)
            throw new InvalidOperationException("Profiling session already active");

        _frameMetrics.Clear();
        _gcMetrics.Clear();
        _sessionTimer.Restart();
        _isProfileing = true;
    }

    /// <summary>
    /// End performance profiling session
    /// </summary>
    public void EndSession()
    {
        if (!_isProfileing)
            throw new InvalidOperationException("No profiling session active");

        _sessionTimer.Stop();
        _isProfileing = false;
    }

    /// <summary>
    /// Record frame metrics (called once per frame)
    /// </summary>
    public void RecordFrame(double frameTimeMs, int drawCalls, int stateChanges, long memoryBytes)
    {
        if (!_isProfileing)
            throw new InvalidOperationException("Profiling session not active");

        _frameMetrics.Add(new FrameMetrics
        {
            FrameTimeMs = frameTimeMs,
            DrawCalls = drawCalls,
            StateChanges = stateChanges,
            MemoryBytes = memoryBytes,
            Timestamp = _sessionTimer.ElapsedMilliseconds
        });

        // Record GC metrics every frame
        var totalMemory = GC.GetTotalMemory(false);
        var gen0Collections = GC.CollectionCount(0);
        var gen1Collections = GC.CollectionCount(1);
        var gen2Collections = GC.CollectionCount(2);

        _gcMetrics.Add(new GCMetrics
        {
            TotalMemory = totalMemory,
            Gen0Collections = gen0Collections,
            Gen1Collections = gen1Collections,
            Gen2Collections = gen2Collections,
            Timestamp = _sessionTimer.ElapsedMilliseconds
        });
    }

    /// <summary>
    /// Get performance profiling results
    /// </summary>
    public PerformanceProfile GetProfile()
    {
        if (_frameMetrics.Count == 0)
            return new PerformanceProfile();

        var frameTimesSorted = _frameMetrics.Select(f => f.FrameTimeMs).OrderBy(x => x).ToList();
        var memoryReadings = _frameMetrics.Select(f => f.MemoryBytes).OrderBy(x => x).ToList();

        var profile = new PerformanceProfile
        {
            SessionDurationMs = _sessionTimer.ElapsedMilliseconds,
            TotalFrames = _frameMetrics.Count,

            // Frame timing statistics
            AverageFrameTime = _frameMetrics.Average(f => f.FrameTimeMs),
            MinFrameTime = _frameMetrics.Min(f => f.FrameTimeMs),
            MaxFrameTime = _frameMetrics.Max(f => f.FrameTimeMs),
            MedianFrameTime = GetPercentile(frameTimesSorted, 0.5),
            P95FrameTime = GetPercentile(frameTimesSorted, 0.95),
            P99FrameTime = GetPercentile(frameTimesSorted, 0.99),

            // Frame metrics
            AverageDrawCalls = _frameMetrics.Average(f => f.DrawCalls),
            AverageStateChanges = _frameMetrics.Average(f => f.StateChanges),
            TotalDrawCalls = _frameMetrics.Sum(f => f.DrawCalls),
            TotalStateChanges = _frameMetrics.Sum(f => f.StateChanges),

            // Memory statistics
            AverageMemory = (long)_frameMetrics.Average(f => f.MemoryBytes),
            PeakMemory = _frameMetrics.Max(f => f.MemoryBytes),
            MinMemory = _frameMetrics.Min(f => f.MemoryBytes),

            // Derived metrics
            AverageFps = 1000.0 / _frameMetrics.Average(f => f.FrameTimeMs),
            FrameTimeVariance = CalculateVariance(frameTimesSorted),

            // GC metrics
            TotalGCCollections = _gcMetrics.Count > 0
                ? (_gcMetrics.Last().Gen0Collections +
                   _gcMetrics.Last().Gen1Collections +
                   _gcMetrics.Last().Gen2Collections)
                : 0
        };

        return profile;
    }

    /// <summary>
    /// Generate human-readable performance report
    /// </summary>
    public string GenerateReport()
    {
        var profile = GetProfile();
        var lines = new List<string>
        {
            "=== PERFORMANCE PROFILING REPORT ===",
            $"Session Duration: {profile.SessionDurationMs}ms",
            $"Total Frames: {profile.TotalFrames}",
            "",
            "=== FRAME TIMING (ms) ===",
            $"Average:       {profile.AverageFrameTime:F2}",
            $"Minimum:       {profile.MinFrameTime:F2}",
            $"Maximum:       {profile.MaxFrameTime:F2}",
            $"Median:        {profile.MedianFrameTime:F2}",
            $"95th %ile:     {profile.P95FrameTime:F2}",
            $"99th %ile:     {profile.P99FrameTime:F2}",
            $"Variance:      {profile.FrameTimeVariance:F2}",
            $"FPS (avg):     {profile.AverageFps:F1}",
            "",
            "=== RENDERING METRICS ===",
            $"Total Draw Calls:     {profile.TotalDrawCalls}",
            $"Average per frame:    {profile.AverageDrawCalls:F1}",
            $"Total State Changes:  {profile.TotalStateChanges}",
            $"Average per frame:    {profile.AverageStateChanges:F1}",
            "",
            "=== MEMORY (MB) ===",
            $"Average:       {profile.AverageMemory / 1024.0 / 1024.0:F1}",
            $"Minimum:       {profile.MinMemory / 1024.0 / 1024.0:F1}",
            $"Peak:          {profile.PeakMemory / 1024.0 / 1024.0:F1}",
            $"Total GC Calls: {profile.TotalGCCollections}",
            ""
        };

        // Performance assessment
        lines.Add("=== ASSESSMENT ===");

        if (profile.AverageFps >= 60)
            lines.Add("✅ Frame rate: EXCELLENT (60+ FPS)");
        else if (profile.AverageFps >= 30)
            lines.Add("⚠️  Frame rate: ACCEPTABLE (30-60 FPS)");
        else
            lines.Add("❌ Frame rate: POOR (<30 FPS)");

        if (profile.FrameTimeVariance < 5)
            lines.Add("✅ Frame time variance: EXCELLENT (<5ms)");
        else if (profile.FrameTimeVariance < 10)
            lines.Add("⚠️  Frame time variance: ACCEPTABLE (5-10ms)");
        else
            lines.Add("❌ Frame time variance: POOR (>10ms)");

        lines.Add("");
        lines.Add("=== OPTIMIZATION OPPORTUNITIES ===");

        if (profile.AverageDrawCalls > 1000)
            lines.Add("• High draw call count - consider batching or LOD optimization");
        if (profile.AverageStateChanges > 500)
            lines.Add("• High state change count - consider grouping similar state changes");
        if (profile.PeakMemory > 1024 * 1024 * 512) // 512 MB
            lines.Add("• Peak memory usage is high - consider memory pooling optimization");
        if (profile.FrameTimeVariance > 10)
            lines.Add("• High frame time variance - investigate for GC stalls or frame drops");

        return string.Join(Environment.NewLine, lines);
    }

    private double GetPercentile(List<double> sortedValues, double percentile)
    {
        if (sortedValues.Count == 0) return 0;
        int index = (int)((sortedValues.Count - 1) * percentile);
        return sortedValues[Math.Max(0, Math.Min(sortedValues.Count - 1, index))];
    }

    private double CalculateVariance(List<double> values)
    {
        if (values.Count == 0) return 0;
        double mean = values.Average();
        double variance = values.Sum(x => Math.Pow(x - mean, 2)) / values.Count;
        return Math.Sqrt(variance); // Standard deviation
    }

    public void Dispose()
    {
        if (_disposed) return;
        _sessionTimer?.Stop();
        _disposed = true;
    }
}

/// <summary>
/// Per-frame performance metrics
/// </summary>
public class FrameMetrics
{
    public double FrameTimeMs { get; set; }
    public int DrawCalls { get; set; }
    public int StateChanges { get; set; }
    public long MemoryBytes { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Per-frame garbage collection metrics
/// </summary>
public class GCMetrics
{
    public long TotalMemory { get; set; }
    public int Gen0Collections { get; set; }
    public int Gen1Collections { get; set; }
    public int Gen2Collections { get; set; }
    public long Timestamp { get; set; }
}

/// <summary>
/// Aggregated performance profiling results
/// </summary>
public class PerformanceProfile
{
    [JsonPropertyName("sessionDurationMs")]
    public long SessionDurationMs { get; set; }

    [JsonPropertyName("totalFrames")]
    public int TotalFrames { get; set; }

    // Frame timing
    [JsonPropertyName("averageFrameTimeMs")]
    public double AverageFrameTime { get; set; }

    [JsonPropertyName("minFrameTimeMs")]
    public double MinFrameTime { get; set; }

    [JsonPropertyName("maxFrameTimeMs")]
    public double MaxFrameTime { get; set; }

    [JsonPropertyName("medianFrameTimeMs")]
    public double MedianFrameTime { get; set; }

    [JsonPropertyName("p95FrameTimeMs")]
    public double P95FrameTime { get; set; }

    [JsonPropertyName("p99FrameTimeMs")]
    public double P99FrameTime { get; set; }

    [JsonPropertyName("frameTimeVarianceMs")]
    public double FrameTimeVariance { get; set; }

    [JsonPropertyName("averageFps")]
    public double AverageFps { get; set; }

    // Rendering metrics
    [JsonPropertyName("totalDrawCalls")]
    public long TotalDrawCalls { get; set; }

    [JsonPropertyName("averageDrawCalls")]
    public double AverageDrawCalls { get; set; }

    [JsonPropertyName("totalStateChanges")]
    public long TotalStateChanges { get; set; }

    [JsonPropertyName("averageStateChanges")]
    public double AverageStateChanges { get; set; }

    // Memory
    [JsonPropertyName("averageMemoryBytes")]
    public long AverageMemory { get; set; }

    [JsonPropertyName("peakMemoryBytes")]
    public long PeakMemory { get; set; }

    [JsonPropertyName("minMemoryBytes")]
    public long MinMemory { get; set; }

    [JsonPropertyName("totalGCCollections")]
    public int TotalGCCollections { get; set; }
}

/// <summary>
/// CPU hot path tracking for optimization
/// </summary>
public class CpuHotPathTracker
{
    private readonly Dictionary<string, HotPath> _hotPaths = new();

    public void RecordHotPath(string name, long elapsedMs)
    {
        if (!_hotPaths.ContainsKey(name))
            _hotPaths[name] = new HotPath { Name = name };

        var path = _hotPaths[name];
        path.CallCount++;
        path.TotalTimeMs += elapsedMs;
        path.MinTimeMs = Math.Min(path.MinTimeMs, elapsedMs);
        path.MaxTimeMs = Math.Max(path.MaxTimeMs, elapsedMs);
    }

    public List<HotPath> GetHotPaths()
    {
        return _hotPaths.Values
            .OrderByDescending(p => p.TotalTimeMs)
            .ToList();
    }

    public string GenerateHotPathReport()
    {
        var paths = GetHotPaths();
        if (paths.Count == 0)
            return "No hot paths recorded";

        var lines = new List<string>
        {
            "=== CPU HOT PATH ANALYSIS ===",
            ""
        };

        foreach (var path in paths.Take(20))
        {
            var avgTime = path.TotalTimeMs / (double)path.CallCount;
            lines.Add($"{path.Name}:");
            lines.Add($"  Calls: {path.CallCount}");
            lines.Add($"  Total: {path.TotalTimeMs}ms");
            lines.Add($"  Average: {avgTime:F2}ms");
            lines.Add($"  Min/Max: {path.MinTimeMs}/{path.MaxTimeMs}ms");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Individual hot path metrics
/// </summary>
public class HotPath
{
    public string Name { get; set; }
    public long CallCount { get; set; }
    public long TotalTimeMs { get; set; }
    public long MinTimeMs { get; set; } = long.MaxValue;
    public long MaxTimeMs { get; set; }
}
