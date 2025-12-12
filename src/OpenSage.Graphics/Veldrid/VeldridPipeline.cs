using System;
using OpenSage.Graphics.Abstractions;
using OpenSage.Graphics.State;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Adapters;

/// <summary>
/// Veldrid implementation of IPipeline.
/// Wraps a Veldrid GraphicsPipeline with state information for validation and debugging.
/// </summary>
internal class VeldridPipeline : IPipeline
{
    private bool _disposed = false;

    public uint Id { get; }
    public uint Generation { get; }
    public bool IsValid => !_disposed;

    public RasterState RasterState { get; }
    public BlendState BlendState { get; }
    public DepthState DepthState { get; }
    public StencilState StencilState { get; }
    public Handle<IShaderProgram> VertexShader { get; }
    public Handle<IShaderProgram> FragmentShader { get; }

    public VeldridLib.Pipeline Pipeline { get; }

    /// <summary>
    /// Creates a new VeldridPipeline wrapper.
    /// </summary>
    public VeldridPipeline(
        VeldridLib.Pipeline pipeline,
        RasterState rasterState,
        BlendState blendState,
        DepthState depthState,
        StencilState stencilState,
        Handle<IShaderProgram> vertexShader,
        Handle<IShaderProgram> fragmentShader,
        uint id = 0,
        uint generation = 1)
    {
        if (pipeline == null)
            throw new ArgumentNullException(nameof(pipeline));

        Pipeline = pipeline;
        RasterState = rasterState;
        BlendState = blendState;
        DepthState = depthState;
        StencilState = stencilState;
        VertexShader = vertexShader;
        FragmentShader = fragmentShader;
        Id = id;
        Generation = generation;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        Pipeline?.Dispose();
        _disposed = true;
    }
}
