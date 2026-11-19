using Microsoft.Extensions.DependencyInjection;
using RapidRepo.UnitOfWork;
using System.Diagnostics;
using System.Reflection;

namespace RapidRepo.Extensions.DependencyInjection;

/// <summary>
/// Configuration options for <see cref="RapidRepoServiceCollectionExtensions.AddRapidRepo"/>.
/// </summary>
public sealed class RapidRepoOptions
{
    private readonly List<Assembly> _assemblies = [];
    private readonly List<Func<Type, bool>> _includes = [];
    private readonly List<Func<Type, bool>> _excludes = [];
    private readonly List<(Type InterfaceType, Type ImplementationType)> _unitOfWorkRegistrations = [];

    /// <summary>Lifetime applied to every discovered repository. Default: <see cref="ServiceLifetime.Scoped"/>.</summary>
    public ServiceLifetime Lifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>Also register each concrete repository against its own type.</summary>
    public bool RegisterAsSelf { get; set; }

    /// <summary>Throw when two concrete types implement the same user-defined interface. Default: <c>true</c>.</summary>
    public bool ThrowOnAmbiguousRegistration { get; set; } = true;

    /// <summary>Throw instead of warn when <see cref="Lifetime"/> is <see cref="ServiceLifetime.Singleton"/>. Default: <c>false</c>.</summary>
    public bool ThrowOnSingletonMisuse { get; set; }

    /// <summary>
    /// Have the provider register its generic repository implementation for the root repository interfaces
    /// (<see cref="RapidRepoRegistrationContext.RootInterfaces"/>), so they can be injected without writing a
    /// repository class.
    /// Specific registrations from assembly scanning always take precedence.
    /// Requires a provider (see <see cref="UseProvider"/>). Default: <c>false</c>.
    /// </summary>
    public bool RegisterGenericRepositories { get; set; }

    internal IRapidRepoProvider? Provider { get; private set; }

    internal IReadOnlyList<Assembly> Assemblies => _assemblies;
    internal IReadOnlyList<Func<Type, bool>> Includes => _includes;
    internal IReadOnlyList<Func<Type, bool>> Excludes => _excludes;
    internal IReadOnlyList<(Type InterfaceType, Type ImplementationType)> UnitOfWorkRegistrations => _unitOfWorkRegistrations;

    /// <summary>Add assemblies to scan for repository implementations. Duplicates are ignored.</summary>
    public RapidRepoOptions ScanAssemblies(params Assembly[] assemblies)
    {
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
    /// Use <paramref name="provider"/> for the provider-specific registrations of this <c>AddRapidRepo</c> call.
    /// Provider packages wrap this in their own extension, for example <c>UseEntityFrameworkCore&lt;TContext&gt;()</c>.
    /// </summary>
    /// <param name="provider">The persistence provider.</param>
    /// <returns>The same options, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A provider has already been configured for this call.</exception>
    public RapidRepoOptions UseProvider(IRapidRepoProvider provider)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (Provider is not null)
            throw new InvalidOperationException(
                $"A provider has already been configured for this AddRapidRepo call ('{Provider.Name}'). " +
                "Configure one provider per AddRapidRepo call, and use a separate AddRapidRepo call per provider " +
                "or data source.");

        Provider = provider;
        return this;
    }

    /// <summary>
    /// Register a unit of work. Registers <typeparamref name="TInterface"/> → <typeparamref name="TImpl"/>
    /// and also forwards any closed <c>IUnitOfWork&lt;TKey&gt;</c> that <typeparamref name="TImpl"/> implements.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// <typeparamref name="TInterface"/> is not an interface type, or <typeparamref name="TImpl"/> is abstract.
    /// </exception>
    /// <exception cref="InvalidOperationException"><typeparamref name="TInterface"/> has already been registered.</exception>
    public RapidRepoOptions UseUnitOfWork<TInterface, TImpl>()
        where TInterface : class
        where TImpl : class, TInterface
    {
        if (!typeof(TInterface).IsInterface)
            throw new ArgumentException(
                $"'{typeof(TInterface).Name}' is not an interface type. TInterface must be an interface.",
                nameof(TInterface));

        EnsureConcrete(typeof(TImpl), nameof(TImpl));
        return AddUnitOfWork(typeof(TInterface), typeof(TImpl));
    }

    /// <summary>
    /// Register a unit of work by automatically detecting the single user-defined
    /// <see cref="IUnitOfWork{TUserKey}"/>-derived interface implemented by <typeparamref name="TImpl"/>.
    /// Use the two-type overload <see cref="UseUnitOfWork{TInterface,TImpl}"/> when
    /// <typeparamref name="TImpl"/> has zero or more than one such interface.
    /// </summary>
    /// <exception cref="ArgumentException"><typeparamref name="TImpl"/> is abstract.</exception>
    /// <exception cref="InvalidOperationException">
    /// <typeparamref name="TImpl"/> implements no user-defined <see cref="IUnitOfWork{TUserKey}"/> interface,
    /// implements more than one, or the detected interface has already been registered.
    /// </exception>
    public RapidRepoOptions UseUnitOfWork<TImpl>()
        where TImpl : class
    {
        var implType = typeof(TImpl);

        EnsureConcrete(implType, nameof(TImpl));

        // Collect user-defined interfaces that extend IUnitOfWork<TKey> but are not the root itself.
        var candidateInterfaces = implType.GetInterfaces()
            .Where(i => i.IsInterface
                        && !(i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IUnitOfWork<>))
                        && i.GetInterfaces().Any(p => p.IsGenericType
                                                      && p.GetGenericTypeDefinition() == typeof(IUnitOfWork<>)))
            .ToList();

        if (candidateInterfaces.Count == 0)
            throw new InvalidOperationException(
                $"'{implType.Name}' does not implement any user-defined IUnitOfWork<> interface. " +
                $"Use UseUnitOfWork<TInterface, TImpl>() to register it explicitly, " +
                $"or directly against IUnitOfWork<TKey>.");

        if (candidateInterfaces.Count > 1)
            throw new InvalidOperationException(
                $"'{implType.Name}' implements multiple user-defined IUnitOfWork<> interfaces: " +
                $"[{string.Join(", ", candidateInterfaces.Select(i => i.Name))}]. " +
                $"Use UseUnitOfWork<TInterface, TImpl>() to specify the interface explicitly.");

        return AddUnitOfWork(candidateInterfaces[0], implType);
    }

    /// <summary>
    /// Additive include predicate. A type is registered only if at least one include predicate returns <c>true</c>
    /// (when any includes are configured). Evaluated after excludes; exclude always wins.
    /// </summary>
    public RapidRepoOptions Include(Func<Type, bool> predicate)
    {
        _includes.Add(predicate);
        return this;
    }

    /// <summary>
    /// Additive exclude predicate. A type that matches any exclude predicate is skipped,
    /// regardless of include predicates.
    /// </summary>
    public RapidRepoOptions Exclude(Func<Type, bool> predicate)
    {
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

    private RapidRepoOptions AddUnitOfWork(Type interfaceType, Type implementationType)
    {
        if (_unitOfWorkRegistrations.Any(r => r.InterfaceType == interfaceType))
            throw new InvalidOperationException(
                $"A unit of work has already been registered for interface '{interfaceType.Name}'. " +
                "Call UseUnitOfWork once per interface type.");

        _unitOfWorkRegistrations.Add((interfaceType, implementationType));
        return this;
    }

    private static void EnsureConcrete(Type implementationType, string paramName)
    {
        if (implementationType.IsAbstract)
            throw new ArgumentException(
                $"'{implementationType.Name}' is abstract and cannot be used as a unit-of-work implementation. " +
                "Provide a concrete class.",
                paramName);
    }
}
