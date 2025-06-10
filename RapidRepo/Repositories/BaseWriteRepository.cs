using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;

namespace RapidRepo.Repositories;

/// <summary>
/// The <see cref="BaseWriteRepository{TEntity, TId}"/> class exists mainly for backward compatibility in the <see cref="BaseRepository{TEntity, TId}"/> class.
/// </summary>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <param name="dbContext">The <see cref="DbContext"/> used for data access operations.</param>
/// <remarks>
/// Serves to maintain backward compatibility in the <see cref="BaseRepository{TEntity, TId}"/> class.
/// </remarks>
public sealed class BaseWriteRepository<TEntity, TId>(DbContext dbContext)
    : WriteRepository<TEntity, TId>(dbContext)
    where TEntity : BaseEntity<TId>
    where TId : struct
{
}
