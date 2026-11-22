namespace RapidRepo.UnitOfWork;

/// <summary>
/// Adapts a transaction operation without a result to the generic form, so that each unit of work implements
/// <c>ExecuteInTransactionAsync</c> once, for operations that return a result.
/// </summary>
internal static class TransactionOperations
{
    public static Func<CancellationToken, Task<ValueTuple>> WithoutResult(Func<CancellationToken, Task> operation)
    {
        ArgumentNullException.ThrowIfNull(operation);

        return async cancellationToken =>
        {
            await operation(cancellationToken);
            return default;
        };
    }
}
