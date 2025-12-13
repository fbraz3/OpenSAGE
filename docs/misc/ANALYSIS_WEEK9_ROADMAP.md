# OpenSAGE Graphics System - Week 9 Implementation Roadmap

**Objetivo**: Completar gaps identificados entre Phase 2 spec e implementação atual.

---

## EXECUTIVE SUMMARY

**Status Atual**: 70% funcional para Veldrid (CommandList, RenderPipeline, State Objects operacionais)

**Gaps Críticos**: 3 bloqueadores principais (Shader, Pipeline, Framebuffer)

**Timeline**: 13 horas de trabalho para completar criticais

**Success Criteria**: Triangle rendering com texturas e lighting completo

---

## DAY-BY-DAY IMPLEMENTATION PLAN (WEEK 9)

### MONDAY (Day 1) - Shader Compilation Integration

#### Task 1.1: Implement VeldridGraphicsDevice.CreateShaderProgram() 

**File**: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs`

**Current (Linha 253)**:
```csharp
public Handle<IShaderProgram> CreateShader(string name, ReadOnlySpan<byte> spirvData, string entryPoint = "main")
{
    // Placeholder - Week 9 will implement shader compilation
    uint id = _nextResourceId++;
    _shaders[id] = null;
    return new Handle<IShaderProgram>(id, 1);
}
```

**Changes Required**:

1. **Rename method** (IGraphicsDevice signature mismatch):
   ```csharp
   public IShaderProgram CreateShaderProgram(ShaderSource source)
   // ou
   public Handle<IShaderProgram> CreateShaderProgram(ShaderSource source)
   ```

2. **Add shader pool**:
   ```csharp
   private readonly ResourcePool<VeldridLib.Shader> _shaderPool;
   
   public VeldridGraphicsDevice(...)
   {
       _shaderPool = new ResourcePool<VeldridLib.Shader>(64);
       AddDisposable(_shaderPool);
   }
   ```

3. **Implement compilation**:
   ```csharp
   public IShaderProgram CreateShaderProgram(ShaderSource source)
   {
       if (source.Stage == ShaderStages.None)
           throw new GraphicsException("Shader stage cannot be None.");
       
       try
       {
           // Veldrid.SPIRV cross-compilation
           var veldridShader = _device.ResourceFactory.CreateFromSpirv(
               new ShaderDescription(
                   ConvertShaderStage(source.Stage),
                   source.SpirVBytes,
                   source.EntryPoint));
           
           // Pool allocation
           var poolHandle = _shaderPool.Allocate(veldridShader);
           
           // Wrap in adapter
           var adapter = new VeldridShaderProgram(veldridShader, source.EntryPoint);
           return adapter;
       }
       catch (Exception ex)
       {
           throw new GraphicsException(
               $"Failed to compile {source.Stage} shader {source.EntryPoint}: {ex.Message}",
               ex);
       }
   }
   ```

4. **Add conversion helper**:
   ```csharp
   private VeldridLib.ShaderStages ConvertShaderStage(ShaderStages stages)
       => stages switch
       {
           ShaderStages.Vertex => VeldridLib.ShaderStages.Vertex,
           ShaderStages.Fragment => VeldridLib.ShaderStages.Fragment,
           ShaderStages.Compute => VeldridLib.ShaderStages.Compute,
           ShaderStages.Geometry => VeldridLib.ShaderStages.Geometry,
           ShaderStages.TessControl => VeldridLib.ShaderStages.TessellationControl,
           ShaderStages.TessEval => VeldridLib.ShaderStages.TessellationEvaluation,
           _ => throw new ArgumentException($"Unsupported shader stage: {stages}")
       };
   ```

**Effort**: 2 horas (incluindo testes)

**Files to Create**: 
- `src/OpenSage.Graphics/Veldrid/VeldridShaderProgram.cs` (adapter)

---

#### Task 1.2: Create VeldridShaderProgram Adapter

**File**: `src/OpenSage.Graphics/Veldrid/VeldridShaderProgram.cs` (NEW)

```csharp
using System;
using VeldridLib = Veldrid;
using OpenSage.Graphics.Abstractions;

namespace OpenSage.Graphics.Veldrid;

/// <summary>
/// Veldrid implementation of IShaderProgram.
/// Wraps compiled shader and provides metadata.
/// </summary>
internal class VeldridShaderProgram : IShaderProgram
{
    private readonly VeldridLib.Shader _native;
    private bool _disposed;
    
    public uint Id { get; }
    public uint Generation { get; }
    public bool IsValid => !_disposed;
    public string Name { get; }
    public string EntryPoint { get; }
    
    public VeldridShaderProgram(VeldridLib.Shader native, string entryPoint, uint id = 0, uint generation = 1)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        EntryPoint = entryPoint ?? "main";
        Id = id;
        Generation = generation;
        Name = $"{native.Stage}_{entryPoint}";
    }
    
    public VeldridLib.Shader Native => _native;
    
    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}
```

**Effort**: 1 hora

---

### TUESDAY (Day 2) - Pipeline Creation

#### Task 2.1: Implement CreatePipeline()

**File**: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs` (linhas 282-289)

**Current**:
```csharp
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
```

**Implementation**:

```csharp
public Handle<IPipeline> CreatePipeline(
    Handle<IShaderProgram> vertexShader,
    Handle<IShaderProgram> fragmentShader,
    RasterState rasterState = default,
    DepthState depthState = default,
    BlendState blendState = default,
    StencilState stencilState = default)
{
    // Validate handles
    if (!vertexShader.IsValid || !fragmentShader.IsValid)
        throw new GraphicsException("Invalid shader handle");
    
    // Retrieve shader objects (need to implement GetShader)
    var vertShader = GetShaderNative(vertexShader);
    var fragShader = GetShaderNative(fragmentShader);
    
    // Build pipeline description
    var pipelineDesc = new VeldridLib.GraphicsPipelineDescription(
        blendStateDesc: ConvertBlendState(blendState),
        depthStencilStateDesc: ConvertDepthState(depthState),
        rasterizerState: ConvertRasterState(rasterState),
        primitiveTopology: VeldridLib.PrimitiveTopology.TriangleList,
        resourceLayouts: new[] { GetDefaultResourceLayout() },  // Global resources
        shaders: new[] { vertShader, fragShader },
        outputs: RenderPipeline.GameOutputDescription);  // From Game project
    
    try
    {
        var veldridPipeline = _device.ResourceFactory.CreateGraphicsPipeline(ref pipelineDesc);
        
        // Pool allocation
        var poolHandle = _pipelinePool.Allocate(veldridPipeline);
        return new Handle<IPipeline>(poolHandle.Index, poolHandle.Generation);
    }
    catch (Exception ex)
    {
        throw new GraphicsException(
            $"Failed to create graphics pipeline: {ex.Message}", ex);
    }
}
```

**Conversion Helpers**:
```csharp
private VeldridLib.BlendStateDescription ConvertBlendState(BlendState state)
{
    return new VeldridLib.BlendStateDescription(
        rtBlend: new VeldridLib.BlendAttachmentDescription(
            blendEnabled: state.Enabled,
            colorFunction: ConvertBlendOperation(state.ColorOperation),
            alphaFunction: ConvertBlendOperation(state.AlphaOperation),
            colorSrcFactor: ConvertBlendFactor(state.SourceColorFactor),
            colorDestFactor: ConvertBlendFactor(state.DestinationColorFactor),
            alphaSrcFactor: ConvertBlendFactor(state.SourceAlphaFactor),
            alphaDestFactor: ConvertBlendFactor(state.DestinationAlphaFactor)));
}

private VeldridLib.DepthStencilStateDescription ConvertDepthState(DepthState state, StencilState stencilState)
{
    return new VeldridLib.DepthStencilStateDescription(
        depthTestEnabled: state.TestEnabled,
        depthWriteEnabled: state.WriteEnabled,
        comparison: ConvertCompareFunction(state.CompareFunction),
        stencilTestEnabled: stencilState.TestEnabled,
        stencilFront: new VeldridLib.StencilBehaviorDescription(
            fail: ConvertStencilOp(stencilState.FrontFailOperation),
            pass: ConvertStencilOp(stencilState.FrontPassOperation),
            depthFail: ConvertStencilOp(stencilState.FrontDepthFailOperation),
            comparison: ConvertCompareFunction(stencilState.FrontCompareFunction)),
        stencilBack: new VeldridLib.StencilBehaviorDescription(...),  // Similar
        stencilReadMask: stencilState.ReadMask,
        stencilWriteMask: stencilState.WriteMask);
}

private VeldridLib.RasterizerStateDescription ConvertRasterState(RasterState state)
{
    return new VeldridLib.RasterizerStateDescription(
        fillMode: ConvertFillMode(state.FillMode),
        cullMode: ConvertCullMode(state.CullMode),
        frontFace: ConvertFrontFace(state.FrontFace),
        depthClipEnabled: state.DepthClamp,
        scissorTestEnabled: state.ScissorTest);
}
```

**Effort**: 3 horas (incluindo todos os helpers)

---

#### Task 2.2: Create VeldridPipeline Adapter

**File**: `src/OpenSage.Graphics/Veldrid/VeldridPipeline.cs` (NEW)

```csharp
internal class VeldridPipeline : IPipeline
{
    private readonly VeldridLib.Pipeline _native;
    
    public uint Id { get; }
    public uint Generation { get; }
    public bool IsValid { get; }
    
    public State.RasterState RasterState { get; }
    public State.BlendState BlendState { get; }
    public State.DepthState DepthState { get; }
    public State.StencilState StencilState { get; }
    
    public Handle<IShaderProgram> VertexShader { get; }
    public Handle<IShaderProgram> FragmentShader { get; }
    
    public VeldridPipeline(
        VeldridLib.Pipeline native,
        State.RasterState rasterState,
        State.BlendState blendState,
        State.DepthState depthState,
        State.StencilState stencilState,
        Handle<IShaderProgram> vertexShader,
        Handle<IShaderProgram> fragmentShader)
    {
        _native = native;
        RasterState = rasterState;
        BlendState = blendState;
        DepthState = depthState;
        StencilState = stencilState;
        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
    }
    
    public void Dispose() => _native?.Dispose();
}
```

**Effort**: 1 hora

---

### WEDNESDAY (Day 3) - Framebuffer & Binding

#### Task 3.1: Implement Framebuffer Creation

**File**: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs` (linhas 225-237)

**Current**:
```csharp
public Handle<IFramebuffer> CreateFramebuffer(Resources.FramebufferDescription desc)
{
    // Placeholder implementation - Week 9 will implement full framebuffer support
    var fb = _device.SwapchainFramebuffer;
    
    // Allocate from pool with generation validation
    var poolHandle = _framebufferPool.Allocate(fb);
    
    // Convert PoolHandle to Handle<IFramebuffer> using index as ID
    return new Handle<IFramebuffer>(poolHandle.Index, poolHandle.Generation);
}
```

**Implementation**:
```csharp
public Handle<IFramebuffer> CreateFramebuffer(Resources.FramebufferDescription desc)
{
    // Support both render targets and depth
    var colorTargets = desc.ColorTargets != null ? new VeldridLib.Texture[desc.ColorTargets.Length] : Array.Empty<VeldridLib.Texture>();
    
    for (int i = 0; i < colorTargets.Length; i++)
    {
        var textureHandle = desc.ColorTargets[i];
        colorTargets[i] = GetTextureNative(textureHandle) 
            ?? throw new GraphicsException($"Invalid color target handle {i}");
    }
    
    var depthTarget = desc.DepthTarget.IsValid 
        ? GetTextureNative(desc.DepthTarget) 
        : null;
    
    var framebufferDesc = new VeldridLib.FramebufferDescription(depthTarget, colorTargets);
    var veldridFb = _device.ResourceFactory.CreateFramebuffer(framebufferDesc);
    
    var poolHandle = _framebufferPool.Allocate(veldridFb);
    return new Handle<IFramebuffer>(poolHandle.Index, poolHandle.Generation);
}
```

**Effort**: 2 horas

---

#### Task 3.2: Implement Resource Binding

**Files to Update**:
- `BindVertexBuffer()` 
- `BindIndexBuffer()`
- `BindUniformBuffer()`
- `BindTexture()`

**Implementation Strategy**:

```csharp
public void BindVertexBuffer(Handle<IBuffer> buffer, uint offset = 0)
{
    var veldridBuf = GetBufferNative(buffer)
        ?? throw new GraphicsException($"Invalid buffer handle");
    
    _cmdList.SetVertexBuffer(0, veldridBuf, offset);
}

public void BindIndexBuffer(Handle<IBuffer> buffer, uint offset = 0)
{
    var veldridBuf = GetBufferNative(buffer)
        ?? throw new GraphicsException($"Invalid buffer handle");
    
    _cmdList.SetIndexBuffer(veldridBuf, VeldridLib.IndexFormat.UInt32, offset);
}

public void BindUniformBuffer(Handle<IBuffer> buffer, uint slot)
{
    // Requires ResourceSet integration (Week 10)
    throw new NotImplementedException("ResourceSet binding in Week 10");
}

public void BindTexture(Handle<ITexture> texture, uint slot, Handle<ISampler> sampler)
{
    // Requires ResourceSet integration (Week 10)
    throw new NotImplementedException("ResourceSet binding in Week 10");
}
```

**Effort**: 1 hora (vertex/index), defer uniform/texture binding to Week 10.

---

### THURSDAY (Day 4) - Integration Testing

#### Task 4.1: Unit Tests for Shader/Pipeline

**File**: `src/OpenSage.Graphics/Testing/ShaderCompilationTests.cs` (EXPAND)

**Test Cases**:
```csharp
[TestFixture]
public class ShaderPipelineTests
{
    private IGraphicsDevice _device;
    
    [SetUp]
    public void Setup()
    {
        var veldridDevice = GraphicsDeviceFactory.CreateVeldridDevice();
        _device = new VeldridGraphicsDevice(veldridDevice);
    }
    
    [Test]
    public void CreateShaderProgram_ValidSpirV_ReturnsHandle()
    {
        var spirv = LoadTestShaderSpirV("vertex.spv");
        var source = new ShaderSource(ShaderStages.Vertex, spirv, "main");
        
        var handle = _device.CreateShaderProgram(source);
        
        Assert.That(handle.IsValid);
    }
    
    [Test]
    public void CreatePipeline_ValidShaders_ReturnsHandle()
    {
        var vertShader = CreateTestShader(ShaderStages.Vertex);
        var fragShader = CreateTestShader(ShaderStages.Fragment);
        
        var handle = _device.CreatePipeline(
            vertShader, fragShader,
            RasterState.Solid,
            DepthState.Default,
            BlendState.Opaque);
        
        Assert.That(handle.IsValid);
    }
    
    [Test]
    public void BindVertexBuffer_ValidHandle_Succeeds()
    {
        var bufferDesc = new BufferDescription(256, BufferUsage.VertexBuffer);
        var buffer = _device.CreateBuffer(bufferDesc);
        
        _device.BeginFrame();
        _device.BindVertexBuffer(buffer);
        // Should not throw
    }
}
```

**Effort**: 2 horas

---

#### Task 4.2: Integration with RenderPipeline

**File**: `src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs`

**Required Changes**:
1. Use `IGraphicsDevice` interface instead of Veldrid directly
2. Update `Execute()` to use abstracted methods
3. Update shader/pipeline creation calls

**Effort**: 2 horas

---

### FRIDAY (Day 5) - Integration & Polish

#### Task 5.1: Implement GraphicsDeviceFactory

**File**: `src/OpenSage.Graphics/Factory/GraphicsDeviceFactory.cs` (NEW)

```csharp
public static class GraphicsDeviceFactory
{
    public static IGraphicsDevice CreateDevice(GraphicsBackend backend, GraphicsDeviceOptions? options = null)
    {
        return backend switch
        {
            GraphicsBackend.Veldrid => CreateVeldridDevice(options),
            GraphicsBackend.BGFX => throw new NotImplementedException("BGFX Week 10+"),
            _ => throw new ArgumentException($"Unknown backend: {backend}")
        };
    }
    
    private static IGraphicsDevice CreateVeldridDevice(GraphicsDeviceOptions? options)
    {
        var veldridOptions = new VeldridLib.GraphicsDeviceOptions
        {
            PreferStandardClipSpaceYDirection = true,
            PreferDepthRangeZeroToOne = true,
            Debug = options?.Debug ?? false
        };
        
        var veldridDevice = VeldridLib.GraphicsDevice.Create(
            window: null,  // Headless for now
            options: veldridOptions);
        
        return new VeldridGraphicsDevice(veldridDevice);
    }
}
```

**Effort**: 1 hora

---

#### Task 5.2: Documentation & Cleanup

**Files to Update**:
- `Phase_3_Core_Implementation.md` - Atualizar status Week 9
- `ANALYSIS_GRAPHICS_SYSTEM_STATUS.md` - Refresh findings
- Inline comments em VeldridGraphicsDevice

**Effort**: 2 horas

---

## WEEK 9 SUMMARY CHECKLIST

- [ ] **Day 1**: Shader compilation integrated (2h)
  - [ ] VeldridGraphicsDevice.CreateShaderProgram() implementado
  - [ ] VeldridShaderProgram adapter criado
  - [ ] Unit tests passando

- [ ] **Day 2**: Pipeline creation completo (3h)
  - [ ] CreatePipeline() implementado com todas conversões
  - [ ] VeldridPipeline adapter criado
  - [ ] State object conversions testadas

- [ ] **Day 3**: Framebuffer & binding (3h)
  - [ ] Framebuffer creation implementado
  - [ ] BindVertexBuffer/IndexBuffer implementados
  - [ ] Uniform/Texture binding deferred documentado

- [ ] **Day 4**: Integration testing (2h)
  - [ ] Unit tests para shader/pipeline
  - [ ] RenderPipeline integration começada
  - [ ] Edge cases documentados

- [ ] **Day 5**: Polish & docs (2h)
  - [ ] GraphicsDeviceFactory completo
  - [ ] Documentation atualizada
  - [ ] Code review + cleanup

**Total Hours**: 12 horas (within 13h estimate)

---

## PHASE 2 SPEC VALIDATION

**Post-Week 9 Coverage**:

| Design | Phase 2 Spec | Week 9 Implementation | Status |
|--------|-------------|---------------------|--------|
| Handle System | ✅ Generation validation | Fully implemented | ✅ Complete |
| State Objects | ✅ Immutable structs | Fully implemented | ✅ Complete |
| Graphics Device | ✅ Abstraction interface | Core methods done | ✅ 70% |
| Shader Compilation | ✅ SPIR-V → backend | Veldrid.SPIRV integrated | ✅ 100% |
| Pipeline Creation | ✅ State → GPU pipeline | State mapping complete | ✅ 100% |
| Resource Pooling | ✅ Generational validation | Fully operational | ✅ 100% |
| Framebuffer Support | ✅ Multi-RT capable | Basic implementation | ⚠️ 70% |
| Resource Binding | ✅ Vertex/Index/Uniform | Vertex/Index done | ⏳ 50% |

---

## NEXT PHASES (Week 10+)

### Week 10: ResourceSet Integration
- Uniform buffer descriptor sets
- Texture/sampler descriptor sets
- Dynamic binding during frame

### Week 11: BGFX Backend Architecture
- Encoder-based command recording
- View/framebuffer mapping
- Handle lifecycle matching BGFX model

### Week 12+: Advanced Features
- Compute shaders
- Indirect rendering
- GPU profiling integration

---

## CRITICAL SUCCESS FACTORS

1. **Type Safety**: Never cast Handle<T> implicitly
2. **Resource Cleanup**: Always use DisposableBase + AddDisposable
3. **Validation**: Check handle validity in public methods
4. **Error Messages**: Include context (stage, entry point, resource ID)
5. **Testing**: Unit tests for each new public method

---

## RISKS & MITIGATIONS

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|-----------|
| Veldrid.SPIRV API mismatch | Low | High | Test with glslc output early |
| Pipeline state enum mismatch | Medium | Medium | Create exhaustive conversion tests |
| ResourceSet binding complexity | Medium | High | Defer to Week 10, implement basic binding first |
| Handle wraparound (uint overflow) | Very Low | Critical | Document limitation, monitor in profiler |

---

