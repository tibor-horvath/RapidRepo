namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an auditable entity with properties for tracking creation and modification information.
/// </summary>
/// <typeparam name="TUserKey">The type of the user ID.</typeparam>
public interface IAuditableEntity<TUserKey> : IAuditableEntity
    where TUserKey : struct
{
    /// <summary>
    /// Gets or sets the user ID of the creator of the entity.
    /// </summary>
    TUserKey CreatedBy { get; set; }

    /// <summary>
    /// Gets or sets the user ID of the last modifier of the entity.
    /// </summary>
    TUserKey? ModifiedBy { get; set; }
}

/// <summary>
/// Represents an auditable entity with properties for tracking creation and modification information.
/// </summary>
public interface IAuditableEntity
{
    /// <summary>
    /// Gets or sets the date and time when the entity was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// Gets or sets the date and time when the entity was last modified.
    /// </summary>
    DateTime? ModifiedAt { get; set; }
}
