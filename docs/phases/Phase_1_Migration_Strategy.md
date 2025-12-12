# Phase 1.3: Migration Strategy & Architecture Design

**Date**: December 12, 2025  
**Document Type**: Technical Architecture & Implementation Strategy  
**Prepared For**: BGFX Graphics Engine Integration  
**Classification**: OpenSAGE Architecture Planning  

---

## Executive Summary

This document outlines the strategic approach to migrate OpenSAGE from Veldrid to BGFX graphics backend. The strategy emphasizes incremental, low-risk migration with parallel abstraction layer development. Key principles: maintain feature parity, minimize game code changes, validate at each phase, and enable rollback capability.

**Migration Approach**: Layered abstraction with incremental porting  
**Timeline**: 8-10 weeks (2-3 developer team)  
**Risk Level**: MEDIUM (well-mitigated via phased approach)  
**Success Probability**: HIGH (90%+)

---

## 1. Strategic Architecture Overview

### 1.1 Abstraction Layer Pattern

The migration strategy uses a **Graphics Abstraction Layer** to isolate BGFX changes from game code:

```
OpenSage.Game
├─ Game.cs (game loop - NO CHANGE)
├─ GameLogic.cs (game state - NO CHANGE)
├─ Scene3D.cs (3D world - NO CHANGE)
└─ Graphics/
   └─ IGraphicsDevice (abstraction)
       ├─ VeldridGraphicsDevice (current)
       ├─ BgfxGraphicsDevice (new)
       └─ RenderPipeline.cs (minimal changes)
```

### 1.2 Phased Migration Model

**Phase 1 (Research)**: ✅ COMPLETE
- Technical feasibility validated
- Architecture patterns documented
- Requirements specified

**Phase 2 (PoC & Bindings)**: → NEXT
- Create BGFX C# bindings
- Implement hello-world rendering
- Validate shader compilation
- Establish performance baseline

**Phase 3 (Core Integration)**: 
- Implement graphics device abstraction
- Port render pipeline
- Full feature validation
- Cross-platform testing

**Phase 4 (Optimization)**:
- Multi-threading support
- Performance tuning
- Advanced features (ImGui, post-effects)
- Final integration

---

## 2. System Decomposition

### 2.1 Current System Components

```
Graphics System Hierarchy:

GraphicsSystem (IGameSystem)
├─ RenderPipeline
│  ├─ Shadow Pass Renderer
│  ├─ Forward Pass Renderer
│  ├─ Transparent Pass Renderer
│  ├─ Water Pass Renderer
│  └─ Texture Copier
├─ RenderScene
│  ├─ RenderBucket[Shadow]
│  ├─ RenderBucket[Opaque]
│  ├─ RenderBucket[Transparent]
│  └─ RenderBucket[Water]
├─ ShaderSet Managers (9 types)
│  ├─ TerrainShaderResources
│  ├─ RoadShaderResources
│  ├─ ObjectShaderResources
│  ├─ ParticleShaderResources
│  ├─ WaterShaderResources
│  ├─ SpriteShaderResources
│  ├─ SimpleShaderResources
│  ├─ FixedFunctionShaderResources
│  └─ ImGuiShaderResources
└─ Device Management
   └─ GraphicsDeviceUtility
   └─ ContentManager (texture/model loading)
```

### 2.2 Porting Priority Order

**Tier 1 (CRITICAL - Must have for PoC)**:
1. Graphics Device Abstraction (IGraphicsDevice interface)
2. BGFX C# Bindings (P/Invoke layer)
3. Basic Rendering (setView, setShader, submit)
4. Buffer Management (vertex, index, texture)
5. Shader Loading (pre-compiled binary loading)

**Tier 2 (HIGH - Required for Phase 3)**:
6. Render Pipeline Orchestration
7. Shadow Mapping
8. Material System & Binding
9. Framebuffer Management
10. State Management (pipeline caching)

**Tier 3 (MEDIUM - Phase 3-4)**:
11. Water Rendering
12. Particle System
13. ImGui Integration
14. Post-Processing Effects
15. Optimization (multi-threading, batching)

### 2.3 Dependency Graph

```
Tier 1 Dependencies:
- IGraphicsDevice ← P/Invoke Bindings ← BGFX native DLL

Tier 2 Dependencies:
- RenderPipeline ← IGraphicsDevice ← Tier 1
- ShaderResources ← IGraphicsDevice ← Tier 1
- FramebufferManager ← IGraphicsDevice ← Tier 1

Tier 3 Dependencies:
- Water, Particles, ImGui ← RenderPipeline ← Tier 2
- Advanced Features ← State Management ← Tier 2
```

---

## 3. Implementation Architecture

### 3.1 Graphics Abstraction Interface

**File**: `src/OpenSage.Graphics.BGFX/IGraphicsDevice.cs`

```csharp
/// <summary>
/// Abstract graphics device interface supporting multiple backends
/// (Veldrid, BGFX, etc.)
/// </summary>
public interface IGraphicsDevice : IDisposable
{
    // Frame management
    void BeginFrame();
    void EndFrame();
    void Present();

    // View/viewport management
    void SetViewport(int x, int y, int width, int height);
    void SetView(string viewName, Matrix4x4 viewMatrix, Matrix4x4 projMatrix);
    
    // Clearing
    void Clear(Color color, float depth = 1f, uint stencil = 0);
    void ClearColor(Color color);
    void ClearDepth(float depth);

    // State management
    void SetBlendMode(BlendMode mode);
    void SetDepthTest(DepthTestMode mode);
    void SetCulling(CullingMode mode);
    
    // Resource creation
    IShader CreateShader(string name, ShaderDescription desc);
    ITexture CreateTexture(TextureDescription desc);
    IBuffer CreateBuffer(BufferDescription desc);
    IFramebuffer CreateFramebuffer(FramebufferDescription desc);
    
    // Command submission
    void Submit(RenderCommand[] commands);
    void SubmitBatch(RenderBatch batch);
    
    // Statistics
    RenderStatistics GetStatistics();
}
```

### 3.2 BGFX Binding Structure

**Directory**: `src/OpenSage.Graphics.BGFX/`

```
OpenSage.Graphics.BGFX/
├── Native/
│   ├── Bgfx.cs (P/Invoke bindings)
│   ├── BgfxDefines.cs (constants)
│   ├── BgfxStructs.cs (data structures)
│   ├── BgfxEnums.cs (enumerations)
│   └── BgfxCallbacks.cs (callback marshaling)
├── Core/
│   ├── BgfxDevice.cs (IGraphicsDevice implementation)
│   ├── BgfxShader.cs (IShader implementation)
│   ├── BgfxTexture.cs (ITexture implementation)
│   ├── BgfxBuffer.cs (IBuffer implementation)
│   └── BgfxFramebuffer.cs (IFramebuffer implementation)
├── Resources/
│   ├── ShaderCompiler.cs (shaderc integration)
│   ├── ShaderLoader.cs (binary shader loading)
│   └── ResourceCache.cs (caching layer)
└── Utilities/
    ├── BgfxUtil.cs (helper functions)
    └── BgfxDebug.cs (debugging utilities)
```

### 3.3 Migration Phases Detail

#### Phase 2A: Bindings & Hello-World (Week 1-2)

**Goal**: Get BGFX rendering "something" on screen

**Deliverables**:
- [ ] Complete P/Invoke bindings (320+ functions)
- [ ] Hello-world triangle rendering
- [ ] Frame pacing (60 FPS)
- [ ] Platform validation (Windows + macOS at minimum)

**Code Structure**:
```csharp
// BgfxDevice.cs - Main graphics device implementation
public class BgfxDevice : IGraphicsDevice
{
    private uint _frame = 0;
    
    public void BeginFrame()
    {
        // bgfx::frame() - process frame
        Bgfx.frame();
    }
    
    public void EndFrame()
    {
        _frame++;
    }
    
    public void Submit(RenderCommand[] commands)
    {
        foreach (var cmd in commands)
        {
            // Set state, bindings, etc.
            Bgfx.setTransform(ref cmd.Transform);
            Bgfx.setVertexBuffer(0, cmd.VertexBuffer.Handle);
            Bgfx.setIndexBuffer(cmd.IndexBuffer.Handle);
            Bgfx.submit(cmd.ViewId, cmd.ShaderHandle);
        }
    }
}
```

**Testing**:
- RenderDoc validation on Windows
- Metal profiler on macOS
- Frame time measurement

#### Phase 2B: Shader System (Week 2-3)

**Goal**: Load and compile shaders, establish compilation pipeline

**Deliverables**:
- [ ] Shader compilation build target
- [ ] shaderc integration
- [ ] Binary shader loading
- [ ] Shader hot-reload (offline recompile)

**Build System Integration**:
```xml
<!-- In csproj file -->
<Target Name="CompileShaders" BeforeBuild="true">
    <ItemGroup>
        <ShaderSource Include="Assets/Shaders/**/*.glsl" />
    </ItemGroup>
    
    <Exec Command="shaderc -i Assets/Shaders -o Assets/Shaders/Compiled -f bin" />
    
    <ItemGroup>
        <EmbeddedResource Include="Assets/Shaders/Compiled/**/*.bin" />
    </ItemGroup>
</Target>
```

**Code**:
```csharp
// ShaderLoader.cs
public class ShaderLoader
{
    public static ShaderHandle LoadShader(string name, RendererType renderer)
    {
        // Load precompiled shader binary
        byte[] binary = LoadEmbeddedResource($"Shaders/Compiled/{name}_{renderer}.bin");
        var memory = Bgfx.makeRef(binary);
        return Bgfx.createShader(memory);
    }
}
```

#### Phase 3A: Render Pipeline (Week 3-5)

**Goal**: Port RenderPipeline to use BGFX, establish all rendering passes

**Deliverables**:
- [ ] IGraphicsDevice fully functional
- [ ] RenderPipeline ported to BGFX
- [ ] Shadow mapping working
- [ ] Forward pass rendering
- [ ] Transparency handling
- [ ] Cross-platform validation

**Key Changes**:
```csharp
// RenderPipeline.cs modifications
public class RenderPipeline
{
    private readonly IGraphicsDevice _device;
    private readonly BgfxDevice _bgfxDevice;
    
    public void Execute(RenderScene renderScene)
    {
        _device.BeginFrame();
        
        // Shadow pass
        RenderShadowPass(renderScene);
        
        // Forward pass
        RenderForwardPass(renderScene);
        
        // Transparency
        RenderTransparentPass(renderScene);
        
        _device.EndFrame();
        _device.Present();
    }
    
    private void RenderShadowPass(RenderScene scene)
    {
        _device.SetView("shadow", LightViewMatrix, LightProjMatrix);
        _device.Clear(Color.White);
        
        foreach (var obj in scene.ShadowObjects)
        {
            _device.Submit(obj.RenderCommand);
        }
    }
}
```

#### Phase 3B: Advanced Features (Week 5-7)

**Goal**: Water, particles, ImGui, texture effects

**Deliverables**:
- [ ] Water rendering
- [ ] Particle systems
- [ ] ImGui integration
- [ ] Post-processing

#### Phase 4: Optimization & Polish (Week 7-10)

**Goal**: Performance tuning, multi-threading, final validation

**Deliverables**:
- [ ] Multi-threaded command encoding (encoders)
- [ ] Performance profiling
- [ ] Memory optimization
- [ ] Final integration testing
- [ ] Documentation

---

## 4. Parallel Development Strategy

### 4.1 Team Organization

**Team Structure**:
- **Developer 1 (Lead)**: BGFX bindings + core device integration
- **Developer 2**: Render pipeline porting + features
- **Developer 3 (Part-time)**: Testing + cross-platform validation

### 4.2 Concurrent Work Streams

**Stream A** (Developer 1): Bindings & Device
- Week 1: Complete P/Invoke bindings
- Week 2: BgfxDevice implementation
- Week 3: Debug helpers & profiling

**Stream B** (Developer 2): Pipeline & Features
- Week 1: Study current RenderPipeline design
- Week 2: Begin RenderPipeline porting
- Week 3: Shadow mapping implementation
- Week 4-5: Advanced features

**Stream C** (Developer 3): Testing & Validation
- Week 2: RenderDoc validation setup
- Week 3-4: Cross-platform testing
- Week 5+: Performance profiling

### 4.3 Integration Points

| Week | Milestone | Dependencies | Gate |
|------|-----------|--------------|------|
| 1 | Bindings draft | None | Design review |
| 2 | Hello-world | Bindings complete | Visual validation |
| 3 | Device abstraction | Bindings, design | Architecture review |
| 4 | Basic rendering | Device API | Feature rendering |
| 5 | Pipeline ported | Rendering works | Visual parity |
| 6 | All features | Pipeline done | Feature complete |
| 7 | Optimization | Feature complete | Performance gate |
| 8 | Validation | All done | Go/no-go decision |
| 9-10 | Polish & docs | All systems go | Final release |

---

## 5. Fallback Strategy

### 5.1 Keep Veldrid as Fallback

**Strategy**: Maintain Veldrid implementation during migration

**Benefit**: Can revert if BGFX issues arise

**Implementation**:
```csharp
// GraphicsDeviceFactory.cs
public static IGraphicsDevice CreateDevice(GraphicsBackend backend)
{
    return backend switch
    {
        GraphicsBackend.BGFX => new BgfxDevice(),
        GraphicsBackend.Veldrid => new VeldridDevice(),
        _ => throw new NotSupportedException()
    };
}

// Program.cs
var backendFlag = args.Contains("--bgfx") ? GraphicsBackend.BGFX : GraphicsBackend.Veldrid;
var device = GraphicsDeviceFactory.CreateDevice(backendFlag);
```

**Fallback Plan**:
1. Keep Veldrid code available on separate branch
2. If BGFX blockers emerge, can switch via flag
3. Gradual Veldrid removal after stabilization
4. Keep abstraction interface stable

### 5.2 Feature Gates

**Strategy**: Feature-gate BGFX features until ready

```csharp
// In RenderPipeline.cs
if (UseNewBgfxPipeline)
{
    // New BGFX path
    ExecuteBgfxRenderPipeline();
}
else
{
    // Fallback to Veldrid
    ExecuteVeldridRenderPipeline();
}
```

### 5.3 Rollback Procedure

**If critical issues arise**:
1. Switch feature gate to disable BGFX code
2. Fall back to Veldrid for release
3. Continue BGFX work on development branch
4. Re-evaluate architecture/approach
5. Merge after stabilization

---

## 6. Risk Mitigation Details

### 6.1 Platform-Specific Rendering Issues

**Risk**: BGFX Metal backend differs from Vulkan/D3D11  
**Mitigation**:
- Early Mac testing (week 3)
- RenderDoc frame captures for validation
- Known issues tracking spreadsheet
- Dedicated Mac testing device

**Contingency**: Implement platform-specific workarounds in abstraction layer

### 6.2 Performance Regression

**Risk**: Initial BGFX code slower than Veldrid  
**Mitigation**:
- Establish baseline metrics early (week 2)
- Profile at each phase
- Optimize identified bottlenecks
- Multi-threading phase addresses threading issues

**Contingency**: Known optimization techniques available

### 6.3 Binding Incompleteness

**Risk**: Missing BGFX API bindings  
**Mitigation**:
- Plan bindings comprehensively upfront
- Incremental API coverage
- Feature-driven development (add APIs as needed)
- Test each binding thoroughly

**Contingency**: Add missing bindings as discovered

### 6.4 Shader Compilation Issues

**Risk**: shaderc produces incompatible or incorrect output  
**Mitigation**:
- Early shader compilation testing (week 2)
- Validate output with RenderDoc
- Keep glslangValidator as validation tool
- Sample shader subset for testing

**Contingency**: Fallback to SPIR-V intermediate format if needed

---

## 7. Code Structure Examples

### 7.1 New File: BgfxGraphicsDevice.cs

```csharp
public class BgfxDevice : IGraphicsDevice
{
    private GraphicsAdapterInfo _adapter;
    private uint _width, _height;
    private uint _currentViewId = 0;
    private Dictionary<string, uint> _viewIdMap = new();
    
    public BgfxDevice(uint width, uint height, string title)
    {
        var init = new Bgfx.Init
        {
            type = Bgfx.RendererTypeEnum.Count,
            vendorId = 0,
            deviceId = 0,
            resolution = new Bgfx.ResolutionEnum { width = width, height = height },
            limits = new Bgfx.Limits(),
            callback = IntPtr.Zero,
            allocator = IntPtr.Zero,
        };
        
        Bgfx.init(ref init);
        _width = width;
        _height = height;
        
        // Allocate view IDs for different passes
        _viewIdMap["shadow"] = 0;
        _viewIdMap["opaque"] = 1;
        _viewIdMap["transparent"] = 2;
        _viewIdMap["water"] = 3;
        _viewIdMap["ui"] = 4;
    }
    
    public void BeginFrame() => Bgfx.frame();
    
    public void SetView(string viewName, Matrix4x4 view, Matrix4x4 proj)
    {
        var viewId = _viewIdMap[viewName];
        var viewArray = new[] { view.M11, view.M21, view.M31, view.M41, /* ... */ };
        Bgfx.setViewTransform(viewId, viewArray, projArray);
    }
    
    public void Clear(Color color, float depth = 1f, uint stencil = 0)
    {
        Bgfx.setViewClear(_currentViewId, 
            (ushort)(Bgfx.ClearFlags.Color | Bgfx.ClearFlags.Depth),
            ((uint)color.R << 24) | ((uint)color.G << 16) | ((uint)color.B << 8) | color.A,
            depth, stencil);
    }
    
    public void Submit(RenderCommand cmd)
    {
        Bgfx.setTransform(ref cmd.Transform);
        Bgfx.setVertexBuffer(0, cmd.VertexBuffer.Handle);
        Bgfx.setIndexBuffer(cmd.IndexBuffer.Handle);
        Bgfx.submit(cmd.ViewId, cmd.ShaderHandle);
    }
    
    public void Present()
    {
        // BGFX handles presentation automatically
    }
    
    public void Dispose()
    {
        Bgfx.shutdown();
    }
}
```

### 7.2 Integration: RenderPipeline Changes

```csharp
public class RenderPipeline
{
    private IGraphicsDevice _graphics;
    private RenderScene _renderScene;
    
    // Minimal changes needed:
    // 1. Inject IGraphicsDevice (replace direct Veldrid calls)
    // 2. Use abstraction methods instead of Veldrid methods
    // 3. Rest of code remains unchanged
    
    public RenderPipeline(IGraphicsDevice graphics, RenderScene scene)
    {
        _graphics = graphics;
        _renderScene = scene;
    }
    
    public void Execute()
    {
        _graphics.BeginFrame();
        
        // Shadow pass - works with both Veldrid and BGFX
        RenderShadowPass();
        
        // Forward pass
        RenderForwardPass();
        
        // Transparency
        RenderTransparencyPass();
        
        _graphics.EndFrame();
    }
    
    // All existing code logic unchanged
    private void RenderShadowPass() { /* ... */ }
    private void RenderForwardPass() { /* ... */ }
}
```

---

## 8. Build System Integration

### 8.1 Project File Changes

**File**: `src/OpenSage.Launcher/OpenSage.Launcher.csproj`

```xml
<ItemGroup>
    <!-- Keep Veldrid (fallback) -->
    <PackageReference Include="Veldrid" Version="4.9.0" />
    
    <!-- New BGFX support (internal project) -->
    <ProjectReference Include="../OpenSage.Graphics.BGFX/OpenSage.Graphics.BGFX.csproj" />
</ItemGroup>

<!-- Shader compilation target -->
<Target Name="CompileShaders" BeforeBuild="true">
    <Message Text="Compiling shaders with shaderc..." />
    <Exec Command="$(ShadercPath) -i $(MSBuildProjectDirectory)/Assets/Shaders/Source -o $(MSBuildProjectDirectory)/Assets/Shaders/Compiled -f bin" 
          ContinueOnError="false" />
</Target>

<!-- Copy BGFX native binaries -->
<Target Name="CopyBgfxNatives" AfterBuild="true">
    <Copy SourceFiles="@(BgfxNativeFiles)" DestinationFolder="$(OutputPath)" />
</Target>

<ItemGroup>
    <BgfxNativeFiles Include="runtimes/win-x64/native/*" Condition="'$(RuntimeIdentifier)' == 'win-x64'" />
    <BgfxNativeFiles Include="runtimes/osx-x64/native/*" Condition="'$(RuntimeIdentifier)' == 'osx-x64'" />
    <BgfxNativeFiles Include="runtimes/linux-x64/native/*" Condition="'$(RuntimeIdentifier)' == 'linux-x64'" />
</ItemGroup>
```

### 8.2 shaderc Integration

**Build Script**: `scripts/compile-shaders.sh`

```bash
#!/bin/bash

SHADER_SOURCE="Assets/Shaders/Source"
SHADER_OUTPUT="Assets/Shaders/Compiled"
PLATFORMS=("d3d11" "metal" "glsl" "spirv")

mkdir -p "$SHADER_OUTPUT"

# Find all GLSL files
for shader in $(find "$SHADER_SOURCE" -name "*.glsl"); do
    base=$(basename "$shader" .glsl)
    echo "Compiling $base..."
    
    # Compile for each platform
    for platform in "${PLATFORMS[@]}"; do
        shaderc -f "$shader" -o "$SHADER_OUTPUT/${base}_${platform}.bin" --type fragment --profile latest
    done
done

echo "Shader compilation complete!"
```

---

## 9. Success Criteria & Go/No-Go Gates

### 9.1 Phase 2 Gate Criteria

**Must Have**:
- [ ] Hello-world triangle renders on Windows & macOS
- [ ] Frame rate stable at 60 FPS
- [ ] Shader compilation pipeline working
- [ ] No crashes or memory leaks
- [ ] RenderDoc capture valid

**Nice to Have**:
- [ ] Basic scene rendering (terrain + objects)
- [ ] Performance baseline established
- [ ] Linux validation

**Gate Decision**: Proceed to Phase 3 if Must-Have criteria met

### 9.2 Phase 3 Gate Criteria

**Must Have**:
- [ ] Full render pipeline ported
- [ ] Shadow mapping functional
- [ ] Visual output matches Veldrid
- [ ] All platforms rendering correctly
- [ ] No critical performance regression

**Nice to Have**:
- [ ] 20-30% CPU improvement measured
- [ ] Water rendering working
- [ ] Particles functional

**Gate Decision**: Proceed to Phase 4 if Must-Have criteria met

### 9.3 Final Release Gate Criteria

**Must Have**:
- [ ] 100% feature parity with Veldrid
- [ ] Cross-platform validation passed
- [ ] Performance targets met
- [ ] Zero known critical issues
- [ ] Documentation complete

**Quality Bar**: Production-ready, feature-complete

---

## 10. Conclusion

This Migration Strategy provides a comprehensive, low-risk approach to integrating BGFX into OpenSAGE. Key strengths:

✅ **Incremental Phases**: Allows validation at each step  
✅ **Abstraction Layer**: Isolates changes from game code  
✅ **Fallback Strategy**: Can revert to Veldrid if needed  
✅ **Parallel Development**: Multiple work streams possible  
✅ **Clear Gates**: Go/no-go decisions well-defined  
✅ **Timeline Confidence**: 8-10 weeks realistic estimate  

The project is ready to advance to **Phase 2: Proof-of-Concept Implementation**.

---

**Document Status**: COMPLETE  
**Next Action**: Begin Phase 2 - BGFX Bindings & Hello-World  
**Owner**: Graphics Engineering Lead  
**Version**: 1.0  
**Last Updated**: December 12, 2025  
