# Veldrid 4.9.0 Graphics Device API Reference

## Overview

This document provides the complete API reference for implementing a custom graphics device backend for Veldrid 4.9.0, specifically for BGFX integration.

---

## 1. Core Type Names and Interfaces

### GraphicsDevice (Abstract Class)

**Full Name**: `Veldrid.GraphicsDevice`

**Type**: Abstract base class (not an interface)

**Summary**: Represents an abstract graphics device, capable of creating device resources and executing commands.

**Key Point**: `GraphicsDevice` is the main type to extend when implementing a custom graphics device backend. It's an abstract class, not an interface.

---

## 2. GraphicsDeviceFeatures Structure

### Definition

**Full Name**: `Veldrid.GraphicsDeviceFeatures`

**Type**: Read-only struct (auto-properties)

**Purpose**: Enumerates the optional features supported by a given `GraphicsDevice`.

### Properties (All Boolean Read-Only)

| Property | Description |
|----------|-------------|
| `ComputeShader` | Indicates whether Compute Shaders can be used. |
| `GeometryShader` | Indicates whether Geometry Shaders can be used. |
| `TessellationShaders` | Indicates whether Tessellation Shaders can be used. |
| `MultipleViewports` | Indicates whether multiple independent viewports can be set simultaneously. If not supported, only the first Viewport index will be used. |
| `SamplerLodBias` | Indicates whether LOD bias can be applied to samplers. |
| `DrawBaseVertex` | Indicates whether a non-zero "vertexStart" value can be used in draw commands. |
| `DrawBaseInstance` | Indicates whether a non-zero "instanceStart" value can be used in draw commands. |
| `DrawIndirect` | Indicates whether indirect draw commands can be issued. |
| `DrawIndirectBaseInstance` | Indicates whether indirect draw structures can contain a non-zero FirstInstance value. |
| `FillModeWireframe` | Indicates whether wireframe fill mode can be used. |
| `SamplerAnisotropy` | Indicates whether anisotropic filtering is supported. |
| `DepthClipDisable` | Indicates whether depth clipping can be disabled. |
| `Texture1D` | Indicates whether 1D textures are supported. |
| `IndependentBlend` | Indicates whether independent blend states can be set per render target. |
| `StructuredBuffer` | Indicates whether structured buffers are supported. |
| `SubsetTextureView` | Indicates whether a subset of a texture can be viewed. |
| `CommandListDebugMarkers` | Indicates whether debug markers in command lists are supported. |
| `BufferRangeBinding` | Indicates whether uniform and structured buffers can be bound with an offset and size. If false, buffers must be bound with their full range. |
| `ShaderFloat64` | Indicates whether 64-bit floating point integers can be used in shaders. |

### Constructor Pattern

In Veldrid 4.9.0, `GraphicsDeviceFeatures` is created via `ResourceFactory`:

```csharp
M:Veldrid.ResourceFactory.#ctor(Veldrid.GraphicsDeviceFeatures)
```

The features are passed to the ResourceFactory constructor and are typically created by the GraphicsDevice implementation itself based on backend capabilities.

---

## 3. GraphicsDeviceOptions Structure

### Definition

**Full Name**: `Veldrid.GraphicsDeviceOptions`

**Type**: Struct

**Purpose**: Describes several common properties of a GraphicsDevice.

### Fields (Public Configuration)

| Field | Type | Description |
|-------|------|-------------|
| `Debug` | `bool` | Indicates whether the GraphicsDevice will support debug features. |
| `HasMainSwapchain` | `bool` | Indicates whether the GraphicsDevice will include a "main" Swapchain. |
| `SwapchainDepthFormat` | `PixelFormat?` | An optional depth/stencil format for the main Swapchain. |
| `SyncToVerticalBlank` | `bool` | Indicates whether the main Swapchain will sync to vertical refresh rate. |
| `ResourceBindingModel` | `ResourceBindingModel` | Specifies the resource binding model (Common or Improved). |
| `PreferDepthRangeZeroToOne` | `bool` | Indicates whether 0-to-1 depth range is preferred (default depends on backend). |
| `PreferStandardClipSpaceYDirection` | `bool` | Indicates whether standard (bottom-to-top) clip space Y direction is preferred. |
| `SwapchainSrgbFormat` | `bool` | Indicates whether the main Swapchain should use sRGB format. |

### Constructors

```csharp
// No main swapchain
GraphicsDeviceOptions(bool debug)

// With main swapchain
GraphicsDeviceOptions(
    bool debug,
    PixelFormat? swapchainDepthFormat,
    bool syncToVerticalBlank)

GraphicsDeviceOptions(
    bool debug,
    PixelFormat? swapchainDepthFormat,
    bool syncToVerticalBlank,
    ResourceBindingModel resourceBindingModel)

GraphicsDeviceOptions(
    bool debug,
    PixelFormat? swapchainDepthFormat,
    bool syncToVerticalBlank,
    ResourceBindingModel resourceBindingModel,
    bool preferDepthRangeZeroToOne)

GraphicsDeviceOptions(
    bool debug,
    PixelFormat? swapchainDepthFormat,
    bool syncToVerticalBlank,
    ResourceBindingModel resourceBindingModel,
    bool preferDepthRangeZeroToOne,
    bool preferStandardClipSpaceYDirection)

GraphicsDeviceOptions(
    bool debug,
    PixelFormat? swapchainDepthFormat,
    bool syncToVerticalBlank,
    ResourceBindingModel resourceBindingModel,
    bool preferDepthRangeZeroToOne,
    bool preferStandardClipSpaceYDirection,
    bool swapchainSrgbFormat)
```

---

## 4. GraphicsDevice Abstract Class - Key Members

### Public Properties

| Property | Type | Description |
|----------|------|-------------|
| `DeviceName` | `string` | Gets the name of the device. |
| `VendorName` | `string` | Gets the name of the device vendor. |
| `ApiVersion` | `uint` | Gets the API version of the graphics backend. |
| `BackendType` | `GraphicsBackend` | Gets the graphics API type (Direct3D11, Vulkan, OpenGL, Metal, etc.). |
| `IsUvOriginTopLeft` | `bool` | Gets whether UV coordinates begin at top-left (true) or bottom-left (false). |
| `IsClipSpaceYInverted` | `bool` | Gets whether clip space Y increases from top to bottom. |
| `IsDepthRangeZeroToOne` | `bool` | Gets whether depth range is 0-1 (true) or -1-1 (false). |
| `ResourceFactory` | `ResourceFactory` | Gets the resource factory for creating device resources. |
| `MainSwapchain` | `Swapchain` | Gets the main Swapchain (null if not created with one). |
| `SwapchainFramebuffer` | `Framebuffer` | Gets the framebuffer for the main swapchain. |
| `Features` | `GraphicsDeviceFeatures` | Gets the optional features supported by this device. |
| `SyncToVerticalBlank` | `bool` | Gets or sets whether the main Swapchain syncs to vertical blank. |
| `UniformBufferMinOffsetAlignment` | `uint` | The required alignment in bytes for uniform buffer offsets. |
| `StructuredBufferMinOffsetAlignment` | `uint` | The required alignment in bytes for structured buffer offsets. |

### Critical Methods

#### Resource Management
```csharp
// Resource submission
void SubmitCommands(CommandList commandList);
void SubmitCommands(CommandList commandList, Fence fence);

// Fence synchronization
void WaitForFence(Fence fence);
void WaitForFence(Fence fence, TimeSpan timeout);
void WaitForFence(Fence fence, ulong nanosecondTimeout);
void WaitForFences(Fence[] fences, bool waitAll);
void WaitForFences(Fence[] fences, bool waitAll, TimeSpan timeout);
void WaitForFences(Fence[] fences, bool waitAll, ulong nanosecondTimeout);
void ResetFence(Fence fence);

// GPU synchronization
void WaitForIdle(); // Blocks until all GPU commands complete
```

#### Swapchain Operations
```csharp
void SwapBuffers(); // Swaps main swapchain
void SwapBuffers(Swapchain swapchain); // Swaps specific swapchain
void ResizeMainWindow(uint width, uint height); // Notifies device of window resize
```

#### Resource Mapping (for CPU access)
```csharp
MappedResource Map(MappableResource resource, MapMode mode);
MappedResource Map(MappableResource resource, MapMode mode, uint subresource);
T Map<T>(MappableResource resource, MapMode mode) where T : unmanaged;
T Map<T>(MappableResource resource, MapMode mode, uint subresource) where T : unmanaged;
void Unmap(MappableResource resource);
void Unmap(MappableResource resource, uint subresource);
```

#### Texture/Buffer Updates
```csharp
void UpdateTexture(Texture texture, IntPtr source, uint srcX, uint srcY, uint srcZ, 
                   uint dstX, uint dstY, uint dstZ, uint width, uint height, uint depth);
void UpdateTexture<T>(Texture texture, T[] source, uint srcX, uint srcY, uint srcZ,
                      uint dstX, uint dstY, uint dstZ, uint width, uint height) where T : unmanaged;
void UpdateBuffer<T>(DeviceBuffer buffer, uint bufferOffsetInBytes, T source) where T : unmanaged;
void UpdateBuffer<T>(DeviceBuffer buffer, uint bufferOffsetInBytes, T[] source) where T : unmanaged;
void UpdateBuffer<T>(DeviceBuffer buffer, uint bufferOffsetInBytes, ReadOnlySpan<T> source) where T : unmanaged;
void UpdateBuffer(DeviceBuffer buffer, uint bufferOffsetInBytes, IntPtr source, uint sizeInBytes);
```

#### Utility Methods
```csharp
bool GetPixelFormatSupport(PixelFormat format, TextureType type, TextureUsage usage);
bool GetPixelFormatSupport(PixelFormat format, TextureType type, TextureUsage usage, out PixelFormatProperties properties);
uint GetSampleCountLimit(PixelFormat format, bool depthFormat);
bool IsBackendSupported(GraphicsBackend backend);
void DisposeWhenIdle(IDisposable disposable); // Deferred disposal
```

#### Samplers (Common, Pre-created)
```csharp
Sampler PointSampler { get; }   // Point filtering
Sampler LinearSampler { get; }  // Linear filtering
Sampler Aniso4xSampler { get; } // 4x anisotropic filtering
```

#### Abstract Methods to Override
```csharp
protected abstract void PlatformDispose();
protected virtual void PostDeviceCreated();
protected abstract MappedResource MapCore(MappableResource resource, MapMode mode, uint subresource);
protected abstract void UnmapCore(MappableResource resource, uint subresource);
```

---

## 5. Backend-Specific Creation Methods

Veldrid provides static factory methods on `GraphicsDevice` for creating backend-specific instances:

```csharp
// Direct3D 11
static GraphicsDevice CreateD3D11(GraphicsDeviceOptions options);
static GraphicsDevice CreateD3D11(GraphicsDeviceOptions options, SwapchainDescription swapchain);
static GraphicsDevice CreateD3D11(GraphicsDeviceOptions options, D3D11DeviceOptions d3d11Options);

// Vulkan
static GraphicsDevice CreateVulkan(GraphicsDeviceOptions options);
static GraphicsDevice CreateVulkan(GraphicsDeviceOptions options, VulkanDeviceOptions vulkanOptions);
static GraphicsDevice CreateVulkan(GraphicsDeviceOptions options, SwapchainDescription swapchain);

// OpenGL
static GraphicsDevice CreateOpenGL(GraphicsDeviceOptions options, OpenGLPlatformInfo platformInfo, uint width, uint height);

// OpenGL ES
static GraphicsDevice CreateOpenGLES(GraphicsDeviceOptions options, SwapchainDescription swapchain);

// Metal (macOS, iOS)
static GraphicsDevice CreateMetal(GraphicsDeviceOptions options);
static GraphicsDevice CreateMetal(GraphicsDeviceOptions options, SwapchainDescription swapchain);
static GraphicsDevice CreateMetal(GraphicsDeviceOptions options, IntPtr mtlDeviceHandle);
```

---

## 6. How to Implement a Custom BGFX Graphics Device

### Step 1: Create Your Implementation Class

```csharp
public abstract class BgfxGraphicsDevice : GraphicsDevice
{
    // Implementation code
}
```

### Step 2: Implement Required Properties

At minimum, implement these properties to report device capabilities:

```csharp
public override string DeviceName { get; }
public override string VendorName { get; }
public override uint ApiVersion { get; }
public override GraphicsBackend BackendType => GraphicsBackend.BGFX;
public override bool IsUvOriginTopLeft { get; }
public override bool IsClipSpaceYInverted { get; }
public override bool IsDepthRangeZeroToOne { get; }
public override GraphicsDeviceFeatures Features { get; }
public override ResourceFactory ResourceFactory { get; }
public override Swapchain MainSwapchain { get; }
public override Framebuffer SwapchainFramebuffer { get; }
```

### Step 3: Create GraphicsDeviceFeatures

Create a struct instance based on BGFX backend capabilities:

```csharp
var features = new GraphicsDeviceFeatures
{
    ComputeShader = bgfxSupportsComputeShaders,
    GeometryShader = bgfxSupportsGeometryShaders,
    // ... set other features based on BGFX capabilities
};
```

### Step 4: Implement Abstract Methods

```csharp
protected override void PlatformDispose()
{
    // BGFX shutdown code
}

protected override MappedResource MapCore(MappableResource resource, MapMode mode, uint subresource)
{
    // Implement memory mapping for the resource
}

protected override void UnmapCore(MappableResource resource, uint subresource)
{
    // Implement unmap for the resource
}
```

### Step 5: Implement Command Submission

```csharp
public override void SubmitCommands(CommandList commandList)
{
    // Cast to BGFX command list and submit to BGFX
    var bgfxCmdList = (BgfxCommandList)commandList;
    bgfxCmdList.SubmitToDevice();
}
```

---

## 7. ResourceFactory and Its Role

**Type**: `Veldrid.ResourceFactory`

**Purpose**: A device object responsible for creating graphics resources (buffers, textures, samplers, shaders, pipelines, etc.).

**Usage**: Always accessible via `GraphicsDevice.ResourceFactory` property.

**Key Constructor**:
```csharp
ResourceFactory(GraphicsDeviceFeatures features)
```

**Responsibilities**:
- Create and manage device resources
- Report format support via `GetPixelFormatSupport()`
- Create samplers, buffers, textures, shaders, pipelines, framebuffers, command lists

---

## 8. Key Enums and Types to Reference

```csharp
// GraphicsBackend enum
public enum GraphicsBackend
{
    Direct3D11,
    Vulkan,
    OpenGL,
    Metal,
    OpenGLES
    // Note: BGFX would need to be added
}

// ResourceBindingModel enum
public enum ResourceBindingModel
{
    Common,    // Each resource is individually bound
    Improved   // Descriptor tables/layouts used
}

// MapMode enum (for GPU memory access)
public enum MapMode
{
    Read,      // Read-only access
    Write,     // Write-only access
    ReadWrite  // Full read-write access
}
```

---

## 9. OpenSAGE Integration Notes

Based on the codebase analysis, OpenSAGE wraps Veldrid with a custom `IGraphicsDevice` interface:

**OpenSAGE's IGraphicsDevice**:
- Custom abstraction layer in `src/OpenSage.Graphics/Abstractions/IGraphicsDevice.cs`
- Adapts Veldrid's `GraphicsDevice` for OpenSAGE's architecture
- Used by `VeldridGraphicsDeviceAdapter` and planned `BgfxGraphicsDevice`

**For BGFX Implementation**:
1. Extend Veldrid's `GraphicsDevice` abstract class
2. Implement all required abstract members and properties
3. Create a corresponding adapter class implementing OpenSAGE's `IGraphicsDevice`
4. Register in `GraphicsDeviceFactory` for backend selection

---

## 10. Summary Table

| Concept | Type | Details |
|---------|------|---------|
| Main Device Class | Abstract Class | `Veldrid.GraphicsDevice` |
| Features Struct | Struct | `Veldrid.GraphicsDeviceFeatures` (19 boolean properties) |
| Options Struct | Struct | `Veldrid.GraphicsDeviceOptions` (8+ constructors) |
| No Interface | N/A | Veldrid uses abstract class, NOT an interface |
| Base Classes | N/A | Only abstract class hierarchy; no base implementations |
| Resource Factory | Class | `Veldrid.ResourceFactory` - created by device |
| Creation Pattern | Static Methods | Backend-specific factory methods on `GraphicsDevice` |

---

## References

- Veldrid Version: 4.9.0
- NuGet Package: `veldrid` (4.9.0)
- XML Documentation: `/Users/felipebraz/.nuget/packages/veldrid/4.9.0/lib/net6.0/Veldrid.xml`

