using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore;

public abstract class EfUnitOfWork<TUserKey> : IUnitOfWork<TUserKey>, IDisposable
    where TUserKey : struct
{
    private readonly TUserKey _defaultUserKey;
    private readonly IServiceProvider? _serviceProvider;

    protected DbContext DbContext { get; set; }

    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey)
    {
        DbContext = dbContext;
        _defaultUserKey = defaultUserKey;
    }

    protected EfUnitOfWork(DbContext dbContext, TUserKey defaultUserKey, IServiceProvider serviceProvider)
        : this(dbContext, defaultUserKey)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        _serviceProvider = serviceProvider;
    }

    protected TRepo ResolveRepository<TRepo>() where TRepo : class
    {
        if (_serviceProvider is null)
        {
            throw new InvalidOperationException(
                $"ResolveRepository<{typeof(TRepo).Name}>() requires an IServiceProvider. " +
                "Pass IServiceProvider to the unit of work constructor.");
        }

        return (TRepo?)_serviceProvider.GetService(typeof(TRepo))
            ?? throw new InvalidOperationException(
                $"No service of type '{typeof(TRepo).Name}' is registered.");
    }

    public void Commit(TUserKey? userId = null)
    {
        ExecuteAudit(userId);
        DbContext.SaveChanges();
    }

    public async Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default)
    {
        ExecuteAudit(userId);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    protected IRepository<TEntity, TKey> GetRepository<TEntity, TKey>()
        where TEntity : BaseEntity<TKey>
        where TKey : notnull
        => new EfRepository<TEntity, TKey>(DbContext);

    public void Dispose()
    {
        DbContext.Dispose();
    }

    /// <summary>
    /// Executes the audit process for the entities being added or modified.
    /// </summary>
    /// <param name="userId">The optional user ID to associate with the audit.</param>
    private void ExecuteAudit(TUserKey? userId = null)
    {
        var utcNow = DateTime.UtcNow;
        var defaultUserKey = _defaultUserKey;

        foreach (var entry in DbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified))
        {
            var entity = entry.Entity;
            var state = entry.State;

            if (entity is IAuditableEntity<TUserKey> auditableEntityWithUserInfo)
            {
                if (state == EntityState.Added)
                {
                    auditableEntityWithUserInfo.CreatedAt = utcNow;
                    auditableEntityWithUserInfo.CreatedBy = userId ?? defaultUserKey;
                }
                else if (state == EntityState.Modified)
                {
                    auditableEntityWithUserInfo.ModifiedAt = utcNow;
                    auditableEntityWithUserInfo.ModifiedBy = userId ?? defaultUserKey;
                }
            }
            else if (entity is IAuditableEntity auditableEntity)
            {
                if (state == EntityState.Added)
                {
                    auditableEntity.CreatedAt = utcNow;
                }
                else if (state == EntityState.Modified)
                {
                    auditableEntity.ModifiedAt = utcNow;
                }
            }

            if (entity is IDeletableEntity<TUserKey> softDeletableEntityWithUserInfo && softDeletableEntityWithUserInfo.DeletedAt != null)
            {
                softDeletableEntityWithUserInfo.DeletedAt = utcNow;
                softDeletableEntityWithUserInfo.DeletedBy = userId ?? defaultUserKey;
            }
            else if (entity is IDeletableEntity softDeletableEntity && softDeletableEntity.DeletedAt != null)
            {
                softDeletableEntity.DeletedAt = utcNow;
            }
        }
    }
}
