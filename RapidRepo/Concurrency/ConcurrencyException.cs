namespace RapidRepo.Concurrency;

/// <summary>
/// The exception a commit throws when it would overwrite or delete a row of an
/// <see cref="Entities.Interfaces.IVersionedEntity"/> that was changed or deleted by someone else since it was read.
/// Nothing in the commit is persisted.
/// </summary>
/// <remarks>
/// <para>
/// The unit of work still holds the stale entities, so retrying the same commit fails again. Reload the entities in
/// a new unit of work (or scope), apply the change again, and commit, or report the conflict to the caller (for
/// example as HTTP 409 or 412).
/// </para>
/// <para>
/// The provider's own exception, if any, is kept as <see cref="Exception.InnerException"/>.
/// </para>
/// </remarks>
public sealed class ConcurrencyException : Exception
{
    private const string DefaultMessage =
        "The commit was rejected because an entity was changed or deleted by someone else since it was read.";

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </summary>
    public ConcurrencyException()
        : this(DefaultMessage, [], innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a message.
    /// </summary>
    /// <param name="message">The message.</param>
    public ConcurrencyException(string message)
        : this(message, [], innerException: null)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class with a message and the
    /// provider's exception.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="innerException">The provider's exception.</param>
    public ConcurrencyException(string message, Exception? innerException)
        : this(message, [], innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class for the conflicting entities, with
    /// a message that names their types.
    /// </summary>
    /// <param name="entities">The entities whose update or delete conflicted.</param>
    /// <param name="innerException">The provider's exception, if any.</param>
    public ConcurrencyException(IEnumerable<object> entities, Exception? innerException = null)
        : this(Snapshot(entities), innerException)
    {
    }

    private ConcurrencyException(IReadOnlyList<object> entities, Exception? innerException)
        : this(Describe(entities), entities, innerException)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConcurrencyException"/> class.
    /// </summary>
    /// <param name="message">The message.</param>
    /// <param name="entities">The entities whose update or delete conflicted.</param>
    /// <param name="innerException">The provider's exception, if any.</param>
    public ConcurrencyException(string message, IEnumerable<object> entities, Exception? innerException)
        : base(message, innerException)
    {
        ArgumentNullException.ThrowIfNull(entities);
        Entities = entities.ToList().AsReadOnly();
    }

    /// <summary>
    /// Gets the entities whose update or delete conflicted. A provider that stops at the first conflict, as EF Core
    /// does, reports only the entities it reached, so the list is not necessarily every stale entity in the commit.
    /// </summary>
    public IReadOnlyList<object> Entities { get; }

    private static IReadOnlyList<object> Snapshot(IEnumerable<object> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        return entities.ToList();
    }

    private static string Describe(IReadOnlyList<object> entities)
    {
        if (entities.Count == 0)
            return DefaultMessage;

        var subject = entities.Count == 1
            ? $"the {entities[0].GetType().Name} was"
            : $"{entities.Count} entities ({string.Join(", ", entities.Select(e => e.GetType().Name).Distinct())}) were";

        return $"The commit was rejected because {subject} changed or deleted by someone else since it was read. " +
            "Reload and apply the change again.";
    }
}
