# Veldrid Shader System: Implementation Quick Reference

**Purpose**: Compact reference for implementing VeldridGraphicsDevice.CreateShaderProgram()  
**Format**: Code snippets ready for integration  
**Last Updated**: December 12, 2025

---

## Quick Navigation

- [1. File Structure](#1-file-structure)
- [2. VeldridShaderProgram Class (Complete)](#2-veldridshaderprogram-class-complete)
- [3. CreateShader Method (Complete)](#3-createshader-method-complete)
- [4. Helper Extensions](#4-helper-extensions)
- [5. Integration Checklist](#5-integration-checklist)
- [6. Common Patterns](#6-common-patterns)

---

## 1. File Structure

```
src/OpenSage.Graphics/
├── Veldrid/
│   ├── VeldridGraphicsDevice.cs         ← Main implementation
│   ├── VeldridResourceAdapters.cs       ← Add VeldridShaderProgram
│   └── ShaderStageHelpers.cs            ← New: stage conversion
├── Abstractions/
│   ├── IGraphicsDevice.cs               ← Interface (already has CreateShader)
│   └── ResourceInterfaces.cs            ← IShaderProgram interface
├── Resources/
│   └── ShaderSource.cs                  ← Already exists
├── Shaders/
│   └── ShaderCompilationCache.cs        ← Already exists
└── Core/
    └── GraphicsException.cs             ← Already exists
```

---

## 2. VeldridShaderProgram Class (Complete)

Add this to: `src/OpenSage.Graphics/Veldrid/VeldridResourceAdapters.cs`

```csharp
/// <summary>
/// Thin wrapper around Veldrid.Shader implementing IShaderProgram interface.
/// Manages a compiled shader program for a specific graphics backend.
/// Wraps native backend shader objects (MSL, GLSL, HLSL, SPIR-V).
/// </summary>
internal class VeldridShaderProgram : IShaderProgram
{
    private readonly VeldridLib.Shader _native;
    private readonly string _name;
    private readonly string _entryPoint;
    private bool _disposed;

    /// <summary>
    /// Initializes a new instance of the <see cref="VeldridShaderProgram"/> class.
    /// </summary>
    /// <param name="native">The native Veldrid shader object.</param>
    /// <param name="name">The shader identifier/name.</param>
    /// <param name="entryPoint">The shader entry point function name.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown if native, name, or entryPoint is null.
    /// </exception>
    public VeldridShaderProgram(VeldridLib.Shader native, string name, string entryPoint)
    {
        _native = native ?? throw new ArgumentNullException(nameof(native));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _entryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
    }

    /// <summary>
    /// Gets the underlying Veldrid shader object.
    /// Used internally for pipeline creation and resource cleanup.
    /// </summary>
    public VeldridLib.Shader Native => _native;

    /// <summary>
    /// Gets the shader identifier/name.
    /// Used for debugging and resource tracking.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the shader entry point function name (typically "main").
    /// </summary>
    public string EntryPoint => _entryPoint;

    /// <summary>
    /// Releases the native Veldrid shader resource.
    /// Called when the handle is destroyed or the cache is cleared.
    /// </summary>
    public void Dispose()
    {
        if (!_disposed)
        {
            _native?.Dispose();
            _disposed = true;
        }
    }
}
```

---

## 3. CreateShader Method (Complete)

Replace the placeholder in: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs`

### A. Add ResourcePool for Shaders

In the `VeldridGraphicsDevice` class constructor/field initialization:

```csharp
public class VeldridGraphicsDevice : DisposableBase, IGraphicsDevice
{
    private readonly VeldridLib.GraphicsDevice _device;
    private readonly VeldridLib.CommandList _cmdList;

    // ===== Existing pools =====
    private readonly ResourcePool<VeldridLib.DeviceBuffer> _bufferPool;
    private readonly ResourcePool<VeldridLib.Texture> _texturePool;
    private readonly ResourcePool<VeldridLib.Sampler> _samplerPool;
    private readonly ResourcePool<VeldridLib.Framebuffer> _framebufferPool;
    
    // ===== ADD THIS: Shader and Pipeline pools =====
    private readonly ResourcePool<VeldridShaderProgram> _shaderPool;
    private readonly ResourcePool<VeldridPipeline> _pipelinePool;

    public VeldridGraphicsDevice(VeldridLib.GraphicsDevice device)
    {
        // ... existing initialization ...

        // Initialize shader pool
        _shaderPool = new ResourcePool<VeldridShaderProgram>(256);
        AddDisposable(_shaderPool);

        // Initialize pipeline pool
        _pipelinePool = new ResourcePool<VeldridPipeline>(128);
        AddDisposable(_pipelinePool);
    }
}
```

### B. Implement CreateShader Method

```csharp
public Handle<IShaderProgram> CreateShader(
    string name,
    ReadOnlySpan<byte> spirvData,
    string entryPoint = "main")
{
    // ===== Validation =====
    if (string.IsNullOrWhiteSpace(name))
        throw new ArgumentException("Shader name cannot be null or empty.", nameof(name));
    
    if (spirvData.IsEmpty)
        throw new ArgumentException("SPIR-V bytecode cannot be empty.", nameof(spirvData));
    
    if (string.IsNullOrWhiteSpace(entryPoint))
        throw new ArgumentException("Entry point cannot be null or empty.", nameof(entryPoint));

    // ===== SPIR-V Magic Number Validation =====
    if (!ValidateSpirVMagic(spirvData))
        throw new ArgumentException(
            "Invalid SPIR-V bytecode: magic number mismatch or data too short.",
            nameof(spirvData));

    try
    {
        // ===== Infer Shader Stage from Naming Convention =====
        var stage = InferShaderStageFromName(name);

        // ===== Create ShaderDescription =====
        var shaderDesc = new VeldridLib.ShaderDescription(
            stage: stage,
            shaderBytes: spirvData.ToArray(),
            entryPoint: entryPoint);

        // ===== Cross-Compile SPIR-V to Backend Format =====
        // Veldrid.SPIRV handles the conversion internally
        var nativeShader = _device.ResourceFactory.CreateFromSpirv(shaderDesc);

        if (nativeShader == null)
            throw new GraphicsException(
                $"Failed to create shader '{name}': ResourceFactory returned null.");

        // ===== Wrap in OpenSAGE Adapter =====
        var wrapper = new VeldridShaderProgram(nativeShader, name, entryPoint);

        // ===== Allocate from Pool =====
        var poolHandle = _shaderPool.Allocate(wrapper);

        // ===== Return Handle =====
        return new Handle<IShaderProgram>(poolHandle.Index, poolHandle.Generation);
    }
    catch (VeldridLib.VeldridException ex)
    {
        var backendInfo = $"Backend: {_device.BackendType}, Device: {_device.DeviceName}";
        throw new GraphicsException(
            $"Failed to compile shader '{name}' (entry: '{entryPoint}'). " +
            $"{backendInfo}. " +
            $"SPIR-V bytecode may be invalid or unsupported by this backend.",
            ex);
    }
    catch (Exception ex) when (!(ex is GraphicsException))
    {
        throw new GraphicsException(
            $"Unexpected error during shader '{name}' compilation.",
            ex);
    }
}
```

### C. Add Helper Methods

```csharp
/// <summary>
/// Validates SPIR-V magic number without full parsing.
/// SPIR-V magic: 0x07230203 (little-endian uint32)
/// </summary>
private static bool ValidateSpirVMagic(ReadOnlySpan<byte> spirvData)
{
    const int MinHeaderSize = 20;  // SPIR-V header is 5 words (20 bytes)
    
    if (spirvData.Length < MinHeaderSize)
        return false;

    uint magic = BitConverter.ToUInt32(spirvData);
    const uint SpirvMagic = 0x07230203;
    
    return magic == SpirvMagic;
}

/// <summary>
/// Infers shader stage from filename convention.
/// Conventions: .vert, .frag, .comp, .geom, .tesc, .tese
/// </summary>
private static VeldridLib.ShaderStages InferShaderStageFromName(string shaderName)
{
    var lower = shaderName.ToLowerInvariant();
    
    if (lower.Contains(".vert") || lower.EndsWith("_vs") || lower.EndsWith("_vertex"))
        return VeldridLib.ShaderStages.Vertex;
    
    if (lower.Contains(".frag") || lower.EndsWith("_fs") || lower.EndsWith("_fragment"))
        return VeldridLib.ShaderStages.Fragment;
    
    if (lower.Contains(".comp") || lower.EndsWith("_cs") || lower.EndsWith("_compute"))
        return VeldridLib.ShaderStages.Compute;
    
    if (lower.Contains(".geom") || lower.EndsWith("_gs") || lower.EndsWith("_geometry"))
        return VeldridLib.ShaderStages.Geometry;
    
    if (lower.Contains(".tesc") || lower.EndsWith("_tcs") || lower.EndsWith("_tesscontrol"))
        return VeldridLib.ShaderStages.TessellationControl;
    
    if (lower.Contains(".tese") || lower.EndsWith("_tes") || lower.EndsWith("_tesseval"))
        return VeldridLib.ShaderStages.TessellationEvaluation;

    throw new ArgumentException(
        $"Cannot infer shader stage from name '{shaderName}'. " +
        $"Use naming convention: *.vert, *.frag, *.comp, *.geom, *.tesc, *.tese",
        nameof(shaderName));
}
```

### D. Update DestroyShader

```csharp
public void DestroyShader(Handle<IShaderProgram> shader)
{
    if (shader.IsValid)
    {
        // Look up in handle map and release from pool
        if (_shaderHandles.TryGetValue(shader.Id, out var poolHandle))
        {
            _shaderPool.Release(poolHandle);
            _shaderHandles.Remove(shader.Id);
        }
    }
}
```

### E. Update GetShader

```csharp
public IShaderProgram? GetShader(Handle<IShaderProgram> shader)
{
    if (!shader.IsValid)
        return null;

    if (_shaderHandles.TryGetValue(shader.Id, out var poolHandle))
    {
        return _shaderPool.TryGet(poolHandle);
    }

    return null;
}
```

---

## 4. Helper Extensions

Create new file: `src/OpenSage.Graphics/Veldrid/ShaderStageHelpers.cs`

```csharp
using OpenSage.Graphics.Resources;
using VeldridLib = Veldrid;

namespace OpenSage.Graphics.Veldrid;

/// <summary>
/// Extension methods for shader stage conversion.
/// </summary>
internal static class ShaderStageHelpers
{
    /// <summary>
    /// Converts OpenSAGE ShaderStages to Veldrid ShaderStages.
    /// </summary>
    public static VeldridLib.ShaderStages ToVeldridShaderStages(this ShaderStages stages)
    {
        return stages switch
        {
            ShaderStages.Vertex => VeldridLib.ShaderStages.Vertex,
            ShaderStages.Fragment => VeldridLib.ShaderStages.Fragment,
            ShaderStages.Compute => VeldridLib.ShaderStages.Compute,
            ShaderStages.Geometry => VeldridLib.ShaderStages.Geometry,
            ShaderStages.TessControl => VeldridLib.ShaderStages.TessellationControl,
            ShaderStages.TessEval => VeldridLib.ShaderStages.TessellationEvaluation,
            _ => throw new ArgumentException(
                $"Unsupported shader stage: {stages}",
                nameof(stages))
        };
    }

    /// <summary>
    /// Converts Veldrid ShaderStages to OpenSAGE ShaderStages.
    /// </summary>
    public static ShaderStages ToOpenSageShaderStages(this VeldridLib.ShaderStages stages)
    {
        return stages switch
        {
            VeldridLib.ShaderStages.Vertex => ShaderStages.Vertex,
            VeldridLib.ShaderStages.Fragment => ShaderStages.Fragment,
            VeldridLib.ShaderStages.Compute => ShaderStages.Compute,
            VeldridLib.ShaderStages.Geometry => ShaderStages.Geometry,
            VeldridLib.ShaderStages.TessellationControl => ShaderStages.TessControl,
            VeldridLib.ShaderStages.TessellationEvaluation => ShaderStages.TessEval,
            _ => ShaderStages.None
        };
    }
}
```

---

## 5. Integration Checklist

- [ ] **Create VeldridShaderProgram class**
  - Location: `VeldridResourceAdapters.cs`
  - Follows same pattern as VeldridBuffer, VeldridTexture, VeldridSampler
  - Has `Native` property for internal use
  - Has `Name` and `EntryPoint` properties
  - Implements `IDisposable` (calls `_native.Dispose()`)

- [ ] **Add shader pool to VeldridGraphicsDevice**
  - `_shaderPool = new ResourcePool<VeldridShaderProgram>(256)`
  - Track pool handles in `Dictionary<uint, PoolHandle>`

- [ ] **Implement CreateShader method**
  - Validates inputs (name, spirvData, entryPoint)
  - Validates SPIR-V magic number
  - Infers shader stage from name
  - Calls `CreateFromSpirv` via ResourceFactory
  - Wraps result in VeldridShaderProgram
  - Allocates from pool
  - Returns Handle<IShaderProgram>

- [ ] **Add helper methods**
  - `ValidateSpirVMagic()` - checks 0x07230203
  - `InferShaderStageFromName()` - parses *.vert, *.frag, etc.
  - Stage conversion extensions (optional)

- [ ] **Update DestroyShader/GetShader**
  - Remove placeholder implementations
  - Use pool lookup

- [ ] **Add Veldrid.SPIRV NuGet reference**
  - Already listed in csproj: `<PackageReference Include="Veldrid.SPIRV" />`
  - Verify version 1.0.15 or later

- [ ] **Test with real shaders**
  - Load embedded SPIR-V resources
  - Create handles for multiple stages
  - Verify pipeline creation works

---

## 6. Common Patterns

### Loading Embedded SPIR-V

```csharp
private static byte[] LoadEmbeddedShaderSpirV(string resourceName)
{
    var assembly = Assembly.GetExecutingAssembly();
    using (var stream = assembly.GetManifestResourceStream(resourceName))
    {
        if (stream == null)
            throw new FileNotFoundException($"Shader resource not found: {resourceName}");
        
        var buffer = new byte[stream.Length];
        stream.Read(buffer, 0, buffer.Length);
        return buffer;
    }
}

// Usage
var terrainVertSpirV = LoadEmbeddedShaderSpirV(
    "OpenSage.Game.Assets.Shaders.Terrain.vert.spv");
```

### Creating Shader from ShaderSource

```csharp
public Handle<IShaderProgram> CreateShaderProgram(ShaderSource source)
{
    if (source.Stage == ShaderStages.None)
        throw new ArgumentException("Shader source must specify valid stage.", nameof(source));

    return CreateShader(
        $"Shader_{source.Stage}_{source.EntryPoint}",
        source.SpirVBytes.Span,
        source.EntryPoint);
}

// Usage
var vertexSource = new ShaderSource(
    ShaderStages.Vertex,
    terrainVertSpirV,
    "main");

var vsHandle = device.CreateShaderProgram(vertexSource);
```

### Error Handling Pattern

```csharp
try
{
    var shader = device.ResourceFactory.CreateFromSpirv(desc);
}
catch (VeldridLib.VeldridException ex)
{
    throw new GraphicsException(
        $"Shader compilation failed on {device.BackendType} backend: {ex.Message}",
        ex);
}
```

### Feature Detection

```csharp
// Check if backend supports required features
if (!device.Features.TessellationShaders)
{
    Console.WriteLine("Note: Tessellation not available (common on Metal)");
    // Use fallback LOD meshes
}

if (!device.Features.ComputeShaders)
{
    Console.WriteLine("Warning: Compute shaders not supported");
    // Use CPU-based particle updates
}
```

---

## Testing Snippet

```csharp
[Test]
public void CreateShader_WithValidSpirV_ReturnsValidHandle()
{
    var spirvData = LoadEmbeddedShaderSpirV("test_shader.vert.spv");
    
    var handle = device.CreateShader("TestShader", spirvData, "main");
    
    Assert.IsTrue(handle.IsValid);
    
    var shader = device.GetShader(handle);
    Assert.IsNotNull(shader);
    Assert.AreEqual("TestShader", shader.Name);
    Assert.AreEqual("main", shader.EntryPoint);
}

[Test]
public void CreateShader_WithInvalidSpirV_ThrowsGraphicsException()
{
    var invalidSpirV = new byte[] { 0xFF, 0xFF, 0xFF, 0xFF };
    
    Assert.Throws<ArgumentException>(() =>
        device.CreateShader("Invalid", invalidSpirV, "main"));
}

[Test]
public void DestroyShader_WithValidHandle_SucceedsWithoutError()
{
    var spirvData = LoadEmbeddedShaderSpirV("test_shader.vert.spv");
    var handle = device.CreateShader("TestShader", spirvData, "main");
    
    device.DestroyShader(handle);
    
    var shader = device.GetShader(handle);
    Assert.IsNull(shader);
}
```

---

**Ready to integrate!**
