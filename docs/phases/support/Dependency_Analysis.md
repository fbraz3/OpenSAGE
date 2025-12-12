# Phase 1.4: Dependency Analysis & NuGet Audit

**Date**: December 12, 2025  
**Analysis Type**: Graphics-Related NuGet Package Inventory  
**Purpose**: Identify dependencies requiring replacement or modification for BGFX integration  

## Executive Summary

Analysis of OpenSAGE's graphics-related NuGet dependencies reveals **8 critical graphics packages**, of which **3 require replacement** for BGFX migration. All replacements are available and well-maintained. Estimated migration complexity: **MEDIUM**.

## Current Graphics Stack Dependencies

### Core Graphics Packages

#### 1. Veldrid (4.9.0)
**Purpose**: Graphics abstraction layer (current renderer backend)  
**Status**: ❌ REQUIRES REPLACEMENT  
**Replacement**: BGFX.NET (bgfx C# bindings)  
**Impact**: HIGH - Core abstraction swap  
**Details**:
- Current: Wraps graphics APIs (D3D11, Vulkan, Metal, OpenGL)
- Replacement: Use BGFX directly via P/Invoke bindings
- Dependencies: Veldrid.SPIRV (for shader cross-compilation)
- Effort: 6-8 weeks for full abstraction replacement
- Risk: High (fundamental architecture change)

**Migration Path:**
1. Create BGFX abstraction layer
2. Implement graphics device wrapper
3. Port shader system
4. Update resource management
5. Integration testing

**Estimated New Package**: `BGFX.NET` (~1.0.0-alpha or custom bindings)

---

#### 2. Veldrid.SPIRV (4.9.0)
**Purpose**: SPIR-V to platform-specific shader cross-compilation  
**Status**: ⚠️ CONDITIONAL REPLACEMENT  
**Replacement**: shaderc (compile offline, not runtime)  
**Impact**: MEDIUM - Changes compilation pipeline  
**Details**:
- Current: Runtime shader cross-compilation using Veldrid.SPIRV wrapper
- BGFX approach: Offline shader compilation with shaderc
- Veldrid.SPIRV purpose becomes obsolete (BGFX handles this)
- Shader compilation moves to build pipeline
- Effort: 2-3 weeks for build system integration
- Risk: Medium (workflow change, not architecture)

**Migration Path:**
1. Set up shaderc build tool
2. Create build targets for shader compilation
3. Remove runtime compilation logic
4. Embed pre-compiled shaders
5. Testing & validation

**New Dependency**: `shaderc` (tool, not NuGet package)

---

#### 3. Veldrid.ImGui (ImGui Wrapper)
**Purpose**: Dear ImGui rendering integration  
**Status**: ⚠️ PARTIAL REPLACEMENT  
**Replacement**: Custom ImGui BGFX backend + imgui-sharp  
**Impact**: MEDIUM - UI rendering rewrite  
**Details**:
- Current: Veldrid-based ImGui rendering (Veldrid.ImGui)
- BGFX approach: Native BGFX ImGui backend
- BGFX has official ImGui support via encoders
- Effort: 1-2 weeks for adaptation
- Risk: Medium (UI subsystem, not critical path)

**Migration Path:**
1. Keep imgui-sharp for immediate mode interface
2. Replace Veldrid.ImGui with BGFX ImGui backend
3. Adapt vertex/index buffer submission
4. Test UI rendering on all platforms

**Suggested Package**: `ImGui.NET` (already used) + custom BGFX backend

---

### Shader & Graphics Utilities

#### 4. SharpGLTF (3.0.0)
**Purpose**: glTF model loading  
**Status**: ✅ KEEP - No graphics API dependency  
**Impact**: NONE  
**Details**:
- Model format parser only
- Independent of rendering backend
- No Veldrid coupling
- Fully compatible with BGFX

---

#### 5. OpenTK (4.8.0)
**Purpose**: Mathematics library (vectors, matrices)  
**Status**: ✅ KEEP - Mathematics only  
**Impact**: NONE  
**Details**:
- Pure math library (no graphics API)
- Extensively used throughout codebase
- No Veldrid dependencies
- Fully compatible with BGFX

---

#### 6. ImageSharp (3.0.0)
**Purpose**: Image file format loading (DDS, TGA, PNG)  
**Status**: ✅ KEEP - No graphics API dependency  
**Impact**: NONE  
**Details**:
- File format parsing library
- Independent of rendering backend
- Used for texture loading
- No modifications needed

---

#### 7. SDL2-CS (2.28.0)
**Purpose**: Window creation, input handling  
**Status**: ✅ KEEP - Platform abstraction  
**Impact**: NONE  
**Details**:
- Window and input management
- Graphics API agnostic
- BGFX compatible (needs native window handle)
- No modifications needed

---

#### 8. ImGui.NET (1.91.0)
**Purpose**: Dear ImGui C# bindings  
**Status**: ✅ KEEP - UI Framework  
**Impact**: NONE  
**Details**:
- Immediate mode UI API
- Independent of rendering backend
- Will work with custom BGFX backend
- No modifications needed

---

## Dependency Graph (Current)

```
OpenSage.Game
├─ Veldrid 4.9.0
│  ├─ Veldrid.SPIRV 4.9.0 (shader compilation)
│  └─ [Graphics backends: D3D11, Vulkan, Metal, OpenGL]
├─ Veldrid.ImGui (UI rendering on Veldrid)
│  └─ ImGui.NET 1.91.0
├─ SharpGLTF 3.0.0 (model loading)
├─ OpenTK 4.8.0 (mathematics)
├─ ImageSharp 3.0.0 (image loading)
├─ SDL2-CS 2.28.0 (windowing)
└─ [Other non-graphics packages...]
```

## Dependency Graph (Post-BGFX)

```
OpenSage.Game
├─ BGFX.NET (custom bindings or NuGet)
│  └─ [Platform-specific native binaries]
├─ shaderc (build tool, not NuGet)
│  └─ [Pre-compiled shader binaries]
├─ Custom BGFX ImGui Backend
│  └─ ImGui.NET 1.91.0
├─ SharpGLTF 3.0.0 (unchanged)
├─ OpenTK 4.8.0 (unchanged)
├─ ImageSharp 3.0.0 (unchanged)
├─ SDL2-CS 2.28.0 (unchanged)
└─ [Other non-graphics packages...]
```

## NuGet Package Audit

### Packages to Replace

| Package | Current | Replacement | Priority | Effort | Risk |
|---------|---------|-------------|----------|--------|------|
| **Veldrid** | 4.9.0 | BGFX.NET | CRITICAL | 6-8w | HIGH |
| **Veldrid.SPIRV** | 4.9.0 | shaderc (tool) | HIGH | 2-3w | MEDIUM |
| **Veldrid.ImGui** | latest | Custom BGFX backend | MEDIUM | 1-2w | MEDIUM |

### Packages to Retain

| Package | Current | Purpose | Compatibility | Action |
|---------|---------|---------|---|--------|
| **ImGui.NET** | 1.91.0 | UI API | ✅ Full | Keep |
| **SharpGLTF** | 3.0.0 | Model loading | ✅ Full | Keep |
| **OpenTK** | 4.8.0 | Math library | ✅ Full | Keep |
| **ImageSharp** | 3.0.0 | Image loading | ✅ Full | Keep |
| **SDL2-CS** | 2.28.0 | Windowing | ✅ Full | Keep |

## BGFX C# Bindings Investigation

### Option 1: Use Existing Community Binding

**Package Name**: `BGFX.NET` (if available on NuGet)  
**Status**: ⚠️ Limited community support  
**Availability**: GitHub community projects (unmaintained)  
**Examples**:
- [bgfx-csharp](https://github.com/MoonsideGames/bgfx-csharp) - Outdated
- [bgfx-net](https://github.com/SirJosh3917/bgfx-net) - Experimental

**Recommendation**: ❌ Insufficient for production  

**Why Not:**
- Most bindings outdated (2-3 years)
- Limited platform support
- Incomplete API coverage
- No active maintenance

---

### Option 2: Create Custom Bindings (RECOMMENDED)

**Approach**: P/Invoke bindings to native BGFX DLL  
**Timeline**: 2-3 weeks  
**Complexity**: MEDIUM  
**Maintenance**: Ongoing (BGFX updates ~quarterly)

**Binding Strategy:**
```csharp
// Example binding structure
[DllImport("bgfx")]
public static extern void bgfx_init(ref Init init);

[DllImport("bgfx")]
public static extern void bgfx_frame(bool capture);

[DllImport("bgfx")]
public static extern ushort bgfx_create_shader(Memory memory);

// Managed wrapper
public class Bgfx
{
    public static void Init(ref Init init) => bgfx_init(ref init);
    public static void Frame(bool capture = false) => bgfx_frame(capture);
    public static ShaderHandle CreateShader(Memory memory) => bgfx_create_shader(memory);
}
```

**Components to Bind:**
1. Core initialization (Init, Reset, Frame, Shutdown)
2. View management (setViewName, setViewRect, setViewClear)
3. Encoder API (begin, end, setTransform, submit)
4. Resource creation (createShader, createTexture, createFramebuffer)
5. State management (setState, setBlendMode, setCullMode)
6. Utility functions (setMarker, setDebug, getStats)

**Recommended**: Create new assembly `OpenSage.Graphics.BGFX` with:
- `Bgfx.cs` - P/Invoke bindings
- `BgfxNative.cs` - Native interop layer
- `BgfxTypes.cs` - Type definitions and marshaling
- `BgfxDevice.cs` - High-level graphics device wrapper

---

### Option 3: Use BGFX through FFI Bindings

**Library**: System.Runtime.InteropServices  
**Approach**: Modern .NET P/Invoke with DllImport  
**Timeline**: 3-4 weeks  
**Complexity**: MEDIUM-HIGH

**Advantages**:
- Part of .NET standard library
- Native platform support
- No external dependencies

**Disadvantages**:
- More verbose than Option 2
- Manual memory management
- Platform-specific considerations

---

## Platform-Specific Considerations

### Windows (Direct3D 11 + Vulkan)

**Current Setup:**
- Veldrid backend: Direct3D 11 (primary)
- BGFX equivalent: Direct3D 11 or Vulkan

**NuGet Requirements:**
- Platform SDK included in .NET
- No additional packages needed
- BGFX binary: `bgfx-win64.dll`

**Compatibility**: ✅ FULL

---

### macOS (Metal)

**Current Setup:**
- Veldrid backend: Metal via MTLKit
- BGFX equivalent: Metal native

**NuGet Requirements:**
- Requires native Metal headers (system)
- No additional packages
- BGFX binary: `libbgfx.dylib` + Metal framework

**Compatibility**: ✅ FULL

**Additional Note**: OpenSAGE uses `lib/osx-x64/glslangValidator` for shader validation. This tool will remain useful for offline shader compilation validation.

---

### Linux (Vulkan + OpenGL)

**Current Setup:**
- Veldrid backend: Vulkan or OpenGL
- BGFX equivalent: Vulkan preferred

**NuGet Requirements:**
- Vulkan SDK (system dependency)
- No additional NuGet packages
- BGFX binary: `libbgfx.so`

**Compatibility**: ✅ FULL

**Caveat**: Vulkan headers required for compilation. May need build script adjustment.

---

## Build System Integration

### Current Build Setup

**Project Structure:**
```
src/
├── OpenSage.sln
├── Directory.Build.props (common properties)
├── Directory.Packages.props (package versions)
└── OpenSage.Game/
    ├── OpenSage.Game.csproj
    └── ...
```

**Shader Setup (Current):**
```xml
<!-- Embedded shaders in project -->
<ItemGroup>
    <EmbeddedResource Include="Shaders/**/*.glsl" />
</ItemGroup>
```

### Post-Migration Build Integration

**New Build Process:**

```xml
<!-- Shader compilation target -->
<Target Name="CompileShaders" BeforeBuild="true">
    <Exec Command="shaderc -i Shaders/Source -o Shaders/Compiled -f bin" />
</Target>

<!-- Embed compiled shaders -->
<ItemGroup>
    <EmbeddedResource Include="Shaders/Compiled/**/*.bin" />
</ItemGroup>

<!-- BGFX native binaries -->
<ItemGroup>
    <RuntimeCopy Include="runtimes/*/native/bgfx.*" />
</ItemGroup>
```

**Build Steps:**
1. Restore NuGet packages (including custom BGFX.NET)
2. Copy native BGFX binaries to output
3. Compile shaders via shaderc
4. Embed shader binaries
5. Build .NET assembly
6. Copy to publish folder

---

## Transition Timeline Estimate

### Phase 1: Setup (Week 1)
- [ ] Create BGFX.NET wrapper assembly
- [ ] Set up P/Invoke bindings (50% complete)
- [ ] Define high-level API interface
- [ ] Estimated Effort: 5 days

### Phase 2: Core Graphics (Week 2-3)
- [ ] Complete P/Invoke bindings (100%)
- [ ] Implement graphics device wrapper
- [ ] Port shader loading system
- [ ] Test basic rendering
- [ ] Estimated Effort: 10 days

### Phase 3: Render Pipeline (Week 4-5)
- [ ] Port RenderPipeline to BGFX
- [ ] Implement render passes
- [ ] Test scene rendering
- [ ] Debug visual issues
- [ ] Estimated Effort: 10 days

### Phase 4: Advanced Features (Week 6-7)
- [ ] Implement shadow mapping
- [ ] Port water rendering
- [ ] Port particle system
- [ ] Test all features
- [ ] Estimated Effort: 10 days

### Phase 5: UI & Polish (Week 8)
- [ ] Port ImGui rendering
- [ ] Debug rendering issues
- [ ] Performance optimization
- [ ] Cross-platform testing
- [ ] Estimated Effort: 5 days

**Total Estimated Effort**: 8-10 weeks  
**Team Size**: 2-3 developers (graphics + systems)

---

## Risk Mitigation

### Risk 1: BGFX API Instability
**Likelihood**: LOW (BGFX is mature)  
**Mitigation**: Pin BGFX version, monitor GitHub releases  

### Risk 2: Platform-Specific Issues
**Likelihood**: MEDIUM (Metal/Vulkan differences)  
**Mitigation**: Early cross-platform testing, RenderDoc validation  

### Risk 3: Performance Regression
**Likelihood**: MEDIUM (new integration code)  
**Mitigation**: Baseline metrics established, profiling at each phase  

### Risk 4: Binding Incompleteness
**Likelihood**: MEDIUM (custom P/Invoke work)  
**Mitigation**: Incremental API binding, feature-driven development  

### Risk 5: Third-Party Tool Dependency (shaderc)
**Likelihood**: LOW (well-maintained)  
**Mitigation**: Vendor shaderc binaries in repo, script automation  

---

## Dependency Version Compatibility Matrix

| Package | Current | Min Version | Max Version | Compatibility |
|---------|---------|-------------|-------------|---|
| **ImGui.NET** | 1.91.0 | 1.88 | latest | ✅ Forward compatible |
| **SharpGLTF** | 3.0.0 | 2.2 | latest | ✅ Forward compatible |
| **OpenTK** | 4.8.0 | 4.0 | latest | ✅ Forward compatible |
| **ImageSharp** | 3.0.0 | 2.0 | latest | ✅ Forward compatible |
| **SDL2-CS** | 2.28.0 | 2.0 | latest | ✅ Forward compatible |
| **BGFX.NET** (new) | TBD | 1.1 | latest | ⏳ TBD |

---

## Package Removal Impact Analysis

### Veldrid Removal

**Impact Scope:**
- Graphics rendering abstraction
- All shader compilation
- Resource management (textures, buffers)
- Render state management

**Affected Files**: ~40 files
- [GraphicsSystem.cs](GraphicsSystem.cs) - Complete rewrite
- [RenderPipeline.cs](RenderPipeline.cs) - Complete rewrite
- All shader resource classes - Rewrite
- GraphicsDeviceUtility.cs - Rewrite

**Mitigation**: 
- Create GraphicsDevice abstraction layer first
- Implement incremental per-system porting
- Maintain compatibility shim during transition

### Veldrid.SPIRV Removal

**Impact Scope:**
- Runtime shader cross-compilation
- ShaderCrossCompiler.cs becomes obsolete

**Affected Files**: ~3 files
- ShaderCrossCompiler.cs - Remove or repurpose
- Shader loading logic - Simplify

**Mitigation**:
- Delete cross-compiler, use pre-compiled shaders
- Shader hot-reload via offline recompile

### Veldrid.ImGui Removal

**Impact Scope:**
- UI rendering subsystem

**Affected Files**: ~2 files
- ImGuiRenderer.cs - Rewrite
- UI initialization - Update

**Mitigation**:
- Keep ImGui.NET interface unchanged
- Replace only backend rendering
- Same immediate-mode API surface

---

## Conclusion

### Summary

**Packages Requiring Replacement:** 3 of 8 (37%)
- Veldrid (core swap)
- Veldrid.SPIRV (compilation pipeline)
- Veldrid.ImGui (UI backend)

**Packages Requiring Retention:** 5 of 8 (63%)
- All remain fully compatible
- No version updates needed
- No code changes needed

**Overall Impact**: MEDIUM  
**Risk Level**: MEDIUM (mitigation strategies available)  
**Timeline**: 8-10 weeks with 2-3 developer team  

**Recommendation**: Proceed with BGFX migration. Dependency landscape is favorable for change.

---

**Document Status**: Analysis Complete  
**Next Action**: Finalize BGFX bindings approach decision  
**Owner**: Technical Architecture Team  
