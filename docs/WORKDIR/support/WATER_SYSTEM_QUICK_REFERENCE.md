# Water System Quick Reference

## File Organization

```
Water Rendering Core
├── src/OpenSage.Game/Terrain/
│   ├── WaterArea.cs                    # Water mesh and rendering
│   ├── WaterAreaCollection.cs          # Collection management
│   ├── WaterSet.cs                     # Material settings (INI)
│   ├── WaterTransparency.cs            # Global transparency
│   └── WaterTextureList.cs             # Texture asset list
│
├── src/OpenSage.Game/Graphics/Rendering/Water/
│   ├── WaterMapRenderer.cs             # Reflection/refraction orchestrator
│   ├── WaterData.cs                    # GPU resources
│   └── WaterSettings.cs                # Runtime settings
│
├── src/OpenSage.Game/Graphics/Shaders/
│   └── WaterShaderResources.cs         # Shader pipeline setup
│
├── src/OpenSage.Game/Assets/Shaders/
│   ├── Water.vert                      # Vertex shader
│   └── Water.frag                      # Fragment shader
│
└── src/OpenSage.Game/Data/Map/
    ├── StandingWaterArea.cs            # Map parser for SWAA
    ├── StandingWaveArea.cs             # Map parser for SWAV
    ├── RiverArea.cs                    # Map parser for rivers
    └── GlobalWaterSettings.cs          # Global settings
```

## Key Classes

| Class | Purpose | Key Methods |
|-------|---------|-------------|
| `WaterArea` | Individual water mesh | `CreateGeometry()`, `BuildRenderList()`, `TryCreate()` |
| `WaterAreaCollection` | All water areas | `BuildRenderList()` |
| `WaterMapRenderer` | Reflection/refraction | `RenderWaterShaders()`, `CalculateUVOffset()` |
| `WaterData` | GPU textures/framebuffers | Constructor creates reflection/refraction resources |
| `WaterSettings` | Runtime config | Properties for map sizes, distances, toggles |
| `WaterShaderResources` | Shader pipeline | Constructor creates graphics pipeline |

## Data Flow

```
Map File
  ├─ PolygonTriggers (IsWater/IsRiver)
  ├─ StandingWaterAreas (SWAA chunk)
  └─ StandingWaveAreas (SWAV chunk)
        ↓
    Scene3D (during map load)
        ↓
    WaterAreaCollection
        ├─ For each trigger/area, create WaterArea
        └─ Add to _waterAreas List
        ↓
    Frame Rendering
        ├─ Scene3D.BuildRenderList()
        ├─ WaterAreaCollection.BuildRenderList(renderList)
        └─ renderList.Water.RenderItems[] populated
        ↓
    RenderPipeline.Execute()
        ├─ For each water RenderItem
        ├─ CalculateWaterShaderMap() → Reflection/Refraction pass
        └─ Draw water mesh with shader
```

## TODO Items (In Code)

| Location | TODO | Priority |
|----------|------|----------|
| WaterArea.cs:125 | MaterialResourceSet (for bump maps) | HIGH |
| WaterArea.cs:140 | Add wave animation for standing waves | HIGH |
| WaterArea.cs:148 | Use depth colors (DepthColors field) | HIGH |
| WaterArea.cs:149 | Use FX shader if specified | MEDIUM |
| RenderPipeline.cs:363 | Fix soft edge rendering when refraction disabled | LOW |
| WaterMapRenderer.cs:105 | Get bump texture from water area | MEDIUM |

## How to Add Wave Animation

**Current State:** Wave data is loaded but animation not implemented.

**Location to modify:** [WaterArea.cs](src/OpenSage.Game/Terrain/WaterArea.cs) constructor for `StandingWaveArea`

```csharp
// Current (line 137):
private WaterArea(
    AssetLoadContext loadContext,
    StandingWaveArea area) : this(loadContext, area.Name)
{
    CreateGeometry(loadContext, area.Points, area.FinalHeight);
    //TODO: add waves  <-- HERE
}
```

**What you have available:**
```csharp
area.FinalWidth                 // Target width
area.FinalHeight                // Target height
area.InitialWidthFraction       // Starting scale
area.InitialHeightFraction      // Starting scale
area.InitialVelocity            // Expansion rate
area.TimeToFade                 // Duration in ms
area.TimeToCompress             // Compression phase duration
area.TimeOffset2ndWave          // Delay for second wave
area.DistanceFromShore          // Origin offset
area.Texture                    // Wave texture name
area.EnablePcaWave              // Physics-based flag
area.WaveParticleFXName         // Particle effect
```

**Options for implementation:**
1. **Texture animation approach:** Use existing UV scroll mechanism
2. **Mesh deformation:** Add wave vertex buffer and animate it each frame
3. **Layered meshes:** Create multiple wave meshes with different lifespans
4. **Particle effects:** Spawn particles as defined in WaveParticleFXName

## Shader Constants (Updated Each Frame)

```csharp
struct WaterConstantsPS
{
    vec2 UVOffset;                          // UV scroll offset
    float FarPlaneDistance;                 // Camera far plane
    float NearPlaneDistance;                // Camera near plane
    uint IsRenderReflection;                // Feature toggle
    uint IsRenderRefraction;                // Feature toggle
    float TransparentWaterMinOpacity;       // Min alpha
    float TransparentWaterDepth;            // Depth for alpha blending
    vec4 DiffuseColor;                      // Color tint
    vec4 TransparentDiffuseColor;           // Transparent color tint
}
```

Updated in [WaterMapRenderer.cs](src/OpenSage.Game/Graphics/Rendering/Water/WaterMapRenderer.cs):227

## Performance Settings (Scene3D.Waters)

```csharp
public WaterSettings Waters { get; } = new WaterSettings();

// Adjustable at runtime:
Waters.ReflectionMapSize = 512;              // Default 256
Waters.RefractionMapSize = 512;              // Default 256
Waters.ReflectionRenderDistance = 800;       // Default 600
Waters.RefractionRenderDistance = 2500;      // Default 2000
Waters.IsRenderReflection = true;            // Toggle on/off
Waters.IsRenderRefraction = true;            // Toggle on/off
Waters.IsRenderSoftEdge = true;              // Soft edges
Waters.IsRenderCaustics = true;              // Caustics (infrastructure only)
```

## Integration Points

| System | Integration Point | Purpose |
|--------|-------------------|---------|
| Scene3D | `WaterAreas` property | Owns all water areas |
| Scene3D | `Waters` property | Owns water settings |
| RenderPipeline | `_waterMapRenderer` | Orchestrates reflection/refraction |
| Terrain | `Update(WaterSettings)` | Receives water settings for caustics |
| RenderList | `Water` bucket | Queue for water render items |

## Debugging

```csharp
// From developer mode (F11):
// Access water areas:
scene.WaterAreas               // Get collection

// Toggle water visibility:
scene.ShowWater = false;       // Disable rendering

// Adjust quality:
scene.Waters.ReflectionMapSize = 128;  // Lower quality, faster

// Check reflection/refraction:
scene.Waters.IsRenderReflection = false;  // Disable reflections
scene.Waters.IsRenderRefraction = false;  // Disable refractions
```

## Map File Chunks

| Chunk ID | Class | When Present |
|----------|-------|--------------|
| PTRG | PolygonTriggers | Most maps (GLA01+) |
| SWAA | StandingWaterAreas | Most maps (GLA01+) |
| SWAV | StandingWaveAreas | BFME+ maps |
| ? | RiverAreas | Some maps (BFME+) |

## Vertex Structure

```csharp
struct WaterVertex
{
    Vector3 Position;  // World space (X, Y, Z)
    
    // NO other data - very simple!
    // Normal, UV, etc. computed in shader
}
```

## Texture Binding

Water shader expects these textures:
1. **WaterTexture** - Repeating water pattern (scrolled by UVOffset)
2. **BumpTexture** - Normal map for distortion (currently hardcoded to white)
3. **ReflectionMap** - Scene reflection (sky, terrain above water)
4. **RefractionMap** - Scene below water (terrain, objects)
5. **RefractionDepthMap** - Depth for soft edges

## Common Operations

### Enable/Disable Water
```csharp
scene.ShowWater = false;  // Hide all water
```

### Change Water Quality
```csharp
scene.Waters.ReflectionMapSize = 512;
scene.Waters.RefractionMapSize = 512;
```

### Toggle Reflection/Refraction
```csharp
scene.Waters.IsRenderReflection = false;
scene.Waters.IsRenderRefraction = false;
```

### Check if Water Rendering is Active
```csharp
if (scene.Waters.IsRenderReflection && scene.ReflectionMap != null)
{
    // Water reflections are being rendered
}
```

