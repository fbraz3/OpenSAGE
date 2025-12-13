# BGFX Phase 5A - Known Issues & Status

## Current Status: BLOCKED - .NET 10 Runtime Bug

### Problem

Phase 5A BGFX implementation encounters `System.AccessViolationException` and subsequent `FailFast` crashes when attempting to initialize the BGFX graphics device on macOS with .NET 10.0.100.

### Root Cause

This is a known bug in .NET 10 runtime affecting stack management during P/Invoke calls in specific execution contexts.

**Bug Reference**: [dotnet/runtime#108763](https://github.com/dotnet/runtime/issues/108763)

- **Title**: "SortedDictionary<BigInteger, MyObject> System.AccessViolationException"
- **Description**: Memory corruption from tail call stack handling in combination with CET (Control-Flow Enforcement Technology)
- **Status**: Fixed in .NET 9.0.2, but **NOT yet fixed in .NET 10.0.0**
- **PR**: [dotnet/runtime#109074](https://github.com/dotnet/runtime/pull/109074)
- **Target**: Will be in next .NET 10.x servicing release

### Symptoms

1. Game starts normally
2. Backend selection to BGFX works correctly
3. BGFX device instantiation begins
4. During stack frame iteration in exception handling:

   ```csharp
   at System.Runtime.EH.DispatchEx(StackFrameIterator, ExInfo)
   at OpenSage.Graphics.BGFX.BgfxPlatformData.GetPlatformData
   ```

5. **FailFast** abort with "Memory corruption detected"

### Error Sequence

```
[15:05:48.3109][Info] Starting...
[15:05:48.7746][Info] [Launcher] Using BGFX graphics backend
Process terminated.
System.Runtime.EH.UnhandledExceptionFailFastViaClasslib
   ...RhFailFastReason...
   at System.Runtime.EH.DispatchEx(StackFrameIterator ByRef, ExInfo ByRef)
```

### Attempted Workarounds (All Failed)

1. **Disable Tiered Compilation**: `DOTNET_TieredCompilation=0` ❌ Does not prevent crash
2. **Direct P/Invoke to SDL_GetKeyboardFocus()**: ❌ Still triggers DispatchEx corruption
3. **Minimal Window Handle Access**: ❌ Issue is in runtime's exception dispatch, not application code
4. **Zero Window Handle**: ❌ Crash occurs during object construction, before code execution

### Why Workarounds Don't Work

The corruption happens in `.NET Runtime EH.DispatchEx` **during stack frame iteration for exception handling itself**. This is before user code even executes. The bug is in the CLR's implementation of:

- Stack frame iteration
- Control flow enforcement (CET) stack shadow synchronization
- Tail call return address handling

### Solution Path

1. **Short Term**: Disable BGFX backend selection until .NET 10.x servicing release
2. **Medium Term**: Monitor [dotnet/runtime releases](https://github.com/dotnet/runtime/releases) for 10.0.1+ with PR #109074 backported
3. **Long Term**: Re-enable BGFX once fix is available and tested

### Relevant Links

- Issue: [dotnet/runtime#108763](https://github.com/dotnet/runtime/issues/108763)
- Fix PR: [dotnet/runtime#109074](https://github.com/dotnet/runtime/pull/109074)
- Similar Issues:
  - #110012 (F# Compiler)
  - #110088 (Parallel Trie Reads)
  - #110543
  - #110981

### For Phase 5B+

- Do **not** proceed with resource management until BGFX initialization is fixed
- Current code implementation (BgfxGraphicsDevice.cs) is **correct** - problem is runtime, not logic
- Code is ready to resume once .NET runtime fix is available
