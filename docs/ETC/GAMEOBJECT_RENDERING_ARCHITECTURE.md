# GameObject Rendering Architecture in OpenSAGE

## Overview

This document describes how map objects (GameObjects) are rendered in OpenSAGE, including the integration between the game logic layer and the rendering layer.

## Architecture Diagram

```text
Game Loop (Game.cs @ 60 Hz)
    ↓
RenderPipeline.Render() 
    ↓
Scene3D.BuildRenderList(RenderList, Camera, TimeInterval)
    ├─→ For each GameObject in GameObjects.Objects:
    │   ├─→ Frustum culling check
    │   └─→ gameObject.BuildRenderList(RenderList, Camera, TimeInterval)
    │       ├─→ Drawable.BuildRenderList(...)
    │       │   ├─→ For each DrawModule in Drawable.DrawModules:
    │       │   │   └─→ drawModule.BuildRenderList(...)
    │       │   │       └─→ W3dModelDraw.BuildRenderList(...)
    │       │   │           └─→ ModelInstance.BuildRenderList(...)
    │       │   │               └─→ For each ModelMesh in Model.SubObjects:
    │       │   │                   └─→ ModelMesh.BuildRenderList(...) [Creates RenderItems]
    │       │   │
    │       │   └─→ RenderList.Opaque/Transparent.RenderItems.Add(RenderItem)
    │       │
    │       └─→ Visibility checks: Sold condition, Hidden flag
    │
    ├─→ WaterAreas.BuildRenderList(RenderList)
    ├─→ Bridges.BuildRenderList(RenderList)
    └─→ OrderGeneratorSystem.BuildRenderList(RenderList)
```

## Key Components

### 1. GameObjects and Rendering

**File**: [src/OpenSage.Game/Logic/Object/GameObject.cs](src/OpenSage.Game/Logic/Object/GameObject.cs)

- **Field**: `public readonly Drawable Drawable` (line 449)
- **Method**: `internal void BuildRenderList(RenderList renderList, Camera camera, in TimeInterval gameTime)` (line 1091)
  - Visibility check: `if (ModelConditionFlags.Get(ModelConditionFlag.Sold) || Hidden) { return; }`
  - Already respects the `Hidden` property for visibility control
  - Creates `MeshShaderResources.RenderItemConstantsPS` with:
    - `HouseColor` from Owner
    - `Opacity` (1.0 or 0.7 for placement preview)
    - `TintColor` (red for invalid placement)
  - Delegates to `Drawable.BuildRenderList(...)`

### 2. Scene3D and BuildRenderList Orchestration

**File**: [src/OpenSage.Game/Scene3D.cs](src/OpenSage.Game/Scene3D.cs)

- **Property**: `public IGameObjectCollection GameObjects => GameEngine.GameLogic;`
- **Property**: `public RenderScene RenderScene { get; }` (initialized as empty in constructor)
- **Method**: `public void BuildRenderList(RenderList renderList, Camera camera, in TimeInterval gameTime)` (line 389)
  - Iterates through all GameObjects
  - Applies frustum culling if enabled: `gameObject.RoughCollider.Intersects(Camera.BoundingFrustum)`
  - Respects `ShowObjects` property (default true) to toggle rendering
  - Calls `gameObject.BuildRenderList(...)` for each visible object
  - Also processes WaterAreas, Bridges, and OrderGenerator

### 3. Drawable and DrawModules

**File**: [src/OpenSage.Game/Client/Drawable.cs](src/OpenSage.Game/Client/Drawable.cs)

- **Property**: `public readonly GameObject GameObject`
- **Property**: `public bool IsVisible` (line 86)
  - Returns true if ANY DrawModule is visible
  - Loops through `_drawModules` checking `drawModule.IsVisible`
- **Method**: `internal void BuildRenderList(...)` (line 353)
  - Skips hidden draw modules: `if (_hiddenDrawModules.Contains(drawModule.Tag)) continue;`
  - Calls `drawModule.BuildRenderList(...)` for each visible DrawModule
  - Already has infrastructure for hidden subobjects via `_hiddenSubObjects` dictionary

### 4. DrawModule Hierarchy

**Base Class**: [src/OpenSage.Game/Logic/Object/Draw/DrawModule.cs](src/OpenSage.Game/Logic/Object/Draw/DrawModule.cs)

- **Abstract Method**: `internal abstract void BuildRenderList(...)`
- **Property**: `public virtual bool IsVisible => true`
- **Property**: `public Drawable? Drawable`

**Main Implementation**: `W3dModelDraw` ([src/OpenSage.Game/Logic/Object/Draw/W3dModelDraw.cs](src/OpenSage.Game/Logic/Object/Draw/W3dModelDraw.cs))

- Implements `BuildRenderList` that delegates to `ModelInstance.BuildRenderList(...)`

### 5. ModelInstance and RenderItem Creation

**File**: [src/OpenSage.Game/Graphics/ModelInstance.cs](src/OpenSage.Game/Graphics/ModelInstance.cs)

- **Method**: `internal void BuildRenderList(...)` (line 206)
  - Iterates through all Model.SubObjects (mesh parts)
  - Calls `subObject.RenderObject.BuildRenderList(...)`

### 6. ModelMesh - Where RenderItems Are Created

**File**: [src/OpenSage.Game/Graphics/ModelMesh.cs](src/OpenSage.Game/Graphics/ModelMesh.cs)

- **Method**: `internal override void BuildRenderListWithWorldMatrix(...)` (line 66)
  - Visibility check: `if (Hidden || !modelInstance.BoneFrameVisibilities[parentBone.Index]) { return; }`
  - Creates `RenderItem` objects for each mesh part
  - Adds to either `renderList.Shadow`, `renderList.Opaque`, or `renderList.Transparent`
  - Each RenderItem contains:
    - Name, Material pass info
    - World matrix
    - Bounding box
    - Index buffer and range
    - Render callback

## RenderScene and RenderBuckets

**File**: [src/OpenSage.Rendering/RenderScene.cs](src/OpenSage.Rendering/RenderScene.cs)

### Current State

The RenderScene exists but is **NOT currently used** for GameObject rendering. Instead:

- GameObjects use the inline `RenderList` structure with Opaque/Transparent/Shadow buckets
- Each system (Terrain, Roads, Particles, WaterAreas) creates custom `RenderBucket` objects
- These buckets are stored separately and used during rendering passes

### RenderBucket Infrastructure

- **Method**: `RenderBucket.AddObject(RenderObject)`
  - Maintains separate forward and shadow pass lists
  - Expects `RenderObject` interface implementation

- **Method**: `RenderBucket.DoRenderPass(...)`
  - Culls objects
  - Renders each RenderObject
  - Updates GPU resource sets

### How Other Systems Use RenderBuckets

Terrain ([src/OpenSage.Game/Terrain/Terrain.cs](src/OpenSage.Game/Terrain/Terrain.cs#L66)):

```csharp
_renderBucket = scene.CreateRenderBucket("Terrain", 0);
```

Roads ([src/OpenSage.Game/Terrain/Roads/RoadCollection.cs](src/OpenSage.Game/Terrain/Roads/RoadCollection.cs#L45)):

```csharp
var renderBucket = scene.CreateRenderBucket("Roads", 10);
```

Particles ([src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs](src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs#L29)):

```csharp
_renderBucket = scene.RenderScene.CreateRenderBucket("Particles", 15);
```

## Visibility Control - Current State

### GameObject Level

- **Property**: `GameObject.Hidden` (line 353)
  - Used in BuildRenderList check: prevents rendering entirely
  - Used in game logic for deletion, invisibility, etc.

### Drawable Level

- **Method**: `Drawable.HideDrawModule(string module)` / `ShowDrawModule(...)`
  - Manages `_hiddenDrawModules` list
  - Skipped during BuildRenderList

### SubObject Level

- **Method**: `Drawable.HideSubObject(string subObject)` / `ShowSubObject(...)`
  - Manages `_hiddenSubObjects` dictionary
  - Passed to DrawModule.BuildRenderList as parameter

### Model Instance Level

- **Field**: `BoneFrameVisibilities[boneIndex]` - controls per-bone visibility

## Integration Flow for Waypoints/Visibility Features

### Where to Add Waypoint/Visibility Visualization

#### Option 1: Use existing GameObject infrastructure (RECOMMENDED)

- Create a special `WaypointMarker` or `DebugGeometry` GameObject
- Add a `DebugVisualizationDrawModule` that renders waypoint/path geometry
- Uses existing BuildRenderList infrastructure
- Respects `ShowObjects` property

#### Option 2: Extend RenderScene with debug bucket

- Create a "Waypoints" RenderBucket in Scene3D
- Add RenderObjects for waypoint markers and paths
- Would require implementing RenderObject interface

#### Option 3: Add to Scene25D (2D overlay)

- [src/OpenSage.Game/Scene25D.cs](src/OpenSage.Game/Scene25D.cs) already handles 2D rendering
- Could draw waypoint indicators as 2D elements

### Visibility Properties Already Available

- `Scene3D.ShowObjects` - toggle all object rendering
- `Scene3D.ShowTerrain` - uses RenderBucket.Visible
- `Scene3D.ShowWater` - conditional flag
- `Scene3D.ShowRoads` - uses RenderBucket.Visible
- `Scene3D.ShowBridges` - conditional flag

## Summary

### 1. Where RenderObject Instances Come From

- **RenderItems are created in**: `ModelMesh.BuildRenderListWithWorldMatrix()`
- **Not actual RenderObject interface implementations** - instead `RenderItem` structs added to `RenderList.Opaque/Transparent`
- The RenderScene's RenderBucket/RenderObject pattern exists but is currently used only for Terrain, Roads, Particles

### 2. Integration Point Between GameObjects and RenderScene

- **Primary path**: Scene3D → GameObjects → Drawable → DrawModules → ModelInstance → ModelMesh → RenderItems
- **RenderScene is separate**: Used only for non-GameObject entities (Terrain, Roads, etc.)
- **RenderList is immediate**: Render items added directly to RenderList buckets during BuildRenderList call

### 3. Object Rendering Status

- **Already fully implemented**: GameObjects render correctly through DrawModules
- **Already have visibility control**:
  - GameObject.Hidden flag
  - Scene3D.ShowObjects property
  - Frustum culling support
  - Per-DrawModule hide/show
  - Per-SubObject hide/show
  - Per-bone visibility control

### 4. What Needs to Be Added for Visibility/Waypoint Support

#### For Waypoint Display

1. **Create debug visualization GameObjects** that appear in a "debug" layer
   - These could use a special `DebugVisualizationDrawModule` or similar
   - Or create marker objects in a separate collection

2. **Add Scene3D properties** to control debug visibility:
   - `ShowWaypoints` property
   - `ShowWaypointConnections` property
   - Could use existing `DebugOverlay` system

3. **Add waypoint rendering** either:
   - Via GameObject + custom DrawModule
   - Via RenderScene debug bucket
   - Via Scene25D overlay rendering

#### For Selection Visibility Toggle

1. **Leverage existing infrastructure**:
   - Use `GameObject.Hidden` to hide selected objects
   - Use `Drawable.HideDrawModule()` to hide specific draw modules
   - Use `Scene3D.ShowObjects` to toggle all objects

2. **Add UI integration** in debug/developer mode:
   - Add toggle buttons for visibility controls
   - Add property inspector for GameObject.Hidden
   - Add waypoint marker creation

### 5. Recommended Next Steps

1. **For waypoint visualization**: Create a `WaypointVisualization` system that adds temporary debug GameObjects
2. **For visibility UI**: Add developer mode toggles using existing `DebugOverlay` or new debug UI panels
3. **For performance**: Consider creating debug-only RenderBucket separate from gameplay rendering
4. **For integration**: All visibility controls should respect developer mode to avoid runtime overhead

## Architecture Notes

- The codebase has **two separate rendering systems**:
  1. **RenderList** (inline): Used by GameObjects, immediate-mode rendering
  2. **RenderScene/RenderBucket**: Used by static/semi-static geometry (Terrain, Roads, etc.)

- **No object pooling** for RenderItems - created fresh each frame in BuildRenderList

- **Visibility already layered**:
  - Scene level: ShowObjects, ShowTerrain, etc.
  - Object level: GameObject.Hidden, ModelConditionFlag.Sold
  - Module level: HideDrawModule, HideSubObject
  - Bone level: BoneFrameVisibilities

- **Frustum culling** is optional (Scene3D.FrustumCulling property) and per-object
