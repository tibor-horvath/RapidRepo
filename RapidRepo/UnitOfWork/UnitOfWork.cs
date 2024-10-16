using Microsoft.EntityFrameworkCore;
using RapidRepo.Entities.Interfaces;

namespace RapidRepo.UnitOfWork;

public abstract class UnitOfWork<TUserId> : IUnitOfWork<TUserId>, IDisposable
    where TUserId : struct
{
    protected DbContext DbContext { get; set; }

    /// <summary>
    /// Gets the default user ID.
    /// </summary>
    public abstract TUserId DefaultUserId { get; }

    protected UnitOfWork(DbContext dbContext)
    {
        DbContext = dbContext;
    }

    public void Commit(TUserId? userId = null)
    {
        ExecuteAudit(userId);
        DbContext.SaveChanges();
    }

    public async Task CommitAsync(TUserId? userId = null, CancellationToken cancellationToken = default)
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
    private void ExecuteAudit(TUserId? userId = null)
    {
        var entities = DbContext.ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified)
            .Select(e => e.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            if (entity is IAuditableEntity<TUserId> auditableEntityWithUserInfo)
            {
                auditableEntityWithUserInfo.CreatedAt = DateTime.UtcNow;
                auditableEntityWithUserInfo.CreatedBy = userId ?? DefaultUserId;
            }

            if (entity is IAuditableEntity auditableEntity)
            {
                auditableEntity.CreatedAt = DateTime.UtcNow;
            }

            if (entity is IDeletableEntity softDeletableEntity && softDeletableEntity.DeletedAt != null)
            {
                softDeletableEntity.DeletedAt = DateTime.UtcNow;
            }

            if (entity is IDeletableEntity<TUserId> softDeletableEntityWithUserInfo && softDeletableEntityWithUserInfo.DeletedAt != null)
            {
                softDeletableEntityWithUserInfo.DeletedAt = DateTime.UtcNow;
                softDeletableEntityWithUserInfo.DeletedBy = userId ?? DefaultUserId;
            }
        }
    }
}
