using FluentAssertions;
using RapidRepo.Concurrency;
using RapidRepo.Repositories;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Optimistic concurrency of <see cref="RapidRepo.Entities.Interfaces.IVersionedEntity"/> entities: the version is
/// incremented by every update, and an update or delete of a row changed since it was read fails with
/// <see cref="ConcurrencyException"/> and persists nothing.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class ConcurrencyConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    [Fact]
    public void Add_KeepsTheInitialVersion()
    {
        var id = SeedTickets(new Ticket { Title = "New" })[0].Id;

        InNewSession(s => s.Tickets().GetById(id)!.Version).Should().Be(0);
    }

    [Fact]
    public void Update_IncrementsTheVersion_InTheStoreAndOnTheEntity()
    {
        var id = SeedTickets(new Ticket { Title = "Draft" })[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;

        ticket.Title = "Final";
        session.Tickets().Update(ticket);
        session.UnitOfWork.Commit();

        ticket.Version.Should().Be(1, "the new version is returned to the caller, to hand back to the client");
        InNewSession(s => s.Tickets().GetById(id)!.Version).Should().Be(1);
    }

    [Fact]
    public void Commit_WithoutChanges_DoesNotIncrementTheVersion()
    {
        var id = SeedTickets(new Ticket())[0].Id;
        var session = OpenSession();
        _ = session.Tickets().GetById(id);

        session.UnitOfWork.Commit();

        InNewSession(s => s.Tickets().GetById(id)!.Version).Should().Be(0);
    }

    [Fact]
    public void SoftDelete_AndRestore_EachIncrementTheVersion()
    {
        var id = SeedTickets(new Ticket())[0].Id;

        Commit(s => s.Tickets().DeleteById(id));
        InNewSession(s => s.Tickets().GetById(id, IncludeDeleted)!.Version).Should().Be(1);

        Commit(s => s.Tickets().RestoreById(id));
        InNewSession(s => s.Tickets().GetById(id)!.Version).Should().Be(2);
    }

    [Fact]
    public void TwoSessions_UpdatingTheSameRow_TheSecondCommitThrows()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var first = OpenSession();
        var second = OpenSession();
        var firstCopy = first.Tickets().GetById(id)!;
        var secondCopy = second.Tickets().GetById(id)!;

        firstCopy.Title = "First";
        first.Tickets().Update(firstCopy);
        first.UnitOfWork.Commit();

        secondCopy.Title = "Second";
        second.Tickets().Update(secondCopy);
        var commit = () => second.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>()
            .Which.Entities.Should().ContainSingle().Which.Should().BeSameAs(secondCopy);
        var stored = InNewSession(s => s.Tickets().GetById(id))!;
        stored.Title.Should().Be("First");
        stored.Version.Should().Be(1);
    }

    [Fact]
    public async Task TwoSessions_UpdatingTheSameRow_TheSecondCommitAsyncThrows()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var first = OpenSession();
        var second = OpenSession();
        var firstCopy = await first.Tickets().GetByIdAsync(id);
        var secondCopy = await second.Tickets().GetByIdAsync(id);

        firstCopy!.Title = "First";
        first.Tickets().Update(firstCopy);
        await first.UnitOfWork.CommitAsync();

        secondCopy!.Title = "Second";
        second.Tickets().Update(secondCopy);
        var commit = () => second.UnitOfWork.CommitAsync();

        await commit.Should().ThrowAsync<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(id))!.Title.Should().Be("First");
    }

    [Fact]
    public void Update_AfterAConcurrentHardDelete_Throws()
    {
        var id = SeedTickets(new Ticket())[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;

        Commit(s => s.Tickets().HardDeleteById(id));

        ticket.Title = "Too late";
        session.Tickets().Update(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().ExistsById(id, IncludeDeleted)).Should().BeFalse();
    }

    [Fact]
    public void Update_AfterAConcurrentSoftDelete_Throws()
    {
        var id = SeedTickets(new Ticket())[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;

        Commit(s => s.Tickets().DeleteById(id));

        ticket.Title = "Too late";
        session.Tickets().Update(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(id, IncludeDeleted))!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public void SoftDelete_AfterAConcurrentUpdate_Throws()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;

        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });

        session.Tickets().Delete(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().ExistsById(id)).Should().BeTrue();
    }

    [Fact]
    public void HardDelete_AfterAConcurrentUpdate_Throws()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;

        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });

        session.Tickets().HardDelete(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(id))!.Title.Should().Be("Changed");
    }

    [Fact]
    public void DetachedUpdate_WithTheCurrentVersion_Succeeds()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        var session = OpenSession();

        // Built from a request that carries the version the client read.
        var fromClient = new Ticket { Id = id, Title = "From client", Version = 0 };
        session.Tickets().Update(fromClient);
        session.UnitOfWork.Commit();

        var stored = InNewSession(s => s.Tickets().GetById(id))!;
        stored.Title.Should().Be("From client");
        stored.Version.Should().Be(1);
    }

    [Fact]
    public void DetachedUpdate_WithAStaleVersion_Throws()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });
        var session = OpenSession();

        var fromClient = new Ticket { Id = id, Title = "From a stale client", Version = 0 };
        session.Tickets().Update(fromClient);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(id))!.Title.Should().Be("Changed");
    }

    [Fact]
    public void LoadedEntity_GivenAStaleVersionFromTheClient_Throws()
    {
        var id = SeedTickets(new Ticket { Title = "Original" })[0].Id;
        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });
        var session = OpenSession();

        // Load, copy the client's version and changes onto the entity, update: the client's version is checked.
        var ticket = session.Tickets().GetById(id)!;
        ticket.Version = 0;
        ticket.Title = "From a stale client";
        session.Tickets().Update(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        InNewSession(s => s.Tickets().GetById(id))!.Title.Should().Be("Changed");
    }

    [Fact]
    public void FailedCommit_LeavesTheEntityAtTheVersionItWasReadAt()
    {
        var id = SeedTickets(new Ticket())[0].Id;
        var session = OpenSession();
        var ticket = session.Tickets().GetById(id)!;
        Commit(s =>
        {
            var other = s.Tickets().GetById(id)!;
            other.Title = "Changed";
            s.Tickets().Update(other);
        });

        ticket.Title = "Stale";
        session.Tickets().Update(ticket);
        var commit = () => session.UnitOfWork.Commit();

        commit.Should().Throw<ConcurrencyException>();
        ticket.Version.Should().Be(0);
        commit.Should().Throw<ConcurrencyException>("a retried commit checks the same version again");
        InNewSession(s => s.Tickets().GetById(id)!.Version).Should().Be(1);
    }

    [Fact]
    public void NonVersionedEntities_KeepLastWriteWins()
    {
        var id = SeedNotes(new Note { Title = "Original" })[0].Id;
        var first = OpenSession();
        var second = OpenSession();
        var firstCopy = first.Notes().GetById(id)!;
        var secondCopy = second.Notes().GetById(id)!;

        firstCopy.Title = "First";
        first.Notes().Update(firstCopy);
        first.UnitOfWork.Commit();

        secondCopy.Title = "Second";
        second.Notes().Update(secondCopy);
        second.UnitOfWork.Commit();

        InNewSession(s => s.Notes().GetById(id))!.Title.Should().Be("Second");
    }
}
