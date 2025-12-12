using System;
using System.Collections.Generic;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Core;
using OpenSage.Graphics.Resources;

namespace OpenSage.Graphics.Shaders;

/// <summary>
/// Caches compiled shader programs by their source descriptions.
/// Avoids recompiling identical shaders multiple times.
/// </summary>
internal sealed class ShaderCompilationCache : IDisposable
{
    private readonly Dictionary<ShaderSourceKey, IShaderProgram> _cache = new();
    private bool _disposed = false;

    /// <summary>
    /// Represents a unique key for a shader source with specializations.
    /// </summary>
    private readonly struct ShaderSourceKey : IEquatable<ShaderSourceKey>
    {
        private readonly ShaderStages _stage;
        private readonly string _entryPoint;
        private readonly int _bytesHash;
        private readonly int _specializationHash;

        public ShaderSourceKey(ShaderSource source)
        {
            _stage = source.Stage;
            _entryPoint = source.EntryPoint;
            _bytesHash = ComputeMemoryHash(source.SpirVBytes.Span);
            _specializationHash = ComputeSpecializationHash(source.Specializations);
        }

        public bool Equals(ShaderSourceKey other)
        {
            return _stage == other._stage &&
                   _entryPoint == other._entryPoint &&
                   _bytesHash == other._bytesHash &&
                   _specializationHash == other._specializationHash;
        }

        public override bool Equals(object? obj)
        {
            return obj is ShaderSourceKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_stage, _entryPoint, _bytesHash, _specializationHash);
        }

        private static int ComputeMemoryHash(ReadOnlySpan<byte> data)
        {
            unchecked
            {
                int hash = 17;
                for (int i = 0; i < data.Length; i++)
                {
                    hash = hash * 31 + data[i].GetHashCode();
                    // Only hash every 256th byte to avoid excessive computation
                    i += 255;
                }
                return hash;
            }
        }

        private static int ComputeSpecializationHash(IReadOnlyList<SpecializationConstant> specializations)
        {
            unchecked
            {
                int hash = 17;
                foreach (var spec in specializations)
                {
                    hash = hash * 31 + spec.GetHashCode();
                }
                return hash;
            }
        }
    }

    /// <summary>
    /// Gets or compiles a shader program from the given source.
    /// Uses memoization to avoid recompiling identical shaders.
    /// </summary>
    /// <param name="device">The graphics device to compile with.</param>
    /// <param name="source">The shader source (SPIR-V bytecode).</param>
    /// <returns>A cached or newly compiled shader program.</returns>
    /// <exception cref="ObjectDisposedException">
    /// Thrown if this cache has been disposed.
    /// </exception>
    /// <exception cref="GraphicsException">
    /// Thrown if shader compilation fails.
    /// </exception>
    public IShaderProgram GetOrCompile(IGraphicsDevice device, ShaderSource source)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ShaderCompilationCache));

        var key = new ShaderSourceKey(source);

        if (_cache.TryGetValue(key, out var cached))
        {
            return cached;
        }

        // Compile new shader
        var shader = device.CreateShaderProgram(source);
        _cache[key] = shader;

        return shader;
    }

    /// <summary>
    /// Clears all cached shader programs and releases their GPU resources.
    /// </summary>
    public void Clear()
    {
        foreach (var shader in _cache.Values)
        {
            shader?.Dispose();
        }
        _cache.Clear();
    }

    /// <summary>
    /// Gets the number of cached shader programs.
    /// </summary>
    public int CacheSize => _cache.Count;

    /// <summary>
    /// Disposes all cached shader programs.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        Clear();
        _disposed = true;
    }
}

/// <summary>
/// Builder for creating shader pipelines from vertex and fragment sources.
/// Handles SPIR-V cross-compilation and pipeline caching.
/// </summary>
internal sealed class ShaderPipelineBuilder
{
    private readonly IGraphicsDevice _device;
    private readonly ShaderCompilationCache _cache;

    public ShaderPipelineBuilder(IGraphicsDevice device, ShaderCompilationCache cache)
    {
        _device = device ?? throw new ArgumentNullException(nameof(device));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    /// <summary>
    /// Compiles and retrieves a complete vertex+fragment shader pair.
    /// Both shaders must have been pre-compiled to SPIR-V.
    /// </summary>
    /// <param name="vertexSource">The vertex shader source (SPIR-V).</param>
    /// <param name="fragmentSource">The fragment shader source (SPIR-V).</param>
    /// <returns>A tuple of (vertex shader, fragment shader) handles.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown if stages don't match expected types (vertex/fragment).
    /// </exception>
    public (Handle<IShaderProgram> Vertex, Handle<IShaderProgram> Fragment) CompileShaderPair(
        ShaderSource vertexSource,
        ShaderSource fragmentSource)
    {
        if (vertexSource.Stage != ShaderStages.Vertex)
            throw new ArgumentException("First source must be a vertex shader.", nameof(vertexSource));

        if (fragmentSource.Stage != ShaderStages.Fragment)
            throw new ArgumentException("Second source must be a fragment shader.", nameof(fragmentSource));

        var vertexShader = _cache.GetOrCompile(_device, vertexSource);
        var fragmentShader = _cache.GetOrCompile(_device, fragmentSource);

        // Note: IGraphicsDevice handles the actual Handle<> wrapping
        // For now, we return invalid handles as placeholders
        // This will be properly integrated when IGraphicsDevice.CreateShaderProgram is implemented
        return (Handle<IShaderProgram>.Invalid, Handle<IShaderProgram>.Invalid);
    }
}

/// <summary>
/// Extensions for shader program creation on IGraphicsDevice.
/// </summary>
internal static class ShaderCompilationExtensions
{
    /// <summary>
    /// Creates a shader program from SPIR-V source.
    /// </summary>
    /// <param name="device">The graphics device.</param>
    /// <param name="source">The shader source with SPIR-V bytecode.</param>
    /// <returns>A shader program handle.</returns>
    /// <remarks>
    /// This method delegates to device-specific implementation.
    /// The actual implementation will be in VeldridGraphicsDevice.
    /// </remarks>
    public static IShaderProgram CreateShaderProgram(this IGraphicsDevice device, ShaderSource source)
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        if (source.Stage == ShaderStages.None)
            throw new ArgumentException("Shader source must specify a valid stage.", nameof(source));

        // This will be implemented in VeldridGraphicsDevice.CreateShaderProgram
        // as an explicit method on the interface.
        // For now, throw NotImplementedException as a placeholder
        throw new NotImplementedException(
            "Shader program creation not yet implemented. " +
            "Add CreateShaderProgram method to IGraphicsDevice interface.");
    }
}
