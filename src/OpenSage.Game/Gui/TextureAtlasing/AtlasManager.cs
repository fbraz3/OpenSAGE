#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Content;
using OpenSage.Graphics;

namespace OpenSage.Gui.TextureAtlasing;

/// <summary>
/// Manages texture atlases and optimizes texture binding for UI rendering.
/// Tracks MappedImage usage patterns to reduce texture swaps during rendering.
/// </summary>
public sealed class AtlasManager : DisposableBase
{
    private readonly Dictionary<string, TextureAtlasImage> _atlasImageCache;
    private readonly Dictionary<GuiTextureAsset, AtlasInfo> _textureToAtlas;
    private readonly ScopedAssetCollection<MappedImage> _mappedImages;
    private bool _isDirty;

    /// <summary>
    /// Gets the total number of unique textures used by UI elements.
    /// </summary>
    public int UniqueTextureCount => _textureToAtlas.Count;

    /// <summary>
    /// Gets the number of mapped images in the collection.
    /// </summary>
    public int MappedImageCount => _atlasImageCache.Count;

    public AtlasManager(ScopedAssetCollection<MappedImage> mappedImages)
    {
        _mappedImages = mappedImages ?? throw new ArgumentNullException(nameof(mappedImages));
        _atlasImageCache = new Dictionary<string, TextureAtlasImage>(StringComparer.OrdinalIgnoreCase);
        _textureToAtlas = new Dictionary<GuiTextureAsset, AtlasInfo>();
        _isDirty = true;

        RebuildAtlas();
    }

    /// <summary>
    /// Gets a mapped image by name, with automatic cache update if dirty.
    /// </summary>
    public TextureAtlasImage? GetImage(string name)
    {
        if (_isDirty)
        {
            RebuildAtlas();
        }

        return _atlasImageCache.TryGetValue(name, out var image) ? image : null;
    }

    /// <summary>
    /// Gets all mapped images for a specific texture.
    /// Useful for batch rendering optimization.
    /// </summary>
    public IEnumerable<TextureAtlasImage> GetImagesForTexture(GuiTextureAsset texture)
    {
        if (!_textureToAtlas.TryGetValue(texture, out var atlasInfo))
        {
            return Enumerable.Empty<TextureAtlasImage>();
        }

        return atlasInfo.Images;
    }

    /// <summary>
    /// Gets statistics about texture usage (for profiling).
    /// </summary>
    public AtlasStatistics GetStatistics()
    {
        var stats = new AtlasStatistics
        {
            TotalMappedImages = _atlasImageCache.Count,
            UniqueTextures = _textureToAtlas.Count,
            TextureDetails = _textureToAtlas
                .Select(kvp => new TextureStatistic
                {
                    Texture = kvp.Key,
                    ImageCount = kvp.Value.Images.Count,
                    EstimatedMemoryKB = EstimateTextureMemory(kvp.Key)
                })
                .ToList()
        };

        return stats;
    }

    /// <summary>
    /// Marks the atlas as needing rebuild (call when MappedImages collection changes).
    /// </summary>
    public void Invalidate()
    {
        _isDirty = true;
    }

    /// <summary>
    /// Rebuilds the atlas cache from current MappedImages collection.
    /// </summary>
    private void RebuildAtlas()
    {
        _atlasImageCache.Clear();
        _textureToAtlas.Clear();

        foreach (var mappedImage in _mappedImages)
        {
            var atlasImage = new TextureAtlasImage(mappedImage);

            if (!atlasImage.Validate())
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Invalid atlas image '{mappedImage.Name}'");
                continue;
            }

            _atlasImageCache[mappedImage.Name] = atlasImage;

            // Track which texture this image belongs to
            var texture = mappedImage.Texture.Value;
            if (texture != null)
            {
                if (!_textureToAtlas.TryGetValue(texture, out var atlasInfo))
                {
                    atlasInfo = new AtlasInfo { Texture = texture, Images = new List<TextureAtlasImage>() };
                    _textureToAtlas[texture] = atlasInfo;
                }

                atlasInfo.Images.Add(atlasImage);
            }
        }

        _isDirty = false;
    }

    /// <summary>
    /// Estimates memory used by a texture (rough approximation).
    /// </summary>
    private static int EstimateTextureMemory(GuiTextureAsset guiTexture)
    {
        if (guiTexture?.Texture == null)
        {
            return 0;
        }

        var texture = guiTexture.Texture;
        // Rough estimation: width * height * 4 bytes (RGBA)
        int estimatedBytes = (int)(texture.Width * texture.Height * 4);
        return estimatedBytes / 1024; // Convert to KB
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _atlasImageCache.Clear();
            _textureToAtlas.Clear();
        }

        base.Dispose(disposing);
    }

    /// <summary>
    /// Internal structure tracking images for a specific texture.
    /// </summary>
    private sealed class AtlasInfo
    {
        public required GuiTextureAsset Texture { get; init; }
        public required List<TextureAtlasImage> Images { get; init; }
    }
}

/// <summary>
/// Statistics about atlas usage (for profiling and optimization).
/// </summary>
public sealed class AtlasStatistics
{
    public int TotalMappedImages { get; set; }
    public int UniqueTextures { get; set; }
    public List<TextureStatistic> TextureDetails { get; set; } = new();
}

/// <summary>
/// Per-texture statistics.
/// </summary>
public sealed class TextureStatistic
{
    public required GuiTextureAsset Texture { get; init; }
    public int ImageCount { get; set; }
    public int EstimatedMemoryKB { get; set; }
}

