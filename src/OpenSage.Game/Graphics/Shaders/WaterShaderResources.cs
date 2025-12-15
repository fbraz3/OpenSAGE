using System;
using System.Numerics;
using OpenSage.Graphics.Rendering;
using OpenSage.Rendering;
using Veldrid;

namespace OpenSage.Graphics.Shaders;

internal sealed class WaterShaderResources : ShaderSetBase
{
    public readonly ResourceLayout WaterResourceLayout;
    public readonly ResourceLayout WaveAnimationLayout;

    public readonly Pipeline Pipeline;

    /// <summary>
    /// Wave animation constants buffer for GPU.
    /// Contains active wave positions, sizes, and alpha values.
    /// Reference: EA W3DWater.cpp - Wave deformation system for standing water areas
    /// </summary>
    public struct WaveAnimationConstants
    {
        // Maximum 32 active waves per water area (matches shader)
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public struct WaveData
        {
            // x, y = wave position, z = wave radius, w = alpha
            public Vector4 WavePositionAndAlpha;
        }

        public const int MaxWaves = 32;

        // Array of active waves
        public WaveData[] Waves;

        // Number of active waves (0-32)
        public uint WaveCount;

        // Time for animation (unused for now, could be used for additional effects)
        public float Time;

        public WaveAnimationConstants()
        {
            Waves = new WaveData[MaxWaves];
            WaveCount = 0;
            Time = 0.0f;
        }
    }

    public WaterShaderResources(ShaderSetStore store)
        : base(store, "Water", WaterVertex.VertexDescriptor)
    {
        WaterResourceLayout = ResourceLayouts[2];
        
        // Wave animation layout: uniform buffer for wave data passed to vertex shader
        WaveAnimationLayout = ResourceLayouts[1];

        Pipeline = AddDisposable(store.GraphicsDevice.ResourceFactory.CreateGraphicsPipeline(
            new GraphicsPipelineDescription(
                BlendStateDescription.SingleAlphaBlend,
                DepthStencilStateDescription.DepthOnlyLessEqualRead,
                RasterizerStateDescriptionUtility.DefaultFrontIsCounterClockwise,
                PrimitiveTopology.TriangleList,
                Description,
                ResourceLayouts,
                RenderPipeline.GameOutputDescription)));
    }

    public struct WaterVertex
    {
        public Vector3 Position;

        public static readonly VertexLayoutDescription VertexDescriptor = new VertexLayoutDescription(
            new VertexElementDescription("POSITION", VertexElementSemantic.TextureCoordinate, VertexElementFormat.Float3));
    }
}
