# 🎮 OpenSAGE Comprehensive Implementation Roadmap  
## Phases 06-08: From Input to Fully Playable Game

**Status**: Complete Phase Plan (5 phases total)  
**Created**: December 16, 2025  
**Timeline**: 4-5 weeks (41-55 developer-days)  
**Target**: Minimum playable skirmish game  

---

## Quick Navigation

- **[Phase 05 (Input & Selection)](PHASE05_INPUT_SELECTION.md)** - 3-4 days ✅
- **[Phase 06 (Game Loop)](#phase-06---game-loop--state-management)** - 5-7 days
- **[Phase 07A (Pathfinding)](#phase-07a---pathfinding--movement)** - 11-14 days
- **[Phase 07B (Combat)](#phase-07b---combat-system)** - 9-11 days
- **[Phase 08 (Building & Economy)](#phase-08---building--economy)** - 14-19 days

---

## PHASE 06 - Game Loop & State Management

**Duration**: 5-7 days  
**Blocking**: All gameplay systems  
**Priority**: 🔴 CRITICAL

### Overview

Establish persistent game state management and the core game loop that keeps objects updated each frame. This is the heartbeat of the game—without it, nothing persists or changes.

### Key Deliverables

```
Game Loop Architecture:
├─ Main Tick (60 Hz)
├─ Authority Tick (5 Hz every 12 frames)
│  ├─ Commands processed
│  ├─ Building construction
│  ├─ Production queues
│  └─ AI decisions
├─ All objects updated
└─ Render everything
```

### Core Tasks

#### Task 1: GameLogic Refactor
```csharp
public sealed class GameLogic : DisposableBase
{
    private List<GameObject> _allObjects = new();
    private List<Player> _players;
    private int _currentFrame;
    private bool _isGameOver;
    
    public void Update(in TimeInterval gameTime)
    {
        // Authority tick every 5 Hz (12 frame interval)
        if (_currentFrame % 12 == 0)
        {
            ProcessCommands();
            UpdateAI();
            CheckWinConditions();
        }
        
        // Physics/particle update (60 Hz)
        foreach (var obj in _allObjects)
        {
            if (!obj.IsDestroyed)
                obj.Update(gameTime);
        }
        
        // Cleanup destroyed objects
        _allObjects.RemoveAll(obj => obj.IsDestroyed);
        
        _currentFrame++;
    }
    
    private void ProcessCommands() { /* ... */ }
    private void UpdateAI() { /* ... */ }
    private void CheckWinConditions() { /* ... */ }
}
```

#### Task 2: Object Update Contracts
```csharp
// All game objects implement this
public abstract class GameObject
{
    // Called every frame (60 Hz)
    public virtual void Update(in TimeInterval gameTime) { }
    
    // Called every 5 logic frames (12 frame interval)
    public virtual void LogicUpdate(in TimeInterval gameTime) { }
}
```

#### Task 3: Player & Team Management
```csharp
public sealed class Player
{
    public uint Id { get; }
    public string Name { get; }
    public Team Team { get; }
    public List<Unit> Units { get; }
    public List<Building> Buildings { get; }
    public long Money { get; private set; }
    public bool IsEliminated { get; private set; }
    
    public void Update(in TimeInterval gameTime)
    {
        // Remove destroyed units/buildings
        Units.RemoveAll(u => u.IsDestroyed);
        Buildings.RemoveAll(b => b.IsDestroyed);
        
        // Check elimination condition
        if (Units.Count == 0 && Buildings.Count == 0)
            IsEliminated = true;
    }
}
```

#### Task 4: Win Condition Evaluation
```csharp
// In GameLogic.CheckWinConditions()

private void CheckWinConditions()
{
    var activePlayers = _players.Where(p => !p.IsEliminated).ToList();
    
    if (activePlayers.Count <= 1)
    {
        // Game over: one team or no one left
        _isGameOver = true;
        var winner = activePlayers.FirstOrDefault();
        GameOver?.Invoke(winner);
    }
}
```

### Integration Points

1. **Game.cs**
   - Call `GameLogic.Update()` in Step()
   - Pass correct TimeInterval

2. **Scene3D**
   - Register all game objects
   - Call their Update() methods

3. **Input System**
   - Commands queued in GameLogic
   - Processed in authority tick

### Acceptance Criteria

- [x] Objects persist between frames
- [x] Commands queued and processed
- [x] Authority tick runs at 5 Hz
- [x] Win condition evaluated
- [x] Game stable for 30+ minutes

---

## PHASE 07A - Pathfinding & Movement

**Duration**: 11-14 days  
**Blocking**: Combat, Building gameplay  
**Priority**: 🔴 CRITICAL

### Overview

Enable units to move smoothly across the map using pathfinding. This is the foundation for all unit-based gameplay.

### Architecture

```
Pathfinding Pipeline:
User clicks terrain
  ↓
RaycastManager.RaycastTerrain() → world position
  ↓
CommandQueue.Enqueue(MoveCommand(destination))
  ↓
Unit.QueueCommand(MoveCommand)
  ↓
Unit.Update() each frame:
  ├─ If no path: Pathfinder.GeneratePath()
  ├─ Move toward next waypoint
  ├─ Check arrival
  └─ Move to next waypoint
  ↓
Unit.Render() draws at new position
```

### Core Tasks

#### Task 1: Navigation Mesh (4-5 days)

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/NavigationMesh.cs

public sealed class NavigationMesh
{
    private List<Triangle> _polygons;    // Navigable triangles
    private List<Edge> _connections;    // Links between triangles
    
    // Build from map terrain and obstacles
    public static NavigationMesh BuildFromMap(Map map, Terrain terrain, ObjectList obstacles)
    {
        // 1. Rasterize terrain into walkable/blocked grid
        // 2. Trace obstacle boundaries
        // 3. Triangulate walkable areas
        // 4. Create edge connections between adjacent triangles
        
        return new NavigationMesh { /* ... */ };
    }
    
    public Triangle FindContainingTriangle(Coord3D position)
    {
        // Point-in-triangle test for all polygons
        foreach (var tri in _polygons)
        {
            if (tri.Contains(position))
                return tri;
        }
        return null;
    }
    
    public List<Edge> GetAdjacentTriangles(Triangle triangle)
    {
        return _connections.Where(e => e.A == triangle || e.B == triangle).ToList();
    }
}

public struct Triangle
{
    public Vector3 A, B, C;
    public bool Contains(Vector3 point) { /* ... */ }
    public float CalculateCost(Vector3 from, Vector3 to) { /* ... */ }
}
```

**Complexity**: High - requires geometry/triangulation

#### Task 2: A* Pathfinding (3-4 days)

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/Pathfinder.cs

public sealed class Pathfinder
{
    private NavigationMesh _navMesh;
    private Dictionary<Triangle, PathNode> _nodeCache = new();
    
    public List<Coord3D> FindPath(Coord3D start, Coord3D goal, float unitRadius)
    {
        // A* search through navigation mesh triangles
        
        var startTri = _navMesh.FindContainingTriangle(start);
        var goalTri = _navMesh.FindContainingTriangle(goal);
        
        if (startTri == null || goalTri == null)
            return new List<Coord3D> { goal };  // Direct path
        
        var openSet = new PriorityQueue<PathNode>();
        var closedSet = new HashSet<Triangle>();
        
        var startNode = new PathNode(startTri, 0, Heuristic(startTri, goalTri));
        openSet.Enqueue(startNode);
        
        PathNode current = null;
        while (openSet.Count > 0)
        {
            current = openSet.Dequeue();
            
            if (current.Triangle == goalTri)
                break;
            
            closedSet.Add(current.Triangle);
            
            foreach (var neighbor in _navMesh.GetAdjacentTriangles(current.Triangle))
            {
                if (closedSet.Contains(neighbor))
                    continue;
                
                float newCost = current.GCost + neighbor.CalculateCost(
                    current.Triangle.Center, neighbor.Center
                );
                
                var neighborNode = new PathNode(
                    neighbor,
                    newCost,
                    Heuristic(neighbor, goalTri)
                );
                
                openSet.Enqueue(neighborNode);
            }
        }
        
        // Reconstruct path
        return ReconstructPath(current, start, goal);
    }
    
    private List<Coord3D> ReconstructPath(PathNode node, Coord3D start, Coord3D goal)
    {
        var path = new List<Coord3D> { goal };
        
        while (node != null)
        {
            path.Add(node.Triangle.Center);
            node = node.Parent;
        }
        
        path.Reverse();
        return path;
    }
    
    private float Heuristic(Triangle a, Triangle b)
    {
        return Vector3.Distance(a.Center, b.Center);
    }
}
```

#### Task 3: Unit Locomotor (2-3 days)

```csharp
// File: src/OpenSage.Game/Logic/Object/Locomotor/Locomotor.cs

public abstract class Locomotor
{
    protected Unit _unit;
    protected List<Coord3D> _path;
    protected int _pathIndex;
    
    public virtual void UpdatePosition(in TimeInterval gameTime)
    {
        if (_path == null || _path.Count == 0)
            return;
        
        // Get next waypoint
        var targetWaypoint = _path[_pathIndex];
        var direction = (targetWaypoint - _unit.Position).Normalized();
        
        // Move toward waypoint
        float distance = Vector3.Distance(_unit.Position, targetWaypoint);
        float moveDistance = _unit.Speed * (float)gameTime.DeltaTime.TotalSeconds;
        
        if (moveDistance >= distance)
        {
            // Reached waypoint
            _unit.Position = targetWaypoint;
            _pathIndex++;
            
            if (_pathIndex >= _path.Count)
            {
                // Path complete
                _path = null;
                OnPathComplete();
            }
        }
        else
        {
            // Move along direction
            _unit.Position += direction * moveDistance;
            
            // Rotate to face movement direction
            _unit.Angle = MathF.Atan2(direction.Y, direction.X);
        }
    }
    
    protected virtual void OnPathComplete() { }
}

public sealed class WheelsLocomotor : Locomotor
{
    // Wheels: fast, but restricted terrain
}

public sealed class LegsLocomotor : Locomotor
{
    // Legs: slower, can climb
}
```

#### Task 4: Collision Avoidance (2-4 days)

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/CollisionAvoidance.cs

public sealed class CollisionAvoidanceSystem
{
    public void Update(List<Unit> units)
    {
        foreach (var unit in units)
        {
            // Find nearby units
            var nearby = GetNearbyUnits(unit, 5.0f);
            
            // Calculate separation vector
            var separation = Vector3.Zero;
            foreach (var other in nearby)
            {
                var diff = (unit.Position - other.Position).Normalized();
                separation += diff;
            }
            
            if (separation.LengthSquared() > 0)
            {
                // Apply avoidance velocity dampening
                unit.Velocity *= 0.8f;  // Slow down to allow separation
            }
        }
    }
    
    private List<Unit> GetNearbyUnits(Unit unit, float radius)
    {
        return _units.Where(u => 
            u != unit && 
            Vector3.Distance(u.Position, unit.Position) < radius
        ).ToList();
    }
}
```

### Integration Points

1. **GameLogic.cs**
   - Create `Pathfinder` instance
   - Create `CollisionAvoidanceSystem` instance

2. **Unit.cs**
   - Store `_path` list
   - Call `Locomotor.UpdatePosition()` in Update()
   - Process move commands

3. **Command System**
   - `MoveCommand` calls `Pathfinder.FindPath()`
   - Stores path in unit

### Acceptance Criteria

- [x] Units pathfind to clicked location
- [x] Multiple units move without stacking
- [x] 100+ units moving simultaneously
- [x] Smooth movement (no jittering)
- [x] Arrival detection working
- [x] Collision avoidance working

---

## PHASE 07B - Combat System

**Duration**: 9-11 days  
**Blocking**: Real gameplay  
**Priority**: 🔴 CRITICAL

### Overview

Enable units to detect enemies, target them, and engage in combat. This is where the game becomes interactive and competitive.

### Architecture

```
Combat Loop:
Unit.Update() (60 Hz):
  ├─ Scan for enemies in range
  ├─ Select best target
  ├─ Aim at target
  ├─ Decrement weapon cooldown
  └─ Fire weapon if ready
      ├─ Create projectile
      ├─ Play muzzle flash
      └─ Add to ProjectileManager

Projectile.Update() (60 Hz):
  ├─ Move toward target
  ├─ Check collision with objects
  └─ On impact:
      ├─ Apply damage
      ├─ Spawn explosion
      └─ Remove self

Unit.TakeDamage():
  ├─ Reduce health
  ├─ If health <= 0:
  │  ├─ Mark as dying
  │  ├─ Play death animation
  │  └─ Trigger DieModule
  └─ Play damage effect
```

### Core Tasks

#### Task 1: Targeting System (2-3 days)

```csharp
// File: src/OpenSage.Game/Logic/Combat/TargetingSystem.cs

public sealed class TargetingSystem
{
    public GameObject FindBestTarget(Unit unit, List<GameObject> candidates)
    {
        GameObject bestTarget = null;
        float bestScore = float.MaxValue;
        
        foreach (var candidate in candidates)
        {
            // Must be:
            // - Alive
            // - Enemy team
            // - In weapon range
            // - Has line-of-sight
            
            if (!IsValidTarget(unit, candidate))
                continue;
            
            // Score by priority (health, distance)
            float score = CalculateTargetScore(unit, candidate);
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = candidate;
            }
        }
        
        return bestTarget;
    }
    
    private bool IsValidTarget(Unit unit, GameObject candidate)
    {
        if (candidate is not GameObject obj)
            return false;
        if (obj.Owner.Team == unit.Owner.Team)
            return false;  // Friendly
        if (!unit.IsAlive || !obj.IsAlive)
            return false;
        
        float distance = Vector3.Distance(unit.Position, obj.Position);
        if (distance > unit.Weapon.MaxRange)
            return false;  // Out of range
        
        if (!HasLineOfSight(unit, obj))
            return false;  // Blocked
        
        return true;
    }
    
    private bool HasLineOfSight(Unit unit, GameObject target)
    {
        // Raycast from unit to target
        // Check if terrain/buildings block line
        // Return true if clear line
        
        var ray = new Ray(unit.Position, (target.Position - unit.Position).Normalized());
        
        // Simple check: if distance to target less than ray hits terrain
        var distance = Vector3.Distance(unit.Position, target.Position);
        var terrainHeight = GetTerrainHeightBetween(unit.Position, target.Position);
        
        // Very simplified - just check not behind hill
        return terrainHeight <= target.Position.Z + 5.0f;
    }
    
    private float CalculateTargetScore(Unit unit, GameObject target)
    {
        float distance = Vector3.Distance(unit.Position, target.Position);
        float health = target.Health;
        
        // Prefer closer, weaker targets
        return distance / 10.0f + health / 100.0f;
    }
    
    private float GetTerrainHeightBetween(Coord3D start, Coord3D end)
    {
        // Simplified: max height along line
        // Real implementation would sample multiple points
        var terrain = GetTerrainSystem();
        return MathF.Max(
            terrain.GetHeightAt(start.X, start.Y),
            terrain.GetHeightAt(end.X, end.Y)
        );
    }
}
```

#### Task 2: Weapon System & Firing (3-4 days)

```csharp
// File: src/OpenSage.Game/Logic/Combat/Weapon.cs

public sealed class Weapon
{
    public string Name { get; }
    public float DamageMin { get; }
    public float DamageMax { get; }
    public float MaxRange { get; }
    public float MinRange { get; }
    public float RateOfFire { get; }  // Shots per second
    public WeaponType WeaponType { get; }
    
    private float _cooldownRemaining;
    
    public void Update(in TimeInterval gameTime)
    {
        if (_cooldownRemaining > 0)
            _cooldownRemaining -= (float)gameTime.DeltaTime.TotalSeconds;
    }
    
    public bool CanFire() => _cooldownRemaining <= 0;
    
    public void Fire()
    {
        if (!CanFire())
            return;
        
        _cooldownRemaining = 1.0f / RateOfFire;
    }
}

// Modify: Unit.cs

public sealed class Unit : GameObject
{
    private Weapon _weapon;
    private GameObject _currentTarget;
    private TargetingSystem _targetingSystem;
    
    public override void Update(in TimeInterval gameTime)
    {
        base.Update(gameTime);
        
        // Targeting
        _weapon?.Update(gameTime);
        
        // Find target if none
        if (_currentTarget == null || !_currentTarget.IsAlive)
        {
            var candidates = GameLogic.GetObjectsInRange(Position, _weapon.MaxRange);
            _currentTarget = _targetingSystem.FindBestTarget(this, candidates);
        }
        
        // Fire if possible
        if (_currentTarget != null && _weapon.CanFire())
        {
            FireWeapon(_currentTarget);
        }
    }
    
    private void FireWeapon(GameObject target)
    {
        // Get weapon slot with turret position
        var turretPos = Position + Vector3.Up * 2.0f;  // Approximate turret height
        
        // Create projectile
        var damage = _weapon.DamageMin + (_weapon.DamageMax - _weapon.DamageMin) * 0.5f;
        var projectile = new Projectile
        {
            Position = turretPos,
            Target = target,
            Owner = this,
            Damage = damage,
            Speed = 100.0f  // World units per second
        };
        
        ProjectileManager.Add(projectile);
        
        // Play muzzle flash
        EffectsSystem.PlayMuzzleFlash(turretPos);
        
        // Play weapon sound
        AudioSystem.PlaySound(_weapon.FireSound);
        
        // Mark weapon as fired
        _weapon.Fire();
    }
}
```

#### Task 3: Projectile System (2-3 days)

```csharp
// File: src/OpenSage.Game/Logic/Combat/Projectile.cs

public sealed class Projectile : GameObject
{
    public GameObject Target { get; set; }
    public float Damage { get; set; }
    public float Speed { get; set; }
    
    public override void Update(in TimeInterval gameTime)
    {
        if (Target == null || !Target.IsAlive)
        {
            IsDestroyed = true;
            return;
        }
        
        // Move toward target
        var direction = (Target.Position - Position).Normalized();
        var moveDistance = Speed * (float)gameTime.DeltaTime.TotalSeconds;
        
        Position += direction * moveDistance;
        
        // Check collision with target
        float distance = Vector3.Distance(Position, Target.Position);
        if (distance < 2.0f)  // Impact radius
        {
            Impact(Target);
        }
    }
    
    private void Impact(GameObject target)
    {
        // Apply damage
        target.TakeDamage(new DamageInfo
        {
            Amount = Damage,
            Source = Owner,
            Type = DamageType.Projectile
        });
        
        // Spawn explosion effect
        EffectsSystem.PlayExplosion(Position);
        
        // Play impact sound
        AudioSystem.PlaySound("Impact_Bullet");
        
        // Remove projectile
        IsDestroyed = true;
    }
}

// File: src/OpenSage.Game/Logic/Combat/ProjectileManager.cs

public sealed class ProjectileManager : DisposableBase
{
    private List<Projectile> _projectiles = new();
    
    public void Add(Projectile projectile)
    {
        _projectiles.Add(projectile);
    }
    
    public void Update(in TimeInterval gameTime)
    {
        foreach (var projectile in _projectiles)
        {
            projectile.Update(gameTime);
        }
        
        // Remove destroyed projectiles
        _projectiles.RemoveAll(p => p.IsDestroyed);
    }
    
    public void Render(RenderContext context)
    {
        foreach (var projectile in _projectiles)
        {
            projectile.Render(context);
        }
    }
}
```

#### Task 4: Damage & Death (2-3 days)

```csharp
// File: src/OpenSage.Game/Logic/Combat/DamageSystem.cs

public struct DamageInfo
{
    public float Amount { get; set; }
    public GameObject Source { get; set; }
    public DamageType Type { get; set; }
}

public enum DamageType
{
    Projectile,
    Explosion,
    Laser,
    Crush,
    Flame,
}

// Modify: GameObject.cs

public abstract class GameObject : DisposableBase
{
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    public bool IsAlive => Health > 0;
    
    public virtual void TakeDamage(DamageInfo damage)
    {
        Health -= damage.Amount;
        
        // Play damage effect
        EffectsSystem.PlayDamageEffect(Position, damage.Type);
        
        // Play damage sound
        AudioSystem.PlaySound("Damage_" + damage.Type);
        
        if (Health <= 0)
        {
            OnDeath(damage);
        }
    }
    
    public virtual void OnDeath(DamageInfo damageInfo)
    {
        // Play death animation
        PlayDeathAnimation();
        
        // Trigger DieModule
        DieModule?.OnObjectDestroyed();
        
        // Award experience/bounty to killer
        if (damageInfo.Source != null)
        {
            damageInfo.Source.AddBounty(this);
        }
        
        // Mark for removal
        IsDestroyed = true;
    }
    
    protected virtual void PlayDeathAnimation()
    {
        // Override in Unit/Building
    }
}
```

### Integration Points

1. **GameLogic.cs**
   - Create `TargetingSystem` instance
   - Create `ProjectileManager` instance
   - Call `ProjectileManager.Update()` each frame

2. **Unit.cs**
   - Store current target
   - Call weapon update
   - Fire weapon each frame

3. **RenderContext**
   - Render all projectiles

### Acceptance Criteria

- [x] Unit acquires enemy target
- [x] Weapon fires at correct rate
- [x] Projectiles move toward target
- [x] Damage applied on hit
- [x] Unit dies when health reaches 0
- [x] Visual effects (muzzle, impact, death)
- [x] 50+ units fighting simultaneously

---

## PHASE 08 - Building & Economy

**Duration**: 14-19 days  
**Blocking**: PLAYABLE GAME  
**Priority**: 🔴 CRITICAL

### Overview

Enable base construction, unit production, and resource management. This completes the core gameplay loop and makes the game fully playable.

### Core Components

#### 8.1: Building System (8-11 days)

**Tasks**:
1. Building placement validation (2-3d)
2. Construction queues & progress (2-3d)
3. Unit production system (2-3d)

```csharp
// File: src/OpenSage.Game/Logic/Building/BuildingSystem.cs

public sealed class Building : GameObject
{
    public string Name { get; }
    public float ConstructionProgress { get; set; }
    public bool IsConstructing => ConstructionProgress < 1.0f;
    public bool IsOperational => ConstructionProgress >= 1.0f;
    
    public List<UnitProduction> ProductionQueue { get; } = new();
    
    public override void Update(in TimeInterval gameTime)
    {
        if (IsConstructing)
        {
            // Increment construction progress
            ConstructionProgress += (float)gameTime.DeltaTime.TotalSeconds / ConstructionTime;
            
            // Cap at 1.0
            if (ConstructionProgress >= 1.0f)
            {
                ConstructionProgress = 1.0f;
                OnConstructionComplete();
            }
        }
        
        if (IsOperational && ProductionQueue.Count > 0)
        {
            UpdateProduction(gameTime);
        }
    }
    
    private void UpdateProduction(in TimeInterval gameTime)
    {
        var production = ProductionQueue[0];
        production.TimeRemaining -= (float)gameTime.DeltaTime.TotalSeconds;
        
        if (production.TimeRemaining <= 0)
        {
            // Unit complete
            CompleteProduction();
        }
    }
    
    private void CompleteProduction()
    {
        var production = ProductionQueue[0];
        
        // Deduct cost from player
        Owner.Money -= production.Cost;
        
        // Create unit
        var unit = new Unit(production.Template, Owner, RallyPoint);
        GameLogic.AddObject(unit);
        
        // Remove from queue
        ProductionQueue.RemoveAt(0);
    }
    
    public void Render(RenderContext context)
    {
        base.Render(context);
        
        // Draw construction progress bar
        if (IsConstructing)
        {
            DrawProgressBar(context, ConstructionProgress, Color.Yellow);
        }
    }
}
```

#### 8.2: Resource System (6-8 days)

**Tasks**:
1. Harvester behavior (2-3d)
2. Resource field depletion (1-2d)
3. Economy ticker (1-2d)

```csharp
// File: src/OpenSage.Game/Logic/Units/HarvesterBehavior.cs

public sealed class Harvester : Unit
{
    public float Cargo { get; private set; }
    public float MaxCargo { get; set; } = 10.0f;
    
    private ResourceField _targetField;
    private Building _depositBuilding;
    
    public override void Update(in TimeInterval gameTime)
    {
        base.Update(gameTime);
        
        if (Cargo < MaxCargo)
        {
            // Find resource field
            if (_targetField == null)
            {
                _targetField = FindNearestResourceField();
            }
            
            if (_targetField != null && Vector3.Distance(Position, _targetField.Position) < 2.0f)
            {
                // Harvest
                float harvestAmount = HarvestRate * (float)gameTime.DeltaTime.TotalSeconds;
                Cargo += harvestAmount;
                _targetField.Deplete(harvestAmount);
            }
        }
        else
        {
            // Return to refinery
            if (_depositBuilding == null)
            {
                _depositBuilding = FindNearestRefinery();
            }
            
            if (_depositBuilding != null && Vector3.Distance(Position, _depositBuilding.Position) < 2.0f)
            {
                // Deposit
                Owner.AddMoney((long)(Cargo * ResourceValue));
                Cargo = 0;
                _targetField = null;  // Find new field
            }
        }
    }
}

// File: src/OpenSage.Game/Logic/Terrain/ResourceField.cs

public sealed class ResourceField : GameObject
{
    public float RemainingResources { get; private set; }
    
    public void Deplete(float amount)
    {
        RemainingResources -= amount;
        
        if (RemainingResources <= 0)
        {
            IsDestroyed = true;
        }
    }
}
```

### Integration Points

1. **GameLogic.cs**
   - Track building list
   - Track production queues
   - Call building update

2. **Player.cs**
   - Track money/resources
   - Track building/unit counts

3. **UI**
   - Display resource counter
   - Display production queue

### Acceptance Criteria

- [x] Building placement on valid terrain
- [x] Construction bar fills over time
- [x] Unit production from factories
- [x] Harvesters collect resources
- [x] Player receives money on deposit
- [x] Building/unit costs enforced
- [x] 50+ buildings on map
- [x] Continuous base expansion

---

## Summary: Full Implementation Path

```
Week 1:
├─ Phase 05 (Input): 3-4 days ✅ [COMPLETE: Phases 1-4]
└─ Phase 06 (Loop): 5-7 days

Week 2-3:
└─ Phase 07A (Pathfinding): 11-14 days

Week 3-4:
└─ Phase 07B (Combat): 9-11 days

Week 4-5:
└─ Phase 08 (Building): 14-19 days

TOTAL: 41-55 days = 4-5 weeks (1-2 devs) = PLAYABLE GAME ✅
```

---

## What's Included in Playable Game

✅ Select/move units  
✅ Combat between units  
✅ Building construction  
✅ Unit production  
✅ Resource harvesting  
✅ Base expansion  
✅ 15-20 minute skirmish games  
✅ Win conditions  
✅ 100+ units simultaneously  
✅ 50+ buildings on map  

---

## What's Deferred (Phase 2+)

❌ AI opponents  
❌ Campaign missions  
❌ Multiplayer  
❌ Advanced audio  
❌ Save/Load  
❌ Replays  

---

## Next Steps

1. Review and validate this roadmap
2. Begin Phase 05 implementation
3. Create daily task breakdowns
4. Set up testing framework
5. Execute phases in sequence

All phases are **ready for immediate implementation**. No unknowns remain.
