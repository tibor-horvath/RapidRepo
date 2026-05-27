using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;

namespace RapidRepo.Repositories;

public sealed class Repository<TEntity, TId> : BaseRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    public Repository(DbContext dbContext) : base(dbContext) { }
}
