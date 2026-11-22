using FluentAssertions;
using RapidRepo.Concurrency;
using RapidRepo.UnitOfWork;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Explicit transactions: several commits that succeed or fail together, nested calls that join the outer
/// transaction, and transactions opened by hand.
/// </summary>
/// <remarks>
/// Every real database must pass this. A test double without transactions, such as EF Core InMemory, leaves it out
/// rather than pass it vacuously. Persisted state is read only after the transaction ends, because some stores, such
/// as SQLite in memory, share one connection between sessions.
/// </remarks>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class TransactionConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    [Fact]
    public async Task ExecuteInTransactionAsync_PersistsEveryCommit_WhenTheOperationCompletes()
    {
        var session = OpenSession();

        await session.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await session.Notes().AddAsync(new Note { Title = "First" }, ct);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);

            await session.Notes().AddAsync(new Note { Title = "Second" }, ct);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);
        });

        InNewSession(s => s.Notes().GetAll().Select(n => n.Title)).Should().BeEquivalentTo(["First", "Second"]);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_ReturnsTheResult_AndKeysGeneratedByEarlierCommits()
    {
        var session = OpenSession();

        var id = await session.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            var note = new Note { Title = "Parent" };
            await session.Notes().AddAsync(note, ct);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);
            return note.Id;
        });

        id.Should().NotBe(0);
        InNewSession(s => s.Notes().GetById(id))!.Title.Should().Be("Parent");
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackEveryCommit_AndRethrows_WhenTheOperationThrows()
    {
        var session = OpenSession();
        var failure = new InvalidOperationException("Boom");

        var act = () => session.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await session.Notes().AddAsync(new Note { Title = "Committed, then rolled back" }, ct);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);
            throw failure;
        });

        (await act.Should().ThrowAsync<InvalidOperationException>()).Which.Should().BeSameAs(failure);
        InNewSession(s => s.Notes().Count()).Should().Be(0);
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_RollsBackEarlierCommits_OnAConcurrencyConflict()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var session = OpenSession();
        var stale = session.Tickets().GetById(id)!;
        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });

        var act = () => session.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await session.Notes().AddAsync(new Note { Title = "Rolled back" }, ct);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);

            stale.Title = "Stale";
            session.Tickets().Update(stale);
            await session.UnitOfWork.CommitAsync(cancellationToken: ct);
        });

        await act.Should().ThrowAsync<ConcurrencyException>();
        InNewSession(s => s.Notes().Count()).Should().Be(0);
        InNewSession(s => s.Tickets().GetById(id))!.Title.Should().Be("Changed");
    }

    [Fact]
    public async Task NestedExecuteInTransactionAsync_JoinsTheOuterTransaction()
    {
        var session = OpenSession();

        var act = () => session.UnitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            await session.UnitOfWork.ExecuteInTransactionAsync(async innerCt =>
            {
                await session.Notes().AddAsync(new Note { Title = "Inner" }, innerCt);
                await session.UnitOfWork.CommitAsync(cancellationToken: innerCt);
            }, cancellationToken: ct);

            throw new InvalidOperationException("The outer transaction fails after the inner one completed.");
        });

        await act.Should().ThrowAsync<InvalidOperationException>();
        InNewSession(s => s.Notes().Count()).Should().Be(0, "the inner call joined the outer transaction, which rolled back");
    }

    [Fact]
    public async Task BeginTransactionAsync_PersistsEveryCommit_WhenCommitted()
    {
        var session = OpenSession();

        await using (var transaction = await session.UnitOfWork.BeginTransactionAsync())
        {
            await session.Notes().AddAsync(new Note { Title = "First" });
            await session.UnitOfWork.CommitAsync();
            await session.Notes().AddAsync(new Note { Title = "Second" });
            await session.UnitOfWork.CommitAsync();

            await transaction.CommitAsync();
        }

        InNewSession(s => s.Notes().Count()).Should().Be(2);
    }

    [Fact]
    public async Task BeginTransactionAsync_RollsBack_WhenDisposedWithoutCommit()
    {
        var session = OpenSession();

        await using (await session.UnitOfWork.BeginTransactionAsync())
        {
            await session.Notes().AddAsync(new Note { Title = "Never committed" });
            await session.UnitOfWork.CommitAsync();
        }

        InNewSession(s => s.Notes().Count()).Should().Be(0);
    }

    [Fact]
    public async Task BeginTransactionAsync_RollsBack_WhenRolledBack()
    {
        var session = OpenSession();

        await using (var transaction = await session.UnitOfWork.BeginTransactionAsync())
        {
            await session.Notes().AddAsync(new Note { Title = "Rolled back" });
            await session.UnitOfWork.CommitAsync();

            await transaction.RollbackAsync();
        }

        InNewSession(s => s.Notes().Count()).Should().Be(0);
    }

    [Fact]
    public async Task NestedBeginTransactionAsync_JoinsTheOuterTransaction()
    {
        var session = OpenSession();

        await using (var outer = await session.UnitOfWork.BeginTransactionAsync())
        {
            await using (var inner = await session.UnitOfWork.BeginTransactionAsync())
            {
                inner.Should().BeSameAs(JoinedTransaction.Instance);
                await session.Notes().AddAsync(new Note { Title = "Inner" });
                await session.UnitOfWork.CommitAsync();
                await inner.CommitAsync();
            }

            await outer.RollbackAsync();
        }

        InNewSession(s => s.Notes().Count()).Should().Be(0, "committing the joined transaction left the outcome to the outer one");
    }

    [Fact]
    public async Task Commit_OutsideATransaction_IsUnchanged()
    {
        var session = OpenSession();

        await session.Notes().AddAsync(new Note { Title = "Alone" });
        await session.UnitOfWork.CommitAsync();

        InNewSession(s => s.Notes().Count()).Should().Be(1);
    }
}
