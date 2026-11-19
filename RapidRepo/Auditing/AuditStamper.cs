using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Auditing;

/// <summary>
/// Stamps the audit fields of an entity for a given <see cref="EntityChangeKind"/>. Providers call it at commit
/// time, so the rules live in one place whatever the persistence technology.
/// </summary>
/// <remarks>
/// <list type="bullet">
/// <item><see cref="EntityChangeKind.Added"/> sets <c>CreatedAt</c> and <c>CreatedBy</c>.</item>
/// <item>
/// <see cref="EntityChangeKind.Modified"/>, <see cref="EntityChangeKind.SoftDeleted"/> and
/// <see cref="EntityChangeKind.Restored"/> set <c>ModifiedAt</c> and <c>ModifiedBy</c>.
/// </item>
/// <item>
/// <see cref="EntityChangeKind.SoftDeleted"/> also sets <c>DeletedAt</c> and <c>DeletedBy</c>. Only that
/// transition does, so editing an entity that is already deleted keeps who deleted it and when.
/// </item>
/// <item><see cref="EntityChangeKind.Restored"/> also clears <c>DeletedAt</c> and <c>DeletedBy</c>.</item>
/// </list>
/// User fields are stamped only on entities whose user key type is <typeparamref name="TUserKey"/>.
/// </remarks>
/// <typeparam name="TUserKey">The type of the user key.</typeparam>
/// <param name="defaultUserKey">The user recorded when a commit names none.</param>
/// <param name="timeProvider">The clock, or <see langword="null"/> for <see cref="TimeProvider.System"/>.</param>
public sealed class AuditStamper<TUserKey>(TUserKey defaultUserKey, TimeProvider? timeProvider = null)
    where TUserKey : struct
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>
    /// Gets the user recorded when a commit names none.
    /// </summary>
    public TUserKey DefaultUserKey { get; } = defaultUserKey;

    /// <summary>
    /// Gets the current UTC time from the configured clock.
    /// </summary>
    public DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;

    /// <summary>
    /// Stamps the audit fields of <paramref name="entity"/> for <paramref name="kind"/> at the current time.
    /// Entities that are neither auditable nor deletable are left untouched.
    /// </summary>
    /// <param name="entity">The entity being committed.</param>
    /// <param name="kind">The kind of change.</param>
    /// <param name="userId">The user making the change, or <see langword="null"/> for <see cref="DefaultUserKey"/>.</param>
    public void Stamp(object entity, EntityChangeKind kind, TUserKey? userId = null) =>
        Stamp(entity, kind, userId, UtcNow);

    /// <summary>
    /// Stamps the audit fields of <paramref name="entity"/> for <paramref name="kind"/> at <paramref name="utcNow"/>,
    /// so that every entity in one commit carries the same timestamp.
    /// </summary>
    /// <param name="entity">The entity being committed.</param>
    /// <param name="kind">The kind of change.</param>
    /// <param name="userId">The user making the change, or <see langword="null"/> for <see cref="DefaultUserKey"/>.</param>
    /// <param name="utcNow">The commit time.</param>
    public void Stamp(object entity, EntityChangeKind kind, TUserKey? userId, DateTime utcNow)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var user = userId ?? DefaultUserKey;

        if (kind == EntityChangeKind.Added)
        {
            if (entity is IAuditableEntity auditable)
                auditable.CreatedAt = utcNow;

            if (entity is IAuditableEntity<TUserKey> auditableByUser)
                auditableByUser.CreatedBy = user;

            return;
        }

        if (entity is IAuditableEntity modified)
            modified.ModifiedAt = utcNow;

        if (entity is IAuditableEntity<TUserKey> modifiedByUser)
            modifiedByUser.ModifiedBy = user;

        switch (kind)
        {
            case EntityChangeKind.SoftDeleted:
                if (entity is IDeletableEntity deleted)
                    deleted.DeletedAt = utcNow;

                if (entity is IDeletableEntity<TUserKey> deletedByUser)
                    deletedByUser.DeletedBy = user;
                break;

            case EntityChangeKind.Restored:
                if (entity is IDeletableEntity restored)
                    restored.DeletedAt = null;

                if (entity is IDeletableEntity<TUserKey> restoredByUser)
                    restoredByUser.DeletedBy = null;
                break;
        }
    }
}
