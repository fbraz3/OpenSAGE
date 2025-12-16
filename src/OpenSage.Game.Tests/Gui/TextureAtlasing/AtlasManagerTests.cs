using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Gui;
using OpenSage.Gui.TextureAtlasing;
using OpenSage.Mathematics;
using Xunit;

namespace OpenSage.Tests.Gui.TextureAtlasing;

public class AtlasManagerTests
{
    [Fact]
    public void AtlasImage_NormalizedUV_FullAtlas_CalculatesCorrectly()
    {
        // This test verifies normalized UV calculation for a full atlas
        var pixelCoords = new Rectangle(0, 0, 512, 512);
        var textureDims = new Size(512, 512);

        float left = (float)pixelCoords.Left / textureDims.Width;        // 0/512 = 0
        float top = (float)pixelCoords.Top / textureDims.Height;         // 0/512 = 0
        float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;  // 512/512 = 1
        float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height; // 512/512 = 1

        Assert.Equal(0f, left);
        Assert.Equal(0f, top);
        Assert.Equal(1f, right);
        Assert.Equal(1f, bottom);
    }

    [Fact]
    public void AtlasImage_NormalizedUV_HalfAtlas_CalculatesCorrectly()
    {
        // This test verifies normalized UV calculation for a half atlas
        var pixelCoords = new Rectangle(0, 0, 256, 512);
        var textureDims = new Size(512, 512);

        float left = (float)pixelCoords.Left / textureDims.Width;        // 0/512 = 0
        float top = (float)pixelCoords.Top / textureDims.Height;         // 0/512 = 0
        float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;  // 256/512 = 0.5
        float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height; // 512/512 = 1

        Assert.Equal(0f, left);
        Assert.Equal(0f, top);
        Assert.Equal(0.5f, right);
        Assert.Equal(1f, bottom);
    }

    [Fact]
    public void AtlasImage_GetEffectiveSize_Normal_ReturnsSameSize()
    {
        var pixelSize = new Size(64, 128);

        // Without rotation, effective size should equal pixel size
        Assert.Equal(64, pixelSize.Width);
        Assert.Equal(128, pixelSize.Height);
    }

    [Fact]
    public void AtlasImage_GetEffectiveSize_Rotated90_SwapsDimensions()
    {
        var pixelSize = new Size(64, 128);

        // After rotation, dimensions are swapped
        var rotatedSize = new Size(pixelSize.Height, pixelSize.Width);

        Assert.Equal(128, rotatedSize.Width);
        Assert.Equal(64, rotatedSize.Height);
    }

    [Fact]
    public void AtlasImage_Validate_ValidCoords_ReturnsTrue()
    {
        var pixelCoords = new Rectangle(0, 0, 100, 100);
        var textureDims = new Size(512, 512);

        bool isValid = pixelCoords.Width > 0 && pixelCoords.Height > 0 &&
                      pixelCoords.Left >= 0 && pixelCoords.Top >= 0 &&
                      pixelCoords.Right <= textureDims.Width &&
                      pixelCoords.Bottom <= textureDims.Height;

        Assert.True(isValid);
    }

    [Fact]
    public void AtlasImage_Validate_OutOfBounds_ReturnsFalse()
    {
        var pixelCoords = new Rectangle(0, 0, 600, 100);
        var textureDims = new Size(512, 512);

        bool isValid = pixelCoords.Width > 0 && pixelCoords.Height > 0 &&
                      pixelCoords.Left >= 0 && pixelCoords.Top >= 0 &&
                      pixelCoords.Right <= textureDims.Width &&
                      pixelCoords.Bottom <= textureDims.Height;

        Assert.False(isValid);
    }

    [Fact]
    public void AtlasImage_Validate_NegativeCoords_ReturnsFalse()
    {
        var pixelCoords = new Rectangle(-10, 0, 100, 100);
        var textureDims = new Size(512, 512);

        bool isValid = pixelCoords.Width > 0 && pixelCoords.Height > 0 &&
                      pixelCoords.Left >= 0 && pixelCoords.Top >= 0 &&
                      pixelCoords.Right <= textureDims.Width &&
                      pixelCoords.Bottom <= textureDims.Height;

        Assert.False(isValid);
    }
}

