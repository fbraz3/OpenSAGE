# Graphics Resource Pooling and Framebuffer Binding Analysis

## Overview

OpenSAGE uses **generation-based resource pooling** to safely manage GPU resource lifetimes with automatic use-after-free detection. This document provides complete API coverage, implementation patterns, and production usage examples for the graphics resource pooling system.

---

## 1. ResourcePool<T> Complete API Reference

### 1.1 Core Data Structure

[ResourcePool.cs](src/OpenSage.Graphics/Pooling/ResourcePool.cs) implements a thread-unsafe, generation-aware resource pool:

```csharp
public class ResourcePool<T> : IDisposable where T : class, IDisposable
{
    private T[] _resources;              // Actual resource array
    private uint[] _generations;         // Generation counter per slot
    private Queue<uint> _freeSlots;      // Free slot reuse queue
    private uint _nextId;                // Next slot to allocate

    // Nested handle struct
    public readonly struct PoolHandle : IEquatable<PoolHandle>
    {
        public readonly uint Index;      // Slot index in pool
        public readonly uint Generation; // Generation for validation
        public bool IsValid => Index != uint.MaxValue;
        
        public static PoolHandle Invalid => new(uint.MaxValue, 0);
    }
}
```

**Storage Model**:
- Slots: fixed array with 2x growth on capacity exhaustion
- Generations: parallel array tracking reuse count
- Free queue: FIFO queue of released slot indices
- NextId: allocation cursor (never decreases)

### 1.2 Constructor & Lifecycle

```csharp
// Initialize with desired capacity (default 256)
public ResourcePool(int initialCapacity = 256)
{
    if (initialCapacity <= 0)
        throw new ArgumentException("Initial capacity must be positive", nameof(initialCapacity));
    
    _resources = new T[initialCapacity];
    _generations = new uint[initialCapacity];
    _freeSlots = new Queue<uint>(initialCapacity);
    _nextId = 0;
}

// Dispose releases all resources
public void Dispose()
{
    Clear();
    _resources = null;
    _generations = null;
    _freeSlots = null;
}

// Clear disposes all allocated resources without destroying pool
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
```

### 1.3 Allocate() Method

**Signature**: `public PoolHandle Allocate(T resource)`

**Behavior**:
1. Null-checks resource (throws `ArgumentNullException` if null)
2. Reuses freed slots from `_freeSlots` queue when available
3. Increments generation counter on slot reuse to invalidate stale handles
4. Grows capacity (2x) if all slots exhausted
5. Allocates new slot at `_nextId` cursor if no free slots
6. Sets generation to 1 for fresh slots

```csharp
public PoolHandle Allocate(T resource)
{
    if (resource == null)
        throw new ArgumentNullException(nameof(resource));

    uint idx;

    // Path 1: Reuse freed slot (generation-aware)
    if (_freeSlots.TryDequeue(out idx))
    {
        _resources[idx] = resource;
        _generations[idx]++;  // CRITICAL: Increment to invalidate old handles
        return new PoolHandle(idx, _generations[idx]);
    }

    // Path 2: Allocate new slot with growth if needed
    if (_nextId >= _resources.Length)
        GrowCapacity();  // Doubles capacity with Array.Resize()

    idx = _nextId++;
    _resources[idx] = resource;
    _generations[idx] = 1;  // First generation always 1
    return new PoolHandle(idx, 1);
}

private void GrowCapacity()
{
    var newCapacity = _resources.Length * 2;
    Array.Resize(ref _resources, newCapacity);
    Array.Resize(ref _generations, newCapacity);
}
```

**Generation Semantics**:
- Initial allocation: generation = 1
- Slot reuse: generation incremented (e.g., 1 → 2 → 3...)
- Prevents stale handle reuse through generation mismatch

### 1.4 TryGet() Method

**Signature**: `public bool TryGet(PoolHandle handle, out T resource)`

**Returns**: `true` if handle is valid and resource exists; `false` otherwise

**Validation Checks** (all must pass):
1. Handle.IsValid (not uint.MaxValue)
2. Index in allocated range (< _nextId)
3. Generation matches stored generation[index]
4. Resource slot is non-null

```csharp
public bool TryGet(PoolHandle handle, out T resource)
{
    // Check 1: Handle itself is valid (not Invalid sentinel)
    if (!handle.IsValid || handle.Index >= _nextId)
    {
        resource = default;
        return false;
    }

    // Check 2: Generation matches (prevents use-after-free)
    if (_generations[handle.Index] != handle.Generation)
    {
        // Handle is stale (slot was released and possibly reused)
        resource = default;
        return false;
    }

    // Check 3: Slot contains resource
    resource = _resources[handle.Index];
    return resource != null;
}
```

**Error Handling**: Returns `false` rather than throwing; caller must check return value

**Critical Path**: Generation validation is the ROOT CAUSE prevention for use-after-free

### 1.5 Release() Method

**Signature**: `public bool Release(PoolHandle handle)`

**Returns**: `true` if handle was valid and released; `false` otherwise

**Behavior**:
1. Validates handle via TryGet() (generation check included)
2. Calls Dispose() on resource
3. Nullifies slot
4. Enqueues slot index for reuse
5. Generation will auto-increment on next Allocate()

```csharp
public bool Release(PoolHandle handle)
{
    if (!TryGet(handle, out var resource))
        return false;  // Invalid handle - nothing to release

    resource?.Dispose();           // Dispose GPU resource
    _resources[handle.Index] = null;  // Clear slot
    _freeSlots.Enqueue(handle.Index); // Return for reuse
    return true;
}
```

**Reuse Flow**:
```
Release(handle1)  →  Enqueue slot 0  →  generation[0] still 1
Allocate(resource2)  →  Dequeue slot 0  →  generation[0]++ = 2  →  Return (index=0, gen=2)
```

Now handle1 (index=0, gen=1) is stale; only handle2 (index=0, gen=2) is valid.

### 1.6 IsValid() Method

**Signature**: `public bool IsValid(PoolHandle handle)`

**Returns**: `true` if handle is currently valid; `false` if released or invalid

**Validation**:
```csharp
public bool IsValid(PoolHandle handle)
{
    return handle.IsValid &&                    // Not Invalid sentinel
           handle.Index < _nextId &&            // In allocated range
           _generations[handle.Index] == handle.Generation &&  // Gen match
           _resources[handle.Index] != null;    // Slot populated
}
```

**Use Case**: Can be called safely to pre-check before TryGet()

### 1.7 Properties

```csharp
// Total allocated resources (excluding reused slots in queue)
public int AllocatedCount => (int)(_nextId - _freeSlots.Count);

// Available free slots for reuse
public int FreeSlots => _freeSlots.Count;

// Current pool capacity
public int Capacity => _resources.Length;
```

**Memory Calculation**:
```
AllocatedCount + FreeSlots = _nextId
Example: _nextId=100, FreeSlots=20 → AllocatedCount=80
```

---

## 2. Handle<T> to ResourcePool.PoolHandle Conversion

### 2.1 The Two Handle Types

OpenSAGE has **two parallel handle systems**:

1. **Handle<T>** (`GraphicsHandles.cs`): Public type-safe graphics handles
   - Used in IGraphicsDevice public API
   - Wraps uint Id + uint Generation
   - Implements IEquatable, == operator, GetHashCode

2. **ResourcePool.PoolHandle** (`ResourcePool.cs`): Internal pool handles
   - Used internally by ResourcePool<T>
   - Wraps uint Index + uint Generation
   - Simpler struct for pool bookkeeping

### 2.2 Conversion Logic

The VeldridGraphicsDevice maps handles bidirectionally:

**Handle<T> → PoolHandle** (on lookup):
```csharp
// When you want to release a resource from public API:
public void DestroyBuffer(Handle<IBuffer> buffer)
{
    if (!buffer.IsValid)
        return;

    // Reconstruct PoolHandle from Handle's Id field
    // (Handle.Id actually stores pool index, Handle.Generation is the generation)
    var poolHandle = new ResourcePool<VeldridLib.DeviceBuffer>.PoolHandle(
        buffer.Id,        // maps to pool slot index
        buffer.Generation // maps to generation counter
    );
    
    _bufferPool.Release(poolHandle);
}
```

**PoolHandle → Handle<T>** (on creation):
```csharp
public Handle<IBuffer> CreateBuffer(Resources.BufferDescription desc, ReadOnlySpan<byte> data = default)
{
    var buf = _device.ResourceFactory.CreateBuffer(vDesc);
    
    // Allocate from pool
    var poolHandle = _bufferPool.Allocate(buf);
    
    // Convert: PoolHandle.Index becomes Handle.Id (public API uses "Id")
    // PoolHandle.Generation becomes Handle.Generation
    return new Handle<IBuffer>(poolHandle.Index, poolHandle.Generation);
}
```

### 2.3 Generation Validation in Handle<T>

The Handle<T> struct validates against IGraphicsResource:

```csharp
public readonly struct Handle<T> : IEquatable<Handle<T>>
    where T : IGraphicsResource
{
    private const uint InvalidId = uint.MaxValue;
    private readonly uint _id;
    private readonly uint _generation;

    public bool IsValid => _id != InvalidId;

    // Validates handle matches resource's current generation
    public bool IsValidFor(IGraphicsResource resource)
    {
        return IsValid && 
               _id == resource.Id && 
               _generation == resource.Generation;
    }

    // Throws if generation mismatch detected
    public void ValidateOrThrow(IGraphicsResource resource)
    {
        if (_id != resource.Id || _generation != resource.Generation)
        {
            throw new GraphicsException(
                $"Handle is invalid. Resource has been disposed or reallocated. " +
                $"Expected ID: {_id}, Generation: {_generation}; " +
                $"Current ID: {resource.Id}, Generation: {resource.Generation}");
        }
    }
}
```

---

## 3. SetRenderTarget() Implementation Patterns

### 3.1 Current Implementation (Veldrid Adapter)

**Location**: [VeldridGraphicsDevice.cs](src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs#L304-L315)

```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    if (framebuffer.IsValid && _framebuffers.TryGetValue(framebuffer.Id, out var obj) && obj is VeldridLib.Framebuffer fb)
    {
        _currentFramebuffer = fb;
        _cmdList.SetFramebuffer(fb);
    }
    else
    {
        // Fall back to backbuffer
        _currentFramebuffer = _device.SwapchainFramebuffer;
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
    }
}
```

**Current Issues**:
1. Uses undefined `_framebuffers` dictionary (should be `_framebufferPool`)
2. Missing integration with ResourcePool
3. No generation validation via TryGet()

### 3.2 Correct Implementation Pattern

**Correct approach using ResourcePool**:

```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    // Case 1: Invalid handle → use backbuffer (default)
    if (!framebuffer.IsValid)
    {
        _currentFramebuffer = _device.SwapchainFramebuffer;
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
        return;
    }

    // Case 2: Valid handle → lookup in pool with generation validation
    var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
        framebuffer.Id,
        framebuffer.Generation
    );

    if (_framebufferPool.TryGet(poolHandle, out var veldridFramebuffer))
    {
        _currentFramebuffer = veldridFramebuffer;
        _cmdList.SetFramebuffer(veldridFramebuffer);
    }
    else
    {
        // Generation mismatch or invalid slot → use backbuffer
        _currentFramebuffer = _device.SwapchainFramebuffer;
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
    }
}
```

**Error Handling Hierarchy**:
1. **IsValid check** (quick reject): Catches Handle.Invalid sentinel
2. **Pool TryGet()** (generation validation): Catches use-after-free
3. **Fallback to backbuffer**: Safe degradation on errors

### 3.3 Framebuffer Lifecycle Management

**Complete flow from creation to rendering**:

```csharp
// 1. CREATE - Allocate framebuffer resource and handle
public Handle<IFramebuffer> CreateFramebuffer(Resources.FramebufferDescription desc)
{
    // Create underlying Veldrid framebuffer
    var vfb = _device.ResourceFactory.CreateFramebuffer(new VeldridLib.FramebufferDescription(
        depthTarget: /* ... */,
        colorTargets: new[] { /* ... */ }
    ));

    // Allocate from pool with generation tracking
    var poolHandle = _framebufferPool.Allocate(vfb);

    // Return public Handle<IFramebuffer> (using pool slot as ID)
    return new Handle<IFramebuffer>(poolHandle.Index, poolHandle.Generation);
}

// 2. RENDER - Use framebuffer
void RenderToTexture(Handle<IFramebuffer> framebuffer)
{
    device.SetRenderTarget(framebuffer);      // Sets current framebuffer
    device.ClearRenderTarget(...);            // Clears active target
    device.DrawIndexed(...);                  // Renders to active target
}

// 3. DESTROY - Release framebuffer and invalidate handle
public void DestroyFramebuffer(Handle<IFramebuffer> framebuffer)
{
    if (!framebuffer.IsValid)
        return;

    var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
        framebuffer.Id,
        framebuffer.Generation
    );

    _framebufferPool.Release(poolHandle);  // Disposes Veldrid.Framebuffer
}

// 4. ATTEMPT REUSE (STALE HANDLE) - Will fail with generation mismatch
void StaleHandleRejection()
{
    var oldHandle = framebuffer1;           // Generation 1
    DestroyFramebuffer(framebuffer1);       // Releases pool slot 0
    
    var framebuffer2 = CreateFramebuffer(...); // Reuses slot 0, generation 2
    
    device.SetRenderTarget(oldHandle);      // TryGet fails: gen 1 != gen 2
    // → Automatically falls back to backbuffer
}
```

### 3.4 Backbuffer Detection

**Backbuffer is detected three ways**:

1. **Invalid Handle**:
   ```csharp
   var backbuffer = Handle<IFramebuffer>.Invalid;
   device.SetRenderTarget(backbuffer);  // Always uses swapchain
   ```

2. **Explicit Sentinel**:
   ```csharp
   // API convention: Invalid handle = render to backbuffer
   device.SetRenderTarget(default);  // Same as Handle<IFramebuffer>.Invalid
   ```

3. **Failed Pool Lookup**:
   ```csharp
   var staleHandle = /* released framebuffer */;
   device.SetRenderTarget(staleHandle);  // Generation mismatch → backbuffer fallback
   ```

**Default Target**:
- Backbuffer = `_device.SwapchainFramebuffer` (Veldrid term)
- Never pooled (always exists for lifetime of GraphicsDevice)
- No generation tracking needed

---

## 4. Backbuffer vs Custom Framebuffer Management

### 4.1 Backbuffer Characteristics

**Veldrid SwapchainFramebuffer**:
- Automatically created by GraphicsDevice
- Size matches window/swapchain resolution
- Reused every frame (no disposal)
- Presents to screen after frame submission
- Lifetime = GraphicsDevice lifetime

**Detection in code**:
```csharp
bool IsBackbuffer(VeldridLib.Framebuffer fb)
{
    return fb == _device.SwapchainFramebuffer;
}
```

### 4.2 Custom Framebuffer (Texture Target)

**Creation**:
```csharp
// Create color and depth textures
var colorTex = _device.ResourceFactory.CreateTexture(
    TextureDescription.Texture2D(
        width: 1024,
        height: 768,
        mipLevels: 1,
        arrayLayers: 1,
        format: PixelFormat.R8_G8_B8_A8_UNorm,
        usage: TextureUsage.RenderTarget | TextureUsage.Sampled
    )
);

var depthTex = _device.ResourceFactory.CreateTexture(
    TextureDescription.Texture2D(
        width: 1024,
        height: 768,
        mipLevels: 1,
        arrayLayers: 1,
        format: PixelFormat.D24_UNorm_S8_UInt,
        usage: TextureUsage.DepthStencil
    )
);

// Create framebuffer from textures
var fb = _device.ResourceFactory.CreateFramebuffer(
    new VeldridLib.FramebufferDescription(
        depthTarget: depthTex,
        colorTargets: new[] { colorTex }
    )
);

// Pool it
var poolHandle = _framebufferPool.Allocate(fb);
var handle = new Handle<IFramebuffer>(poolHandle.Index, poolHandle.Generation);
```

**Usage Pattern**:
```csharp
// Render to texture
device.SetRenderTarget(offscreenHandle);
device.ClearRenderTarget(clearColor);
device.DrawIndexed(...);

// Then render that texture to backbuffer
device.SetRenderTarget(Handle<IFramebuffer>.Invalid);  // Back to screen
device.BindTexture(offscreenColorTexture, slot: 0, sampler);
device.DrawIndexed(...);
```

### 4.3 When to Use Which

| Scenario | Target | Why |
|----------|--------|-----|
| Normal scene rendering | Backbuffer | Output directly to screen; no extra memory |
| Post-processing pass | Custom FBO | Render scene to texture, apply effects, composite |
| Shadow map generation | Custom FBO | Depth-only render target; reused each frame |
| Water reflection/refraction | Custom FBO | Render reflections to texture; combine in final pass |
| UI overlay rendering | Backbuffer or custom | UI can render directly or to texture for composition |

---

## 5. Handle Validation and Sentinel Values

### 5.1 Handle<T>.Invalid Sentinel

**Definition**:
```csharp
public static Handle<T> Invalid => default;  // Id = uint.MaxValue, Generation = 0
```

**Properties**:
```csharp
public bool IsValid => _id != InvalidId;  // Returns false for Invalid
```

**Uses**:
1. Render target = Invalid → use backbuffer
2. Texture handle = Invalid → skip texture binding
3. Sampler = Invalid → use default sampler

### 5.2 ResourcePool.PoolHandle.Invalid Sentinel

**Definition**:
```csharp
public static PoolHandle Invalid => new(uint.MaxValue, 0);

public bool IsValid => Index != uint.MaxValue;
```

**Matching with Handle<T>.Invalid**:
```csharp
// Both use uint.MaxValue as sentinel
var handle1 = Handle<IFramebuffer>.Invalid;      // Id = uint.MaxValue
var poolHandle = new ResourcePool<FB>.PoolHandle(handle1.Id, handle1.Generation);
// poolHandle.IsValid == false ✓
```

### 5.3 Validation Patterns

**Pattern 1: Pre-check before TryGet()**
```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    if (!framebuffer.IsValid)
    {
        // Use backbuffer immediately
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
        return;
    }

    // Only proceed if potentially valid
    var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
        framebuffer.Id,
        framebuffer.Generation
    );

    if (_framebufferPool.TryGet(poolHandle, out var fb))
    {
        _cmdList.SetFramebuffer(fb);
    }
    else
    {
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
    }
}
```

**Pattern 2: ValidateOrThrow (strict validation)**
```csharp
public void SetRenderTargetStrict(Handle<IFramebuffer> framebuffer)
{
    var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
        framebuffer.Id,
        framebuffer.Generation
    );

    // Throws GraphicsException if generation mismatch
    if (!_framebufferPool.TryGet(poolHandle, out var fb))
    {
        throw new GraphicsException(
            $"Invalid framebuffer handle (generation mismatch). " +
            $"Handle may have been released.");
    }

    _cmdList.SetFramebuffer(fb);
}
```

**Pattern 3: Graceful degradation (recommended)**
```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    // Start with backbuffer as default
    var target = _device.SwapchainFramebuffer;

    // Try to use custom target if provided and valid
    if (framebuffer.IsValid)
    {
        var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
            framebuffer.Id,
            framebuffer.Generation
        );

        if (_framebufferPool.TryGet(poolHandle, out var fb))
        {
            target = fb;
        }
    }

    _cmdList.SetFramebuffer(target);
}
```

---

## 6. Resource Lifetime Management

### 6.1 Allocation Timing

**When to Allocate**:
1. On explicit resource creation request
2. During initialization (e.g., shadow map FBOs)
3. On demand when size/format changes

**Allocation in VeldridGraphicsDevice**:
```csharp
public Handle<IBuffer> CreateBuffer(Resources.BufferDescription desc, ReadOnlySpan<byte> data = default)
{
    // Step 1: Create Veldrid resource
    var buf = _device.ResourceFactory.CreateBuffer(/* ... */);
    
    // Step 2: Pool immediately (takes ownership of lifetime)
    var poolHandle = _bufferPool.Allocate(buf);
    
    // Step 3: Return handle to caller
    return new Handle<IBuffer>(poolHandle.Index, poolHandle.Generation);
    
    // From this point: caller owns Handle, pool owns Veldrid resource
}
```

### 6.2 Release Timing

**When to Release**:
1. On explicit resource destruction request
2. When size/format changes (recreate with different params)
3. During cleanup before shutdown

**Deferred Release Pattern** (for reuse):
```csharp
private readonly Queue<(Handle<ITexture>, uint)> _pendingReleases = new();
private const int FramesBeforeFreeingUnused = 3;  // Delay 3 frames

void ReleaseTextureDeferred(Handle<ITexture> texture)
{
    _pendingReleases.Enqueue((texture, _frameCounter + FramesBeforeFreeingUnused));
}

void UpdatePendingReleases()
{
    while (_pendingReleases.Count > 0 && _pendingReleases.Peek().Item2 <= _frameCounter)
    {
        var (handle, _) = _pendingReleases.Dequeue();
        
        var poolHandle = new ResourcePool<VeldridLib.Texture>.PoolHandle(
            handle.Id,
            handle.Generation
        );
        
        _texturePool.Release(poolHandle);
    }
}
```

### 6.3 Dispose vs Release Semantics

**IDisposable Chain**:
```
VeldridGraphicsDevice.Dispose()
  → _bufferPool.Dispose()
    → Calls Clear()
      → For each resource: resource.Dispose()
      → Disposes all Veldrid buffers
  → _texturePool.Dispose()
    → Disposes all Veldrid textures
  → _framebufferPool.Dispose()
    → Disposes all Veldrid framebuffers
```

**Manual Resource Cleanup**:
```csharp
// User-initiated cleanup (before device destruction)
device.DestroyBuffer(myBuffer);    // Calls _bufferPool.Release()
device.DestroyTexture(myTexture);  // Calls _texturePool.Release()
device.DestroyFramebuffer(myFbo);  // Calls _framebufferPool.Release()

// Automatic cleanup
device.Dispose();  // Calls Clear() on all pools
```

### 6.4 Resource Reuse Strategy

**Slot Reuse (Generation-Safe)**:
```
Frame 1:
  buffer1 = CreateBuffer(1024 bytes)  // Slot 0, Gen 1
  
Frame 10:
  DestroyBuffer(buffer1)              // Release slot 0
  buffer2 = CreateBuffer(1024 bytes)  // Reuse slot 0, Gen 2
  
Frame 15:
  // buffer1 is now INVALID (Gen 1 != stored Gen 2)
  device.BindVertexBuffer(buffer1)    // TryGet returns false
  // Silently skipped or error (depending on implementation)
```

**Memory Efficiency**:
```csharp
// Instead of:
for (int i = 0; i < 1000; i++)
{
    var tex = device.CreateTexture(...);     // 1000 allocations
    device.DestroyTexture(tex);              // 1000 deallocations
}

// Do:
var texPool = new List<Handle<ITexture>>();
for (int i = 0; i < 1000; i++)
{
    // Reuses same pool slots via generation increment
    var tex = device.CreateTexture(...);
    texPool.Add(tex);
}

foreach (var tex in texPool)
{
    device.DestroyTexture(tex);
}
```

---

## 7. Thread-Safety Considerations

### 7.1 ResourcePool<T> Thread-Safety

**Current Status**: **NOT thread-safe**

```csharp
// Not safe for concurrent access:
// - _resources array can be resized
// - _generations array can be resized
// - _freeSlots queue can be modified
// - _nextId can be incremented
```

### 7.2 Usage Pattern: Single-Threaded Rendering

**Standard OpenSAGE pattern**:
```
Main Thread
  ├─ Game Logic (5Hz)
  │   └─ Can create/destroy resources
  └─ Render Loop (60Hz)
      └─ Can only use resources (read-only)
      
Worker Threads (if any)
  └─ MUST NOT call graphics device methods
```

**Thread-Safe Wrapping** (if needed for future):
```csharp
public class ThreadSafeResourcePool<T> where T : class, IDisposable
{
    private readonly ResourcePool<T> _pool;
    private readonly object _lock = new object();

    public PoolHandle Allocate(T resource)
    {
        lock (_lock)
        {
            return _pool.Allocate(resource);
        }
    }

    public bool TryGet(PoolHandle handle, out T resource)
    {
        lock (_lock)
        {
            return _pool.TryGet(handle, out resource);
        }
    }

    public bool Release(PoolHandle handle)
    {
        lock (_lock)
        {
            return _pool.Release(handle);
        }
    }
}
```

### 7.3 Safe Resource Exchange Between Threads

**Pattern: Thread-safe handle passing**:
```csharp
// Thread A: Create resource
var texHandle = device.CreateTexture(desc);
_sharedHandle = texHandle;  // Atomic assignment of struct

// Thread B: Use resource
var poolHandle = new ResourcePool<Texture>.PoolHandle(
    _sharedHandle.Id,
    _sharedHandle.Generation
);

if (texturePool.TryGet(poolHandle, out var tex))
{
    // Use tex safely (read-only)
}
```

**Why Safe**: Handle is immutable struct; no synchronization needed for passing struct values

---

## 8. Production Code Examples

### 8.1 Complete Framebuffer Management

**Example: Shadow Map Rendering**

```csharp
public class ShadowMapRenderer : DisposableBase
{
    private readonly IGraphicsDevice _device;
    private Handle<IFramebuffer> _shadowMapFbo;
    private Handle<ITexture> _shadowMapDepth;
    private const uint ShadowResolution = 2048;

    public ShadowMapRenderer(IGraphicsDevice device)
    {
        _device = device;
        InitializeShadowMap();
    }

    private void InitializeShadowMap()
    {
        // Create depth texture
        _shadowMapDepth = _device.CreateTexture(new TextureDescription
        {
            Width = ShadowResolution,
            Height = ShadowResolution,
            Format = PixelFormat.D32_Float,
            IsRenderTarget = true,
            IsShaderResource = true
        });

        // Create framebuffer
        _shadowMapFbo = _device.CreateFramebuffer(new FramebufferDescription
        {
            DepthTarget = _shadowMapDepth
            // No color targets for depth-only rendering
        });
    }

    public void RenderShadowMap(Light light, IEnumerable<Drawable> objects)
    {
        // Validate handle before use
        if (!_shadowMapFbo.IsValid)
        {
            Console.WriteLine("Warning: Shadow map handle invalid, skipping shadow pass");
            return;
        }

        // Set render target
        _device.SetRenderTarget(_shadowMapFbo);

        // Clear depth
        _device.ClearRenderTarget(
            clearColor: Vector4.One,
            clearDepth: 1.0f,
            colorMask: false,   // Don't clear color (there isn't any)
            depthMask: true
        );

        // Render shadow casters
        foreach (var obj in objects)
        {
            if (obj.CastsShadow)
            {
                obj.RenderShadowDepth(_device);
            }
        }
    }

    public Handle<ITexture> GetShadowMapDepth() => _shadowMapDepth;

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _device.DestroyFramebuffer(_shadowMapFbo);
            _device.DestroyTexture(_shadowMapDepth);
        }
        base.Dispose(disposing);
    }
}
```

**Usage**:
```csharp
var shadowRenderer = new ShadowMapRenderer(device);

// Each frame
shadowRenderer.RenderShadowMap(mainLight, sceneMeshes);

// Later when light changes
shadowRenderer.Dispose();  // Clean up old shadow map
shadowRenderer = new ShadowMapRenderer(device);  // Create new one
```

### 8.2 Dynamic Render Target Resizing

**Example: Water Reflection/Refraction**

```csharp
public class WaterRenderTarget : DisposableBase
{
    private readonly IGraphicsDevice _device;
    private Handle<IFramebuffer> _reflectionFbo;
    private Handle<ITexture> _reflectionTexture;
    private Handle<ITexture> _reflectionDepth;
    private Size _currentSize;

    public Handle<ITexture> ReflectionTexture => _reflectionTexture;

    public WaterRenderTarget(IGraphicsDevice device)
    {
        _device = device;
        _currentSize = default;
    }

    // Recreate FBO if window was resized
    public void EnsureSize(in Size newSize)
    {
        if (_currentSize == newSize)
            return;

        _currentSize = newSize;

        // Clean up old resources
        if (_reflectionFbo.IsValid)
            RemoveAndDispose(ref _reflectionFbo);
        if (_reflectionTexture.IsValid)
            RemoveAndDispose(ref _reflectionTexture);
        if (_reflectionDepth.IsValid)
            RemoveAndDispose(ref _reflectionDepth);

        // Create new resources
        _reflectionTexture = _device.CreateTexture(new TextureDescription
        {
            Width = (uint)newSize.Width,
            Height = (uint)newSize.Height,
            Format = PixelFormat.R8G8B8A8_UNorm,
            IsRenderTarget = true,
            IsShaderResource = true
        });

        _reflectionDepth = _device.CreateTexture(new TextureDescription
        {
            Width = (uint)newSize.Width,
            Height = (uint)newSize.Height,
            Format = PixelFormat.D24_UNorm_S8_UInt,
            IsRenderTarget = true
        });

        _reflectionFbo = _device.CreateFramebuffer(new FramebufferDescription
        {
            ColorTargets = new[] { _reflectionTexture },
            DepthTarget = _reflectionDepth
        });
    }

    public void Render(Action<IGraphicsDevice> renderScene)
    {
        if (!_reflectionFbo.IsValid)
            return;

        _device.SetRenderTarget(_reflectionFbo);
        _device.ClearRenderTarget(new Vector4(0, 0.5f, 1, 1), 1.0f);

        renderScene(_device);
    }

    private void RemoveAndDispose(ref Handle<IFramebuffer> handle)
    {
        if (handle.IsValid)
        {
            _device.DestroyFramebuffer(handle);
            handle = Handle<IFramebuffer>.Invalid;
        }
    }

    private void RemoveAndDispose(ref Handle<ITexture> handle)
    {
        if (handle.IsValid)
        {
            _device.DestroyTexture(handle);
            handle = Handle<ITexture>.Invalid;
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            RemoveAndDispose(ref _reflectionFbo);
            RemoveAndDispose(ref _reflectionTexture);
            RemoveAndDispose(ref _reflectionDepth);
        }
        base.Dispose(disposing);
    }
}
```

**Usage**:
```csharp
var waterRT = new WaterRenderTarget(device);

// Each frame
waterRT.EnsureSize(window.ClientSize);
waterRT.Render(device => {
    // Render reflection scene
});

// Use reflection texture in main rendering
device.BindTexture(waterRT.ReflectionTexture, slot: 0, sampler);
device.DrawIndexed(...);
```

### 8.3 Handle Validation in Binding Operations

**Example: Safe Texture Binding**

```csharp
public class MaterialBinder
{
    private readonly IGraphicsDevice _device;
    private Handle<ITexture> _diffuseTexture;
    private Handle<ITexture> _normalTexture;

    public void SetDiffuseTexture(Handle<ITexture> texture)
    {
        _diffuseTexture = texture;
    }

    public void Apply(Handle<ISampler> sampler)
    {
        // Bind diffuse if handle is valid
        if (_diffuseTexture.IsValid)
        {
            _device.BindTexture(_diffuseTexture, slot: 0, sampler);
        }
        else
        {
            // Bind default white texture instead
            _device.BindTexture(Handle<ITexture>.Invalid, slot: 0, sampler);
        }

        // Bind normal map with fallback
        if (_normalTexture.IsValid)
        {
            _device.BindTexture(_normalTexture, slot: 1, sampler);
        }
        else
        {
            // Bind default flat normal (0, 0, 1)
            _device.BindTexture(Handle<ITexture>.Invalid, slot: 1, sampler);
        }
    }

    public void SetTextureDeferredCleanup(Handle<ITexture> newTexture)
    {
        // Release old texture after N frames (prevent thrashing)
        if (_diffuseTexture.IsValid)
        {
            ScheduleDeferred(_diffuseTexture);
        }

        _diffuseTexture = newTexture;
    }

    private void ScheduleDeferred(Handle<ITexture> handle)
    {
        // Implementation: add to deferred cleanup queue
        // Released after 3 frames to prevent rapid reallocation
    }
}
```

### 8.4 Error Handling and Recovery

**Example: Robust Framebuffer Setup**

```csharp
public class RenderPipeline
{
    private readonly IGraphicsDevice _device;
    private Handle<IFramebuffer> _mainPassFbo;
    private Handle<IFramebuffer> _postProcessFbo;

    public bool Initialize(uint width, uint height)
    {
        try
        {
            if (!CreateMainPassFramebuffer(width, height))
                return false;

            if (!CreatePostProcessFramebuffer(width, height))
            {
                // Cleanup on partial failure
                _device.DestroyFramebuffer(_mainPassFbo);
                return false;
            }

            return true;
        }
        catch (GraphicsException ex)
        {
            Console.WriteLine($"Graphics initialization failed: {ex.Message}");
            return false;
        }
    }

    private bool CreateMainPassFramebuffer(uint width, uint height)
    {
        try
        {
            var colorTex = _device.CreateTexture(new TextureDescription
            {
                Width = width,
                Height = height,
                Format = PixelFormat.R8G8B8A8_UNorm,
                IsRenderTarget = true,
                IsShaderResource = true
            });

            var depthTex = _device.CreateTexture(new TextureDescription
            {
                Width = width,
                Height = height,
                Format = PixelFormat.D24_UNorm_S8_UInt,
                IsRenderTarget = true
            });

            _mainPassFbo = _device.CreateFramebuffer(new FramebufferDescription
            {
                ColorTargets = new[] { colorTex },
                DepthTarget = depthTex
            });

            // Validate creation succeeded
            if (!_mainPassFbo.IsValid)
            {
                Console.WriteLine("Failed to create main pass framebuffer");
                _device.DestroyTexture(colorTex);
                _device.DestroyTexture(depthTex);
                return false;
            }

            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Main pass framebuffer creation error: {ex.Message}");
            return false;
        }
    }

    private bool CreatePostProcessFramebuffer(uint width, uint height)
    {
        // Similar to CreateMainPassFramebuffer
        // ...
        return true;
    }

    public void Render()
    {
        // Main rendering pass
        if (!_mainPassFbo.IsValid)
        {
            Console.WriteLine("Error: Main pass framebuffer is invalid");
            return;  // Skip frame
        }

        _device.SetRenderTarget(_mainPassFbo);
        _device.ClearRenderTarget(Vector4.Zero);
        // ... render objects ...

        // Post-process pass
        if (!_postProcessFbo.IsValid)
        {
            Console.WriteLine("Warning: Post-process framebuffer invalid, skipping");
            _device.SetRenderTarget(Handle<IFramebuffer>.Invalid);  // Back to backbuffer
        }
        else
        {
            _device.SetRenderTarget(_postProcessFbo);
            _device.BindTexture(_mainPassFbo.ColorTargets[0], 0, sampler);
            // ... apply post-process ...
        }

        // Final composite to backbuffer
        _device.SetRenderTarget(Handle<IFramebuffer>.Invalid);
        // ... render to screen ...
    }
}
```

### 8.5 Unit Test Patterns

**See**: [ResourcePoolTests.cs](src/OpenSage.Graphics.Tests/Pooling/ResourcePoolTests.cs)

Key test patterns:

```csharp
[Test]
public void Generation_PreventsUseAfterFree()
{
    var pool = new ResourcePool<Texture>(16);
    var tex1 = new MockTexture();
    var tex2 = new MockTexture();

    // Allocate and release
    var handle1 = pool.Allocate(tex1);
    pool.Release(handle1);

    // Reuse same slot
    var handle2 = pool.Allocate(tex2);

    // Old handle should be invalid (gen mismatch)
    Assert.That(pool.TryGet(handle1, out _), Is.False);
    
    // New handle should be valid
    Assert.That(pool.TryGet(handle2, out var retrieved), Is.True);
    Assert.That(retrieved, Is.SameAs(tex2));
}

[Test]
public void IsValid_ReflectsCurrentState()
{
    var pool = new ResourcePool<Texture>(16);
    var tex = new MockTexture();
    var handle = pool.Allocate(tex);

    Assert.That(pool.IsValid(handle), Is.True);

    pool.Release(handle);

    Assert.That(pool.IsValid(handle), Is.False);
}
```

---

## Summary Table: Resource Pool API

| Method | Purpose | Returns | Errors |
|--------|---------|---------|--------|
| `Allocate(T)` | Add resource to pool | `PoolHandle` | Throws if null |
| `TryGet(handle, out T)` | Retrieve resource | `bool` | Returns false on invalid/stale |
| `Release(handle)` | Remove & dispose | `bool` | Returns false if invalid |
| `IsValid(handle)` | Check validity | `bool` | Never throws |
| `Clear()` | Dispose all | — | Never throws |
| `Dispose()` | Cleanup pool | — | Idempotent |

---

## Key Takeaways

1. **Generation-based validation** is the ROOT CAUSE prevention for use-after-free
2. **SetRenderTarget()** must validate handles via pool TryGet() before binding
3. **Backbuffer** is the safe fallback for any invalid framebuffer handle
4. **Handle conversion** maps pool index→Handle.Id, generation→Handle.Generation
5. **Resource lifetime** follows: allocate → use → release → reuse (with generation bump)
6. **Thread-safety** requires external locking; ResourcePool is single-threaded
7. **Error handling** should use graceful degradation (fallback to backbuffer)
