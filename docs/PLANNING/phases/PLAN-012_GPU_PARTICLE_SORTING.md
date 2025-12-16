# PLAN-012: GPU-Side Particle Sorting & Batching Optimization

**Status**: Analysis & Implementation Phase  
**Phase**: Phase 4 - Optimization & Performance  
**Start Date**: December 15, 2025  
**Estimated Completion**: December 17-18, 2025  

---

## Executive Summary

PLAN-012 optimizes particle rendering by implementing priority-based sorting and draw call batching. The current system renders each ParticleSystem independently, resulting in 50-150 draw calls per frame. By sorting systems by priority and batching compatible systems, we can reduce this to 15-40 draw calls while maintaining visual correctness.

**Key Achievement**: This plan removes the explicit "TODO: Sort by ParticleSystem.Priority" comment in ParticleSystemManager.Update()

---

## Current State Analysis (OpenSAGE)

### Problem 1: Unsorted Particle Systems

**Location**: `ParticleSystemManager.Update()` line 220

**Current Code**:

```csharp
public void Update(in TimeInterval gameTime)
{
    // TODO: Sort by ParticleSystem.Priority. ← THIS TODO
    
    for (var i = 0; i < _particleSystems.Count; i++)
    {
        var particleSystem = _particleSystems[i];
        particleSystem.Update(gameTime);
        // ... each system rendered independently
    }
}
```

**Issue**: Systems rendered in creation order, not priority order → incorrect visual layering

**Impact**: Depth sorting issues with multiple particle systems, visual artifacts

### Problem 2: Per-System Draw Calls

**Location**: `ParticleSystem.Render()` in rendering pipeline

**Current Architecture**:

- Each `ParticleSystem` → 1 draw call
- ~50-100 active systems → ~50-100 draw calls just for particles
- No batching across compatible systems

**Opportunity**: Systems with same (Texture, Shader, BlendMode) can batch

### Problem 3: No Cross-System Batching

**Current**: Each system manages its own vertex buffer

- System A: texture_001.dds, ADDITIVE blend
- System B: texture_001.dds, ADDITIVE blend (same settings!)
- System C: texture_002.dds, ADDITIVE blend

Result: 3 draw calls when they could be 2 (A+B batched, C separate)

---

## EA Generals Reference Architecture

### Priority System

```cpp
enum ParticlePriorityType {
    PARTICLE_PRIORITY_LOWEST = 0,      // Dust, smoke (most expendable)
    PARTICLE_PRIORITY_LOW,
    PARTICLE_PRIORITY_NORMAL,
    PARTICLE_PRIORITY_HIGH,
    PARTICLE_PRIORITY_VERY_HIGH,
    PARTICLE_PRIORITY_CRITICAL,        // Important effects
    PARTICLE_PRIORITY_WEAPON_EXPLOSION,
    PARTICLE_PRIORITY_HIGHEST,         // Final priority before ALWAYS_RENDER
    PARTICLE_PRIORITY_ALWAYS_RENDER,   // Never culled (final frame effects)
};
```

### Data Structures

```cpp
// Separate linked lists per priority
Particle* m_allParticlesHead[NUM_PARTICLE_PRIORITIES];
Particle* m_allParticlesTail[NUM_PARTICLE_PRIORITIES];

// FIFO removal algorithm (lowest priority first)
void removeOldestParticles() {
    for (int priority = 0; priority < NUM_PARTICLE_PRIORITIES; priority++) {
        // Remove particles from front of this priority's list
        Particle* p = m_allParticlesHead[priority];
        while (p && count < target) {
            Particle* next = p->m_overallNext;
            remove(p);
            p = next;
        }
    }
}
```

### Rendering Strategy

1. Iterate particle systems in priority order
2. Group by material (texture + shader + blend mode)
3. Issue single draw call per material group
4. Restore state changes only when necessary

---

## Implementation Plan

### Stage 1: Priority-Based System Sorting ✅ FOUNDATION READY

**Goal**: Render particle systems in priority order

**Implementation**:
```csharp
public void Update(in TimeInterval gameTime)
{
    // Step 1: Update all systems (unchanged)
    for (var i = 0; i < _particleSystems.Count; i++)
    {
        var particleSystem = _particleSystems[i];
        if (particleSystem.State != ParticleSystemState.Inactive)
        {
            particleSystem.Update(gameTime);
        }
    }
    
    // Step 2: Sort by priority (NEW)
    _particleSystems.Sort((a, b) => 
        b.Template.Priority.CompareTo(a.Template.Priority));
    
    // Step 3: Update render bucket in priority order (NEW)
    _renderBucket.Clear();
    for (var i = 0; i < _particleSystems.Count; i++)
    {
        if (_particleSystems[i].State != ParticleSystemState.Inactive)
        {
            _renderBucket.AddObject(_particleSystems[i]);
        }
    }
}
```

**Expected Result**: Particle systems rendered in correct priority order

**Performance Impact**: Minimal (<0.1ms sort overhead for 50-100 systems)

### Stage 2: Material-Based Batching

**Goal**: Reduce draw calls by batching compatible systems

**Key Insight**: ParticleSystem already has a MaterialPass property determined by:

- ParticleSystemShader type (Alpha, Additive, Multiply)
- IsGroundAligned boolean
- Texture reference

**Strategy**:

```csharp
// In rendering pipeline
private void RenderParticles(CommandList commandList)
{
    // Group systems by material
    var materialGroups = new Dictionary<MaterialKey, List<ParticleSystem>>();
    
    foreach (var system in _particleSystems)
    {
        var key = new MaterialKey(
            system.MaterialPass,
            system.Texture,
            system.Template.Shader);
        
        if (!materialGroups.TryGetValue(key, out var group))
        {
            group = new List<ParticleSystem>();
            materialGroups[key] = group;
        }
        group.Add(system);
    }
    
    // Render each material group
    foreach (var (material, systems) in materialGroups)
    {
        commandList.SetPipeline(material.Pipeline);
        
        foreach (var system in systems)
        {
            commandList.SetVertexBuffer(system.VertexBuffer);
            commandList.DrawIndexed(system.IndexCount, 1, 0, 0, 0);
        }
    }
}
```

**Expected Result**: 50-100 draw calls → 15-40 draw calls (~60-70% reduction)

### Stage 3: Dirty Flag Optimization (Optional)

**Goal**: Skip upload of unchanged particle data

**Implementation**:

```csharp
public class ParticleSystem
{
    private bool _isDirty = true;  // Mark as needing upload
    
    public void Update(TimeInterval gameTime)
    {
        _isDirty = false;
        
        // Update particles
        for (int i = 0; i < _particles.Length; i++)
        {
            if (_particles[i].IsAlive)
            {
                UpdateParticle(ref _particles[i], gameTime);
                _isDirty = true;  // Mark dirty if any particle changed
            }
        }
    }
    
    public void Render(CommandList commandList)
    {
        if (_isDirty)
        {
            // Only upload if dirty
            commandList.UpdateBuffer(_vertexBuffer, _particleVertexData);
        }
        commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
    }
}
```

**Expected Gain**: 20-30% CPU overhead reduction on particle uploads

---

## Verification Against EA Source

| Feature | EA Generals | OpenSAGE Current | OpenSAGE After PLAN-012 |
|---------|-------------|------------------|------------------------|
| Priority Levels | 9 (ParticlePriorityType) | 5 (ParticleSystemPriority) | ✅ Same 5 levels |
| FIFO Culling | ✅ Implemented | ✅ Implemented | ✅ Maintained |
| Per-Priority Lists | ✅ Yes | ✅ Yes (added in Phase 3) | ✅ Maintained |
| System Sorting | ✅ By priority | ❌ TODO | ✅ Implemented |
| Draw Call Batching | ✅ Material-based | ❌ No | ✅ Implemented |
| Visible Culling | ✅ Yes | ✅ Yes | ✅ Maintained |

---

## Implementation Details

### File: ParticleSystemManager.cs

**Changes**:

1. Add sorting in Update() method (remove TODO)
2. Maintain sorted order in _renderBucket
3. Add ParticleSystemComparer for consistent sorting

### New Class: ParticleSystemComparer

```csharp
/// <summary>
/// Compares particle systems by priority for correct rendering order.
/// Higher priority systems render first (back to front for transparency).
/// Reference: EA Generals rendering depth for transparent objects.
/// </summary>
internal sealed class ParticleSystemComparer : IComparer<ParticleSystem>
{
    public int Compare(ParticleSystem? a, ParticleSystem? b)
    {
        if (a == null && b == null) return 0;
        if (a == null) return 1;
        if (b == null) return -1;
        
        // Higher priority first (descending sort)
        // ALWAYS_RENDER > CRITICAL > ... > LOWEST
        int result = b.Template.Priority.CompareTo(a.Template.Priority);
        
        // Stable sort by system ID if same priority
        if (result == 0)
        {
            result = a.SystemId.CompareTo(b.SystemId);
        }
        
        return result;
    }
}
```

### Integration with Rendering Pipeline

**Location**: RenderPipeline or appropriate render pass

**Logic**:

```csharp
private void RenderParticleSystems(CommandList commandList)
{
    // Particles are added to render bucket in sorted priority order
    // by ParticleSystemManager.Update()
    
    MaterialKey? currentMaterial = null;
    
    foreach (var particleSystem in _particleSystemManager.GetSortedSystems())
    {
        var material = GetMaterialKey(particleSystem);
        
        // Change pipeline only if material changed
        if (currentMaterial != material)
        {
            commandList.SetPipeline(material.Pipeline);
            currentMaterial = material;
        }
        
        particleSystem.Render(commandList);
    }
}
```

---

## Acceptance Criteria

- ✅ Particle systems rendered in priority order (solves TODO)
- ✅ No visual artifacts or changed behavior
- ✅ Draw calls reduced by at least 40% (typical: 50%)
- ✅ Consistent with EA Generals priority system
- ✅ All existing particle effects work correctly
- ✅ Performance profiling shows measurable improvement
- ✅ No regressions in other rendering systems
- ✅ Unit tests for sorting logic

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public void ParticleSystemSorting_HigherPriorityFirst()
{
    // Create systems with different priorities
    var lowPriority = CreateSystem(ParticleSystemPriority.VeryLow);
    var highPriority = CreateSystem(ParticleSystemPriority.VeryHigh);
    var critical = CreateSystem(ParticleSystemPriority.Critical);
    
    manager.Add(lowPriority);
    manager.Add(critical);
    manager.Add(highPriority);
    
    manager.Update(0.016f);
    
    // Verify sort order: critical, high, low
    var sorted = manager.GetSortedSystems();
    Assert.Equal(critical, sorted[0]);
    Assert.Equal(highPriority, sorted[1]);
    Assert.Equal(lowPriority, sorted[2]);
}

[Fact]
public void DrawCallReduction_MaterialBatching()
{
    // Create 10 systems with only 2 unique materials
    CreateAndAddSystems(10, 2);
    
    var drawCalls = CountDrawCalls();
    
    // Should be ~2 draw calls (one per material), not 10
    Assert.True(drawCalls <= 3);  // Allow for state changes
}
```

### Integration Tests

- Verify no visual artifacts in particle rendering
- Confirm profiler shows reduced draw calls
- Validate memory usage unchanged
- Test with all particle system types

### Performance Benchmarks

- Measure draw call count before/after
- Measure GPU time before/after
- Measure CPU sort overhead
- Verify frame time improvement

---

## Risks & Mitigations

### Risk 1: Sort Performance

**Risk**: Sorting on every frame could be expensive

**Mitigation**:

- Only sort if particle system list changed (dirty flag)
- Use fast comparer (just priority + ID)
- Profile to verify overhead <0.5ms

### Risk 2: Visual Regression

**Risk**: Wrong sort order could cause visual artifacts

**Mitigation**:

- Implement unit tests for sort correctness
- Compare visual output with original
- Test with all particle system types

### Risk 3: Breaking Changes

**Risk**: Render bucket behavior change could break other systems

**Mitigation**:

- Ensure only particle systems affected
- Don't modify render bucket API
- Test other renderable types unchanged

### Risk 4: Batch Incompatibility

**Risk**: Batching logic might miss compatible systems

**Mitigation**:

- Material key must include all state that affects rendering
- Add assertions to catch missing keys
- Profile to verify expected batching

---

## Related Files

- [PHASE04_OPTIMIZATION_ANALYSIS.md](PHASE04_OPTIMIZATION_ANALYSIS.md) - Phase 4 overview
- [PLAN-015_PROFILING_FRAMEWORK.md](PLAN-015_PROFILING_FRAMEWORK.md) - Profiling for measurement
- `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs` - Main implementation file
- `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs` - Individual system rendering

---

## Success Metrics

| Metric | Target | Measurement |
|--------|--------|-------------|
| Sort Implementation | Remove TODO | Code review ✅ |
| Draw Call Reduction | 40%+ | PerfGather profiling |
| Performance Impact | <0.5ms overhead | Stopwatch measurement |
| Visual Correctness | No artifacts | Visual comparison tests |
| Priority Ordering | 100% correct | Unit tests |
| Batch Effectiveness | 50%+ reduction | Draw call count |

---

## Next Steps

1. **Implement priority sorting** in ParticleSystemManager.Update()
2. **Add unit tests** for sorting and batching logic
3. **Integrate profiling** using PLAN-015 PerfGather
4. **Benchmark before/after** draw call counts
5. **Visual validation** with multiple particle effects
6. **Commit and document** results

---

**Status**: Ready for implementation  
**Foundation**: PLAN-015 profiling framework complete  
**Priority**: High (removes TODO, improves performance)  
**Dependencies**: PLAN-004, PLAN-005 (particle systems must be complete)

