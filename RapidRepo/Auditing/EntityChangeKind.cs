namespace RapidRepo.Auditing;

/// <summary>
/// The kind of change a commit applies to an entity, which decides the audit fields it stamps.
/// </summary>
public enum EntityChangeKind
{
    /// <summary>The entity is inserted.</summary>
    Added,

    /// <summary>The entity is updated, other than by a soft delete or a restore.</summary>
    Modified,

    /// <summary>The entity transitions from not deleted to soft-deleted.</summary>
    SoftDeleted,

    /// <summary>The entity transitions from soft-deleted to not deleted.</summary>
    Restored,
}
