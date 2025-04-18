﻿using RapidRepo.Entities;
using RapidRepo.Entities.Interfaces;

namespace Repository.Entities;

/// <summary>
/// Represents a base auditable entity with common audit fields.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
/// <typeparam name="TUserKey">The type of the user identifier.</typeparam>
public class BaseAuditableEntity<TId, TUserKey> : BaseEntity<TId>, IAuditableEntity<TUserKey>
    where TId : struct
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
}

/// <summary>
/// Represents a base auditable entity with common audit fields.
/// </summary>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public class BaseAuditableEntity<TId> : BaseEntity<TId>, IAuditableEntity
    where TId : struct
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
