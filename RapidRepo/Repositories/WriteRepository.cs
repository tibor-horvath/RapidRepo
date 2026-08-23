using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;
using RapidRepo.Repositories.Interfaces;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace RapidRepo.Repositories;

public abstract class WriteRepository<TEntity, TId>(DbContext dbContext) : IWriteRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// The name of the property declared by <see cref="IDeletableEntity{TUserKey}"/>.
    /// </summary>
    private const string DeletedByPropertyName = nameof(IDeletableEntity<Guid>.DeletedBy);

    /// <summary>
    /// Whether <typeparamref name="TEntity"/> tracks who deleted it. Resolved once per closed entity type.
    /// </summary>
    private static readonly bool TracksDeletedBy = Array.Exists(
        typeof(TEntity).GetInterfaces(),
        i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IDeletableEntity<>));

    protected readonly DbContext DbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    protected readonly bool SupportsSoftDelete =
            typeof(IDeletableEntity).IsAssignableFrom(typeof(TEntity));

    public virtual void Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbContext.Set<TEntity>().Add(entity);
    }

    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        DbContext.Set<TEntity>().AddRange(entities);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        await DbContext.Set<TEntity>().AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Delete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (!SupportsSoftDelete)
        {
            HardDelete(entity);
            return;
        }

        MarkAsDeleted(entity);
    }

    public virtual void DeleteRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);

        if (!SupportsSoftDelete)
        {
            HardDeleteRange(entities);
            return;
        }

        // Materialised before mutating: the source may be a live query over the very set being changed.
        foreach (var entity in entities.ToList())
        {
            MarkAsDeleted(entity);
        }
    }

    public virtual void Restore(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureSupportsSoftDelete();

        ClearDeletionMarks(entity);
    }

    public virtual void RestoreRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureSupportsSoftDelete();

        foreach (var entity in entities.ToList())
        {
            ClearDeletionMarks(entity);
        }
    }

    public virtual void RestoreById(TId id, bool ignoreQueryFilters = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        EnsureSupportsSoftDelete();

        ApplyById(id, ignoreQueryFilters, Restore);
    }

    public virtual Task RestoreByIdAsync(
        TId id,
        bool ignoreQueryFilters = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        EnsureSupportsSoftDelete();

        return ApplyByIdAsync(id, ignoreQueryFilters, Restore, cancellationToken);
    }

    public virtual void HardDelete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbContext.Remove(entity);
    }

    public virtual void HardDeleteRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        DbContext.RemoveRange(entities.ToList());
    }

    public virtual void HardDeleteById(TId id, bool ignoreQueryFilters = false)
    {
        ArgumentNullException.ThrowIfNull(id);

        ApplyById(id, ignoreQueryFilters, HardDelete);
    }

    public virtual Task HardDeleteByIdAsync(
        TId id,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return ApplyByIdAsync(id, ignoreQueryFilters, HardDelete, cancellationToken);
    }

    public virtual void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        DbContext.Set<TEntity>().Update(entity);
    }

    public virtual void DeleteById(TId id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = DbContext.Set<TEntity>().Find(id);
        if (entity != null)
        {
            Delete(entity);
        }
    }

    public virtual async Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await DbContext.Set<TEntity>().FindAsync([id], cancellationToken);
        if (entity != null)
        {
            Delete(entity);
        }
    }

    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        DbContext.Set<TEntity>().UpdateRange(entities);
    }

    /// <summary>
    /// Marks the entity as deleted, tracking it first when it is not already tracked as an update.
    /// </summary>
    /// <param name="entity">The entity to mark as deleted.</param>
    private void MarkAsDeleted(TEntity entity)
    {
        Debug.Assert(SupportsSoftDelete, $"'{typeof(TEntity).Name}' does not implement '{nameof(IDeletableEntity)}'.");

        TrackForUpdate(entity);
        ((IDeletableEntity)entity).DeletedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Clears the deletion marks from the entity, tracking it first when it is not already tracked as an update.
    /// </summary>
    /// <remarks>
    /// <c>DeletedBy</c> is declared on the closed <see cref="IDeletableEntity{TUserKey}"/>, whose user key type
    /// is not a type parameter of this repository, so it cannot be reached through a cast the way
    /// <see cref="IDeletableEntity.DeletedAt"/> can. EF already models the property, so it is cleared through
    /// the change tracker rather than through reflection.
    /// </remarks>
    /// <param name="entity">The entity to restore.</param>
    private void ClearDeletionMarks(TEntity entity)
    {
        Debug.Assert(SupportsSoftDelete, $"'{typeof(TEntity).Name}' does not implement '{nameof(IDeletableEntity)}'.");

        var entry = TrackForUpdate(entity);

        ((IDeletableEntity)entity).DeletedAt = null;

        if (TracksDeletedBy && entry.Metadata.FindProperty(DeletedByPropertyName) != null)
        {
            entry.Property(DeletedByPropertyName).CurrentValue = null;
        }
    }

    /// <summary>
    /// Tracks the entity so that subsequent property changes are persisted as an update.
    /// </summary>
    /// <remarks>
    /// An entity staged for removal by <see cref="HardDelete(TEntity)"/> is revived, so that a soft delete
    /// or a restore applied afterwards is not silently discarded when the row is dropped.
    /// </remarks>
    /// <param name="entity">The entity to track.</param>
    /// <returns>The change tracker entry for the entity.</returns>
    private EntityEntry<TEntity> TrackForUpdate(TEntity entity)
    {
        var entry = DbContext.Entry(entity);

        entry.State = entry.State switch
        {
            EntityState.Detached => EntityState.Unchanged,
            EntityState.Deleted => EntityState.Modified,
            _ => entry.State,
        };

        return entry;
    }

    /// <summary>
    /// Applies an operation to the entity with the given identifier, if there is one.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    /// <param name="operation">The operation to apply.</param>
    private void ApplyById(TId id, bool ignoreQueryFilters, Action<TEntity> operation)
    {
        var entity = FindById(id, ignoreQueryFilters);
        if (entity != null)
        {
            operation(entity);
        }
    }

    /// <summary>
    /// Asynchronously applies an operation to the entity with the given identifier, if there is one.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    /// <param name="operation">The operation to apply.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    private async Task ApplyByIdAsync(
        TId id,
        bool ignoreQueryFilters,
        Action<TEntity> operation,
        CancellationToken cancellationToken)
    {
        var entity = await FindByIdAsync(id, ignoreQueryFilters, cancellationToken);
        if (entity != null)
        {
            operation(entity);
        }
    }

    /// <summary>
    /// Finds an entity by its identifier, including one staged for insertion but not yet committed.
    /// </summary>
    /// <remarks>
    /// A pending addition exists only in the change tracker, so it is looked up there; anything already in the
    /// store is resolved by the query, which is what applies <paramref name="ignoreQueryFilters"/>. EF returns
    /// the tracked instance for a row it already tracks, so a tracked entity is still found — but only when the
    /// filters admit it. Consulting the tracker for those as well would let a soft-deleted entity be reached
    /// through <see cref="HardDeleteById(TId, bool)"/> merely because something had loaded it earlier.
    /// </remarks>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when querying the store.</param>
    /// <returns>The entity, or <see langword="null"/> when no entity has that identifier.</returns>
    private TEntity? FindById(TId id, bool ignoreQueryFilters) =>
        FindPendingAddition(id) ?? QueryById(id, ignoreQueryFilters).FirstOrDefault();

    /// <summary>
    /// Asynchronously finds an entity by its identifier, including one staged for insertion but not yet committed.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when querying the store.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity, or <see langword="null"/> when no entity has that identifier.</returns>
    private async Task<TEntity?> FindByIdAsync(TId id, bool ignoreQueryFilters, CancellationToken cancellationToken) =>
        FindPendingAddition(id) ?? await QueryById(id, ignoreQueryFilters).FirstOrDefaultAsync(cancellationToken);

    /// <summary>
    /// Finds an entity staged for insertion but not yet committed, which no store query can reach.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <returns>The entity, or <see langword="null"/> when no pending addition has that identifier.</returns>
    private TEntity? FindPendingAddition(TId id) =>
        DbContext.ChangeTracker
            .Entries<TEntity>()
            .Where(e => e.State == EntityState.Added)
            .Select(e => e.Entity)
            .FirstOrDefault(e => id.Equals(e.Id));

    /// <summary>
    /// Builds a store query for a single entity.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters.</param>
    /// <returns>The query.</returns>
    private IQueryable<TEntity> QueryById(TId id, bool ignoreQueryFilters) =>
        DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: e => id.Equals(e.Id),
                ignoreQueryFilters: ignoreQueryFilters);

    /// <summary>
    /// Guards operations that are only meaningful for entities participating in soft delete.
    /// </summary>
    /// <param name="operation">The calling member, supplied by the compiler.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="IDeletableEntity"/>.
    /// </exception>
    private void EnsureSupportsSoftDelete([CallerMemberName] string operation = "")
    {
        if (SupportsSoftDelete)
        {
            return;
        }

        throw new InvalidOperationException(
            $"'{operation}' requires '{typeof(TEntity).Name}' to implement '{nameof(IDeletableEntity)}'. " +
            $"Entities without soft delete are removed permanently by '{nameof(Delete)}' and cannot be restored.");
    }
}
