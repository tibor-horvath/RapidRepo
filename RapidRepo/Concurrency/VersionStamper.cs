using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Concurrency;

/// <summary>
/// Increments the <see cref="IVersionedEntity.Version"/> of an updated entity at commit time, so that every
/// provider applies the same optimistic concurrency rule.
/// </summary>
/// <remarks>
/// <para>A provider, for each staged change of an <see cref="IVersionedEntity"/>:</para>
/// <list type="bullet">
/// <item>
/// Update (a modification, soft delete or restore): calls <see cref="Stamp"/>, and writes the row only where the
/// stored version still equals the returned expected version.
/// </item>
/// <item>Hard delete: deletes the row only where the stored version still equals the entity's <c>Version</c>.</item>
/// <item>Insert: writes the entity's <c>Version</c> as it is.</item>
/// </list>
/// <para>
/// When no row matches, the provider throws <see cref="ConcurrencyException"/> and persists nothing from the commit.
/// If the commit fails for any reason, it calls <see cref="Revert"/> for every stamped entity, so that the entities
/// carry the version they were read at again.
/// </para>
/// </remarks>
public static class VersionStamper
{
    /// <summary>
    /// Increments the version of an updated entity.
    /// </summary>
    /// <param name="entity">The entity being updated.</param>
    /// <returns>The version the store must still hold: the entity's <c>Version</c> before the increment.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public static long Stamp(IVersionedEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var expected = entity.Version;
        entity.Version = unchecked(expected + 1);
        return expected;
    }

    /// <summary>
    /// Undoes <see cref="Stamp"/> after a failed commit, so that the entity carries the version it was read at.
    /// </summary>
    /// <param name="entity">The entity that was stamped.</param>
    /// <param name="expectedVersion">The value <see cref="Stamp"/> returned for it.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public static void Revert(IVersionedEntity entity, long expectedVersion)
    {
        ArgumentNullException.ThrowIfNull(entity);

        entity.Version = expectedVersion;
    }
}
