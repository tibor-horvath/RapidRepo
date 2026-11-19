using Microsoft.EntityFrameworkCore;
using RapidRepo.Repositories;
using System.Linq.Expressions;

namespace RapidRepo.EntityFrameworkCore.Internal;

internal static class IQueryableExtensions
{
    /// <summary>
    /// Applies a repository read to an EF Core query: options, filter, ordering and includes.
    /// </summary>
    /// <param name="query">The query to extend.</param>
    /// <param name="condition">The filter, if any.</param>
    /// <param name="orderBy">The sort order, if any. <see cref="EfReadOptions{TEntity}.OrderBy"/> takes precedence.</param>
    /// <param name="options">The read options; <see langword="null"/> means <see cref="ReadOptions.Default"/>.</param>
    /// <param name="allowTracking">
    /// <see langword="false"/> for projections and aggregates, which are never tracked whatever the options say.
    /// </param>
    /// <returns>The extended query.</returns>
    public static IQueryable<TEntity> ApplyRead<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        bool allowTracking = true)
        where TEntity : class
    {
        options ??= ReadOptions.Default;

        // Options for another entity type carry no usable EF hints; only the neutral members apply.
        var efOptions = options as EfReadOptions<TEntity>;

        if (!allowTracking || efOptions is { Track: false })
        {
            query = query.AsNoTracking();
        }

        // TODO(#111, Phase 4): on EF Core 10, map IncludeDeleted to the named soft-delete filter only.
        if (options.IncludeDeleted || efOptions is { IgnoreAllQueryFilters: true })
        {
            query = query.IgnoreQueryFilters();
        }

        if (condition != null)
        {
            query = query.Where(condition);
        }

        if (efOptions?.OrderBy != null)
        {
            query = efOptions.OrderBy(query);
        }
        else if (orderBy != null)
        {
            query = query.ApplySortOrder(orderBy);
        }

        if (efOptions?.Include != null)
        {
            query = efOptions.Include(query);
        }

        if (efOptions is { SplitQuery: true })
        {
            query = query.AsSplitQuery();
        }

        return query;
    }

    /// <summary>
    /// Translates a <see cref="SortOrder{TEntity}"/> into a LINQ <c>OrderBy</c>/<c>ThenBy</c> chain.
    /// </summary>
    /// <param name="query">The query to sort.</param>
    /// <param name="sortOrder">The sort order.</param>
    /// <returns>The sorted query.</returns>
    public static IOrderedQueryable<TEntity> ApplySortOrder<TEntity>(this IQueryable<TEntity> query, SortOrder<TEntity> sortOrder)
    {
        var expression = query.Expression;

        for (var i = 0; i < sortOrder.Keys.Count; i++)
        {
            var (key, descending) = sortOrder.Keys[i];
            var method = (i == 0, descending) switch
            {
                (true, false) => nameof(Queryable.OrderBy),
                (true, true) => nameof(Queryable.OrderByDescending),
                (false, false) => nameof(Queryable.ThenBy),
                (false, true) => nameof(Queryable.ThenByDescending),
            };

            expression = Expression.Call(
                typeof(Queryable),
                method,
                [typeof(TEntity), key.ReturnType],
                expression,
                Expression.Quote(key));
        }

        return (IOrderedQueryable<TEntity>)query.Provider.CreateQuery<TEntity>(expression);
    }
}
