using FluentAssertions;
using RapidRepo.Concurrency;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// A commit that hits a concurrency conflict persists nothing, not even the changes that were written before the
/// conflict was detected.
/// </summary>
/// <remarks>
/// Every real database must pass this. It is kept apart from <see cref="ConcurrencyConformanceTests{TStore}"/> so
/// that a test double without transactions, such as EF Core InMemory, can leave it out rather than pass it vacuously.
/// </remarks>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class ConcurrencyAtomicityConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    [Fact]
    public void Conflict_PersistsNothingFromTheCommit()
    {
        var ids = SeedTickets(new Ticket { Title = "Fresh" }, new Ticket { Title = "Stale" }).Select(t => t.Id).ToList();
        var session = OpenSession();
        var fresh = session.Tickets().GetById(ids[0])!;
        var stale = session.Tickets().GetById(ids[1])!;
        Commit(s =>
        {
            var other = s.Tickets().GetById(ids[1])!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });

        fresh.Title = "Edited";
        session.Tickets().Update(fresh);
        stale.Title = "Edited";
        session.Tickets().Update(stale);
        session.Notes().Add(new Note { Title = "Added in the failed commit" });
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(ids[0]))!.Title.Should().Be("Fresh");
        InNewSession(s => s.Notes().Count()).Should().Be(0);
    }
}
