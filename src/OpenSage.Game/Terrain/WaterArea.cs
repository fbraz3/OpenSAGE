using System;
using System.Linq;
using System.Numerics;
using OpenSage.Content.Loaders;
using OpenSage.Data.Map;
using OpenSage.Graphics.Rendering;
using OpenSage.Graphics.Shaders;
using OpenSage.Logic.Map;
using OpenSage.Mathematics;
using OpenSage.Rendering;
using OpenSage.Utilities;
using OpenSage.Utilities.Extensions;
using Veldrid;

namespace OpenSage.Terrain;

public sealed class WaterArea : DisposableBase
{
    private readonly string _debugName;

    private DeviceBuffer _vertexBuffer;
    private AxisAlignedBoundingBox _boundingBox;

    private DeviceBuffer _indexBuffer;
    private uint _numIndices;

    private readonly ShaderSet _shaderSet;
    private readonly Pipeline _pipeline;

    private readonly Material _material;
    private WaveSimulation _waveSimulation;
    private StandingWaveArea _waveAreaData;

    // Wave animation GPU resources
    private DeviceBuffer _waveConstantsBuffer;
    private ResourceSet _waveResourceSet;
    private readonly GraphicsDevice _graphicsDevice;

    private readonly BeforeRenderDelegate _beforeRender;
    private Matrix4x4 _world;

    internal static bool TryCreate(
        AssetLoadContext loadContext,
        PolygonTrigger trigger,
        out WaterArea result)
    {
        if (trigger.Points.Length < 3)
        {
            // Some maps (such as Training01) have water areas with fewer than 3 points.
            result = null;
            return false;
        }

        result = new WaterArea(loadContext, trigger);
        return true;
    }

    internal static bool TryCreate(
        AssetLoadContext loadContext,
        StandingWaterArea area,
        out WaterArea result)
    {
        if (area.Points.Length < 3)
        {
            // Some maps (such as Training01) have water areas with fewer than 3 points.
            result = null;
            return false;
        }

        result = new WaterArea(loadContext, area);
        return true;
    }

    internal static bool TryCreate(
        AssetLoadContext loadContext,
        StandingWaveArea area,
        out WaterArea result)
    {
        if (area.Points.Length < 3)
        {
            // Some maps (such as Training01) have water areas with fewer than 3 points.
            result = null;
            return false;
        }

        result = new WaterArea(loadContext, area);
        return true;
    }

    private void CreateGeometry(AssetLoadContext loadContext,
                            Vector2[] points, uint height)
    {
        Triangulator.Triangulate(
            points,
            WindingOrder.CounterClockwise,
            out var trianglePoints,
            out var triangleIndices);

        var vertices = trianglePoints
            .Select(x =>
                new WaterShaderResources.WaterVertex
                {
                    Position = new Vector3(x.X, x.Y, height)
                })
            .ToArray();

        _boundingBox = AxisAlignedBoundingBox.CreateFromPoints(vertices.Select(x => x.Position));

        _vertexBuffer = AddDisposable(loadContext.GraphicsDevice.CreateStaticBuffer(
            vertices,
            BufferUsage.VertexBuffer));

        _numIndices = (uint)triangleIndices.Length;

        _indexBuffer = AddDisposable(loadContext.GraphicsDevice.CreateStaticBuffer(
            triangleIndices,
            BufferUsage.IndexBuffer));

        _world = Matrix4x4.Identity;
        _world.Translation = new Vector3(0, height, 0);
    }

    private WaterArea(AssetLoadContext loadContext, string debugName)
    {
        _shaderSet = loadContext.ShaderResources.Water;
        _pipeline = loadContext.ShaderResources.Water.Pipeline;
        _graphicsDevice = loadContext.GraphicsDevice;

        _material = AddDisposable(
            new Material(
                _shaderSet,
                _pipeline,
                null,
                SurfaceType.Transparent)); // TODO: MaterialResourceSet

        _debugName = debugName;

        _beforeRender = (CommandList cl, in RenderItem renderItem) =>
        {
            cl.SetVertexBuffer(0, _vertexBuffer);
            
            // Bind wave animation constants if available
            if (_waveResourceSet != null)
            {
                cl.SetGraphicsResourceSet(1, _waveResourceSet);
            }
        };
    }

    private WaterArea(
        AssetLoadContext loadContext,
        StandingWaveArea area) : this(loadContext, area.Name)
    {
        CreateGeometry(loadContext, area.Points, area.FinalHeight);
        _waveAreaData = area;
        _waveSimulation = AddDisposable(new WaveSimulation());
        
        // Initialize wave constants buffer for GPU sync
        // Reference: EA W3DWater.cpp - WaterRenderObjClass::ReAcquireResources()
        InitializeWaveConstantsBuffer(loadContext);
    }

    private WaterArea(
        AssetLoadContext loadContext,
        StandingWaterArea area) : this(loadContext, area.Name)
    {
        CreateGeometry(loadContext, area.Points, area.WaterHeight);
        // TODO: use depthcolors
        // TODO: use FXShader?
    }

    private WaterArea(
        AssetLoadContext loadContext,
        PolygonTrigger trigger) : this(loadContext, trigger.Name)
    {
        var triggerPoints = trigger.Points
            .Select(x => new Vector2(x.X, x.Y))
            .ToArray();

        CreateGeometry(loadContext, triggerPoints, (uint)trigger.Points[0].Z);
    }

    private void InitializeWaveConstantsBuffer(AssetLoadContext loadContext)
    {
        // Create uniform buffer for wave animation constants
        // Size: 32 * Vector4 (waves) + 4 uints for metadata = 512 + 16 = 528 bytes, aligned to 256
        _waveConstantsBuffer = AddDisposable(loadContext.GraphicsDevice.ResourceFactory.CreateBuffer(
            new BufferDescription(
                512, // 32 waves * 16 bytes per Vector4
                BufferUsage.UniformBuffer)));

        var waterShaderResources = (WaterShaderResources)_shaderSet;
        _waveResourceSet = AddDisposable(loadContext.GraphicsDevice.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(
                waterShaderResources.WaveAnimationLayout,
                _waveConstantsBuffer)));
    }

    internal void BuildRenderList(RenderList renderList)
    {
        renderList.Water.RenderItems.Add(new RenderItem(
            _debugName,
            _material,
            _boundingBox,
            _world,
            0,
            _numIndices,
            _indexBuffer,
            _beforeRender));
    }

    internal void Update(float deltaTime)
    {
        if (_waveSimulation != null)
        {
            _waveSimulation.Update(deltaTime);
            
            // Sync wave data to GPU
            SyncWaveConstantsToGPU();
        }
    }

    private void SyncWaveConstantsToGPU()
    {
        if (_waveConstantsBuffer == null || _waveSimulation == null)
            return;

        var activeWaves = _waveSimulation.GetActiveWaves();
        
        // Build wave data for GPU
        // Each wave: x, y = position, z = radius, w = alpha
        var waveData = new Vector4[32];
        for (int i = 0; i < Math.Min(activeWaves.Length, 32); i++)
        {
            var wave = activeWaves[i];
            waveData[i] = new Vector4(
                wave.Position.X,
                wave.Position.Y,
                Math.Max(wave.CurrentWidth, wave.CurrentHeight),  // Use largest dimension as radius
                wave.Alpha);
        }

        // Update buffer with wave data
        // Reference: EA W3DWater.cpp - Wave vertex deformation in shader
        _graphicsDevice.UpdateBuffer(_waveConstantsBuffer, 0, waveData);
    }

    internal WaveSimulation GetWaveSimulation() => _waveSimulation;
}
