using RapidRepo.Auditing;
using RapidRepo.Entities.Interfaces;
using System.ComponentModel;

namespace RapidRepo.UnitOfWork;

/// <summary>
/// How a staged change will be written to the store.
/// </summary>
public enum StagedChangeState
{
    /// <summary>The entity will be inserted.</summary>
    Added,

    /// <summary>The entity will be updated, including by a soft delete or a restore.</summary>
    Modified,

    /// <summary>The entity's row will be removed (a hard delete).</summary>
    Deleted,
}

/// <summary>
/// One change a provider has staged for the next commit, as the commit sequence of
/// <see cref="UnitOfWorkBase{TUserKey}"/> sees it: the entity, how it will be written, and the provider's means to
/// make the store check an entity's version.
/// </summary>
/// <remarks>
/// A provider derives from this class and returns its changes from
/// <see cref="UnitOfWorkBase{TUserKey}.GetStagedChanges"/>. The commit sequence stamps audit fields from
/// <see cref="State"/> and <see cref="TryGetOriginalDeletedAt"/>, and for every <see cref="IVersionedEntity"/>
/// increments the version and calls <see cref="ExpectVersion"/>.
/// </remarks>
[EditorBrowsable(EditorBrowsableState.Advanced)]
public abstract class StagedChange
{
    /// <summary>Creates a staged change.</summary>
    /// <param name="entity">The entity being written.</param>
    /// <param name="state">How it will be written.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    protected StagedChange(object entity, StagedChangeState state)
    {
        Entity = entity ?? throw new ArgumentNullException(nameof(entity));
        State = state;
    }

    /// <summary>Gets the entity being written.</summary>
    public object Entity { get; }

    /// <summary>Gets how the entity will be written.</summary>
    public StagedChangeState State { get; }

    /// <summary>
    /// Gets the <c>DeletedAt</c> value a <see cref="StagedChangeState.Modified"/> entity had before the change, so that
    /// a soft delete or restore is told apart from an ordinary edit (see <see cref="EntityChangeClassifier"/>).
    /// </summary>
    /// <remarks>
    /// A change-tracking provider reads the original value; a command-based provider records it when the change is
    /// staged. Return <see langword="false"/> when the store does not hold <c>DeletedAt</c>; the change is then
    /// stamped as an ordinary edit.
    /// </remarks>
    /// <param name="originalDeletedAt">The original value, when known.</param>
    /// <returns><see langword="true"/> when the original value is known.</returns>
    protected internal virtual bool TryGetOriginalDeletedAt(out DateTime? originalDeletedAt)
    {
        originalDeletedAt = null;
        return false;
    }

    /// <summary>
    /// Makes the store write this change only where the stored version still equals
    /// <paramref name="expectedVersion"/>, and report a conflict otherwise. Called for every
    /// <see cref="StagedChangeState.Modified"/> or <see cref="StagedChangeState.Deleted"/>
    /// <see cref="IVersionedEntity"/>, after the version of a modified one was incremented.
    /// </summary>
    /// <param name="expectedVersion">The version the store must still hold.</param>
    /// <exception cref="NotSupportedException">Always, unless overridden: a version that is never checked would let conflicting writes be lost silently.</exception>
    protected internal virtual void ExpectVersion(long expectedVersion) =>
        throw new NotSupportedException(
            $"'{Entity.GetType().Name}' implements {nameof(IVersionedEntity)}, but the provider's " +
            $"'{GetType().Name}' does not check versions. Override {nameof(ExpectVersion)}, or don't use " +
            $"{nameof(IVersionedEntity)} with this provider.");

    /// <summary>
    /// Undoes <see cref="ExpectVersion"/> after a failed commit, so that a retried commit checks the version again.
    /// The commit sequence restores the entity's <c>Version</c> itself.
    /// </summary>
    protected internal virtual void RevertExpectedVersion()
    {
    }

    /// <summary>
    /// The kind of audit stamp, or <see langword="null"/> for a hard delete, which is not stamped.
    /// </summary>
    internal EntityChangeKind? GetAuditKind() => State switch
    {
        StagedChangeState.Added => EntityChangeKind.Added,
        StagedChangeState.Modified when TryGetOriginalDeletedAt(out var originalDeletedAt) =>
            EntityChangeClassifier.ClassifyUpdate(Entity, originalDeletedAt),
        StagedChangeState.Modified => EntityChangeKind.Modified,
        _ => null,
    };
}
