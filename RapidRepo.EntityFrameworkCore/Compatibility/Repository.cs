using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore;
using RapidRepo.EntityFrameworkCore.Compatibility;

namespace RapidRepo.Repositories;

/// <summary>The 8.x name of <see cref="EfRepository{TEntity, TId}"/>.</summary>
[Obsolete(Obsoletions.Repository, DiagnosticId = Obsoletions.DiagnosticId)]
public sealed class Repository<TEntity, TId> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    /// <summary>Creates a repository that reads from and stages changes in <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context the repository reads from and stages changes in.</param>
    public Repository(DbContext dbContext) : base(dbContext) { }
}

/// <summary>The 8.x name of <see cref="EfRepository{TEntity, TId, TContext}"/>.</summary>
[Obsolete(Obsoletions.ContextBoundRepository, DiagnosticId = Obsoletions.DiagnosticId)]
public sealed class Repository<TEntity, TId, TContext> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
    where TContext : DbContext
{
    /// <summary>Creates a repository that reads from and stages changes in <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context the repository reads from and stages changes in.</param>
    public Repository(TContext dbContext) : base(dbContext) { }
}
