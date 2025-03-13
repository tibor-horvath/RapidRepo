namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an entity that can be marked as deleted and has auditing information.
/// </summary>
/// <typeparam name="TUserKey">The type of the user ID.</typeparam>
public interface IAuditableDeletableEntity<TUserKey> : IAuditableEntity<TUserKey>, IDeletableEntity<TUserKey>
    where TUserKey : struct
{
}

/// <summary>
/// Represents an entity that can be marked as deleted and has auditing information.
/// </summary>
public interface IAuditableDeletableEntity : IAuditableEntity, IDeletableEntity
{
}
