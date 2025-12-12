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
using Veldrid.SPIRV;

namespace OpenSage.Graphics.Adapters;

/// <summary>
/// Veldrid graphics device adapter implementing IGraphicsDevice.
///
/// Phase 4 Implementation Status:
/// - Week 20: Base integration, dual-path architecture (COMPLETE)
/// - Week 21: Resource management and pooling (IN PROGRESS)
/// - Week 22: Rendering operations and shaders (PLANNED)
/// - Week 23+: BGFX backend, testing (PLANNED)
/// </summary>
public sealed class VeldridGraphicsDeviceAdapter : DisposableBase, IGraphicsDevice
{
    private readonly VeldridLib.GraphicsDevice _device;
    private readonly VeldridLib.CommandList _commandList;
    private uint _nextHandleId = 1;

    // Resource pools for lifetime management
    private readonly ResourcePool<VeldridLib.DeviceBuffer> _bufferPool;
    private readonly ResourcePool<VeldridLib.Texture> _texturePool;
    private readonly ResourcePool<VeldridLib.Sampler> _samplerPool;
    private readonly ResourcePool<VeldridLib.Framebuffer> _framebufferPool;

    // Handle-to-poolhandle mappings for resource lookup
    private readonly Dictionary<uint, ResourcePool<VeldridLib.DeviceBuffer>.PoolHandle> _bufferHandles;
    private readonly Dictionary<uint, ResourcePool<VeldridLib.Texture>.PoolHandle> _textureHandles;
    private readonly Dictionary<uint, ResourcePool<VeldridLib.Sampler>.PoolHandle> _samplerHandles;
    private readonly Dictionary<uint, ResourcePool<VeldridLib.Framebuffer>.PoolHandle> _framebufferHandles;

    // Shader and pipeline storage (not pooled, stored directly)
    private readonly Dictionary<uint, VeldridShaderProgram> _shaders;
    private readonly Dictionary<uint, VeldridLib.Pipeline> _pipelines;

    // Current rendering state tracking
    private VeldridLib.Framebuffer? _currentFramebuffer;
    private VeldridLib.Pipeline? _currentPipeline;
    private uint _boundVertexBufferSlot = 0;
    private uint _boundIndexBufferOffset = 0;

    public GraphicsBackend Backend => GraphicsBackend.Veldrid;
    public GraphicsCapabilities Capabilities { get; }
    public bool IsReady { get; }

    public VeldridGraphicsDeviceAdapter(VeldridLib.GraphicsDevice device)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));

        // Create command list for recording rendering commands
        _commandList = _device.ResourceFactory.CreateCommandList();

        // Initialize resource pools with standard capacities
        _bufferPool = new ResourcePool<VeldridLib.DeviceBuffer>(256);
        _texturePool = new ResourcePool<VeldridLib.Texture>(128);
        _samplerPool = new ResourcePool<VeldridLib.Sampler>(64);
        _framebufferPool = new ResourcePool<VeldridLib.Framebuffer>(32);

        // Initialize handle mappings
        _bufferHandles = new Dictionary<uint, ResourcePool<VeldridLib.DeviceBuffer>.PoolHandle>();
        _textureHandles = new Dictionary<uint, ResourcePool<VeldridLib.Texture>.PoolHandle>();
        _samplerHandles = new Dictionary<uint, ResourcePool<VeldridLib.Sampler>.PoolHandle>();
        _framebufferHandles = new Dictionary<uint, ResourcePool<VeldridLib.Framebuffer>.PoolHandle>();

        // Initialize shader and pipeline storage
        _shaders = new Dictionary<uint, VeldridShaderProgram>();
        _pipelines = new Dictionary<uint, VeldridLib.Pipeline>();

        // Initialize capabilities based on Veldrid device
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
            supportsComputeShaders: false,
            supportsIndirectRendering: true
        );

        IsReady = true;
    }

    /// <summary>
    /// Gets the underlying Veldrid GraphicsDevice.
    /// Used by infrastructure components that still directly use Veldrid.
    /// </summary>
    public VeldridLib.GraphicsDevice UnderlyingDevice => _device;

    public void BeginFrame()
    {
        // Veldrid doesn't require explicit frame begin - CommandList.Begin() handles this
    }

    public void EndFrame()
    {
        // Veldrid doesn't require explicit frame end - SubmitCommands handles this
    }

    public void WaitForIdle()
    {
        _device.WaitForIdle();
    }

    // ===== Buffer Operations =====

    public Handle<IBuffer> CreateBuffer(BufferDescription description, ReadOnlySpan<byte> initialData = default)
    {
        // Create Veldrid buffer from description
        var bufferUsage = MapBufferUsageToVeldrid(description.Usage);
        var veldridBuffer = _device.ResourceFactory.CreateBuffer(
            new VeldridLib.BufferDescription(
                description.SizeInBytes,
                bufferUsage
            )
        );

        // If initial data provided, update the buffer
        if (!initialData.IsEmpty && description.Usage != BufferUsage.Dynamic)
        {
            _device.UpdateBuffer(veldridBuffer, 0, initialData);
        }

        // Allocate from pool
        var poolHandle = _bufferPool.Allocate(veldridBuffer);
        var handleId = _nextHandleId++;
        _bufferHandles[handleId] = poolHandle;

        // Return Handle<IBuffer> with handleId
        return new Handle<IBuffer>(handleId, 1);
    }

    public void DestroyBuffer(Handle<IBuffer> buffer)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the pooled Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            // Release from pool (which disposes the resource)
            _bufferPool.Release(poolHandle);
            _bufferHandles.Remove(buffer.Id);
        }
    }

    public IBuffer? GetBuffer(Handle<IBuffer> buffer)
    {
        if (!buffer.IsValid)
            return null;

        // TODO: Implement proper IBuffer wrapper for Veldrid.DeviceBuffer
        // For now, return null (placeholder - this requires creating a full adapter class)
        return null;
    }

    // ===== Texture Operations =====

    public Handle<ITexture> CreateTexture(TextureDescription description, ReadOnlySpan<byte> initialData = default)
    {
        // Create Veldrid texture from description
        var format = MapFormatToVeldrid(description.Format);
        var veldridTexture = _device.ResourceFactory.CreateTexture(
            new VeldridLib.TextureDescription(
                description.Width,
                description.Height,
                description.Depth,
                description.MipLevels,
                description.ArrayLayers,
                format,
                MapTextureUsageToVeldrid(description.IsRenderTarget, description.IsShaderResource),
                VeldridLib.TextureType.Texture2D
            )
        );

        // If initial data provided, update the texture
        if (!initialData.IsEmpty)
        {
            _device.UpdateTexture(veldridTexture, initialData, 0, 0, 0, description.Width, description.Height, 1, 0, 0);
        }

        // Allocate from pool
        var poolHandle = _texturePool.Allocate(veldridTexture);
        var handleId = _nextHandleId++;
        _textureHandles[handleId] = poolHandle;

        return new Handle<ITexture>(handleId, 1);
    }

    public void DestroyTexture(Handle<ITexture> texture)
    {
        if (!texture.IsValid)
            return;

        // Lookup the pooled Veldrid texture
        if (_textureHandles.TryGetValue(texture.Id, out var poolHandle))
        {
            // Release from pool (which disposes the resource)
            _texturePool.Release(poolHandle);
            _textureHandles.Remove(texture.Id);
        }
    }

    public ITexture? GetTexture(Handle<ITexture> texture)
    {
        if (!texture.IsValid)
            return null;

        // TODO: Implement proper ITexture wrapper for Veldrid.Texture
        // For now, return null (placeholder - this requires creating a full adapter class)
        return null;
    }

    // ===== Sampler Operations =====

    public Handle<ISampler> CreateSampler(SamplerDescription description)
    {
        // Create Veldrid sampler with basic default configuration
        // TODO: Map all OpenSAGE sampler properties to Veldrid
        var veldridSampler = _device.ResourceFactory.CreateSampler(
            VeldridLib.SamplerDescription.Linear
        );

        // Allocate from pool
        var poolHandle = _samplerPool.Allocate(veldridSampler);
        var handleId = _nextHandleId++;
        _samplerHandles[handleId] = poolHandle;

        return new Handle<ISampler>(handleId, 1);
    }

    public void DestroySampler(Handle<ISampler> sampler)
    {
        if (!sampler.IsValid)
            return;

        // Lookup the pooled Veldrid sampler
        if (_samplerHandles.TryGetValue(sampler.Id, out var poolHandle))
        {
            // Release from pool (which disposes the resource)
            _samplerPool.Release(poolHandle);
            _samplerHandles.Remove(sampler.Id);
        }
    }

    public ISampler? GetSampler(Handle<ISampler> sampler)
    {
        if (!sampler.IsValid)
            return null;

        // TODO: Implement proper ISampler wrapper for Veldrid.Sampler
        // For now, return null (placeholder - this requires creating a full adapter class)
        return null;
    }

    // ===== Framebuffer Operations =====

    public Handle<IFramebuffer> CreateFramebuffer(FramebufferDescription description)
    {
        // Resolve color target textures from handles
        var colorTargets = new VeldridLib.Texture[description.ColorTargets.Length];
        for (int i = 0; i < description.ColorTargets.Length; i++)
        {
            var handle = description.ColorTargets[i];
            if (!handle.IsValid || !_textureHandles.TryGetValue(handle.Id, out var poolHandle))
            {
                throw new InvalidOperationException($"Invalid color target texture handle at index {i}");
            }
            if (!_texturePool.TryGet(poolHandle, out var texture))
            {
                throw new InvalidOperationException($"Color target texture at index {i} is no longer valid");
            }
            colorTargets[i] = texture;
        }

        // Resolve depth target texture from handle (if provided)
        VeldridLib.Texture? depthTarget = null;
        if (description.DepthTarget.IsValid)
        {
            if (!_textureHandles.TryGetValue(description.DepthTarget.Id, out var poolHandle))
            {
                throw new InvalidOperationException("Invalid depth target texture handle");
            }
            if (!_texturePool.TryGet(poolHandle, out var texture))
            {
                throw new InvalidOperationException("Depth target texture is no longer valid");
            }
            depthTarget = texture;
        }

        // Create Veldrid framebuffer
        var veldridFramebuffer = _device.ResourceFactory.CreateFramebuffer(
            new VeldridLib.FramebufferDescription(depthTarget, colorTargets)
        );

        // Allocate from pool
        var poolHandle_ = _framebufferPool.Allocate(veldridFramebuffer);
        var handleId = _nextHandleId++;
        _framebufferHandles[handleId] = poolHandle_;

        return new Handle<IFramebuffer>(handleId, 1);
    }

    public void DestroyFramebuffer(Handle<IFramebuffer> framebuffer)
    {
        if (!framebuffer.IsValid)
            return;

        // Lookup the pooled Veldrid framebuffer
        if (_framebufferHandles.TryGetValue(framebuffer.Id, out var poolHandle))
        {
            // Release from pool (which disposes the resource)
            _framebufferPool.Release(poolHandle);
            _framebufferHandles.Remove(framebuffer.Id);
        }
    }

    public IFramebuffer? GetFramebuffer(Handle<IFramebuffer> framebuffer)
    {
        if (!framebuffer.IsValid)
            return null;

        // TODO: Implement proper IFramebuffer wrapper for Veldrid.Framebuffer
        // For now, return null (placeholder - this requires creating a full adapter class)
        return null;
    }

    // ===== Shader Operations =====

    public Handle<IShaderProgram> CreateShader(string name, VeldridLib.ShaderStages stage, ReadOnlySpan<byte> spirvData, string entryPoint = "main")
    {
        try
        {
            // Create shader description for the given stage
            var shaderDescription = new VeldridLib.ShaderDescription(stage, spirvData.ToArray(), entryPoint);
            
            // Create cross-compile options for the current backend
            // Using default options which work for most cases
            var options = new CrossCompileOptions();
            
            // Compile SPIR-V to backend-specific shader format via Veldrid.SPIRV
            // Note: Single ShaderDescription returns a single Shader (not an array)
            var shader = _device.ResourceFactory.CreateFromSpirv(shaderDescription, options);
            
            shader.Name = name;
            
            // Create wrapper and store it
            var handleId = _nextHandleId++;
            var wrapper = new VeldridShaderProgram(name, shader, entryPoint, handleId, 1);
            
            // Store wrapper in dictionary for GetShader() calls
            _shaders[handleId] = wrapper;
            
            return new Handle<IShaderProgram>(handleId, 1);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create shader '{name}': {ex.Message}", ex);
        }
    }

    public void DestroyShader(Handle<IShaderProgram> shader)
    {
        if (_shaders.TryGetValue(shader.Id, out var shaderProgram))
        {
            shaderProgram.Dispose();
            _shaders.Remove(shader.Id);
        }
    }

    public IShaderProgram? GetShader(Handle<IShaderProgram> shader)
    {
        return _shaders.TryGetValue(shader.Id, out var shaderProgram) ? shaderProgram : null;
    }

    // ===== Pipeline Operations =====

    public Handle<IPipeline> CreatePipeline(
        Handle<IShaderProgram> vertexShader,
        Handle<IShaderProgram> fragmentShader,
        State.RasterState rasterState = default,
        State.DepthState depthState = default,
        State.BlendState blendState = default,
        State.StencilState stencilState = default)
    {
        try
        {
            // Retrieve shader programs from handles
            var vsProgram = GetShader(vertexShader);
            var fsProgram = GetShader(fragmentShader);
            
            if (vsProgram == null)
                throw new InvalidOperationException($"Vertex shader handle {vertexShader.Id} is invalid");
            if (fsProgram == null)
                throw new InvalidOperationException($"Fragment shader handle {fragmentShader.Id} is invalid");
            
            // Cast to Veldrid implementation
            var vsVeldrid = (VeldridShaderProgram)vsProgram;
            var fsVeldrid = (VeldridShaderProgram)fsProgram;
            
            // Get Veldrid shaders
            var vs = vsVeldrid.VeldridShader;
            var fs = fsVeldrid.VeldridShader;
            
            // Create shader set description (minimal for now - empty vertex layout)
            // TODO: Accept vertex layout description as parameter
            var shaderSet = new VeldridLib.ShaderSetDescription(
                Array.Empty<VeldridLib.VertexLayoutDescription>(),
                new[] { vs, fs });
            
            // Convert OpenSAGE state to Veldrid state
            var rasterizerState = MapRasterState(rasterState);
            var depthStencilState = MapDepthStencilState(depthState, stencilState);
            var blendStateDesc = MapBlendState(blendState);
            
            // Create graphics pipeline description
            var pipelineDesc = new VeldridLib.GraphicsPipelineDescription
            {
                BlendState = blendStateDesc,
                DepthStencilState = depthStencilState,
                RasterizerState = rasterizerState,
                PrimitiveTopology = VeldridLib.PrimitiveTopology.TriangleList,
                ShaderSet = shaderSet,
                ResourceLayouts = Array.Empty<VeldridLib.ResourceLayout>(), // TODO: Add resource layouts
                Outputs = _device.SwapchainFramebuffer.OutputDescription
            };
            
            // Create the pipeline
            var pipeline = _device.ResourceFactory.CreateGraphicsPipeline(ref pipelineDesc);
            
            // Store in dictionary
            var handleId = _nextHandleId++;
            _pipelines[handleId] = pipeline;
            
            return new Handle<IPipeline>(handleId, 1);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to create pipeline: {ex.Message}", ex);
        }
    }

    public void DestroyPipeline(Handle<IPipeline> pipeline)
    {
        if (_pipelines.TryGetValue(pipeline.Id, out var veldridPipeline))
        {
            veldridPipeline?.Dispose();
            _pipelines.Remove(pipeline.Id);
        }
    }

    public IPipeline? GetPipeline(Handle<IPipeline> pipeline)
    {
        // TODO: Implement IPipeline wrapper for Veldrid.Pipeline
        return null;
    }

    // ===== Rendering Operations =====

    public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
    {
        if (!framebuffer.IsValid)
            return;

        // Lookup the Veldrid framebuffer
        if (_framebufferHandles.TryGetValue(framebuffer.Id, out var poolHandle))
        {
            if (_framebufferPool.TryGet(poolHandle, out var veldridFramebuffer))
            {
                _currentFramebuffer = veldridFramebuffer;
                _commandList.SetFramebuffer(veldridFramebuffer);
            }
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
        if (_currentFramebuffer == null)
            return;

        // Clear color targets
        if (colorMask)
        {
            for (uint i = 0; i < (uint)_currentFramebuffer.ColorTargets.Count; i++)
            {
                _commandList.ClearColorTarget(i, new VeldridLib.RgbaFloat(clearColor.X, clearColor.Y, clearColor.Z, clearColor.W));
            }
        }

        // Clear depth/stencil target
        if (depthMask || stencilMask)
        {
            _commandList.ClearDepthStencil(clearDepth, clearStencil);
        }
    }

    public void SetPipeline(Handle<IPipeline> pipeline)
    {
        if (!pipeline.IsValid)
            return;

        // Lookup the Veldrid pipeline
        if (_pipelines.TryGetValue(pipeline.Id, out var veldridPipeline))
        {
            _currentPipeline = veldridPipeline;
            _commandList.SetPipeline(veldridPipeline);
        }
    }

    public void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f)
    {
        var viewport = new VeldridLib.Viewport(x, y, width, height, minDepth, maxDepth);
        _commandList.SetViewport(0, viewport);
    }

    public void SetScissor(int x, int y, int width, int height)
    {
        _commandList.SetScissorRect(0, (uint)x, (uint)y, (uint)width, (uint)height);
    }

    public void BindVertexBuffer(Handle<IBuffer> buffer, uint offset = 0)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            if (_bufferPool.TryGet(poolHandle, out var veldridBuffer))
            {
                _commandList.SetVertexBuffer(_boundVertexBufferSlot, veldridBuffer, offset);
                _boundVertexBufferSlot++;
            }
        }
    }

    public void BindIndexBuffer(Handle<IBuffer> buffer, uint offset = 0)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            if (_bufferPool.TryGet(poolHandle, out var veldridBuffer))
            {
                _boundIndexBufferOffset = offset;
                // Note: SetIndexBuffer in Veldrid CommandList requires IndexFormat parameter
                // For now, assume standard IndexFormat.UInt32
                _commandList.SetIndexBuffer(veldridBuffer, VeldridLib.IndexFormat.UInt32, offset);
            }
        }
    }

    public void BindUniformBuffer(Handle<IBuffer> buffer, uint slot)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            if (_bufferPool.TryGet(poolHandle, out var veldridBuffer))
            {
                // TODO: Need to track resource sets to bind uniform buffers
                // This requires CreatePipeline to set up resource sets first
            }
        }
    }

    public void BindTexture(Handle<ITexture> texture, uint slot, Handle<ISampler> sampler)
    {
        if (!texture.IsValid || !sampler.IsValid)
            return;

        // TODO: Implement texture binding via resource sets
        // This requires CreatePipeline to set up resource sets first
    }

    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0)
    {
        _commandList.DrawIndexed(indexCount, instanceCount, startIndex, baseVertex, startInstance);
    }

    public void DrawVertices(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0)
    {
        _commandList.Draw(vertexCount, instanceCount, startVertex, startInstance);
    }

    public void DrawIndexedIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            if (_bufferPool.TryGet(poolHandle, out var veldridBuffer))
            {
                // Veldrid's DrawIndexedIndirect expects drawCount and stride
                for (uint i = 0; i < drawCount; i++)
                {
                    _commandList.DrawIndexedIndirect(veldridBuffer, offset + (i * stride), 1, stride);
                }
            }
        }
    }

    public void DrawVerticesIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride)
    {
        if (!buffer.IsValid)
            return;

        // Lookup the Veldrid buffer
        if (_bufferHandles.TryGetValue(buffer.Id, out var poolHandle))
        {
            if (_bufferPool.TryGet(poolHandle, out var veldridBuffer))
            {
                // Veldrid's DrawIndirect expects drawCount and stride
                for (uint i = 0; i < drawCount; i++)
                {
                    _commandList.DrawIndirect(veldridBuffer, offset + (i * stride), 1, stride);
                }
            }
        }
    }

    // ===== Helper Methods for Veldrid Conversion =====

    private VeldridLib.BufferUsage MapBufferUsageToVeldrid(BufferUsage usage)
    {
        return usage switch
        {
            BufferUsage.Static => VeldridLib.BufferUsage.VertexBuffer,
            BufferUsage.Dynamic => VeldridLib.BufferUsage.Dynamic,
            BufferUsage.Stream => VeldridLib.BufferUsage.VertexBuffer,
            _ => VeldridLib.BufferUsage.VertexBuffer,
        };
    }

    private VeldridLib.PixelFormat MapFormatToVeldrid(PixelFormat format)
    {
        return format switch
        {
            PixelFormat.R8G8B8A8_SRgb => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm_SRgb,
            PixelFormat.R8G8B8A8_UNorm => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm,
            PixelFormat.B8G8R8A8_SRgb => VeldridLib.PixelFormat.B8_G8_R8_A8_UNorm_SRgb,
            PixelFormat.B8G8R8A8_UNorm => VeldridLib.PixelFormat.B8_G8_R8_A8_UNorm,
            PixelFormat.D32_Float => VeldridLib.PixelFormat.R32_Float,
            PixelFormat.D24_UNorm_S8_UInt => VeldridLib.PixelFormat.D24_UNorm_S8_UInt,
            PixelFormat.D32_Float_S8_UInt => VeldridLib.PixelFormat.D32_Float_S8_UInt,
            _ => VeldridLib.PixelFormat.R8_G8_B8_A8_UNorm,
        };
    }

    private VeldridLib.TextureUsage MapTextureUsageToVeldrid(bool isRenderTarget, bool isShaderResource)
    {
        var usage = VeldridLib.TextureUsage.Sampled;
        if (isRenderTarget)
            usage |= VeldridLib.TextureUsage.RenderTarget;
        if (!isShaderResource)
            usage &= ~VeldridLib.TextureUsage.Sampled;
        return usage;
    }

    private VeldridLib.SamplerAddressMode MapAddressMode(SamplerAddressMode mode)
    {
        return mode switch
        {
            SamplerAddressMode.Wrap => VeldridLib.SamplerAddressMode.Wrap,
            SamplerAddressMode.Mirror => VeldridLib.SamplerAddressMode.Mirror,
            SamplerAddressMode.Clamp => VeldridLib.SamplerAddressMode.Clamp,
            SamplerAddressMode.Border => VeldridLib.SamplerAddressMode.Border,
            _ => VeldridLib.SamplerAddressMode.Wrap,
        };
    }

    // ===== Graphics State Mapping =====

    private VeldridLib.RasterizerStateDescription MapRasterState(State.RasterState state)
    {
        return new VeldridLib.RasterizerStateDescription
        {
            FillMode = state.FillMode == State.FillMode.Solid
                ? VeldridLib.PolygonFillMode.Solid
                : VeldridLib.PolygonFillMode.Wireframe,
            CullMode = state.CullMode switch
            {
                State.CullMode.None => VeldridLib.FaceCullMode.None,
                State.CullMode.Front => VeldridLib.FaceCullMode.Front,
                State.CullMode.Back => VeldridLib.FaceCullMode.Back,
                _ => VeldridLib.FaceCullMode.Back,
            },
            FrontFace = state.FrontFace == State.FrontFace.CounterClockwise
                ? VeldridLib.FrontFace.CounterClockwise
                : VeldridLib.FrontFace.Clockwise,
            DepthClipEnabled = !state.DepthClamp,
            ScissorTestEnabled = state.ScissorTest,
        };
    }

    private VeldridLib.DepthStencilStateDescription MapDepthStencilState(State.DepthState depthState, State.StencilState stencilState)
    {
        return new VeldridLib.DepthStencilStateDescription
        {
            DepthTestEnabled = depthState.TestEnabled,
            DepthWriteEnabled = depthState.WriteEnabled,
            DepthComparison = MapCompareFunction(depthState.CompareFunction),
            StencilTestEnabled = false, // TODO: Implement stencil state mapping
            StencilReadMask = 0xFF,
            StencilWriteMask = 0xFF,
        };
    }

    private VeldridLib.BlendStateDescription MapBlendState(State.BlendState state)
    {
        if (!state.Enabled)
        {
            return VeldridLib.BlendStateDescription.SingleDisabled;
        }

        return VeldridLib.BlendStateDescription.SingleAlphaBlend;
    }

    private VeldridLib.ComparisonKind MapCompareFunction(State.CompareFunction func)
    {
        return func switch
        {
            State.CompareFunction.Never => VeldridLib.ComparisonKind.Never,
            State.CompareFunction.Less => VeldridLib.ComparisonKind.Less,
            State.CompareFunction.Equal => VeldridLib.ComparisonKind.Equal,
            State.CompareFunction.LessEqual => VeldridLib.ComparisonKind.LessEqual,
            State.CompareFunction.Greater => VeldridLib.ComparisonKind.Greater,
            State.CompareFunction.NotEqual => VeldridLib.ComparisonKind.NotEqual,
            State.CompareFunction.GreaterEqual => VeldridLib.ComparisonKind.GreaterEqual,
            State.CompareFunction.Always => VeldridLib.ComparisonKind.Always,
            _ => VeldridLib.ComparisonKind.Less,
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _bufferPool?.Dispose();
            _texturePool?.Dispose();
            _samplerPool?.Dispose();
            _framebufferPool?.Dispose();

            _bufferHandles?.Clear();
            _textureHandles?.Clear();
            _samplerHandles?.Clear();
            _framebufferHandles?.Clear();
        }
        base.Dispose(disposing);
    }
}
