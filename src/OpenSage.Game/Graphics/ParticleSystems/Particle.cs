#nullable enable

using System.Numerics;
using OpenSage.Client;

namespace OpenSage.Graphics.ParticleSystems;

internal struct Particle : IPersistableObject
{
    private readonly ParticleSystem _system;

    public int Timer;

    public Vector3 Position;
    public Vector3 EmitterPosition;

    public Vector3 Velocity;
    public float VelocityDamping;

    public float Size;
    public float SizeRate;
    public float SizeRateDamping;

    public float AngleZ;

    public float AngularRateZ;

    public float AngularDamping;

    public int Lifetime;
    public bool Dead;

    // Drawable particle fields
    public object? AttachedDrawable;
    public uint AttachedDrawableId;

    // Streak particle fields - stores trail history for ribbon rendering
    public Vector3[]? StreakVertices;
    public int StreakVertexCount;
    private const int MaxStreakVertices = 50;

    public float ColorScale;
    public Vector3 Color;

    public readonly ParticleAlphaKeyframe[] AlphaKeyframes;
    public float Alpha;

    public bool IsParticleUpTowardsEmitter;
    public float UnknownFloat;
    public uint ParticleId;
    public uint UnknownInt2;
    public uint UnknownInt3;
    public uint UnknownInt4;
    public uint UnknownInt5;
    public Vector3 UnknownVector;
    public uint UnknownInt6;

    /// <summary>
    /// Attaches a drawable to this particle.
    /// The drawable will follow the particle's position, size, and rotation.
    /// </summary>
    public void AttachDrawable(object drawable)
    {
        AttachedDrawable = drawable;
        if (drawable != null)
        {
            // Try to extract ID from drawable (works for Drawable and compatible mock objects)
            if (drawable is Drawable d)
            {
                AttachedDrawableId = d.ID;
            }
            else if (drawable.GetType().GetProperty("ID")?.GetValue(drawable) is uint id)
            {
                // Fallback for mock objects with ID property (like FakeDrawable)
                AttachedDrawableId = id;
            }
            else
            {
                AttachedDrawableId = 0;
            }
        }
        else
        {
            AttachedDrawableId = 0;
        }
    }

    /// <summary>
    /// Detaches the drawable from this particle.
    /// </summary>
    public void DetachDrawable()
    {
        AttachedDrawable = null;
        AttachedDrawableId = 0;
    }

    /// <summary>
    /// Records current particle position as a vertex in the streak trail.
    /// Shifts older vertices back in the array, adding new position at index 0.
    /// </summary>
    public void RecordStreakVertex()
    {
        if (StreakVertices == null)
        {
            StreakVertices = new Vector3[MaxStreakVertices];
        }

        // Shift all vertices back one position to create history
        for (int i = MaxStreakVertices - 1; i > 0; i--)
        {
            StreakVertices[i] = StreakVertices[i - 1];
        }

        // Add current position as the newest vertex
        StreakVertices[0] = Position;

        // Track how many vertices have been recorded
        if (StreakVertexCount < MaxStreakVertices)
        {
            StreakVertexCount++;
        }
    }

    public Particle(ParticleSystem system)
    {
        _system = system;

        Timer = 0;
        Position = Vector3.Zero;
        EmitterPosition = Vector3.Zero;
        Velocity = Vector3.Zero;
        VelocityDamping = 0;
        Size = 0;
        SizeRate = 0;
        SizeRateDamping = 0;
        AngleZ = 0;
        AngularRateZ = 0;
        AngularDamping = 0;
        Lifetime = 0;
        Dead = true;
        ColorScale = 0;
        Color = Vector3.Zero;
        AlphaKeyframes = new ParticleAlphaKeyframe[8];
        Alpha = 0;
        IsParticleUpTowardsEmitter = false;
        UnknownFloat = 0;
        ParticleId = 0;
        UnknownInt2 = 0;
        UnknownInt3 = 0;
        UnknownInt4 = 0;
        UnknownInt5 = 0;
        UnknownVector = Vector3.Zero;
        UnknownInt6 = 0;
        AttachedDrawable = null;
        AttachedDrawableId = 0;
        StreakVertices = null;
        StreakVertexCount = 0;
    }

    public void Persist(StatePersister reader)
    {
        reader.PersistVersion(1);

        reader.BeginObject("Base");
        reader.PersistVersion(1);
        reader.EndObject();

        var unusedFloat = 0.0f;

        reader.PersistVector3(ref Velocity);
        reader.PersistVector3(ref Position);
        reader.PersistVector3(ref EmitterPosition);
        reader.PersistSingle(ref VelocityDamping);
        reader.PersistSingle(ref unusedFloat, "AngleX");
        reader.PersistSingle(ref unusedFloat, "AngleY");
        reader.PersistSingle(ref AngleZ, "AngleZ");
        reader.PersistSingle(ref unusedFloat, "AngularRateX");
        reader.PersistSingle(ref unusedFloat, "AngularRateY");
        reader.PersistSingle(ref AngularRateZ);
        reader.PersistInt32(ref Lifetime);
        reader.PersistSingle(ref Size);
        reader.PersistSingle(ref SizeRate);
        reader.PersistSingle(ref SizeRateDamping);

        reader.PersistArray(AlphaKeyframes,
            static (StatePersister persister, ref ParticleAlphaKeyframe item) =>
            {
                persister.PersistObjectValue(ref item);
            });

        reader.PersistArray(_system.ColorKeyframes,
            static (StatePersister persister, ref ParticleColorKeyframe item) =>
            {
                persister.PersistObjectValue(ref item);
            },
            "ColorKeyframes");

        reader.PersistSingle(ref ColorScale);
        reader.PersistBoolean(ref IsParticleUpTowardsEmitter);
        reader.PersistSingle(ref UnknownFloat);
        reader.PersistUInt32(ref ParticleId);

        reader.SkipUnknownBytes(24);

        reader.PersistUInt32(ref UnknownInt2); // 49
        reader.PersistUInt32(ref UnknownInt3); // 1176
        reader.PersistSingle(ref Alpha);
        reader.PersistUInt32(ref UnknownInt4); // 0
        reader.PersistUInt32(ref UnknownInt5); // 1
        reader.PersistVector3(ref Color);
        reader.PersistVector3(ref UnknownVector);
        reader.PersistUInt32(ref UnknownInt6); // 1

        reader.SkipUnknownBytes(8);
    }
}

internal struct ParticleAlphaKeyframe : IParticleKeyframe, IPersistableObject
{
    public uint Time;
    public float Alpha;

    uint IParticleKeyframe.Time => Time;

    public ParticleAlphaKeyframe(RandomAlphaKeyframe keyframe)
    {
        Time = keyframe.Time;
        Alpha = ParticleSystemUtility.GetRandomFloat(keyframe.Value);
    }

    public ParticleAlphaKeyframe(uint time, float alpha)
    {
        Time = time;
        Alpha = alpha;
    }

    public void Persist(StatePersister persister)
    {
        persister.PersistSingle(ref Alpha);
        persister.PersistUInt32(ref Time);
    }
}
