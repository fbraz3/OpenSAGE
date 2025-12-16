# PLAN-013 & PLAN-014 Implementation Analysis

**Date**: December 16, 2025  
**Status**: Research Complete - Ready for Implementation Planning  
**Analyzed Systems**: EA Generals Texture Atlasing + Asset Streaming  

---

## Executive Summary

Based on deep analysis of the original EA Generals source code and OpenSAGE's current implementation:

- **PLAN-013 (Texture Atlasing for UI)**: Should focus on **UI image atlasing similar to EA's Image system**, NOT full mesh atlasing (which is already partially handled by terrain roads)
- **PLAN-014 (Asset Streaming)**: Should implement **LOD-based streaming using existing StaticGameLod/DynamicGameLod framework** already partially implemented in OpenSAGE

---

## PLAN-013: Texture Atlasing for UI Elements

### Current State in OpenSAGE

**What already exists**:
- `TextureAtlas` class for terrain roads (roads/RoadCollection.cs)
- `RadiusCursorDecals` system using TextureArray for decals
- Partial `TextureArray` usage for terrain textures
- **Gap**: No UI image atlasing system exists yet

**What EA Generals does**:
- Uses `Image` class to store atlas coordinates (simple approach!)
- Each Image stores:
  - Texture filename
  - Texture size (width/height of the atlas page)
  - UV coordinates (normalized by texture size)
  - Image size in pixels
  - Status bits (rotation, raw texture flags)

### EA's Image System Architecture

```cpp
// From GeneralsMD/Code/GameEngine/Include/GameClient/Image.h

class Image
{
    AsciiString m_name;           // "ButtonUp", "MenuBackground", etc.
    AsciiString m_filename;       // "UI/Buttons.dds" - texture atlas file
    ICoord2D m_textureSize;       // Atlas texture dimensions (e.g., 512x512)
    Region2D m_UVCoords;          // Normalized UV bounds (0.0 to 1.0)
    ICoord2D m_imageSize;         // Pixel dimensions of this image
    UnsignedInt m_status;         // Rotation + flags
};

// INI format:
// Image = ButtonUp
//   Texture = UI/Buttons.dds
//   TextureWidth = 512
//   TextureHeight = 512
//   Coords = Left:0 Top:0 Right:64 Bottom:64
//   Status = ROTATED_90_CLOCKWISE (optional)
```

### Key Implementation Details

1. **Coordinate Parsing**:
   - INI format: `Coords = Left:X Top:Y Right:W Bottom:H`
   - Raw pixel coordinates on the atlas texture
   - **Normalized** by dividing by texture size: `uv = coord / textureSize`
   - Image size = `(Right-Left, Bottom-Top)` in pixels

2. **Image Collection**:
   - `ImageCollection` holds all images in a map
   - Loaded from `Data\INI\MappedImages\TextureSize_XXX` directories
   - Supports rotation (90° clockwise) - swaps width/height

3. **WinDrawData Usage**:
   - GUI controls reference `Image` pointers
   - Each control state (enabled/disabled/hilited) has its own Image
   - Drawing system uses Image::getUV() to fetch normalized coordinates

4. **ImagePacker Tool**:
   - Tool to pack individual images into texture atlases
   - Generates INI files with coordinates
   - Can rotate images 90° to fit better
   - Creates separate atlas files per size category

### OpenSAGE Implementation Plan for PLAN-013

**Step 1: Create Image & ImageCollection classes**
- Mirror EA's structure
- Implement coordinate normalization
- Support rotation flags

**Step 2: Create MappedImage INI parsing**
- Parse image definitions from INI files
- Store in ImageCollection
- Integrate with existing INI system

**Step 3: Integrate with GUI/WND rendering**
- Update `WndWindowManager` to use Images
- Modify control rendering to fetch UV coords from Image
- Update dirty region tracking for atlased images

**Step 4: Create texture atlas from existing UI assets**
- Analyze current scattered UI textures
- Create packed atlases
- Generate INI coordinate data

---

## PLAN-014: Asset Streaming

### Current State in OpenSAGE

**What already exists**:
- `StaticGameLod` and `DynamicGameLod` structs parsed from INI
- `OnDemandAssetLoadStrategy` for lazy loading
- `ContentManager` for asset handling
- `AssetStore` managing asset collections
- Missing: **Actual streaming implementation**

**What EA Generals does**:
- **Static LOD** (at startup):
  - Determined by hardware (CPU, RAM, GPU)
  - Sets texture reduction factor (0-4, dividing resolution by 2^n)
  - Controls max particle count
  - Controls shadow type (volumes vs. decals)
  - Controls terrain features (trees, clouds, light maps)
  - Loaded from `Data\INI\GameLOD.ini` and `GameLODPresets.ini`

- **Dynamic LOD** (during gameplay):
  - Adjusts based on FPS measurement
  - Levels: LOW, MEDIUM, HIGH, VERY_HIGH
  - Controls particle/debris skipping
  - Controls animation speed scaling
  - Minimum particle priority threshold

- **Asset Preloading**:
  - `GameClient::preloadAssets()` loads map-specific assets
  - Iterates through all Drawable objects
  - Calls `preloadAssets()` on each module
  - Logs memory before/after

### EA's LOD System Architecture

```cpp
// From GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h

// Static LOD (determined once at startup)
struct StaticGameLodInfo
{
    Int m_textureReduction;        // 0-4: divide resolution by 2^n
    Int m_maxParticleCount;
    Bool m_useShadowVolumes;
    Bool m_useShadowDecals;
    Bool m_useCloudMap;
    Bool m_useLightMap;
    Bool m_showSoftWaterEdge;
    Bool m_useTreeSway;
    Bool m_useTrees;
    Int m_maxTankTrackEdges;
    // ... more fields
};

// Dynamic LOD (adjusts during gameplay)
struct DynamicGameLodInfo
{
    UnsignedInt m_dynamicParticleSkipMask;    // Skip every Nth particle
    UnsignedInt m_dynamicDebrisSkipMask;
    Real m_slowDeathScale;
    Int m_minDynamicParticlePriority;
};

// Manager
class GameLODManager
{
    StaticGameLodLevel findStaticLODLevel();
    DynamicGameLodLevel findDynamicLODLevel(Real fps);
    void setStaticLODLevel(StaticGameLodLevel level);
    void setDynamicLODLevel(DynamicGameLodLevel level);
};
```

### OpenSAGE Implementation Plan for PLAN-014

**Step 1: Enhance LOD infrastructure**
- Verify StaticGameLod/DynamicGameLod INI parsing
- Add hardware detection (CPU, RAM, GPU capabilities)
- Implement `findStaticLODLevel()` based on hardware

**Step 2: Implement texture reduction**
- Create texture loading with resolution scaling
- Hook into texture loader (OnDemandTextureLoader)
- Apply mipmap selection or resize based on LOD level

**Step 3: Implement dynamic LOD adjustment**
- Measure average FPS during gameplay
- Call `findDynamicLODLevel(fps)` periodically (e.g., every 1 second)
- Update particle skip masks and priority thresholds

**Step 4: Asset preloading framework**
- Create preload phase after map load
- Iterate through all game objects
- Call preload on drawable/audio/particle modules
- Log memory usage impact

**Step 5: Streaming integration**
- Hook LOD changes into ContentManager
- Unload high-detail assets when LOD decreases
- Load better assets when LOD increases
- Handle texture pool management

---

## Dependency Analysis

### PLAN-013 Dependencies
- ✅ PLAN-011 (Tooltip System) - provides UI foundation
- ✅ Existing GUI/WND rendering
- ✅ INI parsing infrastructure
- 🔧 No critical blockers - can start immediately

### PLAN-014 Dependencies
- ✅ Existing LOD structures
- ✅ ContentManager
- ✅ AssetStore
- 🔧 No critical blockers - can start immediately

### Cross-Dependencies
- PLAN-013 ➜ PLAN-014: If UI atlasing is done first, can enable UI-specific LOD (reduce atlas quality on low LOD)
- PLAN-014 ➜ PLAN-013: If streaming is done first, can apply streaming to atlased textures

**Recommendation**: PLAN-013 first (simpler, UI-focused), then PLAN-014 (more complex, affects all systems)

---

## Risk Assessment

### PLAN-013 Risks
- **Low**: Mirrors well-understood EA system
- **Risk**: INI parsing edge cases with special characters
- **Risk**: Rotation flag handling in rendering
- **Mitigation**: Comprehensive unit tests for coordinate transforms

### PLAN-014 Risks
- **Medium**: Complex FPS-based state machine
- **Risk**: Performance regression if LOD changes too frequently
- **Risk**: Memory thrashing with rapid LOD switches
- **Mitigation**: Add hysteresis to LOD transitions, implement cooldown

---

## File Locations in References

```
references/generals_code/
├── GeneralsMD/Code/GameEngine/Include/GameClient/
│   ├── Image.h (UI image atlas definition)
│   └── WinInstanceData.h (GUI data structures)
├── GeneralsMD/Code/GameEngine/Source/GameClient/System/
│   └── Image.cpp (atlas coordinate parsing)
├── GeneralsMD/Code/GameEngine/Include/Common/
│   ├── GameLOD.h (LOD manager interface)
│   └── GlobalData.h (LOD configuration)
└── GeneralsMD/Code/Tools/ImagePacker/ (atlas generation tool)
```

---

## Next Steps

1. **PLAN-013 Detailed Design** (1-2 hours)
   - Design OpenSAGE Image class structure
   - Plan ImageCollection integration
   - Design GUI binding mechanism

2. **PLAN-013 Implementation** (2-3 days)
   - Create Image & ImageCollection
   - Implement INI parsing
   - Integrate with WndWindowManager
   - Create atlas from existing assets

3. **PLAN-014 Detailed Design** (1-2 hours)
   - Hardware detection strategy
   - FPS sampling approach
   - Streaming lifecycle

4. **PLAN-014 Implementation** (3-4 days)
   - Enhance LOD system
   - Implement texture streaming
   - Add particle/asset LOD
   - Performance profiling

---

## Summary Table

| Aspect | PLAN-013 | PLAN-014 |
|--------|----------|----------|
| Complexity | Medium | High |
| Est. Duration | 2-3 days | 3-4 days |
| Lines of Code | 500-800 | 1000-1500 |
| Files to Create | 4-6 | 3-5 |
| Files to Modify | 5-8 | 10-15 |
| Risk Level | Low | Medium |
| Dependencies | UI system | ContentManager |
| Performance Impact | +5-10% UI | +15-25% overall |
| Test Coverage | Unit + integration | Unit + integration + perf |

