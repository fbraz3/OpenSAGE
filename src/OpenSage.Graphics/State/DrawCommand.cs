using System;
using System.Numerics;

namespace OpenSage.Graphics.State;

/// <summary>
/// Represents a complete graphics draw command with all render state.
/// Useful for serializing and replaying render commands.
/// </summary>
public struct DrawCommand
{
    /// <summary>
    /// Gets or sets the graphics pipeline.
    /// </summary>
    public Abstractions.Handle<Abstractions.IPipeline> Pipeline { get; set; }

    /// <summary>
    /// Gets or sets the vertex buffer.
    /// </summary>
    public Abstractions.Handle<Abstractions.IBuffer> VertexBuffer { get; set; }

    /// <summary>
    /// Gets or sets the index buffer.
    /// </summary>
    public Abstractions.Handle<Abstractions.IBuffer> IndexBuffer { get; set; }

    /// <summary>
    /// Gets or sets the number of indices/vertices to draw.
    /// </summary>
    public uint Count { get; set; }

    /// <summary>
    /// Gets or sets the number of instances.
    /// </summary>
    public uint InstanceCount { get; set; }

    /// <summary>
    /// Gets or sets the base vertex offset.
    /// </summary>
    public int BaseVertex { get; set; }

    /// <summary>
    /// Gets or sets the base instance offset.
    /// </summary>
    public uint BaseInstance { get; set; }

    /// <summary>
    /// Gets or sets the viewport.
    /// </summary>
    public Vector4 Viewport { get; set; }

    /// <summary>
    /// Gets or sets the scissor rectangle.
    /// </summary>
    public Vector4 Scissor { get; set; }

    /// <summary>
    /// Gets or sets the clear color (if applicable).
    /// </summary>
    public Vector4 ClearColor { get; set; }

    /// <summary>
    /// Gets or sets the clear depth value (0.0-1.0).
    /// </summary>
    public float ClearDepth { get; set; }

    /// <summary>
    /// Gets or sets the clear stencil value.
    /// </summary>
    public byte ClearStencil { get; set; }

    /// <summary>
    /// Gets or sets the blend state.
    /// </summary>
    public BlendState BlendState { get; set; }

    /// <summary>
    /// Gets or sets the depth state.
    /// </summary>
    public DepthState DepthState { get; set; }

    /// <summary>
    /// Gets or sets the raster state.
    /// </summary>
    public RasterState RasterState { get; set; }

    /// <summary>
    /// Gets or sets the stencil state.
    /// </summary>
    public StencilState StencilState { get; set; }

    /// <summary>
    /// Gets or sets the render target framebuffer (Invalid for backbuffer).
    /// </summary>
    public Abstractions.Handle<Abstractions.IFramebuffer> RenderTarget { get; set; }
}
