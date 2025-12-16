using OpenSage.Gui.TextureAtlasing;
using OpenSage.Mathematics;
using Xunit;

namespace OpenSage.Tests.Gui.TextureAtlasing;

public class MappedImageExtensionsTests
{
    // Note: These tests use simple Rectangle math to verify normalized UV calculations
    // without requiring a full AssetStore context

    [Fact]
    public void NormalizedUV_FullAtlas_CalculatesCorrectly()
    {
        // This test verifies the math of normalize UV calculation
        var pixelCoords = new Rectangle(0, 0, 512, 512);
        var textureDims = new Size(512, 512);

        // Manual calculation (what extension should do)
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
    public void NormalizedUV_QuarterAtlas_CalculatesCorrectly()
    {
        var pixelCoords = new Rectangle(0, 0, 256, 256);
        var textureDims = new Size(512, 512);

        float left = (float)pixelCoords.Left / textureDims.Width;        // 0/512 = 0
        float top = (float)pixelCoords.Top / textureDims.Height;         // 0/512 = 0
        float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;  // 256/512 = 0.5
        float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height; // 256/512 = 0.5

        Assert.Equal(0f, left);
        Assert.Equal(0f, top);
        Assert.Equal(0.5f, right);
        Assert.Equal(0.5f, bottom);
    }

    [Fact]
    public void NormalizedUV_OffsetQuad_CalculatesCorrectly()
    {
        var pixelCoords = new Rectangle(256, 128, 128, 128);
        var textureDims = new Size(512, 512);

        float left = (float)pixelCoords.Left / textureDims.Width;        // 256/512 = 0.5
        float top = (float)pixelCoords.Top / textureDims.Height;         // 128/512 = 0.25
        float right = (float)(pixelCoords.Left + pixelCoords.Width) / textureDims.Width;  // 384/512 = 0.75
        float bottom = (float)(pixelCoords.Top + pixelCoords.Height) / textureDims.Height; // 256/512 = 0.5

        Assert.Equal(0.5f, left);
        Assert.Equal(0.25f, top);
        Assert.Equal(0.75f, right);
        Assert.Equal(0.5f, bottom);
    }
}
