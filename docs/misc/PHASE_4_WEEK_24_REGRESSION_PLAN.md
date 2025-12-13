# PHASE 4 WEEK 24 - REGRESSION TESTING PLAN

**Status**: 🔄 PLANNING PHASE - Week 23 Complete
**Focus**: Visual Output Validation & Regression Detection
**Estimated Duration**: 5 working days (Monday-Friday)

---

## Week 24 Objectives

### Primary Goal: Regression Testing with Actual Rendering

Verify that graphics abstraction layer integration (Weeks 20-23) does not cause:
- Visual regression in game rendering
- Performance degradation
- Color/texture mismatches
- Lighting or shadow errors
- UI rendering issues

### Success Criteria

- [X] Baseline rendering images captured (map, UI, effects)
- [X] Integration tests with actual Veldrid device created
- [X] Visual comparison testing framework operational
- [X] Performance baseline established
- [X] Zero regressions detected
- [X] Documentation of findings

---

## Week 24 Implementation Plan

### Phase 1: Integration Test Framework (Days 1-2)

**Goal**: Create real graphics device integration tests

**Tasks**:
1. Create `Week24RegressionTests.cs` with integration test infrastructure
   - Extend MockedGameTest with real GraphicsDevice initialization
   - Create helper for running game logic with rendering
   - Set up frame capture mechanism

2. Create rendering helper classes:
   - `RenderingTestHelper.cs` - Frame capture, image saving
   - `RenderingComparator.cs` - Visual comparison logic
   - `ReferenceImageManager.cs` - Reference image management

3. Establish test patterns:
   - Pattern: Create game instance → Run game logic → Capture frame → Compare
   - Handle: Window creation, GPU context, resource cleanup
   - Error handling: Graceful degradation for headless environments

**Expected Output**:
- Integration test infrastructure ready
- Frame capture working
- Baseline reference images created

### Phase 2: Baseline Image Capture (Days 2-3)

**Goal**: Establish visual reference for regression detection

**Tests to Create**:

1. **MapRenderingTest** - Terrain and objects
   - Load map: Army_vs_Army (standard test map)
   - Render: 10 frames to stabilize
   - Capture: Single frame output
   - Save: As reference image `maps/army_vs_army_render_baseline.png`

2. **UIRenderingTest** - User interface
   - Load main menu
   - Render: UI system (10 frames)
   - Capture: Screenshot of menu
   - Save: As reference image `ui/main_menu_baseline.png`

3. **EffectsRenderingTest** - Visual effects
   - Load map with effects (gunfire, explosions)
   - Trigger: Visual effect events
   - Render: 5 frames
   - Capture: Effect frames
   - Save: Reference images for each effect

4. **LightingTest** - Shadows and lighting
   - Load: Map with varied lighting
   - Render: 10 frames
   - Verify: Shadow map generation
   - Capture: Lighting output
   - Save: Lighting baseline

5. **ParticleSystemTest** - Particle effects
   - Create: Particle systems (dust, smoke, water)
   - Render: 10 frames
   - Capture: Particle rendering
   - Save: Particle reference images

**Expected Output**:
- 5+ reference baseline images
- Performance metrics baseline
- Memory usage baseline

### Phase 3: Regression Test Implementation (Days 3-4)

**Goal**: Automated visual comparison

**Tests to Create**:

1. **RegressionComparison_MapRendering**
   - Render current: Army_vs_Army map
   - Compare: Against baseline image
   - Threshold: 99% pixel match (allow 1% tolerance for timing variations)
   - Assert: Pass if match within threshold

2. **RegressionComparison_UIRendering**
   - Render current: Main menu UI
   - Compare: Against baseline
   - Threshold: 99.5% match (UI more deterministic)
   - Assert: Pass if match

3. **RegressionComparison_EffectsRendering**
   - Render: Visual effects
   - Compare: Against captured baseline
   - Threshold: 98% match (effects can vary slightly)
   - Assert: Pass if within threshold

4. **PerformanceRegression_FrameTime**
   - Measure: Average frame time over 100 frames
   - Baseline: Current performance
   - Threshold: < 10% degradation
   - Assert: Average frame time acceptable

5. **PerformanceRegression_Memory**
   - Measure: Memory usage during rendering
   - Baseline: Current usage
   - Threshold: < 15% increase
   - Assert: Memory usage acceptable

**Implementation Pattern**:

```csharp
[Fact]
public void RegressionTest_MapRendering_NoVisualChanges()
{
    // ARRANGE
    var game = LoadGameWithMap("maps/army_vs_army.map");
    var referenceImage = LoadReferenceImage("maps/army_vs_army_render_baseline.png");
    
    // ACT
    game.Update(100, 0);  // Run 100 frames
    var currentFrame = game.CaptureFrame();
    
    // ASSERT
    var similarity = ImageComparator.CompareSimilarity(referenceImage, currentFrame);
    Assert.True(similarity > 0.99, $"Expected >99% match, got {similarity:P}");
}
```

**Expected Output**:
- 5 automated regression tests
- Visual diff reports
- Performance metrics logged

### Phase 4: Documentation & Analysis (Days 4-5)

**Goal**: Report findings and document process

**Deliverables**:

1. **Regression Test Report**
   - Summary: All tests passed / Failed
   - Visual comparisons: Side-by-side images
   - Performance metrics: Baselines vs current
   - Any regressions found: Details and analysis

2. **Performance Analysis**
   - Frame time: Average, min, max
   - Memory usage: Peak, average
   - GPU utilization: If measurable
   - CPU load: If measurable

3. **Test Infrastructure Documentation**
   - How to run regression tests
   - How to capture new baselines
   - How to add new regression tests
   - Troubleshooting guide

4. **Integration Recommendations**
   - CI/CD integration steps
   - Baseline update procedure
   - False positive handling
   - Performance threshold adjustments

**Expected Output**:
- Comprehensive regression test report
- Performance baseline documented
- Test infrastructure guide
- Ready for CI/CD integration

---

## Technical Architecture

### Integration Test Framework

```csharp
public class Week24RegressionTests : IDisposable
{
    private Game _game;
    private GraphicsDevice _graphicsDevice;
    private Texture2D _frameBuffer;
    
    public void Setup(string mapFileName)
    {
        // Create real graphics device with window
        // Load game with specified map
        // Initialize rendering pipeline
    }
    
    public Texture2D CaptureFrame()
    {
        // Render one frame
        // Read GPU texture to CPU
        // Return captured frame
    }
    
    public void SaveFrameAsImage(Texture2D frame, string filePath)
    {
        // Convert texture to bitmap
        // Save to PNG file
    }
}
```

### Image Comparison Algorithm

```csharp
public class ImageComparator
{
    public static double CompareSimilarity(Bitmap reference, Bitmap current)
    {
        // Compare pixel-by-pixel
        // Account for minor rendering variations (anti-aliasing, dithering)
        // Return similarity percentage (0-1)
        // Typical: 99%+ for deterministic rendering
    }
    
    public static Bitmap CreateDiffImage(Bitmap reference, Bitmap current)
    {
        // Highlight differences in red
        // For visual inspection
        // Save diff for investigation
    }
}
```

### Performance Measurement

```csharp
public class PerformanceMonitor
{
    private Stopwatch _frameTimer;
    private List<long> _frameTimings;
    
    public void StartFrame()
    {
        _frameTimer.Restart();
    }
    
    public void EndFrame()
    {
        _frameTimings.Add(_frameTimer.ElapsedMilliseconds);
    }
    
    public PerformanceMetrics GetMetrics()
    {
        return new PerformanceMetrics
        {
            AverageFrameTime = _frameTimings.Average(),
            MinFrameTime = _frameTimings.Min(),
            MaxFrameTime = _frameTimings.Max(),
            FramesPerSecond = 1000.0 / _frameTimings.Average()
        };
    }
}
```

---

## Risk Mitigation

### Headless Rendering Challenge

**Problem**: Tests may run in headless CI/CD environment without display

**Solutions**:
1. Use SwiftShader or Mesa3D for software rendering fallback
2. Skip visual comparison in headless environment
3. Log performance metrics (CPU-side timing still possible)
4. Mark as optional/skip in CI if necessary

### Image Comparison Noise

**Problem**: Minor rendering variations (anti-aliasing, precision) can cause false failures

**Solutions**:
1. Use perceptual image comparison (not pixel-perfect)
2. Allow 1% pixel threshold (99% match required)
3. Blur images slightly before comparison
4. Focus on major visual elements

### Performance Variance

**Problem**: Performance metrics vary between runs

**Solutions**:
1. Use averages over many frames (100+)
2. Set realistic thresholds (±15% acceptable)
3. Run multiple times, keep best result
4. Account for system load variations

---

## Success Metrics

| Metric | Target | Success |
|--------|--------|---------|
| **Test Pass Rate** | 100% | All regression tests pass |
| **Visual Match %** | >99% | Reference vs current match |
| **Performance Regression** | <10% | No significant slowdown |
| **Memory Overhead** | <15% | Reasonable memory usage |
| **Tests Created** | 5+ | Comprehensive coverage |
| **Documentation** | Complete | Full implementation guide |

---

## Files to Create/Modify

### New Files
- `Week24RegressionTests.cs` - Integration test suite
- `RenderingTestHelper.cs` - Frame capture utilities
- `RenderingComparator.cs` - Image comparison logic
- `PerformanceMonitor.cs` - Performance measurement
- `ReferenceImages/` - Baseline image directory

### Reference Images (to be captured)
- `maps/army_vs_army_render_baseline.png`
- `ui/main_menu_baseline.png`
- `effects/gunfire_baseline.png`
- `effects/explosion_baseline.png`
- `lighting/shadow_map_baseline.png`

### Documentation
- `PHASE_4_WEEK_24_REGRESSION_REPORT.md` - Final report
- `REGRESSION_TEST_GUIDE.md` - How-to guide
- `PERFORMANCE_BASELINE.md` - Performance metrics

---

## Day-by-Day Breakdown

### Day 1 (Monday): Infrastructure Setup
- Create integration test framework
- Implement frame capture
- Get first baseline image captured
- Commit infrastructure code

### Day 2 (Tuesday): Baseline Capture
- Capture remaining baseline images (4+)
- Create reference image directory
- Document baseline capture process
- Initial performance measurements

### Day 3 (Wednesday): Regression Tests
- Implement 5 regression comparison tests
- All tests running and passing
- Performance monitoring integrated
- Commit regression test suite

### Day 4 (Thursday): Validation & Analysis
- Run all regression tests multiple times
- Analyze any variations
- Create performance report
- Generate visual diff images (if needed)

### Day 5 (Friday): Documentation & Finalization
- Complete regression test report
- Document test infrastructure
- Create CI/CD integration guide
- Final commit and review

---

## Next Steps After Week 24

### Week 25: Performance Testing (Detailed)
- CPU profiling
- GPU profiling
- Memory allocation analysis
- Frame time variance analysis

### Week 26: Documentation
- API documentation
- Troubleshooting guide
- Migration guide to abstraction layer

### Week 27: Release Preparation
- Feature flags implementation
- Rollback procedures
- Release checklist

---

## Current Phase 4 Status

| Week | Focus | Status |
|------|-------|--------|
| 20 | Graphics Abstraction Layer | ✅ COMPLETE |
| 21 | Shader & Pipeline Operations | ✅ COMPLETE |
| 22 | Tool Integration | ✅ COMPLETE |
| 23 | Functional Testing Framework | ✅ COMPLETE |
| 24 | Regression Testing | 🔄 READY TO START |
| 25 | Performance Testing | ⏳ PLANNED |
| 26 | Documentation | ⏳ PLANNED |
| 27 | Release Preparation | ⏳ PLANNED |

---

## Build Status

**Current**: ✅ CLEAN (0 ERRORS)
- All tests passing: 10/10 (Week 23)
- Full solution: 0 errors verified
- Ready for Week 24 integration testing

---

**Planning Complete**  
**Date**: Current Session  
**Status**: Ready for Week 24 Implementation  
**Next Action**: Begin Day 1 of Week 24 on next session

