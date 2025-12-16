#nullable enable

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using OpenSage.Content;
using OpenSage.Graphics;
using OpenSage.Gui.TextureAtlasing;

namespace OpenSage.Gui.Benchmarks;

/// <summary>
/// Benchmarks texture atlasing performance and tracks rendering optimizations.
/// Can measure texture bind reduction, frame time improvements, and memory overhead.
/// </summary>
public sealed class TextureAtlasingBenchmark : DisposableBase
{
    private readonly MappedImageRenderOptimizer _optimizer;
    private readonly Stopwatch _frameTimer;
    private readonly List<RenderOptimizationStatistics> _frameStatistics;
    private readonly List<long> _frameTimes;
    private int _frameCount;
    private long _totalFrameTime;

    /// <summary>
    /// Gets the number of frames profiled so far.
    /// </summary>
    public int FrameCount => _frameCount;

    /// <summary>
    /// Gets the total frame time accumulated (in milliseconds).
    /// </summary>
    public long TotalFrameTime => _totalFrameTime;

    /// <summary>
    /// Gets the average frame time (in milliseconds).
    /// </summary>
    public double AverageFrameTime => _frameCount > 0 ? (double)_totalFrameTime / _frameCount : 0;

    /// <summary>
    /// Gets the list of all frame statistics collected.
    /// </summary>
    public IReadOnlyList<RenderOptimizationStatistics> FrameStatistics => _frameStatistics.AsReadOnly();

    public TextureAtlasingBenchmark(ScopedAssetCollection<MappedImage> mappedImages)
    {
        _optimizer = new MappedImageRenderOptimizer(mappedImages);
        _frameTimer = new Stopwatch();
        _frameStatistics = new List<RenderOptimizationStatistics>();
        _frameTimes = new List<long>();
        _frameCount = 0;
        _totalFrameTime = 0;
    }

    /// <summary>
    /// Starts profiling a single frame. Call this at the beginning of each frame to benchmark.
    /// </summary>
    public void StartFrameProfiling()
    {
        _optimizer.EnableProfiling();
        _frameTimer.Restart();
    }

    /// <summary>
    /// Ends profiling for the current frame and records statistics.
    /// Call this at the end of each frame after rendering is complete.
    /// </summary>
    public void EndFrameProfiling()
    {
        _frameTimer.Stop();
        var stats = _optimizer.DisableProfiling();

        _frameStatistics.Add(stats);
        var frameTime = _frameTimer.ElapsedMilliseconds;
        _frameTimes.Add(frameTime);

        _totalFrameTime += frameTime;
        _frameCount++;
    }

    /// <summary>
    /// Gets a summary report of the benchmark results.
    /// </summary>
    public BenchmarkReport GetReport()
    {
        var textureBindStats = CalculateTextureBindStatistics();
        var lookupStats = CalculateLookupStatistics();
        var memoryStats = CalculateMemoryStatistics();

        return new BenchmarkReport
        {
            FrameCount = _frameCount,
            AverageFrameTime = AverageFrameTime,
            MinFrameTime = _frameTimes.Count > 0 ? _frameTimes.Min() : 0,
            MaxFrameTime = _frameTimes.Count > 0 ? _frameTimes.Max() : 0,
            TextureBindStats = textureBindStats,
            LookupStats = lookupStats,
            MemoryStats = memoryStats,
            Recommendations = GenerateOptimizationRecommendations()
        };
    }

    private TextureBindStatistics CalculateTextureBindStatistics()
    {
        if (_frameStatistics.Count == 0)
        {
            return new TextureBindStatistics();
        }

        var bindCounts = _frameStatistics.Select(s => s.TextureBindChanges).ToList();
        return new TextureBindStatistics
        {
            AverageBindChanges = bindCounts.Count > 0 ? bindCounts.Average() : 0,
            MaxBindChanges = bindCounts.Max(),
            MinBindChanges = bindCounts.Min(),
            TotalBindChanges = bindCounts.Sum()
        };
    }

    private LookupStatistics CalculateLookupStatistics()
    {
        if (_frameStatistics.Count == 0)
        {
            return new LookupStatistics();
        }

        var lookupCounts = _frameStatistics.Select(s => s.UniqueLookups).ToList();
        return new LookupStatistics
        {
            AverageLookups = lookupCounts.Count > 0 ? lookupCounts.Average() : 0,
            MaxLookups = lookupCounts.Max(),
            MinLookups = lookupCounts.Min(),
            TotalLookups = lookupCounts.Sum()
        };
    }

    private MemoryStatistics CalculateMemoryStatistics()
    {
        if (_frameStatistics.Count == 0)
        {
            return new MemoryStatistics();
        }

        var memoryUsage = _frameStatistics
            .Select(s => s.AtlasStats?.TextureDetails.Sum(t => (long)t.EstimatedMemoryKB * 1024) ?? 0)
            .ToList();

        return new MemoryStatistics
        {
            AverageMemory = memoryUsage.Count > 0 ? memoryUsage.Average() : 0,
            MaxMemory = memoryUsage.Max(),
            MinMemory = memoryUsage.Min()
        };
    }

    private List<string> GenerateOptimizationRecommendations()
    {
        var recommendations = new List<string>();

        if (_frameStatistics.Count == 0)
        {
            return recommendations;
        }

        var avgBindChanges = _frameStatistics.Average(s => s.TextureBindChanges);
        var avgLookups = _frameStatistics.Average(s => s.UniqueLookups);

        if (avgBindChanges > 20)
        {
            recommendations.Add(
                $"HIGH: Average {avgBindChanges:F1} texture bind changes per frame detected. " +
                "Consider batch sorting optimization.");
        }

        if (avgLookups > 1000)
        {
            recommendations.Add(
                $"MEDIUM: Average {avgLookups:F0} unique lookups per frame. " +
                "Consider expanding image cache.");
        }

        var lastStats = _frameStatistics.Last();
        if (lastStats.AtlasStats?.UniqueTextures > 10)
        {
            recommendations.Add(
                $"MEDIUM: Using {lastStats.AtlasStats.UniqueTextures} unique textures. " +
                "Consider consolidating related textures into atlases.");
        }

        if (recommendations.Count == 0)
        {
            recommendations.Add("✓ Rendering is well-optimized. No major bottlenecks detected.");
        }

        return recommendations;
    }

    /// <summary>
    /// Resets all benchmark data for a new benchmarking session.
    /// </summary>
    public void Reset()
    {
        _frameStatistics.Clear();
        _frameTimes.Clear();
        _frameCount = 0;
        _totalFrameTime = 0;
        _frameTimer.Reset();
    }
}

/// <summary>
/// Complete benchmark report containing frame timing, binding statistics, and recommendations.
/// </summary>
public class BenchmarkReport
{
    /// <summary>
    /// Total number of frames profiled.
    /// </summary>
    public int FrameCount { get; set; }

    /// <summary>
    /// Average frame time in milliseconds.
    /// </summary>
    public double AverageFrameTime { get; set; }

    /// <summary>
    /// Minimum frame time recorded (in milliseconds).
    /// </summary>
    public long MinFrameTime { get; set; }

    /// <summary>
    /// Maximum frame time recorded (in milliseconds).
    /// </summary>
    public long MaxFrameTime { get; set; }

    /// <summary>
    /// Texture binding statistics.
    /// </summary>
    public TextureBindStatistics TextureBindStats { get; set; } = new();

    /// <summary>
    /// Image lookup statistics.
    /// </summary>
    public LookupStatistics LookupStats { get; set; } = new();

    /// <summary>
    /// Memory usage statistics.
    /// </summary>
    public MemoryStatistics MemoryStats { get; set; } = new();

    /// <summary>
    /// Generated optimization recommendations.
    /// </summary>
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Formats the report as a human-readable string.
    /// </summary>
    public override string ToString()
    {
        var lines = new List<string>
        {
            "=== Texture Atlasing Benchmark Report ===",
            "",
            "Frame Statistics:",
            $"  Total Frames: {FrameCount}",
            $"  Average Frame Time: {AverageFrameTime:F2} ms",
            $"  Min Frame Time: {MinFrameTime} ms",
            $"  Max Frame Time: {MaxFrameTime} ms",
            "",
            "Texture Binding Statistics:",
            $"  Average Bind Changes: {TextureBindStats.AverageBindChanges:F1} per frame",
            $"  Max Bind Changes: {TextureBindStats.MaxBindChanges}",
            $"  Min Bind Changes: {TextureBindStats.MinBindChanges}",
            $"  Total Bind Changes: {TextureBindStats.TotalBindChanges}",
            "",
            "Lookup Statistics:",
            $"  Average Lookups: {LookupStats.AverageLookups:F0} per frame",
            $"  Max Lookups: {LookupStats.MaxLookups}",
            $"  Min Lookups: {LookupStats.MinLookups}",
            "",
            "Memory Statistics:",
            $"  Average Memory: {MemoryStats.AverageMemory:F0} bytes",
            $"  Max Memory: {MemoryStats.MaxMemory} bytes",
            $"  Min Memory: {MemoryStats.MinMemory} bytes",
            "",
            "Recommendations:",
        };

        foreach (var rec in Recommendations)
        {
            lines.Add($"  • {rec}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}

/// <summary>
/// Texture binding statistics collected during benchmarking.
/// </summary>
public class TextureBindStatistics
{
    public double AverageBindChanges { get; set; }
    public int MaxBindChanges { get; set; }
    public int MinBindChanges { get; set; }
    public int TotalBindChanges { get; set; }
}

/// <summary>
/// Image lookup statistics collected during benchmarking.
/// </summary>
public class LookupStatistics
{
    public double AverageLookups { get; set; }
    public int MaxLookups { get; set; }
    public int MinLookups { get; set; }
    public int TotalLookups { get; set; }
}

/// <summary>
/// Memory usage statistics collected during benchmarking.
/// </summary>
public class MemoryStatistics
{
    public double AverageMemory { get; set; }
    public long MaxMemory { get; set; }
    public long MinMemory { get; set; }
}
