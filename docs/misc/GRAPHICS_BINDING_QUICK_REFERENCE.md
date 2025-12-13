# Graphics Binding System - Quick Implementation Reference

**Purpose**: Fast lookup guide for implementing graphics abstraction layer  
**Created**: December 12, 2025

---

## 1. Vertex Buffer Binding

### Quick Pattern
```csharp
// Bind vertex buffer to slot
commandList.SetVertexBuffer(uint slot, DeviceBuffer buffer)
commandList.SetVertexBuffer(0, myVertexBuffer);

// With offset
commandList.SetVertexBuffer(0, myVertexBuffer);  // Implicit offset 0
```

### Creation Pattern
```csharp
// Static (created once)
DeviceBuffer vb = graphicsDevice.CreateStaticBuffer(vertices, BufferUsage.VertexBuffer);

// Dynamic (updated per frame)
DeviceBuffer vb = graphicsDevice.ResourceFactory.CreateBuffer(
    new BufferDescription(
        (uint)(stride * vertexCount),
        BufferUsage.VertexBuffer | BufferUsage.Dynamic));

// Update before rendering
commandList.UpdateBuffer(vb, 0, newVertexData);
```

### Real Example: TerrainPatch
```csharp
public override void Render(CommandList commandList)
{
    commandList.SetVertexBuffer(0, _vertexBuffer);
    commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
    commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
}
```

---

## 2. Index Buffer Binding

### Quick Pattern
```csharp
commandList.SetIndexBuffer(DeviceBuffer buffer, IndexFormat format)
commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
```

### Creation Pattern
```csharp
// Static index buffer
DeviceBuffer ib = graphicsDevice.CreateStaticBuffer(
    indices,  // ushort[]
    BufferUsage.IndexBuffer);
```

### Key Facts
- OpenSAGE uses **always UInt16** format
- Must be set **before DrawIndexed call**
- Index count passed to DrawIndexed, not from buffer

---

## 3. Uniform Buffer Binding

### Quick Pattern (Via ResourceSet)
```csharp
// Create resource layout (describes slots)
var layout = graphicsDevice.ResourceFactory.CreateResourceLayout(
    new ResourceLayoutDescription(
        new ResourceLayoutElementDescription("MyConstants", ResourceKind.UniformBuffer, ShaderStages.Vertex)));

// Create buffer
DeviceBuffer buf = graphicsDevice.ResourceFactory.CreateBuffer(
    new BufferDescription(64, BufferUsage.UniformBuffer | BufferUsage.Dynamic));

// Create resource set
var resourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(
    new ResourceSetDescription(layout, buf));

// Bind
commandList.SetGraphicsResourceSet(slot, resourceSet);
```

### ConstantBuffer Pattern (Type-Safe)
```csharp
// Declaration
private ConstantBuffer<GlobalShaderResources.GlobalConstants> _globalConstantBuffer;

// Creation
_globalConstantBuffer = new ConstantBuffer<GlobalShaderResources.GlobalConstants>(
    graphicsDevice, "GlobalConstants");

// Update
_globalConstantBuffer.Value.CameraPosition = cameraPos;
_globalConstantBuffer.Value.TimeInSeconds = time;
_globalConstantBuffer.Update(commandList);

// Create resource set with buffer
GlobalConstantsResourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(
    new ResourceSetDescription(
        globalShaderResources.GlobalConstantsResourceLayout,
        _globalConstantBuffer.Buffer));
```

### Real Example: GlobalShaderResourceData
```csharp
public ResourceSet GetForwardPassResourceSet(
    Texture cloudTexture,
    ConstantBuffer<GlobalShaderResources.ShadowConstantsPS> shadowConstantsPSBuffer,
    Texture shadowMap)
{
    return _graphicsDevice.ResourceFactory.CreateResourceSet(
        new ResourceSetDescription(
            _globalShaderResources.ForwardPassResourceLayout,
            _globalLightingBufferVS.Buffer,           // Slot 0
            _globalLightingBufferPS.Buffer,           // Slot 1
            cloudTexture,                             // Slot 2
            _graphicsDevice.Aniso4xSampler,           // Slot 3
            shadowConstantsPSBuffer.Buffer,           // Slot 4
            shadowMap,                                // Slot 5
            _globalShaderResources.ShadowSampler,     // Slot 6
            _globalShaderResources.RadiusCursorDecals.TextureArray,
            _standardGraphicsResources.Aniso4xClampSampler,
            _globalShaderResources.RadiusCursorDecals.DecalConstants,
            _globalShaderResources.RadiusCursorDecals.DecalsBuffer));
}
```

---

## 4. Texture Binding

### Quick Pattern (Via ResourceSet)
```csharp
// Create texture layout
var layout = graphicsDevice.ResourceFactory.CreateResourceLayout(
    new ResourceLayoutDescription(
        new ResourceLayoutElementDescription("MainTexture", ResourceKind.TextureReadOnly, ShaderStages.Fragment),
        new ResourceLayoutElementDescription("MainSampler", ResourceKind.Sampler, ShaderStages.Fragment)));

// Create resource set
var resourceSet = graphicsDevice.ResourceFactory.CreateResourceSet(
    new ResourceSetDescription(layout, myTexture, mySampler));

// Bind
commandList.SetGraphicsResourceSet(slot, resourceSet);
```

### Caching Pattern (SpriteBatch)
```csharp
private ResourceSet GetTextureResourceSet(Texture texture)
{
    if (!_textureResourceSets.TryGetValue(texture, out var result))
    {
        result = _spriteShaderResources.CreateTextureResourceSet(texture);
        _textureResourceSets.Add(texture, result);
    }
    return result;
}

// Usage
var textureResourceSet = GetTextureResourceSet(batchItem.Texture);
_commandList.SetGraphicsResourceSet(2, textureResourceSet);
```

### Real Example: Fixed-Function Material
```csharp
private ResourceSet GetCachedMaterialResourceSet(in MaterialConstantsKey key)
{
    if (!_materialConstantsCache.TryGetValue(key, out var result))
    {
        result = GraphicsDevice.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(
                MaterialResourceLayout,
                materialConstantsBuffer,      // Slot 0
                key.Texture0,                 // Slot 1
                key.Texture1,                 // Slot 2
                GraphicsDevice.Aniso4xSampler)); // Slot 3
        
        _materialConstantsCache.Add(key, result);
    }
    return result;
}
```

---

## 5. Pipeline (SetPipeline)

### Quick Pattern
```csharp
// Create pipeline
var pipeline = graphicsDevice.ResourceFactory.CreateGraphicsPipeline(
    new GraphicsPipelineDescription(
        BlendStateDescription.SingleAlphaBlend,
        DepthStencilStateDescription.DepthOnlyLessEqual,
        RasterizerStateDescriptionUtility.DefaultFrontIsCounterClockwise,
        PrimitiveTopology.TriangleList,
        vertexLayouts,
        resourceLayouts,
        outputDescription));

// Set pipeline
commandList.SetPipeline(pipeline);
```

### Caching Pattern
```csharp
private Dictionary<PipelineKey, Pipeline> _pipelineCache = new();

private Pipeline GetCachedPipeline(in PipelineKey pipelineKey)
{
    if (!_pipelineCache.TryGetValue(pipelineKey, out var result))
    {
        // Create pipeline...
        _pipelineCache.Add(pipelineKey, result);
    }
    return result;
}
```

### Optimization Pattern (Minimize Changes)
```csharp
int? lastRenderItemIndex = null;

foreach (var i in renderItems)
{
    ref var renderItem = ref renderItems[i];
    
    // Only change pipeline if needed
    var newMaterial = true;
    if (lastRenderItemIndex != null)
    {
        var lastMaterial = renderItems[lastRenderItemIndex.Value].Material;
        newMaterial = lastMaterial.Pipeline != renderItem.Material.Pipeline;
    }
    
    if (newMaterial)
    {
        commandList.SetPipeline(renderItem.Material.Pipeline);
        SetGlobalResources(commandList, passResourceSet);
    }
    
    // ... rest of rendering ...
    
    lastRenderItemIndex = i;
}
```

---

## 6. Complete Rendering Sequence

### Minimal Example
```csharp
// 1. Set pipeline
commandList.SetPipeline(myPipeline);

// 2. Bind resources
commandList.SetGraphicsResourceSet(0, globalResourceSet);
commandList.SetGraphicsResourceSet(1, materialResourceSet);

// 3. Bind vertex/index buffers
commandList.SetVertexBuffer(0, vertexBuffer);
commandList.SetIndexBuffer(indexBuffer, IndexFormat.UInt16);

// 4. Draw
commandList.DrawIndexed(indexCount, 1, 0, 0, 0);
```

### Real Production Example (SpriteBatch.End)
```csharp
for (var i = 0; i < _currentBatchIndex; i++)
{
    ref var batchItem = ref _batchItems[i];
    
    // Update dynamic constants if changed
    if (batchItem.OutputOffset != _spriteConstantsPSBuffer.Value.OutputOffset
        || batchItem.OutputSize != _spriteConstantsPSBuffer.Value.OutputSize
        || batchItem.FillMethod != _spriteConstantsPSBuffer.Value.FillMethod
        || batchItem.FillAmount != _spriteConstantsPSBuffer.Value.FillAmount
        || batchItem.Grayscale != _spriteConstantsPSBuffer.Value.Grayscale)
    {
        _spriteConstantsPSBuffer.Value.OutputOffset = batchItem.OutputOffset;
        _spriteConstantsPSBuffer.Value.OutputSize = batchItem.OutputSize;
        _spriteConstantsPSBuffer.Value.FillMethod = batchItem.FillMethod;
        _spriteConstantsPSBuffer.Value.FillAmount = batchItem.FillAmount;
        _spriteConstantsPSBuffer.Value.Grayscale = batchItem.Grayscale;
        _spriteConstantsPSBuffer.Update(_commandList);
    }
    
    // Update vertex buffer
    _vertices[0] = batchItem.VertexTL;
    _vertices[1] = batchItem.VertexTR;
    _vertices[2] = batchItem.VertexBL;
    _vertices[3] = batchItem.VertexBR;
    _commandList.UpdateBuffer(_vertexBuffer, 0, _vertices);
    
    // Bind buffers and resources
    _commandList.SetVertexBuffer(0, _vertexBuffer);
    _commandList.SetGraphicsResourceSet(2, GetTextureResourceSet(batchItem.Texture));
    _commandList.SetGraphicsResourceSet(3, GetAlphaMaskResourceSet(batchItem.AlphaMask ?? _solidWhiteTexture));
    _commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
    
    // Draw
    var indexCount = batchItem.ItemType == SpriteBatchItemType.Quad ? 6u : 3u;
    _commandList.DrawIndexed(indexCount);
}
```

---

## 7. DrawIndexed Parameters

### Signature
```csharp
void DrawIndexed(
    uint indexCount,           // How many indices to draw
    uint instanceCount = 1,    // Number of instances
    uint startIndex = 0,       // First index in buffer
    int baseVertex = 0,        // Vertex offset
    uint startInstance = 0)    // Instance offset
```

### Quick Reference
```csharp
// Simple: Draw all indices once
commandList.DrawIndexed(indexCount, 1, 0, 0, 0);

// With start offset
commandList.DrawIndexed(indexCount, 1, startIndex, 0, 0);

// With vertex offset (useful for vertex buffer sharing)
commandList.DrawIndexed(indexCount, 1, 0, baseVertex, 0);

// Instanced rendering
commandList.DrawIndexed(indexCount, instanceCount, 0, 0, 0);

// All parameters
commandList.DrawIndexed(indexCount, instanceCount, startIndex, baseVertex, startInstance);
```

### Real Examples

**TerrainPatch (Simple)**
```csharp
commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
```

**RenderPipeline (With Offsets)**
```csharp
commandList.DrawIndexed(
    renderItem.IndexCount,
    1,
    renderItem.StartIndex,
    0,
    0);
```

**ParticleSystem (Dynamic)**
```csharp
commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
```

---

## 8. DrawVertices (Non-Indexed)

### Signature
```csharp
void Draw(
    uint vertexCount,         // How many vertices to draw
    uint instanceCount = 1,   // Number of instances
    uint startVertex = 0,     // First vertex in buffer
    uint startInstance = 0)   // Instance offset
```

### Pattern
```csharp
// Only vertex buffer, no index buffer
commandList.SetVertexBuffer(0, vertexBuffer);
commandList.Draw(vertexCount, 1, 0, 0);
```

---

## 9. GPU Capability Queries

### Veldrid Capabilities
```csharp
// At device creation
var capabilities = new GraphicsCapabilities(
    isInitialized: true,
    backendName: device.BackendType.ToString(),
    apiVersion: device.ApiVersion.ToString(),
    vendorName: device.VendorName ?? "Unknown",
    deviceName: device.DeviceName ?? "Unknown",
    maxTextureSize: 16384,
    maxViewports: 16,
    maxRenderTargets: 8,
    supportsTextureCompressionBC: true,
    supportsComputeShaders: device.Features.ComputeShaders,
    supportsIndirectRendering: true
);

// Query in code
if (device.Capabilities.SupportsComputeShaders)
{
    // Use GPU particles
}
else
{
    // Fall back to CPU particles
}
```

### Backend-Specific Shader Compilation
```csharp
CrossCompileTarget target = backend switch {
    GraphicsBackend.Direct3D11 => CrossCompileTarget.HLSL,
    GraphicsBackend.OpenGL => CrossCompileTarget.GLSL,
    GraphicsBackend.OpenGLES => CrossCompileTarget.GLSL_ES,
    GraphicsBackend.Metal => CrossCompileTarget.MSL,
    GraphicsBackend.Vulkan => CrossCompileTarget.HLSL,
};
```

---

## 10. Common Patterns Summary

| Pattern | Purpose | Example |
|---------|---------|---------|
| **ResourceSet** | Group related resources | Uniform buffer + textures + sampler |
| **ConstantBuffer<T>** | Type-safe uniform buffer | Global constants, per-material constants |
| **Caching by Key** | Avoid redundant allocations | Pipeline cache, resource set cache |
| **Dynamic Update** | Per-frame changes | Sprite position, animation, lighting |
| **Debug Markers** | Profiler integration | PushDebugGroup, InsertDebugMarker |
| **Lazy Binding** | Check before setting | Only set pipeline if changed |
| **Resource Pool** | Lifecycle management | Handle generation validation |

---

## 11. Anti-Patterns to Avoid

❌ **Don't**: Recreate pipelines every frame
✅ **Do**: Cache pipelines by state key

❌ **Don't**: Set pipeline unconditionally
✅ **Do**: Check if pipeline changed before SetPipeline

❌ **Don't**: Create resource sets per draw call
✅ **Do**: Cache resource sets by resource combination

❌ **Don't**: Update constant buffers unconditionally
✅ **Do**: Update only when values change

❌ **Don't**: Mix individual buffer bindings with resource sets
✅ **Do**: Use resource sets exclusively

---

## 12. File References for Copy-Paste Code

| File | Content |
|------|---------|
| [RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs) | Complete 3D rendering pass, DoRenderPass pattern |
| [SpriteBatch.cs](src/OpenSage.Game/Graphics/SpriteBatch.cs) | Complete sprite rendering, resource binding |
| [GlobalShaderResourceData.cs](src/OpenSage.Game/Graphics/Shaders/GlobalShaderResourceData.cs) | Complex resource set creation |
| [FixedFunctionShaderResources.cs](src/OpenSage.Game/Graphics/Shaders/FixedFunctionShaderResources.cs) | Pipeline caching, material system |
| [ImGuiRenderer.cs](src/Veldrid.ImGui/ImGuiRenderer.cs) | Alternative pipeline creation, texture binding |
| [TerrainPatch.cs](src/OpenSage.Game/Terrain/TerrainPatch.cs) | Minimal rendering example |
| [ParticleSystem.cs](src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs) | Dynamic buffer updates |

---

**End of Quick Reference**
