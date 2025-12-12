# Phase 3 Quick Reference Guide

**Purpose**: Fast lookup for key findings, patterns, and decisions  
**Use**: Bookmark this file for implementation phase reference

---

## 🎯 Core Findings (TL;DR)

| Finding | Root Cause | Solution |
|---------|-----------|----------|
| **GPU Errors After Destroy** | GPU pipeline latency | Async destruction queue + fence wait |
| **Thread-Safety Issues** | API constraints (GL not thread-safe) | Explicit synchronization + locks (Vulkan) or dedicated thread (GL) |
| **Slow State Creation** | Native state objects expensive (0.5-2µs) | Permanent cache with struct keys |
| **Handle Reuse Bugs** | Index reuse without tracking | Generation field + validation on access |
| **Silent Failures** | No error indication | Explicit exceptions (fail-fast) |

---

## 📊 Architecture Decision Matrix

### Problem: How to Create Resources Safely?

```
Decision: Async Destruction Queue with Fences

Alternatives:
1. ❌ Synchronous destroy → blocks GPU wait (slow)
2. ❌ Immediate destroy → GPU use-after-free (crash)
3. ✅ Queue destroy + fence wait → safe + fast

Implementation:
DestructionQueue<T> where:
  - Enqueue(resource, fence) adds to pending list
  - ProcessCompleted() calls Dispose() when fence signals
  - Called once per frame in EndFrame()

Cost: ~0.1% frame time overhead, zero crashes
```

### Problem: How to Prevent Handle Reuse Bugs?

```
Decision: Generation Field in Handle<T>

Alternatives:
1. ❌ Index-only → reused index can access wrong resource
2. ✅ Index + Generation → generation mismatch caught immediately

Implementation:
public struct Handle<T>
{
    public uint Index;      // resource array index
    public uint Generation; // incremented on free
}

Cost: 8 bytes per handle, 0 performance impact
Benefit: Use-after-free immediately caught
```

### Problem: How to Optimize State Creation?

```
Decision: Permanent State Cache with Immutable Keys

Alternatives:
1. ❌ No cache → 10,000 state creates per frame (15ms waste)
2. ❌ LRU eviction → complexity, cold starts, fragmentation
3. ✅ Permanent cache → graphics state is permanent lifetime

Implementation:
StateCache<BlendStateKey, BlendStateDescription> _blendCache;
- Key: immutable readonly struct with all properties
- Cache lookup: O(1) hash table, 10ns
- Cache create: Only on miss (100 creates vs 10,000)

Cost: Memory for ~10,000 entries (negligible, ~5MB)
Benefit: 60x speedup for typical workload
```

---

## 📁 File Creation Checklist

### Week 9 Files (8-10 day sprint)

**Core Infrastructure** (2 files)
- [ ] `src/OpenSage.Graphics/Core/DestructionQueue.cs` (150 lines)
- [ ] `src/OpenSage.Graphics/Core/GraphicsResourceBase.cs` (100 lines)

**Main Adapter** (1 file)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridGraphicsDevice.cs` (600 lines)

**Resource Adapters** (3 files)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridBuffer.cs` (150 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridTexture.cs` (200 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridFramebuffer.cs` (150 lines)

**Graphics Objects** (3 files)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridShaderProgram.cs` (200 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridPipeline.cs` (250 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/VeldridSampler.cs` (100 lines)

**Utilities** (3 files)
- [ ] `src/OpenSage.Graphics/Veldrid/FormatMapper.cs` (200 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/CapabilityChecker.cs` (150 lines)
- [ ] `src/OpenSage.Graphics/Veldrid/ValidationHelpers.cs` (150 lines)

**Tests** (2 files)
- [ ] `src/OpenSage.Graphics.Tests/VeldridGraphicsDeviceTests.cs` (300 lines)
- [ ] `src/OpenSage.Graphics.Tests/TriangleRenderTest.cs` (150 lines)

**Total**: 15 files, ~2,800 lines

---

## 🔑 Key Code Patterns

### Pattern 1: Handle Validation on Every Access
```csharp
// ALWAYS validate handle before use
private IBuffer GetBuffer(Handle<Buffer> handle)
{
    if (!_bufferAllocator.IsValid(handle))
        throw new GraphicsException("Invalid buffer handle");
    return _buffers[handle.Index];
}
```

### Pattern 2: Async Destruction Enqueue
```csharp
// NEVER destroy Veldrid resources directly
public void DestroyBuffer(Handle<Buffer> handle)
{
    var veldridBuffer = _buffers[handle.Index];
    var fence = _currentFence;  // Allocate fence for this submission
    
    _bufferDestructionQueue.Enqueue(veldridBuffer, fence);
    _bufferAllocator.FreeHandle(handle);
    
    // Resource not destroyed yet - GPU still using it
    // ProcessDestructionQueue() called at frame end will destroy
}
```

### Pattern 3: State Cache Lookup
```csharp
// ALWAYS lookup cache before creating state
var blendKey = ExtractBlendState(description);
var blendState = _blendStateCache.GetOrCreate(blendKey, key =>
{
    // Only called on cache miss
    return ConvertToVeldridBlendState(key);
});
```

### Pattern 4: Frame Lifecycle Guard
```csharp
// ALWAYS guard against nested frames
public void BeginFrame()
{
    if (_insideFrame)
        throw new GraphicsException("Nested BeginFrame not allowed");
    
    _commandList.Begin();
    ProcessDestructionQueues();
    _insideFrame = true;
}

public void EndFrame()
{
    if (!_insideFrame)
        throw new GraphicsException("EndFrame without BeginFrame");
    
    _commandList.End();
    _veldridDevice.SubmitCommands(_commandList);
    _insideFrame = false;
}
```

### Pattern 5: Error Translation
```csharp
// ALWAYS catch and translate Veldrid exceptions
public IBuffer CreateBuffer(in BufferDescription desc)
{
    try
    {
        // Validate first
        ValidateBufferDescription(desc);
        
        // Create
        var veldridBuffer = _resourceFactory.CreateBuffer(desc);
        
        // Wrap and return
        var handle = _bufferAllocator.AllocateHandle();
        _buffers[handle.Index] = veldridBuffer;
        return new VeldridBuffer(this, handle, veldridBuffer);
    }
    catch (VeldridException vex)
    {
        throw ErrorTranslator.Translate(vex, "CreateBuffer");
    }
}
```

---

## ⚠️ Critical Gotchas

### Gotcha 1: CommandList Not Thread-Safe
```csharp
// ❌ WRONG - Multiple threads recording to same CommandList
Thread t1 = new(() => _commandList.UpdateBuffer(...));
Thread t2 = new(() => _commandList.SetPipeline(...));
// CRASH: Data race on CommandList state

// ✅ CORRECT - Single-threaded recording
_commandList.Begin();
RecordAllDrawCalls();  // Single thread only
_commandList.End();
```

### Gotcha 2: Destruction Without Queue
```csharp
// ❌ WRONG - Direct destruction
public void DestroyTexture(ITexture tex)
{
    ((VeldridTexture)tex)._veldridTexture.Dispose();
    // CRASH: GPU still reading texture!
}

// ✅ CORRECT - Queued destruction
public void DestroyTexture(ITexture tex)
{
    _textureDestructionQueue.Enqueue(veldridTexture, fence);
    // Returns immediately, destroyed later when safe
}
```

### Gotcha 3: Handle Generation Ignored
```csharp
// ❌ WRONG - No generation check
if (handle.Index < _resources.Count)
{
    return _resources[handle.Index];
    // Bug: Accesses wrong resource if handle freed/reused
}

// ✅ CORRECT - Generation validation
if (_allocator.IsValid(handle))  // Checks index AND generation
{
    return _resources[handle.Index];
}
```

### Gotcha 4: Validation Disabled in Release
```csharp
// Remember: Validation is #if VALIDATE_USAGE
// In release builds, validation is skipped!
// So test thoroughly in debug mode

#if VALIDATE_USAGE
    ValidateBufferDescription(desc);  // Only in debug
#endif

// Result: Release build crashes on misuse
// Solution: Ensure debug builds thorough before release
```

### Gotcha 5: State Cache Unbounded Growth
```csharp
// ⚠️ RISK: Dictionary grows without limit
_blendStateCache[key] = state;  // No eviction

// Mitigation: Monitor in debug, clear on device reset
public void Reset()
{
    _blendStateCache.Clear();  // Called when device reset
}
```

---

## 🧪 Testing Checklist

### Unit Test: Handle Allocation
```csharp
[Test]
public void HandleAllocation_SameIndexDifferentGeneration()
{
    var handle1 = allocator.AllocateHandle();
    allocator.FreeHandle(handle1);
    var handle2 = allocator.AllocateHandle();  // Reuses index
    
    Assert.AreEqual(handle1.Index, handle2.Index);  // Same index
    Assert.AreNotEqual(handle1.Generation, handle2.Generation);  // Different gen
    Assert.False(allocator.IsValid(handle1));  // Old handle invalid
    Assert.True(allocator.IsValid(handle2));   // New handle valid
}
```

### Unit Test: Destruction Queue
```csharp
[Test]
public void DestructionQueue_ProcessesWhenSignaled()
{
    var resource = new MockResource();
    var fence = new MockFence { Signaled = false };
    
    queue.Enqueue(resource, fence);
    queue.ProcessCompleted();
    
    Assert.False(resource.Disposed);  // Not disposed yet
    
    fence.Signaled = true;
    queue.ProcessCompleted();
    
    Assert.True(resource.Disposed);  // Disposed after fence
}
```

### Unit Test: State Caching
```csharp
[Test]
public void StateCache_ReturnsSameObjectForSameKey()
{
    var key = new BlendStateKey(...);
    
    var state1 = cache.GetOrCreate(key, k => CreateBlendState(k));
    var state2 = cache.GetOrCreate(key, k => CreateBlendState(k));
    
    Assert.AreSame(state1, state2);  // Same object (cached)
}
```

### Integration Test: Triangle Rendering
```csharp
[Test]
public void TriangleRender_ProducesValidFramebuffer()
{
    var device = CreateVeldridGraphicsDevice();
    
    // Create resources
    var buffer = device.CreateBuffer(triangleVertexDesc);
    var shader = device.CreateShaderProgram(shaderDesc);
    var pipeline = device.CreatePipeline(pipelineDesc);
    var framebuffer = device.CreateFramebuffer(framebufferDesc);
    
    // Render
    device.BeginFrame();
    device.SetFramebuffer(framebuffer);
    device.SetPipeline(pipeline);
    device.SetVertexBuffer(buffer);
    device.Draw(3, 0);  // Draw triangle
    device.EndFrame();
    
    // Verify
    device.WaitForIdle();
    VerifyFramebufferContainsTriangle(framebuffer);
}
```

---

## 📈 Performance Targets

| Metric | Target | How to Measure |
|--------|--------|----------------|
| **Handle allocation** | < 1 µs | Time 10,000 allocations |
| **Buffer creation** | < 50 µs | Time 100 buffer creates |
| **State cache hit rate** | ≥ 90% | Count hits vs. misses in benchmark |
| **Destruction queue overhead** | < 1% frame | Time frame end processing |
| **Pipeline creation** | < 100 µs (cached) | Time with cache enabled |

---

## 🔗 Documentation Map

| Document | Length | Purpose |
|----------|--------|---------|
| [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md) | 2,200+ lines | Deep technical analysis WHY patterns exist |
| [Phase3-Week9-Implementation-Plan.md](Phase3-Week9-Implementation-Plan.md) | 1,800+ lines | Day-by-day implementation guide HOW to build |
| [Phase3-Session-Summary.md](Phase3-Session-Summary.md) | 600 lines | Progress summary WHAT was done |
| **This file (Quick Reference)** | 400 lines | Fast lookup WHAT to do next |

---

## ⏱️ Implementation Timeline

```
Week 9:
├─ Mon-Tue: Handle system + DestructionQueue (Phase 1)
├─ Wed-Thu: VeldridGraphicsDevice core (Phase 2)
├─ Fri: State caching (Phase 3)
└─ Weekend: Resource adapters start (Phase 4)

Week 10:
├─ Mon-Tue: Resource adapters complete (Phase 4)
├─ Wed-Thu: Shader/Pipeline/Sampler (Phase 5)
├─ Fri: Format mapping + validation (Phases 6-7)
└─ Weekend: Unit tests + integration test (Phase 8)

Result: VeldridGraphicsDevice ready for use
```

---

## 🚀 Launch Checklist (Before Implementation)

- [ ] Read [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md) (understand WHY)
- [ ] Read [Phase3-Week9-Implementation-Plan.md](Phase3-Week9-Implementation-Plan.md) (understand HOW)
- [ ] Review 5 key code patterns above
- [ ] Review 5 critical gotchas above
- [ ] Create project structure (subdirectories)
- [ ] Add references to Veldrid 4.9.0
- [ ] Set up test framework
- [ ] Create first test (handle allocation)

Then proceed to Phase 1 (Day 1) implementation.

---

## 📚 Research Reference

### Key Queries for Fast Lookup

If you need to understand a specific topic again:

- **Thread-safety**: See Query 7 results in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **Async destruction**: See Query 1 + Part 2 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **State caching**: See Query 8 + Part 3 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **Error handling**: See Query 11-13 + Part 4 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **Shader loading**: See Query 14 + Part 5 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **Format mapping**: See Query 15 + Part 6 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)
- **Performance**: See Query 10 + Part 8 in [Phase3-Research-Root-Causes.md](Phase3-Research-Root-Causes.md)

---

## ✅ Success Definition

**Week 9 Complete When**:
- All 15 files created (2,800+ lines)
- VeldridGraphicsDevice compiles (0 errors)
- Unit tests: ≥ 80% coverage
- Triangle renders to framebuffer
- No known crashes or data races
- All public methods documented

**Ready for Week 10 When**:
- Integration tests pass
- Performance benchmarks meet targets
- Code review approved
- Zero known issues

---

**Status**: Research complete, implementation ready. Proceed with Phase 1.

