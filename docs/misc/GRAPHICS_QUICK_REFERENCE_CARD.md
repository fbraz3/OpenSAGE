# Graphics System - Quick Reference Card

**Print this for your desk!**

---

## Binding Sequence (Most Common Pattern)

```
┌─ Setup Phase ──────────────────────────┐
│ 1. SetPipeline(material.Pipeline)       │
│ 2. SetResourceSet(0, globalResources)   │
│ 3. SetResourceSet(1, passResources)     │
│ 4. SetResourceSet(2, materialResources) │
├─ Buffer Binding ──────────────────────┤
│ 5. SetVertexBuffer(0, vertexBuffer)    │
│ 6. SetIndexBuffer(indexBuffer)         │
├─ Draw ────────────────────────────────┤
│ 7. DrawIndexed(indexCount, ...)        │
└────────────────────────────────────────┘
```

---

## Resource Set Slots

```
Slot 0: Global Resource Set
  └─ Global Constants Buffer
  
Slot 1: Pass Resource Set
  ├─ Lighting Constants (VS)
  ├─ Lighting Constants (PS)
  ├─ Cloud Texture + Sampler
  └─ Shadow Map + Sampler
  
Slot 2: Material Resource Set
  ├─ Material Constants
  ├─ Diffuse Texture
  └─ Secondary Texture
  
Slot 3+: Special Resources (varies by shader)
```

---

## Code Snippets

### Minimal Rendering
```csharp
commandList.SetPipeline(pipeline);
commandList.SetGraphicsResourceSet(0, resourceSet);
commandList.SetVertexBuffer(0, vertexBuffer);
commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
commandList.DrawIndexed(indexCount, 1, 0, 0, 0);
```

### With Resource Set Batching
```csharp
commandList.SetPipeline(pipeline);
commandList.SetGraphicsResourceSet(0, global);
commandList.SetGraphicsResourceSet(1, material);
commandList.SetVertexBuffer(0, vb);
commandList.SetIndexBuffer(ib, IndexFormat.UInt16);
commandList.DrawIndexed(indexCount, 1, startIdx, 0, 0);
```

### Dynamic Update (Sprite)
```csharp
commandList.UpdateBuffer(buffer, 0, vertices);
commandList.SetVertexBuffer(0, buffer);
commandList.SetGraphicsResourceSet(2, textureSet);
commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);
commandList.DrawIndexed(6);  // Quad = 6 indices
```

---

## Pipeline Creation Pattern

```csharp
// Create once, cache by key
var pipeline = graphicsDevice.ResourceFactory
    .CreateGraphicsPipeline(
        new GraphicsPipelineDescription(
            blendState,
            depthState,
            rasterizerState,
            PrimitiveTopology.TriangleList,
            vertexLayouts,
            resourceLayouts,
            outputDescription));

// Store in cache
_pipelineCache[key] = pipeline;

// Use many times
commandList.SetPipeline(pipeline);
```

---

## Resource Set Creation

```csharp
// 1. Create layout (once)
var layout = gd.ResourceFactory.CreateResourceLayout(
    new ResourceLayoutDescription(
        new ResourceLayoutElementDescription(
            "MyBuffer", ResourceKind.UniformBuffer, 
            ShaderStages.Vertex)));

// 2. Create resources
var buffer = gd.ResourceFactory.CreateBuffer(...);
var texture = gd.ResourceFactory.CreateTexture(...);
var sampler = gd.ResourceFactory.CreateSampler(...);

// 3. Create resource set
var resourceSet = gd.ResourceFactory.CreateResourceSet(
    new ResourceSetDescription(layout, buffer, texture, sampler));

// 4. Bind when needed
commandList.SetGraphicsResourceSet(slot, resourceSet);
```

---

## DrawIndexed Parameters Quick Guide

```csharp
DrawIndexed(
    indexCount,      // How many indices (total geometry)
    instanceCount,   // How many copies (usually 1)
    startIndex,      // First index in buffer (0 = from start)
    baseVertex,      // Vertex offset (0 = no offset)
    startInstance)   // Instance offset (0 = no offset)

// Examples:
DrawIndexed(100, 1, 0, 0, 0)        // Simple: draw 100 indices
DrawIndexed(50, 1, 50, 0, 0)        // Offset: start at index 50
DrawIndexed(24, 10, 0, 0, 0)        // Instanced: 10 copies
DrawIndexed(24, 10, 0, 100, 0)      // Instance + vertex offset
```

---

## Validation Checklist

Before calling DrawIndexed, ensure:

- [ ] Pipeline is set (`SetPipeline` called)
- [ ] All required resource sets are bound (`SetGraphicsResourceSet`)
- [ ] Vertex buffer is bound (`SetVertexBuffer`)
- [ ] Index buffer is bound (`SetIndexBuffer`)
- [ ] Framebuffer is set (if not default)
- [ ] Viewport is set (if custom)
- [ ] IndexBuffer matches drawing vertex layout

---

## Performance Tips

✅ **DO**:
- Cache pipelines by state key
- Cache resource sets by resource combination
- Check pipeline before `SetPipeline`
- Update constant buffers only when values change
- Use batching for multiple binding operations

❌ **DON'T**:
- Create new pipelines every frame
- Recreate resource sets every frame
- Set pipeline unconditionally
- Update unchanged constant buffers
- Create multiple small resource sets (group them)

---

## GPU Capability Check

```csharp
// Query capabilities
var caps = graphicsDevice.Capabilities;

if (caps.SupportsComputeShaders)
    UseGpuParticles();
else
    UseCpuParticles();

if (caps.SupportsIndirectRendering)
    UseIndirectDraws();
else
    UseDirectDraws();
```

---

## Backend-Specific Shaders

```csharp
// Shader compilation adapts to backend
target = backend switch {
    Direct3D11 => CrossCompileTarget.HLSL,     // .hlsl
    OpenGL => CrossCompileTarget.GLSL,         // .glsl
    OpenGLES => CrossCompileTarget.GLSL_ES,    // .glsles
    Metal => CrossCompileTarget.MSL,           // .metallib
    Vulkan => CrossCompileTarget.HLSL,         // .spv (via HLSL)
};
```

---

## Debug Markers (Profiler)

```csharp
commandList.PushDebugGroup("Render opaque objects");
{
    // ... rendering ...
    commandList.InsertDebugMarker("Draw tree");
    commandList.DrawIndexed(...);
}
commandList.PopDebugGroup();

// RenderDoc will show hierarchy:
// ├─ Render opaque objects
//     ├─ Draw tree
//     └─ ...
```

---

## Real Example: TerrainPatch

```csharp
public override void Render(CommandList commandList)
{
    commandList.SetVertexBuffer(0, _vertexBuffer);
    commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
    commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
    // That's it! (pipeline & resources set by caller)
}
```

---

## Real Example: SpriteBatch

```csharp
public void End()
{
    for (var i = 0; i < _currentBatchIndex; i++)
    {
        ref var item = ref _batchItems[i];
        
        // Update vertex data
        _vertices[0..4] = item.GetVertices();
        _commandList.UpdateBuffer(_vertexBuffer, 0, _vertices);
        
        // Bind and draw
        _commandList.SetVertexBuffer(0, _vertexBuffer);
        _commandList.SetGraphicsResourceSet(2, GetTextureResourceSet(item.Texture));
        _commandList.SetGraphicsResourceSet(3, GetAlphaMaskResourceSet(item.AlphaMask));
        _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        
        var count = item.ItemType == Quad ? 6u : 3u;
        _commandList.DrawIndexed(count);
    }
}
```

---

## File References

| Need | File | Line |
|------|------|------|
| **Complete pipeline** | RenderPipeline.cs | 227-290 |
| **Binding pattern** | SpriteBatch.cs | 76-105 |
| **Resource management** | GlobalShaderResourceData.cs | 31-64 |
| **Pipeline caching** | FixedFunctionShaderResources.cs | 73-100 |
| **Simple example** | TerrainPatch.cs | 145-151 |
| **Dynamic updates** | ParticleSystem.cs | 527-531 |
| **ImGui pattern** | ImGuiRenderer.cs | 157-181 |

---

## Architecture at a Glance

```
Application Code
        ↓
RenderPipeline.Execute()
        ├─→ BuildRenderList()
        ├─→ Render3DScene()
        │   ├─→ Shadow Pass (DoRenderPass)
        │   ├─→ Forward Pass
        │   │   ├─ Opaque (DoRenderPass)
        │   │   ├─ Transparent (DoRenderPass)
        │   │   └─ Water (DoRenderPass)
        │   └─→ Copy to Backbuffer
        └─→ Submit to GPU
        
DoRenderPass(bucket)
        ├─ For each render item:
        │   ├─ SetPipeline (if changed)
        │   ├─ SetResourceSets
        │   ├─ SetVertexBuffer
        │   ├─ SetIndexBuffer
        │   └─ DrawIndexed
        └─ Return item count
```

---

**Print & Post on Your Monitor!**

This card covers 90% of graphics binding patterns in OpenSAGE.

For complete details, see GRAPHICS_BINDING_QUICK_REFERENCE.md
