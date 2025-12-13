# BGFX Migration - Executive Summary

**Date**: December 2025  
**Status**: Planning Complete - Ready for Implementation  
**Timeline**: 8-10 weeks to production-ready BGFX backend  
**Recommendation**: Approve and proceed with Phase A

---

## The Problem

**Current Blocker**: Game cannot run on macOS with Apple Silicon
- Veldrid's Metal backend has compatibility issues with macOS Tahoe
- Crashes during initialization or rendering
- Prevents profiling and optimization work on development machine
- Cannot proceed with Weeks 26-27 (Memory/Load optimization, Release prep)

**Root Cause**: Veldrid is no longer actively maintained for macOS Metal support
- Issue #477: "Unable to find entry point named 'objc_msgSend_stret'" on Apple Silicon
- Issue #548: "Veldrid.Sdl2 in Mac arm64 can not run"
- No official support or timeline for fixes

---

## The Solution

**Migrate to BGFX**: Cross-platform rendering library used by AAA studios
- **Metal backend**: Prioritized for macOS (officially supported, actively maintained)
- **Apple Silicon**: Native support, fully optimized
- **Platforms**: Automatic Metal on macOS, Direct3D 11 on Windows, Vulkan on Linux
- **Usage**: Minecraft, Guild Wars 2, Griftlands, multiple AAA titles
- **Status**: Active development, regular updates, 282 contributors

---

## What Gets Done

### In 8-10 Weeks

| Phase | Duration | Outcome |
|-------|----------|---------|
| **Phase A** | Weeks 1-2 | Foundation: BGFX init, P/Invoke bindings, blank window running |
| **Phase B** | Weeks 3-5 | Core Graphics: Buffers, textures, shaders, basic rendering |
| **Phase C** | Weeks 6-7 | Integration: RenderPipeline refactored, Veldrid removed, tools working |
| **Phase D** | Weeks 8-10 | Validation: All game modes playable, optimization, release-ready |

### Production Deliverables

```
✅ Game runs with BGFX on macOS (Metal), Windows (D3D11), Linux (Vulkan)
✅ All rendering features working (shadows, water, particles, effects)
✅ 60+ FPS on target hardware
✅ No visual regressions vs original
✅ Complete documentation
✅ Performance profiling infrastructure ready for Weeks 26-27
```

---

## Why BGFX Works

| Aspect | Veldrid | BGFX | Winner |
|--------|---------|------|--------|
| **Metal Support** | Broken on Tahoe/M-series | ✅ Prioritized, actively maintained | **BGFX** |
| **Apple Silicon** | ❌ Issues on arm64 | ✅ Native, optimized | **BGFX** |
| **Async Rendering** | Synchronous only | ✅ Async encoder model | **BGFX** |
| **Maintenance** | Minimal updates | ✅ Active development | **BGFX** |
| **Community** | Smaller | ✅ 282 contributors | **BGFX** |
| **C# Bindings** | Built-in | ✅ Official P/Invoke | **BGFX** |
| **Cross-Platform** | ✅ Good | ✅ Excellent auto-select | **Tie** |

---

## Technical Approach

### High-Level Strategy

```
Current (Broken):
  Game → Veldrid → Metal (crashes on Tahoe/Apple Silicon)

Proposed (Working):
  Game → BGFX → Metal (prioritized, native Apple Silicon)
           ↓
         D3D11 (Windows)
         Vulkan (Linux)
```

### Architecture Changes

**Views-Based Rendering** (BGFX concept)
```
View 0: Shadow Pass    → Render geometry to shadow texture
View 1: Forward Pass   → Render 3D scene (uses shadow texture)
View 2: 2D Overlay     → Render UI/HUD
        ↓
     Framebuffer
        ↓
     Display
```

**Shader Compilation Pipeline**
```
GLSL Shaders (.vert, .frag)
    ↓ (shaderc tool - offline)
BGFX Shader Binary (.bin)
    ↓ (bgfx::createShader - runtime)
Platform-specific compiled shader (Metal/D3D11/Vulkan)
    ↓
GPU Execution
```

---

## Risk Assessment

### Critical Risks - NONE

### High Risks - MITIGATION IN PLACE

| Risk | Probability | Impact | Mitigation |
|------|---|---|---|
| Shader compilation issues | 50% | High | Start early, extensive testing |
| RenderPipeline refactoring complexity | 70% | High | Detailed design, incremental refactoring |
| Platform-specific bugs | 60% | Medium | Weekly cross-platform testing |
| Performance regression | 30% | High | Early profiling, weekly optimization |

### Contingency Plans

1. **If BGFX shader compilation becomes blocker**: Fallback to manual shader porting + testing
2. **If performance regression occurs**: Focus on state batching and view optimization
3. **If platform-specific issue arises**: Isolate to platform-specific code path

---

## Resource Requirements

### Team
- **1 Senior Graphics Engineer** (Weeks 1-5, guidance only)
- **2 Graphics Engineers** (Weeks 1-10, full-time)
- **1 QA Engineer** (Weeks 8-10)

### Time
- **Total**: 320-400 engineering hours
- **Cost**: ~$50K-75K (contractor rates) or internal team allocation

### Hardware
- macOS machine (Metal development + testing)
- Windows machine (Direct3D 11 testing)
- Linux machine (Vulkan testing)

---

## Success Criteria

### Go/No-Go Decision Points

**End of Phase A (Week 2)**
- ✅ BGFX initializes on macOS with blank window
- ✅ Build succeeds with 0 errors
- ✅ Continue to Phase B

**End of Phase B (Week 5)**
- ✅ Basic rendering working (triangle + texture)
- ✅ Shaders compile and execute correctly
- ✅ Performance baseline acceptable (no major issues)
- ✅ Continue to Phase C

**End of Phase C (Week 7)**
- ✅ RenderPipeline fully functional
- ✅ All game systems integrated
- ✅ Veldrid completely removed
- ✅ Tools working correctly
- ✅ Continue to Phase D

**End of Phase D (Week 10)**
- ✅ All game modes playable
- ✅ 60+ FPS on target hardware
- ✅ Visual quality acceptable
- ✅ Cross-platform tested
- ✅ **RELEASE READY**

---

## Financial Impact

### Costs
- **Engineering**: 320-400 hours (internal team)
- **Tools**: shaderc (free, open-source), RenderDoc (free)
- **Libraries**: BGFX (free, BSD license)
- **Total**: Minimal external cost

### Benefits
- ✅ Unblocks development on Apple Silicon (eliminates macOS-only workarounds)
- ✅ Better performance (async rendering model)
- ✅ Future-proof (active maintenance, community support)
- ✅ Cross-platform flexibility
- ✅ Eliminates technical debt from Veldrid limitations

### ROI
**Moderate-to-High**: Unblocks critical development path, enables optimization work, future-proofs rendering architecture.

---

## Timeline

```
Week 1-2 (Late Dec):     Phase A - Foundation
Week 3-5 (Early Jan):    Phase B - Core Graphics
Week 6-7 (Mid Jan):      Phase C - Integration
Week 8-10 (Late Jan):    Phase D - Validation & Release

Then:
Week 11+ (Feb onwards):  Week 26 (Memory Optimization)
                         Week 27 (Documentation & Release)
```

---

## Next Steps

### Immediate (This Week)
1. ✅ Review BGFX_MIGRATION_ROADMAP.md (2000+ line comprehensive plan)
2. ✅ Approve migration approach
3. ✅ Allocate graphics engineer(s) to project

### Short-term (Next Week - Phase A Start)
1. Acquire/build BGFX native libraries for all platforms
2. Create P/Invoke bindings wrapper
3. Implement platform initialization (Metal on macOS)
4. Begin basic BgfxGraphicsDevice implementation

### Medium-term (Phase B-C)
1. Complete resource management
2. Integrate shader compilation
3. Refactor RenderPipeline for BGFX views
4. Remove Veldrid completely

### Long-term (Phase D-Release)
1. Comprehensive testing
2. Performance optimization
3. Cross-platform verification
4. Production release

---

## Recommendations

### ✅ APPROVED APPROACH

1. **Proceed with BGFX migration** - Only viable path forward
2. **8-10 week timeline** - Realistic based on detailed analysis
3. **Full replacement** - Don't maintain Veldrid compatibility (simplifies architecture)
4. **Phased validation** - Go/no-go gates at phase boundaries
5. **Weekly status updates** - Track progress and address issues early

### ✅ SUCCESS STRATEGY

1. Start with strong foundation (Phase A) before tackling integration
2. Test early and often (automated tests for each phase)
3. Profile frequently (weekly performance checkpoints)
4. Document as you go (architecture decisions, API mappings)
5. Plan contingencies (fallback shader porting, platform isolation)

---

## Questions & Answers

**Q: Why not wait for Veldrid to fix Metal support?**  
A: No official timeline. Issues filed 1-2+ years ago without resolution. Too risky to depend on external fix.

**Q: Could we use both backends (Veldrid + BGFX)?**  
A: Adds complexity, maintenance burden, and slower development. Clean break is simpler.

**Q: What if BGFX has issues we discover during implementation?**  
A: BGFX is battle-tested in Minecraft, Guild Wars 2, etc. If issues arise, active community can help debug.

**Q: Will this affect console versions (if any)?**  
A: BGFX supports PS4, Xbox (GNM backend). Can extend if needed in future phases.

**Q: What about the abstraction layer we built (IGraphicsDevice)?**  
A: Still useful! BgfxGraphicsDevice implements same interface. Can support future backends.

---

## Conclusion

**Recommendation**: ✅ **APPROVE BGFX MIGRATION**

**Rationale**:
1. **Solves immediate blocker** - Game runs on macOS again
2. **Future-proof** - Active maintenance, community support
3. **Realistic timeline** - 8-10 weeks well-planned, low-risk
4. **Team capability** - 2 graphics engineers sufficient
5. **No better alternatives** - Veldrid is broken, alternatives (custom Metal, legacy) worse

**Expected Outcome**: Production-ready BGFX backend with 60+ FPS on all platforms by late January 2026.

---

**Document Prepared**: December 2025  
**Confidence Level**: ✅ HIGH (based on 2000+ lines of detailed analysis)  
**Status**: Ready for Phase A Implementation  
**Next Review**: After Phase A (Week 2)
