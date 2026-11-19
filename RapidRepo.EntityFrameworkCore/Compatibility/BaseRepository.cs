using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 8.x base class for custom repositories; derive from <see cref="EfRepository{TEntity, TId}"/> instead.</summary>
[Obsolete(Obsoletions.BaseRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public abstract class BaseRepository<TEntity, TId> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    public BaseRepository(DbContext dbContext) : base(dbContext) { }
}
