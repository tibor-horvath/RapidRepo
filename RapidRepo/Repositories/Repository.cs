using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;

namespace RapidRepo.Repositories;

public sealed class Repository<TEntity, TId> : BaseRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    public Repository(DbContext dbContext) : base(dbContext) { }
}

/// <summary>
/// A <see cref="Repository{TEntity, TId}"/> bound to a specific <typeparamref name="TContext"/>.
/// </summary>
/// <remarks>
/// <para>
/// Depending on <typeparamref name="TContext"/> rather than the base <see cref="DbContext"/> lets the
/// container resolve the constructor directly — applications register their derived context with
/// <c>AddDbContext&lt;TContext&gt;()</c>, which does not register the base <see cref="DbContext"/> type.
/// </para>
/// <para>
/// It also makes the binding explicit, so an application with several contexts can register one
/// repository per context for the same entity without them competing for a single
/// <see cref="DbContext"/> registration.
/// </para>
/// </remarks>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <typeparam name="TContext">The context that owns <typeparamref name="TEntity"/>.</typeparam>
public sealed class Repository<TEntity, TId, TContext> : BaseRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
    where TContext : DbContext
{
    public Repository(TContext dbContext) : base(dbContext) { }
}
