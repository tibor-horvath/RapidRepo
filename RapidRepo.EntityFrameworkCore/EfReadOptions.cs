using Microsoft.EntityFrameworkCore.Query;
using RapidRepo.Repositories;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// <see cref="ReadOptions"/> with the EF Core specific hints: includes, tracking, split queries and query filters.
/// </summary>
/// <remarks>
/// Pass it wherever the repository contract takes a <see cref="ReadOptions"/>. Other providers ignore the EF
/// members and honour only <see cref="ReadOptions.IncludeDeleted"/>, as do EF repositories for a different
/// <typeparamref name="TEntity"/>.
/// </remarks>
/// <example>
/// <code>
/// repository.GetAll(
///     e =&gt; e.IsActive,
///     SortOrder&lt;Employee&gt;.By(e =&gt; e.LastName),
///     new EfReadOptions&lt;Employee&gt; { Include = q =&gt; q.Include(e =&gt; e.Company), Track = false });
/// </code>
/// </example>
/// <typeparam name="TEntity">The type of the entity being read.</typeparam>
public sealed class EfReadOptions<TEntity> : ReadOptions
    where TEntity : class
{
    /// <summary>
    /// Gets the related data to load, for example <c>q =&gt; q.Include(e =&gt; e.Company)</c>.
    /// </summary>
    public Func<IQueryable<TEntity>, IIncludableQueryable<TEntity, object>>? Include { get; init; }

    /// <summary>
    /// Gets an ordering applied directly to the query. When set, it replaces the <see cref="SortOrder{TEntity}"/>
    /// passed to the repository method.
    /// </summary>
    /// <remarks>
    /// An escape hatch for orderings a <see cref="SortOrder{TEntity}"/> cannot express. Prefer
    /// <see cref="SortOrder{TEntity}"/>, which keeps the call portable to other providers.
    /// </remarks>
    public Func<IQueryable<TEntity>, IOrderedQueryable<TEntity>>? OrderBy { get; init; }

    /// <summary>
    /// Gets a value indicating whether collection includes load through split queries. See
    /// <see href="https://learn.microsoft.com/ef/core/querying/single-split-queries"/>.
    /// </summary>
    public bool SplitQuery { get; init; }

    /// <summary>
    /// Gets a value indicating whether returned entities are tracked by the <c>DbContext</c>. Defaults to
    /// <see langword="true"/>. Projections and aggregates are never tracked.
    /// </summary>
    public bool Track { get; init; } = true;

    /// <summary>
    /// Gets a value indicating whether every global query filter is bypassed, not only the soft-delete one.
    /// </summary>
    /// <remarks>
    /// The tenant filter of <c>AddTenantQueryFilters</c> is the exception: it stays in the query, on the root and on
    /// the tenant entities reached through navigations, and lifts itself only while the query runs inside a host
    /// scope. On a tenant entity, this option throws outside a host scope.
    /// </remarks>
    public bool IgnoreAllQueryFilters { get; init; }
}
