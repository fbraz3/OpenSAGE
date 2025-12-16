#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Content;
using OpenSage.Graphics;
using OpenSage.Gui.TextureAtlasing;

namespace OpenSage.Gui;

/// <summary>
/// Integrates AtlasManager with DrawingContext2D to optimize texture binding.
/// Tracks MappedImage usage and can generate statistics for batching optimization.
/// </summary>
public sealed class MappedImageRenderOptimizer : DisposableBase
{
    private readonly AtlasManager _atlasManager;
    private readonly Dictionary<GuiTextureAsset, int> _textureDrawCallCounts;
    private readonly Dictionary<string, int> _imageLookupCounts;
    private bool _isProfilingEnabled;

    /// <summary>
    /// Gets the underlying atlas manager.
    /// </summary>
    public AtlasManager AtlasManager => _atlasManager;

    /// <summary>
    /// Gets whether profiling is currently enabled.
    /// </summary>
    public bool IsProfilingEnabled => _isProfilingEnabled;

    public MappedImageRenderOptimizer(ScopedAssetCollection<MappedImage> mappedImages)
    {
        _atlasManager = new AtlasManager(mappedImages);
        _textureDrawCallCounts = new Dictionary<GuiTextureAsset, int>();
        _imageLookupCounts = new Dictionary<string, int>();
    }

    /// <summary>
    /// Enables profiling to track rendering statistics.
    /// </summary>
    public void EnableProfiling()
    {
        _isProfilingEnabled = true;
        _textureDrawCallCounts.Clear();
        _imageLookupCounts.Clear();
    }

    /// <summary>
    /// Disables profiling and returns collected statistics.
    /// </summary>
    public RenderOptimizationStatistics DisableProfiling()
    {
        _isProfilingEnabled = false;

        var stats = new RenderOptimizationStatistics
        {
            AtlasStats = _atlasManager.GetStatistics(),
            TotalDrawCalls = _textureDrawCallCounts.Values.Sum(),
            TextureBindChanges = _textureDrawCallCounts.Count,
            UniqueLookups = _imageLookupCounts.Count,
            TextureSpecificDrawCalls = new Dictionary<GuiTextureAsset, int>(_textureDrawCallCounts),
            ImageLookupFrequency = new Dictionary<string, int>(_imageLookupCounts)
        };

        return stats;
    }

    /// <summary>
    /// Records a MappedImage lookup (for profiling).
    /// </summary>
    public void RecordImageLookup(string imageName)
    {
        if (!_isProfilingEnabled)
        {
            return;
        }

        if (!_imageLookupCounts.TryGetValue(imageName, out int count))
        {
            _imageLookupCounts[imageName] = 1;
        }
        else
        {
            _imageLookupCounts[imageName] = count + 1;
        }
    }

    /// <summary>
    /// Records a texture bind (for profiling).
    /// </summary>
    public void RecordTextureBinding(MappedImage? mappedImage)
    {
        if (!_isProfilingEnabled || mappedImage == null)
        {
            return;
        }

        var texture = mappedImage.Texture.Value;
        if (texture == null)
        {
            return;
        }

        if (!_textureDrawCallCounts.TryGetValue(texture, out int count))
        {
            _textureDrawCallCounts[texture] = 1;
        }
        else
        {
            _textureDrawCallCounts[texture] = count + 1;
        }
    }

    /// <summary>
    /// Gets recommended optimizations based on profiling data.
    /// </summary>
    public IEnumerable<OptimizationRecommendation> GetRecommendations()
    {
        if (!_isProfilingEnabled)
        {
            yield break;
        }

        // Recommendation 1: High-frequency textures should be in same atlas
        var frequentTextures = _textureDrawCallCounts
            .Where(kvp => kvp.Value > 100)
            .Select(kvp => kvp.Key)
            .ToList();

        if (frequentTextures.Count > 1)
        {
            yield return new OptimizationRecommendation
            {
                Type = RecommendationType.ConsolidateFrequentTextures,
                Priority = 1,
                Description = $"Consolidate {frequentTextures.Count} frequently-used textures into a single atlas"
            };
        }

        // Recommendation 2: Many unique lookups indicate poor cache hit rates
        if (_imageLookupCounts.Count > 1000)
        {
            yield return new OptimizationRecommendation
            {
                Type = RecommendationType.ExpandCache,
                Priority = 2,
                Description = "Large number of unique image lookups detected; consider expanding cache"
            };
        }

        // Recommendation 3: Many texture bind changes indicate batching opportunity
        if (_textureDrawCallCounts.Count > 20)
        {
            yield return new OptimizationRecommendation
            {
                Type = RecommendationType.OptimizeBatching,
                Priority = 1,
                Description = $"{_textureDrawCallCounts.Count} texture bind changes detected; implement batching"
            };
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _atlasManager.Dispose();
            _textureDrawCallCounts.Clear();
            _imageLookupCounts.Clear();
        }

        base.Dispose(disposing);
    }
}

/// <summary>
/// Statistics from render optimization profiling.
/// </summary>
public sealed class RenderOptimizationStatistics
{
    public required AtlasStatistics AtlasStats { get; init; }
    public int TotalDrawCalls { get; set; }
    public int TextureBindChanges { get; set; }
    public int UniqueLookups { get; set; }
    public Dictionary<GuiTextureAsset, int> TextureSpecificDrawCalls { get; set; } = new();
    public Dictionary<string, int> ImageLookupFrequency { get; set; } = new();
}

/// <summary>
/// An optimization recommendation from profiling.
/// </summary>
public sealed class OptimizationRecommendation
{
    public RecommendationType Type { get; set; }
    public int Priority { get; set; }
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Types of optimization recommendations.
/// </summary>
public enum RecommendationType
{
    ConsolidateFrequentTextures,
    ExpandCache,
    OptimizeBatching,
    ReduceMemory,
    ImproveLocality
}

