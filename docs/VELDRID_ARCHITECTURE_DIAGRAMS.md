# Veldrid Architecture Diagrams & Flow Charts

**Visualizações dos padrões Veldrid para OpenSAGE**  
Data: 12 de dezembro de 2025

---

## 1. ResourceFactory Pattern Hierarchy

```
┌─────────────────────────────────────────────────────────────┐
│                    GraphicsDevice                           │
│  - Backend: Vulkan | Direct3D11 | Metal | OpenGL           │
│  - Features: GraphicsDeviceFeatures                         │
│  - ResourceFactory: Abstract factory                        │
└──────────────────────┬──────────────────────────────────────┘
                       │
           ┌───────────┴───────────┐
           │                       │
    ┌──────▼──────────┐   ┌────────▼────────┐
    │ ResourceFactory │   │ GraphicsDevice  │
    │  (Abstract)     │   │   - Features    │
    └──────┬──────────┘   └────────┬────────┘
           │                       │
    ┌──────┴─────────────────────┐ │
    │                            │ │
    │        4 Implementations    │ │
    │                            │ │
    ├────────────────────────────┤ │
    │  1. VkResourceFactory      │ │
    │     (Vulkan)               │ │
    │     - VkPipeline           │ │
    │     - VkCommandList        │ │
    │     - VkFramebuffer        │ │
    ├────────────────────────────┤ │
    │  2. D3D11ResourceFactory   │ │
    │     (Direct3D11)           │ │
    │     - D3D11Pipeline        │ │
    │     - D3D11CommandList     │ │
    │     - D3D11Framebuffer     │ │
    ├────────────────────────────┤ │
    │  3. MTLResourceFactory     │ │
    │     (Metal)                │ │
    │     - MTLPipeline          │ │
    │     - MTLCommandList       │ │
    │     - MTLFramebuffer       │ │
    ├────────────────────────────┤ │
    │  4. OpenGLResourceFactory  │ │
    │     (OpenGL)               │ │
    │     - OpenGLPipeline       │ │
    │     - OpenGLCommandList    │ │
    │     - OpenGLFramebuffer    │ │
    └────────────────────────────┘ │
           │                       │
           │ Creates              │ Uses
           │                       │
    ┌──────▼────────────┬──────────▼────────┐
    │ Concrete          │                   │
    │ Resources         │  Application      │
    │                   │  Code             │
    ├───────────────────┤                   │
    │ - Pipeline        │  ┌──────────────┐ │
    │ - CommandList     │  │ GraphicsSystem│ │
    │ - Framebuffer     │  └──────────────┘ │
    │ - Buffer          │                   │
    │ - Texture         │  ┌──────────────┐ │
    │ - ResourceSet     │  │ RenderPass   │ │
    │ - ResourceLayout  │  └──────────────┘ │
    └───────────────────┴───────────────────┘
```

---

## 2. Two-Level Resource Binding Flow

```
APPLICATION CODE
────────────────

Step 1: Define Resource Structure
┌─────────────────────────────────────┐
│ ResourceLayoutDescription           │
├─────────────────────────────────────┤
│ Elements:                           │
│  [0] ViewMatrix (UniformBuffer)     │
│  [1] Diffuse (TextureReadOnly)      │
│  [2] Sampler (Sampler)              │
└──────────────┬──────────────────────┘
               │
               ▼
    factory.CreateResourceLayout()
               │
               ▼
┌──────────────────────────────────────┐
│ ResourceLayout (GPU Template)        │
│ - Defines expected structure         │
│ - Validates at pipeline creation    │
└──────────────┬──────────────────────┘

Step 2: Bind Concrete Resources
┌──────────────────────────────────────┐
│ ResourceSetDescription               │
├──────────────────────────────────────┤
│ Layout: ^above                       │
│ Resources:                           │
│  [0] DeviceBuffer(256 bytes)         │  Must match
│  [1] TextureView(Texture2D)          │  Layout order
│  [2] Sampler(Linear)                 │  & types!
└──────────────┬──────────────────────┘
               │
               ▼
    factory.CreateResourceSet()
               │
               ▼
┌──────────────────────────────────────┐
│ ResourceSet (GPU Binding)            │
│ - Binds actual resources             │
│ - Validates against Layout           │
│ - Reusable across draws              │
└──────────────┬──────────────────────┘

Step 3: Create Pipeline with Layouts
┌──────────────────────────────────────┐
│ GraphicsPipelineDescription          │
├──────────────────────────────────────┤
│ ResourceLayouts: [^Layout from Step1]│
│ ShaderSet: (vs.glsl, fs.glsl)       │
│ RasterizerState, BlendState, etc    │
└──────────────┬──────────────────────┘
               │
               ▼
    factory.CreateGraphicsPipeline()
               │
               ▼
┌──────────────────────────────────────┐
│ Pipeline                             │
│ - Knows about ResourceLayout[0]      │
│ - Will validate ResourceSet at bind  │
└──────────────┬──────────────────────┘

Step 4: Record Commands with Resources
┌──────────────────────────────────────┐
│ CommandList Recording                │
├──────────────────────────────────────┤
│ cl.Begin()                           │
│ cl.SetPipeline(pipeline)             │
│   ^ Sets ResourceLayout[0]           │
│ cl.SetGraphicsResourceSet(0, set)    │
│   ^ Validates set.Layout             │
│   ^ == pipeline.ResourceLayout[0]    │
│ cl.Draw(vertexCount)                 │
│   ^ GPU accesses resources via set   │
│ cl.End()                             │
└──────────────────────────────────────┘
```

---

## 3. CommandList Lifecycle & Threading

```
┌──────────────────────────────────────────────────────────┐
│                    RECORDING PHASE                       │
│                   (Application Thread)                   │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  CommandList cl = factory.CreateCommandList()           │
│         │                                                │
│         ▼                                                │
│  ┌────────────────┐                                      │
│  │   Ready State  │                                      │
│  │   (pristine)   │                                      │
│  └────────┬───────┘                                      │
│           │                                              │
│           │  cl.Begin()                                  │
│           ▼                                              │
│  ┌────────────────────────────────────────┐             │
│  │   RECORDING STATE                      │             │
│  │                                        │             │
│  │  ┌──────────────────────────────────┐ │             │
│  │  │ Issue Commands:                  │ │             │
│  │  │  cl.SetFramebuffer(fb)           │ │             │
│  │  │  cl.SetPipeline(pipe)            │ │             │
│  │  │  cl.SetGraphicsResourceSet(0,rs) │ │             │
│  │  │  cl.Draw(6)                      │ │             │
│  │  │  cl.Draw(12)                     │ │             │
│  │  │  cl.CopyBuffer()                 │ │             │
│  │  │  ...                             │ │             │
│  │  └──────────────────────────────────┘ │             │
│  │                                        │             │
│  │  Backend-Specific Recording:           │             │
│  │  ┌──────────────────────────────────┐ │             │
│  │  │ Vulkan: VkCommandBuffer          │ │             │
│  │  │ D3D11: Deferred ID3D11Context   │ │             │
│  │  │ Metal: MTLCommandBuffer          │ │             │
│  │  │ OpenGL: OpenGLCommandEntryList  │ │             │
│  │  └──────────────────────────────────┘ │             │
│  └────────┬────────────────────────────────┘             │
│           │                                              │
│           │  cl.End()                                    │
│           ▼                                              │
│  ┌────────────────────────────────────────┐             │
│  │   RECORDED STATE                       │             │
│  │   (ready for submission)               │             │
│  │                                        │             │
│  │   Backend-Specific Finalization:       │             │
│  │   ┌──────────────────────────────────┐ │             │
│  │   │ Vulkan: VkCommandBuffer ends    │ │             │
│  │   │ D3D11: ID3D11CommandList        │ │             │
│  │   │ Metal: MTLCommandBuffer.commit()│ │             │
│  │   │ OpenGL: Entry list ready        │ │             │
│  │   └──────────────────────────────────┘ │             │
│  └────────┬────────────────────────────────┘             │
│           │                                              │
└───────────┼──────────────────────────────────────────────┘
            │
            │  gd.SubmitCommands(cl)
            ▼
┌──────────────────────────────────────────────────────────┐
│                   EXECUTION PHASE                        │
│                  (GPU + Executor Thread)                 │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  Backend-Specific Submission:                           │
│  ┌──────────────────────────────────────┐               │
│  │ Vulkan:                              │               │
│  │  vkQueueSubmit()                     │               │
│  │  ↓ GPU executes                      │               │
│  ├──────────────────────────────────────┤               │
│  │ Direct3D11:                          │               │
│  │  ExecuteCommandList()                │               │
│  │  (ID3D11DeviceContext::Immediate)    │               │
│  │  ↓ GPU executes                      │               │
│  ├──────────────────────────────────────┤               │
│  │ Metal:                               │               │
│  │  MTLCommandBuffer.commit()           │               │
│  │  ↓ GPU executes                      │               │
│  ├──────────────────────────────────────┤               │
│  │ OpenGL:                              │               │
│  │  ExecutionThread processes list      │               │
│  │  ↓ Thread executes GL calls          │               │
│  └──────────────────────────────────────┘               │
│                                                          │
│  ┌──────────────────────────────────────┐               │
│  │   GPU WORK HAPPENING                 │               │
│  │   (Application thread can record      │               │
│  │    next CommandList in parallel)      │               │
│  └──────────────────────────────────────┘               │
│           │                                              │
│           ▼                                              │
│  ┌────────────────────────────────────────┐             │
│  │   FINISHED (GPU processing complete)   │             │
│  │                                        │             │
│  │   CommandList now reusable:            │             │
│  │   ─ Call Begin() to record new cmds    │             │
│  │   ─ Or reuse with different data       │             │
│  └────────────────────────────────────────┘             │
│           │                                              │
└───────────┼──────────────────────────────────────────────┘
            │
            └─→ Loop back to RECORDING PHASE
                for next frame
```

---

## 4. Pipeline Caching Architecture

```
┌──────────────────────────────────────────────────────────┐
│           RenderResourceCache (Non-threadsafe)           │
│                (Main Render Thread Only)                 │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Private Dictionaries:                              │ │
│  │                                                    │ │
│  │ • s_pipelines: GraphicsPipelineDesc → Pipeline    │ │
│  │   { desc1: pipe1, desc2: pipe2, ... }             │ │
│  │                                                    │ │
│  │ • s_layouts: ResourceLayoutDesc → ResourceLayout  │ │
│  │   { desc1: layout1, desc2: layout2, ... }         │ │
│  │                                                    │ │
│  │ • s_sets: ResourceSetDesc → ResourceSet           │ │
│  │   { desc1: set1, desc2: set2, ... }               │ │
│  └────────────────────────────────────────────────────┘ │
│           │                              │              │
│           │                              │              │
│  ┌────────▼────────┐          ┌──────────▼────────────┐│
│  │ GetPipeline()   │          │ GetResourceLayout()   ││
│  ├─────────────────┤          ├───────────────────────┤│
│  │ ref Desc →      │          │ ref Desc →            ││
│  │                 │          │                       ││
│  │ ✓ Cache hit?    │          │ ✓ Cache hit?          ││
│  │  └─ Return ✓    │          │  └─ Return ✓          ││
│  │                 │          │                       ││
│  │ ✗ Cache miss?   │          │ ✗ Cache miss?         ││
│  │  ├─ Create new  │          │  ├─ Create new        ││
│  │  │ factory.Cre- │          │  │ factory.Cre-       ││
│  │  │ ateGraphics- │          │  │ ateResourceLayout()││
│  │  │ Pipeline()   │          │  │                    ││
│  │  ├─ Add to dict │          │  ├─ Add to dict       ││
│  │  └─ Return new  │          │  └─ Return new        ││
│  └─────────────────┘          └───────────────────────┘│
│           │                              │              │
│           └──────────────┬───────────────┘              │
│                          │                              │
│                ┌─────────▼──────────┐                   │
│                │ Clear()            │                   │
│                ├────────────────────┤                   │
│                │ Called on:         │                   │
│                │ • Window resized   │                   │
│                │ • Backend changed  │                   │
│                │ • Device lost      │                   │
│                │                    │                   │
│                │ ▼                  │                   │
│                │ Dispose all        │                   │
│                │ Pipelines in dict  │                   │
│                │ Dispose all        │                   │
│                │ Layouts in dict     │                   │
│                │ Clear all dicts     │                   │
│                └────────────────────┘                   │
│                                                          │
└──────────────────────────────────────────────────────────┘

USAGE IN RENDER LOOP:
─────────────────────

Frame N:
┌──────────────────────────────────────┐
│ Pipeline 1 → GetPipeline(desc1)      │
│             └─ Cache miss → Create   │
│ Pipeline 2 → GetPipeline(desc2)      │
│             └─ Cache miss → Create   │
│ ...                                  │
│ Pipeline 5 → GetPipeline(desc5)      │
│             └─ Cache miss → Create   │
└──────────────────────────────────────┘
         5 pipelines created

Frame N+1:
┌──────────────────────────────────────┐
│ Pipeline 1 → GetPipeline(desc1)      │
│             └─ Cache HIT! Reuse      │
│ Pipeline 2 → GetPipeline(desc2)      │
│             └─ Cache HIT! Reuse      │
│ ...                                  │
│ Pipeline 5 → GetPipeline(desc5)      │
│             └─ Cache HIT! Reuse      │
└──────────────────────────────────────┘
         0 pipelines created ✓
```

---

## 5. Feature Detection & Fallback Flow

```
┌─────────────────────────────────┐
│  Graphics Device Initialization │
└────────────┬────────────────────┘
             │
             ▼
┌─────────────────────────────────────────────────────────┐
│ GraphicsDevice.Features initialization                  │
│ (Backend-specific at creation time)                     │
├─────────────────────────────────────────────────────────┤
│                                                         │
│ Vulkan: VkPhysicalDeviceFeatures                       │
│  ├─ geometryShader             ──┐                     │
│  ├─ tessellationShader         ──┤─→ Features struct   │
│  └─ shaderFloat64              ──┘                     │
│                                                         │
│ Direct3D11: Device.CheckFeatureSupport()               │
│  ├─ D3D11_FEATURE_DOUBLES      ──┐                     │
│  ├─ D3D11_FEATURE_COMPUTE      ──┤─→ Features struct   │
│  └─ D3D11_FEATURE_SHADER64     ──┘                     │
│                                                         │
│ Metal: MTLDevice + MTLFeatureSets                      │
│  ├─ MTLFeatureSet.macOS_GPUFamily1_v2 ──┐             │
│  ├─ MTLFeatureSet.macOS_GPUFamily1_v3 ──┤─→ struct    │
│  └─ MTLFeatureSet.macOS_GPUFamily2_v1 ──┘             │
│                                                         │
│ OpenGL: Extension strings                             │
│  ├─ GL_ARB_compute_shader      ──┐                     │
│  ├─ GL_ARB_tessellation_shader ──┤─→ Features struct   │
│  └─ GL_EXT_texture_filter...   ──┘                     │
│                                                         │
└────────────┬────────────────────────────────────────────┘
             │
             ▼
┌─────────────────────────────────┐
│  Application Initialization     │
│  GraphicsCapabilities.Supports()│
└────────────┬────────────────────┘
             │
   ┌─────────┴─────────┐
   │                   │
   ▼                   ▼
┌──────────────┐   ┌──────────────┐
│ Feature A    │   │ Feature B    │
│ Supported?   │   │ Supported?   │
└────┬─────────┘   └────┬─────────┘
     │                  │
     ├─ YES             ├─ NO
     │                  │
     ▼                  ▼
┌────────────┐      ┌────────────┐
│ Use GPU    │      │ Use CPU    │
│ Path       │      │ Fallback   │
│ (Fast)     │      │ (Compatible)│
│            │      │            │
│ Compute    │      │ Fragment   │
│ Shader     │      │ Shader     │
│ Tessellation│      │ No Tess    │
└────────────┘      └────────────┘
```

---

## 6. Multi-Pass Rendering with Framebuffers

```
┌─────────────────────────────────────────────────────────┐
│              RENDER PIPELINE (Multiple Passes)          │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  ┌─────────────────────────────────────────────────┐   │
│  │  PASS 1: Shadow Map Generation                  │   │
│  ├─────────────────────────────────────────────────┤   │
│  │                                                 │   │
│  │  Framebuffer: ShadowMapFB                       │   │
│  │  ├─ DepthTarget: depthTexture (2048x2048)      │   │
│  │  └─ ColorTargets: (empty)                       │   │
│  │                                                 │   │
│  │  Pipeline: ShadowMapPipeline                    │   │
│  │  ├─ Vertex: position only                       │   │
│  │  └─ Fragment: none (depth only)                 │   │
│  │                                                 │   │
│  │  View: Light view matrix                        │   │
│  │  Render: All shadow casters                     │   │
│  │                                                 │   │
│  │  Output: depthTexture ──┐                       │   │
│  │                         │                       │   │
│  └─────────────────────────┼───────────────────────┘   │
│                            │                           │
│  ┌─────────────────────────┼───────────────────────┐   │
│  │  PASS 2: Scene Rendering                        │   │
│  ├─────────────────────────┼───────────────────────┤   │
│  │                         │                       │   │
│  │  Framebuffer: SceneFB                           │   │
│  │  ├─ ColorTarget: colorTexture (1920x1080)       │   │
│  │  └─ DepthTarget: depthTexture (1920x1080)       │   │
│  │                                                 │   │
│  │  Pipeline: ShadedObjectPipeline                 │   │
│  │  ├─ Uses: depthTexture from PASS 1 ◄────────┐  │   │
│  │  └─ Compares shadow depth                   │  │   │
│  │                                              │  │   │
│  │  View: Camera view matrix                    │  │   │
│  │  Render: All scene objects                   │  │   │
│  │                                              │  │   │
│  │  Output: colorTexture ──┐                    │  │   │
│  │                         │                    │  │   │
│  └─────────────────────────┼────────────────────┼──┘   │
│                            │                    │      │
│  ┌─────────────────────────┼────────────────────┼──┐   │
│  │  PASS 3: Post-Processing (e.g., Bloom)       │  │   │
│  ├─────────────────────────┼────────────────────┼──┤   │
│  │                         │                    │  │   │
│  │  Framebuffer: BloomFB                        │  │   │
│  │  ├─ ColorTarget: bloomTexture (1920x1080)    │  │   │
│  │  └─ DepthTarget: none                        │  │   │
│  │                                              │  │   │
│  │  Pipeline: BloomBlurPipeline                 │  │   │
│  │  ├─ Inputs: colorTexture from PASS 2 ◄──────┼──┘   │
│  │  └─ Outputs: bright areas                    │      │
│  │                                              │      │
│  │  Output: bloomTexture ──┐                    │      │
│  │                         │                    │      │
│  └─────────────────────────┼────────────────────┘      │
│                            │                           │
│  ┌─────────────────────────┼────────────────┐          │
│  │  PASS 4: Composition (Final)              │          │
│  ├─────────────────────────┼────────────────┤          │
│  │                         │                │          │
│  │  Framebuffer: SwapchainFB                │          │
│  │  ├─ ColorTarget: swapchain (1920x1080)   │          │
│  │  └─ DepthTarget: none                    │          │
│  │                                          │          │
│  │  Pipeline: CompositionPipeline           │          │
│  │  ├─ Inputs: colorTexture (PASS 2) ◄──────┘          │
│  │  ├─ Inputs: bloomTexture (PASS 3)                   │
│  │  └─ Blend bloom with color               │          │
│  │                                          │          │
│  │  Output: Displayed to screen             │          │
│  │                                          │          │
│  └──────────────────────────────────────────┘          │
│                                                         │
└─────────────────────────────────────────────────────────┘

TEXTURE DEPENDENCY GRAPH:
────────────────────────

depthTexture (PASS 1)
      │
      └─→ used in PASS 2
              │
              └─→ colorTexture
                      │
                      ├─→ used in PASS 3
                      │   └─→ bloomTexture
                      │           │
                      │           └─→ used in PASS 4
                      │
                      └─→ used in PASS 4

MEMORY LAYOUT:
──────────────

Frame Time:
│
├─ 0ms: PASS 1 (Shadow)
│   GPU write: depthTexture
│
├─ 2ms: PASS 2 (Scene)
│   GPU read: depthTexture
│   GPU write: colorTexture
│
├─ 4ms: PASS 3 (Bloom)
│   GPU read: colorTexture
│   GPU write: bloomTexture
│
├─ 6ms: PASS 4 (Composite)
│   GPU read: colorTexture, bloomTexture
│   GPU write: swapchain
│
└─ 8ms: Done, display frame
```

---

## 7. Dynamic Uniform Buffer Pattern

```
┌──────────────────────────────────────────────────────────┐
│           FRAME RENDERING WITH DYNAMIC UNIFORMS          │
├──────────────────────────────────────────────────────────┤
│                                                          │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Initialization (Once)                              │ │
│  ├────────────────────────────────────────────────────┤ │
│  │                                                    │ │
│  │ Create large buffer for 1000 objects:             │ │
│  │ ┌────────────────────────────────────────────┐    │ │
│  │ │ DynamicUniformBuffer (256KB)               │    │ │
│  │ │ Element size: 256 bytes (alignment)        │    │ │
│  │ │ Max elements: 1000                         │    │ │
│  │ │ Total: 256KB GPU memory (once allocated)   │    │ │
│  │ └────────────────────────────────────────────┘    │ │
│  │                                                    │ │
│  │ Create single ResourceSet:                        │ │
│  │ ┌────────────────────────────────────────────┐    │ │
│  │ │ ResourceLayout (DynamicBinding)            │    │ │
│  │ │ ResourceSet → points to DynamicBuffer      │    │ │
│  │ │ (Created once, reused forever)             │    │ │
│  │ └────────────────────────────────────────────┘    │ │
│  │                                                    │ │
│  └────────────────────────────────────────────────────┘ │
│           │                                              │
│           ▼                                              │
│  ┌────────────────────────────────────────────────────┐ │
│  │ Frame Rendering (Each Frame)                       │ │
│  ├────────────────────────────────────────────────────┤ │
│  │                                                    │ │
│  │ Each Object:                                       │ │
│  │ ┌────────────────────────────────────────────┐    │ │
│  │ │ Object 1:                                  │    │ │
│  │ │  uniform = {matrix, color, animState}     │    │ │
│  │ │  offset1 = AppendUniform(uniform) → 0     │    │ │
│  │ │  cl.SetGraphicsResourceSet(1, set, [0])   │    │ │
│  │ │  cl.Draw(obj1.vertexCount)                 │    │ │
│  │ ├────────────────────────────────────────────┤    │ │
│  │ │ Object 2:                                  │    │ │
│  │ │  uniform = {matrix, color, animState}     │    │ │
│  │ │  offset2 = AppendUniform(uniform) → 256   │    │ │
│  │ │  cl.SetGraphicsResourceSet(1, set, [256]) │    │ │
│  │ │  cl.Draw(obj2.vertexCount)                 │    │ │
│  │ ├────────────────────────────────────────────┤    │ │
│  │ │ Object 3:                                  │    │ │
│  │ │  uniform = {matrix, color, animState}     │    │ │
│  │ │  offset3 = AppendUniform(uniform) → 512   │    │ │
│  │ │  cl.SetGraphicsResourceSet(1, set, [512]) │    │ │
│  │ │  cl.Draw(obj3.vertexCount)                 │    │ │
│  │ ├────────────────────────────────────────────┤    │ │
│  │ │ ... (999 more objects)                     │    │ │
│  │ └────────────────────────────────────────────┘    │ │
│  │                                                    │ │
│  │ After all objects rendered:                       │ │
│  │ ┌────────────────────────────────────────────┐    │ │
│  │ │ buffer.Reset()  (offset counter → 0)       │    │ │
│  │ └────────────────────────────────────────────┘    │ │
│  │                                                    │ │
│  └────────────────────────────────────────────────────┘ │
│           │                                              │
│           ▼                                              │
│  IMPACT: 1000 objects = 1 ResourceSet created once       │
│          vs. 1000 ResourceSets (naive approach)          │
│          = 1000x allocation reduction                    │
│                                                          │
└──────────────────────────────────────────────────────────┘

GPU BUFFER MEMORY LAYOUT:
────────────────────────

Offset      Content             Used By
────────────────────────────────────────
0x00000     Object 1 uniform    Frame 1 draw
0x00100     Object 2 uniform    Frame 1 draw
0x00200     Object 3 uniform    Frame 1 draw
...
0x0FC00     Object 1000 uniform Frame 1 draw
────────────────────────────────────────
Frame N+1:
0x00000     Object 1 uniform    Frame N+1 draw (reused)
0x00100     Object 2 uniform    Frame N+1 draw (reused)
...
────────────────────────────────────────

SHADER ACCESS (dynamic offsets):
──────────────────────────────────

layout(binding = 0) uniform ObjectUniforms {
    mat4 worldMatrix;
    vec4 color;
    vec4 animState;
} object;

// When offset = 0:   access buffer[0..255]   (Object 1)
// When offset = 256: access buffer[256..511] (Object 2)
// GPU hardware automatically applies offset!
```

---

## 8. Backend Comparison: Resource Creation Flow

```
APPLICATION CODE: factory.CreateGraphicsPipeline(ref desc)
────────────────────────────────────────────────────────────

         │
         ▼
    ┌────────────────┐
    │ ResourceFactory│
    │ (Abstract)     │
    └────────┬───────┘
             │
    ┌────────┴────────┬──────────────┬──────────────┐
    │                 │              │              │
    ▼                 ▼              ▼              ▼
┌──────────┐    ┌──────────┐   ┌──────────┐   ┌──────────┐
│VkResource│    │D3D11Res. │   │MTLResource│   │OpenGLRes.│
│Factory   │    │Factory   │   │Factory    │   │Factory   │
└────┬─────┘    └────┬─────┘   └────┬─────┘   └────┬─────┘
     │              │              │              │
     ▼              ▼              ▼              ▼
┌─────────────────────────────────────────────────────────┐
│            Backend-Specific Implementation              │
├─────────────────────────────────────────────────────────┤
│                                                         │
│  VULKAN:                    DIRECT3D11:                 │
│  ──────────────────────     ─────────────────────       │
│  1. Compile SPIR-V shaders  1. Compile HLSL shaders     │
│  2. Create VkShaderModule   2. Create ID3D11Shader*     │
│  3. VkGraphicsPipelineCI    3. ID3D11InputLayout        │
│  4. VkPipelineLayout        4. ID3D11BlendState         │
│  5. vkCreateGraphicsPipe    5. ID3D11RasterizerState    │
│  6. Return VkPipeline       6. ID3D11DepthStencil       │
│                             7. Return D3D11Pipeline     │
│                                                         │
│  METAL:                     OPENGL:                     │
│  ─────────────────────      ─────────────────────       │
│  1. Compile Metal shaders   1. Compile GLSL shaders     │
│  2. Create MTLFunction      2. Create GL program        │
│  3. MTLRenderPipelineDesc   3. Create VAO + state       │
│  4. MTLDepthStencilDesc     4. Create texture units     │
│  5. createRenderPipeline()  5. Link and validate        │
│  6. Return MTLPipeline      6. Return OpenGLPipeline    │
│                                                         │
└─────────────────────────────────────────────────────────┘
             │                 │              │
             └─────────────────┴──────────────┘
                    │
                    ▼
      ┌─────────────────────────┐
      │ Return Veldrid Pipeline │
      │ (Abstract wrapper)      │
      └────────────┬────────────┘
                   │
                   ▼
      APPLICATION CODE receives
      unified Pipeline interface
      (use same way on all backends!)
```

---

## 9. Error Prevention Flow

```
API Call: factory.CreateResourceSet(ref description)
───────────────────────────────────────────────────────

         │
         ▼
    ┌────────────────────────────┐
    │ Validate ResourceSet       │
    │ against ResourceLayout     │
    ├────────────────────────────┤
    │                            │
    │ Check 1: Element Count     │
    │ ┌────────────────────────┐ │
    │ │ set.Resources.Length   │ │
    │ │   == layout.Elements   │ │
    │ │   .Length              │ │
    │ └────────────────────────┘ │
    │        │                   │
    │        ├─ ✓ PASS: Continue │
    │        └─ ✗ FAIL: Throw    │
    │                            │
    │ Check 2: Element Types     │
    │ ┌────────────────────────┐ │
    │ │ For each (set, layout) │ │
    │ │  pair:                 │ │
    │ │  set[i].Type           │ │
    │ │   == layout[i].Kind    │ │
    │ └────────────────────────┘ │
    │        │                   │
    │        ├─ ✓ PASS: Continue │
    │        └─ ✗ FAIL: Throw    │
    │           "Buffer where    │
    │            Texture expected"│
    │                            │
    │ Check 3: Buffer Size       │
    │ ┌────────────────────────┐ │
    │ │ If UniformBuffer:      │ │
    │ │  size % 16 == 0 ?      │ │
    │ └────────────────────────┘ │
    │        │                   │
    │        ├─ ✓ PASS: Continue │
    │        └─ ✗ FAIL: Throw    │
    │           "Must align      │
    │            to 16 bytes"    │
    └────────────────────────────┘
         │
         ▼
    ┌────────────────────┐
    │ All Checks Pass?   │
    └───┬────────┬───────┘
        │        │
       YES      NO
        │        │
        ▼        ▼
    ┌──────┐  ┌──────────┐
    │Create│  │Throw     │
    │Set   │  │Detailed  │
    └──────┘  │Exception │
              └──────────┘
```

---

**Document Version**: 1.0  
**Status**: Complete with all diagrams  
**Last Updated**: 12 de dezembro de 2025
