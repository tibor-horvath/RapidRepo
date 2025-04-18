﻿using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using RapidRepo.Dtos;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;
using RapidRepo.Repositories.Interfaces;
using System.Linq.Expressions;

namespace RapidRepo.Repositories;

public abstract class BaseRepository<TEntity, TId> : IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : struct
{
    protected readonly DbContext DbContext;
    protected readonly bool SupportsSoftDelete;

    protected BaseRepository(DbContext dbContext)
    {
        DbContext = dbContext;
        SupportsSoftDelete =
            typeof(IDeletableEntity<TId>).IsAssignableFrom(typeof(TEntity)) ||
            typeof(IDeletableEntity).IsAssignableFrom(typeof(TEntity));
    }

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

    public virtual bool Any(Expression<Func<TEntity, bool>> condition)
    {
        return DbContext.Set<TEntity>().Any(condition);
    }

    public virtual async Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> condition,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<TEntity>().AnyAsync(condition, cancellationToken);
    }

    public virtual int Count(
        Expression<Func<TEntity, bool>>? condition = null,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                ignoreQueryFilters: ignoreQueryFilters)
            .Count();
    }

    public virtual async Task<int> CountAsync(
        Expression<Func<TEntity,
        bool>>? condition = null,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                ignoreQueryFilters: ignoreQueryFilters)
            .CountAsync(cancellationToken);
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
            DbContext.AttachRange(entitiesToRemove);
            entitiesToRemove.ForEach(e => ((IDeletableEntity)e).DeletedAt = DateTime.UtcNow);
        }
        else
        {
            DbContext.RemoveRange(entitiesToRemove);
        }
    }

    public virtual IEnumerable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .ToList();
    }

    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .ToListAsync(cancellationToken);
    }

    public virtual TEntity? GetById(
        TId id,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool track = true,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .FirstOrDefault(e => e.Id.Equals(id));
    }

    public virtual TResult? GetById<TResult>(
        TId id,
        Expression<Func<TEntity, TResult>> selector,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: e => e.Id.Equals(id),
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Where(e => e.Id.Equals(id))
            .Select(selector)
            .FirstOrDefault();
    }

    public virtual async Task<TEntity?> GetByIdAsync(
        TId id,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool track = true,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .FirstOrDefaultAsync(e => e.Id.Equals(id), cancellationToken);
    }

    public virtual TEntity GetFirst(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .First();
    }

    public virtual async Task<TEntity> GetFirstAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .FirstAsync(cancellationToken);
    }

    public virtual TEntity? GetFirstOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .FirstOrDefault();
    }

    public virtual async Task<TEntity?> GetFirstOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual Paged<TEntity> GetAllPaged(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        int pageIndex = 1,
        int pageSize = 10)
    {
        var query = DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track);

        var totalCount = query.Count();

        var results = query.Skip((pageIndex - 1) * pageSize)
                           .Take(pageSize)
                           .ToList();

        return new Paged<TEntity>
        {
            Results = results,
            TotalCount = totalCount,
            Page = pageIndex,
            PageSize = pageSize
        };
    }

    public virtual async Task<Paged<TEntity>> GetPagedAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        int pageIndex = 1,
        int pageSize = 10,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track);

        var totalCount = await query.CountAsync(cancellationToken);

        var results = await query.Skip((pageIndex - 1) * pageSize)
                                 .Take(pageSize)
                                 .ToListAsync(cancellationToken);

        return new Paged<TEntity>
        {
            Results = results,
            TotalCount = totalCount,
            Page = pageIndex,
            PageSize = pageSize
        };
    }

    public virtual TEntity GetSingle(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .Single();
    }

    public virtual async Task<TEntity> GetSingleAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .SingleAsync(cancellationToken);
    }

    public virtual TEntity? GetSingleOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .SingleOrDefault();
    }

    public virtual async Task<TEntity?> GetSingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        bool track = true,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: track)
            .SingleOrDefaultAsync(cancellationToken);
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

    public virtual Task<TResult?> GetByIdAsync<TResult>(
        TId id,
        Expression<Func<TEntity, TResult>> selector,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: e => e.Id.Equals(id),
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual TResult? GetFirstOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .FirstOrDefault();
    }

    public virtual Task<TResult?> GetFirstOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public virtual TResult GetFirst<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .First();
    }

    public virtual Task<TResult> GetFirstAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .FirstAsync(cancellationToken);
    }

    public virtual TResult? GetSingleOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .SingleOrDefault();
    }

    public virtual async Task<TResult?> GetSingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public virtual TResult GetSingle<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .Single();
    }

    public virtual async Task<TResult> GetSingleAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellationToken = default)
    {
        return await DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .SingleAsync(cancellationToken);
    }

    public virtual IEnumerable<TResult> GetAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false)
    {
        return DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false)
            .Select(selector)
            .ToList();
    }

    public virtual async Task<IEnumerable<TResult>> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool useSplitQueries = false,
        bool ignoreQueryFilters = false,
        CancellationToken cancellation = default)
    {
        var query = DbContext
            .Set<TEntity>()
            .AsQueryable()
            .ApplyFilters<TEntity, TId>(
                condition: condition,
                orderBy: orderBy,
                include: include,
                ignoreQueryFilters: ignoreQueryFilters,
                track: false);

        return await query
            .Select(selector)
            .ToListAsync(cancellation);
    }
}
