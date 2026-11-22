using System.Data;

namespace RapidRepo.UnitOfWork;

/// <summary>
/// Represents a unit of work for managing database transactions.
/// </summary>
/// <typeparam name="TUserKey">The type of the user ID.</typeparam>
public interface IUnitOfWork<TUserKey>
    where TUserKey : struct
{
    /// <summary>
    /// Asynchronously commits the changes made in the unit of work to the database.
    /// </summary>
    /// <param name="userId">The optional user ID associated with the commit.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous commit operation.</returns>
    Task CommitAsync(TUserKey? userId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Commits the changes made in the unit of work to the database.
    /// </summary>
    /// <param name="userId">The optional user ID associated with the commit.</param>
    void Commit(TUserKey? userId = null);

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction, so that every commit it makes succeeds or fails together.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The transaction commits when <paramref name="operation"/> completes, and rolls back when it throws; the
    /// exception is rethrown. It does not commit the unit of work: call <c>CommitAsync</c> inside
    /// <paramref name="operation"/>, as often as needed.
    /// </para>
    /// <para>
    /// A provider may run <paramref name="operation"/> again after a transient failure, so create and stage the
    /// entities inside it. Called while a transaction is open, it joins that transaction instead, and runs at its
    /// isolation level; a provider throws <see cref="InvalidOperationException"/> when <paramref name="isolationLevel"/>
    /// is stricter than that.
    /// </para>
    /// </remarks>
    /// <param name="operation">The work to run in the transaction.</param>
    /// <param name="isolationLevel">The isolation level, or <see langword="null"/> for the database's default.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <exception cref="NotSupportedException">The unit of work's provider does not support transactions.</exception>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default) =>
        ExecuteInTransactionAsync(TransactionOperations.WithoutResult(operation), isolationLevel, cancellationToken);

    /// <summary>
    /// Runs <paramref name="operation"/> in a transaction, so that every commit it makes succeeds or fails together,
    /// and returns its result.
    /// </summary>
    /// <remarks>
    /// See <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, IsolationLevel?, CancellationToken)"/>.
    /// </remarks>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="operation">The work to run in the transaction.</param>
    /// <param name="isolationLevel">The isolation level, or <see langword="null"/> for the database's default.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The result of <paramref name="operation"/>.</returns>
    /// <exception cref="NotSupportedException">The unit of work's provider does not support transactions.</exception>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default) =>
        throw TransactionErrors.NotSupported(GetType());

    /// <summary>
    /// Opens a transaction that stays open until it is committed, rolled back or disposed. Prefer
    /// <see cref="ExecuteInTransactionAsync(Func{CancellationToken, Task}, IsolationLevel?, CancellationToken)"/>,
    /// which is retried on transient failures where the provider supports it; this form never is.
    /// </summary>
    /// <remarks>
    /// Called while a transaction is open, it returns a <see cref="JoinedTransaction"/>, which leaves the outcome
    /// to the outer transaction and runs at its isolation level; a provider throws
    /// <see cref="InvalidOperationException"/> when <paramref name="isolationLevel"/> is stricter than that.
    /// </remarks>
    /// <param name="isolationLevel">The isolation level, or <see langword="null"/> for the database's default.</param>
    /// <param name="cancellationToken">A token to cancel the asynchronous operation.</param>
    /// <returns>The transaction. Disposing it without committing rolls it back.</returns>
    /// <exception cref="NotSupportedException">The unit of work's provider does not support transactions.</exception>
    Task<IUnitOfWorkTransaction> BeginTransactionAsync(
        IsolationLevel? isolationLevel = null,
        CancellationToken cancellationToken = default) =>
        throw TransactionErrors.NotSupported(GetType());
}
