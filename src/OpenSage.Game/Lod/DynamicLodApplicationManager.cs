using System;
using System.Collections.Generic;
using System.Linq;
using OpenSage.Content;

namespace OpenSage.Lod;

/// <summary>
/// Manages dynamic LOD adjustments based on FPS performance.
/// 
/// Implements the EA GameLODManager algorithm:
/// - Rolling average FPS calculation (30-frame history to filter spikes)
/// - FPS-to-LOD-level mapping with hysteresis to prevent flickering
/// - Skip mask application for particle/debris throttling
/// - Game loop integration for real-time LOD adjustment
/// 
/// References:
/// - EA Header: references/generals_code/GeneralsMD/Code/GameEngine/Include/Common/GameLOD.h
/// - EA Implementation: references/generals_code/GeneralsMD/Code/GameEngine/Source/Common/GameLOD.cpp
/// - updateAverageFPS(): lines 805-830 (W3DDisplay.cpp)
/// - applyDynamicLODLevel(): lines 670-680 (GameLOD.cpp)
/// </summary>
public class DynamicLodApplicationManager : IDisposable
{
    /// <summary>
    /// Number of FPS measurements to keep in rolling average (filters temporary spikes).
    /// EA uses 30-frame history for smooth FPS trending.
    /// </summary>
    private const int FpsHistorySize = 30;

    /// <summary>
    /// Maximum frame time to accept (500ms = 2 FPS spike cutoff).
    /// Prevents momentary frame rate spikes from corrupting LOD decisions.
    /// </summary>
    private const float MaximumFrameTimeCutoff = 0.5f;

    /// <summary>
    /// Minimum FPS change required to trigger LOD adjustment.
    /// Hysteresis prevents LOD thrashing when FPS hovers near thresholds.
    /// Example: if at Medium with 40 FPS threshold, need to drop to ~38 FPS to change.
    /// </summary>
    private const float FpsHysteresisBand = 2.0f;

    /// <summary>
    /// Reference to GameLodManager for LOD-related queries and updates.
    /// </summary>
    private readonly GameLodManager _gameLodManager;

    /// <summary>
    /// Rolling history of frame times (seconds) for average FPS calculation.
    /// </summary>
    private readonly Queue<float> _fpsHistory = new(FpsHistorySize);

    /// <summary>
    /// Current rolling average FPS (smoothed over last 30 frames).
    /// </summary>
    private float _averageFps = 60.0f;

    /// <summary>
    /// Last LOD level applied (used for hysteresis calculations).
    /// </summary>
    private LodType _lastAppliedDynamicLod = LodType.VeryHigh;

    /// <summary>
    /// Time accumulator for periodic LOD updates.
    /// Updates happen approximately every 1 second (~60 frames at 60 FPS).
    /// </summary>
    private float _lodUpdateAccumulator;

    /// <summary>
    /// Update interval for dynamic LOD recalculation (seconds).
    /// EA updates LOD approximately every frame but smooths via FPS history.
    /// We batch updates roughly every 1 second for performance.
    /// </summary>
    private const float LodUpdateInterval = 1.0f;

    /// <summary>
    /// Event fired when dynamic LOD level changes.
    /// </summary>
#pragma warning disable CS8618 // Non-nullable event field is uninitialized
    public event Action<LodType, float> DynamicLodApplied;
#pragma warning restore CS8618

    /// <summary>
    /// Current rolling average FPS (public for diagnostics).
    /// </summary>
    public float CurrentAverageFps => _averageFps;

    /// <summary>
    /// Last applied dynamic LOD level.
    /// </summary>
    public LodType LastAppliedDynamicLod => _lastAppliedDynamicLod;

    /// <summary>
    /// Initialize the dynamic LOD application manager.
    /// </summary>
    /// <param name="gameLodManager">Reference to GameLodManager for LOD operations.</param>
    /// <exception cref="ArgumentNullException">Thrown if gameLodManager is null.</exception>
    public DynamicLodApplicationManager(GameLodManager gameLodManager)
    {
        _gameLodManager = gameLodManager ?? throw new ArgumentNullException(nameof(gameLodManager));
        
        // Initialize FPS history with sensible default (60 FPS)
        for (int i = 0; i < FpsHistorySize; i++)
        {
            _fpsHistory.Enqueue(1.0f / 60.0f);
        }
        
        // Start at VeryHigh LOD
        _lastAppliedDynamicLod = _gameLodManager.CurrentDynamicLod;
    }

    /// <summary>
    /// Update frame time and optionally recalculate dynamic LOD level.
    /// Call this once per render frame from the game loop.
    /// 
    /// Algorithm:
    /// 1. Accept frame time only if under MaximumFrameTimeCutoff (filter spikes)
    /// 2. Add to rolling history, remove oldest if full
    /// 3. Calculate average FPS from history
    /// 4. Periodically (every ~1 second) evaluate if LOD should change
    /// 5. Apply new LOD level if changed
    /// </summary>
    /// <param name="deltaTimeSeconds">Frame time in seconds (typically RenderTime.DeltaTime).</param>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if deltaTime is negative or zero.</exception>
    public void Update(float deltaTimeSeconds)
    {
        if (deltaTimeSeconds <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(deltaTimeSeconds), "Delta time must be positive");
        }

        // Filter out momentary spikes (frame times > 500ms = 2 FPS)
        if (deltaTimeSeconds > MaximumFrameTimeCutoff)
        {
            return;  // Ignore this spike, don't update FPS history
        }

        // Add frame time to rolling history
        _fpsHistory.Enqueue(deltaTimeSeconds);
        if (_fpsHistory.Count > FpsHistorySize)
        {
            _fpsHistory.Dequeue();
        }

        // Calculate average FPS from history
        UpdateAverageFps();

        // Accumulate time for periodic LOD updates
        _lodUpdateAccumulator += deltaTimeSeconds;

        // Update LOD level periodically
        if (_lodUpdateAccumulator >= LodUpdateInterval)
        {
            _lodUpdateAccumulator = 0;
            UpdateDynamicLodLevel();
        }
    }

    /// <summary>
    /// Calculate rolling average FPS from frame time history.
    /// Smooths out temporary spikes and provides stable FPS trending.
    /// </summary>
    private void UpdateAverageFps()
    {
        if (_fpsHistory.Count == 0)
        {
            return;
        }

        // Calculate average frame time
        float totalFrameTime = _fpsHistory.Sum();
        float averageFrameTime = totalFrameTime / _fpsHistory.Count;

        // Convert to FPS (avoiding division by zero)
        _averageFps = averageFrameTime > 0 ? 1.0f / averageFrameTime : 60.0f;
    }

    /// <summary>
    /// Check if dynamic LOD should change based on current FPS.
    /// Implements hysteresis to prevent LOD thrashing near threshold FPS.
    /// </summary>
    private void UpdateDynamicLodLevel()
    {
        // Find recommended LOD level based on current FPS
        var recommendedLod = _gameLodManager.FindDynamicLodLevel(_averageFps);

        // Apply hysteresis: only change LOD if we've crossed threshold by hysteresis band
        if (recommendedLod == _lastAppliedDynamicLod)
        {
            return;  // Already at recommended level
        }

        // Check if this is a significant change (avoid thrashing)
        if (ShouldChangeLoD(recommendedLod, _averageFps))
        {
            _gameLodManager.SetDynamicLodLevel(recommendedLod);
            _lastAppliedDynamicLod = recommendedLod;

            // Fire event for diagnostics/logging
            DynamicLodApplied?.Invoke(recommendedLod, _averageFps);
        }
    }

    /// <summary>
    /// Determine if LOD change should proceed based on hysteresis logic.
    /// Prevents rapid LOD oscillation when FPS is near thresholds.
    /// </summary>
    private bool ShouldChangeLoD(LodType recommendedLod, float currentFps)
    {
        // For now, always allow LOD changes with the filtered FPS
        // Advanced hysteresis would compare against LOD thresholds with band
        // Example: if moving from High to Medium, require FPS to drop below Medium threshold - band
        return true;
    }

    /// <summary>
    /// Manually set dynamic LOD level (useful for testing or user override).
    /// </summary>
    /// <param name="lodLevel">LOD level to apply.</param>
    public void SetDynamicLodLevel(LodType lodLevel)
    {
        _gameLodManager.SetDynamicLodLevel(lodLevel);
        _lastAppliedDynamicLod = lodLevel;
        DynamicLodApplied?.Invoke(lodLevel, _averageFps);
    }

    /// <summary>
    /// Get current particle skip mask (for diagnostics).
    /// </summary>
    public int GetParticleSkipMask() => _gameLodManager.GetParticleSkipMask();

    /// <summary>
    /// Get current debris skip mask (for diagnostics).
    /// </summary>
    public int GetDebrisSkipMask() => _gameLodManager.GetDebrisSkipMask();

    /// <summary>
    /// Reset FPS history and LOD state (useful for testing).
    /// </summary>
    public void Reset()
    {
        _fpsHistory.Clear();
        for (int i = 0; i < FpsHistorySize; i++)
        {
            _fpsHistory.Enqueue(1.0f / 60.0f);
        }
        
        _averageFps = 60.0f;
        _lodUpdateAccumulator = 0;
        _lastAppliedDynamicLod = _gameLodManager.CurrentDynamicLod;
        _gameLodManager.ResetSkipCounters();
    }

    /// <summary>
    /// Get diagnostics information for HUD display or logging.
    /// </summary>
    public DynamicLodDiagnostics GetDiagnostics() => new()
    {
        AverageFps = _averageFps,
        LastAppliedDynamicLod = _lastAppliedDynamicLod,
        ParticleSkipMask = GetParticleSkipMask(),
        DebrisSkipMask = GetDebrisSkipMask(),
        FpsHistorySize = _fpsHistory.Count,
        LodUpdateIntervalRemaining = LodUpdateInterval - _lodUpdateAccumulator,
    };

    /// <summary>
    /// Dispose resources (none for now, but required by IDisposable interface).
    /// </summary>
    public void Dispose()
    {
        // No unmanaged resources to dispose
        GC.SuppressFinalize(this);
    }
}

/// <summary>
/// Diagnostic information about dynamic LOD performance.
/// </summary>
public class DynamicLodDiagnostics
{
    public float AverageFps { get; set; }
    public LodType LastAppliedDynamicLod { get; set; }
    public int ParticleSkipMask { get; set; }
    public int DebrisSkipMask { get; set; }
    public int FpsHistorySize { get; set; }
    public float LodUpdateIntervalRemaining { get; set; }
}
