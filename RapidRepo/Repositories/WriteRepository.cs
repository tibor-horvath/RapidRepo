using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Repositories;

public abstract class WriteRepository<TEntity, TId>(DbContext dbContext) : IWriteRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : struct
{
    protected readonly DbContext DbContext = dbContext;
    protected readonly bool SupportsSoftDelete =
            typeof(IDeletableEntity<TId>).IsAssignableFrom(typeof(TEntity)) ||
            typeof(IDeletableEntity).IsAssignableFrom(typeof(TEntity));

    public virtual void Add(TEntity entity)
    {
        DbContext.Set<TEntity>().Add(entity);
    }

    public virtual void AddRange(IEnumerable<TEntity> entities)
    {
        DbContext.Set<TEntity>().AddRange(entities);
    }

    public virtual async Task AddAsync(TEntity entity, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<TEntity>().AddAsync(entity, cancellationToken);
    }

    public virtual async Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<TEntity>().AddRangeAsync(entities, cancellationToken);
    }

    public virtual void Delete(TEntity entity)
    {
        if (SupportsSoftDelete)
        {
            if (DbContext.Entry(entity).State == EntityState.Detached)
            {
                DbContext.Attach(entity);
            }

            ((IDeletableEntity)entity).DeletedAt = DateTime.UtcNow;
        }
        else
        {
            DbContext.Remove(entity);
        }
    }

public virtual void DeleteRange(IEnumerable<TEntity> entities)
{
    var entitiesToRemove = entities.ToList();

    if (SupportsSoftDelete)
    {
        foreach (var e in entitiesToRemove)
        {
            if (DbContext.Entry(e).State == EntityState.Detached)
                DbContext.Attach(e);

            if (e is IDeletableEntity deletable)
                deletable.DeletedAt = DateTime.UtcNow;
        }
    }
    else
    {
        DbContext.RemoveRange(entitiesToRemove);
    }
}

    public virtual void Update(TEntity entity)
    {
        DbContext.Set<TEntity>().Update(entity);
    }

    public virtual void DeleteById(TId id)
    {
        var entity = DbContext.Set<TEntity>().Find(id);
        if (entity != null)
        {
            Delete(entity);
        }
    }

    public virtual void UpdateRange(IEnumerable<TEntity> entities)
    {
        DbContext.Set<TEntity>().UpdateRange(entities);
    }
}
