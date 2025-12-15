using System;
using OpenSage.Graphics.ParticleSystems;

namespace OpenSageGameTests.Graphics.ParticleSystems;

/// <summary>
/// Fake drawable for testing particle attachment.
/// Mimics essential properties of Drawable without inheritance.
/// </summary>
internal class FakeDrawable
{
    public uint ID { get; set; }

    public FakeDrawable(uint id = 0)
    {
        ID = id;
    }

    // Additional properties can be added as needed for testing
    public string? Name { get; set; }
}

/// <summary>
/// Mock particle system for testing individual particle behavior.
/// </summary>
internal class MockParticleSystem
{
    public ParticleColorKeyframe[] ColorKeyframes { get; } = new ParticleColorKeyframe[ParticleSystem.KeyframeCount];
}
