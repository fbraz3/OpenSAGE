# Veldrid Shader System: Architecture & Data Flow Diagrams

**Purpose**: Visual reference for shader compilation pipeline and resource lifecycle  
**Format**: ASCII diagrams and flowcharts  
**Date**: December 12, 2025

---

## 1. Shader Compilation Pipeline (Build Time → Runtime)

```
┌─────────────────────────────────────────────────────────────────┐
│                     BUILD TIME (MSBuild)                         │
└─────────────────────────────────────────────────────────────────┘

       GLSL/HLSL Source Files
       ├─ terrain.vert.glsl
       ├─ terrain.frag.glsl
       ├─ particle.comp.glsl
       └─ ... (18 shaders total)
              │
              ↓
    ┌─────────────────────────────┐
    │   glslangValidator          │
    │   (Windows/Linux/macOS)     │
    │   Offline Compilation       │
    └─────────────────────────────┘
              │
              ↓ (Pre-build step)
        SPIR-V Bytecode
        ├─ terrain.vert.spv       (2-10 KB)
        ├─ terrain.frag.spv       (5-20 KB)
        ├─ particle.comp.spv      (3-8 KB)
        └─ ... (18 SPIR-V files)
              │
              ↓ (MSBuild CompileShaders target)
    ┌─────────────────────────────┐
    │  Embed as Resources         │
    │  (DotNET Resource Embed)    │
    └─────────────────────────────┘
              │
              ↓
    ┌─────────────────────────────┐
    │   Assembly Binary           │
    │   OpenSage.Game.dll         │
    │   (Contains embedded SPIR-V)│
    └─────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                     RUNTIME (Veldrid Device)                     │
└─────────────────────────────────────────────────────────────────┘

    Game Start
       │
       ↓
    Assembly Load
    LoadEmbeddedResource("...terrain.vert.spv")
       │
       ↓ (byte[] SPIR-V data)
    ShaderSource Descriptor
    ┌──────────────────────────────┐
    │ Stage: Vertex                │
    │ SpirVBytes: [0x07, 0x23...]  │
    │ EntryPoint: "main"           │
    │ Specializations: null        │
    └──────────────────────────────┘
       │
       ↓
    ShaderCompilationCache
    .GetOrCompile(device, source)
       │
       ├─ Create ShaderSourceKey (hash)
       ├─ Lookup in _cache Dictionary
       │
       └─ (Cache miss) Continue...
              │
              ↓
    IGraphicsDevice.CreateShaderProgram()
       │
       ↓
    VeldridGraphicsDevice.CreateShader()
       ├─ Validate inputs
       ├─ Validate SPIR-V magic (0x07230203)
       ├─ Infer stage from name
       │
       ↓
    ResourceFactory.CreateFromSpirv()
    ┌────────────────────────────────────────┐
    │  Veldrid.SPIRV Cross-Compilation       │
    │  ┌─ Metal: SPIR-V → MSL                │
    │  ├─ Vulkan: Keep SPIR-V                │
    │  ├─ OpenGL: SPIR-V → GLSL              │
    │  ├─ D3D11: SPIR-V → HLSL               │
    │  └─ OpenGL ES: SPIR-V → GLSL ES        │
    │  (via SPIRV-Cross internally)          │
    └────────────────────────────────────────┘
       │
       ↓
    Veldrid.Shader (native object)
    ┌──────────────────────────────┐
    │ MSL (Metal)                  │
    │ GLSL (OpenGL)                │
    │ HLSL (Direct3D)              │
    │ SPIR-V (Vulkan)              │
    │ GLSL ES (Mobile)             │
    └──────────────────────────────┘
       │
       ↓
    VeldridShaderProgram Wrapper
    ┌──────────────────────────────┐
    │ .Native: Veldrid.Shader      │
    │ .Name: "terrain.vert"        │
    │ .EntryPoint: "main"          │
    └──────────────────────────────┘
       │
       ↓
    ResourcePool<VeldridShaderProgram>
    .Allocate(wrapper)
       │
       ↓
    Handle<IShaderProgram>
    ┌──────────────────────────────┐
    │ .Id: 42                      │
    │ .Generation: 3               │
    │ .IsValid: true               │
    └──────────────────────────────┘
       │
       ↓
    (Cached for reuse)
       │
       ↓
    Pipeline Creation
       │
       └─ Bind to graphics pipeline
```

---

## 2. Shader Stages & Type System

```
OpenSAGE ShaderStages Enum          Veldrid ShaderStages
┌──────────────────────────┐        ┌──────────────────────────┐
│ None            = 0x00   │        │ Vertex         = 0x01    │
│ Vertex          = 0x01   │◄─────► │ Fragment       = 0x10    │
│ Fragment        = 0x02   │        │ Compute        = 0x20    │
│ Compute         = 0x04   │        │ Geometry       = 0x08    │
│ Geometry        = 0x08   │        │ TessControl    = 0x02    │
│ TessControl     = 0x10   │        │ TessEval       = 0x04    │
│ TessEval        = 0x20   │        │                          │
└──────────────────────────┘        └──────────────────────────┘
         │
         └─ ToVeldridShaderStages()
            (Switch expression)

Typical Vertex+Fragment Pipeline
┌────────────────────────────────────────┐
│  Vertex Shader                         │
│  ┌────────────────────────────────┐   │
│  │ Input: Vertex attributes       │   │
│  │  • Position (vec3)             │   │
│  │  • Normal (vec3)               │   │
│  │  • TexCoord (vec2)             │   │
│  │                                │   │
│  │ Process: Transform + Lighting  │   │
│  │                                │   │
│  │ Output: Interpolated values    │   │
│  │  to rasterizer                 │   │
│  └────────────────────────────────┘   │
└────────────────────────────────────────┘
               ↓
       ┌──────────────────┐
       │   Rasterization  │
       │   (Fixed HW)     │
       └──────────────────┘
               ↓
┌────────────────────────────────────────┐
│  Fragment Shader (Pixel Shader)        │
│  ┌────────────────────────────────┐   │
│  │ Input: Interpolated vertex     │   │
│  │  data per pixel                │   │
│  │                                │   │
│  │ Process: Sample textures,      │   │
│  │  compute color                 │   │
│  │                                │   │
│  │ Output: Final color (RGBA)     │   │
│  │  to render target              │   │
│  └────────────────────────────────┘   │
└────────────────────────────────────────┘

Optional: Geometry Shader (between VS & FS)
┌────────────────────────────────────────┐
│  Geometry Shader                       │
│  ┌────────────────────────────────┐   │
│  │ Input: Primitives (triangles)  │   │
│  │                                │   │
│  │ Process: Generate new prims    │   │
│  │ (e.g., expand to particle)     │   │
│  │                                │   │
│  │ Output: Multiple primitives    │   │
│  └────────────────────────────────┘   │
│ Note: NOT supported on Metal backend   │
└────────────────────────────────────────┘

Optional: Tessellation Shaders
┌────────────────────────────────────────┐
│  Tessellation Control (Hull)           │
│  ┌────────────────────────────────┐   │
│  │ Input: Control points          │   │
│  │ Output: Tessellation levels    │   │
│  └────────────────────────────────┘   │
└────────────────────────────────────────┘
               ↓
       ┌──────────────────┐
       │  Tessellator HW  │
       │  (Subdivide mesh)│
       └──────────────────┘
               ↓
┌────────────────────────────────────────┐
│  Tessellation Evaluation (Domain)      │
│  ┌────────────────────────────────┐   │
│  │ Input: Subdivided vertices     │   │
│  │ Output: Final vertex pos/attrs │   │
│  └────────────────────────────────┘   │
│ Note: NOT supported on Metal backend   │
└────────────────────────────────────────┘

Standalone: Compute Shader (No Graphics Pipeline)
┌────────────────────────────────────────┐
│  Compute Shader                        │
│  ┌────────────────────────────────┐   │
│  │ Input: Work group dimensions   │   │
│  │  (X, Y, Z threads)             │   │
│  │                                │   │
│  │ Process: General GPU computing │   │
│  │  • Particle simulation         │   │
│  │  • FFT / GPGPU algorithms      │   │
│  │  • Texture generation          │   │
│  │                                │   │
│  │ Output: Buffers / Textures     │   │
│  └────────────────────────────────┘   │
└────────────────────────────────────────┘
```

---

## 3. Resource Lifecycle & Memory Management

```
Handle<IShaderProgram> Creation Path
┌──────────────────────────────────────┐
│  uint id = NextResourceId++          │
│  uint generation = 1                 │
└──────────────────────────────────────┘
            ↓
┌──────────────────────────────────────┐
│  Handle<IShaderProgram>              │
│  ┌────────────────────────────────┐  │
│  │ .Id: uint (unique per resource)│  │
│  │ .Generation: uint (validation) │  │
│  │ .IsValid: bool (id != 0xFFFF)  │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘

ResourcePool<VeldridShaderProgram> Allocation
┌──────────────────────────────────────┐
│  new ResourcePool<...>(256)          │
│  ┌────────────────────────────────┐  │
│  │ Capacity: 256 shaders          │  │
│  │ _items: Stack<T>               │  │
│  │ _handles: PoolHandle[]         │  │
│  │ _generation: uint[]            │  │
│  └────────────────────────────────┘  │
└──────────────────────────────────────┘
            ↓
┌──────────────────────────────────────┐
│  poolHandle = pool.Allocate(shader)  │
│  Returns: PoolHandle { Index, Gen }  │
└──────────────────────────────────────┘
            ↓
┌──────────────────────────────────────┐
│  Handle<IShaderProgram>(              │
│    poolHandle.Index,                 │
│    poolHandle.Generation)            │
└──────────────────────────────────────┘

Generational Validation Example
┌──────────────────────────────────────────┐
│  1. Create shader                        │
│     pool.Allocate(shader1)               │
│     → Handle { Id: 0, Gen: 1 }           │
│     → _generation[0] = 1                 │
└──────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────┐
│  2. Use shader (handle valid)            │
│     pool.TryGet(Handle { Id: 0, Gen: 1 })│
│     → _generation[0] == 1? YES ✓         │
│     → Return shader1                     │
└──────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────┐
│  3. Destroy shader                       │
│     pool.Release(Handle { Id: 0, Gen: 1 })│
│     → _generation[0]++ → 2               │
│     → shader1.Dispose()                  │
└──────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────┐
│  4. Reuse ID (new shader)                │
│     pool.Allocate(shader2)               │
│     → Handle { Id: 0, Gen: 2 }           │
│     → _generation[0] = 2                 │
└──────────────────────────────────────────┘
                 ↓
┌──────────────────────────────────────────┐
│  5. Old handle now invalid               │
│     pool.TryGet(Handle { Id: 0, Gen: 1 })│
│     → _generation[0] == 1? NO ✗          │
│     → Return null (use-after-free!)      │
└──────────────────────────────────────────┘

Disposal Hierarchy
┌────────────────────────────────────────┐
│  VeldridGraphicsDevice                  │
│  (extends DisposableBase)              │
│  ┌──────────────────────────────────┐  │
│  │ OnDispose(bool disposing)        │  │
│  │  ↓ _disposables.ForEach(d.Disp)  │  │
│  │                                  │  │
│  │ ├─ _bufferPool.Dispose()         │  │
│  │ │  ├─ _items.ForEach(Dispose)    │  │
│  │ │  └─ VeldridLib.DeviceBuffer[]  │  │
│  │ │                                │  │
│  │ ├─ _shaderPool.Dispose()  ◄─────┼──┤ (NEW)
│  │ │  ├─ _items.ForEach(Dispose)    │  │
│  │ │  └─ VeldridShaderProgram[]     │  │
│  │ │     └─ Veldrid.Shader[]        │  │
│  │ │                                │  │
│  │ ├─ _texturePool.Dispose()        │  │
│  │ ├─ _samplerPool.Dispose()        │  │
│  │ └─ _framebufferPool.Dispose()    │  │
│  │                                  │  │
│  └──────────────────────────────────┘  │
└────────────────────────────────────────┘
```

---

## 4. SPIR-V Cross-Compilation Details

```
Veldrid.SPIRV Runtime Compilation
┌─────────────────────────────────────────────────────┐
│  byte[] spirvBytecode (platform-independent IR)     │
│  ┌───────────────────────────────────────────────┐  │
│  │ Magic: 0x07230203 (SPIR-V identifier)         │  │
│  │ Version: 1.0 / 1.1 / 1.2 / 1.3 / 1.4 / 1.5   │  │
│  │ Generator: glslangValidator (0x00080001)      │  │
│  │ Bound: 1234 (resource ID upper limit)         │  │
│  │ Schema: 0                                      │  │
│  │ Instructions: [OpTypeFloat, OpTypeVector...]  │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
                      ↓
┌─────────────────────────────────────────────────────┐
│  Veldrid.ResourceFactory.CreateFromSpirv()          │
│  ┌───────────────────────────────────────────────┐  │
│  │ device.BackendType switch:                    │  │
│  └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
    │
    ├─ Metal (macOS) ──────┐
    │                       │
    ├─ Vulkan (Linux) ──┐   │
    │                   │   │
    ├─ OpenGL ─────┐    │   │
    │              │    │   │
    ├─ D3D11 ─┐    │    │   │
    │         │    │    │   │
    └─ OpenGL ES   │    │   │
              │    │    │   │
              ↓    ↓    ↓   ↓
         ┌────────────────────────────────────┐
         │  SPIRV-Cross Compilation Chains    │
         ├────────────────────────────────────┤
         │  Metal:   SPIR-V → SPIRV-Cross →  │
         │           MSL (Metal Shading Lang) │
         │                                    │
         │  Vulkan:  SPIR-V (pass-through)    │
         │           (zero-cost)              │
         │                                    │
         │  OpenGL:  SPIR-V → SPIRV-Cross →  │
         │           GLSL (deduce version)    │
         │                                    │
         │  D3D11:   SPIR-V → SPIRV-Cross →  │
         │           HLSL bytecode            │
         │                                    │
         │  OpenGL   SPIR-V → SPIRV-Cross →  │
         │  ES:      GLSL ES 3.10 / 3.20     │
         └────────────────────────────────────┘
              │
              ↓
         ┌────────────────────────────────────┐
         │  Backend-Specific Compilation      │
         ├────────────────────────────────────┤
         │  MSL  → Metal SDK compiler         │
         │  GLSL → GLSL shader compiler       │
         │  HLSL → Direct3D compiler          │
         │  SPIR-V → Vulkan loader (spirv)   │
         │  GLSL ES → ANGLE / Native ES      │
         └────────────────────────────────────┘
              │
              ↓
         ┌────────────────────────────────────┐
         │  Veldrid.Shader (Native Object)    │
         ├────────────────────────────────────┤
         │  .Handle: VkShaderModule (Vulkan)  │
         │           MTLFunction (Metal)      │
         │           GLuint (OpenGL)          │
         │           ID3D11PixelShader (D3D11)│
         │           ID3D11VertexShader       │
         │           etc.                     │
         │                                    │
         │  .Name: Resource name              │
         │  .Stage: ShaderStages (stage type) │
         └────────────────────────────────────┘

Backend-Specific Quirks
┌─────────────────────────────────────┐
│  Metal (macOS)                      │
│  ├─ No geometry shaders             │
│  ├─ No tessellation shaders         │
│  ├─ Requires function_constants     │
│  │  for specialization              │
│  ├─ MSL syntax differs from GLSL    │
│  └─ Veldrid handles internally ✓   │
│                                     │
│  Vulkan                             │
│  ├─ Native SPIR-V support           │
│  ├─ Requires SPIR-V 1.0+            │
│  ├─ Extensions checked at load      │
│  └─ Zero-cost path ✓               │
│                                     │
│  OpenGL                             │
│  ├─ No SPIR-V support               │
│  ├─ Requires GLSL conversion        │
│  ├─ Version bounds-checked          │
│  │  (GLSL 330 - 460)                │
│  └─ SPIRV-Cross generates ✓        │
│                                     │
│  Direct3D 11                        │
│  ├─ No SPIR-V support               │
│  ├─ Requires HLSL compilation       │
│  ├─ D3D SDK compiler (FXC)          │
│  └─ SPIRV-Cross + FXC pipeline ✓  │
└─────────────────────────────────────┘
```

---

## 5. Error Handling Flow

```
CreateShader() Error Flow
┌──────────────────────────┐
│  Input Validation        │
└──────────────────────────┘
    │
    ├─ name null/empty? ──┐
    │                     ↓
    │             ArgumentException
    │
    ├─ spirvData empty? ──┐
    │                     ↓
    │             ArgumentException
    │
    ├─ entryPoint null/empty? ──┐
    │                            ↓
    │                    ArgumentException
    │
    └─ SPIR-V magic invalid? ──┐
                                ↓
                        ArgumentException
                                │
                                ↓
┌──────────────────────────┐
│  CreateFromSpirv()       │
│  (Veldrid.SPIRV)         │
└──────────────────────────┘
    │
    ├─ Invalid bytecode? ──┐
    │                      ↓
    │          VeldridException
    │                      │
    │                      ↓
    │          ┌──────────────────────┐
    │          │ Catch & Re-wrap      │
    │          │ GraphicsException    │
    │          │ (with backend info)  │
    │          └──────────────────────┘
    │
    ├─ Backend not supported? ──┐
    │                           ↓
    │              VeldridException
    │                           │
    │                           ↓
    │          ┌──────────────────────┐
    │          │ GraphicsException    │
    │          │ "Feature not avail"  │
    │          │ on {BackendType}     │
    │          └──────────────────────┘
    │
    └─ Success? ──┐
                  ↓
    ┌──────────────────────────┐
    │  Wrap + Pool + Return    │
    │  Handle<IShaderProgram>  │
    └──────────────────────────┘

Exception Type Mapping
┌──────────────────────┬─────────────────────────────┐
│  Cause               │  Exception Type              │
├──────────────────────┼─────────────────────────────┤
│ Null/invalid args    │ ArgumentException           │
│ Bad SPIR-V magic     │ ArgumentException           │
│ Veldrid cross-comp   │ → GraphicsException         │
│  failure             │  (wraps VeldridException)   │
│ Feature unavailable  │ → GraphicsException         │
│  (e.g., tessellation)│  + backend info             │
│ Unexpected error     │ → GraphicsException         │
│ (programming bug)    │  (wraps original exception) │
└──────────────────────┴─────────────────────────────┘

Backend-Specific Messages
┌──────────────────────────────────────────┐
│  "Failed to compile shader on Metal:     │
│   SPIRV-Cross MSL generation failed.     │
│   Check SPIR-V bytecode validity."       │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│  "Failed to compile shader on OpenGL:    │
│   GLSL version may be unsupported.       │
│   Required: GLSL 330+, Available: 150"   │
└──────────────────────────────────────────┘

┌──────────────────────────────────────────┐
│  "Shader compilation failed on Vulkan:   │
│   SPIR-V validation error.               │
│   Check SPIR-V extensions required."     │
└──────────────────────────────────────────┘
```

---

## 6. Cache Hit/Miss Behavior

```
ShaderCompilationCache Workflow
┌──────────────────────────────────┐
│  GetOrCompile(device, source)    │
└──────────────────────────────────┘
    │
    ↓
┌──────────────────────────────────┐
│  Create ShaderSourceKey(source)  │
│  Hash:                           │
│   • Stage (enum value)           │
│   • EntryPoint (string)          │
│   • SPIR-V bytes (sampled)       │
│   • Specializations (list)       │
└──────────────────────────────────┘
    │
    ↓
┌──────────────────────────────────┐
│  Lookup in _cache Dictionary     │
│  if (_cache.TryGetValue(key))    │
└──────────────────────────────────┘
    │
    ├─ CACHE HIT (found) ──┐
    │                      ↓
    │         ┌────────────────────────┐
    │         │ Return cached shader   │
    │         │ (zero allocation cost) │
    │         │ Fast path (typically   │
    │         │  < 1 microsecond)      │
    │         └────────────────────────┘
    │
    └─ CACHE MISS (not found) ──┐
                                │
                                ↓
                   ┌────────────────────────────┐
                   │ device.CreateShader()      │
                   │ (cross-compile + wrap)     │
                   │ Slow path (typically       │
                   │  1-50 milliseconds)        │
                   └────────────────────────────┘
                                │
                                ↓
                   ┌────────────────────────────┐
                   │ Store in cache             │
                   │ _cache[key] = shader       │
                   └────────────────────────────┘
                                │
                                ↓
                   ┌────────────────────────────┐
                   │ Return newly compiled      │
                   └────────────────────────────┘

Typical Cache Behavior (Real Game)
┌──────────────────────────────────┐
│  Frame 1:                        │
│  Load terrain shader × 2 Miss    │
│  Load particle shader × 1 Miss   │
│  Cache: 3 entries               │
└──────────────────────────────────┘
    │
    ↓ (Frame 2-60:)
┌──────────────────────────────────┐
│  Same shaders requested          │
│  × 59 HITS                       │
│  + new sky shader × 1 Miss       │
│  Cache: 4 entries (cumulative)   │
└──────────────────────────────────┘
    │
    ↓ (Post-game shutdown:)
┌──────────────────────────────────┐
│  cache.Dispose()                 │
│  → Dispose all cached shaders    │
│  → Clear _cache Dictionary       │
│  → GPU memory released           │
└──────────────────────────────────┘
```

---

## 7. Integration Points in VeldridGraphicsDevice

```
VeldridGraphicsDevice Class Structure
┌────────────────────────────────────────────────┐
│  VeldridGraphicsDevice : DisposableBase        │
│  (extends: IGraphicsDevice)                    │
├────────────────────────────────────────────────┤
│                                                │
│  Fields (Resource Pools):                      │
│  ├─ _device: Veldrid.GraphicsDevice           │
│  ├─ _cmdList: Veldrid.CommandList             │
│  ├─ _bufferPool: ResourcePool<Buffer>         │
│  ├─ _texturePool: ResourcePool<Texture>       │
│  ├─ _samplerPool: ResourcePool<Sampler>       │
│  ├─ _framebufferPool: ResourcePool<FB>        │
│  ├─ _shaderPool: ResourcePool<ShaderProg>◄── NEW
│  └─ _pipelinePool: ResourcePool<Pipeline>     │
│                                                │
│  Methods (Shader Operations):                 │
│  ├─ CreateShader(name, spv, entry)            │
│  │  ├─ ValidateInputs()                       │
│  │  ├─ ValidateSpirVMagic()                   │
│  │  ├─ InferShaderStage()                     │
│  │  ├─ factory.CreateFromSpirv() ──Veldrid   │
│  │  ├─ new VeldridShaderProgram()             │
│  │  ├─ pool.Allocate()                        │
│  │  └─ return Handle<IShaderProgram>          │
│  ├─ DestroyShader(handle)                     │
│  └─ GetShader(handle)                         │
│                                                │
│  Methods (Existing):                          │
│  ├─ CreateBuffer()                            │
│  ├─ CreateTexture()                           │
│  ├─ CreateSampler()                           │
│  ├─ CreateFramebuffer()                       │
│  ├─ BeginFrame() / EndFrame()                 │
│  └─ SetRenderTarget() / SetPipeline()         │
│                                                │
└────────────────────────────────────────────────┘
```

---

**End of Diagrams**
