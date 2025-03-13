using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities.Interfaces;

namespace RapidRepo.UnitOfWork;

public abstract class UnitOfWork<TUserKey> : IUnitOfWork<TUserKey>, IDisposable
    where TUserKey : struct
{
    protected DbContext DbContext { get; set; }

    /// <summary>
    /// Gets the default user ID.
    /// </summary>
    public abstract TUserKey DefaultUserKey { get; }

    protected UnitOfWork(DbContext dbContext)
    {
        DbContext = dbContext;
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
        var defaultUserKey = DefaultUserKey;

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
