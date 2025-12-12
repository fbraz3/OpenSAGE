# Phase 2: Architectural Design (Weeks 4-7)

## Overview

Phase 2 translates the requirements and learnings from Phase 1 into detailed architectural designs and implementation plans. This phase creates the blueprint for actual code development in Phase 3.

**Phase 1 Foundation Documents**:
- [Phase_1_Technical_Feasibility_Report.md](support/Phase_1_Technical_Feasibility_Report.md) - Feasibility basis for all designs
- [Phase_1_Requirements_Specification.md](support/Phase_1_Requirements_Specification.md) - Requirements driving Phase 2 design
- [Feature_Audit.md](support/Feature_Audit.md) - Feature compatibility reference
- [Dependency_Analysis.md](support/Dependency_Analysis.md) - NuGet package strategy

## Goals

1. Design graphics abstraction layer
2. Plan component refactoring
3. Design shader compilation pipeline
4. Create implementation specifications
5. Prepare development environment

## Deliverables

### 2.1 Graphics Abstraction Layer Design

**Objective**: Define interface between OpenSAGE and graphics backend

**Reference**: See [Phase_1_Requirements_Specification.md](support/Phase_1_Requirements_Specification.md) for requirements driving this design.

**Scope**: This is the most critical deliverable of Phase 2. The abstraction layer must:
1. Separate OpenSAGE's rendering logic from Veldrid/BGFX implementation details
2. Support both Veldrid and BGFX backends without conditional compilation in client code
3. Minimize performance overhead
4. Support all existing rendering features in OpenSAGE

**Design Principles**:

1. **Adapter Pattern**: Use factory pattern for backend selection
   - Single `IGraphicsDevice` interface that both Veldrid and BGFX implement
   - `GraphicsDeviceFactory.CreateDevice(GraphicsBackend backend)` for selection
   - No coupling between OpenSAGE and specific backends

2. **Resource Handle System**: Abstract resource pointers
   - `IBuffer`, `ITexture`, `IFramebuffer`, `IShaderProgram` interfaces
   - Opaque handles prevent direct API access
   - Type-safe handle wrapping

3. **Stateless Operations**: Command-based rendering
   - Draw calls don't modify global state
   - `DrawCommand` struct containing all rendering state
   - Enables thread-safe command recording

4. **Error Handling**: Consistent across backends
   - Define error codes and exception types
   - Graceful fallback mechanisms
   - Clear error messages for debugging

**Tasks**:

- [ ] **Core Abstraction Interfaces (Week 4)**
  - Define `IGraphicsDevice` with capabilities, resource creation, rendering
  - Define `IBuffer` for vertex/index/uniform buffers
  - Define `ITexture` for all texture types
  - Define `IFramebuffer` for render targets
  - Define `ISampler` for texture sampling configuration
  - Define `IShaderProgram` for compiled shaders
  - Define `IPipeline` for graphics pipeline state
  - Document threading model in interfaces

- [ ] **Handle & Resource Management (Week 4)**
  - Design generic `Handle<T>` wrapper for type safety
  - Implement generation-based handles for validation
  - Plan resource lifecycle management
  - Document memory ownership model
  - Plan disposal and cleanup strategies

- [ ] **State Management Abstraction (Week 4-5)**
  - Design immutable state objects (`ViewportState`, `BlendState`, `DepthState`, etc.)
  - Plan state caching to reduce redundant operations
  - Document state validation
  - Handle state incompatibilities between backends

- [ ] **Backend Adapter Implementation Specs (Week 5)**
  
  **VeldridGraphicsDevice**:
  - Map to existing Veldrid usage in GraphicsSystem
  - Direct mapping for most operations (minimal overhead)
  - Cache Veldrid CommandList per frame
  - Implement swapchain management
  
  **BgfxGraphicsDevice**:
  - Map `IFramebuffer` to BGFX views
  - Use BGFX encoder for command recording
  - Handle view/encoder lifecycle
  - Implement dynamic framebuffer binding

- [ ] **Type System & Conversions (Week 5)**
  - Define cross-backend texture formats (`TextureFormat` enum)
  - Map to Veldrid `PixelFormat` and BGFX texture formats
  - Define buffer usage flags compatible with both APIs
  - Document format conversion fallback strategy
  - Handle unsupported formats gracefully

- [ ] **Error Handling & Validation (Week 5)**
  - Define `GraphicsException` and error codes
  - Plan assertion mechanisms for debug builds
  - Document fallback rendering paths
  - Plan shader compilation error reporting
  - Implement resource state validation

**Deliverable**: `GraphicsAbstractionLayer_Design.md` (30-40 pages)

**Design Examples**:

```csharp
// Core interfaces
public interface IGraphicsDevice : IDisposable
{
    // Resource creation
    IBuffer CreateBuffer(BufferDescription desc);
    ITexture CreateTexture(TextureDescription desc);
    IFramebuffer CreateFramebuffer(FramebufferDescription desc);
    IShaderProgram CreateShaderProgram(string name, ShaderSource[] sources);
    ISampler CreateSampler(SamplerDescription desc);
    
    // Rendering
    void SetViewport(Viewport viewport);
    void SetFramebuffer(IFramebuffer framebuffer);
    void Clear(ClearOptions options);
    void Draw(in DrawCommand command);
    void Present();
    
    // Queries
    GraphicsCapabilities GetCapabilities();
    GraphicsStats GetStats();
}

// Factory pattern
public static class GraphicsDeviceFactory
{
    public static IGraphicsDevice CreateDevice(GraphicsBackend backend, GraphicsDeviceOptions options)
    {
        return backend switch
        {
            GraphicsBackend.Veldrid => new VeldridGraphicsDevice(options),
            GraphicsBackend.BGFX => new BgfxGraphicsDevice(options),
            _ => throw new ArgumentException($"Unsupported backend: {backend}")
        };
    }
}

// State objects (immutable)
public struct BlendState
{
    public bool Enabled { get; init; }
    public BlendFunction SourceFactor { get; init; }
    public BlendFunction DestinationFactor { get; init; }
    public BlendOperation Operation { get; init; }
    
    public BlendState() => this = Default;
    public static BlendState Default => new()
    {
        Enabled = false,
        SourceFactor = BlendFunction.SourceAlpha,
        DestinationFactor = BlendFunction.InverseSourceAlpha,
        Operation = BlendOperation.Add
    };
}
```

**Acceptance Criteria**:

- [ ] All interfaces complete and documented with XML comments
- [ ] Type system covers all existing OpenSAGE rendering features
- [ ] Error handling strategy defined and documented
- [ ] Resource lifecycle clearly specified
- [ ] Handle system prevents use-after-free issues
- [ ] Design allows both sync and async rendering
- [ ] Threading model documented for multi-threaded encoders
- [ ] Design reviewed and approved by team

### 2.2 Component Refactoring Plan

**Objective**: Identify and plan refactoring of interdependent components

**Reference**: Review [Dependency_Analysis.md](support/Dependency_Analysis.md) from Phase 1 for comprehensive NuGet package analysis and dependency mapping.

**Scope**: Phase 1 analysis identified Veldrid dependencies scattered throughout OpenSAGE. This section creates the detailed refactoring roadmap.

**Analysis from Phase 1**:
- ~320+ BGFX API functions to implement
- Veldrid directly used in: GraphicsSystem, RenderPipeline, ShaderSystem, Game, ContentManager
- Indirect dependencies through assets (shaders, textures, pipelines)
- No major architectural blockers identified

**Refactoring Strategy**:

1. **Staged Approach**: Low-coupling first, then higher-level systems
   - Stage 1 (Lowest coupling): ResourceFormat, BufferDescriptions, basic types
   - Stage 2 (Low coupling): Buffer and Texture management
   - Stage 3 (Medium coupling): RenderPass and command recording
   - Stage 4 (High coupling): RenderPipeline and Scene3D rendering
   - Stage 5 (Final): Integration with Game and ContentManager

2. **Feature Flags**: Gradual transition without breaking main branch
   - Conditional compilation with `#if BGFX_ENABLED`
   - Runtime backend selection without recompilation
   - Allows parallel development and testing

3. **Backwards Compatibility**: Maintain existing APIs during migration
   - Keep `GraphicsDevice` (Veldrid) working until Phase 3 completion
   - New code uses `IGraphicsDevice` interface
   - Deprecation warnings on old APIs

**Tasks**:

- [ ] **Dependency Map & Impact Analysis (Week 4)**
  - List all files importing Veldrid types
  - Document each import's purpose and coupling level
  - Identify circular dependencies
  - Map component boundaries
  - Create detailed dependency graph
  
  **Key Components to Refactor**:
  ```
  Priority 1 (Week 4):
  - ResourceFormat & TextureFormat enum
  - BufferDescription, TextureDescription, FramebufferDescription
  - Basic handle types
  
  Priority 2 (Week 5):
  - Texture loading and management
  - Buffer creation and updates
  - Framebuffer attachment
  
  Priority 3 (Week 6):
  - RenderPass system
  - Command recording pipeline
  - View management
  
  Priority 4 (Week 7):
  - Scene3D rendering refactoring
  - Material system updates
  - Lighting system integration
  ```

- [ ] **Incremental Refactoring Strategy (Week 4-5)**
  - For each component, create `IComponent` interface
  - Implement `VeldridComponent` and `BgfxComponent` adapters
  - Replace direct Veldrid calls with interface calls
  - Add unit tests for each interface
  - Validate against existing behavior

- [ ] **Component Specifications (Week 5-6)**
  - For each refactored component:
    - Current implementation analysis (how Veldrid is used)
    - New interface definition (ITextureManger, IBufferManager, etc.)
    - Integration points (what systems depend on this)
    - Testing strategy (unit tests, integration tests)
    - Performance expectations
    - Rollback procedure

- [ ] **Backward Compatibility Planning (Week 6)**
  - Document deprecated APIs
  - Create migration path documentation
  - Plan deprecation timeline
  - Identify APIs that must remain compatible
  - Plan alternative implementations for incompatible features

**Refactoring Checklist Template**:

```markdown
## Component: [ComponentName]

### Current Implementation
- Uses Veldrid: [specific classes/types]
- Integration points: [which systems depend on this]
- Coupling level: [High/Medium/Low]
- Complexity: [Simple/Moderate/Complex]

### New Interface
- Interface name: I[ComponentName]
- Key methods: ...
- Dependencies: ...

### Refactoring Plan
- [ ] Create interface
- [ ] Implement Veldrid adapter
- [ ] Implement BGFX adapter
- [ ] Add unit tests
- [ ] Validate behavior equivalence
- [ ] Update documentation
- [ ] Code review

### Risk Mitigation
- Rollback strategy: ...
- Testing focus: ...
- Known issues: ...

### Integration Checkpoints
- Week X: [Checkpoint description]
- Week Y: [Checkpoint description]
```

**Component Refactoring Matrix**:

| Component | Current | New Interface | Complexity | Priority | Coupling | Risk |
|-----------|---------|----------------|-----------|----------|----------|------|
| TextureManager | Veldrid-coupled | ITextureManager | High | P1 | Low | Low |
| BufferManager | Veldrid-coupled | IBufferManager | High | P1 | Low | Low |
| ShaderCompiler | Runtime reflection | IShaderCompiler | High | P2 | Medium | Medium |
| RenderPipeline | Veldrid CommandList | RenderPass abstraction | Very High | P3 | High | High |
| Scene3D Rendering | Direct Veldrid | Scene renderer abstraction | Complex | P4 | Very High | High |
| MaterialSystem | Veldrid-specific | IMaterial interface | Medium | P3 | Medium | Medium |
| ViewSystem | Framebuffer bindings | IViewManager | High | P3 | High | Medium |

**Deliverable**: `Component_Refactoring_Plan.md` (20-25 pages)

**Acceptance Criteria**:

- [ ] All Veldrid-coupled components identified and documented
- [ ] Refactoring order justified based on coupling analysis
- [ ] Each component has specified tests
- [ ] Rollback procedures documented for each stage
- [ ] Feature flag strategy clearly explained
- [ ] Integration checkpoints defined
- [ ] Risk mitigation strategies for high-risk components
- [ ] Timeline realistic and validated

### 2.3 Shader Compilation Pipeline Design

**Objective**: Plan transition from runtime to offline shader compilation

**Reference**: See [Shader_Compatibility.md](support/Shader_Compatibility.md) for detailed shader inventory and compatibility assessment.

**Current State (OpenSAGE with Veldrid)**:
- Shaders compiled at runtime using reflection API
- Direct integration with Veldrid shader compilation
- No offline compilation or caching
- Slow startup times due to shader compilation

**New State (OpenSAGE with BGFX)**:
- All shaders pre-compiled at build time to SPIR-V
- BGFX consumes compiled shaders from asset directory
- Faster startup and frame times
- Better shader variant management

**Architecture**:

```
Development Workflow:
  GLSL/HLSL Source → shaderc compiler → SPIR-V Binary → BGFX Asset
     ↓
  Build Process:
    MSBuild Task → Detect shader changes → Compile via shaderc → Cache
     ↓  
  Runtime:
    Load pre-compiled SPIR-V → BGFX processes → Device-specific code
```

**Tasks**:

- [ ] **Shader Format Analysis (Week 4)**
  - Inventory all shaders in OpenSAGE (Phase 1: Feature_Audit.md documents 18 shaders)
  - Identify variants (technique variations, platform-specific code)
  - Document permutations (diffuse maps, normal maps, specular, etc.)
  - Plan variant caching strategy
  - Identify fallback shaders for missing features

  **Shader Classification**:
  ```
  Category 1: Core Rendering (10 shaders)
  - Standard.fx (multi-technique)
  - Shadow rendering
  - Deferred rendering
  
  Category 2: Special Effects (5 shaders)
  - Water rendering
  - Particle systems
  - Deferred post-processing
  
  Category 3: UI/Tools (3 shaders)
  - 2D UI rendering
  - Debug visualization
  - Editor tools
  ```

- [ ] **Build System Integration (Week 4-5)**
  - Integrate shaderc compiler into MSBuild pipeline
  - Create custom MSBuild task for shader compilation
  - Detect shader source changes (timestamps, content hash)
  - Implement shader compilation cache
  - Define shader asset output directory structure
  - Plan parallel compilation for faster builds

  **Implementation Details**:
  - Create `CompileShaders.targets` MSBuild file
  - Define shader item group with metadata (source, format, variants)
  - Execute `glslc` or library API for each shader
  - Validate compilation success/failure
  - Generate shader metadata JSON files
  - Update Asset Registry with compiled shaders

- [ ] **Shader Metadata Strategy (Week 5)**
  - Define metadata format (JSON with uniform layout, texture bindings, variants)
  - Plan uniform extraction from compiled shaders
  - Design shader descriptor format for runtime consumption
  - Plan reflection data generation
  - Document version compatibility handling

  **Metadata Example**:
  ```json
  {
    "name": "StandardPBR",
    "format": "spirv",
    "variants": [
      {
        "name": "opaque",
        "defines": ["OPAQUE=1"]
      },
      {
        "name": "transparent",
        "defines": ["TRANSPARENT=1"]
      }
    ],
    "uniforms": [
      {
        "name": "u_model",
        "type": "mat4",
        "frequency": "instance"
      },
      {
        "name": "u_view",
        "type": "mat4",
        "frequency": "frame"
      }
    ],
    "samplers": [
      {
        "name": "s_albedo",
        "type": "texture2d",
        "binding": 0
      },
      {
        "name": "s_normal",
        "type": "texture2d",
        "binding": 1
      }
    ],
    "renderState": {
      "blend": "alpha",
      "depthTest": "lessequal"
    }
  }
  ```

- [ ] **Build System Tasks (Week 5)**
  
  Create MSBuild task sequence:
  
  1. **ShaderGlobbing**: Find all *.glsl, *.hlsl files
  2. **ShaderValidation**: Check syntax and dependencies
  3. **ShaderCompilation**: Execute shaderc for each shader
  4. **MetadataGeneration**: Extract uniform/texture data
  5. **CacheManagement**: Store compiled binaries
  6. **RegistryUpdate**: Update ContentManager asset registry

- [ ] **Development Workflow (Week 5)**
  - Plan shader editor integration
  - Design hot-reload mechanism for development
  - Document debugging support
  - Plan error reporting (compilation failures, missing includes)
  - Create shader validation tools

- [ ] **Deliverable Files**

  **ShaderCompilation_Pipeline.md** (15-20 pages):
  - Complete architecture documentation
  - MSBuild integration guide
  - Shader metadata format specification
  - Build performance optimization
  - Troubleshooting guide

  **Source Files**:
  - CompileShaders.targets (MSBuild task definitions)
  - ShaderCompiler.cs (C# wrapper for shaderc)
  - ShaderMetadata.cs (metadata definitions)
  - CompilationResult.cs (result and error handling)

**Key Integration Points**:

1. **Game.cs**: Changed only in asset loading
2. **ContentManager.cs**: New shader asset type
3. **RenderPipeline.cs**: Use pre-compiled shaders
4. **Build Process**: Add shader compilation step
5. **Asset Directory**: New compiled shaders location

**Compiler Setup**:

- **Tool**: shaderc (Google's GLSL to SPIR-V compiler)
- **NuGet Package**: `shaderc.net` or build shaderc.dll locally
- **Supported Formats**: GLSL, HLSL → SPIR-V
- **Platforms**: Windows, macOS, Linux (pre-built binaries available)
- **Performance**: ~100-200ms per shader depending on complexity

**Acceptance Criteria**:

- [ ] shaderc integrated and working on all platforms
- [ ] MSBuild task successfully compiles all 18 shaders
- [ ] Metadata extraction works for all shader types
- [ ] Compilation cache reduces rebuild times by >50%
- [ ] Error messages clear and actionable
- [ ] Hot-reload works in development builds
- [ ] All existing shaders compile without modification
- [ ] Performance: Startup time reduced by >50%
- [ ] Build time increase <10% for full rebuild

### 2.4 Multi-Threading Architecture

**Objective**: Design thread-safe rendering with BGFX

**Key Insight from Phase 1**: BGFX has built-in multi-threading support via Encoder API, enabling efficient parallel command recording.

**Current OpenSAGE Model**:
- Single-threaded rendering in main thread
- Game logic on separate threads (logic tick vs render tick)
- No parallel command recording

**New Model (BGFX)**:
- **API Thread** (main thread): User code making BGFX calls
- **Render Thread** (background): Executes GPU commands
- **Worker Threads** (optional): Record rendering commands in parallel

**Architecture Diagram**:

```
Main Thread (Game Logic)
  ↓
  └─→ Encoder Interface ────────→ Command Buffer
              ↓
  Worker Thread 1 ───→ Encoder ──→ Command Buffer
  Worker Thread 2 ───→ Encoder ──→ Command Buffer
  Worker Thread N ───→ Encoder ──→ Command Buffer
              ↓
        Render Thread
          (BGFX Core)
              ↓
        GPU Backend
      (Metal/Vulkan/DX)
```

**Tasks**:

- [ ] **Threading Model Design (Week 4)**
  - Define thread responsibilities:
    - **Main Thread**: Scene graph traversal, visibility determination, command generation
    - **Render Thread**: Execute GPU commands from BGFX queue
    - **Worker Threads** (optional): Parallel mesh batching, shadow rendering
  
  - Plan synchronization points:
    - Frame boundary (swap)
    - Resource updates (texture/buffer uploads)
    - Present operation
  
  - Document thread-local storage requirements
  - Plan thread pool size based on CPU cores

- [ ] **Encoder-Based Threading (Week 4-5)**
  
  **BGFX Encoder Model**:
  - Each thread gets an `Encoder` via `bgfx::begin(true)` for parallel recording
  - Encoders are lightweight command recorders
  - Main thread finalizes with `bgfx::end()` and `bgfx::frame()`
  
  **Integration with OpenSAGE**:
  - RenderPass splits rendering tasks to workers
  - Each worker encodes commands to its encoder
  - Main thread waits for all workers to finish
  - Submit all encoders to BGFX in order
  
  ```csharp
  // Pseudo-code example
  public void RenderFrame(RenderContext context)
  {
      var tasks = new Task[NumWorkers];
      
      // Encode commands in parallel
      for (int i = 0; i < NumWorkers; i++)
      {
          int index = i;
          tasks[i] = Task.Run(() =>
          {
              var encoder = _graphics.BeginEncoder();
              
              // Each worker encodes its portion
              EncodeRenderCommands(context, encoder, index);
              
              _graphics.EndEncoder(encoder);
          });
      }
      
      Task.WaitAll(tasks);
      
      // Submit frame to BGFX
      _graphics.Present();
  }
  ```

- [ ] **Lock-Free Data Structures (Week 5)**
  - Identify hot-path data structures:
    - Render queue (commands)
    - Camera/view parameters
    - Uniform buffers
  
  - Use thread-safe patterns:
    - Lock-free queues for command submission (e.g., `ConcurrentQueue`)
    - Atomic operations for simple counters
    - Double-buffering for mutable state
  
  - Document which structures need synchronization
  - Plan memory barriers for GPU sync

- [ ] **Synchronization Strategy (Week 5)**
  
  **Synchronization Points**:
  1. **Frame Boundary**: Wait for previous frame to render
  2. **Resource Updates**: Ensure no GPU access during CPU update
  3. **Encoder Completion**: All workers finished encoding
  4. **Frame Submit**: All encoders ready for GPU
  
  **Implementation**:
  ```csharp
  public class RenderThread
  {
      private readonly AutoResetEvent _frameReady = new(false);
      private readonly AutoResetEvent _frameComplete = new(false);
      private readonly IGraphicsDevice _graphics;
      
      public void EncodeFrame(Action encodeFunc)
      {
          // Signal render thread to start frame
          _frameReady.Set();
          
          // Do work on main thread
          encodeFunc();
          
          // Wait for render to complete
          _frameComplete.WaitOne();
      }
      
      private void RenderThreadLoop()
      {
          while (running)
          {
              _frameReady.WaitOne();
              
              // Execute GPU commands
              _graphics.ExecuteFrame();
              
              // Signal complete
              _frameComplete.Set();
          }
      }
  }
  ```

- [ ] **Performance Considerations (Week 5)**
  - Measure encoder overhead
  - Profile parallelization overhead
  - Determine optimal worker thread count
  - Plan for CPU-bound vs GPU-bound scenarios
  - Document expected improvements

- [ ] **Debug & Profiling (Week 5)**
  - Plan thread naming for debugger
  - Add threading visualization tools
  - Document thread event logging
  - Plan deadlock detection
  - Create stress tests for thread safety

**Deliverable**: `Threading_Architecture.md` (15-20 pages)

**Code Example Structure**:

```csharp
// Public API for rendering systems
public interface IGraphicsDevice
{
    // Single-threaded API
    void Clear(ClearOptions options);
    void SetViewport(Viewport viewport);
    void Present();
    
    // Multi-threaded API
    IEncoder BeginEncoder(bool forThread = false);
    void EndEncoder(IEncoder encoder);
    void SubmitEncoder(IEncoder encoder);
}

// Encoder for parallel command recording
public interface IEncoder
{
    void SetState(RenderState state);
    void SetIndexBuffer(IBuffer buffer);
    void SetVertexBuffer(IBuffer buffer);
    void Submit(uint program, uint depth);
    // ... other commands
}

// Thread-safe render queue
public class RenderQueue
{
    private readonly ConcurrentQueue<RenderCommand> _commands;
    
    public void Enqueue(RenderCommand cmd) => _commands.Enqueue(cmd);
    public IEnumerable<RenderCommand> GetAll() => _commands.ToList();
    public void Clear() => ... // Clear without reallocating
}
```

**Acceptance Criteria**:

- [ ] Multi-threading model clearly documented
- [ ] Encoder API fully specified
- [ ] Synchronization points identified and justified
- [ ] Worker thread count determination documented
- [ ] Lock-free structures chosen and justified
- [ ] Performance expectations documented
- [ ] Debug tools planned
- [ ] Thread safety verified through design review
- [ ] No data races identified in design

### 2.5 Debug & Profiling Integration

**Objective**: Leverage BGFX's built-in debugging capabilities

**BGFX Built-In Features**:
- Frame capture for offline analysis
- GPU memory stats
- Encoder stats (draw calls, state changes)
- Performance counters
- Debug text output
- Call stack tracking

**Tasks**:

- [ ] **Debug Feature Mapping (Week 5)**
  - Document BGFX debug capabilities
  - Plan integration with OpenSAGE debug UI
  - Design in-game profiler visualization
  - Plan frame capture file format
  - Document shader debugging support

  **Capabilities**:
  - `bgfx::captureFrame()` - Capture frames for offline replay
  - `bgfx::getStats()` - Performance statistics
  - `bgfx::setMarker()` - Debug markers in frame capture
  - `bgfx::setDebugFlags()` - Enable debug output

- [ ] **Metrics & Telemetry (Week 5)**
  - Design metric collection system
  - Plan dashboard for visualization
  - Document performance benchmarking methodology
  - Plan telemetry export (JSON, CSV)

  **Metrics to Track**:
  - Frame time (GPU and CPU)
  - Draw calls per frame
  - State changes per frame
  - Triangles per second
  - Memory usage (VRAM, staging)
  - Cache efficiency
  - Encoder overhead

  ```csharp
  public struct FrameStats
  {
      public float FrameTimeMs { get; set; }
      public uint DrawCalls { get; set; }
      public uint StateChanges { get; set; }
      public uint TrianglesPerSecond { get; set; }
      public ulong MemoryUsed { get; set; }
      public float CacheHitRate { get; set; }
  }
  ```

- [ ] **Error Reporting (Week 5)**
  - Design BGFX callback integration
  - Plan error message propagation
  - Document assertion handling
  - Plan crash diagnostics and recovery

  **Error Categories**:
  - Compilation errors (shader failures)
  - State errors (invalid state combinations)
  - Resource errors (memory allocation failures)
  - API misuse errors
  - GPU errors

- [ ] **Performance Profiling Tools (Week 5)**
  - Plan in-game profiler UI (FPS counter, frame time graph)
  - Design frame capture integration
  - Plan shader compilation profiling
  - Document timing instrumentation

  **Tool Set**:
  - Frame timing overlay
  - Memory profiler
  - GPU trace viewer
  - Shader compiler profiler
  - Asset loading profiler

**Deliverable**: `Debug_Integration_Plan.md` (10-15 pages)

**Code Example**:

```csharp
public class DebugProfiler
{
    private readonly IGraphicsDevice _graphics;
    private readonly Queue<FrameStats> _history = new(120);
    
    public void OnFrameComplete()
    {
        var stats = new FrameStats
        {
            FrameTimeMs = (float)_frameTimer.ElapsedMilliseconds,
            DrawCalls = _graphics.GetStats().DrawCalls,
            StateChanges = _graphics.GetStats().StateChanges,
            TrianglesPerSecond = CalculateTriangleRate(),
            MemoryUsed = _graphics.GetStats().MemoryUsed
        };
        
        _history.Enqueue(stats);
        if (_history.Count > 120) _history.Dequeue();
    }
    
    public void RenderDebugUI(IDrawingContext2D context)
    {
        if (!_showProfiler) return;
        
        var avg = _history.Average(s => s.FrameTimeMs);
        context.DrawText($"FPS: {1000 / avg:F1}", 10, 10);
        context.DrawText($"DrawCalls: {_history.Last().DrawCalls}", 10, 30);
        context.DrawText($"Memory: {_history.Last().MemoryUsed / 1024 / 1024}MB", 10, 50);
        
        // Draw frame time graph
        DrawFrameTimeGraph(context);
    }
}
```

**Acceptance Criteria**:

- [ ] BGFX debug features documented
- [ ] Metrics collection system designed
- [ ] Error reporting strategy complete
- [ ] Profiling tool requirements specified
- [ ] Dashboard UI mockups created
- [ ] Frame capture workflow documented
- [ ] Performance baseline expectations defined
- [ ] Debug UI integration planned

### 2.6 Testing Strategy

**Objective**: Plan comprehensive testing for graphics system

**Test Pyramid**:
```
        UI Tests (Manual)
       /                \
      /    Integration   \
     /       Tests        \
    /                      \
   /       Unit Tests       \
  /____________________    \
```

**Tasks**:

- [ ] **Unit Test Plan (Week 5)**
  - Test abstraction layer interfaces
    - Mock graphics device implementation
    - Verify interface contracts
    - Test error handling
  - Test resource lifecycle
    - Buffer creation/deletion
    - Texture upload/download
    - Framebuffer attachment
  - Test state machine
    - State transitions validity
    - Invalid state detection
    - State caching correctness
  
  **Example Unit Tests**:
  ```csharp
  [TestClass]
  public class BufferTests
  {
      [TestMethod]
      public void CreateBuffer_ValidDescription_Succeeds()
      {
          var device = new MockGraphicsDevice();
          var desc = new BufferDescription { Size = 1024, Usage = BufferUsage.Vertex };
          
          var buffer = device.CreateBuffer(desc);
          
          Assert.IsNotNull(buffer);
          Assert.AreEqual(1024u, buffer.Size);
      }
      
      [TestMethod]
      [ExpectedException(typeof(GraphicsException))]
      public void CreateBuffer_ZeroSize_Throws()
      {
          var device = new MockGraphicsDevice();
          var desc = new BufferDescription { Size = 0 };
          
          device.CreateBuffer(desc);
      }
      
      [TestMethod]
      public void SetData_UpdatesBuffer()
      {
          var device = new MockGraphicsDevice();
          var buffer = device.CreateBuffer(...);
          var data = new byte[256];
          
          buffer.SetData(data, 0, 256);
          
          // Verify update was recorded
      }
  }
  ```

- [ ] **Integration Test Plan (Week 6)**
  - Full rendering pipeline tests
    - Veldrid → Abstraction → Results match baseline
    - BGFX → Abstraction → Results match baseline
  - Shader compilation validation
    - All shaders compile successfully
    - Metadata generated correctly
    - Fallback shaders work
  - Multi-threading stress tests
    - Encoder parallelization
    - Race condition detection
    - Frame synchronization
  
  **Integration Test Framework**:
  ```csharp
  public abstract class GraphicsAdapterTest
  {
      protected IGraphicsDevice Device { get; set; }
      
      [TestInitialize]
      public virtual void Setup()
      {
          // Initialize device
      }
      
      [TestMethod]
      public void RenderTriangle_ProducesOutput()
      {
          // Create geometry
          var vertices = new[] { ... };
          var indices = new ushort[] { ... };
          
          // Render
          Device.Clear(ClearOptions.All);
          Device.Draw(new DrawCommand { ... });
          Device.Present();
          
          // Verify framebuffer contains rendered triangle
          var pixels = CaptureFramebuffer();
          Assert.IsTrue(PixelsMatch(pixels, BaselinePixels));
      }
  }
  
  [TestClass]
  public class VeldridAdapterTest : GraphicsAdapterTest
  {
      [TestInitialize]
      public override void Setup() => Device = new VeldridGraphicsDevice(...);
  }
  
  [TestClass]
  public class BgfxAdapterTest : GraphicsAdapterTest
  {
      [TestInitialize]
      public override void Setup() => Device = new BgfxGraphicsDevice(...);
  }
  ```

- [ ] **Performance Test Plan (Week 6)**
  - Design benchmark suite for baseline performance
  - Plan regression detection
  - Document profiling methodology
  - Plan hardware variation handling
  
  **Benchmark Categories**:
  - **Micro-benchmarks**: encoder creation, buffer upload, draw call submission
  - **Macro-benchmarks**: full frame rendering time, memory usage
  - **Regression tests**: compare against baseline, detect >5% regressions
  - **Hardware tests**: test on various GPU vendors

- [ ] **Compatibility Test Plan (Week 6)**
  - Cross-platform testing
    - Windows (NVIDIA, AMD, Intel)
    - macOS (Intel, Apple Silicon)
    - Linux (NVIDIA, AMD, Intel)
  - Feature detection tests
    - Unsupported features fallback gracefully
    - Feature querying works correctly
  - Shader compilation tests
    - All 18 shaders compile
    - Variant creation works
    - Metadata correct

**Test Infrastructure**:

```csharp
// Base test fixture
public abstract class GraphicsTestFixture
{
    protected IGraphicsDevice Device { get; private set; }
    protected IRenderWindow Window { get; private set; }
    protected readonly CommandList Commands = new();
    
    protected virtual void OnSetUp() { }
    
    [TestInitialize]
    public void SetUp()
    {
        Window = new HeadlessRenderWindow(1280, 720);
        Device = CreateGraphicsDevice();
        OnSetUp();
    }
    
    protected abstract IGraphicsDevice CreateGraphicsDevice();
    
    protected void Render(Action<IGraphicsDevice> renderFunc)
    {
        Device.Clear(ClearOptions.All);
        renderFunc(Device);
        Device.Present();
    }
    
    protected byte[] CaptureFramebuffer()
    {
        return Device.ReadFramebuffer();
    }
}
```

**Test Coverage Goals**:

| Category | Target Coverage | Files Affected |
|----------|-----------------|-----------------|
| Unit Tests | >80% | All abstraction interfaces |
| Integration Tests | >70% | Core rendering paths |
| Performance Tests | Baseline established | Critical hot paths |
| Compatibility Tests | 100% platform coverage | Platform-specific code |

**Deliverable**: `Testing_Strategy.md` (20-25 pages)

**Acceptance Criteria**:

- [ ] Unit test framework designed and sample tests written
- [ ] Integration test framework with baseline comparison
- [ ] Performance benchmarks established
- [ ] Cross-platform test matrix defined
- [ ] >80% code coverage target for abstraction layer
- [ ] Regression detection strategy implemented
- [ ] Manual testing checklist created
- [ ] Test data and assets organized
- [ ] CI/CD integration planned

### 2.7 Migration Checklist & Rollback Plan

**Objective**: Ensure safe, reversible migration

**Key Principle**: Never break the main branch. Use feature flags and dual-path rendering to allow parallel development.

**Tasks**:

- [ ] **Feature Flag System (Week 6)**
  - Design feature flag architecture
    - Runtime configuration via `graphicsConfig.json`
    - `GraphicsBackend` enum: `Veldrid` | `BGFX` | `Auto`
    - Per-system feature flags for fine-grained control
  
  - Implementation:
  ```csharp
  public class GraphicsConfiguration
  {
      public GraphicsBackend Backend { get; set; } = GraphicsBackend.Veldrid;
      public Dictionary<string, bool> FeatureFlags { get; set; } = new();
      
      public bool IsFeatureEnabled(string feature) 
          => FeatureFlags.TryGetValue(feature, out var enabled) && enabled;
  }
  
  // Usage
  if (config.Backend == GraphicsBackend.BGFX || config.Backend == GraphicsBackend.Auto)
  {
      device = new BgfxGraphicsDevice(options);
  }
  else
  {
      device = new VeldridGraphicsDevice(options);
  }
  ```
  
  - Conditional compilation fallback:
  ```csharp
  #if BGFX_ENABLED
    var device = new BgfxGraphicsDevice(options);
  #else
    var device = new VeldridGraphicsDevice(options);
  #endif
  ```

- [ ] **Rollback Procedures (Week 6)**
  
  **Step-by-Step Rollback**:
  
  1. **Immediate Rollback** (< 5 minutes):
     - Revert `graphicsConfig.json` to `Backend: Veldrid`
     - Restart application
     - All rendering returns to Veldrid path
  
  2. **Code Rollback** (git operations):
     ```bash
     git revert <commit-hash>  # Revert specific changes
     git checkout main          # Return to last working commit
     ```
  
  3. **Data Recovery**:
     - Frame captures saved in temporary directory
     - Shader cache can be cleared safely
     - Texture/buffer caches automatically rebuild
  
  4. **Communication**:
     - Document rollback reason
     - Notify team immediately
     - Post-mortem analysis
  
  **Identified Risks & Mitigations**:
  - Risk: Shader incompatibility breaks rendering
    - Mitigation: Keep Veldrid path active, test shaders early
  - Risk: Resource handle format incompatible
    - Mitigation: Abstract handles completely in Phase 2
  - Risk: Performance regression goes unnoticed
    - Mitigation: Baseline performance metrics, continuous monitoring
  - Risk: Memory leaks in BGFX adapter
    - Mitigation: Comprehensive resource lifecycle tests

- [ ] **Integration Checkpoints (Week 6-7)**
  
  Define clear milestones with success criteria:
  
  **Checkpoint 1: Abstraction Layer (End Week 5)**
  - [ ] `IGraphicsDevice` interface complete and documented
  - [ ] `VeldridGraphicsDevice` adapter compiles and initializes
  - [ ] Simple triangle rendering works with Veldrid adapter
  - [ ] Unit tests (>50) all passing
  - [ ] Code review approved
  - **Gate Decision**: GO/NO-GO for Phase 3 start
  
  **Checkpoint 2: Component Refactoring (End Week 6)**
  - [ ] Top 5 components refactored to use IGraphicsDevice
  - [ ] Zero behavioral changes observed (regression tests pass)
  - [ ] Performance overhead <2% (profiling validates)
  - [ ] Feature flags working correctly
  - [ ] Documentation complete
  - **Gate Decision**: Ready for BGFX adapter implementation
  
  **Checkpoint 3: Shader Pipeline (End Week 5)**
  - [ ] shaderc integrated successfully
  - [ ] All 18 existing shaders compile to SPIR-V
  - [ ] Metadata generation working
  - [ ] Build time acceptable (<30s for full recompile)
  - [ ] Hot-reload functional in dev builds
  - **Gate Decision**: Ready for shader system refactoring
  
  **Checkpoint 4: Design Review (Week 7)**
  - [ ] All 7 design documents complete (100+ pages)
  - [ ] Team has reviewed all designs
  - [ ] No blocking issues identified
  - [ ] Risk mitigation strategies documented
  - [ ] Timeline validated as realistic
  - [ ] Phase 3 readiness confirmed
  - **Gate Decision**: Formal approval to proceed to Phase 3

**Design Review Sign-Off Template**:

```markdown
## Design Review - Phase 2 (Week 7)

### Attendees
- [ ] Architecture Lead
- [ ] Graphics Lead
- [ ] Project Manager
- [ ] QA Lead

### Documents Reviewed
- [x] Graphics Abstraction Layer Design (40 pages)
- [x] Component Refactoring Plan (25 pages)
- [x] Shader Compilation Pipeline (20 pages)
- [x] Threading Architecture (15 pages)
- [x] Debug Integration Plan (10 pages)
- [x] Testing Strategy (25 pages)
- [x] Migration Checklist (10 pages)

### Approval Status
- [ ] Architecture: APPROVED / CONDITIONAL / REJECTED
- [ ] Technical Feasibility: APPROVED / CONDITIONAL / REJECTED
- [ ] Timeline: APPROVED / CONDITIONAL / REJECTED
- [ ] Risk Assessment: ACCEPTABLE / NEEDS MITIGATION

### Comments & Issues
[Issue list here]

### Conditional Approvals
If any "CONDITIONAL" above, list conditions:
1. ...
2. ...

### Phase 3 Readiness
- [ ] All conditional approvals addressed
- [ ] Risk mitigation plan approved
- [ ] Team trained and prepared
- [ ] Development environment ready
- [ ] **FORMAL GO FOR PHASE 3**

Approved by: ____________  Date: ________
```

**Deliverable**: `Migration_Checklist.md` (10-15 pages) + rollback runbook

**Acceptance Criteria**:

- [ ] Feature flag system fully designed
- [ ] Rollback procedures documented and tested
- [ ] All checkpoints clearly defined with success criteria
- [ ] Risk register complete with mitigations
- [ ] Sign-off template prepared
- [ ] Fallback mechanisms in place
- [ ] No single point of failure in rollback path

## Timeline

| Week | Focus | Deliverable |
|------|-------|------------|
| 4 | Abstraction design, Component analysis | Design documents |
| 5 | Shader pipeline, Threading design | Pipeline documentation |
| 6 | Testing strategy, Integration plan | Test plan, Checklist |
| 7 | Review & refinement | Final architecture docs |

## Design Review

**Week 7 Design Review Gate**:
- [ ] All design documents complete and reviewed
- [ ] Abstraction interfaces approved
- [ ] Refactoring plan is realistic
- [ ] Shader pipeline is viable
- [ ] Testing strategy is comprehensive
- [ ] Risk mitigation strategies are sound
- [ ] Team is ready for Phase 3

## Success Criteria

- [ ] Architecture designs are detailed and implementable
- [ ] All interfaces are well-defined and documented
- [ ] Refactoring strategy minimizes disruption
- [ ] Shader pipeline is integrated with build system
- [ ] Testing strategy covers all critical paths
- [ ] Rollback procedures are documented and tested
- [ ] Team understands architecture and can implement it

## Resources Required

- **Team**: 2-3 Architects + Graphics engineers
- **Tools**: Draw.io/PlantUML for diagrams
- **Time**: 4 weeks of dedicated effort
- **Documents**: 7-10 design specification documents

## Notes

Phase 2 is the most critical planning phase. Thorough, high-quality design will make Phase 3 implementation smoother and reduce costly rework. All team members must understand and agree with the designs before proceeding to Phase 3.

The architecture should be flexible enough to accommodate learning from Phase 1 PoC while being specific enough to guide Phase 3 implementation.
