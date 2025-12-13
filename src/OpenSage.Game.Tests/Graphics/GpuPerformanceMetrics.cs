using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// GPU performance metrics collection for Week 25 optimization
///
/// Tracks GPU-specific metrics:
/// - Texture memory bandwidth and usage
/// - Render target switches
/// - State changes and pipeline bindings
/// - GPU command buffer sizes
/// - Draw call complexity
///
/// Helps identify GPU bottlenecks and optimization opportunities
/// </summary>
public class GpuPerformanceMetrics
{
    private readonly List<GpuFrameMetrics> _frameMetrics = new();
    private bool _isCollecting;
    private bool _disposed;

    public void StartCollection()
    {
        if (_isCollecting)
            throw new InvalidOperationException("GPU metrics collection already active");

        _frameMetrics.Clear();
        _isCollecting = true;
    }

    public void EndCollection()
    {
        if (!_isCollecting)
            throw new InvalidOperationException("GPU metrics collection not active");

        _isCollecting = false;
    }

    /// <summary>
    /// Record GPU metrics for a single frame
    /// </summary>
    public void RecordFrame(
        int drawCalls,
        int stateChanges,
        long textureMemoryBytes,
        long renderTargetMemoryBytes,
        int renderTargetSwitches,
        long commandBufferBytes,
        double estimatedGpuTimeMs)
    {
        if (!_isCollecting)
            throw new InvalidOperationException("GPU metrics collection not active");

        _frameMetrics.Add(new GpuFrameMetrics
        {
            DrawCalls = drawCalls,
            StateChanges = stateChanges,
            TextureMemoryBytes = textureMemoryBytes,
            RenderTargetMemoryBytes = renderTargetMemoryBytes,
            RenderTargetSwitches = renderTargetSwitches,
            CommandBufferBytes = commandBufferBytes,
            EstimatedGpuTimeMs = estimatedGpuTimeMs
        });
    }

    /// <summary>
    /// Get aggregated GPU metrics
    /// </summary>
    public GpuPerformanceProfile GetProfile()
    {
        if (_frameMetrics.Count == 0)
            return new GpuPerformanceProfile();

        var profile = new GpuPerformanceProfile
        {
            TotalFrames = _frameMetrics.Count,

            // Draw call metrics
            TotalDrawCalls = _frameMetrics.Sum(f => f.DrawCalls),
            AverageDrawCalls = _frameMetrics.Average(f => f.DrawCalls),
            MaxDrawCalls = _frameMetrics.Max(f => f.DrawCalls),
            MinDrawCalls = _frameMetrics.Min(f => f.DrawCalls),

            // State change metrics
            TotalStateChanges = _frameMetrics.Sum(f => f.StateChanges),
            AverageStateChanges = _frameMetrics.Average(f => f.StateChanges),
            MaxStateChanges = _frameMetrics.Max(f => f.StateChanges),

            // Memory metrics
            AverageTextureMemoryBytes = (long)_frameMetrics.Average(f => f.TextureMemoryBytes),
            PeakTextureMemoryBytes = _frameMetrics.Max(f => f.TextureMemoryBytes),

            AverageRenderTargetMemoryBytes = (long)_frameMetrics.Average(f => f.RenderTargetMemoryBytes),
            PeakRenderTargetMemoryBytes = _frameMetrics.Max(f => f.RenderTargetMemoryBytes),

            // Render target switching
            TotalRenderTargetSwitches = _frameMetrics.Sum(f => f.RenderTargetSwitches),
            AverageRenderTargetSwitches = _frameMetrics.Average(f => f.RenderTargetSwitches),

            // Command buffer
            AverageCommandBufferBytes = (long)_frameMetrics.Average(f => f.CommandBufferBytes),
            PeakCommandBufferBytes = _frameMetrics.Max(f => f.CommandBufferBytes),

            // Estimated GPU time
            AverageGpuTimeMs = _frameMetrics.Average(f => f.EstimatedGpuTimeMs),
            MaxGpuTimeMs = _frameMetrics.Max(f => f.EstimatedGpuTimeMs),

            // Derived metrics
            DrawCallsPerMB = _frameMetrics.Sum(f => f.DrawCalls) /
                             Math.Max(1, (_frameMetrics.Sum(f => f.TextureMemoryBytes) / (1024.0 * 1024.0))),

            StateChangesPerDrawCall = _frameMetrics.Sum(f => f.StateChanges) > 0
                ? (_frameMetrics.Sum(f => f.StateChanges) / (double)Math.Max(1, _frameMetrics.Sum(f => f.DrawCalls)))
                : 0
        };

        return profile;
    }

    /// <summary>
    /// Generate human-readable GPU performance report
    /// </summary>
    public string GenerateReport()
    {
        var profile = GetProfile();
        var lines = new List<string>
        {
            "=== GPU PERFORMANCE REPORT ===",
            $"Total Frames: {profile.TotalFrames}",
            "",
            "=== DRAW CALLS ===",
            $"Total:         {profile.TotalDrawCalls}",
            $"Average/frame: {profile.AverageDrawCalls:F1}",
            $"Min:           {profile.MinDrawCalls}",
            $"Max:           {profile.MaxDrawCalls}",
            "",
            "=== STATE CHANGES ===",
            $"Total:         {profile.TotalStateChanges}",
            $"Average/frame: {profile.AverageStateChanges:F1}",
            $"Max/frame:     {profile.MaxStateChanges}",
            $"Per draw call: {profile.StateChangesPerDrawCall:F2}",
            "",
            "=== TEXTURE MEMORY (MB) ===",
            $"Average: {profile.AverageTextureMemoryBytes / 1024.0 / 1024.0:F1}",
            $"Peak:    {profile.PeakTextureMemoryBytes / 1024.0 / 1024.0:F1}",
            "",
            "=== RENDER TARGETS (MB) ===",
            $"Average: {profile.AverageRenderTargetMemoryBytes / 1024.0 / 1024.0:F1}",
            $"Peak:    {profile.PeakRenderTargetMemoryBytes / 1024.0 / 1024.0:F1}",
            $"Switches/frame: {profile.AverageRenderTargetSwitches:F1}",
            "",
            "=== COMMAND BUFFER (KB) ===",
            $"Average: {profile.AverageCommandBufferBytes / 1024.0:F1}",
            $"Peak:    {profile.PeakCommandBufferBytes / 1024.0:F1}",
            "",
            "=== GPU TIME ===",
            $"Average: {profile.AverageGpuTimeMs:F2}ms",
            $"Max:     {profile.MaxGpuTimeMs:F2}ms",
            "",
            "=== EFFICIENCY METRICS ===",
            $"Draw calls per MB texture: {profile.DrawCallsPerMB:F2}",
            $"State changes per draw call: {profile.StateChangesPerDrawCall:F2}",
            ""
        };

        // GPU assessment
        lines.Add("=== OPTIMIZATION OPPORTUNITIES ===");

        if (profile.AverageStateChanges > 100)
            lines.Add("• High state change rate - consider grouping similar states");

        if (profile.StateChangesPerDrawCall > 2)
            lines.Add("• Too many state changes per draw call - reduce batching complexity");

        if (profile.AverageRenderTargetSwitches > 10)
            lines.Add("• High render target switching overhead - reduce target changes");

        if (profile.PeakCommandBufferBytes > 1024 * 1024) // 1 MB
            lines.Add("• Large command buffers - reduce recorded command complexity");

        if (profile.PeakTextureMemoryBytes > 256 * 1024 * 1024) // 256 MB
            lines.Add("• High texture memory usage - consider compression or pooling");

        return string.Join(Environment.NewLine, lines);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _frameMetrics.Clear();
        _disposed = true;
    }
}

/// <summary>
/// Per-frame GPU metrics
/// </summary>
public class GpuFrameMetrics
{
    public int DrawCalls { get; set; }
    public int StateChanges { get; set; }
    public long TextureMemoryBytes { get; set; }
    public long RenderTargetMemoryBytes { get; set; }
    public int RenderTargetSwitches { get; set; }
    public long CommandBufferBytes { get; set; }
    public double EstimatedGpuTimeMs { get; set; }
}

/// <summary>
/// Aggregated GPU performance metrics
/// </summary>
public class GpuPerformanceProfile
{
    [JsonPropertyName("totalFrames")]
    public int TotalFrames { get; set; }

    // Draw call metrics
    [JsonPropertyName("totalDrawCalls")]
    public long TotalDrawCalls { get; set; }

    [JsonPropertyName("averageDrawCalls")]
    public double AverageDrawCalls { get; set; }

    [JsonPropertyName("maxDrawCalls")]
    public int MaxDrawCalls { get; set; }

    [JsonPropertyName("minDrawCalls")]
    public int MinDrawCalls { get; set; }

    // State changes
    [JsonPropertyName("totalStateChanges")]
    public long TotalStateChanges { get; set; }

    [JsonPropertyName("averageStateChanges")]
    public double AverageStateChanges { get; set; }

    [JsonPropertyName("maxStateChanges")]
    public int MaxStateChanges { get; set; }

    // Memory
    [JsonPropertyName("averageTextureMemoryBytes")]
    public long AverageTextureMemoryBytes { get; set; }

    [JsonPropertyName("peakTextureMemoryBytes")]
    public long PeakTextureMemoryBytes { get; set; }

    [JsonPropertyName("averageRenderTargetMemoryBytes")]
    public long AverageRenderTargetMemoryBytes { get; set; }

    [JsonPropertyName("peakRenderTargetMemoryBytes")]
    public long PeakRenderTargetMemoryBytes { get; set; }

    // Render targets
    [JsonPropertyName("totalRenderTargetSwitches")]
    public long TotalRenderTargetSwitches { get; set; }

    [JsonPropertyName("averageRenderTargetSwitches")]
    public double AverageRenderTargetSwitches { get; set; }

    // Command buffer
    [JsonPropertyName("averageCommandBufferBytes")]
    public long AverageCommandBufferBytes { get; set; }

    [JsonPropertyName("peakCommandBufferBytes")]
    public long PeakCommandBufferBytes { get; set; }

    // GPU time
    [JsonPropertyName("averageGpuTimeMs")]
    public double AverageGpuTimeMs { get; set; }

    [JsonPropertyName("maxGpuTimeMs")]
    public double MaxGpuTimeMs { get; set; }

    // Efficiency
    [JsonPropertyName("drawCallsPerMB")]
    public double DrawCallsPerMB { get; set; }

    [JsonPropertyName("stateChangesPerDrawCall")]
    public double StateChangesPerDrawCall { get; set; }
}

/// <summary>
/// GPU utilization tracker for identifying bottlenecks
/// </summary>
public class GpuUtilizationTracker
{
    private class GpuOperation
    {
        public string Name { get; set; }
        public long Duration { get; set; }
        public int CallCount { get; set; }
    }

    private readonly Dictionary<string, GpuOperation> _operations = new();

    public void RecordOperation(string name, long durationMs)
    {
        if (!_operations.ContainsKey(name))
            _operations[name] = new GpuOperation { Name = name };

        var op = _operations[name];
        op.Duration += durationMs;
        op.CallCount++;
    }

    public string GenerateUtilizationReport()
    {
        if (_operations.Count == 0)
            return "No GPU operations recorded";

        var sorted = _operations.Values
            .OrderByDescending(o => o.Duration)
            .ToList();

        var lines = new List<string>
        {
            "=== GPU UTILIZATION BREAKDOWN ===",
            ""
        };

        var totalDuration = sorted.Sum(o => o.Duration);

        foreach (var op in sorted.Take(15))
        {
            var percentage = (op.Duration / (double)Math.Max(1, totalDuration)) * 100;
            lines.Add($"{op.Name}:");
            lines.Add($"  Time: {op.Duration}ms ({percentage:F1}%)");
            lines.Add($"  Calls: {op.CallCount}");
            lines.Add($"  Avg/call: {op.Duration / (double)op.CallCount:F2}ms");
            lines.Add("");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
