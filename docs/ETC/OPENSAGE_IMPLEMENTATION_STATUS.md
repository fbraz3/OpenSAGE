# OpenSAGE Implementation Status & Roadmap
**Status**: Analysis Report
**Date**: December 15, 2025
**Focus**: Map Rendering, Particle Systems, and GUI (WND) Implementation Progress

---

## Executive Summary

This document provides a comprehensive analysis of three major in-progress features listed in OpenSAGE's README:
1. **Render maps loaded from `.map`** (in progress)
2. **Render particle systems** (in progress)
3. **Render GUI loaded from `.wnd`** (in progress)

Based on deep analysis of the original Generals/Zero Hour source code and current OpenSAGE implementation, this report includes:
- Current implementation status
- Architecture comparison with original game
- Integration opportunities
- Recommended implementation roadmap
- Specific code references and examples

---

## Part 1: Map Rendering

### Original Game Architecture (C++)

#### Core Classes & Structures
- **`MapCache`**: Manages map metadata and caching
- **`TerrainVisual`**: Abstract interface for terrain rendering
- **`TerrainLogic`**: Device-independent terrain loading and management
- **`MapObject`**: Represents individual map objects (buildings, units, etc.)
- **`MapMetaData`**: Stores map size, CRC, player count, waypoints, supplies, tech positions
- **`DataChunkInput`**: Reads data from chunks in map files
- **`Image`**: Stores map preview images

#### Map Loading Pipeline
```
MapFile.Load()
  ↓
ParseMapData(BinaryReader)
  ↓
- Parse height map chunk
- Parse object list chunk
- Parse world dict chunk
- Parse preview image
  ↓
MapMetaData created with:
  - Size (width, height, border)
  - Elevation data
  - Object positions/properties
  - Waypoints
  - Supply/tech positions
  ↓
TerrainVisual->load(filename)
  (device-specific rendering initialization)
```

#### Key Data Structures
```cpp
// Map dimensions & elevation
HeightMapData {
    uint32 width, height, borderWidth
    byte[width][height] elevations
    float horizontalScale
    float verticalScale
}

// Texture blending
BlendTileData {
    List<string> textureNames
    byte[width][height] blendIndices  // Which texture per tile
}

// Objects on map
MapObject {
    string name
    Vector3 position
    float angle
    List<Property> properties
}
```

### Current OpenSAGE Implementation

#### Existing Infrastructure ✅
- **`MapFile`** class ([MapFile.cs](src/OpenSage.Game/Data/Map/MapFile.cs))
  - ✅ Parses `.map` files correctly
  - ✅ Extracts height map data
  - ✅ Extracts blend tile data
  - ✅ Extracts object list
  - ✅ Extracts lighting configuration

- **`Terrain`** class ([Terrain.cs](src/OpenSage.Game/Terrain/Terrain.cs))
  - ✅ Creates render bucket ("Terrain")
  - ✅ Generates terrain patches from height map
  - ✅ Creates tile data texture
  - ✅ Creates cliff details buffer
  - ✅ Loads and blends textures (up to 14 layers!)
  - ✅ Handles caustics animation
  - ✅ Supports macro textures

- **`HeightMap`** class ([HeightMap.cs](src/OpenSage.Game/Terrain/HeightMap.cs))
  - ✅ Stores elevation data
  - ✅ Provides height lookups
  - ✅ Handles terrain smoothing

- **`TerrainPatch`** class ([TerrainPatch.cs](src/OpenSage.Game/Terrain/TerrainPatch.cs))
  - ✅ Individual 17x17 mesh patches
  - ✅ Index buffer caching
  - ✅ Render implementation

#### Integration with Scene3D ✅
```csharp
// From Scene3D.cs
public Terrain.Terrain Terrain { get; }
public bool ShowTerrain { get; set; }  // Controls RenderBucket visibility

// Loading in Game.cs
TerrainLogic.LoadMap(mapFile, heightMap)
```

### Implementation Status: 🟢 LARGELY COMPLETE

The map rendering infrastructure is **substantially implemented** in OpenSAGE. Terrain meshes are generated, textured, and rendered correctly. The main gaps are minor enhancements.

#### What's Working
1. ✅ Height map parsing and mesh generation
2. ✅ Terrain texture loading and blending (14-layer support)
3. ✅ Caustics animation on water
4. ✅ Macro textures for detail
5. ✅ Render bucket integration

#### What Needs Attention
1. ⚠️ **Water rendering** - Basic water plane, but needs:
   - Wave animation
   - Reflection/refraction
   - Caustics interaction
   - Water transparency settings

2. ⚠️ **Road rendering** - Infrastructure exists but incomplete:
   - Road texture blending
   - Road mesh generation
   - Bridge integration

3. ⚠️ **Cliff details** - Partially implemented:
   - Cliff texture application
   - Slope angle detection

4. ⚠️ **Object placement** - Map objects parsed but needs:
   - Waypoint rendering
   - Trigger area visualization
   - Building foundation rendering

### Recommended Enhancements

#### Priority 1: Complete Water System
```csharp
// Enhance WaterSettings & WaterArea rendering
public class WaterAreaEnhanced
{
    public Vector3[] GenerateWaveVertices(float time)
    {
        // Use Gerstner waves for realistic water
        // Reference: OpenSage.Graphics.Rendering.Water
    }
    
    public void RenderReflection(RenderContext context)
    {
        // Render terrain to reflection texture
    }
}
```

**Complexity**: Medium | **Effort**: 2-3 days
**Reference**: `src/OpenSage.Game/Graphics/Rendering/Water/`

#### Priority 2: Complete Road Rendering
```csharp
// Enhance RoadCollection rendering
public class RoadCollectionEnhanced : RoadCollection
{
    public void GenerateRoadMeshes()
    {
        // For each road segment:
        // 1. Get waypoint path
        // 2. Generate quad mesh along path
        // 3. Apply road texture blend
        // 4. Add to Roads render bucket
    }
}
```

**Complexity**: Medium | **Effort**: 2-3 days
**Reference**: `src/OpenSage.Game/Terrain/Roads/`

---

## Part 2: Particle Systems

### Original Game Architecture (C++)

#### Core Classes & Hierarchy
```cpp
ParticleSystemManager
  └─ ParticleSystem[] (active systems)
       └─ Particle[] (individual particles)
            └─ ParticleInfo (properties & animation)
```

#### Emission System
- **`ParticleSystemTemplate`**: Configuration template loaded from `.ini` files
- **`FXParticleSystemInfo`**: Properties including:
  - Emission volume (box, sphere, cylinder, line, point)
  - Emission velocity (directional, radial, outward)
  - Lifetime, size, color keyframes
  - Shader type (ADDITIVE, ALPHA, ALPHA_TEST, MULTIPLY)
  - Particle type (PARTICLE, DRAWABLE, STREAK, VOLUME_PARTICLE)

#### Particle Lifecycle
```
Template defines emission → BurstDelay/BurstCount
  ↓
Emit new particles → ParticleSystemManager::createParticle()
  ↓
Initialize → position, velocity, lifetime, size, color
  ↓
Update each frame:
  - Apply velocity & gravity
  - Update size (size rate, damping)
  - Update rotation & angular velocity
  - Interpolate color/alpha via keyframes
  ↓
Render via billboard (billboarding or ground-aligned)
  ↓
Remove when lifetime expires
```

#### Rendering Features
- **Billboarding**: Particles face camera
- **Streaks**: Trail-like particles
- **Drawables**: Attach sprites/models
- **Volume particles**: 3D volumetric effects
- **Shader blending**: ADDITIVE for fire, ALPHA for smoke, MULTIPLY for shadows

### Current OpenSAGE Implementation

#### Existing Infrastructure ✅
- **`ParticleSystem`** class ([ParticleSystem.cs](src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs))
  - ✅ Full particle lifecycle management
  - ✅ Template-based instantiation
  - ✅ Emission system (bursts, delays)
  - ✅ Color & alpha keyframe interpolation
  - ✅ Physics simulation (velocity, gravity, damping)
  - ✅ Billboard rendering
  - ✅ Rotation & angular velocity
  - ✅ Vertex buffer management

- **`ParticleSystemManager`** class ([ParticleSystemManager.cs](src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs))
  - ✅ Creates & manages all active particle systems
  - ✅ Maintains render bucket for particles
  - ✅ Update loop with priority handling
  - ✅ State persistence (save/load)
  - ✅ Cleanup of finished systems

- **`Particle`** class ([Particle.cs](src/OpenSage.Game/Graphics/ParticleSystems/Particle.cs))
  - ✅ Individual particle properties
  - ✅ Animation keyframes
  - ✅ Position & velocity
  - ✅ Size & rotation
  - ✅ Dead/alive state management

- **Supporting Classes**
  - ✅ `ParticleSystemTemplate` - Stores template data
  - ✅ `FXParticleSystemTemplate` - Template inheritance
  - ✅ `ParticleColorKeyframe`, `ParticleAlphaKeyframe` - Interpolation
  - ✅ `ParticleSystemUtility` - Helper functions

#### Integration with Scene3D ✅
```csharp
// From IScene3D.cs & Scene3D.cs
internal ParticleSystemManager ParticleSystemManager { get; }

// Usage:
var particleSystem = ParticleSystemManager.Create(
    template,
    worldMatrix);
```

### Implementation Status: 🟢 LARGELY COMPLETE

The particle system infrastructure is **substantially implemented** and functional. Most core features work correctly.

#### What's Working
1. ✅ Template-based particle system creation
2. ✅ Burst emission with configurable delays
3. ✅ Color & alpha keyframe interpolation
4. ✅ Physics (velocity, damping, gravity)
5. ✅ Billboard rendering with rotation
6. ✅ Lifecycle management & cleanup
7. ✅ State persistence

#### What Needs Attention
1. ⚠️ **Emission volumes** - Only basic support:
   - Needs full box/sphere/cylinder/line implementation
   - Reference: `FXParticleEmissionVolumeBase` hierarchy

2. ⚠️ **Emission velocity types** - Partial implementation:
   - Directional, radial, outward velocities need refinement
   - Reference: `FXParticleEmissionVelocityBase` hierarchy

3. ⚠️ **Advanced particle types**:
   - ⚠️ DRAWABLE particles (attach sprites to particles)
   - ⚠️ STREAK particles (trail rendering)
   - ⚠️ VOLUME_PARTICLE (volumetric effects)
   - Current: Only basic PARTICLE type fully implemented

4. ⚠️ **Shader type support**:
   - ⚠️ ADDITIVE blending (mostly works)
   - ⚠️ MULTIPLY blending (needs implementation)
   - Current: ALPHA & ALPHA_TEST working

5. ⚠️ **Performance optimization**:
   - Max particle count limit exists but commented out
   - Need proper priority culling when limits exceeded

### Recommended Enhancements

#### Priority 1: Implement All Emission Volumes
```csharp
// Extend FXParticleEmissionVolumeBase hierarchy
public class FXParticleEmissionVolumeSphere : FXParticleEmissionVolumeBase
{
    public float Radius { get; set; }
    
    public override Ray GetRay()
    {
        // Generate random point in sphere
        var angle = Random.Range(0, 2 * PI);
        var z = Random.Range(-1, 1);
        var r = MathF.Sqrt(1 - z*z);
        var point = new Vector3(r * Cos(angle), r * Sin(angle), z) * Radius;
        return new Ray(point, point.Normalized());
    }
}
```

**Complexity**: Low | **Effort**: 1 day
**Reference**: `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs` line ~220

#### Priority 2: Implement Streak & Drawable Particle Types
```csharp
// Add to ParticleSystem.cs
public void RenderStreakParticles(CommandList commandList)
{
    // For streak particles: connect current & previous position
    // Create continuous line/ribbon from tail
}

public void RenderDrawableParticles(CommandList commandList)
{
    // Attach sprite/model to each particle
    // Render with particle's transform (position, rotation, scale)
}
```

**Complexity**: High | **Effort**: 3-4 days
**Reference**: Original game source - `ParticleType` enum handling

#### Priority 3: Implement MULTIPLY Shader Blending
```csharp
// In ParticleMaterial setup
private ShaderSet GetParticleShaderSet()
{
    // Create additional shader variants for MULTIPLY
    // Use screen-space multiply blend state
}
```

**Complexity**: Low-Medium | **Effort**: 1-2 days
**Reference**: `src/OpenSage.Game/Graphics/Shaders/ParticleShaders/`

---

## Part 3: GUI Rendering (WND Files)

### Original Game Architecture (C++)

#### Core Classes & Hierarchy
```cpp
GameWindowManager (singleton)
  └─ WindowLayout[]
       └─ GameWindow[] (hierarchy tree)
            └─ Gadget (button, slider, checkbox, etc.)
                 ├─ WinInstanceData (style, status, text)
                 └─ DrawingInfo (enabled, disabled, highlighted states)
```

#### WND File Parsing Pipeline
```
WindowLayout::load()
  ↓
Open .wnd file
  ↓
Read version & layout block
  ↓
Parse script recursively:
  - createWindow() for containers
  - createGadget() for controls (button, listbox, etc.)
  - parseRadioButtonData() for radio groups
  - parseTooltipText() for help text
  ↓
Build window tree
  ↓
Initialize callbacks (OnInit, OnUpdate, OnShutdown)
  ↓
Return WindowLayout with all windows
```

#### Window Control Types
- **StaticText**: Labels
- **PushButton**: Clickable buttons
- **RadioButton**: Mutually exclusive options
- **CheckBox**: Toggle options
- **ListBox**: Scrollable item lists
- **ComboBox**: Dropdown lists
- **Slider**: Value adjustment
- **EditBox**: Text input
- **Image**: Static images
- **Progress**: Progress bars
- **VerticalScrollBar**: Scrolling

### Current OpenSAGE Implementation

#### Existing Infrastructure ✅
- **`WndWindowManager`** class ([WndWindowManager.cs](src/OpenSage.Game/Gui/Wnd/WndWindowManager.cs))
  - ✅ Window stack management
  - ✅ Push/Pop window operations
  - ✅ Window transitions
  - ✅ Focused control tracking
  - ✅ Message box display

- **`Window`** class (Wnd/Controls/)
  - ✅ Window hierarchy
  - ✅ Control collection
  - ✅ Layout initialization callbacks
  - ✅ Size management

- **Control Classes** (Wnd/Controls/)
  - ✅ `Control` base class
  - ✅ `Button` - Click handling
  - ✅ `Label` - Text rendering
  - ✅ `ListBox` - Item lists
  - ✅ `CheckBox` - Toggle
  - ✅ `RadioButton` - Exclusive selection
  - ✅ `Slider` - Value adjustment
  - ✅ `TextBox` - Text input
  - ✅ `ImageControl` - Image display
  - ✅ `ProgressBar` - Progress indication

- **File Loading**
  - ✅ WND file parsing via `IGame.LoadWindow()`
  - ✅ Content manager integration
  - ✅ Resource caching

#### Architecture Comparison

**Original Game (C++)**:
```cpp
// Immediate mode rendering every frame
GameWindowManager::update()
  → For each WindowLayout:
      → For each GameWindow (tree traversal):
          → GameWindow::draw() → Render to screen
```

**OpenSAGE (.NET/Veldrid)**:
```csharp
// Retained mode with drawing context
Game.Render(DrawingContext2D context)
  → WndWindowManager renders all windows
  → Each Window/Control renders to context
  → Context batches and submits to GPU
```

### Implementation Status: 🟢 SUBSTANTIALLY COMPLETE

The GUI system is **functional** with most core features implemented. The architecture is well-integrated into the rendering pipeline.

#### What's Working
1. ✅ Window stack management & transitions
2. ✅ Control creation & hierarchy
3. ✅ Basic control types (button, label, listbox, etc.)
4. ✅ Event handling (click, input, focus)
5. ✅ Text rendering
6. ✅ Image rendering
7. ✅ Callback integration
8. ✅ Save/Load of window state

#### What Needs Attention
1. ⚠️ **Advanced control types**:
   - ⚠️ ListBox multi-selection modes
   - ⚠️ ComboBox dropdown behavior
   - ⚠️ TextBox text selection & editing
   - ⚠️ Slider with custom ranges

2. ⚠️ **Layout system**:
   - ⚠️ Anchor/docking system
   - ⚠️ Responsive layouts
   - ⚠️ Dynamic control creation

3. ⚠️ **Visual polish**:
   - ⚠️ Control state animations
   - ⚠️ Tooltip display & positioning
   - ⚠️ Disabled state styling
   - ⚠️ Custom control skinning

4. ⚠️ **Performance**:
   - ⚠️ Dirty region tracking
   - ⚠️ Redraw optimization
   - ⚠️ Large list handling (1000+ items)

### Recommended Enhancements

#### Priority 1: Complete ListBox Multi-Selection
```csharp
// Enhance ListBox.cs
public class ListBoxEnhanced : Control
{
    private List<int> _selectedIndices = new();
    public SelectionMode Mode { get; set; }  // Single, Multiple, Extended
    
    public event EventHandler<ListBoxSelectionChangedEventArgs> SelectionChanged;
    
    protected override void OnMouseDown(MouseEventArgs e)
    {
        var index = GetIndexAtPoint(e.Location);
        
        if (Mode == SelectionMode.Multiple && e.ModifierKeys.HasFlag(ModifierKeys.Control))
        {
            if (_selectedIndices.Contains(index))
                _selectedIndices.Remove(index);
            else
                _selectedIndices.Add(index);
        }
        else if (Mode == SelectionMode.Extended && e.ModifierKeys.HasFlag(ModifierKeys.Shift))
        {
            // Range selection
        }
        else
        {
            _selectedIndices.Clear();
            _selectedIndices.Add(index);
        }
        
        SelectionChanged?.Invoke(this, new ListBoxSelectionChangedEventArgs(_selectedIndices));
    }
}
```

**Complexity**: Medium | **Effort**: 1-2 days
**Reference**: `src/OpenSage.Game/Gui/Wnd/Controls/ListBox.cs`

#### Priority 2: Implement Dirty Region Tracking
```csharp
// Add to Window.cs
public class WindowOptimized : Window
{
    private Rectangle _dirtyRegion = Rectangle.Empty;
    
    public void InvalidateRegion(Rectangle region)
    {
        if (_dirtyRegion.IsEmpty)
            _dirtyRegion = region;
        else
            _dirtyRegion = Rectangle.Union(_dirtyRegion, region);
    }
    
    public override void Render(DrawingContext2D context)
    {
        // Only render controls within dirty region
        foreach (var control in GetVisibleControls())
        {
            if (control.Bounds.Intersects(_dirtyRegion))
            {
                control.Render(context);
            }
        }
        
        _dirtyRegion = Rectangle.Empty;
    }
}
```

**Complexity**: Medium | **Effort**: 2-3 days
**Reference**: `src/OpenSage.Game/Gui/DrawingContext2D.cs`

#### Priority 3: Implement Tooltip System
```csharp
// New: TooltipManager.cs
public class TooltipManager
{
    public string GetTooltip(Control control) => control.Tag as string;
    
    public void ShowTooltip(Control control, Point location)
    {
        // Render semi-transparent background
        // Render text with word wrapping
        // Position near cursor without going off-screen
    }
}
```

**Complexity**: Low | **Effort**: 1 day
**Reference**: Original game - tooltip positioning logic

---

## Integration Roadmap

### Phase 1: Quick Wins (Week 1)
- [ ] Complete emission volume types (particles)
- [ ] Fix road rendering visibility
- [ ] Add ListBox multi-selection support
- **Effort**: 3-4 days | **Impact**: Medium

### Phase 2: Core Features (Weeks 2-3)
- [ ] Implement streak & drawable particle types
- [ ] Complete water animation & reflection
- [ ] Add dirty region tracking (GUI performance)
- [ ] Implement tooltip system
- **Effort**: 1-2 weeks | **Impact**: High

### Phase 3: Polish (Weeks 4-5)
- [ ] Add advanced shader effects (MULTIPLY blending)
- [ ] Responsive layout system
- [ ] Particle count limiting with priority culling
- [ ] Advanced control state animations
- **Effort**: 1-2 weeks | **Impact**: Medium

### Phase 4: Optimization (Week 6+)
- [ ] Profile rendering performance
- [ ] Implement GPU-side particle sorting
- [ ] Texture atlasing for UI
- [ ] Streaming map assets
- **Effort**: Ongoing | **Impact**: Performance

---

## Code Organization & References

### Map Rendering
- Main: `src/OpenSage.Game/Terrain/`
  - `Terrain.cs` - Terrain master class
  - `TerrainPatch.cs` - Individual mesh patches
  - `HeightMap.cs` - Height data
  - `TerrainTexture.cs` - Texture management
  - `Roads/RoadCollection.cs` - Road rendering
  - `WaterArea.cs` - Water areas

- Supporting: `src/OpenSage.Game/Graphics/Rendering/`
  - `Water/` - Water effects
  - `Shadows/` - Shadow mapping

### Particle Systems
- Main: `src/OpenSage.Game/Graphics/ParticleSystems/`
  - `ParticleSystem.cs` - Main system class
  - `ParticleSystemManager.cs` - Manager & lifecycle
  - `Particle.cs` - Individual particles
  - `ParticleSystemTemplate.cs` - Template definitions
  - `ParticleMaterial.cs` - Shader material

### GUI/WND
- Main: `src/OpenSage.Game/Gui/Wnd/`
  - `WndWindowManager.cs` - Manager
  - `Controls/` - Control implementations
    - `Control.cs` - Base class
    - `Button.cs`, `Label.cs`, `ListBox.cs`, etc.
  - `Transitions/` - Window transitions

---

## Testing & Validation

### Map Rendering Test
```csharp
// Test loading and rendering a map
[Test]
public void TestMapRenderingComplete()
{
    var mapFile = MapFile.FromFileSystemEntry(entry);
    var terrain = new Terrain(mapFile, heightMap, loadContext, renderScene);
    
    Assert.IsTrue(terrain.Patches.Count > 0, "Terrain patches generated");
    Assert.IsTrue(terrain.ShowTerrain, "Terrain visibility enabled");
    // Verify texture loading, water rendering, road rendering
}
```

### Particle System Test
```csharp
// Test particle system creation and lifecycle
[Test]
public void TestParticleSystemComplete()
{
    var template = assetStore.FXParticleSystemTemplates.GetByName("SmokeSmall");
    var system = particleSystemManager.Create(template, Matrix4x4.Identity);
    
    Assert.AreEqual(ParticleSystemState.Active, system.State);
    
    // Simulate multiple frames
    for (int i = 0; i < 100; i++)
    {
        system.Update(timeInterval);
        Assert.Greater(system.CurrentParticleCount, 0);
    }
}
```

### GUI Rendering Test
```csharp
// Test window loading and control interaction
[Test]
public void TestWindowRenderingComplete()
{
    var window = game.LoadWindow("Menus/MainMenu.wnd");
    var button = window.Controls.FindControl("MainMenu:PlayButton");
    
    Assert.IsNotNull(button);
    Assert.IsTrue(button.Visible);
    
    // Simulate click event
    button.OnClick?.Invoke(button, EventArgs.Empty);
}
```

---

## Conclusion

**Overall Status**: 🟢 **LARGELY COMPLETE**

All three major features are substantially implemented in OpenSAGE with solid architectural foundations. The gaps are primarily:

1. **Maps**: Polish (water, roads, object rendering)
2. **Particles**: Advanced types (streaks, drawables, volumetric)
3. **GUI**: Optimization (dirty region tracking, tooltips)

The recommended roadmap above provides a structured path to completion with prioritized effort and expected impact metrics. Each enhancement has references to existing code and complexity/effort estimates.

**Estimated Timeline to Full Feature Completion**: 6-8 weeks with focused effort on priority items.

---

## References

### Original Game Source
- [CnC_Generals_Zero_Hour Repository](https://github.com/electronicarts/CnC_Generals_Zero_Hour)
- Terrain rendering: `GameEngine/Source/GameLogic/Map/TerrainLogic.cpp`
- Particle systems: `GameEngine/Source/GameLogic/Particle/ParticleSystem.cpp`
- GUI: `GameEngine/Source/GameEngine/Shell/GameWindowManager.cpp`

### OpenSAGE Codebase
- [OpenSAGE/OpenSAGE Repository](https://github.com/OpenSAGE/OpenSAGE)
- Main branch: Well-maintained and documented
- All referenced file paths relative to `src/`

### Further Reading
- [OpenSAGE Developer Guide](docs/developer-guide.md)
- [OpenSAGE Coding Style](docs/coding-style.md)
- [OpenSAGE Architecture](docs/architecture.md) (if available)
