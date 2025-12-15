# Phase Planning: Particle Systems

**Phase Identifier**: PHASE02_PARTICLE_SYSTEMS  
**Status**: Planning  
**Priority**: High  
**Estimated Duration**: 2-3 weeks

---

## Overview

Complete the particle system with advanced types, emission modes, and performance optimization.

**Current Status**: 75% complete  
**Target Status**: 100% complete

---

## Detailed Tasks

### Task 1: Complete Emission Volumes (PLAN-001)
**Phase**: Phase 1 (Week 1)  
**Complexity**: Low  
**Effort**: 1 day  
**Dependencies**: None  
**Note**: This task spans both particles and maps

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

### Task 2: Implement Streak Particles (PLAN-004)
**Phase**: Phase 2 (Weeks 2-3)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: PLAN-001  

**Description**:
Implement trail/streak particles that leave visual trails as they move.

**Current State**:
- Particle type enum exists but STREAK not implemented
- Reference: `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs`

**Implementation**:

**Add streak particle storage**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/Particle.cs (enhancement)

public sealed class Particle : IPersistableObject
{
    // ... existing fields ...
    
    // Streak-specific fields
    public Vector3[] StreakVertices { get; set; }  // Trail vertices
    public int StreakVertexCount { get; private set; }
    private const int MaxStreakVertices = 50;
    
    public void RecordStreakVertex()
    {
        if (StreakVertices == null)
            StreakVertices = new Vector3[MaxStreakVertices];
        
        // Shift vertices back
        for (int i = MaxStreakVertices - 1; i > 0; i--)
            StreakVertices[i] = StreakVertices[i - 1];
        
        StreakVertices[0] = Position;
        StreakVertexCount = Math.Min(StreakVertexCount + 1, MaxStreakVertices);
    }
}
```

**Add streak rendering**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs (enhancement)

private void RenderStreakParticles(CommandList commandList)
{
    if (Template.ParticleType != FXParticleType.STREAK)
        return;
    
    var streakVertices = new List<ParticleShaderResources.ParticleVertex>();
    var streakIndices = new List<ushort>();
    
    ushort vertexIndex = 0;
    
    foreach (var particle in _particles)
    {
        if (particle.Dead || particle.StreakVertexCount < 2)
            continue;
        
        // Create ribbon from streak vertices
        for (int i = 0; i < particle.StreakVertexCount; i++)
        {
            var position = particle.StreakVertices[i];
            var alphaGradient = 1.0f - ((float)i / particle.StreakVertexCount);
            
            streakVertices.Add(new ParticleShaderResources.ParticleVertex
            {
                Position = position,
                Size = particle.Size * alphaGradient,
                Color = particle.Color,
                Alpha = particle.Alpha * alphaGradient,
                AngleZ = particle.AngleZ
            });
        }
        
        // Create ribbon indices
        for (int i = 0; i < particle.StreakVertexCount - 1; i++)
        {
            var base0 = vertexIndex + i;
            var base1 = vertexIndex + i + 1;
            
            streakIndices.AddRange(new ushort[]
            {
                base0, (ushort)(base0 + 1), base1,
                (ushort)(base0 + 1), (ushort)(base1 + 1), base1
            });
        }
        
        vertexIndex += (ushort)particle.StreakVertexCount;
    }
    
    if (streakVertices.Count == 0)
        return;
    
    // Update buffers and render
    commandList.UpdateBuffer(_vertexBuffer, 0, streakVertices.ToArray());
    commandList.SetVertexBuffer(0, _vertexBuffer);
    commandList.SetIndexBuffer(_indexBuffer, IndexFormat.UInt16);
    commandList.DrawIndexed((uint)streakIndices.Count, 1, 0, 0, 0);
}

public override void Render(CommandList commandList)
{
    if (Template.ParticleType == FXParticleType.STREAK)
    {
        RenderStreakParticles(commandList);
    }
    else
    {
        RenderNormalParticles(commandList);
    }
}
```

**Acceptance Criteria**:
- [ ] Streak particles render with proper trails
- [ ] Trail fades with distance
- [ ] Multiple streaks don't interfere
- [ ] Performance maintained with 100+ streaks
- [ ] Streak width scales with particle size

**Testing**:
```csharp
[Test]
public void TestStreakParticleTrail()
{
    var particle = new Particle(this);
    
    for (int i = 0; i < 30; i++)
    {
        particle.Position = new Vector3(i * 1f, 0, 0);
        particle.RecordStreakVertex();
    }
    
    Assert.AreEqual(30, particle.StreakVertexCount);
    Assert.AreEqual(particle.Position, particle.StreakVertices[0]);
}
```

---

### Task 3: Implement Drawable Particles (PLAN-005)
**Phase**: Phase 2 (Weeks 2-3)  
**Complexity**: High  
**Effort**: 2-3 days  
**Dependencies**: PLAN-001  

**Description**:
Attach sprites or drawable objects to particles for complex effects.

**Current State**:
- Particle type enum exists but DRAWABLE not implemented

**Implementation**:

**Add drawable support**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/Particle.cs (enhancement)

public sealed class Particle : IPersistableObject
{
    // ... existing fields ...
    
    // Drawable-specific fields
    public Drawable AttachedDrawable { get; set; }
    public string DrawableName { get; set; }
    
    public void AttachDrawable(Drawable drawable)
    {
        AttachedDrawable = drawable;
    }
}
```

**Add drawable rendering**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs (enhancement)

private void RenderDrawableParticles(CommandList commandList, RenderContext context)
{
    if (Template.ParticleType != FXParticleType.DRAWABLE)
        return;
    
    foreach (var particle in _particles)
    {
        if (particle.Dead || particle.AttachedDrawable == null)
            continue;
        
        // Update drawable transform
        var transform = Matrix4x4.CreateScale(particle.Size)
            * Matrix4x4.CreateRotationZ(particle.AngleZ)
            * Matrix4x4.CreateTranslation(particle.Position);
        
        particle.AttachedDrawable.SetWorldMatrix(transform);
        
        // Render drawable
        particle.AttachedDrawable.Render(context);
    }
}
```

**Acceptance Criteria**:
- [ ] Drawable objects attach to particles
- [ ] Transform follows particle position/rotation/scale
- [ ] Multiple drawable types supported
- [ ] Performance with 50+ drawable particles
- [ ] Drawables persist with particle lifetime

---

### Task 4: Implement MULTIPLY Shader Blending (PLAN-008)
**Phase**: Phase 3 (Weeks 4-5)  
**Complexity**: Medium  
**Effort**: 1-2 days  
**Dependencies**: PLAN-004, PLAN-005  

**Description**:
Add MULTIPLY blend mode for shadow-like particle effects.

**Current State**:
- Only ADDITIVE and ALPHA blending implemented
- Reference: `src/OpenSage.Game/Graphics/ParticleSystems/ParticleMaterial.cs`

**Implementation**:

```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/ParticleMaterial.cs

public class ParticleMaterialEnhanced
{
    public BlendStateDescription GetBlendState(FXShaderType shaderType)
    {
        return shaderType switch
        {
            FXShaderType.ADDITIVE => new BlendStateDescription
            {
                AttachmentStates = new[]
                {
                    new BlendAttachmentDescription
                    {
                        BlendEnabled = true,
                        SourceColorFactor = BlendFactor.SourceAlpha,
                        DestinationColorFactor = BlendFactor.One,
                        ColorFunction = BlendFunction.Add,
                        SourceAlphaFactor = BlendFactor.SourceAlpha,
                        DestinationAlphaFactor = BlendFactor.One,
                        AlphaFunction = BlendFunction.Add
                    }
                }
            },
            
            FXShaderType.ALPHA => new BlendStateDescription
            {
                AttachmentStates = new[]
                {
                    new BlendAttachmentDescription
                    {
                        BlendEnabled = true,
                        SourceColorFactor = BlendFactor.SourceAlpha,
                        DestinationColorFactor = BlendFactor.InverseSourceAlpha,
                        ColorFunction = BlendFunction.Add,
                        SourceAlphaFactor = BlendFactor.SourceAlpha,
                        DestinationAlphaFactor = BlendFactor.InverseSourceAlpha,
                        AlphaFunction = BlendFunction.Add
                    }
                }
            },
            
            FXShaderType.MULTIPLY => new BlendStateDescription
            {
                AttachmentStates = new[]
                {
                    new BlendAttachmentDescription
                    {
                        BlendEnabled = true,
                        SourceColorFactor = BlendFactor.DestinationColor,
                        DestinationColorFactor = BlendFactor.Zero,
                        ColorFunction = BlendFunction.Add,
                        SourceAlphaFactor = BlendFactor.SourceAlpha,
                        DestinationAlphaFactor = BlendFactor.One,
                        AlphaFunction = BlendFunction.Add
                    }
                }
            },
            
            _ => new BlendStateDescription()
        };
    }
}
```

**Acceptance Criteria**:
- [ ] MULTIPLY blending renders correctly
- [ ] Shadow particles appear darker than background
- [ ] Multiple MULTIPLY particles blend correctly
- [ ] Performance not impacted

---

### Task 5: Implement Particle Count Limiting (PLAN-010)
**Phase**: Phase 3 (Weeks 4-5)  
**Complexity**: Medium  
**Effort**: 1-2 days  
**Dependencies**: PLAN-004, PLAN-005  

**Description**:
Implement priority-based particle culling when max particle count exceeded.

**Current State**:
- MaxParticleCount limit exists but culling commented out
- Reference: `src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs`

**Implementation**:

```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystemManager.cs

public sealed class ParticleSystemManager : DisposableBase, IPersistableObject
{
    private readonly int _maxParticleCount;
    private int _currentParticleCount;
    
    public void Update(in TimeInterval gameTime)
    {
        var totalParticles = 0;
        var particleSystems = new List<(ParticleSystem, int)>();
        
        // Count particles in each system
        for (var i = 0; i < _particleSystems.Count; i++)
        {
            var particleSystem = _particleSystems[i];
            
            if (particleSystem.State == ParticleSystemState.Inactive)
                continue;
            
            particleSystem.Update(gameTime);
            
            if (particleSystem.State == ParticleSystemState.Dead)
            {
                _renderBucket.RemoveObject(particleSystem);
                particleSystem.Dispose();
                RemoveToDispose(particleSystem);
                _particleSystems.RemoveAt(i);
                i--;
                continue;
            }
            
            totalParticles += particleSystem.CurrentParticleCount;
            particleSystems.Add((particleSystem, particleSystem.Template.Priority));
        }
        
        _currentParticleCount = totalParticles;
        
        // Apply culling if exceeded
        if (totalParticles > _maxParticleCount)
        {
            ApplyParticleCulling(particleSystems, _maxParticleCount);
        }
    }
    
    private void ApplyParticleCulling(List<(ParticleSystem, int)> systems, int targetCount)
    {
        // Sort by priority (lower priority culled first)
        systems.Sort((a, b) => b.Item2.CompareTo(a.Item2));
        
        int particleCount = 0;
        foreach (var (system, priority) in systems)
        {
            var particlesToKeep = Math.Max(0, targetCount - particleCount);
            var particlesToRemove = Math.Max(0, system.CurrentParticleCount - particlesToKeep);
            
            system.CullParticles(particlesToRemove);
            particleCount += system.CurrentParticleCount;
        }
    }
}
```

**Add culling support to ParticleSystem**:
```csharp
// File: src/OpenSage.Game/Graphics/ParticleSystems/ParticleSystem.cs

public void CullParticles(int count)
{
    int culled = 0;
    
    for (int i = 0; i < _particles.Length && culled < count; i++)
    {
        if (!_particles[i].Dead)
        {
            _particles[i].Dead = true;
            _deadList.Add(i);
            culled++;
        }
    }
    
    CurrentParticleCount -= culled;
}
```

**Acceptance Criteria**:
- [ ] Particle count never exceeds max
- [ ] Low-priority systems culled first
- [ ] High-priority particles preserved
- [ ] Culling smooth and imperceptible to player
- [ ] Performance stable at max particles

---

## Integration Points

### With ParticleSystemManager
```csharp
// In ParticleSystemManager.cs constructor
public ParticleSystemManager(IScene3D scene, AssetLoadContext assetLoadContext)
{
    _scene = scene;
    _loadContext = assetLoadContext;
    _maxParticleCount = assetLoadContext.AssetStore.GameData.Current.MaxParticleCount;
    
    _particleSystems = new List<ParticleSystem>();
    _renderBucket = scene.RenderScene.CreateRenderBucket("Particles", 15);
}
```

### With Template System
```csharp
// Ensure templates support all new particle types
public enum FXParticleType
{
    PARTICLE,        // ✅ Working
    DRAWABLE,        // ⚠️ PLAN-005
    STREAK,          // ⚠️ PLAN-004
    VOLUME_PARTICLE  // Future
}
```

---

## Testing Strategy

### Unit Tests
- All 5 emission volume types
- Streak trail generation
- Drawable attachment
- Blend mode calculations
- Particle culling logic

### Integration Tests
- Particle systems with all types active
- Emission volume + velocity combinations
- Drawable particle rendering
- Culling behavior under load

### Performance Tests
- 1000+ streak particles at 60 FPS
- 500+ drawable particles at 60 FPS
- Culling doesn't cause frame hitches

---

## Success Metrics

- [ ] All particle types working correctly
- [ ] Performance: 1000+ particles at 60 FPS
- [ ] No memory leaks with rapid creation/destruction
- [ ] All shader blending modes working
- [ ] Code follows OpenSAGE standards
- [ ] Unit test coverage > 80%
- [ ] Documentation updated
