using System;
using System.Collections.Generic;
using OpenSage.Graphics.Core;

namespace OpenSage.Graphics.Abstractions;

/// <summary>
/// Base interface for all graphics resources.
/// Provides generation-based validation to prevent use-after-free errors.
/// </summary>
public interface IGraphicsResource
{
    /// <summary>
    /// Gets the unique identifier of this resource.
    /// </summary>
    uint Id { get; }

    /// <summary>
    /// Gets the generation number of this resource.
    /// Incremented each time a resource with the same ID is created.
    /// </summary>
    uint Generation { get; }

    /// <summary>
    /// Gets a value indicating whether this resource is currently valid.
    /// A resource becomes invalid when it is disposed.
    /// </summary>
    bool IsValid { get; }
}

/// <summary>
/// Type-safe opaque handle to a graphics resource.
/// Prevents direct access to graphics API objects and enforces generation-based validation.
/// </summary>
/// <typeparam name="T">The type of graphics resource this handle references.</typeparam>
public readonly struct Handle<T> : IEquatable<Handle<T>>
    where T : IGraphicsResource
{
    /// <summary>
    /// Gets a value indicating whether this is a valid handle.
    /// </summary>
    public bool IsValid => _id != InvalidId;

    private const uint InvalidId = uint.MaxValue;

    private readonly uint _id;
    private readonly uint _generation;

    /// <summary>
    /// Gets an invalid/null handle.
    /// </summary>
    public static Handle<T> Invalid => default;

    /// <summary>
    /// Initializes a new instance of the <see cref="Handle{T}"/> struct.
    /// </summary>
    /// <param name="id">The resource identifier.</param>
    /// <param name="generation">The generation counter.</param>
    internal Handle(uint id, uint generation)
    {
        _id = id;
        _generation = generation;
    }

    /// <summary>
    /// Validates that this handle matches the given resource's current generation.
    /// Throws an exception if the handle is no longer valid.
    /// </summary>
    /// <param name="resource">The resource to validate against.</param>
    /// <exception cref="GraphicsException">
    /// Thrown if the handle's ID or generation does not match the resource's current state.
    /// </exception>
    public void ValidateOrThrow(IGraphicsResource resource)
    {
        if (_id != resource.Id || _generation != resource.Generation)
        {
            throw new GraphicsException(
                $"Handle is invalid. Resource has been disposed or reallocated. " +
                $"Expected ID: {_id}, Generation: {_generation}; " +
                $"Current ID: {resource.Id}, Generation: {resource.Generation}");
        }
    }

    /// <summary>
    /// Validates that this handle matches the given resource's current generation.
    /// Returns false if the handle is no longer valid.
    /// </summary>
    /// <param name="resource">The resource to validate against.</param>
    /// <returns>True if the handle is valid; false otherwise.</returns>
    public bool IsValidFor(IGraphicsResource resource)
    {
        return IsValid && _id == resource.Id && _generation == resource.Generation;
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current handle.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is Handle<T> other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified handle is equal to the current handle.
    /// </summary>
    public bool Equals(Handle<T> other)
    {
        return _id == other._id && _generation == other._generation;
    }

    /// <summary>
    /// Returns the hash code for this handle.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(_id, _generation);
    }

    /// <summary>
    /// Determines whether two handles are equal.
    /// </summary>
    public static bool operator ==(Handle<T> left, Handle<T> right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two handles are not equal.
    /// </summary>
    public static bool operator !=(Handle<T> left, Handle<T> right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Allocator for type-safe resource handles with generation-based validation.
/// Ensures handles become invalid when their resources are deallocated.
/// </summary>
/// <typeparam name="T">The type of graphics resource.</typeparam>
public sealed class HandleAllocator<T>
    where T : IGraphicsResource
{
    private uint _nextId = 0;
    private readonly Dictionary<uint, uint> _generations = new();

    /// <summary>
    /// Allocates a new handle with generation counter.
    /// </summary>
    /// <returns>A new handle for resource allocation.</returns>
    public Handle<T> Allocate()
    {
        uint id = _nextId++;
        uint generation = 0;

        if (!_generations.TryAdd(id, generation))
        {
            // Reusing an ID, increment the generation to invalidate old handles
            generation = ++_generations[id];
        }

        return new Handle<T>(id, generation);
    }

    /// <summary>
    /// Increments the generation for a given handle ID.
    /// This invalidates all existing handles pointing to this ID.
    /// </summary>
    /// <param name="id">The resource ID to increment the generation for.</param>
    public void InvalidateId(uint id)
    {
        if (_generations.ContainsKey(id))
        {
            _generations[id]++;
        }
    }

    /// <summary>
    /// Gets the current generation number for a resource ID.
    /// </summary>
    /// <param name="id">The resource ID.</param>
    /// <param name="generation">The current generation number.</param>
    /// <returns>True if the ID exists; false otherwise.</returns>
    public bool TryGetGeneration(uint id, out uint generation)
    {
        return _generations.TryGetValue(id, out generation);
    }
}
