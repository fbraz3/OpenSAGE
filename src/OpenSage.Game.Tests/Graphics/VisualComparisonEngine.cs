using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// Visual comparison utility for regression detection
/// Compares baseline and current frame images to detect visual regressions
/// 
/// Week 24: Regression Testing Framework
/// </summary>
public sealed class VisualComparisonEngine
{
    /// <summary>
    /// Comparison result indicating whether a regression was detected
    /// </summary>
    public class ComparisonResult
    {
        /// <summary>
        /// Whether this is a detected regression
        /// </summary>
        public bool IsRegression { get; set; }

        /// <summary>
        /// Percentage of pixels that differ (0-100)
        /// </summary>
        public double DifferencePercentage { get; set; }

        /// <summary>
        /// Number of differing pixels
        /// </summary>
        public int DifferingPixelCount { get; set; }

        /// <summary>
        /// Total number of pixels compared
        /// </summary>
        public int TotalPixelCount { get; set; }

        /// <summary>
        /// Maximum color difference observed (0-255 per channel)
        /// </summary>
        public int MaxColorDifference { get; set; }

        /// <summary>
        /// Average color difference across all pixels
        /// </summary>
        public double AvgColorDifference { get; set; }

        /// <summary>
        /// Detailed regions with largest differences
        /// </summary>
        public List<DifferenceRegion> TopDifferenceRegions { get; set; }

        public ComparisonResult()
        {
            TopDifferenceRegions = new List<DifferenceRegion>();
        }

        public override string ToString()
        {
            return $"Regression: {IsRegression}, Diff: {DifferencePercentage:F2}%, " +
                   $"Pixels: {DifferingPixelCount}/{TotalPixelCount}, " +
                   $"MaxDiff: {MaxColorDifference}, AvgDiff: {AvgColorDifference:F2}";
        }
    }

    /// <summary>
    /// Region with significant color differences
    /// </summary>
    public class DifferenceRegion
    {
        /// <summary>
        /// X coordinate of region center
        /// </summary>
        public int X { get; set; }

        /// <summary>
        /// Y coordinate of region center
        /// </summary>
        public int Y { get; set; }

        /// <summary>
        /// Width of region (pixels)
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of region (pixels)
        /// </summary>
        public int Height { get; set; }

        /// <summary>
        /// Average color difference in this region
        /// </summary>
        public double AvgDifference { get; set; }
    }

    private readonly uint _width;
    private readonly uint _height;
    private readonly double _regressionThreshold;

    /// <summary>
    /// Creates a visual comparison engine
    /// </summary>
    /// <param name="width">Image width in pixels</param>
    /// <param name="height">Image height in pixels</param>
    /// <param name="regressionThreshold">Percentage of differing pixels to trigger regression (default 5%)</param>
    public VisualComparisonEngine(uint width, uint height, double regressionThreshold = 5.0)
    {
        if (width == 0 || height == 0)
            throw new ArgumentException("Image dimensions must be > 0");
        if (regressionThreshold < 0 || regressionThreshold > 100)
            throw new ArgumentException("Regression threshold must be 0-100%");

        _width = width;
        _height = height;
        _regressionThreshold = regressionThreshold;
    }

    /// <summary>
    /// Compares two images pixel-by-pixel
    /// </summary>
    /// <param name="baselineData">Baseline image pixel data (RGBA8)</param>
    /// <param name="currentData">Current image pixel data (RGBA8)</param>
    /// <returns>Comparison result with detailed analysis</returns>
    public ComparisonResult Compare(byte[] baselineData, byte[] currentData)
    {
        if (baselineData == null)
            throw new ArgumentNullException(nameof(baselineData));
        if (currentData == null)
            throw new ArgumentNullException(nameof(currentData));

        var expectedSize = (int)(_width * _height * 4); // RGBA = 4 bytes per pixel
        if (baselineData.Length != expectedSize || currentData.Length != expectedSize)
            throw new ArgumentException($"Image data size mismatch. Expected {expectedSize} bytes");

        var result = new ComparisonResult
        {
            TotalPixelCount = (int)(_width * _height)
        };

        int differingPixels = 0;
        int totalDifference = 0;
        int maxDifference = 0;
        var differenceMap = new List<(int pixelIndex, int diff)>();

        // Compare pixels
        for (int i = 0; i < baselineData.Length; i += 4)
        {
            // Extract RGBA components
            int bR = baselineData[i];
            int bG = baselineData[i + 1];
            int bB = baselineData[i + 2];
            int bA = baselineData[i + 3];

            int cR = currentData[i];
            int cG = currentData[i + 1];
            int cB = currentData[i + 2];
            int cA = currentData[i + 3];

            // Calculate pixel difference (max of RGBA differences)
            int diffR = Math.Abs(bR - cR);
            int diffG = Math.Abs(bG - cG);
            int diffB = Math.Abs(bB - cB);
            int diffA = Math.Abs(bA - cA);

            int pixelDiff = Math.Max(Math.Max(diffR, diffG), Math.Max(diffB, diffA));

            if (pixelDiff > 0)
            {
                differingPixels++;
                differenceMap.Add((i / 4, pixelDiff));
                totalDifference += pixelDiff;
                maxDifference = Math.Max(maxDifference, pixelDiff);
            }
        }

        result.DifferingPixelCount = differingPixels;
        result.DifferencePercentage = (double)differingPixels / result.TotalPixelCount * 100.0;
        result.MaxColorDifference = maxDifference;
        result.AvgColorDifference = differingPixels > 0 ? (double)totalDifference / differingPixels : 0;
        result.IsRegression = result.DifferencePercentage > _regressionThreshold;

        // Find top difference regions
        if (differenceMap.Count > 0)
        {
            result.TopDifferenceRegions = FindDifferenceRegions(differenceMap);
        }

        return result;
    }

    /// <summary>
    /// Analyzes comparison results against previous baseline
    /// </summary>
    public string GenerateReport(ComparisonResult result)
    {
        var lines = new List<string>
        {
            "=== Visual Regression Comparison Report ===",
            $"Regression Detected: {result.IsRegression}",
            $"Threshold: {_regressionThreshold}%",
            "",
            "=== Pixel Differences ===",
            $"Differing Pixels: {result.DifferingPixelCount} / {result.TotalPixelCount}",
            $"Difference %: {result.DifferencePercentage:F4}%",
            $"Max Color Diff: {result.MaxColorDifference}",
            $"Avg Color Diff: {result.AvgColorDifference:F2}",
            ""
        };

        if (result.TopDifferenceRegions.Count > 0)
        {
            lines.Add("=== Top Difference Regions ===");
            foreach (var region in result.TopDifferenceRegions.Take(5))
            {
                lines.Add($"  Region at ({region.X}, {region.Y}): " +
                         $"{region.Width}x{region.Height} pixels, " +
                         $"Avg Diff: {region.AvgDifference:F2}");
            }
        }
        else
        {
            lines.Add("=== Analysis ===");
            lines.Add("Images are identical (no difference regions found)");
        }

        lines.Add("");
        lines.Add(result.IsRegression
            ? "⚠️  REGRESSION DETECTED - Visual changes exceed threshold"
            : "✅ No regression detected - images within tolerance");

        return string.Join(Environment.NewLine, lines);
    }

    private List<DifferenceRegion> FindDifferenceRegions(List<(int pixelIndex, int diff)> differences)
    {
        if (differences.Count == 0)
            return new List<DifferenceRegion>();

        // Sort by difference magnitude
        var sorted = differences.OrderByDescending(x => x.diff).ToList();

        // Group nearby pixels into regions (simple clustering)
        var regions = new List<DifferenceRegion>();
        var processed = new HashSet<int>();
        const int regionSize = 32; // 32x32 pixel regions

        foreach (var (pixelIndex, _) in sorted.Take(10)) // Top 10 differences
        {
            if (processed.Contains(pixelIndex))
                continue;

            // Convert linear index to 2D coordinates
            int y = pixelIndex / (int)_width;
            int x = pixelIndex % (int)_width;

            // Create region around this difference
            var region = new DifferenceRegion
            {
                X = x,
                Y = y,
                Width = Math.Min(regionSize, (int)_width - x),
                Height = Math.Min(regionSize, (int)_height - y),
                AvgDifference = differences.Where(d =>
                    d.pixelIndex / (int)_width >= y &&
                    d.pixelIndex / (int)_width < y + regionSize &&
                    d.pixelIndex % (int)_width >= x &&
                    d.pixelIndex % (int)_width < x + regionSize
                ).Average(d => d.diff)
            };

            regions.Add(region);

            // Mark pixels in this region as processed
            for (int py = y; py < Math.Min(y + regionSize, _height); py++)
            {
                for (int px = x; px < Math.Min(x + regionSize, _width); px++)
                {
                    processed.Add(py * (int)_width + px);
                }
            }
        }

        return regions.OrderByDescending(r => r.AvgDifference).ToList();
    }
}

/// <summary>
/// Comparison statistics tracker for multiple runs
/// </summary>
public class ComparisonStatistics
{
    /// <summary>
    /// Comparisons performed
    /// </summary>
    public List<VisualComparisonEngine.ComparisonResult> Results { get; }

    /// <summary>
    /// Average difference percentage across all comparisons
    /// </summary>
    public double AverageDifferencePercent =>
        Results.Count > 0 ? Results.Average(r => r.DifferencePercentage) : 0;

    /// <summary>
    /// Number of regressions detected
    /// </summary>
    public int RegressionCount => Results.Count(r => r.IsRegression);

    /// <summary>
    /// Pass rate (1.0 = all passed, 0.0 = all failed)
    /// </summary>
    public double PassRate => Results.Count > 0 ? 1.0 - (double)RegressionCount / Results.Count : 1.0;

    public ComparisonStatistics()
    {
        Results = new List<VisualComparisonEngine.ComparisonResult>();
    }

    public override string ToString()
    {
        return $"Comparisons: {Results.Count}, " +
               $"Regressions: {RegressionCount}, " +
               $"Pass Rate: {PassRate:P}, " +
               $"Avg Diff: {AverageDifferencePercent:F4}%";
    }
}
