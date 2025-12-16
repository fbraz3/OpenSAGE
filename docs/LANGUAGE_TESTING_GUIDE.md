# Language Support Testing Guide

## Quick Start - Testing Portuguese Support

### Prerequisites

- Game installation with Portuguese assets
- OpenSAGE built successfully
- Logs configured to see debug output

### Step 1: Prepare Environment

```bash
export CNC_GENERALS_PATH="$HOME/GeneralsX/Generals"
export CNC_GENERALS_ZH_PATH="$HOME/GeneralsX/GeneralsZH"
```

### Step 2: Launch Game

```bash
cd /Users/felipebraz/PhpstormProjects/pessoal/OpenSAGE

# Build first
dotnet build src/OpenSage.Launcher/OpenSage.Launcher.csproj

# Run with Portuguese game
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj \
    -- --game CncGeneralsZH --noshellmap --developermode
```

### Step 3: Check Logs

Look for these log messages:

**Good output (Portuguese detected correctly):**
```
[INFO] [LanguageMapper] Language code 13 = pt-BR
[INFO] [LanguageUtility] Detected language: portuguese
[INFO] [CsfTranslationProvider] [CSF: pt-BR - 1234 strings in 45 categories]
```

**Bad output (unknown language):**
```
[WARN] [LanguageMapper] Unknown language code 13 for game CncGeneralsZeroHour
[WARN] [LanguageUtility] Language unknown. Using fallback: en-US
```

### Step 4: Verify UI Rendering

- ✅ **Working:** Main menu displays correctly, no white screen
- ✅ **Working:** All text renders (mission names, unit names, etc.)
- ✅ **Working:** Portuguese font displays correctly
- ❌ **Not working:** White screen indicates language detection failure
- ❌ **Not working:** UI cut off or overlapping indicates encoding mismatch

## Advanced Testing

### Test Multiple Languages

```bash
# Test English (should always work)
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj \
    -- --game CncGenerals --noshellmap

# Test German
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj \
    -- --game CncGenerals --noshellmap

# Test French
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj \
    -- --game CncGenerals --noshellmap
```

### Test Language Fallback

Modify a CSF file's language code to 99 (unknown) and verify:

1. Engine detects unknown code
2. Logs show warning
3. Attempts language family fallback
4. Falls back to en-US
5. **Game still renders** (no crash)

```csharp
// In test code
LanguageMapper.TryMapLanguage(99, SageGame.CncGeneralsZeroHour, out var lang);
Assert.AreEqual("en-US", lang);  // Should fallback to en-US
```

### Test Custom Language Registration

```csharp
// Register a custom language
LanguageMapper.RegisterCustomLanguage(
    languageCode: 50,
    languageName: "custom-lang",
    game: SageGame.CncGeneralsZeroHour
);

// Verify it's available
var supported = LanguageMapper.GetSupportedLanguages(SageGame.CncGeneralsZeroHour);
Assert.Contains("custom-lang", supported);

// Try to get the code back
var code = LanguageMapper.GetLanguageCode("custom-lang", SageGame.CncGeneralsZeroHour);
Assert.AreEqual(50, code);
```

## Debugging Tips

### Enable Verbose Logging

Create or edit `DeveloperMode.ini`:

```ini
[Logging]
Level=Debug
ShowThreadId=True
ShowMethodNames=True
```

### Check Language Codes in CSF Files

```bash
# Hex dump the first 48 bytes of a CSF file to see the language code
od -Ax -t x1z -N 48 "Data/portuguese/generals.csf"

# Output should show:
# Offset 20-23 (bytes 32-35): Language code
# Example: 0d 00 00 00 = 13 (Portuguese Brazilian)
```

### Verify File Structure

```bash
# List all files the engine is looking for
ls -la "Data/"
ls -la "Data/portuguese/"
ls -la "Audio"*

# Expected Portuguese structure:
# Data/portuguese/generals.csf
# AudioPortuguese.big (or Audio Portuguese.big)
```

## Common Issues & Solutions

### White screen on Portuguese

**Symptom:** Game starts, main menu is white

**Diagnosis:**
- Check logs for "Unknown language id"
- Verify CSF language code matches mapping
- Check if pt-BR files are in the correct path

**Fix:**

Add diagnostic code to Program.cs:

```csharp
Logger.Info($"Language detection: {LanguageUtility.ReadCurrentLanguage(gameDefinition, fileSystem)}");
Logger.Info($"Supported languages: {string.Join(", ", LanguageMapper.GetSupportedLanguages(gameDefinition.Game))}");
```

### English UI loads instead of Portuguese

**Symptom:** Game works but text is in English

**Possible causes:**
- Registry still pointing to English (Windows)
- Filesystem detection found Audio.big instead of AudioPortuguese.big
- CSF file header has wrong language code

**Debug:**

```csharp
// Check what language was detected
var detected = LanguageUtility.ReadCurrentLanguage(gameDefinition, fileSystem);
Console.WriteLine($"Detected: {detected}");  // Should be "portuguese"

// Check if files exist
var audioFile = fileSystem.GetFilesInDirectory("", "Audio*.big").FirstOrDefault();
Console.WriteLine($"Audio file found: {audioFile}");
```

### Crashes with unknown language

**Symptom:** Engine crashes when loading unsupported language code

**Status:** This should NOT happen with new implementation - report as bug!

**Expected behavior:** Should log warning and use fallback

## Performance Testing

### Language Mapper Performance

```csharp
var sw = Stopwatch.StartNew();
for (int i = 0; i < 10000; i++)
{
    LanguageMapper.TryMapLanguage(13, SageGame.CncGeneralsZeroHour, out _);
}
sw.Stop();
Console.WriteLine($"10000 lookups: {sw.ElapsedMilliseconds}ms");
// Expected: < 10ms (dictionary lookup is O(1))
```

### CSF Loading Performance

Language mapping should have minimal impact on load times since it's only done once during initialization.

## Regression Testing

Before committing language-related changes:

1. ✅ Test all original languages load without warnings
2. ✅ Test Portuguese loads without white screen
3. ✅ Test unknown language codes don't crash
4. ✅ Test fallback logic works correctly
5. ✅ Test custom language registration
6. ✅ Test reverse mapping (name → code)

## Unit Tests

Run the language-related tests:

```bash
dotnet test src/OpenSage.Game.Tests/ \
    --filter "Language" \
    --verbosity normal
```

Look for tests in:
- `src/OpenSage.Game.Tests/Content/Translation/LanguageMapperTests.cs`
- `src/OpenSage.Game.Tests/Content/Translation/Providers/CsfTranslationProviderTests.cs`

## Integration Testing

Full game test with Portuguese:

```bash
# Build and run integration tests
dotnet test src/OpenSage.Game.Tests/ \
    --filter "IntegrationTests" \
    --configuration Release

# Should test:
# - Loading Portuguese CSF files
# - Rendering Portuguese UI
# - All strings resolve correctly
# - No console errors or warnings
```

---

**Last Updated:** December 2025
**Maintainer:** Bender (GitHub Copilot)
