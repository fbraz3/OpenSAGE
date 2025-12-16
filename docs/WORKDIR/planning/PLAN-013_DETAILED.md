# PLAN-013: Texture Atlasing for UI - Implementation Plan

**Phase**: Phase 4 (Optimization)  
**Priority**: High (improves UI rendering performance)  
**Estimated Duration**: 2-3 days  
**Acceptance Criteria**: ✅ Complete  

---

## Overview

Implement texture atlasing for UI elements, following EA's Image system architecture. This involves:
1. Creating an `Image` class to reference atlas coordinates
2. Building an `ImageCollection` to manage all UI images
3. Integrating with the existing GUI/WND rendering system
4. Creating atlases from scattered UI textures

---

## Architecture

### Image System

```csharp
// Core data structure for atlas references
public class TextureAtlasImage
{
    public string Name { get; set; }                    // "ButtonUp", "IconAttack", etc.
    public string AtlasFilename { get; set; }           // "UI/Common.dds", "UI/Icons.dds"
    public Size AtlasSize { get; set; }                 // (512, 512) - atlas page dimensions
    public Rectangle UvBounds { get; set; }             // Normalized UV coordinates (0.0-1.0)
    public Size PixelSize { get; set; }                 // Actual image dimensions in pixels
    public ImageStatus Status { get; set; }             // Rotation + flags
    
    public Region2D GetNormalizedUV() 
        => new Region2D(UvBounds.X / AtlasSize.Width, ...);
}

// Flags for image transformations
[Flags]
public enum ImageStatus
{
    None = 0,
    Rotated90Clockwise = 1,
    RawTextureData = 2
}
```

### ImageCollection

```csharp
public class TextureAtlasImageCollection
{
    private Dictionary<string, TextureAtlasImage> _images = new();
    
    public void Load(int textureSize)
    {
        // Load from Data/INI/MappedImages/TextureSize_[size]/*.ini
        // Parse image definitions and populate collection
    }
    
    public TextureAtlasImage FindByName(string name)
        => _images.TryGetValue(name.ToLowerInvariant(), out var img) ? img : null;
    
    public void AddImage(TextureAtlasImage image)
        => _images[image.Name.ToLowerInvariant()] = image;
}
```

### INI Format

```ini
; File: Data/INI/MappedImages/TextureSize_512/UI_Common.ini

Image = ButtonUpEnabled
  Texture = UI/Common.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:0 Top:0 Right:64 Bottom:64
  Status = NONE

Image = ButtonUpHilited
  Texture = UI/Common.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:64 Top:0 Right:128 Bottom:64
  Status = NONE

Image = ButtonUpDisabled
  Texture = UI/Common.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:128 Top:0 Right:192 Bottom:64
  Status = ROTATED_90_CLOCKWISE
```

---

## Implementation Checklist

### Phase 1: Core Image System (8 hours)

- [ ] **Create TextureAtlasImage class**
  - Fields: Name, AtlasFilename, AtlasSize, UvBounds, PixelSize, Status
  - Methods: GetNormalizedUV(), GetPixelCoordinates()
  - Coordinate normalization logic

- [ ] **Create ImageStatus enum**
  - Rotated90Clockwise flag
  - RawTextureData flag
  - Any rotation affects width/height swapping

- [ ] **Create TextureAtlasImageCollection class**
  - Dictionary<string, TextureAtlasImage> storage
  - FindByName(string name) with case-insensitive lookup
  - Load(int textureSize) method skeleton
  - Enumerate() for iteration

- [ ] **Create INI parsing for Image definitions**
  - Parse "Image = Name" entries
  - Parse Texture field (string)
  - Parse TextureWidth/TextureHeight (int)
  - Parse Coords field with Left/Top/Right/Bottom
  - Parse Status field with flags
  - Hook into existing IniLoader framework

- [ ] **Unit tests for coordinate transforms**
  - Test normalization: pixel coords → normalized (0.0-1.0)
  - Test rotation: 90° clockwise width/height swap
  - Test edge cases: 0-sized images, full atlas

### Phase 2: GUI Integration (8 hours)

- [ ] **Update WndWindowManager to use TextureAtlasImage**
  - Modify control rendering to fetch from ImageCollection
  - Update button rendering to use atlas coordinates
  - Update all control states (enabled, disabled, hilited)

- [ ] **Update WndWindow classes**
  - Modify each control type (Button, ListBox, Checkbox, etc.)
  - Replace direct texture references with image names
  - Render using atlas UVs instead of full texture

- [ ] **Update dirty region tracking**
  - Ensure dirty regions still work with atlased images
  - No rendering changes needed (same texture, different UVs)

- [ ] **Integration tests with existing GUI**
  - Render a window with atlased controls
  - Verify visual appearance matches original
  - Test all control states

### Phase 3: Asset Creation (6 hours)

- [ ] **Audit existing UI textures**
  - Find all scattered UI images
  - Categorize by usage (buttons, icons, backgrounds, etc.)
  - Document current texture sizes

- [ ] **Design atlas layouts**
  - Group related images together
  - Plan for 256x256, 512x512, 1024x1024 size variants
  - Account for LOD system (may need smaller atlases for low LOD)

- [ ] **Create atlas generation tool**
  - Simple packing algorithm or use existing ImagePacker reference
  - Generate INI files with Coords entries
  - Support rotation for better packing

- [ ] **Generate atlases and INI files**
  - Create Data/INI/MappedImages/TextureSize_512/ directory structure
  - Generate UI_Common.ini, UI_Buttons.ini, UI_Icons.ini, etc.
  - Create corresponding DDS texture files

### Phase 4: Optimization & Polish (4 hours)

- [ ] **Performance measurements**
  - Benchmark: draw calls before/after atlasing
  - Benchmark: texture binding changes
  - Benchmark: memory usage

- [ ] **Error handling**
  - Handle missing atlas files gracefully
  - Fall back to placeholder texture if image not found
  - Log warnings for unused images

- [ ] **Documentation**
  - Document Image INI format
  - Document how to add new images to atlases
  - Document integration points in code

- [ ] **Final testing**
  - Full UI render test in all game screens
  - Cross-platform verification (Windows, Mac, Linux)
  - Performance profiling with existing systems

---

## File Structure

```
src/OpenSage.Game/Gui/Images/
├── TextureAtlasImage.cs              [NEW]
├── ImageStatus.cs                    [NEW]
├── TextureAtlasImageCollection.cs    [NEW]
└── ImageLoader.cs                    [NEW - INI parsing]

src/OpenSage.Game/Gui/Wnd/
├── Controls/WndControl.cs            [MODIFY - use TextureAtlasImage]
├── Controls/WndButton.cs             [MODIFY]
├── Controls/WndListBox.cs            [MODIFY]
└── WndWindowManager.cs               [MODIFY - render with atlases]

Data/INI/MappedImages/
├── TextureSize_256/
│   ├── UI_Common.ini                 [NEW]
│   └── UI_Buttons.ini                [NEW]
├── TextureSize_512/
│   ├── UI_Common.ini                 [NEW]
│   └── UI_Buttons.ini                [NEW]
└── TextureSize_1024/
    ├── UI_Common.ini                 [NEW]
    └── UI_Buttons.ini                [NEW]

Textures/UI/
├── Common_256.dds                    [NEW]
├── Buttons_256.dds                   [NEW]
├── Common_512.dds                    [NEW]
├── Buttons_512.dds                   [NEW]
└── ... (all size variants)
```

---

## Integration Points

### 1. GUI Control Rendering
```csharp
// BEFORE
public void Render(DrawingContext context, Texture buttonTexture)
{
    context.DrawImage(buttonTexture, position, size);
}

// AFTER
public void Render(DrawingContext context, TextureAtlasImage atlasImage)
{
    var atlasTexture = TextureManager.GetTexture(atlasImage.AtlasFilename);
    var uvBounds = atlasImage.GetNormalizedUV();
    context.DrawImage(atlasTexture, position, size, uvBounds);
}
```

### 2. Image Loading
```csharp
// Hook into existing system
public class Game
{
    private void InitializeAssets()
    {
        var imageCollection = new TextureAtlasImageCollection();
        imageCollection.Load(512);  // Load 512x512 atlases
        AssetStore.SetImageCollection(imageCollection);
    }
}
```

### 3. INI Parsing
```csharp
// Extend existing IniLoader
[IniClass("Image")]
public class TextureAtlasImageDefinition
{
    [IniField] public string Texture { get; set; }
    [IniField] public int TextureWidth { get; set; }
    [IniField] public int TextureHeight { get; set; }
    [IniField] public string Coords { get; set; }  // Parsed as "Left:X Top:Y Right:W Bottom:H"
    [IniField] public string Status { get; set; }
}
```

---

## Performance Impact

### Expected Improvements
- **Draw call reduction**: 20-30% fewer texture bindings in UI rendering
- **Memory savings**: Single larger texture vs. many small ones
- **Cache efficiency**: Better GPU texture cache hits

### Measurements to Track
- Draw calls per frame during UI rendering
- Texture binding overhead
- Memory usage (atlases vs. scattered textures)
- Frame time impact

---

## Risk Mitigation

### Risk: Breaking existing GUI
**Mitigation**: 
- Start with simple controls (buttons)
- Add feature flag for atlas rendering
- Keep fallback to old system

### Risk: Coordinate precision issues
**Mitigation**:
- Comprehensive unit tests for normalization
- Visual regression tests
- Texture border padding to avoid sampling artifacts

### Risk: Different atlas sizes
**Mitigation**:
- Support multiple size variants (256, 512, 1024)
- LOD system selects appropriate size
- Automatic atlas resolution scaling

---

## Success Criteria

✅ All UI controls render correctly with atlased textures  
✅ No visual regression from original scattered textures  
✅ Draw call count reduced by 20-30%  
✅ INI parsing handles all image definitions correctly  
✅ Coordinates handle rotation flags properly  
✅ Unit tests: 100% pass rate  
✅ Integration tests: All GUI screens functional  
✅ Performance: +10-15% UI rendering improvement  

---

## References

- Original: `references/generals_code/GeneralsMD/Code/GameEngine/Include/GameClient/Image.h`
- Original: `references/generals_code/GeneralsMD/Code/GameEngine/Source/GameClient/System/Image.cpp`
- Original: `references/generals_code/GeneralsMD/Code/GameEngine/Include/GameClient/WinInstanceData.h`
- Related: [PLAN-013-014_ANALYSIS.md](PLAN-013-014_ANALYSIS.md)

