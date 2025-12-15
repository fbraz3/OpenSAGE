# OpenSAGE Water Rendering System - Comprehensive Analysis

**Last Updated:** December 15, 2025

## Executive Summary

OpenSAGE has a **partially implemented water rendering system** with:
- ✅ Basic water area mesh generation from map data
- ✅ Reflection and refraction map rendering
- ✅ Water shader with UV scrolling and depth-based transparency
- ✅ Multiple water area types support (standing water, rivers, standing waves)
- ⚠️ **UNIMPLEMENTED:** Wave animation for standing wave areas
- ⚠️ **UNIMPLEMENTED:** River texture animation ("downstream" scrolling)
- ⚠️ **TODO:** Material resource sets and bump map textures
- ⚠️ **TODO:** Caustics rendering (infrastructure exists but disabled)

---

## 1. Current Water System Files

### Core Water Area Management

| File | Purpose | Status |
|------|---------|--------|
| [src/OpenSage.Game/Terrain/WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs) | Main water area mesh and rendering | ✅ Implemented |
| [src/OpenSage.Game/Terrain/WaterAreaCollection.cs](src/OpenSage.Game/Terrain/WaterAreaCollection.cs) | Collection management for all water areas | ✅ Implemented |
| [src/OpenSage.Game/Terrain/WaterSet.cs](src/OpenSage.Game/Terrain/WaterSet.cs) | Water material settings (INI-based) | ✅ Implemented |
| [src/OpenSage.Game/Terrain/WaterTransparency.cs](src/OpenSage.Game/Terrain/WaterTransparency.cs) | Global water transparency/reflection settings | ✅ Implemented |
| [src/OpenSage.Game/Terrain/WaterTextureList.cs](src/OpenSage.Game/Terrain/WaterTextureList.cs) | Water texture list asset | ✅ Implemented |

### Rendering Pipeline

| File | Purpose | Status |
|------|---------|--------|
| [src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs) | Main water rendering orchestrator | ✅ Implemented |
| [src/OpenSage.Game/Graphics/Rendering/Water/WaterSettings.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterSettings.cs) | Runtime water quality settings | ✅ Implemented |
| [src/OpenSage.Game/Graphics/Rendering/Water/WaterData.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterData.cs) | Reflection/refraction texture/framebuffer management | ✅ Implemented |
| [src/OpenSage.Game/Graphics/Shaders/WaterShaderResources.cs](src/OpenSage.Game/Graphics/Shaders/WaterShaderResources.cs) | Water shader pipeline setup | ✅ Implemented |

### Map Data Structures

| File | Purpose | Status |
|------|---------|--------|
| [src/OpenSage.Game/Data/Map/StandingWaterArea.cs](src/OpenSage.Game/Data/Map/StandingWaterArea.cs) | Static water areas (map format parser) | ✅ Implemented |
| [src/OpenSage.Game/Data/Map/StandingWaveArea.cs](src/OpenSage.Game/Data/Map/StandingWaveArea.cs) | Animated wave areas (map format parser) | ✅ Implemented |
| [src/OpenSage.Game/Data/Map/RiverArea.cs](src/OpenSage.Game/Data/Map/RiverArea.cs) | River areas with flow animation (map format parser) | ✅ Implemented |
| [src/OpenSage.Game/Data/Map/GlobalWaterSettings.cs](src/OpenSage.Game/Data/Map/GlobalWaterSettings.cs) | Global map water reflection plane settings | ✅ Implemented |

### Shaders

| File | Purpose | Status |
|------|---------|--------|
| [src/OpenSage.Game/Assets/Shaders/Water.vert](src/OpenSage.Game/Assets/Shaders/Water.vert) | Water vertex shader (GLSL 450) | ✅ Implemented |
| [src/OpenSage.Game/Assets/Shaders/Water.frag](src/OpenSage.Game/Assets/Shaders/Water.frag) | Water fragment shader with reflection/refraction | ✅ Implemented |

---

## 2. Water Mesh Data Structure

### Geometry Generation

**Location:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs#L96) - `CreateGeometry()` method

#### How it works:
1. **Input:** Polygon points from map file (2D coordinates) + height value
2. **Triangulation:** Uses `Triangulator.Triangulate()` to convert polygon to triangles
3. **Vertex Data:** Simple structure with position only
4. **Buffers:**
   - **Vertex Buffer:** Static GPU buffer containing `WaterVertex` (Vector3 position)
   - **Index Buffer:** Static GPU buffer containing triangle indices
   - **Bounding Box:** Axis-aligned bounding box for culling

```csharp
// Water vertex structure - very simple
struct WaterVertex
{
    Vector3 Position;  // World position (X, Y, height)
}
```

### Water Area Types

OpenSAGE supports three water area types from map files:

| Type | Source Data | Current Status | Notes |
|------|-------------|-----------------|-------|
| **Standing Water** | `StandingWaterArea` (SWAA chunk) | ✅ Basic mesh | Static polygon, no animation |
| **Standing Waves** | `StandingWaveArea` (SWAV chunk) | ⚠️ Mesh only | Data available but waves **NOT implemented** |
| **Rivers** | `PolygonTrigger` with IsRiver flag | ⚠️ Mesh only | Treated as static water; flow animation **NOT implemented** |

### Mesh Data in Memory

```
WaterArea Instance:
├─ _vertexBuffer (static) 
│  └─ Array of WaterVertex structures
├─ _indexBuffer (static)
│  └─ Array of UInt32 triangle indices
├─ _boundingBox (for frustum culling)
└─ _world (Matrix4x4 - always Identity with Y translation = water height)
```

---

## 3. Wave Animation Code

### Current Status: ❌ NOT IMPLEMENTED

**Location:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs#L140)

```csharp
private WaterArea(
    AssetLoadContext loadContext,
    StandingWaveArea area) : this(loadContext, area.Name)
{
    CreateGeometry(loadContext, area.Points, area.FinalHeight);
    //TODO: add waves  // <-- UNIMPLEMENTED
}
```

### StandingWaveArea Data Available

The map parser has all wave animation parameters:

```csharp
public class StandingWaveArea
{
    // Wave dimensions
    public uint FinalWidth { get; }          // Target width when fully expanded
    public uint FinalHeight { get; }         // Target height when fully expanded
    public uint InitialWidthFraction { get; }   // Starting width (as fraction of final)
    public uint InitialHeightFraction { get; }  // Starting height (as fraction of final)
    
    // Wave dynamics
    public uint InitialVelocity { get; }     // Starting outward velocity
    public uint TimeToFade { get; }          // Duration until wave dissipates
    public uint TimeToCompress { get; }      // Duration of compression phase
    public uint TimeOffset2ndWave { get; }   // Delay before second wave spawns
    public uint DistanceFromShore { get; }   // How far from shore wave originates
    
    // Rendering
    public string Texture { get; }           // Wave texture name
    public bool EnablePcaWave { get; }       // Physics-based wave flag
    public string WaveParticleFXName { get; }  // Particle effect [RA3+]
}
```

### What Wave Animation Should Do

Based on map data structure and EA's SAGE engine:
1. **Wave Expansion:** Waves expand from center, growing from initial fraction to final size
2. **Wave Movement:** Waves move outward at `InitialVelocity`
3. **Fade Out:** Waves fade over `TimeToFade` milliseconds
4. **Compression:** Wave compress over `TimeToCompress` milliseconds
5. **Multiple Waves:** Second wave spawns after `TimeOffset2ndWave` delay
6. **Distance Control:** Waves originate from `DistanceFromShore` units away

---

## 4. Water Area Loading from Map Files

### Loading Pipeline

**Location:** [Scene3D.cs](src/OpenSage.Game/Scene3D.cs#L299)

```csharp
// During map loading:
WaterAreas = AddDisposable(new WaterAreaCollection(
    mapFile.PolygonTriggers,          // Water areas marked as IsWater or IsRiver
    mapFile.StandingWaterAreas,       // SWAA chunk data
    mapFile.StandingWaveAreas,        // SWAV chunk data
    game.AssetStore.LoadContext));
```

### WaterAreaCollection Processing

**Location:** [WaterAreaCollection.cs](src/OpenSage.Game/Terrain/WaterAreaCollection.cs#L17)

1. **PolygonTriggers** → Filters triggers where `IsWater || IsRiver`
2. **StandingWaterAreas** → Loads SWAA chunk water polygons
3. **StandingWaveAreas** → Loads SWAV chunk animated wave areas
4. **Validation:** Skips water areas with < 3 points (invalid polygons)

### Map Data Structure

Map files contain water definitions in three formats:

| Map Chunk | Class | Purpose |
|-----------|-------|---------|
| SWAA | `StandingWaterAreas` | Static water polygon areas |
| SWAV | `StandingWaveAreas` | Animated wave areas (BFME+) |
| PTRG | `PolygonTriggers` | Trigger areas marked as water/rivers |

Each water area has:
- **Geometry:** Polygon points array (Vector2[])
- **Height:** Water surface Z-coordinate (uint)
- **Rendering Data:**
  - Texture references
  - UV scroll speeds
  - Blending mode (additive vs normal)
  - Bump/sky textures
  - Depth colors (for variable transparency)

---

## 5. Current Rendering Pipeline for Water

### Render Path Overview

```
Scene3D.BuildRenderList()
    ↓
WaterAreaCollection.BuildRenderList(renderList)
    ↓ (adds to renderList.Water)
    ↓
RenderPipeline.Execute()
    ↓
RenderPipeline.DoRenderPass(bucket="Water")
    ↓
For each water RenderItem:
    ├─ CalculateWaterShaderMap()  ← Reflection/Refraction pass
    │  ├─ WaterMapRenderer.RenderWaterShaders()
    │  │  ├─ Render scene to ReflectionFramebuffer (mirrored camera)
    │  │  └─ Render scene to RefractionFramebuffer (clipped below water)
    │  └─ Update UV scroll offset (using DeltaTimer)
    │
    ├─ Set Water Material Pipeline
    ├─ Set Water Shader Resource Set (textures, constants)
    └─ Draw water mesh (vertex + index buffers)
```

### Key Components

#### 1. **WaterMapRenderer** [src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs)

**Purpose:** Orchestrates reflection/refraction texture generation

**Key Methods:**
- `RenderWaterShaders()` - Main entry point, manages render pass callbacks
- `CalculateUVOffset()` - Scrolls water texture based on `UScrollPerMS`/`VScrollPerMS`
- `UpdateVariableBuffers()` - Updates shader constants each frame

**State Management:**
- Triple buffering for Metal GPU (deferred cleanup)
- Clipping planes for reflection/refraction
- Camera mirroring for reflections
- UV offset accumulation

#### 2. **WaterData** [src/OpenSage.Game/Graphics/Rendering/Water/WaterData.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterData.cs)

**Purpose:** Manages GPU textures and framebuffers

**Resources Created:**
- `ReflectionMap` - 32-bit color texture (default 256x256)
- `ReflectionDepthMap` - 32-bit depth texture
- `ReflectionMapFramebuffer` - Renders scene reflections

- `RefractionMap` - 32-bit color texture (default 256x256)
- `RefractionDepthMap` - 32-bit depth texture
- `RefractionMapFramebuffer` - Renders underwater scene

#### 3. **WaterSettings** [src/OpenSage.Game/Graphics/Rendering/Water/WaterSettings.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterSettings.cs)

**Purpose:** Runtime quality/feature toggles

**Settings:**
- `ReflectionMapSize` - Resolution (clamped to power of 2)
- `RefractionMapSize` - Resolution (clamped to power of 2)
- `ReflectionRenderDistance` - Culling distance for reflections (600m default)
- `RefractionRenderDistance` - Culling distance for refractions (2000m default)
- `ClippingOffset` - Plane offset to prevent artifacts (1.0 default)
- `IsRenderReflection` - Feature toggle
- `IsRenderRefraction` - Feature toggle
- `IsRenderSoftEdge` - Soft edge transparency toggle
- `IsRenderCaustics` - Caustics effect toggle

### Shader Passes

#### Vertex Shader: Water.vert

**Input:** Water polygon vertices (World position)

**Output to Fragment Shader:**
- `out_WorldPosition` - For water surface texel lookups
- `out_CloudUV` - For lighting calculation
- `out_ViewSpaceDepth` - For depth-based transparency

#### Fragment Shader: Water.frag

**Inputs:**
- Water texture with UV offset
- Reflection map (if enabled)
- Refraction map (if enabled)
- Refraction depth map (for soft edges)
- Bump texture (normal map)
- Depth constants for transparency

**Processing:**
1. **UV Scrolling:** Apply `UVOffset` to texture coordinates
2. **Normal Mapping:** Read bump texture for distortion
3. **Fresnel Effect:** Blend reflection/refraction based on view angle
4. **Depth Calculation:** Compare fragment depth to refraction depth
5. **Transparency:** Calculate alpha based on water depth (configurable)
6. **Lighting:** Apply terrain lighting to water surface
7. **Shadow Application:** Include shadow visibility
8. **Compositing:** Final color = (texture * diffuse * lighting) * alpha

**Key Constants:**
```glsl
const float distortionPower = 0.05f;      // How much textures distort reflections
const float depthFactor = 2.0f;            // Transparency falloff
const float minWaterDepth = 20.0f;         // Minimum depth for transparency
```

---

## 6. Shader Files and Water Rendering

### Shader Assets

| File | Type | Language | Status |
|------|------|----------|--------|
| [Water.vert](src/OpenSage.Game/Assets/Shaders/Water.vert) | Vertex | GLSL 450 | ✅ Implemented |
| [Water.frag](src/OpenSage.Game/Assets/Shaders/Water.frag) | Fragment | GLSL 450 | ✅ Implemented |

### Shader Resource Binding (GLSL)

**Resource Set Layout:**
```
Set 0: Global constants (shared by all shaders)
Set 1: Global lighting constants
Set 2: Water-specific (WaterResourceLayout)
  ├─ Binding 0: WaterConstantsPS (UBO)
  │  ├─ vec2 UVOffset
  │  ├─ float FarPlaneDistance
  │  ├─ float NearPlaneDistance
  │  ├─ uint IsRenderReflection
  │  ├─ uint IsRenderRefraction
  │  ├─ float TransparentWaterMinOpacity
  │  ├─ float TransparentWaterDepth
  │  ├─ vec4 DiffuseColor
  │  └─ vec4 TransparentDiffuseColor
  ├─ Binding 1: WaterTexture (sampled texture)
  ├─ Binding 2: BumpTexture (sampled texture)
  ├─ Binding 3: WaterSampler
  ├─ Binding 4: ReflectionMap (sampled texture)
  ├─ Binding 5: ReflectionMapSampler
  ├─ Binding 6: RefractionMap (sampled texture)
  ├─ Binding 7: RefractionMapSampler
  └─ Binding 8: RefractionDepthMap (sampled texture)
```

### Shader Pipeline Configuration

**Location:** [WaterShaderResources.cs](src/OpenSage.Game/Graphics/Shaders/WaterShaderResources.cs)

```csharp
// Blend state: SingleAlphaBlend (alpha blending enabled)
// Depth state: DepthOnlyLessEqualRead (read depth, no write)
// Rasterizer: DefaultFrontIsCounterClockwise
// Topology: TriangleList
// Output: GameOutputDescription (depth + color)
```

**Important:** Water uses **alpha blending** and **reads depth but doesn't write** it. This allows water to blend with opaque geometry below while being occluded by geometry above.

### Texture Scrolling Implementation

**Location:** [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs#L100)

```csharp
private void CalculateUVOffset(TimeOfDay timeOfDay)
{
    // UVScroll specifies pixels the texture moves per millisecond
    var deltaTime = (float)_deltaTimer.CurrentGameTime.DeltaTime.Milliseconds;
    var tex = _waterTextureSet[timeOfDay];
    var texSize = new Vector2(tex.Width, tex.Height);
    
    // Multiply by deltaTime and divide by texture size to get UV coordinates
    var uvScroll = _waterUvScrollSet[timeOfDay] * deltaTime / texSize;
    _uvOffset += uvScroll;
    
    // Wrap around [0, 1)
    _uvOffset.X %= 1;
    _uvOffset.Y %= 1;
}
```

**Note:** UV offset is updated **every frame** and accumulated. The shader applies this offset to water texture coordinates.

---

## 7. Water-Terrain Interaction

### Terrain Integration

**Location:** [Terrain.cs](src/OpenSage.Game/Terrain/Terrain.cs#L638) - `Update()` method

```csharp
internal void Update(WaterSettings waterSettings, in TimeInterval time)
{
    RadiusCursorDecals.Update(time);
    
    // Update caustics texture index based on water settings
    _materialConstantsBuffer.Value.CausticTextureIndex = waterSettings.IsRenderCaustics
        ? GetCausticsTextureIndex(time)
        : -1;
    
    _materialConstantsBuffer.Update(_graphicsDevice);
}
```

### Interaction Types

| Interaction | Implementation | Status |
|-------------|-----------------|--------|
| **Height intersection** | `HeightMap.GetHeight(x, y)` available for queries | ✅ Available |
| **Caustics on terrain** | Texture index passed to terrain shader | ⚠️ Infrastructure only |
| **Water transparency depth** | Refraction depth map used in shader | ✅ Implemented |
| **Soft water edges** | Depth comparison in fragment shader | ✅ Implemented |
| **Object underwater rendering** | Clipping planes applied to refraction pass | ✅ Implemented |

### Caustics Rendering

**Status:** ⚠️ **Infrastructure exists but not fully implemented**

- Caustics texture index is calculated and passed to terrain shader
- Caustics toggle exists in `WaterSettings.IsRenderCaustics`
- Terrain shader receives caustics texture index
- **Missing:** Actual caustics texture cycling and terrain shader integration details

---

## 8. Architecture Decisions Already Made

### 1. **Triple Buffering for GPU Safety**

**Decision:** Use 2-frame deferred cleanup for Metal GPU compatibility

**Rationale:** Prevent use-after-free errors on Metal GPU (iOS/macOS)

**Implementation:** [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs#L28-L41)

```csharp
private readonly Queue<ResourceSet> _deferredCleanupResourceSets = new Queue<ResourceSet>();
private readonly Queue<WaterData> _deferredCleanupData = new Queue<WaterData>();
private const int DeferredCleanupFrames = 2;
```

### 2. **Separate Reflection and Refraction Passes**

**Decision:** Render reflection and refraction to separate framebuffers with different clipping planes

**Rationale:**
- Reflection uses mirrored camera below water surface
- Refraction uses normal camera clipped above water surface
- Allows accurate underwater/above-water rendering

**Implementation:** [RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs#L254)

### 3. **Per-TimeOfDay Water Configuration**

**Decision:** Store separate water sets for each time of day (day/night/etc)

**Implementation:** [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs#L57-L65)

**Storage Dicts:**
- `_waterTextureSet[TimeOfDay]`
- `_waterUvScrollSet[TimeOfDay]`
- `_waterDiffuseColorSet[TimeOfDay]`
- `_waterTransparentDiffuseColorSet[TimeOfDay]`

### 4. **Static Water Mesh (No Vertex Deformation)**

**Decision:** Water surface is flat plane defined by polygon + height. No vertex shader deformation.

**Rationale:** Simplicity and performance. Wave animation should be handled through:
- Texture scrolling (already implemented)
- Particle effects (referenced in StandingWaveArea)
- Separate wave mesh geometry (not implemented)

### 5. **Material Resource Sets as TODO**

**Decision:** Water materials don't use custom material resource sets yet

**Code:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs#L125)

```csharp
new Material(
    _shaderSet,
    _pipeline,
    null,  // <-- MaterialResourceSet is NULL
    SurfaceType.Transparent)  // TODO: MaterialResourceSet
```

**Impact:** Bump map texture is currently hardcoded to solid white; cannot be customized per water area.

---

## 9. Dependencies and Integration Points

### External Dependencies

| System | Usage | File |
|--------|-------|------|
| **Veldrid 4.9.0** | GPU graphics API abstraction | All Graphics files |
| **Triangulator** | Polygon to triangle mesh conversion | [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs#L96) |
| **Global Shader Resources** | Lighting, view-projection, viewport data | [RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs#L29) |
| **AssetStore** | Water sets, textures, transparency settings | [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs#L57) |
| **ContentManager** | Asset loading | [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs#L57) |

### Integration Points in Codebase

#### 1. **Scene3D** (Primary Integration)

**File:** [src/OpenSage.Game/Scene3D.cs](src/OpenSage.Game/Scene3D.cs)

```csharp
public WaterAreaCollection WaterAreas { get; }        // Line 68
public WaterSettings Waters { get; }                  // Line 94
Terrain?.Update(Waters, gameTime);                    // Line 384
WaterAreas.BuildRenderList(renderList);              // Line 393 in BuildRenderList()
```

**Responsibilities:**
- Owns `WaterAreaCollection` and `WaterSettings`
- Loads water areas from map file during scene creation
- Updates water each frame
- Builds render list with water areas

#### 2. **RenderPipeline** (Rendering Integration)

**File:** [src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs)

**Responsibilities:**
- Owns `WaterMapRenderer` instance
- Calls reflection/refraction pass for each water RenderItem
- Manages GPU resources (reflection/refraction maps)
- Sets water shader resource sets during rendering

**Key Methods:**
- `CalculateWaterShaderMap()` - Reflection/refraction rendering [Line 254]
- `DoRenderPass()` - Detects water bucket and calls CalculateWaterShaderMap() [Line 346]

#### 3. **Terrain** (Height Interaction)

**File:** [src/OpenSage.Game/Terrain/Terrain.cs](src/OpenSage.Game/Terrain/Terrain.cs)

**Responsibilities:**
- `Update(WaterSettings)` method receives water settings
- Updates caustics texture index based on water settings
- Can query height map for water-terrain intersection

#### 4. **Map File Loading**

**File:** [src/OpenSage.Game/Data/Map/MapFile.cs](src/OpenSage.Game/Data/Map/MapFile.cs)

**Provides:**
- `PolygonTriggers` - Water trigger areas
- `StandingWaterAreas` - SWAA chunk data
- `StandingWaveAreas` - SWAV chunk data
- `GlobalWaterSettings` - Global reflection plane settings

---

## 10. What Needs to Be Implemented

### High Priority (Core Features)

1. **Standing Wave Animation System**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs) (add wave update logic)
   - **What:** Implement wave expansion, movement, fade, compression logic
   - **Inputs:** `StandingWaveArea` parameters (FinalWidth, FinalHeight, InitialVelocity, TimeToFade, etc.)
   - **Output:** Dynamic wave mesh or animated texture coordinates
   - **Complexity:** Medium - requires wave dynamics simulation

2. **River Texture Animation ("Downstream" Flow)**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs) (differentiate river handling)
   - **What:** Rivers should scroll texture perpendicular to flow direction
   - **Current:** Treated same as static water with generic UV scroll
   - **Complexity:** Low - just needs direction detection

3. **Material Resource Sets for Water Areas**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs)
   - **What:** Store bump map texture and sky texture per water area
   - **Current:** Hardcoded to white/null
   - **Complexity:** Low - apply existing Material API

4. **Depth Colors Support**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs) and shader
   - **What:** Use `StandingWaterArea.DepthColors` for variable transparency
   - **Current:** Fixed transparency depth values in shader
   - **Complexity:** Medium - requires depth color texture and shader modification

### Medium Priority (Polish)

5. **Caustics Texture Animation**
   - **Files involved:** [Terrain.cs](src/OpenSage.Game/Terrain/Terrain.cs), terrain shader
   - **What:** Cycle caustics texture index based on time
   - **Current:** Infrastructure exists, texture cycling missing
   - **Complexity:** Low

6. **FX Shader Integration**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs)
   - **What:** Use `StandingWaterArea.FxShader` if specified
   - **Current:** Ignored (TODO comment exists)
   - **Complexity:** Medium - depends on FX shader system design

7. **Wave Particle Effects**
   - **Files to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs)
   - **What:** Spawn particle effects from `StandingWaveArea.WaveParticleFXName` (RA3+)
   - **Current:** Not used
   - **Complexity:** Low - just needs particle system integration

### Lower Priority (Optimization)

8. **Soft Edge Rendering Improvements**
   - **Comment:** [RenderPipeline.cs](src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs#L363) mentions "TODO: Fix soft edge water rendering when refraction is turned off"
   - **What:** Improve transparency edges when refraction disabled
   - **Complexity:** Low

9. **Water Culling and LOD**
   - **What:** Optimize water rendering with distance-based LOD
   - **Current:** All water areas rendered if in frustum
   - **Complexity:** Medium

---

## 11. Summary: Implementation Status

### Implemented ✅

- Water area mesh generation from polygon data
- Reflection/refraction map rendering with separate passes
- Water shader with UV scrolling texture animation
- Depth-based water transparency
- Fresnel blending of reflections and refractions
- Per-TimeOfDay water configuration
- Triple-buffered GPU resource management
- Water area loading from 3 map chunk types (PTRG, SWAA, SWAV)
- Basic caustics infrastructure

### Partially Implemented ⚠️

- Wave animation (data loaded, rendering not implemented)
- River flow animation (treated as static water)
- Material resource sets (hardcoded to null/white)
- Depth colors support (not used)
- FX shader integration (not used)
- Caustics animation (infrastructure only)

### Not Implemented ❌

- Standing wave expansion/movement/fade physics
- River directional flow texture scrolling
- Wave particle effects
- Per-water-area bump maps
- Advanced water features (foam, spray, etc.)

---

## 12. Code Examples for Reference

### Creating a Water Area

```csharp
// From WaterArea.cs - How water geometry is created

Triangulator.Triangulate(
    points,  // Vector2[] from map
    WindingOrder.CounterClockwise,
    out var trianglePoints,
    out var triangleIndices);

var vertices = trianglePoints
    .Select(x => new WaterShaderResources.WaterVertex
    {
        Position = new Vector3(x.X, x.Y, height)
    })
    .ToArray();

// Create GPU buffers
_vertexBuffer = loadContext.GraphicsDevice.CreateStaticBuffer(
    vertices, BufferUsage.VertexBuffer);
_indexBuffer = loadContext.GraphicsDevice.CreateStaticBuffer(
    triangleIndices, BufferUsage.IndexBuffer);
```

### Rendering Water in Frame

```csharp
// From RenderPipeline.cs - Water rendering flow

if (bucket.RenderItemName == "Water")
{
    CalculateWaterShaderMap(
        context.Scene3D, 
        context, 
        commandList, 
        renderItem, 
        forwardPassResourceSet);
    
    // Set resources
    SetGlobalResources(commandList, passResourceSet);
    if (_waterMapRenderer.ResourceSetForRendering != null)
    {
        commandList.SetGraphicsResourceSet(
            2, _waterMapRenderer.ResourceSetForRendering);
    }
}

// Set vertex/index buffers and draw
renderItem.BeforeRenderCallback?.Invoke(commandList, renderItem);
commandList.DrawIndexed(_numIndices, 1, 0, 0, 0);
```

---

## References

- **Map Format:** [docs/Map Format.txt](docs/Map Format.txt)
- **SAGE Engine Code:** [references/generals_code/](references/generals_code/)
- **Veldrid Documentation:** Veldrid 4.9.0 API
- **Developer Guide:** [docs/developer-guide.md](docs/developer-guide.md)
- **Coding Style:** [docs/coding-style.md](docs/coding-style.md)

