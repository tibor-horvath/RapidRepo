namespace RapidRepo.UnitOfWork;

/// <summary>
/// A transaction opened by <see cref="IUnitOfWork{TUserKey}.BeginTransactionAsync"/>. Every commit of the unit of
/// work made while it is open becomes permanent only when <see cref="CommitAsync"/> is called.
/// </summary>
/// <remarks>
/// Disposing the transaction without calling <see cref="CommitAsync"/> rolls it back. Prefer
/// <see cref="IUnitOfWork{TUserKey}.ExecuteInTransactionAsync{TResult}"/>, which also retries on transient failures
/// where the provider supports it.
/// </remarks>
public interface IUnitOfWorkTransaction : IAsyncDisposable, IDisposable
{
    /// <summary>
    /// Makes every commit made in the transaction permanent.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Discards every commit made in the transaction.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
