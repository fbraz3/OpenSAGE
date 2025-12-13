using System;
using System.Collections.Generic;
using System.Numerics;
using OpenSage.Graphics;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Core;
using Veldrid;

namespace OpenSage.Tests.Graphics;

/// <summary>
/// Helper class for rendering test content in regression tests
/// Provides simplified rendering pipeline for baseline and comparison renders
///
/// Week 24: Regression Testing Framework
/// </summary>
public sealed class RenderingTestHelper : IDisposable
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly IGraphicsDevice _abstractDevice;
    private readonly Framebuffer _renderTarget;
    private readonly Texture _colorTexture;
    private readonly Texture _depthTexture;
    private readonly CommandList _commandList;
    private bool _disposed;

    /// <summary>
    /// Width of render target in pixels
    /// </summary>
    public uint RenderWidth { get; }

    /// <summary>
    /// Height of render target in pixels
    /// </summary>
    public uint RenderHeight { get; }

    /// <summary>
    /// Number of renders completed with this helper
    /// </summary>
    public int RenderCount { get; private set; }

    public RenderingTestHelper(GraphicsDevice graphicsDevice, IGraphicsDevice abstractDevice, uint width, uint height)
    {
        if (graphicsDevice == null)
            throw new ArgumentNullException(nameof(graphicsDevice));
        if (abstractDevice == null)
            throw new ArgumentNullException(nameof(abstractDevice));

        _graphicsDevice = graphicsDevice;
        _abstractDevice = abstractDevice;
        RenderWidth = width;
        RenderHeight = height;
        RenderCount = 0;

        // Create render target textures
        var format = PixelFormat.R8_G8_B8_A8_UNorm;
        var colorDesc = TextureDescription.Texture2D(
            width, height, mipLevels: 1, arrayLayers: 1,
            format,
            TextureUsage.RenderTarget | TextureUsage.Sampled
        );
        _colorTexture = _graphicsDevice.ResourceFactory.CreateTexture(colorDesc);
        _colorTexture.Name = "RenderTest_ColorTarget";

        // Create depth texture
        var depthDesc = TextureDescription.Texture2D(
            width, height, mipLevels: 1, arrayLayers: 1,
            PixelFormat.D32_Float_S8_UInt,
            TextureUsage.DepthStencil
        );
        _depthTexture = _graphicsDevice.ResourceFactory.CreateTexture(depthDesc);
        _depthTexture.Name = "RenderTest_DepthTarget";

        // Create framebuffer
        _renderTarget = _graphicsDevice.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(_depthTexture, _colorTexture)
        );
        _renderTarget.Name = "RenderTest_Framebuffer";

        // Create command list
        _commandList = _graphicsDevice.ResourceFactory.CreateCommandList();
        _commandList.Name = "RenderTest_CommandList";
    }

    /// <summary>
    /// Begins a render frame to the test render target
    /// </summary>
    public void BeginRender()
    {
        ThrowIfDisposed();
        _commandList.Begin();
        _commandList.SetFramebuffer(_renderTarget);
    }

    /// <summary>
    /// Clears the render target to a solid color
    /// </summary>
    /// <param name="color">Color to clear to (RGBA)</param>
    public void ClearRenderTarget(RgbaFloat color)
    {
        ThrowIfDisposed();
        _commandList.ClearColorTarget(0, color);
        _commandList.ClearDepthStencil(1.0f);
    }

    /// <summary>
    /// Ends render frame and submits commands to GPU
    /// </summary>
    public void EndRender()
    {
        ThrowIfDisposed();
        _commandList.End();
        _graphicsDevice.SubmitCommands(_commandList);
        _graphicsDevice.SwapBuffers(_graphicsDevice.MainSwapchain);
        RenderCount++;
    }

    /// <summary>
    /// Renders a simple test pattern (grid) for baseline validation
    /// Useful for verifying rendering pipeline is working
    /// </summary>
    public void RenderTestPattern()
    {
        ThrowIfDisposed();
        BeginRender();
        ClearRenderTarget(new RgbaFloat(0.1f, 0.1f, 0.1f, 1.0f));

        // Simple grid pattern rendered to color texture
        // In a full implementation, would use shaders to render grid
        // For now, this is a placeholder demonstrating the pattern

        EndRender();
    }

    /// <summary>
    /// Captures the current render target to CPU memory
    /// Returns pixel data that can be saved to disk or compared
    /// </summary>
    /// <returns>Raw pixel data (RGBA8) from render target</returns>
    public byte[] CaptureRenderTarget()
    {
        ThrowIfDisposed();

        // Create staging texture for GPU→CPU copy
        var stagingDesc = TextureDescription.Texture2D(
            RenderWidth, RenderHeight, mipLevels: 1, arrayLayers: 1,
            PixelFormat.R8_G8_B8_A8_UNorm,
            TextureUsage.Staging
        );
        var stagingTexture = _graphicsDevice.ResourceFactory.CreateTexture(stagingDesc);

        try
        {
            // Copy render target to staging texture
            var copyCL = _graphicsDevice.ResourceFactory.CreateCommandList();
            copyCL.Begin();
            copyCL.CopyTexture(
                _colorTexture, 0, 0, 0, 0, 0,
                stagingTexture, 0, 0, 0, 0, 0,
                RenderWidth, RenderHeight, 1, 1
            );
            copyCL.End();
            _graphicsDevice.SubmitCommands(copyCL);
            copyCL.Dispose();

            // Wait for copy to complete
            _graphicsDevice.WaitForIdle();

            // Map staging texture and copy data
            var mappedResource = _graphicsDevice.Map(stagingTexture, MapMode.Read);
            var pixelData = new byte[mappedResource.SizeInBytes];

            // Copy mapped data to byte array using Marshal
            System.Runtime.InteropServices.Marshal.Copy(
                mappedResource.Data,
                pixelData,
                0,
                (int)mappedResource.SizeInBytes
            );

            _graphicsDevice.Unmap(stagingTexture);

            return pixelData;
        }
        finally
        {
            stagingTexture?.Dispose();
        }
    }

    /// <summary>
    /// Gets the abstract graphics device used by this helper
    /// Useful for testing IGraphicsDevice compatibility
    /// </summary>
    public IGraphicsDevice GetAbstractDevice() => _abstractDevice;

    /// <summary>
    /// Gets the underlying Veldrid graphics device
    /// </summary>
    public GraphicsDevice GetVeldridDevice() => _graphicsDevice;

    /// <summary>
    /// Gets render target framebuffer
    /// </summary>
    public Framebuffer GetRenderTarget() => _renderTarget;

    public void Dispose()
    {
        if (_disposed)
            return;

        _commandList?.Dispose();
        _renderTarget?.Dispose();
        _colorTexture?.Dispose();
        _depthTexture?.Dispose();

        _disposed = true;
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
            throw new ObjectDisposedException(GetType().Name, "RenderingTestHelper has been disposed");
    }
}

/// <summary>
/// Configuration for rendering test scenes
/// Allows customization of render parameters for baseline and comparison tests
/// </summary>
public class RenderingTestConfig
{
    /// <summary>
    /// Width of render target (default 1920)
    /// </summary>
    public uint Width { get; set; } = 1920;

    /// <summary>
    /// Height of render target (default 1080)
    /// </summary>
    public uint Height { get; set; } = 1080;

    /// <summary>
    /// Background color to clear to (default dark gray)
    /// </summary>
    public RgbaFloat ClearColor { get; set; } = new RgbaFloat(0.1f, 0.1f, 0.1f, 1.0f);

    /// <summary>
    /// Number of frames to warm up GPU before capturing (default 3)
    /// Helps stabilize performance metrics
    /// </summary>
    public int WarmupFrames { get; set; } = 3;

    /// <summary>
    /// Enable debug output (default false)
    /// </summary>
    public bool DebugOutput { get; set; } = false;

    /// <summary>
    /// Validate that configuration is sensible
    /// </summary>
    public bool IsValid()
    {
        return Width > 0 && Width <= 4096 &&
               Height > 0 && Height <= 4096 &&
               WarmupFrames >= 0 && WarmupFrames <= 10;
    }
}
