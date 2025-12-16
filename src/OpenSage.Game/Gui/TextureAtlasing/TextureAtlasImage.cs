using OpenSage.Content;
using OpenSage.Mathematics;

namespace OpenSage.Gui.TextureAtlasing;

/// <summary>
/// Represents a normalized texture atlas image with UV coordinates.
/// Extends MappedImage with normalized UV coordinate support.
/// </summary>
public sealed class TextureAtlasImage
{
    private readonly MappedImage _mappedImage;
    private RectangleF? _cachedNormalizedUv;

    public string Name => _mappedImage.Name;

    public MappedImage Source => _mappedImage;

    public Size TextureDimensions => _mappedImage.TextureDimensions;

    public Rectangle PixelCoords => _mappedImage.Coords;

    public Size PixelSize => new Size(_mappedImage.Coords.Width, _mappedImage.Coords.Height);

    public MappedImageStatus Status => _mappedImage.Status;

    /// <summary>
    /// Gets the normalized UV coordinates (0.0 to 1.0 range).
    /// </summary>
    public RectangleF NormalizedUV
    {
        get
        {
            if (_cachedNormalizedUv.HasValue)
            {
                return _cachedNormalizedUv.Value;
            }

            var pixelCoords = PixelCoords;
            var textureDims = TextureDimensions;

            float left = (float)pixelCoords.Left / textureDims.Width;
            float top = (float)pixelCoords.Top / textureDims.Height;
            float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;
            float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height;

            _cachedNormalizedUv = new RectangleF(left, top, right - left, bottom - top);
            return _cachedNormalizedUv.Value;
        }
    }

    public TextureAtlasImage(MappedImage mappedImage)
    {
        _mappedImage = mappedImage;
    }

    /// <summary>
    /// Gets the effective pixel size, accounting for rotation.
    /// </summary>
    public Size GetEffectiveSize()
    {
        if (Status == MappedImageStatus.Rotated90Clockwise)
        {
            // When rotated 90 degrees, width and height are swapped
            return new Size(PixelSize.Height, PixelSize.Width);
        }

        return PixelSize;
    }

    /// <summary>
    /// Validates the atlas image configuration.
    /// </summary>
    public bool Validate()
    {
        if (TextureDimensions.Width <= 0 || TextureDimensions.Height <= 0)
        {
            return false;
        }

        if (PixelCoords.Width <= 0 || PixelCoords.Height <= 0)
        {
            return false;
        }

        if (PixelCoords.Left < 0 || PixelCoords.Top < 0)
        {
            return false;
        }

        if (PixelCoords.Right > TextureDimensions.Width || PixelCoords.Bottom > TextureDimensions.Height)
        {
            return false;
        }

        return true;
    }
}
