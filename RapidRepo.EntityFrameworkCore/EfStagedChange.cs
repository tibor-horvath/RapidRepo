using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using RapidRepo.Entities.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore;

/// <summary>
/// A change tracker entry as the core commit sequence sees it. The version check is EF Core's concurrency token:
/// the version the store must still hold becomes the property's original value, which EF Core puts in the
/// <c>WHERE</c> clause.
/// </summary>
internal sealed class EfStagedChange : StagedChange
{
    private readonly EntityEntry _entry;
    private readonly PropertyEntry? _version;
    private long? _previousOriginal;

    private EfStagedChange(EntityEntry entry, StagedChangeState state)
        : base(entry.Entity, state)
    {
        _entry = entry;

        // Looked up for inserts too, so a missing concurrency token fails on the entity's first commit.
        if (entry.Entity is IVersionedEntity)
            _version = entry.Property(EfConcurrency.GetVersionProperty(entry.Metadata));
    }

    /// <summary>
    /// Reads every added, modified or deleted entry of <paramref name="context"/>. Reading the entries detects
    /// changes, so the list includes edits made to tracked entities without calling <c>Update</c>.
    /// </summary>
    public static IReadOnlyList<StagedChange> From(DbContext context)
    {
        var changes = new List<StagedChange>();

        foreach (var entry in context.ChangeTracker.Entries())
        {
            StagedChangeState? state = entry.State switch
            {
                EntityState.Added => StagedChangeState.Added,
                EntityState.Modified => StagedChangeState.Modified,
                EntityState.Deleted => StagedChangeState.Deleted,
                _ => null,
            };

            if (state is { } staged)
                changes.Add(new EfStagedChange(entry, staged));
        }

        return changes;
    }

    /// <remarks>
    /// Read from the property's original value, when <c>DeletedAt</c> is mapped. Only the transition counts, so
    /// saving an already deleted entity again does not overwrite who deleted it and when.
    /// </remarks>
    protected override bool TryGetOriginalDeletedAt(out DateTime? originalDeletedAt)
    {
        if (Entity is IDeletableEntity && _entry.Metadata.FindProperty(nameof(IDeletableEntity.DeletedAt)) is not null)
        {
            originalDeletedAt = (DateTime?)_entry.Property(nameof(IDeletableEntity.DeletedAt)).OriginalValue;
            return true;
        }

        originalDeletedAt = null;
        return false;
    }

    /// <remarks>
    /// The expected version becomes the property's original value. That covers both a tracked entity and a detached
    /// one passed to <c>Update</c> with the version a client sent back. For an update, the incremented version is
    /// also set as the current value, so EF Core writes it.
    /// </remarks>
    protected override void ExpectVersion(long expectedVersion)
    {
        var version = _version!;

        _previousOriginal = (long)version.OriginalValue!;
        version.OriginalValue = expectedVersion;

        if (State == StagedChangeState.Modified)
            version.CurrentValue = ((IVersionedEntity)Entity).Version;
    }

    /// <inheritdoc />
    protected override void RevertExpectedVersion()
    {
        if (_previousOriginal is not { } previous)
            return;

        _version!.OriginalValue = previous;
        _previousOriginal = null;
    }
}
