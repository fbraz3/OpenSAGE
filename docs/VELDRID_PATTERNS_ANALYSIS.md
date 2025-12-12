# Veldrid Patterns Analysis para OpenSAGE

**Análise técnica dos padrões implementáveis do Veldrid**
Data: 12 de dezembro de 2025

---

## 1. ResourceFactory & Two-Level Binding Pattern

### 1.1 Conceito Fundamental

Veldrid implementa separação entre **declaração de recursos** e **instâncias concretas**:

```
ResourceLayout (template) ──binds──> ResourceSet (concrete resources)
     ↓                                      ↓
  Define estrutura                   Instancia recursos
  (tipos, shader stages)             (buffers, texturas)
```

### 1.2 ResourceFactory Pattern

**Propósito**: Abstrair criação de recursos por backend

```csharp
public abstract class ResourceFactory
{
    protected ResourceFactory(GraphicsDeviceFeatures features)
    {
        Features = features;  // Backend capabilities
    }

    public abstract GraphicsBackend BackendType { get; }
    public GraphicsDeviceFeatures Features { get; }

    // Template Methods com validação em base
    public Pipeline CreateGraphicsPipeline(ref GraphicsPipelineDescription desc)
    {
        #if VALIDATE_USAGE
            // Validações que chamam Features
            if (!Features.FillModeWireframe && desc.RasterizerState.FillMode == PolygonFillMode.Wireframe)
                throw new VeldridException("FillMode.Wireframe não suportado");
        #endif

        return CreateGraphicsPipelineCore(ref desc);  // Backend específico
    }

    // Cada backend implementa
    protected abstract Pipeline CreateGraphicsPipelineCore(ref GraphicsPipelineDescription desc);
}
```

**Backend implementations**:
- `VkResourceFactory` (Vulkan)
- `D3D11ResourceFactory` (Direct3D 11)
- `MTLResourceFactory` (Metal)
- `OpenGLResourceFactory` (OpenGL)

### 1.3 Two-Level Binding em Ação

#### Nível 1: Definir Layout

```csharp
ResourceLayout layout = factory.CreateResourceLayout(
    new ResourceLayoutDescription(
        new ResourceLayoutElementDescription(
            "ViewMatrix",
            ResourceKind.UniformBuffer,
            ShaderStages.Vertex),
        new ResourceLayoutElementDescription(
            "TextureSampler",
            ResourceKind.TextureReadOnly,
            ShaderStages.Fragment),
        new ResourceLayoutElementDescription(
            "Sampler0",
            ResourceKind.Sampler,
            ShaderStages.Fragment)
    )
);
```

#### Nível 2: Criar ResourceSet

```csharp
// Mesma ordem do layout
ResourceSet resourceSet = factory.CreateResourceSet(
    new ResourceSetDescription(
        layout,
        viewMatrixBuffer,      // ResourceKind.UniformBuffer
        textureView,           // ResourceKind.TextureReadOnly
        sampler                // ResourceKind.Sampler
    )
);
```

#### Nível 3: Usar em CommandList

```csharp
commandList.Begin();
commandList.SetPipeline(pipeline);

// Pipeline contém ResourceLayout em posição 0
// ResourceSet deve ser compatível com esse layout
commandList.SetGraphicsResourceSet(0, resourceSet);

commandList.Draw(vertexCount);
commandList.End();
```

### 1.4 Dynamic Binding com Offsets

Para recursos marcados `DynamicBinding`, não recria ResourceSet:

```csharp
// Layout definition
var layoutDesc = new ResourceLayoutDescription(
    new ResourceLayoutElementDescription(
        "DynamicBuffer",
        ResourceKind.UniformBuffer,
        ShaderStages.Vertex,
        ResourceLayoutElementOptions.DynamicBinding  // ← Flag crítica
    )
);

// Bind durante execução com offset
uint[] offsets = { 256 };  // byte offset no buffer
commandList.SetGraphicsResourceSet(0, resourceSet, offsets);
```

### 1.5 Implementação em OpenSAGE

**Padrão existente que pode evoluir**:

```csharp
// ATUAL: GraphicsSystem expõe ResourceFactory
public class GraphicsSystem
{
    public ResourceFactory ResourceFactory { get; }
}

// PRÓXIMO: Estratégia de cache (NeoDemo pattern)
public static class RenderResourceCache
{
    private static Dictionary<GraphicsPipelineDescription, Pipeline> s_pipelines = new();
    private static Dictionary<ResourceLayoutDescription, ResourceLayout> s_layouts = new();

    public static Pipeline GetPipeline(
        ResourceFactory factory,
        ref GraphicsPipelineDescription desc)
    {
        if (!s_pipelines.TryGetValue(desc, out var pipeline))
        {
            pipeline = factory.CreateGraphicsPipeline(ref desc);
            s_pipelines[desc] = pipeline;
        }
        return pipeline;
    }
}
```

**Benefícios**:
- ✅ Evita recriação de pipelines caros
- ✅ Suporte implicit a múltiplos backends
- ✅ Validação centralizada via ResourceFactory

---

## 2. CommandList Model: Deferred Recording

### 2.1 Fases de Execução

```
[Recording Phase]          [Execution Phase]
Begin() ──commands──> End() ──SubmitCommands()──> GPU
  ↓                       ↓                          ↓
Store                   Finalize             Backend executa
commands            (Vulkan buffer,          com sincronização
in list            D3D11 deferred)           apropriada
```

### 2.2 Ciclo Completo

```csharp
// 1. Recording
CommandList cl = factory.CreateCommandList();
cl.Begin();

// Issue commands - NÃO executam ainda
cl.SetFramebuffer(framebuffer);
cl.SetPipeline(pipeline);
cl.SetGraphicsResourceSet(0, resourceSet);
cl.Draw(6);

cl.End();

// 2. Execution
graphicsDevice.SubmitCommands(cl);

// 3. Reuse
cl.Begin();  // Reset para nova gravação
// ...
```

### 2.3 Backend-Specific Implementations

#### Vulkan (`VkCommandList`)
```
Usa Vulkan natively:
  vkBeginCommandBuffer() ──Begin()
  vkCmdDrawIndexed()     ──Draw()
  vkEndCommandBuffer()   ──End()
  vkQueueSubmit()        ──SubmitCommands()
```

**Vantagem**: Mapping direto, máxima performance

#### Direct3D 11 (`D3D11CommandList`)
```
Deferred Context:
  ID3D11DeviceContext deferred ──Begin()
  Deferred commands                ──Draw()
  FinishCommandList()           ──End()
  ExecuteCommandList()          ──SubmitCommands()
```

**Vantagem**: Deferred context para paralelização (teórica)

#### OpenGL (`OpenGLCommandList`)
```
Custom command recording:
  OpenGLCommandEntryList ──Begin()
  Entry structs (Draw, SetPipeline) ──Commands()
  BlockingCollection    ──End()
  ExecutionThread       ──SubmitCommands()
                            (single-threaded)
```

**Key Point**: OpenGL não suporta true deferred, emula via thread executor

#### Metal (`MTLCommandList`)
```
MTLCommandBuffer:
  MTLCommandBuffer ──Begin()
  MTLRenderCommandEncoder ──Commands()
  commit()              ──End()
                        ──SubmitCommands()
```

### 2.4 Threading Constraints

⚠️ **CRÍTICO: CommandList NÃO é thread-safe**

```csharp
// ✅ CORRETO: Single thread per CommandList
Task.Run(() => {
    CommandList cl = factory.CreateCommandList();
    cl.Begin();
    // ... record
    cl.End();
    gd.SubmitCommands(cl);
});

// ❌ ERRADO: Múltiplas threads → undefined behavior
CommandList cl = factory.CreateCommandList();
Task.Run(() => cl.SetPipeline(pipe1));
Task.Run(() => cl.Draw(100));  // RACE CONDITION
```

**OpenGL Special Case**:
```csharp
// OpenGL tem ExecutionThread interno
class OpenGLGraphicsDevice
{
    private Thread _executionThread;
    private BlockingCollection<WorkItem> _workItems;

    // Application thread → enqueue work
    // ExecutionThread → executa OpenGL calls
    // Sincronização via ManualResetEventSlim
}
```

### 2.5 Aplicação em OpenSAGE

**Padrão atual** (Game.cs):
```csharp
public void Update()  // Main thread, 60Hz
{
    // Record commands
    _commandList.Begin();
    // ... rendering
    _commandList.End();

    // Submit
    GraphicsDevice.SubmitCommands(_commandList);
}
```

**Melhorias possíveis**:
1. **Command recording paralelization** (futuro):
   ```csharp
   var perThreadLists = new CommandList[numThreads];
   Parallel.For(0, numThreads, i => {
       var cl = factory.CreateCommandList();
       RecordTerrainCommands(cl, terrainTiles[i]);  // Thread-local
       perThreadLists[i] = cl;
   });
   
   foreach (var cl in perThreadLists)
       gd.SubmitCommands(cl);
   ```

2. **Secondary command lists** (batch recording):
   ```csharp
   var primaryList = factory.CreateCommandList();
   var shadowCasters = factory.CreateCommandList();
   
   primaryList.Begin();
   shadowCasters.Begin();
   
   foreach (var obj in renderQueue)
   {
       if (obj.CastsShadow)
           RecordDrawCall(shadowCasters, obj);
       RecordDrawCall(primaryList, obj);
   }
   
   primaryList.ExecuteCommandList(shadowCasters);
   primaryList.End();
   ```

---

## 3. Pipeline Caching & NeoDemo Patterns

### 3.1 Pipeline Creation Cost

Pipelines são **MUITO caros** para criar:

- Validação de estado
- Compilation de shaders
- Backend-specific optimization
- State object creation (D3D11, OpenGL)

**Solução**: Não recriar para mesma descrição

### 3.2 StaticResourceCache Pattern

NeoDemo implementa cache simples:

```csharp
internal static class StaticResourceCache
{
    // Static dictionaries por tipo
    private static Dictionary<GraphicsPipelineDescription, Pipeline> s_pipelines = new();
    private static Dictionary<ResourceLayoutDescription, ResourceLayout> s_layouts = new();
    private static Dictionary<(string name, Shader vs, Shader fs), Pipeline> s_shaderPipelines = new();

    public static Pipeline GetPipeline(
        ResourceFactory factory,
        ref GraphicsPipelineDescription desc)
    {
        // Check cache
        if (s_pipelines.TryGetValue(desc, out var pipeline))
            return pipeline;

        // Create and cache
        pipeline = factory.CreateGraphicsPipeline(ref desc);
        s_pipelines.Add(desc, pipeline);
        return pipeline;
    }

    public static ResourceLayout GetResourceLayout(
        ResourceFactory factory,
        ResourceLayoutDescription desc)
    {
        if (s_layouts.TryGetValue(desc, out var layout))
            return layout;

        layout = factory.CreateResourceLayout(ref desc);
        s_layouts.Add(desc, layout);
        return layout;
    }

    public static void DestroyAllDeviceObjects()
    {
        // Cleanup on device change
        foreach (var pipe in s_pipelines.Values)
            pipe.Dispose();
        s_pipelines.Clear();

        foreach (var layout in s_layouts.Values)
            layout.Dispose();
        s_layouts.Clear();
    }
}
```

### 3.3 Backend-Specific Caching

D3D11 também caches internos (estado backend):

```csharp
internal class D3D11ResourceFactory : ResourceFactory
{
    private readonly D3D11ResourceCache _cache;

    protected override Pipeline CreateGraphicsPipelineCore(ref GraphicsPipelineDescription desc)
    {
        // D3D11ResourceCache caches:
        // - ID3D11BlendState
        // - ID3D11DepthStencilState
        // - ID3D11RasterizerState
        // - ID3D11InputLayout
        return new D3D11Pipeline(_cache, ref desc);
    }
}
```

### 3.4 Invalidation Strategy

Quando backend muda (janela redimensionada, device lost):

```csharp
// Window resized
RecreateWindowSizedResources()
{
    // Cleanup NeoDemo caches
    StaticResourceCache.DestroyAllDeviceObjects();

    // Recreate resources
    MainSceneFramebuffer = factory.CreateFramebuffer(...);
    // ...
}
```

### 3.5 Implementação em OpenSAGE

**Padrão sugerido**:

```csharp
// src/OpenSage.Graphics/RenderResourceCache.cs
public class RenderResourceCache
{
    private readonly GraphicsDevice _gd;
    private readonly Dictionary<GraphicsPipelineDescription, Pipeline> _pipelines = new();
    private readonly Dictionary<ResourceLayoutDescription, ResourceLayout> _layouts = new();

    public RenderResourceCache(GraphicsDevice gd)
    {
        _gd = gd;
    }

    public Pipeline GetPipeline(ref GraphicsPipelineDescription desc)
    {
        if (_pipelines.TryGetValue(desc, out var pipeline))
            return pipeline;

        pipeline = _gd.ResourceFactory.CreateGraphicsPipeline(ref desc);
        _pipelines[desc] = pipeline;
        return pipeline;
    }

    public ResourceLayout GetResourceLayout(ref ResourceLayoutDescription desc)
    {
        if (_layouts.TryGetValue(desc, out var layout))
            return layout;

        layout = _gd.ResourceFactory.CreateResourceLayout(ref desc);
        _layouts[desc] = layout;
        return layout;
    }

    public void Clear()
    {
        foreach (var pipe in _pipelines.Values)
            pipe?.Dispose();
        _pipelines.Clear();

        foreach (var layout in _layouts.Values)
            layout?.Dispose();
        _layouts.Clear();
    }

    public void Dispose()
    {
        Clear();
    }
}

// Em GraphicsSystem
public class GraphicsSystem : IDisposable
{
    public RenderResourceCache ResourceCache { get; private set; }

    public void Initialize(GraphicsDevice gd)
    {
        ResourceCache = new RenderResourceCache(gd);
    }

    public void OnWindowSized()
    {
        ResourceCache.Clear();
        RecreateFramebuffers();
    }

    public void Dispose()
    {
        ResourceCache?.Dispose();
    }
}
```

### 3.6 ⚠️ Thread Safety

`StaticResourceCache` é **explicitly non-thread-safe**:

```csharp
// ✅ SEGURO: Usar em main render thread
class RenderSystem
{
    private RenderResourceCache _cache = new();

    public void Render(CommandList cl)
    {
        var pipeDesc = new GraphicsPipelineDescription(...);
        var pipe = _cache.GetPipeline(ref pipeDesc);  // Main thread only
        cl.SetPipeline(pipe);
    }
}

// ❌ INSEGURO: Se múltiplas threads acessarem
Parallel.For(0, objects.Length, i => {
    var pipe = _cache.GetPipeline(...);  // RACE CONDITION
});
```

**Solução para paralelização**: Thread-local caches ou sincronização

---

## 4. Framebuffer Model & Attachments

### 4.1 Estrutura

```csharp
public class Framebuffer
{
    public FramebufferAttachment DepthTarget { get; }
    public FramebufferAttachment[] ColorTargets { get; }

    public uint Width { get; }  // Do primeiro ColorTarget ou DepthTarget
    public uint Height { get; }
}

public struct FramebufferAttachment
{
    public Texture Target { get; }
    public uint ArrayLayer { get; }  // Para texturas array
    public uint MipLevel { get; }    // Para mipmapped textures
}
```

### 4.2 Criação

```csharp
// Define targets
Texture colorTexture = factory.CreateTexture(new TextureDescription
{
    Width = 800, Height = 600,
    Format = PixelFormat.R8_G8_B8_A8_UNorm,
    Usage = TextureUsage.RenderTarget,
    Type = TextureType.Texture2D
});

Texture depthTexture = factory.CreateTexture(new TextureDescription
{
    Width = 800, Height = 600,
    Format = PixelFormat.D32_Float,
    Usage = TextureUsage.DepthStencil,
    Type = TextureType.Texture2D
});

// Cria framebuffer
Framebuffer fb = factory.CreateFramebuffer(new FramebufferDescription
{
    DepthTarget = new FramebufferAttachmentDescription(depthTexture, 0, 0),
    ColorTargets = new[]
    {
        new FramebufferAttachmentDescription(colorTexture, 0, 0)
    }
);
```

### 4.3 Backend-Specific Implementations

#### Vulkan (`VkFramebuffer`)

Cria múltiplos `VkRenderPass` para diferentes cenários:

```csharp
internal class VkFramebuffer : Framebuffer
{
    private VkRenderPass _renderPassNoClear;      // Load: DontCare
    private VkRenderPass _renderPassNoClearLoad;  // Load: Load
    private VkRenderPass _renderPassClear;        // Load: Clear

    private VkFramebuffer _framebuffer;
    private VkImageView[] _attachments;

    // Layout transitions
    private void TransitionToIntermediateLayout() { }
    private void TransitionToFinalLayout() { }
}
```

**Por que múltiplos render passes?**
- Vulkan requer RenderPass compatível com load/store ops
- Otimização: Diferentes cenários requerem diferentes passes
- Load pass para reuso de target, ClearPass para limpeza

#### Metal (`MTLFramebuffer`)

```csharp
internal class MTLFramebuffer : Framebuffer
{
    private MTLRenderPassDescriptor _renderPassDescriptor;

    public MTLFramebuffer(MTLGraphicsDevice gd, ref FramebufferDescription description)
    {
        _renderPassDescriptor = MTLRenderPassDescriptor.Create();

        // Configure color attachments
        for (int i = 0; i < description.ColorTargets.Length; i++)
        {
            var att = description.ColorTargets[i];
            var colorAtt = _renderPassDescriptor.ColorAttachments[i];
            colorAtt.Texture = ((MTLTexture)att.Target).MTLTexture;
            colorAtt.LoadAction = MTLLoadAction.Clear;
            colorAtt.StoreAction = MTLStoreAction.Store;
            colorAtt.Slice = att.ArrayLayer;
            colorAtt.Level = att.MipLevel;
        }

        // Depth
        if (description.DepthTarget != null)
        {
            var att = description.DepthTarget.Value;
            _renderPassDescriptor.DepthAttachment.Texture = ((MTLTexture)att.Target).MTLTexture;
            _renderPassDescriptor.DepthAttachment.LoadAction = MTLLoadAction.Clear;
            _renderPassDescriptor.DepthAttachment.StoreAction = MTLStoreAction.Store;
        }
    }
}
```

#### OpenGL (`OpenGLFramebuffer`)

```csharp
internal class OpenGLFramebuffer : Framebuffer
{
    private GLHandle _framebuffer;

    public OpenGLFramebuffer(ref FramebufferDescription desc)
    {
        _framebuffer = glGenFramebuffer();
        glBindFramebuffer(GLFramebufferTarget.Framebuffer, _framebuffer);

        // Attach color targets
        for (int i = 0; i < desc.ColorTargets.Length; i++)
        {
            var att = desc.ColorTargets[i];
            var tex = ((OpenGLTexture)att.Target);
            glFramebufferTexture2D(
                GLFramebufferTarget.Framebuffer,
                GLFramebufferAttachment.ColorAttachment0 + i,
                GLTextureTarget.Texture2D,
                tex.Handle,
                (int)att.MipLevel);
        }

        // Attach depth
        if (desc.DepthTarget != null)
        {
            var att = desc.DepthTarget.Value;
            var tex = ((OpenGLTexture)att.Target);
            glFramebufferTexture2D(
                GLFramebufferTarget.Framebuffer,
                GLFramebufferAttachment.DepthAttachment,
                GLTextureTarget.Texture2D,
                tex.Handle,
                (int)att.MipLevel);
        }
    }
}
```

### 4.4 Special Case: Swapchain Framebuffer

```csharp
// Para renderizar direto ao display
public class OpenGLSwapchainFramebuffer : Framebuffer
{
    // Usa textures placeholder que representam swapchain color/depth
    // Permite mesmo API path para backbuffer e offscreen
}
```

### 4.5 Implementação em OpenSAGE

**Padrão existente**:

```csharp
// Scene3D.cs
public class Scene3D
{
    public Framebuffer Framebuffer { get; private set; }

    public void RecreateFramebuffer(GraphicsDevice gd)
    {
        var colorTex = gd.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = (uint)gd.MainSwapchain.Framebuffer.Width,
            Height = (uint)gd.MainSwapchain.Framebuffer.Height,
            Format = PixelFormat.R8_G8_B8_A8_UNorm,
            Usage = TextureUsage.RenderTarget
        });

        var depthTex = gd.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = (uint)gd.MainSwapchain.Framebuffer.Width,
            Height = (uint)gd.MainSwapchain.Framebuffer.Height,
            Format = PixelFormat.D24_UNorm_S8_UInt,
            Usage = TextureUsage.DepthStencil
        });

        Framebuffer = gd.ResourceFactory.CreateFramebuffer(new FramebufferDescription
        {
            DepthTarget = new FramebufferAttachmentDescription(depthTex, 0, 0),
            ColorTargets = new[]
            {
                new FramebufferAttachmentDescription(colorTex, 0, 0)
            }
        );
    }
}
```

### 4.6 Advanced: Attachment Variants

**Array layer selection**:
```csharp
// Render a diferentes slices da mesma textura
for (int layer = 0; layer < cubemapFaces; layer++)
{
    var fb = factory.CreateFramebuffer(new FramebufferDescription
    {
        ColorTargets = new[]
        {
            new FramebufferAttachmentDescription(cubeTexture, layer, 0)
        }
    });

    cl.SetFramebuffer(fb);
    cl.Draw(...);  // Desenha em cubemapTexture[layer]
}
```

**Mip level targeting**:
```csharp
// Para construir mipmaps manualmente
Texture mipChain = factory.CreateTexture(...);

for (uint mip = 0; mip < mipChain.MipLevels; mip++)
{
    uint mipWidth = Math.Max(1u, mipChain.Width >> (int)mip);
    uint mipHeight = Math.Max(1u, mipChain.Height >> (int)mip);

    var fb = factory.CreateFramebuffer(new FramebufferDescription
    {
        ColorTargets = new[]
        {
            new FramebufferAttachmentDescription(mipChain, 0, mip)
        }
    });

    cl.SetFramebuffer(fb);
    cl.Draw(...);  // Render com shader downsampling
}
```

---

## 5. Shader Specialization Constants

### 5.1 Conceito

**Compile-time constants** permitem branch-free shader código:

```glsl
// Shader
layout(constant_id = 0) const bool USE_NORMAL_MAPPING = false;
layout(constant_id = 1) const float GAMMA = 2.2;

void main()
{
    if (USE_NORMAL_MAPPING)
        normal = texture(normalMap, uv).xyz;

    color = pow(color, vec3(1.0 / GAMMA));
}
```

Quando `USE_NORMAL_MAPPING = false`, compilador **elimina dead code**!

### 5.2 Veldrid API

```csharp
// Define constants
var specs = new[]
{
    new SpecializationConstant(0, true),      // ID=0, type=bool
    new SpecializationConstant(1, 2.2f),      // ID=1, type=float
};

// Pass to pipeline
var shaderSet = new ShaderSetDescription(
    vertexLayouts,
    new[] { vs, fs },
    specs);

var pipelineDesc = new GraphicsPipelineDescription
{
    ShaderSet = shaderSet,
    // ...
};

var pipeline = factory.CreateGraphicsPipeline(ref pipelineDesc);
```

### 5.3 NeoDemo Usage Example

```csharp
public static SpecializationConstant[] GetSpecializations(GraphicsDevice gd)
{
    return new[]
    {
        new SpecializationConstant(0, gd.IsClipSpaceYInverted),
        new SpecializationConstant(1, gd.IsDepthRangeZeroToOne),
        new SpecializationConstant(2, gd.Resources.TextureCoordinatesInvertedY),
        new SpecializationConstant(3, swapchainIsSrgb),
    };
}

// Uso
var specs = ShaderHelper.GetSpecializations(gd);
var vs = factory.CreateFromSpirv(new ShaderDescription
{
    Stage = ShaderStages.Vertex,
    ShaderBytes = vertexShaderBytes,
    Specializations = specs  // ← Apply
});
```

### 5.4 Backend Implementations

#### Vulkan

```csharp
// Native Vulkan specialization
VkSpecializationInfo specInfo = new();
specInfo.mapEntryCount = (uint)specs.Length;

var entries = new VkSpecializationMapEntry[specs.Length];
for (uint i = 0; i < specs.Length; i++)
{
    entries[i].constantID = specs[i].ID;
    entries[i].offset = i * sizeof(ulong);
    entries[i].size = 8;  // ulong
}

specInfo.pMapEntries = entries;
specInfo.dataSize = (ulong)(specs.Length * sizeof(ulong));
specInfo.pData = specData;

// Attach a shader stage
shaderStageInfo.pSpecializationInfo = &specInfo;
```

#### Metal

```csharp
// Metal function constants
var constantValues = MTLFunctionConstantValues.Create();

for (uint i = 0; i < specs.Length; i++)
{
    unsafe
    {
        ulong data = specs[i].Data;
        constantValues.SetConstantValue(&data, MTLDataType.Float, i);
    }
}

// Create specialized function
MTLFunction specializedFunc = mtlLibrary.MakeFunction(
    functionName,
    constantValues);
```

### 5.5 Aplicação em OpenSAGE

**Potencial uso**:

```csharp
// terrain.vert.glsl
layout(constant_id = 0) const bool WIREFRAME_MODE = false;
layout(constant_id = 1) const int LOD_LEVEL = 0;

// fixed-function.vert
layout(constant_id = 0) const bool FLIP_Y = false;  // Para clip space

// particle.frag
layout(constant_id = 0) const float GAMMA_CORRECTION = 1.0;
layout(constant_id = 1) const int MAX_LIGHTS = 8;

// Pipeline creation
public static SpecializationConstant[] GetTerrainSpecializations(
    bool wireframeMode,
    int lodLevel)
{
    return new[]
    {
        new SpecializationConstant(0, wireframeMode),
        new SpecializationConstant(1, (uint)lodLevel),
    };
}

public Pipeline CreateTerrainPipeline(
    bool wireframe,
    int lod)
{
    var specs = GetTerrainSpecializations(wireframe, lod);

    var shaderSet = new ShaderSetDescription(
        TerrainVertexLayouts,
        new[] { terrainVs, terrainFs },
        specs);

    return gd.ResourceFactory.CreateGraphicsPipeline(
        new GraphicsPipelineDescription
        {
            ShaderSet = shaderSet,
            // ...
        });
}
```

### 5.6 Compilação Shader

**SPIRV-Cross for specialization**:
```bash
# Compile com specialization constants
glslc terrain.vert -o terrain.vert.spv
glslc terrain.frag -o terrain.frag.spv

# Veldrid usa spirv-cross internamente para criar specialized variants
```

---

## 6. Feature Support Queries

### 6.1 GraphicsDeviceFeatures API

```csharp
public class GraphicsDeviceFeatures
{
    // Shader capabilities
    public bool ComputeShader { get; set; }
    public bool GeometryShader { get; set; }
    public bool TessellationShaders { get; set; }

    // Rendering features
    public bool MultipleViewports { get; set; }
    public bool FillModeWireframe { get; set; }
    public bool DepthClipDisable { get; set; }

    // Sampling
    public bool SamplerLodBias { get; set; }
    public bool SamplerAnisotropy { get; set; }

    // Drawing
    public bool DrawBaseVertex { get; set; }
    public bool DrawBaseInstance { get; set; }
    public bool DrawIndirect { get; set; }
    public bool DrawIndirectBaseInstance { get; set; }

    // Texturing
    public bool Texture1D { get; set; }
    public bool SubsetTextureView { get; set; }

    // Buffer
    public bool StructuredBuffer { get; set; }
    public bool BufferRangeBinding { get; set; }

    // Precision
    public bool ShaderFloat64 { get; set; }

    // Misc
    public bool CommandListDebugMarkers { get; set; }
    public bool IndependentBlend { get; set; }
}
```

### 6.2 Backend Detection

#### Direct3D 11

```csharp
var d3dDevice = gd.Device;
var featureLevel = d3dDevice.FeatureLevel;

// D3D11_FEATURE_LEVEL_11_1 → novos features
D3D11_FEATURE_DATA_DOUBLES doubles;
d3dDevice.CheckFeatureSupport(D3D11_FEATURE.DOUBLES, &doubles, sizeof(doubles));

features.ShaderFloat64 = doubles.DoublePrecisionFloatShaderOps;
```

#### OpenGL

```csharp
// OpenGL uses extension strings
class OpenGLExtensions
{
    public bool ComputeShaders { get; set; }
    public bool Tessellation { get; set; }
    public bool Anisotropy { get; set; }
    // ...

    public void LoadExtensions()
    {
        var exts = GL.GetString(StringName.Extensions).Split();

        ComputeShaders = exts.Contains("GL_ARB_compute_shader")
            || gd.Version >= new Version(4, 3);

        Tessellation = exts.Contains("GL_ARB_tessellation_shader")
            || gd.Version >= new Version(4, 0);

        Anisotropy = exts.Contains("GL_EXT_texture_filter_anisotropic");
    }
}
```

#### Metal

```csharp
// Metal uses feature sets
class MTLFeatureSupport
{
    public bool SupportsGeometryShaders
    {
        get => _device.SupportsFeatureSet(MTLFeatureSet.macOS_GPUFamily1_v2);
    }

    public bool SupportsMultipleViewports
    {
        get => _device.SupportsFeatureSet(MTLFeatureSet.macOS_GPUFamily1_v3);
    }
}
```

#### Vulkan

```csharp
// Vulkan uses physical device features
class VkGraphicsDevice
{
    private VkPhysicalDeviceFeatures _physicalDeviceFeatures;

    public VkGraphicsDevice(VkPhysicalDevice physicalDevice)
    {
        vkGetPhysicalDeviceFeatures(physicalDevice, &_physicalDeviceFeatures);

        features.GeometryShader = _physicalDeviceFeatures.geometryShader;
        features.TessellationShaders = _physicalDeviceFeatures.tessellationShader;
        features.ShaderFloat64 = _physicalDeviceFeatures.shaderFloat64;
    }
}
```

### 6.3 Runtime Feature Queries

```csharp
public void ConfigurePipeline(GraphicsDevice gd)
{
    var features = gd.Features;

    // Fallback patterns
    if (features.FillModeWireframe)
    {
        // Use wireframe for debugging
        rasterizerState.FillMode = PolygonFillMode.Wireframe;
    }
    else
    {
        // Fallback to solid
        rasterizerState.FillMode = PolygonFillMode.Solid;
    }

    if (features.ComputeShader)
    {
        // Use compute for post-processing
        UseComputePostProcessing();
    }
    else
    {
        // Fallback to fragment shader
        UseFragmentPostProcessing();
    }
}
```

### 6.4 Pixel Format Queries

```csharp
public class GraphicsDevice
{
    public bool GetPixelFormatSupport(
        PixelFormat format,
        TextureType type,
        TextureUsage usage,
        out PixelFormatProperties properties)
    {
        // Backend-specific query
        // Returns:
        //   - max dimensions
        //   - sample count support
        //   - format viewability
    }
}
```

**NeoDemo usage**:
```csharp
public class SceneContext
{
    public void RecreateWindowSizedResources(GraphicsDevice gd, CommandList cl)
    {
        // Query support antes de criar targets
        gd.GetPixelFormatSupport(
            PixelFormat.R16_G16_B16_A16_Float,
            TextureType.Texture2D,
            TextureUsage.RenderTarget,
            out PixelFormatProperties properties);

        TextureSampleCount sampleCount = MainSceneSampleCount;

        if (!properties.SampleCounts.Contains(sampleCount))
        {
            // Fallback to supported count
            sampleCount = properties.SampleCounts[0];
        }

        // Create com sample count suportado
        MainSceneColorTexture = gd.ResourceFactory.CreateTexture(
            new TextureDescription
            {
                Width = (uint)gd.MainSwapchain.Framebuffer.Width,
                Height = (uint)gd.MainSwapchain.Framebuffer.Height,
                Format = PixelFormat.R16_G16_B16_A16_Float,
                Usage = TextureUsage.RenderTarget,
                SampleCount = sampleCount  // ← Validated
            });
    }
}
```

### 6.5 Implementação em OpenSAGE

**Melhorias sugeridas**:

```csharp
// src/OpenSage.Graphics/GraphicsCapabilities.cs
public class GraphicsCapabilities
{
    private readonly GraphicsDevice _gd;

    public GraphicsCapabilities(GraphicsDevice gd)
    {
        _gd = gd;
    }

    // High-level capability queries
    public bool SupportsAdvancedRendering
    {
        get => _gd.Features.ComputeShader
            && _gd.Features.TessellationShaders;
    }

    public bool SupportsHighQualityMode
    {
        get => _gd.Features.SamplerAnisotropy
            && _gd.Features.MultipleViewports;
    }

    public TextureSampleCount GetSupportedMSAASampleCount(uint preferred)
    {
        _gd.GetPixelFormatSupport(
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureType.Texture2D,
            TextureUsage.RenderTarget,
            out var props);

        return props.SampleCounts.Contains((TextureSampleCount)preferred)
            ? (TextureSampleCount)preferred
            : props.SampleCounts[0];
    }

    public bool CanUseFeature(string featureName)
    {
        return featureName switch
        {
            "wireframe" => _gd.Features.FillModeWireframe,
            "compute" => _gd.Features.ComputeShader,
            "tessellation" => _gd.Features.TessellationShaders,
            "geometry" => _gd.Features.GeometryShader,
            "anisotropy" => _gd.Features.SamplerAnisotropy,
            _ => false
        };
    }
}

// Usage
public class GraphicsSystem
{
    public GraphicsCapabilities Capabilities { get; private set; }

    public void Initialize(GraphicsDevice gd)
    {
        Capabilities = new GraphicsCapabilities(gd);

        if (!Capabilities.SupportsAdvancedRendering)
            Console.WriteLine("Warning: Advanced rendering features unavailable");

        var msaaCount = Capabilities.GetSupportedMSAASampleCount(8);
        Console.WriteLine($"MSAA sample count: {msaaCount}");
    }
}
```

---

## 7. Implementation Roadmap para OpenSAGE

### Phase 1: Foundation (Já Existe)
- ✅ `ResourceFactory` abstração
- ✅ `CommandList` deferred recording
- ✅ `Framebuffer` + attachments
- ✅ `GraphicsDevice.Features` access

### Phase 2: Optimization Patterns (Próximo)

**1. Pipeline Caching**
```csharp
// Adicionar RenderResourceCache
- Eliminar recriação desnecessária
- Measurable: Profile pipeline creation time
```

**2. Dynamic Binding**
```csharp
// Usar DynamicBinding para uniform buffers
- Reduzir ResourceSet allocations
- Especialmente para per-object uniforms
```

**3. Feature-based Rendering**
```csharp
// Usar GraphicsDeviceFeatures para fallbacks
- Graceful degradation
- Mobile/Low-end hardware support
```

### Phase 3: Advanced (Futuro)

**1. Shader Specialization**
```csharp
// Use spec constants para:
- Wireframe mode debugging
- LOD selection
- Gamma correction per-backend
```

**2. Command List Paralelization**
```csharp
// Secondary command lists para:
- Terrain tiling
- Shadow casters
- UI rendering (batched)
```

**3. Advanced Framebuffer Techniques**
```csharp
// Cube maps, array layers, mip targeting
// For deferred rendering, GBuffer setup
```

---

## 8. Comparativo Backend-Specific

| Aspecto | Vulkan | D3D11 | Metal | OpenGL |
|--------|--------|-------|-------|---------|
| **CommandList** | Native VkCmdBuffer | Deferred context | MTLCommandBuffer | Custom emulation |
| **Thread Safety** | Safe (per queue) | Safe (deferred) | Safe | Single-thread executor |
| **Pipeline Caching** | Manual | D3D11ResourceCache | Manual | Manual |
| **Framebuffer** | VkRenderPass variants | RTV/DSV | MTLRenderPassDescriptor | GLFramebuffer |
| **Specialization** | VkSpecializationInfo | N/A | MTLFunctionConstants | N/A (preprocessor) |
| **Feature Detection** | VkPhysicalDeviceFeatures | CheckFeatureSupport | MTLFeatureSets | Extension strings |

---

## 9. Checklist de Implementação

### ResourceFactory & Two-Level Binding
- [ ] Document ResourceLayout vs ResourceSet distinction
- [ ] Add dynamic binding examples
- [ ] Create ResourceSet reuse patterns

### CommandList
- [ ] Profile command recording overhead
- [ ] Consider secondary command lists for batching
- [ ] Document single-thread constraint

### Pipeline Caching
- [ ] Implement RenderResourceCache in GraphicsSystem
- [ ] Profile cache hit rates
- [ ] Add cache invalidation on window resize

### Framebuffer
- [ ] Document attachment layer/mip selection
- [ ] Implement cube map rendering example
- [ ] Add mip generation pattern

### Specialization Constants
- [ ] Add wireframe mode specialization
- [ ] Add gamma correction specialization
- [ ] Create shader compilation guide

### Feature Queries
- [ ] Implement GraphicsCapabilities wrapper
- [ ] Add graceful feature fallbacks
- [ ] Document backend differences

---

## 10. Referencias

- [Veldrid Source](https://github.com/veldrid/veldrid)
- [NeoDemo Pipeline Patterns](https://github.com/veldrid/veldrid/blob/main/src/NeoDemo/StaticResourceCache.cs)
- [ResourceFactory API](https://github.com/veldrid/veldrid/blob/main/src/Veldrid/ResourceFactory.cs)
- OpenSAGE: `src/OpenSage.Graphics/`

---

**Document version**: 1.0  
**Last updated**: 12 de dezembro de 2025  
**Status**: Ready for implementation planning
