using System;
using System.Collections.Generic;
using System.Numerics;
using OpenSage.Data.Map;
using OpenSage.Graphics.Rendering;
using OpenSage.Mathematics;
using Veldrid;

namespace OpenSage.Graphics.Shaders;

internal sealed class GlobalShaderResourceData : DisposableBase
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly GlobalShaderResources _globalShaderResources;
    private readonly StandardGraphicsResources _standardGraphicsResources;

    private readonly ConstantBuffer<GlobalShaderResources.GlobalConstants> _globalConstantBuffer;

    private readonly ConstantBuffer<GlobalShaderResources.LightingConstantsVS> _globalLightingBufferVS;
    private readonly ConstantBuffer<GlobalShaderResources.LightingConstantsPS> _globalLightingBufferPS;

    public readonly ResourceSet GlobalConstantsResourceSet;

    private ResourceSet _forwardPassResourceSet;
    private Texture _cachedCloudTexture;
    private Texture _cachedShadowMap;

    // Triple buffering: Keep ResourceSets from previous 2 frames alive to avoid use-after-free in Metal
    private readonly Queue<ResourceSet> _deferredCleanup = new Queue<ResourceSet>();
    private const int DeferredCleanupFrames = 2;

    private TimeOfDay? _cachedTimeOfDay;

    public GlobalShaderResourceData(
        GraphicsDevice graphicsDevice,
        GlobalShaderResources globalShaderResources,
        StandardGraphicsResources standardGraphicsResources)
    {
        _graphicsDevice = graphicsDevice;
        _globalShaderResources = globalShaderResources;
        _standardGraphicsResources = standardGraphicsResources;

        _globalConstantBuffer = AddDisposable(new ConstantBuffer<GlobalShaderResources.GlobalConstants>(graphicsDevice, "GlobalConstants"));

        GlobalConstantsResourceSet = AddDisposable(graphicsDevice.ResourceFactory.CreateResourceSet(
            new ResourceSetDescription(
                globalShaderResources.GlobalConstantsResourceLayout,
                _globalConstantBuffer.Buffer)));

        _globalLightingBufferVS = AddDisposable(new ConstantBuffer<GlobalShaderResources.LightingConstantsVS>(graphicsDevice, "GlobalLightingConstantsVS"));
        SetGlobalLightingBufferVS(graphicsDevice);

        _globalLightingBufferPS = AddDisposable(new ConstantBuffer<GlobalShaderResources.LightingConstantsPS>(graphicsDevice, "GlobalLightingConstantsPS"));
    }

    public ResourceSet GetForwardPassResourceSet(
        Texture cloudTexture,
        ConstantBuffer<GlobalShaderResources.ShadowConstantsPS> shadowConstantsPSBuffer,
        Texture shadowMap)
    {
        // Always create the ResourceSet on first call, or when textures change
        if (_forwardPassResourceSet == null || _cachedCloudTexture != cloudTexture || shadowMap != _cachedShadowMap)
        {
            _cachedCloudTexture = cloudTexture;
            _cachedShadowMap = shadowMap;

            // Remove old ResourceSet from managed disposal and queue for deferred cleanup
            if (_forwardPassResourceSet != null)
            {
                _deferredCleanup.Enqueue(_forwardPassResourceSet);
                RemoveToDispose(_forwardPassResourceSet);
            }

            _forwardPassResourceSet = AddDisposable(
                _graphicsDevice.ResourceFactory.CreateResourceSet(
                    new ResourceSetDescription(
                        _globalShaderResources.ForwardPassResourceLayout,
                        _globalLightingBufferVS.Buffer,
                        _globalLightingBufferPS.Buffer,
                        cloudTexture,
                        _graphicsDevice.Aniso4xSampler,
                        shadowConstantsPSBuffer.Buffer,
                        shadowMap,
                        _globalShaderResources.ShadowSampler,
                        _globalShaderResources.RadiusCursorDecals.TextureArray,
                        _standardGraphicsResources.Aniso4xClampSampler,
                        _globalShaderResources.RadiusCursorDecals.DecalConstants,
                        _globalShaderResources.RadiusCursorDecals.DecalsBuffer)));
        }

        return _forwardPassResourceSet;
    }

    public void CleanupOldResourceSets()
    {
        // Clean up ResourceSets that were deferred - GPU should be idle by now
        while (_deferredCleanup.Count > 0)
        {
            var rs = _deferredCleanup.Dequeue();
            rs.Dispose();
        }
    }

    private void SetGlobalLightingBufferVS(GraphicsDevice graphicsDevice)
    {
        var cloudShadowView = Matrix4x4.CreateLookAt(
            Vector3.Zero,
            Vector3.Normalize(new Vector3(0, 0.2f, -1)),
            Vector3.UnitY);

        var cloudShadowProjection = Matrix4x4.CreateOrthographic(1, 1, 0, 1);

        var lightingConstantsVS = new GlobalShaderResources.LightingConstantsVS
        {
            CloudShadowMatrix = cloudShadowView * cloudShadowProjection
        };

        _globalLightingBufferVS.Value = lightingConstantsVS;
        _globalLightingBufferVS.Update(graphicsDevice);
    }

    public void UpdateGlobalConstantBuffers(
        CommandList commandList,
        RenderContext context,
        in Matrix4x4 viewProjection,
        in Vector4? clippingPlane1,
        in Vector4? clippingPlane2)
    {
        var cameraPosition = Matrix4x4Utility.Invert(context.Scene3D.Camera.View).Translation;
        _globalConstantBuffer.Value.CameraPosition = cameraPosition;

        _globalConstantBuffer.Value.TimeInSeconds = (float)context.GameTime.TotalTime.TotalSeconds;

        _globalConstantBuffer.Value.ViewProjection = viewProjection;

        _globalConstantBuffer.Value.ClippingPlane1 = clippingPlane1 ?? Vector4.Zero;
        _globalConstantBuffer.Value.ClippingPlane2 = clippingPlane2 ?? Vector4.Zero;

        _globalConstantBuffer.Value.HasClippingPlane1 = clippingPlane1 != null;
        _globalConstantBuffer.Value.HasClippingPlane2 = clippingPlane2 != null;

        _globalConstantBuffer.Value.ViewportSize = new Vector2(context.RenderTarget.Width, context.RenderTarget.Height);

        _globalConstantBuffer.Update(commandList);

        if (_cachedTimeOfDay != context.Scene3D.Lighting.TimeOfDay)
        {
            _globalLightingBufferPS.Value = context.Scene3D.Lighting.CurrentLightingConfiguration.LightsPS;
            _globalLightingBufferPS.Update(commandList);

            _cachedTimeOfDay = context.Scene3D.Lighting.TimeOfDay;
        }
    }
}
