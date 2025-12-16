# Phase 07A: Pathfinding & Movement

**Phase Identifier**: PHASE07A_PATHFINDING_MOVEMENT  
**Status**: Ready for Implementation  
**Priority**: 🔴 CRITICAL (Core gameplay foundation)  
**Estimated Duration**: 11-14 days  
**Target Completion**: Units moving smoothly across map  

---

## Overview

Enable units to navigate the game world smoothly using pathfinding algorithms. This is the foundation for all unit-based gameplay—without movement, units are statues.

**Current State**: 0% (no navigation mesh, no pathfinding, no locomotion)  
**Target State**: 100% (100+ units moving smoothly with collision avoidance)

---

## Architecture Overview

```
Player clicks terrain
  ↓
RaycastManager converts click to world position (from Phase 05)
  ↓
MoveCommand(destination) queued
  ↓
Unit.Update() processes command:
  ├─ No path? → Call Pathfinder.FindPath(start, destination)
  ├─ Path received? → Store waypoints
  ├─ Move toward next waypoint using Locomotor
  ├─ Check collision with nearby units (CollisionAvoidance)
  └─ Check arrival at waypoint
  ↓
Unit.Render() draws at new position
```

---

## Task 1: Navigation Mesh Generation (PLAN-040)

**Objective**: Build navigable mesh from terrain and obstacles

**Duration**: 3-4 days

**Key Concepts**:
- Walkable areas (terrain)
- Blocked areas (buildings, cliffs)
- Triangulation of walkable space
- Connectivity graph between triangles

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/NavigationMesh.cs

public sealed class NavigationMesh : DisposableBase
{
    private List<NavTriangle> _triangles = new();
    private List<NavEdge> _edges = new();
    private int _gridWidth, _gridHeight;
    private float _gridScale;
    
    public IReadOnlyList<NavTriangle> Triangles => _triangles;
    public int TriangleCount => _triangles.Count;
    
    /// <summary>
    /// Build navigation mesh from terrain and obstacles
    /// </summary>
    public static NavigationMesh BuildFromMap(
        Terrain terrain,
        ObjectList obstacles,
        float gridScale = 1.0f)
    {
        var navMesh = new NavigationMesh
        {
            _gridWidth = (int)(terrain.Width / gridScale),
            _gridHeight = (int)(terrain.Height / gridScale),
            _gridScale = gridScale,
        };
        
        // Step 1: Create walkable grid
        bool[,] walkableGrid = CreateWalkableGrid(terrain, obstacles, gridScale);
        
        // Step 2: Trace contours of walkable regions
        var regions = TraceWalkableRegions(walkableGrid);
        
        // Step 3: Triangulate each region
        foreach (var region in regions)
        {
            var triangles = TriangulateRegion(region);
            navMesh._triangles.AddRange(triangles);
        }
        
        // Step 4: Connect adjacent triangles
        navMesh.ConnectTriangles();
        
        return navMesh;
    }
    
    /// <summary>
    /// Find the triangle containing a position
    /// </summary>
    public NavTriangle FindTriangleAtPosition(Coord3D position)
    {
        foreach (var tri in _triangles)
        {
            if (tri.Contains(position))
            {
                return tri;
            }
        }
        
        // Point is outside mesh - return closest triangle
        return FindClosestTriangle(position);
    }
    
    /// <summary>
    /// Get adjacent walkable triangles
    /// </summary>
    public List<NavTriangle> GetAdjacentTriangles(NavTriangle triangle)
    {
        var result = new List<NavTriangle>();
        
        foreach (var edge in triangle.Edges)
        {
            if (edge.TriangleA == triangle)
            {
                if (edge.TriangleB != null)
                    result.Add(edge.TriangleB);
            }
            else
            {
                if (edge.TriangleA != null)
                    result.Add(edge.TriangleA);
            }
        }
        
        return result;
    }
    
    private static bool[,] CreateWalkableGrid(
        Terrain terrain,
        ObjectList obstacles,
        float gridScale)
    {
        int gridWidth = (int)(terrain.Width / gridScale);
        int gridHeight = (int)(terrain.Height / gridScale);
        
        bool[,] walkable = new bool[gridWidth, gridHeight];
        
        // Mark all as walkable by default
        for (int x = 0; x < gridWidth; x++)
        {
            for (int y = 0; y < gridHeight; y++)
            {
                walkable[x, y] = true;
            }
        }
        
        // Mark steep slopes as unwalkable
        float maxSlope = 0.7f; // 45 degrees
        for (int x = 0; x < gridWidth - 1; x++)
        {
            for (int y = 0; y < gridHeight - 1; y++)
            {
                float h1 = terrain.GetHeightAt(x * gridScale, y * gridScale);
                float h2 = terrain.GetHeightAt((x + 1) * gridScale, y * gridScale);
                
                float slope = MathF.Abs(h2 - h1) / gridScale;
                if (slope > maxSlope)
                {
                    walkable[x, y] = false;
                }
            }
        }
        
        // Mark obstacle locations as unwalkable
        foreach (var obstacle in obstacles)
        {
            // Convert obstacle bounds to grid coordinates
            var boundsMin = obstacle.Position - new Vector3(obstacle.Bounds.Radius, obstacle.Bounds.Radius, 0);
            var boundsMax = obstacle.Position + new Vector3(obstacle.Bounds.Radius, obstacle.Bounds.Radius, 0);
            
            int minX = (int)(boundsMin.X / gridScale);
            int maxX = (int)(boundsMax.X / gridScale);
            int minY = (int)(boundsMin.Y / gridScale);
            int maxY = (int)(boundsMax.Y / gridScale);
            
            for (int x = minX; x <= maxX && x < gridWidth; x++)
            {
                for (int y = minY; y <= maxY && y < gridHeight; y++)
                {
                    if (x >= 0 && y >= 0)
                        walkable[x, y] = false;
                }
            }
        }
        
        return walkable;
    }
    
    private static List<NavTriangle> TriangulateRegion(List<Coord3D> region)
    {
        // Simple ear clipping triangulation
        var triangles = new List<NavTriangle>();
        
        if (region.Count < 3)
            return triangles;
        
        // Find center point
        Vector3 center = Vector3.Zero;
        foreach (var p in region)
            center += p;
        center /= region.Count;
        
        // Create triangles from center to each edge
        for (int i = 0; i < region.Count; i++)
        {
            int next = (i + 1) % region.Count;
            
            var tri = new NavTriangle
            {
                VertexA = center,
                VertexB = region[i],
                VertexC = region[next],
            };
            
            triangles.Add(tri);
        }
        
        return triangles;
    }
    
    private void ConnectTriangles()
    {
        // Find adjacent triangles and create edges between them
        for (int i = 0; i < _triangles.Count; i++)
        {
            for (int j = i + 1; j < _triangles.Count; j++)
            {
                var tri1 = _triangles[i];
                var tri2 = _triangles[j];
                
                // Check if triangles share an edge
                if (TrianglesShareEdge(tri1, tri2))
                {
                    var edge = new NavEdge(tri1, tri2);
                    _edges.Add(edge);
                    
                    tri1.Edges.Add(edge);
                    tri2.Edges.Add(edge);
                }
            }
        }
    }
    
    private bool TrianglesShareEdge(NavTriangle tri1, NavTriangle tri2)
    {
        // Check if two vertices of tri1 match two vertices of tri2
        var tri1Verts = new[] { tri1.VertexA, tri1.VertexB, tri1.VertexC };
        var tri2Verts = new[] { tri2.VertexA, tri2.VertexB, tri2.VertexC };
        
        int matches = 0;
        foreach (var v1 in tri1Verts)
        {
            foreach (var v2 in tri2Verts)
            {
                if (Vector3.Distance(v1, v2) < 0.01f)
                    matches++;
            }
        }
        
        return matches >= 2; // Share at least 2 vertices = share edge
    }
    
    private NavTriangle FindClosestTriangle(Coord3D position)
    {
        NavTriangle closest = null;
        float minDistance = float.MaxValue;
        
        foreach (var tri in _triangles)
        {
            float distance = Vector3.Distance(position, tri.Center);
            if (distance < minDistance)
            {
                minDistance = distance;
                closest = tri;
            }
        }
        
        return closest;
    }
    
    private static List<List<Coord3D>> TraceWalkableRegions(bool[,] grid)
    {
        // Placeholder: simplified contour tracing
        // Real implementation would use more sophisticated algorithm (e.g., marching squares)
        
        var regions = new List<List<Coord3D>>();
        
        // For now, create single region as rectangle of walkable area
        var region = new List<Coord3D>
        {
            new Coord3D(0, 0, 0),
            new Coord3D(grid.GetLength(0), 0, 0),
            new Coord3D(grid.GetLength(0), grid.GetLength(1), 0),
            new Coord3D(0, grid.GetLength(1), 0),
        };
        
        regions.Add(region);
        return regions;
    }
}

// File: src/OpenSage.Game/Logic/Pathfinding/NavTriangle.cs

public sealed class NavTriangle
{
    public Vector3 VertexA { get; set; }
    public Vector3 VertexB { get; set; }
    public Vector3 VertexC { get; set; }
    
    public List<NavEdge> Edges { get; set; } = new();
    
    public Vector3 Center
    {
        get => (VertexA + VertexB + VertexC) / 3f;
    }
    
    public float Area
    {
        get
        {
            var ab = VertexB - VertexA;
            var ac = VertexC - VertexA;
            return Vector3.Cross(ab, ac).Length() * 0.5f;
        }
    }
    
    public bool Contains(Vector3 point, float heightTolerance = 2.0f)
    {
        // Point-in-triangle test (2D, ignoring Z)
        var v0 = VertexC - VertexA;
        var v1 = VertexB - VertexA;
        var v2 = point - VertexA;
        
        float dot00 = Vector3.Dot(v0, v0);
        float dot01 = Vector3.Dot(v0, v1);
        float dot02 = Vector3.Dot(v0, v2);
        float dot11 = Vector3.Dot(v1, v1);
        float dot12 = Vector3.Dot(v1, v2);
        
        float invDenom = 1f / (dot00 * dot11 - dot01 * dot01);
        float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
        float v = (dot00 * dot12 - dot01 * dot02) * invDenom;
        
        return (u >= 0) && (v >= 0) && (u + v <= 1);
    }
}

// File: src/OpenSage.Game/Logic/Pathfinding/NavEdge.cs

public sealed class NavEdge
{
    public NavTriangle TriangleA { get; set; }
    public NavTriangle TriangleB { get; set; }
    
    public NavEdge(NavTriangle triA, NavTriangle triB)
    {
        TriangleA = triA;
        TriangleB = triB;
    }
    
    public float Cost => 1.0f; // Can be modified for terrain types
}
```

**Acceptance Criteria**:
- [ ] Navigation mesh generates from terrain
- [ ] Obstacles create blocked areas
- [ ] Triangles properly connected
- [ ] Point-in-triangle queries work
- [ ] NavMesh persists for pathfinding queries

**Effort**: 3-4 days

---

## Task 2: A* Pathfinding Algorithm (PLAN-041)

**Objective**: Implement A* search on navigation mesh

**Duration**: 3-4 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/Pathfinder.cs

public sealed class Pathfinder : DisposableBase
{
    private NavigationMesh _navMesh;
    private Dictionary<NavTriangle, float> _gCosts = new();
    private Dictionary<NavTriangle, NavTriangle> _cameFrom = new();
    
    public Pathfinder(NavigationMesh navMesh)
    {
        _navMesh = navMesh ?? throw new ArgumentNullException(nameof(navMesh));
    }
    
    /// <summary>
    /// Find path from start to goal using A* algorithm
    /// </summary>
    public List<Coord3D> FindPath(Coord3D start, Coord3D goal)
    {
        // Find triangles containing start and goal
        var startTri = _navMesh.FindTriangleAtPosition(start);
        var goalTri = _navMesh.FindTriangleAtPosition(goal);
        
        if (startTri == null || goalTri == null)
        {
            // Direct path if outside mesh
            return new List<Coord3D> { goal };
        }
        
        // If start and goal in same triangle, direct path
        if (startTri == goalTri)
        {
            return new List<Coord3D> { goal };
        }
        
        // A* search
        var openSet = new PriorityQueue<PathNode>();
        var closedSet = new HashSet<NavTriangle>();
        
        _gCosts.Clear();
        _cameFrom.Clear();
        
        float heuristic = Heuristic(startTri, goalTri);
        var startNode = new PathNode(startTri, 0, heuristic);
        
        openSet.Enqueue(startNode);
        _gCosts[startTri] = 0;
        
        PathNode goalNode = null;
        
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            
            if (current.Triangle == goalTri)
            {
                goalNode = current;
                break;
            }
            
            closedSet.Add(current.Triangle);
            
            // Check all adjacent triangles
            var neighbors = _navMesh.GetAdjacentTriangles(current.Triangle);
            foreach (var neighbor in neighbors)
            {
                if (closedSet.Contains(neighbor))
                    continue;
                
                // Calculate cost to neighbor
                float movementCost = Vector3.Distance(
                    current.Triangle.Center,
                    neighbor.Center
                );
                
                float newGCost = _gCosts[current.Triangle] + movementCost;
                
                // If we found a better path to neighbor, record it
                if (!_gCosts.ContainsKey(neighbor) || newGCost < _gCosts[neighbor])
                {
                    _gCosts[neighbor] = newGCost;
                    _cameFrom[neighbor] = current.Triangle;
                    
                    float fCost = newGCost + Heuristic(neighbor, goalTri);
                    var neighborNode = new PathNode(neighbor, newGCost, fCost);
                    
                    openSet.Enqueue(neighborNode);
                }
            }
        }
        
        if (goalNode == null)
        {
            // No path found - return direct path as fallback
            return new List<Coord3D> { goal };
        }
        
        // Reconstruct path
        return ReconstructPath(goalNode.Triangle, start, goal);
    }
    
    private List<Coord3D> ReconstructPath(NavTriangle goalTri, Coord3D start, Coord3D goal)
    {
        var path = new List<Coord3D>();
        var current = goalTri;
        
        // Trace back through cameFrom map
        while (_cameFrom.ContainsKey(current))
        {
            path.Add(current.Center);
            current = _cameFrom[current];
        }
        
        path.Add(start);
        path.Reverse();
        path.Add(goal);
        
        // Simplify path (remove waypoints in straight line)
        return SimplifyPath(path);
    }
    
    private List<Coord3D> SimplifyPath(List<Coord3D> path)
    {
        if (path.Count < 3)
            return path;
        
        var simplified = new List<Coord3D> { path[0] };
        
        for (int i = 1; i < path.Count - 1; i++)
        {
            var prev = simplified[simplified.Count - 1];
            var current = path[i];
            var next = path[i + 1];
            
            // Check if current point is in line between prev and next
            if (!IsCollinear(prev, current, next))
            {
                simplified.Add(current);
            }
        }
        
        simplified.Add(path[path.Count - 1]);
        return simplified;
    }
    
    private bool IsCollinear(Vector3 a, Vector3 b, Vector3 c, float tolerance = 0.1f)
    {
        // Check if b is approximately on line from a to c
        var ab = b - a;
        var ac = c - a;
        var cross = Vector3.Cross(ab, ac);
        
        return cross.Length() < tolerance;
    }
    
    private float Heuristic(NavTriangle from, NavTriangle to)
    {
        // Euclidean distance heuristic
        return Vector3.Distance(from.Center, to.Center);
    }
}

// File: src/OpenSage.Game/Logic/Pathfinding/PathNode.cs

public sealed class PathNode : IComparable<PathNode>
{
    public NavTriangle Triangle { get; set; }
    public float GCost { get; set; }  // Cost from start
    public float FCost { get; set; }  // GCost + Heuristic
    
    public PathNode(NavTriangle triangle, float gCost, float fCost)
    {
        Triangle = triangle;
        GCost = gCost;
        FCost = fCost;
    }
    
    public int CompareTo(PathNode other)
    {
        return FCost.CompareTo(other.FCost);
    }
}
```

**Acceptance Criteria**:
- [ ] A* finds optimal path on mesh
- [ ] Path simplification removes unnecessary waypoints
- [ ] Handles unreachable goals gracefully
- [ ] Performance acceptable (< 5ms for 100+ paths/frame)

**Effort**: 3-4 days

---

## Task 3: Unit Locomotor System (PLAN-042)

**Objective**: Implement smooth unit movement along paths

**Duration**: 2-3 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Object/Locomotor/Locomotor.cs

public abstract class Locomotor : DisposableBase
{
    protected Unit _unit;
    protected List<Coord3D> _path;
    protected int _pathIndex;
    protected float _moveProgress; // 0-1 along current segment
    
    public bool IsMoving => _path != null && _path.Count > 0;
    
    public Locomotor(Unit unit)
    {
        _unit = unit;
        _path = null;
        _pathIndex = 0;
        _moveProgress = 0;
    }
    
    /// <summary>
    /// Set new path for unit to follow
    /// </summary>
    public void SetPath(List<Coord3D> path)
    {
        _path = path;
        _pathIndex = 0;
        _moveProgress = 0;
    }
    
    /// <summary>
    /// Update unit position along path
    /// </summary>
    public virtual void UpdatePosition(in TimeInterval gameTime)
    {
        if (_path == null || _path.Count == 0)
            return;
        
        // Get current waypoint
        if (_pathIndex >= _path.Count)
        {
            // Path complete
            _path = null;
            OnPathComplete();
            return;
        }
        
        var targetWaypoint = _path[_pathIndex];
        var direction = (targetWaypoint - _unit.Position).Normalized();
        
        // Move toward waypoint
        float distance = Vector3.Distance(_unit.Position, targetWaypoint);
        float moveDistance = GetMovementSpeed() * (float)gameTime.DeltaTime.TotalSeconds;
        
        if (moveDistance >= distance)
        {
            // Reached waypoint
            _unit.Position = targetWaypoint;
            _pathIndex++;
        }
        else
        {
            // Move along direction
            _unit.Position += direction * moveDistance;
            
            // Rotate to face movement
            float targetAngle = MathF.Atan2(direction.Y, direction.X);
            _unit.Angle = LerpAngle(_unit.Angle, targetAngle, 0.1f);
        }
    }
    
    /// <summary>
    /// Get movement speed for this unit type
    /// </summary>
    protected virtual float GetMovementSpeed()
    {
        return _unit.Speed;
    }
    
    /// <summary>
    /// Called when path is complete
    /// </summary>
    protected virtual void OnPathComplete()
    {
        // Can be overridden in derived classes
    }
    
    private float LerpAngle(float from, float to, float t)
    {
        // Interpolate angle smoothly
        float diff = to - from;
        
        // Normalize difference to [-π, π]
        while (diff > MathF.PI) diff -= 2 * MathF.PI;
        while (diff < -MathF.PI) diff += 2 * MathF.PI;
        
        return from + diff * t;
    }
}

// File: src/OpenSage.Game/Logic/Object/Locomotor/WheelsLocomotor.cs

public sealed class WheelsLocomotor : Locomotor
{
    public WheelsLocomotor(Unit unit) : base(unit) { }
    
    protected override float GetMovementSpeed()
    {
        // Wheels are fast on flat terrain
        return _unit.Speed * 1.2f;
    }
    
    public override void UpdatePosition(in TimeInterval gameTime)
    {
        // TODO: Add speed bonus/penalty based on terrain type
        base.UpdatePosition(gameTime);
    }
}

// File: src/OpenSage.Game/Logic/Object/Locomotor/LegsLocomotor.cs

public sealed class LegsLocomotor : Locomotor
{
    public LegsLocomotor(Unit unit) : base(unit) { }
    
    protected override float GetMovementSpeed()
    {
        // Legs are slower but more versatile
        return _unit.Speed * 0.8f;
    }
}

// Modify: src/OpenSage.Game/Logic/Object/Unit/Unit.cs

public sealed class Unit : GameObject
{
    private Locomotor _locomotor;
    
    public Unit(UnitTemplate template, Player owner, Coord3D position)
        : base(template, owner)
    {
        // Select locomotor based on unit type
        _locomotor = template.HasWheels
            ? new WheelsLocomotor(this) as Locomotor
            : new LegsLocomotor(this);
    }
    
    public override void Update(in TimeInterval gameTime)
    {
        if (IsDestroyed)
            return;
        
        // Update locomotion
        _locomotor?.UpdatePosition(gameTime);
        
        base.Update(gameTime);
    }
    
    public void MoveTo(Coord3D destination, Pathfinder pathfinder)
    {
        if (pathfinder != null)
        {
            var path = pathfinder.FindPath(Position, destination);
            _locomotor.SetPath(path);
        }
        else
        {
            // Direct move if no pathfinder
            _locomotor.SetPath(new List<Coord3D> { destination });
        }
    }
}
```

**Acceptance Criteria**:
- [ ] Units move smoothly along paths
- [ ] Rotation toward movement direction works
- [ ] Path completion detected correctly
- [ ] Multiple units move independently

**Effort**: 2-3 days

---

## Task 4: Collision Avoidance (PLAN-043)

**Objective**: Prevent unit stacking and improve natural movement

**Duration**: 2-3 days

**Implementation**:

```csharp
// File: src/OpenSage.Game/Logic/Pathfinding/CollisionAvoidanceSystem.cs

public sealed class CollisionAvoidanceSystem : DisposableBase
{
    private const float SEPARATION_RADIUS = 5.0f;
    private const float AVOIDANCE_STRENGTH = 0.2f;
    
    /// <summary>
    /// Apply collision avoidance to all moving units
    /// </summary>
    public void Update(List<Unit> units)
    {
        foreach (var unit in units)
        {
            if (!unit.Locomotor.IsMoving)
                continue;
            
            // Find nearby units
            var nearby = GetNearbyUnits(unit, units);
            
            if (nearby.Count == 0)
                continue;
            
            // Calculate separation vector
            var separation = Vector3.Zero;
            
            foreach (var other in nearby)
            {
                var diff = (unit.Position - other.Position);
                float distance = diff.Length();
                
                if (distance < 0.01f)
                    distance = 0.01f; // Avoid division by zero
                
                // Separation force decreases with distance
                var force = (diff / distance) * (1.0f - distance / SEPARATION_RADIUS);
                separation += force;
            }
            
            if (separation.LengthSquared() > 0.01f)
            {
                // Apply avoidance: reduce speed to allow separation
                unit.Speed *= (1.0f - AVOIDANCE_STRENGTH);
            }
        }
    }
    
    private List<Unit> GetNearbyUnits(Unit unit, List<Unit> allUnits)
    {
        return allUnits
            .Where(u =>
                u != unit &&
                !u.IsDestroyed &&
                Vector3.Distance(u.Position, unit.Position) < SEPARATION_RADIUS
            )
            .ToList();
    }
}
```

**Acceptance Criteria**:
- [ ] Units don't stack on top of each other
- [ ] Separation feels natural (not too aggressive)
- [ ] Performance acceptable with 100+ units

**Effort**: 2-3 days

---

## Integration Checklist

- [ ] NavigationMesh builds from terrain
- [ ] Pathfinder A* algorithm works
- [ ] Units follow paths smoothly
- [ ] Collision avoidance applied
- [ ] Multiple units move simultaneously
- [ ] 100+ units at 60 FPS

---

## Testing

```csharp
[TestFixture]
public class PathfindingTests
{
    private NavigationMesh _navMesh;
    private Pathfinder _pathfinder;
    
    [SetUp]
    public void Setup()
    {
        var terrain = new MockTerrain(1024, 1024);
        _navMesh = NavigationMesh.BuildFromMap(terrain, new MockObjectList(), 4.0f);
        _pathfinder = new Pathfinder(_navMesh);
    }
    
    [Test]
    public void PathfinderFindsStraightPath()
    {
        var start = new Coord3D(10, 10, 0);
        var goal = new Coord3D(100, 100, 0);
        
        var path = _pathfinder.FindPath(start, goal);
        
        Assert.Greater(path.Count, 0);
        Assert.AreEqual(goal, path[path.Count - 1]);
    }
    
    [Test]
    public void PathfinderAvoidObstacles()
    {
        // Create NavMesh with obstacle in the way
        var terrain = new MockTerrain(1024, 1024);
        var obstacles = new MockObjectList();
        obstacles.Add(new MockGameObject { Position = new Coord3D(50, 50, 0), Bounds = new Bounds { Radius = 10 } });
        
        _navMesh = NavigationMesh.BuildFromMap(terrain, obstacles, 4.0f);
        _pathfinder = new Pathfinder(_navMesh);
        
        var start = new Coord3D(10, 50, 0);
        var goal = new Coord3D(100, 50, 0);
        
        var path = _pathfinder.FindPath(start, goal);
        
        // Path should be longer than direct line due to obstacle avoidance
        float directDistance = Vector3.Distance(start, goal);
        float pathDistance = CalculatePathDistance(path);
        
        Assert.Greater(pathDistance, directDistance * 1.1f); // At least 10% longer
    }
}
```

---

## Success Metrics

After Phase 07A:

✅ Units pathfind to clicked locations  
✅ 100+ units moving simultaneously  
✅ Smooth, natural movement  
✅ No unit stacking  
✅ 60 FPS performance maintained  
✅ Ready for Phase 07B (Combat)

---

## Next Phase Dependencies

**Phase 07B (Combat)** requires:
- Pathfinding working ✅
- Units positioned correctly ✅
- Collision avoidance active ✅

All prerequisites met!
