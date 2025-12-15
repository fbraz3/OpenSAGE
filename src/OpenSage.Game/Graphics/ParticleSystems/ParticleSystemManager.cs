#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using OpenSage.Content.Loaders;
using OpenSage.Rendering;
//using OpenSage.Graphics.Rendering;

namespace OpenSage.Graphics.ParticleSystems;

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
        // TODO: Sort by ParticleSystem.Priority.

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
