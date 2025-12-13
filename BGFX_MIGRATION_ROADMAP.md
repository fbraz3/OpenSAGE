# OpenSAGE BGFX Migration Roadmap

**Document Version**: 1.0  
**Date**: December 2025  
**Status**: Planning Phase - Ready for Implementation  
**Architecture**: BGFX-only (removed Veldrid dependency)

---

## Executive Summary

This document outlines the comprehensive strategy to replace OpenSAGE's graphics backend from Veldrid (which has macOS Tahoe/Apple Silicon limitations) to BGFX (cross-platform rendering library with excellent Metal support on macOS).

### Current Blocker

**Veldrid Limitations on macOS Tahoe/Apple Silicon**:
- Metal backend has compatibility issues with latest macOS versions
- Apple Silicon native support incomplete
- Cannot profile or optimize rendering on development machine
- Solution: Migrate to BGFX which has native Metal support prioritized for macOS

### BGFX Advantages

- ✅ **Superior Metal Backend**: Metal is prioritized backend for macOS (score: 20 vs Vulkan: 10)
- ✅ **Apple Silicon Native**: First-class support, actively maintained
- ✅ **Cross-Platform**: Direct3D 11/12, Vulkan, OpenGL, Metal - automatic selection
- ✅ **Async Rendering Model**: Separate API and render threads for better performance
- ✅ **Official C# Bindings**: P/Invoke bindings available in BGFX repository
- ✅ **Battle-Tested**: Used in Minecraft, Guild Wars 2, multiple AAA titles
- ✅ **Active Development**: Regular updates and maintenance

---

## Part 1: Architecture Analysis

### 1.1 BGFX Core Concepts

#### Rendering Model
BGFX uses an **asynchronous, command-based rendering pipeline**:

```
Application Thread (Main Loop)        Render Thread
        |                                   |
        ├─ bgfx::encoderBegin()           |
        ├─ Submit draw commands           |
        │  (setVertexBuffer, setIndexBuffer, etc.)
        ├─ bgfx::encoderEnd()             |
        ├─ bgfx::frame() ─────────────→ Process commands
        |                                  |
        └─ Continue app logic      ←─ Return when done
```

**Key Differences from Veldrid**:
- Veldrid: Synchronous, immediate-mode API (direct GPU commands)
- BGFX: Asynchronous, command queue (deferred execution)

#### Core Components

1. **Views**: Group rendering commands with specific configuration
   - Each view has unique `ViewId` (0-255)
   - Processed in order: `setViewRect()`, `setViewClear()`, `setViewTransform()`
   - Can bind different framebuffers per view
   - Example: View 0 = shadow pass, View 1 = forward pass, View 2 = post-process

2. **Encoders**: Record drawing commands (one per thread)
   - Main thread: `Encoder::submit()` for draw calls
   - Worker threads: `bgfx::encoderBegin(true)` for parallel command recording
   - Thread-safe command queuing

3. **Frame Submission**: `bgfx::frame()`
   - Synchronization point between API and render threads
   - Swaps command buffers
   - Signals render thread to process commands

4. **Resource Handles**: Opaque type-safe handles
   - `VertexBufferHandle`, `IndexBufferHandle`, `TextureHandle`, `ProgramHandle`, etc.
   - Generation-based validation (same pattern as OpenSAGE's `Handle<T>`)
   - BGFX internally manages lifetime

### 1.2 OpenSAGE Rendering Pipeline Mapping

#### Current Veldrid-Based Pipeline

```
Game (Main Loop, 60 Hz)
  ├─ GraphicsSystem.Draw()
  │  └─ RenderPipeline.Execute()
  │     ├─ Shadow Pass (ShadowMapRenderer)
  │     │  └─ Render geometry to shadow texture
  │     ├─ Forward Pass (3D Scene)
  │     │  ├─ Set viewport, clear framebuffer
  │     │  ├─ For each render bucket:
  │     │  │  ├─ Set pipeline (shader + state)
  │     │  │  ├─ Set resource sets (textures, uniforms)
  │     │  │  └─ Draw indexed geometry
  │     │  ├─ Water/Reflection pass
  │     │  └─ Special effects
  │     ├─ 2D Overlay (UI)
  │     │  └─ DrawingContext2D.Render()
  │     └─ Copy intermediate to final framebuffer
  └─ Present framebuffer
```

#### BGFX Mapped Pipeline

```
Game (Main Loop, 60 Hz)
  ├─ Setup Views (0, 1, 2, 3...)
  │  ├─ View 0: Shadow Pass
  │  │  ├─ setViewFrameBuffer(shadowTexture)
  │  │  ├─ setViewClear() for depth
  │  │  ├─ setViewTransform() (light matrix)
  │  │  ├─ Encoder::submit() for shadow geometry
  │  │  └─ Results in ShadowTexture
  │  │
  │  ├─ View 1: Forward Pass
  │  │  ├─ setViewFrameBuffer(intermediateTexture)
  │  │  ├─ setViewRect(), setViewClear()
  │  │  ├─ setViewTransform() (camera matrix)
  │  │  ├─ Encoder::setTexture(shadowTexture)
  │  │  ├─ Encoder::submit() for scene geometry
  │  │  └─ Results in IntermediateTexture
  │  │
  │  ├─ View 2: 2D Overlay
  │  │  ├─ setViewFrameBuffer(defaultFB)
  │  │  ├─ setViewTransform() (ortho matrix)
  │  │  ├─ Encoder::submit() for UI geometry
  │  │  └─ Results in Display
  │
  ├─ bgfx::frame() ← Submit all views to render thread
  │
  └─ Render thread processes asynchronously
     ├─ Execute View 0: Shadow pass
     ├─ Execute View 1: Forward pass
     ├─ Execute View 2: 2D overlay
     └─ Present to display
```

### 1.3 Shader Compilation Pipeline

#### Current OpenSAGE/Veldrid Approach
```
GLSL Source (.vert, .frag)
    ↓ (glslangValidator via MSBuild)
SPIR-V Bytecode (.spv)
    ↓ (Veldrid.SPIRV at runtime)
Backend-specific (HLSL, MSL, GLSL, etc.)
    ↓ (Platform compiler - D3DCompiler, etc.)
Executable Shader
```

#### BGFX Shader Approach
```
GLSL-like Source (.sc file)
    ↓ (BGFX shaderc tool - offline)
Optimized Shader Binary (.bin)
    ↓ (bgfx::createShader at runtime)
Stored in BGFX's shader cache
```

**Key Differences**:
- BGFX uses offline compilation with `shaderc` tool
- Shaders are platform-independent binaries (one .bin for all backends)
- BGFX backend automatically compiles binary to target format at runtime
- No runtime cross-compilation needed (SPIR-V layer not required)
- GLSL-like syntax with BGFX-specific macros for samplers, varying semantics

### 1.4 Resource Management Mapping

| OpenSAGE (Veldrid) | BGFX Equivalent | Notes |
|---|---|---|
| `DeviceBuffer` | `VertexBufferHandle` / `IndexBufferHandle` | Veldrid uses explicit type, BGFX uses typed handles |
| `Texture` | `TextureHandle` | Same concept, different API |
| `Framebuffer` | View + `FrameBufferHandle` | BGFX views are more powerful |
| `Pipeline` | `ProgramHandle` + State | BGFX separates shader program from state |
| `ResourceSet` | Implicit via `setTexture()`, `setUniform()` | BGFX doesn't have explicit resource sets |
| `CommandList` | `Encoder` | BGFX encoder per-thread |
| `GraphicsDevice` | `bgfx::*` functions | Namespace-based API |

---

## Part 2: Implementation Strategy

### 2.1 Phased Approach

The migration is broken into **4 phases** spread across ~8-10 weeks:

#### Phase A: Foundation (Weeks 1-2)
- [ ] Build/integrate BGFX native library
- [ ] Create C# P/Invoke bindings wrapper
- [ ] Implement platform initialization (Metal on macOS)
- [ ] Create basic BgfxGraphicsDevice adapter

#### Phase B: Core Graphics (Weeks 3-5)
- [ ] Resource management (buffers, textures, framebuffers)
- [ ] Shader compilation pipeline (shaderc integration)
- [ ] Pipeline state management
- [ ] Basic rendering (quad test)

#### Phase C: Engine Integration (Weeks 6-7)
- [ ] Integrate BgfxGraphicsDevice into Game.cs
- [ ] Refactor RenderPipeline for BGFX views
- [ ] Adapt all ShaderResources classes
- [ ] Convert all shaders to BGFX format

#### Phase D: Validation & Optimization (Weeks 8-10)
- [ ] Functional testing (all game modes)
- [ ] Cross-platform verification
- [ ] Performance profiling and optimization
- [ ] Release preparation

### 2.2 Code Structure

#### New Directory Structure
```
src/OpenSage.Graphics/
├── BGFX/                              # New: BGFX-specific implementation
│   ├── BgfxGraphicsDevice.cs          # Main adapter (replaces Veldrid)
│   ├── BgfxPlatformData.cs            # Platform initialization
│   ├── BgfxShaderCompiler.cs          # shaderc integration
│   ├── BgfxResourceManager.cs         # Buffer/texture/framebuffer lifecycle
│   ├── BgfxPipelineState.cs           # Pipeline state objects
│   ├── BgfxViewManager.cs             # View configuration and ordering
│   └── Native/
│       ├── bgfx.cs                    # P/Invoke bindings (auto-generated)
│       └── bgfx_*.dll/.dylib/.so      # Native BGFX libraries
├── Abstractions/                      # Keep: IGraphicsDevice interface
│   └── IGraphicsDevice.cs             # Already designed
├── Shaders/                           # BGFX shader compilation
│   ├── shaders.sc                     # BGFX shader source files
│   └── build/                         # Compiled .bin files
└── (Remove Veldrid/ directory)        # No longer needed
```

#### Key Classes to Create

1. **BgfxGraphicsDevice.cs** (1000+ lines)
   - Implements `IGraphicsDevice`
   - Manages BGFX initialization/shutdown
   - Wraps all BGFX resource creation
   - Manages encoder and frame submission

2. **BgfxShaderCompiler.cs** (500+ lines)
   - Integrates `shaderc` tool
   - Converts OpenSAGE shaders to BGFX format
   - MSBuild task for shader compilation
   - Shader binary caching

3. **BgfxViewManager.cs** (300+ lines)
   - Maps OpenSAGE render passes to BGFX views
   - Manages view ordering and configuration
   - Handles view<→framebuffer binding

### 2.3 BGFX Library Integration

#### Building BGFX Native Library

BGFX must be compiled for:
- **macOS**: Metal backend + x86_64 + Apple Silicon (arm64)
- **Windows**: Direct3D 11 + Vulkan (x86_64)
- **Linux**: Vulkan + OpenGL (x86_64)

**Options**:
1. **Pre-built binaries** (easiest): Download from BGFX releases or build once, commit to repo
2. **NuGet package**: Package compiled libraries (if not available)
3. **Submodule + build**: Add BGFX as git submodule, build via premake

**Recommendation**: Pre-built binaries in `lib/bgfx/` with platform-specific folders:
```
lib/bgfx/
├── macos/
│   ├── arm64/
│   │   ├── libbgfx.dylib
│   │   └── shaderc (tool)
│   └── x86_64/
│       ├── libbgfx.dylib
│       └── shaderc
├── windows/
│   └── x64/
│       ├── bgfx.dll
│       ├── bgfx_debug.dll
│       └── shaderc.exe
└── linux/
    └── x64/
        ├── libbgfx.so
        └── shaderc
```

#### P/Invoke Binding Generation

BGFX provides `scripts/bgfx.idl` that auto-generates C# bindings:
```bash
# Generate C# bindings
python scripts/idl.py --lang cs --output bindings/cs/bgfx.cs
```

These bindings provide:
- `[DllImport("bgfx")]` attributes
- Enums: `RendererType`, `TextureFormat`, `StateFlags`, etc.
- Functions: `bgfx_init()`, `bgfx_create_texture()`, `bgfx_submit()`, etc.

---

## Part 3: Detailed Implementation Phases

### Phase A: Foundation (Weeks 1-2)

#### Week 1: BGFX Integration & Platform Setup

**Tasks**:

1. **Acquire BGFX Libraries** (3 hours)
   - Download/build BGFX for macOS, Windows, Linux
   - Place in `lib/bgfx/[platform]/[arch]/`
   - Update .gitignore if binaries committed

2. **Create P/Invoke Bindings** (4 hours)
   - Generate or copy `bgfx.cs` from BGFX repository
   - Location: `src/OpenSage.Graphics/BGFX/Native/bgfx.cs`
   - Remove Veldrid dependencies from projects

3. **Create BgfxPlatformData.cs** (6 hours)
   - Initialize BGFX platform-specific data (window handle, etc.)
   - macOS: Metal layer setup
   - Windows: HWND + Direct3D device
   - Linux: Window handle + Vulkan surface
   - Implement `PlatformData` struct wrapping

4. **Create BgfxGraphicsDevice Skeleton** (8 hours)
   - Implement `IGraphicsDevice` interface (stub methods)
   - Initialize BGFX in constructor:
     ```csharp
     bgfx_init(&initData);  // C# P/Invoke call
     ```
   - Implement `BeginFrame()` and `EndFrame()`
   - Implement `frame()` submission (single-threaded initially)
   - Add basic error handling and logging

**Deliverables**:
- `src/OpenSage.Graphics/BGFX/Native/bgfx.cs` (P/Invoke)
- `src/OpenSage.Graphics/BGFX/BgfxPlatformData.cs`
- `src/OpenSage.Graphics/BGFX/BgfxGraphicsDevice.cs` (skeleton, ~200 lines)
- Game initializes with BGFX backend (displays blank window)

**Tests**:
- `BgfxInitializationTests.cs`: Verify BGFX initializes on each platform

#### Week 2: Core Resource Management

**Tasks**:

1. **Implement Buffer Management** (8 hours)
   - `CreateBuffer()`: Allocate vertex/index/uniform buffers
   - `DestroyBuffer()`: Release buffers
   - `UpdateBuffer()`: Dynamic buffer updates
   - Handle management using dictionary + generation ID
   - `BgfxResourceManager.cs` (primary class)

2. **Implement Texture Management** (8 hours)
   - `CreateTexture()`: Allocate 2D/3D textures
   - `DestroyTexture()`: Release textures
   - `UpdateTexture()`: Partial updates
   - Texture format mapping (OpenSAGE `TextureFormat` → BGFX)
   - Mipmapping support

3. **Implement Framebuffer Management** (6 hours)
   - `CreateFramebuffer()`: Multi-target framebuffers
   - `DestroyFramebuffer()`: Release resources
   - Attach textures as render targets
   - Default framebuffer handling (backbuffer)

4. **Add Resource Tests** (5 hours)
   - Unit tests for buffer creation/destruction
   - Texture allocation and update tests
   - Framebuffer binding tests

**Deliverables**:
- `src/OpenSage.Graphics/BGFX/BgfxResourceManager.cs` (300+ lines)
- Resource lifecycle tests
- Game can create/destroy graphics resources

**Tests**:
- `BgfxResourceManagementTests.cs`: Comprehensive resource lifecycle

### Phase B: Core Graphics (Weeks 3-5)

#### Week 3: Shader Compilation Pipeline

**Tasks**:

1. **Build shaderc Tool Integration** (10 hours)
   - Create `BgfxShaderCompiler.cs`
   - Execute `shaderc` tool via `System.Diagnostics.Process`
   - Handle platform-specific tool paths (Windows .exe, macOS/Linux binary)
   - Convert OpenSAGE `.vert`/`.frag` GLSL to BGFX `.sc` format:
     - Replace `#version 450` with BGFX directives
     - Convert `uniform` to BGFX uniform syntax
     - Handle varying definitions (use `varying.def.sc`)
   - Compile to `.bin` files
   - Cache compiled shaders

2. **Create Varying Definition File** (4 hours)
   - `src/OpenSage.Graphics/Shaders/varying.def.sc`
   - Define vertex semantics for OpenSAGE geometry formats
   - Map to GLSL varying declarations

3. **Convert Existing Shaders** (8 hours)
   - Analyze current GLSL shaders in `src/OpenSage.Game/Graphics/Shaders/`
   - Convert ~18 shader files to BGFX format
   - Test each conversion for correctness
   - Maintain shadow, normal, and material variations

4. **Create MSBuild Task** (6 hours)
   - MSBuild target for shader compilation
   - Integrated into project build process
   - Automatic `.sc` → `.bin` conversion
   - Embed `.bin` files in assembly as resources

**Deliverables**:
- `src/OpenSage.Graphics/BGFX/BgfxShaderCompiler.cs` (500+ lines)
- `src/OpenSage.Graphics/Shaders/varying.def.sc`
- Converted shader files (18+ `.sc` files)
- `.bin` files for each shader

**Tests**:
- `BgfxShaderCompilationTests.cs`: Verify shader compilation
- Visual test: Simple quad with texture rendering

#### Week 4: Pipeline State & Rendering

**Tasks**:

1. **Create Pipeline State Objects** (8 hours)
   - `BgfxPipelineState.cs`: Wraps shader program + state
   - Map OpenSAGE state objects to BGFX state flags:
     - `RasterState` → `BGFX_STATE_*` (cull, fill, depth)
     - `DepthState` → `BGFX_STATE_DEPTH_*`
     - `BlendState` → `BGFX_STATE_BLEND_*`
     - `StencilState` → `BGFX_STATE_STENCIL_*`
   - Pre-combine state flags for efficiency

2. **Implement Draw Commands** (10 hours)
   - `DrawIndexed()`: Submit indexed geometry
   - `DrawVertices()`: Submit non-indexed geometry
   - `DrawIndexedIndirect()`: Indirect draw calls
   - Set uniforms: `SetUniform()` implementation
   - Set textures: `SetTexture()` with sampler binding
   - Implement view-based rendering:
     - `SetRenderTarget()` → `bgfx::setViewFrameBuffer()`
     - `SetViewport()` → `bgfx::setViewRect()`
     - `SetScissor()` → `bgfx::setViewScissor()`

3. **Encoder & Command Recording** (7 hours)
   - Thread-local encoder management
   - `bgfx::encoderBegin()` / `bgfx::encoderEnd()`
   - Command queueing for deferred execution
   - Handle encoder lifecycle properly

4. **Frame Submission** (5 hours)
   - Implement `bgfx::frame()` call
   - Manage frame buffering (triple buffering)
   - Handle frame timing

**Deliverables**:
- `src/OpenSage.Graphics/BGFX/BgfxPipelineState.cs` (200+ lines)
- `src/OpenSage.Graphics/BGFX/BgfxGraphicsDevice.cs` expanded (500+ lines)
- Basic rendering test (triangle + texture)

**Tests**:
- `BgfxRenderingTests.cs`: Triangle, textured quad, multiple objects

#### Week 5: View Management & Multi-Pass

**Tasks**:

1. **Create BgfxViewManager** (8 hours)
   - Manage view IDs (0-255)
   - `SetupView()`: Configure view parameters
     - Viewport, scissor, clear color/depth
     - Transform matrices (view, projection)
     - Framebuffer binding
   - `SetViewOrder()`: Explicit rendering order
   - Clear state between views

2. **Implement Multi-Pass Rendering** (10 hours)
   - View 0: Shadow pass
   - View 1: Forward pass (3D)
   - View 2: Post-processing
   - View 3: 2D overlay
   - Proper view sequencing and dependencies

3. **Memory & Transient Buffers** (6 hours)
   - Implement `bgfx_alloc()` for transient geometry
   - Dynamic vertex/index buffers
   - Optimize for frame-based allocation

4. **Integration Tests** (6 hours)
   - Test multi-view rendering pipeline
   - Validate view ordering
   - Performance baseline capture

**Deliverables**:
- `src/OpenSage.Graphics/BGFX/BgfxViewManager.cs` (300+ lines)
- Multi-pass rendering functional
- Performance profiling baseline

**Tests**:
- `BgfxMultiPassTests.cs`: Shadow pass, forward pass, post-process

---

### Phase C: Engine Integration (Weeks 6-7)

#### Week 6: RenderPipeline Refactoring

**Tasks**:

1. **Refactor RenderPipeline** (12 hours)
   - Map existing passes to BGFX views:
     - Current: `ShadowMapRenderer.Render()` → View 0
     - Current: 3D forward pass → View 1
     - Current: Water rendering → Part of View 1
     - Current: 2D overlay → View 2
   - Update `Execute()` method to use views instead of CommandList
   - Remove Veldrid `CommandList` references
   - Replace `Pipeline` + `ResourceSet` with BGFX equivalents

2. **Refactor ShaderResources Classes** (12 hours)
   - Update `GlobalShaderResources`
   - Update `MeshShaderResources`
   - Update `TerrainShaderResources`
   - Update `WaterShaderResources`
   - Update `ParticleShaderResources`
   - Replace `Pipeline` creation with `BgfxPipelineState`
   - Replace `ResourceSet` binding with `setTexture()` / `setUniform()`

3. **Update Scene3D Rendering** (8 hours)
   - Adapt `DoRenderPass()` to use BGFX views
   - Update `RenderBucket` iteration
   - Replace `CommandList.SetPipeline()` with `bgfx::setProgram()`
   - Replace `CommandList.DrawIndexed()` with `Encoder::submit()`

**Deliverables**:
- `RenderPipeline.cs` refactored for BGFX
- All `ShaderResources` classes updated
- Scene3D rendering working with BGFX views

**Tests**:
- `BgfxRenderPipelineTests.cs`: Full rendering pipeline functional

#### Week 7: Complete Engine Integration

**Tasks**:

1. **Update StandardGraphicsResources** (6 hours)
   - Replace Veldrid texture creation with BGFX
   - Update sampler creation
   - Update default textures

2. **Update ContentManager** (6 hours)
   - Replace Veldrid device with BGFX
   - Verify texture/shader loading

3. **Remove Veldrid Dependency** (8 hours)
   - Delete `src/OpenSage.Graphics/Veldrid/` directory
   - Remove Veldrid NuGet packages from .csproj
   - Update using statements throughout codebase
   - Fix compilation errors

4. **Tool Integration Testing** (6 hours)
   - Verify BigEditor tool works
   - Test map rendering in tools
   - Verify all tools function correctly

**Deliverables**:
- Veldrid completely removed
- All tools and systems using BGFX
- Solution builds with 0 errors

**Tests**:
- Full solution build test
- Tool functionality tests
- Integration tests

---

### Phase D: Validation & Optimization (Weeks 8-10)

#### Week 8: Functional Testing

**Tasks**:

1. **Game Playability Testing** (10 hours)
   - Play through all game modes
   - Verify graphics rendering correctness
   - Test all unit types, structures, terrain
   - Verify shadows, water, particle effects
   - Verify HUD/UI rendering

2. **Visual Regression Testing** (8 hours)
   - Capture reference images (Veldrid-based if possible, or BGFX baseline)
   - Compare with current rendering
   - Document any visual differences

3. **Platform Testing** (6 hours)
   - macOS: Metal backend verification
   - Windows: Direct3D 11 verification
   - Linux: Vulkan/OpenGL verification

**Deliverables**:
- Playability verification document
- Visual regression baseline
- Platform compatibility report

#### Week 9: Performance Profiling & Optimization

**Tasks**:

1. **CPU Profiling** (8 hours)
   - Use `PerformanceProfiler.cs` from Week 25
   - Profile frame time with BGFX backend
   - Identify hot paths
   - Compare with Veldrid baseline (if available)

2. **GPU Profiling** (8 hours)
   - Use `GpuPerformanceMetrics.cs` from Week 25
   - Profile draw calls, state changes, render time
   - RenderDoc analysis on macOS/Windows

3. **Optimization Passes** (8 hours)
   - Reduce state changes
   - Optimize view ordering
   - Cache pipeline states
   - Reduce allocations
   - Batch rendering where possible

**Deliverables**:
- Performance profiling report
- Optimization improvements documented
- Frame time target: <16.67ms (60 FPS)

#### Week 10: Release Preparation

**Tasks**:

1. **Final Testing & Bug Fixes** (10 hours)
   - Address any remaining issues
   - Final playability testing
   - Cross-platform verification

2. **Documentation** (8 hours)
   - Update developer guide for BGFX backend
   - Document shader compilation process
   - Create BGFX integration guide
   - Update README

3. **Release & Cleanup** (6 hours)
   - Final build verification
   - Tag release version
   - Create release notes

**Deliverables**:
- Production-ready BGFX backend
- Complete documentation
- Release notes

---

## Part 4: Migration Checklist

### Pre-Migration
- [ ] BGFX research complete (✅ Done)
- [ ] Architecture designed (✅ Done)
- [ ] Team alignment on approach
- [ ] Backup of current Veldrid implementation

### Phase A (Weeks 1-2)
- [ ] BGFX libraries built/acquired
- [ ] P/Invoke bindings created
- [ ] Platform initialization working
- [ ] Game initializes with BGFX (blank window)
- [ ] Builds with 0 errors

### Phase B (Weeks 3-5)
- [ ] Buffer/texture/framebuffer management complete
- [ ] Shader compilation pipeline working
- [ ] Basic rendering (triangle + texture) functional
- [ ] Multi-pass rendering implemented
- [ ] Performance baselines captured

### Phase C (Weeks 6-7)
- [ ] RenderPipeline fully refactored
- [ ] All ShaderResources updated
- [ ] Veldrid completely removed
- [ ] Tools working correctly
- [ ] Solution builds with 0 errors

### Phase D (Weeks 8-10)
- [ ] All game modes playable
- [ ] Visual regressions addressed
- [ ] Cross-platform testing complete
- [ ] Performance optimized
- [ ] Documentation complete
- [ ] Ready for release

---

## Part 5: Risk Analysis & Mitigation

### High-Risk Areas

| Risk | Probability | Impact | Mitigation |
|------|---|---|---|
| BGFX shader compilation issues | Medium (50%) | High (schedule) | Start Week 3, thorough conversion testing |
| Platform-specific rendering bugs | Medium (60%) | Medium (schedule) | Early cross-platform testing (Week 6) |
| Performance regression | Low (30%) | High (release blocker) | Weekly profiling, optimization early |
| RenderPipeline refactoring complexity | Medium (70%) | High (schedule) | Detailed design, incremental refactoring |

### Mitigation Strategies

1. **Weekly Builds**: Ensure solution builds every week with 0 errors
2. **Early Testing**: Test each phase before moving to next
3. **Parallel Validation**: Test on all platforms weekly
4. **Performance Checkpoints**: Profile every 2 weeks
5. **Documentation**: Keep migration doc updated with findings

---

## Part 6: Resource Requirements

### Hardware
- macOS machine: Metal backend testing
- Windows machine: Direct3D 11 testing  
- Linux machine (optional): Vulkan testing
- GPU: NVIDIA, AMD, Intel for compatibility

### Software Tools
- BGFX repository (clone for source)
- shaderc tool (shader compiler)
- RenderDoc (GPU debugging)
- Performance profilers (dotTrace, etc.)

### Personnel
- 1 senior engineer (Weeks 1-5, part-time guidance)
- 1-2 graphics engineers (Weeks 1-10, full-time)
- 1 QA engineer (Weeks 8-10)

### Time Estimate
- **Total**: 8-10 weeks (320-400 hours)
- **Phase A**: 2 weeks
- **Phase B**: 3 weeks
- **Phase C**: 2 weeks
- **Phase D**: 2-3 weeks

---

## Part 7: Success Criteria

### Technical Success
- ✅ Game runs with BGFX backend on macOS, Windows, Linux
- ✅ All graphics features functional (shadows, water, particles, effects)
- ✅ Frame rate: 60+ FPS on target hardware
- ✅ No visual regressions vs baseline
- ✅ Build: 0 errors, <10 warnings

### Process Success
- ✅ All phases completed on schedule
- ✅ Code review approved
- ✅ Test coverage: >90%
- ✅ Documentation: 100% complete

### Performance Success
- ✅ Average frame time: <16.67ms (60 FPS)
- ✅ 99th percentile frame time: <20ms
- ✅ GPU utilization: >80%
- ✅ Memory usage: <512 MB

---

## Appendix A: BGFX API Quick Reference

### Initialization
```csharp
// Initialize BGFX
bgfx_init_t init = new bgfx_init_t();
init.platformData = platformData;
init.type = bgfx_renderer_type_t.BGFX_RENDERER_TYPE_COUNT; // Auto-select
init.resolution.width = 1920;
init.resolution.height = 1080;
bgfx_init(&init);

// Frame submission
bgfx_frame(false);

// Shutdown
bgfx_shutdown();
```

### Resources
```csharp
// Create vertex buffer
bgfx_vertex_buffer_handle_t vbh = bgfx_create_vertex_buffer(mem, &vertexLayout);

// Create texture
bgfx_texture_handle_t texHandle = bgfx_create_texture_2d(width, height, flags, format, mem);

// Create shader
bgfx_shader_handle_t shaderHandle = bgfx_create_shader(shaderBinary);

// Create program
bgfx_program_handle_t programHandle = bgfx_create_program(vsh, fsh);
```

### Rendering
```csharp
// Set view configuration
bgfx_set_view_rect(viewId, x, y, width, height);
bgfx_set_view_clear(viewId, clearFlags, rgba, depth, stencil);
bgfx_set_view_transform(viewId, viewMtx, projMtx);
bgfx_set_view_frame_buffer(viewId, fbh);

// Submit draw call
bgfx_encoder_t encoder = bgfx_encoder_begin(false);
bgfx_encoder_set_vertex_buffer(encoder, 0, vbh, 0, count);
bgfx_encoder_set_index_buffer(encoder, ibh);
bgfx_encoder_set_state(encoder, state);
bgfx_encoder_submit(encoder, viewId, programHandle, depth);
bgfx_encoder_end(encoder);
```

---

## Appendix B: Shader Migration Example

### Original OpenSAGE GLSL
```glsl
#version 450

layout(location = 0) in vec3 vPos;
layout(location = 1) in vec3 vNormal;
layout(location = 2) in vec2 vTexCoord;

uniform GlobalData {
    mat4 viewProj;
    vec3 lightDir;
};

layout(binding = 0) uniform sampler2D diffuseTexture;

layout(location = 0) out vec4 outColor;

void main() {
    vec4 texColor = texture(diffuseTexture, vTexCoord);
    float lighting = max(0.0, dot(vNormal, lightDir));
    outColor = texColor * lighting;
}
```

### Converted BGFX Format (.sc file)
```glsl
// varying.def.sc defines these:
// vec3 vPos
// vec3 vNormal  
// vec2 vTexCoord

// uniforms
uniform mat4 viewProj;
uniform vec3 lightDir;

// samplers (BGFX-specific)
uniform sampler2D diffuseTexture;

// shader source follows GLSL but uses varying.def.sc
void main() {
    vec4 texColor = texture2D(diffuseTexture, vTexCoord);
    float lighting = max(0.0, dot(vNormal, lightDir));
    gl_FragColor = texColor * vec4(lighting, lighting, lighting, 1.0);
}
```

### Compilation
```bash
shaderc -f shader.sc -o shader.bin --type fragment --platform macos -p metal
```

---

## References & Resources

- **BGFX Documentation**: https://bkaradzic.github.io/bgfx/
- **BGFX GitHub**: https://github.com/bkaradzic/bgfx
- **BGFX Examples**: https://github.com/bkaradzic/bgfx/tree/master/examples
- **BGFX C# Bindings**: https://github.com/bkaradzic/bgfx/tree/master/bindings/cs
- **OpenSAGE Repository**: https://github.com/OpenSAGE/OpenSAGE

---

**Document Prepared**: December 2025  
**Status**: Ready for Phase A Implementation  
**Next Step**: Begin Week 1 of Phase A (BGFX Foundation)
