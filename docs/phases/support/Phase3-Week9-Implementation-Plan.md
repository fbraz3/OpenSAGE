# Phase 3 Week 9: VeldridGraphicsDevice Implementation Plan

**Focus**: Implementing the adapter layer based on root cause analysis  
**Complexity**: High - requires careful synchronization and error handling  
**Estimated Duration**: 8-10 days  

---

## Architecture Overview

```
┌─────────────────────────────────────────────────────────────┐
│ IGraphicsDevice (Abstract Interface - Phase 3 Week 8)        │
│ - 50+ methods defining graphics capability contract          │
└──────────────┬────────────────────────────────────────────┘
               │
               │ implements
               ▼
┌──────────────────────────────────────────────────────────────┐
│ VeldridGraphicsDevice (Adapter - Phase 3 Week 9)             │
│                                                              │
│ ┌─ Core Components ─────────────────────────────────────┐   │
│ │                                                        │   │
│ │ • GraphicsDevice (from Veldrid)                       │   │
│ │ • ResourceFactory (from Veldrid)                      │   │
│ │ • DestructionQueue (custom, async cleanup)           │   │
│ │ • HandleAllocator<T> (custom, generation tracking)   │   │
│ │ • StateCache<TKey, TValue> (custom, perf)            │   │
│ │ • CommandList (from Veldrid, reused per-frame)       │   │
│ │                                                        │   │
│ ├─ Resource Adapters ──────────────────────────────────┤   │
│ │                                                        │   │
│ │ • VeldridBuffer : IBuffer                             │   │
│ │ • VeldridTexture : ITexture                           │   │
│ │ • VeldridFramebuffer : IFramebuffer                   │   │
│ │ • VeldridSampler : ISampler                           │   │
│ │ • VeldridShaderProgram : IShaderProgram               │   │
│ │ • VeldridPipeline : IPipeline                         │   │
│ │                                                        │   │
│ ├─ Helper Systems ────────────────────────────────────┤   │
│ │                                                        │   │
│ │ • FormatMapper (PixelFormat → native format)          │   │
│ │ • ValidationHelpers (defensive checks)                │   │
│ │ • ErrorTranslator (VeldridException → custom)         │   │
│ │ • CapabilityChecker (hardware feature detection)      │   │
│ │                                                        │   │
│ └────────────────────────────────────────────────────────┘   │
│                                                              │
│ Key Methods:                                                 │
│ • BeginFrame() → ProcessDestructionQueue()                   │
│ • CreateBuffer() → Allocate Handle + create Veldrid buffer   │
│ • CreateTexture() → Allocate Handle + create Veldrid texture │
│ • DestroyBuffer() → Queue async destruction (fence-based)    │
│ • WaitForIdle() → Veldrid.GraphicsDevice.WaitForIdle()      │
│                                                              │
└──────────────────────────────────────────────────────────────┘
               ▲
               │ uses
               │
          Veldrid (external)
          ├─ Vulkan backend (Linux)
          ├─ Direct3D11 backend (Windows)
          ├─ OpenGL backend (legacy)
          └─ Metal backend (macOS)
```

---

## Implementation Phases

### Phase 1: Core Infrastructure (Days 1-2)

**Goal**: Establish handle system and resource tracking

#### 1.1 HandleAllocator<T> Implementation

**File**: `src/OpenSage.Graphics/Abstractions/HandleAllocator.cs`  
**Status**: Already exists from Phase 3 Week 8  
**Verification Checklist**:

- [ ] `Handle<T>` has `Index` and `Generation` fields
- [ ] `AllocateHandle()` increments generation on reuse
- [ ] `FreeHandle()` increments generation (prevents use-after-free)
- [ ] `IsValid()` checks both index and generation
- [ ] `GetResource()` validates before returning
- [ ] Thread-safe (if used with locks)

**Key code**:
```csharp
public struct Handle<T> where T : IGraphicsResource
{
    public uint Index;      // index into _resources array
    public uint Generation; // incremented on free
    
    public bool IsValid(IGraphicsDevice device)
    {
        return device.HandleAllocator.IsValid(this);
    }
}
```

#### 1.2 DestructionQueue<T> Implementation

**File**: `src/OpenSage.Graphics/Core/DestructionQueue.cs` (NEW)  
**Purpose**: Async resource cleanup with fence-based synchronization  
**Checklist**:

- [ ] Generic type `T : IDisposable`
- [ ] Store tuple: `(T resource, Fence fence)`
- [ ] `Enqueue(T resource, Fence fence)` method
- [ ] `ProcessCompleted()` method (called every frame)
- [ ] Remove items with signaled fences
- [ ] Implement IDisposable to flush queue

**Pseudo-code**:
```csharp
public class DestructionQueue<T> : IDisposable where T : IDisposable
{
    private List<(T resource, Fence fence)> _pending = new();

    public void Enqueue(T resource, Fence fence)
    {
        _pending.Add((resource, fence));
    }

    public void ProcessCompleted()
    {
        for (int i = _pending.Count - 1; i >= 0; i--)
        {
            if (IsFenceSignaled(_pending[i].fence))
            {
                _pending[i].resource.Dispose();
                _pending.RemoveAt(i);
            }
        }
    }

    private bool IsFenceSignaled(Fence fence)
    {
        // Veldrid-specific: check fence status
        // This depends on which backend (Vulkan, D3D11, etc.)
    }

    public void Dispose()
    {
        // Flush all remaining resources
        foreach (var (resource, _) in _pending)
            resource?.Dispose();
        _pending.Clear();
    }
}
```

#### 1.3 GraphicsResourceBase Implementation

**File**: `src/OpenSage.Graphics/Core/GraphicsResourceBase.cs` (NEW)  
**Purpose**: Common base for all resource adapters  
**Checklist**:

- [ ] Inherit from `IGraphicsResource`
- [ ] Store `Handle<T>` for identity
- [ ] Store parent `VeldridGraphicsDevice` for cleanup
- [ ] Implement `IsDisposed` property
- [ ] Virtual `Dispose()` method for cleanup

**Code**:
```csharp
public abstract class GraphicsResourceBase<T> : IGraphicsResource where T : class
{
    protected Handle<T> _handle;
    protected VeldridGraphicsDevice _device;
    protected T _resource;
    protected bool _disposed;

    protected GraphicsResourceBase(VeldridGraphicsDevice device, Handle<T> handle, T resource)
    {
        _device = device;
        _handle = handle;
        _resource = resource;
    }

    public Handle GetHandle() => _handle.AsHandle();
    public bool IsDisposed => _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed) return;
        if (disposing)
        {
            _device.QueueResourceForDestruction(this);
        }
        _disposed = true;
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
```

---

### Phase 2: VeldridGraphicsDevice Core (Days 3-4)

**Goal**: Implement main adapter class with lifecycle management

#### 2.1 VeldridGraphicsDevice Base Structure

**File**: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs` (NEW)  
**Complexity**: Very High  
**Checklist**:

- [ ] Inherit from `DisposableBase` (OpenSAGE pattern)
- [ ] Constructor takes `Veldrid.GraphicsDevice` and `Veldrid.ResourceFactory`
- [ ] Implement `BeginFrame()` → process destruction queue
- [ ] Implement `EndFrame()` → submit commands
- [ ] Implement `WaitForIdle()` → wait GPU completion
- [ ] Implement `GetCapabilities()` → query GPU features
- [ ] Create single `Veldrid.CommandList` instance (reused per-frame)

**Key fields**:
```csharp
public class VeldridGraphicsDevice : DisposableBase, IGraphicsDevice
{
    private readonly Veldrid.GraphicsDevice _veldridDevice;
    private readonly Veldrid.ResourceFactory _resourceFactory;
    private readonly Veldrid.CommandList _commandList;
    
    private readonly HandleAllocator<Buffer> _bufferAllocator;
    private readonly HandleAllocator<Texture> _textureAllocator;
    private readonly HandleAllocator<Framebuffer> _framebufferAllocator;
    private readonly HandleAllocator<Pipeline> _pipelineAllocator;
    private readonly HandleAllocator<Sampler> _samplerAllocator;
    private readonly HandleAllocator<Shader> _shaderAllocator;
    
    private readonly DestructionQueue<Veldrid.DeviceBuffer> _bufferDestructionQueue;
    private readonly DestructionQueue<Veldrid.Texture> _textureDestructionQueue;
    private readonly DestructionQueue<Veldrid.Framebuffer> _framebufferDestructionQueue;
    
    private readonly Dictionary<BlendStateKey, Veldrid.BlendStateDescription> _blendStateCache;
    private readonly Dictionary<DepthStateKey, Veldrid.DepthStencilStateDescription> _depthStateCache;
    private readonly Dictionary<RasterStateKey, Veldrid.RasterizerStateDescription> _rasterizerStateCache;
    
    private GraphicsCapabilities _capabilities;
    private bool _insideFrame; // Guard against nested BeginFrame
}
```

#### 2.2 Frame Lifecycle

**Checklist**:

- [ ] `BeginFrame()`:
  - [ ] Validate not already in frame (`_insideFrame = false`)
  - [ ] Call `_commandList.Begin()`
  - [ ] Call `ProcessDestructionQueues()`
  - [ ] Set `_insideFrame = true`

- [ ] `EndFrame()`:
  - [ ] Validate inside frame (`_insideFrame = true`)
  - [ ] Call `_commandList.End()`
  - [ ] Call `_veldridDevice.SubmitCommands(_commandList)`
  - [ ] Allocate fence for this submission (track for destruction queue)
  - [ ] Set `_insideFrame = false`

- [ ] `WaitForIdle()`:
  - [ ] If inside frame: call `EndFrame()` first (implicit)
  - [ ] Call `_veldridDevice.WaitForIdle()`

- [ ] `ProcessDestructionQueues()`:
  - [ ] Call `_bufferDestructionQueue.ProcessCompleted()`
  - [ ] Call `_textureDestructionQueue.ProcessCompleted()`
  - [ ] Call `_framebufferDestructionQueue.ProcessCompleted()`

#### 2.3 Capabilities Detection

**Checklist**:

- [ ] Query `_veldridDevice.GetPixelFormatSupport()` for each format
- [ ] Check `_veldridDevice.IsUvOriginTopLeft`
- [ ] Check `_veldridDevice.IsDepthRangeZeroToOne`
- [ ] Check `_veldridDevice.IsClipSpaceYInverted`
- [ ] Populate `GraphicsCapabilities` struct
- [ ] Return from `GetCapabilities()`

---

### Phase 3: State Caching System (Days 4-5)

**Goal**: Implement state object caching for performance

#### 3.1 State Cache Infrastructure

**File**: `src/OpenSage.Graphics/Core/StateCache.cs` (NEW)  
**Checklist**:

- [ ] Generic `StateCache<TKey, TValue>` class
- [ ] TKey must be `IEquatable<TKey>` and have good `GetHashCode()`
- [ ] Thread-safe (with lock or concurrent dict)
- [ ] `GetOrCreate(TKey key, Func<TKey, TValue> factory)` method
- [ ] `Clear()` for device reset scenarios

**Code**:
```csharp
public class StateCache<TKey, TValue> where TKey : IEquatable<TKey>
{
    private readonly Dictionary<TKey, TValue> _cache = new();
    private readonly object _lock = new();

    public TValue GetOrCreate(TKey key, Func<TKey, TValue> factory)
    {
        lock (_lock)
        {
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var created = factory(key);
            _cache[key] = created;
            return created;
        }
    }

    public void Clear() => _cache.Clear();
}
```

#### 3.2 Blend State Caching

**Checklist**:

- [ ] Create `BlendStateKey` readonly struct
  - [ ] Include all blend state properties (SrcBlend, DestBlend, etc.)
  - [ ] Override `Equals()` and `GetHashCode()`
- [ ] Create `_blendStateCache` in VeldridGraphicsDevice
- [ ] When creating pipeline, lookup blend state in cache
- [ ] Create native blend state only if cache miss

#### 3.3 Depth/Stencil State Caching

**Checklist**:

- [ ] Create `DepthStateKey` readonly struct
- [ ] Create `_depthStateCache` in VeldridGraphicsDevice
- [ ] Implement caching logic (same as blend)

#### 3.4 Rasterizer State Caching

**Checklist**:

- [ ] Create `RasterStateKey` readonly struct
- [ ] Create `_rasterizerStateCache` in VeldridGraphicsDevice
- [ ] Implement caching logic (same as blend)

---

### Phase 4: Resource Creation/Destruction (Days 5-6)

**Goal**: Implement resource adapter creation with proper lifetime management

#### 4.1 Buffer Creation and Destruction

**File**: `src/OpenSage.Graphics/Veldrid/VeldridBuffer.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridBuffer : GraphicsResourceBase<Veldrid.DeviceBuffer>, IBuffer`
- [ ] Implement `CreateBuffer(BufferDescription)`:
  - [ ] Validate description (size > 0, valid usage flags)
  - [ ] Allocate handle via `_bufferAllocator`
  - [ ] Create Veldrid.DeviceBuffer via `_resourceFactory`
  - [ ] Wrap in VeldridBuffer adapter
  - [ ] Return IBuffer interface
  
- [ ] Implement `DestroyBuffer(IBuffer)`:
  - [ ] Cast to VeldridBuffer
  - [ ] Validate handle is valid
  - [ ] Allocate fence (Veldrid.CommandList.Draw() submits implicit fence)
  - [ ] Queue destruction: `_bufferDestructionQueue.Enqueue(buffer, fence)`
  - [ ] Invalidate handle via `_bufferAllocator.FreeHandle()`

- [ ] Implement `MapBuffer()` for staging buffers
- [ ] Implement `UnmapBuffer()` for staging buffers

**Code snippet**:
```csharp
public class VeldridBuffer : GraphicsResourceBase<Veldrid.DeviceBuffer>, IBuffer
{
    public ulong SizeInBytes => _resource.SizeInBytes;
    
    public void SetData(void* data, uint offset, uint size)
    {
        if ((_handle.BufferUsage & BufferUsage.Dynamic) == 0)
            throw new GraphicsException("Buffer not created with Dynamic usage");
        
        _device._commandList.UpdateBuffer(_resource, offset, (uint)size, (IntPtr)data);
    }
}
```

#### 4.2 Texture Creation and Destruction

**File**: `src/OpenSage.Graphics/Veldrid/VeldridTexture.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridTexture : GraphicsResourceBase<Veldrid.Texture>, ITexture`
- [ ] Implement `CreateTexture(TextureDescription)`:
  - [ ] Validate description
  - [ ] Check format support (call `GetPixelFormatSupport()`)
  - [ ] Allocate handle
  - [ ] Create Veldrid.Texture via `_resourceFactory`
  - [ ] Handle mipmap generation if needed
  - [ ] Wrap in VeldridTexture adapter

- [ ] Implement `DestroyTexture(ITexture)`:
  - [ ] Queue async destruction
  - [ ] Invalidate handle

- [ ] Implement `UpdateTexture()` for runtime updates
- [ ] Implement `GetPixelFormatSupport()`

#### 4.3 Framebuffer Creation

**File**: `src/OpenSage.Graphics/Veldrid/VeldridFramebuffer.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridFramebuffer : GraphicsResourceBase<Veldrid.Framebuffer>, IFramebuffer`
- [ ] Implement `CreateFramebuffer(FramebufferDescription)`:
  - [ ] Validate all attachments exist and are compatible
  - [ ] Allocate handle
  - [ ] Convert attachment IDs to Veldrid.Texture[]
  - [ ] Create Veldrid.Framebuffer via `_resourceFactory.CreateFramebuffer()`

- [ ] Handle special cases:
  - [ ] Backbuffer framebuffer (use `_veldridDevice.SwapchainFramebuffer`)
  - [ ] Window-specific framebuffer (future BGFX compatibility)

- [ ] Implement `DestroyFramebuffer()`

---

### Phase 5: Pipelines and Shaders (Days 7-8)

**Goal**: Implement shader loading and pipeline creation

#### 5.1 Shader Program Loading

**File**: `src/OpenSage.Graphics/Veldrid/VeldridShaderProgram.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridShaderProgram : GraphicsResourceBase<Veldrid.Shader>, IShaderProgram`
- [ ] Implement `CreateShaderProgram(ShaderDescription)`:
  - [ ] Validate shader code is not empty
  - [ ] Check if bytecode provided (pre-compiled)
  - [ ] Create Veldrid shaders via `_resourceFactory.CreateShader()`
  - [ ] Store shader stage (vertex, fragment, compute)

- [ ] Implement format detection:
  - [ ] SPIR-V: Load as-is for Vulkan, cross-compile for others
  - [ ] HLSL: Pre-compile for D3D11
  - [ ] GLSL: Load as-is for OpenGL

- [ ] Handle SPIR-V cross-compilation:
  - [ ] Use `Veldrid.SPIRV` library for cross-compile
  - [ ] Apply `CrossCompileOptions` (invertY, fixClipZ)
  - [ ] Cache compiled shaders per format

**Code**:
```csharp
public class VeldridShaderProgram : GraphicsResourceBase<Veldrid.Shader>, IShaderProgram
{
    public static Veldrid.Shader CreateFromSpirv(
        Veldrid.ResourceFactory factory,
        ReadOnlySpan<byte> spirvBytecode,
        Veldrid.ShaderStages stage)
    {
        // Veldrid auto-detects backend and cross-compiles if needed
        var desc = new ShaderDescription(stage, spirvBytecode, "main");
        return factory.CreateShader(desc);
    }
}
```

#### 5.2 Pipeline Creation with State Caching

**File**: `src/OpenSage.Graphics/Veldrid/VeldridPipeline.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridPipeline : GraphicsResourceBase<Veldrid.Pipeline>, IPipeline`
- [ ] Implement `CreatePipeline(PipelineDescription)`:
  - [ ] Extract state objects (blend, depth, rasterizer)
  - [ ] Lookup in caches (BlendStateCache, DepthStateCache, etc.)
  - [ ] Create new native state objects only if cache miss
  - [ ] Create Veldrid.GraphicsPipeline
  - [ ] Validate resource layout compatibility

- [ ] Implement state validation:
  - [ ] Verify all shaders provided
  - [ ] Verify input vertex layout matches shader expectations
  - [ ] Verify resource layouts match shader bindings

#### 5.3 Sampler Creation

**File**: `src/OpenSage.Graphics/Veldrid/VeldridSampler.cs` (NEW)  
**Checklist**:

- [ ] Class `VeldridSampler : GraphicsResourceBase<Veldrid.Sampler>, ISampler`
- [ ] Implement `CreateSampler(SamplerDescription)`:
  - [ ] Convert OpenSAGE sampler description to Veldrid description
  - [ ] Create Veldrid.Sampler via `_resourceFactory`

---

### Phase 6: Format Mapping and Validation (Day 8)

**Goal**: Implement cross-platform format support

#### 6.1 Format Mapper

**File**: `src/OpenSage.Graphics/Veldrid/FormatMapper.cs` (NEW)  
**Checklist**:

- [ ] Static class with conversion methods
- [ ] `ToVeldridPixelFormat(PixelFormat)` → Veldrid.PixelFormat
- [ ] `ToVeldridVertexElementFormat(VertexElementFormat)` → Veldrid.VertexElementFormat
- [ ] `FromVeldridPixelFormat(Veldrid.PixelFormat)` → PixelFormat (reverse mapping)
- [ ] Handle unsupported formats → throw GraphicsCapabilityNotSupportedException

**Code snippet**:
```csharp
public static class FormatMapper
{
    public static Veldrid.PixelFormat ToVeldrid(PixelFormat format) => format switch
    {
        PixelFormat.R8_UNorm => Veldrid.PixelFormat.R8_UNorm,
        PixelFormat.R8_G8_B8_A8_UNorm => Veldrid.PixelFormat.R8_G8_B8_A8_UNorm,
        PixelFormat.R32_Float => Veldrid.PixelFormat.R32_Float,
        PixelFormat.BC1_Rgb_UNorm => Veldrid.PixelFormat.BC1_Rgb_UNorm,
        _ => throw new NotSupportedException($"Format {format} not supported")
    };
}
```

#### 6.2 Capability Checker

**File**: `src/OpenSage.Graphics/Veldrid/CapabilityChecker.cs` (NEW)  
**Checklist**:

- [ ] Query `GetPixelFormatSupport()` for each format
- [ ] Detect format compression support (BC, ETC2, ASTC)
- [ ] Populate `GraphicsCapabilities` struct
- [ ] Log unsupported formats as warnings

---

### Phase 7: Error Handling and Validation (Day 9)

**Goal**: Robust error handling and defensive programming

#### 7.1 Error Translation

**File**: `src/OpenSage.Graphics/Veldrid/ErrorTranslator.cs` (NEW)  
**Checklist**:

- [ ] Static helper to convert Veldrid exceptions
- [ ] `VeldridException` → `GraphicsException` with context
- [ ] Capture stack trace for debugging
- [ ] Log detailed error information

**Code**:
```csharp
public static class ErrorTranslator
{
    public static GraphicsException Translate(VeldridException vex, string context)
    {
        var message = $"Graphics error in {context}: {vex.Message}";
        return new GraphicsException(message, vex);
    }
}
```

#### 7.2 Validation Helpers

**File**: `src/OpenSage.Graphics/Veldrid/ValidationHelpers.cs` (NEW)  
**Checklist**:

- [ ] `ValidateHandle<T>(Handle<T>)` → throw if invalid
- [ ] `ValidateBufferDescription(BufferDescription)` → throw if invalid
- [ ] `ValidateTextureDescription(TextureDescription)` → throw if invalid
- [ ] `ValidateFramebufferDescription(FramebufferDescription)` → throw if invalid
- [ ] `ValidatePipelineDescription(PipelineDescription)` → throw if invalid

**Pattern**:
```csharp
private void ValidateBufferDescription(in BufferDescription desc)
{
    if (desc.SizeInBytes == 0)
        throw new GraphicsException("Buffer size must be > 0");
    
    if ((desc.Usage & (BufferUsage.Vertex | BufferUsage.Index | 
                       BufferUsage.Staging | BufferUsage.Dynamic)) == 0)
        throw new GraphicsException("Buffer must have valid usage flags");
}
```

#### 7.3 Bounds Checking

**Checklist**:

- [ ] Check handle index < allocator.MaxHandles
- [ ] Check texture coordinates within bounds (mips, layers, faces)
- [ ] Check buffer offsets + size <= buffer.SizeInBytes
- [ ] Check framebuffer width/height > 0

---

### Phase 8: Integration and Testing (Days 9-10)

**Goal**: Verify adapter works with existing systems

#### 8.1 Unit Tests

**File**: `src/OpenSage.Graphics.Tests/VeldridGraphicsDeviceTests.cs` (NEW)  
**Checklist**:

- [ ] Test handle allocation/deallocation
- [ ] Test buffer creation/destruction
- [ ] Test texture creation with various formats
- [ ] Test framebuffer creation
- [ ] Test pipeline caching (verify same state → same pipeline)
- [ ] Test error handling (invalid parameters)
- [ ] Test frame lifecycle (BeginFrame/EndFrame/WaitForIdle)
- [ ] Test destruction queue processing

#### 8.2 Integration Test: Triangle Rendering

**File**: `src/OpenSage.Graphics.Tests/TriangleRenderTest.cs` (NEW)  
**Checklist**:

- [ ] Create graphics device
- [ ] Create vertex buffer with triangle data
- [ ] Create shader program (vertex + fragment)
- [ ] Create pipeline
- [ ] Create framebuffer
- [ ] Record draw call
- [ ] Verify framebuffer contains triangle
- [ ] Cleanup resources

**Expected result**: A simple colored triangle renders to framebuffer

#### 8.3 Performance Benchmarks

**Checklist**:

- [ ] Measure buffer creation time (vs. Veldrid direct)
- [ ] Measure pipeline creation time (vs. Veldrid direct)
- [ ] Measure state caching effectiveness (hit rate, lookup time)
- [ ] Measure destruction queue overhead (per-frame cost)

---

## Acceptance Criteria

### Functional Requirements

- [ ] All IGraphicsDevice methods implemented (50+ methods)
- [ ] All resource types supported (Buffer, Texture, Framebuffer, Sampler, Shader, Pipeline)
- [ ] Handle generation prevents use-after-free
- [ ] Async destruction queue prevents GPU synchronization bugs
- [ ] State caching improves performance (measurable in benchmarks)
- [ ] Error handling catches and translates all Veldrid exceptions
- [ ] Validation prevents invalid API usage
- [ ] Thread-safe (with documented constraints)

### Quality Requirements

- [ ] Unit test coverage ≥ 80%
- [ ] All public methods have XML documentation
- [ ] No compiler warnings
- [ ] Consistent with OpenSAGE coding style
- [ ] Follow Allman braces, 4-space indentation
- [ ] All visibility explicitly specified

### Performance Requirements

- [ ] State caching hit rate ≥ 90% (typical game workload)
- [ ] Handle allocation/deallocation < 1 microsecond
- [ ] Pipeline creation < 50 microseconds (cached state)
- [ ] Destruction queue processing < 10% frame budget

### Integration Requirements

- [ ] Compiles successfully with .NET 10.0
- [ ] No external dependencies beyond Veldrid
- [ ] Plugs into existing IGraphicsDevice interface
- [ ] Can render a triangle to offscreen target
- [ ] Compatible with existing RenderPipeline code

---

## Key Implementation Patterns

### Pattern 1: Handle Validation on Every Access

```csharp
public IBuffer GetBuffer(Handle<Buffer> handle)
{
    if (!_bufferAllocator.IsValid(handle))
        throw new GraphicsException($"Invalid buffer handle");
    
    return _bufferResources[handle.Index];
}
```

### Pattern 2: Async Destruction with Fence Tracking

```csharp
public void DestroyBuffer(Handle<Buffer> handle)
{
    ValidateHandle(handle);
    
    var veldridBuffer = _bufferResources[handle.Index]._veldridBuffer;
    var fence = AllocateNewFence();
    
    _bufferDestructionQueue.Enqueue(veldridBuffer, fence);
    _bufferAllocator.FreeHandle(handle);
}
```

### Pattern 3: State Caching with Type Safety

```csharp
private BlendStateKey ExtractBlendState(in PipelineDescription desc)
    => new BlendStateKey(
        desc.BlendState.Enabled,
        desc.BlendState.SrcColorBlend,
        desc.BlendState.DestColorBlend,
        // ... all other fields
    );

// Later:
var key = ExtractBlendState(desc);
var blendState = _blendStateCache.GetOrCreate(key, k => ConvertToVeldrid(k));
```

### Pattern 4: Defensive Validation at Entry Points

```csharp
public IBuffer CreateBuffer(in BufferDescription description)
{
    // Validate before touching GPU
    if (description.SizeInBytes == 0)
        throw new GraphicsException("Buffer size must be > 0");
    if (_insideFrame == false)
        throw new GraphicsException("Must be inside frame (call BeginFrame first)");
    
    // Safe to proceed
    // ...
}
```

### Pattern 5: Try-Catch Error Translation

```csharp
public IFramebuffer CreateFramebuffer(in FramebufferDescription description)
{
    try
    {
        var veldridFramebuffer = _resourceFactory.CreateFramebuffer(
            ConvertToVeldrid(description));
        // ...
    }
    catch (VeldridException vex)
    {
        throw ErrorTranslator.Translate(vex, "CreateFramebuffer");
    }
}
```

---

## Risk Mitigation

### Risk 1: Memory Leaks from Destruction Queue

**Mitigation**:
- [ ] Flush queue in destructor
- [ ] Add assertion in destructor if queue not empty (debug mode)
- [ ] Unit test destruction queue processing

### Risk 2: Handle Reuse Causing Use-After-Free

**Mitigation**:
- [ ] Generation field in Handle<T>
- [ ] Increment generation on free
- [ ] Check generation in IsValid()
- [ ] Unit test handle reuse scenario

### Risk 3: State Cache Growing Unbounded

**Mitigation**:
- [ ] Limit cache size (e.g., max 10k entries)
- [ ] Clear cache when device is reset
- [ ] Monitor cache memory in debug builds

### Risk 4: CommandList Not Ended Properly

**Mitigation**:
- [ ] Flag `_insideFrame` to catch nested BeginFrame
- [ ] Assert in SubmitCommands if CommandList not ended
- [ ] Unit test frame lifecycle

### Risk 5: Format Not Supported by GPU

**Mitigation**:
- [ ] Check support in GetPixelFormatSupport()
- [ ] Throw GraphicsCapabilityNotSupportedException (not silent failure)
- [ ] Log which formats are unsupported at startup

---

## Success Metrics

### Code Quality
- [ ] 0 compiler warnings
- [ ] ≥ 80% test coverage
- [ ] All public methods documented
- [ ] Code reviews pass

### Performance
- [ ] State caching provides measurable speedup
- [ ] Handle allocation < 1µs
- [ ] Destruction queue < 1% frame budget

### Compatibility
- [ ] Runs on Vulkan (Linux)
- [ ] Runs on Direct3D 11 (Windows)
- [ ] Runs on Metal (macOS)
- [ ] Runs on OpenGL (legacy)

### Integration
- [ ] Simple triangle renders correctly
- [ ] Existing RenderPipeline can use new device
- [ ] All error cases handled gracefully

---

## Timeline

| Week | Days | Task | Deliverable |
|------|------|------|-------------|
| W9 | 1-2 | Handle System | DestructionQueue + HandleAllocator verified |
| W9 | 3-4 | VeldridGraphicsDevice Core | Device creation, frame lifecycle, WaitForIdle |
| W9 | 4-5 | State Caching | StateCache + all cache implementations |
| W9 | 5-6 | Buffer/Texture/Framebuffer | All resource adapters created |
| W9 | 7-8 | Shader/Pipeline/Sampler | Shader loading, pipeline caching, sampler creation |
| W9 | 8 | Format Mapping | FormatMapper + CapabilityChecker |
| W9 | 9 | Error Handling | ValidationHelpers + ErrorTranslator |
| W10 | 1-2 | Unit Tests | ≥ 80% coverage |
| W10 | 3 | Integration Test | Triangle render test passes |
| W10 | 4 | Performance | Benchmarks meet requirements |

---

## Conclusion

This implementation plan transforms the abstract IGraphicsDevice interface into a working Veldrid adapter that:

1. **Prevents GPU errors** via handle generation and async destruction
2. **Optimizes performance** via state caching
3. **Catches errors early** via validation
4. **Handles all formats** via format mapping
5. **Integrates cleanly** with existing OpenSAGE code

Success requires careful attention to synchronization, error handling, and resource lifecycle management.

