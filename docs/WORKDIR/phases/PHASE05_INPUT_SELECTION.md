# Phase 05: Input System & Selection - PLAYABLE INTERACTION

**Phase Identifier**: PHASE05_INPUT_AND_SELECTION  
**Status**: Ready for Implementation  
**Priority**: 🔴 CRITICAL (Blocking all gameplay)  
**Estimated Duration**: 3-4 days  
**Target Completion**: ~12 developer-days of work  

---

## Overview

Enable the player to interact with the game through keyboard and mouse input. This phase establishes the foundation for all subsequent gameplay by connecting player input to game logic.

**Current State**: 50% (input captured but not routed to game logic)  
**Target State**: 100% (click, select, command working)

---

## Task Breakdown

### Task 1: Input Routing System (PLAN-032)

**Objective**: Route input events to appropriate handlers (UI vs game logic)

**Implementation**:

```csharp
// File: src/OpenSage.Game/Input/InputRouter.cs

public sealed class InputRouter : DisposableBase
{
    private readonly WndWindowManager _windowManager;
    private readonly GameLogic _gameLogic;
    private readonly InputMessageBuffer _inputBuffer;
    
    public InputRouter(WndWindowManager windowManager, GameLogic gameLogic, InputMessageBuffer buffer)
    {
        _windowManager = windowManager;
        _gameLogic = gameLogic;
        _inputBuffer = buffer;
    }
    
    public void ProcessInputMessage(InputMessage message)
    {
        // First try UI
        if (_windowManager.HandleInput(message))
            return; // UI consumed input
        
        // Otherwise, route to game logic
        _gameLogic.HandleInput(message);
    }
}

// Modify: Game.cs

public sealed class Game : DisposableBase, IGame
{
    private InputRouter _inputRouter;
    
    public Game(...)
    {
        // ... existing code ...
        _inputRouter = AddDisposable(new InputRouter(
            Scene2D.WindowManager,
            GameLogic,
            InputMessageBuffer
        ));
    }
    
    public void Step()
    {
        // Process input
        InputMessageBuffer.PumpEvents(/* messages */, GameTime);
        foreach (var message in InputMessageBuffer.Messages)
        {
            _inputRouter.ProcessInputMessage(message);
        }
        
        // Rest of game loop...
    }
}
```

**Acceptance Criteria**:
- [ ] UI clicks captured before game logic
- [ ] Game receives terrain clicks
- [ ] No input processed twice

**Effort**: 1 day

---

### Task 2: Selection System (PLAN-033)

**Objective**: Select and highlight units/buildings based on mouse clicks

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Selection/SelectionManager.cs

public sealed class SelectionManager
{
    public enum SelectionMode
    {
        Single,      // Single unit/building selected
        Multiple,    // Shift-click adds to selection
        Box,         // Drag to select multiple units
    }
    
    private HashSet<GameObject> _selectedObjects = new();
    private GameObject _hoverObject;
    public IReadOnlyCollection<GameObject> SelectedObjects => _selectedObjects;
    
    public SelectionMode CurrentMode { get; set; } = SelectionMode.Single;
    
    public void Select(GameObject obj, bool additive = false)
    {
        if (!additive)
            _selectedObjects.Clear();
        
        if (obj != null && !_selectedObjects.Contains(obj))
        {
            _selectedObjects.Add(obj);
            obj.OnSelected?.Invoke();
        }
    }
    
    public void Deselect(GameObject obj)
    {
        if (_selectedObjects.Remove(obj))
        {
            obj.OnDeselected?.Invoke();
        }
    }
    
    public void DeselectAll()
    {
        foreach (var obj in _selectedObjects)
        {
            obj.OnDeselected?.Invoke();
        }
        _selectedObjects.Clear();
    }
    
    public void SelectByBox(Rectangle selectionBox, Player onlyPlayer = null)
    {
        DeselectAll();
        
        // Find all objects in selection box
        foreach (var obj in GameLogic.Objects)
        {
            if (onlyPlayer != null && obj.Owner != onlyPlayer)
                continue;
            
            if (selectionBox.Contains((int)obj.Position.X, (int)obj.Position.Y))
            {
                _selectedObjects.Add(obj);
                obj.OnSelected?.Invoke();
            }
        }
    }
}

// Modify: GameObject.cs

public abstract class GameObject : DisposableBase
{
    public event Action OnSelected;
    public event Action OnDeselected;
    
    public virtual void OnMouseEnter() { }
    public virtual void OnMouseExit() { }
    
    public override void Render(RenderContext context)
    {
        // Render selection highlight if selected
        base.Render(context);
        
        // If selected: draw selection indicator
        if (IsSelected)
        {
            DrawSelectionHighlight(context);
        }
    }
    
    private void DrawSelectionHighlight(RenderContext context)
    {
        // Draw green box around selected unit/building
        var bounds = GetBounds();
        DrawDebugBox(context, bounds, Color.Green, 2.0f);
    }
}
```

**Acceptance Criteria**:
- [ ] Left-click selects unit/building
- [ ] Selection highlighted visually (green box)
- [ ] Shift-click adds to selection
- [ ] Ctrl+A selects all player units
- [ ] Escape deselects all

**Effort**: 1.5 days

---

### Task 3: Raycasting & Object Picking (PLAN-034)

**Objective**: Convert mouse position to world coordinate and find clicked object

**Implementation**:

```csharp
// File: src/OpenSage.Game/Terrain/Terrain.cs (extend)

public sealed class RaycastManager
{
    private readonly Terrain _terrain;
    private readonly HeightMap _heightMap;
    private readonly Matrix4x4 _viewProjection;
    
    public RaycastManager(Terrain terrain, HeightMap heightMap, Matrix4x4 viewProjection)
    {
        _terrain = terrain;
        _heightMap = heightMap;
        _viewProjection = viewProjection;
    }
    
    public bool RaycastTerrain(Vector2 screenPos, out Coord3D worldPos)
    {
        // Convert screen coordinates to world ray
        // Cast ray against terrain height map
        // Return intersection point
        
        worldPos = default;
        
        // Unproject screen position to world ray
        var ray = ScreenToWorldRay(screenPos);
        
        // Intersect with terrain at ray.Origin.Z = terrain height
        float terrainHeight = _heightMap.GetHeightAt(ray.Origin.X, ray.Origin.Y);
        
        if (ray.Direction.Z != 0)
        {
            float t = (terrainHeight - ray.Origin.Z) / ray.Direction.Z;
            if (t > 0)
            {
                worldPos = ray.Origin + ray.Direction * t;
                return true;
            }
        }
        
        return false;
    }
    
    public GameObject RaycastObjects(Vector2 screenPos, out GameObject hitObject)
    {
        hitObject = null;
        
        if (!RaycastTerrain(screenPos, out var terrainPos))
            return null;
        
        // Find closest object to ray
        float closestDistance = float.MaxValue;
        GameObject closest = null;
        
        foreach (var obj in GameLogic.Objects)
        {
            float distance = Vector3.Distance(obj.Position, terrainPos);
            if (distance < obj.Bounds.Radius && distance < closestDistance)
            {
                closestDistance = distance;
                closest = obj;
            }
        }
        
        hitObject = closest;
        return closest;
    }
    
    private Ray ScreenToWorldRay(Vector2 screenPos)
    {
        // Use camera and viewport to convert screen → world
        // (Implementation: use inverse of view-projection matrix)
        // For now: placeholder
        
        return new Ray(Vector3.Zero, Vector3.Forward);
    }
}

// Modify: GameLogic.cs

public sealed class GameLogic : DisposableBase
{
    private RaycastManager _raycastManager;
    
    public void Update(in TimeInterval gameTime)
    {
        // ... existing code ...
    }
    
    public bool HandleMouseClick(Vector2 screenPos, MouseButton button, KeyboardModifiers modifiers)
    {
        if (_raycastManager.RaycastObjects(screenPos, out var hitObject))
        {
            // Clicked on object
            switch (button)
            {
                case MouseButton.Left:
                    SelectionManager.Select(hitObject, modifiers.HasFlag(KeyboardModifiers.Shift));
                    break;
                case MouseButton.Right:
                    if (SelectionManager.SelectedObjects.Count > 0)
                    {
                        IssueCommand(SelectionManager.SelectedObjects, hitObject);
                    }
                    break;
            }
            return true;
        }
        else if (_raycastManager.RaycastTerrain(screenPos, out var terrainPos))
        {
            // Clicked on terrain
            switch (button)
            {
                case MouseButton.Left:
                    SelectionManager.DeselectAll();
                    break;
                case MouseButton.Right:
                    if (SelectionManager.SelectedObjects.Count > 0)
                    {
                        IssueCommandMove(SelectionManager.SelectedObjects, terrainPos);
                    }
                    break;
            }
            return true;
        }
        
        return false;
    }
}
```

**Acceptance Criteria**:
- [ ] Screen coordinates convert to world coordinates
- [ ] Terrain intersection calculated correctly
- [ ] Object picking works (nearest object selected)
- [ ] Works with any camera position/angle

**Effort**: 1.5 days

---

### Task 4: Command Queue System (PLAN-035)

**Objective**: Queue unit commands (move, attack, build) for execution each frame

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Commands/Command.cs

public abstract class Command
{
    public abstract void Execute(ICollection<GameObject> objects);
}

public sealed class MoveCommand : Command
{
    public Coord3D Destination { get; }
    
    public MoveCommand(Coord3D destination)
    {
        Destination = destination;
    }
    
    public override void Execute(ICollection<GameObject> objects)
    {
        foreach (var obj in objects.OfType<Unit>())
        {
            obj.QueueCommand(new UnitCommand { Type = UnitCommandType.Move, TargetPosition = Destination });
        }
    }
}

public sealed class AttackCommand : Command
{
    public GameObject Target { get; }
    
    public AttackCommand(GameObject target)
    {
        Target = target;
    }
    
    public override void Execute(ICollection<GameObject> objects)
    {
        foreach (var obj in objects.OfType<Unit>())
        {
            obj.QueueCommand(new UnitCommand { Type = UnitCommandType.Attack, TargetObject = Target });
        }
    }
}

// File: src/OpenSage.Game/Logic/Commands/CommandQueue.cs

public sealed class CommandQueue
{
    private Queue<Command> _commandQueue = new();
    
    public void Enqueue(Command command)
    {
        _commandQueue.Enqueue(command);
    }
    
    public void ProcessQueue(ICollection<GameObject> selectedObjects)
    {
        while (_commandQueue.TryDequeue(out var command))
        {
            command.Execute(selectedObjects);
        }
    }
}

// Modify: GameLogic.cs

public sealed class GameLogic : DisposableBase
{
    private CommandQueue _commandQueue;
    private SelectionManager _selectionManager;
    
    public void Update(in TimeInterval gameTime)
    {
        // Process pending commands each frame
        _commandQueue.ProcessQueue(SelectionManager.SelectedObjects);
        
        // Update all objects
        foreach (var obj in _objects)
        {
            obj.Update(gameTime);
        }
    }
    
    public void IssueCommandMove(ICollection<GameObject> units, Coord3D destination)
    {
        _commandQueue.Enqueue(new MoveCommand(destination));
    }
    
    public void IssueCommandAttack(ICollection<GameObject> units, GameObject target)
    {
        _commandQueue.Enqueue(new AttackCommand(target));
    }
}
```

**Acceptance Criteria**:
- [ ] Commands queued without executing immediately
- [ ] Commands execute in FIFO order
- [ ] Multiple commands can be queued
- [ ] Commands apply to all selected units

**Effort**: 1 day

---

## Integration Checklist

- [ ] InputRouter wired into Game.Step()
- [ ] SelectionManager created in GameLogic
- [ ] RaycastManager created and initialized
- [ ] Command system integrated
- [ ] Selection highlighting renders
- [ ] Input messages flow correctly

---

## Testing

```csharp
[TestFixture]
public class InputIntegrationTests
{
    [Test]
    public void ClickOnTerrain_ShouldDeselectUnits()
    {
        // Arrange
        var game = CreateTestGame();
        var unit = CreateTestUnit(game);
        game.SelectionManager.Select(unit);
        
        // Act
        game.HandleMouseClick(new Vector2(100, 100), MouseButton.Left, KeyboardModifiers.None);
        
        // Assert
        Assert.IsEmpty(game.SelectionManager.SelectedObjects);
    }
    
    [Test]
    public void ClickOnUnit_ShouldSelectUnit()
    {
        // Arrange
        var game = CreateTestGame();
        var unit = CreateTestUnit(game);
        
        // Act
        var screenPos = WorldToScreenPosition(unit.Position);
        game.HandleMouseClick(screenPos, MouseButton.Left, KeyboardModifiers.None);
        
        // Assert
        Assert.Contains(unit, game.SelectionManager.SelectedObjects);
    }
    
    [Test]
    public void RightClickOnTerrain_ShouldIssueMoveCommand()
    {
        // Arrange
        var game = CreateTestGame();
        var unit = CreateTestUnit(game);
        game.SelectionManager.Select(unit);
        
        // Act
        game.HandleMouseClick(new Vector2(200, 200), MouseButton.Right, KeyboardModifiers.None);
        
        // Assert
        Assert.IsNotNull(unit.CurrentCommand);
        Assert.AreEqual(UnitCommandType.Move, unit.CurrentCommand.Type);
    }
}
```

---

## Success Metrics

After Phase 05:
- ✅ Player can select units by clicking
- ✅ Right-click can issue commands (queued, not executed yet)
- ✅ Visual feedback on selection (highlight)
- ✅ Ctrl+click for multi-select
- ✅ Escape to deselect all

---

## Next Phase Dependencies

**Phase 06 (Game Loop)** requires:
- SelectionManager working ✅
- CommandQueue implemented ✅
- Input flowing correctly ✅

All input groundwork complete for gameplay logic.
