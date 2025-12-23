using OpenSage.Graphics.Rendering.Shadows;
using OpenSage.Rendering;
using Veldrid;

namespace OpenSage.Graphics.Shaders;

internal sealed class MeshDepthShaderResources : ShaderSetBase
{
    public readonly Material Material;
    public readonly ResourceSet DummyPassResourceSet;

    public MeshDepthShaderResources(
        ShaderSetStore store)
        : base(store, "MeshDepth", MeshShaderResources.MeshVertex.VertexDescriptors)
    {
        var depthRasterizerState = RasterizerStateDescriptionUtility.DefaultFrontIsCounterClockwise;
        depthRasterizerState.DepthClipEnabled = false;
        depthRasterizerState.ScissorTestEnabled = false;

        var pipeline = AddDisposable(
            GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(
                new GraphicsPipelineDescription(
                    BlendStateDescription.SingleDisabled,
                    DepthStencilStateDescription.DepthOnlyLessEqual,
                    depthRasterizerState,
                    PrimitiveTopology.TriangleList,
                    Description,
                    ResourceLayouts,
                    ShadowData.DepthPassDescription)));

        // Create dummy material resources for the depth shader
        // The shader defines the material layout but doesn't use it (depth-only rendering)
        // We need actual ResourceSet objects to satisfy the pipeline binding
        var dummyResourceSet = CreateDummyMaterialResourceSet();

        // Create dummy pass resources for the depth shader (set 1)
        // Metal requires all resource sets defined in the pipeline to be bound
        DummyPassResourceSet = CreateDummyPassResourceSet();

        Material = AddDisposable(
            new Material(
                this,
                pipeline,
                dummyResourceSet,
                SurfaceType.Opaque));
    }

    private ResourceSet CreateDummyPassResourceSet()
    {
        // Create dummy resources matching the pass layout defined in MeshDepth.frag
        // MeshDepth doesn't use these, but the pipeline requires all slots to be bound
        var factory = GraphicsDevice.ResourceFactory;

        var dummyBuffer0 = AddDisposable(
            factory.CreateBuffer(
                new BufferDescription(
                    16, // Minimum for a uniform buffer (one vec4)
                    BufferUsage.UniformBuffer)));

        var dummyBuffer1 = AddDisposable(
            factory.CreateBuffer(
                new BufferDescription(
                    16,
                    BufferUsage.UniformBuffer)));

        // Get the pass resource layout (slot 1)
        var passLayout = ResourceLayouts[1];

        var dummyResourceSet = AddDisposable(
            factory.CreateResourceSet(
                new ResourceSetDescription(
                    passLayout,
                    dummyBuffer0,
                    dummyBuffer1)));

        return dummyResourceSet;
    }

    private ResourceSet CreateDummyMaterialResourceSet()
    {
        // Create dummy resources matching the material layout defined in MeshDepth.frag
        // MeshDepth doesn't use these, but the pipeline requires all slots to be bound
        var factory = GraphicsDevice.ResourceFactory;

        var dummyBuffer = AddDisposable(
            factory.CreateBuffer(
                new BufferDescription(
                    16, // Minimum for a uniform buffer (one vec4)
                    BufferUsage.UniformBuffer)));

        var dummyTexture = AddDisposable(
            factory.CreateTexture(
                new TextureDescription(
                    1, 1, 1, 1, 1, PixelFormat.R8_G8_B8_A8_UNorm,
                    TextureUsage.Sampled, TextureType.Texture2D)));

        var dummyResourceSet = AddDisposable(
            factory.CreateResourceSet(
                new ResourceSetDescription(
                    MaterialResourceLayout,
                    dummyBuffer,
                    dummyTexture,
                    dummyTexture,
                    GraphicsDevice.Aniso4xSampler)));

        return dummyResourceSet;
    }
}
