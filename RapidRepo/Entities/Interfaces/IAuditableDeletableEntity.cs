namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an entity that can be marked as deleted and has auditing information.
/// </summary>
/// <typeparam name="TKey">The type of the user ID.</typeparam>
public interface IAuditableDeletableEntity<TKey> : IAuditableEntity<TKey>, IDeletableEntity<TKey>
    where TKey : struct
{
}

/// <summary>
/// Represents an entity that can be marked as deleted and has auditing information.
/// </summary>
public interface IAuditableDeletableEntity : IAuditableEntity, IDeletableEntity
{
}
