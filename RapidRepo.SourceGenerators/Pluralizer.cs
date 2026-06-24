using System;

namespace RapidRepo.SourceGenerators;

internal static class Pluralizer
{
    /// <summary>
    /// Derives a plural property name from a repository interface name.
    /// Strips the leading 'I' and trailing 'Repository', then applies simple English pluralization rules.
    /// Irregular names should use [UnitOfWorkProperty("Name")] to override.
    /// </summary>
    internal static string Pluralize(string interfaceName)
    {
        // Strip leading 'I'
        var stem = interfaceName.StartsWith("I", StringComparison.Ordinal) && interfaceName.Length > 1
            ? interfaceName.Substring(1)
            : interfaceName;

        // Strip trailing 'Repository'
        const string suffix = "Repository";
        if (stem.EndsWith(suffix, StringComparison.Ordinal) && stem.Length > suffix.Length)
            stem = stem.Substring(0, stem.Length - suffix.Length);

        if (stem.Length == 0) return interfaceName;

        // Pluralize
        if (stem.EndsWith("y", StringComparison.Ordinal) && stem.Length >= 2)
        {
            var penultimate = stem[stem.Length - 2];
            if (penultimate != 'a' && penultimate != 'e' && penultimate != 'i' && penultimate != 'o' && penultimate != 'u')
                return stem.Substring(0, stem.Length - 1) + "ies"; // Category → Categories
        }

        if (stem.EndsWith("s", StringComparison.Ordinal)
            || stem.EndsWith("sh", StringComparison.Ordinal)
            || stem.EndsWith("ch", StringComparison.Ordinal)
            || stem.EndsWith("x", StringComparison.Ordinal)
            || stem.EndsWith("z", StringComparison.Ordinal))
        {
            return stem + "es"; // Match → Matches
        }

        return stem + "s"; // UserProvider → UserProviders
    }
}
