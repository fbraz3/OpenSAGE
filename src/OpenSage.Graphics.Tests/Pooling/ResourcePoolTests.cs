using NUnit.Framework;
using OpenSage.Graphics.Pooling;
using System;

namespace OpenSage.Graphics.Tests.Pooling;

public class ResourcePoolTests
{
    private class TestResource : IDisposable
    {
        public int Id { get; private set; }
        public bool IsDisposed { get; private set; }

        public TestResource(int id)
        {
            Id = id;
            IsDisposed = false;
        }

        public void Dispose()
        {
            IsDisposed = true;
        }
    }

    [Test]
    public void Allocate_CreatesValidHandle()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource = new TestResource(1);

        var handle = pool.Allocate(resource);

        Assert.That(handle.IsValid, Is.True);
        Assert.That(handle.Index, Is.EqualTo(0));
        Assert.That(handle.Generation, Is.EqualTo(1));
    }

    [Test]
    public void TryGet_ReturnsResourceForValidHandle()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource = new TestResource(1);
        var handle = pool.Allocate(resource);

        bool found = pool.TryGet(handle, out var retrieved);

        Assert.That(found, Is.True);
        Assert.That(retrieved, Is.SameAs(resource));
    }

    [Test]
    public void TryGet_ReturnsFalseForInvalidHandle()
    {
        var pool = new ResourcePool<TestResource>(16);
        var invalidHandle = ResourcePool<TestResource>.PoolHandle.Invalid;

        bool found = pool.TryGet(invalidHandle, out var retrieved);

        Assert.That(found, Is.False);
        Assert.That(retrieved, Is.Null);
    }

    [Test]
    public void Release_DisposesResourceAndFreesSlot()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource = new TestResource(1);
        var handle = pool.Allocate(resource);

        bool released = pool.Release(handle);

        Assert.That(released, Is.True);
        Assert.That(resource.IsDisposed, Is.True);
    }

    [Test]
    public void Release_ReusesSlotsWithIncrementedGeneration()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource1 = new TestResource(1);
        var handle1 = pool.Allocate(resource1);

        pool.Release(handle1);

        var resource2 = new TestResource(2);
        var handle2 = pool.Allocate(resource2);

        // Same index, incremented generation
        Assert.That(handle2.Index, Is.EqualTo(handle1.Index));
        Assert.That(handle2.Generation, Is.GreaterThan(handle1.Generation));

        // Old handle should be invalid
        bool found = pool.TryGet(handle1, out _);
        Assert.That(found, Is.False);

        // New handle should be valid
        found = pool.TryGet(handle2, out var retrieved);
        Assert.That(found, Is.True);
        Assert.That(retrieved, Is.SameAs(resource2));
    }

    [Test]
    public void IsValid_ReturnsTrueForValidHandle()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource = new TestResource(1);
        var handle = pool.Allocate(resource);

        bool valid = pool.IsValid(handle);

        Assert.That(valid, Is.True);
    }

    [Test]
    public void IsValid_ReturnsFalseForReleasedHandle()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resource = new TestResource(1);
        var handle = pool.Allocate(resource);

        pool.Release(handle);
        bool valid = pool.IsValid(handle);

        Assert.That(valid, Is.False);
    }

    [Test]
    public void AllocatedCount_ReflectsAllocationsAndReleases()
    {
        var pool = new ResourcePool<TestResource>(16);

        var resource1 = new TestResource(1);
        var handle1 = pool.Allocate(resource1);
        Assert.That(pool.AllocatedCount, Is.EqualTo(1));

        var resource2 = new TestResource(2);
        var handle2 = pool.Allocate(resource2);
        Assert.That(pool.AllocatedCount, Is.EqualTo(2));

        pool.Release(handle1);
        Assert.That(pool.AllocatedCount, Is.EqualTo(1));
    }

    [Test]
    public void FreeSlots_ReflectsReleasedSlots()
    {
        var pool = new ResourcePool<TestResource>(16);

        var resource1 = new TestResource(1);
        var handle1 = pool.Allocate(resource1);
        Assert.That(pool.FreeSlots, Is.EqualTo(0));

        pool.Release(handle1);
        Assert.That(pool.FreeSlots, Is.EqualTo(1));

        var resource2 = new TestResource(2);
        pool.Allocate(resource2);
        Assert.That(pool.FreeSlots, Is.EqualTo(0));
    }

    [Test]
    public void Clear_DisposesAllResources()
    {
        var pool = new ResourcePool<TestResource>(16);
        var resources = new[] {
            new TestResource(1),
            new TestResource(2),
            new TestResource(3)
        };

        foreach (var res in resources)
        {
            pool.Allocate(res);
        }

        pool.Clear();

        foreach (var res in resources)
        {
            Assert.That(res.IsDisposed, Is.True);
        }

        Assert.That(pool.AllocatedCount, Is.EqualTo(0));
    }

    [Test]
    public void Allocate_ThrowsOnNullResource()
    {
        var pool = new ResourcePool<TestResource>(16);

        Assert.Throws<ArgumentNullException>(() => pool.Allocate(null));
    }

    [Test]
    public void Constructor_ThrowsOnNegativeCapacity()
    {
        Assert.Throws<ArgumentException>(() => new ResourcePool<TestResource>(-1));
        Assert.Throws<ArgumentException>(() => new ResourcePool<TestResource>(0));
    }
}
