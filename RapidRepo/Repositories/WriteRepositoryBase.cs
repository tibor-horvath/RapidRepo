using RapidRepo.Auditing;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using System.Runtime.CompilerServices;

namespace RapidRepo.Repositories;

/// <summary>
/// The provider-neutral write side of a repository. It owns the write rules (soft versus hard delete, restore,
/// argument guards and error messages) and leaves staging the changes to the provider.
/// </summary>
/// <remarks>
/// <para>The contract every provider follows:</para>
/// <list type="number">
/// <item>Writes are staged. Nothing is persisted until the unit of work commits.</item>
/// <item>
/// <c>Delete</c> soft-deletes an <see cref="IDeletableEntity"/> and hard-deletes anything else. <c>Restore</c>
/// throws for an entity that is not an <see cref="IDeletableEntity"/>.
/// </item>
/// <item>Audit fields are stamped at commit time (see <see cref="AuditStamper{TUserKey}"/>).</item>
/// <item>Generated keys are populated on the entity after the commit.</item>
/// <item>
/// Changing a loaded entity without calling <c>Update</c> is not portable: change-tracking providers persist it,
/// others do not.
/// </item>
/// </list>
/// </remarks>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
public abstract class WriteRepositoryBase<TEntity, TKey> : IWriteRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Gets a value indicating whether <typeparamref name="TEntity"/> participates in soft delete.
    /// </summary>
    protected static bool SupportsSoftDelete => EntityTraits<TEntity>.SupportsSoftDelete;

    /// <inheritdoc />
    public virtual void Add(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        StageInsert(entity);
    }

    /// <inheritdoc />
    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        StageInsertRange(entities.ToList());
    }

    /// <inheritdoc />
    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entity);
        await StageInsertAsync(entity, cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entities);
        await StageInsertRangeAsync(entities.ToList(), cancellationToken);
    }

    /// <inheritdoc />
    public virtual void Update(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        StageUpdate(entity);
    }

    /// <inheritdoc />
    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        StageUpdateRange(entities.ToList());
    }

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
    public virtual void DeleteById(TKey id)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = FindForWrite(id, includeDeleted: false);
        if (entity != null)
        {
            Delete(entity);
        }
    }

    /// <inheritdoc />
    public virtual async Task DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await FindForWriteAsync(id, includeDeleted: false, cancellationToken);
        if (entity != null)
        {
            Delete(entity);
        }
    }

    /// <inheritdoc />
    public virtual void Restore(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        EnsureSupportsSoftDelete();

        ClearDeletionMarks(entity);
    }

    /// <inheritdoc />
    public virtual void RestoreRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureSupportsSoftDelete();

        foreach (var entity in entities.ToList())
        {
            ClearDeletionMarks(entity);
        }
    }

    /// <inheritdoc />
    public virtual void RestoreById(TKey id, bool includeDeleted = true)
    {
        ArgumentNullException.ThrowIfNull(id);
        EnsureSupportsSoftDelete();

        var entity = FindForWrite(id, includeDeleted);
        if (entity != null)
        {
            Restore(entity);
        }
    }

    /// <inheritdoc />
    public virtual async Task RestoreByIdAsync(
        TKey id,
        bool includeDeleted = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        EnsureSupportsSoftDelete();

        var entity = await FindForWriteAsync(id, includeDeleted, cancellationToken);
        if (entity != null)
        {
            Restore(entity);
        }
    }

    /// <inheritdoc />
    public virtual void HardDelete(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        StageRemove(entity);
    }

    /// <inheritdoc />
    public virtual void HardDeleteRange(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        StageRemoveRange(entities.ToList());
    }

    /// <inheritdoc />
    public virtual void HardDeleteById(TKey id, bool includeDeleted = false)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = FindForWrite(id, includeDeleted);
        if (entity != null)
        {
            HardDelete(entity);
        }
    }

    /// <inheritdoc />
    public virtual async Task HardDeleteByIdAsync(
        TKey id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        var entity = await FindForWriteAsync(id, includeDeleted, cancellationToken);
        if (entity != null)
        {
            HardDelete(entity);
        }
    }

    /// <summary>Stages <paramref name="entity"/> for insertion.</summary>
    /// <param name="entity">The entity.</param>
    protected abstract void StageInsert(TEntity entity);

    /// <summary>Asynchronously stages <paramref name="entity"/> for insertion.</summary>
    /// <param name="entity">The entity.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected abstract ValueTask StageInsertAsync(TEntity entity, CancellationToken cancellationToken);

    /// <summary>Stages <paramref name="entity"/> for an update.</summary>
    /// <param name="entity">The entity.</param>
    protected abstract void StageUpdate(TEntity entity);

    /// <summary>Stages <paramref name="entity"/> for permanent removal.</summary>
    /// <param name="entity">The entity.</param>
    protected abstract void StageRemove(TEntity entity);

    /// <summary>
    /// Stages a soft delete or a restore. The provider must call <paramref name="applyChange"/>, which sets or
    /// clears the deletion marks, at the point where it can observe the change.
    /// </summary>
    /// <remarks>
    /// A change-tracking provider starts tracking the entity first, so that the tracker sees the transition. A
    /// command-based provider applies the change and queues an update of <paramref name="kind"/>.
    /// </remarks>
    /// <param name="entity">The entity.</param>
    /// <param name="kind"><see cref="EntityChangeKind.SoftDeleted"/> or <see cref="EntityChangeKind.Restored"/>.</param>
    /// <param name="applyChange">Sets or clears the deletion marks on the entity.</param>
    protected abstract void StageDeletionChange(TEntity entity, EntityChangeKind kind, Action applyChange);

    /// <summary>
    /// Finds an entity to write to, including one staged for insertion but not yet committed.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="includeDeleted">Whether soft-deleted entities are found too.</param>
    /// <returns>The entity, or <see langword="null"/> when no entity has that identifier.</returns>
    protected abstract TEntity? FindForWrite(TKey id, bool includeDeleted);

    /// <summary>
    /// Asynchronously finds an entity to write to, including one staged for insertion but not yet committed.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="includeDeleted">Whether soft-deleted entities are found too.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity, or <see langword="null"/> when no entity has that identifier.</returns>
    protected abstract ValueTask<TEntity?> FindForWriteAsync(TKey id, bool includeDeleted, CancellationToken cancellationToken);

    /// <summary>Stages several entities for insertion. Override to batch.</summary>
    /// <param name="entities">The entities, already materialised.</param>
    protected virtual void StageInsertRange(IReadOnlyList<TEntity> entities)
    {
        foreach (var entity in entities)
            StageInsert(entity);
    }

    /// <summary>Asynchronously stages several entities for insertion. Override to batch.</summary>
    /// <param name="entities">The entities, already materialised.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    protected virtual async ValueTask StageInsertRangeAsync(IReadOnlyList<TEntity> entities, CancellationToken cancellationToken)
    {
        foreach (var entity in entities)
            await StageInsertAsync(entity, cancellationToken);
    }

    /// <summary>Stages several entities for an update. Override to batch.</summary>
    /// <param name="entities">The entities, already materialised.</param>
    protected virtual void StageUpdateRange(IReadOnlyList<TEntity> entities)
    {
        foreach (var entity in entities)
            StageUpdate(entity);
    }

    /// <summary>Stages several entities for permanent removal. Override to batch.</summary>
    /// <param name="entities">The entities, already materialised.</param>
    protected virtual void StageRemoveRange(IReadOnlyList<TEntity> entities)
    {
        foreach (var entity in entities)
            StageRemove(entity);
    }

    /// <summary>
    /// Gets the clock for the provisional <c>DeletedAt</c> that <c>Delete</c> sets before the commit. Defaults to
    /// <see cref="TimeProvider.System"/>; the unit of work stamps the definitive time with its own clock at commit.
    /// </summary>
    protected virtual TimeProvider TimeProvider => TimeProvider.System;

    /// <summary>
    /// Marks the entity as deleted. <c>DeletedAt</c> gets a provisional value here; the unit of work stamps the
    /// definitive time and user at commit.
    /// </summary>
    /// <remarks>
    /// An entity that is already deleted keeps its marks: only the transition counts, so deleting it again must
    /// not move <c>DeletedAt</c> away from the <c>DeletedBy</c> it belongs with. It is still staged, so that a
    /// provider can, for example, revive an entity staged for removal.
    /// </remarks>
    private void MarkAsDeleted(TEntity entity) =>
        StageDeletionChange(entity, EntityChangeKind.SoftDeleted, () =>
        {
            var deletable = (IDeletableEntity)entity;
            deletable.DeletedAt ??= TimeProvider.GetUtcNow().UtcDateTime;
        });

    /// <summary>
    /// Clears <c>DeletedAt</c> and, when the entity records it, <c>DeletedBy</c>.
    /// </summary>
    private void ClearDeletionMarks(TEntity entity) =>
        StageDeletionChange(entity, EntityChangeKind.Restored, () =>
        {
            ((IDeletableEntity)entity).DeletedAt = null;
            EntityTraits<TEntity>.ClearDeletedBy(entity);
        });

    /// <summary>
    /// Guards operations that are only meaningful for entities participating in soft delete.
    /// </summary>
    /// <param name="operation">The calling member, supplied by the compiler.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="IDeletableEntity"/>.
    /// </exception>
    private static void EnsureSupportsSoftDelete([CallerMemberName] string operation = "")
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
