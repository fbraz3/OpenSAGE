using System;
using System.Numerics;
using OpenSage.Terrain;
using Xunit;

namespace OpenSage.Tests.Terrain;

/// <summary>
/// Tests for WaveSimulation wave animation system.
/// </summary>
public class WaveSimulationTests
{
    [Fact]
    public void SpawnWave_CreatesActiveWave()
    {
        // Arrange
        var sim = new WaveSimulation();
        var origin = new Vector2(10, 20);
        var direction = new Vector2(1, 0);

        // Act
        sim.SpawnWave(origin, direction, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);

        // Assert
        Assert.Equal(1, sim.ActiveWaveCount);
        var waves = sim.GetActiveWaves();
        Assert.Equal(origin, waves[0].Position);
        Assert.Equal(1.0f, waves[0].Alpha);
    }

    [Fact]
    public void SpawnMultipleWaves_AllTracked()
    {
        // Arrange
        var sim = new WaveSimulation();

        // Act
        for (int i = 0; i < 5; i++)
        {
            sim.SpawnWave(
                new Vector2(i, i),
                new Vector2(1, 0),
                5.0f,
                1.0f,
                1.0f,
                10.0f,
                10.0f,
                2.0f);
        }

        // Assert
        Assert.Equal(5, sim.ActiveWaveCount);
    }

    [Fact]
    public void Update_IncreasesElapsedTime()
    {
        // Arrange
        var sim = new WaveSimulation();
        sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);

        // Act
        sim.Update(0.5f);

        // Assert
        var waves = sim.GetActiveWaves();
        Assert.Equal(1, waves.Length);
        Assert.Equal(0.5f, waves[0].ElapsedTime, precision: 2);
    }

    [Fact]
    public void Update_DecreasesAlpha()
    {
        // Arrange
        var sim = new WaveSimulation();
        sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);

        // Act
        sim.Update(1.0f); // Half of 2-second fade time

        // Assert
        var waves = sim.GetActiveWaves();
        Assert.Equal(1, waves.Length);
        Assert.True(waves[0].Alpha < 1.0f, "Alpha should decrease with time");
        Assert.True(waves[0].Alpha > 0.0f, "Alpha should be positive");
    }

    [Fact]
    public void Update_RemovesExpiredWaves()
    {
        // Arrange
        var sim = new WaveSimulation();
        sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 1.0f);

        // Act
        sim.Update(1.5f); // Longer than fade time

        // Assert
        Assert.Equal(0, sim.ActiveWaveCount);
    }

    [Fact]
    public void Update_MovesWaveInDirection()
    {
        // Arrange
        var sim = new WaveSimulation();
        var direction = Vector2.Normalize(new Vector2(1, 1));
        var initialPos = new Vector2(0, 0);
        sim.SpawnWave(initialPos, direction, 10.0f, 1.0f, 1.0f, 10.0f, 10.0f, 5.0f);

        // Act
        sim.Update(0.1f); // 100ms

        // Assert
        var waves = sim.GetActiveWaves();
        Assert.Equal(1, waves.Length);
        var wave = waves[0];

        // Position should have moved in direction of travel
        Assert.True(wave.Position.X > initialPos.X || wave.Position.Y > initialPos.Y,
            "Wave should move from origin");
    }

    [Fact]
    public void Update_NormalizesDirection()
    {
        // Arrange
        var sim = new WaveSimulation();
        var unnormalizedDir = new Vector2(5, 5); // Not normalized

        // Act
        sim.SpawnWave(Vector2.Zero, unnormalizedDir, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);

        // Assert
        var waves = sim.GetActiveWaves();
        var dir = waves[0].Direction;

        // Direction should be normalized (magnitude = 1)
        var magnitude = (float)Math.Sqrt(dir.X * dir.X + dir.Y * dir.Y);
        Assert.Equal(1.0f, magnitude, precision: 3);
    }

    [Fact]
    public void Clear_RemovesAllWaves()
    {
        // Arrange
        var sim = new WaveSimulation();
        for (int i = 0; i < 3; i++)
        {
            sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);
        }

        // Act
        sim.Clear();

        // Assert
        Assert.Equal(0, sim.ActiveWaveCount);
    }

    [Fact]
    public void SpawnWave_MaxWavesHandled()
    {
        // Arrange
        var sim = new WaveSimulation();

        // Act - Spawn more waves than max capacity
        for (int i = 0; i < 300; i++)
        {
            sim.SpawnWave(
                new Vector2(i, i),
                Vector2.UnitX,
                5.0f,
                1.0f,
                1.0f,
                10.0f,
                10.0f,
                2.0f);
        }

        // Assert - Should not exceed max and not crash
        Assert.True(sim.ActiveWaveCount <= 256, "Should not exceed max waves");
    }

    [Fact]
    public void Update_MultipleFrames_ConsistentBehavior()
    {
        // Arrange
        var sim = new WaveSimulation();
        sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 10.0f, 1.0f, 1.0f, 10.0f, 10.0f, 1.0f);

        // Act - Update in small time steps
        for (int i = 0; i < 10; i++)
        {
            sim.Update(0.1f);
        }

        // Assert
        var waves = sim.GetActiveWaves();
        Assert.Equal(0, waves.Length); // Should be expired after 1 second
    }

    [Fact]
    public void ActiveWaves_ReturnsCorrectSpan()
    {
        // Arrange
        var sim = new WaveSimulation();
        sim.SpawnWave(Vector2.Zero, Vector2.UnitX, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);
        sim.SpawnWave(Vector2.One, Vector2.UnitY, 5.0f, 1.0f, 1.0f, 10.0f, 10.0f, 2.0f);

        // Act
        var waves = sim.ActiveWaves;

        // Assert
        Assert.Equal(2, waves.Length);
    }
}
