namespace RapidRepo.UnitOfWork;

/// <summary>
/// The transaction a provider returns when one is requested while another is already open: the work joins the
/// outer transaction, and this scope neither commits nor rolls it back.
/// </summary>
/// <remarks>
/// Joining lets services that each open a transaction call one another. The outer transaction decides the outcome,
/// so to roll the work back, let the exception propagate to it.
/// </remarks>
public sealed class JoinedTransaction : IUnitOfWorkTransaction
{
    private JoinedTransaction()
    {
    }

    /// <summary>
    /// Gets the shared instance.
    /// </summary>
    public static JoinedTransaction Instance { get; } = new();

    /// <summary>Does nothing: the outer transaction commits.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task CommitAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Does nothing: the outer transaction rolls back when the exception reaches it.</summary>
    /// <param name="cancellationToken">Not used.</param>
    /// <returns>A completed task.</returns>
    public Task RollbackAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <inheritdoc />
    public void Dispose()
    {
    }

    /// <inheritdoc />
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
