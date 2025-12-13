using System;
using System.Runtime.InteropServices;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.BGFX.Native;
using OpenSage.Graphics.Core;
using OpenSage.Graphics.Resources;
using Veldrid.Sdl2;

namespace OpenSage.Graphics.BGFX;

/// <summary>
/// BGFX-based implementation of IGraphicsDevice.
/// Provides cross-platform graphics rendering via the BGFX library.
/// </summary>
internal sealed class BgfxGraphicsDevice : IGraphicsDevice
{
    private GraphicsCapabilities _capabilities;
    private bool _disposed;

    public Core.GraphicsBackend Backend => Core.GraphicsBackend.BGFX;
    public GraphicsCapabilities Capabilities => _capabilities;

    /// <summary>
    /// Initializes a new instance of the <see cref="BgfxGraphicsDevice"/> class.
    /// </summary>
    public BgfxGraphicsDevice(GraphicsDeviceOptions options)
    {
        Initialize(options);
    }

    public void Initialize(GraphicsDeviceOptions options)
    {
        var initSettings = new bgfx.InitSettings();
        initSettings.type = bgfx.RendererType.Count; // Let BGFX choose based on platform

        // Platform-specific window data
        var platformData = BgfxPlatformData.GetPlatformData(options.Window as Sdl2Window
            ?? throw new InvalidOperationException("BGFX backend requires SDL2 window"));
        initSettings.platformData = platformData;

        // Initialize BGFX
        if (!bgfx.init(ref initSettings))
        {
            throw new InvalidOperationException("Failed to initialize BGFX");
        }

        // Create capabilities from BGFX info
        var caps = bgfx.getCaps();
        _capabilities = new GraphicsCapabilities(
            isInitialized: true,
            backendName: GetBackendName(caps.rendererType),
            apiVersion: $"{caps.homogeneousDepth}",
            vendorName: "BGFX",
            deviceName: "BGFX Device",
            maxTextureSize: 16384,
            maxViewports: 16,
            maxRenderTargets: 8,
            supportsTextureCompressionBC: true,
            supportsTextureCompressionASTC: true,
            supportsComputeShaders: (caps.supported & (ulong)bgfx.BGFX_CAPS_COMPUTE) != 0,
            supportsIndirectRendering: (caps.supported & (ulong)bgfx.BGFX_CAPS_DRAW_INDIRECT) != 0);
    }

    public Handle<Buffer> CreateBuffer(Resources.BufferDescription description, IntPtr data = default)
    {
        throw new NotImplementedException("BGFX buffer creation is implemented in Phase 5B");
    }

    public void UpdateBuffer<T>(Handle<Buffer> buffer, uint offsetInBytes, ReadOnlySpan<T> source) where T : struct
    {
        throw new NotImplementedException("BGFX buffer updates are implemented in Phase 5B");
    }

    public void DeleteBuffer(Handle<Buffer> buffer)
    {
        throw new NotImplementedException("BGFX buffer deletion is implemented in Phase 5B");
    }

    public Handle<Texture> CreateTexture(Resources.TextureDescription description, Resources.TextureInitData[] initData)
    {
        throw new NotImplementedException("BGFX texture creation is implemented in Phase 5B");
    }

    public void UpdateTexture(Handle<Texture> texture, Resources.TextureRegion region, IntPtr data)
    {
        throw new NotImplementedException("BGFX texture updates are implemented in Phase 5B");
    }

    public void DeleteTexture(Handle<Texture> texture)
    {
        throw new NotImplementedException("BGFX texture deletion is implemented in Phase 5B");
    }

    public Handle<Sampler> CreateSampler(Resources.SamplerDescription description)
    {
        throw new NotImplementedException("BGFX sampler creation is implemented in Phase 5B");
    }

    public void DeleteSampler(Handle<Sampler> sampler)
    {
        throw new NotImplementedException("BGFX sampler deletion is implemented in Phase 5B");
    }

    public Handle<Framebuffer> CreateFramebuffer(Resources.FramebufferDescription description)
    {
        throw new NotImplementedException("BGFX framebuffer creation is implemented in Phase 5B");
    }

    public void DeleteFramebuffer(Handle<Framebuffer> framebuffer)
    {
        throw new NotImplementedException("BGFX framebuffer deletion is implemented in Phase 5B");
    }

    public Handle<Shader> CreateShader(Resources.ShaderDescription description)
    {
        throw new NotImplementedException("BGFX shader creation is implemented in Phase 5B");
    }

    public void DeleteShader(Handle<Shader> shader)
    {
        throw new NotImplementedException("BGFX shader deletion is implemented in Phase 5B");
    }

    public Handle<Pipeline> CreatePipeline(Resources.PipelineDescription description)
    {
        throw new NotImplementedException("BGFX pipeline creation is implemented in Phase 5B");
    }

    public void DeletePipeline(Handle<Pipeline> pipeline)
    {
        throw new NotImplementedException("BGFX pipeline deletion is implemented in Phase 5B");
    }

    public void BeginFrame()
    {
        // BGFX does not require frame setup - rendering happens in EndFrame
    }

    public void EndFrame()
    {
        // Submit frame to BGFX
        bgfx.frame(false);
    }

    public void SetRenderTarget(Handle<Framebuffer> framebuffer, bool clearColor, bool clearDepth)
    {
        throw new NotImplementedException("BGFX render target setup is implemented in Phase 5B");
    }

    public void SetViewport(uint x, uint y, uint width, uint height)
    {
        throw new NotImplementedException("BGFX viewport setup is implemented in Phase 5B");
    }

    public void SetScissor(uint x, uint y, uint width, uint height)
    {
        throw new NotImplementedException("BGFX scissor setup is implemented in Phase 5B");
    }

    public void BindVertexBuffer(Handle<Buffer> buffer, uint instanceStartIndex = 0)
    {
        throw new NotImplementedException("BGFX vertex buffer binding is implemented in Phase 5B");
    }

    public void BindIndexBuffer(Handle<Buffer> buffer, Resources.IndexFormat indexFormat)
    {
        throw new NotImplementedException("BGFX index buffer binding is implemented in Phase 5B");
    }

    public void BindPipeline(Handle<Pipeline> pipeline)
    {
        throw new NotImplementedException("BGFX pipeline binding is implemented in Phase 5B");
    }

    public void BindTexture(uint slot, Handle<Texture> texture, Handle<Sampler> sampler)
    {
        throw new NotImplementedException("BGFX texture binding is implemented in Phase 5B");
    }

    public void DrawVertices(uint vertexCount, uint vertexStartIndex = 0, uint instanceCount = 1)
    {
        throw new NotImplementedException("BGFX vertex drawing is implemented in Phase 5B");
    }

    public void DrawIndexed(uint indexCount, uint indexStartIndex = 0, uint vertexStartIndex = 0, uint instanceCount = 1)
    {
        throw new NotImplementedException("BGFX indexed drawing is implemented in Phase 5B");
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        bgfx.shutdown();
        _disposed = true;
    }

    private static string GetBackendName(bgfx.RendererType type)
    {
        return type switch
        {
            bgfx.RendererType.Metal => "Metal",
            bgfx.RendererType.Vulkan => "Vulkan",
            bgfx.RendererType.Direct3D11 => "Direct3D11",
            bgfx.RendererType.OpenGL => "OpenGL",
            bgfx.RendererType.OpenGLES => "OpenGL ES",
            _ => "Unknown"
        };
    }
}
