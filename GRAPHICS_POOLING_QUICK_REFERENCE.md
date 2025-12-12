# Graphics Resource Pooling - Quick Reference

## At-a-Glance API Summary

### ResourcePool<T> Core Methods

```csharp
// Create pool
var pool = new ResourcePool<Texture>(initialCapacity: 256);

// Allocate resource
PoolHandle handle = pool.Allocate(textureResource);
if (!handle.IsValid) /* error */;

// Retrieve resource
if (pool.TryGet(handle, out Texture tex)) { /* use tex */ }

// Release resource
bool released = pool.Release(handle);
if (!released) /* handle was invalid or already released */;

// Check validity
bool isValid = pool.IsValid(handle);

// Get stats
int allocated = pool.AllocatedCount;
int free = pool.FreeSlots;
int capacity = pool.Capacity;

// Cleanup
pool.Clear();         // Dispose all, keep pool
pool.Dispose();       // Dispose all, free pool
```

---

## Handle Conversion Pattern

```csharp
// CREATE: PoolHandle → Handle<T>
var poolHandle = _bufferPool.Allocate(veldridBuffer);
var publicHandle = new Handle<IBuffer>(poolHandle.Index, poolHandle.Generation);

// LOOKUP: Handle<T> → PoolHandle
var poolHandle = new ResourcePool<VeldridLib.DeviceBuffer>.PoolHandle(
    publicHandle.Id,          // Maps to pool index
    publicHandle.Generation   // Maps to generation counter
);

if (_bufferPool.TryGet(poolHandle, out var resource))
{
    // Use resource
}
```

---

## SetRenderTarget() Pattern (Correct Implementation)

```csharp
public void SetRenderTarget(Handle<IFramebuffer> framebuffer)
{
    // Early exit: Invalid handle = backbuffer
    if (!framebuffer.IsValid)
    {
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
        return;
    }

    // Convert handle to pool handle
    var poolHandle = new ResourcePool<VeldridLib.Framebuffer>.PoolHandle(
        framebuffer.Id,
        framebuffer.Generation
    );

    // Lookup with generation validation
    if (_framebufferPool.TryGet(poolHandle, out var vfb))
    {
        _cmdList.SetFramebuffer(vfb);
    }
    else
    {
        // Generation mismatch (use-after-free attempt) or invalid
        // → graceful fallback to backbuffer
        _cmdList.SetFramebuffer(_device.SwapchainFramebuffer);
    }
}
```

---

## Generation Validation Flow

```
ALLOCATE:
  slot = find free or create new
  generations[slot] = 1 (or increment if reused)
  return PoolHandle(index: slot, gen: generations[slot])

RELEASE:
  Dispose(resource)
  resources[slot] = null
  freeSlots.Enqueue(slot)
  generation[slot] will increment on next allocate

REUSE:
  slot = freeSlots.Dequeue()
  generation[slot]++  ← KEY: invalidates old handles
  return PoolHandle(index: slot, gen: generations[slot])

TRYGET:
  if handle.generation != generations[handle.index]
    return false  ← OLD HANDLE REJECTED
  return resource
```

---

## Backbuffer vs Custom Framebuffer

| Aspect | Backbuffer | Custom Framebuffer |
|--------|-----------|-------------------|
| Handle | `Invalid` | Valid (from pool) |
| Creation | Automatic | Explicit `CreateFramebuffer()` |
| Lifetime | Device lifetime | Until `DestroyFramebuffer()` |
| Size | Window size | User-defined |
| Pooled | No | Yes |
| Presents | Auto to screen | No (texture-only) |
| Typical Use | Final output | Post-processing, shadow maps |

**Fallback Rule**: Any invalid/stale framebuffer handle → automatically use backbuffer

---

## Common Patterns

### Pattern 1: Create-Use-Destroy

```csharp
// Create
var texture = device.CreateTexture(new TextureDescription { /* ... */ });

// Use
device.BindTexture(texture, slot: 0, sampler);

// Destroy
device.DestroyTexture(texture);  // texture is now invalid
```

### Pattern 2: Deferred Cleanup

```csharp
// Release after N frames to prevent rapid reallocation
void ReleaseDeferred(Handle<ITexture> handle, int delayFrames = 3)
{
    _pendingReleases.Enqueue((handle, _frameCounter + delayFrames));
}

void UpdateDeferred()
{
    while (_pendingReleases.Count > 0 && _pendingReleases.Peek().Frame <= _frameCounter)
    {
        var (handle, _) = _pendingReleases.Dequeue();
        device.DestroyTexture(handle);
    }
}
```

### Pattern 3: Safe Fallback

```csharp
Handle<ITexture> GetTextureOrDefault(Handle<ITexture> requestedTexture)
{
    return requestedTexture.IsValid ? requestedTexture : _defaultWhiteTexture;
}

device.BindTexture(GetTextureOrDefault(myTexture), slot: 0, sampler);
```

### Pattern 4: Validate Before Bind

```csharp
if (myFramebuffer.IsValid)
{
    device.SetRenderTarget(myFramebuffer);
}
else
{
    Console.WriteLine("Warning: framebuffer handle invalid, using backbuffer");
    device.SetRenderTarget(Handle<IFramebuffer>.Invalid);
}
```

---

## Handle Sentinel Values

```csharp
// Generic invalid handle
Handle<IFramebuffer>.Invalid        // Id = uint.MaxValue, Gen = 0
Handle<ITexture>.Invalid            // Id = uint.MaxValue, Gen = 0

// Explicit check
if (handle.IsValid) { /* valid */ }
else { /* use default/backbuffer */ }

// Pool handle invalid
PoolHandle.Invalid                   // Index = uint.MaxValue, Gen = 0
```

---

## Generation Counter Details

| Scenario | Generation | Valid? | Reason |
|----------|-----------|--------|--------|
| Fresh allocate | 1 | ✓ | First allocation |
| After release + reuse | 2 | ✓ if handle matches | Same slot, new gen |
| After release (old handle) | 1 (stale) | ✗ | Gen mismatch: 1 ≠ 2 |
| Pool slot grows | 1 | ✓ | New slot, gen=1 |
| Handle.Invalid | 0 | ✗ | Sentinel value |

---

## Error Handling Decision Tree

```
SetRenderTarget(handle)
  ├─ Is handle.IsValid?
  │  ├─ NO → use backbuffer
  │  └─ YES → continue
  ├─ Convert to PoolHandle
  ├─ Pool.TryGet(poolHandle)?
  │  ├─ YES → bind framebuffer
  │  └─ NO → use backbuffer (gen mismatch)
```

---

## Thread-Safety Model

**Current**: Single-threaded (no internal locks)

**Safe Pattern**:
```csharp
// Main thread only
device.CreateBuffer(...);
device.DestroyBuffer(...);
device.SetRenderTarget(...);

// Worker threads
// ✗ DO NOT call graphics device methods
// ✓ DO pass handles (they're immutable structs)
```

**Thread-Safe Exchange**:
```csharp
// Thread A: Create handle
_sharedHandle = device.CreateTexture(...);  // Atomic struct assignment

// Thread B: Use handle (read-only)
if (_sharedHandle.IsValid)
{
    // Validate and use
}
```

---

## Properties Explained

```csharp
pool.AllocatedCount    // Total allocated (excluding freed reusable slots)
pool.FreeSlots         // Available slots for reuse
pool.Capacity          // Current array capacity

// Relationship:
AllocatedCount + FreeSlots ≤ Capacity
// When AllocatedCount + FreeSlots == Capacity, next Allocate() triggers GrowCapacity()
```

---

## Troubleshooting

| Symptom | Cause | Fix |
|---------|-------|-----|
| TryGet returns false | Generation mismatch | Handle was released; use new handle |
| Rendering shows artifacts | Stale framebuffer handle | Validate handle before SetRenderTarget |
| Memory leak | Forgot to Release | Call DestroyFramebuffer for each CreateFramebuffer |
| Crash on render | Invalid handle used directly | Always check IsValid before use |
| Texture appears black | Released texture | Check texture handle validity |

---

## Performance Notes

- **Allocation**: O(1) amortized (with doubling growth)
- **TryGet**: O(1) array lookup + generation check
- **Release**: O(1) queue enqueue
- **Memory overhead**: 2 bytes per slot (uint generation + padding)
- **Reuse efficiency**: Prevents malloc/free thrashing via slot reuse

---

## Code Location Reference

| Component | File |
|-----------|------|
| ResourcePool<T> | `src/OpenSage.Graphics/Pooling/ResourcePool.cs` |
| Handle<T> | `src/OpenSage.Graphics/Abstractions/GraphicsHandles.cs` |
| VeldridGraphicsDevice | `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs` |
| Tests | `src/OpenSage.Graphics.Tests/Pooling/ResourcePoolTests.cs` |

---

## Key Formulas

### Generation-Based Validation
```
Valid(handle) := (handle.gen == generations[handle.idx]) AND (resource[handle.idx] != null)
```

### Slot Reuse Prevention
```
Old handle: (index=0, gen=1)
Reuse slot 0: generations[0]++ → gen=2
New handle: (index=0, gen=2)
Lookup with old handle: 1 ≠ 2 → INVALID
```

### Capacity Growth
```
When: allocated + free == capacity
Action: new_capacity = capacity * 2
Result: O(n) resize, then O(1) allocations until next threshold
```
