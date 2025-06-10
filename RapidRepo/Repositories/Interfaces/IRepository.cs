using RapidRepo.Entities;

namespace RapidRepo.Repositories.Interfaces;

/// <summary>
/// Represents a generic repository interface for CRUD operations.
/// </summary>
/// <typeparam name="TEntity">The type of the entity.</typeparam>
/// <typeparam name="TKey">The type of the entity identifier.</typeparam>
public interface IRepository<TEntity, in TKey> : IReadOnlyRepository<TEntity, TKey>, IWriteRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : struct
{

}

