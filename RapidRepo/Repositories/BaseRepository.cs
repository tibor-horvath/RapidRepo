using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;

namespace RapidRepo.Repositories;

public abstract class BaseRepository<TEntity, TId> : ReadOnlyRepository<TEntity, TId>, IRepository<TEntity, TId>
    where TEntity : BaseEntity<TId>
    where TId : struct
{
    private readonly IWriteRepository<TEntity, TId> _writeRepository;

    public BaseRepository(
        DbContext dbContext)
        : base(dbContext)
    {
        _writeRepository = new BaseWriteRepository<TEntity, TId>(dbContext);
    }

    public virtual void Add(TEntity entity) => _writeRepository.Add(entity);

    public virtual void AddRange(IEnumerable<TEntity> entities) => _writeRepository.AddRange(entities);

    public virtual Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) =>
        _writeRepository.AddAsync(entity, cancellationToken);

    public virtual Task AddAsync(IEnumerable<TEntity> entities, CancellationToken cancellationToken = default) =>
        _writeRepository.AddAsync(entities, cancellationToken);

    public virtual void Delete(TEntity entity) => _writeRepository.Delete(entity);

    public virtual void DeleteRange(IEnumerable<TEntity> entities) => _writeRepository.DeleteRange(entities);

    public virtual void Update(TEntity entity) => _writeRepository.Update(entity);

    public virtual void DeleteById(TId id) => _writeRepository.DeleteById(id);

    public virtual void UpdateRange(IEnumerable<TEntity> entities) => _writeRepository.UpdateRange(entities);
}
