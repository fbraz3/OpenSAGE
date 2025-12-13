# Phase 5: BGFX Parallel Implementation (Weeks 26-35)

## Executive Summary

**Decision**: Implement BGFX backend in parallel with Veldrid
- **Timeline**: 10 weeks (Weeks 26-35)
- **Approach**: Parallel implementation with BGFX as primary, Veldrid as fallback
- **Architecture**: Factory pattern for backend selection via `GraphicsBackendType` enum
- **Goal**: Game runs on all platforms (macOS Metal, Windows D3D11, Linux Vulkan) without Veldrid dependencies
- **Status**: Planning complete, ready for Week 26 Phase A implementation

---

## Phase Structure

```
Phase 5 (Weeks 26-35): BGFX Parallel Implementation
├─ Phase 5A (Weeks 26-27): Foundation & Library Integration [NEXT]
├─ Phase 5B (Weeks 28-30): Core Graphics Implementation
├─ Phase 5C (Weeks 31-32): Engine Integration & Deprecation
├─ Phase 5D (Weeks 33-35): Validation, Optimization & Release
└─ Post-Phase 5: Veldrid removal (Week 36+, optional based on stability)
```

---

## Phase 5A: Foundation & Library Integration (Weeks 26-27)

### Objectives
1. Acquire BGFX native libraries for all platforms
2. Create P/Invoke bindings wrapper
3. Implement platform initialization (Metal on macOS)
4. Create `BgfxGraphicsDevice` skeleton implementing `IGraphicsDevice`
5. Verify game initializes with blank BGFX window
6. **Success Criteria**: Game boots with `--renderer bgfx` flag showing blank window on macOS

### Week 26: BGFX Library Setup & P/Invoke Bindings

#### Days 1-2: Library Acquisition (8 hours)
**Tasks**:
1. Download/build BGFX binaries for all platforms:
   - macOS: arm64 + x86_64 (Metal backend)
   - Windows: x64 (D3D11 backend)
   - Linux: x64 (Vulkan backend)
2. Create directory structure: `lib/bgfx/[platform]/[arch]/`
3. Place `.dylib`, `.dll`, `.so` files in appropriate directories
4. Create `.gitignore` for binaries (or add to LFS if large)
5. Verify file integrity and library exports

**Deliverables**:
- ✅ BGFX native libraries in `lib/bgfx/`
- ✅ Updated `.gitignore` or LFS configuration
- ✅ Script to auto-download binaries on first build
- ✅ Documentation in `docs/BGFX_SETUP.md`

**Tests**:
- Verify library files are readable
- Check library exports with `nm` / `dumpbin`
- Confirm MD5 checksums match official BGFX builds

---

#### Days 3-5: P/Invoke Bindings Creation (16 hours)
**File**: `src/OpenSage.Graphics/BGFX/Native/bgfx.cs` (~2000 lines)

**Tasks**:
1. Create P/Invoke bindings for essential BGFX APIs:
   ```csharp
   // Initialization
   extern "C" bgfx_init_ctor_t bgfx_init_ctor();
   extern "C" void bgfx_init(bgfx_init_t* init);
   extern "C" void bgfx_shutdown();
   extern "C" bgfx_caps_t* bgfx_get_caps();
   
   // Frame submission
   extern "C" uint32_t bgfx_frame(bool capture);
   extern "C" void bgfx_render_frame(int32_t msecs);
   
   // Rendering
   extern "C" bgfx_encoder_t* bgfx_encoder_begin(bool forThread);
   extern "C" void bgfx_encoder_end(bgfx_encoder_t* encoder);
   
   // Resources
   extern "C" bgfx_vertex_layout_t* bgfx_vertex_layout_begin(bgfx_vertex_layout_t* layout, bgfx_renderer_type_t type);
   extern "C" void bgfx_vertex_layout_add(bgfx_vertex_layout_t* layout, bgfx_attrib_t attrib, uint8_t num, bgfx_attrib_type_t type, bool normalized, bool asInt);
   extern "C" bgfx_buffer_handle_t bgfx_create_vertex_buffer(const bgfx_memory_t* mem, const bgfx_vertex_layout_t* layout, uint16_t flags);
   extern "C" bgfx_index_buffer_handle_t bgfx_create_index_buffer(const bgfx_memory_t* mem, uint16_t flags);
   extern "C" bgfx_texture_handle_t bgfx_create_texture_2d(uint16_t width, uint16_t height, bool hasMips, uint16_t numLayers, bgfx_texture_format_t format, uint64_t flags, const bgfx_memory_t* mem);
   extern "C" bgfx_shader_handle_t bgfx_create_shader(const bgfx_memory_t* mem);
   extern "C" bgfx_program_handle_t bgfx_create_program(bgfx_shader_handle_t vsh, bgfx_shader_handle_t fsh, bool destroyShaders);
   extern "C" bgfx_framebuffer_handle_t bgfx_create_framebuffer(uint16_t width, uint16_t height, bgfx_texture_format_t format, uint64_t textureFlags);
   
   // Destruction
   extern "C" void bgfx_destroy_vertex_buffer(bgfx_vertex_buffer_handle_t handle);
   extern "C" void bgfx_destroy_index_buffer(bgfx_index_buffer_handle_t handle);
   extern "C" void bgfx_destroy_texture(bgfx_texture_handle_t handle);
   extern "C" void bgfx_destroy_shader(bgfx_shader_handle_t handle);
   extern "C" void bgfx_destroy_program(bgfx_program_handle_t handle);
   extern "C" void bgfx_destroy_framebuffer(bgfx_framebuffer_handle_t handle);
   ```

2. Create enums and structs:
   - `bgfx_renderer_type_t` (Metal, Vulkan, D3D11, OpenGL, etc.)
   - `bgfx_texture_format_t` (RGBA8, BGRA8, RG32F, etc.)
   - `bgfx_attrib_t` (Position, Normal, TexCoord0-3, etc.)
   - `bgfx_attrib_type_t` (Uint8, Int16, Float, etc.)
   - `bgfx_init_t`, `bgfx_caps_t`, etc.

3. Create utility structs:
   - `bgfx_memory_t` wrapper for memory allocation
   - `bgfx_platform_data_t` for platform-specific initialization
   - Handle structs with proper validation

**Deliverables**:
- ✅ Complete `bgfx.cs` with 50+ P/Invoke declarations
- ✅ All essential enums and structs
- ✅ Unit tests for P/Invoke marshalling (10 tests)
- ✅ Documentation of each API section

**Tests**:
- DllImport resolution tests
- Struct marshalling tests
- Handle validation tests
- Platform detection tests

---

#### Days 6-7: Platform Data & Window Integration (12 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxPlatformData.cs` (~200 lines)

**Tasks**:
1. Create platform-specific initialization:
   ```csharp
   public static class BgfxPlatformData
   {
       public static bgfx_platform_data_t GetPlatformData(IntPtr windowHandle, IntPtr displayHandle)
       {
           if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
               return GetMacOSData(windowHandle);
           else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
               return GetWindowsData(windowHandle);
           else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
               return GetLinuxData(windowHandle, displayHandle);
       }
       
       private static bgfx_platform_data_t GetMacOSData(IntPtr windowHandle)
       {
           // Extract NSWindow* and NSView* from SDL window
           // Set Metal-specific initialization
       }
   }
   ```

2. Integrate with SDL2 window (via Veldrid.Sdl2)
3. Handle platform-specific memory allocation
4. Verify Metal is selected on macOS

**Deliverables**:
- ✅ Platform data initialization for 3 platforms
- ✅ SDL2 window integration
- ✅ Unit tests for platform detection (6 tests)
- ✅ Integration test with actual SDL2 window

**Tests**:
- Platform detection tests
- Window handle extraction tests
- Metal backend selection verification

---

### Week 27: BgfxGraphicsDevice Skeleton & Initialization

#### Days 1-3: Core Device Implementation (16 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxGraphicsDevice.cs` (~500 lines)

**Tasks**:
1. Implement `BgfxGraphicsDevice : IGraphicsDevice`:
   ```csharp
   public class BgfxGraphicsDevice : IGraphicsDevice
   {
       private bgfx_init_t _initSettings;
       private IntPtr _windowHandle;
       private bool _initialized = false;
       
       public BgfxGraphicsDevice(IntPtr windowHandle, GraphicsDeviceOptions options)
       {
           _windowHandle = windowHandle;
           InitializeBGFX(options);
       }
       
       private void InitializeBGFX(GraphicsDeviceOptions options)
       {
           var initSettings = GetInitSettings(options);
           bgfx_init(&initSettings);
           _initialized = true;
       }
       
       public void RenderFrame(Action<ICommandList> renderCallback)
       {
           var encoder = bgfx_encoder_begin(false);
           var commandList = new BgfxCommandList(encoder);
           renderCallback(commandList);
           bgfx_encoder_end(encoder);
           bgfx_frame(false);
       }
       
       public void Dispose() => bgfx_shutdown();
   }
   ```

2. Implement basic resource management stubs:
   - `CreateBuffer()` → returns dummy Handle
   - `CreateTexture()` → returns dummy Handle
   - `CreateShader()` → returns dummy Handle
   - All actual implementation deferred to Phase 5B

3. Implement frame submission:
   - `RenderFrame(Action<ICommandList> callback)`
   - Encoder creation, frame submission, cleanup

4. Handle lifecycle events:
   - Initialization
   - Shutdown
   - Window resize handling

**Deliverables**:
- ✅ BgfxGraphicsDevice implementation (400-500 lines)
- ✅ Stub implementations of all IGraphicsDevice methods
- ✅ Frame submission pipeline working
- ✅ Unit tests (15 tests)

**Tests**:
- Device initialization tests
- Frame submission tests
- Lifecycle tests
- Error handling tests

---

#### Days 4-5: Command List Interface (12 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxCommandList.cs` (~300 lines)

**Tasks**:
1. Implement `BgfxCommandList : ICommandList`:
   ```csharp
   public class BgfxCommandList : ICommandList
   {
       private readonly bgfx_encoder_t* _encoder;
       
       public void Clear(uint targetFlags, uint color, float depth, byte stencil)
       {
           // Map to bgfx_encoder_clear
       }
       
       public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
       {
           // Map to bgfx_encoder_set_view_frame_buffer
       }
       
       public void SetViewport(Viewport vp)
       {
           // Map to bgfx_encoder_set_view_rect
       }
       
       // All other ICommandList methods with stubs
   }
   ```

2. Map ICommandList methods to encoder calls
3. Track state (current RT, viewport, scissor)
4. Defer actual rendering to Phase 5B

**Deliverables**:
- ✅ BgfxCommandList implementation
- ✅ Encoder API mapping
- ✅ State tracking
- ✅ Unit tests (12 tests)

---

#### Days 6-7: Backend Switching Infrastructure (12 hours)
**File**: `src/OpenSage.Graphics/GraphicsDeviceFactory.cs` (update existing)

**Tasks**:
1. Add `GraphicsBackendType` enum:
   ```csharp
   public enum GraphicsBackendType
   {
       Veldrid = 0,  // Legacy/fallback
       BGFX = 1,     // Primary (default)
   }
   ```

2. Update factory to support both:
   ```csharp
   public static IGraphicsDevice CreateGraphicsDevice(
       IntPtr windowHandle,
       GraphicsBackendType backend,
       GraphicsDeviceOptions options)
   {
       return backend switch
       {
           GraphicsBackendType.BGFX => 
               new BgfxGraphicsDevice(windowHandle, options),
           GraphicsBackendType.Veldrid => 
               new VeldridGraphicsDeviceAdapter(windowHandle, options),
           _ => throw new ArgumentException(...)
       };
   }
   ```

3. Add command-line flag handling:
   - `--renderer bgfx` → Use BGFX
   - `--renderer veldrid` → Use Veldrid (fallback)
   - Default: BGFX

4. Add environment variable:
   - `OPENSAGE_BACKEND=bgfx|veldrid`

**Deliverables**:
- ✅ Backend selection infrastructure
- ✅ Command-line flag parsing
- ✅ Environment variable support
- ✅ Unit tests (8 tests)

---

### Phase 5A Success Criteria

**Go/No-Go Gate**:
```
✅ Game initializes with `--renderer bgfx` flag
✅ BGFX window appears (Metal on macOS, D3D11 on Windows, Vulkan on Linux)
✅ Window is responsive (can resize, close)
✅ No crashes or exceptions
✅ Build: 0 errors, <15 warnings
✅ All 60+ Phase 5A tests passing
✅ Frame rate stable at 60 FPS
```

**Fallback Plan**:
If BGFX initialization fails:
- Game automatically falls back to Veldrid
- Logs warning: "BGFX initialization failed, using Veldrid fallback"
- User can explicitly force backend: `--renderer veldrid`

**Go Decision**: ✅ PROCEED TO PHASE 5B
- If blank window displays correctly with BGFX
- If performance is acceptable (60+ FPS)
- If no critical bugs found

---

## Phase 5B: Core Graphics Implementation (Weeks 28-30)

### Objectives
1. Implement buffer/texture/framebuffer management
2. Implement shader compilation pipeline
3. Implement render pass/view system
4. Create pipeline state mapping
5. Implement basic rendering (triangle + texture)
6. **Success Criteria**: Triangle with texture renders correctly

### Week 28: Resource Management (Buffers, Textures, Samplers)

#### Days 1-2: Buffer Management (12 hours)
**Files**: 
- `src/OpenSage.Graphics/BGFX/BgfxResourceManager.cs` (400+ lines)
- `src/OpenSage.Graphics/BGFX/BgfxBuffer.cs` (100 lines)

**Tasks**:
1. Implement buffer creation/destruction:
   ```csharp
   public Handle<IBuffer> CreateBuffer(BufferDescription description, IntPtr data)
   {
       var layout = new bgfx_vertex_layout_t();
       bgfx_vertex_layout_begin(&layout, _renderer);
       
       // Add vertex attributes based on description.VertexElementCount
       for (int i = 0; i < description.VertexElements.Length; i++)
       {
           bgfx_vertex_layout_add(&layout, 
               MapAttrib(description.VertexElements[i].Semantic),
               description.VertexElements[i].Count,
               MapAttribType(description.VertexElements[i].Type),
               false, false);
       }
       
       bgfx_vertex_layout_end(&layout);
       
       var memory = bgfx_copy(data, description.SizeInBytes);
       var handle = bgfx_create_vertex_buffer(memory, &layout, 0);
       
       // Store in dictionary
       _buffers[handle.idx] = new BgfxBuffer { Handle = handle, Size = description.SizeInBytes };
       
       return new Handle<IBuffer>(handle.idx, handle.generation);
   }
   ```

2. Implement index buffer creation
3. Implement buffer destruction and pooling
4. Implement buffer updates (mapping)

**Deliverables**:
- ✅ Buffer creation/destruction (12 tests)
- ✅ Index buffer support (6 tests)
- ✅ Buffer pooling (8 tests)
- ✅ Memory management tests (6 tests)

---

#### Days 3-4: Texture Management (12 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxTexture.cs` (150 lines)

**Tasks**:
1. Implement 2D texture creation:
   ```csharp
   public Handle<ITexture> CreateTexture(TextureDescription description, IntPtr data)
   {
       var format = MapTextureFormat(description.Format);
       var flags = MapTextureFlags(description.Usage);
       
       bgfx_memory_t* memory = null;
       if (data != IntPtr.Zero)
           memory = bgfx_copy(data, description.SizeInBytes);
       
       var handle = bgfx_create_texture_2d(
           description.Width,
           description.Height,
           description.MipLevels > 1,
           description.Depth,
           format,
           flags,
           memory);
       
       _textures[handle.idx] = new BgfxTexture { Handle = handle };
       return new Handle<ITexture>(handle.idx, handle.generation);
   }
   ```

2. Implement sampler state creation
3. Implement texture destruction
4. Implement format mapping (RGBA8, etc.)

**Deliverables**:
- ✅ Texture creation/destruction (10 tests)
- ✅ Sampler creation (8 tests)
- ✅ Format mapping tests (8 tests)
- ✅ Mipmap support tests (4 tests)

---

#### Days 5-6: Framebuffer Management (12 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxFramebuffer.cs` (150 lines)

**Tasks**:
1. Implement framebuffer creation with attachments
2. Implement depth/stencil attachments
3. Implement framebuffer destruction
4. Handle framebuffer resizing

**Deliverables**:
- ✅ Framebuffer creation (8 tests)
- ✅ Multi-attachment support (6 tests)
- ✅ Depth/stencil tests (4 tests)
- ✅ Framebuffer resizing (4 tests)

---

#### Day 7: Resource Manager Integration (8 hours)

**Deliverables**:
- ✅ Unified resource manager (200 lines)
- ✅ Handle validation
- ✅ Resource lifecycle management
- ✅ Integration tests (12 tests)

---

### Week 29: Shader Compilation & Pipeline State

#### Days 1-3: Shader Compilation Pipeline (18 hours)
**Files**:
- `src/OpenSage.Graphics/BGFX/BgfxShaderCompiler.cs` (500+ lines)
- `docs/BGFX_Shader_Compilation.md` (new documentation)

**Tasks**:
1. Implement offline GLSL → BGFX compilation:
   ```csharp
   public class BgfxShaderCompiler
   {
       public static byte[] CompileShader(
           string glslSource,
           ShaderStage stage,
           bgfx_renderer_type_t platform)
       {
           // 1. Write GLSL to temp file
           var glslFile = Path.GetTempFileName();
           File.WriteAllText(glslFile, glslSource);
           
           // 2. Run shaderc command-line tool
           var outputFile = Path.GetTempFileName();
           var args = $"-f \"{glslFile}\" -o \"{outputFile}\" --type {MapStageToString(stage)} --platform {MapPlatformToString(platform)}";
           var process = Process.Start("shaderc", args);
           process.WaitForExit();
           
           // 3. Read compiled binary
           var bytecode = File.ReadAllBytes(outputFile);
           
           // 4. Cleanup
           File.Delete(glslFile);
           File.Delete(outputFile);
           
           return bytecode;
       }
   }
   ```

2. Implement shader caching (compile once, reuse)
3. Handle shader compilation errors
4. Create shader compilation tests with real .glsl files

**Deliverables**:
- ✅ Offline compilation pipeline (300+ lines)
- ✅ Shader caching (100 lines)
- ✅ Error handling (50 lines)
- ✅ Compilation tests (15 tests)
- ✅ Documentation of workflow

---

#### Days 4-6: Pipeline State Mapping (16 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxPipelineState.cs` (250 lines)

**Tasks**:
1. Implement raster state mapping:
   ```csharp
   public static class BgfxStateMapping
   {
       public static ulong MapRasterState(RasterStateDescription state)
       {
           var flags = BGFX_STATE_DEFAULT;
           
           // CullMode
           flags |= state.CullMode switch
           {
               FaceCullMode.None => 0,
               FaceCullMode.Front => BGFX_STATE_CULL_CCW,
               FaceCullMode.Back => BGFX_STATE_CULL_CW,
           };
           
           // FillMode
           flags |= state.FillMode switch
           {
               PolygonFillMode.Solid => 0,
               PolygonFillMode.Wireframe => BGFX_STATE_WIREFRAME,
           };
           
           return flags;
       }
   }
   ```

2. Implement depth/stencil state mapping
3. Implement blend state mapping
4. Create comprehensive mapping tests (20+ tests)

**Deliverables**:
- ✅ Raster state mapping (50 lines)
- ✅ Depth/stencil mapping (50 lines)
- ✅ Blend state mapping (50 lines)
- ✅ State mapping tests (20+ tests)

---

#### Day 7: Pipeline Creation (10 hours)

**Deliverables**:
- ✅ BgfxPipeline implementation (150 lines)
- ✅ Pipeline caching
- ✅ Pipeline creation tests (12 tests)

---

### Week 30: Rendering Operations & Basic Tests

#### Days 1-2: Render Pass System (12 hours)
**File**: `src/OpenSage.Graphics/BGFX/BgfxViewManager.cs` (200+ lines)

**Tasks**:
1. Implement BGFX view system (0-255 views):
   ```csharp
   public class BgfxViewManager
   {
       private const int MaxViews = 256;
       private BgfxView[] _views = new BgfxView[MaxViews];
       private int _nextViewId = 0;
       
       public int AllocateView(string name, Rect viewport, Handle<IFramebuffer> framebuffer)
       {
           var viewId = _nextViewId++;
           
           bgfx_set_view_name(viewId, name);
           bgfx_set_view_rect(viewId, viewport.X, viewport.Y, viewport.Width, viewport.Height);
           bgfx_set_view_framebuffer(viewId, _framebuffers[framebuffer.Id]);
           
           _views[viewId] = new BgfxView { Id = viewId, Name = name };
           return viewId;
       }
       
       public void Submit(int viewId, Handle<IPipeline> pipeline, Handle<IVertexBuffer> vertices)
       {
           bgfx_set_state(GetPipelineState(pipeline));
           bgfx_set_vertex_buffer(vertices);
           bgfx_submit(viewId, GetProgram(pipeline));
       }
   }
   ```

2. Map OpenSAGE render passes to views:
   - Shadow pass → View 0
   - Forward pass → View 1
   - Post-processing → View 2
   - 2D UI → View 3

3. Implement draw call submission

**Deliverables**:
- ✅ View manager (200 lines)
- ✅ View lifecycle management
- ✅ Draw submission pipeline
- ✅ Tests (15 tests)

---

#### Days 3-5: Draw Operations (16 hours)

**Tasks**:
1. Implement draw calls:
   - `DrawIndexed()`
   - `DrawVertices()`
   - `DrawIndexedIndirect()`
   - `DrawVerticesIndirect()`

2. Implement state binding:
   - `SetRenderTarget()`
   - `SetViewport()`
   - `SetScissor()`
   - `BindVertexBuffer()`
   - `BindIndexBuffer()`

3. Implement clear operations:
   - `ClearRenderTarget()`
   - `ClearDepth()`

**Deliverables**:
- ✅ All draw operations (150 lines)
- ✅ State binding (100 lines)
- ✅ Tests (20+ tests)

---

#### Days 6-7: Basic Rendering Test (12 hours)
**File**: `src/OpenSage.Game.Tests/Graphics/BGFX/BasicRenderingTests.cs`

**Tasks**:
1. Create triangle mesh:
   ```csharp
   [Test]
   public void TestDrawTriangle()
   {
       // 1. Create vertex buffer
       var vertices = new[] {
           new Vector3(0, 0.5f, 0),
           new Vector3(-0.5f, -0.5f, 0),
           new Vector3(0.5f, -0.5f, 0),
       };
       
       var vb = _device.CreateBuffer(CreateVertexBufferDesc(vertices));
       
       // 2. Create basic shader
       var shader = _device.CreateShader(shaderSource, ShaderStage.Vertex);
       
       // 3. Create pipeline
       var pipeline = _device.CreatePipeline(PipelineDescription.Default);
       
       // 4. Submit draw call
       _device.RenderFrame(cmd =>
       {
           cmd.SetRenderTarget(backBuffer);
           cmd.SetViewport(new Viewport(0, 0, 800, 600));
           cmd.Clear(ClearTargets.Color | ClearTargets.Depth, 0xFF000000, 1.0f, 0);
           cmd.SetPipeline(pipeline);
           cmd.BindVertexBuffer(vb);
           cmd.Draw(3);
       });
       
       // 5. Verify triangle was rendered
       VerifyFramebufferContainsTriangle();
   }
   ```

2. Create textured quad test
3. Verify rendering output with framebuffer capture

**Deliverables**:
- ✅ Triangle rendering test
- ✅ Textured quad test
- ✅ Framebuffer capture utilities
- ✅ Visual verification tests (3 tests)

---

### Phase 5B Success Criteria

**Go/No-Go Gate**:
```
✅ Basic triangle renders correctly
✅ Textured quad renders with proper UV mapping
✅ 60+ FPS performance on all platforms
✅ No memory leaks (verified with profiler)
✅ Shader compilation working (offline pipeline)
✅ Pipeline state mapping complete
✅ 100+ Phase 5B tests passing
✅ Build: 0 errors, <15 warnings
```

**Performance Baseline**:
- Triangle: 0.1ms GPU time
- Textured quad: 0.15ms GPU time
- Frame submission: <0.5ms CPU time

**Go Decision**: ✅ PROCEED TO PHASE 5C
- If visual output matches Veldrid reference
- If performance is acceptable
- If all resource management is stable

---

## Phase 5C: Engine Integration & Veldrid Deprecation (Weeks 31-32)

### Objectives
1. Refactor RenderPipeline to use BGFX views
2. Update all ShaderResources classes (5 files)
3. Update Scene3D rendering integration
4. Remove Veldrid from critical paths
5. Maintain Veldrid as fallback option
6. **Success Criteria**: All game systems render correctly with BGFX

### Week 31: RenderPipeline & Multi-Pass Rendering

#### Days 1-4: RenderPipeline Refactoring (24 hours)
**File**: `src/OpenSage.Game/Graphics/RenderPipeline.cs` (refactor existing, ~800 lines)

**Current Structure**:
- Shadow pass → RenderShadowMap()
- Forward pass → RenderScene()
- Water pass → RenderWater()
- Post-pass → RenderPostProcess()
- 2D overlay → Render2D()

**Refactoring Tasks**:
1. Map passes to BGFX views:
   ```csharp
   public class RenderPipeline
   {
       private int _viewShadow = 0;      // Shadow depth pass
       private int _viewForward = 1;     // Main forward rendering
       private int _viewWater = 2;       // Water reflection/refraction
       private int _view2D = 3;          // 2D UI and effects
       
       public void RenderFrame(ICommandList cmd, IGameLogic gameLogic)
       {
           // 1. Shadow pass
           cmd.SetRenderTarget(_shadowFramebuffer);
           RenderShadowPass(cmd, gameLogic);
           
           // 2. Forward pass
           cmd.SetRenderTarget(_mainFramebuffer);
           RenderForwardPass(cmd, gameLogic);
           
           // 3. Water pass (if water present)
           if (HasWater(gameLogic))
           {
               cmd.SetRenderTarget(_waterFramebuffer);
               RenderWaterPass(cmd, gameLogic);
           }
           
           // 4. Post-processing (tone mapping, bloom, etc.)
           RenderPostProcess(cmd);
           
           // 5. 2D UI overlay
           Render2D(cmd, gameLogic.UIState);
       }
   }
   ```

2. Implement shadow pass with BGFX:
   - Create shadow framebuffer (2048x2048 depth)
   - Render scene from light POV
   - Bind shadow map as texture in forward pass

3. Implement forward rendering:
   - Render opaque geometry
   - Bind shadow map
   - Apply lighting and materials

4. Implement water rendering (if water system exists)

5. Implement post-processing:
   - Tone mapping
   - Bloom (if configured)
   - Color grading

6. Implement 2D rendering:
   - Render UI elements
   - Text rendering
   - Selection highlights

**Deliverables**:
- ✅ RenderPipeline refactored for BGFX views (400+ lines modified)
- ✅ Shadow pass implementation (100+ lines)
- ✅ Forward pass implementation (150+ lines)
- ✅ Water pass (if applicable)
- ✅ Post-processing (100+ lines)
- ✅ 2D rendering (150+ lines)
- ✅ Multi-pass tests (15+ tests)

---

#### Days 5-7: ShaderResources Integration (18 hours)
**Files** (all in `src/OpenSage.Game/Graphics/ShaderResources/`):
1. GlobalShaderResources.cs (update)
2. MeshShaderResources.cs (update)
3. TerrainShaderResources.cs (update)
4. WaterShaderResources.cs (update)
5. ParticleShaderResources.cs (update)

**Tasks**:
1. Update GlobalShaderResources:
   ```csharp
   public class GlobalShaderResources
   {
       public Handle<IUniformBuffer> ProjectionMatrixBuffer { get; private set; }
       public Handle<IUniformBuffer> ViewMatrixBuffer { get; private set; }
       public Handle<IUniformBuffer> LightDataBuffer { get; private set; }
       public Handle<ISampler> LinearSampler { get; private set; }
       public Handle<ISampler> PointSampler { get; private set; }
       
       // Update to use BGFX handles instead of Veldrid
       public void Update(IGraphicsDevice device, Matrix4x4 projection, Matrix4x4 view)
       {
           // Update uniform buffers with BGFX
           device.UpdateBuffer(ProjectionMatrixBuffer, projection);
           device.UpdateBuffer(ViewMatrixBuffer, view);
       }
   }
   ```

2. Update MeshShaderResources for standard materials
3. Update TerrainShaderResources for terrain rendering
4. Update WaterShaderResources for water effects
5. Update ParticleShaderResources for particle rendering

**Deliverables**:
- ✅ All 5 ShaderResources classes updated (500+ lines total)
- ✅ BGFX-compatible uniform buffer binding
- ✅ Texture binding for all resource types
- ✅ State binding for each resource type
- ✅ Integration tests (20+ tests)

---

### Week 32: Scene Integration & Deprecation Planning

#### Days 1-3: Scene3D Integration (16 hours)
**File**: `src/OpenSage.Game/Graphics/Scene3D.cs` (update existing)

**Tasks**:
1. Update Scene3D to use BGFX rendering:
   ```csharp
   public class Scene3D
   {
       public void Render(ICommandList commandList, IGraphicsDevice device, RenderPassType pass)
       {
           var visibleObjects = GetVisibleObjects(pass);
           
           foreach (var obj in visibleObjects)
           {
               var drawable = obj.GetComponent<Drawable>();
               if (drawable != null)
               {
                   commandList.SetPipeline(drawable.Pipeline);
                   commandList.BindVertexBuffer(drawable.Vertices);
                   commandList.BindIndexBuffer(drawable.Indices);
                   commandList.BindTexture(0, drawable.DiffuseTexture);
                   commandList.Draw(drawable.IndexCount);
               }
           }
       }
   }
   ```

2. Update GameObject rendering
3. Update terrain rendering
4. Update water rendering
5. Update particle system rendering
6. Verify all visual output matches Veldrid

**Deliverables**:
- ✅ Scene3D BGFX integration (200+ lines modified)
- ✅ GameObject rendering (100+ lines)
- ✅ Terrain rendering (100+ lines)
- ✅ Water rendering (100+ lines)
- ✅ Particle rendering (100+ lines)
- ✅ Visual output tests (10+ tests)

---

#### Days 4-5: Veldrid Deprecation Plan (12 hours)
**File**: `docs/VELDRID_DEPRECATION.md` (new)

**Tasks**:
1. Document Veldrid removal timeline:
   - Week 32: Mark as deprecated
   - Week 36: Remove from master branch
   - Week 40: Archive documentation

2. Create deprecation warnings:
   ```csharp
   [Obsolete("Veldrid backend is deprecated. Use BGFX instead. " +
             "Veldrid will be removed in Week 36. " +
             "To use Veldrid, run with: --renderer veldrid",
             false)]
   public class VeldridGraphicsDeviceAdapter
   {
   }
   ```

3. Document fallback usage:
   - When to use Veldrid
   - How to enable Veldrid
   - Known Veldrid issues on macOS

4. Create migration guide for dependent code

**Deliverables**:
- ✅ Deprecation documentation (200+ lines)
- ✅ Deprecation warnings in code
- ✅ Fallback instructions
- ✅ Migration guide for users

---

#### Days 6-7: Integration Testing (12 hours)
**File**: `src/OpenSage.Game.Tests/Graphics/BGFX/IntegrationTests.cs`

**Tasks**:
1. Create game load test:
   ```csharp
   [Test]
   public void TestGameLoadsWithBGFX()
   {
       var game = new Game(GraphicsBackendType.BGFX);
       game.Initialize();
       
       // Run 5 frames
       for (int i = 0; i < 5; i++)
           game.Update();
       
       Assert.IsTrue(game.IsRunning);
       game.Dispose();
   }
   ```

2. Test map loading and rendering
3. Test unit rendering
4. Test terrain rendering
5. Test water rendering
6. Test particle effects
7. Performance benchmarking

**Deliverables**:
- ✅ Game load test
- ✅ Map rendering test
- ✅ Unit rendering test
- ✅ Terrain rendering test
- ✅ Water/particle tests
- ✅ Performance baseline (15+ tests)

---

### Phase 5C Success Criteria

**Go/No-Go Gate**:
```
✅ All game systems render with BGFX
✅ Visual output matches Veldrid reference
✅ Veldrid fallback still functional
✅ 60+ FPS on all platforms
✅ No memory leaks
✅ 80+ Phase 5C tests passing
✅ Build: 0 errors, <15 warnings
✅ Deprecation warnings present
```

**Visual Verification**:
- Terrain renders correctly
- Objects render with correct materials
- Water effects work (if present)
- Particles render correctly
- 2D UI renders correctly
- Shadows render correctly

**Go Decision**: ✅ PROCEED TO PHASE 5D
- If all visual output is correct
- If performance is stable (60+ FPS)
- If Veldrid fallback works

---

## Phase 5D: Validation, Optimization & Release (Weeks 33-35)

### Objectives
1. Cross-platform testing (macOS, Windows, Linux)
2. Performance optimization and profiling
3. Bug fixes and stability improvements
4. Documentation updates
5. Prepare for production release
6. **Success Criteria**: Stable 60+ FPS on all platforms, production-ready

### Week 33: Cross-Platform Testing

#### Days 1-3: macOS Testing (12 hours)
**Platform**: Apple Silicon (arm64) + Intel (x86_64)

**Tests**:
1. Metal backend initialization ✅
2. Game launch on both architectures
3. Basic rendering and gameplay
4. Performance baseline (expect 60+ FPS)
5. Memory usage (<1GB)
6. No crashes in 10-minute gameplay session
7. Save/load functionality
8. UI responsiveness

**Deliverables**:
- ✅ macOS testing checklist (completed)
- ✅ Performance baseline (Metal backend)
- ✅ Issue log (if any found)
- ✅ Optimization notes

---

#### Days 4-5: Windows Testing (12 hours)
**Platform**: Windows 10/11 x64 (D3D11 backend)

**Tests**:
1. D3D11 backend initialization ✅
2. Game launch
3. DirectX 11 feature level verification
4. Basic rendering and gameplay
5. Performance baseline (expect 60+ FPS)
6. Memory usage (<1GB)
7. Multi-monitor support
8. fullscreen/windowed switching

**Deliverables**:
- ✅ Windows testing checklist
- ✅ D3D11 performance baseline
- ✅ Issue log
- ✅ Optimization notes

---

#### Days 6-7: Linux Testing (12 hours)
**Platform**: Linux x64 (Vulkan backend)

**Tests**:
1. Vulkan driver verification
2. Game launch
3. Vulkan feature verification
4. Basic rendering and gameplay
5. Performance baseline (expect 60+ FPS)
6. Memory usage (<1GB)
7. Compositor compatibility
8. Display resolution handling

**Deliverables**:
- ✅ Linux testing checklist
- ✅ Vulkan performance baseline
- ✅ Issue log
- ✅ Optimization notes

---

### Week 34: Performance Optimization

#### Days 1-2: Profiling & Analysis (12 hours)
**Tools**: BGFX built-in profiler, platform-specific profilers

**Tasks**:
1. Profile each platform:
   - Metal (Xcode Instruments)
   - D3D11 (PIX, Visual Studio Profiler)
   - Vulkan (Renderdoc, GPA)

2. Identify bottlenecks:
   - GPU time by pass (shadow, forward, water, post, 2D)
   - CPU time by subsystem
   - Memory bandwidth usage
   - Cache efficiency

3. Benchmark vs Veldrid:
   - Compare frame times
   - Compare memory usage
   - Compare battery life (mobile if applicable)

**Deliverables**:
- ✅ Performance profiling report (200+ lines)
- ✅ Bottleneck analysis
- ✅ Optimization opportunities identified
- ✅ Platform comparison

---

#### Days 3-5: Optimization Implementation (18 hours)
**Common Optimizations**:

1. **Draw call reduction**:
   - Batch similar objects
   - Use instancing for repeated geometry
   - Combine meshes where appropriate

2. **Texture optimization**:
   - Compression (BC1/BC3 on D3D11, ASTC on Mobile)
   - Mipmap optimization
   - Atlasing where beneficial

3. **Shader optimization**:
   - Remove unused calculations
   - Use lower precision where possible
   - Reduce register pressure

4. **Memory optimization**:
   - Resource pooling improvements
   - Texture streaming
   - Geometry LOD system

5. **CPU optimization**:
   - Reduce state changes
   - Batch uniform updates
   - Async shader compilation

**Deliverables**:
- ✅ Optimization implementations (300+ lines)
- ✅ Performance improvements (target: +20% on weak hardware)
- ✅ Regression tests (verify no visual regressions)
- ✅ Before/after benchmarks

**Target Performance**:
- Strong hardware (RTX 2070+): 120+ FPS
- Mid-range hardware (RTX 1660): 60+ FPS
- Weak hardware (Intel HD 630): 30+ FPS minimum

---

#### Days 6-7: Stability & Bug Fixes (12 hours)

**Tasks**:
1. Fix any remaining issues found in testing
2. Handle edge cases:
   - Window minimize/maximize
   - Display disconnect/reconnect
   - Driver crashes
   - Out-of-memory conditions

3. Add graceful degradation:
   - Fallback to lower resolution if VRAM low
   - Reduce draw distances if CPU-bound
   - Fallback to software rendering if hardware unsupported

**Deliverables**:
- ✅ All critical bugs fixed
- ✅ Edge case handling
- ✅ Graceful degradation
- ✅ Stability tests (20+ tests)

---

### Week 35: Documentation & Release Preparation

#### Days 1-3: Documentation Updates (16 hours)

**Documents to Update/Create**:
1. `docs/GRAPHICS_SYSTEM.md` - Updated for BGFX
2. `docs/RENDERING_PIPELINE.md` - Multi-pass rendering explanation
3. `docs/SHADER_SYSTEM.md` - Shader compilation workflow
4. `docs/PERFORMANCE_TUNING.md` - Optimization guide
5. `docs/BGFX_BACKENDS.md` - Backend selection and platform specifics
6. `docs/VELDRID_MIGRATION.md` - Migration guide from Veldrid
7. `CHANGELOG.md` - Update Phase 5 changes

**Content**:
- Architecture diagrams
- API references
- Troubleshooting guides
- Performance tips
- Platform-specific notes

**Deliverables**:
- ✅ 7 documentation files (1000+ lines total)
- ✅ Architecture diagrams (5+)
- ✅ Code examples (20+)
- ✅ Troubleshooting guides
- ✅ Performance tips for developers

---

#### Days 4-5: Release Notes & Testing (12 hours)

**Tasks**:
1. Create comprehensive release notes:
   ```markdown
   # Phase 5 Release - BGFX Graphics Backend
   
   ## Summary
   Complete migration from Veldrid to BGFX graphics backend.
   
   ## Highlights
   - ✅ Native Metal support for macOS Apple Silicon
   - ✅ 60+ FPS on all platforms
   - ✅ Zero Veldrid dependencies
   - ✅ Full backward compatibility (Veldrid still available as fallback)
   
   ## Changes
   - New: BgfxGraphicsDevice implementation (1000+ lines)
   - New: BGFX resource management (500+ lines)
   - Updated: RenderPipeline for BGFX views
   - Updated: ShaderResources for BGFX binding
   - Deprecated: Veldrid backend (still available, use --renderer veldrid)
   
   ## Platform Support
   - macOS: Metal backend, arm64+x86_64
   - Windows: D3D11 backend, x64
   - Linux: Vulkan backend, x64
   
   ## Performance
   - Triangle: 0.1ms GPU
   - Textured quad: 0.15ms GPU
   - Full scene: 3-5ms GPU (target-dependent)
   
   ## Known Issues
   - None critical
   
   ## Migration Guide
   See: docs/BGFX_MIGRATION_GUIDE.md
   ```

2. Final regression testing
3. Performance regression check
4. Visual regression check

**Deliverables**:
- ✅ Release notes (500+ lines)
- ✅ Final regression test report
- ✅ Performance comparison (Veldrid vs BGFX)
- ✅ Platform support matrix

---

#### Days 6-7: Release & Tag (10 hours)

**Tasks**:
1. Final build verification:
   ```bash
   dotnet build -c Release
   dotnet publish -c Release
   ```

2. Create release tag:
   ```bash
   git tag -a v5.0.0-bgfx -m "Phase 5: BGFX Graphics Backend Release"
   git push origin v5.0.0-bgfx
   ```

3. Update version in:
   - `global.json`
   - `.csproj` files
   - README.md
   - CHANGELOG.md

4. Create GitHub Release:
   - Upload binaries
   - Include release notes
   - Link to documentation
   - Mention contributors

5. Post-release verification:
   - Verify artifacts available
   - Test downloads
   - Monitor for issues

**Deliverables**:
- ✅ v5.0.0-bgfx tag created
- ✅ GitHub Release published
- ✅ Artifacts available for download
- ✅ Documentation linked
- ✅ README updated
- ✅ CHANGELOG updated

---

### Phase 5D Success Criteria

**Go/No-Go Gate**:
```
✅ All platforms tested and verified (macOS, Windows, Linux)
✅ 60+ FPS stable on target platforms
✅ No critical bugs
✅ Visual output matches Veldrid baseline
✅ Memory usage <1GB
✅ 100+ Phase 5D tests passing
✅ Full documentation provided
✅ Release notes published
✅ Performance baseline established
✅ Build: 0 errors, <10 warnings
```

**Release Decision**: ✅ RELEASE v5.0.0-bgfx
- If all platforms verified
- If performance acceptable
- If no critical bugs
- If full documentation complete

---

## Post-Phase 5: Veldrid Removal (Week 36+, Optional)

After Phase 5D is successfully released and stable for 2+ weeks:

### Optional Week 36: Veldrid Cleanup
1. Remove VeldridGraphicsDeviceAdapter
2. Remove Veldrid NuGet dependencies
3. Remove unused shader conversion code
4. Clean up deprecated APIs
5. Final build verification

**Note**: This is OPTIONAL. Veldrid can remain as fallback indefinitely if desired.

---

## Overall Timeline

```
Week 26-27: Phase 5A - Foundation (BGFX goes from 0 to initialization working)
Week 28-30: Phase 5B - Core Graphics (BGFX rendering pipeline functional)
Week 31-32: Phase 5C - Integration (Engine systems migrated to BGFX)
Week 33-35: Phase 5D - Release (Testing, optimization, documentation)
Week 36+:   Optional Veldrid cleanup
```

**Total: 10 weeks to production-ready BGFX backend**

---

## Resource Requirements

### Team
- **2x Graphics Engineer** (full-time): 400-500 hours each
- **1x Senior Graphics Architect** (part-time guidance): 100 hours
- **1x QA Engineer** (testing): 150 hours

**Total: 1050-1200 hours (~6-7 person-months)**

### Hardware
- macOS Apple Silicon (M1/M2/M3) for testing
- Windows 10/11 x64 machine
- Linux x64 machine (optional but recommended)

### External Tools
- BGFX source + pre-built binaries
- shaderc compiler (included with BGFX)
- Platform profilers (Xcode, PIX, Renderdoc)
- Git + GitHub

---

## Risk Management

### High-Risk Items

#### 1. Shader Compilation Pipeline
- **Risk**: BGFX shader compilation differs significantly from Veldrid
- **Probability**: Medium (30%)
- **Impact**: High (could block Phase 5B)
- **Mitigation**:
  - Create shader conversion tests early
  - Validate shaderc output against Veldrid SPIR-V
  - Have fallback plan: use pre-compiled shaders

#### 2. RenderPipeline Refactoring
- **Risk**: Complex refactoring could introduce regressions
- **Probability**: High (60%)
- **Impact**: High (visual corruption possible)
- **Mitigation**:
  - Keep Veldrid side-by-side during refactoring
  - Extensive visual regression testing
  - Frame-by-frame comparison with reference
  - Incremental refactoring (one pass at a time)

#### 3. Performance Regression
- **Risk**: BGFX could be slower than Veldrid on certain hardware
- **Probability**: Low (20%)
- **Impact**: Medium (would need optimization)
- **Mitigation**:
  - Benchmark early and often
  - Profile each platform
  - Have optimization strategies prepared
  - Fallback to Veldrid if needed

#### 4. Platform-Specific Bugs
- **Risk**: Metal, D3D11, or Vulkan backend has bugs
- **Probability**: Medium (40%)
- **Impact**: High (blocks release on that platform)
- **Mitigation**:
  - Test early on all platforms
  - Use official BGFX releases
  - File bugs with BGFX maintainers
  - Have Windows+Linux fallback if macOS fails

#### 5. Memory Management Issues
- **Risk**: Handle leaks or double-frees in BGFX integration
- **Probability**: Medium (30%)
- **Impact**: High (crashes in production)
- **Mitigation**:
  - Extensive unit testing of resource lifecycle
  - Memory leak detection during testing
  - Resource pool validation
  - Handle generation verification

### Mitigation Strategies

1. **Parallel Testing**: Keep Veldrid running throughout
2. **Early Profiling**: Profile from Week 26, not Week 34
3. **Fallback Plans**: Have rollback procedures
4. **Weekly Checkpoints**: Go/no-go gates at each phase
5. **Documentation**: Keep detailed notes of decisions and issues

---

## Success Metrics

### Technical Metrics
- ✅ 0 errors in build
- ✅ <10 warnings in build
- ✅ 100% test pass rate (200+ tests)
- ✅ 60+ FPS stable on all platforms
- ✅ <1GB memory usage
- ✅ <0.5ms input latency
- ✅ All game systems functional

### Business Metrics
- ✅ On-time delivery (Week 35)
- ✅ Within budget (1200 hours)
- ✅ Zero critical bugs at release
- ✅ Full documentation provided
- ✅ Smooth user experience

### Quality Metrics
- ✅ Code coverage >70% (graphics systems)
- ✅ Zero regressions vs Veldrid
- ✅ Performance +10% vs Veldrid (target)
- ✅ All platforms supported

---

## Go/No-Go Gates

### Phase 5A Gate (End of Week 27)
```
Decision: PROCEED TO PHASE 5B?

Criteria:
✅ BGFX initializes correctly on all platforms
✅ Game window displays (blank)
✅ 60 FPS stable
✅ No crashes
✅ Build: 0 errors, <15 warnings
✅ All Phase 5A tests passing (60+ tests)

If NO-GO: 
→ Debug and fix issues
→ Re-evaluate BGFX approach
→ Consider Veldrid-only approach as fallback
```

### Phase 5B Gate (End of Week 30)
```
Decision: PROCEED TO PHASE 5C?

Criteria:
✅ Triangle with texture renders correctly
✅ Framebuffer capture shows correct output
✅ 60+ FPS on all platforms
✅ Shader compilation pipeline working
✅ No memory leaks
✅ All Phase 5B tests passing (100+ tests)

If NO-GO:
→ Optimize and fix graphics pipeline
→ Consider simplified rendering path
→ Investigate platform-specific issues
```

### Phase 5C Gate (End of Week 32)
```
Decision: PROCEED TO PHASE 5D?

Criteria:
✅ All game systems render with BGFX
✅ Visual output matches Veldrid reference
✅ 60+ FPS on all platforms
✅ Veldrid fallback still functional
✅ No critical bugs found
✅ All Phase 5C tests passing (80+ tests)

If NO-GO:
→ Fix integration issues
→ Revert problematic changes
→ Extend timeline if necessary
```

### Phase 5D Gate (End of Week 35)
```
Decision: RELEASE v5.0.0-bgfx?

Criteria:
✅ All platforms tested and verified
✅ 60+ FPS stable
✅ Zero critical bugs
✅ Full documentation complete
✅ Performance baseline established
✅ All Phase 5D tests passing (100+ tests)

If YES:
→ Create v5.0.0-bgfx release tag
→ Publish artifacts
→ Announce release

If NO:
→ Extend Phase 5D for additional fixes
→ Hold release until criteria met
```

---

## Appendix: Implementation Checklist

### Phase 5A Checklist
- [ ] BGFX libraries acquired for all platforms
- [ ] P/Invoke bindings created (bgfx.cs ~2000 lines)
- [ ] BgfxPlatformData.cs created (~200 lines)
- [ ] BgfxGraphicsDevice skeleton (~500 lines)
- [ ] BgfxCommandList implementation (~300 lines)
- [ ] Backend factory updated
- [ ] Command-line flag parsing added
- [ ] Environment variable support added
- [ ] 60+ tests created and passing
- [ ] Build: 0 errors, <15 warnings
- [ ] Phase 5A go/no-go gate cleared

### Phase 5B Checklist
- [ ] Buffer management (creation, destruction, updates)
- [ ] Texture management (creation, formats, samplers)
- [ ] Framebuffer management (attachments, resizing)
- [ ] Shader compilation pipeline (offline compilation)
- [ ] Pipeline state mapping (raster, depth, blend)
- [ ] Render pass system (views 0-3)
- [ ] Draw operations (all variants)
- [ ] Triangle rendering test passing
- [ ] Textured quad rendering test passing
- [ ] 100+ tests created and passing
- [ ] Performance baseline captured
- [ ] Phase 5B go/no-go gate cleared

### Phase 5C Checklist
- [ ] RenderPipeline refactored for BGFX views
- [ ] GlobalShaderResources updated
- [ ] MeshShaderResources updated
- [ ] TerrainShaderResources updated
- [ ] WaterShaderResources updated
- [ ] ParticleShaderResources updated
- [ ] Scene3D integration complete
- [ ] GameObject rendering working
- [ ] All visual output verified
- [ ] Veldrid deprecation warnings added
- [ ] 80+ tests created and passing
- [ ] Phase 5C go/no-go gate cleared

### Phase 5D Checklist
- [ ] macOS testing complete
- [ ] Windows testing complete
- [ ] Linux testing complete
- [ ] Performance profiling complete
- [ ] Optimizations implemented
- [ ] All bugs fixed
- [ ] Documentation updated (7 files, 1000+ lines)
- [ ] Release notes written
- [ ] v5.0.0-bgfx tag created
- [ ] GitHub Release published
- [ ] 100+ tests created and passing
- [ ] Performance targets met
- [ ] Phase 5D go/no-go gate cleared

---

## Next Steps

**Immediate Actions (Before Week 26)**:
1. Acquire BGFX source code and build for all platforms
2. Review BGFX documentation and examples
3. Create project structure (directories, .csproj updates)
4. Set up build infrastructure for BGFX libraries
5. Schedule team kickoff meeting
6. Establish weekly check-in cadence
7. Create per-week task breakdown with individual assignments

**Week 26 Actions**:
1. Start Phase 5A - Foundation
2. Complete BGFX library acquisition (Days 1-2)
3. Begin P/Invoke bindings (Days 3-5)
4. Review bindings with team
5. Plan Week 27 activities

**Success Indicators**:
- ✅ Timeline adhered to (±1 week acceptable)
- ✅ Quality maintained (0 critical bugs at release)
- ✅ Team morale high
- ✅ Users happy with performance and stability
- ✅ Open-source contribution accepted by BGFX maintainers (if applicable)

---

## Conclusion

This comprehensive plan provides a clear path forward for implementing BGFX as the primary graphics backend while maintaining Veldrid as a stable fallback. The 10-week timeline is realistic with proper resource allocation, and the phased approach allows for early validation and course correction.

**Key Success Factors**:
1. Parallel implementation (BGFX + Veldrid side-by-side)
2. Early profiling and optimization
3. Comprehensive testing on all platforms
4. Clear go/no-go gates at each phase
5. Strong team communication and coordination

The team is ready to proceed with Phase 5A starting Week 26.

---

**Document Version**: 1.0  
**Created**: December 12, 2025  
**Status**: Ready for Implementation  
**Owner**: Graphics Team Lead  
**Reviewers**: Tech Lead, Project Manager
