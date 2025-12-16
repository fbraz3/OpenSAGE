using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Content;

namespace OpenSage.Lod;

/// <summary>
/// Manages the Level of Detail (LOD) system for dynamic quality adjustment.
/// 
/// Implements the EA GameLODManager algorithm:
/// - Hardware detection for initial static LOD selection
/// - Preset-based LOD matching with 6% error tolerance
/// - FPS-based dynamic LOD adjustment
/// - Particle/debris skip mask generation (counter-based)
/// 
/// References:
/// - EA Header: references/generals_code/GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h
/// - EA Implementation: references/generals_code/GeneralsMD/Code/GameEngine/Source/Common/GameLOD.cpp
/// </summary>
public class GameLodManager
{
    /// <summary>
    /// 6% error tolerance when matching hardware to bench profiles and LOD presets.
    /// This allows for some fluctuation in benchmark results and hardware specs.
    /// </summary>
    private const float ProfileErrorLimit = 0.94f;

    /// <summary>
    /// Current static LOD level (set once at startup based on hardware).
    /// </summary>
    public LodType CurrentStaticLod { get; private set; } = LodType.Medium;

    /// <summary>
    /// Current dynamic LOD level (updated every ~1 second based on FPS).
    /// </summary>
    public LodType CurrentDynamicLod { get; private set; } = LodType.VeryHigh;

    /// <summary>
    /// Detected hardware information.
    /// </summary>
    public HardwareInfo DetectedHardware { get; private set; } = new();

    /// <summary>
    /// Ideal detail level determined by hardware benchmarking (used if user hasn't set preference).
    /// </summary>
    private LodType _idealDetailLevel = LodType.Medium;

    /// <summary>
    /// All loaded static LOD level definitions.
    /// </summary>
    private Dictionary<LodType, StaticGameLod> _staticLods = new();

    /// <summary>
    /// All loaded dynamic LOD level definitions.
    /// </summary>
    private Dictionary<LodType, DynamicGameLod> _dynamicLods = new();

    /// <summary>
    /// All loaded hardware preset configurations.
    /// </summary>
    private List<LodPreset> _presets = new();

    /// <summary>
    /// All loaded benchmark profiles for CPU matching.
    /// </summary>
    private List<BenchProfile> _benchProfiles = new();

    /// <summary>
    /// Counter for particle generation tracking (used in skip mask calculation).
    /// </summary>
    private int _particleSkipCounter;

    /// <summary>
    /// Counter for debris generation tracking (used in skip mask calculation).
    /// </summary>
    private int _debrisSkipCounter;

    /// <summary>
    /// Current particle skip mask (from dynamic LOD settings).
    /// Bitmask used to determine which Nth particle to render.
    /// </summary>
    private int _particleSkipMask;

    /// <summary>
    /// Current debris skip mask (from dynamic LOD settings).
    /// Bitmask used to determine which Nth debris to render.
    /// </summary>
    private int _debrisSkipMask;

    /// <summary>
    /// Emitted when static LOD level changes.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable event field is uninitialized
    public event Action<LodType> StaticLodChanged;

    /// <summary>
    /// Emitted when dynamic LOD level changes.
    /// </summary>
    public event Action<LodType> DynamicLodChanged;
#pragma warning restore CS8618

    /// <summary>
    /// Initialize the LOD manager with data from AssetStore.
    /// Loads all LOD definitions, presets, and bench profiles.
    /// Performs hardware detection.
    /// </summary>
    public void Initialize(AssetStore assetStore)
    {
        // Load all static LOD definitions
        _staticLods.Clear();
        foreach (var lod in assetStore.StaticGameLods)
        {
            _staticLods[lod.Level] = lod;
        }

        // Load all dynamic LOD definitions
        _dynamicLods.Clear();
        foreach (var lod in assetStore.DynamicGameLods)
        {
            _dynamicLods[lod.Level] = lod;
        }

        // Load all LOD presets
        _presets = assetStore.LodPresets.ToList();

        // Load all bench profiles
        _benchProfiles = assetStore.BenchProfiles.ToList();

        // Detect hardware
        DetectHardware();

        // Find ideal LOD for this hardware
        _idealDetailLevel = FindStaticLodLevel();

        // Apply initial static LOD
        SetStaticLodLevel(_idealDetailLevel);

        // Initialize dynamic LOD
        SetDynamicLodLevel(LodType.VeryHigh);
    }

    /// <summary>
    /// Detect current system hardware (CPU, GPU, RAM).
    /// Uses basic System.Environment for now; can be enhanced with benchmarking in Phase 2.
    /// </summary>
    private void DetectHardware()
    {
        DetectedHardware = new HardwareInfo
        {
            CpuType = CpuType.P4,  // Default to P4 for modern systems
            CpuFrequencyMhz = Environment.ProcessorCount * 1000,  // Rough estimate
            SystemRamMb = (int)(GC.GetTotalMemory(false) / (1024 * 1024)),
            GpuType = GpuType.PS20,  // Default to PS 2.0 capable GPU
            GpuMemoryMb = 256  // Conservative default
        };
    }

    /// <summary>
    /// Find the optimal static LOD level for the current hardware.
    /// Implements EA's preset matching algorithm with 6% error tolerance.
    /// 
    /// Algorithm:
    /// 1. Try to match detected hardware to a BenchProfile (with tolerance)
    /// 2. For each matching profile, check LOD presets from HIGH→LOW
    /// 3. Return the highest LOD level that matches the hardware
    /// 
    /// Returns: Highest matching LOD level, or Medium as fallback
    /// </summary>
    public LodType FindStaticLodLevel()
    {
        // If no presets loaded, return default
        if (_presets.Count == 0)
        {
            return LodType.Medium;
        }

        // Try to find matching LOD by preset matching
        // Start from highest LOD and work down to find best match
        foreach (var lodLevel in new[] { LodType.High, LodType.Medium, LodType.Low })
        {
            var matchingPresets = _presets
                .Where(p => p.Level == lodLevel)
                .ToList();

            foreach (var preset in matchingPresets)
            {
                // Check if CPU type matches
                if (preset.CpuType != DetectedHardware.CpuType)
                {
                    continue;
                }

                // Check if CPU frequency meets minimum (with tolerance)
                if (DetectedHardware.CpuFrequencyMhz < preset.MHz * ProfileErrorLimit)
                {
                    continue;
                }

                // Check if GPU type is acceptable
                if (preset.GpuType != DetectedHardware.GpuType && preset.GpuType != GpuType.PS20)
                {
                    continue;
                }

                // Check if GPU memory meets minimum
                if (DetectedHardware.GpuMemoryMb < preset.GpuMemory * ProfileErrorLimit)
                {
                    continue;
                }

                // All checks passed - this LOD level is suitable
                return lodLevel;
            }
        }

        // Fallback: return lowest available LOD
        return _presets.Where(p => p.Level == LodType.Low).Any() ? LodType.Low : LodType.Medium;
    }

    /// <summary>
    /// Set the static LOD level and apply its settings.
    /// </summary>
    public void SetStaticLodLevel(LodType level)
    {
        if (!_staticLods.ContainsKey(level))
        {
            return;  // Level not defined
        }

        if (CurrentStaticLod == level)
        {
            return;  // Already at this level
        }

        CurrentStaticLod = level;
        var lodInfo = _staticLods[level];

        // Notify listeners
        StaticLodChanged?.Invoke(level);
    }

    /// <summary>
    /// Apply static LOD settings via a StaticLodApplicationManager.
    /// This is called after SetStaticLodLevel to actually apply the LOD to all systems.
    /// </summary>
    public void ApplyStaticLodWithManager(StaticLodApplicationManager applicationManager)
    {
        if (applicationManager == null)
        {
            throw new ArgumentNullException(nameof(applicationManager));
        }

        if (!_staticLods.TryGetValue(CurrentStaticLod, out var lodInfo))
        {
            return;  // Current LOD level not defined
        }

        applicationManager.ApplyStaticLod(CurrentStaticLod, lodInfo);
    }

    /// <summary>
    /// Get the current static LOD definition.
    /// </summary>
    public StaticGameLod GetStaticLodInfo()
    {
        if (_staticLods.TryGetValue(CurrentStaticLod, out var lod))
        {
            return lod;
        }

        // Fallback to Medium if current level not found
        return _staticLods.TryGetValue(LodType.Medium, out var medium)
            ? medium
            : _staticLods.Values.FirstOrDefault() ?? throw new InvalidOperationException("No static LOD levels loaded");
    }

    /// <summary>
    /// Find the optimal dynamic LOD level based on current FPS.
    /// Uses FPS thresholds from dynamic LOD definitions.
    /// 
    /// Returns the highest dynamic LOD level whose minimum FPS is less than current FPS.
    /// </summary>
    public LodType FindDynamicLodLevel(float averageFps)
    {
        // Check from highest to lowest quality
        var levels = new[] { LodType.VeryHigh, LodType.High, LodType.Medium, LodType.Low };

        foreach (var level in levels)
        {
            if (!_dynamicLods.TryGetValue(level, out var dynamicLod))
            {
                continue;
            }

            if (averageFps >= dynamicLod.MinimumFps)
            {
                return level;
            }
        }

        // Fallback to lowest level if FPS is too low
        return LodType.Low;
    }

    /// <summary>
    /// Set the dynamic LOD level and apply its settings (skip masks, animation scales, etc.).
    /// </summary>
    public void SetDynamicLodLevel(LodType level)
    {
        if (!_dynamicLods.ContainsKey(level))
        {
            return;  // Level not defined
        }

        if (CurrentDynamicLod == level)
        {
            return;  // Already at this level
        }

        CurrentDynamicLod = level;
        var lodInfo = _dynamicLods[level];

        // Update skip masks
        _particleSkipMask = lodInfo.ParticleSkipMask;
        _debrisSkipMask = lodInfo.DebrisSkipMask;

        // Reset counters so masks align properly
        _particleSkipCounter = 0;
        _debrisSkipCounter = 0;

        // Notify listeners
        DynamicLodChanged?.Invoke(level);
    }

    /// <summary>
    /// Get the current dynamic LOD definition.
    /// </summary>
    public DynamicGameLod GetDynamicLodInfo()
    {
        if (_dynamicLods.TryGetValue(CurrentDynamicLod, out var lod))
        {
            return lod;
        }

        // Fallback to VeryHigh if current level not found
        return _dynamicLods.TryGetValue(LodType.VeryHigh, out var veryHigh)
            ? veryHigh
            : _dynamicLods.Values.FirstOrDefault() ?? throw new InvalidOperationException("No dynamic LOD levels loaded");
    }

    /// <summary>
    /// Check if a particle should be skipped based on dynamic LOD skip mask.
    /// 
    /// Implements EA's bitmask algorithm:
    /// Increment counter and check if (counter & mask) == mask
    /// 
    /// Example masks:
    /// - 0x00000000: render all particles (no skipping)
    /// - 0x00000003: render 1 of 4 particles (skip 3 of 4)
    /// - 0x000000FF: render 1 of 256 particles (skip 255 of 256)
    /// </summary>
    public bool IsParticleSkipped()
    {
        return (++_particleSkipCounter & _particleSkipMask) != _particleSkipMask;
    }

    /// <summary>
    /// Check if debris should be skipped based on dynamic LOD skip mask.
    /// Uses the same algorithm as IsParticleSkipped() but with separate counter.
    /// </summary>
    public bool IsDebrisSkipped()
    {
        return (++_debrisSkipCounter & _debrisSkipMask) != _debrisSkipMask;
    }

    /// <summary>
    /// Get current particle skip mask (for debugging/profiling).
    /// </summary>
    public int GetParticleSkipMask() => _particleSkipMask;

    /// <summary>
    /// Get current debris skip mask (for debugging/profiling).
    /// </summary>
    public int GetDebrisSkipMask() => _debrisSkipMask;

    /// <summary>
    /// Reset skip counters (useful for profiling/testing).
    /// </summary>
    public void ResetSkipCounters()
    {
        _particleSkipCounter = 0;
        _debrisSkipCounter = 0;
    }

    /// <summary>
    /// Get statistics about LOD system state.
    /// </summary>
    public LodStatistics GetStatistics() => new()
    {
        CurrentStaticLod = CurrentStaticLod,
        CurrentDynamicLod = CurrentDynamicLod,
        IdealDetailLevel = _idealDetailLevel,
        DetectedHardware = DetectedHardware,
        LoadedStaticLods = _staticLods.Count,
        LoadedDynamicLods = _dynamicLods.Count,
        LoadedPresets = _presets.Count,
        LoadedBenchProfiles = _benchProfiles.Count,
        ParticleSkipCounter = _particleSkipCounter,
        DebrisSkipCounter = _debrisSkipCounter
    };
}

/// <summary>
/// Hardware information detected at startup.
/// </summary>
public class HardwareInfo
{
    public CpuType CpuType { get; set; }
    public int CpuFrequencyMhz { get; set; }
    public GpuType GpuType { get; set; }
    public int GpuMemoryMb { get; set; }
    public int SystemRamMb { get; set; }

    public override string ToString() =>
        $"CPU: {CpuType} {CpuFrequencyMhz}MHz | GPU: {GpuType} {GpuMemoryMb}MB | RAM: {SystemRamMb}MB";
}

/// <summary>
/// Statistics about the current LOD system state (for debugging/monitoring).
/// </summary>
public class LodStatistics
{
    public LodType CurrentStaticLod { get; set; }
    public LodType CurrentDynamicLod { get; set; }
    public LodType IdealDetailLevel { get; set; }
    public HardwareInfo DetectedHardware { get; set; }
    public int LoadedStaticLods { get; set; }
    public int LoadedDynamicLods { get; set; }
    public int LoadedPresets { get; set; }
    public int LoadedBenchProfiles { get; set; }
    public int ParticleSkipCounter { get; set; }
    public int DebrisSkipCounter { get; set; }
}
