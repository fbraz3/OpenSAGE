# OpenSAGE Graphics System - Detailed Technical Findings

**Data**: 12 de dezembro de 2025  
**Escopo**: Achados técnicos profundos com code samples e caminhos de arquivo

---

## PARTE 1: GRAPHICS SYSTEM ORCHESTRATION

### 1.1 Game Loop Integration

**Arquivo**: [src/OpenSage.Game/Graphics/GraphicsSystem.cs](src/OpenSage.Game/Graphics/GraphicsSystem.cs)

**Padrão de Inicialização**:
```csharp
public override void Initialize()
{
    RenderPipeline = AddDisposable(new RenderPipeline(Game));
}
```

**Observações**:
- `AddDisposable()` → Padrão OpenSAGE para lifecycle management
- RenderPipeline é criado UMA VEZ na inicialização (não por frame)
- RenderPipeline é responsável por gerenciar CommandList, framebuffers, shaders

**Frame Execution**:
```csharp
internal void Draw(in TimeInterval gameTime)
{
    _renderContext.ContentManager = Game.ContentManager;
    _renderContext.GraphicsDevice = Game.GraphicsDevice;
    _renderContext.Scene3D = Game.Scene3D;
    _renderContext.Scene2D = Game.Scene2D;
    _renderContext.RenderTarget = Game.Panel.Framebuffer;
    _renderContext.GameTime = gameTime;
    
    RenderPipeline.Execute(_renderContext);
}
```

**Achado Crítico**: RenderContext é **reusado e reconstruído por frame** (não alocado novo).

**Overhead**: Mínimo (8 assignments de referência).

---

## PARTE 2: RENDER PIPELINE - DEEP DIVE

### 2.1 CommandList Lifecycle Détaillé

**Arquivo**: [src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs) linhas 55-70

```csharp
public RenderPipeline(IGame game)
{
    var graphicsDevice = game.GraphicsDevice;
    
    // CRÍTICO: CommandList criado UMA VEZ
    _commandList = AddDisposable(graphicsDevice.ResourceFactory.CreateCommandList());
    
    // ... Outros components ...
}

public void Execute(RenderContext context)
{
    // ... Prepare framebuffers ...
    
    _commandList.Begin();        // Começa recording
    
    // --- RENDER COMMANDS ---
    if (context.Scene3D != null)
    {
        _commandList.PushDebugGroup("3D Scene");
        Render3DScene(_commandList, context.Scene3D, context);
        _commandList.PopDebugGroup();
    }
    
    // --- 2D/GUI RENDERING ---
    {
        _commandList.PushDebugGroup("2D Scene");
        DrawingContext.Begin(_commandList, ...);
        context.Scene3D?.Render(DrawingContext);
        context.Scene2D?.Render(DrawingContext);
        DrawingContext.End();
        _commandList.PopDebugGroup();
    }
    
    _commandList.End();          // Completa recording
    context.GraphicsDevice.SubmitCommands(_commandList);  // Submete ao GPU
}
```

**Sequência Frame**:
1. `_commandList.Begin()` - Limpa state anterior, abre para recording
2. Múltiplos `PushDebugGroup()` / `PopDebugGroup()` - Debug markers (Profilers)
3. Comandos renderização acumulam na CommandList interna
4. `_commandList.End()` - Finaliza recording, torna executável
5. `SubmitCommands()` - Envia ao GPU para execução

**Performance Implication**: Veldrid defrags CommandList automaticamente entre frames.

### 2.2 Render Passes - Order Crítica

**Shadow Pass** (Linhas 142-162):
```csharp
commandList.PushDebugGroup("Shadow pass");

_shadowMapRenderer.RenderShadowMap(
    scene,
    context.GraphicsDevice,
    commandList,
    (framebuffer, lightBoundingFrustum) =>
    {
        commandList.SetFramebuffer(framebuffer);
        commandList.ClearDepthStencil(1);
        commandList.SetFullViewports();
        
        // Update shadow view-projection matrix
        var shadowViewProjection = lightBoundingFrustum.Matrix;
        _globalShaderResourceData.UpdateGlobalConstantBuffers(
            commandList, context, shadowViewProjection, null, null);
        
        // Render shadow-casting objects
        DoRenderPass(context, commandList, _renderList.Shadow, 
                    lightBoundingFrustum, null);
    });

commandList.PopDebugGroup();
```

**Forward Pass** (Linhas 167-200):
```csharp
commandList.PushDebugGroup("Forward pass");

// Setup camera frustum culling
var standardPassCameraFrustum = scene.Camera.BoundingFrustum;

commandList.PushDebugGroup("Opaque");
RenderedObjectsOpaque += DoRenderPass(
    context, commandList, _renderList.Opaque, 
    standardPassCameraFrustum, forwardPassResourceSet);
commandList.PopDebugGroup();

commandList.PushDebugGroup("Transparent");
RenderedObjectsTransparent = DoRenderPass(
    context, commandList, _renderList.Transparent, 
    standardPassCameraFrustum, forwardPassResourceSet);
commandList.PopDebugGroup();
```

**Water Pass** (Linhas 212-218):
```csharp
commandList.PushDebugGroup("Water");
DoRenderPass(context, commandList, _renderList.Water, 
            standardPassCameraFrustum, forwardPassResourceSet);
commandList.PopDebugGroup();
```

**Order Implicado**:
1. Shadow mapping (depth from light perspective)
2. Opaque geometry (solid objects)
3. Transparent geometry (glass, particles - back-to-front)
4. Water (uses reflection/refraction from opaque pass)

**Achado**: Reflections/Refractions são calculadas DENTRO Forward Pass (dynamic, cara).

### 2.3 Framebuffer Intermediário - Por quê?

**Código** (Linhas 107-128):
```csharp
private void EnsureIntermediateFramebuffer(GraphicsDevice graphicsDevice, Framebuffer target)
{
    // Recreate se dimensions mudarem
    if (_intermediateDepthBuffer != null && 
        _intermediateDepthBuffer.Width == target.Width && 
        _intermediateDepthBuffer.Height == target.Height)
    {
        return;
    }
    
    // Cleanup antiga
    RemoveAndDispose(ref _intermediateDepthBuffer);
    RemoveAndDispose(ref _intermediateTexture);
    RemoveAndDispose(ref _intermediateFramebuffer);
    
    // Create nova com matching dimensions
    _intermediateDepthBuffer = AddDisposable(
        graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                target.Width, target.Height, 1, 1,
                PixelFormat.D24_UNorm_S8_UInt,     // Depth-Stencil format
                TextureUsage.DepthStencil)));
    
    _intermediateTexture = AddDisposable(
        graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(
                target.Width, target.Height, 1, 1,
                target.ColorTargets[0].Target.Format,  // Match color format
                TextureUsage.RenderTarget | TextureUsage.Sampled)));
    
    _intermediateFramebuffer = AddDisposable(
        graphicsDevice.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(_intermediateDepthBuffer, _intermediateTexture)));
}
```

**Razão para Existir**:
1. **Format Mismatch**: Target framebuffer pode ter formato diferente (ex: SRGB)
2. **Post-processing**: Intermediate texture pode ser amostrada para efeitos
3. **Multisampling**: Intermediate pode ter MSAA enquanto target não
4. **Profundidade**: Intermediate precisa de depth stencil, target pode não ter

**Observation**: TextureCopier copia intermediário → target ao final.

---

## PARTE 3: RESOURCE POOL - ANÁLISE PROFUNDA

### 3.1 Generational Validation Mechanism

**Arquivo**: [src/OpenSage.Graphics/Pooling/ResourcePool.cs](src/OpenSage.Graphics/Pooling/ResourcePool.cs)

**Índices vs Generations**:
```csharp
private T[] _resources;        // Actual objects
private uint[] _generations;   // Per-index generation counter
private Queue<uint> _freeSlots; // Índices reutilizáveis

public PoolHandle Allocate(T resource)
{
    uint idx;
    
    // Reuse freed slot
    if (_freeSlots.TryDequeue(out idx))
    {
        _resources[idx] = resource;
        _generations[idx]++;           // ← CRITICAL: Increment on reuse
        return new PoolHandle(idx, _generations[idx]);
    }
    
    // Allocate new slot
    if (_nextId >= _resources.Length)
        GrowCapacity();
    
    idx = _nextId++;
    _resources[idx] = resource;
    _generations[idx] = 1;      // First generation = 1
    return new PoolHandle(idx, 1);
}
```

**Scenario - Use After Free Detection**:
```csharp
// T=0: Allocate buffer at index 5
var handle1 = pool.Allocate(buffer1);  // Returns PoolHandle(5, 1)

// T=1: Release same buffer
pool.Release(handle1);                  // _generations[5] = 2, 5 added to free queue

// T=2: Allocate different buffer, reuses index 5
var handle2 = pool.Allocate(buffer2);  // Returns PoolHandle(5, 2)

// T=3: Try to use old handle
bool valid = pool.TryGet(handle1, out var buf);  
// _generations[5] = 2, handle1.Generation = 1 → MISMATCH → returns false ✅
```

**Achado**: Overflow de generation não é tratado explicitamente (uint.MaxValue wraps).

**Risk**: Se resource alocado/dealocado 2^32 vezes no mesmo índice, collision possível.

**Mitigation**: Typical usage: < 1M allocations/sec, wraps em 4000 segundos.

### 3.2 Initial Capacity Strategy

**Código**:
```csharp
public VeldridGraphicsDevice(VeldridLib.GraphicsDevice device)
{
    _bufferPool = new ResourcePool<VeldridLib.DeviceBuffer>(256);
    _texturePool = new ResourcePool<VeldridLib.Texture>(128);
    _samplerPool = new ResourcePool<VeldridLib.Sampler>(64);
    _framebufferPool = new ResourcePool<VeldridLib.Framebuffer>(32);
}
```

**Rationale**:
| Pool | Initial | Reasoning |
|------|---------|-----------|
| Buffers | 256 | VB+IB per model + UBOs + staging buffers |
| Textures | 128 | Diffuse, normal, specular, roughness, metallic per material × N |
| Samplers | 64 | Point/Linear/Aniso × Wrap/Clamp/Mirror (9 common combinations) |
| Framebuffers | 32 | Shadow (1) + Water reflection (1) + Water refraction (1) + Custom (N) |

**Growth Strategy**: `newCapacity = _resources.Length * 2` (doubling).

### 3.3 Integration em VeldridGraphicsDevice

**Buffer Creation Flow**:
```
CreateBuffer() public
  ↓
Creates VeldridLib.DeviceBuffer via ResourceFactory
  ↓
_bufferPool.Allocate(buf) 
  ↓
Returns PoolHandle(index, generation)
  ↓
Converts to Handle<IBuffer>(index, generation)  ← Client-facing opaque handle
  ↓
Client stores Handle<IBuffer> in material/mesh
```

**Destruction Flow**:
```
DestroyBuffer(Handle<IBuffer> buffer) public
  ↓
Reconstructs PoolHandle from Handle components
  ↓
_bufferPool.Release(poolHandle)
  ↓
Disposes VeldridLib.DeviceBuffer
  ↓
Frees index to _freeSlots queue
  ↓
Generation incremented
```

**Achado Crítico**: Handle<T> e PoolHandle encapsulam o mesmo (index, generation) mas em tipos diferentes!

```csharp
// In VeldridGraphicsDevice
var poolHandle = _bufferPool.Allocate(buf);
return new Handle<IBuffer>(poolHandle.Index, poolHandle.Generation);
//     ↑ Different struct, same data
```

**Implicação**: Client never sees PoolHandle, só Handle<T>. PoolHandle é internal.

---

## PARTE 4: SHADER SYSTEM - DETAILED ARCHITECTURE

### 4.1 SPIR-V Data Flow

**Compilation Phase** (Offline, MSBuild):
```
GLSL Source (.glsl)
    ↓ glslangValidator
SPIR-V Binary (.spv)
    ↓ Embed as resource
OpenSage.Game.dll
```

**Runtime Phase** (Online, Veldrid):
```
SPIR-V Binary (ReadOnlyMemory<byte>)
    ↓ ShaderSource(Stage, SpirVBytes, EntryPoint, Specializations)
    ↓ ShaderCompilationCache.GetOrCompile()
    ↓ device.CreateShaderProgram() [PLACEHOLDER]
    ↓ Veldrid.SPIRV cross-compiler
    ↓ Veldrid.Shader (MSL/GLSL/HLSL/SPIRV)
    ↓ GPU device
```

### 4.2 ShaderSource - Estrutura Detalhada

**Arquivo**: [src/OpenSage.Graphics/Resources/ShaderSource.cs](src/OpenSage.Graphics/Resources/ShaderSource.cs)

**Validação em Construtor**:
```csharp
public ShaderSource(
    ShaderStages stage,
    ReadOnlyMemory<byte> spirvBytes,
    string entryPoint = "main",
    IReadOnlyList<SpecializationConstant>? specializations = null)
{
    if (stage == ShaderStages.None)
        throw new ArgumentException("Shader stage cannot be None.", nameof(stage));
    
    if (spirvBytes.IsEmpty)
        throw new ArgumentException("SPIR-V bytecode cannot be empty.", nameof(spirvBytes));
    
    if (string.IsNullOrWhiteSpace(entryPoint))
        throw new ArgumentException("Entry point cannot be null or empty.", nameof(entryPoint));
    
    // ...
}
```

**Especialização Constant Example**:
```csharp
// GLSL source (em arquivos):
// #extension GL_EXT_specialization_constant : enable
// layout(constant_id = 0) const bool ENABLE_LIGHTMAP = true;
// layout(constant_id = 1) const int MAX_LIGHTS = 8;

var specs = new[]
{
    new SpecializationConstant(0, true),   // ENABLE_LIGHTMAP
    new SpecializationConstant(1, 8u)      // MAX_LIGHTS
};

var source = new ShaderSource(
    ShaderStages.Fragment,
    spirvData,
    "main",
    specs
);
```

**Achado**: Specialization constants permitem **zero-cost variants** (compiladas em build time, não runtime).

### 4.3 ShaderCompilationCache - Hash Strategy

**Arquivo**: [src/OpenSage.Graphics/Shaders/ShaderCompilationCache.cs](src/OpenSage.Graphics/Shaders/ShaderCompilationCache.cs)

**Key Generation**:
```csharp
private readonly struct ShaderSourceKey : IEquatable<ShaderSourceKey>
{
    private readonly ShaderStages _stage;
    private readonly string _entryPoint;
    private readonly int _bytesHash;
    private readonly int _specializationHash;
    
    public ShaderSourceKey(ShaderSource source)
    {
        _stage = source.Stage;
        _entryPoint = source.EntryPoint;
        _bytesHash = ComputeMemoryHash(source.SpirVBytes.Span);
        _specializationHash = ComputeSpecializationHash(source.Specializations);
    }
    
    private static int ComputeMemoryHash(ReadOnlySpan<byte> data)
    {
        unchecked
        {
            int hash = 17;
            for (int i = 0; i < data.Length; i++)
            {
                hash = hash * 31 + data[i].GetHashCode();
                i += 255;  // ← Sample every 256th byte (performance optimization)
            }
            return hash;
        }
    }
}
```

**Problema Potencial**: Hash com sampled bytes pode causar **collision**.

**Mitigação**: Secundário GetHashCode() em especializations previne maioria dos falsos positivos.

**Achado**: Cache é **shallow** (não valida bytecode integrity via checksum completo).

---

## PARTE 5: STATE OBJECTS - IMMUTABILITY PATTERNS

### 5.1 Preset Pattern Implementado

**Arquivo**: [src/OpenSage.Graphics/State/StateObjects.cs](src/OpenSage.Graphics/State/StateObjects.cs)

**RasterState Presets** (Linhas 100-107):
```csharp
public static RasterState Solid => new(
    FillMode.Solid,
    CullMode.Back,
    FrontFace.CounterClockwise,
    false,
    false);

public static RasterState Wireframe => new(
    FillMode.Wireframe,
    CullMode.None,
    FrontFace.CounterClockwise,
    false,
    false);

public static RasterState NoCull => new(
    FillMode.Solid,
    CullMode.None,
    FrontFace.CounterClockwise,
    false,
    false);
```

**DepthState Presets** (Linhas 226-239):
```csharp
public static DepthState Disabled => new(false, false, CompareFunction.Always);
public static DepthState Default => new(true, true, CompareFunction.Less);
public static DepthState ReadOnly => new(true, false, CompareFunction.Less);
```

**BlendState Presets** (10+ combinations):
```csharp
public static BlendState Opaque { get; } = new()
{
    Enabled = false,
    // ...
};

public static BlendState AlphaBlend { get; } = new()
{
    Enabled = true,
    SourceColorFactor = BlendFactor.SourceAlpha,
    DestinationColorFactor = BlendFactor.InverseSourceAlpha,
    ColorOperation = BlendOperation.Add,
    // ...
};
```

**Achado**: Presets são cached como static properties (instância única per application lifetime).

### 5.2 Equality Implementation

**RasterState.Equals()** (Linhas 130-137):
```csharp
public bool Equals(RasterState other)
{
    return FillMode == other.FillMode &&
           CullMode == other.CullMode &&
           FrontFace == other.FrontFace &&
           DepthClamp == other.DepthClamp &&
           ScissorTest == other.ScissorTest;
}

public override int GetHashCode()
{
    return HashCode.Combine(FillMode, CullMode, FrontFace, DepthClamp, ScissorTest);
}
```

**Pattern Aplicado**:
1. `IEquatable<T>` para comparação type-safe
2. `override Equals(object)` para compatibilidade
3. `GetHashCode()` usando `HashCode.Combine()`
4. Operadores `==` e `!=` overload

**Implicação**: State objects podem ser usados como Dictionary/HashSet keys!

---

## PARTE 6: HANDLE SYSTEM - USE-AFTER-FREE PREVENTION

### 6.1 Validation Pipeline

**Arquivo**: [src/OpenSage.Graphics/Abstractions/GraphicsHandles.cs](src/OpenSage.Graphics/Abstractions/GraphicsHandles.cs)

**Handle<T> Validation Methods**:
```csharp
public void ValidateOrThrow(IGraphicsResource resource)
{
    if (_id != resource.Id || _generation != resource.Generation)
    {
        throw new GraphicsException(
            $"Handle is invalid. Resource has been disposed or reallocated. " +
            $"Expected ID: {_id}, Generation: {_generation}; " +
            $"Current ID: {resource.Id}, Generation: {resource.Generation}");
    }
}

public bool IsValidFor(IGraphicsResource resource)
{
    return IsValid && _id == resource.Id && _generation == resource.Generation;
}
```

**Usage Pattern**:
```csharp
// Client code
var buffer = device.CreateBuffer(desc);  // Returns Handle<IBuffer>

// Later...
device.DestroyBuffer(buffer);

// Much later, buggy code tries to use:
var bufferObj = device.GetBuffer(buffer);  // ← Should validate handle

// Inside GetBuffer():
buffer.ValidateOrThrow(_currentResource);  // ← Throws if stale!
```

**Achado**: Validação é **explicit** (não automática). Requer CheckPoint intencional.

### 6.2 HandleAllocator - Central Registry

**Uso em VeldridGraphicsDevice**:
```csharp
// Não implementado ainda, mas framework pronto
private HandleAllocator<IBuffer> _bufferAllocator = new();

public Handle<IBuffer> CreateBuffer(...)
{
    var veldridBuf = _device.ResourceFactory.CreateBuffer(...);
    var handle = _bufferAllocator.Allocate();  // Get next handle
    _buffers[handle.Id] = veldridBuf;
    return handle;
}

public void DestroyBuffer(Handle<IBuffer> handle)
{
    if (_buffers.TryGetValue(handle.Id, out var buf))
    {
        buf.Dispose();
        _bufferAllocator.InvalidateId(handle.Id);  // Increment generation
    }
}
```

---

## PARTE 7: GAPS ESPECÍFICOS CÓDIGO

### 7.1 Missing Implementations (Placeholders)

**VeldridGraphicsDevice.cs Linhas 225-291**:

#### CreateFramebuffer (Linhas 225-237)
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

**Status**: Hardcoded swapchain. Não suporta múltiplos render targets.

#### CreateShader (Linhas 253-259)
```csharp
public Handle<IShaderProgram> CreateShader(string name, ReadOnlySpan<byte> spirvData, string entryPoint = "main")
{
    // Placeholder - Week 9 will implement shader compilation
    uint id = _nextResourceId++;
    _shaders[id] = null;
    return new Handle<IShaderProgram>(id, 1);
}
```

**Status**: Totalmente não-funcional. Retorna handle inválido.

#### CreatePipeline (Linhas 282-289)
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

**Status**: Skeleton. Não mapeia state objects → Veldrid.Pipeline.

### 7.2 Missing Bindings (Placeholders)

**VeldridGraphicsDevice.cs Linhas 338-388**:

#### SetRenderTarget (Linhas 329-339)
```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    if (framebuffer.IsValid && _framebuffers.TryGetValue(framebuffer.Id, out var obj) && obj is VeldridLib.Framebuffer fb)
    {
        _currentFramebuffer = fb;
        _cmdList.SetFramebuffer(fb);
    }
    else
    {
        _currentFramebuffer = _device.SwapchainFramebuffer;
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
    }
}
```

**Issue**: `_framebuffers` dict não existe no código atual! (VeldridGraphicsDevice.cs)

#### BindVertexBuffer, BindIndexBuffer, BindUniformBuffer (Linhas 353-368)
```csharp
public void BindVertexBuffer(Handle<IBuffer> buffer, uint offset = 0)
{
    // Placeholder - Week 9 will implement
}

public void BindIndexBuffer(Handle<IBuffer> buffer, uint offset = 0)
{
    // Placeholder - Week 9 will implement
}

public void BindUniformBuffer(Handle<IBuffer> buffer, uint slot)
{
    // Placeholder - Week 9 will implement
}
```

**Status**: Sem implementação. Crítico para resource binding.

#### BindTexture (Linhas 373-376)
```csharp
public void BindTexture(Handle<ITexture> texture, uint slot, Handle<ISampler> sampler)
{
    // Placeholder - Week 9 will implement
}
```

**Status**: ResourceSet binding não começado.

---

## PARTE 8: INTEGRATION POINTS - MISSING LINKS

### 8.1 IGraphicsDevice → RenderPipeline

**Current Relationship**:
```
Game.GraphicsDevice (exists, Veldrid-specific)
         ↓
RenderPipeline uses directly via ResourceFactory
         ↓
CommandList, Textures, Framebuffers all Veldrid types
```

**Planned (Phase 3 Week 9)**:
```
Game.GraphicsDevice (IGraphicsDevice abstraction)
         ↓
VeldridGraphicsDevice (implements IGraphicsDevice)
         ↓
RenderPipeline uses abstraction interface
         ↓
Agnostic to backend (Veldrid vs BGFX)
```

**Gap**: RenderPipeline ainda acessa `graphicsDevice.ResourceFactory` diretamente (linha 55 RenderPipeline.cs).

**Fix Required**:
```csharp
// Current (tightly coupled)
_commandList = AddDisposable(graphicsDevice.ResourceFactory.CreateCommandList());

// Future (abstracted)
_commandList = AddDisposable(graphicsDevice.BeginFrame());  // Returns command recorder
```

### 8.2 ShaderCompilationCache ↔ VeldridGraphicsDevice

**Current Status**: Desconectados.

**Integration Point Faltando**:
```csharp
// In VeldridGraphicsDevice constructor
private readonly ShaderCompilationCache _shaderCache = new();

public Handle<IShaderProgram> CreateShader(string name, ReadOnlySpan<byte> spirvData, string entryPoint = "main")
{
    // MISSING: Implementação
    var source = new ShaderSource(
        ShaderStages.??? (not specified in current signature),
        spirvData,
        entryPoint);
    
    var compiled = _shaderCache.GetOrCompile(this, source);
    
    // Store in pool
    var poolHandle = _shaderPool.Allocate(compiled);
    return new Handle<IShaderProgram>(poolHandle.Index, poolHandle.Generation);
}
```

**Issue**: CreateShader signature incompleta (não especifica stage).

---

## PARTE 9: RECOMMENDATIONS FOR WEEK 9

### 9.1 Priority 1: Shader Compilation (Crítico)

**Arquivo a Modificar**: [src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs](src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs) linhas 253-259

**Implementação**:
```csharp
public Handle<IShaderProgram> CreateShaderProgram(ShaderSource source)
{
    if (source.Stage == ShaderStages.None), pre        throw new ArgumentException("Shader stage cannot be None.");
    
    try
    {
        // Veldrid.SPIRV cross-compilation
        var shader = _device.ResourceFactory.CreateFromSpirv(
            new ShaderDescription(
                source.Stage.ToVeldridShaderStages(),
                source.SpirVBytes,
                source.EntryPoint));
        
        // Pool allocation
        var poolHandle = _shaderPool.Allocate(shader);
        return new Handle<IShaderProgram>(poolHandle.Index, poolHandle.Generation);
    }
    catch (Exception ex)
    {
        throw new GraphicsException($"Failed to compile shader {source.EntryPoint}", ex);
    }
}
```

**Helper Needed**:
```csharp
private static VeldridLib.ShaderStages ToVeldridShaderStages(this ShaderStages stages)
    => stages switch
    {
        ShaderStages.Vertex => VeldridLib.ShaderStages.Vertex,
        ShaderStages.Fragment => VeldridLib.ShaderStages.Fragment,
        ShaderStages.Compute => VeldridLib.ShaderStages.Compute,
        _ => throw new ArgumentException($"Unsupported shader stage: {stages}")
    };
```

### 9.2 Priority 2: Pipeline Creation

**Arquivo a Modificar**: [src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs](src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs) linhas 282-289

**Implementação Sketch**:
```csharp
public Handle<IPipeline> CreatePipeline(
    Handle<IShaderProgram> vertexShader,
    Handle<IShaderProgram> fragmentShader,
    RasterState rasterState = default,
    DepthState depthState = default,
    BlendState blendState = default,
    StencilState stencilState = default)
{
    var vertShader = _shaderPool.TryGet(...) ?? throw;
    var fragShader = _shaderPool.TryGet(...) ?? throw;
    
    var pipelineDesc = new GraphicsPipelineDescription(
        blendStateDesc: ConvertBlendState(blendState),
        depthStencilStateDesc: ConvertDepthState(depthState),
        rasterizerState: ConvertRasterState(rasterState),
        primitiveTopology: PrimitiveTopology.TriangleList,
        resourceLayouts: ...,  // ← Need ResourceLayout array
        shaders: new[] { vertShader, fragShader },
        outputs: GameOutputDescription);
    
    var pipeline = _device.ResourceFactory.CreateGraphicsPipeline(ref pipelineDesc);
    
    var poolHandle = _pipelinePool.Allocate(pipeline);
    return new Handle<IPipeline>(poolHandle.Index, poolHandle.Generation);
}
```

---

## PART 10: CODE QUALITY ASSESSMENTS

### 10.1 Strengths ✅

1. **Type Safety**: Handle<T> previne implicit conversions
2. **Immutability**: State objects imutáveis previnem threading bugs
3. **Resource Management**: DisposableBase + AddDisposable pattern consistente
4. **Error Handling**: GraphicsException com messages detalhadas
5. **Validation**: Generational handles detectam use-after-free
6. **Documentation**: XML comments em classes público

### 10.2 Weaknesses ⚠️

1. **Incomplete Implementation**: 50%+ placeholders em VeldridGraphicsDevice
2. **Type Mismatch**: Handle<T> vs PoolHandle confusion
3. **Hash Collisions**: ShaderCompilationCache usa sampled hash (possível collision)
4. **Missing Integration**: ShaderCache não conectado ao device
5. **Hardcoded Values**: ResourcePool initial capacities sem justificativa
6. **No Validation Layer**: Nenhuma assertion/validation em hot paths

---

## CONCLUSÃO: GAPS RESUMIDO

| Componente | Gap | Severity | Fix Time |
|-----------|-----|----------|----------|
| Shader compilation | Placeholder | CRITICAL | 4 horas |
| Pipeline creation | Placeholder | CRITICAL | 4 horas |
| Framebuffer support | Hardcoded swapchain | HIGH | 2 horas |
| Texture binding | Not implemented | HIGH | 2 horas |
| Uniform binding | Not implemented | MEDIUM | 1 hora |

**Total Week 9 Effort**: 13 horas para criticais + MEDIUM.

