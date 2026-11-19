using Microsoft.EntityFrameworkCore;
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

    protected readonly DbContext DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    protected override void StageInsert(TEntity entity) => DbContext.Set<TEntity>().Add(entity);

    protected override async ValueTask StageInsertAsync(TEntity entity, CancellationToken cancellationToken) =>
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);

    protected override void StageInsertRange(IReadOnlyList<TEntity> entities) => DbContext.Set<TEntity>().AddRange(entities);

    protected override async ValueTask StageInsertRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken) =>
        await DbContext.Set<TEntity>().AddRangeAsync(entities, cancellationToken);

    protected override void StageUpdate(TEntity entity) => DbContext.Set<TEntity>().Update(entity);

    protected override void StageUpdateRange(IReadOnlyList<TEntity> entities) => DbContext.Set<TEntity>().UpdateRange(entities);

    protected override void StageRemove(TEntity entity) => DbContext.Remove(entity);

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

    protected override async ValueTask<TEntity?> FindForWriteAsync(TId id, bool includeDeleted, CancellationToken cancellationToken) =>
        FindPendingAddition(id) ?? await QueryById(id, includeDeleted).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Finds an entity staged for insertion but not yet committed, which no store query can reach.
    /// </summary>
    private TEntity? FindPendingAddition(TId id) =>
        DbContext.ChangeTracker
            .Entries<TEntity>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .FirstOrDefault(e => id.Equals(e.Id));

    private IQueryable<TEntity> QueryById(TId id, bool includeDeleted) =>
        DbContext
            .Set<TEntity>()
            .ApplyRead(
                condition: e => id.Equals(e.Id),
                options: includeDeleted ? IncludingDeleted : ReadOptions.Default);
}
