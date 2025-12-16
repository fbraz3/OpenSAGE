# Particle Limiting Implementation Guide - Code Reference

Quick reference for implementing PLAN-010 based on EA Generals source verification.

## 1. Priority Enum

```csharp
/// <summary>
/// Particle priority levels from lowest to highest.
/// ALWAYS_RENDER particles bypass all culling/limiting.
/// </summary>
public enum ParticlePriority
{
    Invalid = 0,
    LowestPriority = 1,
    
    // Combat/Environmental
    WeaponExplosion = 1,        // Lowest priority
    Scorchmark = 2,
    DustTrail = 3,
    Buildup = 4,
    DebrisTrail = 5,
    UnitDamageFx = 6,
    DeathExplosion = 7,
    SemiConstant = 8,
    Constant = 9,
    WeaponTrail = 10,
    AreaEffect = 11,
    Critical = 12,
    AlwaysRender = 13,          // Highest priority - EXEMPT FROM ALL LIMITS
    
    HighestPriority = AlwaysRender,
    Count = 14
}
```

## 2. Dynamic LOD Level Config

```csharp
public class DynamicLODInfo
{
    /// <summary>FPS threshold to trigger this LOD level</summary>
    public int MinimumFPS { get; set; }
    
    /// <summary>Bitmask for particle skip (1-in-N creation pattern)</summary>
    public uint ParticleSkipMask { get; set; }
    
    /// <summary>Priority threshold below which particles are never created</summary>
    public ParticlePriority MinParticlePriority { get; set; }
    
    /// <summary>Priority threshold above which particles bypass skip mask</summary>
    public ParticlePriority MinParticleSkipPriority { get; set; }
}

// LOD level definitions
public enum DynamicLODLevel
{
    Unknown = -1,
    Low = 0,        // FPS < 15   (aggressive culling)
    Medium = 1,     // FPS 15-30  (moderate culling)
    High = 2,       // FPS 30-45  (light culling)
    VeryHigh = 3,   // FPS 45+    (no culling)
    Count = 4
}
```

## 3. Per-Priority Linked List Structure

```csharp
public class ParticleSystemManager
{
    // Per-priority particle lists (head and tail for FIFO)
    private Particle[] m_allParticlesHead = new Particle[(int)ParticlePriority.Count];
    private Particle[] m_allParticlesTail = new Particle[(int)ParticlePriority.Count];
    
    // Global counts
    private uint m_totalParticleCount = 0;
    private uint m_fieldParticleCount = 0;  // Ground-aligned AREA_EFFECT only
    
    // LOD state
    private DynamicLODLevel m_currentLODLevel = DynamicLODLevel.High;
    private uint m_particleGenerationCounter = 0;  // For skip mask
}
```

## 4. Particle Creation Gate

```csharp
public Particle CreateParticle(ParticleInfo info, ParticlePriority priority, 
                                bool forceCreate = false)
{
    if (!forceCreate)
    {
        // Gate 1: Global FX disable
        if (!GlobalData.UseFX)
            return null;

        // Gate 2: Priority threshold culling
        // Particles below this priority are NEVER created at this LOD
        if (priority < m_lodManager.GetMinParticlePriority())
            return null;

        // Gate 3: Skip mask throttling
        // Particles between thresholds use skip mask for 1-in-N creation
        if (priority < m_lodManager.GetMinParticleSkipPriority() &&
            IsParticleSkipped())
            return null;

        // Gate 4: Field particle cap
        // Ground-aligned AREA_EFFECT particles have separate limit
        if (priority == ParticlePriority.AreaEffect && 
            m_isGroundAligned &&
            m_fieldParticleCount > GlobalData.MaxFieldParticleCount)
            return null;

        // Gate 5: Hard count limit (unless ALWAYS_RENDER)
        if (priority != ParticlePriority.AlwaysRender)
        {
            int excess = (int)m_totalParticleCount - GlobalData.MaxParticleCount;
            if (excess > 0)
            {
                // Try to remove enough particles to make room
                int removed = RemoveOldestParticles((uint)excess, priority);
                if (removed < excess)
                    return null;  // Couldn't make room
            }

            if (GlobalData.MaxParticleCount == 0)
                return null;
        }
    }

    // Create particle
    var particle = new Particle(this, info);
    return particle;
}
```

## 5. Remove Oldest Particles Algorithm

```csharp
/// <summary>
/// Remove oldest N particles, starting from lowest priority.
/// Will not remove particles at or above priorityCap.
/// 
/// This ensures incoming particles at low priority can't cause
/// removal of high-priority particles.
/// </summary>
public int RemoveOldestParticles(uint count, ParticlePriority priorityCap)
{
    int removed = 0;
    
    for (int i = 0; i < count && m_totalParticleCount > 0; i++)
    {
        // Check each priority level from lowest to just below priorityCap
        for (int priority = (int)ParticlePriority.LowestPriority; 
             priority < (int)priorityCap; 
             priority++)
        {
            // Get oldest particle in this priority level
            Particle oldest = m_allParticlesHead[priority];
            
            if (oldest != null)
            {
                // Remove it
                oldest.DeleteInstance();
                removed++;
                break;  // Move to next particle to remove
            }
        }
    }
    
    return removed;
}
```

## 6. Add/Remove from Priority Lists

```csharp
public void AddParticleToLists(Particle particle, ParticlePriority priority)
{
    if (particle.InOverallList)
        return;
    
    // Add to head if list is empty, otherwise to tail
    if (m_allParticlesHead[(int)priority] == null)
    {
        m_allParticlesHead[(int)priority] = particle;
        particle.OverallPrev = null;
    }
    else
    {
        m_allParticlesTail[(int)priority].OverallNext = particle;
        particle.OverallPrev = m_allParticlesTail[(int)priority];
    }
    
    m_allParticlesTail[(int)priority] = particle;
    particle.OverallNext = null;
    particle.InOverallList = true;
    
    m_totalParticleCount++;
}

public void RemoveParticleFromLists(Particle particle)
{
    if (!particle.InOverallList)
        return;
    
    ParticlePriority priority = particle.Priority;
    
    // Unlink from neighbors
    if (particle.OverallNext != null)
        particle.OverallNext.OverallPrev = particle.OverallPrev;
    if (particle.OverallPrev != null)
        particle.OverallPrev.OverallNext = particle.OverallNext;
    
    // Update head/tail
    if (particle == m_allParticlesHead[(int)priority])
        m_allParticlesHead[(int)priority] = particle.OverallNext;
    if (particle == m_allParticlesTail[(int)priority])
        m_allParticlesTail[(int)priority] = particle.OverallPrev;
    
    particle.OverallNext = particle.OverallPrev = null;
    particle.InOverallList = false;
    
    m_totalParticleCount--;
}
```

## 7. LOD Skip Mask Logic

```csharp
/// <summary>
/// Implements frame-time throttling via skip mask.
/// For LOD_LOW with mask 0x7: skips 7 out of 8 particles (87.5%)
/// For LOD_MEDIUM with mask 0x3: skips 3 out of 4 particles (75%)
/// </summary>
private bool IsParticleSkipped()
{
    uint skipMask = m_lodManager.GetParticleSkipMask();
    
    // Increment counter and test against mask
    m_particleGenerationCounter++;
    
    // If (counter & mask) != mask, then skip this particle
    return (m_particleGenerationCounter & skipMask) != skipMask;
}
```

## 8. LOD Level Selection

```csharp
public void UpdateDynamicLOD(float averageFPS)
{
    // Find appropriate LOD level for current FPS
    DynamicLODLevel newLevel = DynamicLODLevel.VeryHigh;
    
    for (int i = (int)DynamicLODLevel.VeryHigh; i >= (int)DynamicLODLevel.Low; i--)
    {
        if (m_lodInfo[i].MinimumFPS < averageFPS)
        {
            newLevel = (DynamicLODLevel)i;
            break;
        }
    }
    
    // If LOD changed, apply new settings
    if (newLevel != m_currentLODLevel)
    {
        SetDynamicLODLevel(newLevel);
    }
}

private void SetDynamicLODLevel(DynamicLODLevel level)
{
    m_currentLODLevel = level;
    
    // Reset generation counters for skip mask
    m_particleGenerationCounter = 0;
    
    // Apply new LOD settings
    DynamicLODInfo info = m_lodInfo[(int)level];
    m_currentSkipMask = info.ParticleSkipMask;
    m_minParticlePriority = info.MinParticlePriority;
    m_minParticleSkipPriority = info.MinParticleSkipPriority;
}
```

## 9. Field Particle Tracking

```csharp
/// <summary>
/// AREA_EFFECT particles that are ground-aligned count toward the
/// separate field particle limit. This prevents accumulation of
/// permanent scorch marks and other ground decals from degrading performance.
/// </summary>
public void UpdateFieldParticleCount()
{
    m_fieldParticleCount = 0;
    
    for (int i = (int)ParticlePriority.LowestPriority; 
         i < (int)ParticlePriority.Count; i++)
    {
        Particle current = m_allParticlesHead[i];
        while (current != null)
        {
            // Count AREA_EFFECT ground-aligned particles separately
            if (current.System.Priority == ParticlePriority.AreaEffect &&
                current.System.IsGroundAligned)
            {
                m_fieldParticleCount++;
            }
            current = current.OverallNext;
        }
    }
}
```

## 10. Configuration (INI-style)

```
[GameLOD]
; Static LOD - maximum particles for each detail level
Low MaxParticleCount = 1500
Medium MaxParticleCount = 2500
High MaxParticleCount = 4000

Low MaxFieldParticleCount = 500
Medium MaxFieldParticleCount = 800
High MaxFieldParticleCount = 1200

; Dynamic LOD - FPS-based adjustments
```

```
[DynamicLOD_Low]
MinimumFPS = 15
ParticleSkipMask = 0x7         ; Skip 7 of 8 (87.5%)
MinParticlePriority = WeaponTrail
MinParticleSkipPriority = Critical

[DynamicLOD_Medium]
MinimumFPS = 30
ParticleSkipMask = 0x3         ; Skip 3 of 4 (75%)
MinParticlePriority = WeaponTrail
MinParticleSkipPriority = Critical

[DynamicLOD_High]
MinimumFPS = 45
ParticleSkipMask = 0x1         ; Skip 1 of 2 (50%)
MinParticlePriority = Critical
MinParticleSkipPriority = Critical

[DynamicLOD_VeryHigh]
MinimumFPS = 60
ParticleSkipMask = 0x0         ; No skipping
MinParticlePriority = LowestPriority
MinParticleSkipPriority = Critical
```

## 11. Priority Decision Matrix

| Current LOD | FPS Status | Skip Mask | Min Priority | Min Skip Priority | Effect |
|------------|-----------|-----------|--------------|-------------------|--------|
| Low        | < 15 FPS  | 0x7 (88%) | WeaponTrail  | Critical          | 88% particles skipped, only CRITICAL+ always render |
| Medium     | 15-30 FPS | 0x3 (75%) | WeaponTrail  | Critical          | 75% particles skipped, CRITICAL+ always render |
| High       | 30-45 FPS | 0x1 (50%) | Critical     | Critical          | 50% particles skipped, CRITICAL always render |
| VeryHigh   | 45+ FPS   | 0x0 (0%)  | Lowest       | Critical          | All particles render, CRITICAL+ always render |

## 12. Testing Scenarios

### Scenario A: At Hard Limit, Incoming Low Priority

```
State: 2400/2500 particles, WeaponExplosion incoming, LOD=Medium
Step 1: WeaponExplosion (1) < WeaponTrail (10)? YES → DENY
Result: Particle rejected
```

### Scenario B: At Hard Limit, Incoming High Priority

```
State: 2400/2500 particles, Critical incoming, LOD=Medium
Step 1: Critical (12) < WeaponTrail (10)? NO → PASS
Step 2: Critical (12) < Critical (12)? NO → PASS (no skip mask)
Step 3: 2400 >= 2500? NO → Create
Result: Particle created
```

### Scenario C: At Hard Limit, Incoming Medium Priority with Low Priority Particles

```
State: 2500/2500 particles (2000 WeaponExplosion, 500 WeaponTrail), WeaponTrail incoming
Step 1: WeaponTrail (10) >= WeaponTrail (10)? YES → PASS
Step 2: WeaponTrail (10) < Critical (12)? YES → Check skip mask
         isParticleSkipped() = NO → CREATE
Step 3: 2500 >= 2500? YES → removeOldestParticles(1, WeaponTrail)
         Loop priority 1-9, find oldest WeaponExplosion, delete it
Step 4: Create WeaponTrail
Result: 1 WeaponExplosion removed, 1 WeaponTrail added (net 0 change)
```

### Scenario D: ALWAYS_RENDER Bypass

```
State: 2500/2500 particles, AlwaysRender incoming
Step 1: AlwaysRender (13) >= WeaponTrail (10)? YES → PASS
Step 2: AlwaysRender (13) < Critical (12)? NO → PASS
Step 3: priority != ALWAYS_RENDER? NO → SKIP count check
Step 4: Create AlwaysRender
Result: 2501/2500 - particle created above limit!
```

## 13. Tuning Guide

### For Very Low End Hardware (< 30 FPS)

```csharp
LOD_Low.skipMask = 0xF;  // Skip 15/16 (93.75%)
LOD_Low.minPriority = CRITICAL;  // Only CRITICAL+ render
maxParticleCount_Static = 1000;
maxFieldParticleCount = 200;
```

### For Mid-Range Hardware (30-60 FPS)

```csharp
LOD_Medium.skipMask = 0x3;  // Skip 3/4 (75%)
LOD_Medium.minPriority = WEAPON_TRAIL;
maxParticleCount_Static = 2500;
maxFieldParticleCount = 800;
```

### For High End Hardware (60+ FPS)

```csharp
LOD_High.skipMask = 0x0;  // No skipping
LOD_High.minPriority = LOWEST_PRIORITY;
maxParticleCount_Static = 5000;
maxFieldParticleCount = 1500;
```

---

**Reference**: See [EA_PARTICLE_LIMITING_ANALYSIS.md](EA_PARTICLE_LIMITING_ANALYSIS.md) for complete implementation details with source code citations.

