using System;

namespace OpenSage.Graphics.Abstractions;

/// <summary>
/// Represents a GPU buffer (vertex, index, or uniform).
/// </summary>
public interface IBuffer : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the size of the buffer in bytes.
    /// </summary>
    uint SizeInBytes { get; }

    /// <summary>
    /// Gets the usage pattern of this buffer.
    /// </summary>
    Resources.BufferUsage Usage { get; }

    /// <summary>
    /// Gets the structure byte stride (for structured buffers), or 0 for raw buffers.
    /// </summary>
    uint StructureByteStride { get; }

    /// <summary>
    /// Uploads data to the buffer.
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="data">The data to upload.</param>
    /// <param name="offsetInBytes">The offset in bytes from the buffer start.</param>
    void SetData<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0) where T : unmanaged;

    /// <summary>
    /// Reads data from the buffer (may require a staging buffer transfer).
    /// </summary>
    /// <typeparam name="T">The data type.</typeparam>
    /// <param name="offsetInBytes">The offset in bytes from the buffer start.</param>
    /// <param name="count">The number of elements to read.</param>
    /// <returns>An array of the requested data.</returns>
    T[] GetData<T>(uint offsetInBytes, uint count) where T : unmanaged;
}

/// <summary>
/// Represents a GPU texture (1D, 2D, 3D, or Cube).
/// </summary>
public interface ITexture : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the texture width in pixels.
    /// </summary>
    uint Width { get; }

    /// <summary>
    /// Gets the texture height in pixels.
    /// </summary>
    uint Height { get; }

    /// <summary>
    /// Gets the texture depth (1 for 2D textures).
    /// </summary>
    uint Depth { get; }

    /// <summary>
    /// Gets the pixel format.
    /// </summary>
    Resources.PixelFormat Format { get; }

    /// <summary>
    /// Gets the number of mipmap levels.
    /// </summary>
    uint MipLevels { get; }

    /// <summary>
    /// Gets the number of array slices.
    /// </summary>
    uint ArrayLayers { get; }

    /// <summary>
    /// Gets a value indicating whether this texture can be used as a render target.
    /// </summary>
    bool IsRenderTarget { get; }

    /// <summary>
    /// Gets a value indicating whether this texture can be used as a shader resource.
    /// </summary>
    bool IsShaderResource { get; }

    /// <summary>
    /// Uploads pixel data to the texture.
    /// </summary>
    /// <param name="data">The pixel data (RGBA format).</param>
    /// <param name="mipLevel">The mipmap level (0 = full resolution).</param>
    /// <param name="arrayLayer">The array slice (0 for non-array textures).</param>
    void SetData(ReadOnlySpan<byte> data, uint mipLevel = 0, uint arrayLayer = 0);

    /// <summary>
    /// Generates mipmaps for this texture from the base level.
    /// </summary>
    void GenerateMipmaps();
}

/// <summary>
/// Represents a framebuffer (collection of render target textures).
/// </summary>
public interface IFramebuffer : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the color attachment texture handles.
    /// </summary>
    Handle<ITexture>[] ColorTargets { get; }

    /// <summary>
    /// Gets the depth/stencil attachment texture handle, or Invalid if none.
    /// </summary>
    Handle<ITexture> DepthTarget { get; }

    /// <summary>
    /// Gets the width of the framebuffer in pixels.
    /// </summary>
    uint Width { get; }

    /// <summary>
    /// Gets the height of the framebuffer in pixels.
    /// </summary>
    uint Height { get; }
}

/// <summary>
/// Represents a sampler (texture sampling state).
/// </summary>
public interface ISampler : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the minification filter mode.
    /// </summary>
    Resources.SamplerFilter MinFilter { get; }

    /// <summary>
    /// Gets the magnification filter mode.
    /// </summary>
    Resources.SamplerFilter MagFilter { get; }

    /// <summary>
    /// Gets the mipmap filter mode.
    /// </summary>
    Resources.SamplerFilter MipFilter { get; }

    /// <summary>
    /// Gets the U (X) address mode.
    /// </summary>
    Resources.SamplerAddressMode AddressU { get; }

    /// <summary>
    /// Gets the V (Y) address mode.
    /// </summary>
    Resources.SamplerAddressMode AddressV { get; }

    /// <summary>
    /// Gets the W (Z) address mode.
    /// </summary>
    Resources.SamplerAddressMode AddressW { get; }

    /// <summary>
    /// Gets the maximum anisotropy level.
    /// </summary>
    uint MaxAnisotropy { get; }
}

/// <summary>
/// Represents a compiled shader program for a specific backend.
/// </summary>
public interface IShaderProgram : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the shader name/identifier.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the entry point function name.
    /// </summary>
    string EntryPoint { get; }
}

/// <summary>
/// Specifies the graphics pipeline rendering state.
/// </summary>
public interface IPipeline : IGraphicsResource, IDisposable
{
    /// <summary>
    /// Gets the rasterization state.
    /// </summary>
    State.RasterState RasterState { get; }

    /// <summary>
    /// Gets the blend state.
    /// </summary>
    State.BlendState BlendState { get; }

    /// <summary>
    /// Gets the depth state.
    /// </summary>
    State.DepthState DepthState { get; }

    /// <summary>
    /// Gets the stencil state.
    /// </summary>
    State.StencilState StencilState { get; }

    /// <summary>
    /// Gets the vertex shader program.
    /// </summary>
    Handle<IShaderProgram> VertexShader { get; }

    /// <summary>
    /// Gets the fragment shader program.
    /// </summary>
    Handle<IShaderProgram> FragmentShader { get; }
}
