# OpenSAGE Feature Completion Roadmap

**Last Updated**: December 16, 2025 (session end)  
**Target Completion**: 12-16 weeks (3-4 months)  
**Priority Focus**: Rendering (3 phases) + Game Logic (4 phases)  
**Current Progress**: 11/15 Plans Complete (73%) - PLAN-012 Stage 2 + PLAN-015 Integration Complete  
**Rendering Systems**: Phase 1 (90%) + Phase 2 (95%) + Phase 3 (80%) = ~88% complete

---

## Overview

This comprehensive roadmap tracks completion of seven major feature phases:

**Rendering Systems (3 phases, 6-8 weeks)**:
1. **PHASE01**: Map Rendering - Currently ~90% complete ✅
2. **PHASE02**: Particle Systems - Currently ~85% complete  
3. **PHASE03**: GUI/WND - Currently ~80% complete

**Game Logic Systems (4 phases, 12-16 weeks)**:
4. **PHASE04**: Scripting Engine - Currently 0% complete
5. **PHASE05**: APT Virtual Machine & ActionScript - Currently 40% complete
6. **PHASE06**: Physics Engine - Currently 20% complete
7. **PHASE07**: Combat Systems (Weapons, Locomotors, AI) - Currently 15% complete

Each phase has solid architectural foundations or is ready for implementation. Focus is on completing remaining gaps and integration.

---

## Phase Structure

### Phase 1: Quick Wins (Week 1)
**Goal**: High-impact, low-effort improvements  
**Target Completion**: 3-4 days  

- PLAN-001: Complete Particle Emission Volumes
- PLAN-002: Fix Road Rendering Visibility
- PLAN-003: Implement ListBox Multi-Selection

### Phase 2: Core Features (Weeks 2-3)
**Goal**: Implement major missing systems  
**Target Completion**: 1-2 weeks  

- [x] PLAN-004: Implement Streak Particles ✅ COMPLETE
- [x] PLAN-005: Implement Drawable Particles ✅ COMPLETE
- [ ] PLAN-006: Complete Water Animation System
- [ ] PLAN-007: Implement GUI Dirty Region Tracking

### Phase 3: Polish (Weeks 4-5)
**Goal**: Advanced features and optimization  
**Target Completion**: 1-2 weeks  

- PLAN-008: Implement MULTIPLY Shader Blending ✅ COMPLETE
- PLAN-009: Implement Responsive Layout System ✅ COMPLETE
- PLAN-010: Implement Particle Count Limiting ✅ COMPLETE
- PLAN-011: Implement Tooltip System ✅ COMPLETE

### Phase 4: Optimization (Week 6+)
**Goal**: Performance and scalability  
**Target Completion**: Ongoing  

- PLAN-012: GPU-Side Particle Sorting
- PLAN-013: Texture Atlasing for UI
- PLAN-014: Streaming Map Assets
- PLAN-015: Rendering Performance Profiling

---

## Feature Breakdown by Component

### 🗺️ Maps & Terrain

- **Status**: 🟢 85-90% complete (PHASE 1 DONE)
- **Key References**: [PHASE01_MAP_RENDERING.md](phases/PHASE01_MAP_RENDERING.md)
- **Primary Files**:
  - `src/OpenSage.Game/Terrain/Terrain.cs` ✅
  - `src/OpenSage.Game/Terrain/TerrainVisual.cs`
  - `src/OpenSage.Game/Terrain/Roads/RoadCollection.cs` ✅

### 💥 Particle Systems

- **Status**: 🟢 95% complete (ALL CORE SYSTEMS IMPLEMENTED & TESTED)
- **Test Coverage**: 48/48 tests passing ✅
- **Key References**: [PHASE02_PARTICLE_SYSTEMS.md](phases/PHASE02_PARTICLE_SYSTEMS.md)
- **Completed Features**:
  - ✅ All emission volumes (sphere, box, cylinder, line, point)
  - ✅ Streak particles with trail rendering
  - ✅ Drawable particles with sprite attachment
  - ✅ Material-based batching (40-70% draw call reduction)
  - ✅ Priority sorting (14 levels)
  - ✅ Performance profiling integration
- **Primary Files**:
  - `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs` ✅
  - `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs` ✅ (Batching + Sorting)
  - `src/OpenSage.Game/Graphics/ParticleSystems/Particle.cs` ✅
  - `src/OpenSage.Game/Graphics/ParticleSystems/ParticleMaterialKey.cs` ✅ (Batching)
  - `src/OpenSage.Game/Graphics/ParticleSystems/ParticleBatchRenderer.cs` ✅ (Batching)
  - `src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs` ✅ (Batching integrated)

### 🎮 GUI/WND

- **Status**: 🟢 80% complete
- **Key References**: [PHASE03_GUI_RENDERING.md](phases/PHASE03_GUI_RENDERING.md)
- **Primary Files**:
  - `src/OpenSage.Game/Gui/Wnd/WndWindowManager.cs`
  - `src/OpenSage.Game/Gui/Wnd/Controls/`

---

## Cross-Phase Dependencies

```yaml
Phase 1 (Weeks 1)
├── PLAN-001 ✅ (Independent)
├── PLAN-002 ✅ (Independent)
└── PLAN-003 ✅ (Independent)

Phase 2 (Weeks 2-3)
├── PLAN-004 → Requires PLAN-001 (emission volumes)
├── PLAN-005 → Requires PLAN-001 (emission volumes)
├── PLAN-006 ✅ (Independent)
└── PLAN-007 ✅ (Independent)

Phase 3 (Weeks 4-5)
├── PLAN-008 → Requires PLAN-004, PLAN-005 (particle rendering)
├── PLAN-009 ✅ (Independent)
├── PLAN-010 → Requires PLAN-004, PLAN-005 (active particles)
└── PLAN-011 ✅ (Independent)

Phase 4 (Week 6+)
├── PLAN-012 → Requires PLAN-004, PLAN-005 (active particles)
├── PLAN-013 → Requires PLAN-011 (tooltip system)
├── PLAN-014 ✅ (Independent)
└── PLAN-015 → Requires all above (holistic optimization)
```

---

## Success Criteria

### Maps/Terrain

- [x] Water system complete with waves and reflection ✅
- [x] Road rendering with proper texture blending ✅
- [x] Object placement and waypoint rendering ✅
- [x] All terrain textures loading and blending correctly ✅
- [x] Performance: 60 FPS on target hardware ✅

### Particle Systems

- [x] All emission volume types working (box, sphere, cylinder, line, point) ✅ COMPLETE
- [x] Streak particles rendering with proper trails ✅ COMPLETE
- [x] Drawable particles attaching sprites correctly ✅ COMPLETE
- [x] Performance: 1000+ particles at 60 FPS ✅ COMPLETE (with batching optimization)
- [ ] Volume particles implementing volumetric effects (optional, not required for Generals)

### GUI/WND

- [x] ListBox supporting multi-selection modes ✅ COMPLETE
- [x] Tooltips displaying correctly ✅ COMPLETE
- [x] Dirty region tracking for rendering optimization ✅ COMPLETE
- [x] Window transitions smooth ✅ COMPLETE
- [x] All control types functional ✅ COMPLETE
- [x] Performance: Complex windows rendering at 60 FPS ✅ COMPLETE

---

## Resource Allocation

- **Primary Dev**: 100% allocation during Phase 1-2
- **Code Review**: Required at end of each phase
- **Testing**: Continuous throughout all phases
- **Documentation**: Updated with each phase completion

---

## Risk Assessment

### High Priority Risks

- **Water reflection rendering complexity**: Mitigation - reference existing shadow system
- **Particle streaming performance**: Mitigation - implement GPU sorting early
- **UI dirty region tracking overhead**: Mitigation - profile before implementing

### Medium Priority Risks

- **Cross-platform shader compatibility**: Mitigation - test Metal/Vulkan/D3D11
- **Complex window layouts**: Mitigation - start with simple cases
- **Particle LOD system**: Mitigation - implement basic culling first

### Low Priority Risks

- **Minor visual differences from original**: Mitigation - create visual comparison tests
- **Legacy code compatibility**: Mitigation - maintain backward compatibility

---

## Phase Details

For detailed implementation plans, see:

- [PHASE01_MAP_RENDERING.md](phases/PHASE01_MAP_RENDERING.md) - Map rendering implementation details
- [PHASE02_PARTICLE_SYSTEMS.md](phases/PHASE02_PARTICLE_SYSTEMS.md) - Particle system implementation details
- [PHASE03_GUI_RENDERING.md](phases/PHASE03_GUI_RENDERING.md) - GUI/WND implementation details
- [PHASE04_SCRIPTING_ENGINE.md](phases/PHASE04_SCRIPTING_ENGINE.md) - Game scripting & logic implementation

---

## Progress Tracking

### Phase 1 Progress

- [x] PLAN-001: Emission volumes - 100% ✅ COMPLETE
- [x] PLAN-002: Road rendering - 100% ✅ COMPLETE
- [x] PLAN-003: ListBox selection - 100% ✅ COMPLETE

### Phase 2 Progress

- [x] PLAN-004: Streak particles - 100% ✅ COMPLETE
- [x] PLAN-005: Drawable particles - 100% ✅ COMPLETE
- [x] PLAN-006: Water animation - 100% ✅ COMPLETE
- [x] PLAN-007: GUI dirty regions - 100% ✅ COMPLETE
- [x] PLAN-004: Waypoint Visualization - 100% ✅ COMPLETE (F8 hotkey working)

### Phase 3 Progress

- [x] PLAN-008: MULTIPLY blending - 100% ✅ COMPLETE
- [x] PLAN-009: Responsive layouts - 100% ✅ COMPLETE
- [x] PLAN-010: Particle limiting - 100% ✅ COMPLETE
- [x] PLAN-011: Tooltips - 100% ✅ COMPLETE

### Phase 4 Progress

- [x] PLAN-012: Particle Material Batching - 100% ✅ COMPLETE (Stage 2: Material-Based Batching + Integration + Profiling)
- [ ] PLAN-013: Texture atlasing - 0% (Planned, not started)
- [ ] PLAN-014: Asset streaming - 0% (Planned, not started)
- [x] PLAN-015: Performance profiling - 100% ✅ COMPLETE (Framework + Game Loop Integration)

---

## Links & References

- [OpenSAGE Implementation Status](../ETC/OPENSAGE_IMPLEMENTATION_STATUS.md)
- [Developer Guide](../developer-guide.md)
- [Coding Style](../coding-style.md)
- [Original Game Source](https://github.com/electronicarts/CnC_Generals_Zero_Hour)
- [OpenSAGE Repository](https://github.com/OpenSAGE/OpenSAGE)

