using System;
using System.Collections.Generic;
using System.Linq;
using NLog;

namespace OpenSage.Content.Translation;

/// <summary>
/// Provides dynamic language code to language name mapping.
/// Supports both original EA languages and community-added languages like Portuguese.
/// </summary>
public sealed class LanguageMapper
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    /// <summary>
    /// Mapping for Command & Conquer Generals/Zero Hour language codes
    /// </summary>
    private static readonly Dictionary<uint, string> GeneralsLanguageMap = new()
    {
        { 0, "en-US" },     // LANGUAGE_ID_US
        { 1, "en-UK" },     // LANGUAGE_ID_UK
        { 2, "de-DE" },     // LANGUAGE_ID_GERMAN
        { 3, "fr-FR" },     // LANGUAGE_ID_FRENCH
        { 4, "es-ES" },     // LANGUAGE_ID_SPANISH
        { 5, "it-IT" },     // LANGUAGE_ID_ITALIAN
        { 6, "ja-JP" },     // LANGUAGE_ID_JAPANESE
        { 7, "en-US" },     // LANGUAGE_ID_JABBER (fallback to US)
        { 8, "ko-KR" },     // LANGUAGE_ID_KOREAN
        { 9, "zh-Hant" },   // Chinese Traditional (commonly added)
        { 12, "pl-PL" },    // Polish (commonly added)
        { 13, "pt-BR" },    // Portuguese Brazilian (community mod)
        { 14, "pt-PT" },    // Portuguese European (community mod)
    };

    /// <summary>
    /// Mapping for BFME/BFME2 language codes
    /// </summary>
    private static readonly Dictionary<uint, string> BfmeLanguageMap = new()
    {
        { 0, "en-US" },     // English
        { 1, "es-ES" },     // Spanish
        { 2, "es-ES" },     // (duplicate/alternate Spanish)
        { 3, "de-DE" },     // German
        { 4, "fr-FR" },     // French
        { 5, "it-IT" },     // Italian
        { 6, "nl-NL" },     // Dutch
        { 7, "nl-NL" },     // (duplicate/alternate Dutch)
        { 8, "pl-PL" },     // Polish
        { 11, "pl-PL" },    // Polish (alternate)
        { 9, "nb-NO" },     // Norwegian
        { 10, "zh-Hant" },  // Chinese Traditional
        { 17, "ru-RU" },    // Russian
        { 13, "pt-BR" },    // Portuguese Brazilian (community mod)
        { 14, "pt-PT" },    // Portuguese European (community mod)
    };

    /// <summary>
    /// Reverse mapping: language code string to numeric ID
    /// Used for writing language info back to CSF files
    /// </summary>
    private static readonly Dictionary<string, uint> ReverseGeneralsMap = GeneralsLanguageMap
        .GroupBy(x => x.Value)
        .ToDictionary(g => g.Key, g => g.First().Key);

    private static readonly Dictionary<string, uint> ReverseBfmeMap = BfmeLanguageMap
        .GroupBy(x => x.Value)
        .ToDictionary(g => g.Key, g => g.First().Key);

    private const string DefaultLanguage = "en-US";

    /// <summary>
    /// Try to map a language code to a language name/tag
    /// </summary>
    public static bool TryMapLanguage(uint languageCode, SageGame game, out string languageName)
    {
        var map = game switch
        {
            SageGame.CncGenerals or SageGame.CncGeneralsZeroHour => GeneralsLanguageMap,
            SageGame.Bfme or SageGame.Bfme2 or SageGame.Bfme2Rotwk => BfmeLanguageMap,
            _ => new Dictionary<uint, string>()
        };

        if (map.TryGetValue(languageCode, out languageName))
        {
            return true;
        }

        // Language code not recognized
        Logger.Warn($"Unknown language code {languageCode} for game {game}. Will use fallback language {DefaultLanguage}");
        languageName = DefaultLanguage;
        return false;
    }

    /// <summary>
    /// Get all supported languages for a specific game
    /// </summary>
    public static IEnumerable<string> GetSupportedLanguages(SageGame game)
    {
        var map = game switch
        {
            SageGame.CncGenerals or SageGame.CncGeneralsZeroHour => GeneralsLanguageMap,
            SageGame.Bfme or SageGame.Bfme2 or SageGame.Bfme2Rotwk => BfmeLanguageMap,
            _ => new Dictionary<uint, string>()
        };

        return map.Values.Distinct().OrderBy(x => x);
    }

    /// <summary>
    /// Reverse lookup: get the language code for a language name
    /// Used when writing CSF files with custom languages
    /// </summary>
    public static uint GetLanguageCode(string languageName, SageGame game)
    {
        var reverseMap = game switch
        {
            SageGame.CncGenerals or SageGame.CncGeneralsZeroHour => ReverseGeneralsMap,
            SageGame.Bfme or SageGame.Bfme2 or SageGame.Bfme2Rotwk => ReverseBfmeMap,
            _ => new Dictionary<string, uint>()
        };

        if (reverseMap.TryGetValue(languageName, out var code))
        {
            return code;
        }

        // Default to US English if language not found
        Logger.Warn($"Language {languageName} not recognized for game {game}. Using default language code.");
        return 0;
    }

    /// <summary>
    /// Get the best fallback language for a given language
    /// E.g., if pt-BR is not available, try pt-PT, then es-ES
    /// </summary>
    public static string GetFallbackLanguage(string languageName, SageGame game)
    {
        var supportedLanguages = GetSupportedLanguages(game).ToList();

        // If the language is already supported, return it
        if (supportedLanguages.Contains(languageName))
        {
            return languageName;
        }

        // Try to find a fallback based on language family
        var languageBase = languageName.Split('-')[0].ToLowerInvariant();

        // Find any language with the same base (e.g., pt-BR -> pt-PT or pt-* )
        var sameFamilyLang = supportedLanguages.FirstOrDefault(l =>
            l.Split('-')[0].Equals(languageBase, StringComparison.OrdinalIgnoreCase));

        if (sameFamilyLang != null)
        {
            Logger.Info($"Language {languageName} not directly supported. Using family fallback: {sameFamilyLang}");
            return sameFamilyLang;
        }

        // Ultimate fallback: use English US
        Logger.Warn($"No fallback available for language {languageName}. Using default: {DefaultLanguage}");
        return DefaultLanguage;
    }

    /// <summary>
    /// Register a custom language mapping (for mods or user-defined languages)
    /// </summary>
    public static void RegisterCustomLanguage(uint languageCode, string languageName, SageGame game)
    {
        var map = game switch
        {
            SageGame.CncGenerals or SageGame.CncGeneralsZeroHour => GeneralsLanguageMap,
            SageGame.Bfme or SageGame.Bfme2 or SageGame.Bfme2Rotwk => BfmeLanguageMap,
            _ => null
        };

        if (map == null)
        {
            Logger.Warn($"Cannot register custom language for unknown game {game}");
            return;
        }

        map[languageCode] = languageName;
        Logger.Info($"Registered custom language mapping: {languageCode} -> {languageName} for {game}");

        // Update reverse map
        var reverseMap = game switch
        {
            SageGame.CncGenerals or SageGame.CncGeneralsZeroHour => ReverseGeneralsMap,
            SageGame.Bfme or SageGame.Bfme2 or SageGame.Bfme2Rotwk => ReverseBfmeMap,
            _ => null
        };

        if (reverseMap != null && !reverseMap.ContainsKey(languageName))
        {
            reverseMap[languageName] = languageCode;
        }
    }
}
