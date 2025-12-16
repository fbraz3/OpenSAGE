using System;
using Xunit;
using OpenSage.Lod;

namespace OpenSage.Tests.Lod;

/// <summary>
/// Integration tests for static LOD application.
/// Tests the StaticLodApplicationManager applying LOD settings to GameData.
/// 
/// Note: Since GameData and StaticGameLod have read-only properties,
/// these tests focus on:
/// 1. Manager initialization and state tracking
/// 2. Event firing
/// 3. LOD application logic flow
/// </summary>
public class StaticLodApplicationManagerTests
{
    /// <summary>
    /// Create a test GameData instance.
    /// </summary>
    private static GameData CreateTestGameData()
    {
        return new GameData();
    }

    [Fact]
    public void Constructor_WithNullGameData_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new StaticLodApplicationManager(null));
    }

    [Fact]
    public void Constructor_WithValidGameData_Succeeds()
    {
        // Arrange
        var gameData = CreateTestGameData();

        // Act
        var manager = new StaticLodApplicationManager(gameData);

        // Assert
        Assert.NotNull(manager);
        Assert.Equal(LodType.Medium, manager.CurrentAppliedLod);
    }

    [Fact]
    public void ApplyStaticLod_WithNullLodInfo_ThrowsArgumentNullException()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => manager.ApplyStaticLod(LodType.High, null));
    }

    [Fact]
    public void ApplyStaticLod_ChangesCurrentAppliedLod()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);
        var lodInfo = CreateMockStaticGameLod(LodType.High);

        // Act
        manager.ApplyStaticLod(LodType.High, lodInfo);

        // Assert
        Assert.Equal(LodType.High, manager.CurrentAppliedLod);
    }

    [Fact]
    public void ApplyStaticLod_SkippedWhenAlreadyAtSameLod()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);
        
        var lodInfo1 = CreateMockStaticGameLod(LodType.High);
        var lodInfo2 = CreateMockStaticGameLod(LodType.High);

        // Act - First application
        manager.ApplyStaticLod(LodType.High, lodInfo1);
        var firstLod = manager.CurrentAppliedLod;

        // Act - Second application with same LOD (should skip silently)
        manager.ApplyStaticLod(LodType.High, lodInfo2);
        var secondLod = manager.CurrentAppliedLod;

        // Assert - Both should be High (same LOD)
        Assert.Equal(LodType.High, firstLod);
        Assert.Equal(LodType.High, secondLod);
    }

    [Fact]
    public void ApplyStaticLod_FiresLodAppliedEvent()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);
        var eventFired = false;
        LodType? eventLodLevel = null;

        manager.LodApplied += (level, lod) =>
        {
            eventFired = true;
            eventLodLevel = level;
        };

        var lodInfo = CreateMockStaticGameLod(LodType.High);

        // Act
        manager.ApplyStaticLod(LodType.High, lodInfo);

        // Assert
        Assert.True(eventFired);
        Assert.Equal(LodType.High, eventLodLevel);
    }

    [Fact]
    public void ApplyStaticLod_EventNotFiredWhenSkipped()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);
        var eventFireCount = 0;

        manager.LodApplied += (level, lod) =>
        {
            eventFireCount++;
        };

        var lodInfo1 = CreateMockStaticGameLod(LodType.High);
        var lodInfo2 = CreateMockStaticGameLod(LodType.High);

        // Act - First application (should fire event)
        manager.ApplyStaticLod(LodType.High, lodInfo1);
        Assert.Equal(1, eventFireCount);

        // Act - Second application to same LOD (should NOT fire event)
        manager.ApplyStaticLod(LodType.High, lodInfo2);

        // Assert - Event should only fire once
        Assert.Equal(1, eventFireCount);
    }

    [Fact]
    public void ApplyStaticLod_SequentialApplications_UpdatesLodLevels()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);

        var highLod = CreateMockStaticGameLod(LodType.High);
        var lowLod = CreateMockStaticGameLod(LodType.Low);
        var mediumLod = CreateMockStaticGameLod(LodType.Medium);

        // Act & Assert - Apply High
        manager.ApplyStaticLod(LodType.High, highLod);
        Assert.Equal(LodType.High, manager.CurrentAppliedLod);

        // Act & Assert - Apply Low
        manager.ApplyStaticLod(LodType.Low, lowLod);
        Assert.Equal(LodType.Low, manager.CurrentAppliedLod);

        // Act & Assert - Apply Medium
        manager.ApplyStaticLod(LodType.Medium, mediumLod);
        Assert.Equal(LodType.Medium, manager.CurrentAppliedLod);
    }

    [Fact]
    public void ApplyStaticLod_AllLodLevels_Supported()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);

        var levels = new[] { LodType.Low, LodType.Medium, LodType.High, LodType.VeryHigh };

        // Act & Assert - Apply each LOD level and verify it was set
        foreach (var level in levels)
        {
            var lodInfo = CreateMockStaticGameLod(level);
            manager.ApplyStaticLod(level, lodInfo);
            Assert.Equal(level, manager.CurrentAppliedLod);
        }
    }

    [Fact]
    public void ApplyStaticLod_NoThrow_WithValidInput()
    {
        // Arrange
        var gameData = CreateTestGameData();
        var manager = new StaticLodApplicationManager(gameData);
        var lodInfo = CreateMockStaticGameLod(LodType.High);

        // Act - Should not throw
        manager.ApplyStaticLod(LodType.High, lodInfo);

        // Assert - Manager still functions correctly
        Assert.Equal(LodType.High, manager.CurrentAppliedLod);
    }

    // ===== HELPER METHODS =====

    /// <summary>
    /// Create a mock StaticGameLod using reflection to bypass read-only properties.
    /// </summary>
    private static StaticGameLod CreateMockStaticGameLod(
        LodType level,
        int maxParticleCount = 1000,
        bool useShadowVolumes = false,
        bool useShadowDecals = false,
        bool useShadowMapping = false,
        int textureReductionFactor = 0,
        bool useCloudMap = false,
        bool useLightMap = false,
        int maxTankTrackEdges = 100,
        int maxTankTrackOpaqueEdges = 50,
        int maxTankTrackFadeDelay = 10,
        bool useAnisotropic = false,
        bool usePixelShaders = false,
        bool useHighQualityVideo = false
    )
    {
        var lod = new StaticGameLod();
        
        // Use reflection to set read-only properties
        var levelProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.Level));
        levelProperty?.SetValue(lod, level);

        var maxParticleProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.MaxParticleCount));
        maxParticleProperty?.SetValue(lod, maxParticleCount);

        var shadowVolProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseShadowVolumes));
        shadowVolProperty?.SetValue(lod, useShadowVolumes);

        var shadowDecalProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseShadowDecals));
        shadowDecalProperty?.SetValue(lod, useShadowDecals);

        var shadowMapProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseShadowMapping));
        shadowMapProperty?.SetValue(lod, useShadowMapping);

        var textureReductionProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.TextureReductionFactor));
        textureReductionProperty?.SetValue(lod, textureReductionFactor);

        var cloudMapProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseCloudMap));
        cloudMapProperty?.SetValue(lod, useCloudMap);

        var lightMapProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseLightMap));
        lightMapProperty?.SetValue(lod, useLightMap);

        var maxTrackEdgesProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.MaxTankTrackEdges));
        maxTrackEdgesProperty?.SetValue(lod, maxTankTrackEdges);

        var maxOpaqueEdgesProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.MaxTankTrackOpaqueEdges));
        maxOpaqueEdgesProperty?.SetValue(lod, maxTankTrackOpaqueEdges);

        var fadeDelayProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.MaxTankTrackFadeDelay));
        fadeDelayProperty?.SetValue(lod, maxTankTrackFadeDelay);

        var anisotropicProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseAnisotropic));
        anisotropicProperty?.SetValue(lod, useAnisotropic);

        var pixelShaderProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UsePixelShaders));
        pixelShaderProperty?.SetValue(lod, usePixelShaders);

        var highQualityProperty = typeof(StaticGameLod).GetProperty(nameof(StaticGameLod.UseHighQualityVideo));
        highQualityProperty?.SetValue(lod, useHighQualityVideo);

        return lod;
    }
}
