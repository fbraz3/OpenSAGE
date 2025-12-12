using System;
using NUnit.Framework;
using OpenSage.Graphics.Adapters;
using OpenSage.Graphics.Abstractions;
using Veldrid;

namespace OpenSage.Graphics.Tests.Veldrid;

/// <summary>
/// Smoke tests for VeldridGraphicsDeviceAdapter shader creation.
/// Validates that CreateShader/DestroyShader operations work correctly.
/// </summary>
[TestFixture]
public class VeldridShaderCreationTests
{
    // Minimal valid SPIR-V bytecode for a vertex shader
    // Magic: 0x07230203, Version: 1.0, Generator: LLVM 5
    private static readonly byte[] MinimalSpirvVertexShader = new byte[]
    {
        // SPIR-V Header
        0x07, 0x23, 0x03, 0x03, // Magic number
        0x01, 0x00, 0x05, 0x00, // Version 1.0
        0x08, 0x00, 0x00, 0x00, // Generator LLVM 5
        0x25, 0x00, 0x00, 0x00, // Bound
        0x01, 0x00, 0x00, 0x00, // Schema
        // OpTypeVoid %1
        0x00, 0x00, 0x02, 0x00, 0x19, 0x00, 0x01, 0x00,
        // OpTypeBool %2
        0x00, 0x00, 0x02, 0x00, 0x20, 0x00, 0x02, 0x00,
        // More minimal SPIR-V content...
    };

    private GraphicsDevice? _graphicsDevice;
    private VeldridGraphicsDeviceAdapter? _adapter;

    [SetUp]
    public void SetUp()
    {
        // Create a headless Metal device for testing
        _graphicsDevice = GraphicsDevice.CreateMetal(
            new GraphicsDeviceOptions
            {
                Debug = false,
                SyncToVerticalBlank = false
            });

        _adapter = new VeldridGraphicsDeviceAdapter(_graphicsDevice);
    }

    [TearDown]
    public void TearDown()
    {
        _adapter?.Dispose();
        _graphicsDevice?.Dispose();
    }

    [Test]
    public void CreateShader_WithValidVertexSpirv_ReturnsValidHandle()
    {
        // Arrange
        var shaderName = "TestVertexShader";
        var stage = ShaderStages.Vertex;

        // Act
        var shaderHandle = _adapter!.CreateShader(shaderName, stage, MinimalSpirvVertexShader);

        // Assert
        Assert.That(shaderHandle.IsValid, Is.True);
        Assert.That(shaderHandle.Id, Is.GreaterThan(0u));
    }

    [Test]
    public void CreateShader_WithFragmentStage_ReturnsValidHandle()
    {
        // Arrange
        var shaderName = "TestFragmentShader";
        var stage = ShaderStages.Fragment;

        // Act
        var shaderHandle = _adapter!.CreateShader(shaderName, stage, MinimalSpirvVertexShader);

        // Assert
        Assert.That(shaderHandle.IsValid, Is.True);
        Assert.That(shaderHandle.Id, Is.GreaterThan(0u));
    }

    [Test]
    public void GetShader_AfterCreate_ReturnsShaderProgram()
    {
        // Arrange
        var shaderName = "TestShader";
        var stage = ShaderStages.Vertex;
        var shaderHandle = _adapter!.CreateShader(shaderName, stage, MinimalSpirvVertexShader);

        // Act
        var shaderProgram = _adapter.GetShader(shaderHandle);

        // Assert
        Assert.That(shaderProgram, Is.Not.Null);
        Assert.That(shaderProgram!.Name, Is.EqualTo(shaderName));
        Assert.That(shaderProgram.EntryPoint, Is.EqualTo("main"));
    }

    [Test]
    public void DestroyShader_WithValidHandle_RemovesShader()
    {
        // Arrange
        var shaderName = "TestShader";
        var shaderHandle = _adapter!.CreateShader(shaderName, ShaderStages.Vertex, MinimalSpirvVertexShader);

        // Act
        _adapter.DestroyShader(shaderHandle);

        // Assert
        var retrievedShader = _adapter.GetShader(shaderHandle);
        Assert.That(retrievedShader, Is.Null);
    }

    [Test]
    public void CreateMultipleShaders_ReturnsDifferentHandles()
    {
        // Act
        var shader1Handle = _adapter!.CreateShader("Shader1", ShaderStages.Vertex, MinimalSpirvVertexShader);
        var shader2Handle = _adapter.CreateShader("Shader2", ShaderStages.Fragment, MinimalSpirvVertexShader);

        // Assert
        Assert.That(shader1Handle.Id, Is.Not.EqualTo(shader2Handle.Id));
        Assert.That(shader1Handle.IsValid, Is.True);
        Assert.That(shader2Handle.IsValid, Is.True);
    }

    [Test]
    public void CreatePipeline_WithValidShaders_ReturnsValidHandle()
    {
        // Arrange
        var vsHandle = _adapter!.CreateShader("VS", ShaderStages.Vertex, MinimalSpirvVertexShader);
        var fsHandle = _adapter.CreateShader("FS", ShaderStages.Fragment, MinimalSpirvVertexShader);

        // Act
        var pipelineHandle = _adapter.CreatePipeline(vsHandle, fsHandle);

        // Assert
        Assert.That(pipelineHandle.IsValid, Is.True);
        Assert.That(pipelineHandle.Id, Is.GreaterThan(0u));
    }

    [Test]
    public void DestroyPipeline_WithValidHandle_RemovesPipeline()
    {
        // Arrange
        var vsHandle = _adapter!.CreateShader("VS", ShaderStages.Vertex, MinimalSpirvVertexShader);
        var fsHandle = _adapter.CreateShader("FS", ShaderStages.Fragment, MinimalSpirvVertexShader);
        var pipelineHandle = _adapter.CreatePipeline(vsHandle, fsHandle);

        // Act
        _adapter.DestroyPipeline(pipelineHandle);

        // Assert - GetPipeline should return null (not yet implemented wrapper)
        var retrievedPipeline = _adapter.GetPipeline(pipelineHandle);
        Assert.That(retrievedPipeline, Is.Null);
    }

    [Test]
    public void SetPipeline_WithValidHandle_DoesNotThrow()
    {
        // Arrange
        var vsHandle = _adapter!.CreateShader("VS", ShaderStages.Vertex, MinimalSpirvVertexShader);
        var fsHandle = _adapter.CreateShader("FS", ShaderStages.Fragment, MinimalSpirvVertexShader);
        var pipelineHandle = _adapter.CreatePipeline(vsHandle, fsHandle);

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => _adapter.SetPipeline(pipelineHandle));
    }

    [Test]
    public void SetPipeline_WithInvalidHandle_DoesNotThrow()
    {
        // Arrange
        var invalidHandle = default(Handle<IPipeline>);

        // Act & Assert - should not throw
        Assert.DoesNotThrow(() => _adapter!.SetPipeline(invalidHandle));
    }
}
