using System;
using System.IO;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using OpenSage.Graphics;
using Veldrid;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// Baseline capture utility for establishing reference images and performance metrics
/// Used by Week 24 regression testing framework
/// 
/// Captures:
/// - Visual baselines (render target screenshots)
/// - Performance metrics (frame timing, memory usage)
/// - Device capabilities (for regression detection)
/// </summary>
public sealed class BaselineCapture : IDisposable
{
    private readonly string _baselineDirectory;
    private readonly RenderingTestHelper _renderHelper;
    private readonly Stopwatch _timer;
    private bool _disposed;

    /// <summary>
    /// Directory where baseline files are stored
    /// </summary>
    public string BaselineDirectory => _baselineDirectory;

    /// <summary>
    /// Identifier for this baseline run (usually date/timestamp)
    /// </summary>
    public string BaselineId { get; }

    /// <summary>
    /// Number of baselines captured so far
    /// </summary>
    public int CaptureCount { get; private set; }

    public BaselineCapture(RenderingTestHelper renderHelper, string baselineDirectory)
    {
        if (renderHelper == null)
            throw new ArgumentNullException(nameof(renderHelper));
        if (string.IsNullOrWhiteSpace(baselineDirectory))
            throw new ArgumentException("Baseline directory cannot be null or empty", nameof(baselineDirectory));

        _renderHelper = renderHelper;
        _baselineDirectory = baselineDirectory;
        _timer = new Stopwatch();
        CaptureCount = 0;

        // Create baseline ID based on current date/time
        BaselineId = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        // Ensure baseline directory exists
        if (!Directory.Exists(_baselineDirectory))
        {
            Directory.CreateDirectory(_baselineDirectory);
        }
    }

    /// <summary>
    /// Captures a baseline image from the current render state
    /// Saves as PNG in the baseline directory
    /// </summary>
    /// <param name="testName">Name of the test for file naming</param>
    /// <returns>Full path to saved baseline image</returns>
    public string CaptureImage(string testName)
    {
        ThrowIfDisposed();

        if (string.IsNullOrWhiteSpace(testName))
            throw new ArgumentException("Test name cannot be null or empty", nameof(testName));

        // Render test pattern
        _renderHelper.RenderTestPattern();

        // Capture pixel data
        var pixelData = _renderHelper.CaptureRenderTarget();

        // Create baseline filename
        var filename = $"{testName}_{BaselineId}_baseline.bin";
        var filepath = Path.Combine(_baselineDirectory, filename);

        // Save pixel data as binary (easier than PNG for comparison)
        File.WriteAllBytes(filepath, pixelData);

        CaptureCount++;
        return filepath;
    }

    /// <summary>
    /// Captures performance baseline (frame timing metrics)
    /// Renders multiple frames and averages timing data
    /// </summary>
    /// <param name="testName">Name of the test</param>
    /// <param name="frameCount">Number of frames to average (default 30)</param>
    /// <returns>Captured performance metrics</returns>
    public BaselinePerformanceMetrics CapturePerformanceBaseline(string testName, int frameCount = 30)
    {
        ThrowIfDisposed();

        if (frameCount < 1 || frameCount > 1000)
            throw new ArgumentException("Frame count must be between 1 and 1000", nameof(frameCount));

        var frameTimes = new List<long>();

        // Warm up GPU
        for (int i = 0; i < 3; i++)
        {
            _renderHelper.RenderTestPattern();
        }

        // Capture frame timing
        for (int i = 0; i < frameCount; i++)
        {
            _timer.Restart();
            _renderHelper.RenderTestPattern();
            _timer.Stop();
            
            frameTimes.Add(_timer.ElapsedMilliseconds);
        }

        // Calculate metrics
        var metrics = CalculateMetrics(frameTimes);

        // Save metrics to JSON file
        var metricsFilename = $"{testName}_{BaselineId}_metrics.json";
        var metricsPath = Path.Combine(_baselineDirectory, metricsFilename);
        SaveMetricsToFile(metrics, metricsPath);

        CaptureCount++;
        return metrics;
    }

    /// <summary>
    /// Captures device capabilities and features
    /// Used to detect hardware regressions or incompatibilities
    /// </summary>
    /// <param name="testName">Name of the test</param>
    /// <returns>Device capabilities snapshot</returns>
    public DeviceCapabilities CaptureDeviceCapabilities(string testName)
    {
        ThrowIfDisposed();

        var capabilities = new DeviceCapabilities
        {
            CaptureTimestamp = DateTime.UtcNow,
            RenderResolution = $"{_renderHelper.RenderWidth}x{_renderHelper.RenderHeight}",
            GraphicsBackend = _renderHelper.GetVeldridDevice().BackendType.ToString(),
            VendorName = _renderHelper.GetVeldridDevice().VendorName ?? "Unknown",
            DeviceName = _renderHelper.GetVeldridDevice().DeviceName ?? "Unknown",
            ApiVersion = _renderHelper.GetVeldridDevice().ApiVersion.ToString() ?? "Unknown"
        };

        // Save capabilities to JSON
        var capsFilename = $"{testName}_{BaselineId}_capabilities.json";
        var capsPath = Path.Combine(_baselineDirectory, capsFilename);
        SaveCapabilitiesToFile(capabilities, capsPath);

        return capabilities;
    }

    /// <summary>
    /// Lists all baseline files in the baseline directory
    /// </summary>
    public IEnumerable<string> EnumerateBaselines()
    {
        ThrowIfDisposed();

        if (!Directory.Exists(_baselineDirectory))
            return new string[0];

        return Directory.GetFiles(_baselineDirectory, "*.bin");
    }

    /// <summary>
    /// Gets all metrics files for a specific test
    /// </summary>
    public IEnumerable<string> GetMetricsFiles(string testName)
    {
        ThrowIfDisposed();

        if (!Directory.Exists(_baselineDirectory))
            return new string[0];

        var pattern = $"{testName}_*_metrics.json";
        return Directory.GetFiles(_baselineDirectory, pattern);
    }

    private BaselinePerformanceMetrics CalculateMetrics(List<long> frameTimes)
    {
        if (frameTimes.Count == 0)
            return new BaselinePerformanceMetrics();

        long totalTime = 0;
        long minTime = long.MaxValue;
        long maxTime = long.MinValue;

        foreach (var time in frameTimes)
        {
            totalTime += time;
            minTime = Math.Min(minTime, time);
            maxTime = Math.Max(maxTime, time);
        }

        var avgTime = (double)totalTime / frameTimes.Count;
        var fps = avgTime > 0 ? 1000.0 / avgTime : 0;

        return new BaselinePerformanceMetrics
        {
            AverageFrameTime = avgTime,
            MinFrameTime = minTime,
            MaxFrameTime = maxTime,
            FramesPerSecond = fps,
            TotalFramesCaptured = frameTimes.Count,
            CaptureTimestamp = DateTime.UtcNow
        };
    }

    private void SaveMetricsToFile(BaselinePerformanceMetrics metrics, string filepath)
    {
        var json = JsonSerializer.Serialize(metrics, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filepath, json);
    }

    private void SaveCapabilitiesToFile(DeviceCapabilities capabilities, string filepath)
    {
        var json = JsonSerializer.Serialize(capabilities, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filepath, json);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _timer?.Stop();
        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name, "BaselineCapture has been disposed");
    }
}

/// <summary>
/// Captured performance metrics for a baseline
/// </summary>
public class BaselinePerformanceMetrics
{
    /// <summary>
    /// Average frame time in milliseconds
    /// </summary>
    public double AverageFrameTime { get; set; }

    /// <summary>
    /// Minimum frame time in milliseconds
    /// </summary>
    public long MinFrameTime { get; set; }

    /// <summary>
    /// Maximum frame time in milliseconds
    /// </summary>
    public long MaxFrameTime { get; set; }

    /// <summary>
    /// Frames per second calculated from average frame time
    /// </summary>
    public double FramesPerSecond { get; set; }

    /// <summary>
    /// Total number of frames captured for this metric
    /// </summary>
    public int TotalFramesCaptured { get; set; }

    /// <summary>
    /// When these metrics were captured (UTC)
    /// </summary>
    public DateTime CaptureTimestamp { get; set; }

    /// <summary>
    /// Detects if this metric represents a regression compared to another
    /// A regression is detected if frame time increased by more than threshold %
    /// </summary>
    public bool IsRegression(BaselinePerformanceMetrics other, double thresholdPercent = 10.0)
    {
        if (other?.AverageFrameTime <= 0)
            return false;

        var percentChange = ((AverageFrameTime - other.AverageFrameTime) / other.AverageFrameTime) * 100.0;
        return percentChange > thresholdPercent;
    }
}

/// <summary>
/// Device capabilities snapshot for regression detection
/// </summary>
public class DeviceCapabilities
{
    /// <summary>
    /// When capabilities were captured
    /// </summary>
    public DateTime CaptureTimestamp { get; set; }

    /// <summary>
    /// Resolution of render target (e.g., "1920x1080")
    /// </summary>
    public string RenderResolution { get; set; }

    /// <summary>
    /// Graphics backend name (e.g., "Vulkan", "Metal", "Direct3D11")
    /// </summary>
    public string GraphicsBackend { get; set; }

    /// <summary>
    /// GPU vendor name (e.g., "NVIDIA", "AMD", "Intel")
    /// </summary>
    public string VendorName { get; set; }

    /// <summary>
    /// GPU device name
    /// </summary>
    public string DeviceName { get; set; }

    /// <summary>
    /// Graphics API version
    /// </summary>
    public string ApiVersion { get; set; }

    /// <summary>
    /// Detects if this device matches another (for regression detection)
    /// </summary>
    public bool IsSameDevice(DeviceCapabilities other)
    {
        if (other == null)
            return false;

        return GraphicsBackend == other.GraphicsBackend &&
               VendorName == other.VendorName &&
               DeviceName == other.DeviceName;
    }
}
