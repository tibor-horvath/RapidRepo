using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Extensions;
using RapidRepo.Repositories;
using RapidRepo.Tenancy;
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
    /// <param name="context">
    /// The context the query runs on. Its model is used to find the named soft-delete filter; without it,
    /// <see cref="ReadOptions.IncludeDeleted"/> bypasses every filter. For a tenant entity, it is checked for a current
    /// tenant before the query is built (see <see cref="EnsureTenantReadAllowed{TEntity}"/>).
    /// </param>
    /// <returns>The extended query.</returns>
    /// <exception cref="TenantViolationException">
    /// <typeparamref name="TEntity"/> is a tenant entity, and the read runs without a current tenant, or asks to
    /// ignore every query filter outside a host scope.
    /// </exception>
    public static IQueryable<TEntity> ApplyRead<TEntity>(
        this IQueryable<TEntity> query,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        bool allowTracking = true,
        DbContext? context = null)
        where TEntity : class
    {
        options ??= ReadOptions.Default;

        // Options for another entity type carry no usable EF hints; only the neutral members apply.
        var efOptions = options as EfReadOptions<TEntity>;

        if (context is not null)
            EnsureTenantReadAllowed<TEntity>(context, ignoresAllFilters: efOptions is { IgnoreAllQueryFilters: true });

        if (!allowTracking || efOptions is { Track: false })
        {
            query = query.AsNoTracking();
        }

        if (efOptions is { IgnoreAllQueryFilters: true })
        {
            query = query.IgnoreAllFiltersButTenant(context?.Model);
        }
        else if (options.IncludeDeleted)
        {
            query = query.IgnoreSoftDeleteFilter(context?.Model);
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
    /// it, so every filter is bypassed, as in RapidRepo 8.x. In a model with tenant entities this throws instead: EF
    /// Core lifts filters for the whole query, so the tenant filter of any tenant set joined into it would go too.
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
        {
            // Lifting every filter would lift the tenant filter of any tenant entity the query reads: the root, one it
            // reaches through a navigation, or a set the caller joins into it. Only models without tenant entities keep
            // the 8.x fallback.
            if (TenantReachability.HasTenantData(model!))
            {
                throw new TenantViolationException(
                    $"IncludeDeleted cannot be applied to {typeof(TEntity).Name}: it has an anonymous query filter, " +
                    "which can only be lifted together with the tenant filters of the model. " +
                    "Make the soft-delete filter named " +
                    "(AddSoftDeleteQueryFilters does so unless another anonymous filter exists) and use " +
                    "AddTenantQueryFilters for tenancy.",
                    typeof(TEntity));
            }

            return query.IgnoreQueryFilters();
        }

        return query;
    }

    /// <summary>
    /// Lifts every query filter except the tenant filter, which stays in the query and lifts itself when the query
    /// <em>executes</em> inside a host scope. So a query built in a host scope and run after it, such as one returned
    /// by <c>Query()</c>, is filtered by tenant again, and so are the tenant entities the query reaches through
    /// navigations.
    /// </summary>
    /// <param name="query">The query.</param>
    /// <param name="model">
    /// The model the query runs against. Without it, or in a model without tenant entities, every filter is lifted, as
    /// before tenancy existed. Whether the root reaches tenant data through navigations does not matter: the caller can
    /// join any tenant entity set into the query, and EF Core lifts filters for the whole query.
    /// </param>
    /// <returns>The query without its filters, the tenant filter excepted.</returns>
    /// <exception cref="TenantViolationException">
    /// A type the query can read has an anonymous filter, which cannot be lifted without the tenant filter.
    /// </exception>
    public static IQueryable<TEntity> IgnoreAllFiltersButTenant<TEntity>(this IQueryable<TEntity> query, IModel? model)
        where TEntity : class
    {
        if (model?.FindEntityType(typeof(TEntity)) is not { } entityType || !TenantReachability.HasTenantData(model))
            return query.IgnoreQueryFilters();

        var reachable = TenantReachability.GetReachableTypes(entityType);

        if (reachable.Select(t => t.GetRootType()).FirstOrDefault(t => t.GetDeclaredQueryFilters().Any(f => f.IsAnonymous)) is { } anonymous)
        {
            throw new TenantViolationException(
                $"IgnoreAllQueryFilters cannot be applied to {typeof(TEntity).Name}: {anonymous.ClrType.Name}, which the " +
                "query can read, has an anonymous query filter, and EF Core can only lift it together with the tenant " +
                "filters of the model. Name that filter, or read without IgnoreAllQueryFilters.",
                typeof(TEntity));
        }

        var keys = model.GetEntityTypes()
            .SelectMany(t => t.GetDeclaredQueryFilters())
            .Where(f => !f.IsAnonymous && f.Key != ModelBuilderExtensions.TenantFilterKey)
            .Select(f => f.Key!)
            .Distinct()
            .ToList();

        return keys.Count == 0 ? query : query.IgnoreQueryFilters(keys);
    }

    /// <summary>
    /// Fails closed before a tenant entity is read: the tenancy configuration must be valid, a current tenant must
    /// exist, and every query filter may only be ignored inside a host scope. A query filter cannot throw, so this is
    /// checked here; the filter itself then matches no rows without a tenant, as a second line of defence.
    /// </summary>
    /// <exception cref="TenantViolationException">There is no current tenant, or every filter is ignored outside a host scope.</exception>
    /// <exception cref="RapidRepoConfigurationException">The context's tenancy configuration is invalid.</exception>
    internal static void EnsureTenantReadAllowed<TEntity>(DbContext context, bool ignoresAllFilters)
    {
        if (TenantTraits.GetTenantKeyType(typeof(TEntity)) is not { } keyType)
            return;

        TenancyValidator.EnsureValid(context);

        if (AmbientTenant.IsHostScope)
            return;

        if (ignoresAllFilters)
        {
            throw new TenantViolationException(
                $"IgnoreAllQueryFilters would lift the tenant filter of {typeof(TEntity).Name}. Read across tenants " +
                "only inside ITenantHostScope.BeginHostScope().",
                typeof(TEntity));
        }

        if (TenantDbContexts.GetCurrentTenantId(context, keyType) is null)
            throw TenantViolationException.MissingTenant(typeof(TEntity), "read");
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
