using FluentAssertions;
using RapidRepo.Entities;
using RapidRepo.Repositories;

namespace RapidRepo.Tests.Repositories;

public class PagingTests
{
    private sealed class Item : BaseEntity<int>;

    [Theory]
    [InlineData(0, 10)]
    [InlineData(1, 0)]
    [InlineData(-1, 10)]
    public void EnsureValid_RejectsPagesAndSizesBelowOne(int pageIndex, int pageSize)
    {
        var act = () => Paging.EnsureValid(pageIndex, pageSize);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void EnsureValid_RejectsAPageThatStartsBeyondIntMaxValue()
    {
        var act = () => Paging.EnsureValid(int.MaxValue, 2);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("pageIndex");
    }

    [Fact]
    public void EnsureValid_AcceptsTheLastAddressablePage()
    {
        var act = () => Paging.EnsureValid(2, int.MaxValue);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(1, 10, 0)]
    [InlineData(3, 10, 20)]
    [InlineData(2, 25, 25)]
    public void Offset_SkipsThePrecedingPages(int pageIndex, int pageSize, int expected) =>
        Paging.Offset(pageIndex, pageSize).Should().Be(expected);

    [Fact]
    public void DefaultOrder_SortsAscendingById_AndIsCached()
    {
        var order = Paging.DefaultOrder<Item, int>();

        order.Keys.Should().ContainSingle();
        order.Keys[0].Descending.Should().BeFalse();
        order.Keys[0].Key.Body.ToString().Should().EndWith(".Id");
        Paging.DefaultOrder<Item, int>().Should().BeSameAs(order);
    }

    [Fact]
    public void ToPage_CarriesTheCountsAndResults()
    {
        var page = Paging.ToPage(totalCount: 7, results: ["a", "b"], pageIndex: 2, pageSize: 2);

        page.Results.Should().Equal("a", "b");
        page.TotalCount.Should().Be(7);
        page.Page.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalPages.Should().Be(4);
    }
}
