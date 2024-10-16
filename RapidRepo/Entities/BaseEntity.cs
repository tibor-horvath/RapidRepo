namespace RapidRepo.Entities;

/// <summary>
/// Represents the base entity with an ID.
/// </summary>
/// <typeparam name="TId">The type of the ID.</typeparam>
public abstract class BaseEntity<TId>
    where TId : struct
{
    /// <summary>
    /// Gets or sets the ID of the entity.
    /// </summary>
    public TId Id { get; set; }
}
