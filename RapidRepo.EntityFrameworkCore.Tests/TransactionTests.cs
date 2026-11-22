using System.Data;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using RapidRepo.Entities;

namespace RapidRepo.EntityFrameworkCore.Tests;

/// <summary>
/// EF Core specifics of transactions: execution strategies that retry on failure, clearing the change tracker before
/// a retry, and the isolation level. The provider-neutral behaviour is covered by the conformance suite.
/// </summary>
public sealed class TransactionTests : IDisposable
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly IsolationLevelRecorder _isolationLevels = new();

    public TransactionTests()
    {
        _connection.Open();
        using var context = CreateContext(retrying: false);
        context.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task ExecuteInTransactionAsync_WorksWithAStrategyThatRetriesOnFailure()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            context.Add(new Item { Name = "First" });
            await unitOfWork.CommitAsync(cancellationToken: ct);
            context.Add(new Item { Name = "Second" });
            await unitOfWork.CommitAsync(cancellationToken: ct);
        });

        CountItems().Should().Be(2);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RetriesATransientFailure_WithACleanChangeTracker()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);
        var attempts = 0;

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            attempts++;
            context.ChangeTracker.Entries().Should().BeEmpty("the entities of the failed attempt are not saved again");

            context.Add(new Item { Name = $"Attempt {attempts}" });
            await unitOfWork.CommitAsync(cancellationToken: ct);

            if (attempts == 1)
                throw new TransientTestException();
        });

        attempts.Should().Be(2);
        using var reader = CreateContext(retrying: false);
        reader.Set<Item>().Select(i => i.Name).Should().Equal("Attempt 2");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_DoesNotRetryOtherFailures()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);
        var attempts = 0;

        var act = () => unitOfWork.ExecuteInTransactionAsync(_ =>
        {
            attempts++;
            throw new InvalidOperationException("Not transient");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        attempts.Should().Be(1);
    }

    [Fact]
    public async Task BeginTransactionAsync_WithAStrategyThatRetriesOnFailure_PointsToExecuteInTransactionAsync()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);

        var act = () => unitOfWork.BeginTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ExecuteInTransactionAsync*");
        context.Database.CurrentTransaction.Should().BeNull();
    }

    [Fact]
    public async Task BeginTransactionAsync_InsideExecuteInTransactionAsync_JoinsEvenWithARetryingStrategy()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await using var joined = await unitOfWork.BeginTransactionAsync(cancellationToken: ct);
            joined.Should().BeSameAs(RapidRepo.UnitOfWork.JoinedTransaction.Instance);
        });
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_OpensItsTransactionThroughBeginTransactionCoreAsync_WithARetryingStrategy()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new RecordingUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            context.Add(new Item { Name = "First" });
            await unitOfWork.CommitAsync(cancellationToken: ct);
        });

        unitOfWork.BeginTransactionCalls.Should().Be(1, "an override of the hook applies to both public methods");
        CountItems().Should().Be(1);
    }

    [Fact]
    public async Task BeginTransactionAsync_AfterExecuteInTransactionAsync_IsStillRefusedWithARetryingStrategy()
    {
        await using var context = CreateContext(retrying: true);
        var unitOfWork = new TestUnitOfWork(context);
        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);

        var act = () => unitOfWork.BeginTransactionAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*ExecuteInTransactionAsync*");
    }

    [Theory]
    [InlineData(IsolationLevel.Serializable)]
    [InlineData(IsolationLevel.ReadCommitted)]
    [InlineData(IsolationLevel.Unspecified)]
    public async Task Joining_AtALevelTheOpenTransactionProvides_Succeeds(IsolationLevel requested)
    {
        await using var context = CreateContext(retrying: false);
        var unitOfWork = new TestUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, requested, ct);
            await using var joined = await unitOfWork.BeginTransactionAsync(requested, ct);
            joined.Should().BeSameAs(RapidRepo.UnitOfWork.JoinedTransaction.Instance);
        }, IsolationLevel.Serializable);
    }

    [Fact]
    public async Task Joining_AtALevelTheOpenTransactionDoesNotProvide_Throws()
    {
        await using var context = CreateContext(retrying: false);
        var unitOfWork = new TestUnitOfWork(context);

        // SQLite runs every transaction at Serializable, which is not ordered against Snapshot.
        var act = () => unitOfWork.ExecuteInTransactionAsync(
            ct => unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, IsolationLevel.Snapshot, ct),
            IsolationLevel.Serializable);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Snapshot*Serializable*");
    }

    [Theory]
    [InlineData(IsolationLevel.Serializable, IsolationLevel.ReadCommitted, true)]
    [InlineData(IsolationLevel.RepeatableRead, IsolationLevel.RepeatableRead, true)]
    [InlineData(IsolationLevel.ReadCommitted, IsolationLevel.ReadUncommitted, true)]
    [InlineData(IsolationLevel.Snapshot, IsolationLevel.Snapshot, true)]
    [InlineData(IsolationLevel.ReadCommitted, IsolationLevel.Serializable, false)]
    [InlineData(IsolationLevel.ReadUncommitted, IsolationLevel.ReadCommitted, false)]
    [InlineData(IsolationLevel.Serializable, IsolationLevel.Snapshot, false)]
    [InlineData(IsolationLevel.Snapshot, IsolationLevel.Serializable, false)]
    [InlineData(IsolationLevel.Snapshot, IsolationLevel.ReadCommitted, false)]
    public void IsolationLevels_Satisfies_OrdersTheStandardLevels(IsolationLevel open, IsolationLevel requested, bool expected) =>
        RapidRepo.EntityFrameworkCore.Internal.IsolationLevels.Satisfies(open, requested).Should().Be(expected);

    [Theory]
    [InlineData(IsolationLevel.Serializable)]
    [InlineData(IsolationLevel.ReadCommitted)]
    public async Task IsolationLevel_IsPassedToTheDatabase(IsolationLevel isolationLevel)
    {
        await using var context = CreateContext(retrying: false);
        var unitOfWork = new TestUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask, isolationLevel);
        await using (var transaction = await unitOfWork.BeginTransactionAsync(isolationLevel))
        {
            await transaction.CommitAsync();
        }

        _isolationLevels.Requested.Should().Equal(isolationLevel, isolationLevel);
    }

    [Fact]
    public async Task IsolationLevel_WhenOmitted_IsLeftToTheDatabase()
    {
        await using var context = CreateContext(retrying: false);
        var unitOfWork = new TestUnitOfWork(context);

        await unitOfWork.ExecuteInTransactionAsync(_ => Task.CompletedTask);

        _isolationLevels.Requested.Should().Equal(IsolationLevel.Unspecified);
    }

    private int CountItems()
    {
        using var reader = CreateContext(retrying: false);
        return reader.Set<Item>().Count();
    }

    private ItemContext CreateContext(bool retrying)
    {
        var options = new DbContextOptionsBuilder<ItemContext>()
            .UseSqlite(_connection, sqlite =>
            {
                if (retrying)
                    sqlite.ExecutionStrategy(dependencies => new RetryOnTransientTestException(dependencies));
            })
            .AddInterceptors(_isolationLevels)
            .Options;

        return new ItemContext(options);
    }

    public sealed class Item : BaseEntity<int>
    {
        public string Name { get; set; } = "";
    }

    public sealed class ItemContext(DbContextOptions<ItemContext> options) : DbContext(options)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) => modelBuilder.Entity<Item>();
    }

    private sealed class TestUnitOfWork(DbContext context) : EfUnitOfWork<Guid>(context, Guid.Empty);

    /// <summary>Overrides the transaction hook, as a derived unit of work might to log or set up a session.</summary>
    private sealed class RecordingUnitOfWork(DbContext context) : EfUnitOfWork<Guid>(context, Guid.Empty)
    {
        public int BeginTransactionCalls { get; private set; }

        protected override Task<RapidRepo.UnitOfWork.IUnitOfWorkTransaction> BeginTransactionCoreAsync(
            IsolationLevel? isolationLevel,
            CancellationToken cancellationToken)
        {
            BeginTransactionCalls++;
            return base.BeginTransactionCoreAsync(isolationLevel, cancellationToken);
        }
    }

    private sealed class TransientTestException : Exception;

    /// <summary>An execution strategy that retries <see cref="TransientTestException"/>, like EnableRetryOnFailure.</summary>
    private sealed class RetryOnTransientTestException(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => exception is TransientTestException;
    }

    /// <summary>Records the isolation level of every transaction EF Core starts.</summary>
    private sealed class IsolationLevelRecorder : DbTransactionInterceptor
    {
        public List<IsolationLevel> Requested { get; } = [];

        public override ValueTask<InterceptionResult<DbTransaction>> TransactionStartingAsync(
            DbConnection connection,
            TransactionStartingEventData eventData,
            InterceptionResult<DbTransaction> result,
            CancellationToken cancellationToken = default)
        {
            Requested.Add(eventData.IsolationLevel);
            return base.TransactionStartingAsync(connection, eventData, result, cancellationToken);
        }
    }
}
