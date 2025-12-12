using System;
using VeldridLib = Veldrid;
using OpenSage.Graphics.Abstractions;

namespace OpenSage.Graphics.Veldrid;

/// <summary>
/// Thin wrapper around Veldrid.DeviceBuffer implementing IBuffer interface.
/// Used by ResourcePool to manage GPU memory allocations.
/// </summary>
internal class VeldridBuffer : IBuffer
{
    private readonly VeldridLib.DeviceBuffer _native;
    private bool _disposed;

    public VeldridBuffer(VeldridLib.DeviceBuffer native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public VeldridLib.DeviceBuffer Native => _native;

    public uint SizeInBytes => _native.SizeInBytes;

    public BufferUsage Usage => _native.Usage switch
    {
        VeldridLib.BufferUsage.VertexBuffer => BufferUsage.VertexBuffer,
        VeldridLib.BufferUsage.IndexBuffer => BufferUsage.IndexBuffer,
        VeldridLib.BufferUsage.UniformBuffer => BufferUsage.UniformBuffer,
        VeldridLib.BufferUsage.StructuredBufferReadWrite => BufferUsage.StorageBuffer,
        VeldridLib.BufferUsage.Indirect => BufferUsage.IndirectBuffer,
        _ => BufferUsage.Dynamic
    };

    public bool IsDynamic => _native.Usage.HasFlag(VeldridLib.BufferUsage.Dynamic);

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
    private bool _disposed;

    public VeldridTexture(VeldridLib.Texture native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public VeldridLib.Texture Native => _native;

    public uint Width => _native.Width;
    public uint Height => _native.Height;
    public uint Depth => _native.Depth;

    public PixelFormat Format => _native.Format switch
    {
        VeldridLib.PixelFormat.R8_UNorm => PixelFormat.R8_UNorm,
        VeldridLib.PixelFormat.R8_SNorm => PixelFormat.R8_SNorm,
        VeldridLib.PixelFormat.R8_UInt => PixelFormat.R8_UInt,
        VeldridLib.PixelFormat.R8_SInt => PixelFormat.R8_SInt,

        VeldridLib.PixelFormat.R16_UNorm => PixelFormat.R16_UNorm,
        VeldridLib.PixelFormat.R16_SNorm => PixelFormat.R16_SNorm,
        VeldridLib.PixelFormat.R16_UInt => PixelFormat.R16_UInt,
        VeldridLib.PixelFormat.R16_SInt => PixelFormat.R16_SInt,
        VeldridLib.PixelFormat.R16_Float => PixelFormat.R16_Float,

        VeldridLib.PixelFormat.R32_UInt => PixelFormat.R32_UInt,
        VeldridLib.PixelFormat.R32_SInt => PixelFormat.R32_SInt,
        VeldridLib.PixelFormat.R32_Float => PixelFormat.R32_Float,

        VeldridLib.PixelFormat.R8_G8_UNorm => PixelFormat.R8_G8_UNorm,
        VeldridLib.PixelFormat.R8_G8_SNorm => PixelFormat.R8_G8_SNorm,
        VeldridLib.PixelFormat.R8_G8_UInt => PixelFormat.R8_G8_UInt,
        VeldridLib.PixelFormat.R8_G8_SInt => PixelFormat.R8_G8_SInt,

        VeldridLib.PixelFormat.R16_G16_UNorm => PixelFormat.R16_G16_UNorm,
        VeldridLib.PixelFormat.R16_G16_SNorm => PixelFormat.R16_G16_SNorm,
        VeldridLib.PixelFormat.R16_G16_UInt => PixelFormat.R16_G16_UInt,
        VeldridLib.PixelFormat.R16_G16_SInt => PixelFormat.R16_G16_SInt,
        VeldridLib.PixelFormat.R16_G16_Float => PixelFormat.R16_G16_Float,

        VeldridLib.PixelFormat.R32_G32_UInt => PixelFormat.R32_G32_UInt,
        VeldridLib.PixelFormat.R32_G32_SInt => PixelFormat.R32_G32_SInt,
        VeldridLib.PixelFormat.R32_G32_Float => PixelFormat.R32_G32_Float,

        VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm => PixelFormat.R8_G8_B8_A8_UNorm,
        VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm_SRgb => PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
        VeldridLib.PixelFormat.R8_G8_B8_A8_SNorm => PixelFormat.R8_G8_B8_A8_SNorm,
        VeldridLib.PixelFormat.R8_G8_B8_A8_UInt => PixelFormat.R8_G8_B8_A8_UInt,
        VeldridLib.PixelFormat.R8_G8_B8_A8_SInt => PixelFormat.R8_G8_B8_A8_SInt,

        VeldridLib.PixelFormat.R16_G16_B16_A16_UNorm => PixelFormat.R16_G16_B16_A16_UNorm,
        VeldridLib.PixelFormat.R16_G16_B16_A16_SNorm => PixelFormat.R16_G16_B16_A16_SNorm,
        VeldridLib.PixelFormat.R16_G16_B16_A16_UInt => PixelFormat.R16_G16_B16_A16_UInt,
        VeldridLib.PixelFormat.R16_G16_B16_A16_SInt => PixelFormat.R16_G16_B16_A16_SInt,
        VeldridLib.PixelFormat.R16_G16_B16_A16_Float => PixelFormat.R16_G16_B16_A16_Float,

        VeldridLib.PixelFormat.R32_G32_B32_A32_UInt => PixelFormat.R32_G32_B32_A32_UInt,
        VeldridLib.PixelFormat.R32_G32_B32_A32_SInt => PixelFormat.R32_G32_B32_A32_SInt,
        VeldridLib.PixelFormat.R32_G32_B32_A32_Float => PixelFormat.R32_G32_B32_A32_Float,

        VeldridLib.PixelFormat.D32_Float_S8_UInt => PixelFormat.D32_Float_S8_UInt,
        VeldridLib.PixelFormat.D32_Float => PixelFormat.D32_Float,
        VeldridLib.PixelFormat.D16_UNorm => PixelFormat.D16_UNorm,

        VeldridLib.PixelFormat.BC1_Rgb_UNorm => PixelFormat.BC1_Rgb_UNorm,
        VeldridLib.PixelFormat.BC1_Rgb_UNorm_SRgb => PixelFormat.BC1_Rgb_UNorm_SRgb,
        VeldridLib.PixelFormat.BC1_Rgba_UNorm => PixelFormat.BC1_Rgba_UNorm,
        VeldridLib.PixelFormat.BC1_Rgba_UNorm_SRgb => PixelFormat.BC1_Rgba_UNorm_SRgb,

        VeldridLib.PixelFormat.BC2_UNorm => PixelFormat.BC2_UNorm,
        VeldridLib.PixelFormat.BC2_UNorm_SRgb => PixelFormat.BC2_UNorm_SRgb,

        VeldridLib.PixelFormat.BC3_UNorm => PixelFormat.BC3_UNorm,
        VeldridLib.PixelFormat.BC3_UNorm_SRgb => PixelFormat.BC3_UNorm_SRgb,

        VeldridLib.PixelFormat.BC4_UNorm => PixelFormat.BC4_UNorm,
        VeldridLib.PixelFormat.BC4_SNorm => PixelFormat.BC4_SNorm,

        VeldridLib.PixelFormat.BC5_UNorm => PixelFormat.BC5_UNorm,
        VeldridLib.PixelFormat.BC5_SNorm => PixelFormat.BC5_SNorm,

        _ => PixelFormat.R8_G8_B8_A8_UNorm
    };

    public TextureType Type => _native.Type switch
    {
        VeldridLib.TextureType.Texture1D => TextureType.Texture1D,
        VeldridLib.TextureType.Texture2D => TextureType.Texture2D,
        VeldridLib.TextureType.Texture3D => TextureType.Texture3D,
        _ => TextureType.Texture2D
    };

    public uint MipLevels => _native.MipLevels;
    public uint ArrayLayers => _native.ArrayLayers;

    public bool IsMultisample => _native.SampleCount != VeldridLib.TextureSampleCount.Count1;

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
    private bool _disposed;

    public VeldridSampler(VeldridLib.Sampler native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public VeldridLib.Sampler Native => _native;

    public SamplerFilter MagFilter => _native.Filter.MagFilter switch
    {
        VeldridLib.SamplerFilter.Linear => SamplerFilter.Linear,
        VeldridLib.SamplerFilter.Nearest => SamplerFilter.Nearest,
        _ => SamplerFilter.Linear
    };

    public SamplerFilter MinFilter => _native.Filter.MinFilter switch
    {
        VeldridLib.SamplerFilter.Linear => SamplerFilter.Linear,
        VeldridLib.SamplerFilter.Nearest => SamplerFilter.Nearest,
        _ => SamplerFilter.Linear
    };

    public SamplerAddressMode AddressModeU => _native.AddressU switch
    {
        VeldridLib.SamplerAddressMode.Wrap => SamplerAddressMode.Wrap,
        VeldridLib.SamplerAddressMode.Mirror => SamplerAddressMode.Mirror,
        VeldridLib.SamplerAddressMode.Clamp => SamplerAddressMode.Clamp,
        VeldridLib.SamplerAddressMode.Border => SamplerAddressMode.Border,
        VeldridLib.SamplerAddressMode.MirrorOnce => SamplerAddressMode.MirrorOnce,
        _ => SamplerAddressMode.Wrap
    };

    public SamplerAddressMode AddressModeV => _native.AddressV switch
    {
        VeldridLib.SamplerAddressMode.Wrap => SamplerAddressMode.Wrap,
        VeldridLib.SamplerAddressMode.Mirror => SamplerAddressMode.Mirror,
        VeldridLib.SamplerAddressMode.Clamp => SamplerAddressMode.Clamp,
        VeldridLib.SamplerAddressMode.Border => SamplerAddressMode.Border,
        VeldridLib.SamplerAddressMode.MirrorOnce => SamplerAddressMode.MirrorOnce,
        _ => SamplerAddressMode.Wrap
    };

    public SamplerAddressMode AddressModeW => _native.AddressW switch
    {
        VeldridLib.SamplerAddressMode.Wrap => SamplerAddressMode.Wrap,
        VeldridLib.SamplerAddressMode.Mirror => SamplerAddressMode.Mirror,
        VeldridLib.SamplerAddressMode.Clamp => SamplerAddressMode.Clamp,
        VeldridLib.SamplerAddressMode.Border => SamplerAddressMode.Border,
        VeldridLib.SamplerAddressMode.MirrorOnce => SamplerAddressMode.MirrorOnce,
        _ => SamplerAddressMode.Wrap
    };

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
    private bool _disposed;

    public VeldridFramebuffer(VeldridLib.Framebuffer native)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
    }

    public VeldridLib.Framebuffer Native => _native;

    public uint Width => _native.Width;
    public uint Height => _native.Height;

    public uint ColorTargetCount => (uint)_native.ColorTargets.Count;

    public bool HasDepthTarget => _native.DepthTarget != null;

    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}
