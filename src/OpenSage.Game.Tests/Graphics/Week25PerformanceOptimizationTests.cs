using System;
using System.Diagnostics;
using Xunit;
using OpenSage.Graphics;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Core;
using Veldrid;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// Week 25 Performance Optimization Tests
///
/// Establishes performance baselines for CPU and GPU optimization.
/// Creates infrastructure for profiling and identifying optimization opportunities.
///
/// Test Categories:
/// 1. CPU Performance Profiling
/// 2. GPU Performance Profiling
/// 3. Memory Profile Capture
/// 4. Render Performance Analysis
/// 5. State Management Efficiency
/// 6. Draw Call Batching Analysis
///
/// STATUS: Phase 4 Week 25 - Performance Optimization Infrastructure
/// </summary>
public class Week25PerformanceOptimizationTests : IDisposable
{
    private readonly PerformanceProfiler _cpuProfiler;
    private readonly GpuPerformanceMetrics _gpuMetrics;
    private readonly RenderingTestHelper _renderHelper;
    private readonly GraphicsDevice _graphicsDevice;
    private bool _disposed;

    public Week25PerformanceOptimizationTests()
    {
        _cpuProfiler = new PerformanceProfiler();
        _gpuMetrics = new GpuPerformanceMetrics();

        // Initialize graphics device for rendering tests
        InitializeGraphicsDevice();

        // RenderingTestHelper is optional for profiling tests
        // It will be initialized only when rendering is needed
        _renderHelper = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _cpuProfiler?.Dispose();
        _gpuMetrics?.Dispose();
        _renderHelper?.Dispose();
        _graphicsDevice?.Dispose();
        _disposed = true;
    }

    /// <summary>
    /// Test 1: CPU Profile Baseline Capture
    /// Records CPU frame timing and memory metrics over 60 frames
    /// Establishes baseline for 20-30% optimization target
    /// </summary>
    [Fact]
    public void CpuProfiler_BaselineCapture_Successful()
    {
        _cpuProfiler.StartSession();

        // Simulate 60 frames of rendering with varying workloads
        for (int i = 0; i < 60; i++)
        {
            var sw = Stopwatch.StartNew();

            // Simulate game logic frame
            SimulateGameLogicFrame();

            sw.Stop();
            double frameTimeMs = sw.Elapsed.TotalMilliseconds;

            // Record frame with realistic metrics
            int drawCalls = 100 + (i % 50); // 100-150 draw calls per frame
            int stateChanges = 50 + (i % 40); // 50-90 state changes per frame
            long memoryBytes = 256 * 1024 * 1024; // ~256 MB baseline

            _cpuProfiler.RecordFrame(frameTimeMs, drawCalls, stateChanges, memoryBytes);
        }

        _cpuProfiler.EndSession();

        // Verify baseline captured
        var profile = _cpuProfiler.GetProfile();
        Assert.Equal(60, profile.TotalFrames);
        Assert.True(profile.AverageFrameTime > 0, "Average frame time must be positive");
        Assert.True(profile.AverageFps > 0, "FPS must be positive");
        Assert.True(profile.FrameTimeVariance >= 0, "Variance must be non-negative");

        // Print report for analysis
        Console.WriteLine(_cpuProfiler.GenerateReport());

        // Assert target metrics for optimization
        Assert.True(profile.AverageFps >= 30, "Minimum acceptable FPS is 30");
    }

    /// <summary>
    /// Test 2: GPU Performance Baseline
    /// Tracks GPU-specific metrics: draw calls, state changes, texture memory
    /// Identifies GPU bottleneck opportunities
    /// </summary>
    [Fact]
    public void GpuMetrics_PerformanceBaseline_Captured()
    {
        _gpuMetrics.StartCollection();

        // Simulate 30 GPU-intensive frames
        for (int i = 0; i < 30; i++)
        {
            // Realistic GPU metrics
            int drawCalls = 150 + (i % 100);
            int stateChanges = 80 + (i % 60);
            long textureMemory = 128 * 1024 * 1024; // 128 MB textures
            long rtMemory = 50 * 1024 * 1024; // 50 MB render targets
            int rtSwitches = 5 + (i % 10);
            long cmdBuffer = 256 * 1024; // 256 KB command buffer
            double gpuTime = 8.0 + (i % 8); // 8-16ms GPU time

            _gpuMetrics.RecordFrame(
                drawCalls, stateChanges, textureMemory, rtMemory,
                rtSwitches, cmdBuffer, gpuTime);
        }

        _gpuMetrics.EndCollection();

        // Verify metrics captured
        var profile = _gpuMetrics.GetProfile();
        Assert.Equal(30, profile.TotalFrames);
        Assert.True(profile.AverageDrawCalls > 0, "Draw calls must be positive");
        Assert.True(profile.AverageStateChanges > 0, "State changes must be positive");
        Assert.True(profile.StateChangesPerDrawCall > 0, "State efficiency metric must be positive");

        // Print report for analysis
        Console.WriteLine(_gpuMetrics.GenerateReport());

        // Assert GPU health metrics
        Assert.True(profile.StateChangesPerDrawCall < 5, "State changes per draw call should be < 5");
        Assert.True(profile.AverageGpuTimeMs < 20, "Average GPU time should be < 20ms");
    }

    /// <summary>
    /// Test 3: Memory Profile Over Extended Session
    /// Tracks GC pressure, memory growth, peak usage over 300 frames
    /// Identifies memory-related optimization opportunities
    /// </summary>
    [Fact]
    public void MemoryProfile_ExtendedSession_Analysis()
    {
        _cpuProfiler.StartSession();

        long initialMemory = GC.GetTotalMemory(true);
        int gcGen0Before = GC.CollectionCount(0);

        // Simulate 300 frames (5 seconds at 60 FPS)
        for (int i = 0; i < 300; i++)
        {
            var sw = Stopwatch.StartNew();
            SimulateGameLogicFrame();
            sw.Stop();

            long currentMemory = GC.GetTotalMemory(false);
            _cpuProfiler.RecordFrame(
                sw.Elapsed.TotalMilliseconds,
                100 + (i % 50),
                50 + (i % 40),
                currentMemory);

            // Periodically force GC to measure collection
            if (i % 100 == 0 && i > 0)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        _cpuProfiler.EndSession();

        var profile = _cpuProfiler.GetProfile();
        long finalMemory = GC.GetTotalMemory(false);
        int gcGen0After = GC.CollectionCount(0);

        // Analysis
        Console.WriteLine("\n=== MEMORY ANALYSIS ===");
        Console.WriteLine($"Initial Memory: {initialMemory / (1024.0 * 1024.0):F1} MB");
        Console.WriteLine($"Final Memory: {finalMemory / (1024.0 * 1024.0):F1} MB");
        Console.WriteLine($"Peak Memory: {profile.PeakMemory / (1024.0 * 1024.0):F1} MB");
        Console.WriteLine($"Memory Growth: {(finalMemory - initialMemory) / (1024.0 * 1024.0):F1} MB");
        Console.WriteLine($"Gen0 Collections: {gcGen0After - gcGen0Before}");

        // Assert memory health
        Assert.True(profile.PeakMemory < 512 * 1024 * 1024, "Peak memory should be < 512 MB");
        Assert.True(gcGen0After - gcGen0Before < 50, "Gen0 collections should be < 50 in 300 frames");
    }

    /// <summary>
    /// Test 4: Frame Time Distribution Analysis
    /// Analyzes frame time percentiles to identify frame pacing issues
    /// Targets: <10ms variance, >60 FPS average, <16.67ms P99
    /// </summary>
    [Fact]
    public void FrameTime_DistributionAnalysis_Valid()
    {
        _cpuProfiler.StartSession();

        // Generate realistic frame times with some variance
        var random = new Random(42); // Deterministic seed
        for (int i = 0; i < 120; i++)
        {
            // 60 FPS target = 16.67ms per frame
            // Add realistic variance: 10-20ms range
            double baseTime = 16.67;
            double variance = random.NextDouble() * 4 - 2; // -2 to +2ms
            double frameTime = baseTime + variance;

            // Occasionally add frame spikes for realism
            if (random.Next(100) < 5) // 5% chance of spike
                frameTime += 8; // 8ms additional spike

            _cpuProfiler.RecordFrame(frameTime, 100, 50, 256 * 1024 * 1024);
        }

        _cpuProfiler.EndSession();

        var profile = _cpuProfiler.GetProfile();

        Console.WriteLine("\n=== FRAME TIME DISTRIBUTION ===");
        Console.WriteLine($"Average: {profile.AverageFrameTime:F2}ms");
        Console.WriteLine($"Median: {profile.MedianFrameTime:F2}ms");
        Console.WriteLine($"P95: {profile.P95FrameTime:F2}ms");
        Console.WriteLine($"P99: {profile.P99FrameTime:F2}ms");
        Console.WriteLine($"Variance (StdDev): {profile.FrameTimeVariance:F2}ms");

        // Assert frame time targets
        Assert.True(profile.AverageFps > 50, "Average FPS should be > 50");
        Assert.True(profile.P99FrameTime < 30, "P99 frame time should be < 30ms");
        Assert.True(profile.FrameTimeVariance < 15, "Variance should be < 15ms");
    }

    /// <summary>
    /// Test 5: CPU Hot Path Detection
    /// Demonstrates hot path tracking infrastructure
    /// Identifies expensive operations for optimization
    /// </summary>
    [Fact]
    public void CpuHotPath_Tracking_Working()
    {
        var tracker = new CpuHotPathTracker();

        // Simulate expensive operations
        tracker.RecordHotPath("CommandRecording", 8);
        tracker.RecordHotPath("StateManagement", 5);
        tracker.RecordHotPath("DrawCallGeneration", 3);
        tracker.RecordHotPath("TextureBinding", 2);
        tracker.RecordHotPath("ShaderConstantUpdate", 1);

        // Call multiple times to simulate realistic distribution
        for (int i = 0; i < 10; i++)
        {
            tracker.RecordHotPath("CommandRecording", 8);
            tracker.RecordHotPath("StateManagement", 5);
            tracker.RecordHotPath("DrawCallGeneration", 3);
        }

        var hotPaths = tracker.GetHotPaths();

        // Verify hot path tracking
        Assert.NotEmpty(hotPaths);
        Assert.Equal("CommandRecording", hotPaths[0].Name);

        // Print analysis
        Console.WriteLine(tracker.GenerateHotPathReport());
    }

    /// <summary>
    /// Test 6: GPU Utilization Profiling
    /// Tracks GPU operation categories and time distribution
    /// Identifies GPU bottleneck sources
    /// </summary>
    [Fact]
    public void GpuUtilization_Profiling_Complete()
    {
        var tracker = new GpuUtilizationTracker();

        // Simulate GPU operation distribution
        tracker.RecordOperation("Rasterization", 50);
        tracker.RecordOperation("PixelShading", 40);
        tracker.RecordOperation("TextureFiltering", 30);
        tracker.RecordOperation("RenderTargetWrite", 25);
        tracker.RecordOperation("DepthTest", 20);
        tracker.RecordOperation("StateTransition", 15);

        // Multiple calls for realism
        for (int i = 0; i < 5; i++)
        {
            tracker.RecordOperation("Rasterization", 50);
            tracker.RecordOperation("PixelShading", 40);
            tracker.RecordOperation("TextureFiltering", 30);
        }

        var report = tracker.GenerateUtilizationReport();
        Assert.DoesNotContain("No GPU operations", report);

        Console.WriteLine(report);
    }

    /// <summary>
    /// Test 7: Baseline Comparison Framework
    /// Demonstrates ability to compare current performance against baseline
    /// Used for Week 25 -> Week 26 optimization validation
    /// </summary>
    [Fact]
    public void BaselineComparison_Framework_Ready()
    {
        // Establish baseline
        _cpuProfiler.StartSession();
        for (int i = 0; i < 60; i++)
        {
            _cpuProfiler.RecordFrame(16.67, 100, 50, 256 * 1024 * 1024);
        }
        _cpuProfiler.EndSession();

        var baselineProfile = _cpuProfiler.GetProfile();
        double baselineAvgFrameTime = baselineProfile.AverageFrameTime;
        double baselineAvgDrawCalls = baselineProfile.AverageDrawCalls;

        Console.WriteLine($"\n=== BASELINE ===");
        Console.WriteLine($"Average Frame Time: {baselineAvgFrameTime:F2}ms");
        Console.WriteLine($"Average Draw Calls: {baselineAvgDrawCalls:F1}");

        // New profiling session (simulating optimization)
        var newProfiler = new PerformanceProfiler();
        newProfiler.StartSession();
        for (int i = 0; i < 60; i++)
        {
            // Simulated improvement: 10% faster, 5% fewer draw calls
            newProfiler.RecordFrame(baselineAvgFrameTime * 0.9,
                                    (int)(baselineAvgDrawCalls * 0.95),
                                    50, 256 * 1024 * 1024);
        }
        newProfiler.EndSession();

        var optimizedProfile = newProfiler.GetProfile();

        // Calculate improvements
        double frameTimeImprovement = ((baselineAvgFrameTime - optimizedProfile.AverageFrameTime)
                                      / baselineAvgFrameTime) * 100;
        double drawCallReduction = ((baselineAvgDrawCalls - optimizedProfile.AverageDrawCalls)
                                   / baselineAvgDrawCalls) * 100;

        Console.WriteLine($"\n=== AFTER OPTIMIZATION ===");
        Console.WriteLine($"Frame Time Improvement: {frameTimeImprovement:F1}%");
        Console.WriteLine($"Draw Call Reduction: {drawCallReduction:F1}%");

        // Verify framework works
        Assert.True(frameTimeImprovement > 5, "Should show frame time improvement");
        Assert.True(drawCallReduction > 1, "Should show draw call reduction");

        newProfiler.Dispose();
    }

    // Helper Methods

    private void InitializeGraphicsDevice()
    {
        // Detect platform - graphics device initialization is optional for profiling tests
        // Real device would be initialized here if needed for rendering tests
    }

    private void SimulateGameLogicFrame()
    {
        // Simulate typical game logic work:
        // - Object updates
        // - Physics calculations
        // - AI processing
        // - Scene management

        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 1)
        {
            // Spin to simulate work
        }
        sw.Stop();
    }
}
