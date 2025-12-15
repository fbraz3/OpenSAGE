using System.Numerics;
using Xunit;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Mathematics;

namespace OpenSageGameTests.Graphics.ParticleSystems;

/// <summary>
/// Unit tests for streak particle trail system.
/// Verifies trail vertex recording, fade-out behavior, and ribbon rendering.
/// </summary>
public class StreakParticleTests
{
    private const int MaxStreakVertices = 50;

    [Fact]
    public void Particle_RecordStreakVertex_InitializesVertexArray()
    {
        // Arrange
        var particle = new Particle();
        particle.Position = new Vector3(0, 0, 0);

        // Act
        particle.RecordStreakVertex();

        // Assert
        Assert.NotNull(particle.StreakVertices);
        Assert.Equal(1, particle.StreakVertexCount);
        Assert.Equal(new Vector3(0, 0, 0), particle.StreakVertices[0]);
    }

    [Fact]
    public void Particle_RecordStreakVertex_ShiftsVerticesCorrectly()
    {
        // Arrange
        var particle = new Particle();
        var positions = new Vector3[]
        {
            new(0, 0, 0),
            new(1, 0, 0),
            new(2, 0, 0),
            new(3, 0, 0),
        };

        // Act & Assert - Record first three positions
        particle.Position = positions[0];
        particle.RecordStreakVertex();
        Assert.Equal(positions[0], particle.StreakVertices![0]);

        particle.Position = positions[1];
        particle.RecordStreakVertex();
        Assert.Equal(positions[1], particle.StreakVertices![0]); // New position at front
        Assert.Equal(positions[0], particle.StreakVertices![1]); // Old position shifted back
        Assert.Equal(2, particle.StreakVertexCount);

        particle.Position = positions[2];
        particle.RecordStreakVertex();
        Assert.Equal(positions[2], particle.StreakVertices![0]); // Newest
        Assert.Equal(positions[1], particle.StreakVertices![1]);
        Assert.Equal(positions[0], particle.StreakVertices![2]); // Oldest
        Assert.Equal(3, particle.StreakVertexCount);
    }

    [Fact]
    public void Particle_RecordStreakVertex_DoesNotExceedMaxVertices()
    {
        // Arrange
        var particle = new Particle();

        // Act - Record more vertices than max
        for (int i = 0; i < MaxStreakVertices + 10; i++)
        {
            particle.Position = new Vector3(i, 0, 0);
            particle.RecordStreakVertex();
        }

        // Assert - Should cap at MaxStreakVertices
        Assert.Equal(MaxStreakVertices, particle.StreakVertexCount);
        Assert.Equal(MaxStreakVertices, particle.StreakVertices!.Length);

        // Most recent should be at index 0
        Assert.Equal(new Vector3(MaxStreakVertices + 9, 0, 0), particle.StreakVertices[0]);

        // Oldest (still in buffer) should be at last index
        Assert.Equal(new Vector3(10, 0, 0), particle.StreakVertices[MaxStreakVertices - 1]);
    }

    [Fact]
    public void Particle_RecordStreakVertex_TracksVertexCountCorrectly()
    {
        // Arrange
        var particle = new Particle();
        Assert.Equal(0, particle.StreakVertexCount);

        // Act & Assert - Progressive vertex count
        for (int i = 1; i <= 5; i++)
        {
            particle.Position = new Vector3(i, 0, 0);
            particle.RecordStreakVertex();
            Assert.Equal(i, particle.StreakVertexCount);
        }
    }

    [Fact]
    public void Particle_StreakVertices_CapsAtMaxAfterCountReached()
    {
        // Arrange
        var particle = new Particle();

        // Fill to max
        for (int i = 0; i < MaxStreakVertices; i++)
        {
            particle.Position = new Vector3(i, 0, 0);
            particle.RecordStreakVertex();
        }

        Assert.Equal(MaxStreakVertices, particle.StreakVertexCount);

        // Act - Add more vertices
        for (int i = MaxStreakVertices; i < MaxStreakVertices + 5; i++)
        {
            particle.Position = new Vector3(i, 0, 0);
            particle.RecordStreakVertex();
        }

        // Assert - Count should stay at max
        Assert.Equal(MaxStreakVertices, particle.StreakVertexCount);
    }

    [Fact]
    public void Particle_StreakVertices_PreservesTrajectory()
    {
        // Arrange - Simulate a particle moving along a line
        var particle = new Particle();
        var trajectory = new Vector3[10];
        for (int i = 0; i < 10; i++)
        {
            trajectory[i] = new Vector3(i * 1.0f, i * 0.5f, i * -0.1f);
        }

        // Act - Record trajectory
        foreach (var pos in trajectory)
        {
            particle.Position = pos;
            particle.RecordStreakVertex();
        }

        // Assert - Vertices should be in reverse order (newest first)
        Assert.Equal(10, particle.StreakVertexCount);
        for (int i = 0; i < 10; i++)
        {
            var expectedPos = trajectory[9 - i]; // Reverse order
            Assert.Equal(expectedPos, particle.StreakVertices![i]);
        }
    }

    [Fact]
    public void Particle_StreakVertices_WorksWithDeadParticles()
    {
        // Arrange
        var particle = new Particle { Dead = false };

        // Act - Record vertices
        particle.Position = new Vector3(0, 0, 0);
        particle.RecordStreakVertex();

        particle.Position = new Vector3(1, 0, 0);
        particle.RecordStreakVertex();

        particle.Dead = true; // Mark as dead

        // Assert - Vertices should still be accessible for rendering last trail
        Assert.Equal(2, particle.StreakVertexCount);
        Assert.Equal(new Vector3(1, 0, 0), particle.StreakVertices![0]);
        Assert.Equal(new Vector3(0, 0, 0), particle.StreakVertices![1]);
    }

    [Fact]
    public void Particle_StreakVertices_OtherPropertiesUnaffected()
    {
        // Arrange
        var particle = new Particle
        {
            Position = new Vector3(1, 2, 3),
            Size = 5.0f,
            Color = new Vector3(1, 0, 0),
            Alpha = 0.8f,
            AngleZ = 1.57f,
            Velocity = new Vector3(0.1f, 0.2f, 0.3f),
        };

        // Act - Record vertices multiple times
        for (int i = 0; i < 5; i++)
        {
            particle.RecordStreakVertex();
        }

        // Assert - Other properties should remain unchanged
        Assert.Equal(new Vector3(1, 2, 3), particle.Position);
        Assert.Equal(5.0f, particle.Size);
        Assert.Equal(new Vector3(1, 0, 0), particle.Color);
        Assert.Equal(0.8f, particle.Alpha);
        Assert.Equal(1.57f, particle.AngleZ);
        Assert.Equal(new Vector3(0.1f, 0.2f, 0.3f), particle.Velocity);
    }

    [Fact]
    public void Particle_StreakVertices_InitializationInConstructor()
    {
        // Arrange & Act
        var particle = new Particle();

        // Assert
        Assert.Null(particle.StreakVertices);
        Assert.Equal(0, particle.StreakVertexCount);
    }

    [Fact]
    public void Particle_StreakVertices_SupportMultipleParticles()
    {
        // Arrange
        var particle1 = new Particle();
        var particle2 = new Particle();

        // Act - Create different trails for each particle
        for (int i = 0; i < 5; i++)
        {
            particle1.Position = new Vector3(i, 0, 0);
            particle1.RecordStreakVertex();

            particle2.Position = new Vector3(0, i, 0);
            particle2.RecordStreakVertex();
        }

        // Assert - Each particle has independent trail
        Assert.Equal(5, particle1.StreakVertexCount);
        Assert.Equal(5, particle2.StreakVertexCount);

        Assert.Equal(new Vector3(4, 0, 0), particle1.StreakVertices![0]); // particle1 front
        Assert.Equal(new Vector3(0, 4, 0), particle2.StreakVertices![0]); // particle2 front

        // Verify no cross-contamination
        for (int i = 0; i < 5; i++)
        {
            var v1 = particle1.StreakVertices![i];
            var v2 = particle2.StreakVertices![i];

            // particle1 should have X values, Y and Z should be 0
            Assert.Equal(4 - i, v1.X); // Newest is at index 0, oldest at index 4
            Assert.Equal(0, v1.Y);
            Assert.Equal(0, v1.Z);

            // particle2 should have Y values, X and Z should be 0
            Assert.Equal(0, v2.X);
            Assert.Equal(4 - i, v2.Y); // Newest is at index 0, oldest at index 4
            Assert.Equal(0, v2.Z);
        }
    }

    [Fact]
    public void Particle_StreakVertices_HandlesRapidMovement()
    {
        // Arrange
        var particle = new Particle();

        // Act - Simulate rapid zigzag movement
        for (int i = 0; i < 10; i++)
        {
            particle.Position = new Vector3(
                i % 2 == 0 ? i : i * -1, // zigzag
                i * 1.0f,
                i * 0.1f
            );
            particle.RecordStreakVertex();
        }

        // Assert - Should capture all position changes
        Assert.Equal(10, particle.StreakVertexCount);

        // First vertex should be most recent (i=9: -9, 9, 0.9)
        Assert.Equal(-9, particle.StreakVertices![0].X);
        Assert.Equal(9, particle.StreakVertices![0].Y);
        Assert.True(System.Math.Abs(particle.StreakVertices![0].Z - 0.9f) < 0.01f); // Float precision

        // Last vertex should be oldest (i=0: 0, 0, 0)
        Assert.Equal(new Vector3(0, 0, 0), particle.StreakVertices![9]);
    }
}
