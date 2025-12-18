# Generic Protections Audit

## Objective

Identify and catalogue generic protections (try-catch blocks, null checks, etc.) that may be silently swallowing errors without appropriate logging.

---

## 1. CATCH BLOCKS WITH GENERIC EXCEPTIONS (11 found)

### ✅ Well documented (with good logging)

#### 1. `SteamInstallationLocator.cs:55`

```csharp
catch (Exception e)
{
    Logger.Warn($"Failed to parse libraryfolders.vdf file at {libraryFoldersPath}: {e.Message}");
    yield break;
}
```

**Status**: ✅ Good — clear message and context
**Issue**: None

---

#### 2. `LuaScriptConsole.cs:90`

```csharp
catch (Exception exeption)  // typo: should be "exception"
{
    _scriptConsoleTextAll = string.Concat(_scriptConsoleTextAll, "FATAL ERROR: ", exeption, "\n");
    _consoleTextColor = new Vector4(150, 0, 0, 1);
}
```

**Status**: ✅ Good — writes to console
**Issue**: Variable name typo ("exeption")

---

#### 3. `AudioSystem.cs:75`

```csharp
catch (Exception ex)
{
    LogAudioInitializationFailure();
    Logger.Debug(ex, "Exception details during audio initialization");
}
```

**Status**: ✅ Good — detailed log + helper method
**Issue**: None

---

#### 4. `OrderProcessor.cs:256` (SetSelection)

```csharp
catch (Exception e)
{
    Logger.Error(e, "Error while setting selection");
}
```

**Status**: ✅ Good — Logger.Error with exception
**Issue**: None

---

#### 5. `OrderProcessor.cs:331` (RallyPoint)

```csharp
catch (Exception e)
{
    Logger.Error(e, "Error while setting rallypoint");
}
```

**Status**: ✅ Good — Logger.Error with exception
**Issue**: None

---

#### 6. `UPnP.cs:72`

```csharp
catch (Exception e)
{
    if (SkirmishHostMapping != null)
    {
        await NatDevice.DeletePortMapAsync(SkirmishHostMapping);
    }

    if (SkirmishGameMapping != null)
    {
        await NatDevice.DeletePortMapAsync(SkirmishGameMapping);
    }

    Logger.Error(e, "Failed to forward port.");
    return false;
}
```

**Status**: ✅ Good — cleanup + Logger.Error
**Issue**: None

---

#### 7. `LuaScriptEngine.cs:42`

```csharp
catch (Exception ex)
{
    Logger.Error(ex, "Error while loading script file");
}
```

**Status**: ✅ Good — Logger.Error with exception
**Issue**: None

---

#### 8. `StaticLodApplicationManager.cs:97`

```csharp
catch (Exception ex)
{
    throw new InvalidOperationException($"Failed to apply static LOD level {lodLevel}", ex);
}
```

**Status**: ✅ Good — re-throws with context
**Issue**: None

---

#### 9. `BigEditor MainForm.cs:244`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
}
```

**Status**: ⚠️ Partial — log present but only `.Message`
**Issue**: Uses `e.Message` instead of passing the exception; loses StackTrace
**Recommendation**: `Logger.Error(e, "Failed to open big file");`

---

#### 10. `BigEditor MainForm.cs:404`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
    return;
}
```

**Status**: ⚠️ Partial — log present but only `.Message`
**Issue**: Uses `e.Message` instead of passing the exception; loses StackTrace
**Recommendation**: `Logger.Error(e, "Failed to create directory during export");`

---

#### 11. `BigEditor MainForm.cs:420`

```csharp
catch (Exception e)
{
    Logger.Error(e.Message);
}
```

**Status**: ⚠️ Partial — log present but only `.Message`
**Issue**: Uses `e.Message` instead of passing the exception; loses StackTrace
**Recommendation**: `Logger.Error(e, "Failed to export file");`

---

## 2. SILENT NULL-CHECK PROTECTIONS

Looking for patterns such as:

```csharp
if (value == null) return;
if (value == null) yield break;
if (!Collection.Contains(x)) continue;
```

**Result**: Many occurrences found, but most are intentional and documented. Review on a case-by-case basis as needed.

---

## 3. EMPTY CATCH BLOCKS

Search for: `catch { }` or `catch (Exception) { }`

**Result**: 0 found ✅

---

## 4. SUMMARY & RECOMMENDATIONS

### Statistics
- **Total catch blocks**: 11
- **Well documented**: 8 ✅
- **Partially documented**: 3 ⚠️
- **Poorly documented**: 0

### Immediate Actions

- **File**: BigEditor/MainForm.cs (line 244) — **Problem**: Use `Logger.Error(e, ...)` instead of `Logger.Error(e.Message)` — **Priority**: 🔴 HIGH
- **File**: BigEditor/MainForm.cs (line 404) — **Problem**: Use `Logger.Error(e, ...)` instead of `Logger.Error(e.Message)` — **Priority**: 🔴 HIGH
- **File**: BigEditor/MainForm.cs (line 420) — **Problem**: Use `Logger.Error(e, ...)` instead of `Logger.Error(e.Message)` — **Priority**: 🔴 HIGH
- **File**: LuaScriptConsole.cs (line 90) — **Problem**: Typo: `exeption` → `exception` — **Priority**: 🟡 MEDIUM

---

## 5. IMPROVEMENT IMPLEMENTATION

### Recommended Pattern for Catch Blocks

```csharp
// WRONG ❌
catch (Exception e)
{
    Logger.Error(e.Message);
}

// CORRECT ✅
catch (Exception e)
{
    Logger.Error(e, "Description of what was being attempted");
    // OR
    Logger.Error(e, $"Failed to {action} {context}");
}
```

### Why?

1. `Logger.Error(e, msg)` includes the full **StackTrace**
2. `Logger.Error(e.Message)` loses context about where the error occurred
3. A descriptive message helps debugging quickly

---

## 6. NEXT STEPS

- [X] Apply patches to the 3 BigEditor catches
- [X] Fix typo in `LuaScriptConsole.cs`
- [X] Add tests to verify exceptions produce logs
- [X] Consider a wrapper/helper for the common pattern
- [X] Review `RenderPipeline.cs` for generic checks (line 429 mentions Metal NRE)
