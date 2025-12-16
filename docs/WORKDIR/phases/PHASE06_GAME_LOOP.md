# Phase 06: Game Loop & State Management

**Phase Identifier**: PHASE06_GAME_LOOP  
**Status**: Ready for Implementation  
**Priority**: 🔴 CRITICAL (Blocking all gameplay)  
**Estimated Duration**: 5-7 days  
**Target Completion**: Stable object persistence and updates  

---

## Overview

Establish the core game loop that manages persistent state and object updates. Without this, nothing in the game persists or changes between frames. This is the heartbeat that keeps the game alive.

**Current State**: 5% (main loop exists but calls only rendering)  
**Target State**: 100% (objects persist, update each frame, win conditions evaluated)

---

## Architecture Overview

```
60 Hz Main Tick (Game.Step):
├─ Input processing (from Phase 05)
├─ Authority tick (every 12 frames = 5 Hz)
│  ├─ Process command queue
│  ├─ Building construction progress
│  ├─ Unit production ticks
│  ├─ Resource economy
│  ├─ Win condition checks
│  └─ Script evaluation
├─ Object updates (60 Hz)
│  ├─ Unit movement/locomotion
│  ├─ Particle systems
│  ├─ Projectiles
│  └─ Visual effects
├─ Physics updates
├─ Cleanup destroyed objects
└─ Render everything
```

---

## Task 1: GameLogic Refactor (PLAN-036)

**Objective**: Refactor GameLogic to properly manage game state and objects

**Current Issues**:
- GameLogic exists but is mostly empty
- No object collection management
- No player management
- No state persistence
- No frame counting for authority tick

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/GameLogic.cs

public sealed class GameLogic : DisposableBase
{
    // Object management
    private List<GameObject> _allObjects = new();
    private Dictionary<uint, GameObject> _objectsById = new();
    private List<Player> _players = new();
    
    // Game state
    private int _currentFrame;
    private bool _isGameOver;
    private Player _winner;
    
    // Subsystems
    private CommandQueue _commandQueue;
    private ProjectileManager _projectileManager;
    private ParticleSystemManager _particleSystemManager;
    private CollisionAvoidanceSystem _collisionAvoidance;
    
    // Timing
    private TimeInterval _gameTime;
    private const int AUTHORITY_TICK_INTERVAL = 12; // 5 Hz = 60 Hz / 12
    
    public IReadOnlyList<GameObject> AllObjects => _allObjects;
    public IReadOnlyList<Player> Players => _players;
    public int CurrentFrame => _currentFrame;
    public bool IsGameOver => _isGameOver;
    public Player Winner => _winner;
    
    public event Action<Player> GameOverEvent;
    
    public GameLogic(ContentManager contentManager)
    {
        // Initialize subsystems
        _commandQueue = new CommandQueue();
        _projectileManager = new ProjectileManager();
        _particleSystemManager = new ParticleSystemManager();
        _collisionAvoidance = new CollisionAvoidanceSystem();
        
        _currentFrame = 0;
    }
    
    /// <summary>
    /// Main game update - called every frame at 60 Hz
    /// </summary>
    public void Update(in TimeInterval gameTime)
    {
        if (_isGameOver)
            return;
        
        _gameTime = gameTime;
        
        // Authority tick every 12 frames (5 Hz)
        bool isAuthorityTick = (_currentFrame % AUTHORITY_TICK_INTERVAL) == 0;
        
        if (isAuthorityTick)
        {
            // Authority-level updates (commands, construction, AI decisions)
            ProcessCommands();
            UpdateBuildings();
            UpdateProduction();
            CheckWinConditions();
        }
        
        // Physics/animation updates (60 Hz)
        UpdateObjects(gameTime);
        
        // Projectile updates
        if (_projectileManager != null)
        {
            _projectileManager.Update(gameTime);
        }
        
        // Collision avoidance for units
        if (_collisionAvoidance != null)
        {
            var units = _allObjects.OfType<Unit>().ToList();
            _collisionAvoidance.Update(units);
        }
        
        // Cleanup destroyed objects
        CleanupDestroyedObjects();
        
        _currentFrame++;
    }
    
    /// <summary>
    /// Process queued commands (from player input)
    /// </summary>
    private void ProcessCommands()
    {
        _commandQueue.ProcessQueue();
    }
    
    /// <summary>
    /// Update all game objects
    /// </summary>
    private void UpdateObjects(in TimeInterval gameTime)
    {
        foreach (var obj in _allObjects)
        {
            if (obj.IsDestroyed)
                continue;
            
            // All objects update at 60 Hz
            obj.Update(gameTime);
        }
    }
    
    /// <summary>
    /// Update building construction progress and state
    /// </summary>
    private void UpdateBuildings()
    {
        var buildings = _allObjects.OfType<Building>().ToList();
        
        foreach (var building in buildings)
        {
            if (building.IsDestroyed)
                continue;
            
            // Construction progress tick
            if (building.IsConstructing)
            {
                building.IncrementConstructionProgress(1.0f / 12.0f); // 12 ticks per second
            }
        }
    }
    
    /// <summary>
    /// Update unit production from production buildings
    /// </summary>
    private void UpdateProduction()
    {
        var factories = _allObjects.OfType<ProductionBuilding>().ToList();
        
        foreach (var factory in factories)
        {
            if (factory.IsDestroyed || !factory.IsOperational)
                continue;
            
            // Production tick
            factory.UpdateProduction();
        }
    }
    
    /// <summary>
    /// Check win/lose conditions
    /// </summary>
    private void CheckWinConditions()
    {
        if (_isGameOver)
            return;
        
        // Count active players (those with units or buildings)
        var activePlayers = _players
            .Where(p => !p.IsEliminated)
            .ToList();
        
        // Update elimination status
        foreach (var player in _players)
        {
            var playerUnits = _allObjects.OfType<Unit>().Where(u => u.Owner == player).Count();
            var playerBuildings = _allObjects.OfType<Building>().Where(b => b.Owner == player).Count();
            
            if (playerUnits == 0 && playerBuildings == 0)
            {
                player.IsEliminated = true;
            }
        }
        
        activePlayers = _players.Where(p => !p.IsEliminated).ToList();
        
        // Game over if 1 or fewer active players
        if (activePlayers.Count <= 1)
        {
            _isGameOver = true;
            _winner = activePlayers.FirstOrDefault();
            GameOverEvent?.Invoke(_winner);
        }
    }
    
    /// <summary>
    /// Remove destroyed objects from the world
    /// </summary>
    private void CleanupDestroyedObjects()
    {
        // Remove from list
        var toRemove = _allObjects.Where(obj => obj.IsDestroyed).ToList();
        foreach (var obj in toRemove)
        {
            _allObjects.Remove(obj);
            _objectsById.Remove(obj.Id);
        }
    }
    
    /// <summary>
    /// Add object to the game world
    /// </summary>
    public void AddObject(GameObject obj)
    {
        if (obj == null)
            return;
        
        _allObjects.Add(obj);
        _objectsById[obj.Id] = obj;
        
        obj.GameLogic = this;
    }
    
    /// <summary>
    /// Get object by unique ID
    /// </summary>
    public GameObject GetObjectById(uint id)
    {
        _objectsById.TryGetValue(id, out var obj);
        return obj;
    }
    
    /// <summary>
    /// Get all objects in range of a position
    /// </summary>
    public List<GameObject> GetObjectsInRange(Coord3D center, float radius)
    {
        return _allObjects
            .Where(obj => Vector3.Distance(obj.Position, center) <= radius && !obj.IsDestroyed)
            .ToList();
    }
    
    /// <summary>
    /// Register a player in the game
    /// </summary>
    public void AddPlayer(Player player)
    {
        _players.Add(player);
    }
    
    /// <summary>
    /// Get player by team
    /// </summary>
    public List<Player> GetPlayersByTeam(Team team)
    {
        return _players.Where(p => p.Team == team).ToList();
    }
    
    public override void Dispose()
    {
        _projectileManager?.Dispose();
        _particleSystemManager?.Dispose();
        
        foreach (var obj in _allObjects)
        {
            obj?.Dispose();
        }
        
        base.Dispose();
    }
}
```

**Acceptance Criteria**:
- [ ] Objects persist between frames
- [ ] Authority tick fires every 12 frames
- [ ] Objects can be added/removed from world
- [ ] GetObjectsInRange works correctly
- [ ] No memory leaks (proper cleanup)

**Effort**: 2 days

---

## Task 2: Player & Team Management (PLAN-037)

**Objective**: Establish player tracking and team system

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Player/Player.cs

public sealed class Player
{
    private static uint _nextPlayerId = 1;
    
    public uint Id { get; }
    public string Name { get; set; }
    public Team Team { get; set; }
    public PlayerColor Color { get; set; }
    public long Money { get; set; }
    public bool IsHuman { get; set; }
    public bool IsEliminated { get; set; }
    
    private List<Unit> _units = new();
    private List<Building> _buildings = new();
    private List<int> _counters = new int[256]; // Script counters
    
    public IReadOnlyList<Unit> Units => _units;
    public IReadOnlyList<Building> Buildings => _buildings;
    
    public Player(string name, Team team, PlayerColor color, bool isHuman)
    {
        Id = _nextPlayerId++;
        Name = name;
        Team = team;
        Color = color;
        IsHuman = isHuman;
        Money = 5000; // Starting money
        IsEliminated = false;
    }
    
    /// <summary>
    /// Add money to player account
    /// </summary>
    public void AddMoney(long amount)
    {
        Money = Math.Max(0, Money + amount);
    }
    
    /// <summary>
    /// Attempt to spend money
    /// </summary>
    public bool SpendMoney(long amount)
    {
        if (Money >= amount)
        {
            Money -= amount;
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// Register unit as owned by this player
    /// </summary>
    public void RegisterUnit(Unit unit)
    {
        if (!_units.Contains(unit))
        {
            _units.Add(unit);
        }
    }
    
    /// <summary>
    /// Unregister destroyed unit
    /// </summary>
    public void UnregisterUnit(Unit unit)
    {
        _units.Remove(unit);
    }
    
    /// <summary>
    /// Register building as owned by this player
    /// </summary>
    public void RegisterBuilding(Building building)
    {
        if (!_buildings.Contains(building))
        {
            _buildings.Add(building);
        }
    }
    
    /// <summary>
    /// Unregister destroyed building
    /// </summary>
    public void UnregisterBuilding(Building building)
    {
        _buildings.Remove(building);
    }
    
    /// <summary>
    /// Get script counter value
    /// </summary>
    public int GetCounter(int index)
    {
        if (index < 0 || index >= _counters.Length)
            return 0;
        return _counters[index];
    }
    
    /// <summary>
    /// Set script counter value
    /// </summary>
    public void SetCounter(int index, int value)
    {
        if (index >= 0 && index < _counters.Length)
            _counters[index] = value;
    }
    
    /// <summary>
    /// Increment script counter
    /// </summary>
    public void IncrementCounter(int index, int amount = 1)
    {
        if (index >= 0 && index < _counters.Length)
            _counters[index] += amount;
    }
}

// File: src/OpenSage.Game/Logic/Player/Team.cs

public enum Team
{
    GLA = 1,
    USA = 2,
    China = 3,
    Spectator = 0,
}

// File: src/OpenSage.Game/Logic/Player/PlayerColor.cs

public enum PlayerColor
{
    Red,
    Blue,
    Green,
    Yellow,
    Black,
    White,
    Orange,
    Purple,
}
```

**Acceptance Criteria**:
- [ ] Players track units and buildings
- [ ] Money system works
- [ ] Team assignments correct
- [ ] Script counters accessible

**Effort**: 1 day

---

## Task 3: Object Update Contracts (PLAN-038)

**Objective**: Establish update interface for all game objects

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Object/GameObject.cs (extend existing)

public abstract class GameObject : DisposableBase
{
    // Core properties
    public uint Id { get; protected set; }
    public Coord3D Position { get; set; }
    public float Angle { get; set; }
    public Player Owner { get; set; }
    public ObjectTemplate Template { get; set; }
    
    // State
    public float Health { get; set; }
    public bool IsDestroyed { get; set; }
    public bool IsAlive => Health > 0;
    
    // Reference to game logic (set by GameLogic.AddObject)
    internal GameLogic GameLogic { get; set; }
    
    // Events
    public event Action OnSelected;
    public event Action OnDeselected;
    public event Action<DamageInfo> OnDamaged;
    public event Action<DamageInfo> OnDestroyed;
    
    protected GameObject(ObjectTemplate template, Player owner)
    {
        Id = GetNextId();
        Template = template;
        Owner = owner;
        Health = template.Health;
        IsDestroyed = false;
    }
    
    /// <summary>
    /// Called every frame at 60 Hz
    /// </summary>
    public virtual void Update(in TimeInterval gameTime)
    {
        // Override in derived classes
    }
    
    /// <summary>
    /// Called every authority tick (5 Hz / 12 frame intervals)
    /// </summary>
    public virtual void LogicUpdate()
    {
        // Override in derived classes
    }
    
    /// <summary>
    /// Apply damage to this object
    /// </summary>
    public virtual void TakeDamage(DamageInfo damage)
    {
        Health -= damage.Amount;
        
        OnDamaged?.Invoke(damage);
        
        if (Health <= 0)
        {
            OnDeath(damage);
        }
    }
    
    /// <summary>
    /// Called when object is destroyed
    /// </summary>
    protected virtual void OnDeath(DamageInfo damageInfo)
    {
        IsDestroyed = true;
        OnDestroyed?.Invoke(damageInfo);
        
        Owner?.UnregisterUnit(this as Unit);
        Owner?.UnregisterBuilding(this as Building);
    }
    
    public virtual void Render(RenderContext context)
    {
        // Override in derived classes
    }
    
    private static uint _nextId = 1;
    private static uint GetNextId() => _nextId++;
}

// File: src/OpenSage.Game/Logic/Object/Unit/Unit.cs (extend)

public sealed class Unit : GameObject
{
    public UnitTemplate UnitTemplate { get; }
    public float Speed { get; set; }
    
    // Locomotion
    private List<Coord3D> _currentPath;
    private int _pathIndex;
    private Locomotor _locomotor;
    
    // Combat
    private Weapon _weapon;
    private GameObject _currentTarget;
    private TargetingSystem _targetingSystem;
    
    // Commands
    private Queue<UnitCommand> _commandQueue = new();
    private UnitCommand _currentCommand;
    
    public Unit(UnitTemplate template, Player owner, Coord3D position)
        : base(template, owner)
    {
        UnitTemplate = template;
        Position = position;
        Speed = template.Speed;
        
        // Initialize weapon
        _weapon = new Weapon(template.WeaponTemplate);
        
        // Register with player
        owner?.RegisterUnit(this);
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        if (IsDestroyed)
            return;
        
        // Update locomotion
        if (_locomotor != null)
        {
            _locomotor.UpdatePosition(gameTime);
        }
        
        // Update weapon cooldown
        _weapon?.Update(gameTime);
        
        // Update particles and effects
        base.Update(gameTime);
    }
    
    public override void LogicUpdate()
    {
        if (IsDestroyed)
            return;
        
        // Process targeting and weapon firing
        if (_weapon != null)
        {
            // Find target if none
            if (_currentTarget == null || !_currentTarget.IsAlive)
            {
                var candidates = GameLogic?.GetObjectsInRange(Position, _weapon.MaxRange);
                _currentTarget = _targetingSystem?.FindBestTarget(this, candidates);
            }
            
            // Fire if possible
            if (_currentTarget != null && _weapon.CanFire())
            {
                FireWeapon(_currentTarget);
            }
        }
    }
    
    public void QueueCommand(UnitCommand command)
    {
        _commandQueue.Enqueue(command);
    }
    
    public void MoveTo(Coord3D destination)
    {
        // Pathfinding will be added in Phase 07A
        _currentPath = new List<Coord3D> { destination };
        _pathIndex = 0;
    }
    
    private void FireWeapon(GameObject target)
    {
        // Combat implementation in Phase 07B
        // For now, just mark as fired
        _weapon.Fire();
    }
}

// File: src/OpenSage.Game/Logic/Object/Building/Building.cs (new)

public sealed class Building : GameObject
{
    public BuildingTemplate BuildingTemplate { get; }
    
    public float ConstructionProgress { get; set; }
    public bool IsConstructing => ConstructionProgress < 1.0f;
    public bool IsOperational => ConstructionProgress >= 1.0f;
    
    private Queue<UnitProduction> _productionQueue = new();
    
    public Coord3D RallyPoint { get; set; }
    
    public Building(BuildingTemplate template, Player owner, Coord3D position)
        : base(template, owner)
    {
        BuildingTemplate = template;
        Position = position;
        ConstructionProgress = 0; // Starts under construction
        RallyPoint = position + Vector3.Forward * 10; // Default rally point
        
        // Register with player
        owner?.RegisterBuilding(this);
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        // Visual updates
        base.Update(gameTime);
    }
    
    public override void LogicUpdate()
    {
        if (IsDestroyed)
            return;
        
        // Construction progress tick (3 seconds per tick = 1/12 per frame)
        if (IsConstructing)
        {
            ConstructionProgress += 0.08333f; // 1/12
            if (ConstructionProgress >= 1.0f)
            {
                ConstructionProgress = 1.0f;
                OnConstructionComplete();
            }
        }
    }
    
    public void IncrementConstructionProgress(float amount)
    {
        ConstructionProgress += amount;
        if (ConstructionProgress >= 1.0f)
        {
            ConstructionProgress = 1.0f;
            OnConstructionComplete();
        }
    }
    
    private void OnConstructionComplete()
    {
        // Event: building completed
    }
    
    public void QueueProduction(ObjectTemplate unitTemplate)
    {
        var production = new UnitProduction
        {
            Template = unitTemplate,
            TimeRemaining = unitTemplate.BuildTime,
            Cost = unitTemplate.Cost,
        };
        
        _productionQueue.Enqueue(production);
    }
    
    public void UpdateProduction()
    {
        if (_productionQueue.Count == 0)
            return;
        
        var production = _productionQueue.Peek();
        production.TimeRemaining -= 1.0f / 12.0f; // 12 ticks per second
        
        if (production.TimeRemaining <= 0)
        {
            CompleteProduction();
        }
    }
    
    private void CompleteProduction()
    {
        if (_productionQueue.TryDequeue(out var production))
        {
            // Create unit at rally point
            var unit = new Unit(production.Template as UnitTemplate, Owner, RallyPoint);
            GameLogic?.AddObject(unit);
        }
    }
}

// File: src/OpenSage.Game/Logic/Object/UnitCommand.cs

public sealed class UnitCommand
{
    public UnitCommandType Type { get; set; }
    public Coord3D TargetPosition { get; set; }
    public GameObject TargetObject { get; set; }
}

public enum UnitCommandType
{
    Move,
    Attack,
    Stop,
    Guard,
}
```

**Acceptance Criteria**:
- [ ] All objects have Update() method
- [ ] Authority tick called separately
- [ ] Unit command queue working
- [ ] Building construction progress tracked

**Effort**: 2 days

---

## Task 4: Integration with Game.cs (PLAN-039)

**Objective**: Wire GameLogic into main game loop

**Implementation**:

```csharp
// File: src/OpenSage.Game/Game.cs (modify existing Step method)

public sealed class Game : DisposableBase, IGame
{
    private GameLogic _gameLogic;
    private SelectionManager _selectionManager; // From Phase 05
    private InputRouter _inputRouter; // From Phase 05
    
    public GameLogic GameLogic => _gameLogic;
    
    public Game(GameInstallation installation, ...)
    {
        // ... existing initialization ...
        
        _gameLogic = AddDisposable(new GameLogic(ContentManager));
        _selectionManager = new SelectionManager();
        _inputRouter = new InputRouter(Scene2D.WindowManager, _gameLogic, InputMessageBuffer);
        
        // Initialize test map
        InitializeTestMap();
    }
    
    public void Step()
    {
        // Process input messages
        InputMessageBuffer.PumpEvents(/* input messages */, GameTime);
        foreach (var message in InputMessageBuffer.Messages)
        {
            _inputRouter.ProcessInputMessage(message);
        }
        
        // Update game logic (60 Hz)
        _gameLogic.Update(GameTime);
        
        // Check game over
        if (_gameLogic.IsGameOver)
        {
            HandleGameOver(_gameLogic.Winner);
        }
    }
    
    private void HandleGameOver(Player winner)
    {
        if (winner != null)
        {
            Debug.LogInfo($"Game Over! {winner.Name} wins!");
        }
        else
        {
            Debug.LogInfo("Game Over!");
        }
        
        // Show victory/defeat screen
    }
    
    private void InitializeTestMap()
    {
        // Create test players
        var humanPlayer = new Player("Human", Team.USA, PlayerColor.Blue, isHuman: true);
        var computerPlayer = new Player("Computer", Team.GLA, PlayerColor.Red, isHuman: false);
        
        _gameLogic.AddPlayer(humanPlayer);
        _gameLogic.AddPlayer(computerPlayer);
        
        // Spawn initial units for testing
        var infantryTemplate = ContentManager.Load<UnitTemplate>("Unit_Infantry");
        
        var unit1 = new Unit(infantryTemplate, humanPlayer, new Coord3D(100, 100, 0));
        _gameLogic.AddObject(unit1);
        
        var unit2 = new Unit(infantryTemplate, computerPlayer, new Coord3D(200, 200, 0));
        _gameLogic.AddObject(unit2);
    }
}
```

**Acceptance Criteria**:
- [ ] GameLogic.Update() called each frame
- [ ] Objects persist between frames
- [ ] Game over detection working
- [ ] Test units spawn correctly

**Effort**: 1 day

---

## Testing

```csharp
[TestFixture]
public class GameLoopTests
{
    private GameLogic _gameLogic;
    private Player _player1;
    private Player _player2;
    
    [SetUp]
    public void Setup()
    {
        _gameLogic = new GameLogic(new MockContentManager());
        
        _player1 = new Player("Player1", Team.USA, PlayerColor.Blue, true);
        _player2 = new Player("Player2", Team.GLA, PlayerColor.Red, false);
        
        _gameLogic.AddPlayer(_player1);
        _gameLogic.AddPlayer(_player2);
    }
    
    [Test]
    public void ObjectPersistsAfterUpdate()
    {
        // Arrange
        var unit = new Unit(new MockUnitTemplate(), _player1, Coord3D.Zero);
        _gameLogic.AddObject(unit);
        var initialCount = _gameLogic.AllObjects.Count;
        
        // Act
        _gameLogic.Update(new TimeInterval());
        
        // Assert
        Assert.AreEqual(initialCount, _gameLogic.AllObjects.Count);
        Assert.IsTrue(_gameLogic.AllObjects.Contains(unit));
    }
    
    [Test]
    public void AuthorityTickOccursEvery12Frames()
    {
        // Arrange
        var building = new Building(new MockBuildingTemplate(), _player1, Coord3D.Zero);
        _gameLogic.AddObject(building);
        
        // Act
        for (int i = 0; i < 12; i++)
        {
            _gameLogic.Update(new TimeInterval(TimeSpan.FromMilliseconds(16.67)));
        }
        
        // Assert
        // Construction progress should have incremented once (at frame 11, 0-indexed)
        Assert.Greater(building.ConstructionProgress, 0);
    }
    
    [Test]
    public void GameOverWhenPlayerEliminated()
    {
        // Arrange
        var unit1 = new Unit(new MockUnitTemplate(), _player1, Coord3D.Zero);
        _gameLogic.AddObject(unit1);
        
        // Act
        unit1.IsDestroyed = true;
        _gameLogic.Update(new TimeInterval());
        
        // Assert
        Assert.IsTrue(_gameLogic.IsGameOver);
        Assert.AreEqual(_player2, _gameLogic.Winner);
    }
}
```

---

## Integration Checklist

- [ ] GameLogic refactored with object management
- [ ] Player system implemented
- [ ] Update contracts established for all objects
- [ ] Game.cs Step() calls GameLogic.Update()
- [ ] Test map initializes correctly
- [ ] Objects persist between frames
- [ ] Win conditions evaluated
- [ ] No crashes after 30+ minutes
- [ ] All unit tests passing

---

## Success Metrics

After Phase 06:

✅ Objects persist between frames  
✅ Game runs for 30+ minutes without crash  
✅ State managed correctly (no data loss)  
✅ Players tracked properly  
✅ Win conditions evaluate correctly  
✅ Ready for Phase 07A (Pathfinding)  

---

## Next Phase Dependencies

**Phase 07A (Pathfinding)** requires:
- GameLogic object management ✅
- Unit command queue ✅
- Update loop stable ✅

All prerequisites met!
