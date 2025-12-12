using System;

namespace OpenSage.Graphics.Resources;

/// <summary>
/// Specifies the usage pattern of a buffer.
/// </summary>
public enum BufferUsage
{
    /// <summary>
    /// Buffer data rarely changes (static geometry).
    /// </summary>
    Static,

    /// <summary>
    /// Buffer data changes occasionally per frame.
    /// </summary>
    Dynamic,

    /// <summary>
    /// Buffer data may change every frame.
    /// </summary>
    Stream,
}

/// <summary>
/// Describes parameters for creating a graphics buffer.
/// </summary>
public readonly struct BufferDescription
{
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    public uint SizeInBytes { get; }

    /// <summary>
    /// Gets the buffer usage pattern.
    /// </summary>
    public BufferUsage Usage { get; }

    /// <summary>
    /// Gets the element stride in bytes (for structured buffers).
    /// Set to 0 for raw/untyped buffers.
    /// </summary>
    public uint StructureByteStride { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="BufferDescription"/> struct.
    /// </summary>
    /// <param name="sizeInBytes">The buffer size in bytes.</param>
    /// <param name="usage">The buffer usage pattern.</param>
    /// <param name="structureByteStride">The element stride (0 for raw buffers).</param>
    public BufferDescription(uint sizeInBytes, BufferUsage usage = BufferUsage.Static, uint structureByteStride = 0)
    {
        SizeInBytes = sizeInBytes;
        Usage = usage;
        StructureByteStride = structureByteStride;
    }
}

/// <summary>
/// Specifies the type of texture.
/// </summary>
public enum TextureType
{
    /// <summary>
    /// 1D texture.
    /// </summary>
    Texture1D,

    /// <summary>
    /// 2D texture (most common).
    /// </summary>
    Texture2D,

    /// <summary>
    /// 3D volume texture.
    /// </summary>
    Texture3D,
}

/// <summary>
/// Specifies pixel format for textures and render targets.
/// </summary>
public enum PixelFormat
{
    // Common formats
    /// <summary>
    /// 32-bit RGBA with 8 bits per channel (sRGB color space).
    /// </summary>
    R8G8B8A8_SRgb,

    /// <summary>
    /// 32-bit RGBA with 8 bits per channel (linear color space).
    /// </summary>
    R8G8B8A8_UNorm,

    /// <summary>
    /// 32-bit BGRA with 8 bits per channel (sRGB).
    /// </summary>
    B8G8R8A8_SRgb,

    /// <summary>
    /// 32-bit BGRA with 8 bits per channel (linear).
    /// </summary>
    B8G8R8A8_UNorm,

    /// <summary>
    /// 32-bit depth (float).
    /// </summary>
    D32_Float,

    /// <summary>
    /// 24-bit depth + 8-bit stencil.
    /// </summary>
    D24_UNorm_S8_UInt,

    /// <summary>
    /// 32-bit depth (float) + 8-bit stencil.
    /// </summary>
    D32_Float_S8_UInt,
}

/// <summary>
/// Describes parameters for creating a texture.
/// </summary>
public readonly struct TextureDescription
{
    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    public uint Width { get; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    public uint Height { get; }

    /// <summary>
    /// Gets the texture depth (for 3D textures).
    /// Set to 1 for 2D textures.
    /// </summary>
    public uint Depth { get; }

    /// <summary>
    /// Gets the number of mipmap levels.
    /// Set to 1 for no mipmaps.
    /// </summary>
    public uint MipLevels { get; }

    /// <summary>
    /// Gets the pixel format.
    /// </summary>
    public PixelFormat Format { get; }

    /// <summary>
    /// Gets the number of array slices (for texture arrays).
    /// Set to 1 for non-array textures.
    /// </summary>
    public uint ArrayLayers { get; }

    /// <summary>
    /// Gets a value indicating whether the texture can be used as a render target.
    /// </summary>
    public bool IsRenderTarget { get; }

    /// <summary>
    /// Gets a value indicating whether the texture can be used as a shader resource.
    /// </summary>
    public bool IsShaderResource { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="TextureDescription"/> struct.
    /// </summary>
    public TextureDescription(
        uint width,
        uint height,
        PixelFormat format = PixelFormat.R8G8B8A8_SRgb,
        uint mipLevels = 1,
        uint arrayLayers = 1,
        uint depth = 1,
        bool isRenderTarget = false,
        bool isShaderResource = true)
    {
        Width = width;
        Height = height;
        Depth = depth;
        MipLevels = mipLevels;
        Format = format;
        ArrayLayers = arrayLayers;
        IsRenderTarget = isRenderTarget;
        IsShaderResource = isShaderResource;
    }
}

/// <summary>
/// Describes parameters for creating a framebuffer (render target collection).
/// </summary>
public readonly struct FramebufferDescription
{
    /// <summary>
    /// Gets the color attachment texture handles.
    /// </summary>
    public Abstractions.Handle<Abstractions.ITexture>[] ColorTargets { get; }

    /// <summary>
    /// Gets the depth/stencil attachment texture handle, or Invalid if none.
    /// </summary>
    public Abstractions.Handle<Abstractions.ITexture> DepthTarget { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="FramebufferDescription"/> struct.
    /// </summary>
    /// <param name="colorTargets">The color attachment textures.</param>
    /// <param name="depthTarget">The depth/stencil attachment texture.</param>
    public FramebufferDescription(
        Abstractions.Handle<Abstractions.ITexture>[] colorTargets,
        Abstractions.Handle<Abstractions.ITexture> depthTarget = default)
    {
        ColorTargets = colorTargets ?? Array.Empty<Abstractions.Handle<Abstractions.ITexture>>();
        DepthTarget = depthTarget;
    }
}

/// <summary>
/// Specifies texture filtering mode.
/// </summary>
public enum SamplerFilter
{
    /// <summary>
    /// Nearest neighbor filtering.
    /// </summary>
    Nearest,

    /// <summary>
    /// Linear interpolation filtering.
    /// </summary>
    Linear,

    /// <summary>
    /// Anisotropic filtering (best quality).
    /// </summary>
    Anisotropic,
}

/// <summary>
/// Specifies texture addressing/wrapping mode.
/// </summary>
public enum SamplerAddressMode
{
    /// <summary>
    /// Clamp texture coordinates to [0, 1] range.
    /// </summary>
    Clamp,

    /// <summary>
    /// Repeat/tile the texture.
    /// </summary>
    Wrap,

    /// <summary>
    /// Mirror the texture.
    /// </summary>
    Mirror,

    /// <summary>
    /// Use border color for out-of-range coordinates.
    /// </summary>
    Border,
}

/// <summary>
/// Describes parameters for creating a sampler (texture sampling state).
/// </summary>
public readonly struct SamplerDescription
{
    /// <summary>
    /// Gets the minification filter mode.
    /// </summary>
    public SamplerFilter MinFilter { get; }

    /// <summary>
    /// Gets the magnification filter mode.
    /// </summary>
    public SamplerFilter MagFilter { get; }

    /// <summary>
    /// Gets the mipmap filter mode.
    /// </summary>
    public SamplerFilter MipFilter { get; }

    /// <summary>
    /// Gets the U (X) address mode.
    /// </summary>
    public SamplerAddressMode AddressU { get; }

    /// <summary>
    /// Gets the V (Y) address mode.
    /// </summary>
    public SamplerAddressMode AddressV { get; }

    /// <summary>
    /// Gets the W (Z) address mode.
    /// </summary>
    public SamplerAddressMode AddressW { get; }

    /// <summary>
    /// Gets the maximum anisotropy level (1-16).
    /// </summary>
    public uint MaxAnisotropy { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SamplerDescription"/> struct.
    /// </summary>
    public SamplerDescription(
        SamplerFilter minFilter = SamplerFilter.Linear,
        SamplerFilter magFilter = SamplerFilter.Linear,
        SamplerFilter mipFilter = SamplerFilter.Linear,
        SamplerAddressMode addressU = SamplerAddressMode.Wrap,
        SamplerAddressMode addressV = SamplerAddressMode.Wrap,
        SamplerAddressMode addressW = SamplerAddressMode.Wrap,
        uint maxAnisotropy = 1)
    {
        MinFilter = minFilter;
        MagFilter = magFilter;
        MipFilter = mipFilter;
        AddressU = addressU;
        AddressV = addressV;
        AddressW = addressW;
        MaxAnisotropy = maxAnisotropy;
    }
}
