using OpenSage.Gui.Wnd;
using OpenSage.Mathematics;
using Xunit;

namespace OpenSage.Tests.Gui.Wnd;

/// <summary>
/// Tests for DirtyRegionTracker GUI optimization system.
/// </summary>
public class DirtyRegionTrackerTests
{
    [Fact]
    public void Constructor_InitiallyNeedsFullRedraw()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();

        // Assert
        Assert.True(tracker.NeedsFullRedraw);
        Assert.True(tracker.IsDirty);
        Assert.Equal(new Rectangle(0, 0, 0, 0), tracker.DirtyRegion);
    }

    [Fact]
    public void InvalidateRect_SingleRect_SetsDirtyRegion()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        var rect = new Rectangle(10, 10, 100, 50);

        // Act
        tracker.InvalidateRect(rect);

        // Assert
        Assert.False(tracker.NeedsFullRedraw);
        Assert.True(tracker.IsDirty);
        Assert.Equal(rect, tracker.DirtyRegion);
    }

    [Fact]
    public void InvalidateRect_MultipleRects_UnionsBounds()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        var rect1 = new Rectangle(10, 10, 50, 50);
        var rect2 = new Rectangle(100, 100, 50, 50);

        // Act
        tracker.InvalidateRect(rect1);
        tracker.InvalidateRect(rect2);

        // Assert
        var dirtyRegion = tracker.DirtyRegion;
        Assert.Equal(10, dirtyRegion.Left);
        Assert.Equal(10, dirtyRegion.Top);
        Assert.Equal(150, dirtyRegion.Right);   // 100 + 50
        Assert.Equal(150, dirtyRegion.Bottom);  // 100 + 50
    }

    [Fact]
    public void InvalidateRect_EmptyRect_Ignored()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        var emptyRect = new Rectangle(10, 10, 0, 0);

        // Act
        tracker.InvalidateRect(emptyRect);

        // Assert
        Assert.False(tracker.IsDirty);
        Assert.Equal(new Rectangle(0, 0, 0, 0), tracker.DirtyRegion);
    }

    [Fact]
    public void InvalidateFull_SetsNeedsFullRedraw()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));
        Assert.False(tracker.NeedsFullRedraw);

        // Act
        tracker.InvalidateFull();

        // Assert
        Assert.True(tracker.NeedsFullRedraw);
        Assert.True(tracker.IsDirty);
    }

    [Fact]
    public void Clear_RemovesDirtyState()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));
        Assert.True(tracker.IsDirty);

        // Act
        tracker.Clear();

        // Assert
        Assert.False(tracker.IsDirty);
        Assert.False(tracker.NeedsFullRedraw);
        Assert.Equal(new Rectangle(0, 0, 0, 0), tracker.DirtyRegion);
    }

    [Fact]
    public void ClearAfterFullRedraw_ClearsAndResetsBoth()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));

        // Act
        tracker.ClearAfterFullRedraw();

        // Assert
        Assert.False(tracker.IsDirty);
        Assert.False(tracker.NeedsFullRedraw);
        Assert.Equal(new Rectangle(0, 0, 0, 0), tracker.DirtyRegion);
    }

    [Fact]
    public void IntersectsDirtyRegion_WithFullRedraw_ReturnsTrue()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        var rect = new Rectangle(50, 50, 100, 100);

        // Act
        var intersects = tracker.IntersectsDirtyRegion(rect);

        // Assert
        Assert.True(intersects);
    }

    [Fact]
    public void IntersectsDirtyRegion_IntersectingRect_ReturnsTrue()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));
        var testRect = new Rectangle(40, 40, 50, 50);  // Overlaps with dirty region

        // Act
        var intersects = tracker.IntersectsDirtyRegion(testRect);

        // Assert
        Assert.True(intersects);
    }

    [Fact]
    public void IntersectsDirtyRegion_NonIntersectingRect_ReturnsFalse()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));
        var testRect = new Rectangle(200, 200, 50, 50);  // Does not overlap

        // Act
        var intersects = tracker.IntersectsDirtyRegion(testRect);

        // Assert
        Assert.False(intersects);
    }

    [Fact]
    public void GetScissorRect_WithFullRedraw_ReturnsEmpty()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();

        // Act
        var scissor = tracker.GetScissorRect();

        // Assert
        Assert.Equal(new Rectangle(0, 0, 0, 0), scissor);
    }

    [Fact]
    public void GetScissorRect_WithPartialDirty_ReturnsDirtyRegion()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        var dirtyRect = new Rectangle(50, 50, 100, 100);
        tracker.InvalidateRect(dirtyRect);

        // Act
        var scissor = tracker.GetScissorRect();

        // Assert
        Assert.Equal(dirtyRect, scissor);
    }

    [Fact]
    public void Reset_RestoresToInitialState()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();

        // Act
        tracker.Reset();

        // Assert
        Assert.True(tracker.NeedsFullRedraw);
        Assert.True(tracker.IsDirty);
        Assert.Equal(new Rectangle(0, 0, 0, 0), tracker.DirtyRegion);
    }

    [Fact]
    public void InvalidateRect_OverlappingRects_UnionCorrect()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        var rect1 = new Rectangle(10, 10, 100, 100);
        var rect2 = new Rectangle(50, 50, 100, 100);

        // Act
        tracker.InvalidateRect(rect1);
        tracker.InvalidateRect(rect2);

        // Assert
        var dirtyRegion = tracker.DirtyRegion;
        Assert.Equal(10, dirtyRegion.Left);
        Assert.Equal(10, dirtyRegion.Top);
        Assert.Equal(150, dirtyRegion.Right);   // Max(110, 150)
        Assert.Equal(150, dirtyRegion.Bottom);  // Max(110, 150)
    }

    [Fact]
    public void IntersectsDirtyRegion_TouchingEdges_ReturnsTrue()
    {
        // Arrange
        var tracker = new DirtyRegionTracker();
        tracker.Clear();
        tracker.InvalidateRect(new Rectangle(10, 10, 50, 50));
        var touchingRect = new Rectangle(60, 10, 50, 50);  // Touches right edge

        // Act
        var intersects = tracker.IntersectsDirtyRegion(touchingRect);

        // Assert
        Assert.False(intersects);  // Touching edges don't intersect
    }
}
