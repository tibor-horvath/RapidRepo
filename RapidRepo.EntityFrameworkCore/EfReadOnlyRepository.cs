using Microsoft.EntityFrameworkCore;
using RapidRepo.Dtos;
using RapidRepo.Entities;
using RapidRepo.EntityFrameworkCore.Internal;
using RapidRepo.Repositories;
using System.Linq.Expressions;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// An EF Core implementation of the read side of the repository contract.
/// </summary>
/// <remarks>
/// Every read accepts <see cref="EfReadOptions{TEntity}"/> in place of <see cref="ReadOptions"/> for includes,
/// tracking, split queries and query filters. Entities are tracked unless the options say otherwise; projections,
/// counts and existence checks are never tracked.
/// </remarks>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <param name="dbContext">The <see cref="DbContext"/> used for data access operations.</param>
public abstract class EfReadOnlyRepository<TEntity, TId>(DbContext dbContext) : IEfReadOnlyRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    /// <summary>
    /// The order paging falls back to without a sort order, so pages are stable. <see cref="EfReadOptions{TEntity}.OrderBy"/>
    /// still takes precedence over it.
    /// </summary>
    private static readonly SortOrder<TEntity> PageOrder = Paging.DefaultOrder<TEntity, TId>();

    /// <summary>
    /// Gets the <see cref="DbContext"/> the repository reads from and stages changes in.
    /// </summary>
    protected DbContext DbContext { get; } = dbContext ?? throw new ArgumentNullException(nameof(dbContext));

    /// <inheritdoc />
    public virtual IQueryable<TEntity> Query(EfReadOptions<TEntity>? options = null) => BuildQuery(options: options);

    /// <inheritdoc />
    public virtual bool ExistsById(TId id, ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return Untracked(options: options).Any(ById(id));
    }

    /// <inheritdoc />
    public virtual Task<bool> ExistsByIdAsync(
        TId id,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return Untracked(options: options).AnyAsync(ById(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual bool Any(Expression<Func<TEntity, bool>> condition, ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return Untracked(options: options).Any(condition);
    }

    /// <inheritdoc />
    public virtual Task<bool> AnyAsync(
        Expression<Func<TEntity, bool>> condition,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(condition);

        return Untracked(options: options).AnyAsync(condition, cancellationToken);
    }

    /// <inheritdoc />
    public virtual int Count(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) =>
        Untracked(condition, options: options).Count();

    /// <inheritdoc />
    public virtual Task<int> CountAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        Untracked(condition, options: options).CountAsync(cancellationToken);

    /// <inheritdoc />
    public virtual TEntity? GetById(TId id, ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(id);

        return BuildQuery(options: options).FirstOrDefault(ById(id));
    }

    /// <inheritdoc />
    public virtual TResult? GetById<TResult>(
        TId id,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(ById(id), options: options).Select(selector).FirstOrDefault();
    }

    /// <inheritdoc />
    public virtual Task<TEntity?> GetByIdAsync(
        TId id,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);

        return BuildQuery(options: options).FirstOrDefaultAsync(ById(id), cancellationToken);
    }

    /// <inheritdoc />
    public virtual Task<TResult?> GetByIdAsync<TResult>(
        TId id,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(id);
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(ById(id), options: options).Select(selector).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual IEnumerable<TEntity> GetByIds(IEnumerable<TId> ids, ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids as ICollection<TId> ?? ids.ToList();
        if (idList.Count == 0) return [];

        return BuildQuery(ByIds(idList), options: options).ToList();
    }

    /// <inheritdoc />
    public virtual IEnumerable<TResult> GetByIds<TResult>(
        IEnumerable<TId> ids,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(selector);

        var idList = ids as ICollection<TId> ?? ids.ToList();
        if (idList.Count == 0) return [];

        return Untracked(ByIds(idList), options: options).Select(selector).ToList();
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> GetByIdsAsync(
        IEnumerable<TId> ids,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);

        var idList = ids as ICollection<TId> ?? ids.ToList();
        if (idList.Count == 0) return [];

        return await BuildQuery(ByIds(idList), options: options).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TResult>> GetByIdsAsync<TResult>(
        IEnumerable<TId> ids,
        Expression<Func<TEntity, TResult>> selector,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ids);
        ArgumentNullException.ThrowIfNull(selector);

        var idList = ids as ICollection<TId> ?? ids.ToList();
        if (idList.Count == 0) return [];

        return await Untracked(ByIds(idList), options: options).Select(selector).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity? GetFirstOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null) =>
        BuildQuery(condition, orderBy, options).FirstOrDefault();

    /// <inheritdoc />
    public virtual TResult? GetFirstOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, orderBy, options).Select(selector).FirstOrDefault();
    }

    /// <inheritdoc />
    public virtual Task<TEntity?> GetFirstOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        BuildQuery(condition, orderBy, options).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public virtual Task<TResult?> GetFirstOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, orderBy, options).Select(selector).FirstOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity GetFirst(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null) =>
        BuildQuery(condition, orderBy, options).First();

    /// <inheritdoc />
    public virtual TResult GetFirst<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, orderBy, options).Select(selector).First();
    }

    /// <inheritdoc />
    public virtual Task<TEntity> GetFirstAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        BuildQuery(condition, orderBy, options).FirstAsync(cancellationToken);

    /// <inheritdoc />
    public virtual Task<TResult> GetFirstAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, orderBy, options).Select(selector).FirstAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity? GetSingleOrDefault(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null) =>
        BuildQuery(condition, options: options).SingleOrDefault();

    /// <inheritdoc />
    public virtual TResult? GetSingleOrDefault<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, options: options).Select(selector).SingleOrDefault();
    }

    /// <inheritdoc />
    public virtual Task<TEntity?> GetSingleOrDefaultAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        BuildQuery(condition, options: options).SingleOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public virtual Task<TResult?> GetSingleOrDefaultAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, options: options).Select(selector).SingleOrDefaultAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual TEntity GetSingle(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null) =>
        BuildQuery(condition, options: options).Single();

    /// <inheritdoc />
    public virtual TResult GetSingle<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, options: options).Select(selector).Single();
    }

    /// <inheritdoc />
    public virtual Task<TEntity> GetSingleAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        BuildQuery(condition, options: options).SingleAsync(cancellationToken);

    /// <inheritdoc />
    public virtual Task<TResult> GetSingleAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, options: options).Select(selector).SingleAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual IEnumerable<TEntity> GetAll(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null) =>
        BuildQuery(condition, orderBy, options).ToList();

    /// <inheritdoc />
    public virtual IEnumerable<TResult> GetAll<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return Untracked(condition, orderBy, options).Select(selector).ToList();
    }

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TEntity>> GetAllAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default) =>
        await BuildQuery(condition, orderBy, options).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public virtual async Task<IEnumerable<TResult>> GetAllAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);

        return await Untracked(condition, orderBy, options).Select(selector).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public virtual Paged<TEntity> GetAllPaged(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null)
    {
        Paging.EnsureValid(pageIndex, pageSize);

        var query = BuildQuery(condition, orderBy ?? PageOrder, options);

        return Paging.ToPage(query.Count(), Page(query, pageIndex, pageSize).ToList(), pageIndex, pageSize);
    }

    /// <inheritdoc />
    public virtual Paged<TResult> GetAllPaged<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        Paging.EnsureValid(pageIndex, pageSize);

        var query = Untracked(condition, orderBy ?? PageOrder, options);

        return Paging.ToPage(query.Count(), Page(query.Select(selector), pageIndex, pageSize).ToList(), pageIndex, pageSize);
    }

    /// <inheritdoc />
    public virtual async Task<Paged<TEntity>> GetAllPagedAsync(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Paging.EnsureValid(pageIndex, pageSize);

        var query = BuildQuery(condition, orderBy ?? PageOrder, options);
        var totalCount = await query.CountAsync(cancellationToken);
        var results = await Page(query, pageIndex, pageSize).ToListAsync(cancellationToken);

        return Paging.ToPage(totalCount, results, pageIndex, pageSize);
    }

    /// <inheritdoc />
    public virtual async Task<Paged<TResult>> GetAllPagedAsync<TResult>(
        Expression<Func<TEntity, TResult>> selector,
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        int pageIndex = 1,
        int pageSize = 10,
        ReadOptions? options = null,
        CancellationToken cancellationToken = default)
        where TResult : class
    {
        ArgumentNullException.ThrowIfNull(selector);
        Paging.EnsureValid(pageIndex, pageSize);

        var query = Untracked(condition, orderBy ?? PageOrder, options);
        var totalCount = await query.CountAsync(cancellationToken);
        var results = await Page(query.Select(selector), pageIndex, pageSize).ToListAsync(cancellationToken);

        return Paging.ToPage(totalCount, results, pageIndex, pageSize);
    }

    /// <summary>
    /// Builds a queryable for <typeparamref name="TEntity"/> with the standard read pipeline applied.
    /// Use this in custom repository methods to avoid duplicating the option handling.
    /// </summary>
    /// <param name="condition">The filter, if any.</param>
    /// <param name="orderBy">The sort order, if any.</param>
    /// <param name="options">
    /// The read options, or <see langword="null"/> for <see cref="ReadOptions.Default"/>. Pass
    /// <see cref="EfReadOptions{TEntity}"/> for includes, tracking and split queries.
    /// </param>
    /// <returns>The query.</returns>
    protected IQueryable<TEntity> BuildQuery(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null) =>
        DbContext.Set<TEntity>().ApplyRead(condition, orderBy, options, context: DbContext);

    /// <summary>
    /// Builds a no-tracking query, for projections and aggregates.
    /// </summary>
    private IQueryable<TEntity> Untracked(
        Expression<Func<TEntity, bool>>? condition = null,
        SortOrder<TEntity>? orderBy = null,
        ReadOptions? options = null) =>
        DbContext.Set<TEntity>().ApplyRead(condition, orderBy, options, allowTracking: false, context: DbContext);

    private static Expression<Func<TEntity, bool>> ById(TId id) => e => id.Equals(e.Id);

    private static Expression<Func<TEntity, bool>> ByIds(ICollection<TId> ids) => e => ids.Contains(e.Id);

    private static IQueryable<T> Page<T>(IQueryable<T> query, int pageIndex, int pageSize) =>
        query.Skip(Paging.Offset(pageIndex, pageSize)).Take(pageSize);
}
