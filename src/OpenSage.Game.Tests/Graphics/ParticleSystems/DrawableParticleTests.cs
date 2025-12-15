using System.Numerics;
using Xunit;
using OpenSage.Graphics.ParticleSystems;
using OpenSage.Mathematics;
using OpenSageGameTests.Graphics.ParticleSystems;

namespace OpenSageGameTests.Graphics.ParticleSystems;

/// <summary>
/// Unit tests for drawable particle attachment and transformation.
/// </summary>
public class DrawableParticleTests
{
    [Fact]
    public void Particle_AttachDrawable_SetsDrawableReference()
    {
        // Arrange
        var particle = new Particle();
        var fakeDrawable = new FakeDrawable(42);

        // Act
        particle.AttachDrawable(fakeDrawable);

        // Assert
        Assert.NotNull(particle.AttachedDrawable);
        Assert.Same(fakeDrawable, particle.AttachedDrawable);
        Assert.Equal(42u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_AttachDrawable_WithNull_ClearsAttachment()
    {
        // Arrange
        var particle = new Particle();
        var fakeDrawable = new FakeDrawable(42);
        particle.AttachDrawable(fakeDrawable);

        // Act
        particle.AttachDrawable(null!);

        // Assert
        Assert.Null(particle.AttachedDrawable);
        Assert.Equal(0u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_DetachDrawable_RemovesDrawableReference()
    {
        // Arrange
        var particle = new Particle();
        var fakeDrawable = new FakeDrawable(99);
        particle.AttachDrawable(fakeDrawable);

        // Act
        particle.DetachDrawable();

        // Assert
        Assert.Null(particle.AttachedDrawable);
        Assert.Equal(0u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_AttachDrawable_PreservesDrawableId()
    {
        // Arrange
        var particle = new Particle();
        var drawableWithId = new FakeDrawable(12345);

        // Act
        particle.AttachDrawable(drawableWithId);

        // Assert
        Assert.Equal(12345u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_MultipleAttachDetach_WorksCorrectly()
    {
        // Arrange
        var particle = new Particle();
        var drawable1 = new FakeDrawable(1);
        var drawable2 = new FakeDrawable(2);

        // Act & Assert - First attach
        particle.AttachDrawable(drawable1);
        Assert.Equal(1u, particle.AttachedDrawableId);

        // Act & Assert - Replace
        particle.AttachDrawable(drawable2);
        Assert.Equal(2u, particle.AttachedDrawableId);

        // Act & Assert - Detach
        particle.DetachDrawable();
        Assert.Null(particle.AttachedDrawable);
        Assert.Equal(0u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_DrawableAttachmentInitialized_InConstructor()
    {
        // Arrange & Act
        var particle = new Particle();

        // Assert
        Assert.Null(particle.AttachedDrawable);
        Assert.Equal(0u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_AttachDrawable_WithZeroId_StoresId()
    {
        // Arrange
        var particle = new Particle();
        var drawableWithZeroId = new FakeDrawable(0);

        // Act
        particle.AttachDrawable(drawableWithZeroId);

        // Assert - Zero ID should be stored as-is (drawable didn't allocate valid ID)
        Assert.Equal(0u, particle.AttachedDrawableId);
    }

    [Fact]
    public void Particle_AttachDrawable_PreservesOtherParticleProperties()
    {
        // Arrange
        var particle = new Particle();
        particle.Position = new Vector3(1, 2, 3);
        particle.Size = 5.0f;
        particle.Alpha = 0.8f;
        var fakeDrawable = new FakeDrawable(77);

        // Act
        particle.AttachDrawable(fakeDrawable);

        // Assert - Other properties should remain unchanged
        Assert.Equal(new Vector3(1, 2, 3), particle.Position);
        Assert.Equal(5.0f, particle.Size);
        Assert.Equal(0.8f, particle.Alpha);
        Assert.Equal(77u, particle.AttachedDrawableId);
    }
}
