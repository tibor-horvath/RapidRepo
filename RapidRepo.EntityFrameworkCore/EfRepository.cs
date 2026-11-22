using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// An EF Core repository with both read and write operations over <c>DbContext.Set&lt;TEntity&gt;()</c>.
/// </summary>
/// <remarks>
/// Usable as-is, for example as the open-generic fallback registration, and as the base class for custom repositories.
/// </remarks>
/// <typeparam name="TEntity">The type of the entity managed by the repository.</typeparam>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public class EfRepository<TEntity, TId> : EfReadOnlyRepository<TEntity, TId>, IEfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
{
    private readonly WriteRepository _writeRepository;

    /// <summary>Creates a repository that reads from and stages changes in <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context the repository reads from and stages changes in.</param>
    public EfRepository(DbContext dbContext)
        : base(dbContext)
    {
        _writeRepository = new WriteRepository(this, dbContext);
    }

    /// <inheritdoc />
    public virtual void Add(TEntity entity) => _writeRepository.Add(entity);

    /// <inheritdoc />
    public virtual void AddRange(IEnumerable<TEntity> entities) => _writeRepository.AddRange(entities);

    /// <inheritdoc />
    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _writeRepository.AddAsync(entity, cancellationToken);

    /// <inheritdoc />
    public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _writeRepository.AddRangeAsync(entities, cancellationToken);

    /// <inheritdoc />
    public virtual void Delete(TEntity entity) => _writeRepository.BaseDelete(entity);

    /// <inheritdoc />
    public virtual void DeleteRange(IEnumerable<TEntity> entities) => _writeRepository.DeleteRange(entities);

    /// <inheritdoc />
    public virtual void Restore(TEntity entity) => _writeRepository.BaseRestore(entity);

    /// <inheritdoc />
    public virtual void RestoreRange(IEnumerable<TEntity> entities) => _writeRepository.RestoreRange(entities);

    /// <inheritdoc />
    public virtual void RestoreById(TId id, bool includeDeleted = true) =>
        _writeRepository.RestoreById(id, includeDeleted);

    /// <inheritdoc />
    public virtual Task RestoreByIdAsync(
        TId id,
        bool includeDeleted = true,
        CancellationToken cancellationToken = default) =>
        _writeRepository.RestoreByIdAsync(id, includeDeleted, cancellationToken);

    /// <inheritdoc />
    public virtual void HardDelete(TEntity entity) => _writeRepository.BaseHardDelete(entity);

    /// <inheritdoc />
    public virtual void HardDeleteRange(IEnumerable<TEntity> entities) => _writeRepository.BaseHardDeleteRange(entities);

    /// <inheritdoc />
    public virtual void HardDeleteById(TId id, bool includeDeleted = false) =>
        _writeRepository.HardDeleteById(id, includeDeleted);

    /// <inheritdoc />
    public virtual Task HardDeleteByIdAsync(
        TId id,
        bool includeDeleted = false,
        CancellationToken cancellationToken = default) =>
        _writeRepository.HardDeleteByIdAsync(id, includeDeleted, cancellationToken);

    /// <inheritdoc />
    public virtual void Update(TEntity entity) => _writeRepository.Update(entity);

    /// <inheritdoc />
    public virtual void DeleteById(TId id) => _writeRepository.DeleteById(id);

    /// <inheritdoc />
    public virtual Task DeleteByIdAsync(TId id, CancellationToken cancellationToken = default) =>
        _writeRepository.DeleteByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public virtual void UpdateRange(IEnumerable<TEntity> entities) => _writeRepository.UpdateRange(entities);

    /// <summary>
    /// The write side this repository delegates to; C# has no multiple inheritance from the two abstract bases.
    /// </summary>
    /// <remarks>
    /// The write rules call some operations from others: <c>DeleteById</c> calls <c>Delete</c>, <c>Delete</c> of an
    /// entity without soft delete calls <c>HardDelete</c>, and so on. Those calls are routed back through
    /// <paramref name="owner"/>, so that a repository overriding <see cref="EfRepository{TEntity, TId}.HardDelete"/>
    /// sees every hard delete, not only direct calls. The owner's default implementations call the
    /// <c>Base*</c> members, which run the rules themselves.
    /// </remarks>
    private sealed class WriteRepository(EfRepository<TEntity, TId> owner, DbContext dbContext)
        : EfWriteRepository<TEntity, TId>(dbContext)
    {
        public override void Delete(TEntity entity) => owner.Delete(entity);

        public override void Restore(TEntity entity) => owner.Restore(entity);

        public override void HardDelete(TEntity entity) => owner.HardDelete(entity);

        public override void HardDeleteRange(IEnumerable<TEntity> entities) => owner.HardDeleteRange(entities);

        internal void BaseDelete(TEntity entity) => base.Delete(entity);

        internal void BaseRestore(TEntity entity) => base.Restore(entity);

        internal void BaseHardDelete(TEntity entity) => base.HardDelete(entity);

        internal void BaseHardDeleteRange(IEnumerable<TEntity> entities) => base.HardDeleteRange(entities);
    }
}

/// <summary>
/// An <see cref="EfRepository{TEntity, TId}"/> bound to a specific <typeparamref name="TContext"/>.
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
public sealed class EfRepository<TEntity, TId, TContext> : EfRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : notnull
    where TContext : DbContext
{
    /// <summary>Creates a repository that reads from and stages changes in <paramref name="dbContext"/>.</summary>
    /// <param name="dbContext">The context the repository reads from and stages changes in.</param>
    public EfRepository(TContext dbContext) : base(dbContext) { }
}
