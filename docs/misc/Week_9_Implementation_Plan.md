# Week 9 Implementation Plan - Resource Adapters, Handle Pooling, and Device Integration

**Created**: 12 December 2025 (Post-Research Planning)  
**Status**: Ready for implementation start  
**Based on**: Week_9_Research_Findings.md + Phase 2 Architectural Design

---

## Executive Summary

Week 9 focuses on bridging the gap between the VeldridGraphicsDevice skeleton (created Week 8) and actual Veldrid resource creation/management. The critical deliverable is a working resource pool system with proper handle generation validation, enabling subsequent weeks to build on a solid foundation.

**Key Challenge**: Veldrid's ResourceSet/ResourceLayout two-level binding architecture requires careful abstraction to remain backend-agnostic while supporting future BGFX adaptation.

**Success Criterion**: Simple triangle can be rendered by end of Week 9.

---

## Implementation Schedule (5 Working Days)

### Day 1: Resource Pooling Infrastructure

**Objective**: Create reusable resource pool system for all resource types

**Deliverables**:

1. **File**: `src/OpenSage.Graphics/Pooling/ResourcePool.cs`
   - Generic `ResourcePool<T>` class
   - Handle generation counter
   - Allocation/deallocation with validation
   - Overflow handling strategy
   - Zero-copy operation design

```csharp
using System;
using System.Collections.Generic;

namespace OpenSage.Graphics.Pooling;

/// <summary>
/// Generic resource pool with generation-based handle validation.
/// Prevents use-after-free errors by tracking handle generations.
/// </summary>
public class ResourcePool<T> where T : class, IDisposable
{
    private T[] _resources;
    private uint[] _generations;
    private Queue<uint> _freeSlots;
    private uint _nextId;
    
    public ResourcePool(int initialCapacity = 256)
    {
        _resources = new T[initialCapacity];
        _generations = new uint[initialCapacity];
        _freeSlots = new Queue<uint>(initialCapacity);
        _nextId = 0;
    }
    
    /// <summary>Allocate a resource handle, reusing freed slots when possible.</summary>
    public Handle<T> Allocate(T resource)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        
        uint idx;
        
        // Reuse freed slot
        if (_freeSlots.TryDequeue(out idx))
        {
            _resources[idx] = resource;
            _generations[idx]++;
            return new Handle<T>(idx, _generations[idx]);
        }
        
        // Allocate new slot
        if (_nextId >= _resources.Length)
            GrowCapacity();
        
        idx = _nextId++;
        _resources[idx] = resource;
        _generations[idx] = 1;
        return new Handle<T>(idx, 1);
    }
    
    /// <summary>Try to retrieve resource, validating handle generation.</summary>
    public bool TryGet(Handle<T> handle, out T resource)
    {
        if (!handle.IsValid || handle.Index >= _nextId)
        {
            resource = null;
            return false;
        }
        
        if (_generations[handle.Index] != handle.Generation)
        {
            // Handle is stale (use-after-free attempt)
            resource = null;
            return false;
        }
        
        resource = _resources[handle.Index];
        return resource != null;
    }
    
    /// <summary>Release a resource handle and dispose the resource.</summary>
    public bool Release(Handle<T> handle)
    {
        if (!TryGet(handle, out var resource))
            return false;
        
        resource?.Dispose();
        _resources[handle.Index] = null;
        _freeSlots.Enqueue(handle.Index);
        return true;
    }
    
    private void GrowCapacity()
    {
        var newCapacity = _resources.Length * 2;
        Array.Resize(ref _resources, newCapacity);
        Array.Resize(ref _generations, newCapacity);
    }
    
    public void Clear()
    {
        for (uint i = 0; i < _nextId; i++)
        {
            _resources[i]?.Dispose();
            _resources[i] = null;
        }
        _freeSlots.Clear();
        _nextId = 0;
    }
    
    public void Dispose()
    {
        Clear();
    }
}
```

2. **File**: `src/OpenSage.Graphics/Pooling/PooledResourceWrapper.cs`
   - Base class for pooled Veldrid resource wrappers
   - Automatic disposal tracking
   - Generation validation helpers

```csharp
using System;

namespace OpenSage.Graphics.Pooling;

/// <summary>
/// Base class for resources managed by ResourcePool.
/// Tracks handle identity for debugging and validation.
/// </summary>
public abstract class PooledResourceWrapper : IDisposable
{
    protected Handle<PooledResourceWrapper> Handle { get; set; }
    
    public uint Index => Handle.Index;
    public uint Generation => Handle.Generation;
    
    public abstract void Dispose();
}
```

**Testing**:
- Unit test: Allocate 10 resources, verify handles
- Unit test: Deallocate 5, verify free slots reused
- Unit test: Verify generation mismatch returns false
- Unit test: Verify capacity growth

**Checklist**:
- [ ] ResourcePool compiles
- [ ] All methods have XML documentation
- [ ] Unit tests pass (4/4)
- [ ] No null pointer exceptions

---

### Day 2: Resource Adapter Classes (VeldridBuffer, VeldridTexture)

**Objective**: Implement thin wrapper classes for Veldrid resources

**Deliverables**:

1. **File**: `src/OpenSage.Graphics/Veldrid/VeldridResourceAdapters.cs`

   Contains:
   - `VeldridBuffer` (implements IBuffer)
   - `VeldridTexture` (implements ITexture)
   - `VeldridTextureView` helper
   - `VeldridFramebuffer` (implements IFramebuffer)
   - `VeldridSampler` (implements ISampler)

```csharp
using System;
using Veldrid;

namespace OpenSage.Graphics.Veldrid;

/// <summary>Veldrid implementation of IBuffer abstraction.</summary>
public sealed class VeldridBuffer : IBuffer, IDisposable
{
    private readonly Veldrid.DeviceBuffer _nativeBuffer;
    private Handle<IBuffer> _handle;
    private bool _disposed;
    
    public uint Index => _handle.Index;
    public uint Generation => _handle.Generation;
    public bool IsValid => _handle.IsValid && !_disposed;
    
    /// <summary>Native Veldrid buffer (internal use only).</summary>
    internal Veldrid.DeviceBuffer Native => _nativeBuffer;
    
    internal VeldridBuffer(Handle<IBuffer> handle, Veldrid.DeviceBuffer nativeBuffer)
    {
        _handle = handle;
        _nativeBuffer = nativeBuffer ?? throw new ArgumentNullException(nameof(nativeBuffer));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _nativeBuffer?.Dispose();
    }
}

/// <summary>Veldrid implementation of ITexture abstraction.</summary>
public sealed class VeldridTexture : ITexture, IDisposable
{
    private readonly Veldrid.Texture _nativeTexture;
    private Handle<ITexture> _handle;
    private bool _disposed;
    
    public uint Index => _handle.Index;
    public uint Generation => _handle.Generation;
    public bool IsValid => _handle.IsValid && !_disposed;
    
    public uint Width => _nativeTexture.Width;
    public uint Height => _nativeTexture.Height;
    
    internal Veldrid.Texture Native => _nativeTexture;
    
    internal VeldridTexture(Handle<ITexture> handle, Veldrid.Texture nativeTexture)
    {
        _handle = handle;
        _nativeTexture = nativeTexture ?? throw new ArgumentNullException(nameof(nativeTexture));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _nativeTexture?.Dispose();
    }
}

/// <summary>Veldrid implementation of IFramebuffer abstraction.</summary>
public sealed class VeldridFramebuffer : IFramebuffer, IDisposable
{
    private readonly Veldrid.Framebuffer _nativeFramebuffer;
    private Handle<IFramebuffer> _handle;
    private bool _disposed;
    
    public uint Index => _handle.Index;
    public uint Generation => _handle.Generation;
    public bool IsValid => _handle.IsValid && !_disposed;
    
    public uint Width => _nativeFramebuffer.Width;
    public uint Height => _nativeFramebuffer.Height;
    
    internal Veldrid.Framebuffer Native => _nativeFramebuffer;
    
    internal VeldridFramebuffer(Handle<IFramebuffer> handle, Veldrid.Framebuffer nativeFramebuffer)
    {
        _handle = handle;
        _nativeFramebuffer = nativeFramebuffer ?? throw new ArgumentNullException(nameof(nativeFramebuffer));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _nativeFramebuffer?.Dispose();
    }
}

/// <summary>Veldrid implementation of ISampler abstraction.</summary>
public sealed class VeldridSampler : ISampler, IDisposable
{
    private readonly Veldrid.Sampler _nativeSampler;
    private Handle<ISampler> _handle;
    private bool _disposed;
    
    public uint Index => _handle.Index;
    public uint Generation => _handle.Generation;
    public bool IsValid => _handle.IsValid && !_disposed;
    
    internal Veldrid.Sampler Native => _nativeSampler;
    
    internal VeldridSampler(Handle<ISampler> handle, Veldrid.Sampler nativeSampler)
    {
        _handle = handle;
        _nativeSampler = nativeSampler ?? throw new ArgumentNullException(nameof(nativeSampler));
    }
    
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        _nativeSampler?.Dispose();
    }
}
```

**Checklist**:
- [ ] All 4 adapter classes compile
- [ ] Native properties are internal only
- [ ] Dispose pattern implemented correctly
- [ ] XML documentation complete

---

### Day 3: VeldridGraphicsDevice Resource Management Integration

**Objective**: Integrate resource pools with VeldridGraphicsDevice, implement actual resource creation

**Changes to**: `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs`

**Additions**:

```csharp
// Add fields to VeldridGraphicsDevice class
private readonly ResourcePool<IBuffer> _bufferPool;
private readonly ResourcePool<ITexture> _texturePool;
private readonly ResourcePool<IFramebuffer> _framebufferPool;
private readonly ResourcePool<ISampler> _samplerPool;

// In constructor, initialize pools:
public VeldridGraphicsDevice(...)
{
    // ... existing code ...
    _bufferPool = new ResourcePool<IBuffer>(256);
    _texturePool = new ResourcePool<ITexture>(256);
    _framebufferPool = new ResourcePool<IFramebuffer>(64);
    _samplerPool = new ResourcePool<ISampler>(32);
}

// Reimplement CreateBuffer with proper pooling:
public Handle<IBuffer> CreateBuffer(in BufferDescription description)
{
    var veldridDesc = new Veldrid.BufferDescription(
        (uint)description.SizeInBytes,
        ConvertBufferUsage(description.Usage));
    
    var nativeBuffer = _factory.CreateBuffer(ref veldridDesc);
    _addDisposable(nativeBuffer);
    
    var wrapper = new VeldridBuffer(
        new Handle<IBuffer>(0, 0), // Placeholder, will be set by pool
        nativeBuffer);
    
    var handle = _bufferPool.Allocate(wrapper);
    return handle;
}

// Similar pattern for CreateTexture, CreateSampler, CreateFramebuffer

// Update Dispose to clean up pools:
protected override void DisposeManagedResources()
{
    _bufferPool?.Dispose();
    _texturePool?.Dispose();
    _framebufferPool?.Dispose();
    _samplerPool?.Dispose();
    base.DisposeManagedResources();
}
```

**Checklist**:
- [ ] VeldridGraphicsDevice constructor initializes pools
- [ ] CreateBuffer returns non-invalid handle
- [ ] CreateTexture returns non-invalid handle
- [ ] CreateSampler returns non-invalid handle
- [ ] CreateFramebuffer returns non-invalid handle
- [ ] Destroy methods validate handles before removal
- [ ] Dispose cleans up all pools
- [ ] Project compiles with zero errors

---

### Day 4: Shader System Foundation

**Objective**: Prepare shader loading infrastructure (not full implementation - deferred to Week 10)

**Deliverables**:

1. **File**: `src/OpenSage.Graphics/Shaders/ShaderSource.cs`

```csharp
using System;

namespace OpenSage.Graphics.Shaders;

/// <summary>Represents compiled shader bytecode for a specific backend.</summary>
public class ShaderSource
{
    /// <summary>Shader stage (Vertex, Fragment, Compute).</summary>
    public ShaderStage Stage { get; }
    
    /// <summary>Compiled bytecode (format depends on backend).</summary>
    public ReadOnlyMemory<byte> Bytecode { get; }
    
    /// <summary>Entry point function name (usually "main").</summary>
    public string EntryPoint { get; }
    
    public ShaderSource(ShaderStage stage, ReadOnlyMemory<byte> bytecode, string entryPoint)
    {
        Stage = stage;
        Bytecode = bytecode;
        EntryPoint = entryPoint ?? throw new ArgumentNullException(nameof(entryPoint));
    }
}

public enum ShaderStage
{
    Vertex = 0,
    Fragment = 1,
    Compute = 2,
    Geometry = 3,
    TessControl = 4,
    TessEval = 5
}
```

2. **File**: `src/OpenSage.Graphics/Shaders/ShaderCompilationCache.cs`

```csharp
using System;
using System.IO;
using System.Collections.Generic;
using System.Text;

namespace OpenSage.Graphics.Shaders;

/// <summary>
/// Cache for cross-compiled shaders.
/// Maps SPIR-V hash → compiled bytecode for current backend.
/// </summary>
public class ShaderCompilationCache
{
    private const string CacheDirName = "ShaderCache";
    private readonly string _cacheDir;
    private readonly string _backendSuffix;
    
    public ShaderCompilationCache(GraphicsBackend backend)
    {
        _backendSuffix = backend.ToString().ToLowerInvariant();
        _cacheDir = Path.Combine(AppContext.BaseDirectory, CacheDirName);
        
        if (!Directory.Exists(_cacheDir))
            Directory.CreateDirectory(_cacheDir);
    }
    
    /// <summary>Generate cache file path for shader with hash.</summary>
    public string GetCachePath(string shaderName, string hash)
    {
        var fileName = $"{shaderName}.{hash}.{_backendSuffix}";
        return Path.Combine(_cacheDir, fileName);
    }
    
    /// <summary>Try to load compiled shader from cache.</summary>
    public bool TryLoadCached(string shaderName, string hash, out byte[] compiled)
    {
        var path = GetCachePath(shaderName, hash);
        if (!File.Exists(path))
        {
            compiled = null;
            return false;
        }
        
        try
        {
            compiled = File.ReadAllBytes(path);
            return true;
        }
        catch
        {
            compiled = null;
            return false;
        }
    }
    
    /// <summary>Save compiled shader to cache.</summary>
    public void SaveCached(string shaderName, string hash, byte[] compiled)
    {
        var path = GetCachePath(shaderName, hash);
        File.WriteAllBytes(path, compiled);
    }
}
```

**Checklist**:
- [ ] ShaderSource enum defined
- [ ] ShaderCompilationCache class compiles
- [ ] Cache directory logic works
- [ ] XML documentation complete

---

### Day 5: Integration Testing and Build Verification

**Objective**: Verify all Week 9 components work together, fix compilation errors

**Tasks**:

1. **Build All Projects**:
   ```bash
   cd src
   dotnet build
   ```
   - Target: 0 compilation errors
   - Acceptable: Warnings from unrelated projects

2. **Unit Tests**:
   - Create `src/OpenSage.Graphics.Tests/PoolingTests.cs`
   - Test ResourcePool allocation/deallocation
   - Test handle generation validation
   - Target: 5+ passing tests

3. **Integration Test**:
   - Create simple integration test that:
     1. Creates VeldridGraphicsDevice
     2. Creates buffer (verify non-invalid handle returned)
     3. Creates texture (verify non-invalid handle returned)
     4. Creates sampler (verify non-invalid handle returned)
     5. Destroys each resource
     6. Verifies handle validation fails after destruction

4. **Documentation**:
   - Update Phase_3_Core_Implementation.md with Week 9 completion
   - Add [x] checkmarks for all completed tasks
   - Record any blockers or delays

5. **Code Review Checklist**:
   - [ ] All new classes have XML documentation
   - [ ] All public methods documented with parameters
   - [ ] Naming follows OpenSAGE conventions (PascalCase types, camelCase fields/parameters)
   - [ ] No trailing whitespace
   - [ ] Allman braces on all blocks
   - [ ] 4-space indentation
   - [ ] No TODO comments without context

---

## Risk Mitigation Strategies

### Risk 1: Handle Generation Overflow
**Problem**: After 4 billion deallocations of same slot, generation wraps to 0 → stale handles become valid

**Mitigation**:
- Use `uint` for generation (4 billion cycles)
- In practice: Won't happen in single session
- Future (Week 13): Consider handle expansion to 64-bit

### Risk 2: Resource Leak in Pool
**Problem**: If resource not properly freed, pool grows unbounded

**Mitigation**:
- All adapters implement `IDisposable`
- VeldridGraphicsDevice tracks via DisposableBase
- Unit tests verify cleanup
- Future: Add pool statistics/monitoring

### Risk 3: Veldrid Backend Differences
**Problem**: Different backends (Metal, Vulkan, OpenGL) might have different resource creation semantics

**Mitigation**:
- Test on Metal (macOS environment)
- Stick to Veldrid abstraction API
- Avoid backend-specific code in adapters
- Use Veldrid.SPIRV for cross-compilation

---

## Success Criteria

| Item | Target | Success |
|------|--------|---------|
| ResourcePool compiles | Yes | ✅ |
| All adapters compile | Yes | ✅ |
| VeldridGraphicsDevice refactored | Yes | ⏳ |
| Unit tests pass | 5+ | ⏳ |
| Integration test passes | 1 | ⏳ |
| Build with 0 errors | Yes | ⏳ |
| Documentation updated | 100% | ⏳ |
| Code review passes | Yes | ⏳ |

---

## Next Steps (Week 10)

- Implement IShaderProgram with Veldrid.SPIRV
- Load embedded SPIR-V resources
- Cross-compile to backend-specific format
- Extract ResourceLayoutDescription from reflection data
- Create shader unit tests

