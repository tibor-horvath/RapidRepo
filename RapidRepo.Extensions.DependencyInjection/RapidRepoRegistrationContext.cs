using Microsoft.Extensions.DependencyInjection;

namespace RapidRepo.Extensions.DependencyInjection;

/// <summary>
/// What one <c>AddRapidRepo</c> call registered and how it was configured, handed to
/// <see cref="IRapidRepoProvider.Register"/>.
/// </summary>
public sealed class RapidRepoRegistrationContext
{
    internal RapidRepoRegistrationContext(
        IReadOnlyList<Type> rootInterfaces,
        IReadOnlyList<RepositoryRegistration> repositories,
        IReadOnlyList<UnitOfWorkRegistration> unitsOfWork,
        ServiceLifetime lifetime,
        bool registerAsSelf,
        bool registerGenericRepositories)
    {
        RootInterfaces = rootInterfaces;
        Repositories = repositories;
        UnitsOfWork = unitsOfWork;
        Lifetime = lifetime;
        RegisterAsSelf = registerAsSelf;
        RegisterGenericRepositories = registerGenericRepositories;
    }

    /// <summary>
    /// Gets the open generic repository interfaces: the core's (<c>IRepository&lt;,&gt;</c>,
    /// <c>IReadOnlyRepository&lt;,&gt;</c>, <c>IWriteRepository&lt;,&gt;</c>) followed by the provider's
    /// <see cref="IRapidRepoProvider.AdditionalRootInterfaces"/>. Register the generic repository against these.
    /// </summary>
    public IReadOnlyList<Type> RootInterfaces { get; }

    /// <summary>
    /// Gets the scanned repositories that passed the filters, with the interfaces each was registered against.
    /// </summary>
    public IReadOnlyList<RepositoryRegistration> Repositories { get; }

    /// <summary>
    /// Gets the units of work registered by <see cref="RapidRepoOptions.UseUnitOfWork{TImpl}"/>.
    /// </summary>
    public IReadOnlyList<UnitOfWorkRegistration> UnitsOfWork { get; }

    /// <summary>
    /// Gets the configured <see cref="RapidRepoOptions.Lifetime"/>.
    /// </summary>
    public ServiceLifetime Lifetime { get; }

    /// <summary>
    /// Gets the configured <see cref="RapidRepoOptions.RegisterAsSelf"/>.
    /// </summary>
    public bool RegisterAsSelf { get; }

    /// <summary>
    /// Gets the configured <see cref="RapidRepoOptions.RegisterGenericRepositories"/>. When set, the provider
    /// registers its generic repository implementation for the root repository interfaces.
    /// </summary>
    public bool RegisterGenericRepositories { get; }

    /// <summary>
    /// Gets every concrete type this call registered whose constructor the container has to satisfy: the scanned
    /// repositories and the unit of work implementations.
    /// </summary>
    public IEnumerable<Type> ActivatedTypes =>
        Repositories.Select(r => r.ImplementationType).Concat(UnitsOfWork.Select(u => u.ImplementationType));
}

/// <summary>
/// A scanned repository and the interfaces it was registered against.
/// </summary>
/// <param name="ImplementationType">The concrete repository type.</param>
/// <param name="ServiceTypes">The user-defined repository interfaces it was registered against.</param>
public sealed record RepositoryRegistration(Type ImplementationType, IReadOnlyList<Type> ServiceTypes);

/// <summary>
/// A unit of work registration.
/// </summary>
/// <param name="ServiceType">The unit of work interface.</param>
/// <param name="ImplementationType">The unit of work implementation.</param>
public sealed record UnitOfWorkRegistration(Type ServiceType, Type ImplementationType);
