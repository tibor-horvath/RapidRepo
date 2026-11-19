using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Extensions.DependencyInjection;
using System.Diagnostics;

namespace RapidRepo.EntityFrameworkCore.DependencyInjection;

/// <summary>
/// The EF Core part of <c>AddRapidRepo</c>: the generic repositories, the <see cref="DbContext"/> forwarder and the
/// missing-context check.
/// </summary>
/// <param name="contextType">The context this call is bound to, or <see langword="null"/> when unbound.</param>
/// <param name="options">The provider options.</param>
internal sealed class EntityFrameworkCoreProvider(Type? contextType, EntityFrameworkCoreProviderOptions options)
    : IRapidRepoProvider
{
    public string Name => contextType is null ? "EntityFrameworkCore" : $"EntityFrameworkCore ({contextType.Name})";

    public IEnumerable<Type> AdditionalRootInterfaces => DbContextRegistrar.EfRootInterfaces;

    public void Register(IServiceCollection services, RapidRepoRegistrationContext context)
    {
        var dependentTypes = context.ActivatedTypes.ToList();

        if (context.RegisterGenericRepositories)
        {
            if (contextType is not null)
            {
                // Closed per entity and bound to the context, so several contexts can coexist.
                DbContextRegistrar.RegisterClosedGenericRepositories(
                    services, contextType, context.Lifetime, context.RegisterAsSelf);
            }
            else
            {
                var repoType = typeof(EfRepository<,>);
                foreach (var serviceType in DbContextRegistrar.RootInterfaces)
                    services.TryAdd(ServiceDescriptor.Describe(serviceType, repoType, context.Lifetime));

                // The open-generic fallback takes a base DbContext, so it needs one as much as scanned types do.
                dependentTypes.Add(repoType);
            }
        }

        if (contextType is not null)
        {
            DbContextRegistrar.RegisterForwarder(services, contextType, dependentTypes);
            return;
        }

        WarnOnMissingDbContext(services, dependentTypes);
    }

    private void WarnOnMissingDbContext(IServiceCollection services, List<Type> dependentTypes)
    {
        if (!dependentTypes.Any(DbContextRegistrar.TakesBaseDbContext)
            || services.Any(d => d.ServiceType == typeof(DbContext)))
            return;

        const string message =
            "Repositories or units of work take a 'DbContext' constructor parameter, but no DbContext service " +
            "is registered. AddDbContext<TContext>() registers TContext, not the base DbContext type, so these " +
            "types cannot be activated and will fail on first resolve. Call UseEntityFrameworkCore<TContext>() " +
            "inside AddRapidRepo to register the forwarder.";

        if (options.ThrowOnMissingDbContext)
            throw new InvalidOperationException(message);

        Trace.TraceWarning($"[RapidRepo] {message}");
    }
}
