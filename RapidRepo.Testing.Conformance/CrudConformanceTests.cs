using AwesomeAssertions;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Create, read, update and delete through the neutral contract, and generated keys.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class CrudConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    [Fact]
    public void Add_ThenCommit_IsReadableInANewSession()
    {
        var widget = new Widget { Name = "Gear", Rank = 3 };

        Commit(s => s.Widgets().Add(widget));

        var read = InNewSession(s => s.Widgets().GetById(widget.Id));
        read.Should().NotBeNull();
        read!.Name.Should().Be("Gear");
        read.Rank.Should().Be(3);
    }

    [Fact]
    public void Commit_PopulatesGeneratedKeys()
    {
        var first = new Widget { Name = "A" };
        var second = new Widget { Name = "B" };

        Commit(s => s.Widgets().AddRange([first, second]));

        first.Id.Should().NotBe(0);
        second.Id.Should().NotBe(0).And.NotBe(first.Id);
    }

    [Fact]
    public async Task AddAsync_ThenCommitAsync_IsReadableInANewSession()
    {
        var session = OpenSession();
        var note = new Note { Title = "Async" };

        await session.Notes().AddAsync(note);
        await session.Notes().AddRangeAsync([new Note { Title = "Async 2" }]);
        await session.UnitOfWork.CommitAsync();

        var titles = await InNewSession(s => s.Notes().GetAllAsync(n => n.Title, orderBy: null));
        titles.Should().BeEquivalentTo(["Async", "Async 2"]);
    }

    [Fact]
    public void Update_ThenCommit_PersistsTheChanges()
    {
        var id = SeedWidgets(new Widget { Name = "Old", Rank = 1 })[0].Id;

        Commit(s =>
        {
            var widget = s.Widgets().GetById(id)!;
            widget.Name = "New";
            widget.Rank = 2;
            s.Widgets().Update(widget);
        });

        var read = InNewSession(s => s.Widgets().GetById(id))!;
        read.Name.Should().Be("New");
        read.Rank.Should().Be(2);
    }

    [Fact]
    public void Update_OfAnEntityLoadedElsewhere_PersistsTheChanges()
    {
        var id = SeedWidgets(new Widget { Name = "Old" })[0].Id;
        var loaded = InNewSession(s => s.Widgets().GetById(id))!;

        loaded.Name = "Changed elsewhere";
        Commit(s => s.Widgets().Update(loaded));

        InNewSession(s => s.Widgets().GetById(id))!.Name.Should().Be("Changed elsewhere");
    }

    [Fact]
    public void UpdateRange_ThenCommit_PersistsEveryEntity()
    {
        var ids = SeedWidgets(new Widget { Name = "A" }, new Widget { Name = "B" }).Select(w => w.Id).ToList();

        Commit(s =>
        {
            var widgets = s.Widgets().GetByIds(ids).ToList();
            widgets.ForEach(w => w.Rank = 9);
            s.Widgets().UpdateRange(widgets);
        });

        InNewSession(s => s.Widgets().GetAll().Select(w => w.Rank)).Should().Equal(9, 9);
    }

    [Fact]
    public void Delete_OfANonDeletableEntity_RemovesItPermanently()
    {
        var id = SeedWidgets(new Widget { Name = "Doomed" })[0].Id;

        Commit(s => s.Widgets().Delete(s.Widgets().GetById(id)!));

        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
        InNewSession(s => s.Widgets().Count()).Should().Be(0);
    }

    [Fact]
    public void DeleteById_OfANonDeletableEntity_RemovesItPermanently()
    {
        var id = SeedWidgets(new Widget { Name = "Doomed" })[0].Id;

        Commit(s => s.Widgets().DeleteById(id));

        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeFalse();
    }

    [Fact]
    public void DeleteById_ForAMissingKey_DoesNothing()
    {
        SeedWidgets(new Widget { Name = "Survivor" });

        var act = () => Commit(s => s.Widgets().DeleteById(-1));

        act.Should().NotThrow();
        InNewSession(s => s.Widgets().Count()).Should().Be(1);
    }

    [Fact]
    public void HardDeleteRange_RemovesEveryEntity()
    {
        var ids = SeedWidgets(new Widget(), new Widget(), new Widget()).Select(w => w.Id).ToList();

        Commit(s => s.Widgets().HardDeleteRange(s.Widgets().GetByIds(ids.Take(2))));

        InNewSession(s => s.Widgets().GetAll().Select(w => w.Id)).Should().Equal(ids[2]);
    }

    [Fact]
    public void GetById_ForAMissingKey_ReturnsNull()
    {
        InNewSession(s => s.Widgets().GetById(-1)).Should().BeNull();
    }

    [Fact]
    public void GetByIds_ReturnsTheEntitiesFound_AndSkipsMissingKeys()
    {
        var ids = SeedWidgets(new Widget { Name = "A" }, new Widget { Name = "B" }).Select(w => w.Id).ToList();

        var found = InNewSession(s => s.Widgets().GetByIds([ids[0], -1, ids[1]]).Select(w => w.Name).ToList());

        found.Should().BeEquivalentTo(["A", "B"]);
    }

    [Fact]
    public void ExistsById_Any_And_Count_ReflectCommittedRows()
    {
        var id = SeedWidgets(new Widget { Rank = 1 }, new Widget { Rank = 2 })[0].Id;

        InNewSession(s => s.Widgets().ExistsById(id)).Should().BeTrue();
        InNewSession(s => s.Widgets().ExistsById(-1)).Should().BeFalse();
        InNewSession(s => s.Widgets().Any(w => w.Rank == 2)).Should().BeTrue();
        InNewSession(s => s.Widgets().Any(w => w.Rank == 3)).Should().BeFalse();
        InNewSession(s => s.Widgets().Count(w => w.Rank > 0)).Should().Be(2);
    }

    [Fact]
    public void GetFirst_And_GetSingle_ThrowWhenNothingMatches()
    {
        var first = () => InNewSession(s => s.Widgets().GetFirst());
        var single = () => InNewSession(s => s.Widgets().GetSingle());

        first.Should().Throw<InvalidOperationException>();
        single.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetSingle_ThrowsWhenSeveralMatch_AndGetSingleOrDefaultToo()
    {
        SeedWidgets(new Widget { Rank = 1 }, new Widget { Rank = 1 });

        var single = () => InNewSession(s => s.Widgets().GetSingle(w => w.Rank == 1));
        var singleOrDefault = () => InNewSession(s => s.Widgets().GetSingleOrDefault(w => w.Rank == 1));

        single.Should().Throw<InvalidOperationException>();
        singleOrDefault.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void GetFirstOrDefault_And_GetSingleOrDefault_ReturnNullWhenNothingMatches()
    {
        InNewSession(s => s.Widgets().GetFirstOrDefault(w => w.Rank == 42)).Should().BeNull();
        InNewSession(s => s.Widgets().GetSingleOrDefault(w => w.Rank == 42)).Should().BeNull();
    }
}
