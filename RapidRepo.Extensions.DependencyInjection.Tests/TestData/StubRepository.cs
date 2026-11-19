using RapidRepo.Dtos;
using RapidRepo.Entities;
using RapidRepo.Repositories;
using RapidRepo.Repositories.Interfaces;
using System.Linq.Expressions;

namespace RapidRepo.Extensions.DependencyInjection.Tests.TestData;

/// <summary>
/// A repository with no persistence behind it. Registration tests only need types that implement the contracts and
/// can be constructed without a data source, which keeps this suite free of any provider.
/// </summary>
/// <remarks>Generated from the <see cref="IRepository{TEntity, TKey}"/> declarations; every member throws.</remarks>
public abstract class StubRepository<TEntity, TKey> : IRepository<TEntity, TKey>
    where TEntity : BaseEntity<TKey>
    where TKey : notnull
{
    public virtual bool ExistsById(TKey id, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Task<bool> ExistsByIdAsync(TKey id, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual bool Any(Expression<Func<TEntity, bool>> condition, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Task<bool> AnyAsync(Expression<Func<TEntity, bool>> condition, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual int Count(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Task<int> CountAsync(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual TEntity? GetById(TKey id, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual TResult? GetById<TResult>(TKey id, Expression<Func<TEntity, TResult>> selector, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Task<TEntity?> GetByIdAsync(TKey id, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<TResult?> GetByIdAsync<TResult>(TKey id, Expression<Func<TEntity, TResult>> selector, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual IEnumerable<TEntity> GetByIds(IEnumerable<TKey> ids, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual IEnumerable<TResult> GetByIds<TResult>(IEnumerable<TKey> ids, Expression<Func<TEntity, TResult>> selector, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Task<IEnumerable<TEntity>> GetByIdsAsync(IEnumerable<TKey> ids, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<IEnumerable<TResult>> GetByIdsAsync<TResult>(IEnumerable<TKey> ids, Expression<Func<TEntity, TResult>> selector, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual TEntity? GetFirstOrDefault(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual TResult? GetFirstOrDefault<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<TEntity?> GetFirstOrDefaultAsync(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<TResult?> GetFirstOrDefaultAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual TEntity GetFirst(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual TResult GetFirst<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<TEntity> GetFirstAsync(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<TResult> GetFirstAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual TEntity? GetSingleOrDefault(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual TResult? GetSingleOrDefault<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<TEntity?> GetSingleOrDefaultAsync(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<TResult?> GetSingleOrDefaultAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual TEntity GetSingle(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual TResult GetSingle<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<TEntity> GetSingleAsync(Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<TResult> GetSingleAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual IEnumerable<TEntity> GetAll(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual IEnumerable<TResult> GetAll<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<IEnumerable<TEntity>> GetAllAsync(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<IEnumerable<TResult>> GetAllAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual Paged<TEntity> GetAllPaged(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, int pageIndex = 1, int pageSize = 10, ReadOptions? options = null) => throw new NotSupportedException();

    public virtual Paged<TResult> GetAllPaged<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, int pageIndex = 1, int pageSize = 10, ReadOptions? options = null) where TResult : class => throw new NotSupportedException();

    public virtual Task<Paged<TEntity>> GetAllPagedAsync(Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, int pageIndex = 1, int pageSize = 10, ReadOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task<Paged<TResult>> GetAllPagedAsync<TResult>(Expression<Func<TEntity, TResult>> selector, Expression<Func<TEntity, bool>>? condition = null, SortOrder<TEntity>? orderBy = null, int pageIndex = 1, int pageSize = 10, ReadOptions? options = null, CancellationToken cancellationToken = default) where TResult : class => throw new NotSupportedException();

    public virtual void Add(TEntity entity) => throw new NotSupportedException();

    public virtual void AddRange(IEnumerable<TEntity> entities) => throw new NotSupportedException();

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual void DeleteById(TKey id) => throw new NotSupportedException();

    public virtual Task DeleteByIdAsync(TKey id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual void Delete(TEntity entity) => throw new NotSupportedException();

    public virtual void DeleteRange(IEnumerable<TEntity> entities) => throw new NotSupportedException();

    public virtual void Restore(TEntity entity) => throw new NotSupportedException();

    public virtual void RestoreRange(IEnumerable<TEntity> entities) => throw new NotSupportedException();

    public virtual void RestoreById(TKey id, bool includeDeleted = true) => throw new NotSupportedException();

    public virtual Task RestoreByIdAsync(TKey id, bool includeDeleted = true, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual void HardDelete(TEntity entity) => throw new NotSupportedException();

    public virtual void HardDeleteRange(IEnumerable<TEntity> entities) => throw new NotSupportedException();

    public virtual void HardDeleteById(TKey id, bool includeDeleted = false) => throw new NotSupportedException();

    public virtual Task HardDeleteByIdAsync(TKey id, bool includeDeleted = false, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public virtual void Update(TEntity entity) => throw new NotSupportedException();

    public virtual void UpdateRange(IEnumerable<TEntity> entities) => throw new NotSupportedException();
}
