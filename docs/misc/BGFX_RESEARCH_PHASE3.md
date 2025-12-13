# BGFX Architecture Research - Phase 3 Graphics Adapter Implementation

**Date:** December 12, 2025  
**Purpose:** In-depth analysis of BGFX architecture for implementing graphics adapter compatibility in OpenSAGE  
**Repository:** https://github.com/bkaradzic/bgfx

---

## Executive Summary

BGFX is a **command-based, handle-driven graphics abstraction layer** that separates command encoding from rendering execution. Its core strength is **multi-threaded command submission with asynchronous rendering**, achieved through an encoder-based pipeline. Unlike Veldrid's synchronous imperative API, BGFX uses a **deferred rendering model** with explicit frame synchronization and view-based render pass organization.

---

## 1. BGFX Architecture Overview

### 1.1 Core Design Philosophy

**"Bring Your Own Engine/Framework"** - BGFX provides graphics capability without dictating architecture:
- **Graphics API agnostic**: Supports Direct3D 11/12, OpenGL 2.1/3.1+/ES, Vulkan, Metal, WebGL
- **Command-based rendering**: Records commands for later execution (deferred)
- **Multi-threaded by design**: API thread (command encoding) → Render thread (GPU execution)
- **View-based organization**: Render passes are explicitly named and ordered

### 1.2 Key Architectural Components

```
┌─────────────────────────────────────────────────────────────┐
│                     Application                              │
└──────────────────────┬──────────────────────────────────────┘
                       │
         ┌─────────────┴─────────────┐
         │                           │
    ┌────▼─────┐            ┌───────▼──────┐
    │ API Thread│            │ Worker Threads│
    │ Main      │            │ (Optional)    │
    └────┬──────┘            └───────┬───────┘
         │                           │
         └──────────────┬────────────┘
                        │
              ┌─────────▼────────────┐
              │ Encoder (Command      │
              │ Buffer Recording)     │
              └─────────┬─────────────┘
                        │ bgfx::frame()
              ┌─────────▼─────────────┐
              │ Frame Swap +          │
              │ Render Thread Wake    │
              └─────────┬─────────────┘
                        │
              ┌─────────▼──────────────┐
              │ Render Thread          │
              │ (GPU Command Submission)│
              └───────────────────────┘
```

---

## 2. Handle System Implementation

### 2.1 Handle Structure

**All handles are opaque `uint16_t` indices** that abstract underlying graphics resources:

```cpp
// Example from BGFX (src/bgfx_p.h pattern)
struct TextureHandle { uint16_t idx; };
struct BufferHandle { uint16_t idx; };
struct FrameBufferHandle { uint16_t idx; };
struct ProgramHandle { uint16_t idx; };
struct ShaderHandle { uint16_t idx; };
```

**Key characteristics:**
- **Opaque design**: Prevents direct access to underlying GPU resources
- **Index-based**: Maps to internal allocator pools
- **No reference counting** in the handle itself (managed in allocator)
- **Lightweight**: 2 bytes per handle, can be stored/passed efficiently

### 2.2 Handle Validation

BGFX uses **generation-based validation patterns** (implicitly through allocators):

```cpp
// Patterns observed in allocator design
// - Handles are checked against allocation bitmaps
// - Invalid handles have special sentinel values (e.g., BGFX_INVALID_HANDLE = 0xFFFF)
// - No runtime generation counter visible in public API
```

**Handle lifecycle:**
1. **Create**: `bgfx::createTexture()` → allocates handle, returns to caller
2. **Use**: Handle passed to rendering commands (`setTexture()`, `setIndexBuffer()`)
3. **Destroy**: Explicit `bgfx::destroy(handle)` required
4. **Reuse**: Allocator can reuse index after destruction

### 2.3 Handle Allocators

Located in `src/bgfx_p.h`, context maintains per-resource allocators:

```cpp
// Conceptual (from BGFX Context)
class Context
{
    // Handle allocators for each resource type
    HandleAllocT<BGFX_CONFIG_MAX_TEXTURES> m_textureHandle;
    HandleAllocT<BGFX_CONFIG_MAX_VERTEX_BUFFERS> m_vertexBufferHandle;
    HandleAllocT<BGFX_CONFIG_MAX_INDEX_BUFFERS> m_indexBufferHandle;
    HandleAllocT<BGFX_CONFIG_MAX_FRAME_BUFFERS> m_frameBufferHandle;
    HandleAllocT<BGFX_CONFIG_MAX_PROGRAMS> m_programHandle;
    HandleAllocT<BGFX_CONFIG_MAX_SHADERS> m_shaderHandle;
    // ... etc for other resource types
};
```

**Pool sizes are compile-time configurable** via macros (e.g., `BGFX_CONFIG_MAX_TEXTURES = 4096`).

---

## 3. Encoder Model - Multi-Threaded Command Recording

### 3.1 Encoder Architecture

The `Encoder` struct (in `include/bgfx/bgfx.h`) is the **primary API for command submission**:

```cpp
// Begin encoder (one per thread)
Encoder* encoder = bgfx::begin(bool forThread = false);

// Record commands
encoder->setState(state);
encoder->setIndexBuffer(ibh);
encoder->setVertexBuffer(0, vbh);
encoder->setTexture(0, sampler, texture);
encoder->submit(viewId, program);

// End encoder
bgfx::end(encoder);
```

**Thread safety model:**
- **One encoder per thread**: Each thread calling `bgfx::begin()` gets a unique encoder
- **No lock-free design shown publicly**: Uses mutex-protected allocators
- **API lock**: `m_encoderApiLock` mutex protects encoder allocation on multi-threaded access

### 3.2 Encoder Implementation Details

From internal analysis (`src/bgfx_p.h` patterns):

```cpp
struct EncoderImpl
{
    // Points to current frame being recorded into
    Frame* m_frame;
    
    // Per-encoder uniform buffer (avoids GPU sync)
    uint32_t m_uniformBuffer;
    
    // Command recording methods
    void setIndexBuffer(IndexBufferHandle handle);
    void setVertexBuffer(uint8_t stream, VertexBufferHandle handle);
    void submit(ViewId viewId, ProgramHandle program, uint32_t depth);
    
    // Internally calls Frame::addRenderItem()
};
```

**Encoder lifecycle:**
1. **`bgfx::begin()`**: Allocates `EncoderImpl` from pool (default: 8 max)
2. **Recording**: All commands append to encoder's command buffer
3. **`bgfx::end()`**: Returns encoder to pool for next frame reuse
4. **Frame swap**: `bgfx::frame()` causes render thread to process buffered commands

### 3.3 Maximum Simultaneous Encoders

**Configurable at initialization:**

```cpp
bgfx::Init init;
init.limits.maxEncoders = 8; // Default in multithreaded mode, 1 in single-threaded
```

**Behavior when limit exceeded:**
- Additional `bgfx::begin()` calls block until an encoder becomes available
- Ensures bounded memory usage and prevents runaway thread creation

---

## 4. Resource Management Lifecycle

### 4.1 Buffer Creation & Lifecycle

#### Static Buffers (Immutable)

```cpp
// Create static vertex buffer
bgfx::VertexLayout layout;
layout.begin().add(bgfx::Attrib::Position, 3, bgfx::AttribType::Float).end();

const bgfx::Memory* mem = bgfx::copy(vertexData, vertexDataSize);
bgfx::VertexBufferHandle vbh = bgfx::createVertexBuffer(mem, layout);

// Use throughout frame
encoder->setVertexBuffer(0, vbh);

// Explicit destruction
bgfx::destroy(vbh);
```

#### Dynamic Buffers (Mutable Per-Frame)

```cpp
// Create empty dynamic buffer
bgfx::DynamicVertexBufferHandle dvbh = 
    bgfx::createDynamicVertexBuffer(maxVertexCount, layout);

// Each frame, update data
bgfx::update(dvbh, 0, bgfx::copy(vertexData, newSize));
encoder->setVertexBuffer(0, dvbh);

// Cleanup
bgfx::destroy(dvbh);
```

#### Transient Buffers (Frame-Local)

```cpp
// Allocate from per-frame pool (no explicit creation)
bgfx::TransientVertexBuffer tvb;
bgfx::allocTransientVertexBuffer(&tvb, vertexCount, layout);

// Write data directly to allocated buffer
memcpy(tvb.data, vertexData, vertexDataSize);

// Use directly in submit
encoder->setVertexBuffer(0, &tvb);

// Automatically freed at frame boundary
```

### 4.2 Texture Creation & Lifetime

```cpp
// Create 2D texture from data
const bgfx::Memory* texData = bgfx::copy(imageData, imageSize);
bgfx::TextureHandle th = bgfx::createTexture2D(
    width, height,
    hasMips,        // Auto-generate mips?
    numLayers,      // 1 for normal, >1 for array
    format,         // e.g., RGBA8
    flags           // BGFX_SAMPLER_U_CLAMP, BGFX_TEXTURE_RT, etc.
    texData          // Can be NULL for uninitialized RT
);

// Update texture if mutable
bgfx::updateTexture2D(th, layer, mip, x, y, w, h, bgfx::copy(...));

// Render target variant (no mipmap, write-only)
th = bgfx::createTexture2D(width, height, false, 1,
    BGFX_TEXTURE_RT | BGFX_SAMPLER_U_CLAMP | BGFX_SAMPLER_V_CLAMP);

// Cleanup
bgfx::destroy(th);
```

### 4.3 Framebuffer Creation & Composition

```cpp
// Simple single-attachment framebuffer (RT)
bgfx::FrameBufferHandle fbh = bgfx::createFrameBuffer(
    width, height,
    format,             // Color format
    flags               // BGFX_TEXTURE_RT, BGFX_SAMPLER_U_CLAMP, etc.
);

// MRT (Multiple Render Target) framebuffer
bgfx::TextureHandle colorRt = bgfx::createTexture2D(..., BGFX_TEXTURE_RT);
bgfx::TextureHandle normalRt = bgfx::createTexture2D(..., BGFX_TEXTURE_RT);
bgfx::TextureHandle depthRt = bgfx::createTexture2D(..., BGFX_TEXTURE_RT);

bgfx::FrameBufferHandle mrt = bgfx::createFrameBuffer(
    3,
    {colorRt, normalRt, depthRt}
);

// Attach framebuffer to view
bgfx::setViewFrameBuffer(viewId, fbh);

// Cleanup
bgfx::destroy(fbh); // Optionally destroys attached textures if flag set
```

### 4.4 Shader & Program Lifecycle

```cpp
// Load pre-compiled shader binary
const bgfx::Memory* vsData = bgfx::copy(shaderBinary, shaderSize);
bgfx::ShaderHandle vsh = bgfx::createShader(vsData);

const bgfx::Memory* fsData = bgfx::copy(fragmentBinary, fragmentSize);
bgfx::ShaderHandle fsh = bgfx::createShader(fsData);

// Create program from shaders
bgfx::ProgramHandle program = bgfx::createProgram(vsh, fsh, true); // destroyShaders=true

// Use in draw call
encoder->submit(viewId, program);

// Cleanup
bgfx::destroy(program);
```

**No explicit shader destruction needed if `destroyShaders=true`** in `createProgram()`.

---

## 5. View System - Render Pass Organization

### 5.1 View Concept & Limits

**Views are implicit render passes** (0-255 supported, configurable):

```cpp
// From bgfx::Caps::Limits
maxViews = 256; // Default limit
```

**Each view is a container for:**
- Draw calls with automatic sorting
- Explicit transform matrices (view & projection)
- Clear operations (color, depth, stencil)
- Viewport settings
- Framebuffer binding
- Sorting mode (default, sequential, depth-based)

### 5.2 View Configuration

```cpp
// Set view-wide transform matrices
float viewMtx[16], projMtx[16];
// ... initialize matrices ...
bgfx::setViewTransform(viewId, viewMtx, projMtx);

// Set viewport (pixel coordinates or back-buffer ratio)
bgfx::setViewRect(viewId, 0, 0, width, height);

// Set scissor (clipping rectangle)
bgfx::setViewScissor(viewId, x, y, w, h);

// Configure clear operations
bgfx::setViewClear(
    viewId,
    BGFX_CLEAR_COLOR | BGFX_CLEAR_DEPTH,
    0x303030ff, // Color RGBA
    1.0f,       // Depth
    0           // Stencil
);

// Set view name (debug)
bgfx::setViewName(viewId, "GBuffer Pass");

// Set sorting mode
bgfx::setViewMode(viewId, bgfx::ViewMode::Default); // Auto-sort by state
// bgfx::ViewMode::Sequential; // Preserve submit order
// bgfx::ViewMode::DepthAscending; // Sort by depth ascending
```

### 5.3 Framebuffer Binding per View

```cpp
// Bind MRT to view
bgfx::setViewFrameBuffer(viewId, mrtHandle);

// Subsequent submits to viewId render to MRT attachments
encoder->submit(viewId, program); // Renders to MRT

// Reset view to back-buffer
bgfx::setViewFrameBuffer(viewId, BGFX_INVALID_HANDLE); // Back to default
```

**Default behavior (no explicit binding):**
- Views 0-255 all render to back-buffer by default
- Framebuffer binding **is NOT persistent across `bgfx::frame()`** → Must rebind each frame

### 5.4 View Ordering & Reordering

```cpp
// Default: Views execute in ID order (0, 1, 2, ...)
// Can dynamically reorder at runtime:
uint16_t viewOrder[] = {2, 0, 1}; // Execute views in custom order
bgfx::setViewOrder(0, 3, viewOrder);

// Reset to default order
bgfx::setViewOrder(0, 3, NULL);
```

---

## 6. Shader System - Offline Compilation & Runtime Loading

### 6.1 Shader Compilation Pipeline

BGFX uses **offline shader compilation** via the `shaderc` tool:

```bash
# Compile GLSL-like source to multiple backends
shaderc -f vertex_shader.sc -o vertex.bin -p 450 --type vertex -O 3

# Generates binary for specified platform:
# 450 = OpenGL 4.5
# For Metal: use -p metal
# For D3D11: use -p d3d11
# For Vulkan: use -p spirv
```

**Input format:** GLSL-like + C preprocessor directives

**Output formats per target:**
- **DirectX 11/12**: DXBC bytecode
- **OpenGL**: ARB assembly or GLSL source
- **Vulkan**: SPIR-V bytecode
- **Metal**: Metal Shading Language bytecode
- **WebGL**: GLSL source

### 6.2 Cross-Compilation Support

The `shaderc` tool performs **automatic cross-compilation**:

```cpp
// Internal process in shaderc (tools/shaderc/shaderc.cpp):
// 1. Parse GLSL-like source
// 2. Convert to intermediate representation
// 3. Compile to target backend (D3D, Vulkan, Metal, GL)
// 4. Generate final binary with magic number + metadata
```

**No SPIR-V intermediate used** (uses custom IR from GLSL-Optimizer for some paths).

### 6.3 Runtime Shader Loading

```cpp
// Load pre-compiled shader binary
bgfx::ShaderHandle shader = bgfx::createShader(
    bgfx::copy(shaderBinary, shaderSize)
);

// Internal format validation in createShader():
// 1. Read magic number (validates signature + version)
// 2. Check if compute shader (requires BGFX_CAPS_COMPUTE)
// 3. Verify shader binary version compatibility
// 4. Check hash map to avoid duplicate loads
```

**Binary format (internal, from Caps validation pattern):**
```
[Magic: 4 bytes]
[Version: varies by backend]
[Hash: identify duplicates]
[Metadata: uniforms, samplers, etc.]
[Shader bytecode: backend-specific]
```

### 6.4 Program Creation (Shader Linking)

```cpp
// Combine vertex + fragment shaders into program
bgfx::ProgramHandle program = bgfx::createProgram(
    vertexShader,
    fragmentShader,
    true  // Auto-destroy shaders after linking
);

// Or compute shader
bgfx::ProgramHandle computeProgram = bgfx::createComputeProgram(
    computeShader,
    true
);

// Program linking validates:
// - Vertex output matches fragment input
// - All samplers and uniforms are available
```

---

## 7. Graphics State Management

### 7.1 State Bitmask System

BGFX uses **64-bit state flags** (uint64_t) for configurable rendering state:

```cpp
// Set render state
uint64_t state = 0
    | BGFX_STATE_WRITE_RGB        // Enable RGB write
    | BGFX_STATE_WRITE_A          // Enable alpha write
    | BGFX_STATE_WRITE_Z          // Enable depth write
    | BGFX_STATE_DEPTH_TEST_LESS  // Depth test function
    | BGFX_STATE_CULL_CW          // Backface culling direction
    | BGFX_STATE_MSAA             // Enable MSAA
    | BGFX_STATE_BLEND_FUNC(BGFX_STATE_BLEND_SRC_ALPHA, BGFX_STATE_BLEND_INV_SRC_ALPHA);

encoder->setState(state);
```

### 7.2 State Components (Bitmask Layout)

**Write Masks:**
```cpp
BGFX_STATE_WRITE_R            // Red channel
BGFX_STATE_WRITE_G            // Green channel
BGFX_STATE_WRITE_B            // Blue channel
BGFX_STATE_WRITE_RGB          // R|G|B
BGFX_STATE_WRITE_A            // Alpha
BGFX_STATE_WRITE_Z            // Depth
```

**Depth Test:**
```cpp
BGFX_STATE_DEPTH_TEST_LESS    // <
BGFX_STATE_DEPTH_TEST_LEQUAL  // <=
BGFX_STATE_DEPTH_TEST_EQUAL   // ==
BGFX_STATE_DEPTH_TEST_GEQUAL  // >=
BGFX_STATE_DEPTH_TEST_GREATER // >
BGFX_STATE_DEPTH_TEST_NOTEQUAL // !=
BGFX_STATE_DEPTH_TEST_NEVER   // Never pass
BGFX_STATE_DEPTH_TEST_ALWAYS  // Always pass
```

**Blend Modes:**
```cpp
// Source blend factors
BGFX_STATE_BLEND_ZERO
BGFX_STATE_BLEND_ONE
BGFX_STATE_BLEND_SRC_COLOR
BGFX_STATE_BLEND_INV_SRC_COLOR
BGFX_STATE_BLEND_SRC_ALPHA
BGFX_STATE_BLEND_INV_SRC_ALPHA
BGFX_STATE_BLEND_DST_ALPHA
BGFX_STATE_BLEND_INV_DST_ALPHA
BGFX_STATE_BLEND_DST_COLOR
BGFX_STATE_BLEND_INV_DST_COLOR
BGFX_STATE_BLEND_SRC_ALPHA_SAT
BGFX_STATE_BLEND_FACTOR
BGFX_STATE_BLEND_INV_FACTOR

// Blend equations
BGFX_STATE_BLEND_EQUATION_ADD    // src + dst
BGFX_STATE_BLEND_EQUATION_SUB    // src - dst
BGFX_STATE_BLEND_EQUATION_REVSUB // dst - src
BGFX_STATE_BLEND_EQUATION_MIN    // min(src, dst)
BGFX_STATE_BLEND_EQUATION_MAX    // max(src, dst)
```

**Culling:**
```cpp
BGFX_STATE_CULL_CW              // Cull clockwise
BGFX_STATE_CULL_CCW             // Cull counter-clockwise
// (No cull if neither specified)
```

**Primitive Type:**
```cpp
BGFX_STATE_PT_TRISTRIP  // Triangle strip
BGFX_STATE_PT_LINES     // Line list
BGFX_STATE_PT_LINESTRIP // Line strip
BGFX_STATE_PT_POINTS    // Point list
```

**Other flags:**
```cpp
BGFX_STATE_MSAA                       // Enable MSAA
BGFX_STATE_LINEAA                     // Line antialiasing
BGFX_STATE_BLEND_ALPHA_TO_COVERAGE   // Alpha-to-coverage
BGFX_STATE_BLEND_INDEPENDENT          // Per-target blend
```

### 7.3 Helper Macros

```cpp
// Blend function with separate RGB/Alpha
#define BGFX_STATE_BLEND_FUNC(_srcRgb, _dstRgb, _srcA, _dstA) \
    ( (uint64_t)(_srcRgb) | \
      ((uint64_t)(_dstRgb) << 4) | \
      ((uint64_t)(_srcA) << 8) | \
      ((uint64_t)(_dstA) << 12) )

// Set alpha reference for alpha test
#define BGFX_STATE_ALPHA_REF(_ref) \
    ( ((uint64_t)(_ref) << 40) )

// Set point size
#define BGFX_STATE_POINT_SIZE(_size) \
    ( ((uint64_t)(_size) << 52) )
```

### 7.4 Stencil Configuration

```cpp
// Separate front/back stencil state
uint32_t frontStencil = 0
    | BGFX_STENCIL_TEST_LESS
    | BGFX_STENCIL_FUNC_REF(0)
    | BGFX_STENCIL_FUNC_RMASK(0xff)
    | BGFX_STENCIL_OP_FAIL_KEEP
    | BGFX_STENCIL_OP_ZFAIL_KEEP
    | BGFX_STENCIL_OP_ZPASS_REPLACE;

uint32_t backStencil = BGFX_STENCIL_NONE; // Use front stencil

encoder->setStencil(frontStencil, backStencil);
```

---

## 8. Multi-Threading Model

### 8.1 Thread Architecture

BGFX separates concerns across **two main threads**:

**API Thread (Main):**
- Records rendering commands via encoders
- Updates resources (dynamic buffers, textures)
- Calls `bgfx::frame()` to trigger render
- Multiple worker threads can encode in parallel

**Render Thread:**
- Waits for frame submission from API thread
- Executes GPU commands (submit to graphics API)
- Handles GPU synchronization
- Maintains GPU resource lifecycle

### 8.2 Synchronization Primitives

From `src/bgfx_p.h` pattern analysis:

```cpp
// Internal synchronization (not exposed in public API)
Mutex m_encoderApiLock;         // Protects encoder allocation
Semaphore m_encoderEndSem;      // Signals encoder completion
Mutex m_resourceApiLock;        // Protects resource updates
Semaphore m_renderSem, m_apiSem; // API/Render thread sync
```

**Synchronization flow:**
1. **API thread** encodes commands via encoders
2. **API thread** calls `bgfx::frame()`, locks `m_resourceApiLock`
3. **API thread** posts `m_renderSem` to wake render thread
4. **API thread** waits on `m_apiSem` (blocks until render completes)
5. **Render thread** processes buffered commands
6. **Render thread** posts `m_apiSem` to unblock API thread

### 8.3 Encoder Allocation (Multi-Threading)

```cpp
// Thread-safe encoder allocation
Encoder* bgfx::begin(bool forThread = false)
{
    if (forThread || !isMainThread())
    {
        // Lock allocation
        m_encoderApiLock.lock();
        EncoderHandle eh = m_encoderHandle.alloc();
        m_encoderApiLock.unlock();
        
        EncoderImpl* encoder = getEncoder(eh);
        encoder->begin(currentFrame);
        return encoder;
    }
    // Main thread gets fast path (no lock)
    return getMainThreadEncoder();
}
```

### 8.4 Configuration

**Enable/disable multi-threading at compile time:**

```cpp
// In bgfx (compile-time configuration)
#define BGFX_CONFIG_MULTITHREADED 1  // Enable (default)
#define BGFX_CONFIG_DEFAULT_MAX_ENCODERS 8
```

**Runtime configuration:**

```cpp
bgfx::Init init;
init.limits.maxEncoders = 8; // 0-255 supported
bgfx::init(init);
```

---

## 9. Comparison: BGFX vs Veldrid API Design

| Aspect | BGFX | Veldrid |
|--------|------|---------|
| **API Style** | Deferred (command recording) | Immediate (direct GPU commands) |
| **Threading** | Multi-threaded by design (encoders) | Single-threaded (CommandList per thread, but GPU sync in main) |
| **Command Submission** | `Encoder::submit()` with sorting | `CommandList.DrawIndexed()` imperative |
| **Frame Synchronization** | Explicit `bgfx::frame()` → GPU stall point | `CommandQueue.Submit()` queues, `Fence` for sync |
| **Render Passes** | Views (0-255, implicit sorting) | Framebuffer bindings per draw call |
| **State Management** | 64-bit bitmask (composable flags) | Object-based (BlendStateDescription, etc.) |
| **Resource Handles** | Opaque `uint16_t` indices | Typed wrapper structs with reference counting |
| **Shader Format** | Offline compiled binaries (.bin) | SPIR-V bytecode (cross-compiled) |
| **Framebuffers** | Composable from texture handles | First-class framebuffer objects |
| **Uniforms** | Named + automatic matching | Layout-based (UBO/SSBO binding) |

---

## 10. Key Integration Points for Phase 3

### 10.1 Graphics Adapter Interface Design

```csharp
// Proposed OpenSAGE adapter pattern
public interface IBGFXGraphicsAdapter : IGraphicsAdapter
{
    // Handle management
    TextureHandle CreateTexture2D(TextureDescription desc);
    BufferHandle CreateBuffer(BufferDescription desc);
    FramebufferHandle CreateFramebuffer(FramebufferAttachment[] attachments);
    
    // Encoder-based submission
    Encoder BeginFrame();
    void EndFrame(Encoder encoder);
    
    // View management (BGFX-specific)
    void ConfigureView(uint viewId, ViewConfig config);
    void SubmitToView(uint viewId, ProgramHandle program, uint depth);
    
    // State management (map from OpenSAGE state to BGFX bitmask)
    void SetRenderState(RenderState state);
}
```

### 10.2 Critical Implementation Notes

1. **Frame synchronization**: Must respect BGFX's deferred model
   - Don't expect results until frame after submission
   - Use occlusion queries for previous frame results
   - Implement frame latency tolerance (2-frame minimum)

2. **View organization**: Design render passes as BGFX views
   - Map OpenSAGE passes → BGFX view IDs
   - Respect view limits (0-255)
   - Manage view framebuffer bindings per-frame

3. **Multi-threading**: Leverage encoder pools
   - One encoder per worker thread
   - No lock-free shared state in critical path
   - Batch uniform updates per-encoder

4. **State caching**: BGFX is relatively efficient
   - State changes are part of command record
   - Implement sorting for state optimization (default view mode)
   - Profile with `BGFX_DEBUG_STATS`

---

## 11. API Function Reference

### Core Lifecycle
- `bgfx::init()` - Initialize library
- `bgfx::shutdown()` - Clean up
- `bgfx::reset()` - Change back-buffer resolution
- `bgfx::frame()` - Advance to next frame, trigger render

### Command Recording
- `bgfx::begin()` - Get encoder for thread
- `bgfx::end()` - Finalize encoder

### Resource Creation
- `bgfx::createTexture2D()`, `::createTexture3D()`, `::createTextureCube()`
- `bgfx::createVertexBuffer()`, `::createDynamicVertexBuffer()`
- `bgfx::createIndexBuffer()`, `::createDynamicIndexBuffer()`
- `bgfx::createFrameBuffer()`
- `bgfx::createShader()`, `::createProgram()`

### Resource Destruction
- `bgfx::destroy(handle)` - Destroy any resource

### Rendering Commands (on Encoder)
- `encoder->setState()`
- `encoder->setIndexBuffer()`, `::setVertexBuffer()`
- `encoder->setTexture()`, `::setUniform()`
- `encoder->submit()` - Final draw call

### View Configuration
- `bgfx::setViewRect()`, `::setViewScissor()`
- `bgfx::setViewFrameBuffer()`
- `bgfx::setViewTransform()`
- `bgfx::setViewClear()`
- `bgfx::setViewMode()`, `::setViewOrder()`

### Querying
- `bgfx::getCaps()` - Renderer capabilities
- `bgfx::getStats()` - Frame statistics
- `bgfx::getResult()` - Occlusion query results

---

## 12. Recommended Resources

### Official Documentation
- **API Reference**: https://bkaradzic.github.io/bgfx/bgfx.html
- **Examples**: https://github.com/bkaradzic/bgfx/tree/master/examples
- **Developer Guide**: Look in `docs/` of the repository

### Key Source Files (for deep dives)
- **Core API**: `include/bgfx/bgfx.h` (2000+ lines, well-documented)
- **Implementation**: `src/bgfx_p.h`, `src/bgfx.cpp`
- **Platform-specific**: `src/renderer_mtl.mm` (Metal), `src/renderer_vk.cpp` (Vulkan)
- **Handle system**: `src/handlealloc.h` (bx library)
- **Shader compiler**: `tools/shaderc/shaderc.cpp`

---

## 13. Conclusion

BGFX's strength lies in its **clean separation of concerns**:
1. **Command encoding** (multi-threaded, lock-minimal)
2. **Frame synchronization** (explicit frame boundaries)
3. **Rendering execution** (dedicated thread)

This defers challenges to the integration layer but provides **high-performance, scalable rendering** for complex applications like OpenSAGE.

The **view system** is uniquely powerful—effectively a built-in render graph system that handles sorting and batching automatically.

For Phase 3, the key is **respecting BGFX's deferred model** in the adapter layer while presenting OpenSAGE's immediate-style API to the engine.

---

**Document Version**: 1.0  
**Last Updated**: 2025-12-12  
**Status**: Complete Research
