using Microsoft.EntityFrameworkCore.Storage;
using RapidRepo.UnitOfWork;

namespace RapidRepo.EntityFrameworkCore.Internal;

/// <summary>
/// An <see cref="IUnitOfWorkTransaction"/> over an EF Core transaction. Disposing it without committing rolls it
/// back, as EF Core does.
/// </summary>
/// <param name="transaction">The EF Core transaction, owned by this instance.</param>
internal sealed class EfUnitOfWorkTransaction(IDbContextTransaction transaction) : IUnitOfWorkTransaction
{
    public Task CommitAsync(CancellationToken cancellationToken = default) => transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) => transaction.RollbackAsync(cancellationToken);

    public void Dispose() => transaction.Dispose();

    public ValueTask DisposeAsync() => transaction.DisposeAsync();
}
