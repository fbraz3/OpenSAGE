# Phase Planning: GUI/WND Rendering

**Phase Identifier**: PHASE03_GUI_RENDERING  
**Status**: Planning  
**Priority**: High  
**Estimated Duration**: 2-3 weeks

---

## Overview

Complete the GUI system with advanced controls, layout management, and performance optimization.

**Current Status**: 80% complete  
**Target Status**: 100% complete

---

## Detailed Tasks

### Task 1: ListBox Multi-Selection Support (PLAN-003)
**Phase**: Phase 1 (Week 1)  
**Complexity**: Medium  
**Effort**: 1-2 days  
**Dependencies**: None  

**Description**:
Implement multi-selection modes for ListBox control (Single, Multiple, Extended).

**Current State**:
- ListBox supports only single selection
- Reference: `src/OpenSage.Game/Gui/Wnd/Controls/ListBox.cs`

**Implementation**:

**Define selection modes**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/ListBox.cs

public enum ListBoxSelectionMode
{
    Single,    // One item selected
    Multiple,  // Multiple items via Ctrl+Click
    Extended   // Multiple items via Shift+Click ranges
}

public class ListBox : Control
{
    private List<int> _selectedIndices = new();
    public ListBoxSelectionMode SelectionMode { get; set; } = ListBoxSelectionMode.Single;
    public event EventHandler<ListBoxSelectionChangedEventArgs> SelectionChanged;
    
    public IReadOnlyList<int> SelectedIndices => _selectedIndices.AsReadOnly();
    
    public int SelectedIndex
    {
        get => _selectedIndices.Count > 0 ? _selectedIndices[0] : -1;
        set => SetSingleSelection(value);
    }
    
    private void SetSingleSelection(int index)
    {
        _selectedIndices.Clear();
        if (index >= 0 && index < Items.Count)
        {
            _selectedIndices.Add(index);
        }
        SelectionChanged?.Invoke(this, new ListBoxSelectionChangedEventArgs(_selectedIndices));
    }
    
    private void AddToSelection(int index)
    {
        if (index < 0 || index >= Items.Count)
            return;
        
        if (!_selectedIndices.Contains(index))
        {
            _selectedIndices.Add(index);
            SelectionChanged?.Invoke(this, new ListBoxSelectionChangedEventArgs(_selectedIndices));
        }
    }
    
    private void RemoveFromSelection(int index)
    {
        if (_selectedIndices.Remove(index))
        {
            SelectionChanged?.Invoke(this, new ListBoxSelectionChangedEventArgs(_selectedIndices));
        }
    }
    
    private void ToggleSelection(int index)
    {
        if (_selectedIndices.Contains(index))
            RemoveFromSelection(index);
        else
            AddToSelection(index);
    }
}
```

**Implement mouse handling**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/ListBox.cs (continued)

protected override void OnMouseDown(MouseEventArgs e)
{
    base.OnMouseDown(e);
    
    var index = GetIndexAtPoint(e.Location);
    if (index < 0)
        return;
    
    switch (SelectionMode)
    {
        case ListBoxSelectionMode.Single:
            SetSingleSelection(index);
            break;
            
        case ListBoxSelectionMode.Multiple:
            if ((e.Modifiers & ModifierKeys.Control) != 0)
            {
                ToggleSelection(index);
            }
            else
            {
                SetSingleSelection(index);
            }
            break;
            
        case ListBoxSelectionMode.Extended:
            if ((e.Modifiers & ModifierKeys.Control) != 0)
            {
                ToggleSelection(index);
            }
            else if ((e.Modifiers & ModifierKeys.Shift) != 0)
            {
                SelectRange(index);
            }
            else
            {
                SetSingleSelection(index);
            }
            break;
    }
}

private void SelectRange(int toIndex)
{
    if (_selectedIndices.Count == 0)
    {
        SetSingleSelection(toIndex);
        return;
    }
    
    var fromIndex = _selectedIndices[_selectedIndices.Count - 1];
    _selectedIndices.Clear();
    
    var start = Math.Min(fromIndex, toIndex);
    var end = Math.Max(fromIndex, toIndex);
    
    for (int i = start; i <= end; i++)
    {
        _selectedIndices.Add(i);
    }
    
    SelectionChanged?.Invoke(this, new ListBoxSelectionChangedEventArgs(_selectedIndices));
}

private int GetIndexAtPoint(Point location)
{
    if (location.Y < 0 || location.Y >= Height)
        return -1;
    
    var itemHeight = GetItemHeight();
    var index = _scrollPosition + location.Y / itemHeight;
    
    return index >= 0 && index < Items.Count ? index : -1;
}
```

**Add selection changed event**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/ListBox.cs

public class ListBoxSelectionChangedEventArgs : EventArgs
{
    public IReadOnlyList<int> SelectedIndices { get; }
    
    public ListBoxSelectionChangedEventArgs(IReadOnlyList<int> selectedIndices)
    {
        SelectedIndices = selectedIndices;
    }
}
```

**Acceptance Criteria**:
- [ ] Single selection working as before
- [ ] Ctrl+Click toggles individual items
- [ ] Shift+Click selects ranges
- [ ] SelectionChanged event fires correctly
- [ ] Selected items visually highlighted
- [ ] Performance with 1000+ items acceptable

**Testing**:
```csharp
[Test]
public void TestListBoxMultiSelection()
{
    var listBox = new ListBox { SelectionMode = ListBoxSelectionMode.Multiple };
    listBox.Items.AddRange(new[] { "A", "B", "C", "D", "E" });
    
    // Single selection
    listBox.SelectedIndex = 0;
    Assert.AreEqual(1, listBox.SelectedIndices.Count);
    
    // Ctrl+Click to add
    listBox.OnMouseDown(new MouseEventArgs(1, 0, ModifierKeys.Control));
    Assert.AreEqual(2, listBox.SelectedIndices.Count);
    
    // Shift+Click to range select
    listBox.SelectRange(4);
    Assert.AreEqual(5, listBox.SelectedIndices.Count);
}
```

---

### Task 2: Implement Dirty Region Tracking (PLAN-007)
**Phase**: Phase 2 (Weeks 2-3)  
**Complexity**: Medium  
**Effort**: 2-3 days  
**Dependencies**: None  

**Description**:
Implement dirty region tracking to optimize GUI rendering performance.

**Current State**:
- Full window redraw every frame
- Reference: `src/OpenSage.Game/Gui/DrawingContext2D.cs`

**Implementation**:

**Add dirty region tracking to Window**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Window.cs

public class Window : Control
{
    private Rectangle _dirtyRegion = Rectangle.Empty;
    private bool _needsFullRedraw = true;
    
    public void InvalidateRect(Rectangle rect)
    {
        if (rect.IsEmpty)
        {
            _needsFullRedraw = true;
            return;
        }
        
        if (_dirtyRegion.IsEmpty)
            _dirtyRegion = rect;
        else
            _dirtyRegion = Rectangle.Union(_dirtyRegion, rect);
    }
    
    public void Invalidate()
    {
        _needsFullRedraw = true;
    }
    
    public override void Render(DrawingContext2D context)
    {
        if (_needsFullRedraw)
        {
            // Full redraw
            base.Render(context);
            _needsFullRedraw = false;
            _dirtyRegion = Rectangle.Empty;
        }
        else if (!_dirtyRegion.IsEmpty)
        {
            // Partial redraw - render only dirty region
            context.PushScissor(_dirtyRegion);
            
            foreach (var control in Controls)
            {
                if (control.Bounds.Intersects(_dirtyRegion))
                {
                    control.Render(context);
                }
            }
            
            context.PopScissor();
            _dirtyRegion = Rectangle.Empty;
        }
    }
}
```

**Propagate invalidation to controls**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/Control.cs

public class Control
{
    protected Window ParentWindow { get; set; }
    
    public virtual void Invalidate()
    {
        ParentWindow?.InvalidateRect(Bounds);
    }
    
    public virtual void InvalidateRect(Rectangle rect)
    {
        ParentWindow?.InvalidateRect(Rectangle.Intersect(rect, Bounds));
    }
    
    // Example: Button invalidates on state change
    public void SetPressed(bool pressed)
    {
        if (_pressed != pressed)
        {
            _pressed = pressed;
            Invalidate();  // Triggers dirty region update
        }
    }
}
```

**Integrate with Control updates**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/Button.cs

public override void OnMouseEnter()
{
    base.OnMouseEnter();
    _isHovered = true;
    Invalidate();  // Schedule redraw
}

public override void OnMouseLeave()
{
    base.OnMouseLeave();
    _isHovered = false;
    Invalidate();
}

public override void OnClick()
{
    base.OnClick();
    Invalidate();
}
```

**Acceptance Criteria**:
- [ ] Only dirty regions redrawn
- [ ] Full redraw still works when needed
- [ ] Scissor test properly clips rendering
- [ ] Control state changes trigger invalidation
- [ ] Performance: 2-3x improvement for static UIs
- [ ] No visual artifacts

**Testing**:
```csharp
[Test]
public void TestDirtyRegionTracking()
{
    var window = new Window { Size = new Size(800, 600) };
    var button = new Button { Bounds = new Rectangle(100, 100, 200, 50) };
    window.Controls.Add(button);
    
    // Invalidate only button area
    window.InvalidateRect(button.Bounds);
    Assert.False(window.DirtyRegion.IsEmpty);
    
    // Render should only update dirty region
    var context = new MockDrawingContext2D();
    window.Render(context);
    
    Assert.Greater(context.RedrawRectangles.Count, 0);
    Assert.Contains(button.Bounds, context.RedrawRectangles);
}
```

---

### Task 3: Implement Tooltip System (PLAN-011)
**Phase**: Phase 3 (Weeks 4-5)  
**Complexity**: Low  
**Effort**: 1 day  
**Dependencies**: None  

**Description**:
Display context-sensitive help text (tooltips) when hovering over controls.

**Current State**:
- No tooltip system implemented

**Implementation**:

**Create tooltip manager**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/TooltipManager.cs

public class TooltipManager
{
    private Control _hoveredControl;
    private TimeSpan _hoverTime = TimeSpan.Zero;
    private const double TooltipDelaySeconds = 1.0;
    
    private Window _tooltipWindow;
    private Label _tooltipLabel;
    
    public void Update(in TimeInterval gameTime)
    {
        if (_hoveredControl == null || string.IsNullOrEmpty(_hoveredControl.Tooltip))
        {
            HideTooltip();
            return;
        }
        
        _hoverTime += gameTime.DeltaTime;
        
        if (_hoverTime.TotalSeconds >= TooltipDelaySeconds && _tooltipWindow == null)
        {
            ShowTooltip(_hoveredControl);
        }
    }
    
    public void OnMouseEnter(Control control)
    {
        _hoveredControl = control;
        _hoverTime = TimeSpan.Zero;
        
        if (_tooltipWindow != null)
            HideTooltip();
    }
    
    public void OnMouseLeave(Control control)
    {
        if (_hoveredControl == control)
        {
            _hoveredControl = null;
            HideTooltip();
        }
    }
    
    private void ShowTooltip(Control control)
    {
        var tooltip = control.Tooltip;
        var position = control.ScreenLocation + new Point(0, control.Height + 5);
        
        _tooltipWindow = new Window
        {
            Name = "Tooltip",
            Location = position,
            BackgroundColor = new Color(255, 255, 200),
            BorderColor = Color.Black,
            BorderWidth = 1
        };
        
        _tooltipLabel = new Label
        {
            Text = tooltip,
            ForeColor = Color.Black,
            AutoSize = true,
            Padding = new Padding(5)
        };
        
        _tooltipWindow.Controls.Add(_tooltipLabel);
        _tooltipWindow.Size = new Size(
            _tooltipLabel.Width + 10,
            _tooltipLabel.Height + 10);
        
        // Clamp to screen bounds
        var screenBounds = GetScreenBounds();
        if (_tooltipWindow.Right > screenBounds.Right)
            _tooltipWindow.X -= _tooltipWindow.Right - screenBounds.Right;
        if (_tooltipWindow.Bottom > screenBounds.Bottom)
            _tooltipWindow.Y -= _tooltipWindow.Bottom - screenBounds.Bottom;
    }
    
    private void HideTooltip()
    {
        _tooltipWindow?.Dispose();
        _tooltipWindow = null;
        _tooltipLabel = null;
    }
    
    public void Render(DrawingContext2D context)
    {
        _tooltipWindow?.Render(context);
    }
}
```

**Extend Control with tooltip support**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/Control.cs

public class Control
{
    public string Tooltip { get; set; }
    
    protected override void OnMouseEnter()
    {
        base.OnMouseEnter();
        TooltipManager?.OnMouseEnter(this);
    }
    
    protected override void OnMouseLeave()
    {
        base.OnMouseLeave();
        TooltipManager?.OnMouseLeave(this);
    }
}
```

**Integrate with WndWindowManager**:
```csharp
// File: src/OpenSage.Game/Gui/Wnd/WndWindowManager.cs

public class WndWindowManager
{
    private TooltipManager _tooltipManager = new();
    
    public void Update(in TimeInterval gameTime)
    {
        _tooltipManager.Update(gameTime);
    }
    
    public void Render(DrawingContext2D context)
    {
        // ... render all windows ...
        
        _tooltipManager.Render(context);
    }
}
```

**Acceptance Criteria**:
- [ ] Tooltips appear after 1 second hover
- [ ] Tooltip text displays correctly
- [ ] Tooltip positions near cursor
- [ ] Clamps to screen bounds
- [ ] Disappears on mouse leave
- [ ] Works with all control types

**Testing**:
```csharp
[Test]
public void TestTooltipDisplay()
{
    var control = new Button { Tooltip = "Click me!" };
    var manager = new TooltipManager();
    
    manager.OnMouseEnter(control);
    
    // Wait less than delay
    manager.Update(TimeSpan.FromMilliseconds(500));
    Assert.IsNull(manager.TooltipWindow);
    
    // Wait past delay
    manager.Update(TimeSpan.FromSeconds(1.5));
    Assert.IsNotNull(manager.TooltipWindow);
    Assert.AreEqual("Click me!", manager.TooltipLabel.Text);
}
```

---

### Task 4: Responsive Layout System (PLAN-009)
**Phase**: Phase 3 (Weeks 4-5)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: None  

**Description**:
Implement anchor/docking system for responsive window layouts.

**Current State**:
- Controls use fixed positioning
- No responsive layout system

**Implementation**:

```csharp
// File: src/OpenSage.Game/Gui/Wnd/Controls/Control.cs

[Flags]
public enum DockStyle
{
    None = 0,
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8,
    Fill = 16  // Fill remaining space
}

[Flags]
public enum AnchorStyle
{
    Top = 1,
    Bottom = 2,
    Left = 4,
    Right = 8
}

public class Control
{
    public DockStyle Dock { get; set; }
    public AnchorStyle Anchor { get; set; }
    private Rectangle _anchorMargin;
    
    public void SetAnchor(AnchorStyle anchor, Rectangle margin)
    {
        Anchor = anchor;
        _anchorMargin = margin;
    }
    
    public void UpdateLayout(Rectangle parentBounds)
    {
        if (Dock != DockStyle.None)
        {
            ApplyDockStyle(parentBounds);
        }
        else if (Anchor != AnchorStyle.None)
        {
            ApplyAnchorStyle(parentBounds);
        }
    }
    
    private void ApplyDockStyle(Rectangle parentBounds)
    {
        switch (Dock)
        {
            case DockStyle.Top:
                Bounds = new Rectangle(
                    parentBounds.X, parentBounds.Y,
                    parentBounds.Width, Height);
                break;
                
            case DockStyle.Bottom:
                Bounds = new Rectangle(
                    parentBounds.X,
                    parentBounds.Bottom - Height,
                    parentBounds.Width, Height);
                break;
                
            case DockStyle.Left:
                Bounds = new Rectangle(
                    parentBounds.X, parentBounds.Y,
                    Width, parentBounds.Height);
                break;
                
            case DockStyle.Right:
                Bounds = new Rectangle(
                    parentBounds.Right - Width, parentBounds.Y,
                    Width, parentBounds.Height);
                break;
                
            case DockStyle.Fill:
                Bounds = parentBounds;
                break;
        }
    }
    
    private void ApplyAnchorStyle(Rectangle parentBounds)
    {
        var left = (Anchor & AnchorStyle.Left) != 0 ? X : 0;
        var top = (Anchor & AnchorStyle.Top) != 0 ? Y : 0;
        var right = (Anchor & AnchorStyle.Right) != 0 ? 0 : parentBounds.Width - Right;
        var bottom = (Anchor & AnchorStyle.Bottom) != 0 ? 0 : parentBounds.Height - Bottom;
        
        var newWidth = Math.Max(0, Width + right - left);
        var newHeight = Math.Max(0, Height + bottom - top);
        
        X = left;
        Y = top;
        Width = newWidth;
        Height = newHeight;
    }
}
```

**Acceptance Criteria**:
- [ ] Dock styles working (Top, Bottom, Left, Right, Fill)
- [ ] Anchor styles working with resize
- [ ] Layout updates on parent resize
- [ ] Multiple docked controls layout correctly
- [ ] Performance acceptable

---

## Integration Points

### With WndWindowManager
```csharp
// In WndWindowManager.cs
public void Update(in TimeInterval gameTime)
{
    _tooltipManager.Update(gameTime);
    
    // Update dirty regions
    foreach (var window in WindowStack)
    {
        window.ProcessPendingInvalidation();
    }
}
```

### With DrawingContext2D
```csharp
// In DrawingContext2D.cs
public void PushScissor(Rectangle rect)
{
    // Set scissor test to clip rendering
}

public void PopScissor()
{
    // Restore previous scissor
}
```

---

## Testing Strategy

### Unit Tests
- ListBox selection modes
- Dirty region calculations
- Tooltip positioning
- Layout calculations

### Integration Tests
- Complex UI layouts
- ListBox with 1000+ items
- Multiple windows with tooltips
- Responsive resize behavior

### Performance Tests
- Dirty region reduces redraw 50%+
- ListBox scrolling smooth at 60 FPS
- No memory leaks with tooltips

---

## Success Metrics

- [ ] All control types fully functional
- [ ] GUI performance: 60 FPS on complex layouts
- [ ] Multi-selection working smoothly
- [ ] Tooltips helpful and non-intrusive
- [ ] Layout system responsive
- [ ] Code follows OpenSAGE standards
- [ ] Unit test coverage > 85%
- [ ] Documentation updated
