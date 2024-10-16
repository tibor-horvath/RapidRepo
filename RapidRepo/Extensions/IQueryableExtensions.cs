using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using RapidRepo.Entities;
using System.Linq.Expressions;

namespace RapidRepo.Extensions;
internal static class IQueryableExtensions
{
    public static IQueryable<TEntity> ApplyFilters<TEntity, TId>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? condition = null,
        Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? orderBy = null,
        Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? include = null,
        bool ignoreQueryFilters = false,
        bool track = true)
        where TEntity : BaseEntity<TId>
        where TId : struct
    {
        if (!track)
        {
            query = query.AsNoTracking();
        }

        if (ignoreQueryFilters)
        {
            query = query.IgnoreQueryFilters();
        }

        if (condition != null)
        {
            query = query.Where(condition);
        }

        if (orderBy != null)
        {
            query = orderBy(query);
        }

        if (include != null)
        {
            query = include(query);
        }

        return query;
    }
}
