using FluentAssertions;
using RapidRepo.Repositories;
using Xunit;

namespace RapidRepo.Testing.Conformance;

/// <summary>
/// Filtering, sorting, paging and projection through the neutral read contract.
/// </summary>
/// <typeparam name="TStore">The provider's store.</typeparam>
public abstract class QueryConformanceTests<TStore> : ConformanceTests<TStore>
    where TStore : IConformanceStore, new()
{
    private void SeedRanked() => SeedWidgets(
        new Widget { Name = "Delta", Rank = 2 },
        new Widget { Name = "Alpha", Rank = 1 },
        new Widget { Name = "Echo", Rank = 2 },
        new Widget { Name = "Bravo", Rank = 3 },
        new Widget { Name = "Charlie", Rank = 1 });

    [Fact]
    public void GetAll_WithACondition_ReturnsOnlyMatches()
    {
        SeedRanked();

        InNewSession(s => s.Widgets().GetAll(w => w.Rank == 2).Select(w => w.Name))
            .Should().BeEquivalentTo(["Delta", "Echo"]);
    }

    [Fact]
    public void GetAll_WithASortOrder_SortsByEveryKeyInTurn()
    {
        SeedRanked();

        var order = SortOrder<Widget>.ByDescending(w => w.Rank).ThenBy(w => w.Name);

        InNewSession(s => s.Widgets().GetAll(orderBy: order).Select(w => w.Name))
            .Should().Equal("Bravo", "Delta", "Echo", "Alpha", "Charlie");
    }

    [Fact]
    public void GetFirst_And_GetFirstOrDefault_FollowTheSortOrder()
    {
        SeedRanked();

        InNewSession(s => s.Widgets().GetFirst(orderBy: SortOrder<Widget>.By(w => w.Name)).Name).Should().Be("Alpha");
        InNewSession(s => s.Widgets().GetFirstOrDefault(w => w.Rank == 1, SortOrder<Widget>.ByDescending(w => w.Name))!.Name)
            .Should().Be("Charlie");
    }

    [Fact]
    public void GetAllPaged_ReturnsTheRequestedPage_AndTheTotal()
    {
        SeedRanked();

        var page = InNewSession(s => s.Widgets().GetAllPaged(orderBy: SortOrder<Widget>.By(w => w.Name), pageIndex: 2, pageSize: 2));

        page.Results.Select(w => w.Name).Should().Equal("Charlie", "Delta");
        page.TotalCount.Should().Be(5);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task GetAllPagedAsync_WithAProjectionAndACondition_PagesTheMatches()
    {
        SeedRanked();

        var page = await InNewSession(s => s.Widgets().GetAllPagedAsync(
            w => new WidgetName(w.Name),
            w => w.Rank < 3,
            SortOrder<Widget>.By(w => w.Name),
            pageIndex: 1,
            pageSize: 3));

        page.Results.Select(r => r.Name).Should().Equal("Alpha", "Charlie", "Delta");
        page.TotalCount.Should().Be(4);
    }

    [Fact]
    public async Task GetAllPaged_WithoutASortOrder_OrdersById()
    {
        var ids = SeedWidgets(
            new Widget { Name = "Zulu" }, new Widget { Name = "Alpha" }, new Widget { Name = "Mike" },
            new Widget { Name = "Bravo" }, new Widget { Name = "Yankee" })
            .Select(w => w.Id).Order().ToList();

        var first = InNewSession(s => s.Widgets().GetAllPaged(pageIndex: 1, pageSize: 2));
        var second = await InNewSession(s => s.Widgets().GetAllPagedAsync(w => new WidgetName(w.Name), pageIndex: 2, pageSize: 2));
        var last = InNewSession(s => s.Widgets().GetAllPaged(w => new WidgetName(w.Name), pageIndex: 3, pageSize: 2));

        first.Results.Select(w => w.Id).Should().Equal(ids[0], ids[1]);
        second.Results.Select(w => w.Name).Should().Equal("Mike", "Bravo");
        last.Results.Select(w => w.Name).Should().Equal("Yankee");
    }

    [Fact]
    public void GetAllPaged_PastTheLastPage_ReturnsAnEmptyPage()
    {
        SeedRanked();

        var page = InNewSession(s => s.Widgets().GetAllPaged(orderBy: SortOrder<Widget>.By(w => w.Name), pageIndex: 9, pageSize: 2));

        page.Results.Should().BeEmpty();
        page.TotalCount.Should().Be(5);
    }

    [Fact]
    public void GetAllPaged_RejectsAnInvalidPage()
    {
        var zeroIndex = () => InNewSession(s => s.Widgets().GetAllPaged(pageIndex: 0));
        var zeroSize = () => InNewSession(s => s.Widgets().GetAllPaged(pageSize: 0));

        zeroIndex.Should().Throw<ArgumentOutOfRangeException>();
        zeroSize.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Projections_ReturnTheSelectedShape()
    {
        var id = SeedWidgets(new Widget { Name = "Gear", Rank = 7 })[0].Id;

        InNewSession(s => s.Widgets().GetById(id, w => w.Rank)).Should().Be(7, "GetById projections may be scalar");
        InNewSession(s => s.Widgets().GetAll(w => new WidgetName(w.Name))).Should().Equal(new WidgetName("Gear"));
        InNewSession(s => s.Widgets().GetSingle(w => new { w.Name, w.Rank }, w => w.Id == id))
            .Should().BeEquivalentTo(new { Name = "Gear", Rank = 7 });
        InNewSession(s => s.Widgets().GetByIds([id], w => w.Name)).Should().Equal("Gear");
    }

    [Fact]
    public async Task AsyncReads_MatchTheirSynchronousCounterparts()
    {
        SeedRanked();
        var session = OpenSession();

        (await session.Widgets().CountAsync(w => w.Rank == 1)).Should().Be(2);
        (await session.Widgets().AnyAsync(w => w.Name == "Echo")).Should().BeTrue();
        (await session.Widgets().GetFirstAsync(orderBy: SortOrder<Widget>.ByDescending(w => w.Name))).Name.Should().Be("Echo");
        (await session.Widgets().GetAllAsync(w => w.Rank == 3)).Should().ContainSingle().Which.Name.Should().Be("Bravo");
        (await session.Widgets().GetSingleOrDefaultAsync(w => w.Name == "Nope")).Should().BeNull();
    }

    /// <summary>A reference-type projection; condition-bearing projections require one.</summary>
    public sealed record WidgetName(string Name);
}
