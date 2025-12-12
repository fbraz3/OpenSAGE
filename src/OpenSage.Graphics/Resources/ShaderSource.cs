using System;
using System.Collections.Generic;

namespace OpenSage.Graphics.Resources;

/// <summary>
/// Specifies which stages of the graphics pipeline a shader operates in.
/// </summary>
[Flags]
public enum ShaderStages
{
    /// <summary>
    /// No shader stages (invalid).
    /// </summary>
    None = 0,

    /// <summary>
    /// Vertex shader stage (transforms vertices).
    /// </summary>
    Vertex = 1,

    /// <summary>
    /// Fragment/pixel shader stage (colors pixels).
    /// </summary>
    Fragment = 2,

    /// <summary>
    /// Compute shader stage (general GPU computing).
    /// Backend-dependent support.
    /// </summary>
    Compute = 4,

    /// <summary>
    /// Geometry shader stage (generates primitives from vertices).
    /// Backend-dependent support (not supported on Metal).
    /// </summary>
    Geometry = 8,

    /// <summary>
    /// Tessellation control shader stage (hull shader in D3D11).
    /// Backend-dependent support (not supported on Metal).
    /// </summary>
    TessControl = 16,

    /// <summary>
    /// Tessellation evaluation shader stage (domain shader in D3D11).
    /// Backend-dependent support (not supported on Metal).
    /// </summary>
    TessEval = 32,
}

/// <summary>
/// Represents a compile-time constant that can be specialized in shaders.
/// Used for shader compilation variants.
/// </summary>
public readonly struct SpecializationConstant : IEquatable<SpecializationConstant>
{
    /// <summary>
    /// Gets the constant ID (must match SPIR-V constant ID).
    /// </summary>
    public uint Id { get; }

    /// <summary>
    /// Gets the constant type.
    /// </summary>
    public ShaderConstantType Type { get; }

    /// <summary>
    /// Gets the constant value (8 bytes, interpreted per Type).
    /// </summary>
    public ulong Data { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpecializationConstant"/> struct.
    /// </summary>
    /// <param name="id">The constant ID.</param>
    /// <param name="value">The constant value (bool/uint/int).</param>
    public SpecializationConstant(uint id, bool value)
    {
        Id = id;
        Type = ShaderConstantType.Bool;
        Data = value ? 1UL : 0UL;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpecializationConstant"/> struct.
    /// </summary>
    /// <param name="id">The constant ID.</param>
    /// <param name="value">The constant value (unsigned int).</param>
    public SpecializationConstant(uint id, uint value)
    {
        Id = id;
        Type = ShaderConstantType.UInt;
        Data = value;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpecializationConstant"/> struct.
    /// </summary>
    /// <param name="id">The constant ID.</param>
    /// <param name="value">The constant value (signed int).</param>
    public SpecializationConstant(uint id, int value)
    {
        Id = id;
        Type = ShaderConstantType.Int;
        Data = unchecked((ulong)(long)value);
    }

    public bool Equals(SpecializationConstant other)
    {
        return Id == other.Id && Type == other.Type && Data == other.Data;
    }

    public override bool Equals(object? obj)
    {
        return obj is SpecializationConstant other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Id, Type, Data);
    }

    public static bool operator ==(SpecializationConstant left, SpecializationConstant right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(SpecializationConstant left, SpecializationConstant right)
    {
        return !left.Equals(right);
    }
}

/// <summary>
/// Specifies the type of a shader specialization constant.
/// </summary>
public enum ShaderConstantType
{
    /// <summary>
    /// Boolean constant (0 = false, 1 = true).
    /// </summary>
    Bool,

    /// <summary>
    /// Unsigned 32-bit integer constant.
    /// </summary>
    UInt,

    /// <summary>
    /// Signed 32-bit integer constant.
    /// </summary>
    Int,
}

/// <summary>
/// Describes a shader source with pre-compiled SPIR-V bytecode.
/// SPIR-V is an intermediate representation that Veldrid cross-compiles to backend-specific formats.
/// </summary>
public readonly struct ShaderSource : IEquatable<ShaderSource>
{
    /// <summary>
    /// Gets the shader stage this source operates in.
    /// </summary>
    public ShaderStages Stage { get; }

    /// <summary>
    /// Gets the SPIR-V bytecode.
    /// This must be pre-compiled from GLSL/HLSL source using a tool like glslc.
    /// </summary>
    public ReadOnlyMemory<byte> SpirVBytes { get; }

    /// <summary>
    /// Gets the entry point function name (typically "main").
    /// </summary>
    public string EntryPoint { get; }

    /// <summary>
    /// Gets the specialization constants for this shader.
    /// These are compile-time constants that allow creating shader variants.
    /// </summary>
    public IReadOnlyList<SpecializationConstant> Specializations { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ShaderSource"/> struct.
    /// </summary>
    /// <param name="stage">The shader stage.</param>
    /// <param name="spirvBytes">The SPIR-V bytecode.</param>
    /// <param name="entryPoint">The entry point function name.</param>
    /// <param name="specializations">Optional specialization constants.</param>
    /// <exception cref="ArgumentException">
    /// Thrown if stage is None, spirvBytes is empty, or entryPoint is null/empty.
    /// </exception>
    public ShaderSource(
        ShaderStages stage,
        ReadOnlyMemory<byte> spirvBytes,
        string entryPoint = "main",
        IReadOnlyList<SpecializationConstant>? specializations = null)
    {
        if (stage == ShaderStages.None)
            throw new ArgumentException("Shader stage cannot be None.", nameof(stage));

        if (spirvBytes.IsEmpty)
            throw new ArgumentException("SPIR-V bytecode cannot be empty.", nameof(spirvBytes));

        if (string.IsNullOrWhiteSpace(entryPoint))
            throw new ArgumentException("Entry point cannot be null or empty.", nameof(entryPoint));

        Stage = stage;
        SpirVBytes = spirvBytes;
        EntryPoint = entryPoint;
        Specializations = specializations ?? Array.Empty<SpecializationConstant>();
    }

    public bool Equals(ShaderSource other)
    {
        return Stage == other.Stage &&
               SpirVBytes.Span.SequenceEqual(other.SpirVBytes.Span) &&
               EntryPoint == other.EntryPoint &&
               SpecializationsEqual(Specializations, other.Specializations);
    }

    public override bool Equals(object? obj)
    {
        return obj is ShaderSource other && Equals(other);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            Stage,
            EntryPoint,
            SpirVBytes.GetHashCode(),
            Specializations.Count);
    }

    public static bool operator ==(ShaderSource left, ShaderSource right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ShaderSource left, ShaderSource right)
    {
        return !left.Equals(right);
    }

    private static bool SpecializationsEqual(
        IReadOnlyList<SpecializationConstant> left,
        IReadOnlyList<SpecializationConstant> right)
    {
        if (left.Count != right.Count)
            return false;

        for (int i = 0; i < left.Count; i++)
        {
            if (!left[i].Equals(right[i]))
                return false;
        }

        return true;
    }
}
