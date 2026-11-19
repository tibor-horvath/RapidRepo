using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// An <see cref="IReadOnlyRepository{TEntity, TKey}"/> that also exposes EF Core's <see cref="IQueryable{T}"/>.
/// </summary>
/// <remarks>
/// Derive a custom repository interface from this one to opt into EF Core. Interfaces derived from
/// <see cref="IReadOnlyRepository{TEntity, TKey}"/> stay portable to other providers.
/// </remarks>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
public interface IEfReadOnlyRepository<TEntity, in TKey> : IReadOnlyRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    /// <summary>
    /// Returns a query over the entity set with <paramref name="options"/> applied, to compose further with LINQ.
    /// </summary>
    /// <param name="options">The read options, or <see langword="null"/> for the defaults.</param>
    /// <returns>The query. Nothing runs until it is enumerated.</returns>
    IQueryable<TEntity> Query(EfReadOptions<TEntity>? options = null);
}

/// <summary>
/// An <see cref="IRepository{TEntity, TKey}"/> that also exposes EF Core's <see cref="IQueryable{T}"/>.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
public interface IEfRepository<TEntity, in TKey> : IRepository<TEntity, TKey>, IEfReadOnlyRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
}
