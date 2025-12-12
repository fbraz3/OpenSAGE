using System;
using System.Collections.Generic;

namespace OpenSage.Graphics.Pooling;

/// <summary>
/// Generic resource pool with generation-based handle validation.
/// Prevents use-after-free errors by tracking handle generations.
/// Used by Veldrid adapter to manage lifetime of GPU resources.
/// </summary>
/// <typeparam name="T">The resource type to pool. Must be disposable.</typeparam>
public class ResourcePool<T> : IDisposable where T : class, IDisposable
{
    private T[] _resources;
    private uint[] _generations;
    private Queue<uint> _freeSlots;
    private uint _nextId;

    /// <summary>
    /// Initializes a new ResourcePool with specified initial capacity.
    /// </summary>
    /// <param name="initialCapacity">Initial number of resource slots. Defaults to 256.</param>
    public ResourcePool(int initialCapacity = 256)
    {
        if (initialCapacity <= 0)
            throw new ArgumentException("Initial capacity must be positive", nameof(initialCapacity));

        _resources = new T[initialCapacity];
        _generations = new uint[initialCapacity];
        _freeSlots = new Queue<uint>(initialCapacity);
        _nextId = 0;
    }

    /// <summary>
    /// Represents a pooled handle with generation validation.
    /// </summary>
    public readonly struct PoolHandle : IEquatable<PoolHandle>
    {
        /// <summary>Gets the resource index in the pool.</summary>
        public readonly uint Index;

        /// <summary>Gets the generation number for validation.</summary>
        public readonly uint Generation;

        /// <summary>Gets whether this handle is valid (not the invalid sentinel value).</summary>
        public bool IsValid => Index != uint.MaxValue;

        internal PoolHandle(uint index, uint generation)
        {
            Index = index;
            Generation = generation;
        }

        public static PoolHandle Invalid => new(uint.MaxValue, 0);

        public override bool Equals(object obj) => obj is PoolHandle ph && Equals(ph);
        public bool Equals(PoolHandle other) => Index == other.Index && Generation == other.Generation;
        public override int GetHashCode() => HashCode.Combine(Index, Generation);
    }

    /// <summary>
    /// Allocate a resource handle, reusing freed slots when possible.
    /// </summary>
    /// <param name="resource">The resource to allocate. Must not be null.</param>
    /// <returns>A handle with valid Index and Generation.</returns>
    public PoolHandle Allocate(T resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));

        uint idx;

        // Reuse freed slot
        if (_freeSlots.TryDequeue(out idx))
        {
            _resources[idx] = resource;
            _generations[idx]++;
            return new PoolHandle(idx, _generations[idx]);
        }

        // Allocate new slot
        if (_nextId >= _resources.Length)
            GrowCapacity();

        idx = _nextId++;
        _resources[idx] = resource;
        _generations[idx] = 1;
        return new PoolHandle(idx, 1);
    }

    /// <summary>
    /// Try to retrieve resource, validating handle generation.
    /// </summary>
    /// <param name="handle">Handle to look up.</param>
    /// <param name="resource">Output resource if found and valid.</param>
    /// <returns>True if handle is valid and generation matches, false otherwise.</returns>
    public bool TryGet(PoolHandle handle, out T resource)
    {
        if (!handle.IsValid || handle.Index >= _nextId)
        {
            resource = default;
            return false;
        }

        if (_generations[handle.Index] != handle.Generation)
        {
            // Handle is stale (use-after-free attempt)
            resource = default;
            return false;
        }

        resource = _resources[handle.Index];
        return resource != null;
    }

    /// <summary>
    /// Release a resource handle and dispose the resource.
    /// </summary>
    /// <param name="handle">Handle to release.</param>
    /// <returns>True if handle was valid and released, false otherwise.</returns>
    public bool Release(PoolHandle handle)
    {
        if (!TryGet(handle, out var resource))
            return false;

        resource?.Dispose();
        _resources[handle.Index] = null;
        _freeSlots.Enqueue(handle.Index);
        return true;
    }

    /// <summary>
    /// Check if a handle is currently valid in this pool.
    /// </summary>
    public bool IsValid(PoolHandle handle)
    {
        return handle.IsValid &&
               handle.Index < _nextId &&
               _generations[handle.Index] == handle.Generation &&
               _resources[handle.Index] != null;
    }

    /// <summary>
    /// Get total number of allocated resources (not counting freed slots).
    /// </summary>
    public int AllocatedCount => (int)(_nextId - _freeSlots.Count);

    /// <summary>
    /// Get number of free slots available for reuse.
    /// </summary>
    public int FreeSlots => _freeSlots.Count;

    /// <summary>
    /// Get current capacity of the pool.
    /// </summary>
    public int Capacity => _resources.Length;

    private void GrowCapacity()
    {
        var newCapacity = _resources.Length * 2;
        Array.Resize(ref _resources, newCapacity);
        Array.Resize(ref _generations, newCapacity);
    }

    /// <summary>
    /// Clear all resources and dispose them.
    /// </summary>
    public void Clear()
    {
        for (uint i = 0; i < _nextId; i++)
        {
            _resources[i]?.Dispose();
            _resources[i] = null;
        }
        _freeSlots.Clear();
        _nextId = 0;
    }

    public void Dispose()
    {
        Clear();
        _resources = null;
        _generations = null;
        _freeSlots = null;
    }
}
