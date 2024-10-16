namespace RapidRepo.UnitOfWork;

/// <summary>
/// Represents a unit of work for managing database transactions.
/// </summary>
/// <typeparam name="TUserId">The type of the user ID.</typeparam>
public interface IUnitOfWork<TUserId>
    where TUserId : struct
{
    /// <summary>
    /// Asynchronously commits the changes made in the unit of work to the database.
    /// </summary>
    /// <param name="userId">The optional user ID associated with the commit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    Task CommitAsync(TUserId? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the changes made in the unit of work to the database.
    /// </summary>
    /// <param name="userId">The optional user ID associated with the commit.</param>
    void Commit(TUserId? userId = null);
}
