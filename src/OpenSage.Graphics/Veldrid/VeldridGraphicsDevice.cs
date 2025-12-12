using System;
using System.Collections.Generic;
using System.Numerics;
using OpenSage.Core;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Core;
using OpenSage.Graphics.Pooling;
using OpenSage.Graphics.Resources;
using OpenSage.Graphics.State;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Veldrid;

/// <summary>
/// Veldrid implementation of IGraphicsDevice.
/// Wraps Veldrid's GraphicsDevice and provides command recording via CommandList.
/// Uses resource pooling with generation-based validation for GPU resource lifecycle management.
/// </summary>
public class VeldridGraphicsDevice : DisposableBase, IGraphicsDevice
{
    private readonly VeldridLib.GraphicsDevice _device;
    private readonly VeldridLib.CommandList _cmdList;
    
    // Resource pools with generation-based validation
    private readonly ResourcePool<VeldridLib.DeviceBuffer> _bufferPool;
    private readonly ResourcePool<VeldridLib.Texture> _texturePool;
    private readonly ResourcePool<VeldridLib.Sampler> _samplerPool;
    private readonly ResourcePool<VeldridLib.Framebuffer> _framebufferPool;
    
    private readonly Dictionary<uint, object> _shaders = new();
    private readonly Dictionary<uint, object> _pipelines = new();

    private VeldridLib.Framebuffer _currentFramebuffer;

    public Core.GraphicsBackend Backend => Core.GraphicsBackend.Veldrid;
    public GraphicsCapabilities Capabilities { get; private set; }
    public bool IsReady { get; private set; }

    public VeldridGraphicsDevice(VeldridLib.GraphicsDevice device)
    {
        if (device == null) throw new ArgumentNullException(nameof(device));
        _device = device;
        _cmdList = device.ResourceFactory.CreateCommandList();
        AddDisposable(_cmdList);
        
        // Initialize resource pools with adequate initial capacity
        _bufferPool = new ResourcePool<VeldridLib.DeviceBuffer>(256);
        _texturePool = new ResourcePool<VeldridLib.Texture>(128);
        _samplerPool = new ResourcePool<VeldridLib.Sampler>(64);
        _framebufferPool = new ResourcePool<VeldridLib.Framebuffer>(32);
        
        AddDisposable(_bufferPool);
        AddDisposable(_texturePool);
        AddDisposable(_samplerPool);
        AddDisposable(_framebufferPool);
        
        InitCapabilities();
        IsReady = true;
    }

    private void InitCapabilities()
    {
        Capabilities = new GraphicsCapabilities(
            isInitialized: true,
            backendName: _device.BackendType.ToString(),
            apiVersion: _device.ApiVersion.ToString(),
            vendorName: _device.VendorName ?? "Unknown",
            deviceName: _device.DeviceName ?? "Unknown",
            maxTextureSize: 16384,
            maxViewports: 16,
            maxRenderTargets: 8,
            supportsTextureCompressionBC: true,
            supportsTextureCompressionASTC: false,
            supportsComputeShaders: _device.Features.ComputeShaders,
            supportsIndirectRendering: true
        );
    }

    public void BeginFrame()
    {
        _cmdList.Begin();
    }

    public void EndFrame()
    {
        _cmdList.End();
        _device.SubmitCommands(_cmdList);
    }

    public void WaitForIdle()
    {
        _device.WaitForIdle();
    }

    // ===== Buffer Operations =====

    public Handle<IBuffer> CreateBuffer(Resources.BufferDescription desc, ReadOnlySpan<byte> data = default)
    {
        var vUsage = desc.Usage switch
        {
            BufferUsage.Static => VeldridLib.BufferUsage.Staging,
            BufferUsage.Dynamic => VeldridLib.BufferUsage.Dynamic,
            BufferUsage.Stream => VeldridLib.BufferUsage.Dynamic,
            _ => VeldridLib.BufferUsage.Dynamic
        };

        var vDesc = new VeldridLib.BufferDescription(
            desc.SizeInBytes,
            vUsage,
            desc.StructureByteStride);

        var buf = _device.ResourceFactory.CreateBuffer(vDesc);
        if (!data.IsEmpty)
        {
            _cmdList.UpdateBuffer(buf, 0, data);
        }

        // Allocate from pool with generation validation
        var poolHandle = _bufferPool.Allocate(buf);
        
        // Convert PoolHandle to Handle<IBuffer> using index as ID
        return new Handle<IBuffer>(poolHandle.Index, poolHandle.Generation);
    }

    public void DestroyBuffer(Handle<IBuffer> buffer)
    {
        if (!buffer.IsValid)
            return;

        var poolHandle = new ResourcePool<VeldridLib.DeviceBuffer>.PoolHandle(buffer.Id, buffer.Generation);
        _bufferPool.Release(poolHandle);
    }

    public IBuffer GetBuffer(Handle<IBuffer> buffer)
    {
        // For now, return null as we're not implementing wrapper classes yet
        return null;
    }

    // ===== Texture Operations =====

    public Handle<ITexture> CreateTexture(Resources.TextureDescription desc, ReadOnlySpan<byte> data = default)
    {
        var vDesc = new VeldridLib.TextureDescription(
            desc.Width,
            desc.Height,
            desc.Depth,
            desc.MipLevels,
            desc.ArrayLayers,
            ConvertFormat(desc.Format),
            (desc.IsRenderTarget ? VeldridLib.TextureUsage.RenderTarget : 0) |
            (desc.IsShaderResource ? VeldridLib.TextureUsage.Sampled : 0),
            VeldridLib.TextureType.Texture2D);

        var tex = _device.ResourceFactory.CreateTexture(vDesc);

        if (!data.IsEmpty)
        {
            _cmdList.UpdateTexture(tex, data, 0, 0, 0, desc.Width, desc.Height, desc.Depth, 0, 0);
        }

        // Allocate from pool with generation validation
        var poolHandle = _texturePool.Allocate(tex);
        
        // Convert PoolHandle to Handle<ITexture> using index as ID
        return new Handle<ITexture>(poolHandle.Index, poolHandle.Generation);
    }

    public void DestroyTexture(Handle<ITexture> texture)
    {
        if (!texture.IsValid)
            return;

        var poolHandle = new ResourcePool<VeldridLib.Texture>.PoolHandle(texture.Id, texture.Generation);
        _texturePool.Release(poolHandle);
    }

    public ITexture GetTexture(Handle<ITexture> texture)
    {
        return null;
    }

    // ===== Sampler Operations =====

    public Handle<ISampler> CreateSampler(Resources.SamplerDescription desc)
    {
        var vDesc = new VeldridLib.SamplerDescription(
            ConvertAddress(desc.AddressU),
            ConvertAddress(desc.AddressV),
            ConvertAddress(desc.AddressW),
            ConvertFilter(desc.MinFilter),
            null,
            (uint)Math.Max(1, Math.Min(16, desc.MaxAnisotropy)),
            0,
            float.MaxValue,
            0);

        var samp = _device.ResourceFactory.CreateSampler(vDesc);

        // Allocate from pool with generation validation
        var poolHandle = _samplerPool.Allocate(samp);
        
        // Convert PoolHandle to Handle<ISampler> using index as ID
        return new Handle<ISampler>(poolHandle.Index, poolHandle.Generation);
    }

    public void DestroySampler(Handle<ISampler> sampler)
    {
        if (!sampler.IsValid)
            return;

        var poolHandle = new ResourcePool<VeldridLib.Sampler>.PoolHandle(sampler.Id, sampler.Generation);
        _samplerPool.Release(poolHandle);
    }

    public ISampler GetSampler(Handle<ISampler> sampler)
    {
        return null;
    }

    // ===== Framebuffer Operations =====

    public Handle<IFramebuffer> CreateFramebuffer(Resources.FramebufferDescription desc)
    {
        // Placeholder implementation - Week 9 will implement full framebuffer support
        var fb = _device.SwapchainFramebuffer;
        
        // Allocate from pool with generation validation
        var poolHandle = _framebufferPool.Allocate(fb);
        
        // Convert PoolHandle to Handle<IFramebuffer> using index as ID
        return new Handle<IFramebuffer>(poolHandle.Index, poolHandle.Generation);
    }

    public void DestroyFramebuffer(Handle<IFramebuffer> framebuffer)
    {
        if (!framebuffer.IsValid)
            return;

        var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(framebuffer.Id, framebuffer.Generation);
        _framebufferPool.Release(poolHandle);
    }

    public IFramebuffer GetFramebuffer(Handle<IFramebuffer> framebuffer)
    {
        return null;
    }

    // ===== Shader Operations =====

    public Handle<IShaderProgram> CreateShader(string name, ReadOnlySpan<byte> spirvData, string entryPoint = "main")
    {
        // Placeholder - Week 9 will implement shader compilation
        uint id = _nextResourceId++;
        _shaders[id] = null;
        return new Handle<IShaderProgram>(id, 1);
    }

    public void DestroyShader(Handle<IShaderProgram> shader)
    {
        if (shader.IsValid && _shaders.TryGetValue(shader.Id, out _))
        {
            _shaders.Remove(shader.Id);
        }
    }

    public IShaderProgram GetShader(Handle<IShaderProgram> shader)
    {
        return null;
    }

    // ===== Pipeline Operations =====

    public Handle<IPipeline> CreatePipeline(
        Handle<IShaderProgram> vertexShader,
        Handle<IShaderProgram> fragmentShader,
        RasterState rasterState = default,
        DepthState depthState = default,
        BlendState blendState = default,
        StencilState stencilState = default)
    {
        // Placeholder - Week 9 will implement pipeline creation
        uint id = _nextResourceId++;
        _pipelines[id] = null;
        return new Handle<IPipeline>(id, 1);
    }

    public void DestroyPipeline(Handle<IPipeline> pipeline)
    {
        if (pipeline.IsValid && _pipelines.TryGetValue(pipeline.Id, out _))
        {
            _pipelines.Remove(pipeline.Id);
        }
    }

    public IPipeline GetPipeline(Handle<IPipeline> pipeline)
    {
        return null;
    }

    // ===== Rendering Operations =====

    public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
    {
        if (framebuffer.IsValid && _framebuffers.TryGetValue(framebuffer.Id, out var obj) && obj is VeldridLib.Framebuffer fb)
        {
            _currentFramebuffer = fb;
            _cmdList.SetFramebuffer(fb);
        }
        else
        {
            _currentFramebuffer = _device.SwapchainFramebuffer;
            _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
        }
    }

    public void ClearRenderTarget(
        Vector4 clearColor,
        float clearDepth = 1.0f,
        byte clearStencil = 0,
        bool colorMask = true,
        bool depthMask = true,
        bool stencilMask = true)
    {
        if (colorMask)
        {
            _cmdList.ClearColorTarget(0, new VeldridLib.RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));
        }

        if (depthMask)
        {
            _cmdList.ClearDepthTarget(clearDepth);
        }
    }

    public void SetPipeline(Handle<IPipeline> pipeline)
    {
        // Placeholder - Week 9 will implement
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
    {
        _cmdList.SetViewport(0, new VeldridLib.Viewport(x, y, width, height, minDepth, maxDepth));
    }

    public void SetScissor(int x, int y, int width, int height)
    {
        _cmdList.SetScissorRect(0, (uint)x, (uint)y, (uint)width, (uint)height);
    }

    public void BindVertexBuffer(Handle<IBuffer> buffer, uint offset = 0)
    {
        // Placeholder - Week 9 will implement
    }

    public void BindIndexBuffer(Handle<IBuffer> buffer, uint offset = 0)
    {
        // Placeholder - Week 9 will implement
    }

    public void BindUniformBuffer(Handle<IBuffer> buffer, uint slot)
    {
        // Placeholder - Week 9 will implement
    }

    public void BindTexture(Handle<ITexture> texture, uint slot, Handle<ISampler> sampler)
    {
        // Placeholder - Week 9 will implement
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0)
    {
        _cmdList.DrawIndexed(indexCount, instanceCount, startIndex, baseVertex, startInstance);
    }

    public void DrawVertices(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0)
    {
        _cmdList.Draw(vertexCount, instanceCount, startVertex, startInstance);
    }

    public void DrawIndexedIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride)
    {
        // Placeholder - Week 9 will implement
    }

    public void DrawVerticesIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride)
    {
        // Placeholder - Week 9 will implement
    }

    // ===== Helper Methods =====

    private VeldridLib.PixelFormat ConvertFormat(PixelFormat f) => f switch
    {
        PixelFormat.R8G8B8A8_SRgb => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
        PixelFormat.R8G8B8A8_UNorm => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm,
        PixelFormat.B8G8R8A8_SRgb => VeldridLib.PixelFormat.B8_G8_R8_A8_UNorm_SRgb,
        PixelFormat.B8G8R8A8_UNorm => VeldridLib.PixelFormat.B8_G8_R8_A8_UNorm,
        PixelFormat.D32_Float => VeldridLib.PixelFormat.D32_Float,
        PixelFormat.D24_UNorm_S8_UInt => VeldridLib.PixelFormat.D24_UNorm_S8_UInt,
        PixelFormat.D32_Float_S8_UInt => VeldridLib.PixelFormat.D32_Float_S8_UInt,
        _ => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm
    };

    private VeldridLib.SamplerAddressMode ConvertAddress(Resources.SamplerAddressMode m) => m switch
    {
        Resources.SamplerAddressMode.Clamp => VeldridLib.SamplerAddressMode.Clamp,
        Resources.SamplerAddressMode.Wrap => VeldridLib.SamplerAddressMode.Wrap,
        Resources.SamplerAddressMode.Mirror => VeldridLib.SamplerAddressMode.Mirror,
        Resources.SamplerAddressMode.Border => VeldridLib.SamplerAddressMode.Border,
        _ => VeldridLib.SamplerAddressMode.Clamp
    };

    private VeldridLib.SamplerFilter ConvertFilter(Resources.SamplerFilter f) => f switch
    {
        Resources.SamplerFilter.Anisotropic => VeldridLib.SamplerFilter.Anisotropic,
        Resources.SamplerFilter.Linear => VeldridLib.SamplerFilter.Linear,
        _ => VeldridLib.SamplerFilter.MinPoint
    };

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
    }
}
