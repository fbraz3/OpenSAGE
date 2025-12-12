namespace OpenSage.Graphics.Core;

/// <summary>
/// Describes the graphics capabilities of the current GPU/backend.
/// Used for runtime feature detection and fallback handling.
/// </summary>
public readonly struct GraphicsCapabilities
{
    /// <summary>
    /// Gets a value indicating whether the graphics backend is initialized.
    /// </summary>
    public bool IsInitialized { get; }

    /// <summary>
    /// Gets the name of the graphics backend (e.g., "Vulkan", "Direct3D11", "Metal").
    /// </summary>
    public string BackendName { get; }

    /// <summary>
    /// Gets the graphics API version.
    /// </summary>
    public string ApiVersion { get; }

    /// <summary>
    /// Gets the GPU vendor name.
    /// </summary>
    public string VendorName { get; }

    /// <summary>
    /// Gets the GPU device name.
    /// </summary>
    public string DeviceName { get; }

    /// <summary>
    /// Gets the maximum texture size (width/height).
    /// </summary>
    public uint MaxTextureSize { get; }

    /// <summary>
    /// Gets the maximum number of viewports supported.
    /// </summary>
    public uint MaxViewports { get; }

    /// <summary>
    /// Gets the maximum number of render targets supported.
    /// </summary>
    public uint MaxRenderTargets { get; }

    /// <summary>
    /// Gets a value indicating whether texture compression BC (DXT) is supported.
    /// </summary>
    public bool SupportsTextureCompressionBC { get; }

    /// <summary>
    /// Gets a value indicating whether ASTC texture compression is supported.
    /// </summary>
    public bool SupportsTextureCompressionASTC { get; }

    /// <summary>
    /// Gets a value indicating whether compute shaders are supported.
    /// </summary>
    public bool SupportsComputeShaders { get; }

    /// <summary>
    /// Gets a value indicating whether indirect rendering is supported.
    /// </summary>
    public bool SupportsIndirectRendering { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GraphicsCapabilities"/> struct.
    /// </summary>
    public GraphicsCapabilities(
        bool isInitialized,
        string backendName,
        string apiVersion,
        string vendorName,
        string deviceName,
        uint maxTextureSize,
        uint maxViewports,
        uint maxRenderTargets,
        bool supportsTextureCompressionBC,
        bool supportsTextureCompressionASTC,
        bool supportsComputeShaders,
        bool supportsIndirectRendering)
    {
        IsInitialized = isInitialized;
        BackendName = backendName ?? string.Empty;
        ApiVersion = apiVersion ?? string.Empty;
        VendorName = vendorName ?? string.Empty;
        DeviceName = deviceName ?? string.Empty;
        MaxTextureSize = maxTextureSize;
        MaxViewports = maxViewports;
        MaxRenderTargets = maxRenderTargets;
        SupportsTextureCompressionBC = supportsTextureCompressionBC;
        SupportsTextureCompressionASTC = supportsTextureCompressionASTC;
        SupportsComputeShaders = supportsComputeShaders;
        SupportsIndirectRendering = supportsIndirectRendering;
    }
}
