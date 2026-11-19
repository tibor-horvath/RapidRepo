using RapidRepo.Dtos;
using RapidRepo.Entities;
using System.Linq.Expressions;

namespace RapidRepo.Repositories.Interfaces;

/// <summary>
/// Represents a generic read-only repository interface for querying entities.
/// </summary>
/// <remarks>
/// <para>
/// The contract is provider-neutral. Filters and projections are expression trees, sorting is a declarative
/// <see cref="SortOrder{TEntity}"/>, and everything else travels in a <see cref="ReadOptions"/>, which a provider
/// may subclass to carry its own hints (for example <c>EfReadOptions&lt;TEntity&gt;</c> for includes, tracking and
/// split queries in RapidRepo.EntityFrameworkCore).
/// </para>
/// <para>
/// Unless <see cref="ReadOptions.IncludeDeleted"/> is set, soft-deleted entities are excluded.
/// </para>
/// <para>
/// Projections that also take a condition require a reference-type result. Without that constraint,
/// <c>GetFirstOrDefault(e =&gt; e.IsActive)</c> would be ambiguous between a condition and a <see langword="bool"/>
/// projection. Project scalars through an anonymous type, for example <c>GetAll(e =&gt; new { e.Id })</c>.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
public interface IReadOnlyRepository<TEntity, in TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Checks if an entity with the specified identifier exists.
    /// </summary>
    /// <param name="id">The identifier to look up.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>True if an entity with the specified identifier exists, otherwise false.</returns>
    bool ExistsById(TKey id, ReadOptions? options = null);

    /// <summary>
    /// Asynchronously checks if an entity with the specified identifier exists.
    /// </summary>
    /// <param name="id">The identifier to look up.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if an entity with the specified identifier exists, otherwise false.</returns>
    Task<bool> ExistsByIdAsync(TKey id, ReadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if any entity matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>True if any entity matches the condition, otherwise false.</returns>
    bool Any(Expression<Func<TEntity, bool>> condition, ReadOptions? options = null);

    /// <summary>
    /// Asynchronously checks if any entity matches the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>True if any entity matches the condition, otherwise false.</returns>
    Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> condition,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Counts the entities that match the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> to count all of them.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The number of matching entities.</returns>
    int Count(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null);

    /// <summary>
    /// Asynchronously counts the entities that match the specified condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> to count all of them.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The number of matching entities.</returns>
    Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The entity with the specified identifier, or null if not found.</returns>
    TEntity? GetById(TKey id, ReadOptions? options = null);

    /// <summary>
    /// Gets a projection of an entity by its identifier.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projection of the entity with the specified identifier, or the default value if not found.</returns>
    TResult? GetById<TResult>(TKey id, Expression<Func<TEntity, TResult>> selector, ReadOptions? options = null);

    /// <summary>
    /// Asynchronously gets an entity by its identifier.
    /// </summary>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entity with the specified identifier, or null if not found.</returns>
    Task<TEntity?> GetByIdAsync(TKey id, ReadOptions? options = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets a projection of an entity by its identifier.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="id">The identifier of the entity.</param>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projection of the entity with the specified identifier, or the default value if not found.</returns>
    Task<TResult?> GetByIdAsync<TResult>(
        TKey id,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the entities with the specified identifiers. Identifiers without an entity are skipped.
    /// </summary>
    /// <param name="ids">The identifiers of the entities.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The entities found.</returns>
    IEnumerable<TEntity> GetByIds(IEnumerable<TKey> ids, ReadOptions? options = null);

    /// <summary>
    /// Gets projections of the entities with the specified identifiers. Identifiers without an entity are skipped.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="ids">The identifiers of the entities.</param>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projections of the entities found.</returns>
    IEnumerable<TResult> GetByIds<TResult>(
        IEnumerable<TKey> ids,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null);

    /// <summary>
    /// Asynchronously gets the entities with the specified identifiers. Identifiers without an entity are skipped.
    /// </summary>
    /// <param name="ids">The identifiers of the entities.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The entities found.</returns>
    Task<IEnumerable<TEntity>> GetByIdsAsync(
        IEnumerable<TKey> ids,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets projections of the entities with the specified identifiers. Identifiers without an
    /// entity are skipped.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="ids">The identifiers of the entities.</param>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projections of the entities found.</returns>
    Task<IEnumerable<TResult>> GetByIdsAsync<TResult>(
        IEnumerable<TKey> ids,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the first entity that matches the condition, or null if there is none.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The first matching entity, or null.</returns>
    TEntity? GetFirstOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null);

    /// <summary>
    /// Gets a projection of the first entity that matches the condition, or the default value if there is none.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projection of the first matching entity, or the default value.</returns>
    TResult? GetFirstOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets the first entity that matches the condition, or null if there is none.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first matching entity, or null.</returns>
    Task<TEntity?> GetFirstOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets a projection of the first entity that matches the condition, or the default value if
    /// there is none.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projection of the first matching entity, or the default value.</returns>
    Task<TResult?> GetFirstOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>
    /// Gets the first entity that matches the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The first matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matches.</exception>
    TEntity GetFirst(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null);

    /// <summary>
    /// Gets a projection of the first entity that matches the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projection of the first matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matches.</exception>
    TResult GetFirst<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets the first entity that matches the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The first matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matches.</exception>
    Task<TEntity> GetFirstAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets a projection of the first entity that matches the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order that decides which entity is first, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projection of the first matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity matches.</exception>
    Task<TResult> GetFirstAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>
    /// Gets the only entity that matches the condition, or null if there is none.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The only matching entity, or null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than one entity matches.</exception>
    TEntity? GetSingleOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null);

    /// <summary>
    /// Gets a projection of the only entity that matches the condition, or the default value if there is none.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projection of the only matching entity, or the default value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than one entity matches.</exception>
    TResult? GetSingleOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets the only entity that matches the condition, or null if there is none.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only matching entity, or null.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than one entity matches.</exception>
    Task<TEntity?> GetSingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets a projection of the only entity that matches the condition, or the default value if
    /// there is none.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projection of the only matching entity, or the default value.</returns>
    /// <exception cref="InvalidOperationException">Thrown when more than one entity matches.</exception>
    Task<TResult?> GetSingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>
    /// Gets the only entity that matches the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The only matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity, or more than one, matches.</exception>
    TEntity GetSingle(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null);

    /// <summary>
    /// Gets a projection of the only entity that matches the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projection of the only matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity, or more than one, matches.</exception>
    TResult GetSingle<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets the only entity that matches the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The only matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity, or more than one, matches.</exception>
    Task<TEntity> GetSingleAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets a projection of the only entity that matches the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projection of the only matching entity.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no entity, or more than one, matches.</exception>
    Task<TResult> GetSingleAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>
    /// Gets all entities that match the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The matching entities.</returns>
    IEnumerable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null);

    /// <summary>
    /// Gets projections of all entities that match the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The projections of the matching entities.</returns>
    IEnumerable<TResult> GetAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets all entities that match the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The matching entities.</returns>
    Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets projections of all entities that match the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The projections of the matching entities.</returns>
    Task<IEnumerable<TResult>> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;

    /// <summary>
    /// Gets one page of the entities that match the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The page, with the total number of matching entities.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    Paged<TEntity> GetAllPaged(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null);

    /// <summary>
    /// Gets one page of projections of the entities that match the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <returns>The page, with the total number of matching entities.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    Paged<TResult> GetAllPaged<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null)
        where TResult : class;

    /// <summary>
    /// Asynchronously gets one page of the entities that match the condition.
    /// </summary>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, with the total number of matching entities.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    Task<Paged<TEntity>> GetAllPagedAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Asynchronously gets one page of projections of the entities that match the condition.
    /// </summary>
    /// <typeparam name="TResult">The type of the projection.</typeparam>
    /// <param name="selector">The projection to apply.</param>
    /// <param name="condition">The condition to filter entities, or <see langword="null"/> for all entities.</param>
    /// <param name="orderBy">The sort order, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The 1-based page number.</param>
    /// <param name="pageSize">The number of entities per page.</param>
    /// <param name="options">The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The page, with the total number of matching entities.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="pageIndex"/> or <paramref name="pageSize"/> is less than 1.
    /// </exception>
    Task<Paged<TResult>> GetAllPagedAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class;
}
