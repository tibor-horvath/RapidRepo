using RapidRepo.Entities;

namespace RapidRepo.Repositories.Interfaces;

/// <summary>
/// Defines write operations for a repository handling entities of type <typeparamref name="TEntity"/> with key type <typeparamref name="TKey"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
public interface IWriteRepository<TEntity, in TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(TEntity entity);

    /// <summary>
    /// Adds multiple new entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    void AddRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Adds a new entity to the repository asynchronously.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds multiple new entities to the repository asynchronously.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    void DeleteById(TKey id);

    /// <summary>
    /// Deletes an entity by its identifier asynchronously.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity from the repository.
    /// </summary>
    /// <param name="entity">The entity to delete.</param>
    void Delete(TEntity entity);

    /// <summary>
    /// Deletes multiple entities from the repository.
    /// </summary>
    /// <param name="entities">The entities to delete.</param>
    void DeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Restores a soft-deleted entity by clearing its deletion marks.
    /// </summary>
    /// <param name="entity">The entity to restore.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="Entities.Interfaces.IDeletableEntity"/>.
    /// </exception>
    void Restore(TEntity entity);

    /// <summary>
    /// Restores multiple soft-deleted entities by clearing their deletion marks.
    /// </summary>
    /// <param name="entities">The entities to restore.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="Entities.Interfaces.IDeletableEntity"/>.
    /// </exception>
    void RestoreRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Restores a soft-deleted entity by its identifier. Does nothing when no entity has that identifier.
    /// </summary>
    /// <remarks>
    /// <paramref name="ignoreQueryFilters"/> defaults to <see langword="true"/> because a soft-deleted entity is
    /// hidden by its own soft-delete filter, so the lookup would otherwise never find anything to restore. Note
    /// that this bypasses <em>every</em> global query filter on the entity, not only the soft-delete one — with a
    /// multi-tenant or similar filter in place, pass <see langword="false"/> and load the entity yourself.
    /// </remarks>
    /// <param name="id">The identifier of the entity to restore.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="Entities.Interfaces.IDeletableEntity"/>.
    /// </exception>
    void RestoreById(TKey id, bool ignoreQueryFilters = true);

    /// <summary>
    /// Restores a soft-deleted entity by its identifier asynchronously. Does nothing when no entity has that identifier.
    /// </summary>
    /// <remarks>
    /// <paramref name="ignoreQueryFilters"/> defaults to <see langword="true"/> because a soft-deleted entity is
    /// hidden by its own soft-delete filter, so the lookup would otherwise never find anything to restore. Note
    /// that this bypasses <em>every</em> global query filter on the entity, not only the soft-delete one — with a
    /// multi-tenant or similar filter in place, pass <see langword="false"/> and load the entity yourself.
    /// </remarks>
    /// <param name="id">The identifier of the entity to restore.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TEntity"/> does not implement <see cref="Entities.Interfaces.IDeletableEntity"/>.
    /// </exception>
    Task RestoreByIdAsync(TKey id, bool ignoreQueryFilters = true, CancellationToken cancellationToken = default);

    /// <summary>
    /// Permanently removes an entity, bypassing soft delete.
    /// </summary>
    /// <remarks>
    /// For entities without soft-delete support this is equivalent to <see cref="Delete(TEntity)"/>.
    /// </remarks>
    /// <param name="entity">The entity to remove.</param>
    void HardDelete(TEntity entity);

    /// <summary>
    /// Permanently removes multiple entities, bypassing soft delete.
    /// </summary>
    /// <remarks>
    /// For entities without soft-delete support this is equivalent to <see cref="DeleteRange(IEnumerable{TEntity})"/>.
    /// </remarks>
    /// <param name="entities">The entities to remove.</param>
    void HardDeleteRange(IEnumerable<TEntity> entities);

    /// <summary>
    /// Permanently removes an entity by its identifier, bypassing soft delete.
    /// Does nothing when no entity has that identifier.
    /// </summary>
    /// <remarks>
    /// <paramref name="ignoreQueryFilters"/> defaults to <see langword="false"/>, so only entities the caller can
    /// normally see are reachable. Pass <see langword="true"/> to also reach rows that are already soft-deleted —
    /// but be aware that it bypasses <em>every</em> global query filter on the entity, so with a multi-tenant or
    /// similar filter in place it can permanently erase a row the caller was never meant to see.
    /// </remarks>
    /// <param name="id">The identifier of the entity to remove.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    void HardDeleteById(TKey id, bool ignoreQueryFilters = false);

    /// <summary>
    /// Permanently removes an entity by its identifier asynchronously, bypassing soft delete.
    /// Does nothing when no entity has that identifier.
    /// </summary>
    /// <remarks>
    /// <paramref name="ignoreQueryFilters"/> defaults to <see langword="false"/>, so only entities the caller can
    /// normally see are reachable. Pass <see langword="true"/> to also reach rows that are already soft-deleted —
    /// but be aware that it bypasses <em>every</em> global query filter on the entity, so with a multi-tenant or
    /// similar filter in place it can permanently erase a row the caller was never meant to see.
    /// </remarks>
    /// <param name="id">The identifier of the entity to remove.</param>
    /// <param name="ignoreQueryFilters">Whether to bypass the global query filters when looking the entity up.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task HardDeleteByIdAsync(TKey id, bool ignoreQueryFilters = false, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing entity in the repository.
    /// </summary>
    /// <param name="entity">The entity to update.</param>
    void Update(TEntity entity);

    /// <summary>
    /// Updates multiple existing entities in the repository.
    /// </summary>
    /// <param name="entities">The entities to update.</param>
    void UpdateRange(IEnumerable<TEntity> entities);
}
