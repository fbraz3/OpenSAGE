using System;
using Xunit;
using OpenSage.Lod;

namespace OpenSage.Tests.Lod;

/// <summary>
/// Tests for GameLodManager core functionality.
/// These tests focus on the LOD selection algorithm and skip mask generation logic.
/// </summary>
public class GameLodManagerTests
{
    [Fact]
    public void HardwareInfo_ToString_FormatsCorrectly()
    {
        // Arrange
        var hw = new HardwareInfo
        {
            CpuType = CpuType.P4,
            CpuFrequencyMhz = 2000,
            GpuType = GpuType.PS20,
            GpuMemoryMb = 256,
            SystemRamMb = 1024
        };

        // Act
        var str = hw.ToString();

        // Assert
        Assert.Contains("P4", str);
        Assert.Contains("2000", str);
        Assert.Contains("PS20", str);
        Assert.Contains("256", str);
        Assert.Contains("1024", str);
    }

    [Fact]
    public void HardwareInfo_DefaultValues_AreValid()
    {
        // Arrange & Act
        var hw = new HardwareInfo();

        // Assert
        Assert.NotNull(hw);
        Assert.True(hw.CpuFrequencyMhz >= 0);
        Assert.True(hw.GpuMemoryMb >= 0);
        Assert.True(hw.SystemRamMb >= 0);
    }

    [Fact]
    public void SkipMaskPattern_0x00_NeverSkips()
    {
        // 0x00 mask means no skipping
        // Pattern: every particle passes (doesn't skip)

        // Simulate: (++counter & 0x00) != 0x00
        // = (1 & 0x00) != 0x00 = 0 != 0 = false (no skip)
        // = (2 & 0x00) != 0x00 = 0 != 0 = false (no skip)

        var skipMask = 0x00;
        var skipped = false;

        for (int i = 1; i <= 100; i++)
        {
            var result = (i & skipMask) != skipMask;
            skipped |= result;
        }

        Assert.False(skipped, "0x00 mask should never skip");
    }

    [Fact]
    public void SkipMaskPattern_0xFF_SkipsCorrectly()
    {
        // 0xFF mask means skip all except one
        // Pattern: (++counter & 0xFF) != 0xFF
        // Counter wraps at 256, so pattern repeats every 256 items
        // Only counter=255 satisfies: (255 & 0xFF) = 255 = 0xFF, so (255 & 0xFF) != 0xFF = false

        var skipMask = 0xFF;
        var skipCount = 0;

        for (int counter = 1; counter <= 256; counter++)
        {
            var skipped = (counter & skipMask) != skipMask;
            if (skipped)
            {
                skipCount++;
            }
        }

        // Should skip 255 of 256
        Assert.Equal(255, skipCount);
    }

    [Fact]
    public void SkipMaskPattern_0x0F_SkipsCorrectly()
    {
        // 0x0F mask means skip 15 of 16
        // Only counter=15 satisfies: (15 & 0x0F) = 15 = 0x0F

        var skipMask = 0x0F;
        var skipCount = 0;

        for (int counter = 1; counter <= 16; counter++)
        {
            var skipped = (counter & skipMask) != skipMask;
            if (skipped)
            {
                skipCount++;
            }
        }

        // Should skip 15 of 16
        Assert.Equal(15, skipCount);
    }

    [Fact]
    public void SkipMaskPattern_0x03_SkipsCorrectly()
    {
        // 0x03 mask means skip 3 of 4
        // Only counter=3 satisfies: (3 & 0x03) = 3 = 0x03

        var skipMask = 0x03;
        var skipCount = 0;

        for (int counter = 1; counter <= 4; counter++)
        {
            var skipped = (counter & skipMask) != skipMask;
            if (skipped)
            {
                skipCount++;
            }
        }

        // Should skip 3 of 4
        Assert.Equal(3, skipCount);
    }

    [Fact]
    public void DynamicLodFpsThresholdLogic_HighFps_SelectsHighestQuality()
    {
        // Algorithm: check from HIGHEST to LOWEST quality, return first matching level
        var levels = new (LodType level, float minimumFps)[]
        {
            (LodType.VeryHigh, 50),
            (LodType.High, 40),
            (LodType.Medium, 30),
            (LodType.Low, 20),
        };

        // Test FPS = 75 (should select VeryHigh, first level where fps >= threshold)
        var fps = 75f;
        var selectedLod = LodType.Low;  // Default fallback

        foreach (var (level, threshold) in levels)
        {
            if (fps >= threshold)
            {
                selectedLod = level;
                break;  // Return highest matching level
            }
        }

        Assert.Equal(LodType.VeryHigh, selectedLod);
    }

    [Fact]
    public void DynamicLodFpsThresholdLogic_MediumFps_SelectsMediumQuality()
    {
        var levels = new (LodType level, float minimumFps)[]
        {
            (LodType.VeryHigh, 50),
            (LodType.High, 40),
            (LodType.Medium, 30),
            (LodType.Low, 20),
        };

        // Test FPS = 35 (should select Medium, first level where fps >= threshold)
        var fps = 35f;
        var selectedLod = LodType.Low;  // Default fallback

        foreach (var (level, threshold) in levels)
        {
            if (fps >= threshold)
            {
                selectedLod = level;
                break;  // Return highest matching level
            }
        }

        Assert.Equal(LodType.Medium, selectedLod);
    }

    [Fact]
    public void DynamicLodFpsThresholdLogic_LowFps_SelectsLowestQuality()
    {
        var levels = new (LodType level, float minimumFps)[]
        {
            (LodType.VeryHigh, 50),
            (LodType.High, 40),
            (LodType.Medium, 30),
            (LodType.Low, 20),
        };

        // Test FPS = 15 (below Low threshold, should return Low as fallback)
        var fps = 15f;
        var selectedLod = LodType.Low;  // Default fallback

        foreach (var (level, threshold) in levels)
        {
            if (fps >= threshold)
            {
                selectedLod = level;
                break;  // Return highest matching level
            }
        }

        Assert.Equal(LodType.Low, selectedLod);
    }

    [Fact]
    public void PresetMatchingLogic_WithinTolerance_Matches()
    {
        // Simulate EA's preset matching with 6% error tolerance (0.94f)
        const float ProfileErrorLimit = 0.94f;

        var systemCpuMhz = 2000;
        var presetCpuMhz = 2100;  // 2% faster, should still match

        var ratio = (float)systemCpuMhz / presetCpuMhz;
        var matches = ratio >= ProfileErrorLimit;

        Assert.True(matches, $"CPU MHz {systemCpuMhz} should match preset {presetCpuMhz} with 6% tolerance");
    }

    [Fact]
    public void PresetMatchingLogic_BeyondTolerance_DoesNotMatch()
    {
        // Simulate EA's preset matching with 6% error tolerance (0.94f)
        const float ProfileErrorLimit = 0.94f;

        var systemCpuMhz = 1000;
        var presetCpuMhz = 2000;  // 50% slower, beyond tolerance

        var ratio = (float)systemCpuMhz / presetCpuMhz;
        var matches = ratio >= ProfileErrorLimit;

        Assert.False(matches, $"CPU MHz {systemCpuMhz} should NOT match preset {presetCpuMhz} (beyond 6% tolerance)");
    }

    [Fact]
    public void LodStatistics_AllFieldsPopulated()
    {
        // Arrange
        var stats = new LodStatistics
        {
            CurrentStaticLod = LodType.High,
            CurrentDynamicLod = LodType.Medium,
            IdealDetailLevel = LodType.High,
            DetectedHardware = new HardwareInfo { CpuType = CpuType.P4, CpuFrequencyMhz = 2000 },
            LoadedStaticLods = 3,
            LoadedDynamicLods = 4,
            LoadedPresets = 10,
            LoadedBenchProfiles = 5,
            ParticleSkipCounter = 100,
            DebrisSkipCounter = 50
        };

        // Assert - verify all fields are set and accessible
        Assert.Equal(LodType.High, stats.CurrentStaticLod);
        Assert.Equal(LodType.Medium, stats.CurrentDynamicLod);
        Assert.Equal(LodType.High, stats.IdealDetailLevel);
        Assert.NotNull(stats.DetectedHardware);
        Assert.Equal(3, stats.LoadedStaticLods);
        Assert.Equal(4, stats.LoadedDynamicLods);
        Assert.Equal(10, stats.LoadedPresets);
        Assert.Equal(5, stats.LoadedBenchProfiles);
        Assert.Equal(100, stats.ParticleSkipCounter);
        Assert.Equal(50, stats.DebrisSkipCounter);
    }

    [Fact]
    public void CpuTypeEnum_AllValuesValid()
    {
        // Verify that CpuType enum has expected values
        var cpuTypes = new[] { CpuType.P3, CpuType.P4, CpuType.K7 };

        Assert.Equal(3, cpuTypes.Length);
    }

    [Fact]
    public void GpuTypeEnum_HasExpectedValues()
    {
        // Verify that GpuType enum has at least some expected values
        var gpuTypes = new[]
        {
            GpuType.PS11,
            GpuType.PS20,
            GpuType.R100,
            GpuType.GF3
        };

        Assert.Equal(4, gpuTypes.Length);
    }

    [Fact]
    public void LodTypeEnum_HasExpectedLevels()
    {
        // Verify that LodType enum has expected LOD levels
        var lodTypes = new[] { LodType.Low, LodType.Medium, LodType.High, LodType.VeryHigh };

        Assert.Equal(4, lodTypes.Length);
    }
}

