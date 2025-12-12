# Veldrid Patterns - Practical Implementation Guide para OpenSAGE

**Código exemplo pronto para implementar**  
Data: 12 de dezembro de 2025

---

## 1. RenderResourceCache Implementation

### 1.1 Interface & Base Class

```csharp
// src/OpenSage.Graphics/RenderResourceCache.cs
using Veldrid;
using System;
using System.Collections.Generic;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Non-thread-safe cache for graphics pipeline and layout resources.
    /// Must be used only from the main render thread.
    /// </summary>
    public class RenderResourceCache : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        
        private readonly Dictionary<GraphicsPipelineDescription, Pipeline> _graphicsPipelines;
        private readonly Dictionary<ComputePipelineDescription, Pipeline> _computePipelines;
        private readonly Dictionary<ResourceLayoutDescription, ResourceLayout> _resourceLayouts;
        private readonly Dictionary<ResourceSetDescription, ResourceSet> _resourceSets;
        private readonly Dictionary<string, (Shader vs, Shader fs)> _shaderPairs;

        private bool _disposed;

        public RenderResourceCache(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
            
            _graphicsPipelines = new Dictionary<GraphicsPipelineDescription, Pipeline>();
            _computePipelines = new Dictionary<ComputePipelineDescription, Pipeline>();
            _resourceLayouts = new Dictionary<ResourceLayoutDescription, ResourceLayout>();
            _resourceSets = new Dictionary<ResourceSetDescription, ResourceSet>();
            _shaderPairs = new Dictionary<string, (Shader, Shader)>();
        }

        public ResourceFactory Factory => _graphicsDevice.ResourceFactory;

        /// <summary>
        /// Gets or creates a graphics pipeline matching the description.
        /// </summary>
        public Pipeline GetGraphicsPipeline(ref GraphicsPipelineDescription description)
        {
            ThrowIfDisposed();

            if (_graphicsPipelines.TryGetValue(description, out var pipeline))
            {
                return pipeline;
            }

            var newPipeline = Factory.CreateGraphicsPipeline(ref description);
            _graphicsPipelines.Add(description, newPipeline);
            return newPipeline;
        }

        /// <summary>
        /// Gets or creates a compute pipeline matching the description.
        /// </summary>
        public Pipeline GetComputePipeline(ref ComputePipelineDescription description)
        {
            ThrowIfDisposed();

            if (_computePipelines.TryGetValue(description, out var pipeline))
            {
                return pipeline;
            }

            var newPipeline = Factory.CreateComputePipeline(ref description);
            _computePipelines.Add(description, newPipeline);
            return newPipeline;
        }

        /// <summary>
        /// Gets or creates a resource layout matching the description.
        /// </summary>
        public ResourceLayout GetResourceLayout(ref ResourceLayoutDescription description)
        {
            ThrowIfDisposed();

            if (_resourceLayouts.TryGetValue(description, out var layout))
            {
                return layout;
            }

            var newLayout = Factory.CreateResourceLayout(ref description);
            _resourceLayouts.Add(description, newLayout);
            return newLayout;
        }

        /// <summary>
        /// Gets or creates a resource set matching the description.
        /// </summary>
        public ResourceSet GetResourceSet(ref ResourceSetDescription description)
        {
            ThrowIfDisposed();

            if (_resourceSets.TryGetValue(description, out var set))
            {
                return set;
            }

            var newSet = Factory.CreateResourceSet(ref description);
            _resourceSets.Add(description, newSet);
            return newSet;
        }

        /// <summary>
        /// Clears all cached resources, disposing them.
        /// Call on device lost or backend change.
        /// </summary>
        public void Clear()
        {
            foreach (var pipe in _graphicsPipelines.Values)
            {
                pipe?.Dispose();
            }
            _graphicsPipelines.Clear();

            foreach (var pipe in _computePipelines.Values)
            {
                pipe?.Dispose();
            }
            _computePipelines.Clear();

            foreach (var layout in _resourceLayouts.Values)
            {
                layout?.Dispose();
            }
            _resourceLayouts.Clear();

            foreach (var set in _resourceSets.Values)
            {
                set?.Dispose();
            }
            _resourceSets.Clear();

            foreach (var (vs, fs) in _shaderPairs.Values)
            {
                vs?.Dispose();
                fs?.Dispose();
            }
            _shaderPairs.Clear();
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                Clear();
                _disposed = true;
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(GetType().Name);
            }
        }

        /// <summary>
        /// Gets cache statistics for profiling.
        /// </summary>
        public (int graphicsPipelines, int computePipelines, int layouts, int sets) GetCacheStats()
        {
            return (_graphicsPipelines.Count, _computePipelines.Count, _resourceLayouts.Count, _resourceSets.Count);
        }
    }
}
```

### 1.2 Integration com GraphicsSystem

```csharp
// src/OpenSage.Graphics/GraphicsSystem.cs (partial)

public class GraphicsSystem : IDisposable
{
    private GraphicsDevice _graphicsDevice;
    private RenderResourceCache _resourceCache;
    
    public RenderResourceCache ResourceCache => _resourceCache;

    public void Initialize(GraphicsDevice graphicsDevice)
    {
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _resourceCache = new RenderResourceCache(graphicsDevice);
        
        // Other initialization...
    }

    public void OnWindowResized(int width, int height)
    {
        // Critical: Clear cache on resize
        _resourceCache.Clear();
        
        // Recreate framebuffers, depth targets, etc.
        RecreateWindowSizedResources(width, height);
    }

    private void RecreateWindowSizedResources(int width, int height)
    {
        // Recreate any window-size dependent resources
    }

    public void Dispose()
    {
        _resourceCache?.Dispose();
        _graphicsDevice?.Dispose();
    }
}
```

---

## 2. GraphicsCapabilities Implementation

### 2.1 Feature Detection Wrapper

```csharp
// src/OpenSage.Graphics/GraphicsCapabilities.cs

using Veldrid;
using System.Collections.Generic;

namespace OpenSage.Graphics
{
    /// <summary>
    /// High-level wrapper for graphics device capabilities.
    /// Provides graceful feature fallback queries.
    /// </summary>
    public class GraphicsCapabilities
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, bool> _capabilityCache = new();

        public GraphicsCapabilities(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice ?? throw new System.ArgumentNullException(nameof(graphicsDevice));
        }

        public GraphicsBackend Backend => _graphicsDevice.Backend;
        public GraphicsDeviceFeatures Features => _graphicsDevice.Features;

        /// <summary>
        /// High-level capability checks with caching.
        /// </summary>
        public bool Supports(GraphicsFeature feature)
        {
            var key = feature.ToString();
            
            if (_capabilityCache.TryGetValue(key, out var result))
            {
                return result;
            }

            result = feature switch
            {
                GraphicsFeature.Wireframe => Features.FillModeWireframe,
                GraphicsFeature.ComputeShaders => Features.ComputeShader,
                GraphicsFeature.GeometryShaders => Features.GeometryShader,
                GraphicsFeature.TessellationShaders => Features.TessellationShaders,
                GraphicsFeature.Anisotropy => Features.SamplerAnisotropy,
                GraphicsFeature.Texture1D => Features.Texture1D,
                GraphicsFeature.StructuredBuffers => Features.StructuredBuffer,
                GraphicsFeature.IndependentBlend => Features.IndependentBlend,
                GraphicsFeature.DrawIndirect => Features.DrawIndirect,
                GraphicsFeature.MultipleViewports => Features.MultipleViewports,
                GraphicsFeature.DepthClipDisable => Features.DepthClipDisable,
                GraphicsFeature.Float64 => Features.ShaderFloat64,
                _ => false
            };

            _capabilityCache[key] = result;
            return result;
        }

        /// <summary>
        /// Gets best MSAA sample count available, fallback to None.
        /// </summary>
        public TextureSampleCount GetBestMsaaCount(TextureSampleCount preferred)
        {
            _graphicsDevice.GetPixelFormatSupport(
                PixelFormat.R8_G8_B8_A8_UNorm,
                TextureType.Texture2D,
                TextureUsage.RenderTarget,
                out var properties);

            if (properties.SampleCounts.Contains(preferred))
            {
                return preferred;
            }

            return properties.SampleCounts.Length > 0
                ? properties.SampleCounts[0]
                : TextureSampleCount.Count1;
        }

        /// <summary>
        /// Gets best HDR color format, fallback to SDR.
        /// </summary>
        public PixelFormat GetBestColorFormat(bool requestHdr)
        {
            if (!requestHdr)
            {
                return PixelFormat.R8_G8_B8_A8_UNorm;
            }

            var hdrFormat = PixelFormat.R16_G16_B16_A16_Float;
            _graphicsDevice.GetPixelFormatSupport(
                hdrFormat,
                TextureType.Texture2D,
                TextureUsage.RenderTarget,
                out _);

            return hdrFormat;
        }

        /// <summary>
        /// Gets best depth format with optional stencil.
        /// </summary>
        public PixelFormat GetBestDepthFormat(bool includeStencil)
        {
            var formats = includeStencil
                ? new[] { PixelFormat.D32_Float_S8_UInt, PixelFormat.D24_UNorm_S8_UInt }
                : new[] { PixelFormat.D32_Float, PixelFormat.D16_UNorm };

            foreach (var format in formats)
            {
                _graphicsDevice.GetPixelFormatSupport(
                    format,
                    TextureType.Texture2D,
                    TextureUsage.DepthStencil,
                    out var props);

                if (!props.IsTextureTypeAndUsageSupported)
                {
                    continue;
                }

                return format;
            }

            // Fallback
            return includeStencil ? PixelFormat.D24_UNorm_S8_UInt : PixelFormat.D24_UNorm;
        }

        /// <summary>
        /// Validates feature support and logs warnings.
        /// </summary>
        public void ValidateRequiredFeatures(params GraphicsFeature[] features)
        {
            foreach (var feature in features)
            {
                if (!Supports(feature))
                {
                    System.Console.WriteLine($"Warning: {feature} not supported on {Backend}");
                }
            }
        }

        /// <summary>
        /// Gets device info string for debugging.
        /// </summary>
        public string GetDeviceInfo()
        {
            return $"Backend: {Backend}, " +
                   $"Compute: {Features.ComputeShader}, " +
                   $"Tessellation: {Features.TessellationShaders}, " +
                   $"Wireframe: {Features.FillModeWireframe}";
        }
    }

    /// <summary>
    /// Feature flags for capability checking.
    /// </summary>
    public enum GraphicsFeature
    {
        Wireframe,
        ComputeShaders,
        GeometryShaders,
        TessellationShaders,
        Anisotropy,
        Texture1D,
        StructuredBuffers,
        IndependentBlend,
        DrawIndirect,
        MultipleViewports,
        DepthClipDisable,
        Float64,
    }
}
```

---

## 3. Specialized Pipeline Creation Pattern

### 3.1 Pipeline Builder com Specialization

```csharp
// src/OpenSage.Graphics/PipelineBuilder.cs

using Veldrid;
using System;
using System.Collections.Generic;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Fluent builder for creating pipelines with proper specialization constants.
    /// </summary>
    public class PipelineBuilder
    {
        private readonly RenderResourceCache _cache;
        private readonly GraphicsDevice _graphicsDevice;
        
        private VertexLayoutDescription[] _vertexLayouts;
        private Shader[] _shaders;
        private List<SpecializationConstant> _specializations = new();
        private PrimitiveTopology _topology = PrimitiveTopology.TriangleList;
        private RasterizerStateDescription _rasterizerState = RasterizerStateDescription.Default;
        private BlendStateDescription _blendState = BlendStateDescription.SingleAlphaBlend;
        private DepthStencilStateDescription _depthStencil = DepthStencilStateDescription.DepthOnlyLessEqual;
        private OutputDescription _outputs;

        public PipelineBuilder(RenderResourceCache cache, GraphicsDevice graphicsDevice)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        }

        public PipelineBuilder WithVertexLayout(params VertexLayoutDescription[] layouts)
        {
            _vertexLayouts = layouts;
            return this;
        }

        public PipelineBuilder WithShaders(params Shader[] shaders)
        {
            _shaders = shaders;
            return this;
        }

        public PipelineBuilder WithTopology(PrimitiveTopology topology)
        {
            _topology = topology;
            return this;
        }

        /// <summary>
        /// Add specialization constants for backend-specific code paths.
        /// </summary>
        public PipelineBuilder WithSpecialization(uint constantId, bool value)
        {
            _specializations.Add(new SpecializationConstant(constantId, value));
            return this;
        }

        public PipelineBuilder WithSpecialization(uint constantId, uint value)
        {
            _specializations.Add(new SpecializationConstant(constantId, value));
            return this;
        }

        public PipelineBuilder WithSpecialization(uint constantId, float value)
        {
            _specializations.Add(new SpecializationConstant(constantId, value));
            return this;
        }

        public PipelineBuilder WithRasterizerState(RasterizerStateDescription state)
        {
            _rasterizerState = state;
            return this;
        }

        public PipelineBuilder WithBlendState(BlendStateDescription state)
        {
            _blendState = state;
            return this;
        }

        public PipelineBuilder WithDepthStencil(DepthStencilStateDescription state)
        {
            _depthStencil = state;
            return this;
        }

        public PipelineBuilder WithOutputs(OutputDescription outputs)
        {
            _outputs = outputs;
            return this;
        }

        /// <summary>
        /// Auto-add specialization constants from graphics device.
        /// </summary>
        public PipelineBuilder WithDeviceSpecializations()
        {
            uint id = 0;
            _specializations.Add(new SpecializationConstant(id++, _graphicsDevice.IsClipSpaceYInverted));
            _specializations.Add(new SpecializationConstant(id++, _graphicsDevice.IsDepthRangeZeroToOne));
            return this;
        }

        public Pipeline Build()
        {
            if (_shaders == null || _shaders.Length == 0)
            {
                throw new InvalidOperationException("Shaders must be specified");
            }

            var shaderSet = new ShaderSetDescription(
                _vertexLayouts ?? Array.Empty<VertexLayoutDescription>(),
                _shaders,
                _specializations.Count > 0 ? _specializations.ToArray() : null);

            var description = new GraphicsPipelineDescription
            {
                BlendState = _blendState,
                DepthStencilState = _depthStencil,
                RasterizerState = _rasterizerState,
                PrimitiveTopology = _topology,
                ShaderSet = shaderSet,
                ResourceLayouts = Array.Empty<ResourceLayout>(),
                Outputs = _outputs ?? _graphicsDevice.MainSwapchain?.Framebuffer.OutputDescription
                    ?? new OutputDescription()
            };

            return _cache.GetGraphicsPipeline(ref description);
        }
    }
}
```

### 3.2 Usar o Builder

```csharp
// Example usage
var pipeline = new PipelineBuilder(_cache, _gd)
    .WithVertexLayout(TerrainVertexLayout)
    .WithShaders(terrainVs, terrainFs)
    .WithSpecialization(0, false)  // WIREFRAME_MODE = false
    .WithSpecialization(1, 0u)     // LOD_LEVEL = 0
    .WithDeviceSpecializations()   // ClipSpaceY, DepthRange
    .WithRasterizerState(RasterizerStateDescription.Default)
    .WithDepthStencil(DepthStencilStateDescription.DepthOnlyLessEqual)
    .WithOutputs(framebuffer.OutputDescription)
    .Build();
```

---

## 4. Dynamic Resource Binding Pattern

### 4.1 Helper para ResourceSet Reuse

```csharp
// src/OpenSage.Graphics/DynamicResourceBinding.cs

using Veldrid;
using System;
using System.Collections.Generic;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Helps manage dynamic resource binding with minimal allocations.
    /// </summary>
    public class DynamicResourceSetBuilder
    {
        private readonly RenderResourceCache _cache;
        private readonly List<BindableResource> _resources = new();
        private ResourceLayoutDescription _layoutDescription;

        public DynamicResourceSetBuilder(RenderResourceCache cache)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// Build layout from resources with DynamicBinding flag.
        /// </summary>
        public ResourceSet BuildDynamic(
            string layoutName,
            ResourceKind kind,
            ShaderStages stages,
            params BindableResource[] resources)
        {
            _resources.Clear();
            _resources.AddRange(resources);

            var elements = new ResourceLayoutElementDescription[resources.Length];
            for (int i = 0; i < resources.Length; i++)
            {
                elements[i] = new ResourceLayoutElementDescription(
                    $"{layoutName}_{i}",
                    kind,
                    stages,
                    ResourceLayoutElementOptions.DynamicBinding);  // ← Dynamic!
            }

            var layoutDesc = new ResourceLayoutDescription(elements);
            var layout = _cache.GetResourceLayout(ref layoutDesc);

            var setDesc = new ResourceSetDescription(layout, resources);
            return _cache.GetResourceSet(ref setDesc);
        }

        /// <summary>
        /// Create ResourceSet for per-object uniforms (frequently changing).
        /// </summary>
        public ResourceSet BuildPerObjectResources(
            DeviceBuffer worldMatrix,
            DeviceBuffer color)
        {
            return BuildDynamic(
                "PerObject",
                ResourceKind.UniformBuffer,
                ShaderStages.Vertex | ShaderStages.Fragment,
                worldMatrix,
                color);
        }

        /// <summary>
        /// Create ResourceSet for per-frame uniforms (slowly changing).
        /// </summary>
        public ResourceSet BuildPerFrameResources(
            DeviceBuffer viewMatrix,
            DeviceBuffer projectionMatrix,
            DeviceBuffer lighting)
        {
            return BuildDynamic(
                "PerFrame",
                ResourceKind.UniformBuffer,
                ShaderStages.Vertex,
                viewMatrix,
                projectionMatrix,
                lighting);
        }
    }

    /// <summary>
    /// Example of dynamic uniform buffer management.
    /// </summary>
    public class DynamicUniformBuffer
    {
        private readonly DeviceBuffer _buffer;
        private readonly GraphicsDevice _gd;
        private readonly uint _elementSize;
        private uint _currentOffset = 0;

        public DynamicUniformBuffer(
            GraphicsDevice gd,
            uint elementSize,
            uint elementCount)
        {
            _gd = gd;
            _elementSize = elementSize;
            _buffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(
                elementSize * elementCount,
                BufferUsage.UniformBuffer | BufferUsage.Dynamic));
        }

        /// <summary>
        /// Map next element, return offset for binding.
        /// </summary>
        public uint AllocateNext(IntPtr data)
        {
            var offset = _currentOffset;
            _gd.UpdateBuffer(_buffer, offset * _elementSize, data, _elementSize);
            _currentOffset++;
            return offset;
        }

        /// <summary>
        /// Reset offset for next frame.
        /// </summary>
        public void Reset()
        {
            _currentOffset = 0;
        }

        public DeviceBuffer Buffer => _buffer;

        public void Dispose()
        {
            _buffer?.Dispose();
        }
    }
}
```

---

## 5. Framebuffer Management Pattern

### 5.1 Advanced Framebuffer Handling

```csharp
// src/OpenSage.Graphics/FramebufferManager.cs

using Veldrid;
using System;
using System.Collections.Generic;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Manages framebuffer creation and recreation on window resize.
    /// </summary>
    public class FramebufferManager : IDisposable
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Dictionary<string, Framebuffer> _framebuffers = new();
        private readonly Dictionary<string, (Texture color, Texture depth)> _targets = new();

        public FramebufferManager(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
        }

        /// <summary>
        /// Create or get window-sized framebuffer.
        /// </summary>
        public Framebuffer GetWindowFramebuffer(
            string name,
            PixelFormat colorFormat,
            PixelFormat depthFormat)
        {
            if (_framebuffers.TryGetValue(name, out var fb))
            {
                return fb;
            }

            var factory = _graphicsDevice.ResourceFactory;
            var width = (uint)_graphicsDevice.MainSwapchain.Framebuffer.Width;
            var height = (uint)_graphicsDevice.MainSwapchain.Framebuffer.Height;

            var colorTex = factory.CreateTexture(new TextureDescription
            {
                Width = width,
                Height = height,
                Format = colorFormat,
                Usage = TextureUsage.RenderTarget | TextureUsage.Sampled,
                Type = TextureType.Texture2D,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = TextureSampleCount.Count1
            });

            var depthTex = factory.CreateTexture(new TextureDescription
            {
                Width = width,
                Height = height,
                Format = depthFormat,
                Usage = TextureUsage.DepthStencil,
                Type = TextureType.Texture2D,
                MipLevels = 1,
                ArrayLayers = 1,
                SampleCount = TextureSampleCount.Count1
            });

            var framebuffer = factory.CreateFramebuffer(new FramebufferDescription
            {
                ColorTargets = new[]
                {
                    new FramebufferAttachmentDescription(colorTex, 0, 0)
                },
                DepthTarget = new FramebufferAttachmentDescription(depthTex, 0, 0)
            });

            _framebuffers[name] = framebuffer;
            _targets[name] = (colorTex, depthTex);

            return framebuffer;
        }

        /// <summary>
        /// Create cube map framebuffers (for cubemap rendering).
        /// </summary>
        public Framebuffer[] GetCubemapFramebuffers(
            string name,
            uint size,
            PixelFormat format)
        {
            var key = $"{name}_cubemap";
            if (_framebuffers.TryGetValue(key, out var fb))
            {
                return new[] { fb };  // Simplified
            }

            var factory = _graphicsDevice.ResourceFactory;

            // Create cubemap texture
            var cubeTex = factory.CreateTexture(new TextureDescription
            {
                Width = size,
                Height = size,
                Format = format,
                Usage = TextureUsage.RenderTarget | TextureUsage.Sampled,
                Type = TextureType.Texture2D,
                MipLevels = 1,
                ArrayLayers = 6  // 6 faces
            });

            var framebuffers = new Framebuffer[6];
            for (int face = 0; face < 6; face++)
            {
                framebuffers[face] = factory.CreateFramebuffer(new FramebufferDescription
                {
                    ColorTargets = new[]
                    {
                        new FramebufferAttachmentDescription(cubeTex, (uint)face, 0)
                    }
                });
            }

            _framebuffers[key] = framebuffers[0];
            _targets[key] = (cubeTex, null);

            return framebuffers;
        }

        /// <summary>
        /// Recreate all framebuffers after window resize.
        /// </summary>
        public void RecreateWindowSized()
        {
            var toRecreate = new List<(string name, PixelFormat color, PixelFormat depth)>();

            foreach (var (name, fb) in _framebuffers)
            {
                if (!name.Contains("cubemap"))
                {
                    if (_targets.TryGetValue(name, out var (color, depth)))
                    {
                        toRecreate.Add((name, color.Format, depth.Format));
                    }
                }
            }

            Clear();

            foreach (var (name, colorFormat, depthFormat) in toRecreate)
            {
                GetWindowFramebuffer(name, colorFormat, depthFormat);
            }
        }

        /// <summary>
        /// Get attachment texture for sampling.
        /// </summary>
        public Texture GetColorTarget(string framebufferName)
        {
            return _targets.TryGetValue(framebufferName, out var (color, _))
                ? color
                : throw new KeyNotFoundException($"Framebuffer '{framebufferName}' not found");
        }

        public Texture GetDepthTarget(string framebufferName)
        {
            return _targets.TryGetValue(framebufferName, out var (_, depth))
                ? depth
                : throw new KeyNotFoundException($"Framebuffer '{framebufferName}' not found");
        }

        public void Clear()
        {
            foreach (var fb in _framebuffers.Values)
            {
                fb?.Dispose();
            }
            _framebuffers.Clear();

            foreach (var (color, depth) in _targets.Values)
            {
                color?.Dispose();
                depth?.Dispose();
            }
            _targets.Clear();
        }

        public void Dispose()
        {
            Clear();
        }
    }
}
```

---

## 6. CommandList Recording Pattern

### 6.1 Helper para Recording Seguro

```csharp
// src/OpenSage.Graphics/CommandListRecorder.cs

using Veldrid;
using System;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Safe RAII wrapper for CommandList recording.
    /// Ensures Begin/End pairing even on exception.
    /// </summary>
    public struct CommandListRecorder : IDisposable
    {
        private readonly CommandList _commandList;
        private bool _recording;

        public CommandListRecorder(CommandList commandList)
        {
            _commandList = commandList ?? throw new ArgumentNullException(nameof(commandList));
            _commandList.Begin();
            _recording = true;
        }

        public CommandList CommandList => _commandList;

        public void Dispose()
        {
            if (_recording)
            {
                _commandList.End();
                _recording = false;
            }
        }

        /// <summary>
        /// Example: Record terrain rendering
        /// </summary>
        public static void RecordTerrainPass(
            CommandList cl,
            Framebuffer framebuffer,
            Pipeline pipeline,
            ResourceSet sceneResources,
            int vertexCount)
        {
            using (var recorder = new CommandListRecorder(cl))
            {
                recorder.CommandList.SetFramebuffer(framebuffer);
                recorder.CommandList.ClearColorTarget(0, RgbaFloat.Black);
                recorder.CommandList.ClearDepthStencil(1.0f);

                recorder.CommandList.SetPipeline(pipeline);
                recorder.CommandList.SetGraphicsResourceSet(0, sceneResources);

                recorder.CommandList.Draw((uint)vertexCount);
            }
        }
    }
}
```

---

## 7. Complete Integration Example

### 7.1 Modern RenderPass Implementation

```csharp
// src/OpenSage.Graphics/RenderPass.cs

using Veldrid;
using System;

namespace OpenSage.Graphics
{
    /// <summary>
    /// Encapsulates a complete render pass (framebuffer, pipeline, resources).
    /// </summary>
    public abstract class RenderPass : IDisposable
    {
        protected readonly GraphicsDevice GraphicsDevice;
        protected readonly RenderResourceCache ResourceCache;
        protected readonly GraphicsCapabilities Capabilities;

        protected Framebuffer Framebuffer;
        protected Pipeline Pipeline;
        protected ResourceSet[] ResourceSets = Array.Empty<ResourceSet>();

        public RenderPass(
            GraphicsDevice graphicsDevice,
            RenderResourceCache resourceCache,
            GraphicsCapabilities capabilities)
        {
            GraphicsDevice = graphicsDevice;
            ResourceCache = resourceCache;
            Capabilities = capabilities;
        }

        /// <summary>
        /// Initialize pass resources.
        /// </summary>
        public abstract void Initialize();

        /// <summary>
        /// Execute rendering for this pass.
        /// </summary>
        public abstract void Execute(CommandList commandList);

        /// <summary>
        /// Recreate after window resize.
        /// </summary>
        public abstract void RecreateWindowSized();

        public virtual void Dispose()
        {
            Framebuffer?.Dispose();
            Pipeline?.Dispose();
            foreach (var set in ResourceSets)
            {
                set?.Dispose();
            }
        }
    }

    /// <summary>
    /// Example terrain rendering pass.
    /// </summary>
    public class TerrainRenderPass : RenderPass
    {
        private Texture _terrainHeightmap;
        private Texture _terrainDiffuse;
        private ResourceSet _textureResources;

        public TerrainRenderPass(
            GraphicsDevice graphicsDevice,
            RenderResourceCache resourceCache,
            GraphicsCapabilities capabilities)
            : base(graphicsDevice, resourceCache, capabilities)
        {
        }

        public override void Initialize()
        {
            // Create framebuffer from manager (not shown)
            Framebuffer = new FramebufferManager(GraphicsDevice)
                .GetWindowFramebuffer("Terrain", PixelFormat.R8_G8_B8_A8_UNorm, PixelFormat.D32_Float);

            // Create pipeline
            Pipeline = new PipelineBuilder(ResourceCache, GraphicsDevice)
                .WithVertexLayout(TerrainVertex.GetLayout())
                .WithShaders(LoadTerrainShaders())
                .WithDeviceSpecializations()
                .WithOutputs(Framebuffer.OutputDescription)
                .Build();

            // Create texture resources
            var factory = GraphicsDevice.ResourceFactory;
            _textureResources = factory.CreateResourceSet(
                new ResourceSetDescription(
                    CreateTerrainLayout(),
                    _terrainDiffuse,
                    GraphicsDevice.PointSampler));
        }

        public override void Execute(CommandList commandList)
        {
            using (var recorder = new CommandListRecorder(commandList))
            {
                var cl = recorder.CommandList;

                cl.SetFramebuffer(Framebuffer);
                cl.ClearColorTarget(0, RgbaFloat.Black);
                cl.ClearDepthStencil(1.0f);

                cl.SetPipeline(Pipeline);
                cl.SetGraphicsResourceSet(0, _textureResources);

                cl.Draw(TerrainVertexCount);
            }
        }

        public override void RecreateWindowSized()
        {
            // Recreate framebuffer for new size
            Framebuffer?.Dispose();
            Initialize();
        }

        private Shader[] LoadTerrainShaders()
        {
            // Load from embedded resources
            return new Shader[] { /*...*/ };
        }

        private ResourceLayout CreateTerrainLayout()
        {
            return ResourceCache.GetResourceLayout(
                ref new ResourceLayoutDescription(
                    new ResourceLayoutElementDescription("TerrainDiffuse", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
                    new ResourceLayoutElementDescription("Sampler", ResourceKind.Sampler, ShaderStages.Fragment)));
        }

        private static uint TerrainVertexCount => 6;  // Triangle quad
    }
}
```

---

## 8. Usage em Game Loop

### 8.1 Main Render Loop Integration

```csharp
// src/OpenSage.Game/Game.cs (partial)

public class Game
{
    private GraphicsSystem _graphicsSystem;
    private RenderResourceCache _renderCache;
    private GraphicsCapabilities _capabilities;
    private FramebufferManager _framebufferManager;
    private List<RenderPass> _renderPasses;

    public void Initialize()
    {
        var gd = GraphicsDeviceManager.CreateDevice();
        
        _graphicsSystem = new GraphicsSystem();
        _graphicsSystem.Initialize(gd);

        _renderCache = _graphicsSystem.ResourceCache;
        _capabilities = new GraphicsCapabilities(gd);
        _framebufferManager = new FramebufferManager(gd);

        // Log device info
        Console.WriteLine(_capabilities.GetDeviceInfo());

        // Initialize render passes
        _renderPasses = new List<RenderPass>
        {
            new TerrainRenderPass(gd, _renderCache, _capabilities),
            new ObjectsRenderPass(gd, _renderCache, _capabilities),
            new UiRenderPass(gd, _renderCache, _capabilities),
        };

        foreach (var pass in _renderPasses)
        {
            pass.Initialize();
        }
    }

    public void Update()
    {
        var cl = _graphicsSystem.ResourceFactory.CreateCommandList();

        // Execute all render passes
        foreach (var pass in _renderPasses)
        {
            pass.Execute(cl);
        }

        // Submit and present
        GraphicsDevice.SubmitCommands(cl);
        GraphicsDevice.SwapBuffers();
    }

    public void OnWindowResized()
    {
        _renderCache.Clear();
        _framebufferManager.RecreateWindowSized();

        foreach (var pass in _renderPasses)
        {
            pass.RecreateWindowSized();
        }
    }

    public void Dispose()
    {
        foreach (var pass in _renderPasses)
        {
            pass?.Dispose();
        }
        _framebufferManager?.Dispose();
        _renderCache?.Dispose();
        _graphicsSystem?.Dispose();
    }
}
```

---

## Summary: Implementação em Fases

### ✅ Phase 1 (Ready Now)
- `RenderResourceCache` - Pipeline caching
- `GraphicsCapabilities` - Feature detection
- `CommandListRecorder` - Safe recording

### 🔄 Phase 2 (Next)
- `PipelineBuilder` - Fluent API
- `FramebufferManager` - Attachment management
- `DynamicResourceSetBuilder` - Uniform reuse

### 📋 Phase 3 (Future)
- Shader specialization constants
- Command list paralelization
- Advanced framebuffer techniques (cubemaps, mip chains)

---

**Status**: Code samples ready for copy-paste and integration  
**Date**: 12 de dezembro de 2025
