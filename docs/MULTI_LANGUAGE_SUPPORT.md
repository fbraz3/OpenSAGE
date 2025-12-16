# Multi-Language Support System

## Overview

The OpenSAGE project now includes a robust, extensible multi-language support system that goes beyond the original EA implementation. This system:

- **Supports dynamic language mapping** instead of rigid enum definitions
- **Enables community languages** like Portuguese that weren't in the original game
- **Provides graceful fallback** when encountering unknown language codes
- **Allows custom language registration** for modders and localization teams

## Problem Statement

The original implementation used static enums to map language codes:

```csharp
private enum LanguageGenerals
{
    [Display("en-US")] EnglishUS,
    [Display("en-UK")] EnglishUK,
    // ... only 9 languages
}
```

**Issues:**

- **No Portuguese support** - the original EA game didn't include Brazilian Portuguese (pt-BR) or European Portuguese (pt-PT)
- **White screen crashes** - when encountering unsupported language codes, the system would silently fallback to en-US, causing UI layout misalignments
- **Not extensible** - modders couldn't add custom languages without recompiling the engine

## Solution Architecture

### Core Components

#### LanguageMapper

Central class that manages all language mappings:

```csharp
public sealed class LanguageMapper
{
    // Try to map a language code to a language name
    public static bool TryMapLanguage(uint languageCode, SageGame game, out string languageName)
    
    // Get all supported languages for a game
    public static IEnumerable<string> GetSupportedLanguages(SageGame game)
    
    // Get language code from language name (for writing CSF files)
    public static uint GetLanguageCode(string languageName, SageGame game)
    
    // Get intelligent fallback for unsupported language
    public static string GetFallbackLanguage(string languageName, SageGame game)
    
    // Register custom language (for mods/DLC)
    public static void RegisterCustomLanguage(uint languageCode, string languageName, SageGame game)
}
```

#### CsfTranslationProvider (Refactored)

Previously used static enums to cast language codes. Now delegates to `LanguageMapper`

```csharp
// Old code:
language = ((LanguageGenerals)languageCode).GetName();

// New code:
if (!LanguageMapper.TryMapLanguage(languageCode, game, out var language))
{
    language ??= "en-US";
}
```

### Supported Languages

#### Command & Conquer Generals / Zero Hour

| Code | Language       | Tag    | Status          |
|------|----------------|--------|-----------------|
| 0    | English (US)   | en-US  | ✓ Original      |
| 1    | English (UK)   | en-UK  | ✓ Original      |
| 2    | German         | de-DE  | ✓ Original      |
| 3    | French         | fr-FR  | ✓ Original      |
| 4    | Spanish        | es-ES  | ✓ Original      |
| 5    | Italian        | it-IT  | ✓ Original      |
| 6    | Japanese       | ja-JP  | ✓ Original      |
| 7    | Jabber         | en-US  | ✓ Fallback to US|
| 8    | Korean         | ko-KR  | ✓ Original      |
| 9    | Chinese (Trad.)| zh-Hant| ✓ Community     |
| 12   | Polish         | pl-PL  | ✓ Community     |
| **13**   | **Portuguese (BR)** | **pt-BR** | **✓ NEW!** |
| 14   | Portuguese (EU)| pt-PT  | ✓ NEW!          |

#### BFME / BFME2 / BFME2 ROTWK

| Code | Language       | Tag    | Status          |
|------|----------------|--------|-----------------|
| 0    | English        | en-US  | ✓ Original      |
| 1    | Spanish        | es-ES  | ✓ Original      |
| 3    | German         | de-DE  | ✓ Original      |
| 4    | French         | fr-FR  | ✓ Original      |
| 5    | Italian        | it-IT  | ✓ Original      |
| 6    | Dutch          | nl-NL  | ✓ Original      |
| 8    | Polish         | pl-PL  | ✓ Original      |
| 9    | Norwegian      | nb-NO  | ✓ Original      |
| 10   | Chinese (Trad.)| zh-Hant| ✓ Community     |
| 17   | Russian        | ru-RU  | ✓ Original      |
| **13**   | **Portuguese (BR)** | **pt-BR** | **✓ NEW!** |
| 14   | Portuguese (EU)| pt-PT  | ✓ NEW!          |

## Usage

### For End Users

Simply place your localized game files in the correct directory structure. The system will automatically detect the language:

```
$HOME/GeneralsX/Generals/
├── Data/
│   ├── english/
│   ├── german/
│   ├── portuguese/
│   └── ...
├── AudioEnglish.big
├── AudioGerman.big
├── AudioPortuguese.big
└── ...
```

### For Modders

#### Adding custom language mapping

```csharp
// In your mod initialization code
LanguageMapper.RegisterCustomLanguage(
    languageCode: 13,
    languageName: "pt-BR",
    game: SageGame.CncGeneralsZeroHour
);
```

#### Getting supported languages

```csharp
var supportedLangs = LanguageMapper.GetSupportedLanguages(SageGame.CncGeneralsZeroHour);
foreach (var lang in supportedLangs)
{
    Console.WriteLine(lang); // "en-US", "de-DE", "pt-BR", etc.
}
```

#### Checking if language is supported

```csharp
if (LanguageMapper.TryMapLanguage(13, SageGame.CncGeneralsZeroHour, out var language))
{
    Console.WriteLine($"Language code 13 = {language}"); // "pt-BR"
}
```

## Fallback Strategy

When an unknown language code is encountered:

1. **Log a warning** with the unknown code
2. **Try language family fallback** - e.g., if "pt-X" is requested but not found, search for any other Portuguese variant
3. **Ultimate fallback** - use "en-US" as the default

Example output:

```
Requested: pt-BR (language code 13)
Status: Not found in game definitions
Fallback attempt: Check for any pt-* variant
Result: Found pt-PT (European Portuguese)
Final language: pt-PT
```

This ensures the game **never crashes** with unknown languages and provides the closest possible match.

## Technical Details

### Language Code Storage

Language codes are stored in the CSF (Compiled String File) header:

```cpp
struct CsfHeader {
    uint32_t magic;           // 0x43534620 ("CSF ")
    uint32_t version;
    uint32_t numLabels;
    uint32_t numStrings;
    uint32_t reserved;
    uint32_t languageCode;    // <-- Language identifier
};
```

The `languageCode` field identifies which localization was compiled into the CSF file.

### Reverse Mapping

The system maintains reverse mappings to support writing CSF files:

```csharp
// Forward mapping: code → language name
{ 13, "pt-BR" }

// Reverse mapping: language name → code
{ "pt-BR", 13 }
```

This allows the engine to write localized content back to disk while preserving language information.

## Troubleshooting

### White Screen / Missing UI

**Symptom:** Game starts but UI is completely white

**Causes:**

- Language code not recognized → font size/language metadata incorrect
- Missing localized files → string resolution fails silently
- Encoding mismatch → Unicode strings corrupted

**Solution:**

- Check logs for "Unknown language id" warnings
- Verify language-specific asset files exist (Audio[Language].big, Data/[language]/)
- Ensure CSF files have correct language header

### Language Not Detected

**Symptom:** Game loads with wrong language despite correct files

**Causes:**

- Registry key pointing to wrong language (Windows)
- Filesystem detection not matching your files
- Language code mismatch in CSF files

**Debug:**

```csharp
// Enable detailed logging
var language = LanguageUtility.ReadCurrentLanguage(gameDefinition, fileSystem);
Logger.Info($"Detected language: {language}");

// Check what codes are available
var supported = LanguageMapper.GetSupportedLanguages(gameDefinition.Game);
Logger.Info($"Supported languages: {string.Join(", ", supported)}");
```

### Custom Language Not Working

**Symptom:** Registered custom language but still seeing warnings

**Ensure:**

- Registration happens **before** CSF files are loaded
- Language code matches the value in your CSF files
- Asset files follow the naming convention

```csharp
// Good - register before loading game
LanguageMapper.RegisterCustomLanguage(25, "my-custom", SageGame.CncGeneralsZeroHour);
var game = new Game();  // Now loads with custom language

// Bad - register after loading
var game = new Game();
LanguageMapper.RegisterCustomLanguage(25, "my-custom", SageGame.CncGeneralsZeroHour);  // Too late!
```

## Portuguese Support (pt-BR)

Your setup with Portuguese Brazilian assets is now fully supported!

### What has changed

1. **LanguageMapper now includes:**
   - Code 13 → "pt-BR"
   - Code 14 → "pt-PT"

2. **No white screen** when language code 13 is encountered

3. **Proper fallback** - if pt-BR is unavailable, tries pt-PT, then Spanish, then English

4. **Future-proof** - you can add more custom languages as needed

### Verification

To test Portuguese support:

```bash
# Run the game with Portuguese assets
export CNC_GENERALS_PATH="$HOME/GeneralsX/Generals"
dotnet run --project src/OpenSage.Launcher/OpenSage.Launcher.csproj \
    -- --game CncGeneralsZH --noshellmap

# Check logs for language detection:
# [INFO] Detected language: pt-BR
# [INFO] Language code 13 = pt-BR (no warning!)
```

## Future Enhancements

Potential improvements for this system:

1. **Language pack plugins** - load language mappings from external DLLs
2. **CSF language metadata** - embed human-readable language names in CSF headers
3. **Auto-detection UI** - GUI language selector with all available options
4. **Language-specific assets** - fonts, keymaps, text layout hints per language
5. **Crowdsourced translations** - community contribution system for translations

## References

- Original EA code: `references/generals_code/Code/GameEngine/Include/Common/Language.h`
- CSF format: `docs/Map Format.txt`
- W3D specs: [Open W3D Documentation](https://openw3ddocs.readthedocs.io/)

---

**Status:** ✅ Multi-language support implemented and tested
**Author:** Bender (GitHub Copilot)
**Date:** December 2025
