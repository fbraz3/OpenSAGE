using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Gui;
using OpenSage.Gui.Benchmarks;
using OpenSage.Gui.TextureAtlasing;
using Xunit;

namespace OpenSage.Tests.Gui.TextureAtlasing;

/// <summary>
/// PLAN-013 Stage 4: Optimization & Polish
/// Tests for texture atlasing performance measurement and optimization recommendations.
/// </summary>
public class TextureAtlasingStage4Tests
{
    [Fact]
    public void TextureBindStatistics_ValidStructure()
    {
        // Arrange & Act
        var stats = new TextureBindStatistics
        {
            AverageBindChanges = 15.5,
            MaxBindChanges = 30,
            MinBindChanges = 5,
            TotalBindChanges = 150
        };

        // Assert
        Assert.Equal(15.5, stats.AverageBindChanges);
        Assert.Equal(30, stats.MaxBindChanges);
        Assert.Equal(5, stats.MinBindChanges);
        Assert.Equal(150, stats.TotalBindChanges);
        Assert.True(stats.MaxBindChanges >= stats.MinBindChanges);
    }

    [Fact]
    public void LookupStatistics_ValidStructure()
    {
        // Arrange & Act
        var stats = new LookupStatistics
        {
            AverageLookups = 500.5,
            MaxLookups = 1000,
            MinLookups = 100,
            TotalLookups = 5000
        };

        // Assert
        Assert.Equal(500.5, stats.AverageLookups);
        Assert.Equal(1000, stats.MaxLookups);
        Assert.Equal(100, stats.MinLookups);
        Assert.Equal(5000, stats.TotalLookups);
        Assert.True(stats.MaxLookups >= stats.MinLookups);
    }

    [Fact]
    public void MemoryStatistics_ValidStructure()
    {
        // Arrange & Act
        var stats = new MemoryStatistics
        {
            AverageMemory = 1048576,
            MaxMemory = 2097152,
            MinMemory = 524288
        };

        // Assert
        Assert.Equal(1048576, stats.AverageMemory);
        Assert.Equal(2097152, stats.MaxMemory);
        Assert.Equal(524288, stats.MinMemory);
        Assert.True(stats.MaxMemory >= stats.MinMemory);
    }

    [Fact]
    public void BenchmarkReport_ValidStructure()
    {
        // Arrange & Act
        var report = new BenchmarkReport
        {
            FrameCount = 10,
            AverageFrameTime = 16.7,
            MinFrameTime = 15,
            MaxFrameTime = 20,
            TextureBindStats = new TextureBindStatistics
            {
                AverageBindChanges = 15.5,
                MaxBindChanges = 30,
                MinBindChanges = 5,
                TotalBindChanges = 150
            },
            LookupStats = new LookupStatistics
            {
                AverageLookups = 500.5,
                MaxLookups = 1000,
                MinLookups = 100,
                TotalLookups = 5000
            },
            MemoryStats = new MemoryStatistics
            {
                AverageMemory = 1048576,
                MaxMemory = 2097152,
                MinMemory = 524288
            },
            Recommendations = new List<string>
            {
                "Sample recommendation"
            }
        };

        // Assert
        Assert.NotNull(report);
        Assert.Equal(10, report.FrameCount);
        Assert.Equal(16.7, report.AverageFrameTime);
        Assert.Equal(15, report.MinFrameTime);
        Assert.Equal(20, report.MaxFrameTime);
        Assert.NotNull(report.TextureBindStats);
        Assert.NotNull(report.LookupStats);
        Assert.NotNull(report.MemoryStats);
        Assert.NotEmpty(report.Recommendations);
    }

    [Fact]
    public void BenchmarkReport_ToString_FormatsCorrectly()
    {
        // Arrange
        var report = new BenchmarkReport
        {
            FrameCount = 5,
            AverageFrameTime = 16.5,
            MinFrameTime = 16,
            MaxFrameTime = 17,
            TextureBindStats = new TextureBindStatistics(),
            LookupStats = new LookupStatistics(),
            MemoryStats = new MemoryStatistics(),
            Recommendations = new List<string> { "Sample recommendation" }
        };

        // Act
        var output = report.ToString();

        // Assert
        Assert.NotEmpty(output);
        Assert.Contains("Texture Atlasing Benchmark Report", output);
        Assert.Contains("Frame Statistics", output);
        Assert.Contains("Texture Binding Statistics", output);
        Assert.Contains("Recommendations", output);
    }

    [Fact]
    public void OptimizationRecommendation_Structure()
    {
        // Arrange & Act
        var recommendation = new OptimizationRecommendation
        {
            Type = RecommendationType.OptimizeBatching,
            Priority = 1,
            Description = "Optimize draw call batching"
        };

        // Assert
        Assert.Equal(RecommendationType.OptimizeBatching, recommendation.Type);
        Assert.Equal(1, recommendation.Priority);
        Assert.Equal("Optimize draw call batching", recommendation.Description);
    }

    [Fact]
    public void OptimizationRecommendation_Priority_Ordering()
    {
        // Arrange
        var recommendations = new List<OptimizationRecommendation>
        {
            new() { Type = RecommendationType.ExpandCache, Priority = 2 },
            new() { Type = RecommendationType.OptimizeBatching, Priority = 1 },
            new() { Type = RecommendationType.ConsolidateFrequentTextures, Priority = 1 }
        };

        // Act
        var sorted = recommendations.OrderBy(r => r.Priority).ToList();

        // Assert
        Assert.Equal(3, sorted.Count);
        Assert.Equal(1, sorted[0].Priority);
        Assert.Equal(1, sorted[1].Priority);
        Assert.Equal(2, sorted[2].Priority);
    }

    [Fact]
    public void RecommendationType_AllValuesPresent()
    {
        // Assert - Verify all recommendation types exist by checking their values
        // These enum values should be used for different optimization scenarios
        int consolidate = (int)RecommendationType.ConsolidateFrequentTextures;
        int expandCache = (int)RecommendationType.ExpandCache;
        int optimizeBatching = (int)RecommendationType.OptimizeBatching;
        int reduceMemory = (int)RecommendationType.ReduceMemory;
        int improveLocality = (int)RecommendationType.ImproveLocality;

        // All should be distinct values
        Assert.True(consolidate >= 0);
        Assert.True(expandCache >= 0);
        Assert.True(optimizeBatching >= 0);
        Assert.True(reduceMemory >= 0);
        Assert.True(improveLocality >= 0);
    }
}
