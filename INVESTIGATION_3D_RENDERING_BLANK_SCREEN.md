# Investigation: 3D Rendering Blank Screen Issue

## Problem Statement
Both the main menu (shell map) and skirmish maps render completely blank/white, indicating a fundamental 3D rendering pipeline failure. The main menu uses the 3D engine to display units, so this is a critical rendering issue.

## Key Discovery
The issue is NOT related to WND callbacks or UI systems. The real problem is in the 3D graphics rendering pipeline.

## Architecture Overview

### Two Rendering Systems Identified
1. **RenderList System** (in RenderPipeline.cs)
   - Used by ModelMesh (game object models), WaterArea
   - Populates RenderItems with IndexBuffers
   - Calls DoRenderPass() to submit to GPU

2. **RenderScene System** (in Scene3D.cs)
   - Used by Terrain patches, Roads, ParticleSystems
   - Uses RenderBuckets to organize RenderObjects
   - Called from RenderPipeline.Execute() at line 241

### Rendering Pipeline Flow
```
RenderPipeline.Execute()
├─ Scene3D.BuildRenderList()
│  ├─ WaterAreas.BuildRenderList() → renderList.Water
│  ├─ Bridges.BuildRenderList() → renderList items
│  └─ GameObjects.BuildRenderList() → ModelMesh items to renderList
├─ DoRenderPass() for Shadow/Opaque/Transparent/Water
└─ scene.RenderScene.Render()
   └─ RenderScene.DoRenderPass(RenderPass.Forward)
      └─ RenderBucket.DoRenderPass()
         ├─ renderList.Cull()
         └─ iterate CulledObjects and call Render()
```

## Component Analysis

### Terrain Rendering Chain
1. **Terrain.__init__** (Terrain.cs:121)
   - Creates RenderBucket("Terrain") via RenderScene.CreateRenderBucket()
   - Initializes Material with TerrainShaderResources.Pipeline
   - Calls OnHeightMapChanged()

2. **Terrain.CreatePatches()** (Terrain.cs:276)
   - Creates TerrainPatch objects from heightmap data
   - Each patch has MaterialPass = new MaterialPass(material, null)
   - Adds patches to _renderBucket via AddObject()

3. **RenderBucket.AddObject()** (RenderScene.cs:26)
   - Checks if MaterialPass != null
   - Adds to _forwardPassList if ForwardPass != null
   - **POTENTIAL ISSUE**: Silently skips if MaterialPass is null

4. **TerrainPatch.Render()** (TerrainPatch.cs:143)
   - Sets vertex/index buffers
   - Calls commandList.DrawIndexed()

## Suspected Root Causes

### High Priority (Most Likely)
1. **Terrain patches not being added to RenderBucket**
   - MaterialPass could be null when AddObject() is called
   - Material creation could fail silently
   - TerrainShaderResources.Pipeline could be null

2. **RenderBucket items not being rendered**
   - Visible flag might be false
   - Material.ForwardPass could be null
   - Pipeline could be null
   - ResourceSets could be null

### Medium Priority
3. **HeightMap data issues**
   - Width/Height could be 0 or invalid
   - Elevation data not loaded properly
   - Normals not calculated

### Low Priority
4. **Veldrid graphics state issues**
   - ResourceFactory might not create buffers correctly
   - Metal backend specific issues (NRE catch block at RenderPipeline.cs:415)
   - GPU resource synchronization issues

## Code Locations

**Files to investigate:**
- `src/OpenSage.Rendering/RenderScene.cs` - RenderBucket/RenderScene architecture
- `src/OpenSage.Game/Graphics/Rendering/RenderPipeline.cs` - Main rendering orchestrator
- `src/OpenSage.Game/Terrain/Terrain.cs` - Terrain initialization
- `src/OpenSage.Game/Terrain/TerrainPatch.cs` - Terrain patch rendering
- `src/OpenSage.Game/Graphics/Shaders/TerrainShaderResources.cs` - Shader setup
- `src/OpenSage.Game/Scene3D.cs` - Scene management

## Testing Strategy

To debug this issue, add diagnostic logging at these points:
1. `RenderBucket.AddObject()` - Log when items are added and their MaterialPass status
2. `RenderBucket.DoRenderPass()` - Log CulledObjects count and visibility
3. `Terrain.CreatePatches()` - Log patch creation count
4. `Material` constructor - Verify Pipeline is not null
5. `TerrainPatch` constructor - Verify buffers are created

## Related Issues Fixed
- Added missing WND callbacks: `W3DShellMenuSchemeDraw` and `W3DClockDraw` in DefaultCallbacks.cs
  - These were causing "Failed to resolve callback" warnings but are not the root cause

## Next Steps for Resolution
1. Run game with visual debugging to confirm terrain patches are being created
2. Verify Material and Pipeline are properly initialized
3. Check if RenderBucket items are actually being added
4. Confirm that RenderScene.Render() is being called and populated with objects
5. Verify vertex/index buffers are created for terrain patches
6. Check for any silent exceptions or null reference issues
