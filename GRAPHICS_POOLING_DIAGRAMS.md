# Graphics Resource Pooling - Visual Diagrams & State Machines

## 1. Resource Pool Internal State

### Pool Slot Array Visualization

```
ResourcePool<T> Internal State
═════════════════════════════════════════════════════════════════

                 _resources array              _generations array
                 ┌──────────────┐              ┌──────────────┐
         slot 0: │  Texture[0]  │              │      1       │
                 ├──────────────┤              ├──────────────┤
         slot 1: │    null      │              │      2       │  ← Reused (was gen 1)
                 ├──────────────┤              ├──────────────┤
         slot 2: │  Texture[2]  │              │      1       │
                 ├──────────────┤              ├──────────────┤
         slot 3: │  Texture[3]  │              │      3       │  ← Reused twice
                 ├──────────────┤              ├──────────────┤
         slot 4: │    null      │              │      1       │  ← Recently freed
                 └──────────────┘              └──────────────┘
         
         _nextId = 5 (next allocation slot if no free)
         _freeSlots queue = [1, 4]  (reusable slots)
         
Meaning:
  • Slot 0: Valid (gen=1, resource exists)
  • Slot 1: Free for reuse (gen already incremented to 2)
  • Slot 2: Valid (gen=1, resource exists)
  • Slot 3: Valid (gen=3, reused multiple times)
  • Slot 4: Free for reuse (gen will increment to 2)
```

### Memory Layout Example

```
Pool with 8 slots, 5 allocated, 2 freed

Index: 0    1    2    3    4    5    6    7     _nextId=5
      ┌────┬────┬────┬────┬────┬────┬────┬────┐
_res: │ T0 │nil │ T2 │ T3 │nil │nil │nil │nil │
      └────┴────┴────┴────┴────┴────┴────┴────┘
Gen:   1    2    1    3    1    0    0    0
      ↑    ↑    ↑    ↑    ↑
      │    │    │    │    └─ FREE (can reuse)
      │    │    │    └─ Valid (gen=3, reused)
      │    │    └─ Valid (gen=1)
      │    └─ FREE (can reuse, was gen=1 now 2)
      └─ Valid (gen=1)

Allocation targets: slot 1 or 4 (FIFO queue order), or slot 5 if queue empty
Capacity when full: 8 slots
Growth trigger: _nextId >= capacity (5 >= 8? No, so no growth yet)
```

---

## 2. Generation-Based Validation State Machine

### Handle Lifecycle

```
                    CREATE HANDLE
                         │
                         ▼
                  ┌──────────────┐
                  │  Allocate()  │
                  │  gen ← 1     │
                  └──────┬───────┘
                         │
                         ▼ Return Handle(idx, gen=1)
                 ┌─────────────────┐
                 │   VALID HANDLE  │◄──────────────────────┐
                 │  Use with TryGet│                       │
                 └────────┬────────┘                       │
                          │                                │
                 ┌────────┴─────────┐                      │
                 │                  │                      │
                 ▼ (Release)        ▼ (Still valid)        │
          ┌────────────────┐   ┌─────────────┐            │
          │ Release()      │   │  TryGet()   │            │
          │ Dispose resource│   │  SUCCESS    │            │
          │ Enqueue slot   │   │ Use resource│            │
          └────────┬───────┘   └─────────────┘            │
                   │                                       │
                   ▼                                       │
          ┌──────────────────┐                            │
          │  SLOT IN REUSE   │◄─────────────────┐         │
          │  QUEUE           │                  │         │
          └────────┬─────────┘                  │         │
                   │                            │         │
         ┌─────────┴──────────┐                 │         │
         │ Allocate (reuse    │                 │         │
         │ same slot)         │                 │         │
         └─────────┬──────────┘                 │         │
                   │                            │         │
                   ▼ gen++ (1→2)                │         │
         ┌──────────────────────┐               │         │
         │ Return Handle(idx,2) │               │         │
         └──────────┬───────────┘               │         │
                    │                           │         │
                    ▼ New handle               │ Old handle
         ┌────────────────────┐   STALE      │ with gen=1
         │  NEW VALID HANDLE  │───────────────┘
         │  Old gen=1 invalid │
         │  New gen=2 valid   │
         └────────┬───────────┘
                  │
          ┌───────┴────────┐
          │                │
    ▼(Old Handle)   ▼(New Handle)
┌────────────────┐ ┌──────────────────┐
│ TryGet(gen=1)  │ │ TryGet(gen=2)    │
│ 1 ≠ 2 → FALSE  │ │ 2 = 2 → SUCCESS  │
│ REJECTED ✗     │ │ ACCEPTED ✓       │
└────────────────┘ └──────────────────┘
```

### Validation Decision Tree

```
pool.TryGet(handle)
│
├─ handle.IsValid? (index ≠ uint.MaxValue)
│  │
│  ├─ NO
│  │  └─ return (false, null)  [Invalid sentinel]
│  │
│  └─ YES
│     │
│     ├─ handle.Index < _nextId?
│     │  │
│     │  ├─ NO
│     │  │  └─ return (false, null)  [Index out of range]
│     │  │
│     │  └─ YES
│     │     │
│     │     ├─ _generations[index] == handle.Generation?
│     │     │  │
│     │     │  ├─ NO
│     │     │  │  └─ return (false, null)  [GENERATION MISMATCH ← Use-after-free caught!]
│     │     │  │
│     │     │  └─ YES
│     │     │     │
│     │     │     ├─ _resources[index] != null?
│     │     │     │  │
│     │     │     │  ├─ NO
│     │     │     │  │  └─ return (false, null)  [Slot cleared]
│     │     │     │  │
│     │     │     │  └─ YES
│     │     │     │     └─ return (true, _resources[index])  [VALID!]
```

---

## 3. SetRenderTarget() State Machine

### Framebuffer Binding Flow

```
SetRenderTarget(Handle<IFramebuffer> handle)
│
├─ Step 1: Check handle validity
│  │
│  ├─ handle.IsValid?
│  │  │
│  │  ├─ NO
│  │  │  │
│  │  │  └─ Bind backbuffer ──→ _cmdList.SetFramebuffer(SwapchainFramebuffer)
│  │  │     (Invalid/null framebuffer defaults to screen)
│  │  │
│  │  └─ YES → Continue
│  │
│  └─ Step 2: Create PoolHandle for lookup
│     │
│     └─ poolHandle = new PoolHandle(handle.Id, handle.Generation)
│
├─ Step 3: Lookup in pool
│  │
│  ├─ _framebufferPool.TryGet(poolHandle, out vfb)?
│  │  │
│  │  ├─ NO
│  │  │  │
│  │  │  ├─ Possible causes:
│  │  │  │   • Generation mismatch (use-after-free attempt)
│  │  │  │   • Index out of range
│  │  │  │   • Slot was released
│  │  │  │
│  │  │  └─ Bind backbuffer ──→ _cmdList.SetFramebuffer(SwapchainFramebuffer)
│  │  │     (Fallback: safe degradation)
│  │  │
│  │  └─ YES → Continue
│  │
│  └─ Step 4: Bind to command list
│     │
│     └─ _cmdList.SetFramebuffer(vfb)
│        (Custom framebuffer is now render target)
│
└─ Return (ready for rendering)
```

### Backbuffer vs Custom Framebuffer Detection

```
Current Framebuffer State:

┌──────────────────────────────────┐
│  _cmdList._currentFramebuffer    │
└──────────────────────────────────┘
           │
           ├─ Is SwapchainFramebuffer?
           │  │
           │  ├─ YES → BACKBUFFER (default, renders to screen)
           │  │       Size: window size
           │  │       Auto-presents after frame
           │  │
           │  └─ NO → CUSTOM FRAMEBUFFER (texture target)
           │          Size: user-defined
           │          Must bind texture as shader resource
           │
           ▼ Next frame begins
      _cmdList.Begin()
           │
      (Previous framebuffer bindings reset)
```

---

## 4. Slot Reuse Sequence Diagram

### Complete Reuse Cycle with Stale Handle Detection

```
Time →

FRAME 1: Allocate
═════════════════════════════════════════════════════════════
  Allocate(Texture1)
     │
     ├─ _freeSlots empty? YES
     ├─ _nextId = 0, capacity = 256
     ├─ _resources[0] = Texture1
     ├─ _generations[0] = 1
     │
     └─ Return Handle(idx=0, gen=1)  ◄─────── HANDLE_A

FRAME 50: Use
═════════════════════════════════════════════════════════════
  TryGet(HANDLE_A)
     │
     ├─ HANDLE_A.idx (0) < _nextId (1)? YES
     ├─ _generations[0] (1) == HANDLE_A.gen (1)? YES
     ├─ _resources[0] != null? YES
     │
     └─ return (true, Texture1)  ✓ VALID


FRAME 100: Release
═════════════════════════════════════════════════════════════
  Release(HANDLE_A)
     │
     ├─ TryGet(HANDLE_A) succeeds (still 1==1)
     ├─ Texture1.Dispose()
     ├─ _resources[0] = null
     ├─ _freeSlots.Enqueue(0)  ◄─── Slot 0 now in reuse queue
     │
     └─ return true


FRAME 150: Allocate Again (REUSE)
═════════════════════════════════════════════════════════════
  Allocate(Texture2)
     │
     ├─ _freeSlots.TryDequeue(out 0)? YES
     ├─ _resources[0] = Texture2
     ├─ _generations[0]++  →  2  ◄─── CRITICAL: Generation incremented!
     │
     └─ Return Handle(idx=0, gen=2)  ◄─────── HANDLE_B


FRAME 151: Try Old Handle (STALE)
═════════════════════════════════════════════════════════════
  TryGet(HANDLE_A)  [gen=1, but gen is now 2]
     │
     ├─ HANDLE_A.idx (0) < _nextId (1)? YES
     ├─ _generations[0] (2) == HANDLE_A.gen (1)? NO ✗
     │
     └─ return (false, null)  ✗ STALE HANDLE REJECTED


FRAME 151: Try New Handle (VALID)
═════════════════════════════════════════════════════════════
  TryGet(HANDLE_B)  [gen=2, and gen is 2]
     │
     ├─ HANDLE_B.idx (0) < _nextId (1)? YES
     ├─ _generations[0] (2) == HANDLE_B.gen (2)? YES ✓
     ├─ _resources[0] != null? YES
     │
     └─ return (true, Texture2)  ✓ VALID


Key Insight:
═════════════════════════════════════════════════════════════
  HANDLE_A and HANDLE_B both point to slot 0
  But only HANDLE_B is valid because generation matches
  This prevents use-after-free bugs automatically!
```

---

## 5. Memory Layout During Reuse

### Before and After Reuse

```
INITIAL STATE (After first Allocate)
─────────────────────────────────────────────

_resources:    [Texture1] [unalloc] [unalloc] ...
_generations:  [   1   ] [   0   ] [   0   ] ...
_freeSlots:    []
_nextId:       1

Handle_A = (idx=0, gen=1)


AFTER Release
─────────────────────────────────────────────

_resources:    [  null  ] [unalloc] [unalloc] ...
_generations:  [   1   ] [   0   ] [   0   ] ...
_freeSlots:    [0]
_nextId:       1

Handle_A = (idx=0, gen=1)  ← Still held by caller but invalid


AFTER Reuse Allocate
─────────────────────────────────────────────

_resources:    [Texture2] [unalloc] [unalloc] ...
_generations:  [   2   ] [   0   ] [   0   ] ...
_freeSlots:    []
_nextId:       1

Handle_A = (idx=0, gen=1)  ← Still held but now STALE
Handle_B = (idx=0, gen=2)  ← Fresh handle is VALID


HANDLE VALIDITY CHECK
─────────────────────────────────────────────

Handle_A.IsValid()?
  gen (1) == _generations[0] (2)? NO → INVALID

Handle_B.IsValid()?
  gen (2) == _generations[0] (2)? YES → VALID
```

---

## 6. Framebuffer Pool State Diagram

### Lifecycle of a Framebuffer Resource

```
GraphicsDevice Initialization
│
├─ _framebufferPool = new ResourcePool<Framebuffer>(32)
│  (Pool capacity: 32)
│
└─ _currentFramebuffer = SwapchainFramebuffer
   (Never pooled, always available)

┌────────────────────────────────────────────────────────┐
│                                                        │
│                  RENDERING LOOP                       │
│                                                        │
├────────────────────────────────────────────────────────┤
│                                                        │
│  CreateFramebuffer()                                  │
│    │                                                   │
│    ├─ vfb = new VeldridLib.Framebuffer(...)           │
│    ├─ poolHandle = _framebufferPool.Allocate(vfb)     │
│    │  (Slot allocation, generation=1)                │
│    │                                                   │
│    └─ return Handle(poolHandle.Index, poolHandle.Gen) │
│       ───────────────────────────┬────────────────────│
│                                  │                     │
│  SetRenderTarget(handle)         │                    │
│    │                              ▼                    │
│    ├─ if (!handle.IsValid)       cached handle       │
│    │   _cmdList.SetFramebuffer(SwapchainFramebuffer) │
│    │                                                   │
│    └─ Convert handle → poolHandle                     │
│       if (pool.TryGet(poolHandle, out vfb))           │
│         _cmdList.SetFramebuffer(vfb)  ◄─── Bind!     │
│       else                                            │
│         _cmdList.SetFramebuffer(SwapchainFramebuffer) │
│                                                        │
│  DestroyFramebuffer(handle)                           │
│    │                                                   │
│    ├─ Convert handle → poolHandle                     │
│    ├─ pool.Release(poolHandle)                        │
│    │  (Disposes VeldridLib.Framebuffer)              │
│    │  (Marks slot for reuse)                          │
│    │                                                   │
│    └─ handle is now INVALID (gen mismatch)            │
│                                                        │
└────────────────────────────────────────────────────────┘
         │
         │ (Repeat each frame)
         │
    Device.Dispose()
         │
         ├─ pool.Clear()  (dispose all framebuffers)
         └─ _framebufferPool = null
```

---

## 7. Handle Conversion Mapping Diagram

### Handle<T> ↔ PoolHandle Bijection

```
PUBLIC API                          INTERNAL POOL
═════════════════════════════════════════════════════════

Handle<IFramebuffer>                PoolHandle
┌──────────────────┐                ┌──────────────────┐
│ _id (uint)       │◄──────┬───────►│ Index (uint)     │
│ _generation (uint)│      │        │ Generation (uint)│
└──────────────────┘       │        └──────────────────┘
       │                   │               │
       │ Mapping:          │               │
       │ Id = Index        │               │
       │ Gen = Gen         │               │
       │                   │               │
       │ Handle(id=5, gen=3)               │
       │ ──────────────────┼───────────────│
       │  means: slot 5    │  means: slot 5 │
       │         generation 3         generation 3 │
       │                   │               │
       └───────────────────┴───────────────┘


CONVERSION DIRECTION 1: After CreateFramebuffer()
─────────────────────────────────────────────────

1. CreateFramebuffer()
   └─ vfb = new Veldrid.Framebuffer(...)

2. Allocate to pool
   └─ poolHandle = _framebufferPool.Allocate(vfb)
      Returns: PoolHandle(Index=5, Generation=1)

3. Convert to public Handle
   └─ return Handle<IFramebuffer>(
        id: poolHandle.Index,           // 5
        generation: poolHandle.Generation  // 1
      )

4. Caller receives
   └─ Handle<IFramebuffer> { _id=5, _generation=1 }


CONVERSION DIRECTION 2: Before SetRenderTarget()
─────────────────────────────────────────────────

1. Caller has
   └─ Handle<IFramebuffer> { _id=5, _generation=1 }

2. SetRenderTarget(handle)
   └─ Create PoolHandle from Handle
      poolHandle = new PoolHandle(
        index: handle.Id,           // 5
        generation: handle.Generation  // 1
      )

3. Lookup in pool
   └─ _framebufferPool.TryGet(poolHandle, ...)
      Checks: _generations[5] == 1?

4. If match
   └─ Bind framebuffer


Why this works:
───────────────
• Handle.Id and PoolHandle.Index are the same value
• Handle.Generation and PoolHandle.Generation are the same value
• Mapping is reversible and lossless
• All validation happens at TryGet() level
```

---

## 8. Growth Strategy Visualization

### Capacity Doubling on Exhaustion

```
Initial: capacity = 16, _nextId = 0

After allocating 16 resources:
─────────────────────────────
_resources:  [T1][T2][T3]...[T16][  ][  ]...[  ]
_nextId:     16
capacity:    16

Next Allocate():
─────────────────
if (_nextId >= _resources.Length)  // 16 >= 16? YES
  GrowCapacity()
    newCapacity = 16 * 2 = 32
    Array.Resize(_resources, 32)
    Array.Resize(_generations, 32)

Result:
─────────
_resources:  [T1][T2][T3]...[T16][  ][  ]...[  ][  ]...[  ]
capacity:    32
_nextId:     16

Now room for 16 more allocations before next growth.


Growth Timeline:
────────────────
Allocations  Capacity  Growth Trigger
    1-16        16     _nextId >= 16
   17-32        32     _nextId >= 32
   33-64        64     _nextId >= 64
  65-128       128     _nextId >= 128
```

---

## 9. Error Recovery Flowchart

### Graceful Degradation in SetRenderTarget()

```
SetRenderTarget(framebufferHandle)
│
├─ Attempt 1: Check handle is syntactically valid
│  │
│  └─ handle.IsValid? (Index ≠ uint.MaxValue)
│     │
│     ├─ NO ────────────────────────────────────┐
│     │  (Null/invalid handle)                  │
│     │                                          │
│     └─ YES → Attempt 2                        │
│        │                                       │
│        └─ Attempt 2: Lookup in pool            │
│           │                                    │
│           └─ pool.TryGet(poolHandle)?          │
│              │                                 │
│              ├─ NO ─────────────────────┐     │
│              │  (Stale/released handle)  │    │
│              │                           │    │
│              └─ YES → Bind ────────┐    │    │
│                 (Custom FBO)        │    │    │
│                                     │    │    │
└─────────────────────────────────────┼────┼────┼───────────┐
                                      │    │    │           │
                                      │    └────┴───────┐   │
                                      │                 │   │
                                      ▼                 ▼   ▼
                            ┌──────────────────┐   ┌─────────────────┐
                            │   BIND CUSTOM    │   │ BIND BACKBUFFER │
                            │   FRAMEBUFFER    │   │  (Fallback)     │
                            │                  │   │                 │
                            │ _cmdList.        │   │ _cmdList.       │
                            │ SetFramebuffer   │   │ SetFramebuffer  │
                            │ (veldridFb)      │   │ (SwapchainFB)   │
                            └──────────────────┘   └─────────────────┘
                                      │                    │
                                      └────────┬───────────┘
                                               │
                                               ▼
                                    Continue rendering
                                    (might see visual
                                     difference if
                                     fallback used)

Why this is safe:
─────────────────
• Invalid handle → falls back to backbuffer (worst case: renders to screen)
• Stale handle → falls back to backbuffer (worst case: renders to screen)
• Valid handle → renders to custom target (intended behavior)
• No crashes, no undefined behavior, just graceful degradation
```

---

## 10. Thread-Safety Violation Scenarios

### What NOT to Do

```
SCENARIO 1: Concurrent Pool Access (UNSAFE)
═══════════════════════════════════════════════════════

Thread A              Thread B              Pool State
────────              ────────              ──────────
                                            capacity = 16
                                            _nextId = 15

Allocate(T)           ─────────────────────►  _nextId = 16
  (starts)                                     (about to check capacity)

                      Allocate(T)
                        (starts)
                        Checks: 16 >= 16? YES
                        GrowCapacity()
                        ────────────────────►  capacity = 32
                                               _resources resized
                                               _nextId still 16

  (resumes)
  Continues with old capacity (16)
  May index out of bounds! 🔴 CRASH


SOLUTION:
─────────
Use lock:
  lock (_poolLock)
  {
      handle = _pool.Allocate(resource);
  }
```

### What IS Safe

```
SCENARIO 2: Immutable Handle Passing (SAFE)
═════════════════════════════════════════════

Thread A              Thread B              Shared
────────              ────────              ──────
                                            _texHandle = default

CreateTexture()
  ├─ Allocate to pool
  └─ _texHandle = new Handle(5, 1)  ◄─ Atomic struct assign

                      Read _texHandle  ◄─ Copies struct by value
                      if (_texHandle.IsValid)
                        // Use handle (read-only)

Why safe:
• Handle is immutable struct (no references)
• Copying struct is atomic
• Can't corrupt pool state by reading handle
• Can't use handle to modify pool without calling method
```

---

## Key Insights Summary

```
┌──────────────────────────────────────────────────────────────┐
│                    RESOURCE POOLING CORE                     │
├──────────────────────────────────────────────────────────────┤
│                                                              │
│  1. GENERATION = THE ROOT CAUSE PREVENTER                   │
│     └─ Stale handles automatically rejected                 │
│                                                              │
│  2. BACKBUFFER = SAFE FALLBACK                              │
│     └─ Any invalid handle → render to screen                │
│                                                              │
│  3. SLOT REUSE = MEMORY EFFICIENT                           │
│     └─ Freed slots reused with generation bump              │
│                                                              │
│  4. THREAD-UNSAFE = OK FOR SINGLE-THREADED RENDERING        │
│     └─ Main thread only (typical game loop)                 │
│                                                              │
│  5. O(1) OPERATIONS = FAST                                  │
│     └─ Allocation, lookup, release all constant time        │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```
