using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Internal;
using System.Diagnostics;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>
/// Extension methods for registering RapidRepo components with <see cref="IServiceCollection"/>.
/// </summary>
public static class RapidRepoServiceCollectionExtensions
{
    /// <summary>
    /// Scans the configured assemblies and registers all discovered repository implementations
    /// and the unit of work, using convention-based rules defined in <paramref name="configure"/>.
    /// </summary>
    /// <remarks>
    /// Registrations that depend on the persistence technology, such as the generic repository implementation and
    /// its data source, come from the provider configured with <see cref="RapidRepoOptions.UseProvider"/>, for
    /// example <c>UseEntityFrameworkCore&lt;TContext&gt;()</c>.
    /// </remarks>
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">A delegate that configures the scanning options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    /// <exception cref="InvalidOperationException">
    /// <see cref="RapidRepoOptions.RegisterGenericRepositories"/> is set without a provider, or the configuration is
    /// otherwise invalid.
    /// </exception>
    public static IServiceCollection AddRapidRepo(
        this IServiceCollection services,
        Action<RapidRepoOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RapidRepoOptions();
        configure(options);

        var provider = options.Provider;

        if (options.RegisterGenericRepositories && provider is null)
            throw new InvalidOperationException(
                "RegisterGenericRepositories requires a provider to supply the generic repository implementation. " +
                "Configure one in AddRapidRepo, for example UseEntityFrameworkCore<TContext>() from " +
                "RapidRepo.EntityFrameworkCore.");

        HandleSingletonWarning(options);

        var candidates = RepositoryScanner
            .GetCandidates(options.Assemblies, provider?.AdditionalRootInterfaces ?? [])
            .Where(c => PassesFilters(c.ConcreteType, options))
            .ToList();

        var descriptors = RepositoryDescriptorBuilder.Build(
            candidates,
            options.Lifetime,
            options.RegisterAsSelf,
            options.ThrowOnAmbiguousRegistration);

        foreach (var descriptor in descriptors)
        {
            // TryAddEnumerable requires ServiceType != ImplementationType.
            // Self-registrations (RegisterAsSelf) have ServiceType == ImplementationType — use TryAdd.
            if (descriptor.ServiceType == descriptor.ImplementationType)
                services.TryAdd(descriptor);
            else
                services.TryAddEnumerable(descriptor);
        }

        foreach (var (iface, impl) in options.UnitOfWorkRegistrations)
            UnitOfWorkRegistrar.Register(services, iface, impl, options.Lifetime);

        provider?.Register(services, new RapidRepoRegistrationContext(
            candidates.Select(c => new RepositoryRegistration(c.ConcreteType, c.RegistrationInterfaces)).ToList(),
            options.UnitOfWorkRegistrations.Select(u => new UnitOfWorkRegistration(u.InterfaceType, u.ImplementationType)).ToList(),
            options.Lifetime,
            options.RegisterAsSelf,
            options.RegisterGenericRepositories));

        return services;
    }

    private static bool PassesFilters(Type type, RapidRepoOptions options)
    {
        if (options.Excludes.Any(exclude => exclude(type)))
            return false;

        if (options.Includes.Count > 0 && !options.Includes.Any(include => include(type)))
            return false;

        return true;
    }

    private static void HandleSingletonWarning(RapidRepoOptions options)
    {
        if (options.Lifetime != ServiceLifetime.Singleton)
            return;

        const string message =
            "RapidRepo repositories are being registered as Singleton. " +
            "Repositories depend on scoped data access resources, such as an EF Core DbContext or a database " +
            "connection. A Singleton repository captures one of them for the application lifetime, " +
            "causing thread-safety issues and stale data. Use ServiceLifetime.Scoped instead.";

        if (options.ThrowOnSingletonMisuse)
            throw new InvalidOperationException(message);

        Trace.TraceWarning($"[RapidRepo] {message}");
    }
}
