using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// An EF Core unit of work: stamps audit fields on the tracked changes and saves the <see cref="DbContext"/>.
/// </summary>
/// <remarks>
/// The unit of work does not own the context it is given and never disposes it; whoever created the context,
/// usually the dependency injection container, does.
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key recorded in audit fields.</typeparam>
public abstract class EfUnitOfWork<TUserKey> : UnitOfWorkBase<TUserKey>
    where TUserKey : struct
{
    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
        : this(dbContext, defaultUserKey, serviceProvider: null, timeProvider: null)
    {
    }

    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey, IServiceProvider serviceProvider)
        : this(dbContext, defaultUserKey, serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider)), timeProvider: null)
    {
    }

    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when a commit names none.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from, if any.</param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for the system clock.</param>
    protected EfUnitOfWork(
        DbContext dbContext,
        TUserKey defaultUserKey,
        IServiceProvider? serviceProvider,
        TimeProvider? timeProvider)
        : this(dbContext, defaultUserKey, serviceProvider, timeProvider, currentUserProvider: null)
    {
    }

    /// <param name="dbContext">The context whose changes this unit of work commits.</param>
    /// <param name="defaultUserKey">The user recorded in audit fields when neither the commit nor <paramref name="currentUserProvider"/> names one.</param>
    /// <param name="serviceProvider">The service provider custom repositories are resolved from, if any.</param>
    /// <param name="timeProvider">The clock for audit timestamps, or <see langword="null"/> for the system clock.</param>
    /// <param name="currentUserProvider">
    /// Supplies the audit user when a commit names none, or <see langword="null"/> to resolve one from
    /// <paramref name="serviceProvider"/>, if any.
    /// </param>
    protected EfUnitOfWork(
        DbContext dbContext,
        TUserKey defaultUserKey,
        IServiceProvider? serviceProvider,
        TimeProvider? timeProvider,
        ICurrentUserProvider<TUserKey>? currentUserProvider)
        : base(defaultUserKey, serviceProvider, timeProvider, currentUserProvider)
    {
        DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    protected DbContext DbContext { get; }

    protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() =>
        new EfRepository<TEntity, TKey>(DbContext);

    protected override void CommitCore(TUserKey? userId)
    {
        StampChanges(userId);
        DbContext.SaveChanges();
    }

    protected override async Task CommitCoreAsync(TUserKey? userId, CancellationToken cancellationToken)
    {
        StampChanges(userId);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Stamps every added or modified entry with one timestamp for the whole commit.
    /// </summary>
    private void StampChanges(TUserKey? userId)
    {
        var utcNow = Auditor.UtcNow;

        foreach (var entry in DbContext.ChangeTracker.Entries())
        {
            var kind = Classify(entry);
            if (kind is { } changeKind)
            {
                Auditor.Stamp(entry.Entity, changeKind, userId, utcNow);
            }
        }
    }

    /// <summary>
    /// Maps a change tracker entry to the kind of change it represents, or <see langword="null"/> when it is not
    /// stamped (unchanged, detached or hard-deleted).
    /// </summary>
    /// <remarks>
    /// A soft delete or restore is recognised by <see cref="EntityChangeClassifier.ClassifyUpdate"/>, from the
    /// original <c>DeletedAt</c>. Only the transition counts, so saving an already deleted entity again does not
    /// overwrite who deleted it and when.
    /// </remarks>
    private static EntityChangeKind? Classify(EntityEntry entry)
    {
        switch (entry.State)
        {
            case EntityState.Added:
                return EntityChangeKind.Added;

            case EntityState.Modified when entry.Entity is IDeletableEntity
                && entry.Metadata.FindProperty(nameof(IDeletableEntity.DeletedAt)) is not null:
                var originalDeletedAt = (DateTime?)entry.Property(nameof(IDeletableEntity.DeletedAt)).OriginalValue;

                return EntityChangeClassifier.ClassifyUpdate(entry.Entity, originalDeletedAt);

            case EntityState.Modified:
                return EntityChangeKind.Modified;

            default:
                return null;
        }
    }
}
