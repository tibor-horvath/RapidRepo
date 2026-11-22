using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using RapidRepo.Concurrency;
using RapidRepo.Entities.Interfaces;
using RapidRepo.Extensions;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// Applies optimistic concurrency to one EF Core commit: sets up the version check of every updated or deleted
/// <see cref="IVersionedEntity"/>, undoes it if the commit fails, and translates EF Core's conflict.
/// </summary>
internal sealed class EfVersionTracker
{
    private readonly List<StampedVersion> _stamped = [];

    /// <summary>
    /// Makes EF Core check the version the entity was read at, and increments it for an update.
    /// </summary>
    /// <remarks>
    /// The entity's current <c>Version</c> is the version the store must still hold, so it becomes the property's
    /// original value, which EF Core puts in the <c>WHERE</c> clause. That covers both a tracked entity and a detached
    /// one passed to <c>Update</c> with the version a client sent back. An insert keeps its version as it is.
    /// </remarks>
    public void Stamp(EntityEntry entry)
    {
        if (entry.Entity is not IVersionedEntity entity
            || entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
        {
            return;
        }

        // Looked up for inserts too, so a missing concurrency token fails on the entity's first commit.
        var version = entry.Property(GetVersionProperty(entry.Metadata));

        switch (entry.State)
        {
            case EntityState.Modified:
                Expect(entity, version, VersionStamper.Stamp(entity));
                version.CurrentValue = entity.Version;
                break;

            case EntityState.Deleted:
                Expect(entity, version, entity.Version);
                break;
        }
    }

    /// <summary>
    /// Restores the version and its original value on every stamped entry, so that the entities carry the version
    /// they were read at and a retried commit checks against it again.
    /// </summary>
    public void Revert()
    {
        foreach (var stamped in _stamped)
        {
            VersionStamper.Revert(stamped.Entity, stamped.Expected);
            stamped.Version.OriginalValue = stamped.PreviousOriginal;
        }

        _stamped.Clear();
    }

    /// <summary>
    /// Translates EF Core's conflict into the provider-neutral <see cref="ConcurrencyException"/> when it concerns an
    /// <see cref="IVersionedEntity"/>.
    /// </summary>
    /// <remarks>
    /// A conflict that involves only other entities, for example an update of a row that was deleted, or a
    /// concurrency token the application configured itself, is left as EF Core's
    /// <see cref="DbUpdateConcurrencyException"/>, as before entities could opt in.
    /// </remarks>
    public static ConcurrencyException? Translate(Exception exception) =>
        exception is DbUpdateConcurrencyException concurrency
        && concurrency.Entries.Any(e => e.Entity is IVersionedEntity)
            ? new ConcurrencyException(concurrency.Entries.Select(e => e.Entity), concurrency)
            : null;

    private void Expect(IVersionedEntity entity, PropertyEntry version, long expected)
    {
        _stamped.Add(new StampedVersion(entity, version, expected, (long)version.OriginalValue!));
        version.OriginalValue = expected;
    }

    /// <summary>
    /// Finds the <c>Version</c> property and checks that EF Core compares it on update and delete. Without that, the
    /// version would be incremented but never checked, and conflicting writes would be lost silently.
    /// </summary>
    private static IProperty GetVersionProperty(IEntityType entityType)
    {
        var property = entityType.FindProperty(nameof(IVersionedEntity.Version));
        if (property is { IsConcurrencyToken: true })
            return property;

        throw new InvalidOperationException(
            $"'{entityType.ClrType.Name}' implements '{nameof(IVersionedEntity)}', but its " +
            $"'{nameof(IVersionedEntity.Version)}' property is not mapped as a concurrency token, so conflicting " +
            $"changes would be lost silently. Call configurationBuilder." +
            $"{nameof(ModelConfigurationBuilderExtensions.AddConcurrencyTokens)}() in ConfigureConventions.");
    }

    private sealed record StampedVersion(IVersionedEntity Entity, PropertyEntry Version, long Expected, long PreviousOriginal);
}
