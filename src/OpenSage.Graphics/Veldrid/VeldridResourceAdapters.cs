using System;
using VeldridLib = Veldrid;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Resources;
using OpenSage.Graphics.State;

namespace OpenSage.Graphics.Adapters;

/// <summary>
/// Thin wrapper around Veldrid.DeviceBuffer implementing IBuffer interface.
/// Used by ResourcePool to manage GPU memory allocations.
/// </summary>
internal class VeldridBuffer : IBuffer
{
    private readonly VeldridLib.DeviceBuffer _native;
    private uint _id;
    private uint _generation;
    private bool _disposed;

    public VeldridBuffer(VeldridLib.DeviceBuffer native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public uint SizeInBytes => _native.SizeInBytes;
    public BufferUsage Usage => BufferUsage.Dynamic;
    public uint StructureByteStride => 0;
    public uint Id => _id;
    public uint Generation => _generation;
    public bool IsValid => !_disposed;

    public void SetId(uint id, uint generation)
    {
        _id = id;
        _generation = generation;
    }

    public void SetData<T>(ReadOnlySpan<T> data, uint offsetInBytes = 0) where T : unmanaged
    {
        throw new NotImplementedException("Buffer updates require CommandList");
    }

    public T[] GetData<T>(uint offsetInBytes, uint count) where T : unmanaged
    {
        throw new NotImplementedException("Buffer reads require staging transfers");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Thin wrapper around Veldrid.Texture implementing ITexture interface.
/// Manages GPU texture memory and metadata.
/// </summary>
internal class VeldridTexture : ITexture
{
    private readonly VeldridLib.Texture _native;
    private uint _id;
    private uint _generation;
    private bool _disposed;

    public VeldridTexture(VeldridLib.Texture native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public uint Width => _native.Width;
    public uint Height => _native.Height;
    public uint Depth => _native.Depth;
    public PixelFormat Format => PixelFormat.R8G8B8A8_UNorm;
    public Resources.TextureType Type => Resources.TextureType.Texture2D;
    public uint MipLevels => _native.MipLevels;
    public uint ArrayLayers => _native.ArrayLayers;
    public bool IsRenderTarget => false;
    public bool IsShaderResource => true;
    public uint Id => _id;
    public uint Generation => _generation;
    public bool IsValid => !_disposed;

    public void SetId(uint id, uint generation)
    {
        _id = id;
        _generation = generation;
    }

    public void SetData(ReadOnlySpan<byte> data, uint mipLevel = 0, uint arrayLayer = 0)
    {
        throw new NotImplementedException("Texture updates require CommandList");
    }

    public void GenerateMipmaps()
    {
        throw new NotImplementedException("Mipmap generation requires CommandList");
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Thin wrapper around Veldrid.Sampler implementing ISampler interface.
/// Controls texture filtering and addressing modes.
/// </summary>
internal class VeldridSampler : ISampler
{
    private readonly VeldridLib.Sampler _native;
    private uint _id;
    private uint _generation;
    private bool _disposed;

    public VeldridSampler(VeldridLib.Sampler native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public SamplerFilter MinFilter => SamplerFilter.Linear;
    public SamplerFilter MagFilter => SamplerFilter.Linear;
    public SamplerFilter MipFilter => SamplerFilter.Linear;
    public SamplerAddressMode AddressU => SamplerAddressMode.Wrap;
    public SamplerAddressMode AddressV => SamplerAddressMode.Wrap;
    public SamplerAddressMode AddressW => SamplerAddressMode.Wrap;
    public uint MaxAnisotropy => 1;
    public uint Id => _id;
    public uint Generation => _generation;
    public bool IsValid => !_disposed;

    public void SetId(uint id, uint generation)
    {
        _id = id;
        _generation = generation;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// Thin wrapper around Veldrid.Framebuffer implementing IFramebuffer interface.
/// Represents render targets and depth buffers for rendering operations.
/// </summary>
internal class VeldridFramebuffer : IFramebuffer
{
    private readonly VeldridLib.Framebuffer _native;
    private uint _id;
    private uint _generation;
    private bool _disposed;

    public VeldridFramebuffer(VeldridLib.Framebuffer native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public uint Width => _native.Width;
    public uint Height => _native.Height;
    public Handle<ITexture>[] ColorTargets => Array.Empty<Handle<ITexture>>();
    public Handle<ITexture> DepthTarget => Handle<ITexture>.Invalid;
    public uint Id => _id;
    public uint Generation => _generation;
    public bool IsValid => !_disposed;

    public void SetId(uint id, uint generation)
    {
        _id = id;
        _generation = generation;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}
