namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an entity that can be marked as deleted.
/// </summary>
/// <typeparam name="TUserKey">The type of the user ID.</typeparam>
public interface IDeletableEntity<TUserKey> : IDeletableEntity
    where TUserKey : struct
{
    /// <summary>
    /// Gets or sets the ID of the user who deleted the entity.
    /// </summary>
    TUserKey? DeletedBy { get; set; }
}

/// <summary>
/// Represents an entity that can be marked as deleted.
/// </summary>
public interface IDeletableEntity
{
    /// <summary>
    /// Gets or sets the date and time when the entity was deleted.
    /// </summary>
    DateTime? DeletedAt { get; set; }
}
