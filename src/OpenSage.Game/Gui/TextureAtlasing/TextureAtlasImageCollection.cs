#nullable enable

using System;
using System.Collections.Generic;
using OpenSage.Content;

namespace OpenSage.Gui.TextureAtlasing;

/// <summary>
/// Manages a collection of texture atlas images for efficient lookup and caching.
/// </summary>
public sealed class TextureAtlasImageCollection
{
    private readonly Dictionary<string, TextureAtlasImage> _images;
    private readonly ScopedAssetCollection<MappedImage> _mappedImages;

    public int Count => _images.Count;

    public TextureAtlasImageCollection(ScopedAssetCollection<MappedImage> mappedImages)
    {
        _mappedImages = mappedImages ?? throw new ArgumentNullException(nameof(mappedImages));
        _images = new Dictionary<string, TextureAtlasImage>(StringComparer.OrdinalIgnoreCase);
        BuildCollection();
    }

    /// <summary>
    /// Builds the atlas image collection from mapped images.
    /// </summary>
    private void BuildCollection()
    {
        _images.Clear();

        foreach (var mappedImage in _mappedImages)
        {
            var atlasImage = new TextureAtlasImage(mappedImage);

            if (!atlasImage.Validate())
            {
                System.Diagnostics.Debug.WriteLine($"Warning: Invalid atlas image '{mappedImage.Name}'");
                continue;
            }

            _images[mappedImage.Name] = atlasImage;
        }
    }

    /// <summary>
    /// Finds an atlas image by name (case-insensitive).
    /// </summary>
    public TextureAtlasImage? FindByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return _images.TryGetValue(name, out var image) ? image : null;
    }

    /// <summary>
    /// Tries to get an atlas image by name.
    /// </summary>
    public bool TryGetImage(string name, out TextureAtlasImage? image)
    {
        image = FindByName(name);
        return image != null;
    }

    /// <summary>
    /// Gets an atlas image by name, or throws if not found.
    /// </summary>
    public TextureAtlasImage GetImage(string name)
    {
        var image = FindByName(name);
        if (image == null)
        {
            throw new KeyNotFoundException($"Atlas image not found: '{name}'");
        }

        return image;
    }

    /// <summary>
    /// Gets all atlas images matching a pattern.
    /// </summary>
    public IEnumerable<TextureAtlasImage> FindByPattern(string pattern)
    {
        foreach (var (key, value) in _images)
        {
            if (key.Contains(pattern, StringComparison.OrdinalIgnoreCase))
            {
                yield return value;
            }
        }
    }

    /// <summary>
    /// Enumerates all atlas images in the collection.
    /// </summary>
    public IEnumerable<TextureAtlasImage> EnumerateAll()
    {
        return _images.Values;
    }
}
