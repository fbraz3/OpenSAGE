# EA Generals Particle Limiting & Management Analysis

## Overview
This document details the comprehensive particle limiting, culling, and priority-based management system used in Command & Conquer Generals, based on direct source code analysis.

## Key Finding
EA Generals implements a **multi-layered, priority-based particle management system** that combines:
1. Hard particle count limits
2. Priority-based culling tied to LOD system
3. LOD-driven skip masks for frame-time throttling
4. Field particle specialized caps
5. ALWAYS_RENDER exemption for critical particles

---

## 1. Particle Priority System (13 Levels)

### Priority Hierarchy (Lowest to Highest)
```cpp
enum ParticlePriorityType
{
    INVALID_PRIORITY = 0,
    PARTICLE_PRIORITY_LOWEST = 1,
    
    // Ordered list:
    WEAPON_EXPLOSION = 1,           // Level 1: Low priority
    SCORCHMARK = 2,                 // Level 2
    DUST_TRAIL = 3,                 // Level 3
    BUILDUP = 4,                    // Level 4
    DEBRIS_TRAIL = 5,               // Level 5
    UNIT_DAMAGE_FX = 6,             // Level 6
    DEATH_EXPLOSION = 7,            // Level 7
    SEMI_CONSTANT = 8,              // Level 8
    CONSTANT = 9,                   // Level 9
    WEAPON_TRAIL = 10,              // Level 10
    AREA_EFFECT = 11,               // Level 11
    CRITICAL = 12,                  // Level 12
    ALWAYS_RENDER = 13,             // Level 13: Highest - EXEMPT FROM ALL LIMITS
    
    NUM_PARTICLE_PRIORITIES = 14
};
```

### Critical Notes on Priority System:
- **ALWAYS_RENDER** particles are **EXEMPT from ALL particle count limits and LOD culling**
- Used for logically important display (not fluff)
- Must never be culled regardless of particle cap or LOD
- Hardcoded exemption in `createParticle()` at line 1812-1824

---

## 2. Hard Particle Count Limits

### Global Settings (from GlobalData)
```cpp
Int m_maxParticleCount;          // Maximum total particles in world
Int m_maxFieldParticleCount;     // Maximum field-type particles (ground-aligned AREA_EFFECT)
```

### Static LOD Configuration
```cpp
// Default from StaticGameLODInfo constructor:
m_maxParticleCount = 2500;  // Default max particles across all static LOD levels
```

### Dynamic Configuration
- Set per static LOD level (LOW, MEDIUM, HIGH, CUSTOM)
- User-adjustable via Options menu slider
- Persisted in user preferences
- Can be 0 to disable all particles

---

## 3. Particle Creation Gate Logic

### Flowchart (ParticleSystem::createParticle() - lines 1793-1824)

```
IF forceCreate == FALSE (normal creation):
    ├─ IF useFX is disabled globally → DENY
    ├─ Priority Check (LOD-based):
    │  ├─ IF priority < getMinDynamicParticlePriority() → DENY
    │  └─ IF priority < getMinDynamicParticleSkipPriority() AND isParticleSkipped() → DENY
    ├─ Field Particle Check:
    │  └─ IF (AREA_EFFECT AND ground-aligned AND field count > maxFieldParticleCount) → DENY
    └─ Count Limit Check (unless ALWAYS_RENDER):
       ├─ IF current count > maxParticleCount:
       │  └─ Remove oldest particles at lower priorities until excess is gone
       │     └─ IF cannot remove enough → DENY
       └─ IF maxParticleCount == 0 → DENY
```

### Exact Code Implementation
```cpp
Particle *ParticleSystem::createParticle( const ParticleInfo *info, 
                                          ParticlePriorityType priority,
                                          Bool forceCreate )
{
    if( forceCreate == FALSE )
    {
        if (TheGlobalData->m_useFX == FALSE)
            return NULL;

        // Check if particle is below priorities we allow for this FPS or if being skipped
        if( priority < TheGameLODManager->getMinDynamicParticlePriority() ||
                (priority < TheGameLODManager->getMinDynamicParticleSkipPriority() && 
                 TheGameLODManager->isParticleSkipped()) )
            return NULL;

        // Field particle specific cap
        if ( getParticleCount() > 0 && priority == AREA_EFFECT && 
             m_isGroundAligned && 
             TheParticleSystemManager->getFieldParticleCount() > 
             (UnsignedInt)TheGlobalData->m_maxFieldParticleCount )
            return NULL;
        
        // ALWAYS_RENDER particles are exempt from all count limits
        if (priority != ALWAYS_RENDER)
        {
            int numInExcess = TheParticleSystemManager->getParticleCount() - 
                             (UnsignedInt)TheGlobalData->m_maxParticleCount;
            if ( numInExcess > 0)
            {
                if( TheParticleSystemManager->removeOldestParticles(
                        (UnsignedInt) numInExcess, priority) != numInExcess )
                    return NULL;  // could not remove enough particles
            }

            if (TheGlobalData->m_maxParticleCount == 0)
                return NULL;
        }
    }

    Particle *p = newInstance(Particle)( this, info );
    return p;
}
```

---

## 4. Oldest-Particle-First Removal Algorithm

### Implementation (removeOldestParticles - lines 3298-3318)

**Algorithm**: Remove from lowest priority to higher priority until target count is reached.

```cpp
Int ParticleSystemManager::removeOldestParticles( UnsignedInt count, 
                                                   ParticlePriorityType priorityCap )
{
    Int countToRemove = count;

    while (count-- && getParticleCount()) 
    {
        for( Int i = PARTICLE_PRIORITY_LOWEST;    // Start at priority level 1
             i < priorityCap;                      // Stop before priorityCap
             ++i )
        {
            if( m_allParticlesHead[ i ] )   // Get oldest particle in this priority level
            {
                m_allParticlesHead[ i ]->deleteInstance();  // Delete it
                break;  // exit for - move to next particle to remove
            }
        }
    }

    // return the number of particles actually removed
    return countToRemove - count;
}
```

### Key Characteristics:
- **Per-priority doubly-linked lists**: `m_allParticlesHead[priority]` and `m_allParticlesTail[priority]`
- **FIFO within priority**: Always removes head (oldest) of each priority level
- **Respects priorityCap**: Won't remove particles at or above the new particle's priority
  - This means high-priority particles protect lower-priority particles from being culled
- **Stops early if target met**: Doesn't remove more than necessary

---

## 5. Dynamic LOD Particle Skipping System

### LOD Manager Skip Logic (GameLOD.h, lines ~200-240)

```cpp
Bool GameLODManager::isParticleSkipped(void)
{
    return (++m_numParticleGenerations & m_dynamicParticleSkipMask) 
           != m_dynamicParticleSkipMask;
}
```

**How it works**:
- Maintains a counter `m_numParticleGenerations` (incremented per creation attempt)
- Bitwise AND against a skip mask determines if particle is skipped
- Skip mask is set per LOD level (0 = no skipping, any power of 2 = skip pattern)

### Skip Mask Examples:
```
m_dynamicParticleSkipMask = 0x0:  No skipping (all particles created)
m_dynamicParticleSkipMask = 0x1:  Skip every particle (50% skip rate)
m_dynamicParticleSkipMask = 0x3:  Skip 3 out of 4 particles (75% skip rate)
m_dynamicParticleSkipMask = 0x7:  Skip 7 out of 8 particles (87.5% skip rate)
```

### Dynamic LOD Levels (4 levels)

```cpp
enum DynamicGameLODLevel
{
    DYNAMIC_GAME_LOD_UNKNOWN = -1,
    DYNAMIC_GAME_LOD_LOW = 0,         // Lowest FPS - aggressive skipping
    DYNAMIC_GAME_LOD_MEDIUM = 1,
    DYNAMIC_GAME_LOD_HIGH = 2,
    DYNAMIC_GAME_LOD_VERY_HIGH = 3,   // Highest FPS - no skipping
    DYNAMIC_GAME_LOD_COUNT = 4
};
```

### LOD Level Selection Logic (line 654-668 in GameLOD.cpp)

```cpp
DynamicGameLODLevel GameLODManager::findDynamicLODLevel(Real averageFPS)
{
    Int ifps = (Int)(averageFPS);  // convert to integer
    
    for (Int i = DYNAMIC_GAME_LOD_VERY_HIGH; i >= DYNAMIC_GAME_LOD_LOW; i--)
    {
        if (m_dynamicGameLODInfo[i].m_minFPS < ifps)
            return (DynamicGameLODLevel)i;
    }
    return DYNAMIC_GAME_LOD_LOW;
}
```

**FPS-Based Transition**: Each LOD level has `m_minFPS` threshold
- If average FPS drops below threshold → shift to lower LOD (more aggressive skipping)
- If FPS recovers above threshold → shift to higher LOD (less skipping)

---

## 6. Two-Level Priority Thresholds

### DynamicGameLODInfo Structure

```cpp
struct DynamicGameLODInfo
{
    Int m_minFPS;  // FPS threshold for this LOD level
    
    // Skipping configuration
    UnsignedInt m_dynamicParticleSkipMask;
    
    // Two-level priority gating:
    ParticlePriorityType m_minDynamicParticlePriority;
    ParticlePriorityType m_minDynamicParticleSkipPriority;
};
```

### The Two Thresholds

1. **m_minDynamicParticlePriority**
   - Particles below this priority are **ALWAYS DENIED** at this LOD
   - Hard cap - no particles created
   - Example: At LOW LOD, might be WEAPON_TRAIL → denies WEAPON_EXPLOSION through AREA_EFFECT

2. **m_minDynamicParticleSkipPriority**
   - Particles below this priority use skip mask (1 in N created)
   - Particles at or above are always created (if they pass other checks)
   - "Skip immune" threshold
   - Example: CRITICAL and ALWAYS_RENDER always created at any LOD

### Decision Tree for Creation

```
Priority → Check 1: Is priority >= m_minDynamicParticlePriority?
    NO  → DENY (hard culling)
    YES → Check 2: Is priority < m_minDynamicParticleSkipPriority?
            YES → Apply skip mask (maybe skip, maybe create)
            NO  → CREATE (if count limit allows)
```

---

## 7. Field Particle Specialized Culling

### What are Field Particles?

Ground-aligned AREA_EFFECT particles used for:
- Scorch marks
- Ground decals
- Dust clouds at ground level
- Other terrain-aligned FX

### Separate Cap

```cpp
// Specific check before general count check:
if ( getParticleCount() > 0 && 
     priority == AREA_EFFECT && 
     m_isGroundAligned && 
     TheParticleSystemManager->getFieldParticleCount() > 
     (UnsignedInt)TheGlobalData->m_maxFieldParticleCount )
    return NULL;  // DENY field particle
```

### Purpose
Prevents accumulation of "infinite" ground particles (scorch marks, burn marks) that persist long-term and degrade performance over time without direct player interaction.

---

## 8. Integration Points in Engine

### 1. Particle Manager Update Loop
- **File**: `src/OpenSage.Game/Game.cs` (logical equivalent in Generals)
- Each frame: Update all particle systems
- Call `ParticleSystemManager::update()` which updates all systems

### 2. Game LOD Manager
- **File**: `GameLOD.cpp` / `GameLOD.h`
- Monitors average FPS
- Adjusts dynamic LOD level
- Sets skip masks and priority thresholds

### 3. Global Data Configuration
- **Static Settings**: Per LOD level (LOW/MEDIUM/HIGH/CUSTOM)
- **Dynamic Settings**: User options menu adjusts m_maxParticleCount slider
- Persisted in user preferences

### 4. Rendering Pipeline
- **File**: `W3DParticleSys.cpp` (rendering device layer)
- Frustum culling: Particles outside camera view are culled
- On-screen count tracked but doesn't affect creation

---

## 9. Thresholds & Defaults

### Default Static LOD Particle Limits
```cpp
// From StaticGameLODInfo::StaticGameLODInfo()
m_maxParticleCount = 2500;  // Across all LOD levels
```

### Default Dynamic LOD Configuration
```cpp
// From DynamicGameLODInfo::DynamicGameLODInfo()
m_minFPS = 0;
m_dynamicParticleSkipMask = 0;              // No skipping by default
m_minDynamicParticlePriority = PARTICLE_PRIORITY_LOWEST;  // All priorities OK
m_minDynamicParticleSkipPriority = PARTICLE_PRIORITY_LOWEST;  // All subject to skip mask
```

### Actual INI-Configurable Values
Parsed from INI files with these keys:
```
[Dynamic LOD Level]
MinimumFPS = <int>
ParticleSkipMask = <hex bitmask>
DebrisSkipMask = <hex bitmask>
SlowDeathScale = <float>
MinParticlePriority = <priority name>
MinParticleSkipPriority = <priority name>
```

---

## 10. Culling Mechanisms Summary

| Mechanism | Type | Applicability | Effect |
|-----------|------|---------------|--------|
| **Hard Count Limit** | Hard cap | All particles except ALWAYS_RENDER | Removes oldest low-priority particles when limit exceeded |
| **Priority Threshold** | Hard gate | Below m_minDynamicParticlePriority | Particles never created at this LOD |
| **Skip Mask** | Frame-throttling | Below m_minDynamicParticleSkipPriority | 1-in-N creation probability |
| **Field Particle Cap** | Specialized | AREA_EFFECT ground-aligned only | Separate limit for terrain-accumulated particles |
| **Frustum Culling** | Rendering-time | All particles | Particles outside view frustum not rendered (creation still allowed) |
| **ALWAYS_RENDER Exemption** | Priority-based | ALWAYS_RENDER priority only | Bypasses all creation-time limits |

---

## 11. Example Scenario: Creating a Weapon Explosion

### Initial State
```
Current particles: 2,400 / 2,500
Dynamic LOD: MEDIUM
  - m_minDynamicParticlePriority: WEAPON_TRAIL
  - m_dynamicParticleSkipMask: 0x3 (75% skip)
  - m_minDynamicParticleSkipPriority: WEAPON_TRAIL
```

### Attempting to Create WEAPON_EXPLOSION (priority=1)

**Step 1**: Check priority vs m_minDynamicParticlePriority
```
WEAPON_EXPLOSION (1) < WEAPON_TRAIL (10)?
YES → DENY. Particle not created.
```

Result: Particle rejected before even checking count limits.

### Attempting to Create WEAPON_TRAIL (priority=10)

**Step 1**: Check hard priority
```
WEAPON_TRAIL (10) >= WEAPON_TRAIL (10)?
YES → Proceed.
```

**Step 2**: Check skip mask
```
WEAPON_TRAIL (10) < WEAPON_TRAIL (10)?
NO → Skip mask NOT applied, particle will be created (count permitting)
```

**Step 3**: Check count
```
2,400 < 2,500?
YES → Create particle.
```

**Step 4**: Add to global particle list
```
m_allParticlesTail[WEAPON_TRAIL] = new particle
m_particleCount = 2,401
```

### Attempting to Create CRITICAL (priority=12) when at limit

**Step 1-2**: Pass priority gates

**Step 3**: Check count
```
2,401 >= 2,500?
YES → numInExcess = 1
```

**Step 4**: Remove oldest particles
```
for i = WEAPON_EXPLOSION (1) to CRITICAL (12):
    if m_allParticlesHead[1]:
        delete m_allParticlesHead[1]
        break
```
Removes one WEAPON_EXPLOSION particle.

**Step 5**: Check priority != ALWAYS_RENDER
```
CRITICAL != ALWAYS_RENDER?
YES → normal creation (not exempt)
```

**Step 6**: Create critical particle
```
m_particleCount = 2,401  // Still at limit but oldest removed
```

---

## 12. Performance Characteristics

### Time Complexity
- **Particle creation**: O(1) average for allocation
- **Remove oldest**: O(N) in worst case where N = priorityCap level depth
  - Typically O(1) or O(2) since lowest priority lists are checked first
- **Skip mask check**: O(1) - single bitwise AND

### Space Complexity
- Per-priority linked lists: O(P) where P = NUM_PARTICLE_PRIORITIES (13)
- Each particle: Standard node data + particle info

### Behavioral Characteristics
- **Particle lifetime**: Independent of priority (priority only affects creation)
- **No age-based culling**: Only count-based (oldest removed when limit hit)
- **No distance-based culling**: Only frustum culling at render time
- **Deterministic**: Same particle sequence generated for same game state

---

## 13. Key Algorithm Insights for PLAN-010

### What EA Got Right
1. **Multi-layered approach**: Combines hard limits, priority gates, and frame-throttling
2. **Priority system**: 13 levels provide fine-grained control
3. **Exemption mechanism**: ALWAYS_RENDER allows critical FX to always render
4. **Separate field cap**: Recognizes that ground particles accumulate differently
5. **FPS-based adaptation**: Dynamic LOD adjusts to maintain target framerate
6. **Per-priority FIFO**: Fair removal across different particle types

### What to Implement in OpenSAGE
1. **Priority enum** with all 13 levels
2. **ParticleSystemManager** with per-priority doubly-linked lists
3. **removeOldestParticles()** with priority-aware algorithm
4. **LOD integration** - connect to frame-time monitoring
5. **Skip mask logic** - bitwise AND for 1-in-N particle creation
6. **Field particle tracking** - count AREA_EFFECT ground-aligned separately
7. **ALWAYS_RENDER bypass** - exception path in createParticle()

### Recommended Thresholds for OpenSAGE
```cpp
// Static LOD defaults
STATIC_LOD_LOW:    maxParticleCount = 1500
STATIC_LOD_MEDIUM: maxParticleCount = 2500
STATIC_LOD_HIGH:   maxParticleCount = 4000
STATIC_LOD_CUSTOM: maxParticleCount = 3000 (user adjustable)

// Dynamic LOD defaults
DYNAMIC_LOD_LOW:       minFPS=15, skipMask=0x7 (87.5% skip)
DYNAMIC_LOD_MEDIUM:    minFPS=30, skipMask=0x3 (75% skip)
DYNAMIC_LOD_HIGH:      minFPS=45, skipMask=0x1 (50% skip)
DYNAMIC_LOD_VERY_HIGH: minFPS=60, skipMask=0x0 (no skip)

// Priority thresholds
LOW:    minDynamicParticlePriority = WEAPON_TRAIL (10)
MEDIUM: minDynamicParticlePriority = WEAPON_TRAIL (10)
HIGH:   minDynamicParticlePriority = CRITICAL (12)

All LODs: minDynamicParticleSkipPriority = CRITICAL (12)
```

---

## 14. Code References

### Files Analyzed
- `Generals/Code/GameEngine/Include/GameClient/ParticleSys.h` - Structs and enums
- `Generals/Code/GameEngine/Source/GameClient/System/ParticleSys.cpp` - Core logic
- `Generals/Code/GameEngine/Include/Common/GameLOD.h` - LOD definitions
- `Generals/Code/GameEngine/Source/Common/GameLOD.cpp` - LOD implementation
- `Generals/Code/GameEngine/Include/Common/GlobalData.h` - Configuration
- `Generals/Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DParticleSys.cpp` - Rendering integration

### Key Functions
- `ParticleSystem::createParticle()` - Line 1793-1824
- `ParticleSystemManager::removeOldestParticles()` - Line 3298-3318
- `ParticleSystemManager::addParticle()` - Line 3215-3240
- `GameLODManager::findDynamicLODLevel()` - Line 654-668
- `GameLODManager::isParticleSkipped()` - Line 205 (inline)

---

## Conclusion

EA's particle management system in Generals is a well-engineered, multi-tiered approach that prioritizes critical FX while gracefully degrading non-essential particles under performance pressure. The combination of hard counts, priority-based culling, LOD-based skip masks, and FPS-adaptive behavior makes it suitable as a reference implementation for PLAN-010.

The system is:
- **Predictable**: Deterministic culling based on priority
- **Fair**: Round-robin FIFO removal within priority levels
- **Efficient**: O(1) typical case, O(P) worst case where P << N
- **Configurable**: All thresholds are INI-settable
- **Flexible**: Extensible to distance-based or other criteria if needed

