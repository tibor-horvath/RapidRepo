using RapidRepo.Entities.Interfaces;

namespace RapidRepo.Auditing;

/// <summary>
/// Decides which <see cref="EntityChangeKind"/> a staged update of an existing entity is, so that every provider
/// stamps soft deletes and restores the same way.
/// </summary>
/// <remarks>
/// A provider calls <see cref="ClassifyUpdate"/> at commit time for each entity it is about to update (not for
/// inserts, which are always <see cref="EntityChangeKind.Added"/>, nor for hard deletes, which are not stamped). It
/// passes the <c>DeletedAt</c> value the entity had when the provider first saw it: a change-tracking provider
/// reads its original value, and a command-based provider records it when the change is staged, before
/// <c>StageDeletionChange</c> applies the change.
/// </remarks>
public static class EntityChangeClassifier
{
    /// <summary>
    /// Classifies an update by comparing the entity's original and current deletion marks.
    /// </summary>
    /// <param name="entity">The entity being updated.</param>
    /// <param name="originalDeletedAt">
    /// The entity's <c>DeletedAt</c> before the change. Ignored when the entity does not implement
    /// <see cref="IDeletableEntity"/>.
    /// </param>
    /// <returns>
    /// <see cref="EntityChangeKind.SoftDeleted"/> on the transition from not deleted to deleted,
    /// <see cref="EntityChangeKind.Restored"/> on the transition back, and <see cref="EntityChangeKind.Modified"/>
    /// otherwise, so an edit to an already deleted entity keeps its deletion stamp.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="entity"/> is <see langword="null"/>.</exception>
    public static EntityChangeKind ClassifyUpdate(object entity, DateTime? originalDeletedAt)
    {
        ArgumentNullException.ThrowIfNull(entity);

        if (entity is not IDeletableEntity deletable)
            return EntityChangeKind.Modified;

        return (originalDeletedAt is not null, deletable.DeletedAt is not null) switch
        {
            (false, true) => EntityChangeKind.SoftDeleted,
            (true, false) => EntityChangeKind.Restored,
            _ => EntityChangeKind.Modified,
        };
    }
}
