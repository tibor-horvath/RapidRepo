using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Entities;

/// <summary>
/// Represents a base entity with audit fields and soft-delete support, tracked by user.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <typeparam name="TUserKey">The type of the user identifier.</typeparam>
public class BaseAuditableDeletableEntity<TId, TUserKey> : BaseEntity<TId>, IAuditableDeletableEntity<TUserKey>
    where TId : notnull
    where TUserKey : struct
{
    /// <summary>
    /// Gets or sets the date and time when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the user who created the entity.
    /// </summary>
    public TUserKey CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the entity was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who last modified the entity.
    /// </summary>
    public TUserKey? ModifiedBy { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the entity was deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>
    /// Gets or sets the user who deleted the entity.
    /// </summary>
    public TUserKey? DeletedBy { get; set; }
}

/// <summary>
/// Represents a base entity with audit fields and soft-delete support.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public class BaseAuditableDeletableEntity<TId> : BaseEntity<TId>, IAuditableDeletableEntity
    where TId : notnull
{
    /// <summary>
    /// Gets or sets the date and time when the entity was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the date and time when the entity was last modified.
    /// </summary>
    public DateTime? ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the entity was deleted.
    /// </summary>
    public DateTime? DeletedAt { get; set; }
}
