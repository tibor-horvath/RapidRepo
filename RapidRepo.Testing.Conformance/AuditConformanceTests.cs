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
}
