namespace RapidRepo.Entities.Interfaces;

/// <summary>
/// Represents an entity protected by optimistic concurrency: a commit that would overwrite or delete a row changed
/// by someone else since it was read fails with <see cref="Concurrency.ConcurrencyException"/> instead of silently
/// losing that change.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Version"/> is managed by the library. At commit, the unit of work takes the entity's current
/// <see cref="Version"/> as the version the store must still hold, and increments it for every update, including a
/// soft delete and a restore. An update or delete whose expected version no longer matches the stored one is a
/// conflict.
/// </para>
/// <para>
/// For a detached update (an entity built from a request), copy the version the client last saw onto the entity
/// before calling <c>Update</c>, the same way an HTTP <c>ETag</c> is sent back in <c>If-Match</c>.
/// </para>
/// </remarks>
public interface IVersionedEntity
{
    /// <summary>
    /// Gets or sets the concurrency token: the version the entity was read at. Treat it as opaque, return it to
    /// clients with the entity and take it back with their changes, but do not increment it yourself.
    /// </summary>
    long Version { get; set; }
}
