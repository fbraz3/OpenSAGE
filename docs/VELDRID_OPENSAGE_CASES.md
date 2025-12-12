# Veldrid Patterns - OpenSAGE-Specific Implementation Cases

**Aplicações práticas para cenários do OpenSAGE**  
Data: 12 de dezembro de 2025

---

## 1. Terrain Rendering com Pipeline Caching

### 1.1 Cenário

OpenSAGE renderiza terreno em LOD (Level of Detail) com diferentes shaders:
- Ultra distance: simplified geometry
- Far: normal LOD
- Medium: detail textures
- Close: full quality + normal mapping

### 1.2 Problema sem Caching

```csharp
// ❌ Sem cache: recria pipeline a cada frame
for (int lod = 0; lod < 4; lod++)
{
    var pipeDesc = CreatePipelineDescription(lod);
    var pipe = factory.CreateGraphicsPipeline(ref pipeDesc);  // EXPENSIVE!
    
    cl.SetPipeline(pipe);
    RenderTerrainLOD(cl, lod);
}
```

**Impacto**: 4x pipeline creation overhead por frame!

### 1.3 Solução com Cache

```csharp
// ✅ Com cache: cria uma vez, reusa sempre
public class TerrainRenderSystem
{
    private readonly RenderResourceCache _cache;
    private readonly Pipeline[] _lodPipelines = new Pipeline[4];

    public void Initialize(RenderResourceCache cache)
    {
        _cache = cache;
        
        // Create all LOD pipelines once
        for (int lod = 0; lod < 4; lod++)
        {
            var pipeDesc = CreatePipelineDescription(lod);
            _lodPipelines[lod] = cache.GetGraphicsPipeline(ref pipeDesc);
        }
    }

    public void Render(CommandList cl)
    {
        // Reuse cached pipelines
        for (int lod = 0; lod < 4; lod++)
        {
            cl.SetPipeline(_lodPipelines[lod]);
            RenderTerrainLOD(cl, lod);
        }
    }

    private GraphicsPipelineDescription CreatePipelineDescription(int lod)
    {
        var specializations = new SpecializationConstant[]
        {
            new SpecializationConstant(0, (uint)lod),  // LOD_LEVEL
            new SpecializationConstant(1, lod < 2),   // USE_DETAIL = false for far
        };

        return new GraphicsPipelineDescription
        {
            ShaderSet = new ShaderSetDescription(
                new[] { TerrainVertexLayout },
                new[] { terrainVs, terrainFs },
                specializations),
            // ... other state
        };
    }
}
```

**Benefício**: Pipelines criados uma vez no init, zero overhead em runtime

---

## 2. Object Rendering com Dynamic Uniforms

### 2.1 Cenário

OpenSAGE renderiza centenas de objetos (buildings, units) com:
- Per-frame uniforms: view matrix, time, lighting (same for all)
- Per-object uniforms: world matrix, color, animation state (different each)

### 2.2 Problema: Uniform Buffer Overhead

```csharp
// ❌ Ineficiente: novo ResourceSet por objeto
var objectUniforms = new ObjectUniform
{
    WorldMatrix = obj.Transform,
    Color = obj.Team.Color,
    AnimState = obj.AnimationState
};

var objectBuffer = factory.CreateBuffer(new BufferDescription(
    (uint)Marshal.SizeOf<ObjectUniform>(),
    BufferUsage.UniformBuffer));

gd.UpdateBuffer(objectBuffer, 0, ref objectUniforms);

var objectSet = factory.CreateResourceSet(
    new ResourceSetDescription(objectLayout, objectBuffer));  // New set per obj!

// Render
cl.SetGraphicsResourceSet(1, objectSet);
cl.Draw(obj.VertexCount);
```

**Impacto**: 1000 objetos = 1000 ResourceSet allocations + 1000 buffer updates

### 2.3 Solução: Dynamic Binding

```csharp
// ✅ Eficiente: um buffer grande, múltiplos offsets
public class DynamicObjectBuffer
{
    private readonly DeviceBuffer _buffer;
    private readonly GraphicsDevice _gd;
    private readonly uint _elementSize;
    private uint _currentOffset = 0;

    public DynamicObjectBuffer(GraphicsDevice gd, uint maxObjects)
    {
        _gd = gd;
        _elementSize = (uint)Marshal.SizeOf<ObjectUniform>();
        
        // Allocate for all objects once
        _buffer = gd.ResourceFactory.CreateBuffer(new BufferDescription(
            _elementSize * maxObjects,
            BufferUsage.UniformBuffer | BufferUsage.Dynamic));
    }

    public uint AppendUniform(ObjectUniform uniform)
    {
        uint offset = _currentOffset;
        
        unsafe
        {
            fixed (ObjectUniform* ptr = &uniform)
            {
                _gd.UpdateBuffer(_buffer, offset * _elementSize, (IntPtr)ptr, _elementSize);
            }
        }
        
        _currentOffset++;
        return offset;
    }

    public void ResetForNextFrame()
    {
        _currentOffset = 0;
    }

    public DeviceBuffer Buffer => _buffer;
}

// Usage
public class ObjectRenderSystem
{
    private DynamicObjectBuffer _dynamicBuffer;
    private ResourceSet _perFrameSet;
    private Pipeline _objectPipeline;

    public void Initialize(RenderResourceCache cache, GraphicsDevice gd)
    {
        _dynamicBuffer = new DynamicObjectBuffer(gd, maxObjects: 1000);
        
        // Layout with DynamicBinding
        var layoutDesc = new ResourceLayoutDescription(
            new ResourceLayoutElementDescription(
                "ObjectUniforms",
                ResourceKind.UniformBuffer,
                ShaderStages.Vertex,
                ResourceLayoutElementOptions.DynamicBinding));  // ← Dynamic!

        var layout = cache.GetResourceLayout(ref layoutDesc);
        
        // Single ResourceSet with dynamic buffer
        _perFrameSet = gd.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(layout, _dynamicBuffer.Buffer));
    }

    public void Render(CommandList cl, IEnumerable<GameObject> objects)
    {
        _dynamicBuffer.ResetForNextFrame();
        cl.SetPipeline(_objectPipeline);
        cl.SetGraphicsResourceSet(0, _perFrameSet);

        foreach (var obj in objects)
        {
            // Build uniform
            var uniform = new ObjectUniform
            {
                WorldMatrix = obj.Transform,
                Color = obj.Team.Color,
                AnimState = obj.AnimationState
            };

            // Get offset in buffer
            uint offset = _dynamicBuffer.AppendUniform(uniform);

            // Bind with offset - NO new ResourceSet created!
            cl.SetGraphicsResourceSet(1, _perFrameSet, new[] { offset * 256u });

            cl.Draw(obj.VertexCount);
        }
    }
}
```

**Benefício**: 1000 objetos = 1 ResourceSet + 1 buffer, 1000x menos overhead

---

## 3. Shadow Rendering com Multiple Framebuffers

### 3.1 Cenário

OpenSAGE suporta dynamic shadows (DirectionalLight.ShadowsEnabled):
1. Render depth from light view → shadow map
2. Render scene → compara depth com shadow map

### 3.2 Solução: Multiple Framebuffers

```csharp
public class ShadowRenderingSystem
{
    private FramebufferManager _fbManager;
    private Pipeline _shadowMapPipeline;
    private Pipeline _shadedObjectPipeline;
    private Texture _shadowMapTexture;

    public void Initialize(
        GraphicsDevice gd,
        RenderResourceCache cache,
        FramebufferManager fbManager)
    {
        _fbManager = fbManager;

        // Create shadow map framebuffer (depth only)
        var shadowMapFb = gd.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription
            {
                ColorTargets = Array.Empty<FramebufferAttachmentDescription>(),
                DepthTarget = new FramebufferAttachmentDescription(
                    CreateShadowMapTexture(gd, 2048),
                    arrayLayer: 0,
                    mipLevel: 0)
            });

        // Get/create pipelines
        _shadowMapPipeline = cache.GetGraphicsPipeline(
            ref new PipelineBuilder(cache, gd)
                .WithShaders(shadowVs, shadowFs)
                .Build());

        _shadedObjectPipeline = cache.GetGraphicsPipeline(
            ref new PipelineBuilder(cache, gd)
                .WithShaders(shadedVs, shadedFs)
                .Build());
    }

    public void Render(
        CommandList cl,
        DirectionalLight mainLight,
        IEnumerable<GameObject> objects)
    {
        // ═══════════════════════════════════════════════
        // PASS 1: Render to shadow map from light perspective
        // ═══════════════════════════════════════════════
        var shadowFb = _fbManager.GetWindowFramebuffer(
            "ShadowMap",
            PixelFormat.R8_G8_B8_A8_UNorm,
            PixelFormat.D32_Float);

        using (var recorder = new CommandListRecorder(cl))
        {
            recorder.CommandList.SetFramebuffer(shadowFb);
            recorder.CommandList.ClearDepthStencil(1.0f);
            recorder.CommandList.SetPipeline(_shadowMapPipeline);

            // Set view matrix from light
            var lightViewProj = mainLight.GetViewProjectionMatrix();
            UpdateViewProjBuffer(lightViewProj);

            foreach (var obj in objects)
            {
                recorder.CommandList.Draw(obj.VertexCount);
            }
        }

        // ═══════════════════════════════════════════════
        // PASS 2: Render scene with shadows
        // ═══════════════════════════════════════════════
        var sceneFb = _fbManager.GetWindowFramebuffer(
            "Scene",
            PixelFormat.R8_G8_B8_A8_UNorm,
            PixelFormat.D32_Float);

        using (var recorder = new CommandListRecorder(cl))
        {
            recorder.CommandList.SetFramebuffer(sceneFb);
            recorder.CommandList.ClearColorTarget(0, RgbaFloat.Black);
            recorder.CommandList.ClearDepthStencil(1.0f);
            recorder.CommandList.SetPipeline(_shadedObjectPipeline);

            // Bind shadow map as texture
            var shadowTextureView = gd.ResourceFactory.CreateTextureView(_shadowMapTexture);
            var shadowMapSet = gd.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(shadowLayout, shadowTextureView));

            recorder.CommandList.SetGraphicsResourceSet(2, shadowMapSet);

            foreach (var obj in objects)
            {
                recorder.CommandList.Draw(obj.VertexCount);
            }
        }
    }

    private Texture CreateShadowMapTexture(GraphicsDevice gd, uint size)
    {
        return gd.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = size,
            Height = size,
            Format = PixelFormat.D32_Float,
            Usage = TextureUsage.DepthStencil | TextureUsage.Sampled,  // ← Read in shader!
            Type = TextureType.Texture2D,
            MipLevels = 1,
            ArrayLayers = 1,
            SampleCount = TextureSampleCount.Count1
        });
    }
}
```

**Padrão**: Múltiplos framebuffers encadeados com o output de um como input do próximo

---

## 4. Particle System com Compute Shaders (Quando Suportado)

### 4.1 Cenário

OpenSAGE renderiza explosões, fumaça, etc com milhares de partículas.
Física pode ser:
- CPU (compatível todos backends) - lento
- GPU Compute (Vulkan/D3D11) - rápido

### 4.2 Solução: Graceful Fallback

```csharp
public class ParticleSystem
{
    private bool _useComputePhysics;
    private Pipeline _computePhysicsPipeline;
    private Pipeline _cpuPhysicsRenderPipeline;

    public void Initialize(
        GraphicsDevice gd,
        RenderResourceCache cache,
        GraphicsCapabilities caps)
    {
        _useComputePhysics = caps.Supports(GraphicsFeature.ComputeShaders);

        if (_useComputePhysics)
        {
            // GPU-accelerated physics
            _computePhysicsPipeline = cache.GetComputePipeline(
                ref new ComputePipelineDescription
                {
                    ComputeShader = computeShader,
                    Specializations = new[]
                    {
                        new SpecializationConstant(0, 512u),  // Group size
                    }
                });

            Console.WriteLine("✓ Using GPU particle physics");
        }
        else
        {
            // CPU fallback
            _cpuPhysicsRenderPipeline = cache.GetGraphicsPipeline(
                ref new GraphicsPipelineDescription { /* ... */ });

            Console.WriteLine("⚠ Falling back to CPU particle physics");
        }
    }

    public void Update(
        CommandList cl,
        GraphicsDevice gd,
        float deltaTime,
        List<Particle> particles)
    {
        if (_useComputePhysics)
        {
            UpdateComputePhysics(cl, gd, particles);
        }
        else
        {
            UpdateCpuPhysics(gd, particles, deltaTime);
        }
    }

    private void UpdateComputePhysics(
        CommandList cl,
        GraphicsDevice gd,
        List<Particle> particles)
    {
        // Update particle data in GPU buffer
        var particleBuffer = gd.ResourceFactory.CreateBuffer(
            new BufferDescription(
                (uint)particles.Count * ParticleStride,
                BufferUsage.StructuredBufferReadWrite));

        cl.SetPipeline(_computePhysicsPipeline);
        cl.SetComputeResourceSet(0, particleSet);

        uint groupsX = ((uint)particles.Count + 511) / 512;
        cl.Dispatch(groupsX, 1, 1);  // ← GPU computes positions
    }

    private void UpdateCpuPhysics(
        GraphicsDevice gd,
        List<Particle> particles,
        float deltaTime)
    {
        // CPU simulation
        for (int i = 0; i < particles.Count; i++)
        {
            particles[i].Position += particles[i].Velocity * deltaTime;
            particles[i].Velocity += GravityVector * deltaTime;
            // ...
        }

        // Update GPU buffer with CPU results
        var particleData = particles.ToArray();
        gd.UpdateBuffer(particleBuffer, 0, particleData);
    }

    public void Render(CommandList cl, Framebuffer framebuffer)
    {
        using (var recorder = new CommandListRecorder(cl))
        {
            recorder.CommandList.SetFramebuffer(framebuffer);
            recorder.CommandList.SetPipeline(_particleRenderPipeline);

            // Draw instanced particles
            recorder.CommandList.DrawIndirect(
                indirectBuffer,
                indirectBufferOffset);
        }
    }
}
```

**Padrão**: Check feature → Choose code path → Execute optimized or fallback version

---

## 5. Post-Processing Pipeline com Mip Chains

### 5.1 Cenário

OpenSAGE suporta post-processing (bloom, color correction, FXAA).
Bloom requer downsample da cor com mip chain.

### 5.2 Solução: Manual Mip Generation

```csharp
public class PostProcessingSystem
{
    private Texture _hdrScene;
    private Texture _bloomTexture;
    private Framebuffer[] _bloomMipFramebuffers;

    public void Initialize(GraphicsDevice gd, RenderResourceCache cache)
    {
        // HDR scene texture
        _hdrScene = gd.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = 1920,
            Height = 1080,
            Format = PixelFormat.R16_G16_B16_A16_Float,
            Usage = TextureUsage.RenderTarget | TextureUsage.Sampled,
            MipLevels = 1
        });

        // Bloom texture with mips
        _bloomTexture = gd.ResourceFactory.CreateTexture(new TextureDescription
        {
            Width = 1920,
            Height = 1080,
            Format = PixelFormat.R16_G16_B16_A16_Float,
            Usage = TextureUsage.RenderTarget | TextureUsage.Sampled,
            MipLevels = 10  // ← Multiple mips
        });

        // Create framebuffers for each mip level
        _bloomMipFramebuffers = new Framebuffer[10];
        for (uint mip = 0; mip < 10; mip++)
        {
            _bloomMipFramebuffers[mip] = gd.ResourceFactory.CreateFramebuffer(
                new FramebufferDescription
                {
                    ColorTargets = new[]
                    {
                        new FramebufferAttachmentDescription(
                            _bloomTexture,
                            arrayLayer: 0,
                            mipLevel: mip)  // ← Different mip!
                    }
                });
        }
    }

    public void Render(
        CommandList cl,
        Framebuffer sceneFramebuffer,
        GraphicsDevice gd)
    {
        // ═══════════════════════════════════════════════
        // PASS 1: Render scene to HDR
        // ═══════════════════════════════════════════════
        using (var recorder = new CommandListRecorder(cl))
        {
            recorder.CommandList.SetFramebuffer(sceneFramebuffer);
            recorder.CommandList.ClearColorTarget(0, RgbaFloat.Black);
            // ... render scene
        }

        // ═══════════════════════════════════════════════
        // PASS 2: Build bloom mip chain (downsampling)
        // ═══════════════════════════════════════════════
        using (var recorder = new CommandListRecorder(cl))
        {
            var bloomPipeline = GetBloomDownsamplePipeline();

            // Mip 0: Full size
            recorder.CommandList.SetFramebuffer(_bloomMipFramebuffers[0]);
            recorder.CommandList.SetPipeline(bloomPipeline);
            var hdrView = gd.ResourceFactory.CreateTextureView(_hdrScene);
            var hdrSet = gd.ResourceFactory.CreateResourceSet(
                new ResourceSetDescription(bloomLayout, hdrView));
            recorder.CommandList.SetGraphicsResourceSet(0, hdrSet);
            recorder.CommandList.Draw(6);  // Quad

            // Mip 1..N: Progressively smaller
            for (uint mip = 1; mip < 10; mip++)
            {
                recorder.CommandList.SetFramebuffer(_bloomMipFramebuffers[mip]);

                // Sample from previous mip
                var prevMipView = gd.ResourceFactory.CreateTextureView(
                    new TextureViewDescription(_bloomTexture, baseMipLevel: mip - 1, mipLevelCount: 1));
                var prevMipSet = gd.ResourceFactory.CreateResourceSet(
                    new ResourceSetDescription(bloomLayout, prevMipView));

                recorder.CommandList.SetGraphicsResourceSet(0, prevMipSet);
                recorder.CommandList.Draw(6);
            }
        }

        // ═══════════════════════════════════════════════
        // PASS 3: Compose bloom with scene
        // ═══════════════════════════════════════════════
        // (Use bloom texture in composition shader)
    }
}
```

**Padrão**: Mip attachment para progressive downsampling sem intermediate textures

---

## 6. Device Lost Handling (Direct3D 11)

### 6.1 Cenário

D3D11 pode perder device (alt-tab, GPU driver recovery).
OpenSAGE deve handle gracefully.

### 6.2 Solução: Resource Rebuilding

```csharp
public class DeviceManager
{
    private GraphicsDevice _graphicsDevice;
    private RenderResourceCache _cache;
    private FramebufferManager _fbManager;
    private List<IRenderPass> _renderPasses;

    public void CheckDeviceLost()
    {
        if (_graphicsDevice.Backend == GraphicsBackend.Direct3D11)
        {
            // Check for device lost
            if (IsDeviceLost())
            {
                Console.WriteLine("⚠ Graphics device lost, rebuilding resources...");
                RebuildDeviceResources();
            }
        }
    }

    private void RebuildDeviceResources()
    {
        // 1. Clear all cached resources
        _cache.Clear();
        _fbManager.Clear();

        // 2. Notify render passes to rebuild
        foreach (var pass in _renderPasses)
        {
            pass.RecreateWindowSized();
        }

        Console.WriteLine("✓ Resources rebuilt successfully");
    }

    private bool IsDeviceLost()
    {
        // Try to submit empty command list
        try
        {
            var cl = _graphicsDevice.ResourceFactory.CreateCommandList();
            cl.Begin();
            cl.End();
            _graphicsDevice.SubmitCommands(cl);
            return false;
        }
        catch (Exception ex)
        {
            return ex.Message.Contains("device lost") || 
                   ex.Message.Contains("reset");
        }
    }
}
```

---

## 7. Backend-Specific Optimizations

### 7.1 Vulkan: Secondary Command Lists (Future)

```csharp
// ⚠️ Não suportado ainda em Veldrid, mas planejado
public class VulkanOptimizedTerrain
{
    public void RenderTerrain(CommandList primaryList)
    {
        // Ideal pattern (quando implementado):
        // var secondaryLists = ParallelRecordTerrainTiles();
        // foreach (var secondary in secondaryLists)
        //     primaryList.ExecuteCommandList(secondary);
    }
}
```

### 7.2 Metal: TextureBarriers (Automatic)

```csharp
public class MetalOptimizedRendering
{
    public void Render(CommandList cl)
    {
        // Metal handles texture layout transitions automatically
        cl.SetFramebuffer(colorFramebuffer);
        cl.Draw(6);
        
        // No need for manual barrier - MTL handles it
    }
}
```

### 7.3 D3D11: Immediate vs Deferred Context

```csharp
public class D3D11Rendering
{
    public void RenderMainPass(CommandList cl)
    {
        // D3D11 backend uses deferred context automatically
        // No need for manual deferred context management
        
        cl.Begin();
        // ... commands recorded to deferred context
        cl.End();
        
        // Veldrid submits FinishCommandList() + ExecuteCommandList()
        gd.SubmitCommands(cl);
    }
}
```

---

## 8. Integration Checklist para OpenSAGE

### Phase 1: Foundation ✓
- [x] ResourceFactory pattern já existe
- [x] CommandList recording já implementado
- [x] Framebuffer support já existe

### Phase 2: Optimization (Ready to Implement)
- [ ] Implementar `RenderResourceCache` em `GraphicsSystem`
- [ ] Adicionar `GraphicsCapabilities` wrapper
- [ ] Refactor terrain rendering com cache
- [ ] Refactor object rendering com dynamic uniforms
- [ ] Implementar `FramebufferManager` para multi-pass

### Phase 3: Advanced (Planejado)
- [ ] Shader specialization constants
- [ ] Compute shader particle physics (GPU path)
- [ ] Secondary command lists (Vulkan)
- [ ] Mip chain generation automática
- [ ] Device lost handling robusto (D3D11)

---

## 9. Performance Targets

| Métrica | Sem Otimização | Com Padrões | Ganho |
|---------|---------------|------------|-------|
| Pipeline creations/frame | ~20 | ~0 | 20x |
| ResourceSet allocations/frame | ~1000 | ~10 | 100x |
| Memory allocations | ~5MB/frame | ~50KB/frame | 100x |
| CPU time (render setup) | ~8ms | ~0.5ms | 16x |
| GPU frame time | ~12ms | ~11ms | 1.1x |

---

## 10. Testing Strategy

### 10.1 Unit Tests para Padrões

```csharp
[TestFixture]
public class RenderResourceCacheTests
{
    private RenderResourceCache _cache;
    private GraphicsDevice _gd;

    [SetUp]
    public void Setup()
    {
        _gd = CreateTestGraphicsDevice();
        _cache = new RenderResourceCache(_gd);
    }

    [Test]
    public void GetPipeline_SameDescription_ReturnsCachedInstance()
    {
        var desc = new GraphicsPipelineDescription { /* ... */ };

        var pipe1 = _cache.GetGraphicsPipeline(ref desc);
        var pipe2 = _cache.GetGraphicsPipeline(ref desc);

        Assert.AreSame(pipe1, pipe2);  // Same object!
    }

    [Test]
    public void Clear_DisposesAllPipelines()
    {
        var desc1 = new GraphicsPipelineDescription { /* ... */ };
        var desc2 = new GraphicsPipelineDescription { /* ... */ };

        _cache.GetGraphicsPipeline(ref desc1);
        _cache.GetGraphicsPipeline(ref desc2);

        _cache.Clear();

        Assert.Throws<ObjectDisposedException>(() =>
        {
            _cache.GetGraphicsPipeline(ref desc1);
        });
    }
}
```

### 10.2 Integration Tests

```csharp
[TestFixture]
public class TerrainRenderingTests
{
    [Test]
    public void RenderTerrain_WithCaching_NoMemoryLeaks()
    {
        var cache = new RenderResourceCache(_gd);
        var terrain = new TerrainRenderer(cache, _gd);

        terrain.Initialize();

        for (int frame = 0; frame < 100; frame++)
        {
            var cl = _gd.ResourceFactory.CreateCommandList();
            terrain.Render(cl);
            _gd.SubmitCommands(cl);
        }

        var stats = cache.GetCacheStats();
        Assert.That(stats.graphicsPipelines, Is.LessThanOrEqualTo(4));  // LOD count
    }
}
```

---

**Document Version**: 1.0  
**Status**: Ready for integration planning  
**Last Updated**: 12 de dezembro de 2025
