using System;
using System.Numerics;

namespace OpenSage.Graphics.Abstractions;

/// <summary>
/// Primary graphics device interface that abstracts rendering operations.
/// Supports both synchronous (Veldrid) and asynchronous (BGFX) rendering models.
/// </summary>
public interface IGraphicsDevice : IDisposable
{
    /// <summary>
    /// Gets the graphics backend (Veldrid, BGFX, etc.).
    /// </summary>
    Core.GraphicsBackend Backend { get; }

    /// <summary>
    /// Gets the graphics capabilities of the current device.
    /// </summary>
    Core.GraphicsCapabilities Capabilities { get; }

    /// <summary>
    /// Gets a value indicating whether the device is ready for rendering.
    /// </summary>
    bool IsReady { get; }

    /// <summary>
    /// Begins a new frame for rendering.
    /// </summary>
    void BeginFrame();

    /// <summary>
    /// Ends the current frame and submits commands to the GPU.
    /// </summary>
    void EndFrame();

    /// <summary>
    /// Waits for all pending GPU commands to complete.
    /// </summary>
    void WaitForIdle();

    // ===== Buffer Operations =====

    /// <summary>
    /// Creates a new buffer with the specified description.
    /// </summary>
    /// <param name="description">The buffer creation parameters.</param>
    /// <param name="initialData">Optional initial data (may be null).</param>
    /// <returns>A handle to the created buffer.</returns>
    Handle<IBuffer> CreateBuffer(Resources.BufferDescription description, ReadOnlySpan<byte> initialData = default);

    /// <summary>
    /// Destroys a buffer and releases its resources.
    /// </summary>
    /// <param name="buffer">The buffer handle.</param>
    void DestroyBuffer(Handle<IBuffer> buffer);

    /// <summary>
    /// Gets a buffer by its handle for data operations.
    /// </summary>
    /// <param name="buffer">The buffer handle.</param>
    /// <returns>The buffer interface, or null if the handle is invalid.</returns>
    IBuffer? GetBuffer(Handle<IBuffer> buffer);

    // ===== Texture Operations =====

    /// <summary>
    /// Creates a new texture with the specified description.
    /// </summary>
    /// <param name="description">The texture creation parameters.</param>
    /// <param name="initialData">Optional initial texture data.</param>
    /// <returns>A handle to the created texture.</returns>
    Handle<ITexture> CreateTexture(Resources.TextureDescription description, ReadOnlySpan<byte> initialData = default);

    /// <summary>
    /// Destroys a texture and releases its resources.
    /// </summary>
    /// <param name="texture">The texture handle.</param>
    void DestroyTexture(Handle<ITexture> texture);

    /// <summary>
    /// Gets a texture by its handle for data operations.
    /// </summary>
    /// <param name="texture">The texture handle.</param>
    /// <returns>The texture interface, or null if the handle is invalid.</returns>
    ITexture? GetTexture(Handle<ITexture> texture);

    // ===== Sampler Operations =====

    /// <summary>
    /// Creates a new sampler with the specified description.
    /// </summary>
    /// <param name="description">The sampler creation parameters.</param>
    /// <returns>A handle to the created sampler.</returns>
    Handle<ISampler> CreateSampler(Resources.SamplerDescription description);

    /// <summary>
    /// Destroys a sampler and releases its resources.
    /// </summary>
    /// <param name="sampler">The sampler handle.</param>
    void DestroySampler(Handle<ISampler> sampler);

    /// <summary>
    /// Gets a sampler by its handle.
    /// </summary>
    /// <param name="sampler">The sampler handle.</param>
    /// <returns>The sampler interface, or null if the handle is invalid.</returns>
    ISampler? GetSampler(Handle<ISampler> sampler);

    // ===== Framebuffer Operations =====

    /// <summary>
    /// Creates a new framebuffer (render target collection).
    /// </summary>
    /// <param name="description">The framebuffer creation parameters.</param>
    /// <returns>A handle to the created framebuffer.</returns>
    Handle<IFramebuffer> CreateFramebuffer(Resources.FramebufferDescription description);

    /// <summary>
    /// Destroys a framebuffer and releases its resources.
    /// </summary>
    /// <param name="framebuffer">The framebuffer handle.</param>
    void DestroyFramebuffer(Handle<IFramebuffer> framebuffer);

    /// <summary>
    /// Gets a framebuffer by its handle.
    /// </summary>
    /// <param name="framebuffer">The framebuffer handle.</param>
    /// <returns>The framebuffer interface, or null if the handle is invalid.</returns>
    IFramebuffer? GetFramebuffer(Handle<IFramebuffer> framebuffer);

    // ===== Shader Operations =====

    /// <summary>
    /// Creates a shader program from SPIR-V bytecode.
    /// </summary>
    /// <param name="name">The shader name/identifier.</param>
    /// <param name="spirvData">The SPIR-V bytecode.</param>
    /// <param name="entryPoint">The shader entry point function name.</param>
    /// <returns>A handle to the created shader program.</returns>
    Handle<IShaderProgram> CreateShader(string name, ReadOnlySpan<byte> spirvData, string entryPoint = "main");

    /// <summary>
    /// Destroys a shader program.
    /// </summary>
    /// <param name="shader">The shader handle.</param>
    void DestroyShader(Handle<IShaderProgram> shader);

    /// <summary>
    /// Gets a shader by its handle.
    /// </summary>
    /// <param name="shader">The shader handle.</param>
    /// <returns>The shader interface, or null if the handle is invalid.</returns>
    IShaderProgram? GetShader(Handle<IShaderProgram> shader);

    // ===== Pipeline Operations =====

    /// <summary>
    /// Creates a graphics pipeline with the specified state objects.
    /// </summary>
    /// <param name="vertexShader">The vertex shader program handle.</param>
    /// <param name="fragmentShader">The fragment shader program handle.</param>
    /// <param name="rasterState">The rasterization state.</param>
    /// <param name="depthState">The depth state.</param>
    /// <param name="blendState">The blend state.</param>
    /// <param name="stencilState">The stencil state.</param>
    /// <returns>A handle to the created pipeline.</returns>
    Handle<IPipeline> CreatePipeline(
        Handle<IShaderProgram> vertexShader,
        Handle<IShaderProgram> fragmentShader,
        State.RasterState rasterState = default,
        State.DepthState depthState = default,
        State.BlendState blendState = default,
        State.StencilState stencilState = default);

    /// <summary>
    /// Destroys a pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline handle.</param>
    void DestroyPipeline(Handle<IPipeline> pipeline);

    /// <summary>
    /// Gets a pipeline by its handle.
    /// </summary>
    /// <param name="pipeline">The pipeline handle.</param>
    /// <returns>The pipeline interface, or null if the handle is invalid.</returns>
    IPipeline? GetPipeline(Handle<IPipeline> pipeline);

    // ===== Rendering Operations =====

    /// <summary>
    /// Sets the active render target.
    /// </summary>
    /// <param name="framebuffer">The framebuffer handle (Invalid to render to backbuffer).</param>
    void SetRenderTarget(Handle<IFramebuffer> framebuffer);

    /// <summary>
    /// Clears the current render target.
    /// </summary>
    /// <param name="clearColor">The color to clear to (RGBA, 0.0-1.0).</param>
    /// <param name="clearDepth">The depth value to clear to (0.0-1.0).</param>
    /// <param name="clearStencil">The stencil value to clear to.</param>
    /// <param name="colorMask">If true, clear the color attachment.</param>
    /// <param name="depthMask">If true, clear the depth attachment.</param>
    /// <param name="stencilMask">If true, clear the stencil attachment.</param>
    void ClearRenderTarget(
        Vector4 clearColor,
        float clearDepth = 1.0f,
        byte clearStencil = 0,
        bool colorMask = true,
        bool depthMask = true,
        bool stencilMask = true);

    /// <summary>
    /// Sets the active graphics pipeline.
    /// </summary>
    /// <param name="pipeline">The pipeline handle.</param>
    void SetPipeline(Handle<IPipeline> pipeline);

    /// <summary>
    /// Sets the viewport.
    /// </summary>
    /// <param name="x">Left edge in pixels.</param>
    /// <param name="y">Top edge in pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    /// <param name="minDepth">Minimum depth value (typically 0.0).</param>
    /// <param name="maxDepth">Maximum depth value (typically 1.0).</param>
    void SetViewport(float x, float y, float width, float height, float minDepth = 0.0f, float maxDepth = 1.0f);

    /// <summary>
    /// Sets the scissor rectangle.
    /// </summary>
    /// <param name="x">Left edge in pixels.</param>
    /// <param name="y">Top edge in pixels.</param>
    /// <param name="width">Width in pixels.</param>
    /// <param name="height">Height in pixels.</param>
    void SetScissor(int x, int y, int width, int height);

    /// <summary>
    /// Binds a vertex buffer.
    /// </summary>
    /// <param name="buffer">The vertex buffer handle.</param>
    /// <param name="offset">Offset in bytes from the start of the buffer.</param>
    void BindVertexBuffer(Handle<IBuffer> buffer, uint offset = 0);

    /// <summary>
    /// Binds an index buffer.
    /// </summary>
    /// <param name="buffer">The index buffer handle.</param>
    /// <param name="offset">Offset in bytes from the start of the buffer.</param>
    void BindIndexBuffer(Handle<IBuffer> buffer, uint offset = 0);

    /// <summary>
    /// Binds a constant/uniform buffer.
    /// </summary>
    /// <param name="buffer">The uniform buffer handle.</param>
    /// <param name="slot">The shader constant buffer slot (0-15).</param>
    void BindUniformBuffer(Handle<IBuffer> buffer, uint slot);

    /// <summary>
    /// Binds a texture as a shader resource.
    /// </summary>
    /// <param name="texture">The texture handle.</param>
    /// <param name="slot">The shader texture slot (0-15).</param>
    /// <param name="sampler">The sampler handle to use with the texture.</param>
    void BindTexture(Handle<ITexture> texture, uint slot, Handle<ISampler> sampler);

    /// <summary>
    /// Draws indexed geometry.
    /// </summary>
    /// <param name="indexCount">The number of indices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="startIndex">The index of the first vertex to draw.</param>
    /// <param name="baseVertex">The base vertex offset.</param>
    /// <param name="startInstance">The base instance offset.</param>
    void DrawIndexed(uint indexCount, uint instanceCount = 1, uint startIndex = 0, int baseVertex = 0, uint startInstance = 0);

    /// <summary>
    /// Draws non-indexed geometry.
    /// </summary>
    /// <param name="vertexCount">The number of vertices to draw.</param>
    /// <param name="instanceCount">The number of instances to draw.</param>
    /// <param name="startVertex">The index of the first vertex to draw.</param>
    /// <param name="startInstance">The base instance offset.</param>
    void DrawVertices(uint vertexCount, uint instanceCount = 1, uint startVertex = 0, uint startInstance = 0);

    /// <summary>
    /// Draws indirect indexed geometry from a buffer.
    /// </summary>
    /// <param name="buffer">The indirect buffer handle.</param>
    /// <param name="offset">Offset in bytes to the indirect draw arguments.</param>
    /// <param name="drawCount">The number of draw commands.</param>
    /// <param name="stride">Stride in bytes between draw commands.</param>
    void DrawIndexedIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride);

    /// <summary>
    /// Draws indirect non-indexed geometry from a buffer.
    /// </summary>
    /// <param name="buffer">The indirect buffer handle.</param>
    /// <param name="offset">Offset in bytes to the indirect draw arguments.</param>
    /// <param name="drawCount">The number of draw commands.</param>
    /// <param name="stride">Stride in bytes between draw commands.</param>
    void DrawVerticesIndirect(Handle<IBuffer> buffer, uint offset, uint drawCount, uint stride);
}
