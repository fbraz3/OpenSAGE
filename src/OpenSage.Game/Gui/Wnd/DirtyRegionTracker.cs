using System;
using OpenSage.Mathematics;

namespace OpenSage.Gui.Wnd;

/// <summary>
/// Tracks dirty regions in GUI for optimized rendering.
/// Maintains a single bounding rectangle of all invalidated areas.
/// </summary>
public class DirtyRegionTracker
{
    /// <summary>
    /// The accumulated dirty region (zero-size rectangle if no invalidation).
    /// </summary>
    private Rectangle _dirtyRegion = new(0, 0, 0, 0);

    /// <summary>
    /// Flag indicating full window redraw is needed.
    /// </summary>
    private bool _needsFullRedraw = true;

    /// <summary>
    /// Gets the current dirty region rectangle.
    /// </summary>
    public Rectangle DirtyRegion => _dirtyRegion;

    /// <summary>
    /// Gets whether a full redraw is needed.
    /// </summary>
    public bool NeedsFullRedraw => _needsFullRedraw;

    /// <summary>
    /// Gets whether any redraw is needed (full or partial).
    /// </summary>
    public bool IsDirty => _needsFullRedraw || (Width(_dirtyRegion) > 0 && Height(_dirtyRegion) > 0);

    /// <summary>
    /// Marks the entire window as needing redraw.
    /// </summary>
    public void InvalidateFull()
    {
        _needsFullRedraw = true;
        _dirtyRegion = new(0, 0, 0, 0);
    }

    /// <summary>
    /// Marks a specific rectangular region as dirty.
    /// Accumulates with existing dirty region using union.
    /// </summary>
    /// <param name="rect">Rectangle to invalidate (ignored if zero-sized)</param>
    public void InvalidateRect(Rectangle rect)
    {
        if (Width(rect) <= 0 || Height(rect) <= 0)
        {
            return;
        }

        if (Width(_dirtyRegion) <= 0 || Height(_dirtyRegion) <= 0)
        {
            _dirtyRegion = rect;
        }
        else
        {
            _dirtyRegion = Union(_dirtyRegion, rect);
        }
    }

    /// <summary>
    /// Clears the dirty region after rendering.
    /// Resets to clean state until next invalidation.
    /// </summary>
    public void Clear()
    {
        _dirtyRegion = new(0, 0, 0, 0);
        _needsFullRedraw = false;
    }

    /// <summary>
    /// Marks region as cleaned and sets flag indicating full redraw was performed.
    /// </summary>
    public void ClearAfterFullRedraw()
    {
        _dirtyRegion = new(0, 0, 0, 0);
        _needsFullRedraw = false;
    }

    /// <summary>
    /// Checks if a given rectangle intersects with the dirty region.
    /// Useful for culling controls that don't need rendering.
    /// </summary>
    /// <param name="rect">Rectangle to test</param>
    /// <returns>True if rect intersects dirty region or full redraw needed</returns>
    public bool IntersectsDirtyRegion(Rectangle rect)
    {
        if (_needsFullRedraw)
        {
            return true;
        }

        return Intersects(rect, _dirtyRegion);
    }

    /// <summary>
    /// Constrains rendering to dirty region via scissor test.
    /// Converts dirty region to client space (pixel coordinates).
    /// </summary>
    /// <returns>Rectangle for scissor test, or zero-sized rect if no dirty region</returns>
    public Rectangle GetScissorRect()
    {
        return _needsFullRedraw ? new(0, 0, 0, 0) : _dirtyRegion;
    }

    /// <summary>
    /// Resets tracker to initial state (no invalidation).
    /// </summary>
    public void Reset()
    {
        _dirtyRegion = new(0, 0, 0, 0);
        _needsFullRedraw = true;
    }

    /// <summary>
    /// Checks if two rectangles intersect.
    /// </summary>
    private static bool Intersects(in Rectangle a, in Rectangle b)
    {
        return a.Left < b.Right &&
               a.Right > b.Left &&
               a.Top < b.Bottom &&
               a.Bottom > b.Top;
    }

    /// <summary>
    /// Computes the union of two rectangles (minimum bounding rectangle).
    /// </summary>
    private static Rectangle Union(in Rectangle a, in Rectangle b)
    {
        var left = Math.Min(a.Left, b.Left);
        var top = Math.Min(a.Top, b.Top);
        var right = Math.Max(a.Right, b.Right);
        var bottom = Math.Max(a.Bottom, b.Bottom);
        
        return new Rectangle(left, top, right - left, bottom - top);
    }

    /// <summary>
    /// Gets width of rectangle (right - left).
    /// </summary>
    private static int Width(in Rectangle rect) => rect.Right - rect.Left;

    /// <summary>
    /// Gets height of rectangle (bottom - top).
    /// </summary>
    private static int Height(in Rectangle rect) => rect.Bottom - rect.Top;
}
