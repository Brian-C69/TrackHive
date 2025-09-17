using System;
using System.Collections.Generic;

namespace TrackHive.Models;

public static class LanguagePreferences
{
    public const string DefaultLanguage = "en";

    public static readonly IReadOnlyList<LanguagePreferenceOption> SupportedLanguages = new[]
    {
        new LanguagePreferenceOption("en", "English"),
        new LanguagePreferenceOption("ms", "Malay"),
        new LanguagePreferenceOption("zh", "Mandarin")
    };

    public static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return DefaultLanguage;
        }

        foreach (var option in SupportedLanguages)
        {
            if (string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase))
            {
                return option.Value;
            }
        }

        return DefaultLanguage;
    }
}

public sealed record LanguagePreferenceOption(string Value, string Label);
