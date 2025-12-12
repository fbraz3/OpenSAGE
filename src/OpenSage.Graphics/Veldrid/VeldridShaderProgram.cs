using System;
using System.Collections.Generic;
using OpenSage.Graphics.Abstractions;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Adapters;

/// <summary>
/// Veldrid implementation of IShaderProgram.
/// Wraps a Veldrid shader with support for multiple shader stages.
/// </summary>
internal class VeldridShaderProgram : IShaderProgram
{
    private readonly Dictionary<VeldridLib.ShaderStages, VeldridLib.Shader> _stages;
    private bool _disposed = false;

    public string Name { get; }
    public string EntryPoint { get; }
    public uint Id { get; }
    public uint Generation { get; }
    public bool IsValid => !_disposed;
    public VeldridLib.Shader VeldridShader { get; }

    /// <summary>
    /// Creates a new VeldridShaderProgram with a single shader stage.
    /// </summary>
    public VeldridShaderProgram(string name, VeldridLib.Shader shader, string entryPoint = "main", uint id = 0, uint generation = 1)
    {
        if (string.IsNullOrEmpty(name))
            throw new ArgumentNullException(nameof(name));
        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        Name = name;
        EntryPoint = entryPoint;
        VeldridShader = shader;
        Id = id;
        Generation = generation;
        _stages = new Dictionary<VeldridLib.ShaderStages, VeldridLib.Shader>
        {
            { VeldridLib.ShaderStages.Fragment, shader }
        };
    }

    /// <summary>
    /// Sets a shader for a specific stage.
    /// </summary>
    public void SetStageShader(VeldridLib.ShaderStages stage, VeldridLib.Shader shader)
    {
        if (shader == null)
            throw new ArgumentNullException(nameof(shader));

        _stages[stage] = shader;
    }

    /// <summary>
    /// Gets the shader for a specific stage.
    /// </summary>
    public VeldridLib.Shader? GetStageShader(VeldridLib.ShaderStages stage)
    {
        return _stages.TryGetValue(stage, out var shader) ? shader : null;
    }

    /// <summary>
    /// Checks if a specific shader stage is defined.
    /// </summary>
    public bool HasStage(VeldridLib.ShaderStages stage) => _stages.ContainsKey(stage);

    public void Dispose()
    {
        if (_disposed)
            return;

        VeldridShader?.Dispose();
        _disposed = true;
    }
}
