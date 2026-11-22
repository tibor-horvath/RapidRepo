using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Entities;

/// <summary>
/// Represents a base entity protected by optimistic concurrency. See <see cref="IVersionedEntity"/>.
/// </summary>
/// <remarks>
/// To combine concurrency with auditing or soft delete, derive from the auditable base class and implement
/// <see cref="IVersionedEntity"/>, which takes a single property.
/// </remarks>
/// <typeparam name="TId">The type of the entity's identifier.</typeparam>
public class BaseVersionedEntity<TId> : BaseEntity<TId>, IVersionedEntity
    where TId : notnull
{
    /// <inheritdoc />
    public long Version { get; set; }
}
