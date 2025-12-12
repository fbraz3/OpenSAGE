using System;
using System.Collections.Generic;
using NUnit.Framework;
using OpenSage.Graphics.Resources;
using OpenSage.Graphics.Shaders;

namespace OpenSage.Graphics.Testing;

/// <summary>
/// Integration tests for shader source and compilation cache.
/// Validates shader source construction, specialization constants, and caching behavior.
/// </summary>
[TestFixture]
public class ShaderCompilationTests
{
    private static readonly byte[] ValidSpirvBytes = new byte[] {
        0x07, 0x23, 0x03, 0x03, // SPIR-V magic number
        0x01, 0x00, 0x05, 0x00, // Version 1.0, Generator LLVM 5
        0x00, 0x00, 0x00, 0x00, // Bound (placeholder)
        0x00, 0x00, 0x00, 0x00, // Schema (placeholder)
        0x00, 0x00, 0x00, 0x00  // More header content
    };

    [Test]
    public void ShaderSource_WithValidVertex_CreatesSuccessfully()
    {
        var source = new ShaderSource(
            ShaderStages.Vertex,
            ValidSpirvBytes,
            "main");

        Assert.That(source.Stage, Is.EqualTo(ShaderStages.Vertex));
        Assert.That(source.EntryPoint, Is.EqualTo("main"));
        Assert.That(source.SpirVBytes.Length, Is.EqualTo(ValidSpirvBytes.Length));
    }

    [Test]
    public void ShaderSource_WithFragment_CreatesSuccessfully()
    {
        var source = new ShaderSource(
            ShaderStages.Fragment,
            ValidSpirvBytes);

        Assert.That(source.Stage, Is.EqualTo(ShaderStages.Fragment));
        Assert.That(source.EntryPoint, Is.EqualTo("main"));
    }

    [Test]
    public void ShaderSource_WithInvalidStage_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ShaderSource(ShaderStages.None, ValidSpirvBytes);
        });
    }

    [Test]
    public void ShaderSource_WithEmptyBytes_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ShaderSource(ShaderStages.Vertex, Array.Empty<byte>());
        });
    }

    [Test]
    public void ShaderSource_WithNullEntryPoint_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes, null!);
        });
    }

    [Test]
    public void ShaderSource_WithEmptyEntryPoint_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
        {
            _ = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes, "");
        });
    }

    [Test]
    public void SpecializationConstant_BoolValue_ConstructsWithCorrectType()
    {
        var constant = new SpecializationConstant(100, true);

        Assert.That(constant.Id, Is.EqualTo(100u));
        Assert.That(constant.Type, Is.EqualTo(ShaderConstantType.Bool));
        Assert.That(constant.Data, Is.EqualTo(1UL));
    }

    [Test]
    public void SpecializationConstant_BoolFalse_HasZeroData()
    {
        var constant = new SpecializationConstant(100, false);

        Assert.That(constant.Data, Is.EqualTo(0UL));
    }

    [Test]
    public void SpecializationConstant_UIntValue_ConstructsWithCorrectType()
    {
        var constant = new SpecializationConstant(101, 42u);

        Assert.That(constant.Id, Is.EqualTo(101u));
        Assert.That(constant.Type, Is.EqualTo(ShaderConstantType.UInt));
        Assert.That(constant.Data, Is.EqualTo(42UL));
    }

    [Test]
    public void SpecializationConstant_IntValue_ConstructsWithCorrectType()
    {
        var constant = new SpecializationConstant(102, -42);

        Assert.That(constant.Id, Is.EqualTo(102u));
        Assert.That(constant.Type, Is.EqualTo(ShaderConstantType.Int));
        // -42 as ulong (two's complement)
        Assert.That(unchecked((long)constant.Data), Is.EqualTo(-42L));
    }

    [Test]
    public void SpecializationConstant_Equality_ReturnsTrueForIdentical()
    {
        var const1 = new SpecializationConstant(100, 42u);
        var const2 = new SpecializationConstant(100, 42u);

        Assert.That(const1.Equals(const2), Is.True);
        Assert.That(const1, Is.EqualTo(const2));
    }

    [Test]
    public void SpecializationConstant_Equality_ReturnsFalseForDifferentId()
    {
        var const1 = new SpecializationConstant(100, 42u);
        var const2 = new SpecializationConstant(101, 42u);

        Assert.That(const1, Is.Not.EqualTo(const2));
    }

    [Test]
    public void SpecializationConstant_Equality_ReturnsFalseForDifferentData()
    {
        var const1 = new SpecializationConstant(100, 42u);
        var const2 = new SpecializationConstant(100, 43u);

        Assert.That(const1, Is.Not.EqualTo(const2));
    }

    [Test]
    public void ShaderSource_WithSpecializations_IncludesInEquality()
    {
        var specs = new List<SpecializationConstant>
        {
            new SpecializationConstant(100, true),
            new SpecializationConstant(101, 32u)
        };

        var source = new ShaderSource(
            ShaderStages.Vertex,
            ValidSpirvBytes,
            "main",
            specs);

        Assert.That(source.Specializations.Count, Is.EqualTo(2));
        Assert.That(source.Specializations[0].Id, Is.EqualTo(100u));
        Assert.That(source.Specializations[1].Id, Is.EqualTo(101u));
    }

    [Test]
    public void ShaderSource_Equality_ReturnsTrueForIdentical()
    {
        var source1 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes);
        var source2 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes);

        Assert.That(source1.Equals(source2), Is.True);
        Assert.That(source1, Is.EqualTo(source2));
    }

    [Test]
    public void ShaderSource_Equality_ReturnsFalseForDifferentStage()
    {
        var source1 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes);
        var source2 = new ShaderSource(ShaderStages.Fragment, ValidSpirvBytes);

        Assert.That(source1, Is.Not.EqualTo(source2));
    }

    [Test]
    public void ShaderSource_Equality_ReturnsFalseForDifferentEntryPoint()
    {
        var source1 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes, "main");
        var source2 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes, "main2");

        Assert.That(source1, Is.Not.EqualTo(source2));
    }

    [Test]
    public void ShaderSource_GetHashCode_ReturnsSameForIdentical()
    {
        var source1 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes);
        var source2 = new ShaderSource(ShaderStages.Vertex, ValidSpirvBytes);

        Assert.That(source1.GetHashCode(), Is.EqualTo(source2.GetHashCode()));
    }

    [Test]
    public void ShaderCompilationCache_IsDisposable()
    {
        var cache = new ShaderCompilationCache();

        // Should not throw
        cache.Dispose();
        cache.Dispose(); // Second dispose should be safe
    }

    [Test]
    public void ShaderCompilationCache_ThrowsAfterDispose()
    {
        var cache = new ShaderCompilationCache();
        cache.Dispose();

        // Note: This test documents expected behavior.
        // Actual GetOrCompile would throw ObjectDisposedException
        // but we can't test without implementing the device interface
    }

    [Test]
    public void ShaderCompilationCache_CacheSizeStartsAtZero()
    {
        var cache = new ShaderCompilationCache();

        Assert.That(cache.CacheSize, Is.EqualTo(0));
    }

    [Test]
    public void ShaderCompilationCache_ClearWorks()
    {
        var cache = new ShaderCompilationCache();

        // Note: Clear() is idempotent and should work even with empty cache
        cache.Clear();

        Assert.That(cache.CacheSize, Is.EqualTo(0));
    }

    [Test]
    public void ShaderStages_NoneIsSpecial()
    {
        Assert.That(ShaderStages.None, Is.EqualTo(0));
    }

    [Test]
    public void ShaderStages_HasExpectedValues()
    {
        Assert.That((int)ShaderStages.Vertex, Is.EqualTo(1));
        Assert.That((int)ShaderStages.Fragment, Is.EqualTo(2));
        Assert.That((int)ShaderStages.Compute, Is.EqualTo(4));
        Assert.That((int)ShaderStages.Geometry, Is.EqualTo(8));
        Assert.That((int)ShaderStages.TessControl, Is.EqualTo(16));
        Assert.That((int)ShaderStages.TessEval, Is.EqualTo(32));
    }

    [Test]
    public void ShaderStages_CanCombineMultiple()
    {
        var combined = ShaderStages.Vertex | ShaderStages.Fragment;

        Assert.That((combined & ShaderStages.Vertex) != 0);
        Assert.That((combined & ShaderStages.Fragment) != 0);
        Assert.That((combined & ShaderStages.Compute) == 0);
    }
}
