using FluentAssertions;
using RapidRepo.Repositories;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Audit fields are stamped at commit time, with the commit's user and the unit of work's clock.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class AuditConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    [Fact]
    public void Add_StampsCreatedAtAndCreatedBy()
    {
        var note = new Note { Title = "New" };

        Commit(s => s.Notes().Add(note), Alice);

        var read = InNewSession(s => s.Notes().GetById(note.Id))!;
        read.CreatedAt.Should().Be(T1);
        read.CreatedBy.Should().Be(Alice);
        read.ModifiedAt.Should().BeNull();
        read.ModifiedBy.Should().BeNull();
    }

    [Fact]
    public void Commit_WithoutAUser_StampsTheDefaultUser()
    {
        var note = new Note();

        Commit(s => s.Notes().Add(note));

        InNewSession(s => s.Notes().GetById(note.Id))!.CreatedBy.Should().Be(DefaultUser);
    }

    [Fact]
    public void Update_StampsModifiedAtAndModifiedBy_AndKeepsTheCreationStamp()
    {
        var note = new Note { Title = "Draft" };
        Commit(s => s.Notes().Add(note), Alice);
        Clock.UtcNow = T2;

        Commit(s =>
        {
            var loaded = s.Notes().GetById(note.Id)!;
            loaded.Title = "Final";
            s.Notes().Update(loaded);
        }, Bob);

        var read = InNewSession(s => s.Notes().GetById(note.Id))!;
        read.CreatedAt.Should().Be(T1);
        read.CreatedBy.Should().Be(Alice);
        read.ModifiedAt.Should().Be(T2);
        read.ModifiedBy.Should().Be(Bob);
    }

    [Fact]
    public void Delete_StampsDeletedAtAndDeletedBy_AtCommitTime()
    {
        var id = SeedNotes(new Note())[0].Id;
        var session = OpenSession();
        session.Notes().DeleteById(id);

        Clock.UtcNow = T2;
        session.UnitOfWork.Commit(Bob);

        var read = InNewSession(s => s.Notes().GetById(id, IncludeDeleted))!;
        read.DeletedAt.Should().Be(T2, "the commit, not the Delete call, decides the deletion time");
        read.DeletedBy.Should().Be(Bob);
    }

    [Fact]
    public void Update_OfAnAlreadyDeletedEntity_KeepsTheDeletionStamp()
    {
        var id = SeedNotes(new Note { Title = "Before" })[0].Id;
        Commit(s => s.Notes().DeleteById(id), Alice);
        Clock.UtcNow = T2;

        Commit(s =>
        {
            var deleted = s.Notes().GetById(id, IncludeDeleted)!;
            deleted.Title = "Edited while deleted";
            s.Notes().Update(deleted);
        }, Bob);

        var read = InNewSession(s => s.Notes().GetById(id, IncludeDeleted))!;
        read.Title.Should().Be("Edited while deleted");
        read.DeletedAt.Should().Be(T1);
        read.DeletedBy.Should().Be(Alice);
        read.ModifiedBy.Should().Be(Bob);
    }

    [Fact]
    public void Delete_OfAnAlreadyDeletedEntity_KeepsTheDeletionStamp()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id), Alice);
        Clock.UtcNow = T2;

        Commit(s => s.Notes().Delete(s.Notes().GetById(id, IncludeDeleted)!), Bob);

        var read = InNewSession(s => s.Notes().GetById(id, IncludeDeleted))!;
        read.DeletedAt.Should().Be(T1);
        read.DeletedBy.Should().Be(Alice);
    }

    [Fact]
    public void AddThenDelete_InOneCommit_InsertsTheEntityAsDeleted_WithTheCommitsStamp()
    {
        var session = OpenSession();
        var note = new Note { Title = "short-lived" };
        session.Notes().Add(note);
        session.Notes().Delete(note);
        Clock.UtcNow = T2;
        session.UnitOfWork.Commit(Bob);

        var read = InNewSession(s => s.Notes().GetById(note.Id, IncludeDeleted));
        read.Should().NotBeNull();
        read!.DeletedAt.Should().Be(T2);
        read.DeletedBy.Should().Be(Bob);
    }

    [Fact]
    public void Restore_ClearsDeletedBy_AndStampsTheModification()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id), Alice);
        Clock.UtcNow = T2;

        Commit(s => s.Notes().RestoreById(id), Bob);

        var read = InNewSession(s => s.Notes().GetById(id))!;
        read.DeletedAt.Should().BeNull();
        read.DeletedBy.Should().BeNull();
        read.ModifiedAt.Should().Be(T2);
        read.ModifiedBy.Should().Be(Bob);
    }

    [Fact]
    public void OneCommit_StampsEveryEntityWithTheSameTime()
    {
        Commit(s => s.Notes().AddRange([new Note(), new Note(), new Note()]));

        InNewSession(s => s.Notes().GetAll().Select(n => n.CreatedAt).Distinct()).Should().Equal(T1);
    }

    // ── Current-user provider: explicit userId > provider > default user ─────

    [Fact]
    public void CurrentUser_StampsCreatedBy_WhenTheCommitNamesNone()
    {
        var note = new Note();
        var session = OpenSession(new ConformanceCurrentUser(Alice));
        session.Notes().Add(note);

        session.UnitOfWork.Commit();

        InNewSession(s => s.Notes().GetById(note.Id))!.CreatedBy.Should().Be(Alice);
    }

    [Fact]
    public async Task CurrentUser_StampsModifiedByAndDeletedBy_OnAsyncCommits()
    {
        var edited = new Note { Title = "Draft" };
        var deleted = new Note();
        SeedNotes(edited, deleted);
        Clock.UtcNow = T2;

        var session = OpenSession(new ConformanceCurrentUser(Bob));
        var loaded = session.Notes().GetById(edited.Id)!;
        loaded.Title = "Final";
        session.Notes().Update(loaded);
        session.Notes().DeleteById(deleted.Id);
        await session.UnitOfWork.CommitAsync();

        InNewSession(s => s.Notes().GetById(edited.Id))!.ModifiedBy.Should().Be(Bob);
        var read = InNewSession(s => s.Notes().GetById(deleted.Id, IncludeDeleted))!;
        read.DeletedBy.Should().Be(Bob);
        read.ModifiedBy.Should().Be(Bob);
    }

    [Fact]
    public void CurrentUser_StampsModifiedBy_OnRestore_AndClearsDeletedBy()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id), Alice);
        Clock.UtcNow = T2;

        var session = OpenSession(new ConformanceCurrentUser(Bob));
        session.Notes().RestoreById(id);
        session.UnitOfWork.Commit();

        var read = InNewSession(s => s.Notes().GetById(id))!;
        read.DeletedAt.Should().BeNull();
        read.DeletedBy.Should().BeNull();
        read.ModifiedAt.Should().Be(T2);
        read.ModifiedBy.Should().Be(Bob);
    }

    [Fact]
    public void CurrentUser_IsOverriddenByAnExplicitUser()
    {
        var note = new Note();
        var currentUser = new ConformanceCurrentUser(Alice);
        var session = OpenSession(currentUser);
        session.Notes().Add(note);

        session.UnitOfWork.Commit(Bob);

        InNewSession(s => s.Notes().GetById(note.Id))!.CreatedBy.Should().Be(Bob);
        currentUser.Calls.Should().Be(0, "an explicit user means the provider is not asked");
    }

    [Fact]
    public void CurrentUser_ReturningNull_StampsTheDefaultUser()
    {
        var note = new Note();
        var session = OpenSession(new ConformanceCurrentUser(null));
        session.Notes().Add(note);

        session.UnitOfWork.Commit();

        InNewSession(s => s.Notes().GetById(note.Id))!.CreatedBy.Should().Be(DefaultUser);
    }

    [Fact]
    public void CurrentUser_IsAskedOnce_ForACommitOfSeveralEntities()
    {
        var currentUser = new ConformanceCurrentUser(Alice);
        var session = OpenSession(currentUser);
        session.Notes().AddRange([new Note(), new Note(), new Note()]);

        session.UnitOfWork.Commit();

        currentUser.Calls.Should().Be(1);
        InNewSession(s => s.Notes().GetAll().Select(n => n.CreatedBy).Distinct()).Should().Equal(Alice);
    }
}
