namespace OpenSage.Graphics.Core;

/// <summary>
/// Specifies the graphics backend used for rendering.
/// </summary>
public enum GraphicsBackend
{
    /// <summary>
    /// Veldrid cross-platform graphics abstraction.
    /// Supports: Direct3D11, Vulkan, OpenGL, Metal, OpenGLES
    /// </summary>
    Veldrid,

    /// <summary>
    /// BGFX renderer (planned for future implementation).
    /// Supports: Direct3D11, Vulkan, OpenGL, Metal, WebGPU
    /// </summary>
    BGFX,
}
