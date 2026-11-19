using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// What a provider supplies to run the conformance suite: an empty data store holding the conformance entities
/// (<see cref="Widget"/> and <see cref="Note"/>), and sessions over it.
/// </summary>
/// <remarks>
/// A new store is created for every test, so implementations need a parameterless constructor and must start empty.
/// Soft-deleted <see cref="Note"/>s must be excluded from reads by default, the way the provider normally sets that
/// up (for EF Core, <c>AddSoftDeleteQueryFilters()</c>).
/// </remarks>
public interface IConformanceStore : IDisposable
{
    /// <summary>
    /// Opens a session: a unit of work and repositories sharing its staged changes. Sessions over the same store
    /// see each other's committed changes and nothing else, like separate requests in an application.
    /// </summary>
    /// <param name="clock">The clock the unit of work stamps audit fields with.</param>
    /// <param name="currentUserProvider">
    /// The unit of work's current-user provider, or <see langword="null"/> for none. Pass it to the
    /// <c>UnitOfWorkBase&lt;TUserKey&gt;</c> constructor, which owns the resolution order.
    /// </param>
    /// <returns>The session.</returns>
    IConformanceSession OpenSession(TimeProvider clock, ICurrentUserProvider<Guid>? currentUserProvider = null);
}

/// <summary>
/// One unit of work and the repositories that stage changes in it.
/// </summary>
public interface IConformanceSession : IDisposable
{
    /// <summary>
    /// Gets the unit of work. Its default user must be <see cref="ConformanceDefaults.DefaultUser"/>.
    /// </summary>
    IUnitOfWork<Guid> UnitOfWork { get; }

    /// <summary>
    /// Gets a repository whose writes are staged in <see cref="UnitOfWork"/>.
    /// </summary>
    /// <typeparam name="TEntity">A conformance entity type.</typeparam>
    /// <typeparam name="TKey">Its key type.</typeparam>
    /// <returns>The repository.</returns>
    IRepository<TEntity, TKey> Repository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull;
}
