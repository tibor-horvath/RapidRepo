using FluentAssertions;
using RapidRepo.Repositories;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Soft delete, restore and permanent removal of <see cref="RapidRepo.Entities.Interfaces.IDeletableEntity"/>
/// entities, and the <c>includeDeleted</c> lookups.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class SoftDeleteConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    private static readonly ReadOptions IncludeDeleted = new() { IncludeDeleted = true };

    [Fact]
    public void Delete_OfADeletableEntity_HidesIt_ButKeepsTheRow()
    {
        var id = SeedNotes(new Note { Title = "Gone" })[0].Id;

        Commit(s => s.Notes().Delete(s.Notes().GetById(id)!));

        InNewSession(s => s.Notes().GetById(id)).Should().BeNull();
        InNewSession(s => s.Notes().ExistsById(id)).Should().BeFalse();
        var deleted = InNewSession(s => s.Notes().GetById(id, IncludeDeleted));
        deleted.Should().NotBeNull();
        deleted!.DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteByIdAsync_SoftDeletes()
    {
        var id = SeedNotes(new Note())[0].Id;
        var session = OpenSession();

        await session.Notes().DeleteByIdAsync(id);
        await session.UnitOfWork.CommitAsync();

        InNewSession(s => s.Notes().ExistsById(id)).Should().BeFalse();
        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeTrue();
    }

    [Fact]
    public void DeleteRange_SoftDeletesEveryEntity()
    {
        SeedNotes(new Note(), new Note());

        Commit(s => s.Notes().DeleteRange(s.Notes().GetAll()));

        InNewSession(s => s.Notes().Count()).Should().Be(0);
        InNewSession(s => s.Notes().Count(options: IncludeDeleted)).Should().Be(2);
    }

    [Fact]
    public void RestoreById_BringsTheEntityBack_AndClearsTheDeletionMarks()
    {
        var id = SeedNotes(new Note { Title = "Back" })[0].Id;
        Commit(s => s.Notes().DeleteById(id), Alice);

        Commit(s => s.Notes().RestoreById(id));

        var restored = InNewSession(s => s.Notes().GetById(id));
        restored.Should().NotBeNull();
        restored!.DeletedAt.Should().BeNull();
        restored.DeletedBy.Should().BeNull();
    }

    [Fact]
    public void Restore_OfALoadedDeletedEntity_BringsItBack()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));

        Commit(s => s.Notes().Restore(s.Notes().GetById(id, IncludeDeleted)!));

        InNewSession(s => s.Notes().ExistsById(id)).Should().BeTrue();
    }

    [Fact]
    public void RestoreRange_BringsEveryEntityBack()
    {
        SeedNotes(new Note(), new Note());
        Commit(s => s.Notes().DeleteRange(s.Notes().GetAll()));

        Commit(s => s.Notes().RestoreRange(s.Notes().GetAll(options: IncludeDeleted)));

        InNewSession(s => s.Notes().Count()).Should().Be(2);
    }

    [Fact]
    public void RestoreById_WithoutIncludeDeleted_CannotFindTheDeletedEntity()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));

        Commit(s => s.Notes().RestoreById(id, includeDeleted: false));

        InNewSession(s => s.Notes().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public void Restore_OfANonDeletableEntity_Throws()
    {
        var id = SeedWidgets(new Widget())[0].Id;
        var session = OpenSession();

        var restore = () => session.Widgets().Restore(session.Widgets().GetById(id)!);
        var restoreById = () => session.Widgets().RestoreById(id);

        restore.Should().Throw<InvalidOperationException>();
        restoreById.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void HardDelete_OfADeletableEntity_RemovesItPermanently()
    {
        var id = SeedNotes(new Note())[0].Id;

        Commit(s => s.Notes().HardDelete(s.Notes().GetById(id)!));

        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeFalse();
    }

    [Fact]
    public void HardDeleteById_ByDefault_CannotReachASoftDeletedEntity()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));

        Commit(s => s.Notes().HardDeleteById(id));

        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeTrue();
    }

    [Fact]
    public async Task HardDeleteByIdAsync_IncludeDeleted_RemovesASoftDeletedEntity()
    {
        var id = SeedNotes(new Note())[0].Id;
        Commit(s => s.Notes().DeleteById(id));
        var session = OpenSession();

        await session.Notes().HardDeleteByIdAsync(id, includeDeleted: true);
        await session.UnitOfWork.CommitAsync();

        InNewSession(s => s.Notes().ExistsById(id, IncludeDeleted)).Should().BeFalse();
    }

    [Fact]
    public void IncludeDeleted_IsHonouredByEveryRead()
    {
        var ids = SeedNotes(new Note { Title = "Live", Rank = 1 }, new Note { Title = "Dead", Rank = 2 })
            .Select(n => n.Id).ToList();
        Commit(s => s.Notes().DeleteById(ids[1]));

        InNewSession(s => s.Notes().GetAll(options: IncludeDeleted)).Should().HaveCount(2);
        InNewSession(s => s.Notes().GetByIds(ids, IncludeDeleted)).Should().HaveCount(2);
        InNewSession(s => s.Notes().Count(options: IncludeDeleted)).Should().Be(2);
        InNewSession(s => s.Notes().Any(n => n.Rank == 2, IncludeDeleted)).Should().BeTrue();
        InNewSession(s => s.Notes().GetSingle(n => n.Rank == 2, options: IncludeDeleted).Title).Should().Be("Dead");
        InNewSession(s => s.Notes().GetById(ids[1], n => n.Title, IncludeDeleted)).Should().Be("Dead");
        InNewSession(s => s.Notes().GetAllPaged(pageSize: 10, options: IncludeDeleted).TotalCount).Should().Be(2);

        InNewSession(s => s.Notes().GetAll()).Should().ContainSingle();
        InNewSession(s => s.Notes().GetByIds(ids)).Should().ContainSingle();
        InNewSession(s => s.Notes().GetAllPaged(pageSize: 10).TotalCount).Should().Be(1);
    }
}
