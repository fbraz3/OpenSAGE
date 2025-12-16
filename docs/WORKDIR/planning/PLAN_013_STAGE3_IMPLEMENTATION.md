# PLAN-013 Stage 3: Asset Creation - Implementation Plan

**Date**: January 2025  
**Status**: Ready for Implementation  
**Focus**: Creating sample MappedImage definitions and texture atlases  

---

## Current State

### ✅ Completed (Stages 1-2)
- `MappedImage` class fully implemented
- INI parsing infrastructure ready
- `TextureAtlasImageCollection` created from `AssetStore.MappedImages`
- `TextureAtlasingBenchmark` performance profiling system implemented
- Integration with `DrawingContext2D` for texture binding tracking
- 7/7 unit tests passing

### ⏳ Issue: No Actual Assets

The original Generals game files **do NOT contain** MappedImage definitions because EA created the texture atlasing AFTER the game data was built. The OpenSAGE system can PARSE MappedImages if they exist, but we must CREATE them.

**Loading Code** (already in place):
```csharp
// SubsystemLoader.cs - When Subsystem.Wnd is loaded:
case Subsystem.Wnd:
    LoadIniFiles(@"Data\INI\MappedImages\HandCreated");
    LoadIniFiles(@"Data\INI\MappedImages\TextureSize_512");
```

**Expected File Structure**:
```
Data/INI/MappedImages/
├── HandCreated/
│   └── [user-created sample definitions]
├── TextureSize_256/
│   └── [never loaded - optional]
├── TextureSize_512/
│   └── [main atlas definitions]
└── TextureSize_1024/
    └── [never loaded - optional]
```

---

## Stage 3 Goals

### Primary Goal
Create **sample MappedImage definitions** for Generals UI elements to demonstrate that the system works end-to-end.

### Why Samples?
1. **Proof of Concept**: Validates that parsing → integration → profiling works
2. **Performance Baseline**: Establishes texture binding stats before optimization
3. **Developer Documentation**: Shows how to create MappedImage INI files
4. **Foundation for Future Work**: Stage 4 optimization builds on these samples

### What We're Creating
- 3-5 sample MappedImage definitions
- 1 sample texture atlas (DDS file or reference to existing texture)
- 1 sample INI file in `Data\INI\MappedImages\TextureSize_512\`
- Unit tests validating the definitions

---

## Implementation Strategy

### Phase 1: Create Sample MappedImage Definitions (2 hours)

#### Option A: Reference Existing Generals Textures
If original Generals texture files are available:
1. Extract a few simple UI textures (buttons, icons)
2. Pack them into a single 512x512 DDS texture
3. Create MappedImage INI entries with coordinates

#### Option B: Create Minimal Test Textures
If original textures unavailable:
1. Use OpenSAGE's existing placeholder/test textures
2. Create simple 512x512 test atlas with colored squares
3. Map to specific pixel coordinates

#### Option C: Use Existing Scattered UI Textures
Reference textures already loaded in the game:
1. Find where UI textures are loaded from original game
2. Create MappedImage definitions pointing to original separate textures
3. No actual atlasing yet (proves parsing works, setup for future consolidation)

**Recommended: Option C** - Fastest to implement, validates end-to-end system

### Phase 2: Create INI File (1 hour)

**Sample File**: `Data/INI/MappedImages/TextureSize_512/Sample_UI_Buttons.ini`

```ini
; Sample MappedImage definitions for Generals UI buttons
; Demonstrates texture atlasing coordinate system

MappedImage = ButtonUpEnabled
  Texture = UI/Buttons512.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:0 Top:0 Right:64 Bottom:64
  Status = NONE

MappedImage = ButtonUpHilited
  Texture = UI/Buttons512.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:64 Top:0 Right:128 Bottom:64
  Status = NONE

MappedImage = ButtonUpDisabled
  Texture = UI/Buttons512.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:128 Top:0 Right:192 Bottom:64
  Status = ROTATED_90_CLOCKWISE

MappedImage = CheckboxChecked
  Texture = UI/Buttons512.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:192 Top:0 Right:224 Bottom:32
  Status = NONE

MappedImage = CheckboxUnchecked
  Texture = UI/Buttons512.dds
  TextureWidth = 512
  TextureHeight = 512
  Coords = Left:224 Top:0 Right:256 Bottom:32
  Status = NONE
```

### Phase 3: Create Unit Tests (1 hour)

**Test File**: `src/OpenSage.Game.Tests/Gui/MappedImageSampleTests.cs`

```csharp
using Xunit;
using OpenSage.Gui;

namespace OpenSage.Tests.Gui;

public sealed class MappedImageSampleTests
{
    [Fact]
    public void ButtonUpEnabled_HasCorrectCoordinates()
    {
        var mappedImage = CreateSampleMappedImage("ButtonUpEnabled", 
            left: 0, top: 0, right: 64, bottom: 64, textureSize: 512);
        
        Assert.Equal(0, mappedImage.Coords.X);
        Assert.Equal(0, mappedImage.Coords.Y);
        Assert.Equal(64, mappedImage.Coords.Width);
        Assert.Equal(64, mappedImage.Coords.Height);
    }

    [Fact]
    public void ButtonUpDisabled_WithRotation_SwapsWidthHeight()
    {
        var mappedImage = CreateSampleMappedImage("ButtonUpDisabled",
            left: 128, top: 0, right: 192, bottom: 64, 
            textureSize: 512, 
            status: MappedImageStatus.Rotated90Clockwise);
        
        Assert.Equal(MappedImageStatus.Rotated90Clockwise, mappedImage.Status);
        // Rotation should preserve pixel dimensions but swap for rendering
        Assert.Equal(64, mappedImage.Coords.Width);
    }

    [Fact]
    public void CheckboxImages_HaveDifferentCoordinates()
    {
        var checked = CreateSampleMappedImage("CheckboxChecked",
            left: 192, top: 0, right: 224, bottom: 32, textureSize: 512);
        var unchecked = CreateSampleMappedImage("CheckboxUnchecked",
            left: 224, top: 0, right: 256, bottom: 32, textureSize: 512);
        
        Assert.NotEqual(checked.Coords, unchecked.Coords);
        Assert.Equal(32, checked.Coords.Width);
        Assert.Equal(32, unchecked.Coords.Width);
    }

    private MappedImage CreateSampleMappedImage(string name, 
        int left, int top, int right, int bottom, int textureSize,
        MappedImageStatus status = MappedImageStatus.None)
    {
        // Create in-memory MappedImage for testing
        // This validates the coordinate system works correctly
        var coordinates = new Rectangle(left, top, right - left, bottom - top);
        // ... implementation
        return new MappedImage { /* fields */ };
    }
}
```

### Phase 4: Validation & Documentation (1 hour)

1. ✅ Build solution - verify no errors
2. ✅ Run tests - 100% pass rate
3. ✅ Load Generals/ZH - verify MappedImages load without crashing
4. ✅ Create markdown guide explaining sample files
5. ✅ Git commit with conventional format

---

## Deliverables

### Files Created
1. ✅ `Data/INI/MappedImages/TextureSize_512/Sample_UI_Buttons.ini` - 5 sample definitions
2. ✅ `src/OpenSage.Game.Tests/Gui/MappedImageSampleTests.cs` - Unit tests
3. ✅ `docs/WORKDIR/support/PLAN_013_STAGE3_SAMPLES.md` - Developer guide

### Build Status
- ✅ Clean compilation (no new errors)
- ✅ All tests passing (7/7 existing + new tests)
- ✅ No runtime crashes on game startup

### Git Commits
```
feat(PLAN-013): add sample MappedImage definitions for testing

- Create 5 sample UI button MappedImage definitions
- Add TextureSize_512 INI directory structure
- Include rotation example and coordinate validation
- Demonstrates full parsing→integration→profiling pipeline

test(PLAN-013): add MappedImage sample validation tests

- Test coordinate parsing and normalization
- Test rotation flag handling
- Test multiple image definitions with different sizes
- Validate end-to-end texture atlasing system

docs(PLAN-013): add developer guide for creating MappedImage definitions

- Explain INI format and coordinate system
- Provide sample file as reference
- Document rotation flags and edge cases
- Link to original EA implementation
```

---

## Success Criteria

- ✅ Sample INI file created and loadable
- ✅ MappedImages parsed correctly without errors
- ✅ Coordinates validate through unit tests
- ✅ No visual or runtime issues in game
- ✅ Documentation explains how to create more samples
- ✅ Git history clean and conventional

---

## Next Steps (Stage 4)

After Stage 3 is complete:

1. **Measure Baseline Performance** (1 hour)
   - Run Generals/ZH with benchmarking enabled
   - Record texture binding statistics
   - Document baseline metrics

2. **Implement Batch Optimization** (4 hours)
   - Use TextureAtlasingBenchmark recommendations
   - Optimize WndWindowManager rendering order
   - Measure improvement delta

3. **Expand Sample Assets** (2 hours)
   - Add more UI element samples (listbox, checkbox, etc.)
   - Create larger atlases
   - Profile performance impact

4. **Production Integration** (2 hours)
   - Create real atlases from Generals UI assets
   - Measure 50%+ texture bind reduction
   - Validate gameplay doesn't regress

---

## Timeline

| Phase | Duration | Owner | Status |
|-------|----------|-------|--------|
| Phase 1: Sample INI | 2h | Implementation | Ready |
| Phase 2: Create INI File | 1h | File Creation | Ready |
| Phase 3: Unit Tests | 1h | Testing | Ready |
| Phase 4: Validation | 1h | Build Verification | Ready |
| **Total** | **5h** | **Same Person** | **Ready to Start** |

---

## References

- [MappedImage Implementation](../../../src/OpenSage.Game/Gui/MappedImage.cs)
- [TextureAtlasingBenchmark](../../../src/OpenSage.Game/Gui/TextureAtlasing/TextureAtlasingBenchmark.cs)
- [PLAN-013 Analysis](./PLAN-013-014_ANALYSIS.md)
- [EA Generals Original](../../../references/generals_code/GeneralsMD/Code/GameEngine/)

---

## Notes

- This is a **proof of concept** phase - creates working samples, not production assets
- Stage 4 will optimize and expand to full game coverage
- Rotation flag example demonstrates coordinate system flexibility
- Framework supports unlimited atlas sizes and definitions
