using System.Collections.Concurrent;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace RapidRepo.Tenancy;

/// <summary>
/// What counts as "no tenant": <see langword="null"/>, the default value of a value-type key, and (as a
/// misconfiguration) an empty string. Shared by the core and the providers, so every source of a tenant is read the
/// same way.
/// </summary>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public static class TenantKeys
{
    // The boxed default of each value-type key, created once: IsNone runs on every read and every query.
    private static readonly ConcurrentDictionary<Type, object> Defaults = new();

    /// <summary>
    /// Gets a value indicating whether <paramref name="value"/> means "no tenant".
    /// </summary>
    /// <typeparam name="T">The tenant key type.</typeparam>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> for <see langword="null"/>, the default value, or an empty string.</returns>
    public static bool IsNone<T>(T value) =>
        value is null
        || EqualityComparer<T>.Default.Equals(value, default!)
        || value is string { Length: 0 };

    /// <summary>
    /// Gets a value indicating whether the boxed <paramref name="value"/> means "no tenant".
    /// </summary>
    /// <param name="value">The value, boxed.</param>
    /// <returns><see langword="true"/> for <see langword="null"/>, the default value, or an empty string.</returns>
    public static bool IsNone(object? value) =>
        value is null
        || value is string { Length: 0 }
        || (value.GetType().IsValueType && value.Equals(DefaultOf(value.GetType())));

    /// <summary>
    /// Turns a tenant source's answer into a tenant, or <see langword="null"/> for none.
    /// </summary>
    /// <param name="value">The answer.</param>
    /// <param name="source">The source, named in the error message.</param>
    /// <returns>The tenant, or <see langword="null"/> for none.</returns>
    /// <exception cref="TenantViolationException">The source returned an empty string, which is a misconfiguration.</exception>
    public static object? Normalize(object? value, string source)
    {
        if (value is string { Length: 0 })
        {
            throw new TenantViolationException(
                $"{source} returned an empty string as the current tenant. An empty string is not a tenant; return " +
                "null when there is none.");
        }

        return ToTenantOrNull(value);
    }

    /// <summary>
    /// Turns a tenant source's answer into a tenant, or <see langword="null"/> for none, treating an empty string as
    /// none instead of throwing. For code that cannot throw, such as a query filter.
    /// </summary>
    /// <param name="value">The answer.</param>
    /// <returns>The tenant, or <see langword="null"/> for none.</returns>
    public static object? ToTenantOrNull(object? value) => IsNone(value) ? null : value;

    private static object DefaultOf(Type valueType) =>
        Defaults.GetOrAdd(valueType, RuntimeHelpers.GetUninitializedObject);
}
