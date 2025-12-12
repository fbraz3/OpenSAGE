# Graphics Abstraction Layer - Implementation Architecture

**Purpose**: Detailed architectural blueprint for implementing graphics binding system abstraction  
**Based on**: DeepWiki research of OpenSAGE codebase (December 12, 2025)

---

## Executive Summary

The OpenSAGE graphics system is built on **Veldrid**, a cross-platform graphics abstraction. Analysis of the codebase reveals:

1. **Current binding pattern**: Direct CommandList method calls with ResourceSet groups
2. **Production validation**: Implicit validation in Veldrid; no explicit checks in OpenSAGE
3. **Key optimization**: Pipeline and resource set caching to minimize state changes
4. **Resource lifecycle**: Generation-based validation in resource pools (Week 9 implementation)

This document outlines how to create a clean abstraction layer that:
- Preserves Veldrid's optimization patterns
- Adds explicit validation and debugging
- Enables shader backend switching
- Maintains performance characteristics

---

## Proposed Abstraction Architecture

### Layer Structure

```
Application Code (RenderPipeline, SpriteBatch, etc.)
        ↓
[Proposed Abstraction Layer]
    ├── IGraphicsCommand (batches binding calls)
    ├── IResourceSetBuilder (groups resources)
    ├── IBindingValidation (debug support)
    └── ICapabilityQuery (feature detection)
        ↓
Veldrid (Current Implementation)
        ↓
GPU Backend (Metal, Direct3D11, OpenGL, Vulkan)
```

### Core Interfaces

#### 1. IGraphicsCommand (Replaces Direct CommandList Calls)

```csharp
/// <summary>
/// Abstracts graphics command recording with validation and batching.
/// </summary>
public interface IGraphicsCommand
{
    // Pipeline State
    void SetPipeline(IPipeline pipeline);
    void SetViewport(Viewport viewport);
    void SetScissor(ScissorRect rect);
    void SetFramebuffer(IFramebuffer framebuffer);
    
    // Resource Binding
    void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0);
    void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0);
    void SetResourceSet(uint slot, IResourceSet resourceSet);
    
    // Alternative: Batch binding
    IBindingBatch StartBindingBatch();
    
    // Clear Commands
    void ClearRenderTarget(Vector4 color, float depth = 1.0f, byte stencil = 0);
    
    // Drawing
    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0);
    void Draw(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0);
    
    // Debug Support
    void PushDebugGroup(string name);
    void PopDebugGroup();
    void InsertDebugMarker(string name);
}
```

#### 2. IBindingBatch (Fluent API for Grouped Binding)

```csharp
/// <summary>
/// Fluent API for batching binding operations.
/// Reduces function call overhead for complex binding sequences.
/// </summary>
public interface IBindingBatch : IDisposable
{
    IBindingBatch SetPipeline(IPipeline pipeline);
    IBindingBatch SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0);
    IBindingBatch SetIndexBuffer(IBuffer buffer, IndexFormat format);
    IBindingBatch SetResourceSet(uint slot, IResourceSet resourceSet);
    void Flush();
}

// Usage
using (var batch = cmd.StartBindingBatch())
{
    batch.SetPipeline(pipeline)
         .SetVertexBuffer(0, vertexBuffer)
         .SetIndexBuffer(indexBuffer, IndexFormat.UInt16)
         .SetResourceSet(0, globalResourceSet)
         .SetResourceSet(1, materialResourceSet)
         .Flush();
}

cmd.DrawIndexed(indexCount);
```

#### 3. IResourceSetBuilder (Type-Safe Resource Grouping)

```csharp
/// <summary>
/// Type-safe builder for creating resource sets with validation.
/// </summary>
public interface IResourceSetBuilder
{
    IResourceSetBuilder SetUniformBuffer(uint slot, string name, IBuffer buffer);
    IResourceSetBuilder SetTexture(uint slot, string name, ITexture texture);
    IResourceSetBuilder SetSampler(uint slot, string name, ISampler sampler);
    IResourceSetBuilder SetStructuredBuffer(uint slot, string name, IBuffer buffer);
    
    IResourceSet Build();
}

// Usage
var resourceSet = builder.Create()
    .SetUniformBuffer(0, "GlobalConstants", constantsBuffer)
    .SetTexture(1, "MainTexture", texture)
    .SetSampler(2, "MainSampler", sampler)
    .Build();
```

#### 4. ICapabilityQuery (Feature Detection)

```csharp
/// <summary>
/// Queries GPU capabilities for adaptive rendering.
/// </summary>
public interface ICapabilityQuery
{
    // Backend Info
    string BackendName { get; }
    string ApiVersion { get; }
    string VendorName { get; }
    string DeviceName { get; }
    
    // Feature Support
    bool SupportsComputeShaders { get; }
    bool SupportsIndirectRendering { get; }
    bool SupportsTextureCompressionBC { get; }
    bool SupportsTextureCompressionASTC { get; }
    
    // Limits
    uint MaxTextureSize { get; }
    uint MaxViewports { get; }
    uint MaxRenderTargets { get; }
    uint MaxConstantBufferSize { get; }
    uint MaxStructuredBufferSize { get; }
    
    // Shader Language
    ShaderLanguage PreferredShaderLanguage { get; }
    ShaderLanguage[] SupportedShaderLanguages { get; }
}

// Usage
if (capabilities.SupportsComputeShaders)
{
    renderingPath = new GpuParticleRenderingPath();
}
else
{
    renderingPath = new CpuParticleRenderingPath();
}
```

#### 5. IBindingValidation (Debug Support)

```csharp
/// <summary>
/// Validates binding state before draw calls (debug mode only).
/// </summary>
public interface IBindingValidation
{
    bool ValidatePipelineSet(out string? errorMessage);
    bool ValidateRequiredResourceSetsBound(out string? errorMessage);
    bool ValidateVertexBuffersBound(out string? errorMessage);
    bool ValidateIndexBufferBound(out string? errorMessage);
    bool ValidateAllBindings(out string[]? errorMessages);
    
    string GetBindingDiagnostics();
}
```

---

## Implementation Strategy

### Phase 1: Core Abstraction

**Files to modify/create:**
1. `src/OpenSage.Graphics/Abstractions/IGraphicsCommand.cs` - Core interface
2. `src/OpenSage.Graphics/Veldrid/VeldridGraphicsCommand.cs` - Veldrid implementation
3. `src/OpenSage.Graphics/Abstractions/IResourceSetBuilder.cs` - Resource set building
4. `src/OpenSage.Graphics/Veldrid/VeldridResourceSetBuilder.cs` - Implementation

### Phase 2: Validation & Debug

**Files to create:**
1. `src/OpenSage.Graphics/Debug/BindingValidator.cs` - Validation logic
2. `src/OpenSage.Graphics/Debug/DebugGraphicsCommand.cs` - Wrapper for debug mode

### Phase 3: Capability System

**Files to create:**
1. `src/OpenSage.Graphics/Abstractions/ICapabilityQuery.cs` - Capability interface
2. `src/OpenSage.Graphics/Veldrid/VeldridCapabilityQuery.cs` - Implementation

---

## Detailed Interface Designs

### Complete IGraphicsCommand Implementation

```csharp
namespace OpenSage.Graphics.Abstractions;

public interface IGraphicsCommand : IDisposable
{
    // ===== Frame Management =====
    
    /// <summary>
    /// Begins recording a new command list for the current frame.
    /// </summary>
    void BeginFrame();
    
    /// <summary>
    /// Ends command list recording and submits to GPU.
    /// </summary>
    void EndFrame();
    
    // ===== Pipeline State =====
    
    /// <summary>
    /// Sets the graphics pipeline for subsequent draw calls.
    /// Pipeline contains all rendering state: blend, depth, rasterizer, shaders.
    /// </summary>
    void SetPipeline(IPipeline pipeline);
    
    /// <summary>
    /// Gets the currently bound pipeline.
    /// </summary>
    IPipeline? CurrentPipeline { get; }
    
    // ===== Viewport & Scissor =====
    
    /// <summary>
    /// Sets the viewport for coordinate transformation.
    /// </summary>
    void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f);
    
    /// <summary>
    /// Sets the scissor rectangle for pixel-level clipping.
    /// </summary>
    void SetScissor(int x, int y, int width, int height);
    
    // ===== Render Target =====
    
    /// <summary>
    /// Sets the framebuffer (render target collection).
    /// </summary>
    void SetFramebuffer(IFramebuffer framebuffer);
    
    /// <summary>
    /// Clears the current framebuffer.
    /// </summary>
    void Clear(Vector4 color, float depth = 1.0f, byte stencil = 0,
        bool clearColor = true, bool clearDepth = true, bool clearStencil = false);
    
    // ===== Buffer Binding =====
    
    /// <summary>
    /// Binds a vertex buffer to the specified slot.
    /// </summary>
    void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0);
    
    /// <summary>
    /// Binds an index buffer.
    /// </summary>
    void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0);
    
    // ===== Resource Set Binding =====
    
    /// <summary>
    /// Binds a resource set (grouped resources: buffers, textures, samplers).
    /// </summary>
    void SetResourceSet(uint slot, IResourceSet resourceSet);
    
    // ===== Batched Binding =====
    
    /// <summary>
    /// Starts a binding batch for multiple binding operations.
    /// Useful for minimizing function call overhead.
    /// </summary>
    IBindingBatch StartBindingBatch();
    
    // ===== Drawing =====
    
    /// <summary>
    /// Draws indexed geometry (geometry using an index buffer).
    /// </summary>
    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0);
    
    /// <summary>
    /// Draws non-indexed geometry (geometry from vertex buffer directly).
    /// </summary>
    void Draw(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0);
    
    /// <summary>
    /// Draws indexed geometry using parameters from a buffer (indirect drawing).
    /// </summary>
    void DrawIndexedIndirect(IBuffer indirectBuffer, uint offset, uint drawCount, uint stride);
    
    /// <summary>
    /// Draws non-indexed geometry using parameters from a buffer (indirect drawing).
    /// </summary>
    void DrawIndirect(IBuffer indirectBuffer, uint offset, uint drawCount, uint stride);
    
    // ===== Debug Support =====
    
    /// <summary>
    /// Pushes a debug group marker onto the debug group stack.
    /// Visible in GPU profilers like RenderDoc.
    /// </summary>
    void PushDebugGroup(string name);
    
    /// <summary>
    /// Pops the current debug group.
    /// </summary>
    void PopDebugGroup();
    
    /// <summary>
    /// Inserts a debug marker at the current position.
    /// </summary>
    void InsertDebugMarker(string name);
    
    // ===== Diagnostics =====
    
    /// <summary>
    /// Gets the currently bound state for diagnostics.
    /// </summary>
    IGraphicsCommandState State { get; }
}

/// <summary>
/// Diagnostic state of bound resources.
/// </summary>
public interface IGraphicsCommandState
{
    IPipeline? BoundPipeline { get; }
    IBuffer? BoundIndexBuffer { get; }
    Dictionary<uint, IBuffer> BoundVertexBuffers { get; }
    Dictionary<uint, IResourceSet> BoundResourceSets { get; }
}
```

### Veldrid Implementation (Minimal Wrapper)

```csharp
namespace OpenSage.Graphics.Veldrid;

public class VeldridGraphicsCommand : IGraphicsCommand
{
    private readonly CommandList _commandList;
    private readonly GraphicsDevice _graphicsDevice;
    private IGraphicsCommandState _state;
    
    public VeldridGraphicsCommand(CommandList commandList, GraphicsDevice graphicsDevice)
    {
        _commandList = commandList ?? throw new ArgumentNullException(nameof(commandList));
        _graphicsDevice = graphicsDevice ?? throw new ArgumentNullException(nameof(graphicsDevice));
        _state = new GraphicsCommandState();
    }
    
    public void BeginFrame()
    {
        _commandList.Begin();
    }
    
    public void EndFrame()
    {
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
    }
    
    public void SetPipeline(IPipeline pipeline)
    {
        if (pipeline is not VeldridPipeline vPipeline)
            throw new GraphicsException("Invalid pipeline type");
        
        _commandList.SetPipeline(vPipeline.VeldridPipeline);
        _state.BoundPipeline = pipeline;
    }
    
    public void SetVertexBuffer(uint slot, IBuffer buffer, uint offset = 0)
    {
        if (buffer is not VeldridBuffer vBuffer)
            throw new GraphicsException("Invalid buffer type");
        
        _commandList.SetVertexBuffer(slot, vBuffer.VeldridBuffer, offset);
        _state.BoundVertexBuffers[slot] = buffer;
    }
    
    public void SetIndexBuffer(IBuffer buffer, IndexFormat format, uint offset = 0)
    {
        if (buffer is not VeldridBuffer vBuffer)
            throw new GraphicsException("Invalid buffer type");
        
        var vFormat = format switch
        {
            IndexFormat.UInt16 => Veldrid.IndexFormat.UInt16,
            IndexFormat.UInt32 => Veldrid.IndexFormat.UInt32,
            _ => throw new GraphicsException($"Unsupported index format: {format}")
        };
        
        _commandList.SetIndexBuffer(vBuffer.VeldridBuffer, vFormat, offset);
        _state.BoundIndexBuffer = buffer;
    }
    
    public void SetResourceSet(uint slot, IResourceSet resourceSet)
    {
        if (resourceSet is not VeldridResourceSet vResourceSet)
            throw new GraphicsException("Invalid resource set type");
        
        _commandList.SetGraphicsResourceSet(slot, vResourceSet.VeldridResourceSet);
        _state.BoundResourceSets[slot] = resourceSet;
    }
    
    public IBindingBatch StartBindingBatch()
    {
        return new VeldridBindingBatch(this, _commandList);
    }
    
    public void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0)
    {
        _commandList.DrawIndexed(indexCount, instanceCount, startIndex, baseVertex, startInstance);
    }
    
    public void Draw(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0)
    {
        _commandList.Draw(vertexCount, instanceCount, startVertex, startInstance);
    }
    
    // ... rest of implementation ...
    
    public IGraphicsCommandState State => _state;
}
```

---

## Migration Path for Existing Code

### Before (Direct Veldrid)
```csharp
commandList.SetPipeline(material.Pipeline);
commandList.SetGraphicsResourceSet(0, globalResourceSet);
commandList.SetGraphicsResourceSet(1, materialResourceSet);
commandList.SetVertexBuffer(0, vertexBuffer);
commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
commandList.DrawIndexed(indexCount, 1, startIndex, 0, 0);
```

### After (Abstraction Layer)
```csharp
using (var batch = cmd.StartBindingBatch())
{
    batch.SetPipeline(material.Pipeline)
         .SetResourceSet(0, globalResourceSet)
         .SetResourceSet(1, materialResourceSet)
         .SetVertexBuffer(0, vertexBuffer)
         .SetIndexBuffer(indexBuffer, IndexFormat.UInt16)
         .Flush();
}

cmd.DrawIndexed(indexCount, 1, startIndex, 0, 0);
```

### Gradual Migration
```csharp
// Phase 1: Create IGraphicsCommand wrapper
var cmd = new VeldridGraphicsCommand(commandList, graphicsDevice);

// Phase 2: Replace direct calls incrementally
cmd.SetPipeline(material.Pipeline);
cmd.SetResourceSet(0, globalResourceSet);
cmd.SetResourceSet(1, materialResourceSet);
cmd.SetVertexBuffer(0, vertexBuffer);
cmd.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
commandList.DrawIndexed(indexCount, 1, startIndex, 0, 0);  // Still direct

// Phase 3: Replace all calls
cmd.DrawIndexed(indexCount, 1, startIndex, 0, 0);
```

---

## Performance Considerations

### Optimization Patterns to Preserve

1. **Pipeline Caching**: By key, not by creation order
2. **Resource Set Caching**: By unique resource combination
3. **State Change Minimization**: Check before SetPipeline
4. **Lazy Constant Buffer Updates**: Only update when values change

### Abstraction Layer Overhead

| Operation | Overhead | Mitigation |
|-----------|----------|-----------|
| SetPipeline call | 1 virtual call | Negligible (GPU work dominates) |
| SetResourceSet call | 1 virtual call | Negligible |
| Batching | ~5-10% reduction | Use IBindingBatch for complex setups |
| Validation (debug) | ~5-15% overhead | Debug-only, stripped in release |

**Conclusion**: Abstraction overhead is negligible compared to GPU work.

---

## Testing Strategy

### Unit Tests
```csharp
[TestFixture]
public class BindingValidationTests
{
    [Test]
    public void DrawIndexed_WithoutPipeline_ThrowsValidationError()
    {
        var cmd = new DebugGraphicsCommand(veldridCmd);
        var validator = cmd.GetValidator();
        
        // Draw without setting pipeline
        Assert.Throws<GraphicsException>(() => cmd.DrawIndexed(100));
    }
    
    [Test]
    public void SetPipeline_ThenDraw_Succeeds()
    {
        var cmd = new DebugGraphicsCommand(veldridCmd);
        cmd.SetPipeline(pipeline);
        cmd.SetVertexBuffer(0, vertexBuffer);
        cmd.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
        
        Assert.DoesNotThrow(() => cmd.DrawIndexed(100));
    }
}
```

### Integration Tests
```csharp
[TestFixture]
public class RenderingIntegrationTests
{
    [Test]
    public void RenderPipeline_WithAbstraction_ProducesSameResult()
    {
        // Render with old code (direct Veldrid)
        var result1 = RenderWithDirectVeldrid();
        
        // Render with abstraction layer
        var result2 = RenderWithAbstraction();
        
        // Results should be identical
        Assert.That(result1.PixelData, Is.EqualTo(result2.PixelData));
    }
}
```

---

## Next Steps

1. **Implement Core Interfaces** (Week 9 Phase 1)
   - Create IGraphicsCommand, IResourceSetBuilder
   - Implement Veldrid wrapper classes
   
2. **Add Validation Layer** (Week 9 Phase 2)
   - Implement IBindingValidation
   - Create DebugGraphicsCommand wrapper
   
3. **Implement Capability System** (Week 9 Phase 3)
   - Create ICapabilityQuery
   - Integrate with existing LodPreset system
   
4. **Migrate Existing Code** (Week 10)
   - Update RenderPipeline to use IGraphicsCommand
   - Update SpriteBatch, ImGuiRenderer
   - Update all rendering systems incrementally

5. **Performance Validation** (Week 10)
   - Profile before/after overhead
   - Optimize virtual call chains if needed
   - Benchmark resource set caching

---

## References

- [Graphics Binding Research Complete](GRAPHICS_BINDING_RESEARCH_COMPLETE.md)
- [Graphics Binding Quick Reference](GRAPHICS_BINDING_QUICK_REFERENCE.md)
- [RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs)
- [VeldridGraphicsDevice.cs](src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs)
