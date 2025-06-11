using RapidRepo.Entities;

namespace RapidRepo.Repositories.Interfaces;

/// <summary>
/// Defines write operations for a repository handling entities of type <typeparamref name="TEntity"/> with key type <typeparamref name="TKey"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TKey">The type of the entity's identifier.</typeparam>
public interface IWriteRepository<TEntity, in TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
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
    Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity to delete.</param>
    void DeleteById(TKey id);

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
