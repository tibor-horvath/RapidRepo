using Microsoft.EntityFrameworkCore.Query;
using RapidRepo.Entities;
using System.Linq.Expressions;

namespace RapidRepo.Repositories.Interfaces;

/// <summary>
/// Represents a generic repository interface for CRUD operations.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
public interface IRepository<TEntity, in TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
{
    /// <summary>
    /// Checks if any entity matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <returns>True if any entity matches the condition, otherwise false.</returns>
    bool Any(Expression<Func<TEntity, bool>> condition);

    /// <summary>
    /// Checks if any entity matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if any entity matches the condition, otherwise false.</returns>
    Task<bool> AnyAsync(Expression<Func<TEntity, bool>> condition, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <returns>The entity with the specified identifier, or null if not found.</returns>
    TEntity? GetById(TKey id,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool track = true,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Gets an entity by its identifier and applies a selector.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <returns>The result of the selector function for the entity with the specified identifier, or null if not found.</returns>
    TResult? GetById<TResult>(TKey id,
        Expression<Func<TEntity, TResult>> selector,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity with the specified identifier, or null if not found.</returns>
    Task<TEntity?> GetByIdAsync(TKey id,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool track = true,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets an entity by its identifier and applies a selector.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the selector function for the entity with the specified identifier, or null if not found.</returns>
    Task<TResult?> GetByIdAsync<TResult>(TKey id,
        Expression<Func<TEntity, TResult>> selector,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new entity to the repository.
    /// </summary>
    /// <param name="entity">The entity to add.</param>
    void Add(TEntity entity);

    /// <summary>
    /// Adds multiple new entities to the repository.
    /// </summary>
    /// <param name="entities">The entities to add.</param>
    void Add(IEnumerable<TEntity> entities);

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
    void Delete(IEnumerable<TEntity> entities);

    /// <summary>
    /// Deletes a range of entities from the repository.
    /// </summary>
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

    /// <summary>
    /// Gets the first entity that matches the specified condition or null if no match is found.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The first entity that matches the condition, or null if no match is found.</returns>
    TEntity? GetFirstOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true);

    /// <summary>
    /// Gets the first entity that matches the specified condition and applies a selector, or null if no match is found.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <returns>The result of the selector function for the first entity that matches the condition, or null if no match is found.</returns>
    TResult? GetFirstOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets the first entity that matches the specified condition or null if no match is found.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first entity that matches the condition, or null if no match is found.</returns>
    Task<TEntity?> GetFirstOrDefaultAsync(Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the first entity that matches the specified condition and applies a selector, or null if no match is found.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the selector function for the first entity that matches the condition, or null if no match is found.</returns>
    Task<TResult?> GetFirstOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity that matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The first entity that matches the condition.</returns>
    TEntity GetFirst(Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true);

    /// <summary>
    /// Gets the first entity that matches the specified condition and applies a selector.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <returns>The result of the selector function for the first entity that matches the condition.</returns>
    TResult GetFirst<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets the first entity that matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first entity that matches the condition.</returns>
    Task<TEntity> GetFirstAsync(Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets the first entity that matches the specified condition and applies a selector.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the selector function for the first entity that matches the condition.</returns>
    Task<TResult> GetFirstAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the single entity that matches the specified condition or null if no match is found.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The single entity that matches the condition, or null if no match is found.</returns>
    TEntity? GetSingleOrDefault(Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true);

    /// <summary>
    /// Gets the single entity that matches the specified condition and applies a selector, or null if no match is found.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <returns>The result of the selector function for the single entity that matches the condition, or null if no match is found.</returns>
    TResult? GetSingleOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets the single entity that matches the specified condition or null if no match is found.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The single entity that matches the condition, or null if no match is found.</returns>
    Task<TEntity?> GetSingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the single entity that matches the specified condition and applies a selector, or null if no match is found.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of the selector function for the single entity that matches the condition, or null if no match is found.</returns>
    Task<TResult?> GetSingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the single entity that matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The single entity that matches the condition.</returns>
    TEntity GetSingle(Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true);

    /// <summary>
    /// Gets the single entity that matches the specified condition and applies a selector.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The result of the selector function for the single entity that matches the condition.</returns>
    TResult GetSingle<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets the single entity that matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The single entity that matches the condition.</returns>
    Task<TEntity> GetSingleAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default);


    /// <summary>
    /// Asynchronously gets the single entity that matches the specified condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The single entity that matches the condition.</returns>
    Task<TResult> GetSingleAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all entities that match the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The entities that match the condition.</returns>
    IEnumerable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true);

    /// <summary>
    /// Gets all entities that match the specified condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <returns>The entities that match the condition.</returns>
    IEnumerable<TResult> GetAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false);

    /// <summary>
    /// Asynchronously gets all entities that match the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entities that match the condition.</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets all entities that match the specified condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="selector">A function to select the result.</param>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="orderBy">A function to order the entities.</param>
    /// <param name="include">A function to include related entities.</param>
    /// <param name="useSplitQueries">Whether to use split queries for related entities. See <see href="https://learn.microsoft.com/en-us/ef/core/querying/single-split-queries"/> for more details.</param>
    /// <param name="ignoreQueryFilters">Whether to ignore query filters.</param>
    /// <param name="track">Whether to track the entity in the context.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entities that match the condition.</returns>
    Task<IEnumerable<TResult>> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellation = default);
}

