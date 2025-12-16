#nullable enable

using System;
using System.Collections.Generic;
using Xunit;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Graphics.Shaders;

namespace OpenSageGameTests.Graphics.ParticleSystems;

/// <summary>
/// Unit tests for PLAN-012 Stage 2: Material-based particle draw call batching.
/// Tests grouping logic that enables reducing 50-100 draw calls to 15-40 by batching systems with identical materials.
/// Reference: EA Generals dx8renderer.h line 78 - TextureCategory batching pattern
/// </summary>
public class ParticleMaterialBatchingTests
{
    /// <summary>
    /// Test that ParticleMaterialKey correctly identifies identical materials.
    /// Two keys with same shader, ground alignment, and texture should be equal.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_Equality_IdenticalKeys()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test_texture");

        var key2 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test_texture");

        // Act & Assert
        Assert.Equal(key1, key2);
        Assert.Equal(key1.GetHashCode(), key2.GetHashCode());
    }

    /// <summary>
    /// Test that ParticleMaterialKey distinguishes materials with different shaders.
    /// Alpha and Additive shaders should create different batches.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_Inequality_DifferentShader()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test_texture");

        var key2 = new ParticleMaterialKey(
            ParticleSystemShader.Additive,
            isGroundAligned: false,
            "test_texture");

        // Act & Assert
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Test that ParticleMaterialKey distinguishes materials with different ground alignment.
    /// Camera-facing and ground-facing particles need separate render passes.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_Inequality_DifferentGroundAlignment()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test_texture");

        var key2 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: true,
            "test_texture");

        // Act & Assert
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Test that ParticleMaterialKey distinguishes materials with different textures.
    /// Different particle textures require separate draw calls.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_Inequality_DifferentTexture()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "texture1");

        var key2 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "texture2");

        // Act & Assert
        Assert.NotEqual(key1, key2);
    }

    /// <summary>
    /// Test that ParticleMaterialKey normalizes null texture names to empty strings.
    /// Null and empty string should be treated identically for batching purposes.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_NullTexture_TreatedAsEmpty()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            null!);

        var key2 = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            string.Empty);

        // Act & Assert
        // Both should normalize null to empty string
        Assert.Equal(key1, key2);
    }

    /// <summary>
    /// Test that ParticleMaterialGroup constructor initializes with empty systems list.
    /// Each group tracks systems with identical materials.
    /// </summary>
    [Fact]
    public void ParticleMaterialGroup_Constructor_CreatesEmptySystemList()
    {
        // Arrange
        var key = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test");

        // Act
        var group = new ParticleMaterialGroup(key);

        // Assert
        Assert.Equal(key, group.MaterialKey);
        Assert.Empty(group.Systems);
        Assert.Equal(0, group.SystemCount);
    }

    /// <summary>
    /// Test that ParticleMaterialGroup maintains correct system count.
    /// SystemCount property should reflect actual Systems list length.
    /// </summary>
    [Fact]
    public void ParticleMaterialGroup_SystemCount_ReflectsListSize()
    {
        // Arrange
        var key = new ParticleMaterialKey(
            ParticleSystemShader.Alpha,
            isGroundAligned: false,
            "test");

        var group = new ParticleMaterialGroup(key);

        // Act - add some systems (mock)
        group.Systems.Add(null!); // Mock ParticleSystem
        group.Systems.Add(null!);
        group.Systems.Add(null!);

        // Assert
        Assert.Equal(3, group.SystemCount);
    }

    /// <summary>
    /// Test material key hashing consistency.
    /// Equal keys must produce equal hash codes (required for Dictionary usage).
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_HashCode_ConsistentForEqualKeys()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(ParticleSystemShader.Additive, true, "fire");
        var key2 = new ParticleMaterialKey(ParticleSystemShader.Additive, true, "fire");

        var dict = new Dictionary<ParticleMaterialKey, int>();

        // Act
        dict[key1] = 1;
        dict[key2] = 2; // Should update existing entry, not add new one

        // Assert
        Assert.Single(dict);
        Assert.Equal(2, dict[key1]);
    }

    /// <summary>
    /// Test material key operator overloads.
    /// Equality operators should match Equals() behavior.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_Operators_MatchEqualsMethod()
    {
        // Arrange
        var key1 = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "smoke");
        var key2 = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "smoke");
        var key3 = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "fire");

        // Act & Assert
        Assert.True(key1 == key2);
        Assert.False(key1 != key2);
        Assert.False(key1 == key3);
        Assert.True(key1 != key3);
    }

    /// <summary>
    /// Test all material key distinguishing factors simultaneously.
    /// Verifies that changing ANY field creates a different key.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_AllFieldsCombined_CreateUniqueKeys()
    {
        // Arrange - create 8 unique keys by varying each bit
        var baseKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "tex");
        
        var shaderKey = new ParticleMaterialKey(ParticleSystemShader.Additive, false, "tex");
        var alignKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, true, "tex");
        var textureKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "tex2");

        // Act & Assert
        Assert.Equal(baseKey, baseKey);
        Assert.NotEqual(baseKey, shaderKey);
        Assert.NotEqual(baseKey, alignKey);
        Assert.NotEqual(baseKey, textureKey);
    }

    /// <summary>
    /// Test ParticleMaterialKey with edge case: empty string texture.
    /// Empty textures should be treated same as null.
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_EmptyTexture_SameAsNull()
    {
        // Arrange
        var nullKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, null!);
        var emptyKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "");

        // Act & Assert
        Assert.Equal(nullKey, emptyKey);
    }

    /// <summary>
    /// Test ParticleMaterialKey case sensitivity for texture names.
    /// Texture names should be case-sensitive (file systems typically are).
    /// </summary>
    [Fact]
    public void ParticleMaterialKey_TextureCase_IsSensitive()
    {
        // Arrange
        var lowerKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "texture");
        var upperKey = new ParticleMaterialKey(ParticleSystemShader.Alpha, false, "TEXTURE");

        // Act & Assert
        Assert.NotEqual(lowerKey, upperKey);
    }
}
