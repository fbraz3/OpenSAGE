# Veldrid Patterns - Quick Reference & Comparison

**Tabelas e referências rápidas para implementação**  
Data: 12 de dezembro de 2025

---

## 1. ResourceFactory Pattern - Quick Reference

### Pattern Template

```csharp
// Public interface
public abstract class ResourceFactory
{
    protected ResourceFactory(GraphicsDeviceFeatures features)
    {
        Features = features;  // Access in validation methods
    }

    public abstract GraphicsBackend BackendType { get; }
    public GraphicsDeviceFeatures Features { get; }

    // Template Method with validation
    public Pipeline CreateGraphicsPipeline(ref GraphicsPipelineDescription desc)
    {
        ValidateFeatures(ref desc);  // Use Features to check support
        return CreateGraphicsPipelineCore(ref desc);  // Backend impl
    }

    // Pure virtual for backend
    protected abstract Pipeline CreateGraphicsPipelineCore(ref GraphicsPipelineDescription desc);
}

// Backend implementation
internal class MetalResourceFactory : ResourceFactory
{
    public override GraphicsBackend BackendType => GraphicsBackend.Metal;

    protected override Pipeline CreateGraphicsPipelineCore(ref GraphicsPipelineDescription desc)
    {
        return new MTLPipeline(ref desc, _gd);  // Metal-specific
    }
}
```

### Two-Level Binding Checklist

| Step | Example | Purpose |
|------|---------|---------|
| 1. **Define Layout** | `ResourceLayoutDescription` with elements | Template for what resources the shader expects |
| 2. **Create Layout** | `factory.CreateResourceLayout(ref desc)` | Compile resource structure |
| 3. **Create ResourceSet** | `factory.CreateResourceSet(ref setDesc)` | Bind actual resources (buffers, textures) |
| 4. **Use in Pipeline** | `pipeline.ResourceLayouts[0]` | Pipeline validates ResourceSet compatibility |
| 5. **Bind in CommandList** | `cl.SetGraphicsResourceSet(0, set)` | GPU accesses resources via set |

### ResourceSet Binding Rules

```csharp
// ✅ CORRECT: Layout and ResourceSet must match
ResourceLayout layout = factory.CreateResourceLayout(ref new ResourceLayoutDescription(
    new ResourceLayoutElementDescription("ViewMatrix", ResourceKind.UniformBuffer, ShaderStages.Vertex),
    new ResourceLayoutElementDescription("Sampler0", ResourceKind.Sampler, ShaderStages.Fragment)));

ResourceSet set = factory.CreateResourceSet(ref new ResourceSetDescription(
    layout,
    viewMatrixBuffer,  // Position 0: UniformBuffer ✓
    sampler));         // Position 1: Sampler ✓

// ❌ WRONG: Wrong types or count
// ResourceSet(layout, sampler, viewMatrixBuffer)  // Swapped!
// ResourceSet(layout, viewMatrixBuffer)           // Missing sampler!
// ResourceSet(layout, viewMatrixBuffer, sampler, extra)  // Too many!
```

---

## 2. CommandList Model - Backend Comparison

### Execution Model por Backend

| Backend | Native API | Recording | Thread Safety | Submission |
|---------|-----------|-----------|---------------|-----------|
| **Vulkan** | `VkCommandBuffer` | `vkCmdXxx()` calls | Per-queue safe | `vkQueueSubmit()` |
| **Direct3D11** | `ID3D11CommandList` | Deferred context | Deferred-safe | `ExecuteCommandList()` |
| **Metal** | `MTLCommandBuffer` | `MTLRenderCommandEncoder` | `@synchronized` | `commit()` |
| **OpenGL** | Custom emulation | Entry list | Single executor thread | BlockingCollection |

### CommandList Lifecycle

```
┌──────────────┐
│   Created    │
└──────┬───────┘
       │
       ↓
┌──────────────┐
│   Begin()    │  ← Recording starts
└──────┬───────┘
       │ SetPipeline()
       │ SetFramebuffer()
       │ Draw() / Dispatch()
       │ SetGraphicsResourceSet()
       │
       ↓
┌──────────────┐
│   End()      │  ← Recording finishes
└──────┬───────┘
       │
       ↓
┌──────────────────────┐
│ SubmitCommands()     │  ← GPU executes
└──────┬───────────────┘
       │
       ↓
┌──────────────┐
│   Reusable   │  ← Can call Begin() again
└──────────────┘
```

### CommandList Best Practices

| ❌ DON'T | ✅ DO |
|---------|------|
| Share one `CommandList` across threads | Create thread-local `CommandList` |
| Call commands outside Begin/End | Use scope guard with IDisposable |
| Assume submission order | Track dependencies with fences/semaphores |
| Submit same list multiple times | Create new list or reset properly |

---

## 3. Pipeline Caching Patterns

### Cache Strategy Comparison

| Strategy | Hit Rate | Memory | Thread Safety | Best For |
|----------|----------|--------|---------------|----------|
| **No Cache** | 0% | Low | Safe | Prototyping |
| **Simple Dict** | 70-90% | Moderate | Unsafe | Single-threaded render |
| **LRU Cache** | 85-95% | Controlled | Unsafe | Limited pipelines |
| **Backend Cache** | 90-99% | High | Backend handles | Production |

### NeoDemo Pattern Implementation

```csharp
// Static cache - lifetime = entire app
internal static class RenderResourceCache
{
    private static Dictionary<GraphicsPipelineDescription, Pipeline> s_pipelines = new();

    public static Pipeline GetPipeline(ResourceFactory factory, ref GraphicsPipelineDescription desc)
    {
        // Read-only check
        if (s_pipelines.TryGetValue(desc, out var cached))
            return cached;

        // Create and store
        var pipeline = factory.CreateGraphicsPipeline(ref desc);
        s_pipelines.Add(desc, pipeline);
        return pipeline;
    }

    // Called on device lost / backend switch
    public static void Clear()
    {
        foreach (var pipe in s_pipelines.Values)
            pipe.Dispose();
        s_pipelines.Clear();
    }
}
```

### Invalidation Scenarios

| Trigger | Action | Code |
|---------|--------|------|
| Window resize | Recreate framebuffers, keep pipelines | `Clear()` then rebuild targets |
| Backend change | Recreate everything | `Clear()` then `CreateDevice()` |
| Graphics settings change | Rebuild affected pipelines | Selective `Clear()` by criteria |
| Device lost | D3D11 only | Backend handles |

---

## 4. Framebuffer Architecture per Backend

### Load/Store Operations

```csharp
// Vulkan: Multiple RenderPass objects for scenarios
_renderPassNoClear     // loadOp = VK_ATTACHMENT_LOAD_OP_DONT_CARE
_renderPassNoClearLoad // loadOp = VK_ATTACHMENT_LOAD_OP_LOAD
_renderPassClear       // loadOp = VK_ATTACHMENT_LOAD_OP_CLEAR

// Metal: Single RenderPassDescriptor
colorAttachment.loadAction = MTLLoadAction.Clear  // or .Load
colorAttachment.storeAction = MTLStoreAction.Store

// OpenGL: Single FBO
glBindFramebuffer(GL_FRAMEBUFFER, fbo)

// D3D11: RTV/DSV arrays
ID3D11RenderTargetView* rtvs[]
ID3D11DepthStencilView* dsv
```

### Attachment Variants

```csharp
// Standard: Single mip, single layer
new FramebufferAttachmentDescription(texture, arrayLayer: 0, mipLevel: 0)

// Cube maps: Different layer for each face
for (int face = 0; face < 6; face++)
    new FramebufferAttachmentDescription(cubemapTexture, arrayLayer: face, mipLevel: 0)

// Mip chain generation: Different mip levels
for (uint mip = 0; mip < texture.MipLevels; mip++)
    new FramebufferAttachmentDescription(texture, arrayLayer: 0, mipLevel: mip)

// Array slicing: 3D textures or texture arrays
new FramebufferAttachmentDescription(arrayTexture, arrayLayer: selectedLayer, mipLevel: 0)
```

### Framebuffer Dimensions

```csharp
// Automatically determined from attachments
public uint Width  // From first ColorTarget or DepthTarget
public uint Height // From first ColorTarget or DepthTarget

// All attachments at same dimension
ColorTarget[0] = 512x512  ✓
ColorTarget[1] = 512x512  ✓
DepthTarget    = 512x512  ✓

// ❌ MISMATCH
ColorTarget[0] = 512x512
DepthTarget    = 256x256  → Runtime error!
```

---

## 5. Specialization Constants Reference

### Mapping Shader IDs

```glsl
// GLSL Shader
layout(constant_id = 0) const bool USE_NORMAL_MAPPING = false;
layout(constant_id = 1) const int LOD_LEVEL = 0;
layout(constant_id = 2) const float GAMMA = 2.2;

// Matching C# code
var specs = new SpecializationConstant[]
{
    new SpecializationConstant(0, true),    // ID 0 ← USE_NORMAL_MAPPING
    new SpecializationConstant(1, 2u),      // ID 1 ← LOD_LEVEL
    new SpecializationConstant(2, 2.2f),    // ID 2 ← GAMMA
};
```

### Data Type Mapping

| C# Type | Shader Type | Size | Constructor |
|---------|-------------|------|-------------|
| `bool` | `bool` | 1 byte | `new(id, true)` |
| `byte`, `ushort` | `uint` | 4 bytes | `new(id, (uint)value)` |
| `uint` | `uint` | 4 bytes | `new(id, value)` |
| `float` | `float` | 4 bytes | `new(id, value)` |
| `double` | `double` | 8 bytes | Stored as ulong |

### Compilation Effects

```glsl
// BEFORE specialization
if (USE_NORMAL_MAPPING) {
    normal = texture(normalMap, uv).xyz;
} else {
    normal = normal; // pass-through
}

// AFTER specialization (if USE_NORMAL_MAPPING = false)
// Compiler ELIMINATES dead code!
// Result: smaller, faster shader
```

---

## 6. Feature Support Matrix

### Features by Backend

| Feature | Vulkan | D3D11 | Metal | OpenGL |
|---------|--------|-------|-------|---------|
| ComputeShader | ✅ 1.0 | ✅ 10_0 | ✅ 10_12 | ✅ 4.3 |
| GeometryShader | ✅ 1.0 | ✅ 10_0 | ❌ | ✅ 3.2 |
| TessellationShaders | ✅ 1.0 | ✅ 11_0 | ✅ 10_12 | ✅ 4.0 |
| FillModeWireframe | ✅ | ✅ | ✅ | ✅ |
| MultipleViewports | ✅ | ✅ 10_0 | ✅ 10_12 | ✅ 4.1 |
| SamplerAnisotropy | ✅ | ✅ | ✅ | ✅ EXT |
| StructuredBuffer | ✅ | ✅ 11_0 | ❌ | ✅ 4.3 |
| ShaderFloat64 | ✅ 1.2 | ✅ 11_0 | ❌ | ✅ ARB |
| IndependentBlend | ✅ | ✅ 11_0 | ✅ 10_12 | ✅ |

### Runtime Query Pattern

```csharp
// Before using feature
if (gd.Features.ComputeShader)
{
    // Use compute path
    var computePipeline = factory.CreateComputePipeline(...);
}
else
{
    // Fallback to fragment path
    var fragPipeline = factory.CreateGraphicsPipeline(...);
}

// Get specific support
gd.GetPixelFormatSupport(
    PixelFormat.R16_G16_B16_A16_Float,
    TextureType.Texture2D,
    TextureUsage.RenderTarget,
    out var properties);

if (!properties.SampleCounts.Contains(TextureSampleCount.Count8))
{
    msaaCount = properties.SampleCounts[0];  // Fallback
}
```

---

## 7. Threading Model Summary

### CommandList Thread Safety

```
Vulkan          Direct3D11      Metal           OpenGL
───────         ──────────      ─────           ──────
Per-queue       Deferred        Synchronized    Single executor
safe            context         access          thread

Multiple        Multiple        Multiple        Single thread!
threads can     deferred        threads can
record to       contexts per    record (with    _executionThread
different       queue           sync)           handles all
queues                                          actual GL calls
```

### Safe Usage Patterns

```csharp
// ✅ SAFE: Thread-local resources
var threadLocalList = factory.CreateCommandList();  // Per thread
threadLocalList.Begin();
RecordCommands(threadLocalList);
threadLocalList.End();

// ✅ SAFE: Main thread only
MainThreadRender()
{
    var cl = factory.CreateCommandList();
    cl.Begin();
    RecordCommands(cl);
    cl.End();
    gd.SubmitCommands(cl);
}

// ❌ UNSAFE: Shared across threads
CommandList sharedList = factory.CreateCommandList();
Task.Run(() => sharedList.Draw(...));  // RACE!

// ❌ UNSAFE: Main thread waits for GPU (unless intentional)
gd.SubmitCommands(cl);
gd.WaitForIdle();  // Stalls rendering!
```

---

## 8. Error Prevention Checklist

### ResourceLayout & ResourceSet

- [ ] Number of elements in ResourceSet matches ResourceLayout
- [ ] Element types (UniformBuffer vs TextureReadOnly) match
- [ ] Element order matches (position 0 = first element)
- [ ] ShaderStages include all stages using the resource
- [ ] Buffer sizes are multiples of 16 bytes (if uniform)

### Pipeline Creation

- [ ] All required ResourceLayouts are set
- [ ] VertexLayout matches vertex structure size
- [ ] VertexLayout offsets are consistent (all explicit or none)
- [ ] OutputDescription matches actual render target format
- [ ] Specialization constants IDs are unique
- [ ] FillModeWireframe supported if requested
- [ ] IndependentBlend supported if using multiple blend states

### CommandList Recording

- [ ] Begin() called before issuing commands
- [ ] End() called before SubmitCommands()
- [ ] Framebuffer set before Draw/Dispatch
- [ ] Pipeline set before resource set bindings
- [ ] Resource sets match pipeline layout
- [ ] ResourceSet not bound before pipeline with matching layout

### Framebuffer Setup

- [ ] All ColorTargets same dimensions
- [ ] DepthTarget same dimensions as ColorTargets
- [ ] Attachment textures have correct usage flags
- [ ] Array layer < texture.ArrayLayers
- [ ] Mip level < texture.MipLevels

---

## 9. Performance Hotspots

### What's Expensive (Avoid Recreating)

| Resource | Cost | Mitigation |
|----------|------|-----------|
| Pipeline | 🔴 Very High | Cache all pipelines |
| ResourceLayout | 🟠 High | Cache layouts |
| Sampler | 🟠 High | Reuse samplers |
| Texture | 🟡 Medium | Pre-allocate |
| Buffer | 🟡 Medium | Pool buffers |
| ResourceSet | 🟢 Low | Can create freely |
| CommandList | 🟢 Low | Reuse per frame |

### Profiling Points

```csharp
// Measure pipeline creation time
var sw = Stopwatch.StartNew();
var pipeline = factory.CreateGraphicsPipeline(ref desc);
sw.Stop();
Console.WriteLine($"Pipeline creation: {sw.ElapsedMilliseconds}ms");

// Cache hit rate
var stats = renderCache.GetCacheStats();
float hitRate = (stats.totalHits / stats.totalRequests) * 100;
Console.WriteLine($"Cache hit rate: {hitRate:F1}%");

// Frame time breakdown
gd.SubmitCommands(cl);
gd.WaitForIdle();  // ⚠️ For measurement only!
sw.Stop();
```

---

## 10. Decision Tree: Which Pattern to Use?

```
Need Pipeline?
├─ Yes
│  ├─ One-time creation?
│  │  └─ factory.CreateGraphicsPipeline()
│  │
│  └─ Multiple instances (same desc)?
│     └─ Use RenderResourceCache.GetPipeline()
│
Need Resource Binding?
├─ Yes
│  ├─ Frequently changing?
│  │  └─ Use DynamicBinding with offset
│  │
│  └─ Rarely changing?
│     └─ Create new ResourceSet
│
Need Rendering?
├─ Yes
│  ├─ Simple single-pass?
│  │  └─ Inline CommandList recording
│  │
│  └─ Complex multi-pass?
│     └─ Create RenderPass subclass
│
Need Feature Detection?
├─ Yes
│  ├─ Single feature?
│  │  └─ Check gd.Features.X directly
│  │
│  └─ Multiple features?
│     └─ Use GraphicsCapabilities wrapper
│
Need Framebuffer?
├─ Yes
│  ├─ Window-sized?
│  │  └─ Use FramebufferManager
│  │
│  └─ Specialized (cubemap, mip)?
│     └─ Manual creation with attachments
```

---

## 11. Code Snippet Library

### Snippet 1: Safe Pipeline Creation

```csharp
// With validation and caching
Pipeline GetOrCreatePipeline(
    RenderResourceCache cache,
    GraphicsCapabilities caps,
    ref GraphicsPipelineDescription desc)
{
    // Validate before creation
    if (desc.RasterizerState.FillMode == PolygonFillMode.Wireframe
        && !caps.Supports(GraphicsFeature.Wireframe))
    {
        throw new NotSupportedException("Wireframe mode not supported");
    }

    // Get from cache
    return cache.GetGraphicsPipeline(ref desc);
}
```

### Snippet 2: Safe CommandList Scope

```csharp
// Using IDisposable pattern
void RenderFrame(CommandList cl)
{
    using (var recorder = new CommandListRecorder(cl))
    {
        var cmdList = recorder.CommandList;
        
        cmdList.SetFramebuffer(fb);
        cmdList.ClearColorTarget(0, RgbaFloat.Black);
        
        cmdList.SetPipeline(pipeline);
        cmdList.SetGraphicsResourceSet(0, resourceSet);
        
        cmdList.Draw(6);
        
        // recorder.Dispose() called here → cl.End()
    }
    
    gd.SubmitCommands(cl);
}
```

### Snippet 3: Feature-Based Rendering

```csharp
// Graceful fallback
void InitializeRenderingPipeline(GraphicsCapabilities caps)
{
    if (caps.Supports(GraphicsFeature.ComputeShaders))
    {
        _useComputePostProcessing = true;
        _postProcessPipeline = CreateComputePostProcessing();
    }
    else
    {
        _useComputePostProcessing = false;
        _postProcessPipeline = CreateFragmentPostProcessing();
    }

    Console.WriteLine($"Post-processing: {(_useComputePostProcessing ? "Compute" : "Fragment")}");
}
```

### Snippet 4: Framebuffer Variants

```csharp
// Create for different purposes
Framebuffer[] CreateCubemapFramebuffers(
    ResourceFactory factory,
    uint size,
    PixelFormat format)
{
    var cubeTex = factory.CreateTexture(new TextureDescription
    {
        Width = size, Height = size,
        Format = format,
        ArrayLayers = 6,  // 6 faces
        Usage = TextureUsage.RenderTarget | TextureUsage.Sampled
    });

    var framebuffers = new Framebuffer[6];
    for (int face = 0; face < 6; face++)
    {
        framebuffers[face] = factory.CreateFramebuffer(new FramebufferDescription
        {
            ColorTargets = new[]
            {
                new FramebufferAttachmentDescription(
                    cubeTex,
                    arrayLayer: (uint)face,
                    mipLevel: 0)
            }
        });
    }

    return framebuffers;
}
```

---

## Summary Table: All Patterns at a Glance

| Pattern | Purpose | Thread-Safe | Cost | When to Use |
|---------|---------|-------------|------|------------|
| **ResourceFactory** | Abstract resource creation | Backend-dependent | Low | Always |
| **Two-Level Binding** | ResourceLayout + ResourceSet | N/A | Low | All bindings |
| **CommandList** | Deferred recording | No | Low | Every frame |
| **Pipeline Cache** | Avoid recreation | No | High impact | Every pipeline |
| **Feature Detection** | Runtime capability check | Yes | Very low | Initialization |
| **Framebuffer Management** | Attachment organization | Yes | Medium | Every pass |
| **Specialization Constants** | Compile-time customization | Yes | Medium | Shader variants |

---

**Last updated**: 12 de dezembro de 2025  
**Status**: Complete reference ready for integration
