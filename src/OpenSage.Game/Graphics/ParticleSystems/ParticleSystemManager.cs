#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenSage.Content.Loaders;
using OpenSage.Graphics.Shaders;
using OpenSage.Rendering;
//using OpenSage.Graphics.Rendering;

namespace OpenSage.Graphics.ParticleSystems;

/// <summary>
/// Uniquely identifies a particle material for batching purposes.
/// Materials with identical keys can be grouped together to reduce draw calls.
/// Reference: EA Generals dx8renderer.h line 78 - "render in 'TextureCategory' batches"
/// </summary>
internal readonly struct ParticleMaterialKey : IEquatable<ParticleMaterialKey>
{
    public readonly ParticleSystemShader ShaderType;
    public readonly bool IsGroundAligned;
    public readonly string TextureName;

    public ParticleMaterialKey(ParticleSystemShader shaderType, bool isGroundAligned, string textureName)
    {
        ShaderType = shaderType;
        IsGroundAligned = isGroundAligned;
        TextureName = textureName ?? string.Empty;
    }

    public override bool Equals(object? obj) => obj is ParticleMaterialKey key && Equals(key);

    public bool Equals(ParticleMaterialKey other) =>
        ShaderType == other.ShaderType &&
        IsGroundAligned == other.IsGroundAligned &&
        TextureName == other.TextureName;

    public override int GetHashCode() =>
        HashCode.Combine(ShaderType, IsGroundAligned, TextureName);

    public static bool operator ==(ParticleMaterialKey left, ParticleMaterialKey right) =>
        left.Equals(right);

    public static bool operator !=(ParticleMaterialKey left, ParticleMaterialKey right) =>
        !left.Equals(right);
}

/// <summary>
/// Groups particle systems that share the same material for batching.
/// Preserves priority order within each group for correct rendering.
/// </summary>
internal readonly struct ParticleMaterialGroup
{
    public readonly ParticleMaterialKey MaterialKey;
    public readonly List<ParticleSystem> Systems;

    public ParticleMaterialGroup(ParticleMaterialKey materialKey)
    {
        MaterialKey = materialKey;
        Systems = new List<ParticleSystem>();
    }

    public int SystemCount => Systems.Count;
}

/// <summary>
/// Manages all particle systems and enforces global particle count limits.
/// Implements EA Generals particle culling algorithm with priority-based FIFO removal.
/// Reference: EA Generals W3DParticleSys.cpp and ParticleSys.cpp line 1794-1824
/// </summary>
internal sealed class ParticleSystemManager : DisposableBase, IPersistableObject
{
    private readonly IScene3D _scene;
    private readonly AssetLoadContext _loadContext;
    private readonly int _maxParticleCount;

    private readonly RenderBucket _renderBucket;

    private readonly List<ParticleSystem> _particleSystems;

    /// <summary>
    /// Per-priority linked lists tracking active particles for FIFO culling.
    /// Each element is (ParticleSystem, particleIndex) to allow efficient removal.
    /// Index corresponds to ParticleSystemPriority enum value.
    /// </summary>
    private readonly LinkedList<(ParticleSystem system, int particleIndex)>[] _particlesByPriority;

    /// <summary>
    /// Total count of active particles across all systems.
    /// Used for quick checks against _maxParticleCount.
    /// </summary>
    private int _totalActiveParticles;

    private uint _previousParticleSystemId;

    public ParticleSystemManager(IScene3D scene, AssetLoadContext assetLoadContext)
    {
        _scene = scene;
        _loadContext = assetLoadContext;
        _maxParticleCount = assetLoadContext.AssetStore.GameData.Current.MaxParticleCount;

        _particleSystems = new List<ParticleSystem>();

        // Initialize priority-based particle tracking (one list per priority level)
        // Reference: EA Generals ParticleSys.cpp line 3108 - m_allParticlesHead/Tail arrays
        var priorityCount = (int)ParticleSystemPriority.VeryLowOrAbove + 1;
        _particlesByPriority = new LinkedList<(ParticleSystem, int)>[priorityCount];
        for (var i = 0; i < priorityCount; i++)
        {
            _particlesByPriority[i] = new LinkedList<(ParticleSystem, int)>();
        }

        _totalActiveParticles = 0;

        _renderBucket = scene.RenderScene.CreateRenderBucket("Particles", 15);
    }

    public ParticleSystem Create(
        FXParticleSystemTemplate template,
        ParticleSystem.GetMatrixReferenceDelegate getWorldMatrix)
    {
        ParticleSystem result;

        _particleSystems.Add(
            AddDisposable(
                result = new ParticleSystem(
                    template,
                    _loadContext,
                    getWorldMatrix)));

        _renderBucket.AddObject(result);

        return result;
    }

    public ParticleSystem Create(
        FXParticleSystemTemplate template,
        in Matrix4x4 worldMatrix)
    {
        ParticleSystem result;

        _particleSystems.Add(
            AddDisposable(
                result = new ParticleSystem(
                    template,
                    _loadContext,
                    worldMatrix)));

        _renderBucket.AddObject(result);

        return result;
    }

    public void Remove(ParticleSystem particleSystem)
    {
        if (_particleSystems.Remove(particleSystem))
        {
            _renderBucket.RemoveObject(particleSystem);
            RemoveAndDispose(ref particleSystem);
        }
    }

    /// <summary>
    /// Registers a newly created particle for global tracking and limit enforcement.
    /// Called by ParticleSystem when a particle is emitted.
    ///
    /// Implementation references EA Generals ParticleSys.cpp addParticle() line 3116:
    /// - Checks priority thresholds
    /// - Applies FIFO culling if particle limit exceeded
    /// - Maintains per-priority linked lists for efficient removal
    ///
    /// Returns true if particle was successfully added, false if culling denied creation.
    /// </summary>
    /// <param name="particleSystem">The system that owns this particle</param>
    /// <param name="particleIndex">Index of the particle in ParticleSystem._particles array</param>
    /// <param name="priority">The priority level of the particle</param>
    /// <returns>True if particle was added, false if creation was denied</returns>
    internal bool AddParticle(ParticleSystem particleSystem, int particleIndex, ParticleSystemPriority priority)
    {
        // ALWAYS_RENDER particles bypass all limits
        // Reference: EA Generals ParticleSys.cpp line 1812-1813
        if (priority == ParticleSystemPriority.AlwaysRender)
        {
            _particlesByPriority[(int)priority].AddLast((particleSystem, particleIndex));
            _totalActiveParticles++;
            return true;
        }

        // Check if we need to remove older particles to stay within limit
        if (_totalActiveParticles >= _maxParticleCount)
        {
            // Remove oldest particles from lower priorities until we have room
            // Reference: EA Generals ParticleSys.cpp removeOldestParticles() line 3201-3218
            int numToRemove = _totalActiveParticles - _maxParticleCount + 1;

            if (!RemoveOldestParticles(numToRemove, priority))
            {
                // Failed to remove enough particles - deny creation
                return false;
            }
        }

        // Add particle to this priority's list
        _particlesByPriority[(int)priority].AddLast((particleSystem, particleIndex));
        _totalActiveParticles++;

        return true;
    }

    /// <summary>
    /// Removes oldest particles from lower priorities to make room for a new particle.
    ///
    /// FIFO algorithm: Iterate from lowest priority up to (but not including) the new particle's priority,
    /// removing the oldest (first) particle from each priority level until target count is met.
    ///
    /// Reference: EA Generals ParticleSys.cpp line 3201-3218
    /// </summary>
    /// <param name="count">Number of particles to remove</param>
    /// <param name="priorityCap">Don't remove particles at this priority or higher (except ALWAYS_RENDER)</param>
    /// <returns>True if removed exactly 'count' particles, false if ran out of removable particles</returns>
    private bool RemoveOldestParticles(int count, ParticleSystemPriority priorityCap)
    {
        int removed = 0;
        int priorityCapInt = (int)priorityCap;

        // Iterate from lowest priority upward
        for (int priority = 0; priority < priorityCapInt && removed < count; priority++)
        {
            var list = _particlesByPriority[priority];

            while (list.Count > 0 && removed < count)
            {
                var entry = list.First;
                var (system, particleIndex) = entry.Value;

                // Mark particle as dead in the system
                system.MarkParticleDead(particleIndex);

                list.RemoveFirst();
                _totalActiveParticles--;
                removed++;
            }
        }

        return removed == count;
    }

    /// <summary>
    /// Unregisters a particle when it dies or is removed.
    /// Called by ParticleSystem when a particle reaches end of life.
    /// </summary>
    internal void RemoveParticle(ParticleSystemPriority priority, LinkedListNode<(ParticleSystem, int)>? node)
    {
        if (node != null)
        {
            if (priority >= 0 && (int)priority < _particlesByPriority.Length)
            {
                _particlesByPriority[(int)priority].Remove(node);
                _totalActiveParticles--;
            }
        }
    }

    /// <summary>
    /// Gets current total active particle count across all systems.
    /// </summary>
    public int GetActiveParticleCount() => _totalActiveParticles;

    /// <summary>
    /// Gets the maximum allowed particle count (configurable via LOD settings).
    /// </summary>
    public int GetMaxParticleCount() => _maxParticleCount;

    public void Update(in TimeInterval gameTime)
    {
        var totalParticles = 0;

        for (var i = 0; i < _particleSystems.Count; i++)
        {
            var particleSystem = _particleSystems[i];

            if (particleSystem.State == ParticleSystemState.Inactive)
            {
                continue;
            }

            particleSystem.Update(gameTime);

            if (particleSystem.State == ParticleSystemState.Dead)
            {
                _renderBucket.RemoveObject(particleSystem);
                particleSystem.Dispose();
                RemoveToDispose(particleSystem);
                _particleSystems.RemoveAt(i);
                i--;
            }

            totalParticles += particleSystem.CurrentParticleCount;

            //if (totalParticles > _maxParticleCount)
            //{
            //    break;
            //}
        }

        // Sort particle systems by priority for correct rendering order
        // Reference: EA Generals ParticleSys.cpp - particle priority-based rendering
        // Higher priority systems render first (back to front for transparency)
        SortSystemsByPriority();
    }

    /// <summary>
    /// Sorts particle systems by priority in descending order (highest first).
    /// This ensures correct visual depth ordering for transparent particle effects.
    /// Reference: EA Generals ParticleSys.cpp line 1794 - priority-based particle culling
    /// </summary>
    private void SortSystemsByPriority()
    {
        _particleSystems.Sort((a, b) => {
            // Compare by priority (higher first, descending order)
            var priorityComparison = b.Template.Priority.CompareTo(a.Template.Priority);

            // If same priority, sort by template name for stable ordering
            if (priorityComparison == 0)
            {
                return a.Template.Name.CompareTo(b.Template.Name);
            }

            return priorityComparison;
        });
    }

    /// <summary>
    /// Extracts the material key from a particle system.
    /// Used to group systems with identical materials for batching.
    /// </summary>
    private static ParticleMaterialKey ExtractMaterialKey(ParticleSystem system)
    {
        var template = system.Template;
        var textureName = template.ParticleTexture?.Value?.Name ?? string.Empty;
        var shaderType = template.Shader;
        var isGroundAligned = template.IsGroundAligned;

        return new ParticleMaterialKey(shaderType, isGroundAligned, textureName ?? string.Empty);
    }

    /// <summary>
    /// Groups particle systems by their material for batching.
    /// Preserves priority order: systems within each group are sorted by priority.
    /// Expected: 50-100 systems → 15-40 groups (2-5 systems per group average).
    /// Reference: EA Generals dx8renderer.h - TextureCategory batching pattern
    /// </summary>
    public List<ParticleMaterialGroup> GroupSystemsByMaterial()
    {
        var groups = new Dictionary<ParticleMaterialKey, ParticleMaterialGroup>();
        var groupOrder = new List<ParticleMaterialKey>();

        // Iterate through systems in priority order (from previous sort)
        // This ensures priority is maintained within groups
        foreach (var system in _particleSystems)
        {
            if (system.State == ParticleSystemState.Inactive || system.CurrentParticleCount == 0)
            {
                continue;
            }

            var materialKey = ExtractMaterialKey(system);

            // Create group if it doesn't exist, preserving insertion order
            if (!groups.ContainsKey(materialKey))
            {
                var group = new ParticleMaterialGroup(materialKey);
                groups[materialKey] = group;
                groupOrder.Add(materialKey);
            }

            if (groups.TryGetValue(materialKey, out var existingGroup))
            {
                existingGroup.Systems.Add(system);
            }
        }

        // Return groups in order they were first encountered (preserves priority)
        return groupOrder.Select(key => groups[key]).ToList();
    }

    /// <summary>
    /// Sets up material-based batch rendering for all active particle systems.
    /// Replaces individual particle systems in the render bucket with batch renderers.
    ///
    /// Algorithm:
    /// 1. Group systems by material (call GroupSystemsByMaterial())
    /// 2. For each group, remove individual systems from render bucket
    /// 3. Create a ParticleBatchRenderer for the group
    /// 4. Add batch renderer to render bucket
    ///
    /// Expected draw call reduction: 40-70% (50-100 systems → 15-40 batches)
    /// Reference: EA Generals dx8renderer.h line 78 - TextureCategory batching
    /// </summary>
    public void SetupBatchRendering()
    {
        // Get current material groups
        var groups = GroupSystemsByMaterial();

        if (groups.Count == 0)
        {
            return; // No active systems to batch
        }

        // Remove individual systems from render bucket and create batch renderers
        foreach (var group in groups)
        {
            // Remove all systems in this group from individual rendering
            foreach (var system in group.Systems)
            {
                _renderBucket.RemoveObject(system);
            }

            // Create batch renderer for the group
            var batchRenderer = new ParticleBatchRenderer(group);

            // Add batch renderer to render bucket (will render all systems in group)
            _renderBucket.AddObject(batchRenderer);
        }
    }

    public void Persist(StatePersister reader)
    {
        reader.PersistVersion(1);

        reader.PersistUInt32(ref _previousParticleSystemId);

        var count = (uint)_particleSystems.Count;
        reader.PersistUInt32(ref count);

        reader.BeginArray("ParticleSystems");
        if (reader.Mode == StatePersistMode.Read)
        {
            for (var i = 0; i < count; i++)
            {
                reader.BeginObject();

                var templateName = "";
                reader.PersistAsciiString(ref templateName);

                if (templateName != string.Empty)
                {
                    var template = _loadContext.AssetStore.FXParticleSystemTemplates.GetByName(templateName);

                    var particleSystem = Create(
                        template,
                        Matrix4x4.Identity); // TODO

                    reader.PersistObject(particleSystem);
                }

                reader.EndObject();
            }
        }
        else
        {
            foreach (var particleSystem in _particleSystems)
            {
                reader.BeginObject();

                var templateName = particleSystem.Template.Name;
                reader.PersistAsciiString(ref templateName);

                reader.PersistObject(particleSystem);

                reader.EndObject();
            }
        }
        reader.EndArray();
    }
}
