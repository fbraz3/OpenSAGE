using System;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.BGFX;
using OpenSage.Graphics.Core;
using Veldrid.Sdl2;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Factory;

/// <summary>
/// Factory for creating graphics devices for different backends.
/// Supports both BGFX and Veldrid backends with automatic fallback.
/// </summary>
public static class GraphicsDeviceFactory
{
    /// <summary>
    /// Creates a graphics device for the specified backend.
    /// Automatically falls back to Veldrid if BGFX initialization fails.
    /// </summary>
    /// <param name="backend">The graphics backend to use.</param>
    /// <param name="window">The SDL2 window for BGFX backend (required for BGFX).</param>
    /// <param name="veldridDevice">The Veldrid GraphicsDevice (required for Veldrid backend or fallback).</param>
    /// <returns>An IGraphicsDevice instance for the specified backend.</returns>
    /// <exception cref="ArgumentNullException">Thrown when required parameters are null.</exception>
    /// <exception cref="ArgumentException">Thrown for unknown graphics backend.</exception>
    public static IGraphicsDevice CreateDevice(
        GraphicsBackend backend,
        Sdl2Window? window = null,
        VeldridLib.GraphicsDevice? veldridDevice = null)
    {
        return backend switch
        {
            GraphicsBackend.BGFX => CreateBgfxDevice(window, veldridDevice),
            GraphicsBackend.Veldrid => CreateVeldridDevice(veldridDevice!),
            _ => throw new ArgumentException($"Unknown graphics backend: {backend}", nameof(backend))
        };
    }

    /// <summary>
    /// Attempts to create a BGFX device, with automatic fallback to Veldrid if initialization fails.
    /// </summary>
    /// <param name="window">The SDL2 window for BGFX initialization.</param>
    /// <param name="veldridFallback">The Veldrid device to use if BGFX fails.</param>
    /// <returns>A BGFX IGraphicsDevice, or Veldrid fallback if BGFX initialization fails.</returns>
    private static IGraphicsDevice CreateBgfxDevice(Sdl2Window? window, VeldridLib.GraphicsDevice? veldridFallback)
    {
        if (window == null)
        {
            throw new ArgumentNullException(
                nameof(window),
                "SDL2 window is required for BGFX backend.");
        }

        try
        {
            // Create BGFX device directly with SDL2 window
            var bgfxDevice = new BgfxGraphicsDevice(window);

            Console.WriteLine("[Graphics] ✅ BGFX device initialized successfully");
            return bgfxDevice;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Graphics] ⚠️  BGFX initialization failed: {ex.Message}");

            // Fallback to Veldrid if BGFX fails
            if (veldridFallback != null)
            {
                Console.WriteLine("[Graphics] ℹ️  Falling back to Veldrid backend");
                return new Adapters.VeldridGraphicsDeviceAdapter(veldridFallback);
            }

            throw new InvalidOperationException(
                "BGFX initialization failed and no Veldrid fallback device provided.",
                ex);
        }
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

        return new Adapters.VeldridGraphicsDeviceAdapter(device);
    }
}
