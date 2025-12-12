using System;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.Core;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Factory;

/// <summary>
/// Factory for creating graphics devices for different backends.
/// </summary>
public static class GraphicsDeviceFactory
{
    /// <summary>
    /// Creates a graphics device for the specified backend.
    /// </summary>
    /// <param name="backend">The graphics backend to use.</param>
    /// <param name="veldridDevice">The Veldrid GraphicsDevice (required for Veldrid backend).</param>
    /// <returns>An IGraphicsDevice instance for the specified backend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when veldridDevice is null for Veldrid backend.</exception>
    /// <exception cref="NotImplementedException">Thrown for BGFX backend (planned for Week 14-18).</exception>
    public static IGraphicsDevice CreateDevice(
        GraphicsBackend backend,
        VeldridLib.GraphicsDevice? veldridDevice = null)
    {
        return backend switch
        {
            GraphicsBackend.Veldrid => CreateVeldridDevice(veldridDevice!),
            GraphicsBackend.BGFX => throw new NotImplementedException(
                "BGFX backend will be implemented in Phase 3, Weeks 14-18."),
            _ => throw new ArgumentException($"Unknown graphics backend: {backend}", nameof(backend))
        };
    }

    /// <summary>
    /// Creates a Veldrid-based graphics device.
    /// </summary>
    /// <param name="device">The Veldrid GraphicsDevice to wrap.</param>
    /// <returns>A VeldridGraphicsDevice instance.</returns>
    /// <exception cref="ArgumentNullException">Thrown when device is null.</exception>
    private static IGraphicsDevice CreateVeldridDevice(VeldridLib.GraphicsDevice device)
    {
        if (device == null)
        {
            throw new ArgumentNullException(
                nameof(device),
                "Veldrid GraphicsDevice is required for Veldrid backend.");
        }

        return new Veldrid.VeldridGraphicsDevice(device);
    }
}
