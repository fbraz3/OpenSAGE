using System;
using System.Numerics;

namespace OpenSage.Terrain;

/// <summary>
/// Manages wave animation for standing wave areas.
/// Simulates wave expansion, movement, and fade-out over time.
/// </summary>
public sealed class WaveSimulation : IDisposable
{
    /// <summary>
    /// Represents an active wave instance with its current state.
    /// </summary>
    public struct ActiveWave
    {
        /// <summary>
        /// Current width of the wave (expands over time).
        /// </summary>
        public float CurrentWidth { get; set; }

        /// <summary>
        /// Current height of the wave (expands over time).
        /// </summary>
        public float CurrentHeight { get; set; }

        /// <summary>
        /// Center position of the wave.
        /// </summary>
        public Vector2 Position { get; set; }

        /// <summary>
        /// Direction of wave movement.
        /// </summary>
        public Vector2 Direction { get; set; }

        /// <summary>
        /// Current velocity of the wave (decreases as it fades).
        /// </summary>
        public float Velocity { get; set; }

        /// <summary>
        /// Alpha/opacity of the wave (0-1).
        /// </summary>
        public float Alpha { get; set; }

        /// <summary>
        /// Elapsed time since wave was created (seconds).
        /// </summary>
        public float ElapsedTime { get; set; }

        /// <summary>
        /// Total time before wave fades out completely.
        /// </summary>
        public float TimeToFade { get; set; }

        /// <summary>
        /// Whether this wave is active (time elapsed < TimeToFade).
        /// </summary>
        public bool IsActive => ElapsedTime < TimeToFade;
    }

    private const int MaxActiveWaves = 256;
    private ActiveWave[] _activeWaves = new ActiveWave[MaxActiveWaves];
    private int _activeWaveCount = 0;

    /// <summary>
    /// Gets the number of currently active waves.
    /// </summary>
    public int ActiveWaveCount => _activeWaveCount;

    /// <summary>
    /// Gets all currently active waves.
    /// </summary>
    public ReadOnlySpan<ActiveWave> ActiveWaves => _activeWaves.AsSpan(0, _activeWaveCount);

    /// <summary>
    /// Creates and spawns a new wave at the specified origin.
    /// </summary>
    /// <param name="origin">Center point where wave originates</param>
    /// <param name="direction">Direction the wave will travel</param>
    /// <param name="initialVelocity">Initial velocity of wave expansion (units/second)</param>
    /// <param name="initialWidth">Initial width of wave</param>
    /// <param name="initialHeight">Initial height of wave</param>
    /// <param name="finalWidth">Target width when fully expanded</param>
    /// <param name="finalHeight">Target height when fully expanded</param>
    /// <param name="timeToFade">How long wave persists (seconds)</param>
    public void SpawnWave(
        Vector2 origin,
        Vector2 direction,
        float initialVelocity,
        float initialWidth,
        float initialHeight,
        float finalWidth,
        float finalHeight,
        float timeToFade)
    {
        if (_activeWaveCount >= MaxActiveWaves)
        {
            // Remove oldest wave to make room
            for (int i = 0; i < _activeWaveCount - 1; i++)
            {
                _activeWaves[i] = _activeWaves[i + 1];
            }
            _activeWaveCount--;
        }

        var normalizedDirection = Vector2.Normalize(direction);

        _activeWaves[_activeWaveCount++] = new ActiveWave
        {
            Position = origin,
            Direction = normalizedDirection,
            CurrentWidth = initialWidth,
            CurrentHeight = initialHeight,
            Velocity = initialVelocity,
            Alpha = 1.0f,
            ElapsedTime = 0.0f,
            TimeToFade = timeToFade
        };
    }

    /// <summary>
    /// Updates all active waves based on elapsed time.
    /// </summary>
    /// <param name="deltaTime">Time elapsed since last update (seconds)</param>
    public void Update(float deltaTime)
    {
        int writeIndex = 0;

        for (int i = 0; i < _activeWaveCount; i++)
        {
            var wave = _activeWaves[i];
            wave.ElapsedTime += deltaTime;

            if (!wave.IsActive)
            {
                // Skip inactive waves
                continue;
            }

            // Calculate fade progress (0 to 1)
            float fadeProgress = wave.ElapsedTime / wave.TimeToFade;

            // Update alpha (linear fade-out)
            wave.Alpha = 1.0f - fadeProgress;

            // Update position (moves in direction with velocity that decreases over time)
            float velocityMultiplier = 1.0f - (fadeProgress * 0.5f); // Velocity decreases to 50% of initial
            wave.Position += wave.Direction * wave.Velocity * velocityMultiplier * deltaTime;

            // Update size (linear interpolation from initial to final size)
            // This assumes initial size is already stored, so we expand to final size
            // For now, we'll use a simplified expansion based on time
            float expansionFactor = 1.0f + (fadeProgress * 2.0f); // Doubles in size over lifetime
            wave.CurrentWidth *= expansionFactor;
            wave.CurrentHeight *= expansionFactor;

            _activeWaves[writeIndex++] = wave;
        }

        // Update count to remove inactive waves
        _activeWaveCount = writeIndex;
    }

    /// <summary>
    /// Clears all active waves.
    /// </summary>
    public void Clear()
    {
        _activeWaveCount = 0;
    }

    /// <summary>
    /// Gets all waves as a span for efficient iteration.
    /// </summary>
    public ReadOnlySpan<ActiveWave> GetActiveWaves()
    {
        return _activeWaves.AsSpan(0, _activeWaveCount);
    }

    /// <summary>
    /// Releases resources used by the wave simulation.
    /// </summary>
    public void Dispose()
    {
        Clear();
    }
}
