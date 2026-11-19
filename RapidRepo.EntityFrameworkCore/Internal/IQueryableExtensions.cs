using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Extensions;
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
    /// <param name="model">
    /// The model, used to find the named soft-delete filter. Without it, <see cref="ReadOptions.IncludeDeleted"/>
    /// bypasses every filter.
    /// </param>
    /// <returns>The extended query.</returns>
    public static IQueryable<TEntity> ApplyRead<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        bool allowTracking = true,
        IModel? model = null)
        where TEntity : class
    {
        options ??= ReadOptions.Default;

        // Options for another entity type carry no usable EF hints; only the neutral members apply.
        var efOptions = options as EfReadOptions<TEntity>;

        if (!allowTracking || efOptions is { Track: false })
        {
            query = query.AsNoTracking();
        }

        if (efOptions is { IgnoreAllQueryFilters: true })
        {
            query = query.IgnoreQueryFilters();
        }
        else if (options.IncludeDeleted)
        {
            query = query.IgnoreSoftDeleteFilter(model);
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
    /// Bypasses the soft-delete filter, and only as much else as it must:
    /// <list type="bullet">
    /// <item>
    /// The entity has the named filter that <see cref="ModelBuilderExtensions.AddSoftDeleteQueryFilters"/>
    /// registers: only that filter is bypassed, and other filters, such as a tenant filter, stay in force.
    /// </item>
    /// <item>
    /// The entity has an anonymous filter: it may hold the soft-delete condition, and EF Core cannot bypass part of
    /// it, so every filter is bypassed, as in RapidRepo 8.x.
    /// </item>
    /// <item>
    /// The entity has only other named filters, or none: there is no soft-delete filter to bypass, so the query is
    /// returned unchanged. Bypassing everything here would lift a tenant filter for nothing.
    /// </item>
    /// </list>
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="model">
    /// The model the query runs against. Without it the filters cannot be inspected, so every filter is bypassed.
    /// </param>
    /// <returns>The query without the soft-delete filter.</returns>
    public static IQueryable<TEntity> IgnoreSoftDeleteFilter<TEntity>(this IQueryable<TEntity> query, IModel? model)
        where TEntity : class
    {
        var rootType = model?.FindEntityType(typeof(TEntity))?.GetRootType();
        if (rootType is null)
            return query.IgnoreQueryFilters();

        if (rootType.FindDeclaredQueryFilter(ModelBuilderExtensions.SoftDeleteFilterKey) is not null)
            return query.IgnoreQueryFilters([ModelBuilderExtensions.SoftDeleteFilterKey]);

        if (rootType.GetDeclaredQueryFilters().Any(f => f.IsAnonymous))
            return query.IgnoreQueryFilters();

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
