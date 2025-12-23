using System;
using System.Numerics;
using OpenSage.Content;
using OpenSage.Data.Map;
using OpenSage.Graphics.Rendering.Shadows;
using OpenSage.Graphics.Rendering.Water;
using OpenSage.Graphics.Shaders;
using OpenSage.Gui;
using OpenSage.Mathematics;
using Veldrid;

namespace OpenSage.Graphics.Rendering;

internal sealed class RenderPipeline : DisposableBase
{
    public event EventHandler<Rendering2DEventArgs> Rendering2D;
    public event EventHandler<BuildingRenderListEventArgs> BuildingRenderList;

    private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

    private const int ParallelCullingBatchSize = 128;

    // Clear color used when no Scene3D is present (e.g., --noshellmap mode)
    // Using a dark gray as fallback background
    private static readonly RgbaFloat ClearColor = new RgbaFloat(0.1f, 0.1f, 0.15f, 1.0f);

    public static readonly OutputDescription GameOutputDescription = new OutputDescription(
        new OutputAttachmentDescription(PixelFormat.D32_Float_S8_UInt),
        new OutputAttachmentDescription(PixelFormat.B8_G8_R8_A8_UNorm));

    private readonly RenderList _renderList;

    private readonly CommandList _commandList;

    private readonly GraphicsLoadContext _loadContext;
    private readonly GlobalShaderResources _globalShaderResources;
    private readonly GlobalShaderResourceData _globalShaderResourceData;

    internal readonly DrawingContext2D DrawingContext;

    private readonly ShadowMapRenderer _shadowMapRenderer;
    private readonly WaterMapRenderer _waterMapRenderer;

    private Texture _intermediateDepthBuffer;
    private Texture _intermediateTexture;
    private Framebuffer _intermediateFramebuffer;

    private readonly TextureCopier _textureCopier;

    public Texture ShadowMap => _shadowMapRenderer.ShadowMap;
    public Texture ReflectionMap => _waterMapRenderer.ReflectionMap;
    public Texture RefractionMap => _waterMapRenderer.RefractionMap;

    public int RenderedObjectsOpaque { get; private set; }
    public int RenderedObjectsTransparent { get; private set; }

    public RenderPipeline(IGame game)
    {
        _renderList = new RenderList();

        var graphicsDevice = game.GraphicsDevice;

        _loadContext = game.GraphicsLoadContext;

        _globalShaderResources = game.GraphicsLoadContext.ShaderResources.Global;
        _globalShaderResourceData = AddDisposable(new GlobalShaderResourceData(game.GraphicsDevice, _globalShaderResources, game.GraphicsLoadContext.StandardGraphicsResources));

        _commandList = AddDisposable(graphicsDevice.ResourceFactory.CreateCommandList());

        DrawingContext = AddDisposable(new DrawingContext2D(
            game.ContentManager,
            game.GraphicsLoadContext,
            BlendStateDescription.SingleAlphaBlend,
            GameOutputDescription));

        _shadowMapRenderer = AddDisposable(new ShadowMapRenderer(game.GraphicsDevice));
        _waterMapRenderer = AddDisposable(new WaterMapRenderer(game.AssetStore, _loadContext, game.GraphicsDevice, game.GraphicsLoadContext.ShaderResources.Global));

        _textureCopier = AddDisposable(new TextureCopier(
            game,
            game.Panel.OutputDescription));
    }

    private void EnsureIntermediateFramebuffer(GraphicsDevice graphicsDevice, Framebuffer target)
    {
        if (_intermediateDepthBuffer != null && _intermediateDepthBuffer.Width == target.Width && _intermediateDepthBuffer.Height == target.Height)
        {
            return;
        }

        RemoveAndDispose(ref _intermediateDepthBuffer);
        RemoveAndDispose(ref _intermediateTexture);
        RemoveAndDispose(ref _intermediateFramebuffer);

        _intermediateDepthBuffer = AddDisposable(graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(target.Width, target.Height, 1, 1, PixelFormat.D32_Float_S8_UInt, TextureUsage.DepthStencil)));

        _intermediateTexture = AddDisposable(graphicsDevice.ResourceFactory.CreateTexture(
            TextureDescription.Texture2D(target.Width, target.Height, 1, 1, target.ColorTargets[0].Target.Format, TextureUsage.RenderTarget | TextureUsage.Sampled)));

        _intermediateFramebuffer = AddDisposable(graphicsDevice.ResourceFactory.CreateFramebuffer(
            new FramebufferDescription(_intermediateDepthBuffer, _intermediateTexture)));
    }

    private int _logCounter = 0;

    public void Execute(RenderContext context)
    {
        RenderedObjectsOpaque = 0;
        RenderedObjectsTransparent = 0;

        EnsureIntermediateFramebuffer(context.GraphicsDevice, context.RenderTarget);

        _renderList.Clear();

        context.Scene3D?.BuildRenderList(
            _renderList,
            context.Scene3D.Camera,
            context.GameTime);

        BuildingRenderList?.Invoke(this, new BuildingRenderListEventArgs(
            _renderList,
            context.Scene3D?.Camera,
            context.GameTime));

        // PLAN-012 Stage 2: Setup material-based batching for particle systems
        // Must be called after BuildRenderList() but before rendering
        // Reduces draw calls from ~50-100 to ~15-40 by grouping systems with identical materials
        // Reference: EA Generals dx8renderer.h line 78 - TextureCategory batching
        context.Scene3D?.ParticleSystemManager.SetupBatchRendering();

        _commandList.Begin();

        if (context.Scene3D != null)
        {
            _commandList.PushDebugGroup("3D Scene");
            Render3DScene(_commandList, context.Scene3D, context);
            _commandList.PopDebugGroup();
        }
        else
        {
            Logger.Info("Scene3D is null, clearing screen with default color");
            _commandList.SetFramebuffer(_intermediateFramebuffer);
            _commandList.ClearColorTarget(0, ClearColor);
        }

        if (_logCounter++ % 60 == 0)
        {
            Logger.Info($"[RENDER] Opaque: {RenderedObjectsOpaque}, Transparent: {RenderedObjectsTransparent}");
            if (context.Scene3D != null)
            {
                var cam = context.Scene3D.Camera;
                Logger.Info($"[RENDER] Camera Pos: {cam.Position}, Target: {cam.Target}, Up: {cam.Up}");
                Logger.Info($"[RENDER] Camera Near: {cam.NearPlaneDistance}, Far: {cam.FarPlaneDistance}, FOV: {cam.FieldOfView}");
                var vp = cam.ViewProjection;
                Logger.Info($"[RENDER] VP Diagonal: {vp.M11}, {vp.M22}, {vp.M33}, {vp.M44}");
                Logger.Info($"[RENDER] VP Translation: {vp.M41}, {vp.M42}, {vp.M43}");
            }
        }

        // GUI and camera-dependent 2D elements
        {
            _commandList.PushDebugGroup("2D Scene");

            DrawingContext.Begin(
                _commandList,
                _loadContext.StandardGraphicsResources.LinearClampSampler,
                new SizeF(context.RenderTarget.Width, context.RenderTarget.Height),
                context.GameTime);

            // Render Scene2D FIRST so it appears on top
            context.Scene2D?.Render(DrawingContext);

            // Render Scene3D 2.5D overlay AFTER Scene2D so UI stays on top
            context.Scene3D?.Render(DrawingContext);

            _shadowMapRenderer.DrawDebugOverlay(
                context.Scene3D,
                DrawingContext);

            Rendering2D?.Invoke(this, new Rendering2DEventArgs(DrawingContext));

            DrawingContext.End();

            // Render camera fade overlay AFTER all 2D content so it appears on top
            context.Scene3D?.Game.Scripting.CameraFadeOverlay.Render(
                _commandList,
                new SizeF(context.RenderTarget.Width, context.RenderTarget.Height));

            _commandList.PopDebugGroup();
        }

        _commandList.End();

        context.GraphicsDevice.SubmitCommands(_commandList);

        _globalShaderResourceData.CleanupOldResourceSets();
        _waterMapRenderer.CleanupOldResourceSets();

        _textureCopier.Execute(
            _intermediateTexture,
            context.RenderTarget);
    }

    private void Render3DScene(
        CommandList commandList,
        IScene3D scene,
        RenderContext context)
    {
        Texture cloudTexture;
        if (scene.Lighting.TimeOfDay != TimeOfDay.Night
            && scene.Lighting.EnableCloudShadows
            && scene.Terrain != null)
        {
            cloudTexture = scene.Terrain.CloudTexture;
        }
        else
        {
            cloudTexture = _loadContext.StandardGraphicsResources.SolidWhiteTexture;
        }

        // Shadow map passes.

        commandList.PushDebugGroup("Shadow pass");

        _shadowMapRenderer.RenderShadowMap(
            scene,
            context.GraphicsDevice,
            commandList,
            (framebuffer, lightBoundingFrustum) =>
            {
                commandList.SetFramebuffer(framebuffer);

                commandList.ClearDepthStencil(1);

                commandList.SetFullViewports(framebuffer);

                var shadowViewProjection = lightBoundingFrustum.Matrix;
                _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, shadowViewProjection, null, null);

                DoRenderPass(context, commandList, _renderList.Shadow, lightBoundingFrustum, null);
            });

        commandList.PopDebugGroup();

        // Standard pass.

        commandList.PushDebugGroup("Forward pass");

        var shadowMap = _shadowMapRenderer.ShadowMap ?? _loadContext.StandardGraphicsResources.SolidWhiteTexture;

        var forwardPassResourceSet = _globalShaderResourceData.GetForwardPassResourceSet(
            cloudTexture,
            _shadowMapRenderer.ShadowConstantsPSBuffer,
            shadowMap);

        commandList.SetFramebuffer(_intermediateFramebuffer);

        _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, scene.Camera.ViewProjection, null, null);

        commandList.ClearColorTarget(0, ClearColor);
        commandList.ClearDepthStencil(1);

        commandList.SetFullViewports(_intermediateFramebuffer);

        var standardPassCameraFrustum = scene.Camera.BoundingFrustum;

        commandList.PushDebugGroup("Opaque");
        RenderedObjectsOpaque += DoRenderPass(context, commandList, _renderList.Opaque, standardPassCameraFrustum, forwardPassResourceSet);
        commandList.PopDebugGroup();

        commandList.PushDebugGroup("Transparent");
        RenderedObjectsTransparent = DoRenderPass(context, commandList, _renderList.Transparent, standardPassCameraFrustum, forwardPassResourceSet);
        commandList.PopDebugGroup();

        if (RenderedObjectsOpaque == 0 && RenderedObjectsTransparent == 0)
        {
            Logger.Info("No opaque or transparent objects rendered in this frame");
        }

        scene.RenderScene.Render(commandList, _globalShaderResourceData.GlobalConstantsResourceSet, forwardPassResourceSet);

        commandList.PushDebugGroup("Water");
        DoRenderPass(context, commandList, _renderList.Water, standardPassCameraFrustum, forwardPassResourceSet);
        commandList.PopDebugGroup();

        commandList.PopDebugGroup();
    }

    private void CalculateWaterShaderMap(IScene3D scene, RenderContext context, CommandList commandList, RenderItem renderItem, ResourceSet forwardPassResourceSet)
    {
        _waterMapRenderer.RenderWaterShaders(
            scene,
            context.GraphicsDevice,
            commandList,
            (reflectionFramebuffer, refractionFramebuffer) =>
            {
                var camera = scene.Camera;
                var clippingOffset = scene.Waters.ClippingOffset;
                var originalFarPlaneDistance = camera.FarPlaneDistance;
                var pivot = renderItem.World.Translation.Y;

                if (refractionFramebuffer != null)
                {
                    commandList.PushDebugGroup("Refraction");
                    camera.FarPlaneDistance = scene.Waters.RefractionRenderDistance;

                    var clippingPlaneTop = new Plane(-Vector3.UnitZ, pivot + clippingOffset);

                    var transparentWaterDepth = scene.AssetLoadContext.AssetStore.WaterTransparency.Current.TransparentWaterDepth;
                    var clippingPlaneBottom = new Plane(Vector3.UnitZ, -pivot + transparentWaterDepth);

                    // Render normal scene for water refraction shader
                    _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, camera.ViewProjection, clippingPlaneTop.AsVector4(), clippingPlaneBottom.AsVector4());

                    commandList.SetFramebuffer(refractionFramebuffer);

                    commandList.ClearColorTarget(0, ClearColor);
                    commandList.ClearDepthStencil(1);

                    commandList.SetFullViewports(refractionFramebuffer);

                    RenderedObjectsOpaque += DoRenderPass(context, commandList, _renderList.Opaque, camera.BoundingFrustum, forwardPassResourceSet, clippingPlaneTop, clippingPlaneBottom);
                    commandList.PopDebugGroup();
                }

                if (reflectionFramebuffer != null)
                {
                    commandList.PushDebugGroup("Reflection");
                    camera.FarPlaneDistance = scene.Waters.ReflectionRenderDistance;
                    var clippingPlane = new Plane(Vector3.UnitZ, -pivot - clippingOffset);

                    // TODO: Improve rendering speed somehow?
                    // ------------------- Used for creating stencil mask -------------------
                    _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, camera.ViewProjection, clippingPlane.AsVector4(), null);

                    commandList.SetFramebuffer(reflectionFramebuffer);
                    commandList.ClearColorTarget(0, ClearColor);
                    commandList.ClearDepthStencil(1);

                    commandList.SetFullViewports(reflectionFramebuffer);

                    // -----------------------------------------------------------------------

                    // Render inverted scene for water reflection shader
                    camera.SetMirrorX(pivot);
                    _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, camera.ViewProjection, clippingPlane.AsVector4(), null);

                    //commandList.SetFramebuffer(reflectionFramebuffer);
                    commandList.ClearColorTarget(0, ClearColor);
                    //commandList.ClearDepthStencil(1);

                    commandList.SetFullViewports();

                    RenderedObjectsOpaque += DoRenderPass(context, commandList, _renderList.Opaque, camera.BoundingFrustum, forwardPassResourceSet, clippingPlane);

                    camera.SetMirrorX(pivot);
                    commandList.PopDebugGroup();
                }

                if (reflectionFramebuffer != null || refractionFramebuffer != null)
                {
                    camera.FarPlaneDistance = originalFarPlaneDistance;
                    _globalShaderResourceData.UpdateGlobalConstantBuffers(commandList, context, camera.ViewProjection, null, null);

                    // Reset the render item pipeline
                    commandList.SetFramebuffer(_intermediateFramebuffer);
                    commandList.InsertDebugMarker("Setting pipeline");
                    commandList.SetPipeline(renderItem.Material.Pipeline);
                }
            });
    }

    private int DoRenderPass(
        RenderContext context,
        CommandList commandList,
        RenderBucket bucket,
        BoundingFrustum cameraFrustum,
        ResourceSet forwardPassResourceSet,
        in Plane? clippingPlane1 = null,
        in Plane? clippingPlane2 = null)
    {
        // TODO: Make culling batch size configurable at runtime
        bucket.RenderItems.CullAndSort(cameraFrustum, clippingPlane1, clippingPlane2, ParallelCullingBatchSize);

        if (bucket.RenderItems.CulledItemIndices.Count == 0)
        {
            return 0;
        }

        int? lastRenderItemIndex = null;

        foreach (var i in bucket.RenderItems.CulledItemIndices)
        {
            ref var renderItem = ref bucket.RenderItems[i];

            commandList.PushDebugGroup($"Render item: {renderItem.DebugName}");

            // For shadow pass, use a dummy ResourceSet for slot 1 since the depth shader
            // defines the layout but doesn't use the data. Metal requires all slots to be bound.
            var passResourceSet = (bucket.RenderItemName == "Shadow")
                ? _loadContext.ShaderResources.MeshDepth.DummyPassResourceSet
                : forwardPassResourceSet;

            var newMaterial = true;
            if (lastRenderItemIndex != null)
            {
                var lastMaterial = bucket.RenderItems[lastRenderItemIndex.Value].Material;

                newMaterial = lastMaterial.Pipeline != renderItem.Material.Pipeline;
            }

            if (newMaterial)
            {
                commandList.InsertDebugMarker("Setting pipeline");
                commandList.SetPipeline(renderItem.Material.Pipeline);
                SetGlobalResources(commandList, passResourceSet);
            }

            if (bucket.RenderItemName == "Water")
            {
                CalculateWaterShaderMap(context.Scene3D, context, commandList, renderItem, forwardPassResourceSet);

                SetGlobalResources(commandList, passResourceSet);
                if (_waterMapRenderer.ResourceSetForRendering != null)
                {
                    commandList.SetGraphicsResourceSet(2, _waterMapRenderer.ResourceSetForRendering);
                }
            }

            renderItem.BeforeRenderCallback?.Invoke(commandList, renderItem);

            // Metal backend requires all resource sets to be bound
            // All shaders now define a MATERIAL_CONSTANTS_RESOURCE_SET layout (slot 2)
            // If a shader doesn't use it, we still need to bind a valid ResourceSet
            if (renderItem.Material.MaterialResourceSet == null)
            {
                Logger.Warn($"[RENDER] Skipping render - MaterialResourceSet null for: {renderItem.DebugName}");
                commandList.PopDebugGroup();
                continue;
            }
            commandList.SetGraphicsResourceSet(2, renderItem.Material.MaterialResourceSet);

            if (renderItem.IndexBuffer == null)
            {
                // Skip rendering if index buffer is null
                Logger.Warn($"[RENDER] Skipping render - IndexBuffer null for: {renderItem.DebugName}");
                commandList.PopDebugGroup();
                continue;
            }

            if (renderItem.IndexCount == 0)
            {
                Logger.Warn($"[RENDER] Skipping render - IndexCount is 0 for: {renderItem.DebugName}");
                commandList.PopDebugGroup();
                continue;
            }

            Logger.Debug($"[RENDER] About to draw: {renderItem.DebugName}, IndexCount={renderItem.IndexCount}");
            
            commandList.SetIndexBuffer(renderItem.IndexBuffer, IndexFormat.UInt16);
            try
            {
                commandList.DrawIndexed(
                    renderItem.IndexCount,
                    1,
                    renderItem.StartIndex,
                    0,
                    0);
            }
            catch (NullReferenceException ex)
            {
                // Ignore ResourceSet-related NullReferenceExceptions from Metal backend
                // This occurs when Graphics resources are released before GPU finishes processing
                Logger.Error($"[RENDER] NullReferenceException during DrawIndexed for {renderItem.DebugName}: {ex.Message}");
                // Log the stack trace for debugging
                Logger.Error($"[RENDER] Stack trace: {ex.StackTrace}");
            }

            lastRenderItemIndex = i;

            commandList.PopDebugGroup();
        }

        return bucket.RenderItems.CulledItemIndices.Count;
    }

    private void SetGlobalResources(CommandList commandList, ResourceSet passResourceSet)
    {
        if (_globalShaderResourceData.GlobalConstantsResourceSet != null)
        {
            commandList.SetGraphicsResourceSet(0, _globalShaderResourceData.GlobalConstantsResourceSet);
        }

        if (passResourceSet != null)
        {
            commandList.SetGraphicsResourceSet(1, passResourceSet);
        }
    }
}

internal static class CommandListExtensions
{
    internal static void SetFullViewports(this CommandList commandList, Framebuffer framebuffer)
    {
        if (framebuffer != null)
        {
            var viewport = new Viewport(0, 0, framebuffer.Width, framebuffer.Height, 0, 1);
            commandList.SetViewport(0, viewport);
        }
    }

    internal static void SetFullViewports(this CommandList commandList)
    {
        // Default to 1920x1080 if framebuffer is not accessible
        // This is a fallback - the Render methods should pass the framebuffer explicitly
        var viewport = new Viewport(0, 0, 1920, 1080, 0, 1);
        commandList.SetViewport(0, viewport);
    }
}
