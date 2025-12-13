using System;
using System.Runtime.InteropServices;

namespace OpenSage.Graphics.BGFX.Native;

/// <summary>
/// BGFX P/Invoke Native Bindings
/// Cross-platform graphics API-agnostic rendering library
/// </summary>
public static class BgfxNative
{
    private const string DllName = "bgfx";

    // ============================================
    // ENUMS
    // ============================================

    /// <summary>
    /// Renderer type enumeration
    /// </summary>
    public enum RendererType : ushort
    {
        Metal = 0,
        Vulkan = 1,
        Direct3D11 = 2,
        Direct3D12 = 3,
        Direct3D9 = 4,
        OpenGL = 5,
        OpenGLES = 6,
        WebGL = 7,
        Count = 8,
    }

    /// <summary>
    /// Texture format enumeration
    /// </summary>
    public enum TextureFormat : ushort
    {
        BC1 = 0,
        BC2 = 1,
        BC3 = 2,
        BC4 = 3,
        BC5 = 4,
        BC6H = 5,
        BC7 = 6,
        ETC1 = 7,
        ETC2 = 8,
        ETC2A = 9,
        ETC2A1 = 10,
        PTC12 = 11,
        PTC14 = 12,
        PTC12A = 13,
        PTC14A = 14,
        PTC22 = 15,
        PTC24 = 16,
        ATC = 17,
        ATCE = 18,
        ATCI = 19,
        ASTC4x4 = 20,
        ASTC5x5 = 21,
        ASTC6x6 = 22,
        ASTC8x5 = 23,
        ASTC8x6 = 24,
        ASTC8x8 = 25,
        ASTC10x5 = 26,
        ASTC10x6 = 27,
        ASTC10x8 = 28,
        ASTC10x10 = 29,
        ASTC12x10 = 30,
        ASTC12x12 = 31,
        UNKNOWN = 32,
        R1 = 33,
        A8 = 34,
        R8 = 35,
        R8I = 36,
        R8U = 37,
        R8S = 38,
        R16 = 39,
        R16I = 40,
        R16U = 41,
        R16F = 42,
        R16S = 43,
        RG8 = 44,
        RG8I = 45,
        RG8U = 46,
        RG8S = 47,
        RGB8 = 48,
        RGB8I = 49,
        RGB8U = 50,
        RGB8S = 51,
        RGB9E5F = 52,
        BGRA8 = 53,
        RGBA8 = 54,
        RGBA8I = 55,
        RGBA8U = 56,
        RGBA8S = 57,
        RGBA16 = 58,
        RGBA16I = 59,
        RGBA16U = 60,
        RGBA16F = 61,
        RGBA16S = 62,
        R32I = 63,
        R32U = 64,
        R32F = 65,
        RG32I = 66,
        RG32U = 67,
        RG32F = 68,
        RGB32I = 69,
        RGB32U = 70,
        RGB32F = 71,
        RGBA32I = 72,
        RGBA32U = 73,
        RGBA32F = 74,
        R64F = 75,
        RG64F = 76,
        RGB64F = 77,
        RGBA64F = 78,
        Depth16 = 79,
        Depth24 = 80,
        Depth24S8 = 81,
        Depth32F = 82,
        D16 = 83,
        D24 = 84,
        D24S8 = 85,
        D32 = 86,
        D0S8 = 87,
    }

    /// <summary>
    /// Vertex attribute enumeration
    /// </summary>
    public enum VertexAttribute : byte
    {
        Position = 0,
        Normal = 1,
        Tangent = 2,
        Bitangent = 3,
        Color0 = 4,
        Color1 = 5,
        Color2 = 6,
        Color3 = 7,
        Indices = 8,
        Weight = 9,
        TexCoord0 = 10,
        TexCoord1 = 11,
        TexCoord2 = 12,
        TexCoord3 = 13,
        TexCoord4 = 14,
        TexCoord5 = 15,
        TexCoord6 = 16,
        TexCoord7 = 17,
        Count = 18,
    }

    /// <summary>
    /// Vertex attribute type enumeration
    /// </summary>
    public enum VertexAttributeType : byte
    {
        Uint8 = 0,
        Int16 = 1,
        Half = 2,
        Float = 3,
        Count = 4,
    }

    /// <summary>
    /// Uniform type enumeration
    /// </summary>
    public enum UniformType : ushort
    {
        Sampler = 0,
        End = 1,
        Vec4 = 2,
        Mat3 = 3,
        Mat4 = 4,
        Count = 5,
    }

    // ============================================
    // STRUCTS
    // ============================================

    /// <summary>
    /// Init settings structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct InitSettings
    {
        public IntPtr Type;  // bgfx_renderer_type_t*
        public uint VendorId;
        public uint DeviceId;
        public ulong Capabilities;
        public byte Debug;
        public byte Profile;
    }

    /// <summary>
    /// Capabilities structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Capabilities
    {
        public RendererType RendererType;
        public uint VendorId;
        public uint DeviceId;
        public ulong Formats;
    }

    /// <summary>
    /// Platform data structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct PlatformData
    {
        public IntPtr Ndt;        // Native Display Type (X11 Display, Wayland Display, etc)
        public IntPtr Nwh;        // Native Window Handle (X11 Window, Wayland Surface, etc)
        public IntPtr Context;    // GL context or D3D device
        public IntPtr BackBuffer; // SwapChain or MTLDrawable
        public IntPtr BackBufferDS; // Backbuffer depth/stencil
    }

    /// <summary>
    /// Memory structure
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct Memory
    {
        public IntPtr Data;
        public uint Size;
    }

    /// <summary>
    /// Transient vertex buffer
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TransientVertexBuffer
    {
        public IntPtr Data;
        public uint Size;
        public ushort StartVertex;
        public ushort Stride;
        public ushort Handle;
        public ushort Decl;
    }

    /// <summary>
    /// Transient index buffer
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct TransientIndexBuffer
    {
        public IntPtr Data;
        public uint Size;
        public uint StartIndex;
        public ushort Handle;
    }

    // ============================================
    // HANDLE TYPES
    // ============================================

    public struct VertexBufferHandle { public ushort idx; }
    public struct IndexBufferHandle { public ushort idx; }
    public struct VertexDeclHandle { public ushort idx; }
    public struct TextureHandle { public ushort idx; }
    public struct FrameBufferHandle { public ushort idx; }
    public struct UniformHandle { public ushort idx; }
    public struct ShaderHandle { public ushort idx; }
    public struct ProgramHandle { public ushort idx; }
    public struct SamplerHandle { public ushort idx; }
    public struct IndirectBufferHandle { public ushort idx; }

    // ============================================
    // INITIALIZATION & LIFECYCLE
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_init_ctor(ref InitSettings init);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte bgfx_init(ref InitSettings init);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_shutdown();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern byte bgfx_get_reset_flags();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern RendererType bgfx_get_renderer_type();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_get_caps();  // Returns Capabilities*

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_platform_data(ref PlatformData pd);

    // ============================================
    // FRAME SUBMISSION
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint bgfx_frame(byte capture);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_render_frame(int msecs);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_encoder_begin(byte forThread);  // Returns bgfx_encoder_t*

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_encoder_end(IntPtr encoder);

    // ============================================
    // VERTEX LAYOUT
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_vertex_layout_create();

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_vertex_layout_begin(IntPtr layout, RendererType rendererType);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_vertex_layout_add(
        IntPtr layout,
        VertexAttribute attrib,
        byte num,
        VertexAttributeType type,
        byte normalized,
        byte asInt);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_vertex_layout_end(IntPtr layout);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_vertex_layout_destroy(IntPtr layout);

    // ============================================
    // BUFFERS
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern VertexBufferHandle bgfx_create_vertex_buffer(
        ref Memory mem,
        IntPtr layout,
        ushort flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IndexBufferHandle bgfx_create_index_buffer(
        ref Memory mem,
        ushort flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_vertex_buffer(VertexBufferHandle handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_index_buffer(IndexBufferHandle handle);

    // ============================================
    // TEXTURES
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern TextureHandle bgfx_create_texture_2d(
        ushort width,
        ushort height,
        byte hasMips,
        ushort numLayers,
        TextureFormat format,
        ulong flags,
        IntPtr mem);  // Memory*

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_texture(TextureHandle handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern uint bgfx_read_texture(TextureHandle handle, IntPtr data, byte mip);

    // ============================================
    // MEMORY MANAGEMENT
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_alloc(uint size);  // Returns Memory*

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_copy(IntPtr data, uint size);  // Returns Memory*

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr bgfx_make_ref(IntPtr data, uint size);  // Returns Memory*

    // ============================================
    // SHADERS & PROGRAMS
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ShaderHandle bgfx_create_shader(ref Memory mem);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern ProgramHandle bgfx_create_program(
        ShaderHandle vsh,
        ShaderHandle fsh,
        byte destroyShaders);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_shader(ShaderHandle handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_program(ProgramHandle handle);

    // ============================================
    // UNIFORMS
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern UniformHandle bgfx_create_uniform(
        [MarshalAs(UnmanagedType.LPStr)] string name,
        UniformType type,
        ushort num);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_uniform(UniformHandle handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_uniform(
        UniformHandle handle,
        IntPtr value,
        ushort num);

    // ============================================
    // FRAMEBUFFERS
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern FrameBufferHandle bgfx_create_frame_buffer(
        ushort width,
        ushort height,
        TextureFormat format,
        ulong textureFlags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_destroy_frame_buffer(FrameBufferHandle handle);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern TextureHandle bgfx_get_texture(FrameBufferHandle handle, byte attachment);

    // ============================================
    // VIEW MANAGEMENT
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_view_name(byte id, [MarshalAs(UnmanagedType.LPStr)] string name);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_view_rect(byte id, ushort x, ushort y, ushort width, ushort height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_view_scissor(byte id, ushort x, ushort y, ushort width, ushort height);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_view_clear(
        byte id,
        ushort flags,
        uint rgba,
        float depth,
        byte stencil);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_view_frame_buffer(byte id, FrameBufferHandle handle);

    // ============================================
    // RENDERING STATE
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_state(ulong state, uint rgba);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_transform(IntPtr matrix, ushort num);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_vertex_buffer(byte stream, VertexBufferHandle handle, uint startVertex, uint numVertices);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_index_buffer(IndexBufferHandle handle, uint firstIndex, uint numIndices);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_texture(
        byte stage,
        SamplerHandle sampler,
        TextureHandle texture,
        uint flags);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_set_sampler(
        byte stage,
        SamplerHandle sampler,
        uint flags);

    // ============================================
    // DRAW CALLS
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_submit(byte id, ProgramHandle program, uint depth, byte preserveState);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_submit_indirect(
        byte id,
        ProgramHandle program,
        IndirectBufferHandle indirectHandle,
        ushort start,
        ushort num,
        uint depth,
        byte preserveState);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_touch(byte id);

    // ============================================
    // DEBUG / PROFILING
    // ============================================

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_dbg_text_clear(byte attr, byte bg);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_dbg_text_printf(ushort x, ushort y, byte attr, [MarshalAs(UnmanagedType.LPStr)] string format);

    [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void bgfx_dbg_text_image(ushort x, ushort y, ushort width, ushort height, IntPtr data, ushort pitch);
}
