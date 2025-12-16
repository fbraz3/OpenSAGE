#nullable enable

using OpenSage.Mathematics;

namespace OpenSage.Gui.TextureAtlasing;

/// <summary>
/// Extension methods for MappedImage to support texture atlasing operations.
/// </summary>
public static class MappedImageExtensions
{
    /// <summary>
    /// Gets normalized UV coordinates (0.0-1.0 range) for this mapped image.
    /// </summary>
    public static RectangleF GetNormalizedUV(this MappedImage mappedImage)
    {
        var pixelCoords = mappedImage.Coords;
        var textureDims = mappedImage.TextureDimensions;

        float left = (float)pixelCoords.Left / textureDims.Width;
        float top = (float)pixelCoords.Top / textureDims.Height;
        float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;
        float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height;

        return new RectangleF(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Gets the effective pixel size, accounting for rotation.
    /// </summary>
    public static Size GetEffectiveSize(this MappedImage mappedImage)
    {
        var pixelSize = new Size(mappedImage.Coords.Width, mappedImage.Coords.Height);

        if (mappedImage.Status == MappedImageStatus.Rotated90Clockwise)
        {
            // When rotated 90 degrees, width and height are swapped
            return new Size(pixelSize.Height, pixelSize.Width);
        }

        return pixelSize;
    }

    /// <summary>
    /// Validates the mapped image configuration.
    /// </summary>
    public static bool Validate(this MappedImage mappedImage)
    {
        if (mappedImage.TextureDimensions.Width <= 0 || mappedImage.TextureDimensions.Height <= 0)
        {
            return false;
        }

        if (mappedImage.Coords.Width <= 0 || mappedImage.Coords.Height <= 0)
        {
            return false;
        }

        if (mappedImage.Coords.Left < 0 || mappedImage.Coords.Top < 0)
        {
            return false;
        }

        if (mappedImage.Coords.Right > mappedImage.TextureDimensions.Width ||
            mappedImage.Coords.Bottom > mappedImage.TextureDimensions.Height)
        {
            return false;
        }

        return true;
    }
}
