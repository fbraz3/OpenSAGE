using System;
using NUnit.Framework;
using OpenSage.Graphics.Adapters;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Factory;
using OpenSage.Graphics.Core;
using Veldrid;
using Veldrid.StartupUtilities;

namespace OpenSage.Graphics.Tests;

/// <summary>
/// Tests for VeldridGraphicsDeviceAdapter - verifies the abstraction layer integration
/// </summary>
[TestFixture]
public class VeldridGraphicsDeviceAdapterTests
{
    [Test]
    public void CreateGraphicsDevice_Veldrid_ReturnsValidDevice()
    {
        // Create a headless Veldrid device for testing
        var graphicsDevice = GraphicsDevice.CreateMetal(
            new GraphicsDeviceOptions
            {
                Debug = false,
                SyncToVerticalBlank = false
            });

        try
        {
            // Create adapter
            var adapter = new VeldridGraphicsDeviceAdapter(graphicsDevice);

            // Verify it implements IGraphicsDevice
            Assert.That(adapter, Is.InstanceOf<IGraphicsDevice>());

            // Verify underlying device is accessible
            Assert.That(adapter.UnderlyingDevice, Is.EqualTo(graphicsDevice));
        }
        finally
        {
            graphicsDevice.Dispose();
        }
    }

    [Test]
    public void GraphicsDeviceFactory_CreateDevice_ReturnsAdapterForVeldrid()
    {
        // Create a headless Veldrid device
        var graphicsDevice = GraphicsDevice.CreateMetal(
            new GraphicsDeviceOptions
            {
                Debug = false,
                SyncToVerticalBlank = false
            });

        try
        {
            // Use factory to create device
            var device = GraphicsDeviceFactory.CreateDevice(GraphicsBackend.Veldrid, graphicsDevice);

            // Verify it returns IGraphicsDevice
            Assert.That(device, Is.InstanceOf<IGraphicsDevice>());

            // Verify it's the adapter
            Assert.That(device, Is.InstanceOf<VeldridGraphicsDeviceAdapter>());
        }
        finally
        {
            graphicsDevice.Dispose();
        }
    }

    [Test]
    public void VeldridGraphicsDeviceAdapter_BufferOperations_NoThrow()
    {
        // Create a headless Veldrid device
        var graphicsDevice = GraphicsDevice.CreateMetal(
            new GraphicsDeviceOptions
            {
                Debug = false,
                SyncToVerticalBlank = false
            });

        try
        {
            var adapter = new VeldridGraphicsDeviceAdapter(graphicsDevice);

            // These should not throw - they return dummy handles
            var bufferHandle = adapter.CreateBuffer(100, GraphicsBufferKind.Vertex);
            Assert.That(bufferHandle, Is.Not.Null);

            var textureHandle = adapter.CreateTexture(100, 100, 1, GraphicsPixelFormat.RGBA_UInt8, GraphicsTextureUsage.Shader);
            Assert.That(textureHandle, Is.Not.Null);

            var samplerHandle = adapter.CreateSampler(new GraphicsSamplerDescription());
            Assert.That(samplerHandle, Is.Not.Null);
        }
        finally
        {
            graphicsDevice.Dispose();
        }
    }

    [Test]
    public void VeldridGraphicsDeviceAdapter_RenderingOperations_NoThrow()
    {
        // Create a headless Veldrid device
        var graphicsDevice = GraphicsDevice.CreateMetal(
            new GraphicsDeviceOptions
            {
                Debug = false,
                SyncToVerticalBlank = false
            });

        try
        {
            var adapter = new VeldridGraphicsDeviceAdapter(graphicsDevice);

            // These should not throw - they're no-ops
            adapter.Clear(new GraphicsColorValue(0, 0, 0, 1));
            adapter.DrawPrimitives(GraphicsPrimitiveTopology.TriangleList, 0, 3);
            adapter.DrawIndexedPrimitives(GraphicsPrimitiveTopology.TriangleList, 0, 3, 0);
        }
        finally
        {
            graphicsDevice.Dispose();
        }
    }
}
