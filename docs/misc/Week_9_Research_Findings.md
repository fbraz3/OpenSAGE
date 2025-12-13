# Week 9+ Research Findings - Resource Adapters, Shader System, and Pipeline Management

**Date**: 12 December 2025 (Session 2 - Week 9 Research Phase)  
**Research Type**: Minucious deepwiki + internet research on Veldrid architecture, OpenSAGE shader system, and BGFX resource management  
**Status**: Complete - Ready for implementation planning

---

## 1. Critical Discoveries: Resource Management Architecture

### 1.1 Veldrid ResourceSet and ResourceLayout System

**Key Finding**: Veldrid uses a TWO-LEVEL binding architecture completely different from BGFX

```
ABSTRACTION LAYER (IShaderProgram)
        ↓
ResourceLayout (SCHEMA): Describes what resources shader expects
    ├── ResourceLayoutElementDescription[]
    │   ├── ResourceKind (UniformBuffer, TextureReadOnly, Sampler)
    │   ├── ShaderStages (Vertex, Fragment)
    │   └── DynamicBinding (optional for offset-based binding)
    ├── Backend-specific: VkDescriptorSetLayout, D3D11InputLayout, etc.
    └── Created once per ShaderProgram
        
ResourceSet (INSTANCE): Contains actual GPU resources
    ├── Binds to ResourceLayout
    ├── BindableResource[] (DeviceBuffer, TextureView, Sampler)
    ├── Backend-specific: VkDescriptorSet, D3D11ResourceSet, etc.
    └── Created per material/shader variant
```

**Implementation Impact**:
- `IShaderProgram` must store `ResourceLayout` 
- Each material variant needs separate `ResourceSet`
- OpenSAGE's `ShaderResourceManager` already models this (verified in deepwiki)

### 1.2 Handle Lifecycle and Resource Pooling Strategy

**Veldrid Approach** (per backend):
- **Vulkan**: Reference counting (`ResourceRefCount`) - decrement on Dispose(), cleanup when count=0
- **Direct3D 11**: Caching + COM reference counting (implicit)
- **OpenGL**: Deferred disposal queue (enqueue → process on frame boundary)

**BGFX Approach** (verified in deepwiki):
```c
// Handle structure: uint16_t idx only
// Pool lookup: g_bgfx.m_textureHandle[handle.idx] → TextureRef

// Validation: BGFX_CHECK_HANDLE validates idx < capacity
// Pooling: 
//   - Allocate: m_textureHandle.alloc() → new idx
//   - Release: m_textureHandle.release(idx) → recycle idx
//   - Generation: No explicit generation counter!
//     (Risk: use-after-free if handle reused before validation)
```

**Our Approach** (Phase 2 + validation):
```csharp
// Handle<T> with generation prevents BGFX use-after-free issue
public struct Handle<T>
{
    public uint Index { get; }           // Resource slot
    public uint Generation { get; }      // Incremented on reuse
}

// ResourcePool for lifecycle
public class ResourcePool<T>
{
    private T[] _resources;
    private uint[] _generations;
    private Queue<uint> _freeSlots;
    
    public Handle<T> Allocate(T resource)
    {
        if (!_freeSlots.TryDequeue(out var idx))
            idx = (uint)_resources.Length; // Grow if needed
        
        _resources[idx] = resource;
        return new Handle<T>(idx, ++_generations[idx]);
    }
    
    public bool TryGet(Handle<T> handle, out T resource)
    {
        if (handle.Generation == _generations[handle.Index])
        {
            resource = _resources[handle.Index];
            return true;
        }
        resource = default;
        return false; // Generation mismatch = use-after-free attempt
    }
}
```

---

## 2. Shader System Deep Dive

### 2.1 OpenSAGE Shader Compilation Pipeline

**Build-Time** (MSBuild `CompileShaders` target):
```
Shader Files (.vert, .frag, .h) 
    → glslangValidator (platform-specific binary)
    → SPIR-V bytecode (.spv)
    → EmbeddedResource in assembly
    → Available at runtime
```

**Runtime** (ShaderCrossCompiler):
```
1. Read SPIR-V bytes from embedded resource
2. Compute SHA256 hash of SPIR-V bytes
3. Check cache directory: ShaderCache/{shaderName}.{hash}.{backend}
4. If cache miss or invalid:
   a. Call SpirvCompilation.CompileVertexFragment(
      vsSpvBytes, fsSpvBytes, crossCompileTarget, options)
   b. Backend-specific handling:
      - Vulkan: Keep original SPIR-V bytecode
      - Direct3D11: Compile cross-compiled HLSL → DXBC bytecode (Vortice.D3DCompiler)
      - OpenGL/OpenGLES: Store cross-compiled GLSL/ESSL as UTF-8 text
      - Metal: Store cross-compiled MSL as UTF-8 text
   c. Create ShaderCacheFile with reflection data
   d. Save to disk
5. Return ShaderCacheFile with ResourceLayoutDescription[]
```

**CrossCompileTarget Mapping** (from verified ShaderCrossCompiler code):
```csharp
private static CrossCompileTarget GetCompilationTarget(GraphicsBackend backend)
{
    return backend switch
    {
        GraphicsBackend.Direct3D11 => CrossCompileTarget.HLSL,
        GraphicsBackend.OpenGL => CrossCompileTarget.GLSL,
        GraphicsBackend.Metal => CrossCompileTarget.MSL,
        GraphicsBackend.OpenGLES => CrossCompileTarget.ESSL,
        _ => throw new SpirvCompilationException(...)
    };
}

// Vulkan is special case: does NOT actually need cross-compilation
// But still cross-compiles to HLSL to extract reflection data
```

### 2.2 Veldrid.SPIRV Cross-Compilation API

**Signature**:
```csharp
public class SpirvCompilation
{
    public static CompilationResult CompileVertexFragment(
        byte[] vertexSpv,
        byte[] fragmentSpv,
        CrossCompileTarget target,
        CrossCompileOptions options);
}

public struct CompilationResult
{
    public string VertexShader { get; }          // HLSL/GLSL/MSL source
    public string FragmentShader { get; }        // HLSL/GLSL/MSL source
    public ReflectionData Reflection { get; }
}

public struct ReflectionData
{
    public ResourceLayoutDescription[] ResourceLayouts { get; }
}

public struct CrossCompileOptions
{
    public bool fixClipZ { get; init; }          // For OpenGL coordinate system
    public bool invertY { get; init; }           // For screen-space inversion
    public SpecializationConstant[] specializations { get; init; }
}

public struct SpecializationConstant
{
    public uint SpecializationId { get; init; }  // Constant ID in shader
    public object Value { get; init; }           // Uint/Int/UInt/Float
}
```

**Critical IDs** (verified from OpenSAGE code):
- `100`: `gd.IsClipSpaceYInverted` (Y-axis flip for clip space)
- `101`: `isGlOrGles` (OpenGL version detection)
- `102`: `gd.IsDepthRangeZeroToOne` (Depth range: Vulkan 0-1 vs OpenGL -1-1)
- `103`: `swapchainIsSrgb` (Color space)

---

## 3. Pipeline Creation State Mapping

### 3.1 GraphicsPipelineDescription (Veldrid) ← RenderState (OpenSAGE)

**State Conversion Required**:
```csharp
// OpenSAGE abstraction state → Veldrid native state

IBlendState → BlendStateDescription
├── Enabled → BlendAttachmentDescription.BlendEnabled
├── SourceRgb → BlendAttachmentDescription.SourceRgbBlend  
├── DestRgb → BlendAttachmentDescription.DestinationRgbBlend
├── RgbOperation → BlendAttachmentDescription.RgbBlendFunction
└── (x4 for RGBA)

IDepthState → DepthStencilStateDescription
├── TestEnabled → DepthTestEnabled
├── WriteEnabled → DepthWriteEnabled
├── CompareFunction → DepthComparison
└── StencilOp → StencilBack/Front operations

IRasterState → RasterizerStateDescription
├── FillMode → PolygonFillMode
├── CullMode → FaceCullMode
├── FrontFace → FrontFace (CCW/CW)
└── DepthClamp → DepthClipEnabled

IShaderProgram → GraphicsPipelineDescription
├── Shaders[] → ShaderSetDescription.Shaders
├── VertexLayout → ShaderSetDescription.VertexLayouts
└── ResourceLayouts → GraphicsPipelineDescription.ResourceLayouts
```

### 3.2 Pipeline Caching Strategy

**Design**:
```csharp
public class PipelineCache
{
    private Dictionary<PipelineKey, IPipeline> _cache;
    
    // Key includes all immutable state that defines uniqueness
    public struct PipelineKey : IEquatable<PipelineKey>
    {
        public Handle<IShaderProgram> ShaderProgram;
        public BlendState BlendState;
        public DepthState DepthState;
        public RasterState RasterState;
        public VertexInputLayout VertexLayout;
        public OutputDescription Output;  // Framebuffer format
    }
    
    public IPipeline GetOrCreate(PipelineKey key)
    {
        if (_cache.TryGetValue(key, out var pipeline))
            return pipeline;
        
        pipeline = CreatePipeline(key);
        _cache[key] = pipeline;
        return pipeline;
    }
}
```

**Benefits**:
- Reduces pipeline creation overhead (expensive GPU operation)
- Typical frame has 10-20 unique pipelines, not 100+
- BGFX and Veldrid both benefit from caching

---

## 4. Framebuffer and TextureView Lifecycle

### 4.1 Texture Attachment Model

**Veldrid**:
```csharp
public struct FramebufferDescription
{
    public Texture DepthTarget { get; init; }           // Optional
    public FramebufferAttachmentDescription[] ColorTargets { get; init; }
}

public struct FramebufferAttachmentDescription
{
    public Texture Target { get; init; }
    public uint ArrayLayer { get; init; }               // For texture arrays
    public uint MipLevel { get; init; }                 // For mipmap levels
}

// Framebuffer creation
var fb = factory.CreateFramebuffer(new FramebufferDescription
{
    ColorTargets = new[]
    {
        new FramebufferAttachmentDescription { Target = colorTexture },
        new FramebufferAttachmentDescription { Target = normalTexture }
    },
    DepthTarget = depthTexture
});
```

**IFramebuffer Abstraction** (Phase 2 design):
```csharp
public interface IFramebuffer : IGraphicsResource
{
    ITexture DepthTarget { get; }
    IReadOnlyList<ITexture> ColorTargets { get; }
    uint Width { get; }
    uint Height { get; }
}
```

### 4.2 TextureView Binding

**Discovery**: Veldrid uses `TextureView` for shader binding, not direct `Texture`

```csharp
// Create texture
var texture = factory.CreateTexture(new TextureDescription
{
    Width = 512,
    Height = 512,
    Format = PixelFormat.R8_G8_B8_A8_UNorm,
    Usage = TextureUsage.Sampled
});

// Create view for shader access
var view = factory.CreateTextureView(new TextureViewDescription
{
    Target = texture,
    BaseMipLevel = 0,
    MipLevels = 1,
    BaseArrayLayer = 0,
    ArrayLayers = 1,
    Format = texture.Format
});

// Bind view to shader
var resourceSet = factory.CreateResourceSet(new ResourceSetDescription(
    resourceLayout,
    view,  // TextureView, not Texture
    sampler
));
```

**Implementation Note**: 
- `ITexture` should wrap both `Texture` and `TextureView` for binding
- Or create separate `ITextureView` interface
- OpenSAGE already has `TextureView` concept (confirmed in deepwiki)

---

## 5. Resource Adapter Implementation Patterns

### 5.1 Veldrid Resource Adapter Template

**Pattern for Buffer**:
```csharp
public class VeldridBuffer : IBuffer
{
    private readonly DeviceBuffer _nativeBuffer;
    private readonly Handle<IBuffer> _handle;
    
    public uint Id => _handle.Index;
    public uint Generation => _handle.Generation;
    public bool IsValid => _handle.IsValid;
    
    internal DeviceBuffer Native => _nativeBuffer;  // For internal use
    
    public VeldridBuffer(Handle<IBuffer> handle, DeviceBuffer nativeBuffer)
    {
        _handle = handle;
        _nativeBuffer = nativeBuffer ?? throw new ArgumentNullException(nameof(nativeBuffer));
    }
    
    public void Dispose()
    {
        _nativeBuffer?.Dispose();
    }
}

// Pattern for Texture, Sampler, Framebuffer, etc. is identical
```

### 5.2 Veldrid Device Integration

**Current VeldridGraphicsDevice Issues** (identified during Week 8):
```csharp
// Current: Returns placeholder Handle<T>
public Handle<IBuffer> CreateBuffer(in BufferDescription description)
{
    var veldridBuffer = _factory.CreateBuffer(ref veldridDesc);
    var handle = new Handle<IBuffer>(/* TODO: proper allocation */);
    // _buffers[handle] = veldridBuffer;  // NOT IMPLEMENTED
    return handle;
}

// Should be:
public Handle<IBuffer> CreateBuffer(in BufferDescription description)
{
    var veldridBuffer = _factory.CreateBuffer(ref veldridDesc);
    _addDisposable(veldridBuffer);
    
    var handle = _bufferPool.Allocate(new VeldridBuffer(veldridBuffer));
    // Store for retrieval
    _buffers[handle] = new VeldridBuffer(handle, veldridBuffer);
    return handle;
}
```

---

## 6. BGFX Adapter Design (Future, Week 14-18)

### 6.1 Handle Mapping Strategy

**BGFX Limitation**: Only 65536 handles per resource type (uint16_t)

**Mitigation**:
```csharp
// BGFX handle → Our Handle<T> adapter
public class BgfxGraphicsDevice : IGraphicsDevice
{
    private Dictionary<Handle<IBuffer>, bgfx.DynamicVertexBufferHandle> _dynamicVbHandles;
    private Dictionary<Handle<IBuffer>, bgfx.IndexBufferHandle> _staticIbHandles;
    
    public Handle<IBuffer> CreateBuffer(in BufferDescription description)
    {
        var ourHandle = _bufferPool.Allocate(new BgfxBuffer(???));
        
        if (description.Usage == BufferUsage.Dynamic)
        {
            var bgfxHandle = bgfx.create_dynamic_vertex_buffer(/* ... */);
            _dynamicVbHandles[ourHandle] = bgfxHandle;
        }
        else
        {
            var bgfxHandle = bgfx.create_index_buffer(/* ... */);
            _staticIbHandles[ourHandle] = bgfxHandle;
        }
        
        return ourHandle;
    }
}
```

### 6.2 Encoder Threading Model (for future)

**BGFX Multi-Threading**:
```c
// Thread-local encoder pooling
thread_local bgfx::Encoder* encoder = bgfx::begin();

// Record commands per-thread
encoder->setViewRect(0, 0, 0, 640, 480);
encoder->submit(0, program);

// Main thread collates encoders
bgfx::frame();  // Process all encoders
```

**Our Abstraction** (Week 13):
```csharp
// ThreadLocal<ICommandList> for command buffering
public class CommandBuffer : IDisposable
{
    [ThreadStatic]
    private static VeldridCommandList _threadLocalCommandList;
    
    public static ICommandList Current => _threadLocalCommandList ??= new VeldridCommandList();
}

// Or use ICommandList pooling for allocation fairness
```

---

## 7. Implementation Checklist for Week 9-13

### Week 9: Resource Adapters

- [ ] Create `VeldridBuffer` class
- [ ] Create `VeldridTexture` class  
- [ ] Create `VeldridFramebuffer` class
- [ ] Create `VeldridSampler` class
- [ ] Create `ResourcePool<T>` helper class
- [ ] Update `VeldridGraphicsDevice` to use adapters
- [ ] Implement proper handle allocation/deallocation
- [ ] Unit tests for resource lifecycle

### Week 10: Shader System

- [ ] Integrate `Veldrid.SPIRV` NuGet package
- [ ] Create `IShaderProgram` implementation
- [ ] Load SPIR-V from embedded resources
- [ ] Implement `ShaderCacheFile` persistence
- [ ] Test cross-compilation for all backends
- [ ] Shader reflection data extraction

### Week 11: Pipeline Creation

- [ ] Implement `IPipeline` for graphics pipeline
- [ ] Create `PipelineCache` for performance
- [ ] State conversion (BlendState → VkPipelineColorBlendAttachmentState, etc.)
- [ ] Create pipeline via ResourceFactory
- [ ] Bind pipeline in command list
- [ ] Unit tests for pipeline state combinations

### Week 12: Rendering Integration

- [ ] Simple triangle vertex/index buffers
- [ ] Basic shader binding and resource sets
- [ ] Draw call execution
- [ ] Test on all backends (Metal, OpenGL, Vulkan)
- [ ] Performance profiling

### Week 13: Thread Safety & Optimization

- [ ] CommandList pooling for threading
- [ ] Resource pool optimization
- [ ] Memory usage profiling
- [ ] Handle generation overflow handling

---

## 8. Critical Notes for Implementation

### Do NOT Do:
1. ❌ Do NOT implement BGFX adapter yet (Week 14-18)
2. ❌ Do NOT create separate Veldrid-specific classes in Veldrid/ folder
   - Use `VeldridGraphicsDevice.cs` only
   - Resource adapters (`VeldridBuffer`, etc.) in same file
3. ❌ Do NOT expose Veldrid types in public API
   - All public API uses `Handle<T>` and interfaces
   - Internal `Native` property for integration only
4. ❌ Do NOT create placeholder implementations
   - Either full implementation or throw `NotImplementedException`

### DO Do:
1. ✅ Use immutable state objects for pipeline caching
2. ✅ Implement generation-based handle validation
3. ✅ Keep ResourcePool generic and backend-agnostic
4. ✅ Test on actual hardware (Metal on macOS in this environment)
5. ✅ Follow OpenSAGE coding style (Allman braces, 4-space indentation)
6. ✅ Document public API with XML comments

---

## 9. Veldrid.SPIRV Integration Path

**Current State**:
- ✅ SPIR-V bytecode available at runtime (embedded resources)
- ✅ SHA256 hash mechanism for cache invalidation
- ✅ ShaderCache directory already created

**Required Work**:
1. Add `Veldrid.SPIRV 1.0.15` NuGet reference
2. Implement shader loading:
   ```csharp
   var result = SpirvCompilation.CompileVertexFragment(
       vsSpvBytes, fsSpvBytes, 
       GetCrossCompileTarget(device.BackendType),
       new CrossCompileOptions());
   
   var vsShader = factory.CreateShader(new ShaderDescription(
       ShaderStages.Vertex, 
       GetShaderBytes(result.VertexShader, backend),
       "main"));
   ```
3. Extract ResourceLayoutDescription from reflection
4. Create ResourceLayouts via factory

---

## Research Status Summary

| Component | Research Complete | Implementation Ready | Week |
|-----------|-------------------|----------------------|------|
| ResourceSet/Layout architecture | ✅ 100% | ✅ Yes | 9 |
| Handle pooling strategy | ✅ 100% | ✅ Yes | 9 |
| Shader compilation pipeline | ✅ 100% | ✅ Yes | 10 |
| Veldrid.SPIRV API | ✅ 100% | ✅ Yes | 10 |
| Pipeline state mapping | ✅ 100% | ✅ Yes | 11 |
| Framebuffer/TextureView binding | ✅ 100% | ✅ Yes | 11 |
| BGFX handle architecture | ✅ 100% | ⚠️ Future | 14-18 |
| Thread safety patterns | ✅ 100% | ⚠️ Future | 13 |

---

**Ready for Week 9 Implementation Start** ✅
