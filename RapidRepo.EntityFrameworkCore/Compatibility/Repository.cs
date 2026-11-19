using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 7.x name of <see cref="EfRepository{TEntity, TId}"/>.</summary>
[Obsolete(Obsoletions.Repository, DiagnosticId = Obsoletions.DiagnosticId)]
public sealed class Repository<TEntity, TId> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    public Repository(DbContext dbContext) : base(dbContext) { }
}

/// <summary>The 7.x name of <see cref="EfRepository{TEntity, TId, TContext}"/>.</summary>
[Obsolete(Obsoletions.ContextBoundRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public sealed class Repository<TEntity, TId, TContext> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
    where TContext : DbContext
{
    public Repository(TContext dbContext) : base(dbContext) { }
}
