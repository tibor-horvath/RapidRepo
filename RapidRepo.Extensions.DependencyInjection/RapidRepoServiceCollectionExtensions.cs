using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.Extensions.DependencyInjection;
using RapidRepo.Extensions.DependencyInjection.Internal;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
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
    /// <param name="services">The service collection to register into.</param>
    /// <param name="configure">A delegate that configures the scanning options.</param>
    /// <returns>The same <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddRapidRepo(
        this IServiceCollection services,
        Action<RapidRepoOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new RapidRepoOptions();
        configure(options);

        HandleSingletonWarning(options);

        var candidates = RepositoryScanner
            .GetCandidates(options.Assemblies)
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

        if (options.RegisterGenericRepositories)
        {
            var repoType = typeof(Repository<,>);
            services.TryAdd(ServiceDescriptor.Describe(typeof(IRepository<,>),         repoType, options.Lifetime));
            services.TryAdd(ServiceDescriptor.Describe(typeof(IReadOnlyRepository<,>), repoType, options.Lifetime));
            services.TryAdd(ServiceDescriptor.Describe(typeof(IWriteRepository<,>),    repoType, options.Lifetime));
        }

        foreach (var (iface, impl) in options.UnitOfWorkRegistrations)
            UnitOfWorkRegistrar.Register(services, iface, impl, options.Lifetime);

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
            "Repositories depend on DbContext, which is Scoped by default. " +
            "A Singleton repository will capture and hold a single DbContext for the application lifetime, " +
            "causing thread-safety issues and stale data. Use ServiceLifetime.Scoped instead.";

        if (options.ThrowOnSingletonMisuse)
            throw new InvalidOperationException(message);

        Trace.TraceWarning($"[RapidRepo] {message}");
    }
}
