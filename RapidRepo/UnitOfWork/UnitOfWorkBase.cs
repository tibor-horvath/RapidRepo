using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.UnitOfWork;

/// <summary>
/// The provider-neutral base of a unit of work: commit entry points, audit stamping, and the repository accessors
/// that <c>[GenerateUnitOfWork]</c> emits code against.
/// </summary>
/// <remarks>
/// Providers derive from this class, for example <c>EfUnitOfWork&lt;TUserKey&gt;</c> in
/// RapidRepo.EntityFrameworkCore. A provider maps its staged changes to <see cref="EntityChangeKind"/> values and
/// stamps each entity through <see cref="Auditor"/> before persisting.
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public abstract class UnitOfWorkBase<TUserKey> : IUnitOfWork<TUserKey>
    where TUserKey : struct
{
    private readonly IServiceProvider? _serviceProvider;

    /// <summary>
    /// Initializes a new instance of the <see cref="UnitOfWorkBase{TUserKey}"/> class.
    /// </summary>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">
    /// The service provider that <see cref="ResolveRepository{TRepo}"/> resolves custom repositories from, or
    /// <see langword="null"/> when none are needed.
    /// </param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
    protected UnitOfWorkBase(TUserKey defaultUserKey, IServiceProvider? serviceProvider = null, TimeProvider? timeProvider = null)
    {
        _serviceProvider = serviceProvider;
        Auditor = new AuditStamper<TUserKey>(defaultUserKey, timeProvider);
    }

    /// <summary>
    /// Gets the audit stamper the provider applies to each changed entity at commit time.
    /// </summary>
    protected AuditStamper<TUserKey> Auditor { get; }

    /// <inheritdoc />
    public void Commit(TUserKey? userId = null) => CommitCore(userId);

    /// <inheritdoc />
    public Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default) =>
        CommitCoreAsync(userId, cancellationToken);

    /// <summary>
    /// Gets a generic repository for <typeparamref name="TEntity"/> that shares this unit of work's changes.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
    /// <returns>The repository.</returns>
    protected IRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => CreateRepository<TEntity, TKey>();

    /// <summary>
    /// Resolves a custom repository from the service provider passed to the constructor.
    /// </summary>
    /// <typeparam name="TRepo">The repository type, usually a custom repository interface.</typeparam>
    /// <returns>The repository.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no service provider was passed to the constructor, or <typeparamref name="TRepo"/> is not registered.
    /// </exception>
    protected TRepo ResolveRepository<TRepo>() where TRepo : class
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                $"ResolveRepository<{typeof(TRepo).Name}>() requires an IServiceProvider. " +
                "Pass IServiceProvider to the unit of work constructor.");
        }

        return (TRepo?)_serviceProvider.GetService(typeof(TRepo))
            ?? throw new InvalidOperationException(
                $"No service of type '{typeof(TRepo).Name}' is registered.");
    }

    /// <summary>
    /// Creates the provider's generic repository for <typeparamref name="TEntity"/>, bound to this unit of work.
    /// </summary>
    /// <typeparam name="TEntity">The type of the entity.</typeparam>
    /// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
    /// <returns>The repository.</returns>
    protected abstract IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull;

    /// <summary>
    /// Stamps audit fields on every staged change and persists them.
    /// </summary>
    /// <param name="userId">The user making the changes, or <see langword="null"/> for the default user.</param>
    protected abstract void CommitCore(TUserKey? userId);

    /// <summary>
    /// Asynchronously stamps audit fields on every staged change and persists them.
    /// </summary>
    /// <param name="userId">The user making the changes, or <see langword="null"/> for the default user.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract Task CommitCoreAsync(TUserKey? userId, CancellationToken cancellationToken);
}
