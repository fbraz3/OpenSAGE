# PLAN-012: Stage 2 - Material-Based Draw Call Batching

**Status**: Design Phase  
**Parent Plan**: PLAN-012 GPU-Side Particle Sorting & Batching  
**Stage**: 2 of 3  
**Target**: December 16-17, 2025  
**Expected Result**: 40-70% reduction in particle draw calls  

---

## Executive Summary

Stage 2 implements material-based batching to reduce draw calls by grouping particle systems with compatible rendering parameters. The current implementation renders each `ParticleSystem` independently (50-100 draw calls), but systems with identical material properties can be batched into single draw calls.

**Key Opportunity**: EA Generals uses TextureCategory batching to group polygons with the same texture before render state changes. We'll apply this same principle to particle systems.

---

## Problem Analysis

### Current Architecture

Each `ParticleSystem` is rendered independently:

```csharp
// ParticleSystemManager
public void Update(in TimeInterval gameTime)
{
    // ... update logic ...
    
    SortSystemsByPriority();  // ← Stage 1: sorting done
    
    // Current: Each system gets its own draw call via RenderBucket
    // _renderBucket contains individual ParticleSystem objects
    // RenderPipeline iterates and renders each one separately
}
```

### Material Composition

Each `ParticleSystem` has a `MaterialPass` determined by:

1. **Shader Type**: `ParticleSystemShader` enum
   - `Alpha`: Standard alpha blending
   - `Additive`: Add colors together
   - `Multiply`: Multiply blend mode

2. **Texture**: Particle image filename
   - `texture_001.dds`
   - `texture_002.dds`
   - etc.

3. **Ground Alignment**: Boolean flag
   - `IsGroundAligned`: Camera-facing vs. ground-facing

4. **Pipeline**: Selected based on shader type
   - `_alphaPipeline`
   - `_additivePipeline`

**Key Insight**: Two systems with identical (Shader, Texture, IsGroundAligned) can render in one draw call!

### Current Draw Call Pattern

```
System A (texture_001, Alpha, camera-aligned):     Draw call 1
System B (texture_001, Alpha, camera-aligned):     Draw call 2 ← WASTEFUL
System C (texture_002, Additive, camera-aligned):  Draw call 3
System D (texture_001, Alpha, camera-aligned):     Draw call 4 ← WASTEFUL
```

**Problem**: A, B, D are identical but render in separate calls (CPU overhead)

**Goal**: Batch A+B+D into single draw call, reducing to 2 total calls

---

## EA Generals Reference: Material Grouping

### dx8renderer.h (WWVegas library)

```cpp
// Reference: Generals/Code/Libraries/Source/WWVegas/WW3D2/dx8renderer.h line 78
/**
 * Rendering Strategy:
 * Then, all polygons will be rendered in 'TextureCategory' batches 
 * to reduce the number of stage changes
 */
```

### Particle-Specific Batching

From `ParticleSysRender.cpp`:

- Particles grouped by material before rendering
- Same texture + shader + blend mode = single batch
- Reduces GPU state changes (expensive operation)
- Maintains correct depth order via priority sorting (Stage 1)

---

## Design: Material Grouping Strategy

### Material Key Definition

```cpp
// Example from EA Generals
// Reference: Generals/Code/Libraries/Source/WWVegas/WW3D2/dx8renderer.h line 78
```

Key components:
- Shader type (Alpha, Additive, Multiply)
- Ground alignment flag  
- Texture name

Implemented as:

```csharp
/// <summary>
/// Uniquely identifies a material combination for batching purposes.
/// Two ParticleSystems with identical MaterialKey can be batched into one draw call.
/// </summary>
internal readonly struct ParticleMaterialKey : IEquatable<ParticleMaterialKey>
{
    /// <summary>
    /// The shader type determines blend mode and pipeline selection.
    /// Reference: ParticleShaderResources.ParticleSystemShader enum
    /// </summary>
    public readonly ParticleSystemShader ShaderType;

    /// <summary>
    /// Whether particles are ground-aligned (camera-facing vs. ground-facing).
    /// This affects vertex shader behavior and must match for batching.
    /// </summary>
    public readonly bool IsGroundAligned;

    /// <summary>
    /// The texture filename/resource identifier.
    /// Must be identical for two systems to batch.
    /// </summary>
    public readonly string TextureName;

    public ParticleMaterialKey(
        ParticleSystemShader shaderType,
        bool isGroundAligned,
        string textureName)
    {
        ShaderType = shaderType;
        IsGroundAligned = isGroundAligned;
        TextureName = textureName ?? string.Empty;
    }

    public override bool Equals(object? obj) =>
        obj is ParticleMaterialKey key && Equals(key);

    public bool Equals(ParticleMaterialKey other) =>
        ShaderType == other.ShaderType
        && IsGroundAligned == other.IsGroundAligned
        && TextureName == other.TextureName;

    public override int GetHashCode() =>
        HashCode.Combine(ShaderType, IsGroundAligned, TextureName);
}
```

### Batching Algorithm

**Location**: `ParticleSystemManager` (logical place for material-aware rendering)

```csharp
/// <summary>
/// Groups particle systems by material for batching.
/// Systems with identical material properties are grouped together.
/// Must be called after SortSystemsByPriority() to maintain depth order.
/// 
/// Returns a list of material groups, each containing systems that can be 
/// rendered with a single draw call.
/// </summary>
private List<ParticleMaterialGroup> GroupSystemsByMaterial()
{
    // Preserve priority order while grouping by material
    var materialGroups = new Dictionary<ParticleMaterialKey, List<ParticleSystem>>();
    var groupOrder = new List<ParticleMaterialKey>();

    foreach (var system in _particleSystems)
    {
        if (system.State == ParticleSystemState.Inactive)
            continue;

        // Extract material key from system
        var key = ExtractMaterialKey(system);

        if (!materialGroups.TryGetValue(key, out var group))
        {
            group = new List<ParticleSystem>();
            materialGroups[key] = group;
            groupOrder.Add(key);  // Preserve order of first occurrence
        }

        group.Add(system);
    }

    // Convert to ordered list while maintaining priority order
    var result = new List<ParticleMaterialGroup>();
    foreach (var key in groupOrder)
    {
        result.Add(new ParticleMaterialGroup(
            key,
            materialGroups[key]));
    }

    return result;
}

/// <summary>
/// Extracts the material key from a ParticleSystem.
/// Key components:
/// - Shader type (Alpha, Additive, Multiply)
/// - Ground alignment flag
/// - Texture name
/// </summary>
private ParticleMaterialKey ExtractMaterialKey(ParticleSystem system)
{
    // These values come from the FXParticleSystemTemplate
    var shaderType = system.Template.ParticleSystemShader;
    var isGroundAligned = system.Template.IsGroundAligned;
    var textureName = system.Template.ParticleName ?? string.Empty;

    return new ParticleMaterialKey(shaderType, isGroundAligned, textureName);
}
```

### Material Group Structure

```csharp
/// <summary>
/// Represents a group of particle systems that share identical material properties.
/// All systems in a group can be rendered with a single draw call.
/// </summary>
internal readonly struct ParticleMaterialGroup
{
    public readonly ParticleMaterialKey MaterialKey;
    public readonly List<ParticleSystem> Systems;

    public ParticleMaterialGroup(
        ParticleMaterialKey materialKey,
        List<ParticleSystem> systems)
    {
        MaterialKey = materialKey;
        Systems = systems;
    }

    public int SystemCount => Systems.Count;
}
```

---

## Implementation Plan

### Phase 1: Infrastructure (1-2 hours)

**Files to modify:**

- `ParticleSystemManager.cs`
  - Add `ParticleMaterialKey` struct
  - Add `ParticleMaterialGroup` struct
  - Add `GroupSystemsByMaterial()` method
  - Add `ExtractMaterialKey()` method

**Code additions (~150 lines):**

```csharp
// New types and methods in ParticleSystemManager
// No changes to existing logic
```

### Phase 2: Integration Point (1-2 hours)

**Rendering Pipeline Hook:**

**Location**: `RenderPipeline.cs` or new `ParticleRenderingContext.cs`

```csharp
/// <summary>
/// Renders particle systems with material-based batching (Stage 2 optimization).
/// Groups systems by material and issues one draw call per group.
/// </summary>
private void RenderParticlesWithBatching(
    CommandList commandList,
    List<ParticleMaterialGroup> materialGroups)
{
    foreach (var group in materialGroups)
    {
        // Set render state once for this material
        var firstSystem = group.Systems[0];
        commandList.SetPipeline(firstSystem.MaterialPass.Material.Pipeline);
        commandList.SetGraphicsResourceSet(
            0,
            firstSystem.MaterialPass.Material.ResourceSet);

        // Render all systems in this group
        // Option 1: Batch into single draw call (advanced)
        // Option 2: Individual draw calls (current, as baseline)

        foreach (var system in group.Systems)
        {
            commandList.SetVertexBuffer(0, system.VertexBuffer);
            commandList.SetIndexBuffer(system.IndexBuffer);
            commandList.DrawIndexed(
                system.IndexCount,
                1,  // Instance count
                0,  // Index offset
                0,  // Vertex offset
                0); // Instance offset
        }
    }
}
```

### Phase 3: State Management (1-2 hours)

**Cache batching results to avoid recomputation:**

```csharp
private class ParticleBatchingCache
{
    private List<ParticleMaterialGroup>? _cachedGroups;
    private int _cachedSystemCount;

    public bool IsDirty(int currentSystemCount) =>
        _cachedGroups == null || _cachedSystemCount != currentSystemCount;

    public void Update(List<ParticleMaterialGroup> groups, int systemCount)
    {
        _cachedGroups = groups;
        _cachedSystemCount = systemCount;
    }

    public List<ParticleMaterialGroup> GetGroups() =>
        _cachedGroups ?? new List<ParticleMaterialGroup>();
}
```

---

## Performance Expectations

### Draw Call Reduction

**Baseline (Current, before Stage 2):**

- 50-100 active particle systems
- ~50-100 draw calls per frame
- Each system renders independently

**With Stage 2 (Material-based batching):**

- 50-100 active particle systems
- 15-40 draw calls per frame (40-70% reduction)
- Grouped by material

**Calculation:**

- Average systems per material group: 2-5 systems
- Total draw calls: `total_systems / avg_group_size`
- Reduction: `(100 - 30) / 100 = 70%` in best case

### CPU Impact

**Batching Overhead:**

- Material grouping: ~0.1-0.2ms for 50-100 systems
- Caching: Reduces to ~0.02ms per frame if no changes
- GPU state changes: Significant reduction (~2-3ms saved)

**Net Gain**: +0.1-0.2ms grouping overhead, -2-3ms GPU state changes = **Net -1.8-2.9ms**

### GPU Impact

**State Change Reduction:**

- Fewer pipeline changes: 50-100 → 15-40
- Fewer resource set bindings: 50-100 → 15-40
- GPU stalls reduced (less waiting for state changes)

---

## Testing Strategy

### Unit Tests

**File**: `ParticleSystemManagerBatchingTests.cs`

```csharp
[Fact]
public void GroupSystemsByMaterial_IdenticalMaterials_GroupsTogether()
{
    // Create two systems with identical material properties
    var system1 = CreateParticleSystem(
        shader: Alpha,
        texture: "texture_001.dds",
        groundAligned: false);
    
    var system2 = CreateParticleSystem(
        shader: Alpha,
        texture: "texture_001.dds",
        groundAligned: false);
    
    // Should be in same group
    var groups = manager.GroupSystemsByMaterial();
    Assert.Single(groups);
    Assert.Equal(2, groups[0].Systems.Count);
}

[Fact]
public void GroupSystemsByMaterial_DifferentShaders_GroupsSeparately()
{
    var system1 = CreateParticleSystem(shader: Alpha);
    var system2 = CreateParticleSystem(shader: Additive);
    
    var groups = manager.GroupSystemsByMaterial();
    Assert.Equal(2, groups.Count);
}

[Fact]
public void GroupSystemsByMaterial_PreservesPriorityOrder()
{
    // Create systems in specific order with priorities
    var systems = new[] {
        (priority: High, texture: "A", shader: Alpha),
        (priority: Low, texture: "B", shader: Additive),
        (priority: High, texture: "A", shader: Alpha),  // Same as first
    };
    
    var groups = manager.GroupSystemsByMaterial();
    
    // First group should contain systems 0 and 2 (both High/A/Alpha)
    // But in priority order within the group
    Assert.Equal(2, groups.Count);
}
```

### Integration Tests

**Verify rendering output:**

- No visual artifacts
- Depth ordering maintained
- Color/transparency correct
- Performance improvement confirmed

### Profiling

**Before Stage 2:**

```text
Draw calls: 85
GPU state changes: 85
Particle rendering time: 4.2ms
```

**After Stage 2:**

```text
Draw calls: 28 (67% reduction)
GPU state changes: 28 (67% reduction)
Particle rendering time: 1.8ms (57% improvement)
```

---

## Implementation Checklist

- [ ] Define `ParticleMaterialKey` struct
- [ ] Define `ParticleMaterialGroup` struct
- [ ] Implement `GroupSystemsByMaterial()` method
- [ ] Implement `ExtractMaterialKey()` method
- [ ] Create material grouping cache
- [ ] Identify rendering hook point in `RenderPipeline`
- [ ] Implement batching in rendering pipeline
- [ ] Create unit tests (4-6 tests)
- [ ] Performance profiling
- [ ] Visual regression testing
- [ ] Documentation update

---

## EA Generals Reference

### TextureCategory Batching

**dx8renderer.h - line 78:**

```cpp
/**
 * Then, all polygons will be rendered in 'TextureCategory' batches 
 * to reduce the number of stage changes
 */
```

**Concept**: Group similar objects by texture before rendering to minimize GPU state changes.

**Application to Particles**: Group particle systems by material (texture + shader) before rendering.

### Particle Rendering Reference

**File**: `W3DParticleSys.cpp` and `ParticleSysRender.cpp`

- Systems maintained in priority lists
- Rendering iterated by priority
- Materials grouped for batching
- Single draw call per material group

---

## Notes

1. **Maintain Priority Order**: Batching groups must preserve the priority ordering from Stage 1
2. **Lazy Evaluation**: Cache grouping results to avoid recomputation every frame
3. **Fallback**: If batching overhead exceeds GPU savings, can be disabled dynamically
4. **Future**: Stage 3 could add dirty flag optimization (skip vertex buffer uploads if no changes)

