# PLAN-014: Implementation Strategy & Current State

**Date**: December 2025  
**Status**: Research Phase Complete - Ready for Stage 1  
**Priority**: High  
**Scope**: LOD System Infrastructure (Hardware Detection → Dynamic FPS Adjustment)

---

## Current OpenSAGE State

### Existing LOD Data Structures ✅

OpenSAGE already has all data model classes in `src/OpenSage.Game/Lod/`:

1. **StaticGameLod.cs** ✅
   - 38 properties (MinimumFps, MaxParticleCount, TextureReductionFactor, etc.)
   - Full INI parsing support
   - All EA parameters implemented
   - Supports multiple games (Generals, BFME, BFME2)

2. **DynamicGameLod.cs** ✅
   - 6 properties (MinimumFps, ParticleSkipMask, DebrisSkipMask, SlowDeathScale, etc.)
   - Full INI parsing support
   - Exact EA implementation

3. **BenchProfile.cs** ✅
   - CPU type, MHz, benchmark indices (Unknown1-3)
   - Full INI parsing support

4. **LodPreset.cs** ✅
   - Level, CPU type, MHz, GPU type, GPU memory
   - Extended support for BFME+ (resolution, etc.)
   - Full INI parsing support

### Missing: GameLodManager Implementation ❌

**No `GameLodManager` class exists yet!** This is the core orchestrator that:
- Manages current LOD state
- Performs hardware detection
- Implements preset matching algorithm
- Handles static LOD application
- Implements dynamic FPS-based LOD adjustment
- Provides skip mask logic for particles/debris

---

## EA Reference Implementation Summary

### Key Algorithm: Preset Matching

**Source**: `GameLOD.cpp::findStaticLODLevel()` (lines 483+)

```cpp
// 1. Get hardware info
testMinimumRequirements(&videoChipType, &cpuType, &cpuFreq, &numRAM, 
                        &intBenchIndex, &floatBenchIndex, &memBenchIndex);

// 2. Find matching BenchProfile
for (Int k=0; k<m_numBenchProfiles; k++) {
    if (intBenchIndex/prof->m_intBenchIndex >= PROFILE_ERROR_LIMIT &&
        floatBenchIndex/prof->m_floatBenchIndex >= PROFILE_ERROR_LIMIT &&
        memBenchIndex/prof->m_memBenchIndex >= PROFILE_ERROR_LIMIT) {
        
        // Profile matches! Find highest LOD level
        for (Int i=STATIC_GAME_LOD_HIGH; i >= STATIC_GAME_LOD_LOW; i--) {
            // Check if preset matches this LOD level
        }
    }
}

// 3. Return highest matching LOD level
return m_idealDetailLevel;
```

### Key Algorithm: Skip Mask Logic

**Source**: `GameLOD.h` inline functions

```cpp
Bool GameLODManager::isParticleSkipped(void) {
    return (++m_numParticleGenerations & m_dynamicParticleSkipMask) != m_dynamicParticleSkipMask;
}

Bool GameLODManager::isDebrisSkipped(void) {
    return (++m_numDebrisGenerations & m_dynamicDebrisSkipMask) != m_dynamicDebrisSkipMask;
}
```

**Why This Works**:
- `0x000000FF` skip mask = render 1 of 256 particles
- `0x00000003` skip mask = render 1 of 4 particles
- `0x00000000` skip mask = render all particles
- Counter wraps automatically, no division needed

### Critical Constants

```cpp
#define MAX_LOD_PRESETS_PER_LEVEL 32    // Max presets per LOD level
#define MAX_BENCH_PROFILES 16           // Max CPU benchmark profiles
#define PROFILE_ERROR_LIMIT 0.94f       // 6% tolerance for matching
```

---

## PLAN-014 Stage 1: Implementation Plan

### Objective
Create `GameLodManager` class that:
1. Loads LOD definitions from INI files
2. Performs hardware detection
3. Matches hardware to preset LOD level
4. Provides query methods for current LOD state

### Deliverables

#### File 1: `src/OpenSage.Game/Lod/GameLodManager.cs` (NEW)

**Core Class**:
```csharp
public class GameLodManager
{
    // Current LOD state
    private LodType _currentStaticLod = LodType.Medium;
    private LodType _currentDynamicLod = LodType.VeryHigh;
    
    // Data loaded from INI
    private Dictionary<LodType, StaticGameLod> _staticLods;
    private Dictionary<LodType, DynamicGameLod> _dynamicLods;
    private List<LodPreset> _presets;
    private List<BenchProfile> _benchProfiles;
    
    // Hardware info
    private CpuType _detectedCpuType;
    private int _detectedCpuFreq;
    private GpuType _detectedGpuType;
    private int _detectedGpuMemoryMb;
    private int _detectedRamMb;
    
    // Dynamic LOD particle skipping
    private int _particleSkipCounter;
    private int _debrisSkipCounter;
    
    // Public methods
    public void Initialize(GameData gameData);
    public LodType FindStaticLodLevel();
    public void SetStaticLodLevel(LodType level);
    public LodType GetStaticLodLevel() => _currentStaticLod;
    
    public LodType FindDynamicLodLevel(float averageFps);
    public void SetDynamicLodLevel(LodType level);
    public LodType GetDynamicLodLevel() => _currentDynamicLod;
    
    public bool IsParticleSkipped() => (++_particleSkipCounter & /* mask */) != /* mask */;
    public bool IsDebrisSkipped() => (++_debrisSkipCounter & /* mask */) != /* mask */;
    
    public HardwareInfo GetDetectedHardware();
}
```

**Key Methods**:

1. **Initialize(gameData)**
   - Load StaticGameLod entries from gameData
   - Load DynamicGameLod entries from gameData
   - Load LodPreset entries from gameData
   - Load BenchProfile entries from gameData
   - Detect current hardware

2. **FindStaticLodLevel()**
   - Detect hardware (CPU, GPU, RAM)
   - Match to BenchProfile with tolerance (6%)
   - Find highest matching LodPreset
   - Return corresponding LOD level

3. **FindDynamicLodLevel(fps)**
   - Simple threshold matching:
     - FPS > threshold[VeryHigh] → VeryHigh
     - FPS > threshold[High] → High
     - FPS > threshold[Medium] → Medium
     - Else → Low

4. **IsParticleSkipped() / IsDebrisSkipped()**
   - Counter-based bitmask logic (exact EA algorithm)

#### File 2: `src/OpenSage.Game/Lod/HardwareInfo.cs` (NEW)

```csharp
public class HardwareInfo
{
    public CpuType CpuType { get; set; }
    public int CpuFrequencyMhz { get; set; }
    public GpuType GpuType { get; set; }
    public int GpuMemoryMb { get; set; }
    public int SystemRamMb { get; set; }
}
```

#### File 3: Unit Tests (`src/OpenSage.Game.Tests/Lod/GameLodManagerTests.cs`)

**Test Cases**:
- Hardware detection returns valid info
- Preset matching respects 6% tolerance
- Dynamic LOD thresholds work correctly
- Particle skip masks generate correct pattern
- LOD state transitions properly
- Setting LOD applies to current state

### Integration Points

1. **Game.cs**: Initialize GameLodManager on startup
   ```csharp
   _lodManager = new GameLodManager();
   _lodManager.Initialize(gameData);
   _lodManager.FindStaticLodLevel();
   ```

2. **Game Loop**: Update dynamic LOD every ~1 second
   ```csharp
   if (timeSinceLastLodCheck > 1.0f) {
       var newDynamicLod = _lodManager.FindDynamicLodLevel(currentFps);
       if (newDynamicLod != _lodManager.GetDynamicLodLevel()) {
           _lodManager.SetDynamicLodLevel(newDynamicLod);
       }
       timeSinceLastLodCheck = 0;
   }
   ```

3. **Particle System**: Skip particles based on LOD
   ```csharp
   if (_lodManager.IsParticleSkipped()) {
       return;  // Don't create this particle
   }
   ```

---

## EA Reference Architecture Pattern

### Static LOD Application (from GameLOD.cpp::applyStaticLODLevel)

```cpp
void GameLODManager::applyStaticLODLevel(StaticGameLODLevel level) {
    StaticGameLODInfo *lodInfo = &m_staticGameLODInfo[level];
    
    // Update all systems
    TheGlobalData->m_textureReductionFactor = lodInfo->m_textureReduction;
    TheGlobalData->m_maxParticleCount = lodInfo->m_maxParticleCount;
    TheGlobalData->m_useShadowVolumes = lodInfo->m_useShadowVolumes;
    TheGlobalData->m_useShadowDecals = lodInfo->m_useShadowDecals;
    // ... more updates ...
    
    // Notify systems of changes
    TheGameClient->setStaticLodLevel();
    TheTerrainVisual->setStaticLodLevel();
}
```

**OpenSAGE Pattern**: Instead of global data, emit events or use DI:
```csharp
public event Action<StaticGameLod> StaticLodChanged;
public event Action<DynamicGameLod> DynamicLodChanged;
```

---

## INI Files Status

### GameLOD.ini Verification

Need to verify these INI blocks exist:
```ini
StaticGameLod Low
    MinimumFPS = 30
    MaxParticleCount = 100
    // ... etc
EndStaticGameLod

StaticGameLod Medium
    MinimumFPS = 50
    MaxParticleCount = 500
    // ... etc
EndStaticGameLod

StaticGameLod High
    MinimumFPS = 60
    MaxParticleCount = 2500
    // ... etc
EndStaticGameLod

DynamicGameLod Low
    MinimumFPS = 20
    ParticleSkipMask = 0x000000FF
    // ... etc
EndDynamicGameLod

// ... more dynamic LOD entries
```

### GameLODPresets.ini Verification

Need to verify these preset blocks:
```ini
LODPreset Low P3 700 GF2 32
LODPreset Low P4 1500 TNT2 32
LODPreset Medium P3 1000 GF3 64
LODPreset High P4 2000 GF3 128
// etc
```

---

## Risk Assessment

### Risk 1: INI Files Don't Exist ❌ CONFIRMED
- **Status**: No GameLOD.ini or GameLODPresets.ini found in workspace
- **Mitigation**: Create minimal INI files or use embedded defaults
- **Decision**: Embed reasonable defaults in code; allow INI override if available

### Risk 2: Hardware Detection Not Available ⚠️ UNKNOWN
- **Status**: Need to check if hardware detection exists in OpenSage.Graphics
- **Mitigation**: Implement basic detection (CPU cores, RAM) using System.Environment
- **Decision**: Phase 1 won't include advanced benchmarking; Phase 2 will add it

### Risk 3: GameData Integration ⚠️ UNKNOWN
- **Status**: Need to check how GameData loads LOD entries
- **Mitigation**: Check existing patterns (how other game data is loaded)
- **Decision**: Follow established OpenSAGE patterns for INI loading

### Risk 4: Global State vs Dependency Injection
- **Status**: EA uses global `TheGameLODManager` pointer
- **OpenSAGE**: Uses more modern DI patterns
- **Decision**: Inject GameLodManager into systems that need it

---

## Success Criteria for Stage 1

✅ GameLodManager class created with all core methods  
✅ Hardware detection working (basic CPU/GPU/RAM)  
✅ Preset matching algorithm implemented with 6% tolerance  
✅ Dynamic LOD threshold logic implemented  
✅ Particle/debris skip masks working  
✅ 12+ unit tests all passing  
✅ No new build errors or warnings  
✅ Clean integration with existing LOD data structures  

---

## Performance Targets

- Hardware detection: < 100ms (once at startup)
- Preset matching: < 50ms (once at startup)
- IsParticleSkipped() call: inline, ~1 CPU cycle
- IsDebrisSkipped() call: inline, ~1 CPU cycle
- Dynamic LOD decision: < 1ms (once per second)
- Memory overhead: < 1MB for LOD system

---

## Next Steps

1. **Verify INI Files**: Check if GameLOD.ini exists in game data
2. **Create GameLodManager**: Implement core class with all methods
3. **Add Hardware Detection**: Basic detection without benchmarking
4. **Create Unit Tests**: Comprehensive test coverage
5. **Build & Verify**: Clean build, all tests passing
6. **Commit**: Conventional commit with Stage 1 work

---

## References

- EA Header: `references/generals_code/GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h`
- EA Implementation: `references/generals_code/GeneralsMD/Code/GameEngine/Source/Common/GameLOD.cpp` (708 lines)
- OpenSAGE LOD Data: `src/OpenSage.Game/Lod/StaticGameLod.cs`, `DynamicGameLod.cs`, `BenchProfile.cs`, `LodPreset.cs`
- Research Document: `PLAN-014_EA_SOURCE_ANALYSIS.md`
