# PLAN-014: Asset Streaming - Implementation Plan

**Phase**: Phase 4 (Optimization)  
**Priority**: High (improves performance and memory usage)  
**Estimated Duration**: 3-4 days  
**Acceptance Criteria**: ✅ Complete  

---

## Overview

Implement a comprehensive LOD-based asset streaming system that dynamically adjusts game quality based on hardware capabilities and runtime FPS. This involves:

1. Enhancing the existing StaticGameLod/DynamicGameLod system
2. Implementing hardware detection for initial LOD selection
3. Adding dynamic FPS-based LOD adjustment
4. Creating texture resolution scaling
5. Implementing asset preloading framework

---

## Architecture

### Static LOD System (Hardware-Based)

```csharp
public enum StaticGameLodLevel
{
    VeryLow = 0,
    Low = 1,
    Medium = 2,
    High = 3,
    Ultra = 4
}

public class StaticGameLodInfo
{
    public int TextureReductionFactor { get; set; }     // 0-4: divide by 2^n
    public int MaxParticleCount { get; set; }           // 100-5000+
    public bool UseShadowVolumes { get; set; }          // Volumetric shadows
    public bool UseShadowDecals { get; set; }           // 2D decal shadows
    public bool UseCloudMap { get; set; }
    public bool UseLightMap { get; set; }
    public bool ShowSoftWaterEdge { get; set; }
    public bool UseTreeSway { get; set; }
    public bool UseTrees { get; set; }
    public int MaxTankTrackEdges { get; set; }
    public int MaxTankTrackOpaqueEdges { get; set; }
    public int MaxTankTrackFadeDelay { get; set; }
}
```

### Dynamic LOD System (FPS-Based)

```csharp
public enum DynamicGameLodLevel
{
    Low = 0,
    Medium = 1,
    High = 2,
    VeryHigh = 3
}

public class DynamicGameLodInfo
{
    public uint ParticleSkipMask { get; set; }          // Skip every Nth particle
    public uint DebrisSkipMask { get; set; }
    public float SlowDeathScale { get; set; }           // Death animation speedup
    public int MinDynamicParticlePriority { get; set; } // Minimum priority to render
}
```

### GameLODManager

```csharp
public class GameLodManager
{
    private StaticGameLodLevel _staticLod = StaticGameLodLevel.Medium;
    private DynamicGameLodLevel _dynamicLod = DynamicGameLodLevel.VeryHigh;
    
    // Static LOD - determined once at startup
    public StaticGameLodLevel FindStaticLodLevel()
    {
        var hardware = HardwareDetector.Detect();
        return DetermineStaticLod(hardware);
    }
    
    public void SetStaticLodLevel(StaticGameLodLevel level)
    {
        _staticLod = level;
        ApplyStaticLodSettings();
    }
    
    // Dynamic LOD - adjusts every frame based on FPS
    public DynamicGameLodLevel FindDynamicLodLevel(float fps)
    {
        if (fps > 50) return DynamicGameLodLevel.VeryHigh;
        if (fps > 30) return DynamicGameLodLevel.High;
        if (fps > 20) return DynamicGameLodLevel.Medium;
        return DynamicGameLodLevel.Low;
    }
    
    public void SetDynamicLodLevel(DynamicGameLodLevel level)
    {
        _dynamicLod = level;
        ApplyDynamicLodSettings();
    }
}
```

### Hardware Detection

```csharp
public class HardwareInfo
{
    public int CpuCores { get; set; }
    public double CpuGhz { get; set; }
    public ulong RamMb { get; set; }
    public string GpuName { get; set; }
    public ulong GpuMemoryMb { get; set; }
}

public class HardwareDetector
{
    public static HardwareInfo Detect()
    {
        return new HardwareInfo
        {
            CpuCores = Environment.ProcessorCount,
            CpuGhz = GetCpuSpeed(),
            RamMb = GetTotalMemory(),
            GpuName = GetGpuName(),
            GpuMemoryMb = GetGpuMemory()
        };
    }
}
```

---

## Implementation Checklist

### Phase 1: LOD Infrastructure (8 hours)

- [ ] **Verify StaticGameLod/DynamicGameLod INI parsing**
  - Check existing implementations in GameData
  - Ensure all LOD parameters load from Data/INI/GameLOD.ini
  - Verify GameLODPresets.ini with preset configurations

- [ ] **Create GameLodManager class**
  - Store static and dynamic LOD settings
  - Implement getter/setter methods
  - Create Apply methods for each LOD change

- [ ] **Implement hardware detection**
  - Detect CPU core count and frequency
  - Detect available RAM
  - Detect GPU name and VRAM
  - Map hardware to LOD presets

- [ ] **Create LOD selection algorithm**
  - Map hardware to StaticGameLodLevel
  - Consider minimum requirements
  - Use fallback strategies for unknown hardware

- [ ] **Unit tests for LOD selection**
  - Test various hardware configurations
  - Test fallback behavior
  - Test threshold transitions

### Phase 2: Static LOD Application (8 hours)

- [ ] **Implement texture reduction**
  - Create MipmapSelector based on reduction factor
  - Hook into OnDemandTextureLoader
  - Apply 2^n reduction to texture resolution
  - Cache reduced textures

- [ ] **Implement shadow system LOD**
  - Disable shadow volumes on low LOD
  - Use decal shadows instead
  - Disable shadows entirely on very low LOD

- [ ] **Implement terrain feature LOD**
  - Control tree visibility
  - Control cloud map rendering
  - Control light map usage
  - Control soft water edge rendering

- [ ] **Implement particle LOD**
  - Enforce MaxParticleCount limit
  - Create particle culling system
  - Skip low-priority particles on low LOD

- [ ] **Integration tests**
  - Load scene with different static LODs
  - Verify visual quality differences
  - Measure memory impact

### Phase 3: Dynamic LOD Adjustment (8 hours)

- [ ] **Implement FPS measurement**
  - Track frame times over 1-2 second window
  - Calculate average FPS
  - Implement hysteresis to avoid thrashing

- [ ] **Implement dynamic LOD update loop**
  - Check FPS every 1 second
  - Call FindDynamicLodLevel(fps)
  - Only change LOD if threshold crossed with hysteresis
  - Log LOD changes

- [ ] **Implement particle skip mask**
  - Create particle skip patterns
  - Apply mask in particle renderer
  - Skip every Nth particle based on mask

- [ ] **Implement animation scale adjustment**
  - Reduce death animation duration on low FPS
  - Scale other animation speeds accordingly
  - Maintain visual coherence

- [ ] **Performance tests**
  - Measure FPS at different LODs
  - Verify hysteresis prevents thrashing
  - Verify smooth transitions

### Phase 4: Asset Preloading (6 hours)

- [ ] **Create preload system**
  - After map load, queue assets for preloading
  - Preload buildings, units, effects, particles
  - Preload terrain-specific models and textures

- [ ] **Implement map asset preloading**
  - Iterate through all map objects
  - Call PreloadAssets() on each drawable
  - Load faction-specific units and structures
  - Load damage states and debris

- [ ] **Implement memory tracking**
  - Log memory before/after preload
  - Report preload impact
  - Implement cleanup for unneeded assets

- [ ] **Create preload progress system**
  - Show loading screen during preload
  - Display progress percentage
  - Allow cancellation

- [ ] **Integration tests**
  - Preload various maps
  - Verify all assets available
  - Measure preload time

### Phase 5: Streaming Integration (6 hours)

- [ ] **Hook LOD changes into asset manager**
  - When static LOD changes, reload textures
  - When dynamic LOD changes, adjust particle limits
  - Manage memory efficiently during changes

- [ ] **Implement texture pool management**
  - Allocate texture pool based on LOD
  - Limit pool size on low LOD
  - Implement LRU eviction when pool full

- [ ] **Implement model LOD system**
  - Support multiple LOD models for complex objects
  - Select appropriate LOD based on distance
  - Load/unload models as needed

- [ ] **Performance profiling**
  - Measure FPS stability across LOD transitions
  - Measure memory usage stability
  - Measure load times

- [ ] **Final integration tests**
  - Play game across multiple maps
  - Force LOD changes mid-game
  - Monitor for crashes or visual artifacts

---

## File Structure

```
src/OpenSage.Game/GameLogic/Lod/
├── GameLodManager.cs                 [NEW]
├── StaticGameLod.cs                  [NEW - extend existing]
├── DynamicGameLod.cs                 [NEW - extend existing]
├── HardwareDetector.cs               [NEW]
├── LodPresetResolver.cs              [NEW]
└── LodConstants.cs                   [NEW]

src/OpenSage.Game/Assets/
├── AssetPreloader.cs                 [NEW]
└── AssetStreamingManager.cs          [NEW]

src/OpenSage.Graphics/
├── Textures/MipmapSelector.cs        [NEW]
├── Textures/TextureReductionManager.cs [NEW]
└── RenderLodSelector.cs              [NEW]

src/OpenSage.Game/Graphics/
├── ParticleSystems/ParticleSkipMask.cs [NEW]
└── Rendering/RenderPipeline.cs       [MODIFY - LOD integration]

Data/INI/
├── GameLOD.ini                       [MODIFY - ensure complete]
└── GameLODPresets.ini                [MODIFY - ensure complete]
```

---

## INI Configuration

### GameLOD.ini

```ini
; Static LOD Levels
StaticGameLod VeryLow
  TextureReduction = 4              ; 2^4 = 16x reduction
  MaxParticleCount = 100
  UseShadowVolumes = No
  UseShadowDecals = No
  UseCloudMap = No
  UseLightMap = No
  ShowSoftWaterEdge = No
  UseTreeSway = No
  UseTrees = No
EndStaticGameLod

StaticGameLod Low
  TextureReduction = 3              ; 2^3 = 8x reduction
  MaxParticleCount = 300
  UseShadowVolumes = No
  UseShadowDecals = Yes
  UseCloudMap = No
  UseLightMap = No
  ShowSoftWaterEdge = No
  UseTreeSway = No
  UseTrees = Yes
EndStaticGameLod

StaticGameLod Medium
  TextureReduction = 2              ; 2^2 = 4x reduction
  MaxParticleCount = 1000
  UseShadowVolumes = Yes
  UseShadowDecals = Yes
  UseCloudMap = Yes
  UseLightMap = Yes
  ShowSoftWaterEdge = Yes
  UseTreeSway = Yes
  UseTrees = Yes
EndStaticGameLod

StaticGameLod High
  TextureReduction = 1              ; 2x reduction
  MaxParticleCount = 3000
  UseShadowVolumes = Yes
  UseShadowDecals = Yes
  UseCloudMap = Yes
  UseLightMap = Yes
  ShowSoftWaterEdge = Yes
  UseTreeSway = Yes
  UseTrees = Yes
EndStaticGameLod

StaticGameLod Ultra
  TextureReduction = 0              ; No reduction
  MaxParticleCount = 5000
  UseShadowVolumes = Yes
  UseShadowDecals = Yes
  UseCloudMap = Yes
  UseLightMap = Yes
  ShowSoftWaterEdge = Yes
  UseTreeSway = Yes
  UseTrees = Yes
EndStaticGameLod

; Dynamic LOD Levels (FPS-based)
DynamicGameLod Low
  ParticleSkipMask = 0x000000FF      ; Skip 255 of 256 particles
  DebrisSkipMask = 0x00000007        ; Skip 7 of 8 debris
  SlowDeathScale = 2.0               ; 2x faster death animation
  MinDynamicParticlePriority = 10
EndDynamicGameLod

DynamicGameLod Medium
  ParticleSkipMask = 0x0000000F      ; Skip 15 of 16 particles
  DebrisSkipMask = 0x00000001        ; Skip 1 of 2 debris
  SlowDeathScale = 1.5
  MinDynamicParticlePriority = 5
EndDynamicGameLod

DynamicGameLod High
  ParticleSkipMask = 0x00000003      ; Skip 3 of 4 particles
  DebrisSkipMask = 0x00000000        ; Render all debris
  SlowDeathScale = 1.0
  MinDynamicParticlePriority = 1
EndDynamicGameLod

DynamicGameLod VeryHigh
  ParticleSkipMask = 0x00000000      ; Render all particles
  DebrisSkipMask = 0x00000000
  SlowDeathScale = 1.0
  MinDynamicParticlePriority = 0
EndDynamicGameLod
```

---

## Integration Points

### 1. Game Initialization

```csharp
public class Game
{
    private GameLodManager _lodManager;
    
    private void Initialize()
    {
        // Detect hardware and set initial LOD
        _lodManager = new GameLodManager();
        var staticLod = _lodManager.FindStaticLodLevel();
        _lodManager.SetStaticLodLevel(staticLod);
        
        // Apply initial LOD settings
        ApplyLodSettings();
    }
    
    private void ApplyLodSettings()
    {
        // Apply to texture manager
        TextureManager.SetTextureReduction(_lodManager.CurrentStaticLod.TextureReductionFactor);
        
        // Apply to particle system
        ParticleSystemManager.SetMaxParticleCount(_lodManager.CurrentStaticLod.MaxParticleCount);
        
        // Apply to rendering
        RenderPipeline.SetShadowSettings(_lodManager.CurrentStaticLod.UseShadowVolumes, 
                                        _lodManager.CurrentStaticLod.UseShadowDecals);
    }
}
```

### 2. Dynamic FPS Update

```csharp
private float _fpsAccumulator;
private int _frameCount;

public void Update(float deltaTime)
{
    _fpsAccumulator += deltaTime;
    _frameCount++;
    
    // Check FPS every 1 second
    if (_fpsAccumulator >= 1.0f)
    {
        float fps = _frameCount / _fpsAccumulator;
        var newDynamicLod = _lodManager.FindDynamicLodLevel(fps);
        
        if (newDynamicLod != _lodManager.CurrentDynamicLod)
        {
            _lodManager.SetDynamicLodLevel(newDynamicLod);
            Logger.Info($"LOD changed to {newDynamicLod}, FPS: {fps:F1}");
        }
        
        _fpsAccumulator = 0;
        _frameCount = 0;
    }
}
```

### 3. Preload Integration

```csharp
private async void OnMapLoaded(Map map)
{
    var preloader = new AssetPreloader();
    var progress = new PreloadProgress();
    
    var preloadTask = preloader.PreloadMapAssetsAsync(map, progress);
    
    // Show loading screen
    ShowLoadingScreen(progress);
    
    await preloadTask;
    
    // Hide loading screen, start game
    HideLoadingScreen();
}
```

---

## Performance Targets

### Static LOD Performance
- VeryLow: 60+ FPS on minimum hardware
- Low: 60+ FPS on low-mid hardware
- Medium: 60+ FPS on mid-range hardware
- High: 60+ FPS on high-end hardware
- Ultra: 60+ FPS on top-end hardware

### Dynamic LOD Performance
- Smooth FPS monitoring without jitter
- LOD transitions within 1 frame
- No memory spikes during transitions
- Hysteresis prevents thrashing (±5 FPS threshold)

### Memory Impact
- VeryLow LOD: <500 MB
- Low LOD: <750 MB
- Medium LOD: <1.2 GB
- High LOD: <1.8 GB
- Ultra LOD: <2.5 GB

---

## Success Criteria

✅ Hardware detection working on all platforms (Windows, Mac, Linux)  
✅ Static LOD selection matches hardware capabilities  
✅ Texture reduction properly scales resolution  
✅ Dynamic LOD adjusts based on FPS  
✅ No visual popping during LOD transitions  
✅ Memory usage stays within target ranges  
✅ Asset preloading works for all map types  
✅ Performance improves by 15-25% on low LOD  
✅ Unit tests: 100% pass rate  
✅ Integration tests: All LOD levels playable  

---

## References

- Original: `references/generals_code/GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h`
- Original: `references/generals_code/GeneralsMD/Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DGameClient.cpp`
- Original: `references/generals_code/GeneralsMD/Code/GameEngineDevice/Source/W3DDevice/GameClient/W3DDisplay.cpp`
- Related: [PLAN-013-014_ANALYSIS.md](PLAN-013-014_ANALYSIS.md)

