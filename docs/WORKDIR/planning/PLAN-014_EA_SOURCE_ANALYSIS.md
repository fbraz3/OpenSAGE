# PLAN-014: EA Source Code Analysis & Reference Architecture

**Date**: December 2025  
**Source**: `references/generals_code/GeneralsMD/Code/GameEngine/{Include,Source}/Common/GameLOD.{h,cpp}`  
**Analysis Focus**: LOD System Architecture, Hardware Detection, Static/Dynamic LOD Implementation

---

## Key Findings from EA Implementation

### 1. Hardware Detection & CPU Benchmarking

The EA implementation uses a sophisticated **profiling system** rather than simple CPU detection:

```cpp
// From GameLOD.cpp (lines 300+)
extern Bool testMinimumRequirements(ChipsetType *videoChipType, CpuType *cpuType, 
    Int *cpuFreq, Int *numRAM, Real *intBenchIndex, Real *floatBenchIndex, Real *memBenchIndex);
```

**Critical Details**:
- **Three benchmark indices**: `intBenchIndex`, `floatBenchIndex`, `memBenchIndex`
- **Composite performance**: `m_compositeBenchIndex = m_intBenchIndex + m_floatBenchIndex`
- **Profile matching threshold**: `PROFILE_ERROR_LIMIT = 0.94f` (allows 6% variance)
- **Profiles array**: Pre-populated `m_benchProfiles[MAX_BENCH_PROFILES]` (max 16 profiles)
- **Preset matching**: If benchmark matches a profile, iterate HIGH→LOW LOD to find highest matching preset

**Algorithm Flow**:
1. Call `testMinimumRequirements()` to get CPU/GPU/RAM/benchmarks
2. If unknown CPU type or forced benchmark, run benchmark to get indices
3. Compare benchmark indices to all `BenchProfile` entries (within `PROFILE_ERROR_LIMIT`)
4. For each matching profile, find highest LOD preset matching CPU MHz
5. Store detected: `m_cpuType`, `m_cpuFreq`, `m_videoChipType`, `m_numRAM`

### 2. Static LOD Presets & Matching

**LODPresetInfo Structure**:
```cpp
struct LODPresetInfo {
    CpuType m_cpuType;              // P3, P4, K7, XX (unknown)
    Int m_mhz;
    Real m_cpuPerfIndex;            // Used for unidentified CPUs
    ChipsetType m_videoType;        // GeForce3, Radeon 8500, etc.
    Int m_memory;                   // RAM in MB
};
```

**Matching Logic** (3-way requirement):
1. CPU type must match `m_cpuType`
2. CPU frequency must exceed preset's MHz (with error limit)
3. Video card must match OR generic pixel shader level
4. System RAM must exceed preset's requirement

**Allowed LOD Levels** (3, not 5):
- `STATIC_GAME_LOD_LOW` (index 0)
- `STATIC_GAME_LOD_MEDIUM` (index 1)
- `STATIC_GAME_LOD_HIGH` (index 2)
- `STATIC_GAME_LOD_CUSTOM` (index 3, user-defined)

### 3. StaticGameLODInfo Parameters

The EA implementation includes **23 distinct LOD parameters**:

```cpp
struct StaticGameLODInfo {
    // Performance gates
    Int m_minFPS;                       // Minimum recommended FPS for this LOD
    Int m_minProcessorFPS;              // Minimum CPU time (ms) to recommend
    
    // Audio parameters
    Int m_sampleCount2D;                // UI audio samples (default: 6)
    Int m_sampleCount3D;                // World audio samples (default: 24)
    Int m_streamCount;                  // Streaming audio sources (default: 2)
    
    // Particle system
    Int m_maxParticleCount;             // Cap on simultaneous particles
    
    // Shadow rendering
    Bool m_useShadowVolumes;            // Volumetric shadows
    Bool m_useShadowDecals;             // 2D decal shadows
    
    // Terrain visual effects
    Bool m_useCloudMap;                 // Cloud shadow scrolling
    Bool m_useLightMap;                 // Noise pattern to break tiling
    Bool m_showSoftWaterEdge;           // Feathered water edges
    
    // Tank tracks
    Int m_maxTankTrackEdges;            // Maximum track length
    Int m_maxTankTrackOpaqueEdges;      // Fade start length
    Int m_maxTankTrackFadeDelay;        // Fade persistence time (ms)
    
    // Visual polish
    Bool m_useBuildupScaffolds;         // Scaffolding during construction
    Bool m_useTreeSway;                 // Wind animation on trees
    Bool m_useEmissiveNightMaterials;   // Second lighting pass for night
    Bool m_useHeatEffects;              // Heat distortion (e.g., Microwave Tank)
    
    // Performance settings
    Int m_textureReduction;             // Divide resolution by 2^n
    Bool m_useFpsLimit;                 // Cap FPS (don't lock to 30Hz)
    Bool m_enableDynamicLOD;            // Allow FPS-based adjustment
    Bool m_useTrees;                    // Include trees on map
};
```

**Default Preset** (from constructor):
```cpp
m_minFPS = 0;
m_sampleCount2D = 6;
m_sampleCount3D = 24;
m_streamCount = 2;
m_maxParticleCount = 2500;
m_useShadowVolumes = TRUE;
m_useShadowDecals = TRUE;
m_useCloudMap = TRUE;
m_useLightMap = TRUE;
m_showSoftWaterEdge = TRUE;
m_textureReduction = 0;         // No reduction
m_useFpsLimit = TRUE;
m_enableDynamicLOD = TRUE;
m_useTrees = TRUE;
```

### 4. Dynamic LOD System

**DynamicGameLODInfo Structure**:
```cpp
struct DynamicGameLODInfo {
    Int m_minFPS;                           // Threshold FPS for this level
    UnsignedInt m_dynamicParticleSkipMask;  // Bitmask for particle skipping
    UnsignedInt m_dynamicDebrisSkipMask;    // Bitmask for debris skipping
    Real m_slowDeathScale;                  // Death animation scale (< 1.0 = faster)
    ParticlePriorityType m_minDynamicParticlePriority;  // Minimum priority to render
    ParticlePriorityType m_minDynamicParticleSkipPriority;  // Never skip at this level+
};
```

**Skip Mask Algorithm** (Genius bit manipulation):
```cpp
// From GameLOD.h inline functions
Bool GameLODManager::isParticleSkipped(void) {
    return (++m_numParticleGenerations & m_dynamicParticleSkipMask) != m_dynamicParticleSkipMask;
}

Bool GameLODManager::isDebrisSkipped(void) {
    return (++m_numDebrisGenerations & m_dynamicDebrisSkipMask) != m_dynamicDebrisSkipMask;
}
```

**Why This Works**:
- Counter increments EVERY particle/debris created
- Bitmask determines which N-th item to render
- `0x000000FF` = render 1 of 256 (very low FPS)
- `0x00000003` = render 1 of 4 (high FPS)
- `0x00000000` = render all (very high FPS)

**Allowed Dynamic Levels** (4 levels):
- `DYNAMIC_GAME_LOD_LOW` (most aggressive skipping)
- `DYNAMIC_GAME_LOD_MEDIUM`
- `DYNAMIC_GAME_LOD_HIGH`
- `DYNAMIC_GAME_LOD_VERY_HIGH` (no skipping)

### 5. INI Configuration System

The system loads two INI files at initialization:

**GameLOD.ini**:
- Definition of all Static LOD levels (Low, Medium, High, Custom)
- Definition of all Dynamic LOD levels (Low, Medium, High, VeryHigh)
- Parameter values for each level

**GameLODPresets.ini**:
- Hardware preset configurations
- Maps hardware specs to recommended static LOD levels
- Contains CPU, GPU, RAM requirements for each preset

### 6. User Preference Integration

The EA implementation integrates with user preferences:

```cpp
// From GameLOD.cpp
if (userSetDetail == STATIC_GAME_LOD_CUSTOM) {
    TheWritableGlobalData->m_textureReductionFactor = optionPref.getTextureReduction();
    TheWritableGlobalData->m_useShadowVolumes = optionPref.get3DShadowsEnabled();
    TheWritableGlobalData->m_useShadowDecals = optionPref.get2DShadowsEnabled();
    TheWritableGlobalData->m_maxParticleCount = optionPref.getParticleCap();
    TheWritableGlobalData->m_enableDynamicLOD = optionPref.getDynamicLODEnabled();
    TheWritableGlobalData->m_useFpsLimit = optionPref.getFPSLimitEnabled();
    // ... more settings ...
}
```

**Pattern**: Settings can be loaded from user preferences OR predetermined LOD presets.

---

## Architecture Decisions Based on EA Code

### Decision 1: Benchmark Profiling vs Simple Detection ✅

**EA Approach**: Sophisticated multi-index profiling (Int, Float, Memory benchmarks)  
**Rationale**: Different apps use CPU differently; single MHz isn't sufficient  
**OpenSAGE Implementation**: 
- ✅ Support simple hardware detection first
- ✅ Add optional benchmarking for refined LOD selection
- ✅ Use composite index like EA does

### Decision 2: LOD Level Count

**EA Approach**: 3 static levels + 1 custom = 4 total; 4 dynamic levels  
**Rationale**: More levels = more testing; fewer levels = simpler presets  
**OpenSAGE Recommendation**: 
- ✅ Follow EA: 3 static (Low, Medium, High) + custom
- ✅ Follow EA: 4 dynamic levels with increasing particle rendering

### Decision 3: Preset Matching Algorithm

**EA Approach**: Iterative matching from HIGH→LOW LOD, with error tolerance  
**Key Feature**: `PROFILE_ERROR_LIMIT = 0.94f` allows 6% variance  
**Rationale**: Exact matches unlikely; some tolerance needed  
**OpenSAGE Implementation**:
- ✅ Implement tolerance-based matching
- ✅ Iterate from highest to lowest (user gets best possible LOD)

### Decision 4: Skip Mask Algorithm

**EA Approach**: Counter-based bitmask for particle/debris skipping  
**Advantage**: CPU efficient, no per-particle branching  
**OpenSAGE Implementation**:
- ✅ Use EA's exact algorithm for dynamic LOD skipping
- ✅ Inline functions for performance

### Decision 5: Global Data Integration

**EA Approach**: LOD settings modify `TheGlobalData` values  
**Pattern**: LOD manager is "setter", all systems read from GlobalData  
**OpenSAGE Integration Point**: Hook into existing `GameData` system

---

## Critical Implementation Points

### 1. Hardware Detection Integration

**Location in OpenSAGE**: `src/OpenSage.Game/GameLogic/Lod/HardwareDetector.cs`

Must map to EA's detection function results:
- CPU type (P3, P4, K7)
- CPU frequency (MHz)
- GPU chipset type
- RAM (MB)
- Optional: Benchmark indices

### 2. INI Parsing

**Files to verify**:
- `Data/INI/GameLOD.ini` - Verify all parameters are parsed
- `Data/INI/GameLODPresets.ini` - Verify preset loading

**Current Status**: Need to verify these files exist in game data

### 3. Global Data Update Points

When LOD changes, update these GlobalData values:
- `m_textureReductionFactor`
- `m_maxParticleCount`
- `m_useShadowVolumes`, `m_useShadowDecals`
- `m_useCloudMap`, `m_useLightMap`
- `m_showSoftWaterEdge`
- `m_useBuildupScaffolds`
- `m_useHeatEffects`
- `m_useTreeSway`
- `m_useTrees`
- `m_enableDynamicLOD`
- `m_useFpsLimit`

### 4. Dynamic LOD Update Loop

**Frequency**: Every 1 second (not every frame)  
**Hysteresis**: Avoid LOD thrashing (±5 FPS threshold recommended)  
**Location**: `Game.Update()` loop

### 5. Particle/Debris Skip Integration

**Pattern**: Every system that creates particles/debris calls:
```csharp
if (GameLodManager.IsParticleSkipped()) {
    return;  // Don't create this particle
}
```

---

## Test Strategy (Based on EA Implementation)

### 1. Hardware Detection Tests
- Verify detection results match expected values
- Test with known hardware profiles
- Verify benchmark calculation accuracy

### 2. Preset Matching Tests
- Test matching within `PROFILE_ERROR_LIMIT` (6% tolerance)
- Test exact matches
- Test boundary cases (just below/above threshold)
- Test iteration from HIGH→LOW priority

### 3. Static LOD Application Tests
- Load each LOD level
- Verify GlobalData is updated correctly
- Verify all UI elements reflect new LOD

### 4. Dynamic LOD Tests
- Simulate FPS changes
- Verify LOD transitions smooth
- Verify hysteresis prevents thrashing
- Test particle skip masks: verify pattern

### 5. Integration Tests
- Play game at each static LOD
- Verify all systems respect particle caps
- Verify texture reduction applies
- Monitor memory usage

---

## Risk Mitigation

### Risk 1: Benchmark Profiling Too Complex

**Mitigation**: Start without benchmarking; use simple hardware specs  
**Phased Approach**:
1. Phase 1: Simple detection + preset matching (no benchmarks)
2. Phase 2: Add optional benchmarking for refined selection
3. Phase 3: Composite index calculation and profile matching

### Risk 2: INI Files Don't Exist

**Mitigation**: Check game data directory  
**Fallback**: Embed reasonable defaults in code; allow INI override

### Risk 3: Dynamic LOD Too Aggressive

**Mitigation**: Implement hysteresis threshold  
**Recommendation**: ±5 FPS threshold (e.g., only change if FPS < 25 when at Medium, > 35 to upgrade)

### Risk 4: Performance Regression

**Mitigation**: Benchmark before/after each stage  
**Target**: No more than ±10% FPS change during LOD transitions

---

## EA Implementation Statistics

| Metric | Value |
|--------|-------|
| Total LOD parameters | 23 |
| Static LOD levels | 3 (+ custom) |
| Dynamic LOD levels | 4 |
| Max bench profiles | 16 |
| Max presets per level | 32 |
| Profile error tolerance | 6% |
| CPU types supported | 4 (P3, P4, K7, XX) |
| GPU types supported | 17+ |
| Audio sample configs | 2 (2D, 3D) |
| Shadow types | 2 (volumes, decals) |

---

## Next Steps

1. **Phase 1 Planning**: Determine hardware detection strategy
2. **Phase 1 Start**: Implement GameLodManager with preset system
3. **Phase 2 Planning**: Dynamic LOD adjustment algorithm
4. **Phase 3 Planning**: Integration with existing systems
5. **Phase 4 Planning**: Performance optimization and profiling

---

## References

- **Header**: `references/generals_code/GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h`
- **Implementation**: `references/generals_code/GeneralsMD/Code/GameEngine/Source/Common/GameLOD.cpp` (708 lines)
- **Key Functions**:
  - `GameLODManager::findStaticLODLevel()` - Hardware→LOD detection
  - `GameLODManager::findDynamicLODLevel(fps)` - FPS→LOD adjustment
  - `GameLODManager::init()` - INI loading and initialization
  - Inline: `isParticleSkipped()`, `isDebrisSkipped()` - Skip logic
