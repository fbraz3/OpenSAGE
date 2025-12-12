using System;

namespace OpenSage.Graphics.State;

/// <summary>
/// Specifies the fill mode for rasterization.
/// </summary>
public enum FillMode
{
    /// <summary>
    /// Fill triangles (default).
    /// </summary>
    Solid,

    /// <summary>
    /// Draw wireframe.
    /// </summary>
    Wireframe,
}

/// <summary>
/// Specifies the face culling mode.
/// </summary>
public enum CullMode
{
    /// <summary>
    /// No culling.
    /// </summary>
    None,

    /// <summary>
    /// Cull front-facing triangles.
    /// </summary>
    Front,

    /// <summary>
    /// Cull back-facing triangles.
    /// </summary>
    Back,
}

/// <summary>
/// Specifies vertex winding order for front-facing determination.
/// </summary>
public enum FrontFace
{
    /// <summary>
    /// Counter-clockwise winding is front-facing (default, OpenGL convention).
    /// </summary>
    CounterClockwise,

    /// <summary>
    /// Clockwise winding is front-facing (Direct3D convention).
    /// </summary>
    Clockwise,
}

/// <summary>
/// Immutable rasterization state.
/// Controls how triangles are converted to pixels.
/// </summary>
public readonly struct RasterState : IEquatable<RasterState>
{
    /// <summary>
    /// Gets the fill mode (solid or wireframe).
    /// </summary>
    public FillMode FillMode { get; }

    /// <summary>
    /// Gets the face culling mode.
    /// </summary>
    public CullMode CullMode { get; }

    /// <summary>
    /// Gets the front-facing winding order.
    /// </summary>
    public FrontFace FrontFace { get; }

    /// <summary>
    /// Gets a value indicating whether depth clamping is enabled.
    /// </summary>
    public bool DepthClamp { get; }

    /// <summary>
    /// Gets a value indicating whether scissor testing is enabled.
    /// </summary>
    public bool ScissorTest { get; }

    /// <summary>
    /// Gets the predefined solid rasterization state (default).
    /// </summary>
    public static RasterState Solid => new(FillMode.Solid, CullMode.Back, FrontFace.CounterClockwise, false, false);

    /// <summary>
    /// Gets the predefined wireframe rasterization state.
    /// </summary>
    public static RasterState Wireframe => new(FillMode.Wireframe, CullMode.None, FrontFace.CounterClockwise, false, false);

    /// <summary>
    /// Gets the predefined no-cull rasterization state.
    /// </summary>
    public static RasterState NoCull => new(FillMode.Solid, CullMode.None, FrontFace.CounterClockwise, false, false);

    /// <summary>
    /// Initializes a new instance of the <see cref="RasterState"/> struct.
    /// </summary>
    public RasterState(
        FillMode fillMode = FillMode.Solid,
        CullMode cullMode = CullMode.Back,
        FrontFace frontFace = FrontFace.CounterClockwise,
        bool depthClamp = false,
        bool scissorTest = false)
    {
        FillMode = fillMode;
        CullMode = cullMode;
        FrontFace = frontFace;
        DepthClamp = depthClamp;
        ScissorTest = scissorTest;
    }

    /// <summary>
    /// Determines whether the specified <see cref="RasterState"/> is equal to the current state.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is RasterState other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="RasterState"/> is equal to the current state.
    /// </summary>
    public bool Equals(RasterState other)
    {
        return FillMode == other.FillMode &&
               CullMode == other.CullMode &&
               FrontFace == other.FrontFace &&
               DepthClamp == other.DepthClamp &&
               ScissorTest == other.ScissorTest;
    }

    /// <summary>
    /// Returns the hash code for this state.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(FillMode, CullMode, FrontFace, DepthClamp, ScissorTest);
    }

    /// <summary>
    /// Determines whether two raster states are equal.
    /// </summary>
    public static bool operator ==(RasterState left, RasterState right) => left.Equals(right);

    /// <summary>
    /// Determines whether two raster states are not equal.
    /// </summary>
    public static bool operator !=(RasterState left, RasterState right) => !left.Equals(right);
}

/// <summary>
/// Specifies depth comparison function.
/// </summary>
public enum CompareFunction
{
    /// <summary>
    /// Never passes (always false).
    /// </summary>
    Never,

    /// <summary>
    /// Passes if new value is less than stored value.
    /// </summary>
    Less,

    /// <summary>
    /// Passes if values are equal.
    /// </summary>
    Equal,

    /// <summary>
    /// Passes if new value is less than or equal to stored value.
    /// </summary>
    LessEqual,

    /// <summary>
    /// Passes if new value is greater than stored value.
    /// </summary>
    Greater,

    /// <summary>
    /// Passes if values are not equal.
    /// </summary>
    NotEqual,

    /// <summary>
    /// Passes if new value is greater than or equal to stored value.
    /// </summary>
    GreaterEqual,

    /// <summary>
    /// Always passes (true).
    /// </summary>
    Always,
}

/// <summary>
/// Immutable depth testing state.
/// </summary>
public readonly struct DepthState : IEquatable<DepthState>
{
    /// <summary>
    /// Gets a value indicating whether depth testing is enabled.
    /// </summary>
    public bool TestEnabled { get; }

    /// <summary>
    /// Gets a value indicating whether depth writing is enabled.
    /// </summary>
    public bool WriteEnabled { get; }

    /// <summary>
    /// Gets the depth comparison function.
    /// </summary>
    public CompareFunction CompareFunction { get; }

    /// <summary>
    /// Gets the predefined disabled depth state.
    /// </summary>
    public static DepthState Disabled => new(false, false, CompareFunction.Always);

    /// <summary>
    /// Gets the predefined default depth state (test enabled, less-equal, write enabled).
    /// </summary>
    public static DepthState Default => new(true, true, CompareFunction.Less);

    /// <summary>
    /// Gets the predefined depth read-only state (test enabled, no write).
    /// </summary>
    public static DepthState ReadOnly => new(true, false, CompareFunction.Less);

    /// <summary>
    /// Initializes a new instance of the <see cref="DepthState"/> struct.
    /// </summary>
    public DepthState(
        bool testEnabled = true,
        bool writeEnabled = true,
        CompareFunction compareFunction = CompareFunction.Less)
    {
        TestEnabled = testEnabled;
        WriteEnabled = writeEnabled;
        CompareFunction = compareFunction;
    }

    /// <summary>
    /// Determines whether the specified <see cref="DepthState"/> is equal to the current state.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is DepthState other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="DepthState"/> is equal to the current state.
    /// </summary>
    public bool Equals(DepthState other)
    {
        return TestEnabled == other.TestEnabled &&
               WriteEnabled == other.WriteEnabled &&
               CompareFunction == other.CompareFunction;
    }

    /// <summary>
    /// Returns the hash code for this state.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(TestEnabled, WriteEnabled, CompareFunction);
    }

    /// <summary>
    /// Determines whether two depth states are equal.
    /// </summary>
    public static bool operator ==(DepthState left, DepthState right) => left.Equals(right);

    /// <summary>
    /// Determines whether two depth states are not equal.
    /// </summary>
    public static bool operator !=(DepthState left, DepthState right) => !left.Equals(right);
}

/// <summary>
/// Specifies the blend operation.
/// </summary>
public enum BlendOperation
{
    /// <summary>
    /// Add source and destination.
    /// </summary>
    Add,

    /// <summary>
    /// Subtract destination from source.
    /// </summary>
    Subtract,

    /// <summary>
    /// Subtract source from destination.
    /// </summary>
    ReverseSubtract,

    /// <summary>
    /// Take minimum of source and destination.
    /// </summary>
    Min,

    /// <summary>
    /// Take maximum of source and destination.
    /// </summary>
    Max,
}

/// <summary>
/// Specifies the blend factor.
/// </summary>
public enum BlendFactor
{
    /// <summary>
    /// Factor is (0, 0, 0, 0).
    /// </summary>
    Zero,

    /// <summary>
    /// Factor is (1, 1, 1, 1).
    /// </summary>
    One,

    /// <summary>
    /// Factor is the source color.
    /// </summary>
    SourceColor,

    /// <summary>
    /// Factor is (1 - source color).
    /// </summary>
    InverseSourceColor,

    /// <summary>
    /// Factor is the source alpha.
    /// </summary>
    SourceAlpha,

    /// <summary>
    /// Factor is (1 - source alpha).
    /// </summary>
    InverseSourceAlpha,

    /// <summary>
    /// Factor is the destination color.
    /// </summary>
    DestinationColor,

    /// <summary>
    /// Factor is (1 - destination color).
    /// </summary>
    InverseDestinationColor,

    /// <summary>
    /// Factor is the destination alpha.
    /// </summary>
    DestinationAlpha,

    /// <summary>
    /// Factor is (1 - destination alpha).
    /// </summary>
    InverseDestinationAlpha,
}

/// <summary>
/// Immutable blend state.
/// Controls how source and destination colors are combined.
/// </summary>
public readonly struct BlendState : IEquatable<BlendState>
{
    /// <summary>
    /// Gets a value indicating whether blending is enabled.
    /// </summary>
    public bool Enabled { get; }

    /// <summary>
    /// Gets the source color blend factor.
    /// </summary>
    public BlendFactor SourceColorFactor { get; }

    /// <summary>
    /// Gets the destination color blend factor.
    /// </summary>
    public BlendFactor DestinationColorFactor { get; }

    /// <summary>
    /// Gets the color blend operation.
    /// </summary>
    public BlendOperation ColorOperation { get; }

    /// <summary>
    /// Gets the source alpha blend factor.
    /// </summary>
    public BlendFactor SourceAlphaFactor { get; }

    /// <summary>
    /// Gets the destination alpha blend factor.
    /// </summary>
    public BlendFactor DestinationAlphaFactor { get; }

    /// <summary>
    /// Gets the alpha blend operation.
    /// </summary>
    public BlendOperation AlphaOperation { get; }

    /// <summary>
    /// Gets the predefined opaque state (blending disabled).
    /// </summary>
    public static BlendState Opaque => new(enabled: false);

    /// <summary>
    /// Gets the predefined alpha blend state (traditional alpha blending).
    /// </summary>
    public static BlendState AlphaBlend => new(
        enabled: true,
        sourceColorFactor: BlendFactor.SourceAlpha,
        destinationColorFactor: BlendFactor.InverseSourceAlpha,
        colorOperation: BlendOperation.Add,
        sourceAlphaFactor: BlendFactor.SourceAlpha,
        destinationAlphaFactor: BlendFactor.InverseSourceAlpha,
        alphaOperation: BlendOperation.Add);

    /// <summary>
    /// Gets the predefined additive blend state.
    /// </summary>
    public static BlendState Additive => new(
        enabled: true,
        sourceColorFactor: BlendFactor.SourceAlpha,
        destinationColorFactor: BlendFactor.One,
        colorOperation: BlendOperation.Add,
        sourceAlphaFactor: BlendFactor.SourceAlpha,
        destinationAlphaFactor: BlendFactor.One,
        alphaOperation: BlendOperation.Add);

    /// <summary>
    /// Initializes a new instance of the <see cref="BlendState"/> struct.
    /// </summary>
    public BlendState(
        bool enabled = false,
        BlendFactor sourceColorFactor = BlendFactor.One,
        BlendFactor destinationColorFactor = BlendFactor.Zero,
        BlendOperation colorOperation = BlendOperation.Add,
        BlendFactor sourceAlphaFactor = BlendFactor.One,
        BlendFactor destinationAlphaFactor = BlendFactor.Zero,
        BlendOperation alphaOperation = BlendOperation.Add)
    {
        Enabled = enabled;
        SourceColorFactor = sourceColorFactor;
        DestinationColorFactor = destinationColorFactor;
        ColorOperation = colorOperation;
        SourceAlphaFactor = sourceAlphaFactor;
        DestinationAlphaFactor = destinationAlphaFactor;
        AlphaOperation = alphaOperation;
    }

    /// <summary>
    /// Determines whether the specified <see cref="BlendState"/> is equal to the current state.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is BlendState other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="BlendState"/> is equal to the current state.
    /// </summary>
    public bool Equals(BlendState other)
    {
        return Enabled == other.Enabled &&
               SourceColorFactor == other.SourceColorFactor &&
               DestinationColorFactor == other.DestinationColorFactor &&
               ColorOperation == other.ColorOperation &&
               SourceAlphaFactor == other.SourceAlphaFactor &&
               DestinationAlphaFactor == other.DestinationAlphaFactor &&
               AlphaOperation == other.AlphaOperation;
    }

    /// <summary>
    /// Returns the hash code for this state.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            Enabled,
            SourceColorFactor,
            DestinationColorFactor,
            ColorOperation,
            SourceAlphaFactor,
            DestinationAlphaFactor,
            AlphaOperation);
    }

    /// <summary>
    /// Determines whether two blend states are equal.
    /// </summary>
    public static bool operator ==(BlendState left, BlendState right) => left.Equals(right);

    /// <summary>
    /// Determines whether two blend states are not equal.
    /// </summary>
    public static bool operator !=(BlendState left, BlendState right) => !left.Equals(right);
}

/// <summary>
/// Specifies the stencil operation.
/// </summary>
public enum StencilOperation
{
    /// <summary>
    /// Keep the existing stencil value.
    /// </summary>
    Keep,

    /// <summary>
    /// Set the stencil value to zero.
    /// </summary>
    Zero,

    /// <summary>
    /// Replace with the reference stencil value.
    /// </summary>
    Replace,

    /// <summary>
    /// Increment and clamp.
    /// </summary>
    IncrementClamp,

    /// <summary>
    /// Decrement and clamp.
    /// </summary>
    DecrementClamp,

    /// <summary>
    /// Invert (bitwise NOT).
    /// </summary>
    Invert,

    /// <summary>
    /// Increment and wrap.
    /// </summary>
    IncrementWrap,

    /// <summary>
    /// Decrement and wrap.
    /// </summary>
    DecrementWrap,
}

/// <summary>
/// Immutable stencil state.
/// </summary>
public readonly struct StencilState : IEquatable<StencilState>
{
    /// <summary>
    /// Gets a value indicating whether stencil testing is enabled.
    /// </summary>
    public bool TestEnabled { get; }

    /// <summary>
    /// Gets the stencil comparison function.
    /// </summary>
    public CompareFunction CompareFunction { get; }

    /// <summary>
    /// Gets the stencil operation when the test fails.
    /// </summary>
    public StencilOperation FailOperation { get; }

    /// <summary>
    /// Gets the stencil operation when the test passes but depth fails.
    /// </summary>
    public StencilOperation DepthFailOperation { get; }

    /// <summary>
    /// Gets the stencil operation when both tests pass.
    /// </summary>
    public StencilOperation PassOperation { get; }

    /// <summary>
    /// Gets the reference stencil value.
    /// </summary>
    public byte Reference { get; }

    /// <summary>
    /// Gets the stencil mask for read operations.
    /// </summary>
    public byte ReadMask { get; }

    /// <summary>
    /// Gets the stencil mask for write operations.
    /// </summary>
    public byte WriteMask { get; }

    /// <summary>
    /// Gets the predefined disabled stencil state.
    /// </summary>
    public static StencilState Disabled => new(testEnabled: false);

    /// <summary>
    /// Initializes a new instance of the <see cref="StencilState"/> struct.
    /// </summary>
    public StencilState(
        bool testEnabled = false,
        CompareFunction compareFunction = CompareFunction.Always,
        StencilOperation failOperation = StencilOperation.Keep,
        StencilOperation depthFailOperation = StencilOperation.Keep,
        StencilOperation passOperation = StencilOperation.Keep,
        byte reference = 0,
        byte readMask = 0xFF,
        byte writeMask = 0xFF)
    {
        TestEnabled = testEnabled;
        CompareFunction = compareFunction;
        FailOperation = failOperation;
        DepthFailOperation = depthFailOperation;
        PassOperation = passOperation;
        Reference = reference;
        ReadMask = readMask;
        WriteMask = writeMask;
    }

    /// <summary>
    /// Determines whether the specified <see cref="StencilState"/> is equal to the current state.
    /// </summary>
    public override bool Equals(object? obj)
    {
        return obj is StencilState other && Equals(other);
    }

    /// <summary>
    /// Determines whether the specified <see cref="StencilState"/> is equal to the current state.
    /// </summary>
    public bool Equals(StencilState other)
    {
        return TestEnabled == other.TestEnabled &&
               CompareFunction == other.CompareFunction &&
               FailOperation == other.FailOperation &&
               DepthFailOperation == other.DepthFailOperation &&
               PassOperation == other.PassOperation &&
               Reference == other.Reference &&
               ReadMask == other.ReadMask &&
               WriteMask == other.WriteMask;
    }

    /// <summary>
    /// Returns the hash code for this state.
    /// </summary>
    public override int GetHashCode()
    {
        return HashCode.Combine(
            TestEnabled,
            CompareFunction,
            FailOperation,
            DepthFailOperation,
            PassOperation,
            Reference,
            ReadMask,
            WriteMask);
    }

    /// <summary>
    /// Determines whether two stencil states are equal.
    /// </summary>
    public static bool operator ==(StencilState left, StencilState right) => left.Equals(right);

    /// <summary>
    /// Determines whether two stencil states are not equal.
    /// </summary>
    public static bool operator !=(StencilState left, StencilState right) => !left.Equals(right);
}
