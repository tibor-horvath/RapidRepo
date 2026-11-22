using System.Diagnostics;
using System.Reflection;

namespace RapidRepo.Extensions.DependencyInjection;

public sealed partial class RapidRepoOptions
{
    private readonly List<Assembly> _assemblies = [];
    private readonly List<Func<Type, bool>> _includes = [];
    private readonly List<Func<Type, bool>> _excludes = [];

    internal IReadOnlyList<Assembly> Assemblies => _assemblies;
    internal IReadOnlyList<Func<Type, bool>> Includes => _includes;
    internal IReadOnlyList<Func<Type, bool>> Excludes => _excludes;

    /// <summary>Add assemblies to scan for repository implementations. Duplicates are ignored.</summary>
    /// <exception cref="ArgumentNullException"><paramref name="assemblies"/> or one of its elements is <c>null</c>.</exception>
    public RapidRepoOptions ScanAssemblies(params Assembly[] assemblies)
    {
        ArgumentNullException.ThrowIfNull(assemblies);

        // Checked before adding any, so a null element leaves the scan list unchanged.
        foreach (var assembly in assemblies)
            ArgumentNullException.ThrowIfNull(assembly, nameof(assemblies));

        foreach (var assembly in assemblies)
            if (!_assemblies.Contains(assembly))
                _assemblies.Add(assembly);
        return this;
    }

    /// <summary>Add the assembly that contains <typeparamref name="TMarker"/> to the scan list. Duplicates are ignored.</summary>
    public RapidRepoOptions ScanAssembliesContaining<TMarker>()
    {
        var assembly = typeof(TMarker).Assembly;
        if (!_assemblies.Contains(assembly))
            _assemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Add the calling assembly to the scan list.
    /// <para>
    /// Prefer <see cref="ScanAssembliesContaining{TMarker}"/> when possible — it is unambiguous
    /// regardless of how the application is hosted or tested.
    /// </para>
    /// </summary>
    public RapidRepoOptions ScanCallingAssembly()
    {
        var assembly = new StackFrame(1, needFileInfo: false).GetMethod()?.DeclaringType?.Assembly
            ?? Assembly.GetEntryAssembly()
            ?? throw new InvalidOperationException(
                "Could not determine the calling assembly. Use ScanAssembliesContaining<T>() instead.");

        if (!_assemblies.Contains(assembly))
            _assemblies.Add(assembly);
        return this;
    }

    /// <summary>
    /// Additive include predicate. A type is registered only if at least one include predicate returns <c>true</c>
    /// (when any includes are configured). Evaluated after excludes; exclude always wins.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    public RapidRepoOptions Include(Func<Type, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _includes.Add(predicate);
        return this;
    }

    /// <summary>
    /// Additive exclude predicate. A type that matches any exclude predicate is skipped,
    /// regardless of include predicates.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="predicate"/> is <c>null</c>.</exception>
    public RapidRepoOptions Exclude(Func<Type, bool> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _excludes.Add(predicate);
        return this;
    }

    /// <summary>
    /// Include only types whose namespace starts with <paramref name="ns"/>.
    /// Equivalent to <c>Include(t =&gt; t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true)</c>.
    /// </summary>
    /// <remarks>
    /// The match is a plain prefix check: <c>"MyApp.Data"</c> matches <c>"MyApp.DataV2"</c>.
    /// Add a trailing dot — <c>"MyApp.Data."</c> — to enforce namespace-segment boundaries.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="ns"/> is <c>null</c>.</exception>
    public RapidRepoOptions IncludeNamespace(string ns)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return Include(t => t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Exclude types whose namespace starts with <paramref name="ns"/>.
    /// Equivalent to <c>Exclude(t =&gt; t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true)</c>.
    /// </summary>
    /// <remarks>
    /// The match is a plain prefix check: <c>"MyApp.Data"</c> matches <c>"MyApp.DataV2"</c>.
    /// Add a trailing dot — <c>"MyApp.Data."</c> — to enforce namespace-segment boundaries.
    /// </remarks>
    /// <exception cref="ArgumentNullException"><paramref name="ns"/> is <c>null</c>.</exception>
    public RapidRepoOptions ExcludeNamespace(string ns)
    {
        ArgumentNullException.ThrowIfNull(ns);
        return Exclude(t => t.Namespace?.StartsWith(ns, StringComparison.Ordinal) == true);
    }

    /// <summary>
    /// Include only the exact concrete type <typeparamref name="T"/>.
    /// Equivalent to <c>Include(t =&gt; t == typeof(T))</c>.
    /// </summary>
    public RapidRepoOptions IncludeType<T>() => Include(t => t == typeof(T));

    /// <summary>
    /// Exclude the exact concrete type <typeparamref name="T"/>.
    /// Equivalent to <c>Exclude(t =&gt; t == typeof(T))</c>.
    /// </summary>
    public RapidRepoOptions ExcludeType<T>() => Exclude(t => t == typeof(T));
}
