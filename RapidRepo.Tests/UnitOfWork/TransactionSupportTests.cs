using System.Data;
using AwesomeAssertions;
using RapidRepo.Entities;
using RapidRepo.Repositories.Interfaces;
using RapidRepo.UnitOfWork;

namespace RapidRepo.Tests.UnitOfWork;

/// <summary>
/// The provider-neutral transaction members: the defaults that keep existing units of work compiling, and the
/// template that runs an operation in a provider's transaction.
/// </summary>
public class TransactionSupportTests
{
    [Fact]
    public async Task DirectInterfaceImplementation_ThrowsNotSupported()
    {
        IUnitOfWork<Guid> unitOfWork = new HandWrittenUnitOfWork();

        var execute = () => unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
        var executeWithResult = () => unitOfWork.ExecuteInTransactionAsync(_ => Task.FromResult(1));
        var begin = () => unitOfWork.BeginTransactionAsync();

        await execute.Should().ThrowAsync<NotSupportedException>().WithMessage($"*{nameof(HandWrittenUnitOfWork)}*");
        await executeWithResult.Should().ThrowAsync<NotSupportedException>();
        await begin.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task DirectInterfaceImplementation_OfTheGenericOverload_AlsoServesTheOverloadWithoutAResult()
    {
        var implementation = new GenericOnlyUnitOfWork();
        IUnitOfWork<Guid> unitOfWork = implementation;
        var ran = false;

        await unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            ran = true;
            return Task.CompletedTask;
        });

        ran.Should().BeTrue();
        implementation.Calls.Should().Be(1);
    }

    [Fact]
    public async Task ProviderWithoutTransactions_ThrowsNotSupported()
    {
        var unitOfWork = new ProviderUnitOfWork(transaction: null);

        var execute = () => unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);
        var begin = () => unitOfWork.BeginTransactionAsync();

        await execute.Should().ThrowAsync<NotSupportedException>().WithMessage($"*{nameof(ProviderUnitOfWork)}*");
        await begin.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_CommitsAndDisposes_WhenTheOperationCompletes()
    {
        var transaction = new RecordingTransaction();
        var unitOfWork = new ProviderUnitOfWork(transaction);
        using var cts = new CancellationTokenSource();
        CancellationToken seen = default;

        var result = await unitOfWork.ExecuteInTransactionAsync(
            ct =>
            {
                seen = ct;
                return Task.FromResult(42);
            },
            IsolationLevel.Serializable,
            cts.Token);

        result.Should().Be(42);
        seen.Should().Be(cts.Token);
        unitOfWork.RequestedIsolationLevel.Should().Be(IsolationLevel.Serializable);
        transaction.Calls.Should().Equal("Commit", "Dispose");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_DisposesWithoutCommit_AndRethrows_WhenTheOperationThrows()
    {
        var transaction = new RecordingTransaction();
        var unitOfWork = new ProviderUnitOfWork(transaction);
        var failure = new InvalidOperationException("Boom");

        var act = () => unitOfWork.ExecuteInTransactionAsync(_ => Task.FromException(failure));

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        transaction.Calls.Should().Equal("Dispose");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_WithoutAnOperation_Throws()
    {
        var unitOfWork = new ProviderUnitOfWork(new RecordingTransaction());

        var execute = () => unitOfWork.ExecuteInTransactionAsync(null!);
        var executeWithResult = () => unitOfWork.ExecuteInTransactionAsync<int>(null!);

        await execute.Should().ThrowAsync<ArgumentNullException>();
        await executeWithResult.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public async Task JoinedTransaction_DoesNothing()
    {
        var joined = JoinedTransaction.Instance;

        await joined.CommitAsync();
        await joined.RollbackAsync();
        await joined.DisposeAsync();
        joined.Dispose();

        JoinedTransaction.Instance.Should().BeSameAs(joined);
    }

    /// <summary>Implements the interface directly, as a hand-written test fake would, without the new members.</summary>
    private sealed class HandWrittenUnitOfWork : IUnitOfWork<Guid>
    {
        public Task CommitAsync(Guid? userId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit(Guid? userId = null)
        {
        }
    }

    /// <summary>A hand-written unit of work that implements only the generic transaction overload.</summary>
    private sealed class GenericOnlyUnitOfWork : IUnitOfWork<Guid>
    {
        public int Calls { get; private set; }

        public Task CommitAsync(Guid? userId = null, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public void Commit(Guid? userId = null)
        {
        }

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<CancellationToken, Task<TResult>> operation,
            IsolationLevel? isolationLevel = null,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return operation(cancellationToken);
        }
    }

    /// <summary>A provider that supports transactions when given one, and otherwise keeps the default.</summary>
    private sealed class ProviderUnitOfWork(RecordingTransaction? transaction) : UnitOfWorkBase<Guid>(Guid.Empty)
    {
        public IsolationLevel? RequestedIsolationLevel { get; private set; }

        protected override Task<IUnitOfWorkTransaction> BeginTransactionCoreAsync(
            IsolationLevel? isolationLevel,
            CancellationToken cancellationToken)
        {
            if (transaction is null)
                return base.BeginTransactionCoreAsync(isolationLevel, cancellationToken);

            RequestedIsolationLevel = isolationLevel;
            return Task.FromResult<IUnitOfWorkTransaction>(transaction);
        }

        protected override IRepository<TEntity, TKey> CreateRepository<TEntity, TKey>() => throw new NotSupportedException();

        protected override void CommitCore(Guid? userId)
        {
        }

        protected override Task CommitCoreAsync(Guid? userId, CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RecordingTransaction : IUnitOfWorkTransaction
    {
        public List<string> Calls { get; } = [];

        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Commit");
            return Task.CompletedTask;
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            Calls.Add("Rollback");
            return Task.CompletedTask;
        }

        public void Dispose() => Calls.Add("Dispose");

        public ValueTask DisposeAsync()
        {
            Calls.Add("Dispose");
            return ValueTask.CompletedTask;
        }
    }
}
