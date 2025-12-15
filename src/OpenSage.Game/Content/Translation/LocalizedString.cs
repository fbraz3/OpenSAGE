#nullable enable

using System;
using System.Linq;
using System.Runtime.InteropServices;

namespace OpenSage.Content.Translation;

public sealed class LocalizedString
{
    public string Original { get; }
    private string? _localized;

    public LocalizedString(string original)
    {
        Original = original;
    }

    public string Localize(params object[] args)
    {
        _localized ??= Original?.Translate() ?? string.Empty;
        
        if (args.Length == 0)
        {
            return _localized;
        }

        // On macOS and non-Windows platforms, SprintfNET's P/Invoke to swprintf fails.
        // Use .NET's string.Format as a fallback for compatibility.
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return string.Format(_localized, args);
        }

        try
        {
            return SprintfNET.StringFormatter.PrintF(_localized, args);
        }
        catch (DllNotFoundException)
        {
            // Fallback if swprintf is unavailable on Windows
            return string.Format(_localized, args);
        }
    }
    public static LocalizedString CreateApt(string original)
    {
        var trimmed = original.Replace("$", "APT:") // All string values begin with $
            .Split('&').First(); // Query strings after ampersand

        return new LocalizedString(trimmed);
    }
}
