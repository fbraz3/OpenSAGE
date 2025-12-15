using System;
using System.Numerics;
using Xunit;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Mathematics;

namespace OpenSage.Tests;

/// <summary>
/// Unit tests for all particle emission volume types.
/// These tests verify that emission volumes generate valid rays
/// and that particles emit in correct spatial distributions.
/// </summary>
public class ParticleEmissionVolumeTests
{
    private const int SampleCount = 100;
    private const float Epsilon = 0.001f;

    [Fact]
    public void TestSphereVolumeGeneratesValidRays()
    {
        var volume = new FXParticleEmissionVolumeSphere { Radius = 10f, IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.False(float.IsNaN(ray.Position.X) || float.IsNaN(ray.Position.Y) || float.IsNaN(ray.Position.Z));
            Assert.False(float.IsNaN(ray.Direction.X) || float.IsNaN(ray.Direction.Y) || float.IsNaN(ray.Direction.Z));
        }
    }

    [Fact]
    public void TestSphereVolumeEmitsWithinRadius()
    {
        var volume = new FXParticleEmissionVolumeSphere { Radius = 10f, IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();
            var distance = ray.Position.Length();

            Assert.True(distance <= 10f + Epsilon, $"Particle should emit within sphere radius, got {distance}");
        }
    }

    [Fact]
    public void TestSphereVolumeHollowEmitsAtRadius()
    {
        var volume = new FXParticleEmissionVolumeSphere { Radius = 10f, IsHollow = true };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();
            var distance = ray.Position.Length();

            Assert.True(Math.Abs(distance - 10f) <= Epsilon, $"Hollow sphere should emit at exact radius, got {distance}");
        }
    }

    [Fact]
    public void TestBoxVolumeGeneratesValidRays()
    {
        var volume = new FXParticleEmissionVolumeBox { HalfSize = new Vector3(5f, 5f, 5f), IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.False(float.IsNaN(ray.Position.X) || float.IsNaN(ray.Position.Y) || float.IsNaN(ray.Position.Z));
        }
    }

    [Fact]
    public void TestBoxVolumeEmitsWithinBounds()
    {
        var halfSize = new Vector3(5f, 3f, 4f);
        var volume = new FXParticleEmissionVolumeBox { HalfSize = halfSize, IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.True(Math.Abs(ray.Position.X) <= halfSize.X + Epsilon);
            Assert.True(Math.Abs(ray.Position.Y) <= halfSize.Y + Epsilon);
            Assert.True(ray.Position.Z >= 0 - Epsilon);
            Assert.True(ray.Position.Z <= halfSize.Z * 2 + Epsilon);
        }
    }

    [Fact]
    public void TestCylinderVolumeGeneratesValidRays()
    {
        var volume = new FXParticleEmissionVolumeCylinder { Radius = 5f, Length = 10f, IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.False(float.IsNaN(ray.Position.X) || float.IsNaN(ray.Position.Y) || float.IsNaN(ray.Position.Z));
        }
    }

    [Fact]
    public void TestCylinderVolumeEmitsWithinBounds()
    {
        var radius = 5f;
        var length = 10f;
        var volume = new FXParticleEmissionVolumeCylinder { Radius = radius, Length = length, IsHollow = false };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();
            var radialDistance = MathF.Sqrt(ray.Position.X * ray.Position.X + ray.Position.Y * ray.Position.Y);

            Assert.True(radialDistance <= radius + Epsilon);
            Assert.True(ray.Position.Z >= 0 - Epsilon);
            Assert.True(ray.Position.Z <= length + Epsilon);
        }
    }

    [Fact]
    public void TestLineVolumeGeneratesValidRays()
    {
        var start = new Vector3(0f, 0f, 0f);
        var end = new Vector3(10f, 10f, 10f);
        var volume = new FXParticleEmissionVolumeLine { StartPoint = start, EndPoint = end };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.False(float.IsNaN(ray.Position.X) || float.IsNaN(ray.Position.Y) || float.IsNaN(ray.Position.Z));
        }
    }

    [Fact]
    public void TestLineVolumeEmitsAlongLine()
    {
        var start = new Vector3(0f, 0f, 0f);
        var end = new Vector3(10f, 0f, 0f);
        var volume = new FXParticleEmissionVolumeLine { StartPoint = start, EndPoint = end };

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            // Particles should emit along the line
            Assert.True(ray.Position.X >= start.X - Epsilon);
            Assert.True(ray.Position.X <= end.X + Epsilon);
            Assert.True(Math.Abs(ray.Position.Y) <= Epsilon);
            Assert.True(Math.Abs(ray.Position.Z) <= Epsilon);
        }
    }

    [Fact]
    public void TestPointVolumeEmitsFromOrigin()
    {
        var volume = new FXParticleEmissionVolumePoint();

        for (int i = 0; i < SampleCount; i++)
        {
            var ray = volume.GetRay();

            Assert.True(Math.Abs(ray.Position.X) <= Epsilon);
            Assert.True(Math.Abs(ray.Position.Y) <= Epsilon);
            Assert.True(Math.Abs(ray.Position.Z) <= Epsilon);
        }
    }

    [Fact]
    public void TestTerrainFireVolumeGeneratesValidRays()
    {
        // NOTE: TerrainFire properties are private and can only be set through INI parsing
        // This test just verifies that the type exists and can be instantiated
        var volume = new FXParticleEmissionVolumeTerrainFire();

        // GetRay() should not throw even with default values
        _ = volume.GetRay();
    }

    [Fact]
    public void TestLightningVolumeGeneratesValidRays()
    {
        // NOTE: Lightning properties are private and can only be set through INI parsing
        // This test just verifies that the type exists and can be instantiated
        var volume = new FXParticleEmissionVolumeLightning();

        // GetRay() should not throw even with default values
        _ = volume.GetRay();
    }

    [Fact]
    public void TestAllVolumesHaveValidDirections()
    {
        var volumes = new FXParticleEmissionVolumeBase[]
        {
            new FXParticleEmissionVolumeSphere { Radius = 10f },
            new FXParticleEmissionVolumeBox { HalfSize = new Vector3(5, 5, 5) },
            new FXParticleEmissionVolumeCylinder { Radius = 5f, Length = 10f },
            new FXParticleEmissionVolumeLine { StartPoint = Vector3.Zero, EndPoint = new Vector3(10, 0, 0) },
            new FXParticleEmissionVolumePoint(),
            new FXParticleEmissionVolumeTerrainFire(),
            new FXParticleEmissionVolumeLightning()
        };

        foreach (var volume in volumes)
        {
            for (int i = 0; i < 10; i++)
            {
                var ray = volume.GetRay();

                // Direction should either be zero (for Point volume) or normalized
                var directionLength = ray.Direction.Length();
                Assert.True(
                    directionLength < Epsilon || Math.Abs(directionLength - 1.0f) < Epsilon,
                    $"Direction should be normalized or zero, got length: {directionLength} for {volume.GetType().Name}");
            }
        }
    }
}

