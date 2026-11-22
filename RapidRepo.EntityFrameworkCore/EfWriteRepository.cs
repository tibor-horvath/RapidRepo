using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// The EF Core write side of a repository: stages changes in the <see cref="DbContext"/> change tracker. The write
/// rules themselves (soft delete, restore, guards) live in <see cref="WriteRepositoryBase{TEntity, TKey}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <param name="dbContext">The <see cref="DbContext"/> used for data access operations.</param>
public abstract class EfWriteRepository<TEntity, TId>(DbContext dbContext) : WriteRepositoryBase<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    private static readonly ReadOptions IncludingDeleted = new() { IncludeDeleted = true };

    /// <summary>
    /// Gets the <see cref="DbContext"/> the repository reads from and stages changes in.
    /// </summary>
    protected DbContext DbContext { get; } = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    protected override void StageInsert(TEntity entity) => DbContext.Set<TEntity>().Add(entity);

    /// <inheritdoc />
    protected override async ValueTask StageInsertAsync(TEntity entity, CancellationToken cancellationToken) =>
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    /// <inheritdoc />
    protected override void StageInsertRange(IReadOnlyList<TEntity> entities) => DbContext.Set<TEntity>().AddRange(entities);

    /// <inheritdoc />
    protected override async ValueTask StageInsertRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken) =>
        await DbContext.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    /// <inheritdoc />
    protected override void StageUpdate(TEntity entity) => DbContext.Set<TEntity>().Update(entity);

    /// <inheritdoc />
    protected override void StageUpdateRange(IReadOnlyList<TEntity> entities) => DbContext.Set<TEntity>().UpdateRange(entities);

    /// <inheritdoc />
    protected override void StageRemove(TEntity entity) => DbContext.Remove(entity);

    /// <inheritdoc />
    protected override void StageRemoveRange(IReadOnlyList<TEntity> entities) => DbContext.RemoveRange(entities);

    /// <summary>
    /// Tracks the entity before the deletion marks change, so that the change tracker records the transition and
    /// the unit of work can tell a soft delete or restore apart from an ordinary edit.
    /// </summary>
    /// <remarks>
    /// An entity staged for removal by <c>HardDelete</c> is revived, so that a soft delete or a restore applied
    /// afterwards is not silently discarded when the row is dropped.
    /// </remarks>
    protected override void StageDeletionChange(TEntity entity, EntityChangeKind kind, Action applyChange)
    {
        var entry = DbContext.Entry(entity);

        entry.State = entry.State switch
        {
            EntityState.Detached => EntityState.Unchanged,
            EntityState.Deleted => EntityState.Modified,
            _ => entry.State,
        };

        applyChange();
    }

    /// <remarks>
    /// A pending addition exists only in the change tracker, so it is looked up there; anything already in the
    /// store is resolved by the query, which is what applies <paramref name="includeDeleted"/>. EF returns the
    /// tracked instance for a row it already tracks, so a tracked entity is still found — but only when the
    /// filters admit it. Consulting the tracker for those as well would let a soft-deleted entity be reached
    /// through <c>HardDeleteById</c> merely because something had loaded it earlier.
    /// </remarks>
    protected override TEntity? FindForWrite(TId id, bool includeDeleted) =>
        FindPendingAddition(id) ?? QueryById(id, includeDeleted).FirstOrDefault();

    /// <inheritdoc />
    protected override async ValueTask<TEntity?> FindForWriteAsync(TId id, bool includeDeleted, CancellationToken cancellationToken) =>
        FindPendingAddition(id) ?? await QueryById(id, includeDeleted).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Finds an entity staged for insertion but not yet committed, which no store query can reach.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only additions whose key is already final are matched. A store-generated key is unset or temporary until
    /// the commit, so matching on it would let <c>DeleteById(default)</c> discard an arbitrary pending insert.
    /// </para>
    /// <para>
    /// The tracker is read without detecting changes, as <c>DbSet.Find</c> does. Otherwise every call would scan
    /// every tracked entity of every type, which makes a loop of <c>DeleteById</c> calls quadratic. An entity
    /// becomes <see cref="EntityState.Added"/> through <c>Add</c>, which tracks it at once; only one reached
    /// solely through a navigation of a tracked entity, and not yet seen by the tracker, is missed.
    /// </para>
    /// </remarks>
    private TEntity? FindPendingAddition(TId id)
    {
        var changeTracker = DbContext.ChangeTracker;
        var autoDetectChanges = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;

        try
        {
            foreach (var entry in changeTracker.Entries<TEntity>())
            {
                // The cheap key comparison first: HasFinalKey reads key metadata and property state.
                if (entry.State == EntityState.Added && id.Equals(entry.Entity.Id) && HasFinalKey(entry))
                    return entry.Entity;
            }

            return null;
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = autoDetectChanges;
        }
    }

    private static bool HasFinalKey(EntityEntry<TEntity> entry) =>
        entry.IsKeySet && !entry.Metadata.FindPrimaryKey()!.Properties.Any(p => entry.Property(p.Name).IsTemporary);

    private IQueryable<TEntity> QueryById(TId id, bool includeDeleted) =>
        DbContext
            .Set<TEntity>()
            .ApplyRead(
                condition: e => id.Equals(e.Id),
                options: includeDeleted ? IncludingDeleted : ReadOptions.Default,
                context: DbContext);
}
