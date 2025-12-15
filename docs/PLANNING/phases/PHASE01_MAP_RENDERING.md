# Phase Planning: Map Rendering

**Phase Identifier**: PHASE01_MAP_RENDERING  
**Status**: Planning  
**Priority**: High  
**Estimated Duration**: 2-3 weeks

---

## Overview

Complete the map rendering system with water, roads, objects, and emissions.

**Current Status**: 75% complete  
**Target Status**: 100% complete

---

## Detailed Tasks

### Task 1: Complete Emission Volumes (PLAN-001)
**Phase**: Phase 1 (Week 1)  
**Complexity**: Low  
**Effort**: 1 day  
**Dependencies**: None  
**Note**: This task spans both maps and particles

**Description**:
Implement all particle emission volume types that are currently incomplete.

**Current State**:
- Only basic volumes implemented
- Reference: `src/OpenSage.Game/Graphics/ParticleSystems/`

**Implementation**:

**Create base emission volume interface**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/FXParticleEmissionVolume.cs
// (add to existing file)

public abstract class FXParticleEmissionVolumeBase
{
    public abstract Ray GetRay();
}

public class FXParticleEmissionVolumeSphere : FXParticleEmissionVolumeBase
{
    public float Radius { get; set; }
    
    public override Ray GetRay()
    {
        var theta = Random.Shared.NextSingle() * 2 * MathF.PI;
        var phi = Random.Shared.NextSingle() * MathF.PI;
        var r = Radius * MathF.Cbrt(Random.Shared.NextSingle());
        
        var x = r * MathF.Sin(phi) * MathF.Cos(theta);
        var y = r * MathF.Sin(phi) * MathF.Sin(theta);
        var z = r * MathF.Cos(phi);
        
        var point = new Vector3(x, y, z);
        return new Ray(point, Vector3.Normalize(point));
    }
}

public class FXParticleEmissionVolumeBox : FXParticleEmissionVolumeBase
{
    public Vector3 Extents { get; set; }
    
    public override Ray GetRay()
    {
        var x = (Random.Shared.NextSingle() - 0.5f) * 2 * Extents.X;
        var y = (Random.Shared.NextSingle() - 0.5f) * 2 * Extents.Y;
        var z = (Random.Shared.NextSingle() - 0.5f) * 2 * Extents.Z;
        
        var point = new Vector3(x, y, z);
        var direction = Vector3.Normalize(point + Vector3.One * 0.1f);
        
        return new Ray(point, direction);
    }
}

public class FXParticleEmissionVolumeCylinder : FXParticleEmissionVolumeBase
{
    public float Radius { get; set; }
    public float Height { get; set; }
    
    public override Ray GetRay()
    {
        var angle = Random.Shared.NextSingle() * 2 * MathF.PI;
        var r = Radius * MathF.Sqrt(Random.Shared.NextSingle());
        var h = (Random.Shared.NextSingle() - 0.5f) * Height;
        
        var x = r * MathF.Cos(angle);
        var y = h;
        var z = r * MathF.Sin(angle);
        
        var point = new Vector3(x, y, z);
        return new Ray(point, Vector3.Normalize(point));
    }
}

public class FXParticleEmissionVolumeLine : FXParticleEmissionVolumeBase
{
    public Vector3 Start { get; set; }
    public Vector3 End { get; set; }
    
    public override Ray GetRay()
    {
        var t = Random.Shared.NextSingle();
        var point = Vector3.Lerp(Start, End, t);
        var direction = Vector3.Normalize(End - Start);
        
        return new Ray(point, direction);
    }
}

public class FXParticleEmissionVolumePoint : FXParticleEmissionVolumeBase
{
    public Vector3 Position { get; set; }
    
    public override Ray GetRay()
    {
        var theta = Random.Shared.NextSingle() * 2 * MathF.PI;
        var phi = Random.Shared.NextSingle() * MathF.PI;
        
        var x = MathF.Sin(phi) * MathF.Cos(theta);
        var y = MathF.Sin(phi) * MathF.Sin(theta);
        var z = MathF.Cos(phi);
        
        var direction = new Vector3(x, y, z);
        return new Ray(Position, direction);
    }
}
```

**Acceptance Criteria**:
- [ ] All 5 volume types implemented and tested
- [ ] Particles emit in correct spatial distribution
- [ ] Random velocity generation working
- [ ] All existing particle templates still working

**Testing**:
```csharp
[Test]
public void TestAllEmissionVolumes()
{
    var volumes = new FXParticleEmissionVolumeBase[]
    {
        new FXParticleEmissionVolumeSphere { Radius = 10f },
        new FXParticleEmissionVolumeBox { Extents = new Vector3(5, 5, 5) },
        new FXParticleEmissionVolumeCylinder { Radius = 5f, Height = 10f },
        new FXParticleEmissionVolumeLine { Start = Vector3.Zero, End = new Vector3(10, 0, 0) },
        new FXParticleEmissionVolumePoint { Position = Vector3.Zero }
    };
    
    foreach (var volume in volumes)
    {
        for (int i = 0; i < 100; i++)
        {
            var ray = volume.GetRay();
            Assert.IsNotNull(ray);
        }
    }
}
```

---

### Task 2: Fix Road Rendering Visibility (PLAN-002)
**Phase**: Phase 1 (Week 1)  
**Complexity**: Low  
**Effort**: 1 day  
**Dependencies**: None  

**Description**:
Fix road mesh rendering and visibility culling issues.

**Current State**:
- Road meshes created but not properly rendered
- Reference: `src/OpenSage.Game/Terrain/Roads/RoadCollection.cs`

**Implementation**:

**Add road rendering to terrain visual**:
```csharp
// File: src/OpenSage.Game/Terrain/TerrainVisual.cs (enhancement)

public sealed class TerrainVisual : DisposableBase
{
    private RoadRenderingSystem _roadSystem;
    
    public TerrainVisual(Terrain terrain, ContentManager contentManager)
    {
        // ... existing initialization ...
        _roadSystem = new RoadRenderingSystem(terrain, contentManager);
    }
    
    public void Render(RenderContext context)
    {
        // Render terrain patches
        foreach (var patch in _terrain.Patches)
        {
            patch.Render(context);
        }
        
        // Render roads
        _roadSystem.Render(context);
    }
    
    public override void Dispose()
    {
        _roadSystem?.Dispose();
        base.Dispose();
    }
}
```

**Create road rendering system**:
```csharp
// File: src/OpenSage.Game/Terrain/Roads/RoadRenderingSystem.cs

public sealed class RoadRenderingSystem : DisposableBase
{
    private readonly Terrain _terrain;
    private readonly List<RoadSegmentRenderData> _renderData = new();
    private Material _roadMaterial;
    
    public RoadRenderingSystem(Terrain terrain, ContentManager contentManager)
    {
        _terrain = terrain;
        _roadMaterial = contentManager.Load<Material>("Shaders/Road.hlsl");
        
        BuildRenderData();
    }
    
    private void BuildRenderData()
    {
        foreach (var road in _terrain.Roads)
        {
            var segments = RoadMeshGenerator.GenerateSegments(road, _terrain);
            
            foreach (var segment in segments)
            {
                _renderData.Add(new RoadSegmentRenderData
                {
                    VertexBuffer = segment.VertexBuffer,
                    IndexBuffer = segment.IndexBuffer,
                    IndexCount = segment.IndexCount,
                    BoundingBox = segment.BoundingBox
                });
            }
        }
    }
    
    public void Render(RenderContext context)
    {
        var frustum = context.Camera.BoundingFrustum;
        
        foreach (var renderData in _renderData)
        {
            // Frustum culling
            if (frustum.Intersects(renderData.BoundingBox) == ContainmentType.Disjoint)
                continue;
            
            context.CommandList.SetVertexBuffer(0, renderData.VertexBuffer);
            context.CommandList.SetIndexBuffer(renderData.IndexBuffer, IndexFormat.UInt16);
            context.CommandList.DrawIndexed(renderData.IndexCount, 1, 0, 0, 0);
        }
    }
    
    public override void Dispose()
    {
        foreach (var renderData in _renderData)
        {
            renderData.VertexBuffer?.Dispose();
            renderData.IndexBuffer?.Dispose();
        }
        _renderData.Clear();
        _roadMaterial?.Dispose();
        base.Dispose();
    }
}

public class RoadSegmentRenderData
{
    public DeviceBuffer VertexBuffer { get; set; }
    public DeviceBuffer IndexBuffer { get; set; }
    public uint IndexCount { get; set; }
    public BoundingBox BoundingBox { get; set; }
}
```

**Acceptance Criteria**:
- [ ] Roads render on terrain
- [ ] Road visibility culling working
- [ ] Road meshes mesh properly with terrain
- [ ] No Z-fighting with terrain
- [ ] Performance acceptable with many roads

**Testing**:
```csharp
[Test]
public void TestRoadRendering()
{
    var terrain = LoadTestTerrain();
    var roadSystem = new RoadRenderingSystem(terrain, contentManager);
    
    Assert.Greater(roadSystem.RenderDataCount, 0);
    
    var context = CreateMockRenderContext();
    roadSystem.Render(context);
    
    Assert.Greater(context.DrawCalls, 0);
}
```

---

### Task 3: Complete Water Animation System (PLAN-006)
**Phase**: Phase 2 (Week 2)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: None  

**Description**:
Implement water rendering with wave animation, reflections, and caustics.

**Current State**:
- Water mesh exists but animations incomplete
- Reference: `src/OpenSage.Game/Terrain/Water/WaterSystem.cs`

**Implementation**:

**Create water simulation**:
```csharp
// File: src/OpenSage.Game/Terrain/Water/WaterSimulation.cs

public sealed class WaterSimulation
{
    public const float GerstnerWaveCount = 4;
    
    public struct GerstnerWave
    {
        public Vector2 Direction;
        public float Wavelength;
        public float Amplitude;
        public float Speed;
        public float Frequency => 2 * MathF.PI / Wavelength;
        public float PhaseVelocity => Speed * Frequency;
    }
    
    private GerstnerWave[] _waves;
    
    public WaterSimulation()
    {
        _waves = new GerstnerWave[GerstnerWaveCount]
        {
            new()
            {
                Direction = Vector2.Normalize(new Vector2(1, 0.3f)),
                Wavelength = 60f,
                Amplitude = 0.5f,
                Speed = 6f
            },
            new()
            {
                Direction = Vector2.Normalize(new Vector2(0.7f, 1)),
                Wavelength = 31f,
                Amplitude = 0.4f,
                Speed = 5f
            },
            new()
            {
                Direction = Vector2.Normalize(new Vector2(1, 1)),
                Wavelength = 20f,
                Amplitude = 0.25f,
                Speed = 4f
            },
            new()
            {
                Direction = Vector2.Normalize(new Vector2(0.5f, 1)),
                Wavelength = 15f,
                Amplitude = 0.15f,
                Speed = 3f
            }
        };
    }
    
    public Vector3 GetWaveHeight(Vector2 position, float time)
    {
        var height = Vector3.Zero;
        var normal = Vector3.Zero;
        var tangent = Vector3.Zero;
        
        foreach (var wave in _waves)
        {
            var phase = Vector2.Dot(position, wave.Direction) * wave.Frequency + time * wave.PhaseVelocity;
            var sinPhase = MathF.Sin(phase);
            var cosPhase = MathF.Cos(phase);
            
            height.Y += wave.Amplitude * sinPhase;
            
            var derivativeX = wave.Frequency * wave.Amplitude * cosPhase * wave.Direction.X;
            var derivativeY = wave.Frequency * wave.Amplitude * cosPhase * wave.Direction.Y;
            
            normal.X -= derivativeX;
            normal.Z -= derivativeY;
        }
        
        normal.Y = 1.0f;
        normal = Vector3.Normalize(normal);
        
        return height;
    }
}
```

**Create water rendering**:
```csharp
// File: src/OpenSage.Game/Terrain/Water/WaterRenderer.cs

public sealed class WaterRenderer : DisposableBase
{
    private WaterSimulation _simulation;
    private Material _waterMaterial;
    private DeviceBuffer _vertexBuffer;
    private DeviceBuffer _indexBuffer;
    private uint _indexCount;
    
    private Texture _reflectionTexture;
    private Framebuffer _reflectionFramebuffer;
    
    public WaterRenderer(int waterMeshWidth, ContentManager contentManager)
    {
        _simulation = new WaterSimulation();
        _waterMaterial = contentManager.Load<Material>("Shaders/Water.hlsl");
        
        CreateWaterMesh(waterMeshWidth);
        CreateReflectionResources(1024, 1024);
    }
    
    private void CreateWaterMesh(int gridSize)
    {
        var vertices = new List<WaterVertex>();
        var indices = new List<ushort>();
        
        for (int z = 0; z < gridSize; z++)
        {
            for (int x = 0; x < gridSize; x++)
            {
                vertices.Add(new WaterVertex
                {
                    Position = new Vector3(x, 0, z),
                    TexCoord = new Vector2(x / (float)gridSize, z / (float)gridSize)
                });
            }
        }
        
        for (int z = 0; z < gridSize - 1; z++)
        {
            for (int x = 0; x < gridSize - 1; x++)
            {
                var i00 = (ushort)(z * gridSize + x);
                var i10 = (ushort)(z * gridSize + x + 1);
                var i01 = (ushort)((z + 1) * gridSize + x);
                var i11 = (ushort)((z + 1) * gridSize + x + 1);
                
                indices.AddRange(new[] { i00, i10, i01, i10, i11, i01 });
            }
        }
        
        _indexCount = (uint)indices.Count;
        _vertexBuffer = CreateBuffer(BufferUsage.VertexBuffer, vertices.ToArray());
        _indexBuffer = CreateBuffer(BufferUsage.IndexBuffer, indices.ToArray());
    }
    
    private void CreateReflectionResources(int width, int height)
    {
        _reflectionTexture = GraphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R32_G32_B32_A32_Float,
                TextureUsage.RenderTarget | TextureUsage.Sampled));
        
        var depthTexture = GraphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(width, height, 1, 1, PixelFormat.R32_Float,
                TextureUsage.DepthStencil));
        
        _reflectionFramebuffer = GraphicsDevice.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(depthTexture, _reflectionTexture));
    }
    
    public void Render(RenderContext context, float elapsedTime, Action renderSceneCallback)
    {
        // First pass: Render reflection to texture
        context.CommandList.SetFramebuffer(_reflectionFramebuffer);
        context.CommandList.ClearColorTarget(0, RgbaFloat.CornflowerBlue);
        context.CommandList.ClearDepthStencil(1);
        
        renderSceneCallback();
        
        // Second pass: Render water with reflection
        context.CommandList.SetFramebuffer(context.RenderTarget);
        context.CommandList.SetVertexBuffer(0, _vertexBuffer);
        context.CommandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
        
        context.CommandList.DrawIndexed(_indexCount, 1, 0, 0, 0);
    }
    
    public override void Dispose()
    {
        _reflectionTexture?.Dispose();
        _reflectionFramebuffer?.Dispose();
        _vertexBuffer?.Dispose();
        _indexBuffer?.Dispose();
        _waterMaterial?.Dispose();
        base.Dispose();
    }
}

public struct WaterVertex
{
    public Vector3 Position;
    public Vector2 TexCoord;
}
```

**Acceptance Criteria**:
- [ ] Water mesh animates with Gerstner waves
- [ ] Wave parameters produce realistic motion
- [ ] Reflection rendering working
- [ ] Normal maps applied correctly
- [ ] Caustic animations optional but supported
- [ ] Performance: 60 FPS with water active

**Testing**:
```csharp
[Test]
public void TestWaterSimulation()
{
    var simulation = new WaterSimulation();
    
    var pos = new Vector2(10, 10);
    var height0 = simulation.GetWaveHeight(pos, 0);
    var height1 = simulation.GetWaveHeight(pos, 0.1f);
    
    Assert.AreNotEqual(height0.Y, height1.Y);
}
```

---

### Task 4: Object Placement & Waypoints (PLAN-004)
**Phase**: Phase 2 (Week 2)  
**Complexity**: Medium  
**Effort**: 1-2 days  
**Dependencies**: None  

**Description**:
Render map objects and debug visualization for waypoints.

**Current State**:
- Objects loaded but not rendered
- Reference: `src/OpenSage.Game/Terrain/Objects/MapObject.cs`

**Implementation**:

```csharp
// File: src/OpenSage.Game/Terrain/Objects/MapObjectRenderer.cs

public sealed class MapObjectRenderer : DisposableBase
{
    private readonly Dictionary<MapObject, ObjectRenderData> _renderData = new();
    private readonly Terrain _terrain;
    
    public void RegisterObject(MapObject mapObject)
    {
        if (_renderData.ContainsKey(mapObject))
            return;
        
        _renderData[mapObject] = new ObjectRenderData
        {
            Transform = Matrix4x4.CreateTranslation(mapObject.Position),
            BoundingBox = mapObject.GetBoundingBox()
        };
    }
    
    public void RenderObjects(RenderContext context)
    {
        var frustum = context.Camera.BoundingFrustum;
        
        foreach (var (mapObject, renderData) in _renderData)
        {
            if (frustum.Intersects(renderData.BoundingBox) == ContainmentType.Disjoint)
                continue;
            
            mapObject.Render(context);
        }
    }
    
    public void RenderWaypoints(RenderContext context)
    {
        foreach (var waypoint in _terrain.Waypoints)
        {
            DrawWaypointMarker(context, waypoint);
        }
    }
    
    private void DrawWaypointMarker(RenderContext context, Waypoint waypoint)
    {
        // Draw debug sphere at waypoint
        var transform = Matrix4x4.CreateTranslation(waypoint.Position);
        context.SetWorldMatrix(transform);
        
        DrawDebugSphere(context, 5.0f, Color.Yellow);
        DrawDebugLabel(context, waypoint.Name, waypoint.Position);
    }
}

public class ObjectRenderData
{
    public Matrix4x4 Transform { get; set; }
    public BoundingBox BoundingBox { get; set; }
}
```

**Acceptance Criteria**:
- [ ] Map objects render at correct positions
- [ ] Waypoints visible in debug mode
- [ ] Waypoint labels display correctly
- [ ] No Z-fighting with terrain
- [ ] Performance acceptable

---

## Integration Points

### With Terrain System
```csharp
// In Terrain.cs
public void Render(RenderContext context)
{
    Visual.Render(context);
    _roadSystem.Render(context);
    _waterRenderer.Render(context, gameTime.TotalGameTime.TotalSeconds, () => 
    {
        // Render scene for reflection
    });
    _objectRenderer.RenderObjects(context);
    _objectRenderer.RenderWaypoints(context);
}
```

### With Scene3D
```csharp
// In Scene3D.cs
public void Render(RenderContext context)
{
    terrain.Render(context);
    // ... render other objects ...
}
```

---

## Testing Strategy

### Unit Tests
- Emission volume generation
- Road mesh generation
- Water height calculation
- Object frustum culling

### Integration Tests
- Full terrain with roads and water
- Object placement and rendering
- Waypoint visualization

### Performance Tests
- Large terrains with many roads
- Complex water with reflections
- 1000+ map objects

---

## Success Metrics

- [ ] All map components rendering
- [ ] Performance: 60 FPS on target hardware
- [ ] No memory leaks
- [ ] Visual quality matches original
- [ ] Code follows OpenSAGE standards
- [ ] Unit test coverage > 80%
- [ ] Documentation updated
